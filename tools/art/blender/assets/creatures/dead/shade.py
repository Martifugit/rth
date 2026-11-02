r"""The shade, a soul of the dead that drifts along the long way back and can't be killed: a hollow
hood over a body of pale soul-stuff that narrows to a wisp trailing on the ground behind it, no legs,
the arms two sheets trailing forward and down. Grown as one mesh (artlib/grow.py), like the goblin.

What must read, in order: the dark empty hood; the drifting, legless taper; the reaching sheets.
About 2.1 m to the hood's peak, a head taller than the player. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\shade.py")
"""
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

COLOURS = ["SoulPale", "SoulGlow", "Sable"]
PALE, GLOW, VOID = range(3)

HOOD = 1.62                     # the hood's bottom (the shoulders' top); the hood's rings follow from it


def shade():
    g = Grow(sides=12)
    # the body, upright, from the wisp's end lying on the ground behind up to the hood's root:
    # rings (f, s, u, half-width, in front, behind)
    g.tube([
        (-0.62, 0, 0.05, 0.03, 0.03, 0.03),    # the wisp's end, trailing on the ground
        (-0.44, 0, 0.13, 0.06, 0.05, 0.05),
        (-0.24, 0, 0.32, 0.12, 0.1, 0.1),
        (-0.08, 0, 0.6, 0.19, 0.15, 0.15),     # the robe, swelling slowly
        (0.02, 0, 0.94, 0.25, 0.18, 0.18),
        (0.08, 0, 1.2, 0.29, 0.19, 0.2),
        (0.13, 0, 1.42, 0.33, 0.19, 0.22),     # the shoulders, leaning into the drift
        (0.17, 0, HOOD - 0.06, 0.24, 0.17, 0.2),
        (0.21, 0, HOOD + 0.06, 0.21, 0.21, 0.21),  # the hood, deep, hung forward
        (0.24, 0, HOOD + 0.2, 0.22, 0.22, 0.21),
        (0.22, 0, HOOD + 0.34, 0.2, 0.2, 0.2),
        (0.13, 0, HOOD + 0.44, 0.14, 0.12, 0.15),  # the hood's crown, swept back
        (0.0, 0, HOOD + 0.5, 0.05, 0.04, 0.05),    # its point
    ], PALE, upright=True)
    # the hood's opening: the front faces between the hood's rings pushed deep into it, all void
    mouth = g.patch([(0.45, 0, HOOD + 0.13), (0.45, 0.07, HOOD + 0.13), (0.45, -0.07, HOOD + 0.13),
                     (0.45, 0, HOOD + 0.27), (0.45, 0.07, HOOD + 0.27), (0.45, -0.07, HOOD + 0.27)], (1, 0, 0))
    g.extrude(mouth, [(0.38, 0, HOOD + 0.19, 0.12, 0.13, 0.11),          # the rim, turned in
                      (0.22, 0, HOOD + 0.19, 0.09, 0.1, 0.08)], VOID)    # the empty dark inside
    # the arms: sheets of soul hanging from the shoulders, trailing forward and down
    for s in (1, -1):
        root = g.patch([(0.13, s * 0.33, 1.32), (0.1, s * 0.31, 1.1)], (0, s, 0))
        g.extrude(root, [
            (0.24, s * 0.4, 1.26, 0.09, 0.1, 0.09),       # the shoulder's fall
            (0.46, s * 0.42, 1.08, 0.05, 0.12, 0.1),      # flattening into a sheet, reaching
            (0.66, s * 0.38, 0.86, 0.03, 0.12, 0.08),
            (0.8, s * 0.33, 0.66, 0.02, 0.08, 0.05),
        ], PALE, tip=(0.88, s * 0.3, 0.46))
    g.smooth(0.25, repeat=1)
    for f in g.bm.faces:                                # the soul brightens toward the wisp and the arms' ends
        c = f.calc_center_median()
        if f.material_index == PALE and (c.z < 0.42 or (-c.y > 0.6 and c.z < 0.95)):
            f.material_index = GLOW
    return g


def build(name="Shade", coll="Creatures"):
    return shade().make(name, COLOURS, coll=coll, smooth_angle=70)


ASSETS = {"Shade": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
