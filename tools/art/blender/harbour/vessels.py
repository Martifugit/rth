"""The harbour kit's boats and ships, built on one hull: a rowing boat, a fishing boat, a river boat and
a sea-going trader (docs/harbour-kit.md). Bow to +Y, origin amidships at the waterline.

The technique follows the owner's references (images/osrs-port-3, -4); the forms are our own. One warm
timber throughout, its planks drawn by the chunky Boards texture laid along the hull, never painted bands;
detail from a few crafted, repeated things: balustrades of small posts along every deck edge, round
portholes, a curled stem head, sails furled into tied bundles. Open boats keep their inner floor above
the waterline so the water never shows inside them.
"""
import math
import random

import bmesh
from mathutils import Vector

import rth_art as A
import basin as N
import common as C
from common import (STOREY, Walk, base_name, beam, make, rope_coil)

MATS = ["Boards", "Ash", "Fence", "Linen", "Hide", "Iron", "Glaze", "CandleWax", "DeadWood", "Driftwood"]
HULL, POST, TRIM, SAIL, ROPE, IRON, GLASS, LIGHT, DARK, TIM = range(len(MATS))


def make_vessel(name, bm, walk=None, smooth=30):
    """Make the vessel, then lay the Boards texture along it: on the hull's sides the boards run fore
    and aft (u up the side, v along the length), on the transom across it, on decks along the length."""
    ob = make(name, bm, MATS, walk, smooth=smooth)
    me = ob.data
    uv = me.uv_layers[0]
    names = [base_name(m) for m in me.materials]
    if "Boards" not in names:
        return ob
    plank = names.index("Boards")
    s = 1.0 / A.surfaces().get("Boards", {}).get("tiles", 1)
    for p in me.polygons:
        if p.material_index != plank:
            continue
        n = p.normal
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if abs(n.z) > 0.75:
                uv.data[li].uv = (co.x * s, co.y * s)
            elif abs(n.y) > 0.75:
                uv.data[li].uv = (co.z * s, co.x * s)
            else:
                uv.data[li].uv = (co.z * s, co.y * s)
    return ob


