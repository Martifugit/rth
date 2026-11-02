"""
Builds clothing for build_body.py's bodies: each garment a skinned mesh on the same rig that
replaces the kit parts it covers (the client hides them by part name), with materials named by
colour channel (primary, trim, leather, skin...), so every garment has many colourways.

Garments follow the classic kit's way (measured, never traced): a top is the default outfit's box
shirt (wear_shirt) and the bare arms (hand tuning included, body_tuning.json), eased out a little,
square shoulders kept,
so clothes match the body. What hangs loose is a single open sheet of a few big flat sides, not a
closed tube: a skirt is tucked under a loose belt band and flares hard to its hem (wider behind),
straight from top to hem, pushed out until it clears the trousers at every height. Where a
garment meets a part it does not replace it ends on that part's own ring (the fist's wrist), so
it joins with no gap. Colour blocks (a belt, trim bands) do the rest instead of texture. Rounding
everything (v2) or hanging a closed box skirt off the shirt (v1) both looked wrong.

Run inside Blender (5.x) through the Blender MCP with SEX ("male"/"female") and, for a look in
the open scene, PREVIEW_RIG (the body's rig object to wear it on) and GARMENT.
"""

import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\dev\rth\tools\character-models"
SEX = globals().get("SEX", "male")


def load_body(sex):
    """build_body.py's tables and functions for one sex, without building anything."""
    path = os.path.join(HERE, "build_body.py")
    ns = {"SEX": sex, "PREVIEW": True, "LIBRARY": True, "__file__": path, "__name__": "build_body"}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return ns


B = load_body(SEX)
H = B["HEIGHT"]

# A garment ring's twelve directions, from the front (+y) round by the right (+x).
AROUND = 12
DIRS = [Vector((math.sin(2 * math.pi * k / AROUND), math.cos(2 * math.pi * k / AROUND))) for k in range(AROUND)]
RIGHT_SIDE, LEFT_SIDE = (2, 3, 4), (10, 9, 8)   # front, side and back points of each side


# ---------------------------------------------------------------------------------------------
# Reading the (tuned) body.

def tuned(name):
    """A built, tuned body part's vertex positions."""
    p = B["built_part"](name)
    verts = [v.co.copy() for v in p.bm.verts]
    faces = [[v.co.copy() for v in f.verts] for f in p.bm.faces]
    p.bm.free()
    return verts, faces


def shirt_faces(verts):
    """The tuned shirt as a closed loft, armholes filled, for measuring a torso without holes."""
    n = len(B["SHIRT"])
    rings = [verts[8 * i:8 * i + 8] for i in range(n)]
    faces = []
    for r0, r1 in zip(rings, rings[1:]):
        faces += [[r0[k], r0[(k + 1) % 8], r1[(k + 1) % 8], r1[k]] for k in range(8)]
    return faces, rings


def slice_at(faces, z):
    """Where a surface crosses the height z (metres): a list of 2D segments. A height exactly on
    a ring is nudged just below it (or above, at the top) so it still cuts faces."""
    for dz in (-1e-5, 1e-5):
        segs = []
        for f in faces:
            pts = []
            for a, b in zip(f, f[1:] + f[:1]):
                if (a.z - z - dz) * (b.z - z - dz) < 0:
                    t = (z + dz - a.z) / (b.z - a.z)
                    pts.append((a + (b - a) * t).xy)
            if len(pts) == 2:
                segs.append(pts)
        if segs:
            return segs
    return []


def reach(segs, d, c=Vector((0.0, 0.0))):
    """How far out from c, along d, the sliced surface goes (0 if the ray misses it)."""
    best = 0.0
    for p, q in segs:
        e = q - p
        den = d.x * e.y - d.y * e.x
        if abs(den) < 1e-12:
            continue
        w = p - c
        t = (w.x * e.y - w.y * e.x) / den
        u = (w.x * d.y - w.y * d.x) / den
        if t > 0 and -1e-6 <= u <= 1 + 1e-6:
            best = max(best, t)
    return best


