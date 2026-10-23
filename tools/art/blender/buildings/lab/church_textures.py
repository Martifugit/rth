#!/usr/bin/env python3
"""
The great church's painted textures (lab/monumental.py), in the paint lab's style
(lab/paint_textures.py): clean surfaces, a fine low-contrast pattern in close shades of a few
palette colours, richness from the contrast between materials rather than from grime.

  ChurchAshlar   pale dressed stone, big blocks a tile long (one repeat: 2 x 2 tiles)
  ChurchTrim     Synod red stone for string courses, drip courses, rails (1 x 1)
  ChurchSlate    dark slate in neat rows for the roofs and spires (2 x 1)
  ChurchGlass    red glass in diamond quarries, dark leads (1 x 1)
  ChurchFloor    big dark flagstones (4 x 4)
  ChurchBrick    the plain church's one stone: small high-contrast bricks (2 x 2)

    python3 tools/art/blender/buildings/lab/church_textures.py

Writes PNGs into textures/ beside this script. Standard library only; seeded, so repeatable.
"""
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from paint_textures import Image, col, mix, roof_rows, shade  # noqa: E402

W = 128


def ashlar(seed, ramp, courses, blocks, joint=0.72, lit=1.06, spread=0.4):
    """Square tile of dressed stone: `courses` rows of `blocks` blocks, every other row offset by
    half a block (so it repeats), thin joints, each block a shade of the ramp: close to its
    middle (spread 0.4), or anywhere along it (spread 1.0, for strong contrast)."""
    base = [col(n) for n in ramp]
    r = random.Random(seed)
    img = Image(W, W)
    ch, bw = W / courses, W / blocks
    shades = {(c, b): mix(base[1], r.choice(base), r.uniform(0.0, spread)) for c in range(courses) for b in range(blocks)}
    mortar = shade(mix(base[0], base[-1], 0.5), joint)
    for y in range(W):
        c = int(y / ch)
        yy = y - c * ch
        off = bw / 2 if c % 2 else 0
        for x in range(W):
            xx = (x + off) % W
            b = int(xx / bw)
            ix = xx - b * bw
            colour = shades[(c, b)]
            if yy < 1.5 or ix < 1.5:
                colour = mortar
            elif yy < 3.5 or ix < 3.5:
                colour = shade(colour, lit)
            img.set(x, W - 1 - y, colour)
    return img


def glass(seed, ramp, lead, pitch=32, width=2):
    """Diamond quarries: lead lines on both diagonals every `pitch` pixels; each diamond one of
    the ramp's shades, a little lighter toward its top."""
    base = [col(n) for n in ramp]
    dark = col(lead)
    r = random.Random(seed)
    img = Image(W, W)
    panes = {}
    for y in range(W):
        for x in range(W):
            a, b = (x + y) % W, (x - y) % W
            if a % pitch < width or b % pitch < width:
                img.set(x, y, dark)
                continue
            key = (a // pitch, b // pitch)
            if key not in panes:
                panes[key] = mix(base[1], r.choice(base), r.uniform(0.0, 0.5))
            c = panes[key]
            if (a % pitch) + (b % pitch) < pitch * 0.55:
                c = shade(c, 1.12)
            img.set(x, y, c)
    return img


def main():
    ashlar(31, ["Stone", "Gravestone", "Driftwood"], courses=4, blocks=2).save("ChurchAshlar")
    ashlar(32, ["SaintRobe", "SynodRed", "Brick"], courses=2, blocks=2, joint=0.8, lit=1.04).save("ChurchTrim")
    roof_rows(33, ["Iron", "Slate", "Ashgrey"], run0=0.0, run1=1.0, rows_per_tile=6, unit_w=(14, 22)).save("ChurchSlate")
    glass(34, ["FountBlood", "SynodRed", "Blood"], "CryptIron").save("ChurchGlass")
    ashlar(35, ["CryptFlagstone", "CryptFloor", "CryptDais"], courses=2, blocks=2, joint=0.65, lit=1.03).save("ChurchFloor")
    # The plain church's one stone, after Varrock castle's wall: small bricks (a quarter tile tall,
    # half a tile long over a 2 x 2 repeat), dark mortar outlines, light edges, strong contrast.
    ashlar(36, ["Granite", "Stone", "Gravestone"], courses=8, blocks=4, joint=0.5, lit=1.15, spread=1.0).save("ChurchBrick")
    print("wrote church textures")


if __name__ == "__main__":
    main()
