"""The outdoors beyond trees and fences, for the nature kit: what fills the ground between the big
pieces and tells a player what kind of land this is and who uses it.

- Ground cover: bracken, bramble, meadow flowers, heather, thistles.
- Deadwood: a fallen trunk with bracket fungus, a woodpile, bundles of brushwood, a chopping
  block with its hatchet in it.
- Loose stone: a scree heap, stepping stones across shallow water.
- Ways and work: a fingerpost, a milestone, a plank footbridge, a campfire (laid and burnt out),
  a lean-to, a water trough, bee skeps on their bench, a scarecrow, eel traps; a barrel, a crate
  and sacks for anywhere people work.
- Trees: a birch (and its stump), a staked sapling, an old rotten stump.

The same rules as the rest of the kit (README "Nature"): forms are R'th's own, a worked land under a
wind off the spine; leaves are textured cushions and cards with a ragged hem; timber is crooked and
flat coloured; everything stands on or is sunk into the ground. Each asset is a builder at the
origin (the middle of its tile), placed on the board by basin.py's rows.
"""
import math
import random

import bmesh
from mathutils import Matrix, Vector

import rth_art as A
import basin as N

WIND = N.WIND


def card(bm, uvs, path, widths, side, mat, u0=0.0, u1=1.0):
    """A leaf card bent along `path` (points from its foot to its tip), `widths` across it along
    `side`, wearing the texture from v 0 at the foot to 1 at the tip, columns u0..u1."""
    rows = []
    for p, w in zip(path, widths):
        p = Vector(p)
        rows.append((bm.verts.new(p - side * w), bm.verts.new(p + side * w)))
    n = len(rows) - 1
    for i in range(n):
        (a0, a1), (b0, b1) = rows[i], rows[i + 1]
        f = bm.faces.new((a0, a1, b1, b0))
        f.material_index = mat
        uvs[f] = [(u0, i / n), (u1, i / n), (u1, (i + 1) / n), (u0, (i + 1) / n)]


def flat_chip(bm, c, s, rnd, mat, h=0.6):
    c = Vector(c)
    pts = [c + Vector((s * math.cos(a), s * math.sin(a) * rnd.uniform(0.6, 1.0), -0.02))
           for a in (rnd.uniform(0, 1.2), rnd.uniform(2.0, 3.2), rnd.uniform(4.0, 5.4))]
    pts.append(c + Vector((0, 0, s * h)))
    for f in A.hull(bm, pts):
        f.material_index = mat


# ---- ground cover ------------------------------------------------------------------------------

def bracken(seed=1, at=(0, 0, 0)):
    """A stand of bracken: a dozen fronds rising from one root and arching outwards and over, each a
    bent card of the bracken texture, the tallest at waist height, tips turned tawny."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    for k in range(12):
        a = 2 * math.pi * k / 12 + rnd.uniform(-0.25, 0.25)
        out = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-out.y, out.x, 0))
        h = rnd.uniform(0.55, 0.95)
        reach = rnd.uniform(0.45, 0.75)
        base = out * rnd.uniform(0.02, 0.12)
        path = [base + Vector((0, 0, -0.03)), base + out * reach * 0.2 + Vector((0, 0, h * 0.55)),
                base + out * reach * 0.6 + Vector((0, 0, h * 0.95)), base + out * reach + Vector((0, 0, h * 0.75))]
        u0 = rnd.uniform(0, 0.6)
        card(bm, uvs, path, (0.1, 0.2, 0.22, 0.12), side, 0, u0, u0 + 0.4)
    return N.leafy("Bracken", bm, uvs, [N.leaf_mat("Bracken")], at)


def bramble(seed=1, at=(0, 0, 0)):
    """A bramble patch: a low tangled mound of dark leaves, pale flowers and berries, in three
    heaped cushions. (Canes arching out of it read as legs at game distance, so the leaves carry it.)"""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    for (x, y), r in (((0.0, 0.0), (0.55, 0.5, 0.5)), ((0.4, 0.25), (0.4, 0.36, 0.38)), ((-0.35, -0.2), (0.38, 0.34, 0.32))):
        N.clump(bm, N.grounded(x, y, r), r, rnd, 0, 1, uvs, sides=7)
    return N.leafy("Bramble", bm, uvs, [N.leaf_mat("Bramble"), "Leaf1"], at)


def wildflowers(seed=1, at=(0, 0, 0)):
    """Meadow flowers in a loose drift: thin stems, flat pale heads of yarrow and small yellow
    cups held above a few leaves; no bigger than a hand at game distance, so few and clear."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(4):                                       # leaves at the foot
        a = rnd.uniform(0, 2 * math.pi)
        d = Vector((math.cos(a), math.sin(a), 0))
        s = Vector((-d.y, d.x, 0))
        c = Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 0))
        v = [bm.verts.new(c + p) for p in (Vector((0, 0, 0.02)), d * 0.1 + s * 0.05 + Vector((0, 0, 0.06)),
                                           d * 0.22 + Vector((0, 0, 0.03)), d * 0.1 - s * 0.05 + Vector((0, 0, 0.06)))]
        bm.faces.new(v).material_index = 0
    for k in range(13):
        c = Vector((rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), 0))
        h = rnd.uniform(0.3, 0.55)
        top = c + Vector((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), h))
        N.branch(bm, c - Vector((0, 0, 0.02)), top, 0.01, 0.008, mat=0, sides=3)
        if k % 3:                                            # yarrow: a flat pale head
            r = rnd.uniform(0.05, 0.07)
            A.prism(bm, 6, r, r * 0.8, top.z - 0.01, top.z + 0.02, (top.x, top.y), mat_index=1)
        else:                                                # a buttercup
            A.loft(bm, [(top - Vector((0, 0, 0.01)), 0, 0), (top + Vector((0, 0, 0.03)), 0.04, 0.04),
                        (top + Vector((0, 0, 0.035)), 0, 0)], sides=5, ref=(1, 0, 0), mats=2)
    return N.make("Wildflowers", bm, ["Leaf4", "Yarrow", "Buttercup"], smooth_angle=50, at=at)


