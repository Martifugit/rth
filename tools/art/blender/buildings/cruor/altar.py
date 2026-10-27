"""The Altar of Martyrs: the centrepiece of Cruor's cathedral and the heart of Troth's church,
where the faithful honour the fallen martyrs; for the dais the cathedral leaves at its east end
(cathedral.py; 18 tiles across under a 22-tile nave). Troth itself is not on it (the god is
creatures/troth.py).

Our reading of images/blood-altar-of-martyrs.jpg, not a copy of it:
- Three martyrs stand back to back on a tall pedestal in a round pool of blood, larger than life
  in pale stone: hooded, heads up, one hand on the heart (the oath), the other raising a sword up
  and outwards, so the three blades fan out over the pool.
- Faces of the fallen are carved round the pedestal's shaft, eyes closed, mouths open, each
  pouring a stream of blood down the shaft into the pool.
- Channels come in from the nave across the dais and down its steps to the pool: the faith's blood
  going where the church gathers it. Four iron braziers stand at its corners.

Red is the faith's and the blood's only (docs/regions/cruor-architecture.md): the pool, the
channels, the streams. Stone is the cathedral's (painted CruorStone); the martyrs and the faces are
paler, smooth stone. Front is -Y (towards the nave); turn it to face west in the cathedral.

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
PED_TOP = 3.9              # the martyrs stand here
CHANNELS = (270, 0, 180)   # degrees: from the nave (front), and from either side
FACES = (270, 0, 90, 180)  # degrees: the faces of the fallen round the shaft


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
    # The faces of the fallen, each pouring blood into the pool.
    for deg in FACES:
        fallen_face(bm, deg)
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


# ---- the faces, the martyrs and the braziers ---------------------------------------------------

def fallen_face(bm, deg):
    """A face of the fallen carved on the pedestal's shaft, on its side facing `deg`: pale stone, eyes
    closed, mouth open, a stream of blood from the mouth down the shaft and into the pool."""
    c = math.cos(math.pi / 8)

    def out(z):                              # the shaft's face, this far out at height z
        return (1.2 - 0.2 * (z - 1.3) / 2.3) * c

    before = set(bm.verts)
    y = out(2.95)
    A.hull(bm, [p for sx in (-1, 1) for p in ((sx * 0.22, y - 0.02, 3.38), (sx * 0.17, y + 0.15, 3.32),
                                               (sx * 0.25, y - 0.02, 2.88), (sx * 0.2, y + 0.17, 2.92),
                                               (sx * 0.1, y - 0.02, 2.55), (sx * 0.07, y + 0.14, 2.6))]
           + [(0, y + 0.25, 3.05)], MARBLE)
    A.hull(bm, [p for sx in (-1, 1) for p in ((sx * 0.31, y - 0.02, 3.5), (sx * 0.33, y - 0.02, 2.68),
                                               (sx * 0.27, y + 0.12, 3.42), (sx * 0.29, y + 0.1, 2.72))]
           + [(0, y - 0.02, 3.66), (0, y + 0.13, 3.55)], MARBLE)                 # the cowl round it
    from mathutils.bvhtree import BVHTree
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    face = BVHTree.FromBMesh(bm)
    for sx in (-1, 1):                       # the eyes, closed in peace: dark slits, the outer ends low
        A.decal(bm, [(sx * 0.03, y + 2, 3.1), (sx * 0.16, y + 2, 3.08), (sx * 0.15, y + 2, 3.11), (sx * 0.03, y + 2, 3.135)],
                (0, 1, 0), VOID, onto=face, facing=0.2)
    A.decal(bm, [(-0.08, y + 2, 2.66), (0.08, y + 2, 2.66), (0.06, y + 2, 2.78), (-0.06, y + 2, 2.78)], (0, 1, 0), VOID,
            onto=face, facing=0.2)                                                # the open mouth
    A.hull(bm, [(-0.05, y + 0.15, 2.7), (0.05, y + 0.15, 2.7), (-0.05, y + 0.12, 2.7), (0.05, y + 0.12, 2.7),
                (-0.08, out(1.3) + 0.03, 1.3), (0.08, out(1.3) + 0.03, 1.3), (-0.08, out(1.3), 1.3), (0.08, out(1.3), 1.3)],
           BLOOD)
    A.hull(bm, [(-0.12, 1.5 * c + 0.01, 1.3), (0.12, 1.5 * c + 0.01, 1.3), (-0.28, 1.52 * c + 0.2, POOL_Z + 0.01),
                (0.28, 1.52 * c + 0.2, POOL_Z + 0.01), (0, 1.5 * c - 0.05, 1.3)], BLOOD)
    turn(bm, new(bm, before), deg - 90)


def martyrs_raised():
    """Three martyrs back to back on the pedestal, larger than life in pale stone: hooded, heads up,
    one hand on the heart (the oath) and the other raising a sword up and outwards, so the three
    blades fan out over the pool."""
    bm = bmesh.new()
    for k, facing in enumerate((270, 30, 150)):
        before = set(bm.verts)
        z0 = PED_TOP
        secs = [(0.0, 0.55, 0.45), (1.1, 0.42, 0.34), (2.0, 0.54, 0.36), (2.2, 0.26, 0.26), (2.5, 0.32, 0.34),
                (2.85, 0.17, 0.2), (3.0, 0, 0)]
        A.loft(bm, [((0, -0.45 - 0.05 * max(0.0, z - 2.2), z0 + z), w, d) for z, w, d in secs], sides=8, ref=(0, 1, 0),
               mats=MARBLE)
        # The left hand on the heart; the right arm raised with the sword.
        A.loft(bm, [((-0.5, -0.45, z0 + 1.95), 0.12, 0.12), ((-0.42, -0.82, z0 + 1.55), 0.11, 0.11),
                    ((-0.05, -0.86, z0 + 1.72), 0.1, 0.1)], sides=5, ref=(0, 0, 1), mats=MARBLE)
        hand = Vector((0.95, -0.75, z0 + 2.95))
        A.loft(bm, [((0.5, -0.45, z0 + 1.95), 0.12, 0.12), ((0.85, -0.6, z0 + 2.45), 0.11, 0.11), (hand, 0.1, 0.1)],
               sides=5, ref=(0, 0, 1), mats=MARBLE)
        d = Vector((0.35, -0.45, 1.0)).normalized()
        side = d.cross(Vector((0, 0, 1))).normalized()
        guard = hand + d * 0.14
        A.hull(bm, [guard + side * 0.09, guard - side * 0.09, guard + d * 1.7, guard + side * 0.07 + d * 0.15 + Vector((0, 0, 0.02)),
                    guard - side * 0.07 + d * 0.15 - Vector((0, 0, 0.02))], BLADE)
        A.hull(bm, [guard + side * s * 0.28 + d * dd for s in (-1, 1) for dd in (-0.03, 0.03)] +
               [guard + Vector((0, 0, 0.04)), guard - Vector((0, 0, 0.04))], BRONZE)
        A.loft(bm, [(hand - d * 0.12, 0.045, 0.045), (guard, 0.045, 0.045)], sides=5, ref=(0, 0, 1), mats=DARKIRON)
        # The face in the hood's shadow.
        from mathutils.bvhtree import BVHTree
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        A.decal(bm, [(-0.16, -3.0, z0 + 2.32), (0.16, -3.0, z0 + 2.32), (0.13, -3.0, z0 + 2.7), (-0.13, -3.0, z0 + 2.7)],
                (0, -1, 0), VOID, onto=BVHTree.FromBMesh(bm), facing=0.2)
        turn(bm, new(bm, before), facing - 270)
    return obj("AltarMartyrs", bm, smooth=40)


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
    A.box(bm, (-13, -13, -0.1), (13, 13, 0.0), FLOOR)
    floor = obj("Floor", bm, smooth=None)
    for c in list(floor.users_collection):
        c.objects.unlink(floor)
    A.collection("Preview").objects.link(floor)
    objs = [dais(), martyrs_raised()]
    for k in range(4):
        a = math.radians(45 + 90 * k)
        objs.append(brazier(at=(7.4 * math.cos(a), 7.4 * math.sin(a), 0)))
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
