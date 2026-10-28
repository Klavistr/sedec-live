"""Chapter-aware ISO B-proportion pages with deterministic post-render lenses."""
from pathlib import Path
import hashlib
import json
import math
import random
import re
from PIL import Image, ImageDraw, ImageFont, ImageOps, ImageFilter


def clean_literary_text(text):
    text = re.sub(r"(?m)^[ \t]*(?:[*_=\-][ \t]*){3,}$", "", text)
    text = re.sub(r"\[Illustration[^\]]*\]", "", text, flags=re.I)
    return re.sub(r"\s+", " ", text.replace("_", "")).strip()


def chapters_from_source(path):
    raw = path.read_text(encoding="utf-8-sig")
    raw = raw.split("*** START OF THE PROJECT GUTENBERG EBOOK",1)[1].split("***",1)[1]
    raw = raw.split("*** END OF THE PROJECT GUTENBERG EBOOK",1)[0]
    headings = list(re.finditer(r"(?m)^CHAPTER ([IVX]+)\.\n([^\n]+)\n", raw))
    if len(headings) != 12:
        raise ValueError("Expected twelve original chapter headings")
    return [{"number":i+1, "roman":m[1], "title":m[2],
             "text":clean_literary_text(raw[m.end():headings[i+1].start() if i+1<12 else len(raw)])}
            for i,m in enumerate(headings)]


def contour_rows(image, x, y, margin):
    mask = image.convert("L").point(lambda p: 255 if p<205 else 0)
    padded = Image.new("L", (image.width+2*margin,image.height+2*margin))
    padded.paste(mask,(margin,margin))
    padded = padded.filter(ImageFilter.MaxFilter(2*margin+1))
    rows = []
    for line in range(padded.height):
        span = padded.crop((0,line,padded.width,line+1)).getbbox()
        rows.append((x-margin+span[0],x-margin+span[2]) if span else None)
    return y-margin, rows


def lens_mesh(width, height, rng):
    # Smooth in-plane refraction, without a radial focus or a vanishing point.
    # The envelope fixes every page edge, while local derivatives gently change
    # letter size and shear. Keep a positive Jacobian to avoid folds and streaks.
    amplitude_x = rng.uniform(.095,.115)
    amplitude_y = rng.uniform(.072,.09)
    phase_x,phase_y = rng.uniform(0,2*math.pi),rng.uniform(0,2*math.pi)
    def field(u,v,scale):
        envelope = math.sin(math.pi*u)*math.sin(math.pi*v)
        return (u+amplitude_x*scale*envelope*math.sin(2*math.pi*v+phase_x),
                v+amplitude_y*scale*envelope*math.sin(2*math.pi*u+phase_y))
    scale = 1.0
    for attempt in range(20):
        minimum = 10
        epsilon = .0001
        for row in range(1,40):
            for column in range(1,40):
                u,v = column/40,row/40
                x0,y0 = field(u-epsilon,v,scale)
                x1,y1 = field(u+epsilon,v,scale)
                x2,y2 = field(u,v-epsilon,scale)
                x3,y3 = field(u,v+epsilon,scale)
                determinant = ((x1-x0)*(y3-y2)-(x3-x2)*(y1-y0))/(4*epsilon*epsilon)
                minimum = min(minimum,determinant)
        if minimum > .28: break
        scale *= .9
    def sample(x,y):
        u,v = field(x/width,y/height,scale)
        return u*width,v*height
    mesh = []
    for top in range(0,height,12):
        bottom = min(top+12,height)
        for left in range(0,width,12):
            right = min(left+12,width)
            quad = (*sample(left,top),*sample(left,bottom),*sample(right,bottom),*sample(right,top))
            mesh.append(((left,top,right,bottom),quad))
    return mesh, {"type":"smooth-wave-refraction", "amplitudePixels":[amplitude_x*scale*width,amplitude_y*scale*height],
                  "phase":[phase_x,phase_y],"jacobianMinimum":minimum,"edgeMapping":"identity"}


