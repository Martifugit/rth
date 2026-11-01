"""The wolf, grown as one mesh (artlib/grow.py): a body tube, the neck and head extruded out of its
front, the tail out of its rump, the legs and ears pulled out of its faces.

Proportions are a real grey wolf's (shoulder 0.8 m, skull 23-28 cm long and 13-15 wide, ears 9-11
cm, a tail a third of the body, a deep chest and a level back), then stylised for the game camera
the way the 2004 game's animals are: a bigger head, a ruff, thicker legs and paws (STYLE).

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\creatures\\common\\wolf.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
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

from artlib import family  # noqa: E402
from artlib.grow import Grow  # noqa: E402

COLOURS = ["Ashgrey", "SaltFlat", "Charcoal"]
FUR, PALE, DARK = range(3)

# How the game exaggerates a real animal so it reads from the camera.
STYLE = dict(size=1.12, body=1.25, head=1.45, ruff=1.35, legs=1.4)


# The head in its own frame from the root of the neck: (d forward, lateral, up). A short, thick neck
# sloping forward, the head held just above the back: a broad skull with cheeks, a stop, then a
# muzzle as long as the skull, deeper at the jaw than above. Rings scale with h.
NECK = [(0.07, 0.09, 0.11, 0.1, 0.1),        # the neck
        (0.14, 0.13, 0.12, 0.09, 0.09)]      # the skull and cheeks, wider than the neck
FACE = [(0.21, 0.125, 0.085, 0.07, 0.08),    # the stop
        (0.29, 0.1, 0.052, 0.042, 0.065),    # the muzzle
        (0.36, 0.09, 0.032, 0.03, 0.045)]
NOSE = (0.385, 0.095, 0.022, 0.022, 0.024)
EAR = dict(at=(0.13, 0.08, 0.23), ring=(0.12, 0.09, 0.26, 0.048, 0.034, 0.03), tip=(0.115, 0.1, 0.33))


def head(g, region, root, yaw, h, neck=(), mats=(FUR, PALE, DARK)):
    """A wolf's neck and head grown out of `region`, from `root` (f, s, u), turned `yaw` radians to
    the creature's left. neck: rings (d, u, w, top, bottom) in metres before the head, for a longer
    neck; the head follows on from the last of them. mats: fur, pale face, nose. Returns a function
    from a point in the head's own frame (d, lateral, u; lateral in head units) to (f, s, u)."""
    fur, pale, dark = mats
    cy, sy = math.cos(yaw), math.sin(yaw)
    shift = max([n[0] for n in neck], default=0.0)

    def at(d, lat, u):
        return (root[0] + d * cy - lat * sy, root[1] + d * sy + lat * cy, root[2] + u)

    def ring(d, u, w, top, bottom, k=h, off=shift):
        return at(d + off, 0.0, u) + (w * k, top * k, bottom * k)

    up = max([n[1] for n in neck], default=0.0)
    lift = lambda rs: [(d, u + up, w, t, b) for d, u, w, t, b in rs]
    skull = g.extrude(region, [ring(*n, k=1.0, off=0.0) for n in neck] + [ring(*n) for n in lift(NECK)], fur)
    nose = g.extrude(skull, [ring(*n) for n in lift(FACE)], pale)
    g.extrude(nose, [ring(*lift([NOSE])[0])], dark)
    for s in (1, -1):
        (ad, al, au), (rd, rl, ru, rw, rt, rb), (td, tl, tu) = EAR["at"], EAR["ring"], EAR["tip"]
        g.extrude([g.face(at(ad + shift, s * al * h, au + up), (-sy * s * 0.3, cy * s * 0.3, 1))],
                  [at(rd + shift, s * rl * h, ru + up) + (rw * h, rt, rb)], fur,
                  tip=at(td + shift, s * tl * h, tu + up))
    return lambda d, lat, u: at(d + shift, lat * h, u + up)   # a point on the head, for painting


