"""Venaria's village kit: the common buildings of Cruor's empire (Tolling's realm).

The realm's look, in pieces: pale walls, timber frames, stone where there is money or faith;
red only where the faith or the regime touches (church glass, a font's blood, a banner); leaded
glass that repeats, a little on a good house and a lot on a chapel; a narrow pointed window (the
lancet, the wound's shape) in gables and churches; the tithe font by a sworn door.

Every piece is one tile on the grid (Blender X along the wall, -Y outside, Z up; 1 unit = 1 tile)
and painted by palette name, so stone walls, slate roofs and red church glass are recolours of
the same pieces. Buildings are put together face by face from a short description (volume()),
the way a world builder would from data.

The storey is 2.3 (docs/world-scale.md)."""
import math
import os
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

EXPORT = True
STOREY = 2.3
PITCH = 42.0
RISE = math.tan(math.radians(PITCH))
T = 0.22                        # wall thickness, centred on the grid line
PLINTH = 0.35
RAIL = 0.12
POST = 0.13
OVERHANG = 0.35                 # eaves past the wall
VERGE = 0.4                     # roof past the gable ends
JETTY = 0.25                    # how far an upper storey juts out over a gable end
SLAB = 0.09
# The roof rests on the walls' outer edge (and the corner posts), not their centre line, so no
# rail or post pokes through the slope.
ROOF_BASE = STOREY + (T / 2 + 0.06) * RISE

MATS = ["Stone", "Plaster", "Timber", "Thatch", "Planks", "Iron", "Sable", "Glaze", "Blackiron",
        "Blood", "SynodRed", "Bronze", "Leaves", "DarkLeaves", "Ochre", "Wool", "Bark", "Ash", "Wheat",
        "CandleWax", "GraveTurf", "Oak"]
(STONE, PLASTER, TIMBER, THATCH, PLANKS, IRON, DARK, GLASS, LEAD, BLOOD, BANNER, BRONZE, LEAF, LEAF_DARK,
 FLOWER, FLOWER2, BARK, LOG_END, HAY, CANDLE, GRASS, OAK) = range(len(MATS))


def box(bm, x0, x1, y0, y1, z0, z1, mat):
    return A.box(bm, (x0, y0, z0), (x1, y1, z1), mat)


def prism_xz(bm, outline, y0, y1, mat):
    """A convex outline in the wall's plane (x, z), given depth from y0 to y1."""
    return A.hull(bm, [(x, y, z) for x, z in outline for y in (y0, y1)], mat)


# ---- leaded glass ----

def clip(p0, p1, poly):
    """The part of segment p0-p1 inside a convex polygon (Cyrus-Beck), or None."""
    t0, t1 = 0.0, 1.0
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]
    n = len(poly)
    cx = sum(p[0] for p in poly) / n
    cz = sum(p[1] for p in poly) / n
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        ex, ez = b[0] - a[0], b[1] - a[1]
        nx, nz = ez, -ex                                           # an edge normal
        if nx * (cx - a[0]) + nz * (cz - a[1]) > 0:
            nx, nz = -nx, -nz                                      # make it point outward
        denom = nx * dx + nz * dz
        num = nx * (p0[0] - a[0]) + nz * (p0[1] - a[1])
        if abs(denom) < 1e-9:
            if num > 0:
                return None
            continue
        t = -num / denom
        if denom < 0:
            t0 = max(t0, t)
        else:
            t1 = min(t1, t)
        if t0 > t1:
            return None
    return (p0[0] + dx * t0, p0[1] + dz * t0), (p0[0] + dx * t1, p0[1] + dz * t1)


def leaded_pane(bm, outline, spacing=0.075, lead=0.008):
    """Glass in a convex opening, with a diamond lattice of lead over it on both faces."""
    prism_xz(bm, outline, -0.015, 0.015, GLASS)
    xs = [p[0] for p in outline]
    zs = [p[1] for p in outline]
    x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
    span = (x1 - x0) + (z1 - z0)
    k = -span
    while k < span:
        for direction in (1, -1):
            p0 = (x0 + k, z0)
            p1 = (x0 + k + direction * span, z0 + span)
            seg = clip(p0, p1, outline)
            if seg:
                (ax, az), (bx, bz) = seg
                length = math.hypot(bx - ax, bz - az) or 1
                ox, oz = -(bz - az) / length * lead, (bx - ax) / length * lead
                for y in (-0.017, 0.017):
                    f = bm.faces.new([bm.verts.new((ax - ox, y, az - oz)), bm.verts.new((bx - ox, y, bz - oz)),
                                      bm.verts.new((bx + ox, y, bz + oz)), bm.verts.new((ax + ox, y, az + oz))])
                    f.normal_update()
                    if (f.normal.y < 0) != (y < 0):
                        f.normal_flip()
                    f.material_index = LEAD
        k += spacing


# ---- walls ----

def wall_frame(bm):
    box(bm, 0, 1, -T / 2 - 0.02, T / 2 + 0.02, 0, PLINTH, STONE)
    box(bm, 0, 1, -T / 2 - 0.015, T / 2 + 0.015, STOREY - RAIL, STOREY, TIMBER)
    box(bm, 0, 1, -T / 2 - 0.015, T / 2 + 0.015, PLINTH, PLINTH + RAIL * 0.7, TIMBER)


LO, HI = PLINTH + RAIL * 0.7, STOREY - RAIL


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
    panel(bm, 0, 1, LO, HI)
    return bm