class Hull:
    """A hull's shape. length, beam: overall; depth: the gunwale's height above the water midships;
    keel: the keel's height midships; sheer: how much the gunwale rises at the ends (or `top`, a
    function of y giving the gunwale's height); bow, transom: half widths left at the ends; rocker:
    how far the keel rises toward the ends; flat: half width of a flat bottom; fill: how full the
    ends are; floor: the lowest the inside may go (above the waterline, so no water shows in it)."""

    TS = (0.0, 0.08, 0.18, 0.32, 0.5, 0.72, 1.0)

    def __init__(self, length, beam_, depth, keel, sheer=0.0, bow=0.0, transom=0.0, rocker=0.1, flat=0.0,
                 fill_bow=2.2, fill_stern=3.0, floor=0.06, top=None):
        self.L, self.B = length / 2, beam_ / 2
        self.depth, self.keel, self.sheer = depth, keel, sheer
        self.bow, self.transom, self.rocker, self.flat = bow, transom, rocker, flat
        self.fill_bow, self.fill_stern, self.floor = fill_bow, fill_stern, floor
        self._top = top

    def half(self, y):
        t = max(-1.0, min(1.0, y / self.L))
        end, fill = (self.bow, self.fill_bow) if t > 0 else (self.transom, self.fill_stern)
        return end + (self.B - end) * (1 - abs(t) ** fill)

    def top(self, y):
        return self._top(y) if self._top else self.depth + self.sheer * (y / self.L) ** 2

    def keel_z(self, y):
        return self.keel + self.rocker * abs(y / self.L) ** 3

    def point(self, y, t, side, inset=0.0):
        b = max(self.half(y) - inset, 0.005)
        zt, zk = self.top(y), self.keel_z(y) + inset
        a = (math.pi / 2) * t
        x = math.cos(a) * b
        z = zk + (zt - zk) * (1 - math.sin(a)) ** 1.5
        if abs(x) <= self.flat:
            z = zk
        if inset > 0 and self.floor is not None:
            z = max(z, self.floor)
        return Vector((side * x, y, z))

    def section(self, y, inset=0.0):
        return [self.point(y, t, -1, inset) for t in self.TS] + [self.point(y, t, 1, inset) for t in reversed(self.TS[:-1])]

    def stations(self, n):
        return [-self.L + 2 * self.L * k / (n - 1) for k in range(n)]

    def build(self, bm, stations=15, wall=0.06, inside=DARK, outside=HULL):
        """The skin: planked outside, an inner skin `wall` thick (its floor kept above the water), the
        gunwale closed between them, the transom closed."""
        ys = self.stations(stations)
        outer = [[bm.verts.new(p) for p in self.section(y)] for y in ys]
        inner = [[bm.verts.new(p) for p in self.section(y, wall)] for y in ys]
        for a, b in zip(outer, outer[1:]):
            for k in range(len(a) - 1):
                bm.faces.new((a[k], b[k], b[k + 1], a[k + 1])).material_index = outside
        for a, b in zip(inner, inner[1:]):
            for k in range(len(a) - 1):
                bm.faces.new((a[k], a[k + 1], b[k + 1], b[k])).material_index = inside
        for a, b, ia, ib in zip(outer, outer[1:], inner, inner[1:]):
            for k in (0, len(a) - 1):
                bm.faces.new((a[k], ia[k], ib[k], b[k])).material_index = TRIM
        for o, i in ((outer[0], inner[0]), (outer[-1], inner[-1])):
            for k in range(len(o) - 1):
                bm.faces.new((o[k], o[k + 1], i[k + 1], i[k])).material_index = outside
            if self.half(o[0].co.y) > 0.05:
                bm.faces.new(i).material_index = outside

    def gunwale(self, y, side, wall=0.06):
        return Vector((side * (self.half(y) - wall / 2), y, self.top(y)))

    def rail(self, bm, mat=TRIM, wall=0.06, h=0.05, n=17, y0=None, y1=None):
        """A capping rail along the gunwale on each side."""
        y0 = -self.L if y0 is None else y0
        y1 = self.L if y1 is None else y1
        for side in (-1, 1):
            pts = [(self.gunwale(y0 + (y1 - y0) * k / (n - 1), side, wall) + Vector((0, 0, h / 2)), wall * 0.95, h) for k in range(n)]
            A.loft(bm, pts, sides=4, ref=(0, 0, 1), mats=mat)

    def stem(self, bm, mat=POST, w=0.07, curl=0.5, end=1):
        """The stem (end=1) or sternpost (end=-1): up the hull's end, standing above the gunwale and
        curling back over itself at the head."""
        y = end * self.L
        zk, zt = self.keel_z(y), self.top(y)
        pts = [Vector((0, y + end * 0.02, zk)), Vector((0, y + end * 0.06, (zk + zt) / 2)), Vector((0, y + end * 0.06, zt))]
        c = Vector((0, y + end * (0.06 - curl * 0.35), zt + curl * 0.75))
        for k in range(6):                                  # the curl: round and back over
            a = math.pi * (k / 5) * 1.3
            pts.append(c + Vector((0, end * curl * 0.42 * math.cos(a), curl * 0.42 * math.sin(a) - curl * 0.1 * k / 5)))
        A.loft(bm, [(p, w, w * (1.2 if i < 3 else 0.9)) for i, p in enumerate(pts)], sides=4, ref=(1, 0, 0), mats=mat)

    def keel_strip(self, bm, mat=TRIM, w=0.05):
        L = self.L
        A.loft(bm, [(Vector((0, y, self.keel_z(y) - w * 0.5)), w, w * 0.8) for y in (-L, -L * 0.5, 0, L * 0.5, L)], sides=4, ref=(0, 0, 1), mats=mat)

    def ribs(self, bm, ys, mat=TRIM, wall=0.06, r=0.022):
        for y in ys:
            A.loft(bm, [(p, r, r) for p in self.section(y, wall + r)], sides=4, ref=(0, 1, 0), mats=mat)

    def thwart(self, bm, y, below, mat=POST, w=0.14, t=0.045, wall=0.06):
        z = self.top(y) - below
        b = self.half(y) - wall
        for k in range(12):
            p = self.point(y, k / 11, 1, wall)
            if p.z <= z:
                b = p.x
                break
        A.box(bm, (-b, y - w / 2, z - t), (b, y + w / 2, z), mat)

    def deck(self, bm, y0, y1, z, inset=0.1, n=12, mat=HULL):
        """A deck as one planked surface following the hull's width, from y0 to y1 at height z, with
        a thickness under it."""
        ys = [y0 + (y1 - y0) * k / (n - 1) for k in range(n)]
        rows = [(bm.verts.new((-(self.half(y) - inset), y, z)), bm.verts.new(((self.half(y) - inset), y, z))) for y in ys]
        under = [(bm.verts.new((-(self.half(y) - inset), y, z - 0.08)), bm.verts.new(((self.half(y) - inset), y, z - 0.08))) for y in ys]
        for (a0, a1), (b0, b1) in zip(rows, rows[1:]):
            bm.faces.new((a0, a1, b1, b0)).material_index = mat
        for (a0, a1), (b0, b1) in zip(under, under[1:]):
            bm.faces.new((b0, b1, a1, a0)).material_index = TRIM
        for i in (0, len(ys) - 1):
            bm.faces.new((rows[i][0], rows[i][1], under[i][1], under[i][0])).material_index = TRIM
        for k in range(len(ys) - 1):
            for s in (0, 1):
                bm.faces.new((rows[k][s], rows[k + 1][s], under[k + 1][s], under[k][s])).material_index = TRIM

    def patch(self, bm, y, z, side, rnd, mat=DARK, w=0.7, h=0.26):
        """A patch over a sprung plank: a short board of another timber nailed onto the side."""
        pts = []
        for dy in (-w / 2, w / 2):
            for dz in (-h / 2, h / 2):
                p = min((self.point(y + dy, k / 40, side) for k in range(41)), key=lambda q: abs(q.z - (z + dz)))
                pts += [p + Vector((side * 0.01, 0, 0)), p + Vector((side * 0.045, 0, 0))]
        A.hull(bm, pts, mat)

    def patches(self, bm, rnd, n, z0, z1, mat=DARK):
        """Worn: `n` patches on each side, between heights z0 and z1."""
        if not C.WEAR:
            return
        for side in (-1, 1):
            for _ in range(n):
                self.patch(bm, rnd.uniform(-self.L * 0.6, self.L * 0.6), rnd.uniform(z0, z1), side, rnd, mat)

    def porthole(self, bm, y, z, side, r=0.17, broken=False):
        """A round porthole in the hull's side: a ring of timber and the glass in it (broken: dark)."""
        best = min((self.point(y, k / 40, side) for k in range(41)), key=lambda p: abs(p.z - z))
        n = Vector((side, 0, 0))
        A.loft(bm, [(best - n * 0.03, r, r), (best + n * 0.05, r, r)], sides=10, ref=(0, 0, 1), mats=POST)
        A.loft(bm, [(best + n * 0.05, r * 0.7, r * 0.7), (best + n * 0.065, r * 0.7, r * 0.7)], sides=10, ref=(0, 0, 1),
               mats=DARK if broken else GLASS)


