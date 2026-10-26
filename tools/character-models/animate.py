"""
Character animations keyframed in code on the shared skeleton (the skilling swings).

Run inside Blender (5.x), through the Blender MCP or:
    blender --background --python tools/character-models/animate.py

It opens the built HumanMale.blend (run build_humanoid.py first), poses the Rig frame by frame and
exports each clip as client/Assets/Resources/Characters/Animations/<name>.fbx: the armature only,
with the same axis settings as the body, so CharacterImporter maps it as one of our own skeletons.
Every clip needs an entry in animations.json (the catalog). Set CLIPS (names) to build only some,
EXPORT = False to pose without writing, and PREVIEW = True to stand a copy of the character per
clip side by side, each with its tool in the hand, all playing together (Space in the viewport).

How a clip is described: keys (Pose) of a few scalars, eased between keys and solved every frame.
  - The body: `bend` (forward, degrees), `twist` (positive turns left), `drop` (metres the hips
    sink), `shift` (metres the hips move forward, into a lunge) and `look` (how much the head bows
    to the work). The feet stand at the clip's `stance`; `step_l`/`step_r` move one forward or back
    and `lift_l`/`lift_r` raise it while it travels.
  - The right arm is posed joint by joint from hanging at the side (forward kinematics, so it
    can't break): `raise` lifts it forward (90 level, 180 straight up), `spread` takes it out to
    the side, `roll` turns it about its length, `flex` bends the elbow, `turn` rolls the forearm
    (positive brings the thumb up from palm-down), `cock` tilts the fist towards the thumb
    (negative: the little finger) and `wrist` bends it towards the palm. Every one is held to a
    human range (LIMITS). The tool follows the fist: the haft out past the thumb, the business
    side (a pick's point, an axe's edge) off the knuckles.
  - A key may instead `aim` the tool (Aim: where the grip is, which way the haft and the edge
    point, in the body's space: +Y ahead, +X right, metres up from the ground). fit() then finds
    the joint angles that come closest within the limits, keeping the elbow clear of the head and
    the upper arm near straight out from the shoulder. Between keys the angles are eased, so the
    tool travels in arcs, as a swung tool does.
  - Two-handed tools: the left fist closes round the haft `left` further up it (IK; its elbow
    finds the least twisted, least tilted place clear of the head, near parallel to the right).

The report gives each key's fit (how far the grip missed, degrees off for haft and edge) and the
left hand's worst forearm twist and wrist tilt (keep twist under about 70, tilt within 50-110).
"""

import math
import os
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\dev\rth\tools\character-models"
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND = os.path.join(HERE, "HumanMale.blend")
OUT = os.path.join(REPO, "client", "Assets", "Resources", "Characters", "Animations")
ITEMS = os.path.join(REPO, "client", "Assets", "Resources", "Items")

EXPORT = globals().get("EXPORT", True)
PREVIEW = globals().get("PREVIEW", False)
FPS = 30

# step_l/step_r move a foot forward (metres) from the stance, lift_l/lift_r raise it (a step in the
# air); shift moves the hips forward over the feet (a lunge) or back.
BODY_FIELDS = ("bend", "twist", "drop", "look", "left", "step_l", "step_r", "lift_l", "lift_r", "shift")
ARM_FIELDS = ("raise", "spread", "roll", "flex", "turn", "cock", "wrist")
ARM_FIELDS_L = tuple("l_" + f for f in ARM_FIELDS)   # the left arm's, mirrored (hold "dual")
ALL_FIELDS = BODY_FIELDS + ARM_FIELDS + ARM_FIELDS_L
# A human range for each joint of the right arm, in degrees.
LIMITS = {"raise": (-45, 170), "spread": (-35, 100), "roll": (-80, 80), "flex": (0, 140),
          "turn": (-85, 85), "cock": (-25, 20), "wrist": (-55, 55)}
SMOOTH_FRAMES = 3.0  # how many frames either side the left arm's course is eased over
GAZE_TURN = 80      # degrees the head may turn from the chest to follow its gaze
GAZE_NECK = 0.4     # the neck's share of the gaze's turn and tilt; the head takes the rest
TWIST_SHARE = 0.5   # how much of a forearm's turn its bone takes; the wrist takes the rest
SKULL_CLEAR = 0.2   # metres from the skull's middle that elbows and hands keep out of
LEFT_REACH = 0.58   # metres from the left shoulder to the furthest grip the left fist can close on
LEFT_CLOSEST = 0.32  # ... and to the nearest, before the elbow folds shut
# The torso, for keeping elbows and wrists out of it: an oval round the spine (hips to neck),
# half-widths across and front to back in metres, already allowing for the limb's own thickness.
# Elbows rest at the sides, just outside it.
TORSO_ACROSS, TORSO_DEPTH = 0.19, 0.15


def torso_clear(point, spine, body):
    """How far outside the torso's oval point is: 1 on its surface, more outside, less inside.
    `body` turns the rest frame into the chest's (x across, y ahead)."""
    from mathutils.geometry import intersect_point_line
    hips, neck = spine
    near, u = intersect_point_line(point, hips, neck)
    u = max(0.0, min(1.0, u))
    local = body.inverted() @ (point - (hips + (neck - hips) * u))
    return math.hypot(local.x / TORSO_ACROSS, local.y / TORSO_DEPTH)


def elbow_up(shoulder, elbow, wrist):
    """How far an elbow points upwards, off the line from shoulder to wrist (1 straight up)."""
    axis = (wrist - shoulder).normalized()
    point = (elbow - shoulder) - axis * (elbow - shoulder).dot(axis)
    return point.normalized().z if point.length > 1e-4 else 0.0


def elbow_inward(shoulder, elbow, wrist, body, side):
    """How far an elbow points in towards the body's midline (0 out, down or back; 1 straight in).
    Elbows point out, down or back; one turned in towards the chest looks broken."""
    axis = (wrist - shoulder).normalized()
    point = (elbow - shoulder) - axis * (elbow - shoulder).dot(axis)
    if point.length < 1e-4:
        return 0.0
    local = body.inverted() @ point.normalized()
    return -local.x if side == "R" else local.x


class Aim:
    """Where a key wants the tool: a point (x, y, z), `at` metres up the haft from the grip (0: the
    grip itself; the length of a net's pole: its hoop), and the haft's and the edge's directions,
    each weighted (0 leaves it free), and optionally which way the palm faces."""

    def __init__(self, grip, haft, edge, edge_weight=1.0, at=0.0, haft_weight=1.0, palm=None):
        self.grip, self.haft = Vector(grip), Vector(haft).normalized()
        self.at, self.haft_weight = at, haft_weight
        # Which way the right palm faces (e.g. up, under a pole it supports), or None.
        self.palm = Vector(palm).normalized() if palm is not None else None
        edge = Vector(edge)
        self.edge = (edge - self.haft * edge.dot(self.haft)).normalized()
        self.edge_weight = edge_weight


class Pose:
    """One key (see the module notes). Unset fields follow the clip's defaults."""

    def __init__(self, aim=None, aim_l=None, **kw):
        for k in kw:
            assert k in ALL_FIELDS, k
        self.values, self.aim, self.aim_l, self.fit = kw, aim, aim_l, None


