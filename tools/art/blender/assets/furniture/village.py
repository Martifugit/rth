"""Furniture, village. The village's furniture: the tithe font by a sworn door.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Furniture/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\furniture\\village.py")
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
from artlib.village import BLOOD, STONE, piece  # noqa: E402


def piece_font():
    """The tithe font by a sworn door: a stone basin on a stem, dark with blood."""
    bm = bmesh.new()
    A.loft(bm, [((0, 0, 0), 0.16, 0.16), ((0, 0, 0.1), 0.16, 0.16), ((0, 0, 0.12), 0.07, 0.07),
                ((0, 0, 0.77), 0.06, 0.06), ((0, 0, 0.82), 0.17, 0.17), ((0, 0, 0.95), 0.19, 0.19)],
           sides=8, ref=(0, 1, 0), mats=STONE)
    A.loft(bm, [((0, 0, 0.954), 0.15, 0.15), ((0, 0, 0.956), 0, 0)], sides=8, ref=(0, 1, 0), mats=BLOOD)
    return bm


ASSETS = {
    "Font": piece(piece_font),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
