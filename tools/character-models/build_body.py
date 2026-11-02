"""
Builds R'th's low-poly player body from measurements, not from a source mesh.

Every part is lofted from a few rings of points. The numbers are shares of the body's height (sole
0, crown 1), measured off the classic low-poly player: a square-shouldered box of a shirt with short
sleeves, long legs in flat trouser panels with a narrow knee and a calf, small fists, boot blocks, and
a big, flat-shaded, planar head. The shapes are our own; only the proportions and the density (a few
hundred triangles) are the reference's.

The body parts are the bare body in plain underclothes, the base every garment is made on: a bare
torso, bare arms, fists, shorts and bare legs, bare feet. The default outfit (a shirt, trousers and
boots, "wear_" parts) is built over it from its own tables, and the client hides the body parts each
garment covers (OUTFIT, written to Resources/Characters/outfit.json).

Parts meet edge to edge, never buried in each other: an arm grows out of an armhole cut in the
torso's side (the hole's corners are the arm's first ring), the wrist ring is shared by the arm
and the fist, the legs are one mesh whose hip ring splits into the two legs at the crotch, and the
leg's last ring is the foot's first. The shirt's sleeve ends on the bare arm's own ring. Only cloth
overlaps: the shirt hangs over the belt, and the shorts' band over the bare torso.

Run inside Blender (5.x), through the Blender MCP or
    blender --background --python tools/character-models/build_body.py
Set SEX ("male") and PREVIEW (build into the open scene, no export or save) as globals first.

The contract with the client is build_humanoid.py's (see README.md): the same bones, the
"body_<part>" and "hair_<style>" meshes, colour channels as material names, grip.L/R sockets. The
rest pose is a T-pose facing +Y.
"""

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\dev\rth\tools\character-models"
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SEX = globals().get("SEX", "male")
PREVIEW = globals().get("PREVIEW", False)
NAME = {"male": "HumanMale", "female": "HumanFemale"}[SEX]
EXPORT = os.path.join(REPO, "client", "Assets", "Resources", "Characters", NAME + ".fbx")
BUILT_BLEND = os.path.join(HERE, NAME + ".blend")
sys.path.insert(0, os.path.join(REPO, "tools", "art", "blender"))
import rth_art as A  # noqa: E402  (decal)

HEIGHT = 1.846     # sole to crown, in tiles: the player the world is built around (docs/world-scale.md)

# ---------------------------------------------------------------------------------------------
# The shirt: rings of a chamfered box, (height, half width, front, back) from the body's middle.
# The armhole is the side face between the armpit ring and the shoulder ring.
SHIRT = [                        # measured off the classic player's shirt: a block from the belt up
    (0.590, 0.085, 0.066, 0.064),   # hem, hanging over the belt
    (0.640, 0.087, 0.061, 0.059),   # waist
    (0.755, 0.096, 0.062, 0.058),   # armpit
    (0.830, 0.094, 0.056, 0.053),   # shoulders: square, as wide as the chest
    (0.850, 0.034, 0.024, 0.030),   # collar: the neck fills it
]
ARMPIT, SHOULDER_RING = 2, 3
CHAMFER = 0.32

# The sleeve, T-pose, out along x from the shirt's side: box rings like the shirt's, (distance,
# middle height, half height, front, back). Its first ring is the armhole.
ARM = [
    (0.020, 0.797, 0.040, 0.049, 0.049),   # the shoulder: flush with the torso's top, deep front to back
    (0.070, 0.790, 0.037, 0.046, 0.046),   # the sleeve's end, its top still sloping down from the shoulder
]
# The bare arm, from the sleeve's end to the wrist: six-sided rings, flat over the top and
# underneath (the outside and the inside when the arm hangs) with a ridge down the front and the
# back, (distance, middle height, up, down, front, back). Palm down, the biceps is in front and the
# triceps (the bigger) behind; the elbow is narrow with its point behind; the forearm's muscle is
# fullest just past the elbow, on top and in front, and narrows to tendons before the wrist.
LIMB = [
    (0.070, 0.791, 0.024, 0.022, 0.023, 0.025),   # at the sleeve's edge: the deltoid's end, the triceps
    (0.098, 0.792, 0.023, 0.021, 0.026, 0.023),   # biceps
    (0.125, 0.793, 0.019, 0.018, 0.022, 0.024),   # elbow
    (0.145, 0.794, 0.023, 0.020, 0.026, 0.022),   # forearm, at its fullest
    (0.178, 0.793, 0.017, 0.016, 0.021, 0.019),   # narrowing to the wrist
    (0.205, 0.792, 0.013, 0.012, 0.016, 0.015),   # wrist: shared with the hand
]
HEX = 0.5                        # the top and bottom faces' corners, as a share of front and back
ELBOW, WRIST = 0.125, 0.205
# The fist, palm down, the same six sides: the heel of the hand broadening into the back of the
# hand, the knuckles fullest underneath where the fingers curl into the palm, and a flat, bevelled
# end. Bigger than life, so it reads as a fist from the game's camera.
FIST = [
    (0.215, 0.792, 0.017, 0.015, 0.021, 0.018),   # the heel of the hand
    (0.233, 0.792, 0.022, 0.021, 0.028, 0.022),   # the back of the hand
    (0.257, 0.791, 0.022, 0.025, 0.028, 0.021),   # knuckles
    (0.270, 0.789, 0.015, 0.018, 0.022, 0.016),   # the end, bevelled, capped flat
]
# The thumb: a pad on the fist's front, under, from the back of the hand to the knuckles: how far it
# stands out, how far its tip tucks back toward the wrist and down, and how narrow the tip is.
THUMB = (0.005, 0.002, 0.009, 0.6)

# The trousers. Hip rings go all the way round (ten points); below the crotch each leg is a six-
# point ring: (height, half width, front, back) about the leg's middle line, LEG_X out.
TROUSERS = [
    (0.625, 0.075, 0.056, 0.056),   # the belt's top, under the shirt
    (0.595, 0.078, 0.058, 0.060),   # the belt's bottom
    (0.545, 0.084, 0.054, 0.060),   # hips, widest
]
BELT_RINGS = 2                   # the faces between the first rings are the belt (leather)
LEG_X = 0.044
CROTCH = (0.490, 0.050, 0.056)   # height, front, back: the legs part here
LEG = [
    (0.400, 0.035, 0.042, 0.046),   # thigh
    (0.325, 0.027, 0.036, 0.034),   # knee, narrow
    (0.262, 0.035, 0.040, 0.046),   # calf
    (0.185, 0.026, 0.032, 0.032),
    (0.100, 0.021, 0.029, 0.029),   # cuff: shared with the boot
]
KNEE = 0.325

BOOT = [(0.054, 0.022, 0.030, 0.030)]   # the boot's shaft, below the cuff
# The foot, the same six points as a leg ring: (forward, height) for each of inner front, outer
# front, outer side front, outer side back, outer back, inner back; and the half widths.
FOOT_TOP = [(0.095, 0.021), (0.095, 0.021), (0.045, 0.034), (-0.012, 0.046), (-0.036, 0.045), (-0.036, 0.045)]
FOOT_X = (-0.022, 0.018, 0.026, 0.026, 0.016, -0.022)   # out from the leg's line (negative: inwards)

