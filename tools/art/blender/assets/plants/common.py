"""Plants, common. Plants: undergrowth, reeds, crops and cabbages, nettles, dry ground cover, herbs, mushrooms, berry bushes (each with its picked state), and the overgrown cave's roots, ivy and fern.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Plants/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\plants\\common.py")
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
import artlib.caves as caves  # noqa: E402
from artlib.caves import rng  # noqa: E402
from artlib.nature import blob, branch, clump, leaf_mat, leafy, make, rod  # noqa: E402


def grounded(x, y, radii):
    """The centre for a clump of these radii standing on the ground at (x, y)."""
    return (x, y, radii[2] * 0.3)


def gorse(seed=9, at=(0, 0, 0)):
    """A gorse bush for the heath: four spiny cushions heaped together, the tallest at the back,
    their hems of spines reaching the ground, a few yellow flowers in the tangle."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    for (x, y), r in (((0.0, 0.1), (0.5, 0.45, 0.62)), ((0.45, -0.15), (0.42, 0.38, 0.48)),
                      ((-0.42, -0.1), (0.4, 0.36, 0.42)), ((0.05, -0.45), (0.36, 0.3, 0.36))):
        clump(bm, grounded(x, y, r), r, rnd, 0, 1, uvs, sides=6)
    return leafy("Gorse", bm, uvs, [leaf_mat("Gorse"), "Leaf1"], at)


def reeds(seed=3, at=(0, 0, 0)):
    """A clump of reeds standing in the shallows: bent blades and three seed heads."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for _ in range(11):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 0.3)
        base = Vector((d * math.cos(a), d * math.sin(a), -0.2))
        h = rnd.uniform(0.8, 1.35)
        lean_dir = Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(0.15, 0.4)
        w = 0.04
        side = Vector((-math.sin(a + 1.2), math.cos(a + 1.2), 0)) * w
        mid = base + Vector((0, 0, h * 0.55)) + lean_dir * 0.3
        tip = base + Vector((0, 0, h)) + lean_dir
        v = [bm.verts.new(p) for p in (base - side, base + side, mid + side * 0.7, mid - side * 0.7, tip)]
        bm.faces.new((v[0], v[1], v[2], v[3]))
        bm.faces.new((v[3], v[2], v[4]))
    for k in range(3):
        a = rnd.uniform(0, 2 * math.pi)
        base = Vector((0.1 * math.cos(a), 0.1 * math.sin(a), -0.2))
        top = base + Vector((0.08 * math.cos(a), 0.08 * math.sin(a), rnd.uniform(1.25, 1.5)))
        branch(bm, base, top, 0.012, 0.012, mat=0, sides=3)
        head = top - Vector((0, 0, 0.02))
        A.loft(bm, [(head, 0, 0), (head + Vector((0, 0, 0.06)), 0.035, 0.035),
                    (head + Vector((0, 0, 0.2)), 0.035, 0.035), (head + Vector((0, 0, 0.26)), 0, 0)],
               sides=4, ref=(1, 0, 0), mats=1)
    return make("Reeds", bm, ["Reed", "Bark"], at=at)


def tussock(seed=1, at=(0, 0, 0)):
    """A tussock of moor grass: a low dense mound with a few stiff blades standing out of it,
    their tips dried pale."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    blob(bm, (0, 0, 0.1), (0.3, 0.27, 0.2), 1, rnd, jitter=(0.85, 1.15), mat=0, squash_below=-0.02)
    for _ in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        out = Vector((math.cos(a), math.sin(a), 0))
        base = out * rnd.uniform(0.0, 0.15) + Vector((0, 0, 0.12))
        side = Vector((-out.y, out.x, 0)) * 0.03
        mid = base + out * 0.1 + Vector((0, 0, rnd.uniform(0.18, 0.28)))
        tip = mid + out * rnd.uniform(0.1, 0.22) + Vector((0, 0, rnd.uniform(0.05, 0.15)))
        v = [bm.verts.new(p) for p in (base - side, base + side, mid + side * 0.6, mid - side * 0.6, tip)]
        bm.faces.new((v[0], v[1], v[2], v[3])).material_index = 0
        bm.faces.new((v[3], v[2], v[4])).material_index = 1
    return make("Tussock", bm, ["Heath", "Wheat"], smooth_angle=60, at=at)


