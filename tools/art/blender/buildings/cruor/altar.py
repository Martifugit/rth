"""The Altar of Martyrs: the centrepiece of Cruor's cathedral and the heart of Troth's church,
where the faithful honour the fallen martyrs; for the dais the cathedral leaves at its east end
(cathedral.py; 18 tiles across under a 22-tile nave). Troth itself is not on it (the god is
creatures/troth.py).

Our reading of images/blood-altar-of-martyrs.jpg, made the way the nature and caves kits are
(tools/art/blender/README.md): chunky faceted masses with smooth light over few faces, nothing quite
true, flat palette colours with the contrast between materials, detail painted onto big faces.
- Three martyrs stand back to back on a tall pedestal in a round pool of blood, larger than life
  in bone-pale stone: hooded, a hand on the heart (the oath), a broad sword raised outwards, so the
  three blades fan out over the pool.
- Faces of the fallen are carved round the pedestal, cowled, eyes closed, mouths open, each pouring
  a thick stream of blood into the pool.
- Channels come in from the nave across the dais and down its steps to the pool. Four iron braziers
  stand at its corners.

Red is the faith's and the blood's only (docs/regions/cruor-architecture.md). As dressing, it is
flat colour, not the cathedral's painted stone (rule 10). Front is -Y (towards the nave).

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
ART = os.path.join(HERE, "..", "..")
for p in (os.path.join(ART, "caves"), os.path.join(ART, "nature"), ART):
    if p not in sys.path:
        sys.path.insert(0, p)
for m in ("rth_art", "basin", "shapes"):
    sys.modules.pop(m, None)
import rth_art as A  # noqa: E402
from shapes import block, chunk  # noqa: E402  (the caves kit's stone)

MATS = ["Gravestone", "Ashgrey", "SaintBone", "FountBlood", "Sable", "Steel", "Bronze", "Blackiron", "Ember",
        "Charcoal", "CryptFloor"]
STONE, DARKSTONE, BONE, BLOOD, VOID, BLADE, BRONZE, IRON, COALS, CHAR, FLOOR = range(len(MATS))

STEPS = ((5.6, 0.25), (5.0, 0.5), (4.4, 0.75))   # the dais's rings: outer radius, top
TOP = 0.75
POOL_R, KERB_R = 2.9, 3.35
POOL_Z, KERB_Z = 0.88, 1.08
PED_TOP = 3.9
CHANNELS = (270, 0, 180)                         # degrees: from the nave (front), and from either side
FACES = (270, 0, 90, 180)
BONE3 = (BONE, BONE, BONE)


def obj(name, bm, smooth=40):
    A.finish(bm)
    return A.mesh_from(name, bm, MATS, coll="Altar", smooth_angle=smooth)


def turn(bm, verts, deg, at=(0, 0, 0)):
    bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, 'Z'), verts=verts)


def new(bm, before):
    return [v for v in bm.verts if v not in before]


def chunky(bm, points, half, mat):
    """A thick limb or bar: the hull of a box of half-size `half` round each point."""
    hx, hy, hz = half
    return A.hull(bm, [Vector(p) + Vector((dx, dy, dz)) for p in points
                       for dx in (-hx, hx) for dy in (-hy, hy) for dz in (-hz, hz)], mat)


def wedge(bm, r0, r1, a0, a1, z0, z1, rnd, mat, chip=0.03, gap=0.012):
    """One block of a ring: from radius r0 to r1 and angle a0 to a1 (radians), z0 to z1, its
    corners knocked out of true and a hair of gap to its neighbours."""
    pts = []
    for a in (a0 + gap, a1 - gap):
        for r in (r0, r1):
            for z in (z0, z1):
                j = chip if z == z1 else chip * 0.3
                pts.append((r * math.cos(a) + rnd.uniform(-j, j), r * math.sin(a) + rnd.uniform(-j, j),
                            z + (rnd.uniform(-j, j * 0.5) if z == z1 else 0)))
    return A.hull(bm, pts, mat)


def ring(bm, r0, r1, z0, z1, rnd, mat, size=1.1, chip=0.03):
    """A ring of wedge blocks about `size` long at the outside."""
    n = max(6, int(2 * math.pi * r1 / size))
    for k in range(n):
        wedge(bm, r0, r1, 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n, z0, z1, rnd, mat, chip)


def course(bm, r_bottom, r_top, z0, z1, rnd, mat, sides=8, turn_by=0.0, chip=0.04):
    """One course of the pedestal: a faceted drum, its corners out of true, turned a little from the
    course below."""
    pts = []
    for r, z in ((r_bottom, z0), (r_top, z1)):
        for k in range(sides):
            a = turn_by + 2 * math.pi * k / sides
            pts.append((r * math.cos(a) + rnd.uniform(-chip, chip), r * math.sin(a) + rnd.uniform(-chip, chip),
                        z + (rnd.uniform(-chip, chip * 0.4) if z == z1 else 0)))
    return A.hull(bm, pts, mat)


# ---- the dais, the pool, the pedestal, the faces, the channels --------------------------------------

def dais():
    r = random.Random(3)
    bm = bmesh.new()
    for k, (rad, top) in enumerate(STEPS):
        inner = STEPS[k + 1][0] - 0.05 if k + 1 < len(STEPS) else KERB_R
        ring(bm, inner, rad, top - 0.3, top, r, STONE, size=1.4, chip=0.035)
    # The pool's kerb, block by block, and the blood inside it.
    ring(bm, POOL_R, KERB_R, TOP - 0.05, KERB_Z, r, DARKSTONE, size=0.9, chip=0.03)
    pool = [bm.verts.new((POOL_R * math.cos(a) * r.uniform(0.99, 1.01), POOL_R * math.sin(a) * r.uniform(0.99, 1.01), POOL_Z))
            for a in [2 * math.pi * k / 16 for k in range(16)]]
    bm.faces.new(pool).material_index = BLOOD
    # The pedestal: a plinth out of the pool, two drums, a cap; each a little off the one below.
    course(bm, 1.6, 1.5, TOP, 1.35, r, DARKSTONE, turn_by=0.1)
    course(bm, 1.2, 1.12, 1.33, 2.45, r, STONE, turn_by=0.32)
    course(bm, 1.12, 1.02, 2.43, 3.6, r, STONE, turn_by=0.15)
    course(bm, 1.5, 1.45, 3.58, PED_TOP, r, DARKSTONE, turn_by=0.4)
    for deg in FACES:
        fallen_face(bm, deg, r)
    # The channels: a gutter of chipped blocks either side of a stream of blood, from the nave
    # across the floor, down the steps and over the dais to the kerb.
    profile = [(5.6, 10.0, 0.0), (5.0, 5.6, 0.25), (4.4, 5.0, 0.5), (KERB_R, 4.4, TOP)]
    for deg in CHANNELS:
        before = set(bm.verts)
        for r0, r1, z in profile:
            x = r0
            while x < r1 - 0.01:
                x1 = min(r1, x + r.uniform(0.7, 1.0))
                for y0, y1 in ((-0.34, -0.17), (0.17, 0.34)):
                    block(bm, (x + 0.01, y0, z - 0.05), (x1 - 0.01, y1, z + 0.09), r, mat=DARKSTONE, chip=0.015)
                x = x1
            A.box(bm, (r0, -0.17, z - 0.05), (r1, 0.17, z + 0.025), BLOOD)
        turn(bm, new(bm, before), deg)
    return obj("AltarDais", bm, smooth=30)


def fallen_face(bm, deg, r):
    """A face of the fallen on the pedestal's side facing `deg`: a cowled head in bone-pale stone
    growing out of the drum, one smooth mass, its eyes closed (two thin dark strokes, the outer ends
    low) and its mouth open, a thick stream of blood from the mouth down the drums and a spill of it
    into the pool."""
    before = set(bm.verts)
    y = 1.0                                  # inside the drum's face, so the head grows out of it
    A.loft(bm, [((y, 0, 2.62), 0.42, 0.62), ((y + 0.22, 0, 2.66), 0.4, 0.58), ((y + 0.34, 0, 2.6), 0.3, 0.46),
                ((y + 0.42, 0, 2.55), 0.18, 0.3), ((y + 0.45, 0, 2.52), 0, 0)], sides=8, ref=(0, 0, 1), mats=BONE)
    A.loft(bm, [((y, 0, 2.66), 0.5, 0.72), ((y + 0.22, 0, 2.72), 0.5, 0.72), ((y + 0.3, 0, 2.8), 0, 0)],
           sides=8, ref=(0, 0, 1), mats=BONE)                                         # the cowl behind it
    chunky(bm, [(y + 0.4, 0, 2.38), (y + 0.3, 0.02, 2.05), (1.2, 0.0, 1.4)], (0.03, 0.09, 0.02), BLOOD)
    chunk(bm, (1.62, 0, POOL_Z - 0.02), (0.3, 0.35, 0.06), r, sink=0.0, mats=(BLOOD, BLOOD, BLOOD), points=10)
    turn(bm, new(bm, before), deg)
    a = math.radians(deg)
    out = Vector((math.cos(a), math.sin(a), 0))
    side = Vector((-out.y, out.x, 0))
    from mathutils.bvhtree import BVHTree
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    face = BVHTree.FromBMesh(bm)
    far = out * 3.0
    for sx in (-1, 1):
        inner, outer = side * sx * 0.05, side * sx * 0.22
        A.decal(bm, [far + inner + Vector((0, 0, 2.72)), far + outer + Vector((0, 0, 2.68)),
                     far + outer + Vector((0, 0, 2.74)), far + inner + Vector((0, 0, 2.78))], out, VOID, onto=face, facing=0.2)
    A.decal(bm, [far - side * 0.09 + Vector((0, 0, 2.38)), far + side * 0.09 + Vector((0, 0, 2.38)),
                 far + side * 0.07 + Vector((0, 0, 2.5)), far - side * 0.07 + Vector((0, 0, 2.5))], out, VOID, onto=face, facing=0.2)


# ---- the martyrs ----------------------------------------------------------------------------------

def martyr_mass(bm, r, facing):
    """One martyr, built facing -Y and turned to `facing`: one smooth robed mass rising into its
    hood, thick arms grown out of the shoulders (one to the heart, one raised), a broad sword held up
    and outwards."""
    before = set(bm.verts)
    z0 = PED_TOP
    secs = [(0.0, 0.64, 0.5), (0.5, 0.6, 0.46), (1.3, 0.47, 0.37), (2.0, 0.6, 0.4), (2.18, 0.3, 0.3), (2.45, 0.37, 0.39),
            (2.8, 0.2, 0.24), (2.98, 0, 0)]
    A.loft(bm, [((0, -0.5 - 0.1 * max(0.0, z - 2.1), z0 + z), w, d) for z, w, d in secs], sides=8, ref=(0, 1, 0), mats=BONE)
    A.loft(bm, [((-0.42, -0.5, z0 + 1.92), 0.16, 0.16), ((-0.4, -0.88, z0 + 1.6), 0.14, 0.14),
                ((-0.05, -0.92, z0 + 1.72), 0.13, 0.13), ((0.04, -0.93, z0 + 1.74), 0, 0)], sides=6, ref=(0, 0, 1), mats=BONE)
    hand = Vector((0.95, -0.8, z0 + 2.9))
    A.loft(bm, [((0.42, -0.5, z0 + 1.92), 0.16, 0.16), ((0.82, -0.66, z0 + 2.42), 0.14, 0.14), (hand, 0.13, 0.13),
                (hand + Vector((0.03, -0.02, 0.1)), 0, 0)], sides=6, ref=(1, 0, 0), mats=BONE)
    d = Vector((0.35, -0.45, 1.0)).normalized()
    side = d.cross(Vector((0, 0, 1))).normalized()
    thick = d.cross(side).normalized() * 0.035
    guard = hand + d * 0.2
    A.hull(bm, [guard + side * 0.15 + thick, guard - side * 0.15 + thick, guard + side * 0.15 - thick, guard - side * 0.15 - thick,
                guard + side * 0.12 + d * 1.0, guard - side * 0.12 + d * 1.0, guard + d * 1.32], BLADE)
    A.hull(bm, [guard + side * s * 0.36 + d * dd + Vector((0, 0, dz)) for s in (-1, 1) for dd in (-0.06, 0.06)
                for dz in (-0.05, 0.05)], BRONZE)
    A.loft(bm, [(hand - d * 0.2, 0.05, 0.05), (guard, 0.05, 0.05)], sides=5, ref=(0, 0, 1), mats=IRON)
    turn(bm, new(bm, before), facing - 270)


def martyrs_raised():
    r = random.Random(11)
    bm = bmesh.new()
    for facing in (270, 30, 150):
        martyr_mass(bm, r, facing)
    for facing in (270, 30, 150):            # the dark in each hood, painted on its face
        a = math.radians(facing)
        out = Vector((math.cos(a), math.sin(a), 0))
        A.paint(bm, out * 0.95 + Vector((0, 0, PED_TOP + 2.45)), VOID, facing=out, size=0.85)
    return obj("AltarMartyrs", bm, smooth=60)


# ---- the braziers ---------------------------------------------------------------------------------

def brazier(at=(0, 0, 0), seed=1):
    """An iron brazier on three thick legs, a wide rough bowl heaped with coals."""
    r = random.Random(seed)
    bm = bmesh.new()
    for k in range(3):
        a = 2 * math.pi * k / 3
        chunky(bm, [(math.cos(a) * 0.6, math.sin(a) * 0.6, -0.02), (math.cos(a) * 0.34, math.sin(a) * 0.34, 0.85)],
               (0.06, 0.06, 0.02), IRON)
    course(bm, 0.3, 0.7, 0.85, 1.32, r, IRON, sides=8, chip=0.03)
    for k in range(8):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0, 0.42)
        chunk(bm, (d * math.cos(a), d * math.sin(a), 1.28), (0.15, 0.13, 0.16), r, sink=0.0,
              mats=(COALS, CHAR, COALS) if k % 3 else (CHAR, CHAR, CHAR), points=8)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    return obj("AltarBrazier", bm, smooth=35)


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
        objs.append(brazier(at=(7.4 * math.cos(a), 7.4 * math.sin(a), 0), seed=k + 1))
    return objs


def rig():
    A.preview_rig(focus=(0, 0, 3.0), distance=26.0, pitch=25, yaw=-20, ground=0, human=True, human_at=(0.0, -7.5))
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