# ---------------------------------------------------------------------------------------------
# The bare body: what every garment is made on. It sits a little inside the shirt, trousers and
# boots everywhere (they are cloth over it), with the same structure, so a garment can follow it
# ring by ring. Plain underclothes are colour blocks in their own channel ("under"): shorts, and
# on her a band over the bust.
#
# The torso is laid out like the head: rows from inside the shorts' band up to the neck's base,
# each seven columns from the front middle round to the back middle (the other side mirrors),
# (across, forward, up) in shares of the height, so a row can slant. Columns: 0 the front middle
# (the breastbone, set back a little between the chest's sides), 1 the chest and the belly, 2 the
# chest's outer edge (the front of the armpit), 3 the side (the armpit, and the top of the shoulder),
# 4 the back's side (the lats, the back of the armpit), 5 the shoulder blade, 6 the spine (set in
# between the back's muscles). Read up the rows: a waist, the ribs widening to the lats, the chest's
# lower edge rising toward the armpit, the armpit dipping between its folds, the collarbones' shelf
# and the top of the shoulder (square and nearly level, as the shirt's: a shoulder that slopes from
# the neck reads as a dome the neck stands on), the trapezius rising steeply into the neck, and the last row tucked up
# inside the neck's base so the neck grows out of it. The shoulder's line keeps falling from the
# neck over the top of the shoulder (column 3) onto the arm's cap (DELTOID), never rising again.
BARE_TORSO_ROWS = [                # proportions measured off the classic player (inside its shirt)
    [(0, .034, .585), (.026, .034, .585), (.050, .026, .585), (.062, .000, .585), (.058, -.024, .585), (.032, -.037, .585), (0, -.038, .585)],   # inside the shorts' band
    [(0, .043, .615), (.033, .044, .615), (.060, .032, .615), (.071, .000, .615), (.068, -.026, .615), (.040, -.043, .615), (0, -.041, .615)],   # the waist, just above the band: straight, as the classic player's
    [(0, .048, .660), (.036, .050, .660), (.071, .036, .660), (.081, .000, .660), (.078, -.028, .660), (.047, -.048, .660), (0, -.045, .660)],   # flaring fast into the block of the ribs
    [(0, .050, .700), (.038, .053, .700), (.074, .038, .700), (.084, .004, .700), (.082, -.029, .700), (.049, -.049, .700), (0, -.046, .700)],   # ribs, the lats
    [(0, .051, .738), (.038, .055, .742), (.076, .039, .748), (.087, .004, .742), (.085, -.030, .740), (.051, -.050, .738), (0, -.047, .738)],   # the chest's lower edge; the shoulder blades' tips
    [(0, .051, .758), (.038, .055, .760), (.079, .036, .764), (.088, .005, .756), (.085, -.030, .764), (.053, -.050, .762), (0, -.047, .758)],   # armpit
    [(0, .044, .818), (.034, .048, .821), (.078, .032, .826), (.088, .000, .830), (.080, -.032, .828), (.051, -.048, .823), (0, -.044, .821)],   # the top of the shoulder: level, square, as wide as the chest
    [(0, .034, .834), (.026, .036, .836), (.050, .026, .837), (.060, .000, .838), (.052, -.026, .838), (.036, -.039, .835), (0, -.037, .834)],   # trapezius: a short step in to the neck
    [(0, .016, .856), (.008, .016, .856), (.016, .015, .854), (.022, .009, .852), (.023, -.016, .850), (.017, -.023, .848), (0, -.023, .850)],   # tucked up inside the neck
]
# The armhole: between the armpit row and the shoulder row, from the front column to the back one
# (columns 2 to 4), six points the arm's first ring joins. It must stay within the shirt's armhole
# (front to back), or the arm's root cuts through the sleeve when the arm comes down.
BARE_PIT, BARE_TOP = 5, 6
BARE_BAND = None                 # (from row, to row): the faces between them are her band top
# The shoulder's cap, between the bare torso's armhole and the bare arm (LIMB, which carries on
# from here): six-sided rings like LIMB's, (distance, middle height, up, down, front, back). It
# fits inside the shirt's hand-tuned sleeve, whose underside rises toward its end.
DELTOID = [
    (0.018, 0.799, 0.028, 0.019, 0.036, 0.036),   # its top near the shoulder's
    (0.045, 0.795, 0.015, 0.022, 0.032, 0.032),   # low on top: lowered, the bend's outside runs past the sleeve's flat side
]
# The shorts and legs: the trousers' structure, tighter. Hip rings (height, half width, front,
# back); the band's top wraps over the torso's bottom ring.
BARE_HIPS = [
    (0.598, 0.070, 0.052, 0.052),   # the shorts' band, where the classic player's belt is
    (0.575, 0.075, 0.054, 0.056),
    (0.545, 0.078, 0.051, 0.056),   # hips
]
BARE_CROTCH = (0.490, 0.046, 0.050)
BARE_LEG = [                     # the first ring is the shorts' hem; the faces above it are the shorts
    (0.440, 0.034, 0.041, 0.042),
    (0.400, 0.032, 0.039, 0.042),   # thigh
    (0.325, 0.024, 0.032, 0.031),   # knee
    (0.262, 0.031, 0.034, 0.042),   # calf
    (0.185, 0.022, 0.027, 0.028),
    (0.100, 0.017, 0.023, 0.023),   # above the ankle, at the trousers' cuff: shared with the foot
]
# The bare foot: from the leg's last ring, an ankle, then a foot shaped like the boot's, smaller.
BARE_ANKLE = [(0.055, 0.017, 0.024, 0.024)]
BARE_FOOT_TOP = [(0.086, 0.015), (0.082, 0.015), (0.040, 0.027), (-0.010, 0.038), (-0.030, 0.038), (-0.030, 0.038)]
BARE_FOOT_X = (-0.019, 0.015, 0.021, 0.021, 0.013, -0.019)

# Which body parts each garment of the default outfit covers (the client hides them under it).
OUTFIT = {"wear_shirt": ["torso"], "wear_trousers": ["legs"], "wear_boots": ["feet"]}
OUTFIT_FILE = os.path.join(REPO, "client", "Assets", "Resources", "Characters", "outfit.json")