def dock(seed=1, at=(0, 0, 0)):
    """Dock gone to seed: broad leaves at the foot and a stiff stalk branching into rust-red seed
    spikes, the basin's one warm note in the grass."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(3):
        a = 2 * math.pi * k / 3 + rnd.uniform(-0.3, 0.3)
        d = Vector((math.cos(a), math.sin(a), 0))
        s = Vector((-d.y, d.x, 0))
        v = [bm.verts.new(p) for p in (Vector((0, 0, 0.03)), d * 0.15 + s * 0.09 + Vector((0, 0, 0.08)),
                                         d * 0.36 + Vector((0, 0, 0.04)), d * 0.15 - s * 0.09 + Vector((0, 0, 0.08)))]
        bm.faces.new(v).material_index = 0
    lean = Vector((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), 0))
    top = Vector((0, 0, 0.85)) + lean
    rod(bm, [(0, 0, 0), Vector((0, 0, 0.45)) + lean * 0.5, top], 0.014, mat=1, sides=3)
    A.loft(bm, [(top - Vector((0, 0, 0.04)), 0, 0), (top + Vector((0, 0, 0.04)), 0.03, 0.03),
                (top + Vector((0, 0, 0.16)), 0.025, 0.025), (top + Vector((0, 0, 0.22)), 0, 0)],
           sides=4, ref=(1, 0, 0), mats=2)
    for k in range(3):
        a = 2 * math.pi * k / 3 + rnd.uniform(0, 1)
        z = 0.45 + 0.12 * k
        base = Vector((0, 0, z)) + lean * (z / 0.85)
        tip = base + Vector((0.18 * math.cos(a), 0.18 * math.sin(a), 0.22))
        A.spike(bm, base, 0.012, base.lerp(tip, 0.4), sides=3, mat=1)
        A.loft(bm, [(base.lerp(tip, 0.35), 0, 0), (base.lerp(tip, 0.55), 0.025, 0.025),
                    (base.lerp(tip, 0.9), 0.02, 0.02), (tip, 0, 0)], sides=4, ref=(0, 0, 1), mats=2)
    return make("Dock", bm, ["Leaf4", "Bark", "IronOre"], smooth_angle=50, at=at)


def flax(seed=5, at=(0, 0, 0), picked=False):
    """A stand of flax (Foraging's fibre): slender upright stalks, each with a small pale-blue
    flower. picked: the stalks pulled, a few short broken stems left in the ground."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for i in range(16):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 0.32)
        base = Vector((d * math.cos(a), d * math.sin(a), -0.02))
        if picked:
            if i % 3 == 0:
                rod(bm, [base, base + Vector((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), rnd.uniform(0.1, 0.2)))],
                    0.012, mat=0, sides=3)
            continue
        h = rnd.uniform(0.75, 1.0)
        top = base + Vector((rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1), h))
        rod(bm, [base, base.lerp(top, 0.5) + Vector((rnd.uniform(-0.03, 0.03), 0, 0)), top], 0.012, mat=0, sides=3)
        A.loft(bm, [(top - Vector((0, 0, 0.01)), 0, 0), (top + Vector((0, 0, 0.03)), 0.05, 0.05),
                    (top + Vector((0, 0, 0.045)), 0, 0)], sides=5, ref=(1, 0, 0), mats=1)
    name = "FlaxPicked" if picked else "Flax"
    return make(name, bm, ["Leaf5", "Moonlight"] if not picked else ["Wheat"], smooth_angle=60, at=at)


