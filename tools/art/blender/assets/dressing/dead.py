r"""Dressing, dead. What lies along the long way back through the realm of the dead: grasping hands
(the hazard that rises from hidden tiles and holds you), bone-pale standing stones, drowned posts, the
gear others lost on the way, dead reeds.

Run it to lay the family out beside the player (with SAVE set, it also saves the working file
dead.blend beside this script); with EXPORT set it writes every member of ASSETS to
client/Assets/Resources/Art/Assets/Dressing/Dead/<Name>.fbx.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\dressing\dead.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import bmesh
import math
import os
import random
import sys

from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
import artlib.nature as N  # noqa: E402
from artlib.caves import make  # noqa: E402


def mound(bm, r, h, rnd, mat, points=10, centre=(0, 0)):
    """A low, lumpy patch of disturbed ground: a ring sunk just under the surface, a few lumps on top."""
    cx, cy = centre
    pts = []
    for k in range(points):
        a = 2 * math.pi * k / points + rnd.uniform(-0.15, 0.15)
        rr = r * rnd.uniform(0.85, 1.05)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a), -0.02))
    for k in range(points // 2):
        a = rnd.uniform(0, 2 * math.pi)
        rr = r * rnd.uniform(0.2, 0.6)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a), h * rnd.uniform(0.6, 1.0)))
    return A.hull(bm, pts, mat=mat)


def grasping_hands(seed=11, at=(0, 0, 0)):
    """Four thin pale arms clawing up out of a patch of dark, broken ground, about a tile across, each
    ending in a hand of three crooked fingers and a thumb reaching up and in."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    mound(bm, 0.5, 0.07, rnd, 0, points=12)
    arms = [(0.22, 0.1, 0.85), (-0.2, 0.16, 0.7), (0.02, -0.24, 0.95), (-0.18, -0.12, 0.55)]
    for x, y, h in arms:
        base = Vector((x, y, -0.06))
        out = Vector((x, y, 0)).normalized()
        elbow = base + out * 0.08 + Vector((rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04), h * 0.5))
        wrist = elbow + out * 0.06 + Vector((0, 0, h * 0.45))
        palm = wrist + out * 0.02 + Vector((0, 0, 0.08))
        A.loft(bm, [(base, 0.05, 0.05), (elbow, 0.036, 0.036), (wrist, 0.028, 0.028), (palm, 0.05, 0.022)],
               sides=6, ref=tuple(out), mats=1)
        d = (palm - wrist).normalized()
        side = d.cross(out).normalized()
        for i in (-1, 0, 1):                                     # fingers, crooked over toward the middle
            root = palm - d * 0.02 + side * (i * 0.028)
            knuckle = root + d * 0.08 + side * (i * 0.012)
            tip = knuckle + d * 0.03 - out * 0.06
            N.rod(bm, [root, knuckle], 0.013, mat=1, sides=4, tip=tip)
        thumb = palm - d * 0.03 + out * 0.03
        N.rod(bm, [thumb - out * 0.03, thumb + out * 0.02 + d * 0.03], 0.012, mat=1, sides=4,
              tip=thumb + d * 0.07 - out * 0.01)
    return make("GraspingHands", bm, ["Peat", "BonePale"], at=at, smooth=40)


def standing_stone(seed=21, at=(0, 0, 0), height=1.9, width=0.36, lean=(0.22, 0.06), name="StandingStoneA"):
    """A rough bone-pale stone, set upright long ago and leaning now, its foot dark with wet."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    pts = []
    for z, k in ((-0.25, 1.0), (0.12, 1.0), (0.5, 0.92), (height * 0.75, 0.72), (height, 0.42)):
        t = max(z, 0) / height
        cx, cy = lean[0] * t, lean[1] * t
        for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
            pts.append((cx + sx * width * k * rnd.uniform(0.85, 1.05), cy + sy * width * 0.55 * k * rnd.uniform(0.8, 1.05),
                        z + (rnd.uniform(-0.08, 0.06) if z > 0 else 0)))
    pts.append((lean[0] * 0.9 + rnd.uniform(-0.1, 0.1), lean[1], height + 0.12))     # the crown, off centre
    faces = A.hull(bm, pts, mat=0)
    for f in faces:
        if f.calc_center_median().z < 0.06:
            f.material_index = 1
    return make(name, bm, ["BonePale", "DeadSlate"], at=at, smooth=20)


def foot_stones(bm, rnd, mat, count=3, r=0.2):
    """Dark stones packed round the foot of a post."""
    for k in range(count):
        a = 2 * math.pi * k / count + rnd.uniform(0, 1)
        c = Vector((r * math.cos(a), r * math.sin(a), 0))
        s = rnd.uniform(0.08, 0.12)
        A.hull(bm, [c + Vector((rnd.uniform(-s, s), rnd.uniform(-s, s), -0.04)) for _ in range(4)]
               + [c + Vector((rnd.uniform(-s, s) * 0.6, rnd.uniform(-s, s) * 0.6, s * 0.7)) for _ in range(3)], mat=mat)


def drowned_post(seed=31, at=(0, 0, 0)):
    """A drowned timber, the last of some old marker, leaning and split at the top, an iron band
    rusted black round it."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    top = Vector((0.2, -0.05, 1.15))
    line = N.wobble(rnd, (0, 0, -0.15), top, 5, 0.03)
    N.rod(bm, line, 0.1, mat=0, sides=7, taper=0.8)
    for k in range(3):                                       # the split top: three splinters out of the end
        a = 2 * math.pi * k / 3 + 0.4
        root = top + Vector((0.04 * math.cos(a), 0.04 * math.sin(a), -0.06))
        A.spike(bm, root, 0.035, root + Vector((0.03 * math.cos(a) + 0.02, 0.03 * math.sin(a), rnd.uniform(0.12, 0.22))),
                sides=4, mat=0)
    band = line[2] + (line[3] - line[2]) * 0.3                 # the band, round the post's middle
    lean = (line[3] - line[2]).normalized()
    A.loft(bm, [(band - lean * 0.03, 0.098, 0.098), (band + lean * 0.03, 0.098, 0.098)], sides=7, ref=(1, 0, 0), mats=1)
    foot_stones(bm, rnd, 2)
    return make("DrownedPost", bm, ["DrownedWood", "Blackiron", "DeadSlate"], at=at, smooth=35)


