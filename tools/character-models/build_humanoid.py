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
  5. Adds short hair as its own part, and exports one FBX (skeleton + parts) into the Unity
     project. Also saves the built .blend beside this script for hand edits.

Naming contract with the client (client/Assets/Scripts/Characters/):
  - bones: hips, spine, chest, neck, head, upper_arm.L/R, forearm.L/R, hand.L/R,
           thigh.L/R, shin.L/R, foot.L/R, toe.L/R, and grip.L/R (no weights: the centre of each
           closed fist, where held things attach)
  - meshes: "body_<part>" for body kit (head, torso, arms, hands, legs, feet) and "hair_<style>"
            (a mesh may not share a name with a bone, or Unity can't map the Humanoid)
  - materials: the colour channel name
"""

import math
import os
import bpy
import bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\dev\rth\tools\character-models"
SOURCE = os.path.join(HERE, "source", "human.blend")
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
EXPORT = os.path.join(REPO, "client", "Assets", "Resources", "Characters", "HumanMale.fbx")
BUILT_BLEND = os.path.join(HERE, "HumanMale.blend")

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


HEAD_SCALE = 1.15          # how much bigger than the source the head is
WAIST_ABOVE_HIP = 0.07     # where the shirt meets the trousers, above the hip joints


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
        "secondary": (0.3, 0.24, 0.17, 1), "leather": (0.2, 0.13, 0.08, 1),
    }
    mat.diffuse_color = preview.get(name, (0.6, 0.6, 0.6, 1))
    return mat


def build():
    bpy.ops.wm.open_mainfile(filepath=SOURCE, load_ui=False)
    body = bpy.data.objects["HumanMale"]
    old_rig = bpy.data.objects["MaleArm"]

    # Remove the female, camera and lamp; this file makes the male.
    for name in ("HumanFemale", "FemaleArm", "Camera", "Lamp"):
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
    top = max(v.co.z for v in mesh.vertices)

    # Flat shading: chunky faces, like the rest of the world.
    for p in mesh.polygons:
        p.use_smooth = False

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

    # 5. Short hair: the top and back of the head, pushed out along the normals.
    head = parts["head"]
    hair = head.copy()
    hair.data = head.data.copy()
    hair.name = hair.data.name = "hair_short"
    bpy.context.scene.collection.objects.link(hair)
    bm = bmesh.new()
    bm.from_mesh(hair.data)
    bm.faces.ensure_lookup_table()
    brow = neck_z + (top - neck_z) * 0.62
    nape = neck_z + (top - neck_z) * 0.2

    def keep(f):
        c = f.calc_center_median()
        if c.z > brow:
            return True
        return c.z > nape and c.y < back_y + 0.02   # behind the ears (front is +Y)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep(f)], context="FACES")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.022
    bm.to_mesh(hair.data)
    bm.free()
    hair.data.materials.clear()
    hair.data.materials.append(material("hair"))
    for p in hair.data.polygons:
        p.use_smooth = False
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
