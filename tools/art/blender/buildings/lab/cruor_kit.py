"""Cruor's building kit: the parts the passed great church (B, monumental.plain_church) is made of,
reusable at any length and height, and a first set of the city's ordinary buildings made from
them, with church B standing behind for scale. The rules are in docs/regions/cruor-architecture.md:
one pale stone on thin walls, merlons along every wall top, one pinnacle shape, small lancets,
chamfered and corbelled towers, slate roofs whose gables are their own ends, red only for the faith.

Parts (each adds to a monumental.Build; 1 unit = 1 tile, Z up):
  wall           a skin with a plinth and merlons, along any line
  city_wall      a thick, walkable curtain with merlons on both edges and slits
  buttress       the pier with the one pinnacle
  lancet         a pointed window in a thin frame (red glass for the faith, dark otherwise)
  doorway        a pointed door in a deeper frame
  tower          chamfered and corbelled, battlemented, slits; a spire or a belfry and spire
  hall           a rectangular building: walls, buttresses, lancets, gable ends, slate roof
  gatehouse      two towers, a gate arch with a raised portcullis, a battlemented top
  townhouse      a narrow house: stone ground floor, jettied plaster and timber (or all stone), slate roof
  street         two terraced rows of town houses facing each other

Run in Blender (MCP): runpy.run_path(".../lab/cruor_kit.py", run_name="__main__").
Run lab/church_textures.py first after changing the textures."""
import math
import os
import sys

from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import rth_art as A  # noqa: E402
import monumental as M  # noqa: E402
from monumental import Build, arch, merlon_parts, MATS, PALE, ROOF, DARK, METAL, GLASS, DOOR  # noqa: E402

WT = 0.25                     # a wall is a skin
SLATE_PITCH = 55.0            # roofs: steep, as the church's
PAINT = M.PAINT_B             # the church's textures: Cruor's stone, slate, red glass


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


def arch_frame(b, f, w_in, w_out, spring, d0, d1, m=PALE, base=0.0):
    n = 3 if w_out < 2.0 else 5
    pi, po = arch(w_in, spring, n), arch(w_out, spring, n)
    for sg in (-1, 1):
        g = lambda s, z, d: at(f, sg * s, z, d)  # noqa: E731
        b.hull([g(s, z, d) for s in (w_in, w_out) for z in (base, spring) for d in (d0, d1)], m)
        for k in range(len(pi) - 1):
            b.hull([g(s, z, d) for (s, z) in (pi[k], pi[k + 1], po[k], po[k + 1]) for d in (d0, d1)], m)


def arch_spandrel(b, f, w, spring, top, half, d0, d1, m=PALE, base=0.0):
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
    arch_frame(b, f, w, w + 0.2, spring, 0.0, 0.12, PALE, base=base)
    arch_panel(b, f, w, base, spring, glass)


def doorway(b, f, w, top, door=DOOR):
    """A pointed door up to its apex at `top`, in a deeper frame of two orders."""
    spring = top - head(w)
    arch_frame(b, f, w, w + 0.3, spring, 0.0, 0.3, PALE)
    arch_frame(b, f, w + 0.3, w + 0.55, spring + 0.1, 0.0, 0.15, PALE)
    arch_panel(b, f, w, 0.0, spring, door)


def pinnacle(b, cx, cy, z, half=0.5):
    """The one pinnacle: a square pyramid five half-widths tall."""
    b.hull([V(cx + dx, cy + dy, z) for dx in (-half, half) for dy in (-half, half)] + [V(cx, cy, z + 5 * half)], PALE)


def battlement(b, p0, p1, thick, z):
    for pts in merlon_parts(p0, p1, thick, z):
        b.hull(pts, PALE)


# ---------------------------------------------------------------- the parts

def wall(b, p0, p1, h, t=WT, merlons=True, plinth=True):
    """A wall skin along the centre line p0 -> p1 (x, y), h tall. Returns (along, normal)."""
    a, c = V(*p0), V(*p1)
    d = (c - a).normalized()
    n = V(-d.y, d.x)
    b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2, t / 2) for z in (0, h)], PALE)
    if plinth:
        b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2 - 0.15, t / 2 + 0.15) for z in (0, 0.6)], PALE)
    if merlons:
        battlement(b, p0, p1, t, h)
    return d, n