# ---------------------------------------------------------------------------------------------
# The head: rows of points from the neck's base to the crown, each seven columns from the front
# midline round to the back midline (the other half mirrors), (across, forward, up) in shares of
# the head's height (chin 0, crown 1), forward from the skull's middle. Columns: 0 front midline,
# 1 the nose's side and the inner eye, 2 the outer eye and the cheek's front, 3 the cheekbone and
# the jaw, 4 the side (by the ear), 5 the back's side, 6 the back midline. Read down the rows: a
# flat-topped skull with straight sides, a brow shelf over sunken eyes, a nose that is a wedge from
# the brow to its tip, a short mouth and a broad chin set back under it, the jaw rising to the ears.
HEAD_HEIGHT = 0.120              # chin to crown
CHIN_Z = 0.872                   # low on the shoulders: a long neck reads as alien
HEAD_Y = 0.032                   # the skull's middle, in front of the body's
# Tuned by hand in Blender by the owner (2026-10-27) and read back from the mesh.
NECK_ROWS = [   # the owner's hand-tuned head, read back with the neck loop they added
    [(0, -.053, -.316), (.102, -.05, -.306), (.224, -.054, -.354), (.323, -.123, -.395), (.339, -.486, -.408), (.257, -.578, -.45), (0, -.569, -.406)],   # the neck's base, a rough square filling the collar
    [(0, -.001, -.127), (.09, -.014, -.083), (.179, -.063, -.049), (.303, -.119, -.089), (.309, -.323, -.085), (.274, -.512, -.169), (0, -.566, -.188)],   # the neck's middle (the owner's loop)
    [(0, .044, .04), (.125, .018, .046), (.237, -.071, .13), (.285, -.117, .18), (.287, -.163, .197), (.29, -.453, .078), (0, -.564, .004)],   # its top, well inside the jaw so the jaw stands out over it
]
HEAD_ROWS = [
    [(0, .268, -.049), (.115, .252, -.031), (.233, .149, .013), (.362, -.063, .182), (.339, -.114, .235), (.287, -.431, .187), (0, -.556, .104)],   # the jaw's edge
    [(0, .266, .076), (.11, .246, .076), (.249, .156, .084), (.39, -.032, .242), (.374, -.072, .282), (.318, -.418, .258), (0, -.547, .194)],   # the chin's front
    [(0, .302, .189), (.114, .262, .181), (.272, .173, .19), (.411, -.014, .301), (.395, -.065, .324), (.34, -.41, .321), (0, -.554, .291)],   # mouth, behind the chin
    [(0, .446, .234), (.079, .346, .234), (.285, .183, .305), (.427, -.002, .39), (.418, -.087, .389), (.369, -.444, .408), (0, -.574, .427)],   # the nose's tip
    [(0, .376, .464), (.089, .286, .464), (.279, .246, .464), (.388, .075, .492), (.44, -.12, .52), (.391, -.464, .554), (0, -.578, .584)],   # eyes, sunken
    [(0, .37, .588), (.141, .387, .587), (.301, .316, .591), (.433, .166, .615), (.429, -.114, .624), (.385, -.464, .664), (0, -.569, .674)],   # brow
    [(0, .326, .784), (.124, .315, .784), (.289, .276, .794), (.381, .096, .786), (.389, -.114, .804), (.353, -.434, .8), (0, -.524, .804)],   # forehead's top
    [(0, .146, .946), (.119, .123, .946), (.198, .066, .947), (.256, -.054, .961), (.283, -.174, .97), (.217, -.334, .959), (0, -.39, .951)],   # the crown's edge
]
CROWN = (0, -.163, 1.004)
EYE = [(.086, .464), (.177, .504), (.278, .465), (.179, .435)]   # a dark diamond: inner, top, outer, bottom
HAIR_FROM = [6, 6, 6, 5, 4, 2, 2]   # the row each column's hair starts at (front high, back low)
HAIR_THICK = 0.06                # in head heights; half that at the edge

MOUTH = (0.130, 0.075, 0.012)    # a small mouth mark on the lip: (height, half width, half height), or none
NECK_GIRTH = 0.80               # the neck rows' width and depth, scaled about each row's middle
HIP_Z, SPINE_Z, CHEST_Z = 0.530, 0.620, 0.700   # where the hips, spine and chest bones start



# Her head is her own, on his grid (the same rows and columns, so tuning reads back the same
# way): an egg, widest at the cheekbones and tapering to a small pointed chin, with no jaw corner;
# the jaw runs short and steep from the chin up to under the ear. The face is flatter: a small
# nose with only a slight step under it, and a smooth brow. Her neck is slim and round at the top,
# and still fills her collar at the base.
FEMALE_NECK_ROWS = [   # the owner's hand-tuned head, read back with the neck loop they added
    [(0, -.031, -.317), (.086, -.033, -.317), (.188, -.048, -.354), (.271, -.108, -.395), (.285, -.428, -.408), (.216, -.509, -.45), (0, -.501, -.406)],
    [(0, -.005, -.149), (.075, -.003, -.137), (.184, -.054, -.079), (.214, -.119, -.102), (.236, -.293, -.105), (.193, -.441, -.188), (0, -.475, -.201)],
    [(0, .017, -.006), (.083, .016, -.002), (.158, -.035, .061), (.218, -.104, .147), (.195, -.179, .152), (.173, -.383, .034), (0, -.453, -.026)],
]
FEMALE_HEAD_ROWS = [
    [(0, .202, -.066), (.073, .187, -.058), (.168, .097, .004), (.28, -.063, .166), (.25, -.113, .209), (.196, -.344, .207), (0, -.425, .104)],
    [(0, .221, .019), (.083, .207, .022), (.196, .129, .082), (.314, -.044, .208), (.292, -.083, .247), (.231, -.36, .27), (0, -.443, .2)],
    [(0, .245, .114), (.1, .237, .133), (.229, .168, .169), (.338, -.023, .25), (.327, -.074, .292), (.28, -.385, .319), (0, -.477, .27)],
    [(0, .352, .189), (.053, .277, .172), (.25, .201, .231), (.361, -.003, .324), (.356, -.086, .354), (.315, -.409, .386), (0, -.52, .372)],
    [(0, .342, .394), (.068, .277, .394), (.251, .237, .394), (.355, .072, .424), (.377, -.125, .478), (.337, -.438, .493), (0, -.553, .514)],
    [(0, .349, .524), (.118, .345, .524), (.264, .28, .547), (.347, .122, .564), (.381, -.113, .588), (.335, -.451, .606), (0, -.553, .599)],
    [(0, .315, .714), (.103, .305, .714), (.246, .224, .719), (.329, .086, .733), (.368, -.113, .739), (.323, -.423, .739), (0, -.516, .734)],
    [(0, .15, .902), (.103, .127, .904), (.188, .077, .901), (.233, -.053, .911), (.243, -.166, .908), (.215, -.334, .871), (0, -.373, .874)],
]

