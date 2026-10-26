"""The smithed platebody, for the male and the female body.

A platebody replaces the torso and the arms (it hides those kit parts), so it is built from
them: their own surface, welded and pushed out a few millimetres, so the silhouette stays the
body's. What makes it a platebody is cut into that surface, not stacked on it:

  - a breastplate with a raised ridge down the middle, its lower edge a chevron dipping to a
    point at the front and the back;
  - one fauld lame below it, then the hem;
  - a shoulder cap over each shoulder, a seam where it meets the chest and one across the arm,
    its middle domed a little;
  - a seam at the elbow and a cuff at the wrist;
  - a low standing collar round the neck.

Every measure comes from the body's own bones and outline, so the same code fits both sexes.

Materials: "Copper" is the plate and "CopperShade" its darker tone, the two things a bar
recolours ("recolor": {"Copper": "Iron", "CopperShade": "IronShade"} on the item, as the held
weapons do; every metal has a "<Metal>Shade" in the palette); "Blackiron" is the dark of slits
and seams, the same on any metal.

Rig space: the body faces +Y, its left is -X, Z up, the T-pose's arms along X.

Run from the MCP:
    import runpy; ns = runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\armour\\platebody.py")
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

BODIES = {"male": "HumanMale", "female": "HumanFemale"}
CHARACTERS = os.path.join(A.REPO, "client", "Assets", "Resources", "Characters")
MATS = ["Copper", "Blackiron", "CopperShade"]
METAL, SEAM, SHADE = range(3)
HIDES = ["torso", "arms"]

LIFT = 0.004                   # how far the plate stands off the body
SEAM_W = 0.014                 # a seam's width
RIDGE = 0.008                  # the breastplate's ridge, forward of the plate
STEP = 0.004                   # the breastplate, proud of the fauld below it
CAP_DOME = 0.010               # the shoulder cap, proud of the chest and the arm
COLLAR_H = 0.022               # the standing collar's height
COLLAR_IN = 0.006              # and how far it leans in at the top
BREAST = 0.30                  # the chevron's point, as a share of hem-to-shoulder above the hem
BREAST_RISE = 0.38             # and how steeply it rises towards the sides
FAULD = 0.15                   # the fauld's seam, likewise


# ---- the body ----

def load_body(sex, x=0.0):
    """The body of one sex, imported once into its own collection; returns its rig."""
    name = BODIES[sex]
    rig = bpy.data.objects.get(name + "_Rig")
    if rig is None:
        win = bpy.context.window_manager.windows[0]
        area = next(a for a in win.screen.areas if a.type == "VIEW_3D")
        before = set(bpy.data.objects)
        with bpy.context.temp_override(window=win, area=area, scene=win.scene, view_layer=win.view_layer):
            bpy.ops.import_scene.fbx(filepath=os.path.join(CHARACTERS, name + ".fbx"))
        c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
        if c.name not in bpy.context.scene.collection.children:
            bpy.context.scene.collection.children.link(c)
        for o in set(bpy.data.objects) - before:
            for uc in list(o.users_collection):
                uc.objects.unlink(o)
            c.objects.link(o)
            if o.type == "ARMATURE":
                o.name = name + "_Rig"
                rig = o
            else:
                o.name = name + "_" + o.name.split(".")[0]
    rig.location = (x, 0, 0)
    for o in rig.children:
        for m in o.data.materials:
            colour = A.PLAYER_PREVIEW.get(m.name.split(".")[0])
            if colour is not None:
                m.diffuse_color = A.rgba(colour)
                if m.node_tree and m.node_tree.nodes.get("Principled BSDF"):
                    bsdf = m.node_tree.nodes["Principled BSDF"]
                    bsdf.inputs["Base Color"].default_value = A.rgba(colour)
                    bsdf.inputs["Roughness"].default_value = 1.0
    return rig


def part(rig, name):
    return next((o for o in rig.children if o.name.endswith("_body_" + name)), None)


def to_rig(rig, o):
    return rig.matrix_world.inverted() @ o.matrix_world


def bone(rig, name):
    return Vector(rig.data.bones[name].head_local)


class Body:
    """Where things are on one body, in rig space."""

    def __init__(self, rig):
        self.rig = rig
        torso = part(rig, "torso")
        m = to_rig(rig, torso)
        vs = [m @ v.co for v in torso.data.vertices]
        self.hem = min(v.z for v in vs)
        self.top = max(v.z for v in vs)
        self.mid_y = (min(v.y for v in vs) + max(v.y for v in vs)) / 2
        self.shoulder = bone(rig, "upper_arm.R")       # the joint; the left mirrors it
        self.elbow = bone(rig, "forearm.R")
        self.wrist = bone(rig, "hand.R")
        self.neck = bone(rig, "neck")
        self.width = max(abs(v.x) for v in vs if v.z < self.shoulder.z - 0.08)


# ---- building ----

def surface(rig, lift=LIFT, parts=HIDES):
    """Kit parts (the torso and arms) as one welded skin in rig space, pushed out by `lift`."""
    bm = bmesh.new()
    for name in parts:
        o = part(rig, name)
        tmp = o.data.copy()
        tmp.transform(to_rig(rig, o))
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * lift
    for f in bm.faces:
        f.material_index = METAL
    return bm


def cut(bm, co, no, keep=None):
    """Cut the faces keep(centre) chooses along a plane; returns the new edges."""
    faces = [f for f in bm.faces if keep is None or keep(f.calc_center_median())]
    geom = list({v for f in faces for v in f.verts}) + list({e for f in faces for e in f.edges}) + faces
    out = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=Vector(co), plane_no=Vector(no))
    return [g for g in out["geom_cut"] if isinstance(g, bmesh.types.BMEdge)]


def seam(bm, co, no, keep=None, width=SEAM_W):
    """A dark seam along a plane: two cuts width apart, the band between them painted."""
    no = Vector(no).normalized()
    co = Vector(co)
    for k in (-0.5, 0.5):
        cut(bm, co + no * width * k, no, keep)
    for f in bm.faces:
        c = f.calc_center_median()
        if abs((c - co).dot(no)) < width / 2 and (keep is None or keep(c)):
            f.material_index = SEAM


def side_of(c):
    return 1 if c.x >= 0 else -1


def coat(rig):
    """The plated coat: cut every seam first, then shape the plates between them: the breastplate and the
    shoulder caps stand a little proud of what they overlap, so each seam reads as the
    shadowed underside of a plate's edge."""
    b = Body(rig)
    bm = surface(rig)
    span = b.shoulder.z - b.hem
    sx, ex, wx = b.shoulder.x, b.elbow.x, b.wrist.x
    half = SEAM_W / 2 + 1e-4
    on_trunk = lambda c: abs(c.x) < sx and c.z < b.shoulder.z - 0.1

    # The breastplate's lower edge: a chevron each side of the middle, front and back alike.
    point = Vector((0, 0, b.hem + span * BREAST))
    a = math.atan(BREAST_RISE)
    chevron = {s: Vector((-s * math.sin(a), 0, math.cos(a))) for s in (1, -1)}
    for s in (1, -1):
        seam(bm, point, chevron[s], lambda c, s=s: side_of(c) == s and on_trunk(c))

    # The fauld: one seam round the trunk below it.
    seam(bm, (0, 0, b.hem + span * FAULD), (0, 0, 1), on_trunk)

    # The shoulder caps: a seam slanting from beside the neck down to the armpit, front and
    # back, and one across the arm.
    cap_out = sx + (ex - sx) * 0.32
    caps = {}
    for s in (1, -1):
        neck_side = Vector((s * (b.width * 0.75), 0, b.top))
        armpit = Vector((s * (sx + 0.02), 0, b.shoulder.z - 0.08))
        d = armpit - neck_side
        no = Vector((d.z, 0, -d.x)).normalized()
        if no.dot(Vector((s, 0, 1))) < 0:
            no = -no
        mid = neck_side.lerp(armpit, 0.5)
        caps[s] = (mid, no)
        seam(bm, mid, no, lambda c, s=s: side_of(c) == s and abs(c.x) < cap_out + 0.02 and c.z > b.shoulder.z - 0.14)
        seam(bm, (s * cap_out, 0, 0), (1, 0, 0), lambda c, s=s: side_of(c) == s and abs(c.x) > sx)

    # The arms: a seam at the elbow, a cuff at the wrist.
    for s in (1, -1):
        arm = lambda c, s=s: side_of(c) == s and abs(c.x) > sx + 0.05
        seam(bm, (s * ex, 0, 0), (1, 0, 0), arm)
        seam(bm, (s * (wx - 0.035), 0, 0), (1, 0, 0), arm, width=SEAM_W * 1.6)

    # The ridge needs a line of vertices down the front's middle.
    cut(bm, (0, 0, 0), (1, 0, 0), keep=lambda c: abs(c.x) < 0.08 and c.y > b.mid_y)

    # Shape. Each vertex is judged where it was cut, then moved.
    bm.normal_update()
    moves = {}
    for v in bm.verts:
        p, s = v.co.copy(), side_of(v.co)
        move = Vector()
        breast = (p - point).dot(chevron[s]) > -half and abs(p.x) < cap_out - SEAM_W / 2 + 1e-4
        if breast:
            move += v.normal * STEP
        mid, no = caps[s]
        if (p - mid).dot(no) > -half and abs(p.x) < cap_out - SEAM_W / 2 + 1e-4:
            centre = Vector((s * (sx + cap_out) / 2, b.shoulder.y, b.shoulder.z))
            move += (p - centre).normalized() * CAP_DOME
        if abs(p.x) < 1e-4 and p.y > b.mid_y and (p - point).z > SEAM_W and p.z < b.top - 0.03:
            move.y += RIDGE
        moves[v] = move
    for v, move in moves.items():
        v.co += move

    collar(bm, b)
    return bm, b


