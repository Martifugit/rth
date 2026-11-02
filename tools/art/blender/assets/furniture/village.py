"""Furniture, village. What furnishes the basin's houses, chapel, bank, apothecary and the undercroft
under Tolling's bell tower: tables, seats, beds, chests, shelves, the hearth, the altar and pews, the
counter, lectern and clerk's desk, a bench and a clothes press, a bookshelf, a bunk, the biers, coffins and the dead
laid out; and the tithe font by a sworn door.

Each faces Blender -Y (where you stand to use it; a seat's sitter faces -Y) and stands on the origin,
the middle of its footprint (given in its docstring, along X first). Plain country work: oak and
sawn boards with the grain run along every part, iron only where it holds something, linen and wool
in dull close colours; carved stone is plain smooth stone (Gravestone), masonry only on the hearth.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Furniture/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\furniture\\village.py")
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
from artlib.props import bar, box, cyl, legs, make, planks, rod  # noqa: E402
from artlib.village import BLOOD, STONE, piece  # noqa: E402

COLL = "Furniture"
TABLE_H = 0.76                  # a table top, for a player 1.9 tall
SEAT_H = 0.46


def piece_font():
    """The tithe font by a sworn door: a stone basin on a stem, dark with blood."""
    bm = bmesh.new()
    A.loft(bm, [((0, 0, 0), 0.16, 0.16), ((0, 0, 0.1), 0.16, 0.16), ((0, 0, 0.12), 0.07, 0.07),
                ((0, 0, 0.77), 0.06, 0.06), ((0, 0, 0.82), 0.17, 0.17), ((0, 0, 0.95), 0.19, 0.19)],
           sides=8, ref=(0, 1, 0), mats=STONE)
    A.loft(bm, [((0, 0, 0.954), 0.15, 0.15), ((0, 0, 0.956), 0, 0)], sides=8, ref=(0, 1, 0), mats=BLOOD)
    return bm


# ---- tables and seats ------------------------------------------------------------------------------

def table(seed=1, at=(0, 0, 0)):
    """A farmhouse table. 2 x 1. Three boards along X on a frame, four square legs tied by a low
    stretcher along the middle."""
    mats = ["Oak", "Timber"]
    OAK, TIMBER = range(2)
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.8, 0.8, -0.4, 0.4, TABLE_H
    planks(bm, x0, x1, y0, y1, H - 0.05, H, 3, OAK)
    box(bm, x0 + 0.08, y0 + 0.06, H - 0.14, x1 - 0.08, y1 - 0.06, H - 0.05, TIMBER)          # the apron
    legs(bm, x0, x1, y0, y1, H - 0.05, 0.08, TIMBER)
    box(bm, x0 + 0.12, -0.03, 0.14, x1 - 0.12, 0.03, 0.2, TIMBER)
    for x in (x0 + 0.08, x1 - 0.08):
        box(bm, x - 0.03, y0 + 0.08, 0.14, x + 0.03, y1 - 0.08, 0.2, TIMBER)
    return make("Table", bm, mats, COLL, at)


def trestle_table(seed=1, at=(0, 0, 0)):
    """A trestle table, put up for a meal or a laying-out and taken down after. 2 x 1. Two loose
    boards along X on a pair of trestles, each an A of two legs under a cross-bar."""
    mats = ["Planks", "DeadWood"]
    BOARD, TRESTLE = range(2)
    bm = bmesh.new()
    H = TABLE_H - 0.02
    planks(bm, -0.95, 0.95, -0.34, 0.34, H - 0.045, H, 2, BOARD, gap=0.02)
    for x in (-0.65, 0.65):
        bar(bm, (x, -0.32, H - 0.07), (x, 0.32, H - 0.07), 0.07, 0.05, TRESTLE)            # the cross-bar
        for y in (-0.24, 0.24):
            for s in (-1, 1):
                bar(bm, (x + s * 0.17, y, 0.0), (x, y, H - 0.09), 0.05, 0.05, TRESTLE)
        bar(bm, (x - 0.1, 0, 0.3), (x + 0.1, 0, 0.3), 0.04, 0.04, TRESTLE)
        bar(bm, (x, -0.24, 0.3), (x, 0.24, 0.3), 0.04, 0.04, TRESTLE)                      # a brace between the A's
    return make("TrestleTable", bm, mats, COLL, at)


def stool(seed=1, at=(0, 0, 0)):
    """A three-legged stool. 1 x 1. A round slab of a seat, three legs splayed out under it."""
    mats = ["Oak", "Timber"]
    bm = bmesh.new()
    cyl(bm, 0, 0, SEAT_H - 0.05, SEAT_H, 0.17, sides=8, mat=0)
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 2
        rod(bm, (0.2 * math.cos(a), 0.2 * math.sin(a), 0.0), (0.09 * math.cos(a), 0.09 * math.sin(a), SEAT_H - 0.04),
            0.022, 1, sides=5)
    return make("Stool", bm, mats, COLL, at)


def chair(seed=1, at=(0, 0, 0)):
    """A plain chair. 1 x 1. A board seat on four legs, the back legs rising into a back of two
    broad slats; the sitter faces -Y."""
    mats = ["Oak", "Timber"]
    OAK, TIMBER = range(2)
    bm = bmesh.new()
    w, d = 0.22, 0.2
    box(bm, -w, -d, SEAT_H - 0.04, w, d, SEAT_H, OAK)
    for x in (-w + 0.03, w - 0.03):
        box(bm, x - 0.025, -d + 0.005, 0, x + 0.025, -d + 0.055, SEAT_H - 0.04, TIMBER)
        bar(bm, (x, d - 0.03, 0), (x, d + 0.02, 1.0), 0.05, 0.05, TIMBER)                   # back legs, a little raked
        box(bm, x - 0.02, -d + 0.03, 0.14, x + 0.02, d - 0.03, 0.18, TIMBER)                 # side stretchers
    for z in (0.66, 0.88):
        bar(bm, (-w + 0.03, d + z * 0.02 - 0.03, z), (w - 0.03, d + z * 0.02 - 0.03, z), 0.03, 0.09, OAK)
    return make("Chair", bm, mats, COLL, at)


def pew(seed=1, at=(0, 0, 0)):
    """A chapel pew. 2 x 1. A long seat and a raked back between two solid end boards with rounded
    tops, the congregation facing -Y (the altar)."""
    mats = ["Oak", "Timber"]
    OAK, TIMBER = range(2)
    bm = bmesh.new()
    x0, x1 = -0.9, 0.9
    box(bm, x0, -0.2, SEAT_H - 0.05, x1, 0.18, SEAT_H, OAK)
    bar(bm, (x0, 0.2, SEAT_H + 0.02), (x1, 0.2, SEAT_H + 0.02), 0.04, 0.05, TIMBER)
    for k, z in enumerate((0.62, 0.82)):
        bar(bm, (x0, 0.22 + 0.03 * k, z), (x1, 0.22 + 0.03 * k, z), 0.035, 0.16, OAK, up=(0, 0.15, 1))
    for x in (x0 - 0.03, x1 + 0.03):
        outline = [(-0.24, 0.0), (0.3, 0.0), (0.3, 0.9)] + [(0.3 - 0.11 + 0.11 * math.cos(t), 0.9 + 0.11 * math.sin(t))
                                                           for t in (math.pi * k / 4 for k in range(5))] + [(-0.24, SEAT_H + 0.15)]
        A.hull(bm, [(x + dx, y, z) for y, z in outline for dx in (-0.025, 0.025)], TIMBER)
    return make("Pew", bm, mats, COLL, at)


# ---- beds ------------------------------------------------------------------------------------------

def bed(seed=1, at=(0, 0, 0)):
    """A box bed. 1 x 2 (along Y). A plank frame with a headboard at +Y, a straw tick in it, a wool
    blanket over the lower two-thirds and a bolster at the head."""
    rnd = random.Random(seed)
    mats = ["Oak", "Timber", "Linen", "Wool", "Russet"]
    OAK, TIMBER, TICK, BOLSTER, BLANKET = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1 = -0.45, 0.45, -1.0, 1.0
    for y in (y0, y1 - 0.06):
        box(bm, x0, y, 0, x1, y + 0.06, 0.38, OAK)
    for x in (x0, x1 - 0.05):
        box(bm, x, y0, 0.12, x + 0.05, y1, 0.36, OAK)
    box(bm, x0, y1 - 0.06, 0.38, x1, y1, 0.85, OAK)                                         # the headboard
    for x in (x0, x1 - 0.07):
        box(bm, x, y1 - 0.08, 0, x + 0.07, y1 + 0.01, 0.92, TIMBER)
    A.hull(bm, [(x, y, z) for x in (x0 + 0.06, x1 - 0.06) for y in (y0 + 0.06, y1 - 0.07) for z in (0.24, 0.42)] +
           [(x, y, 0.45) for x in (x0 + 0.12, x1 - 0.12) for y in (y0 + 0.12, y1 - 0.13)], TICK)
    A.loft(bm, [((x0 + 0.08, y1 - 0.2, 0.5), 0.1, 0.06), ((x1 - 0.08, y1 - 0.2, 0.5), 0.1, 0.06)], sides=6,
           ref=(0, 0, 1), mats=BOLSTER)
    yb = y1 - 0.48                                                                            # the blanket's turned edge
    top = [(x, y, 0.47 + rnd.uniform(-0.01, 0.01)) for x in (x0 + 0.02, x1 - 0.02) for y in (y0 + 0.05, yb)]
    A.hull(bm, top + [(x, y, 0.2) for x in (x0 - 0.015, x1 + 0.015) for y in (y0 + 0.02, yb)], BLANKET)
    return make("Bed", bm, mats, COLL, at, smooth=35)


def cradle(seed=1, at=(0, 0, 0)):
    """A rocking cradle. 1 x 1. A plank box on two curved rockers, a hood over the head end, a
    small blanket in it."""
    mats = ["Oak", "Timber", "Wool"]
    OAK, TIMBER, WOOL = range(3)
    bm = bmesh.new()
    L, W, z0, z1 = 0.42, 0.22, 0.16, 0.48
    box(bm, -W, -L, z0, W, L, z0 + 0.03, OAK)
    for s in (-1, 1):
        box(bm, s * W - 0.015, -L, z0, s * W + 0.015, L, z1, OAK)
        box(bm, -W, s * L - 0.015, z0, W, s * L + 0.015, z1 + (0.08 if s > 0 else 0.0), OAK)
    A.hull(bm, [(x, y, z) for x in (-W - 0.02, W + 0.02) for y, z in ((L, z1 + 0.08), (L - 0.3, z1 + 0.05))] +
           [(x, y, z) for x in (-W + 0.02, W - 0.02) for y, z in ((L - 0.02, z1 + 0.04), (L - 0.28, z1 + 0.02))], OAK)
    for y in (-L + 0.08, L - 0.08):                                                          # the rockers
        pts = [(0.34 * math.sin(t), 0.25 - 0.25 * math.cos(t) * 0.9) for t in (math.radians(a) for a in range(-60, 61, 20))]
        A.hull(bm, [(x, y + dy, z) for x, z in pts for dy in (-0.02, 0.02)] +
               [(x, y + dy, z0) for x in (-0.3, 0.3) for dy in (-0.02, 0.02)], TIMBER)
    A.hull(bm, [(x, y, z) for x in (-W + 0.03, W - 0.03) for y in (-L + 0.04, L - 0.32) for z in (z0 + 0.03, z0 + 0.2)], WOOL)
    return make("Cradle", bm, mats, COLL, at)


# ---- chests, shelves and the dresser ---------------------------------------------------------------

def chest(seed=1, at=(0, 0, 0)):
    """A plank chest at the foot of a bed. 1 x 1. Boards on a frame, a lid a little proud of the box,
    two iron straps over the lid and down the front, a hasp."""
    mats = ["Planks", "Oak", "Iron"]
    BOARD, OAK, IRON = range(3)
    bm = bmesh.new()
    w, d, h = 0.42, 0.24, 0.42
    box(bm, -w, -d, 0.03, w, d, h, BOARD)
    for x in (-w, w):
        for y in (-d, d):
            box(bm, x - 0.03, y - 0.03, 0, x + 0.03, y + 0.03, h, OAK)
    box(bm, -w - 0.035, -d - 0.035, h, w + 0.035, d + 0.035, h + 0.06, OAK)                     # the lid
    for x in (-w * 0.55, w * 0.55):
        box(bm, x - 0.025, -d - 0.045, h + 0.06, x + 0.025, d + 0.045, h + 0.07, IRON)
        box(bm, x - 0.025, -d - 0.045, h - 0.16, x + 0.025, -d - 0.035, h + 0.07, IRON)
    box(bm, -0.04, -d - 0.05, h - 0.12, 0.04, -d - 0.035, h + 0.03, IRON)                       # the hasp
    return make("Chest", bm, mats, COLL, at)


def shelf_unit(name, things, seed=1, at=(0, 0, 0)):
    """Open shelves against a wall (at +Y). 2 x 1. Two uprights, three shelves and a board back;
    `things(bm, x0, x1, y, z, rnd, mats)` stocks a shelf at height z."""
    rnd = random.Random(seed)
    mats = ["Oak", "Planks", "Glaze", "Ochre", "RedRock", "Linen", "Umber", "Russet", "Moor", "Hide"]
    OAK, BOARD = range(2)
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.8, 0.8, -0.1, 0.18, 1.85
    for x in (x0, x1 - 0.05):
        box(bm, x, y0, 0, x + 0.05, y1, H, OAK)
    box(bm, x0 + 0.05, y1 - 0.02, 0.1, x1 - 0.05, y1, H, BOARD)
    for z in (0.12, 0.62, 1.12, 1.6):
        box(bm, x0 + 0.05, y0, z - 0.03, x1 - 0.05, y1 - 0.02, z, OAK)
        if z < 1.7:
            things(bm, x0 + 0.1, x1 - 0.1, (y0 + y1) / 2 - 0.01, z, rnd, mats)
    box(bm, x0 - 0.02, y0 - 0.02, H, x1 + 0.02, y1, H + 0.04, OAK)
    return make(name, bm, mats, COLL, at)


def stock_household(bm, x0, x1, y, z, rnd, mats):
    """A shelf of a house: crocks, a jug, folded cloth, a basket."""
    x = x0
    while x < x1 - 0.12:
        kind = rnd.choice(("crock", "crock", "jug", "cloth", "gap"))
        if kind == "crock":
            r, h = rnd.uniform(0.06, 0.08), rnd.uniform(0.1, 0.16)
            cyl(bm, x + r, y, z, z + h, r, r * 0.8, sides=6, mat=mats.index("Ochre"))
            x += 2 * r + 0.04
        elif kind == "jug":
            A.loft(bm, [((x + 0.07, y, z), 0.06, 0.06), ((x + 0.07, y, z + 0.1), 0.075, 0.075),
                        ((x + 0.07, y, z + 0.2), 0.035, 0.035), ((x + 0.07, y, z + 0.23), 0.045, 0.045)],
                   sides=8, ref=(1, 0, 0), mats=mats.index("RedRock"))
            x += 0.18
        elif kind == "cloth":
            for k in range(rnd.randint(2, 3)):
                box(bm, x, y - 0.1, z + 0.035 * k, x + 0.28, y + 0.08, z + 0.035 * (k + 1) - 0.004, mats.index("Linen"))
            x += 0.33
        else:
            x += 0.12


def stock_jars(bm, x0, x1, y, z, rnd, mats):
    """A shelf of the apothecary's: stoppered jars of glass and glazed clay, close-packed."""
    x = x0
    while x < x1 - 0.08:
        r, h = rnd.uniform(0.04, 0.06), rnd.uniform(0.1, 0.2)
        body = rnd.choice(("Glaze", "Glaze", "Ochre", "RedRock"))
        cyl(bm, x + r, y, z, z + h, r, r * 0.92, sides=6, mat=mats.index(body))
        cyl(bm, x + r, y, z + h, z + h + 0.025, r * 0.7, sides=5, mat=mats.index("Umber"))
        x += 2 * r + rnd.uniform(0.015, 0.04)


