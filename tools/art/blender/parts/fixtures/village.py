"""Fixtures, village. What hangs on the village kit's walls, in a wall tile's frame: banner, lantern, a woodpile and a bench against the wall, signs (one hung out
over the street, one fixed over the door) and a candle sconce.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Fixtures/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\fixtures\\village.py")
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
from artlib.village import (  # noqa: E402
    BANNER, BARK, BRONZE, CANDLE, DARK, DOOR, IRON, LOG_END, OAK,
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


def piece_hanging_sign():
    """A shop sign hung out over the street: an iron bracket jutting from the wall with a scroll under
    it, a board swinging from two rings below it, end-on to the wall so it is read from up and down
    the street, the shop's mark painted on both faces. In a wall tile's frame."""
    bm = bmesh.new()
    c, z = 0.5, 2.15
    box(bm, c - 0.02, c + 0.02, -0.85, -T / 2, z, z + 0.04, IRON)                         # the arm
    box(bm, c - 0.03, c + 0.03, -T / 2 - 0.02, -T / 2, z - 0.35, z + 0.06, IRON)            # its wall plate
    for y0, y1, z0, z1 in ((-T / 2 - 0.02, -0.4, z - 0.32, z - 0.02), (-0.4, -0.6, z - 0.04, z - 0.0)):
        A.hull(bm, [(c + dx, y, zz) for dx in (-0.012, 0.012) for y, zz in ((y0, z0), (y1, z1))] +
               [(c + dx, y1, z1 - 0.025) for dx in (-0.012, 0.012)], IRON)              # the stay
    for y in (-0.3, -0.75):
        box(bm, c - 0.01, c + 0.01, y - 0.01, y + 0.01, z - 0.1, z, IRON)                   # the rings
    box(bm, c - 0.025, c + 0.025, -0.82, -0.23, z - 0.62, z - 0.1, PLANKS)                  # the board
    box(bm, c - 0.03, c + 0.03, -0.82, -0.23, z - 0.13, z - 0.1, OAK)
    box(bm, c - 0.03, c + 0.03, -0.82, -0.23, z - 0.62, z - 0.59, OAK)
    for s in (-1, 1):                                                                        # its mark, both faces
        x = c + s * 0.027
        A.hull(bm, [(x, -0.525 + 0.13 * math.cos(a), z - 0.36 + 0.13 * math.sin(a)) for a in
                    (2 * math.pi * k / 8 for k in range(8))] + [(x - s * 0.002, -0.525, z - 0.36)], DARK)
    return bm


def piece_shop_sign():
    """A shop's name board fixed flat over its door: a long board in a moulded frame on two iron
    brackets, the lettering painted dark in a band across it. In a wall tile's frame, the board
    spanning most of the tile."""
    bm = bmesh.new()
    z0, z1 = DOOR + 0.15, DOOR + 0.5
    box(bm, 0.06, 0.94, -T / 2 - 0.06, -T / 2, z0, z1, PLANKS)
    for zz in (z0 - 0.03, z1):
        box(bm, 0.03, 0.97, -T / 2 - 0.08, -T / 2, zz, zz + 0.03, OAK)
    for x in (0.03, 0.94):
        box(bm, x, x + 0.03, -T / 2 - 0.08, -T / 2, z0 - 0.03, z1 + 0.03, OAK)
    for x in (0.2, 0.8):
        box(bm, x - 0.015, x + 0.015, -T / 2 - 0.1, -T / 2, z0 - 0.1, z0 - 0.03, IRON)
    for k in range(6):                                                                       # the letters, as dark strokes
        x = 0.16 + 0.12 * k
        box(bm, x, x + 0.08, -T / 2 - 0.063, -T / 2 - 0.06, z0 + 0.1, z1 - 0.1, DARK)
    return bm


def piece_sconce():
    """An iron sconce on the wall of the undercroft or the chapel: a back plate, a curled arm, a
    drip pan and a candle on its spike. In a wall tile's frame, at the tile's middle."""
    bm = bmesh.new()
    c, z = 0.5, 1.6
    box(bm, c - 0.05, c + 0.05, -T / 2 - 0.015, -T / 2, z - 0.2, z + 0.12, IRON)
    A.hull(bm, [(c + dx, y, zz) for dx in (-0.012, 0.012) for y, zz in ((-T / 2, z - 0.15), (-T / 2, z - 0.1),
                                                                       (-T / 2 - 0.2, z), (-T / 2 - 0.2, z + 0.03))], IRON)
    A.loft(bm, [((c, -T / 2 - 0.2, z), 0.02, 0.02), ((c, -T / 2 - 0.2, z + 0.03), 0.075, 0.075)], sides=8, ref=(1, 0, 0), mats=IRON)
    A.loft(bm, [((c, -T / 2 - 0.2, z + 0.03), 0.028, 0.028), ((c, -T / 2 - 0.2, z + 0.22), 0.026, 0.026)], sides=6,
           ref=(1, 0, 0), mats=CANDLE)
    A.spike(bm, (c, -T / 2 - 0.2, z + 0.215), 0.013, (c, -T / 2 - 0.2, z + 0.28), sides=4, mat=CANDLE)                            # the flame: the kit's candle glow
    return bm


ASSETS = {
    "Banner": piece(piece_banner),
    "Lantern": piece(piece_lantern),
    "Woodpile": piece(piece_woodpile),
    "Bench": piece(piece_bench),
    "HangingSign": piece(piece_hanging_sign),
    "ShopSign": piece(piece_shop_sign),
    "Sconce": piece(piece_sconce),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
