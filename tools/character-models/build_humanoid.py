"""
Builds R'th's modular humanoid from Clint Bellanger's base human (CC-BY 3.0, source/human.blend).

Run inside Blender (5.x), e.g. through the Blender MCP or:
    blender --background --python tools/character-models/build_humanoid.py

What it does, for the male base (the female follows once there is an appearance screen):
  1. Opens the source, clears the old pose (the rest pose is a T-pose), centres the body and
     turns it to face +Y (Unity's +Z after export).
  2. Replaces the old skeleton with one laid out the way Unity's Humanoid expects
     (hips > spine > chest > neck > head, arms off the chest, legs off the hips), and moves the
     old skin weights across to it.
  3. Splits the body into kit parts (head, torso, arms, hands, legs, feet), each one skinned
     mesh. Equipment later is more skinned meshes on the same skeleton that hide the kit parts
     they cover.
  4. Gives every face a colour channel through its material name (skin, hair, primary,
     secondary, leather); the game colours each channel, so one mesh can be recoloured.
  5. Replaces the source's head with our own, lofted from HEAD_GRID (a jaw, a nose, a brow with
     the eyes set under it, ears), with dark almonds for the eyes and a bare neck above the collar.
  6. Adds short hair as its own part, and exports one FBX (skeleton + parts) into the Unity
     project. Also saves the built .blend beside this script for hand edits.

Naming contract with the client (client/Assets/Scripts/Characters/):
  - bones: hips, spine, chest, neck, head, upper_arm.L/R, forearm.L/R, hand.L/R,
           thigh.L/R, shin.L/R, foot.L/R, toe.L/R, and grip.L/R (no weights: the centre of each
           closed fist, where held things attach)
  - meshes: "body_<part>" for body kit (head, torso, arms, hands, legs, feet) and "hair_<style>"
            (a mesh may not share a name with a bone, or Unity can't map the Humanoid)
  - materials: the colour channel name (skin, hair, primary, secondary, leather, eyes)
"""

import math
import os
import sys
import bpy
import bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\dev\rth\tools\character-models"
SOURCE = os.path.join(HERE, "source", "human.blend")
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
# Set SEX ("male" or "female") and PREVIEW (build without exporting or saving) before running.
SEX = globals().get("SEX", "male")
PREVIEW = globals().get("PREVIEW", False)
NAME = {"male": "HumanMale", "female": "HumanFemale"}[SEX]
EXPORT = os.path.join(REPO, "client", "Assets", "Resources", "Characters", NAME + ".fbx")
BUILT_BLEND = os.path.join(HERE, NAME + ".blend")
sys.path.insert(0, os.path.join(REPO, "tools", "art", "blender"))
import rth_art as A  # noqa: E402  (decal)

# Old vertex group -> new bone. Back and Head are split by height below.
GROUP_MAP = {
    "Hip": "hips",
    "Upperarm.Left": "upper_arm.L", "Upperarm.Right": "upper_arm.R",
    "Forearm.Left": "forearm.L", "Forearm.Right": "forearm.R",
    "Thigh.Left": "thigh.L", "Thigh.Right": "thigh.R",
    "Shin.Left": "shin.L", "Shin.Right": "shin.R",
    "Foot.Left": "foot.L", "Foot.Right": "foot.R",
    "Toes.Left": "toe.L", "Toes.Right": "toe.R",
}
for side, s in (("Left", "L"), ("Right", "R")):
    for g in ("Hand", "Fingers1", "Fingers2", "Thumb1", "Thumb2"):
        GROUP_MAP[f"{g}.{side}"] = f"hand.{s}"

# Bone -> body kit part, and each part's colour channel.
PART_OF_BONE = {
    "head": "head", "neck": "head",
    "chest": "torso", "spine": "torso",
    "upper_arm.L": "arms", "upper_arm.R": "arms", "forearm.L": "arms", "forearm.R": "arms",
    "hand.L": "hands", "hand.R": "hands",
    "hips": "legs", "thigh.L": "legs", "thigh.R": "legs", "shin.L": "legs", "shin.R": "legs",
    "foot.L": "feet", "foot.R": "feet", "toe.L": "feet", "toe.R": "feet",
}
CHANNEL_OF_PART = {
    "head": "skin", "torso": "primary", "arms": "primary", "hands": "skin",
    "legs": "secondary", "feet": "leather",
}


