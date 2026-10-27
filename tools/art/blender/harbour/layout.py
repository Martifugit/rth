"""Harbours drawn as text: a whole waterfront from rows of characters, one a tile, the way a region's
ground is drawn (rows from the north, characters from the west). The composer builds the stone, the
timber, the stairs and the dressing from the drawing, and one walk layout for all of it.

Legend:
    .   water (also a space)
    S   stone, at the quay's height: a quay, a pier, a mole. Its sides are battered masonry only
        where they face water; coping along those edges, a buttress every third tile of a straight
        run; paved on top.
    J   a timber jetty's deck, a half tile above the water: piles round its outline and through its
        middle, headstocks and joists along the grid lines, kerbs on its open edges.
    W   worn timber (grey, sagging; walkable). X: broken timber (a hole; not walkable).
    > < ^ v   stairs, going down in the arrow's direction, from the tile behind to the tile in
        front: timber stairs between stone and a jetty, stone steps down from stone to the water.
    b   an iron bollard on stone; p a tall mooring post on timber; l a lamp (on stone or timber).
        Each stands on the deck of its neighbours' kind and blocks its tile.

Ships and boats are placed on top (`berths`), each a model from vessels.py at a tile with a turn.
"""
import math
import random

import bmesh
from mathutils import Vector

import rth_art as A
import common as C
from common import BED, JETTY, QUAY, WEED, Walk, beam, bollard_at, deck, make, pile, ring_on
from structures import chip_off, cracks, loose_stone

MATS = ["Fence", "Boards", "Iron", "Driftwood", "Moss", "DeadWood", "Harbourstone", "Gravestone", "Flagstone", "CandleWax", "Hide"]
TIM, BOARD, IRON, GREY, WEEDM, OLD, STONE, COPE, PAVE, GLOW, ROPE = range(len(MATS))

STONEY = set("Sbs")          # cells built as stone
TIMBER = set("JWXp")         # cells built as timber
STAIRS = {">": (1, 0), "<": (-1, 0), "^": (0, 1), "v": (0, -1)}
DIRS = {"E": (1, 0), "W": (-1, 0), "N": (0, 1), "S": (0, -1)}


class Plan:
    def __init__(self, rows):
        self.h = len(rows)
        self.w = max(len(r) for r in rows)
        self.cells = {}
        for r, row in enumerate(rows):
            y = self.h - 1 - r
            for x, ch in enumerate(row):
                if ch not in ". ":
                    self.cells[(x, y)] = ch

    def get(self, x, y):
        return self.cells.get((x, y), ".")

    def kind(self, x, y):
        """stone, timber, stairs, or water."""
        ch = self.get(x, y)
        if ch in STAIRS:
            return "stairs"
        if ch == "l":                       # a lamp takes its neighbours' kind
            near = [self.kind(x + dx, y + dy) for dx, dy in DIRS.values() if self.get(x + dx, y + dy) != "l"]
            return "stone" if "stone" in near else "timber"
        if ch in STONEY:
            return "stone"
        if ch in TIMBER:
            return "timber"
        return "water"

    def height(self, x, y):
        k = self.kind(x, y)
        return QUAY if k == "stone" else JETTY if k == "timber" else None


