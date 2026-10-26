"""Standalone helms: historical shapes (docs/research/armour.md) built as low-poly shells, each on a
wooden stand, to judge the craft apart from the character.

A helm is a shell lofted through rings round the head: each ring a height t (shares of chin to
crown, H), a half width, a front and a back (shares of H from the head's middle), how square its
front half is, and how far its middle is shifted back (for tails). Openings are cut as holes; the
shell gets real thickness, its inside dark, so every opening shows a rim and a shadow. Strips,
combs and studs are laid onto the surface by `surf`, the same function the loft uses, so they sit
on it. Two tones of the metal (the lighter "Steel", the darker "SteelShade"), one dark, flat
shading, few polygons.

Run from the MCP:
    import runpy; ns = runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\armour\\helms.py")
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

H = 0.25                       # chin to crown of the head inside, metres
THICK = 0.006                  # the shell
LIGHT, DARK, INSIDE, ACCENT, WOOD = range(5)
COLL = "Helms"


# ---- the shell ----

def ring_at(rings, t):
    """A ring's parameters at height t, run straight between the given rings."""
    if t <= rings[0][0]:
        return rings[0][1:]
    for r0, r1 in zip(rings, rings[1:]):
        if t <= r1[0]:
            k = (t - r0[0]) / (r1[0] - r0[0])
            return tuple(a + (b - a) * k for a, b in zip(r0[1:], r1[1:]))
    return rings[-1][1:]


def outline(a, w, f, b, p):
    """The ring's outline at angle a (radians, 0 the front, + towards the left): a superellipse
    between the front and the side (exponent p), an ellipse between the side and the back."""
    s, c = math.sin(a), math.cos(a)
    if c >= 0:
        r = w if c < 1e-9 else 1 / ((c / f) ** p + (abs(s) / w) ** p) ** (1 / p)
    else:
        r = 1 / ((-c / b) ** 2.2 + (abs(s) / w) ** 2.2) ** (1 / 2.2)
    return r * s, r * c


def surf(rings, t, a):
    """A point on the shell (head space: +Y the front, Z up, chin at 0)."""
    w, f, b, p, dy = ring_at(rings, t)
    x, y = outline(a, w, f, b, p)
    return Vector((x * H, (y - dy) * H, t * H))


def surf_normal(rings, t, a):
    e = 1e-3
    dt = surf(rings, t + e, a) - surf(rings, t - e, a)
    da = surf(rings, t, a + e) - surf(rings, t, a - e)
    n = da.cross(dt).normalized()
    p = surf(rings, t, a)
    w, f, b, pp, dy = ring_at(rings, t)
    centre = Vector((0, -dy * H, t * H))
    return n if n.dot(p - centre) >= 0 else -n


def mirror(angles):
    """Degrees 0..180 to a ring of radians, both sides (0 the front)."""
    pos = sorted(set(angles))
    full = pos + [-a for a in reversed(pos) if 0 < a < 180]
    return [math.radians(a) for a in full]


def loft(bm, rings, ts, angles_deg, top="flat"):
    """The shell's outer surface through the heights ts and the angles. Returns faces as
    {face: (t_mid, angle_mid_degrees)} for cutting and colouring."""
    angles = mirror(angles_deg)
    grid = [[bm.verts.new(surf(rings, t, a)) for a in angles] for t in ts]
    n = len(angles)
    where = {}
    for i in range(len(ts) - 1):
        for j in range(n):
            k = (j + 1) % n
            f = bm.faces.new((grid[i][j], grid[i][k], grid[i + 1][k], grid[i + 1][j]))
            a0, a1 = angles[j], angles[k]
            if abs(a1 - a0) > math.pi:
                a1 += 2 * math.pi if a1 < a0 else -2 * math.pi
            mid = math.degrees((a0 + a1) / 2)
            mid = (mid + 180) % 360 - 180
            where[f] = ((ts[i] + ts[i + 1]) / 2, mid)
    if top is not None:
        last = ts[-1]
        w, fr, b, p, dy = ring_at(rings, last)
        apex = bm.verts.new(Vector((0, -dy * H, (last if top == "flat" else top) * H)))
        for j in range(n):
            f = bm.faces.new((grid[-1][j], grid[-1][(j + 1) % n], apex))
            where[f] = (last + 0.01, 0.0)
    bmesh.ops.recalc_face_normals(bm, faces=list(where))
    return where