def collar(bm, b):
    """Stand the neck opening up into a low collar, leaning in a little."""
    bm.edges.ensure_lookup_table()
    rim = [e for e in bm.edges if e.is_boundary and all(abs(v.co.x) < b.shoulder.x and v.co.z > b.shoulder.z
                                                         for v in e.verts)]
    if not rim:
        return
    verts = list({v for e in rim for v in e.verts})
    centre = sum((v.co for v in verts), Vector()) / len(verts)
    top_z = max(v.co.z for v in verts)
    out = bmesh.ops.extrude_edge_only(bm, edges=rim)
    new = [g for g in out["geom"] if isinstance(g, bmesh.types.BMVert)]
    for v in new:
        inward = Vector((centre.x - v.co.x, centre.y - v.co.y, 0))
        inward = inward.normalized() * COLLAR_IN if inward.length > 1e-6 else Vector()
        # A level top: the opening's low front comes up to the back's height.
        v.co = Vector((v.co.x, v.co.y, max(v.co.z, top_z - 0.02) + COLLAR_H)) + inward
    for f in {f for v in new for f in v.link_faces}:
        f.material_index = METAL


# ---- the platebody: separate plates ----
#
# The coat above is cloth with seams painted on. A platebody is made of plates: the same surface
# cut into pieces (breastplate, faulds, pauldron cap and lames, rerebrace, couter, vambrace, cuff),
# each offset to its own depth so the one above overlaps the one below, every edge turned in to
# the body as a solid rim (the step and its shadow are what read as plate), the chest made rigid
# (no waist, no cleavage: one barrel), a keel down the breastplate and a gorget at the neck.
# Only the pauldrons and the couters stand well off the body; the trunk stays within a centimetre.

STYLES = {
    # How far each plate stands off the body (a pair: at its top and at its bottom, so a lame flares
    # out over the one below and tucks in under the one above); "dome" swells the pauldron's cap, "keel" brings the
    # breastplate's ridge forward, "cap" is the cap's reach down the upper arm (a share of it).
    "restrained": {"breast": .010, "fauld1": (.005, .011), "fauld2": (.006, .012), "cap": .020, "lame1": .015,
                   "lame2": .011, "rere": .008, "couter": .014, "vam": .008, "cuff": .012,
                   "dome": .010, "keel": .015, "reach": .30},
    "bold": {"breast": .012, "fauld1": (.006, .014), "fauld2": (.007, .015), "cap": .034, "lame1": .025,
             "lame2": .017, "rere": .009, "couter": .020, "vam": .009, "cuff": .016,
             "dome": .018, "keel": .020, "reach": .36},
}
RIM = 0.003                    # how close to the body a plate's edge turns in
LAME = 0.04                    # a pauldron lame's width down the arm
GORGET_H = 0.03                # the gorget's height
COUTER = 0.035                 # half the couter's width along the arm


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def rigidize(bm, b):
    """Make the trunk one rigid barrel: every vertex below the shoulders moves out to the convex
    outline of its own height (the waist, the small of the back and between the breasts fill in)."""
    from mathutils.geometry import convex_hull_2d, intersect_line_line_2d
    trunk = [v for v in bm.verts if abs(v.co.x) < b.shoulder.x - 0.02 and v.co.z < b.shoulder.z - 0.04]
    moved = {}
    for v in trunk:
        band = [(u.co.x, u.co.y - b.mid_y) for u in trunk if abs(u.co.z - v.co.z) < 0.035]
        if len(band) < 3:
            continue
        hull = [Vector(band[i]) for i in convex_hull_2d(band)]
        d = Vector((v.co.x, v.co.y - b.mid_y))
        if d.length < 1e-6:
            continue
        far = d.normalized() * 2.0
        best = d.length
        for p, q in zip(hull, hull[1:] + hull[:1]):
            hit = intersect_line_line_2d(Vector((0, 0)), far, p, q)
            if hit is not None:
                best = max(best, hit.length)
        moved[v] = d.normalized() * best
    for v, d in moved.items():
        v.co.x, v.co.y = d.x, b.mid_y + d.y


