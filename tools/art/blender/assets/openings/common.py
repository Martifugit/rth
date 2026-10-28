"""Openings, common. Ways in and down of no style: pits, sinkholes, fissures, shafts, stairwells, sumps, burrows and what stands over them; cave doors, gates and grates.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Openings/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\openings\\common.py")
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
from artlib.caves import ROCK, STONE, added, block, chunk, lay, make, rng, weather  # noqa: E402


def hole(bm, outline, rim_h, rnd, mats=(0, 1, 2, 3, 4), rim_w=0.45, rings=3, bottom_z=0.03):
    """A hole in the ground, built up rather than dug (nothing can go below the terrain): a rim
    rising from the ground outside `outline` (points round its middle) to a crest `rim_h` high, then
    walls dropping inside it, narrowing, to a floor just above the ground. From the game's camera the
    walls read as a drop and the floor as the dark. mats: the rim's outside, its crest, the walls'
    upper ring, the deeper walls, the floor."""
    c = Vector((sum(p[0] for p in outline) / len(outline), sum(p[1] for p in outline) / len(outline), 0))
    n = len(outline)

    def ring(scale, z, push=0.0, jit=0.0):
        out = []
        for x, y in outline:
            d = Vector((x, y, 0)) - c
            e = d.normalized() if d.length else Vector((0, 0, 0))
            p = c + d * scale + e * push
            out.append(bm.verts.new((p.x + rnd.uniform(-jit, jit), p.y + rnd.uniform(-jit, jit), z)))
        return out

    foot = ring(1.0, -0.05, rim_w, 0.05)
    crest = ring(1.0, 0.0, 0.08, 0.03)
    for v in crest:
        v.co.z = rim_h * rnd.uniform(0.85, 1.12)
    lip = ring(0.97, rim_h * 0.88)
    walls = [ring(1.0 - 0.13 * k, rim_h * 0.88 * (1 - k / rings) + bottom_z * k / rings) for k in range(1, rings + 1)]
    bands = [(foot, crest, mats[0], 1), (crest, lip, mats[1], -1)]
    prev = lip
    for k, w in enumerate(walls):
        bands.append((prev, w, mats[2] if k == 0 else mats[3], -1))
        prev = w
    for a, b, m, side in bands:
        for k in range(n):
            q = (a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])
            f = bm.faces.new(q)
            f.material_index = m
            f.normal_update()
            mid = f.calc_center_median()
            want = (mid - c) * side
            want.z = 0.6
            if f.normal.dot(want) < 0:
                f.normal_flip()
    floor = bm.faces.new(prev)
    floor.material_index = mats[4]
    floor.normal_update()
    if floor.normal.z < 0:
        floor.normal_flip()
    return c


def door(seed=1, at=(0, 0, 0), ajar=False):
    """A plank door for a built doorway, two battens and two iron straps across it, hung on its left
    edge. ajar: swung open into the dark."""
    r = rng(seed)
    bm = bmesh.new()
    w, h = 0.95, 2.2
    for k in range(6):
        x0 = -w + 2 * w * k / 6
        block(bm, (x0 + 0.005, -0.04, 0.0), (x0 + 2 * w / 6 - 0.005, 0.04, h - r.uniform(0, 0.04)), r, chip=0.008)
    for z in (0.45, 1.75):
        block(bm, (-w + 0.05, 0.04, z), (w - 0.05, 0.1, z + 0.18), r, chip=0.008)
        block(bm, (-w, -0.07, z + 0.04), (w * 0.6, -0.04, z + 0.12), r, mat=1, chip=0.0)
    if ajar:
        lay(bm, list(bm.verts), Matrix.Translation((-w, 0, 0)) @ Matrix.Rotation(math.radians(-70), 4, 'Z')
            @ Matrix.Translation((w, 0, 0)))
    return make("DoorAjar" if ajar else "Door", bm, ["Planks", "Blackiron"], at=at, smooth=20)


def boarded(seed=3, at=(0, 0, 0)):
    """An opening boarded up: rough planks nailed across it every way, one hanging loose."""
    r = rng(seed)
    bm = bmesh.new()
    boards = [(-1.2, 0.6, 1.2, 0.9), (-1.2, 1.6, 1.2, 1.3), (-1.1, 0.2, 1.1, 2.0), (-1.2, 2.1, 1.1, 2.2)]
    for k, (x0, z0, x1, z1) in enumerate(boards):
        before = set(bm.verts)
        length = math.hypot(x1 - x0, z1 - z0)
        block(bm, (-length / 2, -0.03, -0.1), (length / 2, 0.03, 0.1), r, chip=0.01)
        lay(bm, added(bm, before), Matrix.Translation(((x0 + x1) / 2, -0.06 - 0.035 * k, (z0 + z1) / 2))   # each over the last
            @ Matrix.Rotation(-math.atan2(z1 - z0, x1 - x0), 4, 'Y'))
    before = set(bm.verts)
    block(bm, (-0.6, -0.03, -0.09), (0.6, 0.03, 0.09), r, chip=0.01)
    lay(bm, added(bm, before), Matrix.Translation((0.75, -0.12, 0.95)) @ Matrix.Rotation(math.radians(-62), 4, 'Y'))
    return make("Boarded", bm, ["Planks"], at=at, smooth=20)


def gate(seed=1, at=(0, 0, 0), open_=False):
    """An iron gate for a doorway SPAN wide: upright bars with spiked tops through three rails, hung
    on its left edge. open_: swung half open."""
    r = rng(seed)
    bm = bmesh.new()
    w, h = 1.0, 2.3
    for k in range(9):
        x = -w + 2 * w * (k + 0.5) / 9
        N.rod(bm, [(x, 0, 0.0), (x, 0, h)], 0.022, sides=4, tip=(x, 0, h + 0.14))
    for z in (0.15, 1.2, h - 0.15):
        N.rod(bm, [(-w, 0, z), (w, 0, z)], 0.03, sides=4)
    for x in (-w, w):
        N.rod(bm, [(x, 0, -0.05), (x, 0, h)], 0.04, sides=4)
    if open_:
        lay(bm, list(bm.verts), Matrix.Translation((-w, 0, 0)) @ Matrix.Rotation(math.radians(-55), 4, 'Z')
            @ Matrix.Translation((w, 0, 0)))
    return make("GateOpen" if open_ else "Gate", bm, ["Blackiron"], at=at, smooth=40)


def grate(seed=2, at=(0, 0, 0)):
    """A grate set flush in the ground, a stone kerb round iron bars, black below: a drain, a vent,
    or a way down that is not for people."""
    r = rng(seed)
    bm = bmesh.new()
    for x0, y0, x1, y1 in ((-0.7, -0.7, 0.7, -0.5), (-0.7, 0.5, 0.7, 0.7), (-0.7, -0.5, -0.5, 0.5), (0.5, -0.5, 0.7, 0.5)):
        block(bm, (x0, y0, -0.1), (x1, y1, 0.06), r, chip=0.01)
    for k in range(5):
        y = -0.5 + (k + 0.5) / 5
        N.rod(bm, [(-0.52, y, 0.03), (0.52, y, 0.03)], 0.025, mat=1, sides=4)
    floor = [bm.verts.new(p) for p in ((-0.5, -0.5, -0.06), (0.5, -0.5, -0.06), (0.5, 0.5, -0.06), (-0.5, 0.5, -0.06))]
    bm.faces.new(floor).material_index = 2
    return make("Grate", bm, [STONE, "Blackiron", "CryptEarth"], at=at, smooth=20)


EARTH = ["Grass", "Dirt", "Mud", "CryptDarkStone", "CryptEarth"]   # a hole's outside, crest, walls, deep walls, floor


def outline_round(r, n, rnd, jit, stretch=(1.0, 1.0)):
    """A ragged ring of points round the origin, r across, stretched (x, y)."""
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n + rnd.uniform(-0.15, 0.15)
        rr = r * rnd.uniform(1 - jit, 1 + jit)
        pts.append((math.cos(a) * rr * stretch[0], math.sin(a) * rr * stretch[1]))
    return pts


def pit(seed=1, at=(0, 0, 0)):
    """A pit in the turf, about two tiles across: an earth rim, walls dropping into the dark. For a
    rope (RopeAnchor) or a ladder (LadderTop), or a jump. A few stones on its lip."""
    r = rng(seed)
    bm = bmesh.new()
    hole(bm, outline_round(0.95, 10, r, 0.1), 0.35, r)
    for k in range(3):
        a = r.uniform(0, 2 * math.pi)
        chunk(bm, (1.25 * math.cos(a), 1.25 * math.sin(a), 0.1), (0.16, 0.14, 0.22), r, mats=(5, 6, 7), points=10)
    return make("Pit", bm, EARTH + ROCK, at=at)


def sinkhole(seed=2, at=(0, 0, 0)):
    """Ground that has fallen in: a wide ragged hole, its lip torn, two slabs of turf sliding in."""
    r = rng(seed)
    bm = bmesh.new()
    c = hole(bm, outline_round(1.5, 13, r, 0.28), 0.3, r, rim_w=0.6)
    for k, a in enumerate((0.6, 3.6)):
        before = set(bm.verts)
        chunk(bm, (0, 0, 0), (0.45, 0.3, 0.22), r, mats=(1, 2, 0), points=12)
        out = Vector((math.cos(a), math.sin(a), 0))
        lay(bm, added(bm, before), Matrix.Translation(out * 1.05 + Vector((0, 0, 0.12)))
            @ Matrix.Rotation(a + math.pi / 2, 4, 'Z') @ Matrix.Rotation(math.radians(28), 4, 'X'))
    return make("Sinkhole", bm, EARTH, at=at)


def fissure(seed=3, at=(0, 0, 0)):
    """A crack in the ground, three tiles long and barely a body wide, rock breaking through the turf
    along both lips."""
    r = rng(seed)
    bm = bmesh.new()
    pts = []
    for k in range(14):
        a = 2 * math.pi * k / 14
        pts.append((math.cos(a) * 1.5 * r.uniform(0.9, 1.05), math.sin(a) * 0.35 + math.sin(math.cos(a) * 4) * 0.12))
    hole(bm, pts, 0.4, r, rim_w=0.35)
    for k in range(5):
        x = -1.3 + 0.65 * k
        side = 1 if k % 2 else -1
        chunk(bm, (x, side * 0.62, 0), (0.28, 0.22, r.uniform(0.35, 0.6)), r, points=12)
    return make("Fissure", bm, ["Scree", "Moss", "Cinder", "CryptDarkStone", "CryptEarth"], at=at)


def shaft(seed=4, at=(0, 0, 0)):
    """A shaft going straight down, built: a square kerb of stone blocks round a dark drop. For a
    rope, a ladder, or a windlass (Windlass)."""
    r = rng(seed)
    bm = bmesh.new()
    hole(bm, [(-0.7, -0.7), (0.7, -0.7), (0.7, 0.7), (-0.7, 0.7)], 0.5, r, rim_w=0.05, rings=3)
    for x0, y0, x1, y1 in ((-0.95, -0.95, 0.0, -0.7), (0.0, -0.95, 0.95, -0.7), (-0.95, 0.7, 0.0, 0.95),
                           (0.0, 0.7, 0.95, 0.95), (-0.95, -0.7, -0.7, 0.7), (0.7, -0.7, 0.95, 0.7)):
        block(bm, (x0 + 0.005, y0 + 0.005, -0.1), (x1 - 0.005, y1 - 0.005, 0.5), r, mat=0, chip=0.02)
    weather(bm, r, 0.008)
    return make("Shaft", bm, [STONE, STONE, "CryptDarkStone", "CryptEarth", "CryptEarth"], at=at, smooth=20)


def stairwell(seed=5, at=(0, 0, 0)):
    """Steps going down into the ground (+Y), built: a kerb round a long opening, a low sill to step
    over, six steps dropping inside it, the lower ones lost in the dark."""
    r = rng(seed)
    bm = bmesh.new()
    top = 0.45
    for x0, y0, x1, y1, h in ((-0.75, -1.25, 0.75, -1.05, 0.25), (-0.75, -1.05, -0.55, 1.05, top),
                              (0.55, -1.05, 0.75, 1.05, top), (-0.75, 1.05, 0.75, 1.25, top)):
        block(bm, (x0, y0, -0.1), (x1, y1, h), r, mat=0, chip=0.02)
    for k in range(6):
        y0 = -1.05 + 0.28 * k
        z = 0.4 - 0.066 * k
        block(bm, (-0.55, y0, -0.1), (0.55, y0 + 0.3, z), r, mat=1 if k < 3 else 2, chip=0.01)
    for (x0, y0), (x1, y1) in (((-0.55, -1.05), (-0.55, 1.05)), ((0.55, 1.05), (0.55, -1.05)), ((-0.55, 1.05), (0.55, 1.05))):
        v = [bm.verts.new(p) for p in ((x0, y0, 0.02), (x1, y1, 0.02), (x1, y1, top), (x0, y0, top))]
        bm.faces.new(v).material_index = 2
    floor = [bm.verts.new(p) for p in ((-0.55, 0.63, 0.02), (0.55, 0.63, 0.02), (0.55, 1.05, 0.02), (-0.55, 1.05, 0.02))]
    bm.faces.new(floor).material_index = 3
    weather(bm, r, 0.006)
    return make("Stairwell", bm, [STONE, STONE, "CryptDarkStone", "CryptEarth"], at=at, smooth=20)


def sump(seed=6, at=(0, 0, 0)):
    """A sump at the foot of the rock: a pool of still black water ringed with stone, the way on
    under the surface. Rock heaped behind it (+Y)."""
    r = rng(seed)
    bm = bmesh.new()
    hole(bm, outline_round(1.0, 11, r, 0.12), 0.3, r, rim_w=0.3, bottom_z=0.14)
    for k, (x, y, h) in enumerate(((-0.9, 1.1, 1.1), (0.4, 1.4, 1.5), (1.2, 0.7, 0.8))):
        chunk(bm, (x, y, 0), (0.55, 0.45, h), r, mats=(0, 2, 1), points=16)
    return make("Sump", bm, ["Scree", "Moss", "Cinder", "CryptDarkStone", "Sea"], at=at)


def burrow(seed=7, at=(0, 0, 0)):
    """An earth bank with a low hole in it, a body's width (pair it with ThroatBurrow): two shoulders
    of earth and a turf roof over the gap, roots showing where the bank was dug."""
    r = rng(seed)
    bm = bmesh.new()
    for x in (-0.68, 0.68):                       # deep enough to cover its throat (2.3 tiles)
        chunk(bm, (x, 1.0, 0), (0.48, 1.4, 0.8), r, mats=(1, 2, 0), points=18)
    chunk(bm, (0, 1.1, 0.5), (0.8, 1.35, 0.5), r, sink=0.0, mats=(1, 2, 0), points=18)
    for k in range(4):
        x = r.uniform(-0.3, 0.3)
        N.rod(bm, [(x, -0.15, 0.62), (x + r.uniform(-0.1, 0.1), -0.2, 0.42)], 0.015, mat=3, sides=3, taper=0.5)
    return make("Burrow", bm, ["Grass", "Dirt", "Mud", "Bark"], at=at)


def rope_anchor(seed=1, at=(0, 0, 0)):
    """A rope down a hole: a stake driven in a little way back from the lip, leaning away from it, the
    rope tied off round it and run over the lip (+Y, the lip about a tile away) and down into the
    dark; the spare coiled at the stake's foot."""
    r = rng(seed)
    bm = bmesh.new()
    N.rod(bm, [(0, 0, -0.1), (0.02, -0.1, 0.55)], 0.05, sides=5, taper=0.8)
    N.rod(bm, [(0.02, -0.06, 0.42), (0.0, 0.5, 0.3), (0.0, 1.05, 0.4), (0.02, 1.25, 0.25), (0.0, 1.35, 0.05)],
          0.02, mat=1, sides=4)
    for k, (rad, z) in enumerate(((0.16, 0.03), (0.13, 0.07))):
        pts = [(0.32 + rad * math.cos(a), -0.25 + rad * math.sin(a), z) for a in [2 * math.pi * i / 10 for i in range(11)]]
        N.rod(bm, pts, 0.02, mat=1, sides=4)
    return make("RopeAnchor", bm, ["Driftwood", "Wheat"], at=at, smooth=50)


