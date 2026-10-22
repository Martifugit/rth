#!/usr/bin/env python3
"""
Paints R'th's leaf-clump texture: 128 x 128 pixel art in the palette's seven leaf greens
(Leaf1..Leaf7 in client/Assets/Resources/Art/palette.json), opaque leaves with one ragged fringe
along the bottom edge (v = 0) and transparent pixels beyond it. Tree canopies map the fringe onto
one edge of each face (docs/assets.md, "Leafy trees"). Our own art, generated: nothing is copied.

    python3 tools/art/leaf_texture.py [out.png] [--seed N] [--preview preview.png]

Standard library only. Re-running with the same seed gives the same image.
"""
import json
import math
import os
import random
import struct
import sys
import zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SIZE = 128
FRINGE = 26  # rows from the bottom that the ragged edge can reach into


def palette_greens():
    with open(os.path.join(ROOT, "client", "Assets", "Resources", "Art", "palette.json"), encoding="utf-8") as f:
        colours = {c["name"]: c["hex"] for c in json.load(f)["colours"]}
    out = []
    for i in range(1, 8):
        h = colours["Leaf%d" % i].lstrip("#")
        out.append(tuple(int(h[k:k + 2], 16) for k in (0, 2, 4)))
    return out


def write_png(path, pixels, w, h):
    raw = b"".join(b"\x00" + bytes(v for px in pixels[y * w:(y + 1) * w] for v in px) for y in range(h))

    def chunk(kind, data):
        c = kind + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def paint(seed):
    rng = random.Random(seed)
    greens = palette_greens()
    # Shade index 0 (darkest) .. 6 (lightest); -1 is transparent.
    shade = [[1] * SIZE for _ in range(SIZE)]  # rows from the top, PNG order

    # Leaves: small rotated ellipses, lit from the top left, darker towards the bottom.
    leaves = []
    for _ in range(420):
        cx, cy = rng.uniform(-4, SIZE + 4), rng.uniform(-4, SIZE + 4)
        leaves.append((cy, cx, rng.uniform(3.0, 6.0), rng.uniform(1.8, 3.4), rng.uniform(0, math.pi)))
    leaves.sort()  # paint top rows first so lower leaves overlap them, as in a hanging clump
    for cy, cx, rx, ry, a in leaves:
        base = 2 + int(3.2 * (1 - cy / SIZE) + rng.uniform(-0.6, 1.4))
        ca, sa = math.cos(a), math.sin(a)
        for y in range(int(cy - rx) - 1, int(cy + rx) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                u, v = (dx * ca + dy * sa) / rx, (-dx * sa + dy * ca) / ry
                d = u * u + v * v
                if d > 1:
                    continue
                s = base
                if d > 0.62 and (dx + dy) > 0:
                    s -= 1          # rim in shadow, bottom right
                elif d < 0.3 and (dx + dy) < -0.5:
                    s += 1          # highlight, top left
                shade[y % SIZE][x % SIZE] = max(0, min(6, s))  # wraps sideways and vertically

    # The fringe: a ragged edge of leaf tips along the bottom. Below it is transparent.
    tips = []
    x = -6.0
    while x < SIZE + 6:
        w = rng.uniform(8, 16)
        tips.append((x + w / 2, w / 2, rng.uniform(8, FRINGE - 4)))
        x += w * rng.uniform(0.55, 0.85)
    for col in range(SIZE):
        reach = 4.0  # rows from the bottom that are always cut
        for tx, half, depth in tips:
            t = (col + 0.5 - tx) / half
            if abs(t) < 1:
                reach = max(reach, depth * math.sqrt(1 - t * t))
        edge = FRINGE - reach  # leaves end this many rows above the bottom
        for row in range(int(math.ceil(edge))):
            shade[SIZE - 1 - row][col] = -1
        # The last leaf pixels above the cut are in shadow.
        r = int(math.ceil(edge))
        if r < SIZE:
            shade[SIZE - 1 - r][col] = max(0, shade[SIZE - 1 - r][col] - 2) if shade[SIZE - 1 - r][col] >= 0 else -1
    # A few gaps between leaves just above the fringe, so the dark core shows through.
    for _ in range(7):
        gx, gy = rng.randrange(SIZE), SIZE - 1 - rng.randrange(FRINGE, FRINGE + 14)
        for y in range(gy - 1, gy + 1):
            for x in range(gx - 1, gx + 2):
                if 0 <= y < SIZE:
                    shade[y][x % SIZE] = -1

    pixels = []
    for row in shade:
        for s in row:
            pixels.append((0, 0, 0, 0) if s < 0 else greens[s] + (255,))
    return pixels


def main(argv):
    out = os.path.join(ROOT, "client", "Assets", "Art", "Textures", "LeafClump.png")
    seed = 11
    preview = None
    args = list(argv)
    while args:
        a = args.pop(0)
        if a == "--seed":
            seed = int(args.pop(0))
        elif a == "--preview":
            preview = args.pop(0)
        else:
            out = a
    pixels = paint(seed)
    write_png(out, pixels, SIZE, SIZE)
    colours = {p for p in pixels if p[3]}
    print("wrote %s: %d x %d, %d colours, %d%% transparent" %
          (out, SIZE, SIZE, len(colours), 100 * sum(1 for p in pixels if not p[3]) // len(pixels)))
    if preview:  # 4x, on a grey background, to look at
        scale = 4
        big = []
        for y in range(SIZE * scale):
            for x in range(SIZE * scale):
                p = pixels[(y // scale) * SIZE + x // scale]
                big.append(p if p[3] else (120, 120, 120, 255))
        write_png(preview, big, SIZE * scale, SIZE * scale)


if __name__ == "__main__":
    main(sys.argv[1:])
