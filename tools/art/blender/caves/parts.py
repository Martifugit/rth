"""The caves kit's generic layer: rock, openings, dressed stone, timber and iron, neutral enough
to build any entrance and to take a place's identity from the sets on top (identity.py).

Conventions: every part faces Blender -Y (the front, Unity +Z) with its origin on the ground at its
middle. A doorway is SPAN wide between its posts' centres; spanning parts (lips, lintels, arches)
carry their own height and sit on posts DOOR_H high. Dressed stone is the palette's Stone, so a
place retints it (objects.json "recolor"); rock is Scree with cinder undersides and mossy ledges.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import rth_art as A
import basin as N
from shapes import DARK, ROCK, STONE, added, along_x, block, chunk, hole, lay, make, rng, weather

SPAN = 2.5          # between a doorway's post centres
DOOR_H = 2.6        # posts' height; lintels and arches start here


# ---- rock -------------------------------------------------------------------------------------

def rock_mass(seed=1, at=(0, 0, 0), kind="big"):
    """A rock mass to build hillsides and cave sides from, two or three chunks riding on each other.
    kind: big (2 x 1.5 tiles, 3 high), small (1 x 1, 2.2), wide (a low shoulder 2.5 long), tall (a
    sheer face 1.2 wide, 3.5 high)."""
    r = rng(seed)
    bm = bmesh.new()
    if kind == "big":
        chunk(bm, (0, 0, 0), (1.0, 0.75, 1.7), r)
        chunk(bm, (r.uniform(-0.45, -0.2), 0.1, 0.9), (0.62, 0.55, 2.0), r, lean=(r.uniform(-0.12, 0.0), 0.05))
        chunk(bm, (r.uniform(0.25, 0.5), 0.2, 0.5), (0.55, 0.5, 1.6), r, lean=(0.08, 0.04))
    elif kind == "small":
        chunk(bm, (0, 0, 0), (0.55, 0.5, 1.3), r)
        chunk(bm, (0.08, 0.08, 0.7), (0.38, 0.36, 1.4), r, lean=(r.uniform(-0.1, 0.1), 0.04))
    elif kind == "wide":
        chunk(bm, (-0.6, 0, 0), (0.8, 0.6, 1.2), r)
        chunk(bm, (0.55, 0.1, 0), (0.75, 0.55, 1.0), r)
        chunk(bm, (0.0, 0.2, 0.6), (0.6, 0.45, 0.9), r)
    else:
        chunk(bm, (0, 0, 0), (0.6, 0.45, 2.2), r)
        chunk(bm, (0.05, 0.05, 1.8), (0.45, 0.38, 1.7), r, lean=(r.uniform(-0.06, 0.06), 0.03))
    return make("RockMass" + {"big": "", "small": "Small", "wide": "Wide", "tall": "Tall"}[kind], bm, ROCK, at=at)


def rock_spire(seed=3, at=(0, 0, 0)):
    """A tall leaning tooth of rock, for a cleft, or to stand guard by a mouth."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.45, 0.4, 1.6), r)
    chunk(bm, (0.05, 0.02, 1.3), (0.28, 0.25, 1.9), r, lean=(0.12, 0.04))
    return make("RockSpire", bm, ROCK, at=at)


def rock_lip(seed=5, at=(0, 0, 0), height=2.2, long=False):
    """An overhang across a mouth: a flat slab of rock lying at `height`, its ends resting on the
    masses either side; 2.4 tiles long, or 3.4 (long). Moss on top, shadow under."""
    r = rng(seed)
    bm = bmesh.new()
    half = 1.7 if long else 1.2
    chunk(bm, (0, 0.1, height), (half, 0.7, 0.55), r, sink=0.0)
    chunk(bm, (r.uniform(-0.4, 0.4) * half, 0.25, height + 0.35), (half * 0.55, 0.5, 0.45), r, sink=0.0)
    return make("RockLipLong" if long else "RockLip", bm, ROCK, at=at)


def rock_slab(seed=6, at=(0, 0, 0)):
    """A flat slab lying tilted on the ground, a ledge or a step up to a mouth."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.65, 0.45, 0.35), r)
    lay(bm, list(bm.verts), Matrix.Rotation(math.radians(r.uniform(5, 10)), 4, Vector((1, 0.4, 0)).normalized()))
    return make("RockSlab", bm, ROCK, at=at)


def boulder(seed=8, at=(0, 0, 0)):
    """A single boulder, about a tile across."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.5, 0.42, 0.8), r, points=18)
    return make("Boulder", bm, ROCK, at=at)


