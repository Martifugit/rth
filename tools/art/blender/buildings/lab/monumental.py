"""Monumental kits: Cruor's great church (third pass) and a castle (first pass), in flat colours.

  - Cruor's great church: a nave of repeating bays (aisles, buttresses flying over the aisle
    roofs, clerestory lancets), a twin-towered west front with spires, a rose window and a deep
    portal, and a round apse at the east end. It is meant to tower over everything: it breaks the
    usual height limits on purpose (docs/world-scale.md), and needs open ground round it.
    You walk into it (like the arboretum in images/rs3-arboretum-*), so it is built in layers:
      * outside, everything shows;
      * inside, the roofs and everything above the outer walls (clerestory, arcade arches, the
        towers' upper tiers, flying buttresses) hide, leaving one tall storey of wall round a
        wide, open floor of piers;
      * on the side toward the camera, the wall above CUT hides too and a crown of points shows
        on the cut instead (the Drakan trick, images/rs3-drakan-*), so a wall never stands
        between the camera and the player.
  - A castle: a curtain wall with a walk and crenels, round corner towers, an open gatehouse
    between two drum towers (no roof over the gateway), and a tall keep with corner turrets.

Both are built from repeating parts (a bay, a wall run, a tower), so they are kits: a longer
nave is more bays, a bigger castle more runs and towers. 1 unit = 1 tile; Z up.

Run in Blender (MCP): runpy.run_path(".../lab/monumental.py", run_name="__main__").
It builds the church three times in a row (outside, inside, inside with the near walls cut)."""
import itertools
import math
import os
import sys

import bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
import rth_art as A  # noqa: E402

# Flat colours for judging shapes (none of these wear a texture).
MATS = ["Gravestone", "Ashgrey", "Sable", "SynodRed", "Shingle", "Iron", "CryptFloor"]
PALE, ROOF, DARK, RED, CASTLE, METAL, FLOOR = range(len(MATS))


class Build:
    """A bmesh with shape helpers; every shape takes a material index."""

    def __init__(self):
        self.bm = bmesh.new()

    def box(self, x0, y0, z0, x1, y1, z1, m):
        A.box(self.bm, (min(x0, x1), min(y0, y1), z0), (max(x0, x1), max(y0, y1), z1), m)

    def hull(self, pts, m):
        A.hull(self.bm, [tuple(p) for p in pts], m)

    def cyl(self, cx, cy, r, z0, z1, m, sides=12, r_top=None):
        A.prism(self.bm, sides, r, r if r_top is None else r_top, z0, z1, (cx, cy), mat_index=m, cap_bottom=True)

    def cone(self, cx, cy, r, z0, z1, m, sides=8):
        A.loft(self.bm, [((cx, cy, z0), r, r), ((cx, cy, z1), 0, 0)], sides=sides, ref=(0, 1, 0), mats=m)

    def gable_roof(self, x0, x1, y0, y1, z, rise, m, along="x", over=0.3):
        """A pitched roof over the rectangle, ridge along `along`, eaves `over` past the walls."""
        if along == "x":
            ym = (y0 + y1) / 2
            pts = [(x, y, z) for x in (x0 - over, x1 + over) for y in (y0 - over, y1 + over)]
            pts += [(x, ym, z + rise) for x in (x0 - over, x1 + over)]
        else:
            xm = (x0 + x1) / 2
            pts = [(x, y, z) for x in (x0 - over, x1 + over) for y in (y0 - over, y1 + over)]
            pts += [(xm, y, z + rise) for y in (y0 - over, y1 + over)]
        self.hull(pts, m)

    def panel(self, origin, along, outward, s0, s1, z0, z1, m, apex=None, depth=0.06):
        """A flat shape on a wall face (a window, a slit, a portal): a rectangle in the face, with
        a pointed top up to `apex` if given. origin is a point on the face; along runs across it."""
        self.hull(panel_points(origin, along, outward, s0, s1, z0, z1, apex, depth), m)

    def merlons(self, p0, p1, z, h, t, m, size=0.5, gap=0.45, outward=None):
        """Crenels along a wall top from p0 to p1 (x, y), on the wall's outer edge."""
        p0, p1 = Vector((*p0, 0)), Vector((*p1, 0))
        d = p1 - p0
        length = d.length
        d.normalize()
        n = Vector((*outward[:2], 0)) if outward else Vector((-d.y, d.x, 0))
        count = max(1, int((length + gap) / (size + gap)))
        step = length / count
        for i in range(count):
            c = p0 + d * (step * (i + 0.5))
            pts = []
            for s in (-size / 2, size / 2):
                for k in (0.0, t):
                    for zz in (z, z + h):
                        q = c + d * s + n * k
                        pts.append((q.x, q.y, zz))
            self.hull(pts, m)

    def ring_merlons(self, cx, cy, r, z, h, m, count=10, t=0.35):
        for i in range(count):
            a0 = 2 * math.pi * (i + 0.2) / count
            a1 = 2 * math.pi * (i + 0.7) / count
            pts = []
            for a in (a0, a1):
                for rr in (r - t, r):
                    for zz in (z, z + h):
                        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a), zz))
            self.hull(pts, m)


