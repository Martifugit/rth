r"""Vessels, dead. The ferryman's boat at the Landing: long, narrow and double-ended, its prow rising in a
high curl like a shepherd's crook with a soul lantern hanging inside the curl over the bow, an oar laid
along the thwarts.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
dead.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Vessels/Dead/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\vessels\dead.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
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

MATS = ["DrownedWood", "Blackiron", "SoulGlow"]
WOOD, IRON, FLAME = range(3)

# the hull's sections along Y, stern (+Y) to bow (-Y, the front: the game's north at no rotation):
# (y, half-beam at the gunwale, gunwale height, keel depth), the waterline at z = 0
SECTIONS = [(1.3, 0.2, 0.5, -0.06), (1.0, 0.33, 0.4, -0.17), (0.4, 0.41, 0.34, -0.24), (-0.4, 0.41, 0.34, -0.24),
            (-1.0, 0.32, 0.41, -0.18), (-1.32, 0.15, 0.53, -0.08)]
STERN = Vector((0.0, 1.5, 0.64))                    # where the stern closes to its post
BOW = Vector((0.0, -1.5, 0.68))                      # where the bow closes to its stem, the curl's foot
INSET = 0.035                                       # the planking's thickness
# the prow's curl, from the stem up and back over the bow: (y, z) along the centre line
CURL = [(-1.5, 0.58), (-1.62, 0.92), (-1.62, 1.32), (-1.52, 1.6), (-1.36, 1.7), (-1.22, 1.64), (-1.17, 1.53)]
THWARTS = (0.75, -0.55)                             # the seats across, by y
CAGE = (0.085, 0.22)                                # the lantern's half width and height


def hull(bm):
    """The planked hull, open at the top: an outer and an inner skin of five-point rings joined at the
    gunwales, each end closing to a point."""
    def ring(y, w, top, keel, inset=0.0):
        return [Vector((-w + inset, y, top)), Vector((-w * 0.82 + inset, y, (top + keel) * 0.3)),
                Vector((0.0, y, keel + inset)), Vector((w * 0.82 - inset, y, (top + keel) * 0.3)), Vector((w - inset, y, top))]
    outer = [[bm.verts.new(p) for p in ring(*s)] for s in SECTIONS]
    inner = [[bm.verts.new(p) for p in ring(*s, inset=INSET)] for s in SECTIONS]
    for (o0, i0), (o1, i1) in zip(zip(outer, inner), zip(outer[1:], inner[1:])):
        for k in range(4):
            bm.faces.new((o0[k], o1[k], o1[k + 1], o0[k + 1])).material_index = WOOD
            bm.faces.new((i0[k + 1], i1[k + 1], i1[k], i0[k])).material_index = WOOD
        for k in (0, 4):
            bm.faces.new((o0[k], i0[k], i1[k], o1[k])).material_index = WOOD          # the gunwales
    for end, tip in ((0, STERN), (-1, BOW)):
        o, i = outer[end], inner[end]
        to = bm.verts.new(tip)
        ti = bm.verts.new(tip - Vector((0, 0.07 if end == 0 else -0.07, 0.04)))      # the inside's point, inboard
        for k in range(4):
            f = bm.faces.new((o[k], o[k + 1], to))
            g = bm.faces.new((i[k + 1], i[k], ti))
            f.material_index = g.material_index = WOOD
        for k in (0, 4):
            bm.faces.new((o[k], to, ti, i[k])).material_index = WOOD
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))


def lantern(bm, top):
    """A soul lantern hanging from a point: a short chain, a peaked iron cap, four bars, a floor plate
    and the pale flame standing on it."""
    hw, h = CAGE
    cap_top = top - Vector((0, 0, 0.1))
    rod(bm, [top + Vector((0, 0, 0.02)), cap_top], 0.01, mat=IRON, sides=4)
    z1 = cap_top.z - 0.07
    z0 = z1 - h
    cx, cy = top.x, top.y
    A.prism(bm, 4, hw + 0.035, 0.015, z1, cap_top.z + 0.01, center=(cx, cy), rot=math.pi / 4, mat_index=IRON,
            cap_bottom=True)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        x, y = cx + hw * math.cos(a) * 1.25, cy + hw * math.sin(a) * 1.25
        rod(bm, [(x, y, z0 + 0.01), (x, y, z1 + 0.01)], 0.011, mat=IRON, sides=4)
    A.prism(bm, 4, hw + 0.02, hw + 0.02, z0 - 0.025, z0 + 0.005, center=(cx, cy), rot=math.pi / 4, mat_index=IRON,
            cap_bottom=True)
    fl = []
    for k in range(6):
        a = 2 * math.pi * k / 6
        fl.append((cx + 0.05 * math.cos(a), cy + 0.05 * math.sin(a), z0 + 0.08))
        fl.append((cx + 0.03 * math.cos(a + 0.5), cy + 0.03 * math.sin(a + 0.5), z0 + 0.008))
    fl.append((cx, cy, z1 - 0.04))
    A.hull(bm, fl, mat=FLAME)


def ferry_boat(at=(0, 0, 0)):
    """The ferryman's boat, about a tile wide and three long along Y, sitting in the water with its
    waterline at the origin: a dark planked hull, double-ended, two thwarts, the stern post a little
    raised; the prow a high curl over the bow, banded in iron, the soul lantern hanging from its tip
    inside the curl; one long oar laid along the thwarts, its blade at the stern."""
    bm = bmesh.new()
    hull(bm)
    # the curl: one timber out of the stem, tapering as it climbs and turns back; the stern post short
    pts = [Vector((0.0, y, z)) for y, z in CURL]
    rod(bm, pts, 0.07, mat=WOOD, sides=6, taper=0.55)
    rod(bm, [STERN - Vector((0, 0.06, 0.12)), STERN + Vector((0, 0.08, 0.16))], 0.055, mat=WOOD, sides=6, taper=0.7)
    for i, r in ((1, 0.066), (3, 0.054)):                       # iron bands round the curl
        a, b = pts[i], pts[i + 1]
        d = (b - a).normalized()
        c = a + (b - a) * 0.35
        A.loft(bm, [(c - d * 0.025, r, r), (c + d * 0.025, r, r)], sides=6, ref=(1, 0, 0), mats=IRON)
    lantern(bm, pts[-1] - Vector((0, 0, 0.03)))
    for y in THWARTS:                                           # the seats, resting on the inner skin
        w = next(s[1] for s in SECTIONS if abs(s[0] - y) < 0.4) - 0.03
        A.box(bm, (-w, y - 0.1, 0.2), (w, y + 0.1, 0.25), WOOD)
    # the oar, laid along the thwarts: loom up the boat, the blade flat at the stern
    lo, hi = Vector((0.11, -1.0, 0.27)), Vector((0.14, 0.85, 0.285))
    rod(bm, [lo, hi], 0.022, mat=WOOD, sides=5)
    A.hull(bm, [(x, y, z) for x in (0.05, 0.22) for y in (0.78, 1.2) for z in (0.27, 0.3)], mat=WOOD)
    return make("FerryBoat", bm, MATS, smooth_angle=30, at=at)


ASSETS = {
    "FerryBoat": ferry_boat,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
