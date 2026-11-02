"""The wardens' gate on Palmerway: a timber gatehouse where the road passes the palisade. 6 x 3
tiles along X, the road running through along Y. Two square towers of squared logs on stone
footings, each with a dark lookout slit under a pent roof; a covered walk across the top joining
them under a shallow gable; the gateway between them three tiles wide. The gate is its own model
(WaygateGates: two heavy leaves of beams and a drop bar), so it can stand open or barred. The
palisade (parts/fences/village Palisade) runs off either side. Origin at the gateway's middle.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\landmarks\\village\\waygate.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""
import os
import random
import sys

import bmesh

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.props import bar, box, make  # noqa: E402

MATS = ["Granite", "DeadWood", "Timber", "Planks", "Iron", "Thatch", "Charcoal"]
STONE, LOGS, TIMBER, PLANKS, IRON, THATCH, DARK = range(len(MATS))
GAP = 1.5                       # half the gateway's width
TOWER = 1.4                     # a tower's side
TOP = 4.6                       # the walk's floor


def log_wall(bm, x0, x1, y0, y1, z0, z1, along_x, rnd):
    """A wall of squared logs laid one on another, each its own part so the grain runs along it."""
    z = z0
    while z < z1 - 0.01:
        h = min(0.24, z1 - z)
        j = rnd.uniform(-0.02, 0.02)
        if along_x:
            box(bm, x0 - 0.04 + j, y0, z, x1 + 0.04 + j, y1, z + h - 0.01, LOGS)
        else:
            box(bm, x0, y0 - 0.04 + j, z, x1, y1 + 0.04 + j, z + h - 0.01, LOGS)
        z += h


def waygate():
    rnd = random.Random(4)
    bm = bmesh.new()
    t = 0.22
    for s in (-1, 1):
        lo, hi = sorted((s * GAP, s * (GAP + TOWER)))
        box(bm, lo - 0.08, -TOWER / 2 - 0.08, 0.0, hi + 0.08, TOWER / 2 + 0.08, 0.4, STONE)
        log_wall(bm, lo, hi, -TOWER / 2, -TOWER / 2 + t, 0.4, TOP + 1.3, True, rnd)
        log_wall(bm, lo, hi, TOWER / 2 - t, TOWER / 2, 0.4, TOP + 1.3, True, rnd)
        log_wall(bm, lo, lo + t, -TOWER / 2, TOWER / 2, 0.4, TOP + 1.3, False, rnd)
        log_wall(bm, hi - t, hi, -TOWER / 2, TOWER / 2, 0.4, TOP + 1.3, False, rnd)
        for y in (-TOWER / 2 - 0.045, TOWER / 2 + 0.04):                      # a dark lookout slit, front and back
            box(bm, lo + 0.4, y, TOP + 0.5, hi - 0.4, y + 0.005, TOP + 0.85, DARK)
        A.hull(bm, [(x, y, z + dz) for x in (lo - 0.3, hi + 0.3) for y, z in ((-TOWER / 2 - 0.35, TOP + 1.45), (TOWER / 2 + 0.35, TOP + 1.95))
                    for dz in (0.0, 0.14)], THATCH)                            # the pent roof
        for x in (lo + 0.05, hi - 0.05):                                      # posts holding it up on the walls
            box(bm, x - 0.05, TOWER / 2 - 0.1, TOP + 1.3, x + 0.05, TOWER / 2, TOP + 1.95, TIMBER)
    # the walk across the top: a floor on two great beams, rails, a shallow gabled roof on posts
    for y in (-0.55, 0.55):
        box(bm, -GAP - 0.2, y - 0.12, TOP - 0.3, GAP + 0.2, y + 0.12, TOP, TIMBER)
    box(bm, -GAP, -0.7, TOP, GAP, 0.7, TOP + 0.08, PLANKS)
    for y in (-0.68, 0.68):
        for x in (-GAP + 0.1, 0.0, GAP - 0.1):
            box(bm, x - 0.05, y - 0.05, TOP + 0.08, x + 0.05, y + 0.05, TOP + 1.9, TIMBER)
        box(bm, -GAP, y - 0.04, TOP + 0.9, GAP, y + 0.04, TOP + 1.0, TIMBER)
    for s in (-1, 1):
        A.hull(bm, [(x, s * y, z + dz) for x in (-GAP - 0.1, GAP + 0.1) for y, z in ((0.0, TOP + 2.4), (0.95, TOP + 1.85))
                    for dz in (0.0, 0.12)], THATCH)
    for s in (-1, 1):                                                         # the gate posts the leaves hang on
        box(bm, s * GAP - 0.12, -0.12, 0.0, s * GAP + 0.12, 0.12, TOP - 0.3, TIMBER)
        box(bm, s * (GAP - 0.14) - 0.04, -0.2, 1.1, s * (GAP - 0.14) + 0.04, -0.12, 1.3, IRON)   # the bar's keeps
    return make("Waygate", bm, MATS, "Landmarks")


def waygate_gates():
    """The waygate's two leaves, shut and barred: each boarded over a frame of heavy rails, iron
    straps on the rails, a brace up from the hinge foot, the drop bar across both in its keeps.
    Origin at the gateway's middle, as Waygate's."""
    bm = bmesh.new()
    H = 2.9
    for s in (-1, 1):
        x0, x1 = s * 0.02, s * (GAP - 0.14)
        lo, hi = sorted((x0, x1))
        n = 6
        w = (hi - lo) / n
        for k in range(n):
            box(bm, lo + k * w + 0.005, -0.06, 0.05, lo + (k + 1) * w - 0.005, 0.0, H, PLANKS)
        for z in (0.35, H / 2, H - 0.3):
            box(bm, lo, -0.12, z - 0.09, hi, -0.06, z + 0.09, TIMBER)
            box(bm, lo + 0.05, -0.125, z + 0.1, hi - 0.05, -0.115, z + 0.15, IRON)
        bar(bm, (x1, -0.09, 0.35), (x0, -0.09, H - 0.3), 0.14, 0.06, TIMBER)
    box(bm, -GAP + 0.1, -0.24, 1.14, GAP - 0.1, -0.13, 1.26, TIMBER)          # the drop bar
    return make("WaygateGates", bm, MATS, "Landmarks")


ASSETS = {
    "Waygate": waygate,
    "WaygateGates": waygate_gates,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
