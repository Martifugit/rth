"""Dressing, common. Dressing of no style: broken masonry, offerings, ribbons, bones, a nest, a hide rack.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Dressing/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\dressing\\common.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import sys

from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
import artlib.nature as N  # noqa: E402
from artlib.caves import STONE, added, block, lay, make, rng, weather  # noqa: E402


def masonry_rubble(seed=4, at=(0, 0, 0)):
    """Fallen masonry: dressed blocks tumbled in a heap, some half sunk, chips round them."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(6):
        before = set(bm.verts)
        s = r.uniform(0.18, 0.3)
        block(bm, (-s * 1.4, -s, 0), (s * 1.4, s, s * 1.6), r)
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0, 0.6)
        lay(bm, added(bm, before), Matrix.Translation((d * math.cos(a), d * math.sin(a), -0.05 + (0.25 if i == 5 else 0)))
            @ Matrix.Rotation(r.uniform(0, 3), 4, 'Z') @ Matrix.Rotation(r.uniform(-0.4, 0.4), 4, 'X'))
    weather(bm, r, 0.015, moss=1, up=0.9)
    return make("MasonryRubble", bm, [STONE, "Moss"], at=at, smooth=20)


def offering(seed=3, at=(0, 0, 0)):
    """An offering left at a mouth: four flat stones stacked askew, a guttered candle stub on top
    with a small flame, a scatter of pale chips (bone, or shell) at the foot."""
    r = rng(seed)
    bm = bmesh.new()
    z, rad = 0.0, 0.26
    for _ in range(4):
        h = r.uniform(0.06, 0.09)
        N.slab(bm, (r.uniform(-0.02, 0.02), r.uniform(-0.02, 0.02), z), rad, h, 6, r, rot=r.uniform(0, 3))
        z += h - 0.005
        rad *= 0.8
    N.rod(bm, [(0, 0, z - 0.01), (0.005, 0, z + 0.09)], 0.03, mat=2, sides=5)
    A.spike(bm, (0.005, 0, z + 0.085), 0.018, (0.01, 0, z + 0.16), sides=4, mat=3)
    for _ in range(5):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0.3, 0.5)
        c = Vector((d * math.cos(a), d * math.sin(a), 0))
        A.hull(bm, [c + Vector((0.035 * math.cos(t), 0.02 * math.sin(t), -0.01)) for t in (0, 2.1, 4.2)] +
               [c + Vector((0, 0, 0.025))], mat=2)
    return make("Offering", bm, ["Scree", "Gravestone", "CandleWax", "Ember"], at=at, smooth=30)


def ribbon_post(seed=6, at=(0, 0, 0)):
    """A stake by a holy mouth where people tie their asks: strips of pale and dyed cloth knotted
    to it and its crossbar, hanging still."""
    r = rng(seed)
    bm = bmesh.new()
    N.rod(bm, N.wobble(r, (0, 0, -0.05), (0.02, 0, 1.6), 3, 0.03), 0.05, sides=5, taper=0.85)
    N.rod(bm, [(-0.4, 0, 1.35), (0.42, 0, 1.38)], 0.03, sides=4)
    for k in range(9):
        x = r.uniform(-0.38, 0.38)
        top = 1.35 + 0.03 * (x + 0.4) / 0.8
        length = r.uniform(0.25, 0.6)
        w = r.uniform(0.03, 0.05)
        y = r.uniform(-0.02, 0.02)
        v = [bm.verts.new(p) for p in ((x - w, y - 0.035, top), (x + w, y - 0.035, top),
                                         (x + w * 0.7 + r.uniform(-0.03, 0.03), y - 0.05, top - length),
                                         (x - w * 0.7, y - 0.05, top - length))]
        bm.faces.new(v).material_index = 1 + k % 2
    return make("RibbonPost", bm, ["Driftwood", "Linen", "Russet"], at=at, smooth=50)


