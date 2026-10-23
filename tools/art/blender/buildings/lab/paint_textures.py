#!/usr/bin/env python3
"""
The paint lab's textures: experiments in painting building pieces so they look painted rather than
flat, in the spirit of the references (not copying them). Unlike tools/art/surface_textures.py
(a grey pattern tinted by one colour, repeating every tile), these are:

  - painted in several palette colours (warm lights, cool shades), each surface a small ramp;
  - painted for one whole piece: a wall tile's texture covers the tile, a roof piece's covers its
    run, so they can carry shading at the scale of a building (darker at the foot of a wall and
    under its rail, a roof's courses continuing from eave to ridge);
  - bold: few, big shapes, so they survive the game camera's distance.

    python3 tools/art/blender/buildings/lab/paint_textures.py

Writes PNGs beside this script (textures/). Standard library only; seeded, so repeatable.
"""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "art"))
from surface_textures import write_png  # noqa: E402

OUT = os.path.join(HERE, "textures")
W = 128
OVERHANG = 0.35            # the eave piece's run starts this far out (matches the kit)
COURSES = 3                # thatch courses per tile of run
SLATE_ROWS = 4             # slate rows per tile of run

with open(os.path.join(ROOT, "client", "Assets", "Resources", "Art", "palette.json"), encoding="utf-8") as f:
    PALETTE = {c["name"]: c["hex"] for c in json.load(f)["colours"]}


def col(name):
    h = PALETTE[name].lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def ramp(names, t):
    """Pick from a ramp of palette colours (dark to light) by t in 0-1: stepped, like pixel art."""
    i = max(0, min(len(names) - 1, int(t * len(names))))
    return col(names[i])


def noise(rnd, cells_x, cells_y):
    """Smooth value noise, tiling across x (period = the image width); returns f(u, v) in 0-1."""
    grid = [[rnd.random() for _ in range(cells_x)] for _ in range(cells_y + 1)]

    def f(u, v):
        x = (u % 1.0) * cells_x
        y = max(0.0, min(0.9999, v)) * cells_y
        x0, y0 = int(x), int(y)
        tx, ty = x - x0, y - y0
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        a = grid[y0][x0 % cells_x] + (grid[y0][(x0 + 1) % cells_x] - grid[y0][x0 % cells_x]) * tx
        b = grid[y0 + 1][x0 % cells_x] + (grid[y0 + 1][(x0 + 1) % cells_x] - grid[y0 + 1][x0 % cells_x]) * tx
        return a + (b - a) * ty
    return f


class Image:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [(1.0, 1.0, 1.0)] * (w * h)

    def set(self, x, y, c):
        if 0 <= y < self.h:
            self.px[y * self.w + (x % self.w)] = c

    def get(self, x, y):
        return self.px[y * self.w + (x % self.w)]

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        write_png(os.path.join(OUT, name + ".png"), self.px, self.w, self.h)


# ---- a wall tile: plaster, painted for the whole tile ----

def plaster_wall(rnd):
    """One wall tile's plaster (128 x 256, v = 0 at the plinth, 1 at the top rail). Warm light in
    the middle, cooler shade toward the posts and under the rail, rising damp along the foot,
    a few cracks. Ramp: Wool (shade) -> Linen -> Plaster -> CandleWax (light)."""
    img = Image(W, 256)
    big = noise(rnd, 3, 4)
    small = noise(rnd, 9, 14)
    damp_edge = noise(rnd, 7, 1)
    names = ["Shingle", "Wool", "Linen", "Plaster", "CandleWax"]
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        for x in range(W):
            u = x / W
            side = min(u, 1 - u)                                   # 0 at the posts, 0.5 mid-tile
            t = 0.66 + 0.16 * (big(u, v) - 0.5) + 0.08 * (small(u, v) - 0.5)
            t -= 0.35 * max(0.0, 0.14 - side) / 0.14               # shade into the posts
            t -= 0.30 * max(0.0, v - 0.9) / 0.1                    # shade under the top rail
            damp = 0.16 + 0.06 * damp_edge(u, 0)
            if v < damp:
                t = 0.18 + 0.15 * (v / damp) + 0.08 * (small(u, v) - 0.5)
            img.set(x, y, ramp(names, t))
    for _ in range(3):                                             # cracks
        x, y = rnd.randrange(W), rnd.randrange(40, 200)
        for k in range(rnd.randint(10, 26)):
            img.set(x, y + k, col("Wool"))
            x += rnd.choice((-1, 0, 0, 1))
    return img


# ---- thatch: courses that continue across eave, slope and ridge ----

