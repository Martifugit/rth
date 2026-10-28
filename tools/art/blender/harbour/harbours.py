"""Three harbours drawn with the composer (layout.py), side by side on one board, to show the kinds of
waterfront the kit makes from a drawing: a fishing harbour with finger jetties off a stone quay, a
merchant mole closing a basin with a ship berthed in it, and a ferry landing whose timber jetty
zig-zags out past a worn and broken stretch. Each harbour writes its walk layout to world/walk.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\harbour\\harbours.py")
"""
import math
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
for name in [m for m in sys.modules if m in ("rth_art", "common", "structures", "vessels", "layout", "styles")
             or m.split(".")[0] in ("artlib", "assets", "parts")]:
    sys.modules.pop(name, None)
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import common as C  # noqa: E402
import layout as Lay  # noqa: E402
import vessels as V  # noqa: E402

A, N = C.A, C.N
A._palette = None

# The drawings: rows from the north, one character a tile (legend in layout.py). The top rows are the
# quay; the land runs on behind them.
FISHING = [
    "SSSSSSSSSSSSSSSSSSSSSSSS",
    "SSbSSSSlSSSSSSSbSSSSlSSS",
    "...v.......v.......v....",
    "...J.......J.......J....",
    "...J.......J.......J....",
    "...J.......J......pJ....",
    "...J......JJJ......J....",
    "...J......JJJ......J....",
    "..pJp.....JlJ......JJJJ.",
    "..JJJ..............JJJp.",
    "..JJJ...................",
    "........................",
]

MOLE = [
    "SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS",
    "SSSSbSSSSSSSSbSSSSSSSSSSSSSSSSSbSS",
    "............................SS....",
    "............................SS....",
    "............................bS....",
    "............................SS>...",
    "............................SS....",
    "............................SS....",
    "..........^.................bS....",
    "...SSSSSSSSSSSSSSSSSSSSSSSSSSS....",
    "...lSSSbSSSSSSSbSSSSSSSbSSSSSS....",
    "..................................",
]

FERRY = [
    "SSSSSSSSSSSSSSSS",
    "SSSlSSSSSSSbSSSS",
    "....v...........",
    "....J...........",
    "....J...........",
    "....JJJJJJ......",
    ".........J......",
    ".........J......",
    ".........JJJJ...",
    "........pJJJJp..",
    "...........W....",
    "...........W....",
    "...........X....",
    "...........X....",
    "................",
]

HARBOURS = [("HarbourFishing", FISHING, 0), ("HarbourMole", MOLE, 30), ("HarbourFerry", FERRY, 70)]


def ground():
    """Water under everything, the bed under the water, and land behind each harbour's quay."""
    bm = bmesh.new()
    A.box(bm, (-4, -6, C.BED - 0.1), (90, 20, C.BED), 0)
    bm.faces.new([bm.verts.new(p) for p in ((-4, -6, 0), (90, -6, 0), (90, 20, 0), (-4, 20, 0))]).material_index = 1
    for name, rows, x0 in HARBOURS:
        top = len(rows) - 0.5                                       # the quay's north edge
        A.box(bm, (x0 - 0.5, top, C.BED), (x0 + len(rows[0]) - 0.5, top + 6, C.QUAY - 0.02), 2)
    return N.make("Board", bm, ["Mud", "Mere", "Grass"], coll="Preview")


def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Harbour", "Walk", "Preview"):
            bpy.data.collections.remove(c)
    ground()
    o = {}
    C.WEAR = 1                                  # lived-in: a broken board here, a chipped block there
    for name, rows, x0 in HARBOURS:
        ob = Lay.compose(name, rows)
        ob.location = (x0, 0, 0)
        o[name] = ob

    def berth(key, ob, x, y, rot):
        ob.location = (x, y, 0)
        ob.rotation_euler.z = math.radians(rot)
        o[key] = ob
    berth("fishing_boat", V.fishing_boat(), 5.6, 5.5, 180)
    berth("fishing_boat_2", V.fishing_boat(), 13.6, 4.8, 175)
    berth("rowing_boat", V.rowing_boat(), 1.5, 4.0, 185)
    berth("rowing_boat_2", V.rowing_boat(), 21.6, 5.5, 180)
    berth("ship", V.ship(), 30 + 16.0, 6.0, 90)
    berth("river_boat", V.barge(), 70 + 4.0, 6.4, 90)
    C.WEAR = 0
    return o


def rig():
    A.preview_rig(focus=(40.0, 5.0, 1.0), distance=70.0, pitch=40, yaw=-15, ground=0, human=True, human_at=(3.0, 7.0))
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


objs = build()
rig()
result = {k: A.report([v])["triangles"] for k, v in objs.items()}
