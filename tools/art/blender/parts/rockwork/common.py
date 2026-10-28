"""Rockwork, common. Cave rock, a part each: rock masses, spires, lips and slabs, boulders, rubble and scree, and the throats a cave mouth opens on.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
common.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Rockwork/Common/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\rockwork\\common.py")
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
from artlib.caves import ROCK, chunk, lay, make, rng  # noqa: E402


DARK = ["CryptDarkStone", "CryptEarth"]    # the throat, from its rim to black


def rock_mass(seed=1, at=(0, 0, 0), kind="big"):
    """A rock mass to build hillsides and cave sides from, two or three chunks riding on each other.
    kind: big (2 x 1.5 tiles, 3 high), small (1 x 1, 2.2), wide (a low shoulder 2.5 long), tall (a
    sheer face 1.2 wide, 3.5 high)."""
    r = rng(seed)
    bm = bmesh.new()
    if kind == "big":
        chunk(bm, (0, 0, 0), (1.0, 0.75, 1.7), r)
        chunk(bm, (r.uniform(-0.45, -0.2), 0.1, 0.9), (0.62, 0.55, 2.0), r, lean=(r.uniform(-0.12, 0.0), 0.05))
        chunk(bm, (r.uniform(0.25, 0.5), 0.2, 0.5), (0.55, 0.5, 1.6), r, lean=(0.08, 0.04))
    elif kind == "small":
        chunk(bm, (0, 0, 0), (0.55, 0.5, 1.3), r)
        chunk(bm, (0.08, 0.08, 0.7), (0.38, 0.36, 1.4), r, lean=(r.uniform(-0.1, 0.1), 0.04))
    elif kind == "wide":
        chunk(bm, (-0.6, 0, 0), (0.8, 0.6, 1.2), r)
        chunk(bm, (0.55, 0.1, 0), (0.75, 0.55, 1.0), r)
        chunk(bm, (0.0, 0.2, 0.6), (0.6, 0.45, 0.9), r)
    else:
        chunk(bm, (0, 0, 0), (0.6, 0.45, 2.2), r)
        chunk(bm, (0.05, 0.05, 1.8), (0.45, 0.38, 1.7), r, lean=(r.uniform(-0.06, 0.06), 0.03))
    return make("RockMass" + {"big": "", "small": "Small", "wide": "Wide", "tall": "Tall"}[kind], bm, ROCK, at=at)


def rock_spire(seed=3, at=(0, 0, 0)):
    """A tall leaning tooth of rock, for a cleft, or to stand guard by a mouth."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.45, 0.4, 1.6), r)
    chunk(bm, (0.05, 0.02, 1.3), (0.28, 0.25, 1.9), r, lean=(0.12, 0.04))
    return make("RockSpire", bm, ROCK, at=at)


def rock_lip(seed=5, at=(0, 0, 0), height=2.2, long=False):
    """An overhang across a mouth: a flat slab of rock lying at `height`, its ends resting on the
    masses either side; 2.4 tiles long, or 3.4 (long). Moss on top, shadow under."""
    r = rng(seed)
    bm = bmesh.new()
    half = 1.7 if long else 1.2
    chunk(bm, (0, 0.1, height), (half, 0.7, 0.55), r, sink=0.0)
    chunk(bm, (r.uniform(-0.4, 0.4) * half, 0.25, height + 0.35), (half * 0.55, 0.5, 0.45), r, sink=0.0)
    return make("RockLipLong" if long else "RockLip", bm, ROCK, at=at)


def rock_slab(seed=6, at=(0, 0, 0)):
    """A flat slab lying tilted on the ground, a ledge or a step up to a mouth."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.65, 0.45, 0.35), r)
    lay(bm, list(bm.verts), Matrix.Rotation(math.radians(r.uniform(5, 10)), 4, Vector((1, 0.4, 0)).normalized()))
    return make("RockSlab", bm, ROCK, at=at)


def boulder(seed=8, at=(0, 0, 0)):
    """A single boulder, about a tile across."""
    r = rng(seed)
    bm = bmesh.new()
    chunk(bm, (0, 0, 0), (0.5, 0.42, 0.8), r, points=18)
    return make("Boulder", bm, ROCK, at=at)


def rubble(seed=7, at=(0, 0, 0)):
    """Fallen stone at the foot of a face: a spill of chunks, the biggest at the back."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(8):
        a, d = r.uniform(0, 2 * math.pi), r.uniform(0, 0.75) * (0.5 if i < 2 else 1)
        s = r.uniform(0.18, 0.32) if i < 3 else r.uniform(0.08, 0.16)
        chunk(bm, (d * math.cos(a), d * math.sin(a) * 0.6 + 0.2, 0), (s, s * 0.9, s * 1.1), r, sink=0.05, points=12)
    return make("Rubble", bm, ROCK, at=at)


