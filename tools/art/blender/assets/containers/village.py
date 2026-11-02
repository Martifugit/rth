"""Containers, village. Barrels, crates, sacks and the trough of the Wennow basin's farms and yards;
the washtub, the basin and jug and the linen chest of its houses; the church's strongbox (the bank);
buckets, baskets, the water butt and a rack of casks;
the fishers' crates; a cauldron.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Containers/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\containers\\village.py")
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
import artlib.props as P  # noqa: E402


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


def washtub(seed=1, at=(0, 0, 0)):
    """A washtub on a low stand: a wide, shallow coopered tub with two stave handles standing up,
    two iron hoops, grey water in it and a scrubbing board leaning on its rim."""
    bm = bmesh.new()
    P.box(bm, -0.38, -0.12, 0.0, 0.38, 0.12, 0.18, 4)
    P.box(bm, -0.12, -0.38, 0.0, 0.12, 0.38, 0.18, 4)
    h, r0, r1 = 0.38, 0.38, 0.44
    rings = []
    for z, r in ((0.18, r0), (0.18 + h, r1)):
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * j / 12), r * math.sin(2 * math.pi * j / 12), z)) for j in range(12)])
    inner = [[bm.verts.new((v.co.x * (r - 0.035) / r, v.co.y * (r - 0.035) / r, v.co.z)) for v in ring]
             for ring, r in zip(rings, (r0, r1))]
    for j in range(12):
        n = (j + 1) % 12
        bm.faces.new((rings[0][j], rings[0][n], rings[1][n], rings[1][j])).material_index = 0
        bm.faces.new((inner[1][j], inner[1][n], inner[0][n], inner[0][j])).material_index = 0
        bm.faces.new((rings[1][j], rings[1][n], inner[1][n], inner[1][j])).material_index = 0
    bm.faces.new(list(reversed(rings[0]))).material_index = 0
    for z in (0.26, 0.48):
        r = r0 + (r1 - r0) * (z - 0.18) / h + 0.012
        A.prism(bm, 12, r, r, z - 0.02, z + 0.02, mat_index=1, cap_top=False)
    for s in (-1, 1):
        P.box(bm, s * (r1 - 0.02) - 0.03, -0.06, 0.18 + h - 0.05, s * (r1 - 0.02) + 0.03, 0.06, 0.18 + h + 0.1, 0)
    rr = r1 - 0.06
    A.prism(bm, 12, rr, rr, 0.18 + h - 0.1, 0.18 + h - 0.095, mat_index=2)                      # the water
    P.bar(bm, (-0.1, 0.32, 0.25), (-0.05, 0.12, 0.8), 0.3, 0.025, 3)                              # the scrubbing board
    return P.make("Washtub", bm, ["Planks", "Iron", "Mere", "Boards", "Timber"], "Containers", at, smooth=30)


def basin_and_jug(seed=1, at=(0, 0, 0)):
    """A washing basin with its jug standing in it, as left on a stand or a table: a broad glazed
    earthenware bowl with water in it, the jug tall and bellied with a lip and a handle."""
    bm = bmesh.new()
    A.loft(bm, [((0, 0, 0.0), 0.12, 0.12), ((0, 0, 0.02), 0.13, 0.13), ((0, 0, 0.1), 0.25, 0.25),
                ((0, 0, 0.11), 0.27, 0.27), ((0, 0, 0.095), 0.23, 0.23)], sides=12, ref=(1, 0, 0), mats=0)
    A.prism(bm, 12, 0.22, 0.22, 0.07, 0.072, mat_index=2)
    x = 0.04
    A.loft(bm, [((x, 0, 0.03), 0.08, 0.08), ((x, 0, 0.14), 0.12, 0.12), ((x, 0, 0.27), 0.075, 0.075),
                ((x, 0, 0.36), 0.06, 0.06), ((x, 0, 0.4), 0.075, 0.075)], sides=10, ref=(1, 0, 0), mats=1)
    A.hull(bm, [(x - 0.06, -0.03, 0.39), (x - 0.06, 0.03, 0.39), (x - 0.13, 0.0, 0.415), (x - 0.06, 0.0, 0.405)], 1)   # the lip
    N.rod(bm, [(x + 0.07, 0, 0.36), (x + 0.15, 0, 0.32), (x + 0.15, 0, 0.2), (x + 0.11, 0, 0.15)], 0.015, mat=1, sides=4)
    return P.make("BasinAndJug", bm, ["Ochre", "RedRock", "Mere"], "Containers", at, smooth=40)


def linen_chest(seed=1, at=(0, 0, 0)):
    """A linen chest: a long low box of boards on a plinth, a flat lid with a moulded edge, iron
    hinges at the back and a lock plate at the front, a folded sheet left lying on the lid."""
    bm = bmesh.new()
    w, d, h = 0.6, 0.26, 0.48
    P.box(bm, -w - 0.03, -d - 0.03, 0, w + 0.03, d + 0.03, 0.06, 1)
    P.box(bm, -w, -d, 0.06, w, d, h, 0)
    for x in (-w * 0.33, w * 0.33):                                                                   # panel stiles on the front
        P.box(bm, x - 0.03, -d - 0.012, 0.08, x + 0.03, -d, h - 0.03, 1)
    P.box(bm, -w - 0.035, -d - 0.035, h, w + 0.035, d + 0.035, h + 0.05, 1)
    for x in (-w * 0.6, w * 0.6):
        P.box(bm, x - 0.04, d + 0.03, h - 0.12, x + 0.04, d + 0.045, h + 0.05, 2)
    P.box(bm, -0.05, -d - 0.025, h - 0.12, 0.05, -d - 0.01, h - 0.02, 2)
    P.box(bm, -0.3, -0.18, h + 0.05, 0.18, 0.16, h + 0.09, 3)
    P.box(bm, -0.29, -0.17, h + 0.09, 0.17, 0.15, h + 0.12, 3)
    return P.make("LinenChest", bm, ["Planks", "Oak", "Iron", "Linen"], "Containers", at)


def strongbox(seed=1, at=(0, 0, 0)):
    """The church's strongbox: the bank. A heavy oak chest with a domed lid, bound all over in black
    iron (bands round the body and over the dome, corner plates, studs) and a great lock plate in
    bronze, the one bright thing; it stands on two timber runners off the damp floor."""
    bm = bmesh.new()
    OAK, IRON, BRONZE, RUNNER = range(4)
    w, d, h = 0.5, 0.32, 0.5
    for y in (-d + 0.06, d - 0.06):
        P.box(bm, -w - 0.02, y - 0.05, 0, w + 0.02, y + 0.05, 0.07, RUNNER)
    P.box(bm, -w, -d, 0.07, w, d, h, OAK)
    n = 6                                                                                             # the dome, a half-round along X
    arc = [(d * math.cos(math.pi * k / n), h + 0.2 * math.sin(math.pi * k / n)) for k in range(n + 1)]
    for (ya, za), (yb, zb) in zip(arc, arc[1:]):
        A.hull(bm, [(x, y, z) for x in (-w, w) for y, z in ((ya, za), (yb, zb), (ya * 0.5, h), (yb * 0.5, h))], OAK)
    for x in (-w + 0.04, -w * 0.33, w * 0.33, w - 0.04):                                              # bands over the dome and down
        for (ya, za), (yb, zb) in zip(arc, arc[1:]):
            A.hull(bm, [(x + dx, y * 1.04, (z - h) * 1.06 + h) for dx in (-0.03, 0.03) for y, z in ((ya, za), (yb, zb))] +
                   [(x + dx, y * 1.0, (z - h) + h) for dx in (-0.03, 0.03) for y, z in ((ya, za), (yb, zb))], IRON)
        for y in (-d - 0.012, d):
            P.box(bm, x - 0.03, y, 0.07, x + 0.03, y + 0.012, h, IRON)
    for z in (0.12, h - 0.03):                                                                        # bands round the body
        P.box(bm, -w - 0.012, -d - 0.012, z - 0.025, w + 0.012, d + 0.012, z + 0.025, IRON)
    P.box(bm, -0.11, -d - 0.025, h - 0.22, 0.11, -d - 0.012, h + 0.04, BRONZE)                         # the lock plate
    P.box(bm, -0.018, -d - 0.03, h - 0.15, 0.018, -d - 0.024, h - 0.08, IRON)                          # its keyhole, painted dark
    return P.make("Strongbox", bm, ["Oak", "Blackiron", "Bronze", "Timber"], "Containers", at)


def bucket(seed=1, at=(0, 0, 0)):
    """A wooden bucket: staves wider at the top, two iron hoops, an iron bail handle standing up,
    water in it."""
    bm = bmesh.new()
    A.loft(bm, [((0, 0, 0), 0.14, 0.14), ((0, 0, 0.32), 0.17, 0.17)], sides=10, ref=(1, 0, 0), mats=0)
    for z in (0.06, 0.26):
        r = 0.14 + 0.03 * z / 0.32 + 0.008
        A.prism(bm, 10, r, r, z - 0.015, z + 0.015, mat_index=1, cap_top=False)
    A.prism(bm, 10, 0.155, 0.155, 0.27, 0.272, mat_index=2)
    N.rod(bm, [(-0.17, 0, 0.28), (-0.13, 0, 0.5), (0.0, 0, 0.56), (0.13, 0, 0.5), (0.17, 0, 0.28)], 0.008, mat=1, sides=3)
    return P.make("Bucket", bm, ["Planks", "Iron", "Mere"], "Containers", at)


def baskets(seed=2, at=(0, 0, 0)):
    """Baskets of withies by a door or a stall: a tall one with a lid, a round one heaped with
    apples, a shallow one of turnips, each bound with a darker band at the rim."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    WICKER, BAND, APPLE, ROOT = range(4)
    A.loft(bm, [((-0.3, 0.1, 0), 0.16, 0.16), ((-0.3, 0.1, 0.55), 0.19, 0.19)], sides=9, ref=(1, 0, 0), mats=WICKER)
    A.loft(bm, [((-0.3, 0.1, 0.55), 0.2, 0.2), ((-0.3, 0.1, 0.6), 0.2, 0.2), ((-0.3, 0.1, 0.64), 0.06, 0.06)], sides=9, ref=(1, 0, 0), mats=BAND)
    for (cx, cy), r, h, fill in (((0.2, 0.15), 0.22, 0.3, APPLE), ((0.05, -0.35), 0.25, 0.14, ROOT)):
        A.loft(bm, [((cx, cy, 0), r * 0.8, r * 0.8), ((cx, cy, h), r, r)], sides=9, ref=(1, 0, 0), mats=WICKER)
        A.prism(bm, 9, r + 0.01, r + 0.01, h - 0.03, h, center=(cx, cy), mat_index=BAND, cap_top=False)
        for _ in range(9):
            a, d = rnd.uniform(0, 6.28), rnd.uniform(0, r * 0.65)
            N.blob(bm, (cx + d * math.cos(a), cy + d * math.sin(a), h + 0.02), (0.05, 0.05, 0.045), 0, rnd, mat=fill)
    return P.make("Baskets", bm, ["Wheat", "Umber", "Russet", "Ochre"], "Containers", at, smooth=40)