def herb(seed=6, at=(0, 0, 0), picked=False):
    """A herb (Foraging): a low rosette of broad leaves curling up at the tips, a stalk from its
    middle with a head of small pale flowers. picked: the stalk and the best leaves taken, a few
    small leaves left flat on the ground."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    n = 4 if picked else 8
    for k in range(n):
        a = 2 * math.pi * k / n + rnd.uniform(-0.2, 0.2)
        d = Vector((math.cos(a), math.sin(a), 0))
        s = Vector((-d.y, d.x, 0))
        length = rnd.uniform(0.18, 0.24) if picked else rnd.uniform(0.28, 0.38)
        lift = 0.03 if picked else 0.12
        v = [bm.verts.new(p) for p in (Vector((0, 0, 0.03)), d * length * 0.45 + s * 0.08 + Vector((0, 0, lift * 0.4)),
                                         d * length + Vector((0, 0, lift)), d * length * 0.45 - s * 0.08 + Vector((0, 0, lift * 0.4)))]
        bm.faces.new(v).material_index = 0
    if not picked:
        top = Vector((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), 0.5))
        rod(bm, [(0, 0, 0.02), Vector((0, 0, 0.28)), top], 0.014, mat=0, sides=3)
        for k in range(5):
            a = 2 * math.pi * k / 5
            c = top + Vector((0.05 * math.cos(a), 0.05 * math.sin(a), rnd.uniform(-0.03, 0.02)))
            A.loft(bm, [(c - Vector((0, 0, 0.025)), 0, 0), (c, 0.035, 0.035), (c + Vector((0, 0, 0.025)), 0, 0)],
                   sides=4, ref=(1, 0, 0), mats=1)
    return make("HerbPicked" if picked else "Herb", bm, ["Leaf4", "SaintBone"], smooth_angle=60, at=at)


def mushrooms(seed=7, at=(0, 0, 0), picked=False):
    """A cluster of mushrooms (Foraging): pale stalks under broad ochre caps, one large, the rest
    smaller, leaning out from each other. picked: the caps taken, broken stalks and two buttons left."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    sizes = [1.6, 1.2, 0.95, 0.8, 0.65]
    for i, k in enumerate(sizes):
        a = rnd.uniform(0, 2 * math.pi)
        d = 0.0 if i == 0 else rnd.uniform(0.2, 0.38)
        out = Vector((math.cos(a), math.sin(a), 0))
        base = out * d + Vector((0, 0, -0.02))
        if picked and i < 3:
            rod(bm, [base, base + Vector((0, 0, 0.06 * k))], 0.035 * k, mat=0, sides=5)
            continue
        if picked:
            k *= 0.5
        h = 0.22 * k
        top = base + out * 0.04 * k + Vector((0, 0, h))
        rod(bm, [base, top], 0.03 * k, mat=0, sides=5)
        r = 0.13 * k
        A.loft(bm, [(top - Vector((0, 0, 0.01)), r * 0.35, r * 0.35), (top + Vector((0, 0, 0.0)), r, r),
                    (top + Vector((0, 0, 0.05 * k)), r * 0.75, r * 0.75), (top + Vector((0, 0, 0.08 * k)), 0, 0)],
                   sides=7, ref=(1, 0, 0), mats=1)
    return make("MushroomsPicked" if picked else "Mushrooms", bm, ["SaintBone", "Sandstone"], smooth_angle=60, at=at)


def lily_pads(seed=1, at=(0, 0, 0)):
    """Lily pads on still water: round flat leaves, each with its notch, lying at the surface (the
    origin), two small pale flowers among them."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    pads = []
    for _ in range(7):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 0.6)
        pads.append((Vector((d * math.cos(a), d * math.sin(a), 0.01)), rnd.uniform(0.12, 0.22)))
    for c, r in pads:
        notch = rnd.randrange(8)
        rot = rnd.uniform(0, 1)
        centre = bm.verts.new(c)
        ring = [bm.verts.new(c + Vector((r * math.cos(rot + 2 * math.pi * k / 8), r * math.sin(rot + 2 * math.pi * k / 8), 0)))
                for k in range(8)]
        for k in range(8):
            if k != notch:
                bm.faces.new((centre, ring[k], ring[(k + 1) % 8])).material_index = 0
    for c, r in pads[:2]:
        A.loft(bm, [(c + Vector((0, 0, 0.0)), 0, 0), (c + Vector((0, 0, 0.03)), 0.05, 0.05),
                    (c + Vector((0, 0, 0.06)), 0.035, 0.035), (c + Vector((0, 0, 0.07)), 0, 0)],
               sides=6, ref=(1, 0, 0), mats=1)
    return make("LilyPads", bm, ["Leaf4", "SaintBone"], smooth_angle=60, at=at)


def berry_bush_low(seed=21, at=(0, 0, 0), picked=False):
    """A Foraging bush, low and spreading: three broad cushions of dark leaves, blue-black berries.
    picked: the depleted node, the same mesh with the bare texture."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    for (x, y), r in (((0.0, 0.0), (0.62, 0.55, 0.55)), ((0.55, 0.25), (0.45, 0.4, 0.44)),
                      ((-0.5, 0.3), (0.42, 0.38, 0.4))):
        clump(bm, grounded(x, y, r), r, rnd, 0, 1, uvs, sides=7)
    tex = "BushDarkPicked" if picked else "BushDark"
    return leafy("BerryBushLow" + ("Picked" if picked else ""), bm, uvs, [leaf_mat(tex), "Leaf1"], at)


