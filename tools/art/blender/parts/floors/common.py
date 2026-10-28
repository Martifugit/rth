"""Floors, common. Floors of no style: steps and a plinth.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Floors/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\floors\\common.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.caves import STONE, block, make, rng, weather  # noqa: E402


def steps(seed=1, at=(0, 0, 0), wide=True):
    """Three steps rising towards a doorway (+Y), the top one running on as its threshold; 2.2
    wide, or 1.2."""
    r = rng(seed)
    bm = bmesh.new()
    w = 1.1 if wide else 0.6
    for k in range(3):
        y0 = -0.9 + 0.38 * k
        block(bm, (-w + 0.04 * k, y0, -0.1), (w - 0.04 * k, 0.6 if k == 2 else y0 + 0.4, 0.14 * (k + 1)), r, chip=0.015)
    weather(bm, r, 0.008)
    return make("Steps" if wide else "StepsNarrow", bm, [STONE], at=at, smooth=20)


def plinth(seed=2, at=(0, 0, 0)):
    """A squat plinth, for whatever a place sets before its door (a brazier, a statue, a bowl)."""
    r = rng(seed)
    bm = bmesh.new()
    block(bm, (-0.45, -0.45, -0.1), (0.45, 0.45, 0.25), r)
    block(bm, (-0.36, -0.36, 0.25), (0.36, 0.36, 0.75), r)
    block(bm, (-0.42, -0.42, 0.75), (0.42, 0.42, 0.88), r)
    weather(bm, r, 0.01, moss=1, up=0.9)
    return make("Plinth", bm, [STONE, "Moss"], at=at, smooth=20)


ASSETS = {
    "Steps": steps,
    "StepsNarrow": lambda at=(0, 0, 0): steps(at=at, wide=False),
    "Plinth": plinth,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
