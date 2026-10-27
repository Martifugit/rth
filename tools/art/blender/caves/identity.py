"""The caves kit's identity sets: small kits of dressing that say what a place is, set on the
generic parts (parts.py). Each set is a group in IDENTITY; a new place's own set goes beside them.

- makers: the unfinished world's stonework (pale, twisted, too regular, the hand that set the
  heath's standing stones), weathered and mossed, one muted moonstone inlay per piece.
- mine: rails into the dark, a tub, a spoil heap, a lantern on its post.
- overgrown: roots hanging from the lip, ivy draped down the rock, ferns at the foot.
- shrine: an offering cairn, candles, a ribbon post where people tie their asks.
- lair: bones, a nest of sticks, hides stretched to dry.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import rth_art as A
import basin as N
from parts import DOOR_H, SPAN
from shapes import ROCK, STONE, added, along_x, block, chunk, lay, make, rng, twisted, weather

MAKER = "Gravestone"                       # the makers' pale stone
INLAY = "Moonstone"                        # their one inlaid band, muted moonlight
MAKER_MATS = [MAKER, INLAY, "Moss"]


# ---- the makers -------------------------------------------------------------------------------

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


def unfinished_door(seed=4, at=(0, 0, 0)):
    """A doorway caught half made: the left post a finished twisted column, the right one still raw
    rock with only its foot cut square; the lintel twists out from the left post as worked stone and
    turns into rough rock across the middle, where a seam of inlay marks where the making stopped."""
    r = rng(seed)
    bm = bmesh.new()
    twist = math.radians(70)
    half = SPAN / 2
    before = set(bm.verts)
    twisted(bm, -0.1, DOOR_H + 0.1, (0.24, 0.24), (0.19, 0.19), 0.0, twist, 0)
    lay(bm, added(bm, before), Matrix.Translation((-half, 0, 0)))
    block(bm, (half - 0.26, -0.24, -0.1), (half + 0.26, 0.24, 0.85), r, chip=0.02)          # the cut foot
    z = DOOR_H + 0.22
    before = set(bm.verts)
    twisted(bm, 0, half + 0.35, (0.24, 0.2), (0.24, 0.2), 0.0, math.radians(45), 0)
    along_x(bm, added(bm, before), -half - 0.3, z)
    weather(bm, r, 0.02, moss=4)
    chunk(bm, (half + 0.05, 0.02, 0.8), (0.36, 0.33, DOOR_H - 0.55), r, mats=(2, 3, 4))       # the raw rest
    chunk(bm, (0.85, 0.0, z - 0.3), (0.95, 0.3, 0.6), r, sink=0.0, mats=(2, 3, 4))
    A.box(bm, (0.03, -0.27, z - 0.27), (0.09, 0.27, z + 0.27), 1)                          # the seam
    return make("UnfinishedDoor", bm, [MAKER, INLAY, "Scree", "Cinder", "Moss"], at=at, smooth=20)


def seal_stone(seed=1, at=(0, 0, 0)):
    """The round stone that once sealed a doorway, rolled half aside: a thick disc on its edge,
    leaning, sunk a little, moss on its rim, a raised square on its face turned on its corner with a
    small inlay in it."""
    r = rng(seed)
    bm = bmesh.new()
    A.prism(bm, 12, 0.95, 0.95, -0.12, 0.12, (0, 0), mat_index=0, cap_top=True, cap_bottom=True)
    A.prism(bm, 4, 0.42, 0.38, 0.12, 0.2, (0, 0), rot=math.radians(45), mat_index=0, cap_top=True)
    A.prism(bm, 4, 0.14, 0.14, 0.2, 0.215, (0, 0), rot=math.radians(45), mat_index=1, cap_top=True)
    lay(bm, list(bm.verts), Matrix.Translation((0, 0, 0.82)) @ Matrix.Rotation(math.radians(-8), 4, 'Y')
        @ Matrix.Rotation(math.radians(90), 4, 'X'))
    weather(bm, r, 0.02, moss=2, up=0.8)
    return make("SealStone", bm, MAKER_MATS, at=at, smooth=30)


# ---- the mine ---------------------------------------------------------------------------------

def mine_rails(seed=1, at=(0, 0, 0)):
    """One tile of rails running into the dark (+Y): three sleepers, two iron rails on them."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(3):
        y = -0.5 + (k + 0.5) / 3
        block(bm, (-0.42, y - 0.08, -0.04), (0.42, y + 0.08, 0.05), r, chip=0.015)
    for x in (-0.25, 0.25):
        A.box(bm, (x - 0.025, -0.52, 0.05), (x + 0.025, 0.52, 0.11), 1)
    return make("MineRails", bm, ["Timber", "Iron"], at=at, smooth=20)


