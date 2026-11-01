r"""Boss concepts (2026-11-01): a board of maquettes, one idea each, to find what works.
The briefing is docs/bosses/concepts.md.

Each concept is a real thing made a little more fantastical, with one standout feature that sets its
theme. Maquettes, not models: judge the idea and the silhouette. Every cell has a label and a player
(1.8 m, indigo) for scale.

    import runpy; ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\boss_concepts.py"); ns["run"]()
"""
import math
import os
import random
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)

import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

COLS = ["Indigo", "SaintBone", "Moonlight", "Sea", "Ice", "Cinder", "Moor", "Reed", "Ashgrey", "Sable",
        "Bronze", "SaintRobe", "Gold", "Ember", "Chalk", "Unmade", "Wheat", "Dust", "Fen", "Skin2", "Charcoal",
        "Obsidian", "CandleWax", "Peat", "Russet", "HairGrey", "Iron", "Blood", "Moss", "Grass", "Mud",
        "Heather", "Verdigris", "Snow", "BlackBile", "Hide", "Leaves", "Gall", "Steel", "Water", "Ochre",
        "BronzeShade", "Copper", "Skin4", "DarkLeaves", "Leaf3", "Thistle", "Sand", "Phlegm", "SynodRed"]


def M(name):
    return COLS.index(name)


# -- primitives -----------------------------------------------------------------------------------

def _paint(verts, mat):
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = M(mat)


def tube(bm, pts, rs, mat, sides=8, caps=True):
    """A tube through points with a radius at each: limbs, necks, tails, stalks, chains."""
    P = [Vector(p) for p in pts]
    if isinstance(rs, (int, float)):
        rs = [rs] * len(P)
    n = len(P)
    tans = [(P[min(i + 1, n - 1)] - P[max(i - 1, 0)]).normalized() for i in range(n)]
    ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    rings = []
    for p, t, r in zip(P, tans, rs):
        s = ref - t * ref.dot(t)
        s = s.normalized() if s.length > 1e-6 else t.orthogonal().normalized()
        u = t.cross(s)
        ref = s
        rings.append([bm.verts.new(p + (s * math.cos(2 * math.pi * k / sides) + u * math.sin(2 * math.pi * k / sides)) * r)
                      for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k])).material_index = M(mat)
    if caps:
        bm.faces.new(list(reversed(rings[0]))).material_index = M(mat)
        bm.faces.new(rings[-1]).material_index = M(mat)


def lathe(bm, base, prof, mat, seg=16, sx=1.0, sy=1.0, mats=None, cap_top=True, cap_bottom=True):
    """A turned shape: a profile of (height, radius) from the bottom up revolved round a vertical axis
    at base: bells, robes, candles, hives, caps. mats: a colour per band."""
    b = Vector(base)
    rings = []
    for z, r in prof:
        rings.append([bm.verts.new(b + Vector((r * sx * math.cos(2 * math.pi * k / seg),
                                                r * sy * math.sin(2 * math.pi * k / seg), z))) for k in range(seg)])
    for i, (a, c) in enumerate(zip(rings, rings[1:])):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], c[(k + 1) % seg], c[k])).material_index = M(mats[i] if mats else mat)
    if cap_bottom and prof[0][1] > 1e-4:
        bm.faces.new(list(reversed(rings[0]))).material_index = M(mat)
    if cap_top and prof[-1][1] > 1e-4:
        bm.faces.new(rings[-1]).material_index = M(mats[-1] if mats else mat)


def ball(bm, c, r, mat, s=(1, 1, 1), seg=12, rings=8, rot=None):
    m = Matrix.Translation(Vector(c)) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((s[0], s[1], s[2], 1))
    v = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r, matrix=m)["verts"]
    _paint(v, mat)
    return v


def align(d):
    return Vector((0, 0, 1)).rotation_difference(Vector(d).normalized()).to_matrix().to_4x4()


def cone(bm, base, tip, r0, r1, mat, seg=8):
    b, t = Vector(base), Vector(tip)
    m = Matrix.Translation((b + t) / 2) @ align(t - b)
    v = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r0, radius2=r1,
                              depth=(t - b).length, matrix=m)["verts"]
    _paint(v, mat)
    return v


def box(bm, c, size, mat, rot_z=0.0, d=None):
    m = Matrix.Translation(Vector(c))
    if d is not None:
        m = m @ align(d)
    m = m @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Diagonal((size[0], size[1], size[2], 1))
    v = bmesh.ops.create_cube(bm, size=1.0, matrix=m)["verts"]
    _paint(v, mat)
    return v


def disc(bm, c, r, mat, seg=28, rough=0.0, rnd=None):
    v = []
    for k in range(seg):
        rr = r * (1 + (rnd.uniform(-rough, rough) if rnd else 0))
        v.append(bm.verts.new((c[0] + rr * math.cos(2 * math.pi * k / seg), c[1] + rr * math.sin(2 * math.pi * k / seg), c[2])))
    bm.faces.new(v).material_index = M(mat)


def quad(bm, pts, mat):
    bm.faces.new([bm.verts.new(p) for p in pts]).material_index = M(mat)


def house(bm, at, rz, w=2.6, d=2.0, h=1.8, wall="Chalk", roof="Russet"):
    """A cottage: walls and a pitched roof, turned rz round its middle."""
    x, y, z = at
    box(bm, (x, y, z + h / 2), (w, d, h), wall, rot_z=rz)
    R = Matrix.Rotation(rz, 3, "Z")
    c = Vector((x, y, z))
    for s_ in (1, -1):
        pts = [Vector((-w / 2 - 0.15, s_ * (d / 2 + 0.2), h)), Vector((w / 2 + 0.15, s_ * (d / 2 + 0.2), h)),
               Vector((w / 2 + 0.15, 0, h + 1.1)), Vector((-w / 2 - 0.15, 0, h + 1.1))]
        quad(bm, [tuple(c + R @ p) for p in pts], roof)
    for s_ in (1, -1):
        pts = [Vector((s_ * w / 2, -d / 2, h)), Vector((s_ * w / 2, d / 2, h)), Vector((s_ * w / 2, 0, h + 1.1))]
        bm.faces.new([bm.verts.new(c + R @ p) for p in pts]).material_index = M(wall)


def player(bm, x, y, z=0.0):
    tube(bm, [(x, y, z + 0.1), (x, y, z + 1.0), (x, y, z + 1.45)], [0.18, 0.22, 0.2], "Indigo", 6)
    ball(bm, (x, y, z + 1.65), 0.14, "Skin2", seg=8, rings=6)


def figure(bm, at, h, body, skin, robe=None, hunch=0.0, arms=None, head=True, head_mat=None, legs=True,
           thin=1.0):
    """A humanoid of height h at `at`, facing -y: legs, a torso bending forward by `hunch`, arms given as
    (elbow, hand) offsets from the shoulders in heights (left arm; the right mirrors unless given as a
    pair), a robe (a lathe flaring to the ground) if given. Returns its points (head, hands, chest)."""
    x, y, z = at
    k = h
    neck_y = -hunch * k
    pelvis = Vector((x, y, z + 0.5 * k))
    chest = Vector((x, y - hunch * 0.6 * k, z + 0.72 * k))
    neck = Vector((x, y + neck_y, z + 0.83 * k - hunch * 0.06 * k))
    headc = neck + Vector((0, -hunch * 0.08 * k, 0.09 * k))
    if legs and robe is None:
        for s in (1, -1):
            tube(bm, [(x + s * 0.09 * k, y, z + 0.48 * k), (x + s * 0.1 * k, y - 0.02 * k, z + 0.25 * k),
                      (x + s * 0.1 * k, y, z + 0.02 * k)], [0.06 * k * thin, 0.045 * k * thin, 0.04 * k * thin], body, 6)
    tube(bm, [pelvis, chest, neck], [0.11 * k * thin, 0.14 * k * thin, 0.05 * k], body, 8)
    if robe:
        lathe(bm, (x, y - hunch * 0.2 * k, z), [(0, 0.3 * k), (0.45 * k, 0.17 * k), (0.78 * k, 0.13 * k), (0.84 * k, 0.05 * k)],
              robe, seg=12)
    hands = []
    arms = arms or [((0.06, 0.02, -0.2), (0.06, -0.06, -0.38))]
    if len(arms) == 1:
        e, hnd = arms[0]
        arms = [arms[0], ((-e[0], e[1], e[2]), (-hnd[0], hnd[1], hnd[2]))]
    for s, (e, hnd) in zip((1, -1), arms):
        sh = chest + Vector((s * 0.15 * k, 0, 0.08 * k))
        el = sh + Vector((e[0] * k * s if s > 0 else e[0] * k, e[1] * k, e[2] * k))
        ha = sh + Vector((hnd[0] * k * s if s > 0 else hnd[0] * k, hnd[1] * k, hnd[2] * k))
        tube(bm, [sh, el, ha], [0.045 * k * thin, 0.035 * k * thin, 0.03 * k * thin], body if robe is None else robe, 6)
        ball(bm, ha, 0.035 * k, skin, seg=6, rings=4)
        hands.append(ha)
    if head:
        ball(bm, headc, 0.075 * k, head_mat or skin, s=(0.85, 0.9, 1.05))
    return dict(head=headc, hands=hands, chest=chest, neck=neck)