def measure(faces, z):
    """The body's reach round the twelve directions at a height (shares of height)."""
    segs = slice_at(faces, z * H)
    return [reach(segs, d) / H for d in DIRS]


def clearance(faces, z, ring_pts, ease):
    """How far out (metres) each point of a flat-sided ring must sit, along its own direction, to
    clear a sliced surface at height z (shares) by ease: the reach there and half way to each
    neighbour, allowing for the side cutting in between."""
    segs = slice_at(faces, z * H)
    n = len(ring_pts)
    out = []
    for i, v in enumerate(ring_pts):
        a = math.atan2(v.x, v.y)
        half = min(abs(math.remainder(math.atan2(w.x, w.y) - a, 2 * math.pi)) for w in (ring_pts[i - 1], ring_pts[(i + 1) % n])) / 2
        r = max(reach(segs, Vector((math.sin(a + o), math.cos(a + o)))) for o in (-half, 0, half))
        out.append(r / math.cos(half) + ease * H)
    return out


def skirt_clear(top, hem, faces, z_top, z_hem, ease, steps=10):
    """Widen a straight-sided skirt (rings top and hem, the same count, counter-clockwise from
    above) panel by panel until every point where the surface `faces` crosses each height
    between sits at least `ease` inside it."""
    hem = [v.copy() for v in hem]
    for _ in range(200):
        moved = False
        for n in range(1, steps + 1):
            t = n / steps
            z = z_top - (z_top - z_hem) * t
            ring_ = [a.xy + (h.xy - a.xy) * t for a, h in zip(top, hem)]
            pts = [q for seg in slice_at(faces, z * H) for q in seg]
            for i in range(len(ring_)):
                a, b = ring_[i], ring_[(i + 1) % len(ring_)]
                out = (b - a).orthogonal().normalized()
                if out.dot(a) < 0:
                    out = -out
                if any(out.dot(q - a) > -ease and 0 <= (q - a).dot(b - a) <= (b - a).length_squared for q in pts):
                    for j in (i, (i + 1) % len(ring_)):
                        hem[j].x *= 1.02
                        hem[j].y *= 1.02
                    moved = True
        if not moved:
            break
    return hem


def oval(need, power=2.4):
    """The roundest outline that keeps every direction at least as far out as `need`: a
    superellipse (squarer as power grows) with its own front, back and side, grown until nothing
    pokes through."""
    a = max(r * abs(d.x) for r, d in zip(need, DIRS)) or 1e-4
    f = max(r * d.y for r, d in zip(need, DIRS)) or 1e-4
    b = max(-r * d.y for r, d in zip(need, DIRS)) or 1e-4

    def at(d):
        fy = f if d.y >= 0 else b
        return (abs(d.x / a) ** power + abs(d.y / fy) ** power) ** (-1 / power)
    shape = [at(d) for d in DIRS]
    grow = max(r / s for r, s in zip(need, shape))
    return [s * grow for s in shape]


def fit(sections, z, ease, power=2.4):
    """A rounded ring standing `ease` off every section (a list of slicable surfaces) at z."""
    need = [0.0] * AROUND
    for faces in sections:
        need = [max(n, r) for n, r in zip(need, measure(faces, z))]
    return oval([n + ease for n in need], power)


def ring(bm, z, radii):
    return [bm.verts.new((r * d.x * H, r * d.y * H, z * H)) for r, d in zip(radii, DIRS)]


def loft(bm, rings, mats):
    for (r0, r1), mat in zip(zip(rings, rings[1:]), mats):
        B["quads"](bm, r0, r1, mat=mat)


def grow(points, d, axes=(0, 1)):
    """A closed ring of points pushed out by d (metres) in the plane of two axes, each point
    along the bisector of its two sides, so flat sides stay flat and corners stay corners."""
    i, j = axes
    q = [Vector((p[i], p[j])) for p in points]
    area = sum(a.x * b.y - b.x * a.y for a, b in zip(q, q[1:] + q[:1]))
    out = []
    for k, p in enumerate(points):
        n1 = (q[k] - q[k - 1]).orthogonal().normalized()
        n2 = (q[(k + 1) % len(q)] - q[k]).orthogonal().normalized()
        if area > 0:
            n1, n2 = -n1, -n2
        m = (n1 + n2).normalized()
        step = m * (d / max(m.dot(n1), 0.5))
        p = p.copy()
        p[i] += step.x
        p[j] += step.y
        out.append(p)
    return out