FEMALE = dict(
    HEIGHT=1.818,
    SHIRT=[
        (0.575, 0.075, 0.056, 0.057),   # hem (measured off the classic player's: widening up to the shoulders)
        (0.625, 0.074, 0.054, 0.046),   # waist
        (0.695, 0.076, 0.074, 0.046),   # under the bust
        (0.728, 0.078, 0.090, 0.047),   # bust
        (0.755, 0.080, 0.078, 0.047),   # armpit
        (0.808, 0.085, 0.051, 0.047),   # shoulders
        (0.842, 0.030, 0.022, 0.027),   # collar
    ],
    ARMPIT=4, SHOULDER_RING=5,
    ARM=[
        (0.016, 0.784, 0.033, 0.041, 0.041),
        (0.060, 0.778, 0.030, 0.038, 0.038),
    ],
    LIMB=[                              # slimmer, with less biceps
        (0.060, 0.779, 0.022, 0.020, 0.021, 0.022),
        (0.084, 0.780, 0.022, 0.020, 0.023, 0.021),
        (0.108, 0.780, 0.018, 0.016, 0.020, 0.021),
        (0.125, 0.781, 0.021, 0.018, 0.023, 0.020),
        (0.158, 0.780, 0.016, 0.014, 0.018, 0.016),
        (0.184, 0.780, 0.011, 0.010, 0.013, 0.012),
    ],
    ELBOW=0.108, WRIST=0.184,
    FIST=[
        (0.193, 0.780, 0.014, 0.012, 0.017, 0.015),
        (0.209, 0.780, 0.018, 0.017, 0.023, 0.018),
        (0.230, 0.779, 0.018, 0.021, 0.023, 0.017),
        (0.241, 0.777, 0.012, 0.015, 0.018, 0.013),
    ],
    THUMB=(0.004, 0.002, 0.008, 0.6),
    BARE_TORSO_ROWS=[                   # measured off the classic player: a waist, a bust, broad shoulders
        [(0, .033, .570), (.024, .033, .570), (.044, .025, .570), (.053, .000, .570), (.050, -.023, .570), (.029, -.035, .570), (0, -.035, .570)],   # inside the shorts' band
        [(0, .045, .600), (.032, .046, .600), (.056, .033, .600), (.065, .000, .600), (.062, -.023, .600), (.036, -.037, .600), (0, -.035, .600)],   # waist
        [(0, .052, .668), (.034, .056, .670), (.060, .040, .672), (.069, .002, .674), (.066, -.025, .674), (.040, -.040, .672), (0, -.037, .672)],   # under the bust
        [(0, .068, .722), (.032, .084, .726), (.062, .062, .728), (.073, .004, .730), (.071, -.028, .728), (.044, -.043, .724), (0, -.040, .722)],   # bust
        [(0, .062, .750), (.032, .070, .752), (.066, .042, .756), (.075, .004, .750), (.072, -.026, .756), (.046, -.044, .754), (0, -.041, .752)],   # armpit; the bust's top
        [(0, .044, .797), (.030, .047, .799), (.071, .030, .802), (.081, .000, .806), (.072, -.026, .801), (.044, -.043, .801), (0, -.039, .800)],   # the top of the shoulder: square
        [(0, .030, .822), (.020, .031, .824), (.040, .022, .826), (.050, .000, .826), (.042, -.022, .826), (.028, -.033, .822), (0, -.031, .822)],   # trapezius
        [(0, .016, .842), (.008, .016, .842), (.015, .015, .840), (.020, .009, .838), (.021, -.016, .836), (.015, -.022, .834), (0, -.022, .836)],   # tucked up inside the neck
    ],
    BARE_BAND=(2, 4),
    BARE_PIT=4, BARE_TOP=5,
    DELTOID=[
        (0.016, 0.779, 0.022, 0.021, 0.030, 0.026),
        (0.038, 0.779, 0.016, 0.020, 0.025, 0.025),
    ],
    BARE_HIPS=[
        (0.580, 0.061, 0.045, 0.045),
        (0.557, 0.066, 0.046, 0.048),
        (0.505, 0.088, 0.049, 0.060),
    ],
    BARE_CROTCH=(0.455, 0.047, 0.055),
    BARE_LEG=[
        (0.425, 0.039, 0.042, 0.047, 0.047),
        (0.380, 0.036, 0.038, 0.044, 0.046),
        (0.318, 0.025, 0.030, 0.029, 0.042),
        (0.255, 0.031, 0.032, 0.040, 0.044),
        (0.180, 0.021, 0.026, 0.026, 0.045),
        (0.100, 0.016, 0.022, 0.022, 0.046),
    ],
    BARE_ANKLE=[(0.055, 0.016, 0.022, 0.022)],
    BARE_FOOT_TOP=[(0.080, 0.014), (0.076, 0.014), (0.037, 0.025), (-0.009, 0.035), (-0.028, 0.035), (-0.028, 0.035)],
    BARE_FOOT_X=(-0.017, 0.013, 0.019, 0.019, 0.012, -0.017),
    TROUSERS=[
        (0.600, 0.066, 0.049, 0.049),
        (0.575, 0.070, 0.051, 0.053),
        (0.505, 0.094, 0.052, 0.064),   # hips, wide and low
    ],
    LEG_X=0.050,
    CROTCH=(0.455, 0.050, 0.058),
    LEG=[                               # the fifth number: the leg's line, knees a little in
        (0.380, 0.040, 0.042, 0.048, 0.046),
        (0.318, 0.029, 0.034, 0.032, 0.042),
        (0.255, 0.035, 0.036, 0.044, 0.044),
        (0.180, 0.025, 0.030, 0.030, 0.045),
        (0.100, 0.019, 0.027, 0.027, 0.046),   # cuff: at his height, so her boots are his
    ],
    KNEE=0.318,
    HEAD_HEIGHT=0.128, CHIN_Z=0.862, HEAD_Y=0.030,
    NECK_ROWS=FEMALE_NECK_ROWS,
    NECK_GIRTH=0.84,
    HEAD_ROWS=FEMALE_HEAD_ROWS,
    EYE=[(.087, .392), (.182, .442), (.281, .400), (.184, .360)],
    CROWN=(0, -.100, .940),
    MOUTH=(0.067, 0.065, 0.011),     # on the lip between the chin and the nose, not under the nose
    HIP_Z=0.500, SPINE_Z=0.600, CHEST_Z=0.690,
)
if SEX == "female":
    globals().update(FEMALE)


def h(x):
    return x * HEIGHT


# ---------------------------------------------------------------------------------------------
# Building blocks.

def box_section(a, f, b, c=CHAMFER):
    """A chamfered box's eight corners, (across, forward): 0 front right, 1 front left, 2 left
    front, 3 left back, 4 back left, 5 back right, 6 right back, 7 right front (right is +x)."""
    return [(a * (1 - c), f), (-a * (1 - c), f), (-a, f * (1 - c)), (-a, -b * (1 - c)),
            (-a * (1 - c), -b), (a * (1 - c), -b), (a, -b * (1 - c)), (a, f * (1 - c))]


def quads(bm, r0, r1, closed=True, mat=0):
    """Faces between two rings of BMVerts with the same count."""
    n = len(r0)
    out = []
    for k in range(n if closed else n - 1):
        out.append(bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k])))
    for f in out:
        f.material_index = mat
    return out


def decal_island(face, most=8):
    """Whether a face belongs to a decal: a loose, nearly flat sheet of at most `most` faces
    (a thumb is loose and small too, but bent)."""
    seen, todo = {face}, [face]
    while todo:
        for e in todo.pop().edges:
            for g in e.link_faces:
                if g not in seen:
                    seen.add(g)
                    todo.append(g)
                    if len(seen) > most:
                        return False
    face.normal_update()
    for g in seen:
        g.normal_update()
        if g.normal.dot(face.normal) < 0.7:
            return False
    return True


def cap(bm, ring, mat=0):
    f = bm.faces.new(ring)
    f.material_index = mat
    return f


def vert(bm, x, y, z):
    return bm.verts.new((h(x), h(y), h(z)))


# Hand tuning: per-vertex offsets on top of what the tables build, per sex and part, in shares of
# HEIGHT, keyed by the vertex's index in the built part. Written by `capture` from a scene where
# the parts were moved by hand (X-mirror editing in Blender); a rebuild applies them. Changing a
# part's ring counts changes its indices, so recapture after such a change.
TUNING_FILE = os.path.join(HERE, "body_tuning.json")


def load_tuning():
    if not os.path.exists(TUNING_FILE):
        return {}
    with open(TUNING_FILE) as f:
        return json.load(f).get(SEX, {})


