"""Ways, village. The basin's ways: fingerpost, milestone, footbridge.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Ways/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\ways\\village.py")
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


def board(bm, a, b, w, t, rnd, mat=0, point=False):
    """A sawn board from a to b, `w` half its height, `t` half its thickness; point: the end at b
    cut to a point."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    side = d.cross(Vector((0, 0, 1))).normalized()
    up = Vector((0, 0, 1))
    pts = []
    for p, tip in ((a, False), (b, point)):
        base = p - d * w if tip else p
        for ss in (-t, t):
            for uu in (-w, w):
                pts.append(base + side * ss + up * uu)
    if point:
        pts += [b + side * ss for ss in (-t, t)]
    for f in A.hull(bm, pts):
        f.material_index = mat


def fingerpost(seed=1, at=(0, 0, 0), arms=(30, 160, 260)):
    """A fingerpost where ways meet: a squared post with a little gabled cap, an arm pointing down
    each way (headings in degrees), the names painted on as dark strokes."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    A.box(bm, (-0.07, -0.07, -0.1), (0.07, 0.07, 2.0), 0)
    A.hull(bm, [Vector((x, y, 2.0)) for x in (-0.1, 0.1) for y in (-0.1, 0.1)] +
           [Vector((x, 0, 2.12)) for x in (-0.1, 0.1)], 1)
    for k, deg in enumerate(arms):
        z = 1.78 - 0.2 * k
        a = math.radians(deg)
        d = Vector((math.cos(a), math.sin(a), 0))
        board(bm, d * 0.05 + Vector((0, 0, z)), d * 0.72 + Vector((0, 0, z)), 0.07, 0.02, rnd, 2, point=True)
        side = d.cross(Vector((0, 0, 1))).normalized()
        for sgn in (-1, 1):                                    # the name, on both faces
            for j in range(3):
                c = d * (0.17 + 0.14 * j) + side * sgn * 0.022 + Vector((0, 0, z))
                v = [bm.verts.new(c + d * dx + Vector((0, 0, dz))) for dx, dz in ((0, -0.025), (0.09, -0.025), (0.09, 0.025), (0, 0.025))]
                bm.faces.new(v if sgn > 0 else list(reversed(v))).material_index = 3
    return N.make("Fingerpost", bm, ["Timber", "DeadWood", "Planks", "Charcoal"], at=at)


def milestone(seed=1, at=(0, 0, 0)):
    """A milestone by the road: a dressed stone with a rounded head, leaning a little, the distance
    cut into a sunk panel on its face, lichen on its head."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    prof = [(-0.15, 0.2), (0.45, 0.2), (0.55, 0.17), (0.6, 0.12), (0.62, 0.06)]
    rings = [[bm.verts.new((x * w, y * 0.12, z)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))] for z, w in prof]
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k])).material_index = 0
    bm.faces.new(rings[-1]).material_index = 2
    bm.faces.new(list(reversed(rings[0]))).material_index = 0
    for f in bm.faces:
        if f.calc_center_median().z > 0.5:
            f.material_index = 2                               # lichen on the head
    lean = lambda p: p + Vector((p.z * 0.05, p.z * 0.03, 0))
    for v in bm.verts:
        v.co = lean(v.co)
    panel = [Vector((x, -0.122, z)) for x, z in ((-0.12, 0.12), (0.12, 0.12), (0.12, 0.38), (-0.12, 0.38))]
    bm.faces.new([bm.verts.new(lean(p)) for p in panel]).material_index = 1
    for j in range(2):                                         # the figures cut in
        z = 0.18 + 0.1 * j
        v = [bm.verts.new(lean(Vector((x, -0.124, zz)))) for x, zz in ((-0.07, z), (0.07, z), (0.07, z + 0.035), (-0.07, z + 0.035))]
        bm.faces.new(v).material_index = 3
    return N.make("Milestone", bm, ["Gravestone", "Granite", "MossyStone", "Charcoal"], smooth_angle=30, at=at)


def footbridge(seed=1, at=(0, 0, 0)):
    """A plank footbridge over a stream along Y, three tiles: two log stringers bedded on stone pads
    on each bank, rising a little to the middle, planks laid across them, a rail of poles on one
    side. Walkers cross along Y; the stream runs along X under it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for y in (-1.45, 1.45):
        N.slab(bm, (0, y, -0.2), 0.55, 0.3, 6, rnd, rot=0.4, side_mat=3, top_mat=3)

    def deck(y):
        return 0.16 + 0.1 * (1 - (y / 1.5) ** 2)
    for x in (-0.32, 0.32):
        A.loft(bm, [(Vector((x, y, deck(y) - 0.12)), 0.1, 0.1) for y in (-1.5, -0.75, 0.0, 0.75, 1.5)], sides=6,
               ref=(0, 0, 1), mats=0)
    y = -1.45
    while y < 1.4:
        w = rnd.uniform(0.17, 0.22)
        z = deck(y + w / 2)
        A.box(bm, (-0.45 + rnd.uniform(-0.03, 0.03), y, z - 0.03), (0.45 + rnd.uniform(-0.03, 0.03), y + w - 0.015, z + 0.03), 1)
        y += w
    for y in (-1.3, 0.0, 1.3):
        N.rod(bm, [(0.42, y, deck(y) - 0.15), (0.42, y, deck(y) + 0.95)], 0.045, mat=2, sides=5)
    N.rod(bm, [(0.42, y, deck(y) + 0.9) for y in (-1.4, -0.7, 0.0, 0.7, 1.4)], 0.035, mat=2, sides=5)
    for y0 in (-1.3, 1.3):                                     # the rail's braces
        N.rod(bm, [(0.42, y0, deck(y0) + 0.05), (0.42, y0 * 0.6, deck(y0) + 0.75)], 0.025, mat=2, sides=4)
    return N.make("Footbridge", bm, ["DeadWood", "Planks", "Driftwood", "Scree"], smooth_angle=40, at=at)


ASSETS = {
    "Fingerpost": fingerpost,
    "Milestone": milestone,
    "Footbridge": footbridge,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
