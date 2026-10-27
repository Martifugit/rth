"""Cave and dungeon entrances: a kit of parts that arrange in many ways (see tools/art/blender/README.md).

How the parts fit together:
- Rock pieces are irregular faceted masses that overlap where they meet, never edge-matched
  tiles, so any arrangement works: a hillside cave, a crawl hole, a cleft between spires.
- Every piece faces Blender -Y (the front, Unity +Z) with its origin on the ground at its middle;
  spanning pieces (the lip, the lintels) carry their own height, so they sit on rocks or posts.
- The darkness is its own piece: a throat that tunnels into the hill, narrowing and sloping down,
  its faces darkening from cinder to black. Set behind any arrangement, it gives the depth.

The mystery is the lore's unfinished world: the makers' stonework (pale, twisted, too regular: the
same hand as the heath's standing stones), pieces caught half made (a cut post beside a raw one, a
lintel turning from worked stone into rock), and one pale-blue inlaid band per piece, the moonlight
of Selenfall's crater. Dressing: hanging roots, an offering cairn, a propped mine adit, a seal-stone.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\caves\\entrances.py")
    runpy.run_path(path, init_globals={"EXPORT": True})   # every part to Resources/Caves/<Name>.fbx
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
sys.path.insert(0, os.path.join(HERE, "..", "nature"))
import rth_art as A  # noqa: E402
import basin as N  # noqa: E402  (the nature kit's helpers: make, rod, wobble, slab)

ROCK = ["Scree", "Cinder", "Moss"]          # faces, undersides in shadow, mossy tops
MAKER = "Gravestone"                       # the makers' pale stone
INLAY = "Moonlight"                        # their one pale-blue band


# ---- shapes -----------------------------------------------------------------------------------

def chunk(bm, centre, size, rnd, lean=(0.0, 0.0), sink=0.1, mats=(0, 1, 2), points=20):
    """A faceted rock mass: points scattered over a rounded box (size: half-widths and the
    height), each pushed in or out, the highest flattened into a ledge, wrapped in a hull; lean
    shifts the top. Faces are coloured by which way they face: rock, cinder underneath, moss on top
    (material slots `mats`)."""
    c = Vector(centre)
    sx, sy, h = size
    pts = []
    for i in range(points):
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
        t = (d.z + 1) / 2 if i >= 4 else 0.0                     # the first four hold the footprint
        j = rnd.uniform(0.78, 1.08)
        x = math.copysign(abs(d.x) ** 0.55, d.x) * sx * j
        y = math.copysign(abs(d.y) ** 0.55, d.y) * sy * j
        if i < 4:
            x, y = (sx if i % 2 else -sx) * rnd.uniform(0.75, 1.0), (sy if i < 2 else -sy) * rnd.uniform(0.75, 1.0)
        z = -sink + t * (h + sink)
        if z > h * 0.82:
            z = h * rnd.uniform(0.86, 0.96)                       # a ledge on top
        pts.append(c + Vector((x + lean[0] * h * t, y + lean[1] * h * t, z)))
    faces = A.hull(bm, pts)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    for f in faces:
        f.normal_update()
        f.material_index = mats[2] if f.normal.z > 0.75 else mats[1] if f.normal.z < -0.3 else mats[0]
    return faces


def twisted(bm, z0, length, half0, half1, a0, twist, mat, rings=6, caps=True):
    """A four-sided column from z0 up `length`, its section shrinking from half0 to half1 (half
    widths) while it turns from a0 through `twist`: the makers' shape."""
    out = []
    for i in range(rings + 1):
        t = i / rings
        hx, hy = half0[0] + (half1[0] - half0[0]) * t, half0[1] + (half1[1] - half0[1]) * t
        a = a0 + twist * t
        out.append([bm.verts.new((x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a), z0 + length * t))
                    for x, y in ((-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy))])
    for r0, r1 in zip(out, out[1:]):
        for k in range(4):
            bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k])).material_index = mat
    if caps:
        bm.faces.new(out[-1]).material_index = mat
        bm.faces.new(list(reversed(out[0]))).material_index = mat
    return out


