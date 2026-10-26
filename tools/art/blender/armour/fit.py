"""Armour on the player: a scene to dress the body in, measurements of the body to fit armour
to, binding armour to the skeleton, and posing the player for a look.

Armour is modelled on the T-posed body (tools/character-models/README.md: the contract) in its
own scene, "Armour", so the rest of HumanMale.blend is never touched. A piece is a mesh fitted
around the body's own cross-sections (`rings`), bound to the skeleton by copying the weights of
the body underneath (`bind`), and it names the kit parts it hides."""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

SCENE = "Armour"
BODY = "ArmourBody"            # collection holding the imported player
PARTS = ("body_head", "body_torso", "body_arms", "body_hands", "body_legs", "body_feet")


def scene():
    """Switch the window to the Armour scene (made on first use)."""
    s = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
    bpy.context.window.scene = s
    s.unit_settings.system = "METRIC"
    return s


def coll(name):
    s = bpy.context.scene
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
    if c.name not in s.collection.children:
        s.collection.children.link(c)
    return c


def clear(name):
    """Remove every object in a collection (and meshes nothing else uses)."""
    c = bpy.data.collections.get(name)
    if c is None:
        return
    for o in list(c.objects):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)


def body():
    """The player, imported once into this scene; its rig is ArmourRig at the origin."""
    rig = bpy.data.objects.get("ArmourRig")
    if rig is None:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=A.HUMAN_FBX)
        c = coll(BODY)
        for o in set(bpy.data.objects) - before:
            for uc in list(o.users_collection):
                uc.objects.unlink(o)
            c.objects.link(o)
            if o.type == "ARMATURE":
                o.name = "ArmourRig"
                rig = o
            elif o.type == "MESH":
                o.name = "Armour_" + o.name.split(".")[0]
    rig.location = (0, 0, 0)
    for o in rig.children:
        for m in o.data.materials:
            colour = A.PLAYER_PREVIEW.get(m.name.split(".")[0])
            if colour is None:
                continue
            m.use_nodes = True
            bsdf = m.node_tree.nodes["Principled BSDF"]
            bsdf.inputs["Base Color"].default_value = A.rgba(colour)
            bsdf.inputs["Roughness"].default_value = 1.0
            m.diffuse_color = A.rgba(colour)
    return rig


def part(name):
    return bpy.data.objects.get("Armour_" + name)


def body_bvh(parts=PARTS):
    """A BVH of the T-posed body parts in rig space."""
    verts, polys = [], []
    for p in parts:
        o = part(p)
        if o is None:
            continue
        to_rig = o.parent.matrix_world.inverted() @ o.matrix_world
        base = len(verts)
        verts += [to_rig @ v.co for v in o.data.vertices]
        polys += [[base + i for i in poly.vertices] for poly in o.data.polygons]
    return BVHTree.FromPolygons(verts, polys)


def frame(axis_from, axis_to, up=(0, 0, 1)):
    """Unit vectors (along, u, s) for rings around a line: u is `up` made square to the line."""
    a, b = Vector(axis_from), Vector(axis_to)
    t = (b - a).normalized()
    u = Vector(up)
    u = (u - t * u.dot(t)).normalized()
    return t, u, t.cross(u)


def rings(bvh, axis_from, axis_to, stations, sides=8, up=(0, 0, 1), reach=0.6, inside=False):
    """The body's cross-sections along a line: for each station t (0 at axis_from, 1 at
    axis_to), the distance from the line out to the body's surface in `sides` directions,
    starting at `up` and going round. Returns [(centre, [radius...])], u, s. A miss is 0.
    inside: cast from the line outwards (for a limb, whose line runs inside it), else from
    `reach` away inwards (the outermost surface)."""
    a, b = Vector(axis_from), Vector(axis_to)
    _, u, s = frame(a, b, up)
    out = []
    for t in stations:
        c = a.lerp(b, t)
        rs = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            d = u * math.cos(ang) + s * math.sin(ang)
            if inside:
                hit = bvh.ray_cast(c, d, reach)
                rs.append(0.0 if hit[0] is None else hit[3])
            else:
                hit = bvh.ray_cast(c + d * reach, -d, reach)
                rs.append(0.0 if hit[0] is None else reach - hit[3])
        out.append((c, rs))
    return out, u, s