def panel_points(origin, along, outward, s0, s1, z0, z1, apex=None, depth=0.06):
    o, a, n = Vector(origin), Vector(along).normalized(), Vector(outward).normalized()
    up = Vector((0, 0, 1))
    outline = [(s0, z0), (s1, z0), (s1, z1), (s0, z1)]
    if apex:
        outline = [(s0, z0), (s1, z0), (s1, z1), ((s0 + s1) / 2, apex), (s0, z1)]
    return [o + a * s + up * z + n * d for s, z in outline for d in (0.0, depth)]


def box_points(x0, y0, z0, x1, y1, z1):
    return [Vector((x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]


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


# ---------------------------------------------------------------- the great church

# Planned from the inside: a wide, open floor seen from above (nave and aisles together about
# 40 across), one tall storey of outer wall round it, everything higher only seen from outside.
NAVE_W = 18.0             # clear width between the arcade piers
AISLE_W = 10.0            # clear width of each aisle
BAY = 8.0                 # one bay: about the aisle's width
BAYS = 6
WALL = 1.0                # wall thickness
AISLE_H = 10.0            # the outer walls: the one storey you see from inside
ARCADE_H = 8.0            # the piers' capitals; the arches above hide inside
NAVE_H = 22.0             # nave walls, outside only
CUT = 2.3                 # a wall cut toward the camera stops at one storey, under its crown
TOWER = 12.0              # west towers' side
TOWER_H = 42.0            # about twice the nave walls
SPIRE_H = 18.0
PORTAL_W, PORTAL_H = 6.0, 9.0
PITCH = 55.0
SIDES = ("N", "S", "E", "W")


class Church:
    """The church's layers, each its own object: `base` always shows; `upper` and `roofs` hide
    inside; `top[side]` (that side's walls from CUT to AISLE_H) swaps for `crown[side]` when the
    camera is on that side."""

    def __init__(self):
        self.base, self.upper, self.roofs = Build(), Build(), Build()
        self.top = {s: Build() for s in SIDES}
        self.crown = {s: Build() for s in SIDES}

    def hull(self, side, pts, m):
        """A solid belonging to a side's walls, split by height into the layers."""
        for b, za, zb in ((self.base, -1e9, CUT), (self.top[side], CUT, AISLE_H), (self.upper, AISLE_H, 1e9)):
            part = clip_z(pts, za, zb)
            if len(part) >= 4 and max(p.z for p in part) - min(p.z for p in part) > 1e-3:
                b.hull(part, m)

    def box(self, side, x0, y0, z0, x1, y1, z1, m):
        self.hull(side, box_points(min(x0, x1), min(y0, y1), z0, max(x0, x1), max(y0, y1), z1), m)

    def panel(self, side, origin, along, outward, s0, s1, z0, z1, m, apex=None, depth=0.06):
        self.hull(side, panel_points(origin, along, outward, s0, s1, z0, z1, apex, depth), m)

    def cone(self, side, cx, cy, r, z0, z1, m, sides=4):
        """A pinnacle: a pyramid of `sides` faces."""
        pts = [Vector((cx + r * math.cos(2 * math.pi * (k + 0.5) / sides), cy + r * math.sin(2 * math.pi * (k + 0.5) / sides), z0))
               for k in range(sides)] + [Vector((cx, cy, z1))]
        self.hull(side, pts, m)

    def crown_run(self, side, p0, p1, outward, thick=WALL, pier_every=None):
        """The crown on a cut wall from p0 to p1 (x, y; the wall's centre line): a coping, then a
        row of pointed fins, taller ones where the bays' piers are."""
        c = self.crown[side]
        p0, p1 = Vector((*p0, 0)), Vector((*p1, 0))
        d = p1 - p0
        length = d.length
        d.normalize()
        n = Vector((*outward[:2], 0)).normalized()
        h = thick / 2 + 0.15
        c.hull([p + n * k + Vector((0, 0, z)) for p in (p0 - d * 0.15, p1 + d * 0.15) for k in (-h, h)
                for z in (CUT, CUT + 0.25)], PALE)
        count = max(1, round(length / 1.0))
        step = length / count
        for i in range(count + 1):
            q = p0 + d * (step * i)
            tall = pier_every and abs(((step * i) / pier_every) - round((step * i) / pier_every)) < 1e-3
            top = CUT + 0.25 + (1.4 if tall else (0.8 if i % 2 else 0.45))
            w = 0.35 if tall else 0.22
            pts = [q + d * a + n * k + Vector((0, 0, CUT + 0.25)) for a in (-w, w) for k in (-w, w)]
            pts += [q + d * a + n * k + Vector((0, 0, top - (0.6 if tall else 0.3))) for a in (-w, w) for k in (-w, w)]
            pts += [q + Vector((0, 0, top))]
            c.hull(pts, PALE)

    def finish(self, prefix="GreatChurch"):
        objs = {}
        parts = [("Base", self.base), ("Upper", self.upper), ("Roofs", self.roofs)]
        parts += [(f"Top_{s}", self.top[s]) for s in SIDES] + [(f"Crown_{s}", self.crown[s]) for s in SIDES]
        for name, b in parts:
            A.finish(b.bm)
            objs[name] = A.mesh_from(f"{prefix}_{name}", b.bm, MATS, coll="Monumental")
        return objs


def great_church(ox, oy, prefix="GreatChurch"):
    """West front at x = ox, the nave running east (+x), centred on y = oy. Returns the layers'
    objects by name (Base, Upper, Roofs, Top_N.., Crown_N..)."""
    c = Church()
    L = BAY * BAYS
    hn = NAVE_W / 2                                               # pier line
    ha = hn + AISLE_W                                             # aisle wall's inner face
    rise = (hn + WALL) * math.tan(math.radians(PITCH))
    x0, x1 = ox, ox + L
    fx0 = ox - TOWER                                              # the west front's face
    c.base.box(fx0, oy - ha - WALL, 0, x1 + WALL, oy + ha + WALL, 0.05, FLOOR)
    c.base.hull([(x1 + (hn + WALL) * math.cos(a), oy + (hn + WALL) * math.sin(a), z)
                 for a in (math.pi * (-0.5 + k / 18) for k in range(19)) for z in (0, 0.05)], FLOOR)

    for side, key in ((-1, "S"), (1, "N")):
        y_pier, y_wall = oy + side * hn, oy + side * (ha + WALL / 2)
        y_out, y_in = y_wall + side * WALL / 2, y_wall - side * WALL / 2
        # The outer wall, its lancets on both faces, a shaft inside at every pier.
        c.box(key, x0, y_wall - WALL / 2, 0, x1, y_wall + WALL / 2, AISLE_H, PALE)
        for i in range(BAYS):
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_out, side), (y_in, -side)):
                c.panel(key, (xm, face, 0), (1, 0, 0), (0, out, 0), -1.2, 1.2, 1.8, 6.6, RED, apex=8.4, depth=0.04)
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            c.box(key, xp - 0.4, y_in, 0, xp + 0.4, y_in - side * 0.4, AISLE_H, PALE)
        c.crown_run(key, (x0, y_wall), (x1, y_wall), (0, side), pier_every=BAY)
        # The arcade: piers with capitals (always), pointed arches between them (outside only).
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            c.base.box(xp - 0.7, y_pier - 0.7, 0, xp + 0.7, y_pier + 0.7, ARCADE_H - 0.5, PALE)
            c.base.box(xp - 0.9, y_pier - 0.9, ARCADE_H - 0.5, xp + 0.9, y_pier + 0.9, ARCADE_H, PALE)
        for i in range(BAYS):
            xa, xb = x0 + BAY * i + 0.7, x0 + BAY * (i + 1) - 0.7
            xm = (xa + xb) / 2
            for xs, xe in ((xa, xm), (xb, xm)):                    # spandrels make the pointed arch
                c.upper.hull([(x, y_pier + dy, z) for x, z in ((xs, ARCADE_H), (xs, AISLE_H + 1), (xe, AISLE_H + 1))
                              for dy in (-WALL / 2, WALL / 2)], PALE)
        c.upper.box(x0, y_pier - WALL / 2, AISLE_H + 1, x1, y_pier + WALL / 2, NAVE_H, PALE)
        for i in range(BAYS):                                     # clerestory lancets, both faces
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_pier + side * WALL / 2, side), (y_pier - side * WALL / 2, -side)):
                c.upper.panel((xm, face, 0), (1, 0, 0), (0, out, 0), -1.4, 1.4, AISLE_H + 4.5,
                              NAVE_H - 3.0, RED, apex=NAVE_H - 1.0, depth=0.04)
        # Aisle roof: a lean-to from the aisle wall up to the clerestory.
        c.roofs.hull([(x, y_out + side * 0.4, z) for x in (x0, x1) for z in (AISLE_H, AISLE_H + 0.3)] +
                     [(x, y_pier + side * WALL / 2, z) for x in (x0, x1) for z in (AISLE_H + 4.0, AISLE_H + 4.3)], ROOF)
        # Buttresses: piers on the outer wall with tall pinnacles, flying across to the clerestory.
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            far = y_out + side * 2.4
            c.box(key, xp - 0.7, y_out, 0, xp + 0.7, far, AISLE_H + 3.5, PALE)
            c.hull(key, [(xp + dx, y, AISLE_H + 3.5) for dx in (-0.7, 0.7) for y in (y_out, far)] +
                   [(xp + dx, y_out + side * 0.4, AISLE_H + 4.6) for dx in (-0.7, 0.7)], PALE)
            pc = (y_out + far) / 2 + side * 0.4
            c.cone(key, xp, pc, 0.75, AISLE_H + 4.0, AISLE_H + 10.0, PALE)
            c.crown[key].cone(xp, (y_out + far) / 2, 0.9, CUT, CUT + 1.2, PALE, sides=4)
            c.upper.hull([(xp + dx, far - side * 0.6, z) for dx in (-0.4, 0.4) for z in (AISLE_H + 2.6, AISLE_H + 3.4)] +
                         [(xp + dx, y_pier + side * WALL / 2, z) for dx in (-0.4, 0.4) for z in (NAVE_H - 3.5, NAVE_H - 2.7)], PALE)
            c.upper.cone(xp, y_pier + side * (WALL / 2 + 0.3), 0.45, NAVE_H, NAVE_H + 4.0, PALE, sides=4)
    # Nave roof.
    c.roofs.gable_roof(x0, x1, oy - hn - WALL / 2, oy + hn + WALL / 2, NAVE_H, rise, ROOF, over=0.4)

    # East: the aisles close; the nave ends in a round apse (outer wall to AISLE_H, then upper).
    for side in (-1, 1):
        c.box("E", x1, oy + side * hn, 0, x1 + WALL, oy + side * (ha + WALL), AISLE_H, PALE)
        c.crown_run("E", (x1 + WALL / 2, oy + side * hn), (x1 + WALL / 2, oy + side * (ha + WALL)), (1, 0))
    segs = 9
    ro = hn + WALL
    for k in range(segs):
        a0, a1 = math.pi * (-0.5 + k / segs), math.pi * (-0.5 + (k + 1) / segs)
        pts = [(x1 + rr * math.cos(a), oy + rr * math.sin(a), z) for a in (a0, a1) for rr in (hn, ro) for z in (0, NAVE_H)]
        c.hull("E", pts, PALE)
        am = (a0 + a1) / 2
        nrm = Vector((math.cos(am), math.sin(am), 0))
        along = Vector((-math.sin(am), math.cos(am), 0))
        rc = ro * math.cos((a1 - a0) / 2)                          # the segment's flat face
        for face, out in ((rc, nrm), (hn * math.cos((a1 - a0) / 2), -nrm)):
            p = Vector((x1, oy, 0)) + nrm * face
            c.panel("E", p, along, out, -0.9, 0.9, 1.8, NAVE_H - 3.0, RED, apex=NAVE_H - 1.4, depth=0.04)
        mid = lambda a, r_: (x1 + r_ * math.cos(a), oy + r_ * math.sin(a))
        c.crown_run("E", mid(a0, hn + WALL / 2), mid(a1, hn + WALL / 2), tuple(nrm))
        if k % 2 == 0:                                            # buttresses round the apse
            pb = Vector((x1 + ro * math.cos(a0), oy + ro * math.sin(a0), 0))
            n = Vector((math.cos(a0), math.sin(a0), 0))
            t = Vector((-math.sin(a0), math.cos(a0), 0))
            c.hull("E", [pb + t * dt + n * dn + Vector((0, 0, z)) for dt in (-0.7, 0.7) for dn in (0, 2.2)
                         for z in (0, NAVE_H - 4)], PALE)
            q = pb + n * 1.2
            c.cone("E", q.x, q.y, 0.75, NAVE_H - 4, NAVE_H + 1.5, PALE)
            c.crown["E"].cone(q.x, q.y, 0.9, CUT, CUT + 1.2, PALE, sides=4)
    c.roofs.cone(x1, oy, ro + 0.4, NAVE_H, NAVE_H + rise, ROOF, sides=18)
    # A slender spire over the crossing (two bays in from the apse).
    fx = x0 + BAY * (BAYS - 2)
    c.roofs.cyl(fx, oy, 1.6, NAVE_H + rise - 2, NAVE_H + rise + 5, PALE, sides=8)
    c.roofs.cone(fx, oy, 1.9, NAVE_H + rise + 5, NAVE_H + rise + 19, ROOF, sides=8)

    # The west front: two towers flanking a porch hall, a gable wall with the rose and the portal.
    for side in (-1, 1):
        ya, yb = sorted((oy + side * hn, oy + side * (hn + TOWER)))
        c.box("W", fx0, ya, 0, ox, yb, TOWER_H, PALE)
        for zz in (NAVE_H * 0.5, NAVE_H, TOWER_H - 1.0):          # ledges marking the tiers
            c.box("W", fx0 - 0.3, ya - 0.3, zz - 0.4, ox + 0.3, yb + 0.3, zz, PALE)
        for cx_ in (fx0, ox):                                     # stepped corner buttresses
            for cy_ in (ya, yb):
                for h, w in ((NAVE_H * 0.5, 1.6), (NAVE_H, 1.1), (TOWER_H - 4, 0.7)):
                    c.box("W", cx_ - w, cy_ - w, 0, cx_ + w, cy_ + w, h, PALE)
                c.cone("W", cx_, cy_, 1.1, TOWER_H - 4, TOWER_H + 5, PALE)
        for zz, h in ((NAVE_H + 1.5, 7.0), (NAVE_H * 0.5 + 1.5, 5.0)):   # tiers of openings on the front
            for s0 in (2.2, TOWER - 4.2):
                c.panel("W", (fx0, ya, 0), (0, 1, 0), (-1, 0, 0), s0, s0 + 2.0, zz, zz + h, DARK, apex=zz + h + 1.4)
        c.panel("W", (fx0, ya, 0), (0, 1, 0), (-1, 0, 0), TOWER / 2 - 1.2, TOWER / 2 + 1.2, 1.8, 6.6, RED, apex=8.4)
        c.upper.cone((fx0 + ox) / 2, (ya + yb) / 2, TOWER / 2 - 0.6, TOWER_H, TOWER_H + SPIRE_H, ROOF, sides=8)
        # The tower cut: a crown round its top, the inner face too (it is the porch's wall).
        for p0, p1, out in (((fx0, ya), (fx0, yb), (-1, 0)), ((ox, ya), (ox, yb), (1, 0)),
                            ((fx0, ya), (ox, ya), (0, -1)), ((fx0, yb), (ox, yb), (0, 1))):
            q0, q1 = Vector((*p0, 0)), Vector((*p1, 0))
            inward = -Vector((*out, 0)) * 0.5
            c.crown_run("W", tuple(q0 + inward)[:2], tuple(q1 + inward)[:2], out)
    # Porch hall between the towers: its side walls are the towers; the front wall has the portal.
    pw = PORTAL_W / 2
    c.box("W", fx0, oy - hn, 0, fx0 + WALL, oy - pw, NAVE_H, PALE)
    c.box("W", fx0, oy + pw, 0, fx0 + WALL, oy + hn, NAVE_H, PALE)
    c.box("W", fx0, oy - pw, PORTAL_H, fx0 + WALL, oy + pw, NAVE_H, PALE)
    for sy in (-1, 1):
        c.crown_run("W", (fx0 + WALL / 2, oy + sy * hn), (fx0 + WALL / 2, oy + sy * pw), (-1, 0))
    c.upper.hull([(x, y, z) for x in (fx0, fx0 + WALL) for y, z in ((oy - hn, NAVE_H), (oy + hn, NAVE_H), (oy, NAVE_H + rise))], PALE)
    c.roofs.gable_roof(fx0 + WALL, ox, oy - hn, oy + hn, NAVE_H, rise, ROOF, over=0.0)
    rose = Vector((fx0, oy, NAVE_H + 2.0))
    ex = Vector((1, 0, 0))
    A.loft(c.upper.bm, [(tuple(rose - ex * 0.4), 4.8, 4.8), (tuple(rose + ex * 0.1), 4.8, 4.8)], sides=20, ref=(0, 0, 1), mats=PALE)
    A.loft(c.upper.bm, [(tuple(rose - ex * 0.46), 0, 0), (tuple(rose - ex * 0.45), 4.1, 4.1), (tuple(rose), 4.1, 4.1)],
           sides=20, ref=(0, 0, 1), mats=RED)
    # The portal, open: three orders stepping out and back from the opening, each a pair of
    # jambs and a pointed arch, under a steep gable between two pinnacles.
    step = 0.7
    for k in range(3):
        wi, wo, d = pw + step * k, pw + step * (k + 1), 0.5 * (k + 1)
        for sy in (-1, 1):
            c.box("W", fx0 - d, oy + sy * wi, 0, fx0, oy + sy * wo, PORTAL_H, PALE)
            c.hull("W", [(x, y, z) for x in (fx0 - d, fx0) for y, z in ((oy + sy * wo, PORTAL_H), (oy + sy * wi, PORTAL_H),
                                                                      (oy, PORTAL_H + wi * 1.15), (oy, PORTAL_H + wo * 1.15))], PALE)
    gw = pw + step * 3 + 0.4
    c.hull("W", [(x, y, z) for x in (fx0 - 1.5, fx0) for y, z in ((oy - gw, PORTAL_H + 0.6), (oy + gw, PORTAL_H + 0.6),
                                                                 (oy, PORTAL_H + gw * 2.0))], PALE)
    for sy in (-1, 1):
        y = oy + sy * (gw + 0.5)
        c.box("W", fx0 - 1.3, y - 0.5, 0, fx0, y + 0.5, PORTAL_H + gw * 1.3, PALE)
        c.cone("W", fx0 - 0.65, y, 0.75, PORTAL_H + gw * 1.3, PORTAL_H + gw * 1.3 + 3.5, PALE)
    c.base.box(fx0 - 4.4, oy - hn - 1, 0, fx0 - 1.5, oy + hn + 1, 0.4, PALE)   # steps
    c.base.box(fx0 - 3.0, oy - hn - 1, 0, fx0 - 1.5, oy + hn + 1, 0.8, PALE)
    for sy in (-1, 1):                                            # banners on the towers' fronts
        y = oy + sy * (hn + TOWER / 2)
        c.box("W", fx0 - 0.4, y - 1.2, NAVE_H * 0.5 - 8, fx0 - 0.35, y + 1.2, NAVE_H * 0.5 - 0.8, RED)
    return c.finish(prefix)


