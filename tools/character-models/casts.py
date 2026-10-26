"""
Occultism's cast animations, dual wielding, keyframed in code.

Run inside Blender (5.x), through the Blender MCP or:
    blender --background --python tools/character-models/casts.py

It uses animate.py's machinery (poses, the fitter, both arms, steps, export) for its own clips: see
that file's notes for how a key is described. Each clip is named after its ability and starts and
ends in the dual-wield fighting stance. PREVIEW = True stands them side by side in their own scene,
"Occult casts", with an athame in each hand: a placeholder (the off hand may become a censer; the
main hand is undecided), so the casts are built round the arms and body, not the blades.

Rules every cast keeps:
  - Done by 1.2 s. The global cooldown is 3 ticks (1.8 s) and the next cast must be able to start.
  - The release (the moment the spell leaves) is early, about 0.3-0.45 s in, and held for a beat.
  - One clear silhouette, readable from the game's camera. Yellow (the fire) is sharp, rising and
    flung outward; black (the solvent) is heavy and inward: pulling, sinking, drawing to the body.

Keys aim each hand: where its grip is, in the body's frame (+Y ahead, at the target; +X right;
metres up from the ground), and loosely which way its blade leans (a soft preference, so the wrists
stay easy). R() is the right hand, L() the left. The body: bend, twist (positive left), drop,
shift (forward into a lunge), look (negative looks up), step_l/step_r and lift_l/lift_r (the feet).
"""

import os

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\dev\rth\tools\character-models"
_settings = {k: globals()[k] for k in ("EXPORT", "PREVIEW", "CLIPS") if k in globals()}
_lib = {"__name__": "animate", "__file__": os.path.join(HERE, "animate.py"), "RUN": False, **_settings}
exec(compile(open(os.path.join(HERE, "animate.py"), encoding="utf-8").read(), _lib["__file__"], "exec"), _lib)
Aim, Pose, Clip = _lib["Aim"], _lib["Pose"], _lib["Clip"]

WEAPON = "Athame"   # a placeholder in each hand
STANCE_FEET = ((-0.18, 0.12), (0.20, -0.10), 12)   # left foot forward, a fighting stance


def R(grip, blade=(0.05, 0.35, 0.94)):
    """The right hand at grip; its blade leaning (loosely) the given way."""
    return Aim(grip=grip, haft=blade, edge=(0, 0, 1), edge_weight=0, haft_weight=0.3)


def L(grip, blade=(-0.05, 0.35, 0.94)):
    """The left hand at grip; its blade leaning (loosely) the given way."""
    return Aim(grip=grip, haft=blade, edge=(0, 0, 1), edge_weight=0, haft_weight=0.3)


def stance():
    """Ready to cast: fists up in front, thumbs up; the body a little low."""
    return Pose(R((0.24, 0.30, 1.20)), L((-0.24, 0.28, 1.18)), bend=4, drop=0.05, look=0.15)


def cast(name, seconds, keys):
    assert seconds <= 1.2, name + ": a cast must be done by 1.2 s"
    return Clip(name, WEAPON, seconds, keys, {}, stance=STANCE_FEET, hold="dual")


# ---- yellow: the fire ----

def gall_spark():
    """Builder. A backhand flick: the right hand cocks across to the left shoulder, then snaps out
    straight to the front-right, throwing the spark; the left pulls back to the hip."""
    ready = stance()
    cock = Pose(R((-0.10, 0.22, 1.40), (-0.3, 0, 0.95)), L((-0.26, 0.20, 1.05)),
                bend=0, twist=25, drop=0.05, look=0.1)
    flick = Pose(R((0.48, 0.52, 1.40), (0.5, 0.5, 0.7)), L((-0.28, 0.00, 1.02)),
                 bend=5, twist=-20, drop=0.07, look=0.1)
    return cast("gall_spark", 1.0, [(0.00, ready, "smooth"), (0.18, cock, "out"), (0.30, flick, "in"),
                                    (0.45, flick, "linear"), (1.00, ready, "smooth")])