def bone(name, at="head"):
    """A bone's head (or tail) in rig space, from the reference body's rig."""
    b = bpy.data.objects["ArmourRig"].data.bones[name]
    return Vector(b.head_local if at == "head" else b.tail_local)


def inflate(bm, parts, d, mat, keep=None):
    """Add to bm a copy of kit parts pushed out along their normals by d (a layer that fits the
    body exactly: mail, cloth, a boot), in rig space, all faces in material `mat`. keep(centre)
    -> bool chooses which faces to take (a short sleeve, a toe cap)."""
    for p in parts:
        o = part(p)
        if o is None:
            continue
        b2 = bmesh.new()
        b2.from_mesh(o.data)
        b2.transform(o.parent.matrix_world.inverted() @ o.matrix_world)
        bmesh.ops.remove_doubles(b2, verts=list(b2.verts), dist=1e-4)
        if keep is not None:
            bmesh.ops.delete(b2, geom=[f for f in b2.faces if not keep(f.calc_center_median())], context="FACES")
        bmesh.ops.recalc_face_normals(b2, faces=list(b2.faces))
        b2.normal_update()
        for v in b2.verts:
            v.co += v.normal * d
        for f in b2.faces:
            f.material_index = mat
        tmp = bpy.data.meshes.new("_inflate")
        b2.to_mesh(tmp)
        b2.free()
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)


def ribbon(bm, points, normals, width, mat, onto, lift=0.006, across=None):
    """A strap laid over a surface: each point is cast along -normal onto `onto` (a BVHTree) and
    lifted; the strap is `width` wide, across `across` (else square to the path and normal).
    Points that miss are skipped."""
    hits = []
    for p, n in zip(points, normals):
        n = Vector(n).normalized()
        hit = onto.ray_cast(Vector(p) + n * 0.5, -n, 1.0)
        if hit[0] is not None:
            hits.append((hit[0] + hit[1] * lift, hit[1]))
    rows = []
    for i, (h, n) in enumerate(hits):
        a = hits[max(i - 1, 0)][0]
        b = hits[min(i + 1, len(hits) - 1)][0]
        side = Vector(across) if across else (b - a).cross(n).normalized()
        rows.append((bm.verts.new(h - side * width / 2), bm.verts.new(h + side * width / 2)))
    for i, ((a0, a1), (b0, b1)) in enumerate(zip(rows, rows[1:])):
        f = bm.faces.new((a0, a1, b1, b0))
        f.material_index = mat
        f.normal_update()
        if f.normal.dot(hits[i][1]) < 0:
            f.normal_flip()
    return rows


def shell(bm, sections, u, s, mats=0, cap=(True, True)):
    """A tube through rings: sections are (centre, [radius per direction]), directions going
    round from u towards s (as `rings` measures them). Returns the rings of verts and faces."""
    rings_v = []
    for c, rs in sections:
        n = len(rs)
        rings_v.append([bm.verts.new(Vector(c) + (u * math.cos(2 * math.pi * k / n)
                                                   + s * math.sin(2 * math.pi * k / n)) * r)
                        for k, r in enumerate(rs)])
    faces = []
    for i in range(len(rings_v) - 1):
        r0, r1 = rings_v[i], rings_v[i + 1]
        m = mats if isinstance(mats, int) else mats[i]
        n = len(r0)
        for k in range(n):
            f = bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
            f.material_index = m
            faces.append(f)
    for idx, flag in ((0, cap[0]), (len(rings_v) - 1, cap[1])):
        if flag:
            f = bm.faces.new(rings_v[idx] if idx else list(reversed(rings_v[idx])))
            f.material_index = mats if isinstance(mats, int) else mats[min(idx, len(mats) - 1)]
            faces.append(f)
    return rings_v, faces