def berry_bush_tall(seed=22, at=(0, 0, 0), picked=False):
    """A Foraging bush, tall: two cushions of paler leaves stacked off-centre, red berries.
    picked: the same mesh with the bare texture."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    low = (0.5, 0.47, 0.75)
    clump(bm, grounded(0.0, 0.0, low), low, rnd, 0, 1, uvs, sides=7)
    clump(bm, (0.12, -0.08, 0.95), (0.38, 0.36, 0.45), rnd, 0, 1, uvs, sides=6)
    tex = "BushPalePicked" if picked else "BushPale"
    return leafy("BerryBushTall" + ("Picked" if picked else ""), bm, uvs, [leaf_mat(tex), "Leaf1"], at)


def card(bm, uvs, path, widths, side, mat, u0=0.0, u1=1.0):
    """A leaf card bent along `path` (points from its foot to its tip), `widths` across it along
    `side`, wearing the texture from v 0 at the foot to 1 at the tip, columns u0..u1."""
    rows = []
    for p, w in zip(path, widths):
        p = Vector(p)
        rows.append((bm.verts.new(p - side * w), bm.verts.new(p + side * w)))
    n = len(rows) - 1
    for i in range(n):
        (a0, a1), (b0, b1) = rows[i], rows[i + 1]
        f = bm.faces.new((a0, a1, b1, b0))
        f.material_index = mat
        uvs[f] = [(u0, i / n), (u1, i / n), (u1, (i + 1) / n), (u0, (i + 1) / n)]


def bracken(seed=1, at=(0, 0, 0)):
    """A stand of bracken: a dozen fronds rising from one root and arching outwards and over, each a
    bent card of the bracken texture, the tallest at waist height, tips turned tawny."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    for k in range(12):
        a = 2 * math.pi * k / 12 + rnd.uniform(-0.25, 0.25)
        out = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-out.y, out.x, 0))
        h = rnd.uniform(0.55, 0.95)
        reach = rnd.uniform(0.45, 0.75)
        base = out * rnd.uniform(0.02, 0.12)
        path = [base + Vector((0, 0, -0.03)), base + out * reach * 0.2 + Vector((0, 0, h * 0.55)),
                base + out * reach * 0.6 + Vector((0, 0, h * 0.95)), base + out * reach + Vector((0, 0, h * 0.75))]
        u0 = rnd.uniform(0, 0.6)
        card(bm, uvs, path, (0.1, 0.2, 0.22, 0.12), side, 0, u0, u0 + 0.4)
    return N.leafy("Bracken", bm, uvs, [N.leaf_mat("Bracken")], at)


def bramble(seed=1, at=(0, 0, 0)):
    """A bramble patch: a low tangled mound of dark leaves, pale flowers and berries, in three
    heaped cushions. (Canes arching out of it read as legs at game distance, so the leaves carry it.)"""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    for (x, y), r in (((0.0, 0.0), (0.55, 0.5, 0.5)), ((0.4, 0.25), (0.4, 0.36, 0.38)), ((-0.35, -0.2), (0.38, 0.34, 0.32))):
        N.clump(bm, grounded(x, y, r), r, rnd, 0, 1, uvs, sides=7)
    return N.leafy("Bramble", bm, uvs, [N.leaf_mat("Bramble"), "Leaf1"], at)


