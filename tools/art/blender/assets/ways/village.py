"""Ways, village. The basin's ways: fingerpost, milestone, footbridge, the stone road bridge, duckboards over the fen.

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


def road_bridge(seed=1, at=(0, 0, 0)):
    """Where Palmerway crosses the river: a stone bridge of one round-headed arch, cart-wide, the
    road running along Y over it and the river along X under it. 4 x 10 tiles. Rubble masonry with
    a dressed ring of voussoirs round the arch at each face, a low parapet with a coping either
    side, the deck humped over the arch and running down to the banks, cutwaters against the
    stream. Origin at the banks' level; the arch springs a little above the water."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    MASONRY, DRESSED, ROAD = range(3)
    W, L = 1.6, 5.0                    # half the road's width (a cart and a walker) and half the length
    span, spring = 1.9, -1.2           # the arch's half-span and where it springs, near the water
    crown = 1.25                       # the deck's rise over the middle
    assert spring + span + 0.38 < crown - 0.12, "the arch ring must stay under the road"
    para_h, para_t = 0.75, 0.28

    def deck(y):
        t = min(1.0, abs(y) / L)
        return crown * (1 - t * t)

    def soffit(y):                     # the underside: the arch, or the abutments buried in the banks
        if abs(y) < span:
            return spring + math.sqrt(span * span - y * y)
        return -1.2
    ys = [-L + k * (2 * L) / 20 for k in range(21)]
    ys = sorted(set(ys + [-span, span] + [span * math.sin(math.radians(a)) for a in range(-75, 90, 15)]))
    for y0, y1 in zip(ys, ys[1:]):
        bot = [soffit(y0 + 1e-4 if y0 == -span else y0), soffit(y1 - 1e-4 if y1 == span else y1)]
        top = [deck(y0), deck(y1)]
        A.hull(bm, [(x, y, z) for x in (-W - para_t, W + para_t) for y, z in ((y0, bot[0]), (y1, bot[1]), (y0, top[0] - 0.08),
                                                                              (y1, top[1] - 0.08))], MASONRY)
        A.hull(bm, [(x, y, z) for x in (-W, W) for y, z in ((y0, top[0] - 0.081), (y1, top[1] - 0.081), (y0, top[0]),
                                                            (y1, top[1]))], ROAD)
        for s in (-1, 1):                                                                       # the parapets and their coping
            xi, xo = s * W, s * (W + para_t)
            A.hull(bm, [(x, y, z) for x in (xi, xo) for y, z in ((y0, top[0] - 0.08), (y1, top[1] - 0.08),
                                                                 (y0, top[0] + para_h), (y1, top[1] + para_h))], MASONRY)
            A.hull(bm, [(x, y, z) for x in (xi - s * 0.04, xo + s * 0.04) for y, z in ((y0, top[0] + para_h), (y1, top[1] + para_h),
                                                                                         (y0, top[0] + para_h + 0.1),
                                                                                         (y1, top[1] + para_h + 0.1))], DRESSED)
    for s in (-1, 1):                                                                           # the arch rings, standing proud
        x = s * (W + para_t + 0.015)
        n = 10
        for k in range(n):
            a0, a1 = math.pi * k / n, math.pi * (k + 1) / n
            pts = []
            for a in (a0, a1):
                for r in (span, span + 0.38):
                    pts.append((x, -r * math.cos(a), spring + r * math.sin(a)))
            A.hull(bm, [p for p in pts] + [(x - s * 0.04, y, z) for _, y, z in pts], DRESSED)
    for s in (-1, 1):                                                                           # cutwaters on the piers, upstream (-X)
        y = s * (span + 0.45)
        A.hull(bm, [(-W - para_t, y + dy, z) for dy in (-0.45, 0.45) for z in (-1.2, spring + 0.5)] +
               [(-W - para_t - 0.6, y, z) for z in (-1.2, spring + 0.3)] + [(-W - para_t, y, spring + 0.9)], DRESSED)
    return N.make("RoadBridge", bm, ["Stone", "Granite", "Trodden"], smooth_angle=20, at=at)


def duckboards(seed=1, at=(0, 0, 0)):
    """Duckboards over the fen, one tile along Y (lay them end to end): two split rails on short stakes
    sunk in the mud, short boards nailed across them a little apart, uneven with age."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    deck = 0.18
    for x in (-0.28, 0.28):
        A.box(bm, (x - 0.05, -0.5, deck - 0.1), (x + 0.05, 0.5, deck - 0.03), 1)
        for y in (-0.35, 0.35):
            N.rod(bm, [(x, y, -0.3), (x + rnd.uniform(-0.02, 0.02), y, deck - 0.08)], 0.04, mat=2, sides=5)
    y = -0.5
    while y < 0.45:
        w = rnd.uniform(0.12, 0.16)
        dz = rnd.uniform(-0.012, 0.012)
        A.box(bm, (-0.42 + rnd.uniform(-0.03, 0.03), y + 0.01, deck - 0.03 + dz), (0.42 + rnd.uniform(-0.03, 0.03), y + w - 0.02, deck + dz), 0)
        y += w
    return N.make("Duckboards", bm, ["Boards", "DeadWood", "Driftwood"], smooth_angle=30, at=at)


ASSETS = {
    "Fingerpost": fingerpost,
    "Milestone": milestone,
    "Footbridge": footbridge,
    "RoadBridge": road_bridge,
    "Duckboards": duckboards,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
