r"""The Moonfall Beast (2026-11-01): a world boss of the cracked moon thread (docs/lore.md V), built by
tools/art/blender/creatures.md.

What should read, in order: something huge and wrong crawled out of a crater; a hulking quadruped,
the forequarters massive and high, the back falling to the hips; a heavy head hanging below the
shoulders; columnar legs on clawed feet; crater-rock slabs fused over it (artlib/armour.chunk); pale
moon crystals bursting out between and through the slabs, crowning the brow, rising along the spine.

Proportions from the player (1.8 m): the shoulders 5.8 m (over three times the player), the crystals to
about 7.5 m, snout to tail about 14 m.

Two parts under one root, each the left half (+X) with a Mirror modifier:
- MoonfallBeast         the body, one closed mesh grown from a body tube
- MoonfallBeast_Plates  the overlay: rock chunks and crystal clusters, closed pieces

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\common\moonfall_beast.py")
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

from artlib import armour as AR  # noqa: E402
from artlib import family  # noqa: E402
from artlib.beast import foot, keep_left, symmetric, to_object, tune, tuning  # noqa: E402
from artlib.grow import Grow, P  # noqa: E402
from artlib.plates import outline, split_ladder  # noqa: E402

COLOURS = ["Moonstone", "Cinder", "Moonlight", "SaintBone"]   # pale flesh, crater rock, moon crystal, claws
SKIN, ROCK, CRYSTAL, HORN = range(4)
PI = math.pi
N = 22                                            # points round a chunk's outline

TORSO = [
    (-4.0, 0, 3.5, 1.0, 1.0, 1.0),               # the root of the tail
    (-3.4, 0, 3.7, 1.55, 1.35, 1.4),             # the hips
    (-2.7, 0, 3.65, 1.65, 1.4, 1.55),
    (-2.0, 0, 3.6, 1.6, 1.4, 1.6),               # the waist
    (-1.2, 0, 3.7, 1.75, 1.5, 1.8),
    (-0.4, 0, 3.85, 1.9, 1.65, 1.95),            # the ribs
    (0.35, 0, 4.05, 2.0, 1.8, 2.0),              # the chest, the highest of the back
    (1.1, 0, 4.15, 1.95, 1.75, 1.9),             # the shoulders
    (1.8, 0, 4.0, 1.6, 1.5, 1.6),
    (2.4, 0, 3.75, 1.2, 1.15, 1.2)]              # the neck's root
HEAD = [
    (3.0, 0, 3.5, 1.15, 1.1, 1.15),              # the neck, short and thick, falling forward
    (3.6, 0, 3.3, 1.15, 1.05, 1.15),
    (4.15, 0, 3.15, 1.25, 1.05, 1.25),           # the back of the skull, below the shoulders
    (4.7, 0, 3.05, 1.35, 1.0, 1.35),             # the cheeks and jaw: massive
    (5.25, 0, 2.9, 1.15, 0.85, 1.2),
    (5.7, 0, 2.75, 0.9, 0.7, 0.95),              # the snout, short and blunt
    (6.0, 0, 2.65, 0.75, 0.6, 0.75)]
NOSE = (6.2, 0, 2.6)
TAIL = [(-4.6, 0, 3.15, 0.8, 0.8, 0.8), (-5.3, 0, 2.6, 0.62, 0.62, 0.62), (-6.0, 0, 2.0, 0.45, 0.45, 0.45),
        (-6.6, 0, 1.45, 0.3, 0.3, 0.3), (-7.1, 0, 1.05, 0.2, 0.2, 0.2)]
TAIL_TIP = (-7.5, 0, 0.85)
# the legs, left side: where the patch on the body is, the rings, the foot
FORE_PATCH = [(0.75, 1.89, 3.6), (0.75, 1.39, 2.72), (0.75, 0.51, 2.22)]
FORE = [(0.9, 1.95, 2.55, 1.0, 1.062, 1.062),      # the upper arm
        (0.62, 2.1, 1.75, 0.775, 0.775, 0.775),     # the elbow, behind
        (0.7, 2.1, 1.2, 0.7, 0.7, 0.7),       # the forearm: a straight column down to the wrist
        (0.82, 2.08, 0.7, 0.65, 0.65, 0.65),
        (0.92, 2.05, 0.35, 0.65, 0.6, 0.6),    # the wrist
        (1.0, 2.05, 0.2, 0.65, 0.475, 0.4)]
FORE_FOOT = (1.84, 2.05)                        # the pad a scaled step ahead of the ankle
HIND_PATCH = [(-3.05, 1.55, 3.3), (-3.05, 1.13, 2.63), (-3.05, 0.41, 2.25)]
HIND = [(-3.0, 2.05, 2.75, 0.62, 1.05, 1.05),    # the thigh: no wider along the body than its patch
        (-2.4, 2.1, 1.8, 0.62, 0.66, 0.66),      # the knee, forward
        (-2.78, 2.1, 1.2, 0.6, 0.6, 0.6),        # the shin, upright enough to clear the foot
        (-3.08, 2.05, 0.65, 0.55, 0.55, 0.55),   # the hock, back
        (-2.98, 2.05, 0.32, 0.58, 0.52, 0.48),
        (-2.88, 2.05, 0.2, 0.58, 0.42, 0.35)]
HIND_FOOT = (-2.04, 2.05)
FOOT = 3.8                                       # the shared three-toed foot, scaled up


def body():
    g = Grow(sides=12)
    torso = g.tube(TORSO, SKIN)
    g.extrude(torso.caps["end"], HEAD, SKIN, tip=NOSE, blunt=0.35)
    g.extrude(torso.caps["start"], TAIL, SKIN, tip=TAIL_TIP, blunt=0.06)
    s = 1                                         # the left side only: symmetric() copies it over
    for patch, rings, (ff, fw) in ((FORE_PATCH, FORE, FORE_FOOT), (HIND_PATCH, HIND, HIND_FOOT)):
        leg = g.patch(patch, (0, 0.6, -1))
        ankle = g.extrude(leg, rings, SKIN)
        foot(g, ankle, ff, fw, s, SKIN, HORN, size=FOOT, nail=0.7)
    symmetric(g.bm)
    return g


# -- the overlay: rock and crystal ----------------------------------------------------------------

def axes():
    spine = [TAIL_TIP] + [r[:3] for r in reversed(TAIL)] + [r[:3] for r in TORSO] + [r[:3] for r in HEAD] + [NOSE]
    fore = [(0.75, 0.6, 3.0), (0.75, 1.4, 2.7)] + [r[:3] for r in FORE] + [(FORE_FOOT[0], FORE_FOOT[1], 0.1)]
    hind = [(-3.05, 0.6, 3.1), (-3.05, 1.2, 2.7)] + [r[:3] for r in HIND] + [(HIND_FOOT[0], HIND_FOOT[1], 0.1)]
    return AR.Axis(spine, ref=(0, 0, 1)), AR.Axis(fore, ref=(1, 0, 0)), AR.Axis(hind, ref=(1, 0, 0))


def crystal(bm, base, dirn, length, r, mat=CRYSTAL, sides=6):
    """One crystal: a six-sided prism sunk into the body at `base` (a Blender point), standing along
    `dirn`, its point a short facet cone closed with quads. A crystal standing in the creature's middle
    plane has corners on that plane, so the Mirror splits it cleanly."""
    d = dirn.normalized()
    x = Vector((1, 0, 0))
    t1 = x - d * x.dot(d)
    t1 = t1.normalized() if t1.length > 1e-6 else Vector((0, 1, 0))
    t2 = d.cross(t1).normalized()
    rings = []
    for along, k in ((-1.3 * r, 1.0), (0.66 * length, 0.94), (0.92 * length, 0.3), (length, 0.08)):
        c = base + d * along
        rings.append([bm.verts.new(c + t2 * (r * k * math.cos(2 * PI * j / sides)) +
                                   t1 * (r * k * math.sin(2 * PI * j / sides))) for j in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for j in range(sides):
            bm.faces.new((a[j], a[(j + 1) % sides], b[(j + 1) % sides], b[j])).material_index = mat
    split_ladder(bm, rings[0], mat, flip=True)
    split_ladder(bm, rings[-1], mat)


def cluster(bm, frame, d, theta, specs):
    """Crystals bursting out of the body round (d, theta) on an axis: each spec (dd, dtheta, length, r,
    lean_side, lean_back) stands at an offset from there, leaning out to the side and back. A crystal
    on the centre line (theta 0, no offsets to the side) must not lean to the side."""
    ax = frame.axis
    for dd, dth, length, r, side, back in specs:
        base = P(*frame.on(d + dd, theta + dth))
        n = ax.direction(d + dd, theta + dth)
        _, t, _, _ = ax.at(d + dd)
        w = t.cross(n).normalized() * ax.left
        crystal(bm, base, n + w * side - t * back, length, r)


def overlay(body_bm):
    bvh = BVHTree.FromBMesh(body_bm)
    sa, fa, ha = axes()
    sp, fo, hi = AR.Frame(bvh, sa), AR.Frame(bvh, fa), AR.Frame(bvh, ha)
    sd = lambda f: sa.nearest(P(f, 0, 3.5))[0]     # noqa: E731  spine distance at f
    bm = bmesh.new()
    rock = dict(dome=0.5, ridge=0.6, facets=6, facet=0.35, mat=ROCK)
    AR.chunk(bm, sp, sd(-0.55), sd(2.25), -1.95, 1.95, 0.24,
             outline(N, front=0.2, back=0.35, square=0.4, lobes=7, lobe=0.07), **rock)        # shoulder carapace
    AR.chunk(bm, sp, sd(-3.7), sd(-1.35), -1.7, 1.7, 0.22,
             outline(N, front=0.3, back=0.25, square=0.4, lobes=7, lobe=0.07), **rock)        # hip carapace
    AR.chunk(bm, sp, sd(2.4), sd(3.95), -1.25, 1.25, 0.16,
             outline(N, front=0.3, back=0.3, square=0.3, lobes=5, lobe=0.06), **rock)         # neck plate
    AR.chunk(bm, sp, sd(3.95), sd(5.85), -1.6, 1.6, 0.18,
             outline(N, front=0.5, back=0.25, square=0.3, lobes=5, lobe=0.07), **rock)        # helm
    ends = [-4.2, -5.2, -6.2, -7.0]
    for a, b in zip(ends, ends[1:]):                                                         # tail plates
        AR.chunk(bm, sp, sa.nearest(P(b, 0, 2.0))[0] - 0.12, sa.nearest(P(a, 0, 3.0))[0], -1.4, 1.4, 0.12,
                 outline(N, front=0.3, back=0.2, square=0.35, lobes=5, lobe=0.05),
                 ridge=0.6, lip_back=1.0, mat=ROCK)
    for fr, rings, k in ((fo, FORE, 0.9), (hi, HIND, 1.0)):                                 # leg guards
        ax = fr.axis
        d0 = ax.nearest(P(*rings[0][:3]))[0]
        d1 = ax.nearest(P(*rings[1][:3]))[0]
        AR.chunk(bm, fr, d0 - 0.02, d1 * 0.95, PI / 2 - 1.3, PI / 2 + 1.3, 0.16 * k,   # from where the leg leaves the body
                 outline(N, front=0.3, back=0.3, square=0.3, lobes=5, lobe=0.06),
                 dome=0.4, facets=4, facet=0.3, mat=ROCK)
    # the crystals: a crest in the gap between the carapaces, on the centre line and either side
    cluster(bm, sp, sd(-0.95), 0.0, [(0, 0, 2.3, 0.34, 0, 0.25), (0.45, 0, 1.4, 0.24, 0, -0.1),
                                     (-0.5, 0, 1.2, 0.22, 0, 0.45)])
    cluster(bm, sp, sd(-0.95), 0.55, [(0.1, 0, 1.6, 0.26, 0.35, 0.15), (-0.35, 0.2, 1.0, 0.18, 0.6, 0.35)])
    # bursting through the shoulder carapace's flank, and the hip's
    cluster(bm, sp, sd(1.1), 1.05, [(0, 0, 1.5, 0.27, 0.3, 0.3), (0.35, 0.18, 0.9, 0.18, 0.55, 0.0),
                                    (-0.3, -0.12, 1.1, 0.2, 0.1, 0.55)])
    cluster(bm, sp, sd(-2.6), 0.85, [(0, 0, 1.2, 0.23, 0.35, 0.35), (0.3, 0.15, 0.7, 0.15, 0.6, 0.1)])
    cluster(bm, sp, sd(0.3), 0.0, [(0, 0, 1.3, 0.24, 0, 0.5)])                       # one out of the carapace
    # a crown on the brow, and a few down the tail
    cluster(bm, sp, sd(4.65), 0.0, [(0, 0, 1.2, 0.2, 0, -0.35)])
    cluster(bm, sp, sd(4.55), 0.5, [(0, 0, 1.0, 0.17, 0.5, 0.1), (-0.3, 0.15, 0.7, 0.13, 0.7, 0.3)])
    cluster(bm, sp, sd(-5.7), 0.0, [(0, 0, 0.8, 0.15, 0, 0.6)])
    # tusks of moon crystal jutting up out of the jaw's sides, the left one (the Mirror makes the right)
    for f, u, length, r in ((5.45, 2.35, 1.3, 0.17), (5.0, 2.2, 0.8, 0.13)):
        base = P(*sp.on(sd(f), 1.95))
        crystal(bm, base, Vector((0.35, -0.55, 1.0)), length, r)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def build(name="MoonfallBeast", coll="Creatures"):
    g = body()
    over = overlay(g.bm)
    keep_left(g.bm)
    keep_left(over)
    tune(g.bm, tuning(__file__, name))
    root = to_object(g.bm, name, COLOURS, coll, clip=True)
    to_object(over, name + "_Plates", COLOURS, coll, root, clip=True, smooth=20)
    return root


ASSETS = {"MoonfallBeast": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