class Part:
    """One kit part: a bmesh plus each vertex's weights by bone name."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.weights = {}

    def weigh(self, verts, **bones):
        for v in verts:
            self.weights[v] = dict(bones)

    def tune(self, tuning):
        """Apply the hand-tuned offsets for this part."""
        offsets = tuning.get(self.name, {})
        if offsets:
            self.bm.verts.ensure_lookup_table()
            for i, d in offsets.items():
                self.bm.verts[int(i)].co += Vector(d) * HEIGHT

    def finish(self, materials, smooth=False):
        bm = self.bm
        # Recalculating a loose decal (an eye, the mouth) can turn it inside out, so only the
        # shells are recalculated; a decal keeps the facing A.decal gave it.
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if not decal_island(f)])
        for f in bm.faces:
            f.smooth = smooth
        index = {v: i for i, v in enumerate(bm.verts)}
        name = self.name if self.name.startswith(("hair", "wear")) else "body_" + self.name
        me = bpy.data.meshes.new(name)
        weights = [(index[v], w) for v, w in self.weights.items() if v.is_valid]
        bm.to_mesh(me)
        obj = bpy.data.objects.new(name, me)
        for m in materials:
            me.materials.append(material(m))
        for i, w in weights:
            for bone, x in w.items():
                if x <= 0:
                    continue
                g = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
                g.add([i], x, "REPLACE")
        bm.free()
        return obj


def lr(side):
    return "L" if side < 0 else "R"


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------------------------------------
# The torso and arms: the bare body's, and the shirt's.

def box_rings(bm, table):
    return [[vert(bm, x, y, z) for x, y in box_section(a, f, b)] for z, a, f, b in table]


def shirt_rings(bm):
    return box_rings(bm, SHIRT)


def armhole(rings, side, lo=None, hi=None):
    """The armhole's corners, in the order an arm ring goes: bottom front, bottom back, top back,
    top front. lo and hi are the armpit and shoulder rings (the shirt's unless given)."""
    front, back = (2, 3) if side < 0 else (7, 6)
    lo, hi = rings[ARMPIT if lo is None else lo], rings[SHOULDER_RING if hi is None else hi]
    return [lo[front], lo[back], hi[back], hi[front]]


def torso_shell(p, table, lo, hi, band=None):
    """A torso from a table of box rings, open at the bottom and the neck, the armholes cut (the
    arms bring their own faces). The faces between band's rings get material 1."""
    bm = p.bm
    rings = box_rings(bm, table)
    for i, (r0, r1) in enumerate(zip(rings, rings[1:])):
        quads(bm, r0, r1, mat=1 if band and band[0] <= i < band[1] else 0)
    for side in (-1, 1):
        hole = set(armhole(rings, side, lo, hi))
        for f in list(bm.faces):
            if set(f.verts) == hole:
                bm.faces.remove(f)
    for (z, *_), ring in zip(table, rings):
        t = smoothstep(0.66, 0.74, z)
        p.weigh(ring, spine=1 - t, chest=t)
    return rings


def arm_ring(bm, side, s, zc, hh, f, b, x_side):
    x = side * (x_side + s)
    return [vert(bm, x, f, zc - hh), vert(bm, x, -b, zc - hh), vert(bm, x, -b, zc + hh), vert(bm, x, f, zc + hh)]


def limb_ring(bm, side, s, zc, u, d, f, b, x_side):
    """A six-sided ring, the same way round as arm_ring's four: front under, back under, back,
    back over, front over, front."""
    x, k = side * (x_side + s), HEX
    return [vert(bm, x, f * k, zc - d), vert(bm, x, -b * k, zc - d), vert(bm, x, -b, zc),
            vert(bm, x, -b * k, zc + u), vert(bm, x, f * k, zc + u), vert(bm, x, f, zc)]


BOX_TO_LIMB = (0, 1, 3, 4)       # where arm_ring's four corners fall among limb_ring's six


def bridge(bm, box, ring, mat=0):
    """Faces from a four-point arm ring to a six-point one: a quad on each side, and a quad and a
    triangle where the six-point ring has a ridge (front and back)."""
    out = []
    for i in range(4):
        a, b = BOX_TO_LIMB[i], BOX_TO_LIMB[(i + 1) % 4]
        chain = [ring[(a + k) % 6] for k in range((b - a) % 6 + 1)]
        out.append(bm.faces.new((box[i], box[(i + 1) % 4], chain[-1], chain[-2])))
        if len(chain) == 3:
            out.append(bm.faces.new((box[i], chain[1], chain[0])))
    for f in out:
        f.material_index = mat
    return out


def x_side():
    return SHIRT[ARMPIT][1]


def weigh_arm_ring(p, ring, s, side):
    """An arm ring's weights by its distance out: half on the chest at the sleeve's first ring,
    blending to the upper arm alone at the sleeve's end (so the shoulder's cap under the sleeve
    swings with it), then the elbow and the wrist."""
    s_ = lr(side)
    if s < ARM[-1][0]:
        t = 0.5 + 0.5 * smoothstep(ARM[0][0], ARM[-1][0], s)
        p.weigh(ring, chest=1 - t, **{f"upper_arm.{s_}": t})
    elif s >= WRIST:
        p.weigh(ring, **{f"forearm.{s_}": 0.5, f"hand.{s_}": 0.5})
    else:
        t = smoothstep(ELBOW - 0.02, ELBOW + 0.02, s)
        p.weigh(ring, **{f"upper_arm.{s_}": 1 - t, f"forearm.{s_}": t})


def torso_row(row):
    """Seven columns, front to back on the right, then the left back to the front: twelve points."""
    return row + [(-x, y, z) for x, y, z in reversed(row[1:-1])]


def bare_hole(rings, side):
    """The bare torso's armhole: the armpit row's front, middle and back columns, then the
    shoulder row's back, top and front, the way round a limb ring goes (each of the arm's first
    ring's points falls between two of these)."""
    cols = (2, 3, 4) if side > 0 else (10, 9, 8)
    lo, hi = rings[BARE_PIT], rings[BARE_TOP]
    return [lo[cols[0]], lo[cols[1]], lo[cols[2]], hi[cols[2]], hi[cols[1]], hi[cols[0]]]


def bare_torso_rings(bm):
    return [[bm.verts.new(Vector((h(x), h(y), h(z)))) for x, y, z in torso_row(r)] for r in BARE_TORSO_ROWS]


def bare_torso(p):
    """The bare torso: the rows of BARE_TORSO_ROWS, open at the bottom and the neck, the armholes
    cut; the faces between BARE_BAND's rows get material 1 (her band top)."""
    bm = p.bm
    rings = bare_torso_rings(bm)
    for i, (r0, r1) in enumerate(zip(rings, rings[1:])):
        quads(bm, r0, r1, mat=1 if BARE_BAND and BARE_BAND[0] <= i < BARE_BAND[1] else 0)
    for side in (-1, 1):
        hole = set(bare_hole(rings, side))
        for f in list(bm.faces):
            if set(f.verts) <= hole:
                bm.faces.remove(f)
    for v in bm.verts:
        t = smoothstep(0.66, 0.74, v.co.z / HEIGHT)
        p.weigh([v], spine=1 - t, chest=t)


def antiprism(bm, a, b, mat=0):
    """A band of triangles between two six-point rings whose points alternate round the arm
    (b's k-th point lies between a's k-th and the next)."""
    n = len(a)
    for k in range(n):
        for vs in ((a[k], a[(k + 1) % n], b[k]), (b[k], a[(k + 1) % n], b[(k + 1) % n])):
            bm.faces.new(vs).material_index = mat


def bare_arms(p):
    """Both bare arms: from the bare torso's armhole (its points, copied) over the shoulder's cap
    and on down the arm to the wrist, open there."""
    bm = p.bm
    tmp = bmesh.new()
    torso = bare_torso_rings(tmp)
    for side in (-1, 1):
        hole = [bm.verts.new(v.co) for v in bare_hole(torso, side)]
        rings = [limb_ring(bm, side, *r, x_side()) for r in DELTOID + LIMB]
        antiprism(bm, hole, rings[0])
        for r0, r1 in zip(rings, rings[1:]):
            quads(bm, r0, r1)
        p.weigh(hole, chest=1.0)
        for (s, *_), ring in zip(DELTOID + LIMB, rings):
            weigh_arm_ring(p, ring, s, side)
    tmp.free()


def arm_layout(verts, side):
    """A built bare arms part's rings on one side: the armhole, the shoulder's cap rings (DELTOID's)
    and the arm's (LIMB's)."""
    per = 6 + 6 * (len(DELTOID) + len(LIMB))
    v = verts[(0 if side < 0 else per):][:per]
    six = [v[6 + 6 * i:12 + 6 * i] for i in range(len(DELTOID) + len(LIMB))]
    return v[:6], six[:len(DELTOID)], six[len(DELTOID):]


def shirt(p):
    """The shirt: a square-shouldered box with short boxed sleeves, the sleeve's end stepping in
    to the bare arm's own ring there (LIMB's first), so it meets the arm with no gap."""
    bm = p.bm
    rings = torso_shell(p, SHIRT, ARMPIT, SHOULDER_RING)
    for side in (-1, 1):
        box = [armhole(rings, side)] + [arm_ring(bm, side, *r, x_side()) for r in ARM]
        end = limb_ring(bm, side, *LIMB[0], x_side())
        for r0, r1 in zip(box, box[1:]):
            quads(bm, r0, r1)
        bridge(bm, box[-1], end)                     # the sleeve's underside
        for (s, *_), ring in zip(ARM + LIMB[:1], box[1:] + [end]):
            weigh_arm_ring(p, ring, s, side)


def shirt_sleeve(verts, side):
    """A built shirt's sleeve rings on one side (ARM's), after the torso's."""
    per = 4 * len(ARM) + 6
    v = verts[8 * len(SHIRT) + (0 if side < 0 else per):][:per]
    return [v[4 * i:4 * i + 4] for i in range(len(ARM))]


def wrist_ring(hand_verts, side):
    """A built hands part's wrist ring on one side (the same points as the arm's last ring)."""
    return hand_verts[(0 if side < 0 else len(hand_verts) // 2):][:6]


def thumb(bm, rings, side):
    """The thumb: the fist's front-under face between the back of the hand and the knuckles,
    pushed out into a pad, its tip tucked back and down and narrowed."""
    a, b = rings[2], rings[3]
    root = [a[5], a[0], b[0], b[5]]
    for f in list(bm.faces):
        if set(f.verts) == set(root):
            bm.faces.remove(f)
    out, tuck, down, narrow = THUMB
    mid = sum((v.co for v in root), Vector()) / 4
    axis = Vector((mid.x, 0, h(FIST[1][1])))
    n = (mid - axis)
    n.x = 0
    n.normalize()
    tip_mid = (b[0].co + b[5].co) / 2
    new = []
    for i, v in enumerate(root):
        co = v.co + n * h(out)
        if i >= 2:                                   # the tip
            co = tip_mid + (v.co - tip_mid) * narrow + n * h(out * 0.8)
            co += Vector((-side * h(tuck), 0, -h(down)))
        new.append(bm.verts.new(co))
    for i in range(4):
        bm.faces.new((root[i], root[(i + 1) % 4], new[(i + 1) % 4], new[i]))
    bm.faces.new(new)
    return new


def fists(p):
    bm = p.bm
    for side in (-1, 1):
        s_ = lr(side)
        rings = [limb_ring(bm, side, *r, x_side()) for r in [LIMB[-1]] + FIST]
        for r0, r1 in zip(rings, rings[1:]):
            quads(bm, r0, r1)
        cap(bm, rings[-1])
        p.weigh(rings[0], **{f"forearm.{s_}": 0.5, f"hand.{s_}": 0.5})
        for ring in rings[1:]:
            p.weigh(ring, **{f"hand.{s_}": 1.0})
        p.weigh(thumb(bm, rings, side), **{f"hand.{s_}": 1.0})


# ---------------------------------------------------------------------------------------------
# The legs and feet: the bare body's (shorts, bare legs and feet) and the trousers and boots.

def hip_section(a, f, b):
    """Ten points round the hips, in the order the two legs' top rings make: front middle, left
    front, left side front, left side back, left back, back middle, then the right side back to
    the front."""
    return [(0, f), (-a * 0.5, f), (-a, f * 0.4), (-a, -b * 0.4), (-a * 0.5, -b),
            (0, -b), (a * 0.5, -b), (a, -b * 0.4), (a, f * 0.4), (a * 0.5, f)]


def leg_section(a, f, b):
    """Six points about a leg's line, (outward, forward): inner front, outer front, outer side
    front, outer side back, outer back, inner back."""
    return [(-a, f), (a * 0.45, f), (a, f * 0.3), (a, -b * 0.3), (a * 0.45, -b), (-a, -b)]


def leg_ring(bm, side, z, a, f, b, x=None):
    """A leg ring about the leg's line: LEG_X out, or x where the leg leans in (her knees)."""
    cx = LEG_X if x is None else x
    return [vert(bm, side * (cx + u), y, z) for u, y in leg_section(a, f, b)]


def leg_line(z):
    """How far out the leg's line is at a height, from the LEG rings that give one."""
    pts = [(r[0], r[4] if len(r) > 4 else LEG_X) for r in LEG]
    pts = [(CROTCH[0], LEG_X)] + pts
    for (z0, x0), (z1, x1) in zip(pts, pts[1:]):
        if z1 <= z <= z0:
            return x0 + (x1 - x0) * (z0 - z) / (z0 - z1)
    return pts[-1][1] if z < pts[-1][0] else LEG_X


def hips_and_legs(p, hips_table, crotch_def, leg, mat_of):
    """Hips (ten-point rings, capped at the top) parting at the crotch into two legs of six-point
    rings, open at the last. mat_of(n) is the material of the n-th band of faces from the top,
    counting the hips', the crotch's and then each leg's."""
    bm = p.bm
    hips = [[vert(bm, x, y, z) for x, y in hip_section(a, f, b)] for z, a, f, b in hips_table]
    for i, (r0, r1) in enumerate(zip(hips, hips[1:])):
        quads(bm, r0, r1, mat=mat_of(i))
    cap(bm, hips[0], mat=mat_of(0))
    # The crotch: one ring round both legs' tops, sharing the front and back middle points.
    z, f, b = crotch_def
    fc, bc = vert(bm, 0, f, z), vert(bm, 0, -b, z)
    tops = {}
    for side in (-1, 1):
        pts = leg_section(LEG_X, f, b)[1:5]
        tops[side] = [fc] + [vert(bm, side * (LEG_X + u), y, z) for u, y in pts] + [bc]
    crotch = tops[-1] + list(reversed(tops[1][1:5]))     # round the hips' ring order
    n = len(hips) - 1
    quads(bm, hips[-1], crotch, mat=mat_of(n))
    for ring in hips:
        p.weigh(ring, hips=1.0)
    p.weigh([fc, bc], hips=1.0)
    for side in (-1, 1):
        s_ = lr(side)
        rings = [tops[side]] + [leg_ring(bm, side, *r) for r in leg]
        for i, (r0, r1) in enumerate(zip(rings, rings[1:])):
            quads(bm, r0, r1, mat=mat_of(n + 1 + i))
        p.weigh(tops[side][1:5], hips=0.5, **{f"thigh.{s_}": 0.5})
        for (z, *_r), ring in zip(leg, rings[1:]):
            t = smoothstep(KNEE - 0.02, KNEE + 0.02, z)
            w = {f"thigh.{s_}": t, f"shin.{s_}": 1 - t}
            if z <= leg[-1][0]:
                w = {f"shin.{s_}": 0.6, f"foot.{s_}": 0.4}
            p.weigh(ring, **w)


def trousers(p):
    hips_and_legs(p, TROUSERS, CROTCH, LEG, lambda n: 1 if n < BELT_RINGS - 1 else 0)


def bare_legs(p):
    """The shorts (material 1) from the band down to the first leg ring, then bare legs."""
    shorts = len(BARE_HIPS) + 1                   # the hips' bands, the crotch's and the thighs' first
    hips_and_legs(p, BARE_HIPS, BARE_CROTCH, BARE_LEG, lambda n: 1 if n < shorts else 0)


def feet_from(p, leg_end, shaft_table, foot_top, foot_x):
    """Both feet from a leg's last ring: a shaft (the boot's, or the ankle), then the foot's top and
    its sole, capped."""
    bm = p.bm
    for side in (-1, 1):
        s_ = lr(side)
        fx = leg_line(0.0)
        cuff = leg_ring(bm, side, *leg_end)
        shaft = [leg_ring(bm, side, *r[:4], x=fx) for r in shaft_table]
        top = [vert(bm, side * (fx + u), y, z) for u, (y, z) in zip(foot_x, foot_top)]
        sole = [vert(bm, side * (fx + u), y, 0.0) for u, (y, z) in zip(foot_x, foot_top)]
        rings = [cuff] + shaft + [top, sole]
        for r0, r1 in zip(rings, rings[1:]):
            quads(bm, r0, r1)
        cap(bm, sole)
        p.weigh(cuff, **{f"shin.{s_}": 0.6, f"foot.{s_}": 0.4})
        for ring in shaft:
            p.weigh(ring, **{f"foot.{s_}": 1.0})
        for ring in (top, sole):
            p.weigh(ring[:2], **{f"toe.{s_}": 0.6, f"foot.{s_}": 0.4})
            p.weigh(ring[2:], **{f"foot.{s_}": 1.0})


def boots(p):
    feet_from(p, LEG[-1], BOOT, FOOT_TOP, FOOT_X)


def bare_feet(p):
    feet_from(p, BARE_LEG[-1], BARE_ANKLE, BARE_FOOT_TOP, BARE_FOOT_X)


# ---------------------------------------------------------------------------------------------
# The head.

def head_point(x, y, z):
    return Vector((h(x * HEAD_HEIGHT), h(HEAD_Y + y * HEAD_HEIGHT), h(CHIN_Z + z * HEAD_HEIGHT)))


def full_row(row):
    """Seven columns, front to back on the right, then the left back to the front: twelve points."""
    return row + [(-x, y, z) for x, y, z in reversed(row[1:-1])]


def slim_neck(row):
    """A neck row scaled by NECK_GIRTH across and front to back, about its own middle."""
    mid = (row[0][1] + row[-1][1]) / 2
    return [(x * NECK_GIRTH, mid + (y - mid) * NECK_GIRTH, z) for x, y, z in row]


def head(p):
    bm = p.bm
    rows = [slim_neck(r) for r in NECK_ROWS] + HEAD_ROWS
    rings = [[bm.verts.new(head_point(*q)) for q in full_row(r)] for r in rows]
    for r0, r1 in zip(rings, rings[1:]):
        quads(bm, r0, r1)
    top = bm.verts.new(head_point(*CROWN))
    n = len(rings[-1])
    for k in range(n):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % n], top))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    n = len(NECK_ROWS)
    for i, ring in enumerate(rings[:n]):      # the neck: from its base on the chest to the head
        t = i / (n - 1)
        p.weigh(ring, chest=0.4 * (1 - t), neck=0.6 - 0.1 * t, head=0.5 * t)
    for ring in rings[n:]:
        p.weigh(ring, head=1.0)
    p.weigh([top], head=1.0)
    # The eyes: dark diamonds cast onto the sockets from the front.
    for s in (-1, 1):
        outline = [head_point(s * x, 1.0, z) for x, z in EYE]
        eye = A.decal(bm, outline, (0, 1, 0), mat=1, facing=0.2)
        if eye:
            p.weigh(list(eye.verts), head=1.0)
            bmesh.ops.triangulate(bm, faces=[eye])
    if MOUTH:
        mz, hw, hh = MOUTH
        outline = [head_point(hw * math.cos(a), 1.0, mz + hh * math.sin(a))
                   for a in [2 * math.pi * i / 6 for i in range(6)]]
        mouth = A.decal(bm, outline, (0, 1, 0), mat=2, facing=0.2)
        if mouth:
            p.weigh(list(mouth.verts), head=1.0)
            bmesh.ops.triangulate(bm, faces=[mouth])
    for v in bm.verts:
        p.weights.setdefault(v, {"head": 1.0})


