"""Dressing, village. The basin's yards and fields: woodpile, chopping block, skeps, scarecrow, eel traps, a lean-to, hay, a farm cart, graves and a mooring post;
the square's handcart, stall and notice board; the farm's tools, barrow, coop and haystack; the mill's
flour and millstones; the fishers' nets and gutting table; the skinning yard's hide frame; the
apothecary's drying herbs; the undercroft's charnel recess and coffin boards; the fallen toll bar; a ladder, a grindstone, a sawhorse and a cooking tripod;
Sour Hollow's leaking cask; tombs for the churchyard; the stocks; fish drying; a cart wheel; a churn;
stalls of fish and cloth; the wardens' weapon rack and barricade; reed bundles; bell ropes; a butcher's block.

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
import artlib.props as P  # noqa: E402


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


def wheel(bm, x, y, z, r, mat, half=0.03, spokes=4, sides=10):
    """A spoked wheel standing in the XZ plane at (x, y, z), its axle along Y: a rim as a short tube,
    a hub and `spokes` spokes."""
    a_ = [bm.verts.new((x + r * math.cos(2 * math.pi * k / sides), y - half, z + r * math.sin(2 * math.pi * k / sides))) for k in range(sides)]
    b_ = [bm.verts.new((x + (v.co.x - x) * 0.82, y - half, z + (v.co.z - z) * 0.82)) for v in a_]
    c_ = [bm.verts.new((v.co.x, y + half, v.co.z)) for v in a_]
    d_ = [bm.verts.new((v.co.x, y + half, v.co.z)) for v in b_]
    for k in range(sides):
        n = (k + 1) % sides
        for q in ((a_[k], a_[n], c_[n], c_[k]), (b_[n], b_[k], d_[k], d_[n]), (a_[n], a_[k], b_[k], b_[n]), (c_[k], c_[n], d_[n], d_[k])):
            bm.faces.new(q).material_index = mat
    for k in range(spokes):
        a = math.pi / spokes + 2 * math.pi * k / spokes
        rod(bm, [(x, y, z), (x + r * 0.85 * math.cos(a), y, z + r * 0.85 * math.sin(a))], 0.018, mat=mat, sides=4)
    rod(bm, [(x, y - half * 2, z), (x, y + half * 2, z)], r * 0.15, mat=mat, sides=6)


def handcart(seed=1, at=(0, 0, 0)):
    """A pedlar's handcart at rest in the square, its legs down: a small box bed on two wheels, two
    handles running out behind (-X) to be pushed, a cloth thrown over its load."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    BOARD, FRAME, CLOTH = range(3)
    z = 0.5
    P.box(bm, -0.55, -0.38, z, 0.55, 0.38, z + 0.05, BOARD)
    for y in (-0.38, 0.35):
        P.box(bm, -0.55, y, z + 0.05, 0.55, y + 0.03, z + 0.28, BOARD)
    for x in (-0.55, 0.52):
        P.box(bm, x, -0.35, z + 0.05, x + 0.03, 0.35, z + 0.28, BOARD)
    for s in (-1, 1):
        wheel(bm, 0.2, s * 0.47, 0.36, 0.36, FRAME)
        P.bar(bm, (0.5, s * 0.3, z - 0.03), (-1.2, s * 0.3, z + 0.25), 0.05, 0.05, FRAME)
        P.bar(bm, (-0.45, s * 0.3, z - 0.02), (-0.45, s * 0.3, 0.0), 0.05, 0.05, FRAME)              # the legs it rests on
    P.rod(bm, (0.2, -0.5, 0.36), (0.2, 0.5, 0.36), 0.03, FRAME)
    blob(bm, (0.0, 0.0, z + 0.28), (0.5, 0.33, 0.14), 1, rnd, jitter=(0.92, 1.06), mat=CLOTH, squash_below=0.0)
    return P.make("Handcart", bm, ["Planks", "DeadWood", "Russet"], "Dressing", at, smooth=35)


def wheelbarrow(seed=1, at=(0, 0, 0)):
    """A wooden wheelbarrow: a tray of boards wider at the back, one wheel at the front (+X), two
    handles and two legs behind; a little earth in the tray."""
    bm = bmesh.new()
    BOARD, FRAME, EARTH = range(3)
    tray = [(0.35, 0.18, 0.38), (0.35, -0.18, 0.38), (-0.3, 0.26, 0.32), (-0.3, -0.26, 0.32)]
    rim = [(0.48, 0.24, 0.62), (0.48, -0.24, 0.62), (-0.38, 0.34, 0.66), (-0.38, -0.34, 0.66)]
    A.hull(bm, tray + rim, BOARD)
    A.hull(bm, [(x * 0.9, y * 0.85, 0.55) for x, y, _ in rim] + [(0.0, 0.0, 0.6)], EARTH)
    wheel(bm, 0.62, 0.0, 0.2, 0.2, FRAME, half=0.025, spokes=4, sides=8)
    for s in (-1, 1):
        P.bar(bm, (0.66, s * 0.06, 0.2), (-0.95, s * 0.27, 0.55), 0.045, 0.045, FRAME)
        P.bar(bm, (-0.3, s * 0.24, 0.42), (-0.32, s * 0.24, 0.0), 0.04, 0.04, FRAME)
    return P.make("Wheelbarrow", bm, ["Planks", "DeadWood", "Peat"], "Dressing", at)


def farm_tools(seed=1, at=(0, 0, 0)):
    """A rake, a pitchfork and a scythe leaning together against a wall (at +Y), their heads up."""
    bm = bmesh.new()
    HAFT, IRON = range(2)
    # the rake: a long haft, a head bar across with wooden teeth
    top = Vector((-0.35, 0.12, 1.75))
    P.rod(bm, (-0.3, -0.35, 0.0), top, 0.02, HAFT, sides=5)
    P.bar(bm, top + Vector((-0.25, 0, 0)), top + Vector((0.25, 0, 0)), 0.04, 0.04, HAFT)
    for k in range(7):
        x = top.x - 0.21 + 0.07 * k
        P.rod(bm, (x, top.y - 0.01, top.z), (x, top.y - 0.08, top.z + 0.04), 0.008, HAFT, sides=3)
    # the pitchfork: a haft and two long iron tines
    base, tip = Vector((0.05, -0.38, 0.0)), Vector((0.08, 0.1, 1.45))
    P.rod(bm, base, tip, 0.02, HAFT, sides=5)
    for s in (-1, 1):
        P.rod(bm, tip, tip + Vector((s * 0.05, 0.0, 0.05)), 0.01, IRON, sides=4)
        A.spike(bm, tip + Vector((s * 0.05, 0.0, 0.05)), 0.01, tip + Vector((s * 0.05, 0.08, 0.38)), sides=3, mat=IRON)
    # the scythe: a curved snath with a hand grip, the blade sweeping off its top
    pts = [(0.4, -0.3, 0.0), (0.45, -0.15, 0.6), (0.44, 0.0, 1.1), (0.42, 0.1, 1.55)]
    N.rod(bm, pts, 0.022, mat=HAFT, sides=5)
    P.rod(bm, (0.44, -0.1, 0.9), (0.6, -0.12, 0.92), 0.015, HAFT, sides=4)
    blade = [(0.42 + 0.7 * math.sin(t) * 0.9, 0.1 + 0.04 * t, 1.55 - 0.18 * (1 - math.cos(t))) for t in (0.0, 0.35, 0.7, 1.0)]
    for (xa, ya, za), (xb, yb, zb) in zip(blade, blade[1:]):
        A.hull(bm, [(xa, ya, za), (xb, yb, zb), (xa, ya - 0.004, za - 0.07 * (1 - (xa - 0.42) / 0.7)),
                    (xb, yb - 0.004, zb - 0.07 * (1 - (xb - 0.42) / 0.7) + 0.005),
                    (xa, ya + 0.008, za), (xb, yb + 0.008, zb)], IRON)
    return P.make("FarmTools", bm, ["Timber", "Iron"], "Dressing", at)


