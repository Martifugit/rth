"""Style board for the environment art: a patch of Tolling's basin (grass, the mere's edge, a
trodden path) with one of each kind of nature asset wave 1 needs, the player for scale.

The references (images/osrs-tree-*, -foliage-*, -fence-*) set the technique only: few big faces,
leaf textures with a ragged hem, smooth light, flat-coloured timber. The forms are R'th's own: a
worked, weathered land under a wind off the spine. Each tree has its own habit and its own leaf
(a coppiced alder stool, a tall airy ash, a wind-flagged hawthorn, pollard willows on the bank) and
a felled state; bark is dark and crooked; berry bushes have a picked state; fences are bucks and
woven hurdles; the grass grows in tussocks, with rust-red dock. Textures are this kit's own
(nature/textures.py). The rules that passed are in tools/art/blender/README.md ("Nature").

Each asset is its own builder at the origin (the middle of its tile), moved onto the board.
Running the script builds the board; with EXPORT set it writes every asset in ASSETS to
client/Assets/Resources/Nature/<Name>.fbx and copies the textures to client/Assets/Art/Textures
(runpy.run_path(path, init_globals={"EXPORT": True})).

    import runpy; ns = runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\nature\\basin.py")
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import rth_art as A  # noqa: E402

A._palette = None                  # read the palette afresh: Blender keeps the module between runs

TEXTURES = os.path.join(HERE, "textures")


# ---- materials --------------------------------------------------------------------------------

def leaf_mat(name):
    """A leaf texture from nature/textures (Foliage, Willow), cut out at alpha 0.5, both sides
    drawn, point filtered."""
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.surface_render_method = "DITHERED"
    m.use_backface_culling = False
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Specular IOR Level"].default_value = 0.0
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEXTURES, name + ".png"), check_existing=True)
    tex.interpolation = "Closest"
    clip = nodes.new("ShaderNodeMath")
    clip.operation = "GREATER_THAN"
    clip.inputs[1].default_value = 0.5
    links.new(tex.outputs["Alpha"], clip.inputs[0])
    links.new(clip.outputs[0], bsdf.inputs["Alpha"])
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    m.diffuse_color = (0.2, 0.25, 0.1, 1)
    return m


def make(name, bm, materials, smooth_angle=None, coll="Nature", leaf_uvs=None, at=(0, 0, 0), rot=0.0,
         finish=True):
    """A bmesh to an object, materials by name (palette colours or ready materials). leaf_uvs:
    {face index: [uv per corner]} for faces wearing the leaf texture (those need finish=False, so
    their corners keep their order)."""
    if finish:
        A.finish(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for n in materials:
        me.materials.append(n if isinstance(n, bpy.types.Material) else A.mat(n))
    A.box_uvs(me)
    if leaf_uvs:
        uv = me.uv_layers[0]
        for fi, uvs in leaf_uvs.items():
            p = me.polygons[fi]
            for li, c in zip(p.loop_indices, uvs):
                uv.data[li].uv = c
    ob = bpy.data.objects.new(name, me)
    A.collection(coll).objects.link(ob)
    if smooth_angle is not None:
        for p in me.polygons:
            p.use_smooth = True
        with bpy.context.temp_override(object=ob, active_object=ob, selected_editable_objects=[ob]):
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth_angle))
    ob.location = at
    ob.rotation_euler.z = rot
    return ob


# ---- shapes -----------------------------------------------------------------------------------

def ico_points(subdiv):
    """Unit icosphere corners and triangles (index lists)."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    bm.verts.ensure_lookup_table()
    pts = [v.co.normalized() for v in bm.verts]
    tris = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return pts, tris


def blob(bm, centre, radii, subdiv, rnd, jitter=(0.88, 1.1), mat=0, squash_below=None):
    """A lumpy closed solid: an icosphere with each corner pushed in or out. squash_below: flatten
    everything under this height (a rock's buried base)."""
    pts, tris = ico_points(subdiv)
    c = Vector(centre)
    vs = []
    for p in pts:
        q = c + Vector((p.x * radii[0], p.y * radii[1], p.z * radii[2])) * rnd.uniform(*jitter)
        if squash_below is not None and q.z < squash_below:
            q.z = squash_below
        vs.append(bm.verts.new(q))
    faces = []
    for t in tris:
        f = bm.faces.new([vs[i] for i in t])
        f.material_index = mat
        faces.append(f)
    return faces


def branch(bm, base, tip, r0, r1=None, mat=0, sides=4):
    """A tapering limb from base to tip, base left open (bury it in what it grows from). r1=None
    ends in a point."""
    base, tip = Vector(base), Vector(tip)
    if r1 is None:
        return A.spike(bm, base, r0, tip, sides=sides, mat=mat)
    t = (tip - base).normalized()
    ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((0, 1, 0))
    up = (ref - t * ref.dot(t)).normalized()
    side = t.cross(up)
    rings = []
    for c, r in ((base, r0), (tip, r1)):
        rings.append([bm.verts.new(c + (side * math.cos(2 * math.pi * k / sides) + up * math.sin(2 * math.pi * k / sides)) * r)
                      for k in range(sides)])
    a, b = rings
    for k in range(sides):
        n = (k + 1) % sides
        bm.faces.new((a[k], a[n], b[n], b[k])).material_index = mat
    bm.faces.new(b).material_index = mat
    return rings