def choleric_flare():
    """Spender. Both hands drawn to the right hip, coiled; then a step in with the left foot and both
    thrust out together at the target, the body following into a lunge."""
    ready = stance()
    draw = Pose(R((0.30, 0.08, 1.00)), L((0.12, 0.22, 1.02)),
                bend=4, twist=-40, drop=0.08, shift=-0.05, look=0.2)
    stepping = Pose(R((0.25, 0.30, 1.10)), L((0.05, 0.40, 1.12)),
                    bend=8, twist=-15, drop=0.10, shift=0.08, step_l=0.14, lift_l=0.08, look=0.2)
    lunge = Pose(R((0.10, 0.72, 1.25), (0, 0.6, 0.8)), L((-0.06, 0.72, 1.25), (0, 0.6, 0.8)),
                 bend=12, twist=5, drop=0.14, shift=0.20, step_l=0.28, look=0.15)
    back = Pose(R((0.24, 0.36, 1.18)), L((-0.22, 0.34, 1.16)),
                bend=6, drop=0.08, shift=0.06, step_l=0.12, lift_l=0.05, look=0.15)
    return cast("choleric_flare", 1.2, [(0.00, ready, "smooth"), (0.25, draw, "smooth"), (0.34, stepping, "in"),
                                        (0.42, lunge, "in"), (0.60, lunge, "linear"), (0.90, back, "smooth"),
                                        (1.20, ready, "smooth")])


def vitriol_rain():
    """Ultimate. Calling down the rain: from a crouch, the right arm thrown straight up at the sky,
    the left down and out to the side, the face up; then both sweep down in front as it falls."""
    ready = stance()
    crouch = Pose(R((0.06, 0.36, 0.95)), L((-0.06, 0.38, 0.97)),
                  bend=20, drop=0.14, look=0.4)
    skyward = Pose(R((0.24, 0.10, 2.10), (0.3, 0, 0.95)), L((-0.58, 0.10, 0.95), (-0.9, 0.3, 0.3)),
                   bend=-10, twist=8, drop=0.0, look=-0.6)
    fall = Pose(R((0.45, 0.42, 1.00)), L((-0.45, 0.42, 1.00)),
                bend=15, drop=0.08, look=0.3)
    return cast("vitriol_rain", 1.2, [(0.00, ready, "smooth"), (0.20, crouch, "smooth"), (0.40, skyward, "in"),
                                      (0.62, skyward, "linear"), (0.78, fall, "in"), (1.20, ready, "smooth")])


# ---- black: the solvent ----

def black_draught():
    """Builder. A pull: the left arm reaches into the target, then yanks back to the chest as the
    right foot steps back and the body leans away, the right hand crossing to meet it."""
    ready = stance()
    reach = Pose(R((0.20, 0.22, 1.20)), L((-0.10, 0.70, 1.38), (0, 0.35, 0.94)),
                 bend=8, twist=15, drop=0.05, shift=0.08, look=0.1)
    yanking = Pose(R((0.12, 0.20, 1.25)), L((-0.10, 0.35, 1.32)),
                   bend=0, twist=4, drop=0.05, shift=-0.02, step_r=-0.08, lift_r=0.07, look=0.0)
    yank = Pose(R((0.06, 0.17, 1.28), (-0.3, 0.2, 0.93)), L((-0.08, 0.15, 1.30), (0.3, 0.2, 0.93)),
                bend=-8, twist=-5, drop=0.06, shift=-0.10, step_r=-0.16, look=-0.1)
    return cast("black_draught", 1.1, [(0.00, ready, "smooth"), (0.22, reach, "out"), (0.30, yanking, "in"),
                                       (0.38, yank, "out"), (0.55, yank, "linear"), (1.10, ready, "smooth")])


def black_sun():
    """Ultimate. A stamp and a slam: both hands up overhead together as the right foot lifts, then
    a deep squat drives them down at the ground as the foot stamps."""
    ready = stance()
    raised = Pose(R((0.12, 0.22, 2.00), (-0.8, 0, 0.6)), L((-0.12, 0.22, 2.00), (0.8, 0, 0.6)),
                  bend=-10, drop=0.0, lift_r=0.12, step_r=0.05, look=-0.6)
    slam = Pose(R((0.16, 0.55, 0.72), (0, 0.8, 0.6)), L((-0.16, 0.55, 0.72), (0, 0.8, 0.6)),
                bend=35, drop=0.30, shift=0.05, step_r=0.08, look=0.6)
    return cast("black_sun", 1.2, [(0.00, ready, "smooth"), (0.25, raised, "out"), (0.42, slam, "in"),
                                   (0.65, slam, "linear"), (1.20, ready, "smooth")])


CASTS = [gall_spark, choleric_flare, vitriol_rain, black_draught, black_sun]
_lib["CLIPS"] = globals().get("CLIPS", [c.__name__ for c in CASTS])

if globals().get("RUN", True):
    result = _lib["run"](CASTS, "Occult casts")