def split_path(path, t0, t1):
    """The parts of a path before fraction t0 and after t1 of its length."""
    path = [Vector(p) for p in path]
    seg = [(a, b, (b - a).length) for a, b in zip(path, path[1:])]
    total = sum(l for _, _, l in seg)

    def at(d):
        for a, b, l in seg:
            if d <= l + 1e-6:
                return a.lerp(b, d / l if l else 0)
            d -= l
        return path[-1]
    before, after = [path[0]], []
    acc = 0.0
    for a, b, l in seg:
        if acc + l < t0 * total:
            before.append(b)
        acc += l
    before.append(at(t0 * total))
    after.append(at(t1 * total))
    acc = 0.0
    for a, b, l in seg:
        acc += l
        if acc > t1 * total:
            after.append(b)
    return before, after


def balustrade(bm, path, h=0.75, spacing=0.3, mat=POST, rail=TRIM, gap=None):
    """A balustrade along a path of points (the line it stands on): small square posts every
    `spacing`, a bottom rail and a broader top rail. The references' signature piece. gap: (t0, t1),
    the fractions of its length where it is broken: posts gone, the rail snapped and hanging."""
    if gap:
        before, after = split_path(path, *gap)
        balustrade(bm, before, h, spacing, mat, rail)
        balustrade(bm, after, h, spacing, mat, rail)
        a, b = before[-1], after[0]
        beam(bm, a + Vector((0, 0, h)), a.lerp(b, 0.45) + Vector((0, 0, 0.12)), 0.06, 0.08, mat=rail)    # the rail, hanging
        A.box(bm, b.lerp(a, 0.4) + Vector((-0.03, -0.03, 0.03)), b.lerp(a, 0.4) + Vector((0.03, 0.03, 0.3)), mat)   # a snapped post
        return
    path = [Vector(p) for p in path]
    A.loft(bm, [(p + Vector((0, 0, 0.03)), 0.06, 0.05) for p in path], sides=4, ref=(0, 0, 1), mats=rail)
    A.loft(bm, [(p + Vector((0, 0, h)), 0.09, 0.06) for p in path], sides=4, ref=(0, 0, 1), mats=rail)
    seg = [(a, b, (b - a).length) for a, b in zip(path, path[1:])]
    total = sum(l for _, _, l in seg)
    count = max(2, int(total / spacing) + 1)
    for k in range(count):
        d = total * k / (count - 1)
        p = path[-1]
        for a, b, l in seg:
            if d <= l + 1e-6:
                p = a.lerp(b, d / l if l else 0)
                break
            d -= l
        big = k in (0, count - 1)
        s = 0.045 if big else 0.028
        A.box(bm, p + Vector((-s, -s, 0.03)), p + Vector((s, s, h + (0.06 if big else 0))), mat)


def furled_sail(bm, a, b, r, mat=SAIL, ties=4, rope=ROPE):
    """A sail furled along its yard from a to b: bundles bulging between the ties that gather it,
    hung just under the yard."""
    a, b = Vector(a), Vector(b)
    pts = []
    n = ties * 4
    for k in range(n + 1):
        t = k / n
        bulge = abs(math.sin(math.pi * t * ties))
        env = 0.55 + 0.45 * math.sin(math.pi * t)
        rr = r * env * (0.45 + 0.55 * bulge)
        pts.append((a.lerp(b, t) - Vector((0, 0, r * 0.85 * env)), rr, rr * 1.15))
    A.loft(bm, pts, sides=7, ref=(0, 0, 1), mats=mat)
    for k in range(1, ties):
        p = a.lerp(b, k / ties) - Vector((0, 0, r * 0.4))
        d = (b - a).normalized() * 0.02
        A.loft(bm, [(p - d, r * 0.42, r * 0.5), (p + d, r * 0.42, r * 0.5)], sides=6, ref=(0, 0, 1), mats=rope)


