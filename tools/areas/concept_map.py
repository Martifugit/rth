"""
Draws docs/lore-rework/concept-map.png: a schematic of the continent from the board
(docs/lore-rework/powers.md). Positions are rough; it is a concept, not a layout.

    python3 tools/areas/concept_map.py

Needs numpy and Pillow. One pixel is TILES tiles.
"""
import math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'docs', 'lore-rework', 'concept-map.png')
W, H, TILES = 1100, 950, 1.7
rng = np.random.default_rng(7)
yy, xx = np.mgrid[0:H, 0:W].astype(float)

def noise(scale, octaves=4):
    out = np.zeros((H, W))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        s = scale / 2 ** o
        gh, gw = int(H / s) + 2, int(W / s) + 2
        g = rng.standard_normal((gh, gw))
        gy, gx = yy / s, xx / s
        y0, x0 = gy.astype(int), gx.astype(int)
        fy, fx = gy - y0, gx - x0
        fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
        a = g[y0, x0] * (1 - fx) + g[y0, x0 + 1] * fx
        b = g[y0 + 1, x0] * (1 - fx) + g[y0 + 1, x0 + 1] * fx
        out += amp * (a * (1 - fy) + b * fy); total += amp; amp *= 0.5
    return out / total

def dist_to_polyline(pts):
    d = np.full((H, W), 1e9)
    for (x1, y1), (x2, y2) in zip(pts, pts[1:]):
        dx, dy = x2 - x1, y2 - y1
        t = np.clip(((xx - x1) * dx + (yy - y1) * dy) / (dx * dx + dy * dy), 0, 1)
        d = np.minimum(d, np.hypot(xx - (x1 + t * dx), yy - (y1 + t * dy)))
    return d

# ---- land: one big continent, continuing off the west edge (beyond the spine) and into the cold north
n1, n2 = noise(160), noise(60)
cx, cy = 600, 500
shape = 1 - ((xx - cx) / 470) ** 2 - ((yy - cy) / 420) ** 2
shape = np.maximum(shape, 0.55 - ((yy - 470) / 430) ** 2 - np.clip((xx - 260) / 260, 0, None) ** 2 * 3)  # west, off-map
shape = np.maximum(shape, 0.35 - ((xx - 520) / 520) ** 2 - np.clip((yy - 110) / 110, 0, None) ** 2 * 3)  # north, off-map
land = shape + 0.32 * n1 + 0.12 * n2 > 0.05
# southern isles
for ix, iy, r in [(560, 905, 28), (650, 920, 18), (470, 915, 14), (1010, 640, 22), (1040, 560, 14)]:
    land |= np.hypot(xx - ix, yy - iy) + 12 * n2 < r

# ---- the spine: from the warm south-west up into the cold north
spine = [(300, 950), (330, 820), (300, 690), (345, 540), (320, 400), (360, 260), (330, 130), (380, 0)]
d_spine = dist_to_polyline(spine) + 10 * n2
width = 34 + np.clip((500 - yy) / 500, 0, 1) * 22          # wider in the north
range_mask = land & (d_spine < width)
cold = yy + 60 * n1 < 170

# ---- territories: warped nearest-seed, then the range and the salients on top
seeds = {
    'empire': [(620, 450), (720, 380), (560, 560), (700, 560), (520, 330), (800, 470), (640, 300)],
    'gall': [(830, 760), (720, 790), (900, 680)],
    'houses': [(860, 230), (950, 330), (780, 160)],
    'sere': [(440, 700), (470, 820), (420, 580)],
    'frontier_w': [(120, 450), (150, 750), (130, 200)],
    'north': [(560, 60), (760, 50), (450, 90)],
}
wx, wy = xx + 70 * noise(120), yy + 70 * noise(120)
names = list(seeds)
best = np.full((H, W), 1e9); terr = np.zeros((H, W), int)
for i, k in enumerate(names):
    for sx, sy in seeds[k]:
        d = np.hypot(wx - sx, wy - sy)
        m = d < best; best[m] = d[m]; terr[m] = i
idx = {k: i for i, k in enumerate(names)}
terr[(xx < np.interp(yy, [p[1] for p in spine[::-1]], [p[0] for p in spine[::-1]]) - 20)] = idx['frontier_w']
RANGE = len(names); terr[range_mask] = RANGE
terr[cold & ~range_mask & (xx > 330)] = idx['north']

def poly_mask(pts):
    im = Image.new('L', (W, H), 0); ImageDraw.Draw(im).polygon(pts, fill=1); return np.array(im, bool)
salients = {
    'up Sere\'s river': [(470, 600), (520, 560), (560, 600), (470, 690), (430, 700), (430, 670)],
    'into the foothills': [(470, 400), (505, 380), (420, 330), (395, 345)],
    'a fortress pocket': [(770, 700), (805, 690), (815, 725), (780, 735)],
    'a coastal enclave': [(900, 380), (955, 360), (990, 410), (930, 425)],
}
for pts in salients.values():
    terr[poly_mask(pts) & land] = idx['empire']