# ---- the assets -------------------------------------------------------------------------------

WIND = Vector((1.0, 0.15, 0.0)).normalized()   # the weather comes off the spine, from the west


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


def leafy(name, bm, uvs, materials, at, smooth=60):
    """Make a tree object; its textured faces (uvs: face -> uv per corner) get their UVs. Faces
    are not triangulated or turned, so their corners keep the order the UVs were given in."""
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in uvs])
    index = {f: i for i, f in enumerate(bm.faces)}
    leaf_uvs = {index[f]: u for f, u in uvs.items()}
    return make(name, bm, materials, smooth_angle=smooth, leaf_uvs=leaf_uvs, at=at, finish=False)


def clump(bm, centre, radii, rnd, mat, core, uvs, sides=7):
    """A clump of leaves: a rounded cushion whose leaf texture runs from its crown down to its rim,
    so the ragged hem of leaves hangs only along the bottom edge; a dark underside closes it. A
    clump standing on the ground wants its centre at about 0.3 of its height (`grounded`): the
    hem's transparent foot leaves the visible leaves well above the rim."""
    c = Vector(centre)
    rx, ry, rz = radii
    # Rings from the top: (angle down from the crown, how far out the rim swings, v on the texture).
    bands = ((50, 1.0, 0.62), (90, 1.0, 0.32), (122, 1.08, 0.0))
    turn = rnd.uniform(0, 1)
    apex = bm.verts.new(c + Vector((rnd.uniform(-0.05, 0.05) * rx, 0, rz * rnd.uniform(0.95, 1.05))))
    rings = []
    for polar, swing, v in bands:
        t = math.radians(polar)
        ring = []
        for k in range(sides):
            a = turn + 2 * math.pi * (k + 0.5 * (len(rings) % 2)) / sides
            j = rnd.uniform(0.88, 1.1)
            ring.append(bm.verts.new(c + Vector((math.sin(t) * math.cos(a) * rx * j * swing,
                                                 math.sin(t) * math.sin(a) * ry * j * swing,
                                                 math.cos(t) * rz * rnd.uniform(0.92, 1.08)))))
        rings.append((ring, v))
    top, v_top = rings[0]
    for k in range(sides):
        n = (k + 1) % sides
        f = bm.faces.new((top[k], top[n], apex))
        f.material_index = mat
        uvs[f] = [(0, v_top), (1, v_top), (0.5, 1)]
    for (up, vu), (lo, vl) in zip(rings, rings[1:]):
        for k in range(sides):
            n = (k + 1) % sides
            f = bm.faces.new((lo[k], lo[n], up[n], up[k]))
            f.material_index = mat
            uvs[f] = [(0, vl), (1, vl), (1, vu), (0, vu)]
    rim, _ = rings[-1]
    under = [bm.verts.new(c + (v.co - c) * 0.82 + Vector((0, 0, rz * 0.12))) for v in rim]
    bm.faces.new(list(reversed(under))).material_index = core


def grounded(x, y, radii):
    """The centre for a clump of these radii standing on the ground at (x, y)."""
    return (x, y, radii[2] * 0.3)


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
    for k, h in enumerate((3.3, 2.8, 2.5)):
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
    pts = [Vector((0, 0, -0.05)), Vector((0.08, 0.0, 0.7)), WIND * 0.55 + Vector((0, 0, 1.3)),
           WIND * 1.2 + Vector((0, 0, 1.6))]
    rod(bm, pts, 0.14, sides=6, taper=0.4)
    roots(bm, 0.14, 0.45, 3, rnd, rot=2.0)
    for d, side, z, radii in ((1.25, 0.0, 1.8, (1.1, 0.75, 0.6)), (0.55, 0.3, 1.45, (0.6, 0.5, 0.42)),
                              (1.95, -0.15, 1.45, (0.55, 0.45, 0.35))):
        c = WIND * d + Vector((-WIND.y, WIND.x, 0)) * side + Vector((0, 0, z))
        clump(bm, c, radii, rnd, 1, 2, uvs)
    return leafy("Hawthorn", bm, uvs, ["Bark", leaf_mat("Thorn"), "Leaf1"], at)


