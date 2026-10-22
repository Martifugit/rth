"""Held items: the common smithed weapons and tools, plus a few one-off pieces.

The smithed set follows real weapons from the age of blades, one silhouette each, so they read
apart at game distance: knife (the athame's shape), sword (a gladius), falcata (R'th's answer to
the scimitar), longsword (a claymore with a church hilt), warhammer, and the tools (a felling
hatchet, a pickaxe). The mace is the penitent's mace's, outside the smithed set for now. Every smithed model has one metal material, "Copper" (tier 1);
other tiers are a recolour in items.json ("recolor": { "Copper": "Iron" }).

Held art convention (tools/character-models/README.md): the origin is the middle of the grip and
the handle runs forward, along Blender -Y (Unity +Z after export), as if held in the T-posed
right fist. Blades stand in the vertical plane: their width runs up and down (Blender Z). The
business side (a single edge, an axe's blade, a hammer's face, a pick's long point) is on top,
+Z: the client's hold turns that to face forward in the hand (HeldItemArt.Hold).

Each model exports to client/Assets/Resources/Items/<Name>.fbx and is named by "model" in
items.json."""
import math
import os
import sys

import bmesh

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

EXPORT = True
METAL, GRIP = 0, 1          # material slots of the smithed models
SMITHED = ["Copper", "Umber"]
HAFTED = ["Copper", "Ash"]


def F(along, up=0.0, side=0.0):
    """(distance forward of the grip, up, to the right) -> Blender."""
    return (side, -along, up)


def rod(bm, start, end, r, sides=6, mat=GRIP, r_end=None):
    """A straight round shaft along the handle (grips, hafts)."""
    A.loft(bm, [(F(start), r, r), (F(end), r_end or r, r_end or r)], sides=sides, ref=(0, 0, 1), mats=mat)


def blade(bm, sections, mat=METAL):
    """A double-edged blade: (along, half width, half thickness) sections, a diamond in section,
    ending in a point."""
    A.loft(bm, [(F(a), t, w) if w else (F(a), 0, 0) for a, w, t in sections],
           sides=4, ref=(0, 0, 1), mats=mat)


def plate(bm, profile, spine_t, edge_t, mat=METAL):
    """A flat single-edged shape from its outline in (along, up), thick at the top (the spine)
    and thin at the bottom (the edge): falchions, knives, axe blades."""
    ups = [u for _, u in profile]
    top, bottom = max(ups), min(ups)
    pts = []
    for a, u in profile:
        k = (u - bottom) / (top - bottom) if top > bottom else 1
        t = edge_t + (spine_t - edge_t) * k
        pts += [F(a, u, -t), F(a, u, t)]
    return A.hull(bm, pts, mat)


def strip(bm, path, tops, bots, t_top, t_bot, mat=METAL):
    """A blade along a curved centreline: path is (along, up) points; at each, the blade reaches
    tops[i] to the left of the path (up, for a straight blade) and bots[i] to the right, with
    thickness t_top at the top edge and t_bot at the bottom (a spine is thick, an edge thin;
    scalars or lists). A last station with tops = -bots is the point. Curved blades (falcata,
    khopesh, sickles) and wavy ones (flamberge) are all strips."""
    n = len(path)
    t_top = t_top if isinstance(t_top, list) else [t_top] * n
    t_bot = t_bot if isinstance(t_bot, list) else [t_bot] * n
    rings = []
    for i, (a, u) in enumerate(path):
        pa, pu = path[max(i - 1, 0)]
        na, nu = path[min(i + 1, n - 1)]
        ta, tu = na - pa, nu - pu
        length = math.hypot(ta, tu) or 1.0
        ta, tu = ta / length, tu / length
        la, lu = -tu, ta                                  # left of the path
        top = (a + la * tops[i], u + lu * tops[i])
        bot = (a - la * bots[i], u - lu * bots[i])
        rings.append([bm.verts.new(F(top[0], top[1], -t_top[i])), bm.verts.new(F(top[0], top[1], t_top[i])),
                      bm.verts.new(F(bot[0], bot[1], t_bot[i])), bm.verts.new(F(bot[0], bot[1], -t_bot[i]))])
    faces = []
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(4):
            j = (k + 1) % 4
            faces.append(bm.faces.new((r0[k], r0[j], r1[j], r1[k])))
    faces.append(bm.faces.new(rings[0]))
    faces.append(bm.faces.new(list(reversed(rings[-1]))))
    for f in faces:
        f.material_index = mat
    bmesh.ops.remove_doubles(bm, verts=[v for r in rings for v in r], dist=1e-5)
    return faces


def box(bm, a0, a1, u0, u1, s, mat=METAL):
    return A.hull(bm, [F(a, u, x) for a in (a0, a1) for u in (u0, u1) for x in (-s, s)], mat)


def pommel(bm, at, r, mat=METAL):
    """A wheel pommel: a short fat disc across the grip's end."""
    A.loft(bm, [(F(at - 0.012), r, r), (F(at + 0.012), r, r)], sides=8, ref=(0, 0, 1), mats=mat)