def band(bm, z, half, angle, mat, depth=0.06, swell=1.08):
    """The inlaid band: a short ring a hair proud of a twisted column at height z."""
    twisted(bm, z - depth / 2, depth, (half[0] * swell, half[1] * swell), (half[0] * swell, half[1] * swell),
            angle, 0.0, mat, rings=1)


def lay(bm, verts, matrix):
    bmesh.ops.transform(bm, matrix=matrix, verts=verts)


# ---- natural rock ----------------------------------------------------------------------------

def rock_mass(seed=1, at=(0, 0, 0), big=True):
    """A rock mass to build cave sides and hillsides from: a broad base chunk with a narrower one
    riding on it, set back and leaning. big: about 2 x 1.5 tiles and 3 high; else 1 x 1 and 2.2."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    if big:
        chunk(bm, (0, 0, 0), (1.0, 0.75, 1.7), rnd)
        chunk(bm, (rnd.uniform(-0.45, -0.2), 0.1, 0.9), (0.62, 0.55, 2.0), rnd, lean=(rnd.uniform(-0.12, 0.0), 0.05))
        chunk(bm, (rnd.uniform(0.25, 0.5), 0.2, 0.5), (0.55, 0.5, 1.6), rnd, lean=(0.08, 0.04))
    else:
        chunk(bm, (0, 0, 0), (0.55, 0.5, 1.3), rnd)
        chunk(bm, (0.08, 0.08, 0.7), (0.38, 0.36, 1.4), rnd, lean=(rnd.uniform(-0.1, 0.1), 0.04))
    return N.make("RockMass" if big else "RockMassSmall", bm, ROCK, smooth_angle=25, at=at, coll="Caves")


def rock_spire(seed=3, at=(0, 0, 0)):
    """A tall leaning tooth of rock, for a cleft, or to stand guard by a mouth."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.45, 0.4, 1.6), rnd)
    chunk(bm, (0.05, 0.02, 1.3), (0.28, 0.25, 1.9), rnd, lean=(0.12, 0.04))
    return N.make("RockSpire", bm, ROCK, smooth_angle=25, at=at, coll="Caves")


def rock_lip(seed=5, at=(0, 0, 0), height=2.2):
    """The overhang over a mouth: a long flat slab of rock lying at `height`, about 3 tiles long, its
    ends to rest on the masses either side; moss on top, shadow under."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0.1, height), (1.55, 0.7, 0.55), rnd, sink=0.0)
    chunk(bm, (rnd.uniform(-0.6, 0.6), 0.25, height + 0.35), (0.8, 0.5, 0.45), rnd, sink=0.0)
    return N.make("RockLip", bm, ROCK, smooth_angle=25, at=at, coll="Caves")


def rubble(seed=7, at=(0, 0, 0)):
    """Fallen stone at the foot of a face: a spill of chunks, the biggest at the back."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for i in range(8):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 0.75) * (0.5 if i < 2 else 1)
        s = rnd.uniform(0.18, 0.32) if i < 3 else rnd.uniform(0.08, 0.16)
        chunk(bm, (d * math.cos(a), d * math.sin(a) * 0.6 + 0.2, 0), (s, s * 0.9, s * 1.1), rnd, sink=0.05)
    return N.make("Rubble", bm, ROCK, smooth_angle=25, at=at, coll="Caves")


