"""
Renders pose_review.py's posed copies into contact sheets, one per outfit and body: the poses
across, front, side and back down. In a throwaway scene (the open scene is not touched). Exec
inside Blender through the MCP after pose_review.py; optionally OUT (a folder, default the
system temp folder) and ONLY (outfit names). REVIEW_SHEETS lists the files written.
"""
import array
import math
import os
import tempfile

import bpy

OUT = globals().get("OUT", tempfile.gettempdir())
W, H = 300, 420
col = bpy.data.collections["Pose_Review"]
groups = {}
for c in col.children:
    sex, rest = c.name.split("_", 1)
    outfit, pose = rest.rsplit("_", 1) if not rest.endswith("arms_up") else (rest[:-len("_arms_up")], "arms_up")
    if globals().get("ONLY") and outfit not in ONLY:
        continue
    groups.setdefault((sex, outfit), []).append((pose, c))

sc = bpy.data.scenes.new("_check_tmp")
cd = bpy.data.cameras.new("_chk"); cd.type = "ORTHO"; cd.ortho_scale = 2.1
cam = bpy.data.objects.new("_chk", cd); sc.collection.objects.link(cam); sc.camera = cam
sc.render.engine = "BLENDER_WORKBENCH"; sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "MATERIAL"
sc.render.resolution_x = W; sc.render.resolution_y = H
REVIEW_SHEETS = []
views = (("front", (0, 4), 180), ("side", (4, 0), 90), ("back", (0, -4), 0))
for (sex, outfit), items in groups.items():
    files = []
    for pose, c in items:
        for o in list(sc.collection.objects):
            if o is not cam:
                sc.collection.objects.unlink(o)
        for o in c.objects:
            sc.collection.objects.link(o)
        rig = next(o for o in c.objects if o.type == "ARMATURE")
        x, y = rig.location.x, rig.location.y
        for k, (dx, dy), rz in views:
            cam.location = (x + dx, y + dy, 0.95)
            cam.rotation_euler = (math.radians(90), 0, math.radians(rz))
            f = os.path.join(OUT, f"_{sex}_{outfit}_{pose}_{k}.png")
            sc.render.filepath = f
            bpy.ops.render.render(write_still=True, scene=sc.name)
            files.append(f)
    cols, rows = len(items), len(views)
    sheet = bpy.data.images.new("_sheet", cols * W, rows * H)
    px = array.array("f", [0.0]) * (cols * W * rows * H * 4)
    for n, f in enumerate(files):
        im = bpy.data.images.load(f)
        src = array.array("f", [0.0]) * (W * H * 4)
        im.pixels.foreach_get(src)
        c_, r_ = n // rows, rows - 1 - n % rows
        for yy in range(H):
            o = ((r_ * H + yy) * cols * W + c_ * W) * 4
            px[o:o + W * 4] = src[yy * W * 4:(yy + 1) * W * 4]
        bpy.data.images.remove(im)
        os.remove(f)
    sheet.pixels.foreach_set(px)
    path = os.path.join(OUT, f"outfit_{sex}_{outfit}.png")
    sheet.filepath_raw = path
    sheet.file_format = "PNG"
    sheet.save()
    bpy.data.images.remove(sheet)
    REVIEW_SHEETS.append(path)
bpy.data.scenes.remove(sc); bpy.data.objects.remove(cam); bpy.data.cameras.remove(cd)
