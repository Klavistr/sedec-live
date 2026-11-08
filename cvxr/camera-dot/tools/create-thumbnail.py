"""Original diagram for CVR listing metadata; not an in-world texture."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
out=Path(__file__).resolve().parents[1]/'build'/'camera-dot-thumbnail.png'
out.parent.mkdir(parents=True,exist_ok=True)
im=Image.new('RGB',(1024,1024),(21,28,40));d=ImageDraw.Draw(im)
font=lambda size:ImageFont.load_default(size=size)
def text(y,s,size,color=(236,241,248)):
    d.text((512,y),s,font=font(size),fill=color,anchor='mt')
d.rounded_rectangle((34,34,990,990),radius=38,outline=(80,107,137),width=3)
text(90,'Camera Dot',90)
text(205,'Invisible capture avatar',42,(166,190,215))
# Crosshair is a diagram of the eye position, not a rendered body.
d.ellipse((392,326,632,566),outline=(98,133,168),width=5)
for a,b in [((512,292),(512,363)),((512,529),(512,600)),((358,446),(429,446)),((595,446),(666,446))]:d.line((a,b),fill=(150,181,212),width=5)
d.ellipse((502,436,522,456),fill=(232,193,108))
text(648,'Fully transparent',59)
text(738,'1.5 m eye height',43,(166,190,215))
text(811,'No visible body, depth or shadow',34,(166,190,215))
text(922,'Diagram only - avatar itself is invisible',27,(125,148,174))
im.save(out)
print(out.name)