def city_wall(b, p0, p1, h=7.0, t=1.2, out=1, slit_every=3.0):
    """A curtain wall you walk along the top of: merlons on both edges, a battered footing and
    slits on the outer face (`out`: which side, +1 or -1 of the line's left normal)."""
    a, c = V(*p0), V(*p1)
    d = (c - a).normalized()
    n = V(-d.y, d.x)
    b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2, t / 2) for z in (0, h)], PALE)
    b.hull([p + n * k + V(0, 0, z) for p in (a, c) for k in (-t / 2 - 0.4, t / 2 + 0.4) for z in (0, 0.2)] +
           [p + n * k + V(0, 0, 1.4) for p in (a, c) for k in (-t / 2, t / 2)], PALE)
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
    b.hull([o + a * s + n * k + V(0, 0, z) for s in (-width / 2, width / 2) for k in (-0.05, depth) for z in (0, zb)], PALE)
    b.hull([o + a * s + n * k + V(0, 0, z) for s in (-width / 2 - 0.12, width / 2 + 0.12) for k in (-0.05, depth + 0.12)
            for z in (0, 0.8)], PALE)
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
    b.hull([V(x, y, z) for x, y in body for z in (0, h)], PALE)
    b.hull([V(x, y, z) for x, y in plan(hs + 0.2, c + 0.08) for z in (0, 0.9)], PALE)          # plinth
    corbel = plan(hs + 0.3, c + 0.12)
    b.hull([V(x, y, h - 0.9) for x, y in body] + [V(x, y, z) for x, y in corbel for z in (h - 0.5, h)], PALE)
    for p, q in zip(corbel, corbel[1:] + corbel[:1]):
        mid = V((p[0] + q[0]) / 2 - cx, (p[1] + q[1]) / 2 - cy).normalized() * 0.25
        battlement(b, (p[0] - mid.x, p[1] - mid.y), (q[0] - mid.x, q[1] - mid.y), 0.5, h)
    for k in range(4):                                            # a pinnacle on each chamfer
        p, q = corbel[2 * k + 1], corbel[(2 * k + 2) % 8]
        mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
        inward = V(cx - mx, cy - my).normalized() * 0.55
        b.box(mx + inward.x - 0.45, my + inward.y - 0.45, h, mx + inward.x + 0.45, my + inward.y + 0.45, h + 0.9, PALE)
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
        b.hull(ring(r, h) + ring(r, h + bh), PALE)
        rf = r * math.cos(math.pi / 8)
        for k in range(8):
            phi = 2 * math.pi * k / 8
            f = frame((cx + rf * math.cos(phi), cy + rf * math.sin(phi)), (-math.sin(phi), math.cos(phi)), (math.cos(phi), math.sin(phi)))
            arch_panel(b, f, 0.3, h + 0.5, h + bh - 0.6, DARK)
        b.hull(ring(r + 0.25, h + bh) + ring(r + 0.25, h + bh + 0.35), PALE)
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
                for y, z in ((y0, 0), (y1, 0), (y1, h), (y0, h), (ym, h + rise))], PALE)
        b.hull([V(x, y, z) for x in (xc - WT / 2 - 0.15, xc + WT / 2 + 0.15) for y in (y0, y1) for z in (0, 0.6)], PALE)
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
    b.box(cx - half, y - depth / 2, 0, cx - w / 2, y + depth / 2, spring, PALE)
    b.box(cx + w / 2, y - depth / 2, 0, cx + half, y + depth / 2, spring, PALE)
    for f in (front, back):
        arch_frame(b, f, w / 2, w / 2 + 0.45, spring, 0.0, 0.35, PALE)
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

# Houses add the paint lab's plaster and a dark timber to the church's materials.
PLASTER, TIMBER = len(MATS), len(MATS) + 1
KIT_MATS = MATS + ["Plaster", "Umber"]
PAINT = dict(PAINT, Plaster=("TileWall", 1, 2.3))
STOREY = 2.3                  # docs/world-scale.md, D2


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
    b.box(0, 0, 0, w, d, S, PALE)                                  # the stone ground floor
    b.box(-0.1, -0.1, 0, w + 0.1, d + 0.1, 0.4, PALE)
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
            b.box(0, 0, z0, w, d, z1, PALE)
            for x in xs:
                lancet(b, frame((x, 0.0), (1, 0), (0, -1)), 0.32, z0 + 0.7, z0 + 1.9, DARK)
    if plaster and storeys > 1:
        b.box(-0.06, front - 0.08, top - 0.14, w + 0.06, d + 0.06, top + 0.04, TIMBER)
    wall_m = PLASTER if plaster else PALE
    edge_m = TIMBER if plaster else PALE
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
        b.box(w - 0.9, d - 1.4, 0, w - 0.1, d - 0.6, ridge_top + 0.4, PALE)
        b.box(w - 1.0, d - 1.5, ridge_top + 0.2, w, d - 0.5, ridge_top + 0.5, PALE)


