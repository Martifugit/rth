"""Monumental kits, first pass: massing only, in flat colours (no textures yet).

Two kinds of building that are nothing like the village houses:

  - Cruor's great church: a nave of repeating bays (aisles, buttresses flying over the aisle
    roofs, clerestory lancets), a twin-towered west front with spires, a rose window and a deep
    portal, and a round apse at the east end. It is meant to tower over everything: it breaks the
    usual height limits on purpose (docs/world-scale.md), and needs open ground round it.
  - A castle: a curtain wall with a walk and crenels, round corner towers, an open gatehouse
    between two drum towers (no roof over the gateway), and a tall keep with corner turrets.

Both are built from repeating parts (a bay, a wall run, a tower), so they are kits: a longer
nave is more bays, a bigger castle more runs and towers. 1 unit = 1 tile; Z up.

Run in Blender (MCP): runpy.run_path(".../lab/monumental.py", run_name="__main__")."""
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
        o, a, n = Vector(origin), Vector(along).normalized(), Vector(outward).normalized()
        up = Vector((0, 0, 1))
        outline = [(s0, z0), (s1, z0), (s1, z1), (s0, z1)]
        if apex:
            outline = [(s0, z0), (s1, z0), (s1, z1), ((s0 + s1) / 2, apex), (s0, z1)]
        self.hull([o + a * s + up * z + n * d for s, z in outline for d in (0.0, depth)], m)

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


# ---------------------------------------------------------------- the great church

# Planned from the inside: clear floor in tiles (the player is about 2 tall), then heights from
# Gothic ratios, compressed for the camera (real naves are nearer 3x their width).
NAVE_W = 12.0             # clear width between the arcade piers
AISLE_W = 6.0             # clear width of each aisle
BAY = 6.0                 # one bay: about the aisle's width
BAYS = 6
WALL = 1.0                # wall thickness
NAVE_H = 18.0             # nave walls: 1.5 x the nave's width
AISLE_H = 9.0             # aisles: half the nave
ARCADE_H = 7.0            # where the arcade's arches spring
TOWER = 10.0              # west towers' side
TOWER_H = 34.0            # about twice the nave walls
SPIRE_H = 16.0
PORTAL_W, PORTAL_H = 5.0, 9.0
PITCH = 55.0


