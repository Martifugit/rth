"""Shared parts and plumbing for grown creatures (tools/art/blender/creatures.md): a model as mirrored
parts under a root, the owner's hand edits as data, and the pieces several creatures share (a
three-toed clawed foot, a row of spines along the top line).

Positions are (f, s, u) in the creature's frame, as in artlib/grow.py.
"""
import json
import os

import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from artlib.grow import P


def to_object(bm, name, colours, coll, parent=None, mirror=True, clip=False):
    """A part as its own object (under `parent`), with a Mirror modifier on X: edit the +X half and
    the other follows. clip: the half's centre line is clipped and merged (a body, not a limb)."""
    import rth_art as A
    A.finish(bm)
    ob = A.mesh_from(name, bm, colours, coll=coll, smooth_angle=60)
    if parent is not None:
        ob.parent = parent
    if mirror:
        mod = ob.modifiers.new("Mirror", "MIRROR")
        mod.use_axis = (True, False, False)
        mod.use_clip = clip
        mod.use_mirror_merge = clip
        mod.merge_threshold = 1e-3
    return ob


def keep_left(bm):
    """Only the +X half (the centre line kept exactly on x = 0), for a Mirror modifier to complete."""
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x < -1e-4], context="VERTS")
    for v in bm.verts:
        if abs(v.co.x) <= 1e-4:
            v.co.x = 0.0


def symmetric(bm):
    """Exactly symmetric, the +X half copied over, so the left half can stand for both."""
    bmesh.ops.symmetrize(bm, input=bm.verts[:] + bm.edges[:] + bm.faces[:], direction="X", dist=1e-3)


def tuning(script, part):
    """The owner's hand edits for a part, from <script>.tuning.json beside the creature's script."""
    path = os.path.splitext(os.path.abspath(script))[0] + ".tuning.json"
    return json.load(open(path)).get(part, []) if os.path.exists(path) else []


def tune(bm, edits):
    """Apply hand edits: each [x, y, z, dx, dy, dz] moves the vertices built at (x, y, z) (or, for a
    tip since made blunt, round it). Recorded by matching positions against a fresh build, never by
    vertex index."""
    for x, y, z, dx, dy, dz in edits:
        at, d = Vector((x, y, z)), Vector((dx, dy, dz))
        if abs(x) < 0.02:                          # on the centre line: it stays there
            d.x = 0.0
        near = ([v for v in bm.verts if (v.co - at).length < 0.012] or
                [v for v in bm.verts if (v.co - at).length < 0.07])
        for v in near:
            v.co += d


def foot(g, ankle, f, w, s, skin, claw, size=1.0, nail=1.0):
    """A foot out of the ankle's end (a ring of 8 corners: a limb grown from three faces of a
    12-sided body): a broad, low pad, its end split into three toes, a claw grown out of the end of
    each. f, w: where the pad sits (w signed by the side s). size scales it; nail scales the claws'
    length (0.4: short blunt nails on a padded foot)."""
    k = size
    pad = g.extrude(ankle, [(f, w, 0.08 * k, 0.25 * k, 0.09 * k, 0.07 * k)], skin)
    for i, q in enumerate(g.split_cap(pad, (1, 0, -0.2), (0, s, 0), 3)):
        side = w + s * (0.16 - 0.16 * i) * k        # the row runs from the outer toe in
        f0 = f + (0.2 - abs(i - 1) * 0.05) * k
        toe = g.extrude([q], [(f0, side, 0.07 * k, 0.065 * k, 0.06 * k, 0.05 * k),
                               (f0 + 0.1 * k, side, 0.06 * k, 0.055 * k, 0.05 * k, 0.04 * k)], skin)
        g.extrude(toe, [(f0 + (0.1 + 0.06 * nail) * k, side, 0.055 * k, 0.04 * k, 0.035 * k, 0.03 * k)], claw,
                  tip=(f0 + (0.1 + 0.2 * nail) * k, side, (0.055 - 0.035 * nail) * k), blunt=(0.006 + 0.02 * (1 - nail)) * k)