def mast(bm, x, y, z0, z1, r0, r1, mat=POST):
    A.prism(bm, 8, r0, r1, z0, z1, (x, y), mat_index=mat)
    A.prism(bm, 8, r1 * 1.4, r1 * 1.1, z1, z1 + 0.12, (x, y), mat_index=TRIM)


def yard(bm, y, z, span, sail_r, mat=POST):
    A.loft(bm, [(Vector((-span, y, z)), 0.06, 0.06), (Vector((0, y, z + 0.03)), 0.1, 0.1), (Vector((span, y, z)), 0.06, 0.06)],
           sides=6, ref=(0, 0, 1), mats=mat)
    furled_sail(bm, (-span * 0.9, y + 0.03, z), (span * 0.9, y + 0.03, z), sail_r)


def oar(bm, a, b, mat=POST, blade=0.42):
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    A.loft(bm, [(a, 0.025, 0.025), (b - d * blade, 0.03, 0.03)], sides=5, ref=(0, 0, 1), mats=mat)
    side = d.cross(Vector((0, 0, 1))).normalized()
    A.hull(bm, [b - d * blade + side * s * 0.03 for s in (-1, 1)] + [b + side * s * 0.08 + Vector((0, 0, z)) for s in (-1, 1) for z in (-0.012, 0.012)]
           + [b - d * 0.1 + side * s * 0.09 for s in (-1, 1)], mat)


def barrel_upright(bm, x, y, z, h=0.7, r=0.24):
    rings = [[bm.verts.new((x + rr * math.cos(2 * math.pi * k / 10), y + rr * math.sin(2 * math.pi * k / 10), z + zz)) for k in range(10)]
             for rr, zz in ((r * 0.85, 0.0), (r, h * 0.5), (r * 0.85, h))]
    for a, b in zip(rings, rings[1:]):
        for k in range(10):
            bm.faces.new((a[k], a[(k + 1) % 10], b[(k + 1) % 10], b[k])).material_index = TRIM
    bm.faces.new(rings[-1]).material_index = POST
    for zz in (0.1, h - 0.1):
        rr = r * (0.85 + 0.15 * min(zz, h - zz) / (h * 0.5)) + 0.01
        A.prism(bm, 10, rr, rr, z + zz - 0.02, z + zz + 0.02, (x, y), mat_index=IRON, cap_top=False)


def crate(bm, x, y, z, s=0.3):
    A.box(bm, (x - s, y - s, z), (x + s, y + s, z + 2 * s), HULL)
    for zz in (0.0, 2 * s - 0.05):
        A.box(bm, (x - s - 0.01, y - s - 0.01, z + zz), (x + s + 0.01, y + s + 0.01, z + zz + 0.05), TRIM)


# ---- small boats ---------------------------------------------------------------------------------

def rowing_boat(name="RowingBoat"):
    """A rowing boat, 1.5 by 3.5 tiles: a planked hull with a little sheer, a small transom and a stem
    that stands up in a small curl, ribs and two thwarts inside over bottom boards (all above the
    water), a pair of oars shipped along the thwarts in their rowlocks. Scenery."""
    bm = bmesh.new()
    h = Hull(3.4, 1.35, 0.5, -0.22, 0.2, bow=0.0, transom=0.3, rocker=0.12, fill_bow=2.0, fill_stern=2.6, floor=0.08)
    h.build(bm)
    h.rail(bm, h=0.04)
    h.stem(bm, w=0.05, curl=0.12)
    h.keel_strip(bm)
    h.ribs(bm, (-1.0, -0.5, 0.0, 0.5, 1.0))
    for y in (-0.7, 0.35):
        h.thwart(bm, y, 0.17)
    A.box(bm, (-0.25, -1.2, 0.08), (0.25, 1.0, 0.11), DARK)
    h.patches(bm, C.wear_rnd(name), 1, 0.12, 0.3, mat=TIM)
    for s in (-1, 1):
        oar(bm, (s * 0.2, -1.25, h.top(0) - 0.12), (s * 0.36, 1.6, h.top(0) - 0.06))
        A.box(bm, (s * h.half(0.05) - 0.03, -0.02, h.top(0.0)), (s * h.half(0.05) + 0.03, 0.12, h.top(0.0) + 0.07), IRON)
    return make_vessel(name, bm, smooth=35)


