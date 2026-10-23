"""Cruor's cathedral: the massive, intimidating centre of the city (docs/regions/cruor-architecture.md).
The owner passed this design on 2026-10-23 (it was church "B" in the lab, buildings/lab/monumental.py,
where the earlier passes and the comparison with them are kept). Built from the kit's parts
(kit.py) on a Layered builder, so it can be walked into.

A nave of six bays with aisles, flying buttresses from pinnacled piers to a battlemented
clerestory, a round apse with blade buttresses, and a west front of two chamfered towers with
octagonal belfries and spires either side of the nave's own gable, the great red window with its
rose, and a deep portal. Inside: an open floor of clustered piers, a red runner, and a dais over
the east end left empty for the altar.

    runpy.run_path(".../buildings/cruor/cathedral.py", run_name="__main__")

builds it three times in a row: outside, inside (every wall standing) and inside with the south
and west walls cut (the camera beyond them)."""
import math
import os
import sys

from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", ".."))
import rth_art as A  # noqa: E402
import kit as K  # noqa: E402
from kit import STONE, ROOF, DARK, FLOOR, GLASS, DOOR, RUNNER, WT, frame, head  # noqa: E402

# The plan, in tiles: a wide open floor (nave and aisles together about 40 across).
NAVE_W = 18.0             # clear width between the pier rows
AISLE_W = 10.0            # each aisle
BAY = 8.0
BAYS = 6
PIERS = range(1, BAYS - 1)  # no pier against the towers, none in the last bays (the altar's room)
FLOOR_Z = 0.6             # the floor stands on a plinth, three steps up
TOWER = 12.0              # the west towers' side
CHAMFER = 3.0             # their outer corners cut off
CW = 0.5                  # the clerestory wall, standing on the arcade
# Heights: deliberately past the usual limits (the cathedral dominates the city).
AISLE_H = 10.0            # the outer walls: the one storey seen from inside
ARCADE_H = 8.0
NAVE_H = 22.0
AISLE_RISE = 4.0          # the aisle roofs' climb to the clerestory
PITCH = 55.0
TOWER_H = 42.0
BELFRY_H = 11.0
SPIRE_H = 26.0
PORTAL_W, PORTAL_H = 7.0, 9.0
GREAT_W = 5.0             # the great window's half-width (it is 10 wide, filling the gable)


def shifted(f, s):
    o, a, n = f
    return o + a * s, a, n


def window(b, f, w, base, spring):
    """A lancet given by its springing (the kit's lancet takes the apex)."""
    K.lancet(b, f, w, base, spring + head(w))


