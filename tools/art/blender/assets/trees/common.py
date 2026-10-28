"""Trees, common. Trees and their felled states: alder, ash, hawthorn, pollard willow, elm, pine, birch, a sapling, a snag, stumps and a fallen log.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Trees/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\trees\\common.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import random
import sys

from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
import artlib.nature as N  # noqa: E402
from artlib.nature import WIND, blob, clump, cut_stub, leaf_mat, leafy, make, rod, wobble  # noqa: E402


def vase(bm, centre, z0, r0, z1, r1, sides, mat, uvs, rnd):
    """An open crown flaring upwards, its texture upside down so the leaf tips stand up at the rim:
    a pollard's fresh shoots."""
    cx, cy = centre
    lo, hi = [], []
    for k in range(sides):
        a = 2 * math.pi * k / sides
        j = rnd.uniform(0.85, 1.15)
        lo.append(bm.verts.new((cx + r0 * math.cos(a), cy + r0 * math.sin(a), z0)))
        hi.append(bm.verts.new((cx + r1 * j * math.cos(a), cy + r1 * j * math.sin(a), z1 + rnd.uniform(-0.15, 0.15))))
    for k in range(sides):
        n = (k + 1) % sides
        f = bm.faces.new((lo[k], lo[n], hi[n], hi[k]))
        f.material_index = mat
        uvs[f] = [(0, 1), (1, 1), (1, 0), (0, 0)]


def roots(bm, r, reach, n, rnd, mat=0, rot=0.0, rise=2.4):
    """Root flares: tapering roots that leave the trunk well above the ground and bend down into
    it, flattened as they go. rise: where they leave the trunk, in trunk radii above the ground."""
    for k in range(n):
        a = rot + 2 * math.pi * k / n + rnd.uniform(-0.3, 0.3)
        d = Vector((math.cos(a), math.sin(a), 0))
        R = reach * rnd.uniform(0.8, 1.2)
        A.loft(bm, [(d * r * 0.3 + Vector((0, 0, r * rise)), r * 0.5, r * 0.5),
                    (d * (r * 0.9) + Vector((0, 0, r * min(0.9, rise * 0.4))), r * 0.42, r * 0.36),
                    (d * (r + (R - r) * 0.55) + Vector((0, 0, r * 0.2)), r * 0.28, r * 0.16),
                    (d * R + Vector((0, 0, -0.03)), 0, 0)], sides=5, ref=(0, 0, 1), mats=mat)


def alder(seed=11, at=(0, 0, 0)):
    """The basin's common tree, as the mere's people keep it: a coppice stool with three stems
    standing up and a little apart, each carrying a tall narrow clump of round leaves, so the tree
    reads as a tight upright cluster."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    blob(bm, (0, 0, 0.12), (0.38, 0.34, 0.3), 1, rnd, jitter=(0.8, 1.15), mat=0, squash_below=-0.02)
    for k, h in enumerate((3.6, 3.15, 2.9)):          # every clump clears a head (the player is 1.9)
        a = 2 * math.pi * k / 3 + rnd.uniform(-0.3, 0.3) + 0.4
        out = Vector((math.cos(a), math.sin(a), 0))
        top = out * 0.4 + Vector((0, 0, h))
        rod(bm, wobble(rnd, out * 0.12 + Vector((0, 0, 0.2)), top, 4, 0.05), 0.075, taper=0.55)
        clump(bm, top + out * 0.1 + Vector((0, 0, 0.05)), (0.55, 0.55, 0.95), rnd, 1, 2, uvs)
    return leafy("Alder", bm, uvs, ["Bark", leaf_mat("Foliage"), "Leaf1"], at)


def ash(seed=5, at=(0, 0, 0)):
    """The tall tree of the basin: a straight trunk forking high into upswept limbs, small light
    clumps of leaflets at their tips with sky showing between them, an open domed crown."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    fork = Vector((0.1, 0.05, 2.6))
    stem = wobble(rnd, (0, 0, -0.05), fork, 4, 0.04)
    rod(bm, stem + [fork + Vector((0.05, 0.02, 0.7)), fork + Vector((0.02, 0, 1.3))], 0.17, sides=6, taper=0.3)
    roots(bm, 0.17, 0.6, 4, rnd, rot=0.3)
    limbs = 5
    for k in range(limbs):
        a = 2 * math.pi * k / limbs + rnd.uniform(-0.25, 0.25)
        out = Vector((math.cos(a), math.sin(a), 0))
        start = fork + Vector((0, 0, rnd.uniform(-0.5, 0.2)))
        tip = fork + out * rnd.uniform(1.0, 1.35) + Vector((0, 0, rnd.uniform(1.0, 1.5)))
        mid = start.lerp(tip, 0.5) + out * 0.15 - Vector((0, 0, 0.15))
        rod(bm, [start, mid, tip], 0.07, taper=0.5)
        clump(bm, tip + Vector((0, 0, 0.1)), (0.62, 0.62, 0.45), rnd, 1, 2, uvs)
    clump(bm, fork + Vector((0.02, 0, 1.4)), (0.6, 0.6, 0.48), rnd, 1, 2, uvs)
    return leafy("Ash", bm, uvs, ["Bark", leaf_mat("Ashleaf"), "Leaf1"], at)


