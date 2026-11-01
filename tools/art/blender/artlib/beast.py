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


def to_object(bm, name, colours, coll, parent=None, mirror=True, clip=False, smooth=60):
    """A part as its own object (under `parent`), with a Mirror modifier on X: edit the +X half and
    the other follows. clip: the half's centre line is clipped and merged (a body, not a limb)."""
    import rth_art as A
    A.finish(bm)
    ob = A.mesh_from(name, bm, colours, coll=coll, smooth_angle=smooth)
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


def quadruped(spec, skin=0, claw=1, sides=12):
    """A four-legged animal from a spec of numbers (the canines, the rat, the hare): one closed mesh
    grown from a body tube (`torso` rings); the neck and head out of its front (`head`, ending at
    `nose`), the tail out of its back (`tail`, ending at `tail_tip`), each leg from a 2x2 patch of the
    body's faces (`fore_patch`, `hind_patch`) through its rings (`fore`, `hind`) into a four-toed paw
    (`fore_paw`, `hind_paw`, `paw_size`; `feet`: paw or hoof), the ears out of the skull (`ear`) and any
    `growths` (tusks, antlers, tines): see grow_out.
    Side parts are grown on the left only, then the mesh is made symmetric. Returns the Grow."""
    from artlib.grow import Grow
    g = Grow(sides=sides)
    torso = g.tube(spec["torso"], skin)
    nose, blunt = spec["nose"]
    g.extrude(torso.caps["end"], spec["head"], skin, tip=nose, blunt=blunt)
    tip, tblunt = spec["tail_tip"]
    g.extrude(torso.caps["start"], spec["tail"], skin, tip=tip, blunt=tblunt)
    feet = spec.get("feet", "paw")
    for patch, rings, at, way in (("fore_patch", "fore", "fore_paw", (0, 0.5, -1)),
                                  ("hind_patch", "hind", "hind_paw", (0, 0.6, -1))):
        leg = g.patch(spec[patch], spec.get(patch + "_toward", way))
        ankle = g.extrude(leg, spec[rings], skin)
        size = spec.get(at + "_size", spec["paw_size"])
        if feet == "hoof":
            hoof(g, ankle, spec[at][0], spec[at][1], claw, size=size)
        else:
            paw(g, ankle, spec[at][0], spec[at][1], 1, skin, claw, size=size)
    for e in [spec["ear"]] + list(spec.get("growths", [])):
        grow_out(g, e, skin)
    if spec.get("extra"):                          # anything built by code (antlers, a bristle ridge)
        spec["extra"](g)
    symmetric(g.bm)
    return g


def grow_out(g, e, default_mat):
    """Something grown out of the surface (an ear, a tusk, an antler and its tines), the left one: the
    face nearest `at` (facing `toward`) shrunk to a slim base (`keep` wide, `thin` deep: an ear is flat)
    and extruded through `rings` to `tip`. A ring's thickness (its top/bottom) lies along `up`
    (forward by default: an ear's flat faces look forward and back). Grown in order, so a tine can grow
    out of a beam grown before it."""
    face = g.face(e["at"], e.get("toward", (0.2, 0.4, 1)))
    if face is None:
        return
    g.inset(face, e["keep"])
    thin = e.get("thin")
    if thin:                                       # flatten the base along `up`: a slim slot, not a square
        from artlib.grow import P
        u = P(*e.get("up", (1, 0, 0))).normalized()
        c = face.calc_center_median()
        for v in face.verts:
            d = v.co - c
            along = d.dot(u)
            v.co = c + (d - u * along) + u * along * (thin / max(e["keep"], 1e-6))
    g.extrude([face], e["rings"], e.get("mat", default_mat), tip=e["tip"], blunt=e.get("blunt", 0.004),
              up=e.get("up", (1, 0, 0)))


def hoof(g, ankle, f, w, mat, size=1.0):
    """A hoof out of the ankle's end: a short dark block widening to a flat sole, closed with quads."""
    k = size
    g.extrude(ankle, [(f, w, 0.06 * k, 0.034 * k, 0.034 * k, 0.03 * k),
                      (f + 0.008 * k, w, 0.02 * k, 0.04 * k, 0.042 * k, 0.034 * k)], mat,
              tip=(f + 0.01 * k, w, 0.0), blunt=0.036 * k)