def lost_belongings(seed=41, at=(0, 0, 0)):
    """What others carried this far and dropped: a pack slumped on its side, a round shield half sunk in
    the ash, a single boot. A small heap, under a tile."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    mound(bm, 0.45, 0.04, rnd, 0)
    # the pack: a soft sack, its flap the darker leather
    pts = []
    for z, k in ((0.0, 1.0), (0.18, 1.05), (0.3, 0.8)):
        for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
            pts.append((-0.12 + sx * 0.18 * k + rnd.uniform(-0.02, 0.02), 0.1 + sy * 0.13 * k, z))
    for f in A.hull(bm, pts, mat=1):
        c = f.calc_center_median()
        if c.z > 0.24:
            f.material_index = 2
    # the shield: a round of boards in an iron rim, tilted and half sunk in the ash
    before = set(bm.verts)
    _, faces = A.prism(bm, 10, 0.26, 0.26, 0.0, 0.05, mat_index=4, cap_bottom=True)
    faces[-2].material_index = faces[-1].material_index = 3      # both faces: painted boards, faded
    tilt = Matrix.Translation((0.2, -0.16, 0.0)) @ Matrix.Rotation(math.radians(-28), 4, "Y")
    for v in set(bm.verts) - before:
        v.co = tilt @ v.co
    # the boot, lying on its side: a shaft and a foot, kicked off beside the pack
    bx, by = -0.4, -0.45
    A.hull(bm, [(bx + 0.05 + dx, by + 0.22 + dy, z) for dx in (-0.06, 0.06) for dy in (-0.05, 0.05) for z in (0.0, 0.1)]
           + [(bx + 0.05 + dx, by + 0.48, z) for dx in (-0.065, 0.065) for z in (0.0, 0.11)], mat=2)
    A.hull(bm, [(bx + 0.05 + dx, by + 0.18 + dy, z) for dx in (-0.06, 0.06) for dy in (-0.05, 0.05) for z in (0.0, 0.1)]
           + [(bx + 0.23, by + 0.17, 0.0), (bx + 0.23, by + 0.17, 0.06), (bx + 0.23, by + 0.21, 0.0),
              (bx + 0.23, by + 0.21, 0.05)], mat=2)
    return make("LostBelongings", bm, ["Peat", "Hide", "Mud", "Moor", "Blackiron"], at=at, smooth=30)


def dead_reeds(seed=51, at=(0, 0, 0)):
    """A clump of dead reeds at the water's edge: dry grey blades from a dark tussock, some snapped and
    bent over."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    mound(bm, 0.22, 0.08, rnd, 0, points=8)
    for k in range(14):
        a, d = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 0.18)
        base = Vector((d * math.cos(a), d * math.sin(a), -0.04))
        h = rnd.uniform(0.55, 1.15)
        lean = Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(0.05, 0.25)
        mat = 1 if k % 3 else 2
        if k % 4 == 0:                                       # snapped: up, then bent over toward the water
            knee = base + lean * 0.4 + Vector((0, 0, h * 0.6))
            N.rod(bm, [base, knee], 0.016, mat=mat, sides=3,
                  tip=knee + lean.normalized() * h * 0.35 - Vector((0, 0, h * 0.15)))
        else:
            A.spike(bm, base, 0.018, base + lean + Vector((0, 0, h)), sides=3, mat=mat)
    return make("DeadReeds", bm, ["Peat", "Ashworn", "Ashgrey"], at=at, smooth=30)


ASSETS = {
    "GraspingHands": grasping_hands,
    "StandingStoneA": standing_stone,
    "StandingStoneB": lambda: standing_stone(seed=22, height=1.3, width=0.42, lean=(-0.12, 0.3), name="StandingStoneB"),
    "DrownedPost": drowned_post,
    "LostBelongings": lost_belongings,
    "DeadReeds": dead_reeds,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