def wildflowers(seed=1, at=(0, 0, 0)):
    """Meadow flowers in a loose drift: thin stems, flat pale heads of yarrow and small yellow
    cups held above a few leaves; no bigger than a hand at game distance, so few and clear."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(4):                                       # leaves at the foot
        a = rnd.uniform(0, 2 * math.pi)
        d = Vector((math.cos(a), math.sin(a), 0))
        s = Vector((-d.y, d.x, 0))
        c = Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 0))
        v = [bm.verts.new(c + p) for p in (Vector((0, 0, 0.02)), d * 0.1 + s * 0.05 + Vector((0, 0, 0.06)),
                                           d * 0.22 + Vector((0, 0, 0.03)), d * 0.1 - s * 0.05 + Vector((0, 0, 0.06)))]
        bm.faces.new(v).material_index = 0
    for k in range(13):
        c = Vector((rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), 0))
        h = rnd.uniform(0.3, 0.55)
        top = c + Vector((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), h))
        N.branch(bm, c - Vector((0, 0, 0.02)), top, 0.01, 0.008, mat=0, sides=3)
        if k % 3:                                            # yarrow: a flat pale head
            r = rnd.uniform(0.05, 0.07)
            A.prism(bm, 6, r, r * 0.8, top.z - 0.01, top.z + 0.02, (top.x, top.y), mat_index=1)
        else:                                                # a buttercup
            A.loft(bm, [(top - Vector((0, 0, 0.01)), 0, 0), (top + Vector((0, 0, 0.03)), 0.04, 0.04),
                        (top + Vector((0, 0, 0.035)), 0, 0)], sides=5, ref=(1, 0, 0), mats=2)
    return N.make("Wildflowers", bm, ["Leaf4", "Yarrow", "Buttercup"], smooth_angle=50, at=at)


def heather(seed=1, at=(0, 0, 0)):
    """Heather on the moor: low springy cushions, dark and woody underneath, their tops a muted
    purple in flower, one cushion still green."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k, ((x, y), r) in enumerate((((0, 0), (0.42, 0.38, 0.3)), ((0.48, 0.18), (0.32, 0.3, 0.24)),
                                     ((-0.4, 0.22), (0.3, 0.26, 0.22)), ((0.1, -0.42), (0.3, 0.28, 0.2)))):
        faces = N.blob(bm, (x, y, r[2] * 0.25), r, 1, rnd, jitter=(0.82, 1.15), squash_below=-0.02)
        for f in faces:
            f.normal_update()
            f.material_index = (2 if k == 3 else 1) if f.normal.z > 0.35 else 0
    return N.make("Heather", bm, ["DeepWood", "Heather", "Sedge"], smooth_angle=60, at=at)


def thistles(seed=1, at=(0, 0, 0)):
    """Thistles in rough pasture: stiff stalks with spiny leaves sticking out, each crowned with a
    green bulb and a purple tuft."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(4):
        c = Vector((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), 0))
        h = rnd.uniform(0.6, 0.95)
        top = c + Vector((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), h))
        N.rod(bm, [c - Vector((0, 0, 0.02)), c.lerp(top, 0.5), top], 0.02, mat=0, sides=4, taper=0.7)
        for j in range(4):
            z = h * (0.2 + 0.18 * j)
            a = rnd.uniform(0, 2 * math.pi)
            base = c.lerp(top, z / h)
            tip = base + Vector((0.2 * math.cos(a), 0.2 * math.sin(a), 0.06)) * (1.2 - 0.2 * j)
            A.spike(bm, base, 0.035, tip, sides=3, mat=0)
        A.loft(bm, [(top - Vector((0, 0, 0.02)), 0, 0), (top + Vector((0, 0, 0.04)), 0.045, 0.045),
                    (top + Vector((0, 0, 0.09)), 0.035, 0.035)], sides=6, ref=(1, 0, 0), mats=0)
        A.loft(bm, [(top + Vector((0, 0, 0.085)), 0.03, 0.03), (top + Vector((0, 0, 0.16)), 0.05, 0.05),
                    (top + Vector((0, 0, 0.17)), 0, 0)], sides=6, ref=(1, 0, 0), mats=1)
    return N.make("Thistles", bm, ["Leaf3", "Thistle"], smooth_angle=40, at=at)


def brushwood(seed=1, at=(0, 0, 0)):
    """Brushwood cut from the coppice and bound into bundles for the fire and the oven: three
    faggots of rods, two lying, one leaning on them, each tied twice."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k, (c, d, lean) in enumerate((((0, -0.15, 0.12), (1, 0.1, 0), 0), ((0.05, 0.18, 0.12), (1, -0.05, 0), 0),
                                      ((-0.1, 0.0, 0.35), (1, 0.2, 0.35), 1))):
        c, d = Vector(c), Vector(d).normalized()
        side = d.cross(Vector((0, 0, 1))).normalized()
        up = side.cross(d)
        for j in range(9):
            a = 2 * math.pi * j / 9
            off = side * math.cos(a) * 0.08 + up * math.sin(a) * 0.08
            l = rnd.uniform(0.6, 0.72)
            p0 = c - d * l + off * rnd.uniform(0.5, 1.0)
            p1 = c + d * l + off * rnd.uniform(0.9, 1.5)
            N.rod(bm, [p0, p1], 0.018, mat=0, sides=3)
        for t in (-0.3, 0.3):
            A.loft(bm, [(c + d * (t - 0.025), 0.1, 0.1), (c + d * (t + 0.025), 0.1, 0.1)], sides=7,
                   ref=tuple(up), mats=1)
    return N.make("Brushwood", bm, ["DeadWood", "Umber"], smooth_angle=40, at=at)


