"""Independent bounded FFmpeg HLS receivers; no URLs, raw logs or media saved.

Each subprocess remuxes its own HLS to an in-memory MPEG-TS pipe, drained and
discarded by Python. Output bytes approximate media throughput, not HTTP/TLS
wire traffic. No decoding or CVR simulation. Ctrl+C/SIGTERM stops all children.
"""
from __future__ import annotations

import argparse
import collections
import datetime
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import threading
import time

ROOT = Path(__file__).resolve().parents[1]


def error_category(line):
    """Return a fixed label only; FFmpeg diagnostics frequently contain URLs."""
    line = re.sub(r'https?://\S+', '[url]', line)
    match = re.search(r'(?:HTTP error|Server returned)\s+(\d{3})\b', line, re.I)
    if match:
        return 'http_' + match[1]
    lower = line.lower()
    for needles, label in (
        (('timed out', 'timeout'), 'timeout'),
        (('failed to open segment', 'failed to reload playlist'), 'hls_fetch_error'),
        (('packet corrupt', 'corrupt input packet'), 'corrupt_packet'),
        (('non-monoton', 'timestamp discontinuity'), 'timestamp_warning'),
        (('error', 'invalid data', 'failed'), 'other_error'),
    ):
        if any(word in lower for word in needles):
            return label
    return None


