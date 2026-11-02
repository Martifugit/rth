"""Creatures from a skeleton spec, so a creature is numbers, not hand-placed parts (the method study,
2026-10-28: the owner finds organic shapes, proportions and joints hard to get right by eye).

A spec is a dict:
  joints   name -> (f, s, u, w, h, mat): a point in the creature's own frame (forward, side to its
           left, up; metres, 1 tile = 1 m), its half-width across and half-height (or half-depth, on
           an upright chain) there, and the palette colour index of what grows from it. A joint
           whose name ends in ".L" has an ".R" twin mirrored across the middle.
  chains   lists of joints lofted as one continuous mass (head and body, a tail, a neck); ("up",
           [...]) for an upright chain (a goblin's trunk), whose sections face up the chain.
  bones    (parent, child) pairs: rigid limb segments. The parent end sinks into whatever it grows
           from; the child end carries a buried ball, so the next segment's joint never shows a seam.
  spikes   (base, tip, radius, mat, sides): ears, beaks, fangs, claws (points in the frame).
  paint    (point, facing, mat, size): marks painted onto the surface (eyes).
  colours  palette names; measure: the bounding box the creature must fill (length, width, height).

Three ways to build the same spec (the study's rows):
  A  rigid: chains lofted, limbs as rigid tapered segments with buried balls at the joints (the 2004
     game's way: docs/research/04-osrs-assets.md section 8, no skin weighting, joints hidden).
  B  skin: one continuous shell from Blender's Skin modifier over the whole skeleton, smoothed once
     and decimated back to A's triangle count, coloured by the nearest bone.
  C  hybrid: the chains as a Skin shell, the limbs rigid as in A.
Every creature faces Blender -Y and stands on the origin."""

import math

import bmesh
import bpy
from mathutils import Vector

import rth_art as A


def P(f, s, u):
    return Vector((s, -f, u))


def J(f, s, u, w, top, bottom=None, mat=0, wt=0.6, wb=0.75):
    """A joint with a faceted section: half-width w at its middle, `top` above and `bottom` below
    its line (on an upright chain, in front and behind), the top's half-width wt * w and the
    bottom's wb * w. Six faces round: a deep narrow chest, a flat back, a wedge of a head."""
    return dict(f=f, s=s, u=u, w=w, top=top, bottom=top if bottom is None else bottom, mat=mat, wt=wt, wb=wb, hexa=True)


def norm(j):
    """Any joint as a dict (a plain (f, s, u, w, h, mat) tuple is a round section)."""
    if isinstance(j, dict):
        return j
    f, s, u, w, h, m = j
    return dict(f=f, s=s, u=u, w=w, top=h, bottom=h, mat=m, wt=1.0, wb=1.0, hexa=False)


def expand(spec):
    """The spec with every '.L' joint, bone, spike and paint mirrored to its '.R' twin."""
    joints = {n: norm(j) for n, j in spec["joints"].items()}
    for n, j in list(joints.items()):
        if n.endswith(".L"):
            joints[n[:-2] + ".R"] = dict(j, s=-j["s"])

    def twin(n):
        return n[:-2] + ".R" if n.endswith(".L") else n
    bones = list(spec.get("bones", []))
    bones += [(twin(a), twin(b)) for a, b in spec.get("bones", []) if a.endswith(".L") or b.endswith(".L")]
    chains = []
    for c in spec.get("chains", []):
        up, names = (True, c[1]) if isinstance(c, tuple) else (False, c)
        chains.append((up, names))
        if any(n.endswith(".L") for n in names):
            chains.append((up, [twin(n) for n in names]))
    mirror = lambda p: (p[0], -p[1], p[2])  # noqa: E731
    spikes = list(spec.get("spikes", []))
    spikes += [(mirror(b), mirror(t), r, m, sd) for b, t, r, m, sd in spec.get("spikes", []) if abs(b[1]) > 1e-6]
    paint = list(spec.get("paint", []))
    paint += [(mirror(p), mirror(fc), m, sz) for p, fc, m, sz in spec.get("paint", []) if abs(p[1]) > 1e-6]
    return joints, chains, bones, spikes, paint


def pos(joints, n):
    j = joints[n]
    return P(j["f"], j["s"], j["u"])


def segments(joints, chains, bones):
    """Every bone of the skeleton as (start, end, colour): the chains' links and the limbs."""
    out = []
    for _, names in chains:
        for a, b in zip(names, names[1:]):
            out.append((pos(joints, a), pos(joints, b), joints[b]["mat"]))
    for a, b in bones:
        out.append((pos(joints, a), pos(joints, b), joints[b]["mat"]))
    return out


