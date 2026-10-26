"""Smithed plate: helm, body and legs in three bands, each band a step up from the one before.

The set is one shape language that grows, so a player can read a band at a glance:

  band I   (tiers 1-3)  open-faced helm with a low comb; a plain shell with a keel; one round
                        pauldron a shoulder; a short skirt; plain legs with knee cops
  band II  (tiers 4-6)  the helm closes (a T-slit) and the comb rises; pauldrons of three lames;
                        trim on the edges; tassets over the thighs; winged knees
  band III (tiers 7-9)  a tall swept crest; great pauldrons of four lames with a neck guard; a
                        high collar; long tassets; pointed knees

Each piece is a few chunky shells around the body, sized from its measured cross-sections (so
they always clear it), flat shaded. Plates have thickness, so every edge shows as an edge. Trim is
painted, never modelled: a row of faces in the trim metal.

Materials, in order: plate (recoloured per tier), trim, dark (the visor), under (leather).

Rig space (armour/fit.py): the body faces +Y, its left is -X, Z up, the T-pose's arms along X.
Round the trunk, angles go from the front (0) towards the left (+90); the back is 180."""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import fit  # noqa: E402
import rth_art as A  # noqa: E402

PLATE, TRIM, DARK, UNDER = range(4)
# How each band looks in the line-up (palette names); a tier recolours "plate" and "trim".
LOOK = {1: ("Steel", "Iron", "Sable", "Umber"),
        2: ("Steel", "Bronze", "Sable", "Umber"),
        3: ("Silver", "Gold", "Sable", "Umber")}
THICK = 0.008                  # plate thickness


# ---- building blocks ----

def sup(ang, w, f, b, p=2.4):
    """Distance from the middle to a rounded-box outline at angle ang (degrees from the front
    towards the left): half-width w, front f, back b; p over 2 squares it off."""
    r = math.radians(ang)
    c, s = math.cos(r), math.sin(r)
    d = f if c >= 0 else b
    return 1 / ((abs(c) / d) ** p + (abs(s) / w) ** p) ** (1 / p)


def out(ang):
    r = math.radians(ang)
    return Vector((-math.sin(r), math.cos(r), 0))


def grid(bm, cols, mat=PLATE, closed=True, apex=None):
    """Quads between columns of points (each column top to bottom, all the same length); closed
    joins the last column to the first; apex: a point above the first row, fanned to. Returns
    faces[j][i] (between columns j, j+1 and rows i, i+1) and the apex fan."""
    vs = [[bm.verts.new(p) for p in col] for col in cols]
    n, rows = len(cols), len(cols[0])
    faces = []
    for j in range(n if closed else n - 1):
        k = (j + 1) % n
        faces.append([bm.faces.new((vs[j][i], vs[j][i + 1], vs[k][i + 1], vs[k][i])) for i in range(rows - 1)])
    fan = []
    if apex is not None:
        top = bm.verts.new(apex)
        fan = [bm.faces.new((top, vs[(j + 1) % n][0], vs[j][0])) for j in range(n if closed else n - 1)]
    for f in [f for col in faces for f in col] + fan:
        f.material_index = mat
    return faces, fan


def orient(bm, centre_of):
    """Turn every face outwards, away from centre_of(face centre) (the axis it is built round)."""
    for f in bm.faces:
        f.normal_update()
        c = f.calc_center_median()
        if f.normal.dot(c - centre_of(c)) < 0:
            f.normal_flip()


def orient_line(bm, pts):
    """Turn every face outwards, away from the nearest point on the polyline pts (a limb's line)."""
    from mathutils.geometry import intersect_point_line

    def nearest(c):
        best = None
        for a, b in zip(pts, pts[1:]):
            p, t = intersect_point_line(c, a, b)
            p = a if t < 0 else b if t > 1 else p
            if best is None or (p - c).length < (best - c).length:
                best = p
        return best
    orient(bm, nearest)