# ---- swords and knives ----

def sword():
    bm = bmesh.new()
    # The sword is a gladius: short, straight, swelling a little before its long point.
    blade(bm, [(0.07, 0.030, 0.006), (0.30, 0.027, 0.006), (0.44, 0.032, 0.005), (0.56, 0, 0)])
    A.loft(bm, [(F(0.05), 0.030, 0.034), (F(0.075), 0.030, 0.034)], sides=8, ref=(0, 0, 1), mats=METAL)
    rod(bm, -0.06, 0.05, 0.018)
    A.loft(bm, [(F(-0.09), 0.026, 0.030), (F(-0.06), 0.034, 0.038), (F(-0.045), 0.02, 0.02)],
           sides=8, ref=(0, 0, 1), mats=METAL)
    return bm, "Sword", SMITHED


def falcata():
    bm = bmesh.new()
    # Iberian: the single edge (on top, the business side) waists in near the hilt and swells to
    # a heavy belly, the spine curving up to the point; the grip hooks over the hand.
    path = [(0.06, 0), (0.15, 0), (0.28, 0), (0.40, 0), (0.50, 0.005), (0.58, 0.02)]
    strip(bm, path, [0.016, 0.012, 0.030, 0.044, 0.034, 0.006], [0.016, 0.013, 0.016, 0.012, 0.004, -0.006],
          0.0015, 0.007)
    rod(bm, -0.07, 0.06, 0.016)
    A.hull(bm, [F(-0.07, -0.012, -0.014), F(-0.07, -0.012, 0.014), F(-0.07, 0.016, -0.014), F(-0.07, 0.016, 0.014),
                F(-0.11, 0.01, -0.012), F(-0.11, 0.01, 0.012), F(-0.10, 0.05, -0.01), F(-0.10, 0.05, 0.01)], METAL)
    return bm, "Falcata", SMITHED


def longsword():
    """A claymore with a church hilt: guard arms sloping toward the blade with quatrefoil ends,
    joined by a ring round the blade (a wheel cross), and a reliquary pommel."""
    bm = bmesh.new()
    blade(bm, [(0.10, 0.033, 0.007), (1.02, 0.022, 0.006), (1.12, 0, 0)])
    A.loft(bm, [(F(0.06), 0.02, 0.03), (F(0.10), 0.022, 0.034)], sides=6, ref=(0, 0, 1), mats=METAL)
    for s_ in (-1, 1):
        A.hull(bm, [F(0.07, 0, -0.012), F(0.07, 0, 0.012), F(0.09, 0, -0.012), F(0.09, 0, 0.012),
                    F(0.15, s_ * 0.15, -0.010), F(0.15, s_ * 0.15, 0.010), F(0.17, s_ * 0.14, -0.010),
                    F(0.17, s_ * 0.14, 0.010)], METAL)
        # Quatrefoil: a boss on the arm's end with four lobes overlapping it, all one piece.
        for da, du, size in ((0, 0, 0.015), (0.016, 0, 0.012), (-0.016, 0, 0.012), (0, 0.016, 0.012),
                             (0, -0.016, 0.012)):
            ca, cu = 0.165 + da, s_ * 0.148 + du
            A.hull(bm, [F(ca + size * math.cos(k * math.pi / 2), cu + size * math.sin(k * math.pi / 2), z)
                        for k in range(4) for z in (-0.011, 0.011)], METAL)
    # The ring: eight short bars round the cross, through the arms and across the blade.
    ring_a, ring_r, n = 0.115, 0.085, 8
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        pts = []
        for a in (a0, a1):
            for r in (ring_r - 0.008, ring_r + 0.008):
                for z in (-0.009, 0.009):
                    pts.append(F(ring_a + r * math.cos(a), r * math.sin(a), z))
        A.hull(bm, pts, METAL)
    rod(bm, -0.22, 0.06, 0.017)
    # Reliquary pommel: a small eight-sided capsule.
    A.loft(bm, [(F(-0.22), 0.02, 0.02), (F(-0.235), 0.032, 0.032), (F(-0.275), 0.032, 0.032),
                (F(-0.29), 0.0, 0.0)], sides=8, ref=(0, 0, 1), mats=METAL)
    return bm, "Longsword", SMITHED

# ---- hafted ----

def mace():
    bm = bmesh.new()
    rod(bm, -0.12, 0.44, 0.02)
    # Head: a six-sided block with four flanges standing out of it.
    A.loft(bm, [(F(0.42), 0.05, 0.05), (F(0.58), 0.05, 0.05)], sides=6, ref=(0, 0, 1), mats=METAL)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        dx, dz = math.cos(a), math.sin(a)
        A.hull(bm, [F(0.44, dz * 0.03, dx * 0.03), F(0.56, dz * 0.03, dx * 0.03),
                    F(0.47, dz * 0.085, dx * 0.085), F(0.53, dz * 0.085, dx * 0.085),
                    F(0.50, dz * 0.03 + dx * 0.01, dx * 0.03 - dz * 0.01),
                    F(0.50, dz * 0.03 - dx * 0.01, dx * 0.03 + dz * 0.01)], METAL)
    A.loft(bm, [(F(0.58), 0.05, 0.05), (F(0.62), 0, 0)], sides=6, ref=(0, 0, 1), mats=METAL)
    return bm, "Mace", HAFTED


