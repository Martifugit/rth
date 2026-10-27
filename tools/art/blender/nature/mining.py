"""Mining nodes for the nature kit: rocks worth swinging a pickaxe at, in three kinds, two variants
each, every one with its worked-out (depleted) state built from the same rock.

- Boulder: a free-standing block of broken bedrock, flat fracture faces, a vein of ore running
  through it as a band standing a little proud, knobbed with lumps of ore.
- Outcrop: bedrock breaking through the turf in tilted beds, the ore one of the beds, its edge
  showing between the plain ones.
- Crag: a tall broken face (the foot of a hill or a cliff), the vein across its front (A), or two
  leaning slabs with the ore filling the fissure between them (B).

Worked out, the same rock keeps its shape: the vein is picked out into a groove or an empty
fissure, its walls fresh broken stone, dark spoil in the bottom, a heap of spoil at the foot.

Every ore shares these models: the ore is the material CopperOre and nothing else, so another ore
is a recolour (objects.json "recolor": {"CopperOre": "IronOre"}). The rest is Scree rock, Cinder
undersides, Moss on what faces the sky, DarkRock for the vein's edges and Gravel for broken stone.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\nature\\basin.py")   (the board)
"""
import math
import random

import bmesh
from mathutils import Matrix, Vector

import rth_art as A
import basin as N

MATS = ["Scree", "Cinder", "Moss", "CopperOre", "DarkRock", "Gravel", "Peat"]
ROCK, SHADE, MOSS, ORE, EDGE, BROKEN, SPOIL = range(len(MATS))


def make(name, bm, at=(0, 0, 0), rot=0.0):
    """The node's object with only the materials it uses (a worked-out rock has no ore slot)."""
    used = sorted({f.material_index for f in bm.faces})
    remap = {m: i for i, m in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    return N.make(name, bm, [MATS[m] for m in used], smooth_angle=28, at=at, rot=rot)


# ---- rock -------------------------------------------------------------------------------------

def rock_points(rnd, size, lean=(0.0, 0.0), sink=0.08, n=18, front=None, ledge=0.85, taper=0.3):
    """Corners of a block of broken bedrock: points round a rounded box (half widths and height) in
    four rough courses, the first four holding the footprint, narrowing by `taper` toward the top,
    the highest cut flat (a ledge), the top shifted by `lean`.
    front: flatten everything in front of this y (a cliff's face)."""
    sx, sy, h = size
    pts = []
    for i in range(n):
        # round the sides by angle, up the height in even steps: walls stay steep, the top broad
        a = i * 2.39996 + rnd.uniform(-0.25, 0.25)              # the golden angle: evenly round
        t = 0.0 if i < 4 else min(1.0, (i % 4) / 3.0 + rnd.uniform(-0.12, 0.12))
        if t > 0.66:
            t = rnd.uniform(0.8, 1.0)
        c, s = math.cos(a), math.sin(a)
        j = rnd.uniform(0.8, 1.02) * (1 - taper * t)
        x = math.copysign(abs(c) ** 0.6, c) * sx * j
        y = math.copysign(abs(s) ** 0.6, s) * sy * j
        if i < 4:
            x, y = (sx if i % 2 else -sx) * rnd.uniform(0.8, 1.0), (sy if i < 2 else -sy) * rnd.uniform(0.8, 1.0)
        z = -sink + t * (h + sink)
        if z > h * ledge:
            z = h * rnd.uniform(ledge + 0.02, 0.97)
        if front is not None and y < front:
            y = front + rnd.uniform(-0.03, 0.0)
        pts.append(Vector((x + lean[0] * h * t, y + lean[1] * h * t, z)))
    return pts


def colour_by_facing(faces, top=MOSS, under=SHADE, side=ROCK, moss_up=0.9):
    for f in faces:
        f.normal_update()
        f.material_index = top if f.normal.z > moss_up else under if f.normal.z < -0.35 else side


def solid(points):
    """A hull in a bmesh of its own, normals out."""
    bm = bmesh.new()
    A.hull(bm, points)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def cut(bm, co, no, cap_mat):
    """Cut away everything on the side `no` points to, and close the cut with a face of cap_mat."""
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_outer=True)
    edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and e.is_valid]
    for f in bmesh.ops.holes_fill(bm, edges=edges)["faces"]:
        f.material_index = cap_mat


def merge(dst, src, matrix=None):
    """Copy src's faces into dst (materials kept), moved by matrix; frees src."""
    vmap = {}
    for v in src.verts:
        vmap[v] = dst.verts.new(matrix @ v.co if matrix else v.co)
    out = []
    for f in src.faces:
        g = dst.faces.new([vmap[v] for v in f.verts])
        g.material_index = f.material_index
        out.append(g)
    src.free()
    return out