def plates(rig, st):
    b = Body(rig)
    bm = surface(rig, lift=0.0)
    rigidize(bm, b)
    span = b.shoulder.z - b.hem
    sx, ex, wx = b.shoulder.x, b.elbow.x, b.wrist.x
    on_trunk = lambda c: abs(c.x) < sx and c.z < b.shoulder.z - 0.1

    # Where the plates part.
    point = Vector((0, 0, b.hem + span * BREAST))
    a = math.atan(BREAST_RISE)
    chevron = {s: Vector((-s * math.sin(a), 0, math.cos(a))) for s in (1, -1)}
    fauld_z = b.hem + span * FAULD
    cap_out = sx + (ex - sx) * st["reach"]
    arm_cuts = [cap_out, cap_out + LAME, cap_out + 2 * LAME, ex - COUTER, ex + COUTER, wx - 0.05]
    caps = {}
    for s in (1, -1):
        cut(bm, point, chevron[s], lambda c, s=s: side_of(c) == s and on_trunk(c))
        neck_side = Vector((s * (b.width * 0.75), 0, b.top))
        armpit = Vector((s * (sx + 0.02), 0, b.shoulder.z - 0.08))
        d = armpit - neck_side
        no = Vector((d.z, 0, -d.x)).normalized()
        if no.dot(Vector((s, 0, 1))) < 0:
            no = -no
        caps[s] = (neck_side.lerp(armpit, 0.5), no)
        cut(bm, caps[s][0], no, lambda c, s=s: side_of(c) == s and abs(c.x) < cap_out + 0.02
            and c.z > b.shoulder.z - 0.14)
        for x in arm_cuts:
            cut(bm, (s * x, 0, 0), (1, 0, 0), lambda c, s=s: side_of(c) == s and abs(c.x) > sx)
    cut(bm, (0, 0, fauld_z), (0, 0, 1), on_trunk)
    cut(bm, (0, 0, 0), (1, 0, 0), keep=lambda c: abs(c.x) < 0.08 and c.y > b.mid_y)

    def piece(c):
        s, ax = side_of(c), abs(c.x)
        mid, no = caps[s]
        past = (c - mid).dot(no) > 0
        if ax > sx - 0.01 or past:
            if past and ax < cap_out:
                return "cap"
            if ax < arm_cuts[0]:
                return "rere"
            names = ["lame1", "lame2", "rere", "couter", "vam", "cuff"]
            for name, x0, x1 in zip(names, arm_cuts, arm_cuts[1:] + [9.0]):
                if x0 <= ax < x1:
                    return name
        if (c - point).dot(chevron[s]) > 0:
            return "breast"
        return "fauld1" if c.z > fauld_z else "fauld2"

    of = {f: piece(f.calc_center_median()) for f in bm.faces}
    neck = {_key(v.co) for e in bm.edges if e.is_boundary for v in e.verts
            if abs(v.co.x) < b.shoulder.x and v.co.z > b.shoulder.z}
    normal = split_plates(bm, of)
    at_neck = {v for v in bm.verts if _key(v.co) in neck}
    fauld_top = lambda x: point.z + abs(x) * math.tan(a)
    spans = {"fauld1": lambda v: (fauld_top(v.co.x), fauld_z), "fauld2": lambda v: (fauld_z, b.hem)}

    # Each plate to its depth, with the cap's swell and the breastplate's keel.
    lift = {}
    keel_lo, keel_hi = point.z + 0.02, b.top - 0.03
    for v in bm.verts:
        name = of[v.link_faces[0]]
        n = normal[_key(v.co)]
        depth = plate_depth(st[name], v.co.z, *spans[name](v)) if name in spans else st[name]
        move = Vector()
        if name == "cap":
            # It rises out of the breastplate beside the neck and stands off over the arm.
            s = side_of(v.co)
            mid, no = caps[s]
            rise = smoothstep(0.0, 0.06, (v.co - mid).dot(no))
            depth = st["breast"] + (st["cap"] - st["breast"]) * rise
            centre = Vector((s * (sx + cap_out) / 2, b.shoulder.y, b.shoulder.z - 0.02))
            move += (v.co - centre).normalized() * st["dome"] * rise
        move += n * depth
        if name == "breast" and abs(v.co.x) < 1e-4 and v.co.y > b.mid_y and keel_lo < v.co.z < keel_hi:
            move.y += st["keel"] * math.sin(math.pi * (v.co.z - keel_lo) / (keel_hi - keel_lo))
        lift[v] = (n, max(depth - RIM, 0.004))
        v.co += move

    gorget(bm, [e for e in bm.edges if e.is_boundary and all(v in at_neck for v in e.verts)], lift)
    rim_plates(bm, lift)
    return bm, b


def _key(co):
    return (round(co.x, 5), round(co.y, 5), round(co.z, 5))


def plate_depth(d, z, top, bottom):
    """A plate's depth at height z: a number, or (at its top, at its bottom) run straight between."""
    if not isinstance(d, tuple):
        return d
    t = max(0.0, min(1.0, (top - z) / (top - bottom))) if top != bottom else 0.0
    return d[0] + (d[1] - d[0]) * t


def split_plates(bm, of):
    """Part the surface into its plates (`of`: face -> plate name) along the edges between them.
    Returns each point's normal on the whole surface, by position, so plates move apart cleanly."""
    bm.normal_update()
    normal = {_key(v.co): v.normal.copy() for v in bm.verts}
    parting = [e for e in bm.edges if len(e.link_faces) == 2 and of[e.link_faces[0]] != of[e.link_faces[1]]]
    bmesh.ops.split_edges(bm, edges=parting)
    return normal


def rim_plates(bm, lift):
    """Every open edge turns in towards the body (lift: vertex -> (normal, depth)): a rim as deep
    as the plate stands off, so each plate shows its thickness and the step to the next."""
    rims = [lp for f in list(bm.faces) for lp in f.loops if lp.edge.is_boundary]
    inner = {}
    for lp in rims:
        for v in (lp.vert, lp.link_loop_next.vert):
            if v not in inner:
                n, depth = lift.get(v, (Vector((0, 0, 0)), 0.0))
                inner[v] = bm.verts.new(v.co - n * depth)
    for lp in rims:
        a, b = lp.vert, lp.link_loop_next.vert
        if (inner[a].co - a.co).length < 1e-5:
            continue
        bm.faces.new((b, a, inner[a], inner[b]))
    for f in bm.faces:
        f.material_index = METAL


# ---- the platelegs ----
#
# The same plates on the legs kit part: two tassets flaring round the hips and the tops of the
# thighs (tucked in under the platebody's hem), the cuisses on the thighs, poleyns over the knees
# with a point forward, greaves down the shins, and a cuff flared over the ankle.

LEG_STYLES = {
    "restrained": {"crotch": .004, "tasset1": (.004, .012), "tasset2": (.006, .014), "cuisse": .007,
                   "poleyn": .013, "greave": .008, "cuff": (.009, .012), "knee": .010},
    "bold": {"crotch": .004, "tasset1": (.004, .018), "tasset2": (.007, .020), "cuisse": .008,
             "poleyn": .018, "greave": .009, "cuff": (.010, .016), "knee": .018},
}
LEG_HIDES = ["legs"]
CROTCH = 0.045                 # the inner thighs' gap, either side of the middle