def flour_sacks(seed=3, at=(0, 0, 0)):
    """The mill's flour in sacks: five pale sacks heaped two deep, tied at the neck, flour dusted on
    the ground in front of them."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    SACK, TIE, FLOUR = range(3)
    for (x, y, z), r in (((-0.32, 0.1, 0.0), (0.25, 0.2, 0.3)), ((0.15, 0.12, 0.0), (0.26, 0.21, 0.32)),
                         ((0.55, 0.0, 0.0), (0.22, 0.2, 0.28)), ((-0.1, 0.12, 0.5), (0.24, 0.18, 0.26))):
        blob(bm, (x, y, z + r[2] * 0.95), r, 1, rnd, jitter=(0.9, 1.08), mat=SACK, squash_below=z)
        neck = Vector((x, y, z + r[2] * 1.85))
        A.loft(bm, [(neck - Vector((0, 0, 0.08)), 0.08, 0.07), (neck, 0.045, 0.045), (neck + Vector((0, 0, 0.06)), 0.06, 0.05),
                    (neck + Vector((0, 0, 0.09)), 0, 0)], sides=6, ref=(1, 0, 0), mats=SACK)
        A.loft(bm, [(neck - Vector((0, 0, 0.012)), 0.05, 0.05), (neck + Vector((0, 0, 0.012)), 0.05, 0.05)],
               sides=6, ref=(1, 0, 0), mats=TIE)
    blob(bm, (0.1, -0.38, 0.12), (0.34, 0.17, 0.13), 1, rnd, jitter=(0.9, 1.08), mat=SACK, squash_below=0.0)
    N.flat_chip(bm, (0.2, -0.65, 0.0), 0.2, rnd, FLOUR, h=0.05)
    N.flat_chip(bm, (-0.25, -0.6, 0.0), 0.14, rnd, FLOUR, h=0.05)
    return P.make("FlourSacks", bm, ["Linen", "Umber", "Chalk"], "Dressing", at, smooth=60)


def haystack(seed=1, at=(0, 0, 0)):
    """A haystack in the rickyard, 2 x 2: a round stack of hay with straight-ish sides, a conical
    thatched cap tied down with ropes weighted by stones hanging round the eaves."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    HAY_, CAP, ROPE, STONE_ = range(4)
    n = 12
    prof = ((0.0, 0.82), (0.25, 0.9), (1.0, 0.95), (1.35, 0.98))
    rings = [[bm.verts.new(((r * rnd.uniform(0.97, 1.03)) * math.cos(2 * math.pi * j / n),
                            (r * rnd.uniform(0.97, 1.03)) * math.sin(2 * math.pi * j / n), z)) for j in range(n)] for z, r in prof]
    for a, b in zip(rings, rings[1:]):
        for j in range(n):
            bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j])).material_index = HAY_
    bm.faces.new(list(reversed(rings[0]))).material_index = HAY_
    eave = [bm.verts.new((1.08 * math.cos(2 * math.pi * j / n), 1.08 * math.sin(2 * math.pi * j / n), 1.3)) for j in range(n)]
    apex = bm.verts.new((0.0, 0.0, 2.35))
    for j in range(n):
        bm.faces.new((rings[-1][j], rings[-1][(j + 1) % n], eave[(j + 1) % n], eave[j])).material_index = CAP
        bm.faces.new((eave[j], eave[(j + 1) % n], apex)).material_index = CAP
    for k in range(4):                                                                               # the ropes over the cap
        a = math.pi / 4 + math.pi / 2 * k
        d = Vector((math.cos(a), math.sin(a), 0))
        N.rod(bm, [(0, 0, 2.36), tuple(d * 0.55 + Vector((0, 0, 1.85))), tuple(d * 1.11 + Vector((0, 0, 1.3))),
                   tuple(d * 1.11 + Vector((0, 0, 0.95)))], 0.018, mat=ROPE, sides=3)
        c = d * 1.12 + Vector((0, 0, 0.85))
        A.hull(bm, [tuple(c + Vector((rnd.uniform(-0.09, 0.09), rnd.uniform(-0.09, 0.09), rnd.uniform(-0.1, 0.08)))) for _ in range(8)], STONE_)
    return P.make("Haystack", bm, ["Wheat", "Thatch", "Umber", "Scree"], "Dressing", at, smooth=40)


def chicken_coop(seed=1, at=(0, 0, 0)):
    """A hen house on legs, 1 x 1: a little box of boards raised off the ground against foxes, a pent
    roof sloping to the back, a pop-hole with a slatted ramp down to the ground, a nesting box on its
    side with a lid."""
    bm = bmesh.new()
    BOARD, FRAME, ROOF, DARK = range(4)
    w, d, z0, z1 = 0.42, 0.34, 0.35, 0.95
    for x in (-w + 0.04, w - 0.04):
        for y in (-d + 0.04, d - 0.04):
            P.box(bm, x - 0.035, y - 0.035, 0, x + 0.035, y + 0.035, z0 + 0.05, FRAME)
    P.box(bm, -w, -d, z0, w, d, z1, BOARD)
    A.hull(bm, [(x, y, z) for x in (-w - 0.07, w + 0.07) for y, z in ((-d - 0.1, z1 + 0.2), (d + 0.1, z1 - 0.02))] +
           [(x, y, z + 0.04) for x in (-w - 0.07, w + 0.07) for y, z in ((-d - 0.1, z1 + 0.2), (d + 0.1, z1 - 0.02))], ROOF)
    A.hull(bm, [(x, -d, z1 + 0.21) for x in (-w, w)] + [(x, -d, z1) for x in (-w, w)] +
           [(x, d, z1) for x in (-w, w)], BOARD)                                                       # the gable under the pent
    P.box(bm, -0.09, -d - 0.006, z0 + 0.03, 0.09, -d, z0 + 0.22, DARK)                                  # the pop-hole
    a, b = Vector((0.0, -d - 0.02, z0 + 0.02)), Vector((0.0, -d - 0.7, 0.0))
    P.bar(bm, a, b, 0.2, 0.025, BOARD)
    for k in range(4):
        p = a + (b - a) * (0.15 + 0.22 * k) + Vector((0, 0, 0.02))
        P.bar(bm, p - Vector((0.1, 0, 0)), p + Vector((0.1, 0, 0)), 0.02, 0.02, FRAME)
    P.box(bm, w, -0.2, z0 + 0.1, w + 0.25, 0.2, z0 + 0.4, BOARD)                                        # the nesting box
    A.hull(bm, [(w, y, z0 + 0.48) for y in (-0.23, 0.23)] + [(w + 0.29, y, z0 + 0.38) for y in (-0.23, 0.23)] +
           [(w, y, z0 + 0.45) for y in (-0.23, 0.23)] + [(w + 0.29, y, z0 + 0.35) for y in (-0.23, 0.23)], ROOF)
    return P.make("ChickenCoop", bm, ["Planks", "DeadWood", "Boards", "Charcoal"], "Dressing", at)


