r"""Lights, dead. The realm of the dead's lights along the long way back: soul lanterns, a small iron
cage holding a pale soul flame on top of a drowned post leaning out of the water, and the same lantern
spent (the flame gone, a dim cinder in the cage). Soul lanterns restore blood; a spent one has been used.

And the candles that light the underworld in their thousands, each a cold blue-green soul flame on
ivory wax: small and large clusters run together in their own wax, a bone-pale stone lined with them,
a great melted heap (a landmark), a few on wax rafts drifting on the water, and a ledge lined with
them for the Landing's edges. Every candle is one recipe (`candle`): a six-sided stick of wax, its
wick painted as a dark spot on its top, a small teardrop flame; heights, leans and which have gone
out are set in each builder's table, not rolled.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
dead.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Lights/Dead/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\lights\dead.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import random
import sys

from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
import artlib.nature as N  # noqa: E402
from artlib.caves import make  # noqa: E402

MATS = ["DrownedWood", "Blackiron", "SoulGlow", "DeadSlate", "Cinder"]
WOOD, IRON, FLAME, STONE, CINDER = range(5)

POST_H = 1.55                   # the post's top, where the cage stands
LEAN = (0.12, 0.05)             # how far the top leans off upright (x, y)
CAGE_R = 0.11                   # the cage's half width
CAGE_H = 0.3


def soul_lantern(seed=1, at=(0, 0, 0), spent=False):
    """A post of drowned timber sunk in a ring of dark stones, leaning a little, an iron cage on its top:
    a floor plate, four bars, a peaked cap. Lit, a pale soul flame stands on the floor plate; spent,
    only a dim cinder."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    top = Vector((LEAN[0], LEAN[1], POST_H))
    line = N.wobble(rnd, (0, 0, -0.15), top + Vector((0, 0, 0.02)), 5, 0.035)        # crooked, warped by the water
    N.rod(bm, line, 0.095, mat=WOOD, sides=7, taper=0.65)
    for k in range(5):                                      # stones packed round the foot
        a = 2 * math.pi * k / 5 + rnd.uniform(-0.2, 0.2)
        c = Vector((0.2 * math.cos(a), 0.2 * math.sin(a), 0))
        s = rnd.uniform(0.1, 0.14)
        pts = [c + Vector((rnd.uniform(-s, s), rnd.uniform(-s, s), -0.04)) for _ in range(4)]
        pts += [c + Vector((rnd.uniform(-s, s) * 0.7, rnd.uniform(-s, s) * 0.7, s * rnd.uniform(0.6, 0.9))) for _ in range(3)]
        A.hull(bm, pts, mat=STONE)
    # the cage, level on the post's top
    cx, cy, z0 = top.x, top.y, top.z
    A.prism(bm, 4, CAGE_R + 0.02, CAGE_R + 0.02, z0, z0 + 0.03, center=(cx, cy), rot=math.pi / 4,
            mat_index=IRON, cap_bottom=True)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        x, y = cx + CAGE_R * math.cos(a) * 1.3, cy + CAGE_R * math.sin(a) * 1.3
        N.rod(bm, [(x, y, z0 + 0.01), (x, y, z0 + CAGE_H + 0.01)], 0.012, mat=IRON, sides=4)
    A.prism(bm, 4, CAGE_R + 0.04, 0.02, z0 + CAGE_H, z0 + CAGE_H + 0.1, center=(cx, cy), rot=math.pi / 4,
            mat_index=IRON, cap_bottom=True)
    A.prism(bm, 4, 0.025, 0.025, z0 + CAGE_H + 0.09, z0 + CAGE_H + 0.14, center=(cx, cy), mat_index=IRON)  # the finial
    if spent:
        A.hull(bm, [(cx + rnd.uniform(-0.05, 0.05), cy + rnd.uniform(-0.05, 0.05), z0 + 0.02) for _ in range(5)]
               + [(cx, cy, z0 + 0.06)], mat=CINDER)
    else:                                                   # the flame: a pale teardrop on the floor plate
        fl = []
        for k in range(6):
            a = 2 * math.pi * k / 6
            fl.append((cx + 0.06 * math.cos(a), cy + 0.06 * math.sin(a), z0 + 0.1))
            fl.append((cx + 0.035 * math.cos(a + 0.5), cy + 0.035 * math.sin(a + 0.5), z0 + 0.025))
        fl.append((cx + 0.01, cy, z0 + CAGE_H - 0.04))
        A.hull(bm, fl, mat=FLAME)
    return make("SoulLanternSpent" if spent else "SoulLantern", bm, MATS, at=at, smooth=35)


# ---- candles -------------------------------------------------------------------------------------

