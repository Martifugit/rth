"""Dressing, village. The basin's yards and fields: woodpile, chopping block, skeps, scarecrow, eel traps, a lean-to, hay, a farm cart, graves and a mooring post.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Dressing/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\dressing\\village.py")
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
from artlib.nature import blob, flat_chip, make, rod, wobble  # noqa: E402
from artlib.village import BARK, HAY, box, piece  # noqa: E402


def mooring_post(seed=1, at=(0, 0, 0)):
    """A mooring post standing in the shallows (origin at the water's surface): a leaning round
    post driven into the bed, a rope wound round it and trailing off into the water."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    rod(bm, wobble(rnd, (0, 0, -0.8), (0.06, 0.03, 0.95), 3, 0.03), 0.09, mat=0, sides=6, taper=0.85)
    rod(bm, [(0.04, 0.02, 0.5), (0.045, 0.022, 0.62)], 0.105, mat=1, sides=6)
    rod(bm, [(0.1, 0.03, 0.55), (0.45, 0.1, 0.3), (0.85, 0.05, -0.02)], 0.016, mat=1, sides=3)
    return make("MooringPost", bm, ["Driftwood", "Wheat"], smooth_angle=50, at=at)


def grave(seed=1, at=(0, 0, 0), old=False):
    """A grave: a plain headstone with a rounded top and a low turf mound before it. old: the stone
    leaning and sunk, the mound grown down almost flat."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    w, h, t = 0.24, 0.62, 0.05
    prof = [(-w, -0.1), (w, -0.1), (w, h - 0.1)] + \
           [(w * math.cos(a), h - 0.1 + 0.12 * math.sin(a)) for a in (math.radians(40), math.radians(90), math.radians(140))] + \
           [(-w, h - 0.1)]
    A.hull(bm, [(x, y, z) for x, z in prof for y in (-t, t)], mat=0)
    if old:
        from mathutils import Matrix as M
        bmesh.ops.transform(bm, matrix=M.Rotation(math.radians(rnd.choice((-1, 1)) * 11), 4, Vector((0, 1, 0.2)).normalized()),
                            verts=bm.verts)
        bmesh.ops.translate(bm, vec=Vector((0, 0, -0.14)), verts=bm.verts)
    mound_h = 0.06 if old else 0.16
    blob(bm, (0, -1.0, 0.0), (0.3, 0.85, mound_h), 1, rnd, jitter=(0.92, 1.06), mat=1, squash_below=-0.02)
    return make("GraveOld" if old else "Grave", bm, ["Gravestone", "GraveTurf"], smooth_angle=40, at=at)


def hay_stook(seed=1, at=(0, 0, 0)):
    """A stook of sheaves drying in the field: six sheaves leaning together, each tied at the waist."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(6):
        a = 2 * math.pi * k / 6 + rnd.uniform(-0.15, 0.15)
        out = Vector((math.cos(a), math.sin(a), 0))
        foot = out * 0.3 + Vector((0, 0, -0.02))
        waist = out * 0.17 + Vector((0, 0, 0.45))
        head = out * 0.08 + Vector((0, 0, 0.78 + rnd.uniform(-0.04, 0.04)))
        A.loft(bm, [(foot, 0.13, 0.13), (waist - Vector((0, 0, 0.04)), 0.075, 0.075),
                    (waist + Vector((0, 0, 0.04)), 0.075, 0.075), (head, 0.12, 0.12),
                    (head + Vector((0, 0, 0.14)), 0, 0)], sides=6, ref=(1, 0, 0), mats=[0, 1, 0, 0])
    return make("HayStook", bm, ["Wheat", "Bark"], smooth_angle=50, at=at)


def farm_cart(seed=1, at=(0, 0, 0), rot=0.0):
    """A two-wheeled farm cart at rest, its shafts down on the ground: a plank bed with low sides,
    two spoked wheels on an axle."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    A.box(bm, (-0.75, -0.45, 0.48), (0.75, 0.45, 0.56), 0)
    for y in (-0.45, 0.41):
        A.box(bm, (-0.75, y, 0.56), (0.75, y + 0.04, 0.78), 0)
    A.box(bm, (-0.75, -0.41, 0.56), (-0.71, 0.41, 0.76), 0)
    # Wheels: ten-sided rims as short tubes along Y, a hub, four spokes.
    for s in (-1, 1):
        y = s * 0.56
        ring_a = [bm.verts.new((0.42 * math.cos(2 * math.pi * k / 10), y - 0.03, 0.42 + 0.42 * math.sin(2 * math.pi * k / 10))) for k in range(10)]
        ring_b = [bm.verts.new((v.co.x * 0.84, y - 0.03, 0.42 + (v.co.z - 0.42) * 0.84)) for v in ring_a]
        ring_c = [bm.verts.new((v.co.x, y + 0.03, v.co.z)) for v in ring_a]
        ring_d = [bm.verts.new((v.co.x, y + 0.03, v.co.z)) for v in ring_b]
        for k in range(10):
            n = (k + 1) % 10
            bm.faces.new((ring_a[k], ring_a[n], ring_c[n], ring_c[k])).material_index = 1
            bm.faces.new((ring_b[n], ring_b[k], ring_d[k], ring_d[n])).material_index = 1
            bm.faces.new((ring_a[n], ring_a[k], ring_b[k], ring_b[n])).material_index = 1
            bm.faces.new((ring_c[k], ring_c[n], ring_d[n], ring_d[k])).material_index = 1
        for k in range(4):
            a = math.pi / 4 + math.pi / 2 * k
            rod(bm, [(0, y, 0.42), (0.37 * math.cos(a), y, 0.42 + 0.37 * math.sin(a))], 0.02, mat=1, sides=4)
        rod(bm, [(0, y - 0.06, 0.42), (0, y + 0.06, 0.42)], 0.06, mat=1, sides=6)
    rod(bm, [(0, -0.6, 0.42), (0, 0.6, 0.42)], 0.03, mat=1, sides=4)
    for s in (-1, 1):
        rod(bm, [(0.7, s * 0.32, 0.5), (1.4, s * 0.34, 0.27), (2.0, s * 0.36, 0.04)], 0.035, mat=1, sides=4)
    return make("FarmCart", bm, ["Planks", "Driftwood"], smooth_angle=35, at=at, rot=rot)


WIND = N.WIND


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


def piece_hay():
    bm = bmesh.new()
    A.hull(bm, [(x, y, z) for x in (-0.45, 0.45) for y in (-0.25, 0.25) for z in (0, 0.42)]
           + [(x, y, 0.47) for x in (-0.4, 0.4) for y in (-0.2, 0.2)], HAY)
    for x in (-0.2, 0.2):
        box(bm, x - 0.015, x + 0.015, -0.26, 0.26, 0.0, 0.48, BARK)
    return bm


ASSETS = {
    "MooringPost": mooring_post,
    "Grave": grave,
    "GraveOld": lambda: grave(old=True),
    "HayStook": hay_stook,
    "FarmCart": farm_cart,
    "Woodpile": woodpile,
    "ChoppingBlock": chopping_block,
    "LeanTo": lean_to,
    "Skeps": skeps,
    "Scarecrow": scarecrow,
    "EelTraps": eel_traps,
    "HayBale": piece(piece_hay),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