def great_church(ox, oy):
    """West front at x = ox, the nave running east (+x), centred on y = oy. Returns (shell, roofs):
    the roofs are their own object, so a cutaway can hide them."""
    b, r = Build(), Build()
    L = BAY * BAYS
    hn = NAVE_W / 2                                               # pier line
    ha = hn + AISLE_W                                             # aisle wall's inner face
    rise = (hn + WALL) * math.tan(math.radians(PITCH))
    x0, x1 = ox, ox + L
    # Floor.
    b.box(x0 - TOWER, oy - ha - WALL, 0, x1 + hn + WALL, oy + ha + WALL, 0.05, FLOOR)

    for side in (-1, 1):
        y_pier, y_wall = oy + side * hn, oy + side * (ha + WALL / 2)
        # Aisle wall, with its windows (glass on both faces).
        b.box(x0, y_wall - WALL / 2, 0, x1, y_wall + WALL / 2, AISLE_H, PALE)
        for i in range(BAYS):
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_wall + side * WALL / 2, side), (y_wall - side * WALL / 2, -side)):
                b.panel((x0, face, 0), (1, 0, 0), (0, out, 0), xm - x0 - 1.0, xm - x0 + 1.0, 2.2, 6.0, RED,
                        apex=7.6, depth=0.04)
        # The arcade: piers, pointed arches between them, the clerestory wall above.
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            b.box(xp - 0.6, y_pier - 0.6, 0, xp + 0.6, y_pier + 0.6, AISLE_H, PALE)
        for i in range(BAYS):
            xa, xb = x0 + BAY * i + 0.6, x0 + BAY * (i + 1) - 0.6
            xm = (xa + xb) / 2
            for xs, xe in ((xa, xm), (xb, xm)):                    # spandrels make the pointed arch
                b.hull([(x, y_pier + dy, z) for x, z in ((xs, ARCADE_H), (xs, AISLE_H), (xe, AISLE_H))
                        for dy in (-WALL / 2, WALL / 2)], PALE)
        b.box(x0, y_pier - WALL / 2, AISLE_H, x1, y_pier + WALL / 2, NAVE_H, PALE)
        for i in range(BAYS):                                     # clerestory lancets, both faces
            xm = x0 + BAY * (i + 0.5)
            for face, out in ((y_pier + side * WALL / 2, side), (y_pier - side * WALL / 2, -side)):
                b.panel((x0, face, 0), (1, 0, 0), (0, out, 0), xm - x0 - 1.1, xm - x0 + 1.1, AISLE_H + 3.8,
                        NAVE_H - 3.0, RED, apex=NAVE_H - 1.2, depth=0.04)
        # Aisle roof: a lean-to from the aisle wall up to the clerestory.
        r.hull([(x, y_wall + side * (WALL / 2 + 0.4), z) for x in (x0, x1) for z in (AISLE_H, AISLE_H + 0.3)] +
               [(x, y_pier + side * WALL / 2, z) for x in (x0, x1) for z in (AISLE_H + 3.0, AISLE_H + 3.3)], ROOF)
        # Buttresses: piers on the aisle wall with tall pinnacles, flying across to the clerestory.
        for i in range(BAYS + 1):
            xp = x0 + BAY * i
            far = y_wall + side * (WALL / 2 + 2.2)
            b.box(xp - 0.6, y_wall, 0, xp + 0.6, far, AISLE_H + 3.5, PALE)
            b.hull([(xp + dx, y, AISLE_H + 3.5) for dx in (-0.6, 0.6) for y in (y_wall, far)] +
                   [(xp + dx, y_wall + side * 0.4, AISLE_H + 4.4) for dx in (-0.6, 0.6)], PALE)
            pc = (y_wall + far) / 2 + side * 0.4
            b.cone(xp, pc, 0.55, AISLE_H + 4.0, AISLE_H + 9.0, PALE, sides=4)
            for zf, zt in ((AISLE_H + 2.6, NAVE_H - 3.0),):        # the flying arm
                b.hull([(xp + dx, far - side * 0.6, zf + dz) for dx in (-0.35, 0.35) for dz in (0, 0.8)] +
                       [(xp + dx, y_pier + side * WALL / 2, zt + dz) for dx in (-0.35, 0.35) for dz in (0, 0.8)], PALE)
            b.cone(xp, y_pier + side * (WALL / 2 + 0.3), 0.4, NAVE_H, NAVE_H + 3.5, PALE, sides=4)
    # Nave roof.
    r.gable_roof(x0, x1, oy - hn - WALL / 2, oy + hn + WALL / 2, NAVE_H, rise, ROOF, over=0.4)
    # East: the aisles close; the nave ends in a round apse.
    for side in (-1, 1):
        b.box(x1, oy + side * hn, 0, x1 + WALL, oy + side * (ha + WALL), AISLE_H, PALE)
    segs = 9
    for k in range(segs):
        a0, a1 = math.pi * (-0.5 + k / segs), math.pi * (-0.5 + (k + 1) / segs)
        pts = []
        for a in (a0, a1):
            for rr in (hn + WALL / 2 - WALL / 2, hn + WALL):
                for z in (0, NAVE_H):
                    pts.append((x1 + rr * math.cos(a), oy + rr * math.sin(a), z))
        b.hull(pts, PALE)
        am = (a0 + a1) / 2
        p = Vector((x1 + (hn + WALL) * math.cos(am), oy + (hn + WALL) * math.sin(am), 0))
        along = Vector((-math.sin(am), math.cos(am), 0))
        b.panel(p, along, (math.cos(am), math.sin(am), 0), -0.9, 0.9, 3.0, NAVE_H - 3.0, RED, apex=NAVE_H - 1.4, depth=0.04)
        if k % 2 == 0:                                            # buttresses round the apse
            pb = Vector((x1 + (hn + WALL) * math.cos(a0), oy + (hn + WALL) * math.sin(a0), 0))
            n = Vector((math.cos(a0), math.sin(a0), 0))
            t = Vector((-math.sin(a0), math.cos(a0), 0))
            b.hull([pb + t * dt + n * dn + Vector((0, 0, z)) for dt in (-0.6, 0.6) for dn in (0, 2.0)
                    for z in (0, NAVE_H - 4)], PALE)
            c = pb + n * 1.2
            b.cone(c.x, c.y, 0.55, NAVE_H - 4, NAVE_H + 1, PALE, sides=4)
    r.cone(x1, oy, hn + WALL + 0.4, NAVE_H, NAVE_H + rise, ROOF, sides=18)
    # A slender spire over the crossing (two bays in from the apse).
    fx = x0 + BAY * (BAYS - 2)
    r.cyl(fx, oy, 1.4, NAVE_H + rise - 2, NAVE_H + rise + 4, PALE, sides=8)
    r.cone(fx, oy, 1.6, NAVE_H + rise + 4, NAVE_H + rise + 16, ROOF, sides=8)

    # The west front: two towers flanking a porch hall, a gable wall with the rose and the portal.
    fx0 = ox - TOWER                                              # the front face
    for side in (-1, 1):
        ty0 = oy + side * hn
        ty1 = oy + side * (hn + TOWER)
        ya, yb = min(ty0, ty1), max(ty0, ty1)
        b.box(fx0, ya, 0, ox, yb, TOWER_H, PALE)
        for zz in (NAVE_H * 0.5, NAVE_H, TOWER_H - 1.0):          # ledges marking the tiers
            b.box(fx0 - 0.3, ya - 0.3, zz - 0.4, ox + 0.3, yb + 0.3, zz, PALE)
        for cx_ in (fx0, ox):                                     # stepped corner buttresses
            for cy_ in (ya, yb):
                for k, (h, w) in enumerate(((NAVE_H * 0.5, 1.6), (NAVE_H, 1.1), (TOWER_H - 4, 0.7))):
                    b.box(cx_ - w, cy_ - w, 0, cx_ + w, cy_ + w, h, PALE)
                b.cone(cx_, cy_, 0.8, TOWER_H - 4, TOWER_H + 4, PALE, sides=4)
        for zz, h in ((NAVE_H + 1.5, 6.0), (NAVE_H * 0.5 + 1.5, 4.5)):   # tiers of openings on the front
            for s0 in (1.8, TOWER - 3.6):
                b.panel((fx0, ya, 0), (0, 1, 0), (-1, 0, 0), s0, s0 + 1.8, zz, zz + h, DARK, apex=zz + h + 1.2)
        b.panel((fx0, ya, 0), (0, 1, 0), (-1, 0, 0), TOWER / 2 - 1.0, TOWER / 2 + 1.0, 2.5, 6.5, RED, apex=8.0)
        b.cone((fx0 + ox) / 2, (ya + yb) / 2, TOWER / 2 - 0.6, TOWER_H, TOWER_H + SPIRE_H, ROOF, sides=8)
    # Porch hall between the towers: its side walls are the towers; the front wall has the portal.
    pw = PORTAL_W / 2
    b.box(fx0, oy - hn, 0, fx0 + WALL, oy - pw, NAVE_H, PALE)
    b.box(fx0, oy + pw, 0, fx0 + WALL, oy + hn, NAVE_H, PALE)
    b.box(fx0, oy - pw, PORTAL_H, fx0 + WALL, oy + pw, NAVE_H, PALE)
    b.hull([(x, y, z) for x in (fx0, fx0 + WALL) for y, z in ((oy - hn, NAVE_H), (oy + hn, NAVE_H), (oy, NAVE_H + rise))], PALE)
    r.gable_roof(fx0 + WALL, ox, oy - hn, oy + hn, NAVE_H, rise, ROOF, over=0.0)
    rose = Vector((fx0, oy, NAVE_H + 1.5))
    ex = Vector((1, 0, 0))
    A.loft(b.bm, [(tuple(rose - ex * 0.4), 4.4, 4.4), (tuple(rose + ex * 0.1), 4.4, 4.4)], sides=20, ref=(0, 0, 1), mats=PALE)
    A.loft(b.bm, [(tuple(rose - ex * 0.46), 0, 0), (tuple(rose - ex * 0.45), 3.8, 3.8), (tuple(rose), 3.8, 3.8)],
           sides=20, ref=(0, 0, 1), mats=RED)
    # The portal, open: stepped jambs either side and pointed hoods over it.
    for k, (w, d) in enumerate(((pw + 1.4, 1.2), (pw + 0.8, 0.8), (pw + 0.3, 0.4))):
        for sy in (-1, 1):
            b.box(fx0 - d, oy + sy * (pw + (w - pw)), 0, fx0, oy + sy * pw, PORTAL_H, PALE if k != 1 else ROOF)
        apex = PORTAL_H + w * 1.1
        for sy in (-1, 1):
            b.hull([(x, y, z) for x in (fx0 - d, fx0) for y, z in ((oy + sy * w, PORTAL_H), (oy + sy * (w - 0.6), PORTAL_H),
                                                                     (oy, apex), (oy, apex - 0.8))], PALE if k != 1 else ROOF)
    b.box(fx0 - 4.0, oy - hn - 1, 0, fx0 - 1.2, oy + hn + 1, 0.4, PALE)  # steps
    b.box(fx0 - 2.6, oy - hn - 1, 0, fx0 - 1.2, oy + hn + 1, 0.8, PALE)
    for sy in (-1, 1):                                            # banners on the towers' fronts
        y = oy + sy * (hn + TOWER / 2)
        b.box(fx0 - 0.4, y - 1.0, NAVE_H * 0.5 - 7, fx0 - 0.35, y + 1.0, NAVE_H * 0.5 - 0.8, RED)
    A.finish(b.bm)
    A.finish(r.bm)
    return (A.mesh_from("GreatChurch", b.bm, MATS, coll="Monumental"),
            A.mesh_from("GreatChurch_Roofs", r.bm, MATS, coll="Monumental"))


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
    church, roofs = great_church(0, 0)
    keep = castle(-20, -70)
    return {"GreatChurch": A.report([church, roofs]), "Castle": A.report([keep])}


if __name__ == "__main__":
    result = build()
    cam = A.preview_rig(focus=(6, 0, 12), distance=95, pitch=28, yaw=-120, ground=80, human_at=(-16, 0))
    cam.data.clip_end = 1000
    # Players for scale: in the portal, in the nave, in an aisle, in the apse.
    scale_figures([(-9.5, 0, 0), (12, 2, 0), (20, -9, 0), (38, 0, 0)])
