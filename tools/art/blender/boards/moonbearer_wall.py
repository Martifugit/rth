r"""The Moon-Bearer surfacing, study (2026-11-01): one great dark fissure in the shell's wall (where the
body is: two pale eyes in the dark), and the barbed legs (boards/moonbearer_leg.py) punched straight
through the stone round it from inside, cracks radiating from each puncture, the light leaking out.

    import runpy; ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\moonbearer_wall.py"); ns["run"]()
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if not __name__.startswith("boards."):
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "boards")]:
        del sys.modules[_m]

import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

from boards import moonbearer_leg as L  # noqa: E402

COLOURS = L.COLOURS
SHELL, MEMBRANE, CRUST, BARB, GLOW = range(5)
FRONT, BACK = -0.3, 0.8                         # the wall's faces (y)
WIDTH, HEIGHT = 26.0, 11.0

# the legs: (base behind the wall (x, y, z), heading out (x, y), cracks round the puncture (angle deg, length))
REACH = [0, 5, -32, -46, -26, -10]               # out and up to a high knee in the room, then down to the floor
LEGS = [((-3.2, 1.1, 4.6), (-0.45, -0.89), [(20, 1.6), (95, 2.2), (160, 1.2), (215, 1.8), (300, 1.4)]),
        ((3.4, 1.1, 5.4), (0.4, -0.92), [(-15, 1.5), (70, 1.9), (140, 1.3), (230, 2.0), (330, 1.1)]),
        ((-6.8, 1.1, 2.4), (-0.3, -0.95), [(10, 1.2), (120, 1.6), (200, 1.0), (270, 1.4)]),
        ((7.0, 1.1, 2.9), (0.25, -0.97), [(40, 1.3), (110, 1.1), (190, 1.5), (300, 1.2)])]
# the fissure: a jagged slot up the middle, (x, z) points bottom to top, its half width at each
FISSURE = [((0.2, 0.0), 1.15), ((-0.35, 1.6), 1.25), ((0.45, 3.3), 1.05), ((-0.25, 5.0), 0.85),
           ((0.35, 6.6), 0.55), ((0.0, 7.9), 0.12)]


def prism(points, y0=-2.0, y1=2.5):
    """A cutter: a polygon in the wall's plane ((x, z) points, any order round it) pushed through."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in points]
    b = [bm.verts.new((x, y1, z)) for x, z in points]
    n = len(points)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def fissure_outline():
    left = [(x - w, z) for (x, z), w in FISSURE]
    right = [(x + w * (0.9 + 0.1 * math.sin(i * 2.3)), z) for i, ((x, z), w) in enumerate(FISSURE)]
    return left + list(reversed(right))


def crack(cx, cz, angle, length, width=0.12):
    """A tapering wedge from a puncture outward, one kink partway."""
    a = math.radians(angle)
    d = (math.cos(a), math.sin(a))
    nrm = (-d[1], d[0])
    kink = (cx + d[0] * length * 0.5 + nrm[0] * length * 0.12, cz + d[1] * length * 0.5 + nrm[1] * length * 0.12)
    end = (cx + d[0] * length, cz + d[1] * length)
    return [(cx + nrm[0] * width, cz + nrm[1] * width), (kink[0] + nrm[0] * width * 0.5, kink[1] + nrm[1] * width * 0.5),
            end, (kink[0] - nrm[0] * width * 0.5, kink[1] - nrm[1] * width * 0.5),
            (cx - nrm[0] * width, cz - nrm[1] * width)]


def hole(cx, cz, r, n=7):
    """A jagged puncture round where a leg comes through."""
    return [(cx + r * (1 + 0.25 * ((k * 3) % 2)) * math.cos(2 * math.pi * k / n),
             cz + r * (1 + 0.25 * ((k * 3) % 2)) * math.sin(2 * math.pi * k / n)) for k in range(n)]


def crossing(pts):
    """Where the leg's chain passes the wall's front face (x, z), and the segment's radius there."""
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        if (a.y - FRONT) * (b.y - FRONT) <= 0 and a.y != b.y:
            t = (FRONT - a.y) / (b.y - a.y)
            p = a.lerp(b, t)
            return p.x, p.z, L.SEGMENTS[i][2]
    return pts[0].x, pts[0].z, 0.3