def dresser(seed=1, at=(0, 0, 0)):
    """A kitchen dresser against a wall (at +Y). 2 x 1. A cupboard base with two doors under a work
    top, and above it a rack of shelves with plates stood on edge and jugs hung below."""
    rnd = random.Random(seed)
    mats = ["Oak", "Planks", "Iron", "Ochre", "RedRock"]
    OAK, BOARD, IRON, PLATE, JUG = range(len(mats))
    bm = bmesh.new()
    x0, x1, H = -0.8, 0.8, 0.86
    box(bm, x0, -0.24, 0, x1, 0.22, H - 0.04, BOARD)
    box(bm, x0 - 0.03, -0.27, H - 0.04, x1 + 0.03, 0.24, H, OAK)
    for cx in (-0.4, 0.4):                                                                   # the doors, proud of the carcass
        box(bm, cx - 0.33, -0.255, 0.1, cx + 0.33, -0.24, H - 0.12, OAK)
        box(bm, cx - (0.26 if cx > 0 else -0.26) - 0.02, -0.275, 0.44, cx - (0.26 if cx > 0 else -0.26) + 0.02, -0.255, 0.5, IRON)
    box(bm, x0, -0.24, 0, x1, 0.22, 0.08, OAK)                                               # the plinth
    for x in (x0, x1 - 0.05):
        box(bm, x, 0.0, H, x + 0.05, 0.22, 1.95, OAK)
    box(bm, x0 + 0.05, 0.2, H, x1 - 0.05, 0.22, 1.95, BOARD)
    box(bm, x0 - 0.02, -0.02, 1.95, x1 + 0.02, 0.24, 2.0, OAK)
    for z in (1.25, 1.62):
        box(bm, x0 + 0.05, 0.02, z - 0.025, x1 - 0.05, 0.2, z, OAK)
        bar(bm, (x0 + 0.05, 0.03, z + 0.05), (x1 - 0.05, 0.03, z + 0.05), 0.015, 0.02, OAK)  # the rail plates lean on
        x = x0 + 0.17
        while x < x1 - 0.15:                                                                  # plates on edge, leaning back
            r = rnd.uniform(0.11, 0.13)
            A.loft(bm, [((x, 0.08, z + r), r, r), ((x, 0.1, z + r + 0.01), r, r)], sides=8, ref=(0, 0, 1), mats=PLATE)
            x += 2 * r + 0.03
    for x in (-0.55, -0.15, 0.25, 0.6):                                                       # jugs on hooks under the lower shelf
        rod(bm, (x, 0.05, 1.22), (x, 0.05, 1.17), 0.006, IRON, sides=3)
        A.loft(bm, [((x, 0.05, 1.0), 0.05, 0.05), ((x, 0.05, 1.07), 0.065, 0.065), ((x, 0.05, 1.15), 0.035, 0.035)],
               sides=7, ref=(1, 0, 0), mats=JUG)
    return make("Dresser", bm, mats, COLL, at)


