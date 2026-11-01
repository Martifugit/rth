r"""Plating study (2026-11-01): the drake's bare body four times over, each plated a different way with
artlib/plates.py, to find how bone plating and the features that grow out of it should be built.

- A  Shields   a shield-shaped skull cap, brow plates that thicken into swept horns, keeled leaf plates
               down the neck and tail, big paired shields over the shoulders (each with a short spike)
               and smaller ones down the back
- B  Ridge     one row of keeled plates down the centre line from the skull to the tail, every plate
               thickening into a raked spine, sized by a curve that peaks over the shoulders; round
               plates paired over the shoulders and hips
- C  Scales    three staggered rows of pointed scales a side, shingled, smaller toward the flanks; a
               keeled ridge down the middle
- D  Crown     the head: a skull cap, a fan of spike plates round the back of the skull, brow ridges,
               cheek plates and a row of small jaw plates; plain shields down the back

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\plating_study.py")
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "boards")]:
    del sys.modules[_m]

import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

from artlib.beast import keep_left, to_object  # noqa: E402
from artlib.grow import P  # noqa: E402
from artlib.grow import Grow  # noqa: E402
from artlib.plates import FLAT, KEELED, KNOB, PROFILE, Frame, outline, slab  # noqa: E402
from assets.creatures.common import drake  # noqa: E402

SKIN, HORN, PLATE = range(3)
N = 14                                          # points round a plate: chunky facets
SMOOTH = 20                                     # plates shaded nearly flat: their facets catch the light
COLOURS = drake.COLOURS
BACK = P(-1, 0, 0)


def top(frame, f, s=0.0, up=1.0):
    """The body's surface at f, seen from the centre line toward (s, up), and the outward direction."""
    q = frame.axis_pt(P(f, 0, 0))
    surf, d = frame.cast(q + P(0, s, up))
    return surf, d


def fsu(v):
    return (-v.y, v.x, v.z)


def spike(frame, f, s, up, length, rake=0.7, spread=0.0):
    """A horn spec for a plate at (f, s, up): out from the body by `length`, raked back."""
    surf, d = top(frame, f, s, up)
    out = (d + Vector((spread, 0, 0))).normalized() if spread else d
    end = surf + out * length + BACK * (length * rake)
    pull = surf + out * (length * 0.55) + BACK * (length * rake * 0.25)
    return dict(end=fsu(end), pull=fsu(pull))


def skull_cap(bm, fr, horns=True):
    slab(bm, fr, (2.47, 0, 1.95), (1, 0, -0.12), 0.5, 0.4, outline(N, front=0.25, back=0.65, bulge=0.15),
         0.06, PROFILE, ridge=0.35, mat=PLATE)
    if horns:                                     # brow plates thickening into swept horns
        h = spike(fr, 2.52, 0.2, 0.75, 0.3, rake=1.1)
        slab(bm, fr, (2.52, 0.2, 1.75), (1, 0, 0.1), 0.2, 0.15, outline(N, front=0.1, back=0.3), 0.05, PROFILE,
             mat=PLATE, horn=dict(h, tip=0.012, rings=7, mat=HORN))


def variant_a(bm, fr):
    skull_cap(bm, fr)
    for f, ln, w in ((2.08, 0.3, 0.3), (1.8, 0.31, 0.34), (1.52, 0.32, 0.38)):
        slab(bm, fr, (f, 0, 1.9), (1, 0, 0), ln, w, outline(N, front=0.5, back=0.5), 0.05, KEELED, ridge=0.6,
             tilt=0.5, mat=PLATE)
    h = spike(fr, 0.85, 0.3, 1.0, 0.24)
    slab(bm, fr, (0.88, 0.3, 1.8), (1, 0, 0), 0.84, 0.62, outline(N, front=0.1, back=0.7, bulge=0.25), 0.11,
         PROFILE, tilt=0.6, mat=PLATE, horn=dict(h, tip=0.01, rings=7, mat=HORN, r=0.07))
    for f, k in ((0.3, 0.9), (-0.22, 0.82), (-0.72, 0.74), (-1.15, 0.62)):
        slab(bm, fr, (f, 0.28, 1.8), (1, 0, 0), 0.78 * k, 0.6 * k, outline(N, front=0.1, back=0.65, bulge=0.2),
             0.1 * k, PROFILE, tilt=0.7, mat=PLATE)
    for i, f in enumerate((-1.35, -1.8, -2.3, -2.85, -3.45, -4.05)):
        k = 0.8 - i * 0.1
        slab(bm, fr, (f, 0, 1.7), (1, 0, -0.1), 0.42 * k, 0.4 * k, outline(N, front=0.5, back=0.5), 0.07 * k,
             KEELED, ridge=0.6, tilt=0.5, mat=PLATE)


