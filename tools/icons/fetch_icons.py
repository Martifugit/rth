"""Builds client/Assets/Resources/Icons from game-icons.net (CC BY 3.0) per icons.json.
Pass icon ids to fetch only those.

Each icon is downloaded white on transparent, scaled to 96 px with a soft dark edge so it reads
on dark slots, and saved under our own id. The client tints them (an item's colour, the theme's
gold), so they stay white here.
"""
import json, os, sys, urllib.request, io
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "client", "Assets", "Resources", "Icons")
SIZE = 96

def fetch(path):
    url = "https://game-icons.net/icons/ffffff/transparent/1x1/" + path + ".png"
    with urllib.request.urlopen(url) as r:
        return Image.open(io.BytesIO(r.read())).convert("RGBA")

def process(img):
    img = img.resize((SIZE - 8, SIZE - 8), Image.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.paste(img, (4, 4), img)
    alpha = canvas.split()[3]
    edge = alpha.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(1.2))
    shadow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    shadow.putalpha(edge.point(lambda a: int(a * 0.85)))
    return Image.alpha_composite(shadow, canvas)

def main():
    mapping = json.load(open(os.path.join(HERE, "icons.json")))
    os.makedirs(OUT, exist_ok=True)
    failed = []
    only = set(sys.argv[1:])  # optional: just these ids
    for key, path in mapping.items():
        if key.startswith("_") or (only and key not in only):
            continue
        try:
            process(fetch(path)).save(os.path.join(OUT, key + ".png"))
        except Exception as e:
            failed.append(key + " (" + path + "): " + str(e))
    print((len(only) if only else len(mapping) - 1) - len(failed), "icons written to", os.path.normpath(OUT))
    for f in failed:
        print("FAILED", f)
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
