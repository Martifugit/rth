"""Tolling's bell tower, the town's mark on the skyline (docs/areas/tolling-bell-tower.md): a plain
parish tower of the Wennow basin, rubble stone in courses, three storeys and a low slate pyramid.

3 x 3 tiles, walls on the grid lines, the door on the south face (-Y). Bottom to top: a battered
granite plinth; the hall and the ringing chamber, a string course at each floor; slit windows
staggered round the faces; the open belfry, an arched opening in every face (two in the faces seen
most), the bronze bell hung in its timber frame on the belfry floor; the slate pyramid with a plain
iron finial (no cross: the faith's symbol is not decided). The inside floors, stair and ladders come
with the walk-in building; this is the shell. DrownedBelfry is its twin, standing in Knellmere. Origin at the middle of the footprint, on the ground.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\landmarks\\village\\bell_tower.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""
import math
import os
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
from artlib.props import arch, box, cyl, make, rod  # noqa: E402

MATS = ["Stone", "Granite", "Slate", "Oak", "Timber", "Bronze", "Planks", "Iron", "Charcoal", "Fen"]
STONE, GRANITE, SLATE, OAK, TIMBER, BRONZE, PLANKS, IRON, DARK, WEED = range(len(MATS))

HALF = 1.5                      # the footprint's half-width: walls on the grid lines
T = 0.32                        # wall thickness, centred on the line
PLINTH = 0.3
STOREY = 2.6
BELFRY = 2.4
FLOOR1 = PLINTH + STOREY                    # the ringing chamber's floor
FLOOR2 = FLOOR1 + STOREY                    # the belfry's floor
EAVES = FLOOR2 + BELFRY
ROOF = 2.0
DOOR_W, DOOR_H = 1.0, 2.1


def face(d):
    """(along, out) unit axes for the wall on side d: 0 south (-Y), 1 east, 2 north, 3 west."""
    return [((1, 0), (0, -1)), ((0, 1), (1, 0)), ((-1, 0), (0, 1)), ((0, -1), (-1, 0))][d]


def on_face(d, u, w, z):
    """A point on wall d: u along the wall from its middle, w out from the wall's centre line."""
    (ax, ay), (ox, oy) = face(d)
    return (ax * u + ox * (HALF + w), ay * u + oy * (HALF + w), z)


def slab(bm, d, u0, u1, z0, z1, mat, w0=-T / 2, w1=T / 2):
    """A piece of wall d between u0 and u1 along it and z0 and z1 up it."""
    A.hull(bm, [on_face(d, u, w, z) for u in (u0, u1) for w in (w0, w1) for z in (z0, z1)], mat)


def arched_opening(bm, d, cx, half, sill, spring, top, u0, u1, mat=STONE):
    """Wall d from u0 to u1 between sill and top with a round-headed opening `half` wide either side
    of cx, its arch springing at `spring`."""
    slab(bm, d, u0, cx - half, sill, top, mat)
    slab(bm, d, cx + half, u1, sill, top, mat)
    pts = arch(cx, spring, half, n=6)
    for (ua, za), (ub, zb) in zip(pts, pts[1:]):
        A.hull(bm, [on_face(d, u, w, z) for u, z in ((ua, za), (ub, zb), (ua, top), (ub, top)) for w in (-T / 2, T / 2)], mat)


