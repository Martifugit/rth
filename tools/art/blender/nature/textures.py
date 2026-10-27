#!/usr/bin/env python3
"""
Paints the nature kit's leaf textures (128 x 128 pixel art) into nature/textures/: our own,
generated, nothing copied. Each has its ragged hem along the bottom (v = 0) with transparent pixels
below it, so a canopy skirt wearing it ends in hanging leaves.

- Foliage.png: round broad leaves in olive greens (the alder; recolour for autumn).
- Ashleaf.png: sprays of narrow paired leaflets, light yellow-green (the ash).
- Elmleaf.png: broad rough ovals hanging slantwise, a deeper blue-green (the elm).
- Needles.png: tufts of long dark needles, a cold blue-green (the pine).
- Ivy.png: strands of three-lobed dark leaves trailing down, gaps between (drapes over rock).
- Thorn.png: small crowded dark leaves flecked with red haws (the hawthorn).
- Gorse.png: short dark spines in a tangle, a few small yellow flowers.
- BushDark.png / BushDarkPicked.png: a low bush's dark leaves with blue-black berries, and picked.
- BushPale.png / BushPalePicked.png: a tall bush's paler leaves with red berries, and picked.
- Lance.png: long narrow silvery leaves, standing up and out (the pollard willow's shoots).
- Wattle.png: a woven hurdle panel, opaque.

    python tools/art/blender/nature/textures.py [--seed N] [--preview]

Standard library only (shares leaf_texture.py's PNG writer). Same seed, same image.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
from leaf_texture import write_png  # noqa: E402

SIZE = 128
OUT = os.path.join(HERE, "textures")

# Olive ramp, darkest to lightest: the yellow-olive of the references, graded to sit on the
# basin's olive grass (palette Grass #5a7236).
OLIVE = ["#262d16", "#343e1d", "#444f25", "#55612d", "#677336", "#7b8640", "#919a4d"]
SILVER = ["#2b3022", "#3a412d", "#4b533a", "#5d6648", "#707a59", "#848d6b", "#99a07f"]
ASH_GREEN = ["#2c3317", "#3d4720", "#505c29", "#647133", "#78853d", "#8e9a49", "#a5ad58"]
THORN_GREEN = ["#1d2619", "#28331f", "#334027", "#3f4e2f", "#4c5c37", "#5a6a41", "#69784c"]
GORSE_GREEN = ["#1b2313", "#252f19", "#303c1f", "#3c4926", "#49572e", "#576636", "#66753f"]
BUSH_DARK = ["#1a2318", "#232f20", "#2d3b28", "#384830", "#445539", "#516342", "#5f714c"]
BUSH_PALE = ["#283117", "#36411e", "#465327", "#576530", "#69773a", "#7c8945", "#909b52"]
ELM_GREEN = ["#1f2a19", "#2a3821", "#36462a", "#435533", "#51643c", "#5f7346", "#6e8250"]
NEEDLE_GREEN = ["#151f1c", "#1c2a25", "#24362f", "#2d4339", "#375143", "#42604e", "#4e6f5a"]
IVY_GREEN = ["#141c12", "#1c2718", "#24331f", "#2e4027", "#394e2f", "#455c38", "#536b42"]
WITHY = ["#2a241c", "#3f3529", "#544736", "#695a45", "#7e6d55", "#938266"]


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def ellipse(shade, cx, cy, rx, ry, a, base, lit=True):
    ca, sa = math.cos(a), math.sin(a)
    r = max(rx, ry)
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            u, v = (dx * ca + dy * sa) / rx, (-dx * sa + dy * ca) / ry
            d = u * u + v * v
            if d > 1:
                continue
            s = base
            if lit and d > 0.6 and (dx + dy) > 0:
                s -= 1
            elif lit and d < 0.3 and (dx + dy) < -0.4:
                s += 1
            if 0 <= y < SIZE:
                shade[y][x % SIZE] = max(0, min(6, s))


def hem(shade, rng, depth_min, depth_max, width=(7, 14)):
    """Cut a ragged edge of hanging leaf tips along the bottom; below it is transparent."""
    tips = []
    x = -8.0
    while x < SIZE + 8:
        w = rng.uniform(*width)
        tips.append((x + w / 2, w / 2, rng.uniform(depth_min, depth_max)))
        x += w * rng.uniform(0.5, 0.8)
    for col in range(SIZE):
        reach = 3.0
        for tx, half, depth in tips:
            t = (col + 0.5 - tx) / half
            if abs(t) < 1:
                reach = max(reach, depth * (1 - t * t) ** 0.6)     # leafy lobes, a little pointed
        edge = int(math.ceil(depth_max + 4 - reach))
        for row in range(edge):
            shade[SIZE - 1 - row][col] = -1
        if edge < SIZE and shade[SIZE - 1 - edge][col] >= 0:
            shade[SIZE - 1 - edge][col] = max(0, shade[SIZE - 1 - edge][col] - 2)


def foliage(seed):
    """Big olive leaves in clumps, lit from the top left, darker low down; deep pointed hem."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    leaves = []
    for _ in range(440):
        leaves.append((rng.uniform(-6, SIZE + 6), rng.uniform(-6, SIZE + 6), rng.uniform(4.5, 8.0),
                       rng.uniform(2.4, 4.0), rng.uniform(0, math.pi)))
    leaves.sort()
    for cy, cx, rx, ry, a in [(l[1], l[0], l[2], l[3], l[4]) for l in leaves]:
        base = 2 + int(3.0 * (1 - cy / SIZE) + rng.uniform(-0.8, 1.3))
        ellipse(shade, cx, cy, rx, ry, a, base)
    # Dark pockets between clumps, where the inside of the tree shows.
    for _ in range(6):
        ellipse(shade, rng.uniform(0, SIZE), rng.uniform(10, SIZE - 30), rng.uniform(2, 4), rng.uniform(1.5, 3),
                rng.uniform(0, math.pi), 0, lit=False)
    hem(shade, rng, 8, 24, width=(10, 20))
    return shade, [rgb(c) for c in OLIVE]


