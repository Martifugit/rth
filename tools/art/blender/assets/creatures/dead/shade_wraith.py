r"""The wraith: the shades of the long way back, the drifting dead who didn't make the walk. They can't be
fought, and they strike those who come close. One who has been dead so long it has forgotten its shape:
stretched and hunched, its back a high arch, its trail of soul-stuff flowing out behind it, lifting and
dipping over the water; a long neck carries a shaped mask (a frowning brow, cheek planes, two slanted
eye slits and a long crying mouth, all void); two arms much too long, folding at three joints, reach
forward and down to three long fingers, torn soul-cloth hanging from them like sleeves.

What must read, in order: the high hunched arch and the flowing trail; the masked face; the reaching,
draped arms. About 1.9 m to the hump, 3.2 m from the trail's end to the fingers. Grown as one mesh
(artlib/grow.py): the body a tube from the trail to the head, the arms extruded from the shoulders,
the fingers out of the hand's split end; the mask and the sleeves are closed sheets of quads sunk
into the head and the arms. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\shade_wraith.py")
"""
import math
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

from mathutils import Vector  # noqa: E402

from artlib import family  # noqa: E402
from artlib.grow import P, Grow  # noqa: E402

COLOURS = ["SoulPale", "SoulGlow", "Sable"]
PALE, GLOW, VOID = range(3)

HUMP = 1.9                      # the arch's top; the shoulders, neck and face hang from it
FACE = (0.62, HUMP - 0.16)      # (f, u) the face's middle, thrust forward and low
MASK_TILT = 18                  # degrees the mask turns up from straight ahead, toward the game camera
MASK_R = (0.16, 0.27)           # the mask's half width and half height
MASK_GRID = (14, 22)            # quads across and down the mask

# the arm's middle line for the +X side (f, s, u, half-width, above, below): shoulder, three joints, wrist, hand
ARM = [
    (0.04, 0.4, HUMP - 0.14, 0.08, 0.09, 0.09),      # the shoulder's knob
    (0.14, 0.5, HUMP - 0.08, 0.05, 0.055, 0.055),
    (0.24, 0.62, HUMP - 0.04, 0.075, 0.075, 0.075),  # the first joint, level with the hump
    (0.4, 0.68, HUMP - 0.3, 0.045, 0.05, 0.05),
    (0.52, 0.72, HUMP - 0.52, 0.07, 0.07, 0.07),     # the second, forward and down
    (0.72, 0.66, HUMP - 0.6, 0.042, 0.046, 0.046),
    (0.9, 0.58, HUMP - 0.66, 0.065, 0.065, 0.065),   # the third
    (0.98, 0.52, HUMP - 0.9, 0.04, 0.044, 0.044),
    (1.04, 0.45, HUMP - 1.1, 0.05, 0.055, 0.05),     # the wrist, hanging
    (1.07, 0.44, HUMP - 1.2, 0.08, 0.035, 0.035),    # the hand, flat and wide
]
SLEEVE = (2, 7)                 # the sleeve hangs from ARM[2] (the first joint) to ARM[7], under the third
SLEEVE_COLS = 11                # points along it (odd: the ends are long tatters)
SLEEVE_DROP = (0.3, 0.72)       # how far it hangs at its ends and at its middle


