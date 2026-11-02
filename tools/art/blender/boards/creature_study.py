"""The creature method study (2026-10-28): four very different creatures, each built from one skeleton
spec three ways (artlib/creatures.py: A rigid segments, B a Skin shell, C hybrid), with the spec
drawn as a stick figure above them, so the owner can pick the method the cast is then built with.

Proportions are numbers, taken from the real animal (a grey wolf, a grey heron, a wolf spider scaled
up to a hound's size) and pushed for reading at game distance: heads a little bigger, legs a little
thicker. The goblin is fantasy: a third of a man's height lower, a head a quarter of its height,
hands to its knees.

It builds in its own scene, "Creature study", so other boards in the file are left alone; it
exports nothing.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\boards\\creature_study.py")
"""
import os
import sys

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards", "buildings")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
import artlib.creatures as C  # noqa: E402

# ---- the four specs (f forward, s to its left, u up; w half-width, h half-height) ----------------

BODY, PALE, DARK = 0, 1, 2
WOLF = dict(
    name="Wolf", colours=["Ashgrey", "Dust", "Charcoal"], measure={"length": 1.85, "width": 0.38, "height": 0.99},
    joints={
        "rump": (-0.55, 0, 0.64, 0.07, 0.07, BODY), "hips": (-0.42, 0, 0.64, 0.13, 0.15, BODY),
        "waist": (-0.24, 0, 0.63, 0.13, 0.14, BODY), "ribs": (-0.04, 0, 0.6, 0.16, 0.21, BODY),
        "chest": (0.16, 0, 0.6, 0.17, 0.24, BODY), "neck": (0.31, 0, 0.74, 0.14, 0.16, BODY),
        "skull": (0.43, 0, 0.79, 0.13, 0.12, BODY), "head": (0.54, 0, 0.76, 0.115, 0.1, BODY),
        "muzzle": (0.7, 0, 0.7, 0.065, 0.06, PALE), "nose": (0.8, 0, 0.69, 0.03, 0.03, DARK),
        "tail1": (-0.7, 0, 0.58, 0.05, 0.05, BODY), "tail2": (-0.88, 0, 0.45, 0.065, 0.065, BODY), "tail3": (-1.0, 0, 0.36, 0, 0, DARK),
        "shoulder.L": (0.2, 0.11, 0.55, 0.1, 0.1, BODY), "elbow.L": (0.12, 0.12, 0.36, 0.065, 0.065, BODY),
        "wrist.L": (0.16, 0.11, 0.12, 0.042, 0.042, BODY), "paw.L": (0.22, 0.11, 0.03, 0.05, 0.05, DARK),
        "hip.L": (-0.42, 0.11, 0.56, 0.12, 0.12, BODY), "stifle.L": (-0.3, 0.12, 0.36, 0.075, 0.075, BODY),
        "hock.L": (-0.47, 0.11, 0.17, 0.045, 0.045, BODY), "hpaw.L": (-0.43, 0.11, 0.03, 0.05, 0.05, DARK),
    },
    chains=[["rump", "hips", "waist", "ribs", "chest", "neck", "skull", "head", "muzzle", "nose"],
            ["rump", "tail1", "tail2", "tail3"]],
    bones=[("chest", "shoulder.L"), ("shoulder.L", "elbow.L"), ("elbow.L", "wrist.L"), ("wrist.L", "paw.L"),
           ("hips", "hip.L"), ("hip.L", "stifle.L"), ("stifle.L", "hock.L"), ("hock.L", "hpaw.L")],
    spikes=[((0.45, 0.065, 0.84), (0.42, 0.095, 0.99), 0.05, BODY, 3)],
    paint=[((0.6, 0.09, 0.79), (0.5, 1.0, 0.3), DARK, 0.4)],
)