def platelegs(rig, st):
    bm = surface(rig, lift=0.0, parts=LEG_HIDES)
    hip, knee, ankle = bone(rig, "thigh.R"), bone(rig, "shin.R"), bone(rig, "foot.R")
    top = max(v.co.z for v in bm.verts)
    bands = [("tasset1", top, hip.z - 0.05), ("tasset2", hip.z - 0.05, hip.z - 0.13),
             ("cuisse", hip.z - 0.13, knee.z + 0.05), ("poleyn", knee.z + 0.05, knee.z - 0.045),
             ("greave", knee.z - 0.045, ankle.z + 0.10), ("cuff", ankle.z + 0.10, -1.0)]
    crotch_z = hip.z - 0.12
    for _, _, z in bands[:-1]:
        cut(bm, (0, 0, z), (0, 0, 1))
    for sd in (1, -1):
        cut(bm, (sd * CROTCH, 0, 0), (1, 0, 0), lambda c: c.z > crotch_z - 0.01)

    def piece(c):
        if abs(c.x) < CROTCH and c.z > crotch_z:
            return "crotch"
        return next(name for name, z0, z1 in bands if z1 <= c.z < z0 or name == "cuff")

    of = {f: piece(f.calc_center_median()) for f in bm.faces}
    normal = split_plates(bm, of)
    span = {name: (z0, z1) for name, z0, z1 in bands}
    lift = {}
    for v in bm.verts:
        name = of[v.link_faces[0]]
        n = normal[_key(v.co)]
        depth = plate_depth(st[name], v.co.z, *span.get(name, (0, 0)))
        move = n * depth
        if name == "poleyn":
            # The knee cop's point, forward over the kneecap.
            kx = knee.x if v.co.x >= 0 else -knee.x
            fwd = max(0.0, n.y) * (1 - min(1.0, abs(v.co.z - knee.z) / 0.05))
            move += Vector((0, 1, 0)) * st["knee"] * fwd * (1 - min(1.0, abs(abs(v.co.x) - abs(kx)) / 0.08))
        lift[v] = (n, max(depth - RIM, 0.004))
        v.co += move
    rim_plates(bm, lift)
    return bm


def gorget(bm, rim, lift):
    """Stand the neck opening (its edges, `rim`) up into a gorget: straight up, leaning in a
    little, level on top."""
    if not rim:
        return
    verts = list({v for e in rim for v in e.verts})
    centre = sum((v.co for v in verts), Vector()) / len(verts)
    top_z = max(v.co.z for v in verts)
    out = bmesh.ops.extrude_edge_only(bm, edges=rim)
    for v in [g for g in out["geom"] if isinstance(g, bmesh.types.BMVert)]:
        outward = Vector((v.co.x - centre.x, v.co.y - centre.y, 0))
        outward = outward.normalized() if outward.length > 1e-6 else Vector((0, 1, 0))
        v.co = Vector((v.co.x, v.co.y, max(v.co.z, top_z - 0.02) + GORGET_H)) - outward * COLLAR_IN
        lift[v] = (outward, 0.007)


# ---- the helms ----
#
# A shell measured off the body's own head (hair left out: it goes under the helm), rigid on the
# head bone. Rings round the head from below the chin to the crown: at each, the head's outline is
# measured with rays, made convex, stood off by the helm's clearance, and hung straight down from
# the skull's widest point so the helm is a bucket, not a mask of the face. Then plates as on the
# body: the skull, the visor (a little proud) and the neck guard, a dark slit set in for the eyes,
# a comb over the top and a ridge down the visor, every edge with a rim.
#   full: closed, with a visor slit; it narrows to the jaw like a face, with a prow down the visor,
#         a band round the brow, the visor's pivots at the temples and breaths on the lower
#         cheeks (hides the head and the hair)
#   open: a skull cap with cheek guards and a nasal, the face showing (hides the hair)

HELM_STYLES = {
    # "chin": how much of the widest skull the lower face keeps (lower follows the face more).
    "full": {"clear": 0.018, "comb": 0.022, "prow": 0.012, "visor": 0.006, "neck": 0.004,
             "brow": 0.006, "chin": 0.78, "hides": ["head", "hair_short", "hair_bob"]},
    "open": {"clear": 0.016, "comb": 0.014, "prow": 0.0, "visor": 0.0, "neck": 0.004,
             "brow": 0.006, "chin": 0.93, "hides": ["hair_short", "hair_bob"]},
    # The iconic set's helm: the shell follows the neck in below the chin ("neck_in").
    "iconic": {"clear": 0.018, "chin": 0.80, "neck_in": True, "visor_out": 0.03,
               "rings": [-0.24, -0.12, -0.02, 0.06, 0.16, 0.28, 0.40, 0.50, 0.60, 0.72, 0.84, 0.93, 0.98],
               "hides": ["head", "hair_short", "hair_bob"]},
}
HELM_SIDES = 16
HELM_RINGS = [-0.06, 0.04, 0.14, 0.24, 0.33, 0.40, 0.44, 0.52, 0.58, 0.66, 0.76, 0.86, 0.93, 0.98]
HELM_THICK = 0.012
SLIT = (0.44, 0.52)            # the eye slit, between these heights (shares of chin to crown)
BROW = (0.52, 0.58)            # the band round the brow, just above it


def head_frame(rig):
    """The head's chin height, height (chin to crown) and middle (x, y) in rig space."""
    head = part(rig, "head")
    m = to_rig(rig, head)
    vs = [m @ v.co for v in head.data.vertices]
    crown = max(v.z for v in vs)
    height = 0.135 * crown / 0.975          # build_humanoid: HEAD_HEIGHT of a body CHIN below its crown
    chin = crown - height
    face = [v for v in vs if v.z > chin + height * 0.3]
    mid_y = (min(v.y for v in face) + max(v.y for v in face)) / 2
    return chin, height, Vector((0, mid_y, 0))


def helm_rings(rig, st):
    """The helm's rings: per height, a radius per side, from the head's measured outline."""
    from mathutils.bvhtree import BVHTree
    from mathutils.geometry import convex_hull_2d, intersect_line_line_2d
    head = part(rig, "head")
    me = head.data.copy()
    me.transform(to_rig(rig, head))
    bvh = BVHTree.FromPolygons([v.co for v in me.vertices], [p.vertices for p in me.polygons])
    bpy.data.meshes.remove(me)
    chin, height, mid = head_frame(rig)
    angles = [2 * math.pi * k / HELM_SIDES for k in range(HELM_SIDES)]    # a vertex on the midline
    dirs = [Vector((math.sin(a), math.cos(a), 0)) for a in angles]   # 0 is the front (+Y)
    rings = []
    for t in st.get("rings", HELM_RINGS):
        z = chin + t * height
        c = Vector((mid.x, mid.y, z))
        pts = []
        for d in dirs:
            hit = bvh.ray_cast(c + d * 0.5, -d, 0.5)
            r = 0.5 - hit[3] if hit[0] is not None else 0.02
            pts.append(d * max(r, 0.02))
        hull = [Vector((p.x, p.y)) for p in [pts[i] for i in convex_hull_2d([(p.x, p.y) for p in pts])]]
        radii = []
        for d in dirs:
            far = Vector((d.x, d.y)) * 2.0
            best = 0.02
            for p, q in zip(hull, hull[1:] + hull[:1]):
                hit = intersect_line_line_2d(Vector((0, 0)), far, p, q)
                if hit is not None:
                    best = max(best, hit.length)
            radii.append(best + st["clear"])
        rings.append((t, z, radii))
    # Hang from the widest: below the eyes each side falls nearly straight, then the face and the
    # sides draw in towards the jaw (by "chin"); the back stays fuller over the nape.
    widest = [0.0] * HELM_SIDES
    for i in range(len(rings) - 1, -1, -1):
        t, z, radii = rings[i]
        for k in range(HELM_SIDES):
            back = abs(math.degrees(math.atan2(dirs[k].x, dirs[k].y))) > 110
            low = st["chin"] if not back else (1 + st["chin"]) / 2
            taper = 0.97 if t >= SLIT[0] else low + (0.97 - low) * max(0.0, t) / SLIT[0]
            if not (st.get("neck_in") and t < 0.02):
                radii[k] = max(radii[k], widest[k] * taper)
            widest[k] = max(widest[k], radii[k])
    # The face under the slit draws back towards the chin (the head is hidden, so nothing shows
    # through): the visor slopes like a face instead of jutting over the nose and mouth.
    at_slit = next(r for t, z, r in rings if t >= SLIT[0])
    for t, z, radii in rings:
        if t < SLIT[0]:
            for k in range(HELM_SIDES):
                if abs(math.degrees(math.atan2(dirs[k].x, dirs[k].y))) < 70:
                    radii[k] = min(radii[k], at_slit[k] * (0.86 + 0.14 * max(t, 0.0) / SLIT[0]))
    return rings, dirs, mid, chin, height


