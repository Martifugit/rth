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
It builds the church in three rows: A as polished, B and C restyled after Varrock castle
(docs/research/osrs-varrock-castle.md), B at A's heights and C lower. Each row shows it three
times: outside, inside, and inside with the near walls cut."""
import itertools
import math
import os
import sys

import bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
import rth_art as A  # noqa: E402

# Flat colours for judging shapes; the church then wears painted textures over some of them.
MATS = ["Gravestone", "Ashgrey", "Sable", "SynodRed", "Shingle", "Iron", "CryptFloor", "Blood", "Yew", "CryptBanner", "Gold"]
PALE, ROOF, DARK, RED, CASTLE, METAL, FLOOR, GLASS, DOOR, RUNNER, GOLD = range(len(MATS))

# The church's painted textures (lab/church_textures.py, the paint lab's style) by the flat colour
# they replace, with the tiles one repeat covers (along, up). The glass also glows a little.
PAINT = {"Gravestone": ("ChurchAshlar", 2, 2), "SynodRed": ("ChurchTrim", 1, 1), "Ashgrey": ("ChurchSlate", 2, 1),
         "Blood": ("ChurchGlass", 1, 1), "CryptFloor": ("ChurchFloor", 4, 4)}
GLOW = {"Blood": 0.22}


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


def arch_apex(w, spring):
    return arch(w, spring)[-1][1]


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
PIERS = range(1, BAYS - 1)  # which bay lines have a pier (not at the towers, not in the last bays)
FLOOR_Z = 0.6             # the floor stands on a plinth, three steps up
WALL = 1.0                # wall thickness
AISLE_H = 10.0            # the outer walls: the one storey you see from inside
ARCADE_H = 8.0            # the piers' capitals; the arches above hide inside
NAVE_H = 22.0             # nave walls, outside only
CUT = 2.3                 # a wall cut toward the camera stops at one storey, under its crown
TOWER = 12.0              # west towers' side
TOWER_H = 42.0            # the towers' square stages, then a belfry and a spire
BELFRY_R = 4.8
SPIRE_H = 26.0
SCREEN_H, SCREEN_PITCH = 28.0, 62.0   # the screen wall between the towers, and its gable
PORTAL_W, PORTAL_H = 7.0, 9.0
PITCH = 55.0
SIDES = ("N", "S", "E", "W")


class Church:
    """The church's layers, each its own object: `base` always shows; `upper` and `roofs` hide
    inside; `top[side]` (that side's walls from CUT to AISLE_H) swaps for `crown[side]` when the
    camera is on that side."""

    def __init__(self, aisle_h=None, crown_style="fins"):
        self.base, self.upper, self.roofs = Build(), Build(), Build()
        self.top = {s: Build() for s in SIDES}
        self.crown = {s: Build() for s in SIDES}
        self.aisle_h = aisle_h or AISLE_H                        # where the inside view stops
        self.crown_style = crown_style                           # "fins", or "merlons" (the plain church)

    def hull(self, side, pts, m):
        """A solid belonging to a side's walls, split by height into the layers."""
        for b, za, zb in ((self.base, -1e9, CUT), (self.top[side], CUT, self.aisle_h), (self.upper, self.aisle_h, 1e9)):
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
        row of pointed fins, taller ones where the bays' piers are. The plain church crowns its cut
        walls with the same merlons as its parapets."""
        c = self.crown[side]
        if self.crown_style == "merlons":
            for pts in merlon_parts(p0, p1, thick, CUT):
                c.hull(pts, PALE)
            return
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

    def spikes(self, side, a, b, size=0.3, height=1.1, every=1.6, m=PALE, surface=None):
        """Small pointed spikes standing along the line a -> b (x, y), e.g. crockets up a gable's
        rake or a parapet of points round a tower top. surface(x, y) is the height of what they
        stand on (a number for a flat top); each base corner sits a little into it, so a spike
        on a slope is flush all round."""
        a, b = Vector((*a[:2], 0)), Vector((*b[:2], 0))
        z_at = surface if callable(surface) else (lambda x, y: surface)
        count = max(1, round((b - a).length / every))
        for i in range(count + 1):
            q = a.lerp(b, i / count)
            h = height * (1.0 if i % 2 else 0.6)
            pts = [Vector((q.x + dx, q.y + dy, z_at(q.x + dx, q.y + dy) - 0.08)) for dx in (-size, size) for dy in (-size, size)]
            self.hull(side, pts + [Vector((q.x, q.y, z_at(q.x, q.y) + h))], m)

    def parapet(self, side, x0, x1, y0, y1, z, every=1.0):
        """A pierced parapet along x from x0 to x1, y0..y1 thick, standing on z: a rail on posts,
        open between them."""
        self.box(side, x0, y0, z + 0.85, x1, y1, z + 1.15, RED)
        count = max(1, round((x1 - x0) / every))
        for i in range(count + 1):
            x = x0 + (x1 - x0) * i / count
            self.box(side, x - 0.15, y0, z, x + 0.15, y1, z + 0.85, PALE)

    def arch_frame(self, side, face, w_in, w_out, spring, d0, d1, m, base=0.0):
        """A pointed-arch band (an archivolt and its jambs) between two arches, in depth d0..d1 out
        from the face. face = (origin, along, outward); s = 0 is the arch's centre."""
        o, al, out = face
        n = 3 if w_out < 2.0 else 5                              # both arches in step
        pi, po = arch(w_in, spring, n), arch(w_out, spring, n)
        for sg in (-1, 1):
            at = lambda s, z, d: Vector(o) + Vector(al) * (sg * s) + Vector((0, 0, z)) + Vector(out) * d
            self.hull(side, [at(s, z, d) for s in (w_in, w_out) for z in (base, spring) for d in (d0, d1)], m)
            for k in range(len(pi) - 1):
                self.hull(side, [at(s, z, d) for (s, z) in (pi[k], pi[k + 1], po[k], po[k + 1]) for d in (d0, d1)], m)

    def arch_spandrel(self, side, face, w, spring, top, half, d0, d1, m, base=0.0):
        """The solid round a pointed opening: from the arch out to a rectangle `half` either side
        and up to `top`, in depth d0..d1."""
        o, al, out = face
        pts = arch(w, spring)
        for sg in (-1, 1):
            at = lambda s, z, d: Vector(o) + Vector(al) * (sg * s) + Vector((0, 0, z)) + Vector(out) * d
            self.hull(side, [at(s, z, d) for s in (w, half) for z in (base, top) for d in (d0, d1)], m)
            for k in range(len(pts) - 1):
                (s0, z0), (s1, z1) = pts[k], pts[k + 1]
                self.hull(side, [at(s, z, d) for (s, z) in ((s0, z0), (s1, z1), (s0, top), (s1, top)) for d in (d0, d1)], m)

    def arch_panel(self, side, face, w, spring, base, m, depth=0.04, d0=0.0):
        """A filled pointed shape on a face (a window's glass, a door)."""
        o, al, out = face
        pts = arch(w, spring)
        outline = [(-w, base), (w, base)] + pts + [(-s, z) for s, z in pts]
        self.hull(side, [Vector(o) + Vector(al) * s + Vector((0, 0, z)) + Vector(out) * d
                         for s, z in outline for d in (d0, d0 + depth)], m)

    def finish(self, prefix="GreatChurch", smooth=None):
        """smooth (degrees): light across edges flatter than this as one surface (the reference's
        smooth lighting: octagons, apse and shafts stop looking faceted), keeping sharper edges."""
        objs = {}
        parts = [("Base", self.base), ("Upper", self.upper), ("Roofs", self.roofs)]
        parts += [(f"Top_{s}", self.top[s]) for s in SIDES] + [(f"Crown_{s}", self.crown[s]) for s in SIDES]
        for name, b in parts:
            A.finish(b.bm)
            objs[name] = A.mesh_from(f"{prefix}_{name}", b.bm, MATS, coll="Monumental", smooth_angle=smooth)
        return objs


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
    c.base.box(fx0, oy - ha - WALL, 0, x1 + WALL, oy + ha + WALL, FLOOR_Z, FLOOR)
    c.base.hull([(x1 + (hn + WALL) * math.cos(a), oy + (hn + WALL) * math.sin(a), z)
                 for a in (math.pi * (-0.5 + k / 18) for k in range(19)) for z in (0, FLOOR_Z)], FLOOR)
    # Inside: pale bands across the floor at every pier, a red runner up the nave, and a dais over
    # the last bays and the apse (three steps up), left empty for the altar and what goes round it.
    zf = FLOOR_Z
    dais_x = x0 + BAY * (BAYS - 2) + 2.0
    for i in PIERS:
        c.base.box(x0 + BAY * i - 0.3, oy - ha, zf, x0 + BAY * i + 0.3, oy + ha, zf + 0.02, PALE)
    c.base.box(fx0 - 3.0, oy - 1.6, zf, dais_x - 0.9, oy + 1.6, zf + 0.03, RUNNER)
    for e in (-1, 1):
        c.base.box(fx0 - 3.0, oy + e * 1.6, zf, dais_x - 0.9, oy + e * 1.45, zf + 0.035, GOLD)
    for k in range(3):
        c.base.box(dais_x - 0.9 + 0.3 * k, oy - hn, zf, x1, oy + hn, zf + 0.2 * (k + 1), PALE)
    c.base.hull([(x1 + hn * math.cos(a), oy + hn * math.sin(a), z)
                 for a in (math.pi * (-0.5 + k / 18) for k in range(19)) for z in (zf, zf + 0.6)], PALE)

    for side, key in ((-1, "S"), (1, "N")):
        y_pier, y_wall = oy + side * hn, oy + side * (ha + WALL / 2)
        y_out, y_in = y_wall + side * WALL / 2, y_wall - side * WALL / 2
        # The outer wall, its lancets on both faces, a shaft inside at every pier.
        c.box(key, x0, y_wall - WALL / 2, 0, x1, y_wall + WALL / 2, AISLE_H, PALE)
        c.box(key, x0, y_out, 0, x1, y_out + side * 0.3, 0.9, PALE)                             # plinth
        c.box(key, x0, y_out, AISLE_H - 0.45, x1, y_out + side * 0.25, AISLE_H - 0.1, RED)    # drip course under the parapet
        for i in range(BAYS):
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_out, side), (y_in, -side)):
                f = ((xm, face, 0), (1, 0, 0), (0, out, 0))
                c.arch_frame(key, f, 1.2, 1.6, 6.6, 0.0, 0.25, PALE, base=1.8)
                c.arch_panel(key, f, 1.2, 6.6, 1.8, GLASS)
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            c.box(key, xp - 0.4, y_in, 0, xp + 0.4, y_in - side * 0.4, AISLE_H, PALE)
        c.box(key, x0, y_in, 0, x1, y_in - side * 0.6, FLOOR_Z + 0.5, PALE)                   # a stone bench along the wall
        c.box(key, x0, y_in, 1.45, x1, y_in - side * 0.2, 1.75, RED)                            # a sill course under the windows
        c.crown_run(key, (x0, y_wall), (x1, y_wall), (0, side), pier_every=BAY)
        # The arcade: piers with capitals (always), pointed arches between them (outside only).
        # No pier against the towers, and none in the last bays: the east end stays open for the
        # altar and what goes round it. Arches run between neighbouring supports.
        supports = [x0] + [x0 + BAY * i for i in PIERS] + [x1]
        for xp in supports[1:-1]:
            c.upper.box(xp - 0.7, y_pier - WALL / 2, ARCADE_H, xp + 0.7, y_pier + WALL / 2, AISLE_H + 1, PALE)   # over the capital
            c.base.box(xp - 1.0, y_pier - 1.0, 0, xp + 1.0, y_pier + 1.0, FLOOR_Z + 0.5, PALE)   # plinth
            c.base.box(xp - 0.6, y_pier - 0.6, FLOOR_Z + 0.5, xp + 0.6, y_pier + 0.6, ARCADE_H - 0.8, PALE)
            for dx, dy in ((0.6, 0), (-0.6, 0), (0, 0.6), (0, -0.6)):          # four shafts round a core
                c.base.cyl(xp + dx, y_pier + dy, 0.38, FLOOR_Z + 0.5, ARCADE_H - 0.8, PALE, sides=8)
            c.base.box(xp - 0.8, y_pier - 0.8, ARCADE_H - 0.8, xp + 0.8, y_pier + 0.8, ARCADE_H - 0.4, PALE)   # a stepped capital
            c.base.box(xp - 1.0, y_pier - 1.0, ARCADE_H - 0.4, xp + 1.0, y_pier + 1.0, ARCADE_H, PALE)
        for k, (sa, sb) in enumerate(zip(supports, supports[1:])):
            xa = sa + (0.7 if k > 0 else 0.0)
            xb = sb - (0.7 if k < len(supports) - 2 else 0.0)
            xm = (xa + xb) / 2
            for xs, xe in ((xa, xm), (xb, xm)):                    # spandrels make the pointed arch
                c.upper.hull([(x, y_pier + dy, z) for x, z in ((xs, ARCADE_H), (xs, AISLE_H + 1), (xe, AISLE_H + 1))
                              for dy in (-WALL / 2, WALL / 2)], PALE)
        c.upper.box(x0, y_pier - WALL / 2, AISLE_H + 1, x1, y_pier + WALL / 2, NAVE_H, PALE)
        c.upper.box(x0, y_pier + side * WALL / 2, NAVE_H - 0.45, x1, y_pier + side * (WALL / 2 + 0.25), NAVE_H - 0.1, RED)   # drip course
        for i in range(BAYS):                                     # clerestory lancets, outside only (never seen inside)
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_pier + side * WALL / 2, side),):
                f = ((xm, face, 0), (1, 0, 0), (0, out, 0))
                c.arch_frame(key, f, 1.4, 1.8, NAVE_H - 3.0, 0.0, 0.25, PALE, base=AISLE_H + 4.5)
                c.arch_panel(key, f, 1.4, NAVE_H - 3.0, AISLE_H + 4.5, GLASS)
        # Aisle roof: a lean-to from the aisle wall up to the clerestory, behind a pierced parapet
        # on the wall's outer half; another parapet runs along the clerestory's head.
        c.roofs.hull([(x, y_wall, z) for x in (x0, x1) for z in (AISLE_H, AISLE_H + 0.3)] +
                     [(x, y_pier + side * WALL / 2, z) for x in (x0, x1) for z in (AISLE_H + 4.0, AISLE_H + 4.3)], ROOF)
        c.parapet(key, x0, x1, y_wall, y_out, AISLE_H)
        c.parapet(key, x0, x1, y_pier, y_pier + side * WALL / 2, NAVE_H)
        # Buttresses: piers on the outer wall with tall pinnacles, flying across to the clerestory.
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            far = y_out + side * 2.4
            zb = AISLE_H + 3.5
            c.box(key, xp - 0.7, y_out, 0, xp + 0.7, far, zb, PALE)
            # A pinnacle as wide as the buttress on its outer end; between it and the wall the
            # top slopes up to the wall.
            pc = far - side * 0.7
            c.cone(key, xp, pc, 0.7 * math.sqrt(2), zb, zb + 6.5, PALE)
            c.hull(key, [(xp + dx, y, zb) for dx in (-0.7, 0.7) for y in (y_out, pc)] +
                   [(xp + dx, y_out, zb + 1.2) for dx in (-0.7, 0.7)], PALE)
            c.upper.hull([(xp + dx, far - side * 0.6, z) for dx in (-0.4, 0.4) for z in (AISLE_H + 2.6, AISLE_H + 3.4)] +
                         [(xp + dx, y_pier + side * WALL / 2, z) for dx in (-0.4, 0.4) for z in (NAVE_H - 3.5, NAVE_H - 2.7)], PALE)
            c.cone(key, xp, y_pier, 0.45 * math.sqrt(2), NAVE_H - 0.5, NAVE_H + 4.0, PALE)   # on the clerestory wall head
    # Nave roof.
    c.roofs.gable_roof(x0, x1, oy - hn, oy + hn, NAVE_H, rise, ROOF, over=0.0)   # its eaves behind the parapets

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
        c.hull("E", [(x1 + rr * math.cos(a), oy + rr * math.sin(a), z) for a in (a0, a1) for rr in (hn, ro + 0.3) for z in (0, 0.9)], PALE)   # plinth
        am = (a0 + a1) / 2
        nrm = Vector((math.cos(am), math.sin(am), 0))
        along = Vector((-math.sin(am), math.cos(am), 0))
        rc = ro * math.cos((a1 - a0) / 2)                          # the segment's flat face
        for face, out in ((rc, nrm), (hn * math.cos((a1 - a0) / 2), -nrm)):
            p = Vector((x1, oy, 0)) + nrm * face
            c.arch_frame("E", (tuple(p), tuple(along), tuple(out)), 0.9, 1.25, NAVE_H - 3.0, 0.0, 0.25, PALE, base=1.8)
            c.arch_panel("E", (tuple(p), tuple(along), tuple(out)), 0.9, NAVE_H - 3.0, 1.8, GLASS)
        mid = lambda a, r_: (x1 + r_ * math.cos(a), oy + r_ * math.sin(a))
        c.crown_run("E", mid(a0, hn + WALL / 2), mid(a1, hn + WALL / 2), tuple(nrm))
        if k % 2 == 0:                                            # buttresses round the apse
            pb = Vector((x1 + ro * math.cos(a0), oy + ro * math.sin(a0), 0))
            n = Vector((math.cos(a0), math.sin(a0), 0))
            t = Vector((-math.sin(a0), math.cos(a0), 0))
            zb = NAVE_H - 7
            c.hull("E", [pb + t * dt + n * dn + Vector((0, 0, z)) for dt in (-0.7, 0.7) for dn in (-0.3, 2.2)
                         for z in (0, zb)], PALE)
            # On top, a blade as wide as the buttress, raking out to a point and back up to the
            # wall where the roof begins; it thins to an edge toward the top.
            blade = [(-0.3, zb, 0.7), (2.2, zb, 0.7), (2.7, zb + 3.2, 0.25), (-0.3, NAVE_H + 0.4, 0.25)]
            c.hull("E", [pb + n * dn + t * (sg * w) + Vector((0, 0, z)) for dn, z, w in blade for sg in (-1, 1)], PALE)
    for side in (-1, 1):                                          # close the aisle roofs' ends
        c.hull("E", [(x, oy + side * y, z) for x in (x1, x1 + WALL)
                     for y, z in ((ha + WALL, AISLE_H), (hn, AISLE_H), (hn, AISLE_H + 4.3))], PALE)
    c.roofs.cone(x1, oy, ro + 0.4, NAVE_H, NAVE_H + rise, ROOF, sides=18)
    # A slender spire where the apse roof meets the nave roof.
    fx = x1
    c.roofs.cyl(fx, oy, 1.6, NAVE_H + rise - 3, NAVE_H + rise + 5, PALE, sides=8)   # its foot down in both roofs
    c.roofs.cone(fx, oy, 1.9, NAVE_H + rise + 5, NAVE_H + rise + 19, ROOF, sides=8)

    # The west front (after images/cruor_chapel.png): one tall facade. Two square towers stand
    # flush with it, their corners climbing as shafts that end in pinnacles, then an octagonal
    # belfry and spire each; between them the gable wall with one great red window (the rose in
    # its head), and below it a deep portal under a spiky gable.
    def shift(face, s):
        o, al, out = face
        return (tuple(Vector(o) + Vector(al) * s), al, out)

    for side in (-1, 1):
        ya, yb = sorted((oy + side * hn, oy + side * (hn + TOWER)))
        yc, xc = (ya + yb) / 2, (fx0 + ox) / 2
        yo = oy + side * (hn + TOWER)                             # the outer side
        c.box("W", fx0, ya, 0, ox, yb, TOWER_H, PALE)
        c.box("W", fx0 - 0.3, ya, 0, fx0, yb, 0.9, PALE)                 # plinth, front and outer side
        c.box("W", fx0, yo, 0, ox, yo + side * 0.3, 0.9, PALE)
        for cx_, cy_ in ((fx0, ya), (fx0, yb), (ox, ya), (ox, yb)):   # corner shafts and pinnacles
            c.box("W", cx_ - 0.9, cy_ - 0.9, 0, cx_ + 0.9, cy_ + 0.9, TOWER_H + 1.0, PALE)
            c.cone("W", cx_, cy_, 1.1, TOWER_H + 1.0, TOWER_H + 10.0, PALE)
        for z in (11.0, 27.0):                                    # red string courses at the tiers
            c.box("W", fx0 - 0.3, ya, z, fx0, yb, z + 0.5, RED)
            c.box("W", fx0, yo, z, ox, yo + side * 0.3, z + 0.5, RED)
        front = ((fx0, yc, 0), (0, 1, 0), (-1, 0, 0))
        outer = ((xc, yo, 0), (1, 0, 0), (0, side, 0))
        c.arch_frame("W", front, 1.7, 2.4, 4.6, 0.0, 0.5, PALE)   # a red door in the base
        c.arch_panel("W", front, 1.7, 4.6, 0.0, DOOR)
        for face in (front, outer):
            for s in (-2.6, 2.6):                                 # two tall red lancets
                f = shift(face, s)
                c.arch_frame("W", f, 1.0, 1.4, 21.0, 0.0, 0.3, PALE, base=12.5)
                c.arch_panel("W", f, 1.0, 21.0, 12.5, GLASS)
            for s in (-2.6, 2.6):                                 # a tall dark pair in the top tier
                f = shift(face, s)
                c.arch_frame("W", f, 0.9, 1.3, 36.5, 0.0, 0.3, PALE, base=28.5)
                c.arch_panel("W", f, 0.9, 36.5, 28.5, DARK)
        for (p0, p1) in (((fx0 + 1.2, ya), (fx0 + 1.2, yb)), ((ox - 1.2, ya), (ox - 1.2, yb)),
                         ((fx0, ya + side * 0 + 1.2), (ox, ya + 1.2)), ((fx0, yb - 1.2), (ox, yb - 1.2))):
            q0, q1 = Vector((*p0, TOWER_H)), Vector((*p1, TOWER_H))
            d = (q1 - q0).normalized()
            c.spikes("W", q0 + d * 1.6, q1 - d * 1.6, size=0.25, height=1.4, every=1.4, surface=TOWER_H)
        # The octagonal belfry, its cornice and spire.
        ring = lambda r, z: [Vector((xc + r * math.cos(2 * math.pi * (k + 0.5) / 8), yc + r * math.sin(2 * math.pi * (k + 0.5) / 8), z))
                             for k in range(8)]
        top = TOWER_H + 11.0
        c.hull("W", ring(BELFRY_R, TOWER_H) + ring(BELFRY_R, top), PALE)
        rf = BELFRY_R * math.cos(math.pi / 8)
        for k in range(8):
            phi = 2 * math.pi * k / 8
            n = Vector((math.cos(phi), math.sin(phi), 0))
            al = Vector((-math.sin(phi), math.cos(phi), 0))
            c.arch_panel("W", (tuple(Vector((xc, yc, 0)) + n * rf), tuple(al), tuple(n)), 0.7, top - 3.5, TOWER_H + 2.0, DARK)
            g0 = Vector((xc, yc, 0)) + n * rf                    # a small gable over the opening
            c.hull("W", [g0 + al * s + n * d + Vector((0, 0, z)) for s, z in ((-1.1, top - 1.6), (1.1, top - 1.6), (0.0, top + 2.6))
                         for d in (0.0, 0.45)], PALE)
            c.cone("W", *(g0 + n * 0.22)[:2], 0.3, top + 1.7, top + 3.8, PALE)
        c.hull("W", ring(BELFRY_R + 0.4, top) + ring(BELFRY_R + 0.4, top + 0.6), RED)
        c.hull("W", ring(BELFRY_R + 0.1, top + 0.6) + [Vector((xc, yc, top + SPIRE_H))], ROOF)
        for k in range(8):                                       # small pinnacles round the spire's foot
            p = ring(BELFRY_R + 0.1, 0)[k]
            c.cone("W", p.x, p.y, 0.35, top + 0.6, top + 3.2, PALE)
        # Inside, the towers' inner faces are the porch's walls: crown them when cut.
        c.crown_run("W", (fx0, oy + side * (hn + 0.5)), (ox, oy + side * (hn + 0.5)), (0, -side))

    # The screen wall between the towers: it rises above the nave roof into a steep gable, spiky
    # along its rakes, with room for one great window.
    face = ((fx0, oy, 0), (0, 1, 0), (-1, 0, 0))
    PH = PORTAL_W / 2 + 2.1                                       # the portal block's half-width
    T = arch_apex(PORTAL_W / 2 + 1.5, PORTAL_H) + 0.8              # and its top
    srise = hn * math.tan(math.radians(SCREEN_PITCH))          # a screen gable, steeper and higher than the roof behind it
    c.box("W", fx0, oy - hn, 0, fx0 + WALL, oy - PH, SCREEN_H, PALE)
    c.box("W", fx0, oy + PH, 0, fx0 + WALL, oy + hn, SCREEN_H, PALE)
    c.box("W", fx0, oy - PH, T, fx0 + WALL, oy + PH, SCREEN_H, PALE)
    c.hull("W", [(x, y, z) for x in (fx0, fx0 + WALL) for y, z in ((oy - hn, SCREEN_H), (oy + hn, SCREEN_H), (oy, SCREEN_H + srise))], PALE)
    for sy in (-1, 1):
        c.spikes("W", (fx0 + 0.5, oy + sy * (hn - 0.8)), (fx0 + 0.5, oy + sy * 1.2), surface=lambda x, y: SCREEN_H + srise * (1 - abs(y - oy) / hn),
                 size=0.28, height=1.5, every=1.5)
        c.crown_run("W", (fx0 + WALL / 2, oy + sy * hn), (fx0 + WALL / 2, oy + sy * PH), (-1, 0))
    c.cone("W", fx0 + 0.5, oy, 0.9, SCREEN_H + srise - 1.4, SCREEN_H + srise + 4.5, PALE)   # foot down in the gable
    c.roofs.gable_roof(fx0 + WALL, ox, oy - hn, oy + hn, NAVE_H, rise, ROOF, over=0.0)

    # The great window: red, pale mullions, the rose in its head.
    wb, ws, ww = T + 6.0, SCREEN_H + 2.0, 5.6                # clear of the gable's edges and their crockets
    c.arch_frame("W", face, ww, ww + 0.45, ws, 0.0, 0.3, PALE, base=wb)
    c.arch_panel("W", face, ww, ws, wb, GLASS)
    for s in (-2.8, 0.0, 2.8):
        c.box("W", fx0 - 0.15, oy + s - 0.13, wb, fx0, oy + s + 0.13, ws + 0.4, PALE)
    for s in (-2.8, 2.8):                                         # two sub-arches, two lights each
        c.arch_frame("W", shift(face, s), 2.55, 2.8, ws + 0.4, 0.0, 0.15, PALE, base=ws + 0.4)
    rz, rr = ws + 6.0, 2.0                                      # the rose over the two sub-arches
    for k in range(12):                                          # the rose: a ring and its spokes
        a0, a1 = 2 * math.pi * k / 12, 2 * math.pi * (k + 1) / 12
        c.hull("W", [(fx0 - d, oy + r * math.cos(a), rz + r * math.sin(a)) for a in (a0, a1) for r in (rr - 0.3, rr)
                     for d in (0.0, 0.2)], PALE)
    for k in range(6):
        a = math.pi * k / 6
        c.hull("W", [(fx0 - d, oy + r * math.cos(a) + e * math.sin(a), rz + r * math.sin(a) - e * math.cos(a))
                     for r in (-rr, rr) for e in (-0.1, 0.1) for d in (0.0, 0.15)], PALE)

    # The portal: three orders stepping out from the opening, in a block under a steep gable
    # with crockets, between two pinnacles.
    pw = PORTAL_W / 2
    c.arch_spandrel("W", face, pw + 1.5, PORTAL_H, T, PH, -WALL, 3.0, PALE)
    for k in range(3):
        c.arch_frame("W", face, pw + 0.5 * k, pw + 0.5 * (k + 1), PORTAL_H, -WALL, 1.0 + k, PALE if k != 1 else RED)
    c.hull("W", [(fx0 - d, oy + y, z) for d in (-WALL, 3.0) for y, z in ((-PH, T), (PH, T), (0, T + 5.0))], PALE)
    for sy in (-1, 1):
        c.spikes("W", (fx0 - 2.8, oy + sy * (PH - 0.6)), (fx0 - 2.8, oy + sy * 0.9), size=0.22, height=1.1, every=1.2,
                 surface=lambda x, y: T + 5.0 * (1 - abs(y - oy) / PH))
        y = oy + sy * (PH + 0.6)
        c.box("W", fx0 - 2.9, y - 0.55, 0, fx0 - 1.8, y + 0.55, T + 1.5, PALE)
        c.cone("W", fx0 - 2.35, y, 0.75, T + 1.5, T + 6.5, PALE)
    c.cone("W", fx0 - 2.6, oy, 0.6, T + 4.6, T + 7.5, PALE)
    c.base.box(fx0 - 3.0, oy - PH, 0, fx0, oy + PH, FLOOR_Z, FLOOR)   # the floor out under the portal
    c.base.box(fx0 - 6.0, oy - PH - 2, 0, fx0 - 3.0, oy + PH + 2, FLOOR_Z / 3, PALE)   # steps up to it
    c.base.box(fx0 - 4.5, oy - PH - 2, 0, fx0 - 3.0, oy + PH + 2, FLOOR_Z * 2 / 3, PALE)
    return c.finish(prefix)