def hanging_roots(seed=2, at=(0, 0, 0), height=2.25, span=1.6):
    """Roots hanging from under a lip across a mouth, thin and kinked, some long enough to brush a
    tall visitor's head."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(9):
        x = -span / 2 + span * (i + r.uniform(0.1, 0.9)) / 9
        y = r.uniform(-0.15, 0.15)
        length = r.uniform(0.35, 1.0)
        pts = [Vector((x, y, height + 0.1))]
        for k in range(1, 4):
            pts.append(pts[-1] + Vector((r.uniform(-0.08, 0.08), r.uniform(-0.05, 0.05), -length / 3)))
        N.rod(bm, pts, r.uniform(0.014, 0.026), sides=3, taper=0.4, tip=pts[-1] - Vector((0, 0, 0.08)))
    return caves.make("HangingRoots", bm, ["Bark"], at=at, smooth=50)


def ivy_drape(seed=3, at=(0, 0, 0), height=2.4, span=1.4):
    """Ivy falling from a ledge at `height` down a rock face: strands of leaf cards hanging side by
    side, each its own length, bowed out a little from the face."""
    r = rng(seed)
    bm = bmesh.new()
    uvs = {}
    n = 4
    for k in range(n):
        x0 = -span / 2 + span * k / n + r.uniform(-0.05, 0.05)
        x1 = x0 + span / n * r.uniform(1.0, 1.25)
        drop = r.uniform(1.0, 1.7)
        bulge = r.uniform(0.05, 0.14)
        top = [bm.verts.new((x, -0.02, height)) for x in (x0, x1)]
        mid = [bm.verts.new((x, -0.02 - bulge, height - drop * 0.5)) for x in (x0, x1)]
        low = [bm.verts.new((x, -0.02 - bulge * 0.6, height - drop)) for x in (x0, x1)]
        f = bm.faces.new((mid[0], mid[1], top[1], top[0]))
        uvs[f] = [(0, 0.5), (1, 0.5), (1, 1), (0, 1)]
        f = bm.faces.new((low[0], low[1], mid[1], mid[0]))
        uvs[f] = [(0, 0), (1, 0), (1, 0.5), (0, 0.5)]
    return N.leafy("IvyDrape", bm, uvs, [N.leaf_mat("Ivy")], at, smooth=60)


def fern(seed=4, at=(0, 0, 0)):
    """A fern at the foot of the rock where the damp comes out: fronds arching up and over, each a
    stem with leaflets along it."""
    r = rng(seed)
    bm = bmesh.new()
    for k in range(7):
        a = 2 * math.pi * k / 7 + r.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-out.y, out.x, 0))
        length = r.uniform(0.5, 0.75)
        pts = [Vector((0, 0, 0.02)), out * length * 0.35 + Vector((0, 0, length * 0.55)),
               out * length * 0.8 + Vector((0, 0, length * 0.5)), out * length + Vector((0, 0, length * 0.2))]
        for i in range(len(pts) - 1):
            w = 0.09 * (1 - i / 3)
            a0, a1 = pts[i], pts[i + 1]
            v = [bm.verts.new(p) for p in (a0 - side * w, a0 + side * w, a1 + side * w * 0.6, a1 - side * w * 0.6)]
            bm.faces.new(v).material_index = 0
    return caves.make("Fern", bm, ["Leaf4"], at=at, smooth=50)


def dead_grass(seed=2, at=(0, 0, 0)):
    """Dry grass where the fields give out into gravel, about a tile across: five low tufts of
    straw-pale blades lying over downwind, no green left in them, a little bare dust between."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(5):
        a, d = 2 * math.pi * k / 5 + rnd.uniform(-0.4, 0.4), rnd.uniform(0.12, 0.4)
        c = Vector((d * math.cos(a), d * math.sin(a), 0.0))
        for j in range(rnd.randint(6, 9)):
            b = rnd.uniform(0, 2 * math.pi)
            out = (Vector((math.cos(b), math.sin(b), 0)) * 0.5 + N.WIND).normalized()
            side = Vector((-out.y, out.x, 0)) * 0.025
            base = c + Vector((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), -0.01))
            mid = base + out * 0.07 + Vector((0, 0, rnd.uniform(0.1, 0.17)))
            tip = mid + out * rnd.uniform(0.1, 0.18) + Vector((0, 0, rnd.uniform(-0.04, 0.03)))
            v = [bm.verts.new(p) for p in (base - side, base + side, mid + side * 0.5, mid - side * 0.5, tip)]
            bm.faces.new((v[0], v[1], v[2], v[3])).material_index = 0
            bm.faces.new((v[3], v[2], v[4])).material_index = 1
    return make("DeadGrass", bm, ["Wheat", "Dust"], smooth_angle=60, at=at)