def solidify(bm, faces, thick=THICK):
    """Give the shell (these faces) thickness inwards: an inside skin, dark, and a rim round every
    open edge in the face's own material."""
    bm.normal_update()
    verts = {v for f in faces for v in f.verts}
    inner = {v: bm.verts.new(v.co - v.normal * thick) for v in verts}
    rim = [(lp.vert, lp.link_loop_next.vert, f.material_index) for f in faces for lp in f.loops
           if lp.edge.is_boundary]
    for f in faces:
        bm.faces.new([inner[v] for v in reversed(f.verts)]).material_index = INSIDE
    for a, b, m in rim:
        bm.faces.new((b, a, inner[a], inner[b])).material_index = m


# ---- things laid on the shell ----

def strip(bm, rings, path, width, raise_, mat, taper=None):
    """A raised band along a path of (t, a) points on the shell: a flat-topped strip `width` wide
    standing `raise_` off the surface (from just inside it)."""
    secs = []
    pts = [surf(rings, t, a) for t, a in path]
    for i, (t, a) in enumerate(path):
        p, n = pts[i], surf_normal(rings, t, a)
        tangent = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = tangent.cross(n).normalized()
        w = width * (taper(i / (len(path) - 1)) if taper else 1.0)
        secs.append([p - n * 0.002 - side * w / 2, p + n * raise_ - side * w / 2,
                     p + n * raise_ + side * w / 2, p - n * 0.002 + side * w / 2])
    vs = [[bm.verts.new(c) for c in s] for s in secs]
    for s0, s1 in zip(vs, vs[1:]):
        for k in range(3):
            bm.faces.new((s0[k], s1[k], s1[k + 1], s0[k + 1])).material_index = mat
    for s, flip in ((vs[0], True), (vs[-1], False)):
        f = bm.faces.new(list(reversed(s)) if flip else s)
        f.material_index = mat


def on(rings, path):
    """(t, a) points on the shell as (point, normal)."""
    return [(surf(rings, t, a), surf_normal(rings, t, a)) for t, a in path]


def midline(rings, t_front, t_back, top, step=0.04):
    """The path up the front's middle from t_front, over the crown (through the loft's top point)
    and down the back's middle to t_back, as (point, normal)."""
    last = rings[-1][0]
    front = [t_front + step * k for k in range(int((last - t_front) / step) + 1)] + [last]
    back = [last - step * k for k in range(int((last - t_back) / step) + 1)][1:] + [t_back]
    w, f, b, p, dy = ring_at(rings, last)
    crown = Vector((0, -dy * H, top * H))
    up = Vector((0, 0, 1))
    pts = on(rings, [(t, 0.0) for t in front])
    pts.append((crown, up))
    pts += on(rings, [(t, math.pi) for t in back])
    return pts


def fin(bm, path, height, base, thick, mat):
    """A comb or crest standing along a path of (point, normal) on the shell; height(u) for u 0..1
    along it."""
    pts = [p for p, n in path]
    secs = []
    for i, (p, n) in enumerate(path):
        tangent = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = tangent.cross(n).normalized()
        h = height(i / (len(path) - 1))
        secs.append([p - n * 0.003 - side * base / 2, p + n * h - side * thick / 2,
                     p + n * h + side * thick / 2, p - n * 0.003 + side * base / 2])
    vs = [[bm.verts.new(c) for c in s] for s in secs]
    for s0, s1 in zip(vs, vs[1:]):
        for k in range(3):
            bm.faces.new((s0[k], s1[k], s1[k + 1], s0[k + 1])).material_index = mat
    bm.faces.new(list(reversed(vs[0]))).material_index = mat
    bm.faces.new(vs[-1]).material_index = mat