def piece_wall_braced():
    bm = bmesh.new()
    wall_frame(bm)
    panel(bm, 0, 1, LO, HI)
    for y in (-T / 2 - 0.015, T / 2 + 0.015):
        A.hull(bm, [(x, y + dy, z) for x, z in ((0.1, LO), (0.2, LO), (0.9, HI), (0.8, HI)) for dy in (-0.01, 0.01)],
               TIMBER)
    return bm


def window_surround(bm, w0, w1, s0, s1, sill=True):
    for x in (w0 - 0.04, w1):
        box(bm, x, x + 0.04, -T / 2 - 0.02, T / 2 + 0.02, s0, s1, TIMBER)
    if sill:
        box(bm, w0 - 0.06, w1 + 0.06, -T / 2 - 0.06, T / 2 + 0.02, s0 - 0.06, s0, TIMBER)
    box(bm, w0 - 0.04, w1 + 0.04, -T / 2 - 0.02, T / 2 + 0.02, s1, s1 + 0.05, TIMBER)


def holed_wall(bm, w0, w1, s0, s1):
    panel(bm, 0, w0, LO, HI)
    panel(bm, w1, 1, LO, HI)
    panel(bm, w0, w1, LO, s0)
    panel(bm, w0, w1, s1, HI)


def piece_wall_shuttered():
    """A poor house's window: a dark opening and plank shutters standing open."""
    bm = bmesh.new()
    wall_frame(bm)
    w0, w1, s0, s1 = 0.32, 0.68, 1.05, 1.7
    holed_wall(bm, w0, w1, s0, s1)
    box(bm, w0, w1, -0.02, 0.02, s0, s1, DARK)
    window_surround(bm, w0, w1, s0, s1)
    for x0, x1 in ((w0 - 0.22, w0 - 0.05), (w1 + 0.05, w1 + 0.22)):
        box(bm, x0, x1, -T / 2 - 0.05, -T / 2 - 0.02, s0 + 0.02, s1 - 0.02, PLANKS)
    return bm


def piece_wall_leaded():
    """A better house's window: two lights of leaded glass either side of a mullion."""
    bm = bmesh.new()
    wall_frame(bm)
    w0, w1, s0, s1 = 0.24, 0.76, 0.95, 1.75
    holed_wall(bm, w0, w1, s0, s1)
    mid = (w0 + w1) / 2
    for a, b in ((w0, mid - 0.025), (mid + 0.025, w1)):
        leaded_pane(bm, [(a, s0), (b, s0), (b, s1), (a, s1)])
    box(bm, mid - 0.03, mid + 0.03, -T / 2 - 0.01, T / 2 + 0.01, s0, s1, TIMBER)
    window_surround(bm, w0, w1, s0, s1)
    return bm


def lancet_outline(w0, w1, s0, s1, apex):
    """A pointed arch, as a convex outline (two straight-chorded halves of the arch)."""
    mid = (w0 + w1) / 2
    h = apex - s1
    return [(w0, s0), (w1, s0), (w1, s1), (w1 - (w1 - w0) * 0.12, s1 + h * 0.62), (mid, apex),
            (w0 + (w1 - w0) * 0.12, s1 + h * 0.62), (w0, s1)]


def piece_wall_lancet():
    """The wound's shape: a narrow pointed window of leaded glass (red in a church, by recolour)."""
    bm = bmesh.new()
    wall_frame(bm)
    w0, w1, s0, s1, apex = 0.36, 0.64, 0.8, 1.5, 1.92
    panel(bm, 0, w0, LO, HI)
    panel(bm, w1, 1, LO, HI)
    panel(bm, w0, w1, LO, s0)
    panel(bm, w0, w1, apex, HI)
    out = lancet_outline(w0, w1, s0, s1, apex)
    for side in (1, -1):                                          # the spandrels round the arch
        corner = (w0 if side > 0 else w1, apex)
        springing, shoulder = out[6 if side > 0 else 2], out[5 if side > 0 else 3]
        prism_xz(bm, [corner, (out[4][0], apex), shoulder], -T / 2, T / 2, PLASTER)
        prism_xz(bm, [corner, shoulder, springing], -T / 2, T / 2, PLASTER)
    leaded_pane(bm, out, spacing=0.065)
    for side in (-1, 1):                                          # a surround that follows the arch
        for i in range(len(out)):
            a, b = out[i], out[(i + 1) % len(out)]
            if a[1] == s0 and b[1] == s0:
                continue
            nx, nz = (b[1] - a[1]), -(b[0] - a[0])
            ln = math.hypot(nx, nz) or 1
            nx, nz = nx / ln * 0.04, nz / ln * 0.04
            cx, cz = (w0 + w1) / 2, s1
            if nx * (a[0] - cx) + nz * (a[1] - cz) < 0:
                nx, nz = -nx, -nz
            y0, y1 = (side * T / 2, side * (T / 2 + 0.025))
            A.hull(bm, [(p[0] + dx, y, p[1] + dz) for p in (a, b) for dx, dz in ((0, 0), (nx, nz))
                        for y in (y0, y1)], TIMBER)
    box(bm, w0 - 0.05, w1 + 0.05, -T / 2 - 0.05, T / 2 + 0.02, s0 - 0.05, s0, TIMBER)
    return bm


