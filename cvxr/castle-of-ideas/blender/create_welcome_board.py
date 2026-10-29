#!/usr/bin/env python3
"""Typeset an original, replaceable 16:9 sign using installed fonts only."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
TITLE_FONT = Path('/System/Library/Fonts/Supplemental/STIXGeneral.otf')
BODY_FONT = Path('/System/Library/Fonts/Supplemental/Hoefler Text.ttc')


def fraktur(text):
    capitals = {2: 'ℭ', 7: 'ℌ', 8: 'ℑ', 17: 'ℜ', 25: 'ℨ'}
    return ''.join(capitals.get(ord(c)-65, chr(0x1D504+ord(c)-65)) if 'A' <= c <= 'Z'
                   else chr(0x1D51E+ord(c)-97) if 'a' <= c <= 'z' else c for c in text)


def main():
    out = ROOT / 'unity/Assets/CastleOfIdeas/Textures/Signs'
    out.mkdir(parents=True, exist_ok=True)
    image = Image.new('RGB', (1920,1080), (12,16,17))
    draw = ImageDraw.Draw(image)
    title = ImageFont.truetype(str(TITLE_FONT),150)
    body = ImageFont.truetype(str(BODY_FONT),55)
    gold = (220,185,104)
    draw.rectangle((55,55,1865,1025),outline=(119,95,48),width=3)
    spec = json.loads((ROOT/'design/welcome-board.json').read_text())
    lines = [(text,title,y) for text,y in zip(spec['titleLines'],(65,245))]
    lines += [(text,body,y) for text,y in zip(spec['directions'],(490,575))]
    lines += [(text,body,y) for text,y in zip(spec['programLines'],(730,810,905))]
    for text,font,y in lines:
        label = fraktur(text) if font is title else text
        bbox = draw.textbbox((0,0),label,font=font)
        x = (1920-(bbox[2]-bbox[0]))/2-bbox[0]
        if font is title:
            draw.text((x+3,y+3),label,font=font,fill=(80,56,18))
        draw.text((x,y),label,font=font,fill=gold)
    target=out/'welcome-board.png'
    image.save(target)
    provenance={'source':'Original text and deterministic Pillow typesetting; no external artwork',
        'size':[1920,1080], 'fonts':[
            {'file':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in (TITLE_FONT,BODY_FONT)],
        'titleStyle':'Installed STIX General mathematical Fraktur glyphs (blackletter)',
        'fontFilesRedistributed':False,'text':[line[0] for line in lines],
        'outputSha256':hashlib.sha256(target.read_bytes()).hexdigest()}
    (out/'provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2)+'\n')


if __name__ == '__main__':
    main()
