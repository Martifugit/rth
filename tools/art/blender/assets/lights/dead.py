r"""Lights, dead. The realm of the dead's lights along the long way back: soul lanterns, a small iron
cage holding a pale soul flame on top of a drowned post leaning out of the water, and the same lantern
spent (the flame gone, a dim cinder in the cage). Soul lanterns restore blood; a spent one has been used.

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


ASSETS = {
    "SoulLantern": soul_lantern,
    "SoulLanternSpent": lambda: soul_lantern(spent=True),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
