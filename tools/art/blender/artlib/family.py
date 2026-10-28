"""Runs a family script (docs/assets.md section 10): lays its members out beside the player, saves
its working file, or exports them.

A family is one kind in one style, at tools/art/blender/<layer>/<kind>/<style>.py. Its ASSETS map
each member's name in the game to a builder that makes it at the origin (the middle of its tile).
The path decides where it exports: assets/trees/common.py writes
client/Assets/Resources/Art/Assets/Trees/Common/<Name>.fbx, and the family owns that folder.
"""
import os

import bpy

import rth_art as A

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAP = 0.6                                       # between members in the row


def where(script):
    """(layer, kind, style) of a family script, from its path."""
    rel = os.path.relpath(os.path.abspath(script), ROOT).replace("\\", "/")
    layer, kind, style = rel[:-3].split("/")
    return layer, kind, style


def folder(script):
    """The family's export folder, under client/Assets."""
    return "Resources/Art/" + "/".join(p.title() for p in where(script))


def build(name, builder):
    ob = builder()
    ob.name = ob.data.name = name
    return ob


def export(script, assets):
    """Every member to its own FBX; models the family no longer makes are removed from its folder."""
    rel = folder(script)
    path = os.path.join(A.REPO, "client", "Assets", *rel.split("/"))
    for f in os.listdir(path) if os.path.isdir(path) else ():
        if f.endswith(".fbx") and f[:-4] not in assets:
            os.remove(os.path.join(path, f))
    out = {}
    for name, builder in assets.items():
        A.reset()
        ob = build(name, builder)
        A.export([ob], "%s/%s.fbx" % (rel, name))
        out[name] = A.report([ob])["triangles"]
    return out


def show(script, assets, save=False):
    """Every member in a row along X, the player at the start of it, in Material Preview."""
    A.reset()
    x, out = 0.0, {}
    for name, builder in assets.items():
        ob = build(name, builder)
        bpy.context.view_layer.update()
        xs = [(ob.matrix_world @ v.co).x for v in ob.data.vertices] or [0.0]
        x += -min(xs)
        ob.location.x += x
        x += max(xs) + GAP
        out[name] = A.report([ob])["triangles"]
    A.preview_rig(focus=(x / 2, 0.0, 1.0), distance=max(8.0, x * 0.9), pitch=35, yaw=-15, ground=0,
                  human=True, human_at=(-1.0, 0.0))
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True
    if save:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.splitext(os.path.abspath(script))[0] + ".blend")
    return out


def run(script, assets, ns):
    """A family script's entry point: EXPORT writes the FBXs, otherwise it shows them (SAVE also
    saves the working .blend beside the script)."""
    if ns.get("EXPORT"):
        return export(script, assets)
    return show(script, assets, save=ns.get("SAVE", False))
