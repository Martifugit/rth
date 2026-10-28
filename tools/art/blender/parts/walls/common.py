"""Walls, common. Dressed stone of no style: posts, columns, lintels, arches, a wall span.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Walls/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\walls\\common.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import sys

from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.caves import DOOR_H, SPAN, STONE, added, block, lay, make, rng, weather  # noqa: E402


def post(seed=1, at=(0, 0, 0), round_=False, broken=False):
    """A doorpost DOOR_H high: a square shaft (or a round column) on a base block, under a cap.
    broken: snapped at chest height, its top lying at its foot."""
    r = rng(seed)
    bm = bmesh.new()
    top = 1.3 if broken else DOOR_H - 0.18
    block(bm, (-0.32, -0.32, -0.1), (0.32, 0.32, 0.22), r)
    if round_:
        A.prism(bm, 8, 0.24, 0.22, 0.2, top, (0, 0), mat_index=0, cap_top=True)
    else:
        block(bm, (-0.23, -0.23, 0.2), (0.23, 0.23, top), r, chip=0.015)
    if broken:
        for v in bm.verts:
            if v.co.z > top - 0.01:
                v.co.z += r.uniform(-0.18, 0.06)
        before = set(bm.verts)
        block(bm, (-0.23, -0.23, 0), (0.23, 0.23, 1.0), r)
        lay(bm, added(bm, before), Matrix.Translation((0.55, -0.45, 0.23)) @ Matrix.Rotation(math.radians(84), 4, Vector((0.3, 1, 0)).normalized()))
    else:
        block(bm, (-0.3, -0.3, top), (0.3, 0.3, DOOR_H), r)
    weather(bm, r, 0.012, moss=1, up=0.9)
    name = ("Column" if round_ else "Post") + ("Broken" if broken else "")
    return make(name, bm, [STONE, "Moss"], at=at, smooth=30 if round_ else 20)


def lintel(seed=1, at=(0, 0, 0), long=False):
    """A plain lintel across two posts SPAN apart (or 1.5 x SPAN, long), lying on their caps."""
    r = rng(seed)
    bm = bmesh.new()
    half = (SPAN * (1.5 if long else 1.0)) / 2 + 0.35
    block(bm, (-half, -0.26, DOOR_H), (half, 0.26, DOOR_H + 0.48), r, chip=0.03)
    weather(bm, r, 0.012, moss=1, up=0.9)
    return make("LintelLong" if long else "Lintel", bm, [STONE, "Moss"], at=at, smooth=20)


def arch(seed=1, at=(0, 0, 0), broken=False):
    """A round arch over a doorway SPAN wide: jambs of three stacked blocks each, and seven
    wedge-shaped voussoirs springing from them. broken: the keystone and its neighbour fallen, lying
    at the foot."""
    r = rng(seed)
    bm = bmesh.new()
    spring = 1.6
    inner, outer, d = SPAN / 2 - 0.25, SPAN / 2 + 0.3, 0.3
    for s in (-1, 1):
        for k in range(3):
            z0 = spring * k / 3
            block(bm, (s * SPAN / 2 - 0.3, -d, z0 - (0.1 if k == 0 else 0)), (s * SPAN / 2 + 0.3, d, z0 + spring / 3 - 0.01), r)
    n = 7
    skip = {3, 4} if broken else set()
    for k in range(n):
        if k in skip:
            continue
        a0, a1 = math.pi * k / n + 0.01, math.pi * (k + 1) / n - 0.01
        pts = []
        for a in (a0, a1):
            for rad in (inner, outer):
                for y in (-d, d):
                    pts.append((math.cos(math.pi - a) * rad, y, spring + math.sin(a) * rad))
        A.hull(bm, pts, mat=0)
    if broken:
        for k, (x, y, a) in enumerate(((0.9, -1.1, 0.4), (-0.4, -1.4, 1.9))):
            before = set(bm.verts)
            block(bm, (-0.28, -0.3, 0), (0.28, 0.3, 0.5), r)
            lay(bm, added(bm, before), Matrix.Translation((x, y, 0.05)) @ Matrix.Rotation(a, 4, 'Z')
                @ Matrix.Rotation(math.radians(20), 4, 'X'))
    weather(bm, r, 0.015, moss=1, up=0.85)
    return make("ArchBroken" if broken else "Arch", bm, [STONE, "Moss"], at=at, smooth=20)


def wall_span(seed=1, at=(0, 0, 0)):
    """One tile of coursed wall along X, DOOR_H high, to face a hillside either side of a built
    doorway: four courses of blocks, each course's joints offset, the top course ragged."""
    r = rng(seed)
    bm = bmesh.new()
    course = DOOR_H / 4
    for k in range(4):
        z0 = course * k - (0.1 if k == 0 else 0)
        cut = r.uniform(-0.15, 0.15) + (0.25 if k % 2 else -0.25)
        for x0, x1 in ((-0.5, cut), (cut, 0.5)):
            top = course * (k + 1) - 0.01 - (r.uniform(0, 0.15) if k == 3 else 0)
            block(bm, (x0 + 0.005, -0.25, z0), (x1 - 0.005, 0.25, top), r, chip=0.02)
    weather(bm, r, 0.01, moss=1, up=0.9)
    return make("WallSpan", bm, [STONE, "Moss"], at=at, smooth=20)


ASSETS = {
    "Post": post,
    "PostBroken": lambda at=(0, 0, 0): post(at=at, broken=True),
    "Column": lambda at=(0, 0, 0): post(at=at, round_=True),
    "ColumnBroken": lambda at=(0, 0, 0): post(at=at, round_=True, broken=True),
    "Lintel": lintel,
    "LintelLong": lambda at=(0, 0, 0): lintel(at=at, long=True),
    "Arch": arch,
    "ArchBroken": lambda at=(0, 0, 0): arch(at=at, broken=True),
    "WallSpan": wall_span,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