def hawthorn(seed=3, at=(0, 0, 0)):
    """A thorn on the open heath, flagged by the wind: a low trunk bent almost flat downwind and
    one dense mass of small dark leaves streaming away from the weather, red with haws."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    lift = 1.1                                    # the crown clears a head walking under it (the player is 1.9)
    pts = [Vector((0, 0, -0.05)), Vector((0.08, 0.0, 0.7 + lift)), WIND * 0.55 + Vector((0, 0, 1.3 + lift)),
           WIND * 1.2 + Vector((0, 0, 1.6 + lift))]
    rod(bm, pts, 0.14, sides=6, taper=0.4)
    roots(bm, 0.14, 0.45, 3, rnd, rot=2.0)
    for d, side, z, radii in ((1.25, 0.0, 1.8, (1.1, 0.75, 0.6)), (0.55, 0.3, 1.45, (0.6, 0.5, 0.42)),
                              (1.95, -0.15, 1.45, (0.55, 0.45, 0.35))):
        c = WIND * d + Vector((-WIND.y, WIND.x, 0)) * side + Vector((0, 0, z))
        clump(bm, c + Vector((0, 0, lift)), radii, rnd, 1, 2, uvs)
    return leafy("Hawthorn", bm, uvs, ["Bark", leaf_mat("Thorn"), "Leaf1"], at)


def pollard(seed=4, at=(0, 0, 0)):
    """A pollard willow on the bank: a short fat trunk cut at head height long ago, swollen into a
    knuckle, with a crown of rods standing up from it and their silvery leaves flaring out."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    rod(bm, wobble(rnd, (0, 0, -0.05), (0.08, 0.02, 1.45), 3, 0.05), 0.24, sides=7, taper=0.85)
    roots(bm, 0.22, 0.55, 4, rnd)
    head = Vector((0.08, 0.02, 1.55))
    blob(bm, head, (0.36, 0.34, 0.24), 1, rnd, jitter=(0.85, 1.12), mat=0)
    for k in range(7):
        a = 2 * math.pi * k / 7 + rnd.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), 0))
        rod(bm, [head + out * 0.15 + Vector((0, 0, 0.1)), head + out * 0.45 + Vector((0, 0, 0.85)),
                 head + out * 0.75 + Vector((0, 0, 1.45 + rnd.uniform(-0.2, 0.2)))], 0.025, sides=3, taper=0.5)
    vase(bm, (head.x, head.y), head.z + 0.1, 0.35, head.z + 1.45, 1.15, 8, 1, uvs, rnd)
    vase(bm, (head.x, head.y), head.z + 0.05, 0.22, head.z + 1.0, 0.55, 6, 1, uvs, rnd)
    return leafy("Pollard", bm, uvs, ["DeadWood", leaf_mat("Lance")], at)


