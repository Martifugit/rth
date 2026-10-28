"""The harbour kit's showcase: one working waterfront built from the kits, the preview at the top of the
gallery. It shows what the pieces do together rather than one by one:

- a long stone quay (the composer: layout.py) with bollards, lamps, water stairs, the sea ship berthed
  alongside, reached by a gangplank, a crane over her stern; a capstan, crates, pots and nets about;
- a stone mole closing the basin on the east, carried over the water on an arch, widening through a
  splay into the round lamp head at the harbour's mouth, channel markers outside;
- two timber jetties down stairs off the quay to the west: one turning into an L with a lamp and a
  pontoon beyond it, the other ending in a T-head with mooring posts; the river boat alongside, fishing
  and rowing boats between them;
- past the quay's end, a natural shore: shingle, a slipway with a boat drawn up beside it, the old
  jetty rotting into the reeds (weathered), trees behind (the nature kit).

Everything is worn (common.WEAR): it is a harbour in use. Tiles: x east, y north; the quay's face is
the line y 30.5, the water south of it, the land north.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\harbour\\showcase.py")
"""
import math
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
for name in [m for m in sys.modules if m in ("rth_art", "common", "structures", "vessels", "layout", "styles")
             or m.split(".")[0] in ("artlib", "assets", "parts")]:
    sys.modules.pop(name, None)
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import common as C  # noqa: E402
import layout as Lay  # noqa: E402
import structures as S  # noqa: E402
import vessels as V  # noqa: E402
from styles import STYLES  # noqa: E402
import assets.containers.village as containers  # noqa: E402  (the things people leave about)
import assets.dressing.village as dressing  # noqa: E402
import assets.plants.common as plants  # noqa: E402
import assets.trees.common as trees  # noqa: E402
import assets.ways.village as ways  # noqa: E402

A, N = C.A, C.N
A._palette = None

W, H = 64, 44                   # the drawing: tiles x 0..63, y 0..43
QX0 = 10                        # where the stone quay begins; west of it the natural shore


# ---- the ground ---------------------------------------------------------------------------------

def shore_y(x):
    """Where the natural shore meets the water, west of the quay."""
    return 27.5 + 1.2 * math.sin(x * 0.6 + 1.0)


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


SLIP = (5.4, 7.6, 26.0, 31.5)   # the slipway's ramp, cut into the bank (x0, x1, y0, y1)


def slip_z(y):
    return C.QUAY - 0.1 + (-0.5 - (C.QUAY - 0.1)) * (31.5 - y) / 5.0


def ground_z(x, y):
    """The land: at the quay's height behind the quay, a bank sloping to shingle and under the water
    along the natural shore (cut down where the slipway runs into it), the bed under the basin."""
    x0, x1, y0, y1 = SLIP
    if x0 <= x <= x1 and y0 <= y <= y1:
        return min(_ground_z(x, y), slip_z(y) - 0.08)
    return _ground_z(x, y)


def on_road(x, y):
    """The trodden road along the back of the quay, and the track inland from it."""
    return (x >= 7.0 and 32.6 <= y <= 34.4) or (27.4 <= x <= 29.2 and y >= 33.0)


def _ground_z(x, y):
    if x >= QX0 - 0.5:
        return C.QUAY - 0.02 if y >= 30.5 else -1.0
    land = 0.6 + (C.QUAY - 0.62) * smooth((x - 5.0) / 4.5)
    d = y - shore_y(x)
    if d >= 3.0:
        return land
    if d >= 0.0:
        return 0.05 + (land - 0.05) * smooth(d / 3.0)
    return 0.05 - 1.05 * smooth(-d / 2.0)


def ground():
    """The land as one mesh (grass, shingle at the water's edge, mud under it), the water over the
    basin and the sea, and the bed below."""
    bm = bmesh.new()
    step = 0.5
    x0, x1, y0, y1 = -10.0, W + 2.0, -6.0, H
    nx, ny = int((x1 - x0) / step), int((y1 - y0) / step)
    vs = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            x, y = x0 + i * step, y0 + j * step
            vs[i, j] = bm.verts.new((x, y, ground_z(x, y)))
    for i in range(nx):
        for j in range(ny):
            q = (vs[i, j], vs[i + 1, j], vs[i + 1, j + 1], vs[i, j + 1])
            z = sum(v.co.z for v in q) / 4
            cx, cy = x0 + (i + 0.5) * step, y0 + (j + 0.5) * step
            m = (4 if on_road(cx, cy) else 0) if z > 0.38 else 1 if z > -0.02 else 2
            bm.faces.new(q).material_index = m
    water = [bm.verts.new(p) for p in ((x0, y0, 0), (x1, y0, 0), (x1, 31.0, 0), (x0, 31.0, 0))]
    bm.faces.new(water).material_index = 3
    for xa, xb in ((-12.0, x0), (x1, 120.0)):           # the sea on either side, out to the gallery's width
        bm.faces.new([bm.verts.new(p) for p in ((xa, y0, 0), (xb, y0, 0), (xb, 31.0, 0), (xa, 31.0, 0))]).material_index = 3
        bm.faces.new([bm.verts.new(p) for p in ((xa, y0, -1.5), (xa, 31.0, -1.5), (xb, 31.0, -1.5), (xb, y0, -1.5))]).material_index = 2
    return N.make("ShowcaseGround", bm, ["Grass", "Shingle", "Mud", "Mere", "Trodden"], coll="Preview")


