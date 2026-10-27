"""Cave and dungeon entrances: a large kit of generic parts (parts.py) and identity sets that say
what a place is (identity.py), built in Blender (see tools/art/blender/README.md).

This script is the board and the export. The board shows the same entrance dressed seven ways
along the front (a plain cave, a mine, an overgrown mouth, a shrine, a lair, the makers' door, a
sealed crypt), the ways down and through behind them (a pit with a rope, a sinkhole, a fissure with
a ladder, a shaft with a windlass, a stairwell, a squeeze, a sump, a burrow, a planked pit), and
every part behind those, one row per group.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\caves\\entrances.py")
    runpy.run_path(path, init_globals={"EXPORT": True})   # every part to Resources/Caves/<Name>.fbx
"""
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, os.path.join(HERE, ".."), os.path.join(HERE, "..", "nature")):
    if p not in sys.path:
        sys.path.insert(0, p)
for m in ("rth_art", "basin", "shapes", "parts", "identity"):
    sys.modules.pop(m, None)                     # pick up edits when re-run in the same Blender

import rth_art as A  # noqa: E402
import basin as N  # noqa: E402
import identity as I  # noqa: E402
import parts as P  # noqa: E402
from identity import IDENTITY  # noqa: E402
from parts import GENERIC, SPAN  # noqa: E402

W, H = 64, 52


def ground():
    bm = bmesh.new()
    vs = {(x, y): bm.verts.new((x, y, 0)) for x in range(W + 1) for y in range(H + 1)}
    for x in range(W):
        for y in range(H):
            bm.faces.new((vs[x, y], vs[x + 1, y], vs[x + 1, y + 1], vs[x, y + 1]))
    return N.make("Ground", bm, ["Grass"], coll="Preview")


def cave(o, key, x, y, seed):
    """The generic mouth every cave dressing starts from: masses either side, a lip, the throat."""
    o[key + "_l"] = P.rock_mass(seed=seed, at=(x - 2.0, y + 0.4, 0))
    o[key + "_r"] = P.rock_mass(seed=seed + 1, at=(x + 2.1, y + 0.3, 0))
    o[key + "_back"] = P.rock_mass(seed=seed + 2, kind="wide", at=(x, y + 2.9, 0))
    o[key + "_lip"] = P.rock_lip(seed=seed + 3, at=(x, y + 0.3, 0))
    o[key + "_throat"] = P.throat(seed=seed, at=(x, y, 0))


def built(o, key, x, y, seed):
    """The generic built doorway: coursed wall either side, set into rock, the square throat behind;
    its posts, lintel or arch are the dressing's."""
    for i, dx in enumerate((-3.0, -2.0, 2.0, 3.0)):
        o["%s_wall%d" % (key, i)] = P.wall_span(seed=seed + i, at=(x + dx, y + 0.1, 0))
    o[key + "_rock_l"] = P.rock_mass(seed=seed + 5, kind="wide", at=(x - 2.6, y + 1.6, 0))
    o[key + "_rock_r"] = P.rock_mass(seed=seed + 6, kind="wide", at=(x + 2.6, y + 1.6, 0))
    o[key + "_back"] = P.rock_mass(seed=seed + 7, at=(x, y + 3.3, 0))
    o[key + "_throat"] = P.throat(seed=seed, kind="square", at=(x, y + 0.15, 0))