def scaled(spec, k):
    """A copy of a spec with every length scaled by k (a smaller breed from a larger one)."""
    def sc(x):
        if isinstance(x, (int, float)) and not isinstance(x, bool):
            return x * k
        if isinstance(x, tuple):
            return tuple(sc(y) for y in x)
        if isinstance(x, list):
            return [sc(y) for y in x]
        if isinstance(x, dict):
            return {key: (v if key in ("colours", "feet", "mat", "toward", "up") else sc(v)) for key, v in x.items()}
        return x
    return sc(spec)


# -- bones: antlers, horns, tusks, spines, ribs ---------------------------------------------------

def curve(p0, p1, p2, r0, r1, n, ease=1.0):
    """n + 1 rings (f, s, u, r, r, r) along the quadratic curve p0 -> p2 (pulled toward p1), evenly
    spaced by length, the radius easing from r0 to r1 (ease > 1 keeps it thick longer). The last ring
    is the curve's end: pass it as a tip."""
    from mathutils import Vector
    a, b, c = Vector(p0), Vector(p1), Vector(p2)
    fine = [a * (1 - t) ** 2 + b * (2 * t * (1 - t)) + c * t * t for t in (i / 200 for i in range(201))]
    run = [0.0]
    for p, q in zip(fine, fine[1:]):
        run.append(run[-1] + (q - p).length)
    out, j = [], 0
    for i in range(n + 1):
        want = run[-1] * i / n
        while j < 200 and run[j + 1] < want:
            j += 1
        p = fine[j]
        t = i / n
        r = r0 + (r1 - r0) * (t ** ease)
        out.append((p.x, p.y, p.z, r, r, r))
    return out


def bone(g, mat, root, toward, across, path, r0, r1, rings=5, faces=2, ease=1.0, blunt=None):
    """A bone grown out of a surface (a horn, a tine out of a beam, a spine, a rib, a tusk): its root
    is a patch of `faces` edge-joined faces at `root` facing `toward` (2: a six-sided bone, 4: eight
    sided), laid out `across` (f, s, u: the way the patch runs), so it is round and flares where it
    leaves; it follows the curve `path` (p1, p2: the pull point and the end, from `root`), tapering
    from r0 to r1. Returns the end ring's centre."""
    from mathutils import Vector
    R, A = Vector(root), Vector(across).normalized()
    if faces == 4:
        up = Vector(toward).normalized().cross(A).normalized()
        d = 0.5
        pts = [tuple(R + A * x * d * r0 * 2 + up * y * d * r0 * 2) for x in (-1, 1) for y in (-1, 1)]
    else:
        pts = [tuple(R - A * r0), tuple(R + A * r0)]
    left = lambda f: min(v.co.x for v in f.verts) > 1e-4   # noqa: E731  a left-side bone never takes a face
    patch = g.patch(pts, toward, allow=left)                  # on the centre line (the mirror would cut it)
    p1, p2 = path
    ring_list = curve(root, p1, p2, r0, r1, rings, ease)[1:]
    tip = ring_list.pop()
    # a low, wide collar just off the surface first (a burr, a pedicle): the root's wall stays short
    # and never folds over between the patch's outline and the bone
    lift = Vector(toward).normalized() * (r0 * 0.5)
    first = Vector(ring_list[0][:3]) if ring_list else Vector(tip[:3])
    c = R + lift + (first - R) * 0.15
    ring_list.insert(0, (c.x, c.y, c.z, r0 * 1.2, r0 * 1.2, r0 * 1.2))
    g.extrude(patch, ring_list, mat, tip=tip[:3], blunt=blunt if blunt is not None else max(0.002, r1 * 0.5))
    return tip[:3]


# -- overlays: plates, scutes, caps ---------------------------------------------------------------