def merge(bm, sub, loc=(0, 0, 0), rot=0.0, scale=1.0):
    import bpy
    bmesh.ops.scale(sub, vec=(scale, scale, scale), verts=sub.verts)
    bmesh.ops.rotate(sub, cent=(0, 0, 0), matrix=Matrix.Rotation(rot, 3, "Z"), verts=sub.verts)
    bmesh.ops.translate(sub, vec=Vector(loc), verts=sub.verts)
    me = bpy.data.meshes.new("_merge")
    sub.to_mesh(me)
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def creature(spec, skin, claw):
    from artlib.beast import quadruped
    return quadruped(spec, M(skin), M(claw)).bm


def branch(bm, p, d, length, depth, r, mat, spread=0.55, rng=None):
    e = p + d * length
    cone(bm, p, e, r, r * 0.5, mat, 6)
    if depth == 0:
        return
    for turn in (-spread, spread):
        axis = d.cross(Vector((0, 0, 1)))
        axis = axis.normalized() if axis.length > 1e-6 else Vector((1, 0, 0))
        nd = (Matrix.Rotation(turn, 3, axis if rng is None else Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.2)).normalized()) @ d)
        nd = (nd + Vector((0, 0, 0.25))).normalized()
        branch(bm, e, nd, length * 0.72, depth - 1, r * 0.55, mat, spread, rng)


# -- the concepts ---------------------------------------------------------------------------------

def lure(bm):
    disc(bm, (0, 0, 0.02), 13, "Sea", 32)
    ball(bm, (0, 4.5, -0.6), 3.2, "Peat", s=(1.3, 1.2, 0.35), seg=14, rings=6)   # the head, barely breaking
    for x in (-2.2, 2.2):
        ball(bm, (x, 2.4, 0.35), 0.45, "Phlegm", s=(1, 0.7, 0.6), seg=8, rings=5)   # milky eyes
    tube(bm, [(0, 4.8, 0.4), (0, 4.6, 3.5), (0, 3.0, 6.2), (0, 1.0, 6.6), (0, 0, 5.0)],
         [0.28, 0.24, 0.2, 0.15, 0.08], "Peat")
    ball(bm, (0, 0, 4.5), 0.6, "Moonlight", s=(1, 1, 1.4), seg=6, rings=4)
    for k in range(7):                                  # shards round the lure: it looks like loot
        a = k * 0.9
        cone(bm, (0.8 * math.cos(a), 0.8 * math.sin(a), 4.3), (1.2 * math.cos(a), 1.2 * math.sin(a), 4.9), 0.12, 0, "Moonlight", 5)
    for k in range(24):                                 # the fangs of the lower jaw breaking the surface
        a = math.pi * (0.05 + 0.9 * k / 23)
        x, y = 5.8 * math.cos(a), 1.6 - 3.5 * math.sin(a)
        h = 0.6 + 1.3 * math.sin(a) ** 2
        cone(bm, (x, y, -0.3), (x * 0.9, y + 0.5, h), 0.3, 0.0, "SaintBone", 5)
    player(bm, 1.5, -5.5)


def moon_jelly(bm):
    lathe(bm, (0, 0, 13.5), [(0, 6.2), (0.6, 6.4), (1.6, 6.0), (2.6, 5.0), (3.4, 3.4), (3.9, 1.6), (4.05, 0)], "Ice", seg=24)
    for k in range(4):                                  # the four glowing rings: a moon jelly's mark
        a = math.pi / 2 * k + 0.4
        c = Vector((2.0 * math.cos(a), 2.0 * math.sin(a), 15.6))
        pts = [c + Vector((0.9 * math.cos(t) * math.cos(a) - 0.5 * math.sin(t) * math.sin(a),
                           0.9 * math.cos(t) * math.sin(a) + 0.5 * math.sin(t) * math.cos(a), 0)) for t in
               [2 * math.pi * j / 10 for j in range(11)]]
        tube(bm, pts, 0.22, "Moonlight", 6, caps=False)
    for k in range(32):                                 # the fringe of tendrils from the rim
        a = 2 * math.pi * k / 32
        x, y = 6.2 * math.cos(a), 6.2 * math.sin(a)
        w = 0.8 * math.sin(a * 3 + 1)
        tube(bm, [(x, y, 13.5), (x * 1.02 + w, y * 1.02, 9), (x * 0.95 - w, y * 0.95, 4.5), (x * 0.9, y * 0.9, 0.5)],
             [0.08, 0.06, 0.04, 0.015], "Ice", 4)
    for k in range(4):                                  # oral arms: wide frilled ribbons, twisting down
        a = math.pi / 2 * k
        pts = []
        for j in range(8):
            t = j / 7
            pts.append((1.0 * math.cos(a + t * 2.5) * (1 + t), 1.0 * math.sin(a + t * 2.5) * (1 + t), 13.4 - t * 9))
        tube(bm, pts, [0.7 - 0.5 * j / 7 for j in range(8)], "Thistle", 6)
    player(bm, 3, -4)


def tortoise(bm):
    shell = lathe(bm, (0, 0, 1.5), [(0, 15.5), (1.6, 15.8), (4, 14.5), (6.5, 11.5), (8.4, 7.2), (9.4, 3.0), (9.6, 0)],
                  "Cinder", seg=18, sy=0.82)
    for ring, (r, z, n) in enumerate(((0.0, 11.0, 1), (6.0, 10.0, 6), (10.5, 7.7, 10), (13.6, 4.5, 14))):   # scutes
        for k in range(n):
            a = 2 * math.pi * k / n + ring * 0.3
            c = (r * math.cos(a), r * 0.82 * math.sin(a), z)
            ball(bm, c, 2.4 if ring < 3 else 1.9, "Ashgrey", s=(1, 1, 0.28), seg=6, rings=4,
                 rot=align(Vector((c[0], c[1] / 0.82 ** 2, c[2] - 1.5 + 6))))
    for x, y, rz in ((-3, -2, 0.3), (2.5, -3.5, -0.2), (4, 2.5, 0.5), (-4.5, 3, -0.4), (0, 5, 0.1)):   # the moved village
        z = 11.0 - 0.05 * (x * x + y * y) ** 0.5
        house(bm, (x, y, z), rz)
    box(bm, (1.0, 0.5, 13.5), (1.6, 1.6, 6.0), "Ashgrey", rot_z=0.2)          # a broken tower
    box(bm, (1.3, 0.5, 16.7), (1.0, 1.6, 0.6), "Ashgrey", rot_z=0.5)
    tube(bm, [(-6, -3, 10.5), (-5.5, -2, 12.5), (-6.2, -1, 14.5)], [0.35, 0.25, 0.1], "Peat", 5)   # an old tree
    for sx, sy in ((10.5, -9), (-10.5, -9), (10.5, 7.5), (-10.5, 7.5)):    # elephantine legs
        tube(bm, [(sx * 0.9, sy * 0.9, 4.0), (sx, sy, 1.5), (sx * 1.04, sy * 1.04, 0)], [3.0, 3.2, 3.4], "Moor", 10)
        for k in (-1, 0, 1):
            cone(bm, (sx * 1.04 + k * 1.4, sy * 1.04 - 3.0, 0.4), (sx * 1.04 + k * 1.5, sy * 1.04 - 3.8, 0.1), 0.5, 0, "SaintBone", 5)
    neck = [(0, -12, 3.5), (0, -14.5, 5.5), (0, -16.2, 8.5), (0, -17.5, 10.5)]
    tube(bm, neck, [3.2, 2.9, 2.7, 2.6], "Moor", 12)
    for j in range(5):                                  # wrinkle folds round the neck
        t = j / 4
        p = Vector(neck[0]).lerp(Vector(neck[2]), t)
        tube(bm, [p + Vector((0, 0.3, 0)), p - Vector((0, 0.3, 0))], 3.2 - t * 0.4, "Peat", 10)
    ball(bm, (0, -19.5, 11.2), 3.0, "Moor", s=(0.95, 1.35, 0.85))
    cone(bm, (0, -22.4, 10.6), (0, -24.0, 9.6), 1.4, 0.2, "SaintBone", 8)     # the beak
    for x in (2.3, -2.3):
        ball(bm, (x, -20.5, 12.0), 0.55, "Gall", seg=8, rings=5)
    player(bm, -1.0, -5.5, 11.0)


def heron(bm):
    rnd = random.Random(2)
    disc(bm, (0, 0, 0.02), 12, "Fen", 28)
    for k in range(70):
        x, y = rnd.uniform(-11, 11), rnd.uniform(-10, 10)
        cone(bm, (x, y, 0), (x + rnd.uniform(-0.3, 0.3), y, 1.5 + rnd.uniform(0, 1.6)), 0.05, 0.0, "Reed", 4)
    for s in (1, -1):
        tube(bm, [(s * 0.4, 0.4, 6.4), (s * 0.45, 0.8, 3.4), (s * 0.4, 0.2, 0)], [0.13, 0.1, 0.07], "Ashgrey", 6)
        tube(bm, [(s * 0.4, 0.2, 0.02), (s * 0.5, -0.8, 0.02)], 0.04, "Ashgrey", 4)
    tube(bm, [(0, 2.4, 6.8), (0, 1.0, 7.4), (0, -1.0, 7.8), (0, -1.8, 8.2)], [0.5, 1.2, 1.3, 0.7], "HairGrey", 10)
    for s in (1, -1):                                   # folded wings, darker primaries
        tube(bm, [(s * 1.1, -0.8, 8.0), (s * 1.2, 1.0, 7.6), (s * 0.7, 3.4, 6.6)], [0.45, 0.5, 0.15], "Ashgrey", 6)
    neck = [(0, -1.9, 8.4), (0, -2.8, 10.0), (0, -1.6, 11.6), (0, -0.4, 12.6), (0, -1.4, 14.0), (0, -3.6, 14.6),
            (0, -6.2, 13.8), (0, -8.6, 12.0)]
    tube(bm, neck, [0.5, 0.42, 0.35, 0.33, 0.33, 0.32, 0.33, 0.4], "HairGrey", 8)
    ball(bm, (0, -9.0, 11.8), 0.55, "HairGrey", s=(0.8, 1.3, 0.9))
    cone(bm, (0, -9.6, 11.6), (0, -12.8, 10.8), 0.3, 0.0, "Ochre", 6)
    tube(bm, [(0, -8.6, 12.3), (0, -7.2, 12.6), (0, -6.0, 12.4)], [0.12, 0.08, 0.02], "Sable", 4)   # crest plume
    player(bm, 2.0, -14.0)


