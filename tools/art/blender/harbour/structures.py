"""The harbour kit's waterfront: jetties, quays, stairs, slipway, pontoon, gangplank, and the dressing
along them. Every walkable part writes its walk layout (common.Walk). docs/harbour-kit.md.

Built as a carpenter and a mason would: piles driven in pairs with headstocks across them and joists
along, cross-braced; deck boards of uneven widths with ragged ends and the odd replacement; a band of
weed wherever timber or stone stands in the water; a quay of battered stone with a coping of separate
blocks, timber fenders against its face, iron bollards and rings.
"""
import math
import random

import bmesh
from mathutils import Vector

import rth_art as A
import artlib.nature as N
import common as C
from common import (BED, JETTY, QUAY, WEED, Walk, beam, bollard_at, deck, deck_boards, make, pile, ring_on, rope_coil)

MATS = ["Fence", "Boards", "Iron", "Driftwood", "Moss", "DeadWood", "Hide", "Harbourstone", "Gravestone", "Mud"]
TIM, BOARD, IRON, GREY, WEEDM, OLD, ROPE, STONE, COPE, MUD = range(len(MATS))


def frame(bm, rnd, xs, ys, deck, posts=(), post_h=0.85, brace_every=2):
    """A jetty's frame: a pile at every (x, y) of xs x ys, a headstock along each row of piles (across
    X), joists along Y over them, X-bracing between the outer piles of every other row. posts: the
    piles (x, y) that stand on above the deck as mooring posts."""
    for y in ys:
        for x in xs:
            tall = (x, y) in posts
            pile(bm, x, y, deck + (post_h if tall else -0.04), rnd, r=0.13 if tall else 0.115, mat=TIM, weed=WEEDM)
        beam(bm, (xs[0] - 0.12, y, deck - 0.24), (xs[-1] + 0.12, y, deck - 0.24), 0.13, 0.13, mat=TIM)
    for x in xs:
        beam(bm, (x, ys[0] - 0.05, deck - 0.12), (x, ys[-1] + 0.05, deck - 0.12), 0.1, 0.1, mat=OLD)
    for i, y in enumerate(ys):
        if i % brace_every:
            continue
        for a, b in ((xs[0], xs[-1]), (xs[-1], xs[0])):
            beam(bm, (a, y + 0.13, deck - 0.3), (b, y + 0.13, WEED[1] + 0.05), 0.07, 0.05, mat=OLD)


def kerb(bm, x, y0, y1, deck_z, rnd=None):
    """A kerb along the deck's edge, so the edge reads from the game's camera. Worn, a length of it
    has gone."""
    if C.WEAR and rnd is not None and y1 - y0 > 1.2 and rnd.random() < 0.6:
        g = rnd.uniform(y0 + 0.3, y1 - 0.9)
        beam(bm, (x, y0, deck_z + 0.035), (x, g, deck_z + 0.035), 0.07, 0.1, mat=TIM)
        beam(bm, (x, g + rnd.uniform(0.35, 0.6), deck_z + 0.035), (x, y1, deck_z + 0.035), 0.07, 0.1, mat=TIM)
        return
    beam(bm, (x, y0, deck_z + 0.035), (x, y1, deck_z + 0.035), 0.07, 0.1, mat=TIM)


def jetty_span(width=1, length=1, name=None):
    """A straight run of jetty out over the water along +Y, `width` tiles wide: piles in pairs at the
    start of each tile under a headstock, joists along, X-bracing every other pair, boards across
    with a kerb along each side, the deck a half tile above the water. Walkable; its sides are open
    (the water beside it is blocked anyway). Worn: a board missing, snapped or patched here and
    there, a length of kerb gone."""
    name = name or "JettySpan%dx%d" % (width, length)
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    x0, x1 = -0.5, width - 0.5
    xs = [x0 + 0.12] + ([x0 + width / 2] if width > 1 else []) + [x1 - 0.12]
    frame(bm, rnd, xs, [k - 0.5 for k in range(length + 1)], JETTY)
    deck(bm, x0 - 0.06, x1 + 0.06, -0.5, length - 0.5, JETTY, BOARD, OLD, rnd, C.WEAR)
    for x in (x0 + 0.03, x1 - 0.03):
        kerb(bm, x, -0.5, length - 0.5, JETTY, rnd)
    w = Walk(name)
    w.rect(0, 0, width, length, JETTY)
    return make(name, bm, MATS, w)


def jetty_head(name="JettyHead"):
    """A jetty's end: a platform three tiles square, a tall mooring post at each corner with rope
    wound on it, a ladder down to the water on its far side, a coil of rope on the boards."""
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    xs = ys = [-0.38, 1.0, 2.38]
    posts = {(-0.38, -0.38), (2.38, -0.38), (-0.38, 2.38), (2.38, 2.38)}
    frame(bm, rnd, xs, ys, JETTY, posts=posts, brace_every=1)
    deck(bm, -0.56, 2.56, -0.5, 2.5, JETTY, BOARD, OLD, rnd, C.WEAR)
    for x in (-0.47, 2.47):
        kerb(bm, x, -0.5, 2.5, JETTY, rnd)
    for x, y in ((-0.38, 2.38), (2.38, 2.38)):                  # rope wound on the far posts
        for k in range(3):
            A.prism(bm, 7, 0.16, 0.16, JETTY + 0.45 + 0.06 * k, JETTY + 0.5 + 0.06 * k, (x, y), mat_index=ROPE)
    for x in (0.78, 1.22):                                       # the ladder: timber stiles, iron rungs
        beam(bm, (x, 2.6, WEED[0] - 0.5), (x, 2.6, JETTY + 0.4), 0.07, 0.07, mat=TIM)
    for k, z in enumerate((-0.45, -0.15, 0.15, 0.42)):
        if C.WEAR and k == 1:
            continue                                             # a rung gone
        A.loft(bm, [(Vector((0.78, 2.6, z)), 0.02, 0.02), (Vector((1.22, 2.6, z)), 0.02, 0.02)], sides=4, ref=(0, 0, 1), mats=IRON)
    rope_coil(bm, (0.3, 0.1, JETTY), 0.22, ROPE)
    w = Walk(name)
    w.rect(0, 0, 3, 3, JETTY)
    w.drop((0, 0), (2, 0), (0, 2), (2, 2))                      # the posts block their corners
    return make(name, bm, MATS, w)