def stud(bm, rings, t, a, r, mat, height=None):
    """A round-headed rivet or boss on the shell."""
    p, n = surf(rings, t, a), surf_normal(rings, t, a)
    u = n.cross(Vector((0, 0, 1)))
    u = u.normalized() if u.length > 1e-6 else Vector((1, 0, 0))
    v = n.cross(u)
    h = height if height is not None else r * 0.6
    ring = [u * math.cos(k * math.pi / 3) + v * math.sin(k * math.pi / 3) for k in range(6)]
    A.hull(bm, [p - n * 0.002 + q * r for q in ring] + [p + n * h * 0.7 + q * r * 0.6 for q in ring]
           + [p + n * h], mat)


def mark(bm, rings, t, a, w, h, mat):
    """A small dark slot on the shell (a breath): a thin slab, solid, so it faces out."""
    p, n = surf(rings, t, a), surf_normal(rings, t, a)
    u = n.cross(Vector((0, 0, 1))).normalized()
    up = n.cross(u).normalized() * -1 if n.cross(u).z < 0 else n.cross(u).normalized()
    corners = [p + u * dx + up * dz for dx, dz in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2))]
    A.hull(bm, [c - n * 0.001 for c in corners] + [c + n * 0.0015 for c in corners], mat)


# ---- the helms ----
# rings: (t, half width, front, back, squareness, shift back), all shares of H.

def great_helm(bm):
    """A flat-topped can: eye slits either side of a raised cross, breaths on the right."""
    rings = [(-0.10, .37, .43, .47, 3.0, 0), (0.30, .40, .47, .50, 3.2, 0), (0.70, .40, .46, .50, 3.2, 0),
             (1.02, .37, .43, .47, 3.0, 0)]
    ts = [-0.10, 0.10, 0.30, 0.48, 0.535, 0.70, 0.88, 1.02]
    where = loft(bm, rings, ts, [0, 9, 25, 45, 64, 90, 120, 150, 180])
    holes = [f for f, (t, a) in where.items() if 0.48 < t < 0.535 and 9 < abs(a) < 64]
    bmesh.ops.delete(bm, geom=holes, context="FACES")
    faces = [f for f in where if f.is_valid]
    for f in faces:
        f.material_index = LIGHT
    solidify(bm, faces)
    # The cross: a band down the face and one round the brow, in the darker tone.
    strip(bm, rings, [(t, 0.0) for t in [x / 20 for x in range(-1, 21)]], 0.035, 0.005, DARK)
    strip(bm, rings, [(0.565, math.radians(a)) for a in range(-80, 81, 8)], 0.026, 0.004, DARK)
    for t in (0.20, 0.27, 0.34):
        for a in (22, 30, 38):
            mark(bm, rings, t, math.radians(-a), 0.007, 0.016, INSIDE)        # breaths, the right side
    for a in (-60, -30, 30, 60):
        stud(bm, rings, 0.565, math.radians(a), 0.006, LIGHT)
    return rings


