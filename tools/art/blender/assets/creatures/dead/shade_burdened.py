r"""The walker (once "the burdened", variant C of the shades): someone who tried the long way back in life
and failed, and now waits at the far shore to hand travellers back their belongings. A hooded soul
leaning forward under a great dark bundle lashed to its back with iron, a ring of keys hanging from it;
what pokes out of the bundle is the player's own gear: a sword's hilt, a pickaxe, a round shield, a
boot. It walks with a crook in its right hand; its left fist holds the strap at its chest. The legs
are long gone to a wisp that drags behind. What it carries is solid; the body is soul-stuff.

What must read, in order: a person (the hooded head and its pale face, the shoulders, the bent back);
the lashed bundle and the gear in it; the crook. Human-sized beside the player: about 1.8 m to the
hood, the bundle's knot at 1.7 m, the gear standing over it. Grown as one mesh (artlib/grow.py); the
bundle a second tube of lumpy rings sunk into the back, the lashing, keys, crook and gear rods and
hulls on it. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\shade_burdened.py")
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

import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.grow import P, Grow  # noqa: E402
import artlib.nature as N  # noqa: E402

COLOURS = ["SoulPale", "SoulGlow", "Sable", "Hide", "Blackiron"]
PALE, GLOW, PACK, GEAR, IRON = range(5)

HIPS = 0.8                      # where the body comes undone into the wisp
SHOULDER = (0.26, 1.36)         # (f, u) the shoulders' line, leaning forward under the load
HEAD = (0.42, 1.64)             # (f, u) the middle of the face, raised to look ahead
FIST = (0.68, -0.35, 1.12)      # (f, s, u) the right fist round the crook
CROOK = ((0.8, 0.0), (0.6, 1.72))   # (f, u) the crook's foot on the ground and its top, through the fist

# the bundle: rings (f, s, u, half-width, toward the back, away) up its middle, leaning with the back;
# lumps of what's inside and two cinches where the iron bands hold it
BUNDLE = [
    (-0.22, 0.0, 0.84, 0.12, 0.1, 0.1),        # tucked under
    (-0.24, 0.02, 0.93, 0.27, 0.17, 0.2),
    (-0.16, -0.01, 1.07, 0.24, 0.17, 0.17),    # the lower band
    (-0.12, 0.02, 1.2, 0.31, 0.2, 0.25),       # the big middle
    (-0.05, 0.0, 1.34, 0.29, 0.19, 0.24),
    (0.0, -0.02, 1.45, 0.23, 0.17, 0.18),      # the upper band
    (0.04, 0.01, 1.54, 0.25, 0.17, 0.2),
    (0.06, 0.0, 1.62, 0.15, 0.1, 0.12),        # tied off
    (0.07, 0.0, 1.69, 0.07, 0.05, 0.06),       # the knot
]
BANDS = (2, 5)                  # the rings the iron bands go round


def lump(k, i, n):
    """How far a corner of the bundle bulges: the shapes of what's inside, a pattern rather than noise."""
    a = 2 * math.pi * k / n
    return 1 + 0.1 * math.cos(3 * a + 1.7 * i) + 0.05 * math.cos(5 * a - i)