def stool_cut(seed=11, at=(0, 0, 0)):
    """The alder's coppice stool after Woodcutting: the knuckled stool and three stems cut low,
    their fresh faces pale. (A tree's depleted state.)"""
    rnd = random.Random(seed)
    bm = bmesh.new()
    blob(bm, (0, 0, 0.12), (0.38, 0.34, 0.3), 1, rnd, jitter=(0.8, 1.15), mat=0, squash_below=-0.02)
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.4
        cut_stub(bm, (0.17 * math.cos(a), 0.17 * math.sin(a), 0.15), 0.08, 0.22 + 0.06 * k, 0.05, rnd)
    return make("AlderStool", bm, ["Bark", "Ash"], smooth_angle=50, at=at)


def ash_stump(seed=5, at=(0, 0, 0)):
    """The ash after Woodcutting: a stout stump with one slanted cut, its roots gripping the ground
    below the cut."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    cut_stub(bm, (0, 0, -0.05), 0.17, 0.45, 0.07, rnd, sides=7)
    roots(bm, 0.17, 0.6, 4, rnd, rot=0.3, rise=1.3)
    return make("AshStump", bm, ["Bark", "Ash"], smooth_angle=50, at=at)


def hawthorn_stump(seed=3, at=(0, 0, 0)):
    """The hawthorn after Woodcutting: a low stump still leaning downwind, cut flat across."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    cut_stub(bm, (0, 0, -0.05), 0.13, 0.32, 0.03, rnd, lean=(WIND.x * 0.12, WIND.y * 0.12))
    roots(bm, 0.13, 0.45, 3, rnd, rot=2.0, rise=1.2)
    return make("HawthornStump", bm, ["Bark", "Ash"], smooth_angle=50, at=at)


def pollard_stump(seed=4, at=(0, 0, 0)):
    """The pollard after Woodcutting: its fat trunk felled at the height an axe swings, a broad
    slanted cut, roots gripping the bank."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    cut_stub(bm, (0, 0, -0.05), 0.24, 0.42, 0.07, rnd, sides=7)
    roots(bm, 0.22, 0.6, 4, rnd, rise=1.2)
    return make("PollardStump", bm, ["DeadWood", "Ash"], smooth_angle=50, at=at)


def snag(seed=2, at=(0, 0, 0)):
    """A dead tree stripped silver by the wind: the trunk leaning downwind and broken off in a
    jagged point, two stubs of limbs flagging the same way."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    pts = wobble(rnd, (0, 0, -0.05), WIND * 0.45 + Vector((0, 0, 2.3)), 4, 0.06)
    rod(bm, pts, 0.15, sides=6, taper=0.55, tip=pts[-1] + WIND * 0.12 + Vector((0, 0, 0.45)))
    roots(bm, 0.15, 0.5, 4, rnd, rot=0.6, rise=1.2)
    for z, lift, length in ((1.2, 0.35, 0.8), (1.75, 0.5, 0.55)):
        base = WIND * (0.45 * z / 2.3) + Vector((0, 0, z))
        A.spike(bm, base, 0.06, base + WIND * length + Vector((0, 0, lift)), sides=4)
    A.spike(bm, pts[1] + Vector((0, 0, 0.1)), 0.05, pts[1] - WIND * 0.35 + Vector((0, 0.1, 0.25)), sides=4)
    return make("Snag", bm, ["Driftwood"], smooth_angle=45, at=at)


