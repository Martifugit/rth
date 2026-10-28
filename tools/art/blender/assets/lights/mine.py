"""Lights, mine. A mine's light: the lantern post.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
mine.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Lights/Mine/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\lights\\mine.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
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


def lantern_post(seed=2, at=(0, 0, 0)):
    """A lantern on a post by a mine's mouth: a crooked post, an arm, an iron lantern hanging from
    it with warm panes (the light itself is the client's)."""
    r = rng(seed)
    bm = bmesh.new()
    N.rod(bm, N.wobble(r, (0, 0, -0.05), (0.03, 0, 2.2), 3, 0.03), 0.06, sides=5, taper=0.85)
    N.rod(bm, [(0.0, 0, 2.05), (0.45, 0, 2.12)], 0.035, sides=4)
    N.rod(bm, [(0.42, 0, 2.1), (0.42, 0, 1.85)], 0.01, mat=1, sides=3)
    A.prism(bm, 4, 0.1, 0.1, 1.55, 1.82, (0.42, 0), rot=math.radians(45), mat_index=2, cap_top=False)
    A.prism(bm, 4, 0.13, 0.03, 1.82, 1.92, (0.42, 0), rot=math.radians(45), mat_index=1, cap_top=True)
    A.prism(bm, 4, 0.12, 0.12, 1.5, 1.56, (0.42, 0), rot=math.radians(45), mat_index=1, cap_top=True, cap_bottom=True)
    return make("LanternPost", bm, ["Timber", "Blackiron", "Ember"], at=at, smooth=30)


ASSETS = {
    "LanternPost": lantern_post,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
