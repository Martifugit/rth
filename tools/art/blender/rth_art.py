"""Shared helpers for building R'th props in Blender (see tools/art/blender/README.md).

Run inside Blender 5.x. A prop script does:

    import rth_art as A
    A.reset()
    ... build meshes with A.mesh_from(...) and A.mat("Stone") ...
    A.preview_rig()
    A.report(objects)

1 Blender unit = 1 tile. Blender is Z-up; the FBX export turns it into Unity's Y-up.
"""
import json
import math
import os

import bmesh
import bpy
from mathutils import Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
PALETTE_PATH = os.path.join(REPO, "client", "Assets", "Resources", "Art", "palette.json")
HUMAN_FBX = os.path.join(REPO, "client", "Assets", "Resources", "Characters", "HumanMale.fbx")

_palette = None


def palette():
    global _palette
    if _palette is None:
        with open(PALETTE_PATH, encoding="utf-8") as f:
            _palette = {c["name"]: c["hex"] for c in json.load(f)["colours"]}
    return _palette


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgba(name):
    h = palette()[name].lstrip("#")
    return tuple(_srgb_to_linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def mat(name):
    """A matte material named after (and coloured as) a palette colour."""
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba(name)
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Specular IOR Level"].default_value = 0.0
    m.diffuse_color = rgba(name)
    return m


def reset():
    """Empty the scene (objects, meshes, materials) for a clean rebuild."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights,
                 bpy.data.armatures, bpy.data.actions):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def mesh_from(name, bm, materials, coll="Prop", smooth_angle=None):
    """Turn a bmesh into an object. Faces carry material_index into `materials` (palette names).
    smooth_angle (degrees): shade smooth, keeping edges sharper than this split."""
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for n in materials:
        me.materials.append(mat(n))
    ob = bpy.data.objects.new(name, me)
    collection(coll).objects.link(ob)
    if smooth_angle is not None:
        for p in me.polygons:
            p.use_smooth = True
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        with bpy.context.temp_override(object=ob, active_object=ob, selected_editable_objects=[ob]):
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth_angle))
    return ob


# ---- geometry helpers (all return the new verts/faces in the given bmesh) ----

def prism(bm, sides, r_bottom, r_top, z0, z1, center=(0, 0), rot=0.0, mat_index=0,
          cap_top=True, cap_bottom=False, jitter=None):
    """A vertical n-sided prism or frustum. jitter: callable(i, ring) -> (dx, dy, dz) for
    hand-made irregularity."""
    cx, cy = center
    rings = []
    for ring, (r, z) in enumerate(((r_bottom, z0), (r_top, z1))):
        vs = []
        for i in range(sides):
            a = rot + 2 * math.pi * i / sides
            d = jitter(i, ring) if jitter else (0, 0, 0)
            vs.append(bm.verts.new((cx + r * math.cos(a) + d[0], cy + r * math.sin(a) + d[1], z + d[2])))
        rings.append(vs)
    faces = []
    b, t = rings
    for i in range(sides):
        j = (i + 1) % sides
        faces.append(bm.faces.new((b[i], b[j], t[j], t[i])))
    if cap_top:
        faces.append(bm.faces.new(t))
    if cap_bottom:
        faces.append(bm.faces.new(list(reversed(b))))
    for f in faces:
        f.material_index = mat_index
    return rings, faces


def box(bm, lo, hi, mat_index=0, skip=()):
    """An axis-aligned box from corner lo to hi. skip: faces to leave out ('-z', '+x', ...)."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    v = [bm.verts.new(p) for p in (
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    quads = {
        "-z": (0, 3, 2, 1), "+z": (4, 5, 6, 7),
        "-y": (0, 1, 5, 4), "+y": (2, 3, 7, 6),
        "-x": (3, 0, 4, 7), "+x": (1, 2, 6, 5),
    }
    faces = []
    for k, q in quads.items():
        if k in skip:
            continue
        f = bm.faces.new([v[i] for i in q])
        f.material_index = mat_index
        faces.append(f)
    return v, faces


def loft(bm, sections, sides=6, ref=(0, 0, 1), mats=0, roll=0.0):
    """An organic tube through a list of sections, the main tool for creatures.
    Each section is (centre, half_width, half_height); a section with zero size is a point (a tip).
    Rings face along the path; `ref` is the 'up' they keep (use another axis for vertical tubes).
    mats: one material index, or one per segment. Open ends are capped flat."""
    ref = Vector(ref).normalized()
    pts = [Vector(s[0]) for s in sections]
    rings = []
    for i, (c, w, h) in enumerate(sections):
        c = Vector(c)
        if w == 0 and h == 0:
            rings.append([bm.verts.new(c)])
            continue
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        t = (b - a).normalized()
        up = (ref - t * ref.dot(t)).normalized()
        side = t.cross(up)
        ring = []
        for k in range(sides):
            ang = roll + 2 * math.pi * k / sides
            ring.append(bm.verts.new(c + side * (w * math.cos(ang)) + up * (h * math.sin(ang))))
        rings.append(ring)
    faces = []
    for i in range(len(rings) - 1):
        m = mats if isinstance(mats, int) else mats[i]
        r0, r1 = rings[i], rings[i + 1]
        seg = []
        if len(r0) == 1 and len(r1) == 1:
            continue
        for k in range(sides):
            n = (k + 1) % sides
            if len(r0) == 1:
                seg.append(bm.faces.new((r0[0], r1[k], r1[n])))
            elif len(r1) == 1:
                seg.append(bm.faces.new((r0[k], r0[n], r1[0])))
            else:
                seg.append(bm.faces.new((r0[k], r0[n], r1[n], r1[k])))
        for f in seg:
            f.material_index = m
        faces += seg
    for ring, m in ((rings[0], mats if isinstance(mats, int) else mats[0]),
                    (rings[-1], mats if isinstance(mats, int) else mats[-1])):
        if len(ring) > 2:
            f = bm.faces.new(ring)
            f.material_index = m
            faces.append(f)
    return rings, faces


def spike(bm, base, radius, tip, sides=4, mat=0, roll=0.0):
    """A pyramid from a buried base to a tip: legs, ears, claws, horns. The base has no cap; sink it
    inside the part it grows from so the joint never shows."""
    base, tip = Vector(base), Vector(tip)
    t = (tip - base).normalized()
    ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((0, 1, 0))
    up = (ref - t * ref.dot(t)).normalized()
    side = t.cross(up)
    ring = [bm.verts.new(base + (side * math.cos(roll + 2 * math.pi * k / sides)
                                 + up * math.sin(roll + 2 * math.pi * k / sides)) * radius)
            for k in range(sides)]
    tv = bm.verts.new(tip)
    faces = []
    for k in range(sides):
        f = bm.faces.new((ring[k], ring[(k + 1) % sides], tv))
        f.material_index = mat
        faces.append(f)
    return faces


def paint(bm, point, mat, facing=None, size=1.0):
    """Colour a triangle at the face nearest `point` (eyes, nostrils, a mark): detail painted onto
    the surface instead of stuck on it. facing: only faces whose normal points this way.
    size < 1 colours a smaller triangle inside that face (scaled toward its centre), so the mark
    is not as big as the face around it. Triangulates the mesh first."""
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    point = Vector(point)
    best, best_d = None, 1e9
    for f in bm.faces:
        if facing is not None and f.normal.dot(Vector(facing)) < 0.3:
            continue
        d = (f.calc_center_median() - point).length
        if d < best_d:
            best, best_d = f, d
    if best is None:
        return None
    if size >= 1.0:
        best.material_index = mat
        return best
    outer = list(best.verts)
    c = best.calc_center_median()
    old_mat = best.material_index
    inner = [bm.verts.new(c.lerp(v.co, size)) for v in outer]
    bm.faces.remove(best)
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((outer[i], outer[j], inner[j], inner[i])).material_index = old_mat
    f = bm.faces.new(inner)
    f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return f


def decal(bm, outline, toward, mat=0, lift=0.0015):
    """A flat mark laid onto the surface already in `bm` (eyes on a face, a sigil on a shield):
    each outline point is cast along -toward onto the surface and lifted a hair off it, so the
    mark follows the surface instead of floating. Returns the new face, facing `toward`."""
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromBMesh(bm)
    toward = Vector(toward).normalized()
    verts = []
    for p in outline:
        hit, normal, _, _ = bvh.ray_cast(Vector(p) + toward * 0.5, -toward)
        verts.append(bm.verts.new((hit + normal * lift) if hit is not None else Vector(p)))
    f = bm.faces.new(verts)
    f.normal_update()
    if f.normal.dot(toward) < 0:
        f.normal_flip()
    f.material_index = mat
    return f


def mirror_y(points):
    return [(x, -y, z) for (x, y, z) in points]


def finish(bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    return bm


# ---- preview ----

def preview_rig(focus=(0.5, 0.5, 0.6), distance=9.0, pitch=40.0, yaw=-30.0, ground=6, human=True,
                ground_colour="Grass", human_at=(-0.5, 0.5)):
    """Ground of tiles, the game's sun, a camera at the game's pitch, and the player for scale.
    Everything goes in the 'Preview' collection, which the export skips."""
    coll = collection("Preview")
    scene = bpy.context.scene

    # Ground: one quad per tile, two alternating shades of the ground colour so tiles read.
    bm = bmesh.new()
    for x in range(-ground, ground):
        for y in range(-ground, ground):
            vs = [bm.verts.new(p) for p in ((x, y, 0), (x + 1, y, 0), (x + 1, y + 1, 0), (x, y + 1, 0))]
            f = bm.faces.new(vs)
            f.material_index = (x + y) % 2
    me = bpy.data.meshes.new("PreviewGround")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(ground_colour))
    alt = bpy.data.materials.new(ground_colour + "_alt")
    alt.use_nodes = True
    c = rgba(ground_colour)
    alt.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (c[0] * 0.9, c[1] * 0.9, c[2] * 0.9, 1)
    alt.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    me.materials.append(alt)
    g = bpy.data.objects.new("PreviewGround", me)
    coll.objects.link(g)

    # The game's sun: Unity Euler(50, -35, 0). Unity (x, y, z) is Blender (x, z, y).
    p, yw = math.radians(50), math.radians(-35)
    fwd_unity = Vector((math.sin(yw) * math.cos(p), -math.sin(p), math.cos(yw) * math.cos(p)))
    fwd = Vector((fwd_unity.x, fwd_unity.z, fwd_unity.y))
    ld = bpy.data.lights.new("PreviewSun", "SUN")
    ld.energy = 3.5
    ld.color = (1.0, 0.96, 0.88)
    ld.angle = math.radians(3)
    sun = bpy.data.objects.new("PreviewSun", ld)
    sun.rotation_euler = fwd.to_track_quat("-Z", "Y").to_euler()
    coll.objects.link(sun)

    # Camera looking at focus from the given pitch (down from horizontal) and yaw.
    cd = bpy.data.cameras.new("PreviewCam")
    cd.lens = 50
    cam = bpy.data.objects.new("PreviewCam", cd)
    coll.objects.link(cam)
    aim_camera(cam, focus, distance, pitch, yaw)
    scene.camera = cam

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.55, 0.62, 0.70, 1)
    bg.inputs["Strength"].default_value = 0.6

    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    scene.view_settings.view_transform = "Standard"

    if human and os.path.exists(HUMAN_FBX):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=HUMAN_FBX)
        for o in set(bpy.data.objects) - before:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            coll.objects.link(o)
            if o.parent is None:
                o.location = (human_at[0], human_at[1], 0)
    return cam