def jetty_stairs(name="JettyStairs"):
    """Steps from the bank (a quay's height) down onto a jetty along +Y, two tiles: a stringer each
    side notched for the treads, each tread with a nosing, a post at the foot of each stringer. Worn:
    one tread gone, one split across."""
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    n = 5
    for k in range(n):
        z = QUAY - 0.02 - (QUAY - JETTY) * (k + 1) / (n + 1)
        y0, y1 = -0.5 + 1.9 * k / n, -0.5 + 1.9 * (k + 1) / n
        if C.WEAR and k == 2:
            continue
        if C.WEAR and k == 3:
            A.box(bm, (-0.42, y0, z - 0.05), (-0.05, y1 + 0.04, z), GREY)
            A.hull(bm, [Vector((xx, yy, zz)) for yy in (y0, y1 + 0.04) for xx, zz in ((0.02, z - 0.03), (0.02, z - 0.08), (0.42, z), (0.42, z - 0.05))], GREY)
            continue
        A.box(bm, (-0.42, y0, z - 0.05), (0.42, y1 + 0.04, z), BOARD if k % 3 else GREY)
    for x in (-0.47, 0.47):
        A.hull(bm, [Vector((x + s, y, z)) for s in (-0.04, 0.04) for y, z in ((-0.5, QUAY), (-0.5, QUAY - 0.32),
                                                                          (1.5, JETTY - 0.25), (1.5, JETTY + 0.08))], TIM)
        pile(bm, x, 1.45, JETTY + 0.15, rnd, r=0.09, mat=TIM, weed=WEEDM)
    w = Walk(name)
    w.tile(0, 0, QUAY - 0.22)
    w.tile(0, 1, JETTY + 0.12)
    return make(name, bm, MATS, w)


