"""Rocks, common. Rocks: boulders, scree, a cairn, standing stones.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Rocks/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\rocks\\common.py")
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
from artlib.nature import blob, flat_chip, make, slab  # noqa: E402


def boulders(seed=6, at=(0, 0, 0), size=1.0):
    """A rock cluster: one big rounded stone, two small ones leaning on it, a scatter of flat
    chips. One colour; smooth light does the rest."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    blob(bm, (0, 0, 0.32), (0.75, 0.6, 0.55), 1, rnd, jitter=(0.85, 1.12), squash_below=-0.05)
    blob(bm, (0.7, -0.35, 0.15), (0.38, 0.32, 0.3), 0, rnd, jitter=(0.85, 1.15), squash_below=-0.05)
    blob(bm, (-0.55, 0.45, 0.12), (0.3, 0.28, 0.22), 0, rnd, jitter=(0.85, 1.15), squash_below=-0.05)
    for _ in range(9):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.9, 1.6)
        c = Vector((d * math.cos(a), d * math.sin(a), 0))
        s = rnd.uniform(0.05, 0.1)
        A.hull(bm, [c + Vector((s * math.cos(t), s * math.sin(t), -0.01)) for t in
                    (rnd.uniform(0, 1), rnd.uniform(2, 3), rnd.uniform(4, 5))] + [c + Vector((0, 0, s * 0.6))])
    for v in bm.verts:
        v.co *= size
    return make("Boulders", bm, ["Scree"], smooth_angle=40, at=at)


def cairn(seed=4, at=(0, 0, 0)):
    """A cairn on the downs: flat stones piled in courses, each a little askew, and one slab set on
    end at the top as a marker."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    z, r = 0.0, 0.55
    for i in range(6):
        h = rnd.uniform(0.12, 0.17)
        slab(bm, (rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04), z), r, h, 6, rnd, rot=rnd.uniform(0, 3))
        z += h - 0.01
        r *= 0.86
    A.box(bm, (-0.12, -0.04, z - 0.05), (0.12, 0.04, z + 0.45), 1)
    for v in list(bm.verts)[-8:]:
        v.co.x += (v.co.z - z) * 0.08
    return make("Cairn", bm, ["Scree", "Gravestone"], smooth_angle=30, at=at)


def monolith(bm, length, half, twist, rnd, mat=0):
    """A four-sided stone column, each course turned a little further than the one below, so it
    twists as it rises: a shape no mason in R'th makes. Returns nothing; flat top."""
    rings = []
    n = 6
    for i in range(n + 1):
        z = -0.1 + length * i / n
        taper = 1 - 0.25 * i / n
        a = twist * i / n
        ring = []
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            x, y = sx * half[0] * taper, sy * half[1] * taper
            ring.append(bm.verts.new((x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a), z)))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k])).material_index = mat
    bm.faces.new(rings[-1]).material_index = mat
    bm.faces.new(list(reversed(rings[0]))).material_index = mat


def standing_stone(seed=1, at=(0, 0, 0)):
    """A standing stone on the heath in a style nobody made: a tall pale column twisted through a
    quarter turn as it rises, too regular for weather, leaning a little."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    monolith(bm, 2.5, (0.34, 0.22), math.radians(80), rnd)
    from mathutils import Matrix as M
    bmesh.ops.transform(bm, matrix=M.Rotation(math.radians(5), 4, Vector((1, 0.3, 0)).normalized()), verts=bm.verts)
    return make("StandingStone", bm, ["Gravestone"], smooth_angle=20, at=at)


def standing_stone_fallen(seed=2, at=(0, 0, 0)):
    """One of the twisted stones fallen and half sunk into the turf."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    monolith(bm, 2.2, (0.32, 0.2), math.radians(80), rnd)
    from mathutils import Matrix as M
    bmesh.ops.transform(bm, matrix=M.Rotation(math.radians(84), 4, Vector((0, 1, 0))), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((-1.0, 0, 0.04)), verts=bm.verts)
    return make("StandingStoneFallen", bm, ["Gravestone"], smooth_angle=20, at=at)


def scree(seed=1, at=(0, 0, 0)):
    """A heap of broken stone at the foot of a slope or a crag: a low mound of chips, the big ones
    at the bottom, a few fallen clear of it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    N.blob(bm, (0, 0, 0.0), (0.7, 0.55, 0.26), 1, rnd, jitter=(0.9, 1.08), mat=2, squash_below=-0.02)
    for k in range(22):
        a, dist = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 1) ** 0.7
        x, y = 0.75 * dist * math.cos(a), 0.6 * dist * math.sin(a)
        z = 0.26 * max(0.0, 1 - dist * dist) - 0.03
        s = 0.06 + 0.09 * dist * rnd.uniform(0.6, 1.0)
        c = Vector((x, y, z))
        pts = [c + Vector((rnd.uniform(-s, s), rnd.uniform(-s, s), rnd.uniform(-0.3, 0.3) * s)) for _ in range(5)]
        pts.append(c + Vector((0, 0, s * 0.45)))
        for f in A.hull(bm, pts):
            f.material_index = 0 if k % 4 else 1
    for k in range(4):
        a = rnd.uniform(0, 2 * math.pi)
        flat_chip(bm, (0.95 * math.cos(a), 0.8 * math.sin(a), 0), rnd.uniform(0.06, 0.1), rnd, 0)
    return N.make("Scree", bm, ["Scree", "Cinder", "Gravel"], smooth_angle=20, at=at)


ASSETS = {
    "Boulders": boulders,
    "Cairn": cairn,
    "StandingStone": standing_stone,
    "StandingStoneFallen": standing_stone_fallen,
    "Scree": scree,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