def piece_wall_doorway():
    bm = bmesh.new()
    box(bm, 0, 0.12, -T / 2 - 0.02, T / 2 + 0.02, 0, PLINTH, STONE)
    box(bm, 0.88, 1, -T / 2 - 0.02, T / 2 + 0.02, 0, PLINTH, STONE)
    d1 = 1.85
    panel(bm, 0, 0.12, PLINTH, HI)
    panel(bm, 0.88, 1, PLINTH, HI)
    panel(bm, 0.12, 0.88, d1 + 0.1, HI)
    box(bm, 0, 1, -T / 2 - 0.015, T / 2 + 0.015, HI, STOREY, TIMBER)
    for x in (0.08, 0.84):
        box(bm, x, x + 0.08, -T / 2 - 0.02, T / 2 + 0.02, 0, d1 + 0.1, TIMBER)
    box(bm, 0.04, 0.96, -T / 2 - 0.03, T / 2 + 0.03, d1, d1 + 0.12, TIMBER)
    box(bm, 0.16, 0.84, -T / 2 - 0.02, T / 2 + 0.02, 0, 0.04, STONE)
    return bm


def piece_wall_barn_door():
    """A byre's opening: almost the whole tile, two plank leaves, a heavy lintel."""
    bm = bmesh.new()
    d1 = 1.95
    for x0, x1 in ((0, 0.06), (0.94, 1)):
        box(bm, x0, x1, -T / 2 - 0.02, T / 2 + 0.02, 0, HI, TIMBER)
    panel(bm, 0.06, 0.94, d1 + 0.12, HI)
    box(bm, 0, 1, -T / 2 - 0.015, T / 2 + 0.015, HI, STOREY, TIMBER)
    box(bm, 0, 1, -T / 2 - 0.04, T / 2 + 0.03, d1, d1 + 0.12, TIMBER)
    for x0, x1 in ((0.06, 0.5), (0.5, 0.94)):
        box(bm, x0 + 0.005, x1 - 0.005, -0.03, 0.03, 0.02, d1, PLANKS)
        A.hull(bm, [(x, y, z) for x, z in ((x0 + 0.04, 0.3), (x0 + 0.1, 0.3), (x1 - 0.04, d1 - 0.3), (x1 - 0.1, d1 - 0.3))
                    for y in (-0.05, -0.03)], TIMBER)              # a brace across each leaf
    return bm


def piece_door():
    """Planks behind heavy iron: three straps and studs (the iron worn openly)."""
    bm = bmesh.new()
    box(bm, 0.16, 0.84, -0.03, 0.03, 0.04, 1.85, PLANKS)
    for z in (0.35, 0.95, 1.55):
        box(bm, 0.16, 0.78, -0.05, -0.03, z, z + 0.08, IRON)
        for x in (0.3, 0.5, 0.7):
            box(bm, x - 0.018, x + 0.018, -0.065, -0.05, z + 0.022, z + 0.058, IRON)
    box(bm, 0.70, 0.75, -0.07, -0.03, 0.95, 1.02, IRON)
    return bm


def piece_jetty_beams():
    """Under a jettied upper storey: joist ends sticking out of the wall below."""
    bm = bmesh.new()
    for x in (0.17, 0.5, 0.83):
        box(bm, x - 0.06, x + 0.06, -T / 2 - JETTY, T / 2, -0.14, 0, TIMBER)
    box(bm, 0, 1, -T / 2 - JETTY - 0.01, -T / 2 - JETTY + 0.1, -0.02, 0.05, TIMBER)
    return bm


# ---- roof ----

def roof_slab(bm, run0, run1, x0=0.0, x1=1.0):
    pts = []
    for r in (run0, run1):
        z = ROOF_BASE + RISE * r
        for x in (x0, x1):
            pts += [(x, r, z), (x, r, z + SLAB)]
    return A.hull(bm, pts, THATCH)


def piece_roof_eave(x0=0.0, x1=1.0, plate=True):
    def make():
        bm = bmesh.new()
        roof_slab(bm, -OVERHANG, 1.0, x0, x1)
        z = ROOF_BASE - RISE * OVERHANG
        box(bm, x0, x1, -OVERHANG - 0.04, -OVERHANG + 0.12, z - 0.06, z + SLAB, THATCH)
        if plate:                                              # the wall plate, closing the eave
            top = ROOF_BASE - RISE * (T / 2 + 0.02) + SLAB * 0.5
            box(bm, x0, x1, -T / 2 - 0.02, T / 2 + 0.02, STOREY - 0.02, top, TIMBER)
        return bm
    return make


def piece_roof_slope(x0=0.0, x1=1.0):
    def make():
        bm = bmesh.new()
        roof_slab(bm, 0.0, 1.0, x0, x1)
        return bm
    return make


def piece_roof_eave_thatch(x0=0.0, x1=1.0, plate=True):
    """A thatch eave: the slab, then the thatch rolled over the edge in a fat, rounded lip."""
    def make():
        bm = bmesh.new()
        roof_slab(bm, -OVERHANG + 0.08, 1.0, x0, x1)
        z = ROOF_BASE - RISE * (OVERHANG - 0.08)
        y = -OVERHANG + 0.06
        A.loft(bm, [((x0, y, z), 0.0, 0.0), ((x0, y, z), 0.13, 0.11), ((x1, y, z), 0.13, 0.11), ((x1, y, z), 0.0, 0.0)],
               sides=6, ref=(0, 0, 1), mats=THATCH)
        if plate:
            top = ROOF_BASE - RISE * (T / 2 + 0.02) + SLAB * 0.5
            box(bm, x0, x1, -T / 2 - 0.02, T / 2 + 0.02, STOREY - 0.02, top, TIMBER)
        return bm
    return make