def boulders(seed=6, at=(0, 0, 0), size=1.0):
    """A rock cluster: one big rounded stone, two small ones leaning on it, a scatter of flat
    chips. One colour; smooth light does the rest."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    blob(bm, (0, 0, 0.32), (0.75, 0.6, 0.55), 1, rnd, jitter=(0.85, 1.12), squash_below=-0.05)
    blob(bm, (0.7, -0.35, 0.15), (0.38, 0.32, 0.3), 0, rnd, jitter=(0.85, 1.15), squash_below=-0.05)
    blob(bm, (-0.55, 0.45, 0.12), (0.3, 0.28, 0.22), 0, rnd, jitter=(0.85, 1.15), squash_below=-0.05)
    for _ in range(9):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.9, 1.6)
        c = Vector((d * math.cos(a), d * math.sin(a), 0))
        s = rnd.uniform(0.05, 0.1)
        A.hull(bm, [c + Vector((s * math.cos(t), s * math.sin(t), -0.01)) for t in
                    (rnd.uniform(0, 1), rnd.uniform(2, 3), rnd.uniform(4, 5))] + [c + Vector((0, 0, s * 0.6))])
    for v in bm.verts:
        v.co *= size
    return make("Boulders", bm, ["Scree"], smooth_angle=40, at=at)


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


def wall_span(seed=1, at=(0, 0, 0), rot=0.0):
    """One tile of dry-stone field wall along X: a battered body (narrower at the top) in field
    stone, a row of cope stones on edge along its top. The texture carries the coursing."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    h, wb, wt = 0.62, 0.24, 0.17
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


def rod(bm, points, r, mat=0, sides=5, taper=1.0, tip=None):
    """A crooked round timber through points (a post, a rail, a stake), its radius tapering to
    r * taper; tip: a point the end comes to (a sharpened stake)."""
    pts = [Vector(p) for p in points]
    secs = []
    for i, p in enumerate(pts):
        rr = r * (1 + (taper - 1) * i / (len(pts) - 1))
        secs.append((p, rr, rr))
    if tip is not None:
        secs.append((Vector(tip), 0, 0))
    t = (pts[-1] - pts[0]).normalized()
    ref = (1, 0, 0) if abs(t.z) > 0.7 else (0, 0, 1)
    A.loft(bm, secs, sides=sides, ref=ref, mats=mat, roll=math.pi / sides)


def wobble(rnd, a, b, n, amount):
    """n points from a to b, the inner ones pushed aside a little: a crooked timber's line."""
    a, b = Vector(a), Vector(b)
    out = []
    for i in range(n):
        p = a.lerp(b, i / (n - 1))
        if 0 < i < n - 1:
            p += Vector((rnd.uniform(-amount, amount), rnd.uniform(-amount, amount), rnd.uniform(-amount, amount)))
        out.append(p)
    return out


def buck_fence(seed=1, at=(0, 0, 0), end_post=False):
    """A buck fence, one tile along X: no posts dug in, a pair of poles crossed like an X at the
    span's start with the rail laid in their crotch, and a lower rail tied to the legs. Weathered
    grey. end_post adds the closing buck."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for x in ((0.0, 1.0) if end_post else (0.0,)):
        for s in (-1, 1):
            foot = Vector((x + s * 0.04, s * 0.32, -0.05))
            head = Vector((x - s * 0.03, -s * 0.24, 1.1 + rnd.uniform(-0.05, 0.05)))
            rod(bm, wobble(rnd, foot, head, 3, 0.03), 0.04, sides=4, taper=0.8)
    crotch = 0.66
    rod(bm, wobble(rnd, (-0.08, 0.0, crotch + 0.03), (1.08, 0.0, crotch + 0.05), 4, 0.03), 0.045, sides=5)
    rod(bm, wobble(rnd, (-0.05, 0.17, 0.3), (1.05, 0.17, 0.32), 3, 0.03), 0.035, sides=4)
    return make("BuckFence", bm, ["Driftwood"], smooth_angle=50, at=at)


def hurdle(seed=1, at=(0, 0, 0), end_post=False):
    """A woven hurdle, one tile along X: a panel of woven rods (painted, not modelled) bowing a
    little in the middle, hung on a round stake at the span's start, a binding rod along its top."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for x in ((0.0, 1.0) if end_post else (0.0,)):
        rod(bm, wobble(rnd, (x, 0, -0.05), (x + rnd.uniform(-0.03, 0.03), 0, 1.0), 3, 0.02), 0.045,
            sides=5, taper=0.85, tip=(x, 0, 1.06))
    uvs = {}
    cols = [(0.04, 0.0), (0.5, 0.035), (0.96, 0.0)]
    lo = [bm.verts.new((x, y, 0.06 + rnd.uniform(-0.02, 0.02))) for x, y in cols]
    hi = [bm.verts.new((x, y, 0.84 + rnd.uniform(-0.03, 0.03))) for x, y in cols]
    for k in range(2):
        f = bm.faces.new((lo[k], lo[k + 1], hi[k + 1], hi[k]))
        f.material_index = 1
        u0, u1 = k * 0.5, (k + 1) * 0.5
        uvs[f] = [(u0, 0), (u1, 0), (u1, 0.85), (u0, 0.85)]
    rod(bm, [hi[0].co + Vector((-0.02, 0, 0.02)), hi[1].co + Vector((0, 0, 0.02)), hi[2].co + Vector((0.02, 0, 0.02))],
        0.025, sides=4)
    return leafy("Hurdle", bm, uvs, ["Driftwood", leaf_mat("Wattle")], at, smooth=50)


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


