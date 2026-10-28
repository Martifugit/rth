"""Containers, village. Barrels, crates, sacks and the trough of the Wennow basin's farms and yards.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Containers/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\containers\\village.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
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
import artlib.nature as N  # noqa: E402


def trough(seed=1, at=(0, 0, 0)):
    """A stone water trough along X, cut from one block, water standing in it a hand below the
    rim, its foot sunk in the ground."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    L, W, H, t = 0.75, 0.32, 0.5, 0.07
    A.box(bm, (-L, -W, -0.05), (L, W, 0.12), 0)
    A.box(bm, (-L, -W, 0.12), (L, -W + t, H), 0)
    A.box(bm, (-L, W - t, 0.12), (L, W, H), 0)
    A.box(bm, (-L, -W + t, 0.12), (-L + t, W - t, H), 0)
    A.box(bm, (L - t, -W + t, 0.12), (L, W - t, H), 0)
    for v in bm.verts:
        if v.co.z > H - 0.01:
            v.co += Vector((rnd.uniform(-0.01, 0.01), rnd.uniform(-0.01, 0.01), rnd.uniform(-0.015, 0.0)))
    water = [bm.verts.new((x, y, H - 0.09)) for x, y in ((-L + t, -W + t), (L - t, -W + t), (L - t, W - t), (-L + t, W - t))]
    bm.faces.new(water).material_index = 1
    return N.make("Trough", bm, ["Gravestone", "Mere"], smooth_angle=20, at=at)


def barrel(seed=1, at=(0, 0, 0)):
    """A barrel standing on end: bellied staves, two iron hoops near each end, a plain head."""
    bm = bmesh.new()
    h, r0, r1 = 0.8, 0.25, 0.3

    def radius(z):
        return r0 + (r1 - r0) * math.sin(math.pi * z / h) ** 0.7
    zs = [0.0, 0.12, h / 2, h - 0.12, h]
    rings = [[bm.verts.new((radius(z) * math.cos(2 * math.pi * j / 12), radius(z) * math.sin(2 * math.pi * j / 12), z))
              for j in range(12)] for z in zs]
    for a, b in zip(rings, rings[1:]):
        for j in range(12):
            bm.faces.new((a[j], a[(j + 1) % 12], b[(j + 1) % 12], b[j])).material_index = 0
    bm.faces.new(rings[-1]).material_index = 2
    bm.faces.new(list(reversed(rings[0]))).material_index = 0
    for z in (0.07, 0.17, h - 0.17, h - 0.07):
        r = radius(z) + 0.012
        A.prism(bm, 12, r, r, z - 0.022, z + 0.022, mat_index=1, cap_top=False)
    return N.make("Barrel", bm, ["Oak", "Iron", "Planks"], smooth_angle=35, at=at)


def crate(seed=1, at=(0, 0, 0)):
    """A plank crate: a box of boards with a darker frame round its edges."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    s, e = 0.3, 0.035
    A.box(bm, (-s, -s, 0.0), (s, s, 2 * s), 0)
    for x in (-s, s):
        for y in (-s, s):
            A.box(bm, (x - e, y - e, 0.0), (x + e, y + e, 2 * s + e), 1)
    for z0, z1 in ((0.0, e), (2 * s - e, 2 * s + e)):
        for x in (-s, s):
            A.box(bm, (x - e, -s, z0), (x + e, s, z1), 1)
        for y in (-s, s):
            A.box(bm, (-s, y - e, z0), (s, y + e, z1), 1)
    return N.make("Crate", bm, ["Planks", "Timber"], at=at, rot=rnd.uniform(-0.2, 0.2))


def sacks(seed=1, at=(0, 0, 0)):
    """Sacks of grain: two slumped against each other and one lying across in front, each tied at
    the neck."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for (x, y), r, tilt in (((-0.15, 0.08), (0.24, 0.2, 0.36), 0.05), ((0.2, 0.12), (0.22, 0.2, 0.33), -0.04)):
        N.blob(bm, (x, y, r[2] * 0.95), r, 1, rnd, jitter=(0.9, 1.08), mat=0, squash_below=0.0)
        neck = Vector((x + tilt, y, r[2] * 1.85))
        A.loft(bm, [(neck - Vector((0, 0, 0.1)), 0.09, 0.08), (neck, 0.05, 0.05), (neck + Vector((0, 0, 0.07)), 0.07, 0.06),
                    (neck + Vector((0, 0, 0.1)), 0, 0)], sides=6, ref=(1, 0, 0), mats=0)
        A.loft(bm, [(neck - Vector((0, 0, 0.015)), 0.056, 0.056), (neck + Vector((0, 0, 0.015)), 0.056, 0.056)],
               sides=6, ref=(1, 0, 0), mats=1)
    N.blob(bm, (0.0, -0.32, 0.13), (0.36, 0.18, 0.15), 1, rnd, jitter=(0.9, 1.08), mat=0, squash_below=0.0)
    return N.make("Sacks", bm, ["Hide", "Umber"], smooth_angle=60, at=at)


ASSETS = {
    "Trough": trough,
    "Barrel": barrel,
    "Crate": crate,
    "Sacks": sacks,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