def stone_cell(bm, plan, x, y, rnd):
    """One tile of stone: a block from the bed to the deck whose sides lean out toward the bed only
    where they face water; a weed band on those sides, coping along their tops, paving between."""
    water = {d: plan.kind(x + dx, y + dy) in ("water",) or (plan.kind(x + dx, y + dy) == "timber")
             for d, (dx, dy) in DIRS.items()}

    def corner(sx, sy, z, out):
        cx, cy = x + 0.5 * sx, y + 0.5 * sy
        ex = out if water["E" if sx > 0 else "W"] else 0.0
        ey = out if water["N" if sy > 0 else "S"] else 0.0
        return Vector((cx + sx * ex, cy + sy * ey, z))

    def batter(z):
        return 0.3 * (1 - (z - BED) / (QUAY - 0.13 - BED))
    pts = [corner(sx, sy, z, batter(z)) for sx in (-1, 1) for sy in (-1, 1) for z in (BED, QUAY - 0.13)]
    A.hull(bm, pts, STONE)
    if any(water.values()):
        band = [corner(sx, sy, z, batter(z) + 0.012) for sx in (-1, 1) for sy in (-1, 1) for z in WEED]
        for f in A.hull(bm, band, WEEDM):
            f.normal_update()
    A.box(bm, (x - 0.5, y - 0.5, QUAY - 0.13), (x + 0.5, y + 0.5, QUAY - 0.02), PAVE)
    if C.WEAR and rnd.random() < 0.3:
        cracks(bm, x - 0.45, x + 0.45, y - 0.45, y + 0.45, QUAY - 0.017, rnd, n=1)
    for d, (dx, dy) in DIRS.items():
        if not water[d]:
            continue
        if dx:
            ex = x + 0.5 * dx
            pts = [Vector((xx, yy, z)) for xx in (ex - 0.24 * dx, ex + 0.08 * dx) for yy in (y - 0.51, y + 0.51) for z in (QUAY - 0.13, QUAY)]
        else:
            ey = y + 0.5 * dy
            pts = [Vector((xx, yy, z)) for yy in (ey - 0.24 * dy, ey + 0.08 * dy) for xx in (x - 0.51, x + 0.51) for z in (QUAY - 0.13, QUAY)]
        if C.WEAR and rnd.random() < 0.22:                       # a chipped block, the chip beside it
            chip_off(pts, Vector((dx, dy, 0)) + Vector((dy, dx, 0)) * rnd.choice((-1, 1)), rnd)
            loose_stone(bm, (x - dx * 0.2 + dy * rnd.uniform(-0.3, 0.3), y - dy * 0.2 + dx * rnd.uniform(-0.3, 0.3), QUAY - 0.02), 0.06, rnd, COPE)
        A.hull(bm, pts, COPE)
        if C.WEAR and rnd.random() < 0.12:                       # a stone fallen at the foot
            loose_stone(bm, (x + dx * rnd.uniform(0.95, 1.2), y + dy * rnd.uniform(0.95, 1.2), -0.3), rnd.uniform(0.14, 0.2), rnd, STONE)
        straight = all(plan.kind(x + dy * s, y + dx * s) == "stone" and plan.kind(x + dy * s + dx, y + dx * s + dy) == "water" for s in (-1, 1))
        if straight and (x + y) % 3 == 0:                         # a buttress in a straight run
            side = Vector((dx, dy, 0))
            along = Vector((dy, dx, 0))
            base = Vector((x, y, 0)) + side * 0.5
            A.hull(bm, [base + side * o + along * s * 0.25 + Vector((0, 0, z)) for s in (-1, 1)
                        for o, z in ((0.0, BED), (0.5, BED), (0.0, QUAY - 0.2), (0.35, QUAY - 0.2))], STONE)
            A.hull(bm, [base + side * o + along * s * 0.29 + Vector((0, 0, z)) for s in (-1, 1)
                        for o, z in ((0.05, QUAY - 0.2), (0.4, QUAY - 0.2), (0.05, QUAY - 0.13), (0.36, QUAY - 0.09))], COPE)
        elif (x * 7 + y * 3) % 4 == 1:                            # an iron ring now and then
            n = Vector((dx, dy, 0))
            ring_on(bm, Vector((x, y, QUAY - 0.42)) + n * 0.6, n, 0.09, IRON)


def timber_frame(bm, plan, rnd, cells):
    """The timber under every jetty deck: piles at the grid points round the outline and every other
    one inside, a headstock under each grid line across X and a joist under each along Y, X-braces
    under the open edges."""
    def is_t(x, y):
        return (x, y) in cells

    points = set()
    for (x, y) in cells:
        for sx in (0, 1):
            for sy in (0, 1):
                points.add((x + sx, y + sy))           # grid points: tile (x, y) spans grid x..x+1
    for gx, gy in points:
        around = [(gx - 1 + i, gy - 1 + j) for i in (0, 1) for j in (0, 1)]
        mine = [c for c in around if is_t(*c)]
        if not mine:
            continue
        edge = len(mine) < 4
        if not edge and (gx % 2 or gy % 2):
            continue
        if any(plan.kind(*c) == "stone" for c in around):
            continue                                    # the stone carries this corner
        pos = Vector((gx - 0.5, gy - 0.5, 0))
        if edge:
            d = sum((Vector((c[0], c[1], 0)) - pos for c in mine), Vector())
            pos += d.normalized() * 0.16
        pile(bm, pos.x, pos.y, JETTY - 0.04, rnd, r=0.115, mat=TIM, weed=WEEDM)
    for gy in {p[1] for p in points}:                   # headstocks along X
        run = sorted(x for (x, y) in cells if y == gy or y == gy - 1)
        xs = sorted(set(run))
        start = None
        for i, x in enumerate(xs + [None]):
            if start is None:
                start = x
            elif x is None or x != xs[i - 1] + 1:
                beam(bm, (start - 0.5, gy - 0.5, JETTY - 0.24), (xs[i - 1] + 0.5, gy - 0.5, JETTY - 0.24), 0.13, 0.13, mat=TIM)
                start = x
    for gx in {p[0] for p in points}:                   # joists along Y
        run = sorted(set(y for (x, y) in cells if x == gx or x == gx - 1))
        start = None
        for i, y in enumerate(run + [None]):
            if start is None:
                start = y
            elif y is None or y != run[i - 1] + 1:
                beam(bm, (gx - 0.5, start - 0.5, JETTY - 0.12), (gx - 0.5, run[i - 1] + 0.5, JETTY - 0.12), 0.1, 0.1, mat=OLD)
                start = y


