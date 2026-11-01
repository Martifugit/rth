"""Grow a creature as one mesh, the way a modeller box-models one: start from a tube, extrude the
limbs, neck, head, tail, ears and nose out of its faces, and add rings where more shape is needed
(the creature study, 2026-10-29: parts modelled as separate blocks and pushed together read as
blocks, never as one creature).

Everything is placed by numbers in the creature's own frame (f forward, s to its left, u up;
metres, 1 tile = 1 m; the creature faces Blender -Y). A ring is (f, s, u, w, top, bottom): its
middle, its half-width, and how far it reaches above and below its middle (on an upright tube, in
front and behind). An extrusion's rings are placed the same way, its last point may be a tip.

    g = Grow(sides=8)
    body = g.tube([ring, ring, ...], mat)                 # rings along a path, capped at both ends
    g.extrude(g.cap(body, "end"), [ring, ...], mat)       # a neck and head out of the front
    g.extrude([g.face(point, toward)], [ring, ...], mat)  # a leg out of the face nearest a point
    ob = g.make("Wolf", colours)
"""

import math

import bmesh
from mathutils import Vector

import rth_art as A


def P(f, s, u):
    return Vector((s, -f, u))


class Tube:
    def __init__(self, rings, caps):
        self.rings, self.caps = rings, caps