# ---------------------------------------------------------------- the plain church (Varrock rules)

# The same plan as great_church, restyled after docs/research/osrs-varrock-castle.md:
#   - walls are skins a quarter tile thick, all in one stone (one brick texture);
#   - one repeated silhouette element: merlons, one a tile, along every wall top, and one pinnacle
#     shape on every buttress and tower corner (no crockets, gablets or spikes of other sizes);
#   - small windows (a tile wide), red only in the glass; the great window and its rose stay as
#     the one big statement;
#   - towers chamfered on their outer corners (rounder, as the reference's octagonal towers);
#   - smooth lighting across shallow edges.
# PLAIN_B keeps the great church's heights; PLAIN_C is lower and wider in feel.
PLAIN_B = dict(aisle_h=10.0, arcade_h=8.0, nave_h=22.0, aisle_rise=4.0, pitch=55.0, tower_h=42.0, belfry_h=11.0,
               spire_h=26.0, portal_w=7.0, portal_h=9.0, window_w=5.0)   # the screen is the nave roof's gable
PLAIN_C = dict(aisle_h=7.0, arcade_h=5.6, nave_h=13.0, aisle_rise=2.5, pitch=45.0, tower_h=22.0, belfry_h=7.0,
               spire_h=13.0, screen_h=15.0, screen_pitch=55.0, portal_w=5.0, portal_h=6.0, window_w=4.0)
