"""Walls, makers. The makers' stonework: pillars, a lintel, a threshold.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
makers.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Walls/Makers/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\walls\\makers.py")
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
from artlib.caves import (  # noqa: E402
    DOOR_H, MAKER_MATS, SPAN, added, along_x, lay, make,
    rng, twisted, weather)


def band(bm, z, half, angle, mat=1, depth=0.06, swell=1.07):
    """The inlaid band: a short ring a hair proud of a twisted column at height z."""
    twisted(bm, z - depth / 2, depth, (half * swell, half * swell), (half * swell, half * swell), angle, 0.0, mat, rings=1)


def maker_pillar(seed=1, at=(0, 0, 0), broken=False):
    """A doorpost of the makers: a pale square column twisting through 70 degrees as it rises,
    weathered, moss on its top, one band of inlay. broken: snapped at chest height, the break
    jagged, its top lying at its foot."""
    r = rng(seed)
    bm = bmesh.new()
    twist = math.radians(70)
    if broken:
        h = 1.35
        rings = twisted(bm, -0.1, h + 0.1, (0.24, 0.24), (0.215, 0.215), 0.0, twist * h / DOOR_H, 0, caps=False)
        peak = bm.verts.new((0.05, -0.03, h + 0.22))
        for k in range(4):
            rings[-1][k].co.z += r.uniform(-0.12, 0.08)
            bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 4], peak)).material_index = 0
        before = set(bm.verts)
        twisted(bm, 0, 0.9, (0.21, 0.21), (0.19, 0.19), 0.3, twist * 0.35, 0)
        lay(bm, added(bm, before), Matrix.Translation((0.55, -0.35, 0.21))
            @ Matrix.Rotation(math.radians(86), 4, Vector((0.3, 1, 0)).normalized()))
        band(bm, 0.55, 0.235, twist * 0.65 / DOOR_H)
    else:
        twisted(bm, -0.1, DOOR_H + 0.1, (0.24, 0.24), (0.19, 0.19), 0.0, twist, 0)
        band(bm, 1.95, 0.24 - 0.05 * 2.05 / 2.7, twist * 2.05 / 2.7)
    weather(bm, r, 0.02, moss=2)
    return make("MakerPillarBroken" if broken else "MakerPillar", bm, MAKER_MATS, at=at, smooth=20)


def maker_lintel(seed=1, at=(0, 0, 0)):
    """The makers' lintel for two pillars SPAN apart: a beam lying across their tops, twisting a
    quarter turn along its length, moss along its back, a band of inlay at its middle."""
    r = rng(seed)
    bm = bmesh.new()
    length = SPAN + 0.6
    z = DOOR_H + 0.22
    before = set(bm.verts)
    twisted(bm, 0, length, (0.24, 0.2), (0.24, 0.2), 0.0, math.radians(90), 0)
    along_x(bm, added(bm, before), -length / 2, z)
    before = set(bm.verts)
    twisted(bm, -0.03, 0.06, (0.26, 0.22), (0.26, 0.22), math.radians(45), 0, 1, rings=1)
    along_x(bm, added(bm, before), 0, z)
    weather(bm, r, 0.02, moss=2)
    return make("MakerLintel", bm, MAKER_MATS, at=at, smooth=20)


def maker_threshold(seed=1, at=(0, 0, 0)):
    """The makers' threshold: three treads rising towards the dark, each turned a few degrees from
    the one below, as if the stair had been wound a little."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(3):
        before = set(bm.verts)
        w = 1.0 - 0.07 * k
        A.box(bm, (-w, -0.45, -0.1), (w, 0.45 if k < 2 else 1.0, 0.15 * (k + 1)), 0)
        lay(bm, added(bm, before), Matrix.Translation((0, -0.55 + 0.38 * k, 0)) @ Matrix.Rotation(math.radians(4 * k), 4, 'Z'))
    weather(bm, r, 0.015, moss=2, up=0.95)
    return make("MakerThreshold", bm, MAKER_MATS, at=at, smooth=20)


ASSETS = {
    "MakerPillar": maker_pillar,
    "MakerPillarBroken": lambda at=(0, 0, 0): maker_pillar(at=at, broken=True),
    "MakerLintel": maker_lintel,
    "MakerThreshold": maker_threshold,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