def coffin_boards(seed=1, at=(0, 0, 0)):
    """Spare coffin boards and a lid leaning against a wall (at +Y), the joiner's stock in the
    undercroft: five sawn boards of different lengths, a lid in front of them, offcuts at their feet."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    BOARD, EDGE = range(2)
    for k in range(5):
        x = -0.55 + 0.24 * k + rnd.uniform(-0.03, 0.03)
        h = rnd.uniform(1.5, 1.95)
        P.bar(bm, (x, -0.12 - 0.02 * k, 0.03), (x + rnd.uniform(-0.05, 0.05), 0.16, h), 0.022, 0.2, BOARD, up=(0, -1, 0))
    out = [(0.16, 0.95), (0.27, 0.55), (0.18, -0.95)]
    pts = [(w, y) for w, y in out] + [(-w, y) for w, y in reversed(out)]
    lean = math.radians(14)
    A.hull(bm, [(0.2 + x, -0.32 + (y + 0.95) * math.sin(lean) * 0.1 - t, 0.02 + (y + 0.95) * math.cos(lean)) for x, y in pts
                for t in (0.0, 0.035)], BOARD)
    for s in (-1, 1):
        A.hull(bm, [(0.2 + s * 0.07 + dx, -0.36 + (y + 0.95) * math.sin(lean) * 0.1, 0.05 + (y + 0.95) * math.cos(lean))
                    for y in (-0.9, 0.9) for dx in (-0.006, 0.006)] + [(0.2 + s * 0.07, -0.352, 0.05)], EDGE)
    for k in range(3):
        x, y = rnd.uniform(-0.5, 0.5), rnd.uniform(-0.55, -0.4)
        P.box(bm, x, y, 0, x + rnd.uniform(0.2, 0.35), y + 0.18, 0.025, BOARD)
    return P.make("CoffinBoards", bm, ["Boards", "DeadWood"], "Dressing", at)


def gutting_table(seed=1, at=(0, 0, 0)):
    """The fishers' gutting table on the shore, 2 x 1: driftwood boards on four stakes, the day's
    fish laid on it, a gutting knife, a bucket of guts beside it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    BOARD, STAKE, FISH, BLADE, BUCKET, GUTS = range(6)
    H = 0.82
    P.planks(bm, -0.85, 0.85, -0.35, 0.35, H - 0.05, H, 3, BOARD, gap=0.02)
    for x in (-0.72, 0.72):
        for y in (-0.25, 0.25):
            N.rod(bm, [(x, y, -0.05), (x + rnd.uniform(-0.03, 0.03), y, H - 0.05)], 0.045, mat=STAKE, sides=5)
        P.bar(bm, (x, -0.3, H - 0.1), (x, 0.3, H - 0.1), 0.05, 0.05, STAKE)
    for k in range(4):
        P.fish(bm, (-0.6 + 0.3 * k + rnd.uniform(-0.04, 0.04), rnd.uniform(-0.15, 0.15), H + 0.03), rnd.uniform(-0.4, 0.4), FISH)
    A.hull(bm, [(0.45, -0.22, H), (0.65, -0.24, H), (0.45, -0.18, H), (0.65, -0.235, H + 0.01), (0.45, -0.2, H + 0.012)], BLADE)
    P.rod(bm, (0.3, -0.21, H + 0.012), (0.45, -0.2, H + 0.012), 0.012, STAKE, sides=4)
    A.loft(bm, [((1.15, -0.1, 0.0), 0.15, 0.15), ((1.15, -0.1, 0.32), 0.17, 0.17)], sides=8, ref=(1, 0, 0), mats=BUCKET)
    A.prism(bm, 8, 0.15, 0.15, 0.24, 0.25, center=(1.15, -0.1), mat_index=GUTS)
    return P.make("GuttingTable", bm, ["Driftwood", "DeadWood", "Ashgrey", "Iron", "Planks", "BloodDeep"], "Dressing", at)


def hanging_herbs(seed=1, at=(0, 0, 0)):
    """Herbs drying from a rafter, 1 x 1: a pole hung on two cords from the ceiling (the storey's
    2.6), bunches of herbs tied to it upside down, stems up and leaves hanging."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    POLE, CORD, *HERBS = range(5)
    z = 2.0
    P.rod(bm, (-0.5, 0, z), (0.5, 0, z), 0.025, POLE, sides=5)
    for x in (-0.42, 0.42):
        P.rod(bm, (x, 0, z), (x, 0, 2.6), 0.008, CORD, sides=3)
    for k in range(6):
        x = -0.36 + 0.145 * k
        top = Vector((x, 0, z - 0.03))
        L = rnd.uniform(0.32, 0.45)
        P.rod(bm, top + Vector((0, 0, 0.03)), top - Vector((0, 0, 0.06)), 0.012, CORD, sides=3)
        A.loft(bm, [(top - Vector((0, 0, 0.05)), 0.02, 0.02), (top - Vector((0, 0, 0.12)), 0.05, 0.045),
                    (top - Vector((0, 0, L * 0.75)), 0.075, 0.065), (top - Vector((0, 0, L)), 0.0, 0.0)],
               sides=6, ref=(1, 0, 0), mats=HERBS[k % 3])
    return P.make("HangingHerbs", bm, ["Timber", "Umber", "Leaves", "Yarrow", "Heather"], "Dressing", at, smooth=45)


def hide_frame(seed=1, at=(0, 0, 0)):
    """Hides drying on a frame in the skinning yard, 2 x 1: two square frames of poles lashed at the
    corners and propped leaning back, a hide stretched in each on cords, scraped pale on the flesh
    side that faces out (-Y)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    POLE, HIDE_, CORD = range(3)
    lean = 0.25
    for cx in (-0.5, 0.5):
        corners = [Vector((cx + sx * 0.42, sz * lean, sz)) for sx, sz in ((-1, 0.0), (1, 0.0), (1, 1.5), (-1, 1.5))]
        for a, b in zip(corners, corners[1:] + corners[:1]):
            ext = (b - a).normalized() * 0.08
            N.rod(bm, [a - ext, b + ext], 0.03, mat=POLE, sides=4)
        N.rod(bm, [corners[2] + Vector((-0.42, 0.0, 0.0)), Vector((cx, 0.75, -0.02))], 0.025, mat=POLE, sides=4)   # the prop
        outline = []
        for k in range(10):
            a = 2 * math.pi * k / 10
            outline.append((cx + 0.33 * math.cos(a) * rnd.uniform(0.85, 1.0), 0.75 + 0.6 * math.sin(a) * rnd.uniform(0.88, 1.0)))
        A.hull(bm, [(x, (z * lean / 1.5) * 1.0 + t, z) for x, z in outline for t in (-0.01, 0.0)], HIDE_)
        for x, z in outline[::2]:                                                                     # cords from its edge to the frame
            fx = cx + math.copysign(0.42, x - cx) if abs(x - cx) > 0.2 else x
            fz = z if abs(x - cx) > 0.2 else (1.5 if z > 0.75 else 0.0)
            N.rod(bm, [(x, z * lean / 1.5, z), (fx, fz * lean / 1.5, fz)], 0.006, mat=CORD, sides=3)
    return P.make("HideFrame", bm, ["DeadWood", "Hide", "Umber"], "Dressing", at)


STALLS = {                                     # goods: (awning, the goods' colours)
    "produce": ("Ochre", ["Thatch", "Russet", "Leaves"]),
    "fish": ("Moor", ["Planks", "Ashgrey", "Shallows"]),
    "cloth": ("Hide", ["Linen", "Russet", "Indigo", "Wool"]),
}