COL = {'empire': (150, 52, 52), 'gall': (214, 120, 40), 'houses': (110, 78, 128), 'sere': (206, 176, 112),
       'frontier_w': (120, 128, 112), 'north': (214, 226, 236)}
img = np.zeros((H, W, 3))
img[:] = (34, 58, 92)                                     # sea
for k, c in COL.items(): img[land & (terr == idx[k])] = c
img[range_mask] = (128, 120, 112)
img[range_mask & cold] = (200, 205, 215)
shade = (0.85 + 0.15 * n2)[..., None]
img = np.where(land[..., None], img * shade, img)
# fog over the frontiers: the world grows there later
fog = np.clip((90 - xx) / 90, 0, 1) + np.clip((40 - yy) / 40, 0, 1)
img = img * (1 - 0.55 * np.clip(fog, 0, 1)[..., None]) + 200 * 0.55 * np.clip(fog, 0, 1)[..., None]
# borders between territories
edge = np.zeros((H, W), bool)
edge[1:, :] |= terr[1:, :] != terr[:-1, :]; edge[:, 1:] |= terr[:, 1:] != terr[:, :-1]
img[edge & land] = img[edge & land] * 0.45
coast = land ^ np.roll(land, 1, 0) | land ^ np.roll(land, 1, 1)
img[coast] = (20, 26, 34)

out = Image.fromarray(img.clip(0, 255).astype(np.uint8))
d = ImageDraw.Draw(out)
F = lambda s, b=True: ImageFont.truetype('DejaVuSerif-Bold.ttf' if b else 'DejaVuSerif.ttf', s)
def text(x, y, s, size=16, fill=(255, 255, 255), b=True):
    d.text((x, y), s, font=F(size, b), fill=fill, anchor='mm', stroke_width=3, stroke_fill=(0, 0, 0))

# mountains along the spine
for t in np.linspace(0, 1, 70):
    i = min(int(t * (len(spine) - 1)), len(spine) - 2); f = t * (len(spine) - 1) - i
    x = spine[i][0] + (spine[i + 1][0] - spine[i][0]) * f; y = spine[i][1] + (spine[i + 1][1] - spine[i][1]) * f
    for off in (-14, 8):
        s = 12 + (8 if y < 400 else 0)
        px, py = x + off + rng.normal(0, 4), y + rng.normal(0, 6)
        if 0 < px < W and 0 < py < H and land[int(py), int(px)]:
            d.polygon([(px - s, py + s * 0.7), (px, py - s), (px + s, py + s * 0.7)], fill=(170, 165, 160) if py > 170 else (240, 244, 250), outline=(40, 40, 40))
# rivers
rivers = [[(380, 470), (450, 470), (530, 430), (600, 450), (700, 420), (820, 440), (930, 470), (1000, 480)],   # the great river
          [(370, 330), (450, 320), (560, 350), (620, 400)],
          [(370, 600), (430, 640), (490, 620), (560, 580), (620, 520)],                                          # Sere's river, into the empire
          [(650, 600), (700, 680), (690, 760), (720, 860)]]
for r in rivers: d.line(r, fill=(70, 130, 200), width=4, joint='curve')
# features
def blob(x, y, r, c, n=9):
    for _ in range(n):
        ox, oy = rng.normal(0, r / 2, 2); d.ellipse([x + ox - r / 3, y + oy - r / 3, x + ox + r / 3, y + oy + r / 3], fill=c)
blob(700, 300, 50, (60, 100, 56), 16); text(700, 300, 'p-forest', 13, b=False)               # a great forest
blob(250, 520, 60, (70, 96, 60), 14)                                                          # the frontier woods
blob(880, 260, 55, (82, 98, 82), 14); text(880, 268, 'fens', 13, b=False)
d.ellipse([560, 500, 640, 540], fill=(52, 92, 140)); text(600, 520, 'p-lake', 12, b=False)   # a lake, not a bay
for vx, vy in [(860, 790), (900, 740), (800, 830)]:
    d.polygon([(vx - 13, vy + 9), (vx, vy - 13), (vx + 13, vy + 9)], fill=(110, 50, 40), outline=(30, 10, 10)); d.ellipse([vx - 3, vy - 16, vx + 3, vy - 10], fill=(255, 120, 40))
for i in range(40):
    x, y = rng.uniform(400, 520), rng.uniform(720, 860)
    if land[int(y), int(x)] and terr[int(y), int(x)] == idx['sere']: d.arc([x - 7, y - 4, x + 7, y + 4], 200, 340, fill=(160, 130, 80), width=2)