def thicken(bm, t=THICK, lip=None):
    """Give an open shell's edges thickness, so a rim reads as a plate's edge: a wall down from
    each open edge, then (lip) a strip turned inwards under it. lip None: a whole inner skin, for
    small plates seen from both sides."""
    bm.normal_update()
    faces = list(bm.faces)
    edge = [(lp.vert, lp.link_loop_next.vert, f.material_index) for f in faces for lp in f.loops
            if lp.edge.is_boundary]
    rim = {v for a, b, _ in edge for v in (a, b)}
    inner = {v: bm.verts.new(v.co - v.normal * t) for v in (rim if lip is not None else list(bm.verts))}
    if lip is None:
        for f in faces:
            bm.faces.new([inner[v] for v in reversed(f.verts)]).material_index = f.material_index
    for a, b, m in edge:
        bm.faces.new((b, a, inner[a], inner[b])).material_index = m
    if lip is not None:
        # The strip under the rim: towards the shell's middle along the surface, back inside.
        nxt = {}
        for f in faces:
            for lp in f.loops:
                v = lp.vert
                if v in rim and v not in nxt:
                    others = [e.other_vert(v) for e in v.link_edges if not e.is_boundary and e.other_vert(v) in bm.verts]
                    if others:
                        nxt[v] = v.co + (others[0].co - v.co).normalized() * lip - v.normal * t
        deep = {v: bm.verts.new(nxt.get(v, inner[v].co)) for v in rim}
        for a, b, m in edge:
            bm.faces.new((inner[b], inner[a], deep[a], deep[b])).material_index = m


def add(dst, src):
    """Merge one bmesh into another (and free it)."""
    tmp = bpy.data.meshes.new("_add")
    src.to_mesh(tmp)
    src.free()
    dst.from_mesh(tmp)
    bpy.data.meshes.remove(tmp)


def dome(bm, centre, rx, ry, rz, mat=PLATE, flat_x=None, side=1, segs=8, rings=3, low=-35):
    """A faceted dome (a convex hull of an ellipsoid's points from `low` degrees below its middle
    to its top): pauldrons, knee cops. flat_x cuts its inner side flat at that x (towards the
    body), so it does not reach into the neck."""
    c = Vector(centre)
    pts = []
    for i in range(rings + 1):
        el = math.radians(low + (90 - low) * i / rings)
        for k in range(segs if i < rings else 1):
            az = 2 * math.pi * (k + 0.5 * (i % 2)) / segs
            p = c + Vector((math.cos(el) * math.cos(az) * rx, math.cos(el) * math.sin(az) * ry, math.sin(el) * rz))
            if flat_x is not None:
                p.x = max(p.x, flat_x) if side > 0 else min(p.x, -flat_x)
            pts.append(p)
    return A.hull(bm, pts, mat)


def fin(bm, outline, across, t, mat=PLATE):
    """A flat plate cut to an outline (points in order), t thick across `across`: crests, wings,
    neck guards."""
    d = Vector(across).normalized() * (t / 2)
    return A.hull(bm, [Vector(p) + d for p in outline] + [Vector(p) - d for p in outline], mat)


