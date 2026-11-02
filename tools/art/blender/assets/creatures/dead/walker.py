r"""The walker: someone who tried the long way back in life and failed, and now waits at its end to help
those who make it. A stooped traveller in a worn hooded cloak to the ground, leaning on a long staff
in the right hand, a small soul lantern hung from a crook at its top. The one solid, warm thing in the
realm of the dead. Rigid (no rig); the face is lost in the hood (the README's faces rules: no
modelled face). Grown as one mesh like the goblin; the staff and lantern are rods and prisms gripped
by the hand.

What must read, in order: the stoop and the hump of the back; the hood; the staff taller than they
are, with its pale light. About 1.85 m to the hood's top, stooped; the staff 2.2 m.

    import runpy; runpy.run_path(r"C:\dev\rth\tools\art\blender\assets\creatures\dead\walker.py")
"""
import os
import sys

from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.grow import P, Grow  # noqa: E402
import artlib.nature as N  # noqa: E402

COLOURS = ["Hide", "Mud", "Sable", "Skin3", "Bark", "Blackiron", "SoulPale", "HairGrey"]
CLOAK, UNDER, VOID, SKIN, STAFF, IRON, SOUL, BEARD = range(8)

SHOULDER = 1.42                                  # the hump's top; the hood and arms follow from it
STAFF_AT = (0.58, -0.4)                          # (f, s) where the staff meets the ground, on the right
GRIP = 1.22                                      # the hand's height on the staff
STAFF_TOP = 2.2


def walker():
    g = Grow(sides=12)
    g.tube([
        (0.0, 0, 0.0, 0.36, 0.33, 0.38),          # the hem, on the ground, frayed wide
        (0.0, 0, 0.14, 0.34, 0.31, 0.36),         # the hem's top: the wet line
        (0.02, 0, 0.32, 0.3, 0.26, 0.31),
        (0.06, 0, 0.72, 0.25, 0.2, 0.25),
        (0.12, 0, 1.02, 0.24, 0.17, 0.25),        # the waist, belted
        (0.21, 0, 1.25, 0.26, 0.17, 0.27),        # the chest, stooped forward
        (0.3, 0, SHOULDER, 0.25, 0.15, 0.29),     # the shoulders, the back humped behind them
        (0.38, 0, SHOULDER + 0.07, 0.15, 0.12, 0.16),   # the neck, thrust forward
        (0.46, 0, SHOULDER + 0.13, 0.16, 0.16, 0.16),   # the hood
        (0.52, 0, SHOULDER + 0.25, 0.17, 0.17, 0.16),
        (0.5, 0, SHOULDER + 0.36, 0.14, 0.14, 0.15),
        (0.41, 0, SHOULDER + 0.43, 0.07, 0.06, 0.08),   # its peak, fallen back
    ], CLOAK, upright=True)
    # the face lost in the hood: the opening pushed in, dark
    h = SHOULDER + 0.25
    mouth = g.patch([(0.68, 0, h - 0.06), (0.68, 0.06, h - 0.06), (0.68, -0.06, h - 0.06),
                     (0.68, 0, h + 0.06), (0.68, 0.06, h + 0.06), (0.68, -0.06, h + 0.06)], (1, 0, 0))
    g.extrude(mouth, [(0.63, 0, h, 0.085, 0.09, 0.08), (0.5, 0, h, 0.07, 0.07, 0.06)], VOID)
    # the right arm out of the shoulder, forward to the staff; the left kept under the cloak
    sf, ss = STAFF_AT
    root = g.patch([(0.3, -0.27, SHOULDER - 0.06), (0.24, -0.26, SHOULDER - 0.2)], (0, -1, 0))
    wrist = g.extrude(root, [
        (0.36, -0.36, SHOULDER - 0.12, 0.08, 0.09, 0.09),     # the sleeve at the shoulder
        (0.42, -0.4, GRIP + 0.02, 0.07, 0.08, 0.08),          # the elbow, low
        (sf - 0.08, ss + 0.01, GRIP + 0.02, 0.07, 0.08, 0.08),     # the cuff, wide
    ], CLOAK)
    g.extrude(wrist, [(sf - 0.05, ss, GRIP, 0.04, 0.045, 0.045),
                      (sf, ss, GRIP, 0.05, 0.06, 0.05)], SKIN, tip=(sf + 0.05, ss, GRIP - 0.01))   # the fist round the staff
    g.smooth(0.2, repeat=1)
    for f in g.bm.faces:                         # the hem dark with the wet and ash it drags through
        if f.material_index == CLOAK and f.calc_center_median().z < 0.14:
            f.material_index = UNDER
    bm = g.bm
    # a grey beard out of the hood's shadow: the one sign of a face, its root deep in the dark
    A.hull(bm, [P(0.44, 0.07, h - 0.02), P(0.44, -0.07, h - 0.02), P(0.44, 0.06, h - 0.13), P(0.44, -0.06, h - 0.13),
                P(0.6, 0.07, h - 0.1), P(0.6, -0.07, h - 0.1), P(0.53, 0.03, h - 0.32), P(0.53, -0.03, h - 0.32)], mat=BEARD)
    # the staff: a crooked pole from the ground, through the fist, to a crook bent forward at the top
    base = P(sf, ss, 0.0)
    N.rod(bm, [base, P(sf + 0.01, ss, GRIP), P(sf, ss, 1.8), P(sf - 0.01, ss, STAFF_TOP - 0.05)],
          0.03, mat=STAFF, sides=6, taper=0.8)
    crook = [P(sf - 0.01, ss, STAFF_TOP - 0.1), P(sf + 0.05, ss, STAFF_TOP + 0.02), P(sf + 0.18, ss, STAFF_TOP)]
    N.rod(bm, crook, 0.022, mat=STAFF, sides=5, taper=0.8)
    # the lantern hung from the crook's end: a short chain, an iron cap, the soul light, an iron foot
    end = crook[-1]
    top = end.z - 0.04
    N.rod(bm, [end + Vector((0, 0, 0.01)), Vector((end.x, end.y, top - 0.12))], 0.008, mat=IRON, sides=4)
    lz = top - 0.12
    A.prism(bm, 6, 0.045, 0.02, lz - 0.04, lz + 0.005, center=(end.x, end.y), mat_index=IRON, cap_bottom=True)
    A.prism(bm, 6, 0.04, 0.04, lz - 0.17, lz - 0.035, center=(end.x, end.y), mat_index=SOUL, cap_bottom=True)
    A.prism(bm, 6, 0.05, 0.045, lz - 0.2, lz - 0.165, center=(end.x, end.y), mat_index=IRON, cap_bottom=True)
    return g


def build(name="Walker", coll="Creatures"):
    return walker().make(name, COLOURS, coll=coll, smooth_angle=60)


ASSETS = {"Walker": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