def piece_roof_ridge_thatch(x0=0.0, x1=1.0):
    """A thatch ridge: a thick rounded bundle along the top, bound with two hazel liggers."""
    def make():
        bm = bmesh.new()
        z = ROOF_BASE + SLAB - 0.02
        A.loft(bm, [((x0, 0, z), 0.0, 0.0), ((x0, 0, z), 0.24, 0.13), ((x1, 0, z), 0.24, 0.13), ((x1, 0, z), 0.0, 0.0)],
               sides=6, ref=(0, 0, 1), roll=math.pi / 6, mats=THATCH)
        for y in (-0.15, 0.15):
            zz = z + 0.08 - abs(y) * 0.35
            box(bm, x0, x1, y - 0.012, y + 0.012, zz, zz + 0.025, BARK)
        return bm
    return make


def piece_roof_ridge(x0=0.0, x1=1.0):
    def make():
        bm = bmesh.new()
        z = ROOF_BASE + SLAB
        A.hull(bm, [(x, y, zz) for x in (x0, x1) for y, zz in ((-0.16, z - RISE * 0.16), (0.16, z - RISE * 0.16),
                                                              (-0.06, z + 0.08), (0.06, z + 0.08))], TIMBER)
        return bm
    return make


def piece_gable(k, slit=False):
    """The end wall above the storey, k tiles in from the eave, rising with the roof. slit: the
    top piece carries the wound: a narrow dark lancet."""
    def make():
        bm = bmesh.new()
        z0, z1 = ROOF_BASE + RISE * k, ROOF_BASE + RISE * (k + 1)
        prism_xz(bm, [(0, STOREY), (1, STOREY), (1, z1 - 0.02), (0, z0 - 0.02)], -T / 2, T / 2, PLASTER)
        A.hull(bm, [(x, y, z) for x, z in ((0, z0 - 0.06), (1, z1 - 0.06), (1, z1 + 0.04), (0, z0 + 0.04))
                    for y in (-T / 2 - 0.03, T / 2 + 0.03)], TIMBER)
        if slit:
            out = lancet_outline(0.86, 0.98, STOREY + 0.25, STOREY + 0.65, STOREY + 0.82)
            prism_xz(bm, out, -T / 2 - 0.005, -T / 2 + 0.01, DARK)
        return bm
    return make


# ---- props ----

def piece_font():
    """The tithe font by a sworn door: a stone basin on a stem, dark with blood."""
    bm = bmesh.new()
    A.loft(bm, [((0, 0, 0), 0.16, 0.16), ((0, 0, 0.1), 0.16, 0.16), ((0, 0, 0.12), 0.07, 0.07),
                ((0, 0, 0.5), 0.06, 0.06), ((0, 0, 0.55), 0.17, 0.17), ((0, 0, 0.68), 0.19, 0.19)],
           sides=8, ref=(0, 1, 0), mats=STONE)
    A.loft(bm, [((0, 0, 0.684), 0.15, 0.15), ((0, 0, 0.686), 0, 0)], sides=8, ref=(0, 1, 0), mats=BLOOD)
    return bm


def piece_bellcote():
    """A small stone frame on a gable's apex with one bronze bell (origin at the apex)."""
    bm = bmesh.new()
    for x in (-0.26, 0.18):
        box(bm, x, x + 0.08, -0.12, 0.12, -0.1, 0.75, STONE)
    A.hull(bm, [(x, y, z) for x, z in ((-0.3, 0.75), (0.3, 0.75), (0, 1.05)) for y in (-0.13, 0.13)], STONE)
    box(bm, -0.3, 0.3, -0.13, 0.13, -0.15, -0.05, STONE)
    A.loft(bm, [((0, 0, 0.62), 0.03, 0.03), ((0, 0, 0.55), 0.1, 0.1), ((0, 0, 0.3), 0.14, 0.14),
                ((0, 0, 0.26), 0.0, 0.0)], sides=8, ref=(0, 1, 0), mats=BRONZE)
    return bm


def piece_chimney_breast():
    """The hearth's back, outside an end wall tile: a stone breast a storey high whose top steps
    in to the stack (in the wall tile's frame: x along the wall, -Y outside)."""
    bm = bmesh.new()
    box(bm, 0.1, 0.9, -T / 2 - 0.42, T / 2, 0, STOREY - 0.5, STONE)
    A.hull(bm, [(x, y, z) for x, z in ((0.1, STOREY - 0.5), (0.9, STOREY - 0.5), (0.72, STOREY - 0.1), (0.28, STOREY - 0.1))
                for y in (-T / 2 - 0.42, T / 2)], STONE)
    return bm


def piece_chimney_stack():
    """One tile's height of stack above the breast (stacked as high as the roof needs)."""
    bm = bmesh.new()
    box(bm, 0.28, 0.72, -T / 2 - 0.36, T / 2 - 0.04, 0, 1.0, STONE)
    return bm


def piece_chimney_cap():
    bm = bmesh.new()
    box(bm, 0.24, 0.76, -T / 2 - 0.40, T / 2, 0, 0.12, STONE)
    box(bm, 0.36, 0.64, -T / 2 - 0.3, T / 2 - 0.1, 0.12, 0.125, DARK)
    return bm