def helm(rig, st, style):
    rings, dirs, mid, chin, height = helm_rings(rig, st)
    bm = bmesh.new()
    vs = [[bm.verts.new(Vector((mid.x, mid.y, z)) + dirs[k] * r) for k, r in enumerate(radii)]
          for t, z, radii in rings]
    n = HELM_SIDES
    for r0, r1 in zip(vs, vs[1:]):
        for k in range(n):
            bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
    top = bm.verts.new(Vector((mid.x, mid.y, chin + height * 1.03)))
    for k in range(n):
        bm.faces.new((vs[-1][k], vs[-1][(k + 1) % n], top))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))

    def bearing(c):                 # degrees from the front, 0-180
        d = Vector((c.x - mid.x, c.y - mid.y))
        return abs(math.degrees(math.atan2(d.x, d.y)))

    def share(c):
        return (c.z - chin) / height

    if style == "open":
        # The face opens: below the brow in front, below the cheekbones beside it, below the nape.
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if (lambda c: (bearing(c) < 50 and share(c) < 0.60)
                                   or (bearing(c) < 100 and share(c) < 0.33) or share(c) < 0.14)(
                                   f.calc_center_median())], context="FACES")

    def piece(c):
        b, t = bearing(c), share(c)
        if style == "full" and b < 70 and SLIT[0] <= t < SLIT[1]:
            return "slit"
        if BROW[0] <= t < BROW[1]:
            return "brow"
        if style == "full" and b < 70 and t < SLIT[0]:
            return "visor"
        if t < 0.1:
            return "neck"
        return "skull"

    of = {f: piece(f.calc_center_median()) for f in bm.faces}
    normal = split_plates(bm, of)
    depth = {"skull": 0.0, "visor": st["visor"], "neck": st["neck"], "slit": -HELM_THICK, "brow": st["brow"]}
    lift = {}
    for v in bm.verts:
        if not v.link_faces:
            continue
        name = of.get(v.link_faces[0], "skull")
        nrm = normal.get(_key(v.co), v.normal)
        move = nrm * depth[name]
        b, t = bearing(v.co), share(v.co)
        if b < 1 or b > 179:
            if t > BROW[1] and name == "skull":
                move += nrm * st["comb"] * min(1.0, (t - BROW[1]) / 0.12)     # the comb over the top
            if name == "visor" and b < 1:
                move += nrm * st["prow"] * min(1.0, (SLIT[0] - t) / 0.08 + 0.4)   # the prow down the visor
        lift[v] = (nrm, 0.0 if name == "slit" else HELM_THICK + max(depth[name], 0.0))
        v.co += move
    rim_plates(bm, lift)
    for f in bm.faces:
        f.material_index = SEAM if of.get(f) == "slit" else METAL
    if style == "open":
        nasal(bm, mid, chin, height, rings)
    if style == "full":
        visor_fittings(bm, mid, chin, height)
    return bm


def visor_fittings(bm, mid, chin, height):
    """The visor's pivot at each temple (a low boss) and breaths on each lower cheek (dark slits)."""
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromBMesh(bm)

    def on_shell(deg, t):
        d = Vector((math.sin(math.radians(deg)), math.cos(math.radians(deg)), 0))
        c = Vector((mid.x, mid.y, chin + t * height))
        hit = bvh.ray_cast(c + d * 0.5, -d, 0.5)
        if hit[0] is None:
            return None, None
        return hit[0], (hit[1] if hit[1].dot(d) > 0 else -hit[1])      # facing out

    for sd in (1, -1):
        p, n = on_shell(sd * 88, (SLIT[0] + SLIT[1]) / 2)
        if p is not None:
            u = n.cross(Vector((0, 0, 1))).normalized()
            w = n.cross(u)
            ring = [(u * math.cos(a) + w * math.sin(a)) for a in [2 * math.pi * i / 8 for i in range(8)]]
            pts = [p - n * 0.004 + r * 0.03 for r in ring] + [p + n * 0.012 + r * 0.02 for r in ring] + [p + n * 0.016]
            A.hull(bm, pts, SEAM)           # dark, like the slit: one metal alone hides a boss
        for deg in (28, 40, 52):
            p, n = on_shell(sd * deg, 0.24)
            if p is None:
                continue
            u = n.cross(Vector((0, 0, 1))).normalized()
            up = Vector((0, 0, 1))
            # A thin dark slab on the shell (solid, so it faces out whichever way it is seen).
            outline = [p + u * dx + up * dz for dx, dz in ((-0.003, -0.018), (0.003, -0.018), (0.003, 0.018), (-0.003, 0.018))]
            A.hull(bm, [q - n * 0.001 for q in outline] + [q + n * 0.0025 for q in outline], SEAM)


def nasal(bm, mid, chin, height, rings):
    """A bar down the nose from the brow, standing on the front of the cap."""
    def front_at(t):
        lo = max((r for r in rings if r[0] <= t), key=lambda r: r[0])
        hi = min((r for r in rings if r[0] >= t), key=lambda r: r[0])
        f = (t - lo[0]) / (hi[0] - lo[0]) if hi[0] > lo[0] else 0.0
        front = lambda radii: radii[0]
        return front(lo[2]) + (front(hi[2]) - front(lo[2])) * f
    w, t0, t1 = 0.012, 0.36, 0.64
    pts = []
    for t in (t0, t1):
        y = mid.y + front_at(t)
        z = chin + t * height
        for dx in (-w, w):
            for dy in (-0.004, 0.008):
                pts.append(Vector((mid.x + dx, y + dy, z)))
    for f in A.hull(bm, pts, METAL):
        f.material_index = METAL