def harvestman(bm):
    for k in range(9):                                  # the vault's wall, a ring of rough stone
        a = 2 * math.pi * k / 9 + 0.2
        box(bm, (17 * math.cos(a), 17 * math.sin(a), 6), (9, 2.0, 12), "Ashgrey", rot_z=a + math.pi / 2)
    ball(bm, (0, 0, 13), 1.9, "Chalk", s=(1, 1, 0.72), seg=14, rings=8)
    ball(bm, (0, 0, 14.3), 0.6, "Chalk", s=(1, 1, 0.6))
    for k in range(10):
        a = 2 * math.pi * k / 10
        ball(bm, (1.85 * math.cos(a), 1.85 * math.sin(a), 13.1), 0.2, "Moonlight", seg=6, rings=4)
    for k in range(8):
        a = 2 * math.pi * k / 8 + 0.2
        d = Vector((math.cos(a), math.sin(a), 0))
        tube(bm, [Vector((0, 0, 13)) + d * 1.5, Vector((0, 0, 18.5)) + d * 7.5, Vector((0, 0, 9 + 2 * math.sin(k))) + d * 15.8],
             [0.2, 0.14, 0.05], "Ashgrey", 6)
    player(bm, 3, -3)


def bell_ringer(bm):
    f = figure(bm, (0, 0, 0), 7.0, "Peat", "Skin4", robe="SaintRobe", hunch=0.3, head=False,
               arms=[((0.12, -0.1, -0.15), (0.18, -0.25, -0.42)), ((-0.1, -0.2, 0.05), (-0.05, -0.3, 0.3))])
    n = f["neck"] + Vector((0, -0.2, 0.0))                 # the bell sits on its shoulders: its head
    lathe(bm, n + Vector((0, 0, -0.15)), [(0, 1.55), (0.12, 1.5), (0.45, 1.15), (1.0, 0.92), (1.65, 0.88), (2.05, 0.7),
                                          (2.25, 0.3), (2.3, 0)], "Bronze", seg=16)
    lathe(bm, n + Vector((0, 0, -0.17)), [(0, 1.56), (0.1, 1.48)], "BronzeShade", seg=16)
    tube(bm, [n + Vector((0, 0, 1.9)), n + Vector((0, 0, 2.6))], 0.18, "Iron", 6)   # the crown's loop
    tube(bm, [n + Vector((0, -0.3, 0.6)), n + Vector((0, -0.55, -0.55))], 0.06, "Iron", 6)
    ball(bm, n + Vector((0, -0.6, -0.8)), 0.36, "Iron", seg=8, rings=6)     # the clapper, hanging where a jaw would be
    box(bm, n + Vector((0.55, -1.05, 0.8)), (0.07, 0.05, 1.3), "Sable", rot_z=0.4)   # the crack
    rope_top = f["hands"][1] + Vector((0, 0, 0.1))
    tube(bm, [rope_top, rope_top + Vector((0, 0, 6))], 0.05, "Hide", 4)
    player(bm, 3.5, -3.5)


def unquiet_saint(bm):
    box(bm, (0, 4.0, 1.6), (6.5, 3.0, 2.6), "Gold")                          # the reliquary
    for s in (1, -1):                                  # its peaked roof
        quad(bm, [(-3.25, 4.0 + s * 1.5, 2.9), (3.25, 4.0 + s * 1.5, 2.9), (3.25, 4.0, 4.6), (-3.25, 4.0, 4.6)], "Bronze")
    for x in (-3.25, 3.25):
        quad(bm, [(x, 2.5, 2.9), (x, 5.5, 2.9), (x, 4.0, 4.6)], "Bronze")
    for x in (-2.7, -0.9, 0.9, 2.7):
        cone(bm, (x, 4.0, 4.4), (x, 4.0, 5.8), 0.25, 0.0, "Gold", 6)
        box(bm, (x, 2.45, 1.6), (0.4, 0.1, 2.0), "BronzeShade")
    box(bm, (0, 4.0, 0.15), (7.0, 3.4, 0.3), "Peat")                          # runners
    for x in (-2.2, -1.4, 1.4, 2.2):
        tube(bm, [(x, 3.0, 4.0), (x, 3.0, 4.5)], 0.08, "CandleWax", 6)
        cone(bm, (x, 3.0, 4.5), (x, 3.0, 4.85), 0.07, 0.0, "Ember", 4)
    f = figure(bm, (0, -1.8, 0), 4.4, "Chalk", "Chalk", robe="SaintRobe", hunch=0.5,
               arms=[((0.05, 0.25, -0.2), (0.0, 0.5, -0.2))], thin=0.75)
    ring = Vector((0, -0.4, 1.6))
    for s in (1, -1):                                  # chains from his wrists back to the tomb
        a = f["hands"][0 if s > 0 else 1]
        b = Vector((s * 2.0, 2.45, 2.2))
        n = 9
        for j in range(n):
            p = a.lerp(b, (j + 0.5) / n) + Vector((0, 0, -0.35 * math.sin(math.pi * j / n)))
            box(bm, p, (0.12, 0.3, 0.06), "Iron", d=(b - a), rot_z=j * 1.57)
    player(bm, 3.5, -4.5)


def tithe_collector(bm):
    for s in (1, -1):
        tube(bm, [(s * 0.45, 0, 0), (s * 0.5, -0.2, 2.0), (s * 0.55, 0, 3.6)], [0.12, 0.14, 0.18], "Sable", 6)
    for k in range(10):
        a = 2 * math.pi * k / 10
        x, y = 1.25 * math.cos(a), 1.0 * math.sin(a)
        tube(bm, [(x * 0.45, y * 0.45, 3.6), (x, y, 4.6), (x * 1.05, y * 1.05, 6.0), (x * 0.4, y * 0.4, 7.0)], 0.07, "SaintBone", 4)
    tube(bm, [(0, 0.7, 3.6), (0, 0.9, 7.0)], 0.12, "SaintBone", 6)            # the spine behind the cage
    for k in range(8):                                 # the lights of those who never paid
        ball(bm, (0.55 * math.cos(k * 1.7), 0.45 * math.sin(k * 2.3), 4.0 + k * 0.38), 0.2, "Ember", seg=6, rings=4)
    tube(bm, [(0, 0, 7.0), (0, -0.1, 7.6), (0, -0.3, 8.6)], [0.2, 0.32, 0.12], "SaintBone", 6)   # a long mask
    for x in (0.12, -0.12):
        ball(bm, (x, -0.55, 8.0), 0.06, "Ember", seg=6, rings=4)
    cone(bm, (0, -0.1, 8.4), (0, 0.1, 9.6), 0.45, 0.0, "Sable", 8)
    for s in (1, -1):
        tube(bm, [(s * 1.0, 0, 6.6), (s * 2.0, -0.6, 5.2), (s * 2.3, -1.2, 3.4)], [0.1, 0.08, 0.06], "Sable", 5)
    tube(bm, [(2.3, -1.2, 3.4), (2.3, -1.2, 2.2)], 0.02, "Iron", 4)            # a balance in one hand
    tube(bm, [(1.8, -1.2, 2.2), (2.8, -1.2, 2.2)], 0.04, "Gold", 4)
    for x in (1.8, 2.8):
        disc(bm, (x, -1.2, 1.7), 0.3, "Gold", 10)
    tube(bm, [(-2.3, -1.2, 3.4), (-2.4, -1.4, 1.6), (-2.7, -1.9, 0.08), (-3.8, -2.4, 0.06)], 0.16, "CandleWax", 4)   # the ledger
    player(bm, -3.5, -4.0)


def fused(bm):
    """The Fused: the betrayers grown into one mass of faceless bodies; every hand holds up a borrowed face."""
    rnd = random.Random(3)
    lathe(bm, (0, 0, 0), [(0, 2.6), (1.2, 2.4), (2.6, 2.0), (3.8, 1.5), (4.6, 0.9), (5.0, 0)], "Charcoal", seg=14)
    tops = [(0, 0, 5.0), (-1.3, 0.3, 4.3), (1.4, 0.2, 4.4), (-0.7, 0.9, 3.8), (0.8, -0.8, 4.0)]
    for t in tops:                                     # torsos growing out of the mass, each with a blank head
        base = Vector((t[0] * 0.6, t[1] * 0.6, t[2] - 1.6))
        top = Vector(t) + Vector((t[0] * 0.25, t[1] * 0.25, 0.9))
        tube(bm, [base, (base + top) / 2 + Vector((0, 0, 0.2)), top], [0.55, 0.45, 0.25], "Charcoal", 8)
        ball(bm, top + Vector((0, 0, 0.45)), 0.42, "Chalk", s=(0.85, 0.8, 1.1), seg=10, rings=8)
    for k in range(14):                                # arms reaching up and out, each holding a face
        a = k * 2.399
        b0 = Vector((0.9 * math.cos(a), 0.9 * math.sin(a), 2.0 + (k % 4) * 0.8))
        hand = b0 + Vector((2.0 * math.cos(a), 2.0 * math.sin(a) - 0.6, 1.2 + (k % 3) * 0.7))
        elbow = (b0 + hand) / 2 + Vector((0, 0, 0.6))
        tube(bm, [b0, elbow, hand], [0.3, 0.22, 0.16], "Charcoal", 6)
        mat = rnd.choice(["Skin2", "Skin4", "CandleWax", "HairGrey"])
        face = hand + Vector((0, -0.3, 0.25))
        ball(bm, face, 0.42, mat, s=(0.85, 0.3, 1.05), seg=10, rings=6)
        for xe in (-0.14, 0.14):
            ball(bm, face + Vector((xe, -0.13, 0.1)), 0.06, "Sable", seg=6, rings=4)
    player(bm, 4.0, -4.5)