def look_rig(colours=None, front_light=True):
    """A sun and a camera, without ground or a reference figure: for looking at a character.
    front_light: light from the front-left above (to judge a face's shape; the game's sun is
    behind a character facing +Y, leaving the face in shade). colours: channel material ->
    palette colour, to preview the game's colouring."""
    scene = bpy.context.scene
    coll = collection("Preview")
    ld = bpy.data.lights.new("LookSun", "SUN")
    ld.energy = 3.5
    sun = bpy.data.objects.new("LookSun", ld)
    coll.objects.link(sun)
    p, yw = math.radians(50), math.radians(-35)
    fu = Vector((math.sin(yw) * math.cos(p), -math.sin(p), math.cos(yw) * math.cos(p)))
    travel = Vector((fu.x, fu.z, fu.y))
    if front_light:
        travel = Vector((0.45, -0.75, -0.55)).normalized()
    sun.rotation_euler = travel.to_track_quat("-Z", "Y").to_euler()
    cd = bpy.data.cameras.new("LookCam")
    cd.lens = 85
    cam = bpy.data.objects.new("LookCam", cd)
    coll.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 700
    scene.view_settings.view_transform = "Standard"
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.62, 0.70, 1)
    for channel, name in (colours or {}).items():
        m = bpy.data.materials.get(channel)
        if m is None:
            continue
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = rgba(name)
            bsdf.inputs["Roughness"].default_value = 1.0
    return cam


