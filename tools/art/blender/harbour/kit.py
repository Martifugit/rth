"""The harbour kit, blockout (docs/harbour-kit.md): jetties, quays, boats and ships at their final
sizes in plain colours, each walkable part with its walk layout. Generic: a port's identity comes
later as a set on top (colours, stone, figureheads, lamps), as the caves kit does.

Conventions:
- One tile is one unit. A part's origin is the middle of its first tile (tile (0, 0)), at the
  water's surface (z 0); walkable parts run their length along +Y (out from the bank), boats point
  their bow to +Y. A region turns them in quarter turns.
- Walk layouts (`Walk`): the tiles you can stand on, per plane, each with its deck height above the
  water; the tile edges that block (rails, gunwales); climbs between planes and into other places
  (stairs, a hatch). Written to world/walk/<Name>.json beside the model's export, read by the region
  compiler (the walk mechanism, docs/harbour-kit.md section 2), and drawn on the board: green tiles,
  red blocked edges, blue climbs.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\harbour\\kit.py")
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..")
for p in (ART, os.path.join(ART, "nature")):
    if p not in sys.path:
        sys.path.insert(0, p)
sys.modules.pop("rth_art", None)
sys.modules.pop("basin", None)
import rth_art as A  # noqa: E402
import basin as N  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
WALK_DIR = os.path.join(REPO, "world", "walk")

BED = -1.4          # the bottom piles stand on
JETTY = 0.45        # a jetty's deck above the water
QUAY = 0.9          # the bank and a quay's top above the water
STOREY = 2.3        # docs/world-scale.md D2


# ---- walk layouts -------------------------------------------------------------------------------

class Walk:
    """A part's walk layout: tiles to stand on (plane, dx, dy, height), blocked tile edges (plane and
    a grid line from (x0, y0) to (x1, y1), where tile (0, 0) spans grid 0..1), climbs."""

    def __init__(self, name):
        self.name = name
        self.tiles = {}
        self.walls = []
        self.climbs = []

    def tile(self, dx, dy, h, plane=0):
        self.tiles[(plane, dx, dy)] = round(h, 3)

    def rect(self, x0, y0, x1, y1, h, plane=0):
        for dx in range(x0, x1):
            for dy in range(y0, y1):
                self.tile(dx, dy, h, plane)

    def wall(self, x0, y0, x1, y1, plane=0):
        self.walls.append((plane, x0, y0, x1, y1))

    def ring(self, plane=0, gaps=()):
        """Block every edge between a walkable tile of this plane and one that isn't, except at the
        given (dx, dy, side) gaps (side N, E, S, W): a rail round the whole deck."""
        mine = {(dx, dy) for (p, dx, dy) in self.tiles if p == plane}
        for (dx, dy) in mine:
            for side, (nx, ny), line in (("N", (dx, dy + 1), (dx, dy + 1, dx + 1, dy + 1)),
                                         ("S", (dx, dy - 1), (dx, dy, dx + 1, dy)),
                                         ("E", (dx + 1, dy), (dx + 1, dy, dx + 1, dy + 1)),
                                         ("W", (dx - 1, dy), (dx, dy, dx, dy + 1))):
                if (nx, ny) not in mine and (dx, dy, side) not in gaps:
                    self.wall(*line, plane=plane)

    def climb(self, kind, frm, to):
        """frm, to: (plane, dx, dy); to may name another place: ("hold", plane, x, y)."""
        self.climbs.append({"kind": kind, "from": list(frm), "to": list(to)})

    def data(self):
        planes = {}
        for (p, dx, dy), h in sorted(self.tiles.items()):
            planes.setdefault(str(p), []).append([dx, dy, h])
        return {"model": self.name, "planes": planes, "walls": [list(w) for w in self.walls], "climbs": self.climbs}

    def save(self):
        os.makedirs(WALK_DIR, exist_ok=True)
        with open(os.path.join(WALK_DIR, self.name + ".json"), "w", encoding="utf-8") as f:
            json.dump(self.data(), f, indent=1)

    def overlay(self, ob):
        """Draw the layout over the model on the board: a green square on every walkable tile at its
        height, red bars on blocked edges, blue lines for climbs. Parented to the model."""
        bm = bmesh.new()
        for (p, dx, dy), h in self.tiles.items():
            z = h + 0.03
            v = [bm.verts.new((dx + x, dy + y, z)) for x, y in ((-0.45, -0.45), (0.45, -0.45), (0.45, 0.45), (-0.45, 0.45))]
            bm.faces.new(v).material_index = 0
        for (p, x0, y0, x1, y1) in self.walls:
            hs = [h for (pp, dx, dy), h in self.tiles.items() if pp == p and min(x0, x1) - 1 <= dx <= max(x0, x1) and min(y0, y1) - 1 <= dy <= max(y0, y1)]
            z = (max(hs) if hs else 0) + 0.25
            a, b = Vector((x0 - 0.5, y0 - 0.5, z)), Vector((x1 - 0.5, y1 - 0.5, z))
            d = (b - a).normalized()
            s = Vector((-d.y, d.x, 0)) * 0.03
            A.hull(bm, [a + s, a - s, b + s, b - s, a + s + Vector((0, 0, 0.06)), b - s + Vector((0, 0, 0.06))], 1)
        for c in self.climbs:
            if not isinstance(c["to"][0], int):
                continue
            pa = self.tiles.get(tuple(c["from"]), 0)
            pb = self.tiles.get(tuple(c["to"]), 0)
            a = Vector((c["from"][1], c["from"][2], pa + 0.1))
            b = Vector((c["to"][1], c["to"][2], pb + 0.1))
            N.branch(bm, a, b, 0.04, 0.04, mat=2, sides=4)
        me = bpy.data.meshes.new(self.name + "Walk")
        bm.to_mesh(me)
        bm.free()
        for name, col in (("WalkTile", (0.1, 0.8, 0.2, 0.45)), ("WalkWall", (0.9, 0.1, 0.1, 1.0)), ("WalkClimb", (0.1, 0.3, 0.95, 1.0))):
            m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            m.diffuse_color = col
            m.use_nodes = True
            bsdf = m.node_tree.nodes["Principled BSDF"]
            bsdf.inputs["Base Color"].default_value = col
            bsdf.inputs["Alpha"].default_value = col[3]
            m.surface_render_method = "BLENDED"
            me.materials.append(m)
        o = bpy.data.objects.new(self.name + "Walk", me)
        A.collection("Walk").objects.link(o)
        o.parent = ob
        return o


def make(name, bm, materials, walk=None, smooth=None):
    ob = N.make(name, bm, materials, smooth_angle=smooth, coll="Harbour")
    if walk is not None:
        walk.save()
        walk.overlay(ob)
    return ob


# ---- timber -----------------------------------------------------------------------------------

def pile(bm, x, y, top, r=0.11, mat=0):
    """A pile driven into the bed, its head standing a little proud of what it carries."""
    A.prism(bm, 6, r, r * 0.92, BED, top, (x, y), mat_index=mat, cap_bottom=False)


def planks(bm, x0, y0, x1, y1, z, rnd, across="x", t=0.06, mat=1, n=None):
    """Deck boards from (x0, y0) to (x1, y1), their top at z, laid across `across` (the boards run
    along it), a hair apart."""
    if across == "x":
        n = n or max(1, round((y1 - y0) / 0.2))
        w = (y1 - y0) / n
        for k in range(n):
            A.box(bm, (x0, y0 + k * w + 0.008, z - t), (x1, y0 + (k + 1) * w - 0.008, z), mat)
    else:
        n = n or max(1, round((x1 - x0) / 0.2))
        w = (x1 - x0) / n
        for k in range(n):
            A.box(bm, (x0 + k * w + 0.008, y0, z - t), (x0 + (k + 1) * w - 0.008, y1, z), mat)


def beam(bm, a, b, h, w, mat=0):
    """A squared timber from a to b (its middle line), `h` deep and `w` wide."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    s = d.cross(Vector((0, 0, 1)))
    if s.length < 1e-6:
        s = Vector((1, 0, 0))
    s.normalize()
    u = s.cross(d)
    A.hull(bm, [p + s * ss * w + u * uu * h for p in (a, b) for ss in (-0.5, 0.5) for uu in (-0.5, 0.5)], mat)


