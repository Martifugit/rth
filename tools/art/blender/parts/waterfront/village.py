"""Waterfront, village. The basin's waterfront: a timber jetty span, the mill leat's sluice.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Waterfront/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\waterfront\\village.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
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
from artlib.nature import make, rod  # noqa: E402


def jetty_span(seed=1, at=(0, 0, 0), rot=0.0):
    """One tile of jetty along X, centred on its tile, its origin at the water's surface: planks
    laid across on two stringers, each end carried by a pair of piles driven into the bed."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    deck = 0.32
    for k in range(5):
        x0 = -0.5 + 0.2 * k + 0.01
        dz = rnd.uniform(-0.015, 0.015)
        A.box(bm, (x0, -0.55 + rnd.uniform(-0.03, 0.03), deck - 0.05 + dz),
              (x0 + 0.18, 0.55 + rnd.uniform(-0.03, 0.03), deck + dz), 0)
    for y in (-0.42, 0.42):
        A.box(bm, (-0.5, y - 0.05, deck - 0.14), (0.5, y + 0.05, deck - 0.05), 1)
        rod(bm, [(-0.45, y, -0.9), (-0.45, y, deck - 0.02)], 0.07, mat=1, sides=6)
    return make("JettySpan", bm, ["Planks", "Driftwood"], smooth_angle=30, at=at, rot=rot)


def sluice(seed=1, at=(0, 0, 0)):
    """A timber sluice across a leat, one tile, the water running along Y under it; origin at the
    water's surface. Two heavy grooved posts driven into the bed, a cross-beam over them carrying a
    winding post with its handle, the gate of boards raised a hand in the grooves so the water slips
    out under it, a sill beam across the bed."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    BOARD, POST, IRON = range(3)
    for s in (-1, 1):
        A.box(bm, (s * 0.55 - 0.09, -0.12, -0.9), (s * 0.55 + 0.09, 0.12, 1.25), POST)
        A.box(bm, (s * 0.55 - 0.1, -0.05, -0.3), (s * 0.55 + 0.1, 0.05, 1.1), POST)           # the cheek the groove is cut in
    A.box(bm, (-0.75, -0.13, 1.25), (0.75, 0.13, 1.42), POST)                                   # the cross-beam
    A.box(bm, (-0.55, -0.1, -0.35), (0.55, 0.1, -0.25), POST)                                   # the sill
    lift = 0.12
    for k in range(5):                                                                         # the gate's boards, laid across
        z = -0.25 + lift + 0.2 * k
        A.box(bm, (-0.46, -0.025, z + rnd.uniform(-0.005, 0.005)), (0.46, 0.025, z + 0.19), BOARD)
    A.box(bm, (-0.04, -0.035, -0.25 + lift + 1.0), (0.04, 0.035, 1.25), POST)                   # the gate's rod up through the beam
    A.box(bm, (-0.06, -0.06, 1.42), (0.06, 0.06, 1.75), POST)                                   # the winding post
    rod(bm, [(0.0, 0.0, 1.62), (0.0, -0.3, 1.62)], 0.02, mat=IRON, sides=4)
    rod(bm, [(0.0, -0.3, 1.62), (0.0, -0.3, 1.45)], 0.02, mat=IRON, sides=4)                   # its handle
    return make("Sluice", bm, ["Planks", "Oak", "Iron"], smooth_angle=30, at=at)


ASSETS = {
    "JettySpan": jetty_span,
    "Sluice": sluice,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
