"""The crypt's door in the churchyard: the cellar kit's outdoor entrance, a stone porch over the
head of a stair going down. 2 x 3 tiles, the stair descending along +Y from the open arch at the
front (-Y). Rubble side walls and a back wall under a slate roof at the village pitch, its gable over
the round-headed arch; an iron grille standing open against the wall; inside, the steps going down
into the dark, the pit's sides lined in stone. Origin at the middle of the footprint, on the ground.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\landmarks\\village\\crypt_entrance.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""
import math
import os
import sys

import bmesh

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.props import arch, box, make  # noqa: E402

MATS = ["Stone", "Granite", "Slate", "Iron", "CryptFloor", "Charcoal"]
STONE, GRANITE, SLATE, IRON, STEP, DARK = range(len(MATS))


def crypt_entrance():
    bm = bmesh.new()
    W, D, T = 1.0, 1.5, 0.28                 # half-width, half-depth (along Y), wall thickness
    eaves, pitch = 2.0, math.radians(42)
    ridge = eaves + (W + T) * math.tan(pitch)
    for s in (-1, 1):                                                         # the side walls and the back wall
        box(bm, s * W - T / 2, -D, 0.0, s * W + T / 2, D, eaves, STONE)
    box(bm, -W, D - T, 0.0, W, D, eaves, STONE)
    for y in (-D, D - T):                                                     # the gables
        A.hull(bm, [(x, yy, eaves) for x in (-W - T / 2, W + T / 2) for yy in (y, y + T)] +
               [(0.0, yy, ridge - 0.1) for yy in (y, y + T)], STONE)
    for s in (-1, 1):                                                         # the front arch's jambs and head
        box(bm, s * (W - 0.3) - 0.15, -D, 0.0, s * (W - 0.3) + 0.15, -D + T, eaves, STONE)
    pts = arch(0.0, 1.6, 0.55, n=6)
    for (xa, za), (xb, zb) in zip(pts, pts[1:]):
        A.hull(bm, [(x, y, z) for x, z in ((xa, za), (xb, zb), (xa, eaves), (xb, eaves)) for y in (-D - 0.02, -D + T)], GRANITE)
    for s in (-1, 1):                                                         # the roof, overhanging
        A.hull(bm, [(s * x, y, z + t) for x, z in ((0.0, ridge), (W + T / 2 + 0.25, eaves - 0.2)) for y in (-D - 0.25, D + 0.25)
                    for t in (0.0, 0.1)], SLATE)
    for k in range(7):                                                        # the stair going down, along +Y
        y0 = -D + T + 0.15 + k * 0.32
        z = -0.28 * (k + 1)
        box(bm, -W + T / 2, y0, z - 0.1, W - T / 2, y0 + 0.34, z, STEP)
    box(bm, -W + T / 2, -D + T, -2.4, W - T / 2, D - T, -2.35, DARK)          # the dark below
    for s in (-1, 1):                                                         # the pit's sides
        box(bm, s * (W - T / 2) - 0.02, -D + T, -2.4, s * (W - T / 2) + 0.02, D - T, 0.0, STONE)
    box(bm, -W + T / 2, D - T - 0.02, -2.4, W - T / 2, D - T, 0.0, DARK)
    for k in range(5):                                                        # the grille, swung open against the left jamb
        y = -D - 0.08 - k * 0.15
        box(bm, -W + 0.135, y - 0.015, 0.0, -W + 0.165, y + 0.015, 1.7, IRON)
    for z in (0.15, 0.85, 1.6):
        box(bm, -W + 0.13, -D - 0.7, z, -W + 0.17, -D - 0.06, z + 0.04, IRON)
    return make("CryptEntrance", bm, MATS, "Landmarks")


ASSETS = {
    "CryptEntrance": crypt_entrance,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
