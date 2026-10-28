"""Dressing, mine. A mine's dressing: the cart and the spoil heap.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
mine.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Dressing/Mine/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\dressing\\mine.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
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
from artlib.caves import added, chunk, lay, make, rng  # noqa: E402


def mine_cart(seed=1, at=(0, 0, 0)):
    """A mine tub on the rails: a tapered iron-banded box on four small wheels, ore heaped in it."""
    r = rng(seed)
    bm = bmesh.new()
    A.hull(bm, [(x * w, y * d, z) for z, w, d in ((0.22, 0.32, 0.45), (0.75, 0.42, 0.58)) for x in (-1, 1) for y in (-1, 1)], mat=0)
    for z in (0.3, 0.68):
        A.hull(bm, [(x * (0.34 + 0.12 * (z - 0.22) / 0.53 + 0.015), y * (0.47 + 0.13 * (z - 0.22) / 0.53 + 0.015), z + dz)
                    for x in (-1, 1) for y in (-1, 1) for dz in (-0.03, 0.03)], mat=1)
    chunk(bm, (0, 0, 0.62), (0.36, 0.5, 0.3), r, sink=0.0, mats=(2, 2, 2), points=14)
    for x in (-0.25, 0.25):
        for y in (-0.32, 0.32):
            before = set(bm.verts)
            A.prism(bm, 8, 0.11, 0.11, -0.03, 0.03, (0, 0), mat_index=1, cap_top=True, cap_bottom=True)
            lay(bm, added(bm, before), Matrix.Translation((x * 1.25, y, 0.12)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    return make("MineCart", bm, ["Timber", "Iron", "Cinder"], at=at, smooth=30)


def spoil_heap(seed=3, at=(0, 0, 0)):
    """The spoil dug out of a mine and tipped by its mouth: a low dark heap of broken stone."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.9, 0.7, 0.5), r, mats=(0, 1, 0), points=22)
    for _ in range(10):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0.8, 1.2)
        s = r.uniform(0.06, 0.12)
        chunk(bm, (d * math.cos(a) * 1.1, d * math.sin(a) * 0.9, 0), (s, s, s * 0.8), r, sink=0.03, mats=(0, 1, 0), points=8)
    return make("SpoilHeap", bm, ["Cinder", "CryptDarkStone"], at=at)


ASSETS = {
    "MineCart": mine_cart,
    "SpoilHeap": spoil_heap,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
