"""The Altar of Martyrs: the centrepiece of Cruor's cathedral and the heart of Troth's church.
Design and decisions: docs/regions/cruor-altar.md (read it first). This is the blockout: every
part at its final size, deliberate and symmetric (revolved mouldings, designed figures; nothing
random), mostly plain, the blood already red because it is part of the focus.

Read from the nave: three pale martyrs back to back on a tall dark pedestal, swords raised and
fanned, in a round red pool ringed with outward spikes, on a stepped round dais; one red line
running to it from the nave. Front is -Y (the nave).

    runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\buildings\\cruor\\altar.py")
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..", "..")
if ART not in sys.path:
    sys.path.insert(0, ART)
sys.modules.pop("rth_art", None)
import rth_art as A  # noqa: E402

# The blockout's values: dark stone, pale trim, the martyrs' stone, iron, the floor; blood in four
# shades, deep (in shadow, round the pedestal's foot), the pool's own, running (shallows, gutters,
# streams) and the sheen where it catches the light; one bronze accent on the swords.
MATS = ["Ashgrey", "Gravestone", "Tin", "FountBlood", "Blackiron", "CryptFloor", "Sable",
        "BloodDeep", "Blood", "BloodSheen", "Bronze"]
DARK, TRIM, FIGURE, BLOOD, IRON, FLOOR, VOID, DEEP, FLOW, SHEEN, BRONZE = range(len(MATS))

# The dais: three round steps, each a riser and a pale nose.
STEPS = [(5.5, 0.25), (4.9, 0.5), (4.3, 0.75)]
TOP = 0.75
RIM_OUT, RIM_IN, RIM_H = 3.4, 2.95, 1.08     # the pool's rim
POOL_Z = 0.92
SILL = POOL_Z - 0.012                        # the notch the pool spills through, toward the nave
PED_TOP = 4.1
SPIKES = 12
SIDES = 48                                   # the dais, rim and pool
HALF = math.pi / SIDES
GAP_ROT = -math.pi / 2 - HALF                # turns the rim and pool so one segment faces the nave: the notch
GUT_W, KERB_W, KERB_H = 0.13, 0.055, 0.045   # the gutter: half the blood's width, its kerbs
SPOUT_Z = 1.10                               # the spouts' trough floor
AT_R = 0.72                                  # the martyrs' feet from the pedestal's axis


def obj(name, bm, smooth=None):
    A.finish(bm)
    up = bm.faces.layers.int.get("up")
    if up is not None:                       # flat surfaces (pool, ripples) always face the sky
        bm.normal_update()
        for f in bm.faces:
            if f[up] and f.normal.z < 0:
                f.normal_flip()
    return A.mesh_from(name, bm, MATS, coll="Altar", smooth_angle=smooth)


def flat(bm, verts, mat):
    """A face that must look up (a liquid surface), whatever recalculating normals decides."""
    lay = bm.faces.layers.int.get("up") or bm.faces.layers.int.new("up")
    f = bm.faces.new(verts)
    f.material_index = mat
    f[lay] = 1
    return f


def turn(bm, verts, deg, at=(0, 0, 0)):
    bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, 'Z'), verts=verts)


def new(bm, before):
    return [v for v in bm.verts if v not in before]


def join(bm, rings, mats, caps=None):
    """Faces between consecutive rings (a ring of one vertex is a point); open ends capped flat.
    mats: one material per band between rings; caps: the (bottom, top) caps' materials."""
    bottom, top = caps or (mats[0], mats[-1])
    sides = max(len(r) for r in rings)
    for i in range(1, len(rings)):
        a, b, m = rings[i - 1], rings[i], mats[i - 1]
        for k in range(sides):
            n = (k + 1) % sides
            if len(a) == 1:
                f = bm.faces.new((a[0], b[k], b[n]))
            elif len(b) == 1:
                f = bm.faces.new((a[k], a[n], b[0]))
            else:
                f = bm.faces.new((a[k], a[n], b[n], b[k]))
            f.material_index = m
    if len(rings[-1]) > 1:
        bm.faces.new(rings[-1]).material_index = top
    if len(rings[0]) > 1:
        bm.faces.new(list(reversed(rings[0]))).material_index = bottom
    return rings