def quay_edge(length=4, name=None):
    """A stone quay's face along X, the water toward -Y: a battered wall from the bed to the bank
    (the stone's texture carries its courses), a weed band at the waterline, a coping of separate
    pale blocks overhanging the face, an iron bollard on every other block, a ring under each
    bollard, and a timber fender against the face between them. Worn: chipped and missing coping,
    stones fallen at the foot, a fender gone."""
    name = name or "QuayEdge%d" % length
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    x0, x1 = -0.5, length - 0.5

    def face_y(z):                                  # the battered face: out at the bed, in at the top
        return -0.88 + 0.33 * (z - BED) / (QUAY - 0.1 - BED)
    A.hull(bm, [Vector((x, y, z)) for x in (x0, x1) for y, z in ((face_y(BED), BED), (face_y(QUAY - 0.1), QUAY - 0.1),
                                                                   (0.5, QUAY - 0.1), (0.5, BED))], STONE)
    A.hull(bm, [Vector((x, y, z)) for x in (x0, x1) for y, z in ((face_y(WEED[0]) - 0.015, WEED[0]), (face_y(WEED[1]) - 0.015, WEED[1]),
                                                                   (face_y(WEED[0]) + 0.05, WEED[0]), (face_y(WEED[1]) + 0.05, WEED[1]))], WEEDM)
    for k in range(length):                         # the coping, block by block
        a, b = k - 0.5 + 0.012, k + 0.5 - 0.012
        pts = [Vector((x, y, QUAY - 0.12)) for x in (a, b) for y in (-0.64, 0.25)] + \
              [Vector((x, y, QUAY)) for x in (a + 0.02, b - 0.02) for y in (-0.61, 0.25)]
        if C.WEAR and rnd.random() < 0.3:
            chip_off(pts, Vector((rnd.choice((-1, 1)), -1, 0)), rnd)
            loose_stone(bm, (rnd.uniform(a + 0.2, b - 0.2), face_y(-0.3) - rnd.uniform(0.2, 0.4), -0.25), 0.08, rnd, COPE)
        A.hull(bm, pts, COPE)
    for k in range(0, length, 2):
        bollard_at(bm, k, -0.2, QUAY, IRON)
        ring_on(bm, (k, face_y(QUAY - 0.4) - 0.01, QUAY - 0.38), (0, -1, 0), 0.09, IRON)
    for k in range(1, length, 2):                   # fenders between the bollards
        if C.WEAR and rnd.random() < 0.3:
            continue
        beam(bm, (k, face_y(BED + 0.3) - 0.09, BED + 0.3), (k, face_y(QUAY - 0.16) - 0.09, QUAY - 0.16), 0.16, 0.14, mat=OLD)
    if C.WEAR:
        for _ in range(max(1, length // 3)):
            loose_stone(bm, (rnd.uniform(x0 + 0.3, x1 - 0.3), face_y(-0.3) - rnd.uniform(0.3, 0.6), -0.3), rnd.uniform(0.15, 0.22), rnd, STONE)
    w = Walk(name)
    w.rect(0, 0, length, 1, QUAY)
    for k in range(0, length, 2):
        w.drop((k, 0))                              # the bollards block their tiles
    return make(name, bm, MATS, w)


def quay_steps(name="QuaySteps"):
    """Water stairs let into a quay's face, running down along it (+X) from the coping to the water:
    three broad stone steps with pale nosings, the lowest green with weed, a low wall with its own
    coping on the water side. Worn: a nosing chipped, the chip in the water."""
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    hs = [QUAY - 0.2, JETTY, 0.15]
    for k, h in enumerate(hs):
        A.box(bm, (k - 0.5, -0.5, BED), (k + 0.5, 0.5, h - 0.06), STONE)
        chip = Vector((-1, -1, 0)) if (C.WEAR and k == 1) else None
        nosing(bm, k - 0.52, -0.5, k + 0.5, 0.5, h, WEEDM if k == 2 else COPE, rnd, chip)
    A.box(bm, (-0.5, -0.66, BED), (2.5, -0.5, QUAY - 0.08), STONE)
    A.hull(bm, [Vector((x, y, z)) for x in (-0.5, 2.5) for y, z in ((-0.7, QUAY - 0.1), (-0.46, QUAY - 0.1), (-0.68, QUAY), (-0.48, QUAY))], COPE)
    A.box(bm, (-0.5, -0.69, WEED[0]), (2.5, -0.66, WEED[1]), WEEDM)
    if C.WEAR:
        loose_stone(bm, (1.6, -0.95, -0.2), 0.12, rnd, COPE)
    w = Walk(name)
    for k, h in enumerate(hs):
        w.tile(k, 0, h)
    w.wall(0, 0, 3, 0)
    return make(name, bm, MATS, w)


def slipway(name="Slipway"):
    """A slipway for hauling boats out: two tiles wide, five long, a ramp of earth and stone edged with
    kerb timbers, crossbearers laid across it every third of a tile, green where the water reaches."""
    bm = bmesh.new()
    top, bottom = QUAY - 0.1, -0.5

    def z(y):
        return top + (bottom - top) * (-(y - 0.5) / 5.0)
    A.hull(bm, [Vector((x, y, zz)) for x in (-0.5, 1.5) for y, zz in ((0.5, top - 0.05), (0.5, BED), (-4.5, BED), (-4.5, bottom - 0.05))], MUD)
    for x in (-0.48, 1.48):
        A.hull(bm, [Vector((x + s, y, z(y) + dz)) for s in (-0.06, 0.06) for y in (0.5, -4.5) for dz in (-0.1, 0.1)], TIM)
    y = 0.4
    while y > -4.5:
        zz = z(y)
        A.hull(bm, [Vector((x, yy, z(yy) + dz)) for x in (-0.42, 1.42) for yy in (y, y - 0.16) for dz in (-0.04, 0.05)],
               WEEDM if zz < WEED[1] else TIM)
        y -= 0.34
    w = Walk(name)
    for dy in range(-4, 1):
        h = z(dy)
        if h > 0.05:
            w.rect(0, dy, 2, dy + 1, h)
    return make(name, bm, MATS, w)


def float_barrel(bm, a, b, r):
    """A barrel lying along a..b as a float: bellied, an iron hoop at each end."""
    a, b = Vector(a), Vector(b)
    pts = [(a.lerp(b, t), r * (0.86 + 0.14 * math.sin(math.pi * t)), r * (0.86 + 0.14 * math.sin(math.pi * t))) for t in (0, 0.25, 0.5, 0.75, 1.0)]
    A.loft(bm, pts, sides=8, ref=(0, 0, 1), mats=OLD)
    for t in (0.12, 0.88):
        p = a.lerp(b, t)
        d = (b - a).normalized() * 0.03
        rr = r * (0.86 + 0.14 * math.sin(math.pi * t)) + 0.012
        A.loft(bm, [(p - d, rr, rr), (p + d, rr, rr)], sides=8, ref=(0, 0, 1), mats=IRON)


def pontoon(name="Pontoon"):
    """A floating landing two tiles by four, low on the water: a deck framed on a raft of lashed
    barrels, a kerb round three sides, iron cleats on its corners."""
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    z = 0.24
    for x in (-0.22, 1.22):
        for k in range(4):
            float_barrel(bm, (x, k - 0.42, 0.0), (x, k + 0.42, 0.0), 0.24)
    for x in (-0.4, 0.5, 1.4):
        beam(bm, (x, -0.5, z - 0.1), (x, 3.5, z - 0.1), 0.1, 0.1, mat=TIM)
    deck(bm, -0.54, 1.54, -0.5, 3.5, z, BOARD, OLD, rnd, C.WEAR)
    for x in (-0.47, 1.47):
        kerb(bm, x, -0.5, 3.5, z, rnd)
    beam(bm, (-0.5, 3.47, z + 0.035), (1.5, 3.47, z + 0.035), 0.07, 0.1, mat=TIM)
    for x, y in ((-0.35, 3.3), (1.35, 3.3), (-0.35, -0.3), (1.35, -0.3)):
        A.box(bm, (x - 0.12, y - 0.03, z), (x + 0.12, y + 0.03, z + 0.05), IRON)
        A.box(bm, (x - 0.03, y - 0.03, z), (x + 0.03, y + 0.03, z + 0.03), IRON)
    w = Walk(name)
    w.rect(0, 0, 2, 4, z)
    return make(name, bm, MATS, w)


def gangplank(rise=1.1, length=3, name="Gangplank"):
    """A gangplank along +Y from a quay or jetty up onto a deck `rise` higher: two planks side by side
    with cleats nailed across them, a post at each corner and a rope rail between them."""
    bm = bmesh.new()
    z0, z1 = QUAY, QUAY + rise
    L = length - 0.15

    def at(t, x=0.0, dz=0.0):
        return Vector((x, -0.5 + L * t, z0 + rise * t + dz))
    for x0, x1 in ((-0.34, -0.01), (0.01, 0.34)):
        A.hull(bm, [at(t, x, dz) for x in (x0, x1) for t in (0.0, 1.0) for dz in (0.0, -0.07)], BOARD)
    for k in range(1, 10):
        t = k / 10
        A.box(bm, at(t, -0.32) - Vector((0, 0.025, 0)), at(t, 0.32) + Vector((0, 0.025, 0.035)), TIM)
    for x in (-0.4, 0.4):
        for t in (0.04, 0.96):
            beam(bm, at(t, x, -0.05), at(t, x, 0.85), 0.05, 0.05, mat=TIM)
        A.loft(bm, [(at(0.04, x, 0.8), 0.02, 0.02), (at(0.5, x, 0.68), 0.02, 0.02), (at(0.96, x, 0.8), 0.02, 0.02)],
               sides=4, ref=(1, 0, 0), mats=ROPE)
    w = Walk(name)
    for dy in range(length):
        w.tile(0, dy, z0 + rise * (dy + 0.5) / length)
    w.wall(0, 0, 0, length)
    w.wall(1, 0, 1, length)
    return make(name, bm, MATS, w)


# ---- dressing -----------------------------------------------------------------------------------

def bollard(name="Bollard"):
    bm = bmesh.new()
    bollard_at(bm, 0, 0, -0.02, IRON, scale=1.1)
    return make(name, bm, MATS)


def capstan(name="Capstan"):
    """A capstan for warping ships in: a waisted drum with whelps (ribs) on a round stone base, a
    ring of bar holes in its head, two bars shipped. Worn: one bar gone, a whelp split off."""
    bm = bmesh.new()
    A.prism(bm, 10, 0.5, 0.45, -0.05, 0.14, mat_index=STONE)
    for z0, z1, r0, r1 in ((0.14, 0.48, 0.3, 0.23), (0.48, 0.74, 0.23, 0.3)):
        A.prism(bm, 10, r0, r1, z0, z1, mat_index=TIM)
    for k in range(6):
        if C.WEAR and k == 2:
            continue
        a = 2 * math.pi * k / 6
        d = Vector((math.cos(a), math.sin(a), 0))
        A.hull(bm, [d * r + Vector((0, 0, z)) + Vector((-d.y, d.x, 0)) * s for r, z in ((0.25, 0.16), (0.32, 0.16), (0.2, 0.48), (0.26, 0.48),
                                                                                       (0.25, 0.72), (0.32, 0.72)) for s in (-0.03, 0.03)], OLD)
    A.prism(bm, 10, 0.36, 0.36, 0.74, 0.9, mat_index=TIM)
    for k in range(1 if C.WEAR else 2):
        a = math.pi * k / 2 + 0.3
        d = Vector((math.cos(a), math.sin(a), 0))
        beam(bm, d * -1.0 + Vector((0, 0, 0.83)), d * 1.0 + Vector((0, 0, 0.83)), 0.07, 0.07, mat=GREY)
    return make(name, bm, MATS)


def crane(name="Crane"):
    """A quay crane: a post on a stone base, a raking jib braced to it, a winch drum with cranks on
    the post, the rope run up over the jib's head to a hook over the water (-Y)."""
    bm = bmesh.new()
    A.box(bm, (-0.48, -0.48, -0.05), (0.48, 0.48, 0.3), STONE)
    beam(bm, (0, 0, 0.25), (0, 0, 3.3), 0.26, 0.26, mat=TIM)
    beam(bm, (0, 0.05, 1.3), (0, -2.4, 4.1), 0.2, 0.2, mat=TIM)
    beam(bm, (0, 0, 3.0), (0, -1.5, 3.42), 0.13, 0.13, mat=OLD)
    for s in (-1, 1):
        beam(bm, (s * 0.35, 0.35, 0.3), (0, 0.08, 1.6), 0.1, 0.1, mat=OLD)
    A.loft(bm, [(Vector((-0.35, -0.22, 1.1)), 0.13, 0.13), (Vector((0.35, -0.22, 1.1)), 0.13, 0.13)], sides=8, ref=(0, 0, 1), mats=ROPE)
    for s in (-1, 1):
        beam(bm, (s * 0.4, -0.22, 1.1), (s * 0.4, -0.45, 1.3), 0.04, 0.04, mat=IRON)
    N.rod(bm, [(0, -0.3, 1.2), (0, -2.35, 4.05)], 0.018, mat=ROPE, sides=3)
    N.rod(bm, [(0, -2.35, 4.0), (0, -2.35, 1.7)], 0.018, mat=ROPE, sides=3)
    A.prism(bm, 8, 0.1, 0.1, 3.95, 4.08, (0, -2.35), mat_index=IRON, cap_bottom=True)
    A.hull(bm, [Vector((x, -2.35 + y, z)) for x in (-0.03, 0.03) for y, z in ((0, 1.7), (0, 1.55), (0.12, 1.45), (0.15, 1.55))], IRON)
    return make(name, bm, MATS)


def net_rack(name="NetRack"):
    """Nets hung to dry along X: two forked posts, a pole between them, the net in a sagging dark
    sheet, cork floats along its foot. Worn: a tear through the net, floats lost."""
    rnd = random.Random(8)
    bm = bmesh.new()
    for x in (-0.95, 0.95):
        pile(bm, x, 0, 1.78, rnd, r=0.05, mat=GREY, lean=0.0)
        for s in (-1, 1):
            N.branch(bm, (x, 0, 1.62), (x, s * 0.1, 1.86), 0.025, None, mat=GREY, sides=4)
    N.rod(bm, [(-1.05, 0, 1.75), (1.05, 0, 1.76)], 0.035, mat=GREY, sides=5)
    cols = 8
    top = [bm.verts.new((-0.88 + 1.76 * k / cols, 0.01, 1.7)) for k in range(cols + 1)]
    mid = [bm.verts.new((-0.88 + 1.76 * k / cols, 0.05, 1.0 - 0.08 * math.sin(math.pi * k / cols))) for k in range(cols + 1)]
    bot = [bm.verts.new((-0.86 + 1.72 * k / cols, 0.09, 0.4 + 0.12 * math.sin(k * 1.7) ** 2)) for k in range(cols + 1)]
    for r_i, (row_a, row_b) in enumerate(((mid, top), (bot, mid))):
        for k in range(cols):
            if C.WEAR and r_i == 1 and k in (5, 6):
                continue                                              # the tear
            bm.faces.new((row_a[k], row_a[k + 1], row_b[k + 1], row_b[k])).material_index = OLD
    for k in range(0, cols + 1, 2):
        if C.WEAR and k in (4, 6):
            continue
        A.loft(bm, [(bot[k].co + Vector((0, 0, -0.05)), 0.055, 0.04), (bot[k].co + Vector((0, 0, 0.05)), 0.055, 0.04)],
               sides=6, ref=(1, 0, 0), mats=BOARD)
    return make(name, bm, MATS)


def harbour_lamp(name="HarbourLamp"):
    """A lamp at a jetty's end or a quay's corner: a squared post with a chamfered head, an iron
    bracket and a lantern hanging from it."""
    bm = bmesh.new()
    beam(bm, (0, 0, -0.05), (0, 0, 2.35), 0.15, 0.15, mat=TIM)
    A.hull(bm, [Vector((x, y, 2.35)) for x in (-0.09, 0.09) for y in (-0.09, 0.09)] + [Vector((0, 0, 2.48))], TIM)
    beam(bm, (0, 0, 2.2), (0, -0.58, 2.2), 0.05, 0.04, mat=IRON)
    beam(bm, (0, 0, 1.9), (0, -0.4, 2.2), 0.03, 0.03, mat=IRON)
    N.rod(bm, [(0, -0.52, 2.18), (0, -0.52, 2.02)], 0.012, mat=IRON, sides=3)
    A.prism(bm, 6, 0.1, 0.12, 1.74, 1.98, (0, -0.52), mat_index=len(MATS))      # the glass, lit
    A.prism(bm, 6, 0.13, 0.02, 1.98, 2.06, (0, -0.52), mat_index=IRON)
    A.prism(bm, 6, 0.12, 0.12, 1.7, 1.74, (0, -0.52), mat_index=IRON, cap_bottom=True)
    return make(name, bm, MATS + ["CandleWax"])


def channel_marker(name="ChannelMarker"):
    """A post in the water marking the channel: a tall pile, weed at its foot, a slatted barrel cage
    on top for the mark."""
    rnd = random.Random(3)
    bm = bmesh.new()
    pile(bm, 0, 0, 2.2, rnd, r=0.14, mat=TIM, weed=WEEDM, lean=0.0)
    for z in (1.95, 2.45):
        A.prism(bm, 8, 0.24, 0.24, z, z + 0.05, mat_index=IRON, cap_bottom=True)
    for k in range(8):
        a = math.pi * k / 4
        beam(bm, (0.23 * math.cos(a), 0.23 * math.sin(a), 1.97), (0.23 * math.cos(a), 0.23 * math.sin(a), 2.48), 0.04, 0.05, mat=GREY)
    return make(name, bm, MATS)


def fish_crates(name="FishCrates"):
    """Fish crates stacked on the quay: shallow slatted boxes, three high and one beside, a coil of
    rope against them. Worn: a slat gone, the one beside knocked over on its side."""
    bm = bmesh.new()
    for k, (x, y, z) in enumerate(((0, 0, 0), (0.03, 0.02, 0.24), (-0.02, -0.01, 0.48), (0.62, -0.08, 0))):
        if C.WEAR and k == 3:
            A.box(bm, (x - 0.27, y - 0.2, 0), (x + 0.27, y + 0.02, 0.4), GREY)
            for xx in (-0.15, 0.15):
                A.box(bm, (x + xx - 0.02, y - 0.21, 0), (x + xx + 0.02, y + 0.03, 0.41), BOARD)
            continue
        A.box(bm, (x - 0.27, y - 0.2, z), (x + 0.27, y + 0.2, z + 0.22), GREY)
        for j, zz in enumerate((0.07, 0.15)):
            if C.WEAR and k == 1 and j == 1:
                continue
            A.box(bm, (x - 0.28, y - 0.21, z + zz - 0.02), (x + 0.28, y + 0.21, z + zz + 0.02), BOARD)
    rope_coil(bm, (0.25, -0.5, 0), 0.24, ROPE)
    return make(name, bm, MATS)


def lobster_pots(name="LobsterPots"):
    """Wicker pots for crabs and eels, stacked by a jetty's foot: domed baskets with a dark mouth on
    top, three on the ground and one on them. Worn: one crushed flat and split."""
    bm = bmesh.new()
    for k, (x, y, z) in enumerate(((-0.3, 0, 0), (0.3, 0.05, 0), (0.0, -0.45, 0), (0.0, -0.18, 0.38))):
        h = 0.36
        if C.WEAR and k == 2:
            h = 0.18
            A.hull(bm, [Vector((x + 0.27 * math.cos(a), y + 0.27 * math.sin(a), z + (0.0 if j == 0 else 0.14 + 0.03 * math.sin(3 * a))))
                        for a in (2 * math.pi * i / 8 for i in range(8)) for j in (0, 1)], GREY)
            continue
        A.prism(bm, 8, 0.25, 0.25, z, z + 0.12, (x, y), mat_index=GREY, cap_bottom=True)
        A.prism(bm, 8, 0.25, 0.12, z + 0.12, z + h, (x, y), mat_index=GREY)
        A.prism(bm, 8, 0.12, 0.11, z + h, z + h + 0.01, (x, y), mat_index=OLD)
        for zz in (0.06, 0.2):
            rr = 0.255 if zz < 0.12 else 0.2
            A.prism(bm, 8, rr, rr, z + zz, z + zz + 0.025, (x, y), mat_index=OLD, cap_top=False)
    return make(name, bm, MATS)


# ---- stone jetties ------------------------------------------------------------------------------
#
# Higher and sturdier than timber: the deck at the quay's height, solid masonry from the bed with
# battered sides, a weed band, a coping of pale blocks along each edge, paving between them, and
# buttresses standing out of the sides every other tile. They run on from a quay along +Y.

PAVE = len(MATS)                    # the paving's material slot (Flagstone: the flags texture)
STONE_MATS = MATS + ["Flagstone", "CandleWax"]
GLOW = PAVE + 1


def coping_run(bm, x, y0, y1, side, rnd=None):
    """Coping blocks along an edge at x from y0 to y1, overhanging on `side` (-1 west, +1 east). Worn:
    the odd block chipped at a corner, the chip lying on the paving beside it, and now and then a
    block gone, left lying askew where it was pushed."""
    y = y0
    while y < y1 - 0.05:
        b = min(y + 1.0, y1)
        pts = [Vector((xx, yy, QUAY - 0.13)) for xx in (x - 0.22 * side, x + 0.1 * side) for yy in (y + 0.012, b - 0.012)] + \
              [Vector((xx, yy, QUAY)) for xx in (x - 0.22 * side, x + 0.08 * side) for yy in (y + 0.03, b - 0.03)]
        roll = rnd.random() if (C.WEAR and rnd is not None) else 1.0
        if roll < 0.07 and y > y0:
            c = Vector((x - side * 0.55, (y + b) / 2, QUAY - 0.02))
            a = rnd.uniform(0.3, 0.6)
            ca, sa = math.cos(a), math.sin(a)
            loose = [Vector((c.x + dx * ca - dy * sa, c.y + dx * sa + dy * ca, c.z + dz))
                     for dx in (-0.14, 0.14) for dy in (-0.42, 0.42) for dz in (0.0, 0.13)]
            A.hull(bm, loose, COPE)
        else:
            if roll < 0.3:
                chip_off(pts, Vector((side, rnd.choice((-1, 1)), 0)), rnd)
                loose_stone(bm, (x - side * rnd.uniform(0.35, 0.5), rnd.uniform(y + 0.2, b - 0.2), QUAY - 0.02), 0.06, rnd, COPE)
            A.hull(bm, pts, COPE)
        y = b


def buttress(bm, x, y, side, w=0.5, out=0.35):
    """A buttress standing out of the pier's side at (x, y), battered like the wall, capped, green at
    the waterline."""
    A.hull(bm, [Vector((x + side * o, y + s * w / 2, z)) for s in (-1, 1) for o, z in ((0.0, BED), (out + 0.15, BED), (0.0, QUAY - 0.2), (out, QUAY - 0.2))], STONE)
    A.hull(bm, [Vector((x + side * o, y + s * (w / 2 + 0.04), z)) for s in (-1, 1)
                for o, z in ((-0.02, QUAY - 0.2), (out + 0.05, QUAY - 0.2), (-0.02, QUAY - 0.13), (out - 0.02, QUAY - 0.08))], COPE)
    A.hull(bm, [Vector((x + side * o, y + s * (w / 2 + 0.01), z)) for s in (-1, 1)
                for o, z in ((-0.01, WEED[0]), (out + 0.14, WEED[0]), (-0.01, WEED[1]), (out + 0.13, WEED[1]))], WEEDM)


def stone_pier(width=2, length=4, name=None):
    """A solid stone pier along +Y, `width` tiles wide: masonry from the bed with battered sides and a
    weed band, a coping of pale blocks along each edge, paving between, a buttress out of each side
    every other tile with an iron bollard over one of them, rings on the sides. Walkable at the
    quay's height. Worn: chipped and missing coping, weeds in the paving's cracks, stones fallen at
    the foot."""
    name = name or "StonePier%dx%d" % (width, length)
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    x0, x1 = -0.5, width - 0.5
    y0, y1 = -0.5, length - 0.5
    mid = (x0 + x1) / 2
    for side, x in ((-1, x0), (1, x1)):
        A.hull(bm, [Vector((xx, y, z)) for y in (y0, y1) for xx, z in ((x + side * 0.3, BED), (x, QUAY - 0.13), (mid, QUAY - 0.13), (mid, BED))], STONE)
        bat = lambda z: 0.3 * (1 - (z - BED) / (QUAY - 0.13 - BED)) + 0.012
        A.hull(bm, [Vector((x + side * bat(z), y, z)) for y in (y0, y1) for z in WEED] +
               [Vector((x + side * (bat(z) - 0.05), y, z)) for y in (y0, y1) for z in WEED], WEEDM)
        coping_run(bm, x, y0, y1, side, rnd)
        if C.WEAR:
            fallen(bm, x, y0, y1, side, rnd, n=max(1, length // 2))
    A.box(bm, (x0 + 0.2, y0, QUAY - 0.1), (x1 - 0.2, y1, QUAY - 0.02), PAVE)
    if C.WEAR:
        cracks(bm, x0 + 0.25, x1 - 0.25, y0 + 0.1, y1 - 0.1, QUAY - 0.017, rnd, n=length)
    for k in range(1, length, 2):
        for side, x in ((-1, x0), (1, x1)):
            buttress(bm, x, k - 0.5, side)
        bollard_at(bm, x0 + 0.1 if k % 4 == 1 else x1 - 0.1, k - 0.5, QUAY, IRON)
    for k in range(0, length, 2):
        for side, x in ((-1, x0), (1, x1)):
            ring_on(bm, (x + side * 0.1, k, QUAY - 0.42), (side, 0, 0), 0.09, IRON)
    w = Walk(name)
    w.rect(0, 0, width, length, QUAY)
    for k in range(1, length, 2):
        w.drop((0 if k % 4 == 1 else width - 1, k))
    return make(name, bm, STONE_MATS, w)


def stone_arch(width=2, name="StoneArch"):
    """Three tiles of stone pier along +Y carried on an arch, so water and small boats pass under it:
    solid ends, a round arch between them with a ring of pale voussoirs on each face, the same coping
    and paving as the solid pier on top. Walkable at the quay's height. Worn: as the solid pier."""
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    x0, x1 = -0.5, width - 0.5
    y0, y1 = -0.5, 2.5
    a0, a1, spring = 0.3, 1.7, 0.0
    crown = QUAY - 0.4
    n = 12
    arch = [(a0 + (a1 - a0) * (1 - math.cos(math.pi * k / n)) / 2, spring + (crown - spring) * math.sin(math.pi * k / n)) for k in range(n + 1)]
    outline = [(y0, BED), (y0, QUAY - 0.13), (y1, QUAY - 0.13), (y1, BED), (a1, BED)] + list(reversed(arch)) + [(a0, BED)]
    faces = [[bm.verts.new((x, y, z)) for y, z in outline] for x in (x0, x1)]
    bm.faces.new(faces[0]).material_index = STONE
    bm.faces.new(list(reversed(faces[1]))).material_index = STONE
    m = len(outline)
    for k in range(m):
        j = (k + 1) % m
        bm.faces.new((faces[0][k], faces[0][j], faces[1][j], faces[1][k])).material_index = STONE
    cy = (a0 + a1) / 2
    for x, side in ((x0, -1), (x1, 1)):
        ring = []
        for y, z in arch:                                    # the voussoirs: a band round the arch
            d = Vector((0, y - cy, z - spring)).normalized()
            ring.append(Vector((x + side * 0.02, y, z)) + d * 0.1)
        A.loft(bm, [(p, 0.03, 0.1) for p in ring], sides=4, ref=(1, 0, 0), mats=COPE)
        A.box(bm, (x - 0.012, y0, WEED[0]), (x + 0.012, a0 - 0.02, WEED[1]), WEEDM)
        A.box(bm, (x - 0.012, a1 + 0.02, WEED[0]), (x + 0.012, y1, WEED[1]), WEEDM)
        coping_run(bm, x, y0, y1, side, rnd)
    A.box(bm, (x0 + 0.2, y0, QUAY - 0.1), (x1 - 0.2, y1, QUAY - 0.02), PAVE)
    if C.WEAR:
        cracks(bm, x0 + 0.25, x1 - 0.25, y0 + 0.1, y1 - 0.1, QUAY - 0.017, rnd, n=3)
    w = Walk(name)
    w.rect(0, 0, width, 3, QUAY)
    return make(name, bm, STONE_MATS, w)


def stone_head(width=4, neck=2, name=None):
    """A pier's head, `width` tiles wide, its far end rounded: battered masonry with a weed band, a
    coping round its edge, paving. Its back joins a pier `neck` tiles wide, centred; either side of
    the neck the back is finished like any other face (battered, coped), so a 4-wide head makes a T
    on a 2-wide pier. A 4-wide head has a stone plinth with a lamp and bollards round its nose; a
    2-wide one a bollard at its tip. Walkable at the quay's height but for the plinth and bollards."""
    name = name or ("StonePierHead%d" % width if width != 4 else "StonePierHead")
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    r = width / 2
    cx, cy = (width - 1) / 2, r - 0.5
    n0, n1 = cx - neck / 2, cx + neck / 2
    arc = [(cx + r * math.cos(a), cy + r * math.sin(a)) for a in (math.pi * k / 8 for k in range(1, 8))]
    outline = ([(n0, -0.5), (n1, -0.5)] + ([(width - 0.5, -0.5)] if n1 < width - 0.5 - 1e-6 else []) +
               [(width - 0.5, cy)] + arc + [(-0.5, cy)] + ([(-0.5, -0.5)] if n0 > -0.5 + 1e-6 else []))
    push = []
    for x, y in outline:
        if y < -0.45 and n0 - 1e-6 <= x <= n1 + 1e-6:
            push.append((0.0, 0.0))                                   # where the pier joins
        elif y < -0.45:
            push.append((1.0 if x > cx else -1.0, -1.0))
        elif y <= cy + 1e-6:
            push.append((1.0 if x > cx else -1.0, 0.0))
        else:
            d = Vector((x - cx, y - cy, 0)).normalized()
            push.append((d.x, d.y))
    coped = [k for k in range(len(outline)) if k != 0]               # every edge but the neck's
    outline_solid(bm, outline, push, rnd, coped)
    if C.WEAR:
        cracks(bm, -0.2, width - 0.8, -0.3, cy + r * 0.5, QUAY - 0.017, rnd, n=width)
    if width >= 4:
        A.box(bm, (cx - 0.45, cy - 0.45, QUAY - 0.13), (cx + 0.45, cy + 0.45, QUAY + 0.35), STONE)
        top = [Vector((x, y, z)) for x in (cx - 0.52, cx + 0.52) for y in (cy - 0.52, cy + 0.52) for z in (QUAY + 0.35, QUAY + 0.45)]
        if C.WEAR:
            chip_off(top, Vector((1, 1, 0)), rnd)
        A.hull(bm, top, COPE)
        A.prism(bm, 8, 0.12, 0.09, QUAY + 0.45, QUAY + 2.2, (cx, cy), mat_index=IRON)
        A.prism(bm, 6, 0.2, 0.24, QUAY + 2.2, QUAY + 2.65, (cx, cy), mat_index=GLOW)
        A.prism(bm, 6, 0.28, 0.03, QUAY + 2.65, QUAY + 2.82, (cx, cy), mat_index=IRON)
        bollards = [(cx + (r - 0.3) * math.cos(a), cy + (r - 0.3) * math.sin(a)) for a in (math.pi * 0.2, math.pi * 0.5, math.pi * 0.8)]
    else:
        bollards = [(cx, cy + r - 0.35)]
    for bx, by in bollards:
        bollard_at(bm, bx, by, QUAY, IRON)
    w = Walk(name)
    for dx in range(width):
        for dy in range(width):
            if dy <= cy or math.hypot(dx - cx, dy - cy) < r - 0.25:
                w.tile(dx, dy, QUAY)
    if width >= 4:
        w.drop((round(cx - 0.5), round(cy - 0.5)), (round(cx + 0.5), round(cy - 0.5)), (round(cx - 0.5), round(cy + 0.5)), (round(cx + 0.5), round(cy + 0.5)))
    for bx, by in bollards:
        w.drop((round(bx), round(by)))
    return make(name, bm, STONE_MATS, w)


def stone_steps(name="StoneSteps"):
    """Stairs down the side of a stone pier to the water, three tiles along +Y against the pier's face
    (to the east, +X): broad stone steps with pale nosings, a low parapet on the water side (-X).
    Worn: the middle nosing chipped, a stone of the parapet's coping gone."""
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    hs = [QUAY - 0.28, QUAY - 0.58, 0.12]
    for k, h in enumerate(hs):
        A.box(bm, (-0.5, k - 0.5, BED), (0.5, k + 0.5, h - 0.06), STONE)
        chip = Vector((-1, -1, 0)) if (C.WEAR and k == 1) else None
        nosing(bm, -0.5, k - 0.52, 0.5, k + 0.5, h, WEEDM if k == 2 else COPE, rnd, chip)
    A.box(bm, (-0.62, -0.5, BED), (-0.5, 2.5, QUAY - 0.1), STONE)
    if C.WEAR:
        A.box(bm, (-0.66, -0.5, QUAY - 0.1), (-0.46, 1.2, QUAY), COPE)
        A.box(bm, (-0.66, 1.7, QUAY - 0.1), (-0.46, 2.5, QUAY), COPE)
        loose_stone(bm, (-0.95, 1.4, -0.25), 0.14, rnd, COPE)
    else:
        A.box(bm, (-0.66, -0.5, QUAY - 0.1), (-0.46, 2.5, QUAY), COPE)
    A.box(bm, (-0.67, -0.5, WEED[0]), (-0.62, 2.5, WEED[1]), WEEDM)
    w = Walk(name)
    for k, h in enumerate(hs):
        w.tile(0, k, h)
    w.wall(0, 0, 0, 3)
    return make(name, bm, STONE_MATS, w)


# ---- worn and broken timber ---------------------------------------------------------------------

def snapped(bm, x, y, top, rnd, r=0.115, mat=TIM):
    """A pile snapped off at `top`: its stump, splinters standing up from the break."""
    pile(bm, x, y, top, rnd, r=r, mat=mat, weed=WEEDM)
    for k in range(4):
        a = 2 * math.pi * k / 4 + rnd.uniform(-0.3, 0.3)
        b = Vector((x + math.cos(a) * r * 0.5, y + math.sin(a) * r * 0.5, top - 0.02))
        A.spike(bm, b, r * 0.45, b + Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, rnd.uniform(0.08, 0.2))), sides=3, mat=OLD)


def jetty_worn(length=3, name="JettySpanWorn"):
    """A timber jetty gone grey with age: the deck sagging between its piles, boards weathered, split
    and a few missing, one pile leaning hard, weed climbing high. Still walkable."""
    rnd = random.Random(41)
    bm = bmesh.new()
    xs = [-0.38, 0.38]

    def sag(y):
        return 0.07 * math.sin(math.pi * ((y + 0.5) % 1.0))
    for k in range(length + 1):
        y = k - 0.5
        for x in xs:
            pile(bm, x, y, JETTY - 0.04, rnd, r=0.11, mat=OLD, weed=WEEDM, lean=0.1 if k == 2 and x > 0 else 0.03)
        beam(bm, (xs[0] - 0.12, y, JETTY - 0.24), (xs[-1] + 0.12, y + rnd.uniform(-0.03, 0.03), JETTY - 0.27), 0.13, 0.13, mat=OLD)
        if k % 2 == 0:
            for a, b in ((xs[0], xs[-1]), (xs[-1], xs[0])):
                beam(bm, (a, y + 0.13, JETTY - 0.3), (b, y + 0.13, WEED[1] + 0.05), 0.07, 0.05, mat=OLD)
    for x in xs:
        A.loft(bm, [(Vector((x, y, JETTY - 0.12 - sag(y))), 0.1, 0.1) for y in [-0.5 + 0.25 * j for j in range(length * 4 + 1)]],
               sides=4, ref=(0, 0, 1), mats=OLD)
    y = -0.5
    while y < length - 0.55:
        w = min(rnd.uniform(0.16, 0.24), length - 0.5 - y)
        if rnd.random() > 0.12:
            z = JETTY - sag(y + w / 2)
            a, b = -0.56 + rnd.uniform(-0.05, 0.05), 0.56 + rnd.uniform(-0.06, 0.04)
            if rnd.random() < 0.15:
                b = rnd.uniform(0.1, 0.35)                   # a board split short
            A.box(bm, (a, y + 0.01, z - 0.06), (b, y + w - 0.012, z + rnd.uniform(-0.012, 0.006)), rnd.choice((GREY, GREY, OLD)))
        y += w
    walk = Walk(name)
    walk.rect(0, 0, 1, length, JETTY - 0.04)
    return make(name, bm, MATS, walk)


def jetty_broken(name="JettySpanBroken"):
    """Three tiles of jetty that has given way: the first sound, then a hole where the boards have
    gone over a snapped joist and a pile snapped below the deck, and beyond it the deck sliding down
    into the water. Walkable only on its first tile."""
    rnd = random.Random(43)
    bm = bmesh.new()
    xs = [-0.38, 0.38]
    for x in xs:
        pile(bm, x, -0.5, JETTY - 0.04, rnd, r=0.115, mat=TIM, weed=WEEDM)
        pile(bm, x, 0.5, JETTY - 0.04, rnd, r=0.115, mat=OLD, weed=WEEDM)
    snapped(bm, xs[0], 1.5, 0.05, rnd)
    pile(bm, xs[1], 1.5, JETTY - 0.18, rnd, r=0.11, mat=OLD, weed=WEEDM, lean=0.12)
    snapped(bm, xs[0], 2.5, -0.05, rnd)
    snapped(bm, xs[1], 2.5, 0.2, rnd)
    for y in (-0.5, 0.5):
        beam(bm, (-0.5, y, JETTY - 0.24), (0.5, y, JETTY - 0.24), 0.13, 0.13, mat=OLD)
    for x in xs:
        beam(bm, (x, -0.5, JETTY - 0.12), (x, 0.55, JETTY - 0.12), 0.1, 0.1, mat=OLD)
    beam(bm, (xs[0], 0.55, JETTY - 0.12), (xs[0] + 0.02, 1.15, JETTY - 0.45), 0.1, 0.1, mat=OLD)       # the snapped joist
    beam(bm, (xs[1], 0.55, JETTY - 0.12), (xs[1], 1.5, JETTY - 0.3), 0.1, 0.1, mat=OLD)
    deck_boards(bm, -0.5, -0.5, 0.5, 0.5, JETTY, rnd, mats=(BOARD, GREY, OLD))
    for k in range(3):                                       # boards left hanging over the hole
        y = 0.55 + 0.2 * k
        A.hull(bm, [Vector((x, y + dy, z)) for x, z in ((-0.56, JETTY - 0.02 - 0.12 * k), (0.2 - 0.25 * k, JETTY - 0.12 - 0.15 * k))
                    for dy in (0.0, 0.18)] + [Vector((-0.56, y, JETTY - 0.08 - 0.12 * k))], GREY)
    tilt0, tilt1 = JETTY - 0.3, -0.25                        # the deck beyond, sliding into the water
    y = 1.6
    while y < 2.9:
        z = tilt0 + (tilt1 - tilt0) * (y - 1.6) / 1.3
        A.box(bm, (-0.52 + rnd.uniform(-0.05, 0.05), y, z - 0.06), (0.5 + rnd.uniform(-0.05, 0.05), y + 0.18, z), rnd.choice((GREY, OLD)))
        y += 0.2
    for x in xs:
        beam(bm, (x, 1.55, tilt0 - 0.1), (x, 2.9, tilt1 - 0.1), 0.1, 0.1, mat=OLD)
    w = Walk(name)
    w.tile(0, 0, JETTY)
    w.wall(0, 1, 1, 1)                                       # the edge of the hole
    return make(name, bm, MATS, w)


def pile_stubs(name="PileStubs"):
    """The stumps of an old jetty standing in the water in two rows, rotted off at different heights,
    green to above the waterline, a fallen timber across two of them. Scenery."""
    rnd = random.Random(47)
    bm = bmesh.new()
    for k in range(5):
        for x in (-0.4, 0.4):
            if rnd.random() < 0.25:
                continue
            snapped(bm, x + rnd.uniform(-0.05, 0.05), k + rnd.uniform(-0.08, 0.08), rnd.uniform(0.05, 0.55), rnd, r=rnd.uniform(0.1, 0.13), mat=OLD)
    beam(bm, (-0.4, 2.0, 0.35), (0.35, 3.0, -0.1), 0.1, 0.1, mat=GREY)
    return make(name, bm, MATS)


def stone_splay(neck=2, width=4, length=2, name=None):
    """Where a pier widens: `length` tiles along +Y over which its sides splay out from `neck` tiles
    wide to `width`, centred, battered and coped like the rest; the paving runs through. Joins a
    narrow pier behind and a wide pier or head ahead."""
    name = name or "StoneSplay%dto%d" % (neck, width)
    rnd = C.wear_rnd(name)
    bm = bmesh.new()
    o = (width - neck) / 2
    y0, y1 = -0.5, length - 0.5
    outline = [(-0.5, y0), (neck - 0.5, y0), (neck - 0.5 + o, y1), (-0.5 - o, y1)]
    push = [(-1.0, 0.0), (1.0, 0.0), (1.0, 0.0), (-1.0, 0.0)]      # out sideways, as the piers either end
    outline_solid(bm, outline, push, rnd, coped=[1, 3])
    if C.WEAR:
        cracks(bm, -0.2, neck - 0.8, y0 + 0.1, y1 - 0.1, QUAY - 0.017, rnd, n=2)
    w = Walk(name)
    for dy in range(length):
        half = neck / 2 + o * (dy + 0.5) / length
        for dx in range(-math.ceil(o), neck + math.ceil(o)):
            if abs(dx - (neck - 1) / 2) + 0.5 <= half + 0.05:
                w.tile(dx, dy, QUAY)
    return make(name, bm, STONE_MATS, w)


def outline_solid(bm, outline, push, rnd, coped=None, lamp_at=None):
    """Battered masonry under an outline of (x, y) points: each point pushed out at the bed along its
    `push` vector (zero where it meets another piece), a weed band and a coping along the edges in
    `coped` (indices of the outline's edges, i to i+1), paving on top."""
    def batter(z):
        return 0.3 * (1 - (z - BED) / (QUAY - 0.13 - BED))

    def ring(z, extra=0.0):
        return [bm.verts.new((x + px * (batter(z) + extra * (abs(px) + abs(py) > 0)), y + py * (batter(z) + extra * (abs(px) + abs(py) > 0)), z))
                for (x, y), (px, py) in zip(outline, push)]
    lo, hi = ring(BED), ring(QUAY - 0.13)
    m = len(outline)
    for k in range(m):
        j = (k + 1) % m
        bm.faces.new((lo[k], lo[j], hi[j], hi[k])).material_index = STONE
    bm.faces.new(list(reversed(lo))).material_index = STONE
    cap = [bm.verts.new((x, y, QUAY - 0.02)) for x, y in outline]    # the paving, as high as the piers'
    for k in range(m):
        j = (k + 1) % m
        if any(push[i] != (0.0, 0.0) for i in (k, j)):                  # not where it meets another piece
            bm.faces.new((hi[k], hi[j], cap[j], cap[k])).material_index = STONE
    bm.faces.new(cap).material_index = PAVE
    coped = range(m) if coped is None else coped
    wl, wh = ring(WEED[0], 0.012), ring(WEED[1], 0.012)
    for k in coped:
        j = (k + 1) % m
        bm.faces.new((wl[k], wl[j], wh[j], wh[k])).material_index = WEEDM
    runs, cur = [], []
    for k in range(m):
        if k in coped:
            if not cur:
                cur = [k]
            cur.append((k + 1) % m)
        elif cur:
            runs.append(cur)
            cur = []
    if cur:
        if runs and runs[0][0] == cur[-1]:
            runs[0] = cur[:-1] + runs[0]
        else:
            runs.append(cur)
    def normal(k):
        """The outward normal of edge k (the outline runs anticlockwise)."""
        (ax, ay), (bx, by) = outline[k], outline[(k + 1) % m]
        return Vector((by - ay, -(bx - ax), 0)).normalized()

    for run in runs:
        # lay the coping as coping_run does: from 0.22 inside the face to 0.1 outside it, so it lines
        # up with the piers this piece joins; at each point the normals of its coped edges are mitred
        pts = []
        for idx, i in enumerate(run):
            edges = [e for e in ((i - 1) % m, i) if e in coped and (idx > 0 or e == i) and (idx < len(run) - 1 or e != i)]
            n = sum((normal(e) for e in edges), Vector()) or normal(i)
            n.normalize()
            k = 1.0 / max(n.dot(normal(edges[0])), 0.5)
            pts.append((Vector((outline[i][0], outline[i][1], QUAY - 0.065)) + n * (-0.06 * k), 0.16 * k, 0.065))
        A.loft(bm, pts, sides=4, ref=(0, 0, 1), mats=COPE)
        if C.WEAR and rnd is not None:
            for _ in range(len(run) // 3):
                i = rnd.choice(run)
                loose_stone(bm, (outline[i][0] - push[i][0] * 0.35, outline[i][1] - push[i][1] * 0.35, QUAY - 0.02), 0.06, rnd, COPE)


def nosing(bm, x0, y0, x1, y1, z, mat, rnd=None, chip=None):
    """A step's pale nosing slab from (x0, y0) to (x1, y1), its top at z; chip: a direction whose
    corner is knocked off."""
    pts = [Vector((x, y, zz)) for x in (x0, x1) for y in (y0, y1) for zz in (z - 0.06, z)]
    if chip is not None:
        chip_off(pts, chip, rnd)
    A.hull(bm, pts, mat)


def fallen(bm, x, y0, y1, side, rnd, n=2):
    """Stones fallen from the face, lying at its foot half in the water."""
    for _ in range(n):
        loose_stone(bm, (x + side * rnd.uniform(0.45, 0.8), rnd.uniform(y0 + 0.3, y1 - 0.3), rnd.uniform(-0.35, -0.15)),
                    rnd.uniform(0.15, 0.22), rnd, STONE)


def cracks(bm, x0, x1, y0, y1, z, rnd, n=3):
    """Weeds in the cracks of the paving: thin green lines wandering across it."""
    for _ in range(n):
        p = Vector((rnd.uniform(x0, x1), rnd.uniform(y0, y1), z))
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)).normalized()
        for k in range(rnd.randint(3, 5)):
            q = p + d * rnd.uniform(0.12, 0.22)
            q.x, q.y = min(max(q.x, x0), x1), min(max(q.y, y0), y1)
            side = Vector((-d.y, d.x, 0)) * 0.018
            v = [bm.verts.new(c) for c in (p - side, p + side, q + side, q - side)]
            bm.faces.new(v).material_index = WEEDM
            d = (d + Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), 0))).normalized()
            p = q


def loose_stone(bm, at, s, rnd, mat):
    """A broken-off stone lying where it fell, its foot just under it."""
    at = Vector(at)
    pts = [at + Vector((rnd.uniform(-s, s), rnd.uniform(-s, s), -0.01)) for _ in range(4)]
    pts += [at + Vector((rnd.uniform(-s, s) * 0.7, rnd.uniform(-s, s) * 0.7, s * rnd.uniform(0.6, 1.0))) for _ in range(3)]
    A.hull(bm, pts, mat)


def chip_off(pts, which, rnd):
    """Knock a corner off a block given by its points: the top point nearest `which` (a direction)
    drops and draws in."""
    top = max(p.z for p in pts)
    tops = [p for p in pts if p.z > top - 1e-4]
    c = max(tops, key=lambda p: p.dot(which))
    mid = sum(tops, Vector()) / len(tops)
    c.z -= rnd.uniform(0.05, 0.08)
    c += (mid - c).normalized() * rnd.uniform(0.08, 0.14) * Vector((1, 1, 0))
    return pts