def heather(seed=1, at=(0, 0, 0)):
    """Heather on the moor: low springy cushions, dark and woody underneath, their tops a muted
    purple in flower, one cushion still green."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k, ((x, y), r) in enumerate((((0, 0), (0.42, 0.38, 0.3)), ((0.48, 0.18), (0.32, 0.3, 0.24)),
                                     ((-0.4, 0.22), (0.3, 0.26, 0.22)), ((0.1, -0.42), (0.3, 0.28, 0.2)))):
        faces = N.blob(bm, (x, y, r[2] * 0.25), r, 1, rnd, jitter=(0.82, 1.15), squash_below=-0.02)
        for f in faces:
            f.normal_update()
            f.material_index = (2 if k == 3 else 1) if f.normal.z > 0.35 else 0
    return N.make("Heather", bm, ["DeepWood", "Heather", "Sedge"], smooth_angle=60, at=at)


def thistles(seed=1, at=(0, 0, 0)):
    """Thistles in rough pasture: stiff stalks with spiny leaves sticking out, each crowned with a
    green bulb and a purple tuft."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(4):
        c = Vector((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), 0))
        h = rnd.uniform(0.6, 0.95)
        top = c + Vector((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), h))
        N.rod(bm, [c - Vector((0, 0, 0.02)), c.lerp(top, 0.5), top], 0.02, mat=0, sides=4, taper=0.7)
        for j in range(4):
            z = h * (0.2 + 0.18 * j)
            a = rnd.uniform(0, 2 * math.pi)
            base = c.lerp(top, z / h)
            tip = base + Vector((0.2 * math.cos(a), 0.2 * math.sin(a), 0.06)) * (1.2 - 0.2 * j)
            A.spike(bm, base, 0.035, tip, sides=3, mat=0)
        A.loft(bm, [(top - Vector((0, 0, 0.02)), 0, 0), (top + Vector((0, 0, 0.04)), 0.045, 0.045),
                    (top + Vector((0, 0, 0.09)), 0.035, 0.035)], sides=6, ref=(1, 0, 0), mats=0)
        A.loft(bm, [(top + Vector((0, 0, 0.085)), 0.03, 0.03), (top + Vector((0, 0, 0.16)), 0.05, 0.05),
                    (top + Vector((0, 0, 0.17)), 0, 0)], sides=6, ref=(1, 0, 0), mats=1)
    return N.make("Thistles", bm, ["Leaf3", "Thistle"], smooth_angle=40, at=at)


# ---- deadwood ----------------------------------------------------------------------------------

