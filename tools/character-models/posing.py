"""
Poses for pose_review.py (posed copies to look at), and the skirt rig every pose drives.

A pose is {bone: rotation} in the armature's own axes (x right, y forward, z up), applied at each
bone relative to its parent, in degrees through R(). About x, a positive angle swings a bone that
hangs down forward (a thigh, an arm) but tips one that points up backward (the spine): lean
forward with a negative angle. "hips": "drop" lowers the whole body until the lowest point of the
feet is back on the ground (crouching, sitting).

**The skirt rig.** Plain skinning cannot keep a skirt over two legs (the front, blended between
the thighs, stays halfway between them, and a leg comes through), and weights fitted to chosen
poses only hold near those poses. So skirts, robes and the skirts of tunics hang from a ring of
`CHAINS` short bone chains on the hips (`skirt.<k>.1` from the top of the skirt, the hips, to the
knees' height, `skirt.<k>.2` on to the ankles; k = 0 at the front, going round to the left), never
animated: every frame, after the animation, each segment hangs straight down (as near as its
plane allows) and swings outward in the vertical plane through its chain, only as far as it must
to clear capsules round the hidden thighs and shins (`skirt_capsules` on the rig: their radii,
measured from the body); then no chain may hang more than SAG below a neighbour (cloth between
them is held taut: the front drapes between the knees rather than dropping straight down). So
the cloth always lies outside where the legs are, in any animation: over the lap sitting, behind
the trailing calf walking. The chains hang from the hips, not the hip joints: from level with a
raised thigh's axis no straight segment can lie on top of the thigh. `solve_chain` is the whole rule, written so the
client (Unity) can do exactly the same; `drive` applies it to a Blender rig.
"""
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

FEET = ("foot.L", "foot.R", "toe.L", "toe.R")
CHAINS = 12
LEG_BONES = ("thigh.L", "thigh.R", "shin.L", "shin.R")
CLEAR = 0.012              # how far a chain keeps beyond a leg's capsule
THIGH_FROM = 0.3           # a thigh's capsule starts this far down it: above, it is inside the pelvis,
                           # where the chains hang from
SKIP = 0.15                # the share of a segment, from its head, the clearance test skips
SAG = 35                   # the most a chain hangs below a neighbour, degrees
SWEEP = 160                # the furthest a segment swings out from hanging, degrees
STEP = 2                   # in steps of


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 3, axis)


def mirror(pose):
    """The same pose on the other side: .L and .R swapped, every rotation reflected in x."""
    s = Matrix.Diagonal((-1, 1, 1))
    out = {}
    for name, rot in pose.items():
        other = name[:-1] + {"L": "R", "R": "L"}[name[-1]] if name[-2:] in (".L", ".R") else name
        out[other] = rot if isinstance(rot, str) else s @ rot @ s
    return out


def lowest(rig, posed):
    bones = rig.pose.bones if posed else rig.data.bones
    if posed:
        return min(min(bones[n].head.z, bones[n].tail.z) for n in FEET)
    return min(min(bones[n].head_local.z, bones[n].tail_local.z) for n in FEET)


# ---------------------------------------------------------------------------------------------
# The skirt rig.

def chain_names(k):
    return f"skirt.{k}.1", f"skirt.{k}.2"


