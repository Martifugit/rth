"""A modular house kit: one-tile pieces that snap to the tile grid, painted by palette name, so
one kit makes many kinds of house by recolouring (docs/assets.md 1.6-1.7).

Pieces (each in its own tile, Blender X along the wall, -Y the outside, Z up; 1 unit = 1 tile):
  Post          a timber post at a grid corner, where walls meet
  Wall          one tile of wall on a grid line: stone plinth, plaster panel, timber rails
  WallBraced    the same with a diagonal brace (variety along a run)
  WallWindow    a window: frame, sill, a dark pane, shutters
  WallDoorway   an opening a tile wide with a lintel
  Door          the door leaf for a doorway (its own piece: it opens)
  RoofEave      the first tile of roof from the wall out, with the overhang
  RoofSlope     one more tile of roof up the slope
  RoofRidge     the cap along the ridge
  Gable<k>      the wall above an end wall, k tiles in from the eave (k = 0, 1)

Materials are palette names: Stone, Plaster, Timber, Thatch, Planks, Iron, Sable (the dark of a
window). A kit for another kind of house is a recolour of these, not new pieces. The storey is
one number (decision D2 in docs/world-scale.md is still open)."""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

EXPORT = True
STOREY = 2.3                    # wall height (D2: 2.3 proposed, 1.7 today)
PITCH = 40.0                    # roof pitch, degrees
RISE = math.tan(math.radians(PITCH))   # the roof rises this much per tile of run
T = 0.22                        # wall thickness (centred on the grid line)
PLINTH = 0.35                   # stone base height
RAIL = 0.12                     # timber rail height
POST = 0.13                     # half the post's width
OVERHANG = 0.35                 # how far the eave reaches past the wall
SLAB = 0.09                     # roof thickness

MATS = ["Stone", "Plaster", "Timber", "Thatch", "Planks", "Iron", "Sable"]
STONE, PLASTER, TIMBER, THATCH, PLANKS, IRON, DARK = range(len(MATS))


def box(bm, x0, x1, y0, y1, z0, z1, mat):
    return A.box(bm, (x0, y0, z0), (x1, y1, z1), mat)


def quad_prism(bm, profile, y0, y1, mat):
    """A prism from a polygon in the x-z plane, from y0 to y1 (gables, sloped things)."""
    return A.hull(bm, [(x, y, z) for x, z in profile for y in (y0, y1)], mat)


# ---- walls ----

def wall_frame(bm, x0=0.0, x1=1.0, top=STOREY):
    """The parts every wall tile shares: the plinth along the bottom and the top rail."""
    box(bm, x0, x1, -T / 2 - 0.02, T / 2 + 0.02, 0, PLINTH, STONE)
    box(bm, x0, x1, -T / 2 - 0.015, T / 2 + 0.015, top - RAIL, top, TIMBER)
    box(bm, x0, x1, -T / 2 - 0.015, T / 2 + 0.015, PLINTH, PLINTH + RAIL * 0.7, TIMBER)


def panel(bm, x0, x1, z0, z1):
    box(bm, x0, x1, -T / 2, T / 2, z0, z1, PLASTER)


def piece_post():
    bm = bmesh.new()
    box(bm, -POST, POST, -POST, POST, 0, PLINTH + 0.02, STONE)
    box(bm, -POST * 0.85, POST * 0.85, -POST * 0.85, POST * 0.85, PLINTH, STOREY + 0.02, TIMBER)
    return bm


def piece_wall():
    bm = bmesh.new()
    wall_frame(bm)
    panel(bm, 0, 1, PLINTH + RAIL * 0.7, STOREY - RAIL)
    return bm


def piece_wall_braced():
    bm = bmesh.new()
    wall_frame(bm)
    panel(bm, 0, 1, PLINTH + RAIL * 0.7, STOREY - RAIL)
    z0, z1 = PLINTH + RAIL * 0.7, STOREY - RAIL
    for y in (-T / 2 - 0.015, T / 2 + 0.015):                  # a brace on each face
        d = 0.05
        A.hull(bm, [(0.1, y - 0.01, z0), (0.1 + d * 2, y - 0.01, z0), (0.9, y - 0.01, z1), (0.9 - d * 2, y - 0.01, z1),
                    (0.1, y + 0.01, z0), (0.1 + d * 2, y + 0.01, z0), (0.9, y + 0.01, z1), (0.9 - d * 2, y + 0.01, z1)],
               TIMBER)
    return bm