BILL = 2
HERON = dict(
    name="Heron", colours=["Ashgrey", "Linen", "Ochre", "Charcoal"], measure={"length": 0.82, "width": 0.3, "height": 1.06},
    joints={
        "tailtip": (-0.34, 0, 0.56, 0, 0, BODY), "tail": (-0.26, 0, 0.58, 0.07, 0.035, BODY),
        "back": (-0.1, 0, 0.62, 0.12, 0.1, BODY), "breast": (0.07, 0, 0.62, 0.105, 0.11, PALE),
        "neck0": (0.13, 0, 0.72, 0.06, 0.06, PALE), "neck1": (0.09, 0, 0.84, 0.045, 0.045, PALE),
        "neck2": (0.15, 0, 0.96, 0.04, 0.04, PALE), "head": (0.22, 0, 1.0, 0.045, 0.05, PALE),
        "bill0": (0.28, 0, 1.0, 0.022, 0.028, BILL), "bill": (0.44, 0, 0.98, 0, 0, BILL),
        "wing0.L": (0.08, 0.1, 0.66, 0.025, 0.075, BODY), "wing1.L": (-0.18, 0.09, 0.62, 0.02, 0.06, BODY),
        "wing2.L": (-0.36, 0.05, 0.58, 0, 0, 3),
        "hip.L": (0.0, 0.05, 0.56, 0.03, 0.03, BODY), "heel.L": (-0.02, 0.06, 0.3, 0.018, 0.018, BILL),
        "foot.L": (0.0, 0.06, 0.03, 0.016, 0.016, BILL),
    },
    chains=[["tailtip", "tail", "back", "breast", "neck0", "neck1", "neck2", "head", "bill0", "bill"],
            ["wing0.L", "wing1.L", "wing2.L"]],
    bones=[("back", "hip.L"), ("hip.L", "heel.L"), ("heel.L", "foot.L")],
    spikes=[((0.0, 0.06, 0.02), (0.12, 0.08, 0.0), 0.012, BILL, 3), ((0.0, 0.06, 0.02), (0.05, 0.13, 0.0), 0.012, BILL, 3),
            ((0.0, 0.06, 0.02), (-0.07, 0.06, 0.0), 0.012, BILL, 3), ((0.19, 0, 1.03), (0.05, 0, 1.06), 0.015, 3, 3)],
    paint=[((0.235, 0.045, 1.01), (0.3, 1.0, 0.2), 3, 0.5)],
)

LEG, MARK, EYE = 1, 2, 3
SPIDER = dict(
    name="Spider", colours=["Umber", "Sable", "Ochre", "BloodSheen"], measure={"length": 1.45, "width": 1.6, "height": 0.67},
    joints={
        "front": (0.4, 0, 0.31, 0, 0, BODY), "head": (0.32, 0, 0.33, 0.09, 0.07, BODY),
        "ceph": (0.17, 0, 0.36, 0.16, 0.1, BODY), "waist": (0.03, 0, 0.37, 0.06, 0.06, BODY),
        "abd0": (-0.08, 0, 0.42, 0.18, 0.16, BODY), "abd1": (-0.28, 0, 0.46, 0.25, 0.21, BODY),
        "abd2": (-0.48, 0, 0.42, 0.17, 0.15, BODY), "spin": (-0.58, 0, 0.36, 0, 0, BODY),
        **{"leg%da.L" % i: (f, 0.1, 0.35, 0.035, 0.035, LEG) for i, f in enumerate((0.27, 0.2, 0.13, 0.06))},
        **{"leg%dk.L" % i: (f, s, 0.62, 0.03, 0.03, LEG) for i, (f, s) in enumerate(((0.48, 0.36), (0.26, 0.42), (0.02, 0.42), (-0.2, 0.38)))},
        **{"leg%dx.L" % i: (f, s, 0.25, 0.02, 0.02, LEG) for i, (f, s) in enumerate(((0.7, 0.55), (0.36, 0.68), (-0.06, 0.68), (-0.48, 0.58)))},
        **{"leg%df.L" % i: (f, s, 0.0, 0.008, 0.008, LEG) for i, (f, s) in enumerate(((0.8, 0.62), (0.4, 0.78), (-0.08, 0.78), (-0.6, 0.66)))},
    },
    chains=[["spin", "abd2", "abd1", "abd0", "waist", "ceph", "head", "front"]],
    bones=[b for i in range(4) for b in (("ceph", "leg%da.L" % i), ("leg%da.L" % i, "leg%dk.L" % i),
                                         ("leg%dk.L" % i, "leg%dx.L" % i), ("leg%dx.L" % i, "leg%df.L" % i))],
    spikes=[((0.36, 0.04, 0.3), (0.42, 0.03, 0.17), 0.025, LEG, 3)],
    paint=[((0.39, 0.03, 0.36), (1.0, 0.2, 0.4), EYE, 0.35), ((0.37, 0.06, 0.37), (1.0, 0.5, 0.5), EYE, 0.3),
           ((-0.28, 0, 0.67), (0, 0, 1), MARK, 1.0), ((-0.42, 0, 0.6), (-0.3, 0, 1), MARK, 0.8)],
)