def hounskull(bm):
    """A bascinet with a pointed skull and a snouted visor, over a mail aventail."""
    rings = [(0.00, .34, .40, .44, 2.2, 0), (0.40, .37, .43, .49, 2.2, 0), (0.70, .34, .40, .48, 2.2, 0.03),
             (0.92, .22, .25, .34, 2.2, 0.06), (1.10, .08, .09, .13, 2.2, 0.10)]
    ts = [0.00, 0.20, 0.40, 0.55, 0.70, 0.82, 0.92, 1.02, 1.10]
    where = loft(bm, rings, ts, [0, 15, 32, 50, 70, 90, 115, 140, 160, 180], top=1.24)
    faces = list(where)
    for f in faces:
        f.material_index = LIGHT
    solidify(bm, faces)
    # The visor: a snout drawn out forward to a point. Its base is a loop on the face (from the brow
    # to the chin, round to the temples); rings shrink towards the point as they go forward.
    loop = []
    t0, t1, side = 0.06, 0.62, math.radians(64)
    for k in range(5):                                         # along the top, left to right
        loop.append((t1, side - 2 * side * k / 4))
    for k in range(1, 4):                                      # down the right side
        loop.append((t1 - (t1 - t0) * k / 4, -side))
    for k in range(5):                                         # along the bottom, right to left
        loop.append((t0, -side + 2 * side * k / 4))
    for k in range(1, 4):                                      # up the left side
        loop.append((t0 + (t1 - t0) * k / 4, side))
    base = [surf(rings, t, a) for t, a in loop]
    tip = surf(rings, 0.36, 0.0) + Vector((0, 0.30 * H, -0.02 * H))
    centre = sum(base, Vector()) / len(base)
    steps = [(0.0, 0.0), (0.30, 0.10), (0.62, 0.32)]           # (share of the way to the tip, lift off the face)
    snout = []
    for k, (u, lift) in enumerate(steps):
        ring = []
        for q in base:
            n = (q - centre)
            p0 = q + Vector((0, 0.006, 0))
            pt = p0.lerp(tip, u)
            ring.append(bm.verts.new(pt))
        snout.append(ring)
    apex = bm.verts.new(tip)
    m = len(loop)
    grid = {}
    for i, (r0, r1) in enumerate(zip(snout, snout[1:])):
        for j in range(m):
            grid[(i, j)] = bm.faces.new((r0[j], r0[(j + 1) % m], r1[(j + 1) % m], r1[j]))
    for j in range(m):
        grid[(len(snout) - 1, j)] = bm.faces.new((snout[-1][j], snout[-1][(j + 1) % m], apex))
    sfaces = list(grid.values())
    bmesh.ops.recalc_face_normals(bm, faces=sfaces)
    for f in sfaces:
        f.material_index = LIGHT
    # The eye slits: the outer two of the four top faces nearest the face; breaths: the next
    # band down on the right-hand side of the bottom.
    for j in (0, 3):
        grid[(0, j)].material_index = INSIDE
    for j in (9, 10):
        grid[(1, j)].material_index = INSIDE
    for sd in (1, -1):
        stud(bm, rings, 0.48, sd * math.radians(86), 0.012, DARK, height=0.008)
    # The aventail: mail from the helm's lower edge flaring to the shoulders, a zigzag hem.
    mail = [(0.00, 1.0, 0.0), (-0.22, 1.25, 0.0), (-0.42, 1.65, 0.0)]
    angs = mirror([0, 15, 32, 50, 70, 90, 115, 140, 160, 180])
    grid = []
    for i, (t, grow, _) in enumerate(mail):
        row = []
        for j, a in enumerate(angs):
            p = surf(rings, 0.0, a)
            c = Vector((0, 0, p.z))
            q = c + (p - c) * grow * (0.98 if i == 0 else 1.0)
            dz = (0.035 if j % 2 else 0.0) if i == len(mail) - 1 else 0.0
            row.append(bm.verts.new(Vector((q.x, q.y, t * H + dz))))
        grid.append(row)
    n = len(angs)
    mfaces = []
    for i in range(len(grid) - 1):
        for j in range(n):
            k = (j + 1) % n
            mfaces.append(bm.faces.new((grid[i][j], grid[i][k], grid[i + 1][k], grid[i + 1][j])))
    bmesh.ops.recalc_face_normals(bm, faces=mfaces)
    for f in mfaces:
        f.material_index = ACCENT
    solidify(bm, mfaces, 0.004)
    return rings


