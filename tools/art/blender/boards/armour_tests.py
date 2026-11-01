r"""Armour capability tests (2026-11-01): the drake's bare body plated seven ways, each testing one hard
case for artlib/armour.py and artlib/plates.py, with numbers (armour.report) beside the renders.

- T1 Limbs     plates on the legs from the legs' own axes: a forearm cuff, an upper-arm plate, an elbow
               cap, a shin bracer, a knee cap, a hock cap, a thigh plate
- T2 Wrap      plates that wrap round a part: collars on the neck, shingled rings down the tail
- T3 Flow      a generated ridge of keeled plates from the skull down the neck and back, following the
               neck's curve, sized by the body's girth
- T4 Head      brow plates with horns, cheek plates, a row of jaw plates, a snout band, a skull cap
- T5 Under     belly scutes: part-bands across the underside from the chest down the tail
- T6 Armour    a whole armour generated from a few rules: a ridge, two staggered flank rows, belly
               scutes, tail rings, a collar, leg cuffs and caps, the head
- T7 Moving    T6 with the neck raised, the tail lowered and the knees bent, body and plates together

    import runpy; ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\armour_tests.py"); ns["run"]()
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
from mathutils.bvhtree import BVHTree  # noqa: E402

from artlib import armour as AR  # noqa: E402
from artlib.beast import keep_left, to_object  # noqa: E402
from artlib.grow import P  # noqa: E402
from artlib.plates import KEELED, PROFILE, outline, slab  # noqa: E402
from assets.creatures.common import drake as D  # noqa: E402

SKIN, HORN, PLATE = range(3)
N = 14
PI = math.pi


def axes():
    spine = [D.TAIL_TIP] + [r[:3] for r in reversed(D.TAIL)] + [r[:3] for r in D.TORSO] + [r[:3] for r in D.HEAD] + [D.NOSE]
    hf, hs, hu = D.HIND_ROOT
    hind = [(hf, 0.25, hu + 0.05), D.HIND_ROOT] + [r[:3] for r in D.HIND] + [(D.HIND[-1][0] + 0.12, D.HIND[-1][1], 0.04)]
    ff, fs_, fu = D.FORE_ROOT
    fore = [(ff, 0.25, fu + 0.05), D.FORE_ROOT] + [r[:3] for r in D.FORE] + [(D.FORE[-1][0] + 0.12, D.FORE[-1][1], 0.04)]
    return (AR.Axis(spine, ref=(0, 0, 1)), AR.Axis(hind, ref=(1, 0, 0)), AR.Axis(fore, ref=(1, 0, 0)))


def d_at(axis, point):
    return axis.nearest(P(*point))[0]


def ring_d(axis, rings, i):
    return d_at(axis, rings[i][:3])


class Ctx:
    def __init__(self, body_bm):
        bvh = BVHTree.FromBMesh(body_bm)
        sa, ha, fa = axes()
        self.spine, self.hind, self.fore = AR.Frame(bvh, sa), AR.Frame(bvh, ha), AR.Frame(bvh, fa)
        self.sd = lambda f: d_at(sa, (f, 0, D.axis(f)))           # spine distance at f


def plate_on(bm, fr, d, theta, length, width, thick, **kw):
    """A slab on a part at (d, theta), lying along the part."""
    shape = kw.pop("shape", outline(N, front=0.25, back=0.55, lobes=5, lobe=0.06))
    prof = kw.pop("profile", PROFILE)
    kw.setdefault("mat", PLATE)
    c = fr.on(d, theta)
    return slab(bm, fr.local(c), c, fr.tangent(d), length, width, shape, thick, prof, **kw)


# -- the tests ------------------------------------------------------------------------------------

def t_limbs(bm, c):
    fa, ha = c.fore.axis, c.hind.axis
    de, dw = ring_d(fa, D.FORE, 2), ring_d(fa, D.FORE, 4)               # elbow, wrist
    AR.band(bm, c.fore, de + (dw - de) * 0.55, (dw - de) * 0.55, 0.035, mat=PLATE, tilt=0.4)      # forearm cuff
    du = ring_d(fa, D.FORE, 0)
    plate_on(bm, c.fore, (du + de) / 2, PI / 2, 0.3, 0.22, 0.05)                                  # upper arm
    plate_on(bm, c.fore, de, PI, 0.18, 0.16, 0.045, shape=outline(N, front=0.2, back=0.2))       # elbow cap
    dk, dh = ring_d(ha, D.HIND, 1), ring_d(ha, D.HIND, 3)               # knee, hock
    AR.band(bm, c.hind, (dk + dh) / 2, (dh - dk) * 0.6, 0.035, theta0=-0.6 * PI, theta1=0.6 * PI,
            mat=PLATE, taper=0.5)                                                                # shin bracer
    plate_on(bm, c.hind, dk, 0.0, 0.2, 0.18, 0.05, shape=outline(N, front=0.2, back=0.2))        # knee cap
    plate_on(bm, c.hind, dh, PI, 0.14, 0.12, 0.04, shape=outline(N, front=0.2, back=0.2))        # hock cap
    dt = ring_d(ha, D.HIND, 0)
    plate_on(bm, c.hind, dt, PI / 2, 0.42, 0.3, 0.06)                                            # thigh


def t_wrap(bm, c):
    for f, ln in ((1.32, 0.2), (1.75, 0.16)):                                                    # collars
        AR.band(bm, c.spine, c.sd(f), ln, 0.045, n=20, mat=PLATE, tilt=0.4)
    for d, L, g in AR.row(c.spine, c.sd(-1.75), c.sd(-5.3), lambda g: max(0.14, 1.5 * g), overlap=0.2):
        AR.band(bm, c.spine, d, L, max(0.018, 0.22 * g), n=16, mat=PLATE, tilt=0.3, lip=1.6)      # tail rings


def ridge(bm, c, f0, f1, k=1.0):
    for d, L, g in AR.row(c.spine, c.sd(f0), c.sd(f1), lambda g: 0.55 * g + 0.12, overlap=0.28):
        plate_on(bm, c.spine, d, 0.0, L, 0.75 * g * k, 0.07 * g + 0.02, profile=KEELED, ridge=0.5, tilt=0.3,
                 lip_back=2.2,
                 shape=outline(N, front=0.35, back=0.5, lobes=5, lobe=0.06))


def t_flow(bm, c):
    ridge(bm, c, 2.3, -1.7)


def head(bm, c):
    sp = c.spine
    plate_on(bm, sp, c.sd(2.42), 0.0, 0.42, 0.34, 0.06, shape=outline(N, front=0.25, back=0.6, lobes=5, lobe=0.07),
             ridge=0.3, facets=4, facet=0.2)                                                     # skull cap
    plate_on(bm, sp, c.sd(2.62), 0.55, 0.22, 0.14, 0.05, shape=outline(N, front=0.5, back=0.3),
             horn=dict(length=0.32, rake=1.2, spread=0.35, tip=0.012, mat=HORN, r=0.045))       # brow, horned
    plate_on(bm, sp, c.sd(2.55), 1.45, 0.24, 0.18, 0.04, tilt=0.5)                               # cheek
    for d, L, g in AR.row(sp, c.sd(3.05), c.sd(2.45), lambda g: 0.12, overlap=0.15, theta=2.2):  # jaw row
        plate_on(bm, sp, d, 2.2, L, 0.09, 0.03, shape=outline(N, front=0.2, back=0.5), lip_back=1.5,
                 horn=dict(length=0.06, rake=0.6, tip=0.006, mat=HORN, r=0.025))
    AR.band(bm, sp, c.sd(3.05), 0.16, 0.03, theta0=-0.75, theta1=0.75, n=8, mat=PLATE, taper=0.6)  # snout band


def t_head(bm, c):
    head(bm, c)


def belly(bm, c, f0=0.95, f1=-4.0):
    for d, L, g in AR.row(c.spine, c.sd(f0), c.sd(f1), lambda g: 0.35 * g + 0.06, overlap=0.15, theta=PI):
        AR.band(bm, c.spine, d, L, 0.025, theta0=PI - 0.6, theta1=PI + 0.6, n=8, mat=PLATE, tilt=0.2,
                taper=0.5, lip=1.6)


def t_under(bm, c):
    belly(bm, c)


def armour(bm, c):
    """Rules: every plate sized by the girth where it lies; rows overlap a quarter; flank rows staggered
    half a plate behind the ridge; tail and neck in rings; joints capped; the head as in T4."""
    head(bm, c)
    ridge(bm, c, 2.2, -1.65)
    rows = [0.62, 1.12, 1.62]                     # flank rows: angles round the spine, each plate as wide as
    for r_i, theta in enumerate(rows):            # the arc to its neighbour row, so the rows meet
        dth = rows[1] - rows[0]
        places = AR.row(c.spine, c.sd(1.0), c.sd(-1.5), lambda g: 0.55 * g + 0.08, overlap=0.25, theta=theta)
        for i, (d, L, g) in enumerate(places):
            d2 = d - (L * 0.375 if r_i % 2 else 0.0)                                             # staggered
            w = g * dth * 1.25
            plate_on(bm, c.spine, d2, theta, L * (1 - 0.12 * r_i), w * (1 - 0.1 * r_i), 0.04 * g + 0.015,
                     tilt=0.3, lip_back=1.0, lip_down=1.0, profile=KEELED, ridge=0.3,
                     shape=outline(N, front=0.15, back=0.6, square=0.35, lobes=3, lobe=0.05))
    AR.band(bm, c.spine, c.sd(1.32), 0.2, 0.045, n=20, mat=PLATE, tilt=0.4)                     # collar
    for d, L, g in AR.row(c.spine, c.sd(-1.75), c.sd(-5.3), lambda g: max(0.14, 1.5 * g), overlap=0.2):
        AR.band(bm, c.spine, d, L, max(0.018, 0.22 * g), n=16, mat=PLATE, tilt=0.3, lip=1.6)
    belly(bm, c, 0.95, -1.6)
    t_limbs(bm, c)


def t_armour(bm, c):
    armour(bm, c)


def moving(body, plates):
    w_neck = lambda f, s, u: AR.smooth(f, 1.1, 1.6) * (abs(s) < 0.6)      # noqa: E731
    AR.bend([body, plates], (1.2, 0, 1.26), (0, 1, 0), math.radians(-25), w_neck)
    w_tail = lambda f, s, u: AR.smooth(-f, 1.55, 2.2) * (abs(s) < 0.5)    # noqa: E731
    AR.bend([body, plates], (-1.6, 0, 1.05), (0, 1, 0), math.radians(-20), w_tail)
    for side in (1, -1):
        kf, kw, ku = D.HIND[1][:3]
        w_knee = lambda f, s, u: AR.smooth(ku - u, 0.0, 0.12) * (s * side > 0.55) * (f < -0.5)  # noqa: E731
        AR.bend([body, plates], (kf, kw * side, ku), (0, 1, 0), math.radians(-20), w_knee)


TESTS = [("T1_Limbs", t_limbs, None), ("T2_Wrap", t_wrap, None), ("T3_Flow", t_flow, None),
         ("T4_Head", t_head, None), ("T5_Under", t_under, None), ("T6_Armour", t_armour, None),
         ("T7_Moving", t_armour, moving)]


def build(name, fn, after, x, y=14.0):
    g = D.body(crown=False, spine=False)
    bmesh.ops.recalc_face_normals(g.bm, faces=g.bm.faces[:])
    c = Ctx(g.bm)
    bm = bmesh.new()
    fn(bm, c)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if after:
        after(g.bm, bm)
    stats = AR.report(g.bm, bm)
    keep_left(g.bm)
    keep_left(bm)
    root = to_object(g.bm, name, D.COLOURS, "Armour", clip=True)
    to_object(bm, name + "_Plates", D.COLOURS, "Armour", root, clip=True, smooth=20)
    root.location = (x, y, 0)
    return root, stats


def run(only=None, start=0.0, gap=7.0):
    import bpy
    out = {}
    for i, (name, fn, after) in enumerate(TESTS):
        if only and name not in only:
            continue
        old = bpy.data.objects.get(name)
        if old:
            for o in [old] + list(old.children_recursive):
                me = o.data
                bpy.data.objects.remove(o, do_unlink=True)
                bpy.data.meshes.remove(me)
        _, stats = build(name, fn, after, start + i * gap)
        out[name] = stats
    return out