def dead_bush(seed=4, at=(0, 0, 0)):
    """A bush that died in a dry summer and stayed: a knot of grey stems from one root crown,
    forking twice into bare twigs, a few brown leaves still caught in it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    blob(bm, (0, 0, 0.03), (0.14, 0.12, 0.07), 0, rnd, mat=0, squash_below=0.0)

    def grow(base, d, length, r, depth):
        tip = base + d * length
        branch(bm, base, tip, r, None, mat=0, sides=4)
        if depth == 0:
            if rnd.random() < 0.35:
                A.hull(bm, [tuple(tip + Vector((rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04), rnd.uniform(-0.03, 0.03))))
                            for _ in range(4)], 1)
            return
        for _ in range(2):
            nd = (d + Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), rnd.uniform(-0.1, 0.4)))).normalized()
            grow(base.lerp(tip, rnd.uniform(0.5, 0.75)), nd, length * 0.6, r * 0.55, depth - 1)
    for k in range(5):
        a = 2 * math.pi * k / 5 + rnd.uniform(-0.3, 0.3)
        d = Vector((0.55 * math.cos(a), 0.55 * math.sin(a), 1.0)).normalized()
        grow(Vector((0, 0, 0.04)), d, rnd.uniform(0.45, 0.65), 0.028, 2)
    return make("DeadBush", bm, ["DeadWood", "Autumn"], smooth_angle=40, at=at)


def crops(seed=5, at=(0, 0, 0), ripe=True):
    """A strip of a field in crop, 2 x 2: three ridges of turned earth along X, a row of corn on each,
    every plant a few stiff stalks with an ear at the top. ripe=False: green, before the ears turn."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    EARTH, STALK, EAR = range(3)
    for y in (-0.65, 0.0, 0.65):
        A.loft(bm, [((-1.0, y, -0.02), 0.22, 0.08), ((1.0, y, -0.02), 0.22, 0.08)], sides=6, ref=(0, 0, 1), mats=EARTH)
        x = -0.9
        while x < 0.92:
            for _ in range(2):
                base = Vector((x + rnd.uniform(-0.05, 0.05), y + rnd.uniform(-0.06, 0.06), 0.03))
                h = rnd.uniform(0.6, 0.85) if ripe else rnd.uniform(0.4, 0.6)
                lean = Vector((rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08), 0)) + N.WIND * 0.05
                top = base + Vector((0, 0, h)) + lean
                rod(bm, [base, top], 0.008, mat=STALK, sides=3)
                A.loft(bm, [(top - Vector((0, 0, 0.01)), 0, 0), (top + Vector((0, 0, 0.04)), 0.022, 0.022),
                            (top + Vector((0, 0, 0.12)), 0, 0)], sides=3, ref=(1, 0, 0), mats=EAR)
            x += rnd.uniform(0.13, 0.18)
    colours = ["Furrow", "Wheat", "Thatch"] if ripe else ["Furrow", "Leaf5", "Leaf4"]
    return make("Crops" if ripe else "CropsGreen", bm, colours, smooth_angle=50, at=at)