def rejected_dragon(bm):
    from assets.creatures.common import drake
    g = drake.body(crown=False, spine=False)
    for f in g.bm.faces:                                 # the left half finished, the right a blank draft
        c = f.calc_center_median()
        if c.x > 0.02:                                  # the side facing the board's camera is the draft
            f.material_index = M("Unmade")
        else:
            f.material_index = M("SaintBone") if f.material_index == 1 else M("Russet")
    for v in g.bm.verts:
        if v.co.x > 0.02:
            v.co.x *= 0.7
    merge(bm, g.bm, scale=1.6)
    for k in range(4):                                  # a wing of bones only, on the finished side
        a = 0.35 + k * 0.33
        tube(bm, [(-0.9, -1.0, 3.0), (-1.6, -0.6, 4.5), (-1.6 - 6.5 * math.cos(a), 0.5 + 2.2 * k, 3.6 + 5.5 * math.sin(a))],
             [0.16, 0.12, 0.03], "SaintBone", 5)
    quad(bm, [(0.6, -1.0, 2.8), (1.0, 2.5, 2.7), (7.0, 3.0, 6.5), (6.5, -2.5, 8.0)], "Chalk")   # one flat plane
    tube(bm, [(0.3, -2.4, 2.6), (1.5, -4.0, 3.6), (2.2, -5.4, 4.0)], 0.4, "Unmade", 4)   # a second neck
    box(bm, (2.5, -6.0, 4.1), (1.1, 1.1, 1.1), "Unmade", rot_z=0.3)           # its head: a blank block
    player(bm, 4.5, -5.0)


def hive_queen(bm):
    rnd = random.Random(5)
    tube(bm, [(-7, 2, 0), (-6.5, 2, 6), (-4.5, 2, 10.5), (0, 2, 13), (6, 2, 13.4)], [1.0, 0.85, 0.6, 0.45, 0.2], "Peat", 8)
    for k in range(5):
        tube(bm, [(-6.3 + k * 0.1, 2, 7 + k), (-8 - k * 0.4, 2.5, 9 + k * 0.6)], [0.25, 0.05], "Peat", 5)
    prof = [(0, 0.6), (0.7, 2.6), (1.5, 3.4), (2.4, 3.7), (3.3, 3.8), (4.2, 3.6), (5.1, 3.1), (6.0, 2.2), (6.8, 1.2), (7.2, 0.4)]
    lathe(bm, (0, 2, 4.4), prof, "Wheat", seg=16, mats=["Dust", "Wheat", "Dust", "Wheat", "Dust", "Wheat", "Dust", "Wheat", "Dust"])
    disc(bm, (0, -1.85, 6.5), 1.1, "Sable", 12)
    ball(bm, (0, -1.4, 6.6), 0.9, "Sable", s=(1, 1.1, 0.9))                    # her head, out of the mouth
    ball(bm, (0, -2.2, 6.0), 0.75, "Ochre", s=(1, 1.2, 1))                     # thorax
    for j in range(6):                                  # the striped abdomen, curling down to the sting
        t = j / 5
        c = (0, -2.8 - t * 2.4, 5.6 - t * 2.4)
        ball(bm, c, 0.9 - t * 0.45, "Ochre" if j % 2 else "Sable", s=(1, 1.2, 1), seg=10, rings=6)
    cone(bm, (0, -5.4, 3.0), (0, -6.0, 2.2), 0.2, 0.0, "Sable", 5)
    for s in (1, -1):
        quad(bm, [(s * 0.4, -2.0, 6.6), (s * 3.6, -1.0, 8.0), (s * 3.9, -2.6, 7.2), (s * 0.4, -2.6, 6.4)], "Ice")
        for k in range(3):
            tube(bm, [(s * 0.6, -2.2 - k * 0.2, 5.6), (s * 1.8, -2.6 - k * 0.3, 4.4), (s * 2.0, -3.0 - k * 0.4, 2.8)],
                 [0.08, 0.06, 0.02], "Sable", 4)
    for k in range(40):
        a = k * 2.399
        c = Vector((5.2 * math.cos(a), 2 + 5.2 * math.sin(a), 3 + (k * 0.53 % 9)))
        ball(bm, c, 0.18, "Ochre", s=(1, 1.4, 1), seg=6, rings=4)
    player(bm, 4.5, -5.5)


def mire_hand(bm):
    rnd = random.Random(6)
    disc(bm, (0, 0, 0.02), 13, "Fen", 28, rough=0.08, rnd=rnd)
    tube(bm, [(0, 0, -1), (0.3, 0, 4), (0.5, 0.2, 6.8)], [1.6, 1.4, 1.6], "Mud", 10)   # the forearm, slime-dark
    ball(bm, (0.5, 0.2, 8.6), 2.0, "Mud", s=(1.0, 0.45, 1.0), seg=12, rings=8)   # the palm
    ball(bm, (0.5, -0.65, 8.6), 0.75, "Moonlight", s=(1, 0.4, 0.75))            # the eye in it
    ball(bm, (0.5, -0.85, 8.6), 0.3, "Sable", s=(1, 0.3, 1))
    for x, lift in ((-1.3, 0.0), (-0.45, 0.4), (0.45, 0.5), (1.3, 0.2)):        # long fingers, curling in
        b = (0.5 + x, 0.2, 10.2)
        tube(bm, [b, (0.5 + x * 1.25, 0.0, 12.2 + lift), (0.5 + x * 1.3, -0.6, 13.8 + lift), (0.5 + x * 1.2, -1.4, 14.6 + lift)],
             [0.5, 0.42, 0.34, 0.22], "Mud", 6)
    tube(bm, [(-1.2, 0.2, 8.0), (-2.6, -0.2, 9.4), (-3.4, -0.9, 10.6)], [0.55, 0.45, 0.3], "Mud", 6)
    for k in range(11):                                 # fingertips breaking the surface round you
        a = 2 * math.pi * k / 11
        x, y = 8.5 * math.cos(a), 8.5 * math.sin(a)
        tube(bm, [(x, y, -0.3), (x * 0.97, y * 0.97, 0.9), (x * 0.9, y * 0.9, 1.4)], [0.35, 0.3, 0.2], "Mud", 6)
    for k in range(12):
        ball(bm, (rnd.uniform(-9, 9), rnd.uniform(-9, 9), 0.05), 0.6, "Leaf3", s=(1, 1, 0.15), seg=8, rings=4)
    player(bm, 3.5, -4.0)


def long_pilgrim(bm):
    for s in (1, -1):
        tube(bm, [(s * 0.6, 0, 15), (s * 1.6, -1.2, 8), (s * 2.0, 0.5, 0.3)], [0.17, 0.13, 0.05], "Sable", 6)
    lathe(bm, (0, 0, 13.4), [(0, 0.5), (1.4, 0.9), (2.8, 0.8), (3.2, 0.5)], "Charcoal", seg=10)
    lathe(bm, (0, -0.2, 16.4), [(0, 0.9), (1.0, 0.85), (2.0, 0.5), (2.6, 0)], "Charcoal", seg=10)   # the cowl
    disc(bm, (0, -1.05, 17.1), 0.45, "Sable", 10)
    for k in range(10):                                 # robes hanging in long strips
        a = 2 * math.pi * k / 10
        box(bm, (0.8 * math.cos(a), 0.7 * math.sin(a), 11.6 - (k % 3) * 1.4), (0.4, 0.05, 4.5 + (k % 3) * 1.6),
            "Charcoal" if k % 2 else "Peat", rot_z=a + math.pi / 2)
    tube(bm, [(0.8, -0.3, 16.0), (1.4, -1.2, 14.4), (1.5, -1.5, 13.2)], 0.12, "Charcoal", 5)
    tube(bm, [(1.5, -1.5, 13.2), (1.0, -2.2, 3.2)], 0.03, "Iron", 4)
    lathe(bm, (1.0, -2.2, 2.4), [(0, 0.1), (0.2, 0.45), (0.6, 0.5), (0.8, 0.3), (1.0, 0.05)], "Bronze", seg=8)
    for k in range(4):
        ball(bm, (1.0 + 0.3 * k, -2.2, 3.6 + k * 0.9), 0.5 + k * 0.2, "Ashgrey", seg=8, rings=5)   # smoke
    player(bm, 3.0, -3.5)