def mine_cart(seed=1, at=(0, 0, 0)):
    """A mine tub on the rails: a tapered iron-banded box on four small wheels, ore heaped in it."""
    r = rng(seed)
    bm = bmesh.new()
    A.hull(bm, [(x * w, y * d, z) for z, w, d in ((0.22, 0.32, 0.45), (0.75, 0.42, 0.58)) for x in (-1, 1) for y in (-1, 1)], mat=0)
    for z in (0.3, 0.68):
        A.hull(bm, [(x * (0.34 + 0.12 * (z - 0.22) / 0.53 + 0.015), y * (0.47 + 0.13 * (z - 0.22) / 0.53 + 0.015), z + dz)
                    for x in (-1, 1) for y in (-1, 1) for dz in (-0.03, 0.03)], mat=1)
    chunk(bm, (0, 0, 0.62), (0.36, 0.5, 0.3), r, sink=0.0, mats=(2, 2, 2), points=14)
    for x in (-0.25, 0.25):
        for y in (-0.32, 0.32):
            before = set(bm.verts)
            A.prism(bm, 8, 0.11, 0.11, -0.03, 0.03, (0, 0), mat_index=1, cap_top=True, cap_bottom=True)
            lay(bm, added(bm, before), Matrix.Translation((x * 1.25, y, 0.12)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    return make("MineCart", bm, ["Timber", "Iron", "Cinder"], at=at, smooth=30)


def spoil_heap(seed=3, at=(0, 0, 0)):
    """The spoil dug out of a mine and tipped by its mouth: a low dark heap of broken stone."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.9, 0.7, 0.5), r, mats=(0, 1, 0), points=22)
    for _ in range(10):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0.8, 1.2)
        s = r.uniform(0.06, 0.12)
        chunk(bm, (d * math.cos(a) * 1.1, d * math.sin(a) * 0.9, 0), (s, s, s * 0.8), r, sink=0.03, mats=(0, 1, 0), points=8)
    return make("SpoilHeap", bm, ["Cinder", "CryptDarkStone"], at=at)


def lantern_post(seed=2, at=(0, 0, 0)):
    """A lantern on a post by a mine's mouth: a crooked post, an arm, an iron lantern hanging from
    it with warm panes (the light itself is the client's)."""
    r = rng(seed)
    bm = bmesh.new()
    N.rod(bm, N.wobble(r, (0, 0, -0.05), (0.03, 0, 2.2), 3, 0.03), 0.06, sides=5, taper=0.85)
    N.rod(bm, [(0.0, 0, 2.05), (0.45, 0, 2.12)], 0.035, sides=4)
    N.rod(bm, [(0.42, 0, 2.1), (0.42, 0, 1.85)], 0.01, mat=1, sides=3)
    A.prism(bm, 4, 0.1, 0.1, 1.55, 1.82, (0.42, 0), rot=math.radians(45), mat_index=2, cap_top=False)
    A.prism(bm, 4, 0.13, 0.03, 1.82, 1.92, (0.42, 0), rot=math.radians(45), mat_index=1, cap_top=True)
    A.prism(bm, 4, 0.12, 0.12, 1.5, 1.56, (0.42, 0), rot=math.radians(45), mat_index=1, cap_top=True, cap_bottom=True)
    return make("LanternPost", bm, ["Timber", "Blackiron", "Ember"], at=at, smooth=30)


# ---- overgrown --------------------------------------------------------------------------------

def hanging_roots(seed=2, at=(0, 0, 0), height=2.25, span=1.6):
    """Roots hanging from under a lip across a mouth, thin and kinked, some long enough to brush a
    tall visitor's head."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(9):
        x = -span / 2 + span * (i + r.uniform(0.1, 0.9)) / 9
        y = r.uniform(-0.15, 0.15)
        length = r.uniform(0.35, 1.0)
        pts = [Vector((x, y, height + 0.1))]
        for k in range(1, 4):
            pts.append(pts[-1] + Vector((r.uniform(-0.08, 0.08), r.uniform(-0.05, 0.05), -length / 3)))
        N.rod(bm, pts, r.uniform(0.014, 0.026), sides=3, taper=0.4, tip=pts[-1] - Vector((0, 0, 0.08)))
    return make("HangingRoots", bm, ["Bark"], at=at, smooth=50)


def ivy_drape(seed=3, at=(0, 0, 0), height=2.4, span=1.4):
    """Ivy falling from a ledge at `height` down a rock face: strands of leaf cards hanging side by
    side, each its own length, bowed out a little from the face."""
    r = rng(seed)
    bm = bmesh.new()
    uvs = {}
    n = 4
    for k in range(n):
        x0 = -span / 2 + span * k / n + r.uniform(-0.05, 0.05)
        x1 = x0 + span / n * r.uniform(1.0, 1.25)
        drop = r.uniform(1.0, 1.7)
        bulge = r.uniform(0.05, 0.14)
        top = [bm.verts.new((x, -0.02, height)) for x in (x0, x1)]
        mid = [bm.verts.new((x, -0.02 - bulge, height - drop * 0.5)) for x in (x0, x1)]
        low = [bm.verts.new((x, -0.02 - bulge * 0.6, height - drop)) for x in (x0, x1)]
        f = bm.faces.new((mid[0], mid[1], top[1], top[0]))
        uvs[f] = [(0, 0.5), (1, 0.5), (1, 1), (0, 1)]
        f = bm.faces.new((low[0], low[1], mid[1], mid[0]))
        uvs[f] = [(0, 0), (1, 0), (1, 0.5), (0, 0.5)]
    return N.leafy("IvyDrape", bm, uvs, [N.leaf_mat("Ivy")], at, smooth=60)


def fern(seed=4, at=(0, 0, 0)):
    """A fern at the foot of the rock where the damp comes out: fronds arching up and over, each a
    stem with leaflets along it."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(7):
        a = 2 * math.pi * k / 7 + r.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-out.y, out.x, 0))
        length = r.uniform(0.5, 0.75)
        pts = [Vector((0, 0, 0.02)), out * length * 0.35 + Vector((0, 0, length * 0.55)),
               out * length * 0.8 + Vector((0, 0, length * 0.5)), out * length + Vector((0, 0, length * 0.2))]
        for i in range(len(pts) - 1):
            w = 0.09 * (1 - i / 3)
            a0, a1 = pts[i], pts[i + 1]
            v = [bm.verts.new(p) for p in (a0 - side * w, a0 + side * w, a1 + side * w * 0.6, a1 - side * w * 0.6)]
            bm.faces.new(v).material_index = 0
    return make("Fern", bm, ["Leaf4"], at=at, smooth=50)


# ---- the shrine -------------------------------------------------------------------------------

def offering(seed=3, at=(0, 0, 0)):
    """An offering left at a mouth: four flat stones stacked askew, a guttered candle stub on top
    with a small flame, a scatter of pale chips (bone, or shell) at the foot."""
    r = rng(seed)
    bm = bmesh.new()
    z, rad = 0.0, 0.26
    for _ in range(4):
        h = r.uniform(0.06, 0.09)
        N.slab(bm, (r.uniform(-0.02, 0.02), r.uniform(-0.02, 0.02), z), rad, h, 6, r, rot=r.uniform(0, 3))
        z += h - 0.005
        rad *= 0.8
    N.rod(bm, [(0, 0, z - 0.01), (0.005, 0, z + 0.09)], 0.03, mat=2, sides=5)
    A.spike(bm, (0.005, 0, z + 0.085), 0.018, (0.01, 0, z + 0.16), sides=4, mat=3)
    for _ in range(5):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0.3, 0.5)
        c = Vector((d * math.cos(a), d * math.sin(a), 0))
        A.hull(bm, [c + Vector((0.035 * math.cos(t), 0.02 * math.sin(t), -0.01)) for t in (0, 2.1, 4.2)] +
               [c + Vector((0, 0, 0.025))], mat=2)
    return make("Offering", bm, ["Scree", "Gravestone", "CandleWax", "Ember"], at=at, smooth=30)