def hair_short(p):
    """Short hair: the skull's faces from each column's HAIR_FROM row up, stood off the head."""
    bm = p.bm
    rows = [full_row(r) for r in HEAD_ROWS]
    n = len(rows[0])
    start = HAIR_FROM + list(reversed(HAIR_FROM[1:-1]))
    verts = {}

    def v(r, k):
        key = (r, k % n)
        if key not in verts:
            verts[key] = bm.verts.new(head_point(*rows[r][k % n]))
        return verts[key]
    for r in range(len(rows) - 1):
        for k in range(n):
            if r >= max(start[k], start[(k + 1) % n]):
                bm.faces.new((v(r, k), v(r, k + 1), v(r + 1, k + 1), v(r + 1, k)))
    top = bm.verts.new(head_point(*CROWN))
    for k in range(n):
        bm.faces.new((v(len(rows) - 1, k), v(len(rows) - 1, k + 1), top))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    edge = {x for x in bm.verts if x.is_boundary}
    centre = head_point(0, -0.05, 0.55)
    for x in bm.verts:
        out = (x.co - centre).normalized()
        x.co += out * h(HAIR_THICK * HEAD_HEIGHT * (0.5 if x in edge else 1.0))
    p.weigh(list(bm.verts), head=1.0)


# ---------------------------------------------------------------------------------------------
# The skeleton: the build_humanoid.py names, placed on these proportions.

