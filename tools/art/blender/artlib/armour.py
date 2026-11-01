"""Armour: plating a whole creature, part by part (tools/art/blender/creatures.md, "Armour").

artlib/plates.py shapes one plate. This module places plates on any part of a body and checks the
result:
- Axis      a part's centre line (the spine from tail tip to nose, a leg from the hip to the foot): a
            polyline with a carried frame, so a point on the part is (distance along, angle round)
- Frame     projects onto the body from an axis (plates.slab and band use it): from the nearest point on
            the axis straight out to the surface, so a leg is plated from the leg, not the spine
- band      a plate that wraps round a part: a collar, a tail ring, a bracer, a belly scute
- row       a generated run of shingled plates along an axis, sized by the part's own girth
- checks    pieces, crossings within a piece, plates passing through each other, floating plates,
            coverage by region; and bend(), to deform body and plates together for a movement test

Angles round an axis: 0 is the axis's reference side (up for the spine, forward for a leg), positive
toward the creature's left (+X). Positions are (f, s, u) unless said otherwise.
"""
import math

import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from artlib.grow import P
from artlib.plates import split_ladder  # noqa: F401  (re-exported for boards)

BAND = [(1.0, -0.6), (0.9, 0.55), (0.45, 0.95), (-0.45, 1.0), (-0.9, 0.6), (-1.0, -0.6)]
BAND_KEEL = [(1.0, -0.6), (0.9, 0.5), (0.35, 0.95), (-0.5, 1.1), (-0.9, 0.7), (-1.0, -0.6)]


def fsu(v):
    return (-v.y, v.x, v.z)


class Axis:
    """A part's centre line: points (f, s, u), front last for the spine (so its tangent points to the
    head). ref (f, s, u): the reference direction for angle 0, carried along the line."""

    def __init__(self, points, ref=(0, 0, 1)):
        self.pts = [P(*p) for p in points]
        self.cum = [0.0]
        for a, b in zip(self.pts, self.pts[1:]):
            self.cum.append(self.cum[-1] + (b - a).length)
        self.length = self.cum[-1]
        segs = [(b - a).normalized() for a, b in zip(self.pts, self.pts[1:])]
        self.tan = [segs[0]] + [(segs[i - 1] + segs[i]).normalized() for i in range(1, len(segs))] + [segs[-1]]
        r = P(*ref)
        refs = []
        for t in self.tan:                         # carried: the frame turns with the line, never flips
            r = (r - t * r.dot(t))
            r = r.normalized() if r.length > 1e-6 else refs[-1]
            refs.append(r)
        self.ref = refs
        self.left = 1.0
        mid = len(self.tan) // 2                   # positive angles toward +X
        if self.tan[mid].cross(self.ref[mid]).x < 0:
            self.left = -1.0

    def at(self, d):
        """(point, tangent, reference, side) at distance d along the line."""
        d = max(0.0, min(self.length, d))
        i = max(0, min(len(self.pts) - 2, next((j for j in range(len(self.cum) - 1) if self.cum[j + 1] >= d), 0)))
        seg = self.cum[i + 1] - self.cum[i]
        w = (d - self.cum[i]) / seg if seg > 1e-9 else 0.0
        p = self.pts[i].lerp(self.pts[i + 1], w)
        t = self.tan[i].lerp(self.tan[i + 1], w).normalized()
        r = self.ref[i].lerp(self.ref[i + 1], w)
        r = (r - t * r.dot(t)).normalized()
        side = t.cross(r).normalized() * self.left
        return p, t, r, side

    def nearest(self, p):
        """(distance along, point on the line) nearest p."""
        best = (1e9, 0.0, self.pts[0])
        for i, (a, b) in enumerate(zip(self.pts, self.pts[1:])):
            ab = b - a
            t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
            q = a + ab * t
            dist = (p - q).length
            if dist < best[0]:
                best = (dist, self.cum[i] + ab.length * t, q)
        return best[1], best[2]

    def direction(self, d, theta):
        _, t, r, side = self.at(d)
        return r * math.cos(theta) + side * math.sin(theta)


class Frame:
    """Projects onto the body from an axis: from the nearest point on the axis straight out."""

    def __init__(self, bvh, axis):
        self.bvh, self.axis = bvh, axis

    def cast(self, p):
        d_along, q = self.axis.nearest(p)
        _, t, r, _ = self.axis.at(d_along)
        d = p - q
        d = d - t * d.dot(t)
        d = d.normalized() if d.length > 1e-9 else r
        hit = self.bvh.ray_cast(q, d)[0]
        return (hit if hit is not None else p), d

    def local(self, p):
        """A frame for one plate at p: a straight axis through the nearest point on this one, along its
        tangent there, so every point of the plate is cast from the same line (on a bend, casting each
        point from its own nearest segment folds the plate)."""
        return LocalFrame(self.bvh, *self.axis.at(self.axis.nearest(P(*p) if isinstance(p, tuple) else p)[0])[:2])

    def on(self, d, theta, out=0.0):
        """The body's surface at distance d along the axis and angle theta round it, as (f, s, u)."""
        q, *_ = self.axis.at(d)
        dirn = self.axis.direction(d, theta)
        hit = self.bvh.ray_cast(q, dirn)[0]
        p = (hit if hit is not None else q) + dirn * out
        return fsu(p)

    def girth(self, d, theta=0.0):
        """How far the surface is from the axis at d, theta: the part's radius there."""
        q, *_ = self.axis.at(d)
        hit = self.bvh.ray_cast(q, self.axis.direction(d, theta))[0]
        return (hit - q).length if hit is not None else 0.1

    def tangent(self, d):
        return fsu(self.axis.at(d)[1])