def loft_chain(bm, joints, names, up):
    """A chain lofted through its joints' sections: round ones (8 sides) or faceted ones (6: top
    corners, middle, bottom corners), facing along the chain; an upright chain's sections face up it
    with their 'top' to the front."""
    pts = [pos(joints, n) for n in names]
    ref = Vector((0, -1, 0)) if up else Vector((0, 0, 1))
    rings = []
    for i, n in enumerate(names):
        j = joints[n]
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        upv = (ref - t * ref.dot(t)).normalized()
        side = t.cross(upv)
        c = pts[i]
        if j["w"] == 0 and j["top"] == 0:
            rings.append([bm.verts.new(c)])
            continue
        if j["hexa"]:
            prof = [(j["w"] * j["wt"], j["top"]), (j["w"], 0.0), (j["w"] * j["wb"], -j["bottom"]),
                    (-j["w"] * j["wb"], -j["bottom"]), (-j["w"], 0.0), (-j["w"] * j["wt"], j["top"])]
        else:
            prof = [(j["w"] * math.cos(math.pi / 8 + k * math.pi / 4), (j["top"] if k < 4 else j["bottom"]) * math.sin(math.pi / 8 + k * math.pi / 4))
                    for k in range(8)]
        rings.append([bm.verts.new(c + side * x + upv * y) for x, y in prof])
    n_side = max(len(r) for r in rings)
    for i in range(len(rings) - 1):
        r0, r1, m = rings[i], rings[i + 1], joints[names[i + 1]]["mat"]
        for k in range(n_side):
            q = (k + 1) % n_side
            if len(r0) == 1:
                f = bm.faces.new((r0[0], r1[k], r1[q]))
            elif len(r1) == 1:
                f = bm.faces.new((r0[k], r0[q], r1[0]))
            else:
                f = bm.faces.new((r0[k], r0[q], r1[q], r1[k]))
            f.material_index = m
    for r, n in ((rings[0], names[0]), (rings[-1], names[-1])):
        if len(r) > 2:
            bm.faces.new(r).material_index = joints[n]["mat"]


def rigid_bone(bm, joints, a, b, sides=6):
    """A rigid limb segment from a (sunk in its parent) to b, tapering from a's radius to b's, and
    a buried ball at b for the next segment to turn in."""
    pa, pb = pos(joints, a), pos(joints, b)
    ra, rb, m = joints[a]["w"], joints[b]["w"], joints[b]["mat"]
    d = (pb - pa)
    if d.length < 1e-6:
        return
    ref = (0, 0, 1) if abs(d.normalized().z) < 0.9 else (0, 1, 0)
    A.loft(bm, [(tuple(pa), ra * 0.9, ra * 0.9), (tuple(pb), rb, rb)], sides=sides, ref=ref, mats=m, roll=math.pi / sides)
    t = d.normalized()
    A.loft(bm, [(tuple(pb - t * rb * 1.05), 0, 0), (tuple(pb - t * rb * 0.5), rb * 1.05, rb * 1.05),
                (tuple(pb + t * rb * 0.5), rb * 1.05, rb * 1.05), (tuple(pb + t * rb * 1.05), 0, 0)],
           sides=sides, ref=ref, mats=m, roll=math.pi / sides)


def add_details(bm, spikes, paint):
    for base, tip, r, m, sides in spikes:
        A.spike(bm, tuple(P(*base)), r, tuple(P(*tip)), sides=sides, mat=m)
    for p, facing, m, size in paint:
        A.paint(bm, tuple(P(*p)), m, facing=tuple(P(*facing)), size=size)


def skin_shell(name, joints, links, coll, subsurf=1):
    """A Skin modifier shell over the given links (pairs of joint names), smoothed `subsurf` times,
    as a plain mesh object."""
    names = sorted({n for l in links for n in l})
    index = {n: i for i, n in enumerate(names)}
    me = bpy.data.meshes.new(name + "_skin")
    me.from_pydata([tuple(pos(joints, n)) for n in names], [(index[a], index[b]) for a, b in links], [])
    ob = bpy.data.objects.new(name + "_skin", me)
    coll.objects.link(ob)
    mod = ob.modifiers.new("Skin", "SKIN")
    mod.branch_smoothing = 0.6
    mod.use_smooth_shade = True
    for n, i in index.items():
        w, h = joints[n]["w"], (joints[n]["top"] + joints[n]["bottom"]) / 2
        ob.data.skin_vertices[0].data[i].radius = (max(w, 0.006), max(h, 0.006))
    ob.data.skin_vertices[0].data[0].use_root = True
    if subsurf:
        ob.modifiers.new("Smooth", "SUBSURF").levels = subsurf
    return ob