CANDLE_MATS = ["CandleWax", "CandleFlame", "DeadSlate", "BonePale"]
WAX, CFLAME, WICK, BONE = range(4)


def candle(bm, base, h, r=0.03, lean=(0.0, 0.0), lit=True, sink=0.03):
    """One candle: a six-sided stick of wax from `base` (sunk `sink` into what it stands in), `h` tall
    above it, leaning by (x, y) radians; the wick a dark spot painted on its top, and if lit a small
    teardrop flame standing upright over it. Returns the top's centre."""
    base = Vector(base)
    before = set(bm.verts)
    rot = (math.pi / 3) * ((base.x * 7.3 + base.y * 3.1) % 1.0)          # each turned its own way
    _, faces = A.prism(bm, 6, r, r * 0.92, base.z - sink, base.z + h, center=(base.x, base.y), rot=rot,
                       mat_index=WAX)
    cap = faces[-1]
    rim = bmesh.ops.inset_individual(bm, faces=[cap], thickness=r * 0.62, depth=-0.004)
    cap.material_index = WICK                                            # the inset middle: the burnt wick
    for f in rim["faces"]:
        f.material_index = WAX
    m = Matrix.Rotation(lean[0], 3, "Y") @ Matrix.Rotation(-lean[1], 3, "X")
    bmesh.ops.rotate(bm, verts=[v for v in bm.verts if v not in before], cent=base, matrix=m)
    top = base + m @ Vector((0, 0, h))
    if lit:
        fh, fr = 0.07 + 0.6 * r, 0.55 * r                                # a cold teardrop over the wick
        pts = [top + Vector((fr * math.cos(2 * math.pi * k / 5), fr * math.sin(2 * math.pi * k / 5), fh * 0.3))
               for k in range(5)]
        pts += [top + Vector((0, 0, 0.004)), top + Vector((0, 0, fh))]
        A.hull(bm, pts, mat=CFLAME)
    return top


def pool(bm, r, h, centre=(0.0, 0.0), points=9, mat=WAX, lumps=0.12, z=0.0):
    """Wax run out and set round the candles' feet: a low rounded disc, its edge lobed (not random), its
    rim just under what it lies on."""
    cx, cy = centre
    pts = []
    for k in range(points):
        a = 2 * math.pi * k / points
        rr = r * (1 + lumps * math.cos(3 * a + 0.7) + 0.5 * lumps * math.sin(5 * a))
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a), z - 0.01))
        pts.append((cx + 0.7 * rr * math.cos(a + 0.3), cy + 0.7 * rr * math.sin(a + 0.3), z + h * 0.75))
    pts.append((cx, cy, z + h))
    return A.hull(bm, pts, mat=mat)


def mound_z(d, R, H):
    """The height of a melted mound of wax (`R` across, `H` high) at a distance `d` from its middle."""
    return H * max(0.0, 1 - (d / R) ** 1.6)


def mound(bm, R, H, mat=WAX, rings=(0.0, 0.35, 0.62, 0.84, 1.0), points=11):
    """A great run of melted wax: rings of points on a domed profile, lobed so the edge flows."""
    pts = []
    for i, f in enumerate(rings):
        for k in range(points if f > 0 else 1):
            a = 2 * math.pi * k / points + i * 0.4
            d = R * f * (1 + 0.1 * math.cos(3 * a + i))
            pts.append((d * math.cos(a), d * math.sin(a), mound_z(R * f, R, H) - (0.01 if f == 1.0 else 0.0)))
    return A.hull(bm, pts, mat=mat)


def drip(bm, top, down, length, width, out, mat=WAX):
    """A run of wax down a surface: a thin tapering strip from `top`, `length` along `down`, standing
    proud of the surface along `out` and sunk into it behind."""
    top, down, out = Vector(top), Vector(down).normalized(), Vector(out).normalized()
    across = down.cross(out).normalized()
    pts = []
    for t, w in ((0.0, width), (0.6, width * 0.75), (1.0, width * 0.45)):
        c = top + down * (length * t)
        for s in (-1, 1):
            pts.append(c + across * (s * w) + out * 0.008)
            pts.append(c + across * (s * w) - out * 0.012)
    return A.hull(bm, pts, mat=mat)


# (x, y, height, radius, lean x, lean y, lit): set by hand so each group reads as tended, then burned down
SMALL = [(0.0, 0.0, 0.3, 0.032, 0.0, 0.0, True), (0.08, 0.05, 0.19, 0.03, 0.05, 0.03, True),
         (-0.06, 0.07, 0.24, 0.028, -0.04, 0.05, True), (-0.04, -0.08, 0.12, 0.034, 0.0, 0.0, True),
         (0.07, -0.06, 0.06, 0.03, 0.0, 0.0, False)]