def water_butt(seed=1, at=(0, 0, 0)):
    """A water butt under the eaves: a big cask on end on a stand of two beams, a lid half off it,
    a tap low down with a dipper hung on a nail above it."""
    bm = bmesh.new()
    for y in (-0.22, 0.22):
        P.box(bm, -0.42, y - 0.06, 0.0, 0.42, y + 0.06, 0.2, 3)
    h, r0, r1 = 1.0, 0.36, 0.4
    rings = [[bm.verts.new(((r0 + (r1 - r0) * math.sin(math.pi * t) ** 0.7) * math.cos(2 * math.pi * j / 12),
                            (r0 + (r1 - r0) * math.sin(math.pi * t) ** 0.7) * math.sin(2 * math.pi * j / 12), 0.2 + h * t)) for j in range(12)]
             for t in (0.0, 0.2, 0.5, 0.8, 1.0)]
    for a, b in zip(rings, rings[1:]):
        for j in range(12):
            bm.faces.new((a[j], a[(j + 1) % 12], b[(j + 1) % 12], b[j])).material_index = 0
    bm.faces.new(list(reversed(rings[0]))).material_index = 0
    bm.faces.new(rings[-1]).material_index = 2
    for t in (0.1, 0.35, 0.65, 0.9):
        r = r0 + (r1 - r0) * math.sin(math.pi * t) ** 0.7 + 0.012
        A.prism(bm, 12, r, r, 0.2 + h * t - 0.025, 0.2 + h * t + 0.025, mat_index=1, cap_top=False)
    A.hull(bm, [(x, y, z) for x in (-0.36, 0.1) for y in (-0.3, 0.3) for z in (1.2, 1.24)] + [(0.2, 0.0, 1.22)], 3)   # the lid, half off
    P.rod(bm, (0.0, -r0 - 0.0, 0.42), (0.0, -r0 - 0.12, 0.42), 0.025, 1)                           # the tap
    P.rod(bm, (0.0, -r0 - 0.12, 0.42), (0.0, -r0 - 0.12, 0.36), 0.015, 1)
    return P.make("WaterButt", bm, ["Oak", "Iron", "Mere", "Planks"], "Containers", at, smooth=35)


