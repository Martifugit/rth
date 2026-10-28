"""Fixtures, village. What hangs on the village kit's walls, in a wall tile's frame: banner, lantern, a woodpile and a bench against the wall.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Fixtures/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\fixtures\\village.py")
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
from artlib.village import (  # noqa: E402
    BANNER, BARK, BRONZE, CANDLE, IRON, LOG_END, OAK,
    PLANKS, STOREY, T, box, piece)


LIFT = STOREY - 2.3             # the lantern and banner were placed for a 2.3 storey


def piece_banner():
    """A church banner on an iron bracket: the regime's red. An attachment: it hangs on a plain
    wall tile, in that tile's frame."""
    bm = bmesh.new()
    c = 0.5                                                    # the middle of a wall tile
    box(bm, c - 0.02, c + 0.02, -0.32, -T / 2, 1.72, 1.76, IRON)
    box(bm, c - 0.24, c + 0.24, -0.33, -0.29, 1.70, 1.74, IRON)
    A.hull(bm, [(c + x, y, z) for x, z in ((-0.22, 1.7), (0.22, 1.7), (0.22, 0.75), (0.0, 0.62), (-0.22, 0.75))
                for y in (-0.315, -0.30)], BANNER)
    box(bm, c - 0.05, c + 0.05, -0.32, -0.316, 1.22, 1.32, BRONZE)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, LIFT))
    return bm


def piece_lantern():
    """Beside a door: an iron bracket and a small lantern with its candle (a doorway attachment)."""
    bm = bmesh.new()
    x = 0.97
    box(bm, x - 0.02, x + 0.02, -T / 2 - 0.25, -T / 2, 1.88, 1.92, IRON)
    box(bm, x - 0.07, x + 0.07, -T / 2 - 0.32, -T / 2 - 0.18, 1.62, 1.64, IRON)
    box(bm, x - 0.06, x + 0.06, -T / 2 - 0.31, -T / 2 - 0.19, 1.64, 1.80, CANDLE)
    A.hull(bm, [(x + dx, -T / 2 - 0.25 + dy, 1.80) for dx in (-0.08, 0.08) for dy in (-0.07, 0.07)]
           + [(x, -T / 2 - 0.25, 1.9)], IRON)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, LIFT))
    return bm


def piece_woodpile():
    """Split logs stacked against a plain wall, end grain out."""
    bm = bmesh.new()
    rows = ((0.09, 5, 0.0), (0.25, 4, 0.085), (0.41, 3, 0.17))
    for z, n, shift in rows:
        for i in range(n):
            x = 0.16 + shift + i * 0.17
            A.loft(bm, [((x, -T / 2 - 0.4, z), 0.08, 0.08), ((x, -T / 2 - 0.02, z), 0.08, 0.08)],
                   sides=5, ref=(0, 0, 1), mats=[BARK])
            A.loft(bm, [((x, -T / 2 - 0.401, z), 0.0, 0.0), ((x, -T / 2 - 0.4005, z), 0.072, 0.072)],
                   sides=5, ref=(0, 0, 1), mats=[LOG_END])          # the cut end
    box(bm, 0.04, 0.96, -T / 2 - 0.44, -T / 2, 0.52, 0.56, OAK)          # a board keeping the rain off
    return bm


def piece_bench():
    bm = bmesh.new()
    box(bm, 0.1, 0.9, -T / 2 - 0.36, -T / 2 - 0.04, 0.42, 0.48, PLANKS)
    for x in (0.16, 0.78):
        box(bm, x, x + 0.06, -T / 2 - 0.32, -T / 2 - 0.08, 0, 0.42, OAK)
    return bm


ASSETS = {
    "Banner": piece(piece_banner),
    "Lantern": piece(piece_lantern),
    "Woodpile": piece(piece_woodpile),
    "Bench": piece(piece_bench),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