def fallen_log(seed=1, at=(0, 0, 0), rot=0.0):
    """A trunk the wind brought down long ago, lying along X: bedded into the ground, its top grown
    over with moss, one end snapped into splinters, the other a stub of broken roots; brackets of
    fungus along its side."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    r = 0.22
    pts = N.wobble(rnd, (-1.25, 0, r * 0.75), (1.25, 0, r * 0.65), 5, 0.04)
    rings, faces = A.loft(bm, [(p, r * (1.06 - 0.12 * i / 4), r * (1.0 - 0.1 * i / 4)) for i, p in enumerate(pts)],
                          sides=8, ref=(0, 0, 1), mats=0)
    for f in faces:
        if len(f.verts) == 8:
            f.material_index = 1                               # the broken ends' wood
            continue
        c = f.calc_center_median()
        axis_z = min(pts, key=lambda p: abs(p.x - c.x)).z
        if c.z - axis_z > r * 0.55:
            f.material_index = 2                               # moss along the top
    end = pts[-1]
    for k in range(6):                                         # the snapped end's splinters
        a = 2 * math.pi * k / 6 + rnd.uniform(-0.2, 0.2)
        b = end + Vector((-0.02, math.cos(a) * r * 0.55, math.sin(a) * r * 0.55))
        A.spike(bm, b, 0.06, b + Vector((rnd.uniform(0.12, 0.3), math.cos(a) * 0.03, math.sin(a) * 0.03)), sides=3, mat=1)
    root = pts[0]
    for k in range(5):                                         # the broken roots
        a = 2 * math.pi * k / 5 + rnd.uniform(-0.3, 0.3)
        d = Vector((-0.6, math.cos(a), math.sin(a))).normalized()
        if d.z < -0.3:
            continue
        N.branch(bm, root + Vector((0.08, 0, 0)), root + d * rnd.uniform(0.35, 0.55), 0.08, None, mat=0, sides=4)
    for k in range(3):                                         # bracket fungus on the shady side
        x = rnd.uniform(-0.7, 0.8)
        c = Vector((x, -r * 0.95, r * rnd.uniform(0.6, 0.95)))
        s = rnd.uniform(0.07, 0.11)
        A.hull(bm, [c + Vector((s * math.cos(t), -s * 0.8 * abs(math.sin(t)), dz))
                    for t in (0.0, 0.8, 1.6, 2.4, 3.14) for dz in (0.0, 0.025)], 3)
    return N.make("FallenLog", bm, ["Bark", "DeadWood", "Moss", "Ochre"], smooth_angle=45, at=at, rot=rot)


def log(bm, a, b, r, rnd, sides=6):
    """A cut log from a to b: bark round it, pale sawn ends."""
    rings, faces = A.loft(bm, [(a, r, r), (b, r * 0.96, r * 0.96)], sides=sides, ref=(0, 0, 1), mats=0,
                          roll=rnd.uniform(0, 1))
    for f in faces:
        if len(f.verts) == sides:
            f.material_index = 1


def woodpile(seed=1, at=(0, 0, 0)):
    """Firewood stacked against the weather along X: rows of cut logs, ends out, held between a
    pair of stakes at each end, a few split pieces thrown on top."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    L = 0.55
    for row, (n, z) in enumerate(((5, 0.1), (4, 0.28), (3, 0.46))):
        for k in range(n):
            x = (k - (n - 1) / 2) * 0.2 + rnd.uniform(-0.015, 0.015)
            r = rnd.uniform(0.085, 0.1)
            y0 = -L / 2 + rnd.uniform(-0.04, 0.04)
            log(bm, Vector((x, y0, z)), Vector((x, y0 + L, z)), r, rnd)
    for x in (-0.62, 0.62):
        for y in (-0.15, 0.15):
            N.rod(bm, [(x, y, -0.05), (x + rnd.uniform(-0.02, 0.02), y, 0.62)], 0.03, mat=0, sides=4, tip=(x, y, 0.68))
    for k in range(2):                                         # split pieces on top: half logs
        x = rnd.uniform(-0.25, 0.25)
        A.hull(bm, [Vector((x + dx, y, 0.56 + dz)) for y in (-0.25, 0.25) for dx, dz in ((-0.08, 0.0), (0.08, 0.0), (0.0, 0.08))], 1)
    return N.make("Woodpile", bm, ["Bark", "Ash"], smooth_angle=40, at=at)


