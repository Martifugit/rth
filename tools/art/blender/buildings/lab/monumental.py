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
MATS = ["Gravestone", "Ashgrey", "Sable", "SynodRed", "Shingle", "Iron"]
PALE, ROOF, DARK, RED, CASTLE, METAL = range(len(MATS))


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

BAY = 3.0                 # one bay of the nave, in tiles
BAYS = 7
NAVE_W = 8.0              # nave width (y)
NAVE_H = 9.0              # nave wall height
AISLE_W = 4.0
AISLE_H = 4.6
TOWER = 5.0               # west towers' side
TOWER_H = 18.0
SPIRE_H = 11.0


def great_church(ox, oy):
    """West front at x = ox, the nave running east (+x); the nave's south wall at y = oy."""
    b = Build()
    L = BAY * BAYS
    y0, y1 = oy, oy + NAVE_W
    rise = (NAVE_W / 2) * math.tan(math.radians(55))
    # Nave and its roof.
    b.box(ox, y0, 0, ox + L, y1, NAVE_H, PALE)
    b.gable_roof(ox, ox + L, y0, y1, NAVE_H, rise, ROOF)
    for side, ya, yb, face, out in ((-1, y0 - AISLE_W, y0, y0 - AISLE_W, -1), (1, y1, y1 + AISLE_W, y1 + AISLE_W, 1)):
        # Aisle: a lower block with a lean-to roof up against the nave.
        b.box(ox, ya, 0, ox + L, yb, AISLE_H, PALE)
        inner = y0 if side < 0 else y1
        b.hull([(x, face - out * -0.3, AISLE_H) for x in (ox - 0.2, ox + L + 0.2)] +
               [(x, inner, AISLE_H + 2.0) for x in (ox - 0.2, ox + L + 0.2)] +
               [(x, face - out * -0.3, AISLE_H + 0.15) for x in (ox - 0.2, ox + L + 0.2)], ROOF)
        for i in range(BAYS):
            xm = ox + BAY * (i + 0.5)
            # Aisle windows and clerestory lancets in church glass.
            b.panel((ox, face, 0), (1, 0, 0), (0, out, 0), xm - ox - 0.45, xm - ox + 0.45, 1.2, 3.2, RED, apex=3.8)
            b.panel((ox, inner, 0), (1, 0, 0), (0, out, 0), xm - ox - 0.5, xm - ox + 0.5, AISLE_H + 2.4, NAVE_H - 1.6,
                    RED, apex=NAVE_H - 0.6)
        for i in range(BAYS + 1):
            x = ox + BAY * i
            # Buttress pier on the aisle wall, its pinnacle, and the flying buttress to the nave.
            far = face + out * 1.1
            b.box(x - 0.35, face, 0, x + 0.35, far, AISLE_H + 1.4, PALE)
            b.cone(x, (face + far) / 2, 0.42, AISLE_H + 1.4, AISLE_H + 3.4, PALE, sides=4)
            b.hull([(x + dx, face + out * 0.2, AISLE_H + 0.9 + dz) for dx in (-0.2, 0.2) for dz in (0, 0.5)] +
                   [(x + dx, inner, NAVE_H - 1.8 + dz) for dx in (-0.2, 0.2) for dz in (0, 0.6)], PALE)
    # The apse: a round east end with its own cone roof.
    ax, ay, r = ox + L, oy + NAVE_W / 2, NAVE_W / 2 + 0.6
    b.cyl(ax, ay, r, 0, NAVE_H, PALE, sides=10)
    b.cone(ax, ay, r + 0.3, NAVE_H, NAVE_H + rise, ROOF, sides=10)
    for k in range(5):                                            # tall lancets round the apse
        a = math.radians(-60 + 30 * k)
        p = Vector((ax + r * math.cos(a), ay + r * math.sin(a), 0))
        along = Vector((-math.sin(a), math.cos(a), 0))
        b.panel(p, along, (math.cos(a), math.sin(a), 0), -0.4, 0.4, 2.0, NAVE_H - 2.0, RED, apex=NAVE_H - 1.2)
    # A slender spire (flèche) over the crossing.
    fx = ox + BAY * 4
    b.cyl(fx, ay, 0.9, NAVE_H + rise - 1.0, NAVE_H + rise + 2.0, PALE, sides=8)
    b.cone(fx, ay, 1.0, NAVE_H + rise + 2.0, NAVE_H + rise + 8.0, ROOF, sides=8)
    # The west front: two towers, the gable wall between them with a rose window and a portal.
    for ty in (y0 - AISLE_W - 0.5, y1 + AISLE_W + 0.5 - TOWER):
        tx = ox - TOWER + 1.0
        b.box(tx, ty, 0, tx + TOWER, ty + TOWER, TOWER_H, PALE)
        cx, cy = tx + TOWER / 2, ty + TOWER / 2
        for zz in (NAVE_H + 0.5, TOWER_H - 4.2):
            b.panel((tx, ty, 0), (0, 1, 0), (-1, 0, 0), 1.6, TOWER - 1.6, zz, zz + 2.6, DARK, apex=zz + 3.4)
        b.merlons((tx, ty), (tx, ty + TOWER), TOWER_H, 0.6, 0.4, PALE, outward=(1, 0, 0))
        for px, py in ((tx, ty), (tx + TOWER, ty), (tx, ty + TOWER), (tx + TOWER, ty + TOWER)):
            b.cone(px, py, 0.45, TOWER_H, TOWER_H + 2.5, PALE, sides=4)       # corner pinnacles
        b.cone(cx, cy, TOWER / 2 - 0.3, TOWER_H, TOWER_H + SPIRE_H, ROOF, sides=8)
    fx0 = ox - TOWER + 1.0                                        # the front, flush with the towers
    b.box(fx0, y0 - 0.5, 0, ox, y1 + 0.5, NAVE_H, PALE)
    b.hull([(x, y, NAVE_H) for x in (fx0, ox) for y in (y0 - 0.5, y1 + 0.5)] +
           [(x, oy + NAVE_W / 2, NAVE_H + rise + 0.6) for x in (fx0, ox)], PALE)
    rose = Vector((fx0, oy + NAVE_W / 2, NAVE_H + 1.4))
    ex = Vector((1, 0, 0))
    A.loft(b.bm, [(tuple(rose - ex * 0.2), 2.4, 2.4), (tuple(rose + ex * 0.1), 2.4, 2.4)],
           sides=16, ref=(0, 0, 1), mats=PALE)                        # the rose's stone ring
    A.loft(b.bm, [(tuple(rose - ex * 0.26), 0, 0), (tuple(rose - ex * 0.25), 2.0, 2.0), (tuple(rose), 2.0, 2.0)],
           sides=16, ref=(0, 0, 1), mats=RED)                         # its glass
    # A deep portal: stepped arches round a dark doorway.
    out = 0.0
    for w, h, depth, m in ((2.4, 4.6, 0.5, PALE), (1.9, 4.1, 0.2, ROOF), (1.4, 3.6, 0.08, DARK)):
        b.panel((fx0 - out, oy + NAVE_W / 2, 0), (0, 1, 0), (-1, 0, 0), -w, w, 0, h, m, apex=h + w * 0.9, depth=depth)
        out += depth                                              # each arch stands on the one behind
    b.box(fx0 - 1.2, oy - 0.5, 0, fx0 - 0.3, y1 + 0.5, 0.3, PALE)  # the steps
    # Banners down the towers' fronts.
    for ty in (y0 - AISLE_W - 0.5 + TOWER / 2, y1 + AISLE_W + 0.5 - TOWER / 2):
        b.box(ox - TOWER + 0.95, ty - 0.45, NAVE_H - 4.5, ox - TOWER + 0.9, ty + 0.45, NAVE_H - 0.2, RED)
    A.finish(b.bm)
    return A.mesh_from("GreatChurch", b.bm, MATS, coll="Monumental")


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


def build():
    A.reset()
    church = great_church(0, 0)
    keep = castle(-12, -48)
    return {"GreatChurch": A.report([church]), "Castle": A.report([keep])}


if __name__ == "__main__":
    result = build()
    cam = A.preview_rig(focus=(4, -14, 6), distance=75, pitch=32, yaw=-120, ground=56, human_at=(-9, 4))