TIMBER = ["Timber", "Planks", "Iron", "DeadWood"]       # piles and beams, deck boards, iron, weathered


# ---- the waterfront ----------------------------------------------------------------------------

def jetty_span(width=1, length=1, name=None):
    """A straight run of jetty out over the water along +Y, `width` tiles wide: a pair of piles at
    the start of each tile, a stringer along each side, boards laid across, the deck a half tile
    above the water. Walkable; its sides are open (the water beside it is blocked anyway)."""
    rnd = random.Random(1)
    bm = bmesh.new()
    x0, x1 = -0.5, width - 0.5
    for k in range(length + 1):
        y = k - 0.5
        for x in (x0 + 0.08, x1 - 0.08):
            pile(bm, x, y, JETTY + 0.12)
        beam(bm, (x0 + 0.08, y, JETTY - 0.22), (x1 - 0.08, y, JETTY - 0.22), 0.14, 0.12)    # the cap across
    for x in (x0 + 0.08, x1 - 0.08):
        beam(bm, (x, -0.5, JETTY - 0.11), (x, length - 0.5, JETTY - 0.11), 0.1, 0.12)       # stringers
    planks(bm, x0, -0.5, x1, length - 0.5, JETTY, rnd, across="x")
    w = Walk(name or "JettySpan%dx%d" % (width, length))
    w.rect(0, 0, width, length, JETTY)
    return make(w.name, bm, TIMBER, w)