def candles(seed=5, at=(0, 0, 0)):
    """Candles burned down on a flat stone by a door, wax run over its edge, a few still lit."""
    r = rng(seed)
    bm = bmesh.new()
    N.slab(bm, (0, 0, -0.03), 0.35, 0.1, 7, r, rot=0.3)
    for k in range(7):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0.0, 0.24)
        x, y = d * math.cos(a), d * math.sin(a)
        h = r.uniform(0.05, 0.22)
        N.rod(bm, [(x, y, 0.06), (x, y, 0.06 + h)], r.uniform(0.022, 0.035), mat=2, sides=5)
        if k < 4:
            A.spike(bm, (x, y, 0.06 + h - 0.005), 0.014, (x, y, 0.06 + h + 0.06), sides=4, mat=3)
    return make("Candles", bm, ["Scree", "Gravestone", "CandleWax", "Ember"], at=at, smooth=30)


def ribbon_post(seed=6, at=(0, 0, 0)):
    """A stake by a holy mouth where people tie their asks: strips of pale and dyed cloth knotted
    to it and its crossbar, hanging still."""
    r = rng(seed)
    bm = bmesh.new()
    N.rod(bm, N.wobble(r, (0, 0, -0.05), (0.02, 0, 1.6), 3, 0.03), 0.05, sides=5, taper=0.85)
    N.rod(bm, [(-0.4, 0, 1.35), (0.42, 0, 1.38)], 0.03, sides=4)
    for k in range(9):
        x = r.uniform(-0.38, 0.38)
        top = 1.35 + 0.03 * (x + 0.4) / 0.8
        length = r.uniform(0.25, 0.6)
        w = r.uniform(0.03, 0.05)
        y = r.uniform(-0.02, 0.02)
        v = [bm.verts.new(p) for p in ((x - w, y - 0.035, top), (x + w, y - 0.035, top),
                                         (x + w * 0.7 + r.uniform(-0.03, 0.03), y - 0.05, top - length),
                                         (x - w * 0.7, y - 0.05, top - length))]
        bm.faces.new(v).material_index = 1 + k % 2
    return make("RibbonPost", bm, ["Driftwood", "Linen", "Russet"], at=at, smooth=50)