def limb(parts, a, b, t, sides=10):
    """Where a limb is, at t along the line a->b: the middle of its cross-section and the
    farthest the body reaches from that middle."""
    bvh = fit.body_bvh(parts)
    c, rs = fit.rings(bvh, a, b, [t], sides=sides, inside=True, reach=0.3)[0][0]
    _, u, s = fit.frame(Vector(a), Vector(b))
    found = sorted(r for r in rs if r > 0)
    if not found:
        return c, 0.06
    cap = found[len(found) // 2] * 1.6          # a ray that crossed into the trunk is not the limb
    pts = [c + (u * math.cos(2 * math.pi * k / sides) + s * math.sin(2 * math.pi * k / sides)) * r
           for k, r in enumerate(rs) if 0 < r <= cap]
    mid = sum(pts, Vector()) / len(pts)
    return mid, max((p - mid).length for p in pts)


def tube(bm, stations, sides=10, mat=PLATE, roll=0.0):
    """A tube through stations (centre, radius, along): rings square to the limb's line."""
    cols = [[] for _ in range(sides)]
    pts = [Vector(c) for c, _, _ in stations]
    for i, (c, r, _) in enumerate(stations):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
        t = (b - a).normalized()
        ref = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((0, 0, 1))
        u = (ref - t * ref.dot(t)).normalized()
        s = t.cross(u)
        for k in range(sides):
            ang = roll + 2 * math.pi * k / sides
            cols[k].append(Vector(c) + (u * math.cos(ang) + s * math.sin(ang)) * r)
    return grid(bm, cols, mat)


# ---- helm ----

HC = Vector((0, -0.032, 0))     # the helm's middle, front to back
HELM_COLS = [11.25 + 22.5 * k for k in range(16)]     # faces centred on the front


def helm(band):
    """A skull round the head; band I open in front with a nasal, II and III closed with a T-slit.
    The comb along the top grows with the band, into III's swept crest."""
    bm = bmesh.new()
    w, f, b = 0.138, 0.188, 0.165
    # (height, share of the full outline): crown, temples, brow, eyes, cheeks, jaw.
    levels = [(1.975, 0.42), (1.945, 0.68), (1.90, 0.86), (1.85, 0.97), (1.80, 1.0), (1.765, 1.0),
              (1.745, 0.995), (1.72, 0.985), (1.68, 0.96), (1.64, 0.93), (1.60, 0.92)]
    if band == 1:
        levels[-1] = (1.62, 0.95)
    else:
        levels.insert(-1, (1.618, 0.922))           # a thin row at the rim, for the trim
    eye_row = 5                 # faces between levels 5 and 6 (1.765-1.745): the eye slit
    cols = []
    for ang in HELM_COLS:
        fb = abs(((ang + 180) % 360) - 180)          # 0 in front, 180 behind
        col = []
        for i, (z, k) in enumerate(levels):
            r = sup(ang, w, f, b) * k
            if band > 1 and fb < 30 and 1.62 < z < 1.80:
                r += 0.012                           # the face juts a little: a prow
            if i >= len(levels) - 3 and fb > 90:
                r += 0.03 * (fb - 90) / 90 * (i - len(levels) + 4) / 3   # flared behind
            col.append(HC + out(ang) * r + Vector((0, 0, z)))
        cols.append(col)
    faces, fan = grid(bm, cols, apex=HC + Vector((0, 0, 1.99)))
    for j, ang in enumerate(HELM_COLS):
        mid = (ang + 11.25) % 360
        fb = abs(((mid + 180) % 360) - 180)
        for i, face in enumerate(faces[j]):
            z = (levels[i][0] + levels[i + 1][0]) / 2
            if band == 1:
                if fb < 70 and z < 1.765 and not (fb < 1 and z > 1.69):
                    face.tag = True                  # the open face, all but the nasal
            else:
                if (fb < 62 and i == eye_row) or (fb < 1 and i in (eye_row + 1, eye_row + 2)):
                    face.material_index = DARK       # the T-slit
                if band == 3 and i == eye_row - 1:
                    face.material_index = TRIM       # a brow band
                if band >= 2 and i == len(levels) - 2:
                    face.material_index = TRIM       # the lower rim
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.tag], context="FACES")
    orient(bm, lambda c: Vector((0, HC.y, c.z if c.z < 1.97 else 1.8)))
    thicken(bm, lip=0.025)

    # The comb: a plate standing on the skull from the brow over the top to the nape, growing
    # with the band; band III's sweeps up and back past the head.
    crest = bmesh.new()
    path = []
    for ang in [0, 180]:
        for z, k in levels[2:7] if ang == 0 else levels[2:9]:
            path.append((ang, z, sup(ang, w, f, b) * k))
    front = [HC + out(0) * r + Vector((0, 0, z)) for _, z, r in path if _ == 0]
    back = [HC + out(180) * r + Vector((0, 0, z)) for _, z, r in path if _ == 180]
    line = list(reversed(front[:4])) + [HC + Vector((0, 0, 1.99))] + back[:5]
    rise = {1: 0.022, 2: 0.045, 3: 0.075}[band]
    centre = HC + Vector((0, 0, 1.78))
    top = []
    for i, p in enumerate(line):
        t = i / (len(line) - 1)
        h = rise * math.sin(math.pi * min(1.0, t * 1.1)) + 0.004
        if band == 3:
            h = 0.02 + rise * (0.35 + 1.3 * t) * (1 - max(0, t - 0.75) * 2)
        top.append(p + (p - centre).normalized() * h)
    for i in range(len(line) - 1):                   # one hull per segment keeps it following the skull
        seg = [line[i], line[i + 1], top[i], top[i + 1]]
        seg = [p - (p - centre).normalized() * 0.012 if k < 2 else p for k, p in enumerate(seg)]
        fin(crest, seg, (1, 0, 0), 0.016 if band < 3 else 0.014, TRIM if band == 3 else PLATE)
    if band == 3:                                    # the crest's tail, swept up past the nape
        fin(crest, [line[-2], top[-2], line[-1] + Vector((0, -0.06, 0.17)), line[-1] + Vector((0, -0.10, 0.10)),
                    line[-1]], (1, 0, 0), 0.014, TRIM)
    add(bm, crest)
    hides = ["hair"] if band == 1 else ["hair", "head"]
    return bm, hides


