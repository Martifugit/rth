r"""Ways, dead. The long way back across the realm of the dead's still water: the causeway's worn slabs
(one a tile), single stepping stones, and the soul gate, an arch of two leaning bone-pale stones under a
lintel, where the route begins. And the Landing's jetty, where the ferryman waits by his boat.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
dead.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Ways/Dead/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\ways\dead.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import random
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

from mathutils import Matrix, Vector  # noqa: E402

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
import artlib.nature as N  # noqa: E402
from artlib.caves import make  # noqa: E402

TOP = 0.06                      # a slab's walked top above the tile's ground (the water's level is the game's)
DEEP = 0.6                      # how far a slab reaches down into the water
STONES = ["Ashworn", "DeadSlate"]           # the worn top, the wet sides


def slab(bm, rnd, half=(0.48, 0.48), chip=0.06, top=TOP, sides_mat=1, top_mat=0, centre=(0, 0)):
    """A worn flat stone block: its top a little uneven and its corners knocked off, its sides
    widening down into the water."""
    cx, cy = centre
    ring = []
    for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
        k = rnd.uniform(0.3, 0.7)                                  # each corner chipped by a different amount
        a = (cx + sx * half[0], cy + sy * (half[1] - chip * k))
        b = (cx + sx * (half[0] - chip * k), cy + sy * half[1])
        ring += [a, b] if sx * sy > 0 else [b, a]
    tops = [bm.verts.new((x, y, top + rnd.uniform(-0.015, 0.015))) for x, y in ring]
    feet = [bm.verts.new((cx + (x - cx) * 1.08, cy + (y - cy) * 1.08, -DEEP)) for x, y in ring]
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((feet[i], feet[j], tops[j], tops[i])).material_index = sides_mat
    bm.faces.new(tops).material_index = top_mat
    bm.faces.new(list(reversed(feet))).material_index = sides_mat


def causeway_slab(seed=1, at=(0, 0, 0)):
    """One tile of the causeway: a worn flat stone, its top a hand above the water, laid as two blocks with
    a crack between them."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for y in (-0.245, 0.245):
        slab(bm, rnd, half=(0.48, 0.235), centre=(0, y), chip=0.05)
    return make("CausewaySlab", bm, STONES, at=at, smooth=None)


def stepping_stone(seed=2, at=(0, 0, 0)):
    """A single rounded stone standing out of the water, worn flat on top by those who walked it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    pts = []
    for z, k in ((-DEEP, 1.15), (TOP - 0.08, 1.0), (TOP, 0.82)):
        for i in range(8):
            a = 2 * math.pi * i / 8 + rnd.uniform(-0.15, 0.15)
            r = 0.36 * k * rnd.uniform(0.88, 1.06)
            pts.append((r * math.cos(a), r * 0.85 * math.sin(a), z + (rnd.uniform(-0.01, 0.01) if z > 0 else 0)))
    for f in A.hull(bm, pts):
        f.normal_update()
        f.material_index = 0 if f.normal.z > 0.8 else 1
    return make("SteppingStone", bm, STONES, at=at, smooth=30)


def gate_stone(bm, rnd, x, height, width, lean):
    """One of the gate's standing stones, its foot at x, leaning `lean` toward the middle; returns its top's
    middle and half width (the lintel rests on it)."""
    pts = []
    for z, k in ((-0.3, 1.0), (0.12, 1.05), (height * 0.35, 1.12), (height * 0.7, 0.92), (height, 0.72)):
        t = max(z, 0) / height
        cx = x + lean * t + (rnd.uniform(-0.05, 0.05) if 0 < z < height else 0)     # rough, never quite straight
        for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
            pts.append((cx + sx * width * k * rnd.uniform(0.82, 1.04), sy * width * k * rnd.uniform(0.82, 1.04),
                        z if z < height else height))
    for f in A.hull(bm, pts):
        f.material_index = 1 if f.calc_center_median().z < 0.06 else 0
    return x + lean, width * 0.72


def soul_gate(seed=3, at=(0, 0, 0)):
    """The start of the long way: two bone-pale stones leaning in toward each other, a rough lintel laid
    across their tops, about two tiles wide and walked through along Y. One pale soul-mark is cut into the
    lintel's face, the only sign that this is the way."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    h = 2.5
    ltop = []
    for s in (1, -1):
        ltop.append(gate_stone(bm, rnd, s * 1.15, h, 0.3, -s * 0.14))
    (xr, wr), (xl, wl) = ltop
    lo, hi = xl - wl * 0.8, xr + wr * 0.8                     # its ends lie on the stones, not past them
    pts = []
    for x in (lo, (lo + hi) / 2, hi):
        for y in (-0.21, 0.21):
            pts.append((x + rnd.uniform(-0.05, 0.05), y * rnd.uniform(0.9, 1.05), h - 0.04))
            pts.append((x + rnd.uniform(-0.05, 0.05), y * rnd.uniform(0.85, 1.0), h + 0.42 + rnd.uniform(-0.05, 0.05)))
    lintel = A.hull(bm, pts)
    for f in lintel:
        f.material_index = 0
    A.decal(bm, [(0.0, -0.5, h + 0.06), (0.08, -0.5, h + 0.21), (0.0, -0.5, h + 0.36), (-0.08, -0.5, h + 0.21)],
            (0, -1, 0), mat=2, lift=0.004)                       # the one mark, cast onto the lintel's front
    return make("SoulGate", bm, ["BonePale", "DeadSlate", "SoulGlow"], at=at, smooth=20)