def shows(part, state="outside", cut=()):
    """Whether a church layer shows: "outside" shows everything but the crowns; "inside" hides
    the roofs and upper parts, and on each side in `cut` (the camera's side) swaps its wall top
    for its crown."""
    inside = state == "inside"
    if part in ("Upper", "Roofs"):
        return not inside
    if part.startswith("Top_"):
        return not (inside and part[4:] in cut)
    if part.startswith("Crown_"):
        return inside and part[6:] in cut
    return True


def show_church(state="outside", cut=(), prefix="GreatChurch"):
    import bpy
    for o in bpy.data.objects:
        if o.name.startswith(prefix + "_"):
            o.hide_viewport = o.hide_render = not shows(o.name[len(prefix) + 1:], state, cut)


# The church three times in a row, west to east, its south side toward the viewer: outside;
# inside with every wall standing (the camera is inside the walls); inside with the south and
# west walls cut (the camera beyond them). Each copy keeps only the layers it shows.
STATES = [("Outside", "outside", ()), ("Inside", "inside", ()), ("InsideCut", "inside", ("S", "W"))]
SPACING = 95.0


def church_states(oy=0.0):
    import bpy
    objs = []
    for i, (name, state, cut) in enumerate(STATES):
        layers = great_church(SPACING * i, oy, prefix=f"Church{name}")
        for part, o in layers.items():
            if shows(part, state, cut):
                objs.append(o)
            else:
                bpy.data.objects.remove(o)
    return objs


