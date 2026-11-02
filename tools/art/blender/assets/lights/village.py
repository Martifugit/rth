"""Lights, village. The village's own lights: an iron pricket stand with its candle, a lantern hung
from a beam, a street lantern on a timber post, an iron brazier. Iron and timber, the flame the one
bright thing (the glow is the game's to add).

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Lights/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\lights\\village.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import random
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.props import box, cyl, make, rod  # noqa: E402

COLL = "Lights"


def lantern(bm, x, y, z, IRON, GLOW, s=1.0):
    """A small iron lantern hanging from a ring at (x, y, z): a square frame with horn panes lit from
    within, a pyramid cap."""
    rod(bm, (x, y, z), (x, y, z - 0.06 * s), 0.008 * s, IRON, sides=4)
    A.hull(bm, [(x + dx * s, y + dy * s, z - 0.1 * s) for dx in (-0.09, 0.09) for dy in (-0.09, 0.09)] +
           [(x, y, z - 0.05 * s)], IRON)                                                           # the cap
    box(bm, x - 0.08 * s, y - 0.08 * s, z - 0.32 * s, x + 0.08 * s, y + 0.08 * s, z - 0.1 * s, GLOW)
    for dx in (-0.08, 0.08):                                                                       # the frame's corner bars
        for dy in (-0.08, 0.08):
            box(bm, x + dx * s - 0.012 * s, y + dy * s - 0.012 * s, z - 0.33 * s, x + dx * s + 0.012 * s, y + dy * s + 0.012 * s,
                z - 0.1 * s, IRON)
    box(bm, x - 0.095 * s, y - 0.095 * s, z - 0.36 * s, x + 0.095 * s, y + 0.095 * s, z - 0.32 * s, IRON)


def candle_stand(seed=1, at=(0, 0, 0)):
    """An iron pricket stand on the floor of the chapel or the undercroft: three splayed feet, a
    stem with a knop, a drip dish at the top and a tall candle on its spike, wax run down it."""
    mats = ["Blackiron", "CandleWax", "Ember"]
    IRON, WAX, FLAME = range(3)
    bm = bmesh.new()
    for k in range(3):
        a = 2 * math.pi * k / 3 - math.pi / 2
        rod(bm, (0.2 * math.cos(a), 0.2 * math.sin(a), 0.0), (0.0, 0.0, 0.22), 0.016, IRON, sides=4)
    cyl(bm, 0, 0, 0.2, 1.2, 0.018, sides=6, mat=IRON)
    A.loft(bm, [((0, 0, 0.6), 0.02, 0.02), ((0, 0, 0.63), 0.045, 0.045), ((0, 0, 0.66), 0.02, 0.02)], sides=6, ref=(1, 0, 0), mats=IRON)
    A.loft(bm, [((0, 0, 1.18), 0.03, 0.03), ((0, 0, 1.2), 0.12, 0.12), ((0, 0, 1.23), 0.13, 0.13)], sides=8, ref=(1, 0, 0), mats=IRON)
    cyl(bm, 0, 0, 1.2, 1.5, 0.035, 0.032, sides=6, mat=WAX)
    A.loft(bm, [((0.03, 0, 1.38), 0.012, 0.012), ((0.035, 0, 1.3), 0.014, 0.014), ((0.04, 0, 1.22), 0.0, 0.0)],
           sides=4, ref=(1, 0, 0), mats=WAX)                                                       # a run of wax
    A.spike(bm, (0, 0, 1.495), 0.016, (0, 0, 1.58), sides=4, mat=FLAME)
    return make("CandleStand", bm, mats, COLL, at)


def hanging_lantern(seed=1, at=(0, 0, 0)):
    """A lantern hung from a beam or the vault on a chain: the ceiling's hook at a storey's height
    (2.6), a chain of links, the lantern swinging at about head height."""
    mats = ["Blackiron", "CandleWax"]
    IRON, GLOW = range(2)
    bm = bmesh.new()
    top, bottom = 2.6, 2.0
    n = 8
    for k in range(n):                                                                             # links, turned alternately
        z = top - (top - bottom) * (k + 0.5) / n
        h = (top - bottom) / n * 0.6
        if k % 2:
            box(bm, -0.012, -0.03, z - h, 0.012, 0.03, z + h, IRON)
        else:
            box(bm, -0.03, -0.012, z - h, 0.03, 0.012, z + h, IRON)
    lantern(bm, 0, 0, bottom, IRON, GLOW, s=1.2)
    return make("HangingLantern", bm, mats, COLL, at)


def lamp_post(seed=1, at=(0, 0, 0)):
    """A street lantern in the square: a squared timber post on a stone footing, an iron bracket
    reaching out from its head with the lantern hung from it, a peg low down for the lamplighter's
    ladder."""
    mats = ["Timber", "Gravestone", "Blackiron", "CandleWax"]
    TIMBER, STONE, IRON, GLOW = range(4)
    bm = bmesh.new()
    A.hull(bm, [(x, y, 0.0) for x in (-0.18, 0.18) for y in (-0.18, 0.18)] +
           [(x, y, 0.25) for x in (-0.13, 0.13) for y in (-0.13, 0.13)], STONE)
    box(bm, -0.07, -0.07, 0.25, 0.07, 0.07, 2.65, TIMBER)
    A.hull(bm, [(x, y, 2.65) for x in (-0.09, 0.09) for y in (-0.09, 0.09)] + [(0, 0, 2.78)], TIMBER)
    box(bm, -0.015, -0.6, 2.45, 0.015, -0.07, 2.49, IRON)                                          # the bracket and its stay
    rod(bm, (0, -0.07, 2.1), (0, -0.4, 2.46), 0.012, IRON, sides=4)
    A.loft(bm, [((0, -0.56, 2.42), 0.02, 0.02), ((0, -0.56, 2.46), 0.02, 0.02)], sides=6, ref=(1, 0, 0), mats=IRON)
    lantern(bm, 0, -0.56, 2.42, IRON, GLOW, s=1.3)
    box(bm, -0.02, 0.07, 1.1, 0.02, 0.2, 1.14, TIMBER)
    return make("LampPost", bm, mats, COLL, at)


def brazier(seed=1, at=(0, 0, 0)):
    """An iron brazier for a gate, a yard or a vigil: a basket of iron bands on three legs at waist
    height, coals heaped in it with an ember glow (the flames are the game's to draw)."""
    rnd = random.Random(seed)
    mats = ["Blackiron", "Coal", "Ember"]
    IRON, COAL, EMBER = range(3)
    bm = bmesh.new()
    for k in range(3):
        a = 2 * math.pi * k / 3
        rod(bm, (0.32 * math.cos(a), 0.32 * math.sin(a), 0.0), (0.2 * math.cos(a), 0.2 * math.sin(a), 0.75), 0.022, IRON, sides=4)
    A.loft(bm, [((0, 0, 0.62), 0.12, 0.12), ((0, 0, 0.66), 0.14, 0.14)], sides=8, ref=(1, 0, 0), mats=IRON)
    for z, r in ((0.7, 0.22), (0.85, 0.3), (1.0, 0.34)):                                         # the basket's rings
        A.prism(bm, 10, r, r, z - 0.015, z + 0.015, mat_index=IRON, cap_top=False)               # open bands
    for k in range(8):                                                                            # its upright bands
        a = 2 * math.pi * k / 8
        rod(bm, (0.2 * math.cos(a), 0.2 * math.sin(a), 0.66), (0.34 * math.cos(a), 0.34 * math.sin(a), 1.02), 0.012, IRON, sides=3)
    A.loft(bm, [((0, 0, 0.66), 0.19, 0.19), ((0, 0, 0.9), 0.29, 0.29), ((0, 0, 0.98), 0.0, 0.0)], sides=8, ref=(1, 0, 0), mats=EMBER)
    for _ in range(10):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.0, 0.22)
        x, y, s = d * math.cos(a), d * math.sin(a), rnd.uniform(0.05, 0.08)
        A.hull(bm, [(x + rnd.uniform(-s, s), y + rnd.uniform(-s, s), 0.9 + rnd.uniform(0.0, 0.08) - d * 0.3) for _ in range(6)], COAL)
    return make("Brazier", bm, mats, COLL, at)


ASSETS = {
    "CandleStand": candle_stand,
    "HangingLantern": hanging_lantern,
    "LampPost": lamp_post,
    "Brazier": brazier,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