def throat(seed=1, at=(0, 0, 0), width=1.0, height=2.1, low=False):
    """The darkness behind a mouth: a tunnel going into the hill (+Y) on a level floor (the ground
    would cover anything lower), narrowing as its roof comes down, its faces darkening from the rim
    to black. Seen from inside only (its faces point in), so it disappears wherever rock does not
    cover it; set anything behind it at least 2.3 tiles back from its origin. low: a crawl hole
    about one tile."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    if low:
        width, height = 0.55, 0.95
    # The mouth's outline: a flat floor, walls, a rounded top (half widths, heights).
    outline = [(-1.0, 0.0), (1.0, 0.0), (1.0, 0.55), (0.8, 0.85), (0.4, 1.0), (-0.35, 1.0), (-0.8, 0.85), (-1.0, 0.55)]
    steps = [(-0.15, 1.0, 1.0), (0.5, 0.9, 0.8), (1.1, 0.8, 0.6), (1.7, 0.7, 0.42), (2.2, 0.6, 0.3)]
    rings = []
    for y, scale, dz in steps:
        rings.append([bm.verts.new((x * width * scale * rnd.uniform(0.95, 1.05), y,
                                    0.02 + z * height * dz * rnd.uniform(0.95, 1.05))) for x, z in outline])
    mats = [0, 1, 2, 2]
    for r0, r1, m in zip(rings, rings[1:], mats):
        n = len(outline)
        for k in range(n):
            f = bm.faces.new((r0[k], r1[k], r1[(k + 1) % n], r0[(k + 1) % n]))
            f.material_index = m
    end = bm.faces.new(rings[-1])                         # the far end, black, facing the mouth
    end.material_index = 2
    for f in bm.faces:                                   # every face looks inward
        f.normal_update()
        mid = f.calc_center_median()
        inward = Vector((0, -1, 0)) if f is end else Vector((0, mid.y, mid.z * 0.5 + 0.3)) - mid
        if f.normal.dot(inward) < 0:
            f.normal_flip()
    name = "ThroatLow" if low else "Throat"
    ob = N.make(name, bm, ["CryptDarkStone", "CryptEarth", "CryptEarth"], smooth_angle=60, at=at, coll="Caves", finish=False)
    return ob


def hanging_roots(seed=2, at=(0, 0, 0), height=2.25, span=1.6):
    """Roots hanging from under a lip across a mouth, thin and kinked, some long enough to brush a
    tall visitor's head."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for i in range(9):
        x = -span / 2 + span * (i + rnd.uniform(0.1, 0.9)) / 9
        y = rnd.uniform(-0.15, 0.15)
        length = rnd.uniform(0.35, 1.0)
        pts = [Vector((x, y, height + 0.1))]
        for k in range(1, 4):
            pts.append(pts[-1] + Vector((rnd.uniform(-0.08, 0.08), rnd.uniform(-0.05, 0.05), -length / 3)))
        N.rod(bm, pts, rnd.uniform(0.014, 0.026), sides=3, taper=0.4, tip=pts[-1] - Vector((0, 0, 0.08)))
    return N.make("HangingRoots", bm, ["Bark"], smooth_angle=50, at=at, coll="Caves")


# ---- the makers' stonework ------------------------------------------------------------------------

PILLAR_H = 2.6
SPAN = 1.25                                 # posts stand this far either side of a doorway's middle


