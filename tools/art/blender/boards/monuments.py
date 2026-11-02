"""The monuments board: Religion's ten tiers of monument (assets/monuments/cruor.py) as they would
stand in the world, each dressed for its tier, in two rows of five beside the player, with an
unraised site at the start. It builds nothing of its own and exports nothing: it shows how the
family's pieces and other families' dressing go together, the way a region's data will place them.

- Founts stand where layout(tier) puts them, turned toward their stand tiles (marked pale on the
  podium: where a player stands to use each): the rough fount for tiers 1-3, the carved for 4-6, the
  great for 7-10; one in three sated, as the node would be.
- The dressing grows with the tier: a frontier needle has candles, ribbons and bones left by the
  first pilgrims; a middle one offering bowls, kneelers, banners, a reliquary on its stand and a
  chain round it; the great ones braziers, a stele, runnels and the silver reliquary.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\boards\\monuments.py")
"""
import importlib
import math
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

M = importlib.import_module("assets.monuments.cruor")
DRESSING_COMMON = importlib.import_module("assets.dressing.common").ASSETS
LIGHTS = importlib.import_module("assets.lights.common").ASSETS


def put(coll, make, at, rot=0.0):
    ob = make()
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)
    ob.location = (ob.location.x + at[0], ob.location.y + at[1], ob.location.z + at[2])
    ob.rotation_euler.z += rot
    return ob


def label(text, at, coll):
    cu = bpy.data.curves.new("Label", "FONT")
    cu.body = text
    cu.size = 0.6
    ob = bpy.data.objects.new("Label " + text, cu)
    ob.location = at
    cu.materials.append(A.mat("Charcoal"))
    coll.objects.link(ob)


def mark(coll, x, y, z):
    """A pale square on a stand tile, where a player stands to use a fount (the board's review aid)."""
    import bmesh
    bm = bmesh.new()
    v = [bm.verts.new((x + dx, y + dy, z + 0.012)) for dx, dy in ((-0.42, -0.42), (0.42, -0.42), (0.42, 0.42), (-0.42, 0.42))]
    bm.faces.new(v)
    ob = A.mesh_from("StandTile", bm, ["BlockoutRing"], coll=coll.name)
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)


def dress(coll, tier, x, y, marks=True):
    """The tier's founts from layout(), its stand tiles marked, and its dressing round it, kept off
    the approaches."""
    t = M.TIERS[tier]
    lay = M.layout(tier)
    half = t["rings"][0] / 2
    grade = 1 if tier <= 3 else 2 if tier <= 6 else 3
    name = {1: "Fount", 2: "FountCarved", 3: "FountGreat"}[grade]
    for k, (fx, fy, fz, rot) in enumerate(lay["founts"]):
        sated = k % 3 == 2
        put(coll, M.ASSETS[name + ("Sated" if sated else "")], (x + fx, y + fy, fz), rot=math.radians(rot))
    if marks:
        for sx, sy, sz in lay["stand"]:
            mark(coll, x + sx, y + sy, sz)
    front = y - half - 1.0                                                   # the road side, off the podium
    if tier <= 2:
        put(coll, LIGHTS["Candles"], (x - 2.0, front, 0))
        put(coll, DRESSING_COMMON["RibbonPost"], (x + half + 1.0, y + 0.5, 0))
        put(coll, DRESSING_COMMON["BonePile"], (x + 2.0, front, 0))
        put(coll, M.ASSETS["PilgrimBanner"], (x - half - 1.0, y + 0.5, 0))
    elif tier <= 4:
        put(coll, M.ASSETS["OfferingBowls"], (x - 2.5, front - 0.5, 0))
        put(coll, M.ASSETS["Kneeler"], (x + 2.5, front - 0.5, 0))
        for s in (-1, 1):
            put(coll, M.ASSETS["PilgrimBanner"], (x + s * (half + 1.0), y, 0))
        put(coll, LIGHTS["Candles"], (x - 1.0, front - 1.2, 0))
    else:
        reliquary = "ReliquaryWood" if tier <= 6 else "ReliquaryIron" if tier <= 8 else "ReliquarySilver"
        put(coll, M.ASSETS["ReliquaryStand"], (x - 2.0, front - 0.5, 0))
        put(coll, M.ASSETS[reliquary], (x - 2.0, front - 0.5, 0.98))
        put(coll, M.ASSETS["OfferingBowls"], (x + 2.0, front - 0.5, 0))
        braziers = 2 if tier <= 7 else 4
        for k in range(braziers):
            sx = -1 if k % 2 else 1
            sy = -1 if k < 2 else 1
            put(coll, M.ASSETS["StoneBrazier"], (x + sx * (half + 1.0), y + sy * (half - 0.5), 0))
        if tier >= 7:
            put(coll, M.ASSETS["Stele"], (x + half + 1.5, y + half - 1.0, 0), rot=math.pi / 2)
        if tier >= 9:
            for s in (-1, 1):
                put(coll, M.ASSETS["PilgrimBanner"], (x + s * 1.0, front - 2.5, 0))
        if 5 <= tier <= 8:                                                   # a chain along the front, open in the middle
            for side in (-1, 1):
                for k in range(3):
                    px = x + side * (2.0 + k)
                    put(coll, M.ASSETS["ChainPost"], (px, front - 1.6, 0))
                    if k < 2:
                        put(coll, M.ASSETS["ChainSpan"], (px + side * 0.5, front - 1.6, 0))


def board():
    A.reset()
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    top = A.collection("Monuments board")
    site = bpy.data.collections.new("Unraised site")
    top.children.link(site)
    put(site, M.ASSETS["MonumentSite"], (-6.0, 0.0, 0))
    label("not yet raised", (-7.5, -3.0, 0.01), site)
    x, y = 0.0, 0.0
    for tier in M.TIERS:
        coll = bpy.data.collections.new("Tier %d" % tier)
        top.children.link(coll)
        half = M.TIERS[tier]["rings"][0] / 2
        if tier == 6:
            x, y = 0.0, -20.0
        x += half + 4.0
        put(coll, M.ASSETS["Monument%d" % tier], (x, y, 0))
        dress(coll, tier, x, y)
        label("Tier %d (level %d)" % (tier, 1 if tier == 1 else (tier - 1) * 10), (x - half, y - half - 4.2, 0.01), coll)
        x += half + 4.0
    A.preview_rig(focus=(18, -8, 2), distance=42, pitch=42, yaw=-12, ground=60, human=True, human_at=(-2.5, -2.0))
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True
    return {"tiers": len(M.TIERS)}


if __name__ in ("__main__", "<run_path>"):
    result = board()