# ---- the lair ---------------------------------------------------------------------------------

def bone_pile(seed=7, at=(0, 0, 0)):
    """Bones dragged out of a lair and left in a heap: long bones with knobbed ends crossed every
    way, a skull among them."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(9):
        a = r.uniform(0, math.pi)
        c = Vector((r.uniform(-0.35, 0.35), r.uniform(-0.3, 0.3), 0.04 + 0.03 * k))
        d = Vector((math.cos(a), math.sin(a), r.uniform(-0.15, 0.15))) * r.uniform(0.18, 0.3)
        N.rod(bm, [c - d, c + d], 0.025, sides=4)
        for e in (c - d, c + d):
            A.loft(bm, [(e - d.normalized() * 0.03, 0, 0), (e, 0.045, 0.045), (e + d.normalized() * 0.04, 0, 0)],
                   sides=4, ref=(0, 0, 1))
    skull = Vector((0.15, -0.25, 0.12))
    A.loft(bm, [(skull + Vector((0, 0.12, 0)), 0, 0), (skull + Vector((0, 0.06, 0)), 0.1, 0.09),
                (skull + Vector((0, -0.06, -0.01)), 0.09, 0.08), (skull + Vector((0, -0.13, -0.03)), 0.05, 0.05),
                (skull + Vector((0, -0.15, -0.03)), 0, 0)], sides=6, ref=(0, 0, 1))
    for s in (-1, 1):
        A.paint(bm, skull + Vector((s * 0.04, -0.1, 0.02)), 1, facing=(0, -1, 0.3), size=0.6)
    return make("BonePile", bm, ["SaintBone", "Sable"], at=at, smooth=40)


def nest(seed=8, at=(0, 0, 0)):
    """A nest of sticks and dry grass on the ground inside a mouth, big enough to make a visitor
    wonder what lies in it."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(26):
        a = r.uniform(0, 2 * math.pi)
        rad = r.uniform(0.55, 0.8)
        c = Vector((rad * math.cos(a), rad * math.sin(a), r.uniform(0.04, 0.22)))
        t = Vector((-math.sin(a), math.cos(a), r.uniform(-0.3, 0.3))) * r.uniform(0.25, 0.45)
        N.rod(bm, [c - t, c + t], r.uniform(0.018, 0.03), mat=k % 2, sides=3)
    A.prism(bm, 8, 0.6, 0.6, 0.0, 0.05, (0, 0), mat_index=2, cap_top=True)
    return make("Nest", bm, ["Bark", "Driftwood", "Wheat"], at=at, smooth=40)


