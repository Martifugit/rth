r"""The burdened, a shade (variant C of four for the owner to pick): one who couldn't let go of what they
carried, and so never reached the boat. Bent nearly double under a great dark bundle lashed to its back
with iron, a ring of keys swinging from it, the arms hanging slack toward the water and the legs long
gone to a wisp that drags behind. What it carries is solid and dark, the one real thing left; the body
under it is soul-stuff. The ferryman's hold is full of such things.

What must read, in order: the huge dark burden; the pale body bent double under it; the slack arms.
About 1.75 m to the bundle's top, the back level at 1.2 m. Grown as one mesh (artlib/grow.py); the
bundle is a hull sunk into the back, the lashing and the keys rods and hulls on it. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\shade_burdened.py")
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

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.grow import P, Grow  # noqa: E402
import artlib.nature as N  # noqa: E402

COLOURS = ["SoulPale", "SoulGlow", "Hide", "Blackiron"]
PALE, GLOW, PACK, IRON = range(4)

BACK = 1.2                      # the bent back's line; the bundle sits on it
STRAP_S = 0.15                  # how far either side of the middle the straps come down to the fists
PACK_AT = (-0.04, BACK + 0.46)   # (f, u) the bundle's middle
PACK_SIZE = (0.3, 0.38, 0.32)  # its half sizes: along the back, across, up


def burdened():
    g = Grow(sides=12)
    # the body, from the wisp dragging behind up through the hips and along the bent back to the head:
    # rings (f, s, u, half-width, above, below)
    g.tube([
        (-0.55, 0, 0.04, 0.02, 0.015, 0.015),     # the wisp's end, dragging
        (-0.4, 0, 0.12, 0.06, 0.04, 0.04),
        (-0.25, 0, 0.32, 0.12, 0.09, 0.09),
        (-0.12, 0, 0.6, 0.17, 0.13, 0.13),        # what was the legs, drawn into one
        (-0.04, 0, 0.88, 0.2, 0.15, 0.15),        # the hips
        (0.08, 0, BACK - 0.1, 0.22, 0.16, 0.16),  # the small of the back, bending over
        (0.24, 0, BACK + 0.08, 0.23, 0.16, 0.16),  # the back, bowed under the load
        (0.38, 0, BACK + 0.16, 0.25, 0.13, 0.16),  # the shoulders
        (0.5, 0, BACK + 0.14, 0.08, 0.07, 0.07),  # the neck, bowed
        (0.6, 0, BACK + 0.08, 0.1, 0.1, 0.1),     # the head, hanging, eyes on the ground
        (0.68, 0, BACK - 0.01, 0.1, 0.1, 0.1),
        (0.72, 0, BACK - 0.08, 0.05, 0.05, 0.05),
    ], PALE)
    # the arms: down from the shoulders and up again, the fists clutching the straps at the chest
    for s in (1, -1):
        root = g.patch([(0.38, s * 0.25, BACK + 0.1), (0.32, s * 0.24, BACK + 0.02)], (0, s, 0))
        wrist = g.extrude(root, [
            (0.42, s * 0.31, BACK + 0.06, 0.07, 0.07, 0.07),
            (0.5, s * 0.33, BACK - 0.24, 0.05, 0.055, 0.055),    # the elbow, low and out
            (0.56, s * 0.2, BACK - 0.06, 0.04, 0.045, 0.045),    # the wrist, back up to the chest
        ], PALE)
        g.extrude(wrist, [(0.56, s * STRAP_S, BACK, 0.045, 0.05, 0.05)], GLOW,
                  tip=(0.55, s * (STRAP_S - 0.02), BACK + 0.07), blunt=0.015)
    g.smooth(0.2, repeat=1)
    for f in g.bm.faces:                         # the wisp brightens as it thins
        if f.material_index == PALE and f.calc_center_median().z < 0.5:
            f.material_index = GLOW
    bm = g.bm
    # the bundle: a great lumpy sack sunk into the back, wider than the body, bulging over the sides
    pf, pu = PACK_AT
    lx, ly, lz = PACK_SIZE
    pts = []
    for k in range(10):
        a = 2 * math.pi * k / 10
        for zf, r in ((-1.0, 0.7), (-0.4, 1.0), (0.35, 0.95), (0.9, 0.55), (1.15, 0.22)):   # tied at the top
            bulge = 1 + 0.14 * math.cos(3 * a + 2 * zf) + 0.06 * math.cos(5 * a)            # lumps of what's inside, not random
            pts.append(P(pf + lx * r * bulge * math.cos(a), ly * r * bulge * math.sin(a), pu + lz * zf))
    A.hull(bm, pts, mat=PACK)
    # a smaller bundle heaped on top, askew, sunk into the sack: what wouldn't fit
    tf, ts, tu = pf - 0.06, 0.07, pu + lz * 0.95
    pts = []
    for k in range(8):
        a = 2 * math.pi * k / 8
        for zf, r in ((-0.6, 0.8), (0.0, 1.0), (0.6, 0.7)):
            pts.append(P(tf + 0.2 * r * math.cos(a) + 0.05 * zf, ts + 0.24 * r * math.sin(a), tu + 0.15 * zf + 0.04 * math.sin(a)))
    A.hull(bm, pts, mat=PACK)
    # the straps: iron, from the sack's top over each shoulder and down to the fists that hold them
    for s in (1, -1):
        N.rod(bm, [P(pf + 0.1, s * 0.16, pu + lz * 0.7), P(pf + lx * 0.8, s * 0.2, pu + 0.04),
                   P(0.44, s * 0.2, BACK + 0.22), P(0.54, s * STRAP_S, BACK + 0.06)], 0.02, mat=IRON, sides=4)
    # a band round the sack's middle holding it together
    ring = [P(pf + (lx * 1.1 + 0.01) * math.cos(2 * math.pi * k / 10), (ly * 1.1 + 0.01) * math.sin(2 * math.pi * k / 10), pu - 0.12)
            for k in range(11)]
    N.rod(bm, ring, 0.02, mat=IRON, sides=4)
    # the keys: an iron ring hanging from the sack's side, three keys on it
    kf, ks, kz = pf + 0.18, -ly - 0.01, pu - 0.18
    N.rod(bm, [P(kf + 0.06 * math.cos(2 * math.pi * k / 8), ks - 0.02, kz + 0.06 * math.sin(2 * math.pi * k / 8))
               for k in range(9)], 0.008, mat=IRON, sides=4)
    for k, (dx, ln) in enumerate(((-0.035, 0.16), (0.0, 0.2), (0.035, 0.13))):
        x0, z0 = kf + dx, kz - 0.05
        N.rod(bm, [P(x0, ks - 0.02, z0), P(x0, ks - 0.02, z0 - ln)], 0.009, mat=IRON, sides=4)
        A.hull(bm, [P(x0 - 0.02, ks - 0.02, z0 - ln + 0.02), P(x0 + 0.02, ks - 0.02, z0 - ln + 0.02),
                    P(x0 - 0.02, ks - 0.03, z0 - ln - 0.03), P(x0 + 0.02, ks - 0.01, z0 - ln - 0.03)], mat=IRON)
    return g


def build(name="ShadeBurdened", coll="Creatures"):
    return burdened().make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"ShadeBurdened": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
