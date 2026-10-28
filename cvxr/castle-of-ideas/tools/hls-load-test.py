"""Bounded independent HLS consumers; standard library only, no media retained.

Reads the private receiver URL from streaming.local.json. Never prints or saves
URLs, playlist contents or media. Ctrl+C/SIGTERM stop the run and write a report.
"""
from __future__ import annotations
import argparse
import collections
import datetime
import http.client
import json
import os
from pathlib import Path
import signal
import ssl
import statistics
import threading
import time
import urllib.parse

ROOT = Path(__file__).resolve().parents[1]


def parse_playlist(text, base):
    sequence, target, duration, segments, init = 0, 4.0, None, [], None
    for line in text.splitlines():
        line = line.strip()
        if line.startswith("#EXT-X-MEDIA-SEQUENCE:"): sequence = int(line.split(":",1)[1])
        elif line.startswith("#EXT-X-TARGETDURATION:"): target = float(line.split(":",1)[1])
        elif line.startswith("#EXTINF:"): duration = float(line.split(":",1)[1].split(",")[0])
        elif line.startswith("#EXT-X-STREAM-INF:"):
            raise ValueError("Use a media playlist, not an adaptive master playlist")
        elif line.startswith("#EXT-X-MAP:"):
            import re
            found = re.search(r'URI="([^"]+)"',line)
            if found: init = urllib.parse.urljoin(base,found[1])
        elif line and not line.startswith("#") and duration is not None:
            segments.append((sequence+len(segments),duration,urllib.parse.urljoin(base,line)))
            duration = None
    if not text.startswith("#EXTM3U") or not segments:
        raise ValueError("No HLS media segments in receiver response")
    return target,segments,init


