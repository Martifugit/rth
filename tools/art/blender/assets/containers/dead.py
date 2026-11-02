r"""Containers, dead. The grave chest at the Landing, where the dead keep what they have banked: a big
black chest bound in iron, sunk a little in the ash, its lid strapped down over the front to one lock
whose keyhole glows faintly; candle stubs burn on its lid.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
dead.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Containers/Dead/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\containers\dead.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import os
import sys

from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.caves import make  # noqa: E402
from assets.lights import dead as lights  # noqa: E402

MATS = ["DrownedWood", "Blackiron", "CandleWax", "CandleFlame", "SoulGlow"]
WOOD, IRON, WAX, FLAME, GLOW = range(5)

BODY = (0.46, 0.3, 0.46)        # the body's half length (X), half depth (Y) and top above the ground
SINK = 0.08                     # how deep it sits in the ash
# the lid's arched profile across the chest (y, z above the body's top), front (-Y) to back
LID = [(-0.32, 0.0), (-0.31, 0.07), (-0.2, 0.13), (0.0, 0.155), (0.2, 0.13), (0.31, 0.07), (0.32, 0.0)]
LID_X = 0.48                    # the lid's half length, a little over the body
STRAPS = (-0.27, 0.27)          # the iron straps' middles along the chest
STRAP = (0.035, 0.012)          # a strap's half width and thickness


def lid_profile(off=0.0):
    """The lid's profile pushed out `off` along its normals (a strap over it)."""
    out = []
    for i, (y, z) in enumerate(LID):
        a = Vector(LID[max(i - 1, 0)])
        b = Vector(LID[min(i + 1, len(LID) - 1)])
        t = (b - a).normalized()
        n = Vector((-t.y, t.x)) if (y, z) not in (LID[0], LID[-1]) else Vector((1 if y > 0 else -1, 0))
        if n.y < 0 and (y, z) not in (LID[0], LID[-1]):
            n = -n
        out.append((y + n.x * off, z + n.y * off))
    return out


def lid(bm, top, x0, x1, profile, mat):
    """A solid along X with the given (y, z) profile, z above `top`."""
    a = [bm.verts.new((x0, y, top + z)) for y, z in profile]
    b = [bm.verts.new((x1, y, top + z)) for y, z in profile]
    n = len(profile)
    for i in range(n - 1):
        bm.faces.new((a[i], a[i + 1], b[i + 1], b[i])).material_index = mat
    bm.faces.new((a[-1], a[0], b[0], b[-1])).material_index = mat          # the underside
    bm.faces.new(list(reversed(a))).material_index = mat
    bm.faces.new(b).material_index = mat


def grave_chest(at=(0, 0, 0)):
    """The bank of the dead, one tile: an iron-bound chest of black timber sunk in the ash, an arched
    lid with two straps over it and down the front and back, iron corners, a lock plate at the front
    (Blender -Y, the game's north at no rotation) with a faint soul-glow keyhole and the lid's hasp
    over it; three candle stubs burning on the lid at one end, wax run down its front."""
    bm = bmesh.new()
    hx, hy, h = BODY
    A.box(bm, (-hx, -hy, -SINK), (hx, hy, h), WOOD)
    lid(bm, h, -LID_X, LID_X, [(y * 1.03, z) for y, z in LID], WOOD)
    t = STRAP[1]
    for x in STRAPS:                                           # the straps, over the lid and down both faces
        w = STRAP[0]
        lid(bm, h, x - w, x + w, lid_profile(t), IRON)
        for s in (-1, 1):
            A.box(bm, (x - w, s * hy - (t if s < 0 else 0) - 0.002 * s, -0.02), (x + w, s * hy + (t if s > 0 else 0), h + 0.02),
                  IRON)
    for sx in (-1, 1):                                         # iron at the four upright corners
        for sy in (-1, 1):
            xs, ys = (sx * (hx + 0.012), sx * (hx - 0.06)), (sy * (hy + 0.012), sy * (hy - 0.06))
            A.box(bm, (min(xs), min(ys), -0.02), (max(xs), max(ys), h - 0.01), IRON)
    # the lock: a plate on the front, the hasp coming down from the lid onto it, the keyhole on the plate
    front = -hy - 0.014
    A.box(bm, (-0.075, front - 0.012, h - 0.21), (0.075, front + 0.004, h - 0.04), IRON)
    lip = LID[0][0] * 1.03                                     # the lid's front edge
    A.box(bm, (-0.03, lip - 0.03, h - 0.08), (0.03, lip + 0.005, h + 0.06), IRON)
    kz = h - 0.15
    bm.normal_update()                                         # the decal reads the faces' normals
    A.decal(bm, [(-0.012, -0.5, kz + 0.025), (-0.012, -0.5, kz + 0.04), (0.0, -0.5, kz + 0.05), (0.012, -0.5, kz + 0.04),
                 (0.012, -0.5, kz + 0.025), (0.022, -0.5, kz - 0.03), (-0.022, -0.5, kz - 0.03)],
            (0, -1, 0), mat=GLOW, lift=0.003)
    # candle stubs on the lid, toward its west end, along the ridge
    before = set(bm.faces)
    ridge = h + LID[3][1]
    for x, y, hh, lit in ((-0.34, 0.01, 0.13, True), (-0.26, -0.05, 0.07, True), (-0.38, -0.07, 0.04, False)):
        z = ridge - 0.012 - 0.15 * y * y
        lights.pool(bm, 0.05, 0.015, centre=(x, y), points=6, z=z)
        lights.candle(bm, (x, y, z + 0.008), hh, 0.028, (0.0, 0.0), lit)
    lights.drip(bm, (-0.3, -0.25, h + 0.11), (0, -0.6, -1), 0.1, 0.02, (0, -1, 0.5))
    remap = {lights.WAX: WAX, lights.CFLAME: FLAME, lights.WICK: IRON, lights.BONE: WAX}
    for f in set(bm.faces) - before:
        f.material_index = remap[f.material_index]
    return make("GraveChest", bm, MATS, at=at, smooth=None)


ASSETS = {
    "GraveChest": grave_chest,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
