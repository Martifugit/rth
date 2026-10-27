"""The Altar of Martyrs: the centrepiece of Cruor's cathedral and the heart of Troth's church.
Design and decisions: docs/regions/cruor-altar.md (read it first). This is the blockout: every
part at its final size, deliberate and symmetric (revolved mouldings, designed figures; nothing
random), mostly plain, the blood already red because it is part of the focus.

Read from the nave: three pale martyrs back to back on a tall dark pedestal, swords raised and
fanned, in a round red pool ringed with outward spikes, on a stepped round dais; one red line
running to it from the nave. Front is -Y (the nave).

    runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\buildings\\cruor\\altar.py")
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..", "..")
if ART not in sys.path:
    sys.path.insert(0, ART)
sys.modules.pop("rth_art", None)
import rth_art as A  # noqa: E402

# The blockout's values: dark stone, pale trim, the martyrs' stone, blood, iron, the floor.
MATS = ["Ashgrey", "Gravestone", "Tin", "FountBlood", "Blackiron", "CryptFloor", "Sable"]
DARK, TRIM, FIGURE, BLOOD, IRON, FLOOR, VOID = range(len(MATS))

# The dais: three round steps, each a riser and a pale nose.
STEPS = [(5.5, 0.25), (4.9, 0.5), (4.3, 0.75)]
TOP = 0.75
RIM_OUT, RIM_IN, RIM_H = 3.4, 2.95, 1.08     # the pool's rim
POOL_Z = 0.92
PED_TOP = 4.1
SPIKES = 12


def obj(name, bm, smooth=None):
    A.finish(bm)
    return A.mesh_from(name, bm, MATS, coll="Altar", smooth_angle=smooth)


def turn(bm, verts, deg, at=(0, 0, 0)):
    bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, 'Z'), verts=verts)


def new(bm, before):
    return [v for v in bm.verts if v not in before]


def lathe(bm, profile, sides, rot=0.0, centre=(0, 0)):
    """A solid of revolution: profile is (radius, height, material of the band from the previous
    point) from bottom to top; a radius of 0 closes it to a point; a ring left open at either end is
    capped flat with that point's material."""
    cx, cy = centre
    rings = []
    for r, z, _ in profile:
        if r == 0:
            rings.append([bm.verts.new((cx, cy, z))])
        else:
            rings.append([bm.verts.new((cx + r * math.cos(rot + 2 * math.pi * k / sides),
                                        cy + r * math.sin(rot + 2 * math.pi * k / sides), z)) for k in range(sides)])
    for i in range(1, len(rings)):
        a, b, m = rings[i - 1], rings[i], profile[i][2]
        for k in range(sides):
            n = (k + 1) % sides
            if len(a) == 1:
                f = bm.faces.new((a[0], b[k], b[n]))
            elif len(b) == 1:
                f = bm.faces.new((a[k], a[n], b[0]))
            else:
                f = bm.faces.new((a[k], a[n], b[n], b[k]))
            f.material_index = m
    if len(rings[-1]) > 1:
        bm.faces.new(rings[-1]).material_index = profile[-1][2]
    if len(rings[0]) > 1:
        bm.faces.new(list(reversed(rings[0]))).material_index = profile[0][2]
    return rings


def band(bm, profile, sides):
    """An open band of revolution (no caps): a ring-shaped moulding such as the pool's rim."""
    rings = [[bm.verts.new((r * math.cos(2 * math.pi * k / sides), r * math.sin(2 * math.pi * k / sides), z))
              for k in range(sides)] for r, z, _ in profile]
    for i in range(1, len(rings)):
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((rings[i - 1][k], rings[i - 1][n], rings[i][n], rings[i][k])).material_index = profile[i][2]
    return rings


# ---- the dais, the pool, the spikes, the line ----------------------------------------------------

def dais():
    """Three round steps with pale bevelled noses, the platform, and the pool's rim: a worked
    moulding bevelled on its outer and inner edges, the blood level just under its lip."""
    bm = bmesh.new()
    prof = [(STEPS[0][0], -0.05, DARK)]
    for k, (r, top) in enumerate(STEPS):
        prof += [(r, top - 0.07, DARK), (r - 0.07, top, TRIM)]
        if k + 1 < len(STEPS):
            prof += [(STEPS[k + 1][0] + 0.02, top, TRIM)]
    prof += [(RIM_OUT, TOP, TRIM)]
    lathe(bm, prof, 48)
    band(bm, [(RIM_OUT, TOP - 0.01, DARK), (RIM_OUT, RIM_H - 0.12, DARK), (RIM_OUT - 0.1, RIM_H, TRIM),
              (RIM_IN + 0.1, RIM_H, TRIM), (RIM_IN, RIM_H - 0.08, TRIM), (RIM_IN, POOL_Z - 0.02, DARK)], 48)
    pool = [bm.verts.new((RIM_IN * math.cos(2 * math.pi * k / 48), RIM_IN * math.sin(2 * math.pi * k / 48), POOL_Z)) for k in range(48)]
    bm.faces.new(pool).material_index = BLOOD
    return obj("AltarDais", bm, smooth=40)


