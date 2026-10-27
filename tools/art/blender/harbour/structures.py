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
import basin as N
from common import (BED, JETTY, QUAY, WEED, Walk, beam, bollard_at, deck_boards, make, pile, ring_on, rope_coil)

MATS = ["Timber", "Planks", "Iron", "Driftwood", "Moss", "DeadWood", "Hide", "Stone", "Gravestone", "Mud"]
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


def kerb(bm, x, y0, y1, deck):
    """A kerb along the deck's edge, so the edge reads from the game's camera."""
    beam(bm, (x, y0, deck + 0.035), (x, y1, deck + 0.035), 0.07, 0.1, mat=TIM)


def jetty_span(width=1, length=1, name=None):
    """A straight run of jetty out over the water along +Y, `width` tiles wide: piles in pairs at the
    start of each tile under a headstock, joists along, X-bracing every other pair, boards across
    with a kerb along each side, the deck a half tile above the water. Walkable; its sides are open
    (the water beside it is blocked anyway)."""
    rnd = random.Random(10 + width * 7 + length)
    bm = bmesh.new()
    x0, x1 = -0.5, width - 0.5
    xs = [x0 + 0.12] + ([x0 + width / 2] if width > 1 else []) + [x1 - 0.12]
    frame(bm, rnd, xs, [k - 0.5 for k in range(length + 1)], JETTY)
    deck_boards(bm, x0, -0.5, x1, length - 0.5, JETTY, rnd, mats=(BOARD, BOARD, BOARD, BOARD, OLD))
    for x in (x0 + 0.03, x1 - 0.03):
        kerb(bm, x, -0.5, length - 0.5, JETTY)
    w = Walk(name or "JettySpan%dx%d" % (width, length))
    w.rect(0, 0, width, length, JETTY)
    return make(w.name, bm, MATS, w)


def jetty_head(name="JettyHead"):
    """A jetty's end: a platform three tiles square, a tall mooring post at each corner with rope
    wound on it, a ladder down to the water on its far side, a coil of rope on the boards."""
    rnd = random.Random(2)
    bm = bmesh.new()
    xs = ys = [-0.38, 1.0, 2.38]
    posts = {(-0.38, -0.38), (2.38, -0.38), (-0.38, 2.38), (2.38, 2.38)}
    frame(bm, rnd, xs, ys, JETTY, posts=posts, brace_every=1)
    deck_boards(bm, -0.5, -0.5, 2.5, 2.5, JETTY, rnd, mats=(BOARD, BOARD, BOARD, OLD))
    for x in (-0.47, 2.47):
        kerb(bm, x, -0.5, 2.5, JETTY)
    for x, y in ((-0.38, 2.38), (2.38, 2.38)):                  # rope wound on the far posts
        for k in range(3):
            A.prism(bm, 7, 0.16, 0.16, JETTY + 0.45 + 0.06 * k, JETTY + 0.5 + 0.06 * k, (x, y), mat_index=ROPE)
    for x in (0.78, 1.22):                                       # the ladder: timber stiles, iron rungs
        beam(bm, (x, 2.6, WEED[0] - 0.5), (x, 2.6, JETTY + 0.4), 0.07, 0.07, mat=TIM)
    for z in (-0.45, -0.15, 0.15, 0.42):
        A.loft(bm, [(Vector((0.78, 2.6, z)), 0.02, 0.02), (Vector((1.22, 2.6, z)), 0.02, 0.02)], sides=4, ref=(0, 0, 1), mats=IRON)
    rope_coil(bm, (0.3, 0.1, JETTY), 0.22, ROPE)
    w = Walk(name)
    w.rect(0, 0, 3, 3, JETTY)
    w.drop((0, 0), (2, 0), (0, 2), (2, 2))                      # the posts block their corners
    return make(name, bm, MATS, w)