def axis_of(*ring_lists):
    """The body's centre line as a function f -> u, from ring lists ((f, s, u, ...) with s = 0): the
    torso, head and tail rings of a spec. Plates are cast onto the body from this line."""
    pts = sorted({(r[0], r[2]) for rings in ring_lists for r in rings})

    def at(f):
        if f <= pts[0][0]:
            return pts[0][1]
        for (f0, u0), (f1, u1) in zip(pts, pts[1:]):
            if f <= f1:
                return u0 + (u1 - u0) * (f - f0) / (f1 - f0)
        return pts[-1][1]
    return at


def plate(bm, bvh, axis, centre, along, length, width, thickness, rows=2, cols=3, dome=1.0, ridge=0.0,
          tilt=0.0, sink=None, mat=0, mirror_half=None):
    """A chunky plate laid over the body (an overlay: a skull cap, a scute, a shoulder plate): a grid
    of cols x rows quads over `length` (along `along`, f s u) by `width` (around the body), each point
    cast onto the body's surface from its centre line (`axis`: f -> u; bvh: the body), raised by
    `thickness` shaped by `dome` (fuller in the middle), `ridge` (a keel down its middle) and `tilt`
    (> 0 raises the rear edge, for plates that overlap like shingles); its rim drops `sink` under the
    skin and it is closed underneath: one closed piece of quads, no gap to the body. On the centre
    line (`mirror_half`, default: when centre's s is 0) only the left half is built, its middle edge
    left open on x = 0 for the Mirror modifier to close. Returns the top grid's faces by row."""
    from mathutils import Vector
    from artlib.grow import P
    sink = thickness * 0.6 if sink is None else sink
    half = (abs(centre[1]) < 1e-6) if mirror_half is None else mirror_half
    c = P(*centre)

    def axis_pt(p):
        return Vector((0.0, p.y, axis(-p.y)))
    d0 = (c - axis_pt(c)).normalized()
    al = P(*along)
    al = (al - d0 * al.dot(d0)).normalized()
    side = d0.cross(al).normalized()
    if half and side.x < 0:
        side = -side
    elif not half and side.dot(Vector((c.x, 0, 0))) < 0 and abs(c.x) > 1e-6:
        side = -side
    bs = [width * (j / rows) / 2 for j in range(rows + 1)] if half else \
         [width * (j / rows - 0.5) for j in range(rows + 1)]
    as_ = [length * (i / cols - 0.5) for i in range(cols + 1)]
    top, bot = {}, {}
    for i, a in enumerate(as_):
        for j, b in enumerate(bs):
            p = c + al * a + side * b
            q = axis_pt(p)
            d = (p - q).normalized()
            hit = bvh.ray_cast(q + d * 4.0, -d)[0]
            s = hit if hit is not None else p
            u = 2 * a / length
            v = (b / (width / 2)) if half else 2 * b / width
            shape = (1 - 0.55 * u * u) * (1 - 0.7 * v * v)
            off = thickness * (0.35 + 0.65 * (shape ** dome) + ridge * (1 - abs(v)) - tilt * u * 0.5)
            t, bo = s + d * off, s - d * sink
            if half and j == 0:
                t.x = bo.x = 0.0
            top[i, j], bot[i, j] = bm.verts.new(t), bm.verts.new(bo)
    faces = []
    for i in range(cols):
        row = []
        for j in range(rows):
            f = bm.faces.new((top[i, j], top[i + 1, j], top[i + 1, j + 1], top[i, j + 1]))
            f.material_index = mat
            row.append(f)
            bm.faces.new((bot[i, j + 1], bot[i + 1, j + 1], bot[i + 1, j], bot[i, j])).material_index = mat
        faces.append(row)
    if half:                                       # the walls on the three sides off the centre line
        walk = [(0, j) for j in range(rows + 1)] + [(i, rows) for i in range(1, cols + 1)] + \
               [(cols, j) for j in range(rows - 1, -1, -1)]
        pairs = list(zip(walk, walk[1:]))
    else:
        walk = [(i, 0) for i in range(cols + 1)] + [(cols, j) for j in range(1, rows + 1)] + \
               [(i, rows) for i in range(cols - 1, -1, -1)] + [(0, j) for j in range(rows - 1, -1, -1)]
        pairs = list(zip(walk, walk[1:]))
    for a, b in pairs:
        bm.faces.new((top[a], bot[a], bot[b], top[b])).material_index = mat
    return faces