# ---- the iconic set: Old School's way ----
#
# Big smooth shells and one oversized feature per piece, in two tones of the bar's metal (the
# lighter "Copper", the darker "CopperShade"; a bar recolours both). No lames, no rims on every
# edge, no small fittings:
#   body  one rigid chest whose hem dips to a point at the belly, plain sleeves with a flared cuff,
#         and huge rounded pauldrons in the darker tone
#   legs  a light waistband dipping to a point in front, plain dark legs flaring at the ankle, and
#         big faceted knee caps in the light tone
#   helm  a round dome narrowing to the neck, and a dark visor jutting forward with the eye slit

ICONIC = {"chest": .012, "sleeve": .010, "cuff": (.010, .017), "cap": (.012, .062), "dome": .022,
          "reach": .52, "dip": .07,
          "waist": (.008, .018), "leg": .010, "greave": (.010, .017), "knee": .036}


def iconic_body(rig, st=ICONIC):
    b = Body(rig)
    bm = surface(rig, lift=0.0)
    rigidize(bm, b)
    # The hem dips to a point at the front (and a little at the back).
    for v in bm.verts:
        if v.is_boundary and v.co.z < b.hem + 0.01:
            share = 1 - min(1.0, abs(v.co.x) / (b.width * 0.85))
            v.co.z -= st["dip"] * share * (1.0 if v.co.y > b.mid_y else 0.4)
    sx, ex, wx = b.shoulder.x, b.elbow.x, b.wrist.x
    cap_out = sx + (ex - sx) * st["reach"]
    caps = {}
    for s in (1, -1):
        neck_side = Vector((s * (b.width * 0.62), 0, b.top))
        armpit = Vector((s * (sx + 0.01), 0, b.shoulder.z - 0.09))
        d = armpit - neck_side
        no = Vector((d.z, 0, -d.x)).normalized()
        if no.dot(Vector((s, 0, 1))) < 0:
            no = -no
        caps[s] = (neck_side.lerp(armpit, 0.5), no)
        cut(bm, caps[s][0], no, lambda c, s=s: side_of(c) == s and abs(c.x) < cap_out + 0.02
            and c.z > b.shoulder.z - 0.16)
        for x in (cap_out, wx - 0.05):
            cut(bm, (s * x, 0, 0), (1, 0, 0), lambda c, s=s: side_of(c) == s and abs(c.x) > sx)

    def piece(c):
        s, ax = side_of(c), abs(c.x)
        mid, no = caps[s]
        if (c - mid).dot(no) > 0 and ax < cap_out:
            return "cap"
        if ax > sx - 0.01 or (c - mid).dot(no) > 0:
            return "cuff" if ax > wx - 0.05 else "sleeve"
        return "chest"

    of = {f: piece(f.calc_center_median()) for f in bm.faces}
    neck = {_key(v.co) for e in bm.edges if e.is_boundary for v in e.verts
            if abs(v.co.x) < b.shoulder.x and v.co.z > b.shoulder.z}
    normal = split_plates(bm, of)
    at_neck = {v for v in bm.verts if _key(v.co) in neck}
    lift = {}
    for v in bm.verts:
        name = of[v.link_faces[0]]
        n = normal[_key(v.co)]
        move = Vector()
        if name == "cap":
            # Rises out of the chest beside the neck, flares out over the arm: a big rounded shell.
            s = side_of(v.co)
            mid, no = caps[s]
            reach = smoothstep(0.0, 0.08, (v.co - mid).dot(no))
            down = smoothstep(sx, cap_out, abs(v.co.x))
            depth = st["cap"][0] + (st["cap"][1] - st["cap"][0]) * max(reach, down) * reach
            centre = Vector((s * sx, b.shoulder.y, b.shoulder.z - 0.03))
            move += (v.co - centre).normalized() * st["dome"] * reach
        elif name == "cuff":
            depth = plate_depth(st["cuff"], -abs(v.co.x), -(wx - 0.05), -wx)
        else:
            depth = st[name]
        move += n * depth
        lift[v] = (n, max(depth - RIM, 0.004))
        v.co += move
    gorget(bm, [e for e in bm.edges if e.is_boundary and all(v in at_neck for v in e.verts)], lift)
    rim_plates(bm, lift)
    for f in bm.faces:
        f.material_index = SHADE if of.get(f) == "cap" else METAL
    return bm


def iconic_legs(rig, st=ICONIC):
    bm = surface(rig, lift=0.0, parts=LEG_HIDES)
    hip, knee, ankle = bone(rig, "thigh.R"), bone(rig, "shin.R"), bone(rig, "foot.R")
    top = max(v.co.z for v in bm.verts)
    front_y = max(v.co.y for v in bm.verts if v.co.z > hip.z - 0.12)
    # The waistband's lower edge: a chevron from the hips down to a point in front.
    point = Vector((0, 0, hip.z - 0.11))
    a = math.atan(0.45)
    chev = {s: Vector((-s * math.sin(a), 0, math.cos(a))) for s in (1, -1)}
    for s in (1, -1):
        cut(bm, point, chev[s], lambda c, s=s: side_of(c) == s and c.z > hip.z - 0.2)
    cut(bm, (0, 0, 0), (1, 0, 0), lambda c: c.z > hip.z - 0.2)

    def piece(c):
        if c.z > hip.z - 0.2 and (c - point).dot(chev[side_of(c)]) > 0:
            return "waist"
        return "leg"

    of = {f: piece(f.calc_center_median()) for f in bm.faces}
    normal = split_plates(bm, of)
    lift = {}
    for v in bm.verts:
        name = of[v.link_faces[0]]
        n = normal[_key(v.co)]
        if name == "waist":
            depth = plate_depth(st["waist"], v.co.z, top, point.z)
        elif v.co.z < knee.z:
            depth = plate_depth(st["greave"], v.co.z, knee.z, ankle.z)
        else:
            depth = st["leg"]
        lift[v] = (n, max(depth - RIM, 0.004))
        v.co += n * depth
    rim_plates(bm, lift)
    for f in bm.faces:
        f.material_index = METAL if of.get(f) == "waist" else SHADE
    # The knee caps: big faceted domes over the front of each knee.
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromBMesh(bm)
    for s in (1, -1):
        k = Vector((s * abs(knee.x), knee.y, knee.z + 0.01))
        hit = bvh.ray_cast(k + Vector((0, 0.5, 0)), Vector((0, -1, 0)), 0.6)
        if hit[0] is None:
            continue
        p = hit[0]
        rings = [(-0.006, 0.055), (0.018, 0.048), (0.032, 0.026), (st["knee"], 0.0)]
        pts = []
        for fwd, r in rings:
            for i in range(8 if r > 0 else 1):
                ang = 2 * math.pi * (i + 0.5) / 8
                pts.append(p + Vector((math.cos(ang) * r * 0.95, fwd, math.sin(ang) * r * 1.1)))
        A.hull(bm, pts, METAL)
    return bm


