"""Widdermill's wheel and wheel-house: what the mill building (made from the village kit as data)
needs on its river side. 4 x 3 tiles along X, the mill's wall at +Y. A stone race walled along X
carrying the leat under the wheel; a timber wheel-house over it on posts with a pent roof of boards
falling away from the mill, open on its long side so the wheel shows; the axle running through a
bearing block toward the mill's wall. The wheel is its own model (WaterMillWheel), its origin on the
axle, so the game can turn it: place it at AXLE from the wheel-house's origin. Origin at the middle
of the footprint, at the water's surface.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\landmarks\\village\\water_mill.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""
import math
import os
import sys

import bmesh

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.props import bar, box, make, rod  # noqa: E402

MATS = ["Stone", "Granite", "Oak", "Timber", "Boards", "Iron", "Mere"]
STONE, GRANITE, OAK, TIMBER, BOARDS, IRON, WATER = range(len(MATS))
AXLE = (0.0, 0.0, 1.1)          # where WaterMillWheel's origin goes, from the wheel-house's
RADIUS = 1.45
RACE = 0.85                     # the race's half-width, inside its walls


def water_mill():
    bm = bmesh.new()
    L, w = 2.0, RACE
    for s in (-1, 1):                                                         # the race's walls and their copings
        box(bm, -L, s * (w + 0.15), -1.0, L, s * w, 0.4, STONE)
        box(bm, -L, s * w - 0.03, 0.4, L, s * (w + 0.18), 0.5, GRANITE)
    box(bm, -L, -w, -0.05, L, w, -0.04, WATER)
    for x in (-1.75, 1.75):                                                   # the wheel-house on its posts
        for y in (-w - 0.08, w + 0.08):
            box(bm, x - 0.08, y - 0.08, 0.5, x + 0.08, y + 0.08, 3.0 if y > 0 else 2.5, OAK)
        bar(bm, (x, -w - 0.08, 2.5), (x, w + 0.08, 3.0), 0.16, 0.14, OAK)
    for y, z in ((-w - 0.08, 2.5), (w + 0.08, 3.0)):
        box(bm, -1.9, y - 0.09, z, 1.9, y + 0.09, z + 0.14, OAK)
    A.hull(bm, [(x, y, z + t) for x in (-2.15, 2.15) for y, z in ((-w - 0.55, 2.38), (w + 0.2, 3.14)) for t in (0.0, 0.06)], BOARDS)
    for x in (-1.75, 1.75):                                                   # the end walls boarded down to the wheel's top
        A.hull(bm, [(x + dx, y, z) for dx in (-0.03, 0.03) for y, z in ((-w - 0.08, 2.5), (w + 0.08, 3.0),
                                                                        (-w - 0.08, 2.65), (w + 0.08, 2.65))], BOARDS)
    box(bm, -0.25, w - 0.05, 0.5, 0.25, w + 0.3, 1.5, GRANITE)                 # the bearing block by the mill wall
    rod(bm, (AXLE[0], -0.45, AXLE[2]), (AXLE[0], w + 0.3, AXLE[2]), 0.09, IRON, sides=8)
    return make("WaterMill", bm, MATS, "Landmarks")


def water_mill_wheel():
    """The undershot wheel, its origin on the axle (turned about Y in the game): two rims of
    segments, spokes, paddles across between the rims dipping into the race."""
    bm = bmesh.new()
    n, half, R = 12, 0.32, RADIUS
    for y in (-half, half):
        for k in range(n):
            a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
            A.hull(bm, [(r * math.cos(a), y + dy, r * math.sin(a)) for a in (a0, a1) for r in (R - 0.12, R) for dy in (-0.04, 0.04)], OAK)
        for k in range(0, n, 2):
            a = 2 * math.pi * k / n
            bar(bm, (0, y, 0), (R * 0.93 * math.cos(a), y, R * 0.93 * math.sin(a)), 0.07, 0.06, TIMBER, up=(0, 1, 0))
    for k in range(n):                                                        # the paddles, standing out past the rims
        a = 2 * math.pi * (k + 0.5) / n
        c = (math.cos(a), math.sin(a))
        A.hull(bm, [((R - 0.06 + r) * c[0] - t * c[1], y, (R - 0.06 + r) * c[1] + t * c[0]) for r in (0.0, 0.35)
                    for y in (-half - 0.04, half + 0.04) for t in (-0.025, 0.025)], BOARDS)
    A.loft(bm, [((0, -half - 0.1, 0), 0.18, 0.18), ((0, half + 0.1, 0), 0.18, 0.18)], sides=8, ref=(0, 0, 1), mats=TIMBER)
    return make("WaterMillWheel", bm, MATS, "Landmarks")


ASSETS = {
    "WaterMill": water_mill,
    "WaterMillWheel": water_mill_wheel,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
