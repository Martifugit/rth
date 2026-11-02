"""
The earlier armour kit (tools/art/blender/armour/platebody.py: platebodies cut from the body's own
surface into overlapping plates with turned edges, platelegs, helms) built on the new bodies in
BodyReview.blend, as wearable pieces the posed copies (pose_review.py) can show.

platebody.py was written for the old kit bodies (HumanMale.fbx), which it imports and moves; here
its functions are run as they are, with `part` pointed at the new bodies' objects instead, and
nothing of the scene is moved or hidden. Each piece becomes "wear_pb_<piece>_<style>" (".001" for
the female), parented to the body's rig, skinned by platebody.py's own `bind` (helms rigidly on
the head), with "hides" set as a garment's is.

Exec inside Blender with __file__ set to this file's path; optionally PIECES = [(piece, style)].
PORT_RESULT lists what was made, with triangle counts.
"""
import os

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(HERE, "..", "art", "blender", "armour", "platebody.py")

# The new bodies' parts by the kit's names (build_wear.py's BODIES).
NEW = {
    "male": dict(rig="Rig.004", torso="torso_draft", arms="body_arms.003", legs="wear_trousers",
                 head="body_head.003", hands="body_hands.003", feet="wear_boots", suffix=""),
    "female": dict(rig="Rig.005", torso="torso_draft.001", arms="body_arms.004", legs="wear_trousers.001",
                   head="body_head.004", hands="body_hands.004", feet="wear_boots.001", suffix=".001"),
}
PIECES = globals().get("PIECES", [
    ("body", "coat"), ("body", "restrained"), ("body", "bold"),
    ("legs", "restrained"), ("legs", "bold"),
    ("head", "full"), ("head", "open"),
    ("head", "tower"), ("head", "drop"), ("head", "frostpeak"), ("head", "sallet"), ("head", "frogmouth"),
])


def kit_module():
    g = {"__file__": os.path.abspath(KIT), "__name__": "platebody_port", "result": None}
    exec(compile(open(KIT, encoding="utf-8").read(), KIT, "exec"), g)
    return g


def port(g, sex, piece, style):
    body = NEW[sex]
    rig = bpy.data.objects[body["rig"]]
    g["part"] = lambda r, name: bpy.data.objects.get(body.get(name, ""))
    g["kit"] = g["part"]
    A = g["A"]
    if piece == "head":
        bm = g["design_helm"](rig, style) if style in g["HELM_DESIGNS"] else g["helm"](rig, g["HELM_STYLES"][style], style)
        own = [] if style in g["HELM_DESIGNS"] else (g["HELM_STYLES"][style].get("hides") or [])
        hides = ["head"] if style in g["HELM_DESIGNS"] or "head" in own else []   # an open helm shows the face
    elif piece == "legs":
        bm = g["platelegs"](rig, g["LEG_STYLES"][style])
        hides = ["legs"]
    else:
        bm = g["coat"](rig)[0] if style == "coat" else g["plates"](rig, g["STYLES"][style])[0]
        hides = ["torso", "arms"]
    A.finish(bm)
    name = f"wear_pb_{piece}_{style}{body['suffix']}"
    old = bpy.data.objects.get(name)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for n in g["MATS"]:
        me.materials.append(A.mat(n))
    for p in me.polygons:
        p.use_smooth = False
    if old is not None:                           # rebuilt: posed copies are made again afterwards
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new(name, me)
    rig.users_collection[0].objects.link(ob)
    if piece == "head":
        g["bind_rigid"](ob, rig)
    else:
        g["bind"](ob, rig, hides)
    ob["hides"] = ",".join(hides)
    return ob


def run():
    g = kit_module()
    made = []
    for sex in NEW:
        for piece, style in PIECES:
            ob = port(g, sex, piece, style)
            made.append((ob.name, sum(len(p.vertices) - 2 for p in ob.data.polygons)))
    return made


PORT_RESULT = run()