def mask(bm, centre, tilt, r, grid):
    """The face: a closed curved plate of quads. Its outline is a shield, rounded over the brow and
    narrowing to a long chin; the front rises in a frowning brow ridge and two cheek planes; two eye
    slits under the brow and a long mouth sink deep into it, void. The back skin is sunk into the head."""
    cf, cs, cu = centre
    t = math.radians(tilt)
    fwd = (math.cos(t), math.sin(t))                 # (f, u) the way the face looks
    upw = (-math.sin(t), math.cos(t))                # (f, u) up the face
    rx, ry = r
    nx, ny = grid

    def at(x, y, d):
        return P(cf + fwd[0] * d + upw[0] * y, cs + x, cu + fwd[1] * d + upw[1] * y)

    def half_width(y):
        yn = y / ry
        round_top = math.sqrt(max(0.0, 1 - yn * yn)) if yn > 0 else 1.0
        return rx * max(0.22, min(round_top, 0.28 + 0.72 * (yn + 1) / 1.3))

    def wrap(x, y):                                  # curving back round the skull, most over the brow
        return 1.6 * x * x + 0.3 * y * y + 1.2 * max(0.0, y) ** 2

    def back(x, y):
        return -0.035 - wrap(x, y)

    def brow_y(x):
        return 0.055 + 0.3 * abs(x)                  # lower in the middle: a frown

    def front(x, y):
        d = 0.05 - wrap(x, y)
        d += 0.032 * math.exp(-((y - brow_y(x)) / 0.032) ** 2) * (1 if abs(x) < 0.15 else 0.5)
        for side in (1, -1):                         # the cheek planes, high under the eyes
            d += 0.022 * math.exp(-(((x - side * 0.085) / 0.045) ** 2 + ((y + 0.05) / 0.05) ** 2))
        return d

    def void(x, y):
        ax = abs(x)
        if 0.022 < ax < 0.125:                       # the eye slits, slanted down to the nose
            along = (ax - 0.0735) / 0.0515
            if abs(y - (brow_y(x) - 0.052)) < 0.024 * math.sqrt(max(0.0, 1 - along * along)):
                return True
        if -0.235 < y < -0.06:                       # the mouth, long, widest at its middle
            k = (y + 0.1475) / 0.0875
            if ax < 0.012 + 0.03 * math.sqrt(max(0.0, 1 - k * k)):
                return True
        return False

    F, B, dark = [], [], set()
    for j in range(ny + 1):
        y = -ry + 2 * ry * j / ny
        hw = half_width(y)
        rf, rb = [], []
        for i in range(nx + 1):
            x = hw * (2 * i / nx - 1)
            d = front(x, y)
            if 0 < i < nx and 0 < j < ny and void(x, y):
                d = back(x, y) + 0.02
                dark.add((j, i))
            rf.append(bm.verts.new(at(x, y, d)))
            rb.append(bm.verts.new(at(x, y, back(x, y))))
        F.append(rf)
        B.append(rb)
    for j in range(ny):
        for i in range(nx):
            corners = ((j, i), (j, i + 1), (j + 1, i + 1), (j + 1, i))
            bm.faces.new([F[a][b] for a, b in corners]).material_index = (
                VOID if any(c in dark for c in corners) else GLOW)
            bm.faces.new([B[a][b] for a, b in reversed(corners)]).material_index = PALE
    rim = [(0, i) for i in range(nx)] + [(j, nx) for j in range(ny)] + \
          [(ny, i) for i in range(nx, 0, -1)] + [(j, 0) for j in range(ny, 0, -1)]
    for k, (a, b) in enumerate(rim):
        c, d = rim[(k + 1) % len(rim)]
        bm.faces.new((F[a][b], F[c][d], B[c][d], B[a][b])).material_index = GLOW


def sleeve(bm, line, cols, drop, s):
    """Torn soul-cloth hanging from an arm: a closed sheet of quads whose top runs inside the arm, hanging
    down and a little out, trailing back as it falls, its hem cut into tatters (long and short in turn)."""
    pts = [P(*p[:3]) for p in line]
    lens = [(b - a).length for a, b in zip(pts, pts[1:])]
    total = sum(lens)

    def along(t):
        d = t * total
        for a, b, ln in zip(pts, pts[1:], lens):
            if d <= ln or b is pts[-1]:
                return a.lerp(b, min(1.0, d / ln)), (b - a).normalized()
            d -= ln

    rows = (0.0, 0.38, 0.72, 1.0)
    thick = 0.012
    F, B = [], []
    for r, frac in enumerate(rows):
        rf, rb = [], []
        for i in range(cols):
            t = i / (cols - 1)
            top, tangent = along(t)
            length = drop[0] + (drop[1] - drop[0]) * math.sin(math.pi * t)
            if r == len(rows) - 1 and i % 2:
                length *= 0.66                       # the tatters: every other point cut short
            off = P(-0.09 * frac * frac, s * 0.07 * frac, -1.0)    # down, a little out, trailing back
            p = top + off * (length * frac)
            n = tangent.cross(Vector((0, 0, -1)) if frac == 0 else off.normalized())
            n = n.normalized() if n.length > 1e-6 else Vector((1, 0, 0))
            rf.append(bm.verts.new(p + n * (thick / 2)))
            rb.append(bm.verts.new(p - n * (thick / 2)))
        F.append(rf)
        B.append(rb)
    last = len(rows) - 1
    for r in range(last):
        mat = GLOW if r == last - 1 else PALE
        for i in range(cols - 1):
            bm.faces.new((F[r][i], F[r][i + 1], F[r + 1][i + 1], F[r + 1][i])).material_index = mat
            bm.faces.new((B[r][i], B[r + 1][i], B[r + 1][i + 1], B[r][i + 1])).material_index = mat
        for i in (0, cols - 1):                      # the two side edges
            bm.faces.new((F[r][i], F[r + 1][i], B[r + 1][i], B[r][i])).material_index = mat
    for r, mat in ((0, PALE), (last, GLOW)):         # the top (inside the arm) and the hem
        for i in range(cols - 1):
            bm.faces.new((F[r][i], B[r][i], B[r][i + 1], F[r][i + 1])).material_index = mat