def create_book_wallpaper(root, font_name, font_file, source_dir, output_dir):
    layout = json.loads((root/"design/layout.json").read_text())
    bounds = layout["dimensions"]["sub"]
    long,short = bounds["maxX"]-bounds["minX"],bounds["maxY"]-bounds["minY"]
    ppm,height = 384,round(layout["dimensions"]["wallHeight"]*384)
    page_width = round(height/math.sqrt(2))
    wall_lengths = [round(m*ppm) for m in (long,short,long,short)]
    chapters = chapters_from_source(source_dir/"pg11-source.txt")
    catalog = json.loads((source_dir/"illustrations.json").read_text())["illustrations"]
    # Nineteen full B-proportion pages fit this perimeter within 0.3%. The
    # complete atlas is fitted to the room once; there are no cropped half-pages.
    allocation = [2,1,1,2,2,2,2,2,2,1,1,1]
    paper,ink = (245,242,232),(36,34,31)
    review = root/"preview/alice-wallpaper"
    review.mkdir(parents=True,exist_ok=True)
    page_images,page_records,placements,used_texts = [],[],[],[]
    illustration_layers,text_layers = [],[]
    for chapter,count in zip(chapters,allocation):
        chapter_art = [a for a in catalog if a["chapter"]==chapter["number"]]
        prepared = []
        for local_page in range(count):
            page_number = len(page_images)+local_page+1
            rng = random.Random(241029+page_number*101)
            pictures = Image.new("RGBA",(page_width,height))
            boxes,contours,art_records = [],[],[]
            for art in chapter_art[local_page::count]:
                source = ImageOps.autocontrast(Image.open(source_dir/art["file"]).convert("L"))
                source = source.crop(ImageOps.invert(source).point(lambda p:255 if p>90 else 0).getbbox())
                image = ImageOps.colorize(source,ink,paper)
                image = ImageOps.contain(image,(rng.randint(185,285),rng.randint(235,355)),Image.Resampling.LANCZOS)
                for attempt in range(1000):
                    x,y = rng.randrange(18,page_width-image.width-18),rng.randrange(110,height-image.height-20)
                    rect = (x,y,x+image.width,y+image.height)
                    if not any(rect[0]<b[2]+24 and rect[2]+24>b[0] and rect[1]<b[3]+24 and rect[3]+24>b[1] for b in boxes):
                        break
                else: raise ValueError("Chapter illustration packing failed")
                boxes.append(rect)
                pictures.paste(image,(x,y))
                contours.append(contour_rows(image,x,y,round(.025*ppm)))
                art_records.append({"source":art["file"],"number":art["number"],"chapter":art["chapter"],
                                    "rect":list(rect),"page":page_number})
            mesh,lens = lens_mesh(page_width,height,rng)
            prepared.append((page_number,pictures,contours,art_records,mesh,lens))
        words = chapter["text"].split()
        for font_size in range(15,30):
            font = ImageFont.truetype(font_file,font_size)
            heading_font = ImageFont.truetype(font_file,23)
            cursor,rendered = 0,[]
            exhausted = False
            for local_page,(number,pictures,contours,art_records,mesh,lens) in enumerate(prepared):
                text = Image.new("RGBA",(page_width,height))
                draw = ImageDraw.Draw(text)
                heading = f"CHAPTER {chapter['roman']}.  {chapter['title']}"
                draw.text((10,8),heading,fill=ink+(255,),font=heading_font)
                start = cursor
                # Genuine two-column page composition, with a narrow gutter.
                gutter,margin,pitch = 14,8,font_size+3
                column_width = (page_width-2*margin-gutter)//2
                for column in range(2):
                    left = margin+column*(column_width+gutter)
                    right = left+column_width
                    for y in range(49,height-font_size-3,pitch):
                        x = left
                        while x<right:
                            if cursor>=len(words): exhausted=True;break
                            word = words[cursor]
                            advance = math.ceil(font.getlength(word))+max(3,font_size//4)
                            if x+advance>right: break
                            collision = None
                            for start_y,rows in contours:
                                spans=[s for s in rows[max(0,y-start_y):max(0,min(len(rows),y+pitch-start_y))] if s]
                                if spans:
                                    a,b=min(s[0] for s in spans),max(s[1] for s in spans)
                                    if x<b and x+advance>a:collision=b;break
                            if collision:
                                x=collision+1;continue
                            draw.text((x,y),word,fill=ink+(255,),font=font)
                            x+=advance;cursor+=1
                rendered.append((text,start,cursor))
            if not exhausted: break
        else: raise ValueError("Could not fill chapter pages without repeating text")
        for (number,pictures,contours,art_records,mesh,lens),(text,start,end) in zip(prepared,rendered):
            pictures=pictures.transform(pictures.size,Image.Transform.MESH,mesh,Image.Resampling.BICUBIC)
            text=text.transform(text.size,Image.Transform.MESH,mesh,Image.Resampling.BICUBIC)
            page=Image.new("RGBA",(page_width,height),paper+(255,))
            page.alpha_composite(pictures);page.alpha_composite(text)
            page=page.convert("RGB")
            page.save(review/f"page-{number:02d}.png")
            page_images.append(page);illustration_layers.append(pictures);text_layers.append(text)
            excerpt=" ".join(words[start:end])
            excerpt_path=source_dir/"excerpts"/f"wallpaper-page-{number:02d}.txt"
            excerpt_path.write_text(excerpt+"\n")
            used_texts.append(excerpt)
            page_records.append({"number":number,"chapter":chapter["number"],"wordStart":start,"wordEnd":end,
                                 "fontSizePixels":font_size,"columns":2,"lens":lens,
                                 "excerpt":str(excerpt_path.relative_to(root)),
                                 "textSha256":hashlib.sha256(excerpt.encode()).hexdigest()})
            placements.extend(art_records)
    atlas_width=page_width*len(page_images)
    canvas=Image.new("RGB",(atlas_width,height),paper)
    for i,page in enumerate(page_images):canvas.paste(page,(i*page_width,0))
    # Fit the 19 complete page masters by a fraction of one percent to the walls.
    wall_width=sum(wall_lengths)
    canvas=canvas.resize((wall_width,height),Image.Resampling.LANCZOS)
    canvas.save(review/"panorama.png")
    for name,layers in (("illustrations",illustration_layers),("text",text_layers)):
        layer=Image.new("RGBA",(atlas_width,height))
        for i,page in enumerate(layers):layer.paste(page,(i*page_width,0))
        layer.resize((wall_width,height),Image.Resampling.LANCZOS).save(review/(name+"-layer.png"))
    records,offset=[],0
    source_hash=hashlib.sha256((source_dir/"pg11-source.txt").read_bytes()).hexdigest()
    for wall,length in zip(("north","east","south","west"),wall_lengths):
        name="wallpaper-"+wall
        target=output_dir/(name+".png")
        canvas.crop((offset,0,offset+length,height)).save(target)
        records.append({"name":name,"kind":"wallpaper","source":"pg11-source.txt","sourceSha256":source_hash,
                        "outputSha256":hashlib.sha256(target.read_bytes()).hexdigest(),"fontFamily":font_name,
                        "canvasCropPixels":[offset,0,offset+length,height]})
        offset+=length
    spec={"schemaVersion":3,"seed":241029,"pixelsPerMeter":ppm,"pageSizePixels":[page_width,height],
          "pageAspect":"ISO B: width/height = 1/sqrt(2)","pageCount":len(page_images),
          "atlasHorizontalFitScale":wall_width/atlas_width,"canvasSizePixels":[wall_width,height],
          "wallOrder":["north","east","south","west"],"contourClearanceMeters":.025,
          "removedFormatting":["asterisk separator rows","rule-only separator rows","illustration placeholders"],
          "repeatedText":False,"repeatedIllustrations":False,"textWordsPlaced":sum(p["wordEnd"]-p["wordStart"] for p in page_records),
          "pages":page_records,"placements":placements,"sourceHashes":{a["file"]:a["sha256"] for a in catalog},
          "fontSha256":hashlib.sha256(Path(font_file).read_bytes()).hexdigest()}
    (root/"design/alice-wallpaper.json").write_text(json.dumps(spec,indent=2)+"\n")
    return records