# ---- the drawing --------------------------------------------------------------------------------

def drawing():
    """The quay, the mole and the jetties as a composer drawing (rows from the north)."""
    g = {}

    def paint(xa, ya, xb, yb, ch):
        for x in range(xa, xb + 1):
            for y in range(ya, yb + 1):
                g[(x, y)] = ch
    paint(QX0, 31, 52, 32, "S")                            # the quay, two tiles deep
    for x in (14, 20, 27, 33, 42, 47):
        g[(x, 31)] = "b"
    for x in (12, 24, 36, 49):
        g[(x, 32)] = "l"
    g[(24, 30)] = "v"                                      # water stairs off the quay
    paint(50, 18, 51, 30, "S")                             # the mole, down the east side
    paint(50, 22, 51, 24, "#")                             # ... carried on an arch
    paint(49, 12, 52, 17, "#")                             # ... its splay and its head
    g[(50, 27)] = "b"
    g[(50, 20)] = "b"
    g[(49, 26)] = "<"                                      # stairs down into the basin
    g[(12, 30)] = "v"                                      # jetty A: down off the quay, south, then west
    paint(12, 21, 12, 29, "J")
    paint(8, 20, 12, 20, "J")
    g[(8, 20)] = "l"
    g[(18, 30)] = "v"                                      # jetty B: south to a T-head
    paint(18, 20, 18, 29, "J")
    paint(16, 17, 20, 19, "J")
    g[(16, 17)] = "p"
    g[(20, 17)] = "p"
    g[(18, 17)] = "l"
    paint(8, 16, 9, 19, "=")                               # the pontoon, south of jetty A's L
    rows = []
    for y in range(H - 1, -1, -1):
        rows.append("".join(g.get((x, y), ".") for x in range(W)))
    return rows


# ---- building it --------------------------------------------------------------------------------

def place(ob, x, y, rot=0, z=0.0):
    ob.location = (x, y, z)
    ob.rotation_euler.z = math.radians(rot)
    return ob


