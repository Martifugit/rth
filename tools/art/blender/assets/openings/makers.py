"""Openings, makers. The makers' ways in: the unfinished door and the seal stone.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
makers.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Openings/Makers/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\openings\\makers.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import sys

from mathutils import Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.caves import (  # noqa: E402
    DOOR_H, INLAY, MAKER, MAKER_MATS, SPAN, added, along_x,
    block, chunk, lay, make, rng, twisted, weather)


def unfinished_door(seed=4, at=(0, 0, 0)):
    """A doorway caught half made: the left post a finished twisted column, the right one still raw
    rock with only its foot cut square; the lintel twists out from the left post as worked stone and
    turns into rough rock across the middle, where a seam of inlay marks where the making stopped."""
    r = rng(seed)
    bm = bmesh.new()
    twist = math.radians(70)
    half = SPAN / 2
    before = set(bm.verts)
    twisted(bm, -0.1, DOOR_H + 0.1, (0.24, 0.24), (0.19, 0.19), 0.0, twist, 0)
    lay(bm, added(bm, before), Matrix.Translation((-half, 0, 0)))
    block(bm, (half - 0.26, -0.24, -0.1), (half + 0.26, 0.24, 0.85), r, chip=0.02)          # the cut foot
    z = DOOR_H + 0.22
    before = set(bm.verts)
    twisted(bm, 0, half + 0.35, (0.24, 0.2), (0.24, 0.2), 0.0, math.radians(45), 0)
    along_x(bm, added(bm, before), -half - 0.3, z)
    weather(bm, r, 0.02, moss=4)
    chunk(bm, (half + 0.05, 0.02, 0.8), (0.36, 0.33, DOOR_H - 0.55), r, mats=(2, 3, 4))       # the raw rest
    chunk(bm, (0.85, 0.0, z - 0.3), (0.95, 0.3, 0.6), r, sink=0.0, mats=(2, 3, 4))
    A.box(bm, (0.03, -0.27, z - 0.27), (0.09, 0.27, z + 0.27), 1)                          # the seam
    return make("UnfinishedDoor", bm, [MAKER, INLAY, "Scree", "Cinder", "Moss"], at=at, smooth=20)


def seal_stone(seed=1, at=(0, 0, 0)):
    """The round stone that once sealed a doorway, rolled half aside: a thick disc on its edge,
    leaning, sunk a little, moss on its rim, a raised square on its face turned on its corner with a
    small inlay in it."""
    r = rng(seed)
    bm = bmesh.new()
    A.prism(bm, 12, 0.95, 0.95, -0.12, 0.12, (0, 0), mat_index=0, cap_top=True, cap_bottom=True)
    A.prism(bm, 4, 0.42, 0.38, 0.12, 0.2, (0, 0), rot=math.radians(45), mat_index=0, cap_top=True)
    A.prism(bm, 4, 0.14, 0.14, 0.2, 0.215, (0, 0), rot=math.radians(45), mat_index=1, cap_top=True)
    lay(bm, list(bm.verts), Matrix.Translation((0, 0, 0.82)) @ Matrix.Rotation(math.radians(-8), 4, 'Y')
        @ Matrix.Rotation(math.radians(90), 4, 'X'))
    weather(bm, r, 0.02, moss=2, up=0.8)
    return make("SealStone", bm, MAKER_MATS, at=at, smooth=30)


ASSETS = {
    "UnfinishedDoor": unfinished_door,
    "SealStone": seal_stone,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