def brushwood(seed=1, at=(0, 0, 0)):
    """Brushwood cut from the coppice and bound into bundles for the fire and the oven: three
    faggots of rods, two lying, one leaning on them, each tied twice."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k, (c, d, lean) in enumerate((((0, -0.15, 0.12), (1, 0.1, 0), 0), ((0.05, 0.18, 0.12), (1, -0.05, 0), 0),
                                      ((-0.1, 0.0, 0.35), (1, 0.2, 0.35), 1))):
        c, d = Vector(c), Vector(d).normalized()
        side = d.cross(Vector((0, 0, 1))).normalized()
        up = side.cross(d)
        for j in range(9):
            a = 2 * math.pi * j / 9
            off = side * math.cos(a) * 0.08 + up * math.sin(a) * 0.08
            l = rnd.uniform(0.6, 0.72)
            p0 = c - d * l + off * rnd.uniform(0.5, 1.0)
            p1 = c + d * l + off * rnd.uniform(0.9, 1.5)
            N.rod(bm, [p0, p1], 0.018, mat=0, sides=3)
        for t in (-0.3, 0.3):
            A.loft(bm, [(c + d * (t - 0.025), 0.1, 0.1), (c + d * (t + 0.025), 0.1, 0.1)], sides=7,
                   ref=tuple(up), mats=1)
    return N.make("Brushwood", bm, ["DeadWood", "Umber"], smooth_angle=40, at=at)


def chopping_block(seed=1, at=(0, 0, 0)):
    """Where wood is split: a round of trunk standing on end, its top scarred pale, a hatchet left
    in it, a split log leaning on it and pale chips round its foot."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    N.cut_stub(bm, (0, 0, -0.03), 0.27, 0.48, 0.0, rnd, sides=8)
    hit = Vector((0.05, 0.02, 0.45))
    d = Vector((0.55, -0.2, 0.8)).normalized()
    N.rod(bm, [hit + d * 0.04, hit + d * 0.55], 0.025, mat=2, sides=5)
    side = d.cross(Vector((0, 0, 1))).normalized()
    A.hull(bm, [hit + d * dd + side * ss + Vector((0, 0, zz)) for dd in (0.0, 0.1) for ss in (-0.025, 0.025)
                for zz in (-0.08, 0.06)] + [hit + d * 0.02 + Vector((0, 0, -0.14))], 3)
    A.hull(bm, [Vector((0.4 + x, -0.2 + y, z)) for x, y, z in ((-0.06, 0, -0.02), (0.06, 0, -0.02), (0, 0.05, -0.02),
                                                               (-0.1, 0.1, 0.5), (0.02, 0.1, 0.5), (-0.04, 0.16, 0.5))], 1)
    for k in range(8):
        a, dist = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.32, 0.6)
        flat_chip(bm, (dist * math.cos(a), dist * math.sin(a), 0), rnd.uniform(0.03, 0.05), rnd, 1, h=0.25)
    return N.make("ChoppingBlock", bm, ["Bark", "Ash", "Timber", "Iron"], smooth_angle=40, at=at)


# ---- loose stone -------------------------------------------------------------------------------

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


