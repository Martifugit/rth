"""The harbour kit (docs/harbour-kit.md): its board, a quay along a bank with jetties, boats and a moored
ship in front of it, and its export. The parts are in structures.py (jetties, quays, stairs, the
dressing) and vessels.py (boats, the barge, the ship); shared pieces and the walk layouts in
common.py. Generic: a port's identity comes later as a set on top, as the caves kit does.

Walk layouts (common.Walk) are written to world/walk/<Name>.json and drawn on the board in the Walk
collection: green tiles to stand on, red blocked edges, blue climbs. Hide that collection to judge
the models.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\harbour\\kit.py")
"""
import math
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
for name in ("rth_art", "basin", "common", "structures", "vessels"):
    sys.modules.pop(name, None)
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import common as C  # noqa: E402
import structures as S  # noqa: E402
import vessels as V  # noqa: E402

A, N = C.A, C.N
A._palette = None


def board_ground():
    """The board: the water's bed, the water at z 0, and a bank behind the quay from y 13.5."""
    bm = bmesh.new()
    A.box(bm, (-6, -16, C.BED - 0.1), (46, 24, C.BED), 0)
    water = [bm.verts.new(p) for p in ((-6, -16, 0), (46, -16, 0), (46, 13.5, 0), (-6, 13.5, 0))]
    bm.faces.new(water).material_index = 1
    A.box(bm, (-6, 13.5, C.BED), (46, 24, C.QUAY - 0.02), 2)
    return N.make("Board", bm, ["Mud", "Mere", "Grass"], coll="Preview")


def place(ob, x, y, rot=0):
    ob.location = (x, y, 0)
    ob.rotation_euler.z = math.radians(rot)
    return ob


def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Harbour", "Walk", "Preview"):
            bpy.data.collections.remove(c)
    board_ground()
    o = {}
    o["quay"] = place(S.quay_edge(40, name="QuayEdge"), -4, 14)
    o["quay_steps"] = place(S.quay_steps(), 22, 13)
    o["stairs"] = place(S.jetty_stairs(), 4, 13, rot=180)
    o["jetty"] = place(S.jetty_span(1, 8), 4, 11, rot=180)
    o["jetty_head"] = place(S.jetty_head(), 5, 2, rot=180)
    o["pontoon"] = place(S.pontoon(), 7, 1, rot=180)
    o["jetty_2"] = place(S.jetty_span(2, 6, name="JettySpan2x6"), 12, 13, rot=180)
    o["slipway"] = place(S.slipway(), -3, 13.5)
    o["rowing_boat"] = place(V.rowing_boat(), 2.0, 7.5, rot=180)
    o["rowing_boat_2"] = place(V.rowing_boat(), -1.6, 10.0, rot=200)
    o["fishing_boat"] = place(V.fishing_boat(), 6.9, 8.0, rot=180)
    o["barge"] = place(V.barge(), 14.1, 6.0, rot=180)
    o["ship"] = place(V.ship(), 30.0, 7.0, rot=90)
    o["gangplank"] = place(S.gangplank(rise=1.9, length=4), 30, 13, rot=180)
    o["bollard"] = place(S.bollard(), 20, 14.4)
    o["capstan"] = place(S.capstan(), 24, 15.5)
    o["crane"] = place(S.crane(), 37, 14.5)
    o["net_rack"] = place(S.net_rack(), 9, 16)
    o["lamp"] = place(S.harbour_lamp(), 8, 14.4)
    o["marker"] = place(S.channel_marker(), 22, -4)
    o["fish_crates"] = place(S.fish_crates(), 16, 15.2)
    o["lobster_pots"] = place(S.lobster_pots(), 2, 15.4)
    return o


def rig():
    A.preview_rig(focus=(18.0, 6.0, 1.0), distance=50.0, pitch=40, yaw=-20, ground=0, human=True, human_at=(4.0, 6.0))
    A.figures([(4.0, 12.0, C.JETTY), (30.0, 7.0, 2.0), (28.0, 14.6, C.QUAY)])
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


objs = build()
rig()
result = {k: A.report([v])["triangles"] for k, v in objs.items()}