def scree(seed=9, at=(0, 0, 0)):
    """Scree fanning out from the foot of a face (+Y is the face): many small stones, thicker at
    the back, thinning out forwards."""
    r = rng(seed)
    bm = bmesh.new()
    for i in range(22):
        t = r.random() ** 1.6
        x, y = r.uniform(-1.0, 1.0) * (0.4 + t), 0.6 - t * 1.4
        s = r.uniform(0.06, 0.14) * (1.3 - t * 0.7)
        chunk(bm, (x, y, 0), (s, s * 0.9, s * 0.8), r, sink=0.04, points=8)
    return make("Scree", bm, ROCK, at=at)


THROATS = {
    # outline (half widths, heights 0..1), half width, height
    "wide": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 0.55), (0.8, 0.85), (0.4, 1.0), (-0.35, 1.0), (-0.8, 0.85), (-1.0, 0.55)], 1.0, 2.1),
    "cleft": ([(-1.0, 0.0), (1.0, 0.0), (0.9, 0.45), (0.45, 0.85), (0.05, 1.0), (-0.5, 0.75), (-0.95, 0.4)], 0.45, 2.5),
    "low": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 0.5), (0.6, 0.9), (-0.5, 0.95), (-1.0, 0.55)], 0.55, 0.95),
    "square": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 1.0), (-1.0, 1.0)], 0.95, 2.6),
    "squeeze": ([(-1.0, 0.0), (1.0, 0.0), (0.8, 0.35), (1.0, 0.7), (0.3, 1.0), (-0.6, 0.8), (-1.0, 0.45)], 0.22, 2.1),
    "burrow": ([(-1.0, 0.0), (1.0, 0.0), (1.0, 0.5), (0.55, 0.95), (-0.55, 0.95), (-1.0, 0.5)], 0.32, 0.55),
}

# How far each ring of a throat is shifted sideways: a squeeze bends, so its end can't be seen.
BENDS = {"squeeze": [0.0, 0.12, 0.3, 0.2, 0.0]}


def throat(seed=1, at=(0, 0, 0), kind="wide"):
    """The darkness behind a mouth: a tunnel into the hill (+Y) on a level floor (the ground would
    cover anything lower), narrowing as its roof comes down, its faces darkening from the rim to
    black. Seen from inside only (its faces point in), so it disappears wherever nothing covers it;
    set anything behind it at least 2.3 tiles back from its origin. kind: wide (a cave), cleft (a
    tall crack), low (a crawl hole), square (behind a built doorway), squeeze (a crack barely a body
    wide, bending as it goes), burrow (a body's width, into an earth bank)."""
    r = rng(seed)
    bm = bmesh.new()
    outline, width, height = THROATS[kind]
    steps = [(-0.15, 1.0, 1.0), (0.5, 0.9, 0.8), (1.1, 0.8, 0.6), (1.7, 0.7, 0.42), (2.2, 0.6, 0.3)]
    jitter = 0.0 if kind == "square" else 0.05
    rings = []
    bend = BENDS.get(kind, [0.0] * len(steps))
    for (y, scale, roof), dx in zip(steps, bend):
        rings.append([bm.verts.new((dx + x * width * scale * r.uniform(1 - jitter, 1 + jitter), y,
                                    0.02 + z * height * roof * r.uniform(1 - jitter, 1 + jitter))) for x, z in outline])
    n = len(outline)
    for i, (r0, r1) in enumerate(zip(rings, rings[1:])):
        for k in range(n):
            bm.faces.new((r0[k], r1[k], r1[(k + 1) % n], r0[(k + 1) % n])).material_index = 0 if i == 0 else 1
    end = bm.faces.new(rings[-1])
    end.material_index = 1
    for f in bm.faces:                                   # every face looks inward
        f.normal_update()
        mid = f.calc_center_median()
        inward = Vector((0, -1, 0)) if f is end else Vector((0, mid.y, mid.z * 0.5 + 0.3)) - mid
        if f.normal.dot(inward) < 0:
            f.normal_flip()
    name = "Throat" + {"wide": "", "cleft": "Cleft", "low": "Low", "square": "Square", "squeeze": "Squeeze",
                       "burrow": "Burrow"}[kind]
    return make(name, bm, DARK, at=at, smooth=60, finish=False)


ASSETS = {
    "RockMass": rock_mass,
    "RockMassSmall": lambda at=(0, 0, 0): rock_mass(at=at, kind="small"),
    "RockMassWide": lambda at=(0, 0, 0): rock_mass(at=at, kind="wide"),
    "RockMassTall": lambda at=(0, 0, 0): rock_mass(at=at, kind="tall"),
    "RockSpire": rock_spire,
    "RockLip": rock_lip,
    "RockLipLong": lambda at=(0, 0, 0): rock_lip(at=at, long=True),
    "RockSlab": rock_slab,
    "RockBoulder": boulder,
    "RockRubble": rubble,
    "RockScree": scree,
    "Throat": throat,
    "ThroatCleft": lambda at=(0, 0, 0): throat(at=at, kind="cleft"),
    "ThroatLow": lambda at=(0, 0, 0): throat(at=at, kind="low"),
    "ThroatSquare": lambda at=(0, 0, 0): throat(at=at, kind="square"),
    "ThroatSqueeze": lambda at=(0, 0, 0): throat(at=at, kind="squeeze"),
    "ThroatBurrow": lambda at=(0, 0, 0): throat(at=at, kind="burrow"),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