def variant_b(bm, fr):
    skull_cap(bm, fr)
    fs = [2.12, 1.82, 1.5, 1.16, 0.82, 0.46, 0.1, -0.28, -0.68, -1.08, -1.5, -1.95, -2.45, -3.0, -3.6, -4.2]
    peak = 0.82
    for f in fs:
        k = max(0.3, 1.0 - abs(f - peak) / 4.2) if f < peak else max(0.45, 1.0 - (f - peak) / 2.6)
        h = spike(fr, f, 0, 1.0, 0.3 * k, rake=0.8)
        slab(bm, fr, (f, 0, 1.9), (1, 0, -0.05), 0.36 * k, 0.3 * k, outline(N, front=0.4, back=0.5), 0.06 * k,
             KEELED, ridge=0.5, mat=PLATE, horn=dict(h, tip=0.008, rings=7, mat=HORN))
    for f, s, u, k in ((0.85, 0.42, 0.9, 1.0), (0.4, 0.45, 0.85, 0.85), (-1.15, 0.4, 0.8, 0.9)):
        slab(bm, fr, (f, s, u + 1.0), (1, 0, 0), 0.34 * k, 0.3 * k, outline(N, square=0.3), 0.06 * k, FLAT,
             mat=PLATE)


def variant_c(bm, fr):
    skull_cap(bm, fr)
    rows = [(0.17, 1.0, 0.0, 1.0), (0.48, 0.75, 0.2, 0.8)]
    for r, (s, up, shift, k0) in enumerate(rows):
        f = 1.2 - shift
        while f > -1.4:
            k = k0 * (0.75 + 0.35 * max(0.0, 1 - abs(f - 0.3) / 1.4))   # largest over the middle of the back
            prof = KNOB if (r == 0 and k > 0.9) else PROFILE   # the biggest scales rise into a low knob
            slab(bm, fr, (f, s, up + 1.0), (1, 0, -0.05), 0.42 * k, 0.36 * k, outline(N, front=0.05, back=0.85),
                 0.06 * k, prof, tilt=0.9, mat=PLATE)
            f -= 0.34 * k
    f, i = 1.25, 0
    while f > -4.2:
        k = 0.85 if f > -1.3 else max(0.35, 0.85 - (-1.3 - f) * 0.17)
        slab(bm, fr, (f, 0, 1.9), (1, 0, -0.05), 0.3 * k, 0.24 * k, outline(N, front=0.5, back=0.6), 0.06 * k,
             KEELED, ridge=0.8, tilt=0.4, mat=PLATE)
        f -= 0.3 if f > -1.3 else 0.42