def ladder_top(seed=2, at=(0, 0, 0)):
    """The top of a ladder standing in a hole, its rails sticking up out of it and leaning against the
    lip (-Y), rungs lashed on; its foot is in the dark at the hole's floor."""
    r = rng(seed)
    bm = bmesh.new()
    foot, head = Vector((0, 0.3, 0.03)), Vector((0, -0.35, 1.25))
    for x in (-0.22, 0.22):
        N.rod(bm, [foot + Vector((x, 0, 0)), head + Vector((x * 1.05, 0, 0))], 0.04, sides=5)
    for k in range(5):
        p = foot.lerp(head, 0.12 + 0.19 * k)
        N.rod(bm, [p + Vector((-0.24, 0, 0)), p + Vector((0.24, 0, 0))], 0.025, sides=4)
    return make("LadderTop", bm, ["Driftwood"], at=at, smooth=50)


def windlass(seed=3, at=(0, 0, 0)):
    """A windlass over a shaft: two posts either side of it, an axle with a crank, the rope wound on
    it running straight down into the dark."""
    r = rng(seed)
    bm = bmesh.new()
    for x in (-1.05, 1.05):
        N.rod(bm, N.wobble(r, (x, 0, -0.05), (x, 0, 1.35), 3, 0.02), 0.07, sides=6, taper=0.9)
    N.rod(bm, [(-1.12, 0, 1.2), (1.12, 0, 1.2)], 0.07, sides=6)
    N.rod(bm, [(1.12, 0, 1.2), (1.3, 0, 1.2), (1.3, 0, 0.92)], 0.03, sides=4)
    A.prism(bm, 8, 0.11, 0.11, -0.22, 0.22, (0, 0), mat_index=1, cap_top=True, cap_bottom=True)
    lay(bm, list(bm.verts)[-16:], Matrix.Translation((0, 0, 1.2)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    N.rod(bm, [(0, -0.1, 1.15), (0, -0.1, 0.04)], 0.02, mat=1, sides=4)
    return make("Windlass", bm, ["Timber", "Wheat"], at=at, smooth=40)


def planked(seed=4, at=(0, 0, 0)):
    """Planks laid over a hole about two tiles across, resting on its lip: four boards, one missing
    from the middle, one sagging."""
    r = rng(seed)
    bm = bmesh.new()
    for k, x in enumerate((-0.75, -0.38, 0.38, 0.75)):
        before = set(bm.verts)
        block(bm, (x - 0.15, -1.15, -0.03), (x + 0.15, 1.15, 0.03), r, chip=0.01)
        sag = 0.12 if k == 2 else 0.0
        lay(bm, added(bm, before), Matrix.Translation((0, 0, 0.4 - sag)) @ Matrix.Rotation(r.uniform(-0.05, 0.05), 4, 'Z'))
    return make("Planked", bm, ["Planks"], at=at, smooth=20)


ASSETS = {
    "Pit": pit,
    "Sinkhole": sinkhole,
    "Fissure": fissure,
    "Shaft": shaft,
    "Stairwell": stairwell,
    "Sump": sump,
    "Burrow": burrow,
    "RopeAnchor": rope_anchor,
    "LadderTop": ladder_top,
    "Windlass": windlass,
    "Planked": planked,
    "Door": door,
    "DoorAjar": lambda at=(0, 0, 0): door(at=at, ajar=True),
    "Boarded": boarded,
    "Gate": gate,
    "GateOpen": lambda at=(0, 0, 0): gate(at=at, open_=True),
    "Grate": grate,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