# ---- the Landing's jetty ------------------------------------------------------------------------

JETTY = (2, 9)                  # tiles across (east) and along (north), from the origin's tile
DECK = 0.3                      # the deck's top above the water
PLANK = (0.2, 0.05, 0.225)      # a plank's width, thickness and its spacing along the jetty
STRINGER = (0.12, 0.15)         # the beams under the planks: width, depth
HEADER = 0.12                   # the cross beam on each pair of posts, under the stringers
BENTS = (-0.33, 2.6, 5.5, 8.33)  # where the pairs of posts stand along the jetty (the ends carry the stringers' ends)
POST_R = 0.085
JETTY_MATS = ["DrownedWood", "Blackiron", "CandleWax", "CandleFlame"]
J_WOOD, J_IRON, J_WAX, J_FLAME = range(4)


def jetty_candles(bm, top, specs):
    """Candles stuck on a post's top in a pool of their wax (the lights family's candle, its colours
    moved onto the jetty's: wax, flame, the wick in iron)."""
    from assets.lights import dead as lights
    before = set(bm.faces)
    lights.pool(bm, 0.075, 0.02, centre=(top.x, top.y), points=6, z=top.z)
    for dx, dy, h, lit in specs:
        lights.candle(bm, (top.x + dx, top.y + dy, top.z + 0.012), h, 0.026, (0.0, 0.0), lit)
    remap = {lights.WAX: J_WAX, lights.CFLAME: J_FLAME, lights.WICK: J_IRON, lights.BONE: J_WAX}
    for f in set(bm.faces) - before:
        f.material_index = remap[f.material_index]


def iron_band(bm, c, r, h=0.05):
    A.prism(bm, 8, r, r, c.z - h / 2, c.z + h / 2, center=(c.x, c.y), mat_index=J_IRON, cap_bottom=True)