HEAD_SCALE = globals().get("HEAD_SCALE", 1.15)   # how much bigger than the source the head is
# The source's skull is long from front to back (half as deep again as it is wide); the reference's
# head is about as deep as it is wide. Squashed front to back about the head's middle.
HEAD_DEPTH = globals().get("HEAD_DEPTH", 1.0)
HAIR_THICK = globals().get("HAIR_THICK", 0.022)  # how far short hair stands off the skull
HAIR_EDGE = globals().get("HAIR_EDGE", 0.008)    # at its edge (None: as thick as the rest)
WAIST_ABOVE_HIP = 0.07     # where the shirt meets the trousers, above the hip joints

# The eyes: a small dark ellipse each, cast onto the face from the front. Sizes are shares of the
# head's height (neck joint to crown), so they follow the head: (x, height above the neck joint)
# of the centre, half width, half height, and the tilt (the inner end a little lower, as in the
# reference).
EYE_CENTRE = (0.106, 0.54)
EYE_HALF_W, EYE_HALF_H = 0.065, 0.024
EYE_TILT = 0.20
EYE_POINTS = 6             # the outline's corners: 6 makes a long almond, near the reference's strokes


# Our own head (the source's is a tall box with a flat face), one mesh from the base of the neck
# to the crown, lofted through HEAD_GRID: rows from the neck's base to the top of the skull, each
# ten points from the middle of the front round to the middle of the back, (across, forward, up)
# as shares of the head's height (chin 0, crown 1; below 0 is the neck; forward from the skull's
# middle); the left half mirrors. The columns mean the same thing on every row, so a feature is a
# few points moved: 0 the midline, 1 the side of the nose and the inner eye, 2 the eye, 3 the
# outer eye and the cheek, 4 the cheekbone and the side of the face, 5 the side (the ear's front),
# 6 behind the ear, 7 and 8 the back, 9 the back midline. Read down the rows:
#   - the neck narrows from the shoulders (its base, buried in the collar) to under the jaw;
#   - the jaw's edge runs from a flat chin back and up to its corner behind the mouth, then on to
#     the nape; the mandible stands off the neck, so there is a shelf under the jaw;
#   - the mouth is a little recessed between the chin and the nose's base;
#   - the nose is the midline and column 1 pushed forward between its base and its bridge;
#   - the eyes sit back in sockets under a brow that stands out over them;
#   - the forehead leans back to a flat top; the skull is widest above the ears and longer behind
#     than the face is in front.
# Proportions after the 2004 game's head (chin to crown 13 % of the body, nearly as deep as it is
# wide, a square jaw); the shape is ours.
HEAD_BUILT = globals().get("HEAD_BUILT", True)
HEAD_HEIGHT = 0.135          # chin to crown, as a share of the source body's height
CHIN = 0.16                  # how far below the source's crown the chin stays (where the neck needs it)
HEAD_WIDTH = 1.1             # all the rows' widths times this
BACK_SQUASH = 0.85           # the skull behind the ears times this: nearly as deep as it is wide
HEAD_FORWARD = 0.035         # the skull's middle, in front of the neck joint
HEAD_GRID = {
    "male": [
        [(0, .17, -.50), (.12, .16, -.50), (.24, .12, -.50), (.36, .04, -.50), (.46, -.08, -.50), (.50, -.18, -.50), (.44, -.30, -.50), (.32, -.39, -.50), (.16, -.45, -.50), (0, -.47, -.50)],  # neck's base
        [(0, .13, -.25), (.08, .125, -.25), (.16, .09, -.25), (.22, .02, -.25), (.26, -.07, -.25), (.26, -.16, -.25), (.23, -.25, -.25), (.17, -.32, -.25), (.09, -.37, -.25), (0, -.38, -.25)],  # neck
        [(0, .14, -.03), (.07, .135, -.03), (.14, .10, -.02), (.19, .04, -.01), (.22, -.05, .00), (.23, -.14, .02), (.21, -.23, .05), (.16, -.30, .08), (.08, -.35, .10), (0, -.36, .11)],  # under the jaw
        [(0, .31, 0), (.08, .30, 0), (.13, .28, .01), (.22, .20, .04), (.29, .08, .09), (.31, -.06, .14), (.29, -.17, .17), (.20, -.26, .19), (.10, -.30, .19), (0, -.31, .19)],  # the jaw's edge: chin, corner, nape
        [(0, .37, .07), (.07, .365, .07), (.15, .33, .09), (.23, .24, .12), (.30, .10, .17), (.31, -.05, .22), (.29, -.17, .27), (.22, -.29, .30), (.11, -.35, .30), (0, -.36, .30)],  # chin
        [(0, .355, .20), (.09, .35, .20), (.18, .31, .21), (.26, .23, .24), (.32, .10, .28), (.34, -.04, .33), (.32, -.18, .38), (.24, -.32, .42), (.12, -.40, .43), (0, -.41, .43)],  # mouth
        [(0, .38, .30), (.11, .37, .30), (.19, .33, .31), (.28, .23, .33), (.34, .11, .37), (.36, -.04, .42), (.34, -.19, .48), (.26, -.34, .52), (.13, -.44, .54), (0, -.46, .54)],  # nose's base
        [(0, .50, .36), (.09, .43, .35), (.19, .34, .36), (.29, .24, .38), (.35, .11, .43), (.37, -.05, .50), (.35, -.20, .56), (.27, -.37, .61), (.14, -.48, .63), (0, -.50, .63)],  # nose's tip
        [(0, .42, .44), (.05, .37, .44), (.17, .32, .45), (.27, .25, .46), (.35, .14, .49), (.37, -.05, .56), (.36, -.21, .62), (.28, -.38, .68), (.14, -.49, .70), (0, -.51, .70)],  # bridge, cheekbones
        [(0, .35, .51), (.08, .33, .51), (.18, .30, .51), (.27, .23, .52), (.34, .11, .55), (.37, -.06, .62), (.36, -.22, .68), (.28, -.39, .74), (.14, -.50, .76), (0, -.52, .76)],  # eyes, in their sockets
        [(0, .385, .61), (.09, .39, .61), (.19, .37, .61), (.28, .29, .62), (.34, .14, .64), (.37, -.05, .70), (.35, -.22, .75), (.27, -.38, .80), (.14, -.48, .82), (0, -.50, .82)],  # brow
        [(0, .36, .77), (.09, .355, .77), (.19, .33, .77), (.28, .27, .77), (.35, .14, .78), (.37, -.05, .82), (.35, -.21, .85), (.26, -.35, .89), (.13, -.42, .91), (0, -.44, .91)],  # forehead
        [(0, .25, .93), (.09, .245, .93), (.18, .22, .93), (.26, .17, .93), (.30, .08, .93), (.31, -.06, .94), (.29, -.17, .95), (.20, -.26, .96), (.10, -.30, .97), (0, -.31, .975)],  # top
    ],
}
HEAD_CAP = (0, -.04, 1.0)    # the crown, a point above the top row
# An ear: a wedge off the side of the head, (front top, front bottom, outer top, outer bottom,
# back top, back bottom); the front and back edges sit inside the skull.
EAR = [(.33, -.04, .57), (.34, .00, .35), (.46, -.15, .54), (.43, -.07, .37), (.32, -.22, .56), (.33, -.13, .36)]
# Hard edges, where the mesh is split so the shading breaks: ("row", r, c0, c1) runs along row r
# from column c0 to c1, ("col", c, r0, r1) up column c from row r0 to r1.
HEAD_SHARP = {
    "male": [("row", 3, 0, 6),      # the jaw line, chin to corner
             ("row", 10, 1, 4),     # the brow over the eyes
             ("col", 1, 6, 7), ("row", 6, 0, 1), ("col", 0, 6, 8)],   # the nose's side, underside and ridge
}
# The eyes: a dark almond each, cast onto the socket from the front: (across, height above the
# chin) of the centre, half width and half height, all shares of the head's height.
HEAD_EYES = {"male": (0.14, 0.51, 0.075, 0.028)}


