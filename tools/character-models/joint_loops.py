"""
Joint rings for the review bodies, so they bend well: the original bodies had a single 50/50 ring
at the knee and elbow, none where the spine meets the hips, one at the hip joint, and a seat that
moved only with the hips, so deep bends pinched (a thin, pointed knee) and stretched (the seat
when the thigh lifts). Garments copy their weights from the body (build_wear.py), so they inherit
all of this.

Each ring is a flat cut across the existing faces (bisect_plane), so the shape does not change:
only the joint gains vertices, whose weights are blended from their neighbours along the cut
edge, which gives graded rings (75/25 and 25/75 round a 50/50 knee). Where a ring already lies
close to the place, nothing is cut, so running this again changes nothing.

- knees and elbows: rings `JOINT_GAP` above and below the joint;
- the hip joint: a ring `THIGH_RING` below the crotch;
- the spine on the hips: a ring at the joint, weighted 50/50;
- the seat: the back of the pelvis gets a share of the thigh (`SEAT`), fading from the crotch up.

Run inside Blender with BodyReview.blend open, through the Blender MCP: exec this file.
"""

import bmesh
import bpy
from mathutils import Vector

BODIES = {
    "male": dict(rig="Rig.004", torso="torso_draft", arms="body_arms.003", legs="wear_trousers"),
    "female": dict(rig="Rig.005", torso="torso_draft.001", arms="body_arms.004", legs="wear_trousers.001"),
}
JOINT_GAP = 0.055        # metres either side of a knee or elbow
THIGH_RING = 0.075       # metres below the crotch
SEAT = dict(share=0.35, height=0.12)   # the thigh's share at the crotch, gone this far above it
NEAR = 0.012             # a ring already this close counts as there


def has_ring(me, axis, value, side=None):
    """Whether the mesh already has (most of) a ring at this height (axis 2) or reach (axis 0)."""
    pts = [v.co for v in me.vertices if side is None or v.co.x * side > 0]
    return sum(1 for p in pts if abs(p[axis] - value) < NEAR) >= 5


def cut(ob, axis, value, side=None):
    """A flat cut across the mesh at that height or reach (only on one side's arm when `side`)."""
    me = ob.data
    if has_ring(me, axis, value, side):
        return 0
    bm = bmesh.new()
    bm.from_mesh(me)
    no = Vector((0, 0, 0))
    no[axis] = 1.0
    co = Vector((0, 0, 0))
    co[axis] = value
    geom = [g for g in list(bm.verts) + list(bm.edges) + list(bm.faces)
            if side is None or _on_side(g, side)]
    before = len(bm.verts)
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no)
    made = len(bm.verts) - before
    bm.to_mesh(me)
    bm.free()
    me.update()
    return made


def _on_side(g, side):
    vs = [g] if isinstance(g, bmesh.types.BMVert) else list(g.verts)
    return all(v.co.x * side > 0 for v in vs)


def seat(ob, rig, crotch):
    """The back of the pelvis follows the thigh a little, fading upward from the crotch."""
    groups = {g.name: g for g in ob.vertex_groups}
    me = ob.data
    cy = sum(v.co.y for v in me.vertices) / len(me.vertices)
    changed = 0
    for v in me.vertices:
        if not (crotch <= v.co.z <= crotch + SEAT["height"]) or v.co.y > cy - 0.01 or abs(v.co.x) < 0.015:
            continue
        thigh = groups["thigh." + ("R" if v.co.x > 0 else "L")]
        share = SEAT["share"] * (1 - (v.co.z - crotch) / SEAT["height"])
        ws = {ob.vertex_groups[g.group].name: g.weight for g in v.groups}
        if ws.get(thigh.name, 0) >= share:
            continue
        rest = sum(w for n, w in ws.items() if n != thigh.name)
        scale = (1 - share) / rest if rest else 0
        for n, w in ws.items():
            if n != thigh.name:
                groups[n].add([v.index], w * scale, "REPLACE")
        thigh.add([v.index], share, "REPLACE")
        changed += 1
    return changed


def crotch_of(legs):
    """The crotch: the lowest point weighted at least 40% to the hips (the legs' first shared
    ring). Not the lowest point near the middle: her thighs come within 2 cm of it at the knee."""
    hips = legs.vertex_groups["hips"].index
    return min(v.co.z for v in legs.data.vertices
               if any(g.group == hips and g.weight >= 0.4 for g in v.groups))


def grade(ob, parent, child, joint, axis, sign, reach):
    """Weights round a joint by distance along the limb: 50/50 at the joint, all the child's
    `reach` beyond it, all the parent's `reach` before it. Only vertices on these two bones."""
    groups = {g.name: g for g in ob.vertex_groups}
    names = {parent, child}
    n = 0
    for v in ob.data.vertices:
        if v.co.x * (1 if parent.endswith("R") else -1) <= 0:
            continue
        d = (joint - v.co[axis]) * sign            # how far past the joint toward the child
        if abs(d) > reach:
            continue
        on = {ob.vertex_groups[g.group].name for g in v.groups if g.weight > 0.001}
        if not on or not on <= names:
            continue
        w = min(1.0, max(0.0, 0.5 + d / (2 * reach)))
        for g in list(v.groups):
            ob.vertex_groups[g.group].remove([v.index])
        if w < 1:
            groups[parent].add([v.index], 1 - w, "REPLACE")
        if w > 0:
            groups[child].add([v.index], w, "REPLACE")
        n += 1
    return n


def spine_ring(ob, z):
    """The ring at (or within 1.5 cm of) the spine's joint, half hips and half spine."""
    groups = {g.name: g for g in ob.vertex_groups}
    levels = {}
    for v in ob.data.vertices:
        levels.setdefault(round(v.co.z, 2), []).append(v)       # a ring may wander a few mm
    near = min(levels, key=lambda h: abs(h - z))
    if abs(near - z) > 0.015 or len(levels[near]) < 5:
        return 0
    n = 0
    for v in ob.data.vertices:
        if v in levels[near]:
            for g in list(v.groups):
                ob.vertex_groups[g.group].remove([v.index])
            groups["hips"].add([v.index], 0.5, "REPLACE")
            groups["spine"].add([v.index], 0.5, "REPLACE")
            n += 1
    return n


def run():
    report = {}
    for sex, b in BODIES.items():
        rig = bpy.data.objects[b["rig"]]
        bones = rig.data.bones
        legs, torso, arms = (bpy.data.objects[b[k]] for k in ("legs", "torso", "arms"))
        knee = bones["shin.R"].head_local.z
        crotch = crotch_of(legs)
        spine = bones["spine"].head_local.z
        r = {}
        r["knee"] = cut(legs, 2, knee + JOINT_GAP) + cut(legs, 2, knee - JOINT_GAP)
        r["thigh"] = cut(legs, 2, crotch - THIGH_RING)
        r["spine"] = cut(torso, 2, spine)
        r["spine_ring"] = spine_ring(torso, spine)
        elbow = bones["forearm.R"].head_local.x
        r["elbows"] = sum(cut(arms, 0, s * (elbow + d), side=s) for s in (1, -1) for d in (JOINT_GAP, -JOINT_GAP))
        r["seat"] = seat(legs, rig, crotch)
        reach = JOINT_GAP + 0.01
        r["graded"] = sum(grade(legs, "thigh." + s, "shin." + s, knee, 2, 1, reach) for s in "LR") +             sum(grade(arms, "upper_arm." + s, "forearm." + s, bones["forearm." + s].head_local.x, 0,
                      -1 if s == "R" else 1, reach) for s in "LR")
        report[sex] = r
    return report


JOINT_RESULT = run()