LARGE = [(0.0, 0.0, 0.46, 0.036, 0.0, 0.0, True), (0.12, 0.04, 0.34, 0.034, 0.06, 0.0, True),
         (-0.1, 0.09, 0.38, 0.032, -0.05, 0.06, True), (0.03, 0.15, 0.28, 0.03, 0.0, 0.09, True),
         (-0.15, -0.06, 0.26, 0.034, -0.12, -0.03, True), (0.08, -0.13, 0.22, 0.032, 0.04, -0.08, True),
         (-0.04, -0.17, 0.16, 0.03, 0.0, -0.14, True), (0.21, -0.03, 0.15, 0.03, 0.2, 0.0, True),
         (-0.22, 0.13, 0.12, 0.028, -0.18, 0.1, True), (0.16, 0.17, 0.1, 0.03, 0.1, 0.12, False),
         (-0.24, -0.15, 0.07, 0.03, 0.0, 0.0, False), (0.22, -0.2, 0.08, 0.026, 0.0, 0.0, True),
         (-0.02, 0.26, 0.09, 0.028, 0.0, 0.15, True)]


def cluster(name, table, pool_r, pool_h):
    """Candles standing in the wax they have run out into: a pool, or a mound when it is tall."""
    bm = bmesh.new()
    if pool_h < 0.04:
        pool(bm, pool_r, pool_h)
    else:
        mound(bm, pool_r, pool_h)
    for x, y, h, r, lx, ly, lit in table:
        z = mound_z(math.hypot(x, y), pool_r, pool_h) if pool_h >= 0.04 else pool_h * 0.7
        candle(bm, (x, y, z), h, r, (lx, ly), lit)
    return make(name, bm, CANDLE_MATS, smooth=30)


def candle_cluster_small():
    """Four candles and a burnt-out stub, run together in a small pool of their own wax."""
    return cluster("CandleClusterSmall", SMALL, 0.17, 0.025)


def candle_cluster_large():
    """A dozen candles of every height, the outer ones leaning out, on a low mound of melted wax."""
    return cluster("CandleClusterLarge", LARGE, 0.34, 0.08)


STONE_SIZE = (0.42, 0.2, 0.56)  # the candle stone's half length, half width and height


def candle_stone():
    """A bone-pale stone, long and a little tapered, its top lined with candles burned down to
    different heights, a skin of wax over the top and runs of it down both long faces."""
    bm = bmesh.new()
    lx, ly, h = STONE_SIZE
    pts = []
    for z, k in ((-0.05, 1.0), (h * 0.55, 0.97), (h, 0.9)):        # chamfered corners, a little taper
        for sx in (-1, 1):
            for sy in (-1, 1):
                pts.append((sx * lx * k, sy * (ly - 0.04) * k, z))
                pts.append((sx * (lx - 0.05) * k, sy * ly * k, z))
    top = h + 0.03
    pts += [(sx * (lx - 0.04) * 0.88, sy * (ly - 0.04) * 0.88, top) for sx in (-1, 1) for sy in (-1, 1)]
    A.hull(bm, pts, mat=BONE)
    skin = [(sx * (lx - 0.07), sy * (ly - 0.07), top - 0.01) for sx in (-1, 1) for sy in (-1, 1)]
    skin += [(sx * (lx - 0.12), sy * (ly - 0.1), top + 0.018) for sx in (-1, 1) for sy in (-1, 1)]
    A.hull(bm, skin, mat=WAX)
    for x, hh, lit in ((-0.3, 0.14, True), (-0.18, 0.26, True), (-0.06, 0.09, False), (0.05, 0.31, True),
                       (0.17, 0.2, True), (0.29, 0.12, True)):
        y = 0.03 * math.sin(x * 20)                                  # a line, not a ruled row
        candle(bm, (x, y, top + 0.01), hh, 0.03, (0.0, 0.0), lit)

    def side(z):                                                     # the long face's offset at a height
        return ly * (0.97 + (0.9 - 0.97) * (z - h * 0.55) / (h * 0.45))
    for sy in (-1, 1):
        for x, ln in ((-0.25, 0.22), (-0.04, 0.32), (0.2, 0.17), (0.32, 0.26)):
            xx = x * (1 if sy > 0 else -0.9)
            drip(bm, (xx, sy * side(h - 0.02), h - 0.01), (0, sy * 0.06, -1), ln, 0.022, (0, sy, 0))
    return make("CandleStone", bm, CANDLE_MATS, smooth=30)


HEAP = (0.58, 0.55)             # the heap's mound: radius and height


