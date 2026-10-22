"""Draws the interface skin (9-slice frames, slots, buttons, bars) into client/Assets/Resources/UI/Skin.

Everything is drawn at 4x and scaled down for clean edges. Slice sizes used by the stylesheet are
noted per texture; change both together.
"""
import math
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
    column = Image.new("RGBA", (1, h))
    px = column.load()
    for y in range(h):
        px[0, y] = lerp(top, bottom, y / max(1, h - 1))
    return column.resize((w, h), Image.NEAREST)

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

# ---------------------------------------------------------------- minimap
# The minimap is a round map in a bronze bezel. Its geometry is in panel pixels and must match
# MinimapPanel.cs (Size, Centre, MapRadius, OrbRing, OrbAngles, MedalRing, MedalSize...).
# Angles run clockwise from the top. The bezel texture covers the whole panel at MM_TEX pixels.
MM_SIZE = 240
MM_TEX = 512
MM_C = 120.0
MM_MAP_R = 88.0          # the map is clipped to this circle (the ring covers its edge)
MM_RING_IN = 86.5
MM_RING_OUT = 100.5
MM_ORB_RING = 105.0      # orb centres
MM_ORB_ANGLES = (240, 216, 192, 168, 144, 120)   # run, map, journal, guide, settings, menu
MM_CRADLE = (108, 252, 92.0, 118.0)              # the band the orbs sit on: angles, radii
MM_MEDAL_RING = 97.0     # compass and clock medallion centres
MM_MEDAL_R = 23.0
MM_COMPASS_ANGLE = 315
MM_CLOCK_ANGLE = 45

RIM_TOP = (190, 156, 100, 255)
RIM_BOTTOM = (70, 52, 28, 255)
OUTLINE = (5, 4, 3, 255)
GOLD = (226, 192, 120, 255)

def dgradient(n, lit, shade):
    """A diagonal gradient, lit from the top left like the rest of the skin's metal."""
    small = Image.new("RGBA", (64, 64))
    px = small.load()
    for y in range(64):
        for x in range(64):
            px[x, y] = lerp(lit, shade, (x * 0.6 + y) / (63 * 1.6))
    return small.resize((n, n), Image.BILINEAR)

def darker(c, f):
    return tuple(int(v * f) for v in c[:3]) + (c[3],)

def polar(angle, r, k=1.0):
    a = math.radians(angle)
    return ((MM_C + r * math.sin(a)) * k, (MM_C - r * math.cos(a)) * k)

def disc_mask(n, centre, r):
    m = Image.new("L", (n, n), 0)
    ImageDraw.Draw(m).ellipse((centre[0] - r, centre[1] - r, centre[0] + r, centre[1] + r), fill=255)
    return m

def annulus_mask(n, centre, r0, r1):
    m = disc_mask(n, centre, r1)
    ImageDraw.Draw(m).ellipse((centre[0] - r0, centre[1] - r0, centre[0] + r0, centre[1] + r0), fill=0)
    return m

def band_mask(n, k, a0, a1, r0, r1):
    """An arc band with round ends, in panel units scaled by k."""
    m = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(m)
    steps = 96
    outer = [polar(a0 + (a1 - a0) * i / steps, r1, k) for i in range(steps + 1)]
    inner = [polar(a1 - (a1 - a0) * i / steps, r0, k) for i in range(steps + 1)]
    d.polygon(outer + inner, fill=255)
    cap, mid = (r1 - r0) / 2 * k, (r0 + r1) / 2
    for a in (a0, a1):
        cx, cy = polar(a, mid, k)
        d.ellipse((cx - cap, cy - cap, cx + cap, cy + cap), fill=255)
    return m

def fill(img, colour_or_image, mask):
    src = colour_or_image if isinstance(colour_or_image, Image.Image) else Image.new("RGBA", img.size, colour_or_image)
    img.paste(src, (0, 0), mask)

def radial_alpha(n, centre, r, fn):
    """A mask whose value is fn(distance / r) (0..1 in, 0..255 out) around a centre."""
    size = max(1, int(2 * r))
    g = Image.radial_gradient("L").resize((size, size), Image.BILINEAR)
    g = g.point(lambda v: int(max(0, min(255, fn(v / 181.0)))))
    m = Image.new("L", (n, n), 0)
    m.paste(g, (int(centre[0] - r), int(centre[1] - r)))
    return m

def diamond(d, cx, cy, r, colour):
    d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=colour)

def medallion(img, k, centre):
    """A round bronze mount with a dark face (the compass and the clock sit in these)."""
    n = img.size[0]
    r = MM_MEDAL_R * k
    fill(img, OUTLINE, disc_mask(n, centre, r + 1.2 * k))
    fill(img, dgradient(n, RIM_TOP, RIM_BOTTOM), disc_mask(n, centre, r))
    fill(img, (10, 9, 7, 255), disc_mask(n, centre, r - 3.2 * k))
    fill(img, vgradient(n, n, (36, 31, 25, 255), (12, 11, 9, 255)), disc_mask(n, centre, r - 4.0 * k))
    fill(img, (226, 192, 120, 110), annulus_mask(n, centre, r - 5.6 * k, r - 5.0 * k))

