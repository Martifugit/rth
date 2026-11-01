r"""The Moon-Bearer's leg, study (2026-11-01): one leg, three ways (docs/bosses/moon-bearer.md).

The bet: folded flat in the wall, the leg IS a crack. It folds in tight zigzags like a folding knife
(seven segments, more than a crab has); its barbs along the folded edges make the crack's jagged edges;
its outer face is crusted with the wall's rock, flush with the wall; the light behind the wall leaks out
round it. Peeling off, then reaching, the same leg shows what it is.

Construction (rigid overlapping segments, as the 2004 game hides joints):
- each segment a closed piece: a keeled six-sided section (a ridge on its back), a neck of dark joint
  membrane sunk inside the previous segment's flared cup, a swelling shaft, a flared cup at its end;
- all hinges bend in one plane; the ridge (dorsal) side and the barbs lie in that plane, the crust on
  the outer (lateral) face;
- barbs grown out of the two faces flanking the ridge, hooked back toward the body;
- crust: faceted rock plates (artlib/plates.slab) cast onto the lateral face;
- the last segment a long curved needle; one segment broken once and regrown thinner and bare.

    import runpy; ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\moonbearer_leg.py"); ns["run"]()
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if not __name__.startswith("boards."):
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "boards")]:
        del sys.modules[_m]

import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

from artlib.armour import LocalFrame  # noqa: E402
from artlib.grow import Grow  # noqa: E402
from artlib.plates import PROFILE, outline, slab  # noqa: E402

COLOURS = ["SaltFlat", "BlackBile", "Unmade", "CryptDarkStone", "Moonlight"]
SHELL, MEMBRANE, CRUST, BARB, GLOW = range(5)

# the segments, base to tip: (name, length, radius, barbs at these fractions, crust plates, regrown)
SEGMENTS = [("coxa", 0.6, 0.34, [], [], False),
            ("basis", 0.55, 0.31, [], [(0.5, 0.8, 0.0)], False),
            ("merus", 3.0, 0.29, [0.22, 0.38, 0.54, 0.7], [(0.3, 0.42, 0.0), (0.72, 0.36, 0.25)], False),
            ("carpus", 1.0, 0.25, [0.5], [(0.5, 0.7, 0.0)], False),
            ("propodus", 2.4, 0.21, [0.25, 0.45, 0.65], [(0.35, 0.45, -0.2), (0.75, 0.3, 0.2)], False),
            ("extra", 1.6, 0.14, [0.4], [], True),                 # broken once, regrown thin and bare
            ("dactyl", 2.2, 0.12, [], [(0.15, 0.25, 0.0)], False)]

POSES = {
    # (first segment's angle, the turn at each joint, degrees): all in the leg's own plane
    "folded": (12, [8, 6, 142, -142, 140, -125]),
    "peeling": (20, [8, 10, 75, -70, 65, -55]),
    "reaching": (None, [5, 18, -62, -42, -24, -14]),
}


def sec(c, d, dorsal, n, r, scale):
    """A keeled six-sided section round c: a tall ridge on the dorsal side, a flat belly, flat sides."""
    w, h, hb = r * scale, r * 1.55 * scale, r * 0.75 * scale
    return [c + dorsal * h,
            c + dorsal * (h * 0.35) + n * w,
            c - dorsal * (hb * 0.5) + n * (w * 0.9),
            c - dorsal * hb,
            c - dorsal * (hb * 0.5) - n * (w * 0.9),
            c + dorsal * (h * 0.35) - n * w]


def spike(bm, faces, way, length, mat, thin=0.5, base=0.4, shell=0):
    """A spike or barb out of a patch of faces: their outline lifted to a narrower ring partway along
    `way`, then drawn to a small closing ring at the tip: one closed surface with the segment."""
    from artlib.grow import Grow as G
    loop = G.boundary(faces)
    c = sum((v.co for v in loop), Vector()) / len(loop)
    bmesh.ops.delete(bm, geom=list(faces), context="FACES_ONLY")
    w = way.normalized()
    r1 = [bm.verts.new(c + (v.co - c) * base) for v in loop]        # a small base: the spike doesn't
    r2 = [bm.verts.new(c + w * (length * 0.35) + (v.co - c) * base * thin) for v in loop]   # run the band
    r3 = [bm.verts.new(c + way * 1.0 + (v.co - c) * 0.05) for v in loop]
    n = len(loop)
    for a_, b_ in ((loop, r1),):                                    # the shoulder round the base, flat
        for k in range(n):
            bm.faces.new((a_[k], a_[(k + 1) % n], b_[(k + 1) % n], b_[k])).material_index = shell
    for (a_, b_), m_ in (((r1, r2), shell), ((r2, r3), mat)):        # shell-coloured, a dark point
        for k in range(n):
            bm.faces.new((a_[k], a_[(k + 1) % n], b_[(k + 1) % n], b_[k])).material_index = m_
    if n == 4:
        bm.faces.new(r3).material_index = mat
    else:
        for i in range(n // 2 - 1):
            bm.faces.new((r3[i], r3[i + 1], r3[n - 2 - i], r3[n - 1 - i])).material_index = mat


def chain(base, u, v, start, turns):
    """The joints of the leg in the plane (u, v) from `base`: points, each segment's direction and
    dorsal side (the in-plane normal turned with it)."""
    pts, dirs, dors = [Vector(base)], [], []
    th = math.radians(start)
    for i, (_, length, *_r) in enumerate(SEGMENTS):
        if i > 0:
            th += math.radians(turns[i - 1])
        d = u * math.cos(th) + v * math.sin(th)
        dirs.append(d)
        dors.append(-u * math.sin(th) + v * math.cos(th))
        pts.append(pts[-1] + d * length)
    return pts, dirs, dors


def build_leg(bm, base, u, v, start, turns):
    g = Grow()
    g.bm.free()
    g.bm = bm
    n = u.cross(v).normalized()                         # the lateral side: the crust faces this way
    pts, dirs, dors = chain(base, u, v, start, turns)
    spikes = []                                         # (faces, way, length, material), grown at the end
    axes = []
    for i, (name, length, r, barbs, crust, regrown) in enumerate(SEGMENTS):
        A, d, dorsal = pts[i], dirs[i], dors[i]
        prev_r = SEGMENTS[i - 1][2] if i else r
        ov = prev_r * 1.0                               # the neck starts inside the previous cup
        last = i == len(SEGMENTS) - 1
        prof = [(-ov / length, 0.42, MEMBRANE), (0.06, 0.48, MEMBRANE), (0.13, 0.78, SHELL)]
        if last:                                        # the needle: tapering, curving toward the belly
            for k in range(1, 10):
                t = 0.13 + 0.86 * k / 9
                prof.append((t, max(0.06, 1.05 - 1.0 * (t - 0.13) / 0.86), SHELL))
        else:                                           # a spindle: pinched at the joints, swollen between
            for t, sc in ((0.22, 0.95), (0.36, 1.14), (0.5, 1.22), (0.64, 1.14), (0.78, 0.95), (0.88, 0.82),
                          (0.96, 1.12), (1.02, 1.28), (1.06, 0.92)):
                prof.append((t, sc, SHELL))
        rings, mats, ts = [], [], []
        for t, scale, mat in prof:
            c = A + d * (t * length)
            if last:
                c = c - dorsal * (0.18 * length * max(0.0, t) ** 2)
            rings.append([bm.verts.new(p) for p in sec(c, d, dorsal, n, r, scale)])
            mats.append(mat)
            ts.append(t)
        faces = {}
        for j in range(len(rings) - 1):
            a_, b_ = rings[j], rings[j + 1]
            for k in range(6):
                f = bm.faces.new((a_[k], a_[(k + 1) % 6], b_[(k + 1) % 6], b_[k]))
                f.material_index = mats[j]
                faces[j, k] = f
        g.ladder(list(reversed(rings[0])), MEMBRANE)
        g.ladder(rings[-1], SHELL)
        band = lambda t: min(range(len(ts) - 1), key=lambda jj: abs(ts[jj] - t))   # noqa: E731
        for bt in barbs:                                # barbs on the ridge, hooked back toward the body
            j = band(bt)
            L = r * 2.8
            spikes.append(([faces[j, 5], faces[j, 0]], dorsal * (L * 0.75) - d * (L * 0.8), L, BARB))
        if not last:
            for bt in barbs:                            # a finer serration under the belly, between them
                j = band(bt + 0.08)
                if (j, 2) in faces and faces[j, 2].is_valid:
                    L = r * 0.8
                    spikes.append(([faces[j, 2], faces[j, 3]], -dorsal * (L * 0.8) - d * (L * 0.6), L, BARB))
            jc = band(1.02) - 1                         # spurs on the knuckle's flared cup, out to both sides
            for k, side in ((1, n), (4, -n)):
                L = r * 0.9
                spikes.append(([faces[jc, k]], side * (L * 0.7) + d * (L * 0.6) + dorsal * (L * 0.2), L, BARB))
        axes.append((A, d, dorsal, length, r, crust, regrown, last))
    for fs, way, L, mat in spikes:
        if all(f.is_valid for f in fs):
            spike(bm, fs, way, L, mat)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bvh = BVHTree.FromBMesh(bm)
    for A, d, dorsal, length, r, crust, regrown, last in axes:   # rock crust over the whole lateral face
        if regrown or not crust:
            continue
        spans = [(0.5, 0.8)] if length < 1.2 else [(0.3, 0.46), (0.7, 0.44)]
        if last:
            spans = [(0.3, 0.32)]
        for t, frac in spans:
            q = A + d * (t * length)
            frame = LocalFrame(bvh, q, d)
            c = q + n * r
            slab(bm, frame, (-c.y, c.x, c.z), (-d.y, d.x, d.z), frac * length, r * 2.5,
                 outline(14, front=0.2, back=0.35, square=0.3, lobes=5, lobe=0.08), r * 0.16, PROFILE,
                 facets=3, facet=0.35, mat=CRUST)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return pts, dirs


def reaching_start(base, u, v, turns, floor=0.0):
    """The first segment's angle that puts the needle's tip on the floor (bisection)."""
    lo, hi = -40.0, 80.0
    for _ in range(40):
        mid = (lo + hi) / 2
        tip = chain(base, u, v, mid, turns)[0][-1]
        if tip.z > floor:
            hi = mid
        else:
            lo = mid
    return (lo + hi) / 2


def wall(base, crack_pts, crack_dirs, width=16.0, height=9.0, front=-0.32, back=0.8):
    """A section of the shell's wall, the folded leg's crack cut through it (one box a segment, a little
    wider than the segment), and the glow behind."""
    import bpy
    import rth_art as A
    bm = bmesh.new()
    x0 = base.x - 3.0
    vs = [bm.verts.new((x0 + x, y, z)) for x in (0, width) for y in (front, back) for z in (0, height)]
    for q in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
        bm.faces.new([vs[i] for i in q]).material_index = CRUST
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    w = A.mesh_from("_wall", bm, COLOURS, coll="MoonBearerLeg")
    cutters = []
    for i, (name, length, r, *_x) in enumerate(SEGMENTS):
        cb = bmesh.new()
        a, b = crack_pts[i], crack_pts[i + 1]
        d = crack_dirs[i]
        side = Vector((0, 1, 0)).cross(d).normalized()
        hw = r * 1.2
        a2, b2 = a - d * r * 0.6, b + d * r * 0.6
        corners = [a2 + side * hw, b2 + side * hw * 0.85, b2 - side * hw * 0.85, a2 - side * hw]
        vv = [cb.verts.new(Vector((c.x, y, c.z))) for y in (-2.0, 2.0) for c in corners]
        for q in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            cb.faces.new([vv[k] for k in q])
        bmesh.ops.recalc_face_normals(cb, faces=cb.faces[:])
        c_ob = A.mesh_from("_cut%d" % i, cb, COLOURS, coll="MoonBearerLeg")
        mod = w.modifiers.new("Cut%d" % i, "BOOLEAN")
        mod.object = c_ob
        mod.operation = "DIFFERENCE"
        mod.solver = "EXACT"
        cutters.append(c_ob)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(w.evaluated_get(dg))
    for c_ob in cutters:
        m = c_ob.data
        bpy.data.objects.remove(c_ob)
        bpy.data.meshes.remove(m)
    old = w.data
    w.modifiers.clear()
    w.data = me
    bpy.data.meshes.remove(old)
    gb = bmesh.new()
    q = [gb.verts.new((x0 + x, back + 0.6, z)) for x, z in ((0, 0), (width, 0), (width, height), (0, height))]
    gb.faces.new(q).material_index = GLOW
    gl = A.mesh_from("_glow", gb, COLOURS, coll="MoonBearerLeg")
    return w, gl


def run(y=60.0, gap=18.0):
    import bpy
    import rth_art as A
    for o in [o for o in bpy.data.objects if o.name.startswith("MoonBearerLeg")]:
        m = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if m:
            bpy.data.meshes.remove(m)
    m = bpy.data.materials.get("Moonlight")
    if m and m.use_nodes and m.node_tree.nodes.get("Principled BSDF"):
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Emission Color"].default_value = (0.56, 0.69, 0.86, 1)
        b.inputs["Emission Strength"].default_value = 2.0
    wall_u, wall_v = Vector((1, 0, 0)), Vector((0, 0, 1))
    stats = {}
    for i, pose in enumerate(("folded", "peeling", "reaching")):
        x = i * gap
        base = Vector((x - 2.0, 0.0, 2.6))
        crack_pts, crack_dirs, _ = chain(base, wall_u, wall_v, *POSES["folded"])
        start, turns = POSES[pose]
        if pose == "folded":
            u, v = wall_u, wall_v
        elif pose == "peeling":
            u, v = wall_u, Vector((0, -0.75, 0.66)).normalized()
        else:
            u, v = Vector((0.55, -0.83, 0)).normalized(), Vector((0, 0, 1))
            start = reaching_start(base, u, v, turns)
        bm = bmesh.new()
        build_leg(bm, base, u, v, start, turns)
        stats[pose] = dict(faces=len(bm.faces), tris=sum(1 for f in bm.faces if len(f.verts) == 3),
                           open=sum(1 for e in bm.edges if len(e.link_faces) == 1))
        leg = A.mesh_from("MoonBearerLeg_" + pose, bm, COLOURS, coll="MoonBearerLeg", smooth_angle=25)
        w, gl = wall(base, crack_pts, crack_dirs)
        w.name, gl.name = "MoonBearerLeg_%s_Wall" % pose, "MoonBearerLeg_%s_Glow" % pose
        leg.location = w.location = gl.location = (0, y, 0)
    return stats