d.ellipse([318, 365, 348, 395], outline=(170, 230, 210), width=3); text(333, 410, 'Selenfall', 12)   # moonfall scar on the spine
d.rectangle([770, 540, 790, 575], fill=(110, 105, 100), outline=(20, 20, 20)); d.line([(780, 575), (780, 600)], fill=(160, 200, 255), width=4)
text(780, 525, 'p-head (the falls)', 12)
for x, y in [(520, 760), (750, 200), (990, 520), (430, 250), (640, 690)]:
    d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=(255, 230, 120), outline=(0, 0, 0)); text(x, y + 14, 'p-demigod', 10, b=False)
# places
def place(x, y, name, size=15, cap=False):
    r = 8 if cap else 5
    d.rectangle([x - r, y - r, x + r, y + r], fill=(250, 240, 220), outline=(0, 0, 0), width=2)
    text(x, y - r - 12, name, size)
place(640, 430, 'Cruor', 19, True); place(455, 520, 'Tolling'); place(395, 300, 'Meridian', 15, True)
place(372, 95, 'Frostpeak Forge', 14, True); place(450, 760, 'Cistern', 15, True); place(850, 720, 'p-gall-seat', 15, True)
place(940, 300, 'p-houses', 13); place(345, 700, 'p-dwarf-hold', 12)
for x, y in [(600, 380), (820, 560), (520, 360)]:
    d.ellipse([x - 4, y - 4, x + 4, y + 4], outline=(255, 255, 255), width=2)   # hidden occult cells inside the empire
# salient labels
for (k, pts), (lx, ly) in zip(salients.items(), [(495, 655), (440, 320), (790, 755), (950, 440)]):
    text(lx, ly, k, 11, (255, 210, 210), b=False)
# realm labels
text(690, 470, "CRUOR'S EMPIRE", 26, (255, 225, 210)); text(810, 650, 'HOUSE GALL', 22, (255, 220, 170))
text(900, 200, 'THE LESSER HOUSES', 16, (230, 210, 245)); text(470, 840, 'SERE', 22, (255, 240, 200))
text(322, 585, 'THE', 15, (235, 235, 240)); text(322, 603, 'SPINE', 15, (235, 235, 240)); text(600, 120, 'THE COLD NORTH', 20, (230, 240, 255))
text(140, 450, 'BEYOND', 18, (235, 235, 225)); text(140, 472, 'THE SPINE', 18, (235, 235, 225))
# frontier arrows: the world grows here
for (x1, y1, x2, y2, s) in [(110, 600, 30, 600, 'frontier'), (600, 70, 600, 15, 'frontier'), (560, 880, 560, 935, 'isles'), (1015, 600, 1085, 600, 'isles')]:
    d.line([(x1, y1), (x2, y2)], fill=(255, 255, 255), width=3)
    a = math.atan2(y2 - y1, x2 - x1)
    d.polygon([(x2, y2), (x2 - 12 * math.cos(a - 0.5), y2 - 12 * math.sin(a - 0.5)), (x2 - 12 * math.cos(a + 0.5), y2 - 12 * math.sin(a + 0.5))], fill=(255, 255, 255))
# legend and scale
d.rectangle([W - 300, H - 190, W - 10, H - 10], fill=(18, 20, 26), outline=(90, 90, 90))
items = [(COL['empire'], "Cruor's empire (worships p-sworn)"), (COL['gall'], 'House Gall'), (COL['houses'], 'the lesser occult houses'),
         (COL['sere'], 'Sere'), ((128, 120, 112), 'the spine (surface people, dwarves)'), (COL['frontier_w'], 'frontiers (the world grows here)')]
for i, (c, s) in enumerate(items):
    y = H - 180 + i * 22; d.rectangle([W - 290, y, W - 272, y + 14], fill=c, outline=(0, 0, 0)); d.text((W - 264, y - 1), s, font=F(13, False), fill=(230, 230, 230))
y = H - 40; bar = 500 / TILES
d.ellipse([W - 290, y - 8, W - 280, y + 2], outline=(255, 255, 255), width=2); d.text((W - 274, y - 12), 'hidden occult cell', font=F(12, False), fill=(230, 230, 230))
d.line([(20, H - 22), (20 + bar, H - 22)], fill=(255, 255, 255), width=5); d.text((28 + bar, H - 32), '500 tiles', font=F(14), fill=(255, 255, 255))
text(W / 2, 20, "R'th: concept map (from the board, docs/lore-rework/powers.md)", 18)
out.save(OUT)
land_tiles = land[100:, 100:].sum() * TILES ** 2
print('saved', OUT, f'land in view (excluding fogged edges) ~{land_tiles/1e6:.2f}M tiles')
core = land & (terr != idx['frontier_w']) & (terr != idx['north'])
total = core.sum()
for k in ['empire', 'gall', 'houses', 'sere']: print(f"{k:8s} {(core & (terr == idx[k])).sum() / total:.0%}")
print(f"range    {(core & (terr == RANGE)).sum() / total:.0%}")
print(f"core land {total * TILES ** 2 / 1e6:.2f}M tiles")