def bind(ob, rig, parts=PARTS):
    """Skin a piece (modelled on the T-pose, in rig space) to the rig: each vertex copies the
    weights of the nearest body surface."""
    me = bpy.data.meshes.new("_bindsrc")
    verts, polys, weights = [], [], []
    for p in parts:
        o = part(p)
        if o is None:
            continue
        to_rig = o.parent.matrix_world.inverted() @ o.matrix_world
        base = len(verts)
        names = {g.index: g.name for g in o.vertex_groups}
        for v in o.data.vertices:
            verts.append(tuple(to_rig @ v.co))
            weights.append({names[g.group]: g.weight for g in v.groups if g.group in names})
        polys += [[base + i for i in poly.vertices] for poly in o.data.polygons]
    me.from_pydata(verts, [], polys)
    src = bpy.data.objects.new("_bindsrc", me)
    bpy.context.scene.collection.objects.link(src)
    # Both in rig space at the origin while the weights are copied; parented to the rig after.
    for i, w in enumerate(weights):
        for name, wt in w.items():
            (src.vertex_groups.get(name) or src.vertex_groups.new(name=name)).add([i], wt, "REPLACE")
    ob.parent = None
    ob.matrix_world = Matrix.Identity(4)
    for g in src.vertex_groups:
        if ob.vertex_groups.get(g.name) is None:
            ob.vertex_groups.new(name=g.name)
    dt = ob.modifiers.new("bind", "DATA_TRANSFER")
    dt.object = src
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    dt.layers_vgroup_select_src = "ALL"
    dt.layers_vgroup_select_dst = "NAME"
    bpy.context.view_layer.update()
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=dt.name)
    bpy.data.objects.remove(src, do_unlink=True)
    bpy.data.meshes.remove(me)
    ob.parent = rig
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = Matrix.Identity(4)
    ob.modifiers.new("Armature", "ARMATURE").object = rig
    return ob


def pose(rig, arms_down=72.0):
    """A relaxed stance to look at armour in: the upper arms lowered by arms_down degrees."""
    rig.data.pose_position = "POSE"
    for side, sign in (("L", -1), ("R", 1)):
        pb = rig.pose.bones.get("upper_arm." + side)
        if pb is None:
            continue
        axis = (pb.bone.matrix_local.to_3x3().inverted() @ Vector((0, 1, 0))).normalized()
        pb.rotation_mode = "AXIS_ANGLE"
        pb.rotation_axis_angle = (math.radians(arms_down) * sign, *axis)
    bpy.context.view_layer.update()


def look(focus=(0, 0, 1.0), distance=4.0, pitch=10.0, yaw=-25.0, lens=50, size=(900, 900)):
    """Camera, sun and sky for this scene. Returns the camera."""
    s = bpy.context.scene
    c = coll("ArmourLook")
    cam = bpy.data.objects.get("ArmourCam")
    if cam is None:
        cam = bpy.data.objects.new("ArmourCam", bpy.data.cameras.new("ArmourCam"))
        c.objects.link(cam)
    cam.data.lens = lens
    A.aim_camera(cam, focus, distance, pitch, yaw)
    s.camera = cam
    sun = bpy.data.objects.get("ArmourSun")
    if sun is None:
        ld = bpy.data.lights.new("ArmourSun", "SUN")
        ld.energy = 3.5
        ld.color = (1.0, 0.96, 0.88)
        sun = bpy.data.objects.new("ArmourSun", ld)
        c.objects.link(sun)
    sun.rotation_euler = Vector((0.45, -0.75, -0.55)).normalized().to_track_quat("-Z", "Y").to_euler()
    world = s.world or bpy.data.worlds.new("ArmourWorld")
    s.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.55, 0.62, 0.70, 1)
    bg.inputs["Strength"].default_value = 0.7
    s.render.engine = "BLENDER_EEVEE"
    s.render.resolution_x, s.render.resolution_y = size
    s.view_settings.view_transform = "Standard"
    return cam