def jetty_stairs(name="JettyStairs"):
    """Steps from the bank (a quay's height) down onto a jetty along +Y, two tiles: a stringer each
    side notched for the treads, each tread with a nosing, a post at the foot of each stringer."""
    rnd = random.Random(5)
    bm = bmesh.new()
    n = 5
    for k in range(n):
        z = QUAY - 0.02 - (QUAY - JETTY) * (k + 1) / (n + 1)
        y0, y1 = -0.5 + 1.9 * k / n, -0.5 + 1.9 * (k + 1) / n
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
    bollard, and a timber fender against the face between them."""
    rnd = random.Random(11)
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
        A.hull(bm, [Vector((x, y, QUAY - 0.12)) for x in (a, b) for y in (-0.64, 0.25)] +
               [Vector((x, y, QUAY)) for x in (a + 0.02, b - 0.02) for y in (-0.61, 0.25)], COPE)
    for k in range(0, length, 2):
        bollard_at(bm, k, -0.2, QUAY, IRON)
        ring_on(bm, (k, face_y(QUAY - 0.4) - 0.01, QUAY - 0.38), (0, -1, 0), 0.09, IRON)
    for k in range(1, length, 2):                   # fenders between the bollards
        beam(bm, (k, face_y(BED + 0.3) - 0.09, BED + 0.3), (k, face_y(QUAY - 0.16) - 0.09, QUAY - 0.16), 0.16, 0.14, mat=OLD)
    w = Walk(name or "QuayEdge%d" % length)
    w.rect(0, 0, length, 1, QUAY)
    for k in range(0, length, 2):
        w.drop((k, 0))                              # the bollards block their tiles
    return make(w.name, bm, MATS, w)


def quay_steps(name="QuaySteps"):
    """Water stairs let into a quay's face, running down along it (+X) from the coping to the water:
    three broad stone steps with pale nosings, the lowest green with weed, a low wall with its own
    coping on the water side."""
    bm = bmesh.new()
    hs = [QUAY - 0.2, JETTY, 0.15]
    for k, h in enumerate(hs):
        A.box(bm, (k - 0.5, -0.5, BED), (k + 0.5, 0.5, h - 0.06), STONE)
        A.box(bm, (k - 0.52, -0.5, h - 0.06), (k + 0.5, 0.5, h), WEEDM if k == 2 else COPE)
    A.box(bm, (-0.5, -0.66, BED), (2.5, -0.5, QUAY - 0.08), STONE)
    A.hull(bm, [Vector((x, y, z)) for x in (-0.5, 2.5) for y, z in ((-0.7, QUAY - 0.1), (-0.46, QUAY - 0.1), (-0.68, QUAY), (-0.48, QUAY))], COPE)
    A.box(bm, (-0.5, -0.69, WEED[0]), (2.5, -0.66, WEED[1]), WEEDM)
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
    rnd = random.Random(4)
    bm = bmesh.new()
    z = 0.24
    for x in (-0.22, 1.22):
        for k in range(4):
            float_barrel(bm, (x, k - 0.42, 0.0), (x, k + 0.42, 0.0), 0.24)
    for x in (-0.4, 0.5, 1.4):
        beam(bm, (x, -0.5, z - 0.1), (x, 3.5, z - 0.1), 0.1, 0.1, mat=TIM)
    deck_boards(bm, -0.5, -0.5, 1.5, 3.5, z, rnd, mats=(BOARD, BOARD, OLD))
    for x in (-0.47, 1.47):
        kerb(bm, x, -0.5, 3.5, z)
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
    ring of bar holes in its head, two bars shipped."""
    bm = bmesh.new()
    A.prism(bm, 10, 0.5, 0.45, -0.05, 0.14, mat_index=STONE)
    for z0, z1, r0, r1 in ((0.14, 0.48, 0.3, 0.23), (0.48, 0.74, 0.23, 0.3)):
        A.prism(bm, 10, r0, r1, z0, z1, mat_index=TIM)
    for k in range(6):
        a = 2 * math.pi * k / 6
        d = Vector((math.cos(a), math.sin(a), 0))
        A.hull(bm, [d * r + Vector((0, 0, z)) + Vector((-d.y, d.x, 0)) * s for r, z in ((0.25, 0.16), (0.32, 0.16), (0.2, 0.48), (0.26, 0.48),
                                                                                       (0.25, 0.72), (0.32, 0.72)) for s in (-0.03, 0.03)], OLD)
    A.prism(bm, 10, 0.36, 0.36, 0.74, 0.9, mat_index=TIM)
    for k in range(2):
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
    sheet, cork floats along its foot."""
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
    for row_a, row_b in ((mid, top), (bot, mid)):
        for k in range(cols):
            bm.faces.new((row_a[k], row_a[k + 1], row_b[k + 1], row_b[k])).material_index = OLD
    for k in range(0, cols + 1, 2):
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
    rope against them."""
    rnd = random.Random(6)
    bm = bmesh.new()
    for k, (x, y, z) in enumerate(((0, 0, 0), (0.03, 0.02, 0.24), (-0.02, -0.01, 0.48), (0.62, -0.08, 0))):
        A.box(bm, (x - 0.27, y - 0.2, z), (x + 0.27, y + 0.2, z + 0.22), GREY)
        for zz in (0.07, 0.15):
            A.box(bm, (x - 0.28, y - 0.21, z + zz - 0.02), (x + 0.28, y + 0.21, z + zz + 0.02), BOARD)
    rope_coil(bm, (0.25, -0.5, 0), 0.24, ROPE)
    return make(name, bm, MATS)


def lobster_pots(name="LobsterPots"):
    """Wicker pots for crabs and eels, stacked by a jetty's foot: domed baskets with a dark mouth on
    top, three on the ground and one on them."""
    bm = bmesh.new()
    for x, y, z in ((-0.3, 0, 0), (0.3, 0.05, 0), (0.0, -0.45, 0), (0.0, -0.18, 0.38)):
        A.prism(bm, 8, 0.25, 0.25, z, z + 0.12, (x, y), mat_index=GREY, cap_bottom=True)
        A.prism(bm, 8, 0.25, 0.12, z + 0.12, z + 0.36, (x, y), mat_index=GREY)
        A.prism(bm, 8, 0.12, 0.11, z + 0.36, z + 0.37, (x, y), mat_index=OLD)
        for zz in (0.06, 0.2):
            r = 0.255 if zz < 0.12 else 0.2
            A.prism(bm, 8, r, r, z + zz, z + zz + 0.025, (x, y), mat_index=OLD, cap_top=False)
    return make(name, bm, MATS)