def lathe(bm, profile, sides, rot=0.0, centre=(0, 0)):
    """A solid of revolution: profile is (radius, height, material of the band from the previous
    point) from bottom to top; a radius of 0 closes it to a point; a ring left open at either end is
    capped flat with that point's material."""
    cx, cy = centre
    rings = []
    for r, z, _ in profile:
        if r == 0:
            rings.append([bm.verts.new((cx, cy, z))])
        else:
            rings.append([bm.verts.new((cx + r * math.cos(rot + 2 * math.pi * k / sides),
                                        cy + r * math.sin(rot + 2 * math.pi * k / sides), z)) for k in range(sides)])
    return join(bm, rings, [p[2] for p in profile[1:]], caps=(profile[0][2], profile[-1][2]))


def band(bm, profile, sides, rot=0.0, skip=None):
    """An open band of revolution (no caps): a ring-shaped moulding such as the pool's rim. `skip`
    leaves one segment out (segment 0 runs from `rot` to the next ring vertex)."""
    rings = [[bm.verts.new((r * math.cos(rot + 2 * math.pi * k / sides), r * math.sin(rot + 2 * math.pi * k / sides), z))
              for k in range(sides)] for r, z, _ in profile]
    for i in range(1, len(rings)):
        for k in range(sides):
            if k == skip:
                continue
            n = (k + 1) % sides
            bm.faces.new((rings[i - 1][k], rings[i - 1][n], rings[i][n], rings[i][k])).material_index = profile[i][2]
    return rings


def ring_at(r, a, z):
    return Vector((r * math.cos(a), r * math.sin(a), z))


def annulus(bm, centre, r0, r1, z, mat, sides=12):
    """A flat ring lying on a liquid (a ripple)."""
    c = Vector(centre)
    inner = [bm.verts.new(c + Vector((r0 * math.cos(2 * math.pi * k / sides), r0 * math.sin(2 * math.pi * k / sides), z)))
             for k in range(sides)]
    outer = [bm.verts.new(c + Vector((r1 * math.cos(2 * math.pi * k / sides), r1 * math.sin(2 * math.pi * k / sides), z)))
             for k in range(sides)]
    for k in range(sides):
        n = (k + 1) % sides
        flat(bm, (inner[k], outer[k], outer[n], inner[n]), mat)


# ---- the dais, the pool, the spikes, the line ----------------------------------------------------

def dais():
    """Three round steps with pale bevelled noses, the platform, and the pool's rim: a worked
    moulding bevelled on its outer and inner edges, the blood level just under its lip. The rim is cut
    toward the nave, a notch with a sill the pool spills over into the gutter. The pool is shaded in
    rings: running blood in the shallows at the rim, the pool's own red, deep red round the pedestal,
    and a current of running blood toward the notch."""
    bm = bmesh.new()
    prof = [(STEPS[0][0], -0.05, DARK)]
    for k, (r, top) in enumerate(STEPS):
        prof += [(r, top - 0.07, DARK), (r - 0.07, top, TRIM)]
        if k + 1 < len(STEPS):
            prof += [(STEPS[k + 1][0] + 0.02, top, TRIM)]
    prof += [(RIM_OUT, TOP, TRIM)]
    lathe(bm, prof, SIDES)
    rim = [(RIM_OUT, TOP - 0.01, DARK), (RIM_OUT, RIM_H - 0.12, DARK), (RIM_OUT - 0.1, RIM_H, TRIM),
           (RIM_IN + 0.1, RIM_H, TRIM), (RIM_IN, RIM_H - 0.08, TRIM), (RIM_IN, POOL_Z - 0.02, DARK)]
    rings = band(bm, rim, SIDES, rot=GAP_ROT, skip=0)
    for col in (0, 1):                       # the rim's cut ends, from the sill up
        a = GAP_ROT + 2 * HALF * col
        bm.faces.new([bm.verts.new(ring_at(RIM_OUT, a, SILL))] + [rings[i][col] for i in (1, 2, 3, 4)] +
                     [bm.verts.new(ring_at(RIM_IN, a, SILL))]).material_index = DARK
    A.hull(bm, [ring_at(r, GAP_ROT + 2 * HALF * col, z) for r in (RIM_IN, RIM_OUT) for col in (0, 1)
                for z in (TOP - 0.01, SILL)], TRIM)  # the sill
    radii, mats = [RIM_IN, 2.8, 2.05, 1.6], [FLOW, BLOOD, DEEP]
    rs = [[bm.verts.new(ring_at(r, GAP_ROT + 2 * HALF * k, POOL_Z)) for k in range(SIDES)] for r in radii]
    for i, m in enumerate(mats):
        for k in range(SIDES):
            n = (k + 1) % SIDES
            flat(bm, (rs[i][k], rs[i + 1][k], rs[i + 1][n], rs[i][n]), m)
    flat(bm, rs[-1], DEEP)
    edge = -RIM_IN * math.cos(HALF)
    for pts, lift, m in (([(-0.13, 0), (-0.21, 0.17), (-0.14, 0.4), (0, 0.52), (0.14, 0.4), (0.21, 0.17), (0.13, 0)], 0.003, FLOW),
                         ([(-0.04, 0), (-0.06, 0.2), (0, 0.38), (0.06, 0.2), (0.04, 0)], 0.005, SHEEN)):
        flat(bm, [bm.verts.new((x, edge + y, POOL_Z + lift)) for x, y in reversed(pts)], m)
    return obj("AltarDais", bm, smooth=40)


