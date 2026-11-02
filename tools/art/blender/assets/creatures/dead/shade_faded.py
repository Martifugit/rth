r"""The faded soldier, a shade (variant A of four for the owner to pick): one of the dead who didn't make
the walk, still a man from the waist up. A footsoldier standing at attention in a broad kettle hat,
square shoulders, the broken half of his spear still held upright at his side; below the belt he has
come undone into ribbons of soul-stuff that stream back and down to the water.

What must read, in order: a person (the kettle hat's brim and the square shoulders); the dark broken
spear, the one solid thing he kept; the body unravelling into ribbons below the waist. About 1.85 m
to the hat's crown, the hips hovering at 0.9 m. Grown as one mesh (artlib/grow.py); the spear is a rod
through the fist. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\shade_faded.py")
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

COLOURS = ["SoulPale", "SoulGlow", "Bark", "Sable"]
PALE, GLOW, SHAFT, DARK = range(4)

HIPS = 0.9                      # where he ends and the ribbons begin
SHOULDER = HIPS + 0.52          # the shoulders' line; the neck, head and hat follow from it
HEAD = SHOULDER + 0.24          # the middle of the face
BRIM = HEAD + 0.08              # the kettle hat's brim
FIST = (0.2, -0.31, HIPS + 0.12)    # (f, s, u) the right fist round the spear
SPEAR = (FIST[2] - 0.38, FIST[2] + 0.68)    # the broken shaft's foot and its snapped top

# the ribbons: (angle round the hips, from the front toward his left, in turns; sway): each falls from
# a face of the hips' band and streams back toward the ground behind him
RIBBONS = [(0.0, 0.0), (0.17, 0.08), (-0.17, -0.06), (0.33, 0.1), (-0.33, -0.1), (0.5, 0.0)]


def soldier():
    g = Grow(sides=12)
    # upright, from the hips to the hat's crown: rings (f, s, u, half-width, in front, behind)
    g.tube([
        (0.0, 0, HIPS, 0.16, 0.11, 0.12),              # the hips, where he comes undone
        (0.0, 0, HIPS + 0.1, 0.15, 0.1, 0.11),         # the belt
        (0.01, 0, HIPS + 0.3, 0.19, 0.13, 0.12),       # the chest, a soldier's, held up
        (0.0, 0, SHOULDER - 0.04, 0.26, 0.12, 0.12),   # the shoulders, square
        (0.0, 0, SHOULDER + 0.03, 0.2, 0.1, 0.1),
        (0.01, 0, SHOULDER + 0.1, 0.07, 0.07, 0.07),   # the neck
        (0.02, 0, HEAD - 0.08, 0.085, 0.09, 0.08),     # the jaw
        (0.02, 0, HEAD, 0.1, 0.1, 0.1),                # the face
        (0.02, 0, BRIM - 0.01, 0.11, 0.11, 0.11),
        (0.02, 0, BRIM, 0.22, 0.23, 0.22),             # the brim, broad and flat
        (0.02, 0, BRIM + 0.03, 0.22, 0.23, 0.22),
        (0.02, 0, BRIM + 0.05, 0.13, 0.13, 0.13),      # the crown of the hat
        (0.02, 0, BRIM + 0.13, 0.11, 0.11, 0.11),
        (0.02, 0, BRIM + 0.16, 0.06, 0.06, 0.06),
    ], PALE, upright=True)
    # the arms: the left hangs at his side, the right holds the spear out in front of the hip
    for s in (1, -1):
        root = g.patch([(0.0, s * 0.25, SHOULDER - 0.04), (0.0, s * 0.24, SHOULDER - 0.16)], (0, s, 0))
        if s > 0:
            wrist = g.extrude(root, [
                (0.0, s * 0.3, SHOULDER - 0.08, 0.065, 0.07, 0.07),    # the shoulder
                (-0.02, s * 0.31, SHOULDER - 0.32, 0.05, 0.055, 0.055),   # the elbow
                (0.02, s * 0.29, HIPS + 0.02, 0.04, 0.045, 0.045)], PALE)   # the wrist
            g.extrude(wrist, [(0.03, s * 0.29, HIPS - 0.05, 0.035, 0.045, 0.04)], GLOW,
                      tip=(0.04, s * 0.28, HIPS - 0.13), blunt=0.012)          # the hand, fading
        else:
            fs, fu = FIST[1], FIST[2]
            wrist = g.extrude(root, [
                (0.0, s * 0.3, SHOULDER - 0.08, 0.065, 0.07, 0.07),
                (-0.02, s * 0.31, SHOULDER - 0.3, 0.05, 0.055, 0.055),     # the elbow, at his side
                (FIST[0] - 0.08, fs, fu + 0.01, 0.042, 0.045, 0.045)], PALE)    # the forearm, forward
            g.extrude(wrist, [(FIST[0] - 0.03, fs, fu, 0.045, 0.05, 0.05)], PALE,
                      tip=(FIST[0] + 0.05, fs, fu - 0.01), blunt=0.02)        # the fist
    # the ribbons, each out of one face of the band between the hips and the belt
    for turn, sway in RIBBONS:
        a = 2 * math.pi * turn
        out = (math.cos(a), math.sin(a))                     # (f, s) outward from the hips
        start = (0.13 * out[0], 0.17 * out[1], HIPS + 0.05)
        root = g.face(start, (out[0], out[1], 0))
        back = -0.62 - 0.12 * abs(math.sin(a))               # how far behind him each one reaches
        f0, s0 = 0.18 * out[0], 0.2 * out[1]
        g.extrude([root], [
            (f0, s0, HIPS - 0.08, 0.075, 0.025, 0.025),
            (f0 * 0.8 - 0.06, s0 * 0.95 + sway * 0.5, HIPS - 0.32, 0.07, 0.02, 0.02),
            (f0 * 0.4 - 0.24, s0 * 0.8 + sway, HIPS - 0.58, 0.055, 0.016, 0.016),
            (back + 0.18, s0 * 0.6 + sway * 0.6, 0.16, 0.04, 0.012, 0.012),
        ], GLOW, tip=(back, s0 * 0.5, 0.05), blunt=0.01)
    # what is left of his legs: the hips' underside drawn down into one short wisp among the ribbons
    bottom = g.face((0.0, 0, HIPS - 0.05), (0, 0, -1))
    g.extrude([bottom], [(-0.04, 0, HIPS - 0.2, 0.09, 0.07, 0.07), (-0.14, 0, HIPS - 0.42, 0.04, 0.03, 0.03)],
              GLOW, tip=(-0.26, 0, HIPS - 0.6), blunt=0.01)
    g.smooth(0.2, repeat=1)
    for f in g.bm.faces:                              # the hips glow as they come undone
        if f.material_index == PALE and f.calc_center_median().z < HIPS + 0.06:
            f.material_index = GLOW
    bm = g.bm
    # the broken spear, upright through the fist: the shaft and its splintered top (no head: it broke off)
    fx, fs, fu = FIST
    N.rod(bm, [P(fx + 0.01, fs, SPEAR[0]), P(fx + 0.01, fs, fu), P(fx + 0.01, fs, SPEAR[1])],
          0.022, mat=SHAFT, sides=6, taper=0.9)
    top = SPEAR[1]                                    # snapped: two splinters of unequal length
    A.hull(bm, [P(fx - 0.01, fs - 0.018, top - 0.03), P(fx + 0.03, fs - 0.01, top - 0.03),
                P(fx + 0.01, fs + 0.0, top - 0.03), P(fx + 0.015, fs - 0.012, top + 0.12)], mat=SHAFT)
    A.hull(bm, [P(fx - 0.01, fs + 0.0, top - 0.03), P(fx + 0.02, fs + 0.02, top - 0.03),
                P(fx - 0.012, fs + 0.02, top - 0.03), P(fx + 0.0, fs + 0.012, top + 0.05)], mat=SHAFT)
    # his eyes: two thin dark strokes under the brim, the inner ends lower
    for s in (1, -1):
        A.decal(bm, [P(0.2, s * 0.022, HEAD + 0.005), P(0.2, s * 0.065, HEAD + 0.02),
                     P(0.2, s * 0.065, HEAD + 0.035), P(0.2, s * 0.022, HEAD + 0.02)], tuple(P(1, 0, 0)), mat=DARK)
    return g


def build(name="ShadeFaded", coll="Creatures"):
    return soldier().make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"ShadeFaded": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
