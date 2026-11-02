r"""Furniture, dead. What the Landing offers the newly dead while they gather themselves: a worn plank
bench on two stone feet, a candle stub burning at one end.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
dead.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Furniture/Dead/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\furniture\dead.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import random
import sys

from mathutils import Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.caves import make  # noqa: E402
from assets.lights import dead as lights  # noqa: E402

MATS = ["DrownedWood", "DeadSlate", "CandleWax", "CandleFlame"]
WOOD, STONE, WAX, FLAME = range(4)

SEAT = 0.46                     # the seat's top
PLANK = (0.2, 0.06)             # a seat plank's width and thickness
FEET = (-0.05, 1.05)            # the stone feet's middles along the bench (east, from the origin's tile middle)
FOOT = (0.13, 0.22)             # a foot's half length and half depth


def stone_foot(bm, rnd, x, top):
    """A rough block of dark stone, set a little into the ground, its top dressed flat for the planks."""
    hx, hy = FOOT
    pts = []
    for z, k in ((-0.06, 1.08), (top * 0.5, 1.0), (top, 0.94)):
        for sx in (-1, 1):
            for sy in (-1, 1):
                j = 0.0 if z == top else 0.02
                pts.append((x + sx * hx * k + rnd.uniform(-j, j), sy * hy * k + rnd.uniform(-j, j), z))
    A.hull(bm, pts, mat=STONE)


def bench(seed=1, at=(0, 0, 0)):
    """A worn plank bench two tiles long: two seat planks, the front one split and short at its west end,
    resting on two rough stone feet; a candle stub stuck at the east end in a run of its wax.

    Built in the game's frame (x east) and turned half round at the end, so the export lands it east of
    its origin (the middle of its west tile) with its front to the game's south."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    pw, pt = PLANK
    under = SEAT - pt
    for x in FEET:
        stone_foot(bm, rnd, x, under)
    for k, y in enumerate((-0.11, 0.11)):                       # front (south) and back planks
        a = -0.45 + (0.08 if k == 0 else rnd.uniform(-0.02, 0.0))
        b = 1.45 + rnd.uniform(-0.02, 0.02)
        z = 0.0 if k else -0.006                                # the front one worn a little lower
        A.box(bm, (a, y - pw / 2 + 0.005, under + z), (b, y + pw / 2 - 0.005, SEAT + z), WOOD)
    # the split: a long sliver off the front plank's west end, sagging below it
    A.box(bm, (-0.45, -0.205, under - 0.025), (-0.12, -0.175, SEAT - 0.03), WOOD)
    before = set(bm.faces)
    lights.pool(bm, 0.07, 0.015, centre=(1.3, 0.04), points=6, z=SEAT)
    lights.candle(bm, (1.3, 0.04, SEAT + 0.01), 0.1, 0.03, (0.0, 0.0), True)
    lights.drip(bm, (1.3, 0.21, SEAT - 0.004), (0, 0, -1), 0.05, 0.018, (0, 1, 0))
    remap = {lights.WAX: WAX, lights.CFLAME: FLAME, lights.WICK: STONE, lights.BONE: WAX}
    for f in set(bm.faces) - before:
        f.material_index = remap[f.material_index]
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi, 3, "Z"))
    return make("Bench", bm, MATS, at=at, smooth=30)


ASSETS = {
    "Bench": bench,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
