r"""The Moon-Bearer, silhouette study (2026-11-01): a boss that lives inside the walls of a hollow moon
fragment (the arena is its shell) and surfaces through the cracks. A pale, faintly glowing ghost of a
crab (after the ghost crab: pale, eyes on tall stalks) with long spindly legs (after the Japanese spider
crab), its legs crusted with the walls' moon rock.

Blockouts for choosing a direction, not finished topology: the crab coming out of a crack in a curved
section of shell wall, the crack lit from behind (the crab's own glow through the walls).

- A  Brute     lopsided claws (one huge), the front half heaving out of the main crack, legs out of two
               side cracks
- B  Ghost     matched slender claws, a flat wide body, tall eyestalks, three pairs of very long legs
- C  Reacher   the body barely out; legs groping out of many cracks (one high up), very long eyestalks,
               small claws

    import runpy; ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\boards\moonbearer_study.py"); ns["run"]()
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if not __name__.startswith("boards."):
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "boards")]:
        del sys.modules[_m]

import bmesh  # noqa: E402

from artlib.grow import Grow  # noqa: E402

COLOURS = ["Ice", "Chalk", "Moonlight", "Sable", "Unmade"]   # crab, rock crust, glow and eyes, claw tips, wall
CRAB, CRUST, GLOW, TIP, WALL = range(5)
R_WALL = 14.0                                    # the shell's curve: its walls lean in round the arena


def wall_f(s, u):
    """The wall's surface: a section of the hollow fragment's inside, curving toward the arena."""
    return (s * s + (u - 4.0) ** 2) / (2 * R_WALL)


def crack_main(s, u, half=2.6, top=6.0):
    if u > top:
        return False
    w = half * (1 - u / top) ** 0.55
    return abs(s - 0.35 * math.sin(u * 2.1)) < w


def slit(s0, u0, u1, half=0.4):
    def inside(s, u):
        if not u0 <= u <= u1:
            return False
        t = (u - u0) / (u1 - u0)
        return abs(s - s0 - 0.25 * math.sin(t * 6.0)) < half * math.sin(math.pi * t) ** 0.5
    return inside


def wall(cracks, step=0.4):
    """The wall section as a grid of quads with the cracks left open, and a glowing sheet behind it."""
    bm = bmesh.new()
    vs = {}
    s_vals = [i * step for i in range(-25, 26)]
    u_vals = [j * step for j in range(0, 28)]

    def v(i, j):
        if (i, j) not in vs:
            s, u = s_vals[i], u_vals[j]
            vs[i, j] = bm.verts.new((s, -wall_f(s, u), u))
        return vs[i, j]
    for i in range(len(s_vals) - 1):
        for j in range(len(u_vals) - 1):
            s, u = (s_vals[i] + s_vals[i + 1]) / 2, (u_vals[j] + u_vals[j + 1]) / 2
            if any(c(s, u) for c in cracks):
                continue
            bm.faces.new((v(i, j), v(i + 1, j), v(i + 1, j + 1), v(i, j + 1))).material_index = WALL
    glow = bmesh.new()
    a, b, c, d = [glow.verts.new(p) for p in ((-10, 1.6, 0), (10, 1.6, 0), (10, 1.6, 11), (-10, 1.6, 11))]
    glow.faces.new((a, b, c, d)).material_index = GLOW
    return bm, glow


def limb(g, pts, radii, mat=CRAB, knob=1.25):
    """A jointed limb: a tube through the joints (f, s, u) with a radius each, a slightly fatter ring at
    every inner joint, ending in a point."""
    rings = []
    for i, (p, r) in enumerate(zip(pts, radii)):
        if 0 < i < len(pts) - 1:
            q = pts[i - 1]
            a = tuple(p[k] + (q[k] - p[k]) * 0.12 for k in range(3))
            rings.append(a + (r, r, r))
            rings.append(tuple(p) + (r * knob, r * knob, r * knob))
            n = pts[i + 1]
            b = tuple(p[k] + (n[k] - p[k]) * 0.12 for k in range(3))
            rings.append(b + (r, r, r))
        else:
            rings.append(tuple(p) + (r, r, r))
    g.tube(rings, mat)


def leg(g, base, out, side, reach=1.0, lift=1.0, crust=True):
    """A spider-crab walking leg: up from the base to a high knee, then a long fall to a point on the
    floor. out: (f, s) direction it reaches in; side: +1 / -1."""
    bf, bs, bu = base
    of, os_ = out
    pts = [base,
           (bf + of * 0.6 * reach, bs + os_ * 0.6 * reach, bu + 0.9 * lift),
           (bf + of * 2.2 * reach, bs + os_ * 2.2 * reach, bu + 2.0 * lift),          # the knee, high
           (bf + of * 3.6 * reach, bs + os_ * 3.6 * reach, bu + 0.4 * lift),
           (bf + of * 4.4 * reach, bs + os_ * 4.4 * reach, 0.0)]                     # the point on the floor
    limb(g, pts, [0.2, 0.17, 0.14, 0.09, 0.015], CRUST if crust else CRAB)


def claw(g, base, way, size, side):
    """A claw: an arm forward to a wide flat hand, two fingers out of it, slightly open."""
    bf, bs, bu = base
    wf, ws = way
    k = size
    elbow = (bf + wf * 0.6 * k, bs + ws * 0.6 * k, bu + 0.5 * k)
    wrist = (bf + wf * 1.3 * k, bs + ws * 1.3 * k, bu + 0.35 * k)
    limb(g, [base, elbow, wrist], [0.18 * k, 0.15 * k, 0.13 * k])
    hand_end = (wrist[0] + wf * 0.75 * k, wrist[1] + ws * 0.75 * k, wrist[2] + 0.05 * k)
    g.tube([wrist + (0.18 * k, 0.12 * k, 0.12 * k),
            ((wrist[0] + hand_end[0]) / 2, (wrist[1] + hand_end[1]) / 2, wrist[2] + 0.05 * k, 0.3 * k, 0.17 * k, 0.17 * k),
            hand_end + (0.22 * k, 0.13 * k, 0.13 * k)], CRAB)
    for du, mat in ((0.12, TIP), (-0.12, TIP)):                                        # the fingers
        tip = (hand_end[0] + wf * 0.75 * k, hand_end[1] + ws * 0.75 * k, hand_end[2] + du * k * 1.6)
        mid = (hand_end[0] + wf * 0.35 * k, hand_end[1] + ws * 0.35 * k, hand_end[2] + du * k * 1.2)
        g.tube([hand_end + (0.1 * k, 0.08 * k, 0.08 * k), mid + (0.07 * k, 0.06 * k, 0.06 * k),
                tip + (0.012, 0.012, 0.012)], mat)


def eyestalk(g, base, height, lean):
    bf, bs, bu = base
    top = (bf + lean, bs, bu + height)
    g.tube([base + (0.09, 0.09, 0.09), ((bf + top[0]) / 2, bs, (bu + top[2]) / 2, 0.07, 0.07, 0.07),
            top + (0.07, 0.07, 0.07)], CRAB)
    g.tube([top + (0.07, 0.07, 0.07), (top[0] + 0.08, bs, top[2] + 0.14, 0.16, 0.16, 0.16),
            (top[0] + 0.12, bs, top[2] + 0.32, 0.01, 0.01, 0.01)], GLOW)               # the eye, pale as a moon


def body(g, rings):
    g.tube(rings, CRAB)


def variant_a():
    g = Grow(sides=10)
    body(g, [(-2.5, 0, 2.3, 1.2, 0.6, 0.5), (-1.5, 0, 2.35, 1.7, 0.85, 0.6), (-0.3, 0, 2.35, 1.85, 0.95, 0.65),
             (0.8, 0, 2.25, 1.55, 0.8, 0.6), (1.45, 0, 2.05, 1.0, 0.5, 0.45)])
    for s in (1, -1):
        eyestalk(g, (1.2, s * 0.4, 2.6), 1.1, 0.2)
    claw(g, (1.2, -1.0, 1.9), (0.85, -0.3), 2.0, -1)                                # the great claw
    claw(g, (1.2, 1.0, 1.9), (0.9, 0.25), 0.85, 1)                                  # the small one
    for s in (1, -1):
        leg(g, (0.6, s * 1.5, 2.2), (0.35, s * 0.94), reach=1.25, side=s)
        leg(g, (-0.2, s * 1.65, 2.2), (0.15, s * 1.0), reach=1.35, side=s)
    for s in (1, -1):                                                                 # out of the side cracks
        leg(g, (-0.4, s * 5.6, 2.0), (0.6, s * 0.8), reach=0.9, lift=0.8, side=s)
    cracks = [crack_main, slit(5.6, 0.6, 3.6), slit(-5.6, 0.6, 3.6)]
    return g, cracks


def variant_b():
    g = Grow(sides=10)
    body(g, [(-2.5, 0, 2.1, 1.4, 0.45, 0.4), (-1.4, 0, 2.15, 2.0, 0.6, 0.45), (-0.2, 0, 2.15, 2.15, 0.65, 0.5),
             (0.9, 0, 2.05, 1.8, 0.55, 0.45), (1.5, 0, 1.95, 1.2, 0.4, 0.35)])
    for s in (1, -1):
        eyestalk(g, (1.3, s * 0.55, 2.35), 1.9, 0.35)
    for s in (1, -1):
        claw(g, (1.3, s * 0.95, 1.8), (0.9, s * 0.3), 1.25, s)                       # matched, slender
    for s in (1, -1):
        for f, r in ((0.8, 1.55), (0.05, 1.7), (-0.7, 1.8)):
            leg(g, (f, s * 1.8, 2.05), (0.25 - (0.8 - f) * 0.2, s * 1.0), reach=r, lift=1.15, side=s)
    cracks = [lambda s, u: crack_main(s, u, half=2.9, top=5.2)]
    return g, cracks


def variant_c():
    g = Grow(sides=10)
    body(g, [(-2.5, 0, 2.4, 1.2, 0.6, 0.5), (-1.5, 0, 2.45, 1.6, 0.8, 0.6), (-0.4, 0, 2.45, 1.7, 0.85, 0.6),
             (0.35, 0, 2.35, 1.4, 0.7, 0.55), (0.75, 0, 2.2, 0.9, 0.45, 0.4)])
    for s in (1, -1):
        eyestalk(g, (0.6, s * 0.35, 2.7), 2.4, 0.5)
    for s in (1, -1):
        claw(g, (0.6, s * 0.8, 2.0), (0.9, s * 0.2), 0.7, s)
    for s in (1, -1):
        leg(g, (0.2, s * 1.4, 2.3), (0.4, s * 0.92), reach=1.3, side=s)
    for s0, u0 in ((5.4, 2.2), (-5.4, 2.0), (3.2, 1.2), (-3.4, 1.0), (-7.0, 3.0)):   # out of the side cracks
        s = 1 if s0 > 0 else -1
        leg(g, (-0.5, s0, u0), (0.65, s * 0.75), reach=1.15, lift=0.9, side=s)
    hi = (-0.4, 2.6, 7.4)                                                            # one reaching down from high up
    limb(g, [hi, (0.9, 3.0, 7.9), (2.4, 3.6, 6.2), (3.6, 4.1, 3.2), (4.2, 4.4, 0.0)],
         [0.2, 0.17, 0.14, 0.09, 0.015], CRUST)
    cracks = [lambda s, u: crack_main(s, u, half=2.1, top=4.8), slit(5.4, 1.0, 3.6), slit(-5.4, 0.8, 3.4),
              slit(3.2, 0.2, 2.4), slit(-3.4, 0.1, 2.2), slit(-7.0, 1.6, 4.6), slit(2.6, 6.2, 8.8)]
    return g, cracks


VARIANTS = [("MoonBearerA", variant_a), ("MoonBearerB", variant_b), ("MoonBearerC", variant_c)]


def run(start=0.0, gap=24.0, y=40.0):
    import bpy
    import rth_art as A
    out = []
    for i, (name, fn) in enumerate(VARIANTS):
        for suffix in ("", "_Wall", "_Glow"):
            old = bpy.data.objects.get(name + suffix)
            if old:
                me = old.data
                bpy.data.objects.remove(old, do_unlink=True)
                bpy.data.meshes.remove(me)
        g, cracks = fn()
        bmesh.ops.recalc_face_normals(g.bm, faces=g.bm.faces[:])
        crab = A.mesh_from(name, g.bm, COLOURS, coll="MoonBearer", smooth_angle=35)
        wbm, gbm = wall(cracks)
        w = A.mesh_from(name + "_Wall", wbm, COLOURS, coll="MoonBearer", smooth_angle=30)
        mod = w.modifiers.new("Solidify", "SOLIDIFY")
        mod.thickness = 0.5
        mod.offset = 1.0
        gl = A.mesh_from(name + "_Glow", gbm, COLOURS, coll="MoonBearer")
        for o in (crab, w, gl):
            o.location = (start + i * gap, y, 0)
        out.append(crab)
    return out