def market_stall(seed=1, at=(0, 0, 0), goods="produce"):
    """A market stall in the square, 2 x 1: a trestle counter toward the buyer (-Y), four posts and
    a cloth awning sloping down over the front, crates under the counter, the goods on it (STALLS):
    produce in baskets, fish laid in shallow crates, or cloth in bolts and folded lengths."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    awning, colours = STALLS[goods]
    BOARD, POST, CLOTH = range(3)
    G = [3 + k for k in range(len(colours))]
    x0, x1, y0, y1, H = -0.95, 0.95, -0.4, 0.4, 0.85
    P.planks(bm, x0, x1, y0, y0 + 0.5, H - 0.04, H, 2, BOARD)
    P.box(bm, x0 + 0.05, y0 + 0.02, H - 0.35, x1 - 0.05, y0 + 0.04, H - 0.04, BOARD)                    # the valance board
    for x in (x0 + 0.04, x1 - 0.04):
        for y, h in ((y0 + 0.04, 1.95), (y1 - 0.04, 2.25)):
            P.box(bm, x - 0.035, y - 0.035, 0, x + 0.035, y + 0.035, h, POST)
        P.bar(bm, (x, y0 + 0.04, 1.95), (x, y1 - 0.04, 2.25), 0.04, 0.04, POST)
    A.hull(bm, [(x, y, z + t) for x in (x0 - 0.1, x1 + 0.1) for y, z in ((y0 - 0.3, 1.85), (y1, 2.27)) for t in (0.0, 0.02)], CLOTH)
    for k in range(5):                                                                                # the scalloped hem
        x = x0 - 0.1 + (k + 0.5) * (x1 - x0 + 0.2) / 5
        A.hull(bm, [(x - 0.17, y0 - 0.3, 1.85), (x + 0.17, y0 - 0.3, 1.85), (x, y0 - 0.31, 1.72),
                    (x - 0.17, y0 - 0.29, 1.85), (x + 0.17, y0 - 0.29, 1.85)], CLOTH)
    if goods == "produce":                                                                            # baskets of produce
        for x in (-0.55, 0.0, 0.55):
            A.loft(bm, [((x, y0 + 0.25, H), 0.17, 0.13), ((x, y0 + 0.25, H + 0.12), 0.2, 0.15)], sides=8, ref=(1, 0, 0), mats=G[0])
            blob(bm, (x, y0 + 0.25, H + 0.11), (0.17, 0.12, 0.06), 1, rnd, jitter=(0.85, 1.1), mat=G[1] if x else G[2])
    elif goods == "fish":                                                                             # fish laid in shallow crates
        for x in (-0.52, 0.0, 0.52):
            P.box(bm, x - 0.24, y0 + 0.06, H, x + 0.24, y0 + 0.44, H + 0.08, G[0])
            P.box(bm, x - 0.21, y0 + 0.09, H + 0.075, x + 0.21, y0 + 0.41, H + 0.077, G[2])
            for j in range(3):
                P.fish(bm, (x + rnd.uniform(-0.05, 0.05), y0 + 0.14 + 0.11 * j, H + 0.1), rnd.uniform(-0.3, 0.3) + (math.pi if j % 2 else 0), G[1], 0.9)
    else:                                                                                              # bolts and folded lengths of cloth
        for k, x in enumerate((-0.6, -0.32, -0.04)):
            A.loft(bm, [((x, y0 + 0.05, H + 0.07), 0.07, 0.07), ((x, y0 + 0.45, H + 0.07), 0.07, 0.07)], sides=8, ref=(0, 0, 1), mats=G[k])
        for k in range(4):
            P.box(bm, 0.25, y0 + 0.06, H + 0.04 * k, 0.75, y0 + 0.42, H + 0.04 * (k + 1) - 0.004, G[(k + 1) % 4])
    for x in (-0.5, 0.45):
        P.box(bm, x - 0.25, -0.05, 0, x + 0.25, 0.35, 0.4, BOARD)                                     # crates under it
    name = {"produce": "MarketStall", "fish": "MarketStallFish", "cloth": "MarketStallCloth"}[goods]
    return P.make(name, bm, ["Planks", "DeadWood", awning] + colours, "Dressing", at, smooth=30)


def millstones(seed=1, at=(0, 0, 0)):
    """Millstones in the miller's yard, 2 x 1: two leaning against a wall (at +Y), worn out and
    waiting to be dressed, a third lying flat in front; each a thick disc with the square eye in its
    middle and its grinding furrows painted on the face."""
    bm = bmesh.new()
    STONE_, EYE, FURROW = range(3)

    def stone(c, axis, r, t):
        rings, faces = A.loft(bm, [(tuple(c - axis * t), r, r), (tuple(c + axis * t), r, r)], sides=12,
                              ref=(0, 0, 1) if abs(axis.z) < 0.9 else (1, 0, 0), mats=STONE_)
        face = c - axis * (t + 0.003)
        side = axis.cross(Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))).normalized()
        up = side.cross(axis).normalized()
        A.hull(bm, [tuple(face + side * sx * 0.07 + up * sy * 0.07 + axis * dz) for sx in (-1, 1) for sy in (-1, 1) for dz in (0, -0.002)], EYE)
        for k in range(6):                                                                            # furrows, from the eye outwards
            a = 2 * math.pi * k / 6
            d = side * math.cos(a) + up * math.sin(a)
            e = side * -math.sin(a) + up * math.cos(a)
            A.hull(bm, [tuple(face + d * 0.12 + e * w + axis * dz) for w in (-0.015, 0.015) for dz in (0, -0.002)] +
                   [tuple(face + d * (r * 0.9) + e * (w + 0.06) + axis * dz) for w in (-0.015, 0.015) for dz in (0, -0.002)], FURROW)
    lean = math.radians(12)
    for x in (-0.45, 0.45):
        axis = Vector((0, math.cos(lean), -math.sin(lean)))              # its face (at -axis) toward the yard
        stone(Vector((x, 0.18, 0.55)), axis, 0.55, 0.11)
    stone(Vector((0.1, -0.55, 0.1)), Vector((0, 0, -1)), 0.5, 0.1)
    return P.make("Millstones", bm, ["Gravestone", "Charcoal", "Ashgrey"], "Dressing", at, smooth=25)


def net_rack(seed=1, at=(0, 0, 0)):
    """Nets hung to dry on the shore, 2 x 1: three tall driftwood poles carrying a cross-pole, the
    nets draped over it in loose sheets that sag between the poles, floats of cork along the
    headrope."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    POLE, NET, FLOAT = range(3)
    H = 1.9
    for x in (-0.9, 0.0, 0.9):
        N.rod(bm, N.wobble(rnd, (x, 0, -0.1), (x + rnd.uniform(-0.04, 0.04), 0, H + 0.15), 3, 0.02), 0.05, mat=POLE, sides=5)
    N.rod(bm, [(-1.05, 0, H), (0.0, 0, H + 0.03), (1.05, 0, H)], 0.035, mat=POLE, sides=5)
    for x0, x1 in ((-0.85, -0.05), (0.05, 0.85)):
        for side in (-1, 1):                                                                          # each net hangs both sides of the pole
            drop = rnd.uniform(1.1, 1.5) if side < 0 else rnd.uniform(0.6, 0.9)
            pts = [(x0, side * 0.04, H), (x1, side * 0.04, H), (x0 + 0.05, side * 0.14, H - drop),
                   (x1 - 0.05, side * 0.14, H - drop + rnd.uniform(-0.1, 0.1)), ((x0 + x1) / 2, side * 0.2, H - drop - 0.12)]
            A.hull(bm, pts + [(x, y + side * 0.012, z) for x, y, z in pts], NET)
        for k in range(4):
            x = x0 + (k + 0.5) * (x1 - x0) / 4
            A.loft(bm, [((x, -0.06, H - 0.04), 0.035, 0.035), ((x, 0.06, H - 0.04), 0.035, 0.035)], sides=5, ref=(0, 0, 1), mats=FLOAT)
    return P.make("NetRack", bm, ["Driftwood", "Umber", "Ochre"], "Dressing", at, smooth=30)


def notice_board(seed=1, at=(0, 0, 0)):
    """The square's notice board, 2 x 1: a broad board between two posts at reading height, a little
    pent roof over it against the rain, notices pinned to it at all angles."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    POST, BOARD, ROOF, PAPER, INK = range(5)
    for x in (-0.75, 0.75):
        P.box(bm, x - 0.06, -0.06, 0, x + 0.06, 0.06, 2.15, POST)
    P.box(bm, -0.69, -0.03, 0.85, 0.69, 0.03, 1.85, BOARD)
    P.box(bm, -0.74, -0.06, 1.85, 0.74, 0.06, 1.9, POST)
    A.hull(bm, [(x, y, z + t) for x in (-0.92, 0.92) for y, z in ((-0.35, 2.0), (0.15, 2.22)) for t in (0.0, 0.04)], ROOF)
    for k in range(7):
        w, h = rnd.uniform(0.16, 0.26), rnd.uniform(0.2, 0.32)
        cx, cz = rnd.uniform(-0.5, 0.5), rnd.uniform(1.05, 1.65)
        a = rnd.uniform(-0.15, 0.15)
        corners = [(cx + dx * math.cos(a) - dz * math.sin(a), cz + dx * math.sin(a) + dz * math.cos(a))
                   for dx, dz in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2))]
        y = -0.032 - 0.002 * k
        bm.faces.new([bm.verts.new((x, y, z)) for x, z in corners]).material_index = PAPER
        for j in range(3):                                                                            # the hand on it, painted
            zz = cz + h / 2 - 0.06 - j * 0.06
            bm.faces.new([bm.verts.new((cx + dx, y - 0.001, zz + dz)) for dx, dz in
                          ((-w * 0.35, 0), (w * 0.35, 0), (w * 0.35, 0.015), (-w * 0.35, 0.015))]).material_index = INK
    return P.make("NoticeBoard", bm, ["Timber", "Boards", "Planks", "Linen", "Charcoal"], "Dressing", at)


def toll_bar(seed=1, at=(0, 0, 0)):
    """The old toll-house's bar, fallen in the verge, 3 x 1: the long pole gate lying in the grass along
    X, its heavy butt and the iron collar it swung on at one end, the stump of its post still standing
    there, the rest post at the far end leaning over."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    POLE, POST, IRON, PAINT = range(4)
    A.loft(bm, [((-1.3, 0.0, 0.12), 0.11, 0.11), ((-0.9, 0.02, 0.1), 0.09, 0.09), ((0.4, 0.06, 0.07), 0.065, 0.065),
                ((1.45, 0.1, 0.05), 0.045, 0.045)], sides=6, ref=(0, 0, 1), mats=POLE, roll=0.3)
    for k in range(3):                                                                               # bands of old paint, mostly gone
        x = -0.4 + 0.6 * k
        A.loft(bm, [((x, 0.03 + 0.03 * k, 0.085 - 0.012 * k), 0.075 - 0.006 * k, 0.075 - 0.006 * k),
                    ((x + 0.2, 0.035 + 0.03 * k, 0.08 - 0.012 * k), 0.073 - 0.006 * k, 0.073 - 0.006 * k)],
               sides=6, ref=(0, 0, 1), mats=PAINT, roll=0.3)
    A.loft(bm, [((-1.12, 0.0, 0.11), 0.125, 0.125), ((-1.02, 0.0, 0.11), 0.125, 0.125)], sides=6, ref=(0, 0, 1), mats=IRON)
    N.cut_stub(bm, (-1.3, -0.4, -0.05), 0.11, 0.55, 0.15, rnd, sides=5)
    A.loft(bm, [((-1.3, -0.4, 0.42), 0.12, 0.12), ((-1.3, -0.4, 0.47), 0.12, 0.12)], sides=5, ref=(1, 0, 0), mats=IRON)
    N.rod(bm, [(1.3, -0.35, -0.05), (1.36, -0.45, 0.45), (1.48, -0.5, 0.8)], 0.06, mat=POST, sides=5)
    N.rod(bm, [(1.36, -0.45, 0.62), (1.52, -0.45, 0.72)], 0.03, mat=POST, sides=4)                     # the crook the bar rested in
    return P.make("TollBar", bm, ["DeadWood", "Bark", "Iron", "Ashgrey"], "Dressing", at, smooth=35)


