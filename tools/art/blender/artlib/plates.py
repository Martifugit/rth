"""Plates: bone plating, scutes, caps and armour laid over a creature as an overlay
(tools/art/blender/creatures.md, "Surface features").

A plate is concentric rings of the same outline, from a rim sunk under the skin, up a steep bevel to the
plate's edge, then rising to its crown; every point is cast onto the body from its centre line, so the
plate fits the body exactly. Its middle is closed with quads, or it rises on into a horn or spike: the
plate thickens into the feature, one surface. The underside is closed under the skin.

Rings have n points (n / 2 odd: 10, 14, 18, 22); point 0 is the plate's front tip and point n / 2 its
back, both on the plate's middle line, so a plate on the creature's centre line splits cleanly in two
for a Mirror modifier (beast.keep_left).

Positions are (f, s, u) in the creature's frame.
"""
import math

from mathutils import Vector

from artlib.grow import P

# A plate's profile: (ring scale, height in thicknesses). The rim is sunk; the bevel climbs steeply to
# the edge; then the top rises to the crown.
PROFILE = [(1.0, -0.6), (0.95, 0.5), (0.72, 0.82), (0.44, 0.98), (0.18, 1.04)]
FLAT = [(1.0, -0.6), (0.94, 0.55), (0.6, 0.72), (0.25, 0.78)]
KEELED = [(1.0, -0.6), (0.95, 0.45), (0.7, 0.75), (0.4, 0.95), (0.15, 1.0)]
KNOB = [(1.0, -0.6), (0.95, 0.5), (0.7, 0.8), (0.42, 1.0), (0.3, 1.8), (0.16, 2.2)]   # the plate rises into a
                                                # low knob (an osteoderm): a short feature is profile, not a horn


def outline(n=18, front=0.0, back=0.0, square=0.0, bulge=0.0, lobes=0, lobe=0.0):
    """n points (a, b) of a unit outline (a along, -1 back .. 1 front; b across, -1..1), point k at angle
    2 pi k / n from the front tip. front / back: how pointed each end is (0 round, 1 sharp); square:
    toward a rounded rectangle; bulge: the widest part moved forward (> 0) or back (< 0); lobes, lobe: the
    edge scalloped into that many lobes, `lobe` deep (a broken, chunky edge; symmetric, not random)."""
    e = 1.0 / (1.0 + 2.0 * square)
    pts = []
    for k in range(n):
        t = 2 * math.pi * k / n
        c, s = math.cos(t), math.sin(t)
        a = math.copysign(abs(c) ** e, c)
        b = math.copysign(abs(s) ** e, s)
        p = front if c > 0 else back
        b *= 1 - p * abs(c) ** 1.5
        a = a + bulge * (1 - a * a) * 0.5
        if lobes:
            k_ = 1 + lobe * math.cos(lobes * t)
            a, b = a * k_, b * k_
        pts.append((a, b))
    return pts


def split_ladder(bm, ring, mat, flip=False):
    """Close a ring (n / 2 odd) with quads: two ladders, one each side of the chord from point 0 to
    point n / 2, so a plate on the centre line still splits along that chord."""
    n = len(ring)
    h = n // 2
    out = []
    for chain in (ring[:h + 1], ring[h:] + ring[:1]):
        q = len(chain) // 2
        for k in range(q - 1):
            quad = (chain[k], chain[k + 1], chain[2 * q - 2 - k], chain[2 * q - 1 - k])
            f = bm.faces.new(tuple(reversed(quad)) if flip else quad)
            f.material_index = mat
            out.append(f)
    return out


def quadratic(a, m, b, t):
    return a * (1 - t) ** 2 + m * (2 * t * (1 - t)) + b * t * t


class Frame:
    """Where a plate lies on a body: the body's centre line `axis` (f -> u) and its surface `bvh`."""

    def __init__(self, bvh, axis):
        self.bvh, self.axis = bvh, axis

    def axis_pt(self, p):
        return Vector((0.0, p.y, self.axis(-p.y)))

    def cast(self, p):
        """The body's surface under p, seen from the centre line, and the outward direction there."""
        q = self.axis_pt(p)
        d = p - q
        d = d.normalized() if d.length > 1e-9 else Vector((0, 0, 1))
        hit = self.bvh.ray_cast(q + d * 6.0, -d)[0]
        return (hit if hit is not None else p), d