def piece_banner():
    """A church banner on an iron bracket: the regime's red. An attachment: it hangs on a plain
    wall tile, in that tile's frame."""
    bm = bmesh.new()
    c = 0.5                                                    # the middle of a wall tile
    box(bm, c - 0.02, c + 0.02, -0.32, -T / 2, 1.72, 1.76, IRON)
    box(bm, c - 0.24, c + 0.24, -0.33, -0.29, 1.70, 1.74, IRON)
    A.hull(bm, [(c + x, y, z) for x, z in ((-0.22, 1.7), (0.22, 1.7), (0.22, 0.75), (0.0, 0.62), (-0.22, 0.75))
                for y in (-0.315, -0.30)], BANNER)
    box(bm, c - 0.05, c + 0.05, -0.32, -0.316, 1.22, 1.32, BRONZE)
    return bm


def piece_flower_box():
    """Under a window: a plank box of soil, greens and a few flowers (a window attachment)."""
    bm = bmesh.new()
    box(bm, 0.24, 0.76, -T / 2 - 0.2, -T / 2, 0.80, 0.93, PLANKS)
    for i, x in enumerate((0.3, 0.4, 0.5, 0.6, 0.7)):
        A.hull(bm, [(x + dx, -T / 2 - 0.1 + dy, 0.93 + dz) for dx in (-0.06, 0.06) for dy in (-0.07, 0.07)
                    for dz in (0.0, 0.07 + 0.03 * (i % 2))], LEAF if i % 2 else LEAF_DARK)
    for i, x in enumerate((0.33, 0.47, 0.62, 0.71)):
        A.hull(bm, [(x + dx, -T / 2 - 0.1 + dy, 1.04 + dz) for dx in (-0.025, 0.025) for dy in (-0.025, 0.025)
                    for dz in (0.0, 0.04)], FLOWER if i % 2 else FLOWER2)
    return bm


def piece_lantern():
    """Beside a door: an iron bracket and a small lantern with its candle (a doorway attachment)."""
    bm = bmesh.new()
    x = 0.97
    box(bm, x - 0.02, x + 0.02, -T / 2 - 0.25, -T / 2, 1.88, 1.92, IRON)
    box(bm, x - 0.07, x + 0.07, -T / 2 - 0.32, -T / 2 - 0.18, 1.62, 1.64, IRON)
    box(bm, x - 0.06, x + 0.06, -T / 2 - 0.31, -T / 2 - 0.19, 1.64, 1.80, CANDLE)
    A.hull(bm, [(x + dx, -T / 2 - 0.25 + dy, 1.80) for dx in (-0.08, 0.08) for dy in (-0.07, 0.07)]
           + [(x, -T / 2 - 0.25, 1.9)], IRON)
    return bm


def piece_woodpile():
    """Split logs stacked against a plain wall, end grain out."""
    bm = bmesh.new()
    rows = ((0.09, 5, 0.0), (0.25, 4, 0.085), (0.41, 3, 0.17))
    for z, n, shift in rows:
        for i in range(n):
            x = 0.16 + shift + i * 0.17
            A.loft(bm, [((x, -T / 2 - 0.4, z), 0.08, 0.08), ((x, -T / 2 - 0.02, z), 0.08, 0.08)],
                   sides=5, ref=(0, 0, 1), mats=[BARK])
            A.loft(bm, [((x, -T / 2 - 0.401, z), 0.0, 0.0), ((x, -T / 2 - 0.4005, z), 0.072, 0.072)],
                   sides=5, ref=(0, 0, 1), mats=[LOG_END])          # the cut end
    box(bm, 0.04, 0.96, -T / 2 - 0.44, -T / 2, 0.52, 0.56, OAK)          # a board keeping the rain off
    return bm


def piece_bench():
    bm = bmesh.new()
    box(bm, 0.1, 0.9, -T / 2 - 0.36, -T / 2 - 0.04, 0.42, 0.48, PLANKS)
    for x in (0.16, 0.78):
        box(bm, x, x + 0.06, -T / 2 - 0.32, -T / 2 - 0.08, 0, 0.42, OAK)
    return bm


def piece_ivy():
    """Ivy climbing a plain wall: clumps of leaves thinning out as it climbs."""
    bm = bmesh.new()
    import random
    rnd = random.Random(31)
    for i in range(16):
        z = rnd.uniform(0.0, 1.0) ** 1.6 * 1.9
        x = 0.5 + rnd.uniform(-0.38, 0.38) * (1 - z / 2.6)
        r = rnd.uniform(0.07, 0.13)
        A.hull(bm, [(x + rnd.uniform(-r, r), -T / 2 - rnd.uniform(0.0, 0.08), z + rnd.uniform(-r, r)) for _ in range(7)],
               LEAF if i % 3 else LEAF_DARK)
    return bm


def piece_grass():
    """Tufts along a wall's foot, where the scythe doesn't reach."""
    bm = bmesh.new()
    import random
    rnd = random.Random(7)
    for i in range(5):
        x = rnd.uniform(0.08, 0.92)
        y = -T / 2 - rnd.uniform(0.03, 0.1)
        for k in range(3):
            a = k * 2.1 + rnd.uniform(0, 0.6)
            A.hull(bm, [(x + 0.03 * math.cos(a + d), y + 0.03 * math.sin(a + d), 0) for d in (0, 2.1, 4.2)]
                   + [(x + 0.04 * math.cos(a), y + 0.04 * math.sin(a), rnd.uniform(0.14, 0.26))], GRASS)
    return bm