def charnel_recess(seed=1, at=(0, 0, 0)):
    """A charnel recess in the undercroft wall, 1 x 1: a round-headed arch in rubble stone standing
    proud of the wall behind it (+Y), its niche filled with long bones stacked like firewood, ends
    out, and a row of skulls along the top facing the room."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    STONE_, BACK, BONE, DARK = range(4)
    half, spring, d = 0.42, 0.95, 0.28
    w = 0.16
    P.box(bm, -half - w, -0.02, 0, half + w, d, 0.2, STONE_)                                           # the sill
    for s in (-1, 1):
        P.box(bm, s * half, -0.02, 0.2, s * (half + w), d, spring, STONE_)
    arch = P.arch(0.0, spring, half, n=6)
    outer = P.arch(0.0, spring, half + w, n=6)
    for (a, b), (c, e) in zip(zip(arch, arch[1:]), zip(outer, outer[1:])):
        A.hull(bm, [(x, y, z) for (x, z) in (a, b, c, e) for y in (-0.02, d)], STONE_)
    P.box(bm, -half - w, d - 0.04, 0.2, half + w, d, spring + half + w, BACK)
    for row in range(5):                                                                              # long bones, ends out
        z = 0.25 + 0.085 * row
        for k in range(8):
            x = -0.36 + 0.103 * k + (0.05 if row % 2 else 0.0)
            if abs(x) > half - 0.06:
                continue
            P.rod(bm, (x, 0.02, z), (x, d - 0.04, z), 0.032, BONE, sides=4)
            A.loft(bm, [((x, 0.005, z), 0.042, 0.042), ((x, 0.03, z), 0.042, 0.042)], sides=4, ref=(0, 0, 1), mats=BONE)
    for k in range(5):                                                                                # skulls on top, facing out
        x = -0.32 + 0.16 * k
        c = Vector((x + rnd.uniform(-0.01, 0.01), 0.08, 0.77))
        blob(bm, tuple(c), (0.07, 0.08, 0.075), 1, rnd, jitter=(0.95, 1.04), mat=BONE)
        for dx in (-0.025, 0.025):
            A.paint(bm, tuple(c + Vector((dx, -0.08, 0.0))), DARK, facing=(0, -1, 0), size=0.5)
    return P.make("CharnelRecess", bm, ["Stone", "CryptDarkStone", "SaintBone", "Charcoal"], "Dressing", at, smooth=30)


def ladder(seed=1, at=(0, 0, 0)):
    """A ladder leaning on a wall (at +Y) or a stack: two split poles, round rungs a foot apart, a
    little crooked; its foot out at -Y, its top at the eaves."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    foot, top = Vector((0, -0.75, 0.0)), Vector((0, 0.05, 2.7))
    for s in (-1, 1):
        N.rod(bm, wobble(rnd, foot + Vector((s * 0.22, 0, 0)), top + Vector((s * 0.2, 0, 0.1)), 3, 0.012), 0.03, mat=0, sides=5)
    for k in range(1, 9):
        p = foot.lerp(top, k / 9)
        P.rod(bm, p + Vector((-0.23, 0.01, 0)), p + Vector((0.23, 0.01, 0)), 0.018, 1, sides=4)
    return P.make("Ladder", bm, ["DeadWood", "Timber"], "Dressing", at)


def grindstone(seed=1, at=(0, 0, 0)):
    """A grindstone for blades: a sandstone wheel on an iron spindle in a timber frame, a crank at
    one end, a trough of water under the wheel to wet it, a seat on the frame's end."""
    bm = bmesh.new()
    STONE_, FRAME, IRON, WATER = range(4)
    for y in (-0.22, 0.22):
        P.box(bm, -0.55, y - 0.05, 0.35, 0.55, y + 0.05, 0.45, FRAME)
        for x in (-0.45, 0.45):
            P.bar(bm, (x + 0.1, y, 0.0), (x, y, 0.4), 0.07, 0.07, FRAME)
        P.box(bm, -0.04, y - 0.05, 0.45, 0.04, y + 0.05, 0.75, FRAME)
    A.loft(bm, [((0, -0.08, 0.72), 0.36, 0.36), ((0, 0.08, 0.72), 0.36, 0.36)], sides=14, ref=(0, 0, 1), mats=STONE_)
    P.rod(bm, (0, -0.32, 0.72), (0, 0.3, 0.72), 0.022, IRON)
    P.rod(bm, (0, -0.32, 0.72), (0.18, -0.32, 0.6), 0.016, IRON, sides=4)
    P.rod(bm, (0.18, -0.32, 0.6), (0.18, -0.45, 0.6), 0.02, FRAME, sides=5)
    P.box(bm, -0.3, -0.16, 0.2, 0.3, 0.16, 0.34, FRAME)                                              # the trough under it
    P.box(bm, -0.27, -0.13, 0.31, 0.27, 0.13, 0.32, WATER)
    P.box(bm, 0.4, -0.25, 0.45, 0.6, 0.25, 0.5, FRAME)                                               # the seat
    return P.make("Grindstone", bm, ["Sandstone", "Timber", "Iron", "Mere"], "Dressing", at, smooth=30)


