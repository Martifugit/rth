r"""The souls board: the four directions for the shades of the long way back side by side, beside the player
for scale, on the realm's dark ash, each labelled, for the owner to pick from. The old Shade stands at
the end of the row for comparison. It makes nothing of its own and exports nothing.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\dead_souls.py")
"""

import bmesh
import bpy
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
import artlib.nature as N  # noqa: E402
from assets.creatures.dead import shade, shade_burdened, shade_faded, shade_mourner, shade_wraith  # noqa: E402

GROUND = "Lava"                         # the realm's dark ash
SPACING = 3.0                           # between the variants along X
ROW = [                                 # (name, builder, label)
    ("ShadeFaded", shade_faded.build, "A  Faded soldier"),
    ("ShadeWraith", shade_wraith.build, "B  Wraith"),
    ("ShadeBurdened", shade_burdened.build, "C  Burdened"),
    ("ShadeMourner", shade_mourner.build, "D  Mourner"),
    ("Shade", shade.build, "old Shade"),
]


def plane(name, x0, y0, x1, y1, z, colour):
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
    bm.faces.new(vs)
    return N.make(name, bm, [colour], coll="Preview")


def label(text, at, size=0.28):
    """A flat label on the ground in front of a model, readable from the game camera."""
    cu = bpy.data.curves.new("Label_" + text, "FONT")
    cu.body = text
    cu.size = size
    cu.align_x = "CENTER"
    ob = bpy.data.objects.new("Label_" + text, cu)
    A.collection("Preview").objects.link(ob)
    ob.location = (at[0], at[1], 0.01)
    ob.data.materials.append(A.mat("BonePale"))
    return ob


def build():
    A.reset()
    n = len(ROW)
    plane("Ground", -3.5, -3.0, SPACING * n + 1.0, 4.0, 0.0, GROUND)
    out = {}
    for i, (name, builder, text) in enumerate(ROW):
        x = SPACING * i + 0.5
        ob = family.build(name, builder)
        ob.location = (x, 0.0, 0.0)
        label(text, (x, -1.6))
        out[name] = ob
    return out


def rig():
    n = len(ROW)
    mid = (SPACING * (n - 1)) / 2 + 0.5 - 1.0
    A.preview_rig(focus=(mid, 0.0, 0.9), distance=15.0, pitch=40, yaw=-12, ground=0, human=True,
                  human_at=(-1.8, 0.0))
    world = bpy.context.scene.world
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.08, 0.09, 1)   # the realm's dark
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


if __name__ in ("__main__", "<run_path>"):
    objs = build()
    rig()
    result = {k: A.report(family.parts(ob)) for k, ob in objs.items()}
