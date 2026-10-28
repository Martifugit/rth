"""The basin board: a patch of Tolling's basin (grass, the mere's edge) with one of each kind of
nature asset wave 1 needs, the player for scale, so the families can be judged together on the
ground they will stand on.

The references (images/osrs-tree-*, -foliage-*, -fence-*) set the technique only: few big faces,
leaf textures with a ragged hem, smooth light, flat-coloured timber. The forms are R'th's own: a
worked, weathered land under a wind off the spine. Each tree has its own habit and its own leaf
(a coppiced alder stool, a tall airy ash, a wind-flagged hawthorn, pollard willows on the bank) and
a felled state; bark is dark and crooked; berry bushes have a picked state; fences are bucks and
woven hurdles; the grass grows in tussocks, with rust-red dock. Textures are textures/nature.py's.
The rules that passed are in tools/art/blender/README.md ("Nature").

Every model comes from its family (assets/trees/common.py, parts/fences/village.py...), which
exports it; the board only places them.

    import runpy; ns = runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\boards\\basin.py")
"""

import bmesh
import bpy
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
from artlib.nature import make  # noqa: E402
from assets.containers.village import barrel, crate, sacks, trough  # noqa: E402
from assets.dressing.village import (  # noqa: E402
    chopping_block, eel_traps, farm_cart, grave, hay_stook, lean_to, mooring_post,
    scarecrow, skeps, woodpile)
from assets.lights.common import campfire  # noqa: E402
from assets.nodes.common import KINDS  # noqa: E402
from assets.plants.common import (  # noqa: E402
    berry_bush_low, berry_bush_tall, bracken, bramble, brushwood, dock, flax,
    gorse, heather, herb, lily_pads, mushrooms, reeds, thistles,
    tussock, wildflowers)
from assets.rocks.common import boulders, cairn, scree, standing_stone, standing_stone_fallen  # noqa: E402
from assets.trees.common import (  # noqa: E402
    alder, ash, ash_stump, birch, birch_stump, elm, elm_stump,
    fallen_log, hawthorn, hawthorn_stump, pine, pine_stump, pollard, pollard_stump,
    rotten_stump, sapling, snag, stool_cut)
from assets.vessels.village import punt  # noqa: E402
from assets.ways.common import stepping_stones  # noqa: E402
from assets.ways.village import fingerpost, footbridge, milestone  # noqa: E402
from parts.fences.village import buck_fence, field_gate, hedge, hurdle, wall_span, wall_stile  # noqa: E402
from parts.waterfront.village import jetty_span  # noqa: E402


W, H = 44, 38
WATER_Z = -0.18


def ground_z(x, y):
    """The mere's bank along the south edge: level grass from y 4.5, dipping under the water by y 1."""
    shore = 3.2 + 0.8 * math.sin(x * 0.45) + 0.4 * math.sin(x * 1.3 + 1)
    d = y - shore
    if d >= 1.2:
        return 0.0
    if d <= -1.0:
        return -0.55
    t = (d + 1.0) / 2.2
    return -0.55 + 0.55 * (t * t * (3 - 2 * t))


def ground():
    """Plain grass with the mere along the front edge (mud at the waterline, the water one flat
    surface). Tile corners at whole numbers, like the game's ground."""
    bm = bmesh.new()
    vs = {}
    for x in range(W + 1):
        for y in range(H + 1):
            vs[x, y] = bm.verts.new((x, y, ground_z(x, y)))
    for x in range(W):
        for y in range(H):
            f = bm.faces.new((vs[x, y], vs[x + 1, y], vs[x + 1, y + 1], vs[x, y + 1]))
            f.material_index = 1 if ground_z(x + 0.5, y + 0.5) < WATER_Z + 0.02 else 0
    water = [bm.verts.new(p) for p in ((0, 0, WATER_Z), (W, 0, WATER_Z), (W, 5.2, WATER_Z), (0, 5.2, WATER_Z))]
    bm.faces.new(water).material_index = 2
    return make("Ground", bm, ["Grass", "Mud", "Mere"], coll="Preview")


ROW_TREES, ROW_BUSHES, ROW_STONE, ROW_BOUNDS, ROW_PLACES, ROW_MINING, ROW_WILDS, ROW_WAYS = (
    8.0, 12.5, 16.0, 19.5, 23.0, 27.0, 31.0, 35.0)