def piece_barrel():
    bm = bmesh.new()
    A.loft(bm, [((0, 0, 0), 0.2, 0.2), ((0, 0, 0.3), 0.24, 0.24), ((0, 0, 0.6), 0.2, 0.2)],
           sides=8, ref=(0, 1, 0), mats=OAK)
    for z in (0.08, 0.52):
        A.loft(bm, [((0, 0, z), 0.215, 0.215), ((0, 0, z + 0.04), 0.225, 0.225)], sides=8, ref=(0, 1, 0), mats=IRON)
    return bm


def piece_crate():
    bm = bmesh.new()
    box(bm, -0.25, 0.25, -0.25, 0.25, 0, 0.5, PLANKS)
    for x0, y0 in ((-0.27, -0.27), (0.21, -0.27), (-0.27, 0.21), (0.21, 0.21)):
        box(bm, x0, x0 + 0.06, y0, y0 + 0.06, 0, 0.5, OAK)
    return bm


def piece_hay():
    bm = bmesh.new()
    A.hull(bm, [(x, y, z) for x in (-0.45, 0.45) for y in (-0.25, 0.25) for z in (0, 0.42)]
           + [(x, y, 0.47) for x in (-0.4, 0.4) for y in (-0.2, 0.2)], HAY)
    for x in (-0.2, 0.2):
        box(bm, x - 0.015, x + 0.015, -0.26, 0.26, 0.0, 0.48, BARK)
    return bm


PIECES = {
    "Post": piece_post, "Wall": piece_wall, "WallBraced": piece_wall_braced,
    "WallShuttered": piece_wall_shuttered, "WallLeaded": piece_wall_leaded, "WallLancet": piece_wall_lancet,
    "WallDoorway": piece_wall_doorway, "Door": piece_door, "JettyBeams": piece_jetty_beams,
    "RoofEave": piece_roof_eave(), "RoofSlope": piece_roof_slope(), "RoofRidge": piece_roof_ridge(),
    "VergeEave": piece_roof_eave(0, VERGE, plate=False), "VergeSlope": piece_roof_slope(0, VERGE),
    "VergeRidge": piece_roof_ridge(0, VERGE),
    "Gable0": piece_gable(0), "Gable1": piece_gable(1), "Gable1Slit": piece_gable(1, slit=True),
    "Font": piece_font, "Bellcote": piece_bellcote, "ChimneyBreast": piece_chimney_breast,
    "ChimneyStack": piece_chimney_stack, "ChimneyCap": piece_chimney_cap, "Banner": piece_banner,
    "WallBarnDoor": piece_wall_barn_door,
    "RoofEaveThatch": piece_roof_eave_thatch(), "RoofRidgeThatch": piece_roof_ridge_thatch(),
    "VergeEaveThatch": piece_roof_eave_thatch(0, VERGE, plate=False), "VergeRidgeThatch": piece_roof_ridge_thatch(0, VERGE),
    "FlowerBox": piece_flower_box, "Lantern": piece_lantern, "Woodpile": piece_woodpile, "Bench": piece_bench,
    "Ivy": piece_ivy, "Grass": piece_grass, "Barrel": piece_barrel, "Crate": piece_crate, "Hay": piece_hay,
}


def build_pieces():
    kit = {}
    for name, make in PIECES.items():
        bm = make()
        A.finish(bm)
        kit[name] = A.mesh_from("Kit_" + name, bm, MATS, coll="Kit")
    return kit


# ---- putting buildings together ----

def place(kit, name, coll, x, y, rot=0.0, z=0.0, recolor=None, scale_x=1.0):
    src = kit[name]
    o = bpy.data.objects.new(f"{coll.name}_{name}", src.data)
    o.location = (x, y, z)
    o.rotation_euler = (0, 0, math.radians(rot))
    o.scale = (scale_x, 1, 1)
    coll.objects.link(o)
    if recolor:
        for i, m in enumerate(src.data.materials):
            if m.name in recolor:
                o.material_slots[i].link = "OBJECT"
                o.material_slots[i].material = A.mat(recolor[m.name])
    return o


OPENINGS = {"WallDoorway": "Door"}           # pieces that bring their leaf with them
PLAIN = {"Wall", "WallBraced"}
WINDOWS = {"WallShuttered", "WallLeaded"}
# What may hang on which wall tiles, so dressing never covers a door or a window.
ATTACHMENTS = {"Banner": PLAIN, "Woodpile": PLAIN, "Bench": PLAIN, "Ivy": PLAIN,
               "FlowerBox": WINDOWS, "Lantern": {"WallDoorway"}}


def place_tile(kit, coll, entry, x, y, rot, z, rc):
    """One wall tile: "Wall", or "Wall+Banner" (attachments only on a plain wall, so they never
    cover a door or a window)."""
    name, *extra = entry.split("+")
    place(kit, name, coll, x, y, rot, z, rc)
    if name in OPENINGS:
        place(kit, OPENINGS[name], coll, x, y, rot, z, rc)
    for a in extra:
        assert a in ATTACHMENTS and name in ATTACHMENTS[a], f"{a} can't go on {name}"
        place(kit, a, coll, x, y, rot, z, rc)
    if z == 0 and name not in ("WallDoorway", "WallBarnDoor") and "Woodpile" not in extra and "Bench" not in extra:
        place(kit, "Grass", coll, x, y, rot, 0)                    # tufts along the foot of the wall