def box(bm, centre, axes, half, mat):
    """A box round a centre along three axes (Vectors), half sizes along each."""
    c = Vector(centre)
    pts = [c + axes[0] * (half[0] * x) + axes[1] * (half[1] * y) + axes[2] * (half[2] * z)
           for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
    return A.hull(bm, pts, mat=mat)


def frame(d, hint=Vector((0, 0, 1))):
    d = d.normalized()
    a = (hint - d * hint.dot(d)).normalized()
    return d, a, d.cross(a)


def disc(bm, centre, normal, r, thick, sides, mat):
    """A round slab (a shield's board)."""
    n, a, b = frame(Vector(normal), Vector((0, 0, 1)))
    rings = []
    for side in (-1, 1):
        rings.append([bm.verts.new(Vector(centre) + n * (side * thick / 2) + (a * math.cos(2 * math.pi * k / sides)
                      + b * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)])
    for k in range(sides):
        j = (k + 1) % sides
        bm.faces.new((rings[0][k], rings[0][j], rings[1][j], rings[1][k])).material_index = mat
    bm.faces.new(rings[0]).material_index = mat
    bm.faces.new(list(reversed(rings[1]))).material_index = mat
    return n, a, b


def walker():
    g = Grow(sides=12)
    sf, su = SHOULDER
    hf, hu = HEAD
    # the body, upright, from the wisp dragging behind up through the hips, the leaning back and the
    # shoulders to the hood's peak: rings (f, s, u, half-width, in front, behind)
    g.tube([
        (-0.5, 0, 0.06, 0.02, 0.015, 0.015),      # the wisp's end, dragging
        (-0.38, 0, 0.15, 0.05, 0.04, 0.04),
        (-0.22, 0, 0.32, 0.1, 0.08, 0.08),
        (-0.08, 0, 0.55, 0.15, 0.11, 0.11),       # what was the legs, drawn into one
        (0.0, 0, HIPS, 0.18, 0.13, 0.13),         # the hips
        (0.06, 0, HIPS + 0.2, 0.19, 0.13, 0.15),  # the waist
        (0.16, 0, 1.2, 0.22, 0.14, 0.17),         # the chest, leaning into the load
        (sf, 0, su, 0.25, 0.13, 0.17),            # the shoulders
        (sf + 0.06, 0, su + 0.06, 0.2, 0.1, 0.13),
        (hf - 0.05, 0, hu - 0.16, 0.075, 0.075, 0.075),   # the neck, craned forward
        (hf - 0.01, 0, hu - 0.09, 0.1, 0.1, 0.11),        # the jaw, in the hood
        (hf, 0, hu, 0.12, 0.125, 0.125),                  # the face's middle
        (hf - 0.01, 0, hu + 0.07, 0.125, 0.145, 0.12),    # the hood's brim, over the brow
        (hf - 0.03, 0, hu + 0.13, 0.1, 0.1, 0.11),
        (hf - 0.07, 0, hu + 0.17, 0.05, 0.05, 0.06),      # the hood's peak, falling back
    ], PALE, upright=True)
    # the arms: the left fist holds the strap at the chest, the right reaches forward to the crook
    for s in (1, -1):
        root = g.patch([(sf, s * 0.25, su - 0.02), (sf - 0.02, s * 0.24, su - 0.12)], (0, s, 0))
        if s > 0:
            wrist = g.extrude(root, [
                (sf + 0.02, s * 0.31, su - 0.06, 0.065, 0.065, 0.065),
                (sf + 0.0, s * 0.33, su - 0.3, 0.05, 0.055, 0.055),     # the elbow, low and back
                (0.4, s * 0.2, 1.18, 0.042, 0.045, 0.045)], PALE)       # the forearm, up to the chest
            g.extrude(wrist, [(0.43, s * 0.17, 1.21, 0.045, 0.05, 0.05)], GLOW,
                      tip=(0.46, s * 0.15, 1.25), blunt=0.015)          # the fist on the strap
        else:
            ff, fs, fu = FIST
            wrist = g.extrude(root, [
                (sf + 0.04, s * 0.31, su - 0.06, 0.065, 0.065, 0.065),
                (sf + 0.1, s * 0.36, su - 0.3, 0.05, 0.055, 0.055),     # the elbow
                (ff - 0.08, fs, fu - 0.02, 0.042, 0.045, 0.045)], PALE)  # the forearm, forward
            g.extrude(wrist, [(ff - 0.03, fs, fu, 0.045, 0.05, 0.05)], GLOW,
                      tip=(ff + 0.04, fs, fu + 0.01), blunt=0.02)      # the fist round the crook
    g.smooth(0.2, repeat=1)
    for f in g.bm.faces:                         # the wisp brightens as it thins
        if f.material_index == PALE and f.calc_center_median().z < 0.5:
            f.material_index = GLOW
    bm = g.bm
    # the face: the front of the head sunk into the hood, glowing, with two dark strokes for eyes
    bm.normal_update()
    ahead = P(1, 0, 0)
    face = [f for f in bm.faces if f.normal.dot(ahead) > 0.55 and abs(f.calc_center_median().x) < 0.1
            and hu - 0.1 < f.calc_center_median().z < hu + 0.06]
    bmesh.ops.inset_region(bm, faces=face, thickness=0.012, depth=-0.025)
    flat = {v for f in face for v in f.verts}
    plane = sum(v.co.y for v in flat) / len(flat)
    for v in flat:                               # a plain flat face, so the strokes lie on it whole
        v.co.y = plane
    for f in face:
        f.material_index = GLOW
    eyes = []
    for s in (1, -1):                            # inner ends a little lower
        eyes.append(A.decal(bm, [P(hf + 0.3, s * 0.016, hu + 0.0), P(hf + 0.3, s * 0.066, hu + 0.018),
                                 P(hf + 0.3, s * 0.066, hu + 0.036), P(hf + 0.3, s * 0.016, hu + 0.018)],
                            tuple(ahead), mat=PACK, facing=0.3))
    # the bundle: a second tube of lumpy rings up the back, sunk into it
    tube = g.tube(BUNDLE, PACK, upright=True)
    for i, ring in enumerate(tube.rings):
        c = sum((v.co for v in ring), Vector()) / len(ring)
        for k, v in enumerate(ring):
            v.co = c + (v.co - c) * lump(k, i, len(ring))
    bmesh.ops.smooth_vert(bm, verts=[v for r in tube.rings for v in r], factor=0.3,
                          use_axis_x=True, use_axis_y=True, use_axis_z=True)
    # the iron bands round it, and the shoulder straps from its top, over the shoulders, down the chest
    # (the left one through the fist) and back under the arms
    for i in BANDS:
        ring = tube.rings[i]
        c = sum((v.co for v in ring), Vector()) / len(ring)
        loop = [c + (v.co - c) * 1.03 for v in ring]
        N.rod(bm, loop + loop[:1], 0.02, mat=IRON, sides=4)
    for s in (1, -1):
        pts = [P(0.02, s * 0.16, 1.55), P(0.22, s * 0.19, su + 0.08), P(0.36, s * 0.15, su - 0.03)]
        pts += [P(0.45, s * 0.15, 1.23)] if s > 0 else [P(0.33, s * 0.15, 1.2)]
        pts += [P(0.18, s * 0.25, 1.04), P(-0.1, s * 0.25, 1.0)]
        N.rod(bm, pts, 0.02, mat=IRON, sides=4)
    # what pokes out of it: the player's things
    # a sword, the blade down inside, the hilt standing out of the top
    d, a, b = frame(P(-0.25, 0.22, 1.0))
    base = P(-0.02, 0.1, 1.3)
    guard = base + d * 0.42
    box(bm, (base + guard) / 2, (d, a, b), (0.21, 0.012, 0.035), IRON)            # the blade
    box(bm, guard, (d, a, b), (0.018, 0.03, 0.11), IRON)                           # the crossguard
    N.rod(bm, [guard, guard + d * 0.15], 0.018, mat=GEAR, sides=5)                 # the grip
    A.hull(bm, [guard + d * 0.15 + v for v in (d * 0.0, d * 0.06, a * 0.03, -a * 0.03, b * 0.03, -b * 0.03)
                ], mat=IRON)                                                       # the pommel
    # a pickaxe, the handle out of the top on the other side, the head across it
    d, a, b = frame(P(-0.22, -0.2, 1.0))
    root = P(0.0, -0.06, 1.25)
    top = root + d * 0.78
    N.rod(bm, [root, top], 0.02, mat=GEAR, sides=5)
    across = P(0, 1, 0)
    across = (across - d * across.dot(d)).normalized()
    for k, (side, ln) in enumerate(((1, 0.24), (-1, 0.2))):
        tip = top - d * 0.07 + across * (side * ln)
        N.rod(bm, [top + d * 0.01, top + across * (side * ln * 0.5) - d * 0.02, tip], 0.03, mat=IRON, sides=4, taper=0.3)
    # a round shield lashed to the right side, its rim standing out of the bundle
    n, a, b = disc(bm, P(-0.16, -0.3, 1.16), P(0.35, -1, 0.1), 0.25, 0.04, 10, GEAR)
    c = P(-0.16, -0.3, 1.16)
    rim = [c + (a * math.cos(2 * math.pi * k / 10) + b * math.sin(2 * math.pi * k / 10)) * 0.25 for k in range(10)]
    N.rod(bm, rim + rim[:1], 0.025, mat=IRON, sides=4)
    A.hull(bm, [c + n * 0.02 + v for v in (a * 0.06, -a * 0.06, b * 0.06, -b * 0.06)] + [c + n * 0.08], mat=IRON)
    # a boot hanging by its lace from the upper band on the left, pressed against the bundle
    ring = tube.rings[BANDS[1]]
    hook = min(ring, key=lambda v: (v.co - P(-0.06, 0.4, 1.45)).length).co
    lace = hook + Vector((0.03, 0, -0.07))
    N.rod(bm, [hook, lace], 0.008, mat=IRON, sides=4)
    up, fwd, side = Vector((0, 0, 1)), P(1, 0, 0), Vector((1, 0, 0))
    shaft = lace + Vector((0.02, 0, -0.1))
    box(bm, shaft, (up, fwd, side), (0.12, 0.05, 0.045), GEAR)                       # the leg of it
    box(bm, shaft + up * -0.08 + fwd * 0.08, (fwd, up, side), (0.12, 0.045, 0.045), GEAR)   # the foot
    # the keys: an iron ring hanging from the left strap at the hip, three keys on it
    kc = P(0.04, 0.28, 0.95)
    N.rod(bm, [kc + Vector((0.0, 0.06 * math.cos(2 * math.pi * k / 8), 0.06 * math.sin(2 * math.pi * k / 8)))
               for k in range(9)], 0.008, mat=IRON, sides=4)
    for dy, ln in ((-0.035, 0.16), (0.0, 0.2), (0.035, 0.13)):
        k0 = kc + Vector((0, dy, -0.05))
        N.rod(bm, [k0, k0 - Vector((0, 0, ln))], 0.009, mat=IRON, sides=4)
        e = k0 - Vector((0, 0, ln))
        A.hull(bm, [e + Vector(v) for v in ((0, -0.02, 0.02), (0, 0.02, 0.02), (0.01, -0.02, -0.03), (-0.01, 0.02, -0.03))],
               mat=IRON)
    # the crook: planted ahead, through the right fist, hooked at the top
    (f0, u0), (f1, u1) = CROOK
    s0 = FIST[1]
    N.rod(bm, [P(f0, s0, u0), P(f1, s0, u1), P(f1 + 0.03, s0, u1 + 0.1), P(f1 + 0.11, s0, u1 + 0.15),
               P(f1 + 0.19, s0, u1 + 0.1), P(f1 + 0.2, s0, u1 + 0.01)], 0.022, mat=GEAR, sides=5, taper=0.8)
    return g, eyes


def build(name="ShadeBurdened", coll="Creatures"):
    g, _ = walker()
    return g.make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"ShadeBurdened": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