def build():
    """The board, in rows from the water back: the mere's edge; the trees, each with its felled
    state beside it; bushes and plants; stone; boundaries; places (the heath's stones, graves, the
    farm); mining (assets/nodes/common.py: each kind's variants in copper and iron, then worked out);
    the wilds and the ways ( ground cover, deadwood, loose stone; ways, camps and
    work)."""
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Nature", "Preview"):
            bpy.data.collections.remove(c)
    ground()
    objs = {}
    for i, x in enumerate((7.0, 13.5, 22.0)):
        objs["reeds_%d" % i] = reeds(seed=3 + 5 * i, at=(x, 2.4, ground_z(x, 2.4) + 0.2))
    objs["lily_pads"] = lily_pads(at=(17.5, 1.6, WATER_Z))
    objs["lily_pads_2"] = lily_pads(seed=4, at=(10.0, 1.2, WATER_Z))
    for i, y in enumerate((3.5, 2.5, 1.5, 0.5)):       # a jetty out from the bank
        objs["jetty_%d" % i] = jetty_span(seed=i + 1, at=(30.5, y, WATER_Z), rot=math.pi / 2)
    objs["mooring_post"] = mooring_post(at=(31.4, 1.2, WATER_Z))
    objs["punt"] = punt(at=(32.2, 1.3, WATER_Z), rot=math.pi / 2 + 0.1)
    t = ROW_TREES
    objs.update({
        "alder": alder(at=(3.0, t, 0)), "stool_cut": stool_cut(at=(5.4, t, 0)),
        "ash": ash(at=(9.5, t, 0)), "ash_stump": ash_stump(at=(12.3, t, 0)),
        "hawthorn": hawthorn(at=(15.0, t, 0)), "hawthorn_stump": hawthorn_stump(at=(19.6, t, 0)),
        "pollard": pollard(at=(22.8, t, 0)), "pollard_stump": pollard_stump(at=(25.2, t, 0)),
        "snag": snag(at=(28.6, t, 0)),
        "elm": elm(at=(32.0, t, 0)), "elm_stump": elm_stump(at=(35.2, t, 0)),
        "pine": pine(at=(38.5, t, 0)), "pine_stump": pine_stump(at=(41.0, t, 0)),
    })
    b = ROW_BUSHES
    objs.update({
        "gorse": gorse(at=(2.5, b, 0)), "gorse_2": gorse(seed=21, at=(4.6, b, 0)),
        "berry_low": berry_bush_low(at=(8.0, b, 0)), "berry_low_picked": berry_bush_low(at=(10.2, b, 0), picked=True),
        "berry_tall": berry_bush_tall(at=(13.4, b, 0)),
        "berry_tall_picked": berry_bush_tall(at=(15.2, b, 0), picked=True),
    })
    for i, (x, y) in enumerate(((18.5, b - 0.4), (19.6, b + 0.3), (20.4, b - 0.5), (21.3, b + 0.2))):
        objs["tussock_%d" % i] = tussock(seed=i + 1, at=(x, y, 0))
    for i, (x, y) in enumerate(((24.0, b), (24.8, b + 0.5), (25.5, b - 0.3))):
        objs["dock_%d" % i] = dock(seed=i + 1, at=(x, y, 0))
    objs.update({
        "flax": flax(at=(28.0, b, 0)), "flax_picked": flax(at=(29.4, b, 0), picked=True),
        "herb": herb(at=(31.0, b, 0)), "herb_picked": herb(at=(32.2, b, 0), picked=True),
        "mushrooms": mushrooms(at=(33.8, b, 0)), "mushrooms_picked": mushrooms(at=(35.0, b, 0), picked=True),
    })
    s_ = ROW_STONE
    objs.update({
        "boulders": boulders(at=(3.5, s_, 0)),
        "boulders_small": boulders(seed=17, at=(7.5, s_, 0), size=0.6),
        "boulders_large": boulders(seed=3, at=(11.5, s_, 0), size=1.4),
        "cairn": cairn(at=(15.5, s_, 0)),
    })
    r = ROW_BOUNDS
    for i, x in enumerate((1, 2, 3, 4)):
        objs["buck_%d" % i] = buck_fence(seed=i + 1, at=(x, r, 0), end_post=(i == 3))
    for i, x in enumerate((7, 8, 9)):
        objs["hurdle_%d" % i] = hurdle(seed=i + 1, at=(x, r, 0), end_post=(i == 2))
    objs["gate"] = field_gate(at=(10, r, 0))          # in the hurdles' line
    for i, x in enumerate((12, 13, 15, 16)):           # a gap at 14: a fallen stretch of wall
        objs["wall_%d" % i] = wall_span(seed=i + 1, at=(x, r, 0))
    objs["stile"] = wall_stile(at=(17, r, 0))
    p_ = ROW_PLACES
    objs.update({
        "standing_stone": standing_stone(at=(3.0, p_, 0)),
        "standing_stone_2": standing_stone(seed=5, at=(4.6, p_ + 0.6, 0)),
        "standing_stone_fallen": standing_stone_fallen(at=(7.5, p_, 0)),
        "grave": grave(at=(11.0, p_, 0)), "grave_2": grave(seed=3, at=(12.2, p_, 0)),
        "grave_old": grave(seed=4, at=(13.4, p_, 0), old=True),
        "hay_stook": hay_stook(at=(17.0, p_, 0)), "hay_stook_2": hay_stook(seed=2, at=(18.5, p_ + 0.4, 0)),
        "farm_cart": farm_cart(at=(22.0, p_, 0), rot=0.4),
    })
    for i, x in enumerate(range(19, 25)):
        objs["hedge_%d" % i] = hedge(seed=i + 1, at=(x, r, 0))
    objs.update(row_mining(ROW_MINING))
    objs.update(row_wilds(ROW_WILDS, ROW_WAYS))
    objs["stepping_stones"] = stepping_stones(at=(4.0, 2.2, 0), water=WATER_Z)
    return objs


