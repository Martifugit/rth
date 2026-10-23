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


TEXTURES = {
    "Thatch": lambda r: thatch(r),
    "Slate": lambda r: slate(r),
    "Fieldstone": lambda r: stones(r, 12, 20, 14, 30, rough=True),
    "Ashlar": lambda r: stones(r, 16, 16, 28, 36, rough=False),
    "Planks": lambda r: planks(r),
    "Plaster": lambda r: plaster(r),
    "Beam": lambda r: beam(r),
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