def spikes():
    """Twelve short spikes pointing out from the rim, square in section, a pale collar at each foot."""
    bm = bmesh.new()
    for k in range(SPIKES):
        a = 2 * math.pi * (k + 0.5) / SPIKES
        d = Vector((math.cos(a), math.sin(a), 0))
        s = Vector((-d.y, d.x, 0))
        foot = d * (RIM_OUT - 0.05) + Vector((0, 0, RIM_H - 0.2))
        tip = d * (RIM_OUT + 0.62) + Vector((0, 0, RIM_H + 0.12))
        out = (tip - foot).normalized()
        up = out.cross(s).normalized()
        A.hull(bm, [foot + s * 0.13 + up * 0.13, foot - s * 0.13 + up * 0.13, foot + s * 0.13 - up * 0.13,
                    foot - s * 0.13 - up * 0.13, tip], IRON)
        A.hull(bm, [foot + out * dd + s * ss * 0.16 + up * uu * 0.16 for dd in (0.02, 0.1) for ss in (-1, 1) for uu in (-1, 1)], TRIM)
    return obj("AltarSpikes", bm)


def blood_line():
    """The one red line: inlaid from the nave, down each riser and across each tread, over the
    platform to the rim, edged in pale stone."""
    bm = bmesh.new()
    w, e = 0.16, 0.08
    runs = [(12.0, STEPS[0][0], 0.0)] + [(STEPS[k][0], STEPS[k + 1][0] if k + 1 < len(STEPS) else RIM_OUT, STEPS[k][1])
                                          for k in range(len(STEPS))]
    for r0, r1, z in runs:
        A.box(bm, (-w, -r0, z + 0.005), (w, -r1, z + 0.02), BLOOD)
        A.box(bm, (-w - e, -r0, z + 0.005), (-w, -r1, z + 0.035), TRIM)
        A.box(bm, (w, -r0, z + 0.005), (w + e, -r1, z + 0.035), TRIM)
    for k, (r, top) in enumerate(STEPS):
        z0 = STEPS[k - 1][1] if k else 0.0
        A.box(bm, (-w, -r - 0.02, z0), (w, -r + 0.01, top - 0.06), BLOOD)
    return obj("AltarLine", bm)


# ---- the pedestal and its spouts ----------------------------------------------------------------

def pedestal():
    """A moulded base standing in the pool, an eight-sided shaft (the motif goes on its faces in the
    detail pass), a moulded cap under the martyrs; four plain spouts on the base pouring into the
    pool."""
    bm = bmesh.new()
    lathe(bm, [(1.65, POOL_Z - 0.1, DARK), (1.65, 1.25, DARK), (1.55, 1.35, TRIM), (1.32, 1.42, TRIM),
               (1.32, 1.6, DARK), (1.22, 1.66, TRIM), (1.08, 1.7, TRIM)], 32)
    lathe(bm, [(1.08, 1.69, DARK), (0.98, 3.55, DARK), (0.98, 3.56, DARK)], 8, rot=math.pi / 8)
    lathe(bm, [(1.12, 3.5, TRIM), (1.22, 3.62, TRIM), (1.22, 3.72, DARK), (1.45, 3.88, TRIM), (1.55, 3.95, TRIM),
               (1.55, PED_TOP, TRIM)], 32)
    for deg in (270, 0, 90, 180):
        before = set(bm.verts)
        A.box(bm, (1.2, -0.13, 1.08), (1.75, 0.13, 1.28), TRIM)                     # the spout
        A.box(bm, (1.6, -0.08, 1.12), (1.76, 0.08, 1.2), DARK)                      # its mouth
        lathe(bm, [(0.06, POOL_Z, BLOOD), (0.06, 1.16, BLOOD), (0, 1.17, BLOOD)], 6, centre=(1.82, 0))   # the pour
        turn(bm, new(bm, before), deg)
    return obj("AltarPedestal", bm, smooth=40)


# ---- the martyrs ----------------------------------------------------------------------------------

