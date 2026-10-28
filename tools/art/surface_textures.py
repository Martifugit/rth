#!/usr/bin/env python3
"""
Paints R'th's surface textures: 128 x 128 pixel art that tiles, in a few light greys. They carry
only the pattern (thatch rows, slates, stones, planks); the colour comes from the material's
palette colour multiplying them, so a recoloured piece keeps its pattern in the new colour
(docs/assets.md 4). Which material wears which texture, and how many tiles one repeat covers, is
client/Assets/Resources/Art/surfaces.json.

    python3 tools/art/surface_textures.py [--preview]

Writes client/Assets/Art/Textures/<Name>.png (and <Name>_preview.png at 4x with --preview).
Standard library only; the same seed gives the same images. Our own art, generated.
"""
import os
import random
import struct
import sys
import zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "client", "Assets", "Art", "Textures")
SIZE = 128
# Eight greys from shadow to highlight. Textures stay light: the palette colour is their average.
LEVELS = [0.50, 0.60, 0.68, 0.76, 0.84, 0.90, 0.95, 1.00]


def write_png(path, pixels, w, h, scale=1):
    """pixels: greys (floats) or (r, g, b) floats, 0-1."""
    rows = []
    for y in range(h * scale):
        row = bytearray(b"\x00")
        for x in range(w * scale):
            p = pixels[(y // scale) * w + (x // scale)]
            rgb = (p, p, p) if isinstance(p, float) else p
            row += bytes(int(round(c * 255)) for c in rgb)
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(kind, data):
        c = kind + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w * scale, h * scale, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def split(total, lo, hi, rnd):
    """Random widths between lo and hi that add up to total exactly (so the pattern tiles)."""
    out = []
    while total > 0:
        if total <= hi:
            if total >= lo or not out:
                out.append(total)
            else:
                out[-1] += total
            break
        w = rnd.randint(lo, hi)
        if total - w < lo:
            w = total - lo
        out.append(w)
        total -= w
    return out


class Canvas:
    def __init__(self, fill=6):
        self.px = [LEVELS[fill]] * (SIZE * SIZE)

    def set(self, x, y, level):
        self.px[(y % SIZE) * SIZE + (x % SIZE)] = LEVELS[max(0, min(7, level))]

    def tint(self, x, y, rgb):
        """Multiply one pixel by a colour (moss, lichen): the pattern keeps its shade."""
        i = (y % SIZE) * SIZE + (x % SIZE)
        v = self.px[i]
        v = (v, v, v) if isinstance(v, float) else v
        self.px[i] = tuple(a * b for a, b in zip(v, rgb))

    def darken(self, x, y, steps):
        i = (y % SIZE) * SIZE + (x % SIZE)
        v = self.px[i]
        if isinstance(v, float) and v in LEVELS:
            self.px[i] = LEVELS[max(0, LEVELS.index(v) - steps)]
        elif isinstance(v, float):
            self.px[i] = v * (1 - 0.08 * steps)
        else:
            self.px[i] = tuple(c * (1 - 0.08 * steps) for c in v)


def blob(rnd, cx, cy, r, fn):
    """Apply fn(x, y) over a ragged round patch (weathering, moss)."""
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if (dx * dx + dy * dy) ** 0.5 <= r - rnd.random() * r * 0.45:
                fn(cx + dx, cy + dy)

    def get_level(self, x, y):
        return LEVELS.index(self.px[(y % SIZE) * SIZE + (x % SIZE)])


def thatch(rnd):
    """Bold courses of reed: each course a band of strands with a ragged, shadowed lower edge,
    strands in a few straw colours (golden, pale, grey-brown), older darker patches."""
    straws = [(1.0, 0.97, 0.88), (1.0, 1.0, 1.0), (0.86, 0.82, 0.78), (0.94, 0.90, 0.80)]
    px = [None] * (SIZE * SIZE)
    row_h = 32
    for row in range(SIZE // row_h):
        y0 = row * row_h
        x = 0
        while x < SIZE:
            w = rnd.randint(1, 3)                                  # a strand bundle's width
            colour = rnd.choice(straws)
            level = rnd.choice((4, 5, 5, 6, 6, 7, 7))
            drop = rnd.randint(2, 8)                               # how far its end hangs
            for dx in range(w):
                for dy in range(row_h):
                    lv = level
                    if dy < 3:
                        lv -= 3                                    # tucked under the course above
                    elif dy < 6:
                        lv -= 1
                    if dy >= row_h - drop:
                        lv = 1 if dy >= row_h - 2 else 2           # the shadow under this course's edge
                    if dy % 7 == (x + dx) % 3:
                        lv -= 1                                    # strand texture
                    v = LEVELS[max(0, min(7, lv))]
                    px[(y0 + dy) * SIZE + (x + dx) % SIZE] = tuple(v * c for c in colour)
            x += w
    c = Canvas(5)
    c.px = px
    for _ in range(6):                                             # older, darker patches
        blob(rnd, rnd.randrange(SIZE), rnd.randrange(SIZE), rnd.randint(8, 16), lambda x, y: c.darken(x, y, 1))
    return c


def slate(rnd):
    """Slates in rows, each row half a slate off the last, every slate its own shade."""
    c = Canvas(6)
    row_h = 16
    for row in range(SIZE // row_h):
        y0 = row * row_h
        x = rnd.randint(0, 20)
        for w in split(SIZE, 18, 30, rnd):
            shade = rnd.choice((3, 4, 5, 5, 6, 6, 7))
            for dx in range(w):
                for dy in range(row_h):
                    level = shade
                    if dx == 0:
                        level = 0                                  # the gap between slates
                    elif dy >= row_h - 3:
                        level = 1 if dy == row_h - 1 else 2        # the next row's shadow
                    elif dy == 0:
                        level = min(7, shade + 1)
                    elif rnd.random() < 0.04:
                        level = shade - 1
                    c.set(x + dx, y0 + dy, level)
            x += w
    moss, lichen = (0.82, 0.95, 0.72), (0.98, 0.97, 0.80)
    for _ in range(7):                                             # moss, gathered in patches
        blob(rnd, rnd.randrange(SIZE), rnd.randrange(SIZE), rnd.randint(4, 10),
             lambda x, y: c.tint(x, y, moss) if rnd.random() < 0.7 else None)
    for _ in range(40):                                            # lichen, in specks
        c.tint(rnd.randrange(SIZE), rnd.randrange(SIZE), lichen)
    return c


def plaster(rnd):
    """Limewash over plaster, one repeat per storey (the image's bottom row is the ground):
    faint mottling, rising damp and splashes along the bottom, a shadow under the top."""
    c = Canvas(7)
    for _ in range(30):                                            # mottling
        blob(rnd, rnd.randrange(SIZE), rnd.randrange(SIZE), rnd.randint(3, 9), lambda x, y: c.set(x, y, 6))
    damp = 22
    for x in range(SIZE):
        edge = damp + rnd.randint(-4, 4)
        for k in range(edge):
            c.set(x, SIZE - 1 - k, 5 if k > edge - 5 else (4 if rnd.random() < 0.7 else 5))
    for _ in range(60):                                            # splashes above the damp
        c.set(rnd.randrange(SIZE), SIZE - damp - rnd.randint(0, 10), 5)
    for x in range(SIZE):                                          # the shadow under the top rail
        for y in range(4 + rnd.randint(0, 2)):
            c.set(x, y, 5)
    return c


def stones(rnd, row_lo, row_hi, w_lo, w_hi, rough):
    """Coursed stone: courses of varied height, stones of varied width, mortar between, each
    stone lit from the top left. rough: rounder corners and more speckle (field stone)."""
    c = Canvas(3)                                                  # mortar
    y = 0
    for h in split(SIZE, row_lo, row_hi, rnd):
        x = rnd.randint(0, w_hi)
        for w in split(SIZE, w_lo, w_hi, rnd):
            shade = rnd.choice((5, 6, 6, 7)) if not rough else rnd.choice((4, 5, 6, 6, 7))
            for dx in range(1, w - 1):
                for dy in range(1, h - 1):
                    corner = min(dx, w - 2 - dx) + min(dy, h - 2 - dy)
                    if rough and corner < 2:
                        continue                                   # knock the corners off
                    level = shade
                    if dy == 1 or dx == 1:
                        level = min(7, shade + 1)                  # lit edge
                    if dy == h - 2 or dx == w - 2:
                        level = shade - 2                          # shaded edge
                    if rough and rnd.random() < 0.08:
                        level -= 1
                    c.set(x + dx, y + dy, level)
            x += w
        y += h
    return c


def planks(rnd):
    """Vertical boards with grain, a dark seam between each, the odd knot."""
    c = Canvas(5)
    x = 0
    for w in split(SIZE, 14, 20, rnd):
        shade = rnd.choice((4, 5, 6))
        grain = [rnd.random() < 0.3 for _ in range(w)]
        for dx in range(w):
            for y in range(SIZE):
                level = shade
                if dx == 0:
                    level = 1
                elif grain[dx] and (y + dx * 7) % 23 < 15:
                    level = shade - 1
                elif dx == 1:
                    level = min(7, shade + 1)
                c.set(x + dx, y, level)
        if rnd.random() < 0.6:                                     # a knot
            kx, ky = x + rnd.randint(3, w - 4), rnd.randint(0, SIZE - 1)
            for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (-1, 0), (0, -1)):
                c.set(kx + dx, ky + dy, 2)
        x += w
    return c


def beam(rnd):
    """Old oak: streaky grain, a few splits (checks) and knots. Beams run both ways, so the
    grain is short streaks rather than long lines."""
    c = Canvas(5)
    for _ in range(420):                                           # streaks of grain
        x, y = rnd.randrange(SIZE), rnd.randrange(SIZE)
        level = rnd.choice((3, 4, 4, 6, 6, 7))
        horizontal = rnd.random() < 0.5
        for k in range(rnd.randint(4, 14)):
            c.set(x + (k if horizontal else 0), y + (0 if horizontal else k), level)
    for _ in range(10):                                            # splits
        x, y = rnd.randrange(SIZE), rnd.randrange(SIZE)
        horizontal = rnd.random() < 0.5
        for k in range(rnd.randint(8, 22)):
            c.set(x + (k if horizontal else rnd.randint(0, 1)), y + (rnd.randint(0, 1) if horizontal else k), 1)
    for _ in range(4):                                             # knots
        x, y = rnd.randrange(SIZE), rnd.randrange(SIZE)
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (-1, 0), (0, -1), (2, 1), (1, 2)):
            c.set(x + dx, y + dy, 2 if (dx, dy) in ((0, 0), (1, 1)) else 3)
    return c


# ---- chunky textures --------------------------------------------------------------------------
# The harbour's (and anything else that wants the old look): painted on a 32 x 32 grid and blown up
# four times, so every texel is a visible block, as in the reference screenshots; few shades, bold
# seams, no fine grain.

CELL = 4
GRID = SIZE // CELL


def coarse(grid):
    """A Canvas from a GRID x GRID list of levels, each cell CELL pixels square."""
    c = Canvas(5)
    for gy in range(GRID):
        for gx in range(GRID):
            level = grid[gy][gx]
            for dy in range(CELL):
                for dx in range(CELL):
                    c.set(gx * CELL + dx, gy * CELL + dy, level)
    return c


def chunky(c):
    """Any painter's pattern made as chunky as the 2004 game's: each CELL x CELL block one shade,
    its commonest, or its darkest where a joint or seam runs through it (so mortar lines, slate
    edges and reed shadows stay one cell wide rather than averaging away). Tinted pixels (moss)
    average. Fine 128-pixel detail read as plastic and modern on buildings (the owner, 2026-10-28)."""
    out = Canvas()
    for gy in range(GRID):
        for gx in range(GRID):
            cell = [c.px[(gy * CELL + dy) * SIZE + gx * CELL + dx] for dy in range(CELL) for dx in range(CELL)]
            if any(not isinstance(v, float) for v in cell):
                rgb = [(v, v, v) if isinstance(v, float) else v for v in cell]
                value = tuple(sum(p[k] for p in rgb) / len(rgb) for k in range(3))
            else:
                darkest, mean = min(cell), sum(cell) / len(cell)
                value = darkest if darkest <= LEVELS[2] else min(LEVELS, key=lambda lv: abs(lv - mean))
            for dy in range(CELL):
                for dx in range(CELL):
                    out.px[(gy * CELL + dy) * SIZE + gx * CELL + dx] = value
    return out


def boards(rnd):
    """Broad boards side by side (the seams run down the image), each its own shade, a dark seam and a
    lit edge beside it, a short streak of grain or two, and on each board one butt joint with a pair
    of nail heads by it."""
    g = [[5] * GRID for _ in range(GRID)]
    x = 0
    for w in (8, 8, 8, 8):
        shade = rnd.choice((4, 5, 5, 6))
        joint = rnd.randrange(GRID)
        for gx in range(x, x + w):
            for gy in range(GRID):
                g[gy][gx] = shade
        for gy in range(GRID):
            g[gy][x] = 1                                       # the seam
            g[gy][x + 1] = min(7, shade + 1)                    # the lit edge beside it
        for _ in range(2):                                     # grain
            gx, gy = rnd.randint(x + 2, x + w - 2), rnd.randrange(GRID)
            for k in range(rnd.randint(3, 6)):
                g[(gy + k) % GRID][gx] = shade - 1
        for gx in range(x + 1, x + w):                         # the butt joint
            g[joint][gx] = 2
        for gx in (x + 2, x + w - 2):
            g[(joint + 1) % GRID][gx] = 1                      # nail heads
        x += w
    return coarse(g)


def blocks(rnd):
    """Big squared blocks in courses, each course half a block off the last: a dark joint between them,
    each block its own shade, lit along its top, shaded along its foot."""
    g = [[2] * GRID for _ in range(GRID)]
    course = 4
    for row in range(GRID // course):
        y0 = row * course
        x = (row % 2) * 4 + rnd.randint(0, 1)
        widths = []
        total = 0
        while total < GRID:
            wdt = rnd.choice((6, 7, 8, 8, 9))
            widths.append(wdt)
            total += wdt
        for wdt in widths:
            shade = rnd.choice((4, 5, 5, 6, 6, 7))
            for dx in range(1, wdt):
                for dy in range(1, course):
                    level = shade
                    if dy == 1:
                        level = min(7, shade + 1)
                    elif dy == course - 1:
                        level = shade - 1
                    g[(y0 + dy) % GRID][(x + dx) % GRID] = level
            x += wdt
    for _ in range(6):                                         # a few chips and stains
        g[rnd.randrange(GRID)][rnd.randrange(GRID)] = 3
    return coarse(g)


def coursed(rnd, heights, widths, joint=2):
    """Stone in courses, painted on the chunky grid (one-pixel mortar can't survive being made
    chunky): courses of the given heights in cells (split to fill the tile exactly), stones of the
    given widths, each its own shade, lit along its top and shaded along its foot, a dark joint
    round each. Fieldstone: uneven courses of small stones; ashlar: even courses of long ones."""
    g = [[joint] * GRID for _ in range(GRID)]
    y0 = 0
    for course in split(GRID, heights[0], heights[1], rnd):
        x = rnd.randrange(GRID)
        total = 0
        while total < GRID:
            wdt = min(rnd.randint(widths[0], widths[1]), GRID - total)
            shade = rnd.choice((4, 5, 5, 6, 6, 7))
            for dx in range(1, wdt):
                for dy in range(1, course):
                    level = min(7, shade + 1) if dy == 1 else shade - 1 if dy == course - 1 and course > 2 else shade
                    g[(y0 + dy) % GRID][(x + dx) % GRID] = level
            x += wdt
            total += wdt
        y0 += course
    for _ in range(5):                                         # a few chips and stains
        g[rnd.randrange(GRID)][rnd.randrange(GRID)] = 3
    return coarse(g)


def flags(rnd):
    """Paving flags, roughly square, laid in rows each a little off the last: a dark joint, each flag
    its own shade, the odd crack."""
    g = [[2] * GRID for _ in range(GRID)]
    size = 8
    for row in range(GRID // size):
        y0 = row * size
        x = (row % 2) * 3
        for k in range(GRID // size + 1):
            shade = rnd.choice((4, 5, 5, 6, 6))
            for dx in range(1, size):
                for dy in range(1, size):
                    level = shade
                    if dy == 1 or dx == 1:
                        level = min(7, shade + 1)
                    g[(y0 + dy) % GRID][(x + dx) % GRID] = level
            if rnd.random() < 0.3:                             # a crack across it
                cx, cy = x + rnd.randint(2, 5), y0 + 1
                for dy in range(1, size - 1):
                    cx += rnd.choice((-1, 0, 0, 1))
                    g[(cy + dy) % GRID][cx % GRID] = 3
            x += size
    return coarse(g)


def value_noise(rnd, cells):
    """Smooth noise that tiles: random values on a cells x cells lattice, smoothly interpolated."""
    lat = [[rnd.random() for _ in range(cells)] for _ in range(cells)]
    step = SIZE / cells

    def f(x, y):
        gx, gy = x / step, y / step
        x0, y0 = int(gx) % cells, int(gy) % cells
        x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
        tx, ty = gx - int(gx), gy - int(gy)
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        a = lat[y0][x0] + (lat[y0][x1] - lat[y0][x0]) * tx
        b = lat[y1][x0] + (lat[y1][x1] - lat[y1][x0]) * tx
        return a + (b - a) * ty
    return f


def soft_coursed(rnd, course=(15, 20), length=(36, 64), weathered=False):
    """Coursed stone painted soft, after images/wall-texture.png: long flat stones in thin courses
    (a 1 m repeat: courses about 15 cm, stones 30-50 cm), each its own shade and mottled inside, with
    ragged light-grey joints, a lit top and a shadowed foot and right side, the odd speck; weathered
    adds a little moss, mostly in the joints, and lichen. Painted at full resolution rather than on
    the chunky grid: the chunky stones read as pixel-drawn (the owner, 2026-10-28)."""
    big, mid, fine = value_noise(rnd, 4), value_noise(rnd, 16), value_noise(rnd, 64)
    edge = value_noise(rnd, 32)
    moss_map = value_noise(rnd, 8)
    stone_id = [[-1] * SIZE for _ in range(SIZE)]
    shade, rows = [], []
    y = 0
    for h in split(SIZE, course[0], course[1], rnd):
        x = rnd.randrange(SIZE)
        total = 0
        while total < SIZE:
            w = min(rnd.randint(length[0], length[1]), SIZE - total)
            if SIZE - total - w < length[0] // 2:
                w = SIZE - total
            k = len(shade)
            shade.append(rnd.uniform(-0.15, 0.12))
            rows.append((x, y, w, h))
            for dx in range(w):
                for dy in range(h):
                    stone_id[(y + dy) % SIZE][(x + dx) % SIZE] = k
            x += w
            total += w
        y += h
    px = []
    for py in range(SIZE):
        for qx in range(SIZE):
            k = stone_id[py][qx]
            sx, sy, w, h = rows[k]
            dx, dy = (qx - sx) % SIZE, (py - sy) % SIZE
            gap = 1.6 + 1.6 * edge(qx, py)                            # ragged joints, 1.6-3.2 px
            d = min(dx + 0.5, w - dx - 0.5, dy + 0.5, h - dy - 0.5)
            m = 0.5 * mid(qx, py) + 0.35 * fine(qx, py) + 0.15 * big(qx, py)
            if d < gap:                                               # joint: a light, dull grey
                v = 0.66 + 0.08 * (m - 0.5)
            else:
                v = 0.88 + shade[k] + 0.18 * (m - 0.5)
                top, foot = dy - gap, (h - 1 - dy) - gap
                if top < 2.0:
                    v += 0.10 * (1 - top / 2.0)                       # lit top edge
                if foot < 3.0:
                    v -= 0.12 * (1 - foot / 3.0)                      # shadowed foot
                if (w - 1 - dx) - gap < 2.0:
                    v -= 0.05                                         # shadowed right side
                if rnd.random() < 0.06:
                    v -= rnd.uniform(0.03, 0.08)                      # speckle
            v = max(0.45, min(1.0, v))
            r = g = b = v
            if weathered:
                moss = max(0.0, moss_map(qx, py) - 0.72) * 2.0          # moss gathers in patches...
                if d < gap + 1.5:
                    moss *= 1.5                                       # ...and in the joints
                moss = min(0.35, moss)
                r, g, b = r * (1 - 0.32 * moss), g * (1 - 0.12 * moss), b * (1 - 0.40 * moss)
                if d >= gap and fine(qx * 3 % SIZE, py * 3 % SIZE) > 0.86:
                    r, g, b = min(1, r + 0.08), min(1, g + 0.08), min(1, b + 0.03)   # lichen
            px.append((r, g, b))
    c = Canvas()
    c.px = px
    return c


TEXTURES = {
    "Thatch": lambda r: chunky(thatch(r)),
    "Slate": lambda r: chunky(slate(r)),
    "Fieldstone": lambda r: soft_coursed(random.Random(11), weathered=True),   # the seed the owner chose it at
    "Ashlar": lambda r: coursed(r, (4, 4), (7, 9)),
    "Planks": lambda r: chunky(planks(r)),
    "Plaster": lambda r: chunky(plaster(r)),
    "Beam": lambda r: chunky(beam(r)),
    "Boards": lambda r: boards(r),
    "Blocks": lambda r: blocks(r),
    "Flags": lambda r: flags(r),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    preview = "--preview" in sys.argv
    for i, (name, make) in enumerate(TEXTURES.items()):
        c = make(random.Random(1000 + i))
        write_png(os.path.join(OUT, name + ".png"), c.px, SIZE, SIZE)
        if preview:
            write_png(os.path.join(OUT, name + "_preview.png"), c.px, SIZE, SIZE, scale=4)
        print("wrote", name)


if __name__ == "__main__":
    main()
