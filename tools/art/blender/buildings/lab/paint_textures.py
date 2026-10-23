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

Writes PNGs into textures/ beside this script. The painters are in ../painting.py.
"""
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from painting import Image, W, col, mix, plaster_wall, roof_rows, shade  # noqa: E402

OUT = os.path.join(HERE, "textures")
OVERHANG = 0.35            # the eave piece's run starts this far out (matches the kit)


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
        theme["wall"](10 + i).save(OUT, f"{name}Wall")
        roof = theme["roof"]
        roof_rows(20 + i, run0=-OVERHANG, run1=1.0, **roof).save(OUT, f"{name}RoofEave")
        roof_rows(20 + i, run0=0.0, run1=1.0, **roof).save(OUT, f"{name}RoofSlope")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
