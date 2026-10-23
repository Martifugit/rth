"""Cruor's building kit: the parts its architecture is made of, reusable at any length and height.
The style and the decisions behind it are in docs/regions/cruor-architecture.md; read that first.
In short: one pale stone on thin walls, merlons along every wall top, one pinnacle shape, small
lancets, chamfered and corbelled towers, slate roofs whose gables are their own ends, red only
for the faith (its glass). The cathedral (cathedral.py) is built from these parts too.

Parts (each adds to a Build; 1 unit = 1 tile, Z up):
  wall           a skin with a plinth and merlons, along any line
  city_wall      a thick, walkable curtain with merlons on both edges and slits
  buttress       the pier with the one pinnacle
  pinnacle       the one pinnacle: a square pyramid five half-widths tall
  battlement     merlons along a wall top (also crowns a wall cut for the inside view)
  lancet         a pointed window in a thin frame (red glass for the faith, dark otherwise)
  doorway        a pointed door in a deeper frame
  tower          chamfered and corbelled, battlemented, slits; a spire or a belfry and spire
  hall           a rectangular building: walls, buttresses, lancets, gable ends, slate roof
  gatehouse      two towers, a gate arch with a raised portcullis, a battlemented top
  townhouse      a house: stone ground floor, jettied plaster and timber (or all stone); a
                 single-storey plaster house is a cottage
and shapes on wall faces (frame, arch_panel, arch_frame, arch_spandrel), merlon_parts, and
Layered: a building split into layers for walking in (see its docstring).

piece(name, fn, ...) turns a part into an object; paint(objs) puts Cruor's textures on
(cruor/textures.py writes them). See showcase.py for every part in use."""
import itertools
import math
import os
import sys

import bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
import rth_art as A  # noqa: E402

# Flat palette colours for judging shapes. paint() swaps some of them for Cruor's textures.
MATS = ["Gravestone", "Ashgrey", "Sable", "Iron", "CryptFloor", "Blood", "Yew", "CryptBanner", "Plaster", "Umber"]
STONE, ROOF, DARK, METAL, FLOOR, GLASS, DOOR, RUNNER, PLASTER, TIMBER = range(len(MATS))

# Flat colour -> (texture in cruor/textures, tiles one repeat covers along, up).
PAINT = {"Gravestone": ("CruorStone", 2, 2), "Ashgrey": ("CruorSlate", 2, 1), "Blood": ("CruorGlass", 1, 1),
         "CryptFloor": ("CruorFloor", 4, 4), "Plaster": ("CruorPlaster", 1, 2.3)}
GLOW = {"Blood": 0.22}        # the faith's glass glows a little

WT = 0.25                     # a wall is a skin
SLATE_PITCH = 55.0            # roofs: steep
STOREY = 2.3                  # docs/world-scale.md, D2
SMOOTH = 50                   # light across edges flatter than this (degrees) as one surface


class Build:
    """A bmesh with shape helpers; every shape takes a material index."""

    def __init__(self):
        self.bm = bmesh.new()

    def box(self, x0, y0, z0, x1, y1, z1, m):
        A.box(self.bm, (min(x0, x1), min(y0, y1), z0), (max(x0, x1), max(y0, y1), z1), m)

    def hull(self, pts, m):
        A.hull(self.bm, [tuple(p) for p in pts], m)

    def cyl(self, cx, cy, r, z0, z1, m, sides=12):
        A.prism(self.bm, sides, r, r, z0, z1, (cx, cy), mat_index=m, cap_bottom=True)

    def cone(self, cx, cy, r, z0, z1, m, sides=8):
        A.loft(self.bm, [((cx, cy, z0), r, r), ((cx, cy, z1), 0, 0)], sides=sides, ref=(0, 1, 0), mats=m)

    def gable_roof(self, x0, x1, y0, y1, z, rise, m, along="x", over=0.3):
        """A pitched roof over the rectangle, ridge along `along`, eaves `over` past the walls."""
        pts = [(x, y, z) for x in (x0 - over, x1 + over) for y in (y0 - over, y1 + over)]
        if along == "x":
            ym = (y0 + y1) / 2
            pts += [(x, ym, z + rise) for x in (x0 - over, x1 + over)]
        else:
            xm = (x0 + x1) / 2
            pts += [(xm, y, z + rise) for y in (y0 - over, y1 + over)]
        self.hull(pts, m)


