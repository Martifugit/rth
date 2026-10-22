"""The fen rat: a big reed-bed rat, knee-high to the player. Second test model of the Blender art
pipeline, the first creature.

v2, after the owner's review: drawn the way the reference draws a rat. One hunched wedge for head
and body, spikes for legs, a tapered stick for a tail, the eyes painted onto the head's faces.
Everything that grows from the body has its base buried in it.

Coordinates are written as (forward, side, up) from the rat's own point of view and turned into
Blender's with P(): the rat faces Blender -Y and stands on tile (0, 0)."""
import math
import os
import sys

import bmesh

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

COLOURS = ["CliffRock", "Hide", "Blood"]
FUR, BARE, EYE = range(3)


def P(f, s, u):
    return (0.5 + s, 0.5 - f, u)


def sec(f, u, w, h):
    return (P(f, 0, u), w, h)


def build():
    A.reset()
    bm = bmesh.new()

    # Head and body: one tube, nose to rump, highest over the hips. A ridge along the spine
    # (roll 90 degrees puts a vertex on top).
    A.loft(bm, [
        sec(0.47, 0.10, 0, 0),
        sec(0.42, 0.11, 0.035, 0.035),
        sec(0.28, 0.15, 0.085, 0.075),
        sec(0.16, 0.17, 0.11, 0.10),
        sec(0.02, 0.21, 0.16, 0.16),
        sec(-0.20, 0.25, 0.19, 0.21),
        sec(-0.38, 0.20, 0.14, 0.15),
        sec(-0.46, 0.16, 0, 0),
    ], sides=6, roll=math.pi / 2, mats=FUR)

    for s in (-1, 1):
        # Legs: one spike each, the base well inside the body, the tip on the ground.
        A.spike(bm, P(0.10, s * 0.07, 0.16), 0.05, P(0.17, s * 0.11, 0.0), mat=FUR)
        A.spike(bm, P(-0.24, s * 0.09, 0.14), 0.065, P(-0.17, s * 0.14, 0.0), mat=FUR)
        # Ears: three-sided spikes rising out of the head, the inner face bare.
        ear = A.spike(bm, P(0.21, s * 0.045, 0.18), 0.05, P(0.19, s * 0.11, 0.32), sides=3, mat=FUR)
        max(ear, key=lambda f: -f.calc_center_median().y).material_index = BARE  # the face looking forward

    # Tail: a tapered stick from inside the rump, drooping to the ground.
    A.loft(bm, [
        (P(-0.36, 0, 0.17), 0.04, 0.04),
        (P(-0.50, 0, 0.13), 0.035, 0.035),
        (P(-0.72, 0, 0.07), 0.026, 0.026),
        (P(-0.92, 0, 0.035), 0.016, 0.016),
        (P(-1.06, 0, 0.025), 0, 0),
    ], sides=4, roll=math.pi / 4, mats=BARE)

    # Eyes: one triangle each on the side of the head.
    for s in (-1, 1):
        A.paint(bm, P(0.33, s * 0.07, 0.17), EYE, facing=(s, 0, 0.3), size=0.35)

    A.finish(bm)
    return [A.mesh_from("FenRat", bm, COLOURS, smooth_angle=55)]


objs = build()
cam = A.preview_rig(focus=(0.5, 0.5, 0.4), distance=6.0, pitch=40, yaw=-30)
result = A.report(objs)
