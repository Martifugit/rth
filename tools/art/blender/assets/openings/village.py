"""Openings, village. The village's door leaf, hung in a doorway by the building kit.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Openings/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\openings\\village.py")
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
from artlib.village import DOOR, IRON, PLANKS, box, piece  # noqa: E402


def piece_door():
    """Planks behind heavy iron: three straps and studs (the iron worn openly)."""
    bm = bmesh.new()
    box(bm, 0.08, 0.92, -0.03, 0.03, 0.04, DOOR, PLANKS)
    for z in (0.35, 0.35 + (DOOR - 0.5) / 2, DOOR - 0.3):
        box(bm, 0.08, 0.84, -0.05, -0.03, z, z + 0.08, IRON)
        for x in (0.25, 0.47, 0.69):
            box(bm, x - 0.018, x + 0.018, -0.065, -0.05, z + 0.022, z + 0.058, IRON)
    box(bm, 0.78, 0.83, -0.07, -0.03, DOOR / 2, DOOR / 2 + 0.07, IRON)
    return bm


ASSETS = {
    "Door": piece(piece_door),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