def hide_rack(seed=9, at=(0, 0, 0)):
    """Hides stretched to dry on a pole frame: two forked posts, a pole across, three skins hung
    over it, ragged at their ends."""
    r = rng(seed)
    bm = bmesh.new()
    for x in (-0.9, 0.9):
        N.rod(bm, N.wobble(r, (x, 0, -0.05), (x, 0, 1.45), 3, 0.03), 0.05, sides=5, taper=0.85)
    N.rod(bm, [(-1.05, 0, 1.42), (1.05, 0, 1.45)], 0.04, sides=5)
    for k, x in enumerate((-0.55, 0.0, 0.55)):
        w = r.uniform(0.2, 0.28)
        front, back = r.uniform(0.7, 1.0), r.uniform(0.5, 0.8)
        vs = [bm.verts.new(p) for p in ((x - w, -0.03, 1.47), (x + w, -0.03, 1.47),
                                          (x + w * 1.15, -0.1, 1.47 - front), (x - w * 0.9, -0.08, 1.47 - front - 0.08),
                                          (x - w * 0.9, 0.08, 1.47 - back), (x + w * 1.1, 0.1, 1.47 - back + 0.06))]
        bm.faces.new((vs[0], vs[1], vs[2], vs[3])).material_index = 1 + k % 2
        bm.faces.new((vs[1], vs[0], vs[4], vs[5])).material_index = 1 + k % 2
    return make("HideRack", bm, ["Driftwood", "Hide", "Umber"], at=at, smooth=50)


# Name in the game (Resources/Caves/<Name>) to builder, by set.
IDENTITY = {
    "makers": {
        "MakerPillar": maker_pillar, "MakerPillarBroken": lambda at=(0, 0, 0): maker_pillar(at=at, broken=True),
        "MakerLintel": maker_lintel, "MakerThreshold": maker_threshold, "UnfinishedDoor": unfinished_door,
        "SealStone": seal_stone,
    },
    "mine": {"MineRails": mine_rails, "MineCart": mine_cart, "SpoilHeap": spoil_heap, "LanternPost": lantern_post},
    "overgrown": {"HangingRoots": hanging_roots, "IvyDrape": ivy_drape, "Fern": fern},
    "shrine": {"Offering": offering, "Candles": candles, "RibbonPost": ribbon_post},
    "lair": {"BonePile": bone_pile, "Nest": nest, "HideRack": hide_rack},
}