class Grow:
    def __init__(self, sides=8):
        self.bm = bmesh.new()
        self.sides = sides

    # -- frames and rings --------------------------------------------------------------------------

    @staticmethod
    def frame(d, up_hint, prev_side=None, prev_up=None):
        """Axes across a ring facing along d: `side` and `up` (up as close to up_hint as it can be);
        carried on from the previous ring's side (and up) so the ring doesn't twist or flip."""
        d = d.normalized()
        if prev_side is not None:
            side = (prev_side - d * prev_side.dot(d))
            if side.length > 1e-6:
                side.normalize()
                up = d.cross(side).normalized()
                return side, up * (1 if up.dot(prev_up if prev_up is not None else up_hint) >= 0 else -1)
        up = (up_hint - d * up_hint.dot(d))
        if up.length < 1e-6:
            up = Vector((0, -1, 0)) - d * Vector((0, -1, 0)).dot(d)
        up.normalize()
        return up.cross(d).normalized(), up

    @staticmethod
    def place(c, side, up, w, top, bottom, angle):
        """A point of a ring: at `angle` round it, out to its width across and its top or bottom."""
        x, y = math.cos(angle), math.sin(angle)
        return c + side * (w * x) + up * ((top if y >= 0 else bottom) * y)

    # -- the tube ----------------------------------------------------------------------------------

    def tube(self, rings, mat, upright=False, split_caps=False):
        """Rings along a path (each (f, s, u, w, top, bottom)), faces between them, both ends capped.
        upright: the rings' 'top' faces forward (a standing trunk). split_caps: each cap as three
        parts (one side, the middle, the other side) for legs to grow from (sides a multiple of four)."""
        hint = Vector((0, -1, 0)) if upright else Vector((0, 0, 1))
        cs = [P(*r[:3]) for r in rings]
        verts, side = [], None
        for i, r in enumerate(rings):
            d = cs[min(i + 1, len(cs) - 1)] - cs[max(i - 1, 0)]
            side, up = self.frame(d, hint, side)
            verts.append([self.bm.verts.new(self.place(cs[i], side, up, r[3], r[4], r[5], 2 * math.pi * (k + 0.5) / self.sides))
                          for k in range(self.sides)])
        for a, b in zip(verts, verts[1:]):
            for k in range(self.sides):
                n = (k + 1) % self.sides
                self.bm.faces.new((a[k], a[n], b[n], b[k])).material_index = mat
        caps = {}
        for key, ring, rev in (("start", verts[0], True), ("end", verts[-1], False)):
            vs = list(reversed(ring)) if rev else ring
            if split_caps and self.sides % 4 == 0:
                # one side's half of the ring, a quad across the middle, the other side's half: legs grow
                # from the halves (sides / 2 + ... corners each: four on eight sides, six on twelve)
                n, r = self.sides, ring
                q = n // 4
                one = [r[(3 * q + i) % n] for i in range(2 * q)]
                other = [r[q + i] for i in range(2 * q)]
                mid = (one[-1], other[0], other[-1], one[0])
                caps[key] = [self.bm.faces.new(tuple(reversed(p)) if rev else tuple(p)) for p in (one, mid, other)]
            else:
                caps[key] = [self.bm.faces.new(vs)]
            for f in caps[key]:
                f.material_index = mat
        return Tube(verts, caps)

    # -- picking faces -----------------------------------------------------------------------------

    def face(self, point, toward=None, allow=None):
        """The face nearest a point (f, s, u), turned toward a direction (f, s, u) if given, among the
        faces `allow` accepts (all, by default)."""
        p = P(*point)
        want = P(*toward).normalized() if toward else None
        best, score = None, 1e9
        for f in self.bm.faces:
            if not f.is_valid:
                continue
            f.normal_update()
            d = (f.calc_center_median() - p).length
            if want is not None and f.normal.dot(want) < 0.2:
                continue
            if allow is not None and not allow(f):
                continue
            if d < score:
                best, score = f, d
        return best

    def faces(self, points, toward=None):
        """The faces nearest several points: a region of neighbouring faces to grow a thicker limb from
        (two faces side by side start a six-sided limb, not a four-sided plank)."""
        return [self.face(p, toward) for p in points]

    def patch(self, points, toward=None, allow=None):
        """A region of faces to grow a limb from that is sure to hang together: the face nearest the
        first point, then for each further point the face sharing an edge with the region that is
        nearest it. (Faces picked one by one can meet only at a corner, and a region like that
        extrudes as two prongs: a split leg, a wing arm in two.)"""
        region = [self.face(points[0], toward, allow)]
        for p in points[1:]:
            q = P(*p)
            near = {nf for f in region for e in f.edges for nf in e.link_faces
                    if nf not in region and (allow is None or allow(nf))}
            region.append(min(near, key=lambda f: (f.calc_center_median() - q).length))
        return region

    @staticmethod
    def boundary(faces):
        """The corners round the outside of a region of faces, in order along its edges."""
        pool = set(faces)
        edges = [e for f in faces for e in f.edges if sum(1 for lf in e.link_faces if lf in pool) == 1]
        nxt = {}
        for e in edges:
            a, b = e.verts
            nxt.setdefault(a, []).append(b)
            nxt.setdefault(b, []).append(a)
        start = edges[0].verts[0]
        ring, prev, cur = [start], None, start
        while True:
            step = next(v for v in nxt[cur] if v is not prev)
            if step is start:
                break
            ring.append(step)
            prev, cur = cur, step
        return ring

    def split_cap(self, faces, axis, across, count):
        """Replace a limb's end (its cap faces, a ring of 2 * (count + 1) corners) with a row of
        `count` quads lying `across` (f, s, u) the limb, the first toward `across`: digits (fingers,
        toes) are each extruded out of one, the way a hand is box-modelled. axis: the way the limb
        points (f, s, u). Returns the quads, facing out along axis."""
        ring = self.boundary(faces)                # the end's real edge loop, in order (sorting corners by
        if len(ring) != 2 * (count + 1):           # angle breaks when the end is not a clean convex ring)
            raise ValueError("split_cap: a ring of %d corners can't make a row of %d quads" % (len(ring), count))
        mid = sum((v.co for v in ring), Vector()) / len(ring)
        ax = P(*axis).normalized()
        spread = P(*across)
        spread = (spread - ax * spread.dot(ax)).normalized()
        other = ax.cross(spread)
        turn = sum(((a.co - mid).dot(spread) * (b.co - mid).dot(other) - (b.co - mid).dot(spread) * (a.co - mid).dot(other))
                   for a, b in zip(ring, ring[1:] + ring[:1]))
        if turn < 0:                               # the same way round as the angles run
            ring.reverse()
        m = len(ring)
        k0 = max(range(m), key=lambda i: (ring[i].co - mid).dot(spread) + (ring[i - 1].co - mid).dot(spread))
        ring = ring[k0:] + ring[:k0]              # ring[0] and ring[-1]: the end of the row toward `across`
        mat = faces[0].material_index
        bmesh.ops.delete(self.bm, geom=list(faces), context="FACES_ONLY")
        quads = [self.bm.faces.new((ring[i], ring[i + 1], ring[m - 2 - i], ring[m - 1 - i])) for i in range(count)]
        for q in quads:
            q.normal_update()
            if q.normal.dot(ax) < 0:
                q.normal_flip()
            q.material_index = mat
        for e in [e for e in self.bm.edges if not e.link_faces]:
            self.bm.edges.remove(e)                 # the old cap's inner edges
        return quads

    # -- limbs laid out ring by ring, and hands as fans of digits ---------------------------------
    # Every ring of such a limb has the same corners in the same order: corner 0 on the `lead` side,
    # then over the top (toward `up`), the far side, and back underneath. Nothing is re-sorted or
    # fitted afterwards, so the edge flow runs unbroken from the root to the digits.

    @staticmethod
    def ring_at(centre, axis, up, lead, w, t, n):
        """n points round `centre`, square to `axis` (Blender vectors): out to w toward `lead` and the
        far side, to t toward `up` and below."""
        a = axis.normalized()
        u = (up - a * up.dot(a)).normalized()
        side = a.cross(u)
        if side.dot(lead) < 0:
            side = -side
        return [centre + side * (w * math.cos(2 * math.pi * k / n)) + u * (t * math.sin(2 * math.pi * k / n))
                for k in range(n)]

    def rings_of(self, point_rings, mat, first=None):
        """Vertices for each ring of points (all with the same count) and quads between consecutive
        rings, after an existing ring `first` if given. Returns the rings of vertices."""
        rings = ([first] if first is not None else []) + [[self.bm.verts.new(p) for p in pts] for pts in point_rings]
        for a, b in zip(rings, rings[1:]):
            n = len(a)
            for k in range(n):
                self.bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])).material_index = mat
        return rings

    def ladder(self, ring, mat):
        """Close a ring with a row of quads across it, from the edge ring[-1]-ring[0] to the opposite
        edge (an even ring of 2m corners gives m - 1 quads)."""
        m = len(ring)
        return [self.bm.faces.new((ring[i], ring[i + 1], ring[m - 2 - i], ring[m - 1 - i])) for i in range(m // 2 - 1)]

    @staticmethod
    def seam_ring(centre, axis, up, lead, w, t, arc, skin):
        """A ring with a seam on each side for a web to grow from: [lead top, `arc` corners over the
        top from the lead side to the far side, far top, far bottom, `arc` corners back underneath,
        lead bottom], 4 + 2 * arc corners. A seam is a pair of corners `skin` above and below the
        ring's middle plane (at most 0.6 of t): a web's two skins start from them."""
        a = axis.normalized()
        u = (up - a * up.dot(a)).normalized()
        side = a.cross(u)
        if side.dot(lead) < 0:
            side = -side
        h = min(skin, 0.6 * t)
        ang = [math.pi * i / (arc + 1) for i in range(1, arc + 1)]
        top = [centre + side * (w * math.cos(x)) + u * (t * math.sin(x)) for x in ang]
        bottom = [centre + side * (w * math.cos(x)) - u * (t * math.sin(x)) for x in reversed(ang)]
        return ([centre + side * w + u * h] + top + [centre - side * w + u * h, centre - side * w - u * h]
                + bottom + [centre + side * w - u * h])

    @staticmethod
    def lead_seam(ring):
        """A seam ring's pair on its lead side."""
        return ring[0], ring[-1]

    @staticmethod
    def far_seam(ring):
        """A seam ring's pair on its far side."""
        k = (len(ring) - 4) // 2 + 1
        return ring[k], ring[k + 1]

    @classmethod
    def fan_end(cls, digits, up, lead, skin):
        """The end of a hand whose digits leave side by side, for a tube of seam rings: digits are
        (root, direction, radius) from the lead side, each a seam ring of 8 square to its direction at
        its root. Returns (outline, octagons, slots): the outline is the end ring (4 + 2 * (3k - 1)
        corners for k digits: their tops, a web point between each pair, the last digit's far seam,
        their bottoms, the first's lead seam) and slots say which outline corner is which."""
        octs = [cls.seam_ring(root, d, up, lead, r, r, 2, skin) for root, d, r in digits]
        k = len(octs)
        slots, out = {}, []

        def put(key, p):
            slots[key] = len(out)
            out.append(p)
        put((0, 0), octs[0][0])
        for i in range(k):
            put((i, 1), octs[i][1])
            put((i, 2), octs[i][2])
            if i < k - 1:
                put(("wt", i), (octs[i][2] + octs[i + 1][1]) / 2)
        put((k - 1, 3), octs[-1][3])
        put((k - 1, 4), octs[-1][4])
        for i in range(k - 1, -1, -1):
            put((i, 5), octs[i][5])
            put((i, 6), octs[i][6])
            if i > 0:
                put(("wb", i - 1), (octs[i - 1][5] + octs[i][6]) / 2)
        put((0, 7), octs[0][7])
        return out, octs, slots

    def fan_cap(self, ring, octs, slots, mat):
        """Close a fan_end ring between its digits, leaving each digit's octagon open for its tube and,
        between each pair, the gap between the facing seams for a web. Returns each digit's root ring
        (8 vertices) and, for each gap, the web's root: [(far seam), (middle), (next digit's lead seam)]."""
        octv = [[ring[slots[(i, c)]] if (i, c) in slots else self.bm.verts.new(o[c]) for c in range(8)]
                for i, o in enumerate(octs)]
        roots = []
        for i in range(len(octs) - 1):
            a, b = octv[i], octv[i + 1]
            wt, wb = ring[slots[("wt", i)]], ring[slots[("wb", i)]]
            mt = self.bm.verts.new((a[3].co + b[0].co) / 2)
            mb = self.bm.verts.new((a[4].co + b[7].co) / 2)
            for q in ((a[2], wt, mt, a[3]), (wt, b[1], b[0], mt), (a[4], mb, wb, a[5]), (mb, b[7], b[6], wb)):
                self.bm.faces.new(q).material_index = mat
            roots.append([(a[3], a[4]), (mt, mb), (b[0], b[7])])
        return octv, roots

    def open_seam(self, pairs):
        """Remove a tube's faces between consecutive seam pairs, where a web grows from them."""
        dead = []
        for p, q in zip(pairs, pairs[1:]):
            want = {p[0], p[1], q[0], q[1]}
            dead += [f for f in p[0].link_faces if set(f.verts) == want]
        bmesh.ops.delete(self.bm, geom=dead, context="FACES_ONLY")
        for e in [e for e in self.bm.edges if not e.link_faces]:
            self.bm.edges.remove(e)

    def web(self, A, B, mat, normal, J=2, first=None, last=None, sag=None, skin=0.015):
        """A web (a wing's membrane, a webbed foot, a fin) as a thin closed sheet of quads grown from
        seams. A: the seam pairs it grows from along one side. B: the seam pairs along the other side,
        or points (a free edge, or one sunk in a body), as many as A. first: the J + 1 pairs it starts
        from (a hand between two digits), else a free edge from A[0] to B[0]. last: J + 1 points of its
        free far edge, else it narrows to A[-1] (J even). The inside is a Coons patch of the four
        sides, sagging by sag(s, u) along -normal (s along A, u across); free edges get a rim."""
        n = normal.normalized()
        m = len(A)

        def mid(p):
            return (p[0].co + p[1].co) / 2

        def half(p):
            return (p[0].co - p[1].co).length / 2
        attached = not isinstance(B[0], Vector)
        Am, hA = [mid(p) for p in A], [half(p) for p in A]
        Bm = [mid(p) for p in B] if attached else [Vector(p) for p in B]
        hB = [half(p) for p in B] if attached else [skin] * m
        run = [0.0]
        for p, q in zip(Am, Am[1:]):
            run.append(run[-1] + (q - p).length)
        S = [x / run[-1] for x in run]
        C0 = [mid(p) for p in first] if first else [Am[0].lerp(Bm[0], j / J) for j in range(J + 1)]
        narrow = last is None
        C1 = [Am[-1]] * (J + 1) if narrow else [Vector(p) for p in last]
        V = [[None] * (J + 1) for _ in range(m)]
        for c in range(m):
            s = S[c]
            for j in range(J + 1):
                u = j / J
                if j == 0 or (c == m - 1 and narrow):
                    V[c][j] = A[c]
                elif c == 0 and first:
                    V[c][j] = first[j]
                elif j == J and attached:
                    V[c][j] = B[c]
                else:
                    p = ((1 - u) * Am[c] + u * Bm[c] + (1 - s) * C0[j] + s * C1[j]
                         - ((1 - s) * (1 - u) * Am[0] + (1 - s) * u * Bm[0] + s * (1 - u) * Am[-1] + s * u * Bm[-1]))
                    if sag and j < J:
                        p = p - n * sag(s, u)
                    h = hA[c] * (1 - u) + hB[c] * u
                    V[c][j] = (self.bm.verts.new(p + n * h), self.bm.verts.new(p - n * h))

        def quad(a, b, c, d):
            self.bm.faces.new((a[0], b[0], c[0], d[0])).material_index = mat
            self.bm.faces.new((d[1], c[1], b[1], a[1])).material_index = mat

        def rim(chain):
            for p, q in zip(chain, chain[1:]):
                self.bm.faces.new((p[0], q[0], q[1], p[1])).material_index = mat
        for c in range(m - 1):
            if narrow and c + 1 == m - 1:
                for j in range(J, 0, -2):                # the narrow end: quads fanned from the last pair
                    quad(A[m - 1], V[c][j], V[c][j - 1], V[c][j - 2])
            else:
                for j in range(J):
                    quad(V[c][j], V[c + 1][j], V[c + 1][j + 1], V[c][j + 1])
        if not first:
            rim([V[0][j] for j in range(J + 1)])
        if not attached:
            rim(([V[c][J] for c in range(m - 1)] + [A[m - 1]]) if narrow else [V[c][J] for c in range(m)])
        if not narrow:
            rim([V[m - 1][j] for j in range(J + 1)])

    def inset(self, face, keep=0.03):
        """Shrink a face to a small one in its middle (about `keep` across), ringed by the rest of it:
        a thin spine or horn extruded from that grows out of the surface instead of tenting it."""
        short = min(e.calc_length() for e in face.edges)
        bmesh.ops.inset_individual(self.bm, faces=[face], thickness=max(0.0, short / 2 - keep / 2), depth=0.0)
        c = face.calc_center_median()              # a long face leaves a long strip: draw it in to a small
        for v in face.verts:                       # face, as wide one way as the other
            d = v.co - c
            if d.length > keep / 2:
                v.co = c + d.normalized() * (keep / 2)
        return face

    @staticmethod
    def connected(faces):
        """Whether faces form one region joined edge to edge."""
        faces = [f for f in faces if f is not None and f.is_valid]
        if not faces:
            return True
        seen, stack, pool = {faces[0]}, [faces[0]], set(faces)
        while stack:
            for e in stack.pop().edges:
                for nf in e.link_faces:
                    if nf in pool and nf not in seen:
                        seen.add(nf)
                        stack.append(nf)
        return len(seen) == len(pool)

    def band(self, tube, i):
        """The faces between a tube's rings i and i + 1, in order round it: face k spans corners k
        and k + 1, so on a lying tube it faces (k + 1) / sides of a turn from the creature's left
        (0: left, a quarter: up, a half: right). Ask before extruding from that stretch."""
        a, b, n = tube.rings[i], tube.rings[i + 1], self.sides
        out = []
        for k in range(n):
            want = {a[k], a[(k + 1) % n], b[k]}
            out.append(next(f for f in a[k].link_faces if want <= set(f.verts)))
        return out

    def nearest(self, faces, point):
        """Of some faces (a split cap), the one nearest a point."""
        p = P(*point)
        return min((f for f in faces if f.is_valid), key=lambda f: (f.calc_center_median() - p).length)

    # -- extruding ---------------------------------------------------------------------------------

    def extrude(self, faces, rings, mat, tip=None, up=None, blunt=None, across=(0, 1, 0)):
        """As below; with `blunt` (a size) the tip is a last tiny ring closed with quads instead of a
        point, so a spine, claw or nose ends without a fan of triangles. A closing ring of more than
        four corners becomes a row of quads lying `across` (sideways by default: symmetric on the
        centre line)."""
        if tip is not None and blunt:
            last = rings[-1] if rings else None
            end = self._extrude(faces, list(rings) + [tuple(tip) + (blunt, blunt, blunt)], mat, None, up)
            ring = {v for f in end for v in f.verts}
            if len(ring) > 4 and len(ring) % 2 == 0:
                axis = (tip[0] - last[0], tip[1] - last[1], tip[2] - last[2]) if last else (1, 0, 0)
                end = self.split_cap(end, axis, across, len(ring) // 2 - 1)
            return end
        return self._extrude(faces, rings, mat, tip, up)

    def _extrude(self, faces, rings, mat, tip=None, up=None):
        """Extrude a region of faces through rings (f, s, u, w, top, bottom), one extrusion a ring:
        each new ring is the region's edge moved to the ring's middle and fitted round it. tip: a
        point (f, s, u) the last ring closes to (an ear, a claw, a nose). Returns the end faces.
        A ring's `top` is toward `up` (f, s, u): by default up for a part growing sideways, forward
        for one growing up or down; the frame is chosen once and carried along the part, so a
        neck that bends from upright to forward keeps its face where it was."""
        self.last_rings = []                       # the rings this extrusion made, in order
        region = [f for f in faces if f is not None and f.is_valid]
        if not self.connected(region):
            raise ValueError("extrude: the faces don't join edge to edge (pick them with patch)")
        verts = list({v for f in region for v in f.verts})
        start = sum((v.co for v in verts), Vector()) / len(verts)
        for f in region:                           # a fresh face's stored normal may be stale
            f.normal_update()
        normal = sum((f.normal for f in region), Vector()).normalized()
        hint = P(*up).normalized() if up else (Vector((0, 0, 1)) if abs(normal.z) < 0.9 else Vector((0, -1, 0)))
        side, upv = self.frame(normal, hint)
        prev_c = start
        for i, r in enumerate(rings):
            c = P(*r[:3])
            d = c - prev_c if (c - prev_c).length > 1e-6 else normal
            nxt = P(*rings[i + 1][:3]) if i + 1 < len(rings) else (P(*tip) if tip else c + d)
            along = (nxt - prev_c) if (nxt - prev_c).length > 1e-6 else d
            old = region
            out = bmesh.ops.extrude_face_region(self.bm, geom=old)
            new_verts = [g for g in out["geom"] if isinstance(g, bmesh.types.BMVert)]
            region = [g for g in out["geom"] if isinstance(g, bmesh.types.BMFace)]
            bmesh.ops.delete(self.bm, geom=[f for f in old if f.is_valid], context="FACES")   # the old end, now inside
            # the corners' order round the region, read in the plane of the ring before (on the first
            # ring, the region's own: one lying along the way it grows would collapse if read across
            # that way), then spaced evenly so a half ring or a strip of faces becomes a round limb
            ref_side, ref_up = side, upv
            side, upv = self.frame(along, hint, side, upv)
            # the corners in order along the region's real edge loop (sorting them by angle scrambles an
            # outline that isn't convex, an L of three faces, and the limb's faces cross: a split leg),
            # turned to run the way the angles do
            loop = self.boundary(region)
            mid = sum((v.co for v in loop), Vector()) / len(loop)
            # read the corners in the ring's own plane (Newell's normal, turned along the way it grows),
            # so a ring tilted against the frame before, or a stale face normal, can't collapse or cross
            rn = Vector()
            for a, b in zip(loop, loop[1:] + loop[:1]):
                rn += (a.co - mid).cross(b.co - mid)
            rn = rn.normalized() if rn.length > 1e-9 else along.normalized()
            if rn.dot(along) < 0:
                rn = -rn
            rs = ref_side - rn * ref_side.dot(rn)
            if rs.length < 1e-6:
                rs = ref_up - rn * ref_up.dot(rn)
            rs.normalize()
            ru = rn.cross(rs)                              # handed as the frame before is (frames may be either)
            if ref_side.cross(ref_up).dot(rn) < 0:
                ru = -ru
            ang = [math.atan2((v.co - mid).dot(ru), (v.co - mid).dot(rs)) for v in loop]
            turn = sum(((a.co - mid).dot(rs) * (b.co - mid).dot(ru) - (b.co - mid).dot(rs) * (a.co - mid).dot(ru))
                       for a, b in zip(loop, loop[1:] + loop[:1]))
            if turn < 0:
                loop.reverse()
                ang.reverse()
            k0 = min(range(len(ang)), key=lambda k: ang[k])
            angles = list(zip(ang[k0:] + ang[:k0], loop[k0:] + loop[:k0]))
            for v in new_verts:                            # a corner inside the region stays in the middle
                if v not in loop:
                    v.co = c
            n = len(angles)
            step = 2 * math.pi / n
            phase = math.atan2(sum(math.sin(a - j * step) for j, (a, _) in enumerate(angles)),
                               sum(math.cos(a - j * step) for j, (a, _) in enumerate(angles)))
            for j, (_, v) in enumerate(angles):
                v.co = self.place(c, side, upv, r[3], r[4], r[5], phase + j * step)
            self.last_rings.append([v for _, v in angles])
            for f in region:
                f.material_index = mat
            for f in self.bm.faces:                        # the new side walls take the part's colour
                if f.is_valid and any(v in new_verts for v in f.verts) and f not in region:
                    f.material_index = mat
            prev_c = c
        if tip:
            t = P(*tip)
            edge = list({v for f in region for v in f.verts})
            bmesh.ops.pointmerge(self.bm, verts=edge, merge_co=t)
            return []
        return region

    # -- finishing ---------------------------------------------------------------------------------

    def smooth(self, factor=0.35, repeat=1, keep=()):
        """Round the mesh a little (Laplacian smoothing), leaving `keep` vertices where they are."""
        keep = set(keep)
        for _ in range(repeat):
            bmesh.ops.smooth_vert(self.bm, verts=[v for v in self.bm.verts if v not in keep], factor=factor,
                                  use_axis_x=True, use_axis_y=True, use_axis_z=True)

    def paint(self, point, toward, mat, size=0.5):
        A.paint(self.bm, tuple(P(*point)), mat, facing=tuple(P(*toward)), size=size)

    def make(self, name, colours, coll="Creatures", smooth_angle=60):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        A.finish(self.bm)
        return A.mesh_from(name, self.bm, colours, coll=coll, smooth_angle=smooth_angle)