def volume(kit, coll, ox, oy, length, depth, faces, storeys=1, walls=None, roof=None, jetty=False,
           slit=None, bellcote=None, chimney=None, thatch=True):
    """One block of a building on tiles [ox, ox+length) x [oy, oy+depth), gabled with its ridge
    along X (depth even). faces: {(face, storey): [entry per tile]} for faces "S" (front, -Y),
    "N", "W", "E", tiles listed west to east / south to north; an entry is a piece name with
    optional attachments ("Wall+Banner"); missing tiles are plain "Wall". walls: a recolour per
    storey. roof: the roof's recolour. jetty: upper storeys jut out over the gable ends.
    slit: the end ("W"/"E") whose top gable has the lancet slit. bellcote: the end carrying a
    bell. chimney: (end, tile), a stack in that end wall where the hearth is, or None. thatch: a
    thatched roof (rolled eaves, a bundled ridge); else thin-edged (slate, shingle)."""
    eave, ridge = ("RoofEaveThatch", "RoofRidgeThatch") if thatch else ("RoofEave", "RoofRidge")
    v_eave, v_ridge = ("VergeEaveThatch", "VergeRidgeThatch") if thatch else ("VergeEave", "VergeRidge")
    walls = walls or [None] * storeys
    half = depth // 2

    def storey_recolour(s):
        rc = walls[s] if s < len(walls) else None
        if s > 0:
            rc = dict({"Stone": "Timber"}, **(rc or {}))          # upper storeys sit on a timber sill
        return rc

    for s in range(storeys):
        z = s * STOREY
        jet = JETTY if (jetty and s > 0) else 0.0
        rc = storey_recolour(s)

        def tiles(face, n):
            given = faces.get((face, s), [])
            return [given[i] if i < len(given) and given[i] else "Wall" for i in range(n)]

        for i, entry in enumerate(tiles("S", length)):
            place_tile(kit, coll, entry, ox + i, oy, 0, z, rc)
        for i, entry in enumerate(tiles("N", length)):
            place_tile(kit, coll, entry, ox + i + 1, oy + depth, 180, z, rc)
        for j, entry in enumerate(tiles("W", depth)):
            place_tile(kit, coll, entry, ox - jet, oy + j + 1, 270, z, rc)
            if jet:
                place(kit, "JettyBeams", coll, ox, oy + j + 1, 270, z, rc)
        for j, entry in enumerate(tiles("E", depth)):
            place_tile(kit, coll, entry, ox + length + jet, oy + j, 90, z, rc)
            if jet:
                place(kit, "JettyBeams", coll, ox + length, oy + j, 90, z, rc)
        if jet:                                                    # close the corners the jetty opens
            place(kit, "Wall", coll, ox - jet, oy, 0, z, rc, scale_x=jet)
            place(kit, "Wall", coll, ox + length, oy, 0, z, rc, scale_x=jet)
            place(kit, "Wall", coll, ox, oy + depth, 180, z, rc, scale_x=jet)
            place(kit, "Wall", coll, ox + length + jet, oy + depth, 180, z, rc, scale_x=jet)
        for cx in (ox - jet, ox + length + jet):
            for cy in (oy, oy + depth):
                place(kit, "Post", coll, cx, cy, 0, z, rc)
    top = (storeys - 1) * STOREY
    jet = JETTY if (jetty and storeys > 1) else 0.0
    rc = storey_recolour(storeys - 1)
    for k in range(half):
        for end, ex in (("W", ox - jet), ("E", ox + length + jet)):
            name = "Gable1Slit" if (slit == end and k == half - 1) else f"Gable{k}"
            place(kit, name, coll, ex, oy + k, 90, top, rc)
            place(kit, f"Gable{k}", coll, ex, oy + depth - k, 270, top, rc)
    for i in range(length):
        x = ox + i
        place(kit, eave, coll, x, oy, 0, top, roof)
        place(kit, eave, coll, x + 1, oy + depth, 180, top, roof)
        for k in range(1, half):
            place(kit, "RoofSlope", coll, x, oy + k, 0, top + RISE * k, roof)
            place(kit, "RoofSlope", coll, x + 1, oy + depth - k, 180, top + RISE * k, roof)
        place(kit, ridge, coll, x, oy + half, 0, top + RISE * half, roof)
    for vx in (ox - VERGE, ox + length):                          # the roof reaches past the gable ends
        place(kit, v_eave, coll, vx, oy, 0, top, roof)
        place(kit, v_eave, coll, vx + VERGE, oy + depth, 180, top, roof)
        for k in range(1, half):
            place(kit, "VergeSlope", coll, vx, oy + k, 0, top + RISE * k, roof)
            place(kit, "VergeSlope", coll, vx + VERGE, oy + depth - k, 180, top + RISE * k, roof)
        place(kit, v_ridge, coll, vx, oy + half, 0, top + RISE * half, roof)
    ridge_z = top + ROOF_BASE + RISE * half + SLAB
    if bellcote:
        bx = ox - jet if bellcote == "W" else ox + length + jet
        place(kit, "Bellcote", coll, bx, oy + half, 90, ridge_z - SLAB - 0.05, walls[-1] if walls else None)
    if chimney:
        end, j = chimney
        if end == "W":
            x, y, rot = ox - jet, oy + j + 1, 270
        else:
            x, y, rot = ox + length + jet, oy + j, 90
        place(kit, "ChimneyBreast", coll, x, y, rot, 0, walls[0] if walls else None)
        h = STOREY - 0.1
        while h < ridge_z + 0.35:                                  # stack up past the ridge
            place(kit, "ChimneyStack", coll, x, y, rot, h, walls[0] if walls else None)
            h += 1.0
        place(kit, "ChimneyCap", coll, x, y, rot, h, walls[0] if walls else None)