def wolf(style=STYLE, name="Wolf"):
    h, r, l, b = style["head"], style["ruff"], style["legs"], style.get("body", 1.0)
    g = Grow(sides=8)
    # the body: rings (f, s, u, half-width, above, below) from the rump to the chest
    body = g.tube([
        (-0.46, 0, 0.68, 0.09 * b, 0.08, 0.08),
        (-0.36, 0, 0.68, 0.14 * b, 0.13, 0.13),          # the haunches, as high as the withers: a level back
        (-0.18, 0, 0.67, 0.12 * b, 0.12, 0.12),          # the loin, a little tucked
        (0.02, 0, 0.65, 0.15 * b, 0.14, 0.18),           # the ribs
        (0.18, 0, 0.64, 0.15 * b, 0.15 * r, 0.22),   # the chest and withers, deep below
        (0.3, 0, 0.66, 0.13 * b, 0.15 * r, 0.18),    # the breast under the ruff
    ], FUR)
    skull = g.extrude(body.caps["end"], [(0.38, 0, 0.75, 0.115 * b * r, 0.13 * r, 0.14)], FUR)   # the ruff
    head(g, skull, (0.38, 0, 0.75), 0.0, h)
    # the tail, a third of the body's length, bushy in its middle, hanging
    g.extrude(body.caps["start"], [(-0.53, 0, 0.63, 0.06, 0.07, 0.07), (-0.6, 0, 0.5, 0.08, 0.1, 0.1),
                                   (-0.63, 0, 0.36, 0.07, 0.085, 0.085)], FUR, tip=(-0.64, 0, 0.24))
    for s in (1, -1):
        # each leg grows from two body faces one behind the other, so it starts six-sided, longer than wide
        fore = [g.face((0.1, s * 0.13, 0.5), (0, s * 0.4, -1)), g.face((0.24, s * 0.13, 0.5), (0, s * 0.4, -1))]
        wrist = g.extrude(fore, [
            (0.18, s * 0.115, 0.4, 0.055 * l, 0.06 * l, 0.06 * l),    # the upper arm, then the elbow
            (0.17, s * 0.11, 0.24, 0.042 * l, 0.042 * l, 0.042 * l),  # the forearm
            (0.19, s * 0.11, 0.09, 0.036 * l, 0.036 * l, 0.036 * l)], FUR)   # the wrist
        g.extrude(wrist, [(0.25, s * 0.11, 0.035, 0.045 * l, 0.035 * l, 0.03)], FUR)   # the paw, forward
        hind = [g.face((-0.42, s * 0.12, 0.58), (0, s * 0.4, -1)), g.face((-0.27, s * 0.12, 0.56), (0, s * 0.4, -1))]
        hock = g.extrude(hind, [
            (-0.27, s * 0.13, 0.4, 0.06 * l, 0.085 * l, 0.07 * l),    # the thigh, broad from the side; the stifle forward
            (-0.36, s * 0.125, 0.24, 0.04 * l, 0.045 * l, 0.045 * l), # the shank, back to the hock
            (-0.38, s * 0.12, 0.13, 0.034 * l, 0.034 * l, 0.034 * l), # the hock
            (-0.35, s * 0.12, 0.07, 0.034 * l, 0.034 * l, 0.034 * l)], FUR)
        g.extrude(hock, [(-0.29, s * 0.12, 0.035, 0.045 * l, 0.035 * l, 0.03)], FUR)
    for f in g.bm.faces:                                  # markings: a grey bridge to the nose over the pale face,
        f.normal_update()                                 # and a pale throat and breast under the ruff
        c, n = f.calc_center_median(), f.normal
        if f.material_index == PALE and n.z > 0.55:
            f.material_index = FUR
        elif f.material_index == FUR and -c.y > 0.3 and c.z > 0.45 and (-n.y * 0.6 - n.z * 0.8) > 0.55:
            f.material_index = PALE
    size = style.get("size", 1.0)
    for v in g.bm.verts:
        v.co *= size
    return g


def build(style=STYLE, name="Wolf", coll="Creatures"):
    g = wolf(style, name)
    return g.make(name, COLOURS, coll=coll)


ASSETS = {"Wolf": build}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