def martyr(bm, facing, at_r=0.62):
    """One martyr, designed: a hooded robe with a flared hem, a narrow waist, broad shoulders, the
    hood leaning a little forward; the left hand on the heart, the right raising a broad sword up and
    outwards. Built facing -Y, then turned to `facing` and set back to back."""
    before = set(bm.verts)
    z0 = PED_TOP
    secs = [(0.0, 0.64, 0.56, 0.0), (0.18, 0.6, 0.52, 0.0), (1.05, 0.44, 0.37, 0.0), (1.7, 0.53, 0.4, 0.0),
            (2.02, 0.62, 0.42, 0.02), (2.18, 0.3, 0.3, 0.0), (2.38, 0.35, 0.37, -0.04), (2.72, 0.3, 0.33, -0.08),
            (2.92, 0.13, 0.17, -0.12), (3.0, 0, 0, -0.14)]
    A.loft(bm, [((0, -at_r + dy, z0 + z), w, d) for z, w, d, dy in secs], sides=12, ref=(0, 1, 0), mats=FIGURE)
    A.loft(bm, [((-0.46, -at_r - 0.05, z0 + 1.98), 0.15, 0.15), ((-0.4, -at_r - 0.3, z0 + 1.56), 0.13, 0.13),
                ((-0.08, -at_r - 0.42, z0 + 1.78), 0.12, 0.12), ((0.0, -at_r - 0.43, z0 + 1.82), 0, 0)],
           sides=8, ref=(0, 0, 1), mats=FIGURE)
    hand = Vector((0.86, -at_r - 0.28, z0 + 3.02))
    A.loft(bm, [((0.5, -at_r, z0 + 1.98), 0.15, 0.15), ((0.8, -at_r - 0.14, z0 + 2.52), 0.13, 0.13), (hand, 0.12, 0.12),
                (hand + Vector((0.02, -0.02, 0.1)), 0, 0)], sides=8, ref=(1, 0, 0), mats=FIGURE)
    d = Vector((0.26, -0.36, 1.0)).normalized()
    side = d.cross(Vector((0, 0, 1))).normalized()
    flat = d.cross(side).normalized()
    guard = hand + d * 0.2
    A.loft(bm, [(hand - d * 0.22, 0.045, 0.045), (guard, 0.045, 0.045)], sides=6, ref=(0, 0, 1), mats=IRON)
    A.hull(bm, [guard + side * s * 0.34 + d * dd + flat * ff for s in (-1, 1) for dd in (-0.05, 0.05) for ff in (-0.05, 0.05)], IRON)
    A.hull(bm, [guard + d * 0.05 + side * s * 0.13 + flat * ff for s in (-1, 1) for ff in (-0.035, 0.035)] +
           [guard + d * 0.95 + side * s * 0.11 for s in (-1, 1)] + [guard + d * 0.95 + flat * f * 0.03 for f in (-1, 1)] +
           [guard + d * 1.25], TRIM)
    turn(bm, new(bm, before), facing - 270)


def martyrs():
    bm = bmesh.new()
    for facing in (270, 30, 150):
        martyr(bm, facing)
    for facing in (270, 30, 150):            # the dark of each hood's opening
        a = math.radians(facing)
        out = Vector((math.cos(a), math.sin(a), 0))
        A.paint(bm, out * 1.05 + Vector((0, 0, PED_TOP + 2.55)), VOID, facing=out, size=0.9)
    return obj("AltarMartyrs", bm, smooth=55)


# ---- the braziers -----------------------------------------------------------------------------------

def brazier(at=(0, 0, 0)):
    """A tall iron brazier: a round foot, a shaft, a wide bowl with a crown of short spikes round its
    lip, coals heaped in it."""
    bm = bmesh.new()
    lathe(bm, [(0.42, 0.0, IRON), (0.42, 0.1, IRON), (0.3, 0.18, IRON), (0.1, 0.26, IRON), (0.08, 1.15, IRON),
               (0.14, 1.2, IRON), (0.55, 1.42, IRON), (0.6, 1.5, IRON), (0.5, 1.5, IRON), (0.3, 1.38, IRON)], 16)
    for k in range(8):
        a = 2 * math.pi * k / 8
        d = Vector((math.cos(a), math.sin(a), 0))
        A.hull(bm, [d * 0.5 + Vector((0, 0, 1.46)) + Vector((-d.y, d.x, 0)) * s * 0.07 for s in (-1, 1)] +
               [d * 0.6 + Vector((0, 0, 1.46)), d * 0.62 + Vector((0, 0, 1.78))], IRON)
    lathe(bm, [(0.5, 1.44, BLOOD), (0.32, 1.56, BLOOD), (0, 1.6, BLOOD)], 12)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    return obj("AltarBrazier", bm, smooth=40)


# ---- the board ------------------------------------------------------------------------------------

def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Altar", "Preview"):
            bpy.data.collections.remove(c)
    bm = bmesh.new()
    A.box(bm, (-14, -14, -0.1), (14, 14, 0.0), FLOOR)
    floor = obj("Floor", bm)
    for c in list(floor.users_collection):
        c.objects.unlink(floor)
    A.collection("Preview").objects.link(floor)
    objs = [dais(), spikes(), blood_line(), pedestal(), martyrs()]
    for k in range(4):
        a = math.radians(45 + 90 * k)
        objs.append(brazier(at=(7.2 * math.cos(a), 7.2 * math.sin(a), 0)))
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