def rubble(seed=7, at=(0, 0, 0)):
    """Fallen stone at the foot of a face: a spill of chunks, the biggest at the back."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(8):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0, 0.75) * (0.5 if i < 2 else 1)
        s = r.uniform(0.18, 0.32) if i < 3 else r.uniform(0.08, 0.16)
        chunk(bm, (d * math.cos(a), d * math.sin(a) * 0.6 + 0.2, 0), (s, s * 0.9, s * 1.1), r, sink=0.05, points=12)
    return make("Rubble", bm, ROCK, at=at)


def scree(seed=9, at=(0, 0, 0)):
    """Scree fanning out from the foot of a face (+Y is the face): many small stones, thicker at
    the back, thinning out forwards."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(22):
        t = r.random() ** 1.6
        x, y = r.uniform(-1.0, 1.0) * (0.4 + t), 0.6 - t * 1.4
        s = r.uniform(0.06, 0.14) * (1.3 - t * 0.7)
        chunk(bm, (x, y, 0), (s, s * 0.9, s * 0.8), r, sink=0.04, points=8)
    return make("Scree", bm, ROCK, at=at)


# ---- openings ---------------------------------------------------------------------------------

THROATS = {
    # outline (half widths, heights 0..1), half width, height
    "wide": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 0.55), (0.8, 0.85), (0.4, 1.0), (-0.35, 1.0), (-0.8, 0.85), (-1.0, 0.55)], 1.0, 2.1),
    "cleft": ([(-1.0, 0.0), (1.0, 0.0), (0.9, 0.45), (0.45, 0.85), (0.05, 1.0), (-0.5, 0.75), (-0.95, 0.4)], 0.45, 2.5),
    "low": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 0.5), (0.6, 0.9), (-0.5, 0.95), (-1.0, 0.55)], 0.55, 0.95),
    "square": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 1.0), (-1.0, 1.0)], 0.95, 2.6),
    "squeeze": ([(-1.0, 0.0), (1.0, 0.0), (0.8, 0.35), (1.0, 0.7), (0.3, 1.0), (-0.6, 0.8), (-1.0, 0.45)], 0.22, 2.1),
    "burrow": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 0.5), (0.55, 0.95), (-0.55, 0.95), (-1.0, 0.5)], 0.32, 0.55),
}
# How far each ring of a throat is shifted sideways: a squeeze bends, so its end can't be seen.
BENDS = {"squeeze": [0.0, 0.12, 0.3, 0.2, 0.0]}


def throat(seed=1, at=(0, 0, 0), kind="wide"):
    """The darkness behind a mouth: a tunnel into the hill (+Y) on a level floor (the ground would
    cover anything lower), narrowing as its roof comes down, its faces darkening from the rim to
    black. Seen from inside only (its faces point in), so it disappears wherever nothing covers it;
    set anything behind it at least 2.3 tiles back from its origin. kind: wide (a cave), cleft (a
    tall crack), low (a crawl hole), square (behind a built doorway), squeeze (a crack barely a body
    wide, bending as it goes), burrow (a body's width, into an earth bank)."""
    r = rng(seed)
    bm = bmesh.new()
    outline, width, height = THROATS[kind]
    steps = [(-0.15, 1.0, 1.0), (0.5, 0.9, 0.8), (1.1, 0.8, 0.6), (1.7, 0.7, 0.42), (2.2, 0.6, 0.3)]
    jitter = 0.0 if kind == "square" else 0.05
    rings = []
    bend = BENDS.get(kind, [0.0] * len(steps))
    for (y, scale, roof), dx in zip(steps, bend):
        rings.append([bm.verts.new((dx + x * width * scale * r.uniform(1 - jitter, 1 + jitter), y,
                                    0.02 + z * height * roof * r.uniform(1 - jitter, 1 + jitter))) for x, z in outline])
    n = len(outline)
    for i, (r0, r1) in enumerate(zip(rings, rings[1:])):
        for k in range(n):
            bm.faces.new((r0[k], r1[k], r1[(k + 1) % n], r0[(k + 1) % n])).material_index = 0 if i == 0 else 1
    end = bm.faces.new(rings[-1])
    end.material_index = 1
    for f in bm.faces:                                   # every face looks inward
        f.normal_update()
        mid = f.calc_center_median()
        inward = Vector((0, -1, 0)) if f is end else Vector((0, mid.y, mid.z * 0.5 + 0.3)) - mid
        if f.normal.dot(inward) < 0:
            f.normal_flip()
    name = "Throat" + {"wide": "", "cleft": "Cleft", "low": "Low", "square": "Square", "squeeze": "Squeeze",
                       "burrow": "Burrow"}[kind]
    return make(name, bm, DARK, at=at, smooth=60, finish=False)