def build():
    A.reset()
    for c in list(bpy.data.collections):
        if c.name in ("Harbour", "Walk", "Preview", "Nature"):
            bpy.data.collections.remove(c)
    ground()
    o = {}
    C.WEAR = 1
    C.COLL = "Harbour"
    o["harbour"] = Lay.compose("Showcase", drawing())
    # the mole's arch, splay and head (rot 180: they run south from where they join)
    o["arch"] = place(S.stone_arch(2, name="ShowcaseArch"), 51, 24, 180)
    o["splay"] = place(S.stone_splay(2, 4, name="ShowcaseSplay"), 51, 17, 180)
    o["head"] = place(S.stone_head(4, 4, name="ShowcaseHead"), 52, 15, 180)
    o["pontoon"] = place(S.pontoon(name="ShowcasePontoon"), 9, 19, 180)
    # the sea ship alongside the quay, her gangplank, the crane over her stern
    o["ship"] = place(V.ship(name="ShowcaseShip"), 37, 26.6, 90)
    o["gangplank"] = place(S.gangplank(rise=1.1, length=2, name="ShowcaseGangplank"), 37, 31, 180)
    o["crane"] = place(S.crane(name="ShowcaseCrane"), 44, 32.2, z=C.QUAY)
    o["capstan"] = place(S.capstan(name="ShowcaseCapstan"), 30, 32.4, z=C.QUAY)
    o["crates"] = place(S.fish_crates(name="ShowcaseCrates"), 15.4, 32.5, 10, z=C.QUAY)
    o["crates_2"] = place(S.fish_crates(name="ShowcaseCrates2"), 34.5, 32.6, -20, z=C.QUAY)
    o["pots"] = place(S.lobster_pots(name="ShowcasePots"), 21.3, 32.5, z=C.QUAY)
    o["pots_2"] = place(S.lobster_pots(name="ShowcasePots2"), 51.3, 19.3, z=C.QUAY)
    # boats in the west of the basin
    o["river_boat"] = place(V.barge(name="ShowcaseRiverBoat"), 9.9, 25.4, 0)
    o["fishing_boat"] = place(V.fishing_boat(name="ShowcaseFishingBoat"), 15.0, 24.2, 180)
    o["fishing_boat_2"] = place(V.fishing_boat(name="ShowcaseFishingBoat2"), 21.4, 23.0, 186)
    o["rowing_boat"] = place(V.rowing_boat(name="ShowcaseRowingBoat"), 14.0, 17.6, 200)
    o["rowing_boat_2"] = place(V.rowing_boat(name="ShowcaseRowingBoat2"), 6.2, 21.5, 160)
    # the mouth
    o["marker"] = place(S.channel_marker(name="ShowcaseMarker"), 41, 9)
    o["marker_2"] = place(S.channel_marker(name="ShowcaseMarker2"), 58, 10)
    # the natural shore: the slipway, a boat drawn up, nets drying
    o["slipway"] = place(S.slipway(name="ShowcaseSlipway"), 6, 31)
    o["drawn_up"] = place(V.rowing_boat(name="ShowcaseDrawnUp"), 3.6, 30.2, 165, z=ground_z(3.6, 30.2) + 0.15)
    o["drawn_up"].rotation_euler.x = math.radians(-6)
    o["nets"] = place(S.net_rack(name="ShowcaseNets"), 1.5, 34.5, 10, z=ground_z(1.5, 34.5))
    # the old jetty rotting into the reeds, weathered
    C.STYLE = dict(STYLES["Weathered"])
    o["old_worn"] = place(S.jetty_worn(3, name="ShowcaseOldJetty"), -2, 27, 180)
    o["old_broken"] = place(S.jetty_broken(name="ShowcaseOldJettyBroken"), -2, 24, 180)
    o["old_stubs"] = place(S.pile_stubs(name="ShowcaseOldStubs"), -2, 21, 180)
    C.STYLE = {}
    C.WEAR = 0
    # cargo coming off the ship and waiting on the quay (the nature kit's wilds), a cart for it,
    # a fingerpost where the road leaves the quay, firewood stacked behind
    Q = C.QUAY
    for k, (x, y) in enumerate(((34.9, 32.2), (35.5, 32.75), (34.6, 32.85))):
        o["barrel_%d" % k] = containers.barrel(seed=k + 1, at=(x, y, Q))
    o["sacks"] = containers.sacks(at=(39.4, 32.6, Q))
    o["crate_0"] = containers.crate(seed=2, at=(42.4, 32.5, Q))
    o["crate_1"] = containers.crate(seed=3, at=(42.5, 32.55, Q + 0.6))
    o["crate_2"] = containers.crate(seed=4, at=(43.1, 33.2, Q))
    o["cart"] = dressing.farm_cart(at=(31.5, 35.2, Q), rot=0.5)
    o["fingerpost"] = ways.fingerpost(at=(8.4, 34.9, ground_z(8.4, 34.9)), arms=(0, 90, 200))
    o["woodpile"] = dressing.woodpile(at=(23.0, 36.0, Q))
    o["trough"] = containers.trough(at=(33.5, 36.6, Q))
    # the nature kit: reeds along the shore, trees and rough grass behind
    for k, x in enumerate((-7.0, -5.2, -0.4, 1.8, 3.0)):
        y = shore_y(x) - 0.4
        o["reeds_%d" % k] = plants.reeds(seed=3 + k, at=(x, y, ground_z(x, y) + 0.2))
    for k, (fn, x, y) in enumerate(((trees.alder, -6.0, 37.0), (trees.ash, -1.0, 40.0), (trees.hawthorn, 4.0, 38.5), (trees.alder, 58.0, 38.0),
                                    (trees.ash, 62.0, 41.0))):
        o["tree_%d" % k] = fn(at=(x, y, ground_z(x, y)))
    for k, (x, y) in enumerate(((-3.5, 32.0), (2.5, 33.0), (6.5, 35.0), (-7.0, 31.0))):
        o["tussock_%d" % k] = plants.tussock(seed=k + 1, at=(x, y, ground_z(x, y)))
    return o


def rig():
    A.preview_rig(focus=(28.0, 24.0, 1.0), distance=70.0, pitch=40, yaw=-15, ground=0, human=True, human_at=(18.0, 25.0))
    A.figures([(31.0, 31.6, C.QUAY), (12.0, 24.0, C.JETTY), (50.5, 14.0, C.QUAY), (37.0, 25.6, 2.0), (18.0, 18.0, C.JETTY)])
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            sp = area.spaces.active
            sp.shading.type = "MATERIAL"
            sp.shading.use_scene_lights = True
            sp.shading.use_scene_world = True


objs = build()
rig()
lc = bpy.context.view_layer.layer_collection.children.get("Walk")
if lc:
    lc.hide_viewport = True
result = {k: A.report([v])["triangles"] for k, v in objs.items() if not k[-1].isdigit()}
