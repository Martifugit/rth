"""The paint lab: the reference kit's pieces (buildings/venaria.py) side by side, bland and painted,
to try ways of painting walls and roofs that look painted rather than flat.

Brought over from the kit: the generic wall tile and the roof pieces (eave, slope, ridge, and their
verges). Each specimen is a small block: bland on the left (today's tinted repeating patterns),
painted on the right (lab/paint_textures.py: several palette colours, painted for the whole piece,
roof courses continuing from eave to ridge). Thatch in front, slate behind.

Run in Blender (MCP): runpy.run_path(".../lab/paint_lab.py", run_name="__main__").
Re-run lab/paint_textures.py first after changing the painting."""
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(HERE, ".."))
import rth_art as A  # noqa: E402
import venaria as V  # noqa: E402

TEXTURES = os.path.join(HERE, "textures")


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


def variant(kit, name, slot, image, fit):
    """A copy of a kit piece whose material `slot` (a palette name) is painted with `image`, its
    faces in that slot mapped by fit(co) -> (u, v): the texture covers the whole piece."""
    src = kit[name]
    me = src.data.copy()
    me.name = f"{name}_{image}"
    idx = [m.name for m in me.materials].index(slot)
    me.materials[idx] = painted(f"Mat_{image}", image)
    uv = me.uv_layers[0]
    for p in me.polygons:
        if p.material_index != idx:
            continue
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = fit(co, p.normal)
    o = bpy.data.objects.new(me.name, me)
    A.collection("Kit").objects.link(o)
    o.location = (0, -40, 0)
    return o


def wall_fit(co, n):
    if abs(n.x) > 0.7:                                             # the tile's ends: barely seen
        return (co.y, (co.z - V.LO) / (V.HI - V.LO))
    return (co.x, (co.z - V.LO) / (V.HI - V.LO))


def roof_fit(run0, run1, width=1.0):
    def fit(co, n):
        return (co.x / width if width != 1.0 else co.x, (co.y - run0) / (run1 - run0))
    return fit


def painted_kit(kit, roof):
    """The kit with the generic wall and the roof pieces painted; roof: "Thatch" or "Slate"."""
    k = dict(kit)
    k["Wall"] = variant(kit, "Wall", "Plaster", "PaintWall", wall_fit)
    eave_fit = roof_fit(-V.OVERHANG, 1.0)
    k["RoofEave"] = variant(kit, "RoofEave", "Thatch", f"Paint{roof}Eave", eave_fit)
    k["VergeEave"] = variant(kit, "VergeEave", "Thatch", f"Paint{roof}Eave", eave_fit)
    # The block is four tiles deep: its one slope tile is also the top one, so it carries the ridge.
    k["RoofSlope"] = variant(kit, "RoofSlope", "Thatch", f"Paint{roof}Ridge", roof_fit(0.0, 1.0))
    k["VergeSlope"] = variant(kit, "VergeSlope", "Thatch", f"Paint{roof}Ridge", roof_fit(0.0, 1.0))
    return k


def specimen(kit, name, ox, oy, roof=None):
    coll = A.collection(name)
    V.volume(kit, coll, ox, oy, 3, 4, {("S", 0): ["Wall"] * 3, ("N", 0): ["Wall"] * 3,
                                       ("W", 0): ["Wall"] * 4, ("E", 0): ["Wall"] * 4}, roof=roof)


def build():
    A.reset()
    kit = V.build_pieces()
    for o in kit.values():
        o.location = (0, -40, 0)                                  # out of sight
    slate = {"Thatch": "Slate", "Timber": "DarkRock"}
    specimen(kit, "BlandThatch", 0, 0)
    specimen(painted_kit(kit, "Thatch"), "PaintedThatch", 5, 0)
    specimen(kit, "BlandSlate", 0, 7, roof=slate)
    specimen(painted_kit(kit, "Slate"), "PaintedSlate", 5, 7, roof={"Timber": "DarkRock", "Thatch": "Slate"})


if __name__ == "__main__":
    build()
    cam = A.preview_rig(focus=(4.0, 4.5, 1.8), distance=19, pitch=35, yaw=-25, ground=14, human_at=(4.0, -1.2))
