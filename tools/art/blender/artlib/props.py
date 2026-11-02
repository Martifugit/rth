"""Helpers shared by the made-thing families (furniture, containers, dressing, stations, lights): a
box by its corners, upright and straight round bars, a plank run, a fish, and make(), which turns a
bmesh into the object.

Every builder faces Blender -Y (its working side or front) and stands on the origin, the middle of
its footprint."""

import math

from mathutils import Vector

import rth_art as A


def make(name, bm, materials, coll="Prop", at=(0, 0, 0), rot=0.0, smooth=None):
    """The finished object: materials by palette name (A.mesh_from runs the grain along every
    wooden part)."""
    A.finish(bm)
    ob = A.mesh_from(name, bm, materials, coll=coll, smooth_angle=smooth)
    ob.location = at
    ob.rotation_euler.z = rot
    return ob


def box(bm, x0, y0, z0, x1, y1, z1, mat):
    """A box between two corners, given in any order."""
    return A.box(bm, (min(x0, x1), min(y0, y1), min(z0, z1)), (max(x0, x1), max(y0, y1), max(z0, z1)), mat)


def cyl(bm, x, y, z0, z1, r0, r1=None, sides=8, mat=0):
    """An upright round part (a post, a pot, a body), r0 at the foot and r1 at the top."""
    r1 = r0 if r1 is None else r1
    return A.loft(bm, [((x, y, z0), r0, r0), ((x, y, z1), r1, r1)], sides=sides, ref=(1, 0, 0), mats=mat)


def rod(bm, a, b, r, mat, sides=6):
    """A straight round bar from a to b."""
    d = Vector(b) - Vector(a)
    ref = (0, 0, 1) if abs(d.normalized().z) < 0.9 else (1, 0, 0)
    return A.loft(bm, [(tuple(a), r, r), (tuple(b), r, r)], sides=sides, ref=ref, mats=mat)


def bar(bm, a, b, w, h, mat, up=(0, 0, 1)):
    """A squared bar from a to b, w across and h deep (a rail, a stretcher, a leaning board)."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = Vector(up)
    if abs(d.dot(u)) > 0.95:
        u = Vector((0, 1, 0)) if abs(d.y) < 0.95 else Vector((1, 0, 0))
    s = d.cross(u).normalized()
    u = s.cross(d).normalized()
    return A.hull(bm, [tuple(p + s * ss + u * uu) for p in (a, b) for ss in (-w / 2, w / 2) for uu in (-h / 2, h / 2)], mat)


def planks(bm, x0, x1, y0, y1, z0, z1, n, mat, gap=0.012, along="x"):
    """A surface of n boards side by side (a table top, a lid, a floor), with a hair of gap between
    them so the boards read; `along` is the direction the boards run."""
    if along == "x":
        w = (y1 - y0) / n
        for k in range(n):
            box(bm, x0, y0 + k * w + gap / 2, z0, x1, y0 + (k + 1) * w - gap / 2, z1, mat)
    else:
        w = (x1 - x0) / n
        for k in range(n):
            box(bm, x0 + k * w + gap / 2, y0, z0, x0 + (k + 1) * w - gap / 2, y1, z1, mat)


def legs(bm, x0, x1, y0, y1, z, w, mat, splay=0.0):
    """Four square legs under a top at height z, set in by w, splayed outwards by `splay` at the foot."""
    for x in (x0 + w, x1 - w):
        for y in (y0 + w, y1 - w):
            sx, sy = math.copysign(splay, x), math.copysign(splay, y)
            A.hull(bm, [(x + dx + sx, y + dy + sy, 0.0) for dx in (-w / 2, w / 2) for dy in (-w / 2, w / 2)] +
                   [(x + dx, y + dy, z) for dx in (-w / 2, w / 2) for dy in (-w / 2, w / 2)], mat)


def arch(cx, z_spring, half, n=6):
    """Points (x, z) of a round arch over cx, springing at z_spring, `half` its half-width."""
    return [(cx + half * math.cos(math.pi * k / n), z_spring + half * math.sin(math.pi * k / n)) for k in range(n + 1)]


def slab_from(bm, outline_xz, y0, y1, mat):
    """A flat slab standing in the XZ plane from an outline (x, z), between y0 and y1: a headstone,
    a board, a sign. The outline must be convex."""
    return A.hull(bm, [(x, y, z) for x, z in outline_xz for y in (y0, y1)], mat)


def fish(bm, centre, heading, mat, scale=1.0):
    """A fish lying flat: a body pointed at both ends and a forked tail, heading (radians) its way."""
    d, s = Vector((math.cos(heading), math.sin(heading), 0)) * scale, Vector((-math.sin(heading), math.cos(heading), 0)) * scale
    c, up = Vector(centre), Vector((0, 0, 1)) * scale
    A.hull(bm, [c + d * 0.17, c - d * 0.13, c + s * 0.05, c - s * 0.05, c + up * 0.035, c - up * 0.025], mat)
    A.hull(bm, [c - d * 0.12, c - d * 0.22 + s * 0.06, c - d * 0.22 - s * 0.06, c - d * 0.13 + up * 0.01], mat)
