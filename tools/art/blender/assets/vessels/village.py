"""Vessels, village. The basin's boats: the punt.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Vessels/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\vessels\\village.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import os
import random
import sys

from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.nature import make, rod  # noqa: E402


def punt(seed=1, at=(0, 0, 0), rot=0.0):
    """A flat-bottomed punt for the mere, its origin at the water's surface: square ends rising a
    little, plank walls with an inside, a seat across, and its pole lying along it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    sections = [(-2.0, 0.36, 0.10), (-1.6, 0.5, -0.06), (1.6, 0.5, -0.06), (2.0, 0.36, 0.10)]     # long enough to lie in
    wall, t = 0.24, 0.04
    rings = []
    for x, w, z in sections:
        outer = [Vector((x, -w, z + wall)), Vector((x, -w, z)), Vector((x, w, z)), Vector((x, w, z + wall))]
        inner = [Vector((x, -w + t, z + wall)), Vector((x, -w + t, z + t)), Vector((x, w - t, z + t)), Vector((x, w - t, z + wall))]
        rings.append(([bm.verts.new(p) for p in outer], [bm.verts.new(p) for p in inner]))
    for (o0, i0), (o1, i1) in zip(rings, rings[1:]):
        for k in range(3):
            bm.faces.new((o0[k], o1[k], o1[k + 1], o0[k + 1]))
            bm.faces.new((i0[k], i0[k + 1], i1[k + 1], i1[k]))
        bm.faces.new((o0[0], i0[0], i1[0], o1[0]))           # the gunwales
        bm.faces.new((o0[3], o1[3], i1[3], i0[3]))
    for o, i in (rings[0], rings[-1]):                       # the square ends
        for k in range(3):
            bm.faces.new((o[k], o[k + 1], i[k + 1], i[k]))
    A.box(bm, (0.15, -0.46, 0.08), (0.42, 0.46, 0.13), 0)   # the seat
    rod(bm, [(-1.9, 0.14, 0.2), (0.4, 0.05, 0.24), (2.6, -0.1, 0.3)], 0.022, sides=4)
    return make("Punt", bm, ["Driftwood"], smooth_angle=30, at=at, rot=rot)


ASSETS = {
    "Punt": punt,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