SKIN, RAG, BELT, GEYE, MOUTH = range(5)
GOBLIN = dict(
    name="Goblin", colours=["Olive", "Hide", "Umber", "Sulphur", "Charcoal"], measure={"length": 0.45, "width": 0.66, "height": 1.15},
    joints={
        "pelvis": (0.0, 0, 0.48, 0.14, 0.1, RAG), "belly": (0.04, 0, 0.62, 0.17, 0.15, RAG),
        "chest": (0.03, 0, 0.78, 0.16, 0.11, SKIN), "neck": (0.09, 0, 0.87, 0.07, 0.07, SKIN),
        "head": (0.13, 0, 0.99, 0.13, 0.13, SKIN), "crown": (0.1, 0, 1.1, 0.1, 0.1, SKIN), "top": (0.08, 0, 1.15, 0, 0, SKIN),
        "hip.L": (0.0, 0.08, 0.47, 0.075, 0.075, SKIN), "knee.L": (0.04, 0.13, 0.27, 0.06, 0.06, SKIN),
        "ankle.L": (-0.01, 0.1, 0.07, 0.045, 0.045, SKIN), "toe.L": (0.14, 0.11, 0.03, 0.04, 0.04, SKIN),
        "shoulder.L": (0.04, 0.16, 0.8, 0.055, 0.055, SKIN), "elbow.L": (0.02, 0.24, 0.58, 0.04, 0.04, SKIN),
        "wrist.L": (0.1, 0.24, 0.38, 0.035, 0.035, SKIN), "hand.L": (0.13, 0.23, 0.27, 0.055, 0.055, SKIN),
    },
    chains=[("up", ["pelvis", "belly", "chest", "neck", "head", "crown", "top"])],
    bones=[("pelvis", "hip.L"), ("hip.L", "knee.L"), ("knee.L", "ankle.L"), ("ankle.L", "toe.L"),
           ("chest", "shoulder.L"), ("shoulder.L", "elbow.L"), ("elbow.L", "wrist.L"), ("wrist.L", "hand.L")],
    spikes=[((0.1, 0.11, 1.02), (0.02, 0.32, 1.1), 0.055, SKIN, 3), ((0.22, 0, 1.0), (0.33, 0, 0.94), 0.035, SKIN, 4)],
    paint=[((0.23, 0.05, 1.03), (1.0, 0.4, 0.2), GEYE, 0.45), ((0.25, 0, 0.93), (1.0, 0, 0), MOUTH, 0.5)],
)

