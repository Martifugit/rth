"""A bat-built wing (the dragon's, the wyvern's), bones and membrane as one closed surface, from a
spec of where its joints are (tools/art/blender/creatures.md, "Webs and membranes").

The bone is one tube of seam rings (Grow.seam_ring) from inside the back to the finger roots: every
ring has a seam on its leading and trailing side, corner 0 on the leading side. Past the forearm the
rings blend, corner by corner, into the hand's end: an octagon for each finger at its root, a web point
between each pair (Grow.fan_end). Each corner travels in a straight line, so the hand's trailing seam
runs straight from the forearm to the innermost finger (its brace). Each finger is a tube of seam rings
out of its octagon. The membrane's panels grow from the seams (Grow.web): between each pair of fingers;
behind the arm and the innermost finger (its root sunk in the flank from the armpit to the hip); in
front of the elbow (its free leading edge taut).

Positions are (f, s, u) for the left wing, in metres. `size` scales the bone and membrane
thicknesses, the thumb and the sag (1: the dragon's).
"""
from mathutils import Vector

import bmesh

from artlib.grow import Grow, P

ARC = 11                                    # corners over the top of an arm ring: 4 + 2 * ARC = 26, the
                                            # hand's end for four fingers with a web point between each pair
HAND = [0.33, 0.62, 0.85]                   # the blend's rings between forearm and fan (bend loops)
FINGER = [(0.025, 1.05), (0.05, 1.0), (0.2, 0.8), (0.4, 0.62), (0.6, 0.47), (0.83, 0.3), (0.95, 0.18),
          (1.0, 0.12)]                      # a finger's rings: how far along it, and its radius (of r_finger)
PROPATAGIUM_END = 10                        # the arm ring the leading panel reaches (by the thumb)


class WingSpec:
    """Where a wing's joints are. shoulder, elbow, wrist, thumb (its tip), tips (the four fingers', from
    the leading one back), hip (where the membrane's root ends on the body), lead (the leading edge's
    middle in front of the elbow), root_in / root_out (the arm's root inside the body and where it
    leaves the back), flank (where the membrane's root starts, just behind the arm), front (where the
    leading panel meets the body, just in front of the arm), core (the body's middle height, which
    the root is aimed at; a function of f for a leaning body). r_finger: each finger's radius at its root."""

    def __init__(self, **kw):
        self.size = 1.0
        self.skin = 0.015
        self.hand_from = 0.45               # how far before the wrist the forearm starts to become hand
        self.fan = 0.4                      # each finger's root: this far from the wrist toward its tip
        self.r_shoulder, self.r_elbow = 0.2, 0.13
        self.r_finger = [0.066, 0.064, 0.062, 0.062]
        self.bone, self.membrane, self.claw = 0, 1, 2
        self.__dict__.update(kw)


def bezier(a, ctrl, b, t):
    return a * (1 - t) ** 2 + ctrl * (2 * t * (1 - t)) + b * t * t


def through(a, m, b):
    """The control point of the quadratic curve from a to b that passes m halfway."""
    return m * 2 - (a + b) / 2


def wing_normal(w):
    """The wing's plane, upward: across the fingers' spread."""
    n = (P(*w.tips[0]) - P(*w.wrist)).cross(P(*w.tips[-1]) - P(*w.wrist)).normalized()
    return n if n.z > 0 else -n