# ---- the hearth ------------------------------------------------------------------------------------

def hearth(seed=1, at=(0, 0, 0)):
    """The open hearth of a cottage, against a wall (at +Y). 2 x 1. A raised hearthstone, stone
    cheeks and a fire-back under a heavy oak mantel beam, the hood narrowing from it to the ceiling
    (the stack above the roof is the kit's chimney); logs burning on fire-dogs, an iron pot on a
    crane."""
    rnd = random.Random(seed)
    mats = ["Stone", "Granite", "Oak", "Bark", "Ash", "Ember", "Blackiron", "Charcoal"]
    STONE_, HEARTHSTONE, OAK, BARK, LOG_END, EMBER, IRON, SOOT = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1 = -0.95, 0.95, -0.45, 0.2
    box(bm, x0, y0, 0, x1, y1, 0.1, HEARTHSTONE)
    for x in (x0, x1 - 0.3):
        box(bm, x, -0.1, 0.1, x + 0.3, y1, 1.35, STONE_)
    box(bm, x0 + 0.3, 0.08, 0.1, x1 - 0.3, y1, 1.35, SOOT)                                    # the fire-back, black with soot
    box(bm, x0 - 0.04, -0.16, 1.35, x1 + 0.04, y1, 1.55, OAK)                                  # the mantel beam
    A.hull(bm, [(x, y, 1.55) for x in (x0 - 0.02, x1 + 0.02) for y in (-0.14, y1)] +
           [(x, y, 2.6) for x in (-0.45, 0.45) for y in (0.0, y1)], STONE_)
    for x in (-0.3, 0.3):                                                                    # fire-dogs and the logs on them
        box(bm, x - 0.02, -0.15, 0.1, x + 0.02, 0.05, 0.22, IRON)
    for k, (y, z) in enumerate(((-0.08, 0.25), (0.0, 0.25), (-0.04, 0.37))):
        rings, faces = A.loft(bm, [((-0.42, y, z), 0.07, 0.07), ((0.42, y, z + rnd.uniform(-0.02, 0.02)), 0.065, 0.065)],
                              sides=6, ref=(0, 0, 1), mats=BARK, roll=rnd.uniform(0, 1))
        for f in faces:
            if len(f.verts) == 6:
                f.material_index = LOG_END
    A.hull(bm, [(0.4 * math.cos(a), -0.05 + 0.18 * math.sin(a), 0.1) for a in (2 * math.pi * k / 8 for k in range(8))] +
           [(0.0, -0.05, 0.2)], EMBER)
    rod(bm, (x1 - 0.32, 0.0, 0.2), (x1 - 0.32, 0.0, 1.2), 0.02, IRON, sides=5)               # the crane and its pot
    rod(bm, (x1 - 0.32, 0.0, 1.1), (0.15, -0.05, 1.1), 0.018, IRON, sides=4)
    rod(bm, (0.15, -0.05, 1.1), (0.15, -0.05, 0.72), 0.008, IRON, sides=3)
    A.loft(bm, [((0.15, -0.05, 0.5), 0.1, 0.1), ((0.15, -0.05, 0.6), 0.14, 0.14), ((0.15, -0.05, 0.72), 0.12, 0.12)],
           sides=8, ref=(1, 0, 0), mats=IRON)
    return make("Hearth", bm, mats, COLL, at)