# ---- the second round: proportions measured off the 2004 game's wolf and goblin (tools/research/
# creature_measure.py; numbers only, as shares of height), our own faceted shapes built to them ----
J = C.J
H = 1.0                                     # the wolf's height to the top of its head
WOLF2 = dict(
    name="Wolf2", colours=["Ashgrey", "Dust", "Charcoal"], limb_sides=4,
    measure={"length": 1.5, "width": 0.46, "height": 1.12},
    joints={
        # the body: the chest stands highest and deepest, the back falls to the hips, the belly tucks
        "rump": J(-0.5, 0, 0.64, 0.05, 0.05, 0.05, BODY), "hips": J(-0.38, 0, 0.65, 0.21, 0.14, 0.15, BODY),
        "loin": J(-0.22, 0, 0.62, 0.18, 0.16, 0.18, BODY), "ribs": J(-0.02, 0, 0.63, 0.2, 0.22, 0.22, BODY),
        "chest": J(0.2, 0, 0.7, 0.22, 0.26, 0.27, BODY, wt=0.55, wb=0.6), "breast": J(0.33, 0, 0.73, 0.13, 0.15, 0.2, BODY),
        # the head on its own: a neck sunk in the chest, a broad flat skull, a step down to the muzzle
        "neckbase": J(0.24, 0, 0.82, 0.12, 0.1, 0.12, BODY), "skull": J(0.42, 0, 0.9, 0.13, 0.08, 0.1, BODY, wt=0.8),
        "cheek": J(0.52, 0, 0.87, 0.11, 0.06, 0.09, BODY, wt=0.7), "muzzle": J(0.64, 0, 0.84, 0.055, 0.04, 0.05, BODY, wt=0.8),
        "nose": J(0.7, 0, 0.84, 0.035, 0.03, 0.035, DARK),
        "tail0": J(-0.5, 0, 0.68, 0.045, 0.045, 0.045, BODY), "tail1": J(-0.64, 0, 0.56, 0.075, 0.07, 0.07, BODY),
        "tail2": J(-0.76, 0, 0.38, 0, 0, 0, BODY),
        # legs: four-sided segments under the mass
        "shoulder.L": (0.24, 0.13, 0.62, 0.075, 0.075, BODY), "elbow.L": (0.18, 0.14, 0.38, 0.05, 0.05, BODY),
        "wrist.L": (0.22, 0.13, 0.12, 0.04, 0.04, BODY), "paw.L": (0.27, 0.13, 0.03, 0.05, 0.05, DARK),
        "hip.L": (-0.36, 0.13, 0.62, 0.09, 0.09, BODY), "stifle.L": (-0.26, 0.14, 0.4, 0.06, 0.06, BODY),
        "hock.L": (-0.42, 0.13, 0.17, 0.04, 0.04, BODY), "hpaw.L": (-0.38, 0.13, 0.03, 0.05, 0.05, DARK),
    },
    chains=[["rump", "hips", "loin", "ribs", "chest", "breast"], ["neckbase", "skull", "cheek", "muzzle", "nose"],
            ["tail0", "tail1", "tail2"]],
    bones=[("chest", "shoulder.L"), ("shoulder.L", "elbow.L"), ("elbow.L", "wrist.L"), ("wrist.L", "paw.L"),
           ("hips", "hip.L"), ("hip.L", "stifle.L"), ("stifle.L", "hock.L"), ("hock.L", "hpaw.L")],
    spikes=[((0.43, 0.075, 0.96), (0.4, 0.1, 1.12), 0.055, BODY, 3)],
    paint=[((0.53, 0.085, 0.9), (0.6, 1.0, 0.4), DARK, 0.4)],
)

GOBLIN2 = dict(
    name="Goblin2", colours=["Olive", "Hide", "Umber", "Sulphur", "Charcoal"], limb_sides=4,
    measure={"length": 0.62, "width": 0.72, "height": 1.2},
    joints={
        # the trunk, upright (top = front): a pot belly, the back hunched over it
        "pelvis": J(0.0, 0, 0.5, 0.16, 0.1, 0.1, RAG), "belly": J(0.04, 0, 0.64, 0.19, 0.17, 0.08, RAG),
        "chest": J(0.05, 0, 0.8, 0.18, 0.1, 0.14, SKIN), "shoulders": J(0.09, 0, 0.89, 0.2, 0.08, 0.13, SKIN, wt=0.7, wb=0.6),
        # the head on its own, held forward: a third of its height, wider than the shoulders
        "neck": J(0.12, 0, 0.88, 0.08, 0.06, 0.06, SKIN), "jaw": J(0.22, 0, 0.94, 0.15, 0.13, 0.1, SKIN, wt=0.8, wb=0.6),
        "brow": J(0.22, 0, 1.07, 0.17, 0.15, 0.13, SKIN, wt=0.9, wb=0.8), "crown": J(0.18, 0, 1.17, 0.12, 0.1, 0.1, SKIN),
        "top": J(0.16, 0, 1.21, 0, 0, 0, SKIN),
        # short bowed legs, big feet
        "hip.L": (0.0, 0.1, 0.5, 0.08, 0.08, SKIN), "knee.L": (0.1, 0.15, 0.29, 0.065, 0.065, SKIN),
        "ankle.L": (-0.02, 0.12, 0.08, 0.05, 0.05, SKIN), "toe.L": (0.16, 0.13, 0.03, 0.055, 0.055, SKIN),
        # long arms, hands at the hips
        "shoulder.L": (0.08, 0.21, 0.86, 0.065, 0.065, SKIN), "elbow.L": (0.04, 0.27, 0.69, 0.05, 0.05, SKIN),
        "wrist.L": (0.13, 0.27, 0.56, 0.045, 0.045, SKIN), "hand.L": (0.17, 0.26, 0.49, 0.07, 0.07, SKIN),
    },
    chains=[("up", ["pelvis", "belly", "chest", "shoulders"]), ("up", ["neck", "jaw", "brow", "crown", "top"])],
    bones=[("pelvis", "hip.L"), ("hip.L", "knee.L"), ("knee.L", "ankle.L"), ("ankle.L", "toe.L"),
           ("chest", "shoulder.L"), ("shoulder.L", "elbow.L"), ("elbow.L", "wrist.L"), ("wrist.L", "hand.L")],
    spikes=[((0.2, 0.15, 1.08), (0.12, 0.38, 1.17), 0.065, SKIN, 3), ((0.36, 0, 1.03), (0.5, 0, 0.96), 0.045, SKIN, 4)],
    paint=[((0.37, 0.07, 1.09), (1.0, 0.4, 0.2), GEYE, 0.45), ((0.35, 0, 0.93), (1.0, 0, 0), MOUTH, 0.5)],
)