class Clip:
    def __init__(self, name, tool, seconds, keys, defaults, stance, hold="both", parallel=False, facing=0.0,
                 slide=True, left_palm=None, gaze=None):
        self.name, self.tool, self.seconds, self.hold = name, tool, seconds, hold
        self.tool_l = None          # the left hand's, for hold "dual" (None: the same as the right's)
        self.slide = slide          # the left fist may slide along the haft (a pole; not a trap)
        # Which way the left palm faces, in the body's frame (e.g. down, over a pole it holds), or None.
        self.left_palm = Vector(left_palm).normalized() if left_palm is not None else None
        # Where the eyes follow: that many metres along the tool from the grip (a net's hoop), or
        # None for the body's own `look` (a bow of the head towards the work in front).
        self.gaze = gaze
        # Degrees the whole body stands turned to the left of the way the game faces the player
        # (feet and all); the keys and the stance are in the body's own frame.
        self.facing = facing
        self.parallel = parallel    # the left upper arm keeps near parallel to the right (overhead swings)
        self.keys = keys            # [(seconds, Pose, ease)]: ease is how the move INTO this key goes
        self.defaults = defaults
        self.stance = stance        # feet: (left (x, y), right (x, y), toe-out degrees)

    def value(self, pose, f):
        return pose.values.get(f, self.defaults.get(f, 0.0))

    def at(self, t):
        """The pose's scalars at time t, eased between keys."""
        fields = ALL_FIELDS
        keys = self.keys
        if t <= keys[0][0]:
            return {f: self.value(keys[0][1], f) for f in fields}
        for i in range(1, len(keys)):
            t0, t1 = keys[i - 1][0], keys[i][0]
            if t <= t1:
                u = EASE[keys[i][2]]((t - t0) / (t1 - t0) if t1 > t0 else 1.0)
                a, b = keys[i - 1][1], keys[i][1]
                return {f: self.value(a, f) + (self.value(b, f) - self.value(a, f)) * u for f in fields}
        return {f: self.value(keys[-1][1], f) for f in fields}

    def fit(self, solver):
        """Fill in the joint angles of every aimed key; returns how well each one fits."""
        report = {}
        for i, (t, pose, _) in enumerate(self.keys):
            if (pose.aim is None and pose.aim_l is None) or pose.fit is not None:
                continue
            pose.fit = {}
            for side, aim in (("R", pose.aim), ("L", pose.aim_l)):
                if aim is None:
                    continue
                seed = {f: self.value(pose, f) for f in ALL_FIELDS}
                angles, miss = solver.fit(seed, aim, self.hold, side)
                pose.values.update(angles)
                pose.fit[side] = miss
            report["%.2fs" % t] = pose.fit if pose.aim_l is not None else pose.fit["R"]
        return report


EASE = {
    "smooth": lambda u: u * u * (3 - 2 * u),
    "in": lambda u: u * u,                       # gathers speed: the blow
    "out": lambda u: 1 - (1 - u) * (1 - u),      # loses speed: the lift, the recoil
    "linear": lambda u: u,
}


# ---- the clips ----

def mine():
    """Two overhead blows at a rock in front, low down. Two hands, the left at the butt of the
    haft. Up in front, the upper arms near parallel and the bend in the elbows, until the haft
    points straight back over the head (no further); then over and down, the body folding into
    the blow, until the haft lies level and the point goes straight down into the rock."""
    ready = Pose(**{"raise": 35, "spread": -5, "flex": 70, "cock": -20, "bend": 8, "drop": 0.03, "look": 0.4})
    top = Pose(Aim(grip=(0.10, 0.12, 1.92), haft=(0, -1, 0.05), edge=(0, 0, 1)),
               bend=-4, twist=-6, look=0.2)
    blow = Pose(**{"raise": 68, "spread": -27, "flex": 6, "cock": -25, "wrist": 0,
                   "bend": 40, "twist": 0, "drop": 0.12, "look": 0.7})
    recoil = Pose(**{"raise": 73, "spread": -27, "flex": 12, "cock": -22,
                     "bend": 38, "twist": 0, "drop": 0.11, "look": 0.7})
    keys = []
    for start in (0.0, 1.2):
        keys += [(start + 0.00, ready, "smooth"), (start + 0.50, top, "out"),
                 (start + 0.72, blow, "in"), (start + 0.80, recoil, "out"), (start + 0.88, recoil, "linear")]
    keys += [(2.20, ready, "smooth"), (2.40, ready, "linear")]
    return Clip("mine", "Pickaxe", 2.4, keys, dict(left=-0.075),
                stance=((-0.17, 0.10), (0.21, -0.12), 12), parallel=True)


def chop():
    """Two chops into a trunk in front, one-handed. Cocked by the right ear, the elbow up and out;
    then the hatchet's head goes round the body, out past the right side and in to bite the
    trunk's right side, the edge facing left, the forearm come across in front. The left arm
    hangs free."""
    ready = Pose(**{"raise": 25, "flex": 75, "cock": -10, "bend": 5, "drop": 0.03, "look": 0.3})
    top = Pose(Aim(grip=(0.33, -0.26, 1.78), haft=(-0.15, -0.94, 0.31), edge=(-0.2, 0.33, 0.92), edge_weight=0.5),
               bend=-3, twist=-30, drop=0.01, look=0.15)
    side = Pose(Aim(grip=(0.40, 0.22, 1.22), haft=(0.96, -0.2, -0.1), edge=(0.2, 0.98, 0)),
                bend=4, twist=-5, drop=0.04, look=0.3)
    blow = Pose(Aim(grip=(0.03, 0.45, 1.05), haft=(0.34, 0.93, -0.1), edge=(-0.93, 0.34, 0)),
                bend=10, twist=16, drop=0.07, look=0.4)
    # The blade stops dead in the wood and kicks back: the head springs up and out, the hand
    # comes back towards the body, and the body gives a little before winding up again.
    bounce = Pose(Aim(grip=(0.10, 0.36, 1.13), haft=(0.62, 0.77, 0.12), edge=(-0.77, 0.62, 0), edge_weight=0.5),
                  bend=6, twist=9, drop=0.05, look=0.35)
    settle = Pose(Aim(grip=(0.12, 0.36, 1.12), haft=(0.55, 0.83, 0.1), edge=(-0.83, 0.55, 0), edge_weight=0.5),
                  bend=6, twist=8, drop=0.05, look=0.35)
    keys = []
    for start in (0.0, 1.2):
        keys += [(start + 0.00, ready, "smooth"), (start + 0.40, top, "out"), (start + 0.60, side, "in"),
                 (start + 0.70, blow, "linear"), (start + 0.80, bounce, "out"), (start + 0.95, settle, "smooth")]
    keys += [(2.20, ready, "smooth"), (2.40, ready, "linear")]
    return Clip("chop", "Hatchet", 2.4, keys, {}, stance=((-0.20, 0.06), (0.20, -0.10), 15), hold="right")


# Fishing (docs/skills.md: rod, net, trap and lantern). Water lies a step out from the bank and a
# little below it, so the gear reaches about a metre out and down to the ground or under it.

def rod_grip(grip, haft):
    """A rod in the fist: the thumb along the rod, the knuckles towards the ground (as near as the
    rod's slope allows)."""
    haft = Vector(haft).normalized()
    knuckles = Vector((0, 0.3, -1))
    knuckles = (knuckles - haft * knuckles.dot(haft)).normalized()
    return Aim(grip=grip, haft=haft, edge=knuckles, edge_weight=0.4)


def fish_rod(hold="both", name="fish_rod"):
    """Fly-fishing, both hands on the rod (the left below the right, on the butt), held in front of
    the middle of the body: back past upright, a flick that throws the line out, the rod settling
    over the water, and a twitch of the line while it waits."""
    x = 0.12 if hold == "both" else 0.2   # two hands bring the rod in towards the middle
    y = 0.06 if hold == "both" else 0.0
    ready = Pose(rod_grip((x, 0.34 + y, 1.05), (0, 0.75, 0.66)), bend=4, drop=0.02, look=0.3)
    back = Pose(rod_grip((x + 0.04, 0.16 + y, 1.38), (0.05, -0.12, 0.99)), bend=-4, twist=-6, drop=0.0, look=0.15)
    flick = Pose(rod_grip((x, 0.44 + y, 1.22), (0, 0.87, 0.5)), bend=8, twist=3, drop=0.03, look=0.3)
    settle = Pose(rod_grip((x, 0.38 + y, 1.08), (0, 0.82, 0.57)), bend=6, drop=0.03, look=0.35)
    twitch = Pose(rod_grip((x, 0.35 + y, 1.14), (0, 0.74, 0.67)), bend=5, drop=0.03, look=0.35)
    keys = [(0.00, ready, "smooth"), (0.35, back, "out"), (0.52, flick, "in"), (0.80, settle, "out"),
            (1.45, settle, "linear"), (1.60, twitch, "out"), (1.78, settle, "smooth"),
            (2.20, ready, "smooth"), (2.40, ready, "linear")]
    return Clip(name, "rod", 2.4, keys, dict(left=-0.24), stance=((-0.16, 0.08), (0.18, -0.04), 12), hold=hold)