def jetty_head(name="JettyHead"):
    """A jetty's end: a platform three tiles square on its piles, a tall mooring post at each
    corner, a ladder down to the water on the far side."""
    rnd = random.Random(2)
    bm = bmesh.new()
    for x in (-0.42, 1.0, 2.42):
        for y in (-0.42, 1.0, 2.42):
            corner = abs(x - 1.0) > 1 and abs(y - 1.0) > 1
            pile(bm, x, y, JETTY + (0.85 if corner else 0.12), r=0.13 if corner else 0.11)
    for y in (-0.42, 1.0, 2.42):
        beam(bm, (-0.42, y, JETTY - 0.22), (2.42, y, JETTY - 0.22), 0.14, 0.12)
    for x in (-0.42, 1.0, 2.42):
        beam(bm, (x, -0.5, JETTY - 0.11), (x, 2.5, JETTY - 0.11), 0.1, 0.12)
    planks(bm, -0.5, -0.5, 2.5, 2.5, JETTY, rnd, across="x")
    for x in (0.75, 1.25):                                                     # the ladder
        beam(bm, (x, 2.55, -0.6), (x, 2.55, JETTY + 0.35), 0.06, 0.06)
    for z in (-0.4, -0.1, 0.2):
        beam(bm, (0.75, 2.55, z), (1.25, 2.55, z), 0.04, 0.04)
    w = Walk(name)
    w.rect(0, 0, 3, 3, JETTY)
    return make(name, bm, TIMBER, w)


def jetty_stairs(name="JettyStairs"):
    """Steps from the bank (a quay's height) down onto a jetty along +Y: two tiles, a stringer each
    side and treads between them."""
    bm = bmesh.new()
    steps = [(QUAY - 0.12, -0.5, 0.0), (QUAY - 0.27, 0.0, 0.5), (JETTY + 0.17, 0.5, 1.0), (JETTY + 0.04, 1.0, 1.5)]
    for z, y0, y1 in steps:
        A.box(bm, (-0.42, y0, z - 0.06), (0.42, y1, z), 1)
    for x in (-0.46, 0.46):
        A.hull(bm, [Vector((x + s, y, z)) for s in (-0.04, 0.04) for y, z in ((-0.5, QUAY - 0.05), (-0.5, QUAY - 0.4),
                                                                          (1.5, JETTY - 0.3), (1.5, JETTY + 0.05))], 0)
        pile(bm, x, 1.5, JETTY + 0.12)
    w = Walk(name)
    w.tile(0, 0, QUAY - 0.2)
    w.tile(0, 1, JETTY + 0.12)
    return make(name, bm, TIMBER, w)


def quay_edge(length=4, name=None):
    """A stone quay's face along X, the water toward -Y: a battered wall from the bed up to the bank,
    a pale coping along its top, a bollard and an iron ring every other tile. The ground behind it
    is the region's own; the walk layout is the coping's row."""
    bm = bmesh.new()
    x0, x1 = -0.5, length - 0.5
    A.hull(bm, [Vector((x, y, z)) for x in (x0, x1) for y, z in ((-0.85, BED), (-0.5, QUAY - 0.1), (0.5, QUAY - 0.1), (0.5, BED))], 0)
    A.box(bm, (x0, -0.58, QUAY - 0.1), (x1, 0.3, QUAY), 1)
    for k in range(0, length, 2):
        A.prism(bm, 8, 0.13, 0.11, QUAY, QUAY + 0.35, (k, 0.0), mat_index=2)
        A.prism(bm, 8, 0.16, 0.16, QUAY + 0.33, QUAY + 0.4, (k, 0.0), mat_index=2)
        A.loft(bm, [(Vector((k + 0.5, -0.66, QUAY - 0.35)), 0.09, 0.09), (Vector((k + 0.5, -0.6, QUAY - 0.35)), 0.09, 0.09)],
               sides=8, ref=(0, 0, 1), mats=2)
    w = Walk(name or "QuayEdge%d" % length)
    w.rect(0, 0, length, 1, QUAY)
    for k in range(0, length, 2):
        w.tiles.pop((0, k, 0), None)                   # the bollards block their tiles
    return make(w.name, bm, ["Stone", "Gravestone", "Iron"], w)


def quay_steps(name="QuaySteps"):
    """Water stairs let into a quay's face, running down along it (+X) from the coping to the water:
    three tiles of broad stone steps, a low wall on the water side."""
    bm = bmesh.new()
    hs = [QUAY - 0.2, JETTY, 0.15]
    for k, h in enumerate(hs):
        A.box(bm, (k - 0.5, -0.5, BED), (k + 0.5, 0.5, h), 0)
        A.box(bm, (k - 0.5, -0.52, h - 0.06), (k + 0.5, 0.5, h), 1)
    A.box(bm, (-0.5, -0.62, BED), (2.5, -0.5, QUAY - 0.05), 0)
    A.box(bm, (-0.5, -0.66, QUAY - 0.1), (2.5, -0.46, QUAY), 1)
    w = Walk(name)
    for k, h in enumerate(hs):
        w.tile(k, 0, h)
    w.wall(0, 0, 3, 0)                                 # the low wall on the water side
    return make(name, bm, ["Stone", "Gravestone"], w)