def cut_stub(bm, centre, r, h, slant, rnd, sides=6, lean=(0, 0)):
    """A felled stem: a short prism whose top is one slanted axe cut, the cut face pale wood."""
    cx, cy, cz = centre
    a0 = rnd.uniform(0, math.pi)
    d = Vector((math.cos(a0), math.sin(a0), 0))
    lo, hi = [], []
    for k in range(sides):
        a = 2 * math.pi * k / sides
        p = Vector((r * math.cos(a), r * math.sin(a), 0))
        lo.append(bm.verts.new((cx + p.x * 1.15, cy + p.y * 1.15, cz)))
        hi.append(bm.verts.new((cx + p.x + lean[0], cy + p.y + lean[1], cz + h + p.dot(d) / r * slant)))
    for k in range(sides):
        n = (k + 1) % sides
        bm.faces.new((lo[k], lo[n], hi[n], hi[k])).material_index = 0
    bm.faces.new(hi).material_index = 1


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


def slab(bm, centre, r, h, sides, rnd, rot=0.0, side_mat=0, top_mat=1):
    """A flat irregular slab of layered stone: straight sides, a paler top."""
    cx, cy, cz = centre
    jit = {}

    def j(i, ring):
        if i not in jit:
            jit[i] = (rnd.uniform(-0.25, 0.15) * r, rnd.uniform(-0.25, 0.15) * r)
        dx, dy = jit[i]
        a = rot + 2 * math.pi * i / sides
        k = 1.0 if ring == 0 else 0.93
        return (dx * math.cos(a) * k, dy * math.sin(a) * k, rnd.uniform(-0.02, 0.02) if ring else 0)

    (_, _), faces = A.prism(bm, sides, r, r * 0.93, cz, cz + h, (cx, cy), rot=rot, mat_index=side_mat,
                            cap_top=True, cap_bottom=True, jitter=j)
    faces[sides].material_index = top_mat


def cairn(seed=4, at=(0, 0, 0)):
    """A cairn on the downs: flat stones piled in courses, each a little askew, and one slab set on
    end at the top as a marker."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    z, r = 0.0, 0.55
    for i in range(6):
        h = rnd.uniform(0.12, 0.17)
        slab(bm, (rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04), z), r, h, 6, rnd, rot=rnd.uniform(0, 3))
        z += h - 0.01
        r *= 0.86
    A.box(bm, (-0.12, -0.04, z - 0.05), (0.12, 0.04, z + 0.45), 1)
    for v in list(bm.verts)[-8:]:
        v.co.x += (v.co.z - z) * 0.08
    return make("Cairn", bm, ["Scree", "Gravestone"], smooth_angle=30, at=at)


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
    for x, z in ((0.3, 0.18), (0.5, 0.38), (0.7, 0.58)):
        w = rnd.uniform(0.1, 0.13)                   # a flat stone through the wall, out both sides
        A.box(bm, (x - w, -0.42, z), (x + w, 0.42, z + 0.07), 1)
    bm.to_mesh(ob.data)
    bm.free()
    A.box_uvs(ob.data)
    ob.name = ob.data.name = "WallStile"
    return ob


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


def mooring_post(seed=1, at=(0, 0, 0)):
    """A mooring post standing in the shallows (origin at the water's surface): a leaning round
    post driven into the bed, a rope wound round it and trailing off into the water."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    rod(bm, wobble(rnd, (0, 0, -0.8), (0.06, 0.03, 0.95), 3, 0.03), 0.09, mat=0, sides=6, taper=0.85)
    rod(bm, [(0.04, 0.02, 0.5), (0.045, 0.022, 0.62)], 0.105, mat=1, sides=6)
    rod(bm, [(0.1, 0.03, 0.55), (0.45, 0.1, 0.3), (0.85, 0.05, -0.02)], 0.016, mat=1, sides=3)
    return make("MooringPost", bm, ["Driftwood", "Wheat"], smooth_angle=50, at=at)


def jetty_span(seed=1, at=(0, 0, 0), rot=0.0):
    """One tile of jetty along X, centred on its tile, its origin at the water's surface: planks
    laid across on two stringers, each end carried by a pair of piles driven into the bed."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    deck = 0.32
    for k in range(5):
        x0 = -0.5 + 0.2 * k + 0.01
        dz = rnd.uniform(-0.015, 0.015)
        A.box(bm, (x0, -0.55 + rnd.uniform(-0.03, 0.03), deck - 0.05 + dz),
              (x0 + 0.18, 0.55 + rnd.uniform(-0.03, 0.03), deck + dz), 0)
    for y in (-0.42, 0.42):
        A.box(bm, (-0.5, y - 0.05, deck - 0.14), (0.5, y + 0.05, deck - 0.05), 1)
        rod(bm, [(-0.45, y, -0.9), (-0.45, y, deck - 0.02)], 0.07, mat=1, sides=6)
    return make("JettySpan", bm, ["Planks", "Driftwood"], smooth_angle=30, at=at, rot=rot)


def punt(seed=1, at=(0, 0, 0), rot=0.0):
    """A flat-bottomed punt for the mere, its origin at the water's surface: square ends rising a
    little, plank walls with an inside, a seat across, and its pole lying along it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    sections = [(-1.15, 0.30, 0.10), (-0.85, 0.42, -0.06), (0.85, 0.42, -0.06), (1.15, 0.30, 0.10)]
    wall, t = 0.24, 0.04
    rings = []
    for x, w, z in sections:
        outer = [Vector((x, -w, z + wall)), Vector((x, -w, z)), Vector((x, w, z)), Vector((x, w, z + wall))]
        inner = [Vector((x, -w + t, z + wall)), Vector((x, -w + t, z + t)), Vector((x, w - t, z + t)), Vector((x, w - t, z + wall))]
        rings.append(([bm.verts.new(p) for p in outer], [bm.verts.new(p) for p in inner]))
    for (o0, i0), (o1, i1) in zip(rings, rings[1:]):
        for k in range(3):
            bm.faces.new((o0[k], o1[k], o1[k + 1], o0[k + 1]))
            bm.faces.new((i0[k], i0[k + 1], i1[k + 1], i1[k]))
        bm.faces.new((o0[0], i0[0], i1[0], o1[0]))           # the gunwales
        bm.faces.new((o0[3], o1[3], i1[3], i0[3]))
    for o, i in (rings[0], rings[-1]):                       # the square ends
        for k in range(3):
            bm.faces.new((o[k], o[k + 1], i[k + 1], i[k]))
    A.box(bm, (0.15, -0.38, 0.08), (0.42, 0.38, 0.13), 0)   # the seat
    rod(bm, [(-1.0, 0.12, 0.2), (0.4, 0.05, 0.24), (1.5, -0.08, 0.3)], 0.022, sides=4)
    return make("Punt", bm, ["Driftwood"], smooth_angle=30, at=at, rot=rot)


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


def monolith(bm, length, half, twist, rnd, mat=0):
    """A four-sided stone column, each course turned a little further than the one below, so it
    twists as it rises: a shape no mason in R'th makes. Returns nothing; flat top."""
    rings = []
    n = 6
    for i in range(n + 1):
        z = -0.1 + length * i / n
        taper = 1 - 0.25 * i / n
        a = twist * i / n
        ring = []
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            x, y = sx * half[0] * taper, sy * half[1] * taper
            ring.append(bm.verts.new((x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a), z)))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k])).material_index = mat
    bm.faces.new(rings[-1]).material_index = mat
    bm.faces.new(list(reversed(rings[0]))).material_index = mat