def iconic_helm(rig, st):
    """A round dome narrowing to the neck, and a visor jutting forward with the eye slit."""
    rings, dirs, mid, chin, height = helm_rings(rig, st)
    bm = bmesh.new()
    vs = [[bm.verts.new(Vector((mid.x, mid.y, z)) + dirs[k] * r) for k, r in enumerate(radii)]
          for t, z, radii in rings]
    n = HELM_SIDES
    for r0, r1 in zip(vs, vs[1:]):
        for k in range(n):
            bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
    top = bm.verts.new(Vector((mid.x, mid.y, chin + height * 1.02)))
    for k in range(n):
        bm.faces.new((vs[-1][k], vs[-1][(k + 1) % n], top))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    rim_plates(bm, {v: (v.normal.copy(), HELM_THICK) for v in bm.verts})
    for f in bm.faces:
        f.material_index = METAL

    # The visor: a wedge standing out from the face between the brow and the mouth, its front
    # coming to a ridge down the middle and drawing back towards the chin, the slit across it.
    def ring_at(t):
        return min(rings, key=lambda r: abs(r[0] - t))[2]
    eye = ring_at(0.48)
    front = mid.y + eye[0]
    half = eye[HELM_SIDES // 4] * 0.66
    t_top, t_bot = 0.60, 0.20
    z_top, z_bot = chin + t_top * height, chin + t_bot * height
    out = st["visor_out"]
    box = []
    for z, w, fy in ((z_top, half, front + out), (z_bot, half * 0.72, front + out * 0.45)):
        box.append(Vector((mid.x, fy + out * 0.45, z)))               # the ridge
        for x in (-w, w):
            box.append(Vector((mid.x + x, fy, z)))
            box.append(Vector((mid.x + x * 0.95, front - eye[0] * 0.35, z)))
    A.hull(bm, box, SHADE)
    # The slit: a dark band across the visor's front, following its ridge.
    zs = (chin + 0.45 * height, chin + 0.505 * height)
    slab = []
    for z in zs:
        f = (z - z_bot) / (z_top - z_bot)
        fy = front + out * (0.45 + 0.55 * f)
        w = half * (0.72 + 0.28 * f) - 0.01
        ridge = out * 0.45 * 1.0
        for x, dy in ((-w, 0.0), (0.0, ridge), (w, 0.0)):
            slab += [Vector((mid.x + x, fy + dy - 0.006, z)), Vector((mid.x + x, fy + dy + 0.003, z))]
    A.hull(bm, slab, SEAM)
    return bm


# ---- full helm sketches ----
#
# Full helms drawn as designed shapes, not fitted to the face: rings round the head, each a half
# width, a front and a back (shares of chin-to-crown, from the head's middle) and how square its
# front half is; lofted, with the eye slit set in, one feature in the darker tone, and a rim on
# the open edge. Sizes are in head heights, so one design fits both bodies. Each is ours, from
# somewhere in R'th; the slit is common to all.

HELM_DESIGNS = {
    # Cruor's empire: a squarish tower, crowned with merlons (the capital's one repeated element).
    "tower": {"rings": [(-0.10, .31, .35, .41, 3.2), (0.02, .34, .40, .46, 3.2), (0.30, .355, .42, .48, 3.2),
                        (0.62, .36, .42, .485, 3.2), (0.97, .36, .42, .485, 3.2)],
              "top": "flat", "merlons": 8, "band": (0.86, 0.97)},
    # The blood oath: a drop, round below and rising to a point, bound round the brow.
    "drop": {"rings": [(-0.08, .29, .34, .40, 2.2), (0.05, .345, .40, .46, 2.2), (0.30, .37, .43, .49, 2.2),
                       (0.56, .37, .43, .49, 2.2), (0.80, .32, .37, .43, 2.2), (1.00, .22, .26, .30, 2.2),
                       (1.16, .11, .13, .15, 2.2)],
             "top": 1.34, "band": (0.55, 0.64)},
    # Frostpeak: squat, wide and angular, a heavy brow ledge over the slit.
    "frostpeak": {"rings": [(-0.06, .38, .40, .44, 4.0), (0.20, .40, .44, .48, 4.0), (0.52, .40, .44, .48, 4.0),
                            (0.82, .38, .42, .46, 4.0), (0.96, .30, .34, .40, 3.0)],
                  "top": "flat", "ledge": (0.52, 0.64, 0.12)},
    # A sallet: a smooth dome whose tail sweeps back over the neck.
    "sallet": {"rings": [(-0.22, .36, .30, .80, 2.2), (-0.04, .35, .39, .66, 2.2), (0.20, .36, .42, .55, 2.2),
                         (0.52, .37, .43, .50, 2.2), (0.80, .32, .37, .43, 2.2), (0.95, .20, .24, .28, 2.2)],
               "top": 1.02, "tail": True},
    # A frog-mouth: the skull juts forward over the slit like a beak; the face plate recedes.
    "frogmouth": {"rings": [(-0.06, .30, .31, .44, 2.4), (0.10, .34, .40, .47, 2.6), (0.30, .36, .47, .49, 2.8),
                            (0.43, .36, .43, .49, 2.8), (0.47, .36, .36, .49, 2.8), (0.52, .36, .63, .49, 2.8),
                            (0.62, .36, .50, .49, 2.4), (0.82, .33, .38, .45, 2.2), (0.95, .22, .25, .30, 2.2)],
                  "top": 1.03, "lower": 0.47, "slit": (0.47, 0.52)},
}
DESIGN_SIDES = 16


def _ring_point(a, w, f, b, p):
    """The outline of a ring at angle a (0 the front, towards the left): a superellipse between the
    front and the side (exponent p), an ellipse between the side and the back."""
    s, c = math.sin(a), math.cos(a)
    if c >= 0:
        r = 1 / ((c / f) ** p + (abs(s) / w) ** p) ** (1 / p) if c > 1e-9 else w
    else:
        r = 1 / ((-c / b) ** 2.2 + (abs(s) / w) ** 2.2) ** (1 / 2.2)
    return Vector((s * r, c * r, 0))


def design_helm(rig, name):
    d = HELM_DESIGNS[name]
    chin, height, mid = head_frame(rig)
    centre = lambda t: Vector((mid.x, mid.y, chin + t * height))
    bm = bmesh.new()
    angles = [2 * math.pi * k / DESIGN_SIDES for k in range(DESIGN_SIDES)]
    rings = [[bm.verts.new(centre(t) + _ring_point(a, w, f, b, p) * height) for a in angles]
             for t, w, f, b, p in d["rings"]]
    n = DESIGN_SIDES
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(n):
            bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
    top_t = d["rings"][-1][0] if d["top"] == "flat" else d["top"]
    top = bm.verts.new(centre(top_t))
    for k in range(n):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % n], top))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))

    def bearing(c):
        return abs(math.degrees(math.atan2(c.x - mid.x, c.y - mid.y)))

    def share(c):
        return (c.z - chin) / height

    slit = d.get("slit", SLIT)
    for t in slit:
        cut(bm, centre(t), (0, 0, 1))
    for t in d.get("band", ()):
        cut(bm, centre(t), (0, 0, 1))

    def piece(c):
        b, t = bearing(c), share(c)
        if b < 66 and slit[0] <= t < slit[1]:
            return "slit"
        if "band" in d and d["band"][0] <= t < d["band"][1]:
            return "band"
        if "lower" in d and t < d["lower"] and b < 100:
            return "lower"
        if d.get("tail") and t < 0.12 and b > 80:
            return "tail"
        return "shell"

    of = {f: piece(f.calc_center_median()) for f in bm.faces}
    normal = split_plates(bm, of)
    depth = {"shell": 0.0, "slit": -HELM_THICK, "band": 0.006, "lower": 0.0, "tail": 0.004}
    lift = {}
    for v in bm.verts:
        if not v.link_faces:
            continue
        nm = of[v.link_faces[0]]
        nrm = normal.get(_key(v.co), v.normal)
        v.co += nrm * depth[nm]
        lift[v] = (nrm, 0.0 if nm == "slit" else HELM_THICK + depth[nm])
    rim_plates(bm, lift)
    tone = {"slit": SEAM, "band": SHADE, "lower": SHADE, "tail": SHADE}
    for f in bm.faces:
        f.material_index = tone.get(of.get(f), METAL)

    if d.get("merlons"):
        # Merlons on the rim of the flat top: one every other side, standing up.
        t0, w, fr, bk, p = d["rings"][-1]
        z0, z1 = chin + t0 * height - 0.004, chin + (t0 + 0.11) * height
        for k in range(0, n, 2):
            a0, a1 = angles[k], angles[k] + 2 * math.pi / n * 0.9
            outer = [_ring_point(a, w, fr, bk, p) * height for a in (a0, a1)]
            pts = []
            for o in outer:
                for z in (z0, z1):
                    base = Vector((mid.x, mid.y, z))
                    pts += [base + o * 1.02, base + o * 0.86]
            A.hull(bm, pts, SHADE)
    if d.get("ledge"):
        # A brow ledge: a slab standing out over the slit, round the front and the sides.
        t0, t1, out = d["ledge"]
        z0, z1 = chin + t0 * height, chin + t1 * height
        ring = min(d["rings"], key=lambda r: abs(r[0] - (t0 + t1) / 2))
        _, w, fr, bk, p = ring
        for k in range(-5, 5):
            a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
            pts = []
            for a in (a0, a1):
                o = _ring_point(a, w, fr, bk, p) * height
                grow = out * height * (0.5 + 0.5 * math.cos(a))
                for z, g in ((z0, grow), (z1, grow * 0.35)):
                    base = Vector((mid.x, mid.y, z))
                    pts += [base + o * 0.97, base + o + o.normalized() * g]
            A.hull(bm, pts, SHADE)
    return bm