def candle_heap():
    """A great heap of melted wax about a tile across, built up over years by candles set on the stubs
    of the last: dozens standing all over it, tall ones at the top, every fifth gone out to a stub."""
    bm = bmesh.new()
    R, H = HEAP
    mound(bm, R, H, rings=(0.0, 0.2, 0.4, 0.58, 0.74, 0.88, 1.0), points=13)
    # rings of candles round the heap, fewer and taller toward its top; heights vary by a fixed pattern
    spots = [(0.0, 0.0, 0.5)]
    for ring, (f, n, hh) in enumerate(((0.16, 4, 0.34), (0.34, 7, 0.26), (0.52, 9, 0.2), (0.7, 11, 0.15),
                                        (0.86, 13, 0.1))):
        for k in range(n):
            a = 2 * math.pi * k / n + ring * 0.5
            spots.append((f, a, hh * (0.6 + 0.4 * ((k * 5 + ring * 3) % 7) / 6)))
    for i, (f, a, hh) in enumerate(spots):
        d = R * f
        x, y = d * math.cos(a), d * math.sin(a)
        lean = (0.35 * f * math.cos(a), 0.35 * f * math.sin(a))      # outer ones lean out with the slope
        lit = (i % 5) != 3
        candle(bm, (x, y, mound_z(d, R, H)), hh if lit else hh * 0.4, 0.034 if f < 0.5 else 0.03, lean, lit,
               sink=0.05)
    for k in range(8):                                               # older stubs melted into the slope
        a = 2 * math.pi * k / 8 + 0.2
        d = R * (0.45 + 0.1 * (k % 3))
        pool(bm, 0.07, 0.03, centre=(d * math.cos(a), d * math.sin(a)), points=6, z=mound_z(d, R, H) - 0.01)
    return make("CandleHeap", bm, CANDLE_MATS, smooth=30)


# rafts: (centre, radius, candles (x, y, height) on it)
RAFTS = [((0.0, 0.0), 0.11, [(0.0, 0.0, 0.2), (0.05, 0.04, 0.12)]),
         ((0.34, 0.2), 0.08, [(0.0, 0.0, 0.14)]),
         ((-0.28, 0.26), 0.09, [(0.01, -0.01, 0.17), (-0.04, 0.03, 0.08)])]


def floating_candles():
    """Candles set adrift on small rafts of their own wax, three drifting together on the water (its
    surface at the origin's height)."""
    bm = bmesh.new()
    for (cx, cy), r, cs in RAFTS:
        pool(bm, r, 0.035, centre=(cx, cy), points=7, z=-0.01)
        for x, y, h in cs:
            candle(bm, (cx + x, cy + y, 0.02), h, 0.026)
    return make("FloatingCandles", bm, CANDLE_MATS, smooth=30)


LEDGE = (0.5, 0.17, 0.42)       # the ledge's half length, half depth and height


def candle_shelf():
    """A long ledge of dark stone, its bone-pale top lined with candles, wax run over its front edge:
    set along the Landing's edges."""
    bm = bmesh.new()
    lx, ly, h = LEDGE
    pts = [(sx * lx * 0.96, sy * ly * 0.9, z) for sx in (-1, 1) for sy in (-1, 1) for z in (-0.05, h - 0.04)]
    A.hull(bm, pts, mat=WICK)                                                       # the dark body
    A.box(bm, (-lx - 0.03, -ly - 0.03, h - 0.06), (lx + 0.03, ly + 0.02, h), mat_index=BONE)   # the pale top
    for i, (hh, lit) in enumerate(((0.12, True), (0.24, True), (0.17, True), (0.07, False), (0.29, True),
                                   (0.2, True), (0.13, True), (0.22, True))):
        x = -lx + 0.08 + i * (2 * lx - 0.16) / 7
        y = -0.03 + 0.04 * ((i * 3) % 4) / 3
        pool(bm, 0.05, 0.015, centre=(x, y), points=6, z=h)
        candle(bm, (x, y, h + 0.01), hh, 0.028, (0.0, 0.0), lit)
    for x, ln in ((-0.38, 0.12), (-0.12, 0.2), (0.14, 0.09), (0.33, 0.16)):
        drip(bm, (x, -ly - 0.03, h - 0.005), (0, 0, -1), ln, 0.02, (0, -1, 0))
    return make("CandleShelf", bm, CANDLE_MATS, smooth=30)


ASSETS = {
    "SoulLantern": soul_lantern,
    "SoulLanternSpent": lambda: soul_lantern(spent=True),
    "CandleClusterSmall": candle_cluster_small,
    "CandleClusterLarge": candle_cluster_large,
    "CandleStone": candle_stone,
    "CandleHeap": candle_heap,
    "FloatingCandles": floating_candles,
    "CandleShelf": candle_shelf,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
