"""Monuments, Cruor. Religion's monuments (docs/skills.md 4.4) and everything that dresses them: the
red needle on its stepped plinth in ten tiers, the founts the faithful bleed into, the reliquaries
that quicken them, and the faith's furniture round them.

Every monument in the world is a drawing of the First Monument in Cruor (objects.json
cruor_first_monument), so the tiers are one grammar growing grander, not ten designs: a pale
stepped podium and a red stone needle whose height and collar grow with the tier, with pinnacles,
a pedestal, lesser needles and at last a raised terrace. The top tiers grow by size and clean
masses, one idea each, never by piling on parts (fins, battlemented edges, an arcade and black
niches were tried and read as clutter). A player should read a monument's tier from across a valley.

**Built for walking up to.** A fount is a gathering node: the player walks to the tile in front of
it and pushes an arm in. So every tier is laid out on the tile grid (1 unit = 1 tile, the origin the
middle of the centre tile): the podium's steps are whole tiles deep and a gentle STEP high, each fount
stands on a tile centre with a free stand tile in front of it on the step below, its rim at about a
standing player's waist, and only the needle, pedestal, terrace and lesser needles block their own
tiles. layout(tier) gives all of it for the region data: the founts (tile, height, facing), the
stand tiles and the blocked tiles.

Materials (surfaces.json): the podium's risers in big dressed blocks laid a course to a step
(CruorPlinth), its treads and everything carved from one piece smooth (CruorCarved), the needles
veined red (Bloodstone), the blood standing in the founts as FountBlood's pool, bronze for the fangs.

Each faces Blender -Y (the road side) and stands on the origin. A fount's model faces -Y, toward its
stand tile; a region turns each as layout() says.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\monuments\\cruor.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""

import math
import os
import random
import sys

import bmesh
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards", "buildings")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from buildings.cruor import kit as K  # noqa: E402

# Carved stone first: the kit's pinnacle uses index 0.
MATS = ["CruorCarved", "Bloodstone", "Bronze", "Sable", "FountBlood", "BloodDeep", "Iron", "CryptBanner", "Ashgrey",
        "Timber", "Oak", "Silver", "Gold", "Copper", "SaintBone", "Ember", "CruorPlinth"]
(STONE, NEEDLE, BRONZE, DARK, BLOOD, SATED, IRON, BANNER, SLATE, TIMBER, OAK, SILVER, GOLD, COPPER, BONE, EMBER,
 PLINTH) = range(len(MATS))
COLL = "Monuments"
STEP = 0.15                     # a step's rise: an easy walk


def make(name, b, smooth=K.SMOOTH):
    """The object. The podium's treads (the plinth's faces turned up) are smooth carved stone, its
    risers the coursed blocks."""
    for f in b.bm.faces:
        if f.material_index == PLINTH:
            f.normal_update()
            if f.normal.z > 0.9:
                f.material_index = STONE
    A.finish(b.bm)
    return A.mesh_from(name, b.bm, MATS, coll=COLL, smooth_angle=smooth)


def lathe(bm, cx, cy, profile, sides, mats, rot=0.0):
    """A turned surface round (cx, cy): `profile` is (r, z) points from the bottom's middle out, up and
    back in (a basin: its foot, its outside, its lip, its inside down to the blood); a point with r 0
    closes to the axis. mats: one material per segment."""
    rings = []
    for r, z in profile:
        if r == 0:
            rings.append([bm.verts.new((cx, cy, z))])
        else:
            rings.append([bm.verts.new((cx + r * math.cos(rot + 2 * math.pi * k / sides),
                                        cy + r * math.sin(rot + 2 * math.pi * k / sides), z)) for k in range(sides)])
    for i in range(len(rings) - 1):
        a, c = rings[i], rings[i + 1]
        for k in range(sides):
            n = (k + 1) % sides
            if len(a) == 1:
                f = bm.faces.new((a[0], c[n], c[k]))
            elif len(c) == 1:
                f = bm.faces.new((a[k], a[n], c[0]))
            else:
                f = bm.faces.new((a[k], a[n], c[n], c[k]))
            f.material_index = mats[i]


# ---- the podium ----------------------------------------------------------------------------------

def outline(plan, size, cut=0, arm=0):
    """A ring's outline, `size` tiles across: a square, a square with its corners cut back `cut` tiles
    on the slant (an octagon), or a cross whose arms are 2 * arm + 1 tiles wide."""
    H = size / 2
    if plan == "octagon" and cut:
        c = H - cut
        return [(c, -H), (H, -c), (H, c), (c, H), (-c, H), (-H, c), (-H, -c), (-c, -H)]
    if plan == "cross":
        a = arm + 0.5
        return [(a, -H), (a, -a), (H, -a), (H, a), (a, a), (a, H), (-a, H), (-a, a), (-H, a), (-H, -a), (-a, -a), (-a, -H)]
    return [(-H, -H), (H, -H), (H, H), (-H, H)]


def inside(pts, x, y):
    """Whether a tile's centre lies inside an outline (convex, or the cross: one of its two bars)."""
    if len(pts) == 12:
        a, H = pts[0][0], -pts[0][1]
        return (abs(x) <= a and abs(y) <= H) or (abs(y) <= a and abs(x) <= H)
    n = len(pts)
    for i in range(n):
        (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
        if (x1 - x0) * (y - y0) - (y1 - y0) * (x - x0) < -1e-9:
            return False
    return True


def rings(t):
    """The tier's podium rings, outermost first: (outline, top height)."""
    out = []
    for k, size in enumerate(t["rings"]):
        plan = t["plan"] if k == 0 else "square"
        out.append((outline(plan, size, t.get("cut", 0), t.get("arm", 0)), STEP * (k + 1)))
    return out


def level(t, x, y):
    """The height a player stands at on tile (x, y) of the tier's podium (0 off it)."""
    z = 0.0
    for pts, top in rings(t):
        if inside(pts, x, y):
            z = top
    return z


def slab(b, pts, z0, z1, m):
    if len(pts) == 12:                                              # a cross: its two bars
        a, H = pts[0][0], -pts[0][1]
        b.box(-a, -H, z0, a, H, z1, m)
        b.box(-H, -a, z0, H, a, z1, m)
        return
    b.hull([(x, y, z) for x, y in pts for z in (z0, z1)], m)


def podium(b, t):
    z = 0.0
    for pts, top in rings(t):
        slab(b, pts, z, top, PLINTH)
        z = top
    return z


def needle(b, z, height, w, collar=None, band=False):
    """The red needle: a carved stone foot, a square shaft tapering from 2w across to 1.3w, a
    pyramidion. collar: a moulded collar of pale stone at that fraction of its height (a bevel, a
    band, a bevel); band: a bronze band low on the shaft."""
    top = z + height
    b.hull([(x, y, z) for x in (-w - 0.12, w + 0.12) for y in (-w - 0.12, w + 0.12)] +
           [(x, y, z + 0.32) for x in (-w - 0.04, w + 0.04) for y in (-w - 0.04, w + 0.04)], STONE)        # its foot
    b.hull([(x, y, z + 0.3) for x in (-w, w) for y in (-w, w)] + [(x * 0.65, y * 0.65, top) for x in (-w, w) for y in (-w, w)], NEEDLE)
    b.hull([(x * 0.65, y * 0.65, top) for x in (-w, w) for y in (-w, w)] + [(0, 0, top + w * 1.6)], NEEDLE)
    if band:
        zb = z + 0.3 + height * 0.2
        ww = w * (1 - 0.35 * 0.22) + 0.015
        b.box(-ww, -ww, zb, ww, ww, zb + 0.12, BRONZE)
    if collar:
        zc = z + height * collar
        ww = w * (1 - 0.35 * collar)
        b.hull([(x, y, zz) for zz, k in ((zc, ww + 0.01), (zc + 0.08, ww + 0.1), (zc + 0.2, ww + 0.1), (zc + 0.28, ww - 0.01))
                for x in (-k, k) for y in (-k, k)], STONE)
    return top + w * 1.6


def pedestal(b, z, half, h):
    """A pale block under the needle in the podium's coursed stone, with a moulded base and cornice
    and a raised panel framed on each face (relief, in the same stone)."""
    b.hull([(x, y, zz) for zz, k in ((z, half + 0.1), (z + 0.12, half + 0.1), (z + 0.2, half)) for x in (-k, k) for y in (-k, k)], STONE)
    b.box(-half, -half, z + 0.2, half, half, z + h, PLINTH)
    b.hull([(x, y, zz) for zz, k in ((z + h, half), (z + h + 0.08, half + 0.12), (z + h + 0.18, half + 0.12)) for x in (-k, k)
            for y in (-k, k)], STONE)
    for ax, ay in ((0, -1), (1, 0), (0, 1), (-1, 0)):                  # a framed panel on each face
        sx, sy = -ay, ax
        p, w0, w1, z0, z1 = half + 0.035, half * 0.55, half * 0.55 - 0.07, z + 0.35, z + h - 0.15
        for (u0, u1, v0, v1) in ((-w0, w0, z1 - 0.07, z1), (-w0, w0, z0, z0 + 0.07), (-w0, -w1, z0, z1), (w1, w0, z0, z1)):
            b.hull([(ax * d + sx * u, ay * d + sy * u, v) for d in (half, p) for u in (u0, u1) for v in (v0, v1)], STONE)
    return z + h + 0.18


def lesser_needle(b, x, y, z, h, w):
    b.hull([(x + dx, y + dy, z) for dx in (-w - 0.1, w + 0.1) for dy in (-w - 0.1, w + 0.1)] +
           [(x + dx, y + dy, z + 0.25) for dx in (-w - 0.03, w + 0.03) for dy in (-w - 0.03, w + 0.03)], STONE)
    b.hull([(x + dx, y + dy, z + 0.24) for dx in (-w, w) for dy in (-w, w)] +
           [(x + dx * 0.65, y + dy * 0.65, z + 0.24 + h) for dx in (-w, w) for dy in (-w, w)], NEEDLE)
    b.hull([(x + dx * 0.65, y + dy * 0.65, z + 0.24 + h) for dx in (-w, w) for dy in (-w, w)] + [(x, y, z + 0.24 + h + w * 1.6)], NEEDLE)


def pinnacle(b, x, y, z):
    b.hull([(x + dx, y + dy, zz) for zz, k in ((z, 0.32), (z + 0.1, 0.32), (z + 0.16, 0.24)) for dx in (-k, k) for dy in (-k, k)], STONE)
    b.box(x - 0.2, y - 0.2, z + 0.16, x + 0.2, y + 0.2, z + 0.55, STONE)
    K.pinnacle(b, x, y, z + 0.55, half=0.2)


def runnels(b, t, z_top):
    """Blood runnels cut in the step from each fount in toward the needle, as far as the next ring."""
    for x, y in t["founts"]:
        d = Vector((x, y, 0))
        n = d.normalized()
        s = Vector((-n.y, n.x, 0)) * 0.05
        a, c = d - n * 0.4, n * (t["rings"][-1] / 2 + 0.02)
        z = level(t, x, y)
        b.hull([tuple(p + Vector((0, 0, zz))) for p in (a + s, a - s, c + s, c - s) for zz in (z + 0.001, z + 0.006)], BLOOD)


# ---- the ten tiers -------------------------------------------------------------------------------
# rings: the podium's rings in tiles across, outermost first, each a STEP higher (the outermost on
# the plan: square, octagon with its corners cut `cut` tiles, or a cross with arms 2 * arm + 1 wide).
# founts: tiles (x, y) from the centre tile. Everything else stands on the centre: a needle (height,
# half-width, collar, band), a pedestal (half-width, height) or a terrace (tiles across, height),
# lesser needles (tiles, height, half-width), corner pinnacles (tiles).

def quad(a, b):
    """Tiles (+-a, +-b) and (+-b, +-a): eight round the centre (four if a == 0 or a == b)."""
    out = []
    for x, y in ((a, b), (b, a)):
        for sx in (-1, 1):
            for sy in (-1, 1):
                if (sx * x, sy * y) not in out:
                    out.append((sx * x, sy * y))
    return out


TIERS = {
    1: dict(plan="square", rings=[3], height=2.4, w=0.3, band=True, founts=[(-1, -1), (1, -1)]),
    2: dict(plan="square", rings=[5, 3], height=3.0, w=0.34, band=True, founts=[(-1, -1), (1, -1), (0, 1)]),
    3: dict(plan="square", rings=[5, 3], height=3.6, w=0.38, band=True, founts=quad(1, 1)),
    4: dict(plan="square", rings=[7, 5, 3], height=4.2, w=0.42, band=True, collar=0.62, pinnacles=quad(3, 3),
            founts=quad(0, 2)),
    5: dict(plan="square", rings=[7, 5], height=4.4, w=0.42, collar=0.6, pedestal=(1.3, 1.1), pinnacles=quad(3, 3),
            founts=quad(0, 2)),
    6: dict(plan="octagon", cut=2, rings=[9, 7, 5], height=5.0, w=0.44, collar=0.6, pedestal=(1.3, 1.1),
            lesser=(quad(3, 3), 1.8, 0.16), founts=[(-1, -2), (1, -2), (-1, 2), (1, 2), (-2, 0), (2, 0)]),
    7: dict(plan="octagon", cut=2, rings=[9, 7, 5, 3], height=5.8, w=0.5, collar=0.62, runnels=True,
            founts=[(-1, -2), (1, -2), (-1, 2), (1, 2), (-2, 0), (2, 0)]),
    8: dict(plan="square", rings=[9, 7, 5], height=6.4, w=0.52, collar=0.62, pedestal=(1.3, 1.6),
            lesser=(quad(3, 3), 2.4, 0.2), founts=quad(1, 2)),
    9: dict(plan="cross", arm=2, rings=[11, 7, 5], height=7.4, w=0.56, collar=0.62, pedestal=(1.3, 1.4),
            lesser=([(0, -5), (5, 0), (0, 5), (-5, 0)], 2.6, 0.2), founts=quad(1, 3)),
    10: dict(plan="square", rings=[11, 9], height=8.6, w=0.62, collar=0.6, terrace=(5, 0.6),
             lesser=(quad(2, 2), 3.0, 0.22), founts=quad(1, 3)),
}


def outward(x, y):
    """The way a fount at tile (x, y) faces: out from the centre along its main axis (front or back
    when it sits on a diagonal), toward the tile its user stands on."""
    if abs(x) > abs(y):
        return (1 if x > 0 else -1, 0)
    return (0, 1 if y > 0 else -1)


def layout(tier):
    """Where everything goes on a tier, for a region's data: founts (tile x, y, the height it stands
    at, its facing in degrees about Z), stand (the tile in front of each fount and its height) and
    blocked (tiles nobody walks: the needle, pedestal or terrace, lesser needles, pinnacles, founts)."""
    t = TIERS[tier]
    founts, stand = [], []
    for x, y in t["founts"]:
        dx, dy = outward(x, y)
        founts.append((x, y, level(t, x, y), round(math.degrees(math.atan2(dy, dx)) + 90) % 360))
        stand.append((x + dx, y + dy, level(t, x + dx, y + dy)))
    core = 2 if "terrace" in t else 1 if "pedestal" in t else 0
    blocked = [(x, y) for x in range(-core, core + 1) for y in range(-core, core + 1)]
    blocked += list(t.get("lesser", ([],))[0]) + list(t.get("pinnacles", [])) + list(t["founts"])
    return {"founts": founts, "stand": stand, "blocked": sorted(set(blocked))}


def check(tier):
    """A tier's layout walks: every stand tile free, distinct, and at most one step below its fount."""
    lay = layout(tier)
    blocked = set(lay["blocked"])
    tiles = [(x, y) for x, y, _ in lay["stand"]]
    assert len(set(tiles)) == len(tiles), "tier %d: two founts share a stand tile" % tier
    for (x, y, z), (fx, fy, fz, _) in zip(lay["stand"], lay["founts"]):
        assert (x, y) not in blocked, "tier %d: the stand tile %s is blocked" % (tier, (x, y))
        assert 0 <= fz - z <= STEP + 1e-6, "tier %d: fount %s is more than a step above its stand tile" % (tier, (fx, fy))
    return lay


def monument(tier):
    """A monument of the tier: TIERS says how grand, layout(tier) where its founts and their users go."""
    t = TIERS[tier]
    check(tier)
    b = K.Build()
    z = podium(b, t)
    for x, y in t.get("pinnacles", []):
        pinnacle(b, x, y, level(t, x, y))
    if t.get("runnels"):
        runnels(b, t, z)
    if "terrace" in t:                                                # a terrace raised over the court
        size, h = t["terrace"]
        H = size / 2
        b.box(-H, -H, z, H, H, z + h - 0.1, PLINTH)
        b.hull([(x, y, zz) for zz, k in ((z + h - 0.1, H), (z + h - 0.04, H + 0.1), (z + h, H + 0.1)) for x in (-k, k) for y in (-k, k)], STONE)
        z += h
    base = z
    if "pedestal" in t:
        base = pedestal(b, z, *t["pedestal"])
    needle(b, base, t["height"], t["w"], collar=t.get("collar"), band=t.get("band", False))
    if "lesser" in t:
        tiles, h, w = t["lesser"]
        for x, y in tiles:
            lesser_needle(b, x, y, z if "terrace" in t else level(t, x, y), h, w)
    return make("Monument%d" % tier, b)


# ---- founts --------------------------------------------------------------------------------------
# A fount is a basin carved from one stone with the blood standing a hand below its lip, and a crown
# of curved bronze fangs set in the lip, leaning in over the blood (they close on the arm). Sated: the
# blood brims dark to the lip and the fangs have closed over it. Its rim stands about at a player's
# waist from the stand tile a step below.

def fangs(bm, r, z_lip, n, closed, size=1.0):
    """The crown of bronze fangs set in the lip: each stands up from the lip and curls a little in over
    the blood, so the blood shows open between them; closed (sated), they bend over and meet above
    the middle."""
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        out = Vector((math.cos(a), math.sin(a), 0))
        base = out * r + Vector((0, 0, z_lip - 0.03))
        if closed:
            secs = [(base, 0.032 * size), (out * r * 0.8 + Vector((0, 0, z_lip + 0.13 * size)), 0.022 * size),
                    (out * 0.03 + Vector((0, 0, z_lip + 0.1 * size)), 0)]
        else:
            secs = [(base, 0.032 * size), (out * (r * 0.97) + Vector((0, 0, z_lip + 0.1 * size)), 0.022 * size),
                    (out * (r * 0.8) + Vector((0, 0, z_lip + 0.16 * size)), 0)]
        A.loft(bm, [(tuple(p), w, w) for p, w in secs], sides=3, ref=(0, 0, 1), mats=BRONZE)


def fount(grade=1, sated=False):
    """A fount on its tile, facing -Y (its stand tile). Grade 1 (tiers 1-3): a round drum basin, five
    fangs. Grade 2 (tiers 4-6): a basin on a stem and a moulded foot, a bronze band at its lip, six
    fangs. Grade 3 (tiers 7-10): a broad eight-sided basin on a square chamfered block, a bronze band,
    eight fangs."""
    b = K.Build()
    bm = b.bm
    if grade == 1:
        lip, inner, n, size = 0.64, 0.27, 5, 1.0
        blood = lip - 0.01 if sated else lip - 0.12
        lathe(bm, 0, 0, [(0, 0), (0.3, 0), (0.31, 0.06), (0.27, 0.36), (0.34, 0.56), (0.35, lip - 0.02), (0.31, lip),
                         (inner, lip - 0.03), (inner - 0.01, blood), (0, blood)], 10, [STONE] * 8 + [SATED if sated else BLOOD])
        fangs(bm, 0.3, lip, n, sated, size)
    elif grade == 2:
        lip, inner, n, size = 0.76, 0.34, 6, 1.15
        blood = lip - 0.01 if sated else lip - 0.13
        lathe(bm, 0, 0, [(0, 0), (0.33, 0), (0.33, 0.07), (0.2, 0.11), (0.15, 0.33), (0.38, 0.62), (0.43, lip - 0.03),
                         (0.38, lip), (inner, lip - 0.04), (inner - 0.02, blood), (0, blood)], 10, [STONE] * 9 + [SATED if sated else BLOOD],
              rot=math.pi / 10)
        A.prism(bm, 10, 0.435, 0.435, lip - 0.09, lip - 0.04, mat_index=BRONZE, cap_top=False, rot=math.pi / 10)
        fangs(bm, 0.37, lip, n, sated, size)
    else:
        lip, inner, n, size = 0.86, 0.38, 8, 1.3
        blood = lip - 0.01 if sated else lip - 0.13
        b.hull([(x, y, z) for z, k in ((0.0, 0.42), (0.16, 0.42), (0.24, 0.34)) for x in (-k, k) for y in (-k, k)], STONE)
        lathe(bm, 0, 0, [(0, 0.24), (0.33, 0.24), (0.3, 0.32), (0.44, 0.72), (0.48, lip - 0.03), (0.42, lip),
                         (inner, lip - 0.04), (inner - 0.02, blood), (0, blood)], 8, [STONE] * 7 + [SATED if sated else BLOOD],
              rot=math.pi / 8)
        A.prism(bm, 8, 0.485, 0.485, lip - 0.1, lip - 0.045, mat_index=BRONZE, cap_top=False, rot=math.pi / 8)
        fangs(bm, 0.41, lip, n, sated, size)
    name = {1: "Fount", 2: "FountCarved", 3: "FountGreat"}[grade] + ("Sated" if sated else "")
    return make(name, b, smooth=40)


# ---- reliquaries ---------------------------------------------------------------------------------

def reliquary_stand():
    """Where a reliquary is set before a monument: a carved pedestal with a moulded base and cap, and a
    shallow bronze dish on top, stained, that the casing sits in; empty until someone places one."""
    b = K.Build()
    b.hull([(x, y, z) for z, k in ((0, 0.36), (0.1, 0.36), (0.16, 0.28)) for x in (-k, k) for y in (-k, k)], STONE)
    b.box(-0.26, -0.26, 0.16, 0.26, 0.26, 0.82, STONE)
    b.hull([(x, y, z) for z, k in ((0.82, 0.26), (0.88, 0.34), (0.95, 0.34)) for x in (-k, k) for y in (-k, k)], STONE)
    A.loft(b.bm, [((0, 0, 0.95), 0.14, 0.14), ((0, 0, 0.98), 0.22, 0.22)], sides=10, ref=(1, 0, 0), mats=BRONZE)
    A.prism(b.bm, 10, 0.17, 0.17, 0.97, 0.981, mat_index=SATED)
    return make("ReliquaryStand", b, smooth=30)


def reliquary(grade):
    """A reliquary: remains sealed in a casing of other skills' materials (skills.md 4.4), grade by
    grade. 1: a small box of wood bound with copper at the corners. 2: an iron-bound casket with a
    gabled lid and a hasp. 3: a little house-shaped shrine in silver with a gilt ridge, red glass in
    its end and a bone showing through it. Each sits on the stand's seat (its origin at its foot)."""
    b = K.Build()
    if grade == 1:
        b.box(-0.16, -0.11, 0, 0.16, 0.11, 0.16, OAK)
        for x in (-0.16, 0.16):
            for y in (-0.11, 0.11):
                b.box(x - 0.025, y - 0.025, 0, x + 0.025, y + 0.025, 0.17, COPPER)
        b.box(-0.17, -0.12, 0.16, 0.17, 0.12, 0.2, OAK)
    elif grade == 2:
        b.box(-0.19, -0.12, 0, 0.19, 0.12, 0.18, OAK)
        b.hull([(x, y, 0.18) for x in (-0.2, 0.2) for y in (-0.13, 0.13)] + [(x, 0, 0.3) for x in (-0.2, 0.2)], OAK)
        for x in (-0.12, 0.0, 0.12):
            b.box(x - 0.02, -0.13, 0, x + 0.02, 0.13, 0.19, IRON)
            b.hull([(x + dx, y, z) for dx in (-0.02, 0.02) for y, z in ((-0.135, 0.18), (0.135, 0.18), (0, 0.305))], IRON)
        b.box(-0.03, -0.135, 0.06, 0.03, -0.125, 0.15, IRON)
    else:
        b.box(-0.24, -0.15, 0, 0.24, 0.15, 0.06, GOLD)
        b.box(-0.21, -0.13, 0.06, 0.21, 0.13, 0.26, SILVER)
        b.hull([(x, y, 0.26) for x in (-0.23, 0.23) for y in (-0.15, 0.15)] + [(x, 0, 0.42) for x in (-0.23, 0.23)], SILVER)
        b.box(-0.24, -0.02, 0.41, 0.24, 0.02, 0.45, GOLD)
        for s in (-1, 1):
            K.pinnacle(b, s * 0.23, 0.0, 0.45, half=0.03)
            K.arch_panel(b, K.frame((s * 0.211, 0), (0, s), (s, 0)), 0.07, 0.08, 0.18, NEEDLE, depth=0.004)
        K.arch_panel(b, K.frame((0, -0.131), (1, 0), (0, -1)), 0.11, 0.09, 0.17, BANNER, depth=0.003)
        b.hull([(x, -0.133, z) for x, z in ((-0.06, 0.12), (0.06, 0.12), (0.0, 0.2))] + [(0.0, -0.134, 0.15)], BONE)
    names = {1: "ReliquaryWood", 2: "ReliquaryIron", 3: "ReliquarySilver"}
    return make(names[grade], b, smooth=30)


# ---- the faith's dressing ------------------------------------------------------------------------

def offering_bowls():
    """A low pale slab before a monument with three bronze dishes of blood on it and a bone laid
    across one, what pilgrims leave."""
    b = K.Build()
    b.box(-0.55, -0.25, 0, 0.55, 0.25, 0.32, STONE)
    b.box(-0.6, -0.3, 0.32, 0.6, 0.3, 0.38, STONE)
    for x in (-0.33, 0.0, 0.33):
        A.loft(b.bm, [((x, 0, 0.38), 0.07, 0.07), ((x, 0, 0.42), 0.13, 0.13), ((x, 0, 0.44), 0.14, 0.14)], sides=10, ref=(1, 0, 0), mats=BRONZE)
        A.prism(b.bm, 10, 0.11, 0.11, 0.425, 0.443, center=(x, 0), mat_index=BLOOD)
    A.loft(b.bm, [((0.24, 0.04, 0.46), 0.02, 0.02), ((0.42, -0.05, 0.46), 0.02, 0.02)], sides=5, ref=(0, 0, 1), mats=BONE)
    return make("OfferingBowls", b, smooth=40)


def kneeler():
    """A stone kneeler before a monument: a long low step, its top worn hollow in two places."""
    b = K.Build()
    b.box(-0.6, -0.2, 0, 0.6, 0.2, 0.18, STONE)
    b.box(-0.65, -0.24, 0.18, 0.65, 0.24, 0.24, STONE)
    for x in (-0.25, 0.25):
        b.hull([(x + 0.14 * math.cos(a), 0.11 * math.sin(a), 0.241) for a in (2 * math.pi * k / 8 for k in range(8))] + [(x, 0, 0.243)], SLATE)
    return make("Kneeler", b)


def pilgrim_banner():
    """The faith's banner on a pole, carried to a new monument and planted by it: a dark pole, a
    cross-arm, the red cloth hanging to a point with a pale stripe, a bronze finial (not a cross)."""
    b = K.Build()
    A.loft(b.bm, [((0, 0, -0.1), 0.035, 0.035), ((0, 0, 3.0), 0.03, 0.03)], sides=6, ref=(1, 0, 0), mats=TIMBER)
    A.loft(b.bm, [((-0.42, 0, 2.75), 0.02, 0.02), ((0.42, 0, 2.75), 0.02, 0.02)], sides=5, ref=(0, 0, 1), mats=TIMBER)
    b.hull([(x, y, z) for x, z in ((-0.4, 2.72), (0.4, 2.72), (0.4, 1.4), (0.0, 1.15), (-0.4, 1.4)) for y in (-0.01, 0.01)], BANNER)
    b.hull([(x, y, z) for x, z in ((-0.08, 2.6), (0.08, 2.6), (0.08, 1.45), (-0.08, 1.45)) for y in (-0.014, 0.014)], STONE)
    A.loft(b.bm, [((0, 0, 3.0), 0.0, 0.0), ((0, 0, 3.06), 0.05, 0.05), ((0, 0, 3.12), 0.0, 0.0)], sides=6, ref=(1, 0, 0), mats=BRONZE)
    return make("PilgrimBanner", b, smooth=30)


def stone_brazier():
    """A monument's brazier: a pale stone pillar with the merlon round its head, a bronze bowl of
    coals on it (the flame is the game's)."""
    rnd = random.Random(3)
    b = K.Build()
    b.box(-0.3, -0.3, 0, 0.3, 0.3, 0.15, STONE)
    b.box(-0.2, -0.2, 0.15, 0.2, 0.2, 0.95, STONE)
    b.box(-0.27, -0.27, 0.95, 0.27, 0.27, 1.05, STONE)
    A.loft(b.bm, [((0, 0, 1.05), 0.16, 0.16), ((0, 0, 1.2), 0.32, 0.32), ((0, 0, 1.24), 0.33, 0.33)], sides=10, ref=(1, 0, 0), mats=BRONZE)
    A.prism(b.bm, 10, 0.29, 0.29, 1.2, 1.245, mat_index=EMBER)                                  # the glow between the coals
    for k in range(16):                                                                        # the coals, heaped over it
        a, d = 2 * math.pi * k / 16 * 3 + rnd.uniform(-0.3, 0.3), 0.04 + 0.2 * (k % 5) / 4
        x, y, s = d * math.cos(a), d * math.sin(a), rnd.uniform(0.06, 0.09)
        b.hull([(x + rnd.uniform(-s, s), y + rnd.uniform(-s, s), 1.24 + rnd.uniform(0, s) + 0.06 * (1 - d / 0.24)) for _ in range(6)], DARK)
    return make("StoneBrazier", b, smooth=30)


def chain_post():
    """A pale bollard with the one pinnacle on top and an iron ring each side, for a chain round a
    monument's court (ChainSpan hangs between two)."""
    b = K.Build()
    b.box(-0.13, -0.13, 0, 0.13, 0.13, 0.75, STONE)
    K.pinnacle(b, 0, 0, 0.75, half=0.13)
    for s in (-1, 1):
        A.loft(b.bm, [((s * 0.15, -0.025, 0.62), 0.05, 0.05), ((s * 0.15, 0.025, 0.62), 0.05, 0.05)], sides=6, ref=(0, 0, 1), mats=IRON)
    return make("ChainPost", b, smooth=30)


def link(bm, c, along, flat, length=0.1, width=0.05, r=0.011):
    """One iron link: an oval loop of wire round centre c, its long axis `along`, lying in the plane
    of `along` and `flat`."""
    pts = [c + along * (length / 2 * math.cos(2 * math.pi * k / 6)) + flat * (width / 2 * math.sin(2 * math.pi * k / 6)) for k in range(6)]
    for p, q in zip(pts, pts[1:] + pts[:1]):
        A.loft(bm, [(tuple(p), r, r), (tuple(q), r, r)], sides=3, ref=tuple(along.cross(flat)), mats=IRON)


def chain_span():
    """One tile of chain between two ChainPosts (their rings at x = -0.5 + 0.15 and 0.5 - 0.15), its
    links interlocking, each turned a quarter from the last, sagging between the posts."""
    b = K.Build()
    n = 9
    pts = []
    for k in range(n + 1):
        t = k / n
        pts.append(Vector((-0.35 + 0.7 * t, 0.0, 0.62 - 0.16 * math.sin(math.pi * t))))
    for k in range(n):
        c = (pts[k] + pts[k + 1]) / 2
        along = (pts[k + 1] - pts[k]).normalized()
        side = Vector((0, 1, 0))
        flat = side if k % 2 else along.cross(side).normalized()
        link(b.bm, c, along, flat, length=(pts[k + 1] - pts[k]).length * 1.35)
    return make("ChainSpan", b, smooth=None)


def stele():
    """A proclamation stele raised beside a monument: a pale slab with a pointed head on a base, the
    oath cut into it in rows of dark strokes, a red runnel down its face to a bowl at its foot."""
    b = K.Build()
    b.box(-0.5, -0.25, 0, 0.5, 0.25, 0.2, STONE)
    f = K.frame((0, -0.12), (1, 0), (0, -1))
    K.arch_panel(b, f, 0.38, 0.2, 1.7, STONE, depth=0.24, d0=-0.24)
    for k in range(7):
        z = 1.55 - 0.15 * k
        w = 0.28 if k else 0.18
        b.box(-w, -0.125, z, w, -0.12, z + 0.04, DARK)
    b.box(-0.02, -0.126, 0.35, 0.02, -0.12, 0.45, BLOOD)
    A.loft(b.bm, [((0, -0.3, 0.2), 0.08, 0.08), ((0, -0.3, 0.27), 0.12, 0.12)], sides=8, ref=(1, 0, 0), mats=BRONZE)
    A.prism(b.bm, 8, 0.1, 0.1, 0.25, 0.273, center=(0, -0.3), mat_index=BLOOD)
    return make("Stele", b, smooth=30)


def runnel():
    """One tile of the blood runnel along X: a pale stone channel sunk flush in the ground, the blood
    in its groove, for leading the founts' blood away across a court."""
    b = K.Build()
    b.box(-0.5, -0.2, -0.05, 0.5, -0.07, 0.04, STONE)
    b.box(-0.5, 0.07, -0.05, 0.5, 0.2, 0.04, STONE)
    b.box(-0.5, -0.07, -0.05, 0.5, 0.07, -0.02, STONE)
    b.box(-0.5, -0.07, -0.02, 0.5, 0.07, 0.01, BLOOD)
    return make("Runnel", b, smooth=None)


def monument_site():
    """Where a monument will be raised and hasn't been yet (the state before a player first raises it):
    its plot marked out with stakes and cord, the first course of the plinth laid, the needle's
    foot stone set with a mason's line on it, red blocks waiting in a stack, a mallet."""
    rnd = random.Random(6)
    b = K.Build()
    for x in (-1.6, 1.6):
        for y in (-1.6, 1.6):
            A.loft(b.bm, [((x, y, -0.1), 0.03, 0.03), ((x, y, 0.5), 0.03, 0.03), ((x, y, 0.58), 0.0, 0.0)], sides=4, ref=(1, 0, 0), mats=TIMBER)
    for (x0, y0), (x1, y1) in (((-1.6, -1.6), (1.6, -1.6)), ((1.6, -1.6), (1.6, 1.6)), ((1.6, 1.6), (-1.6, 1.6)), ((-1.6, 1.6), (-1.6, -1.6))):
        A.loft(b.bm, [((x0, y0, 0.45), 0.006, 0.006), ((x1, y1, 0.45), 0.006, 0.006)], sides=3,
               ref=(0, 0, 1), mats=TIMBER)
    for k in range(7):                                                                          # the first course, half laid
        x = -1.25 + 0.42 * k
        b.box(x, -1.3, 0, x + 0.4, -0.9, STEP, PLINTH)
        if k < 4:
            b.box(-1.3, x, 0, -0.9, x + 0.4, STEP, PLINTH)
    b.box(-0.35, -0.35, 0, 0.35, 0.35, 0.3, NEEDLE)                                             # the needle's foot stone
    for row in range(3):                                                                        # red blocks waiting
        for k in range(3 - row):
            x = 0.6 + 0.42 * k + 0.21 * row + rnd.uniform(-0.02, 0.02)
            b.box(x, 0.6, 0.3 * row, x + 0.4, 1.1, 0.3 * row + 0.29, NEEDLE)
    A.loft(b.bm, [((0.3, -0.6, 0.05), 0.02, 0.02), ((0.7, -0.75, 0.05), 0.02, 0.02)], sides=5, ref=(0, 0, 1), mats=TIMBER)
    b.box(0.68, -0.85, 0.0, 0.82, -0.65, 0.12, OAK)
    return make("MonumentSite", b, smooth=None)


ASSETS = {
    **{"Monument%d" % t: (lambda t=t: monument(t)) for t in TIERS},
    "Fount": lambda: fount(1),
    "FountSated": lambda: fount(1, sated=True),
    "FountCarved": lambda: fount(2),
    "FountCarvedSated": lambda: fount(2, sated=True),
    "FountGreat": lambda: fount(3),
    "FountGreatSated": lambda: fount(3, sated=True),
    "ReliquaryStand": reliquary_stand,
    "ReliquaryWood": lambda: reliquary(1),
    "ReliquaryIron": lambda: reliquary(2),
    "ReliquarySilver": lambda: reliquary(3),
    "OfferingBowls": offering_bowls,
    "Kneeler": kneeler,
    "PilgrimBanner": pilgrim_banner,
    "StoneBrazier": stone_brazier,
    "ChainPost": chain_post,
    "ChainSpan": chain_span,
    "Stele": stele,
    "Runnel": runnel,
    "MonumentSite": monument_site,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