def sawhorse(seed=1, at=(0, 0, 0)):
    """A sawhorse with a log on it half sawn through, the saw left in the cut, sawdust and a sawn-off
    round on the ground."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    FRAME, BARK_, END, IRON, DUST = range(5)
    for x in (-0.35, 0.35):
        for s in (-1, 1):
            P.bar(bm, (x, s * 0.28, 0.0), (x, -s * 0.05, 0.75), 0.05, 0.05, FRAME)
    P.bar(bm, (-0.35, 0.0, 0.3), (0.35, 0.0, 0.3), 0.04, 0.04, FRAME)
    log(bm, Vector((-0.75, 0.0, 0.72)), Vector((0.8, 0.02, 0.72)), 0.13, rnd)
    A.hull(bm, [(0.55 + dx, y, z) for dx in (-0.005, 0.005) for y, z in ((-0.12, 0.83), (0.12, 0.83), (-0.25, 1.2), (0.25, 1.15))], IRON)
    P.rod(bm, (0.55, -0.25, 1.2), (0.55, -0.32, 1.28), 0.02, FRAME, sides=4)
    N.flat_chip(bm, (0.55, 0.0, 0.0), 0.2, rnd, DUST, h=0.04)
    rings, faces = A.loft(bm, [((1.0, -0.3, 0.12), 0.13, 0.13), ((1.0, -0.3, 0.0), 0.13, 0.13)], sides=6, ref=(1, 0, 0), mats=BARK_)
    for f in faces:
        if len(f.verts) == 6:
            f.material_index = END
    return P.make("Sawhorse", bm, ["Timber", "Bark", "Ash", "Iron", "Wheat"], "Dressing", at)


def cooking_tripod(seed=1, at=(0, 0, 0)):
    """A cooking tripod over a fire pit: three poles lashed at the top, a chain hanging from them
    with a black iron pot over the fire's ring of stones (the flames are the game's to draw)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    POLE, IRON, STONE_, ASH = range(4)
    apex = Vector((0, 0, 1.6))
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.3
        foot = Vector((0.75 * math.cos(a), 0.75 * math.sin(a), -0.03))
        N.rod(bm, [foot, apex + (apex - foot).normalized() * 0.15], 0.035, mat=POLE, sides=5)
    A.loft(bm, [(apex - Vector((0, 0, 0.06)), 0.06, 0.06), (apex + Vector((0, 0, 0.04)), 0.06, 0.06)], sides=6, ref=(1, 0, 0), mats=POLE)
    for k in range(6):                                                                                # the chain's links
        z = 1.55 - 0.1 * k
        w, d = (0.012, 0.025) if k % 2 else (0.025, 0.012)
        P.box(bm, -w, -d, z - 0.045, w, d, z + 0.045, IRON)
    A.loft(bm, [((0, 0, 0.55), 0.15, 0.15), ((0, 0, 0.7), 0.22, 0.22), ((0, 0, 0.88), 0.2, 0.2), ((0, 0, 0.9), 0.21, 0.21)],
           sides=10, ref=(1, 0, 0), mats=IRON)
    N.rod(bm, [(-0.2, 0, 0.9), (0, 0, 1.0), (0.2, 0, 0.9)], 0.008, mat=IRON, sides=3)
    A.prism(bm, 9, 0.38, 0.34, -0.02, 0.025, mat_index=ASH)
    for k in range(8):
        a = 2 * math.pi * k / 8 + rnd.uniform(-0.1, 0.1)
        c = Vector((0.44 * math.cos(a), 0.44 * math.sin(a), 0))
        A.hull(bm, [tuple(c + Vector((rnd.uniform(-0.09, 0.09), rnd.uniform(-0.08, 0.08), rnd.uniform(-0.02, 0.1)))) for _ in range(7)], STONE_)
    return P.make("CookingTripod", bm, ["DeadWood", "Blackiron", "Scree", "AshPlain"], "Dressing", at, smooth=40)


def leaking_cask(seed=5, at=(0, 0, 0)):
    """The cask in Sour Hollow that the mites gather round: a great cask lying on its side in the
    grass, half sunk, a stave sprung and its hoops rusted through, what leaks from it dried to a pale
    salt crust in a spreading stain, the ground dead round it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    STAVES, IRON, CRUST, STAIN, DARK = range(5)
    L, r0, r1 = 0.65, 0.42, 0.5

    def radius(x):
        return r0 + (r1 - r0) * math.cos(math.pi * x / (2 * L)) ** 0.7
    xs = [-L, -L * 0.6, 0.0, L * 0.6, L]
    n = 12
    rings = [[bm.verts.new((x, radius(x) * math.cos(2 * math.pi * j / n), radius(x) * 0.95 * math.sin(2 * math.pi * j / n) + 0.3))
              for j in range(n)] for x in xs]
    for a, b in zip(rings, rings[1:]):
        for j in range(n):
            if a is rings[2] and j in (2,):                                                    # the sprung stave: a dark gap
                bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j])).material_index = DARK
                continue
            bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j])).material_index = STAVES
    bm.faces.new(list(reversed(rings[0]))).material_index = STAVES
    bm.faces.new(rings[-1]).material_index = STAVES
    for x in (-L * 0.8, -L * 0.3, L * 0.8):                                                    # the hoops, one burst
        r = radius(x) + 0.012
        A.loft(bm, [((x - 0.025, 0, 0.3), r, r * 0.95), ((x + 0.025, 0, 0.3), r, r * 0.95)], sides=n, ref=(0, 0, 1), mats=IRON)
    P.bar(bm, (L * 0.3, 0.45, 0.62), (L * 0.55, 0.62, 0.35), 0.06, 0.02, STAVES)                 # the sprung stave
    A.hull(bm, [(math.cos(a) * rr + 0.2, math.sin(a) * rr * 0.8 - 0.55, 0.003) for a in (2 * math.pi * k / 10 for k in range(10))
                for rr in (1.2 * rnd.uniform(0.85, 1.1),)] + [(0.2, -0.55, 0.012)], STAIN)       # the dead ground
    for _ in range(9):                                                                          # the salt crust
        x, y, s = rnd.uniform(-0.6, 0.9), rnd.uniform(-1.1, -0.1), rnd.uniform(0.06, 0.15)
        A.hull(bm, [(x + rnd.uniform(-s, s), y + rnd.uniform(-s, s), 0.01) for _ in range(6)] + [(x, y, 0.035)], CRUST)
    return P.make("LeakingCask", bm, ["Oak", "Iron", "SaltFlat", "SourGround", "Charcoal"], "Dressing", at, smooth=30)


def chest_tomb(seed=1, at=(0, 0, 0)):
    """A chest tomb in the churchyard, 1 x 2 (along Y): a stone box with a moulded top slab, its sides
    in sunk panels, lichen on the slab, leaning a little where the ground has given."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    STONE_, LICHEN = range(2)
    W, L = 0.42, 0.95
    P.box(bm, -W - 0.05, -L - 0.05, -0.05, W + 0.05, L + 0.05, 0.12, STONE_)
    P.box(bm, -W, -L, 0.12, W, L, 0.72, STONE_)
    for s in (-1, 1):                                                                           # the sunk panels, painted darker
        for y in (-0.5, 0.0, 0.5):
            P.box(bm, s * W - 0.004, y - 0.18, 0.22, s * W + 0.004, y + 0.18, 0.62, LICHEN)
    A.hull(bm, [(x, y, z) for x in (-W - 0.08, W + 0.08) for y in (-L - 0.08, L + 0.08) for z in (0.72, 0.82)] +
           [(x, y, 0.86) for x in (-W - 0.02, W + 0.02) for y in (-L - 0.02, L + 0.02)], STONE_)
    for _ in range(4):                                                                          # lichen on the slab
        x, y = rnd.uniform(-W + 0.1, W - 0.1), rnd.uniform(-L + 0.1, L - 0.1)
        A.hull(bm, [(x + rnd.uniform(-0.12, 0.12), y + rnd.uniform(-0.12, 0.12), 0.861) for _ in range(5)] + [(x, y, 0.864)], LICHEN)
    tilt = math.radians(2.5)
    for v in bm.verts:
        v.co.z += v.co.x * math.sin(tilt)
    return P.make("ChestTomb", bm, ["Gravestone", "MossyStone"], "Dressing", at)


