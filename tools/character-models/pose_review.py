"""
Posed copies of the review bodies, to judge garments and hair in motion: each body is copied
into a row of poses (relaxed, walking, reaching, arms up, crouching) beside it, one row per outfit
(rows behind each other, `ROW` apart), in the collection "Pose_Review". The copies share the originals' meshes (rebuilding a garment in place updates
them all); each has its own rig, so each holds its own pose. Re-run after adding a part or
changing what is worn: it deletes and rebuilds the collection.

Run inside Blender with BodyReview.blend open, through the Blender MCP: exec this file.
Optionally OUTFITS = {name: [garment names]} (default below) and POSES. Every copy wears the
body's head, arms and hands, less what its garments hide; garment names get the body's suffix (".001" for the female).

Poses are posing.py's (rotations per bone in the armature's own axes). Exec with __file__ set
to this file's path, so posing.py loads.
"""

import importlib.util
import os

import bpy

_spec = importlib.util.spec_from_file_location(
    "posing", os.path.join(os.path.dirname(os.path.abspath(__file__)), "posing.py"))
posing = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(posing)
R = posing.R

RIGS = {"male": ("Rig.004", 0.85, 1), "female": ("Rig.005", -0.85, -1)}   # rig, x, which way the row runs
BASE = {"male": ["body_head.003", "body_arms.003", "body_hands.003"],
        "female": ["body_head.004", "body_arms.004", "body_hands.004"]}
SUFFIX = {"male": "", "female": ".001"}
OUTFITS = globals().get("OUTFITS", {
    "plain": ["shirt_loose", "trousers_straight", "boots_simple"],
    "jerkin": ["shirt_long", "jerkin", "hose", "boots_tall"],
    "tunic": ["belted_tunic", "turnshoes"],
    "robe": ["robe", "turnshoes"],
    "plate": ["platebody", "platelegs", "knee_cops", "plate_pauldrons", "full_helm", "plate_boots"],
})
ROW = 1.6
ARMS_DOWN = {"upper_arm.L": R("Y", -78), "upper_arm.R": R("Y", 78)}
POSES = globals().get("POSES", {
    "relaxed": dict(ARMS_DOWN, **{"forearm.L": R("Y", -8), "forearm.R": R("Y", 8)}),
    "walk": posing.WALK,
    "reach": {"upper_arm.L": R("Z", -80), "upper_arm.R": R("Z", 80), "spine": R("X", -8)},
    "arms_up": {"upper_arm.L": R("Y", 70), "upper_arm.R": R("Y", -70),
                "forearm.L": R("Y", 25), "forearm.R": R("Y", -25)},
    "sit": posing.SIT,
    "crouch": posing.CROUCH,
    "kneel": posing.KNEEL,
    "kick": posing.KICK,
})
SPACING = 0.75


def build():
    col = bpy.data.collections.get("Pose_Review")
    if col:
        for o in list(col.all_objects):
            data = o.data if o.type == "ARMATURE" else None
            bpy.data.objects.remove(o)
            if data and data.users == 0:
                bpy.data.armatures.remove(data)
        for c in list(col.children):
            bpy.data.collections.remove(c)
    else:
        col = bpy.data.collections.new("Pose_Review")
        bpy.context.scene.collection.children.link(col)
    made = []
    for sex, (rig_name, x0, way) in RIGS.items():
        src = bpy.data.objects[rig_name]
        for row, (outfit, garments) in enumerate(OUTFITS.items()):
            wear = ["wear_" + g + SUFFIX[sex] for g in garments]
            hidden = {r for g in wear for r in bpy.data.objects[g].get("hides", "").split(",") if r}
            worn = [n for n in BASE[sex] if n.split("_")[1].split(".")[0] not in hidden] + wear   # body_<role>
            for k, (pose_name, pose) in enumerate(POSES.items(), start=1):
                tag = f"{sex}_{outfit}_{pose_name}"
                sub = bpy.data.collections.new(tag)
                col.children.link(sub)
                rig = src.copy()
                rig.data = src.data.copy()
                rig.data.pose_position = "POSE"
                rig.data.display_type = "STICK"
                rig.name = tag + "_rig"
                rig.location.x = x0 + way * SPACING * k
                rig.location.y = -ROW * row
                sub.objects.link(rig)
                for n in worn:
                    o = bpy.data.objects[n].copy()       # shares the mesh
                    o.name = f"{tag}_{n}"
                    o.parent = rig
                    o.hide_viewport = False
                    for m in o.modifiers:
                        if m.type == "ARMATURE":
                            m.object = rig
                    sub.objects.link(o)
                    o.hide_set(False)
                posing.set_pose(rig, pose)
                rig.hide_set(True)
                made.append(tag)
    return made


POSE_RESULT = build()
