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


def rehearsal_load(plan):
    """Translate actual/target viewers into the missing independent feed requests."""
    result = {}
    for stage in ('target', 'actual'):
        group = plan.get(stage) if isinstance(plan, dict) else None
        if not isinstance(group, dict):
            raise ValueError('Plan needs target and actual objects.')
        counts = {}
        for key in ('dualFeedClients', 'programOnlyClients'):
            value = group.get(key)
            if type(value) is not int or not 0 <= value <= 25:
                raise ValueError('Viewer counts must be integers from 0 to 25.')
            counts[key] = value
        result[stage] = {
            **counts,
            'program': counts['dualFeedClients'] + counts['programOnlyClients'],
            'portal': counts['dualFeedClients'],
        }
    result['synthetic'] = {
        role: max(0, result['target'][role] - result['actual'][role])
        for role in ('program', 'portal')
    }
    return result


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


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--program-clients', type=int)
    parser.add_argument('--portal-clients', type=int)
    parser.add_argument('--rehearsal-plan', type=Path,
                        help='Local JSON with target/actual dualFeedClients and programOnlyClients.')
    parser.add_argument('--dry-run', action='store_true', help='Show counts without network or files.')
    parser.add_argument('--seconds', type=float, default=180)
    parser.add_argument('--warmup-seconds', type=float, default=20)
    parser.add_argument('--max-output-gb', type=float, default=3,
                        help='Stop after this many decimal GB of discarded remux output (default: 3).')
    parser.add_argument('--report-dir', type=Path, default=ROOT/'build/ffmpeg-hls-load-test')
    args = parser.parse_args(argv)
    plan = None
    if args.rehearsal_plan:
        if args.program_clients is not None or args.portal_clients is not None:
            parser.error('Use either --rehearsal-plan or explicit receiver counts, not both.')
        try:
            plan = rehearsal_load(json.loads(args.rehearsal_plan.read_text()))
        except (OSError, ValueError):
            parser.error('Invalid rehearsal plan: provide target/actual integer viewer counts (0–25).')
        args.program_clients = plan['synthetic']['program']
        args.portal_clients = plan['synthetic']['portal']
    else:
        args.program_clients = 9 if args.program_clients is None else args.program_clients
        args.portal_clients = 8 if args.portal_clients is None else args.portal_clients
    if not (0 <= args.program_clients <= 25 and 0 <= args.portal_clients <= 25
            and 0 <= args.program_clients+args.portal_clients <= 25
            and 5 <= args.seconds <= 7200 and 0 <= args.warmup_seconds < args.seconds
            and .001 <= args.max_output_gb <= 50):
        parser.error('Use 0–25 receivers, 5–7200 seconds, a shorter warmup, and 0.001–50 GB.')
    if plan:
        for stage in ('target', 'actual'):
            row = plan[stage]
            print(f'{stage}: dual-feed={row["dualFeedClients"]}, program-only={row["programOnlyClients"]}; '
                  f'program={row["program"]}, portal={row["portal"]}', flush=True)
    print(f'Synthetic receivers: program={args.program_clients}, portal={args.portal_clients}', flush=True)
    if args.dry_run or not (args.program_clients + args.portal_clients):
        print('No receivers started.' if args.dry_run else 'Target already covered; no receivers needed.', flush=True)
        return 0
    ffmpeg = shutil.which('ffmpeg')
    if not ffmpeg:
        parser.error('ffmpeg is not installed.')
    try:
        config = json.loads((ROOT/'streaming.local.json').read_text())
        if not isinstance(config, dict):
            raise ValueError()
    except (OSError, ValueError):
        parser.error('Cannot read streaming.local.json. Configure the local receiver URLs first.')
    roles = [('program', args.program_clients, config.get('receiverUrl')),
             ('portal', args.portal_clients, config.get('portalReceiverUrl'))]
    if any(count and (not url or not url.startswith('https://')) for _, count, url in roles):
        parser.error('Configure both requested HTTPS HLS receiver URLs in streaming.local.json.')
    args.report_dir.mkdir(parents=True, exist_ok=True)
    pid_file = args.report_dir/'run.pid'
    try:
        with pid_file.open('x') as file:
            file.write(str(os.getpid())+'\n')
    except FileExistsError:
        parser.error('run.pid already exists in this report directory. Stop the existing run first; '
                     'remove a stale PID file only after confirming that run has stopped.')
    stop = threading.Event()
    signal.signal(signal.SIGINT, lambda *_: stop.set())
    signal.signal(signal.SIGTERM, lambda *_: stop.set())
    if hasattr(signal, 'SIGHUP'):
        signal.signal(signal.SIGHUP, lambda *_: stop.set())
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
                errors = sum(sum(r['errorLogLines'].values()) for r in rows)
                stalled = sum(r['secondsSinceMediaAdvance'] >= 15 for r in rows) if baseline_time else 0
                samples.append({'elapsedSeconds': round(elapsed, 3), 'bytes': total, 'receivers': rows})
                mbps = (total-baseline_bytes)*8/(now-baseline_time)/1e6 if baseline_time and now>baseline_time else None
                suffix = f'{mbps:.2f} Mbps media' if mbps is not None else 'warming up'
                print(f'{elapsed:6.1f}s active program={active["program"]} portal={active["portal"]}; '
                      f'{suffix}; error lines={errors}; stalled>=15s={stalled}', flush=True)
                next_sample = now+10
            if any(row['returnCode'] is not None for row in rows):
                reason = 'receiver_exited'
                break
            if total >= args.max_output_gb * 1_000_000_000:
                reason = 'output_limit'
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
                  'rehearsal': plan,
                  'limits': {'seconds': args.seconds, 'maxOutputGB': args.max_output_gb},
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