def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Caves", "Preview", "Nature"):
            bpy.data.collections.remove(c)
    ground()
    o = {}
    y = 5.0
    # 1. A plain cave.
    x = 5.0
    cave(o, "plain", x, y, 10)
    o["plain_rubble"] = P.rubble(at=(x + 1.6, y - 1.1, 0))
    o["plain_boulder"] = P.boulder(at=(x - 2.4, y - 1.5, 0))
    # 2. A mine.
    x = 13.5
    cave(o, "mine", x, y, 20)
    o["mine_frame"] = P.timber_frame(at=(x, y - 0.1, 0))
    for k in range(3):
        o["mine_rails%d" % k] = I.mine_rails(seed=k, at=(x, y - 1.5 + k, 0))
    o["mine_cart"] = I.mine_cart(at=(x, y - 1.9, 0))
    o["mine_spoil"] = I.spoil_heap(at=(x + 2.4, y - 1.6, 0))
    o["mine_lantern"] = I.lantern_post(at=(x - 1.4, y - 0.7, 0))
    # 3. Overgrown.
    x = 22.0
    cave(o, "green", x, y, 30)
    o["green_roots"] = I.hanging_roots(at=(x, y - 0.25, 0))
    o["green_ivy_l"] = I.ivy_drape(at=(x - 1.7, y - 0.4, 0))
    o["green_ivy_r"] = I.ivy_drape(seed=5, at=(x + 1.8, y - 0.5, 0), height=2.2)
    o["green_fern_l"] = I.fern(at=(x - 1.2, y - 0.9, 0))
    o["green_fern_r"] = I.fern(seed=6, at=(x + 1.3, y - 1.0, 0))
    # 4. A shrine at a built door.
    x = 31.0
    built(o, "shrine", x, y, 40)
    o["shrine_arch"] = P.arch(at=(x, y, 0))
    o["shrine_steps"] = P.steps(at=(x, y - 0.6, 0))
    o["shrine_candles"] = I.candles(at=(x - 1.1, y - 1.9, 0))
    o["shrine_offering"] = I.offering(at=(x + 1.2, y - 1.8, 0))
    o["shrine_ribbons"] = I.ribbon_post(at=(x + 2.6, y - 1.6, 0))
    # 5. A lair.
    x = 40.0
    cave(o, "lair", x, y, 50)
    o["lair_bones"] = I.bone_pile(at=(x + 0.9, y - 1.3, 0))
    o["lair_nest"] = I.nest(at=(x - 0.2, y + 0.7, 0))
    o["lair_hides"] = I.hide_rack(at=(x - 2.6, y - 1.8, 0))
    o["lair_scree"] = P.scree(at=(x + 2.4, y - 0.9, 0))
    # 6. The makers' door.
    x = 49.0
    cave(o, "makers", x, y + 0.6, 60)
    o["makers_left"] = I.maker_pillar(at=(x - SPAN / 2, y, 0))
    o["makers_right"] = I.maker_pillar(seed=2, at=(x + SPAN / 2, y, 0))
    o["makers_lintel"] = I.maker_lintel(at=(x, y, 0))
    o["makers_threshold"] = I.maker_threshold(at=(x, y - 0.2, 0))
    o["makers_seal"] = I.seal_stone(at=(x - 2.6, y - 1.4, 0))
    o["makers_broken"] = I.maker_pillar(seed=3, broken=True, at=(x + 2.6, y - 1.6, 0))
    # 7. A sealed crypt.
    x = 58.0
    built(o, "crypt", x, y, 70)
    o["crypt_post_l"] = P.post(at=(x - SPAN / 2, y, 0))
    o["crypt_post_r"] = P.post(seed=2, at=(x + SPAN / 2, y, 0))
    o["crypt_lintel"] = P.lintel(at=(x, y, 0))
    o["crypt_gate"] = P.gate(at=(x, y + 0.05, 0))
    o["crypt_grate"] = P.grate(at=(x + 2.2, y - 1.5, 0))
    o["crypt_rubble"] = P.masonry_rubble(at=(x - 2.4, y - 1.6, 0))
    # The ways down and through.
    y = 12.0
    o["d_pit"] = P.pit(at=(4.0, y, 0))
    o["d_rope"] = P.rope_anchor(at=(4.0, y - 2.0, 0))
    o["d_sinkhole"] = P.sinkhole(at=(10.0, y, 0))
    o["d_fissure"] = P.fissure(at=(16.5, y, 0))
    o["d_ladder"] = P.ladder_top(at=(16.3, y, 0))
    o["d_shaft"] = P.shaft(at=(23.0, y, 0))
    o["d_windlass"] = P.windlass(at=(23.0, y, 0))
    o["d_stairwell"] = P.stairwell(at=(28.5, y, 0))
    o["d_stair_post"] = P.post(broken=True, at=(29.9, y - 1.2, 0))
    x = 35.0
    o["d_sq_l"] = P.rock_mass(seed=81, kind="tall", at=(x - 0.8, y + 0.3, 0))
    o["d_sq_r"] = P.rock_mass(seed=82, kind="tall", at=(x + 0.85, y + 0.4, 0))
    o["d_sq_back"] = P.rock_mass(seed=83, at=(x, y + 3.0, 0))
    o["d_sq_lip"] = P.rock_lip(seed=84, height=2.0, at=(x, y + 0.6, 0))
    o["d_squeeze"] = P.throat(kind="squeeze", at=(x, y, 0))
    o["d_sump"] = P.sump(at=(41.0, y, 0))
    o["d_burrow"] = P.burrow(at=(47.0, y, 0))
    o["d_burrow_throat"] = P.throat(kind="burrow", at=(47.0, y - 0.1, 0))
    o["d_planked_pit"] = P.pit(seed=5, at=(53.0, y, 0))
    o["d_planked"] = P.planked(at=(53.0, y, 0))
    # Every part, a row per group.
    row = 17.5
    for group in list(GENERIC.values()) + list(IDENTITY.values()):
        for i, (name, fn) in enumerate(group.items()):
            o["part_" + name] = fn(at=(2.5 + 3.4 * i, row, 0))
        row += 3.2
    return o


def rig():
    A.preview_rig(focus=(32.0, 10.0, 1.0), distance=60.0, pitch=40, yaw=-10, ground=0, human=True,
                  human_at=(31.0, 2.4))
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


def export_all():
    """Every part to Resources/Caves/<Name>.fbx; models the kit no longer makes are removed."""
    folder = os.path.join(A.REPO, "client", "Assets", "Resources", "Caves")
    names = {n for g in list(GENERIC.values()) + list(IDENTITY.values()) for n in g}
    for f in os.listdir(folder) if os.path.isdir(folder) else ():
        if f.endswith(".fbx") and f[:-4] not in names:
            os.remove(os.path.join(folder, f))
    out = {}
    for group in list(GENERIC.values()) + list(IDENTITY.values()):
        for name, builder in group.items():
            A.reset()
            ob = builder()
            ob.name = ob.data.name = name
            A.export([ob], "Resources/Caves/%s.fbx" % name)
            out[name] = A.report([ob])["triangles"]
    return out


if globals().get("EXPORT"):
    result = export_all()
else:
    objs = build()
    rig()
    result = {k[5:]: A.report([ob])["triangles"] for k, ob in objs.items() if k.startswith("part_")}