WT = 0.25                 # a wall is a skin
CW = 0.5                  # the clerestory, standing on the arcade
CHAMFER = 3.0             # the towers' outer corners cut off


def plain_church(ox, oy, P, prefix="PlainChurch"):
    AH, ARH, NH, AR = P["aisle_h"], P["arcade_h"], P["nave_h"], P["aisle_rise"]
    TH, BH, SH = P["tower_h"], P["belfry_h"], P["spire_h"]
    SCH = P.get("screen_h", NH)                                   # by default the screen is the end of the nave roof:
    screen_pitch = P.get("screen_pitch", P["pitch"])             # its eaves and its pitch
    c = Church(aisle_h=AH, crown_style="merlons")
    L = BAY * BAYS
    hn = NAVE_W / 2
    ha = hn + AISLE_W
    rise = hn * math.tan(math.radians(P["pitch"]))
    x0, x1 = ox, ox + L
    fx0 = ox - TOWER
    zf = FLOOR_Z

    def pinnacle(side, cx, cy, z, half=0.6):
        """The one pinnacle: a square pyramid, as tall as five of its half-widths."""
        c.cone(side, cx, cy, half * math.sqrt(2), z, z + 5 * half, PALE)

    def battlement(side, p0, p1, thick, z, layer=None):
        for pts in merlon_parts(p0, p1, thick, z):
            (layer.hull(pts, PALE) if layer else c.hull(side, pts, PALE))

    def lancet(side, face, w, base, spring, m=GLASS, frame=0.2):
        c.arch_frame(side, face, w, w + frame, spring, 0.0, 0.12, PALE, base=base)
        c.arch_panel(side, face, w, spring, base, m)

    # Floor, bands, the runner and the dais, as in the great church.
    c.base.box(fx0, oy - ha - WT, 0, x1 + WT, oy + ha + WT, zf, FLOOR)
    c.base.hull([(x1 + (hn + CW) * math.cos(a), oy + (hn + CW) * math.sin(a), z)
                 for a in (math.pi * (-0.5 + k / 18) for k in range(19)) for z in (0, zf)], FLOOR)
    dais_x = x0 + BAY * (BAYS - 2) + 2.0
    for i in PIERS:
        c.base.box(x0 + BAY * i - 0.3, oy - ha, zf, x0 + BAY * i + 0.3, oy + ha, zf + 0.02, PALE)
    for k in range(3):
        c.base.box(dais_x - 0.9 + 0.3 * k, oy - hn, zf, x1, oy + hn, zf + 0.2 * (k + 1), PALE)
    c.base.hull([(x1 + hn * math.cos(a), oy + hn * math.sin(a), z)
                 for a in (math.pi * (-0.5 + k / 18) for k in range(19)) for z in (zf, zf + 0.6)], PALE)
    c.base.box(fx0 - 3.0, oy - 1.6, zf, dais_x - 0.9, oy + 1.6, zf + 0.03, RUNNER)

    wb = zf + 1.6                                                 # aisle windows: a tile wide
    wsp = wb + (AH - wb) * 0.45
    cb, csp = AH + AR + 0.4, NH - 1.4                             # clerestory windows
    for side, key in ((-1, "S"), (1, "N")):
        y_pier, y_wall = oy + side * hn, oy + side * (ha + WT / 2)
        y_out, y_in = y_wall + side * WT / 2, y_wall - side * WT / 2
        c.box(key, x0, y_wall - WT / 2, 0, x1, y_wall + WT / 2, AH, PALE)
        battlement(key, (x0, y_wall), (x1, y_wall), WT, AH)
        c.crown_run(key, (x0, y_wall), (x1, y_wall), (0, side), thick=WT)
        c.box(key, x0, y_in, 0, x1, y_in - side * 0.6, zf + 0.5, PALE)            # a bench along the wall
        for i in range(BAYS):
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_out, side), (y_in, -side)):
                lancet(key, ((xm, face, 0), (1, 0, 0), (0, out, 0)), 0.5, wb, wsp)
        # The arcade.
        supports = [x0] + [x0 + BAY * i for i in PIERS] + [x1]
        for xp in supports[1:-1]:
            c.upper.box(xp - 0.7, y_pier - CW / 2, ARH, xp + 0.7, y_pier + CW / 2, AH + 1, PALE)
            c.base.box(xp - 1.0, y_pier - 1.0, 0, xp + 1.0, y_pier + 1.0, zf + 0.5, PALE)
            c.base.box(xp - 0.6, y_pier - 0.6, zf + 0.5, xp + 0.6, y_pier + 0.6, ARH - 0.8, PALE)
            for dx, dy in ((0.6, 0), (-0.6, 0), (0, 0.6), (0, -0.6)):
                c.base.cyl(xp + dx, y_pier + dy, 0.38, zf + 0.5, ARH - 0.8, PALE, sides=8)
            c.base.box(xp - 1.0, y_pier - 1.0, ARH - 0.8, xp + 1.0, y_pier + 1.0, ARH, PALE)
        for k, (sa, sb) in enumerate(zip(supports, supports[1:])):
            xa = sa + (0.7 if k > 0 else 0.0)
            xb = sb - (0.7 if k < len(supports) - 2 else 0.0)
            xm = (xa + xb) / 2
            for xs, xe in ((xa, xm), (xb, xm)):
                c.upper.hull([(x, y_pier + dy, z) for x, z in ((xs, ARH), (xs, AH + 1), (xe, AH + 1))
                              for dy in (-CW / 2, CW / 2)], PALE)
        # The clerestory: a skin on the arcade, small lancets, a battlement.
        c.upper.box(x0, y_pier - CW / 2, AH + 1, x1, y_pier + CW / 2, NH, PALE)
        for pts in merlon_parts((x0, y_pier), (x1, y_pier), CW, NH):
            c.upper.hull(pts, PALE)
        for i in range(BAYS):
            f = ((x0 + BAY * (i + 0.5), y_pier + side * CW / 2, 0), (1, 0, 0), (0, side, 0))
            c.arch_frame(key, f, 0.5, 0.7, csp, 0.0, 0.12, PALE, base=cb)
            c.arch_panel(key, f, 0.5, csp, cb, GLASS)
        c.roofs.hull([(x, y_in, z) for x in (x0, x1) for z in (AH, AH + 0.3)] +
                     [(x, y_pier + side * CW / 2, z) for x in (x0, x1) for z in (AH + AR, AH + AR + 0.3)], ROOF)
        # Buttresses: chunky, the one pinnacle on each, flying across to the clerestory.
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            far = y_out + side * 1.6
            zb = AH + 0.25 * (NH - AH)                            # above the wall, so the arm can climb
            za = NH - 0.35 * (NH - AH - AR)                       # where the arm meets the clerestory
            c.box(key, xp - 0.6, y_out, 0, xp + 0.6, far, zb, PALE)
            pinnacle(key, xp, y_out + side * 0.8, zb)
            c.upper.hull([(xp + dx, y_out + side * 1.0, z) for dx in (-0.35, 0.35) for z in (zb - 0.9, zb - 0.3)] +
                         [(xp + dx, y_pier + side * CW / 2, z) for dx in (-0.35, 0.35) for z in (za, za + 0.6)], PALE)
    c.roofs.gable_roof(x0, x1, oy - hn, oy + hn, NH, rise, ROOF, over=0.0)

    # East: the aisles close, the apse (a skin, small lancets, a battlement; blade buttresses).
    for side in (-1, 1):
        y_end = oy + side * (ha + WT)
        c.box("E", x1, oy + side * hn, 0, x1 + WT, y_end, AH, PALE)
        battlement("E", (x1 + WT / 2, oy + side * hn), (x1 + WT / 2, y_end), WT, AH)
        c.crown_run("E", (x1 + WT / 2, oy + side * hn), (x1 + WT / 2, y_end), (1, 0), thick=WT)
        c.hull("E", [(x, oy + side * y, z) for x in (x1, x1 + WT)
                     for y, z in ((ha + WT, AH), (hn, AH), (hn, AH + AR + 0.3))], PALE)
    segs, ri, ro = 9, hn, hn + CW
    for k in range(segs):
        a0, a1 = math.pi * (-0.5 + k / segs), math.pi * (-0.5 + (k + 1) / segs)
        c.hull("E", [(x1 + rr * math.cos(a), oy + rr * math.sin(a), z) for a in (a0, a1) for rr in (ri, ro) for z in (0, NH)], PALE)
        am = (a0 + a1) / 2
        nrm = Vector((math.cos(am), math.sin(am), 0))
        along = Vector((-math.sin(am), math.cos(am), 0))
        for face, out in ((ro * math.cos((a1 - a0) / 2), nrm), (ri * math.cos((a1 - a0) / 2), -nrm)):
            p = Vector((x1, oy, 0)) + nrm * face
            lancet("E", (tuple(p), tuple(along), tuple(out)), 0.5, wb, wsp)
        mid = lambda a, r_: (x1 + r_ * math.cos(a), oy + r_ * math.sin(a))
        for pts in merlon_parts(mid(a0, (ri + ro) / 2), mid(a1, (ri + ro) / 2), CW, NH):
            c.hull("E", pts, PALE)
        c.crown_run("E", mid(a0, (ri + ro) / 2), mid(a1, (ri + ro) / 2), tuple(nrm), thick=CW)
        if k % 2 == 0:                                            # blade buttresses round the apse
            pb = Vector((x1 + ro * math.cos(a0), oy + ro * math.sin(a0), 0))
            n = Vector((math.cos(a0), math.sin(a0), 0))
            t = Vector((-math.sin(a0), math.cos(a0), 0))
            zb = 0.68 * NH
            c.hull("E", [pb + t * dt + n * dn + Vector((0, 0, z)) for dt in (-0.6, 0.6) for dn in (-0.3, 2.0)
                         for z in (0, zb)], PALE)
            blade = [(-0.3, zb, 0.6), (2.0, zb, 0.6), (2.4, zb + 0.15 * NH, 0.25), (-0.3, NH + 0.4, 0.25)]
            c.hull("E", [pb + n * dn + t * (sg * w) + Vector((0, 0, z)) for dn, z, w in blade for sg in (-1, 1)], PALE)
    c.roofs.cone(x1, oy, ro + 0.2, NH, NH + rise, ROOF, sides=18)
    c.roofs.cyl(x1, oy, 1.4, NH + rise - 3, NH + rise + 0.2 * SH, PALE, sides=8)
    c.roofs.cone(x1, oy, 1.7, NH + rise + 0.2 * SH, NH + rise + 0.75 * SH, ROOF, sides=8)

    # West: two towers, square on the nave side and chamfered outside, battlemented, a pinnacle
    # at each corner, an octagonal belfry and a spire; between them the screen wall.
    def shift(face, s):
        o, al, out = face
        return (tuple(Vector(o) + Vector(al) * s), al, out)

    for side in (-1, 1):
        yi, yo = oy + side * hn, oy + side * (hn + TOWER)
        yc, xc = (yi + yo) / 2, (fx0 + ox) / 2
        plan = [(fx0, yi), (ox, yi), (ox, yo - side * CHAMFER), (ox - CHAMFER, yo), (fx0 + CHAMFER, yo), (fx0, yo - side * CHAMFER)]
        c.hull("W", [(x, y, z) for x, y in plan for z in (0, TH)], PALE)
        for p, q in zip(plan, plan[1:] + plan[:1]):
            if p[1] == yi and q[1] == yi:
                continue                                          # the nave side: under the roof
            mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
            inward = Vector((xc - mx, yc - my, 0)).normalized() * 0.25
            battlement("W", (p[0] + inward.x, p[1] + inward.y), (q[0] + inward.x, q[1] + inward.y), 0.5, TH)
        corners = [(fx0, yi), (ox, yi), (ox - CHAMFER / 2, yo - side * CHAMFER / 2), (fx0 + CHAMFER / 2, yo - side * CHAMFER / 2)]
        for px, py in corners:                                    # the inner corners and the chamfers' middles
            q = Vector((px, py, 0)) + (Vector((xc, yc, 0)) - Vector((px, py, 0))).normalized() * 0.85
            c.box("W", q.x - 0.6, q.y - 0.6, TH, q.x + 0.6, q.y + 0.6, TH + 0.9, PALE)   # its foot, merlon high
            pinnacle("W", q.x, q.y, TH + 0.9)
        front = ((fx0, oy + side * (hn + (TOWER - CHAMFER) / 2), 0), (0, 1, 0), (-1, 0, 0))
        outer = ((xc, yo, 0), (1, 0, 0), (0, side, 0))
        c.arch_frame("W", front, 1.4, 1.9, 4.0, 0.0, 0.3, PALE)  # a door in the base
        c.arch_panel("W", front, 1.4, 4.0, 0.0, DOOR)
        for tier in (0.36, 0.66):                                 # dark slits, two tiers
            for face in (front, outer):
                for s in (-1.4, 1.4):
                    f = shift(face, s)
                    c.arch_panel("W", f, 0.4, TH * tier + 2.6, TH * tier, DARK)
        ring = lambda r, z: [Vector((xc + r * math.cos(2 * math.pi * (k + 0.5) / 8), yc + r * math.sin(2 * math.pi * (k + 0.5) / 8), z))
                             for k in range(8)]
        br = 0.4 * TOWER
        top = TH + BH
        c.hull("W", ring(br, TH) + ring(br, top), PALE)
        rf = br * math.cos(math.pi / 8)
        for k in range(8):
            phi = 2 * math.pi * k / 8
            n = Vector((math.cos(phi), math.sin(phi), 0))
            al = Vector((-math.sin(phi), math.cos(phi), 0))
            c.arch_panel("W", (tuple(Vector((xc, yc, 0)) + n * rf), tuple(al), tuple(n)), 0.5, top - 2.2, top - 0.55 * BH, DARK)
        c.hull("W", ring(br + 0.3, top) + ring(br + 0.3, top + 0.5), PALE)
        c.hull("W", ring(br + 0.1, top + 0.5) + [Vector((xc, yc, top + SH))], ROOF)
        c.crown_run("W", (fx0, oy + side * (hn + 0.25)), (ox, oy + side * (hn + 0.25)), (0, -side), thick=0.5)

    # The screen wall, its gable, the great window with the rose, and the portal.
    face = ((fx0, oy, 0), (0, 1, 0), (-1, 0, 0))
    pw = P["portal_w"] / 2
    PH = pw + 2.1
    T = arch_apex(pw + 1.5, P["portal_h"]) + 0.8
    srise = hn * math.tan(math.radians(screen_pitch))
    c.box("W", fx0, oy - hn, 0, fx0 + WT * 2, oy - PH, SCH, PALE)
    c.box("W", fx0, oy + PH, 0, fx0 + WT * 2, oy + hn, SCH, PALE)
    c.box("W", fx0, oy - PH, T, fx0 + WT * 2, oy + PH, SCH, PALE)
    c.hull("W", [(x, y, z) for x in (fx0, fx0 + WT * 2) for y, z in ((oy - hn, SCH), (oy + hn, SCH), (oy, SCH + srise))], PALE)
    pinnacle("W", fx0 + 0.25, oy, SCH + srise - 0.8, half=0.5)
    for sy in (-1, 1):
        c.crown_run("W", (fx0 + WT, oy + sy * hn), (fx0 + WT, oy + sy * PH), (-1, 0), thick=WT * 2)
    c.roofs.gable_roof(fx0 + WT * 2, ox, oy - hn, oy + hn, NH, rise, ROOF, over=0.0)
    ww = P["window_w"]
    wb2 = T + 3.8                                                 # just over the portal's gable
    ws2 = max(SCH + 2.0, wb2 + 2.2)
    c.arch_frame("W", face, ww, ww + 0.4, ws2, 0.0, 0.3, PALE, base=wb2)
    c.arch_panel("W", face, ww, ws2, wb2, GLASS)
    for s in (-ww / 2, 0.0, ww / 2):
        c.box("W", fx0 - 0.15, oy + s - 0.13, wb2, fx0, oy + s + 0.13, ws2 + 0.4, PALE)
    for s in (-ww / 2, ww / 2):
        c.arch_frame("W", shift(face, s), ww / 2 - 0.25, ww / 2, ws2 + 0.4, 0.0, 0.15, PALE, base=ws2 + 0.4)
    rz, rr = ws2 + 1.07 * ww, 0.36 * ww
    for k in range(12):
        a0, a1 = 2 * math.pi * k / 12, 2 * math.pi * (k + 1) / 12
        c.hull("W", [(fx0 - d, oy + r * math.cos(a), rz + r * math.sin(a)) for a in (a0, a1) for r in (rr - 0.3, rr)
                     for d in (0.0, 0.2)], PALE)
    for k in range(6):
        a = math.pi * k / 6
        c.hull("W", [(fx0 - d, oy + r * math.cos(a) + e * math.sin(a), rz + r * math.sin(a) - e * math.cos(a))
                     for r in (-rr, rr) for e in (-0.1, 0.1) for d in (0.0, 0.15)], PALE)
    c.arch_spandrel("W", face, pw + 1.5, P["portal_h"], T, PH, -WT * 2, 2.0, PALE)
    for k in range(3):
        c.arch_frame("W", face, pw + 0.5 * k, pw + 0.5 * (k + 1), P["portal_h"], -WT * 2, 0.7 + 0.6 * k, PALE)
    c.hull("W", [(fx0 - d, oy + y, z) for d in (-WT * 2, 2.0) for y, z in ((-PH, T), (PH, T), (0, T + 3.5))], PALE)
    for sy in (-1, 1):
        y = oy + sy * (PH + 0.5)
        c.box("W", fx0 - 1.9, y - 0.5, 0, fx0 - 0.9, y + 0.5, T + 0.5, PALE)
        pinnacle("W", fx0 - 1.4, y, T + 0.5, half=0.5)
    c.base.box(fx0 - 2.0, oy - PH, 0, fx0, oy + PH, zf, FLOOR)
    c.base.box(fx0 - 5.0, oy - PH - 2, 0, fx0 - 2.0, oy + PH + 2, zf / 3, PALE)
    c.base.box(fx0 - 3.5, oy - PH - 2, 0, fx0 - 2.0, oy + PH + 2, zf * 2 / 3, PALE)
    return c.finish(prefix, smooth=50)


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