def wing(w, bvh):
    """The left wing as one closed surface (a bmesh); bvh: the body, for the membrane's root."""
    g = Grow()
    k = w.size
    N = wing_normal(w)
    fwd = P(1, 0, 0)
    W, E, S0 = P(*w.wrist), P(*w.elbow), P(*w.shoulder)
    fore = (W - E).normalized()
    SCALE, WING, HORN, SKIN = w.bone, w.membrane, w.claw, w.skin

    def arm(at, axis, wd, t):
        return g.seam_ring(at, axis, N, fwd, wd, t, ARC, SKIN)
    root_in, root_out = P(*w.root_in), P(*w.root_out)
    up_axis = (E - S0).normalized()
    pts = [arm(root_in, root_out - root_in, 0.22 * k, 0.22 * k),              # inside the body: hidden
           arm(root_out, S0 - root_in, 0.22 * k, 0.21 * k),                   # leaving the back
           arm(S0, up_axis, w.r_shoulder, w.r_shoulder),
           arm(S0.lerp(E, 0.5), up_axis, 0.17 * k, 0.15 * k),
           arm(E + (S0 - E).normalized() * 0.12 * k, up_axis, 0.145 * k, 0.135 * k),   # a ring either side of the elbow
           arm(E, (W - S0).normalized(), 0.15 * k, w.r_elbow + 0.01 * k),
           arm(E + fore * 0.12 * k, fore, 0.13 * k, 0.125 * k),
           arm(E.lerp(W, 0.5), fore, 0.11 * k, 0.11 * k),
           arm(W - fore * w.hand_from, fore, 0.11 * k, 0.11 * k)]               # the forearm, before the hand
    reach = (sum((P(*t) for t in w.tips), Vector()) / len(w.tips) - W).normalized()
    spread = P(*w.tips[0]) - P(*w.tips[-1])
    spread = (spread - reach * spread.dot(reach)).normalized()
    digits = []
    for t, r0 in zip(w.tips, w.r_finger):
        root = W + (P(*t) - W).normalized() * w.fan
        digits.append((root, P(*t) - root, r0 * 1.05))
    fan, octs, slots = Grow.fan_end(digits, N, spread, SKIN)
    start = pts[-1]
    for t in HAND:                                                     # the blend
        pts.append([a.lerp(b, t) for a, b in zip(start, fan)])
    pts.append(fan)
    rings = g.rings_of(pts, SCALE)
    g.ladder(rings[0], SCALE)                                          # the hidden root
    octv, web_roots = g.fan_cap(rings[-1], octs, slots, SCALE)
    fingers = []
    for (root, d, _), t, r0, ov in zip(digits, w.tips, w.r_finger, octv):
        run = P(*t) - root
        fr = g.rings_of([g.seam_ring(root + run * f, d, N, spread, r0 * q, r0 * q, 2, SKIN) for f, q in FINGER],
                        SCALE, first=ov)
        g.ladder(fr[-1], SCALE)
        fingers.append(fr)
    bmesh.ops.recalc_face_normals(g.bm, faces=g.bm.faces[:])
    g.bm.normal_update()

    # the thumb, out of the two faces either side of the first corner over the top, by the wrist
    a, b = rings[PROPATAGIUM_END], rings[PROPATAGIUM_END + 1]
    lead = [f for f in a[1].link_faces if b[1] in f.verts]
    base = sum((f.calc_center_median() for f in lead), Vector()) / len(lead)
    out = sum((f.normal for f in lead), Vector()).normalized()
    centre = sum((v.co for v in a), Vector()) / len(a)
    if out.dot(base - centre) < 0:
        out = -out
    p1 = base + out * 0.06 * k
    p2 = p1.lerp(P(*w.thumb), 0.5) + out * 0.03 * k
    thumb = g.extrude(lead, [(-p1.y, p1.x, p1.z, 0.055 * k, 0.055 * k, 0.055 * k)], SCALE)
    g.extrude(thumb, [(-p2.y, p2.x, p2.z, 0.035 * k, 0.035 * k, 0.035 * k)], HORN, tip=w.thumb, blunt=0.005 * k)

    # the seams the membrane grows from, from the hidden root ring inside the body (the panels' first
    # edges, to the flank, stay under its skin)
    lead_arm = [Grow.lead_seam(r) for r in rings[0:PROPATAGIUM_END + 1]]
    trail = [Grow.far_seam(r) for r in rings[0:]] + [Grow.far_seam(r) for r in fingers[-1][1:]]
    gaps = [([Grow.far_seam(r) for r in fingers[i]], [Grow.lead_seam(r) for r in fingers[i + 1]])
            for i in range(len(fingers) - 1)]
    for seam in [lead_arm, trail] + [x for pair in gaps for x in pair]:
        g.open_seam(seam)

    def mid(pair):
        return (pair[0].co + pair[1].co) / 2

    def onto_body(p):                              # a point on the flank, sunk a little under its skin
        cz = w.core(-p.y) if callable(w.core) else w.core   # a leaning body: its middle's height by f
        inward = (Vector((0.0, p.y, cz)) - p).normalized()
        hit = bvh.ray_cast(p - inward * 2.0, inward)[0]
        return hit + inward * 0.12 * k if hit is not None else p
    down = Vector((0, 0, 0.15 * k))
    for (A, B), first in zip(gaps, web_roots):     # between the fingers
        ta, tb = mid(A[-1]), mid(B[-1])
        dip = ((ta + tb) / 2).lerp(W, 0.3) - down
        g.web(A, B, WING, N, J=2, first=first, last=[ta, dip, tb], sag=lambda s, u: 0.14 * k * 4 * u * (1 - u) * s)
    m = len(trail)                                 # behind the arm and the innermost finger, to the flank
    flank0, hip = P(*w.flank), P(*w.hip)
    flank = [onto_body(flank0.lerp(hip, c / (m - 1))) for c in range(m)]
    ta = mid(trail[-1])
    ctrl = through(ta, ((ta + flank[-1]) / 2).lerp(W, 0.2) - down * 0.7, flank[-1])
    edge = [bezier(ta, ctrl, flank[-1], j / 4) for j in range(5)]
    g.web(trail, flank, WING, N, J=4, last=edge, sag=lambda s, u: 0.16 * k * 4 * u * (1 - u) * s)
    m = len(lead_arm)                              # in front of the elbow, the leading edge taut
    front, end = onto_body(P(*w.front)), mid(lead_arm[-1])
    ctrl = through(front, P(*w.lead), end)
    g.web(lead_arm, [bezier(front, ctrl, end, c / (m - 1)) for c in range(m)], WING, N, J=2,
          sag=lambda s, u: 0.04 * k * 4 * u * (1 - u))
    bmesh.ops.recalc_face_normals(g.bm, faces=g.bm.faces[:])
    return g.bm