def female_head():
    """The female: a smaller, rounder head on a slimmer neck, a narrower jaw and chin, a smaller
    nose, a lighter brow with no ridge, and larger eyes."""
    rows = []
    for i, row in enumerate(HEAD_GRID["male"]):
        out = []
        for x, y, z in row:
            if i <= 2:
                x *= 0.85                              # the neck
            elif 3 <= i <= 6:
                x *= 0.88                              # the jaw and chin
            if i == 7 and x < .1:
                y -= 0.04                              # the nose
            if i == 10:
                y -= 0.03                              # the brow
            out.append((x * 0.96, y * 0.96, z * 0.96 if z > 0 else z))   # a smaller head
        rows.append(out)
    HEAD_GRID["female"] = rows
    HEAD_SHARP["female"] = [("row", 3, 0, 6), ("col", 1, 6, 7), ("row", 6, 0, 1), ("col", 0, 6, 8)]
    HEAD_EYES["female"] = (0.14 * 0.96, 0.51 * 0.96, 0.08, 0.03)


female_head()
# Where short hair stops: (degrees from the front, t), between which it runs straight.
HAIRLINE = [(0, 0.72), (45, 0.70), (80, 0.66), (115, 0.63), (140, 0.44), (180, 0.30)]


def hairline(deg):
    for (a0, t0), (a1, t1) in zip(HAIRLINE, HAIRLINE[1:]):
        if deg <= a1:
            return t0 + (t1 - t0) * (deg - a0) / (a1 - a0)
    return HAIRLINE[-1][1]