def arch(w, spring, n=None):
    """The right half of a pointed arch of half-width w springing at z = spring, as (s, z) points
    from (w, spring) up to the apex at s = 0 (radius 1.6 w: a little sharper than equilateral), in
    n segments: 3 for small openings, 5 for big ones."""
    n = n or (3 if w < 2.0 else 5)
    r = 1.6 * w
    c = w - r
    top = math.acos((r - w) / r)
    return [(c + r * math.cos(top * i / n), spring + r * math.sin(top * i / n)) for i in range(n)] + \
           [(0.0, spring + r * math.sin(top))]


def merlon_parts(p0, p1, thick, z, h=0.6, every=1.0, size=0.5, coping=0.3):
    """The battlement along a wall top from p0 to p1 (x, y; the wall's centre line), standing on z:
    a coping, then one merlon a tile, `size` long and `h` tall, as wide as the wall (Varrock's
    rhythm: docs/research/osrs-varrock-castle.md). Returns point lists, one per convex piece."""
    p0, p1 = Vector((*p0[:2], 0)), Vector((*p1[:2], 0))
    d = p1 - p0
    length = d.length
    d.normalize()
    n = Vector((-d.y, d.x, 0))
    t = thick / 2
    parts = [[p + n * k + Vector((0, 0, zz)) for p in (p0, p1) for k in (-t, t) for zz in (z, z + coping)]]
    count = max(1, round(length / every))
    for i in range(count):
        q = p0 + d * (length * (i + 0.5) / count)
        parts.append([q + d * a + n * k + Vector((0, 0, zz)) for a in (-size / 2, size / 2) for k in (-t, t)
                      for zz in (z + coping - 0.05, z + coping + h)])
    return parts


def V(x, y, z=0.0):
    return Vector((x, y, z))


# ---------------------------------------------------------------- shapes on faces

def frame(origin, along, out):
    """A place on a wall face: origin on the face, `along` across it, `out` away from it."""
    return V(*origin[:2], 0.0), V(*along[:2]).normalized(), V(*out[:2]).normalized()


def at(f, s, z, d=0.0):
    o, a, n = f
    return o + a * s + V(0, 0, z) + n * d


def arch_panel(b, f, w, base, spring, m, depth=0.04, d0=0.0):
    pts = arch(w, spring)
    outline = [(-w, base), (w, base)] + pts + [(-s, z) for s, z in pts]
    b.hull([at(f, s, z, d) for s, z in outline for d in (d0, d0 + depth)], m)


def arch_frame(b, f, w_in, w_out, spring, d0, d1, m=STONE, base=0.0):
    n = 3 if w_out < 2.0 else 5
    pi, po = arch(w_in, spring, n), arch(w_out, spring, n)
    for sg in (-1, 1):
        g = lambda s, z, d: at(f, sg * s, z, d)  # noqa: E731
        b.hull([g(s, z, d) for s in (w_in, w_out) for z in (base, spring) for d in (d0, d1)], m)
        for k in range(len(pi) - 1):
            b.hull([g(s, z, d) for (s, z) in (pi[k], pi[k + 1], po[k], po[k + 1]) for d in (d0, d1)], m)


def arch_spandrel(b, f, w, spring, top, half, d0, d1, m=STONE, base=0.0):
    """The solid round a pointed opening through a wall, out to `half` either side, up to `top`."""
    pts = arch(w, spring)
    for sg in (-1, 1):
        g = lambda s, z, d: at(f, sg * s, z, d)  # noqa: E731
        b.hull([g(s, z, d) for s in (w, half) for z in (base, top) for d in (d0, d1)], m)
        for k in range(len(pts) - 1):
            (s0, z0), (s1, z1) = pts[k], pts[k + 1]
            b.hull([g(s, z, d) for (s, z) in ((s0, z0), (s1, z1), (s0, top), (s1, top)) for d in (d0, d1)], m)