def fishing_boat(name="FishingBoat"):
    """A lake fishing boat, 2 by 5 tiles: a deep planked hull, a curled stem head, a stubby mast stepped
    forward with its lug sail furled to the yard, a rudder and tiller, three thwarts, nets heaped aft
    and a basket for the catch, the inside kept above the water. Scenery."""
    rnd = random.Random(5)
    bm = bmesh.new()
    h = Hull(4.8, 1.95, 0.8, -0.42, 0.32, bow=0.0, transom=0.4, rocker=0.18, fill_bow=2.0, fill_stern=2.8, floor=0.1)
    h.build(bm, wall=0.07)
    h.rail(bm, wall=0.07, h=0.05)
    h.stem(bm, w=0.06, curl=0.2)
    h.keel_strip(bm)
    h.ribs(bm, (-1.6, -0.9, -0.2, 0.5, 1.2, 1.8), wall=0.07)
    for y in (-1.3, -0.1, 1.0):
        h.thwart(bm, y, 0.25, wall=0.07)
    A.box(bm, (-0.35, -1.9, 0.1), (0.35, 1.7, 0.13), DARK)
    h.patches(bm, C.wear_rnd(name), 2, 0.15, 0.6, mat=TIM)
    mast(bm, 0, 1.05, 0.1, 3.4, 0.08, 0.05)
    beam(bm, (0.1, 1.45, 3.2), (0.1, -0.9, 2.3), 0.06, 0.06, mat=POST)
    furled_sail(bm, (0.1, 1.38, 3.17), (0.1, -0.83, 2.33), 0.11, ties=3)
    for s in (-1, 1):
        N.branch(bm, (s * h.half(1.0), 1.0, h.top(1.0)), (0, 1.05, 3.1), 0.012, 0.012, mat=ROPE, sides=3)
    L = h.L
    A.hull(bm, [Vector((x, -L - dy, z)) for x in (-0.03, 0.03) for dy, z in ((0.05, h.top(-L) + 0.1), (0.05, h.keel_z(-L) + 0.05),
                                                                          (0.42, h.keel_z(-L) + 0.15), (0.3, h.top(-L) - 0.15))], POST)
    beam(bm, (0, -L - 0.05, h.top(-L) + 0.05), (0, -L + 0.8, h.top(-L) + 0.12), 0.05, 0.04, mat=POST)
    N.blob(bm, (0.1, -1.75, 0.35), (0.42, 0.36, 0.22), 1, rnd, mat=ROPE)
    A.prism(bm, 8, 0.2, 0.24, 0.13, 0.45, (-0.3, 0.45), mat_index=TRIM)
    return make_vessel(name, bm, smooth=35)


# ---- the river boat -----------------------------------------------------------------------------

def awning(bm, y0, y1, z, half, hoops=3, mat=ROPE, hoop_mat=POST):
    """A hide awning on bent hoops over the cargo: a barrel vault from y0 to y1, its foot at z."""
    n = 8
    rows = []
    for k in range(hoops * 2 + 1):
        y = y0 + (y1 - y0) * k / (hoops * 2)
        sag = 0.0 if k % 2 == 0 else 0.06
        rows.append([bm.verts.new((-half * math.cos(math.pi * j / n), y, z + (half * 0.85 - sag) * math.sin(math.pi * j / n))) for j in range(n + 1)])
    for a, b in zip(rows, rows[1:]):
        for j in range(n):
            bm.faces.new((a[j], a[j + 1], b[j + 1], b[j])).material_index = mat
    for k in range(0, hoops * 2 + 1, 2):
        y = y0 + (y1 - y0) * k / (hoops * 2)
        A.loft(bm, [(Vector((-half * math.cos(math.pi * j / n) * 1.03, y, z + half * 0.88 * math.sin(math.pi * j / n))), 0.03, 0.03)
                    for j in range(n + 1)], sides=4, ref=(0, 1, 0), mats=hoop_mat)


