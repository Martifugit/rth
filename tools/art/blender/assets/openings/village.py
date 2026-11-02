"""Openings, village. The village's door leaf, hung in a doorway by the building kit, and the
churchyard's lychgate, and the cellar trapdoor, shut and open.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Openings/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\openings\\village.py")
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
from artlib.village import DARK, DOOR, IRON, OAK, PLANKS, RISE, STONE, THATCH, TIMBER, box, piece  # noqa: E402


def piece_door():
    """Planks behind heavy iron: three straps and studs (the iron worn openly)."""
    bm = bmesh.new()
    box(bm, 0.08, 0.92, -0.03, 0.03, 0.04, DOOR, PLANKS)
    for z in (0.35, 0.35 + (DOOR - 0.5) / 2, DOOR - 0.3):
        box(bm, 0.08, 0.84, -0.05, -0.03, z, z + 0.08, IRON)
        for x in (0.25, 0.47, 0.69):
            box(bm, x - 0.018, x + 0.018, -0.065, -0.05, z + 0.022, z + 0.058, IRON)
    box(bm, 0.78, 0.83, -0.07, -0.03, DOOR / 2, DOOR / 2 + 0.07, IRON)
    return bm


def lychgate():
    """The churchyard's roofed gate, 2 x 2, the path running through it along Y. Low stone walls on
    each side carry two pairs of oak posts and their wall-plates; over them a steep thatched roof at
    the kit's pitch, its ridge along Y; a pair of plank gates hung on the outer posts (-Y), and
    across the walls inside, the slab where the dead were rested on the way in."""
    bm = bmesh.new()
    half, depth, wall_h, plate = 0.95, 0.9, 0.55, 2.05
    for s in (-1, 1):
        box(bm, s * half - 0.15, s * half + 0.15, -depth, depth, 0, wall_h, STONE)
        box(bm, s * half - 0.17, s * half + 0.17, -depth - 0.02, depth + 0.02, wall_h, wall_h + 0.05, STONE)
        for y in (-depth + 0.08, depth - 0.08):
            box(bm, s * half - 0.07, s * half + 0.07, y - 0.07, y + 0.07, wall_h + 0.05, plate, OAK)
            A.hull(bm, [(s * half + dx, y, z) for dx in (-0.05, 0.05) for z in (plate - 0.4, plate - 0.32)] +
                   [(s * half - s * 0.35 + dx, y, plate - 0.02) for dx in (-0.05, 0.05)] +
                   [(s * half + dx, y, plate - 0.02) for dx in (-0.05, 0.05)], OAK)            # the brace
        box(bm, s * half - 0.08, s * half + 0.08, -depth - 0.15, depth + 0.15, plate, plate + 0.12, OAK)
    for y in (-depth + 0.08, depth - 0.08):
        box(bm, -half, half, y - 0.06, y + 0.06, plate - 0.02, plate + 0.1, OAK)                # the tie-beams
    run = half + 0.35
    rise = run * RISE
    ridge = plate + 0.12 + rise
    for s in (-1, 1):                                                                         # the roof's two slopes
        A.hull(bm, [(s * x, y, z) for x, z in ((0.0, ridge), (run, plate + 0.12)) for y in (-depth - 0.35, depth + 0.35)] +
               [(s * x, y, z + 0.16) for x, z in ((0.0, ridge), (run, plate + 0.12)) for y in (-depth - 0.35, depth + 0.35)], THATCH)
    for y in (-depth - 0.3, depth + 0.3):                                                     # the gables, boarded
        A.hull(bm, [(x, y + dy, plate + 0.12) for x in (-half - 0.05, half + 0.05) for dy in (-0.02, 0.02)] +
               [(0.0, y + dy, ridge - 0.05) for dy in (-0.02, 0.02)], PLANKS)
    for s in (-1, 1):                                                                         # the gates, a little open
        hinge = s * (half - 0.08)
        a = math.radians(20)
        for k in range(4):
            x0 = 0.08 + k * 0.205
            pts = [(hinge - s * (x0 + dx) * math.cos(a), -depth - 0.08 - (x0 + dx) * math.sin(a), z)
                   for dx in (0.0, 0.19) for z in (0.05, 1.05)]
            A.hull(bm, pts + [(x, y + 0.03, z) for x, y, z in pts], PLANKS)
        for z in (0.3, 0.85):
            pts = [(hinge - s * dx * math.cos(a), -depth - 0.12 - dx * math.sin(a), z + dz) for dx in (0.05, 0.85) for dz in (0.0, 0.08)]
            A.hull(bm, pts + [(x, y - 0.03, z) for x, y, z in pts], IRON)
    box(bm, -half + 0.15, half - 0.15, -0.3, 0.3, wall_h - 0.08, wall_h, STONE)               # the coffin rest
    return bm


def trapdoor(open_=False):
    """A cellar trapdoor in a floor, one tile, flush with it at z = 0: a frame of timbers round the
    opening, a door of boards on two iron straps with a ring to lift it. open_: the door thrown back
    on its hinges at +Y and resting against its prop, the dark of the hole and the ladder's top showing."""
    bm = bmesh.new()
    h = 0.4
    for x0, x1, y0, y1 in ((-h - 0.08, -h, -h - 0.08, h + 0.08), (h, h + 0.08, -h - 0.08, h + 0.08),
                           (-h, h, -h - 0.08, -h), (-h, h, h, h + 0.08)):
        box(bm, x0, x1, y0, y1, -0.04, 0.02, TIMBER)
    if not open_:
        box(bm, -h, h, -h, h, -0.02, 0.015, PLANKS)
        for x in (-0.22, 0.22):
            box(bm, x - 0.03, x + 0.03, -h, h, 0.015, 0.022, IRON)
        A.loft(bm, [((0, -0.2, 0.025), 0.05, 0.05), ((0, -0.2, 0.03), 0.05, 0.05)], sides=8, ref=(1, 0, 0), mats=IRON)
        return bm
    box(bm, -h, h, -h, h, -1.2, -1.18, DARK)                                                    # the dark below
    for x in (-h, h - 0.02):
        box(bm, x, x + 0.02, -h, h, -1.2, -0.04, DARK)
    for y in (-h, h - 0.02):
        box(bm, -h, h, y, y + 0.02, -1.2, -0.04, DARK)
    for x in (-0.18, 0.18):                                                                     # the ladder's rails sticking up
        box(bm, x - 0.025, x + 0.025, -0.1, -0.05, -1.2, 0.45, TIMBER)
    for z in (-0.9, -0.5, -0.1, 0.25):
        box(bm, -0.18, 0.18, -0.09, -0.06, z, z + 0.035, TIMBER)
    tilt = math.radians(100)                                                                    # the door, thrown back past upright
    pts = []
    for x in (-h, h):
        for t in (0.0, 0.035):
            for along in (0.0, 2 * h):
                pts.append((x, h - along * math.cos(tilt) + t * math.sin(tilt), along * math.sin(tilt) + t * math.cos(tilt) + 0.02))
    A.hull(bm, pts, PLANKS)
    return bm


ASSETS = {
    "Door": piece(piece_door),
    "Lychgate": piece(lychgate),
    "Trapdoor": piece(trapdoor),
    "TrapdoorOpen": piece(lambda: trapdoor(open_=True)),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
