"""
Measures clipping in pose_review.py's posed copies, two ways, for every pair of visible meshes in
a copy (a body part and a garment, or two garments):

- "out": points of the inner mesh that the outer one covers at rest (inside it, within NEAR of
  it, not beside one of its openings) and that come out through it in the pose;
- "into": points of one mesh that end up inside another in the pose (by rays in six directions,
  most of them crossing it an odd number of times) though they were not inside it at rest: a
  hand sinking into a skirt, a knee into a robe, an arm through a jerkin.

- "self": pairs of one mesh's faces that pass through each other in the pose but not at rest
  (a skirt folding through itself where it bends).

Counted by the bone that moves the point most. Known noise: "into" for legs rising under a
skirt (that is where they belong), points in contact with a seal lip (the neck at a collar),
and points between the two walls of a fold (a tall boot's top), where the nearest face flips. Exec inside Blender through the MCP after
pose_review.py. CLIP_RESULT: {copy: {"kind inner > outer": {"points", "worst_mm", "bones"}}}.
"""
import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

TOL = 0.0008            # metres outside before a point counts
NEAR = 0.03             # a point is under a garment at rest only within this of it
EDGE = 0.01             # a point this close to an opening is beside it, not covered
DIRS = [Vector(d) for d in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]


def tree(cos, me):
    return BVHTree.FromPolygons(cos, [list(p.vertices) for p in me.polygons])


def rims(me):
    """The mesh's open edges, as pairs of vertex indices."""
    bm = bmesh.new()
    bm.from_mesh(me)
    out = [(e.verts[0].index, e.verts[1].index) for e in bm.edges if e.is_boundary]
    bm.free()
    return out


def near_rim(p, cos, rim):
    for a, b in rim:
        u, v = cos[a], cos[b]
        d = v - u
        t = max(0.0, min(1.0, (p - u).dot(d) / max(1e-12, d.dot(d))))
        if (p - (u + d * t)).length < EDGE:
            return True
    return False


def inside(p, t):
    odd = 0
    for d in DIRS:
        n, q = 0, p.copy()
        for _ in range(16):
            hit = t.ray_cast(q + d * 1e-5, d, 5.0)
            if hit[0] is None:
                break
            n += 1
            q = hit[0]
        odd += n % 2
    return odd >= 4


def tangled(cos, me):
    """The pairs of faces of a mesh that pass through each other (not meeting at a corner)."""
    polys = [list(p.vertices) for p in me.polygons]
    t = BVHTree.FromPolygons(cos, polys)
    return {(a, b) for a, b in t.overlap(t) if a < b and not set(polys[a]) & set(polys[b])}


def posed(ob, dg):
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    cos = [ob.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    return cos


def bone_of(ob, i, groups):
    vg = ob.data.vertices[i].groups
    return groups[max(vg, key=lambda g: g.weight).group] if len(vg) else "?"


def run():
    dg = bpy.context.evaluated_depsgraph_get()
    cache, result = {}, {}
    for c in bpy.data.collections["Pose_Review"].children:
        meshes = [o for o in c.objects if o.type == "MESH"]
        pose = {o: posed(o, dg) for o in meshes}
        report = {}
        for ob in meshes:
            k = ("self", ob.data.name)
            if k not in cache:
                cache[k] = tangled([v.co.copy() for v in ob.data.vertices], ob.data)
            new = tangled(pose[ob], ob.data) - cache[k]
            if new:
                report[f"self {ob.data.name}"] = {"points": len(new), "worst_mm": 0, "bones": {}}
        for outer in meshes:
            mo = outer.data
            if mo.name not in cache:
                rest = [v.co.copy() for v in mo.vertices]
                cache[mo.name] = (rest, tree(rest, mo), rims(mo))
            rest_o, t_rest, rim = cache[mo.name]
            t_pose = tree(pose[outer], mo)
            for inner in meshes:
                if inner is outer:
                    continue
                key = (inner.data.name, mo.name)
                if key not in cache:
                    cov, ins = [], set()
                    for v in inner.data.vertices:
                        loc, n, fi, dist = t_rest.find_nearest(v.co)
                        if loc is None:
                            continue
                        if dist <= NEAR and (v.co - loc).dot(n) < 0 and not near_rim(loc, rest_o, rim):
                            cov.append(v.index)
                        if inside(v.co, t_rest):
                            ins.add(v.index)
                    cache[key] = (cov, ins)
                cov, ins = cache[key]
                groups = {g.index: g.name for g in inner.vertex_groups}
                for kind in ("out", "into"):
                    bad, worst, bones = 0, 0.0, {}
                    if kind == "out":
                        for i in cov:
                            p = pose[inner][i]
                            loc, n, fi, _ = t_pose.find_nearest(p)
                            if loc is None or near_rim(loc, pose[outer], rim):
                                continue
                            d = (p - loc).dot(n)
                            if d > TOL:
                                bad += 1
                                worst = max(worst, d)
                                b = bone_of(inner, i, groups)
                                bones[b] = bones.get(b, 0) + 1
                    else:
                        for i in range(len(pose[inner])):
                            if i in ins:
                                continue
                            p = pose[inner][i]
                            loc, n, fi, dist = t_pose.find_nearest(p)
                            if loc is None or dist < TOL or not inside(p, t_pose):
                                continue
                            bad += 1
                            worst = max(worst, dist)
                            b = bone_of(inner, i, groups)
                            bones[b] = bones.get(b, 0) + 1
                    if bad:
                        report[f"{kind} {inner.data.name} > {mo.name}"] = {
                            "points": bad, "worst_mm": round(worst * 1000, 1), "bones": bones}
        if report:
            result[c.name] = report
    return result


CLIP_RESULT = run()