# ---- the chapel ------------------------------------------------------------------------------------

def altar(seed=1, at=(0, 0, 0)):
    """The village chapel's altar. 2 x 1. A plain stone block with a moulded top, a linen cloth over it
    hanging down the front, two candles in iron sticks and a bowl for the blood between them."""
    mats = ["Gravestone", "Linen", "Iron", "CandleWax", "Ember", "Bronze", "Blood"]
    STONE_, CLOTH, IRON, WAX, FLAME, BRONZE, BLOOD_ = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.75, 0.75, -0.35, 0.35, 0.95
    box(bm, x0 - 0.06, y0 - 0.06, 0, x1 + 0.06, y1 + 0.06, 0.1, STONE_)
    box(bm, x0, y0, 0.1, x1, y1, H - 0.08, STONE_)
    box(bm, x0 - 0.05, y0 - 0.05, H - 0.08, x1 + 0.05, y1 + 0.05, H, STONE_)
    box(bm, x0 + 0.05, y0 - 0.058, H - 0.003, x1 - 0.05, y1 - 0.1, H + 0.01, CLOTH)          # the cloth over the top...
    box(bm, -0.45, y0 - 0.065, H - 0.5, 0.45, y0 - 0.052, H + 0.01, CLOTH)                     # ...and down the front
    for x in (-0.55, 0.55):
        cyl(bm, x, 0.05, H + 0.01, H + 0.04, 0.07, 0.05, sides=6, mat=IRON)
        cyl(bm, x, 0.05, H + 0.04, H + 0.2, 0.012, sides=4, mat=IRON)
        cyl(bm, x, 0.05, H + 0.2, H + 0.23, 0.05, 0.05, sides=6, mat=IRON)
        cyl(bm, x, 0.05, H + 0.23, H + 0.42, 0.025, sides=6, mat=WAX)
        A.spike(bm, (x, 0.05, H + 0.415), 0.012, (x, 0.05, H + 0.48), sides=4, mat=FLAME)
    A.loft(bm, [((0, 0.05, H + 0.01), 0.07, 0.07), ((0, 0.05, H + 0.03), 0.07, 0.07), ((0, 0.05, H + 0.09), 0.13, 0.13)],
           sides=10, ref=(1, 0, 0), mats=BRONZE)
    cyl(bm, 0, 0.05, H + 0.075, H + 0.076, 0.11, sides=10, mat=BLOOD_)
    return make("Altar", bm, mats, COLL, at)


