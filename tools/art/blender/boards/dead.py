r"""The dead board: the long way back through the realm of the dead, every model of the style "dead" put
together on dark ash and still water (the families assets/creatures/dead, lights/dead, dressing/dead and
ways/dead). It makes nothing of its own and exports nothing.

From the player at the landing's edge: the soul gate where the route begins, the causeway's slabs out
across the water, broken by stepping stones, soul lanterns lit and spent beside it, standing stones and
drowned posts, dead reeds at the edges, a shade drifting over the water, and at the far bank the
grasping hands, what others lost, and the walker waiting.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\dead.py")
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
from assets.creatures.dead import shade, walker  # noqa: E402
from assets.dressing.dead import dead_reeds, drowned_post, grasping_hands, lost_belongings, standing_stone  # noqa: E402
from assets.lights.dead import soul_lantern  # noqa: E402
from assets.ways.dead import causeway_slab, soul_gate, stepping_stone  # noqa: E402

X0, X1, Y0, Y1 = -8, 8, -4, 28          # the board's ground
WATER = (4, 20)                         # the water's near and far edge (y); ash either side
GROUND = "Lava"                         # the realm's dark ash (the region's DeadAsh is not a palette colour)
STILL = "Obsidian"                      # the still black water


def plane(name, x0, y0, x1, y1, z, colour):
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
    bm.faces.new(vs)
    return N.make(name, bm, [colour], coll="Preview")


def place(o, key, ob, at, rot=0.0):
    ob.name = key
    ob.location = at
    ob.rotation_euler.z = rot
    o[key] = ob


def build():
    A.reset()
    plane("Ground", X0, Y0, X1, Y1, 0.0, GROUND)
    plane("Water", X0, WATER[0], X1, WATER[1], 0.01, STILL)
    o = {}
    place(o, "SoulGate", soul_gate(), (0, 3.0, 0))
    for i, y in enumerate(range(5, 14)):                     # the causeway out across the water
        place(o, "CausewaySlab_%d" % i, causeway_slab(seed=1 + i), (0, y, 0))
    for i, (x, y) in enumerate(((0.25, 14.2), (-0.2, 15.3), (0.15, 16.4))):   # where it has sunk: stepping stones
        place(o, "SteppingStone_%d" % i, stepping_stone(seed=2 + i), (x, y, 0))
    for i, y in enumerate(range(17, 20)):
        place(o, "CausewaySlab_b%d" % i, causeway_slab(seed=20 + i), (0, y + 0.5, 0))
    place(o, "SoulLantern", soul_lantern(), (1.3, 7.0, 0))
    place(o, "SoulLanternSpent", soul_lantern(seed=2, spent=True), (-1.3, 12.0, 0))
    place(o, "SoulLantern_end", soul_lantern(seed=3), (-1.4, 22.3, 0))
    place(o, "StandingStoneA", standing_stone(), (-2.6, 2.2, 0))
    place(o, "StandingStoneB", standing_stone(seed=22, height=1.3, width=0.42, lean=(-0.12, 0.3),
                                              name="StandingStoneB"), (2.7, 2.6, 0))
    place(o, "StandingStoneA_far", standing_stone(seed=23), (3.2, 21.0, 0), rot=1.2)
    place(o, "DrownedPost", drowned_post(), (2.4, 10.0, 0))
    place(o, "DrownedPost_2", drowned_post(seed=32), (-2.6, 16.0, 0), rot=2.0)
    for i, (x, y) in enumerate(((-2.0, 4.6), (2.0, 5.1), (-3.2, 9.0), (1.8, 19.4), (-2.4, 19.6))):
        place(o, "DeadReeds_%d" % i, dead_reeds(seed=51 + i), (x, y, 0), rot=i * 1.3)
    place(o, "GraspingHands", grasping_hands(), (1.4, 21.2, 0))
    place(o, "LostBelongings", lost_belongings(), (-1.2, 21.0, 0), rot=0.6)
    place(o, "Shade", family.build("Shade", shade.build), (2.3, 13.5, 0), rot=-0.6)
    place(o, "Walker", family.build("Walker", walker.build), (0.4, 23.0, 0))
    return o


def rig():
    A.preview_rig(focus=(0.0, 12.0, 0.6), distance=26.0, pitch=40, yaw=-25, ground=0, human=True,
                  human_at=(-1.5, 1.0))
    world = bpy.context.scene.world
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.08, 0.1, 0.12, 1)   # the realm's dark
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


if __name__ in ("__main__", "<run_path>"):
    objs = build()
    rig()
    result = {k: A.report(family.parts(ob))["triangles"] for k, ob in objs.items()}