# ---------------------------------------------------------------- the castle

WALL_H = 5.0
WALL_T = 1.2


def castle(ox, oy, w=26.0, d=20.0):
    """A curtain wall round [ox, ox+w] x [oy, oy+d], gate in the south (-y) wall."""
    b = Build()
    gate_x = ox + w / 2
    runs = [((ox, oy), (gate_x - 1.6, oy), (0, -1)), ((gate_x + 1.6, oy), (ox + w, oy), (0, -1)),
            ((ox + w, oy), (ox + w, oy + d), (1, 0)), ((ox + w, oy + d), (ox, oy + d), (0, 1)),
            ((ox, oy + d), (ox, oy), (-1, 0))]
    for p0, p1, out in runs:
        # A wall run: the wall, a walkway behind the crenels, merlons on the outer edge, slits.
        n = Vector((*out, 0))
        a0, a1 = Vector((*p0, 0)), Vector((*p1, 0))
        pts = [a0 + n * k + Vector((0, 0, z)) for k in (0, -WALL_T) for z in (0, WALL_H)]
        pts += [a1 + n * k + Vector((0, 0, z)) for k in (0, -WALL_T) for z in (0, WALL_H)]
        b.hull(pts, CASTLE)
        b.merlons(p0, p1, WALL_H, 0.7, 0.35, CASTLE, outward=-n)
        length = (a1 - a0).length
        along = (a1 - a0).normalized()
        for i in range(int(length // 3)):
            s = 1.5 + i * 3
            b.panel(a0 + n * 0.0, along, n, s - 0.1, s + 0.1, 1.6, 3.2, DARK)
    for cx, cy in ((ox, oy), (ox + w, oy), (ox + w, oy + d), (ox, oy + d)):
        # Round corner towers, taller than the walls, crenellated, with slits.
        b.cyl(cx, cy, 2.2, 0, WALL_H + 3.0, CASTLE, sides=14)
        b.ring_merlons(cx, cy, 2.4, WALL_H + 3.0, 0.7, CASTLE, count=11)
        b.cyl(cx, cy, 2.4, WALL_H + 2.7, WALL_H + 3.0, CASTLE, sides=14)
    for gx in (gate_x - 2.6, gate_x + 2.6):
        # The gatehouse: two drum towers either side of an open gate (no roof over it).
        b.cyl(gx, oy - 0.4, 1.8, 0, WALL_H + 2.4, CASTLE, sides=12)
        b.ring_merlons(gx, oy - 0.4, 2.0, WALL_H + 2.4, 0.7, CASTLE, count=9)
        b.cyl(gx, oy - 0.4, 2.0, WALL_H + 2.1, WALL_H + 2.4, CASTLE, sides=12)
    b.box(gate_x - 1.6, oy - 0.3, WALL_H - 0.4, gate_x + 1.6, oy + 0.1, WALL_H + 0.3, CASTLE)   # a bridge of wall-walk
    b.panel((gate_x - 1.0, oy - 0.32, 0), (1, 0, 0), (0, -1, 0), 0.0, 2.0, 3.6, 4.4, METAL)       # the raised portcullis
    # The keep: a tall square tower in the north half, turrets on its corners, a banner.
    kx0, ky0, ks, kh = ox + w - 11, oy + d - 10, 8.0, 14.0
    b.box(kx0, ky0, 0, kx0 + ks, ky0 + ks, kh, CASTLE)
    for (p0, p1, out) in (((kx0, ky0), (kx0 + ks, ky0), (0, -1)), ((kx0 + ks, ky0), (kx0 + ks, ky0 + ks), (1, 0)),
                          ((kx0 + ks, ky0 + ks), (kx0, ky0 + ks), (0, 1)), ((kx0, ky0 + ks), (kx0, ky0), (-1, 0))):
        b.merlons(p0, p1, kh, 0.8, 0.4, CASTLE, outward=out)
        a0, a1 = Vector((*p0, 0)), Vector((*p1, 0))
        for zz in (3.0, 7.0, 10.5):
            b.panel(a0, (a1 - a0).normalized(), (*out, 0), ks / 2 - 0.35, ks / 2 + 0.35, zz, zz + 1.4, DARK, apex=zz + 1.8)
    for tx, ty in ((kx0, ky0), (kx0 + ks, ky0), (kx0 + ks, ky0 + ks), (kx0, ky0 + ks)):
        b.cyl(tx, ty, 1.1, kh - 4.0, kh + 2.0, CASTLE, sides=10)
        b.cone(tx, ty, 1.35, kh + 2.0, kh + 5.0, ROOF, sides=10)
    b.box(kx0 + ks / 2 - 0.05, ky0 + ks / 2 - 0.05, kh, kx0 + ks / 2 + 0.05, ky0 + ks / 2 + 0.05, kh + 4.0, METAL)
    b.box(kx0 + ks / 2 + 0.05, ky0 + ks / 2 - 0.02, kh + 2.6, kx0 + ks / 2 + 1.6, ky0 + ks / 2 + 0.02, kh + 3.8, RED)
    # A hall against the west wall, under a pitched roof.
    b.box(ox + 1.2, oy + d - 9, 0, ox + 8.5, oy + d - 1.2, 4.0, CASTLE)
    b.gable_roof(ox + 1.2, ox + 8.5, oy + d - 9, oy + d - 1.2, 4.0, 2.8, ROOF, along="y")
    A.finish(b.bm)
    return A.mesh_from("Castle", b.bm, MATS, coll="Monumental")


def scale_figures(spots):
    """Copies of the player (the preview rig's) standing at each (x, y, z)."""
    import bpy
    rig = bpy.data.objects.get("Rig")
    if rig is None:
        return
    parts = [o for o in bpy.data.objects if o.parent is rig]
    coll = A.collection("Preview")
    for x, y, z in spots:
        new_rig = rig.copy()
        coll.objects.link(new_rig)
        new_rig.location = (x, y, z)
        for p in parts:
            c = p.copy()
            coll.objects.link(c)
            c.parent = new_rig
            for md in c.modifiers:
                if md.type == "ARMATURE":
                    md.object = new_rig


def build():
    A.reset()
    church = church_states()
    keep = castle(SPACING - 13, 60)
    return {"GreatChurch": A.report(church), "Castle": A.report([keep])}


if __name__ == "__main__":
    result = build()
    cam = A.preview_rig(focus=(SPACING + 21, 0, 10), distance=330, pitch=34, yaw=-10, ground=170, human_at=(-20, 0))
    cam.data.lens = 32
    cam.data.clip_end = 2000
    import bpy
    bpy.data.objects["PreviewGround"].location.x = SPACING          # under all three
    # Players for scale, in each copy: on the steps, in the nave, in both aisles, in the apse.
    scale_figures([(SPACING * i + x, y, z) for i in range(len(STATES))
                   for x, y, z in ((-16, 2, 0.8), (20, 2, 0), (28, 15, 0), (12, -15, 0), (52, 0, 0))])
