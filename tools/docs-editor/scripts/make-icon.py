# Makes assets/logo.ico (the taskbar, title bar, Start menu, shortcut and installer icon) from assets/logo.png.
# Windows draws a taskbar icon at about 24 px whatever the file holds, and the logo's dark dragon ring melts
# into a dark taskbar, so the icon is the logo zoomed in on its centre (ZOOM: the share of the logo kept),
# still round; the small sizes are sharpened a little. The logo inside the app (public/logo.png) is the whole one.
#
#   python scripts/make-icon.py            (needs Pillow)

from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ZOOM = 0.80  # about 25% larger than the whole logo
SIZES = [16, 24, 32, 48, 64, 128, 256]

root = Path(__file__).resolve().parent.parent
src = Image.open(root / "assets" / "logo.png").convert("RGBA")
w = src.width
keep = round(w * ZOOM)
o = (w - keep) // 2
big = src.crop((o, o, o + keep, o + keep)).resize((w, w), Image.LANCZOS)

# Round again where the crop cut the medallion's edge (drawn large, then scaled down, for a smooth rim).
mask = Image.new("L", (w * 4, w * 4), 0)
ImageDraw.Draw(mask).ellipse((4, 4, w * 4 - 4, w * 4 - 4), fill=255)
big.putalpha(ImageChops.darker(big.getchannel("A"), mask.resize((w, w), Image.LANCZOS)))

frames = []
for s in SIZES:
    im = big.resize((s, s), Image.LANCZOS)
    if s <= 48: im = im.filter(ImageFilter.UnsharpMask(radius=0.6, percent=80, threshold=1))
    frames.append(im)
out = root / "assets" / "logo.ico"
frames[-1].save(out, format="ICO", sizes=[(s, s) for s in SIZES], append_images=frames[:-1])
print(f"Wrote {out} ({', '.join(map(str, SIZES))} px, zoom {ZOOM})")