def barrel_rack(seed=3, at=(0, 0, 0)):
    """Casks lying on a rack in a cellar or behind an inn, 2 x 1 along X: two long timber cradles
    with chocks, three casks on their sides on them, a spigot in each head, a drip tub under one."""
    bm = bmesh.new()
    OAK, IRON, TIMBER, TAP = range(4)
    for y in (-0.22, 0.22):
        P.box(bm, -0.95, y - 0.05, 0.0, 0.95, y + 0.05, 0.14, TIMBER)
    L, r0, r1 = 0.36, 0.25, 0.29
    for x in (-0.62, 0.0, 0.62):
        rings = []
        for t in (-1.0, -0.5, 0.0, 0.5, 1.0):
            r = r0 + (r1 - r0) * math.cos(math.pi * t / 2) ** 0.7
            rings.append([bm.verts.new((x + r * math.cos(2 * math.pi * j / 10), L * t, 0.14 + r1 + r * math.sin(2 * math.pi * j / 10)))
                          for j in range(10)])
        for a, b in zip(rings, rings[1:]):
            for j in range(10):
                bm.faces.new((a[j], a[(j + 1) % 10], b[(j + 1) % 10], b[j])).material_index = OAK
        bm.faces.new(rings[0]).material_index = TIMBER
        bm.faces.new(list(reversed(rings[-1]))).material_index = TIMBER
        for t in (-0.75, 0.75):
            r = r0 + (r1 - r0) * math.cos(math.pi * t / 2) ** 0.7 + 0.012
            A.loft(bm, [((x, L * t - 0.02, 0.14 + r1), r, r), ((x, L * t + 0.02, 0.14 + r1), r, r)], sides=10, ref=(0, 0, 1), mats=IRON)
        P.rod(bm, (x, -L, 0.14 + r1 - 0.12), (x, -L - 0.1, 0.14 + r1 - 0.12), 0.02, TAP)
    A.loft(bm, [((0.0, -0.62, 0.0), 0.15, 0.15), ((0.0, -0.62, 0.14), 0.17, 0.17)], sides=8, ref=(1, 0, 0), mats=TIMBER)
    return P.make("BarrelRack", bm, ["Oak", "Iron", "Timber", "Bronze"], "Containers", at, smooth=35)