def ensure_skirt(rig, body_points, leg_points, top_z):
    """Gives a rig its skirt chains (and drops the old skirt helpers), and measures the legs'
    capsules. `body_points`: the body's points in armature space (legs and torso), whose outline
    at `top_z` (the hips: where skirts hang from) the chains stand just outside; `leg_points`:
    {bone: [points]} of the legs, the points weighted mostly to each leg bone."""
    bones = rig.data.bones
    hip_z = top_z
    knee_z = bones["shin.L"].head_local.z
    ankle_z = bones["shin.L"].tail_local.z
    ring = []
    for k in range(CHAINS):
        a = 2 * math.pi * k / CHAINS
        d = Vector((-math.sin(a), math.cos(a), 0))            # k = 0 forward (+y), then round to the left
        # hanging clear of the widest the body is below it (the seat, the thighs), as cloth does
        near = [p for p in body_points if knee_z < p.z < hip_z + 0.02 and
                Vector((p.x, p.y, 0)).normalized().dot(d) > math.cos(math.radians(25))]
        r = max((Vector((p.x, p.y, 0)).dot(d) for p in near), default=0.15) + 0.005
        ring.append(Vector((d.x * r, d.y * r, hip_z)))
    caps = {}
    for name in LEG_BONES:
        # A typical radius (the 80th percentile: the farthest points are the seat and the knee's
        # corners, which made the capsules fat), and never so fat that the chains, hanging
        # straight, would not clear it when standing.
        b = bones[name]
        ds = sorted(seg_point(p, b.head_local, b.tail_local) for p in leg_points[name]) or [0.06]
        r = ds[int(0.8 * (len(ds) - 1))]
        a_, b_, _ = capsule(b.head_local, b.tail_local, name, 0)
        for k, head in enumerate(ring):
            r = min(r, seg_seg(head, Vector((head.x, head.y, ankle_z)), a_, b_) - CLEAR - 0.003)
        caps[name] = max(r, 0.02)
    rig["skirt_capsules"] = caps
    old = [n for n in ("skirt.F", "skirt.B", "skirt.F.2", "skirt.B.2") if n in bones]
    if not old and all(chain_names(k)[0] in bones and (bones[chain_names(k)[0]].head_local - ring[k]).length < 1e-4
                       for k in range(CHAINS)):
        return
    view = bpy.context.view_layer
    active, hidden = view.objects.active, rig.hide_get()
    rig.hide_set(False)
    view.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    for n in old:
        eb.remove(eb[n])
    for k, head in enumerate(ring):
        n1, n2 = chain_names(k)
        b1 = eb.get(n1) or eb.new(n1)
        b1.head, b1.tail = head, Vector((head.x, head.y, knee_z))
        b1.parent, b1.use_deform = eb["hips"], True
        b2 = eb.get(n2) or eb.new(n2)
        b2.head, b2.tail = b1.tail.copy(), Vector((head.x, head.y, ankle_z))
        b2.parent, b2.use_connect, b2.use_deform = b1, True, True
    bpy.ops.object.mode_set(mode="OBJECT")
    view.objects.active = active
    rig.hide_set(hidden)


def capsule(head, tail, name, r):
    """A leg bone's capsule: (start, end, radius); a thigh's starts THIGH_FROM down it."""
    start = head.lerp(tail, THIGH_FROM) if name.startswith("thigh") else head.copy()
    return start, tail.copy(), r


def seg_point(p, a, b):
    """The distance from a point to a segment."""
    d = b - a
    t = max(0.0, min(1.0, (p - a).dot(d) / max(1e-12, d.dot(d))))
    return (p - (a + d * t)).length


def seg_seg(p1, q1, p2, q2):
    """The least distance between two segments."""
    d1, d2, r = q1 - p1, q2 - p2, p1 - p2
    a, e, f = d1.dot(d1), d2.dot(d2), d2.dot(r)
    c, b = d1.dot(r), d1.dot(d2)
    den = a * e - b * b
    s = max(0.0, min(1.0, (b * f - c * e) / den)) if den > 1e-12 else 0.0
    t = (b * s + f) / e if e > 1e-12 else 0.0
    if t < 0:
        t, s = 0.0, max(0.0, min(1.0, -c / a)) if a > 1e-12 else 0.0
    elif t > 1:
        t, s = 1.0, max(0.0, min(1.0, (b - c) / a)) if a > 1e-12 else 0.0
    return ((p1 + d1 * s) - (p2 + d2 * t)).length