# ---- the bank and the shop -------------------------------------------------------------------------

def counter(seed=1, at=(0, 0, 0)):
    """A shop or clerk's counter. 2 x 1. A panelled front toward the customer (-Y), a broad top
    overhanging it, a shelf behind for the keeper's things, a small iron-bound cash box on top."""
    mats = ["Oak", "Planks", "Iron", "Umber"]
    OAK, BOARD, IRON, BOX = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.95, 0.95, -0.25, 0.25, 1.0
    box(bm, x0, y0, 0, x1, y0 + 0.05, H - 0.05, BOARD)
    for x in (x0, x1 - 0.06):
        box(bm, x, y0, 0, x + 0.06, y1, H - 0.05, OAK)
    for k in range(4):                                                                         # the panels' stiles
        x = x0 + (k + 1) * (x1 - x0) / 5
        box(bm, x - 0.03, y0 - 0.015, 0.06, x + 0.03, y0, H - 0.1, OAK)
    box(bm, x0, y0 - 0.015, 0.0, x1, y0, 0.1, OAK)
    box(bm, x0 - 0.04, y0 - 0.1, H - 0.05, x1 + 0.04, y1, H, OAK)
    box(bm, x0 + 0.06, y1 - 0.25, 0.45, x1 - 0.06, y1, 0.48, BOARD)                           # the shelf behind
    box(bm, 0.45, -0.05, H, 0.75, 0.15, H + 0.14, BOX)
    for x in (0.5, 0.7):
        box(bm, x - 0.012, -0.06, H, x + 0.012, 0.16, H + 0.15, IRON)
    return make("Counter", bm, mats, COLL, at)