def bake(ob):
    """An object's mesh with its modifiers applied, as a bmesh; the object removed."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    return bm


def decimate_to(bm, triangles):
    """Collapse a bmesh down to about `triangles` (no-op if it is already under)."""
    bmesh.ops.triangulate(bm, faces=bm.faces)
    have = len(bm.faces)
    if have <= triangles:
        return bm
    me = bpy.data.meshes.new("dec")
    bm.to_mesh(me)
    ob = bpy.data.objects.new("dec", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = triangles / have
    out = bake(ob)
    bpy.data.meshes.remove(me)
    return out


def colour_by_bone(bm, segs):
    """Give each face the colour of the bone nearest its middle (a skin shell has no parts)."""
    def dist(p, a, b):
        ab = b - a
        t = 0.0 if ab.length < 1e-9 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
        return (a + ab * t - p).length
    for f in bm.faces:
        c = f.calc_center_median()
        f.material_index = min(segs, key=lambda s: dist(c, s[0], s[1]))[2]


def build(spec, method="A", coll="Creatures"):
    """The creature by method A (rigid), B (skin) or C (hybrid); its object, named spec['name']."""
    joints, chains, bones, spikes, paint = expand(spec)
    name = "%s_%s" % (spec["name"], method)
    target = A.collection(coll)
    if method == "A":
        bm = bmesh.new()
        for up, names in chains:
            loft_chain(bm, joints, names, up)
        for a, b in bones:
            rigid_bone(bm, joints, a, b, spec.get("limb_sides", 6))
    else:
        links = [l for _, names in chains for l in zip(names, names[1:])]
        if method == "B":
            links += bones
        bm = bake(skin_shell(name, joints, links, target))
        rigid_tris = rigid_count(spec, method)
        bm = decimate_to(bm, rigid_tris)
        colour_by_bone(bm, segments(joints, chains, bones if method == "B" else []))
        if method == "C":
            for a, b in bones:
                rigid_bone(bm, joints, a, b)
    add_details(bm, spikes, paint)
    A.finish(bm)
    return A.mesh_from(name, bm, spec["colours"], coll=coll, smooth_angle=55)


def rigid_count(spec, method):
    """Triangles A gives for the part the skin replaces (so the methods compare at equal cost)."""
    joints, chains, bones, _, _ = expand(spec)
    bm = bmesh.new()
    for up, names in chains:
        loft_chain(bm, joints, names, up)
    if method == "B":
        for a, b in bones:
            rigid_bone(bm, joints, a, b)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    n = len(bm.faces)
    bm.free()
    return int(n * 1.15)


def skeleton(spec, coll="Creatures"):
    """The spec drawn as a stick figure: thin rods for the bones, a small knob at each joint, in one
    bright colour, for judging proportions against the built creature."""
    joints, chains, bones, _, _ = expand(spec)
    bm = bmesh.new()
    for a, b, _ in segments(joints, chains, bones):
        if (b - a).length > 1e-6:
            ref = (0, 0, 1) if abs((b - a).normalized().z) < 0.9 else (0, 1, 0)
            A.loft(bm, [(tuple(a), 0.008, 0.008), (tuple(b), 0.008, 0.008)], sides=3, ref=ref, mats=0)
    for n in joints:
        A.prism(bm, 4, 0.02, 0.02, pos(joints, n).z - 0.02, pos(joints, n).z + 0.02, center=pos(joints, n).xy, mat_index=0,
                cap_bottom=True)
    A.finish(bm)
    return A.mesh_from("%s_skeleton" % spec["name"], bm, ["BloodSheen"], coll=coll)


def measure(ob, spec):
    """The built creature's bounding box against the spec's: each dimension and how far off it is."""
    vs = [ob.matrix_world @ v.co for v in ob.data.vertices]
    got = {"length": max(v.y for v in vs) - min(v.y for v in vs), "width": max(v.x for v in vs) - min(v.x for v in vs),
           "height": max(v.z for v in vs) - min(0.0, min(v.z for v in vs))}
    want = spec.get("measure", {})
    return {k: (round(got[k], 2), round(100 * (got[k] - want[k]) / want[k]) if k in want else None) for k in got}
