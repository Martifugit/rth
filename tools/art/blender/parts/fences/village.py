"""Fences, village. The basin's fences, a tile per span: buck fences, hurdles, hedges, the dry-stone field wall with its pier and stile, the field gate. Spans run along X from the tile's middle.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
village.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Parts/Fences/Village/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\parts\\fences\\village.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import random
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
from artlib.nature import WIND, leaf_mat, leafy, make, rod, wobble  # noqa: E402


def shelf(bm, centre, rx, ry, thick, drop, sides, mat, core, uvs, rnd, rot=0.0):
    """A crown shelf: a low irregular cap whose leaf hem hangs over its rim, a short skirt below
    it, and a dark underside, like a stratum of leaves. Its peak sits downwind of its middle."""
    cx, cy, cz = centre
    ring, hem_v = [], []
    for k in range(sides):
        a = rot + 2 * math.pi * k / sides
        j = rnd.uniform(0.72, 1.15)
        x, y = rx * j * math.cos(a), ry * j * math.sin(a)
        ring.append(bm.verts.new((cx + x, cy + y, cz + rnd.uniform(-0.06, 0.06))))
        hem_v.append(bm.verts.new((cx + x * 1.18, cy + y * 1.18, cz - drop * rnd.uniform(0.6, 1.3))))
    apex = bm.verts.new((cx + WIND.x * rx * 0.25, cy + WIND.y * ry * 0.25, cz + thick))
    for k in range(sides):
        n = (k + 1) % sides
        f = bm.faces.new((ring[k], ring[n], apex))
        f.material_index = mat
        uvs[f] = [(0, 0), (1, 0), (0.5, 1)]
        f = bm.faces.new((hem_v[k], hem_v[n], ring[n], ring[k]))
        f.material_index = mat
        uvs[f] = [(0, 0), (1, 0), (1, 1), (0, 1)]
    under = [bm.verts.new((v.co.x * 0.9 + cx * 0.1, v.co.y * 0.9 + cy * 0.1, cz - drop * 0.55)) for v in hem_v]
    bm.faces.new(list(reversed(under))).material_index = core


def wall_span(seed=1, at=(0, 0, 0), rot=0.0):
    """One tile of dry-stone field wall along X: a battered body (narrower at the top) in field
    stone, a row of cope stones on edge along its top. The texture carries the coursing."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    h, wb, wt = 0.92, 0.27, 0.17                 # to the player's waist and more (he is 1.9)
    xs = [0.0, 0.5, 1.0]
    rings = []
    for x in xs:
        dz = rnd.uniform(-0.04, 0.03) if 0 < x < 1 else 0
        rings.append([bm.verts.new(p) for p in ((x, -wb, -0.05), (x, wb, -0.05), (x, wt, h + dz), (x, -wt, h + dz))])
    for a, b in zip(rings, rings[1:]):
        for k in range(4):
            n = (k + 1) % 4
            bm.faces.new((a[k], a[n], b[n], b[k])).material_index = 0
    bm.faces.new(list(reversed(rings[0]))).material_index = 0
    bm.faces.new(rings[-1]).material_index = 0
    # Cope stones: one ragged strip of stones on edge along the top, its crest rising and falling
    # stone by stone (a single piece, so it reads as a row, not as battlements).
    n = 8
    crest, foot = [], []
    for k in range(n + 1):
        x = k / n
        zt = h + rnd.uniform(0.04, 0.12)
        crest.append((bm.verts.new((x, -wt * 0.75, zt)), bm.verts.new((x, wt * 0.75, zt))))
        foot.append((bm.verts.new((x, -wt * 0.95, h - 0.03)), bm.verts.new((x, wt * 0.95, h - 0.03))))
    for k in range(n):
        for s in (0, 1):
            q = (foot[k][s], foot[k + 1][s], crest[k + 1][s], crest[k][s])
            bm.faces.new(q if s == 0 else tuple(reversed(q))).material_index = 1
        bm.faces.new((crest[k][0], crest[k + 1][0], crest[k + 1][1], crest[k][1])).material_index = 1
    for k, flip in ((0, True), (n, False)):
        q = (foot[k][0], crest[k][0], crest[k][1], foot[k][1])
        bm.faces.new(tuple(reversed(q)) if flip else q).material_index = 1
    return make("WallSpan", bm, ["Stone", "Granite"], at=at, rot=rot)