def spikes():
    """Twelve spikes pointing out from the rim, each growing from a block that stands on the platform
    and rises through the rim, capped in pale stone like a merlon; the spike's foot is buried in it."""
    bm = bmesh.new()
    r0, r1, w, top = RIM_IN + 0.17, RIM_OUT + 0.16, 0.17, RIM_H + 0.08
    for k in range(SPIKES):
        a = 2 * math.pi * (k + 0.5) / SPIKES
        d = Vector((math.cos(a), math.sin(a), 0))
        s = Vector((-d.y, d.x, 0))

        def at(r, side, z):
            return d * r + s * side + Vector((0, 0, z))
        A.hull(bm, [at(r, sd * w, z) for r in (r0, r1) for sd in (-1, 1) for z in (TOP - 0.01, top)], DARK)
        A.hull(bm, [at(r, sd * (w + 0.03), top) for r in (r0 - 0.03, r1 + 0.03) for sd in (-1, 1)] +
               [at(r, sd * (w - 0.03), top + 0.07) for r in (r0 + 0.01, r1 - 0.01) for sd in (-1, 1)], TRIM)
        A.spike(bm, at(RIM_OUT + 0.04, 0, 0.97), 0.12, at(RIM_OUT + 0.84, 0, 1.13), sides=4, mat=IRON, roll=math.pi / 4)
    return obj("AltarSpikes", bm)


def offset(prof, d):
    """A profile of (radius, height, vee) moved `d` off the surface it follows (mitred at corners).
    The profile runs inward, so the surface's outside is to its left."""
    out = []
    for i in range(len(prof)):
        normals = []
        for j in (i - 1, i):
            if 0 <= j < len(prof) - 1:
                t = Vector((prof[j + 1][0] - prof[j][0], prof[j + 1][1] - prof[j][1])).normalized()
                normals.append(Vector((t.y, -t.x)))
        n = sum(normals, Vector((0, 0))).normalized()
        k = d / max(n.dot(normals[0]), 0.3)
        out.append((prof[i][0] + n.x * k, prof[i][1] + n.y * k, prof[i][2]))
    return out


def sweep(bm, prof, xs, d0, d1, mats):
    """A solid laid along a profile of (radius, height, vee) toward the nave (-Y), across `xs`, from
    `d0` to `d1` off the surface. `vee` follows the dais's facets: a 48-sided lathe has a corner on
    the nave's axis, so its surface steps in by |x| * vee either side. mats: one per segment."""
    lo, hi = offset(prof, d0), offset(prof, d1)

    def p(pt, x):
        r, z, vee = pt
        return (x, -(r - abs(x) * vee), z)
    g = [[(bm.verts.new(p(lo[i], x)), bm.verts.new(p(hi[i], x))) for x in xs] for i in range(len(prof))]
    for i in range(len(prof) - 1):
        m = mats[i]
        for j in range(len(xs) - 1):
            a, b, c, e = g[i][j], g[i][j + 1], g[i + 1][j + 1], g[i + 1][j]
            bm.faces.new((a[1], b[1], c[1], e[1])).material_index = m
            bm.faces.new((a[0], e[0], c[0], b[0])).material_index = m
        for j in (0, len(xs) - 1):
            bm.faces.new((g[i][j][0], g[i + 1][j][0], g[i + 1][j][1], g[i][j][1])).material_index = m
    for i in (0, len(prof) - 1):
        for j in range(len(xs) - 1):
            bm.faces.new((g[i][j][0], g[i][j + 1][0], g[i][j + 1][1], g[i][j][1])).material_index = mats[min(i, len(mats) - 1)]


