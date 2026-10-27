"""The harbour kit's shared pieces: sizes, the walk layout, making objects, and timber (piles, beams,
deck boards). See kit.py for the kit and docs/harbour-kit.md for the design.

Conventions: one tile is one unit; a part's origin is the middle of its tile (0, 0) at the water's
surface (z 0); walkable parts run out from the bank along +Y; boats point their bow to +Y.
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..")
for p in (ART, os.path.join(ART, "nature")):
    if p not in sys.path:
        sys.path.insert(0, p)
import rth_art as A  # noqa: E402
import basin as N  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
WALK_DIR = os.path.join(REPO, "world", "walk")

BED = -1.4          # the bottom piles stand on
JETTY = 0.45        # a jetty's deck above the water
QUAY = 0.9          # the bank and a quay's top above the water
STOREY = 2.3        # docs/world-scale.md D2
WEED = (-0.12, 0.16)   # the green band on anything standing in the water


# ---- walk layouts -------------------------------------------------------------------------------

class Walk:
    """A part's walk layout: tiles to stand on (plane, dx, dy, height), blocked tile edges (plane and
    a grid line from (x0, y0) to (x1, y1), where tile (0, 0) spans grid 0..1), climbs. Written to
    world/walk/<Name>.json for the region compiler, and drawn on the board."""

    def __init__(self, name):
        self.name = name
        self.tiles = {}
        self.walls = []
        self.climbs = []

    def tile(self, dx, dy, h, plane=0):
        self.tiles[(plane, dx, dy)] = round(h, 3)

    def rect(self, x0, y0, x1, y1, h, plane=0):
        for dx in range(x0, x1):
            for dy in range(y0, y1):
                self.tile(dx, dy, h, plane)

    def drop(self, *tiles, plane=0):
        for dx, dy in tiles:
            self.tiles.pop((plane, dx, dy), None)

    def wall(self, x0, y0, x1, y1, plane=0):
        self.walls.append((plane, x0, y0, x1, y1))

    def ring(self, plane=0, gaps=()):
        """Block every edge between a walkable tile of this plane and one that isn't, except at the
        given (dx, dy, side) gaps (side N, E, S, W): a rail round the whole deck."""
        mine = {(dx, dy) for (p, dx, dy) in self.tiles if p == plane}
        for (dx, dy) in sorted(mine):
            for side, (nx, ny), line in (("N", (dx, dy + 1), (dx, dy + 1, dx + 1, dy + 1)),
                                         ("S", (dx, dy - 1), (dx, dy, dx + 1, dy)),
                                         ("E", (dx + 1, dy), (dx + 1, dy, dx + 1, dy + 1)),
                                         ("W", (dx - 1, dy), (dx, dy, dx, dy + 1))):
                if (nx, ny) not in mine and (dx, dy, side) not in gaps:
                    self.wall(*line, plane=plane)

    def climb(self, kind, frm, to):
        """frm, to: (plane, dx, dy); to may name another place: ("hold", plane, x, y)."""
        self.climbs.append({"kind": kind, "from": list(frm), "to": list(to)})

    def data(self):
        planes = {}
        for (p, dx, dy), h in sorted(self.tiles.items()):
            planes.setdefault(str(p), []).append([dx, dy, h])
        return {"model": self.name, "planes": planes, "walls": [list(w) for w in self.walls], "climbs": self.climbs}

    def save(self):
        os.makedirs(WALK_DIR, exist_ok=True)
        with open(os.path.join(WALK_DIR, self.name + ".json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(self.data(), f, indent=1)

    def overlay(self, ob):
        """Green squares on walkable tiles at their heights, red bars on blocked edges, blue lines
        for climbs; parented to the model, in the Walk collection (hide it to judge the model)."""
        bm = bmesh.new()
        for (p, dx, dy), h in self.tiles.items():
            z = h + 0.03
            v = [bm.verts.new((dx + x, dy + y, z)) for x, y in ((-0.45, -0.45), (0.45, -0.45), (0.45, 0.45), (-0.45, 0.45))]
            bm.faces.new(v).material_index = 0
        for (p, x0, y0, x1, y1) in self.walls:
            hs = [h for (pp, dx, dy), h in self.tiles.items() if pp == p and min(x0, x1) - 1 <= dx <= max(x0, x1) and min(y0, y1) - 1 <= dy <= max(y0, y1)]
            z = (max(hs) if hs else 0) + 0.25
            a, b = Vector((x0 - 0.5, y0 - 0.5, z)), Vector((x1 - 0.5, y1 - 0.5, z))
            d = (b - a).normalized()
            s = Vector((-d.y, d.x, 0)) * 0.03
            A.hull(bm, [a + s, a - s, b + s, b - s, a + s + Vector((0, 0, 0.06)), b - s + Vector((0, 0, 0.06))], 1)
        for c in self.climbs:
            if not isinstance(c["to"][0], int):
                continue
            a = Vector((c["from"][1], c["from"][2], self.tiles.get(tuple(c["from"]), 0) + 0.1))
            b = Vector((c["to"][1], c["to"][2], self.tiles.get(tuple(c["to"]), 0) + 0.1))
            N.branch(bm, a, b, 0.04, 0.04, mat=2, sides=4)
        me = bpy.data.meshes.new(self.name + "Walk")
        bm.to_mesh(me)
        bm.free()
        for name, col in (("WalkTile", (0.1, 0.8, 0.2, 0.45)), ("WalkWall", (0.9, 0.1, 0.1, 1.0)), ("WalkClimb", (0.1, 0.3, 0.95, 1.0))):
            m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            m.diffuse_color = col
            m.use_nodes = True
            bsdf = m.node_tree.nodes["Principled BSDF"]
            bsdf.inputs["Base Color"].default_value = col
            bsdf.inputs["Alpha"].default_value = col[3]
            m.surface_render_method = "BLENDED"
            me.materials.append(m)
        o = bpy.data.objects.new(self.name + "Walk", me)
        A.collection("Walk").objects.link(o)
        o.parent = ob
        return o


def make(name, bm, materials, walk=None, smooth=None):
    """The part's object in the Harbour collection, with only the materials it uses; its walk layout
    saved and drawn."""
    used = sorted({f.material_index for f in bm.faces})
    remap = {m: i for i, m in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    ob = N.make(name, bm, [materials[m] for m in used], smooth_angle=smooth, coll="Harbour")
    if walk is not None:
        walk.save()
        walk.overlay(ob)
    return ob


# ---- timber -------------------------------------------------------------------------------------

def beam(bm, a, b, h, w, mat=0):
    """A squared timber from a to b (its middle line), `h` deep and `w` wide."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    s = d.cross(Vector((0, 0, 1)))
    if s.length < 1e-6:
        s = Vector((1, 0, 0))
    s.normalize()
    u = s.cross(d)
    return A.hull(bm, [p + s * ss * w + u * uu * h for p in (a, b) for ss in (-0.5, 0.5) for uu in (-0.5, 0.5)], mat)