def variant_d(bm, fr):
    skull_cap(bm, fr, horns=False)
    for i, (deg, length) in enumerate(((15, 0.4), (40, 0.36), (65, 0.3), (92, 0.24), (118, 0.17))):
        a = math.radians(deg)
        s, up = math.sin(a), math.cos(a)
        h = spike(fr, 2.24, s, up, length, rake=1.4)
        slab(bm, fr, (2.24, s, up + 1.48), (1, 0, 0), 0.19, 0.15 - i * 0.008, outline(N, front=0.2, back=0.4),
             0.05, PROFILE, mat=PLATE, horn=dict(h, tip=0.01, rings=7, mat=HORN))
    slab(bm, fr, (2.68, 0.18, 1.72), (1, 0, -0.25), 0.3, 0.1, outline(N, front=0.6, back=0.3), 0.04, KEELED,
         ridge=0.6, mat=PLATE)                                    # brow ridge
    slab(bm, fr, (2.5, 0.35, 1.45), (1, 0, -0.1), 0.26, 0.2, outline(N, front=0.3, back=0.5), 0.04, PROFILE,
         tilt=0.5, mat=PLATE)                                     # cheek
    for i, f in enumerate((2.95, 2.75, 2.55)):                   # jaw
        h = spike(fr, f, 0.6, -0.35, 0.06 + i * 0.015, rake=0.6)
        slab(bm, fr, (f, 0.6, 1.1), (1, 0, -0.1), 0.12, 0.08, outline(N, front=0.2, back=0.4), 0.03, PROFILE,
             mat=PLATE, horn=dict(h, tip=0.006, rings=7, mat=HORN))
    for f, k in ((0.88, 1.0), (0.3, 0.85), (-0.25, 0.75), (-0.8, 0.65)):
        slab(bm, fr, (f, 0.27, 1.8), (1, 0, 0), 0.66 * k, 0.48 * k, outline(N, front=0.1, back=0.65, bulge=0.2),
             0.09 * k, PROFILE, tilt=0.6, mat=PLATE)


def variant_e(bm, fr):
    """A few huge plates over the whole back, after the reference: a saddle over the shoulders and one over
    the hips (each one plate across the centre line), neck plates, tail plates, horns from the skull."""
    skull_cap(bm, fr)
    for f, ln, w in ((2.05, 0.34, 0.5), (1.72, 0.36, 0.56), (1.4, 0.36, 0.62)):
        slab(bm, fr, (f, 0, 1.9), (1, 0, 0), ln, w, outline(N, front=0.3, back=0.5, square=0.2), 0.07, PROFILE,
             ridge=0.3, tilt=0.6, mat=PLATE)
    h = spike(fr, 0.75, 0.33, 0.95, 0.22)
    slab(bm, fr, (0.62, 0, 1.9), (1, 0, 0), 1.2, 1.25, outline(N, front=0.2, back=0.45, square=0.35, bulge=0.15),
         0.14, PROFILE, ridge=0.25, tilt=0.5, mat=PLATE)                         # the shoulder saddle
    slab(bm, fr, (0.8, 0.3, 1.8), (1, 0, 0), 0.34, 0.28, outline(N, front=0.2, back=0.6), 0.08, PROFILE, lift=0.12,
         mat=PLATE, horn=dict(length=0.26, rake=0.7, spread=0.2, tip=0.012, mat=HORN, r=0.065))         # a horned boss on it
    slab(bm, fr, (-0.55, 0, 1.9), (1, 0, 0), 1.0, 1.05, outline(N, front=0.3, back=0.6, square=0.3), 0.12,
         PROFILE, ridge=0.3, tilt=0.5, mat=PLATE)                                # the hip saddle
    for i, f in enumerate((-1.3, -1.85, -2.45, -3.1, -3.75)):
        k = 0.85 - i * 0.12
        slab(bm, fr, (f, 0, 1.7), (1, 0, -0.1), 0.52 * k, 0.6 * k, outline(N, front=0.3, back=0.5, square=0.2),
             0.09 * k, PROFILE, ridge=0.4, tilt=0.6, mat=PLATE)