def elm(seed=8, at=(0, 0, 0)):
    """The elm, the basin's great tree: a short heavy trunk forking low into three limbs that rise
    steeply and spread like a vase, under one broad crown of big overlapping clumps, wide and
    flat-topped, narrow at the waist; broad deep-green leaves."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    fork = Vector((0.05, 0.0, 1.5))
    roots(bm, 0.22, 0.7, 4, rnd, rot=0.8)
    for k in range(3):
        a = 2 * math.pi * k / 3 + rnd.uniform(-0.25, 0.25)
        out = Vector((math.cos(a), math.sin(a), 0))
        top = fork + out * rnd.uniform(1.0, 1.2) + Vector((0, 0, rnd.uniform(2.2, 2.5)))
        # Each limb grows out of the one trunk: the trunk's path runs on into it.
        path = [Vector((0, 0, -0.05)), Vector((0.03, 0.0, 0.8)), fork, fork.lerp(top, 0.5) + out * 0.12, top]
        rod(bm, path, 0.22 if k == 0 else 0.2, sides=7, taper=0.3)
        clump(bm, top + out * 0.2 + Vector((0, 0, 0.35)), (1.2, 1.2, 0.85), rnd, 1, 2, uvs)
    clump(bm, fork + Vector((0, 0, 3.15)), (0.95, 0.95, 0.7), rnd, 1, 2, uvs)          # the crown's middle
    return leafy("Elm", bm, uvs, ["Bark", leaf_mat("Elmleaf"), "Leaf1"], at)


def elm_stump(seed=8, at=(0, 0, 0)):
    """The elm after Woodcutting: a broad low stump, its cut showing where the trunk was about to
    fork, heavy roots."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    cut_stub(bm, (0, 0, -0.05), 0.24, 0.5, 0.08, rnd, sides=7)
    roots(bm, 0.22, 0.7, 4, rnd, rot=0.8, rise=1.3)
    return make("ElmStump", bm, ["Bark", "Ash"], smooth_angle=50, at=at)


def pine(seed=7, at=(0, 0, 0)):
    """A pine for the hills and the spine: a tall trunk, bare for most of its height and red-barked
    above, a couple of dead branch stubs, and flat dark clumps of needles on short limbs at the top,
    held out downwind."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    top = WIND * 0.3 + Vector((0, 0, 4.3))
    pts = wobble(rnd, (0, 0, -0.05), top, 5, 0.06)
    secs = [(p, 0.17 - 0.1 * i / (len(pts) - 1), 0.17 - 0.1 * i / (len(pts) - 1)) for i, p in enumerate(pts)]
    A.loft(bm, secs, sides=6, ref=(1, 0, 0), mats=[0, 0, 1, 1], roll=math.pi / 6)
    roots(bm, 0.17, 0.55, 3, rnd, rot=0.4)
    for z, a in ((1.7, 2.5), (2.3, 0.9)):
        base = pts[0].lerp(top, z / 4.3)
        A.spike(bm, base, 0.04, base + Vector((0.4 * math.cos(a), 0.4 * math.sin(a), 0.08)), sides=3, mat=0)
    limbs = ((3.3, 0.3, 0.9), (3.6, 2.6, 0.75), (3.9, 4.4, 0.7))
    for z, a, reach in limbs:
        base = pts[0].lerp(top, z / 4.3)
        out = (Vector((math.cos(a), math.sin(a), 0)) + WIND * 0.6).normalized()
        tip = base + out * reach + Vector((0, 0, 0.12))
        rod(bm, [base, tip], 0.05, mat=1, sides=4, taper=0.6)
        clump(bm, tip + Vector((0, 0, 0.1)), (0.8, 0.65, 0.38), rnd, 2, 3, uvs)
    clump(bm, top + Vector((0, 0, 0.15)), (0.65, 0.6, 0.42), rnd, 2, 3, uvs)
    return leafy("Pine", bm, uvs, ["Bark", "Brick", leaf_mat("Needles"), "Leaf1"], at)


def pine_stump(seed=7, at=(0, 0, 0)):
    """The pine after Woodcutting: a tall narrow stump, a slanted cut, three roots."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    cut_stub(bm, (0, 0, -0.05), 0.17, 0.48, 0.08, rnd, sides=6)
    roots(bm, 0.17, 0.55, 3, rnd, rot=0.4, rise=1.3)
    return make("PineStump", bm, ["Bark", "Ash"], smooth_angle=50, at=at)


