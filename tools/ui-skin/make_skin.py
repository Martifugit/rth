"""Draws the interface skin (9-slice frames, slots, buttons, bars) into client/Assets/Resources/UI/Skin.

Everything is drawn at 4x and scaled down for clean edges. Slice sizes used by the stylesheet are
noted per texture; change both together.
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "client", "Assets", "Resources", "UI", "Skin")
SS = 4  # supersampling

def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

def rounded(draw, box, r, fill):
    draw.rounded_rectangle(box, radius=r, fill=fill)

def vgradient(w, h, top, bottom):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        c = lerp(top, bottom, y / max(1, h - 1))
        for x in range(w):
            px[x, y] = c
    return img

def frame(name, size, radius, rim_top, rim_bottom, inner_top, inner_bottom, ornaments, outline=(5, 4, 3, 255)):
    """A rounded frame: dark outline, bronze rim (lighter at the top), dark line, gradient body."""
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rounded(d, (0, 0, S - 1, S - 1), radius * SS, outline)
    rim = vgradient(S, S, rim_top, rim_bottom)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((SS, SS, S - 1 - SS, S - 1 - SS), radius=(radius - 1) * SS, fill=255)
    img.paste(rim, (0, 0), mask)
    mask2 = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask2).rounded_rectangle((2 * SS, 2 * SS, S - 1 - 2 * SS, S - 1 - 2 * SS), radius=max(1, radius - 2) * SS, fill=255)
    img.paste(Image.new("RGBA", (S, S), (14, 12, 10, 255)), (0, 0), mask2)
    mask3 = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask3).rounded_rectangle((3 * SS, 3 * SS, S - 1 - 3 * SS, S - 1 - 3 * SS), radius=max(1, radius - 3) * SS, fill=255)
    img.paste(vgradient(S, S, inner_top, inner_bottom), (0, 0), mask3)
    if ornaments:
        # Small gold angles at each corner, along the rim.
        gold = (226, 192, 120, 255)
        arm = 9 * SS
        w = SS
        for cx, cy, sx, sy in ((0, 0, 1, 1), (S - 1, 0, -1, 1), (0, S - 1, 1, -1), (S - 1, S - 1, -1, -1)):
            x0 = cx + sx * 2 * SS
            y0 = cy + sy * 2 * SS
            d.rectangle(sorted_box(x0, y0, x0 + sx * arm, y0 + sy * w), fill=gold)
            d.rectangle(sorted_box(x0, y0, x0 + sx * w, y0 + sy * arm), fill=gold)
            # a tiny diamond stud just inside the angle
            dx, dy = x0 + sx * 3 * SS, y0 + sy * 3 * SS
            d.polygon([(dx, dy - 1.5 * SS), (dx + 1.5 * SS, dy), (dx, dy + 1.5 * SS), (dx - 1.5 * SS, dy)], fill=gold)
    save(img.resize((size, size), Image.LANCZOS), name)

def sorted_box(x0, y0, x1, y1):
    return (min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1))

def slot(name, size=32):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rounded(d, (0, 0, S - 1, S - 1), 3 * SS, (44, 40, 34, 255))          # light lower rim
    rounded(d, (0, 0, S - 1, S - 1 - SS), 3 * SS, (6, 5, 4, 255))        # dark upper rim
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((SS, SS, S - 1 - SS, S - 1 - SS), radius=2 * SS, fill=255)
    img.paste(vgradient(S, S, (8, 7, 6, 255), (20, 18, 15, 255)), (0, 0), mask)
    save(img.resize((size, size), Image.LANCZOS), name)

def button(name, top, bottom, rim_top, rim_bottom, size=(32, 24)):
    W, H = size[0] * SS, size[1] * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rounded(d, (0, 0, W - 1, H - 1), 3 * SS, (5, 4, 3, 255))
    rim = vgradient(W, H, rim_top, rim_bottom)
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).rounded_rectangle((SS, SS, W - 1 - SS, H - 1 - SS), radius=2 * SS, fill=255)
    img.paste(rim, (0, 0), m)
    m2 = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m2).rounded_rectangle((2 * SS, 2 * SS, W - 1 - 2 * SS, H - 1 - 2 * SS), radius=2 * SS, fill=255)
    img.paste(vgradient(W, H, top, bottom), (0, 0), m2)
    save(img.resize(size, Image.LANCZOS), name)

def gloss(name):
    img = Image.new("RGBA", (8, 32), (0, 0, 0, 0))
    px = img.load()
    for y in range(32):
        t = y / 31
        a = int(70 * (1 - t / 0.5)) if t < 0.5 else int(-30 * (t - 0.5) / 0.5)
        for x in range(8):
            px[x, y] = (255, 255, 255, max(0, a)) if a >= 0 else (0, 0, 0, -a)
    save(img, name)

def title(name):
    img = vgradient(16, 28, (36, 33, 29, 255), (20, 18, 16, 255))
    d = ImageDraw.Draw(img)
    d.line((0, 27, 15, 27), fill=(120, 98, 62, 255))
    d.line((0, 26, 15, 26), fill=(8, 7, 6, 255))
    save(img, name)

def tab_active(name):
    img = vgradient(24, 24, (58, 50, 40, 255), (34, 30, 25, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 22, 23, 23), fill=(226, 192, 120, 255))
    save(img, name)

def divider(name):
    W, H = 256 * SS, 9 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    mid = H // 2
    for x in range(W):
        t = min(x, W - 1 - x) / (W * 0.25)
        a = int(255 * min(1.0, t))
        d.line((x, mid - SS // 2, x, mid + SS // 2), fill=(160, 130, 80, a))
    cx = W // 2
    gold = (226, 192, 120, 255)
    d.polygon([(cx, mid - 4 * SS), (cx + 4 * SS, mid), (cx, mid + 4 * SS), (cx - 4 * SS, mid)], fill=gold)
    d.polygon([(cx, mid - 2 * SS), (cx + 2 * SS, mid), (cx, mid + 2 * SS), (cx - 2 * SS, mid)], fill=(40, 32, 22, 255))
    save(img.resize((256, 9), Image.LANCZOS), name)

def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))

if __name__ == "__main__":
    # Windows: slice 14. The body gradient runs dark charcoal-brown.
    frame("window", 48, 4, (176, 144, 92, 255), (98, 78, 46, 255), (24, 22, 20, 250), (13, 12, 11, 250), True)
    # HUD pieces (bars, minimap, chat): slice 12, a little translucent.
    frame("hud", 40, 3, (150, 122, 78, 255), (84, 66, 38, 255), (20, 19, 17, 235), (11, 10, 9, 235), False)
    # Inner panels (cards, wells with a rim): slice 6.
    frame("card", 24, 3, (70, 62, 50, 255), (40, 35, 29, 255), (26, 24, 21, 255), (19, 18, 16, 255), False, outline=(6, 5, 4, 255))
    slot("slot")                         # slice 6
    button("button", (78, 64, 44, 255), (46, 38, 27, 255), (190, 156, 98, 255), (96, 76, 46, 255))        # slice 6
    button("button-hover", (102, 84, 56, 255), (60, 49, 34, 255), (226, 192, 120, 255), (120, 96, 58, 255))
    button("button-on", (120, 82, 30, 255), (74, 50, 18, 255), (240, 210, 140, 255), (150, 112, 52, 255))
    button("button-gold", (236, 178, 64, 255), (176, 112, 26, 255), (255, 226, 150, 255), (120, 76, 18, 255))
    button("button-gold-hover", (252, 200, 90, 255), (196, 130, 36, 255), (255, 240, 180, 255), (140, 90, 24, 255))
    gloss("gloss")
    title("title")
    tab_active("tab-active")
    divider("divider")
    print("skin written to", os.path.normpath(OUT))
