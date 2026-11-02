"""Stations, village. Where the village's recipes happen: the smith's forge and anvil, the furnace, the
cooking range, the charcoal kiln, the apothecary's bench and still, the skinning yard's rack and lye vat,
the spinning wheel; the tanner's pit, the loom and the workbench (Tailoring and Artifice).

Each faces Blender -Y (its working side), its origin the middle of its footprint, which is given in
its docstring (1 x 1, 2 x 1 along X, 2 x 2). Masonry textures (Stone's Fieldstone) are for masonry
only: small carved things are plain smooth stone (Gravestone). Built to the art rules (tools/art/blender/README.md):
the silhouette says what it is from the game's camera; detail is painted or a few clear parts;
muted brick, iron, clay and timber, the fire the one bright thing.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Stations/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\stations\\village.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import math
import os
import random
import sys

import bmesh
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402


def make(name, bm, materials, at=(0, 0, 0), smooth=None):
    A.finish(bm)
    ob = A.mesh_from(name, bm, materials, coll="Stations", smooth_angle=smooth)
    ob.location = at
    return ob


def box(bm, x0, y0, z0, x1, y1, z1, mat):
    A.box(bm, (min(x0, x1), min(y0, y1), min(z0, z1)), (max(x0, x1), max(y0, y1), max(z0, z1)), mat)


def cyl(bm, x, y, z0, z1, r0, r1=None, sides=8, mat=0):
    """An upright round part (a post, a pot, a body), r0 at the foot and r1 at the top."""
    r1 = r0 if r1 is None else r1
    A.loft(bm, [((x, y, z0), r0, r0), ((x, y, z1), r1, r1)], sides=sides, ref=(1, 0, 0), mats=mat)


def rod(bm, a, b, r, mat, sides=6):
    """A straight round bar from a to b."""
    d = Vector(b) - Vector(a)
    ref = (0, 0, 1) if abs(d.normalized().z) < 0.9 else (1, 0, 0)
    A.loft(bm, [(tuple(a), r, r), (tuple(b), r, r)], sides=sides, ref=ref, mats=mat)


def coals(bm, cx, cy, z, rx, ry, rnd, coal, ember, n=14):
    """A bed of coals: low lumps over an ember glow."""
    A.hull(bm, [(cx + rx * math.cos(a) * 0.95, cy + ry * math.sin(a) * 0.95, z) for a in
                (2 * math.pi * k / 10 for k in range(10))] +
           [(cx + rx * 0.5 * math.cos(a), cy + ry * 0.5 * math.sin(a), z + 0.04) for a in
            (2 * math.pi * k / 6 for k in range(6))], ember)
    for _ in range(n):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.15, 0.95)
        x, y = cx + rx * d * math.cos(a), cy + ry * d * math.sin(a)
        s = rnd.uniform(0.035, 0.06)
        A.hull(bm, [(x + rnd.uniform(-s, s), y + rnd.uniform(-s, s), z + rnd.uniform(0.0, 0.05)) for _ in range(7)], coal)


def bellows(bm, x, y, z, length, width, toward, board, leather, iron, lift=0.12):
    """A leather bellows lying on its back: two pear-shaped boards, the lower flat and the upper
    lifted at the back, leather pleated between them, an iron nozzle at the front. `toward` is the
    direction its nozzle points (a unit vector in the ground plane)."""
    t = Vector((toward[0], toward[1], 0)).normalized()
    s = Vector((-t.y, t.x, 0))
    o = Vector((x, y, z))

    def p(u, v, h):
        return tuple(o + t * u + s * v + Vector((0, 0, h)))
    outline = [(-length / 2, -width * 0.5), (-length / 2 + 0.1, -width * 0.55), (length * 0.2, -width * 0.38),
               (length / 2, -width * 0.12), (length / 2, width * 0.12), (length * 0.2, width * 0.38),
               (-length / 2 + 0.1, width * 0.55), (-length / 2, width * 0.5)]
    A.hull(bm, [p(u, v, h) for u, v in outline for h in (0.0, 0.03)], board)                    # the lower board
    A.hull(bm, [p(u, v, 0.03 + lift * (0.5 - u / length)) for u, v in outline] +
           [p(u, v, 0.06 + lift * (0.5 - u / length)) for u, v in outline], board)              # the upper, lifted at the back
    inner = [(u * 0.97, v * 0.92) for u, v in outline]
    A.hull(bm, [p(u, v, 0.03) for u, v in inner] + [p(u, v, 0.03 + lift * (0.5 - u / length) * 0.98) for u, v in inner], leather)
    rod(bm, p(length / 2 - 0.02, 0, 0.03), p(length / 2 + 0.22, 0, 0.0), 0.025, iron)            # the nozzle
    rod(bm, p(-length / 2 - 0.15, 0, 0.06 + lift), p(-length / 2 + 0.05, 0, 0.06 + lift * 0.9), 0.02, board)   # the handle


# ---- the forge -------------------------------------------------------------------------------------

def forge(seed=1, at=(0, 0, 0)):
    """The smith's forge. 2 x 2.

    A working smith's hearth, as a village forge is laid out:
    - the hearth: masonry at working height, a firebrick-lined fire pot in its top with the coals
      glowing, a granite rim to rest work on;
    - behind it a back wall carrying a heavy hood over the fire, narrowing into the chimney;
    - the great bellows behind the back wall, lying on a frame, its nozzle through the wall into the
      fire; over them the rocker: a pole pivoting on a post, an iron rod from its back end to the
      bellows' top board, its front end over the hearth's right side with a handle at the smith's
      hand;
    - at the front corner the slack tub of water for quenching, beside it the coal bin;
    - tongs hanging from a rail along the hearth's front.
    The anvil is its own piece, set in front."""
    rnd = random.Random(seed)
    mats = ["Stone", "Granite", "Brick", "Coal", "Ember", "Iron", "Timber", "Hide", "Boards", "Mere"]
    STONE, RIM, BRICK, COAL, EMBER, IRON, TIMBER, HIDE, BOARDS, WATER = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.95, 0.45, -0.9, 0.0, 0.75             # the hearth
    box(bm, x0, y0, 0, x1, y1, H - 0.08, STONE)
    for (a, b, c, d) in ((x0 - 0.03, y0 - 0.03, x1 + 0.03, y0 + 0.14), (x0 - 0.03, y1 - 0.14, x1 + 0.03, y1 + 0.03),
                         (x0 - 0.03, y0 + 0.14, x0 + 0.14, y1 - 0.14), (x1 - 0.14, y0 + 0.14, x1 + 0.03, y1 - 0.14)):
        box(bm, a, b, H - 0.08, c, d, H + 0.04, RIM)
    fx0, fx1, fy0, fy1 = x0 + 0.35, x1 - 0.35, y0 + 0.22, y1 - 0.2   # the fire pot, lined with firebrick
    box(bm, x0 + 0.14, y0 + 0.14, H - 0.08, x1 - 0.14, y1 - 0.14, H - 0.02, BRICK)
    box(bm, fx0, fy0, H - 0.04, fx1, fy1, H - 0.015, COAL)
    coals(bm, (fx0 + fx1) / 2, (fy0 + fy1) / 2, H - 0.015, (fx1 - fx0) / 2, (fy1 - fy0) / 2, rnd, COAL, EMBER)
    # the back wall, the hood on its cheeks, the chimney
    by0, by1 = 0.0, 0.3
    box(bm, x0, by0, 0, x1, by1, 1.65, STONE)
    for x in (x0, x1 - 0.16):
        box(bm, x, y0 + 0.02, H + 0.04, x + 0.16, y0 + 0.2, 1.6, STONE)
    rod(bm, (x0, y0 + 0.12, 1.55), (x1, y0 + 0.12, 1.55), 0.035, IRON)
    cx0, cx1 = -0.5, -0.0                                          # the chimney's footprint
    A.hull(bm, [(x, y, 1.6) for x in (x0 - 0.04, x1 + 0.04) for y in (y0 - 0.02, by1)] +
           [(x, y, 2.3) for x in (cx0, cx1) for y in (-0.3, by1)], STONE)
    box(bm, cx0, -0.3, 2.3, cx1, by1, 3.05, STONE)
    box(bm, cx0 - 0.05, -0.35, 3.05, cx1 + 0.05, by1 + 0.05, 3.13, RIM)
    # the great bellows behind the wall, on a frame, the nozzle through the wall into the fire
    for x in (-0.65, 0.15):
        for y in (0.45, 0.9):
            box(bm, x - 0.04, y - 0.04, 0, x + 0.04, y + 0.04, 0.62, TIMBER)
    box(bm, -0.72, 0.38, 0.6, 0.22, 0.97, 0.66, TIMBER)
    bellows(bm, -0.25, 0.68, 0.66, 0.66, 0.66, (0, -1), BOARDS, HIDE, IRON, lift=0.2)
    # the rocker: a post behind, a pole pivoting on it, an iron rod to the bellows, a handle at the front
    box(bm, 0.55, 0.75, 0, 0.65, 0.85, 1.75, TIMBER)
    rod(bm, (0.6, 1.0, 1.55), (0.6, -0.75, 1.3), 0.035, TIMBER)    # the pole
    rod(bm, (0.6, 0.95, 1.54), (-0.25, 0.95, 1.12), 0.012, IRON, sides=4)
    rod(bm, (-0.25, 0.95, 1.12), (-0.25, 0.95, 0.88), 0.012, IRON, sides=4)
    rod(bm, (0.6, -0.75, 1.3), (0.6, -0.75, 1.05), 0.012, IRON, sides=4)  # the handle's chain
    rod(bm, (0.5, -0.75, 1.03), (0.7, -0.75, 1.03), 0.025, TIMBER, sides=5)
    # the slack tub (a half barrel of water) and the coal bin
    A.loft(bm, [((0.72, -0.62, 0), 0.24, 0.24), ((0.72, -0.62, 0.5), 0.27, 0.27)], sides=10, ref=(1, 0, 0), mats=BOARDS)
    for z in (0.08, 0.4):
        A.loft(bm, [((0.72, -0.62, z), 0.25 + z * 0.06, 0.25 + z * 0.06), ((0.72, -0.62, z + 0.04), 0.255 + z * 0.06, 0.255 + z * 0.06)],
               sides=10, ref=(1, 0, 0), mats=IRON)
    A.loft(bm, [((0.72, -0.62, 0.5), 0.235, 0.235), ((0.72, -0.62, 0.503), 0.235, 0.235)], sides=10, ref=(1, 0, 0), mats=WATER)   # the water, inside the staves' ring
    box(bm, 0.5, -0.25, 0, 0.96, 0.25, 0.35, BOARDS)              # the coal bin, heaped
    A.hull(bm, [(x, y, 0.35) for x in (0.53, 0.93) for y in (-0.22, 0.22)] + [(0.73, 0.0, 0.48), (0.65, 0.08, 0.45)], COAL)
    # tongs hanging on a rail along the hearth's front
    rod(bm, (x0 + 0.1, y0 - 0.06, 0.62), (x1 - 0.1, y0 - 0.06, 0.62), 0.012, IRON, sides=4)
    for x in (-0.6, -0.2):
        for dx in (-0.03, 0.03):
            rod(bm, (x, y0 - 0.06, 0.64), (x + dx * 2.5, y0 - 0.09, 0.15), 0.01, IRON, sides=4)
    return make("Forge", bm, mats, at=at)


def anvil(seed=1, at=(0, 0, 0)):
    """A smith's anvil on its block. 1 x 1.

    A section of tree trunk, bark on, an iron band round its top; on it the anvil: a waisted foot,
    a flat face at hip height, a heel at one end and a horn tapering to a point at the other, the
    whole thing longer than it is wide. A hammer rests across the face."""
    mats = ["Bark", "Timber", "Blackiron", "Iron"]
    BARK, END, DARK, IRON = range(len(mats))
    bm = bmesh.new()
    A.loft(bm, [((0, 0, 0), 0.27, 0.27), ((0, 0, 0.46), 0.25, 0.25)], sides=9, ref=(1, 0, 0), mats=BARK)
    A.loft(bm, [((0, 0, 0.46), 0.25, 0.25), ((0, 0, 0.48), 0.0, 0.0)], sides=9, ref=(1, 0, 0), mats=END)
    A.loft(bm, [((0, 0, 0.38), 0.262, 0.262), ((0, 0, 0.43), 0.262, 0.262)], sides=9, ref=(1, 0, 0), mats=IRON)
    # the anvil, along X: foot, waist, body, heel and horn
    A.hull(bm, [(x, y, 0.48) for x in (-0.22, 0.2) for y in (-0.15, 0.15)] +
           [(x, y, 0.56) for x in (-0.13, 0.12) for y in (-0.08, 0.08)], DARK)
    A.hull(bm, [(x, y, 0.56) for x in (-0.13, 0.12) for y in (-0.08, 0.08)] +
           [(x, y, 0.66) for x in (-0.18, 0.2) for y in (-0.1, 0.1)], DARK)
    A.hull(bm, [(x, y, z) for x in (-0.26, 0.26) for y in (-0.1, 0.1) for z in (0.66, 0.78)], DARK)
    A.loft(bm, [((-0.26, 0, 0.72), 0.1, 0.06), ((-0.42, 0, 0.75), 0.05, 0.035), ((-0.56, 0, 0.77), 0.0, 0.0)],
           sides=6, ref=(0, 0, 1), mats=DARK)                                                   # the horn
    box(bm, -0.26, -0.1, 0.779, 0.26, 0.1, 0.785, IRON)                                         # the bright face
    rod(bm, (0.05, -0.18, 0.79), (0.05, 0.12, 0.81), 0.015, END, sides=5)                       # the hammer's haft
    box(bm, 0.0, 0.1, 0.78, 0.1, 0.16, 0.85, DARK)                                              # and its head
    return make("Anvil", bm, mats, at=at)


# ---- the furnace -----------------------------------------------------------------------------------

def furnace(seed=2, at=(0, 0, 0)):
    """A squat clay furnace for smelting. 1 x 1.

    A round clay body on a ring of fieldstones, a little wider at the belly, drawn in to a
    smoke-blackened throat at shoulder height; two iron hoops binding it; the stoke hole at the
    front glowing; a tapping spout at the foot over a scatter of slag; a small bellows at its side,
    its nozzle in the tuyere."""
    rnd = random.Random(seed)
    mats = ["RedRock", "Scorch", "Stone", "Iron", "Ember", "Cinder", "Timber", "Hide"]
    CLAY, SOOT, STONE, IRON, EMBER, SLAG, TIMBER, HIDE = range(len(mats))
    bm = bmesh.new()
    for k in range(9):                                            # the footing stones
        a = 2 * math.pi * k / 9 + rnd.uniform(-0.1, 0.1)
        x, y = 0.38 * math.cos(a), 0.38 * math.sin(a)
        A.hull(bm, [(x + rnd.uniform(-0.1, 0.1), y + rnd.uniform(-0.1, 0.1), rnd.uniform(0.0, 0.14)) for _ in range(8)], STONE)
    A.loft(bm, [((0, 0, 0.08), 0.36, 0.36), ((0, 0, 0.45), 0.4, 0.4), ((0, 0, 0.95), 0.33, 0.33),
                ((0, 0, 1.3), 0.22, 0.22)], sides=12, ref=(1, 0, 0), mats=CLAY)
    A.loft(bm, [((0, 0, 1.3), 0.22, 0.22), ((0, 0, 1.42), 0.19, 0.19)], sides=12, ref=(1, 0, 0), mats=SOOT)
    A.loft(bm, [((0, 0, 1.42), 0.19, 0.19), ((0, 0, 1.38), 0.12, 0.12)], sides=12, ref=(1, 0, 0), mats=SOOT)
    for z, r in ((0.42, 0.405), (0.92, 0.338)):                   # the hoops
        A.loft(bm, [((0, 0, z), r, r), ((0, 0, z + 0.04), r, r)], sides=12, ref=(1, 0, 0), mats=IRON)
    # the stoke hole, arched, glowing: a dark mouth set into the front with the fire in it
    pts = [(-0.13, -0.39, 0.5), (0.13, -0.39, 0.5)] + [(0.13 * math.cos(t), -0.37, 0.66 + 0.13 * math.sin(t)) for t in
                                                     (math.pi * k / 6 for k in range(7))]
    A.hull(bm, pts + [(x, y + 0.04, z) for x, y, z in pts], SOOT)
    A.hull(bm, [(x * 0.75, y - 0.005, 0.52 + (z - 0.5) * 0.7) for x, y, z in pts] +
           [(x * 0.75, y + 0.01, 0.52 + (z - 0.5) * 0.7) for x, y, z in pts], EMBER)
    # the tapping spout and the slag below it
    A.hull(bm, [(-0.06, -0.36, 0.14), (0.06, -0.36, 0.14), (-0.06, -0.36, 0.22), (0.06, -0.36, 0.22),
                (-0.04, -0.55, 0.1), (0.04, -0.55, 0.1), (-0.04, -0.55, 0.15), (0.04, -0.55, 0.15)], CLAY)
    for _ in range(6):
        x, y, s = rnd.uniform(-0.25, 0.25), rnd.uniform(-0.75, -0.55), rnd.uniform(0.04, 0.07)
        A.hull(bm, [(x + rnd.uniform(-s, s), y + rnd.uniform(-s, s), rnd.uniform(0.0, s)) for _ in range(7)], SLAG)
    # the bellows at its foot, on the left, nozzle into the tuyere
    bellows(bm, -0.62, 0.0, 0.02, 0.5, 0.34, (1, 0), TIMBER, HIDE, IRON, lift=0.12)
    return make("Furnace", bm, mats, at=at, smooth=35)


# ---- the range -------------------------------------------------------------------------------------

def cooking_range(seed=3, at=(0, 0, 0)):
    """An iron cooking range. 1 x 1.

    An iron box on four short feet standing on a stone hearthstone: the firebox on the left with a
    barred door and the fire behind it, the oven on the right with a latched door, a hot plate on top
    with two round lids and a kettle, the flue pipe rising from the back right corner to above head
    height, a damper ring on it."""
    mats = ["Blackiron", "Iron", "Ember", "Granite", "Copper"]
    DARK, IRON, EMBER, STONE, COPPER = range(len(mats))
    bm = bmesh.new()
    box(bm, -0.5, -0.4, 0, 0.5, 0.42, 0.06, STONE)                # the hearthstone
    for x in (-0.4, 0.38):
        for y in (-0.2, 0.25):
            box(bm, x - 0.03, y - 0.03, 0.06, x + 0.03, y + 0.03, 0.16, DARK)
    box(bm, -0.45, -0.28, 0.16, 0.45, 0.32, 0.78, DARK)           # the body
    box(bm, -0.47, -0.3, 0.76, 0.47, 0.34, 0.8, IRON)             # the hot plate's rim
    for x in (-0.22, 0.18):                                       # the lids
        A.loft(bm, [((x, 0.02, 0.8), 0.12, 0.12), ((x, 0.02, 0.81), 0.11, 0.11)], sides=10, ref=(1, 0, 0), mats=IRON)
    box(bm, -0.42, -0.31, 0.3, -0.06, -0.28, 0.62, IRON)          # the firebox door
    for k in range(4):                                            # its bars, the fire between them
        x = -0.38 + k * 0.09
        box(bm, x, -0.315, 0.36, x + 0.05, -0.305, 0.56, EMBER)
    box(bm, 0.02, -0.31, 0.24, 0.42, -0.28, 0.66, IRON)           # the oven door
    rod(bm, (0.08, -0.34, 0.6), (0.36, -0.34, 0.6), 0.012, IRON, sides=5)   # its handle
    box(bm, 0.07, -0.335, 0.58, 0.09, -0.31, 0.62, IRON)
    box(bm, 0.35, -0.335, 0.58, 0.37, -0.31, 0.62, IRON)
    cyl(bm, 0.3, 0.22, 0.8, 2.25, 0.07, sides=8, mat=DARK)        # the flue
    cyl(bm, 0.3, 0.22, 1.5, 1.56, 0.085, sides=8, mat=IRON)       # the damper ring
    cyl(bm, 0.3, 0.22, 2.25, 2.3, 0.1, sides=8, mat=IRON)         # a cap
    # a copper kettle on the left lid: body, lid knob, spout, handle
    A.loft(bm, [((-0.22, 0.02, 0.81), 0.09, 0.09), ((-0.22, 0.02, 0.9), 0.1, 0.1), ((-0.22, 0.02, 0.98), 0.06, 0.06),
                ((-0.22, 0.02, 1.0), 0.0, 0.0)], sides=8, ref=(1, 0, 0), mats=COPPER)
    rod(bm, (-0.14, 0.02, 0.88), (-0.06, 0.02, 0.96), 0.015, COPPER, sides=5)
    rod(bm, (-0.29, 0.02, 0.98), (-0.15, 0.02, 0.98), 0.01, IRON, sides=4)
    return make("Range", bm, mats, at=at)


# ---- the charcoal kiln -----------------------------------------------------------------------------

def charcoal_kiln(seed=4, at=(0, 0, 0)):
    """A charcoal burner's kiln (a clamp). 2 x 2.

    Logs stood on end round a central flue, then covered: sods laid grass-side down and earth and ash
    over them, so the mound is dark, bare and dusty rather than green; a little lopsided, taller than
    a dome, flattened at the top round the flue's mouth. Rows of small vent holes round it, a few
    glowing, scorched above. The burner's things: a short ladder leaning on it to tend the top, a
    woven screen on stakes against the wind, a shovel, and split logs stacked ready."""
    rnd = random.Random(seed)
    mats = ["Peat", "Cinder", "Charcoal", "Ember", "Bark", "Timber"]
    EARTH, ASH, BLACK, EMBER, BARK, TIMBER = range(len(mats))
    bm = bmesh.new()
    profile = ((0.0, 0.98), (0.25, 0.94), (0.6, 0.78), (0.92, 0.55), (1.15, 0.3), (1.27, 0.15))
    rings = []
    n = 16
    for k, (z, r) in enumerate(profile):
        ring = []
        for j in range(n):
            a = 2 * math.pi * (j + 0.5 * (k % 2)) / n
            rr = r * rnd.uniform(0.95, 1.04) * (1.0 + 0.06 * math.cos(a - 0.6))
            ring.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), z + (rnd.uniform(-0.02, 0.03) if k else 0))))
        rings.append(ring)
    vents = []
    for k, (ra, rb) in enumerate(zip(rings, rings[1:])):
        for j in range(n):
            f = bm.faces.new((ra[j], ra[(j + 1) % n], rb[(j + 1) % n], rb[j]))
            # earth below, ashier towards the top
            f.material_index = ASH if k >= 3 else EARTH
            if k in (0, 2) and j % 3 == (k % 3):
                vents.append((f, EMBER if rnd.random() < 0.35 else BLACK))
    for f, mat in vents:                                          # vents: small dark holes laid on the face
        c, nrm = f.calc_center_median(), f.normal.copy()
        side = Vector((-nrm.y, nrm.x, 0)).normalized()
        up = nrm.cross(side).normalized()
        pts = [c + nrm * 0.006 + side * 0.055 * math.cos(t) + up * 0.045 * math.sin(t) for t in
               (2 * math.pi * q / 6 for q in range(6))]
        A.hull(bm, [tuple(p) for p in pts] + [tuple(p - nrm * 0.004) for p in pts], mat)
        if mat == EMBER:                                          # scorched above a glowing vent
            pts = [c + nrm * 0.004 + up * (0.06 + 0.12 * h) + side * 0.05 * (1 - h) * sx for h in (0.0, 1.0) for sx in (-1, 1)]
            A.hull(bm, [tuple(p) for p in pts] + [tuple(p - nrm * 0.003) for p in pts], BLACK)
    # the flue's mouth at the top: a ring of ash round a dark hole
    top = rings[-1]
    hole = [bm.verts.new((0.13 * math.cos(2 * math.pi * (j + 0.5 * ((len(profile) - 1) % 2)) / n),
                          0.13 * math.sin(2 * math.pi * (j + 0.5 * ((len(profile) - 1) % 2)) / n), 1.24)) for j in range(n)]
    for j in range(n):
        bm.faces.new((top[j], top[(j + 1) % n], hole[(j + 1) % n], hole[j])).material_index = ASH
    bm.faces.new(list(reversed(hole))).material_index = BLACK
    # a short ladder leaning on its side to reach the top
    for dx in (-0.2, 0.2):
        rod(bm, (0.2 + dx, -1.35, 0.0), (0.2 + dx, -0.62, 1.05), 0.025, TIMBER, sides=4)
    for k in range(1, 5):
        t = k / 5
        box(bm, 0.0, -1.35 + 0.73 * t - 0.02, 1.05 * t - 0.02, 0.4, -1.35 + 0.73 * t + 0.02, 1.05 * t + 0.02, TIMBER)
    # a shovel standing in the ground, split logs stacked ready
    rod(bm, (1.1, -0.9, 0.12), (1.0, -0.95, 1.1), 0.018, TIMBER, sides=5)
    A.hull(bm, [(1.04, -0.97, 0.0), (1.17, -0.85, 0.0), (1.06, -0.95, 0.24), (1.18, -0.84, 0.22), (1.12, -0.9, 0.0)], BLACK)
    for row, (z, n_) in enumerate(((0.0, 4), (0.17, 3), (0.34, 2))):
        for j in range(n_):
            y = 0.25 + 0.09 * row + j * 0.18
            A.loft(bm, [((1.05, y, z + 0.09), 0.085, 0.085), ((1.6, y, z + 0.09), 0.085, 0.085)], sides=8, ref=(0, 0, 1), mats=[BARK, TIMBER])
    ob = make("CharcoalKiln", bm, mats, at=at, smooth=50)
    # a woven screen against the wind on the west side: two of the village's hurdles, stakes and all
    from parts.fences.village import hurdle
    screen = []
    for k, y in enumerate((-1.0, 0.0)):
        h = hurdle(seed=k + 3, end_post=(k == 1))
        h.rotation_euler.z = math.pi / 2
        h.location = (at[0] - 1.35, at[1] + y, at[2])
        screen.append(h)
    return A.join(ob, screen)


# ---- the apothecary's bench ------------------------------------------------------------------------

def apothecary_bench(seed=5, at=(0, 0, 0)):
    """A scarred apothecary's bench. 2 x 1 (along X).

    A heavy table at standing height with a shelf below; on it a stone mortar with its pestle, a
    rack of stoppered vials, the ledger of what not to mix lying open, three jars and a bunch of
    herbs drying at one end."""
    rnd = random.Random(seed)
    mats = ["Oak", "Timber", "Gravestone", "Glaze", "Linen", "Umber", "Leaves", "Ochre"]   # a smooth stone mortar
    OAK, TIMBER, STONE, GLASS, PAGE, COVER, HERB, CORK = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.9, 0.9, -0.36, 0.36, 0.9
    box(bm, x0, y0, H - 0.07, x1, y1, H, OAK)                     # the top
    for x in (x0 + 0.06, x1 - 0.06):
        for y in (y0 + 0.06, y1 - 0.06):
            box(bm, x - 0.05, y - 0.05, 0, x + 0.05, y + 0.05, H - 0.07, TIMBER)
    box(bm, x0 + 0.06, y0 + 0.04, 0.2, x1 - 0.06, y1 - 0.04, 0.24, TIMBER)    # the shelf below
    # the mortar and pestle
    A.loft(bm, [((-0.55, 0.0, H), 0.11, 0.11), ((-0.55, 0.0, H + 0.1), 0.13, 0.13), ((-0.55, 0.0, H + 0.095), 0.09, 0.09),
                ((-0.55, 0.0, H + 0.04), 0.06, 0.06)], sides=10, ref=(1, 0, 0), mats=STONE)
    rod(bm, (-0.55, 0.0, H + 0.05), (-0.48, 0.07, H + 0.22), 0.022, STONE, sides=6)
    # the vial rack: a little shelf with five stoppered vials
    box(bm, -0.25, 0.14, H, 0.15, 0.28, H + 0.03, TIMBER)
    box(bm, -0.25, 0.14, H + 0.1, 0.15, 0.28, H + 0.12, TIMBER)
    for k in range(5):
        x = -0.21 + k * 0.08
        cyl(bm, x, 0.21, H + 0.03, H + 0.17, 0.025, sides=6, mat=GLASS)
        cyl(bm, x, 0.21, H + 0.17, H + 0.2, 0.018, sides=6, mat=CORK)
    # the ledger, open, its pages lifted a little at the spine
    lx, ly = 0.42, -0.08
    box(bm, lx - 0.22, ly - 0.15, H, lx + 0.22, ly + 0.15, H + 0.015, COVER)
    for s in (-1, 1):
        A.hull(bm, [(lx + s * 0.01, ly - 0.14, H + 0.03), (lx + s * 0.2, ly - 0.14, H + 0.02),
                    (lx + s * 0.01, ly + 0.14, H + 0.03), (lx + s * 0.2, ly + 0.14, H + 0.02),
                    (lx + s * 0.01, ly - 0.14, H + 0.015), (lx + s * 0.2, ly + 0.14, H + 0.015)], PAGE)
    # three jars
    for x, y, h in ((0.72, 0.22, 0.16), (0.58, 0.24, 0.12), (0.12, -0.2, 0.14)):
        cyl(bm, x, y, H, H + h, 0.055, 0.05, sides=8, mat=GLASS)
        cyl(bm, x, y, H + h, H + h + 0.025, 0.04, sides=8, mat=CORK)
    # herbs drying in a bunch at the left end, tied stems up
    for _ in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        tip = (x0 + 0.18 + 0.06 * math.cos(a), -0.2 + 0.06 * math.sin(a), H + 0.01)
        A.hull(bm, [(x0 + 0.06, -0.2, H + 0.03), (x0 + 0.06, -0.21, H + 0.04)] +
               [(tip[0] + rnd.uniform(-0.04, 0.04), tip[1] + rnd.uniform(-0.04, 0.04), tip[2] + rnd.uniform(0, 0.04)) for _ in range(3)], HERB)
    box(bm, x0 + 0.03, -0.23, H, x0 + 0.08, -0.17, H + 0.05, CORK)                                # the tie
    return make("ApothecaryBench", bm, mats, at=at)


# ---- the skinning yard -----------------------------------------------------------------------------

def skinning_rack(seed=6, at=(0, 0, 0)):
    """Wenna's skinning rack, where a carcass is butchered. 2 x 1 (along X).

    A gallows of two stout posts and a crossbeam a little over head height, braced at the corners,
    three iron hooks hanging from it, a fresh hide on one and a carcass's dark shape on another;
    under it a slab of trestle for the knife work, scarred and stained, a bucket to catch what drips
    and a fleshing beam leaning on the post for scraping hides."""
    rnd = random.Random(seed)
    mats = ["Timber", "Bark", "Iron", "Hide", "BloodDeep", "Planks", "Ash"]
    TIMBER, BARK, IRON, HIDE, GORE, BOARDS, SCRAPED = range(len(mats))
    bm = bmesh.new()
    x0, x1, H = -0.85, 0.85, 2.2
    for x in (x0, x1):
        A.loft(bm, [((x, 0.25, -0.05), 0.08, 0.08), ((x, 0.25, H + 0.1), 0.07, 0.07)], sides=6, ref=(1, 0, 0), mats=BARK)
        rod(bm, (x, 0.25, H - 0.45), (x * 0.65, 0.25, H - 0.04), 0.035, BARK, sides=5)                # the braces
    rod(bm, (x0 - 0.15, 0.25, H), (x1 + 0.15, 0.25, H), 0.065, TIMBER, sides=6)
    for x, what in ((-0.45, "hide"), (0.0, None), (0.45, "carcass")):
        rod(bm, (x, 0.25, H - 0.06), (x, 0.25, H - 0.32), 0.012, IRON, sides=4)
        rod(bm, (x, 0.25, H - 0.32), (x + 0.06, 0.25, H - 0.4), 0.01, IRON, sides=4)
        if what == "hide":                                                                         # a hide hung by its neck
            A.hull(bm, [(x + dx, 0.25 + t, z) for dx, z in ((-0.12, H - 0.38), (0.12, H - 0.38), (0.2, H - 0.75),
                                                              (0.14, H - 1.15), (-0.14, H - 1.15), (-0.2, H - 0.75)) for t in (-0.01, 0.01)], HIDE)
        elif what:                                                                                 # a carcass, head down
            A.loft(bm, [((x, 0.25, H - 0.38), 0.05, 0.05), ((x, 0.25, H - 0.5), 0.13, 0.11), ((x, 0.25, H - 0.85), 0.15, 0.13),
                        ((x, 0.25, H - 1.05), 0.07, 0.06), ((x, 0.25, H - 1.15), 0.0, 0.0)], sides=6, ref=(1, 0, 0), mats=GORE)
    box(bm, -0.6, -0.55, 0.78, 0.6, -0.15, 0.85, BOARDS)                                           # the trestle slab
    for x in (-0.5, 0.5):
        for s in (-1, 1):
            rod(bm, (x + s * 0.12, -0.35, 0.0), (x, -0.35, 0.78), 0.03, TIMBER, sides=4)
    for _ in range(4):                                                                              # stains on it
        cx, cy = rnd.uniform(-0.45, 0.45), rnd.uniform(-0.48, -0.22)
        A.hull(bm, [(cx + rnd.uniform(-0.09, 0.09), cy + rnd.uniform(-0.06, 0.06), 0.851) for _ in range(6)] +
               [(cx, cy, 0.853)], GORE)
    A.loft(bm, [((0.15, -0.35, 0.0), 0.13, 0.13), ((0.15, -0.35, 0.28), 0.15, 0.15)], sides=8, ref=(1, 0, 0), mats=BOARDS)
    A.loft(bm, [((-1.35, -0.3, 0.0), 0.11, 0.1), ((-0.95, 0.15, 0.95), 0.09, 0.08)], sides=6, ref=(0, 0, 1), mats=SCRAPED)   # the fleshing beam
    for s in (-1, 1):
        rod(bm, (-1.25 + s * 0.12, -0.1, 0.0), (-1.2, -0.12, 0.5), 0.025, TIMBER, sides=4)
    return make("SkinningRack", bm, mats, at=at)


def lye_vat(seed=7, at=(0, 0, 0)):
    """The lye vat, where vitriol mites are flensed. 1 x 1.

    A great coopered vat on a ring of stones, bound with three iron hoops, the lye standing near its
    brim, sickly and pale; a long paddle across its rim, a dipper hung on its side, salt crusted round
    its foot where it has slopped over."""
    rnd = random.Random(seed)
    mats = ["Planks", "Iron", "Lye", "Stone", "Timber", "SaltOre"]
    STAVES, IRON, LYE, STONE, TIMBER, SALT = range(len(mats))
    bm = bmesh.new()
    for k in range(8):
        a = 2 * math.pi * k / 8 + rnd.uniform(-0.1, 0.1)
        x, y = 0.4 * math.cos(a), 0.4 * math.sin(a)
        A.hull(bm, [(x + rnd.uniform(-0.09, 0.09), y + rnd.uniform(-0.09, 0.09), rnd.uniform(0.0, 0.14)) for _ in range(8)], STONE)
    z0, z1, r0, r1 = 0.12, 1.0, 0.42, 0.47
    A.loft(bm, [((0, 0, z0), r0, r0), ((0, 0, z1), r1, r1)], sides=12, ref=(1, 0, 0), mats=STAVES)
    for z in (0.25, 0.58, 0.9):
        r = r0 + (r1 - r0) * (z - z0) / (z1 - z0) + 0.012
        A.loft(bm, [((0, 0, z - 0.025), r, r), ((0, 0, z + 0.025), r, r)], sides=12, ref=(1, 0, 0), mats=IRON)
    A.loft(bm, [((0, 0, z1 - 0.06), r1 - 0.03, r1 - 0.03), ((0, 0, z1 - 0.055), r1 - 0.03, r1 - 0.03)], sides=12, ref=(1, 0, 0), mats=LYE)
    rod(bm, (-0.6, 0.15, z1 + 0.02), (0.75, -0.1, z1 + 0.3), 0.025, TIMBER, sides=5)                 # the paddle across the rim
    A.hull(bm, [(0.75 + dx, -0.1 + dy, z1 + 0.3 + dz) for dx in (0.0, 0.25) for dy in (-0.07, 0.07) for dz in (-0.01, 0.04)], TIMBER)
    rod(bm, (0.0, -r1 - 0.01, z1 - 0.05), (0.0, -r1 - 0.06, z1 - 0.12), 0.01, IRON, sides=4)          # the dipper on its hook
    A.loft(bm, [((0.0, -r1 - 0.11, z1 - 0.45), 0.06, 0.06), ((0.0, -r1 - 0.11, z1 - 0.35), 0.07, 0.07)], sides=7, ref=(1, 0, 0), mats=IRON)
    rod(bm, (0.0, -r1 - 0.06, z1 - 0.12), (0.0, -r1 - 0.11, z1 - 0.35), 0.012, TIMBER, sides=4)
    for k in range(5):                                                                             # salt crust where it slopped
        a = rnd.uniform(-2.4, -0.7)
        c = (0.6 * math.cos(a), 0.6 * math.sin(a))
        A.hull(bm, [(c[0] + rnd.uniform(-0.12, 0.12), c[1] + rnd.uniform(-0.12, 0.12), 0.0) for _ in range(6)] + [(c[0], c[1], 0.02)], SALT)
    return make("LyeVat", bm, mats, at=at)


# ---- the house -------------------------------------------------------------------------------------

def spinning_wheel(seed=8, at=(0, 0, 0)):
    """A spinning wheel. 1 x 1 (the spinner sits at -Y).

    A sloping bench on three splayed legs; at its high end the big wheel between two uprights, a
    treadle under it worked by the foot through a rod; at its low end the flyer on its maidens, the
    drive band running from the wheel to it; a distaff dressed with wool standing up beside it."""
    mats = ["Oak", "Timber", "Wool", "Linen"]
    OAK, TIMBER, WOOL, BAND = range(len(mats))
    bm = bmesh.new()

    def bench_z(x):
        return 0.38 + (x + 0.35) * 0.18
    A.hull(bm, [(x, y, bench_z(x) + dz) for x in (-0.38, 0.38) for y in (-0.1, 0.1) for dz in (0.0, 0.06)], OAK)
    for x, y in ((-0.3, 0.0), (0.3, -0.16), (0.3, 0.16)):
        rod(bm, (x * 1.25, y * 1.6, 0.0), (x, y * 0.5, bench_z(x)), 0.022, TIMBER, sides=4)
    wx, wz, R = 0.25, 0.95, 0.42                                                                 # the wheel
    for x in (wx - 0.05, wx + 0.05):
        rod(bm, (x, 0.0, bench_z(wx) + 0.05), (x, 0.0, wz + 0.04), 0.02, TIMBER, sides=4)
    n = 14
    rim = [(wx, R * math.cos(2 * math.pi * k / n), wz + R * math.sin(2 * math.pi * k / n)) for k in range(n)]
    for a, b in zip(rim, rim[1:] + rim[:1]):
        rod(bm, a, b, 0.022, OAK, sides=4)
    for k in range(0, n, 2):
        rod(bm, (wx, 0.0, wz), rim[k], 0.01, TIMBER, sides=3)
    rod(bm, (wx - 0.07, 0.0, wz), (wx + 0.07, 0.0, wz), 0.035, TIMBER, sides=6)
    fx, fz = -0.28, bench_z(-0.28) + 0.3                                                         # the flyer on its maidens
    for y in (-0.08, 0.08):
        rod(bm, (fx, y, bench_z(fx) + 0.05), (fx, y, fz + 0.06), 0.018, TIMBER, sides=4)
    A.loft(bm, [((fx, -0.07, fz), 0.03, 0.03), ((fx, 0.07, fz), 0.03, 0.03)], sides=6, ref=(0, 0, 1), mats=WOOL)
    rod(bm, (fx, -0.12, fz), (fx, 0.12, fz), 0.008, TIMBER, sides=3)
    rod(bm, (wx, 0.0, wz - R), (fx, 0.1, fz - 0.03), 0.005, BAND, sides=3)                        # the drive band
    rod(bm, (wx, 0.0, wz + R), (fx, 0.1, fz + 0.03), 0.005, BAND, sides=3)
    box(bm, 0.0, -0.3, 0.02, 0.35, -0.12, 0.05, TIMBER)                                          # the treadle and its rod
    rod(bm, (0.3, -0.12, 0.05), (wx, -0.06, wz - 0.06), 0.008, TIMBER, sides=3)
    rod(bm, (-0.05, 0.08, bench_z(-0.05) + 0.05), (-0.1, 0.12, 1.25), 0.014, TIMBER, sides=4)    # the distaff and its wool
    A.loft(bm, [((-0.095, 0.115, 1.0), 0.03, 0.03), ((-0.097, 0.117, 1.08), 0.07, 0.07), ((-0.1, 0.12, 1.22), 0.04, 0.04),
                ((-0.1, 0.12, 1.25), 0.0, 0.0)], sides=7, ref=(1, 0, 0), mats=WOOL)
    return make("SpinningWheel", bm, mats, at=at, smooth=40)


# ---- the apothecary's still ------------------------------------------------------------------------

def still(seed=9, at=(0, 0, 0)):
    """The apothecary's still. 2 x 1 (along X).

    A brick firebox with its stoke hole glowing, a copper pot sitting in its top; the pot's head
    domed and drawn to a long pipe that slopes down into the worm tub (a barrel of cold water), and a
    spout out of the tub's foot dripping into a glass jar on a stool."""
    mats = ["Brick", "Copper", "Ember", "Planks", "Iron", "Glaze", "Mere", "Timber", "Scorch"]
    BRICK, COPPER, EMBER, STAVES, IRON, GLASS, WATER, TIMBER, SOOT = range(len(mats))
    bm = bmesh.new()
    box(bm, -0.95, -0.35, 0, -0.25, 0.35, 0.55, BRICK)                                          # the firebox
    box(bm, -0.97, -0.37, 0.55, -0.23, 0.37, 0.6, SOOT)
    pts = [(-0.75, -0.36, 0.08), (-0.45, -0.36, 0.08)] + [(-0.6 + 0.15 * math.cos(t), -0.36, 0.25 + 0.12 * math.sin(t))
                                                         for t in (math.pi * k / 4 for k in range(5))]
    A.hull(bm, pts + [(x, y - 0.01, z) for x, y, z in pts], EMBER)
    A.loft(bm, [((-0.6, 0, 0.5), 0.22, 0.22), ((-0.6, 0, 0.72), 0.3, 0.3), ((-0.6, 0, 0.9), 0.27, 0.27),
                ((-0.6, 0, 0.95), 0.12, 0.12)], sides=12, ref=(1, 0, 0), mats=COPPER)            # the pot
    A.loft(bm, [((-0.6, 0, 0.95), 0.12, 0.12), ((-0.6, 0, 1.1), 0.17, 0.17), ((-0.6, 0, 1.25), 0.1, 0.1),
                ((-0.58, 0, 1.33), 0.05, 0.05)], sides=10, ref=(1, 0, 0), mats=COPPER)           # its head
    rod(bm, (-0.58, 0, 1.33), (0.35, 0, 1.0), 0.03, COPPER, sides=6)                             # the pipe
    A.loft(bm, [((0.5, 0, 0), 0.3, 0.3), ((0.5, 0, 0.5), 0.33, 0.33), ((0.5, 0, 0.98), 0.31, 0.31)],
           sides=10, ref=(1, 0, 0), mats=STAVES)                                                  # the worm tub
    for z in (0.12, 0.85):
        A.loft(bm, [((0.5, 0, z - 0.02), 0.32 + z * 0.02, 0.32 + z * 0.02), ((0.5, 0, z + 0.02), 0.32 + z * 0.02, 0.32 + z * 0.02)],
               sides=10, ref=(1, 0, 0), mats=IRON)
    A.loft(bm, [((0.5, 0, 0.94), 0.28, 0.28), ((0.5, 0, 0.945), 0.28, 0.28)], sides=10, ref=(1, 0, 0), mats=WATER)
    rod(bm, (0.5, -0.3, 0.2), (0.5, -0.5, 0.17), 0.018, COPPER, sides=5)                         # the spout
    box(bm, 0.38, -0.68, 0.0, 0.62, -0.46, 0.04, TIMBER)                                         # a block, and the jar on it
    A.loft(bm, [((0.5, -0.57, 0.04), 0.06, 0.06), ((0.5, -0.57, 0.12), 0.08, 0.08), ((0.5, -0.57, 0.16), 0.03, 0.03)],
           sides=8, ref=(1, 0, 0), mats=GLASS)
    return make("Still", bm, mats, at=at, smooth=35)