def buck_fence(seed=1, at=(0, 0, 0), end_post=False, bucks=True):
    """A buck fence, one tile along X: no posts dug in, a pair of poles crossed like an X at the
    span's start with the rail laid in their crotch, and a lower rail tied to the legs. Weathered
    grey. end_post adds the closing buck; bucks=False leaves the rails alone (laid along a grid
    line, the bucks stand at its corners: buck())."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for x in (((0.0, 1.0) if end_post else (0.0,)) if bucks else ()):
        buck_legs(bm, rnd, x)
    crotch = 0.66
    rod(bm, wobble(rnd, (-0.08, 0.0, crotch + 0.03), (1.08, 0.0, crotch + 0.05), 4, 0.03), 0.045, sides=5)
    rod(bm, wobble(rnd, (-0.05, 0.17, 0.3), (1.05, 0.17, 0.32), 3, 0.03), 0.035, sides=4)
    return make("BuckFence", bm, ["Driftwood"], smooth_angle=50, at=at)


def buck_legs(bm, rnd, x=0.0):
    """Two poles crossed like an X across the line at x, their crotch carrying the top rail."""
    for s in (-1, 1):
        foot = Vector((x + s * 0.04, s * 0.32, -0.05))
        head = Vector((x - s * 0.03, -s * 0.24, 1.1 + rnd.uniform(-0.05, 0.05)))
        rod(bm, wobble(rnd, foot, head, 3, 0.03), 0.04, sides=4, taper=0.8)


def buck(seed=1, at=(0, 0, 0)):
    """A buck fence's buck on its own, at the origin: where its rails meet, a corner or an end."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    buck_legs(bm, rnd)
    return make("BuckFenceBuck", bm, ["Driftwood"], smooth_angle=50, at=at)


def hurdle_stake(seed=1, at=(0, 0, 0)):
    """A hurdle's round stake on its own, at the origin: where hurdles meet, a corner or an end."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    rod(bm, wobble(rnd, (0, 0, -0.05), (rnd.uniform(-0.02, 0.02), 0, 1.0), 3, 0.02), 0.05, sides=5, taper=0.85, tip=(0, 0, 1.06))
    return make("HurdleStake", bm, ["Driftwood"], smooth_angle=50, at=at)


def wall_pier(seed=1, at=(0, 0, 0)):
    """Where dry-stone walls meet, a corner or an end, at the origin: a square of the wall's body
    as wide as it is thick, battered like it, under one flat cope stone."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    h, wb, wt = 0.92, 0.29, 0.19
    A.hull(bm, [(x * w, y * w, z) for x in (-1, 1) for y in (-1, 1) for w, z in ((wb, -0.05), (wt, h))], mat=0)
    c = wt + 0.03
    A.hull(bm, [(x * c + rnd.uniform(-0.02, 0.02), y * c + rnd.uniform(-0.02, 0.02), z) for x in (-1, 1) for y in (-1, 1)
                for z in (h - 0.03, h + 0.1)], mat=1)
    return make("WallPier", bm, ["Stone", "Granite"], at=at)