# ---------------------------------------------------------------------------------------------
# Weights.

def weigh_torso_ring(p, verts, z):
    if z < B["SHIRT"][1][0] - 0.005:          # below the waist: with the hips, like the trousers
        p.weigh(verts, hips=1.0)
        return
    t = B["smoothstep"](0.66, 0.74, z)
    p.weigh(verts, spine=1 - t, chest=t)


def weigh_skirt_ring(p, verts, legs):
    """Skirt cloth follows each thigh by how far to that side it hangs, `legs` at the most."""
    a = max(abs(v.co.x) for v in verts) or 1.0
    for v in verts:
        w = legs * B["smoothstep"](0.0, 0.8 * a, abs(v.co.x))
        s_ = B["lr"](-1 if v.co.x < 0 else 1)
        p.weights[v] = {"hips": 1 - w, f"thigh.{s_}": w} if w > 0 else {"hips": 1.0}


def weigh_sleeve_ring(p, verts, s, side):
    B["weigh_arm_ring"](p, verts, s, side)


# ---------------------------------------------------------------------------------------------
# Pieces garments share.

def top_rings(p, torso_verts, ease, ridge=0.0):
    """The tuned shirt from the waist up, each ring pushed out by ease (metres), open at the
    waist; the armholes cut. With a ridge, each ring gets a point in the middle of its front,
    pushed that much further forward (a breastplate's keel). Returns the rings and the holes."""
    shirt = B["SHIRT"]
    bm = p.bm
    rings = []
    for i in range(1, len(shirt)):
        r = grow(torso_verts[8 * i:8 * i + 8], 0.002 * H if i == len(shirt) - 1 else ease)
        if ridge:
            mid = (r[0] + r[1]) / 2 + Vector((0, ridge * (0.4 if i == len(shirt) - 1 else 1.0), 0))
            r = [r[0], mid] + r[1:]
        rings.append([bm.verts.new(v) for v in r])
    loft(bm, rings, [PRIMARY] * (len(rings) - 1))
    for (z, *_), r in zip(shirt[1:], rings):
        weigh_torso_ring(p, r, z)
    at = (lambda i: i if i == 0 else i + 1) if ridge else (lambda i: i)
    holes = {}
    lo, hi = rings[B["ARMPIT"] - 1], rings[B["SHOULDER_RING"] - 1]
    for side in (-1, 1):
        front, back = (at(2), at(3)) if side < 0 else (at(7), at(6))
        holes[side] = [lo[front], lo[back], hi[back], hi[front]]
        hole = set(holes[side])
        for f in list(bm.faces):
            if set(f.verts) == hole:
                bm.faces.remove(f)
    return rings, holes


def follow(ring_, legs, z_from, z_to, ease):
    """ring_ (positions at z_from) carried down to z_to and widened, side by side, until the band
    between them clears the legs at every height (ease in shares)."""
    lower = [Vector((v.x, v.y, z_to * H)) for v in ring_]
    lower = skirt_clear(ring_, lower, legs, z_from, z_to, ease * H)
    for v in lower:
        v.z = z_to * H
    return lower