def slipway(name="Slipway"):
    """A slipway for hauling boats out: two tiles wide, five long, timbers laid across a ramp that
    runs from the bank down under the water along -Y."""
    rnd = random.Random(3)
    bm = bmesh.new()
    top, bottom = QUAY - 0.1, -0.5

    def z(y):
        return top + (bottom - top) * (-(y - 0.5) / 5.0)
    A.hull(bm, [Vector((x, y, zz)) for x in (-0.5, 1.5) for y, zz in ((0.5, top), (0.5, BED), (-4.5, BED), (-4.5, bottom))], 3)
    y = 0.5
    while y > -4.5:
        y1 = y - 0.32
        A.hull(bm, [Vector((x, yy, z(yy) + dz)) for x in (-0.48, 1.48) for yy in (y - 0.05, y - 0.27) for dz in (0.0, 0.08)], 0)
        y = y1
    w = Walk(name)
    for dy in range(-4, 1):
        h = z(dy)
        if h > 0.05:
            w.rect(0, dy, 2, dy + 1, h)
    return make(name, bm, ["Timber", "Planks", "Iron", "Gravel"], w)


def pontoon(name="Pontoon"):
    """A floating landing two tiles by four, low on the water: a deck on a raft of lashed barrels,
    a short rail on its open end."""
    rnd = random.Random(4)
    bm = bmesh.new()
    z = 0.22
    for x in (-0.25, 1.25):
        for k in range(4):
            A.loft(bm, [(Vector((x, k - 0.4, 0.0)), 0.22, 0.22), (Vector((x, k + 0.4, 0.0)), 0.22, 0.22)], sides=8,
                   ref=(0, 0, 1), mats=3)
    planks(bm, -0.5, -0.5, 1.5, 3.5, z, rnd, across="x")
    for x in (-0.45, 1.45):
        beam(bm, (x, -0.5, z - 0.1), (x, 3.5, z - 0.1), 0.12, 0.1)
    w = Walk(name)
    w.rect(0, 0, 2, 4, z)
    return make(name, bm, ["Timber", "Planks", "Iron", "Oak"], w)


def gangplank(rise=1.1, length=3, name="Gangplank"):
    """A gangplank along +Y from a quay or jetty up onto a deck `rise` higher: one board-wide ramp
    with cleats nailed across it, a rope rail on each side."""
    bm = bmesh.new()
    z0, z1 = QUAY, QUAY + rise
    L = length - 0.2
    A.hull(bm, [Vector((x, y, z + dz)) for x in (-0.35, 0.35) for y, z in ((-0.5, z0), (-0.5 + L, z1)) for dz in (0.0, -0.08)], 1)
    for k in range(1, 9):
        t = k / 9
        y, z = -0.5 + L * t, z0 + rise * t
        A.box(bm, (-0.33, y - 0.03, z), (0.33, y + 0.03, z + 0.04), 0)
    for x in (-0.4, 0.4):
        for t in (0.05, 0.95):
            beam(bm, (x, -0.5 + L * t, z0 + rise * t), (x, -0.5 + L * t, z0 + rise * t + 0.85), 0.05, 0.05)
        beam(bm, (x, -0.5 + L * 0.05, z0 + rise * 0.05 + 0.8), (x, -0.5 + L * 0.95, z0 + rise * 0.95 + 0.8), 0.025, 0.025, mat=3)
    w = Walk(name)
    for dy in range(length):
        w.tile(0, dy, z0 + rise * (dy + 0.5) / length)
    w.wall(0, 0, 0, length)
    w.wall(1, 0, 1, length)
    return make(name, bm, ["Timber", "Planks", "Iron", "Hide"], w)


# ---- dressing ---------------------------------------------------------------------------------

def bollard(name="Bollard"):
    bm = bmesh.new()
    A.prism(bm, 8, 0.15, 0.12, -0.05, 0.4, mat_index=0)
    A.prism(bm, 8, 0.19, 0.19, 0.38, 0.47, mat_index=0)
    return make(name, bm, ["Iron"])


def capstan(name="Capstan"):
    """A capstan for warping ships in: a waisted drum on a round base, bars through its head."""
    bm = bmesh.new()
    A.prism(bm, 10, 0.45, 0.4, -0.05, 0.12, mat_index=1)
    for z0, z1, r0, r1 in ((0.12, 0.45, 0.3, 0.22), (0.45, 0.75, 0.22, 0.3)):
        A.prism(bm, 10, r0, r1, z0, z1, mat_index=0)
    A.prism(bm, 10, 0.34, 0.34, 0.75, 0.88, mat_index=0)
    for k in range(4):
        a = math.pi * k / 4
        d = Vector((math.cos(a), math.sin(a), 0))
        beam(bm, d * -0.95 + Vector((0, 0, 0.82)), d * 0.95 + Vector((0, 0, 0.82)), 0.06, 0.06, mat=0)
    return make(name, bm, ["Oak", "Stone"])