def cathedral(ox, oy, prefix="Cathedral"):
    """West front at x = ox - TOWER, the nave running east (+x) from ox, centred on y = oy.
    Returns the layers' objects by name (Base, Upper, Roofs, Top_N.., Crown_N..)."""
    c = K.Layered(inside_h=AISLE_H)
    AH, ARH, NH, AR = AISLE_H, ARCADE_H, NAVE_H, AISLE_RISE
    L = BAY * BAYS
    hn = NAVE_W / 2
    ha = hn + AISLE_W
    rise = hn * math.tan(math.radians(PITCH))
    x0, x1 = ox, ox + L
    fx0 = ox - TOWER
    zf = FLOOR_Z

    # Floor, the bands at the piers, the runner and the dais.
    c.base.box(fx0, oy - ha - WT, 0, x1 + WT, oy + ha + WT, zf, FLOOR)
    c.base.hull([(x1 + (hn + CW) * math.cos(a), oy + (hn + CW) * math.sin(a), z)
                 for a in (math.pi * (-0.5 + k / 18) for k in range(19)) for z in (0, zf)], FLOOR)
    dais_x = x0 + BAY * (BAYS - 2) + 2.0
    for i in PIERS:
        c.base.box(x0 + BAY * i - 0.3, oy - ha, zf, x0 + BAY * i + 0.3, oy + ha, zf + 0.02, STONE)
    for k in range(3):
        c.base.box(dais_x - 0.9 + 0.3 * k, oy - hn, zf, x1, oy + hn, zf + 0.2 * (k + 1), STONE)
    c.base.hull([(x1 + hn * math.cos(a), oy + hn * math.sin(a), z)
                 for a in (math.pi * (-0.5 + k / 18) for k in range(19)) for z in (zf, zf + 0.6)], STONE)
    c.base.box(fx0 - 3.0, oy - 1.6, zf, dais_x - 0.9, oy + 1.6, zf + 0.03, RUNNER)

    wb = zf + 1.6                                                 # aisle windows: a tile wide
    wsp = wb + (AH - wb) * 0.45
    cb, csp = AH + AR + 0.4, NH - 1.4                             # clerestory windows
    for side, key in ((-1, "S"), (1, "N")):
        s = c.on(key)
        y_pier, y_wall = oy + side * hn, oy + side * (ha + WT / 2)
        y_out, y_in = y_wall + side * WT / 2, y_wall - side * WT / 2
        # The aisle wall: a skin, merlons, a bench inside, a lancet in each bay on both faces.
        s.box(x0, y_wall - WT / 2, 0, x1, y_wall + WT / 2, AH, STONE)
        K.battlement(s, (x0, y_wall), (x1, y_wall), WT, AH)
        c.crown_run(key, (x0, y_wall), (x1, y_wall), WT)
        s.box(x0, y_in, 0, x1, y_in - side * 0.6, zf + 0.5, STONE)
        for i in range(BAYS):
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_out, side), (y_in, -side)):
                window(s, frame((xm, face), (1, 0), (0, out)), 0.5, wb, wsp)
        # The arcade: clustered piers (always shown), pointed arches between them (outside only).
        supports = [x0] + [x0 + BAY * i for i in PIERS] + [x1]
        for xp in supports[1:-1]:
            c.upper.box(xp - 0.7, y_pier - CW / 2, ARH, xp + 0.7, y_pier + CW / 2, AH + 1, STONE)
            c.base.box(xp - 1.0, y_pier - 1.0, 0, xp + 1.0, y_pier + 1.0, zf + 0.5, STONE)
            c.base.box(xp - 0.6, y_pier - 0.6, zf + 0.5, xp + 0.6, y_pier + 0.6, ARH - 0.8, STONE)
            for dx, dy in ((0.6, 0), (-0.6, 0), (0, 0.6), (0, -0.6)):
                c.base.cyl(xp + dx, y_pier + dy, 0.38, zf + 0.5, ARH - 0.8, STONE, sides=8)
            c.base.box(xp - 1.0, y_pier - 1.0, ARH - 0.8, xp + 1.0, y_pier + 1.0, ARH, STONE)
        for k, (sa, sb) in enumerate(zip(supports, supports[1:])):
            xa = sa + (0.7 if k > 0 else 0.0)
            xb = sb - (0.7 if k < len(supports) - 2 else 0.0)
            xm = (xa + xb) / 2
            for xs, xe in ((xa, xm), (xb, xm)):
                c.upper.hull([(x, y_pier + dy, z) for x, z in ((xs, ARH), (xs, AH + 1), (xe, AH + 1))
                              for dy in (-CW / 2, CW / 2)], STONE)
        # The clerestory: a skin on the arcade, small lancets, merlons.
        c.upper.box(x0, y_pier - CW / 2, AH + 1, x1, y_pier + CW / 2, NH, STONE)
        K.battlement(c.upper, (x0, y_pier), (x1, y_pier), CW, NH)
        for i in range(BAYS):
            window(s, frame((x0 + BAY * (i + 0.5), y_pier + side * CW / 2), (1, 0), (0, side)), 0.5, cb, csp)
        c.roofs.hull([(x, y_in, z) for x in (x0, x1) for z in (AH, AH + 0.3)] +
                     [(x, y_pier + side * CW / 2, z) for x in (x0, x1) for z in (AH + AR, AH + AR + 0.3)], ROOF)
        # Buttresses with the one pinnacle, flying across to the clerestory.
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            far = y_out + side * 1.6
            zb = AH + 0.25 * (NH - AH)                            # above the wall, so the arm can climb
            za = NH - 0.35 * (NH - AH - AR)                       # where the arm meets the clerestory
            s.box(xp - 0.6, y_out, 0, xp + 0.6, far, zb, STONE)
            K.pinnacle(s, xp, y_out + side * 0.8, zb, half=0.6)
            c.upper.hull([(xp + dx, y_out + side * 1.0, z) for dx in (-0.35, 0.35) for z in (zb - 0.9, zb - 0.3)] +
                         [(xp + dx, y_pier + side * CW / 2, z) for dx in (-0.35, 0.35) for z in (za, za + 0.6)], STONE)
    c.roofs.gable_roof(x0, x1, oy - hn, oy + hn, NH, rise, ROOF, over=0.0)

    # East: the aisles close; the round apse with lancets, merlons and blade buttresses.
    e = c.on("E")
    for side in (-1, 1):
        y_end = oy + side * (ha + WT)
        e.box(x1, oy + side * hn, 0, x1 + WT, y_end, AH, STONE)
        K.battlement(e, (x1 + WT / 2, oy + side * hn), (x1 + WT / 2, y_end), WT, AH)
        c.crown_run("E", (x1 + WT / 2, oy + side * hn), (x1 + WT / 2, y_end), WT)
        e.hull([(x, oy + side * y, z) for x in (x1, x1 + WT) for y, z in ((ha + WT, AH), (hn, AH), (hn, AH + AR + 0.3))], STONE)
    segs, ri, ro = 9, hn, hn + CW
    for k in range(segs):
        a0, a1 = math.pi * (-0.5 + k / segs), math.pi * (-0.5 + (k + 1) / segs)
        e.hull([(x1 + rr * math.cos(a), oy + rr * math.sin(a), z) for a in (a0, a1) for rr in (ri, ro) for z in (0, NH)], STONE)
        am = (a0 + a1) / 2
        nrm = Vector((math.cos(am), math.sin(am), 0))
        along = Vector((-math.sin(am), math.cos(am), 0))
        for dist, out in ((ro * math.cos((a1 - a0) / 2), nrm), (ri * math.cos((a1 - a0) / 2), -nrm)):
            p = Vector((x1, oy, 0)) + nrm * dist
            window(e, frame((p.x, p.y), (along.x, along.y), (out.x, out.y)), 0.5, wb, wsp)
        mid = lambda a, r_: (x1 + r_ * math.cos(a), oy + r_ * math.sin(a))  # noqa: E731
        K.battlement(e, mid(a0, (ri + ro) / 2), mid(a1, (ri + ro) / 2), CW, NH)
        c.crown_run("E", mid(a0, (ri + ro) / 2), mid(a1, (ri + ro) / 2), CW)
        if k % 2 == 0:
            pb = Vector((x1 + ro * math.cos(a0), oy + ro * math.sin(a0), 0))
            n = Vector((math.cos(a0), math.sin(a0), 0))
            t = Vector((-math.sin(a0), math.cos(a0), 0))
            zb = 0.68 * NH
            e.hull([pb + t * dt + n * dn + Vector((0, 0, z)) for dt in (-0.6, 0.6) for dn in (-0.3, 2.0) for z in (0, zb)], STONE)
            blade = [(-0.3, zb, 0.6), (2.0, zb, 0.6), (2.4, zb + 0.15 * NH, 0.25), (-0.3, NH + 0.4, 0.25)]
            e.hull([pb + n * dn + t * (sg * w) + Vector((0, 0, z)) for dn, z, w in blade for sg in (-1, 1)], STONE)
    c.roofs.cone(x1, oy, ro + 0.2, NH, NH + rise, ROOF, sides=18)
    c.roofs.cyl(x1, oy, 1.4, NH + rise - 3, NH + rise + 0.2 * SPIRE_H, STONE, sides=8)
    c.roofs.cone(x1, oy, 1.7, NH + rise + 0.2 * SPIRE_H, NH + rise + 0.75 * SPIRE_H, ROOF, sides=8)

    # West: two towers, square on the nave side and chamfered outside, merlons, a pinnacle at each
    # corner, then an octagonal belfry and a spire.
    w_ = c.on("W")
    for side in (-1, 1):
        yi, yo = oy + side * hn, oy + side * (hn + TOWER)
        yc, xc = (yi + yo) / 2, (fx0 + ox) / 2
        plan = [(fx0, yi), (ox, yi), (ox, yo - side * CHAMFER), (ox - CHAMFER, yo), (fx0 + CHAMFER, yo), (fx0, yo - side * CHAMFER)]
        w_.hull([(x, y, z) for x, y in plan for z in (0, TOWER_H)], STONE)
        for p, q in zip(plan, plan[1:] + plan[:1]):
            if p[1] == yi and q[1] == yi:
                continue                                          # the nave side: under the roof
            mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
            inward = Vector((xc - mx, yc - my, 0)).normalized() * 0.25
            K.battlement(w_, (p[0] + inward.x, p[1] + inward.y), (q[0] + inward.x, q[1] + inward.y), 0.5, TOWER_H)
        corners = [(fx0, yi), (ox, yi), (ox - CHAMFER / 2, yo - side * CHAMFER / 2), (fx0 + CHAMFER / 2, yo - side * CHAMFER / 2)]
        for px, py in corners:
            q = Vector((px, py, 0)) + (Vector((xc, yc, 0)) - Vector((px, py, 0))).normalized() * 0.85
            w_.box(q.x - 0.6, q.y - 0.6, TOWER_H, q.x + 0.6, q.y + 0.6, TOWER_H + 0.9, STONE)
            K.pinnacle(w_, q.x, q.y, TOWER_H + 0.9, half=0.6)
        front = frame((fx0, oy + side * (hn + (TOWER - CHAMFER) / 2)), (0, 1), (-1, 0))
        outer = frame((xc, yo), (1, 0), (0, side))
        K.arch_frame(w_, front, 1.4, 1.9, 4.0, 0.0, 0.3, STONE)
        K.arch_panel(w_, front, 1.4, 0.0, 4.0, DOOR)
        for tier in (0.36, 0.66):                                 # dark slits, two tiers
            for f in (front, outer):
                for sh in (-1.4, 1.4):
                    K.arch_panel(w_, shifted(f, sh), 0.4, TOWER_H * tier, TOWER_H * tier + 2.6, DARK)
        ring = lambda r, z: [Vector((xc + r * math.cos(2 * math.pi * (k + 0.5) / 8), yc + r * math.sin(2 * math.pi * (k + 0.5) / 8), z))  # noqa: E731
                             for k in range(8)]
        br = 0.4 * TOWER
        top = TOWER_H + BELFRY_H
        w_.hull(ring(br, TOWER_H) + ring(br, top), STONE)
        rf = br * math.cos(math.pi / 8)
        for k in range(8):
            phi = 2 * math.pi * k / 8
            f = frame((xc + rf * math.cos(phi), yc + rf * math.sin(phi)), (-math.sin(phi), math.cos(phi)), (math.cos(phi), math.sin(phi)))
            K.arch_panel(w_, f, 0.5, top - 0.55 * BELFRY_H, top - 2.2, DARK)
        w_.hull(ring(br + 0.3, top) + ring(br + 0.3, top + 0.5), STONE)
        w_.hull(ring(br + 0.1, top + 0.5) + [Vector((xc, yc, top + SPIRE_H))], ROOF)
        c.crown_run("W", (fx0, oy + side * (hn + 0.25)), (ox, oy + side * (hn + 0.25)), 0.5)

    # The nave's west gable (the roof's own end), the great window with its rose, and the portal.
    face = frame((fx0, oy), (0, 1), (-1, 0))
    pw = PORTAL_W / 2
    PH = pw + 2.1
    T = K.arch(pw + 1.5, PORTAL_H)[-1][1] + 0.8
    w_.box(fx0, oy - hn, 0, fx0 + WT * 2, oy - PH, NH, STONE)
    w_.box(fx0, oy + PH, 0, fx0 + WT * 2, oy + hn, NH, STONE)
    w_.box(fx0, oy - PH, T, fx0 + WT * 2, oy + PH, NH, STONE)
    w_.hull([(x, y, z) for x in (fx0, fx0 + WT * 2) for y, z in ((oy - hn, NH), (oy + hn, NH), (oy, NH + rise))], STONE)
    K.pinnacle(w_, fx0 + 0.25, oy, NH + rise - 0.8, half=0.5)
    for sy in (-1, 1):
        c.crown_run("W", (fx0 + WT, oy + sy * hn), (fx0 + WT, oy + sy * PH), WT * 2)
    c.roofs.gable_roof(fx0 + WT * 2, ox, oy - hn, oy + hn, NH, rise, ROOF, over=0.0)
    ww = GREAT_W
    wb2 = T + 3.8                                                 # just over the portal's gable
    ws2 = max(NH + 2.0, wb2 + 2.2)
    K.arch_frame(w_, face, ww, ww + 0.4, ws2, 0.0, 0.3, STONE, base=wb2)
    K.arch_panel(w_, face, ww, wb2, ws2, GLASS)
    for sh in (-ww / 2, 0.0, ww / 2):                             # mullions
        w_.box(fx0 - 0.15, oy + sh - 0.13, wb2, fx0, oy + sh + 0.13, ws2 + 0.4, STONE)
    for sh in (-ww / 2, ww / 2):                                  # two sub-arches
        K.arch_frame(w_, shifted(face, sh), ww / 2 - 0.25, ww / 2, ws2 + 0.4, 0.0, 0.15, STONE, base=ws2 + 0.4)
    rz, rr = ws2 + 1.07 * ww, 0.36 * ww                           # the rose: a ring and six spokes
    for k in range(12):
        a0, a1 = 2 * math.pi * k / 12, 2 * math.pi * (k + 1) / 12
        w_.hull([(fx0 - d, oy + r * math.cos(a), rz + r * math.sin(a)) for a in (a0, a1) for r in (rr - 0.3, rr)
                 for d in (0.0, 0.2)], STONE)
    for k in range(6):
        a = math.pi * k / 6
        w_.hull([(fx0 - d, oy + r * math.cos(a) + q * math.sin(a), rz + r * math.sin(a) - q * math.cos(a))
                 for r in (-rr, rr) for q in (-0.1, 0.1) for d in (0.0, 0.15)], STONE)
    K.arch_spandrel(w_, face, pw + 1.5, PORTAL_H, T, PH, -WT * 2, 2.0, STONE)
    for k in range(3):                                            # three orders stepping out
        K.arch_frame(w_, face, pw + 0.5 * k, pw + 0.5 * (k + 1), PORTAL_H, -WT * 2, 0.7 + 0.6 * k, STONE)
    w_.hull([(fx0 - d, oy + y, z) for d in (-WT * 2, 2.0) for y, z in ((-PH, T), (PH, T), (0, T + 3.5))], STONE)
    for sy in (-1, 1):
        y = oy + sy * (PH + 0.5)
        w_.box(fx0 - 1.9, y - 0.5, 0, fx0 - 0.9, y + 0.5, T + 0.5, STONE)
        K.pinnacle(w_, fx0 - 1.4, y, T + 0.5, half=0.5)
    c.base.box(fx0 - 2.0, oy - PH, 0, fx0, oy + PH, zf, FLOOR)
    c.base.box(fx0 - 5.0, oy - PH - 2, 0, fx0 - 2.0, oy + PH + 2, zf / 3, STONE)
    c.base.box(fx0 - 3.5, oy - PH - 2, 0, fx0 - 2.0, oy + PH + 2, zf * 2 / 3, STONE)
    return c.finish(prefix)


