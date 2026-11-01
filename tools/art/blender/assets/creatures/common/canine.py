r"""Canines (2026-11-01): one builder, a spec of numbers per breed, built by tools/art/blender/creatures.md.

Every canine is the same construction (beast.quadruped): one closed mesh grown from a body tube; the neck and head out
of its front, the tail out of its back, the legs from 2x2 patches into four-toed paws (beast.paw), the
ears out of the skull. A breed is only its numbers: the rings of the torso, head and tail, the legs'
joints, the ears, the paws.

- StrayDog  slender and light, the head carried up above the back, a hanging tail: lean, a little
            friendly (the first mammal; the owner read it as a dog)
- Wolf      bigger and heavier: a broad deep chest, raised hackles over the neck and shoulders, the
            head carried low and forward in a stalking line, a broad skull and heavy blunt muzzle,
            big pricked ears, thick legs on big paws, a thick tail held low and straight out

Proportions from the player (1.8 m): the stray's withers 0.84 m, the wolf's 0.92 m.

Each is one part, the left half (+X) with a Mirror modifier.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\canine.py")
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

FUR, DARK = range(2)

STRAY_DOG = dict(
    colours=["Ashgrey", "Sable"],
    torso=[
        (-0.48, 0, 0.7, 0.11, 0.1, 0.1),             # the rump, where the tail leaves
        (-0.38, 0, 0.69, 0.14, 0.13, 0.16),          # the haunches: top level with the withers
        (-0.25, 0, 0.68, 0.125, 0.12, 0.14),
        (-0.12, 0, 0.7, 0.115, 0.11, 0.1),           # the waist, tucked up underneath
        (0.02, 0, 0.68, 0.135, 0.13, 0.17),          # the ribs
        (0.16, 0, 0.66, 0.15, 0.15, 0.22),           # the chest, deep and narrow: a keel
        (0.28, 0, 0.67, 0.15, 0.16, 0.21),           # the shoulders
        (0.37, 0, 0.71, 0.135, 0.135, 0.17),         # the withers
        (0.45, 0, 0.76, 0.125, 0.12, 0.14)],         # the neck's root
    head=[
        (0.52, 0, 0.82, 0.118, 0.11, 0.12),          # the neck, short and thick
        (0.59, 0, 0.88, 0.11, 0.1, 0.11),
        (0.66, 0, 0.92, 0.1, 0.085, 0.09),           # the back of the skull
        (0.73, 0, 0.93, 0.105, 0.09, 0.09),          # the cheeks and brow, widest
        (0.8, 0, 0.895, 0.075, 0.055, 0.075),        # the stop: the brow drops to the muzzle
        (0.88, 0, 0.88, 0.052, 0.045, 0.058),        # the muzzle, flat-topped
        (0.95, 0, 0.865, 0.04, 0.034, 0.044),
        (1.0, 0, 0.858, 0.03, 0.026, 0.032)],
    nose=((1.035, 0, 0.855), 0.01),
    tail=[(-0.54, 0, 0.67, 0.05, 0.05, 0.05), (-0.6, 0, 0.6, 0.058, 0.058, 0.058),
          (-0.64, 0, 0.52, 0.062, 0.062, 0.062), (-0.665, 0, 0.44, 0.056, 0.056, 0.056),
          (-0.68, 0, 0.36, 0.04, 0.04, 0.04)],
    tail_tip=((-0.69, 0, 0.3), 0.012),
    fore_patch=[(0.32, 0.09, 0.5), (0.22, 0.1, 0.5), (0.32, 0.11, 0.56), (0.22, 0.12, 0.56)],
    fore=[(0.27, 0.12, 0.44, 0.055, 0.07, 0.07),     # the upper arm
          (0.21, 0.12, 0.36, 0.045, 0.05, 0.05),     # the elbow, behind
          (0.23, 0.12, 0.29, 0.038, 0.04, 0.04),
          (0.24, 0.12, 0.17, 0.032, 0.032, 0.032),   # the forearm, straight down
          (0.245, 0.12, 0.1, 0.03, 0.03, 0.03),      # the wrist
          (0.26, 0.12, 0.06, 0.03, 0.028, 0.028)],   # the pastern, a little forward
    fore_paw=(0.29, 0.12),
    hind_patch=[(-0.33, 0.12, 0.6), (-0.43, 0.11, 0.6), (-0.33, 0.13, 0.66), (-0.43, 0.12, 0.66)],
    hind=[(-0.31, 0.14, 0.52, 0.065, 0.09, 0.09),    # the thigh
          (-0.23, 0.14, 0.38, 0.045, 0.05, 0.05),    # the stifle, forward
          (-0.33, 0.14, 0.25, 0.035, 0.035, 0.035),
          (-0.39, 0.14, 0.16, 0.03, 0.03, 0.03),     # the hock, back
          (-0.37, 0.14, 0.06, 0.028, 0.028, 0.028)],  # the foot, down and a little forward
    hind_paw=(-0.34, 0.14),
    paw_size=1.0,
    ear=dict(at=(0.69, 0.055, 1.0), keep=0.05, thin=0.016, rings=[(0.685, 0.07, 1.035, 0.025, 0.007, 0.007)],
             tip=(0.68, 0.08, 1.085)),
)

WOLF = dict(
    colours=["Ashgrey", "Sable"],
    torso=[
        (-0.5, 0, 0.76, 0.12, 0.11, 0.11),           # the rump
        (-0.4, 0, 0.75, 0.16, 0.14, 0.17),           # the haunches
        (-0.26, 0, 0.74, 0.145, 0.13, 0.15),
        (-0.12, 0, 0.76, 0.14, 0.12, 0.12),          # the waist, less tucked: a heavy animal
        (0.02, 0, 0.74, 0.165, 0.15, 0.2),           # the ribs
        (0.17, 0, 0.72, 0.185, 0.17, 0.25),          # the chest, broad and deep
        (0.3, 0, 0.74, 0.185, 0.2, 0.24),            # the shoulders, under the hackles
        (0.4, 0, 0.77, 0.17, 0.2, 0.2),              # the withers: the hackles rise
        (0.48, 0, 0.78, 0.16, 0.19, 0.17)],          # the neck's root, the ruff full
    head=[
        (0.56, 0, 0.78, 0.15, 0.17, 0.15),           # the neck, thick, the hackles standing up
        (0.64, 0, 0.77, 0.135, 0.13, 0.13),          # the head low and forward: a stalking line
        (0.72, 0, 0.77, 0.125, 0.1, 0.1),            # the back of the skull
        (0.8, 0, 0.765, 0.13, 0.1, 0.1),             # the cheeks, broad
        (0.88, 0, 0.73, 0.092, 0.065, 0.085),        # the stop
        (0.96, 0, 0.715, 0.07, 0.056, 0.07),         # the muzzle, heavy
        (1.03, 0, 0.7, 0.058, 0.048, 0.058),
        (1.08, 0, 0.693, 0.048, 0.04, 0.048)],       # blunt
    nose=((1.115, 0, 0.69), 0.02),
    tail=[(-0.57, 0, 0.73, 0.06, 0.06, 0.06), (-0.65, 0, 0.68, 0.075, 0.075, 0.075),
          (-0.73, 0, 0.62, 0.08, 0.08, 0.08), (-0.81, 0, 0.56, 0.074, 0.074, 0.074),
          (-0.88, 0, 0.5, 0.055, 0.055, 0.055)],     # thick, held low and straight out
    tail_tip=((-0.93, 0, 0.46), 0.015),
    fore_patch=[(0.34, 0.11, 0.55), (0.23, 0.12, 0.55), (0.34, 0.13, 0.62), (0.23, 0.14, 0.62)],
    fore=[(0.29, 0.15, 0.48, 0.07, 0.085, 0.085),    # the upper arm, heavy
          (0.22, 0.15, 0.39, 0.056, 0.06, 0.06),     # the elbow, behind
          (0.24, 0.15, 0.31, 0.048, 0.05, 0.05),
          (0.25, 0.15, 0.18, 0.041, 0.041, 0.041),   # the forearm, straight down
          (0.255, 0.15, 0.1, 0.038, 0.038, 0.038),   # the wrist
          (0.27, 0.15, 0.065, 0.038, 0.035, 0.035)],
    fore_paw=(0.31, 0.15),
    hind_patch=[(-0.34, 0.14, 0.66), (-0.45, 0.13, 0.66), (-0.34, 0.15, 0.73), (-0.45, 0.14, 0.73)],
    hind=[(-0.32, 0.17, 0.57, 0.08, 0.105, 0.105),   # the thigh, big
          (-0.23, 0.17, 0.41, 0.055, 0.06, 0.06),    # the stifle, forward
          (-0.34, 0.17, 0.27, 0.043, 0.043, 0.043),
          (-0.41, 0.17, 0.17, 0.037, 0.037, 0.037),  # the hock, back
          (-0.39, 0.17, 0.065, 0.036, 0.034, 0.034)],
    hind_paw=(-0.35, 0.17),
    paw_size=1.25,
    ear=dict(at=(0.75, 0.065, 0.86), keep=0.065, thin=0.02, rings=[(0.765, 0.085, 0.9, 0.032, 0.008, 0.008)],
             tip=(0.8, 0.095, 0.96)),                # big, pricked forward
)


def make(spec, name, coll="Creatures"):
    g = quadruped(spec, FUR, DARK)
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, name))
    return to_object(g.bm, name, spec["colours"], coll, clip=True)


ASSETS = {"StrayDog": lambda: make(STRAY_DOG, "StrayDog"), "Wolf": lambda: make(WOLF, "Wolf")}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