def pile(bm, x, y, top, rnd, r=0.12, mat=0, weed=None, lean=0.03):
    """A round pile driven into the bed, tapering a little, its head cut flat; a band of weed where
    it stands in the water (`weed`: its material slot)."""
    lx, ly = rnd.uniform(-lean, lean), rnd.uniform(-lean, lean)
    rings = []
    for z, rr in ((BED, r * 1.05), (WEED[0], r), (WEED[1], r * 0.98), (top, r * 0.92)):
        t = (z - BED) / (top - BED)
        cx, cy = x + lx * t, y + ly * t
        rings.append([bm.verts.new((cx + rr * math.cos(2 * math.pi * k / 7 + 0.3), cy + rr * math.sin(2 * math.pi * k / 7 + 0.3), z))
                      for k in range(7)])
    for i, (a, b) in enumerate(zip(rings, rings[1:])):
        for k in range(7):
            n = (k + 1) % 7
            f = bm.faces.new((a[k], a[n], b[n], b[k]))
            f.material_index = weed if (i == 1 and weed is not None) else mat
    bm.faces.new(rings[-1]).material_index = mat


def deck_boards(bm, x0, y0, x1, y1, z, rnd, t=0.06, mats=(1, 1, 1, 1, 3), overhang=0.06, ragged=0.04):
    """Deck boards laid across a run along Y (each board runs along X), their top at z: widths that
    vary, ends that don't line up and overhang the frame a little, the odd board a replacement in
    another timber (mats: one picked per board, in turn by chance)."""
    y = y0
    while y < y1 - 0.05:
        w = min(rnd.uniform(0.16, 0.24), y1 - y)
        if y1 - (y + w) < 0.1:
            w = y1 - y
        a = x0 - overhang + rnd.uniform(-ragged, ragged)
        b = x1 + overhang + rnd.uniform(-ragged, ragged)
        A.box(bm, (a, y + 0.007, z - t + rnd.uniform(-0.006, 0.006)), (b, y + w - 0.007, z), rnd.choice(mats))
        y += w


def rope_coil(bm, c, r, mat, turns=3, h=0.04):
    """A coil of rope lying flat: a few rings stacked, each smaller."""
    c = Vector(c)
    for k in range(turns):
        rr = r * (1 - 0.18 * k)
        ring = [(c + Vector((rr * math.cos(2 * math.pi * j / 10), rr * math.sin(2 * math.pi * j / 10), h * (k + 0.5))), h * 0.6, h * 0.6)
                for j in range(11)]
        A.loft(bm, ring, sides=4, ref=(0, 0, 1), mats=mat)


def bollard_at(bm, x, y, z, mat, scale=1.0):
    """An iron bollard: a flanged foot, a waisted post, a broad cap."""
    s = scale
    A.prism(bm, 10, 0.2 * s, 0.17 * s, z - 0.02, z + 0.05 * s, (x, y), mat_index=mat, cap_bottom=True)
    A.prism(bm, 10, 0.13 * s, 0.11 * s, z + 0.05 * s, z + 0.3 * s, (x, y), mat_index=mat)
    A.prism(bm, 10, 0.11 * s, 0.14 * s, z + 0.3 * s, z + 0.38 * s, (x, y), mat_index=mat)
    A.prism(bm, 10, 0.19 * s, 0.17 * s, z + 0.38 * s, z + 0.45 * s, (x, y), mat_index=mat, cap_bottom=True)


def ring_on(bm, c, normal, r, mat):
    """An iron mooring ring hanging from a staple on a wall facing `normal`."""
    c, n = Vector(c), Vector(normal).normalized()
    side = n.cross(Vector((0, 0, 1))).normalized()
    pts = [(c + n * 0.04 + side * r * math.sin(a) - Vector((0, 0, r * (1 - math.cos(a)))), 0.022, 0.022)
           for a in (2 * math.pi * k / 10 for k in range(11))]
    A.loft(bm, pts, sides=4, ref=tuple(n), mats=mat)
    A.hull(bm, [c + side * ss + n * nn + Vector((0, 0, zz)) for ss in (-0.05, 0.05) for nn in (0.0, 0.05) for zz in (-0.03, 0.03)], mat)