def spines(g, mat, start, end, peak=(1.0, -0.5), step=0.32, tallest=0.26, keep=0.05):
    """Thin blades along the top line from f = start back to f = end, grown out of the body: each
    from a small inset in the top face a ray straight down meets, raked back along the line. They
    rise from 0.45 of `tallest` at the front to full height over `peak` (f from, f to), then fall to
    a quarter at the end, and are never taller than 0.7 of the body's depth under them."""
    g.bm.faces.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(g.bm)
    down = Vector((0, 0, -1))
    spots, used = [], set()
    f = start
    while f >= end:
        loc, nrm, idx, _ = bvh.ray_cast(P(f, 0.0, 9.0), down)
        face = g.bm.faces[idx] if loc is not None else None
        if face is not None and face not in used and abs(face.calc_center_median().x) < 0.02:
            used.add(face)
            under = bvh.ray_cast(loc + down * 0.01, down)[0]
            spots.append((f, face, (loc.z - under.z) if under is not None else 0.3))
        f -= step
    centres = [face.calc_center_median() for _, face, _ in spots]
    hi, lo = peak
    for i, (f, face, depth) in enumerate(spots):
        if f > hi:
            shape = 0.45 + 0.55 * (start - f) / max(start - hi, 1e-6)
        elif f > lo:
            shape = 1.0
        else:
            shape = 1.0 - 0.75 * (lo - f) / max(lo - end, 1e-6)
        k = min(tallest * shape, 0.7 * depth)
        c = centres[i]
        n = face.normal.normalized()
        d = centres[min(i + 1, len(spots) - 1)] - centres[max(i - 1, 0)]
        back = Vector((0, d.y, d.z)).normalized()
        tip = c + n * k + back * (0.75 * k)
        base = g.inset(face, keep)
        shift = Vector((-base.calc_center_median().x, 0, 0))
        for v in base.verts:                       # stand it exactly on the centre line, so the mirror
            v.co += shift                          # cuts it cleanly in two
        g.extrude([base], [(-c.y, 0.0, c.z + 0.02, 0.02, 0.035, 0.035)], mat, tip=(-tip.y, 0.0, tip.z), blunt=0.004)


def knobs(g, mat, spots, height=0.15, base=0.14):
    """Low, rounded bony knobs (osteoderms) on the top of the body, each grown out of the face a ray
    straight down meets at (f, s): a short blunt cone along the face's normal. spots: (f, s, scale)."""
    g.bm.faces.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(g.bm)
    picked, used = [], set()                       # find every face first: growing changes the mesh
    for f, s, k in spots:
        loc, nrm, idx, _ = bvh.ray_cast(P(f, s, 9.0), Vector((0, 0, -1)))
        if loc is not None and g.bm.faces[idx] not in used:
            used.add(g.bm.faces[idx])
            picked.append((g.bm.faces[idx], k))
    for face, k in picked:
        n = face.normal.normalized()
        c = g.inset(face, base * k).calc_center_median()
        a, tip = c + n * (height * k * 0.5), c + n * (height * k)
        g.extrude([face], [(-a.y, a.x, a.z, base * k * 0.42, base * k * 0.42, base * k * 0.42)], mat,
                  tip=(-tip.y, tip.x, tip.z), blunt=base * k * 0.18)


def paw(g, ankle, f, w, s, mat, claw=None, size=1.0, toe=0.03):
    """A mammal's paw out of the ankle's end: a short low pad forward, its end split into a row of toes
    (as many as the ankle's ring allows: a ring of 10 gives four), each a short rounded toe extruded
    from its own quad, placed from that quad (no positions by hand), with a small claw if `claw`."""
    k = size
    ring = Grow_boundary(ankle)
    toes = len(ring) // 2 - 1
    pad = g.extrude(ankle, [(f, w, 0.032 * k, 0.048 * k, 0.028 * k, 0.022 * k)], mat)
    quads = g.split_cap(pad, (1, 0, -0.35), (0, s, 0), toes)
    centres = [q.calc_center_median() for q in quads]
    gap = min((a - b).length for a, b in zip(centres, centres[1:])) if len(centres) > 1 else toe * k
    r = 0.42 * gap                                  # each toe narrower than its spacing: neighbours never touch
    for q, c in zip(quads, centres):
        cf, cs = -c.y, c.x
        a = (cf + toe * 0.55 * k, cs, 0.024 * k, r, r * 1.1, r * 0.9)
        if claw is None:
            g.extrude([q], [a], mat, tip=(cf + toe * 1.2 * k, cs, 0.012 * k), blunt=0.4 * r)
        else:
            end = g.extrude([q], [a, (cf + toe * 1.05 * k, cs, 0.017 * k, r * 0.7, r * 0.7, r * 0.6)], mat)
            g.extrude(end, [], claw, tip=(cf + toe * 1.55 * k, cs, 0.006 * k), blunt=0.15 * r)


def Grow_boundary(faces):
    from artlib.grow import Grow
    return Grow.boundary(faces)