def blood_line():
    """The one red line: the pool spills through the notch in its rim, over the sill and down its
    face, across the platform and down every nose and riser of the steps, and over the floor to the
    nave. Blood and kerbs are each one piece swept along the dais's own profile, so they follow every
    tread, nose and riser; the kerbs end against the rim's cut and across the end at the nave."""
    bm = bmesh.new()
    vee = math.tan(HALF)
    prof = [(12.0, 0.0, vee), (STEPS[0][0], 0.0, vee)]
    for k, (r, top) in enumerate(STEPS):
        prof += [(r, top - 0.07, vee), (r - 0.07, top, vee)]
        if k + 1 < len(STEPS):
            prof.append((STEPS[k + 1][0] + 0.02, top, vee))
    out_c, in_c = RIM_OUT * math.cos(HALF), RIM_IN * math.cos(HALF)
    prof += [(out_c, TOP, 0.0), (out_c, SILL, 0.0), (in_c, SILL, 0.0)]
    segs = len(prof) - 1
    mats = [BLOOD if abs(prof[i + 1][1] - prof[i][1]) < 1e-6 else FLOW for i in range(segs)]
    mats[-2], mats[-1] = SHEEN, FLOW         # spilling over the sill's face, and the shallows on it
    sweep(bm, prof, (-GUT_W, 0.0, GUT_W), -0.02, 0.012, mats)
    for sd in (-1, 1):
        sweep(bm, prof, sorted((sd * GUT_W, sd * (GUT_W + KERB_W))), -0.02, KERB_H, [TRIM] * segs)
    end = [(12.0 + KERB_W, 0.0, 0.0), (12.0, 0.0, 0.0)]
    sweep(bm, end, (-(GUT_W + KERB_W), GUT_W + KERB_W), -0.02, KERB_H, [TRIM])
    return obj("AltarLine", bm)


# ---- the pedestal and its spouts ----------------------------------------------------------------

def pedestal():
    """A moulded base standing in the pool, an eight-sided shaft (the motif goes on its faces in the
    detail pass), a moulded cap under the martyrs; four spouts on the base, each a trough on a corbel,
    the blood running along it and falling in an arc into the pool, ripples where it lands."""
    bm = bmesh.new()
    lathe(bm, [(1.65, POOL_Z - 0.1, DARK), (1.65, 1.25, DARK), (1.55, 1.35, TRIM), (1.32, 1.42, TRIM),
               (1.32, 1.6, DARK), (1.22, 1.66, TRIM), (1.08, 1.7, TRIM)], 32)
    lathe(bm, [(1.08, 1.69, DARK), (0.98, 3.55, DARK), (0.98, 3.56, DARK)], 8, rot=math.pi / 8)
    lathe(bm, [(1.12, 3.5, TRIM), (1.22, 3.62, TRIM), (1.22, 3.72, DARK), (1.45, 3.88, TRIM), (1.55, 3.95, TRIM),
               (1.55, PED_TOP, TRIM)], 32)
    r0, r1, z = 1.5, 2.05, SPOUT_Z
    lip = Vector((r1 + 0.03, 0, z + 0.05))
    fall = POOL_Z - 0.03
    land = lip.x + 0.26 * math.sqrt((lip.z - POOL_Z) / (lip.z - fall))
    for deg in (270, 0, 90, 180):
        before = set(bm.verts)
        A.hull(bm, [(r0, sd * 0.13, z - 0.17) for sd in (-1, 1)] + [(r0, sd * 0.15, z) for sd in (-1, 1)] +
               [(r1 - 0.1, sd * 0.15, z) for sd in (-1, 1)], TRIM)                                   # the corbel
        A.hull(bm, [(x, sd * 0.17, zz) for sd in (-1, 1) for x, zz in ((r0, z), (r0, z + 0.06), (r1 + 0.04, z),
                                                                        (r1 + 0.04, z + 0.035))], TRIM)  # the floor
        for sd in (-1, 1):
            A.hull(bm, [(x, sd * ww, zz) for ww in (0.11, 0.17) for x, zz in ((r0, z + 0.05), (r0, z + 0.18),
                                                                              (r1, z + 0.03), (r1, z + 0.1))], TRIM)  # walls
        A.hull(bm, [(x, sd * 0.11, zz) for sd in (-1, 1) for x, zz in ((r0, z + 0.05), (r0, z + 0.09),
                                                                       (r1 + 0.035, z + 0.03), (r1 + 0.035, lip.z + 0.01))], FLOW)
        A.loft(bm, [((lip.x - 0.04 + 0.26 * s, 0, lip.z - (lip.z - fall) * s * s), 0.075 - 0.02 * s, 0.026)
                    for s in (0.0, 0.2, 0.4, 0.6, 0.8, 1.0)], sides=6, ref=(1, 0, 1), mats=FLOW)  # the stream
        annulus(bm, (land, 0, 0), 0.08, 0.15, POOL_Z + 0.004, SHEEN)
        annulus(bm, (land, 0, 0), 0.21, 0.26, POOL_Z + 0.004, FLOW)
        turn(bm, new(bm, before), deg)
    return obj("AltarPedestal", bm, smooth=40)


