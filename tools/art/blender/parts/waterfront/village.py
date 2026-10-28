"""Waterfront, village. The basin's waterfront: a timber jetty span.

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


ASSETS = {
    "JettySpan": jetty_span,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
