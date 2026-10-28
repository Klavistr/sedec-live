"""Fetch the historical Tenniel collection from Project Gutenberg eBook 114.

Chapter assignments follow the scenes in Carroll's original novel. The
frontispiece (courtroom, illustration 1) belongs with chapter XI.
"""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parent.parent
CHAPTERS = {1: 11, **dict.fromkeys(range(2,5),1), **dict.fromkeys(range(5,9),2),
            **dict.fromkeys(range(9,11),3), **dict.fromkeys(range(11,15),4),
            **dict.fromkeys(range(15,20),5), **dict.fromkeys(range(20,25),6),
            **dict.fromkeys(range(25,28),7), **dict.fromkeys(range(28,32),8),
            **dict.fromkeys(range(32,35),9), **dict.fromkeys(range(35,37),10),
            **dict.fromkeys(range(37,40),11), **dict.fromkeys(range(40,43),12)}


def main():
    directory = ROOT / "references/alice/tenniel"
    directory.mkdir(parents=True, exist_ok=True)
    records = []
    for number in range(1,43):
        name = f"alice{number:02d}a.gif"
        url = "https://www.gutenberg.org/files/114/114-h/images/"+name
        path = directory/name
        if not path.exists():
            # Use the OS curl certificate store on macOS, where a standalone
            # Python installation may not have its optional CA bundle installed.
            data = subprocess.run(["curl","--fail","--location","--silent","--show-error",
                                   "--retry","2","--max-time","45",url],
                                  check=True,capture_output=True).stdout
            if not data.startswith((b"GIF87a", b"GIF89a")):
                raise ValueError(f"Not an original GIF: {url}")
            path.write_bytes(data)
        records.append({"number":number, "chapter":CHAPTERS[number],
                        "file":"tenniel/"+name, "url":url,
                        "sha256":hashlib.sha256(path.read_bytes()).hexdigest()})
    (directory.parent/"illustrations.json").write_text(json.dumps({
        "author":"John Tenniel", "source":"https://www.gutenberg.org/ebooks/114",
        "rightsStatement":"../illustrations-license.txt", "illustrations":records},indent=2)+"\n")
    print(f"Verified {len(records)} original illustrations and their chapter assignments")


if __name__ == "__main__":
    main()