def murder(bm):
    rnd = random.Random(4)
    shape = [((0, 0, 6.5), 1.6, 2.6), ((0, 0, 10), 0.9, 0.9), ((-2.3, 0, 7.8), 0.5, 1.9), ((2.3, 0, 7.8), 0.5, 1.9),
             ((-0.8, 0, 2.6), 0.5, 2.6), ((0.8, 0, 2.6), 0.5, 2.6)]
    for c, r, h in shape:
        for _ in range(int(85 * r * h / 2)):
            p = Vector(c) + Vector((rnd.uniform(-r, r), rnd.uniform(-r * 0.6, r * 0.6), rnd.uniform(-h, h)))
            d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.2, 0.3))).normalized()
            cone(bm, p, p + d * 0.45, 0.14, 0.0, "Sable", 3)
            if rnd.random() < 0.3:                      # wings
                side = d.cross(Vector((0, 0, 1))).normalized()
                quad(bm, [p, p + side * 0.35 + d * 0.1, p + d * 0.2, p - side * 0.35 + d * 0.1], "Sable")
    for _ in range(80):
        p = Vector((rnd.uniform(-7, 7), rnd.uniform(-3, 3), rnd.uniform(3, 15)))
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)).normalized()
        cone(bm, p, p + d * 0.45, 0.14, 0.0, "Sable", 3)
        side = d.cross(Vector((0, 0, 1))).normalized()
        quad(bm, [p, p + side * 0.4 + d * 0.1, p + d * 0.2, p - side * 0.4 + d * 0.1], "Sable")
    ball(bm, (-0.3, -0.7, 10.2), 0.13, "Gall", seg=6, rings=4)
    ball(bm, (0.3, -0.7, 10.2), 0.13, "Gall", seg=6, rings=4)
    player(bm, 3.5, -3.5)


def glass_stag(bm):
    from assets.creatures.common import wild
    sub = creature(wild.STAG, "HairGrey", "Charcoal")
    merge(bm, sub, scale=1.7)
    rng = random.Random(8)
    for s in (1, -1):                                   # the chandelier of moon crystal
        branch(bm, Vector((s * 0.1, -1.1, 2.75)), Vector((s * 0.7, 0.2, 1)).normalized(), 1.6, 4, 0.12, "Moonlight",
               spread=0.6)
    for k in range(10):
        a = k * 0.63
        ball(bm, (1.5 * math.cos(a), -1.0 + 0.8 * math.sin(a), 6.0 + (k % 3) * 0.6), 0.12, "Moonlight", seg=6, rings=4)
    rnd = random.Random(9)
    for k in range(10):                                 # crystal growing out of the ground where it walked
        x, y = rnd.uniform(-5, 5), rnd.uniform(-4, 6)
        cone(bm, (x, y, 0), (x, y, rnd.uniform(0.5, 1.4)), 0.2, 0.0, "Moonlight", 5)
    player(bm, 3.0, -4.0)


def walking_statue(bm):
    f = figure(bm, (0, 0, 0), 26, "Ashgrey", "Ashgrey", hunch=0.1,
               arms=[((0.1, -0.22, -0.02), (0.08, -0.48, 0.02)), ((-0.04, 0.0, -0.22), (-0.03, 0.02, -0.42))],
               head=True, thin=1.15)
    hand = f["hands"][0]
    box(bm, hand + Vector((0, -2.5, 0.6)), (8, 8, 1.0), "Chalk")                # the palm held out: the arena
    for k in range(4):
        box(bm, hand + Vector((-2.8 + k * 1.9, -6.6, 2.0)), (1.4, 1.4, 3.0), "Chalk")
    box(bm, f["head"] + Vector((0.8, -0.6, 1.2)), (1.6, 2.2, 1.6), "Moss")       # moss on the broken crown
    for k in range(6):                                  # rubble falling from it
        box(bm, (k * 2.3 - 6, -3 - k % 2, 0.5), (1.2, 1, 1), "Ashgrey", rot_z=k)
    box(bm, f["chest"] + Vector((-2.0, -2.1, 0.5)), (2.5, 0.4, 4.0), "Sable", rot_z=0.4)   # a great crack
    player(bm, hand.x, hand.y - 2.5, hand.z + 1.1)


def throat(bm):
    disc(bm, (0, 0, 0.02), 10, "Obsidian", 28)
    lathe(bm, (0, 0, -3), [(0, 0), (2.6, 3.2), (3.0, 4.2), (3.05, 4.6)], "BlackBile", seg=24)   # the pit
    for ring, r in enumerate((4.0, 3.2, 2.4, 1.6)):
        n = 18 - ring * 3
        for k in range(n):
            a = 2 * math.pi * k / n + ring * 0.2
            p = Vector((r * math.cos(a), r * math.sin(a), 0.05 - ring * 0.4))
            cone(bm, p, p * 0.78 + Vector((0, 0, 0.55)), 0.2, 0.0, "SaintBone", 5)
    body = [(7, 4, -1), (7.5, 4, 4), (5, 2.5, 9), (1, 0, 11), (-1.5, -1.8, 9.5)]
    tube(bm, body, [1.7, 1.8, 1.8, 1.7, 1.9], "Peat", 12)
    for j in range(7):                                  # gill holes in a row down its side
        p = Vector(body[1]).lerp(Vector(body[2]), j / 7) + Vector((1.5, -0.9, 0))
        ball(bm, p, 0.22, "Sable", seg=6, rings=4)
    mouth = Vector((-2.0, -2.3, 8.6))                  # the sucker disc, facing down at you
    disc(bm, tuple(mouth), 1.9, "Blood", 20)
    for ring, r in enumerate((1.6, 1.1, 0.6)):
        for k in range(14 - ring * 4):
            a = 2 * math.pi * k / (14 - ring * 4)
            p = mouth + Vector((r * math.cos(a), r * math.sin(a), -0.02))
            cone(bm, p, p + Vector((0, 0, -0.45)), 0.13, 0.0, "SaintBone", 4)
    player(bm, 4.5, -6.0)


def mother_of_spores(bm):
    rnd = random.Random(10)
    lathe(bm, (0, 0, 0), [(0, 1.8), (0.5, 1.4), (3, 1.1), (6, 1.0), (7.6, 1.3)], "CandleWax", seg=14)
    lathe(bm, (0, 0, 7.4), [(0, 1.3), (0.2, 5.0), (0.5, 6.8), (1.0, 7.2), (1.8, 6.9), (2.8, 5.6), (3.6, 3.2), (3.9, 0)],
          "Russet", seg=20)
    for k in range(20):                                 # gills underneath
        a = 2 * math.pi * k / 20
        quad(bm, [(1.4 * math.cos(a), 1.4 * math.sin(a), 7.5), (6.8 * math.cos(a), 6.8 * math.sin(a), 7.75),
                  (6.8 * math.cos(a), 6.8 * math.sin(a), 7.35), (1.4 * math.cos(a), 1.4 * math.sin(a), 7.0)], "Dust")
    for k in range(10):                                 # the husks of those it took, hanging on threads
        a = k * 2.399
        r = 2.4 + (k % 4) * 1.0
        x, y = r * math.cos(a), r * math.sin(a)
        drop = 1.5 + (k % 3) * 0.8
        tube(bm, [(x, y, 7.4), (x, y, 7.4 - drop)], 0.025, "Chalk", 4)
        figure(bm, (x, y, 7.4 - drop - 1.75), 1.75, "Dust", "Dust", arms=[((0.04, 0.0, -0.22), (0.03, 0.02, -0.42))], thin=0.8)
    for k in range(18):
        a = rnd.uniform(0, 6.28)
        r = rnd.uniform(5, 9)
        x, y = r * math.cos(a), r * math.sin(a)
        lathe(bm, (x, y, 0), [(0, 0.15), (0.5, 0.12), (0.6, 0.5), (0.9, 0)], "Russet", seg=8, mats=["CandleWax", "CandleWax", "Russet"])
    player(bm, 4.5, -5.0)


def rat_king(bm):
    from assets.creatures.common import rat
    for k in range(12):                                 # a dozen rats facing out, tails knotted in the middle
        a = 2 * math.pi * k / 12
        sub = creature(rat.RAT, "Peat", "Sable")
        merge(bm, sub, loc=(0.62 * math.cos(a), 0.62 * math.sin(a), 0.05), rot=a + math.pi / 2, scale=0.75)
    ball(bm, (0, 0, 0.18), 0.28, "Skin2", s=(1, 1, 0.7), seg=10, rings=6)       # the knot
    for k in range(6):
        a = 2 * math.pi * k / 6
        tube(bm, [(0.25 * math.cos(a), 0.25 * math.sin(a), 0.15), (0.05 * math.cos(a + 1), 0.05 * math.sin(a + 1), 0.32),
                  (-0.25 * math.cos(a), -0.25 * math.sin(a), 0.15)], 0.03, "Skin2", 4)
    for k in range(10):
        ball(bm, (1.4 * math.cos(k), 1.4 * math.sin(k), 0.03), 0.12, "SaintBone", s=(1.5, 0.6, 0.4), seg=6, rings=4)
    player(bm, 2.2, -1.6)


