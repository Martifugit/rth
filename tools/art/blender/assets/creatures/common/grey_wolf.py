r"""The grey wolf (2026-11-01): the first mammal built by tools/art/blender/creatures.md.

What should read, in order: a level back (the top of the haunch as high as the withers); a deep but
narrow chest, keeled, over a tucked waist; a short thick neck carrying the head just above the back;
a wedge skull with a muzzle as long as itself and upright ears; straight forelegs with the elbow
tucked behind; hind legs in a clear zigzag (thigh, stifle forward, hock back); a tail hanging low.

Proportions from the player (1.8 m) and a real wolf: the withers 0.84 m (under the player's hip),
nose to rump about 1.5 m, the chest 0.3 m across but 0.4 m deep, the tail about 0.45 m.

One part, the left half (+X) with a Mirror modifier; ears, paws, toes and tail grown out of the body.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\grey_wolf.py")
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
from artlib.beast import keep_left, paw, symmetric, to_object, tune, tuning  # noqa: E402
from artlib.grow import Grow  # noqa: E402

COLOURS = ["Ashgrey", "Sable"]              # untextured; one colour a part until the topology is passed
FUR, DARK = range(2)


def body():
    g = Grow(sides=12)
    torso = g.tube([
        (-0.48, 0, 0.7, 0.11, 0.1, 0.1),             # the rump, where the tail leaves
        (-0.38, 0, 0.69, 0.14, 0.13, 0.16),          # the haunches: top level with the withers
        (-0.25, 0, 0.68, 0.125, 0.12, 0.14),
        (-0.12, 0, 0.7, 0.115, 0.11, 0.1),           # the waist, tucked up underneath
        (0.02, 0, 0.68, 0.135, 0.13, 0.17),          # the ribs
        (0.16, 0, 0.66, 0.15, 0.15, 0.22),           # the chest, deep and narrow: a keel
        (0.28, 0, 0.67, 0.15, 0.16, 0.21),           # the shoulders
        (0.37, 0, 0.71, 0.135, 0.135, 0.17),         # the withers
        (0.45, 0, 0.76, 0.125, 0.12, 0.14),          # the neck's root, thick with the ruff
    ], FUR)
    # the neck, short and thick; the head broad, carried just above the back
    g.extrude(torso.caps["end"], [
        (0.52, 0, 0.82, 0.118, 0.11, 0.12),
        (0.59, 0, 0.88, 0.11, 0.1, 0.11),
        (0.66, 0, 0.92, 0.1, 0.085, 0.09),           # the back of the skull
        (0.73, 0, 0.93, 0.105, 0.09, 0.09),          # the cheeks and brow, widest
        (0.8, 0, 0.895, 0.075, 0.055, 0.075),        # the stop: the brow drops to the muzzle
        (0.88, 0, 0.88, 0.052, 0.045, 0.058),        # the muzzle, flat-topped
        (0.95, 0, 0.865, 0.04, 0.034, 0.044),
        (1.0, 0, 0.858, 0.03, 0.026, 0.032),
    ], FUR, tip=(1.035, 0, 0.855), blunt=0.01)
    # the tail, bushy, hanging low
    g.extrude(torso.caps["start"], [
        (-0.54, 0, 0.67, 0.05, 0.05, 0.05), (-0.6, 0, 0.6, 0.058, 0.058, 0.058),
        (-0.64, 0, 0.52, 0.062, 0.062, 0.062), (-0.665, 0, 0.44, 0.056, 0.056, 0.056),
        (-0.68, 0, 0.36, 0.04, 0.04, 0.04)], FUR, tip=(-0.69, 0, 0.3), blunt=0.012)
    s = 1                                         # the left side only: symmetric() copies it over
    # the forelegs: the upper arm back to the elbow tucked behind, the forearm straight down, a ring
    # either side of the elbow and the wrist; grown from four faces, so the paw gets four toes
    fore = g.patch([(0.32, s * 0.09, 0.5), (0.22, s * 0.1, 0.5), (0.32, s * 0.11, 0.56), (0.22, s * 0.12, 0.56)],
                   (0, s * 0.5, -1))
    wrist = g.extrude(fore, [
        (0.27, s * 0.12, 0.44, 0.055, 0.07, 0.07),   # the upper arm
        (0.21, s * 0.12, 0.36, 0.045, 0.05, 0.05),   # the elbow, behind
        (0.23, s * 0.12, 0.29, 0.038, 0.04, 0.04),
        (0.24, s * 0.12, 0.17, 0.032, 0.032, 0.032),  # the forearm, straight down
        (0.245, s * 0.12, 0.1, 0.03, 0.03, 0.03),    # the wrist
        (0.26, s * 0.12, 0.06, 0.03, 0.028, 0.028)], FUR)   # the pastern, a little forward
    paw(g, wrist, 0.29, s * 0.12, s, FUR, DARK)
    # the hind legs: a big thigh, the stifle forward, the hock back, the long foot down and forward
    hind = g.patch([(-0.33, s * 0.12, 0.6), (-0.43, s * 0.11, 0.6), (-0.33, s * 0.13, 0.66), (-0.43, s * 0.12, 0.66)],
                   (0, s * 0.6, -1))
    ankle = g.extrude(hind, [
        (-0.31, s * 0.14, 0.52, 0.065, 0.09, 0.09),  # the thigh
        (-0.23, s * 0.14, 0.38, 0.045, 0.05, 0.05),  # the stifle, forward
        (-0.33, s * 0.14, 0.25, 0.035, 0.035, 0.035),
        (-0.39, s * 0.14, 0.16, 0.03, 0.03, 0.03),   # the hock, back
        (-0.37, s * 0.14, 0.06, 0.028, 0.028, 0.028)], FUR)   # the foot, down and a little forward
    paw(g, ankle, -0.34, s * 0.14, s, FUR, DARK)
    # the ears: upright, flat, out of the top of the skull
    ear = g.inset(g.face((0.69, s * 0.055, 1.0), (0, s * 0.4, 1)), 0.05)
    g.extrude([ear], [(0.685, s * 0.07, 1.035, 0.025, 0.009, 0.009)], FUR, tip=(0.68, s * 0.08, 1.085), blunt=0.004)
    symmetric(g.bm)
    return g


def build(name="GreyWolf", coll="Creatures"):
    g = body()
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, "GreyWolf"))
    return to_object(g.bm, name, COLOURS, coll, clip=True)


ASSETS = {"GreyWolf": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