# ---- the village (and the first town pieces) ----

FIELDSTONE = {"Plaster": "Stone", "Timber": "Stone", "Stone": "Granite"}
CUT_STONE = {"Plaster": "Cobble", "Timber": "Cobble", "Stone": "Granite"}


def cottage(kit, ox, oy):
    """A poor cottage: timber frame and thatch, green-painted shutters with flowers under them, a
    font and a lantern at the door, firewood by the hearth end, the slit in a gable."""
    coll = A.collection("Cottage")
    paint = {"Planks": "Moor"}
    volume(kit, coll, ox, oy, 4, 4, {
        ("S", 0): ["WallShuttered+FlowerBox", "WallDoorway+Lantern", "WallBraced", "WallShuttered+FlowerBox"],
        ("E", 0): ["Wall+Ivy", "WallShuttered", None, None],
        ("W", 0): ["Wall+Woodpile", None, None, "Wall+Woodpile"],
        ("N", 0): [None, "WallShuttered", None, None]},
        walls=[paint], slit="E", chimney=("W", 1))
    place(kit, "Font", coll, ox + 1.15, oy - 0.45)
    place(kit, "Barrel", coll, ox + 4.45, oy - 0.35)


def longhouse(kit, ox, oy):
    """A farm's longhouse: people at the east end by the hearth, beasts in the byre at the west
    end behind the wide doors, all under one long thatch. Pale lime plaster, blue-grey shutters,
    hay and crates by the byre, a bench by the door."""
    coll = A.collection("Longhouse")
    paint = {"Stone": "Granite", "Plaster": "Linen", "Planks": "Indigo"}
    volume(kit, coll, ox, oy, 7, 4, {
        ("S", 0): ["WallBarnDoor", "WallBraced", "Wall+Bench", "WallShuttered+FlowerBox", "WallDoorway+Lantern",
                   "WallBraced+Ivy", "WallShuttered"],
        ("N", 0): [None, "WallBraced", None, "WallShuttered", None, "WallShuttered", None],
        ("W", 0): [None, "WallBraced", "WallBraced", None]},
        walls=[paint], chimney=("E", 1), slit="W")
    place(kit, "Font", coll, ox + 4.15, oy - 0.45)
    place(kit, "Hay", coll, ox - 0.9, oy + 0.4, 15)
    place(kit, "Hay", coll, ox - 0.95, oy + 1.4, -10)
    place(kit, "Crate", coll, ox + 1.4, oy - 0.5, 10)
    place(kit, "Barrel", coll, ox + 1.95, oy - 0.45)


def village_chapel(kit, ox, oy):
    """A village chapel: fieldstone and thatch, a door in the west end under the bell, two red
    lancets each side, one banner by the door, ivy on the north side."""
    coll = A.collection("VillageChapel")
    volume(kit, coll, ox, oy, 5, 4, {
        ("S", 0): ["Wall+Banner", "WallLancet", "Wall", "WallLancet", "Wall"],
        ("N", 0): ["Wall+Ivy", "WallLancet", "Wall", "WallLancet", "Wall+Ivy"],
        ("W", 0): ["Wall", "WallDoorway+Lantern", "Wall", "Wall"],
        ("E", 0): ["Wall", "WallLancet", "WallLancet", "Wall"]},
        walls=[dict(FIELDSTONE, Glaze="SynodRed")], bellcote="W")
    place(kit, "Font", coll, ox - 1.0, oy + 1.5)


def townhouse(kit, ox, oy):
    """A town's house (not a village's): cut stone below, a jettied timber storey above,
    leaded windows, slate."""
    coll = A.collection("Townhouse")
    volume(kit, coll, ox, oy, 5, 4, {
        ("S", 0): ["WallLeaded", None, "WallDoorway", None, "WallLeaded"],
        ("S", 1): ["WallLeaded", "WallBraced", "WallLeaded", "WallBraced", "WallLeaded"],
        ("N", 1): [None, "WallLeaded", None, "WallLeaded", None],
        ("W", 1): [None, "WallLeaded", "WallLeaded", None], ("E", 1): [None, "WallBraced", "WallBraced", None],
        ("W", 0): [None, "WallLeaded", None, None]},
        storeys=2, walls=[CUT_STONE, None], roof={"Thatch": "Slate", "Timber": "DarkRock"}, jetty=True,
        slit="E", chimney=("E", 1), thatch=False)


def build():
    A.reset()
    kit = build_pieces()
    result = {name: A.report([o])["triangles"] for name, o in kit.items()}
    if EXPORT:
        for name, o in kit.items():
            A.export([o], f"Art/Buildings/Venaria/{name}.fbx")
    for i, o in enumerate(kit.values()):
        o.location = (-4.0 + i * 1.3, -9.0, 0)
    cottage(kit, 0, 0)
    longhouse(kit, 7, 0)
    village_chapel(kit, 18, 0)
    townhouse(kit, 8, 9)
    return kit, result


kit, result = build()
cam = A.preview_rig(focus=(12.0, 3.0, 2.0), distance=28, pitch=35, yaw=-20, ground=18, human_at=(5.5, -1.5))
