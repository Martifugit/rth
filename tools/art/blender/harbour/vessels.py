"""The harbour kit's boats and ships, built on one hull: a rowing boat, a fishing boat, a river barge and
a sea-going trader (docs/harbour-kit.md). Bow to +Y, origin amidships at the waterline.

A hull here is a shipwright's, not a jittered blob: stations along its length, each a U from the keel
to the gunwale, a sheer that rises to bow and stern, a keel with a little rocker; tar below the
waterline, a painted band under the gunwale, a capping rail along its top, raised strakes along its
side, a stem and a sternpost; open boats show their ribs and thwarts inside.
"""
import math
import random

import bmesh
from mathutils import Vector

import rth_art as A
import basin as N
from common import (STOREY, Walk, beam, bollard_at, deck_boards, make, rope_coil)

MATS = ["Timber", "Charcoal", "Russet", "Oak", "Planks", "Iron", "Linen", "Hide", "Driftwood", "DeadWood",
        "Indigo", "Ochre", "CandleWax", "Thatch"]
(HULL, TAR, PAINT, RAIL, DECK, IRON, SAIL, ROPE, GREY, OLD, BLUE, YELLOW, GLASS, STRAW) = range(len(MATS))


class Hull:
    """A hull's shape. length, beam: overall, along Y and across; depth: the gunwale's height above the
    water midships; keel: the keel's height midships (below the water); sheer: how much the gunwale
    rises at the ends; bow, transom: half widths left at the ends (0 comes to a point); rocker: how far
    the keel rises toward the ends; flat: half width of a flat bottom; fill: how full the ends are
    (bigger is fuller)."""

    def __init__(self, length, beam_, depth, keel, sheer, bow=0.0, transom=0.0, rocker=0.1, flat=0.0,
                 fill_bow=2.2, fill_stern=3.0, n=10):
        self.L, self.B = length / 2, beam_ / 2
        self.depth, self.keel, self.sheer = depth, keel, sheer
        self.bow, self.transom, self.rocker, self.flat = bow, transom, rocker, flat
        self.fill_bow, self.fill_stern, self.n = fill_bow, fill_stern, n

    def half(self, y):
        t = max(-1.0, min(1.0, y / self.L))
        end, fill = (self.bow, self.fill_bow) if t > 0 else (self.transom, self.fill_stern)
        return end + (self.B - end) * (1 - abs(t) ** fill)

    def top(self, y):
        return self.depth + self.sheer * (y / self.L) ** 2

    def keel_z(self, y):
        return self.keel + self.rocker * abs(y / self.L) ** 3

    def point(self, y, t, side, inset=0.0):
        """The hull's surface at y, a fraction t of the way from the gunwale (0) to the keel (1), on
        side -1 (port) or +1 (starboard); inset: that far inside the skin."""
        b = max(self.half(y) - inset, 0.005)
        zt, zk = self.top(y), self.keel_z(y) + inset
        a = (math.pi / 2) * t
        x = math.cos(a) * b
        z = zk + (zt - zk) * (1 - math.sin(a)) ** 1.6
        if abs(x) <= self.flat:
            z = zk
        return Vector((side * x, y, z))

    # Where the section's points lie, from the gunwale (0) to the keel (1): close together at the top,
    # where the band under the gunwale is painted and the shape shows most.
    TS = (0.0, 0.07, 0.16, 0.3, 0.48, 0.7, 1.0)

    def section(self, y, inset=0.0):
        port = [self.point(y, t, -1, inset) for t in self.TS]
        starboard = [self.point(y, t, 1, inset) for t in reversed(self.TS[:-1])]
        return port + starboard

    def build(self, bm, stations=13, wall=0.06, waterline=0.05, band=0.2, mats=(HULL, TAR, PAINT, HULL), open_top=True):
        """The hull's skin: outside coloured by height (tar below the waterline, the hull, the painted
        band under the gunwale), an inner skin `wall` thick inside, the transom closed."""
        hull_m, tar_m, paint_m, in_m = mats
        ys = [-self.L + 2 * self.L * k / (stations - 1) for k in range(stations)]
        outer = [[bm.verts.new(p) for p in self.section(y)] for y in ys]
        for a, b in zip(outer, outer[1:]):
            for k in range(len(a) - 1):
                f = bm.faces.new((a[k], b[k], b[k + 1], a[k + 1]))
                c = f.calc_center_median()
                f.material_index = tar_m if c.z < waterline else paint_m if c.z > self.top(c.y) - band else hull_m
        inner = [[bm.verts.new(p) for p in self.section(y, wall)] for y in ys]
        for a, b in zip(inner, inner[1:]):
            for k in range(len(a) - 1):
                bm.faces.new((a[k], a[k + 1], b[k + 1], b[k])).material_index = in_m
        for a, b, ia, ib in zip(outer, outer[1:], inner, inner[1:]):
            for k in (0, len(a) - 1):
                bm.faces.new((a[k], ia[k], ib[k], b[k])).material_index = hull_m
        for o, i in ((outer[0], inner[0]), (outer[-1], inner[-1])):
            for k in range(len(o) - 1):
                bm.faces.new((o[k], o[k + 1], i[k + 1], i[k])).material_index = hull_m
            if self.half(o[0].co.y) > 0.05:
                bm.faces.new(i).material_index = hull_m

    def keelson(self, bm, mat, w=0.05):
        """The keel along the bottom, the stem up the bow and the sternpost up the stern, standing
        a little proud of the hull's ends."""
        L = self.L
        pts = [(Vector((0, y, self.keel_z(y) - w * 0.6)), w, w * 0.8) for y in (-L, -L * 0.5, 0, L * 0.5, L * 0.97)]
        A.loft(bm, pts, sides=4, ref=(0, 0, 1), mats=mat)
        stem = [(Vector((0, L + 0.02 + 0.08 * t * t, self.keel_z(L) + (self.top(L) + 0.18 - self.keel_z(L)) * t)), w * 0.9, w * 0.9)
                for t in (0.0, 0.35, 0.7, 1.0)]
        A.loft(bm, stem, sides=4, ref=(1, 0, 0), mats=mat)
        A.loft(bm, [(Vector((0, -L - 0.03, self.keel_z(-L))), w, w), (Vector((0, -L - 0.03, self.top(-L) + 0.08)), w, w)],
               sides=4, ref=(1, 0, 0), mats=mat)

    def rail(self, bm, mat, wall=0.06, h=0.035, stations=15):
        """The capping rail along the gunwale on each side, a little wider than the skin."""
        L = self.L
        for side in (-1, 1):
            pts = []
            for k in range(stations):
                y = -L + 2 * L * k / (stations - 1)
                b = self.half(y) - wall / 2
                pts.append((Vector((side * max(b, 0.0), y, self.top(y) + h * 0.5)), wall * 0.9, h))
            A.loft(bm, pts, sides=4, ref=(0, 0, 1), mats=mat)

    def strake(self, bm, t, mat, w=0.035, h=0.05, out=0.02, ends=0.12, stations=15):
        """A raised strake (a wale) along each side at a fraction t from gunwale to keel."""
        L = self.L * (1 - ends)
        for side in (-1, 1):
            pts = []
            for k in range(stations):
                y = -L + 2 * L * k / (stations - 1)
                p = self.point(y, t, side)
                pts.append((p + Vector((side * out, 0, 0)), w, h))
            A.loft(bm, pts, sides=4, ref=(0, 0, 1), mats=mat)

    def ribs(self, bm, ys, mat, wall=0.06, r=0.022):
        """Frames inside an open boat, up from the keel to the gunwale."""
        for y in ys:
            pts = [(p, r, r) for p in self.section(y, wall + r)]
            A.loft(bm, pts, sides=4, ref=(0, 1, 0), mats=mat)

    def thwart(self, bm, y, below, mat, w=0.13, t=0.04, wall=0.06):
        """A seat across the boat, `below` the gunwale."""
        z = self.top(y) - below
        b = self.half(y) - wall - 0.01
        for k in range(10):                       # narrow to meet the skin where it curves in
            tt = k / 9
            p = self.point(y, tt, 1, wall)
            if p.z <= z:
                b = p.x
                break
        A.box(bm, (-b, y - w / 2, z - t), (b, y + w / 2, z), mat)

    def deck(self, bm, y0, y1, z, rnd, mat, inset=0.12, board=0.2):
        """Deck boards across the hull from y0 to y1 at height z, each as wide as the hull there."""
        y = y0
        while y < y1 - 0.02:
            w = min(board * rnd.uniform(0.85, 1.15), y1 - y)
            b = min(self.half(y), self.half(y + w)) - inset
            if b > 0.05:
                A.box(bm, (-b, y + 0.007, z - 0.06 + rnd.uniform(-0.005, 0.005)), (b, y + w - 0.007, z), mat)
            y += w