def the_field(bm):
    rnd = random.Random(11)
    disc(bm, (0, 0, 0.02), 13, "Wheat", 28)
    for k in range(220):
        x, y = rnd.uniform(-12, 12), rnd.uniform(-12, 12)
        if x * x + y * y < 140:
            cone(bm, (x, y, 0), (x, y, 1.0 + rnd.uniform(0, 0.4)), 0.07, 0.0, "Wheat", 3)
    for k in range(12):
        x, y = rnd.uniform(-9, 9), rnd.uniform(-9, 9)
        tall = k == 5
        h = 3.8 if tall else 2.5
        tube(bm, [(x, y, 0), (x, y, h)], 0.06, "Peat", 4)
        lathe(bm, (x, y, h - 1.3), [(0, 0.45), (0.5, 0.3), (1.1, 0.25), (1.2, 0.1)], "Dust", seg=8)   # a straw coat
        ball(bm, (x, y, h + 0.05), 0.26, "Wheat", s=(1, 1, 1.15), seg=8, rings=6)   # a sack head
        lathe(bm, (x, y, h + 0.2), [(0, 0.55), (0.05, 0.55), (0.08, 0.2), (0.4, 0.16), (0.45, 0)], "Peat", seg=10)   # hat
        arm = 2.6 if tall else 0.7
        for s in (1, -1):
            tube(bm, [(x + s * 0.25, y, h - 0.35), (x + s * 0.5, y - 0.1, h - 0.35 - arm * 0.5), (x + s * 0.55, y - 0.25, h - 0.35 - arm)],
                 [0.08, 0.06, 0.04], "Dust", 4)
            if tall:
                tube(bm, [(x + s * 0.55, y - 0.25, h - 0.35 - arm), (x + s * 1.2, y - 0.6, h - 0.2 - arm), (x + s * 1.3, y - 1.1, h - 0.6 - arm)],
                     [0.03, 0.03, 0.01], "Iron", 4)       # sickles
        if tall:
            for xe in (-0.09, 0.09):
                ball(bm, (x + xe, y - 0.24, h + 0.08), 0.04, "Ember", seg=6, rings=4)
    player(bm, 0.0, -11.0)


def candle_abbess(bm):
    disc(bm, (0, 0, 0.03), 3.8, "CandleWax", 24)
    lathe(bm, (0, 0, 0), [(0, 1.7), (0.3, 1.4), (1.6, 1.15), (3.2, 0.95), (4.2, 0.85), (4.6, 0.55)], "Sable", seg=14)   # habit
    lathe(bm, (0, 0, 4.4), [(0, 0.7), (0.3, 0.75), (0.8, 0.62), (1.3, 0.45)], "CandleWax", seg=12)   # wimple and wax face
    lathe(bm, (0, 0.2, 4.4), [(0, 1.0), (0.6, 0.9), (1.2, 0.8), (1.6, 0.6), (1.75, 0)], "Sable", seg=12, sy=1.1)   # veil
    for k in range(9):                                  # a crown of candles, burning
        a = math.pi * (0.1 + 0.8 * k / 8) + math.pi
        x, y = 0.55 * math.cos(a), 0.55 * math.sin(a) - 0.05
        hgt = 0.6 + 0.3 * math.sin(k * 1.3)
        tube(bm, [(x, y, 5.6), (x, y, 5.6 + hgt)], 0.09, "CandleWax", 6)
        cone(bm, (x, y, 5.6 + hgt), (x, y, 6.05 + hgt), 0.08, 0.0, "Ember", 4)
    for k in range(9):                                  # wax running down the habit in runnels of different length
        a = 2 * math.pi * k / 9 + 0.3
        top = 4.3 - (k % 3) * 0.5
        length = 1.0 + (k * 0.77 % 1.0) * 1.6
        r0 = 0.82 + 0.1 * (4.3 - top)
        tube(bm, [(r0 * math.cos(a), r0 * math.sin(a), top), ((r0 + 0.12) * math.cos(a), (r0 + 0.12) * math.sin(a), top - length * 0.6),
                  ((r0 + 0.16) * math.cos(a), (r0 + 0.16) * math.sin(a), top - length)], [0.1, 0.08, 0.11], "CandleWax", 5)
    tube(bm, [(1.4, -0.7, 0.2), (1.5, -0.8, 7.8)], 0.07, "Iron", 5)            # the snuffer's pole
    lathe(bm, (1.5, -0.8, 7.6), [(0, 0.5), (0.4, 0.3), (0.9, 0.05)], "Iron", seg=10)
    for s in (1, -1):
        tube(bm, [(s * 0.8, -0.2, 3.9), (s * 1.2, -0.6, 3.0), (s * 1.4, -0.8, 3.4)], 0.13, "Sable", 5)
        ball(bm, (s * 1.45, -0.8, 3.45), 0.15, "CandleWax", seg=6, rings=4)
    player(bm, -3.5, -3.5)


def headless_rider(bm):
    from assets.creatures.common import wild
    spec = dict(wild.STAG)
    spec["ear"] = dict(spec["ear"])
    sub = creature(spec, "Sable", "Charcoal")
    merge(bm, sub, scale=1.55)
    for k in range(8):                                  # a ragged mane
        t = k / 7
        p = Vector((0, -0.65, 1.95)).lerp(Vector((0, -0.95, 2.6)), t) * 1.0
        box(bm, (0, -0.6 - t * 0.5, 1.9 + t * 0.6), (0.06, 0.25, 0.5), "Charcoal", rot_z=0.0)
    f = figure(bm, (0, 0.1, 1.9), 2.0, "Iron", "Iron", head=False, legs=False,
               arms=[((0.08, -0.1, -0.15), (0.12, -0.3, -0.05)), ((-0.1, -0.1, -0.1), (-0.25, -0.2, 0.25))])
    for s in (1, -1):
        tube(bm, [(s * 0.2, 0.1, 2.9), (s * 0.45, -0.1, 2.4), (s * 0.4, -0.2, 1.7)], 0.1, "Iron", 6)
    lathe(bm, (0, 0.2, 1.9), [(0, 0.5), (0.8, 0.45), (1.4, 0.3), (1.6, 0)], "SynodRed", seg=10)   # a torn red cloak
    h = f["hands"][1] + Vector((0, 0, 0.35))            # its head carried high as a lantern
    ball(bm, h, 0.28, "Ember", seg=8, rings=6)
    for k in range(6):
        a = 2 * math.pi * k / 6
        tube(bm, [h + Vector((0.32 * math.cos(a), 0.32 * math.sin(a), -0.35)), h + Vector((0.32 * math.cos(a), 0.32 * math.sin(a), 0.35))],
             0.02, "Iron", 4)
    tube(bm, [f["hands"][0], f["hands"][0] + Vector((0.2, -1.6, -0.2))], 0.05, "Steel", 4)   # a blade
    player(bm, 3.0, -4.0)


def siege_snail(bm):
    foot = [(0, 9, 0.6), (0, 4, 0.9), (0, -1, 1.2), (0, -5, 2.4), (0, -7.5, 4.0)]
    tube(bm, foot, [1.2, 2.6, 3.0, 2.4, 1.8], "Phlegm", 12)
    for s in (1, -1):
        tube(bm, [(s * 0.8, -8.0, 5.0), (s * 1.6, -9.5, 8.0)], [0.25, 0.15], "Phlegm", 6)
        ball(bm, (s * 1.6, -9.6, 8.2), 0.4, "Sable", seg=8, rings=5)
    rnd = random.Random(12)
    for k in range(30):                                 # the shell: a ruined tower coiled into a spiral
        t = k / 29
        a = t * 3.2 * math.pi
        r = 5.5 * (1 - t * 0.75)
        z = 3.5 + t * 9 + 3 * math.sin(a) * (1 - t) * 0.5
        c = (r * math.cos(a) * 0.5, 2.5 + r * math.sin(a), z + r * 0.4 * math.sin(a))
        box(bm, c, (2.6 * (1 - t * 0.5), 2.4 * (1 - t * 0.5), 2.2 * (1 - t * 0.5)), "Ashgrey" if k % 3 else "Chalk", rot_z=a)
    for k in range(5):                                  # crenellations on its crown, a banner
        box(bm, (0.8 * math.cos(k * 1.25), 2.5 + 0.8 * math.sin(k * 1.25), 13.6), (0.5, 0.5, 0.8), "Ashgrey")
    tube(bm, [(0, 2.5, 13.5), (0, 2.5, 17)], 0.06, "Peat", 4)
    quad(bm, [(0, 2.5, 16.9), (0, 0.5, 16.6), (0, 0.5, 15.8), (0, 2.5, 16.0)], "SynodRed")
    for k in range(5):
        box(bm, (rnd.uniform(-2, 2), rnd.uniform(-1, 5), rnd.uniform(5, 11)), (0.4, 0.1, 0.7), "Sable")   # arrow slits
    for k in range(10):                                 # the slime trail behind it
        disc(bm, (rnd.uniform(-1.5, 1.5), 10 + k * 1.3, 0.03), 1.6, "Phlegm", 12)
    player(bm, 4.5, -8.0)


def orrery(bm):
    """The Orrery (the surface people): a walking observatory of brass rings round one great lens."""
    for k in range(3):                                  # three tall legs
        a = 2 * math.pi * k / 3 + math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0))
        tube(bm, [Vector((0, 0, 9.5)) + d * 0.6, Vector((0, 0, 7)) + d * 4.0, d * 6.0], [0.25, 0.2, 0.15], "BronzeShade", 6)
        cone(bm, d * 6.0 + Vector((0, 0, 0.6)), d * 6.0, 0.4, 0.05, "Iron", 6)
    c = Vector((0, 0, 11))
    for k, (r, tilt) in enumerate(((3.8, 0.0), (3.4, 1.1), (3.0, 2.2), (2.6, 0.6))):   # the armillary rings
        pts = []
        for j in range(25):
            t = 2 * math.pi * j / 24
            v = Vector((r * math.cos(t), r * math.sin(t), 0))
            v = Matrix.Rotation(tilt, 3, "X") @ Matrix.Rotation(k * 0.7, 3, "Z") @ v
            pts.append(c + v)
        tube(bm, pts, 0.14, "Bronze" if k % 2 else "Gold", 6, caps=False)
    ball(bm, c, 1.5, "Bronze", seg=14, rings=10)
    ball(bm, c + Vector((0, -1.3, 0.1)), 0.9, "Moonlight", s=(1, 0.4, 1))       # the great lens: its eye
    for k, (r, p) in enumerate(((3.8, 0.5), (3.4, 2.4), (3.0, 4.0))):          # planets riding the rings
        ball(bm, c + Vector((r * math.cos(p), r * math.sin(p) * 0.4, r * math.sin(p) * 0.6)), 0.4, ["Ember", "Ice", "Verdigris"][k], seg=8, rings=6)
    tube(bm, [c + Vector((0, 1, 1)), c + Vector((0, 2, 5))], [0.5, 0.3], "BronzeShade", 8)   # a telescope barrel
    player(bm, 3.5, -4.5)