def fallen_log(seed=1, at=(0, 0, 0), rot=0.0):
    """A trunk the wind brought down long ago, lying along X: bedded into the ground, its top grown
    over with moss, one end snapped into splinters, the other a stub of broken roots; brackets of
    fungus along its side."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    r = 0.22
    pts = N.wobble(rnd, (-1.25, 0, r * 0.75), (1.25, 0, r * 0.65), 5, 0.04)
    rings, faces = A.loft(bm, [(p, r * (1.06 - 0.12 * i / 4), r * (1.0 - 0.1 * i / 4)) for i, p in enumerate(pts)],
                          sides=8, ref=(0, 0, 1), mats=0)
    for f in faces:
        if len(f.verts) == 8:
            f.material_index = 1                               # the broken ends' wood
            continue
        c = f.calc_center_median()
        axis_z = min(pts, key=lambda p: abs(p.x - c.x)).z
        if c.z - axis_z > r * 0.55:
            f.material_index = 2                               # moss along the top
    end = pts[-1]
    for k in range(6):                                         # the snapped end's splinters
        a = 2 * math.pi * k / 6 + rnd.uniform(-0.2, 0.2)
        b = end + Vector((-0.02, math.cos(a) * r * 0.55, math.sin(a) * r * 0.55))
        A.spike(bm, b, 0.06, b + Vector((rnd.uniform(0.12, 0.3), math.cos(a) * 0.03, math.sin(a) * 0.03)), sides=3, mat=1)
    root = pts[0]
    for k in range(5):                                         # the broken roots
        a = 2 * math.pi * k / 5 + rnd.uniform(-0.3, 0.3)
        d = Vector((-0.6, math.cos(a), math.sin(a))).normalized()
        if d.z < -0.3:
            continue
        N.branch(bm, root + Vector((0.08, 0, 0)), root + d * rnd.uniform(0.35, 0.55), 0.08, None, mat=0, sides=4)
    for k in range(3):                                         # bracket fungus on the shady side
        x = rnd.uniform(-0.7, 0.8)
        c = Vector((x, -r * 0.95, r * rnd.uniform(0.6, 0.95)))
        s = rnd.uniform(0.07, 0.11)
        A.hull(bm, [c + Vector((s * math.cos(t), -s * 0.8 * abs(math.sin(t)), dz))
                    for t in (0.0, 0.8, 1.6, 2.4, 3.14) for dz in (0.0, 0.025)], 3)
    return N.make("FallenLog", bm, ["Bark", "DeadWood", "Moss", "Ochre"], smooth_angle=45, at=at, rot=rot)


def birch(seed=3, at=(0, 0, 0)):
    """A birch on poor ground: a slender pale trunk with dark scars, leaning a little, thin limbs
    swept up and out, the small light leaves hanging from them in loose strands."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    top = Vector((0.25, 0.1, 4.2))
    N.rod(bm, N.wobble(rnd, (0, 0, -0.05), top, 10, 0.04), 0.12, sides=6, taper=0.35)
    for f in list(bm.faces):                                   # dark scars round the trunk
        if 0.25 < f.calc_center_median().z < 3.8 and rnd.random() < 0.35:
            f.material_index = 3
    roots(bm, 0.12, 0.35, 3, rnd, rise=1.8)
    for k in range(7):
        a = 2.39996 * k + rnd.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), 0))
        start = top * ((1.9 + 0.33 * k) / top.z)
        tip = start + out * rnd.uniform(0.8, 1.15) + Vector((0, 0, rnd.uniform(0.5, 0.9)))
        N.rod(bm, [start, start.lerp(tip, 0.5) + out * 0.1, tip], 0.035, sides=4, taper=0.5)
        N.clump(bm, tip + Vector((0, 0, -0.2)), (0.4, 0.4, 0.62), rnd, 1, 2, uvs, sides=6)
    N.clump(bm, top + Vector((0, 0, 0.05)), (0.38, 0.38, 0.6), rnd, 1, 2, uvs, sides=6)
    return N.leafy("Birch", bm, uvs, ["Birch", N.leaf_mat("Birchleaf"), "Leaf1", "Charcoal"], at)