def crane(name="Crane"):
    """A treadless quay crane: a post on a stone base, a raking jib braced from it, a rope and hook
    hanging from the jib's head over the water (-Y)."""
    bm = bmesh.new()
    A.box(bm, (-0.45, -0.45, -0.05), (0.45, 0.45, 0.25), 2)
    beam(bm, (0, 0, 0.2), (0, 0, 3.2), 0.24, 0.24)
    beam(bm, (0, 0.05, 1.2), (0, -2.4, 4.0), 0.18, 0.18)
    beam(bm, (0, 0, 2.9), (0, -1.6, 3.3), 0.12, 0.12)
    beam(bm, (0, 0, 0.9), (0, -0.9, 1.9), 0.1, 0.1)
    N.rod(bm, [(0, -2.35, 3.95), (0, -2.35, 1.6)], 0.02, mat=3, sides=3)
    A.hull(bm, [Vector((x, -2.35 + y, z)) for x in (-0.06, 0.06) for y, z in ((0, 1.6), (0, 1.45), (0.12, 1.35), (0.15, 1.45))], 1)
    return make(name, bm, ["Timber", "Iron", "Stone", "Hide"])


def net_rack(name="NetRack"):
    """Nets hung to dry along X: two forked posts, a pole between them, the net hanging from it in a
    sagging sheet (a flat dark mesh, a few floats along its foot)."""
    bm = bmesh.new()
    for x in (-0.9, 0.9):
        N.rod(bm, [(x, 0, -0.05), (x, 0, 1.75)], 0.05, mat=0, sides=5)
    N.rod(bm, [(-1.0, 0, 1.7), (1.0, 0, 1.72)], 0.035, mat=0, sides=5)
    cols = 6
    top = [bm.verts.new((-0.85 + 1.7 * k / cols, 0.01, 1.66)) for k in range(cols + 1)]
    bot = [bm.verts.new((-0.85 + 1.7 * k / cols, 0.06 + 0.04 * math.sin(k), 0.35 + 0.15 * math.sin(k * 1.3) ** 2)) for k in range(cols + 1)]
    for k in range(cols):
        bm.faces.new((bot[k], bot[k + 1], top[k + 1], top[k])).material_index = 1
    for k in range(1, cols, 2):
        A.loft(bm, [(bot[k].co + Vector((0, 0, -0.04)), 0.05, 0.05), (bot[k].co + Vector((0, 0, 0.04)), 0.05, 0.05)],
               sides=6, ref=(1, 0, 0), mats=2)
    return make(name, bm, ["DeadWood", "Umber", "Ochre"])


def harbour_lamp(name="HarbourLamp"):
    """A lamp on a post at a jetty's end or the quay's corner: a square post, an iron arm and a
    lantern hanging from it."""
    bm = bmesh.new()
    beam(bm, (0, 0, -0.05), (0, 0, 2.3), 0.14, 0.14)
    beam(bm, (0, 0, 2.15), (0, -0.55, 2.15), 0.05, 0.05, mat=1)
    N.rod(bm, [(0, -0.5, 2.12), (0, -0.5, 1.95)], 0.012, mat=1, sides=3)
    A.prism(bm, 6, 0.1, 0.12, 1.7, 1.93, (0, -0.5), mat_index=2)
    A.prism(bm, 6, 0.12, 0.02, 1.93, 2.0, (0, -0.5), mat_index=1)
    return make(name, bm, ["Timber", "Iron", "CandleWax"])


def channel_marker(name="ChannelMarker"):
    """A post in the water marking the channel: a tall pile with a barrel cage on top."""
    bm = bmesh.new()
    pile(bm, 0, 0, 2.2, r=0.13)
    for z in (2.0, 2.4):
        A.prism(bm, 8, 0.22, 0.22, z, z + 0.05, mat_index=2, cap_bottom=True)
    for k in range(4):
        a = math.pi * k / 2
        beam(bm, (0.22 * math.cos(a), 0.22 * math.sin(a), 2.0), (0.22 * math.cos(a), 0.22 * math.sin(a), 2.45), 0.04, 0.04, mat=2)
    return make(name, bm, ["Timber", "Planks", "Iron"])


# ---- boats and ships --------------------------------------------------------------------------