class LocalFrame:
    """Casts from one straight line (a point and a direction): the frame of a single plate."""

    def __init__(self, bvh, q, t):
        self.bvh, self.q, self.t = bvh, q, t

    def cast(self, p):
        q = self.q + self.t * (p - self.q).dot(self.t)
        d = p - q
        d = d.normalized() if d.length > 1e-9 else Vector((0, 0, 1))
        hit = self.bvh.ray_cast(q, d)[0]
        return (hit if hit is not None else p), d


def band(bm, frame, d, length, thickness, theta0=-math.pi, theta1=math.pi, n=16, profile=BAND, tilt=0.0,
         mat=0, taper=0.0, lip=0.0):
    """A plate wrapping round a part: rings of the profile (axial position -1 back .. 1 front, height in
    thicknesses) at `n` angles from theta0 to theta1 (a full turn: a closed ring, a collar; less: a bracer
    or a scute, its ends closed). Each point is cast out from the axis, so it hugs the part. tilt raises
    the back edge (shingled rings); taper thins it toward its ends (a partial band). n even and a range
    symmetric about 0 or pi puts points on the centre line, for the Mirror. lip: the back edge raised (in
    thicknesses) to rest on the next ring instead of cutting into it. Returns its loops."""
    full = theta1 - theta0 >= 2 * math.pi - 1e-6
    count = n if full else n + 1
    def radii(dd):
        q, t, r, side = frame.axis.at(dd)
        dirs = [r * math.cos(theta0 + (theta1 - theta0) * k / n) + side * math.sin(theta0 + (theta1 - theta0) * k / n)
                for k in range(count)]
        rad = []
        for dirn in dirs:                          # each angle's radius, with a limit: a ray that slips past
            hit = frame.bvh.ray_cast(q, dirn, 2.0)[0]   # the body onto a leg is an outlier
            rad.append((hit - q).length if hit is not None else None)
        good = sorted(x for x in rad if x is not None)
        med = good[len(good) // 2] if good else 0.1
        ok = [x is not None and x < 1.4 * med for x in rad]
        for k in range(count):                     # outliers take the nearest good neighbours' radius
            if not ok[k]:
                lo = next((rad[j] for j in range(k - 1, -1, -1) if ok[j]), None)
                hi = next((rad[j] for j in range(k + 1, count) if ok[j]), None)
                rad[k] = ((lo or hi or med) + (hi or lo or med)) / 2
        for _ in range(2):                         # smoothed round the band: no jagged steps
            rad = [rad[k] if (not full and k in (0, count - 1)) else
                   0.25 * rad[k - 1] + 0.5 * rad[k] + 0.25 * rad[(k + 1) % count] for k in range(count)]
        return rad
    # the radii sampled at the band's front and back only and blended between: a short band lies on one
    # smooth surface, so a crease in the skin under it can't fold it
    r_front, r_back = radii(d + length / 2), radii(d - length / 2)
    loops = []
    for u, h in profile:
        dd = d + u * length / 2
        q, t, r, side = frame.axis.at(dd)
        dirs = [r * math.cos(theta0 + (theta1 - theta0) * k / n) + side * math.sin(theta0 + (theta1 - theta0) * k / n)
                for k in range(count)]
        w = (u + 1) / 2
        rad = [rb + (rf - rb) * w for rf, rb in zip(r_front, r_back)]
        loop = []
        for k in range(count):
            th = theta0 + (theta1 - theta0) * k / n
            dirn = dirs[k]
            s = q + dirn * rad[k]
            edge = 1.0
            if not full and taper:
                x = abs(2 * k / n - 1)
                edge = 1 - taper * x ** 4
            back = max(0.0, -u) ** 1.5 * lip           # the whole back lifts together
            core = (h - tilt * u * 0.5) * (edge if h > 0 else 1.0)
            off = (core + back * edge) * thickness     # the lip thins with the band toward its ends
            loop.append(bm.verts.new(s + dirn * off))
        loops.append(loop)
    m = len(loops)
    for i in range(m):
        a, b = loops[i], loops[(i + 1) % m]
        for k in range(n if full else count - 1):
            k2 = (k + 1) % count
            bm.faces.new((a[k], a[k2], b[k2], b[k])).material_index = mat
    if not full:                                   # close the two ends
        for k in (0, count - 1):
            ring = [loops[i][k] for i in range(m)]
            for i in range(m // 2 - 1):
                bm.faces.new((ring[i], ring[i + 1], ring[m - 2 - i], ring[m - 1 - i])).material_index = mat
    return loops


def row(frame, d0, d1, size, overlap=0.25, theta=0.0):
    """Places for a run of shingled plates along an axis from d0 to d1: each (d, length, girth), the
    length `size(girth)` from the part's own radius there, each plate overlapping the one behind it by
    `overlap` of its length. Front to back or back to front, whichever way d0 -> d1 runs."""
    out = []
    step = 1 if d1 >= d0 else -1
    d = d0
    while (d - d1) * step < 0:
        g = frame.girth(d, theta)
        L = size(g)
        c = d + step * L / 2
        if (c - d1) * step > 0:
            break
        out.append((c, L, g))
        d += step * L * (1 - overlap)
    return out


# -- checks ---------------------------------------------------------------------------------------

def pieces(bm):
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    piece, pid = {}, 0
    for f in bm.faces:
        if f.index in piece:
            continue
        st = [f]
        piece[f.index] = pid
        while st:
            x = st.pop()
            for e in x.edges:
                for nf in e.link_faces:
                    if nf.index not in piece:
                        piece[nf.index] = pid
                        st.append(nf)
        pid += 1
    return piece, pid


def report(body_bm, plate_bm):
    """Numbers for a plated body: plate topology, crossings within a plate, plates passing through each
    other (pairs of pieces), floating plates (no vertex under the skin), and the share of each region's
    skin covered by plates."""
    body = body_bm.copy()
    bmesh.ops.recalc_face_normals(body, faces=body.faces[:])
    body.normal_update()
    bt = BVHTree.FromBMesh(body)
    pl = plate_bm.copy()
    bmesh.ops.recalc_face_normals(pl, faces=pl.faces[:])
    pl.verts.index_update()
    piece, npieces = pieces(pl)
    vs = [v.co.copy() for v in pl.verts]
    polys = [[v.index for v in f.verts] for f in pl.faces]
    pt = BVHTree.FromPolygons(vs, polys)
    def outside(i):                                # a face whose middle is outside the body: it shows
        c = sum((vs[j] for j in polys[i]), Vector()) / len(polys[i])
        loc, n, _, _ = bt.find_nearest(c)
        return loc is None or (c - loc).dot(n) > 0
    within, between = 0, set()
    for a, b in pt.overlap(pt):
        if a >= b or set(polys[a]) & set(polys[b]):
            continue
        if piece[a] == piece[b]:
            within += 1
        elif outside(a) and outside(b):            # shingles meeting under the skin don't show
            between.add((min(piece[a], piece[b]), max(piece[a], piece[b])))
    inside = {}
    for f in pl.faces:
        for v in f.verts:
            loc, n, _, _ = bt.find_nearest(v.co)
            ins = loc is not None and (v.co - loc).dot(n) < 0
            inside.setdefault(piece[f.index], []).append(ins)
    floating = sum(1 for p, flags in inside.items() if not any(flags))
    cover, area = {}, {}
    for f in body.faces:
        c = f.calc_center_median()
        fwd = -c.y
        if abs(c.x) > 0.62 and c.z < 0.9:
            region = "legs"
        elif fwd > 2.2:
            region = "head"
        elif fwd > 1.2:
            region = "neck"
        elif fwd < -1.6:
            region = "tail"
        elif f.normal.z > 0.5:
            region = "back"
        elif f.normal.z < -0.5:
            region = "belly"
        else:
            region = "flanks"
        a = f.calc_area()
        area[region] = area.get(region, 0) + a
        hit = pt.ray_cast(c + f.normal * 0.002, f.normal, 0.5)[0]
        if hit is not None:
            cover[region] = cover.get(region, 0) + a
    stats = dict(pieces=npieces, faces=len(pl.faces), tris=sum(1 for f in pl.faces if len(f.verts) == 3),
                 ngons=sum(1 for f in pl.faces if len(f.verts) > 4),
                 open=sum(1 for e in pl.edges if len(e.link_faces) == 1),
                 nonmanifold=sum(1 for e in pl.edges if len(e.link_faces) > 2),
                 crossings_within=within, visible_collisions=len(between), floating=floating,
                 coverage={k: round(cover.get(k, 0) / area[k], 2) for k in sorted(area)})
    body.free()
    pl.free()
    return stats


def bend(bms, pivot, axis, angle, weight):
    """Deform meshes together (a body and its plates) for a movement test: every vertex turns about the
    line through `pivot` (f, s, u) along `axis` (f, s, u) by angle (radians) times weight(f, s, u) of
    its original position."""
    from mathutils import Matrix
    pv, ax = P(*pivot), P(*axis).normalized()
    for bm in bms:
        for v in bm.verts:
            w = weight(*fsu(v.co))
            if w <= 0:
                continue
            v.co = pv + Matrix.Rotation(angle * min(1.0, w), 3, ax) @ (v.co - pv)


def smooth(x, a, b):
    """0 before a, 1 after b, a smooth step between."""
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)