def ledger_stone(seed=2, at=(0, 0, 0)):
    """A ledger stone flat in the churchyard grass over one of the drowned, 1 x 2 (along Y): a worn
    slab sunk almost level with the turf, its lettering a few dark worn strokes, a crack across it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    STONE_, CUT, TURF = range(3)
    A.hull(bm, [(x * rnd.uniform(0.97, 1.03), y, z) for x in (-0.4, 0.4) for y in (-0.92, 0.92) for z in (-0.05, 0.04)], STONE_)
    for k in range(4):
        y = 0.55 - 0.18 * k
        w = rnd.uniform(0.18, 0.28)
        P.box(bm, -w, y, 0.04, w, y + 0.035, 0.043, CUT)
    A.hull(bm, [(-0.4, -0.2, 0.041), (0.4, -0.35, 0.041), (0.4, -0.33, 0.044), (-0.4, -0.18, 0.044)], CUT)
    A.hull(bm, [(x, y, z) for x in (-0.48, 0.48) for y in (-1.0, 1.0) for z in (-0.02, 0.0)], TURF)
    return P.make("LedgerStone", bm, ["Gravestone", "Charcoal", "GraveTurf"], "Dressing", at)


def stocks(seed=1, at=(0, 0, 0)):
    """The village stocks by the square: two posts with a pair of heavy boards between them, the
    upper one hinged up a little, the holes for the ankles cut in their meeting edge, a low bench
    behind for the one sitting in them, an iron hasp."""
    bm = bmesh.new()
    POST, BOARD, IRON, DARK = range(4)
    for x in (-0.65, 0.65):
        P.box(bm, x - 0.07, -0.07, -0.05, x + 0.07, 0.07, 0.75, POST)
        A.hull(bm, [(x + dx, dy, 0.75) for dx in (-0.08, 0.08) for dy in (-0.08, 0.08)] + [(x, 0, 0.85)], POST)
    P.box(bm, -0.58, -0.05, 0.12, 0.58, 0.05, 0.3, BOARD)                                        # the lower board
    A.hull(bm, [(x, y, 0.3 + z + (0.58 - x) * 0.04) for x in (-0.58, 0.58) for y in (-0.05, 0.05) for z in (0.0, 0.18)],
           BOARD)                                                                                # the upper, raised at its free end
    for x in (-0.3, -0.12, 0.12, 0.3):                                                          # the ankle holes, painted dark
        A.hull(bm, [(x + 0.06 * math.cos(a), -0.052, 0.3 + 0.06 * math.sin(a)) for a in (math.pi * k / 4 for k in range(-4, 5))] +
               [(x, -0.054, 0.3)], DARK)
    P.box(bm, 0.5, -0.06, 0.28, 0.56, 0.06, 0.4, IRON)
    P.box(bm, -0.6, 0.35, 0.35, 0.6, 0.6, 0.4, BOARD)                                            # the bench behind
    for x in (-0.5, 0.5):
        P.box(bm, x - 0.04, 0.4, 0.0, x + 0.04, 0.55, 0.35, POST)
    return P.make("Stocks", bm, ["Oak", "Timber", "Iron", "Charcoal"], "Dressing", at)


def fish_rack(seed=1, at=(0, 0, 0)):
    """Fish split and hung to dry on the fishers' shore, 2 x 1 along X: two crossed-pole ends and a
    ridge pole, the fish hung in pairs over it by their tails, flat and pale."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    POLE, FISH, CORD = range(3)
    H = 1.55
    for x in (-0.9, 0.9):
        for s in (-1, 1):
            N.rod(bm, [(x, s * 0.45, -0.05), (x, -s * 0.08, H + 0.15)], 0.035, mat=POLE, sides=4)
    N.rod(bm, [(-1.05, 0, H), (1.05, 0, H + 0.02)], 0.03, mat=POLE, sides=4)
    for k in range(7):
        x = -0.72 + 0.24 * k + rnd.uniform(-0.03, 0.03)
        for s in (-1, 1):                                                                       # a pair, one each side
            top = Vector((x, s * 0.035, H - 0.02))
            L = rnd.uniform(0.38, 0.48)
            A.hull(bm, [top, top + Vector((0.03, 0, 0)), top + Vector((-0.08, s * 0.03, -L * 0.6)), top + Vector((0.11, s * 0.03, -L * 0.6)),
                        top + Vector((0.015, s * 0.02, -L)), top + Vector((0.015, s * 0.045, -L * 0.5))], FISH)
        N.rod(bm, [(x + 0.015, -0.04, H - 0.02), (x + 0.015, 0.04, H - 0.02)], 0.012, mat=CORD, sides=3)
    return P.make("FishRack", bm, ["Driftwood", "Hide", "Umber"], "Dressing", at)


def cart_wheel(seed=1, at=(0, 0, 0)):
    """A spare cart wheel leaning on a wall (at +Y), a broken spoke, a second lying flat in the grass."""
    bm = bmesh.new()
    lean = math.radians(14)
    wheel(bm, 0.0, 0.0, 0.0, 0.48, 0, half=0.035, spokes=6, sides=12)
    from mathutils import Matrix
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(-lean, 4, "X"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((0, 0.05, 0.48 * math.cos(lean) + 0.02)), verts=bm.verts)
    n = len(bm.verts)
    wheel(bm, 0.0, 0.0, 0.0, 0.48, 0, half=0.035, spokes=6, sides=12)
    bm.verts.ensure_lookup_table()
    lying = list(bm.verts)[n:]
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(math.pi / 2, 4, "X"), verts=lying)
    bmesh.ops.translate(bm, vec=Vector((0.9, -0.5, 0.04)), verts=lying)
    return P.make("CartWheel", bm, ["DeadWood"], "Dressing", at)


def butter_churn(seed=1, at=(0, 0, 0)):
    """A plunge churn by the dairy door: a tall coopered churn narrowing to the top, two hoops, a lid
    with the dasher's handle standing out of it, a cloth over a bowl beside it."""
    bm = bmesh.new()
    STAVES, IRON, HANDLE, CLOTH, BOWL = range(5)
    A.loft(bm, [((0, 0, 0), 0.2, 0.2), ((0, 0, 0.75), 0.15, 0.15)], sides=10, ref=(1, 0, 0), mats=STAVES)
    for z in (0.12, 0.62):
        r = 0.2 - 0.05 * z / 0.75 + 0.01
        A.prism(bm, 10, r, r, z - 0.018, z + 0.018, mat_index=IRON, cap_top=False)
    A.loft(bm, [((0, 0, 0.75), 0.165, 0.165), ((0, 0, 0.79), 0.165, 0.165)], sides=10, ref=(1, 0, 0), mats=STAVES)
    P.rod(bm, (0, 0, 0.7), (0, 0, 1.25), 0.02, HANDLE)
    P.rod(bm, (-0.08, 0, 1.22), (0.08, 0, 1.22), 0.018, HANDLE)
    A.loft(bm, [((0.38, -0.05, 0), 0.08, 0.08), ((0.38, -0.05, 0.12), 0.16, 0.16)], sides=10, ref=(1, 0, 0), mats=BOWL)
    A.hull(bm, [(0.38 + 0.18 * math.cos(a), -0.05 + 0.18 * math.sin(a), 0.125) for a in (2 * math.pi * k / 8 for k in range(8))] +
           [(0.38, -0.05, 0.15)], CLOTH)
    return P.make("ButterChurn", bm, ["Planks", "Iron", "Timber", "Linen", "Ochre"], "Dressing", at, smooth=35)


def weapon_rack(seed=1, at=(0, 0, 0)):
    """The wardens' weapon rack against a wall (at +Y), 2 x 1: a frame of two posts and two rails, the
    lower notched; spears stood in it leaning back, a pair of shields hung on pegs at one end."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    FRAME, SHAFT, IRON, SHIELD, BOSS = range(5)
    for x in (-0.85, 0.85):
        P.box(bm, x - 0.05, 0.05, 0.0, x + 0.05, 0.15, 1.6, FRAME)
    P.box(bm, -0.85, 0.0, 0.35, 0.85, 0.1, 0.43, FRAME)
    P.box(bm, -0.85, 0.08, 1.35, 0.85, 0.16, 1.42, FRAME)
    for k in range(6):                                                                         # the spears
        x = -0.65 + 0.18 * k + rnd.uniform(-0.02, 0.02)
        foot, top = Vector((x, 0.0, 0.0)), Vector((x + rnd.uniform(-0.04, 0.04), 0.14, 2.1))
        P.rod(bm, foot, top, 0.018, SHAFT, sides=5)
        A.spike(bm, top - Vector((0, 0.005, 0.02)), 0.03, top + Vector((0, 0.01, 0.26)), sides=4, mat=IRON)
    for z in (0.75, 1.15):                                                                      # shields on pegs, overlapping
        c = Vector((0.55, -0.02, z))
        A.loft(bm, [(c + Vector((0, 0.04, 0)), 0.24, 0.24), (c, 0.24, 0.24), (c - Vector((0, 0.03, 0)), 0.2, 0.2)],
               sides=10, ref=(0, 0, 1), mats=SHIELD)
        A.loft(bm, [(c - Vector((0, 0.03, 0)), 0.06, 0.06), (c - Vector((0, 0.07, 0)), 0.0, 0.0)], sides=8, ref=(0, 0, 1), mats=BOSS)
    return P.make("WeaponRack", bm, ["Timber", "Oak", "Iron", "Planks", "Blackiron"], "Dressing", at, smooth=30)


def barricade(seed=1, at=(0, 0, 0)):
    """A barricade the wardens drag across the road, 2 x 1 along X: a squared beam on the ground with
    sharpened stakes driven through it crosswise both ways (a cheval-de-frise), points out."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    BEAM, STAKE, POINT = range(3)
    z = 0.45
    P.bar(bm, (-1.0, 0.0, z), (1.0, 0.0, z), 0.2, 0.2, BEAM)
    for k in range(5):
        x = -0.8 + 0.4 * k
        for a in (math.radians(40), math.radians(140)):
            d = Vector((0.0, math.cos(a + (0.0 if k % 2 else 0.12)), math.sin(a)))
            base, tip = Vector((x, 0, z)) - d * 0.55, Vector((x, 0, z)) + d * 0.65
            N.rod(bm, [base, tip], 0.04, mat=STAKE, sides=5, tip=tip + d * 0.18)
    return P.make("Barricade", bm, ["Timber", "DeadWood", "Ash"], "Dressing", at)