# ---- the martyrs ----------------------------------------------------------------------------------

# A martyr's robe and hood: (height, half width, half depth, fold depth, forward lean, and optionally
# how far the front is pushed in: the hood's opening, its faces in shadow). Folds are six
# broad pleats round the robe, deepest at the hem; the belt is the step at the waist; the hood's
# mantle lies over the shoulders and hides where the arms and neck join.
ROBE = [(0.0, 0.6, 0.5, 0.05, 0), (0.1, 0.59, 0.49, 0.05, 0), (0.65, 0.48, 0.38, 0.045, 0), (1.2, 0.37, 0.28, 0.02, 0),
        (1.44, 0.31, 0.22, 0, 0), (1.45, 0.335, 0.245, 0, 0), (1.57, 0.335, 0.245, 0, 0), (1.58, 0.31, 0.22, 0, 0),
        (1.92, 0.38, 0.26, 0, -0.01), (2.12, 0.43, 0.25, 0, 0), (2.3, 0.3, 0.2, 0, 0)]
HOOD = [(2.02, 0.45, 0.29, 0, 0), (2.22, 0.48, 0.3, 0, 0), (2.36, 0.41, 0.28, 0, 0), (2.46, 0.25, 0.25, 0, -0.02),
        (2.53, 0.24, 0.25, 0, -0.03, 0.06), (2.64, 0.23, 0.25, 0, -0.04, 0.08), (2.75, 0.21, 0.23, 0, -0.035, 0.06),
        (2.83, 0.19, 0.21, 0, -0.025), (2.93, 0.12, 0.15, 0, 0.02), (2.99, 0, 0, 0, 0.05)]


def robe(bm, secs, sides, mat):
    rings, dented = [], set()
    for z, w, d, fold, dy, *dent in secs:
        if w == 0:
            rings.append([bm.verts.new((0, dy, z))])
            continue
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides
            m = 1 + fold * math.cos(6 * a)
            v = bm.verts.new((w * m * math.cos(a), dy + d * m * math.sin(a), z))
            if dent and math.sin(a) < -0.85:
                v.co.y += dent[0]
                dented.add(v)
            ring.append(v)
        rings.append(ring)
    join(bm, rings, [mat] * (len(rings) - 1))
    for f in {f for v in dented for f in v.link_faces}:
        if all(v in dented for v in f.verts):
            f.material_index = VOID


