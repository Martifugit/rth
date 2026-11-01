r"""The dragon (2026-11-01), built as two parts under one root object, each the left half (+X) with
a Mirror modifier, so either can be worked on alone with the other side following:

- Dragon         the body: torso, neck, head, legs, toes and tail, one closed mesh grown with
                 artlib/grow.py; everything that sticks out of it (horns, spines, claws) is grown out
                 of its surface, from an inset face, never set on it as a separate piece
- Dragon_Wing    the arm (from inside the back), the fingers and the thumb extruded out of it, and the
                 membrane: a thin closed sheet whose edges along the bones run inside them and whose
                 root sinks into the flank from the shoulder to the hip

The export joins the hierarchy (the client draws every mesh in it). What should read as "dragon", in
order: the wings, the deep keeled chest over a narrow waist, the heavy S neck and wedge skull with
swept horns, the long tail, the crouch. Proportions from the player (1.8 m): crown about 3.3 m,
withers 2.3 m, snout to tail tip about 10 m, wings about 12 m across.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\dragon.py")
"""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]


import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

from artlib import family  # noqa: E402
from artlib.beast import foot, keep_left, spines, to_object, tune, tuning  # noqa: E402
from artlib.grow import Grow, P  # noqa: E402
from artlib.wing import WingSpec, wing  # noqa: E402

COLOURS = ["Moor", "Russet", "SaintBone"]   # one colour a part for now: painting waits on the topology
SCALE, WING, HORN = range(3)

# The wing, left side (f, s, u): shoulder, elbow, a high wrist, the thumb's tip, the four finger tips
# from the leading one back, and the root line along the body from the hip to the shoulder.
SHOULDER = (0.75, 0.55, 2.4)
ELBOW = (0.4, 1.5, 3.1)
WRIST = (1.1, 2.9, 3.9)
THUMB = (1.4, 3.0, 4.2)
TIPS = [(0.5, 5.6, 3.9), (-0.8, 6.1, 3.2), (-1.9, 5.4, 2.3), (-2.4, 3.8, 1.75)]
HIP = (-1.5, 0.55, 1.85)
LEAD = (1.05, 1.75, 3.3)                  # the leading edge of the skin in front of the elbow
R_SHOULDER, R_ELBOW = 0.2, 0.13
R_FINGER = [0.066, 0.064, 0.062, 0.062]   # at the root (the trailing ones are long levers: as stout)
SKIN = 0.015                              # the membrane's half thickness