def hull(bm, length, beam_, depth, sheer, stations=11, bow=0.0, transom=0.0, keel=None, flat=0.0,
         mats=(0, 1), open_top=True, wall=0.06):
    """A boat's hull along Y from -length/2 (stern) to +length/2 (bow): stations across it, each a U
    from the keel to the gunwale. beam_: the full width midships; depth: keel to gunwale midships;
    sheer: how far the gunwale rises at bow and stern; bow, transom: the half widths left at the
    ends (0 comes to a point); flat: the half width of a flat bottom (barges). open_top: an inner
    skin `wall` thick so the boat is hollow, its inside coloured mats[1]. Returns the gunwale height
    and half width along Y as functions."""
    L = length / 2

    def half(y):
        t = y / L
        end = bow if t > 0 else transom
        return end + (beam_ / 2 - end) * (1 - abs(t) ** (2.2 if t > 0 else 3.0))

    def top(y):
        return depth * 0.15 + sheer * (y / L) ** 2 + (depth - depth * 0.15)

    def section(y, inset=0.0):
        b = max(half(y) - inset, 0.01)
        zt, zk = top(y), (keel if keel is not None else 0.0) + inset
        pts = []
        for k in range(7):
            a = math.pi * k / 6
            x = -math.cos(a) * b
            z = zk + (zt - zk) * (1 - math.sin(a)) ** 1.6 if abs(x) > flat else zk
            pts.append(Vector((x, y, z)))
        return pts

    ys = [-L + 2 * L * k / (stations - 1) for k in range(stations)]
    outer = [[bm.verts.new(p) for p in section(y)] for y in ys]
    for a, b in zip(outer, outer[1:]):
        for k in range(len(a) - 1):
            bm.faces.new((a[k], b[k], b[k + 1], a[k + 1])).material_index = mats[0]
    if open_top:
        inner = [[bm.verts.new(p) for p in section(y, wall)] for y in ys]
        for a, b in zip(inner, inner[1:]):
            for k in range(len(a) - 1):
                bm.faces.new((a[k], a[k + 1], b[k + 1], b[k])).material_index = mats[1]
        for a, b, ia, ib in zip(outer, outer[1:], inner, inner[1:]):
            for k in (0, len(a) - 1):
                bm.faces.new((a[k], ia[k], ib[k], b[k])).material_index = mats[0]
        for o, i in ((outer[0], inner[0]), (outer[-1], inner[-1])):
            for k in range(len(o) - 1):
                bm.faces.new((o[k], o[k + 1], i[k + 1], i[k])).material_index = mats[0]
            if half(o[0].co.y) > 0.05:                     # a transom: close it with a flat plate
                bm.faces.new(i).material_index = mats[0]
    else:
        for ring in (outer[0], outer[-1]):
            bm.faces.new(ring).material_index = mats[0]
    return half, top


def hull_deck(bm, half, y0, y1, z, inset=0.15, board=0.22, mat=2):
    """Deck boards across a hull from y0 to y1 at height z, each as wide as the hull is there."""
    n = max(1, round((y1 - y0) / board))
    w = (y1 - y0) / n
    for k in range(n):
        ya, yb = y0 + k * w, y0 + (k + 1) * w
        b = min(half(ya), half(yb)) - inset
        if b > 0.05:
            A.box(bm, (-b, ya + 0.008, z - 0.06), (b, yb - 0.008, z), mat)


def rowing_boat(name="RowingBoat"):
    """A rowing boat, 1.5 by 3.5 tiles: a clinker-built hull with a little sheer, two thwarts, a pair
    of oars shipped along the thwarts. Scenery (boarded for a short crossing, not walked on)."""
    bm = bmesh.new()
    half, top = hull(bm, 3.4, 1.3, 0.55, 0.18, bow=0.0, transom=0.28, keel=-0.2)
    for y in (-0.5, 0.45):
        A.box(bm, (-half(y) + 0.04, y - 0.1, top(y) - 0.2), (half(y) - 0.04, y + 0.1, top(y) - 0.15), 2)
    for s in (-1, 1):
        beam(bm, (s * 0.25, -1.3, top(0) - 0.1), (s * 0.3, 1.3, top(0) - 0.08), 0.04, 0.05, mat=2)
    return make(name, bm, ["Timber", "DeadWood", "Planks"])


def fishing_boat(name="FishingBoat"):
    """A lake fishing boat, 2 by 5 tiles: a deep open hull, one stubby mast with a lug sail furled to
    its yard, a pile of nets in the stern. Scenery."""
    bm = bmesh.new()
    half, top = hull(bm, 4.8, 1.9, 0.85, 0.3, bow=0.0, transom=0.35, keel=-0.4)
    for y in (-1.2, 0.0, 1.0):
        A.box(bm, (-half(y) + 0.05, y - 0.12, top(y) - 0.28), (half(y) - 0.05, y + 0.12, top(y) - 0.22), 2)
    beam(bm, (0, 0.6, -0.3), (0, 0.6, 3.6), 0.12, 0.12)
    beam(bm, (-0.05, 0.6, 3.3), (0.05, -0.9, 2.2), 0.07, 0.07)
    A.loft(bm, [(Vector((0.0, 0.55, 3.25)), 0.11, 0.11), (Vector((0.05, -0.85, 2.25)), 0.13, 0.13)], sides=6, ref=(1, 0, 0), mats=3)
    N.blob(bm, (0, -1.5, top(-1.5) - 0.3), (0.5, 0.4, 0.22), 1, random.Random(5), mat=4)
    return make(name, bm, ["Timber", "DeadWood", "Planks", "Linen", "Umber"])