def lectern(seed=1, at=(0, 0, 0)):
    """A lectern with the ledger lying open on it. 1 x 1. A post on a cross of feet, a sloping desk
    with a lip to hold the book, which faces the reader at -Y."""
    mats = ["Oak", "Timber", "Linen", "Umber", "Charcoal"]
    OAK, TIMBER, PAGE, COVER, INK = range(len(mats))
    bm = bmesh.new()
    for a in (0, math.pi / 2):
        bar(bm, (-0.25 * math.cos(a), -0.25 * math.sin(a), 0.03), (0.25 * math.cos(a), 0.25 * math.sin(a), 0.03),
            0.07, 0.06, TIMBER)
    box(bm, -0.05, -0.05, 0.06, 0.05, 0.05, 1.0, OAK)
    slope = math.radians(25)

    def desk(u, v, h):                                                                         # u across, v up the slope
        return (u, -v * math.cos(slope) + 0.0, 1.0 + v * math.sin(slope) + h)
    A.hull(bm, [desk(u, v, h) for u in (-0.28, 0.28) for v in (-0.2, 0.2) for h in (0, 0.03)], OAK)
    A.hull(bm, [desk(u, -0.2, h) for u in (-0.28, 0.28) for h in (0.03, 0.06)] +
           [desk(u, -0.18, h) for u in (-0.28, 0.28) for h in (0.03, 0.06)], OAK)               # the lip
    A.hull(bm, [desk(u, v, h) for u in (-0.24, 0.24) for v in (-0.17, 0.16) for h in (0.03, 0.045)], COVER)
    for s in (-1, 1):
        A.hull(bm, [desk(s * u, v, h) for u in (0.01, 0.22) for v in (-0.16, 0.15) for h in (0.045, 0.06)], PAGE)
        for k in range(4):                                                                     # lines of the hand, painted
            v0 = 0.1 - k * 0.07
            A.hull(bm, [desk(s * u, v, 0.0605) for u in (0.04, 0.19) for v in (v0, v0 + 0.012)], INK)
    return make("Lectern", bm, mats, COLL, at)


# ---- the dead ----------------------------------------------------------------------------------------

def bier_frame(bm, mats, OAK, TIMBER, L=1.0, W=0.34, H=0.7):
    """A bier: a board bed on four legs, its two carrying poles running out past each end as handles."""
    planks(bm, -W, W, -L, L, H - 0.04, H, 4, OAK, along="y")
    for x in (-W + 0.04, W - 0.04):
        rod(bm, (x, -L - 0.3, H - 0.07), (x, L + 0.3, H - 0.07), 0.035, TIMBER, sides=6)
        for y in (-L + 0.1, L - 0.1):
            box(bm, x - 0.03, y - 0.03, 0, x + 0.03, y + 0.03, H - 0.07, TIMBER)
    for y in (-L + 0.1, L - 0.1):
        box(bm, -W + 0.04, y - 0.02, 0.2, W - 0.04, y + 0.02, 0.25, TIMBER)


def bier(seed=1, at=(0, 0, 0)):
    """The dead-house's bier, where every player first wakes. 1 x 2 (along Y), the head at +Y. The
    shroud lies over its boards and hangs down both sides, thrown back off the head end and gathered
    in folds at the foot (the one who lay under it has just got up)."""
    rnd = random.Random(seed)
    mats = ["Oak", "Timber", "Linen"]
    OAK, TIMBER, SHROUD = range(3)
    bm = bmesh.new()
    L, W, H = 1.0, 0.34, 0.7
    bier_frame(bm, mats, OAK, TIMBER, L, W, H)
    A.hull(bm, [(x, y, z) for x in (-W - 0.02, W + 0.02) for y in (-L + 0.02, L - 0.55) for z in (H + 0.002, H + 0.015)] +
           [(x, y, H - 0.3) for x in (-W - 0.035, W + 0.035) for y in (-L + 0.1, L - 0.6)], SHROUD)
    for k in range(3):                                                                         # the folds thrown back
        y = L - 0.55 + 0.1 * k
        A.loft(bm, [((-W + 0.03, y, H + 0.04 + 0.01 * k), 0.05, 0.035), ((W - 0.03, y + rnd.uniform(-0.03, 0.03), H + 0.04), 0.05, 0.035)],
               sides=5, ref=(0, 0, 1), mats=SHROUD)
    return make("Bier", bm, mats, COLL, at, smooth=40)


def shrouded_body(seed=1, at=(0, 0, 0)):
    """One of the dead laid out under a shroud on a bier. 1 x 2 (along Y), the head at +Y. The shape
    under the linen is only the body's silhouette: feet up, the long legs, the hands folded on the
    breast, the head; the shroud's hem falls down both sides."""
    mats = ["Oak", "Timber", "Linen"]
    OAK, TIMBER, SHROUD = range(3)
    bm = bmesh.new()
    L, W, H = 1.0, 0.34, 0.7
    bier_frame(bm, mats, OAK, TIMBER, L, W, H)
    z = H + 0.01
    A.loft(bm, [((0, -0.92, z + 0.06), 0, 0), ((0, -0.88, z + 0.1), 0.15, 0.1), ((0, -0.75, z + 0.06), 0.14, 0.07),
                ((0, -0.3, z + 0.08), 0.17, 0.08), ((0, 0.05, z + 0.1), 0.2, 0.1), ((0, 0.18, z + 0.14), 0.19, 0.13),
                ((0, 0.45, z + 0.11), 0.23, 0.11), ((0, 0.58, z + 0.06), 0.1, 0.05), ((0, 0.7, z + 0.1), 0.11, 0.1),
                ((0, 0.85, z + 0.09), 0.1, 0.08), ((0, 0.92, z + 0.04), 0, 0)], sides=8, ref=(0, 0, 1), mats=SHROUD)
    A.hull(bm, [(x, y, zz) for x in (-W - 0.02, W + 0.02) for y in (-L + 0.05, L - 0.05) for zz in (z - 0.008, z + 0.002)] +
           [(x, y, H - 0.32) for x in (-W - 0.035, W + 0.035) for y in (-L + 0.12, L - 0.12)], SHROUD)
    return make("ShroudedBody", bm, mats, COLL, at, smooth=45)