# ---- Tailoring and Artifice ------------------------------------------------------------------------

def tanning_vat(seed=10, at=(0, 0, 0)):
    """A tanner's pit, where hides lie in the bark liquor. 2 x 1 (along X).

    A plank-lined pit sunk in the ground with a kerb of boards round it, the brown liquor standing in
    it with a hide's dark back showing; a pole across it to lift hides by, a long hook leaning on the
    kerb, a heap of crushed oak bark beside it for the liquor."""
    rnd = random.Random(seed)
    mats = ["Planks", "Timber", "Umber", "Hide", "Bark", "Iron"]
    BOARDS, TIMBER, LIQUOR, HIDE, BARK, IRON = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1 = -0.8, 0.5, -0.4, 0.4
    for x in (x0, x1 - 0.1):
        box(bm, x, y0, -0.05, x + 0.1, y1, 0.25, BOARDS)
    for y in (y0, y1 - 0.1):
        box(bm, x0, y, -0.05, x1, y + 0.1, 0.25, BOARDS)
    box(bm, x0 + 0.1, y0 + 0.1, 0.12, x1 - 0.1, y1 - 0.1, 0.13, LIQUOR)
    A.hull(bm, [(x, y, 0.135) for x, y in ((-0.5, -0.15), (-0.1, -0.2), (0.15, 0.0), (-0.15, 0.2), (-0.55, 0.12))] +
           [(-0.2, 0.0, 0.15)], HIDE)
    rod(bm, (x0 - 0.1, 0.0, 0.32), (x1 + 0.1, 0.05, 0.32), 0.035, TIMBER, sides=6)
    for x in (x0 - 0.05, x1 + 0.05):
        for s in (-1, 1):
            rod(bm, (x, s * 0.12, -0.02), (x, 0.0, 0.34), 0.025, TIMBER, sides=4)
    rod(bm, (x1 + 0.05, y0 - 0.1, 0.0), (x1 - 0.2, y0 + 0.05, 1.5), 0.02, TIMBER, sides=4)            # the hook, leaning
    rod(bm, (x1 - 0.2, y0 + 0.05, 1.5), (x1 - 0.12, y0 + 0.02, 1.42), 0.012, IRON, sides=4)
    for _ in range(9):                                                                             # the bark heap
        x, y, s = rnd.uniform(0.7, 1.05), rnd.uniform(-0.3, 0.3), rnd.uniform(0.07, 0.12)
        A.hull(bm, [(x + rnd.uniform(-s, s), y + rnd.uniform(-s, s), rnd.uniform(0.0, s * 1.4 * (1.2 - abs(y)))) for _ in range(6)], BARK)
    return make("TanningVat", bm, mats, at=at)