def solve_chain(head, length, down, out, capsules, least=None):
    """How far a skirt segment swings out (radians): it points along down * cos + out * sin, in the
    plane of `down` (the hips' down) and `out` (its chain's outward direction, both unit and at
    right angles), starting as near straight down as that plane allows (or at `least`) and
    swinging outward until the segment (past its first SKIP, which may start close to a capsule)
    clears every capsule (start, end, radius) by CLEAR. If no swing clears them all, the one that
    comes closest."""
    world_down = Vector((0, 0, -1))
    start = math.atan2(world_down.dot(out), world_down.dot(down))
    start = max(-0.5, min(1.5, start))
    if least is not None:
        start = max(start, least)
    best, best_gap = start, -1e9
    for i in range(0, SWEEP // STEP + 1):
        th = start + math.radians(i * STEP)
        d = down * math.cos(th) + out * math.sin(th)
        a, b = head + d * (SKIP * length), head + d * length
        gap = min((seg_seg(a, b, h, t) - r - CLEAR for h, t, r in capsules), default=1.0)
        if gap >= 0:
            return th
        if gap > best_gap:
            best, best_gap = th, gap
    return best


def taut(angles, solve):
    """No chain more than SAG below a neighbour: raise the lowest ones (and solve them again from
    there, so they still clear), round the ring until none moves."""
    sag = math.radians(SAG)
    for _ in range(len(angles)):
        moved = False
        for k in range(len(angles)):
            need = max(angles[k - 1], angles[(k + 1) % len(angles)]) - sag
            if angles[k] < need - 1e-6:
                angles[k] = solve(k, need)
                moved = True
        if not moved:
            break
    return angles


def panels(angles, heads, along, lengths, capsules):
    """The cloth between two neighbouring chains must clear the capsules too (a trailing shin went
    between two chains, each clear on its own): where a panel, sampled across, comes through one,
    both its chains swing further out, a step at a time."""
    n, step, most = len(angles), math.radians(STEP), math.radians(SWEEP)
    for _ in range(SWEEP // STEP):
        moved = False
        for k in range(n):
            j = (k + 1) % n
            ends = [heads[i] + along(i, angles[i]) * lengths[i] for i in (k, j)]
            for t in (0.25, 0.5, 0.75):
                h = heads[k].lerp(heads[j], t)
                e = ends[0].lerp(ends[1], t)
                a = h.lerp(e, SKIP)
                if min((seg_seg(a, e, c0, c1) - r - CLEAR for c0, c1, r in capsules), default=1.0) < 0:
                    for i in (k, j):
                        angles[i] = min(angles[i] + step, most)
                    moved = True
                    break
        if not moved:
            break
    return angles


def drive(rig):
    """Sets the skirt chains from the posed legs (the pose must be evaluated)."""
    caps = rig.get("skirt_capsules")
    if not caps or chain_names(0)[0] not in rig.pose.bones:
        return
    pbs = rig.pose.bones
    capsules = [capsule(pbs[n].head, pbs[n].tail, n, r) for n, r in caps.items()]
    hips = pbs["hips"]
    move = hips.matrix @ hips.bone.matrix_local.inverted()        # rest to posed, armature space
    turn = move.to_3x3()
    down = (turn @ Vector((0, 0, -1))).normalized()
    b1s = [pbs[chain_names(k)[0]] for k in range(CHAINS)]
    b2s = [pbs[chain_names(k)[1]] for k in range(CHAINS)]
    outs = [(turn @ Vector((b.bone.head_local.x, b.bone.head_local.y, 0)).normalized()).normalized() for b in b1s]
    heads = [move @ b.bone.head_local for b in b1s]

    def along(k, th):
        return down * math.cos(th) + outs[k] * math.sin(th)

    def upper(k, least=None):
        return solve_chain(heads[k], b1s[k].bone.length, down, outs[k], capsules, least)
    a1 = taut([upper(k) for k in range(CHAINS)], upper)
    a1 = taut(panels(a1, heads, along, [b.bone.length for b in b1s], capsules), upper)
    knees = [heads[k] + along(k, a1[k]) * b1s[k].bone.length for k in range(CHAINS)]

    def lower(k, least=None):
        return solve_chain(knees[k], b2s[k].bone.length, down, outs[k], capsules, least)
    a2 = taut([lower(k) for k in range(CHAINS)], lower)
    a2 = taut(panels(a2, knees, along, [b.bone.length for b in b2s], capsules), lower)
    for k in range(CHAINS):
        point(b1s[k], heads[k], along(k, a1[k]))
    bpy.context.view_layer.update()
    for k in range(CHAINS):
        point(b2s[k], knees[k], along(k, a2[k]))


# ---------------------------------------------------------------------------------------------
# Half bones: rigid armour over a joint (a pauldron, a couter at the elbow, a poleyn at the knee)
# follows a bone that turns half as far as the joint, so it sits over the bend from either side
# and never stretches. Each is a copy of the joint's own bone (same head, direction and roll) on
# the same parent, turned by half of that bone's turn every frame (here, and by the client).

HALVES = ("upper_arm.L", "upper_arm.R", "forearm.L", "forearm.R", "shin.L", "shin.R")
PIVOT = {"upper_arm": Vector((0, 0, 0.06))}   # a pauldron hinges over the top of the shoulder, not
                                              # at the joint (there its inner edge sank into the shoulder)


def half_name(bone):
    return "half." + bone


def ensure_halves(rig):
    """Adds the half bones a rig lacks (or puts them back where they belong)."""
    def head_of(name, bones):
        return bones[name].head_local + PIVOT.get(name.split(".")[0], Vector())
    bones = rig.data.bones
    missing = [b for b in HALVES if half_name(b) not in bones or
               (bones[half_name(b)].head_local - head_of(b, bones)).length > 1e-4]
    if not missing:
        return
    view = bpy.context.view_layer
    active, hidden = view.objects.active, rig.hide_get()
    rig.hide_set(False)
    view.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    for name in missing:
        src = eb[name]
        h = eb.get(half_name(name)) or eb.new(half_name(name))
        head = src.head + PIVOT.get(name.split(".")[0], Vector())
        h.head, h.tail, h.roll = head, head + (src.tail - src.head) * 0.4, src.roll
        h.parent, h.use_deform = src.parent, True
    bpy.ops.object.mode_set(mode="OBJECT")
    view.objects.active = active
    rig.hide_set(hidden)


def drive_halves(rig):
    """Turns each half bone half as far as its joint's bone (from their shared rest)."""
    for name in HALVES:
        hn = half_name(name)
        if hn not in rig.pose.bones:
            continue
        src, half = rig.pose.bones[name], rig.pose.bones[hn]
        src.rotation_mode = half.rotation_mode = "QUATERNION"
        half.rotation_quaternion = Quaternion().slerp(src.rotation_quaternion, 0.5)


def point(pb, head, d):
    """Poses a bone (resting straight down) to start at `head` and point along `d`."""
    rest = pb.bone.matrix_local.to_3x3()
    turn = Vector((0, 0, -1)).rotation_difference(d).to_matrix()
    m = (turn @ rest).to_4x4()
    m.translation = head
    pb.matrix = m


def set_pose(rig, pose):
    """Poses a rig (which must be in the view layer, so its pose can be evaluated), and drives its
    skirt."""
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    for name, rot in pose.items():
        if isinstance(rot, str):
            continue
        pb = rig.pose.bones[name]
        m = pb.bone.matrix_local.to_3x3()
        pb.rotation_quaternion = (m.inverted() @ rot @ m).to_quaternion()
    drive_halves(rig)
    if pose.get("hips") == "drop":
        bpy.context.view_layer.update()
        drop = lowest(rig, True) - lowest(rig, False)
        rig.pose.bones["hips"].location = (0, -drop, 0)       # the hips bone points up: its y is up
    bpy.context.view_layer.update()
    drive(rig)
    bpy.context.view_layer.update()


WALK = {
    "upper_arm.L": R("X", 22) @ R("Y", -78), "upper_arm.R": R("X", -22) @ R("Y", 78),
    "forearm.L": R("X", 15), "forearm.R": R("X", 5),
    "thigh.L": R("X", 26), "shin.L": R("X", -8),
    "thigh.R": R("X", -18), "shin.R": R("X", -38), "foot.R": R("X", 20),
}
STRIDE = {"hips": "drop", "thigh.L": R("X", 40), "shin.L": R("X", -20), "foot.L": R("X", -10),
          "thigh.R": R("X", -28), "shin.R": R("X", -30), "foot.R": R("X", 25)}
SIT = {                                                        # on a low stool, feet on the ground
    "hips": "drop", "spine": R("X", -6),
    "thigh.L": R("X", 88), "thigh.R": R("X", 88),
    "shin.L": R("X", -88), "shin.R": R("X", -88),
    "upper_arm.L": R("X", 30) @ R("Y", -78), "upper_arm.R": R("X", 30) @ R("Y", 78),
    "forearm.L": R("X", 35), "forearm.R": R("X", 35),
}
CROUCH = {
    "hips": "drop", "spine": R("X", -22),
    "thigh.L": R("X", 80), "thigh.R": R("X", 80),
    "shin.L": R("X", -115), "shin.R": R("X", -115),
    "foot.L": R("X", 35), "foot.R": R("X", 35),
    "upper_arm.L": R("X", 45) @ R("Y", -75), "upper_arm.R": R("X", 45) @ R("Y", 75),
}
HIGH_STEP = {"hips": "drop", "thigh.L": R("X", 70), "shin.L": R("X", -75), "foot.L": R("X", 10)}
WIDE = {"thigh.L": R("Y", 18), "thigh.R": R("Y", -18), "shin.L": R("Y", -6), "shin.R": R("Y", 6)}
KNEEL = {"hips": "drop", "thigh.L": R("X", 85), "shin.L": R("X", -95),
         "thigh.R": R("X", -5), "shin.R": R("X", -95), "foot.R": R("X", 60)}   # one knee down
KICK = {"thigh.L": R("X", 75), "shin.L": R("X", -10)}
