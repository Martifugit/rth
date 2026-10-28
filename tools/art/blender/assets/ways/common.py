"""Ways, common. Ways of no style: stepping stones.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Ways/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\ways\\common.py")
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
import artlib.nature as N  # noqa: E402


def stepping_stones(seed=1, at=(0, 0, 0), water=0.0):
    """Stones across a shallow stream along Y, one a tile: rounded boulders worn flat on top by
    feet, their sides dark with wet, a hand above the water (`water` is its level, relative to the
    origin)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    top = water + 0.1
    for k in range(4):
        x, y = rnd.uniform(-0.12, 0.12), -1.5 + k
        r = rnd.uniform(0.28, 0.35)
        faces = N.blob(bm, (x, y, water - 0.05), (r, r * rnd.uniform(0.8, 0.95), 0.3), 1, rnd, jitter=(0.85, 1.1))
        for f in faces:
            for v in f.verts:
                v.co.z = min(v.co.z, top + rnd.uniform(-0.015, 0.015))   # worn flat by feet
        for f in faces:
            f.material_index = 0 if f.calc_center_median().z > top - 0.04 else 1
    return N.make("SteppingStones", bm, ["Scree", "Cinder"], smooth_angle=30, at=at)


ASSETS = {
    "SteppingStones": stepping_stones,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
