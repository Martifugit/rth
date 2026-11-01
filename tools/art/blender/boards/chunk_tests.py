r"""Large-chunk tests (2026-11-01): the drake's bare body armoured with a few large plates, each covering
much of a part, to see how far one plate can reach (artlib/armour.chunk against plates.slab).

- C0 Flat      one slab the size of a carapace over the back and flanks (the old way, for comparison)
- C1 Carapace  the same as a chunk: shoulders to hips, round the back and down both flanks
- C2 Parts     one chunk per part: the neck's top following its curve, a helm over the skull, three long
               tail plates shingled
- C3 Limbs     pauldrons from the back onto the upper arm, thigh guards from the hips onto the thigh
- C4 Armour    all of it together: a heavily armoured drake in a handful of pieces

    import runpy; ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\chunk_tests.py"); ns["run"]()
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if not __name__.startswith("boards."):            # run directly (not imported): pick up edits to the kits
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "boards")]:
        del sys.modules[_m]

import bmesh  # noqa: E402

from artlib import armour as AR  # noqa: E402
from artlib.beast import keep_left, to_object  # noqa: E402
from artlib.plates import PROFILE, outline, slab  # noqa: E402
from assets.creatures.common import drake as D  # noqa: E402
from boards import armour_tests as T  # noqa: E402

SKIN, HORN, PLATE = range(3)
PI = math.pi
N = 22                                            # points round a big chunk's outline (n / 2 odd)


def flat(bm, c):
    sp = c.spine
    d0, d1 = c.sd(-1.1), c.sd(0.9)
    centre = sp.on((d0 + d1) / 2, 0.0)
    slab(bm, sp.local(centre), centre, sp.tangent((d0 + d1) / 2), d1 - d0, 1.5,
         outline(N, front=0.2, back=0.3, square=0.4, lobes=7, lobe=0.05), 0.06, PROFILE, mat=PLATE)


def carapace(bm, c):
    AR.chunk(bm, c.spine, c.sd(-1.15), c.sd(0.95), -1.9, 1.9, 0.07,
             outline(N, front=0.15, back=0.25, square=0.45, lobes=7, lobe=0.05),
             dome=0.6, ridge=0.8, facets=6, facet=0.25, mat=PLATE)


def parts(bm, c):
    AR.chunk(bm, c.spine, c.sd(1.2), c.sd(2.25), -1.25, 1.25, 0.05,
             outline(N, front=0.35, back=0.2, square=0.3, lobes=5, lobe=0.05),
             ridge=0.8, facets=4, facet=0.2, mat=PLATE)                                   # the neck
    AR.chunk(bm, c.spine, c.sd(2.25), c.sd(3.0), -1.6, 1.6, 0.05,
             outline(N, front=0.55, back=0.25, square=0.3, lobes=5, lobe=0.06),
             dome=0.4, ridge=0.6, facets=4, facet=0.2, mat=PLATE)                         # the helm
    ends = [-1.2, -2.3, -3.4, -4.6]
    for a, b in zip(ends, ends[1:]):                                                     # the tail
        AR.chunk(bm, c.spine, c.sd(b) - 0.08, c.sd(a), -1.4, 1.4, 0.04,
                 outline(N, front=0.3, back=0.2, square=0.35, lobes=5, lobe=0.05),
                 ridge=0.8, lip_back=1.2, mat=PLATE)


def limbs(bm, c):
    fa, ha = c.fore.axis, c.hind.axis
    de, du = T.ring_d(fa, D.FORE, 2), T.ring_d(fa, D.FORE, 0)   # from where the leg leaves the body
    AR.chunk(bm, c.fore, du - 0.04, de * 0.92, PI / 2 - 1.35, PI / 2 + 1.35, 0.05,
             outline(N, front=0.3, back=0.3, square=0.3, lobes=5, lobe=0.06),
             dome=0.5, facets=4, facet=0.2, mat=PLATE)                                   # pauldron
    dk, dt = T.ring_d(ha, D.HIND, 1), T.ring_d(ha, D.HIND, 0)
    AR.chunk(bm, c.hind, dt - 0.06, dk * 0.92, PI / 2 - 1.35, PI / 2 + 1.35, 0.05,
             outline(N, front=0.3, back=0.3, square=0.3, lobes=5, lobe=0.06),
             dome=0.5, facets=4, facet=0.2, mat=PLATE)                                   # thigh guard


def whole(bm, c):
    carapace(bm, c)
    parts(bm, c)
    limbs(bm, c)


TESTS = [("C0_Flat", flat), ("C1_Carapace", carapace), ("C2_Parts", parts), ("C3_Limbs", limbs),
         ("C4_Armour", whole)]


def run(only=None, start=0.0, gap=7.0, y=24.0):
    import bpy
    out = {}
    for i, (name, fn) in enumerate(TESTS):
        if only and name not in only:
            continue
        old = bpy.data.objects.get(name)
        if old:
            for o in [old] + list(old.children_recursive):
                me = o.data
                bpy.data.objects.remove(o, do_unlink=True)
                bpy.data.meshes.remove(me)
        g = D.body(crown=False, spine=False)
        bmesh.ops.recalc_face_normals(g.bm, faces=g.bm.faces[:])
        c = T.Ctx(g.bm)
        bm = bmesh.new()
        fn(bm, c)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        out[name] = AR.report(g.bm, bm)
        keep_left(g.bm)
        keep_left(bm)
        root = to_object(g.bm, name, D.COLOURS, "Chunks", clip=True)
        to_object(bm, name + "_Plates", D.COLOURS, "Chunks", root, clip=True, smooth=20)
        root.location = (start + i * gap, y, 0)
    return out
