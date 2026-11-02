r"""Rocks, dead. Low dark boulders worn round by the still water at the Landing's edges, ash settled pale
on their tops: one alone, and a big one with a small one leaning on it.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
dead.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Rocks/Dead/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\rocks\dead.py")
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
from artlib.caves import make  # noqa: E402

MATS = ["DeadSlate", "Ashworn"]
ROCK, ASH = range(2)


def boulder(bm, rnd, centre, size, lean=(0.0, 0.0), turn=0.0):
    """A rounded boulder: rings of points on a squat dome (lobed, not jittered at random), sunk at its foot,
    its top leaning; faces turned up take the settled ash."""
    cx, cy, cz = centre
    rx, ry, h = size
    pts = []
    for f, z, n in ((1.0, -0.12, 9), (1.04, 0.25, 9), (0.86, 0.62, 8), (0.5, 0.9, 6)):
        for k in range(n):
            a = turn + 2 * math.pi * (k + 0.5 * (z > 0.5)) / n
            lob = 1 + 0.08 * math.cos(3 * a + 1.1) + 0.05 * math.sin(2 * a) + rnd.uniform(-0.03, 0.03)
            t = max(z, 0)
            pts.append((cx + lean[0] * t + rx * f * lob * math.cos(a), cy + lean[1] * t + ry * f * lob * math.sin(a),
                        cz + h * z))
    pts.append((cx + lean[0] + rnd.uniform(-0.05, 0.05) * rx, cy + lean[1], cz + h * 1.0))
    for fc in A.hull(bm, pts, mat=ROCK):
        fc.normal_update()
        if fc.normal.z > 0.72:
            fc.material_index = ASH


def ash_rock_a(seed=61, at=(0, 0, 0)):
    """A single low rounded boulder, about a tile across and knee high."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    boulder(bm, rnd, (0, 0, 0), (0.48, 0.38, 0.46), lean=(0.04, -0.03))
    return make("AshRockA", bm, MATS, at=at, smooth=35)


def ash_rock_b(seed=62, at=(0, 0, 0)):
    """A big boulder and a small one leaning against its flank, together a little over a tile."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    boulder(bm, rnd, (0.08, 0.04, 0), (0.46, 0.4, 0.62), lean=(-0.05, 0.03), turn=0.4)
    boulder(bm, rnd, (-0.42, -0.24, 0), (0.24, 0.2, 0.28), lean=(0.04, 0.02), turn=1.3)
    return make("AshRockB", bm, MATS, at=at, smooth=35)


ASSETS = {
    "AshRockA": ash_rock_a,
    "AshRockB": ash_rock_b,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