# ---------------------------------------------------------------- the showcase

def piece(name, fn, *args, **kw):
    b = Build()
    fn(b, *args, **kw)
    A.finish(b.bm)
    return A.mesh_from(name, b.bm, KIT_MATS, coll="CruorKit", smooth_angle=50)


def street(x0, y_north, y_south, houses):
    """Two terraced rows facing each other across a street running along x: houses =
    [(w, d, kwargs), ...], alternating north and south. y_north and y_south are the two rows'
    front lines. A house faces -y as made, so the north row stands as made and the south row
    is turned round."""
    objs = []
    xn = xs = x0
    for i, (w, d, kw) in enumerate(houses):
        o = piece(f"House{i}", townhouse, w, d, **kw)
        if i % 2 == 0:
            o.location = (xn, y_north, 0)
            xn += w
        else:
            o.rotation_euler.z = math.pi
            o.location = (xs + w, y_south, 0)
            xs += w
        objs.append(o)
    b = Build()                                                   # the street's paving
    b.box(x0 - 1, y_south, 0, max(xn, xs) + 1, y_north, 0.03, M.FLOOR)
    A.finish(b.bm)
    objs.append(A.mesh_from("Street", b.bm, KIT_MATS, coll="CruorKit"))
    return objs


def build():
    A.reset()
    objs = []
    # The passed church, outside, behind the kit for scale.
    for part, o in M.plain_church(0, 0, M.PLAIN_B, prefix="Cathedral").items():
        if M.shows(part, "outside"):
            objs.append(o)
        else:
            import bpy
            bpy.data.objects.remove(o)
    # A run of city wall with wall towers, a corner tower at each end, the gatehouse in the middle.
    yw = -45.0
    objs.append(piece("CityWall_W", city_wall, (-48, yw), (-3.5, yw), out=-1))
    objs.append(piece("CityWall_E", city_wall, (3.5, yw), (48, yw), out=-1))
    objs.append(piece("Gatehouse", gatehouse, 0.0, yw))
    for i, x in enumerate((-26, 26)):
        objs.append(piece(f"WallTower{i}", tower, x, yw, 3.4, 9.5))
    for i, x in enumerate((-50, 50)):
        objs.append(piece(f"CornerTower{i}", tower, x, yw, 3.6, 11.0, top="spire", spire_h=7.0))
    # Civic buildings inside the wall, at ordinary sizes (docs/regions/cruor-assets.md).
    objs.append(piece("Barracks", hall, -44, -38, 16, 10, 7.0))
    objs.append(piece("ChapterHouse", hall, 18, -38, 12, 8, 8.0, bay=3.0, win_w=0.55, glass=GLASS, gable_window=1.1))
    objs.append(piece("Armoury", hall, -22, -38, 9, 8, 6.0, bay=3.0, corner_towers=True))
    objs.append(piece("PalaceTower", tower, 38, -31, 3.6, 13.0, top="belfry", spire_h=6.0))
    # A street of town houses south of the wall: plaster and stone, 2 and 3 storeys, terraced.
    P, Sn = dict(plaster=True), dict(plaster=False)
    houses = [(5, 8, dict(P, storeys=3)), (4.5, 8, dict(P, storeys=3)), (6, 8, dict(Sn, storeys=3)),
              (5, 8, dict(P, storeys=2, gable_front=False)), (4, 7, dict(P, storeys=3)), (6, 8, dict(Sn, storeys=2)),
              (5.5, 8, dict(Sn, storeys=3, gable_front=False)), (5, 8, dict(P, storeys=3, chimney=False)),
              (4.5, 7, dict(P, storeys=2)), (5, 8, dict(P, storeys=3)), (6, 8, dict(Sn, storeys=3)),
              (4.5, 8, dict(P, storeys=2, gable_front=False))]
    objs += street(-14, -55.0, -65.0, houses)
    M.paint(objs, PAINT)
    return {"triangles": A.report(objs)}


if __name__ == "__main__":
    result = build()
    cam = A.preview_rig(focus=(0, -30, 6), distance=120, pitch=28, yaw=-25, ground=100, human_at=(-2, -52))
    cam.data.clip_end = 3000
    M.scale_figures([(0, -45, 0), (-36, -40, 0), (24, -40, 0)])
