"""Painting building textures: small seeded pixel painters shared by the building kits' texture
scripts (cruor/textures.py) and the lab's (lab/paint_textures.py, lab/church_textures.py).
Standard library only, so the scripts also run outside Blender. Colours are palette names
(client/Assets/Resources/Art/palette.json). 128 pixels wide, point filtered in use."""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "art"))
from surface_textures import write_png  # noqa: E402

W = 128

with open(os.path.join(ROOT, "client", "Assets", "Resources", "Art", "palette.json"), encoding="utf-8") as f:
    PALETTE = {c["name"]: c["hex"] for c in json.load(f)["colours"]}


def col(name):
    h = PALETTE[name].lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def shade(c, k):
    return tuple(max(0.0, min(1.0, v * k)) for v in c)


class Image:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [(1.0, 1.0, 1.0)] * (w * h)

    def set(self, x, y, c):
        if 0 <= y < self.h:
            self.px[y * self.w + (x % self.w)] = c

    def save(self, folder, name):
        os.makedirs(folder, exist_ok=True)
        write_png(os.path.join(folder, name + ".png"), self.px, self.w, self.h)


def roof_rows(seed, ramp, run0, run1, rows_per_tile=5, unit_w=(16, 22), joint=0.55):
    """Roof tiles or slates over run0..run1 (v = 0 at run0), rows placed by run so they line up
    from piece to piece. Each tile is one of the ramp's colours, a little lighter along its top,
    with a thin dark line under it (the next row's edge) and between it and its neighbour."""
    base = [col(n) for n in ramp]
    img = Image(W, 128)
    rows = {}
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        run = run0 + (run1 - run0) * v
        row = math.floor(run * rows_per_tile)
        phase = (run * rows_per_tile) % 1.0                      # 0 at a row's lower edge
        if row not in rows:
            r = random.Random(seed * 1009 + row * 7919)
            widths = []
            while sum(widths) < W:
                widths.append(r.randint(*unit_w))
            widths[-1] -= sum(widths) - W                       # rows tile across the piece
            if widths[-1] < unit_w[0] // 2:
                last = widths.pop()                             # (pop first: the index is read before it)
                widths[-1] += last
            tiles = [mix(base[1], r.choice(base), r.uniform(0.0, 0.35)) for _ in widths]   # close to the middle
            rows[row] = (r.randint(0, W - 1), widths, tiles)
        x0, widths, tiles = rows[row]
        for x in range(W):
            xs = (x - x0) % W
            acc, k = 0, 0
            while acc + widths[k] <= xs:
                acc += widths[k]
                k += 1
            c = tiles[k]
            if phase < 0.11:
                c = shade(c, joint)                              # the shadow of the row above
            elif xs - acc == 0:
                c = shade(c, 0.7)                                # the joint beside it
            elif phase > 0.8:
                c = shade(c, 1.1)                                # light along its top
            img.set(x, y, c)
    return img


def ashlar(seed, ramp, courses, blocks, joint=0.72, lit=1.06, spread=0.4):
    """Square tile of dressed stone or brick: `courses` rows of `blocks` blocks, every other row
    offset by half a block (so it repeats), thin joints, each block a shade of the ramp: close to
    its middle (spread 0.4, quiet), or anywhere along it (spread 1.0, strong contrast)."""
    base = [col(n) for n in ramp]
    r = random.Random(seed)
    img = Image(W, W)
    ch, bw = W / courses, W / blocks
    shades = {(c, b): mix(base[1], r.choice(base), r.uniform(0.0, spread)) for c in range(courses) for b in range(blocks)}
    mortar = shade(mix(base[0], base[-1], 0.5), joint)
    for y in range(W):
        c = int(y / ch)
        yy = y - c * ch
        off = bw / 2 if c % 2 else 0
        for x in range(W):
            xx = (x + off) % W
            b = int(xx / bw)
            ix = xx - b * bw
            colour = shades[(c, b)]
            if yy < 1.5 or ix < 1.5:
                colour = mortar
            elif yy < 3.5 or ix < 3.5:
                colour = shade(colour, lit)
            img.set(x, W - 1 - y, colour)
    return img


def plaster_wall(seed, ramp):
    """One wall tile of limewashed plaster (128 x 256, a storey): nearly even, a faint trowel
    texture, slightly cooler toward the top under the rail. Clean, not stained."""
    base = [col(n) for n in ramp]
    r = random.Random(seed)
    img = Image(W, 256)
    for y in range(img.h):
        v = 1 - y / (img.h - 1)
        for x in range(W):
            c = base[1]
            n = r.random()
            if n < 0.08:
                c = base[0]
            elif n > 0.93:
                c = base[2]
            if v > 0.94:
                c = mix(c, base[0], 0.5)
            img.set(x, y, c)
    return img


def glass(seed, ramp, lead, pitch=32, width=2):
    """Leaded glass in diamond quarries: lead lines on both diagonals every `pitch` pixels; each
    diamond one of the ramp's shades, a little lighter toward its top."""
    base = [col(n) for n in ramp]
    dark = col(lead)
    r = random.Random(seed)
    img = Image(W, W)
    panes = {}
    for y in range(W):
        for x in range(W):
            a, b = (x + y) % W, (x - y) % W
            if a % pitch < width or b % pitch < width:
                img.set(x, y, dark)
                continue
            key = (a // pitch, b // pitch)
            if key not in panes:
                panes[key] = mix(base[1], r.choice(base), r.uniform(0.0, 0.5))
            c = panes[key]
            if (a % pitch) + (b % pitch) < pitch * 0.55:
                c = shade(c, 1.12)
            img.set(x, y, c)
    return img