def wraith():
    g = Grow(sides=12)
    fz = FACE
    # the body: rings (f, s, u, half-width, above, below) from the trail's tip, curling up off the water,
    # along a trail that dips and lifts and sways, up the arch to the head
    g.tube([
        (-2.1, 0.22, 0.36, 0.015, 0.01, 0.01),    # the trail's tip, lifted
        (-1.95, 0.22, 0.24, 0.04, 0.018, 0.018),
        (-1.75, 0.15, 0.13, 0.07, 0.03, 0.03),    # dipping to brush the water
        (-1.5, 0.04, 0.1, 0.1, 0.04, 0.04),
        (-1.25, -0.05, 0.16, 0.13, 0.05, 0.05),   # lifting again, swaying the other way
        (-1.0, -0.07, 0.26, 0.16, 0.07, 0.07),
        (-0.78, -0.04, 0.36, 0.19, 0.09, 0.09),
        (-0.58, 0, 0.5, 0.21, 0.12, 0.12),
        (-0.36, 0, 0.88, 0.25, 0.17, 0.15),       # rising steeply into the arch
        (-0.18, 0, 1.38, 0.29, 0.21, 0.17),       # the ribs, a hunched back over them
        (0.0, 0, HUMP - 0.1, 0.3, 0.2, 0.16),     # the shoulders
        (0.2, 0, HUMP - 0.08, 0.2, 0.13, 0.13),   # over the top of the arch
        (0.36, 0, HUMP - 0.1, 0.09, 0.08, 0.08),  # the neck, thrust forward
        (fz[0] - 0.2, 0, fz[1] + 0.05, 0.13, 0.18, 0.15),    # the back of the skull
        (fz[0] - 0.1, 0, fz[1] + 0.03, 0.15, 0.23, 0.22),
        (fz[0] - 0.03, 0, fz[1] + 0.02, 0.13, 0.2, 0.2),     # under the mask
    ], PALE)
    # the arms: out of a strip of three faces on each shoulder, up and out, then folding forward and down
    # through three joints (a knot of soul-stuff at each) to a long hand
    for s in (1, -1):
        root = g.patch([(-0.02, s * 0.25, HUMP - 0.08), (-0.08, s * 0.24, HUMP - 0.22),
                        (-0.13, s * 0.23, HUMP - 0.36)], (0, s, 0))
        hand = g.extrude(root, [(f, s * a, u, w, t, b) for f, a, u, w, t, b in ARM], PALE)
        fingers = g.split_cap(hand, (0.1, 0, -1), (0, s, 0), 3)
        for k, q in enumerate(fingers):                       # three long fingers, splayed
            spread = (k - 1) * 0.05
            g.extrude([q], [(1.1 + 0.02 * (1 - abs(k - 1)), s * (0.44 + spread), HUMP - 1.36, 0.02, 0.018, 0.018),
                            (1.15, s * (0.44 + spread * 1.6), HUMP - 1.54, 0.014, 0.013, 0.013)],
                      GLOW, tip=(1.22, s * (0.44 + spread * 2), HUMP - 1.7), blunt=0.007)
    g.smooth(0.2, repeat=1)
    for f in g.bm.faces:                         # the trail brightens as it thins out over the water
        c = f.calc_center_median()
        if f.material_index == PALE and c.y > 1.1:
            f.material_index = GLOW
    bm = g.bm
    mask(bm, (fz[0], 0.0, fz[1] + 0.02), MASK_TILT, MASK_R, MASK_GRID)
    for s in (1, -1):
        line = [(f, s * a, u) for f, a, u, *_ in ARM[SLEEVE[0]:SLEEVE[1] + 1]]
        sleeve(bm, line, SLEEVE_COLS, SLEEVE_DROP, s)
    return g


def build(name="ShadeWraith", coll="Creatures"):
    return wraith().make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"ShadeWraith": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