def thatch(rnd, run0, run1, eave=False):
    """A thatch roof piece covering run0..run1 tiles of run (v = 0 at run0). Courses are placed by
    run, so they line up from piece to piece. Each course: the ragged lower edge in shadow, a
    sunlit body with straw streaks, the top tucked under the course above. eave: the bottom
    course is the thick eave, darker, with a shadowed lip."""
    img = Image(W, 128)
    streak = [rnd.random() for _ in range(W)]
    ragged = [rnd.random() for _ in range(W)]
    light = noise(rnd, 4, 3)
    body = ["Furrow", "Ochre", "Thatch", "Wheat"]
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        run = run0 + (run1 - run0) * v
        for x in range(W):
            u = x / W
            phase = (run * COURSES) % 1.0                           # 0 = a course's lower edge
            edge = 0.08 + 0.14 * ragged[x]                          # each strand ends at its own length
            if phase < edge:
                c = col("Mud") if phase < edge * 0.5 else col("Furrow")
            elif phase > 0.86:
                c = col("Furrow")                                   # tucked under the course above
            else:
                t = 0.45 + 0.35 * math.sin((phase - edge) / (0.86 - edge) * math.pi)
                t += 0.25 * (streak[x] - 0.5) + 0.15 * (light(u, run / 3) - 0.5)
                c = ramp(body, t)
            if eave and run < 0.0:
                c = mix(c, col("Mud"), 0.25 + 0.35 * min(1.0, -run / OVERHANG))   # the eave in shade
            img.set(x, y, c)
    return img


def thatch_ridge(rnd):
    """The ridge, painted rather than modelled: a pale capping band with crossed hazel liggers
    and its lower edge cut in points, over the top course."""
    img = Image(W, 128)
    base = thatch(rnd, 0.0, 1.0)
    img.px = list(base.px)
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        for x in range(W):
            point = abs(((x / W) * 4) % 1.0 - 0.5) * 2              # four points per tile
            cap_bottom = 0.55 + 0.2 * (1 - point)
            if v > cap_bottom:
                t = 0.55 + 0.3 * (v - cap_bottom) / (1 - cap_bottom)
                c = ramp(["Ochre", "Thatch", "Wheat", "Wheat"], t + 0.1 * (rnd.random() - 0.5))
                if abs(v - cap_bottom) < 0.03:
                    c = col("Furrow")                               # the cut edge's shadow
                lig = (x + y) % 32 < 2 or (x - y) % 32 < 2         # crossed liggers
                if lig and v > cap_bottom + 0.06:
                    c = col("Bark")
                img.set(x, y, c)
    return img


# ---- slate: rows that continue across eave, slope and ridge ----

def slate(rnd, run0, run1, eave=False):
    """A slate roof piece over run0..run1: rows of slates (placed by run, so they line up), each
    slate its own shade, lit along its top, shadowed at its foot; moss on a few lower edges."""
    img = Image(W, 128)
    rows = {}
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        run = run0 + (run1 - run0) * v
        row = math.floor(run * SLATE_ROWS)
        phase = (run * SLATE_ROWS) % 1.0
        if row not in rows:
            r = random.Random(row * 7919 + 13)
            widths, x = [], r.randint(0, 20)
            while sum(widths) < W:
                widths.append(r.randint(22, 34))
            shades = [mix(col("Slate"), col(r.choice(("Slate", "Slate", "Indigo", "Steel"))), r.uniform(0.15, 0.35))
                      for _ in widths]                                # close shades of one slate
            rows[row] = (x, widths, shades, [r.random() < 0.2 for _ in widths])
        x0, widths, shades, moss = rows[row]
        for x in range(W):
            xs = (x - x0) % W
            acc, k = 0, 0
            while acc + widths[k] <= xs:
                acc += widths[k]
                k += 1
                k %= len(widths)
            if xs - acc == 0:
                c = col("Sable")                                   # the joint
            elif phase < 0.12:
                c = col("Sable") if phase < 0.05 else col("Indigo")    # the next row's shadow
            elif phase > 0.85:
                c = mix(shades[k], col("Steel"), 0.3)               # lit top edge
            else:
                c = shades[k]
            if moss[k] and 0.05 < phase < 0.35 and (x * 31 + row * 17) % 7 < 4:
                c = col("Moss") if (x + int(phase * 40)) % 3 else col("MossyStone")
            if eave and run < 0.0:
                c = mix(c, col("Sable"), 0.3 * min(1.0, -run / OVERHANG))
            img.set(x, y, c)
    return img


def slate_ridge(rnd):
    """Rounded ridge tiles over the top row: a lit band along the crest."""
    img = Image(W, 128)
    base = slate(rnd, 0.0, 1.0)
    img.px = list(base.px)
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        if v > 0.6:
            for x in range(W):
                t = (v - 0.6) / 0.4
                c = col("Steel") if t > 0.55 else col("Slate")
                if x % 32 == 0:
                    c = col("Sable")                                # where ridge tiles meet
                if t < 0.08:
                    c = col("Indigo")
                img.set(x, y, c)
    return img


def main():
    rnd = random.Random
    plaster_wall(rnd(1)).save("PaintWall")
    thatch(rnd(2), -OVERHANG, 1.0, eave=True).save("PaintThatchEave")
    thatch(rnd(2), 0.0, 1.0).save("PaintThatchSlope")
    thatch_ridge(rnd(2)).save("PaintThatchRidge")
    slate(rnd(3), -OVERHANG, 1.0, eave=True).save("PaintSlateEave")
    slate(rnd(3), 0.0, 1.0).save("PaintSlateSlope")
    slate_ridge(rnd(3)).save("PaintSlateRidge")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