def veined(bm, points, centre, normal, half, dug, rnd, lumps=6):
    """A rock (its corners) with a vein through it: the slab within `half` of the plane through
    `centre` across `normal`. Live, the vein stands a little proud, ore with dark edges, knobbed
    with lumps; worked out, it is picked back into a groove whose walls are fresh broken stone."""
    c, n = Vector(centre), Vector(normal).normalized()
    for keep in (1, -1):                                   # the rock either side of the vein
        part = solid(points)
        colour_by_facing(part.faces)
        cut(part, c - n * half * keep, n * keep, BROKEN if dug else EDGE)
        merge(bm, part)
    vein = solid(points)
    cut(vein, c + n * half, n, EDGE)
    cut(vein, c - n * half, -n, EDGE)
    for f in vein.faces:
        f.normal_update()
        if abs(f.normal.dot(n)) < 0.9:
            f.material_index = SPOIL if dug else ORE
    mid = sum((v.co for v in vein.verts), Vector()) / max(len(vein.verts), 1)
    across = Matrix.Identity(3) - Matrix(((n.x * n.x, n.x * n.y, n.x * n.z), (n.y * n.x, n.y * n.y, n.y * n.z),
                                          (n.z * n.x, n.z * n.y, n.z * n.z)))
    s = 0.82 if dug else 1.05                             # proud of the rock, or picked back into it
    m = Matrix.Translation(mid) @ (Matrix.Identity(3) + across * (s - 1)).to_4x4() @ Matrix.Translation(-mid)
    faces = merge(bm, vein, m)
    if not dug:
        lumps_on(bm, [f for f in faces if f.material_index == ORE], rnd, lumps)


def lumps_on(bm, faces, rnd, count, size=(0.05, 0.09)):
    """Knobs of ore half sunk into the faces given, spread over the biggest of them."""
    for f in faces:
        f.normal_update()
    faces = sorted((f for f in faces if f.normal.z > -0.3), key=lambda f: -f.calc_area())
    for f in faces[:count]:
        c, nrm = f.calc_center_median(), f.normal.copy()
        r = rnd.uniform(*size)
        side = nrm.cross(Vector((0, 0, 1)) if abs(nrm.z) < 0.9 else Vector((1, 0, 0))).normalized()
        up = nrm.cross(side)
        pts = [c - nrm * r * 0.5]
        for k in range(6):
            a = 2 * math.pi * k / 6 + rnd.uniform(-0.3, 0.3)
            pts.append(c + (side * math.cos(a) + up * math.sin(a)) * r * rnd.uniform(0.7, 1.0) + nrm * r * 0.15)
        pts.append(c + nrm * r * rnd.uniform(0.5, 0.7))
        for g in A.hull(bm, pts):
            g.material_index = ORE


def chip(bm, c, s, rnd, mat):
    """A flat chip of stone lying on the ground, its foot just under it."""
    c = Vector(c)
    pts = [c + Vector((s * math.cos(a), s * math.sin(a) * rnd.uniform(0.6, 1.0), -0.02))
           for a in (rnd.uniform(0, 1.2), rnd.uniform(2.0, 3.2), rnd.uniform(4.0, 5.4))]
    pts.append(c + Vector((rnd.uniform(-0.3, 0.3) * s, rnd.uniform(-0.3, 0.3) * s, s * rnd.uniform(0.45, 0.7))))
    for f in A.hull(bm, pts):
        f.material_index = mat


def rubble(bm, rnd, spots, mats):
    """Chips at the rock's foot: spots are (x, y, size), mats the colours they take in turn."""
    for k, (x, y, s) in enumerate(spots):
        chip(bm, (x, y, 0), s, rnd, mats[k % len(mats)])


def spoil_heap(bm, at, size, rnd):
    """Worked-out spoil: a low heap of broken stone at the foot, a few chips on and round it."""
    x, y = at
    faces = N.blob(bm, (x, y, 0.0), (size, size * 0.8, size * 0.45), 1, rnd, jitter=(0.85, 1.1),
                   mat=SHADE, squash_below=-0.02)
    for f in faces:
        f.normal_update()
        f.material_index = SPOIL if f.normal.z > 0.7 else SHADE
    for k in range(4):
        a = rnd.uniform(0, 2 * math.pi)
        d = size * rnd.uniform(0.9, 1.3)
        chip(bm, (x + d * math.cos(a), y + d * math.sin(a), 0), rnd.uniform(0.04, 0.07), rnd, (BROKEN, ROCK)[k % 2])


# ---- the nodes ---------------------------------------------------------------------------------