def belfry(bm, base):
    """The open belfry on a floor at `base`: two arched openings in the south and north faces, one
    in the east and west, the floor, the cornice, and the bell in its frame with its rope."""
    L = HALF + T / 2
    eaves = base + BELFRY
    spring = base + 1.2
    for d in (0, 2):
        slab(bm, d, -L, -1.0, base, eaves, STONE)
        slab(bm, d, 1.0, L, base, eaves, STONE)
        slab(bm, d, -0.18, 0.18, base, eaves, STONE)
        for cx in (-0.59, 0.59):
            pts = arch(cx, spring, 0.41, n=6)
            for (ua, za), (ub, zb) in zip(pts, pts[1:]):
                A.hull(bm, [on_face(d, u, w, z) for u, z in ((ua, za), (ub, zb), (ua, eaves), (ub, eaves)) for w in (-T / 2, T / 2)], STONE)
        slab(bm, d, -1.0, 1.0, base, base + 0.35, STONE)                     # the sill wall
    for d in (1, 3):
        arched_opening(bm, d, 0.0, 0.6, base + 0.35, spring, eaves, -L, L)
        slab(bm, d, -0.6, 0.6, base, base + 0.35, STONE)
    box(bm, -HALF + T / 2, -HALF + T / 2, base - 0.12, HALF - T / 2, HALF - T / 2, base, PLANKS)   # the belfry floor
    o = L + 0.08                                                      # the cornice: a ring on the wall tops
    for d in range(4):
        A.hull(bm, [on_face(d, u, w, z) for u in (-o, o) for w in (-T / 2, o - HALF) for z in (eaves - 0.02, eaves + 0.14)], GRANITE)
    # The bell in its frame: two trestles across the floor, a headstock, the bell hanging under it.
    hs = base + 1.75
    for x in (-0.75, 0.75):
        box(bm, x - 0.08, -0.7, base, x + 0.08, -0.55, hs, OAK)
        box(bm, x - 0.08, 0.55, base, x + 0.08, 0.7, hs, OAK)
        box(bm, x - 0.09, -0.75, hs, x + 0.09, 0.75, hs + 0.14, OAK)
    box(bm, -0.85, -0.09, hs + 0.14, 0.85, 0.09, hs + 0.3, TIMBER)                   # the headstock
    A.loft(bm, [((0, 0, hs + 0.14), 0.0, 0.0), ((0, 0, hs + 0.1), 0.2, 0.2), ((0, 0, hs - 0.2), 0.27, 0.27),
                ((0, 0, hs - 0.55), 0.36, 0.36), ((0, 0, hs - 0.75), 0.5, 0.5), ((0, 0, hs - 0.8), 0.48, 0.48)],
           sides=12, ref=(1, 0, 0), mats=BRONZE)
    A.loft(bm, [((0, 0, hs - 0.8), 0.48, 0.48), ((0, 0, hs - 0.6), 0.0, 0.0)], sides=12, ref=(1, 0, 0), mats=DARK)  # its mouth
    rod(bm, (0.95, 0, hs + 0.22), (1.15, 0, hs + 0.22), 0.04, TIMBER)
    rod(bm, (1.15, 0, hs + 0.22), (1.15, 0, base), 0.012, TIMBER, sides=4)                      # the rope down


def roof(bm, eaves, broken=False):
    """The low slate pyramid with a little overhang and the iron finial. broken: a quarter of it
    fallen in (the south-east), its rafters showing, the finial gone."""
    L = HALF + T / 2
    over = L + 0.25
    if broken:
        apex = (0, 0, eaves + 0.14 + ROOF)
        for quarter in ((-1, -1), (-1, 1), (1, 1)):                     # three quarters still standing
            qx, qy = quarter
            corners = ((qx * over, qy * over), (qx * over, 0), (0, qy * over))
            A.hull(bm, [(0, 0, eaves + 0.12)] + [(x, y, z) for x, y in corners for z in (eaves + 0.12, eaves + 0.24)] + [apex], SLATE)
        for k in range(3):                                              # bare rafters where the fourth fell in
            t = (k + 1) / 4
            rod(bm, (over * t, -over, eaves + 0.2), (0.0, 0.0, eaves + 0.1 + ROOF), 0.04, TIMBER, sides=4)
        return
    A.hull(bm, [(x * over, y * over, z) for x in (-1, 1) for y in (-1, 1) for z in (eaves + 0.12, eaves + 0.24)] +
           [(0, 0, eaves + 0.14 + ROOF)], SLATE)
    cyl(bm, 0, 0, eaves + 0.1 + ROOF, eaves + 0.9 + ROOF, 0.03, sides=6, mat=IRON)
    A.loft(bm, [((0, 0, eaves + 0.5 + ROOF), 0.0, 0.0), ((0, 0, eaves + 0.56 + ROOF), 0.07, 0.07),
                ((0, 0, eaves + 0.62 + ROOF), 0.0, 0.0)], sides=6, ref=(1, 0, 0), mats=IRON)