def piece_wall_window():
    bm = bmesh.new()
    wall_frame(bm)
    lo, hi = PLINTH + RAIL * 0.7, STOREY - RAIL
    w0, w1, s0, s1 = 0.3, 0.7, 1.0, 1.75                       # the opening
    panel(bm, 0, w0, lo, hi)
    panel(bm, w1, 1, lo, hi)
    panel(bm, w0, w1, lo, s0)
    panel(bm, w0, w1, s1, hi)
    box(bm, w0, w1, -0.02, 0.02, s0, s1, DARK)                  # the dark beyond the glass
    for x in (w0 - 0.04, w1):                                  # frame
        box(bm, x, x + 0.04, -T / 2 - 0.02, T / 2 + 0.02, s0, s1, TIMBER)
    box(bm, w0 - 0.06, w1 + 0.06, -T / 2 - 0.06, T / 2 + 0.02, s0 - 0.06, s0, TIMBER)   # sill, sticking out
    box(bm, w0 - 0.04, w1 + 0.04, -T / 2 - 0.02, T / 2 + 0.02, s1, s1 + 0.05, TIMBER)    # head
    for x0, x1 in ((w0 - 0.24, w0 - 0.05), (w1 + 0.05, w1 + 0.24)):                     # shutters, open
        box(bm, x0, x1, -T / 2 - 0.05, -T / 2 - 0.02, s0 + 0.02, s1 - 0.02, PLANKS)
    return bm


def piece_wall_doorway():
    bm = bmesh.new()
    box(bm, 0, 0.12, -T / 2 - 0.02, T / 2 + 0.02, 0, PLINTH, STONE)
    box(bm, 0.88, 1, -T / 2 - 0.02, T / 2 + 0.02, 0, PLINTH, STONE)
    d1 = 1.85                                                  # the door's height
    panel(bm, 0, 0.12, PLINTH, STOREY - RAIL)
    panel(bm, 0.88, 1, PLINTH, STOREY - RAIL)
    panel(bm, 0.12, 0.88, d1 + 0.1, STOREY - RAIL)
    box(bm, 0, 1, -T / 2 - 0.015, T / 2 + 0.015, STOREY - RAIL, STOREY, TIMBER)
    for x in (0.08, 0.84):                                     # jambs
        box(bm, x, x + 0.08, -T / 2 - 0.02, T / 2 + 0.02, 0, d1 + 0.1, TIMBER)
    box(bm, 0.04, 0.96, -T / 2 - 0.03, T / 2 + 0.03, d1, d1 + 0.12, TIMBER)                # lintel
    box(bm, 0.16, 0.84, -T / 2 - 0.02, T / 2 + 0.02, 0, 0.04, STONE)                     # threshold
    return bm


def piece_door():
    """The leaf: planks with two iron straps, hinged at x = 0.16 (its origin's x is the hinge)."""
    bm = bmesh.new()
    box(bm, 0.16, 0.84, -0.03, 0.03, 0.04, 1.85, PLANKS)
    for z in (0.45, 1.45):
        box(bm, 0.16, 0.74, -0.045, -0.03, z, z + 0.07, IRON)
    box(bm, 0.70, 0.75, -0.06, -0.03, 0.95, 1.02, IRON)        # handle
    return bm


# ---- roof ----

def roof_slab(bm, run0, run1, extra_x=0.0, mat=THATCH):
    """A strip of roof over x 0..1, from run0 to run1 tiles in from the wall line (y = 0 is the wall,
    +Y inward), sitting on the slope that starts at the storey line."""
    pts = []
    for r in (run0, run1):
        z = STOREY + RISE * r
        for x in (-extra_x, 1 + extra_x):
            pts += [(x, r, z), (x, r, z + SLAB)]
    return A.hull(bm, pts, mat)


def piece_roof_eave():
    bm = bmesh.new()
    roof_slab(bm, -OVERHANG, 1.0)
    # The eave's edge: a thicker lip of thatch along the bottom.
    z = STOREY - RISE * OVERHANG
    box(bm, 0, 1, -OVERHANG - 0.04, -OVERHANG + 0.12, z - 0.06, z + SLAB, THATCH)
    return bm


def piece_roof_slope():
    bm = bmesh.new()
    roof_slab(bm, 0.0, 1.0)
    return bm


def piece_roof_ridge():
    """The cap where two slopes meet: set at the ridge line (y = 0 here), the roof's top."""
    bm = bmesh.new()
    z = STOREY + SLAB
    A.hull(bm, [(x, y, zz) for x in (0, 1) for y, zz in ((-0.16, z - RISE * 0.16), (0.16, z - RISE * 0.16),
                                                        (-0.06, z + 0.08), (0.06, z + 0.08))], TIMBER)
    return bm


def piece_gable(k):
    """The end wall above the storey, k tiles in from the eave: a trapezoid of plaster with a
    timber edge, along x 0..1 (0 at the eave side), rising with the roof."""
    def make():
        bm = bmesh.new()
        z0, z1 = STOREY + RISE * k, STOREY + RISE * (k + 1)
        quad_prism(bm, [(0, STOREY), (1, STOREY), (1, z1 - 0.02), (0, z0 - 0.02)], -T / 2, T / 2, PLASTER)
        A.hull(bm, [(x, y, z) for x, z in ((0, z0 - 0.06), (1, z1 - 0.06), (1, z1 + 0.04), (0, z0 + 0.04))
                    for y in (-T / 2 - 0.03, T / 2 + 0.03)], TIMBER)
        return bm
    return make