PLAYER_PREVIEW = {"skin": "Skin2", "hair": "HairBrown", "primary": "Indigo", "secondary": "Umber",
                  "leather": "Hide", "eyes": "Sable"}


def aim_camera(cam, focus, distance, pitch, yaw):
    p, y = math.radians(pitch), math.radians(yaw)
    f = Vector(focus)
    offset = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p))) * distance
    cam.location = f + offset
    cam.rotation_euler = (f - cam.location).to_track_quat("-Z", "Y").to_euler()


def render(path, cam=None, focus=None, distance=None, pitch=None, yaw=None):
    scene = bpy.context.scene
    cam = cam or scene.camera
    if focus is not None:
        aim_camera(cam, focus, distance, pitch, yaw)
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def export(objs, relative_path):
    """Write the prop's meshes as an FBX under the client (e.g. "Resources/Creatures/FenRat.fbx"),
    with the axis settings the character art uses: Blender -Y (the model's front) becomes Unity +Z,
    1 unit stays 1 tile. Materials keep their palette names, which the client colours by."""
    path = os.path.join(REPO, "client", "Assets", *relative_path.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE", use_mesh_modifiers=True, bake_space_transform=True,
        bake_anim=False,
    )
    return path


def report(objs):
    """Triangles and colours of the prop, for the budget in art-budgets.json."""
    tris = 0
    colours = set()
    for o in objs:
        me = o.data
        me.calc_loop_triangles()
        tris += len(me.loop_triangles)
        used = {p.material_index for p in me.polygons}
        colours |= {me.materials[i].name for i in used}
    return {"triangles": tris, "colours": sorted(colours)}
