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


def write_png(path, grey, w, h, scale=1):
    rows = []
    for y in range(h * scale):
        row = bytearray(b"\x00")
        for x in range(w * scale):
            v = int(round(grey[(y // scale) * w + (x // scale)] * 255))
            row += bytes((v, v, v))
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

    def get_level(self, x, y):
        return LEVELS.index(self.px[(y % SIZE) * SIZE + (x % SIZE)])


def thatch(rnd):
    """Rows of reed bundles: strands run down the slope, each row's ragged lower edge shading the
    row below. v grows down the slope here (rows are horizontal)."""
    c = Canvas(5)
    row_h = 16
    for row in range(SIZE // row_h):
        y0 = row * row_h
        shift = rnd.randint(0, 7)
        for x in range(SIZE):
            strand = rnd.choice((4, 5, 5, 6, 6, 7)) if (x + shift) % 3 else rnd.choice((3, 4, 5))
            drop = rnd.randint(1, 4)                               # this strand's ragged end
            for dy in range(row_h):
                level = strand
                if dy < 2:
                    level -= 2                                     # tucked under the row above
                if dy >= row_h - drop:
                    level = 2 if dy == row_h - 1 else 3            # the shadow under the overhang
                c.set(x, y0 + dy, level)
    return c


def slate(rnd):
    """Slates in rows, each row half a slate off the last, every slate its own shade."""
    c = Canvas(6)
    row_h = 16
    for row in range(SIZE // row_h):
        y0 = row * row_h
        x = rnd.randint(0, 20)
        for w in split(SIZE, 18, 30, rnd):
            shade = rnd.choice((4, 5, 5, 6, 6, 7))
            for dx in range(w):
                for dy in range(row_h):
                    level = shade
                    if dx == 0:
                        level = 1                                  # the gap between slates
                    elif dy >= row_h - 2:
                        level = 2                                  # the next row's shadow
                    elif dy == 0:
                        level = min(7, shade + 1)
                    elif rnd.random() < 0.04:
                        level = shade - 1
                    c.set(x + dx, y0 + dy, level)
            x += w
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


TEXTURES = {
    "Thatch": lambda r: thatch(r),
    "Slate": lambda r: slate(r),
    "Fieldstone": lambda r: stones(r, 12, 20, 14, 30, rough=True),
    "Ashlar": lambda r: stones(r, 16, 16, 28, 36, rough=False),
    "Planks": lambda r: planks(r),
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