def church_states(oy=0.0, painted=True, make=None, label="", table=None):
    """The church in its three states in a row. make(ox, oy, prefix) builds one (default: the
    great church); label goes into the objects' names; table is its PAINT table."""
    import bpy
    make = make or (lambda ox, oy_, prefix: great_church(ox, oy_, prefix=prefix))
    objs = []
    for i, (name, state, cut) in enumerate(STATES):
        layers = make(SPACING * i, oy, f"Church{label}{name}")
        for part, o in layers.items():
            if shows(part, state, cut):
                objs.append(o)
            else:
                bpy.data.objects.remove(o)
    if painted:
        paint(objs, table or PAINT)
    return objs


# The plain church wears one strong brick on all its stone (Varrock's way), the same roofs and glass.
PAINT_PLAIN = {"Gravestone": ("ChurchBrick", 2, 2), "Ashgrey": ("ChurchSlate", 2, 1),
               "Blood": ("ChurchGlass", 1, 1), "CryptFloor": ("ChurchFloor", 4, 4)}
PAINT_B = dict(PAINT_PLAIN, Gravestone=("CruorStone", 2, 2))   # Cruor's pale stone; red only in the glass
ROWS = [("", None, PAINT, 0.0),                                     # A: the great church as polished
        ("B", PLAIN_B, PAINT_B, -130.0),                            # B: Varrock rules, same heights, pale stone
        ("C", PLAIN_C, PAINT_PLAIN, -260.0)]                        # C: Varrock rules, lower


