import glob, os, sys
from PIL import Image
fs = sorted(glob.glob(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "client", "Assets", "Resources", "UI", "Skin", "*.png")))
sheet = Image.new("RGBA", (960, 220), (90, 110, 70, 255))
x = 10
for f in fs:
    im = Image.open(f)
    if im.width <= 48:
        im = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
    if x + im.width > 950:
        continue
    sheet.alpha_composite(im, (x, 10))
    x += im.width + 10
sheet.save(sys.argv[1])
print([os.path.basename(f) for f in fs])