def fish_rod_lantern():
    """The rod's cast one-handed, with a lantern held out over the water in the left (lightfin)."""
    return fish_rod(hold="lantern", name="fish_rod_lantern")


def fish_net():
    """A landing net on a long pole (images/fishing-net.jpg). The body stands turned 50 degrees left
    of the water, feet wide; the pole runs across the front of the body, from the butt at the
    left of the chest to the water at the right, sloping down, both hands over it, the bag hanging
    into the water. Out over the water, down into it, a sweep, up out with a shake. Held as a
    spear is, against the leverage: the front (right) hand supports the pole from below, palm up,
    at the waist, the hoop 1.1 m on past its thumb; the back (left) hand holds the butt from
    above, palm down (the net's model is made to match, its origin at the right hand). The eyes
    follow the hoop. Keys are in the body's frame: the water lies about 50 degrees to its right."""
    def pole(grip, run, slope, in_water=False):
        """The right fist at grip; the pole running out `run` degrees right of the body's front,
        sloping down by `slope` degrees towards the hoop. In the water, the hoop's place (where
        that pole would put it) is what counts, and the hands go wherever puts it there."""
        r, sl = math.radians(run), math.radians(slope)
        towards_hoop = Vector((math.sin(r) * math.cos(sl), math.cos(r) * math.cos(sl), -math.sin(sl)))
        if in_water:
            return Aim(grip=Vector(grip) + towards_hoop * 1.32, haft=towards_hoop, edge=(0, 0, 1), edge_weight=0,
                       at=1.32, haft_weight=0.3, palm=(0, 0, 1))
        return Aim(grip=grip, haft=towards_hoop, edge=(0, 0, 1), edge_weight=0, palm=(0, 0, 1))
    # The shoulders turn a little towards the water (twist to the right); the head does the rest.
    ready = Pose(pole((0.25, 0.34, 0.98), 72, 32), bend=10, twist=-14, drop=0.07)
    dip = Pose(pole((0.22, 0.45, 0.78), 72, 52), bend=30, twist=-15, drop=0.16)
    sweep = Pose(pole((0.24, 0.48, 0.78), 58, 50), bend=30, twist=-6, drop=0.16)
    rise = Pose(pole((0.25, 0.38, 0.95), 64, 36), bend=14, twist=-10, drop=0.09)
    lift = Pose(pole((0.25, 0.32, 1.06), 72, 16), bend=6, twist=-14, drop=0.05)
    shake = Pose(pole((0.25, 0.32, 1.02), 72, 21), bend=7, twist=-14, drop=0.06)
    keys = [(0.00, ready, "smooth"), (0.45, dip, "smooth"), (0.95, sweep, "smooth"), (1.20, rise, "smooth"),
            (1.45, lift, "out"), (1.62, shake, "linear"), (1.74, lift, "linear"), (1.86, shake, "linear"),
            (2.20, ready, "smooth"), (2.40, ready, "linear")]
    return Clip("fish_net", "net", 2.4, keys, dict(left=-0.42), stance=((-0.30, -0.06), (0.26, 0.14), 18),
                facing=50, left_palm=(0, 0, -1), gaze=1.32)


def fish_trap():
    """Setting an eel trap: a wicker trap carried across the body by its ends, a squat at the
    bank to lower it to the water, let go, and up again. Played backwards, it hauls one out
    (fish_trap_collect in animations.json)."""
    ready = Pose(Aim(grip=(0.24, 0.30, 1.00), haft=(-1, 0, 0), edge=(0, 0, 1), edge_weight=0),
                 bend=4, drop=0.02, look=0.3)
    low = Pose(Aim(grip=(0.24, 0.55, 0.40), haft=(-1, 0, 0), edge=(0, 0, 1), edge_weight=0),
               bend=55, drop=0.36, look=0.5)
    let_go = Pose(Aim(grip=(0.25, 0.50, 0.48), haft=(-1, 0, 0), edge=(0, 0, 1), edge_weight=0),
                  bend=50, drop=0.34, look=0.5)
    keys = [(0.00, ready, "smooth"), (0.95, low, "smooth"), (1.30, low, "linear"), (1.50, let_go, "out"),
            (2.25, ready, "smooth"), (2.40, ready, "linear")]
    return Clip("fish_trap", "trap", 2.4, keys, dict(left=0.5), stance=((-0.20, 0.12), (0.20, -0.06), 16),
                slide=False)


ALL = [mine, chop, fish_rod, fish_rod_lantern, fish_net, fish_trap]
CLIPS = globals().get("CLIPS", [c.__name__ for c in ALL])


# ---- solving a frame ----

def rot_x(deg): return Matrix.Rotation(math.radians(deg), 3, "X")
def rot_z(deg): return Matrix.Rotation(math.radians(deg), 3, "Z")
def rot(axis, deg): return Matrix.Rotation(math.radians(deg), 3, axis)


def frame3(y, z_hint, x_hint=None):
    """A rotation whose Y is y and whose Z is as close to z_hint as it can be (or X to x_hint)."""
    y = y.normalized()
    if x_hint is not None:
        z = x_hint.cross(y).normalized()
    else:
        z = (z_hint - y * z_hint.dot(y)).normalized()
    x = y.cross(z).normalized()
    return Matrix((x, y, z)).transposed()


def two_bone(start, target, l1, l2, pole):
    """Where the middle joint goes, reaching from start to target and bending towards pole."""
    d = target - start
    dist = min(d.length, (l1 + l2) * 0.999)
    axis = d.normalized()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    p = (pole - axis * pole.dot(axis)).normalized()
    return start + axis * a + p * h, start + axis * dist


def nelder_mead(cost, x0, step, iterations=600):
    """Minimise cost over a list of floats (no scipy in Blender)."""
    n = len(x0)
    simplex = [list(x0)] + [[x + (step[j] if i == j else 0) for j, x in enumerate(x0)] for i in range(n)]
    values = [cost(x) for x in simplex]
    for _ in range(iterations):
        order = sorted(range(n + 1), key=lambda i: values[i])
        simplex, values = [simplex[i] for i in order], [values[i] for i in order]
        centre = [sum(x[j] for x in simplex[:-1]) / n for j in range(n)]
        worst = simplex[-1]
        reflect = [c + (c - w) for c, w in zip(centre, worst)]
        fr = cost(reflect)
        if fr < values[0]:
            expand = [c + 2 * (c - w) for c, w in zip(centre, worst)]
            fe = cost(expand)
            simplex[-1], values[-1] = (expand, fe) if fe < fr else (reflect, fr)
        elif fr < values[-2]:
            simplex[-1], values[-1] = reflect, fr
        else:
            contract = [c + 0.5 * (w - c) for c, w in zip(centre, worst)]
            fc = cost(contract)
            if fc < values[-1]:
                simplex[-1], values[-1] = contract, fc
            else:
                best = simplex[0]
                simplex = [best] + [[b + 0.5 * (x - b) for b, x in zip(best, s)] for s in simplex[1:]]
                values = [values[0]] + [cost(s) for s in simplex[1:]]
    i = min(range(n + 1), key=lambda i: values[i])
    return simplex[i], values[i]