def barge(name="RiverBarge"):
    """A river barge, 3 by 9 tiles: a flat-bottomed hull with raked ends and low sides, a deck over it,
    a deckhouse at the stern with a roof, a long steering oar over the stern, cargo lashed midships.
    Walkable: the deck inside its sides; the deckhouse and the cargo block."""
    rnd = random.Random(6)
    bm = bmesh.new()
    deck = 0.75
    half, top = hull(bm, 8.8, 2.9, 1.25, 0.12, stations=13, bow=0.7, transom=1.0, keel=-0.5, flat=1.0)
    hull_deck(bm, half, -4.3, 4.3, deck, inset=0.12)
    A.box(bm, (-1.2, -3.9, deck), (1.2, -2.0, deck + 1.9), 3)                                  # the deckhouse
    A.hull(bm, [Vector((x, y, z)) for y in (-4.0, -1.9) for x, z in ((-1.35, deck + 1.85), (1.35, deck + 1.85), (0, deck + 2.35))], 4)
    beam(bm, (0.0, -3.9, deck + 1.2), (0.3, -6.2, 0.1), 0.1, 0.1)                              # the steering oar
    A.hull(bm, [Vector((0.3 + x, -6.2 + y, z)) for x in (-0.03, 0.03) for y, z in ((0, 0.4), (-0.6, -0.1), (0.2, -0.3), (-0.5, 0.2))], 0)
    for k, (x, y) in enumerate(((-0.6, 0.2), (0.55, 0.3), (-0.55, 1.15), (0.0, 0.7))):
        A.box(bm, (x - 0.3, y - 0.3, deck), (x + 0.3, y + 0.3, deck + (0.6 if k < 3 else 1.2)), 5)
    w = Walk(name)
    for dy in range(-4, 5):
        for dx in (-1, 0, 1):
            if half(dy) - 0.15 > abs(dx) + 0.35:
                w.tile(dx, dy, deck)
    for t in ((-1, -3), (0, -3), (1, -3), (-1, -2), (0, -2), (1, -2), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1)):
        w.tiles.pop((0,) + t, None)                    # the deckhouse and the cargo
    w.ring(gaps={(1, -1, "E")})                        # boarded from the east, midships astern
    return make(name, bm, ["Timber", "DeadWood", "Planks", "Planks", "Thatch", "Wool"], w)


def ship(name="SeaShip"):
    """A sea-going trader, 6 by 20 tiles, bow to +Y: a deep hull with a strong sheer, the main deck
    two above the water inside its bulwarks, a stern castle one storey higher with the captain's
    cabin under it, a small raised forecastle, two masts with their yards and sails furled (she is
    in port), a bowsprit. Walkable: the main deck (plane 0), the cabin under the stern castle (plane
    0, a door forward), the stern castle's deck (plane 1, stairs up from the main deck), and a hatch
    amidships down into the hold (its own region)."""
    rnd = random.Random(7)
    bm = bmesh.new()
    L, B = 20.0, 6.0
    deck = 2.0
    half, top = hull(bm, L, B, 2.9, 0.9, stations=17, bow=0.0, transom=1.9, keel=-1.2, wall=0.12)
    for y in (-8.5, -3.0, 2.5, 7.0):
        A.box(bm, (-half(y) * 0.98, y - 0.15, 0.85), (half(y) * 0.98, y + 0.15, 1.05), 4)    # a wale (blockout band)
    hull_deck(bm, half, -9.9, 9.0, deck, inset=0.14)
    # the stern castle and the cabin under it; its front on the grid line between tiles -6 and -5
    sc0, sc1 = -10.0, -5.5
    castle = deck + STOREY
    A.box(bm, (-2.4, sc0 + 0.4, deck), (2.4, sc1, castle - 0.1), 3)
    planks(bm, -2.5, sc0 + 0.3, 2.5, sc1 + 0.15, castle, rnd, across="x", mat=2, n=12)
    for x in (-2.5, 2.5):
        A.box(bm, (x - 0.06, sc0 + 0.3, castle), (x + 0.06, sc1 + 0.15, castle + 0.75), 0)
    A.box(bm, (-2.5, sc1 + 0.09, castle), (2.5, sc1 + 0.15, castle + 0.75), 0)
    A.box(bm, (-0.45, sc1 - 0.02, deck), (0.45, sc1 + 0.02, deck + 1.9), 5)                 # the cabin door
    for k in range(3):
        A.box(bm, (-0.7 + 0.7 * k - 0.2, sc0 + 0.35, deck + 1.0), (-0.7 + 0.7 * k + 0.2, sc0 + 0.4, deck + 1.5), 5)
    # stairs up to the stern castle, on the starboard side going aft
    for k in range(6):
        y = sc1 + 1.9 - k * 0.32
        A.box(bm, (1.3, y - 0.32, deck), (2.3, y, deck + (k + 1) * STOREY / 6), 0)
    # the forecastle
    A.box(bm, (-1.6, 7.0, deck), (1.6, 8.6, deck + 0.6), 3)
    # masts, yards (sails furled on them), bowsprit
    for y, h in ((-2.0, 13.0), (4.0, 10.5)):
        A.prism(bm, 8, 0.24, 0.16, 0.0, h, (0, y), mat_index=0)
        for z, span in ((h * 0.55, 3.6), (h * 0.85, 2.6)):
            beam(bm, (-span, y, z), (span, y, z), 0.12, 0.12)
            A.loft(bm, [(Vector((-span * 0.9, y, z - 0.18)), 0.16, 0.14), (Vector((span * 0.9, y, z - 0.18)), 0.16, 0.14)],
                   sides=6, ref=(0, 0, 1), mats=6)
        for s in (-1, 1):                                                                   # shrouds
            for dy in (-0.6, 0.0, 0.6):
                a = Vector((s * half(y + dy) * 0.95, y + dy, top(y + dy)))
                b = Vector((s * 0.15, y, h * 0.8))
                N.branch(bm, a, b, 0.02, 0.02, mat=7, sides=3)
    beam(bm, (0, 8.5, deck + 0.8), (0, 13.0, deck + 2.6), 0.16, 0.16)
    # the boarding step inside the bulwark, where the gangplank comes over the rail (to starboard, +X)
    A.box(bm, (half(0) - 0.75, -0.45, deck), (half(0) - 0.1, 0.45, deck + 0.45), 0)
    # the hatch amidships
    A.box(bm, (-0.6, 0.5, deck), (0.6, 1.5, deck + 0.18), 0)
    A.box(bm, (-0.45, 0.62, deck + 0.18), (0.45, 1.38, deck + 0.2), 5)
    # walk layout
    w = Walk(name)
    for dy in range(-9, 9):
        for dx in range(-2, 3):
            if half(dy) - 0.25 > abs(dx) + 0.5 or (dy <= -6 and abs(dx) <= 2):
                w.tile(dx, dy, deck + (0.6 if dy >= 7 else 0.0))   # main deck and forecastle; the cabin aft
    for dy in range(-9, -5):                                        # the stern castle's deck
        for dx in range(-2, 3):
            w.tile(dx, dy, castle, plane=1)
    for t in ((0, -2), (0, 4), (0, 1), (2, -5), (2, -4)):          # masts, the hatch, the stairs
        w.tiles.pop((0,) + t, None)
    w.ring(gaps={(2, 0, "E")})                                      # boarded amidships, from the quay side
    w.wall(-2, -5, 0, -5)                                           # the cabin's forward wall, its door at 0
    w.wall(1, -5, 3, -5)
    w.ring(plane=1, gaps={(2, -6, "N")})
    w.climb("stairs", (0, 2, -3), (1, 2, -6))
    w.climb("hatch", (0, 0, 2), ("hold", 0, 0, 0))
    return make(name, bm, ["Timber", "DeadWood", "Planks", "Oak", "Charcoal", "Iron", "Linen", "Hide"], w)