def bone_pile(seed=7, at=(0, 0, 0)):
    """Bones dragged out of a lair and left in a heap: long bones with knobbed ends crossed every
    way, a skull among them."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(9):
        a = r.uniform(0, math.pi)
        c = Vector((r.uniform(-0.35, 0.35), r.uniform(-0.3, 0.3), 0.04 + 0.03 * k))
        d = Vector((math.cos(a), math.sin(a), r.uniform(-0.15, 0.15))) * r.uniform(0.18, 0.3)
        N.rod(bm, [c - d, c + d], 0.025, sides=4)
        for e in (c - d, c + d):
            A.loft(bm, [(e - d.normalized() * 0.03, 0, 0), (e, 0.045, 0.045), (e + d.normalized() * 0.04, 0, 0)],
                   sides=4, ref=(0, 0, 1))
    skull = Vector((0.15, -0.25, 0.12))
    A.loft(bm, [(skull + Vector((0, 0.12, 0)), 0, 0), (skull + Vector((0, 0.06, 0)), 0.1, 0.09),
                (skull + Vector((0, -0.06, -0.01)), 0.09, 0.08), (skull + Vector((0, -0.13, -0.03)), 0.05, 0.05),
                (skull + Vector((0, -0.15, -0.03)), 0, 0)], sides=6, ref=(0, 0, 1))
    for s in (-1, 1):
        A.paint(bm, skull + Vector((s * 0.04, -0.1, 0.02)), 1, facing=(0, -1, 0.3), size=0.6)
    return make("BonePile", bm, ["SaintBone", "Sable"], at=at, smooth=40)


def nest(seed=8, at=(0, 0, 0)):
    """A nest of sticks and dry grass on the ground inside a mouth, big enough to make a visitor
    wonder what lies in it."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(26):
        a = r.uniform(0, 2 * math.pi)
        rad = r.uniform(0.55, 0.8)
        c = Vector((rad * math.cos(a), rad * math.sin(a), r.uniform(0.04, 0.22)))
        t = Vector((-math.sin(a), math.cos(a), r.uniform(-0.3, 0.3))) * r.uniform(0.25, 0.45)
        N.rod(bm, [c - t, c + t], r.uniform(0.018, 0.03), mat=k % 2, sides=3)
    A.prism(bm, 8, 0.6, 0.6, 0.0, 0.05, (0, 0), mat_index=2, cap_top=True)
    return make("Nest", bm, ["Bark", "Driftwood", "Wheat"], at=at, smooth=40)


def hide_rack(seed=9, at=(0, 0, 0)):
    """Hides stretched to dry on a pole frame: two forked posts, a pole across, three skins hung
    over it, ragged at their ends."""
    r = rng(seed)
    bm = bmesh.new()
    for x in (-0.9, 0.9):
        N.rod(bm, N.wobble(r, (x, 0, -0.05), (x, 0, 1.45), 3, 0.03), 0.05, sides=5, taper=0.85)
    N.rod(bm, [(-1.05, 0, 1.42), (1.05, 0, 1.45)], 0.04, sides=5)
    for k, x in enumerate((-0.55, 0.0, 0.55)):
        w = r.uniform(0.2, 0.28)
        front, back = r.uniform(0.7, 1.0), r.uniform(0.5, 0.8)
        vs = [bm.verts.new(p) for p in ((x - w, -0.03, 1.47), (x + w, -0.03, 1.47),
                                          (x + w * 1.15, -0.1, 1.47 - front), (x - w * 0.9, -0.08, 1.47 - front - 0.08),
                                          (x - w * 0.9, 0.08, 1.47 - back), (x + w * 1.1, 0.1, 1.47 - back + 0.06))]
        bm.faces.new((vs[0], vs[1], vs[2], vs[3])).material_index = 1 + k % 2
        bm.faces.new((vs[1], vs[0], vs[4], vs[5])).material_index = 1 + k % 2
    return make("HideRack", bm, ["Driftwood", "Hide", "Umber"], at=at, smooth=50)


ASSETS = {
    "MasonryRubble": masonry_rubble,
    "Offering": offering,
    "RibbonPost": ribbon_post,
    "BonePile": bone_pile,
    "Nest": nest,
    "HideRack": hide_rack,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