class Solver:
    def __init__(self, rig):
        self.rig = rig
        self.rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}
        self.order = [b.name for b in rig.data.bones]   # parents come before children
        head = lambda n: self.rest[n].to_translation()
        self.len = {n: (head(c) - head(n)).length for n, c in
                    [("upper_arm.L", "forearm.L"), ("forearm.L", "hand.L"), ("upper_arm.R", "forearm.R"),
                     ("forearm.R", "hand.R"), ("thigh.L", "shin.L"), ("shin.L", "foot.L"),
                     ("thigh.R", "shin.R"), ("shin.R", "foot.R")]}
        self.swivel = {}   # the left elbow's last angle round its circle, to keep it from jumping
        self.thumb_l = None  # which way the left thumb pointed along the haft last frame
        self.last_slide = None  # how far the left fist had slid along the haft last frame
        self.thumb_sign = None  # the clip's left grip (choose_grip); None tries both every frame
        self.passes = 12        # rounds of settling the left hand's turn against the arm's reach
        self.regrip = 5.0       # the cost of the left fist changing grip mid-swing (thumb_sign None)
        self.slide_rate = 0.015  # metres the left fist may slide along a pole in a frame
        self.final_place = True  # put the wrist where the settled fist closes on the haft
        self.force_swivel = None  # set while keying along the smoothed course (track_left)
        self.force_slide = None
        self.left_cost = 0.0
        self.gap = {}      # how far each hand fell short of its grip in the last solve (metres)
        # Where each grip sits in its hand's frame.
        self.grip = {s: self.rest["hand." + s].inverted() @ self.rest["grip." + s].to_translation() for s in "LR"}

    def reset(self):
        """Forget the last frame (a new clip)."""
        self.swivel, self.thumb_l, self.last_slide, self.left_cost = {}, None, None, 0.0

    def track_left(self, clip, frames):
        """The left arm's course through a two-handed clip, smoothed. Each frame's own best elbow
        (round its circle) and slide along the haft, found one frame at a time, step from one
        setting to the next, which shows as a twitchy elbow; so they are found for every frame
        first, then eased over their neighbours (round the loop's end, as the clip loops), and the
        clip is keyed along the eased course (force_swivel, force_slide)."""
        self.force_swivel = self.force_slide = None
        if clip.hold != "both":
            return None
        self.choose_grip(clip, frames)
        for f in range(frames - 4, frames + 1):   # a warm-up on the loop's end, which leads into its start
            self.solve(clip.at(f / FPS), clip)
        swivels, slides = [], []
        for f in range(frames):
            self.solve(clip.at(f / FPS), clip)
            swivels.append(self.swivel["L"])
            slides.append(self.slide)
        for i in range(1, frames):   # unwrap, so 355 then 0 reads as a step of 5
            while swivels[i] - swivels[i - 1] > 180:
                swivels[i] -= 360
            while swivels[i] - swivels[i - 1] < -180:
                swivels[i] += 360
        drift = swivels[-1] + (swivels[-1] - swivels[-2]) - swivels[0]   # how far the loop's end has wound
        weights = [math.exp(-0.5 * (k / SMOOTH_FRAMES) ** 2) for k in range(-3 * int(SMOOTH_FRAMES), 3 * int(SMOOTH_FRAMES) + 1)]
        half = len(weights) // 2

        def eased(values, wind=0.0):
            out = []
            for i in range(frames):
                total = 0.0
                for k, w in enumerate(weights):
                    j = i + k - half
                    turns = math.floor(j / frames)
                    total += w * (values[j % frames] + turns * wind)
                out.append(total / sum(weights))
            return out

        self.track = list(zip(eased(swivels, round(drift / 360) * 360), eased(slides)))
        self.track.append(self.track[0])   # the last frame is the first again
        return self.track

    def choose_grip(self, clip, frames):
        """Which way round the left fist takes the haft for this clip (+1: thumb towards the haft's
        far end, as the right's; -1: the other way): the one with the less strain over all of it
        (the forearm's twist and the wrist's tilt, see strain())."""
        self.thumb_sign = None
        if clip.hold != "both":
            return None
        totals = {}
        for sign in (1, -1):
            self.thumb_sign = sign
            self.reset()
            total = 0.0
            for f in list(range(frames - 4, frames + 1)) + list(range(frames + 1)):
                self.solve(clip.at(f / FPS), clip)
                twist, tilt = self.strain().get("L", (0, 90))
                # The worst moments count most: a hard wring for a few frames is what shows.
                total += (max(0, twist - 30) / 30) ** 4 + ((max(0, 55 - tilt) + max(0, tilt - 115)) / 20) ** 4
                if clip.left_palm is not None:   # and a palm facing the wrong way is the wrong grip
                    facing = -self.checks[-1][3].col[2]
                    total += 4 * (math.degrees(facing.angle(clip.left_palm, 0)) / 60) ** 2
            totals[sign] = total
        self.thumb_sign = min(totals, key=totals.get)
        self.reset()
        return self.thumb_sign

    def rr(self, name):
        return self.rest[name].to_3x3()

    def placer(self, out):
        def place(name, rotation, at=None):
            if at is None:
                parent = self.parent[name]
                at = out[parent] @ (self.rest[parent].inverted() @ self.rest[name].to_translation())
            out[name] = Matrix.Translation(at) @ rotation.to_4x4()
        return place

    def body(self, p, out):
        """Hips to head: the hips carry a share of the bend and twist; the spine and chest the rest."""
        place, rr = self.placer(out), self.rr
        hips_at = self.rest["hips"].to_translation() + Vector((0, p["shift"], -p["drop"]))
        place("hips", rot_z(p["twist"] * 0.35) @ rot_x(-p["bend"] * 0.15) @ rr("hips"), hips_at)
        place("spine", rot_z(p["twist"] * 0.65) @ rot_x(-p["bend"] * 0.55) @ rr("spine"))
        place("chest", rot_z(p["twist"]) @ rot_x(-p["bend"]) @ rr("chest"))
        place("neck", rot_z(p["twist"]) @ rot_x(-p["bend"] - 20 * p["look"]) @ rr("neck"))
        place("head", rot_z(p["twist"] * 0.8) @ rot_x(-p["bend"] - 45 * p["look"]) @ rr("head"))
        shoulder = lambda s: out["chest"] @ (self.rest["chest"].inverted() @ self.rest["upper_arm." + s].to_translation())
        skull = out["head"] @ Vector((0, 0.12, 0))     # 12 cm up the head bone: the skull's middle
        self.spine = (out["hips"].to_translation(), out["neck"].to_translation())
        return shoulder("L"), shoulder("R"), skull

    def gaze_at(self, out, p, target):
        """Turn the neck and head to look at target: the head turns up to GAZE_TURN either way of
        the chest and tilts as far as it needs; the neck takes GAZE_NECK of both."""
        rr = self.rr
        eyes = out["head"] @ Vector((0, 0.1, 0))
        d = target - eyes
        yaw = math.degrees(math.atan2(-d.x, d.y))                       # positive: to the left
        chest_yaw = p["twist"]
        yaw = chest_yaw + max(-GAZE_TURN, min(GAZE_TURN, (yaw - chest_yaw + 180) % 360 - 180))
        pitch = math.degrees(math.atan2(d.z, math.hypot(d.x, d.y)))     # negative: down
        place = self.placer(out)
        neck_yaw = chest_yaw + (yaw - chest_yaw) * GAZE_NECK
        neck_pitch = -p["bend"] + (pitch + p["bend"]) * GAZE_NECK
        place("neck", rot_z(neck_yaw) @ rot_x(neck_pitch) @ rr("neck"))
        place("head", rot_z(yaw) @ rot_x(pitch) @ rr("head"))

    def right_arm(self, p, chest):
        """The right arm's rotations and joints from its angles (see the module notes)."""
        rr = self.rr
        body = chest.to_3x3() @ rr("chest").inverted()
        right_axis, fwd_axis = body @ Vector((1, 0, 0)), body @ Vector((0, 1, 0))
        hanging = body @ rot(Vector((0, 1, 0)), 90) @ rr("upper_arm.R")
        upper = rot(right_axis, p["raise"]) @ hanging
        upper = rot(fwd_axis, -p["spread"]) @ upper
        upper = rot(upper.col[1], p["roll"]) @ upper
        fore = upper @ rr("upper_arm.R").inverted() @ rr("forearm.R")
        fore = rot(upper.col[2], p["flex"]) @ fore
        turned = rot(fore.col[1], p["turn"]) @ fore
        hand = turned @ rr("forearm.R").inverted() @ rr("hand.R")
        # The turn builds up along the forearm, as the radius rolls over the ulna: the forearm's
        # bone takes half of it and the wrist the rest, so the skin isn't wrung at the elbow.
        fore = rot(fore.col[1], p["turn"] * TWIST_SHARE) @ fore
        hand = rot(hand.col[0], -p["wrist"]) @ hand    # the palm is the hand's -Z
        hand = rot(hand.col[2], p["cock"]) @ hand      # the thumb is the hand's -X
        shoulder = chest @ (self.rest["chest"].inverted() @ self.rest["upper_arm.R"].to_translation())
        elbow = shoulder + upper.col[1] * self.len["upper_arm.R"]
        wrist = elbow + fore.col[1] * self.len["forearm.R"]
        rest_hand = rr("hand.R").inverted()
        tool = {"grip": wrist + hand @ self.grip["R"],
                "haft": (hand @ rest_hand @ Vector((0, 1, 0))).normalized(),   # out past the thumb
                "edge": (hand @ rest_hand @ Vector((1, 0, 0))).normalized(),  # off the knuckles
                "palm": -hand.col[2].normalized()}
        return upper, fore, hand, shoulder, elbow, wrist, tool

    def arm_fk(self, p, chest, side):
        """An arm's rotations and joints from its angles: the right's from raise, flex...; the
        left's from l_raise, l_flex..., worked out as a right arm and mirrored across the chest's
        own midline, so both have the same joints and limits."""
        if side == "R":
            return self.right_arm(p, chest)
        q = dict(p)
        q.update({f: p["l_" + f] for f in ARM_FIELDS})
        upper, fore, hand, shoulder, elbow, wrist, tool = self.right_arm(q, chest)
        k = chest @ self.rest["chest"].inverted()
        flip = Matrix.Diagonal((-1, 1, 1, 1))
        mirror = k @ flip @ k.inverted()              # reflection in the chest's midline
        m3, f3 = mirror.to_3x3(), flip.to_3x3()
        bone = lambda r: m3 @ r @ f3                  # a mirrored bone: its X turns round, Y and Z follow
        tool_l = {"grip": mirror @ tool["grip"], "haft": m3 @ tool["haft"], "edge": m3 @ tool["edge"],
                  "palm": m3 @ tool["palm"]}
        return bone(upper), bone(fore), bone(hand), mirror @ shoulder, mirror @ elbow, mirror @ wrist, tool_l

    def fit(self, seed, aim, hold="right", side="R"):
        """The right arm's angles that bring the tool closest to the aim, within LIMITS, with the
        elbow pointing out (not in at the chest) and, for two hands, the haft where the left fist
        can reach it."""
        out = {}
        sl, _, skull = self.body(seed, out)
        chest = out["chest"]
        body = chest.to_3x3() @ self.rr("chest").inverted()

        fields = ARM_FIELDS if side == "R" else ARM_FIELDS_L

        def angles(x):
            return dict(zip(fields, x))

        def cost(x):
            p = dict(seed)
            p.update(angles(x))
            upper, fore, hand, shoulder, elbow, wrist, tool = self.arm_fk(p, chest, side)
            j = {f: p[g] for f, g in zip(ARM_FIELDS, fields)}   # this arm's joints, by plain name
            c = ((tool["grip"] + tool["haft"] * aim.at - aim.grip).length / 0.02) ** 2
            c += aim.haft_weight * (math.degrees(tool["haft"].angle(aim.haft, 0)) / 4) ** 2
            c += aim.edge_weight * (math.degrees(tool["edge"].angle(aim.edge, 0)) / 8) ** 2
            if aim.palm is not None:
                c += (math.degrees(tool["palm"].angle(aim.palm, 0)) / 15) ** 2
            for f, v in zip(ARM_FIELDS, x):
                lo, hi = LIMITS[f]
                c += (max(0, lo - v, v - hi) / 2) ** 2   # (the left's mirror the right's)
            for point in (elbow, wrist):
                c += (max(0.0, SKULL_CLEAR - (point - skull).length) / 0.01) ** 2
                c += (max(0.0, 1.0 - torso_clear(point, self.spine, body)) / 0.05) ** 2
            c += (j["spread"] / 45) ** 2 + (j["roll"] / 60) ** 2     # the upper arm straight out
            c += 0.3 * ((j["wrist"] / 60) ** 2 + (j["cock"] / 40) ** 2) + 0.2 * (j["turn"] / 90) ** 2
            c += (max(0.0, abs(j["turn"]) - 60) / 10) ** 2     # a full palm-down or palm-up only when needed
            c += (max(0.0, elbow_inward(shoulder, elbow, wrist, body, side) - 0.15) / 0.05) ** 2
            if hold == "both" and side == "R":
                reach = (tool["grip"] + tool["haft"] * p["left"] - sl).length
                c += (max(0.0, reach - LEFT_REACH) / 0.01) ** 2
                c += (max(0.0, LEFT_CLOSEST - reach) / 0.01) ** 2   # nor folded up against the shoulder
            return c

        starts = [[seed[f] for f in fields]]
        starts += [[90, 0, 0, 60, 0, 0, 0], [140, 10, 0, 90, 0, 0, 0], [60, 60, 60, 60, 60, 0, 0], [60, 20, -40, 30, -60, 0, 0]]
        best = None
        for x0 in starts:
            x, c = nelder_mead(cost, x0, [25] * len(ARM_FIELDS))
            x, c = nelder_mead(cost, x, [5] * len(ARM_FIELDS))
            if best is None or c < best[1]:
                best = (x, c)
        x = best[0]
        p = dict(seed)
        p.update(angles(x))
        tool = self.arm_fk(p, chest, side)[6]
        miss = {"grip_cm": round((tool["grip"] + tool["haft"] * aim.at - aim.grip).length * 100, 1),
                "haft_deg": round(math.degrees(tool["haft"].angle(aim.haft, 0))),
                "edge_deg": round(math.degrees(tool["edge"].angle(aim.edge, 0))),
                "angles": {f: round(v) for f, v in angles(x).items()}}
        return {f: v for f, v in angles(x).items()}, miss

    def solve(self, p, clip):
        """Armature-space matrices for every bone, from a Pose's scalars, for the clip's hold,
        stance and facing."""
        stance, hold, turned = clip.stance, clip.hold, clip.facing
        out = {}
        self.checks = []
        place = self.placer(out)
        sl, sr, skull = self.body(p, out)

        upper, fore, hand, shoulder, elbow, wrist, tool = self.right_arm(p, out["chest"])
        place("upper_arm.R", upper, shoulder)
        place("forearm.R", fore, elbow)
        place("hand.R", hand, wrist)
        place("grip.R", hand @ self.rr("hand.R").inverted() @ self.rr("grip.R"))
        self.tool = tool

        if hold == "dual":
            # A weapon in each hand: the left arm is posed joint by joint too (l_raise...).
            upper_l, fore_l, hand_l, shoulder_l, elbow_l, wrist_l, self.tool_l = self.arm_fk(p, out["chest"], "L")
            place("upper_arm.L", upper_l, shoulder_l)
            place("forearm.L", fore_l, elbow_l)
            place("hand.L", hand_l, wrist_l)
            place("grip.L", hand_l @ self.rr("hand.L").inverted() @ self.rr("grip.L"))
        elif hold == "both":
            # The left fist closes round the haft; its knuckles run on from the forearm as far as
            # the haft allows.
            haft = tool["haft"]
            self.slide = 0.0
            if self.force_slide is not None:   # the baked, smoothed track (track_left)
                self.slide = self.force_slide
            elif clip.slide:
                # On a pole the left fist may slide along it (up to 20 cm, never onto the right
                # fist) to keep its reach between folded shut and straight. It slides no more than
                # 1.5 cm a frame.
                def reach(offset):
                    return (tool["grip"] + haft * (p["left"] + offset) - sl).length
                last_slide = self.last_slide if self.last_slide is not None else 0.0
                slides = [i * 0.01 * sign for i in range(21) for sign in (1, -1)]
                slides = [d for d in slides if abs(p["left"] + d) >= 0.09 and (p["left"] + d) * p["left"] > 0
                          and abs(d - last_slide) <= self.slide_rate + 1e-9]
                if slides:
                    self.slide = min(slides, key=lambda d: (round(max(0.0, LEFT_CLOSEST - reach(d)) + max(0.0, reach(d) - LEFT_REACH), 3), abs(d)))
                else:
                    self.slide = last_slide
            self.last_slide = self.slide
            target = tool["grip"] + haft * (p["left"] + self.slide)
            body = out["chest"].to_3x3() @ self.rr("chest").inverted()
            mirror = body @ Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1))) @ body.inverted()
            parallel = mirror @ upper.col[1] if clip.parallel else None
            left_palm = clip.left_palm   # in the body's frame, which is the world's until the clip turns it
            # The fist takes the haft either way round (thumb up it or down it, as a shovel's back
            # hand does): whichever the whole clip finds easier (choose_grip), never changing
            # mid-swing.
            last = self.swivel.get("L")
            best = None
            for thumb in ((haft, -haft) if self.thumb_sign is None else (haft * self.thumb_sign,)):
                self.swivel["L"] = last
                hand_rot = frame3(tool["edge"], None, x_hint=thumb)
                # The hand's turn and the arm's reach depend on each other: settle them in turn,
                # then put the wrist where the settled hand's fist closes on the haft exactly.
                for _ in range(self.passes):
                    hand_at = target - hand_rot @ self.grip["L"]
                    elbow_l, wrist_l = self.elbow("L", sl, hand_at, thumb, out["chest"].to_3x3(), skull, parallel,
                                                  only=self.force_swivel, palm=left_palm)
                    forearm = (wrist_l - elbow_l).normalized()
                    knuckles = forearm - thumb * forearm.dot(thumb)
                    settled = frame3(knuckles if knuckles.length > 1e-4 else tool["edge"], None, x_hint=thumb)
                    moved = (settled.col[1] - hand_rot.col[1]).length
                    hand_rot = settled
                    if moved < 1e-3:
                        break
                if self.final_place:
                    hand_at = target - hand_rot @ self.grip["L"]
                    elbow_l, wrist_l = self.elbow("L", sl, hand_at, thumb, out["chest"].to_3x3(), skull, parallel,
                                                  only=self.trial, palm=left_palm)
                cost = self.best_cost
                if self.thumb_l is not None and self.thumb_l.dot(thumb) < 0:
                    cost += self.regrip   # changing grip mid-swing
                if best is None or cost < best[0]:
                    best = (cost, self.trial, elbow_l, wrist_l, hand_rot, thumb)
            self.left_cost, self.swivel["L"], elbow_l, wrist_l, hand_rot, self.thumb_l = best
            self.arm(place, "L", sl, elbow_l, wrist_l, hand_rot)
        elif hold == "lantern":
            # The left hand holds a lantern out over the water, thumb up, the lantern hanging below.
            body = out["chest"].to_3x3() @ self.rr("chest").inverted()
            hand_at = sl + body @ Vector((-0.12, 0.42, -0.14))
            elbow_l, wrist_l = two_bone(sl, hand_at, self.len["upper_arm.L"], self.len["forearm.L"],
                                        body @ Vector((-0.6, -0.2, -1)))
            forearm = wrist_l - elbow_l
            up = body @ Vector((0, 0, 1))
            self.arm(place, "L", sl, elbow_l, wrist_l, frame3(forearm, None, x_hint=up - forearm.normalized() * up.dot(forearm.normalized())))
        else:
            # The free arm hangs a little forward, the hand turned in to the thigh.
            hand_at = sl + rot_z(p["twist"] * 0.5) @ Vector((0.02, 0.10, -0.52))
            elbow_l, wrist_l = two_bone(sl, hand_at, self.len["upper_arm.L"], self.len["forearm.L"],
                                        rot_z(p["twist"]) @ Vector((-0.3, -1, 0)))
            self.arm(place, "L", sl, elbow_l, wrist_l, frame3(wrist_l - elbow_l, rot_z(p["twist"]) @ Vector((-1, 0, 0))))

        if clip.gaze is not None:
            self.gaze_at(out, p, tool["grip"] + tool["haft"] * clip.gaze)

        # Legs: feet planted at the stance, knees towards the toes.
        (lx, ly), (rx, ry), toe_out = stance
        rr = self.rr
        for s, (fx, fy), out_sign in (("L", (lx, ly), 1), ("R", (rx, ry), -1)):
            thigh, shin, foot, toe = "thigh." + s, "shin." + s, "foot." + s, "toe." + s
            foot_rest = self.rest[foot].to_translation()
            foot_at = Vector((fx, fy + foot_rest.y + p["step_" + s.lower()], foot_rest.z + p["lift_" + s.lower()]))
            hip = out["hips"] @ (self.rest["hips"].inverted() @ self.rest[thigh].to_translation())
            facing = rot_z(toe_out * out_sign) @ Vector((0, 1, 0))
            knee, ankle = two_bone(hip, foot_at, self.len[thigh], self.len[shin], facing + Vector((0, 0, 0.2)))
            side_axis = rot_z(toe_out * out_sign) @ Vector((1, 0, 0))
            place(thigh, frame3(knee - hip, None, x_hint=side_axis))
            place(shin, frame3(ankle - knee, None, x_hint=side_axis), knee)
            place(foot, rot_z(toe_out * out_sign) @ rr(foot), ankle)
            place(toe, rot_z(toe_out * out_sign) @ rr(toe))
        if turned:   # the whole body stands turned from the game's facing (Clip.facing)
            turn = rot_z(turned).to_4x4()
            for name in out:
                out[name] = turn @ out[name]
            self.tool = {k: (turn.to_3x3() @ v) for k, v in self.tool.items()}
        return out

    def elbow(self, s, shoulder, hand_at, haft, chest, skull, parallel, only=None, palm=None):
        """The left elbow for a hand at hand_at holding a haft: tried all the way round the circle
        it can sit on, scored by the forearm's twist (0 when the haft points the way the thumb
        naturally does, away from the upper arm), the wrist's tilt (the haft 50-110 degrees off the
        forearm), the upper arm near parallel to the right one (if asked), clear of the head and the body,
        and staying near the last frame's place. `only` scores just that angle round the circle."""
        l1, l2 = self.len["upper_arm." + s], self.len["forearm." + s]
        d = hand_at - shoulder
        dist = min(d.length, (l1 + l2) * 0.999)
        axis = d.normalized()
        a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
        h = math.sqrt(max(l1 * l1 - a * a, 0.0))
        down = Vector((0, 0, -1)) - axis * axis.dot(Vector((0, 0, -1)))
        e1 = down.normalized() if down.length > 1e-3 else (chest @ Vector((0, -1, 0))).normalized()
        e2 = axis.cross(e1)
        centre, wrist = shoulder + axis * a, shoulder + axis * dist
        outward = chest @ Vector((1 if s == "R" else -1, 0, 0))
        best = None
        for theta in ([only] if only is not None else [i * 2.5 for i in range(144)]):
            r = math.radians(theta)
            elbow = centre + (e1 * math.cos(r) + e2 * math.sin(r)) * h
            u, f = elbow - shoulder, (wrist - elbow).normalized()
            u_perp = u - f * u.dot(f)
            h_perp = haft - f * haft.dot(f)
            twist = 0.0
            if u_perp.length > 1e-4 and h_perp.length > 1e-4:
                twist = math.degrees((-u_perp).angle(h_perp))
            tilt = math.degrees(f.angle(haft))
            cost = (twist / 60) ** 2 + ((max(0, 50 - tilt) + max(0, tilt - 110)) / 20) ** 2
            if palm is not None:   # the left hand: X the thumb (haft here), Y the knuckles, palm -Z
                knuckles = f - haft * f.dot(haft)
                if knuckles.length > 1e-4:
                    facing = -haft.cross(knuckles.normalized())
                    cost += (math.degrees(facing.angle(palm, 0)) / 30) ** 2
            if parallel is not None:
                cost += (math.degrees(u.angle(parallel, 0)) / 35) ** 2
            cost += (max(0.0, SKULL_CLEAR - (elbow - skull).length) / 0.02) ** 2
            cost += (max(0.0, 1.0 - torso_clear(elbow, self.spine, chest @ self.rr("chest").inverted())) / 0.1) ** 2
            if wrist.z < shoulder.z:   # elbows hang down; they rise only for work above the shoulder
                cost += (max(0.0, elbow_up(shoulder, elbow, wrist) - 0.3) / 0.15) ** 2
            cost += 6 * max(0.0, -(elbow - shoulder).dot(outward) - 0.02)   # not into the body
            body = chest @ self.rr("chest").inverted()
            cost += (max(0.0, elbow_inward(shoulder, elbow, wrist, body, s) - 0.15) / 0.05) ** 2
            last = self.swivel.get(s)
            if last is not None:
                step = abs((theta - last + 180) % 360 - 180)
                cost += (step / 25) ** 2
            if best is None or cost < best[0]:
                best = (cost, theta, elbow)
        self.trial, self.best_cost = best[1], best[0]
        self.gap[s] = max(0.0, d.length - (l1 + l2))
        return best[2], wrist

    def arm(self, place, s, shoulder, elbow, wrist, hand_rot):
        """An IK arm's bones: upper arm and forearm share the elbow's hinge; the forearm turns with
        the hand (a wrist bends but doesn't wring), so all the twist is the forearm's own."""
        upper, fore = "upper_arm." + s, "forearm." + s
        u, f = elbow - shoulder, wrist - elbow
        hinge = u.cross(f)
        if hinge.length < 1e-3 * u.length * f.length:   # a straight arm: keep the hand's turn
            hinge = hand_rot.col[2] - u.normalized() * hand_rot.col[2].dot(u.normalized())
        elif s == "L":
            hinge = -hinge
        place(upper, frame3(u, hinge.normalized()), shoulder)
        # The forearm's bone takes a share of the turn between the elbow's hinge and the hand
        # (TWIST_SHARE); the wrist the rest.
        fn = f.normalized()
        h_perp = hinge - fn * hinge.dot(fn)
        z_perp = hand_rot.col[2] - fn * hand_rot.col[2].dot(fn)
        z = h_perp.normalized()
        if h_perp.length > 1e-4 and z_perp.length > 1e-4:
            angle = h_perp.angle(z_perp)
            axis = 1.0 if h_perp.cross(z_perp).dot(fn) >= 0 else -1.0
            z = rot(fn, math.degrees(angle) * TWIST_SHARE * axis) @ h_perp.normalized()
        place(fore, frame3(f, z), elbow)
        place("hand." + s, hand_rot, wrist)
        place("grip." + s, hand_rot @ self.rr("hand." + s).inverted() @ self.rr("grip." + s))
        self.checks.append((s, u, f, hand_rot))

    def strain(self):
        """Per IK hand, the forearm's twist away from the natural turn and the wrist's tilt (the
        haft's angle off the forearm; 90 is a straight wrist), in degrees."""
        out = {}
        for s, u, f, hand_rot in self.checks:
            f = f.normalized()
            thumb = hand_rot.col[0] * (-1 if s == "R" else 1)
            u_perp, t_perp = u - f * u.dot(f), thumb - f * thumb.dot(f)
            twist = math.degrees((-u_perp).angle(t_perp, 0)) if u_perp.length > 1e-4 and t_perp.length > 1e-4 else 0
            out[s] = (round(twist), round(math.degrees(f.angle(thumb))))
        return out

    def apply(self, mats, frame):
        """Key every bone's rotation (and the hips' location) from armature-space matrices."""
        R = self.rest
        for name in self.order:
            pb = self.rig.pose.bones[name]
            parent = self.parent[name]
            if parent:
                local_rest = R[parent].inverted() @ R[name]
                basis = local_rest.inverted() @ mats[parent].inverted() @ mats[name]
            else:
                basis = R[name].inverted() @ mats[name]
            loc, q, _ = basis.decompose()
            pb.rotation_mode = "QUATERNION"
            if frame > 0:   # keep quaternions on one side, so they interpolate the short way
                prev = pb.rotation_quaternion
                if prev.dot(q) < 0:
                    q.negate()
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            if parent is None:
                pb.location = loc
                pb.keyframe_insert("location", frame=frame)


