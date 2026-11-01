r"""The goblin, grown as one mesh (artlib/grow.py): an upright trunk from the hips to the neck, the
legs out of the two halves of its bottom, the head out of its top, the arms out of the shoulders,
the ears and the nose out of the head's faces.

Proportions: about 1.2 m, a third of it head (the 2004 goblin is 1.23 m with a head as wide as its
shoulders); short bowed legs, a pot belly, a hunch, arms long enough to reach the knees, a hooked
nose and ears as wide as the head.

    import runpy; runpy.run_path(r"C:\dev
th	oolsrtlenderssets\creatures\common\goblin.py")
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
from artlib.grow import P, Grow  # noqa: E402

COLOURS = ["Moss", "Hide", "Sulphur", "Charcoal"]
SKIN, CLOTH, EYE, DARK = range(4)


def goblin():
    g = Grow(sides=12)
    # the trunk, upright: rings (f, s, u, half-width, in front, behind) from the hips to the neck
    trunk = g.tube([
        (0.0, 0, 0.42, 0.13, 0.1, 0.09),      # the hips
        (0.02, 0, 0.54, 0.145, 0.14, 0.1),    # the pot belly
        (0.03, 0, 0.67, 0.16, 0.12, 0.11),    # the chest, leaning forward
        (0.03, 0, 0.77, 0.21, 0.09, 0.14),    # the shoulders, hunched: more behind than in front
        (0.09, 0, 0.82, 0.1, 0.07, 0.08),     # the root of the neck
    ], SKIN, upright=True, split_caps=True)
    for s in (1, -1):
        hip = g.nearest(trunk.caps["start"], (0, s * 0.08, 0.42))
        ankle = g.extrude([hip], [
            (0.0, s * 0.09, 0.33, 0.08, 0.08, 0.08),       # the thigh
            (0.07, s * 0.1, 0.23, 0.065, 0.065, 0.06),     # the knee, forward and out: bowed
            (0.01, s * 0.1, 0.11, 0.055, 0.055, 0.05),     # the shin
            (-0.01, s * 0.1, 0.05, 0.05, 0.05, 0.05)], SKIN)
        g.extrude(ankle, [(0.09, s * 0.1, 0.035, 0.06, 0.035, 0.03),       # the foot, long and flat
                          (0.15, s * 0.105, 0.03, 0.055, 0.025, 0.028)], SKIN)
        shoulder = g.faces([(0.02, s * 0.18, 0.72), (0.02, s * 0.18, 0.62)], (0, s, 0))
        wrist = g.extrude(shoulder, [
            (0.04, s * 0.25, 0.73, 0.065, 0.07, 0.07),    # the shoulder
            (0.03, s * 0.28, 0.54, 0.05, 0.05, 0.05),     # the elbow
            (0.1, s * 0.28, 0.4, 0.04, 0.04, 0.04)], SKIN)     # the wrist
        g.extrude(wrist, [(0.115, s * 0.28, 0.36, 0.055, 0.06, 0.05), (0.12, s * 0.28, 0.3, 0.06, 0.065, 0.055),
                          (0.12, s * 0.28, 0.26, 0.04, 0.045, 0.04)], SKIN)   # the fist, big and blunt
    # the head out of the neck: a heavy jaw, wide cheeks, a low brow, a small crown
    g.extrude(trunk.caps["end"], [
        (0.15, 0, 0.86, 0.075, 0.07, 0.07),   # the neck, thrust forward
        (0.2, 0, 0.91, 0.13, 0.12, 0.1),      # the jaw
        (0.21, 0, 1.0, 0.16, 0.13, 0.12),     # the cheeks and eyes
        (0.2, 0, 1.08, 0.16, 0.13, 0.13),     # the brow, heavy
        (0.18, 0, 1.15, 0.13, 0.11, 0.12),    # the dome
        (0.17, 0, 1.18, 0.08, 0.07, 0.08),    # the crown, flat
    ], SKIN)
    g.extrude([g.face((0.34, 0, 1.0), (1, 0, 0))], [(0.39, 0, 0.98, 0.03, 0.03, 0.02)], SKIN,
              tip=(0.42, 0, 0.93))                                   # the nose, hooked down
    for s in (1, -1):
        g.extrude([g.face((0.2, s * 0.16, 1.05), (0, s, 0.2))],      # the ears, flat, out, up and back
                  [(0.18, s * 0.23, 1.08, 0.02, 0.06, 0.035)], SKIN, tip=(0.12, s * 0.34, 1.16))
    for f in g.bm.faces:                                           # a loincloth round the hips
        c = f.calc_center_median()
        if 0.34 < c.z < 0.5 and abs(c.x) < 0.2:
            f.material_index = CLOTH
    for s in (1, -1):
        g.paint((0.33, s * 0.075, 1.04), (1, 0, 0.1), EYE, size=0.6)
    return g


def build(name="Goblin", coll="Creatures"):
    return goblin().make(name, COLOURS, coll=coll)


ASSETS = {"Goblin": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