def boulder(variant="A", dug=False, at=(0, 0, 0), name=None):
    """A free-standing block of bedrock, knee to waist high, a vein slanting through it. B is two
    blocks, the vein through the bigger, the smaller fallen against it."""
    rnd = random.Random({"A": 11, "B": 23}[variant])
    bm = bmesh.new()
    if variant == "A":
        pts = rock_points(rnd, (0.58, 0.5, 0.95), lean=(0.04, 0.03))
        veined(bm, pts, (0.05, 0.0, 0.45), (0.85, -0.3, 0.42), 0.065, dug, rnd)
        rubble(bm, rnd, [(0.72, -0.35, 0.11), (0.6, -0.55, 0.08), (-0.7, -0.2, 0.10), (-0.2, -0.68, 0.08)],
               [ROCK, BROKEN] if dug else [ROCK, ORE, ROCK])
        if dug:
            spoil_heap(bm, (0.15, -0.72), 0.22, rnd)
    else:
        pts = [p + Vector((-0.14, 0.04, 0)) for p in rock_points(rnd, (0.5, 0.46, 0.82), lean=(-0.03, 0.02))]
        veined(bm, pts, (-0.2, 0.0, 0.42), (-0.8, -0.35, 0.45), 0.06, dug, rnd, lumps=5)
        small = solid([p + Vector((0.48, -0.12, 0)) for p in rock_points(rnd, (0.28, 0.26, 0.48), lean=(-0.25, 0.0), n=14)])
        colour_by_facing(small.faces)
        merge(bm, small)
        rubble(bm, rnd, [(0.62, 0.32, 0.10), (-0.62, -0.45, 0.11), (0.2, -0.62, 0.08)],
               [ROCK, BROKEN] if dug else [ROCK, ORE])
        if dug:
            spoil_heap(bm, (-0.45, -0.62), 0.2, rnd)
    return make(name or ("MiningBoulder%s%s" % (variant, "Dug" if dug else "")), bm, at=at)


def stratum(rnd, y0, t, length, top, depth=0.7, jag=0.08):
    """One bed of tilted rock before it is tilted: a slab `t` thick from y0, `length` along x,
    rising from `depth` underground to a broken top edge about `top` high, its ends knocked off."""
    pts = []
    xs = [-length / 2 + length * k / 4 for k in range(5)]
    for k, x in enumerate(xs):
        end = k in (0, 4)
        z = top * (0.72 if end else 1.0) + rnd.uniform(-jag, jag)
        x += rnd.uniform(-0.04, 0.04)
        for y in (y0, y0 + t):
            pts.append(Vector((x, y + rnd.uniform(-0.015, 0.015), z - (0.04 if y == y0 else 0.0))))
            if end:
                pts.append(Vector((x * 0.97, y, -depth)))
    return pts


def outcrop(variant="A", dug=False, at=(0, 0, 0), name=None):
    """Bedrock breaking through the turf: tilted beds side by side like the leaves of a book, running
    away from the camera as ribs, their broken tops standing out of the ground at different heights,
    the lower part buried. One bed
    is the ore, standing a little proud of its neighbours and knobbed with lumps. Worked out, the ore
    bed is picked down into a dark slot between them, spoil heaped at the foot."""
    rnd = random.Random({"A": 5, "B": 19}[variant])
    bm = bmesh.new()
    if variant == "A":     # five beds dipping away from the front, the ore the third
        beds = [(0.22, 1.25, 0.3, False), (0.17, 1.35, 0.5, False), (0.13, 1.25, 0.55, True),
                (0.2, 1.3, 0.68, False), (0.24, 1.0, 0.52, False)]
        tilt, turn = 38, 8
    else:                  # four, steeper and shorter, the ore second from the back
        beds = [(0.26, 1.0, 0.42, False), (0.2, 1.15, 0.62, False), (0.12, 1.05, 0.7, True), (0.22, 0.8, 0.5, False)]
        tilt, turn = 52, -14
    total = sum(b[0] for b in beds)
    # the beds run away from the camera (along y), stacked across it, dipping to one side
    place = Matrix.Rotation(math.radians(90 + turn), 4, 'Z') @ Matrix.Rotation(math.radians(-tilt), 4, 'X')
    across = place.to_3x3() @ Vector((0, 1, 0))
    ore_at = next(i for i, b in enumerate(beds) if b[3])
    y = -total / 2
    for i, (t, length, top, is_ore) in enumerate(beds):
        if is_ore:
            if dug:
                part = solid(stratum(rnd, y + 0.01, t - 0.02, length * 0.92, top * 0.45, jag=0.03))
                for f in part.faces:
                    f.material_index = SPOIL
                merge(bm, part, place)
            else:
                part = solid(stratum(rnd, y - 0.005, t + 0.01, length * 1.02, top + 0.06, jag=0.04))
                for f in part.faces:
                    f.material_index = ORE
                faces = merge(bm, part, place)
                for f in faces:
                    f.normal_update()
                lumps_on(bm, [f for f in faces if f.normal.z > 0.2], rnd, 5, size=(0.05, 0.08))
        else:
            part = solid(stratum(rnd, y, t, length, top))
            for f in part.faces:
                f.normal_update()
            faces = merge(bm, part, place)
            for f in faces:
                f.normal_update()
            colour_by_facing(faces, moss_up=0.93)
            if dug and abs(i - ore_at) == 1:           # the slot's walls: fresh broken stone
                toward = across * (1 if i < ore_at else -1)
                for f in faces:
                    if f.normal.dot(toward) > 0.9:
                        f.material_index = BROKEN
        y += t
    rubble(bm, rnd, [(0.85, -0.45, 0.10), (-0.8, -0.35, 0.11), (0.25, -0.75, 0.08)],
           [ROCK, BROKEN] if dug else [ROCK, ORE])
    if dug:
        spoil_heap(bm, (-0.25, -0.78), 0.22, rnd)
    return make(name or ("MiningOutcrop%s%s" % (variant, "Dug" if dug else "")), bm, at=at)


