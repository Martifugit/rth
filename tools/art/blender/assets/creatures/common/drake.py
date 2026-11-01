r"""The drake (2026-11-01): the dragons' wingless kin, a lean stalker, built by
tools/art/blender/creatures.md.

Dragon first (the wedge skull with its brow and deep jaw, dragon legs and claws, a ridge of blades),
then what only it has:
- wingless, and crouched like a hunting cat: a deep chest over a narrow waist, muscular haunches,
  elbows and knees bent, the long neck carrying the head forward and low (the dragon stands tall)
- a crown of spikes swept back from the skull and jaw (the dragon has one pair of long horns)
- a long whip of a tail ending in a cluster of blades

Not a dinosaur: no barrel body, no thick tail, no frill or nose horns.

Proportions from the player (1.8 m): the shoulders about 1.6 m, the head carried about 1.5 m, snout
to tail tip about 9.4 m.

One part: the left half (+X) with a Mirror modifier; crown, blades and claws grown out of its surface.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\drake.py")
"""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

from mathutils import Vector  # noqa: E402

from artlib import family  # noqa: E402
from artlib.beast import foot, keep_left, spines, symmetric, to_object, tune, tuning  # noqa: E402
from artlib.grow import Grow, P  # noqa: E402

COLOURS = ["Russet", "SaintBone"]           # untextured, one colour a part until the topology is passed
SKIN, HORN = range(2)

# The crown: spikes round the back of the skull and down the jaw, (where on the head, length).
CROWN = [((2.42, 0.13, 1.68), 0.62), ((2.38, 0.23, 1.62), 0.5), ((2.38, 0.28, 1.5), 0.42),
         ((2.44, 0.27, 1.37), 0.32), ((2.58, 0.24, 1.26), 0.22)]
SKULL = (2.4, 0.0, 1.48)                    # the skull's middle there: each spike leans out from it