def stone_bier(seed=1, at=(0, 0, 0)):
    """A stone bier in the undercroft, where the dead of standing lie in state. 1 x 2 (along Y). A
    plinth, a plain body and a moulded top slab, all one smooth stone."""
    mats = ["Gravestone"]
    bm = bmesh.new()
    W, L = 0.42, 1.0
    box(bm, -W - 0.06, -L - 0.06, 0, W + 0.06, L + 0.06, 0.12, 0)
    box(bm, -W, -L, 0.12, W, L, 0.6, 0)
    A.hull(bm, [(x, y, 0.6) for x in (-W, W) for y in (-L, L)] +
           [(x, y, z) for x in (-W - 0.07, W + 0.07) for y in (-L - 0.07, L + 0.07) for z in (0.66, 0.74)] +
           [(x, y, 0.77) for x in (-W - 0.04, W + 0.04) for y in (-L - 0.04, L + 0.04)], 0)
    return make("StoneBier", bm, mats, COLL, at, smooth=20)


COFFIN = [(0.16, 0.95), (0.27, 0.55), (0.18, -0.95)]       # (half-width, y) head, shoulders, foot


def coffin_outline(inset=0.0):
    pts = [(w - inset, y) for w, y in COFFIN]
    return pts + [(-w, y) for w, y in reversed(pts)]


def coffin(seed=1, at=(0, 0, 0), lid=False):
    """A plain coffin of rough boards. 1 x 2 (along Y), the head at +Y. Wider at the shoulders than at
    the head and the foot; the boards' joints show along its sides. Open, the inside dark; the lid on
    its own is CoffinLid."""
    rnd = random.Random(seed)
    mats = ["Boards", "DeadWood", "Charcoal"]
    BOARD, EDGE, DARK = range(3)
    bm = bmesh.new()
    out = coffin_outline()
    if lid:
        A.hull(bm, [(x * 1.03, y * 1.02, z) for x, y in out for z in (0.0, 0.04)], BOARD)
        for k in (-1, 1):                                                                      # the joints between its boards
            A.hull(bm, [(k * 0.07 + dx, y, 0.041) for y in (-0.93, 0.93) for dx in (-0.006, 0.006)], EDGE)
        return make("CoffinLid", bm, mats, COLL, at, rot=rnd.uniform(-0.03, 0.03))
    H, t = 0.42, 0.025
    A.hull(bm, [(x, y, z) for x, y in out for z in (0.0, 0.03)], BOARD)
    A.hull(bm, [(x, y, 0.031) for x, y in coffin_outline(t)] + [(x, y, 0.035) for x, y in coffin_outline(t)], DARK)
    n = len(out)
    for k in range(n):
        (xa, ya), (xb, yb) = out[k], out[(k + 1) % n]
        bar(bm, (xa, ya, H / 2), (xb, yb, H / 2), 2 * t, H, BOARD)
        for z in (0.14, 0.28):
            bar(bm, (xa * 1.005, ya * 1.001, z), (xb * 1.005, yb * 1.001, z), 2 * t + 0.004, 0.012, EDGE)
    return make("Coffin", bm, mats, COLL, at)


def bench(seed=1, at=(0, 0, 0)):
    """A free-standing bench, for a table's side or a yard. 2 x 1. A thick plank seat on two slab
    legs, each with a notch cut in its foot, a stretcher between them."""
    mats = ["Oak", "Timber"]
    OAK, TIMBER = range(2)
    bm = bmesh.new()
    box(bm, -0.85, -0.16, SEAT_H - 0.06, 0.85, 0.16, SEAT_H, OAK)
    for x in (-0.6, 0.6):
        A.hull(bm, [(x + dx, y, z) for dx in (-0.03, 0.03) for y, z in ((-0.15, 0.0), (-0.05, 0.12), (0.05, 0.12), (0.15, 0.0),
                                                                       (-0.13, SEAT_H - 0.06), (0.13, SEAT_H - 0.06))], TIMBER)
    box(bm, -0.6, -0.025, 0.2, 0.6, 0.025, 0.26, TIMBER)
    return make("Bench", bm, mats, COLL, at)


def desk(seed=1, at=(0, 0, 0)):
    """A clerk's writing desk. 1 x 1 (the writer sits at -Y). A sloping lid on a box of drawers'
    height on four legs, a ledge at its foot, an inkwell and a quill at the top, a ledger lying open."""
    mats = ["Oak", "Timber", "Linen", "Umber", "Charcoal", "Iron"]
    OAK, TIMBER, PAGE, COVER, INK, IRON = range(len(mats))
    bm = bmesh.new()
    w, d = 0.48, 0.3
    legs(bm, -w, w, -d, d, 0.7, 0.06, TIMBER)
    box(bm, -w, -d, 0.55, w, d, 0.72, OAK)
    A.hull(bm, [(x, y, z) for x in (-w - 0.02, w + 0.02) for y, z in ((-d - 0.04, 0.78), (d + 0.02, 0.92))] +
           [(x, y, z - 0.06) for x in (-w - 0.02, w + 0.02) for y, z in ((-d - 0.04, 0.78), (d + 0.02, 0.92))], OAK)
    box(bm, -w - 0.02, -d - 0.06, 0.72, w + 0.02, -d - 0.02, 0.8, OAK)                       # the ledge
    box(bm, -w, d - 0.08, 0.92, w, d + 0.02, 0.96, OAK)                                       # the flat top
    cyl(bm, 0.32, d - 0.03, 0.96, 1.02, 0.035, sides=6, mat=INK)                              # the inkwell
    rod(bm, (0.32, d - 0.03, 1.0), (0.22, d + 0.02, 1.18), 0.006, PAGE, sides=3)
    slope = math.atan2(0.14, d * 2 + 0.06)
    for s in (-1, 1):                                                                        # the ledger, open on the slope
        A.hull(bm, [(s * u, y, 0.79 + (y + d + 0.04) * math.tan(slope) + h) for u in (0.01, 0.22) for y in (-0.2, 0.12)
                    for h in (0.0, 0.015)], PAGE)
    A.hull(bm, [(u, y, 0.785 + (y + d + 0.04) * math.tan(slope)) for u in (-0.24, 0.24) for y in (-0.22, 0.14)], COVER)
    return make("Desk", bm, mats, COLL, at)