def run(y=80.0):
    import bpy
    import rth_art as A
    for o in [o for o in bpy.data.objects if o.name.startswith("MoonBearerWall")]:
        m = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if m:
            bpy.data.meshes.remove(m)
    cutters = [prism(fissure_outline())]
    stats = {}
    legs = bmesh.new()
    for base, (hx, hy), cracks in LEGS:
        u = Vector((hx, hy, 0.0)).normalized()
        v = Vector((0, 0, 1))
        turns = REACH
        start = L.reaching_start(Vector(base), u, v, turns)
        pts, _ = L.build_leg(legs, Vector(base), u, v, start, turns)
        cx, cz, r = crossing(pts)
        cutters.append(prism(hole(cx, cz, r * 1.9)))
        for ang, ln in cracks:                     # from the hole's edge outward
            a_ = math.radians(ang)
            cutters.append(prism(crack(cx + math.cos(a_) * r * 1.2, cz + math.sin(a_) * r * 1.2, ang, ln, 0.17)))
    stats["leg_faces"] = len(legs.faces)
    # the wall, the cutters taken out of it
    wb = bmesh.new()
    vs = [wb.verts.new((x, yy, z)) for x in (-WIDTH / 2, WIDTH / 2) for yy in (FRONT, BACK) for z in (0, HEIGHT)]
    for q in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
        wb.faces.new([vs[i] for i in q]).material_index = CRUST
    bmesh.ops.recalc_face_normals(wb, faces=wb.faces[:])
    w = A.mesh_from("MoonBearerWall_Wall", wb, COLOURS, coll="MoonBearerWall")
    for i, cb in enumerate(cutters):                # one cut at a time: exact booleans on many overlapping
        c = A.mesh_from("MoonBearerWall_cut%d" % i, cb, COLOURS, coll="MoonBearerWall")   # cutters fail silently
        mod = w.modifiers.new("Cut", "BOOLEAN")
        mod.object, mod.operation, mod.solver = c, "DIFFERENCE", "EXACT"
        bpy.context.view_layer.update()
        me = bpy.data.meshes.new_from_object(w.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        w.modifiers.clear()
        old = w.data
        w.data = me
        bpy.data.meshes.remove(old)
        m = c.data
        bpy.data.objects.remove(c)
        bpy.data.meshes.remove(m)
    # behind: the glow everywhere but the fissure, which is dark, with two pale eyes in it
    gb = bmesh.new()
    q = [gb.verts.new((x, BACK + 1.2, z)) for x, z in ((-WIDTH / 2, 0), (WIDTH / 2, 0), (WIDTH / 2, HEIGHT), (-WIDTH / 2, HEIGHT))]
    gb.faces.new(q).material_index = GLOW
    db = bmesh.new()
    q = [db.verts.new((x, BACK + 0.9, z)) for x, z in ((-2.0, 0), (2.0, 0), (2.0, 8.2), (-2.0, 8.2))]
    db.faces.new(q).material_index = MEMBRANE
    for ex, ez in ((-0.45, 3.9), (0.5, 4.15)):          # the eyes: low faceted domes, looking out
        c = Vector((ex, BACK + 0.6, ez))
        ring = [db.verts.new(c + Vector((0.28 * math.cos(a), 0, 0.22 * math.sin(a))))
                for a in (2 * math.pi * k / 8 for k in range(8))]
        tip = db.verts.new(c + Vector((0, -0.25, 0)))
        for k in range(8):
            db.faces.new((ring[k], ring[(k + 1) % 8], tip)).material_index = GLOW
    gl = A.mesh_from("MoonBearerWall_Glow", gb, COLOURS, coll="MoonBearerWall")
    dk = A.mesh_from("MoonBearerWall_Dark", db, COLOURS, coll="MoonBearerWall")
    lg = A.mesh_from("MoonBearerWall_Legs", legs, COLOURS, coll="MoonBearerWall", smooth_angle=25)
    for o in (w, gl, dk, lg):
        o.location = (0, y, 0)
    return stats