# ---- building and exporting ----

def hold_tool(rig, model, side="R"):
    """The clip's tool in a fist (grip.<side>), for the preview only (the game attaches its own)."""
    path = os.path.join(ITEMS, model + ".fbx")
    if not os.path.exists(path):
        return None
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    tool = next((o for o in new if o.type == "MESH"), None)
    for o in new:
        if o is not tool:
            bpy.data.objects.remove(o, do_unlink=True)
    if tool is None:
        return None
    tool["rth_preview_tool"] = True
    tool.parent = None
    # Held art: handle along its -Y, business side +Z; in a T-posed fist the handle runs forward
    # (+Y) and the business side out along the arm (+X for the right, -X for the left)
    # (HeldItemArt.Hold).
    out = 1 if side == "R" else -1
    held = Matrix(((0, 0, out), (0, -1, 0), (out, 0, 0))).to_4x4()
    grip = rig.data.bones["grip." + side]
    imported = tool.matrix_basis.copy()   # the importer's turn back from FBX's axes
    at_rest = Matrix.Translation(grip.matrix_local.to_translation()) @ held @ imported
    tool.parent = rig
    tool.parent_type = "BONE"
    tool.parent_bone = "grip." + side
    # A bone's children hang from its tail, in the bone's own axes.
    tool.matrix_parent_inverse = Matrix()
    tool.matrix_basis = (grip.matrix_local @ Matrix.Translation((0, grip.length, 0))).inverted() @ at_rest
    return tool