# ---- body ----

YC = -0.05                     # the trunk's middle, front to back


def trunk_levels(band):
    """(height, half-width, front, back) round the trunk, waist to collar."""
    g = {1: 1.0, 2: 1.05, 3: 1.1}[band]
    lv = [(1.06, 0.172, 0.138, 0.122), (1.14, 0.165, 0.14, 0.116), (1.24, 0.19 * g, 0.168 * g, 0.128),
          (1.34, 0.218 * g, 0.195 * g, 0.150), (1.43, 0.222 * g, 0.176 * g, 0.156), (1.50, 0.20, 0.13, 0.142),
          (1.545, 0.13, 0.094, 0.098), (1.575, 0.082, 0.078, 0.08)]
    if band == 3:                                    # the high collar
        lv[-1] = (1.565, 0.105, 0.105, 0.105)
        lv.append((1.635, 0.118, 0.12, 0.118))
    mix = lambda a, b, t: tuple(x + (y - x) * t for x, y in zip(a, b))
    return [lv[0], mix(lv[0], lv[1], 0.3)] + lv[1:-1] + [mix(lv[-2], lv[-1], 0.7), lv[-1]]


def body(band):
    bm = bmesh.new()
    shell = bmesh.new()
    angs = [22.5 * k for k in range(16)]              # a column straight down the front: the keel
    lv = trunk_levels(band)
    keel = {1: 0.016, 2: 0.022, 3: 0.026}[band]
    cols = []
    for ang in angs:
        col = []
        for z, w, f, b in reversed(lv):
            r = sup(ang, w, f, b)
            if ang == 0 and 1.15 < z < 1.48:
                r += keel
            col.append(Vector((0, YC, 0)) + out(ang) * r + Vector((0, 0, z)))
        cols.append(col)
    faces, _ = grid(shell, cols)
    rows = len(lv) - 1
    for col in faces:
        for i, face in enumerate(col):
            if band >= 2 and i == rows - 1:
                face.material_index = TRIM           # the lower edge
            if band >= 2 and i == 0:
                face.material_index = TRIM           # the collar
    orient(shell, lambda c: Vector((0, YC, c.z)))
    thicken(shell, lip=0.03)
    add(bm, shell)

    # The skirt: lames below the waist, each a little wider than the one above.
    n = {1: 2, 2: 3, 3: 3}[band]
    z0, w0, f0, b0 = lv[0]
    for i in range(n):
        lame = bmesh.new()
        top, bot = z0 + 0.012 - i * 0.065, z0 - 0.07 - i * 0.065
        grow = 0.012 + i * 0.016
        cols = []
        for ang in angs:
            cols.append([Vector((0, YC, 0)) + out(ang) * (sup(ang, w0, f0, b0) + grow + dz) + Vector((0, 0, z))
                         for z, dz in ((top, -0.002), (bot + 0.022, 0.012), (bot, 0.018))])
        faces, _ = grid(lame, cols)
        if band >= 2 and i == n - 1:
            for col in faces:
                col[-1].material_index = TRIM
        orient(lame, lambda c: Vector((0, YC, c.z)))
        thicken(lame, lip=0.02)
        add(bm, lame)

    # The sleeves: plate round each arm, a bulge at the elbow, a flared cuff from band II.
    for side in (1, -1):
        sl = bmesh.new()
        sh, el, wr = fit.bone(f"upper_arm.{'R' if side > 0 else 'L'}"), fit.bone(
            f"forearm.{'R' if side > 0 else 'L'}"), fit.bone(f"hand.{'R' if side > 0 else 'L'}")
        st = []
        for a, b2, ts, extra in ((sh, el, (0.12, 0.45, 0.85), (0.0, 0.0, 0.004)),
                                 (el, wr, (0.08, 0.5, 0.93), (0.012, 0.0, 0.0))):
            for t, e in zip(ts, extra):
                c, r = limb(("body_arms",), a, b2, t)
                st.append((c, r + 0.016 + e, t))
        if band >= 2:
            c, r, t = st[-1]
            st[-1] = (c, r + 0.012, t)
            st.append((c + (wr - el).normalized() * 0.008, r + 0.016, t))
        faces, _ = tube(sl, st, sides=8, roll=math.pi / 8)
        if band >= 2:
            for col in faces:
                col[-1].material_index = TRIM
        orient_line(sl, [c for c, _, _ in st])
        thicken(sl, lip=0.02)
        add(bm, sl)
        # The elbow cop, on the point of the elbow (behind, in the T-pose).
        c, r = limb(("body_arms",), el, wr, 0.02)
        dome(bm, c + Vector((0, -r * 0.75, 0)), 0.05, 0.04, 0.05, PLATE, segs=6, rings=2, low=-50)

    # The pauldrons: domes stacked down the shoulder, each further out and lower; band III's top
    # lame carries a neck guard.
    lames = {1: [(0.20, 1.52, 0.13, 0.145, 0.095)],
             2: [(0.20, 1.535, 0.13, 0.15, 0.10), (0.27, 1.485, 0.105, 0.145, 0.085),
                 (0.325, 1.435, 0.08, 0.13, 0.07)],
             3: [(0.20, 1.55, 0.15, 0.17, 0.12), (0.28, 1.50, 0.125, 0.165, 0.10),
                 (0.345, 1.45, 0.10, 0.15, 0.085), (0.395, 1.40, 0.08, 0.135, 0.07)]}[band]
    for side in (1, -1):
        for i, (x, z, rx, ry, rz) in enumerate(lames):
            dome(bm, (x * side, -0.065, z), rx, ry, rz, PLATE, flat_x=0.115, side=side, low=-18)
        if band == 3:                                  # the neck guard, leaning out from the neck
            lean = lambda z: side * (0.13 + (z - 1.58) * 0.55)
            fin(bm, [(lean(z), y, z) for y, z in ((-0.19, 1.59), (-0.15, 1.72), (-0.04, 1.77), (0.06, 1.70),
                                                    (0.07, 1.59))], (1, 0, -0.3 * side), 0.016, PLATE)
    return bm, ["torso", "arms"]