def standing_stone(seed=1, at=(0, 0, 0)):
    """A standing stone on the heath in a style nobody made: a tall pale column twisted through a
    quarter turn as it rises, too regular for weather, leaning a little."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    monolith(bm, 2.5, (0.34, 0.22), math.radians(80), rnd)
    from mathutils import Matrix as M
    bmesh.ops.transform(bm, matrix=M.Rotation(math.radians(5), 4, Vector((1, 0.3, 0)).normalized()), verts=bm.verts)
    return make("StandingStone", bm, ["Gravestone"], smooth_angle=20, at=at)


def standing_stone_fallen(seed=2, at=(0, 0, 0)):
    """One of the twisted stones fallen and half sunk into the turf."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    monolith(bm, 2.2, (0.32, 0.2), math.radians(80), rnd)
    from mathutils import Matrix as M
    bmesh.ops.transform(bm, matrix=M.Rotation(math.radians(84), 4, Vector((0, 1, 0))), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((-1.0, 0, 0.04)), verts=bm.verts)
    return make("StandingStoneFallen", bm, ["Gravestone"], smooth_angle=20, at=at)


def grave(seed=1, at=(0, 0, 0), old=False):
    """A grave: a plain headstone with a rounded top and a low turf mound before it. old: the stone
    leaning and sunk, the mound grown down almost flat."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    w, h, t = 0.24, 0.62, 0.05
    prof = [(-w, -0.1), (w, -0.1), (w, h - 0.1)] + \
           [(w * math.cos(a), h - 0.1 + 0.12 * math.sin(a)) for a in (math.radians(40), math.radians(90), math.radians(140))] + \
           [(-w, h - 0.1)]
    A.hull(bm, [(x, y, z) for x, z in prof for y in (-t, t)], mat=0)
    if old:
        from mathutils import Matrix as M
        bmesh.ops.transform(bm, matrix=M.Rotation(math.radians(rnd.choice((-1, 1)) * 11), 4, Vector((0, 1, 0.2)).normalized()),
                            verts=bm.verts)
        bmesh.ops.translate(bm, vec=Vector((0, 0, -0.14)), verts=bm.verts)
    mound_h = 0.06 if old else 0.16
    blob(bm, (0, -0.55, 0.0), (0.28, 0.48, mound_h), 1, rnd, jitter=(0.92, 1.06), mat=1, squash_below=-0.02)
    return make("GraveOld" if old else "Grave", bm, ["Gravestone", "GraveTurf"], smooth_angle=40, at=at)


def hay_stook(seed=1, at=(0, 0, 0)):
    """A stook of sheaves drying in the field: six sheaves leaning together, each tied at the waist."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    for k in range(6):
        a = 2 * math.pi * k / 6 + rnd.uniform(-0.15, 0.15)
        out = Vector((math.cos(a), math.sin(a), 0))
        foot = out * 0.3 + Vector((0, 0, -0.02))
        waist = out * 0.17 + Vector((0, 0, 0.45))
        head = out * 0.08 + Vector((0, 0, 0.78 + rnd.uniform(-0.04, 0.04)))
        A.loft(bm, [(foot, 0.13, 0.13), (waist - Vector((0, 0, 0.04)), 0.075, 0.075),
                    (waist + Vector((0, 0, 0.04)), 0.075, 0.075), (head, 0.12, 0.12),
                    (head + Vector((0, 0, 0.14)), 0, 0)], sides=6, ref=(1, 0, 0), mats=[0, 1, 0, 0])
    return make("HayStook", bm, ["Wheat", "Bark"], smooth_angle=50, at=at)


