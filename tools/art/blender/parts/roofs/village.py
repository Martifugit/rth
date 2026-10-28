"""Roofs, village. The village kit's roofs, a tile each: eaves, slopes, ridges and verges, the chimney stack and cap, the bellcote.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Roofs/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\roofs\\village.py")
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
    BRONZE, DARK, RISE, ROOF_BASE, STONE, STOREY, T,
    THATCH, TIMBER, box, piece)


OVERHANG = 0.35                 # eaves past the wall
VERGE = 0.4                     # roof past the gable ends
SLAB = 0.09


def roof_slab(bm, run0, run1, x0=0.0, x1=1.0):
    pts = []
    for r in (run0, run1):
        z = ROOF_BASE + RISE * r
        for x in (x0, x1):
            pts += [(x, r, z), (x, r, z + SLAB)]
    return A.hull(bm, pts, THATCH)


def piece_roof_eave(x0=0.0, x1=1.0, plate=True):
    def make():
        bm = bmesh.new()
        roof_slab(bm, -OVERHANG, 1.0, x0, x1)
        z = ROOF_BASE - RISE * OVERHANG
        box(bm, x0, x1, -OVERHANG - 0.04, -OVERHANG + 0.12, z - 0.06, z + SLAB, THATCH)
        if plate:                                              # the wall plate, closing the eave
            top = ROOF_BASE - RISE * (T / 2 + 0.02) + SLAB * 0.5
            box(bm, x0, x1, -T / 2 - 0.02, T / 2 + 0.02, STOREY - 0.02, top, TIMBER)
        return bm
    return make


def piece_roof_slope(x0=0.0, x1=1.0):
    def make():
        bm = bmesh.new()
        roof_slab(bm, 0.0, 1.0, x0, x1)
        return bm
    return make


def piece_roof_ridge(x0=0.0, x1=1.0):
    def make():
        bm = bmesh.new()
        z = ROOF_BASE + SLAB
        A.hull(bm, [(x, y, zz) for x in (x0, x1) for y, zz in ((-0.16, z - RISE * 0.16), (0.16, z - RISE * 0.16),
                                                              (-0.06, z + 0.08), (0.06, z + 0.08))], THATCH)
        return bm
    return make


def piece_bellcote():
    """A small stone frame on a gable's apex with one bronze bell (origin at the apex)."""
    bm = bmesh.new()
    for x in (-0.26, 0.18):
        box(bm, x, x + 0.08, -0.12, 0.12, -0.1, 0.75, STONE)
    A.hull(bm, [(x, y, z) for x, z in ((-0.3, 0.75), (0.3, 0.75), (0, 1.05)) for y in (-0.13, 0.13)], STONE)
    box(bm, -0.3, 0.3, -0.13, 0.13, -0.15, -0.05, STONE)
    A.loft(bm, [((0, 0, 0.62), 0.03, 0.03), ((0, 0, 0.55), 0.1, 0.1), ((0, 0, 0.3), 0.14, 0.14),
                ((0, 0, 0.26), 0.0, 0.0)], sides=8, ref=(0, 1, 0), mats=BRONZE)
    return bm


def piece_chimney_stack():
    """One tile's height of stack above the breast (stacked as high as the roof needs)."""
    bm = bmesh.new()
    box(bm, 0.28, 0.72, -T / 2 - 0.36, T / 2 - 0.04, 0, 1.0, STONE)
    return bm


def piece_chimney_cap():
    bm = bmesh.new()
    box(bm, 0.24, 0.76, -T / 2 - 0.40, T / 2 - 0.04, 0, 0.12, STONE)
    box(bm, 0.36, 0.64, -T / 2 - 0.3, T / 2 - 0.1, 0.12, 0.125, DARK)
    return bm


ASSETS = {
    "RoofEave": piece(piece_roof_eave()),
    "RoofSlope": piece(piece_roof_slope()),
    "RoofRidge": piece(piece_roof_ridge()),
    "VergeEave": piece(piece_roof_eave(0, VERGE, plate=False)),
    "VergeSlope": piece(piece_roof_slope(0, VERGE)),
    "VergeRidge": piece(piece_roof_ridge(0, VERGE)),
    "Bellcote": piece(piece_bellcote),
    "ChimneyStack": piece(piece_chimney_stack),
    "ChimneyCap": piece(piece_chimney_cap),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