# ---- dressed stone ----------------------------------------------------------------------------

def post(seed=1, at=(0, 0, 0), round_=False, broken=False):
    """A doorpost DOOR_H high: a square shaft (or a round column) on a base block, under a cap.
    broken: snapped at chest height, its top lying at its foot."""
    r = rng(seed)
    bm = bmesh.new()
    top = 1.3 if broken else DOOR_H - 0.18
    block(bm, (-0.32, -0.32, -0.1), (0.32, 0.32, 0.22), r)
    if round_:
        A.prism(bm, 8, 0.24, 0.22, 0.2, top, (0, 0), mat_index=0, cap_top=True)
    else:
        block(bm, (-0.23, -0.23, 0.2), (0.23, 0.23, top), r, chip=0.015)
    if broken:
        for v in bm.verts:
            if v.co.z > top - 0.01:
                v.co.z += r.uniform(-0.18, 0.06)
        before = set(bm.verts)
        block(bm, (-0.23, -0.23, 0), (0.23, 0.23, 1.0), r)
        lay(bm, added(bm, before), Matrix.Translation((0.55, -0.45, 0.23)) @ Matrix.Rotation(math.radians(84), 4, Vector((0.3, 1, 0)).normalized()))
    else:
        block(bm, (-0.3, -0.3, top), (0.3, 0.3, DOOR_H), r)
    weather(bm, r, 0.012, moss=1, up=0.9)
    name = ("Column" if round_ else "Post") + ("Broken" if broken else "")
    return make(name, bm, [STONE, "Moss"], at=at, smooth=30 if round_ else 20)


def lintel(seed=1, at=(0, 0, 0), long=False):
    """A plain lintel across two posts SPAN apart (or 1.5 x SPAN, long), lying on their caps."""
    r = rng(seed)
    bm = bmesh.new()
    half = (SPAN * (1.5 if long else 1.0)) / 2 + 0.35
    block(bm, (-half, -0.26, DOOR_H), (half, 0.26, DOOR_H + 0.48), r, chip=0.03)
    weather(bm, r, 0.012, moss=1, up=0.9)
    return make("LintelLong" if long else "Lintel", bm, [STONE, "Moss"], at=at, smooth=20)


def arch(seed=1, at=(0, 0, 0), broken=False):
    """A round arch over a doorway SPAN wide: jambs of three stacked blocks each, and seven
    wedge-shaped voussoirs springing from them. broken: the keystone and its neighbour fallen, lying
    at the foot."""
    r = rng(seed)
    bm = bmesh.new()
    spring = 1.6
    inner, outer, d = SPAN / 2 - 0.25, SPAN / 2 + 0.3, 0.3
    for s in (-1, 1):
        for k in range(3):
            z0 = spring * k / 3
            block(bm, (s * SPAN / 2 - 0.3, -d, z0 - (0.1 if k == 0 else 0)), (s * SPAN / 2 + 0.3, d, z0 + spring / 3 - 0.01), r)
    n = 7
    skip = {3, 4} if broken else set()
    for k in range(n):
        if k in skip:
            continue
        a0, a1 = math.pi * k / n + 0.01, math.pi * (k + 1) / n - 0.01
        pts = []
        for a in (a0, a1):
            for rad in (inner, outer):
                for y in (-d, d):
                    pts.append((math.cos(math.pi - a) * rad, y, spring + math.sin(a) * rad))
        A.hull(bm, pts, mat=0)
    if broken:
        for k, (x, y, a) in enumerate(((0.9, -1.1, 0.4), (-0.4, -1.4, 1.9))):
            before = set(bm.verts)
            block(bm, (-0.28, -0.3, 0), (0.28, 0.3, 0.5), r)
            lay(bm, added(bm, before), Matrix.Translation((x, y, 0.05)) @ Matrix.Rotation(a, 4, 'Z')
                @ Matrix.Rotation(math.radians(20), 4, 'X'))
    weather(bm, r, 0.015, moss=1, up=0.85)
    return make("ArchBroken" if broken else "Arch", bm, [STONE, "Moss"], at=at, smooth=20)


def wall_span(seed=1, at=(0, 0, 0)):
    """One tile of coursed wall along X, DOOR_H high, to face a hillside either side of a built
    doorway: four courses of blocks, each course's joints offset, the top course ragged."""
    r = rng(seed)
    bm = bmesh.new()
    course = DOOR_H / 4
    for k in range(4):
        z0 = course * k - (0.1 if k == 0 else 0)
        cut = r.uniform(-0.15, 0.15) + (0.25 if k % 2 else -0.25)
        for x0, x1 in ((-0.5, cut), (cut, 0.5)):
            top = course * (k + 1) - 0.01 - (r.uniform(0, 0.15) if k == 3 else 0)
            block(bm, (x0 + 0.005, -0.25, z0), (x1 - 0.005, 0.25, top), r, chip=0.02)
    weather(bm, r, 0.01, moss=1, up=0.9)
    return make("WallSpan", bm, [STONE, "Moss"], at=at, smooth=20)


