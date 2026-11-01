r"""The fen rat, grown (2026-11-01): a big reed-bed rat, knee-high to the player, by
tools/art/blender/creatures.md and beast.quadruped. Exported as FenRatGrown beside the old FenRat
until the owner passes it.

What should read: a hunched back arching highest over the middle; a long pointed snout carried low;
small round ears; short legs tucked under, the hind ones with a big haunch; a long, thin, bare tail
trailing on the ground.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\rat.py")
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

COLOURS = ["Peat", "Sable"]                  # untextured, one colour a part until the topology is passed
FUR, CLAW = range(2)

RAT = dict(
    torso=[
        (-0.3, 0, 0.24, 0.09, 0.08, 0.08),           # the rump, where the tail leaves
        (-0.2, 0, 0.28, 0.14, 0.13, 0.13),           # the haunches
        (-0.06, 0, 0.3, 0.14, 0.14, 0.13),           # the back, hunched highest over the middle
        (0.08, 0, 0.27, 0.12, 0.12, 0.12),           # the chest
        (0.17, 0, 0.23, 0.09, 0.09, 0.09)],          # the shoulders: the neck hardly there
    head=[
        (0.24, 0, 0.21, 0.08, 0.075, 0.075),
        (0.31, 0, 0.2, 0.075, 0.065, 0.065),         # the skull
        (0.38, 0, 0.18, 0.055, 0.05, 0.05),
        (0.44, 0, 0.16, 0.038, 0.035, 0.035),        # the snout, long and pointed
        (0.49, 0, 0.145, 0.025, 0.022, 0.022)],
    nose=((0.53, 0, 0.135), 0.012),
    tail=[(-0.36, 0, 0.21, 0.04, 0.04, 0.04), (-0.46, 0, 0.16, 0.03, 0.03, 0.03),
          (-0.58, 0, 0.11, 0.024, 0.024, 0.024), (-0.72, 0, 0.08, 0.018, 0.018, 0.018),
          (-0.86, 0, 0.06, 0.013, 0.013, 0.013), (-0.98, 0, 0.05, 0.009, 0.009, 0.009)],
    tail_tip=((-1.08, 0, 0.045), 0.004),
    fore_patch=[(0.12, 0.08, 0.18), (0.04, 0.09, 0.18), (0.12, 0.1, 0.22), (0.04, 0.11, 0.22)],
    fore=[(0.1, 0.11, 0.13, 0.035, 0.04, 0.04),      # the upper arm
          (0.08, 0.11, 0.09, 0.028, 0.03, 0.03),     # the elbow
          (0.11, 0.1, 0.05, 0.022, 0.022, 0.022),    # the wrist
          (0.12, 0.1, 0.025, 0.02, 0.018, 0.018)],
    fore_paw=(0.14, 0.1),
    hind_patch=[(-0.15, 0.1, 0.2), (-0.25, 0.09, 0.2), (-0.15, 0.12, 0.25), (-0.25, 0.11, 0.25)],
    hind=[(-0.16, 0.14, 0.17, 0.05, 0.07, 0.07),     # the haunch
          (-0.1, 0.14, 0.1, 0.035, 0.035, 0.035),    # the stifle, forward
          (-0.16, 0.13, 0.05, 0.026, 0.026, 0.026),  # the hock, back
          (-0.13, 0.13, 0.025, 0.022, 0.02, 0.02)],
    hind_paw=(-0.09, 0.13),
    paw_size=0.45,
    hind_paw_size=0.55,
    ear=dict(at=(0.3, 0.035, 0.26), keep=0.04, rings=[(0.295, 0.06, 0.29, 0.035, 0.012, 0.012)],
             tip=(0.29, 0.07, 0.31), blunt=0.02),   # small and round
)


def build(name="FenRatGrown", coll="Creatures"):
    g = quadruped(RAT, FUR, CLAW)
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, name))
    return to_object(g.bm, name, COLOURS, coll, clip=True)


ASSETS = {"FenRatGrown": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
