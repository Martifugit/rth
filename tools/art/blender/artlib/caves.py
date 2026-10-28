"""Helpers shared by the cave families: rock and stone colours, chunks, blocks, weathering, laying parts, the span and door height."""

import bmesh
import math
import random

from mathutils import Matrix, Vector

import rth_art as A
import artlib.nature as N


ROCK = ["Scree", "Cinder", "Moss"]          # rock faces, undersides in shadow, mossy ledges
STONE = "Stone"                            # dressed stone; a place retints it (objects.json "recolor")


def make(name, bm, materials, at=(0, 0, 0), rot=0.0, smooth=25, finish=True):
    """A part's object, in the Caves collection."""
    return N.make(name, bm, materials, smooth_angle=smooth, at=at, rot=rot, coll="Caves", finish=finish)


def chunk(bm, centre, size, rnd, lean=(0.0, 0.0), sink=0.1, mats=(0, 1, 2), points=20):
    """A faceted rock mass: points scattered over a rounded box (size: half-widths and the
    height), each pushed in or out, the highest flattened into a ledge, wrapped in a hull; lean
    shifts the top. Faces are coloured by which way they face: rock, cinder underneath, moss on top
    (material slots `mats`)."""
    c = Vector(centre)
    sx, sy, h = size
    pts = []
    for i in range(points):
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
        t = (d.z + 1) / 2 if i >= 4 else 0.0                     # the first four hold the footprint
        j = rnd.uniform(0.78, 1.08)
        x = math.copysign(abs(d.x) ** 0.55, d.x) * sx * j
        y = math.copysign(abs(d.y) ** 0.55, d.y) * sy * j
        if i < 4:
            x, y = (sx if i % 2 else -sx) * rnd.uniform(0.75, 1.0), (sy if i < 2 else -sy) * rnd.uniform(0.75, 1.0)
        z = -sink + t * (h + sink)
        if z > h * 0.82:
            z = h * rnd.uniform(0.86, 0.96)                       # a ledge on top
        pts.append(c + Vector((x + lean[0] * h * t, y + lean[1] * h * t, z)))
    faces = A.hull(bm, pts)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    for f in faces:
        f.normal_update()
        f.material_index = mats[2] if f.normal.z > 0.75 else mats[1] if f.normal.z < -0.3 else mats[0]
    return faces


def block(bm, lo, hi, rnd, mat=0, chip=0.025):
    """A dressed stone block from lo to hi, its corners knocked a little out of true."""
    verts, faces = A.box(bm, lo, hi, mat)
    for v in verts:
        v.co += Vector((rnd.uniform(-chip, chip), rnd.uniform(-chip, chip), rnd.uniform(-chip, chip)))
    return verts, faces


def twisted(bm, z0, length, half0, half1, a0, twist, mat, rings=6, caps=True):
    """A four-sided column from z0 up `length`, its section shrinking from half0 to half1 (half
    widths) while it turns from a0 through `twist`: the makers' shape."""
    out = []
    for i in range(rings + 1):
        t = i / rings
        hx, hy = half0[0] + (half1[0] - half0[0]) * t, half0[1] + (half1[1] - half0[1]) * t
        a = a0 + twist * t
        out.append([bm.verts.new((x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a), z0 + length * t))
                    for x, y in ((-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy))])
    for r0, r1 in zip(out, out[1:]):
        for k in range(4):
            bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k])).material_index = mat
    if caps:
        bm.faces.new(out[-1]).material_index = mat
        bm.faces.new(list(reversed(out[0]))).material_index = mat
    return out


def weather(bm, rnd, amount=0.025, moss=None, up=0.7):
    """Age worked stone: every corner knocked a little out of true, and (moss: a material slot)
    moss on the faces that look up."""
    for v in bm.verts:
        v.co += Vector((rnd.uniform(-amount, amount), rnd.uniform(-amount, amount), rnd.uniform(-amount, amount) * 0.5))
    if moss is not None:
        for f in bm.faces:
            f.normal_update()
            if f.normal.z > up:
                f.material_index = moss


def lay(bm, verts, matrix):
    bmesh.ops.transform(bm, matrix=matrix, verts=verts)


def added(bm, before):
    """The verts made since `before` (a set of the verts there were)."""
    return [v for v in bm.verts if v not in before]


def along_x(bm, verts, x0, z):
    """Turn a column built up Z into a beam lying along X from x0, its middle at height z."""
    lay(bm, verts, Matrix.Translation((x0, 0, z)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))


def rng(seed):
    return random.Random(seed)


SPAN = 2.5          # between a doorway's post centres
DOOR_H = 2.6        # posts' height; lintels and arches start here
MAKER = "Gravestone"                       # the makers' pale stone
INLAY = "Moonstone"                        # their one inlaid band, muted moonlight
MAKER_MATS = [MAKER, INLAY, "Moss"]