def willow_widow(bm):
    """The Willow Widow: a weeping willow whose hanging boughs are long arms that end in hands."""
    rnd = random.Random(13)
    disc(bm, (0, 0, 0.02), 11, "Fen", 24)
    tube(bm, [(0, 0, 0), (0.4, 0, 4), (-0.2, 0, 7), (0, 0, 9)], [1.6, 1.2, 1.0, 0.9], "Bark" if "Bark" in COLS else "Peat", 10)
    ball(bm, (0, -0.95, 5.5), 0.5, "Sable", s=(0.9, 0.3, 1.3))                 # a face in the bark: hollow eyes, mouth
    for x in (-0.45, 0.45):
        ball(bm, (x, -1.0, 6.6), 0.22, "Sable", s=(1, 0.3, 1))
    ball(bm, (0, 0, 9.5), 4.0, "DarkLeaves", s=(1, 1, 0.55), seg=14, rings=8)
    for k in range(26):                                 # the boughs: arms hanging to the ground
        a = k * 2.399
        r = 1.5 + (k % 5) * 0.65
        top = Vector((r * math.cos(a), r * math.sin(a), 9.0))
        low = top * 1.25 + Vector((0, 0, -7.6 - (k % 3) * 0.6))
        mid = top.lerp(low, 0.5) + Vector((math.cos(a) * 0.8, math.sin(a) * 0.8, 0))
        tube(bm, [top, mid, low], [0.12, 0.09, 0.06], "Leaf3", 5)
        hand = low
        for j in range(4):
            fa = a + (j - 1.5) * 0.35
            tube(bm, [hand, hand + Vector((0.3 * math.cos(fa), 0.3 * math.sin(fa), -0.45))], [0.05, 0.015], "Skin4", 4)
    player(bm, 3.5, -5.5)


def drowned_choir(bm):
    """The Drowned Choir: a ring of the drowned standing waist-deep in black water round a sunken bell,
    singing; the song is the attack, and the water rises with it."""
    disc(bm, (0, 0, 0.02), 11, "Sea", 28)
    lathe(bm, (0, 0, -1.2), [(0, 1.8), (0.8, 1.4), (1.6, 1.1), (2.2, 0.8), (2.5, 0)], "Verdigris", seg=14)   # the bell's crown
    for k in range(9):
        a = 2 * math.pi * k / 9 + 0.2
        x, y = 5.5 * math.cos(a), 5.5 * math.sin(a)
        f = figure(bm, (x, y, -1.4), 3.4, "Verdigris", "Phlegm", hunch=0.05, head=True, head_mat="Phlegm",
                   arms=[((0.08, 0.0, 0.15), (0.12, -0.1, 0.38))], thin=0.75)
        ball(bm, f["head"] + Vector((0, -0.25, -0.05)), 0.09, "Sable", s=(1, 0.4, 1.4), seg=6, rings=4)   # the open mouth
        lathe(bm, (x, y, -0.2), [(0, 0.55), (0.3, 0.42)], "Ice", seg=10)       # ripples round the waist
    player(bm, 0.0, -9.5)


def olm(bm):
    """The Olm: the dragon's child of the folk tales, a vast pale blind cave salamander in an underground
    lake, its gills blooming red either side of its head, legs too small for it."""
    disc(bm, (0, 0, 0.02), 13, "Water", 32)
    body = [(0, 13, 0.6), (0.5, 9, 0.9), (0, 5, 1.3), (-0.4, 1, 1.6), (0, -2.5, 1.8), (0, -4.5, 1.9)]
    tube(bm, body, [0.4, 1.1, 1.6, 1.8, 1.7, 1.3], "Skin2", 12)
    ball(bm, (0, -6.2, 1.9), 1.4, "Skin2", s=(1.15, 1.6, 0.6), seg=14, rings=8)   # a long flat snout, no eyes
    for s_ in (1, -1):                                 # three branching gills each side, blood red
        for j in range(3):
            root = Vector((s_ * 1.3, -4.6 + j * 0.6, 2.4 - j * 0.15))
            branch(bm, root, Vector((s_ * 0.8, 0.4, 0.6 - j * 0.3)).normalized(), 1.9, 3, 0.22, "Blood", spread=0.45)
        for fy in (-1.5, 5.5):                         # small, weak legs
            tube(bm, [(s_ * 1.4, fy, 1.4), (s_ * 2.3, fy - 0.3, 0.8), (s_ * 2.6, fy - 0.5, 0.05)], [0.3, 0.22, 0.15], "Skin2", 6)
    for x, y in ((-6, 4), (5, -4), (7, 6), (-8, -6)):  # stalagmites round the lake
        cone(bm, (x, y, 0), (x, y, 3 + abs(x) * 0.2), 0.8, 0.0, "Ashgrey", 6)
    player(bm, 4.0, -9.5)


def antlion(bm):
    """The Antlion: the arena is its pit, a funnel of sand sliding toward the jaws at the bottom."""
    lathe(bm, (0, 0, -9), [(0, 0.0), (0.6, 1.6), (2.4, 3.6), (4.4, 6.0), (6.4, 8.6), (7.8, 11.0), (8.6, 13.4), (9.0, 15.0)],
          "Sand", seg=28, cap_top=False, mats=["Ochre", "Dust", "Sand", "Dust", "Sand", "Dust", "Sand"])
    for k in range(10):                                # the rim's dunes
        a = 2 * math.pi * k / 10
        ball(bm, (15.5 * math.cos(a), 15.5 * math.sin(a), 0), 2.0, "Sand", s=(1.6, 1, 0.5), seg=8, rings=4)
    box(bm, (8.5, -6.5, -2.6), (2.4, 1.3, 1.1), "Peat", rot_z=0.6, d=(0.4, -0.3, 1))   # a caravan's cart, sliding in
    for k in range(3):
        ball(bm, (3 - k * 2.5, 6 + k, -5.5 + k * 1.2), 0.5, "SaintBone", s=(1.4, 0.6, 0.5), seg=6, rings=4)   # what it ate
    for s_ in (1, -1):                                 # the jaws: two long sickles open out of the sand
        tube(bm, [(s_ * 0.7, -0.4, -8.4), (s_ * 2.6, -1.2, -6.0), (s_ * 3.4, -3.6, -4.4), (s_ * 1.8, -5.8, -3.8)],
             [0.55, 0.45, 0.3, 0.05], "Sable", 8)
    ball(bm, (0, 0.6, -8.3), 1.9, "Peat", s=(1.1, 1.0, 0.6), seg=12, rings=6)   # the head, barely out
    for x in (-0.8, 0.8):
        ball(bm, (x, -0.9, -7.5), 0.2, "Ember", seg=6, rings=4)
    rnd = random.Random(14)
    for k in range(18):                                # sand running down into it
        a = rnd.uniform(0, 6.28)
        r = rnd.uniform(4, 12)
        pass
    player(bm, 7.0, -9.0, -1.8)


def ember_axolotl(bm):
    """The Ember Axolotl (House Gall): a pale smiling salamander in a lava pool, its gill frills burning."""
    disc(bm, (0, 0, 0.02), 12, "Obsidian", 28)
    disc(bm, (0, 1, 0.06), 7, "Ember", 24)
    body = [(0, 9.5, 0.6), (0, 6, 0.9), (0, 3, 1.3), (0, 0, 1.6), (0, -2, 1.8)]
    tube(bm, body, [0.25, 1.0, 1.5, 1.7, 1.6], "Skin2", 12)
    quad(bm, [(0, 2, 2.9), (0, 9.6, 1.2), (0, 9.6, 0.3), (0, 2, 1.5)], "Skin2")      # the tail fin
    ball(bm, (0, -3.6, 2.3), 2.0, "Skin2", s=(1.35, 1.0, 0.8), seg=14, rings=8)   # a broad round head
    tube(bm, [(-1.4, -5.15, 1.95), (-0.7, -5.6, 1.75), (0, -5.75, 1.7), (0.7, -5.6, 1.75), (1.4, -5.15, 1.95)], 0.07,
         "Sable", 4)                                       # the smile
    for x in (-1.25, 1.25):
        ball(bm, (x, -4.9, 2.9), 0.22, "Sable", seg=8, rings=5)
    for s_ in (1, -1):                                     # three feathery gills each side, burning
        for j in range(3):
            root = Vector((s_ * 2.3, -3.4 + j * 0.75, 2.5 + 0.55 * (1 - j)))
            d = Vector((s_ * 0.55, 0.45 + j * 0.15, 0.7 - j * 0.3)).normalized()
            branch(bm, root, d, 1.5, 3, 0.22, "Russet", spread=0.5)    # a feathery gill, like the olm's
            for q in range(6):                             # flames licking off its branches
                t = q / 5
                p = root + d * (1.5 + 1.6 * t) + Vector((s_ * 0.3 * math.sin(q * 2.1), 0.2 * math.cos(q * 1.7), 0.2))
                cone(bm, p, p + Vector((0, 0, 0.6 + 0.3 * (q % 2))), 0.16, 0.0, "Ember", 5)
        for fy in (-1.0, 5.0):
            tube(bm, [(s_ * 1.3, fy, 1.2), (s_ * 2.3, fy, 0.6), (s_ * 2.6, fy - 0.4, 0.1)], [0.35, 0.28, 0.22], "Skin2", 6)
    for x, y in ((-6, 6), (6, -2), (-7, -5)):
        cone(bm, (x, y, 0), (x, y, 2.5), 1.2, 0.2, "Cinder", 6)
    player(bm, 4.0, -9.0)