def farm_cart(seed=1, at=(0, 0, 0), rot=0.0):
    """A two-wheeled farm cart at rest, its shafts down on the ground: a plank bed with low sides,
    two spoked wheels on an axle."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    A.box(bm, (-0.75, -0.45, 0.48), (0.75, 0.45, 0.56), 0)
    for y in (-0.45, 0.41):
        A.box(bm, (-0.75, y, 0.56), (0.75, y + 0.04, 0.78), 0)
    A.box(bm, (-0.75, -0.41, 0.56), (-0.71, 0.41, 0.76), 0)
    # Wheels: ten-sided rims as short tubes along Y, a hub, four spokes.
    for s in (-1, 1):
        y = s * 0.56
        ring_a = [bm.verts.new((0.42 * math.cos(2 * math.pi * k / 10), y - 0.03, 0.42 + 0.42 * math.sin(2 * math.pi * k / 10))) for k in range(10)]
        ring_b = [bm.verts.new((v.co.x * 0.84, y - 0.03, 0.42 + (v.co.z - 0.42) * 0.84)) for v in ring_a]
        ring_c = [bm.verts.new((v.co.x, y + 0.03, v.co.z)) for v in ring_a]
        ring_d = [bm.verts.new((v.co.x, y + 0.03, v.co.z)) for v in ring_b]
        for k in range(10):
            n = (k + 1) % 10
            bm.faces.new((ring_a[k], ring_a[n], ring_c[n], ring_c[k])).material_index = 1
            bm.faces.new((ring_b[n], ring_b[k], ring_d[k], ring_d[n])).material_index = 1
            bm.faces.new((ring_a[n], ring_a[k], ring_b[k], ring_b[n])).material_index = 1
            bm.faces.new((ring_c[k], ring_c[n], ring_d[n], ring_d[k])).material_index = 1
        for k in range(4):
            a = math.pi / 4 + math.pi / 2 * k
            rod(bm, [(0, y, 0.42), (0.37 * math.cos(a), y, 0.42 + 0.37 * math.sin(a))], 0.02, mat=1, sides=4)
        rod(bm, [(0, y - 0.06, 0.42), (0, y + 0.06, 0.42)], 0.06, mat=1, sides=6)
    rod(bm, [(0, -0.6, 0.42), (0, 0.6, 0.42)], 0.03, mat=1, sides=4)
    for s in (-1, 1):
        rod(bm, [(0.7, s * 0.32, 0.5), (1.4, s * 0.34, 0.27), (2.0, s * 0.36, 0.04)], 0.035, mat=1, sides=4)
    return make("FarmCart", bm, ["Planks", "Driftwood"], smooth_angle=35, at=at, rot=rot)


# ---- the board --------------------------------------------------------------------------------

W, H = 44, 38
WATER_Z = -0.18


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


def ground_z(x, y):
    """The mere's bank along the south edge: level grass from y 4.5, dipping under the water by y 1."""
    shore = 3.2 + 0.8 * math.sin(x * 0.45) + 0.4 * math.sin(x * 1.3 + 1)
    d = y - shore
    if d >= 1.2:
        return 0.0
    if d <= -1.0:
        return -0.55
    t = (d + 1.0) / 2.2
    return -0.55 + 0.55 * (t * t * (3 - 2 * t))


def ground():
    """Plain grass with the mere along the front edge (mud at the waterline, the water one flat
    surface). Tile corners at whole numbers, like the game's ground."""
    bm = bmesh.new()
    vs = {}
    for x in range(W + 1):
        for y in range(H + 1):
            vs[x, y] = bm.verts.new((x, y, ground_z(x, y)))
    for x in range(W):
        for y in range(H):
            f = bm.faces.new((vs[x, y], vs[x + 1, y], vs[x + 1, y + 1], vs[x, y + 1]))
            f.material_index = 1 if ground_z(x + 0.5, y + 0.5) < WATER_Z + 0.02 else 0
    water = [bm.verts.new(p) for p in ((0, 0, WATER_Z), (W, 0, WATER_Z), (W, 5.2, WATER_Z), (0, 5.2, WATER_Z))]
    bm.faces.new(water).material_index = 2
    return make("Ground", bm, ["Grass", "Mud", "Mere"], coll="Preview")