def bell_tower():
    bm = bmesh.new()
    # The plinth, battered, wider than the wall.
    A.hull(bm, [(x * (HALF + T / 2 + 0.22), y * (HALF + T / 2 + 0.22), 0.0) for x in (-1, 1) for y in (-1, 1)] +
           [(x * (HALF + T / 2 + 0.05), y * (HALF + T / 2 + 0.05), PLINTH) for x in (-1, 1) for y in (-1, 1)], GRANITE)
    L = HALF + T / 2                                                 # walls run corner to corner on the outside
    # The hall: a doorway in the south face, a round-headed opening with a plank door set back in it.
    arched_opening(bm, 0, 0.0, DOOR_W / 2, PLINTH, PLINTH + DOOR_H - DOOR_W / 2, FLOOR1, -L, L)
    for d in (1, 2, 3):
        slab(bm, d, -L, L, PLINTH, FLOOR1, STONE)
    box(bm, -DOOR_W / 2, -HALF + 0.02, PLINTH, DOOR_W / 2, -HALF + 0.07, PLINTH + DOOR_H - DOOR_W / 2, PLANKS)
    A.hull(bm, [(x, y, z) for x, z in arch(0.0, PLINTH + DOOR_H - DOOR_W / 2, DOOR_W / 2, n=6) for y in (-HALF + 0.02, -HALF + 0.07)], PLANKS)
    for z in (PLINTH + 0.45, PLINTH + 1.45):
        box(bm, -0.4, -HALF + 0.0, z, 0.4, -HALF + 0.02, z + 0.08, IRON)
    box(bm, -0.7, -HALF - T / 2 - 0.4, 0.0, 0.7, -HALF - T / 2 - 0.02, PLINTH, GRANITE)       # the step
    # The ringing chamber.
    for d in range(4):
        slab(bm, d, -L, L, FLOOR1, FLOOR2, STONE)
    # Slit windows, staggered: dark slots on the outer face, one storey and one face at a time.
    for d, z in ((1, PLINTH + 1.2), (3, PLINTH + 1.2), (0, FLOOR1 + 1.1), (2, FLOOR1 + 1.1), (1, FLOOR1 + 1.4)):
        A.hull(bm, [on_face(d, u, w, zz) for u in (-0.08, 0.08) for w in (T / 2 + 0.002, T / 2 + 0.004) for zz in (z, z + 0.7)], DARK)
    # String courses at each floor, standing proud.
    for z in (FLOOR1, FLOOR2):
        A.hull(bm, [(x * (L + 0.06), y * (L + 0.06), zz) for x in (-1, 1) for y in (-1, 1) for zz in (z - 0.06, z + 0.1)], GRANITE)
    belfry(bm, FLOOR2)
    roof(bm, EAVES)
    return make("BellTower", bm, MATS, "Landmarks")


def drowned_belfry():
    """The twin of Tolling's bell, in the belfry of a tower the lake rose over (Knellmere: The Bell's
    end). Only the top of the tower stands out of the water, origin at the water's surface: the
    ringing chamber's upper courses dark and green with weed at the waterline, the belfry and the
    bell still in it, the roof broken in on one side. The walls run on down out of sight."""
    bm = bmesh.new()
    L = HALF + T / 2
    base = 1.0                                                      # the belfry's floor above the water
    for d in range(4):
        slab(bm, d, -L, L, -2.5, base, STONE)
        slab(bm, d, -L - 0.01, L + 0.01, -0.25, 0.25, WEED, w0=T / 2, w1=T / 2 + 0.012)
    for d, z in ((0, -0.6), (2, -0.1)):
        A.hull(bm, [on_face(d, u, w, zz) for u in (-0.08, 0.08) for w in (T / 2 + 0.013, T / 2 + 0.015) for zz in (z, z + 0.6)], DARK)
    A.hull(bm, [(x * (L + 0.06), y * (L + 0.06), zz) for x in (-1, 1) for y in (-1, 1) for zz in (base - 0.06, base + 0.1)], GRANITE)
    belfry(bm, base)
    roof(bm, base + BELFRY, broken=True)
    return make("DrownedBelfry", bm, MATS, "Landmarks")


ASSETS = {
    "BellTower": bell_tower,
    "DrownedBelfry": drowned_belfry,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
