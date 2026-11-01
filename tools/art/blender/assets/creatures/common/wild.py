r"""Wild beasts (2026-11-01), each a spec for beast.quadruped, built by tools/art/blender/creatures.md.

- Fox    the stray dog's construction at 0.55 scale: big pricked ears, a long bushy tail
- Boar   a heavy wedge: shoulders high, the back sloping to the rump, no neck, the head a long wedge
         down to a flat snout, tusks curving up from the jaw, short legs on hooves
- Stag   a red deer: long legs on hooves, a slender deep body, the neck rising, a small head with ears
         out to the sides, antlers of bones (beast.bone): a curved beam and four tines grown out of it
- Bear   a brown bear: a massive body with a hump over the shoulders, a broad head low on a thick
         neck, small round ears, thick legs on big paws, a stub of a tail

Proportions from the player (1.8 m): the fox 0.46 m at the withers, the boar 0.84 m, the stag 1.2 m
(its antlers to about 2.1 m), the bear about 1.5 m at the hump.

Each is one part, the left half (+X) with a Mirror modifier; growths and feet out of the body.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\wild.py")
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
from mathutils import Vector  # noqa: E402

from artlib.beast import bone, curve, keep_left, quadruped, scaled, spines, to_object, tune, tuning  # noqa: E402
from assets.creatures.common import canine  # noqa: E402

SKIN, CLAW, HORN = range(3)

FOX = scaled(canine.STRAY_DOG, 0.55)
FOX.update(
    colours=["Russet", "Sable", "SaintBone"],
    ear=dict(at=(0.38, 0.03, 0.55), keep=0.03, thin=0.009, rings=[(0.378, 0.042, 0.585, 0.021, 0.004, 0.004)],
             tip=(0.375, 0.052, 0.64), blunt=0.003),               # big and pricked
    tail=[(-0.3, 0, 0.37, 0.035, 0.035, 0.035), (-0.36, 0, 0.32, 0.05, 0.05, 0.05),
          (-0.42, 0, 0.27, 0.058, 0.058, 0.058), (-0.48, 0, 0.23, 0.055, 0.055, 0.055),
          (-0.54, 0, 0.2, 0.04, 0.04, 0.04)],                      # long and bushy
    tail_tip=((-0.59, 0, 0.19), 0.012),
)

BOAR = dict(
    colours=["Cinder", "Sable", "SaintBone"],
    feet="hoof",
    torso=[
        (-0.55, 0, 0.55, 0.12, 0.11, 0.11),          # the rump
        (-0.45, 0, 0.56, 0.2, 0.17, 0.2),            # the haunches
        (-0.25, 0, 0.56, 0.22, 0.2, 0.24),           # the barrel
        (-0.02, 0, 0.58, 0.23, 0.22, 0.26),
        (0.2, 0, 0.6, 0.22, 0.24, 0.26),             # the shoulders, highest
        (0.35, 0, 0.6, 0.19, 0.21, 0.22)],           # no neck to speak of
    head=[
        (0.47, 0, 0.58, 0.16, 0.17, 0.19),
        (0.58, 0, 0.54, 0.14, 0.13, 0.15),           # the back of the skull
        (0.68, 0, 0.48, 0.115, 0.1, 0.12),           # the cheeks
        (0.78, 0, 0.42, 0.08, 0.075, 0.085),         # the long wedge down to the snout
        (0.87, 0, 0.37, 0.06, 0.055, 0.06),
        (0.93, 0, 0.345, 0.055, 0.05, 0.055)],       # the snout's disc
    nose=((0.96, 0, 0.34), 0.05),
    tail=[(-0.6, 0, 0.55, 0.03, 0.03, 0.03), (-0.65, 0, 0.5, 0.02, 0.02, 0.02)],
    tail_tip=((-0.67, 0, 0.44), 0.006),
    fore_patch=[(0.25, 0.13, 0.4), (0.13, 0.14, 0.4), (0.25, 0.16, 0.46), (0.13, 0.17, 0.46)],
    fore=[(0.19, 0.16, 0.33, 0.06, 0.07, 0.07),      # the upper arm
          (0.16, 0.16, 0.24, 0.045, 0.05, 0.05),     # the elbow
          (0.18, 0.16, 0.14, 0.037, 0.037, 0.037),
          (0.19, 0.16, 0.08, 0.033, 0.033, 0.033)],  # the pastern
    fore_paw=(0.2, 0.16),
    hind_patch=[(-0.38, 0.13, 0.4), (-0.5, 0.12, 0.4), (-0.38, 0.15, 0.46), (-0.5, 0.14, 0.46)],
    hind=[(-0.42, 0.16, 0.33, 0.07, 0.08, 0.08),     # the thigh
          (-0.36, 0.16, 0.24, 0.05, 0.05, 0.05),     # the stifle
          (-0.43, 0.16, 0.15, 0.04, 0.04, 0.04),     # the hock
          (-0.42, 0.16, 0.08, 0.034, 0.034, 0.034)],
    hind_paw=(-0.41, 0.16),
    paw_size=1.0,
    ear=dict(at=(0.6, 0.07, 0.66), keep=0.04, thin=0.012, rings=[(0.6, 0.09, 0.71, 0.032, 0.006, 0.006)],
             tip=(0.59, 0.1, 0.76), blunt=0.004),
    growths=[dict(at=(0.8, 0.075, 0.37), toward=(0.2, 1, -0.3), keep=0.03, mat=HORN, up=(0, 0, 1),
                  rings=[(0.81, 0.095, 0.375, 0.016, 0.016, 0.016), (0.83, 0.11, 0.41, 0.013, 0.013, 0.013)],
                  tip=(0.85, 0.11, 0.47), blunt=0.003)],
    extra=lambda g: spines(g, CLAW, start=0.5, end=-0.4, peak=(0.3, -0.05), step=0.07, tallest=0.07, keep=0.02),
)

STAG = dict(
    colours=["Hide", "Sable", "SaintBone"],
    feet="hoof",
    torso=[
        (-0.55, 0, 1.05, 0.1, 0.1, 0.1),             # the rump
        (-0.45, 0, 1.06, 0.15, 0.14, 0.17),          # the haunches
        (-0.25, 0, 1.02, 0.15, 0.14, 0.18),
        (0.0, 0, 1.02, 0.16, 0.15, 0.22),            # the ribs, deep
        (0.2, 0, 1.04, 0.15, 0.16, 0.22),            # the chest
        (0.33, 0, 1.1, 0.12, 0.13, 0.15)],           # the withers, the neck's root
    head=[
        (0.42, 0, 1.22, 0.125, 0.125, 0.135),        # the neck, rising, thick at its root
        (0.5, 0, 1.36, 0.1, 0.105, 0.11),
        (0.56, 0, 1.48, 0.08, 0.08, 0.085),
        (0.62, 0, 1.56, 0.065, 0.065, 0.07),         # the back of the skull
        (0.7, 0, 1.57, 0.065, 0.06, 0.065),          # the cheeks
        (0.79, 0, 1.52, 0.045, 0.045, 0.05),         # the muzzle, long and fine
        (0.86, 0, 1.48, 0.035, 0.035, 0.038)],
    nose=((0.9, 0, 1.465), 0.02),
    tail=[(-0.6, 0, 1.03, 0.04, 0.04, 0.04)],
    tail_tip=((-0.64, 0, 0.96), 0.012),
    fore_patch=[(0.25, 0.09, 0.86), (0.14, 0.1, 0.86), (0.25, 0.11, 0.92), (0.14, 0.12, 0.92)],
    fore=[(0.2, 0.12, 0.78, 0.05, 0.06, 0.06),       # the upper arm
          (0.15, 0.12, 0.66, 0.04, 0.042, 0.042),    # the elbow
          (0.17, 0.12, 0.56, 0.033, 0.033, 0.033),
          (0.18, 0.12, 0.36, 0.026, 0.026, 0.026),   # the forearm
          (0.18, 0.12, 0.22, 0.024, 0.024, 0.024),   # the knee
          (0.19, 0.12, 0.09, 0.02, 0.02, 0.02),      # the cannon
          (0.21, 0.12, 0.05, 0.022, 0.02, 0.02)],    # the pastern
    fore_paw=(0.23, 0.12),
    hind_patch=[(-0.35, 0.1, 0.92), (-0.48, 0.09, 0.92), (-0.35, 0.12, 0.98), (-0.48, 0.11, 0.98)],
    hind=[(-0.38, 0.13, 0.84, 0.06, 0.08, 0.08),     # the thigh
          (-0.28, 0.13, 0.62, 0.04, 0.045, 0.045),   # the stifle
          (-0.38, 0.13, 0.45, 0.03, 0.03, 0.03),
          (-0.47, 0.13, 0.36, 0.026, 0.026, 0.026),  # the hock, high
          (-0.45, 0.13, 0.2, 0.022, 0.022, 0.022),   # the cannon
          (-0.43, 0.13, 0.08, 0.02, 0.02, 0.02),
          (-0.41, 0.13, 0.05, 0.022, 0.02, 0.02)],
    hind_paw=(-0.39, 0.13),
    paw_size=0.7,
    ear=dict(at=(0.6, 0.06, 1.585), toward=(0, 1, 0.3), keep=0.04, thin=0.01,
             rings=[(0.595, 0.095, 1.605, 0.03, 0.005, 0.005)], tip=(0.585, 0.145, 1.625), blunt=0.006),
    extra=lambda g: antlers(g),
)

# The stag's antlers, left side (f, s, u): a beam from the skull along a curve, tines out of it at a
# fraction of its length, each pointing its own way. Every part is a bone (beast.bone): round, rooted
# in a patch of faces, following a curve, tapering.
BEAM = dict(root=(0.68, 0.055, 1.615), pull=(0.62, 0.15, 1.86), end=(0.47, 0.21, 2.13), r0=0.026, r1=0.009, rings=8)
TINES = [(0.12, (1.0, 0.1, 0.35), 0.14, 0.012),     # brow tine: (where along the beam, way, length, root radius)
         (0.34, (1.0, 0.3, 0.5), 0.12, 0.011),       # bez tine
         (0.62, (0.8, 0.3, 0.6), 0.11, 0.009),       # trez tine
         (0.84, (0.6, 0.35, 0.9), 0.1, 0.008)]       # the fork at the crown


def antlers(g):
    b = BEAM
    bone(g, HORN, b["root"], (0, 0.6, 1), (1, 0, 0), (b["pull"], b["end"]), b["r0"], b["r1"], rings=b["rings"], faces=4)
    line = curve(b["root"], b["pull"], b["end"], b["r0"], b["r1"], 40)
    for t, way, length, r0 in TINES:
        i = round(t * 40)
        c, r = Vector(line[i][:3]), line[i][3]
        tangent = (Vector(line[min(i + 1, 40)][:3]) - Vector(line[max(i - 1, 0)][:3])).normalized()
        w = Vector(way).normalized()
        out = (w - tangent * w.dot(tangent)).normalized()
        root = c + out * r * 0.95
        end = root + w * length
        pull = root + out * length * 0.45 + w * length * 0.25
        bone(g, HORN, tuple(root), tuple(out), tuple(tangent), (tuple(pull), tuple(end)), r0, 0.003, rings=4, faces=2)


BEAR = dict(
    colours=["Mud", "Sable", "SaintBone"],
    torso=[
        (-0.6, 0, 0.72, 0.15, 0.15, 0.15),           # the rump
        (-0.5, 0, 0.75, 0.27, 0.24, 0.26),           # the haunches
        (-0.25, 0, 0.74, 0.3, 0.26, 0.3),            # the belly, massive
        (0.0, 0, 0.76, 0.3, 0.3, 0.32),
        (0.22, 0, 0.82, 0.28, 0.37, 0.32),           # the hump over the shoulders, high
        (0.38, 0, 0.8, 0.22, 0.22, 0.24)],           # the neck's root
    head=[
        (0.5, 0, 0.78, 0.19, 0.18, 0.19),            # the neck, thick and low
        (0.6, 0, 0.76, 0.17, 0.15, 0.16),            # the skull
        (0.7, 0, 0.74, 0.17, 0.13, 0.15),            # the cheeks, broad
        (0.79, 0, 0.69, 0.1, 0.08, 0.1),             # the stop
        (0.88, 0, 0.66, 0.075, 0.06, 0.075),         # the muzzle
        (0.94, 0, 0.645, 0.06, 0.05, 0.06)],
    nose=((0.98, 0, 0.64), 0.03),
    tail=[(-0.66, 0, 0.74, 0.04, 0.04, 0.04)],
    tail_tip=((-0.69, 0, 0.72), 0.02),
    fore_patch=[(0.3, 0.17, 0.55), (0.15, 0.18, 0.55), (0.3, 0.2, 0.62), (0.15, 0.21, 0.62)],
    fore=[(0.24, 0.22, 0.46, 0.09, 0.1, 0.1),        # the upper arm
          (0.2, 0.22, 0.34, 0.075, 0.08, 0.08),      # the elbow
          (0.22, 0.22, 0.24, 0.066, 0.066, 0.066),
          (0.23, 0.22, 0.13, 0.06, 0.06, 0.06),      # the wrist
          (0.25, 0.22, 0.08, 0.06, 0.055, 0.055)],
    fore_paw=(0.3, 0.22),
    hind_patch=[(-0.42, 0.17, 0.55), (-0.58, 0.16, 0.55), (-0.42, 0.2, 0.62), (-0.58, 0.19, 0.62)],
    hind=[(-0.48, 0.22, 0.46, 0.1, 0.12, 0.12),      # the thigh
          (-0.4, 0.22, 0.32, 0.075, 0.075, 0.075),   # the stifle
          (-0.45, 0.22, 0.2, 0.065, 0.065, 0.065),
          (-0.48, 0.22, 0.12, 0.06, 0.06, 0.06),     # the hock, low: it walks on its soles
          (-0.44, 0.22, 0.07, 0.06, 0.055, 0.055)],
    hind_paw=(-0.38, 0.22),
    paw_size=1.8,
    hind_paw_size=1.9,
    ear=dict(at=(0.6, 0.1, 0.9), keep=0.07, thin=0.018, rings=[(0.6, 0.12, 0.93, 0.05, 0.007, 0.007)],
             tip=(0.595, 0.13, 0.96), blunt=0.03),                 # small, round and broad: no stalk
)


def make(spec, name, coll="Creatures"):
    g = quadruped(spec, SKIN, CLAW)
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, name))
    return to_object(g.bm, name, spec["colours"], coll, clip=True)


BEAR = scaled(BEAR, 1.35)                    # a big brown bear: the hump about 1.5 m

ASSETS = {"Fox": lambda: make(FOX, "Fox"), "Boar": lambda: make(BOAR, "Boar"),
          "Stag": lambda: make(STAG, "Stag"), "Bear": lambda: make(BEAR, "Bear")}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