def bare_sleeves(p, holes, shirt_verts, arm_verts, hands_verts, out=0.0, mat=0):
    """A long sleeve that is the arm itself (pushed out by `out` metres), from the armhole to the
    fist's own wrist ring: the shirt's box shoulder, then the six-sided bare arm from the elbow.
    Returns each side's bare arm rings."""
    bm = p.bm
    arm, limb_def = B["ARM"], B["LIMB"]
    limbs = {}
    for side in (-1, 1):
        s_ = B["lr"](side)
        shoulder = B["shirt_sleeve"](shirt_verts, side)[0]
        _, _, limb = B["arm_layout"](arm_verts, side)
        limbs[side] = limb
        sleeve = []
        for r, s in ((shoulder, arm[0][0]), (limb[2], limb_def[2][0]), (limb[3], limb_def[3][0]),
                     (B["wrist_ring"](hands_verts, side), limb_def[-1][0])):
            if out and s < limb_def[-1][0]:
                r = grow(r, out, (1, 2))
            r = [bm.verts.new(v) for v in r]
            sleeve.append(r)
            weigh_sleeve_ring(p, r, s, side)
        loft(bm, [holes[side], sleeve[0]], [mat])
        B["bridge"](bm, sleeve[0], sleeve[1], mat)
        loft(bm, sleeve[1:], [mat] * 2)
        p.weigh(holes[side], chest=1.0)
        p.weigh(sleeve[-1], **{f"forearm.{s_}": 0.5, f"hand.{s_}": 0.5})
    return limbs


def flared_cuff(p, side, box, k, mat):
    """A cuff as an open sheet: from just off the forearm, over the wrist, out and past it."""
    bm = p.bm
    arm = B["LIMB"]
    s_ = B["lr"](side)
    t = 1 - k["cuff"] / (arm[-1][0] - arm[-2][0])
    start = [a + (b - a) * t for a, b in zip(box[-2], box[-1])]
    end = [v + Vector((side * k["cuff_past"] * H, 0, 0)) for v in box[-1]]
    cuff = [[bm.verts.new(v) for v in grow(start, 0.0015 * H, (1, 2))],
            [bm.verts.new(v) for v in grow(end, k["bell"] * H, (1, 2))]]
    loft(bm, cuff, [mat])
    p.weigh(cuff[0], **{f"forearm.{s_}": 1.0})
    p.weigh(cuff[1], **{f"forearm.{s_}": 0.7, f"hand.{s_}": 0.3})


# ---------------------------------------------------------------------------------------------
# Garments.

PRIMARY, TRIM, LEATHER = 0, 1, 2

TUNIC = dict(
    ease=0.003,        # cloth off the body
    belt=0.022,        # the belt's height, down from just above the waist
    belt_out=0.004,    # how far the belt band stands off
    length=0.45,       # the hem: this share of the way from the crotch to the knee
    flare=(0.007, 0.004, 0.011),   # how far the hem stands out beyond the hips: side, front, back
    trim=0.016,        # the hem's trim band
    bell=0.006,        # how far the cuff flares out at its end
    cuff=0.022,        # the cuff's length, up the forearm from the wrist
    cuff_past=0.004,   # how far it hangs past the wrist
)