def sallet(bm):
    """A sallet with a long swept tail, and a bevor rising to meet it: the slit is the gap."""
    rings = [(-0.12, .37, .30, .92, 2.2, 0.0), (0.05, .37, .40, .74, 2.2, 0.0), (0.30, .38, .44, .58, 2.2, 0.0),
             (0.60, .38, .44, .52, 2.2, 0.0), (0.85, .31, .36, .43, 2.2, 0.0), (1.00, .18, .21, .25, 2.2, 0.0)]
    ts = [-0.12, 0.05, 0.30, 0.47, 0.60, 0.75, 0.88, 1.00]
    where = loft(bm, rings, ts, [0, 15, 32, 50, 72, 95, 120, 145, 165, 180], top=1.05)
    # The face is open below the brim: the bevor closes it.
    holes = [f for f, (t, a) in where.items() if t < 0.47 and abs(a) < 72]
    bmesh.ops.delete(bm, geom=holes, context="FACES")
    faces = [f for f in where if f.is_valid]
    for f in faces:
        f.material_index = LIGHT
    solidify(bm, faces)
    # A ridge down the skull's middle from brow to tail.
    fin(bm, midline(rings, 0.62, 0.0, 1.05), lambda u: 0.006, 0.012, 0.004, LIGHT)
    # The bevor: a separate plate over the chin and throat, standing forward of the face, its top
    # edge a hand's width under the brim.
    bev = [(-0.22, .33, .34, .10, 2.2, 0.0), (0.05, .37, .47, .10, 2.4, 0.0), (0.25, .38, .52, .10, 2.6, 0.0),
           (0.43, .37, .50, .10, 2.6, 0.0)]
    bts = [-0.22, 0.05, 0.25, 0.43]
    bw = loft(bm, bev, bts, [0, 15, 32, 50, 72, 90, 100], top=None)
    back = [f for f, (t, a) in bw.items() if abs(a) > 95]
    bmesh.ops.delete(bm, geom=back, context="FACES")
    bfaces = [f for f in bw if f.is_valid]
    for f in bfaces:
        f.material_index = DARK
    solidify(bm, bfaces)
    for sd in (1, -1):
        stud(bm, bev, 0.30, sd * math.radians(80), 0.008, LIGHT)
    return rings


def barbute(bm):
    """A bowl down to the neck, opened in a T for the eyes and mouth, with a low comb."""
    rings = [(-0.08, .33, .40, .47, 2.4, 0), (0.10, .36, .45, .50, 2.6, 0), (0.40, .38, .46, .50, 2.6, 0),
             (0.70, .37, .44, .49, 2.4, 0), (0.92, .27, .31, .37, 2.2, 0), (1.03, .13, .15, .18, 2.2, 0)]
    ts = [-0.08, 0.10, 0.25, 0.43, 0.52, 0.66, 0.82, 0.92, 1.03]
    where = loft(bm, rings, ts, [0, 9, 26, 46, 66, 90, 120, 150, 180], top=1.07)
    holes = [f for f, (t, a) in where.items()
             if (0.43 < t < 0.52 and abs(a) < 46) or (0.10 < t < 0.43 and abs(a) < 9)]
    bmesh.ops.delete(bm, geom=holes, context="FACES")
    faces = [f for f in where if f.is_valid]
    for f in faces:
        f.material_index = LIGHT
    solidify(bm, faces)
    # The opening's edge, turned out: a band round the T in the darker tone.
    strip(bm, rings, [(0.545, math.radians(a)) for a in range(-52, 53, 6)], 0.016, 0.004, DARK)
    strip(bm, rings, [(t / 100, math.radians(13)) for t in range(8, 43, 5)], 0.012, 0.004, DARK)
    strip(bm, rings, [(t / 100, math.radians(-13)) for t in range(8, 43, 5)], 0.012, 0.004, DARK)
    # The comb from the brow over the top to the nape.
    fin(bm, midline(rings, 0.58, 0.30, 1.07), lambda u: 0.012 * math.sin(math.pi * min(1.0, u * 1.05)) + 0.003,
        0.012, 0.004, LIGHT)
    return rings


