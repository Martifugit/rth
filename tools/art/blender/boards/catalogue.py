"""The catalogue board: every model in tools/art/catalogue.json (or a filtered part of it) built and laid
out together, one collection and one row per family, each row labelled with its family and each
model with its name and status, the player at the start of the first row for scale. It builds
nothing of its own: each model comes from its family's ASSETS (a one-off's script in its style's
folder), so what shows is exactly what exports.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\boards\\catalogue.py")
    runpy.run_path(path, init_globals={"STATUS": ["first pass"], "ONLY": ["assets/furniture"]})

STATUS: the statuses to show (all by default). ONLY: ids, families or kinds to show, as whole path
segments (everything by default).
SAVE: also save the working file catalogue.blend beside this script. Models the board cannot build
(no builder in ASSETS yet, as with the older one-off scripts) are listed in its result, not guessed.
"""
import importlib
import json
import os
import sys
import traceback

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards", "buildings")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402

CATALOGUE = os.path.join(ROOT, "..", "catalogue.json")
GAP, ROW_GAP = 0.8, 2.5                         # between models in a row, and between rows


def builder(id_):
    """The builder for a catalogue id (<layer>/<kind>/<style>/<Name>): its family's ASSETS, or a
    one-off script in <layer>/<kind>/<style>/ whose ASSETS name it. None if nothing builds it."""
    layer, kind, style, name = id_.split("/")
    path = os.path.join(ROOT, layer, kind, style + ".py")
    if os.path.exists(path):
        assets = getattr(importlib.import_module("%s.%s.%s" % (layer, kind, style)), "ASSETS", {})
        if name in assets:
            return assets[name]
    folder = os.path.join(ROOT, layer, kind, style)
    for f in sorted(os.listdir(folder)) if os.path.isdir(folder) else ():
        if not f.endswith(".py") or "ASSETS" not in open(os.path.join(folder, f), encoding="utf8").read():
            continue
        assets = getattr(importlib.import_module("%s.%s.%s.%s" % (layer, kind, style, f[:-3])), "ASSETS", {})
        if name in assets:
            return assets[name]
    return None


def within(id_, prefix):
    """Whether a catalogue id falls under a prefix of whole path segments: a family, a kind, or one id."""
    prefix = prefix.rstrip("/")
    return id_ == prefix or id_.startswith(prefix + "/")


def label(text, at, size, coll):
    cu = bpy.data.curves.new("Label", "FONT")
    cu.body = text
    cu.size = size
    ob = bpy.data.objects.new("Label " + text, cu)
    ob.location = at
    cu.materials.append(A.mat("Charcoal"))
    coll.objects.link(ob)
    return ob


def board(status=None, only=None):
    entries = json.load(open(CATALOGUE, encoding="utf8"))["assets"]
    entries = [e for e in entries if (not status or e["status"] in status) and (not only or any(within(e["id"], p) for p in only))]
    families = {}
    for e in entries:
        families.setdefault(e["id"].rsplit("/", 1)[0], []).append(e)
    A.reset()
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    top = A.collection("Catalogue")
    y, widest, built, missing, failed = 0.0, 0.0, 0, [], {}
    for fam in sorted(families):
        coll = bpy.data.collections.new(fam)
        top.children.link(coll)
        row = []
        for e in families[fam]:
            try:
                make = builder(e["id"])
            except Exception:
                failed[e["id"]] = traceback.format_exc(limit=1)
                continue
            if make is None:
                missing.append(e["id"])
                continue
            try:
                ob = make()
            except Exception:
                failed[e["id"]] = traceback.format_exc()[-400:]
                continue
            for c in list(ob.users_collection):
                c.objects.unlink(ob)
            coll.objects.link(ob)
            row.append((e, ob))
        if not row:
            bpy.data.collections.remove(coll)
            continue
        bpy.context.view_layer.update()
        x, depth = 0.0, 0.0
        for e, ob in row:
            vs = [ob.matrix_world @ v.co for v in ob.data.vertices] or [ob.location]
            x0, x1 = min(v.x for v in vs), max(v.x for v in vs)
            y0, y1 = min(v.y for v in vs), max(v.y for v in vs)
            ob.location.x += x - x0
            ob.location.y += y - y1                                     # every model's back on the row's line
            depth = max(depth, y1 - y0)
            label("%s (%s)" % (e["id"].rsplit("/", 1)[1], e["status"]), (x, y - (y1 - y0) - 0.45, 0.01), 0.16, coll)
            x += (x1 - x0) + GAP
            built += 1
        label(fam, (-1.0, y + 0.6, 0.01), 0.45, coll)
        widest = max(widest, x)
        y -= depth + ROW_GAP + 0.6
    A.preview_rig(focus=(min(widest, 30) / 2, y / 2, 1.0), distance=max(14.0, -y * 0.7), pitch=50, yaw=-10,
                  ground=int(max(widest, -y)) + 8, human=True, human_at=(-1.5, -0.5))
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True
    return {"families": len([c for c in top.children]), "built": built, "no_builder": missing, "failed": failed}


if __name__ in ("__main__", "<run_path>"):
    result = board(globals().get("STATUS"), globals().get("ONLY"))
    if globals().get("SAVE"):
        bpy.ops.wm.save_as_mainfile(filepath=os.path.splitext(os.path.abspath(__file__))[0] + ".blend")