def press(seed=1, at=(0, 0, 0)):
    """A clothes press against a wall (at +Y). 2 x 1. A tall cupboard of boards on a plinth, two
    doors in panelled frames, a cornice, iron hinges and a lock plate."""
    mats = ["Oak", "Planks", "Iron"]
    OAK, BOARD, IRON = range(3)
    bm = bmesh.new()
    x0, x1, H = -0.6, 0.6, 1.85
    box(bm, x0 - 0.03, -0.3, 0.0, x1 + 0.03, 0.27, 0.12, OAK)
    box(bm, x0, -0.27, 0.12, x1, 0.25, H, BOARD)
    box(bm, x0 - 0.05, -0.32, H, x1 + 0.05, 0.28, H + 0.1, OAK)
    for cx in (-0.3, 0.3):
        box(bm, cx - 0.27, -0.285, 0.2, cx + 0.27, -0.27, 0.26, OAK)
        box(bm, cx - 0.27, -0.285, H - 0.14, cx + 0.27, -0.27, H - 0.08, OAK)
        box(bm, cx - 0.27, -0.285, 0.2, cx - 0.21, -0.27, H - 0.08, OAK)
        box(bm, cx + 0.21, -0.285, 0.2, cx + 0.27, -0.27, H - 0.08, OAK)
        hx = cx - 0.27 if cx < 0 else cx + 0.27
        for z in (0.45, H - 0.35):
            box(bm, hx - 0.06, -0.29, z, hx + 0.06, -0.283, z + 0.04, IRON)
    box(bm, -0.04, -0.29, 0.95, 0.04, -0.283, 1.1, IRON)
    return make("Press", bm, mats, COLL, at)


def stock_books(bm, x0, x1, y, z, rnd, mats):
    """A shelf of books: spines of a few dull colours standing close, a leaning one, a gap now and then."""
    x = x0
    colours = [mats.index(c) for c in ("Umber", "Russet", "Moor", "Hide")]
    while x < x1 - 0.06:
        if rnd.random() < 0.1:
            x += 0.1
            continue
        w, h = rnd.uniform(0.04, 0.07), rnd.uniform(0.17, 0.23)                 # under the shelf above
        box(bm, x, y - 0.11, z, x + w, y + 0.09, z + h, rnd.choice(colours))
        x += w + 0.004


def bunk(seed=1, at=(0, 0, 0)):
    """A wardens' bunk, two beds one over the other. 1 x 2 (along Y). Four tall posts, two plank
    frames with straw ticks and a blanket each, a ladder of three rungs at the foot."""
    mats = ["Timber", "Planks", "Linen", "Moor"]
    TIMBER, BOARD, TICK, BLANKET = range(4)
    bm = bmesh.new()
    x0, x1, y0, y1 = -0.42, 0.42, -1.0, 1.0
    for x in (x0, x1):
        for y in (y0, y1):
            box(bm, x - 0.05, y - 0.05, 0, x + 0.05, y + 0.05, 1.85, TIMBER)
    for z in (0.3, 1.3):
        for x in (x0, x1 - 0.04):
            box(bm, x, y0, z, x + 0.04, y1, z + 0.14, TIMBER)
        box(bm, x0, y0, z - 0.02, x1, y1, z + 0.02, BOARD)
        box(bm, x0 + 0.05, y0 + 0.05, z + 0.02, x1 - 0.05, y1 - 0.05, z + 0.14, TICK)
        box(bm, x0 + 0.03, y0 + 0.05, z + 0.14, x1 - 0.03, y1 - 0.55, z + 0.17, BLANKET)
    for k in range(3):                                                                         # the ladder at the foot
        box(bm, x0 + 0.08, y0 - 0.06, 0.5 + 0.28 * k, x1 - 0.08, y0 - 0.03, 0.54 + 0.28 * k, TIMBER)
    return make("Bunk", bm, mats, COLL, at)


ASSETS = {
    "Font": piece(piece_font),
    "Table": table,
    "TrestleTable": trestle_table,
    "Stool": stool,
    "Chair": chair,
    "Pew": pew,
    "Bed": bed,
    "Cradle": cradle,
    "Chest": chest,
    "Shelves": lambda: shelf_unit("Shelves", stock_household),
    "JarShelves": lambda: shelf_unit("JarShelves", stock_jars, seed=3),
    "Dresser": dresser,
    "Hearth": hearth,
    "Altar": altar,
    "Counter": counter,
    "Lectern": lectern,
    "Bier": bier,
    "ShroudedBody": shrouded_body,
    "StoneBier": stone_bier,
    "Coffin": coffin,
    "CoffinLid": lambda: coffin(lid=True),
    "Bench": bench,
    "Desk": desk,
    "Press": press,
    "Bookshelf": lambda: shelf_unit("Bookshelf", stock_books, seed=5),
    "Bunk": bunk,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