# ---- legs ----

def legs(band):
    bm = bmesh.new()
    # The hips: a short shell from the waist to the top of the legs.
    hips = bmesh.new()
    angs = [22.5 * k for k in range(16)]
    lv = [(1.08, 0.168, 0.13, 0.13), (1.0, 0.19, 0.14, 0.14), (0.9, 0.195, 0.13, 0.135)]
    grid(hips, [[Vector((0, -0.045, 0)) + out(a) * sup(a, w, f, b) + Vector((0, 0, z)) for z, w, f, b in lv]
                for a in angs])
    bottom = [v for v in hips.verts if v.co.z < 0.91]
    bottom.sort(key=lambda v: math.atan2(v.co.y + 0.045, v.co.x))
    hips.faces.new(bottom)
    orient(hips, lambda c: Vector((0, -0.045, 1.0)))
    thicken(hips, lip=0.02)
    add(bm, hips)

    for side in (1, -1):
        s = "R" if side > 0 else "L"
        hip, knee, ankle = fit.bone(f"thigh.{s}"), fit.bone(f"shin.{s}"), fit.bone(f"foot.{s}")
        # Two plates, the thigh's and the shin's (the greave, a little narrower at its top, so a
        # step shows under the knee cop); band II's greave flares over the foot.
        for a, b2, ts, extra, flare in ((hip, knee, (0.15, 0.5, 0.92), (0.008, 0.006, 0.002), False),
                                        (knee, ankle, (0.06, 0.35, 0.72, 0.9, 0.95), (0.0, 0.004, 0.0, 0.004, 0.006), True)):
            leg = bmesh.new()
            st = []
            for t, e in zip(ts, extra):
                c, r = limb(("body_legs",), a, b2, t)
                st.append((c, r + 0.016 + e, t))
            if flare and band >= 2:
                c, r, t = st[-1]
                st[-1] = (c, r + 0.014, t)
            faces, _ = tube(leg, st, sides=10)
            for col in faces:
                if band >= 2 and flare:
                    col[-1].material_index = TRIM
            orient_line(leg, [c for c, _, _ in st])
            thicken(leg, lip=0.02)
            add(bm, leg)

        # The knee cop: a big dome over the front of the knee, covering where the plates meet;
        # band II gives it a wing on the outside, band III a point.
        c, r = limb(("body_legs",), hip, knee, 0.99)
        kc = c + Vector((0, r * 0.55, 0.0))
        dome(bm, kc, 0.085, 0.075, 0.095, PLATE, low=-55)
        if band >= 2:
            x = kc.x + side * 0.07
            fin(bm, [(x - side * 0.02, kc.y - 0.02, kc.z + 0.06), (x + side * 0.04, kc.y - 0.05, kc.z + 0.02),
                     (x + side * 0.025, kc.y - 0.03, kc.z - 0.06), (x - side * 0.02, kc.y + 0.01, kc.z - 0.04)],
                (1, 0, 0), 0.014, PLATE)
        if band == 3:
            A.spike(bm, kc + Vector((0, 0.04, 0.0)), 0.03, kc + Vector((0, 0.13, 0.04)), sides=4, mat=TRIM,
                    roll=math.pi / 4)

        # Tassets: plates over the front and outside of the thigh, hanging from the waist.
        if band >= 2:
            n = 2 if band == 2 else 3
            for i in range(n):
                ts = bmesh.new()
                z_top, z_bot = 1.0 - i * 0.075, 0.91 - i * 0.075
                cols = []
                for ang in [-35 + 18 * k for k in range(8)]:      # inner front round to the outside
                    a2 = ang if side < 0 else -ang
                    col = []
                    for z, grow in ((z_top, 0.0), (z_bot, 0.012)):
                        tt = (hip.z - z) / (hip.z - knee.z)
                        cc = hip.lerp(knee, tt)
                        rr = 0.125 + 0.012 * i + grow
                        col.append(Vector((cc.x, cc.y, z)) + out(a2) * rr)
                    cols.append(col)
                grid(ts, cols, PLATE, closed=False)
                orient(ts, lambda c, cx=hip.x: Vector((cx, -0.03, c.z)))
                thicken(ts)
                add(bm, ts)
    return bm, ["legs"]