ROW_TREES, ROW_BUSHES, ROW_STONE, ROW_BOUNDS, ROW_PLACES, ROW_MINING, ROW_WILDS, ROW_WAYS = (
    8.0, 12.5, 16.0, 19.5, 23.0, 27.0, 31.0, 35.0)


def build():
    """The board, in rows from the water back: the mere's edge; the trees, each with its felled
    state beside it; bushes and plants; stone; boundaries; places (the heath's stones, graves, the
    farm); mining (nature/mining.py: each kind's variants in copper and iron, then worked out);
    the wilds and the ways (nature/wilds.py: ground cover, deadwood, loose stone; ways, camps and
    work)."""
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Nature", "Preview"):
            bpy.data.collections.remove(c)
    ground()
    objs = {}
    for i, x in enumerate((7.0, 13.5, 22.0)):
        objs["reeds_%d" % i] = reeds(seed=3 + 5 * i, at=(x, 2.4, ground_z(x, 2.4) + 0.2))
    objs["lily_pads"] = lily_pads(at=(17.5, 1.6, WATER_Z))
    objs["lily_pads_2"] = lily_pads(seed=4, at=(10.0, 1.2, WATER_Z))
    for i, y in enumerate((3.5, 2.5, 1.5, 0.5)):       # a jetty out from the bank
        objs["jetty_%d" % i] = jetty_span(seed=i + 1, at=(30.5, y, WATER_Z), rot=math.pi / 2)
    objs["mooring_post"] = mooring_post(at=(31.4, 1.2, WATER_Z))
    objs["punt"] = punt(at=(32.2, 1.3, WATER_Z), rot=math.pi / 2 + 0.1)
    t = ROW_TREES
    objs.update({
        "alder": alder(at=(3.0, t, 0)), "stool_cut": stool_cut(at=(5.4, t, 0)),
        "ash": ash(at=(9.5, t, 0)), "ash_stump": ash_stump(at=(12.3, t, 0)),
        "hawthorn": hawthorn(at=(15.0, t, 0)), "hawthorn_stump": hawthorn_stump(at=(19.6, t, 0)),
        "pollard": pollard(at=(22.8, t, 0)), "pollard_stump": pollard_stump(at=(25.2, t, 0)),
        "snag": snag(at=(28.6, t, 0)),
        "elm": elm(at=(32.0, t, 0)), "elm_stump": elm_stump(at=(35.2, t, 0)),
        "pine": pine(at=(38.5, t, 0)), "pine_stump": pine_stump(at=(41.0, t, 0)),
    })
    b = ROW_BUSHES
    objs.update({
        "gorse": gorse(at=(2.5, b, 0)), "gorse_2": gorse(seed=21, at=(4.6, b, 0)),
        "berry_low": berry_bush_low(at=(8.0, b, 0)), "berry_low_picked": berry_bush_low(at=(10.2, b, 0), picked=True),
        "berry_tall": berry_bush_tall(at=(13.4, b, 0)),
        "berry_tall_picked": berry_bush_tall(at=(15.2, b, 0), picked=True),
    })
    for i, (x, y) in enumerate(((18.5, b - 0.4), (19.6, b + 0.3), (20.4, b - 0.5), (21.3, b + 0.2))):
        objs["tussock_%d" % i] = tussock(seed=i + 1, at=(x, y, 0))
    for i, (x, y) in enumerate(((24.0, b), (24.8, b + 0.5), (25.5, b - 0.3))):
        objs["dock_%d" % i] = dock(seed=i + 1, at=(x, y, 0))
    objs.update({
        "flax": flax(at=(28.0, b, 0)), "flax_picked": flax(at=(29.4, b, 0), picked=True),
        "herb": herb(at=(31.0, b, 0)), "herb_picked": herb(at=(32.2, b, 0), picked=True),
        "mushrooms": mushrooms(at=(33.8, b, 0)), "mushrooms_picked": mushrooms(at=(35.0, b, 0), picked=True),
    })
    s_ = ROW_STONE
    objs.update({
        "boulders": boulders(at=(3.5, s_, 0)),
        "boulders_small": boulders(seed=17, at=(7.5, s_, 0), size=0.6),
        "boulders_large": boulders(seed=3, at=(11.5, s_, 0), size=1.4),
        "cairn": cairn(at=(15.5, s_, 0)),
    })
    r = ROW_BOUNDS
    for i, x in enumerate((1, 2, 3, 4)):
        objs["buck_%d" % i] = buck_fence(seed=i + 1, at=(x, r, 0), end_post=(i == 3))
    for i, x in enumerate((7, 8, 9)):
        objs["hurdle_%d" % i] = hurdle(seed=i + 1, at=(x, r, 0), end_post=(i == 2))
    objs["gate"] = field_gate(at=(10, r, 0))          # in the hurdles' line
    for i, x in enumerate((12, 13, 15, 16)):           # a gap at 14: a fallen stretch of wall
        objs["wall_%d" % i] = wall_span(seed=i + 1, at=(x, r, 0))
    objs["stile"] = wall_stile(at=(17, r, 0))
    p_ = ROW_PLACES
    objs.update({
        "standing_stone": standing_stone(at=(3.0, p_, 0)),
        "standing_stone_2": standing_stone(seed=5, at=(4.6, p_ + 0.6, 0)),
        "standing_stone_fallen": standing_stone_fallen(at=(7.5, p_, 0)),
        "grave": grave(at=(11.0, p_, 0)), "grave_2": grave(seed=3, at=(12.2, p_, 0)),
        "grave_old": grave(seed=4, at=(13.4, p_, 0), old=True),
        "hay_stook": hay_stook(at=(17.0, p_, 0)), "hay_stook_2": hay_stook(seed=2, at=(18.5, p_ + 0.4, 0)),
        "farm_cart": farm_cart(at=(22.0, p_, 0), rot=0.4),
    })
    for i, x in enumerate(range(19, 25)):
        objs["hedge_%d" % i] = hedge(seed=i + 1, at=(x, r, 0))
    objs.update(kit("mining").row(ROW_MINING))
    W_ = kit("wilds")
    objs.update(W_.row(ROW_WILDS, ROW_WAYS))
    objs["stepping_stones"] = W_.stepping_stones(at=(4.0, 2.2, 0), water=WATER_Z)
    return objs


