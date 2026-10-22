#!/usr/bin/env python3
"""
Bootstraps the world layout (docs/world-building.md) from the sketch in images/map.png: one zone
(8 x 8 tiles) per cell, classified by the sketch's colours into biomes, with a macro height that
rises from the water inland and stands the mountains up. Writes:

  client/Assets/World/layout.json    the layout: biome and height rows, north first (hand-editable)
  docs/images/world-layout.png       a preview with the 96-tile region grid and today's regions

    python3 tools/world-layout/layout_from_sketch.py [--tiles-per-square 64] [--seed 1]

Needs Pillow (pip install pillow). This is a starting point to paint over, not the source of
truth: once the layout has been edited by hand, don't run it again over the edits.
"""
import argparse
import json
import math
import os
from collections import Counter, deque

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SKETCH = os.path.join(ROOT, "images", "map.png")
OUT = os.path.join(ROOT, "client", "Assets", "World", "layout.json")
PREVIEW = os.path.join(ROOT, "docs", "images", "world-layout.png")

ZONE = 8                      # tiles per zone (the server's zone size)
GRID_PX = 39                  # the sketch's grid squares, in pixels
TOLLING_PX = (580, 470)       # Tolling's box on the sketch...
TOLLING_TILE = (48, 48)       # ...is the middle of Tolling's region

# Biomes: a letter in the layout, the sketch colour that means it, and how the land behaves.
# ground: a palette colour name; base/slope/max: macro height in tiles (base at the shore, rising
# by slope per tile inland, up to max); rough: base noise amplitude in tiles.
BIOMES = {
    "~": dict(name="Sea", sketch=[(13, 14, 16), (48, 72, 120), (61, 94, 132), (114, 138, 166), (49, 79, 107), (63, 97, 136)],
              ground="Sea", water=True, walkable=False, base=0, slope=0, max=0, rough=0),
    "v": dict(name="Venaria", sketch=[(101, 116, 60), (112, 116, 70), (96, 110, 60)],
              ground="Grass", water=False, walkable=True, base=0.5, slope=0.012, max=4, rough=1.5),
    "s": dict(name="Sere", sketch=[(129, 107, 73), (133, 110, 75)],
              ground="Heath", water=False, walkable=True, base=1, slope=0.02, max=6, rough=2.5),
    "f": dict(name="Slough", sketch=[(78, 97, 83), (70, 92, 80)],
              ground="Fen", water=False, walkable=True, base=0.25, slope=0.006, max=2, rough=0.6),
    "x": dict(name="Scalth", sketch=[(103, 81, 69), (96, 72, 60)],
              ground="Scorch", water=False, walkable=True, base=1, slope=0.03, max=6, rough=3),
    "c": dict(name="Calcinia", sketch=[(136, 131, 85), (132, 127, 83)],
              ground="SaltFlat", water=False, walkable=True, base=0.25, slope=0.01, max=3, rough=1),
    "m": dict(name="Candora", sketch=[(122, 124, 127), (48, 48, 48), (24, 24, 24), (72, 72, 72), (96, 96, 96)],
              ground="CliffRock", water=False, walkable=False, base=9, slope=0, max=9, rough=3),
    "n": dict(name="Nivil", sketch=[(194, 196, 201), (180, 182, 188)],
              ground="Snow", water=False, walkable=False, base=6, slope=0.01, max=9, rough=2),
}
PREVIEW_COLOURS = {"~": (40, 70, 110), "v": (98, 140, 60), "s": (160, 130, 80), "f": (70, 100, 80), "x": (120, 80, 60),
                   "c": (190, 180, 120), "m": (110, 108, 104), "n": (225, 228, 232)}
DIGITS = "0123456789abcdefghijklmnopqrstuvwxyz"  # heights in half tiles: 0 .. 17.5