# Stand-ins for fishing gear that has no model yet, for the preview only: parts laid out from the
# grip along the haft (u, out past the thumb), the knuckles (v) and across them (w), in metres.
STANDINS = {
    "rod": [("rod", (-0.32, 0, 0), (2.0, 0, 0), 0.014, 0.005), ("rod", (2.0, 0, 0), (2.0, 0, -0.9), 0.002, 0.002)],
    "net": [("rod", (-0.52, 0, 0), (1.1, 0, 0), 0.018, 0.016), ("hoop", (1.32, 0, 0), 0.22),
            ("rod", (1.32, 0, 0), (1.32, -0.4, 0), 0.11, 0.01)],
    "trap": [("rod", (0.03, 0, 0), (0.47, 0, 0), 0.12, 0.09)],
    "lantern": [("rod", (-0.02, 0, 0), (-0.07, 0, 0), 0.008, 0.008), ("box", (-0.17, 0, 0), (0.1, 0.1, 0.1))],
}
STANDIN_COLOURS = {"rod": (0.35, 0.22, 0.1, 1), "net": (0.45, 0.35, 0.2, 1), "trap": (0.55, 0.45, 0.25, 1),
                   "lantern": (0.95, 0.75, 0.3, 1)}


def standin(rig, kind, side="R"):
    """A rough stand-in for a piece of gear with no model, held at grip.<side> (preview only)."""
    import bmesh
    grip = rig.data.bones["grip." + side]
    hand = rig.data.bones["hand." + side].matrix_local.to_3x3()
    # In the rest pose the handle leaves the fist forward (+Y) for both hands; the knuckles run
    # along the arm, outwards.
    at, u = grip.matrix_local.to_translation(), Vector((0, 1, 0))
    v = (hand @ Vector((0, 1, 0))).normalized()
    w = u.cross(v)
    to = lambda a: at + u * a[0] + v * a[1] + w * a[2]
    bm = bmesh.new()
    for part in STANDINS[kind]:
        if part[0] == "rod":
            _, a, b, r0, r1 = part
            pa, pb = to(a), to(b)
            axis = pb - pa
            mat = Matrix.Translation((pa + pb) / 2) @ axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
            bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=r0, radius2=r1, depth=axis.length, matrix=mat)
        elif part[0] == "hoop":
            _, c, r = part
            centre = to(c)
            ring = [centre + (u * math.cos(t) + w * math.sin(t)) * r for t in [i * math.pi / 6 for i in range(12)]]
            for a, b in zip(ring, ring[1:] + ring[:1]):
                axis = b - a
                mat = Matrix.Translation((a + b) / 2) @ axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
                bmesh.ops.create_cone(bm, cap_ends=True, segments=5, radius1=0.01, radius2=0.01, depth=axis.length, matrix=mat)
        elif part[0] == "box":
            _, c, size = part
            mat = Matrix.Translation(to(c)) @ Matrix.Diagonal((size[0], size[1], size[2], 1))
            bmesh.ops.create_cube(bm, size=1.0, matrix=mat)
    mesh = bpy.data.meshes.new("standin_" + kind)
    bm.to_mesh(mesh)
    bm.free()
    material = bpy.data.materials.get("standin_" + kind) or bpy.data.materials.new("standin_" + kind)
    material.diffuse_color = STANDIN_COLOURS[kind]
    material.use_nodes = True
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = STANDIN_COLOURS[kind]
    mesh.materials.append(material)
    obj = bpy.data.objects.new("standin_" + kind, mesh)
    rig.users_collection[0].objects.link(obj)
    obj["rth_preview_tool"] = True
    obj.parent, obj.parent_type, obj.parent_bone = rig, "BONE", "grip." + side
    obj.matrix_parent_inverse = Matrix()
    obj.matrix_basis = (grip.matrix_local @ Matrix.Translation((0, grip.length, 0))).inverted()
    return obj