def skeleton():
    xs = x_side()
    zc = LIMB[-1][1]
    shoulder = xs + 0.010
    neck_z = SHIRT[-1][0] - 0.014
    bones = {
        "hips": ((0, 0, HIP_Z), (0, 0, SPINE_Z), None),
        "spine": ((0, 0, SPINE_Z), (0, 0, CHEST_Z), "hips"),
        "chest": ((0, 0, CHEST_Z), (0, 0, neck_z), "spine"),
        "neck": ((0, 0, neck_z), (0, 0.006, CHIN_Z), "chest"),
        "head": ((0, 0.006, CHIN_Z), (0, 0.006, 1.0), "neck"),
    }
    for side in (-1, 1):
        s = lr(side)
        bones[f"upper_arm.{s}"] = ((side * shoulder, 0, zc), (side * (xs + ELBOW), 0, zc), "chest")
        bones[f"forearm.{s}"] = ((side * (xs + ELBOW), 0, zc), (side * (xs + WRIST), 0, zc), f"upper_arm.{s}")
        bones[f"hand.{s}"] = ((side * (xs + WRIST), 0, zc), (side * (xs + FIST[-1][0]), 0, zc), f"forearm.{s}")
        grip = (side * (xs + (FIST[1][0] + FIST[2][0]) / 2), 0, zc)
        bones[f"grip.{s}"] = (grip, (grip[0], 0.08 / HEIGHT, grip[2]), f"hand.{s}")
        kx, fx = side * leg_line(KNEE), side * leg_line(0.0)
        bones[f"thigh.{s}"] = ((side * LEG_X, 0, HIP_Z), (kx, 0, KNEE), "hips")
        bones[f"shin.{s}"] = ((kx, 0, KNEE), (fx, 0, 0.07), f"thigh.{s}")
        bones[f"foot.{s}"] = ((fx, 0, 0.07), (fx, 0.06, 0.02), f"shin.{s}")
        bones[f"toe.{s}"] = ((fx, 0.06, 0.02), (fx, 0.10, 0.02), f"foot.{s}")
    return bones