def kit(name):
    """One of the modules built on this one's helpers (mining, wilds), loaded fresh once they exist."""
    import importlib
    sys.modules.pop(name, None)
    sys.modules.pop("basin", None)
    if HERE not in sys.path:
        sys.path.insert(0, HERE)
    return importlib.import_module(name)


def rig():
    """The game's sun and camera, the player by the trees, Material Preview with scene lights."""
    A.preview_rig(focus=(22.0, 12.5, 0.8), distance=44.0, pitch=40, yaw=-20, ground=0, human=True,
                  human_at=(7.0, ROW_TREES - 1.2))
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


# ---- export -----------------------------------------------------------------------------------

def span(fn, **kw):
    """A one-tile span built along X from 0 to 1, moved so its tile's middle is the origin."""
    def build_span():
        ob = fn(**kw)
        ob.data.transform(Matrix.Translation((-0.5, 0, 0)))
        return ob
    return build_span


# Name in the game (Resources/Nature/<Name>: a piece's "model", objects.json "depletedModel") to builder.
ASSETS = {
    "Alder": alder, "AlderStool": stool_cut,
    "Ash": ash, "AshStump": ash_stump,
    "Hawthorn": hawthorn, "HawthornStump": hawthorn_stump,
    "Pollard": pollard, "PollardStump": pollard_stump,
    "Snag": snag,
    "Gorse": gorse,
    "BerryBushLow": berry_bush_low, "BerryBushLowPicked": lambda: berry_bush_low(picked=True),
    "BerryBushTall": berry_bush_tall, "BerryBushTallPicked": lambda: berry_bush_tall(picked=True),
    "Tussock": tussock, "Dock": dock, "Reeds": reeds,
    "Boulders": boulders, "Cairn": cairn,
    "BuckFence": span(buck_fence), "BuckFenceEnd": span(buck_fence, end_post=True),
    "Hurdle": span(hurdle), "HurdleEnd": span(hurdle, end_post=True),
    "WallSpan": span(wall_span), "Hedge": span(hedge),
    "Elm": elm, "ElmStump": elm_stump,
    "Flax": flax, "FlaxPicked": lambda: flax(picked=True),
    "Herb": herb, "HerbPicked": lambda: herb(picked=True),
    "Mushrooms": mushrooms, "MushroomsPicked": lambda: mushrooms(picked=True),
    "FieldGate": span(field_gate), "WallStile": span(wall_stile),
    "Pine": pine, "PineStump": pine_stump,
    "LilyPads": lily_pads, "MooringPost": mooring_post, "JettySpan": jetty_span, "Punt": punt,
    "StandingStone": standing_stone, "StandingStoneFallen": standing_stone_fallen,
    "Grave": grave, "GraveOld": lambda: grave(old=True),
    "HayStook": hay_stook, "FarmCart": farm_cart,
}


def export_all():
    """Every asset to its own FBX (materials named after palette colours and textures, which the
    client's NatureImporter turns into the game's materials), and the kit's textures beside the
    world's."""
    import shutil
    out = {}
    for name, builder in list(ASSETS.items()) + list(kit("mining").ASSETS.items()) + list(kit("wilds").ASSETS.items()):
        A.reset()
        ob = builder()
        ob.name = ob.data.name = name
        A.export([ob], "Resources/Nature/%s.fbx" % name)
        out[name] = A.report([ob])["triangles"]
    for f in sorted(os.listdir(TEXTURES)):
        if f.endswith(".png") and not f.endswith(".preview.png"):
            shutil.copyfile(os.path.join(TEXTURES, f), os.path.join(A.TEXTURES_DIR, f))
    return out


if globals().get("EXPORT"):
    result = export_all()
elif __name__ != "basin":           # imported by another kit (import basin): its helpers only
    objs = build()
    rig()
    result = {k: A.report([o])["triangles"] for k, o in objs.items() if not k[-1].isdigit() or k.endswith("_0")}