def head(w):
    """How far a pointed head of half-width w rises above its springing."""
    return arch(w, 0.0)[-1][1]


def lancet(b, f, w, base, top, glass=GLASS):
    """A pointed window from `base` up to its apex at `top`, in a thin raised frame."""
    spring = top - head(w)
    arch_frame(b, f, w, w + 0.2, spring, 0.0, 0.12, STONE, base=base)
    arch_panel(b, f, w, base, spring, glass)


def doorway(b, f, w, top, door=DOOR):
    """A pointed door up to its apex at `top`, in a deeper frame of two orders."""
    spring = top - head(w)
    arch_frame(b, f, w, w + 0.3, spring, 0.0, 0.3, STONE)
    arch_frame(b, f, w + 0.3, w + 0.55, spring + 0.1, 0.0, 0.15, STONE)
    arch_panel(b, f, w, 0.0, spring, door)


def pinnacle(b, cx, cy, z, half=0.5):
    """The one pinnacle: a square pyramid five half-widths tall."""
    b.hull([V(cx + dx, cy + dy, z) for dx in (-half, half) for dy in (-half, half)] + [V(cx, cy, z + 5 * half)], STONE)


def battlement(b, p0, p1, thick, z):
    for pts in merlon_parts(p0, p1, thick, z):
        b.hull(pts, STONE)


# ---------------------------------------------------------------- the parts

def wall(b, p0, p1, h, t=WT, merlons=True, plinth=True):
    """A wall skin along the centre line p0 -> p1 (x, y), h tall. Returns (along, normal)."""
    a, c = V(*p0), V(*p1)
    d = (c - a).normalized()
    n = V(-d.y, d.x)
    b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2, t / 2) for z in (0, h)], STONE)
    if plinth:
        b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2 - 0.15, t / 2 + 0.15) for z in (0, 0.6)], STONE)
    if merlons:
        battlement(b, p0, p1, t, h)
    return d, n


