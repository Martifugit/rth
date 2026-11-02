r"""The Landing board: the island where the dead wake (world/regions/realm_of_the_dead.json) laid out from
the region's own data, with the Landing's models in place of its boxes: the ground and its heights from the
region's tiles and terrain, the still water round it, the jetty with the ferryman on it and his boat
beside it, the shore lantern, the bench, the grave chest, the ash rocks, every candle piece the region
places, the soul gate and a standing stone, and the player where the dead wake, for scale. It makes
nothing of its own and exports nothing.

The game's north is Blender -Y (the export turns it into Unity +Z), so the board is the region turned
half round: east is -X, north -Y. A piece's "rot" (degrees, clockwise seen from above in Unity) is a
turn of -rot about Blender Z.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\dead_landing.py")
"""

import bmesh
import bpy
import json
import math
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
from assets.containers.dead import grave_chest  # noqa: E402
from assets.creatures.dead import ferryman  # noqa: E402
from assets.dressing.dead import standing_stone  # noqa: E402
from assets.furniture.dead import bench  # noqa: E402
from assets.lights import dead as lights  # noqa: E402
from assets.rocks.dead import ash_rock_a, ash_rock_b  # noqa: E402
from assets.vessels.dead import ferry_boat  # noqa: E402
from assets.ways.dead import DECK, jetty, soul_gate  # noqa: E402

REGION = os.path.join(A.REPO, "world", "regions", "realm_of_the_dead.json")
CENTRE = (8208.0, 8208.0)       # the region's middle: the board's origin
WATER = 0.0                     # the water's level, which the jetty's and the boat's origins sit at
SHORE = 0.08                    # the terrain's height (in tiles) at the water's edge
WAKE = (8208, 8206)             # where the dead wake: the player stands here
STONE = (8213, 8206)            # a standing stone on the rise east of the wake


def to_board(wx, wy):
    """A point of the world's tile grid (x east, y north) on the board."""
    return -(wx - CENTRE[0]), -(wy - CENTRE[1])