def head_point(p, crown, height, centre_y):
    x, y, z = p
    if y < 0:
        y *= BACK_SQUASH
    return Vector((x * HEAD_WIDTH * height, centre_y + y * height, crown - height + z * height))


def make_head(bm, crown, height, centre_y):
    """Our head into bm: HEAD_GRID lofted and mirrored, from the neck's base to a point at the
    crown, split along HEAD_SHARP, with an ear each side (the only flat faces) and the eyes."""
    rows = HEAD_GRID[SEX]
    half = len(rows[0]) - 1
    rings = []
    for row in rows:
        full = row + [(-x, y, z) for x, y, z in reversed(row[1:-1])]
        rings.append([bm.verts.new(head_point(p, crown, height, centre_y)) for p in full])
    n = len(rings[0])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(n):
            bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k])).smooth = True

    def vert(r, c, side):     # side 1: the right half's column c; -1: its mirror
        return rings[r][c if (side == 1 or c in (0, half)) else n - c]
    sharp = set()
    for kind, a, b0, b1 in HEAD_SHARP[SEX]:
        for side in (1, -1):
            for i in range(b0, b1):
                if kind == "row":
                    v0, v1 = vert(a, i, side), vert(a, i + 1, side)
                else:
                    v0, v1 = vert(i, a, side), vert(i + 1, a, side)
                e = bm.edges.get((v0, v1))
                if e:
                    sharp.add(e)
    bmesh.ops.split_edges(bm, edges=list(sharp))
    top = bm.verts.new(head_point(HEAD_CAP, crown, height, centre_y))
    for k in range(n):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % n], top)).smooth = True
    for s in (1, -1):
        ft, fb, ot, ob, bt, bb = [bm.verts.new(head_point((s * x, y, z), crown, height, centre_y)) for x, y, z in EAR]
        for f in ((ft, fb, ob, ot), (ot, ob, bb, bt), (ft, ot, bt), (fb, bb, ob), (ft, bt, bb, fb)):
            bm.faces.new(f).smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    ex, ez, hw, hh = HEAD_EYES[SEX]
    for s in (-1, 1):
        outline = []
        for i in range(EYE_POINTS):
            a = 2 * math.pi * i / EYE_POINTS
            dx, dz = hw * height * math.cos(a), hh * height * math.sin(a)
            outline.append((s * (ex * HEAD_WIDTH * height + dx), centre_y + 0.1,
                            crown - height + ez * height + dz + dx * EYE_TILT))
        eye = A.decal(bm, outline, (0, 1, 0), mat=1)
        # The almond folds over the socket's crease; as one polygon Unity may call it
        # self-intersecting and drop it, so it goes out as triangles.
        if eye:
            bmesh.ops.triangulate(bm, faces=[eye])


def make_hair(bm, crown, height, centre_y):
    """Short hair into bm: the skull's rows of HEAD_GRID above HAIRLINE, lofted from a clean ring
    along the hairline (a point on the skull's surface for every column) to the crown, then
    stood off the skull. HAIR_THICK over the skull, HAIR_EDGE at the hairline."""
    rows = HEAD_GRID[SEX]
    full = [row + [(-x, y, z) for x, y, z in reversed(row[1:-1])] for row in rows]
    n = len(full[0])
    cols = []
    for k in range(n):
        x, y, _ = full[-2][k]
        t = hairline(math.degrees(abs(math.atan2(x, y))))
        zs = [full[r][k][2] for r in range(len(rows))]
        r = max(i for i in range(len(rows) - 1) if zs[i] <= t)
        f = (t - zs[r]) / (zs[r + 1] - zs[r])
        at = tuple(a + (b - a) * f for a, b in zip(full[r][k], full[r + 1][k]))
        cols.append([at] + [full[i][k] for i in range(r + 1, len(rows))])
    count = max(len(c) for c in cols)
    rings = [[] for _ in range(count)]
    for k, col in enumerate(cols):
        col = [col[0]] * (count - len(col)) + col       # pad at the hairline; the doubles merge below
        for i, p in enumerate(col):
            rings[i].append(bm.verts.new(head_point(p, crown, height, centre_y)))
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(n):
            bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
    top = bm.verts.new(head_point(HEAD_CAP, crown, height, centre_y))
    for k in range(n):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % n], top))
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    edge = {v for v in bm.verts if v.is_boundary}
    ring1 = {e.other_vert(v) for v in edge for e in v.link_edges} - edge
    for v in bm.verts:
        if HAIR_EDGE is None or (v not in edge and v not in ring1):
            d = HAIR_THICK
        else:
            d = HAIR_EDGE if v in edge else (HAIR_EDGE + HAIR_THICK) / 2
        v.co += v.normal * d
    for f in bm.faces:
        f.smooth = True


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# The grip point, from the knuckles: this far towards the fingertips and this far below (palm side).
GRIP_ALONG = -0.015
GRIP_BELOW = 0.025

