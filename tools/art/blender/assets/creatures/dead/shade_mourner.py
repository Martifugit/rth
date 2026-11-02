r"""The mourner, a shade (variant D of four for the owner to pick): one who kept vigil so long for someone
else that they never left. Tall, robed to the water and veiled to the knee, head bowed, no face;
a crown of candles stands on the veiled head, cold soul flames burning on it and wax running down the
veil. It ties the shades to the candles that light the realm of the dead.

What must read, in order: the crown of small flames over the bowed head; the tall veiled shape, the
veil's hem a rim of light over the robe; the bow. About 1.8 m to the veil's crown, the flames to 2.2 m.
Grown as one mesh (artlib/grow.py); the candles are prisms sunk into the head, the flames teardrops on
them, the wax painted onto the veil's faces. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\shade_mourner.py")
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

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.grow import P, Grow  # noqa: E402

COLOURS = ["SoulPale", "SoulGlow", "CandleWax", "CandleFlame"]
PALE, GLOW, WAX, FLAME = range(4)

VEIL = 0.78                     # the veil's hem over the robe; the shoulders and head follow from it
CROWN = (0.1, 1.8)            # (f, u) the top of the bowed head, where the candles stand
CANDLES = [(0.0, 0.2), (0.14, 0.15), (-0.14, 0.17), (0.29, 0.12), (-0.29, 0.13), (0.43, 0.22), (-0.43, 0.1),
           (0.5, 0.26)]   # (turns from the front, height): a tall one at the back


def mourner():
    g = Grow(sides=12)
    cf, cu = CROWN
    # upright, hem to crown: rings (f, s, u, half-width, in front, behind). Each tier's hem stands out
    # from the tier under it and tucks in above, so the drapes overlap like layers of cloth
    g.tube([
        (-0.66, 0, 0.05, 0.1, 0.02, 0.02),        # the robe's train, trailing behind on the water
        (-0.48, 0, 0.1, 0.24, 0.05, 0.05),
        (-0.26, 0, 0.2, 0.36, 0.2, 0.18),         # the robe's hem, wide, swept back
        (-0.12, 0, 0.42, 0.3, 0.25, 0.24),
        (-0.05, 0, 0.62, 0.25, 0.22, 0.23),
        (-0.02, 0, VEIL, 0.38, 0.34, 0.38),       # the veil's hem, standing out over the robe
        (0.0, 0, VEIL + 0.06, 0.34, 0.31, 0.35),
        (0.03, 0, VEIL + 0.36, 0.26, 0.23, 0.26),  # the veil, a bell narrowing up
        (0.05, 0, VEIL + 0.6, 0.2, 0.17, 0.2),    # the shoulders, narrow, sloping
        (0.07, 0, VEIL + 0.74, 0.15, 0.15, 0.16),  # the veil falling from the head over them
        (0.08, 0, VEIL + 0.86, 0.14, 0.14, 0.14),  # the head under the veil, a little bowed
        (cf, 0, cu - 0.08, 0.135, 0.135, 0.135),
        (cf, 0, cu - 0.02, 0.125, 0.125, 0.125),
        (cf - 0.01, 0, cu, 0.11, 0.11, 0.11),     # the crown, flat for the candles
    ], PALE, upright=True)
    # the hands, joined in prayer before the chest, out of the cape's front and back into it
    hands = g.patch([(0.28, 0.0, VEIL + 0.24), (0.27, 0.06, VEIL + 0.24), (0.27, -0.06, VEIL + 0.24)], (1, 0, 0))
    g.extrude(hands, [(0.34, 0, VEIL + 0.22, 0.06, 0.05, 0.05), (0.4, 0, VEIL + 0.26, 0.045, 0.04, 0.04)], GLOW,
              tip=(0.44, 0, VEIL + 0.34), blunt=0.012)
    g.smooth(0.15, repeat=1)
    # each drape's hem is a rim of light
    hems = (VEIL,)
    for f in g.bm.faces:
        z = f.calc_center_median().z
        if f.material_index == PALE and (any(h - 0.02 < z < h + 0.07 for h in hems) or z < 0.3):
            f.material_index = GLOW
    bm = g.bm
    # the crown of candles, sunk into the top of the head, each with a teardrop flame
    for turn, h in CANDLES:
        a = 2 * math.pi * turn
        c = P(cf + 0.085 * math.cos(a), 0.085 * math.sin(a), 0)
        x, y = c.x, c.y
        A.prism(bm, 6, 0.03, 0.027, cu - 0.08, cu + h, center=(x, y), mat_index=WAX)
        top = cu + h
        fl = []
        for k in range(5):
            b = 2 * math.pi * k / 5
            fl.append((x + 0.026 * math.cos(b), y + 0.026 * math.sin(b), top + 0.035))
        fl += [(x, y, top + 0.004), (x, y, top + 0.12)]
        A.hull(bm, fl, mat=FLAME)
    for k in range(4):                           # wax run down the veil from the crown
        a = 2 * math.pi * (0.1 + k * 0.23)
        A.paint(bm, tuple(P(cf + 0.12 * math.cos(a), 0.12 * math.sin(a), cu - 0.06)), WAX,
                facing=(math.sin(a), -math.cos(a), 0.3), size=0.7)
    return g


def build(name="ShadeMourner", coll="Creatures"):
    return mourner().make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"ShadeMourner": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
