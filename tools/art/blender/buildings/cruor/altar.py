"""The Altar of Martyrs: the centrepiece of Cruor's cathedral and the heart of Troth's church, for
the dais the cathedral leaves at its east end (cathedral.py; 18 tiles across under a 22-tile nave).

Our reading of images/blood-altar-of-martyrs.jpg and images/troth.png, not a copy of either:
- Troth stands on a tall pedestal in a round pool of blood: a towering dark figure, hooded and
  faceless, crowned and shouldered with iron spikes, its arms open and lowered to the faithful.
- The betrayers' blades are stabbed into its back, hilts out, fanned like a halo: the wound the
  Untroth struck at the dawn of the world (docs/lore.md II). The wound in its chest never closes;
  blood runs down its robe and down four grooves in the pedestal into the pool.
- Five martyrs stand on the dais round the pool, hooded, heads bowed, facing out to the
  congregation as its guardians, hands resting on swords held point down.
- Channels come in from the nave across the dais and down its steps to the pool: the faith's blood
  going to its god. Four iron braziers stand at its corners.

Red is the faith's and the blood's only (docs/regions/cruor-architecture.md): the pool, the
channels, the wound. Stone is the cathedral's (painted CruorStone); the effigy is dark iron.
Front is -Y (towards the nave); turn it to face west when it is set in the cathedral.

    runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\buildings\\cruor\\altar.py")
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, os.path.join(HERE, "..", "..")):
    if p not in sys.path:
        sys.path.insert(0, p)
for m in ("rth_art", "kit"):
    sys.modules.pop(m, None)
import rth_art as A  # noqa: E402
import kit as K  # noqa: E402

MATS = ["Gravestone", "Blackiron", "FountBlood", "Sable", "Steel", "Bronze", "Ember", "CryptFloor", "Iron", "SaintBone",
        "Blood", "Charcoal"]
STONE, IRON, BLOOD, VOID, BLADE, BRONZE, COALS, FLOOR, DARKIRON, MARBLE, EYES, CHAR = range(len(MATS))

TOP = 0.75                 # the dais's top, three steps up
POOL_R, KERB_R = 3.0, 3.3  # the pool, and its kerb
POOL_Z, KERB_Z = 0.9, 1.05
PED_TOP = 3.9              # Troth stands here
CHANNELS = (270, 0, 180)   # degrees: from the nave (front), and from either side
MARTYRS = (45, 90, 135, 225, 315)
MARTYR_R = 3.9


def obj(name, bm, smooth=40):
    A.finish(bm)
    return A.mesh_from(name, bm, MATS, coll="Altar", smooth_angle=smooth)


def turn(bm, verts, deg, at=(0, 0, 0)):
    bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, 'Z'), verts=verts)


def new(bm, before):
    return [v for v in bm.verts if v not in before]


# ---- the dais, the pool, the pedestal, the channels -------------------------------------------

def dais():
    bm = bmesh.new()
    for k, r in enumerate((5.6, 5.0, 4.4)):
        A.prism(bm, 8, r, r, 0.25 * k - 0.05, 0.25 * (k + 1), (0, 0), rot=math.pi / 8, mat_index=STONE, cap_top=True)
    # The kerb round the pool, side by side, and the pool's surface inside it.
    for k in range(8):
        a0, a1 = math.pi / 8 + k * math.pi / 4, math.pi / 8 + (k + 1) * math.pi / 4
        pts = [(r * math.cos(a), r * math.sin(a), z) for a in (a0, a1) for r in (POOL_R, KERB_R) for z in (TOP - 0.01, KERB_Z)]
        A.hull(bm, pts, STONE)
    A.prism(bm, 8, POOL_R, POOL_R, TOP, POOL_Z, (0, 0), rot=math.pi / 8, mat_index=BLOOD, cap_top=True)
    # The pedestal: a plinth step out of the pool, a tapering shaft, a cap.
    A.prism(bm, 8, 1.55, 1.5, TOP, 1.3, (0, 0), rot=math.pi / 8, mat_index=STONE, cap_top=True)
    A.prism(bm, 8, 1.2, 1.0, 1.3, 3.6, (0, 0), rot=math.pi / 8, mat_index=STONE, cap_top=True)
    A.prism(bm, 8, 1.4, 1.4, 3.6, PED_TOP, (0, 0), rot=math.pi / 8, mat_index=STONE, cap_top=True)
    # Four grooves of blood down the shaft, widening into the pool at its foot.
    c = math.cos(math.pi / 8)
    for deg in (0, 90, 180, 270):
        before = set(bm.verts)
        A.hull(bm, [(1.0 * c + 0.015, -0.09, 3.6), (1.0 * c + 0.015, 0.09, 3.6),
                    (1.2 * c + 0.015, -0.12, 1.3), (1.2 * c + 0.015, 0.12, 1.3),
                    (1.0 * c - 0.02, -0.09, 3.6), (1.2 * c - 0.02, 0.12, 1.3)], BLOOD)
        A.hull(bm, [(1.5 * c + 0.01, -0.13, 1.3), (1.5 * c + 0.01, 0.13, 1.3), (1.52 * c + 0.2, -0.3, POOL_Z + 0.01),
                    (1.52 * c + 0.2, 0.3, POOL_Z + 0.01), (1.5 * c - 0.05, 0, 1.3)], BLOOD)
        turn(bm, new(bm, before), deg)
    # The channels: a gutter of stone kerbs with blood in it, from the nave across the floor, down
    # the steps and over the dais to the pool's kerb.
    profile = [(KERB_R, 10.0, 0.01), (5.6, 10.0, 0.01), (5.0, 5.6, 0.26), (4.4, 5.0, 0.51), (KERB_R, 4.4, TOP + 0.01)]
    for deg in CHANNELS:
        before = set(bm.verts)
        for i, (r0, r1, z) in enumerate(profile[1:]):
            A.box(bm, (r0, -0.17, z - 0.03), (r1, 0.17, z + 0.015), BLOOD)
            for y0, y1 in ((-0.23, -0.17), (0.17, 0.23)):
                A.box(bm, (r0, y0, z - 0.03), (r1, y1, z + 0.07), STONE)
        turn(bm, new(bm, before), deg)
    return obj("AltarDais", bm, smooth=20)


# ---- Troth ------------------------------------------------------------------------------------

def troth(z0=PED_TOP, name="AltarTroth", at=(0, 0, 0)):
    """Troth, about six tiles tall: hooded and faceless but for two red eyes, crowned and shouldered
    with iron spikes, its arms open and lowered, the betrayers' blades still in its back and its
    wound still bleeding. Standing at z0: on the altar's pedestal, or (z0 0) wherever the god itself
    comes."""
    r = random.Random(7)
    bm = bmesh.new()
    secs = [(0.0, 1.3, 1.05), (0.35, 1.2, 0.95), (1.5, 0.85, 0.65), (2.5, 0.78, 0.58), (3.4, 1.05, 0.68),
            (3.9, 1.18, 0.62), (4.2, 0.55, 0.5), (4.55, 0.62, 0.62), (5.15, 0.45, 0.48), (5.6, 0.16, 0.2), (5.75, 0, 0)]
    loft = []
    for z, w, d in secs:
        fwd = -0.18 * max(0.0, (z - 4.2) / 1.5)               # the hood leans out over the faithful
        loft.append(((0, fwd, z0 + z), w, d))
    A.loft(bm, loft, sides=10, ref=(0, 1, 0), mats=IRON)
    # The ragged hem: tongues of robe hanging over the pedestal's cap.
    for k in range(12):
        a = 2 * math.pi * k / 12 + r.uniform(-0.1, 0.1)
        base = Vector((math.cos(a) * 1.15, math.sin(a) * 0.95, z0 + 0.3))
        A.spike(bm, base, 0.22, base + Vector((math.cos(a) * 0.25, math.sin(a) * 0.2, -r.uniform(0.35, 0.6))), sides=3, mat=IRON)
    # Spikes: a crown round the hood, three on each shoulder.
    for k in range(9):
        a = math.pi / 2 + 2 * math.pi * k / 9
        base = Vector((math.cos(a) * 0.3, math.sin(a) * 0.32 - 0.15, z0 + 5.25))
        length = (1.1 if k % 2 == 0 else 0.7) * r.uniform(0.85, 1.1)
        A.spike(bm, base, 0.09, base + Vector((math.cos(a) * 0.35, math.sin(a) * 0.3, length)), sides=4, mat=IRON)
    for s in (-1, 1):
        for k, (dx, dz, l) in enumerate(((0.75, 4.0, 1.0), (0.98, 3.85, 0.8), (0.5, 4.05, 0.65))):
            base = Vector((s * dx, 0.05, z0 + dz))
            A.spike(bm, base, 0.12, base + Vector((s * l * 0.55, 0.1, l)), sides=4, mat=IRON)
    # Arms open and lowered, hands turned out to the faithful.
    for s in (-1, 1):
        A.loft(bm, [((s * 0.95, 0.0, z0 + 3.75), 0.3, 0.28), ((s * 1.6, -0.3, z0 + 2.95), 0.24, 0.22),
                    ((s * 2.0, -0.62, z0 + 2.4), 0.19, 0.17), ((s * 2.12, -0.7, z0 + 2.3), 0, 0)],
                   sides=6, ref=(0, 0, 1), mats=IRON)
        A.hull(bm, [(s * 2.0 + dx, -0.72 + dy, z0 + 2.32 + dz) for dx in (-0.14, 0.16) for dy in (-0.05, 0.05)
                    for dz in (0.0, -0.4)] + [(s * 2.12, -0.75, z0 + 1.82)], IRON)
    # The dark under the hood, two red eyes in it; the wound in the chest, and a ribbon of its blood
    # running down the robe to the hem, all laid on the surface (cast from in front of it).
    from mathutils.bvhtree import BVHTree
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()                   # the casts need the faces' normals
    body = BVHTree.FromBMesh(bm)
    A.decal(bm, [(-0.3, -2.0, z0 + 4.55), (0.3, -2.0, z0 + 4.55), (0.26, -2.0, z0 + 5.05), (0, -2.0, z0 + 5.25), (-0.26, -2.0, z0 + 5.05)],
            (0, -1, 0.15), VOID, onto=body, facing=0.2)
    for s in (-1, 1):
        A.decal(bm, [(s * 0.06, -2.0, z0 + 4.86), (s * 0.2, -2.0, z0 + 4.9), (s * 0.19, -2.0, z0 + 4.95), (s * 0.06, -2.0, z0 + 4.93)],
                (0, -1, 0.15), EYES, lift=0.02, onto=body, facing=0.2)
    A.decal(bm, [(-0.13, -2.0, z0 + 3.05), (0.14, -2.0, z0 + 3.1), (0.08, -2.0, z0 + 3.7), (-0.06, -2.0, z0 + 3.75)], (0, -1, 0), EYES,
            onto=body, facing=0.2)
    zs = [3.1, 2.6, 2.1, 1.6, 1.1, 0.6, 0.2]
    for za, zb in zip(zs, zs[1:]):
        wa, wb = 0.07 + 0.03 * (3.1 - za), 0.07 + 0.03 * (3.1 - zb)
        xa, xb = 0.05 * math.sin(za * 3), 0.05 * math.sin(zb * 3)
        A.decal(bm, [(xa - wa, -2.0, z0 + za), (xa + wa, -2.0, z0 + za), (xb + wb, -2.0, z0 + zb), (xb - wb, -2.0, z0 + zb)],
                (0, -1, 0), BLOOD, onto=body, facing=0.2)
    # The betrayers' blades, stabbed into its back and fanned out like a halo, hilts out.
    for k in range(7):
        a = math.radians(-66 + 22 * k)
        d = Vector((math.sin(a), 0.42, math.cos(a))).normalized()
        root = Vector((math.sin(a) * 0.35, 0.25, z0 + 4.0 + math.cos(a) * 0.3))
        guard = root + d * r.uniform(1.6, 2.0)
        side = Vector((math.cos(a), 0, -math.sin(a)))
        A.hull(bm, [root + side * 0.04, root - side * 0.04, guard + side * 0.12, guard - side * 0.12,
                    guard + Vector((0, 0.03, 0)), guard - Vector((0, 0.03, 0))], BLADE)
        A.hull(bm, [guard + side * 0.32 + Vector((0, dy, dz)) for dy in (-0.05, 0.05) for dz in (-0.03, 0.03)] +
               [guard - side * 0.32 + Vector((0, dy, dz)) for dy in (-0.05, 0.05) for dz in (-0.03, 0.03)], BRONZE)
        A.loft(bm, [(guard, 0.045, 0.045), (guard + d * 0.5, 0.045, 0.045)], sides=5, ref=(0, 0, 1), mats=DARKIRON)
        A.loft(bm, [(guard + d * 0.48, 0, 0), (guard + d * 0.55, 0.09, 0.09), (guard + d * 0.68, 0, 0)], sides=6,
               ref=(0, 0, 1), mats=BRONZE)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    return obj(name, bm, smooth=35)


# ---- the martyrs and the braziers ---------------------------------------------------------------

def martyr(seed=1, at=(0, 0, 0), facing=270):
    """A martyr on his plinth: a hooded figure in pale smooth stone, larger than life, head bowed
    deep, hands resting on a sword held point down before him. Faces `facing` degrees (270 is -Y)."""
    r = random.Random(seed)
    bm = bmesh.new()
    A.box(bm, (-0.48, -0.48, TOP - 0.02), (0.48, 0.48, TOP + 0.38), STONE)
    z0 = TOP + 0.38
    secs = [(0.0, 0.46, 0.38), (0.9, 0.36, 0.3), (1.55, 0.44, 0.3), (1.72, 0.22, 0.22), (1.98, 0.27, 0.29),
            (2.28, 0.15, 0.17), (2.4, 0, 0)]
    A.loft(bm, [((0, -0.24 * max(0.0, (z - 1.6) / 0.8), z0 + z), w, d) for z, w, d in secs], sides=8, ref=(0, 1, 0), mats=MARBLE)
    from mathutils.bvhtree import BVHTree
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    A.decal(bm, [(-0.14, -1.0, z0 + 1.82), (0.14, -1.0, z0 + 1.82), (0.12, -1.0, z0 + 2.12), (-0.12, -1.0, z0 + 2.12)], (0, -1, 0),
            VOID, onto=BVHTree.FromBMesh(bm), facing=0.2)
    for s in (-1, 1):
        A.loft(bm, [((s * 0.34, -0.02, z0 + 1.5), 0.11, 0.11), ((s * 0.22, -0.32, z0 + 1.2), 0.1, 0.1),
                    ((s * 0.07, -0.46, z0 + 1.12), 0.09, 0.09)], sides=5, ref=(0, 0, 1), mats=MARBLE)
    tip, guard = Vector((0, -0.5, z0 + 0.04)), Vector((0, -0.5, z0 + 0.95))
    A.hull(bm, [tip, guard + Vector((0.08, 0, 0)), guard - Vector((0.08, 0, 0)), guard + Vector((0, 0.02, 0)),
                guard - Vector((0, 0.02, 0))], MARBLE)
    A.box(bm, (-0.22, -0.54, z0 + 0.95), (0.22, -0.46, z0 + 1.01), MARBLE)
    A.loft(bm, [(guard + Vector((0, 0, 0.06)), 0.035, 0.035), (guard + Vector((0, 0, 0.2)), 0.035, 0.035)], sides=4,
           ref=(1, 0, 0), mats=MARBLE)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ Matrix.Rotation(math.radians(facing - 270), 4, 'Z'), verts=bm.verts)
    return obj("AltarMartyr", bm, smooth=40)


def brazier(at=(0, 0, 0)):
    """An iron brazier on three legs, a wide bowl of coals."""
    bm = bmesh.new()
    for k in range(3):
        a = 2 * math.pi * k / 3
        foot = Vector((math.cos(a) * 0.55, math.sin(a) * 0.55, 0))
        A.loft(bm, [(foot, 0.04, 0.04), (Vector((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.9)), 0.04, 0.04),
                    (Vector((math.cos(a) * 0.42, math.sin(a) * 0.42, 1.2)), 0.04, 0.04)], sides=4, ref=(0, 0, 1), mats=IRON)
    A.prism(bm, 8, 0.25, 0.62, 0.95, 1.3, (0, 0), mat_index=IRON, cap_bottom=True, cap_top=False)
    A.prism(bm, 8, 0.5, 0.5, 1.12, 1.13, (0, 0), mat_index=CHAR, cap_top=True)
    r = random.Random(at[0] * 7 + at[1])
    for k in range(9):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0, 0.38)
        c = Vector((d * math.cos(a), d * math.sin(a), 1.12))
        s = r.uniform(0.09, 0.15)
        A.hull(bm, [c + Vector((s * math.cos(t), s * math.sin(t), 0)) for t in (r.uniform(0, 1), r.uniform(2, 3), r.uniform(4, 5))]
               + [c + Vector((0, 0, s * r.uniform(0.8, 1.4)))], COALS if k % 3 else CHAR)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    return obj("AltarBrazier", bm, smooth=40)


# ---- the preview ------------------------------------------------------------------------------

def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Altar", "Preview"):
            bpy.data.collections.remove(c)
    bm = bmesh.new()
    A.box(bm, (-13, -13, -0.1), (21, 13, 0.0), FLOOR)
    floor = obj("Floor", bm, smooth=None)
    for c in list(floor.users_collection):
        c.objects.unlink(floor)
    A.collection("Preview").objects.link(floor)
    objs = [dais(), troth()]
    for k, deg in enumerate(MARTYRS):
        a = math.radians(deg)
        objs.append(martyr(seed=k + 1, at=(MARTYR_R * math.cos(a), MARTYR_R * math.sin(a), 0), facing=deg))
    for k in range(4):
        a = math.radians(45 + 90 * k)
        objs.append(brazier(at=(7.4 * math.cos(a), 7.4 * math.sin(a), 0)))
    objs.append(troth(z0=0.0, name="Troth", at=(16.0, 0, 0)))       # the god itself, beside its altar
    K.paint(objs + [floor])
    return objs


def rig():
    A.preview_rig(focus=(0, 0, 3.5), distance=30.0, pitch=25, yaw=-20, ground=0, human=True, human_at=(0.0, -7.5))
    A.figures([(-1.5, -8.2, 0), (1.8, -8.0, 0)])
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


objs = build()
rig()
result = A.report(objs)
