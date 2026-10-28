"""Troth, the god of Cruor's empire, made of the oaths its followers swear in blood (docs/lore.md II),
interpreted from images/troth.png: a towering dark figure about six tiles tall, hooded and faceless
but for two red eyes, crowned and shouldered with iron spikes, its arms open and lowered. Our own
part, from the lore: the betrayers' blades still stand in its back, fanned like a halo, and the
wound they made has never closed; its blood runs down the robe. Troth is not on the Altar of
Martyrs (buildings/cruor/altar.py); this is the god itself, for wherever it makes its entrance.

    runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\creatures\\common\\troth.py")
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
sys.modules.pop("rth_art", None)
import rth_art as A  # noqa: E402

MATS = ["Blackiron", "Sable", "Blood", "Steel", "Bronze", "Iron", "FountBlood"]
IRON, VOID, EYES, BLADE, BRONZE, DARKIRON, BLOOD = range(len(MATS))


def obj(name, bm, smooth=35):
    A.finish(bm)
    return A.mesh_from(name, bm, MATS, coll="Troth", smooth_angle=smooth)


def troth(z0=0.0, name="Troth", at=(0, 0, 0)):
    """Troth, about six tiles tall: hooded and faceless but for two red eyes, crowned and shouldered
    with iron spikes, its arms open and lowered, the betrayers' blades still in its back and its
    wound still bleeding. Its feet at z0."""
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


def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Troth", "Preview"):
            bpy.data.collections.remove(c)
    return troth()


ob = build()
A.preview_rig(focus=(0, 0, 3.2), distance=14.0, pitch=15, yaw=-20, ground=6, ground_colour="CryptFloor",
              human=True, human_at=(-1.8, -2.5))
for area in bpy.context.screen.areas:
    if area.type == "VIEW_3D":
        area.spaces.active.shading.type = "MATERIAL"
result = A.report([ob])