def body():
    g = Grow(sides=12)
    torso = g.tube([
        (-2.0, 0, 1.55, 0.3, 0.3, 0.3),              # the root of the tail
        (-1.5, 0, 1.65, 0.5, 0.42, 0.42),            # the haunches
        (-0.95, 0, 1.6, 0.5, 0.42, 0.48),            # the waist
        (-0.35, 0, 1.6, 0.64, 0.52, 0.68),           # the belly
        (0.3, 0, 1.68, 0.72, 0.6, 0.85),             # the chest, deepest: a keel
        (0.9, 0, 1.85, 0.6, 0.52, 0.7),              # the shoulders
        (1.35, 0, 2.1, 0.44, 0.4, 0.46),             # the neck's root
    ], SCALE)
    # the neck, heavy, rising and curving forward; the head a wedge, chin down
    g.extrude(torso.caps["end"], [
        (1.62, 0, 2.42, 0.42, 0.38, 0.44),
        (1.8, 0, 2.75, 0.35, 0.33, 0.37),
        (1.96, 0, 3.02, 0.3, 0.28, 0.31),
        (2.16, 0, 3.17, 0.27, 0.25, 0.28),
        (2.34, 0, 3.22, 0.27, 0.25, 0.3),            # the back of the skull, the jaw's hinge below
        (2.55, 0, 3.2, 0.31, 0.22, 0.36),            # the cheeks and brow, widest; the jaw deep
        (2.78, 0, 3.12, 0.25, 0.17, 0.3),
        (3.0, 0, 3.04, 0.2, 0.14, 0.24),             # the snout, flat-topped, deep
        (3.2, 0, 2.98, 0.17, 0.12, 0.2),
        (3.36, 0, 2.94, 0.15, 0.1, 0.15),            # the nose
    ], SCALE, tip=(3.45, 0, 2.92), blunt=0.03)
    # the tail, long, straight back along the middle and sweeping down to the ground (a neutral
    # pose: a curl is the rig's to give)
    g.extrude(torso.caps["start"], [
        (-2.55, 0, 1.4, 0.3, 0.3, 0.3), (-3.15, 0, 1.12, 0.25, 0.25, 0.25),
        (-3.75, 0, 0.82, 0.2, 0.2, 0.2), (-4.35, 0, 0.55, 0.15, 0.15, 0.15),
        (-4.95, 0, 0.36, 0.12, 0.12, 0.12), (-5.55, 0, 0.25, 0.09, 0.09, 0.09),
        (-6.1, 0, 0.2, 0.07, 0.07, 0.07)], SCALE, tip=(-6.6, 0, 0.17), blunt=0.015)
    spines(g, HORN, start=2.25, end=-5.2)
    for s in (1, -1):
        # the hind legs: a great thigh, the knee forward, the hock back, standing on the toes
        hind = g.patch([(-1.7, s * 0.48, 1.4), (-1.47, s * 0.49, 1.37), (-1.25, s * 0.5, 1.35)], (0, s * 0.6, -1))
        hock = g.extrude(hind, [
            (-1.25, s * 0.8, 1.15, 0.38, 0.5, 0.48),    # the thigh
            (-0.88, s * 0.84, 0.75, 0.22, 0.22, 0.22),   # the knee, forward
            (-1.3, s * 0.85, 0.42, 0.18, 0.19, 0.19),
            (-1.5, s * 0.85, 0.22, 0.15, 0.15, 0.15),    # the hock, back
            (-1.35, s * 0.85, 0.08, 0.16, 0.11, 0.09)], SCALE)
        foot(g, hock, -1.12, s * 0.86, s, SCALE, HORN)
        # the forelegs: set wide, the elbows out and back, the weight forward on them
        fore = g.patch([(0.25, s * 0.62, 1.05), (0.47, s * 0.59, 1.1), (0.7, s * 0.55, 1.15)], (0, s * 0.6, -1))
        wrist = g.extrude(fore, [
            (0.55, s * 0.85, 1.0, 0.27, 0.31, 0.31),     # the upper arm
            (0.3, s * 0.95, 0.6, 0.21, 0.21, 0.21),      # the elbow
            (0.62, s * 0.92, 0.3, 0.17, 0.17, 0.17),
            (0.72, s * 0.9, 0.1, 0.16, 0.14, 0.14)], SCALE)
        foot(g, wrist, 0.92, s * 0.92, s, SCALE, HORN)
        # the horns, grown out of the brow, swept back; a short spike out of the jaw
        g.extrude([g.inset(g.face((2.42, s * 0.17, 3.38), (-0.4, s * 0.5, 1)), 0.12)], [
            (2.4, s * 0.24, 3.48, 0.07, 0.07, 0.07), (2.15, s * 0.3, 3.62, 0.055, 0.055, 0.055),
            (1.85, s * 0.32, 3.68, 0.04, 0.04, 0.04)], HORN, tip=(1.55, s * 0.3, 3.62), blunt=0.006)
        g.extrude([g.inset(g.face((2.4, s * 0.28, 3.05), (-0.5, s, 0)), 0.07)], [
            (2.32, s * 0.36, 3.02, 0.04, 0.04, 0.04)], HORN, tip=(2.05, s * 0.42, 2.98), blunt=0.005)
    # exactly symmetric, so the left half can stand for both
    bmesh.ops.symmetrize(g.bm, input=g.bm.verts[:] + g.bm.edges[:] + g.bm.faces[:], direction="X", dist=1e-3)
    return g


# -- the wing -------------------------------------------------------------------------------------

WING_SPEC = WingSpec(shoulder=SHOULDER, elbow=ELBOW, wrist=WRIST, thumb=THUMB, tips=TIPS, hip=HIP, lead=LEAD,
                     root_in=(0.75, 0.25, 2.15), root_out=(0.75, 0.45, 2.36), flank=(0.45, 0.45, 2.1),
                     front=(1.1, 0.4, 2.2), core=1.7, r_shoulder=R_SHOULDER, r_elbow=R_ELBOW, r_finger=R_FINGER,
                     skin=SKIN, bone=SCALE, membrane=WING, claw=HORN)


# -- horns, claws, spines -------------------------------------------------------------------------

# -- the whole ------------------------------------------------------------------------------------

def build(name="Dragon", coll="Creatures"):
    g = body()
    bvh = BVHTree.FromBMesh(g.bm)                  # for the wing's root, before the cut
    wing_bm = wing(WING_SPEC, bvh)
    keep_left(g.bm)
    tune(g.bm, tuning(__file__, "Dragon"))
    root = to_object(g.bm, name, COLOURS, coll, clip=True)
    to_object(wing_bm, name + "_Wing", COLOURS, coll, root)
    return root


ASSETS = {"Dragon": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