def body():
    g = Grow(sides=12)
    torso = g.tube([
        (-1.6, 0, 1.05, 0.3, 0.3, 0.3),              # the root of the tail
        (-1.2, 0, 1.12, 0.46, 0.4, 0.4),             # the haunches, muscular
        (-0.7, 0, 1.08, 0.38, 0.35, 0.36),           # the waist, narrow
        (-0.15, 0, 1.05, 0.47, 0.41, 0.5),           # the ribs
        (0.4, 0, 1.06, 0.53, 0.46, 0.62),            # the chest, deep: a keel
        (0.85, 0, 1.15, 0.5, 0.44, 0.52),            # the shoulders
        (1.2, 0, 1.26, 0.36, 0.32, 0.36),            # the neck's root
    ], SKIN)
    # the neck, long, carrying the head forward and low; the head the dragon's wedge, a longer snout
    g.extrude(torso.caps["end"], [
        (1.5, 0, 1.36, 0.29, 0.27, 0.29),
        (1.8, 0, 1.43, 0.25, 0.23, 0.25),
        (2.1, 0, 1.47, 0.23, 0.21, 0.23),
        (2.36, 0, 1.48, 0.24, 0.22, 0.25),           # the back of the skull
        (2.58, 0, 1.46, 0.27, 0.2, 0.29),            # the cheeks and brow, widest; the jaw deep
        (2.82, 0, 1.39, 0.21, 0.15, 0.24),
        (3.06, 0, 1.32, 0.16, 0.11, 0.17),           # the snout
        (3.26, 0, 1.27, 0.12, 0.08, 0.12),
    ], SKIN, tip=(3.38, 0, 1.25), blunt=0.03)
    # the tail, a long whip
    g.extrude(torso.caps["start"], [
        (-2.1, 0, 0.95, 0.24, 0.24, 0.24), (-2.7, 0, 0.8, 0.18, 0.18, 0.18),
        (-3.3, 0, 0.62, 0.13, 0.13, 0.13), (-3.9, 0, 0.46, 0.095, 0.095, 0.095),
        (-4.5, 0, 0.34, 0.068, 0.068, 0.068), (-5.1, 0, 0.26, 0.05, 0.05, 0.05),
        (-5.6, 0, 0.21, 0.04, 0.04, 0.04)], SKIN, tip=(-6.0, 0, 0.18), blunt=0.012)
    spines(g, HORN, start=2.1, end=-4.9, peak=(0.9, -1.4), step=0.28, tallest=0.24)
    skull = P(*SKULL)
    for s in (1, -1):
        # the hind legs, crouched: a heavy thigh, the knee forward, the hock back. The segments are long
        # and the leg thin where it bends sharply, so the rings on the inside of a bend never cross
        hind = g.patch([(-1.4, s * 0.4, 0.88), (-1.2, s * 0.42, 0.85), (-1.0, s * 0.4, 0.86)], (0, s * 0.7, -1))
        ankle = g.extrude(hind, [
            (-1.08, s * 0.72, 0.82, 0.31, 0.35, 0.35),   # the thigh
            (-0.84, s * 0.79, 0.55, 0.19, 0.19, 0.19),   # the knee, forward
            (-0.98, s * 0.8, 0.33, 0.15, 0.15, 0.15),
            (-1.12, s * 0.8, 0.18, 0.13, 0.13, 0.13),    # the hock, back
            (-0.98, s * 0.79, 0.08, 0.14, 0.1, 0.09)], SKIN)   # the ankle, a step forward of the hock
        foot(g, ankle, -0.76, s * 0.8, s, SKIN, HORN)
        # the forelegs, crouched: the elbow out and back, the forearm reaching forward to the wrist
        fore = g.patch([(0.25, s * 0.47, 0.72), (0.48, s * 0.47, 0.72), (0.7, s * 0.44, 0.76)], (0, s * 0.7, -1))
        wrist = g.extrude(fore, [
            (0.62, s * 0.72, 0.72, 0.25, 0.29, 0.29),    # the upper arm
            (0.42, s * 0.85, 0.52, 0.19, 0.19, 0.19),
            (0.38, s * 0.88, 0.41, 0.18, 0.18, 0.18),    # the elbow
            (0.48, s * 0.85, 0.29, 0.16, 0.16, 0.16),
            (0.66, s * 0.8, 0.14, 0.135, 0.135, 0.135),  # the wrist
            (0.68, s * 0.79, 0.08, 0.14, 0.1, 0.09)], SKIN)
        foot(g, wrist, 0.9, s * 0.8, s, SKIN, HORN)
        # the crown: each spike out of the head, swept back and leaning out from the skull; every face
        # found before any grows (growing changes the mesh)
        picked = [(g.face((f, s * w, u), (0, s * w, u - SKULL[2])), L) for (f, w, u), L in CROWN]
        picked = [p for i, p in enumerate(picked) if p[0] not in [q[0] for q in picked[:i]]]   # each face once
        for face, L in picked:
            base = g.inset(face, min(0.14, 0.22 * L)).calc_center_median()
            out = Vector((base.x - skull.x, 0, base.z - skull.z)).normalized()
            back = P(-1, 0, 0)
            mid = base + out * (L * 0.35) + back * (L * 0.3)
            tip = base + out * (L * 0.55) + back * (L * 0.85)
            r = min(0.065, 0.11 * L)
            g.extrude([face], [(-mid.y, mid.x, mid.z, r * 0.7, r * 0.7, r * 0.7)], HORN,
                      tip=(-tip.y, tip.x, tip.z), blunt=0.005)
        # blades round the tail's tip, raked back
        picked = [(g.face((f, s * 0.07, u), (0, s, 0.05)), L) for f, u, L in   # on the tail's side, off the centre line
                  ((-5.0, 0.27, 0.26), (-5.35, 0.23, 0.22), (-5.7, 0.2, 0.17))]
        for face, L in picked:
            base = g.inset(face, 0.03).calc_center_median()
            mid = base + Vector((s * 0.55, 0, 0.35)).normalized() * (L * 0.3) + P(-1, 0, 0) * (L * 0.3)
            tip = base + Vector((s * 0.55, 0, 0.35)).normalized() * L + P(-1, 0, 0) * L
            g.extrude([face], [(-mid.y, mid.x, mid.z, 0.02, 0.02, 0.02)], HORN, tip=(-tip.y, tip.x, tip.z), blunt=0.004)
    symmetric(g.bm)
    return g


def build(name="Drake", coll="Creatures"):
    g = body()
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, "Drake"))
    return to_object(g.bm, name, COLOURS, coll, clip=True)


ASSETS = {"Drake": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