def barge(name="RiverBarge"):
    """A river boat, 3 by 9 tiles: a long, shallow, double-ended planked hull rising to tall stem and
    stern posts that curl over at their heads; a deck inside low sides; a mast amidships with its
    square sail furled on the yard; a steering oar over the starboard quarter on its crutch; cargo
    under a hide awning on hoops, barrels and crates by it, rope fenders over the side. Walkable: the
    deck; the awning, the mast and the cargo block."""
    bm = bmesh.new()
    deck = 0.62
    h = Hull(9.0, 3.0, 0.95, -0.35, 0.65, bow=0.0, transom=0.0, rocker=0.35, flat=0.7, fill_bow=2.2, fill_stern=2.2, floor=deck - 0.05)
    h.build(bm, stations=17, wall=0.08)
    h.rail(bm, wall=0.08, h=0.06)
    h.keel_strip(bm)
    for end in (1, -1):
        h.stem(bm, w=0.09, curl=0.45, end=end)
    h.deck(bm, -3.9, 3.9, deck, inset=0.1)
    h.patches(bm, C.wear_rnd(name), 2, 0.2, 0.75, mat=TIM)
    for y in (-2.6, -0.9, 0.9, 2.6):                                          # frames showing inside the sides
        for side in (-1, 1):
            beam(bm, h.point(y, 0.4, side, 0.09), h.point(y, 0.0, side, 0.09) + Vector((0, 0, 0.04)), 0.06, 0.06, mat=TRIM)
    mast(bm, 0, 1.5, deck, 6.2, 0.11, 0.07)
    yard(bm, 1.5, 5.6, 1.8, 0.16)
    for side in (-1, 1):
        for dy in (-0.4, 0.4):
            N.branch(bm, h.gunwale(1.5 + dy, side), Vector((0, 1.5, 5.8)), 0.012, 0.012, mat=ROPE, sides=3)
    N.branch(bm, Vector((0, 1.5, 6.1)), Vector((0, h.L, h.top(h.L) + 0.2)), 0.012, 0.012, mat=ROPE, sides=3)
    awning(bm, -2.4, 0.4, deck, 1.05)
    for x, y in ((-0.55, -1.8), (0.5, -1.6), (-0.5, -0.4)):
        crate(bm, x, y, deck, s=0.26)
    barrel_upright(bm, 0.5, -0.5, deck, h=0.62, r=0.22)
    stern = -3.4
    A.box(bm, (h.half(stern) - 0.16, stern - 0.08, h.top(stern)), (h.half(stern) - 0.02, stern + 0.08, h.top(stern) + 0.45), POST)
    tip = Vector((h.half(stern) + 0.35, stern - 1.4, -0.3))
    beam(bm, (0.4, stern + 1.6, deck + 1.0), tip, 0.08, 0.08, mat=POST)                       # the steering oar
    A.hull(bm, [tip + Vector((x, y, z)) for x in (-0.03, 0.03) for y, z in ((0.5, 0.35), (-0.4, -0.35), (0.1, -0.45), (-0.5, 0.05))], POST)
    for y in (-1.3, 2.2):
        x = h.half(y) + 0.06
        A.loft(bm, [(Vector((x, y, h.top(y) - 0.2)), 0.09, 0.09), (Vector((x, y, h.top(y) - 0.5)), 0.09, 0.09)], sides=6, ref=(1, 0, 0), mats=ROPE)
        N.branch(bm, (x - 0.06, y, h.top(y) + 0.02), (x, y, h.top(y) - 0.22), 0.012, 0.012, mat=ROPE, sides=3)
    rope_coil(bm, (-0.6, 3.0, deck), 0.2, ROPE)
    w = Walk(name)
    for dy in range(-3, 4):
        for dx in (-1, 0, 1):
            yy = dy + (0.5 if dy > 0 else -0.5)
            if h.half(yy) - 0.12 > abs(dx) + 0.5:
                w.tile(dx, dy, deck)
    w.drop((0, 2), (0, 1), (-1, -2), (0, -2), (1, -2), (-1, -1), (0, -1), (1, -1), (-1, 0), (0, 0), (1, 0))
    w.ring(gaps={(1, 1, "E")})
    return make_vessel(name, bm, w)


# ---- the sea ship -------------------------------------------------------------------------------

