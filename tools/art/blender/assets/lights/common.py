"""Lights, common. Fires and candles of no style: the campfire (lit and burnt out), candles.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Lights/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\lights\\common.py")
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
from artlib.caves import make, rng  # noqa: E402


def campfire(seed=1, at=(0, 0, 0), burnt=False):
    """A fire laid in a ring of stones on a bed of ash: four logs leaned together over kindling
    (the flames are the game's to draw). burnt: the fire out, the logs burnt to black stubs."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    A.prism(bm, 9, 0.38, 0.34, -0.02, 0.025, mat_index=1, rot=rnd.uniform(0, 1))
    for k in range(9):
        a = 2 * math.pi * k / 9 + rnd.uniform(-0.1, 0.1)
        c = Vector((0.46 * math.cos(a), 0.46 * math.sin(a), 0))
        s = rnd.uniform(0.09, 0.12)
        pts = [c + Vector((rnd.uniform(-s, s), rnd.uniform(-s, s) * 0.8, -0.02)) for _ in range(4)]
        pts += [c + Vector((rnd.uniform(-s, s) * 0.6, rnd.uniform(-s, s) * 0.5, s * rnd.uniform(0.8, 1.1))) for _ in range(3)]
        for f in A.hull(bm, pts):
            f.material_index = 0
    if burnt:
        for k in range(4):
            a = 2 * math.pi * k / 4 + rnd.uniform(-0.3, 0.3)
            d = Vector((math.cos(a), math.sin(a), 0))
            N.rod(bm, [d * 0.3 + Vector((0, 0, 0.04)), d * 0.08 + Vector((0, 0, 0.07))], 0.04, mat=3, sides=5, taper=0.6)
        N.blob(bm, (0, 0, 0.02), (0.18, 0.16, 0.06), 0, rnd, mat=1, squash_below=0.0)
    else:
        top = Vector((0, 0, 0.42))
        for k in range(4):
            a = 2 * math.pi * k / 4 + 0.4 + rnd.uniform(-0.15, 0.15)
            d = Vector((math.cos(a), math.sin(a), 0))
            rings, faces = A.loft(bm, [(d * 0.3 + Vector((0, 0, 0.02)), 0.045, 0.045), (top + d * 0.03, 0.035, 0.035)],
                                  sides=5, ref=(0, 0, 1), mats=2)
            for f in faces:
                if f.calc_center_median().z < 0.18:
                    f.material_index = 3                       # charred where the flames reach
        for k in range(6):                                     # kindling
            a = rnd.uniform(0, 2 * math.pi)
            d = Vector((math.cos(a), math.sin(a), 0))
            N.rod(bm, [d * 0.15 + Vector((0, 0, 0.03)), d * 0.03 + Vector((0, 0, 0.2))], 0.012, mat=2, sides=3)
    return N.make("CampfireBurnt" if burnt else "Campfire", bm, ["Scree", "AshPlain", "Bark", "Charcoal"],
                  smooth_angle=40, at=at)


def candles(seed=5, at=(0, 0, 0)):
    """Candles burned down on a flat stone by a door, wax run over its edge, a few still lit."""
    r = rng(seed)
    bm = bmesh.new()
    N.slab(bm, (0, 0, -0.03), 0.35, 0.1, 7, r, rot=0.3)
    for k in range(7):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0.0, 0.24)
        x, y = d * math.cos(a), d * math.sin(a)
        h = r.uniform(0.05, 0.22)
        N.rod(bm, [(x, y, 0.06), (x, y, 0.06 + h)], r.uniform(0.022, 0.035), mat=2, sides=5)
        if k < 4:
            A.spike(bm, (x, y, 0.06 + h - 0.005), 0.014, (x, y, 0.06 + h + 0.06), sides=4, mat=3)
    return make("Candles", bm, ["Scree", "Gravestone", "CandleWax", "Ember"], at=at, smooth=30)


ASSETS = {
    "Campfire": campfire,
    "CampfireBurnt": lambda: campfire(burnt=True),
    "Candles": candles,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