def steps(seed=1, at=(0, 0, 0), wide=True):
    """Three steps rising towards a doorway (+Y), the top one running on as its threshold; 2.2
    wide, or 1.2."""
    r = rng(seed)
    bm = bmesh.new()
    w = 1.1 if wide else 0.6
    for k in range(3):
        y0 = -0.9 + 0.38 * k
        block(bm, (-w + 0.04 * k, y0, -0.1), (w - 0.04 * k, 0.6 if k == 2 else y0 + 0.4, 0.14 * (k + 1)), r, chip=0.015)
    weather(bm, r, 0.008)
    return make("Steps" if wide else "StepsNarrow", bm, [STONE], at=at, smooth=20)


def plinth(seed=2, at=(0, 0, 0)):
    """A squat plinth, for whatever a place sets before its door (a brazier, a statue, a bowl)."""
    r = rng(seed)
    bm = bmesh.new()
    block(bm, (-0.45, -0.45, -0.1), (0.45, 0.45, 0.25), r)
    block(bm, (-0.36, -0.36, 0.25), (0.36, 0.36, 0.75), r)
    block(bm, (-0.42, -0.42, 0.75), (0.42, 0.42, 0.88), r)
    weather(bm, r, 0.01, moss=1, up=0.9)
    return make("Plinth", bm, [STONE, "Moss"], at=at, smooth=20)