def ship(name="SeaShip"):
    """A sea-going trader, 6 by 20 tiles, bow to +Y. Her planked sides rise from the main deck's
    height amidships to enclose a raised forecastle forward and a tall stern castle aft (the captain's
    cabin under it, its deck one storey up), so hull and castle are one piece; balustrades run along
    every deck edge; round portholes light the cabin; the stem curls at its head under a long
    bowsprit; two masts with a top on the main, yards with their sails furled in tied bundles, a few
    shrouds with ratlines; a rudder on the sternpost, a lantern on the stern, windows in the transom;
    on deck a hatch with its grating, a capstan, barrels and crates. Walkable: the main deck and
    forecastle (plane 0), the cabin (plane 0, through its door), the castle's deck (plane 1, up the
    stair), and a hatch down into the hold (its own region)."""
    bm = bmesh.new()
    deck, castle_z = 2.0, 2.0 + STOREY
    fore_z = deck + 0.6
    sc1, fy = -5.5, 6.5                         # the castle's front, the forecastle's step

    def top(y):
        if y < sc1 - 0.4:
            return castle_z + 0.1
        if y < sc1:
            return deck + 0.1 + (castle_z - deck) * (sc1 - y) / 0.4
        if y < fy:
            return deck + 0.1
        if y < fy + 0.4:
            return deck + 0.1 + 0.6 * (y - fy) / 0.4
        return fore_z + 0.1 + 0.25 * ((y - fy - 0.4) / 3.0) ** 2
    h = Hull(20.0, 6.4, 0, -1.2, bow=0.0, transom=2.3, rocker=0.4, fill_bow=1.9, fill_stern=3.2, floor=None, top=top)
    h.build(bm, stations=41, wall=0.14)
    h.keel_strip(bm, w=0.1)
    h.stem(bm, w=0.13, curl=0.35)
    L = h.L
    for y0, y1, n in ((-L, sc1 - 0.4, 9), (sc1 - 0.4, fy + 0.4, 17), (fy + 0.4, L - 0.2, 7)):   # the capping rail, in three runs
        h.rail(bm, wall=0.14, h=0.07, y0=y0, y1=y1, n=n)
    h.deck(bm, -L + 0.2, L - 0.4, deck, inset=0.16, n=30)
    h.deck(bm, fy, L - 0.5, fore_z, inset=0.18, n=8)
    h.deck(bm, -L + 0.15, sc1, castle_z, inset=0.18, n=8)
    # balustrades along the sides of each deck, across the castle's front and the forecastle's back
    rnd = C.wear_rnd(name)
    for side in (-1, 1):
        for k, ys in enumerate(((sc1 + 0.1, -2.5, 0.5, 3.5, fy - 0.1), (-L + 0.15, -8.0, -6.5, sc1 - 0.45), (fy + 0.45, 8.0, L - 1.2))):
            gap = (0.62, 0.7) if (C.WEAR and side < 0 and k == 0) else None    # a stretch stove in, to port
            balustrade(bm, [h.gunwale(y, side, 0.14) + Vector((-side * 0.03, 0, 0.07)) for y in ys], gap=gap)
    h.patches(bm, rnd, 3, 0.4, 1.6, mat=TIM)
    fw = h.half(sc1) - 0.2
    balustrade(bm, [Vector((-fw, sc1 + 0.05, castle_z)), Vector((0.9, sc1 + 0.05, castle_z))])          # open at the stair's head
    ff = h.half(fy) - 0.2
    balustrade(bm, [Vector((-ff, fy + 0.05, fore_z)), Vector((ff, fy + 0.05, fore_z))], h=0.6)
    # the castle's front: planked, the cabin's door and two windows; the forecastle's riser
    A.box(bm, (-fw, sc1 - 0.06, deck), (fw, sc1 + 0.04, castle_z), HULL)
    A.box(bm, (-0.5, sc1 + 0.03, deck), (0.5, sc1 + 0.08, deck + 1.95), DARK)
    A.box(bm, (-0.6, sc1 + 0.03, deck + 1.95), (0.6, sc1 + 0.1, deck + 2.07), TRIM)
    for x in (-1.6, 0.9):
        A.box(bm, (x - 0.24, sc1 + 0.03, deck + 1.0), (x + 0.24, sc1 + 0.08, deck + 1.5), GLASS)
        A.box(bm, (x - 0.3, sc1 + 0.04, deck + 0.94), (x + 0.3, sc1 + 0.09, deck + 1.0), TRIM)
    A.box(bm, (-ff, fy - 0.05, deck), (ff, fy + 0.05, fore_z), HULL)
    # the stair up to the castle, starboard, climbing aft, with its handrail
    for k in range(7):
        y = sc1 + 2.0 - k * 0.28
        A.box(bm, (1.45, y - 0.28, deck), (2.35, y, deck + (k + 1) * STOREY / 7), HULL)
    balustrade(bm, [Vector((1.4, sc1 + 2.0, deck)), Vector((1.4, sc1 + 0.05, castle_z))], h=0.8, spacing=0.5)
    # portholes along the cabin and the hold; windows in the transom; the rudder; the stern lantern
    for side in (-1, 1):
        for y in (-8.6, -7.1):
            h.porthole(bm, y, deck + 1.2, side, r=0.2)
        for y in (-2.0, 1.5, 4.5):
            h.porthole(bm, y, deck - 0.55, side, r=0.15, broken=(C.WEAR and side > 0 and y > 0 and y < 2))
    tw = h.half(-L) - 0.3
    for x in (-tw * 0.6, 0.0, tw * 0.6):
        A.box(bm, (x - 0.3, -L - 0.08, deck + 0.9), (x + 0.3, -L - 0.02, deck + 1.6), GLASS)
        A.box(bm, (x - 0.36, -L - 0.1, deck + 0.84), (x + 0.36, -L - 0.03, deck + 0.9), TRIM)
        A.box(bm, (x - 0.36, -L - 0.1, deck + 1.6), (x + 0.36, -L - 0.03, deck + 1.66), TRIM)
    A.hull(bm, [Vector((x, -L - dy, z)) for x in (-0.07, 0.07) for dy, z in ((0.05, castle_z - 0.6), (0.05, h.keel_z(-L) + 0.1),
                                                                          (1.0, h.keel_z(-L) + 0.25), (0.55, castle_z - 1.2))], TRIM)
    lz = castle_z + 0.85
    A.prism(bm, 6, 0.05, 0.05, castle_z + 0.1, lz + 0.5, (0, -L + 0.3), mat_index=IRON)
    A.prism(bm, 6, 0.17, 0.21, lz + 0.5, lz + 0.95, (0, -L + 0.3), mat_index=LIGHT)
    A.prism(bm, 6, 0.23, 0.03, lz + 0.95, lz + 1.1, (0, -L + 0.3), mat_index=IRON)
    # masts: the main with a top; yards and furled sails; shrouds with ratlines; stays; the bowsprit
    masts = ((-2.0, 13.0, (6.6, 10.0), 3.6), (4.0, 10.5, (5.6, 8.4), 2.9))
    for my, mh, yard_z, span in masts:
        mast(bm, 0, my, 0.0, mh, 0.27, 0.15)
        A.prism(bm, 8, 0.4, 0.36, deck, deck + 0.22, (0, my), mat_index=TRIM)
        for z, sp in zip(yard_z, (span, span * 0.72)):
            yard(bm, my, z, sp, 0.2 if sp == span else 0.15)
        for side in (-1, 1):
            foot = [h.gunwale(my + dy, side, 0.14) + Vector((side * 0.12, 0, 0)) for dy in (-0.8, -0.2, 0.4)]
            head = Vector((side * 0.17, my, mh * 0.74))
            for f in foot:
                A.prism(bm, 6, 0.08, 0.08, f.z - 0.1, f.z + 0.05, (f.x, f.y), mat_index=TRIM)
                N.branch(bm, f, head, 0.018, 0.018, mat=ROPE, sides=3)
            for k in range(1, 7):
                t = k / 8
                N.branch(bm, foot[0].lerp(head, t), foot[-1].lerp(head, t), 0.011, 0.011, mat=ROPE, sides=3)
    top_z = 13.0 * 0.74
    A.prism(bm, 10, 0.8, 0.8, top_z - 0.1, top_z, (0, -2.0), mat_index=TRIM, cap_bottom=True)
    balustrade(bm, [Vector((0.8 * math.cos(a), -2.0 + 0.8 * math.sin(a), top_z)) for a in (2 * math.pi * k / 10 for k in range(11))],
               h=0.45, spacing=0.3)
    bow_tip = Vector((0, L + 4.5, fore_z + 2.4))
    beam(bm, (0, L - 1.8, fore_z + 0.3), bow_tip, 0.2, 0.2, mat=POST)
    yard(bm, L + 2.6, fore_z + 1.2, 1.4, 0.12)
    N.branch(bm, Vector((0, 4.0, 10.5 * 0.97)), bow_tip, 0.018, 0.018, mat=ROPE, sides=3)
    N.branch(bm, Vector((0, -2.0, 13.0 * 0.97)), Vector((0, 4.0, deck + 0.5)), 0.018, 0.018, mat=ROPE, sides=3)
    # on deck: the hatch and its grating, the capstan, barrels and crates
    A.box(bm, (-0.65, 0.45, deck), (0.65, 1.55, deck + 0.24), TRIM)
    A.box(bm, (-0.52, 0.58, deck + 0.24), (0.52, 1.42, deck + 0.25), DARK)
    for k in range(5):
        x = -0.4 + 0.2 * k
        A.box(bm, (x - 0.03, 0.58, deck + 0.25), (x + 0.03, 1.42, deck + 0.28), POST)
    A.prism(bm, 10, 0.3, 0.24, deck, deck + 0.45, (0, -4.0), mat_index=TRIM)
    A.prism(bm, 10, 0.32, 0.32, deck + 0.45, deck + 0.62, (0, -4.0), mat_index=TRIM)
    for k in range(2):
        a = math.pi * k / 2 + 0.4
        d = Vector((math.cos(a), math.sin(a), 0))
        beam(bm, Vector((0, -4.0, deck + 0.56)) - d * 0.8, Vector((0, -4.0, deck + 0.56)) + d * 0.8, 0.05, 0.05, mat=POST)
    for x, y in ((-2.05, -1.25), (-2.05, -0.65)):
        barrel_upright(bm, x, y, deck, h=0.65, r=0.22)
    crate(bm, -2.0, 3.0, deck, s=0.28)
    crate(bm, -2.0, 3.0, deck + 0.56, s=0.24)
    # the boarding step inside the rail, where the gangplank comes over (starboard)
    A.box(bm, (h.half(0) - 0.75, -0.45, deck), (h.half(0) - 0.16, 0.45, deck + 0.35), HULL)
    # the walk layout
    w = Walk(name)
    for dy in range(-9, 10):
        for dx in range(-2, 3):
            edge = min(h.half(dy - 0.5), h.half(dy + 0.5))
            if edge - 0.3 > abs(dx) + 0.5:
                w.tile(dx, dy, fore_z if dy >= 7 else deck)
                if dy <= -6:
                    w.tile(dx, dy, castle_z, plane=1)
    w.drop((0, -2), (0, 4), (0, 1), (0, -4), (-2, -1), (-2, 3), (2, -4), (2, -5))
    w.ring(gaps={(2, 0, "E")})
    w.wall(-3, -5, 0, -5)
    w.wall(1, -5, 3, -5)
    w.ring(plane=1, gaps={(2, -6, "N")})
    w.climb("stairs", (0, 2, -3), (1, 2, -6))
    w.climb("hatch", (0, 0, 2), ("hold", 0, 0, 0))
    return make_vessel(name, bm, w)