def jetty(seed=4, at=(0, 0, 0)):
    """The Landing's jetty, where the ferryman waits by his boat: weathered planks laid one by one
    across three stringers, on cross beams carried by pairs of drowned posts, two tiles wide and nine
    long, running north from the shore into the water. Iron nails over the stringers, one plank gone
    and the next broken; an iron-banded mooring post with a ring on the boat's side (east), and
    candles burning on the posts at the shore and at the far end.

    Built in the game's frame (x east, y north) and turned half round at the end: the export turns
    Blender -Y into the game's north, so the jetty runs along -Y and its second tile lies at -X. The
    origin is the middle of its south-west tile, at the water's level."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    w, l = JETTY
    x0, x1, y0, y1 = -0.5, w - 0.5, -0.5, l - 0.5
    pw, pt, step = PLANK
    under = DECK - pt                                      # the planks' underside, on the stringers
    s_bot = under - STRINGER[1]                            # the stringers' underside, on the headers
    h_bot = s_bot - HEADER                                 # the headers' underside, on the posts
    for x in (x0 + 0.1, (x0 + x1) / 2, x1 - 0.1):
        A.box(bm, (x - STRINGER[0] / 2, y0 + 0.08, s_bot), (x + STRINGER[0] / 2, y1 - 0.08, under), J_WOOD)
    # the planks, one by one: ends ragged, each lying a hair differently; one gone and one broken
    n = int(round((y1 - y0) / step))
    for i in range(n):
        y = y0 + step * (i + 0.5)
        if i == n - 9:
            continue                                       # gone
        a = x0 - rnd.uniform(-0.01, 0.05)
        b = x1 + rnd.uniform(-0.01, 0.05)
        if i == n - 8:
            b = x1 - 0.62                                  # broken off short over the middle stringer
        z = rnd.uniform(-0.008, 0.006)
        A.box(bm, (a, y - pw / 2, under + z), (b, y + pw / 2, DECK + z), J_WOOD)
        for nx in (x0 + 0.1, x1 - 0.1):                    # two nail heads over each outer stringer, painted on
            if a < nx < b:
                for ny in (-0.05, 0.05):
                    q = [bm.verts.new((nx + dx, y + ny + dy, DECK + z + 0.002))
                         for dx, dy in ((-0.016, -0.016), (0.016, -0.016), (0.016, 0.016), (-0.016, 0.016))]
                    bm.faces.new(q).material_index = J_IRON
    # the bents: a cross beam on two posts at each, the posts sunk deep in the water
    tops = {}
    for k, y in enumerate(BENTS):
        A.box(bm, (x0 - 0.12, y - 0.07, h_bot), (x1 + 0.12, y + 0.07, s_bot), J_WOOD)
        for side, x in (("w", x0 - 0.02), ("e", x1 + 0.02)):
            rise = {(0, "w"): 0.42, (0, "e"): 0.3, (2, "e"): 0.85, (3, "w"): 0.62, (3, "e"): 0.5}.get((k, side))
            top = Vector((x + rnd.uniform(-0.02, 0.02), y + 0.11, DECK + rise if rise else h_bot + 0.04))
            line = [Vector((x, y + 0.11, -0.9)), Vector((x, y + 0.11, h_bot)), top]
            N.rod(bm, line, POST_R, mat=J_WOOD, sides=7, taper=0.9)
            if rise:
                tops[(k, side)] = top
    # iron on the posts that stand proud: a band under each top, and the mooring post's ring on the
    # boat's side
    for key, top in tops.items():
        iron_band(bm, top - Vector((0, 0, 0.12)), POST_R * 0.95)
    moor = tops[(2, "e")]
    staple = moor + Vector((POST_R * 0.85, 0, -0.3))
    A.box(bm, staple + Vector((-0.04, -0.03, -0.02)), staple + Vector((0.03, 0.03, 0.02)), J_IRON)
    ring = [staple + Vector((0.02, 0.07 * math.sin(2 * math.pi * k / 8), -0.07 + 0.07 * math.cos(2 * math.pi * k / 8)))
            for k in range(9)]
    N.rod(bm, ring, 0.012, mat=J_IRON, sides=4)
    # candles on the shore posts and the far end's west post
    jetty_candles(bm, tops[(0, "w")], [(0.0, 0.0, 0.2, True), (0.03, -0.03, 0.09, True)])
    jetty_candles(bm, tops[(0, "e")], [(0.0, 0.0, 0.12, True)])
    jetty_candles(bm, tops[(3, "w")], [(0.0, 0.01, 0.26, True), (-0.03, -0.03, 0.14, True), (0.03, -0.02, 0.06, False)])
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi, 3, "Z"))
    return make("Jetty", bm, JETTY_MATS, at=at, smooth=35)


ASSETS = {
    "CausewaySlab": causeway_slab,
    "SteppingStone": stepping_stone,
    "SoulGate": soul_gate,
    "Jetty": jetty,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