PIECES = [("helm", helm), ("body", body), ("legs", legs)]


# ---- dressing the line-up ----

def material(name):
    return A.mat(name)


def mesh(name, bm, band, coll):
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
    A.finish(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for n in LOOK[band]:
        me.materials.append(material(n))
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    fit.coll(coll).objects.link(ob)
    return ob


LINEUP = "SmithedLineup"


def figure(name, x, yaw=0.0):
    base = bpy.data.objects["ArmourRig"]
    c = fit.coll(LINEUP)
    rig = base.copy()
    rig.name = "Sm_" + name
    c.objects.link(rig)
    rig.location = (x, 0, 0)
    rig.rotation_euler = (0, 0, math.radians(yaw))
    for p in base.children:
        q = p.copy()
        q.name = "Sm_" + name + "_" + p.name.replace("Armour_", "")
        c.objects.link(q)
        q.parent = rig
        q.hide_render = False
        q.hide_set(False)
        for md in q.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
    fit.pose(rig)
    return rig


def dress(rig, band, pieces=PIECES):
    report = {}
    for piece, make in pieces:
        bm, hides = make(band)
        ob = mesh(f"{rig.name}_{piece}", bm, band, LINEUP)
        fit.bind(ob, rig)
        ob["hides"] = ",".join(hides)
        report[piece] = A.report([ob])["triangles"]
        for part in hides:
            for ch in rig.children:
                if ch.name.endswith("body_" + part) or (part == "hair" and "_hair_" in ch.name):
                    ch.hide_render = True
                    ch.hide_set(True)
    return report


def build(bands=(1, 2, 3), spacing=0.95):
    fit.scene()
    base = fit.body()
    fit.clear(LINEUP)
    old = bpy.data.collections.get("ArmourLineup")
    if old:
        old.hide_render = True
        old.hide_viewport = True
    out_ = {}
    for i, band in enumerate(bands):
        x = (i - (len(bands) - 1) / 2) * spacing
        out_[band] = dress(figure(f"band{band}", -x), band)
    base.hide_set(True)
    base.hide_render = True
    for ch in base.children:
        ch.hide_set(True)
        ch.hide_render = True
    return out_


if __name__ == "__main__" or "result" not in globals():
    result = build()