def build(clip, solver, rig):
    scene = bpy.context.scene
    scene.render.fps = FPS
    fits = clip.fit(solver)
    rig.animation_data_create()
    action = bpy.data.actions.get(clip.name) or bpy.data.actions.new(clip.name)
    action.use_fake_user = True
    for fc in list(getattr(action, "fcurves", [])):
        action.fcurves.remove(fc)
    rig.animation_data.action = action
    frames = int(round(clip.seconds * FPS))
    strain = {}   # per IK hand: the worst twist (degrees, frame); the tilt's lowest and highest, with frames
    track = solver.track_left(clip, frames)
    for f in range(frames + 1):
        if track:
            solver.force_swivel, solver.force_slide = track[f]
        mats = solver.solve(clip.at(f / FPS), clip)
        solver.apply(mats, f)
        for hand, (twist, tilt) in solver.strain().items():
            if clip.hold != "both":
                continue   # the free arm holds nothing
            worst = strain.setdefault(hand, {"twist": (0, 0), "tilt": (90, 0, 90, 0)})
            worst["twist"] = max(worst["twist"], (twist, f))
            lo, lo_f, hi, hi_f = worst["tilt"]
            worst["tilt"] = (min(lo, tilt), lo_f if lo <= tilt else f, max(hi, tilt), hi_f if hi >= tilt else f)
    solver.force_swivel = solver.force_slide = None
    # Keyed every frame, so straight lines between them.
    for fc in action_fcurves(action):
        for k in fc.keyframe_points:
            k.interpolation = "LINEAR"
    scene.frame_start, scene.frame_end = 0, frames
    return action, frames, {"fits": fits, "left": strain}


