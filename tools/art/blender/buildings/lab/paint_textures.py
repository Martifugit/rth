#!/usr/bin/env python3
"""
The paint lab's textures (second try, after the RS3 exterior references in images/).

What the references taught: surfaces are clean, not dirty. Each material is an even colour with a
fine, low-contrast pattern (small roof tiles in neat rows, big ashlar blocks with thin joints).
Richness comes from contrast between materials and from crisp trims, not from grime. A theme is
a set of materials over the same shell.

So each texture here is:
  - one material in a ramp of three close palette colours (a theme picks the ramp);
  - painted for one whole piece: a wall tile's texture covers the tile, a roof piece's covers its
    run, so roof rows continue from eave to ridge;
  - crisp and fine: thin dark joints, slight shade differences between tiles or blocks.

    python3 tools/art/blender/buildings/lab/paint_textures.py

Writes PNGs into textures/ beside this script. Standard library only; seeded, so repeatable.
"""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "art"))
from surface_textures import write_png  # noqa: E402

OUT = os.path.join(HERE, "textures")
W = 128
OVERHANG = 0.35            # the eave piece's run starts this far out (matches the kit)

with open(os.path.join(ROOT, "client", "Assets", "Resources", "Art", "palette.json"), encoding="utf-8") as f:
    PALETTE = {c["name"]: c["hex"] for c in json.load(f)["colours"]}


def col(name):
    h = PALETTE[name].lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def shade(c, k):
    return tuple(max(0.0, min(1.0, v * k)) for v in c)


class Image:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [(1.0, 1.0, 1.0)] * (w * h)

    def set(self, x, y, c):
        if 0 <= y < self.h:
            self.px[y * self.w + (x % self.w)] = c

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        write_png(os.path.join(OUT, name + ".png"), self.px, self.w, self.h)


# ---- roofs: rows that continue from eave to ridge ----

def roof_rows(seed, ramp, run0, run1, rows_per_tile=5, unit_w=(16, 22), joint=0.55):
    """Roof tiles or slates over run0..run1 (v = 0 at run0), rows placed by run so they line up
    from piece to piece. Each tile is one of the ramp's colours, a little lighter along its top,
    with a thin dark line under it (the next row's edge) and between it and its neighbour."""
    base = [col(n) for n in ramp]
    img = Image(W, 128)
    rows = {}
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        run = run0 + (run1 - run0) * v
        row = math.floor(run * rows_per_tile)
        phase = (run * rows_per_tile) % 1.0                      # 0 at a row's lower edge
        if row not in rows:
            r = random.Random(seed * 1009 + row * 7919)
            widths = []
            while sum(widths) < W:
                widths.append(r.randint(*unit_w))
            widths[-1] -= sum(widths) - W                       # rows tile across the piece
            if widths[-1] < unit_w[0] // 2:
                last = widths.pop()                             # (pop first: the index is read before it)
                widths[-1] += last
            tiles = [mix(base[1], r.choice(base), r.uniform(0.0, 0.35)) for _ in widths]   # close to the middle
            rows[row] = (r.randint(0, W - 1), widths, tiles)
        x0, widths, tiles = rows[row]
        for x in range(W):
            xs = (x - x0) % W
            acc, k = 0, 0
            while acc + widths[k] <= xs:
                acc += widths[k]
                k += 1
            c = tiles[k]
            if phase < 0.11:
                c = shade(c, joint)                              # the shadow of the row above
            elif xs - acc == 0:
                c = shade(c, 0.7)                                # the joint beside it
            elif phase > 0.8:
                c = shade(c, 1.1)                                # light along its top
            img.set(x, y, c)
    return img


# ---- walls: one tile each ----

def ashlar_wall(seed, ramp, courses=6, blocks=2):
    """One wall tile of dressed stone (128 x 256, v = 0 at the plinth): even courses of big
    blocks, every other course offset by half a block (so neighbouring tiles join), thin joints,
    each block its own close shade."""
    base = [col(n) for n in ramp]
    r = random.Random(seed)
    img = Image(W, 256)
    ch = img.h / courses
    bw = W / blocks
    shades = {(c, b): mix(base[1], r.choice(base), r.uniform(0.0, 0.4)) for c in range(courses) for b in range(blocks + 1)}
    mortar = shade(mix(base[0], base[-1], 0.5), 0.72)
    for y in range(img.h):
        c = int(y / ch)
        yy = y - c * ch
        off = bw / 2 if c % 2 else 0
        for x in range(W):
            xx = (x + off) % W
            b = int(xx / bw)
            in_block_x = xx - b * bw
            colour = shades[(c, b)]
            if yy < 1.5 or in_block_x < 1.5:
                colour = mortar
            elif yy < 4 or in_block_x < 4:
                colour = shade(colour, 1.06)                     # a lit edge
            img.set(x, y, colour)
    return img


def plaster_wall(seed, ramp):
    """One wall tile of limewashed plaster: nearly even, a faint trowel texture, slightly cooler
    toward the top under the rail. Clean, not stained."""
    base = [col(n) for n in ramp]
    r = random.Random(seed)
    img = Image(W, 256)
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        for x in range(W):
            c = base[1]
            n = r.random()
            if n < 0.08:
                c = base[0]
            elif n > 0.93:
                c = base[2]
            if v > 0.94:
                c = mix(c, base[0], 0.5)
            img.set(x, y, c)
    return img


# ---- themes: one shell, a set of materials ----

THEMES = {
    "Tile": {   # plaster and orange timber under red clay tiles
        "wall": lambda s: plaster_wall(s, ["Linen", "Plaster", "CandleWax"]),
        "roof": dict(ramp=["Brick", "Russet", "Copper"], rows_per_tile=5, unit_w=(14, 20)),
    },
    "DarkStone": {   # dark ashlar under grey slate
        "wall": lambda s: ashlar_wall(s, ["DarkRock", "CliffRock", "Granite"]),
        "roof": dict(ramp=["Slate", "Ashgrey", "Iron"], rows_per_tile=5, unit_w=(18, 26)),
    },
    "PaleStone": {   # pale ashlar under blue slate
        "wall": lambda s: ashlar_wall(s, ["Wool", "Linen", "SaltFlat"]),
        "roof": dict(ramp=["Indigo", "BayWater", "Slate"], rows_per_tile=5, unit_w=(18, 26)),
    },
}


def main():
    for i, (name, theme) in enumerate(THEMES.items()):
        theme["wall"](10 + i).save(f"{name}Wall")
        roof = theme["roof"]
        roof_rows(20 + i, run0=-OVERHANG, run1=1.0, **roof).save(f"{name}RoofEave")
        roof_rows(20 + i, run0=0.0, run1=1.0, **roof).save(f"{name}RoofSlope")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
