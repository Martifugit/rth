"""Walls, mine. A mine's timbering: the frame round an adit.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
mine.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Walls/Mine/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\walls\\mine.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
import artlib.nature as N  # noqa: E402
from artlib.caves import make, rng  # noqa: E402


def timber_frame(seed=2, at=(0, 0, 0)):
    """A doorway or adit framed in timber: two posts leaning in a little under a cap beam, a wedge
    driven in at each joint."""
    r = rng(seed)
    bm = bmesh.new()
    top = 2.05
    for s in (-1, 1):
        N.rod(bm, N.wobble(r, (s * 0.95, 0, -0.05), (s * 0.82, 0, top), 3, 0.03), 0.08, sides=6, taper=0.9)
        A.hull(bm, [(s * 0.7, -0.06, top - 0.12), (s * 0.7, 0.06, top - 0.12), (s * 0.74, 0, top - 0.02),
                    (s * 0.66, -0.06, top - 0.02), (s * 0.66, 0.06, top - 0.02)])
    N.rod(bm, N.wobble(r, (-1.15, 0, top + 0.06), (1.15, 0, top + 0.08), 4, 0.03), 0.09, sides=6)
    return make("TimberFrame", bm, ["Timber"], at=at, smooth=50)


ASSETS = {
    "TimberFrame": timber_frame,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