def flat_mat(name, hex_colour):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    h = hex_colour.lstrip("#")
    c = tuple(A._srgb_to_linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = c
    bsdf.inputs["Roughness"].default_value = 1.0 if name != "DeadWater" else 0.25
    m.diffuse_color = c
    return m


def expand(row):
    """A terrain row ("0*14 8 23 ...") as a list of numbers."""
    out = []
    for tok in row.split():
        v, _, n = tok.partition("*")
        out += [int(v)] * (int(n) if n else 1)
    return out


def ground(region):
    """The island's tiles with the terrain's corner heights, and the water's sheet under and round it."""
    ox, oy = region["origin"]
    w, h = region["size"]
    tiles = next(p for p in region["pieces"] if p["kind"] == "tiles")
    corners = [expand(r) for r in region["terrain"]["rows"]]           # from the north, (w + 1) by (h + 1)
    legend = tiles["legend"]

    def corner(cx, cy):                                                 # world corner -> board point with height
        z = corners[(oy + h) - cy][cx - ox] / 100.0 - SHORE
        x, y = to_board(cx, cy)
        return (x, y, max(z, WATER + 0.01))
    bm = bmesh.new()
    names = sorted({v["material"] for k, v in legend.items() if not v.get("water")})
    for r, row in enumerate(tiles["rows"]):
        ty = oy + h - 1 - r
        for c, ch in enumerate(row):
            entry = legend.get(ch, {})
            if entry.get("water") or ch == "=":
                continue
            tx = ox + c
            vs = [bm.verts.new(corner(*p)) for p in ((tx, ty), (tx + 1, ty), (tx + 1, ty + 1), (tx, ty + 1))]
            f = bm.faces.new(vs)
            f.material_index = names.index(entry["material"])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    me = bpy.data.meshes.new("Island")
    bm.to_mesh(me)
    bm.free()
    for n in names:
        me.materials.append(flat_mat(n, region["materials"][n]))
    ob = bpy.data.objects.new("Island", me)
    A.collection("Preview").objects.link(ob)
    bm = bmesh.new()
    x0, y0 = to_board(ox - 8, oy - 8)
    x1, y1 = to_board(ox + w + 8, oy + h + 8)
    bm.faces.new([bm.verts.new(p) for p in ((x0, y0, WATER), (x1, y0, WATER), (x1, y1, WATER), (x0, y1, WATER))])
    me = bpy.data.meshes.new("Water")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(flat_mat("DeadWater", region["materials"]["DeadWater"]))
    water = bpy.data.objects.new("Water", me)
    A.collection("Preview").objects.link(water)

    def height(wx, wy, deck=False):                                     # the ground's height under a world point
        tx, ty = int(math.floor(wx)), int(math.floor(wy))
        r = (oy + h - 1) - ty
        if not (0 <= r < h and 0 <= tx - ox < w):
            return WATER
        ch = tiles["rows"][r][tx - ox]
        if ch == "=" and deck:
            return WATER + DECK                                         # on the jetty's planks
        if legend.get(ch, {}).get("water") or ch == "=":
            return WATER
        zs = [corner(tx + dx, ty + dy)[2] for dx in (0, 1) for dy in (0, 1)]
        return sum(zs) / 4
    return height


def put(o, key, ob, wx, wy, z, rot=0.0):
    """Place a model at a world point (x east, y north), turned `rot` degrees as a region piece is."""
    ob.name = key
    x, y = to_board(wx, wy)
    ob.location = (x, y, z)
    ob.rotation_euler.z = -math.radians(rot)
    o[key] = ob


def build():
    A.reset()
    with open(REGION, encoding="utf-8") as f:
        region = json.load(f)
    height = ground(region)
    pieces = {p["id"]: p for p in region["pieces"]}
    o = {}

    def mid(at):
        return at[0] + 0.5, at[1] + 0.5
    jx, jy = mid(pieces["prop-jetty"]["at"])
    put(o, "Jetty", family.build("Jetty", jetty), jx, jy, WATER)
    bx, by = mid(pieces["prop-ferry-boat"]["at"])
    put(o, "FerryBoat", family.build("FerryBoat", ferry_boat), bx + 0.15, by + 0.3, WATER)
    fx, fy = mid(pieces["npc-ferryman"]["at"])
    put(o, "Ferryman", family.build("Ferryman", ferryman.build), fx, fy, WATER + DECK, rot=180)
    for key, builder in (("prop-shore-lantern", lights.shore_lantern), ("prop-bench", bench),
                         ("prop-grave-chest", grave_chest)):
        p = pieces[key]
        x, y = mid(p["at"])
        name = {"prop-shore-lantern": "ShoreLantern", "prop-bench": "Bench", "prop-grave-chest": "GraveChest"}[key]
        put(o, name, family.build(name, builder), x, y, height(x, y), rot=p.get("rot", 0))
    rocks = [p for p in region["pieces"] if p["kind"] == "rock"]
    for i, p in enumerate(rocks):
        x, y = mid(p["at"])
        builder = ash_rock_a if i % 2 == 0 else ash_rock_b
        ob = family.build("AshRock%s" % "AB"[i % 2], builder)
        ob.scale = (p.get("scale", 1.0) * 1.3,) * 3
        put(o, "AshRock_%d" % i, ob, x, y, height(x, y), rot=i * 70)
    gate = pieces["prop-soul-gate"]
    gx, gy = mid(gate["at"])
    put(o, "SoulGate", family.build("SoulGate", soul_gate), gx, gy, height(gx, gy))
    sx, sy = mid(STONE)
    put(o, "StandingStone", family.build("StandingStoneA", standing_stone), sx, sy, height(sx, sy), rot=200)
    # every candle the region places, one mesh per model shared by its copies
    models = {}
    for p in region["pieces"]:
        model = p.get("model", "")
        if "/Lights/Dead/" not in model:
            continue
        name = os.path.splitext(os.path.basename(model))[0]
        if name not in models:
            models[name] = family.build(name, lights.ASSETS[name])
            models[name].location.z = -50                               # the original, out of sight
        ob = models[name].copy()
        A.collection("Prop").objects.link(ob)
        x, y = mid(p["at"])
        z = WATER if name == "FloatingCandles" else height(x, y, deck=True)
        put(o, p["id"], ob, x, y, z, rot=p.get("rot", 0))
    for name, ob in models.items():
        bpy.data.objects.remove(ob, do_unlink=True)
    return o, height


def rig(height):
    wx, wy = WAKE
    px, py = to_board(wx + 0.5, wy + 0.5)
    fx, fy = to_board(8208.0, 8209.5)              # the camera looks north from the south, as the game's does
    A.preview_rig(focus=(fx, fy, 0.4), distance=19.0, pitch=40, yaw=160, ground=0, human=True,
                  human_at=(px, py))
    rig_ob = bpy.data.objects.get("Rig")
    if rig_ob is not None:
        rig_ob.location.z = height(wx + 0.5, wy + 0.5)
    world = bpy.context.scene.world
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.08, 0.09, 1)   # the realm's dark
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


if __name__ in ("__main__", "<run_path>"):
    objs, height = build()
    rig(height)
    result = {k: A.report(family.parts(ob))["triangles"] for k, ob in objs.items()
              if not k.startswith("candle-")}