def maker_pillar(seed=1, at=(0, 0, 0), broken=False):
    """A doorpost of the makers: a pale square column twisting through 70 degrees as it rises, too
    true for any mason, with one band of pale-blue inlay. broken: snapped off at chest height, the
    break jagged, its top lying at its foot."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    half0, half1, twist = (0.24, 0.24), (0.19, 0.19), math.radians(70)
    if broken:
        h = 1.35
        rings = twisted(bm, -0.1, h + 0.1, half0, (0.215, 0.215), 0.0, twist * h / PILLAR_H, 0, caps=False)
        top = rings[-1]
        peak = bm.verts.new((0.05, -0.03, h + 0.22))
        for k in range(4):
            v = top[k]
            v.co.z += rnd.uniform(-0.12, 0.08)
            bm.faces.new((v, top[(k + 1) % 4], peak)).material_index = 0
        before = set(bm.verts)
        twisted(bm, 0, 0.9, (0.21, 0.21), (0.19, 0.19), 0.3, twist * 0.35, 0)
        lay(bm, [v for v in bm.verts if v not in before],
            Matrix.Translation((0.55, -0.35, 0.21)) @ Matrix.Rotation(math.radians(86), 4, Vector((0.3, 1, 0)).normalized()))
        band(bm, 0.55, (0.235, 0.235), twist * 0.65 / PILLAR_H, 1)
    else:
        twisted(bm, -0.1, PILLAR_H + 0.1, half0, half1, 0.0, twist, 0)
        z = 1.95
        band(bm, z, (half0[0] + (half1[0] - half0[0]) * (z + 0.1) / (PILLAR_H + 0.1),) * 2,
             twist * (z + 0.1) / (PILLAR_H + 0.1), 1)
    return N.make("MakerPillarBroken" if broken else "MakerPillar", bm, [MAKER, INLAY], smooth_angle=20, at=at,
                  coll="Caves")


def lintel_beam(bm, x0, x1, z, half, twist, mat, a0=0.0):
    """A twisted beam lying along X from x0 to x1 with its middle at height z."""
    before = set(bm.verts)
    rings = twisted(bm, 0, x1 - x0, half, half, a0, twist, mat)
    lay(bm, [v for v in bm.verts if v not in before],
        Matrix.Translation((x0, 0, z)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    return rings


def maker_lintel(seed=1, at=(0, 0, 0)):
    """The makers' lintel for two pillars SPAN either side of its middle: a beam lying across their
    tops, twisting a quarter turn along its length, a band of inlay at its middle."""
    bm = bmesh.new()
    length = 2 * SPAN + 0.6
    z = PILLAR_H + 0.22
    lintel_beam(bm, -length / 2, length / 2, z, (0.24, 0.2), math.radians(90), 0)
    before = set(bm.verts)
    twisted(bm, -0.04, 0.08, (0.27, 0.23), (0.27, 0.23), math.radians(45), 0, 1, rings=1)
    lay(bm, [v for v in bm.verts if v not in before], Matrix.Translation((0, 0, z)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    return N.make("MakerLintel", bm, [MAKER, INLAY], smooth_angle=20, at=at, coll="Caves")


def maker_steps(seed=1, at=(0, 0, 0)):
    """Steps up to a threshold: three pale treads rising towards the dark (+Y), each a little
    narrower, their edges too sharp, the top one running on as the threshold slab."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(3):
        y0 = -0.9 + 0.38 * k
        top = 0.14 * (k + 1)
        w = 0.95 - 0.06 * k
        A.box(bm, (-w, y0, -0.1), (w, 0.6 if k == 2 else y0 + 0.4, top), 0)
    return N.make("MakerSteps", bm, [MAKER], smooth_angle=20, at=at, coll="Caves")


def unfinished_door(seed=4, at=(0, 0, 0)):
    """A doorway caught half made: the left post a finished twisted column, the right one still raw
    rock with only its foot cut square; the lintel twists out from the left post as worked stone and
    turns into rough rock across the middle, where a seam of inlay marks where the making stopped."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    twist = math.radians(70)
    before = set(bm.verts)
    twisted(bm, -0.1, PILLAR_H + 0.1, (0.24, 0.24), (0.19, 0.19), 0.0, twist, 0)
    lay(bm, [v for v in bm.verts if v not in before], Matrix.Translation((-SPAN, 0, 0)))
    A.box(bm, (SPAN - 0.26, -0.24, -0.1), (SPAN + 0.26, 0.24, 0.85), 0)          # the cut foot
    chunk(bm, (SPAN + 0.05, 0.02, 0.8), (0.36, 0.33, PILLAR_H - 0.55), rnd, mats=(1, 2, 3))  # the raw rest
    z = PILLAR_H + 0.22
    lintel_beam(bm, -SPAN - 0.3, 0.05, z, (0.24, 0.2), math.radians(45), 0)
    chunk(bm, (0.85, 0.0, z - 0.3), (0.95, 0.3, 0.6), rnd, sink=0.0, mats=(1, 2, 3))
    A.box(bm, (0.03, -0.27, z - 0.27), (0.09, 0.27, z + 0.27), 4)                 # the seam of inlay
    return N.make("UnfinishedDoor", bm, [MAKER] + ROCK + [INLAY], smooth_angle=20, at=at, coll="Caves")


def seal_stone(seed=1, at=(0, 0, 0)):
    """The round stone that once sealed a doorway, rolled half aside: a thick disc on its edge,
    leaning, sunk a little, its face carrying a raised square turned on its corner and, in the
    square, a small pale-blue inlay."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    (_, _), faces = A.prism(bm, 12, 0.95, 0.95, -0.12, 0.12, (0, 0), mat_index=0, cap_top=True, cap_bottom=True)
    (_, _), boss = A.prism(bm, 4, 0.42, 0.38, 0.12, 0.2, (0, 0), rot=math.radians(45), mat_index=0, cap_top=True)
    (_, _), inlay = A.prism(bm, 4, 0.14, 0.14, 0.2, 0.215, (0, 0), rot=math.radians(45), mat_index=1, cap_top=True)
    lay(bm, list(bm.verts), Matrix.Translation((0, 0, 0.82)) @ Matrix.Rotation(math.radians(-8), 4, 'Y')
        @ Matrix.Rotation(math.radians(90), 4, 'X'))
    return N.make("SealStone", bm, [MAKER, INLAY], smooth_angle=30, at=at, coll="Caves")