def lance(seed):
    """Long narrow leaves standing up and out, silvery olive (a willow's grey undersides), crowded
    together; their pointed tips make the hem. Worn upside down on a pollard's crown, the tips
    point up like fresh shoots."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    leaves = []
    for _ in range(420):
        leaves.append((rng.uniform(-8, SIZE + 8), rng.uniform(-8, SIZE + 8), rng.uniform(7, 11),
                       rng.uniform(1.3, 2.0), math.pi / 2 + rng.uniform(-0.55, 0.55)))
    leaves.sort(key=lambda l: l[1])
    for cx, cy, rx, ry, a in leaves:
        base = 2 + int(3.0 * (1 - cy / SIZE) + rng.uniform(-0.8, 1.4))
        ellipse(shade, cx, cy, rx, ry, a, base)
    hem(shade, rng, 12, 28, width=(4, 8))
    return shade, [rgb(c) for c in SILVER]


def wattle(seed):
    """A woven hurdle: upright sails every quarter, and rods woven past them in bands, over one
    sail and under the next, each band the other way round from the last. Opaque."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    sails = [16 + 32 * k for k in range(4)]
    band = 8
    for b in range(SIZE // band):
        wave = rng.uniform(-0.6, 0.6)
        for y in range(b * band, (b + 1) * band):
            t = (y - b * band + 0.5) / band            # 0 at the band's top, 1 at its foot
            for x in range(SIZE):
                bulge = 1 - abs(2 * t - 1)               # rounded rod: lit in the middle
                s = 1 + int(round(3.2 * bulge + wave))
                if t < 0.12 or t > 0.88:
                    s = 0                                 # the gap between rods
                shade[y][x] = max(0, min(5, s))
        for i, sx in enumerate(sails):
            if (i + b) % 2:                               # the rod goes under: the sail shows
                for y in range(b * band, (b + 1) * band):
                    for x in range(sx - 3, sx + 3):
                        edge = x in (sx - 3, sx + 2)
                        shade[y][x % SIZE] = 1 if edge else 3
    return shade, [rgb(c) for c in WITHY]


def ashleaf(seed):
    """The ash's leaves: sprays of narrow paired leaflets along short stalks, light yellow-green,
    with sky-dark gaps between the sprays; a fine pointed hem."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    sprays = []
    for _ in range(170):
        sprays.append((rng.uniform(-10, SIZE + 10), rng.uniform(-10, SIZE + 10), rng.uniform(-2.4, -0.7)))
    sprays.sort(key=lambda t: t[1])
    for sx, sy, a in sprays:
        base = 2 + int(3.0 * (1 - sy / SIZE) + rng.uniform(-0.8, 1.3))
        dx, dy = math.cos(a), math.sin(a)
        for k in range(4):
            px, py = sx + dx * 5 * k, sy - dy * 5 * k
            for side in (-1, 1):
                b = a + side * 1.0
                ellipse(shade, px + math.cos(b) * 3.5, py - math.sin(b) * 3.5, 4.5, 1.6, -b, base)
        ellipse(shade, sx + dx * 18, sy - dy * 18, 4.0, 1.6, -a, base)       # the end leaflet
    hem(shade, rng, 10, 26, width=(4, 9))
    return shade, [rgb(c) for c in ASH_GREEN]


def thorn(seed):
    """The hawthorn's leaves: small, crowded, dark cool green, flecked with red haws."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    leaves = []
    for _ in range(950):
        leaves.append((rng.uniform(-4, SIZE + 4), rng.uniform(-4, SIZE + 4), rng.uniform(2.4, 3.6),
                       rng.uniform(1.7, 2.6), rng.uniform(0, math.pi)))
    leaves.sort(key=lambda l: l[1])
    for cx, cy, rx, ry, a in leaves:
        base = 2 + int(3.0 * (1 - cy / SIZE) + rng.uniform(-0.8, 1.3))
        ellipse(shade, cx, cy, rx, ry, a, base)
    for _ in range(34):                                   # haws: a dark red berry with a lit corner
        hx, hy = rng.randrange(SIZE), rng.randrange(4, SIZE - 30)
        for y, x, s in ((hy, hx, 7), (hy, hx + 1, 7), (hy + 1, hx, 7), (hy + 1, hx + 1, 8)):
            shade[y][x % SIZE] = s
    hem(shade, rng, 8, 20, width=(5, 10))
    return shade, [rgb(c) for c in THORN_GREEN] + [rgb("#8a3a2c"), rgb("#5c2219")]


def gorse(seed):
    """Gorse: a dense tangle of short dark spines in every direction, a few small yellow flowers
    caught in it; a fine prickly hem."""
    rng = random.Random(seed)
    shade = [[0 if rng.random() < 0.5 else 1 for _ in range(SIZE)] for _ in range(SIZE)]
    for _ in range(2600):
        x, y = rng.uniform(0, SIZE), rng.uniform(0, SIZE)
        a = rng.uniform(0, math.pi)
        n = rng.randint(3, 5)
        s = 2 + int(2.8 * (1 - y / SIZE) + rng.uniform(-0.6, 1.4))
        for i in range(n):
            px, py = int(x + math.cos(a) * i), int(y + math.sin(a) * i)
            if 0 <= py < SIZE:
                shade[py][px % SIZE] = max(0, min(6, s - (1 if i == n - 1 else 0)))
    for _ in range(26):                                   # flowers: a yellow fleck with a lit corner
        fx, fy = rng.randrange(SIZE), rng.randrange(2, SIZE - 26)
        for y, x, s in ((fy, fx, 7), (fy, fx + 1, 7), (fy + 1, fx, 7), (fy, fx, 8)):
            shade[y][x % SIZE] = s
    hem(shade, rng, 6, 16, width=(3, 6))
    return shade, [rgb(c) for c in GORSE_GREEN] + [rgb("#b39a33"), rgb("#d2b950")]


def bush(seed, ramp, berry, clusters, leaf=(3.0, 4.6)):
    """A berry bush's leaves, painted twice from one seed: with berries (clusters of four round
    berries, a lit pixel each) and picked bare. The picked texture is the depleted node: the same
    mesh, the other material."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    leaves = []
    for _ in range(620):
        leaves.append((rng.uniform(-4, SIZE + 4), rng.uniform(-4, SIZE + 4), rng.uniform(*leaf),
                       rng.uniform(leaf[0] * 0.6, leaf[1] * 0.6), rng.uniform(0, math.pi)))
    leaves.sort(key=lambda l: l[1])
    for cx, cy, rx, ry, a in leaves:
        base = 2 + int(3.0 * (1 - cy / SIZE) + rng.uniform(-0.8, 1.3))
        ellipse(shade, cx, cy, rx, ry, a, base)
    hem(shade, rng, 8, 20, width=(6, 12))
    picked = [row[:] for row in shade]
    for _ in range(clusters):
        bx, by = rng.randrange(SIZE), rng.randrange(4, SIZE - 30)
        for ox, oy in ((0, 0), (3, 0), (1, 3), (4, 3)):
            for y, x, s in ((0, 0, 7), (0, 1, 7), (1, 0, 7), (1, 1, 7), (0, 0, 8)):
                py, px = by + oy + y, (bx + ox + x) % SIZE
                if shade[py][px] >= 0:
                    shade[py][px] = s
    palette = [rgb(c) for c in ramp] + [rgb(berry[0]), rgb(berry[1])]
    return shade, picked, palette


def elmleaf(seed):
    """The elm's leaves: broad rough ovals, larger than the alder's, mostly hanging slantwise, in a
    deeper, slightly blue green; a blunt hem."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    leaves = []
    for _ in range(330):
        a = rng.choice((0.7, 2.4)) + rng.uniform(-0.35, 0.35)
        leaves.append((rng.uniform(-6, SIZE + 6), rng.uniform(-6, SIZE + 6), rng.uniform(5.0, 7.0),
                       rng.uniform(3.0, 4.0), a))
    leaves.sort(key=lambda l: l[1])
    for cx, cy, rx, ry, a in leaves:
        base = 2 + int(3.0 * (1 - cy / SIZE) + rng.uniform(-0.8, 1.3))
        ellipse(shade, cx, cy, rx, ry, a, base)
    hem(shade, rng, 8, 22, width=(10, 18))
    return shade, [rgb(c) for c in ELM_GREEN]


def needles(seed):
    """The pine's needles: tufts of long dark strokes fanning out from short twigs, a cold
    blue-green, darkest underneath; a fine spiky hem."""
    rng = random.Random(seed)
    shade = [[0] * SIZE for _ in range(SIZE)]
    tufts = []
    for _ in range(430):
        tufts.append((rng.uniform(-6, SIZE + 6), rng.uniform(-6, SIZE + 6)))
    tufts.sort(key=lambda t: t[1])
    for cx, cy in tufts:
        base = 3 + int(2.6 * (1 - cy / SIZE) + rng.uniform(-0.8, 1.2))
        for _ in range(9):
            a = rng.uniform(-2.6, -0.5)                  # fanning out and down from the twig
            n = rng.randint(5, 8)
            for i in range(n):
                px, py = int(cx + math.cos(a) * i), int(cy - math.sin(a) * i)
                if 0 <= py < SIZE:
                    shade[py][px % SIZE] = max(0, min(6, base - (1 if i > n - 3 else 0)))
    hem(shade, rng, 8, 20, width=(3, 7))
    return shade, [rgb(c) for c in NEEDLE_GREEN]


def ivy(seed):
    """Ivy hanging in strands: three-lobed dark leaves along trailing stems, gaps between the
    strands, each strand ending at its own length (the hem)."""
    rng = random.Random(seed)
    shade = [[-1] * SIZE for _ in range(SIZE)]
    x = 3.0
    while x < SIZE:
        end = rng.randint(SIZE - 40, SIZE - 2)
        sway = rng.uniform(-0.08, 0.08)
        for y in range(-6, end, 6):
            cx = x + sway * y + rng.uniform(-1.5, 1.5)
            base = 2 + int(2.5 * (1 - y / SIZE) + rng.uniform(-0.6, 1.2))
            for dx, dy, rx, ry in ((0, 0, 3.2, 2.6), (-2.6, 1.2, 2.2, 1.8), (2.6, 1.2, 2.2, 1.8)):
                ellipse(shade, cx + dx, y + 3 + dy, rx, ry, rng.uniform(0, math.pi), base)
        x += rng.uniform(9, 14)
    return shade, [rgb(c) for c in IVY_GREEN]


def save(name, shade, palette, preview):
    pixels = [(0, 0, 0, 0) if s < 0 else palette[s] + (255,) for row in shade for s in row]
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    write_png(path, pixels, SIZE, SIZE)
    if preview:
        k = 4
        big = []
        for y in range(SIZE * k):
            for x in range(SIZE * k):
                p = pixels[(y // k) * SIZE + x // k]
                big.append(p if p[3] else (120, 120, 120, 255))
        write_png(os.path.join(OUT, name + ".preview.png"), big, SIZE * k, SIZE * k)
    print("wrote", path)


def main(argv):
    seed = int(argv[argv.index("--seed") + 1]) if "--seed" in argv else 5
    preview = "--preview" in argv
    save("Foliage", *foliage(seed), preview)
    save("Ashleaf", *ashleaf(seed), preview)
    save("Thorn", *thorn(seed), preview)
    save("Elmleaf", *elmleaf(seed), preview)
    save("Needles", *needles(seed), preview)
    save("Ivy", *ivy(seed), preview)
    save("Gorse", *gorse(seed), preview)
    for name, ramp, berry, n in (("BushDark", BUSH_DARK, ("#3d3a6e", "#7a76b4"), 46),
                                 ("BushPale", BUSH_PALE, ("#8e2b26", "#c4544a"), 40)):
        full, picked, palette = bush(seed + len(name), ramp, berry, n)
        save(name, full, palette, preview)
        save(name + "Picked", picked, palette, preview)
    save("Lance", *lance(seed), preview)
    save("Wattle", *wattle(seed), preview)


if __name__ == "__main__":
    main(sys.argv[1:])