def slab(bm, frame, centre, along, length, width, shape, thickness, profile=PROFILE, ridge=0.0, tilt=0.0,
         lift=0.0, mat=0, horn=None, facets=0, facet=0.0, lip_back=0.0, lip_down=0.0):
    """One plate as a closed piece of quads. centre (f, s, u): a point toward which the plate lies on the
    body; along (f, s, u): the way its length runs; shape: an outline() of n points; profile: its rings.
    ridge: a keel down its middle (in thicknesses); tilt: the rear edge raised (shingles), the front
    tucked; lift: the whole plate raised off the skin (the rim still sunk). horn: dict(end, pull, tip,
    rings, mat) grows a horn or spike out of the plate's middle instead of closing it: the innermost ring
    rises along a curve to `end`, or by `length` out of the plate (raked back by `rake`, out by `spread`:
    better, it can't end inside its own plate), its outline easing to round and its size to `tip`; `r`
    sets its base radius (a boss on a big plate). Returns the rings, rim first."""
    if horn is not None:                          # the horn starts from a big inner ring: the plate thickens
        cut = horn.get("base", 0.7)               # into it, it doesn't sprout from a pinhole in the middle
        if "r" in horn:                           # or from a ring of a given radius: a boss on a big plate
            cut = min(0.9, max(0.15, horn["r"] / (min(length, width) / 2)))
        keep = [r for r in profile if r[0] > cut + 0.1] or profile[:2]   # no ring crowding the cut
        nxt = next((r for r in profile if r[0] <= cut + 1e-6), profile[-1])
        prev_ = keep[-1] if keep else profile[0]
        w_ = (prev_[0] - cut) / max(prev_[0] - nxt[0], 1e-6)
        profile = keep + [(cut, prev_[1] + (nxt[1] - prev_[1]) * min(1.0, w_))]
    c = P(*centre)
    s0, d0 = frame.cast(c)
    al = P(*along)
    al = (al - d0 * al.dot(d0)).normalized()
    side = d0.cross(al).normalized()
    if c.x > 1e-6 and side.x < 0:
        side = -side
    down = 1.0 if side.z < 0 else -1.0             # which way across the plate (b) faces the ground
    rings = []
    for k_ring, (sc, h) in enumerate(profile):
        ring = []
        for a, b in shape:
            p = s0 + al * (a * length / 2 * sc) + side * (b * width / 2 * sc)
            if c.x > 1e-6 and p.x < 0.006:            # a side plate stops at the centre line
                p.x = 0.006
            surf, d = frame.cast(p)
            lip = (lip_back * max(0.0, -a) ** 1.5 + lip_down * max(0.0, b * down) ** 1.5) * thickness
            if k_ring == 0:
                off = h * thickness + lip                 # the whole back lifts together: rings keep their order
            else:
                off = (h + ridge * (1 - abs(b)) * (1 - sc) - tilt * a * sc * 0.5) * thickness + lift + lip
                if facets and 0 < sc < 0.99:
                    ang = math.atan2(b, a)
                    off += facet * thickness * math.cos(facets * ang) * min(1.0, (1 - sc) * 3)
            ring.append(bm.verts.new(surf + d * off))
        rings.append(ring)
    n = len(shape)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(n):
            bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k])).material_index = mat
    split_ladder(bm, rings[0], mat, flip=True)            # the underside, under the skin
    if horn is None:
        split_ladder(bm, rings[-1], mat)
        return rings
    base = rings[-1]
    bc = sum((v.co for v in base), Vector()) / n
    if "length" in horn:                          # measured from the plate itself: out of its top along its
        L_ = horn["length"]                       # base's normal, raked back, spread out to the side
        nb0 = Vector()
        for v0, v1 in zip(base, base[1:] + base[:1]):
            nb0 += (v0.co - bc).cross(v1.co - bc)
        nb0 = nb0.normalized() if nb0.length > 1e-9 else d0
        if nb0.dot(d0) < 0:
            nb0 = -nb0
        end = bc + nb0 * L_ + P(-1, 0, 0) * (L_ * horn.get("rake", 0.7)) + side * (L_ * horn.get("spread", 0.0))
    else:
        end = P(*horn["end"])
    # the first stretch leaves straight out of the plate, then bends toward the end: never folding back
    # across the plate's own top
    reach = (end - bc).length
    r_base = min(length, width) / 2 * profile[-1][0]
    nb = Vector()                                 # the base ring's own normal (a tilted plate tips it)
    for v0, v1 in zip(base, base[1:] + base[:1]):
        nb += (v0.co - bc).cross(v1.co - bc)
    nb = nb.normalized() if nb.length > 1e-9 else d0
    if nb.dot(d0) < 0:
        nb = -nb
    pull = bc + nb * (reach * horn.get("rise", 0.3)) + (end - bc) * 0.35
    m = max(4, min(9, round(reach / (0.45 * r_base))))   # rings spaced about half the base radius:
                                                          # dense enough round a bend, not so dense they crowd
    r_base = min(length, width) / 2 * profile[-1][0]
    hmat = horn.get("mat", mat)
    prev, made = base, []
    t1_prev, t2_sign = None, None
    fine = [quadratic(bc, pull, end, j / 100) for j in range(101)]
    run = [0.0]
    for p0, p1 in zip(fine, fine[1:]):
        run.append(run[-1] + (p1 - p0).length)

    def by_length(t):                             # the curve's parameter at a fraction of its length
        want = run[-1] * t
        j = next((j for j in range(100) if run[j + 1] >= want), 99)
        return (j + (want - run[j]) / max(run[j + 1] - run[j], 1e-9)) / 100
    for i in range(1, m + 1):
        t = i / m
        tq = by_length(t)
        ci = quadratic(bc, pull, end, tq)
        tan = (quadratic(bc, pull, end, min(1, tq + 0.02)) - quadratic(bc, pull, end, max(0, tq - 0.02))).normalized()
        if t1_prev is None:                       # the first ring keeps the base ring's orientation
            ref = al - nb * al.dot(nb)
        else:
            ref = t1_prev                         # carried ring to ring: the section never flips
        t1 = (ref - tan * ref.dot(tan))
        t1 = t1.normalized() if t1.length > 1e-6 else side.cross(tan).normalized()
        t2 = tan.cross(t1).normalized()
        if t2_sign is None:
            t2_sign = 1 if t2.dot(side) >= 0 else -1
        t2 = t2 * t2_sign
        t1_prev = t1
        r = r_base + (horn["tip"] - r_base) * (t ** horn.get("ease", 1.4))   # > 1: thick, then a point
        ring = []
        for k, (a, b) in enumerate(shape):
            ang = 2 * math.pi * k / n
            ca, cb = math.cos(ang), math.sin(ang)
            u = max(0.0, min(1.0, (t - 0.2) * 1.6))   # the first rings keep the base's outline, then round off
            ax, bx = a + (ca - a) * u, b + (cb - b) * u
            ring.append(bm.verts.new(ci + t1 * (ax * r) + t2 * (bx * r)))
        for k in range(n):
            bm.faces.new((prev[k], prev[(k + 1) % n], ring[(k + 1) % n], ring[k])).material_index = hmat
        prev = ring
        made.append(ring)
    split_ladder(bm, prev, hmat)
    return rings + made


def edge_spikes(g, rings, which, length, rake, mat, r0=None):
    """Spikes along a plate's edge (a row on its rear rim, a frill round a cap): each a bone
    (beast.bone) grown out of two neighbouring faces of the plate's bevel (between rings 0 and 1) at the
    outline points `which`, leaving outward and raked back. g: a Grow over the plates' bmesh."""
    from artlib.beast import bone
    n = len(rings[0])
    back = Vector((0, 1, 0))
    for k in which:
        a0, a1 = rings[1][k].co, rings[1][(k + 1) % n].co
        b0 = rings[0][k].co
        root = (a0 + a1) / 2 * 0.7 + b0 * 0.3
        mid = sum((v.co for v in rings[-1]), Vector()) / n
        out = (root - mid)
        out.z += out.length * 0.6
        out.normalize()
        along = (a1 - a0).normalized()
        end = root + out * length + back * (length * rake)
        pull = root + out * length * 0.5
        rad = r0 if r0 else (a1 - a0).length * 0.45
        fsu = lambda v: (-v.y, v.x, v.z)  # noqa: E731
        bone(g, mat, fsu(root), fsu(out), fsu(along), (fsu(pull), fsu(end)), rad, rad * 0.15, rings=5, faces=2)