# ---- dressing ---------------------------------------------------------------------------------

def offering(seed=3, at=(0, 0, 0)):
    """An offering left at a mouth: four flat stones stacked askew, a guttered candle stub on top
    with a small flame, a scatter of pale chips (bone, or shell) at the foot."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    z, r = 0.0, 0.26
    for _ in range(4):
        h = rnd.uniform(0.06, 0.09)
        N.slab(bm, (rnd.uniform(-0.02, 0.02), rnd.uniform(-0.02, 0.02), z), r, h, 6, rnd, rot=rnd.uniform(0, 3))
        z += h - 0.005
        r *= 0.8
    N.rod(bm, [(0, 0, z - 0.01), (0.005, 0, z + 0.09)], 0.03, mat=2, sides=5)
    A.spike(bm, (0.005, 0, z + 0.085), 0.018, (0.01, 0, z + 0.16), sides=4, mat=3)
    for _ in range(5):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.3, 0.5)
        c = Vector((d * math.cos(a), d * math.sin(a), 0))
        A.hull(bm, [c + Vector((0.035 * math.cos(t), 0.02 * math.sin(t), -0.01)) for t in (0, 2.1, 4.2)] +
               [c + Vector((0, 0, 0.025))], mat=2)
    return N.make("Offering", bm, ["Scree", "Gravestone", "SaintBone", "Ember"], smooth_angle=30, at=at, coll="Caves")


def propped_adit(seed=2, at=(0, 0, 0)):
    """A mine adit's mouth propped with timber: two posts leaning in a little under a cap beam, a
    wedge driven in at each joint, for a cave someone has worked."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    top = 2.05
    for s in (-1, 1):
        N.rod(bm, N.wobble(rnd, (s * 0.95, 0, -0.05), (s * 0.82, 0, top), 3, 0.03), 0.08, sides=6, taper=0.9)
        A.hull(bm, [(s * 0.7, -0.06, top - 0.12), (s * 0.7, 0.06, top - 0.12), (s * 0.74, 0, top - 0.02),
                    (s * 0.66, -0.06, top - 0.02), (s * 0.66, 0.06, top - 0.02)])
    N.rod(bm, N.wobble(rnd, (-1.15, 0, top + 0.06), (1.15, 0, top + 0.08), 4, 0.03), 0.09, sides=6)
    return N.make("ProppedAdit", bm, ["Driftwood"], smooth_angle=50, at=at, coll="Caves")


# ---- the board --------------------------------------------------------------------------------

W, H = 34, 18


def ground():
    bm = bmesh.new()
    vs = {(x, y): bm.verts.new((x, y, 0)) for x in range(W + 1) for y in range(H + 1)}
    for x in range(W):
        for y in range(H):
            bm.faces.new((vs[x, y], vs[x + 1, y], vs[x + 1, y + 1], vs[x, y + 1]))
    return N.make("Ground", bm, ["Grass"], coll="Preview")