def corinthian(bm):
    """Bronze: almond eyes either side of a nasal, long cheek pieces with a gap between them, a
    flared neck, a horsehair crest on a stand."""
    rings = [(-0.12, .34, .44, .58, 2.2, 0), (0.05, .33, .44, .50, 2.2, 0), (0.30, .36, .45, .49, 2.4, 0),
             (0.55, .38, .45, .50, 2.4, 0), (0.80, .34, .40, .47, 2.2, 0), (0.98, .17, .20, .24, 2.2, 0)]
    ts = [-0.12, 0.05, 0.18, 0.30, 0.40, 0.47, 0.55, 0.68, 0.82, 0.98]
    where = loft(bm, rings, ts, [0, 8, 14, 30, 46, 64, 90, 120, 150, 180], top=1.03)
    holes = [f for f, (t, a) in where.items()
             if (0.40 < t < 0.55 and 14 < abs(a) < 46) or (0.47 > t > 0.30 and 8 < abs(a) < 14)
             or (t < 0.30 and abs(a) < 8)]
    bmesh.ops.delete(bm, geom=holes, context="FACES")
    faces = [f for f in where if f.is_valid]
    for f in faces:
        f.material_index = LIGHT
    solidify(bm, faces)
    # The brow ridge over the eyes, in relief.
    strip(bm, rings, [(0.575, math.radians(a)) for a in range(-50, 51, 5)], 0.018, 0.005, LIGHT,
          taper=lambda u: 0.5 + 0.5 * math.sin(math.pi * u))
    # The crest: a stand, then a tall arc of horsehair from the brow to the nape.
    fin(bm, midline(rings, 0.70, 0.40, 1.03), lambda u: 0.02, 0.022, 0.016, DARK)
    # The horsehair: rising from the brow, highest over the crown, falling away down the back.
    def mane(u):
        rise = 1 - (1 - min(1.0, u / 0.35)) ** 2
        fall = max(0.0, 1 - (u - 0.35) / 0.65) ** 1.2
        return 0.022 + 0.085 * (rise if u < 0.35 else fall)
    fin(bm, midline(rings, 0.64, 0.12, 1.03), mane, 0.022, 0.012, ACCENT)
    return rings


def kettle_hat(bm):
    """A dome with a wide brim, a ridge over the top."""
    rings = [(0.50, .70, .76, .80, 2.0, 0), (0.56, .66, .72, .76, 2.0, 0), (0.60, .38, .44, .48, 2.0, 0),
             (0.80, .35, .41, .45, 2.0, 0), (0.95, .24, .28, .31, 2.0, 0), (1.05, .10, .12, .13, 2.0, 0)]
    ts = [0.50, 0.56, 0.60, 0.70, 0.80, 0.90, 0.98, 1.05]
    where = loft(bm, rings, ts, [0, 20, 40, 60, 80, 100, 120, 140, 160, 180], top=1.09)
    faces = list(where)
    for f in faces:
        f.material_index = DARK if where[f][0] < 0.60 else LIGHT
    solidify(bm, faces)
    fin(bm, midline(rings, 0.62, 0.62, 1.09), lambda u: 0.006, 0.014, 0.006, LIGHT)
    for k in range(8):
        stud(bm, rings, 0.63, math.radians(22.5 + 45 * k), 0.006, DARK)
    return rings


HELMS = [("Great helm", great_helm, "Steel"), ("Pig-faced bascinet", hounskull, "Steel"),
         ("Sallet and bevor", sallet, "Steel"), ("Barbute", barbute, "Steel"),
         ("Corinthian", corinthian, "Bronze"), ("Kettle hat", kettle_hat, "Steel")]
ACCENTS = {"Pig-faced bascinet": "Iron", "Corinthian": "Brick"}


def stand(bm, top_z):
    """A turned wooden stand: a round foot and a post up into the helm."""
    A.prism(bm, 8, 0.11, 0.10, 0.0, 0.025, mat_index=WOOD)
    A.prism(bm, 6, 0.018, 0.018, 0.025, top_z, mat_index=WOOD)
    A.prism(bm, 8, 0.05, 0.03, top_z, top_z + 0.02, mat_index=WOOD)


def build(spacing=0.65):
    s = bpy.context.scene
    c = bpy.data.collections.get(COLL) or bpy.data.collections.new(COLL)
    if c.name not in s.collection.children:
        s.collection.children.link(c)
    for o in list(c.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    report = {}
    lift = 0.42                                       # the head's chin above the floor
    for i, (name, make, metal) in enumerate(HELMS):
        bm = bmesh.new()
        make(bm)
        bmesh.ops.translate(bm, verts=list(bm.verts), vec=Vector((0, 0, lift)))
        stand(bm, lift + 0.30 * H)
        A.finish(bm)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for m in (metal, metal + "Shade", "Blackiron", ACCENTS.get(name, metal + "Shade"), "Timber"):
            me.materials.append(A.mat(m))
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        c.objects.link(ob)
        ob.location = ((i - (len(HELMS) - 1) / 2) * spacing, 0, 0)
        report[name] = A.report([ob])
    return report


if __name__ == "__main__" or "result" not in globals():
    result = build()
