r"""Ways, dead. The long way back across the realm of the dead's still water: the causeway's worn slabs
(one a tile), single stepping stones, and the soul gate, an arch of two leaning bone-pale stones under a
lintel, where the route begins.

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

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
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


ASSETS = {
    "CausewaySlab": causeway_slab,
    "SteppingStone": stepping_stone,
    "SoulGate": soul_gate,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