def masonry_rubble(seed=4, at=(0, 0, 0)):
    """Fallen masonry: dressed blocks tumbled in a heap, some half sunk, chips round them."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(6):
        before = set(bm.verts)
        s = r.uniform(0.18, 0.3)
        block(bm, (-s * 1.4, -s, 0), (s * 1.4, s, s * 1.6), r)
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0, 0.6)
        lay(bm, added(bm, before), Matrix.Translation((d * math.cos(a), d * math.sin(a), -0.05 + (0.25 if i == 5 else 0)))
            @ Matrix.Rotation(r.uniform(0, 3), 4, 'Z') @ Matrix.Rotation(r.uniform(-0.4, 0.4), 4, 'X'))
    weather(bm, r, 0.015, moss=1, up=0.9)
    return make("MasonryRubble", bm, [STONE, "Moss"], at=at, smooth=20)


# ---- timber -----------------------------------------------------------------------------------

def timber_frame(seed=2, at=(0, 0, 0)):
    """A doorway or adit framed in timber: two posts leaning in a little under a cap beam, a wedge
    driven in at each joint."""
    r = rng(seed)
    bm = bmesh.new()
    top = 2.05
    for s in (-1, 1):
        N.rod(bm, N.wobble(r, (s * 0.95, 0, -0.05), (s * 0.82, 0, top), 3, 0.03), 0.08, sides=6, taper=0.9)
        A.hull(bm, [(s * 0.7, -0.06, top - 0.12), (s * 0.7, 0.06, top - 0.12), (s * 0.74, 0, top - 0.02),
                    (s * 0.66, -0.06, top - 0.02), (s * 0.66, 0.06, top - 0.02)])
    N.rod(bm, N.wobble(r, (-1.15, 0, top + 0.06), (1.15, 0, top + 0.08), 4, 0.03), 0.09, sides=6)
    return make("TimberFrame", bm, ["Timber"], at=at, smooth=50)


def door(seed=1, at=(0, 0, 0), ajar=False):
    """A plank door for a built doorway, two battens and two iron straps across it, hung on its left
    edge. ajar: swung open into the dark."""
    r = rng(seed)
    bm = bmesh.new()
    w, h = 0.95, 2.2
    for k in range(6):
        x0 = -w + 2 * w * k / 6
        block(bm, (x0 + 0.005, -0.04, 0.0), (x0 + 2 * w / 6 - 0.005, 0.04, h - r.uniform(0, 0.04)), r, chip=0.008)
    for z in (0.45, 1.75):
        block(bm, (-w + 0.05, 0.04, z), (w - 0.05, 0.1, z + 0.18), r, chip=0.008)
        block(bm, (-w, -0.07, z + 0.04), (w * 0.6, -0.04, z + 0.12), r, mat=1, chip=0.0)
    if ajar:
        lay(bm, list(bm.verts), Matrix.Translation((-w, 0, 0)) @ Matrix.Rotation(math.radians(-70), 4, 'Z')
            @ Matrix.Translation((w, 0, 0)))
    return make("DoorAjar" if ajar else "Door", bm, ["Planks", "Blackiron"], at=at, smooth=20)


def boarded(seed=3, at=(0, 0, 0)):
    """An opening boarded up: rough planks nailed across it every way, one hanging loose."""
    r = rng(seed)
    bm = bmesh.new()
    boards = [(-1.2, 0.6, 1.2, 0.9), (-1.2, 1.6, 1.2, 1.3), (-1.1, 0.2, 1.1, 2.0), (-1.2, 2.1, 1.1, 2.2)]
    for k, (x0, z0, x1, z1) in enumerate(boards):
        before = set(bm.verts)
        length = math.hypot(x1 - x0, z1 - z0)
        block(bm, (-length / 2, -0.03, -0.1), (length / 2, 0.03, 0.1), r, chip=0.01)
        lay(bm, added(bm, before), Matrix.Translation(((x0 + x1) / 2, -0.06 - 0.035 * k, (z0 + z1) / 2))   # each over the last
            @ Matrix.Rotation(-math.atan2(z1 - z0, x1 - x0), 4, 'Y'))
    before = set(bm.verts)
    block(bm, (-0.6, -0.03, -0.09), (0.6, 0.03, 0.09), r, chip=0.01)
    lay(bm, added(bm, before), Matrix.Translation((0.75, -0.12, 0.95)) @ Matrix.Rotation(math.radians(-62), 4, 'Y'))
    return make("Boarded", bm, ["Planks"], at=at, smooth=20)


# ---- iron -------------------------------------------------------------------------------------

def gate(seed=1, at=(0, 0, 0), open_=False):
    """An iron gate for a doorway SPAN wide: upright bars with spiked tops through three rails, hung
    on its left edge. open_: swung half open."""
    r = rng(seed)
    bm = bmesh.new()
    w, h = 1.0, 2.3
    for k in range(9):
        x = -w + 2 * w * (k + 0.5) / 9
        N.rod(bm, [(x, 0, 0.0), (x, 0, h)], 0.022, sides=4, tip=(x, 0, h + 0.14))
    for z in (0.15, 1.2, h - 0.15):
        N.rod(bm, [(-w, 0, z), (w, 0, z)], 0.03, sides=4)
    for x in (-w, w):
        N.rod(bm, [(x, 0, -0.05), (x, 0, h)], 0.04, sides=4)
    if open_:
        lay(bm, list(bm.verts), Matrix.Translation((-w, 0, 0)) @ Matrix.Rotation(math.radians(-55), 4, 'Z')
            @ Matrix.Translation((w, 0, 0)))
    return make("GateOpen" if open_ else "Gate", bm, ["Blackiron"], at=at, smooth=40)


def grate(seed=2, at=(0, 0, 0)):
    """A grate set flush in the ground, a stone kerb round iron bars, black below: a drain, a vent,
    or a way down that is not for people."""
    r = rng(seed)
    bm = bmesh.new()
    for x0, y0, x1, y1 in ((-0.7, -0.7, 0.7, -0.5), (-0.7, 0.5, 0.7, 0.7), (-0.7, -0.5, -0.5, 0.5), (0.5, -0.5, 0.7, 0.5)):
        block(bm, (x0, y0, -0.1), (x1, y1, 0.06), r, chip=0.01)
    for k in range(5):
        y = -0.5 + (k + 0.5) / 5
        N.rod(bm, [(-0.52, y, 0.03), (0.52, y, 0.03)], 0.025, mat=1, sides=4)
    floor = [bm.verts.new(p) for p in ((-0.5, -0.5, -0.06), (0.5, -0.5, -0.06), (0.5, 0.5, -0.06), (-0.5, 0.5, -0.06))]
    bm.faces.new(floor).material_index = 2
    return make("Grate", bm, [STONE, "Blackiron", "CryptEarth"], at=at, smooth=20)


# ---- descents: holes, and the rigging for going down them -------------------------------------

EARTH = ["Grass", "Dirt", "Mud", "CryptDarkStone", "CryptEarth"]   # a hole's outside, crest, walls, deep walls, floor


def outline_round(r, n, rnd, jit, stretch=(1.0, 1.0)):
    """A ragged ring of points round the origin, r across, stretched (x, y)."""
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n + rnd.uniform(-0.15, 0.15)
        rr = r * rnd.uniform(1 - jit, 1 + jit)
        pts.append((math.cos(a) * rr * stretch[0], math.sin(a) * rr * stretch[1]))
    return pts


def pit(seed=1, at=(0, 0, 0)):
    """A pit in the turf, about two tiles across: an earth rim, walls dropping into the dark. For a
    rope (RopeAnchor) or a ladder (LadderTop), or a jump. A few stones on its lip."""
    r = rng(seed)
    bm = bmesh.new()
    hole(bm, outline_round(0.95, 10, r, 0.1), 0.35, r)
    for k in range(3):
        a = r.uniform(0, 2 * math.pi)
        chunk(bm, (1.25 * math.cos(a), 1.25 * math.sin(a), 0.1), (0.16, 0.14, 0.22), r, mats=(5, 6, 7), points=10)
    return make("Pit", bm, EARTH + ROCK, at=at)


def sinkhole(seed=2, at=(0, 0, 0)):
    """Ground that has fallen in: a wide ragged hole, its lip torn, two slabs of turf sliding in."""
    r = rng(seed)
    bm = bmesh.new()
    c = hole(bm, outline_round(1.5, 13, r, 0.28), 0.3, r, rim_w=0.6)
    for k, a in enumerate((0.6, 3.6)):
        before = set(bm.verts)
        chunk(bm, (0, 0, 0), (0.45, 0.3, 0.22), r, mats=(1, 2, 0), points=12)
        out = Vector((math.cos(a), math.sin(a), 0))
        lay(bm, added(bm, before), Matrix.Translation(out * 1.05 + Vector((0, 0, 0.12)))
            @ Matrix.Rotation(a + math.pi / 2, 4, 'Z') @ Matrix.Rotation(math.radians(28), 4, 'X'))
    return make("Sinkhole", bm, EARTH, at=at)


def fissure(seed=3, at=(0, 0, 0)):
    """A crack in the ground, three tiles long and barely a body wide, rock breaking through the turf
    along both lips."""
    r = rng(seed)
    bm = bmesh.new()
    pts = []
    for k in range(14):
        a = 2 * math.pi * k / 14
        pts.append((math.cos(a) * 1.5 * r.uniform(0.9, 1.05), math.sin(a) * 0.35 + math.sin(math.cos(a) * 4) * 0.12))
    hole(bm, pts, 0.4, r, rim_w=0.35)
    for k in range(5):
        x = -1.3 + 0.65 * k
        side = 1 if k % 2 else -1
        chunk(bm, (x, side * 0.62, 0), (0.28, 0.22, r.uniform(0.35, 0.6)), r, points=12)
    return make("Fissure", bm, ["Scree", "Moss", "Cinder", "CryptDarkStone", "CryptEarth"], at=at)


def shaft(seed=4, at=(0, 0, 0)):
    """A shaft going straight down, built: a square kerb of stone blocks round a dark drop. For a
    rope, a ladder, or a windlass (Windlass)."""
    r = rng(seed)
    bm = bmesh.new()
    hole(bm, [(-0.7, -0.7), (0.7, -0.7), (0.7, 0.7), (-0.7, 0.7)], 0.5, r, rim_w=0.05, rings=3)
    for x0, y0, x1, y1 in ((-0.95, -0.95, 0.0, -0.7), (0.0, -0.95, 0.95, -0.7), (-0.95, 0.7, 0.0, 0.95),
                           (0.0, 0.7, 0.95, 0.95), (-0.95, -0.7, -0.7, 0.7), (0.7, -0.7, 0.95, 0.7)):
        block(bm, (x0 + 0.005, y0 + 0.005, -0.1), (x1 - 0.005, y1 - 0.005, 0.5), r, mat=0, chip=0.02)
    weather(bm, r, 0.008)
    return make("Shaft", bm, [STONE, STONE, "CryptDarkStone", "CryptEarth", "CryptEarth"], at=at, smooth=20)


def stairwell(seed=5, at=(0, 0, 0)):
    """Steps going down into the ground (+Y), built: a kerb round a long opening, a low sill to step
    over, six steps dropping inside it, the lower ones lost in the dark."""
    r = rng(seed)
    bm = bmesh.new()
    top = 0.45
    for x0, y0, x1, y1, h in ((-0.75, -1.25, 0.75, -1.05, 0.25), (-0.75, -1.05, -0.55, 1.05, top),
                              (0.55, -1.05, 0.75, 1.05, top), (-0.75, 1.05, 0.75, 1.25, top)):
        block(bm, (x0, y0, -0.1), (x1, y1, h), r, mat=0, chip=0.02)
    for k in range(6):
        y0 = -1.05 + 0.28 * k
        z = 0.4 - 0.066 * k
        block(bm, (-0.55, y0, -0.1), (0.55, y0 + 0.3, z), r, mat=1 if k < 3 else 2, chip=0.01)
    for (x0, y0), (x1, y1) in (((-0.55, -1.05), (-0.55, 1.05)), ((0.55, 1.05), (0.55, -1.05)), ((-0.55, 1.05), (0.55, 1.05))):
        v = [bm.verts.new(p) for p in ((x0, y0, 0.02), (x1, y1, 0.02), (x1, y1, top), (x0, y0, top))]
        bm.faces.new(v).material_index = 2
    floor = [bm.verts.new(p) for p in ((-0.55, 0.63, 0.02), (0.55, 0.63, 0.02), (0.55, 1.05, 0.02), (-0.55, 1.05, 0.02))]
    bm.faces.new(floor).material_index = 3
    weather(bm, r, 0.006)
    return make("Stairwell", bm, [STONE, STONE, "CryptDarkStone", "CryptEarth"], at=at, smooth=20)


def sump(seed=6, at=(0, 0, 0)):
    """A sump at the foot of the rock: a pool of still black water ringed with stone, the way on
    under the surface. Rock heaped behind it (+Y)."""
    r = rng(seed)
    bm = bmesh.new()
    hole(bm, outline_round(1.0, 11, r, 0.12), 0.3, r, rim_w=0.3, bottom_z=0.14)
    for k, (x, y, h) in enumerate(((-0.9, 1.1, 1.1), (0.4, 1.4, 1.5), (1.2, 0.7, 0.8))):
        chunk(bm, (x, y, 0), (0.55, 0.45, h), r, mats=(0, 2, 1), points=16)
    return make("Sump", bm, ["Scree", "Moss", "Cinder", "CryptDarkStone", "Sea"], at=at)


def burrow(seed=7, at=(0, 0, 0)):
    """An earth bank with a low hole in it, a body's width (pair it with ThroatBurrow): two shoulders
    of earth and a turf roof over the gap, roots showing where the bank was dug."""
    r = rng(seed)
    bm = bmesh.new()
    for x in (-0.68, 0.68):                       # deep enough to cover its throat (2.3 tiles)
        chunk(bm, (x, 1.0, 0), (0.48, 1.4, 0.8), r, mats=(1, 2, 0), points=18)
    chunk(bm, (0, 1.1, 0.5), (0.8, 1.35, 0.5), r, sink=0.0, mats=(1, 2, 0), points=18)
    for k in range(4):
        x = r.uniform(-0.3, 0.3)
        N.rod(bm, [(x, -0.15, 0.62), (x + r.uniform(-0.1, 0.1), -0.2, 0.42)], 0.015, mat=3, sides=3, taper=0.5)
    return make("Burrow", bm, ["Grass", "Dirt", "Mud", "Bark"], at=at)


def rope_anchor(seed=1, at=(0, 0, 0)):
    """A rope down a hole: a stake driven in a little way back from the lip, leaning away from it, the
    rope tied off round it and run over the lip (+Y, the lip about a tile away) and down into the
    dark; the spare coiled at the stake's foot."""
    r = rng(seed)
    bm = bmesh.new()
    N.rod(bm, [(0, 0, -0.1), (0.02, -0.1, 0.55)], 0.05, sides=5, taper=0.8)
    N.rod(bm, [(0.02, -0.06, 0.42), (0.0, 0.5, 0.3), (0.0, 1.05, 0.4), (0.02, 1.25, 0.25), (0.0, 1.35, 0.05)],
          0.02, mat=1, sides=4)
    for k, (rad, z) in enumerate(((0.16, 0.03), (0.13, 0.07))):
        pts = [(0.32 + rad * math.cos(a), -0.25 + rad * math.sin(a), z) for a in [2 * math.pi * i / 10 for i in range(11)]]
        N.rod(bm, pts, 0.02, mat=1, sides=4)
    return make("RopeAnchor", bm, ["Driftwood", "Wheat"], at=at, smooth=50)