def crag(variant="A", dug=False, at=(0, 0, 0), name=None):
    """A tall broken face to set at the foot of a hill or cliff, its flat front toward -Y. A: one
    block, the vein slanting across its face. B: two slabs leaning a little apart, the ore filling
    the fissure between them and bulging out of it; worked out, the fissure is empty and dark."""
    rnd = random.Random({"A": 41, "B": 47}[variant])
    bm = bmesh.new()
    if variant == "A":
        pts = rock_points(rnd, (0.62, 0.48, 1.6), lean=(0.0, 0.06), front=-0.4, n=20, ledge=0.9)
        veined(bm, pts, (0.0, -0.2, 0.8), (0.75, 0.1, 0.6), 0.075, dug, rnd, lumps=7)
        rubble(bm, rnd, [(0.55, -0.62, 0.13), (-0.4, -0.66, 0.10), (0.05, -0.75, 0.08), (-0.75, -0.3, 0.11)],
               [ROCK, BROKEN] if dug else [ROCK, ORE, ROCK])
        if dug:
            spoil_heap(bm, (0.25, -0.78), 0.24, rnd)
    else:
        for x, size, lean in ((-0.36, (0.36, 0.45, 1.5), (-0.1, 0.04)), (0.37, (0.34, 0.42, 1.25), (0.1, 0.05))):
            part = solid([p + Vector((x, 0, 0)) for p in rock_points(rnd, size, lean=lean, front=-0.36, n=20, ledge=0.8, taper=0.12)])
            colour_by_facing(part.faces)
            merge(bm, part)
        if dug:
            pts = [Vector((x, y, z)) for x, y, z in ((-0.06, -0.05, 0.1), (0.07, -0.05, 0.1), (-0.06, 0.3, 0.1), (0.07, 0.3, 0.1),
                                                     (-0.08, -0.02, 1.0), (0.08, -0.02, 1.0), (0.0, 0.3, 1.05))]
            for f in A.hull(bm, pts):
                f.material_index = SPOIL
            spoil_heap(bm, (0.05, -0.62), 0.24, rnd)
        else:
            pts = []
            for z, w, d in ((0.05, 0.12, -0.3), (0.35, 0.1, -0.34), (0.7, 0.08, -0.32), (1.0, 0.06, -0.26), (1.18, 0.02, -0.15)):
                pts += [Vector((-w, d, z)), Vector((w, d, z)), Vector((-w, 0.3, z)), Vector((w, 0.3, z))]
            faces = A.hull(bm, pts)
            for f in faces:
                f.material_index = ORE
            lumps_on(bm, faces, rnd, 6)
        rubble(bm, rnd, [(0.6, -0.6, 0.11), (-0.62, -0.55, 0.10), (-0.2, -0.72, 0.08)],
               [ROCK, BROKEN] if dug else [ROCK, ORE])
    return make(name or ("MiningCrag%s%s" % (variant, "Dug" if dug else "")), bm, at=at)


KINDS = {"Boulder": boulder, "Outcrop": outcrop, "Crag": crag}

# Name in the game (Resources/Nature/<Name>) to builder: every kind's variants and their worked-out states.
ASSETS = {"Mining%s%s%s" % (kind, v, "Dug" if dug else ""): (lambda fn=fn, v=v, dug=dug: fn(v, dug))
          for kind, fn in KINDS.items() for v in "AB" for dug in (False, True)}


def row(y, x0=2.0, ores=("CopperOre", "IronOre")):
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
