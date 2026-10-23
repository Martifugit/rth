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
        "Blood", "SynodRed", "Bronze"]
(STONE, PLASTER, TIMBER, THATCH, PLANKS, IRON, DARK, GLASS, LEAD, BLOOD, BANNER, BRONZE) = range(len(MATS))


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


def piece_roof_eave(x0=0.0, x1=1.0):
    def make():
        bm = bmesh.new()
        roof_slab(bm, -OVERHANG, 1.0, x0, x1)
        z = ROOF_BASE - RISE * OVERHANG
        box(bm, x0, x1, -OVERHANG - 0.04, -OVERHANG + 0.12, z - 0.06, z + SLAB, THATCH)
        return bm
    return make


def piece_roof_slope(x0=0.0, x1=1.0):
    def make():
        bm = bmesh.new()
        roof_slab(bm, 0.0, 1.0, x0, x1)
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


def piece_chimney():
    bm = bmesh.new()
    box(bm, -0.24, 0.24, -0.24, 0.24, 0, 1.0, STONE)
    box(bm, -0.28, 0.28, -0.28, 0.28, 1.0, 1.12, STONE)
    box(bm, -0.14, 0.14, -0.14, 0.14, 1.12, 1.13, DARK)
    return bm


def piece_banner():
    """A church banner on an iron bracket: the regime's red (origin on the wall's face)."""
    bm = bmesh.new()
    box(bm, -0.02, 0.02, -0.32, 0.0, 1.72, 1.76, IRON)
    box(bm, -0.24, 0.24, -0.33, -0.29, 1.70, 1.74, IRON)
    A.hull(bm, [(x, y, z) for x, z in ((-0.22, 1.7), (0.22, 1.7), (0.22, 0.75), (0.0, 0.62), (-0.22, 0.75))
                for y in (-0.315, -0.30)], BANNER)
    box(bm, -0.05, 0.05, -0.32, -0.316, 1.22, 1.32, BRONZE)
    return bm


PIECES = {
    "Post": piece_post, "Wall": piece_wall, "WallBraced": piece_wall_braced,
    "WallShuttered": piece_wall_shuttered, "WallLeaded": piece_wall_leaded, "WallLancet": piece_wall_lancet,
    "WallDoorway": piece_wall_doorway, "Door": piece_door, "JettyBeams": piece_jetty_beams,
    "RoofEave": piece_roof_eave(), "RoofSlope": piece_roof_slope(), "RoofRidge": piece_roof_ridge(),
    "VergeEave": piece_roof_eave(0, VERGE), "VergeSlope": piece_roof_slope(0, VERGE),
    "VergeRidge": piece_roof_ridge(0, VERGE),
    "Gable0": piece_gable(0), "Gable1": piece_gable(1), "Gable1Slit": piece_gable(1, slit=True),
    "Font": piece_font, "Bellcote": piece_bellcote, "Chimney": piece_chimney, "Banner": piece_banner,
}


def build_pieces():
    kit = {}
    for name, make in PIECES.items():
        bm = make()
        A.finish(bm)
        kit[name] = A.mesh_from("Kit_" + name, bm, MATS, coll="Kit")
    return kit


# ---- putting buildings together ----

def place(kit, name, coll, x, y, rot=0.0, z=0.0, recolor=None):
    src = kit[name]
    o = bpy.data.objects.new(f"{coll.name}_{name}", src.data)
    o.location = (x, y, z)
    o.rotation_euler = (0, 0, math.radians(rot))
    coll.objects.link(o)
    if recolor:
        for i, m in enumerate(src.data.materials):
            if m.name in recolor:
                o.material_slots[i].link = "OBJECT"
                o.material_slots[i].material = A.mat(recolor[m.name])
    return o


