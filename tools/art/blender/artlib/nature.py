"""Helpers shared by the natural families (trees, plants, rocks, fences, dressing): materials with the
leaf textures, make(), blobs, branches, leafy clumps, rods, slabs."""

import bmesh
import bpy
import math
import os

from mathutils import Vector

import rth_art as A


TEXTURES = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "textures", "nature")   # textures/nature.py paints them


def leaf_mat(name):
    """A leaf texture from textures/nature (Foliage, Willow), cut out at alpha 0.5, both sides
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


WIND = Vector((1.0, 0.15, 0.0)).normalized()   # the weather comes off the spine, from the west


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


def flat_chip(bm, c, s, rnd, mat, h=0.6):
    c = Vector(c)
    pts = [c + Vector((s * math.cos(a), s * math.sin(a) * rnd.uniform(0.6, 1.0), -0.02))
           for a in (rnd.uniform(0, 1.2), rnd.uniform(2.0, 3.2), rnd.uniform(4.0, 5.4))]
    pts.append(c + Vector((0, 0, s * h)))
    for f in A.hull(bm, pts):
        f.material_index = mat