def variant_f(bm, fr):
    """E rebuilt as broken, faceted slabs: lobed outlines, stepped tops, a row of spikes along the shoulder
    saddle's rear edge, horned bosses, a lobed crown cap."""
    g = Grow()
    g.bm.free()
    g.bm = bm
    cap = slab(bm, fr, (2.47, 0, 1.95), (1, 0, -0.12), 0.52, 0.44, outline(N, front=0.25, back=0.5, lobes=5, lobe=0.08),
               0.07, PROFILE, ridge=0.3, facets=4, facet=0.25, mat=PLATE)
    h = spike(fr, 2.5, 0.2, 0.75, 0.32, rake=1.1)
    slab(bm, fr, (2.5, 0.2, 1.75), (1, 0, 0.1), 0.22, 0.17, outline(N, front=0.1, back=0.3, lobes=3, lobe=0.06), 0.05,
         PROFILE, mat=PLATE, horn=dict(h, tip=0.012, rings=7, mat=HORN, r=0.05))
    for f, ln, w in ((2.05, 0.36, 0.52), (1.72, 0.38, 0.58), (1.38, 0.38, 0.64)):
        slab(bm, fr, (f, 0, 1.9), (1, 0, 0), ln, w, outline(N, front=0.25, back=0.55, square=0.15, lobes=5, lobe=0.07),
             0.07, PROFILE, ridge=0.35, tilt=0.6, facets=2, facet=0.2, mat=PLATE)
    saddle = slab(bm, fr, (0.62, 0, 1.9), (1, 0, 0), 1.2, 1.25,
                  outline(N, front=0.2, back=0.4, square=0.3, bulge=0.15, lobes=7, lobe=0.1),
                  0.15, PROFILE, ridge=0.25, tilt=0.5, facets=6, facet=0.25, mat=PLATE)
    for s_, up_ in ((0.2, 1.0), (0.42, 0.85)):                      # spikes along its rear edge: plates of their own
        h = spike(fr, 0.06, s_, up_, 0.2, rake=1.2)
        slab(bm, fr, (0.06, s_, up_ + 1.0), (1, 0, 0), 0.17, 0.14, outline(N, front=0.2, back=0.4), 0.06, PROFILE,
             lift=0.05, mat=PLATE, horn=dict(h, tip=0.01, mat=HORN, r=0.045))
    h = spike(fr, 0.75, 0.3, 1.0, 0.34)
    slab(bm, fr, (0.8, 0.3, 1.8), (1, 0, 0), 0.34, 0.28, outline(N, front=0.2, back=0.6, lobes=3, lobe=0.06), 0.09,
         PROFILE, lift=0.13, mat=PLATE, horn=dict(length=0.26, rake=0.7, spread=0.2, tip=0.012, mat=HORN, r=0.06))
    slab(bm, fr, (-0.55, 0, 1.9), (1, 0, 0), 1.0, 1.05, outline(N, front=0.3, back=0.5, square=0.3, lobes=7, lobe=0.06),
         0.13, PROFILE, ridge=0.3, tilt=0.5, facets=6, facet=0.25, mat=PLATE)
    for i, f in enumerate((-1.3, -1.85, -2.45, -3.1, -3.75)):
        k = 0.85 - i * 0.12
        slab(bm, fr, (f, 0, 1.7), (1, 0, -0.1), 0.52 * k, 0.6 * k,
             outline(N, front=0.35, back=0.5, square=0.2, lobes=5, lobe=0.07), 0.09 * k, PROFILE, ridge=0.45,
             tilt=0.6, facets=2, facet=0.2, mat=PLATE)


VARIANTS = [("PlateA", variant_a), ("PlateB", variant_b), ("PlateC", variant_c), ("PlateD", variant_d),
            ("PlateE", variant_e), ("PlateF", variant_f)]


def build(name, fn, x):
    g = drake.body(crown=False, spine=False)
    fr = Frame(BVHTree.FromBMesh(g.bm), drake.axis)
    bm = bmesh.new()
    fn(bm, fr)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    keep_left(g.bm)
    keep_left(bm)
    root = to_object(g.bm, name, COLOURS, "Study", clip=True)
    to_object(bm, name + "_Plates", COLOURS, "Study", root, clip=True, smooth=SMOOTH)
    root.location = (x, 0, 0)
    return root


def run(start=22.0, gap=6.0, only=None):
    import bpy
    out = []
    for i, (name, fn) in enumerate(VARIANTS):
        if only and name not in only:
            continue
        old = bpy.data.objects.get(name)
        if old:
            for o in [old] + list(old.children_recursive):
                me = o.data
                bpy.data.objects.remove(o, do_unlink=True)
                bpy.data.meshes.remove(me)
        out.append(build(name, fn, start + i * gap))
    return out


if __name__ in ("__main__", "<run_path>"):
    result = [o.name for o in run()]
