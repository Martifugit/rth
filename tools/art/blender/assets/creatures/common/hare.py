r"""The hare (2026-11-01), by tools/art/blender/creatures.md and beast.quadruped.

What should read: sitting up alert; big round haunches folded low with long hind feet flat on the
ground; the back curving up to the shoulders; long thin forelegs straight down; the head raised, a
short blunt muzzle; tall ears standing up and a little back; a short tail turned up.

A large hare: the top of the head about 0.45 m, the ears to about 0.7 m.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\hare.py")
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
from artlib.beast import keep_left, quadruped, to_object, tune, tuning  # noqa: E402

COLOURS = ["Dust", "Sable"]                  # untextured, one colour a part until the topology is passed
FUR, CLAW = range(2)

HARE = dict(
    torso=[
        (-0.2, 0, 0.18, 0.08, 0.07, 0.07),           # the rump, where the tail leaves
        (-0.13, 0, 0.2, 0.15, 0.13, 0.13),           # the haunches, big and round: the thighs
        (-0.02, 0, 0.25, 0.12, 0.12, 0.11),          # the back, curving up
        (0.07, 0, 0.31, 0.1, 0.1, 0.1),              # the chest
        (0.12, 0, 0.37, 0.075, 0.075, 0.075)],       # the neck's root
    head=[
        (0.15, 0, 0.42, 0.065, 0.065, 0.065),
        (0.19, 0, 0.45, 0.06, 0.06, 0.058),          # the back of the skull
        (0.24, 0, 0.45, 0.055, 0.05, 0.05),          # the cheeks
        (0.29, 0, 0.43, 0.042, 0.038, 0.04),         # the muzzle, short and blunt
        (0.32, 0, 0.415, 0.03, 0.028, 0.03)],
    nose=((0.345, 0, 0.405), 0.014),
    tail=[(-0.24, 0, 0.2, 0.04, 0.04, 0.04), (-0.27, 0, 0.22, 0.035, 0.035, 0.035)],
    tail_tip=((-0.28, 0, 0.25), 0.02),               # a short puff, turned up
    fore_patch=[(0.1, 0.05, 0.23), (0.04, 0.06, 0.23), (0.1, 0.07, 0.27), (0.04, 0.08, 0.27)],
    fore=[(0.08, 0.075, 0.18, 0.03, 0.035, 0.035),   # the upper arm
          (0.075, 0.07, 0.13, 0.022, 0.022, 0.022),  # the elbow
          (0.08, 0.065, 0.07, 0.017, 0.017, 0.017),  # the foreleg, long and thin
          (0.085, 0.065, 0.03, 0.016, 0.015, 0.015)],
    fore_paw=(0.1, 0.065),
    hind_patch=[(-0.08, 0.12, 0.14), (-0.17, 0.11, 0.14), (-0.08, 0.13, 0.19), (-0.17, 0.12, 0.19)],
    hind_patch_toward=(0, 1, -0.6),
    # the thigh is the haunch itself (the torso's big round ring): the leg is the shin and the foot,
    # with one hairpin, at the hock, rounded by two rings
    hind=[(-0.1, 0.15, 0.11, 0.05, 0.055, 0.055),   # the knee, under the haunch
          (-0.16, 0.145, 0.06, 0.03, 0.03, 0.03),    # the shin, back and down
          (-0.2, 0.12, 0.03, 0.024, 0.024, 0.024),   # the hock: two rings round the hairpin
          (-0.17, 0.095, 0.02, 0.022, 0.018, 0.018),
          (-0.1, 0.085, 0.02, 0.022, 0.017, 0.017)],  # the long foot, flat, inside the shin
    hind_paw=(-0.03, 0.085),
    paw_size=0.35,
    hind_paw_size=0.5,
    ear=dict(at=(0.2, 0.03, 0.5), keep=0.045,
             rings=[(0.195, 0.035, 0.55, 0.03, 0.01, 0.01), (0.18, 0.045, 0.62, 0.032, 0.009, 0.009),
                    (0.16, 0.05, 0.68, 0.022, 0.007, 0.007)],
             tip=(0.14, 0.05, 0.72), blunt=0.006),  # tall, standing up and a little back
)


def build(name="Hare", coll="Creatures"):
    g = quadruped(HARE, FUR, CLAW)
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, name))
    return to_object(g.bm, name, COLOURS, coll, clip=True)


ASSETS = {"Hare": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