# How far each old finger bone curls to close the hand round a grip, for the right hand in the
# source's orientation (facing -Y, right hand along -X, palm down): (world axis, degrees). The left
# hand mirrors. OSRS-style hands never move their fingers, so this is baked into the mesh.
GRIP = {
    "Fingers1": ("Y", -80),
    "Fingers2": ("Y", -95),
    "Thumb1": ("X", 40),
    "Thumb2": ("Z", -55),
}


def close_hands(body, rig):
    """Poses the old finger bones into a grip and bakes that into the mesh, then rests the rig."""
    to_rig = rig.matrix_world.to_3x3().normalized()
    for side, mirror in (("Right", 1), ("Left", -1)):
        for bone, (axis, degrees) in GRIP.items():
            pb = rig.pose.bones[f"{bone}.{side}"]
            pb.rotation_mode = "QUATERNION"
            # Mirroring across x flips rotations about y and z, not about x.
            angle = math.radians(degrees * (mirror if axis != "X" else 1))
            world = Matrix.Rotation(angle, 3, axis)
            in_rig = to_rig.inverted() @ world @ to_rig
            rest = pb.bone.matrix_local.to_3x3()
            pb.rotation_quaternion = (rest.inverted() @ in_rig @ rest).to_quaternion()
    for md in list(body.modifiers):
        if md.type != "ARMATURE":
            body.modifiers.remove(md)
    bpy.context.view_layer.update()
    evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    posed = evaluated.to_mesh()
    for v, p in zip(body.data.vertices, posed.vertices):
        v.co = p.co.copy()
    evaluated.to_mesh_clear()
    for pb in rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()


def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    preview = {
        "skin": (0.85, 0.66, 0.49, 1), "hair": (0.25, 0.16, 0.09, 1), "primary": (0.25, 0.45, 0.85, 1),
        "secondary": (0.3, 0.24, 0.17, 1), "leather": (0.2, 0.13, 0.08, 1), "eyes": (0.08, 0.06, 0.06, 1),
    }
    mat.diffuse_color = preview.get(name, (0.6, 0.6, 0.6, 1))
    return mat