CREATURES = [HERON, WOLF, SPIDER, GOBLIN]
MEASURED = {1: WOLF2, 3: GOBLIN2}           # column: the measured spec built with A
ROWS = [("skeleton", "the spec"), ("A", "A  rigid segments"), ("B", "B  one skin shell"), ("C", "C  skin trunk, rigid limbs")]
SCENE = "Creature study"


def label(text, at, size, coll):
    cu = bpy.data.curves.new("Label", "FONT")
    cu.body = text
    cu.size = size
    ob = bpy.data.objects.new("Label " + text, cu)
    ob.location = at
    cu.materials.append(A.mat("Charcoal"))
    coll.objects.link(ob)


def board():
    scene = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
    for name in ("Creature study", "Creature study preview"):         # start clean: our own collections only
        c = bpy.data.collections.get(name)                            # (a player or references added by hand stay)
        if c:
            for o in list(c.all_objects):
                bpy.data.objects.remove(o, do_unlink=True)
            for child in list(c.children_recursive):
                bpy.data.collections.remove(child)
            bpy.data.collections.remove(c)
    bpy.context.window.scene = scene
    top = A.collection("Creature study")
    out = {}
    for r, (row, title) in enumerate(ROWS):
        coll = bpy.data.collections.new("Study " + row)
        top.children.link(coll)
        y = -r * 2.6
        label(title, (-3.2, y - 0.2, 0.01), 0.3, coll)
        for k, spec in enumerate(CREATURES):
            ob = C.skeleton(spec, coll=coll.name) if row == "skeleton" else C.build(spec, row, coll=coll.name)
            for c in list(ob.users_collection):
                if c is not coll:
                    c.objects.unlink(ob)
            if coll not in ob.users_collection:
                coll.objects.link(ob)
            ob.location = (k * 2.4, y, 0)
            if row != "skeleton":
                out["%s %s" % (spec["name"], row)] = {"tris": A.report([ob])["triangles"], **C.measure(ob, spec)}
    coll = bpy.data.collections.new("Study A measured")
    top.children.link(coll)
    y = -len(ROWS) * 2.6
    label("A  measured (2004 proportions)", (-3.2, y - 0.2, 0.01), 0.3, coll)
    for k, spec in MEASURED.items():
        ob = C.build(spec, "A", coll=coll.name)
        ob.location = (k * 2.4, y, 0)
        out["%s A" % spec["name"]] = {"tris": A.report([ob])["triangles"], **C.measure(ob, spec)}
    for k, spec in enumerate(CREATURES):
        label(spec["name"], (k * 2.4 - 0.4, 1.4, 0.01), 0.3, top)
    have_player = any(c.name.startswith("Player") for c in scene.collection.children)
    A.preview_rig(focus=(3.6, -3.9, 0.5), distance=13, pitch=40, yaw=-12, ground=12, human=not have_player, human_at=(-1.8, 0.0),
                  coll_name="Creature study preview")
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True
    return out


if __name__ in ("__main__", "<run_path>"):
    result = board()