def tunic(p, k=TUNIC):
    """A belted tunic in the classic kit's way: the body's own box shirt and sleeves, eased out,
    a loose belt band, and the skirt a single open sheet tucked under the belt and flaring
    to mid-thigh (wider behind, so it reads in a stride). Replaces the torso and arms."""
    bm = p.bm
    shirt = B["SHIRT"]
    torso_verts, _ = tuned("wear_shirt")         # the shirt's rings come first
    arm_verts, _ = tuned("arms")
    hands_verts, _ = tuned("hands")
    _, legs = tuned("wear_trousers")
    ease = k["ease"] * H

    # The top: the tuned shirt from the waist up, square shoulders and all, open at the waist.
    rings, holes = top_rings(p, torso_verts, ease)

    # Below the waist the same surface carries on: the waist ring's own eight sides go down to a
    # cut under the belt, out over the hips, then flare to the hem, so every edge lines up from
    # the collar to the hem. Each ring is the one above it, widened only as far as the body
    # under it needs. The cut is where the parts move apart: above it with the spine, below it
    # with the hips and thighs. Both copies of the cut ring sit in the same place with the same
    # weights, so the seam never opens, and the belt covers it.
    z_waist = shirt[1][0]
    z_cut = z_waist - k["belt"]
    z_hips = min(r[0] for r in B["TROUSERS"])
    z_crotch, z_knee = B["CROTCH"][0], B["KNEE"]
    z_hem = z_crotch - k["length"] * (z_crotch - z_knee)
    waist = [v.co.copy() for v in rings[0]]

    cut = follow(waist, legs, z_waist, z_cut, k["ease"])
    hips = follow(cut, legs, z_cut, z_hips, k["ease"] * 2)
    side, fore, aft = k["flare"]
    hem = []
    for v in hips:
        d = v.xy.normalized()
        out = (side * d.x ** 2 + (fore if d.y > 0 else aft) * d.y ** 2) * H
        hem.append(Vector((v.x + d.x * out, v.y + d.y * out, z_hem * H)))
    # The skirt is straight from the hips to the hem: slice it at each height between and widen
    # the panel that any point of the trousers comes too close to.
    hem = skirt_clear(hips, hem, legs, z_hips, z_hem, k["ease"] * 2 * H)
    t = k["trim"] / (z_hips - z_hem)
    trim = [h + (v - h) * t for v, h in zip(hips, hem)]

    above = [bm.verts.new(v) for v in cut]
    B["quads"](bm, above, rings[0], mat=PRIMARY)
    skirt = [[bm.verts.new(v) for v in r] for r in (hem, trim, hips, cut)]
    loft(bm, skirt, [TRIM, PRIMARY, PRIMARY])
    p.weigh(above, hips=1.0)
    p.weigh(skirt[3], hips=1.0)
    weigh_skirt_ring(p, skirt[2], 0.15)
    weigh_skirt_ring(p, skirt[1], 0.45)
    weigh_skirt_ring(p, skirt[0], 0.50)

    # The belt: a band over the cut, tapering with the cloth from the waist to the cut.
    belt = [[bm.verts.new((v.x, v.y, z * H)) for v in grow(r, k["belt_out"] * H)]
            for r, z in ((cut, z_cut - 0.003), (waist, z_waist + 0.004))]
    loft(bm, belt, [LEATHER])
    p.weigh(belt[0], hips=1.0)
    weigh_torso_ring(p, belt[1], z_waist)

    # The sleeves: the arm itself, recoloured, with nothing added; the cuff loose, like the skirt.
    boxes = bare_sleeves(p, holes, torso_verts, arm_verts, hands_verts)
    for side in (-1, 1):
        flared_cuff(p, side, boxes[side], k, TRIM)


# Every garment: what builds it, its channels (by material index), and the kit parts it hides.
GARMENTS = {
    "tunic": (tunic, ["primary", "trim", "leather"], ["torso", "arms", "wear_shirt"]),
}


def build_garment(name):
    fn, channels, hides = GARMENTS[name]
    p = B["Part"]("wear_" + name)
    fn(p)
    obj = p.finish(channels)
    obj["hides"] = ",".join(hides)
    return obj


def preview(name, rig, collection):
    """Wear a garment on a body in the open scene: parented to its rig, the parts it covers
    hidden, and any earlier preview of it on that rig replaced."""
    for child in list(rig.children):
        if child.name.split(".")[0] == "wear_" + name:
            me = child.data
            bpy.data.objects.remove(child)
            if me.users == 0:
                bpy.data.meshes.remove(me)
    obj = build_garment(name)
    collection.objects.link(obj)
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    hidden = []
    for child in rig.children:
        if child.name.split(".")[0] in [h if h.startswith("wear_") else "body_" + h for h in obj["hides"].split(",")]:
            child.hide_set(True)
            hidden.append(child.name)
    return obj, hidden


if "PREVIEW_RIG" in globals():
    coll = bpy.data.collections.get("Clothes_Preview") or bpy.data.collections.new("Clothes_Preview")
    if coll.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(coll)
    rig = bpy.data.objects[PREVIEW_RIG]  # noqa: F821  (given by the caller)
    obj, hidden = preview(globals().get("GARMENT", "tunic"), rig, coll)
    result = {"object": obj.name, "hidden": hidden, "tris": sum(len(f.vertices) - 2 for f in obj.data.polygons)}