def stepping_stones(seed=1, at=(0, 0, 0), water=0.0):
    """Stones across a shallow stream along Y, one a tile: rounded boulders worn flat on top by
    feet, their sides dark with wet, a hand above the water (`water` is its level, relative to the
    origin)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    top = water + 0.1
    for k in range(4):
        x, y = rnd.uniform(-0.12, 0.12), -1.5 + k
        r = rnd.uniform(0.28, 0.35)
        faces = N.blob(bm, (x, y, water - 0.05), (r, r * rnd.uniform(0.8, 0.95), 0.3), 1, rnd, jitter=(0.85, 1.1))
        for f in faces:
            for v in f.verts:
                v.co.z = min(v.co.z, top + rnd.uniform(-0.015, 0.015))   # worn flat by feet
        for f in faces:
            f.material_index = 0 if f.calc_center_median().z > top - 0.04 else 1
    return N.make("SteppingStones", bm, ["Scree", "Cinder"], smooth_angle=30, at=at)


# ---- ways ---------------------------------------------------------------------------------------

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


# ---- camps and work ----------------------------------------------------------------------------

def campfire(seed=1, at=(0, 0, 0), burnt=False):
    """A fire laid in a ring of stones on a bed of ash: four logs leaned together over kindling
    (the flames are the game's to draw). burnt: the fire out, the logs burnt to black stubs."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    A.prism(bm, 9, 0.38, 0.34, -0.02, 0.025, mat_index=1, rot=rnd.uniform(0, 1))
    for k in range(9):
        a = 2 * math.pi * k / 9 + rnd.uniform(-0.1, 0.1)
        c = Vector((0.46 * math.cos(a), 0.46 * math.sin(a), 0))
        s = rnd.uniform(0.09, 0.12)
        pts = [c + Vector((rnd.uniform(-s, s), rnd.uniform(-s, s) * 0.8, -0.02)) for _ in range(4)]
        pts += [c + Vector((rnd.uniform(-s, s) * 0.6, rnd.uniform(-s, s) * 0.5, s * rnd.uniform(0.8, 1.1))) for _ in range(3)]
        for f in A.hull(bm, pts):
            f.material_index = 0
    if burnt:
        for k in range(4):
            a = 2 * math.pi * k / 4 + rnd.uniform(-0.3, 0.3)
            d = Vector((math.cos(a), math.sin(a), 0))
            N.rod(bm, [d * 0.3 + Vector((0, 0, 0.04)), d * 0.08 + Vector((0, 0, 0.07))], 0.04, mat=3, sides=5, taper=0.6)
        N.blob(bm, (0, 0, 0.02), (0.18, 0.16, 0.06), 0, rnd, mat=1, squash_below=0.0)
    else:
        top = Vector((0, 0, 0.42))
        for k in range(4):
            a = 2 * math.pi * k / 4 + 0.4 + rnd.uniform(-0.15, 0.15)
            d = Vector((math.cos(a), math.sin(a), 0))
            rings, faces = A.loft(bm, [(d * 0.3 + Vector((0, 0, 0.02)), 0.045, 0.045), (top + d * 0.03, 0.035, 0.035)],
                                  sides=5, ref=(0, 0, 1), mats=2)
            for f in faces:
                if f.calc_center_median().z < 0.18:
                    f.material_index = 3                       # charred where the flames reach
        for k in range(6):                                     # kindling
            a = rnd.uniform(0, 2 * math.pi)
            d = Vector((math.cos(a), math.sin(a), 0))
            N.rod(bm, [d * 0.15 + Vector((0, 0, 0.03)), d * 0.03 + Vector((0, 0, 0.2))], 0.012, mat=2, sides=3)
    return N.make("CampfireBurnt" if burnt else "Campfire", bm, ["Scree", "AshPlain", "Bark", "Charcoal"],
                  smooth_angle=40, at=at)


def lean_to(seed=1, at=(0, 0, 0)):
    """A woodsman's lean-to along X: a ridge pole on two forked uprights, poles leaned from it to the
    ground behind, roofed with overlapping sheets of bark; a bedroll under it, a log seat in front."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    ridge_z, back = 1.35, 1.1
    for x in (-0.9, 0.9):
        N.rod(bm, N.wobble(rnd, (x, -0.2, -0.05), (x, -0.2, ridge_z + 0.1), 3, 0.02), 0.05, mat=0, sides=5)
        for s in (-1, 1):
            N.branch(bm, (x, -0.2, ridge_z - 0.05), (x + s * 0.1, -0.2, ridge_z + 0.25), 0.025, None, mat=0, sides=4)
    N.rod(bm, N.wobble(rnd, (-1.15, -0.2, ridge_z), (1.15, -0.2, ridge_z + 0.03), 4, 0.02), 0.045, mat=0, sides=5)
    for k in range(6):
        x = -0.95 + 0.38 * k
        N.rod(bm, [(x, -0.25, ridge_z + 0.05), (x + rnd.uniform(-0.05, 0.05), back, -0.05)], 0.03, mat=0, sides=4)
    for k in range(4):                                         # bark sheets, each a thin slab on the poles
        x0, x1 = -1.05 + 0.53 * k, -1.05 + 0.53 * k + 0.62
        pts = []
        for t in (0.02, 0.98):
            y = -0.22 + (back + 0.22) * t
            z = (ridge_z + 0.09) * (1 - t) + rnd.uniform(-0.02, 0.02)
            pts += [Vector((x0, y, z)), Vector((x1, y, z))]
        for f in A.hull(bm, pts + [p - Vector((0, 0, 0.03)) for p in pts]):
            f.material_index = 1
    A.loft(bm, [((-0.55, 0.45, 0.09), 0.1, 0.09), ((0.45, 0.45, 0.09), 0.1, 0.09), ((0.62, 0.45, 0.05), 0.1, 0.06)],
           sides=7, ref=(0, 0, 1), mats=2)
    N.rod(bm, [(-0.7, -0.75, 0.13), (0.6, -0.8, 0.13)], 0.14, mat=1, sides=7)
    return N.make("LeanTo", bm, ["DeadWood", "Bark", "Wool"], smooth_angle=40, at=at)


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


def skeps(seed=1, at=(0, 0, 0)):
    """Bee skeps on their bench along X: three domed hives of coiled straw, each coil a band, a dark
    doorway at the foot of each, on a plank across two stumps."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    top = 0.42
    for x in (-0.55, 0.55):
        N.cut_stub(bm, (x, 0, -0.03), 0.12, top - 0.03, 0.0, rnd, sides=6)
    A.box(bm, (-0.8, -0.2, top - 0.03), (0.8, 0.2, top + 0.03), 2)
    for x in (-0.5, 0.0, 0.5):
        h, r = rnd.uniform(0.42, 0.5), rnd.uniform(0.19, 0.22)
        rings = []
        for i in range(7):
            t = i / 6
            rr, z = r * math.cos(t * math.pi / 2) ** 0.8, top + 0.03 + h * math.sin(t * math.pi / 2)
            rings.append([bm.verts.new((x, 0, z))] if rr < 0.01 else
                         [bm.verts.new((x + rr * math.cos(2 * math.pi * j / 10), rr * math.sin(2 * math.pi * j / 10), z)) for j in range(10)])
        for i in range(1, len(rings)):
            a, b = rings[i - 1], rings[i]
            for j in range(10):
                jn = (j + 1) % 10
                f = bm.faces.new((a[j], a[jn], b[0]) if len(b) == 1 else (a[j], a[jn], b[jn], b[j]))
                f.material_index = 0 if i % 2 else 1
        door = Vector((x, -r - 0.004, top + 0.035))
        v = [bm.verts.new(door + Vector((dx, 0, dz))) for dx, dz in ((-0.04, 0), (0.04, 0), (0.03, 0.045), (-0.03, 0.045))]
        bm.faces.new(v).material_index = 3
    return N.make("Skeps", bm, ["Thatch", "Wheat", "Planks", "Charcoal"], smooth_angle=50, at=at)


def scarecrow(seed=1, at=(0, 0, 0)):
    """A scarecrow in the furrows: a pole with a cross-stick for arms, an old coat hung on it and
    blown downwind, a sack for a head under a battered hat, straw at the cuffs."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    N.rod(bm, [(0, 0, -0.1), (0.02, 0, 1.9)], 0.04, mat=0, sides=5)
    arm_z = 1.45
    N.rod(bm, [(-0.65, 0, arm_z + 0.03), (0.65, 0, arm_z - 0.02)], 0.03, mat=0, sides=4)
    sway = WIND * 0.08
    A.loft(bm, [((0, 0, arm_z + 0.08), 0.15, 0.1), ((0, 0, arm_z - 0.1), 0.26, 0.15),
                (Vector((0, 0, 0.85)) + sway, 0.28, 0.16), (Vector((0, 0, 0.62)) + sway * 1.5, 0.3, 0.17)],
           sides=8, ref=(0, 1, 0), mats=1)
    for s in (-1, 1):                                          # sleeves hanging off the stick
        A.loft(bm, [((s * 0.15, 0, arm_z), 0.1, 0.1), ((s * 0.5, 0, arm_z - 0.04), 0.09, 0.11),
                    (Vector((s * 0.6, 0, arm_z - 0.12)) + sway, 0.1, 0.12)], sides=6, ref=(0, 1, 0), mats=1)
        for j in range(3):
            base = Vector((s * 0.6, 0, arm_z - 0.14)) + sway
            A.spike(bm, base, 0.025, base + Vector((s * 0.06 + rnd.uniform(-0.03, 0.03), rnd.uniform(-0.04, 0.04), -0.18)),
                    sides=3, mat=3)
    N.blob(bm, (0.02, 0, 1.68), (0.14, 0.13, 0.17), 1, rnd, jitter=(0.9, 1.08), mat=2)
    A.prism(bm, 9, 0.24, 0.24, 1.8, 1.82, (0.02, 0), mat_index=4, cap_bottom=True)
    A.prism(bm, 7, 0.12, 0.09, 1.82, 1.99, (0.03, 0), mat_index=4)
    for dx in (-0.05, 0.05):                                   # eyes drawn on the sack
        v = [bm.verts.new((0.02 + dx + x, -0.15, 1.72 + z)) for x, z in ((-0.015, -0.015), (0.015, -0.015), (0.015, 0.015), (-0.015, 0.015))]
        bm.faces.new(v).material_index = 5
    return N.make("Scarecrow", bm, ["DeadWood", "Russet", "Hide", "Wheat", "Umber", "Charcoal"], smooth_angle=45, at=at)


def eel_traps(seed=1, at=(0, 0, 0)):
    """Eel traps drying on the bank: long wicker funnels, round at the mouth and drawn to a tied
    point, bound with darker withies, two lying and one propped on them."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for c, d in (((-0.1, -0.2, 0.17), (1, 0.15, 0)), ((0.05, 0.22, 0.15), (1, -0.1, 0)), ((-0.05, 0.0, 0.45), (1, 0.05, 0.3))):
        c, d = Vector(c), Vector(d).normalized()
        L = 0.75
        secs = [(c - d * L * 0.5, 0.17, 0.17), (c - d * L * 0.15, 0.16, 0.16), (c + d * L * 0.25, 0.11, 0.11),
                (c + d * L * 0.5, 0.03, 0.03)]
        rings, faces = A.loft(bm, secs, sides=8, ref=(0, 0, 1), mats=0)
        mouth = set(rings[0])
        for f in faces:
            if all(v in mouth for v in f.verts):
                f.material_index = 2                           # the dark inside of the mouth
        for t, r in ((-0.32, 0.175), (0.0, 0.15), (0.3, 0.1)):
            p = c + d * L * t
            A.loft(bm, [(p - d * 0.015, r, r), (p + d * 0.015, r, r)], sides=8, ref=(0, 0, 1), mats=1)
    return N.make("EelTraps", bm, ["Driftwood", "Umber", "Charcoal"], smooth_angle=50, at=at)


# ---- things people leave about ------------------------------------------------------------------

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


# ---- trees -------------------------------------------------------------------------------------

def birch(seed=3, at=(0, 0, 0)):
    """A birch on poor ground: a slender pale trunk with dark scars, leaning a little, thin limbs
    swept up and out, the small light leaves hanging from them in loose strands."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    top = Vector((0.25, 0.1, 4.2))
    N.rod(bm, N.wobble(rnd, (0, 0, -0.05), top, 10, 0.04), 0.12, sides=6, taper=0.35)
    for f in list(bm.faces):                                   # dark scars round the trunk
        if 0.25 < f.calc_center_median().z < 3.8 and rnd.random() < 0.35:
            f.material_index = 3
    N.roots(bm, 0.12, 0.35, 3, rnd, rise=1.8)
    for k in range(7):
        a = 2.39996 * k + rnd.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), 0))
        start = top * ((1.9 + 0.33 * k) / top.z)
        tip = start + out * rnd.uniform(0.8, 1.15) + Vector((0, 0, rnd.uniform(0.5, 0.9)))
        N.rod(bm, [start, start.lerp(tip, 0.5) + out * 0.1, tip], 0.035, sides=4, taper=0.5)
        N.clump(bm, tip + Vector((0, 0, -0.2)), (0.4, 0.4, 0.62), rnd, 1, 2, uvs, sides=6)
    N.clump(bm, top + Vector((0, 0, 0.05)), (0.38, 0.38, 0.6), rnd, 1, 2, uvs, sides=6)
    return N.leafy("Birch", bm, uvs, ["Birch", N.leaf_mat("Birchleaf"), "Leaf1", "Charcoal"], at)


def birch_stump(seed=3, at=(0, 0, 0)):
    """The birch after Woodcutting: a slim pale stump cut aslant."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    N.cut_stub(bm, (0, 0, -0.05), 0.12, 0.35, 0.05, rnd, sides=6)
    N.roots(bm, 0.12, 0.35, 3, rnd, rise=1.5)
    return N.make("BirchStump", bm, ["Birch", "Ash"], smooth_angle=50, at=at)


def sapling(seed=2, at=(0, 0, 0)):
    """A young tree planted out: a thin stem tied to a stake against the wind, one small round head
    of leaves, a ring of dug earth at its foot."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    A.prism(bm, 8, 0.3, 0.26, -0.02, 0.02, mat_index=3)
    N.rod(bm, N.wobble(rnd, (0, 0, -0.05), (0.06, 0.02, 1.5), 4, 0.025), 0.035, sides=5, taper=0.5)
    N.rod(bm, [(-0.1, 0.0, -0.1), (-0.08, 0.0, 1.0)], 0.03, mat=4, sides=4, tip=(-0.08, 0.0, 1.05))
    A.loft(bm, [((-0.04, 0, 0.8), 0.08, 0.045), ((-0.04, 0, 0.84), 0.08, 0.045)], sides=6, ref=(0, 1, 0), mats=4)
    N.clump(bm, (0.06, 0.02, 1.45), (0.4, 0.4, 0.5), rnd, 1, 2, uvs, sides=7)
    return N.leafy("Sapling", bm, uvs, ["Bark", N.leaf_mat("Foliage"), "Leaf1", "Furrow", "DeadWood"], at)


def rotten_stump(seed=5, at=(0, 0, 0)):
    """An old stump rotting where a tree fell long ago: broken off in jagged points, hollow and dark
    at the heart, green with moss on the weather side, toadstools at its foot."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    r, n = 0.3, 9
    lo, hi, inner = [], [], []
    for k in range(n):
        a = 2 * math.pi * k / n
        d = Vector((math.cos(a), math.sin(a), 0))
        lo.append(bm.verts.new(d * r * 1.12 + Vector((0, 0, -0.05))))
        hi.append(bm.verts.new(d * r + Vector((0, 0, rnd.uniform(0.45, 0.75) if k % 2 else rnd.uniform(0.25, 0.4)))))
        inner.append(bm.verts.new(d * r * 0.6 + Vector((0, 0, 0.18))))
    for k in range(n):
        m = (k + 1) % n
        f = bm.faces.new((lo[k], lo[m], hi[m], hi[k]))
        f.material_index = 2 if f.calc_center_median().x < -0.2 else 0      # moss on the weather side
        bm.faces.new((hi[k], hi[m], inner[m], inner[k])).material_index = 1
    bm.faces.new(list(reversed(inner))).material_index = 3
    N.roots(bm, r, 0.6, 4, rnd, rise=0.9)
    for k in range(4):
        a = rnd.uniform(0.5, 2.6)
        c = Vector((0.38 * math.cos(a), 0.38 * math.sin(a), 0))
        h = rnd.uniform(0.06, 0.12)
        N.branch(bm, c - Vector((0, 0, 0.02)), c + Vector((0, 0, h)), 0.015, 0.015, mat=4, sides=4)
        A.loft(bm, [(c + Vector((0, 0, h - 0.01)), 0.06, 0.06), (c + Vector((0, 0, h + 0.035)), 0, 0)], sides=6,
               ref=(1, 0, 0), mats=5)
    return N.make("RottenStump", bm, ["DeadWood", "Umber", "Moss", "CryptEarth", "Linen", "Ochre"], smooth_angle=40, at=at)


# ---- the board ---------------------------------------------------------------------------------

def row(y_wilds, y_ways=None):
    """The board's rows: ground cover, deadwood and stone; then ways, camps and work."""
    objs, x = {}, 2.0
    for name, fn, w in (("bracken", bracken, 1.8), ("bracken_2", lambda at: bracken(seed=4, at=at), 2.0),
                        ("bramble", bramble, 2.6), ("wildflowers", wildflowers, 1.6),
                        ("wildflowers_2", lambda at: wildflowers(seed=7, at=at), 1.8), ("heather", heather, 1.8),
                        ("heather_2", lambda at: heather(seed=3, at=at), 2.0), ("thistles", thistles, 2.4),
                        ("fallen_log", fallen_log, 3.6), ("woodpile", woodpile, 2.2), ("brushwood", brushwood, 2.4),
                        ("chopping_block", chopping_block, 2.6), ("scree", scree, 2.4)):
        objs[name] = fn(at=(x, y_wilds, 0))
        x += w
    if y_ways is None:
        return objs
    x = 2.0
    for name, fn, w in (("fingerpost", fingerpost, 2.0), ("milestone", milestone, 1.6), ("footbridge", footbridge, 2.4),
                        ("campfire", campfire, 1.6), ("campfire_burnt", lambda at: campfire(at=at, burnt=True), 2.0),
                        ("lean_to", lean_to, 3.0), ("trough", trough, 2.2), ("skeps", skeps, 2.4),
                        ("scarecrow", scarecrow, 2.0), ("eel_traps", eel_traps, 2.0), ("barrel", barrel, 1.0),
                        ("crate", crate, 1.0), ("sacks", sacks, 2.2), ("birch", birch, 2.6),
                        ("birch_stump", birch_stump, 1.8), ("sapling", sapling, 1.8), ("rotten_stump", rotten_stump, 2.0)):
        objs[name] = fn(at=(x, y_ways, 0))
        x += w
    return objs


# Name in the game (Resources/Nature/<Name>) to builder.
ASSETS = {
    "Bracken": bracken, "Bramble": bramble, "Wildflowers": wildflowers, "Heather": heather, "Thistles": thistles,
    "FallenLog": fallen_log, "Woodpile": woodpile, "Brushwood": brushwood, "ChoppingBlock": chopping_block,
    "Scree": scree, "SteppingStones": stepping_stones,
    "Fingerpost": fingerpost, "Milestone": milestone, "Footbridge": footbridge,
    "Campfire": campfire, "CampfireBurnt": lambda: campfire(burnt=True), "LeanTo": lean_to, "Trough": trough,
    "Skeps": skeps, "Scarecrow": scarecrow, "EelTraps": eel_traps, "Barrel": barrel, "Crate": crate, "Sacks": sacks,
    "Birch": birch, "BirchStump": birch_stump, "Sapling": sapling, "RottenStump": rotten_stump,
}