def timber_cell(bm, plan, x, y, rnd, worn=False):
    """One tile of jetty deck, a kerb along each edge that faces water (or a drop), a brace under it."""
    if worn:
        sag = 0.06
        yy = y - 0.5
        while yy < y + 0.45:
            w = rnd.uniform(0.17, 0.24)
            if rnd.random() > 0.12:
                A.box(bm, (x - 0.54 + rnd.uniform(-0.04, 0.04), yy + 0.01, JETTY - 0.07 - sag * math.sin(math.pi * (yy - y + 0.5))),
                      (x + 0.54 + rnd.uniform(-0.05, 0.03), min(yy + w, y + 0.5) - 0.01, JETTY - 0.01 - sag * math.sin(math.pi * (yy - y + 0.5))),
                      rnd.choice((GREY, GREY, OLD)))
            yy += w
    else:
        deck(bm, x - 0.5, x + 0.5, y - 0.5, y + 0.5, JETTY, BOARD, OLD, rnd, C.WEAR, chance=0.12)
    for d, (dx, dy) in DIRS.items():
        k = plan.kind(x + dx, y + dy)
        if k in ("timber", "stairs"):
            continue
        if k == "stone":
            continue
        if dx:
            ex = x + 0.47 * dx
            beam(bm, (ex, y - 0.5, JETTY + 0.035), (ex, y + 0.5, JETTY + 0.035), 0.07, 0.1, mat=TIM)
            if (x + y) % 2 == 0:
                for a, b in ((-0.4, 0.4), (0.4, -0.4)):
                    beam(bm, (x + 0.42 * dx, y + a, JETTY - 0.3), (x + 0.42 * dx, y + b, WEED[1] + 0.05), 0.07, 0.05, mat=OLD)
        else:
            ey = y + 0.47 * dy
            beam(bm, (x - 0.5, ey, JETTY + 0.035), (x + 0.5, ey, JETTY + 0.035), 0.07, 0.1, mat=TIM)
            if (x + y) % 2 == 0:
                for a, b in ((-0.4, 0.4), (0.4, -0.4)):
                    beam(bm, (x + a, y + 0.42 * dy, JETTY - 0.3), (x + b, y + 0.42 * dy, WEED[1] + 0.05), 0.07, 0.05, mat=OLD)


def broken_cell(bm, x, y, rnd):
    """A tile where the jetty has gone: a snapped pile, boards hanging into the hole."""
    from structures import snapped
    snapped(bm, x - 0.3, y, rnd.uniform(-0.05, 0.25), rnd)
    for k in range(2):
        A.hull(bm, [Vector((x + xx, y - 0.4 + 0.25 * k + dy, z)) for xx, z in ((-0.55, JETTY - 0.05 - 0.1 * k), (0.1 - 0.3 * k, JETTY - 0.2 - 0.12 * k))
                    for dy in (0.0, 0.18)] + [Vector((x - 0.55, y - 0.4 + 0.25 * k, JETTY - 0.1 - 0.1 * k))], GREY)