def fish_crates(seed=4, at=(0, 0, 0)):
    """The fishers' crates on the landing: low slatted crates stacked two high and one on the ground
    beside them full of fish, a coil of rope on top."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    SLAT, FRAME, FISH, ROPE = range(4)

    def crate(x, y, z, full):
        P.box(bm, x - 0.3, y - 0.22, z, x + 0.3, y + 0.22, z + 0.03, SLAT)
        for k in range(3):
            zz = z + 0.04 + 0.06 * k
            for yy in (y - 0.22, y + 0.2):
                P.box(bm, x - 0.3, yy, zz, x + 0.3, yy + 0.02, zz + 0.04, SLAT)
            for xx in (x - 0.3, x + 0.28):
                P.box(bm, xx, y - 0.22, zz, xx + 0.02, y + 0.22, zz + 0.04, SLAT)
        for xx in (x - 0.3, x + 0.27):
            for yy in (y - 0.22, y + 0.19):
                P.box(bm, xx, yy, z, xx + 0.03, yy + 0.03, z + 0.22, FRAME)
        if full:
            for k in range(5):
                P.fish(bm, (x + rnd.uniform(-0.15, 0.15), y + rnd.uniform(-0.1, 0.1), z + 0.17 + 0.02 * (k % 2)),
                       rnd.uniform(-0.5, 0.5) + (math.pi if k % 2 else 0), FISH, 0.9)
    crate(-0.2, 0.0, 0.0, False)
    crate(-0.2, 0.0, 0.23, False)
    crate(0.5, -0.4, 0.0, True)
    for k in range(3):                                                                        # the rope's coil
        A.loft(bm, [((-0.2, 0.0, 0.48 + 0.025 * k), 0.15 - 0.02 * k, 0.15 - 0.02 * k), ((-0.2, 0.0, 0.5 + 0.025 * k), 0.15 - 0.02 * k, 0.15 - 0.02 * k)],
               sides=10, ref=(1, 0, 0), mats=ROPE)
    return P.make("FishCrates", bm, ["Driftwood", "DeadWood", "Ashgrey", "Wheat"], "Containers", at)


def cauldron(seed=5, at=(0, 0, 0)):
    """A great iron cauldron on three short legs over a ring of hearth stones, a ladle in it, the
    pottage dark inside; the fire under it is the game's to draw."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    IRON, STEW, STONE_, WOOD = range(4)
    for k in range(3):
        a = 2 * math.pi * k / 3
        P.rod(bm, (0.3 * math.cos(a), 0.3 * math.sin(a), 0.0), (0.24 * math.cos(a), 0.24 * math.sin(a), 0.3), 0.03, IRON, sides=4)
    A.loft(bm, [((0, 0, 0.2), 0.2, 0.2), ((0, 0, 0.32), 0.36, 0.36), ((0, 0, 0.5), 0.4, 0.4), ((0, 0, 0.66), 0.34, 0.34),
                ((0, 0, 0.7), 0.37, 0.37)], sides=12, ref=(1, 0, 0), mats=IRON)
    A.prism(bm, 12, 0.32, 0.32, 0.6, 0.61, mat_index=STEW)
    for s in (-1, 1):                                                                        # the lugs
        P.box(bm, s * 0.36, -0.04, 0.6, s * 0.43, 0.04, 0.68, IRON)
    P.rod(bm, (0.1, 0.05, 0.6), (0.3, 0.25, 0.95), 0.015, WOOD, sides=4)
    for k in range(8):
        a = 2 * math.pi * k / 8 + rnd.uniform(-0.1, 0.1)
        c = Vector((0.55 * math.cos(a), 0.55 * math.sin(a), 0))
        A.hull(bm, [tuple(c + Vector((rnd.uniform(-0.1, 0.1), rnd.uniform(-0.09, 0.09), rnd.uniform(-0.02, 0.11)))) for _ in range(7)], STONE_)
    return P.make("Cauldron", bm, ["Blackiron", "Umber", "Scree", "Timber"], "Containers", at, smooth=35)


ASSETS = {
    "Trough": trough,
    "Barrel": barrel,
    "Crate": crate,
    "Sacks": sacks,
    "Washtub": washtub,
    "BasinAndJug": basin_and_jug,
    "LinenChest": linen_chest,
    "Strongbox": strongbox,
    "Bucket": bucket,
    "Baskets": baskets,
    "WaterButt": water_butt,
    "BarrelRack": barrel_rack,
    "FishCrates": fish_crates,
    "Cauldron": cauldron,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