def volume(kit, coll, ox, oy, length, depth, faces, storeys=1, walls=None, roof=None, jetty=False,
           slit=None, bellcote=False):
    """One block of a building on tiles [ox, ox+length) x [oy, oy+depth), gabled with its ridge
    along X (depth even). faces: {(face, storey): [piece per tile]} for faces "S" (front, -Y),
    "N", "W", "E", tiles listed west to east / south to north; missing tiles are plain "Wall".
    walls: a recolour per storey (list). roof: the roof's recolour. jetty: upper storeys jut out
    over the gable ends. slit: "W" or "E", the end whose top gable carries the lancet slit."""
    walls = walls or [None] * storeys
    half = depth // 2
    for s in range(storeys):
        z = s * STOREY
        jet = JETTY if (jetty and s > 0) else 0.0
        rc = walls[s] if s < len(walls) else None
        if s > 0:
            rc = dict({"Stone": "Timber"}, **(rc or {}))          # upper storeys sit on a timber sill

        def tiles(face, n):
            given = faces.get((face, s), [])
            return [given[i] if i < len(given) and given[i] else "Wall" for i in range(n)]

        for i, name in enumerate(tiles("S", length)):
            place(kit, name, coll, ox + i, oy, 0, z, rc)
            if name == "WallDoorway":
                place(kit, "Door", coll, ox + i, oy, 0, z, rc)
        for i, name in enumerate(tiles("N", length)):
            place(kit, name, coll, ox + i + 1, oy + depth, 180, z, rc)
        for j, name in enumerate(tiles("W", depth)):
            place(kit, name, coll, ox - jet, oy + j + 1, 270, z, rc)
            if jet:
                place(kit, "JettyBeams", coll, ox, oy + j + 1, 270, z, rc)
        for j, name in enumerate(tiles("E", depth)):
            place(kit, name, coll, ox + length + jet, oy + j, 90, z, rc)
            if jet:
                place(kit, "JettyBeams", coll, ox + length, oy + j, 90, z, rc)
        for cx in (ox - jet, ox + length + jet):
            for cy in (oy, oy + depth):
                place(kit, "Post", coll, cx, cy, 0, z, rc)
    top = (storeys - 1) * STOREY
    jet = JETTY if (jetty and storeys > 1) else 0.0
    rc = walls[-1] if walls else None
    rc = dict({"Stone": "Timber"}, **(rc or {})) if storeys > 1 else rc
    for k in range(half):
        for end, ex in (("W", ox - jet), ("E", ox + length + jet)):
            name = "Gable1Slit" if (slit == end and k == half - 1) else f"Gable{k}"
            place(kit, name, coll, ex, oy + k, 90, top, rc)
            place(kit, f"Gable{k}", coll, ex, oy + depth - k, 270, top, rc)
    for i in range(length):
        x = ox + i
        place(kit, "RoofEave", coll, x, oy, 0, top, roof)
        place(kit, "RoofEave", coll, x + 1, oy + depth, 180, top, roof)
        for k in range(1, half):
            place(kit, "RoofSlope", coll, x, oy + k, 0, top + RISE * k, roof)
            place(kit, "RoofSlope", coll, x + 1, oy + depth - k, 180, top + RISE * k, roof)
        place(kit, "RoofRidge", coll, x, oy + half, 0, top + RISE * half, roof)
    for vx in (ox - VERGE, ox + length):                          # the roof reaches past the gable ends
        place(kit, "VergeEave", coll, vx, oy, 0, top, roof)
        place(kit, "VergeEave", coll, vx + VERGE, oy + depth, 180, top, roof)
        for k in range(1, half):
            place(kit, "VergeSlope", coll, vx, oy + k, 0, top + RISE * k, roof)
            place(kit, "VergeSlope", coll, vx + VERGE, oy + depth - k, 180, top + RISE * k, roof)
        place(kit, "VergeRidge", coll, vx, oy + half, 0, top + RISE * half, roof)
    if bellcote:
        place(kit, "Bellcote", coll, ox - jet - 0.02, oy + half, 90, top + ROOF_BASE + RISE * half - 0.05,
              walls[-1] if walls else None)


# ---- the village ----

STONE_WALLS = {"Plaster": "Cobble", "Timber": "Cobble", "Stone": "Granite"}


def cottage(kit, ox, oy):
    """A poor cottage: timber frame, thatch, shutters, a font by the door, the slit in a gable."""
    coll = A.collection("Cottage")
    volume(kit, coll, ox, oy, 4, 4, {("S", 0): ["WallShuttered", "WallDoorway", "WallBraced", "WallShuttered"],
                                     ("E", 0): [None, "WallShuttered", None, None],
                                     ("N", 0): [None, "WallShuttered", None, None]}, slit="W")
    place(kit, "Font", coll, ox + 1.15, oy - 0.45)


def farmhouse(kit, ox, oy):
    """A better-off farmhouse: stone below, a jettied timber storey above, leaded windows, slate."""
    coll = A.collection("Farmhouse")
    volume(kit, coll, ox, oy, 5, 4, {
        ("S", 0): ["WallLeaded", None, "WallDoorway", None, "WallLeaded"],
        ("S", 1): ["WallLeaded", "WallBraced", "WallLeaded", "WallBraced", "WallLeaded"],
        ("N", 1): [None, "WallLeaded", None, "WallLeaded", None],
        ("W", 1): [None, "WallLeaded", "WallLeaded", None], ("E", 1): [None, "WallBraced", "WallBraced", None],
        ("W", 0): [None, "WallLeaded", None, None]},
        storeys=2, walls=[STONE_WALLS, None], roof={"Thatch": "Slate", "Timber": "DarkRock"}, jetty=True, slit="E")
    place(kit, "Chimney", coll, ox + 3.5, oy + 3.0, 0, 0)
    bpy.data.objects[f"{coll.name}_Chimney"].scale = (1, 1, 2 * STOREY + RISE * 2 + 0.4)
    place(kit, "Font", coll, ox + 2.15, oy - 0.45)


def chapel(kit, ox, oy):
    """The village chapel: stone, red leaded lancets, a bell-cote, the iron door, a banner."""
    coll = A.collection("Chapel")
    volume(kit, coll, ox, oy, 6, 4, {
        ("S", 0): ["WallLancet", "WallLancet", "WallDoorway", "WallLancet", "WallLancet", "WallLancet"],
        ("N", 0): ["WallLancet"] * 6,
        ("E", 0): [None, "WallLancet", "WallLancet", None]},
        walls=[dict(STONE_WALLS, Glaze="SynodRed")], roof={"Thatch": "Slate", "Timber": "DarkRock"},
        bellcote=True, slit="W")
    place(kit, "Banner", coll, ox + 2.0, oy - T / 2, 0, 0)
    place(kit, "Banner", coll, ox + 3.0, oy - T / 2, 0, 0)
    place(kit, "Font", coll, ox + 2.5, oy - 1.2)


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
    farmhouse(kit, 7, 0)
    chapel(kit, 15, 0)
    return kit, result


kit, result = build()
cam = A.preview_rig(focus=(11.0, 2.0, 2.0), distance=26, pitch=35, yaw=-20, ground=16, human_at=(5.5, -1.5))
