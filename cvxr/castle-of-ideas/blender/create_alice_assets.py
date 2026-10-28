"""Typeset public-domain Alice text and reproduce sourced Tenniel illustrations.

Uses Pillow and locally installed fonts; font software is never copied. This is
ordinary typesetting and deterministic image processing, not image generation.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps
from alice_book_wallpaper import clean_literary_text

ROOT = Path(__file__).resolve().parent.parent
FONTS = {
    "Hoefler Text": "Hoefler Text.ttc",
    "Iowan Old Style": "Iowan Old Style.ttc",
    "Crimson": "Crimson-Roman.otf",
    "Baskerville": "Baskerville.ttc",
    "Big Caslon": "BigCaslon.ttf",
}
SAMPLE = "Alice was beginning to get very tired of sitting by her sister on the bank, and of having nothing to do: once or twice she had peeped into the book her sister was reading, but it had no pictures or conversations in it."
SOURCES = {
    "pg11-source.txt": "https://www.gutenberg.org/cache/epub/11/pg11.txt",
    "white-rabbit.gif": "https://www.gutenberg.org/files/114/114-h/images/alice02a.gif",
    "cheshire-cat.gif": "https://www.gutenberg.org/files/114/114-h/images/alice23a.gif",
    "tea-party.gif": "https://www.gutenberg.org/files/114/114-h/images/alice25a.gif",
    "illustrations-license.txt": "https://www.gutenberg.org/files/114/LICENSE.txt",
}


def font_path(name: str) -> Path:
    for root in (Path("/System/Library/Fonts/Supplemental"), Path("/Library/Fonts"), Path.home() / "Library/Fonts"):
        path = root / FONTS[name]
        if path.exists():
            return path
    raise FileNotFoundError(f"Install or supply the selected local font: {name}")


def wrapped(text: str, font, width: int) -> list[str]:
    lines = []
    for paragraph in text.split("\n"):
        line = ""
        for word in paragraph.split():
            candidate = (line + " " + word).strip()
            if font.getlength(candidate) > width and line:
                lines.append(line)
                line = word
            else:
                line = candidate
        lines.append(line)
    return lines


def font_proof(path: Path) -> None:
    image = Image.new("RGB", (1600, 1750), (246, 242, 230))
    draw = ImageDraw.Draw(image)
    label_font = ImageFont.truetype(str(font_path("Crimson")), 25)
    descriptions = {
        "Hoefler Text": "Old-style book face / recommended for the wall text",
        "Iowan Old Style": "Warm, open shapes / sturdy and readable",
        "Crimson": "Literary, delicate texture / open-source book face",
        "Baskerville": "Transitional serif / crisp, formal page rhythm",
        "Big Caslon": "Display cut / strong chapter headings and large quotations",
    }
    for index, name in enumerate(FONTS):
        top = 35 + index * 340
        font = ImageFont.truetype(str(font_path(name)), 36)
        title = ImageFont.truetype(str(font_path(name)), 52)
        draw.text((50, top), f"{index + 1}. {name}", fill="#8d4e4b", font=label_font)
        draw.text((50, top + 38), "Alice’s Adventures in Wonderland", fill="#292722", font=title)
        for row, line in enumerate(wrapped(SAMPLE, font, 1470)):
            draw.text((50, top + 112 + row * 45), line, fill="#292722", font=font)
        draw.text((50, top + 277), descriptions[name], fill="#766e60", font=label_font)
        draw.line((50, top + 320, 1550, top + 320), fill="#c2b6a0")
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def create_assets(font_name: str) -> None:
    source_dir = ROOT / "references/alice"
    output_dir = ROOT / "unity/Assets/CastleOfIdeas/Textures/AliceLiterature"
    output_dir.mkdir(parents=True, exist_ok=True)
    paper = (247, 244, 234)
    ink = (42, 40, 36)
    records = []
    for name in ("white-rabbit", "cheshire-cat", "tea-party"):
        source = source_dir / (name + ".gif")
        image = Image.open(source).convert("L")
        image = ImageOps.autocontrast(image)
        image = ImageOps.colorize(image, ink, paper)
        canvas = Image.new("RGB", (1024, 1024), paper)
        image = ImageOps.contain(image, (930, 930), Image.Resampling.LANCZOS)
        canvas.paste(image, ((1024 - image.width) // 2, (1024 - image.height) // 2))
        target = output_dir / (name + ".png")
        canvas.save(target)
        records.append({"name": name, "kind": "illustration", "source": source.name, "sourceUrl": SOURCES[source.name], "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(), "outputSha256": hashlib.sha256(target.read_bytes()).hexdigest(), "processing": "autocontrast; black ink/cream paper mapping; proportional resize, no crop"})
    raw = (source_dir / "pg11-source.txt").read_text(encoding="utf-8-sig")
    body = raw.split("*** START OF THE PROJECT GUTENBERG EBOOK", 1)[1].split("***", 1)[1]
    body = body.split("*** END OF THE PROJECT GUTENBERG EBOOK", 1)[0]
    body = body[body.index("Alice was beginning"):]
    definitions = [
        ("text-rabbit-hole", "DOWN THE RABBIT-HOLE", "Alice was beginning"),
        ("text-tea-party", "A MAD TEA-PARTY", "There was a table set out"),
        ("text-curious", "CURIOUSER AND CURIOUSER", "“Curiouser and curiouser!"),
    ]
    font = ImageFont.truetype(str(font_path(font_name)), 38)
    title_font = ImageFont.truetype(str(font_path(font_name)), 46)
    excerpt_dir = source_dir / "excerpts"
    excerpt_dir.mkdir(exist_ok=True)
    for name, title, start in definitions:
        start_index = body.find(start)
        if start_index < 0:
            raise ValueError(f"Passage not found: {start}")
        passage = body[start_index:start_index + 1800].replace("_", "")
        passage = clean_literary_text(passage)
        lines = wrapped(passage, font, 850)[:31]
        canvas = Image.new("RGB", (1024, 1536), paper)
        draw = ImageDraw.Draw(canvas)
        draw.text((87, 66), title, fill=ink, font=title_font)
        draw.line((87, 133, 937, 133), fill=ink, width=2)
        for index, line in enumerate(lines):
            draw.text((87, 175 + index * 40), line, fill=ink, font=font)
        draw.text((87, 1450), "LEWIS CARROLL", fill=ink, font=font)
        target = output_dir / (name + ".png")
        canvas.save(target)
        # Preserve exact source characters separately from the raster typesetting.
        excerpt_file = excerpt_dir / (name + ".txt")
        excerpt_file.write_text("\n".join(lines) + "\n", encoding="utf-8")
        records.append({"name": name, "kind": "text", "sourceUrl": SOURCES["pg11-source.txt"], "sourceSha256": hashlib.sha256((source_dir / "pg11-source.txt").read_bytes()).hexdigest(), "sourceCharacterOffsetFromOpening": start_index, "excerpt": str(excerpt_file.relative_to(ROOT)), "fontFamily": font_name, "fontFile": font_path(font_name).name, "fontSha256": hashlib.sha256(font_path(font_name).read_bytes()).hexdigest(), "outputSha256": hashlib.sha256(target.read_bytes()).hexdigest()})
    records.extend(create_wallpaper(body, font_name, source_dir, output_dir))
    (output_dir / "literature-provenance.json").write_text(json.dumps({"schemaVersion": 2, "selectedFont": font_name, "fontRedistributed": False, "generatedImagery": False, "records": records}, indent=2) + "\n")
    print(f"Created {len(records)} Alice illustration/text textures using {font_name}")


def create_wallpaper(body: str, font_name: str, source_dir: Path, output_dir: Path) -> list[dict]:
    from alice_book_wallpaper import create_book_wallpaper
    return create_book_wallpaper(ROOT, font_name, str(font_path(font_name)), source_dir, output_dir)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font-proof", type=Path)
    parser.add_argument("--font", choices=FONTS, default="Hoefler Text")
    parser.add_argument("--build", action="store_true")
    args = parser.parse_args()
    if args.font_proof:
        font_proof(args.font_proof)
    if args.build:
        create_assets(args.font)


if __name__ == "__main__":
    main()