def rig():
    """The game's sun and camera, the player by the trees, Material Preview with scene lights."""
    A.preview_rig(focus=(22.0, 12.5, 0.8), distance=44.0, pitch=40, yaw=-20, ground=0, human=True,
                  human_at=(7.0, ROW_TREES - 1.2))
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


def row_wilds(y_wilds, y_ways=None):
    """The board's rows: ground cover, deadwood and stone; then ways, camps and work."""
    objs, x = {}, 2.0
    for name, fn, w in (("bracken", bracken, 1.8), ("bracken_2", lambda at: bracken(seed=4, at=at), 2.0),
                        ("bramble", bramble, 2.6), ("wildflowers", wildflowers, 1.6),
                        ("wildflowers_2", lambda at: wildflowers(seed=7, at=at), 1.8), ("heather", heather, 1.8),
                        ("heather_2", lambda at: heather(seed=3, at=at), 2.0), ("thistles", thistles, 2.4),
                        ("fallen_log", fallen_log, 3.6), ("woodpile", woodpile, 2.2), ("brushwood", brushwood, 2.4),
                        ("chopping_block", chopping_block, 2.6), ("scree", scree, 2.4)):
        objs[name] = fn(at=(x, y_wilds, 0))
        x += w
    if y_ways is None:
        return objs
    x = 2.0
    for name, fn, w in (("fingerpost", fingerpost, 2.0), ("milestone", milestone, 1.6), ("footbridge", footbridge, 2.4),
                        ("campfire", campfire, 1.6), ("campfire_burnt", lambda at: campfire(at=at, burnt=True), 2.0),
                        ("lean_to", lean_to, 3.0), ("trough", trough, 2.2), ("skeps", skeps, 2.4),
                        ("scarecrow", scarecrow, 2.0), ("eel_traps", eel_traps, 2.0), ("barrel", barrel, 1.0),
                        ("crate", crate, 1.0), ("sacks", sacks, 2.2), ("birch", birch, 2.6),
                        ("birch_stump", birch_stump, 1.8), ("sapling", sapling, 1.8), ("rotten_stump", rotten_stump, 2.0)):
        objs[name] = fn(at=(x, y_ways, 0))
        x += w
    return objs


def row_mining(y, x0=2.0, ores=("CopperOre", "IronOre")):
    """The board's mining row: each kind's variants, live in each ore and worked out, left to right."""
    objs, x = {}, x0
    for kind, fn in KINDS.items():
        for v in "AB":
            for ore in ores:
                ob = fn(v, False, at=(x, y, 0))
                if ore != "CopperOre":
                    ob.data.materials[ob.data.materials.find("CopperOre")] = A.mat(ore)
                objs["%s_%s_%s" % (kind.lower(), v, ore)] = ob
                x += 1.7
            objs["%s_%s_dug" % (kind.lower(), v)] = fn(v, True, at=(x, y, 0))
            x += 2.4
    return objs


if __name__ in ("__main__", "<run_path>"):
    objs = build()
    rig()
    result = {k: A.report([o])["triangles"] for k, o in objs.items() if not k[-1].isdigit() or k.endswith("_0")}
