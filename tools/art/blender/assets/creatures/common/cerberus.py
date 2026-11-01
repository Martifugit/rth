r"""Cerberus, the three-headed hound, grown as one mesh (artlib/grow.py): the wolf's method on a
heavier body, with three necks rising out of a broad chest (the middle one highest), a serpent's
tail curling up behind and a ridge of spines down the back. The heads are the wolf's (wolf.head).

Proportions: half again a wolf (about 1.6 m to the middle head), a chest much broader than the
haunches so three necks fit it, thick legs and big paws.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\cerberus.py")
"""
import math
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

from artlib import family  # noqa: E402
from artlib.grow import Grow  # noqa: E402
from assets.creatures.common import wolf  # noqa: E402

COLOURS = ["Charcoal", "Cinder", "Sable", "Ember", "Blood"]
FUR, MUZZLE, DARK, EYE, SPINE = range(5)
SIZE = 1.5
HEAD = 1.35


def cerberus():
    g = Grow(sides=12)
    body = g.tube([
        (-0.5, 0, 0.7, 0.1, 0.09, 0.09),
        (-0.4, 0, 0.71, 0.16, 0.14, 0.14),          # the haunches
        (-0.2, 0, 0.69, 0.14, 0.13, 0.13),          # the loin
        (0.02, 0, 0.7, 0.18, 0.16, 0.2),            # the ribs
        (0.2, 0, 0.72, 0.22, 0.19, 0.24),           # the chest, broad and deep
        (0.34, 0, 0.75, 0.2, 0.17, 0.18),           # the breast
    ], FUR)
    chest = g.band(body, 4)                         # round the chest from the left: 0 left, 3 up, 6 right
    spines = [g.band(body, i)[2] for i in (1, 2, 3)]
    mats = (FUR, MUZZLE, DARK)
    heads = [(0.0, wolf.head(g, [chest[1], chest[2], chest[3]], (0.3, 0, 0.85), 0.0, HEAD, mats=mats,
                       neck=[(0.1, 0.12, 0.13, 0.12, 0.12), (0.18, 0.2, 0.12, 0.1, 0.1)]))]
    for s, faces in ((1, [chest[11], chest[0]]), (-1, [chest[4], chest[5]])):
        heads.append((s * 0.6, wolf.head(g, faces, (0.28, s * 0.17, 0.72), s * 0.6, HEAD, mats=mats,
                               neck=[(0.12, 0.05, 0.12, 0.11, 0.11), (0.2, 0.09, 0.11, 0.1, 0.1)])))
    for s in (1, -1):
        fore = g.faces([(0.08, s * 0.16, 0.52), (0.24, s * 0.17, 0.5)], (0, s * 0.4, -1))
        wrist = g.extrude(fore, [
            (0.18, s * 0.16, 0.42, 0.09, 0.09, 0.09),
            (0.17, s * 0.16, 0.25, 0.065, 0.065, 0.065),
            (0.19, s * 0.16, 0.09, 0.055, 0.055, 0.055)], FUR)
        g.extrude(wrist, [(0.28, s * 0.16, 0.04, 0.075, 0.045, 0.04)], FUR)        # the paw
        hind = g.faces([(-0.46, s * 0.14, 0.62), (-0.3, s * 0.15, 0.6)], (0, s * 0.4, -1))
        hock = g.extrude(hind, [
            (-0.3, s * 0.16, 0.42, 0.09, 0.12, 0.1),       # the thigh
            (-0.4, s * 0.16, 0.25, 0.06, 0.065, 0.065),
            (-0.42, s * 0.15, 0.13, 0.05, 0.05, 0.05),     # the hock
            (-0.39, s * 0.15, 0.07, 0.05, 0.05, 0.05)], FUR)
        g.extrude(hock, [(-0.31, s * 0.15, 0.04, 0.07, 0.045, 0.04)], FUR)
    # a serpent's tail, lifted and curling down at the end, darker toward the tip
    g.extrude(body.caps["start"], [
        (-0.6, 0, 0.75, 0.065, 0.065, 0.065), (-0.74, 0, 0.86, 0.055, 0.055, 0.055),
        (-0.88, 0, 0.9, 0.048, 0.048, 0.048), (-1.0, 0, 0.84, 0.04, 0.04, 0.04),
        (-1.06, 0, 0.72, 0.032, 0.032, 0.032)], FUR, tip=(-1.07, 0, 0.6))
    for f in spines:                                # a ridge of spines down the back, raked back
        c = f.calc_center_median()
        fwd, up = -c.y, c.z
        g.extrude([f], [(fwd, 0, up + 0.03, 0.035, 0.03, 0.03)], SPINE, tip=(fwd - 0.07, 0, up + 0.14))
    for f in g.bm.faces:
        c = f.calc_center_median()
        if -c.y < -0.95:
            f.material_index = DARK
    for yaw, at in heads:                          # ember eyes, under the brow
        for s in (1, -1):
            g.paint(at(0.205, s * 0.055, 0.16), (math.cos(yaw), math.sin(yaw), 0.0), EYE, size=0.6)
    for v in g.bm.verts:
        v.co *= SIZE
    return g


def build(name="Cerberus", coll="Creatures"):
    return cerberus().make(name, COLOURS, coll=coll)


ASSETS = {"Cerberus": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