def build():
    bpy.ops.wm.open_mainfile(filepath=SOURCE, load_ui=False)
    body = bpy.data.objects[NAME]
    old_rig = bpy.data.objects[{"male": "MaleArm", "female": "FemaleArm"}[SEX]]

    # Remove the other body, camera and lamp; this file makes one.
    for name in ("HumanFemale", "FemaleArm", "HumanMale", "MaleArm", "Camera", "Lamp"):
        if name in (body.name, old_rig.name):
            continue
        if name in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)

    # 1. Rest pose, centred: feet on z=0, middle on x=0.
    for pb in old_rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    close_hands(body, old_rig)
    old = {b.name: (old_rig.matrix_world @ b.head_local, old_rig.matrix_world @ b.tail_local) for b in old_rig.data.bones}
    shift = Vector((-old["Back"][0].x, 0, 0))
    old = {k: (h + shift, t + shift) for k, (h, t) in old.items()}

    for md in list(body.modifiers):
        body.modifiers.remove(md)
    body.parent = None
    body.data.transform(body.matrix_world)
    body.matrix_world = Matrix.Identity(4)
    body.data.transform(Matrix.Translation(shift))
    min_z = min(v.co.z for v in body.data.vertices)
    body.data.transform(Matrix.Translation((0, 0, -min_z)))
    old = {k: (h - Vector((0, 0, min_z)), t - Vector((0, 0, min_z))) for k, (h, t) in old.items()}
    # Turn to face +Y: exported with forward -Z that is Unity's +Z, the way Unity models face.
    turn = Matrix.Rotation(math.pi, 4, "Z")
    body.data.transform(turn)
    old = {k: (turn @ h, turn @ t) for k, (h, t) in old.items()}
    bpy.data.objects.remove(old_rig, do_unlink=True)

    # 2. The new skeleton.
    top = max(v.co.z for v in body.data.vertices)
    hip_z = (old["Thigh.Left"][0].z + old["Thigh.Right"][0].z) / 2
    back_top, back_y = old["Back"][0].z, old["Back"][0].y
    neck_z = (back_top + old["Head"][0].z) / 2 + 0.03
    spine_z = hip_z + 0.12
    chest_z = spine_z + (back_top - spine_z) * 0.45
    bones = {
        "hips": ((0, back_y, hip_z), (0, back_y, spine_z), None),
        "spine": ((0, back_y, spine_z), (0, back_y, chest_z), "hips"),
        "chest": ((0, back_y, chest_z), (0, back_y, back_top), "spine"),
        "neck": ((0, back_y, back_top), (0, back_y, neck_z), "chest"),
        "head": ((0, back_y, neck_z), (0, back_y, top), "neck"),
    }
    for side, s in (("Left", "L"), ("Right", "R")):
        bones[f"upper_arm.{s}"] = (old[f"Upperarm.{side}"][0], old[f"Upperarm.{side}"][1], "chest")
        bones[f"forearm.{s}"] = (old[f"Forearm.{side}"][0], old[f"Forearm.{side}"][1], f"upper_arm.{s}")
        bones[f"hand.{s}"] = (old[f"Hand.{side}"][0], old[f"Fingers2.{side}"][1], f"forearm.{s}")
        bones[f"thigh.{s}"] = (old[f"Thigh.{side}"][0], old[f"Thigh.{side}"][1], "hips")
        bones[f"shin.{s}"] = (old[f"Shin.{side}"][0], old[f"Shin.{side}"][1], f"thigh.{s}")
        bones[f"foot.{s}"] = (old[f"Foot.{side}"][0], old[f"Foot.{side}"][1], f"shin.{s}")
        bones[f"toe.{s}"] = (old[f"Toes.{side}"][0], old[f"Toes.{side}"][1], f"foot.{s}")
        # Where held things go: the middle of the closed fist, pointing forward along the
        # channel the fingers make (a weapon's handle runs through here).
        along = (old[f"Fingers2.{side}"][1] - old[f"Hand.{side}"][0]).normalized()
        grip = old[f"Fingers1.{side}"][0] + along * GRIP_ALONG + Vector((0, 0, -GRIP_BELOW))
        bones[f"grip.{s}"] = (grip, grip + Vector((0, 0.08, 0)), f"hand.{s}")

    arm_data = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    with bpy.context.temp_override(active_object=rig, object=rig, selected_objects=[rig]):
        bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, parent) in bones.items():
        eb = arm_data.edit_bones.new(name)
        eb.head, eb.tail, eb.roll = Vector(head), Vector(tail), 0.0
    for name, (head, tail, parent) in bones.items():
        if parent:
            eb = arm_data.edit_bones[name]
            eb.parent = arm_data.edit_bones[parent]
            eb.use_connect = (eb.head - eb.parent.tail).length < 1e-4
    with bpy.context.temp_override(active_object=rig, object=rig, selected_objects=[rig]):
        bpy.ops.object.mode_set(mode="OBJECT")

    # Weights: old groups onto new bones; the back splits into spine/chest and the head group
    # gives its lowest part to the neck.
    mesh = body.data
    old_groups = {g.index: g.name for g in body.vertex_groups}
    weights = []
    for v in mesh.vertices:
        w = {}
        for ge in v.groups:
            name = old_groups[ge.group]
            if ge.weight <= 0:
                continue
            if name == "Back":
                t = smoothstep(chest_z - 0.08, chest_z + 0.08, v.co.z)
                w["spine"] = w.get("spine", 0) + ge.weight * (1 - t)
                w["chest"] = w.get("chest", 0) + ge.weight * t
            elif name == "Head":
                t = smoothstep(back_top + 0.01, neck_z + 0.03, v.co.z)
                w["neck"] = w.get("neck", 0) + ge.weight * (1 - t)
                w["head"] = w.get("head", 0) + ge.weight * t
            elif name in GROUP_MAP:
                b = GROUP_MAP[name]
                w[b] = w.get(b, 0) + ge.weight
        total = sum(w.values()) or 1.0
        weights.append({k: x / total for k, x in w.items()})
    for g in list(body.vertex_groups):
        body.vertex_groups.remove(g)
    groups = {name: body.vertex_groups.new(name=name) for name in bones}
    for i, w in enumerate(weights):
        for b, x in w.items():
            if x > 0.001:
                groups[b].add([i], x, "REPLACE")

    # A slightly big head reads better from the game's camera (and is the Old School look).
    pivot = Vector((0, back_y, neck_z))
    head_index = body.vertex_groups["head"].index
    for v in mesh.vertices:
        w = next((ge.weight for ge in v.groups if ge.group == head_index), 0.0)
        if w > 0:
            v.co = pivot + (v.co - pivot) * (1 + (HEAD_SCALE - 1) * w)
    head_vs = [v for v in mesh.vertices
               if next((ge.weight for ge in v.groups if ge.group == head_index), 0.0) > 0.5]
    mid_y = (min(v.co.y for v in head_vs) + max(v.co.y for v in head_vs)) / 2
    for v in mesh.vertices:
        w = next((ge.weight for ge in v.groups if ge.group == head_index), 0.0)
        if w > 0:
            v.co.y = mid_y + (v.co.y - mid_y) * (1 + (HEAD_DEPTH - 1) * w)
    top = max(v.co.z for v in mesh.vertices)

    # Smooth shading, as the reference lights its people: each face keeps one colour, and the
    # facets come from the low triangle count, not from hard edges.
    for p in mesh.polygons:
        p.use_smooth = True

    # 3 + 4. Split into kit parts by each face's strongest bone.
    group_names = {g.index: g.name for g in body.vertex_groups}
    face_part = []
    for p in mesh.polygons:
        score = {}
        for vi in p.vertices:
            for ge in mesh.vertices[vi].groups:
                b = group_names[ge.group]
                score[b] = score.get(b, 0) + ge.weight
        bone = max(score, key=score.get) if score else "chest"
        part = PART_OF_BONE[bone]
        # The old hip weights reach the navel; the shirt should come down to the waistband.
        if part == "legs" and p.center.z > hip_z + WAIST_ABOVE_HIP:
            part = "torso"
        face_part.append(part)

    parts = {}
    for part in CHANNEL_OF_PART:
        obj = body.copy()
        obj.data = body.data.copy()
        obj.name = obj.data.name = "body_" + part   # never the same name as a bone
        bpy.context.scene.collection.objects.link(obj)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if face_part[f.index] != part], context="FACES")
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.materials.clear()
        obj.data.materials.append(material(CHANNEL_OF_PART[part]))
        parts[part] = obj

    # 5. The face: eye strokes on the head, smooth shading, and a bare neck.
    head = parts["head"]
    head.data.materials.append(material("eyes"))
    bm = bmesh.new()
    if HEAD_BUILT:
        head_h = HEAD_HEIGHT * top
        head_cy = back_y + HEAD_FORWARD
        crown = top - CHIN * top + head_h
        make_head(bm, crown, head_h, head_cy)
    else:
        bm.from_mesh(head.data)
    if not HEAD_BUILT:
        for s in (-1, 1):
            h = top - neck_z
            cx, cz = EYE_CENTRE[0] * h, neck_z + EYE_CENTRE[1] * h
            outline = []
            for i in range(EYE_POINTS):
                a = 2 * math.pi * i / EYE_POINTS
                dx, dz = EYE_HALF_W * h * math.cos(a), EYE_HALF_H * h * math.sin(a)
                outline.append((s * (cx + dx), 0.3, cz + dz + dx * EYE_TILT))
            A.decal(bm, outline, (0, 1, 0), mat=1)
    # The neck: a short tube from inside the collar up into the head, so it shows above the shirt.
    neck_rings = []
    if HEAD_BUILT:
        NECK_RINGS = ()
    else:
        NECK_RINGS = ((back_top - 0.02, 0.056), (neck_z + 0.05, 0.052))
    for z, r in NECK_RINGS:
        neck_rings.append([bm.verts.new((r * math.cos(a), back_y + 0.01 + r * math.sin(a), z))
                           for a in [2 * math.pi * i / 8 for i in range(8)]])
    for i in range(8 if neck_rings else 0):
        j = (i + 1) % 8
        bm.faces.new((neck_rings[0][i], neck_rings[0][j], neck_rings[1][j], neck_rings[1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f.material_index == 0])
    bm.to_mesh(head.data)
    bm.free()
    neck_g, head_g = head.vertex_groups["neck"], head.vertex_groups["head"]
    for v in head.data.vertices:
        if v.groups and not HEAD_BUILT:
            continue                      # the source's own vertices keep their weights
        t = smoothstep(back_top + 0.01, neck_z + 0.03, v.co.z)
        if t < 0.999:
            neck_g.add([v.index], 1 - t, "REPLACE")
        if t > 0.001:
            head_g.add([v.index], t, "REPLACE")
    if not HEAD_BUILT:
        for p in head.data.polygons:
            p.use_smooth = True
    torso = parts["torso"]
    bm = bmesh.new()
    bm.from_mesh(torso.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces
                               if f.calc_center_median().z > back_top + 0.03
                               and abs(f.calc_center_median().x) < 0.09], context="FACES")
    bm.to_mesh(torso.data)
    bm.free()

    # 6. Short hair: the top and back of the head, standing off the skull.
    hair = head.copy()
    hair.data = head.data.copy()
    hair.name = hair.data.name = "hair_short"
    bpy.context.scene.collection.objects.link(hair)
    bm = bmesh.new()
    if HEAD_BUILT:
        make_hair(bm, crown, head_h, head_cy)
    else:
        bm.from_mesh(hair.data)
        bm.faces.ensure_lookup_table()
        brow = neck_z + (top - neck_z) * 0.62
        nape = neck_z + (top - neck_z) * 0.2

        def keep(f):
            c = f.calc_center_median()
            if f.material_index != 0:
                return False                  # not the eyes
            if c.z > brow:
                return True
            return c.z > nape and c.y < back_y + 0.02   # behind the ears (front is +Y)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep(f)], context="FACES")
        bm.normal_update()
        # Thick over the skull, thinning to HAIR_EDGE at the hairline, so the hair meets the face
        # without a step all the way round (the step is what makes short hair read as a helmet).
        edge = {v for v in bm.verts if v.is_boundary}
        ring1 = {e.other_vert(v) for v in edge for e in v.link_edges} - edge
        for v in bm.verts:
            if HAIR_EDGE is None or (v not in edge and v not in ring1):
                d = HAIR_THICK
            else:
                d = HAIR_EDGE if v in edge else (HAIR_EDGE + HAIR_THICK) / 2
            v.co += v.normal * d
    bm.to_mesh(hair.data)
    bm.free()
    hair.data.materials.clear()
    hair.data.materials.append(material("hair"))
    for p in hair.data.polygons:
        p.use_smooth = True
        p.material_index = 0
    hair.vertex_groups.clear()
    hg = hair.vertex_groups.new(name="head")
    hg.add(list(range(len(hair.data.vertices))), 1.0, "REPLACE")

    bpy.data.objects.remove(body, do_unlink=True)

    # Skin every part to the rig.
    for obj in list(parts.values()) + [hair]:
        obj.parent = rig
        md = obj.modifiers.new("Armature", "ARMATURE")
        md.object = rig
        # Drop empty groups so each part only lists bones it uses.
        names = {g.index: g.name for g in obj.vertex_groups}
        used = {names[ge.group] for v in obj.data.vertices for ge in v.groups if ge.weight > 0.001}
        for name in [g.name for g in obj.vertex_groups if g.name not in used]:
            obj.vertex_groups.remove(obj.vertex_groups[name])

    if PREVIEW:
        return {"preview": NAME, "height": round(top, 3), "neck_z": round(neck_z, 3), "back_y": round(back_y, 3)}
    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [rig] + list(parts.values()) + [hair]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    exported = [rig] + list(parts.values()) + [hair]
    with bpy.context.temp_override(active_object=rig, object=rig, selected_objects=exported):
        bpy.ops.export_scene.fbx(
            filepath=EXPORT, use_selection=True, object_types={"ARMATURE", "MESH"},
            apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
            add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
            mesh_smooth_type="FACE", use_mesh_modifiers=False, bake_space_transform=True,
        )
    bpy.ops.wm.save_as_mainfile(filepath=BUILT_BLEND)
    return {
        "export": EXPORT,
        "parts": {k: len(v.data.polygons) for k, v in parts.items()},
        "hair": len(hair.data.polygons),
        "bones": list(bones),
        "height": round(top, 3),
    }


result = build()