def hurdle(seed=1, at=(0, 0, 0), end_post=False, stakes=True):
    """A woven hurdle, one tile along X: a panel of woven rods (painted, not modelled) bowing a
    little in the middle, hung on a round stake at the span's start, a binding rod along its top.
    stakes=False leaves the panel alone (laid along a grid line, the stakes stand at its corners:
    hurdle_stake())."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for x in (((0.0, 1.0) if end_post else (0.0,)) if stakes else ()):
        rod(bm, wobble(rnd, (x, 0, -0.05), (x + rnd.uniform(-0.03, 0.03), 0, 1.0), 3, 0.02), 0.045,
            sides=5, taper=0.85, tip=(x, 0, 1.06))
    uvs = {}
    cols = [(0.04, 0.0), (0.5, 0.035), (0.96, 0.0)]
    lo = [bm.verts.new((x, y, 0.06 + rnd.uniform(-0.02, 0.02))) for x, y in cols]
    hi = [bm.verts.new((x, y, 0.84 + rnd.uniform(-0.03, 0.03))) for x, y in cols]
    # The panel has two faces, back to back a hair apart: the game lights each side from its own
    # normal, so a single face would show its back black.
    back_lo = [bm.verts.new(v.co + Vector((0, 0.012, 0))) for v in lo]
    back_hi = [bm.verts.new(v.co + Vector((0, 0.012, 0))) for v in hi]
    for k in range(2):
        u0, u1 = k * 0.5, (k + 1) * 0.5
        f = bm.faces.new((lo[k], lo[k + 1], hi[k + 1], hi[k]))
        f.material_index = 1
        uvs[f] = [(u0, 0), (u1, 0), (u1, 0.85), (u0, 0.85)]
        b = bm.faces.new((back_lo[k + 1], back_lo[k], back_hi[k], back_hi[k + 1]))
        b.material_index = 1
        uvs[b] = [(u1, 0), (u0, 0), (u0, 0.85), (u1, 0.85)]
    rod(bm, [hi[0].co + Vector((-0.02, 0, 0.02)), hi[1].co + Vector((0, 0, 0.02)), hi[2].co + Vector((0.02, 0, 0.02))],
        0.025, sides=4)
    return leafy("Hurdle", bm, uvs, ["Driftwood", leaf_mat("Wattle")], at, smooth=50)


def hedge(seed=1, at=(0, 0, 0)):
    """One tile of laid hedge along X: stems cut half through and laid over at a slant along the
    line, as hedgers do, with the leaves grown thick along their top. Neighbouring spans overlap."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uvs = {}
    for k in range(3):
        x0 = 0.05 + 0.33 * k + rnd.uniform(-0.05, 0.05)
        rod(bm, [(x0, rnd.uniform(-0.08, 0.08), -0.05), (x0 + 0.25, 0, 0.35), (x0 + 0.6, rnd.uniform(-0.06, 0.06), 0.6)],
            0.045, sides=4, taper=0.7)
    shelf(bm, (0.5, 0.0, 0.85), 0.8, 0.42, 0.18, 0.62, 8, 1, 2, uvs, rnd, rot=0.0)
    return leafy("Hedge", bm, uvs, ["Bark", leaf_mat("Foliage"), "Leaf1"], at)


def field_gate(seed=1, at=(0, 0, 0)):
    """A field gate, one tile along X, to hang in any of the fences: a round hanging post at the
    span's start, a frame of crooked rails with a brace running up from the hinge foot, a
    closing post at the far end."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for x in (0.0, 1.0):
        rod(bm, wobble(rnd, (x, 0, -0.05), (x, 0, 1.08), 3, 0.015), 0.055, sides=6, taper=0.9)
    for z in (0.2, 0.45, 0.7, 0.95):
        rod(bm, wobble(rnd, (0.06, 0.06, z), (0.94, 0.06, z + rnd.uniform(-0.02, 0.02)), 3, 0.015), 0.03, sides=4)
    for x in (0.06, 0.94):
        rod(bm, [(x, 0.06, 0.15), (x, 0.06, 1.0)], 0.035, sides=4)
    rod(bm, [(0.08, 0.075, 0.22), (0.9, 0.075, 0.93)], 0.028, sides=4)
    return make("FieldGate", bm, ["Driftwood"], smooth_angle=50, at=at)


def wall_stile(seed=2, at=(0, 0, 0)):
    """A stile through the field wall, one tile along X: the wall's span with three flat through-
    stones set in it as steps, sticking out on both sides, climbing to the top."""
    ob = wall_span(seed=seed, at=at)
    rnd = random.Random(seed)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    for x, z in ((0.3, 0.24), (0.5, 0.52), (0.7, 0.8)):
        w = rnd.uniform(0.1, 0.13)                   # a flat stone through the wall, out both sides
        A.box(bm, (x - w, -0.42, z), (x + w, 0.42, z + 0.07), 1)
    bm.to_mesh(ob.data)
    bm.free()
    A.box_uvs(ob.data)
    ob.name = ob.data.name = "WallStile"
    return ob


def span(fn, **kw):
    """A one-tile span built along X from 0 to 1, moved so its tile's middle is the origin."""
    def build_span():
        ob = fn(**kw)
        ob.data.transform(Matrix.Translation((-0.5, 0, 0)))
        return ob
    return build_span


ASSETS = {
    "BuckFence": span(buck_fence),
    "BuckFenceEnd": span(buck_fence, end_post=True),
    "Hurdle": span(hurdle),
    "HurdleEnd": span(hurdle, end_post=True),
    "FieldWall": span(wall_span),
    "Hedge": span(hedge),
    "BuckFenceRails": span(buck_fence, bucks=False),
    "BuckFenceBuck": buck,
    "HurdlePanel": span(hurdle, stakes=False),
    "HurdleStake": hurdle_stake,
    "FieldWallPier": wall_pier,
    "FieldGate": span(field_gate),
    "FieldWallStile": span(wall_stile),
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
