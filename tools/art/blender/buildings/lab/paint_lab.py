"""The paint lab: one building shell from the reference kit (buildings/venaria.py) shown bland and
in three painted themes, to find a way of painting walls and roofs that comes closer to the
references (images/rs3-*-exterior: one shell, many material themes, clean textures, crisp trims).

Each theme is a set of materials over the same pieces: the wall tile's panel and the roof
pieces wear the theme's textures (lab/paint_textures.py), the timbers and corner posts take a
colour (orange timber, or stone quoins), the ridge is dark, and a dark board runs down each gable
edge. Front left: the bland reference.

Run in Blender (MCP): runpy.run_path(".../lab/paint_lab.py", run_name="__main__").
Re-run lab/paint_textures.py first after changing the painting."""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(HERE, ".."))
import rth_art as A  # noqa: E402
import venaria as V  # noqa: E402

TEXTURES = os.path.join(HERE, "textures")

# Theme -> the colours its other materials take (palette names), and its trim colour.
THEMES = {
    "Tile": {"walls": {"Timber": "Autumn", "Stone": "Granite"}, "trim": "Umber"},
    "DarkStone": {"walls": {"Timber": "CliffRock", "Stone": "DarkRock"}, "trim": "Sable"},
    "PaleStone": {"walls": {"Timber": "SaltFlat", "Stone": "Wool"}, "trim": "Indigo"},
}


def painted(name, image):
    """A material that shows a painted texture as it is (no tint), point filtered."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Specular IOR Level"].default_value = 0.0
    tex = nodes.get("Painted") or nodes.new("ShaderNodeTexImage")
    tex.name = "Painted"
    tex.image = bpy.data.images.load(os.path.join(TEXTURES, image + ".png"), check_existing=True)
    tex.image.reload()
    tex.interpolation = "Closest"
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def variant(kit, name, slot, image, fit, tag):
    """A copy of a kit piece whose material `slot` (a palette name) is painted with `image`, its
    faces in that slot mapped by fit(co, normal) -> (u, v): the texture covers the whole piece."""
    me = kit[name].data.copy()
    me.name = f"{name}_{tag}"
    idx = [m.name for m in me.materials].index(slot)
    me.materials[idx] = painted(f"Mat_{image}", image)
    uv = me.uv_layers[0]
    for p in me.polygons:
        if p.material_index == idx:
            for li in p.loop_indices:
                uv.data[li].uv = fit(me.vertices[me.loops[li].vertex_index].co, p.normal)
    o = bpy.data.objects.new(me.name, me)
    A.collection("Kit").objects.link(o)
    o.location = (0, -40, 0)
    return o


def recoloured(kit, name, swaps, tag):
    me = kit[name].data.copy()
    me.name = f"{name}_{tag}"
    for i, m in enumerate(me.materials):
        if m.name in swaps:
            me.materials[i] = A.mat(swaps[m.name])
    o = bpy.data.objects.new(me.name, me)
    A.collection("Kit").objects.link(o)
    o.location = (0, -40, 0)
    return o


def wall_fit(co, n):
    v = (co.z - V.LO) / (V.HI - V.LO)
    return (co.y if abs(n.x) > 0.7 else co.x, v)


def roof_fit(run0, run1):
    return lambda co, n: (co.x, (co.y - run0) / (run1 - run0))


def themed_kit(kit, theme):
    k = dict(kit)
    k["Wall"] = variant(kit, "Wall", "Plaster", f"{theme}Wall", wall_fit, theme)
    for name in ("Gable0", "Gable1"):                              # gables wear the wall, courses running on up
        k[name] = variant(kit, name, "Plaster", f"{theme}Wall", wall_fit, theme)
    eave = roof_fit(-V.OVERHANG, 1.0)
    for name in ("RoofEave", "VergeEave"):
        k[name] = variant(kit, name, "Thatch", f"{theme}RoofEave", eave, theme)
    for name in ("RoofSlope", "VergeSlope"):
        k[name] = variant(kit, name, "Thatch", f"{theme}RoofSlope", roof_fit(0.0, 1.0), theme)
    trim = {"Thatch": THEMES[theme]["trim"], "Timber": THEMES[theme]["trim"]}
    for name in ("RoofRidge", "VergeRidge"):
        k[name] = recoloured(kit, name, trim, theme)
    return k


def gable_trims(coll, ox, oy, length, depth, colour):
    """A dark board down each gable edge, eave to ridge, standing just above the roof."""
    half = depth // 2
    m = A.mat(colour)
    for ex in (ox - V.VERGE, ox + length + V.VERGE):
        for sign, y0 in ((1, oy), (-1, oy + depth)):
            pts = []
            for r in (-V.OVERHANG, half):
                z = V.ROOF_BASE + V.RISE * r + V.SLAB
                y = y0 + sign * r
                pts += [(ex + dx, y, z + dz) for dx in (-0.05, 0.05) for dz in (-0.16, 0.03)]
            import bmesh
            bm = bmesh.new()
            A.hull(bm, pts, 0)
            me = bpy.data.meshes.new("GableTrim")
            bm.to_mesh(me)
            bm.free()
            me.materials.append(m)
            coll.objects.link(bpy.data.objects.new("GableTrim", me))


def specimen(kit, name, ox, oy, walls=None, trim=None):
    coll = A.collection(name)
    faces = {("S", 0): ["Wall"] * 3, ("N", 0): ["Wall"] * 3, ("W", 0): ["Wall"] * 4, ("E", 0): ["Wall"] * 4}
    V.volume(kit, coll, ox, oy, 3, 4, faces, walls=[walls] if walls else None)
    if trim:
        gable_trims(coll, ox, oy, 3, 4, trim)


def build():
    A.reset()
    kit = V.build_pieces()
    for o in kit.values():
        o.location = (0, -40, 0)                                  # the kit itself, out of sight
    specimen(kit, "Bland", 0, 0)
    for i, (theme, t) in enumerate(THEMES.items()):
        x, y = [(6, 0), (0, 7), (6, 7)][i]
        specimen(themed_kit(kit, theme), theme, x, y, walls=t["walls"], trim=t["trim"])


if __name__ == "__main__":
    build()
    cam = A.preview_rig(focus=(4.5, 4.5, 1.8), distance=20, pitch=35, yaw=-25, ground=14, human_at=(4.5, -1.2))