def city_wall(b, p0, p1, h=7.0, t=1.2, out=1, slit_every=3.0):
    """A curtain wall you walk along the top of: merlons on both edges, a battered footing and
    slits on the outer face (`out`: which side, +1 or -1 of the line's left normal)."""
    a, c = V(*p0), V(*p1)
    d = (c - a).normalized()
    n = V(-d.y, d.x)
    b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2, t / 2) for z in (0, h)], STONE)
    b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2 - 0.4, t / 2 + 0.4) for z in (0, 0.2)] +
           [p + n * k + V(0, 0, 1.4) for p in (a, c) for k in (-t / 2, t / 2)], STONE)
    for side in (-1, 1):
        e = n * (side * (t / 2 - 0.25))
        battlement(b, tuple(a + e)[:2], tuple(c + e)[:2], 0.5, h)
    length = (c - a).length
    count = max(1, int(length // slit_every))
    for i in range(count):
        s = length * (i + 0.5) / count
        f = frame(tuple(a + d * s + n * (out * t / 2)), tuple(d), tuple(n * out))
        arch_panel(b, f, 0.15, 2.4, 3.6, DARK)


def buttress(b, x, y, out, h, along=(1, 0), width=1.0, depth=1.2):
    """A buttress on a wall face at (x, y), standing out `depth` toward `out` (a unit (x, y)), up
    to just under the wall top, crowned with the one pinnacle (which stands above the merlons)."""
    o, a, n = V(x, y), V(*along).normalized(), V(*out).normalized()
    zb = h - 0.4
    b.hull([o + a * s + n * k + V(0, 0, z) for s in (-width / 2, width / 2) for k in (-0.05, depth) for z in (0, zb)], STONE)
    b.hull([o + a * s + n * k + V(0, 0, z) for s in (-width / 2 - 0.12, width / 2 + 0.12) for k in (-0.05, depth + 0.12)
            for z in (0, 0.8)], STONE)
    q = o + n * (depth / 2)
    pinnacle(b, q.x, q.y, zb, half=width / 2)


def tower(b, cx, cy, s, h, chamfer=None, top="merlons", spire_h=None, slits=True):
    """A tower s x s, its corners chamfered, a corbelled band under its battlement, pinnacles on
    the chamfers. top: "merlons" (flat, walkable), "spire" (a slate spire inside the merlons) or
    "belfry" (an octagonal belfry with dark openings, then a spire)."""
    hs = s / 2
    c = chamfer if chamfer is not None else 0.25 * s

    def plan(r, cc):
        return [(cx + x, cy + y) for x, y in ((r - cc, -r), (r, -r + cc), (r, r - cc), (r - cc, r),
                                              (-r + cc, r), (-r, r - cc), (-r, -r + cc), (-r + cc, -r))]

    body = plan(hs, c)
    b.hull([V(x, y, z) for x, y in body for z in (0, h)], STONE)
    b.hull([V(x, y, z) for x, y in plan(hs + 0.2, c + 0.08) for z in (0, 0.9)], STONE)          # plinth
    corbel = plan(hs + 0.3, c + 0.12)
    b.hull([V(x, y, h - 0.9) for x, y in body] + [V(x, y, z) for x, y in corbel for z in (h - 0.5, h)], STONE)
    for p, q in zip(corbel, corbel[1:] + corbel[:1]):
        mid = V((p[0] + q[0]) / 2 - cx, (p[1] + q[1]) / 2 - cy).normalized() * 0.25
        battlement(b, (p[0] - mid.x, p[1] - mid.y), (q[0] - mid.x, q[1] - mid.y), 0.5, h)
    for k in range(4):                                            # a pinnacle on each chamfer
        p, q = corbel[2 * k + 1], corbel[(2 * k + 2) % 8]
        mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
        inward = V(cx - mx, cy - my).normalized() * 0.55
        b.box(mx + inward.x - 0.45, my + inward.y - 0.45, h, mx + inward.x + 0.45, my + inward.y + 0.45, h + 0.9, STONE)
        pinnacle(b, mx + inward.x, my + inward.y, h + 0.9, half=0.45)
    if slits:
        for face in (((cx + hs, cy), (0, 1), (1, 0)), ((cx - hs, cy), (0, -1), (-1, 0)),
                     ((cx, cy + hs), (-1, 0), (0, 1)), ((cx, cy - hs), (1, 0), (0, -1))):
            f = frame(*face)
            z = 2.6
            while z + 1.6 < h - 1.2:
                arch_panel(b, f, 0.18, z, z + 1.2, DARK)
                z += 3.6
    sh = spire_h or 1.6 * s
    if top == "spire":
        b.cone(cx, cy, hs - 0.6, h, h + sh, ROOF, sides=8)
    elif top == "belfry":
        r, bh = 0.7 * hs, 0.55 * s
        ring = lambda rr, z: [V(cx + rr * math.cos(2 * math.pi * (k + 0.5) / 8), cy + rr * math.sin(2 * math.pi * (k + 0.5) / 8), z)  # noqa: E731
                              for k in range(8)]
        b.hull(ring(r, h) + ring(r, h + bh), STONE)
        rf = r * math.cos(math.pi / 8)
        for k in range(8):
            phi = 2 * math.pi * k / 8
            f = frame((cx + rf * math.cos(phi), cy + rf * math.sin(phi)), (-math.sin(phi), math.cos(phi)), (math.cos(phi), math.sin(phi)))
            arch_panel(b, f, 0.3, h + 0.5, h + bh - 0.6, DARK)
        b.hull(ring(r + 0.25, h + bh) + ring(r + 0.25, h + bh + 0.35), STONE)
        b.hull(ring(r + 0.1, h + bh + 0.35) + [V(cx, cy, h + bh + sh)], ROOF)


def hall(b, x0, y0, L, W, h, bay=4.0, pitch=SLATE_PITCH, win_w=0.5, glass=DARK, door_w=1.0, gable_window=None,
         corner_towers=False):
    """A rectangular stone building along x: thin walls with merlons on the long sides, a buttress
    with its pinnacle at every bay, a lancet in every bay, gable ends that are the roof's own ends
    (a pinnacle at each peak), a door in the west gable and a slate roof behind the merlons.
    glass: GLASS (red) for buildings of the faith, DARK for the rest. gable_window: the half-width
    of a tall window over the door, or None."""
    x1, y1 = x0 + L, y0 + W
    ym = (y0 + y1) / 2
    rise = (W / 2) * math.tan(math.radians(pitch))
    bays = max(1, round(L / bay))
    step = L / bays
    for yc, s in ((y0 + WT / 2, -1), (y1 - WT / 2, 1)):
        wall(b, (x0, yc), (x1, yc), h)
        face = yc + s * WT / 2
        for i in range(bays + 1):
            buttress(b, x0 + step * i, face, (0, s), h)
        for i in range(bays):
            lancet(b, frame((x0 + step * (i + 0.5), face), (1, 0), (0, s)), win_w, 1.6, h - 1.2, glass)
    for xc, s in ((x0 + WT / 2, -1), (x1 - WT / 2, 1)):
        b.hull([V(x, y, z) for x in (xc - WT / 2, xc + WT / 2)
                for y, z in ((y0, 0), (y1, 0), (y1, h), (y0, h), (ym, h + rise))], STONE)
        b.hull([V(x, y, z) for x in (xc - WT / 2 - 0.15, xc + WT / 2 + 0.15) for y in (y0, y1) for z in (0, 0.6)], STONE)
        pinnacle(b, xc, ym, h + rise - 0.7, half=0.4)
        for y in (y0, y1):                                        # the gable's feet
            pinnacle(b, xc, y + (0.3 if y == y0 else -0.3), h - 0.2, half=0.3)
    west = frame((x0, ym), (0, -1), (-1, 0))
    door_top = min(h - 1.0, 2.9 + head(door_w))
    doorway(b, west, door_w, door_top)
    if gable_window:
        lancet(b, west, gable_window, door_top + 1.2, h + rise * 0.55, glass)
        lancet(b, frame((x1, ym), (0, 1), (1, 0)), gable_window, 2.0, h + rise * 0.55, glass)
    b.gable_roof(x0 + WT, x1 - WT, y0 + WT, y1 - WT, h, (W / 2 - WT) * math.tan(math.radians(pitch)), ROOF, over=0.0)
    if corner_towers:
        for x, y in ((x0, y0), (x0, y1), (x1, y0), (x1, y1)):
            tower(b, x, y, 2.4, h + 2.5, top="spire", spire_h=4.5, slits=False)


def gatehouse(b, cx, y, w=4.0, gate_h=5.0, s=4.6, tower_h=12.0, depth=5.0, block_h=8.5):
    """A gate through a wall line along x at y: a passage w wide, a pointed arch with a raised
    portcullis between two spired towers, a battlemented block over the arch."""
    for sx in (-1, 1):
        tower(b, cx + sx * (w / 2 + s / 2 - 0.3), y, s, tower_h, top="spire", spire_h=7.5)
    half = w / 2 + 0.6
    front = frame((cx, y - depth / 2), (1, 0), (0, -1))
    back = frame((cx, y + depth / 2), (-1, 0), (0, 1))
    spring = gate_h - head(w / 2)
    arch_spandrel(b, front, w / 2, spring, block_h, half, -depth, 0.0)
    b.box(cx - half, y - depth / 2, 0, cx - w / 2, y + depth / 2, spring, STONE)
    b.box(cx + w / 2, y - depth / 2, 0, cx + half, y + depth / 2, spring, STONE)
    for f in (front, back):
        arch_frame(b, f, w / 2, w / 2 + 0.45, spring, 0.0, 0.35, STONE)
    for yy in (y - depth / 2 + 0.25, y + depth / 2 - 0.25):
        battlement(b, (cx - half, yy), (cx + half, yy), 0.5, block_h)
    yp = y - depth / 2 + 1.0                                      # the portcullis, raised into the arch
    for k in range(-4, 5):
        x = cx + k * (w / 2 - 0.2) / 4
        b.box(x - 0.06, yp - 0.06, spring - 0.6, x + 0.06, yp + 0.06, gate_h, METAL)
        b.cone(x, yp, 0.08, spring - 1.0, spring - 0.6, METAL, sides=4)
    for z in (spring - 0.2, spring + 0.6, spring + 1.4):
        b.box(cx - w / 2, yp - 0.07, z, cx + w / 2, yp + 0.07, z + 0.12, METAL)


# ---------------------------------------------------------------- housing

def gable_boards(b, x0, x1, y, z, rise, m, over=0.3):
    """A board down each edge of a gable at y, eave to ridge, just proud of the roof."""
    xm = (x0 + x1) / 2
    for xe in (x0 - over, x1 + over):
        b.hull([V(x, y + dy, zz + dz) for x, zz in ((xe, z), (xm, z + rise)) for dy in (-0.06, 0.06) for dz in (-0.16, 0.06)], m)


def townhouse(b, w, d, storeys=3, plaster=True, gable_front=True, jetty=0.35, chimney=True, windows=None):
    """A narrow town house, its street front at y = 0 facing -y, x from 0 to w, d deep.
    Plaster: a stone ground floor, then jettied plaster storeys framed in dark timber (posts at
    the corners and beside the windows, a beam at every floor), boards down the gable edges.
    Stone: stone throughout, small dark lancets, a stone coping on the gables. Either way a steep
    slate roof (gable to the street, or eaves to the street), a pointed stone doorway and a chimney.
    Red is for the faith, so houses keep out of it."""
    S = STOREY
    top = S * storeys
    nw = windows or max(1, int(w // 2.2))
    xs = [w * (i + 0.5) / nw for i in range(nw)]
    if plaster and storeys == 1:                                  # a cottage: a stone footing, framed plaster above
        b.box(0, 0, 0, w, d, 0.9, STONE)
        b.box(0, 0, 0.9, w, d, S, PLASTER)
        for x in (0.0, w):
            for y in (0.0, d):
                b.box(x - 0.09, y - 0.09, 0.9, x + 0.09, y + 0.09, S, TIMBER)
        b.box(-0.06, -0.06, S - 0.14, w + 0.06, d + 0.06, S + 0.04, TIMBER)
    else:
        b.box(0, 0, 0, w, d, S, STONE)                              # the stone ground floor
    b.box(-0.1, -0.1, 0, w + 0.1, d + 0.1, 0.4, STONE)
    front = 0.0
    for k in range(1, storeys):
        z0, z1 = S * k, S * (k + 1)
        if plaster:
            front = -jetty * k
            b.box(0, front, z0, w, d, z1, PLASTER)
            b.box(-0.06, front - 0.08, z0 - 0.2, w + 0.06, d + 0.06, z0 + 0.04, TIMBER)    # the floor beam
            for x in [0.0, w] + [x + e for x in xs for e in (-0.55, 0.55)]:
                b.box(x - 0.09, front - 0.06, z0, x + 0.09, front + 0.04, z1, TIMBER)
            for x in xs:
                b.box(x - 0.42, front - 0.04, z0 + 0.75, x + 0.42, front + 0.02, z0 + 1.75, DARK)
                b.box(x - 0.48, front - 0.08, z0 + 0.65, x + 0.48, front + 0.02, z0 + 0.78, TIMBER)   # the sill
        else:
            b.box(0, 0, z0, w, d, z1, STONE)
            for x in xs:
                lancet(b, frame((x, 0.0), (1, 0), (0, -1)), 0.32, z0 + 0.7, z0 + 1.9, DARK)
    if plaster and storeys > 1:
        b.box(-0.06, front - 0.08, top - 0.14, w + 0.06, d + 0.06, top + 0.04, TIMBER)
    wall_m = PLASTER if plaster else STONE
    edge_m = TIMBER if plaster else STONE
    if gable_front:
        rise = (w / 2 + 0.3) * math.tan(math.radians(SLATE_PITCH))
        for y in (front, d):
            b.hull([V(x, yy, z) for yy in (y, y + (0.15 if y == front else -0.15))
                    for x, z in ((0, top), (w, top), (w / 2, top + rise * w / (w + 0.6)))], wall_m)
            gable_boards(b, 0, w, y - 0.32 if y == front else y + 0.32, top, rise, edge_m)
        b.gable_roof(0, w, front, d, top, rise, ROOF, along="y", over=0.3)
        gz = top + 0.3
        if plaster:
            b.box(w / 2 - 0.35, front - 0.04, gz, w / 2 + 0.35, front + 0.02, gz + 0.9, DARK)
        else:
            lancet(b, frame((w / 2, front), (1, 0), (0, -1)), 0.28, gz, gz + 1.2, DARK)
        ridge_top = top + rise
    else:
        depth = d - front
        rise = (depth / 2 + 0.3) * math.tan(math.radians(SLATE_PITCH))
        for x in (0.0, w):
            b.hull([V(xx, y, z) for xx in (x, x + (0.15 if x == 0 else -0.15))
                    for y, z in ((front, top), (d, top), (front + depth / 2, top + rise * depth / (depth + 0.6)))], wall_m)
        b.gable_roof(0, w, front, d, top, rise, ROOF, along="x", over=0.3)
        ridge_top = top + rise
    door_x = xs[0] if nw > 1 else w * 0.3
    doorway(b, frame((min(door_x, w - 0.9), 0.0), (1, 0), (0, -1)), 0.5, 2.2)
    if w > 3.2:
        lancet(b, frame((max(w - 1.0, door_x + 1.4), 0.0), (1, 0), (0, -1)), 0.28, 0.9, 1.9, DARK)
    if chimney:
        b.box(w - 0.9, d - 1.4, 0, w - 0.1, d - 0.6, ridge_top + 0.4, STONE)
        b.box(w - 1.0, d - 1.5, ridge_top + 0.2, w, d - 0.5, ridge_top + 0.5, STONE)


# ---------------------------------------------------------------- walk-in buildings

CUT = STOREY                  # a wall cut toward the camera stops at one storey
SIDES = ("N", "S", "E", "W")


def clip_z(pts, za, zb):
    """The corners of the convex solid round `pts`, cut to za <= z <= zb: the points inside, and
    where every segment between two points crosses either plane (enough: hull edges are among
    those segments, and every such point lies in the solid)."""
    pts = [Vector(p) for p in pts]
    out = [p for p in pts if za - 1e-6 <= p.z <= zb + 1e-6]
    for p, q in itertools.combinations(pts, 2):
        for zc in (za, zb):
            if (p.z - zc) * (q.z - zc) < 0:
                out.append(p.lerp(q, (zc - p.z) / (q.z - p.z)))
    return out


class Layered:
    """A building you walk into, split into layers that show or hide (shows()):
      base          always: the floor and every wall up to CUT (one storey)
      top[side]     that side's walls from CUT up to `inside_h` (the inside view's wall height)
      crown[side]   merlons on that side's cut, shown instead of its top when the camera is there
      upper, roofs  everything higher, and the roofs: hidden inside
    Inside, the far walls stand to `inside_h` as the backdrop and the walls toward the camera are
    cut under their merlons, so nothing stands between the camera and the player.
    on(side) gives a Build-like view whose shapes are split into those layers by height, so every
    kit part can be used on a walk-in building."""

    def __init__(self, inside_h):
        self.base, self.upper, self.roofs = Build(), Build(), Build()
        self.top = {s: Build() for s in SIDES}
        self.crown = {s: Build() for s in SIDES}
        self.inside_h = inside_h

    def hull(self, side, pts, m):
        for b, za, zb in ((self.base, -1e9, CUT), (self.top[side], CUT, self.inside_h), (self.upper, self.inside_h, 1e9)):
            part = clip_z(pts, za, zb)
            if len(part) >= 4 and max(p.z for p in part) - min(p.z for p in part) > 1e-3:
                b.hull(part, m)

    def on(self, side):
        return _Side(self, side)

    def crown_run(self, side, p0, p1, thick):
        """Merlons on a wall's cut, for when that side is cut."""
        for pts in merlon_parts(p0, p1, thick, CUT):
            self.crown[side].hull(pts, STONE)

    def finish(self, prefix, coll="Cruor"):
        objs = {}
        parts = [("Base", self.base), ("Upper", self.upper), ("Roofs", self.roofs)]
        parts += [(f"Top_{s}", self.top[s]) for s in SIDES] + [(f"Crown_{s}", self.crown[s]) for s in SIDES]
        for name, b in parts:
            A.finish(b.bm)
            objs[name] = A.mesh_from(f"{prefix}_{name}", b.bm, MATS, coll=coll, smooth_angle=SMOOTH)
        return objs


class _Side:
    """Layered.on(side): the Build methods the kit parts use, split into that side's layers."""

    def __init__(self, layered, side):
        self.l, self.side = layered, side

    def hull(self, pts, m):
        self.l.hull(self.side, pts, m)

    def box(self, x0, y0, z0, x1, y1, z1, m):
        self.hull([Vector((x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)], m)

    def cone(self, cx, cy, r, z0, z1, m, sides=8):
        self.hull([Vector((cx + r * math.cos(2 * math.pi * k / sides), cy + r * math.sin(2 * math.pi * k / sides), z0))
                   for k in range(sides)] + [Vector((cx, cy, z1))], m)


def shows(part, state="outside", cut=()):
    """Whether a Layered building's layer shows: "outside" shows everything but the crowns;
    "inside" hides the roofs and upper parts, and on each side in `cut` (the camera's side)
    swaps its wall top for its crown."""
    inside = state == "inside"
    if part in ("Upper", "Roofs"):
        return not inside
    if part.startswith("Top_"):
        return not (inside and part[4:] in cut)
    if part.startswith("Crown_"):
        return inside and part[6:] in cut
    return True


# ---------------------------------------------------------------- objects and paint

def piece(name, fn, *args, coll="Cruor", **kw):
    """One object from one part: piece("Barracks", hall, x0, y0, 16, 10, 7.0)."""
    b = Build()
    fn(b, *args, **kw)
    A.finish(b.bm)
    return A.mesh_from(name, b.bm, MATS, coll=coll, smooth_angle=SMOOTH)


def paint(objs, table=None):
    """Swap the flat colours in PAINT for Cruor's painted textures (shown as they are, point
    filtered), scaling each face's texture coordinates (in tiles, from rth_art.box_uvs) to the
    texture's repeat. Run cruor/textures.py first after changing the textures."""
    import bpy
    mats = {}
    for name, (image, ru, rv) in (table or PAINT).items():
        m = bpy.data.materials.get(image) or bpy.data.materials.new(image)
        m.use_nodes = True
        nodes, links = m.node_tree.nodes, m.node_tree.links
        bsdf = nodes.get("Principled BSDF")
        bsdf.inputs["Roughness"].default_value = 1.0
        bsdf.inputs["Specular IOR Level"].default_value = 0.0
        tex = nodes.get("Painted") or nodes.new("ShaderNodeTexImage")
        tex.name = "Painted"
        tex.image = bpy.data.images.load(os.path.join(HERE, "textures", image + ".png"), check_existing=True)
        tex.image.reload()
        tex.interpolation = "Closest"
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if name in GLOW:
            links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = GLOW[name]
        mats[name] = (m, ru, rv)
    for o in objs:
        me = o.data
        uv = me.uv_layers[0]
        for i, slot in enumerate(list(me.materials)):
            if slot is None or slot.name not in mats:
                continue
            m, ru, rv = mats[slot.name]
            me.materials[i] = m
            for p in me.polygons:
                if p.material_index == i:
                    for li in p.loop_indices:
                        u, v = uv.data[li].uv
                        uv.data[li].uv = (u / ru, v / rv)