def classify(rgb):
    """The biome a sketch colour stands for, or None for label text (near white), which doesn't vote."""
    if min(rgb) > 225:
        return None
    best, letter = None, "~"
    for key, b in BIOMES.items():
        for ref in b["sketch"]:
            d = sum((a - c) ** 2 for a, c in zip(rgb, ref))
            if best is None or d < best:
                best, letter = d, key
    return letter


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tiles-per-square", type=int, default=64)
    ap.add_argument("--out", default=OUT)
    ap.add_argument("--preview", default=PREVIEW)
    args = ap.parse_args()

    img = Image.open(SKETCH).convert("RGB")
    w_px, h_px = img.size
    px_per_tile = GRID_PX / args.tiles_per_square

    def tile_to_px(tx, ty):
        return TOLLING_PX[0] + (tx - TOLLING_TILE[0]) * px_per_tile, TOLLING_PX[1] - (ty - TOLLING_TILE[1]) * px_per_tile

    # The zone grid covers the whole sketch, aligned so that zone corners are multiples of 8.
    min_tx = math.floor((0 - TOLLING_PX[0]) / px_per_tile + TOLLING_TILE[0])
    max_tx = math.ceil((w_px - TOLLING_PX[0]) / px_per_tile + TOLLING_TILE[0])
    min_ty = math.floor(TOLLING_TILE[1] - (h_px - TOLLING_PX[1]) / px_per_tile)
    max_ty = math.ceil(TOLLING_TILE[1] + TOLLING_PX[1] / px_per_tile)
    ox, oy = (min_tx // ZONE) * ZONE, (min_ty // ZONE) * ZONE
    cols = (max_tx - ox) // ZONE + 1
    rows = (max_ty - oy) // ZONE + 1

    # Classify each zone by the most common biome among a few samples inside it (labels and
    # dashed boxes on the sketch then lose to the land around them).
    grid = [[None] * cols for _ in range(rows)]  # [row from the south][col]
    for r in range(rows):
        for c in range(cols):
            votes = Counter()
            for sy in (0.2, 0.5, 0.8):
                for sx in (0.2, 0.5, 0.8):
                    px, py = tile_to_px(ox + (c + sx) * ZONE, oy + (r + sy) * ZONE)
                    if 0 <= px < w_px and 0 <= py < h_px:
                        letter = classify(img.getpixel((int(px), int(py))))
                        if letter:
                            votes[letter] += 1
                    else:
                        votes["~"] += 1
            grid[r][c] = votes.most_common(1)[0][0] if votes else None
    # Zones covered by labels take the commonest biome around them.
    for _ in range(4):
        for r in range(rows):
            for c in range(cols):
                if grid[r][c] is None:
                    around = Counter(grid[r + dr][c + dc] for dr in (-1, 0, 1) for dc in (-1, 0, 1)
                                     if 0 <= r + dr < rows and 0 <= c + dc < cols and grid[r + dr][c + dc])
                    grid[r][c] = around.most_common(1)[0][0] if around else None
    grid = [[ch or "~" for ch in row] for row in grid]
    # Label text that survived reads as specks of mountain or snow: any small patch of those
    # takes the commonest other biome around it. (Small patches of water stay: lakes, rivers.)
    seen = [[False] * cols for _ in range(rows)]
    for r0 in range(rows):
        for c0 in range(cols):
            if seen[r0][c0] or grid[r0][c0] not in "mn":
                continue
            patch, edge, q = [], Counter(), deque([(r0, c0)])
            seen[r0][c0] = True
            while q:
                r, c = q.popleft()
                patch.append((r, c))
                for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nr, nc = r + dr, c + dc
                    if not (0 <= nr < rows and 0 <= nc < cols):
                        continue
                    if grid[nr][nc] in "mn":
                        if not seen[nr][nc]:
                            seen[nr][nc] = True
                            q.append((nr, nc))
                    else:
                        edge[grid[nr][nc]] += 1
            if len(patch) < 60 and edge:
                fill = edge.most_common(1)[0][0]
                for r, c in patch:
                    grid[r][c] = fill

    # One pass of a majority filter for stray labels.
    smooth = [row[:] for row in grid]
    for r in range(1, rows - 1):
        for c in range(1, cols - 1):
            around = Counter(grid[r + dr][c + dc] for dr in (-1, 0, 1) for dc in (-1, 0, 1))
            letter, n = around.most_common(1)[0]
            if n >= 6:
                smooth[r][c] = letter
    grid = smooth

    # Macro height: each land zone's distance from the nearest water, through land.
    dist = [[math.inf] * cols for _ in range(rows)]
    q = deque()
    for r in range(rows):
        for c in range(cols):
            if BIOMES[grid[r][c]]["water"]:
                dist[r][c] = 0
                q.append((r, c))
    while q:
        r, c = q.popleft()
        for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nr, nc = r + dr, c + dc
            if 0 <= nr < rows and 0 <= nc < cols and dist[nr][nc] == math.inf:
                dist[nr][nc] = dist[r][c] + 1
                q.append((nr, nc))
    heights = [[0] * cols for _ in range(rows)]
    for r in range(rows):
        for c in range(cols):
            b = BIOMES[grid[r][c]]
            if b["water"]:
                continue
            h = min(b["max"], b["base"] + dist[r][c] * ZONE * b["slope"])
            heights[r][c] = min(len(DIGITS) - 1, round(h * 2))

    layout = {
        "_comment": "R'th's world layout (docs/world-building.md). One letter and one height digit per 8 x 8 tile zone, "
                    "rows from north to south. Heights are base-36 digits in half tiles (0-z = 0-17.5). Bootstrapped from "
                    "images/map.png by tools/world-layout/layout_from_sketch.py, then edited by hand.",
        "zoneTiles": ZONE,
        "originX": ox,
        "originY": oy,
        "tilesPerSketchSquare": args.tiles_per_square,
        "seaLevel": 0,
        "biomes": {k: {kk: v[kk] for kk in ("name", "ground", "water", "walkable", "rough")} for k, v in BIOMES.items()},
        "zones": ["".join(grid[r]) for r in reversed(range(rows))],
        "heights": ["".join(DIGITS[heights[r][c]] for c in range(cols)) for r in reversed(range(rows))],
    }
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(layout, f, indent=1)
        f.write("\n")

    # Preview: 3 px per zone, region grid every 96 tiles, today's regions outlined.
    s = 3
    prev = Image.new("RGB", (cols * s, rows * s))
    d = ImageDraw.Draw(prev)
    for r in range(rows):
        for c in range(cols):
            base = PREVIEW_COLOURS[grid[r][c]]
            shade = 1 + 0.05 * (heights[r][c] / 2 - 2)
            col = tuple(max(0, min(255, int(v * shade))) for v in base)
            y = (rows - 1 - r) * s
            d.rectangle([c * s, y, c * s + s - 1, y + s - 1], fill=col)
    for gx in range(ox - ox % 96, ox + cols * ZONE, 96):
        x = (gx - ox) / ZONE * s
        d.line([(x, 0), (x, rows * s)], fill=(0, 0, 0))
    for gy in range(oy - oy % 96, oy + rows * ZONE, 96):
        y = rows * s - (gy - oy) / ZONE * s
        d.line([(0, y), (cols * s, y)], fill=(0, 0, 0))
    for name, (x0, y0, x1, y1) in {"Tolling": (0, 0, 96, 96), "Palmerway": (96, 0, 192, 96), "Scale test": (0, 96, 96, 192)}.items():
        a = ((x0 - ox) / ZONE * s, rows * s - (y1 - oy) / ZONE * s)
        b = ((x1 - ox) / ZONE * s, rows * s - (y0 - oy) / ZONE * s)
        d.rectangle([a, b], outline=(255, 230, 60), width=2)
        d.text((a[0] + 3, a[1] + 2), name, fill=(255, 230, 60))
    os.makedirs(os.path.dirname(args.preview), exist_ok=True)
    prev.save(args.preview)

    counts = Counter(ch for row in grid for ch in row)
    land = sum(n for k, n in counts.items() if not BIOMES[k]["water"])
    print("zones %d x %d from tile (%d, %d); %d land zones = %.0f regions of 96 x 96" %
          (cols, rows, ox, oy, land, land * ZONE * ZONE / 9216))
    for k, n in counts.most_common():
        print("  %s %-9s %5d zones" % (k, BIOMES[k]["name"], n))


if __name__ == "__main__":
    main()
