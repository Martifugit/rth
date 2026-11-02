"""Vessels, village. The basin's boats: the punt, a clinker rowing boat afloat and turned over on the shore.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Vessels/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\vessels\\village.py")
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


def rowboat_hull(bm, mats):
    """A small clinker-built rowing boat along X, bow at +X, its origin at the waterline: a hull of
    planks (painted strakes, a darker band at each lap), a transom stern, three thwarts, oars shipped
    along the thwarts. Returns nothing; adds to bm."""
    HULL, INSIDE, STRAKE, OAR = mats
    # Sections: (x, half-beam at the gunwale, gunwale height, keel depth); the bow closes to a stem.
    secs = [(-1.5, 0.48, 0.42, -0.2), (-0.9, 0.62, 0.38, -0.28), (0.0, 0.66, 0.36, -0.3), (0.9, 0.55, 0.4, -0.27),
            (1.45, 0.25, 0.5, -0.18)]
    stem = Vector((1.75, 0.0, 0.62))

    def ring(x, w, top, keel, inset=0.0):
        return [Vector((x, -w + inset, top)), Vector((x, -w * 0.8 + inset, (top + keel) * 0.35)),
                Vector((x, 0.0, keel + inset)), Vector((x, w * 0.8 - inset, (top + keel) * 0.35)), Vector((x, w - inset, top))]
    outer = [[bm.verts.new(p) for p in ring(*s)] for s in secs]
    inner = [[bm.verts.new(p) for p in ring(*s, inset=0.035)] for s in secs]
    tip_o, tip_i = bm.verts.new(stem), bm.verts.new(stem - Vector((0.06, 0, 0.03)))
    for (o0, i0), (o1, i1) in zip(zip(outer, inner), zip(outer[1:], inner[1:])):
        for k in range(4):
            bm.faces.new((o0[k], o0[k + 1], o1[k + 1], o1[k])).material_index = HULL
            bm.faces.new((i0[k + 1], i0[k], i1[k], i1[k + 1])).material_index = INSIDE
        for k in (0, 4):
            bm.faces.new((o0[k], o1[k], i1[k], i0[k])).material_index = HULL       # the gunwale
    for k in range(4):
        bm.faces.new((outer[-1][k], outer[-1][k + 1], tip_o)).material_index = HULL
        bm.faces.new((inner[-1][k + 1], inner[-1][k], tip_i)).material_index = INSIDE
    for k in (0, 4):
        bm.faces.new((outer[-1][k], tip_o, tip_i, inner[-1][k])).material_index = HULL
    for k in range(4):                                                                 # the transom
        bm.faces.new((outer[0][k + 1], outer[0][k], inner[0][k], inner[0][k + 1])).material_index = HULL
    bm.faces.new(list(reversed(inner[0]))).material_index = HULL
    for z in (0.05, 0.2):                                                              # the laps of the strakes, painted
        for (xa, wa, ta, ka), (xb, wb, tb, kb) in zip(secs, secs[1:]):
            for s in (-1, 1):
                fa, fb = (z - ka) / (ta - ka), (z - kb) / (tb - kb)
                pa, pb = Vector((xa, s * (wa * fa + 0.004), z)), Vector((xb, s * (wb * fb + 0.004), z))
                A.hull(bm, [pa, pb, pa + Vector((0, 0, 0.02)), pb + Vector((0, 0, 0.02)), pa + Vector((0, s * 0.004, 0.01))], STRAKE)
    for x, w in ((-1.2, 0.5), (-0.1, 0.62), (0.8, 0.52)):                               # the thwarts
        A.box(bm, (x - 0.1, -w + 0.04, 0.22), (x + 0.1, w - 0.04, 0.26), INSIDE)
    for s in (-1, 1):                                                                  # the oars, shipped
        rod(bm, [(-1.35, s * 0.3, 0.29), (1.0, s * 0.22, 0.3)], 0.022, mat=OAR, sides=4)
        A.hull(bm, [(x, s * 0.3 + dy, 0.29 + dz) for x in (-1.6, -1.25) for dy in (-0.07, 0.07) for dz in (-0.008, 0.008)], OAR)


def rowboat(seed=1, at=(0, 0, 0)):
    """A rowing boat for the mere and the lake, afloat (origin at the water's surface)."""
    bm = bmesh.new()
    rowboat_hull(bm, (0, 1, 2, 3))
    return make("Rowboat", bm, ["Boards", "Planks", "Umber", "Driftwood"], smooth_angle=30, at=at)


def rowboat_upturned(seed=2, at=(0, 0, 0)):
    """The same boat drawn up on the fishers' shore and turned over against the rain, its gunwale
    on two logs, a gap under one side."""
    from mathutils import Matrix
    rnd = random.Random(seed)
    bm = bmesh.new()
    rowboat_hull(bm, (0, 1, 2, 3))
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(math.pi, 4, "X") @ Matrix.Rotation(math.radians(-6), 4, "Y"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((0, 0, 0.5)), verts=bm.verts)
    for x in (-0.9, 0.8):
        rod(bm, [(x, -0.8, 0.07), (x + rnd.uniform(-0.05, 0.05), 0.8, 0.07)], 0.08, mat=4, sides=6)
    return make("RowboatUpturned", bm, ["Boards", "Planks", "Umber", "Driftwood", "Bark"], smooth_angle=30, at=at)


ASSETS = {
    "Punt": punt,
    "Rowboat": rowboat,
    "RowboatUpturned": rowboat_upturned,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
