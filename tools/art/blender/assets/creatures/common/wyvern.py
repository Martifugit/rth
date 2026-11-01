r"""The wyvern (2026-11-01): dragon-kin with two legs, built by tools/art/blender/creatures.md.

Dragon first (the wedge skull with swept horns, an S neck, the bat-built wing, a ridge of blades), then
what only it has:
- two legs: the wings are its forelimbs; it stands on long, powerful hind legs, the body leaning
  forward over them and the tail held out behind as the counterweight
- a long whip of a tail ending in a barb: a spade of blades
- leaner and lighter than the dragon, the head narrower and longer, almost a beak

Proportions from the player (1.8 m): the crown about 3.7 m with the head up, the hips about 1.6 m,
snout to tail tip about 8 m, the wings about 11 m across.

Two parts under one root, each the left half (+X) with a Mirror modifier:
- Wyvern        the body, neck, head, legs, toes and tail, one closed mesh; horns, blades and talons
                grown out of its surface
- Wyvern_Wing   the wing (artlib/wing.py): bones and membrane as one closed surface, its root in the
                flank from the shoulder to the hip

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\wyvern.py")
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
from mathutils.bvhtree import BVHTree  # noqa: E402

from artlib import family  # noqa: E402
from artlib.beast import foot, keep_left, spines, symmetric, to_object, tune, tuning  # noqa: E402
from artlib.grow import Grow, P  # noqa: E402
from artlib.wing import WingSpec, wing  # noqa: E402

COLOURS = ["Peat", "Autumn", "SaintBone"]   # untextured, one colour a part until the topology is passed
SKIN, WING, HORN = range(3)

# The leaning torso's middle height along its length (f, u): the wing's root is aimed at it.
CORE = [(-1.0, 1.5), (-0.65, 1.62), (-0.25, 1.8), (0.15, 2.05), (0.45, 2.3), (0.65, 2.55), (0.8, 2.8)]


def core(f):
    if f <= CORE[0][0]:
        return CORE[0][1]
    for (f0, u0), (f1, u1) in zip(CORE, CORE[1:]):
        if f <= f1:
            return u0 + (u1 - u0) * (f - f0) / (f1 - f0)
    return CORE[-1][1]


SIZE = 0.85                                 # the wing against the dragon's: lighter bones and skin
WING_SPEC = WingSpec(
    shoulder=(0.62, 0.42, 2.66), elbow=(0.3, 1.3, 3.25), wrist=(0.9, 2.5, 3.9), thumb=(1.15, 2.6, 4.2),
    tips=[(0.4, 4.9, 3.9), (-0.7, 5.4, 3.3), (-1.6, 4.8, 2.5), (-1.95, 3.4, 1.95)],
    hip=(-0.6, 0.45, 1.72), lead=(0.85, 1.55, 3.45),
    root_in=(0.62, 0.15, 2.48), root_out=(0.62, 0.36, 2.68), flank=(0.35, 0.4, 2.3), front=(0.88, 0.3, 2.78),
    core=core, size=SIZE, r_shoulder=0.17, r_elbow=0.11, r_finger=[r * SIZE for r in (0.066, 0.064, 0.062, 0.062)],
    skin=0.013, hand_from=0.4, fan=0.36, bone=SKIN, membrane=WING, claw=HORN)


def body():
    g = Grow(sides=12)
    torso = g.tube([
        (-1.0, 0, 1.5, 0.28, 0.28, 0.28),            # the root of the tail
        (-0.65, 0, 1.62, 0.42, 0.38, 0.38),          # the hips
        (-0.25, 0, 1.8, 0.37, 0.35, 0.39),           # the waist
        (0.15, 0, 2.05, 0.45, 0.41, 0.5),            # the ribs
        (0.45, 0, 2.3, 0.49, 0.44, 0.52),            # the chest, deep
        (0.65, 0, 2.55, 0.42, 0.38, 0.42),           # the shoulders
        (0.8, 0, 2.8, 0.3, 0.28, 0.3),               # the neck's root
    ], SKIN)
    # the neck, an S rising from the leaning chest; the head narrow and long, almost a beak
    g.extrude(torso.caps["end"], [
        (0.92, 0, 3.05, 0.24, 0.23, 0.24),
        (1.0, 0, 3.3, 0.2, 0.19, 0.2),
        (1.12, 0, 3.5, 0.18, 0.17, 0.18),
        (1.3, 0, 3.62, 0.18, 0.17, 0.18),
        (1.5, 0, 3.66, 0.2, 0.18, 0.21),             # the back of the skull
        (1.72, 0, 3.62, 0.21, 0.16, 0.24),           # the cheeks and brow, widest; the jaw deep
        (1.95, 0, 3.55, 0.15, 0.12, 0.18),
        (2.18, 0, 3.48, 0.11, 0.09, 0.13),           # the snout
        (2.36, 0, 3.43, 0.08, 0.065, 0.085),
    ], SKIN, tip=(2.48, 0, 3.41), blunt=0.022)
    # the tail, a long whip held out behind, falling to the ground at its end
    g.extrude(torso.caps["start"], [
        (-1.5, 0, 1.32, 0.22, 0.22, 0.22), (-2.1, 0, 1.08, 0.17, 0.17, 0.17),
        (-2.7, 0, 0.85, 0.13, 0.13, 0.13), (-3.3, 0, 0.66, 0.1, 0.1, 0.1),
        (-3.9, 0, 0.52, 0.077, 0.077, 0.077), (-4.5, 0, 0.43, 0.06, 0.06, 0.06),
        (-5.0, 0, 0.38, 0.052, 0.052, 0.052)], SKIN, tip=(-5.45, 0, 0.36), blunt=0.012)
    spines(g, HORN, start=0.55, end=-4.6, peak=(0.2, -1.0), step=0.3, tallest=0.17)
    for s in (1, -1):
        # the hind legs: long and powerful, the knee forward, the hock back, standing on the toes
        # under the body's weight; long segments where it folds hard
        hind = g.patch([(-0.85, s * 0.33, 1.42), (-0.65, s * 0.36, 1.38), (-0.45, s * 0.34, 1.42)], (0, s * 0.7, -1))
        ankle = g.extrude(hind, [
            (-0.5, s * 0.6, 1.32, 0.3, 0.36, 0.36),      # the thigh
            (-0.12, s * 0.66, 0.95, 0.19, 0.19, 0.19),   # the knee, forward
            (-0.36, s * 0.67, 0.55, 0.14, 0.14, 0.14),
            (-0.58, s * 0.66, 0.25, 0.115, 0.115, 0.115),  # the hock, back
            (-0.45, s * 0.65, 0.09, 0.13, 0.1, 0.09)], SKIN)   # the ankle, a step forward of the hock
        foot(g, ankle, -0.22, s * 0.66, s, SKIN, HORN, size=1.1)
        # the horns: a pair swept back from the brow, shorter than the dragon's
        g.extrude([g.inset(g.face((1.6, s * 0.12, 3.82), (-0.4, s * 0.5, 1)), 0.1)], [
            (1.5, s * 0.18, 3.92, 0.055, 0.055, 0.055), (1.3, s * 0.22, 4.0, 0.04, 0.04, 0.04)],
            HORN, tip=(1.08, s * 0.22, 3.98), blunt=0.005)
        # the barb: a spade of blades out of the tail's sides near its tip, swept back and out
        picked = [(g.face((f, s * 0.06, u), (0, s, 0.05)), L, a) for f, u, L, a in
                  ((-4.95, 0.39, 0.34, 0.55), (-5.22, 0.37, 0.26, 0.4))] if s > 0 else []   # the left only:
        # symmetric() copies it over, and growing it on the right too can touch the centre line's faces
        for face, L, a in picked:                  # broad, flat leaves lying level, swept back: a spade
            base = g.inset(face, 0.04).calc_center_median()
            way = (Vector((s * a, 0, 0.02)) + P(-1, 0, 0)).normalized()
            mid = base + way * (L * 0.4)
            tip = base + way * L
            g.extrude([face], [(-mid.y, mid.x, mid.z, 0.075, 0.012, 0.012)], HORN, tip=(-tip.y, tip.x, tip.z), blunt=0.004)
    symmetric(g.bm)
    return g


def build(name="Wyvern", coll="Creatures"):
    g = body()
    bvh = BVHTree.FromBMesh(g.bm)                  # for the wing's root, before the cut
    wing_bm = wing(WING_SPEC, bvh)
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, "Wyvern"))
    root = to_object(g.bm, name, COLOURS, coll, clip=True)
    to_object(wing_bm, name + "_Wing", COLOURS, coll, root)
    return root


ASSETS = {"Wyvern": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