def cabbages(seed=6, at=(0, 0, 0)):
    """A cottage's vegetable rows, 2 x 2: three ridges of dark earth along X, a row of cabbages on
    each, every one a ball of tight leaves in a rosette of looser outer ones, blue-green."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    EARTH, OUTER, HEART = range(3)
    for y in (-0.65, 0.0, 0.65):
        A.loft(bm, [((-1.0, y, -0.02), 0.25, 0.08), ((1.0, y, -0.02), 0.25, 0.08)], sides=6, ref=(0, 0, 1), mats=EARTH)
        for k in range(5):
            x = -0.8 + 0.4 * k + rnd.uniform(-0.04, 0.04)
            c = Vector((x, y + rnd.uniform(-0.04, 0.04), 0.05))
            for j in range(5):                                                                   # the outer leaves, cupped
                a = 2 * math.pi * j / 5 + rnd.uniform(-0.2, 0.2)
                d = Vector((math.cos(a), math.sin(a), 0))
                sd = Vector((-d.y, d.x, 0))
                v = [bm.verts.new(p) for p in (c, c + d * 0.12 + sd * 0.08 + Vector((0, 0, 0.06)), c + d * 0.2 + Vector((0, 0, 0.1)),
                                               c + d * 0.12 - sd * 0.08 + Vector((0, 0, 0.06)))]
                bm.faces.new(v).material_index = OUTER
            blob(bm, tuple(c + Vector((0, 0, 0.09))), (0.1, 0.1, 0.085), 1, rnd, jitter=(0.92, 1.06), mat=HEART)
    return make("Cabbages", bm, ["Furrow", "Leaf4", "Moor"], smooth_angle=50, at=at)


def nettles(seed=7, at=(0, 0, 0)):
    """A bed of nettles where the ground is rich and nobody goes: upright stalks in a clump, pairs of
    toothed dark leaves up each one, the tops drooping with their seed."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for _ in range(11):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 0.38)
        base = Vector((d * math.cos(a), d * math.sin(a), -0.02))
        h = rnd.uniform(0.5, 0.85)
        top = base + Vector((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), h))
        rod(bm, [base, top], 0.01, mat=0, sides=3)
        turn = rnd.uniform(0, math.pi)
        for k in range(3):
            p = base.lerp(top, 0.35 + 0.22 * k)
            for s in (-1, 1):                                                                    # leaves in opposite pairs, turning
                dd = Vector((math.cos(turn + k * 1.57) * s, math.sin(turn + k * 1.57) * s, 0))
                side = Vector((-dd.y, dd.x, 0)) * 0.035
                tip = p + dd * 0.13 - Vector((0, 0, 0.03))
                v = [bm.verts.new(q) for q in (p, p + dd * 0.06 + side + Vector((0, 0, 0.01)), tip, p + dd * 0.06 - side + Vector((0, 0, 0.01)))]
                bm.faces.new(v).material_index = 1
        A.loft(bm, [(top, 0, 0), (top + Vector((0.03, 0, -0.04)), 0.015, 0.015), (top + Vector((0.06, 0, -0.1)), 0, 0)],
               sides=3, ref=(0, 0, 1), mats=2)
    return make("Nettles", bm, ["Leaf5", "Fen", "Sedge"], smooth_angle=50, at=at)


ASSETS = {
    "Gorse": gorse,
    "BerryBushLow": berry_bush_low,
    "BerryBushLowPicked": lambda: berry_bush_low(picked=True),
    "BerryBushTall": berry_bush_tall,
    "BerryBushTallPicked": lambda: berry_bush_tall(picked=True),
    "Tussock": tussock,
    "Dock": dock,
    "Reeds": reeds,
    "Flax": flax,
    "FlaxPicked": lambda: flax(picked=True),
    "Herb": herb,
    "HerbPicked": lambda: herb(picked=True),
    "Mushrooms": mushrooms,
    "MushroomsPicked": lambda: mushrooms(picked=True),
    "LilyPads": lily_pads,
    "Bracken": bracken,
    "Bramble": bramble,
    "Wildflowers": wildflowers,
    "Heather": heather,
    "Thistles": thistles,
    "Brushwood": brushwood,
    "HangingRoots": hanging_roots,
    "IvyDrape": ivy_drape,
    "Fern": fern,
    "DeadGrass": dead_grass,
    "DeadBush": dead_bush,
    "Crops": crops,
    "CropsGreen": lambda: crops(ripe=False),
    "Cabbages": cabbages,
    "Nettles": nettles,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