def mara(bm):
    """The Mara: a long-limbed hag crouched on the chest of a giant sleeper; the arena is the sleeper."""
    box(bm, (0, 2, 0.4), (10, 22, 0.8), "Peat")                                # the bed
    box(bm, (0, -7.5, 1.2), (8, 4, 1.2), "Snow")                               # the pillow
    tube(bm, [(0, 12, 1.6), (0, 6, 2.4), (0, 0, 2.8), (0, -4.6, 2.6)], [2.4, 3.6, 3.8, 3.2], "Wheat", 12)   # under the sheet
    ball(bm, (0, -7.4, 3.4), 2.4, "Skin2", s=(1, 0.95, 0.75), seg=14, rings=8)  # the sleeper's face, turned up
    for x in (-0.85, 0.85):
        box(bm, (x, -8.6, 4.85), (0.75, 0.25, 0.08), "Sable")                  # closed eyes
    box(bm, (0, -9.2, 4.2), (0.9, 0.3, 0.1), "Blood")                          # the mouth, a little open
    hip = Vector((0, -2.0, 6.4))                           # the mara, crouched on the chest, leaning to the face
    f = figure(bm, (0, -2.0, 4.3), 3.6, "Charcoal", "Charcoal", hunch=0.9, legs=False,
               arms=[((0.12, -0.35, -0.25), (0.08, -0.85, -0.55))], thin=0.6, head=True)
    for s_ in (1, -1):                                     # knees drawn up past its shoulders
        tube(bm, [hip + Vector((s_ * 0.3, 0.2, -0.2)), hip + Vector((s_ * 1.0, -0.5, 1.4)), hip + Vector((s_ * 0.8, -0.2, -1.6))],
             [0.13, 0.1, 0.07], "Charcoal", 5)
        for j in range(3):                                 # long fingers on the sleeper's face
            hand = f["hands"][0 if s_ > 0 else 1]
            tube(bm, [hand, hand + Vector((s_ * (0.1 + j * 0.15), -0.45, -0.25))], [0.04, 0.01], "Charcoal", 4)
    h = f["head"]
    for k in range(12):                                    # long lank hair hanging over its face, down to the sleeper
        a = math.pi * (0.15 + 0.7 * k / 11) + math.pi
        top = h + Vector((0.22 * math.cos(a), 0.18 * math.sin(a) + 0.05, 0.12))
        low = top + Vector((0.1 * math.cos(a), -0.35, -1.4 - 0.4 * (k % 3)))
        tube(bm, [top, (top + low) / 2 + Vector((0, -0.15, 0)), low], [0.05, 0.04, 0.015], "HairGrey", 4)
    for xe in (-0.1, 0.1):
        ball(bm, h + Vector((xe, -0.24, 0.02)), 0.05, "Gall", seg=6, rings=4)
    player(bm, 3.0, 4.0, 5.2)


def keyholder(bm):
    """The Keyholder: a blind giant kneeling before the dwarves' seal as its living lock; its chest is a
    keyhole, and light from what the seal holds shines out through it."""
    box(bm, (0, 3.2, 8), (18, 2, 16), "Ashgrey")                               # the seal door behind
    for k in range(10):
        a = 2 * math.pi * k / 10
        box(bm, (7.0 * math.cos(a), 2.1, 8.6 + 7.0 * math.sin(a)), (1.2, 0.4, 1.2), "Bronze")   # its ring of studs
    tube(bm, [(0, 0.6, 2.4), (0, 0.2, 5.6), (0, -0.3, 8.4), (0, -0.6, 9.4)], [2.0, 2.5, 2.3, 1.0], "Moor", 10)   # torso, kneeling
    for s_ in (1, -1):
        tube(bm, [(s_ * 1.1, 0.6, 2.6), (s_ * 1.4, -2.2, 1.0), (s_ * 1.4, 0.8, 0.5)], [1.0, 0.8, 0.7], "Moor", 8)   # thighs, shins
        tube(bm, [(s_ * 2.2, -0.2, 8.0), (s_ * 3.0, 0.6, 5.2), (s_ * 2.6, 1.6, 2.8)], [0.8, 0.65, 0.55], "Moor", 8)   # arms back
        ball(bm, (s_ * 2.6, 1.7, 2.6), 0.6, "HairGrey", seg=8, rings=6)
        for j in range(8):                                 # chains from its wrists into the door
            p = Vector((s_ * 2.6, 1.7, 2.6)).lerp(Vector((s_ * 4.5, 2.2, 1.2)), (j + 0.5) / 8)
            box(bm, p, (0.25, 0.5, 0.12), "Iron", d=(s_ * 1.9, 0.5, -1.4), rot_z=j * 1.57)
    ball(bm, (0, -1.6, 10.4), 1.3, "HairGrey", s=(0.9, 1.0, 1.05), seg=12, rings=8)   # head bowed
    box(bm, (0, -2.75, 10.6), (2.0, 0.4, 0.55), "Iron")                       # a band over its eyes
    lathe(bm, (0, -1.9, 4.2), [(0, 1.5), (3.6, 1.5)], "Bronze", seg=12, sy=0.25)   # the escutcheon on its chest
    ball(bm, (0, -2.35, 6.7), 0.75, "Ember", s=(1, 0.3, 1))                   # the keyhole, lit from within
    box(bm, (0, -2.38, 5.5), (0.7, 0.2, 2.0), "Ember")
    player(bm, 4.5, -6.0)



CONCEPTS = [
    ("The Lure", lure, 30), ("The Moon Jelly", moon_jelly, 26), ("The Old Tortoise", tortoise, 52),
    ("The Heron of the Fall", heron, 30), ("The Harvestman", harvestman, 40), ("The Bell-Ringer", bell_ringer, 18),
    ("The Unquiet Saint", unquiet_saint, 18), ("The Collector", tithe_collector, 18), ("The Fused", fused, 18),
    ("The Rejected Dragon", rejected_dragon, 26), ("The Bog-Giant's Hand", mire_hand, 30),
    ("The Long Pilgrim", long_pilgrim, 16), ("The Murder", murder, 20), ("The Mother-of-Spores", mother_of_spores, 26),
    ("The Rat King", rat_king, 10), ("The Field", the_field, 30), ("The Candle Abbess", candle_abbess, 14),
    ("The Orrery", orrery, 18), ("The Willow Widow", willow_widow, 24), ("The Drowned Choir", drowned_choir, 26),
    ("The Olm", olm, 30), ("The Antlion", antlion, 34), ("The Ember Axolotl", ember_axolotl, 28),
    ("The Mara", mara, 26), ("The Keyholder", keyholder, 20),
]

ROW = 5
ORIGIN = (0.0, 150.0)
ROW_DEPTH = 64.0


def label(text, x, y, size=1.6):
    import bpy
    cu = bpy.data.curves.new("BossLabel_" + text, "FONT")
    cu.body = text
    cu.size = size
    cu.align_x = "CENTER"
    ob = bpy.data.objects.new("BossLabel_" + text, cu)
    ob.location = (x, y, 0.05)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def run(only=None):
    import bpy
    import rth_art as A
    for o in [o for o in bpy.data.objects if o.name.startswith(("Boss_", "BossLabel_"))]:
        if only and not any(n in o.name for n in only):
            continue
        d = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if isinstance(d, bpy.types.Mesh):
            bpy.data.meshes.remove(d)
        elif d is not None:
            bpy.data.curves.remove(d)
    for name, (col, strength) in (("Moonlight", ((0.56, 0.69, 0.86, 1), 2.0)), ("Ember", ((0.9, 0.4, 0.15, 1), 3.0))):
        m = bpy.data.materials.get(name)
        if m and m.use_nodes and m.node_tree.nodes.get("Principled BSDF"):
            b = m.node_tree.nodes["Principled BSDF"]
            b.inputs["Emission Color"].default_value = col
            b.inputs["Emission Strength"].default_value = strength
    places = {}
    x, row = ORIGIN[0], 0
    for i, (name, fn, width) in enumerate(CONCEPTS):
        if i and i % ROW == 0:
            row += 1
            x = ORIGIN[0]
        cx, cy = x + width / 2, ORIGIN[1] + row * ROW_DEPTH
        places[name] = (cx, cy, width)
        x += width + 6
        if only and name not in only:
            continue
        bm = bmesh.new()
        fn(bm)
        ob = A.mesh_from("Boss_" + name, bm, COLS, coll="Bosses", smooth_angle=30)
        ob.location = (cx, cy, 0)
        label(name, cx, cy - width * 0.55 - 4)
    return places