def loom(seed=11, at=(0, 0, 0)):
    """A floor loom. 2 x 1 (along X), the weaver sitting at -Y.

    Four posts in a frame braced at the foot, a breast beam toward the weaver with the cloth wound
    on below it, the warp running back to the warp beam behind, the beater hanging from the top
    rail over the warp, two treadles under it and the bench in front."""
    mats = ["Oak", "Timber", "Linen", "Wool"]
    OAK, TIMBER, CLOTH, WARP = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.75, 0.75, -0.4, 0.45, 1.6
    for x in (x0, x1):
        for y in (y0, y1):
            box(bm, x - 0.05, y - 0.05, 0, x + 0.05, y + 0.05, H if y > 0 else 1.0, OAK)
        box(bm, x - 0.04, y0, 0.1, x + 0.04, y1, 0.18, TIMBER)
        box(bm, x - 0.04, y0, 0.95, x + 0.04, y1, 1.0, TIMBER)
    box(bm, x0, y1 - 0.05, H - 0.08, x1, y1 + 0.05, H, TIMBER)                                    # the top rail
    rod(bm, (x0, y0, 0.9), (x1, y0, 0.9), 0.045, OAK)                                             # the breast beam
    A.loft(bm, [((x0 + 0.08, y0 + 0.08, 0.55), 0.08, 0.08), ((x1 - 0.08, y0 + 0.08, 0.55), 0.08, 0.08)], sides=8,
           ref=(0, 0, 1), mats=CLOTH)                                                             # cloth on its beam
    rod(bm, (x0, y1, 0.85), (x1, y1, 0.85), 0.06, OAK)                                            # the warp beam
    A.hull(bm, [(x, y, z) for x in (x0 + 0.08, x1 - 0.08) for y, z in ((y0 + 0.02, 0.94), (y1 - 0.02, 0.9))] +
           [(x, y, z - 0.008) for x in (x0 + 0.08, x1 - 0.08) for y, z in ((y0 + 0.02, 0.94), (y1 - 0.02, 0.9))], WARP)
    for x in (x0 + 0.1, x1 - 0.1):                                                                # the beater, hanging
        box(bm, x - 0.02, 0.05, 1.0, x + 0.02, 0.09, H - 0.08, TIMBER)
    box(bm, x0 + 0.06, 0.03, 0.98, x1 - 0.06, 0.11, 1.06, OAK)
    for x in (-0.18, 0.18):                                                                       # the treadles
        box(bm, x - 0.05, y0 - 0.05, 0.03, x + 0.05, y1 - 0.1, 0.07, TIMBER)
    box(bm, -0.6, -0.95, 0.42, 0.6, -0.7, 0.48, OAK)                                              # the bench
    for x in (-0.5, 0.5):
        box(bm, x - 0.04, -0.9, 0, x + 0.04, -0.75, 0.42, TIMBER)
    return make("Loom", bm, mats, at=at)