def keep(layers, state="outside", cut=()):
    """The layers a view shows; the others are removed. Returns the kept objects."""
    import bpy
    kept = []
    for part, o in layers.items():
        if K.shows(part, state, cut):
            kept.append(o)
        else:
            bpy.data.objects.remove(o)
    return kept


STATES = [("Outside", "outside", ()), ("Inside", "inside", ()), ("InsideCut", "inside", ("S", "W"))]
SPACING = 95.0


def build():
    A.reset()
    objs = []
    for i, (name, state, cut) in enumerate(STATES):
        objs += keep(cathedral(SPACING * i, 0.0, prefix=f"Cathedral{name}"), state, cut)
    K.paint(objs)
    return {"triangles": A.report(objs)}


if __name__ == "__main__":
    result = build()
    cam = A.preview_rig(focus=(SPACING + 21, 0, 10), distance=330, pitch=34, yaw=-10, ground=170, human_at=(-20, 0))
    cam.data.lens = 32
    cam.data.clip_end = 3000
    import bpy
    bpy.data.objects["PreviewGround"].location.x = SPACING
    A.figures([(SPACING * i + x, y, z) for i in range(len(STATES))
               for x, y, z in ((-16, 2, 0.4), (20, 2, FLOOR_Z), (28, 15, FLOOR_Z), (12, -15, FLOOR_Z), (52, 0, FLOOR_Z + 0.6))])