def build():
    """Four entrances built from the parts, along the front; every part on its own behind them."""
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Caves", "Preview", "Nature"):
            bpy.data.collections.remove(c)
    ground()
    o = {}
    # 1. A hillside cave: masses either side, the lip across, roots, the throat, rubble.
    x, y = 5.0, 5.0
    o["h_left"] = rock_mass(seed=1, at=(x - 2.0, y + 0.4, 0))
    o["h_right"] = rock_mass(seed=2, at=(x + 2.1, y + 0.3, 0))
    o["h_back"] = rock_mass(seed=9, at=(x, y + 2.9, 0))
    o["h_lip"] = rock_lip(at=(x, y + 0.3, 0))
    o["h_roots"] = hanging_roots(at=(x, y - 0.25, 0))
    o["h_throat"] = throat(at=(x, y, 0))
    o["h_rubble"] = rubble(at=(x + 1.6, y - 1.1, 0))
    # 2. The makers' door: pillars and lintel before a rock face, steps going down, an offering.
    x = 13.0
    o["m_back_l"] = rock_mass(seed=4, at=(x - 1.6, y + 1.3, 0))
    o["m_back_r"] = rock_mass(seed=5, at=(x + 1.7, y + 1.2, 0))
    o["m_back"] = rock_lip(seed=8, at=(x, y + 1.0, 0), height=2.4)
    o["m_throat"] = throat(seed=3, at=(x, y + 0.6, 0))
    o["m_left"] = maker_pillar(at=(x - SPAN, y, 0))
    o["m_right"] = maker_pillar(seed=2, at=(x + SPAN, y, 0))
    o["m_lintel"] = maker_lintel(at=(x, y, 0))
    o["m_steps"] = maker_steps(at=(x, y + 0.1, 0))
    o["m_offering"] = offering(at=(x + 1.9, y - 0.9, 0))
    # 3. The unfinished door, set in rock, its seal-stone rolled aside.
    x = 21.0
    o["u_left"] = rock_mass(seed=6, at=(x - 2.3, y + 0.8, 0))
    o["u_right"] = rock_mass(seed=7, at=(x + 2.4, y + 0.9, 0))
    o["u_lip"] = rock_lip(seed=11, at=(x + 0.3, y + 1.0, 0), height=2.6)
    o["u_throat"] = throat(seed=5, at=(x, y + 0.5, 0))
    o["u_door"] = unfinished_door(at=(x, y, 0))
    o["u_seal"] = seal_stone(at=(x - 2.0, y - 1.0, 0))
    o["u_broken"] = maker_pillar(seed=3, at=(x + 2.4, y - 1.4, 0), broken=True)
    # 4. A worked crawl into a cleft: two spires, a small mass, a propped adit, the low throat.
    x = 29.0
    o["c_spire_l"] = rock_spire(at=(x - 1.4, y + 0.5, 0))
    o["c_spire_r"] = rock_spire(seed=8, at=(x + 1.5, y + 0.6, 0))
    o["c_back"] = rock_mass(seed=12, at=(x, y + 2.9, 0))
    o["c_adit"] = propped_adit(at=(x, y, 0))
    o["c_throat"] = throat(seed=7, at=(x, y + 0.1, 0))
    # The parts on their own.
    row = 12.5
    for i, (name, fn) in enumerate(PARTS.items()):
        o["part_" + name] = fn(at=(2.0 + 2.6 * i, row + (1.5 if i % 2 else 0), 0))
    return o


def rig():
    A.preview_rig(focus=(17.0, 8.0, 1.0), distance=32.0, pitch=40, yaw=-15, ground=0, human=True,
                  human_at=(13.0, 3.2))
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


# Name in the game (Resources/Caves/<Name>, a piece's "model") to builder.
PARTS = {
    "RockMass": rock_mass, "RockMassSmall": lambda at=(0, 0, 0): rock_mass(at=at, big=False),
    "RockSpire": rock_spire, "RockLip": rock_lip, "Rubble": rubble,
    "Throat": throat, "ThroatLow": lambda at=(0, 0, 0): throat(at=at, low=True),
    "HangingRoots": hanging_roots,
    "MakerPillar": maker_pillar, "MakerPillarBroken": lambda at=(0, 0, 0): maker_pillar(at=at, broken=True),
    "MakerLintel": maker_lintel, "MakerSteps": maker_steps,
    "UnfinishedDoor": unfinished_door, "SealStone": seal_stone,
    "Offering": offering, "ProppedAdit": propped_adit,
}


def export_all():
    out = {}
    for name, builder in PARTS.items():
        A.reset()
        ob = builder()
        ob.name = ob.data.name = name
        A.export([ob], "Resources/Caves/%s.fbx" % name)
        out[name] = A.report([ob])["triangles"]
    return out


if globals().get("EXPORT"):
    result = export_all()
else:
    objs = build()
    rig()
    result = {k: A.report([ob])["triangles"] for k, ob in objs.items() if k.startswith("part_")}
