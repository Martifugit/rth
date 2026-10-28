"""Walls, village. The village kit's walls, a tile each (X along the wall, -Y outside): plain, braced, shuttered, leaded and lancet walls, doorways, posts, the jetty's beams, gables and the chimney breast.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Walls/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\walls\\village.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.village import (  # noqa: E402
    DARK, DOOR, GLASS, LEAD, PLANKS, PLASTER, RISE,
    ROOF_BASE, STONE, STOREY, T, TIMBER, box, piece)


PLINTH = 0.35
RAIL = 0.12
POST = 0.13
JETTY = 0.25                    # how far an upper storey juts out over a gable end
MAX_SPAN = 8                    # the widest gable span (tiles across the ridge) the gable pieces cover


def prism_xz(bm, outline, y0, y1, mat):
    """A convex outline in the wall's plane (x, z), given depth from y0 to y1."""
    return A.hull(bm, [(x, y, z) for x, z in outline for y in (y0, y1)], mat)


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
    w0, w1, s0, s1 = 0.28, 0.72, 1.15, 1.85
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
    w0, w1, s0, s1 = 0.2, 0.8, 1.0, 1.9
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
    w0, w1, s0, s1, apex = 0.34, 0.66, 1.0, 1.75, 2.2
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
    d1 = DOOR
    panel(bm, 0.08, 0.92, d1 + 0.1, HI)
    box(bm, 0, 1, -T / 2 - 0.015, T / 2 + 0.015, HI, STOREY, TIMBER)
    for x in (0.0, 0.92):                                         # the jambs, at the tile's edges
        box(bm, x, x + 0.08, -T / 2 - 0.02, T / 2 + 0.02, 0, d1 + 0.1, TIMBER)
    box(bm, 0.0, 1.0, -T / 2 - 0.03, T / 2 + 0.03, d1, d1 + 0.12, TIMBER)
    box(bm, 0.08, 0.92, -T / 2 - 0.02, T / 2 + 0.02, 0, 0.04, STONE)
    return bm


def piece_wall_barn_door():
    """A byre's opening: almost the whole tile, two plank leaves, a heavy lintel."""
    bm = bmesh.new()
    d1 = DOOR + 0.1
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


def piece_jetty_beams():
    """Under a jettied upper storey: joist ends sticking out of the wall below."""
    bm = bmesh.new()
    for x in (0.17, 0.5, 0.83):
        box(bm, x - 0.06, x + 0.06, -T / 2 - JETTY, T / 2, -0.14, 0, TIMBER)
    box(bm, 0, 1, -T / 2 - JETTY - 0.01, -T / 2 - JETTY + 0.1, -0.02, 0.05, TIMBER)
    return bm


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
            lift = STOREY + 0.25 + RISE * max(0, k - 1)              # in the piece's own rise on a wide gable
            out = lancet_outline(0.86, 0.98, lift, lift + 0.4, lift + 0.57)
            prism_xz(bm, out, -T / 2 - 0.005, -T / 2 + 0.01, DARK)
        return bm
    return make


def piece_chimney_breast():
    """The hearth's back, outside an end wall tile: a stone breast a storey high whose top steps
    in to the stack (in the wall tile's frame: x along the wall, -Y outside)."""
    bm = bmesh.new()
    back = T / 2 - 0.04                                          # inside the wall, never on its inner face
    box(bm, 0.1, 0.9, -T / 2 - 0.42, back, 0, STOREY - 0.5, STONE)
    A.hull(bm, [(x, y, z) for x, z in ((0.1, STOREY - 0.5), (0.9, STOREY - 0.5), (0.72, STOREY - 0.1), (0.28, STOREY - 0.1))
                for y in (-T / 2 - 0.42, back)], STONE)
    return bm


ASSETS = {
    "Post": piece(piece_post),
    "Wall": piece(piece_wall),
    "WallBraced": piece(piece_wall_braced),
    "WallShuttered": piece(piece_wall_shuttered),
    "WallLeaded": piece(piece_wall_leaded),
    "WallLancet": piece(piece_wall_lancet),
    "WallDoorway": piece(piece_wall_doorway),
    "JettyBeams": piece(piece_jetty_beams),
    "Gable0": piece(piece_gable(0, False)),
    "Gable0Slit": piece(piece_gable(0, True)),
    "Gable1": piece(piece_gable(1, False)),
    "Gable1Slit": piece(piece_gable(1, True)),
    "Gable2": piece(piece_gable(2, False)),
    "Gable2Slit": piece(piece_gable(2, True)),
    "Gable3": piece(piece_gable(3, False)),
    "Gable3Slit": piece(piece_gable(3, True)),
    "ChimneyBreast": piece(piece_chimney_breast),
    "WallBarnDoor": piece(piece_wall_barn_door),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