def ladder_top(seed=2, at=(0, 0, 0)):
    """The top of a ladder standing in a hole, its rails sticking up out of it and leaning against the
    lip (-Y), rungs lashed on; its foot is in the dark at the hole's floor."""
    r = rng(seed)
    bm = bmesh.new()
    foot, head = Vector((0, 0.3, 0.03)), Vector((0, -0.35, 1.25))
    for x in (-0.22, 0.22):
        N.rod(bm, [foot + Vector((x, 0, 0)), head + Vector((x * 1.05, 0, 0))], 0.04, sides=5)
    for k in range(5):
        p = foot.lerp(head, 0.12 + 0.19 * k)
        N.rod(bm, [p + Vector((-0.24, 0, 0)), p + Vector((0.24, 0, 0))], 0.025, sides=4)
    return make("LadderTop", bm, ["Driftwood"], at=at, smooth=50)


def windlass(seed=3, at=(0, 0, 0)):
    """A windlass over a shaft: two posts either side of it, an axle with a crank, the rope wound on
    it running straight down into the dark."""
    r = rng(seed)
    bm = bmesh.new()
    for x in (-1.05, 1.05):
        N.rod(bm, N.wobble(r, (x, 0, -0.05), (x, 0, 1.35), 3, 0.02), 0.07, sides=6, taper=0.9)
    N.rod(bm, [(-1.12, 0, 1.2), (1.12, 0, 1.2)], 0.07, sides=6)
    N.rod(bm, [(1.12, 0, 1.2), (1.3, 0, 1.2), (1.3, 0, 0.92)], 0.03, sides=4)
    A.prism(bm, 8, 0.11, 0.11, -0.22, 0.22, (0, 0), mat_index=1, cap_top=True, cap_bottom=True)
    lay(bm, list(bm.verts)[-16:], Matrix.Translation((0, 0, 1.2)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    N.rod(bm, [(0, -0.1, 1.15), (0, -0.1, 0.04)], 0.02, mat=1, sides=4)
    return make("Windlass", bm, ["Timber", "Wheat"], at=at, smooth=40)


def planked(seed=4, at=(0, 0, 0)):
    """Planks laid over a hole about two tiles across, resting on its lip: four boards, one missing
    from the middle, one sagging."""
    r = rng(seed)
    bm = bmesh.new()
    for k, x in enumerate((-0.75, -0.38, 0.38, 0.75)):
        before = set(bm.verts)
        block(bm, (x - 0.15, -1.15, -0.03), (x + 0.15, 1.15, 0.03), r, chip=0.01)
        sag = 0.12 if k == 2 else 0.0
        lay(bm, added(bm, before), Matrix.Translation((0, 0, 0.4 - sag)) @ Matrix.Rotation(r.uniform(-0.05, 0.05), 4, 'Z'))
    return make("Planked", bm, ["Planks"], at=at, smooth=20)


# Name in the game (Resources/Caves/<Name>) to builder, by group.
GENERIC = {
    "rock": {
        "RockMass": rock_mass, "RockMassSmall": lambda at=(0, 0, 0): rock_mass(at=at, kind="small"),
        "RockMassWide": lambda at=(0, 0, 0): rock_mass(at=at, kind="wide"),
        "RockMassTall": lambda at=(0, 0, 0): rock_mass(at=at, kind="tall"),
        "RockSpire": rock_spire, "RockLip": rock_lip, "RockLipLong": lambda at=(0, 0, 0): rock_lip(at=at, long=True),
        "RockSlab": rock_slab, "Boulder": boulder, "Rubble": rubble, "Scree": scree,
    },
    "openings": {
        "Throat": throat, "ThroatCleft": lambda at=(0, 0, 0): throat(at=at, kind="cleft"),
        "ThroatLow": lambda at=(0, 0, 0): throat(at=at, kind="low"),
        "ThroatSquare": lambda at=(0, 0, 0): throat(at=at, kind="square"),
        "ThroatSqueeze": lambda at=(0, 0, 0): throat(at=at, kind="squeeze"),
        "ThroatBurrow": lambda at=(0, 0, 0): throat(at=at, kind="burrow"),
    },
    "descents": {
        "Pit": pit, "Sinkhole": sinkhole, "Fissure": fissure, "Shaft": shaft, "Stairwell": stairwell,
        "Sump": sump, "Burrow": burrow,
        "RopeAnchor": rope_anchor, "LadderTop": ladder_top, "Windlass": windlass, "Planked": planked,
    },
    "stone": {
        "Post": post, "PostBroken": lambda at=(0, 0, 0): post(at=at, broken=True),
        "Column": lambda at=(0, 0, 0): post(at=at, round_=True),
        "ColumnBroken": lambda at=(0, 0, 0): post(at=at, round_=True, broken=True),
        "Lintel": lintel, "LintelLong": lambda at=(0, 0, 0): lintel(at=at, long=True),
        "Arch": arch, "ArchBroken": lambda at=(0, 0, 0): arch(at=at, broken=True),
        "WallSpan": wall_span, "Steps": steps, "StepsNarrow": lambda at=(0, 0, 0): steps(at=at, wide=False),
        "Plinth": plinth, "MasonryRubble": masonry_rubble,
    },
    "timber and iron": {
        "TimberFrame": timber_frame, "Door": door, "DoorAjar": lambda at=(0, 0, 0): door(at=at, ajar=True),
        "Boarded": boarded, "Gate": gate, "GateOpen": lambda at=(0, 0, 0): gate(at=at, open_=True), "Grate": grate,
    },
}