class Run:
    def __init__(self, directory, hold, single_clients=None):
        self.directory,self.hold = directory,hold
        self.single_clients=single_clients
        self.stop = threading.Event()
        self.lock = threading.Lock()
        self.phase = "starting"
        self.data = collections.defaultdict(lambda: {"bytes":0,"requests":0,"httpStatuses":collections.Counter(),
            "errors":collections.Counter(),"segmentSeconds":[],"slowSegments":0,"missedSegments":0,
            "stalePlaylistEvents":0,"mediaDurationSeconds":0.0,"segmentBytes":0})
        self.start = time.monotonic()
        self.utc = datetime.datetime.now(datetime.timezone.utc).isoformat()
        self.context = ssl.create_default_context(cafile="/etc/ssl/cert.pem" if Path("/etc/ssl/cert.pem").exists() else None)
        self.phases = []
        self.total_bytes = 0

    def error(self, kind):
        with self.lock: self.data[self.phase]["errors"][kind]+=1

    def consumer(self, receiver, number):
        connections = {}
        last, init_seen, last_new, stale = None,set(),time.monotonic(),False
        def fetch(url, playlist=False):
            parsed = urllib.parse.urlsplit(url)
            if parsed.scheme != "https": raise ValueError("Only HTTPS resources are allowed")
            key = (parsed.hostname,parsed.port or 443)
            if key not in connections:
                connections[key] = http.client.HTTPSConnection(*key,context=self.context,timeout=15)
            connection = connections[key]
            path = urllib.parse.urlunsplit(("","",parsed.path,parsed.query,""))
            began, count, chunks = time.monotonic(),0,[]
            try:
                connection.request("GET",path,headers={"User-Agent":"CastleOfIdeas-HLS-LoadTest/1.0",
                                                       "Cache-Control":"no-cache" if playlist else "max-age=0"})
                response = connection.getresponse()
                status = response.status
                with self.lock:
                    row=self.data[self.phase]
                    row["requests"]+=1;row["httpStatuses"][str(status)]+=1
                while not self.stop.is_set():
                    chunk = response.read(65536)
                    if not chunk:break
                    count+=len(chunk)
                    with self.lock:
                        self.data[self.phase]["bytes"]+=len(chunk)
                        self.total_bytes+=len(chunk)
                        if self.total_bytes>6_000_000_000:self.stop.set()
                    if playlist:
                        if count>2_000_000:raise ValueError("Playlist exceeded size limit")
                        chunks.append(chunk)
                elapsed=time.monotonic()-began
                if self.stop.is_set():raise InterruptedError()
                if status not in (200,206):raise RuntimeError("HTTP "+str(status))
                return b"".join(chunks).decode("utf-8-sig") if playlist else count,elapsed
            except Exception:
                connection.close();connections.pop(key,None)
                raise
        try:
            # Small initial staggering mimics independent viewers joining.
            if self.stop.wait((number%5)*.09):return
            while not self.stop.is_set():
                try:
                    text,_=fetch(receiver,True)
                    if self.stop.is_set():break
                    target,segments,init=parse_playlist(text,receiver)
                    if init and init not in init_seen:
                        fetch(init);init_seen.add(init)
                    if last is None:last=segments[-1][0]-2
                    if segments[0][0]>last+1:
                        with self.lock:self.data[self.phase]["missedSegments"]+=segments[0][0]-last-1
                        last=segments[0][0]-1
                    for sequence,duration,url in segments:
                        if self.stop.is_set():break
                        if sequence<=last:continue
                        count,elapsed=fetch(url)
                        with self.lock:
                            row=self.data[self.phase]
                            row["segmentSeconds"].append(elapsed)
                            row["segmentBytes"]+=count;row["mediaDurationSeconds"]+=duration
                            if elapsed>duration:row["slowSegments"]+=1
                        last=sequence;last_new=time.monotonic();stale=False
                    if time.monotonic()-last_new>max(12,target*3) and not stale:
                        with self.lock:self.data[self.phase]["stalePlaylistEvents"]+=1
                        stale=True
                    self.stop.wait(max(1,min(target/2,3)))
                except Exception as error:
                    if self.stop.is_set():break
                    # Exception messages may contain URLs; retain only safe type/status.
                    label = str(error) if type(error) is RuntimeError and str(error).startswith("HTTP ") else type(error).__name__
                    self.error(label);self.stop.wait(1)
        finally:
            for connection in connections.values():connection.close()

    def report(self, complete=False):
        with self.lock:
            phases=[]
            for phase in self.phases:
                values=self.data[phase["name"]]
                times=sorted(values["segmentSeconds"])
                elapsed=min(time.monotonic(),phase.get("end",time.monotonic()))-phase["start"]
                row={k:v for k,v in values.items() if k!="segmentSeconds"}
                row.update({"name":phase["name"],"clients":phase["clients"],"elapsedSeconds":round(elapsed,2),
                    "aggregateMbps":round(values["bytes"]*8/max(elapsed,.001)/1e6,2),
                    "segments":len(times),"segmentDownloadP50Seconds":round(statistics.median(times),3) if times else None,
                    "segmentDownloadP95Seconds":round(times[min(len(times)-1,int(len(times)*.95))],3) if times else None,
                    "observedStreamMbps":round(values["segmentBytes"]*8/max(values["mediaDurationSeconds"],.001)/1e6,2)})
                phases.append(row)
            report={"startedUtc":self.utc,"status":"complete" if complete else "running-or-interrupted",
                    "elapsedSeconds":round(time.monotonic()-self.start,2),"totalBytes":self.total_bytes,
                    "independentClients":True,"mediaSaved":False,"receiverUrlLogged":False,
                    "caveat":"One Mac and one downstream connection; local network saturation cannot establish VPS capacity beyond this test.",
                    "phases":phases}
        self.directory.mkdir(parents=True,exist_ok=True)
        temporary=self.directory/"summary.tmp"
        temporary.write_text(json.dumps(report,indent=2)+"\n")
        temporary.replace(self.directory/"summary.json")
        return report

    def run(self, receiver):
        self.directory.mkdir(parents=True,exist_ok=True)
        (self.directory/"process.pid").write_text(str(os.getpid())+"\n")
        threads=[]
        completed=False
        try:
            profile = ((self.single_clients,self.hold),) if self.single_clients else ((1,15),(5,15),(10,15),(20,15),(25,self.hold))
            for clients,seconds in profile:
                if self.stop.is_set():break
                self.phase=f"{clients}-clients"
                phase={"name":self.phase,"clients":clients,"start":time.monotonic()}
                self.phases.append(phase)
                while len(threads)<clients:
                    thread=threading.Thread(target=self.consumer,args=(receiver,len(threads)+1),daemon=True)
                    threads.append(thread);thread.start()
                print(f"{clients} independent HLS clients for {seconds}s",flush=True)
                deadline=time.monotonic()+seconds
                while time.monotonic()<deadline and not self.stop.wait(min(5,max(0,deadline-time.monotonic()))):
                    self.report()
                phase["end"]=time.monotonic()
                row=self.report()["phases"][-1]
                print(f"  {row['aggregateMbps']} Mbps; {row['segments']} segments; slow={row['slowSegments']}; errors={row['errors']}",flush=True)
            completed=not self.stop.is_set()
        finally:
            self.stop.set()
            for thread in threads:thread.join(timeout=1)
            self.report(completed)
            print("Finished. URL-free report: "+str(self.directory/"summary.json"),flush=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--hold-seconds",type=int,default=90)
    parser.add_argument("--report-dir",type=Path,default=ROOT/"build/hls-load-test")
    parser.add_argument("--single-clients",type=int,choices=range(1,26))
    args=parser.parse_args()
    if not 15<=args.hold_seconds<=300:parser.error("Hold duration must be 15..300 seconds")
    receiver=json.loads((ROOT/"streaming.local.json").read_text())["receiverUrl"].strip()
    run=Run(args.report_dir,args.hold_seconds,args.single_clients)
    def stop(*_):run.stop.set()
    signal.signal(signal.SIGINT,stop);signal.signal(signal.SIGTERM,stop)
    print("HLS load test: 1 -> 5 -> 10 -> 20 -> 25 clients. Ctrl+C stops safely.",flush=True)
    run.run(receiver)


if __name__=="__main__":main()
