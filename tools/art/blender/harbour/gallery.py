"""The harbour kit's gallery: every kit laid out as its own cluster, each built in rows, a style (styles.py)
with or without wear (common.WEAR: the flaws of use), labelled, south of the example harbours (harbours.py). The place to judge a kit, compare
its styles, and see a new piece next to the rest.

In the Outliner: Gallery > <kit> > <kit> . <style>. To add a piece, add a line to its kit in KITS;
to add a kit, add an entry; to add a style, add it to styles.py. Walk layouts are not written from
here (the examples and the exports write them).

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\harbour\\gallery.py")
"""
import math
import os
import runpy
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
runpy.run_path(os.path.join(HERE, "harbours.py"), run_name="__main__")      # the examples, from a clean scene
for name in ("common", "structures", "vessels", "styles"):
    sys.modules.pop(name, None)
import common as C  # noqa: E402
import structures as S  # noqa: E402
import vessels as V  # noqa: E402
from styles import STYLES  # noqa: E402

A, N = C.A, C.N

# The kits: (name, [(piece, builder)]). Builders take the object's name.
KITS = [
    ("Timber jetty", [
        ("JettySpan1x1", lambda n: S.jetty_span(1, 1, name=n)),
        ("JettySpan1x3", lambda n: S.jetty_span(1, 3, name=n)),
        ("JettySpan2x3", lambda n: S.jetty_span(2, 3, name=n)),
        ("JettyHead", lambda n: S.jetty_head(name=n)),
        ("JettyStairs", lambda n: S.jetty_stairs(name=n)),
        ("Pontoon", lambda n: S.pontoon(name=n)),
        ("Gangplank", lambda n: S.gangplank(name=n)),
        ("Slipway", lambda n: S.slipway(name=n)),
    ]),
    ("Stone pier", [
        ("StonePier2x2", lambda n: S.stone_pier(2, 2, name=n)),
        ("StonePier2x4", lambda n: S.stone_pier(2, 4, name=n)),
        ("StoneArch", lambda n: S.stone_arch(name=n)),
        ("StonePierHead2", lambda n: S.stone_head(2, 2, name=n)),
        ("StoneSplay2to4", lambda n: S.stone_splay(2, 4, name=n)),
        ("StonePier4x2", lambda n: S.stone_pier(4, 2, name=n)),
        ("StonePierHead", lambda n: S.stone_head(4, 2, name=n)),
        ("StoneSteps", lambda n: S.stone_steps(name=n)),
        ("QuayEdge4", lambda n: S.quay_edge(4, name=n)),
        ("QuaySteps", lambda n: S.quay_steps(name=n)),
    ]),
    ("Worn and broken timber", [
        ("JettySpanWorn", lambda n: S.jetty_worn(3, name=n)),
        ("JettySpanBroken", lambda n: S.jetty_broken(name=n)),
        ("PileStubs", lambda n: S.pile_stubs(name=n)),
    ]),
    ("Waterfront dressing", [
        ("Bollard", lambda n: S.bollard(name=n)),
        ("Capstan", lambda n: S.capstan(name=n)),
        ("Crane", lambda n: S.crane(name=n)),
        ("NetRack", lambda n: S.net_rack(name=n)),
        ("HarbourLamp", lambda n: S.harbour_lamp(name=n)),
        ("ChannelMarker", lambda n: S.channel_marker(name=n)),
        ("FishCrates", lambda n: S.fish_crates(name=n)),
        ("LobsterPots", lambda n: S.lobster_pots(name=n)),
    ]),
    ("Vessels", [
        ("RowingBoat", lambda n: V.rowing_boat(name=n)),
        ("FishingBoat", lambda n: V.fishing_boat(name=n)),
        ("RiverBarge", lambda n: V.barge(name=n)),
        ("SeaShip", lambda n: V.ship(name=n)),
    ]),
]

# The rows each kit is built in: a style (styles.py) and whether it is worn (common.WEAR).
ROWS = [("Plain", 0), ("Plain", 1), ("Weathered", 1), ("Dark", 0), ("Pale", 0)]

TOP = -12.0         # the gallery starts this far south of the examples
GAP = 1.5           # between pieces in a row
ROW_GAP = 2.5       # between style rows
KIT_GAP = 6.0       # between kits


def nest(child, parent):
    """Put collection `child` under `parent` (both made if missing), out of the scene's root."""
    p = A.collection(parent)
    c = A.collection(child)
    root = bpy.context.scene.collection
    if c.name in root.children:
        root.children.unlink(c)
    if c.name not in p.children:
        p.children.link(c)
    return c


def label(text, at, size=1.0, coll="Gallery labels"):
    """A flat text label lying on the ground (read from the south)."""
    cu = bpy.data.curves.new(text, type="FONT")
    cu.body = text
    cu.size = size
    ob = bpy.data.objects.new("Label " + text, cu)
    ob.location = at
    nest(coll, "Gallery")
    A.collection(coll).objects.link(ob)
    return ob


def footprint(ob):
    """The piece's extent on the ground (x0, y0, x1, y1), in its own coordinates."""
    xs = [v.co.x for v in ob.data.vertices]
    ys = [v.co.y for v in ob.data.vertices]
    return min(xs), min(ys), max(xs), max(ys)


def build():
    y = TOP
    C.WALK = False
    out = {}
    for kit, pieces in KITS:
        label(kit, (0.0, y + 0.8, 0.02), size=1.4)
        y -= 1.5
        for style, wear in ROWS:
            row = style + (", worn" if wear else "")
            coll = "%s . %s" % (kit, row)
            nest(kit, "Gallery")
            nest(coll, kit)
            C.COLL, C.STYLE, C.WEAR = coll, dict(STYLES[style]), wear
            label(row, (-11.0, y - 1.0, 0.02), size=0.8)
            x = 0.0
            row_depth = 0.0
            for piece, fn in pieces:
                ob = fn("%s%s.%s" % (piece, "Worn" if wear else "", style))
                x0, y0, x1, y1 = footprint(ob)
                if y1 - y0 > 6 and y1 - y0 > 2 * (x1 - x0):      # long pieces (vessels) lie along the row, bow east
                    ob.rotation_euler.z = math.radians(-90)
                    x0, y0, x1, y1 = y0, -x1, y1, -x0
                ob.location = (x - x0, y - y1, 0)
                x += (x1 - x0) + GAP
                row_depth = max(row_depth, y1 - y0)
                out[ob.name] = ob
            y -= row_depth + ROW_GAP
        y -= KIT_GAP
    C.WALK, C.STYLE, C.COLL, C.WEAR = True, {}, "Harbour", 0
    return out, y


def gallery_ground(bottom):
    """Water under the gallery, the bed under it."""
    bm = bmesh.new()
    A.box(bm, (-12, bottom, C.BED - 0.1), (120, TOP + 2, C.BED), 0)
    bm.faces.new([bm.verts.new(p) for p in ((-12, bottom, 0), (120, bottom, 0), (120, TOP + 2, 0), (-12, TOP + 2, 0))]).material_index = 1
    ob = N.make("GalleryWater", bm, ["Mud", "Mere"], coll="Preview")
    return ob


objs, bottom = build()
gallery_ground(bottom)
lc = bpy.context.view_layer.layer_collection.children.get("Walk")
if lc:
    lc.hide_viewport = True
result = {"pieces": len(objs), "bottom": round(bottom, 1)}