def birch_stump(seed=3, at=(0, 0, 0)):
    """The birch after Woodcutting: a slim pale stump cut aslant."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    N.cut_stub(bm, (0, 0, -0.05), 0.12, 0.35, 0.05, rnd, sides=6)
    roots(bm, 0.12, 0.35, 3, rnd, rise=1.5)
    return N.make("BirchStump", bm, ["Birch", "Ash"], smooth_angle=50, at=at)


def sapling(seed=2, at=(0, 0, 0)):
    """A young tree planted out: a thin stem tied to a stake against the wind, one small round head
    of leaves, a ring of dug earth at its foot."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    A.prism(bm, 8, 0.3, 0.26, -0.02, 0.02, mat_index=3)
    N.rod(bm, N.wobble(rnd, (0, 0, -0.05), (0.06, 0.02, 1.5), 4, 0.025), 0.035, sides=5, taper=0.5)
    N.rod(bm, [(-0.1, 0.0, -0.1), (-0.08, 0.0, 1.0)], 0.03, mat=4, sides=4, tip=(-0.08, 0.0, 1.05))
    A.loft(bm, [((-0.04, 0, 0.8), 0.08, 0.045), ((-0.04, 0, 0.84), 0.08, 0.045)], sides=6, ref=(0, 1, 0), mats=4)
    N.clump(bm, (0.06, 0.02, 1.45), (0.4, 0.4, 0.5), rnd, 1, 2, uvs, sides=7)
    return N.leafy("Sapling", bm, uvs, ["Bark", N.leaf_mat("Foliage"), "Leaf1", "Furrow", "DeadWood"], at)


def rotten_stump(seed=5, at=(0, 0, 0)):
    """An old stump rotting where a tree fell long ago: broken off in jagged points, hollow and dark
    at the heart, green with moss on the weather side, toadstools at its foot."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    r, n = 0.3, 9
    lo, hi, inner = [], [], []
    for k in range(n):
        a = 2 * math.pi * k / n
        d = Vector((math.cos(a), math.sin(a), 0))
        lo.append(bm.verts.new(d * r * 1.12 + Vector((0, 0, -0.05))))
        hi.append(bm.verts.new(d * r + Vector((0, 0, rnd.uniform(0.45, 0.75) if k % 2 else rnd.uniform(0.25, 0.4)))))
        inner.append(bm.verts.new(d * r * 0.6 + Vector((0, 0, 0.18))))
    for k in range(n):
        m = (k + 1) % n
        f = bm.faces.new((lo[k], lo[m], hi[m], hi[k]))
        f.material_index = 2 if f.calc_center_median().x < -0.2 else 0      # moss on the weather side
        bm.faces.new((hi[k], hi[m], inner[m], inner[k])).material_index = 1
    bm.faces.new(list(reversed(inner))).material_index = 3
    roots(bm, r, 0.6, 4, rnd, rise=0.9)
    for k in range(4):
        a = rnd.uniform(0.5, 2.6)
        c = Vector((0.38 * math.cos(a), 0.38 * math.sin(a), 0))
        h = rnd.uniform(0.06, 0.12)
        N.branch(bm, c - Vector((0, 0, 0.02)), c + Vector((0, 0, h)), 0.015, 0.015, mat=4, sides=4)
        A.loft(bm, [(c + Vector((0, 0, h - 0.01)), 0.06, 0.06), (c + Vector((0, 0, h + 0.035)), 0, 0)], sides=6,
               ref=(1, 0, 0), mats=5)
    return N.make("RottenStump", bm, ["DeadWood", "Umber", "Moss", "CryptEarth", "Linen", "Ochre"], smooth_angle=40, at=at)


ASSETS = {
    "Alder": alder,
    "AlderStool": stool_cut,
    "Ash": ash,
    "AshStump": ash_stump,
    "Hawthorn": hawthorn,
    "HawthornStump": hawthorn_stump,
    "Pollard": pollard,
    "PollardStump": pollard_stump,
    "Snag": snag,
    "Elm": elm,
    "ElmStump": elm_stump,
    "Pine": pine,
    "PineStump": pine_stump,
    "FallenLog": fallen_log,
    "Birch": birch,
    "BirchStump": birch_stump,
    "Sapling": sapling,
    "RottenStump": rotten_stump,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
