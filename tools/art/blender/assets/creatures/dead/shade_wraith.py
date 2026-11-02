r"""The wraith, a shade (variant B of four for the owner to pick): one who has been dead so long it has
forgotten its shape. Stretched and hunched, its back a high arch, its trail of soul-stuff lying long on
the water behind it; a long neck carries a hollow face, a pale oval mask round a dark void; two arms
much too long, folding at three joints, reach forward and down to three long fingers.

What must read, in order: the high hunched arch and the long trail; the hollow face; the reaching arms
with too many joints. About 1.9 m to the hump, 3 m from the trail's end to the fingers. Grown as one
mesh (artlib/grow.py): the body a tube from the trail to the face, the arms extruded from the
shoulders, the fingers out of the hand's split end. Faces Blender -Y.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\shade_wraith.py")
"""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

from artlib import family  # noqa: E402
from artlib.grow import Grow  # noqa: E402

COLOURS = ["SoulPale", "SoulGlow", "Sable"]
PALE, GLOW, VOID = range(3)

HUMP = 1.9                      # the arch's top; the shoulders, neck and face hang from it
FACE = (0.62, HUMP - 0.16)      # (f, u) the face's middle, thrust forward and low


def wraith():
    g = Grow(sides=12)
    fz = FACE
    # the body, lying: rings (f, s, u, half-width, above, below) from the trail's end up the arch to the face
    body = g.tube([
        (-1.75, 0, 0.06, 0.02, 0.015, 0.015),     # the trail's end, on the water
        (-1.4, 0, 0.1, 0.08, 0.03, 0.03),
        (-1.0, 0, 0.2, 0.15, 0.06, 0.06),         # the trail, broad and flat
        (-0.62, 0, 0.45, 0.2, 0.11, 0.11),
        (-0.36, 0, 0.88, 0.25, 0.17, 0.15),       # rising steeply into the arch
        (-0.18, 0, 1.38, 0.29, 0.21, 0.17),       # the ribs, a hunched back over them
        (0.0, 0, HUMP - 0.1, 0.3, 0.2, 0.16),     # the shoulders
        (0.2, 0, HUMP - 0.08, 0.2, 0.13, 0.13),   # over the top of the arch
        (0.36, 0, HUMP - 0.1, 0.09, 0.08, 0.08),  # the neck, thrust forward
        (fz[0] - 0.16, 0, fz[1] + 0.02, 0.12, 0.15, 0.14),   # the back of the head
        (fz[0] - 0.05, 0, fz[1], 0.14, 0.24, 0.24),   # the mask: a tall pale oval
        (fz[0], 0, fz[1], 0.13, 0.23, 0.23),          # its rim's lip
    ], PALE)
    # the hollow: the mask's face pushed deep in, dark
    g.extrude(body.caps["end"], [(fz[0] - 0.01, 0, fz[1] - 0.02, 0.06, 0.17, 0.17),    # a long slot, like a cry
                                  (fz[0] - 0.18, 0, fz[1] - 0.02, 0.04, 0.12, 0.12)], VOID)
    # the arms: out of a strip of three faces on each shoulder, up and out, then folding forward and down
    # through three joints (a knot of soul-stuff at each) to a long hand
    for s in (1, -1):
        root = g.patch([(-0.02, s * 0.25, HUMP - 0.08), (-0.08, s * 0.24, HUMP - 0.22),
                        (-0.13, s * 0.23, HUMP - 0.36)], (0, s, 0))
        hand = g.extrude(root, [
            (0.04, s * 0.4, HUMP - 0.14, 0.08, 0.09, 0.09),      # the shoulder's knob
            (0.14, s * 0.5, HUMP - 0.08, 0.05, 0.055, 0.055),
            (0.24, s * 0.62, HUMP - 0.04, 0.075, 0.075, 0.075),  # the first joint, level with the hump
            (0.4, s * 0.68, HUMP - 0.3, 0.045, 0.05, 0.05),
            (0.52, s * 0.72, HUMP - 0.52, 0.07, 0.07, 0.07),     # the second, forward and down
            (0.72, s * 0.66, HUMP - 0.6, 0.042, 0.046, 0.046),
            (0.9, s * 0.58, HUMP - 0.66, 0.065, 0.065, 0.065),    # the third
            (0.98, s * 0.52, HUMP - 0.9, 0.04, 0.044, 0.044),
            (1.04, s * 0.45, HUMP - 1.1, 0.05, 0.055, 0.05),     # the wrist, hanging
            (1.07, s * 0.44, HUMP - 1.2, 0.08, 0.035, 0.035),    # the hand, flat and wide
        ], PALE)
        fingers = g.split_cap(hand, (0.1, 0, -1), (0, s, 0), 3)
        for k, q in enumerate(fingers):                       # three long fingers, splayed
            spread = (k - 1) * 0.05
            g.extrude([q], [(1.1 + 0.02 * (1 - abs(k - 1)), s * (0.44 + spread), HUMP - 1.36, 0.02, 0.018, 0.018),
                            (1.15, s * (0.44 + spread * 1.6), HUMP - 1.54, 0.014, 0.013, 0.013)],
                      GLOW, tip=(1.22, s * (0.44 + spread * 2), HUMP - 1.7), blunt=0.007)
    g.smooth(0.2, repeat=1)
    for f in g.bm.faces:                         # the trail brightens as it thins out on the water
        c = f.calc_center_median()
        if f.material_index == PALE and c.y > 0.55 and c.z < 0.6:
            f.material_index = GLOW
    return g


def build(name="ShadeWraith", coll="Creatures"):
    return wraith().make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"ShadeWraith": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