# ---- the board ---------------------------------------------------------------------------------

def board_ground():
    """The board: water over everything at z 0, and a bank behind the quay from y 14."""
    bm = bmesh.new()
    A.box(bm, (-6, -16, BED - 0.1), (46, 24, BED), 0)
    water = [bm.verts.new(p) for p in ((-6, -16, 0), (46, -16, 0), (46, 13.5, 0), (-6, 13.5, 0))]
    bm.faces.new(water).material_index = 1
    A.box(bm, (-6, 13.5, BED), (46, 24, QUAY), 2)
    ob = N.make("Board", bm, ["Mud", "Mere", "Grass"], coll="Preview")
    return ob


def place(ob, x, y, rot=0):
    ob.location = (x, y, 0)
    ob.rotation_euler.z = math.radians(rot)
    return ob


def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Harbour", "Walk", "Preview"):
            bpy.data.collections.remove(c)
    board_ground()
    o = {}
    o["quay"] = place(quay_edge(40, name="QuayEdge"), -4, 14)
    o["quay_steps"] = place(quay_steps(), 22, 13)
    o["stairs"] = place(jetty_stairs(), 4, 13, rot=180)
    o["jetty"] = place(jetty_span(1, 8), 4, 11, rot=180)
    o["jetty_head"] = place(jetty_head(), 5, 2, rot=180)
    o["pontoon"] = place(pontoon(), 7, 1, rot=180)
    o["jetty_2"] = place(jetty_span(2, 6, name="JettySpan2x6"), 12, 13, rot=180)
    o["slipway"] = place(slipway(), -3, 13.5)
    o["rowing_boat"] = place(rowing_boat(), 2.0, 7.5, rot=180)
    o["rowing_boat_2"] = place(rowing_boat(), -1.6, 10.5, rot=200)
    o["fishing_boat"] = place(fishing_boat(), 6.8, 8.5, rot=180)
    o["barge"] = place(barge(), 14, 6.0, rot=180)
    o["ship"] = place(ship(), 30.0, 7.0, rot=90)
    o["gangplank"] = place(gangplank(rise=1.9, length=4), 30, 13, rot=180)
    o["bollard"] = place(bollard(), 20, 14)
    o["capstan"] = place(capstan(), 24, 15)
    o["crane"] = place(crane(), 36, 14.5)
    o["net_rack"] = place(net_rack(), 9, 16)
    o["lamp"] = place(harbour_lamp(), 8, 14)
    o["marker"] = place(channel_marker(), 22, -4)
    return o


def rig():
    A.preview_rig(focus=(18.0, 6.0, 1.0), distance=50.0, pitch=40, yaw=-20, ground=0, human=True, human_at=(4.0, 6.5))
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


objs = build()
rig()
result = {k: A.report([v])["triangles"] for k, v in objs.items()}