def bind_rigid(ob, rig, bone_name="head"):
    """Hold a piece rigidly on one bone (a helm on the head)."""
    ob.parent = rig
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = Matrix.Identity(4)
    ob.vertex_groups.new(name=bone_name).add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")
    ob.modifiers.new("Armature", "ARMATURE").object = rig
    return ob


def kit(rig, name):
    """A kit part by its name in the client ("torso", "hair_short"...)."""
    return next((o for o in rig.children if o.name.endswith("_body_" + name) or o.name.endswith("_" + name)
                 and name.startswith("hair_")), None)


def bind(ob, rig, parts=HIDES):
    """Skin the piece to the rig: each vertex copies the weights of the nearest kit part it covers."""
    me = bpy.data.meshes.new("_bindsrc")
    src = bpy.data.objects.new("_bindsrc", me)
    bpy.context.scene.collection.objects.link(src)
    verts, polys, weights = [], [], []
    for name in parts:
        o = part(rig, name)
        m = to_rig(rig, o)
        base = len(verts)
        names = {g.index: g.name for g in o.vertex_groups}
        for v in o.data.vertices:
            verts.append(tuple(m @ v.co))
            weights.append({names[g.group]: g.weight for g in v.groups if g.group in names})
        polys += [[base + i for i in p.vertices] for p in o.data.polygons]
    me.from_pydata(verts, [], polys)
    for i, w in enumerate(weights):
        for name, wt in w.items():
            (src.vertex_groups.get(name) or src.vertex_groups.new(name=name)).add([i], wt, "REPLACE")
    ob.parent = None
    ob.matrix_world = Matrix.Identity(4)
    for g in src.vertex_groups:
        if ob.vertex_groups.get(g.name) is None:
            ob.vertex_groups.new(name=g.name)
    dt = ob.modifiers.new("bind", "DATA_TRANSFER")
    dt.object = src
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    dt.layers_vgroup_select_src = "ALL"
    dt.layers_vgroup_select_dst = "NAME"
    bpy.context.view_layer.update()
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=dt.name)
    bpy.data.objects.remove(src, do_unlink=True)
    bpy.data.meshes.remove(me)
    ob.parent = rig
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = Matrix.Identity(4)
    ob.modifiers.new("Armature", "ARMATURE").object = rig
    return ob


def make(sex, x=0.0, style="bold", piece="body"):
    """One piece on one body. piece "body": style "coat" (the plated coat) or a key of STYLES (the
    platebody); piece "legs": a key of LEG_STYLES (the platelegs); piece "head": a key of
    HELM_STYLES (the helms)."""
    rig = load_body(sex, x)
    name = BODIES[sex] + {"legs": "_legs_", "head": "_helm_"}.get(piece, "_") + style
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    full = ["head", "hair_short", "hair_bob"]
    hides = {"legs": LEG_HIDES, "head": full if style in HELM_DESIGNS else HELM_STYLES.get(style, {}).get("hides")}.get(piece) or HIDES
    if piece == "head" and style in HELM_DESIGNS:
        bm = design_helm(rig, style)
    elif style == "iconic":
        bm = {"legs": iconic_legs, "head": lambda r: iconic_helm(r, HELM_STYLES["iconic"])}.get(
            piece, iconic_body)(rig)
    elif piece == "legs":
        bm = platelegs(rig, LEG_STYLES[style])
    elif piece == "head":
        bm = helm(rig, HELM_STYLES[style], style)
    else:
        bm, _ = coat(rig) if style == "coat" else plates(rig, STYLES[style])
    A.finish(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for n in MATS:
        me.materials.append(A.mat(n))
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    rig.users_collection[0].objects.link(ob)
    if piece == "head":
        bind_rigid(ob, rig)
    else:
        bind(ob, rig, hides)
    ob["hides"] = ",".join(hides)
    for p in hides:
        o = kit(rig, p)
        if o is None:
            continue
        o.hide_set(True)
        o.hide_render = True
    return ob


def pose(rig, arms_down=72.0):
    """A relaxed stance to look at armour in: the upper arms lowered."""
    rig.data.pose_position = "POSE"
    for side, sign in (("L", -1), ("R", 1)):
        pb = rig.pose.bones["upper_arm." + side]
        axis = (pb.bone.matrix_local.to_3x3().inverted() @ Vector((0, 1, 0))).normalized()
        pb.rotation_mode = "AXIS_ANGLE"
        pb.rotation_axis_angle = (math.radians(arms_down) * sign, *axis)
    bpy.context.view_layer.update()


def build(posed=True, style="bold"):
    out = {}
    for sex, x in (("male", -0.45), ("female", 0.45)):
        ob = make(sex, x, style)
        if posed:
            pose(ob.parent)
        out[sex] = A.report([ob])
    return out


if __name__ == "__main__" or "result" not in globals():
    result = build()
