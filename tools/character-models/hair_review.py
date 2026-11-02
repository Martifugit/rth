"""
Builds a hair style onto both review heads and renders it from six sides (front, three-quarter,
right, back, left, above) for each body, in a throwaway scene (the open scene is not touched),
into one contact sheet: <OUT>/<TAG>_sheet.png, the male on the top row. Exec it inside Blender
through the MCP:

    g = {"STYLE": "long", "NAMES": ("hair_long", "hair_long.001"), "TAG": "long", "__name__": "rev"}
    exec(open(r"C:\\dev\\rth\\tools\\character-models\\hair_review.py").read(), g)

Globals in: STYLE, NAMES (the male and female hair object names), TAG; optionally OUT (a folder,
default the system temp folder), SCALE (the orthographic frame: 0.42 for short hair, 0.7 for hair
to the shoulders) and DZ (moves the frame up or down). REVIEW_RESULT holds build_hair's results
(vertex counts, clipping). The heads, bodies and rigs named below are BodyReview.blend's.
"""
import array
import math
import os
import tempfile

import bpy

HERE = r"C:\dev\rth\tools\character-models"
OUT = globals().get("OUT", tempfile.gettempdir())
g = {"STYLE": STYLE, "HEADS": {"body_head.003": NAMES[0], "body_head.004": NAMES[1]},
     "BODY": {"body_head.003": ["torso_draft", "body_arms.003"], "body_head.004": ["torso_draft.001", "body_arms.004"]},
     "__name__": "hair"}
exec(open(os.path.join(HERE, "build_hair.py")).read(), g)
REVIEW_RESULT = g["HAIR_RESULT"]

PARTS = {"m": ["Rig.004", "body_head.003", NAMES[0], "torso_draft", "body_arms.003"],
         "f": ["Rig.005", "body_head.004", NAMES[1], "torso_draft.001", "body_arms.004"]}
sc = bpy.data.scenes.new("_check_tmp")
cd = bpy.data.cameras.new("_chk"); cd.type = 'ORTHO'; cd.ortho_scale = globals().get("SCALE", 0.42)
cam = bpy.data.objects.new("_chk", cd); sc.collection.objects.link(cam); sc.camera = cam
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
sc.render.resolution_x = 500; sc.render.resolution_y = 500
views = [("front", (0, 1, 0), 90, 180), ("q", (0.7, 0.7, 0), 90, 135), ("side", (1, 0, 0), 90, 90),
         ("back", (0, -1, 0), 90, 0), ("lside", (-1, 0, 0), 90, -90), ("top", (0, 0.35, 1), 20, 180)]
files = []
for who, x0, z in (("m", 0.85, 1.70 + globals().get("DZ", 0)), ("f", -0.85, 1.65 + globals().get("DZ", 0))):
    for o in list(sc.collection.objects):     # one body at a time: the other would stand in the side views
        if o is not cam:
            sc.collection.objects.unlink(o)
    for n in PARTS[who]:
        sc.collection.objects.link(bpy.data.objects[n])
    for k, d, tilt, rz in views:
        cam.location = (x0 + d[0] * 5, d[1] * 5, z + d[2] * 5)
        cam.rotation_euler = (math.radians(tilt), 0, math.radians(rz))
        f = os.path.join(OUT, f"_{TAG}_{who}_{k}.png"); sc.render.filepath = f
        bpy.ops.render.render(write_still=True, scene=sc.name); files.append(f)
bpy.data.scenes.remove(sc); bpy.data.objects.remove(cam); bpy.data.cameras.remove(cd)

S = bpy.data.images.new("_sheet", 6 * 250, 2 * 250)
px = array.array('f', [1.0]) * (6 * 250 * 2 * 250 * 4)
for n, f in enumerate(files):
    im = bpy.data.images.load(f); im.scale(250, 250)
    src = array.array('f', [0.0]) * (250 * 250 * 4); im.pixels.foreach_get(src)
    col, row = n % 6, 1 - n // 6
    for y in range(250):
        o = ((row * 250 + y) * 6 * 250 + col * 250) * 4
        px[o:o + 250 * 4] = src[y * 250 * 4:(y + 1) * 250 * 4]
    bpy.data.images.remove(im); os.remove(f)
S.pixels.foreach_set(px)
S.filepath_raw = os.path.join(OUT, f"{TAG}_sheet.png"); S.file_format = 'PNG'; S.save()
bpy.data.images.remove(S)
