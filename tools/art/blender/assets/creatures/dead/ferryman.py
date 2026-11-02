r"""The ferryman: the one who rows the dead to the Landing and waits there by his boat. He is not a soul:
a tall, gaunt, solid man in a long dark coat to the ground and a wide-brimmed hat, a long pole planted
upright in his right hand, his left hanging easy. Calm and patient; he already knows you. Across his
chest from the right shoulder to the left hip runs a cord strung with bronze obols, the fares of the
dead: his one standout.

What must read, in order: the tall narrow figure and the hat's brim; the pole standing taller than he
does; the pale face under the brim; the bright row of coins across the dark coat. About 2.1 m to the
hat's crown (the player is 1.8 m), the pole 2.7 m. Grown as one mesh (artlib/grow.py): the body a tube
of rings from the coat's hem up through the shoulders, the collar, the face and out into the brim and
the crown; the arms extruded from the shoulders, the hands their pale ends. The pole, the belt, the
cord and the coins are laid on after, the cord and coins cast onto the coat. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\ferryman.py")
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
from mathutils.bvhtree import BVHTree  # noqa: E402

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.grow import P, Grow  # noqa: E402
import artlib.nature as N  # noqa: E402

COLOURS = ["Sable", "Peat", "BonePale", "Bronze", "Ashworn"]
COAT, HAT, SKIN, COIN, POLE = range(5)

WAIST = 1.1                     # the belt
SHOULDER = 1.55                 # the shoulders' line
COLLAR = 1.7                    # the coat's high collar, where the skin starts
FACE = 1.81                     # the middle of the face
BRIM = 1.93                     # the brim's underside
BRIM_R = (0.27, 0.28, 0.25)     # the brim's half width, reach in front and behind
BRIM_LIFT = 0.04                # how far its front edge turns up (so the face shows under it)
POLE_AT = (0.3, -0.3)           # (f, s) where the pole stands, through the right fist
POLE_TOP = 2.72
FIST = (0.3, -0.3, 1.42)        # (f, s, u) the right fist round the pole
SASH = [(0.06, -0.14, 1.6), (0.12, -0.03, 1.4), (0.12, 0.07, 1.2), (0.1, 0.13, 1.04)]    # (f, s, u) the cord, front
OBOLS = 7                       # coins strung along the front of the cord, a gap between each
OBOL_R = 0.03

# the body's rings, hem to crown: (f, s, u, half-width, in front, behind) on an upright tube
BODY = [
    (0.0, 0, 0.0, 0.27, 0.24, 0.29),          # the hem on the ground, a little flared, trailing behind
    (0.0, 0, 0.06, 0.26, 0.23, 0.28),
    (0.0, 0, 0.5, 0.22, 0.17, 0.2),
    (0.0, 0, 0.92, 0.19, 0.14, 0.15),         # the hips
    (0.0, 0, WAIST, 0.165, 0.12, 0.12),       # the waist, belted
    (0.01, 0, 1.33, 0.19, 0.13, 0.12),        # the chest, narrow
    (0.0, 0, SHOULDER, 0.225, 0.12, 0.13),    # the shoulders, bony
    (0.0, 0, 1.63, 0.16, 0.1, 0.11),
    (0.02, 0, COLLAR, 0.085, 0.09, 0.1),      # the collar, high round the neck
    (0.04, 0, 1.745, 0.075, 0.085, 0.085),    # the long jaw and chin
    (0.04, 0, FACE, 0.095, 0.1, 0.1),         # the face's middle
    (0.04, 0, 1.88, 0.1, 0.1, 0.1),           # the brow
    (0.04, 0, BRIM) + BRIM_R,                 # the brim, under and over
    (0.04, 0, BRIM + 0.035) + BRIM_R,
    (0.04, 0, BRIM + 0.05, 0.125, 0.12, 0.115),  # the crown, a little pinched, flat on top
    (0.04, 0, 2.05, 0.118, 0.11, 0.108),
    (0.04, 0, 2.08, 0.1, 0.092, 0.09),
]
BRIM_RINGS = (12, 13)


def coin(bm, centre, normal, r=OBOL_R, thick=0.01, sides=7):
    """An obol: a small thick disc lying on the coat."""
    n = Vector(normal).normalized()
    a = (Vector((0, 0, 1)) - n * n.z).normalized()
    b = n.cross(a)
    rings = []
    for side in (-1, 1):
        rings.append([bm.verts.new(centre + n * (side * thick / 2) + (a * math.cos(2 * math.pi * k / sides)
                                   + b * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)])
    for k in range(sides):
        j = (k + 1) % sides
        bm.faces.new((rings[0][k], rings[0][j], rings[1][j], rings[1][k])).material_index = COIN
    bm.faces.new(list(reversed(rings[0]))).material_index = COIN
    bm.faces.new(rings[1]).material_index = COIN


def cast(bvh, point, toward, lift):
    """A point (f, s, u) cast onto the body from outside along -toward, lifted off it: (where, normal)."""
    d = P(*toward) - P(0, 0, 0)
    d.normalize()
    hit, normal, _, _ = bvh.ray_cast(P(*point) + d * 0.8, -d)
    return hit + normal * lift, normal


def ferryman():
    g = Grow(sides=12)
    tube = g.tube(BODY, COAT, upright=True)
    brim = {v for i in BRIM_RINGS for v in tube.rings[i]}
    # the arms: the right up to the fist on the pole, the left hanging at his side
    for s in (1, -1):
        root = g.patch([(0.0, s * 0.225, SHOULDER - 0.03), (0.0, s * 0.215, SHOULDER - 0.13)], (0, s, 0))
        if s < 0:
            ff, fs, fu = FIST
            wrist = g.extrude(root, [
                (0.02, s * 0.27, SHOULDER - 0.06, 0.06, 0.06, 0.06),
                (0.05, s * 0.3, 1.22, 0.05, 0.055, 0.055),             # the elbow, at his side
                (ff - 0.1, fs, fu - 0.03, 0.06, 0.06, 0.06)], COAT)    # the cuff, wide
            g.extrude(wrist, [(ff - 0.06, fs, fu - 0.01, 0.04, 0.042, 0.042)], SKIN,
                      tip=(ff + 0.04, fs, fu + 0.01), blunt=0.02)       # the fist round the pole
        else:
            wrist = g.extrude(root, [
                (0.0, s * 0.27, SHOULDER - 0.06, 0.06, 0.06, 0.06),
                (-0.03, s * 0.29, 1.22, 0.05, 0.055, 0.055),           # the elbow
                (0.03, s * 0.27, 0.98, 0.06, 0.062, 0.062)], COAT)     # the cuff
            g.extrude(wrist, [(0.04, s * 0.265, 0.93, 0.042, 0.045, 0.045)], SKIN,
                      tip=(0.05, s * 0.262, 0.86), blunt=0.03)          # the hand, open and easy
    g.smooth(0.2, repeat=1, keep=brim)
    bm = g.bm
    # the brim's front turned up a little
    for v in brim:
        fwd = -v.co.y / BRIM_R[1]
        v.co.z += BRIM_LIFT * max(0.0, fwd) - 0.015 * max(0.0, -fwd)
    # colours by height: the coat to the collar, skin (hair behind) to the brim, the hat above
    bm.normal_update()
    ahead = P(1, 0, 0)
    for f in bm.faces:
        if f.material_index != COAT:
            continue
        c = f.calc_center_median()
        if c.z > BRIM - 0.01:
            f.material_index = HAT
        elif c.z > COLLAR + 0.015:
            f.material_index = SKIN if f.normal.dot(ahead) > -0.25 else COAT
    # the face: the front of the head flattened a little, two dark strokes for eyes under the brim
    face = [f for f in bm.faces if f.material_index == SKIN and f.normal.dot(ahead) > 0.55
            and abs(f.calc_center_median().x) < 0.09 and FACE - 0.07 < f.calc_center_median().z < FACE + 0.06]
    flat = {v for f in face for v in f.verts}
    if flat:
        plane = sum(v.co.y for v in flat) / len(flat)
        for v in flat:
            v.co.y = plane + (v.co.y - plane) * 0.4
    bm.normal_update()
    for s in (1, -1):                                  # inner ends a little lower
        A.decal(bm, [P(0.4, s * 0.018, FACE + 0.016), P(0.4, s * 0.058, FACE + 0.02),
                     P(0.4, s * 0.058, FACE + 0.03), P(0.4, s * 0.018, FACE + 0.027)], tuple(ahead), mat=COAT, facing=0.3)
    bvh = BVHTree.FromBMesh(bm)
    # the belt, round the waist ring
    ring = tube.rings[4]
    c = sum((v.co for v in ring), Vector()) / len(ring)
    loop = [c + (v.co - c) * 1.06 for v in ring]
    N.rod(bm, loop + loop[:1], 0.024, mat=HAT, sides=4)
    # the cord: down the front from the right shoulder to the left hip, back up behind; coins on the front
    front = []
    for (f0, s0, u0), (f1, s1, u1) in zip(SASH, SASH[1:]):
        for k in range(4):
            t = k / 4
            front.append(cast(bvh, (f0, s0 + (s1 - s0) * t, u0 + (u1 - u0) * t), (1, 0, 0), 0.014))
    front.append(cast(bvh, SASH[-1], (1, 0, 0), 0.014))
    back = [cast(bvh, (0, s, u), (-1, 0, 0), 0.014) for s, u in ((0.14, 1.06), (0.08, 1.25), (-0.04, 1.45))]
    top = cast(bvh, (0.0, -0.15, 1.62), (0, -1, 0.6), 0.014)        # over the shoulder, from outside (the brim is above)
    pts = [p for p, _ in front] + [p for p, _ in back] + [top[0]]
    N.rod(bm, pts + pts[:1], 0.012, mat=HAT, sides=4)
    for k in range(OBOLS):
        t = 0.06 + 0.8 * k / (OBOLS - 1)                     # stopping short of the belt
        i = t * (len(front) - 1)
        a, b = front[int(i)], front[min(int(i) + 1, len(front) - 1)]
        w = i - int(i)
        p = a[0].lerp(b[0], w)
        n = a[1].lerp(b[1], w).normalized()
        coin(bm, p + n * 0.005, n, thick=0.008)
    # the pole, planted at his right, through the fist, taller than he is
    pf, ps = POLE_AT
    N.rod(bm, [P(pf, ps, -0.02), P(pf - 0.01, ps + 0.01, 0.8), P(pf, ps, FIST[2]), P(pf + 0.015, ps - 0.005, 2.1),
               P(pf + 0.01, ps, POLE_TOP)], 0.038, mat=POLE, sides=6, taper=0.7)
    return g


def build(name="Ferryman", coll="Creatures"):
    return ferryman().make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"Ferryman": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