PIECES = {
    "Post": piece_post, "Wall": piece_wall, "WallBraced": piece_wall_braced, "WallWindow": piece_wall_window,
    "WallDoorway": piece_wall_doorway, "Door": piece_door, "RoofEave": piece_roof_eave,
    "RoofSlope": piece_roof_slope, "RoofRidge": piece_roof_ridge, "Gable0": piece_gable(0), "Gable1": piece_gable(1),
}


def build_pieces():
    objs = {}
    for name, make in PIECES.items():
        bm = make()
        A.finish(bm)
        objs[name] = A.mesh_from("Kit_" + name, bm, MATS, coll="Kit")
    return objs


# ---- assembling a house (the preview; in the game a builder places the same pieces) ----

def place(kit, name, coll, x, y, rot=0.0, recolor=None, z=0.0):
    src = kit[name]
    o = bpy.data.objects.new(f"{coll.name}_{name}", src.data)
    o.location = (x, y, z)
    o.rotation_euler = (0, 0, math.radians(rot))
    coll.objects.link(o)
    if recolor:
        for slot in o.material_slots:
            slot.link = "OBJECT"
        for i, m in enumerate(src.data.materials):
            o.material_slots[i].material = A.mat(recolor.get(m.name, m.name))
    return o


def house(kit, coll_name, ox, oy, length, depth, door_at, windows, recolor=None):
    """A gabled house on tiles [ox, ox+length) x [oy, oy+depth), ridge along X (depth even).
    The front (door, windows) faces -Y."""
    coll = A.collection(coll_name)
    half = depth // 2
    # Walls: front (y = oy, outside -Y), back (y = oy + depth, outside +Y), ends.
    for i in range(length):
        front = "WallDoorway" if i == door_at else "WallWindow" if i in windows else ("WallBraced" if i % 2 else "Wall")
        place(kit, front, coll, ox + i, oy, 0, recolor)
        if i == door_at:
            place(kit, "Door", coll, ox + i, oy, 0, recolor)
        place(kit, "WallWindow" if i in windows else "Wall", coll, ox + i + 1, oy + depth, 180, recolor)
    for j in range(depth):
        place(kit, "WallBraced" if j % 2 else "Wall", coll, ox, oy + j + 1, 270, recolor)
        place(kit, "WallWindow" if j == half else "Wall", coll, ox + length, oy + j, 90, recolor)
    for cx in (ox, ox + length):
        for cy in (oy, oy + depth):
            place(kit, "Post", coll, cx, cy, 0, recolor)
    # Gables over both end walls, k tiles in from each eave: low edge at the eave side.
    for k in range(half):
        for ex in (ox, ox + length):
            place(kit, f"Gable{k}", coll, ex, oy + k, 90, recolor)               # rising from the front eave
            place(kit, f"Gable{k}", coll, ex, oy + depth - k, 270, recolor)      # rising from the back eave
    # Roof: eave, slopes up to the ridge, on both sides; a ridge cap.
    for i in range(length):
        x = ox + i
        place(kit, "RoofEave", coll, x, oy, 0, recolor)
        place(kit, "RoofEave", coll, x + 1, oy + depth, 180, recolor)
        for k in range(1, half):
            place(kit, "RoofSlope", coll, x, oy + k, 0, recolor, z=RISE * k)
            place(kit, "RoofSlope", coll, x + 1, oy + depth - k, 180, recolor, z=RISE * k)
        place(kit, "RoofRidge", coll, x, oy + half, 0, recolor, z=RISE * half)
    return coll


def build():
    A.reset()
    kit = build_pieces()
    result = {name: A.report([o])["triangles"] for name, o in kit.items()}
    if EXPORT:
        for name, o in kit.items():
            A.export([o], f"Art/Buildings/HouseKit/{name}.fbx")
    # The pieces in a row, then a cottage, then the same pieces repainted as a stone house.
    for i, o in enumerate(kit.values()):
        o.location = (-2.0 + i * 1.4, -6.0, 0)
    house(kit, "Cottage", 0, 0, 5, 4, door_at=2, windows={1, 3})
    house(kit, "StoneHouse", 7, 0, 4, 4, door_at=1, windows={2},
          recolor={"Plaster": "Granite", "Timber": "DarkRock", "Thatch": "Slate", "Stone": "CliffRock"})
    return kit, result


kit, result = build()
cam = A.preview_rig(focus=(5.5, 2.0, 1.5), distance=16, pitch=35, yaw=-30, ground=12, human_at=(4.0, -1.5))
