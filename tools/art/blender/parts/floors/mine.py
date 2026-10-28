"""Floors, mine. A mine's floor: the rails.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
mine.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Floors/Mine/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\floors\\mine.py")
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
from artlib.caves import block, make, rng  # noqa: E402


def mine_rails(seed=1, at=(0, 0, 0)):
    """One tile of rails running into the dark (+Y): three sleepers, two iron rails on them."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(3):
        y = -0.5 + (k + 0.5) / 3
        block(bm, (-0.42, y - 0.08, -0.04), (0.42, y + 0.08, 0.05), r, chip=0.015)
    for x in (-0.25, 0.25):
        A.box(bm, (x - 0.025, -0.52, 0.05), (x + 0.025, 0.52, 0.11), 1)
    return make("MineRails", bm, ["Timber", "Iron"], at=at, smooth=20)


ASSETS = {
    "MineRails": mine_rails,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