def reed_bundles(seed=1, at=(0, 0, 0)):
    """Reeds cut from the fens and bound in bundles for thatch, 2 x 1: bundles stood on end leaning in
    a stook, more lying in a stack beside them, each tied at its waist."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    REED, TIE, HEAD = range(3)
    for k in range(5):                                                                         # the stook
        a = 2 * math.pi * k / 5 + rnd.uniform(-0.15, 0.15)
        out = Vector((math.cos(a), math.sin(a), 0))
        foot, top = Vector((-0.45, 0, 0)) + out * 0.28, Vector((-0.45, 0, 1.55)) + out * 0.05
        mid = foot.lerp(top, 0.45)
        A.loft(bm, [(foot, 0.1, 0.1), (mid, 0.08, 0.08), (top - Vector((0, 0, 0.15)), 0.09, 0.09), (top, 0.06, 0.06)],
               sides=6, ref=(1, 0, 0), mats=[REED, REED, HEAD])
        A.loft(bm, [(mid - Vector((0, 0, 0.03)), 0.088, 0.088), (mid + Vector((0, 0, 0.03)), 0.088, 0.088)], sides=6, ref=(1, 0, 0), mats=TIE)
    for row, (n, z) in enumerate(((3, 0.1), (2, 0.28))):                                         # the stack, butts toward -Y
        for k in range(n):
            x = 0.35 + 0.2 * k + 0.1 * row
            A.loft(bm, [((x, -0.6, z), 0.1, 0.1), ((x, 0.5, z), 0.09, 0.09), ((x, 0.75, z), 0.06, 0.06)], sides=6, ref=(0, 0, 1),
                   mats=[REED, HEAD])
            A.loft(bm, [((x, -0.05, z), 0.105, 0.105), ((x, 0.02, z), 0.105, 0.105)], sides=6, ref=(0, 0, 1), mats=TIE)
    return P.make("ReedBundles", bm, ["Thatch", "Umber", "Dust"], "Dressing", at, smooth=40)


def bell_ropes(seed=1, at=(0, 0, 0)):
    """The ringing chamber's ropes, 1 x 1: two ropes coming down through holes in the ceiling (the
    storey's 2.6), each with its striped woollen grip (the sally) at a ringer's reach and its tail
    looped up on a hook in the wall behind (+Y)."""
    bm = bmesh.new()
    ROPE, SALLY, BAND, IRON = range(4)
    for x in (-0.3, 0.3):
        P.rod(bm, (x, 0, 2.6), (x, 0, 1.0), 0.016, ROPE, sides=5)
        for k in range(4):                                                                     # the sally, striped
            z = 1.35 + 0.1 * k
            A.loft(bm, [((x, 0, z), 0.04, 0.04), ((x, 0, z + 0.1), 0.04, 0.04)], sides=6, ref=(1, 0, 0), mats=SALLY if k % 2 else BAND)
        N.rod(bm, [(x, 0, 1.0), (x, 0.15, 0.8), (x * 0.6, 0.4, 1.3)], 0.014, mat=ROPE, sides=4)
        P.box(bm, x * 0.6 - 0.02, 0.4, 1.28, x * 0.6 + 0.02, 0.46, 1.34, IRON)
        A.loft(bm, [((x, 0, 2.59), 0.05, 0.05), ((x, 0, 2.6), 0.05, 0.05)], sides=6, ref=(1, 0, 0), mats=IRON)   # the ring it passes
    return P.make("BellRopes", bm, ["Wheat", "Wool", "Russet", "Iron"], "Dressing", at)


def butcher_block(seed=1, at=(0, 0, 0)):
    """A butcher's block: a thick round of end grain on three stout legs, its top dished with use and
    stained, a cleaver bitten into it, a pail under it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    BLOCK, END, IRON, STAIN, PAIL = range(5)
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.4
        P.bar(bm, (0.32 * math.cos(a), 0.32 * math.sin(a), 0.0), (0.2 * math.cos(a), 0.2 * math.sin(a), 0.45), 0.08, 0.08, BLOCK)
    A.loft(bm, [((0, 0, 0.4), 0.36, 0.36), ((0, 0, 0.85), 0.35, 0.35)], sides=10, ref=(1, 0, 0), mats=BLOCK)
    A.loft(bm, [((0, 0, 0.85), 0.35, 0.35), ((0, 0, 0.84), 0.2, 0.2)], sides=10, ref=(1, 0, 0), mats=END)
    for _ in range(3):
        x, y = rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15)
        A.hull(bm, [(x + rnd.uniform(-0.08, 0.08), y + rnd.uniform(-0.08, 0.08), 0.852) for _ in range(5)] + [(x, y, 0.854)], STAIN)
    A.hull(bm, [(0.05 + dx, -0.08, z) for dx in (-0.004, 0.004) for z in (0.8, 0.95)] +
           [(0.05 + dx, 0.12, z) for dx in (-0.004, 0.004) for z in (0.82, 0.95)], IRON)              # the cleaver's blade
    P.rod(bm, (0.05, 0.12, 0.92), (0.05, 0.35, 0.98), 0.018, BLOCK, sides=5)
    A.loft(bm, [((0.45, -0.3, 0), 0.13, 0.13), ((0.45, -0.3, 0.26), 0.15, 0.15)], sides=8, ref=(1, 0, 0), mats=PAIL)
    return P.make("ButcherBlock", bm, ["Oak", "Ash", "Iron", "BloodDeep", "Planks"], "Dressing", at)


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
    "Handcart": handcart,
    "Wheelbarrow": wheelbarrow,
    "FarmTools": farm_tools,
    "FlourSacks": flour_sacks,
    "Haystack": haystack,
    "ChickenCoop": chicken_coop,
    "CoffinBoards": coffin_boards,
    "GuttingTable": gutting_table,
    "HangingHerbs": hanging_herbs,
    "HideFrame": hide_frame,
    "MarketStall": market_stall,
    "Millstones": millstones,
    "NetRack": net_rack,
    "NoticeBoard": notice_board,
    "TollBar": toll_bar,
    "CharnelRecess": charnel_recess,
    "Ladder": ladder,
    "Grindstone": grindstone,
    "Sawhorse": sawhorse,
    "CookingTripod": cooking_tripod,
    "LeakingCask": leaking_cask,
    "ChestTomb": chest_tomb,
    "LedgerStone": ledger_stone,
    "Stocks": stocks,
    "FishRack": fish_rack,
    "CartWheel": cart_wheel,
    "ButterChurn": butter_churn,
    "MarketStallFish": lambda: market_stall(goods="fish"),
    "MarketStallCloth": lambda: market_stall(goods="cloth"),
    "WeaponRack": weapon_rack,
    "Barricade": barricade,
    "ReedBundles": reed_bundles,
    "BellRopes": bell_ropes,
    "ButcherBlock": butcher_block,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