def martyr(bm):
    """One martyr, designed, about 1.7 times a player: a pleated robe belted at the waist, a hooded
    mantle over broad shoulders, the face a shadow in the hood; the left hand laid flat on the heart,
    the right raising a broad sword up and outwards, its blade toward the nave. Built facing -Y with
    its feet at the origin."""
    robe(bm, ROBE, 24, FIGURE)
    robe(bm, HOOD, 16, FIGURE)
    # the left arm: the forearm across the chest, the hand flat over the heart
    A.loft(bm, [((-0.34, -0.02, 2.17), 0.12, 0.12), ((-0.42, -0.08, 1.74), 0.11, 0.11),
                ((-0.15, -0.29, 1.92), 0.13, 0.13)], sides=8, ref=(0, -1, 0), mats=FIGURE)
    A.loft(bm, [((-0.17, -0.29, 1.91), 0.06, 0.04), ((-0.07, -0.31, 1.96), 0.075, 0.035),
                ((0.03, -0.3, 2.0), 0, 0)], sides=6, ref=(0, -1, 0), mats=FIGURE)
    # the right arm, raised; the fist round the grip
    A.loft(bm, [((0.34, -0.02, 2.17), 0.12, 0.12), ((0.6, -0.12, 2.6), 0.11, 0.11),
                ((0.66, -0.2, 2.95), 0.125, 0.125)], sides=8, ref=(0, -1, 0), mats=FIGURE)
    g = Vector((0.67, -0.22, 3.05))
    A.loft(bm, [(g - Vector((0, 0, 0.1)), 0.07, 0.07), (g, 0.09, 0.085), (g + Vector((0.01, -0.01, 0.09)), 0, 0)],
           sides=8, ref=(0, -1, 0), mats=FIGURE)
    d = Vector((0.18, -0.32, 1.0)).normalized()
    side = d.cross(Vector((0, 1, 0))).normalized()
    flat_ = d.cross(side).normalized()
    A.hull(bm, [g - d * 0.2 + v * 0.05 for v in (side, -side, flat_, -flat_, d, -d)], BRONZE)          # pommel
    A.loft(bm, [(g - d * 0.17, 0.035, 0.035), (g + d * 0.15, 0.035, 0.035)], sides=6, ref=(0, -1, 0), mats=IRON)
    A.hull(bm, [g + d * dd + side * ss * 0.25 + flat_ * ff for dd in (0.14, 0.2) for ss in (-1, 1) for ff in (-0.04, 0.04)] +
           [g + d * 0.17 + side * ss * 0.29 for ss in (-1, 1)], BRONZE)                                # the guard
    A.hull(bm, [g + d * dd + side * ss * ww for dd, ww in ((0.2, 0.09), (1.25, 0.07)) for ss in (-1, 1)] +
           [g + d * dd + flat_ * ff * 0.035 for dd in (0.2, 1.25) for ff in (-1, 1)] + [g + d * 1.48], TRIM)  # the blade


def martyrs():
    bm = bmesh.new()
    for facing in (270, 30, 150):
        before = set(bm.verts)
        martyr(bm)
        made = new(bm, before)
        bmesh.ops.translate(bm, vec=Vector((0, -AT_R, PED_TOP)), verts=made)
        turn(bm, made, facing - 270)
    return obj("AltarMartyrs", bm, smooth=55)


# ---- the braziers -----------------------------------------------------------------------------------

def brazier(at=(0, 0, 0)):
    """A tall iron brazier: a round foot, a shaft, a wide bowl with a thick lip, a crown of short
    spikes standing on the lip with their feet sunk in it, coals heaped in it."""
    bm = bmesh.new()
    lathe(bm, [(0.42, 0.0, IRON), (0.42, 0.1, IRON), (0.3, 0.18, IRON), (0.1, 0.26, IRON), (0.08, 1.15, IRON),
               (0.14, 1.2, IRON), (0.52, 1.4, IRON), (0.62, 1.44, IRON), (0.64, 1.52, IRON), (0.5, 1.54, IRON),
               (0.48, 1.48, IRON), (0.3, 1.38, IRON)], 16)
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        d = Vector((math.cos(a), math.sin(a), 0))
        A.spike(bm, d * 0.57 + Vector((0, 0, 1.49)), 0.075, d * 0.68 + Vector((0, 0, 1.86)), sides=4, mat=IRON,
                roll=math.pi / 4)
    lathe(bm, [(0.49, 1.46, BLOOD), (0.32, 1.56, BLOOD), (0, 1.6, BLOOD)], 12)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    return obj("AltarBrazier", bm, smooth=40)


# ---- the board ------------------------------------------------------------------------------------

def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Altar", "Preview"):
            bpy.data.collections.remove(c)
    bm = bmesh.new()
    A.box(bm, (-14, -14, -0.1), (14, 14, 0.0), FLOOR)
    floor = obj("Floor", bm)
    for c in list(floor.users_collection):
        c.objects.unlink(floor)
    A.collection("Preview").objects.link(floor)
    objs = [dais(), spikes(), blood_line(), pedestal(), martyrs()]
    for k in range(4):
        a = math.radians(45 + 90 * k)
        objs.append(brazier(at=(7.2 * math.cos(a), 7.2 * math.sin(a), 0)))
    return objs


def rig():
    A.preview_rig(focus=(0, 0, 3.0), distance=26.0, pitch=25, yaw=-20, ground=0, human=True, human_at=(0.0, -7.5))
    A.figures([(-1.5, -8.2, 0), (1.8, -8.0, 0)])
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


objs = build()
rig()
result = A.report(objs)