def minimap_bezel(name):
    n = MM_TEX * SS
    k = n / MM_SIZE
    c = (MM_C * k, MM_C * k)
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # A shadow inside the ring, so the map sinks into the frame.
    def vignette(t):
        if t > 1.0:
            return 0
        return 190 * max(0.0, (t - 0.55) / 0.45) ** 2
    fill(img, (0, 0, 0, 255), radial_alpha(n, c, MM_RING_IN * k + k, vignette))

    # The cradle: a band below the map that the round buttons sit on.
    a0, a1, r0, r1 = MM_CRADLE
    fill(img, OUTLINE, band_mask(n, k, a0, a1, r0 - 1.2, r1 + 1.2))
    fill(img, dgradient(n, darker(RIM_TOP, 0.85), darker(RIM_BOTTOM, 0.85)), band_mask(n, k, a0, a1, r0, r1))
    fill(img, vgradient(n, n, (30, 27, 23, 255), (14, 12, 10, 255)), band_mask(n, k, a0, a1, r0 + 1.6, r1 - 1.6))
    # Gold studs between the buttons.
    for i in range(len(MM_ORB_ANGLES) - 1):
        x, y = polar((MM_ORB_ANGLES[i] + MM_ORB_ANGLES[i + 1]) / 2, r1 - 4.5, k)
        diamond(d, x, y, 2.6 * k, OUTLINE)
        diamond(d, x, y, 1.8 * k, GOLD)

    # The ring: dark outline, a bronze band lit from above, a fine groove, gold studs.
    # The inner half is a step down (darker), so the ring reads as a bevel.
    fill(img, OUTLINE, annulus_mask(n, c, MM_RING_IN * k - 1.2 * k, MM_RING_OUT * k + 1.2 * k))
    mid = (MM_RING_IN + MM_RING_OUT) / 2
    fill(img, dgradient(n, RIM_TOP, RIM_BOTTOM), annulus_mask(n, c, mid * k, MM_RING_OUT * k))
    fill(img, dgradient(n, darker(RIM_TOP, 0.62), darker(RIM_BOTTOM, 0.62)), annulus_mask(n, c, MM_RING_IN * k, mid * k))
    fill(img, (20, 15, 9, 230), annulus_mask(n, c, (mid - 0.5) * k, (mid + 0.4) * k))
    fill(img, (255, 236, 190, 60), annulus_mask(n, c, (MM_RING_OUT - 1.3) * k, (MM_RING_OUT - 0.6) * k))
    fill(img, (226, 192, 120, 90), annulus_mask(n, c, (MM_RING_IN + 0.6) * k, (MM_RING_IN + 1.2) * k))
    for angle in (90, 270):
        x, y = polar(angle, mid, k)
        diamond(d, x, y, 4.2 * k, OUTLINE)
        diamond(d, x, y, 3.2 * k, GOLD)
        diamond(d, x, y, 1.2 * k, (60, 44, 22, 255))
    # A crest at the top, flanked by two rivets.
    for angle in (-14, 14):
        x, y = polar(angle, mid, k)
        fill(img, OUTLINE, disc_mask(n, (x, y), 2.2 * k))
        fill(img, dgradient(n, (255, 226, 160, 255), (150, 112, 52, 255)), disc_mask(n, (x, y), 1.5 * k))
    x, y = polar(0, mid, k)
    diamond(d, x, y, 7.0 * k, OUTLINE)
    diamond(d, x, y, 5.8 * k, GOLD)
    diamond(d, x, y, 3.4 * k, (120, 88, 40, 255))
    diamond(d, x, y, 1.6 * k, (255, 230, 170, 255))

    medallion(img, k, polar(MM_COMPASS_ANGLE, MM_MEDAL_RING, k))
    medallion(img, k, polar(MM_CLOCK_ANGLE, MM_MEDAL_RING, k))
    save(img.resize((MM_TEX, MM_TEX), Image.LANCZOS), name)

def orb(name, face_top, face_bottom, rim_top, rim_bottom, size=64):
    """A round button for the minimap's cradle: bronze rim, domed dark face, a soft highlight."""
    n = size * SS
    k = n / 30.0  # drawn for a 30 px button
    c = (n / 2, n / 2)
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    fill(img, OUTLINE, disc_mask(n, c, 15.0 * k - 1))
    fill(img, vgradient(n, n, rim_top, rim_bottom), disc_mask(n, c, 14.0 * k))
    fill(img, (8, 7, 5, 255), disc_mask(n, c, 12.0 * k))
    fill(img, vgradient(n, n, face_top, face_bottom), disc_mask(n, c, 11.2 * k))
    hl = Image.new("L", (n, n), 0)
    ImageDraw.Draw(hl).ellipse((c[0] - 7 * k, c[1] - 10 * k, c[0] + 7 * k, c[1] - 3 * k), fill=34)
    fill(img, (255, 240, 210, 255), hl)
    save(img.resize((size, size), Image.LANCZOS), name)

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
    # The minimap: its bezel (the whole panel, MM_SIZE px shown) and its round buttons (30 px shown).
    minimap_bezel("minimap-bezel")
    orb("orb", (52, 45, 36, 255), (18, 16, 13, 255), (190, 156, 98, 255), (92, 72, 42, 255))
    orb("orb-hover", (76, 64, 48, 255), (26, 22, 17, 255), (240, 214, 150, 255), (130, 102, 60, 255))
    orb("orb-on", (60, 104, 44, 255), (18, 36, 14, 255), (214, 236, 160, 255), (92, 120, 54, 255))
    print("skin written to", os.path.normpath(OUT))