class Receiver:
    def __init__(self, ffmpeg, role, number, url):
        self.role, self.number = role, number
        self.lock = threading.Lock()
        self.byte_count = 0
        self.media = None
        self.errors = collections.Counter()
        self.changed = time.monotonic()
        self.baseline = None
        self.max_gap = 0.0
        # Private URLs are arguments to the local process only; never print argv
        # or exception messages, nor store stderr verbatim.
        self.process = subprocess.Popen([
            ffmpeg, '-hide_banner', '-nostdin', '-nostats', '-loglevel', 'warning',
            '-rw_timeout', '15000000', '-i', url,
            '-map', '0:v:0', '-map', '0:a:0?', '-c', 'copy',
            '-f', 'mpegts', 'pipe:1', '-progress', 'pipe:2', '-stats_period', '1',
        ], stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
        self.threads = [threading.Thread(target=self.drain, daemon=True),
                        threading.Thread(target=self.progress, daemon=True)]
        for thread in self.threads:
            thread.start()

    def drain(self):
        while block := self.process.stdout.read(65536):
            with self.lock:
                self.byte_count += len(block)

    def progress(self):
        for raw in self.process.stderr:
            line = raw.decode('utf-8', errors='replace').strip()
            if line.startswith('out_time_us='):
                try:
                    value = int(line.split('=', 1)[1]) / 1_000_000
                except ValueError:
                    continue
                with self.lock:
                    if value != self.media:
                        self.changed = time.monotonic()
                    self.media = value
            else:
                category = error_category(line)
                if category:
                    with self.lock:
                        self.errors[category] += 1

    def sample(self, now, measured=False):
        with self.lock:
            if measured:
                self.max_gap = max(self.max_gap, now-self.changed)
            return {'role': self.role, 'number': self.number, 'bytes': self.byte_count,
                    'mediaSeconds': self.media, 'secondsSinceMediaAdvance': round(now-self.changed, 3),
                    'maxObservedMediaGapSeconds': round(self.max_gap, 3),
                    'errorLogLines': dict(self.errors), 'returnCode': self.process.poll()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--program-clients', type=int, default=9)
    parser.add_argument('--portal-clients', type=int, default=8)
    parser.add_argument('--seconds', type=float, default=180)
    parser.add_argument('--warmup-seconds', type=float, default=20)
    parser.add_argument('--report-dir', type=Path, default=ROOT/'build/ffmpeg-hls-load-test')
    args = parser.parse_args()
    if not (0 <= args.program_clients <= 25 and 0 <= args.portal_clients <= 25
            and 1 <= args.program_clients+args.portal_clients <= 25
            and 5 <= args.seconds <= 600 and 0 <= args.warmup_seconds < args.seconds):
        parser.error('Use 1–25 total receivers, 5–600 seconds, and a shorter warmup.')
    ffmpeg = shutil.which('ffmpeg')
    if not ffmpeg:
        parser.error('ffmpeg is not installed.')
    config = json.loads((ROOT/'streaming.local.json').read_text())
    roles = [('program', args.program_clients, config.get('receiverUrl')),
             ('portal', args.portal_clients, config.get('portalReceiverUrl'))]
    if any(count and (not url or not url.startswith('https://')) for _, count, url in roles):
        parser.error('Configure both requested HTTPS HLS receiver URLs in streaming.local.json.')
    args.report_dir.mkdir(parents=True, exist_ok=True)
    pid_file = args.report_dir/'run.pid'
    pid_file.write_text(str(os.getpid())+'\n')
    stop = threading.Event()
    signal.signal(signal.SIGINT, lambda *_: stop.set())
    signal.signal(signal.SIGTERM, lambda *_: stop.set())
    start = time.monotonic()
    utc = datetime.datetime.now(datetime.timezone.utc).isoformat()
    receivers, samples = [], []
    reason, baseline_time, baseline_bytes, baseline_media = 'duration', None, None, {}
    print(f'FFmpeg HLS receivers: program={args.program_clients}, portal={args.portal_clients}; '
          f'{args.seconds:g}s after launch; URLs and raw logs are never printed.', flush=True)
    print(f'Stop: Ctrl+C or kill -INT {os.getpid()}', flush=True)
    try:
        # One separately launched FFmpeg per viewer/feed. Briefly stagger joins.
        for role, count, url in roles:
            for number in range(1, count+1):
                if stop.is_set():
                    break
                receivers.append(Receiver(ffmpeg, role, number, url))
                stop.wait(.15)
        start = time.monotonic()
        next_sample = start
        while not stop.is_set():
            now = time.monotonic()
            elapsed = now-start
            rows = [r.sample(now, elapsed >= args.warmup_seconds) for r in receivers]
            total = sum(row['bytes'] for row in rows)
            if baseline_time is None and elapsed >= args.warmup_seconds:
                baseline_time, baseline_bytes = now, total
                baseline_media = {(r['role'], r['number']): r['mediaSeconds'] for r in rows}
            if now >= next_sample:
                active = collections.Counter(r['role'] for r in rows if r['returnCode'] is None)
                samples.append({'elapsedSeconds': round(elapsed, 3), 'bytes': total, 'receivers': rows})
                mbps = (total-baseline_bytes)*8/(now-baseline_time)/1e6 if baseline_time and now>baseline_time else None
                suffix = f'{mbps:.2f} Mbps media' if mbps is not None else 'warming up'
                print(f'{elapsed:6.1f}s active program={active["program"]} portal={active["portal"]}; {suffix}', flush=True)
                next_sample = now+10
            if any(row['returnCode'] is not None for row in rows):
                reason = 'receiver_exited'
                break
            if total >= 3_000_000_000:
                reason = '3GB_output_limit'
                break
            if elapsed >= args.seconds:
                break
            stop.wait(.25)
        if stop.is_set():
            reason = 'signal'
    except Exception as exception:
        # No argv/URL-bearing error text escapes the harness.
        reason = 'harness_error_'+type(exception).__name__
    finally:
        end = time.monotonic()
        final = [r.sample(end, baseline_time is not None) for r in receivers]
        # Only signal the process groups created by this harness, never other FFmpeg jobs.
        for sig, timeout in ((signal.SIGINT, 3), (signal.SIGTERM, 2), (signal.SIGKILL, 2)):
            for receiver in receivers:
                if receiver.process.poll() is None:
                    try:
                        os.killpg(receiver.process.pid, sig)
                    except ProcessLookupError:
                        pass
            deadline = time.monotonic()+timeout
            for receiver in receivers:
                try:
                    receiver.process.wait(timeout=max(.01, deadline-time.monotonic()))
                except subprocess.TimeoutExpired:
                    pass
        for receiver in receivers:
            for thread in receiver.threads:
                thread.join(timeout=1)
        for row, receiver in zip(final, receivers):
            row['stoppedReturnCode'] = receiver.process.poll()
            before = baseline_media.get((row['role'], row['number']))
            if baseline_time and before is not None and row['mediaSeconds'] is not None:
                media_advance = row['mediaSeconds']-before
                wall_advance = end-baseline_time
                row['steadyMediaSeconds'] = round(media_advance, 3)
                row['steadyWallSeconds'] = round(wall_advance, 3)
                row['mediaClockRatio'] = round(media_advance/wall_advance, 4) if wall_advance else None
                row['mediaLagGrowthSeconds'] = round(wall_advance-media_advance, 3)
        total = sum(row['bytes'] for row in final)
        report = {'startedAtUtc': utc, 'stopReason': reason,
                  'requested': {'program': args.program_clients, 'portal': args.portal_clients},
                  'elapsedSeconds': round(end-start, 3), 'warmupSeconds': args.warmup_seconds,
                  'measurement': 'FFmpeg remuxed MPEG-TS output bytes; excludes HTTP/TLS overhead; no decode',
                  'bytes': total,
                  'steadyMediaMbps': round((total-baseline_bytes)*8/(end-baseline_time)/1e6, 3) if baseline_time and end>baseline_time else None,
                  'receivers': final, 'samples': samples,
                  'allChildrenStopped': all(r.process.poll() is not None for r in receivers)}
        (args.report_dir/'summary.json').write_text(json.dumps(report, indent=2)+'\n')
        pid_file.unlink(missing_ok=True)
        print(f'Finished: {reason}; all FFmpeg stopped={report["allChildrenStopped"]}; '
              f'report={args.report_dir/"summary.json"}', flush=True)
    return 0 if reason in ('duration', 'signal') and report['allChildrenStopped'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
