"""The vitriol mite: a mite the size of a dog, Scalth-touched, that sweats a vitriol which dries to
salt on its back. Tolling's second creature; butchered at the lye vat with a glass flensing hook.

Drawn the way the fen rat is (tools/art/blender/README.md, "Simplicity"): one squat mass for body
and head, eight legs that are only bent spikes, two short mandibles, the eyes painted on, a crust of
salt nubs on its back. Dull close colours: a sour olive hide, darker legs, the salt pale on it.

Coordinates are written as (forward, side, up) from the mite's own point of view and turned into
Blender's with P(): it faces Blender -Y (Unity +Z) and stands centred on the origin, the middle of
its tile.

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\assets\\creatures\\common\\vitriol_mite.py")
    runpy.run_path(path, init_globals={"EXPORT": True})
"""
import math
import os
import random
import sys

import bmesh

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402

COLOURS = ["SourGround", "Peat", "SaltFlat", "Charcoal"]
HIDE, LEG, SALT, EYE = range(4)


def P(f, s, u):
    return (s, -f, u)


def vitriol_mite(seed=3):
    rnd = random.Random(seed)
    bm = bmesh.new()
    # Body and head: one tube, the head a low snout at the front, the body a broad hump behind.
    A.loft(bm, [
        (P(0.36, 0, 0.17), 0, 0),
        (P(0.33, 0, 0.18), 0.06, 0.05),
        (P(0.24, 0, 0.21), 0.11, 0.08),
        (P(0.14, 0, 0.24), 0.15, 0.11),
        (P(0.0, 0, 0.3), 0.27, 0.19),
        (P(-0.2, 0, 0.32), 0.32, 0.22),
        (P(-0.38, 0, 0.26), 0.24, 0.16),
        (P(-0.48, 0, 0.21), 0, 0),
    ], sides=8, roll=math.pi / 2, mats=HIDE)

    for s in (-1, 1):
        # Legs: four a side, each a bent spike: from inside the body out and up to a knee, then down
        # to a point on the ground, splayed wide; the front pair reach forward, the back pair back.
        for k, (f, reach, rake) in enumerate(((0.14, 0.42, 0.22), (0.02, 0.48, 0.08), (-0.14, 0.48, -0.08), (-0.28, 0.42, -0.22))):
            base = P(f, s * 0.1, 0.27)
            knee = P(f + rake * 0.5, s * reach * 0.7, 0.42 + rnd.uniform(-0.02, 0.02))
            foot = P(f + rake, s * reach, 0.0)
            A.loft(bm, [(base, 0.045, 0.045), (knee, 0.03, 0.03), (foot, 0, 0)], sides=4, ref=(0, 0, 1), mats=LEG)
        # Mandibles: short spikes from inside the snout, curving in.
        A.spike(bm, P(0.3, s * 0.04, 0.16), 0.025, P(0.42, s * 0.02, 0.1), sides=3, mat=LEG)
        # Eyes: a pair a side, painted on the head.
        for df, du in ((0.0, 0.0), (-0.04, 0.02)):
            A.paint(bm, P(0.24 + df, s * 0.09, 0.24 + du), EYE, facing=(s, -0.4, 0.4), size=0.3)

    # The salt crust: low nubs along the back, their bases sunk in the hide.
    for k in range(7):
        f, side = rnd.uniform(-0.38, 0.05), rnd.uniform(-0.18, 0.18)
        u = 0.32 + 0.2 * (1 - (side / 0.32) ** 2) ** 0.5 - 0.03
        c = P(f, side, u)
        r = rnd.uniform(0.035, 0.06)
        A.hull(bm, [(c[0] + rnd.uniform(-r, r), c[1] + rnd.uniform(-r, r), c[2] + rnd.uniform(-0.04, r * 0.9)) for _ in range(7)], SALT)

    A.finish(bm)
    return A.mesh_from("VitriolMite", bm, COLOURS, coll="Creatures", smooth_angle=55)


ASSETS = {
    "VitriolMite": vitriol_mite,
}


if __name__ in ("__main__", "<run_path>"):
    result = family.run(__file__, ASSETS, globals())