def make_rig(bones, coll):
    arm_data = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm_data)
    coll.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    with bpy.context.temp_override(active_object=rig, object=rig, selected_objects=[rig]):
        bpy.ops.object.mode_set(mode="EDIT")
    for name, (head_, tail, _) in bones.items():
        eb = arm_data.edit_bones.new(name)
        eb.head, eb.tail, eb.roll = Vector(head_) * HEIGHT, Vector(tail) * HEIGHT, 0.0
    for name, (_, _, parent) in bones.items():
        if parent:
            eb = arm_data.edit_bones[name]
            eb.parent = arm_data.edit_bones[parent]
            eb.use_connect = (eb.head - eb.parent.tail).length < 1e-4
    with bpy.context.temp_override(active_object=rig, object=rig, selected_objects=[rig]):
        bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    preview = {
        "skin": (0.80, 0.58, 0.42, 1), "hair": (0.30, 0.20, 0.10, 1), "primary": (0.36, 0.45, 0.30, 1),
        "secondary": (0.22, 0.20, 0.19, 1), "leather": (0.30, 0.20, 0.12, 1), "eyes": (0.08, 0.06, 0.06, 1),
        "lips": (0.55, 0.30, 0.28, 1), "trim": (0.62, 0.48, 0.22, 1), "metal": (0.46, 0.48, 0.50, 1),
        "under": (0.78, 0.74, 0.64, 1),
    }
    mat.diffuse_color = preview.get(name, (0.6, 0.6, 0.6, 1))
    bsdf = mat.node_tree.nodes.get("Principled BSDF") if mat.node_tree else None
    if bsdf:                         # Material Preview and the FBX read this one, not the above
        bsdf.inputs["Base Color"].default_value = mat.diffuse_color
    return mat


# Every part: its name, what builds it, and its channels. The body parts are the bare body; the
# "wear_" parts are the default outfit, worn over it (OUTFIT says what each covers).
PARTS = [
    ("head", head, lambda: ["skin", "eyes"] + (["lips"] if MOUTH else [])),
    ("torso", bare_torso, lambda: ["skin"] + (["under"] if BARE_BAND else [])),
    ("arms", bare_arms, lambda: ["skin"]),
    ("hands", fists, lambda: ["skin"]),
    ("legs", bare_legs, lambda: ["skin", "under"]),
    ("feet", bare_feet, lambda: ["skin"]),
    ("hair_short", hair_short, lambda: ["hair"]),
    ("wear_shirt", shirt, lambda: ["primary"]),
    ("wear_trousers", trousers, lambda: ["secondary", "leather"]),
    ("wear_boots", boots, lambda: ["leather"]),
]


def built_part(name, tuned=True):
    fn = next(f for n, f, _ in PARTS if n == name)
    p = Part(name)
    fn(p)
    if tuned:
        p.tune(load_tuning())
    return p


def capture(collection, only=None):
    """Read hand-tuned parts back: every mesh in `collection` named after a part (or only those
    in `only`) is compared with an untuned build, and the differences replace that part's offsets
    in TUNING_FILE."""
    all_tuning = {}
    if os.path.exists(TUNING_FILE):
        with open(TUNING_FILE) as f:
            all_tuning = json.load(f)
    mine = all_tuning.setdefault(SEX, {})
    report = {}
    for obj in collection.objects:
        if obj.type != "MESH":
            continue
        name = obj.name.split(".")[0]
        name = name[len("body_"):] if name.startswith("body_") else name
        if name not in [n for n, _, _ in PARTS] or (only and name not in only):
            continue
        if obj.mode == "EDIT":
            cur = [v.co.copy() for v in bmesh.from_edit_mesh(obj.data).verts]
        else:
            cur = [v.co.copy() for v in obj.data.vertices]
        p = built_part(name, tuned=False)
        base = [v.co.copy() for v in p.bm.verts]
        p.bm.free()
        if len(cur) != len(base):
            report[name] = f"skipped: {len(cur)} vertices, the build has {len(base)}"
            continue
        offsets = {}
        for i, (a, b) in enumerate(zip(cur, base)):
            d = (a - b) / HEIGHT
            if d.length > 1e-4:
                offsets[str(i)] = [round(x, 5) for x in d]
        mine[name] = offsets
        report[name] = len(offsets)
    with open(TUNING_FILE, "w") as f:
        json.dump(all_tuning, f, indent=1, sort_keys=True)
    return report


def build(collection=None):
    if not PREVIEW:
        bpy.ops.wm.read_homefile(use_empty=True)
    coll = collection or bpy.context.scene.collection
    parts, hairs = {}, []
    for name, _, mats in PARTS:
        obj = built_part(name).finish(mats())
        if name.startswith("hair"):
            hairs.append(obj)
        else:
            parts[name] = obj

    rig = make_rig(skeleton(), coll)
    for obj in list(parts.values()) + hairs:
        coll.objects.link(obj)
        obj.parent = rig
        md = obj.modifiers.new("Armature", "ARMATURE")
        md.object = rig
    tris = {k: sum(len(f.vertices) - 2 for f in o.data.polygons) for k, o in parts.items()}
    tris.update({o.name: sum(len(f.vertices) - 2 for f in o.data.polygons) for o in hairs})
    bare = sum(n for k, n in tris.items() if not k.startswith(("wear_", "hair")))
    if PREVIEW:
        return {"preview": NAME, "rig": rig.name, "tris": tris, "bare": bare}
    exported = [rig] + list(parts.values()) + hairs
    with bpy.context.temp_override(active_object=rig, object=rig, selected_objects=exported):
        bpy.ops.export_scene.fbx(
            filepath=EXPORT, use_selection=True, object_types={"ARMATURE", "MESH"},
            apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
            add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
            mesh_smooth_type="FACE", use_mesh_modifiers=False, bake_space_transform=True,
        )
    with open(OUTFIT_FILE, "w") as f:
        json.dump({"garments": {g: {"hides": parts_} for g, parts_ in OUTFIT.items()},
                   "default": list(OUTFIT)}, f, indent=1)
    bpy.ops.wm.save_as_mainfile(filepath=BUILT_BLEND)
    return {"export": EXPORT, "tris": tris, "bare": bare}


# LIBRARY: only define everything (build_clothes.py builds on these functions and tables).
if __name__ == "__main__" or not globals().get("LIBRARY"):
    result = build(globals().get("COLLECTION"))