def stairs_cell(bm, plan, x, y, ch, w):
    """Stairs going down in the arrow's direction: timber stairs from stone down to a jetty, or stone
    steps from stone down to the water. Returns the tile's walk height."""
    dx, dy = STAIRS[ch]
    hi = plan.height(x - dx, y - dy) or QUAY
    lo_k = plan.kind(x + dx, y + dy)
    lo = plan.height(x + dx, y + dy) if lo_k != "water" else 0.12
    stone = lo_k == "water" or plan.kind(x - dx, y - dy) == "stone" and lo_k == "stone"
    n = 4
    side = Vector((-dy, dx, 0))
    fwd = Vector((dx, dy, 0))
    c = Vector((x, y, 0))
    for k in range(n):
        z = hi - (hi - lo) * (k + 1) / (n + 1)
        a = c - fwd * 0.5 + fwd * (k / n)
        b = c - fwd * 0.5 + fwd * ((k + 1) / n)
        pts = [p + side * s * 0.46 + Vector((0, 0, zz)) for p in (a, b) for s in (-1, 1)
               for zz in ((BED, z) if stone else (z - 0.06, z))]
        A.hull(bm, pts, STONE if stone else BOARD)
        if stone:
            A.hull(bm, [p + side * s * 0.47 + Vector((0, 0, zz)) for p in (a - fwd * 0.02, b) for s in (-1, 1) for zz in (z - 0.05, z)],
                   WEEDM if k == n - 1 else COPE)
    if not stone:
        for s in (-1, 1):
            beam(bm, c - fwd * 0.5 + side * s * 0.5 + Vector((0, 0, hi - 0.15)), c + fwd * 0.5 + side * s * 0.5 + Vector((0, 0, lo - 0.1)), 0.3, 0.08, mat=TIM)
    return (hi + lo) / 2


def lamp(bm, x, y, on_stone):
    if on_stone:
        A.box(bm, (x - 0.35, y - 0.35, QUAY - 0.13), (x + 0.35, y + 0.35, QUAY + 0.3), STONE)
        A.box(bm, (x - 0.41, y - 0.41, QUAY + 0.3), (x + 0.41, y + 0.41, QUAY + 0.38), COPE)
        z0 = QUAY + 0.38
    else:
        z0 = JETTY
        A.box(bm, (x - 0.5, y - 0.5, JETTY - 0.08), (x + 0.5, y + 0.5, JETTY), BOARD)
    A.prism(bm, 8, 0.1, 0.07, z0, z0 + 1.8, (x, y), mat_index=IRON if on_stone else TIM)
    A.prism(bm, 6, 0.18, 0.22, z0 + 1.8, z0 + 2.2, (x, y), mat_index=GLOW)
    A.prism(bm, 6, 0.26, 0.03, z0 + 2.2, z0 + 2.36, (x, y), mat_index=IRON)


def compose(name, rows, rnd_seed=1):
    """Build a harbour from its drawing: one object, origin at the drawing's south-west tile, and one
    walk layout for all of it."""
    rnd = random.Random(rnd_seed)
    plan = Plan(rows)
    bm = bmesh.new()
    w = Walk(name)
    timber = {c for c in plan.cells if plan.kind(*c) == "timber" and plan.get(*c) != "X"}
    timber_frame(bm, plan, rnd, timber | {c for c in plan.cells if plan.get(*c) == "X"})
    for (x, y), ch in sorted(plan.cells.items()):
        k = plan.kind(x, y)
        if ch in STAIRS:
            w.tile(x, y, stairs_cell(bm, plan, x, y, ch, w))
        elif k == "stone":
            stone_cell(bm, plan, x, y, rnd)
            if ch == "b":
                bollard_at(bm, x, y, QUAY, IRON)
            elif ch == "l":
                lamp(bm, x, y, True)
            else:
                w.tile(x, y, QUAY)
        elif k == "timber":
            if ch == "X":
                broken_cell(bm, x, y, rnd)
                continue
            if ch == "l":
                lamp(bm, x, y, False)
                continue
            timber_cell(bm, plan, x, y, rnd, worn=(ch == "W"))
            if ch == "p":
                pile(bm, x, y, JETTY + 0.85, rnd, r=0.14, mat=TIM, weed=WEEDM, lean=0.0)
                for j in range(3):
                    A.prism(bm, 7, 0.17, 0.17, JETTY + 0.45 + 0.06 * j, JETTY + 0.5 + 0.06 * j, (x, y), mat_index=ROPE)
            else:
                w.tile(x, y, JETTY - (0.04 if ch == "W" else 0.0))
    # edges between walkable tiles at different heights block, unless stairs join them
    for (p, x, y), h in list(w.tiles.items()):
        for dx, dy in ((1, 0), (0, 1)):                       # each pair of neighbours once
            other = w.tiles.get((0, x + dx, y + dy))
            if other is None or abs(other - h) < 0.2:
                continue
            if plan.get(x, y) in STAIRS or plan.get(x + dx, y + dy) in STAIRS:
                continue
            w.wall(*((x + 1, y, x + 1, y + 1) if dx else (x, y + 1, x + 1, y + 1)))
    return make(name, bm, MATS, w)