def action_fcurves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    curves = []   # layered actions (Blender 4.4+)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                curves += list(bag.fcurves)
    return curves


def export(rig, clip):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, clip.name + ".fbx")
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, use_armature_deform_only=False, bake_space_transform=True,
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    )
    return path


def preview_copies(rig, clips, scene_name=None):
    """A copy of the character per clip, side by side along X with its tool(s), all playing
    together: in the Rig's own scene, or in scene_name (made if need be, and shown), so each set of
    clips keeps its own preview."""
    group = scene_name or "skilling"
    for o in list(bpy.data.objects):
        tag = o.get("rth_preview")
        if tag is None and o.get("rth_preview_tool"):
            tag = o.get("rth_group", "skilling")
        if tag is not None and (tag == group or (tag is True or tag == 1) and group == "skilling"):
            bpy.data.objects.remove(o, do_unlink=True)
    if scene_name:
        scene = bpy.data.scenes.get(scene_name) or bpy.data.scenes.new(scene_name)
        scene.render.fps = FPS
        scene.frame_start = 0
        scene.frame_end = max(int(round(c.seconds * FPS)) for c in clips)
        target = scene.collection
        bpy.context.window.scene = scene
    else:
        target = rig.users_collection[0]
    for i, clip in enumerate(clips):
        copy = rig.copy()
        copy.name = "Rig." + clip.name
        copy["rth_preview"] = group
        copy.location = (i * 1.8, 0, 0)
        target.objects.link(copy)
        for child in rig.children:
            if child.get("rth_preview_tool"):
                continue
            part = child.copy()
            part["rth_preview"] = group
            part.parent = copy
            for m in part.modifiers:
                if m.type == "ARMATURE":
                    m.object = copy
            target.objects.link(part)
        copy.animation_data_create()
        copy.animation_data.action = bpy.data.actions[clip.name]
        tools = [hold_tool(copy, clip.tool) if clip.tool not in STANDINS else standin(copy, clip.tool)]
        if clip.hold == "lantern":
            tools.append(standin(copy, "lantern", "L"))
        if clip.hold == "dual":
            tools.append(hold_tool(copy, clip.tool_l or clip.tool, "L"))
        for tool in tools:
            if tool:
                tool["rth_preview"] = group
                if tool.name not in target.objects:
                    for c in list(tool.users_collection):
                        c.objects.unlink(tool)
                    target.objects.link(tool)
    if not scene_name:
        rig.hide_set(True)
        for child in rig.children:
            child.hide_set(True)


def run(makes=None, scene_name=None):
    """Build (and export, and preview) the clips made by `makes` (default: ALL)."""
    makes = makes or ALL
    if os.path.normcase(bpy.data.filepath) != os.path.normcase(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    rig = bpy.data.objects["Rig"]
    rig.hide_set(False)
    for child in rig.children:
        child.hide_set(False)
    solver = Solver(rig)
    report, built = {}, []
    for make in makes:
        if make.__name__ not in CLIPS and not PREVIEW:
            continue
        clip = make()
        action, frames, checks = build(clip, solver, rig)
        built.append(clip)
        report[clip.name] = dict(frames=frames, **checks)
        if EXPORT and make.__name__ in CLIPS:
            report[clip.name]["fbx"] = export(rig, clip)
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix()
    if PREVIEW:
        preview_copies(rig, built, scene_name)
        bpy.context.scene.frame_set(0)
    return report


if globals().get("RUN", True):   # casts.py sets RUN = False to reuse all this for its own clips
    result = run()