def paint(objs, table=None):
    """Swap the flat colours in the table (PAINT by default) for their painted textures (shown
    as they are, point filtered), scaling each face's texture coordinates (in tiles, from
    rth_art.box_uvs) to the texture's repeat."""
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
    church = []
    for label, params, table, oy in ROWS:
        make = None if params is None else (lambda ox, oy_, prefix, P=params: plain_church(ox, oy_, P, prefix=prefix))
        church += church_states(oy, make=make, label=label, table=table)
    keep = castle(SPACING - 13, 60)
    return {"GreatChurch": A.report(church), "Castle": A.report([keep])}


if __name__ == "__main__":
    result = build()
    cam = A.preview_rig(focus=(SPACING + 21, -130, 10), distance=420, pitch=40, yaw=-10, ground=200, human_at=(-20, 0))
    cam.data.lens = 32
    cam.data.clip_end = 3000
    import bpy
    bpy.data.objects["PreviewGround"].location = (SPACING, -130, 0)   # under all the rows
    # Players for scale, in each copy: on the steps, in the nave, in both aisles, on the dais.
    scale_figures([(SPACING * i + x, oy + y, z) for _, _, _, oy in ROWS for i in range(len(STATES))
                   for x, y, z in ((-16, 2, 0.4), (20, 2, FLOOR_Z), (28, 15, FLOOR_Z), (12, -15, FLOOR_Z), (52, 0, FLOOR_Z + 0.6))])
