"""Extract traceable textures from operator-supplied venue photographs.

No image generation/inpainting is used. Every RGB pixel comes from a recorded
crop, optionally resampled, mirrored, tiled or exposure-adjusted. Requires Pillow.
HEIC conversion uses macOS sips; on other platforms supply oriented JPEG copies
alongside the original HEIC files (the original hash is still recorded).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parent.parent


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def open_photo(path: Path, temporary: Path) -> Image.Image:
    jpeg = path.with_suffix(".jpg")
    if not jpeg.exists():
        jpeg = temporary / (path.stem + ".jpg")
        subprocess.run(["sips", "-s", "format", "jpeg", str(path), "--out", str(jpeg)], check=True, capture_output=True)
    with Image.open(jpeg) as source:
        return ImageOps.exif_transpose(source).convert("RGB")


def crop_photo(photo: Image.Image, recipe: dict) -> Image.Image:
    width, height = photo.size
    if "quad" in recipe:
        # Coordinates remain TL, TR, BR, BL in the inspectable manifest.
        # Solve a projective homography: straight pattern borders
        # remain straight when rectifying a photographed square or floor plank.
        points = recipe["quad"]
        out_w, out_h = recipe["size"]
        matrix = []
        for (x,y), (u,v) in zip(((0,0),(out_w,0),(out_w,out_h),(0,out_h)), points):
            u, v = u*width, v*height
            matrix.extend([[x,y,1,0,0,0,-u*x,-u*y,u], [0,0,0,x,y,1,-v*x,-v*y,v]])
        for column in range(8):
            pivot = max(range(column,8), key=lambda row: abs(matrix[row][column]))
            matrix[column], matrix[pivot] = matrix[pivot], matrix[column]
            divisor = matrix[column][column]
            if abs(divisor) < 1e-12:
                raise ValueError("Degenerate texture crop quadrilateral")
            matrix[column] = [value/divisor for value in matrix[column]]
            for row in range(8):
                if row != column:
                    factor = matrix[row][column]
                    matrix[row] = [a-factor*b for a,b in zip(matrix[row],matrix[column])]
        return photo.transform((out_w,out_h), Image.Transform.PERSPECTIVE,
                               tuple(row[8] for row in matrix), Image.Resampling.BICUBIC)
    left, top, right, bottom = recipe["rect"]
    return photo.crop((round(left * width), round(top * height), round(right * width), round(bottom * height)))


def assemble(crop: Image.Image, recipe: dict) -> Image.Image:
    size = tuple(recipe["size"])
    mode = recipe["assembly"]
    if mode == "single":
        result = crop.resize(size, Image.Resampling.LANCZOS)
    else:
        # Opposite edge continuity is obtained by reflecting the supplied pixels.
        half = crop.resize((size[0] // 2, size[1] // 2), Image.Resampling.LANCZOS)
        result = Image.new("RGB", size)
        result.paste(half, (0, 0))
        result.paste(ImageOps.mirror(half), (half.width, 0))
        result.paste(ImageOps.flip(half), (0, half.height))
        result.paste(ImageOps.flip(ImageOps.mirror(half)), half.size)
        if mode in ("brick", "tiles", "planks"):
            # Mortar is a darkened copy of the source, not invented image content.
            background = ImageEnhance.Brightness(result).enhance(0.38)
            columns, rows = (4, 2) if mode == "brick" else (4, 1) if mode == "planks" else (2, 2)
            cell_w, cell_h = size[0] // columns, size[1] // rows
            patch = result.resize((cell_w - 5, cell_h - 5), Image.Resampling.LANCZOS)
            for row in range(rows):
                for column in range(-1, columns + 1):
                    offset = cell_w // 2 if mode == "brick" and row % 2 else 0
                    tile = ImageOps.mirror(patch) if (row + column) % 2 else patch
                    background.paste(tile, (column * cell_w + offset + 2, row * cell_h + 2))
            result = background
    result = ImageEnhance.Brightness(result).enhance(recipe.get("brightness", 1.0))
    if recipe.get("rotateCounterclockwiseDegrees"):
        angle = recipe["rotateCounterclockwiseDegrees"]
        radians = math.radians(abs(angle))
        aspect = result.width / result.height
        safe_height = min(result.width/(aspect*math.cos(radians)+math.sin(radians)),
                          result.height/(aspect*math.sin(radians)+math.cos(radians)))
        rotated = result.rotate(angle,Image.Resampling.BICUBIC,expand=True)
        crop_width,crop_height = aspect*safe_height,safe_height
        left,top = (rotated.width-crop_width)/2,(rotated.height-crop_height)/2
        result = rotated.crop((round(left),round(top),round(left+crop_width),round(top+crop_height))).resize(size,Image.Resampling.LANCZOS)
    if "inkPreservingWhitePaint" in recipe:
        spec = recipe["inkPreservingWhitePaint"]
        # Threshold only the sampled luminance; keep the photograph's dark
        # grain/ink in place and map the pale paint to a muted warm white.
        gray = ImageOps.grayscale(result)
        mask = gray.point(lambda value: 255 if value >= spec["threshold"] else 0)
        result = Image.composite(Image.new("RGB",result.size,tuple(spec["white"])),
                                 Image.new("RGB",result.size,tuple(spec["black"])),mask)
    return result


def normal_map(image: Image.Image, strength: float) -> Image.Image:
    """Height-from-luminance approximation; lighting in photos is not removed."""
    small = ImageOps.grayscale(image).resize((256, 256)).filter(ImageFilter.GaussianBlur(0.7))
    pixels = small.load()
    normals = []
    for y in range(256):
        for x in range(256):
            dx = (pixels[(x + 1) % 256, y] - pixels[(x - 1) % 256, y]) / 255 * strength
            dy = (pixels[x, (y + 1) % 256] - pixels[x, (y - 1) % 256]) / 255 * strength
            length = math.sqrt(dx * dx + dy * dy + 1)
            normals.append((round(127.5 * (1 - dx / length)), round(127.5 * (1 + dy / length)), round(127.5 * (1 + 1 / length))))
    result = Image.new("RGB", (256, 256))
    result.putdata(normals)
    return result.resize(image.size, Image.Resampling.BILINEAR)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--recipes", type=Path, default=ROOT / "design/photo-textures.json")
    parser.add_argument("--output", type=Path, default=ROOT / "unity/Assets/CastleOfIdeas/Textures/PhotoDerived")
    parser.add_argument("--review-output", type=Path, help="Optional local-only annotated source photos and crop contact sheet")
    args = parser.parse_args()
    recipes = json.loads(args.recipes.read_text())
    args.output.mkdir(parents=True, exist_ok=True)
    records = []
    review_photos = {}
    with tempfile.TemporaryDirectory(prefix="castle-photo-") as temporary:
        for recipe in recipes["recipes"]:
            source = args.source_dir / recipe["source"]
            photo = open_photo(source, Path(temporary))
            if args.review_output:
                review_photos.setdefault(recipe["source"], photo.copy())
            result = assemble(crop_photo(photo, recipe), recipe)
            output = args.output / (recipe["name"] + ".png")
            result.save(output)
            normal_path = args.output / (recipe["name"] + "-normal.png")
            normal_map(result, recipe["normalStrength"]).save(normal_path)
            records.append({"recipe": recipe, "sourceSha256": sha256(source), "orientedSourceSize": list(photo.size), "textureSha256": sha256(output), "normalSha256": sha256(normal_path)})
    if args.review_output:
        args.review_output.mkdir(parents=True, exist_ok=True)
        palette = ("#00ffff", "#ffff00", "#ff66cc", "#66ff66")
        sheet = Image.new("RGB", (1600, math.ceil(len(records) / 4) * 300), "#20252b")
        draw_sheet = ImageDraw.Draw(sheet)
        for index, record in enumerate(records):
            recipe = record["recipe"]
            photo = review_photos[recipe["source"]]
            draw = ImageDraw.Draw(photo)
            if "quad" in recipe:
                points = recipe["quad"]
            else:
                left, top, right, bottom = recipe["rect"]
                points = [(left, top), (right, top), (right, bottom), (left, bottom)]
            pixels = [(round(x * photo.width), round(y * photo.height)) for x, y in points]
            color = palette[index % len(palette)]
            draw.line(pixels + [pixels[0]], fill=color, width=max(5, photo.width // 250))
            draw.line([pixels[0], pixels[2]], fill=color, width=2)
            draw.line([pixels[1], pixels[3]], fill=color, width=2)
            label = f"{index + 1:02d} {recipe['name']}"
            label_pos = (pixels[0][0], max(0, pixels[0][1] - 48))
            label_box = draw.textbbox(label_pos, label, font_size=34)
            draw.rectangle(label_box, fill="#101820")
            draw.text(label_pos, label, fill=color, font_size=34)
            texture = Image.open(args.output / (recipe["name"] + ".png"))
            thumbnail = ImageOps.contain(texture, (380, 240))
            xy = ((index % 4) * 400 + 10, (index // 4) * 300)
            sheet.paste(thumbnail, xy)
            draw_sheet.text((xy[0], xy[1] + 245), label, fill=color, font_size=18)
            draw_sheet.text((xy[0], xy[1] + 270), recipe["source"], fill="white", font_size=17)
        for filename, photo in review_photos.items():
            photo.thumbnail((1920, 1920))
            photo.save(args.review_output / (Path(filename).stem + "-crops.jpg"), quality=92)
        sheet.save(args.review_output / "texture-contact-sheet.jpg", quality=94)
    provenance = {"schemaVersion": 1, "recipeSha256": sha256(args.recipes), "algorithm": "EXIF orientation; rectangle/bilinear quad crop; Lanczos resize; optional mirrored mosaic; brightness; luminance finite-difference normals", "generatedImagery": False, "sources": records}
    (args.output / "texture-provenance.json").write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n")
    print(f"Extracted {len(records)} photo textures and derived normals into {args.output}")


if __name__ == "__main__":
    main()