def warhammer():
    bm = bmesh.new()
    rod(bm, -0.12, 0.60, 0.02)
    # Head: a square hammer face on top, a curved spike behind (below), a short top spike.
    box(bm, 0.50, 0.58, -0.035, 0.035, 0.03)
    box(bm, 0.505, 0.575, 0.03, 0.10, 0.034)
    A.hull(bm, [F(0.51, -0.03, -0.022), F(0.51, -0.03, 0.022), F(0.57, -0.03, -0.022), F(0.57, -0.03, 0.022),
                F(0.53, -0.12, -0.008), F(0.55, -0.12, 0.008), F(0.50, -0.18, 0)], METAL)
    A.loft(bm, [(F(0.58), 0.022, 0.022), (F(0.66), 0, 0)], sides=4, ref=(0, 0, 1), mats=METAL)
    return bm, "Warhammer", HAFTED


# ---- tools ----

def hatchet():
    bm = bmesh.new()
    rod(bm, -0.10, 0.50, 0.02)
    # A felling axe: an eye round the haft, a wedge flaring evenly to a slightly rounded edge,
    # and a flat poll behind.
    box(bm, 0.42, 0.50, -0.035, 0.035, 0.026)
    path = [(0.46, 0.03), (0.46, 0.09), (0.46, 0.14), (0.46, 0.165)]
    strip(bm, path, [0.035, 0.045, 0.058, 0.05], [0.035, 0.045, 0.058, 0.05],
          [0.022, 0.014, 0.006, 0.002], [0.022, 0.014, 0.006, 0.002])
    box(bm, 0.425, 0.495, -0.065, -0.03, 0.028)
    return bm, "Hatchet", HAFTED


def pickaxe():
    bm = bmesh.new()
    rod(bm, -0.10, 0.56, 0.02)
    # Head: a block round the top of the haft, a long pick curving up and a short one down.
    box(bm, 0.52, 0.60, -0.04, 0.04, 0.03)
    A.hull(bm, [F(0.53, 0.03, -0.02), F(0.53, 0.03, 0.02), F(0.59, 0.03, -0.02), F(0.59, 0.03, 0.02),
                F(0.54, 0.16, -0.012), F(0.54, 0.16, 0.012), F(0.50, 0.27, 0)], METAL)
    A.hull(bm, [F(0.53, -0.03, -0.02), F(0.53, -0.03, 0.02), F(0.59, -0.03, -0.02), F(0.59, -0.03, 0.02),
                F(0.54, -0.14, -0.012), F(0.54, -0.14, 0.012), F(0.50, -0.24, 0)], METAL)
    return bm, "Pickaxe", HAFTED


# ---- one-offs (not smithed) ----

def knife():
    """The common knife: the athame's shape in the smithed metal."""
    bm, _, _ = athame()
    return bm, "Knife", SMITHED


def athame():
    """A ritual knife: double-edged, black-handled."""
    bm = bmesh.new()
    blade(bm, [(0.07, 0.026, 0.006), (0.22, 0.020, 0.005), (0.29, 0, 0)])
    box(bm, 0.055, 0.075, -0.05, 0.05, 0.014, mat=GRIP)
    rod(bm, -0.06, 0.06, 0.016, r_end=0.014)
    return bm, "Athame", ["Steel", "Sable"]


MODELS = [knife, sword, longsword, falcata, warhammer, hatchet, pickaxe, mace, athame]
# The line-up shown in the preview: the smithed roster.
PREVIEW = ["Knife", "Sword", "Falcata", "Longsword", "Warhammer", "Hatchet", "Pickaxe"]


def build():
    A.reset()
    objs = []
    for make in MODELS:
        bm, name, materials = make()
        A.finish(bm)
        objs.append(A.mesh_from(name, bm, materials, coll="Items"))
    result = {}
    for o in objs:
        r = A.report([o])
        if EXPORT:
            A.export([o], f"Resources/Items/{o.name}.fbx")
        result[o.name] = [r["triangles"], r["colours"]]
    # Line up the preview's shortlist beside the player, handles down, grips at hand height,
    # blades facing -X; hide the rest.
    shown = sorted([o for o in objs if o.name in PREVIEW], key=lambda o: PREVIEW.index(o.name))
    for o in objs:
        o.hide_render = o not in shown
    for i, o in enumerate(shown):
        o.rotation_euler = (-math.pi / 2, 0, 0)
        o.location = (0.6, 1.9 - i * 0.36, 0.6)
    return objs, result


objs, result = build()
cam = A.preview_rig(focus=(0.6, 0.0, 0.95), distance=6.8, pitch=4, yaw=-90, human_at=(0.6, -2.5))