def oar(bm, a, b, mat, blade=0.4):
    """An oar from its grip at a to its blade's tip at b."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    A.loft(bm, [(a, 0.025, 0.025), (b - d * blade, 0.03, 0.03)], sides=5, ref=(0, 0, 1), mats=mat)
    side = d.cross(Vector((0, 0, 1))).normalized()
    A.hull(bm, [b - d * blade + side * s * 0.03 for s in (-1, 1)] + [b + side * s * 0.08 + Vector((0, 0, z)) for s in (-1, 1) for z in (-0.012, 0.012)]
           + [b - d * 0.1 + side * s * 0.09 for s in (-1, 1)], mat)


def furled_sail(bm, a, b, r, mat, sag=0.06):
    """A sail furled along its yard from a to b: a fat bundle hanging just under it, thicker in the
    middle, sagging a little."""
    a, b = Vector(a), Vector(b)
    pts = []
    for t in (0.0, 0.2, 0.5, 0.8, 1.0):
        p = a.lerp(b, t) - Vector((0, 0, r * 0.9 + sag * math.sin(math.pi * t)))
        rr = r * (0.55 + 0.45 * math.sin(math.pi * t))
        pts.append((p, rr, rr * 1.2))
    A.loft(bm, pts, sides=6, ref=(0, 0, 1), mats=mat)


# ---- small boats ---------------------------------------------------------------------------------

def rowing_boat(name="RowingBoat"):
    """A rowing boat, 1.5 by 3.5 tiles: a lapstrake hull with a little sheer and a small transom, tar
    below the waterline and a blue top strake, ribs and two thwarts inside, a pair of oars shipped
    along the thwarts in their rowlocks. Scenery (boarded for a short crossing, not walked on)."""
    bm = bmesh.new()
    h = Hull(3.4, 1.3, 0.48, -0.22, 0.2, bow=0.0, transom=0.28, rocker=0.12, fill_bow=2.0, fill_stern=2.6)
    h.build(bm, mats=(HULL, TAR, BLUE, OLD), band=0.2)
    h.keelson(bm, OLD, w=0.04)
    h.rail(bm, RAIL, h=0.03)
    for t in (0.35, 0.6):
        h.strake(bm, t, HULL, w=0.02, h=0.03, out=0.012)
    h.ribs(bm, (-1.1, -0.65, -0.2, 0.25, 0.7, 1.1), OLD)
    for y in (-0.7, 0.35):
        h.thwart(bm, y, 0.17, DECK)
    A.box(bm, (-0.2, -1.3, h.keel_z(0) + 0.1), (0.2, 1.1, h.keel_z(0) + 0.13), GREY)      # bottom boards
    for s in (-1, 1):
        oar(bm, (s * 0.2, -1.25, h.top(0) - 0.12), (s * 0.35, 1.6, h.top(0) - 0.06), GREY)
        A.box(bm, (s * h.half(0.05) - 0.03, -0.02, h.top(0.0)), (s * h.half(0.05) + 0.03, 0.12, h.top(0.0) + 0.07), IRON)
    return make(name, bm, MATS, smooth=35)


def fishing_boat(name="FishingBoat"):
    """A lake fishing boat, 2 by 5 tiles: a deep open hull with an ochre band, a stubby mast stepped
    forward with its lug sail furled to the yard, a rudder and tiller, three thwarts, nets heaped in
    the stern and a basket for the catch. Scenery."""
    rnd = random.Random(5)
    bm = bmesh.new()
    h = Hull(4.8, 1.9, 0.78, -0.42, 0.32, bow=0.0, transom=0.38, rocker=0.18, fill_bow=2.0, fill_stern=2.8)
    h.build(bm, mats=(HULL, TAR, YELLOW, OLD), band=0.28, wall=0.07)
    h.keelson(bm, OLD, w=0.05)
    h.rail(bm, RAIL, wall=0.07, h=0.035)
    for t in (0.3, 0.55):
        h.strake(bm, t, OLD, w=0.025, h=0.035, out=0.015)
    h.ribs(bm, (-1.7, -1.1, -0.5, 0.1, 0.7, 1.3, 1.8), OLD, wall=0.07)
    for y in (-1.3, -0.1, 1.0):
        h.thwart(bm, y, 0.24, DECK, wall=0.07)
    mast = Vector((0, 1.05, 0))
    A.prism(bm, 7, 0.08, 0.05, h.keel_z(1.0), 3.5, (mast.x, mast.y), mat_index=HULL)
    beam(bm, (0.08, 1.4, 3.25), (0.08, -0.9, 2.3), 0.06, 0.06, mat=OLD)
    furled_sail(bm, (0.08, 1.35, 3.22), (0.08, -0.85, 2.3), 0.1, SAIL)
    for s in (-1, 1):
        N.branch(bm, (s * h.half(1.0), 1.0, h.top(1.0)), (0, 1.05, 3.1), 0.012, 0.012, mat=ROPE, sides=3)
    L = h.L
    A.hull(bm, [Vector((x, -L - dy, z)) for x in (-0.03, 0.03) for dy, z in ((0.05, h.top(-L) + 0.1), (0.05, h.keel_z(-L) + 0.05),
                                                                          (0.42, h.keel_z(-L) + 0.15), (0.3, h.top(-L) - 0.15))], OLD)
    beam(bm, (0, -L - 0.05, h.top(-L) + 0.05), (0, -L + 0.8, h.top(-L) + 0.12), 0.05, 0.04, mat=OLD)
    N.blob(bm, (0.1, -1.75, h.keel_z(-1.7) + 0.45), (0.45, 0.38, 0.25), 1, rnd, mat=ROPE)
    A.prism(bm, 8, 0.2, 0.24, h.keel_z(0.5) + 0.1, h.keel_z(0.5) + 0.42, (-0.3, 0.45), mat_index=GREY)
    return make(name, bm, MATS, smooth=35)


# ---- the river barge ----------------------------------------------------------------------------

def deckhouse(bm, x0, y0, x1, y1, z, h, rnd, door="N", roof=HULL):
    """A small plank deckhouse: walls of boards (the planks texture), a door and a little window, a
    pitched board roof overhanging it, an iron stovepipe."""
    A.box(bm, (x0, y0, z), (x1, y1, z + h), DECK)
    for x in (x0, x1):                                    # corner posts
        for y in (y0, y1):
            A.box(bm, (x - 0.05, y - 0.05, z), (x + 0.05, y + 0.05, z + h + 0.02), HULL)
    dy = y1 if door == "N" else y0
    A.box(bm, (-0.35, dy - 0.02, z), (0.35, dy + 0.02, z + 1.45), OLD)
    for x in (x0, x1):
        A.box(bm, (x - 0.02, (y0 + y1) / 2 - 0.2, z + 0.9), (x + 0.02, (y0 + y1) / 2 + 0.2, z + 1.25), OLD)
    ridge = z + h + 0.5
    A.hull(bm, [Vector((x, y, zz)) for y in (y0 - 0.2, y1 + 0.2) for x, zz in ((x0 - 0.2, z + h - 0.05), (x1 + 0.2, z + h - 0.05),
                                                                            (0, ridge), (x0 - 0.2, z + h + 0.03), (x1 + 0.2, z + h + 0.03))], roof)
    A.prism(bm, 6, 0.07, 0.07, z + h, ridge + 0.5, (x1 - 0.45, y0 + 0.4), mat_index=IRON)
    A.prism(bm, 6, 0.1, 0.1, ridge + 0.5, ridge + 0.58, (x1 - 0.45, y0 + 0.4), mat_index=IRON, cap_bottom=True)


def barrel_upright(bm, x, y, z, rnd, h=0.7, r=0.24):
    prof = [(r * 0.85, 0.0), (r, h * 0.5), (r * 0.85, h)]
    rings = [[bm.verts.new((x + rr * math.cos(2 * math.pi * k / 9), y + rr * math.sin(2 * math.pi * k / 9), z + zz)) for k in range(9)]
             for rr, zz in prof]
    for a, b in zip(rings, rings[1:]):
        for k in range(9):
            bm.faces.new((a[k], a[(k + 1) % 9], b[(k + 1) % 9], b[k])).material_index = RAIL
    bm.faces.new(rings[-1]).material_index = DECK
    for zz in (0.1, h - 0.1):
        rr = r * (0.85 + 0.15 * min(zz, h - zz) / (h * 0.5)) + 0.01
        A.prism(bm, 9, rr, rr, z + zz - 0.02, z + zz + 0.02, (x, y), mat_index=IRON, cap_top=False)


def barge(name="RiverBarge"):
    """A river barge, 3 by 9 tiles: a flat-bottomed hull with raked ends and low sides, an ochre band, a
    planked deck, a deckhouse aft with a pitched roof and a stovepipe, a long sweep over the stern on
    its crutch, cargo midships (crates, barrels, sacks under a lashed tarpaulin), rope fenders over
    the side. Walkable: the deck inside its sides; the deckhouse and the cargo block."""
    rnd = random.Random(6)
    bm = bmesh.new()
    deck = 0.75
    h = Hull(8.8, 2.9, 1.2, -0.5, 0.18, bow=0.75, transom=1.0, rocker=0.3, flat=0.9, fill_bow=2.6, fill_stern=4.0)
    h.build(bm, stations=15, mats=(HULL, TAR, YELLOW, OLD), band=0.3, wall=0.08)
    h.keelson(bm, OLD, w=0.06)
    h.rail(bm, RAIL, wall=0.08, h=0.05)
    h.strake(bm, 0.3, OLD, w=0.04, h=0.06)
    h.deck(bm, -4.35, 4.35, deck, rnd, DECK, inset=0.1)
    deckhouse(bm, -1.15, -3.95, 1.15, -2.0, deck, 1.85, rnd, door="N")
    A.box(bm, (-0.08, -4.42, deck), (0.08, -4.3, deck + 1.0), HULL)                         # the sweep's crutch
    beam(bm, (0.0, -1.6, deck + 1.05), (0.0, -6.6, -0.1), 0.09, 0.09, mat=GREY)
    A.hull(bm, [Vector((x, -6.6 + y, z)) for x in (-0.03, 0.03) for y, z in ((0.6, 0.35), (-0.35, -0.45), (0.15, -0.55), (-0.5, 0.0))], GREY)
    for x, y in ((-0.6, 0.15), (0.6, 0.15), (-0.6, 1.25)):
        A.box(bm, (x - 0.32, y - 0.32, deck), (x + 0.32, y + 0.32, deck + 0.62), DECK)
        for zz in (0.18, 0.44):
            A.box(bm, (x - 0.33, y - 0.33, deck + zz - 0.02), (x + 0.33, y + 0.33, deck + zz + 0.02), HULL)
    barrel_upright(bm, 0.55, 1.2, deck, rnd)
    N.blob(bm, (0.0, 0.7, deck + 0.75), (0.95, 1.0, 0.32), 1, rnd, mat=ROPE)              # the tarpaulin over the top
    for y in (0.15, 1.2):
        A.loft(bm, [(Vector((-1.0, y, deck + 0.3)), 0.015, 0.015), (Vector((0, y, deck + 1.08)), 0.015, 0.015),
                    (Vector((1.0, y, deck + 0.3)), 0.015, 0.015)], sides=3, ref=(0, 1, 0), mats=GREY)
    for y in (-1.0, 2.6):                                                                   # fenders over the side
        x = h.half(y) + 0.05
        A.loft(bm, [(Vector((x, y, h.top(y) - 0.25)), 0.09, 0.09), (Vector((x, y, h.top(y) - 0.55)), 0.09, 0.09)], sides=6, ref=(1, 0, 0), mats=GREY)
        N.branch(bm, (x - 0.06, y, h.top(y) + 0.02), (x, y, h.top(y) - 0.26), 0.012, 0.012, mat=GREY, sides=3)
    bollard_at(bm, 0.0, 3.6, deck, IRON, scale=0.7)
    w = Walk(name)
    for dy in range(-4, 5):
        for dx in (-1, 0, 1):
            if h.half(dy + (0.5 if dy > 0 else -0.5)) - 0.12 > abs(dx) + 0.5:
                w.tile(dx, dy, deck)
    w.drop((-1, -3), (0, -3), (1, -3), (-1, -2), (0, -2), (1, -2), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1), (0, 4))
    w.ring(gaps={(1, -1, "E")})                       # boarded from the east, abaft the cargo
    return make(name, bm, MATS, w, smooth=30)


# ---- the sea ship -------------------------------------------------------------------------------

def ship(name="SeaShip"):
    """A sea-going trader, 6 by 20 tiles, bow to +Y. The hull: a strong sheer, tar below the waterline,
    two black wales and a red band under the rail, a stem and a sternpost with its rudder. On deck:
    the main deck two above the water inside its bulwarks; a stern castle one storey higher whose
    sides follow the hull, with the captain's cabin under it (a door and windows forward, windows in
    the stern) and a stair up to its deck on the starboard side; a raised forecastle; a hatch with a
    grating amidships, a capstan aft of the mainmast, barrels lashed by the mast; two masts with a top
    on the main, yards with their sails furled (she is in port), shrouds with ratlines down to the
    channels, stays to the bowsprit; an anchor at the cathead, a lantern on the stern. Walkable: the
    main deck and forecastle (plane 0), the cabin (plane 0, through its door), the castle's deck
    (plane 1, up the stair), and a hatch down into the hold (its own region)."""
    rnd = random.Random(7)
    bm = bmesh.new()
    deck, castle_z = 2.0, 2.0 + STOREY
    h = Hull(20.0, 6.0, 2.9, -1.2, 0.9, bow=0.0, transom=1.9, rocker=0.35, fill_bow=2.0, fill_stern=3.0, n=12)
    h.build(bm, stations=21, wall=0.12, band=0.75, mats=(HULL, TAR, PAINT, RAIL))
    h.keelson(bm, OLD, w=0.1)
    h.rail(bm, RAIL, wall=0.12, h=0.06)
    for t in (0.2, 0.33):
        h.strake(bm, t, TAR, w=0.06, h=0.12, out=0.04, ends=0.08)
    h.deck(bm, -9.9, 9.2, deck, rnd, DECK, inset=0.14, board=0.24)
    L = h.L
    # the rudder, hung on the sternpost
    A.hull(bm, [Vector((x, -L - dy, z)) for x in (-0.06, 0.06) for dy, z in ((0.05, h.top(-L) - 0.5), (0.05, h.keel_z(-L) + 0.1),
                                                                          (0.95, h.keel_z(-L) + 0.25), (0.5, h.top(-L) - 0.9))], OLD)
    # the stern castle: sides that follow the hull, a front with the cabin's door, its own deck and rail
    sc0, sc1 = -L, -5.5
    ys = [sc0 + (sc1 - sc0) * k / 6 for k in range(7)]
    wall_top = castle_z + 0.85
    for side in (-1, 1):
        for ya, yb in zip(ys, ys[1:]):
            xa, xb = side * (h.half(ya) - 0.06), side * (h.half(yb) - 0.06)
            A.hull(bm, [Vector((x, y, z)) for (x, y) in ((xa, ya), (xb, yb)) for z in (h.top(y) - 0.3, wall_top)] +
                   [Vector((x - side * 0.1, y, z)) for (x, y) in ((xa, ya), (xb, yb)) for z in (h.top(y) - 0.3, wall_top)], HULL)
        for k in range(2):                                                                 # windows in the sides
            y = -7.0 - 1.4 * k
            x = side * (h.half(y) + 0.0)
            A.box(bm, (x - 0.04, y - 0.22, deck + 1.0), (x + 0.04, y + 0.22, deck + 1.5), GLASS if k == 0 else TAR)
    bw = h.half(sc0) - 0.06
    A.box(bm, (-bw, sc0 - 0.05, h.top(sc0) - 0.3), (bw, sc0 + 0.08, wall_top), HULL)          # the stern
    for x in (-0.9, 0.0, 0.9):
        A.box(bm, (x - 0.28, sc0 - 0.08, deck + 0.9), (x + 0.28, sc0 - 0.03, deck + 1.55), GLASS)
        A.box(bm, (x - 0.33, sc0 - 0.1, deck + 0.85), (x + 0.33, sc0 - 0.06, deck + 0.9), RAIL)
    fw = h.half(sc1) - 0.1
    A.box(bm, (-fw, sc1 - 0.06, deck), (fw, sc1 + 0.06, castle_z), DECK)                        # the castle's front
    A.box(bm, (-0.5, sc1 + 0.05, deck), (0.5, sc1 + 0.1, deck + 1.95), OLD)                    # the cabin door
    A.box(bm, (-0.58, sc1 + 0.05, deck + 1.95), (0.58, sc1 + 0.12, deck + 2.05), RAIL)
    for x in (-1.7, 1.0):
        A.box(bm, (x - 0.25, sc1 + 0.05, deck + 1.0), (x + 0.25, sc1 + 0.1, deck + 1.5), GLASS)
    h.deck(bm, sc0 + 0.1, sc1 + 0.1, castle_z, rnd, DECK, inset=0.2, board=0.24)
    A.box(bm, (-fw, sc1 + 0.03, castle_z), (fw, sc1 + 0.13, castle_z + 0.08), RAIL)           # the front rail, on balusters
    A.box(bm, (-fw, sc1 + 0.03, castle_z + 0.82), (fw, sc1 + 0.13, castle_z + 0.9), RAIL)
    x = -fw + 0.1
    while x < fw - 0.6:
        A.box(bm, (x, sc1 + 0.05, castle_z + 0.08), (x + 0.06, sc1 + 0.11, castle_z + 0.82), RAIL)
        x += 0.32
    # the stair up to the castle, starboard side, climbing aft
    for k in range(7):
        y = sc1 + 2.0 - k * 0.28
        A.box(bm, (1.4, y - 0.28, deck), (2.35, y, deck + (k + 1) * STOREY / 7), DECK)
    beam(bm, (1.35, sc1 + 2.0, deck + 0.9), (1.35, sc1, castle_z + 0.85), 0.05, 0.05, mat=RAIL)
    # the forecastle: a step up at the bow
    fy = 6.5
    ff = h.half(fy) - 0.13
    A.box(bm, (-ff, fy - 0.05, deck), (ff, fy + 0.05, deck + 0.6), DECK)
    h.deck(bm, fy, 9.6, deck + 0.6, rnd, DECK, inset=0.16)
    # masts: the main with a top, the fore; yards with furled sails; shrouds, ratlines, stays
    masts = ((-2.0, 13.0, (6.0, 9.6), 3.4), (4.0, 10.5, (5.0, 8.2), 2.7))
    for my, mh, yard_z, span in masts:
        A.prism(bm, 8, 0.26, 0.14, h.keel_z(my), mh, (0, my), mat_index=HULL)
        A.prism(bm, 8, 0.36, 0.34, deck, deck + 0.25, (0, my), mat_index=RAIL)            # the partners
        for z, sp in zip(yard_z, (span, span * 0.72)):
            A.loft(bm, [(Vector((-sp, my, z)), 0.07, 0.07), (Vector((0, my, z + 0.04)), 0.11, 0.11), (Vector((sp, my, z)), 0.07, 0.07)],
                   sides=6, ref=(0, 0, 1), mats=OLD)
            furled_sail(bm, (-sp * 0.92, my + 0.08, z), (sp * 0.92, my + 0.08, z), 0.17, SAIL)
        for side in (-1, 1):
            cy0, cy1 = my - 0.9, my + 0.6
            cx = side * (h.half(my) + 0.18)
            ct = h.top(my) - 0.25
            A.box(bm, (min(cx, side * h.half(my)) - 0.05, cy0, ct - 0.06), (max(cx, side * h.half(my)) + 0.05, cy1, ct), OLD)  # the channel
            head = Vector((side * 0.16, my, mh * 0.72))
            feet = [Vector((cx, cy0 + (cy1 - cy0) * k / 3, ct)) for k in range(4)]
            for f in feet:
                A.prism(bm, 6, 0.07, 0.07, ct, ct + 0.1, (f.x, f.y), mat_index=TAR)             # deadeyes
                N.branch(bm, f + Vector((0, 0, 0.08)), head, 0.014, 0.014, mat=ROPE, sides=3)
            for k in range(1, 9):                                                              # ratlines
                t = k / 10
                a, b = feet[0].lerp(head, t), feet[-1].lerp(head, t)
                N.branch(bm, a, b, 0.009, 0.009, mat=ROPE, sides=3)
    top_y, top_z = -2.0, 13.0 * 0.72
    A.prism(bm, 10, 0.75, 0.75, top_z - 0.08, top_z, (0, top_y), mat_index=OLD, cap_bottom=True)
    A.prism(bm, 10, 0.75, 0.75, top_z, top_z + 0.45, (0, top_y), mat_index=HULL, cap_top=False)
    bow_tip = Vector((0, L + 4.2, deck + 2.6))
    beam(bm, (0, L - 1.5, deck + 0.7), bow_tip, 0.18, 0.18, mat=HULL)                          # the bowsprit
    for my, mh, _, _ in masts:
        N.branch(bm, Vector((0, my, mh * 0.95)), bow_tip if my > 0 else Vector((0, 4.0, deck + 0.4)), 0.016, 0.016, mat=ROPE, sides=3)
    # the anchor at the starboard cathead
    cat = Vector((h.half(L - 2.0) + 0.1, L - 2.0, h.top(L - 2.0) - 0.1))
    beam(bm, cat - Vector((0.9, 0, 0)), cat + Vector((0.35, 0, 0)), 0.14, 0.14, mat=HULL)
    shank_top = cat + Vector((0.3, 0, -0.1))
    beam(bm, shank_top, shank_top - Vector((0, 0, 1.4)), 0.07, 0.07, mat=IRON)
    for s in (-1, 1):
        A.hull(bm, [shank_top - Vector((0, 0, 1.4)) + Vector((0, s * dy, dz)) for dy, dz in ((0.0, 0.0), (0.0, 0.08), (0.45, 0.35), (0.4, 0.42))]
               + [shank_top - Vector((0, 0, 1.4)) + Vector((0.03, s * 0.42, 0.38))], IRON)
    beam(bm, shank_top + Vector((0, -0.4, -0.05)), shank_top + Vector((0, 0.4, -0.05)), 0.07, 0.07, mat=OLD)
    # the hatch with its grating, the capstan, barrels by the mainmast
    A.box(bm, (-0.62, 0.45, deck), (0.62, 1.55, deck + 0.22), HULL)
    A.box(bm, (-0.5, 0.57, deck + 0.22), (0.5, 1.43, deck + 0.23), TAR)
    for k in range(5):
        x = -0.4 + 0.2 * k
        A.box(bm, (x - 0.03, 0.57, deck + 0.23), (x + 0.03, 1.43, deck + 0.26), GREY)
    A.prism(bm, 10, 0.3, 0.24, deck, deck + 0.45, (0, -4.0), mat_index=RAIL)
    A.prism(bm, 10, 0.3, 0.3, deck + 0.45, deck + 0.62, (0, -4.0), mat_index=RAIL)
    for k in range(2):
        a = math.pi * k / 2 + 0.4
        d = Vector((math.cos(a), math.sin(a), 0))
        beam(bm, Vector((0, -4.0, deck + 0.56)) - d * 0.8, Vector((0, -4.0, deck + 0.56)) + d * 0.8, 0.05, 0.05, mat=GREY)
    for x, y in ((-2.05, -1.25), (-2.05, -0.65)):
        barrel_upright(bm, x, y, deck, rnd, h=0.65, r=0.22)
    # the boarding step inside the bulwark, where the gangplank comes over the rail (starboard)
    A.box(bm, (h.half(0) - 0.75, -0.45, deck), (h.half(0) - 0.12, 0.45, deck + 0.45), DECK)
    # a lantern on the stern
    lz = wall_top
    A.prism(bm, 6, 0.05, 0.05, lz, lz + 0.6, (0, sc0 + 0.05), mat_index=IRON)
    A.prism(bm, 6, 0.16, 0.2, lz + 0.6, lz + 1.0, (0, sc0 + 0.05), mat_index=GLASS)
    A.prism(bm, 6, 0.22, 0.03, lz + 1.0, lz + 1.15, (0, sc0 + 0.05), mat_index=IRON)
    # the walk layout
    w = Walk(name)
    for dy in range(-9, 10):
        for dx in range(-2, 3):
            edge = min(h.half(dy - 0.5), h.half(dy + 0.5))
            if edge - 0.2 > abs(dx) + 0.5:
                if dy >= 7:
                    w.tile(dx, dy, deck + 0.6)
                else:
                    w.tile(dx, dy, deck)                                                   # aft of -5: the cabin
                if dy <= -6:
                    w.tile(dx, dy, castle_z, plane=1)
    w.drop((0, -2), (0, 4), (0, 1), (0, -4), (-2, -1), (2, -4), (2, -5))                  # masts, hatch, capstan, barrels, the stair
    w.ring(gaps={(2, 0, "E")})                                                             # boarded amidships, starboard
    w.wall(-3, -5, 0, -5)                                                                  # the cabin's front, its door at 0
    w.wall(1, -5, 3, -5)
    w.ring(plane=1, gaps={(2, -6, "N")})
    w.climb("stairs", (0, 2, -3), (1, 2, -6))
    w.climb("hatch", (0, 0, 2), ("hold", 0, 0, 0))
    return make(name, bm, MATS, w, smooth=30)