def workbench(seed=12, at=(0, 0, 0)):
    """A joiner's workbench, where things are put together. 2 x 1 (along X).

    A thick top of two boards on heavy legs tied by stretchers, a vice at its left end with its
    screw and handle, a shelf below with offcuts on it, tools on the top: a mallet, a saw, a plane,
    chisels in a row, shavings by the plane."""
    rnd = random.Random(seed)
    mats = ["Oak", "Timber", "Iron", "Planks", "Ash"]
    OAK, TIMBER, IRON, BOARDS, SHAVINGS = range(len(mats))
    bm = bmesh.new()
    x0, x1, y0, y1, H = -0.9, 0.9, -0.35, 0.35, 0.86
    for y, w in ((y0, 0.36), (0.01, 0.34)):
        box(bm, x0, y, H - 0.09, x1, y + w, H, OAK)
    for x in (x0 + 0.12, x1 - 0.12):
        for y in (y0 + 0.08, y1 - 0.08):
            box(bm, x - 0.06, y - 0.06, 0, x + 0.06, y + 0.06, H - 0.09, TIMBER)
        box(bm, x - 0.04, y0 + 0.08, 0.2, x + 0.04, y1 - 0.08, 0.27, TIMBER)
    box(bm, x0 + 0.12, -0.25, 0.2, x1 - 0.12, 0.25, 0.25, BOARDS)                                # the shelf
    for k in range(3):
        box(bm, x0 + 0.3 + 0.35 * k, -0.15, 0.25, x0 + 0.55 + 0.35 * k, 0.05, 0.3, BOARDS)
    box(bm, x0 - 0.06, y0 - 0.05, H - 0.3, x0, y0 + 0.25, H, OAK)                                # the vice's chop
    rod(bm, (x0 - 0.2, y0 + 0.1, H - 0.15), (x0 + 0.15, y0 + 0.1, H - 0.15), 0.025, TIMBER)     # its screw
    rod(bm, (x0 - 0.2, y0 + 0.0, H - 0.15), (x0 - 0.2, y0 + 0.2, H - 0.15), 0.015, TIMBER)
    # the tools: a mallet, a saw, a plane with its shavings, chisels
    rod(bm, (-0.45, -0.15, H + 0.03), (-0.15, -0.2, H + 0.03), 0.018, TIMBER)
    box(bm, -0.55, -0.22, H, -0.43, -0.08, H + 0.1, OAK)
    A.hull(bm, [(0.05, -0.2, H + 0.002), (0.6, -0.2, H + 0.002), (0.6, -0.1, H + 0.002), (0.05, -0.06, H + 0.002),
                (0.05, -0.2, H + 0.006), (0.6, -0.2, H + 0.006), (0.6, -0.1, H + 0.006), (0.05, -0.06, H + 0.006)], IRON)
    box(bm, -0.04, -0.2, H, 0.08, -0.06, H + 0.03, TIMBER)
    box(bm, 0.3, 0.12, H, 0.55, 0.2, H + 0.08, OAK)
    for _ in range(6):
        x, y = rnd.uniform(0.55, 0.8), rnd.uniform(0.05, 0.28)
        A.loft(bm, [((x, y, H + 0.02), 0.02, 0.02), ((x + 0.06, y + 0.02, H + 0.025), 0.022, 0.022)], sides=5, ref=(0, 0, 1), mats=SHAVINGS)
    for k in range(4):
        x = -0.75 + 0.07 * k
        rod(bm, (x, 0.12, H + 0.012), (x, 0.3, H + 0.012), 0.012, TIMBER, sides=4)
        box(bm, x - 0.01, 0.02, H + 0.005, x + 0.01, 0.12, H + 0.012, IRON)
    return make("Workbench", bm, mats, at=at)


ASSETS = {
    "Forge": forge,
    "Anvil": anvil,
    "Furnace": furnace,
    "Range": cooking_range,
    "CharcoalKiln": charcoal_kiln,
    "ApothecaryBench": apothecary_bench,
    "SkinningRack": skinning_rack,
    "LyeVat": lye_vat,
    "SpinningWheel": spinning_wheel,
    "Still": still,
    "TanningVat": tanning_vat,
    "Loom": loom,
    "Workbench": workbench,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
