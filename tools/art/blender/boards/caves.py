"""The caves board: cave and dungeon entrances from generic parts and the dressings that say what a
place is (the families under parts/ and assets/ in the styles common, makers and mine). Every
model comes from its family, which exports it; the board only places them.

It shows the same entrance dressed seven ways along the front (a plain cave, a mine, an overgrown
mouth, a shrine, a lair, the makers' door, a sealed crypt), the ways down and through behind them
(a pit with a rope, a sinkhole, a fissure with a ladder, a shaft with a windlass, a stairwell, a
squeeze, a sump, a burrow, a planked pit), and every model behind those, a row per family.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\boards\\caves.py")
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
import assets.dressing.common  # noqa: E402
import assets.dressing.mine  # noqa: E402
import assets.lights.common  # noqa: E402
import assets.lights.mine  # noqa: E402
import assets.openings.common  # noqa: E402
import assets.openings.makers  # noqa: E402
import assets.plants.common  # noqa: E402
import parts.floors.common  # noqa: E402
import parts.floors.mine  # noqa: E402
import parts.rockwork.common  # noqa: E402
import parts.walls.common  # noqa: E402
import parts.walls.makers  # noqa: E402
import parts.walls.mine  # noqa: E402
from artlib.caves import SPAN  # noqa: E402
from assets.dressing.common import (  # noqa: E402
    bone_pile, hide_rack, masonry_rubble, nest, offering, ribbon_post)
from assets.dressing.mine import mine_cart, spoil_heap  # noqa: E402
from assets.lights.common import candles  # noqa: E402
from assets.lights.mine import lantern_post  # noqa: E402
from assets.openings.common import (  # noqa: E402
    burrow, fissure, gate, grate, ladder_top, pit, planked,
    rope_anchor, shaft, sinkhole, stairwell, sump, windlass)
from assets.openings.makers import seal_stone  # noqa: E402
from assets.plants.common import fern, hanging_roots, ivy_drape  # noqa: E402
from parts.floors.common import steps  # noqa: E402
from parts.floors.mine import mine_rails  # noqa: E402
from parts.rockwork.common import boulder, rock_lip, rock_mass, rubble, scree, throat  # noqa: E402
from parts.walls.common import arch, lintel, post, wall_span  # noqa: E402
from parts.walls.makers import maker_lintel, maker_pillar, maker_threshold  # noqa: E402
from parts.walls.mine import timber_frame  # noqa: E402


W, H = 64, 52

# The cave families, and from the families of no style only their cave members.
ROWS = [(parts.rockwork.common, None), (assets.openings.common, None), (parts.walls.common, None),
        (parts.floors.common, None), (parts.walls.makers, None), (assets.openings.makers, None),
        (parts.walls.mine, None), (parts.floors.mine, None), (assets.dressing.mine, None),
        (assets.lights.mine, None), (assets.plants.common, {"HangingRoots", "IvyDrape", "Fern"}),
        (assets.dressing.common, None), (assets.lights.common, {"Candles"})]


def ground():
    bm = bmesh.new()
    vs = {(x, y): bm.verts.new((x, y, 0)) for x in range(W + 1) for y in range(H + 1)}
    for x in range(W):
        for y in range(H):
            bm.faces.new((vs[x, y], vs[x + 1, y], vs[x + 1, y + 1], vs[x, y + 1]))
    return N.make("Ground", bm, ["Grass"], coll="Preview")


def cave(o, key, x, y, seed):
    """The generic mouth every cave dressing starts from: masses either side, a lip, the throat."""
    o[key + "_l"] = rock_mass(seed=seed, at=(x - 2.0, y + 0.4, 0))
    o[key + "_r"] = rock_mass(seed=seed + 1, at=(x + 2.1, y + 0.3, 0))
    o[key + "_back"] = rock_mass(seed=seed + 2, kind="wide", at=(x, y + 2.9, 0))
    o[key + "_lip"] = rock_lip(seed=seed + 3, at=(x, y + 0.3, 0))
    o[key + "_throat"] = throat(seed=seed, at=(x, y, 0))


def built(o, key, x, y, seed):
    """The generic built doorway: coursed wall either side, set into rock, the square throat behind;
    its posts, lintel or arch are the dressing's."""
    for i, dx in enumerate((-3.0, -2.0, 2.0, 3.0)):
        o["%s_wall%d" % (key, i)] = wall_span(seed=seed + i, at=(x + dx, y + 0.1, 0))
    o[key + "_rock_l"] = rock_mass(seed=seed + 5, kind="wide", at=(x - 2.6, y + 1.6, 0))
    o[key + "_rock_r"] = rock_mass(seed=seed + 6, kind="wide", at=(x + 2.6, y + 1.6, 0))
    o[key + "_back"] = rock_mass(seed=seed + 7, at=(x, y + 3.3, 0))
    o[key + "_throat"] = throat(seed=seed, kind="square", at=(x, y + 0.15, 0))


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
    o["plain_rubble"] = rubble(at=(x + 1.6, y - 1.1, 0))
    o["plain_boulder"] = boulder(at=(x - 2.4, y - 1.5, 0))
    # 2. A mine.
    x = 13.5
    cave(o, "mine", x, y, 20)
    o["mine_frame"] = timber_frame(at=(x, y - 0.1, 0))
    for k in range(3):
        o["mine_rails%d" % k] = mine_rails(seed=k, at=(x, y - 1.5 + k, 0))
    o["mine_cart"] = mine_cart(at=(x, y - 1.9, 0))
    o["mine_spoil"] = spoil_heap(at=(x + 2.4, y - 1.6, 0))
    o["mine_lantern"] = lantern_post(at=(x - 1.4, y - 0.7, 0))
    # 3. Overgrown.
    x = 22.0
    cave(o, "green", x, y, 30)
    o["green_roots"] = hanging_roots(at=(x, y - 0.25, 0))
    o["green_ivy_l"] = ivy_drape(at=(x - 1.7, y - 0.4, 0))
    o["green_ivy_r"] = ivy_drape(seed=5, at=(x + 1.8, y - 0.5, 0), height=2.2)
    o["green_fern_l"] = fern(at=(x - 1.2, y - 0.9, 0))
    o["green_fern_r"] = fern(seed=6, at=(x + 1.3, y - 1.0, 0))
    # 4. A shrine at a built door.
    x = 31.0
    built(o, "shrine", x, y, 40)
    o["shrine_arch"] = arch(at=(x, y, 0))
    o["shrine_steps"] = steps(at=(x, y - 0.6, 0))
    o["shrine_candles"] = candles(at=(x - 1.1, y - 1.9, 0))
    o["shrine_offering"] = offering(at=(x + 1.2, y - 1.8, 0))
    o["shrine_ribbons"] = ribbon_post(at=(x + 2.6, y - 1.6, 0))
    # 5. A lair.
    x = 40.0
    cave(o, "lair", x, y, 50)
    o["lair_bones"] = bone_pile(at=(x + 0.9, y - 1.3, 0))
    o["lair_nest"] = nest(at=(x - 0.2, y + 0.7, 0))
    o["lair_hides"] = hide_rack(at=(x - 2.6, y - 1.8, 0))
    o["lair_scree"] = scree(at=(x + 2.4, y - 0.9, 0))
    # 6. The makers' door.
    x = 49.0
    cave(o, "makers", x, y + 0.6, 60)
    o["makers_left"] = maker_pillar(at=(x - SPAN / 2, y, 0))
    o["makers_right"] = maker_pillar(seed=2, at=(x + SPAN / 2, y, 0))
    o["makers_lintel"] = maker_lintel(at=(x, y, 0))
    o["makers_threshold"] = maker_threshold(at=(x, y - 0.2, 0))
    o["makers_seal"] = seal_stone(at=(x - 2.6, y - 1.4, 0))
    o["makers_broken"] = maker_pillar(seed=3, broken=True, at=(x + 2.6, y - 1.6, 0))
    # 7. A sealed crypt.
    x = 58.0
    built(o, "crypt", x, y, 70)
    o["crypt_post_l"] = post(at=(x - SPAN / 2, y, 0))
    o["crypt_post_r"] = post(seed=2, at=(x + SPAN / 2, y, 0))
    o["crypt_lintel"] = lintel(at=(x, y, 0))
    o["crypt_gate"] = gate(at=(x, y + 0.05, 0))
    o["crypt_grate"] = grate(at=(x + 2.2, y - 1.5, 0))
    o["crypt_rubble"] = masonry_rubble(at=(x - 2.4, y - 1.6, 0))
    # The ways down and through.
    y = 12.0
    o["d_pit"] = pit(at=(4.0, y, 0))
    o["d_rope"] = rope_anchor(at=(4.0, y - 2.0, 0))
    o["d_sinkhole"] = sinkhole(at=(10.0, y, 0))
    o["d_fissure"] = fissure(at=(16.5, y, 0))
    o["d_ladder"] = ladder_top(at=(16.3, y, 0))
    o["d_shaft"] = shaft(at=(23.0, y, 0))
    o["d_windlass"] = windlass(at=(23.0, y, 0))
    o["d_stairwell"] = stairwell(at=(28.5, y, 0))
    o["d_stair_post"] = post(broken=True, at=(29.9, y - 1.2, 0))
    x = 35.0
    o["d_sq_l"] = rock_mass(seed=81, kind="tall", at=(x - 0.8, y + 0.3, 0))
    o["d_sq_r"] = rock_mass(seed=82, kind="tall", at=(x + 0.85, y + 0.4, 0))
    o["d_sq_back"] = rock_mass(seed=83, at=(x, y + 3.0, 0))
    o["d_sq_lip"] = rock_lip(seed=84, height=2.0, at=(x, y + 0.6, 0))
    o["d_squeeze"] = throat(kind="squeeze", at=(x, y, 0))
    o["d_sump"] = sump(at=(41.0, y, 0))
    o["d_burrow"] = burrow(at=(47.0, y, 0))
    o["d_burrow_throat"] = throat(kind="burrow", at=(47.0, y - 0.1, 0))
    o["d_planked_pit"] = pit(seed=5, at=(53.0, y, 0))
    o["d_planked"] = planked(at=(53.0, y, 0))
    # Every cave model, a row per family.
    row = 17.5
    for fam, names in ROWS:
        members = [(n, b) for n, b in fam.ASSETS.items() if names is None or n in names]
        for i, (name, builder) in enumerate(members):
            ob = family.build(name, builder)
            ob.location.x += 2.5 + 3.4 * i
            ob.location.y += row
            o["part_" + name] = ob
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


if __name__ in ("__main__", "<run_path>"):
    objs = build()
    rig()
    result = {k[5:]: A.report([ob])["triangles"] for k, ob in objs.items() if k.startswith("part_")}
