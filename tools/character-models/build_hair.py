"""
Builds hair for R'th's player heads, from the head mesh itself, so every style fits every head.

Both heads (male and female) are lofted on one grid: rings of twelve points from the neck's base
up to the crown's edge, then a single crown point (build_body.py's head(): ring r, column k is
vertex r * 12 + k; the crown is vertex 132). Column 0 is the front midline, 6 the back midline,
1-5 the right side front to back and 7-11 the left side back to front. A style names, per column
on one side (0-6, mirrored to the other), the ring its hair starts at, so a style is a few
numbers and fits any head on the grid.

Every style is one connected surface, built in this order (each part optional, all data in STYLES):
- the shell: the head's own faces from the hairline up, stood off along the head's normals by
  `volume` per ring (or one `thick`), lumped round the top (`lumps`), thin along a `part`ing,
  thin outside a middle `strip` (a mohawk). Where neighbouring columns start at different rings,
  a triangle fills the step so the hairline runs on the diagonal;
- points (`locks`): each free edge of the hairline gets a pointed tip lying on the skin, by its
  angle round the head (0 the face, +90 the right ear), so the hairline is never a smooth hem;
- `spikes`: the middle of each top face pulled out, so its faces rise into a pyramid;
- falling locks (`fall`, or groups in `falls`): each free edge in a group's span grows a lock out
  of the shell (its ends are the lock's root corners, so there is no seam), draped under gravity
  over the head and body, widening and then tapering to a point. Groups may start from the
  skull's widest ring (so the hair carries the head's outline down rather than making a ball);
- `ties`: a small seat face opened in the shell (sized to the tie, so the surface runs into it),
  then the gathered hair, a band (its own colour channel) and a tail (straight, or a `braid`
  with an end band) or a `bun`.

Hair never passes through the body or the head: falling hair and tails keep a rest distance
off the torso and arms named in BODY and slide along them where they touch, and every edge is
cast against the body afterwards. HAIR_RESULT reports any point still inside ("clipping").
The coif style is kept as the start of headwear: why it reads as cloth, not hair, is in README.md.

Run inside Blender with the heads open, through the Blender MCP:
    STYLE = "crop"; HEADS = {"body_head.003": "hair_crop", "body_head.004": "hair_crop.001"}
    BODY = {"body_head.003": ["torso_draft", "body_arms.003"], "body_head.004": [...]}
then exec this file (hair_review.py does this and renders a contact sheet). Each hair is
parented to its head's rig and skinned to "head" (falling hair and tails shifting toward "neck"
and "chest" along their length), with the "hair" channel material.
"""

import math

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

RING = 12          # points per ring
CROWN = 132        # the crown point's index
TOP_RING = 10      # the crown's edge

# Rings on the head grid: 0-2 the neck, 3 the jaw's edge, 4 the chin's front, 5 the mouth, 6 the
# nose's tip, 7 the eyes, 8 the brow, 9 the forehead's top, 10 the crown's edge.
STYLES = {
    # Short and close all round: off the forehead, above the ear, to the nape.
    "crop": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],   # columns 0-6: front, beside the front, temple, side, ear, behind, back
        thick=0.012,                      # metres off the scalp
        edge=0.5,                         # the edge's share of the thickness
    ),
    # Short, parted over the left eye: full on top, close at the sides, round above the ears with
    # long sideburns, the fringe swept away from the parting in points.
    "parted": dict(
        hairline=[9, 9, 9, 8, 8, 5, 4, 5, 8, 8, 9, 9],   # all twelve columns: the parting makes it one-sided
        volume={"crown": 0.030, 10: 0.026, 9: 0.020, 8: 0.010, "low": 0.005},
        edge=0.6,
        lumps=dict(**{"from": 9}, count=5, amount=0.30),
        part=dict(**{"from": 9}, angle=-28, width=12, depth=0.25),
        locks=[   # by where the edge lies round the head (degrees: 0 the face, +90 the right ear)
            dict(span=(-20, 55), length=0.030, sweep=0.012),    # the fringe, swept right
            dict(span=(-36, -20), length=0.0),                  # the parting: a notch
            dict(span=(-58, -36), length=0.022, sweep=-0.008),  # the fringe's left, swept left
            dict(span=(58, 85), length=0.045),                  # sideburns
            dict(span=(-85, -58), length=0.045),
            dict(span=(85, 135), length=0.016),                 # round behind the ears
            dict(span=(-135, -85), length=0.016),
            dict(span=(135, 225), length=0.032),                # the nape
        ],
    ),
    # Long and loose, parted in the middle: a full top, and below the ears separate locks that
    # fall over the shoulders, two each side framing the face and falling in front.
    "long": dict(
        hairline=[9, 9, 9, 8, 7, 6, 6],
        volume={"crown": 0.028, 10: 0.024, 9: 0.018, 8: 0.014, "low": 0.012},
        edge=0.6,
        lumps=dict(**{"from": 9}, count=6, amount=0.20),
        part=dict(**{"from": 9}, angle=0, width=8, depth=0.30),
        locks=[
            dict(span=(-6, 6), length=0.0),                     # the parting
            dict(span=(6, 40), length=0.022, sweep=0.012),      # the fringe, falling away from it
            dict(span=(-40, -6), length=0.022, sweep=-0.012),
            dict(span=(40, 75), length=0.020),
            dict(span=(-75, -40), length=0.020),
        ],
        fall=dict(                          # locks grown from every edge of the shell this far from the face
            **{"from": 50},                 # degrees round the head
            length=[(50, 0.22), (90, 0.27), (135, 0.31), (180, 0.33)],     # by angle from the face
            forward=[(50, 0.12), (90, 0.0), (180, -0.12)],                 # lean to the front or back
            height=[(50, 0.012), (180, 0.018)],                            # how far each lock swells out
            vary=0.12,                      # neighbouring locks a little longer and shorter
            spread=0.03,
            per_edge=1,                     # locks from each edge of the rim
            widen=0.7,                      # how much wider than its root a lock spreads, overlapping its neighbours
            segments=6,
            rest=0.010,                     # how far a lock lies off the body it rests on
        ),
    ),
    # Spiky: short at the sides, the top pulled up into spikes, tallest over the forehead, so the
    # outline from the front is tall and jagged.
    "spiky": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],
        volume={"crown": 0.016, 10: 0.016, 9: 0.014, 8: 0.010, "low": 0.006},
        edge=0.6,
        locks=[
            dict(span=(-50, 50), length=0.014),                 # short points along the hairline
            dict(span=(50, 85), length=0.030),                  # sideburns
            dict(span=(-85, -50), length=0.030),
            dict(span=(85, 275), length=0.018),
        ],
        spikes=dict(
            **{"from": 8},                  # faces from this ring up get a spike
            length=[(0, 0.050), (90, 0.038), (180, 0.030)],     # by angle from the face
            up=0.7, back=0.3, vary=0.35,
        ),
    ),
    # Swept: parted far to the left, a heavy fringe sweeping across the forehead and over the
    # right eye, the sides and back falling to the jaw and the nape.
    "swept": dict(
        hairline=[9, 9, 9, 8, 8, 6, 6, 6, 8, 8, 9, 9],
        volume={"crown": 0.030, 10: 0.026, 9: 0.020, 8: 0.014, "low": 0.010},
        edge=0.6,
        lumps=dict(**{"from": 9}, count=5, amount=0.20),
        part=dict(**{"from": 9}, angle=-42, width=10, depth=0.30),
        locks=[
            dict(span=(-48, -36), length=0.0),                  # the parting
            dict(span=(-62, -48), length=0.018, sweep=-0.010),  # left of it, short and swept back
        ],
        falls=[
            dict(   # the fringe: from the parting across the forehead, over the right eye
                span=(-36, 62), from_widest=False,
                length=[(0, 0.090), (40, 0.105), (62, 0.110)],
                sweep=0.55, forward=0.15, spread=0.0,
                height=0.012, widen=0.5, vary=0.10, segments=4, rest=0.006,
            ),
            dict(   # the sides and back, to the jaw and the nape
                span=(62, 298),
                length=[(62, 0.10), (120, 0.11), (180, 0.12)],
                forward=[(62, 0.05), (180, -0.05)],
                height=0.012, widen=0.5, vary=0.12, segments=4, rest=0.008,
            ),
        ],
    ),
    # Two braids: parted in the middle, pulled back over the ears and tied behind each one, the
    # braids falling forward over the shoulders onto the chest, a band at each end.
    "braids": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],
        volume={"crown": 0.016, 10: 0.015, 9: 0.013, 8: 0.010, "low": 0.007},
        edge=0.6,
        part=dict(**{"from": 9}, angle=0, width=8, depth=0.35),
        locks=[
            dict(span=(-6, 6), length=0.0),
            dict(span=(6, 50), length=0.014, sweep=0.010),
            dict(span=(-50, -6), length=0.014, sweep=-0.010),
            dict(span=(50, 310), length=0.012),
        ],
        ties=[dict(angle=a, ring=7.4, radius=0.015, out=0.006, band=0.006, rise=-0.1, material="secondary",
                   tail=dict(length=0.32, radius=0.014, widen=0.1, taper=0.3, segments=10, curl=1.2,
                             forward=0.45, braid=0.28, end_band=True))
              for a in (108, -108)],
    ),
    # A bob with curtains: parted in the middle, the fringe falling to the cheekbones and away
    # to each side, the sides falling straight to the chin.
    "bob": dict(
        hairline=[9, 9, 9, 8, 8, 6, 6],
        volume={"crown": 0.026, 10: 0.022, 9: 0.018, 8: 0.014, "low": 0.012},
        edge=0.6,
        lumps=dict(**{"from": 9}, count=6, amount=0.15),
        part=dict(**{"from": 9}, angle=0, width=8, depth=0.30),
        falls=[
            dict(   # the curtains
                span=(-50, 50), from_widest=False,
                length=[(0, 0.070), (50, 0.100)],
                away=0.5, forward=0.15, spread=0.0,
                height=0.012, widen=0.4, vary=0.05, segments=4, rest=0.006,
            ),
            dict(   # the sides and back, blunt at the chin
                span=(50, 310),
                length=[(50, 0.125), (120, 0.125), (180, 0.105)],
                forward=[(50, 0.08), (180, 0.0)],
                height=0.014, widen=0.5, vary=0.05, segments=4, rest=0.008,
            ),
        ],
    ),
    # A mohawk: the sides shaved close, a tall crest of spikes along the middle.
    "mohawk": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],
        volume={"crown": 0.018, 10: 0.018, 9: 0.016, 8: 0.012, "low": 0.008},
        strip=dict(width=0.036, outside=0.0025),   # hair only within this far of the middle
        edge=0.8,
        locks=[dict(span=(-180, 180), length=0.006)],
        spikes=dict(**{"from": 7}, width=0.022, flat=True, length=[(0, 0.060), (90, 0.060), (180, 0.045)],
                    up=1.0, back=0.2, vary=0.25),
    ),
    # Buzzed: close all over, a crisp hairline.
    "buzz": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],
        thick=0.003,
        edge=1.0,
        locks=[
            dict(span=(-55, 55), length=0.006),
            dict(span=(55, 85), length=0.018),
            dict(span=(-85, -55), length=0.018),
            dict(span=(85, 275), length=0.008),
        ],
    ),
    # Shaggy: full and uneven, a ragged fringe falling forward to the brows, the ears covered,
    # ragged ends to the middle of the neck.
    "shaggy": dict(
        hairline=[9, 9, 9, 8, 8, 6, 6],
        volume={"crown": 0.030, 10: 0.026, 9: 0.020, 8: 0.016, "low": 0.012},
        edge=0.6,
        lumps=dict(**{"from": 9}, count=5, amount=0.30),
        falls=[
            dict(   # the fringe
                span=(-55, 55), from_widest=False,
                length=[(0, 0.065), (55, 0.080)],
                away=0.0, forward=0.30, spread=0.0,
                height=0.015, widen=0.8, vary=0.30, segments=3, rest=0.006,
            ),
            dict(   # the sides and back
                span=(55, 305),
                length=[(55, 0.110), (120, 0.130), (180, 0.140)],
                forward=[(55, 0.08), (180, -0.05)], spread=0.30,
                height=0.018, widen=0.7, vary=0.30, segments=4, rest=0.008,
            ),
        ],
    ),
    # Pulled back into a ponytail: close to the head all over, a clean hairline with short points,
    # tied high at the back, the tail leaving the head and falling between the shoulders.
    "ponytail": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],
        volume={"crown": 0.014, 10: 0.013, 9: 0.011, 8: 0.008, "low": 0.006},
        edge=0.6,
        lumps=dict(**{"from": 9}, count=7, amount=0.15),
        locks=[
            dict(span=(-50, 50), length=0.010),                 # the hairline, pulled back
            dict(span=(50, 85), length=0.022),                  # a short point before each ear
            dict(span=(-85, -50), length=0.022),
            dict(span=(85, 275), length=0.012),                 # round the back
        ],
        tie=dict(
            angle=180, ring=8.3,            # where it is tied: the back of the head, a little above the ears
            radius=0.016,                   # the face the tail grows from
            out=0.010, band=0.008, rise=0.35,
            material="secondary",           # the band's colour channel
            tail=dict(length=0.27, radius=0.016, widen=0.6, segments=6, curl=2.5,
                      settle=0.45),         # how much it leans back in to lie on the body
        ),
    ),
    # Pulled back into a bun high on the back of the head.
    "bun": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],
        volume={"crown": 0.014, 10: 0.013, 9: 0.011, 8: 0.008, "low": 0.006},
        edge=0.6,
        lumps=dict(**{"from": 9}, count=7, amount=0.15),
        locks=[
            dict(span=(-50, 50), length=0.010),
            dict(span=(50, 85), length=0.022),
            dict(span=(-85, -50), length=0.022),
            dict(span=(85, 275), length=0.012),
        ],
        tie=dict(
            angle=180, ring=8.7,            # on the back of the head, above the ears
            radius=0.026, out=0.003, band=0.005, rise=0.25, gather=1.0,
            material="secondary",
            bun=dict(radius=0.036, depth=0.042, twist=0.4, profile=[0.95, 1.05, 0.9, 0.55]),
        ),
    ),
    # Not hair: a coif. One even shell from the skull's widest ring, wrapping the sides and back as
    # a curtain with a closed hem. Kept as the start of headwear (coifs, hoods).
    "coif": dict(
        hairline=[9, 9, 9, 8, 7, 5, 5],
        thick=0.009,
        edge=0.5,
        hang=dict(
            columns=[3, 4, 5, 6],           # temple round to the back midline (mirrored)
            hem=[1.00, 1.22, 1.40, 1.50],   # how far each column falls, in head heights below the crown
            rows=4,                         # rings between the skull and the hem
            flare=0.008,                    # metres outward at the hem
            thick=0.008,                    # the curtain's thickness
            ridges=[4, 6],                  # columns that stand proud as strands...
            ridge=0.014,                    # ...this far out at the hem...
            tips=0.22,                      # ...and fall this much further (head heights)
            gather=0.12,                    # the hem's share drawn in across the back
            weight=0.4,                     # at the hem, the neck's share (the head's is the rest)
        ),
    ),
}


def column_start(hairline):
    """The hairline round all twelve columns: seven are mirrored to the other side, twelve are as given."""
    if len(hairline) == RING:
        return list(hairline)
    return hairline + list(reversed(hairline[1:-1]))


def angle_round(p, centre_y):
    """Where p lies round the head, in degrees seen from above: 0 the face, 90 the right ear (+x),
    180 the back, -90 the left ear."""
    return math.degrees(math.atan2(p.x, p.y - centre_y))


def in_span(angle, span):
    """Whether an angle lies in a (from, to) span of degrees, either way round."""
    a, b = span
    return a <= angle <= b or a <= angle + 360 <= b or a <= angle - 360 <= b


def thickness(style, ring, angle, x=0.0):
    """How far the hair stands off the scalp at a point: the style's volume by ring (or its plain
    thickness), lumped round the top, thin along a parting."""
    vol = style.get("volume")
    if vol:
        t = vol.get(ring, vol["low"]) if ring <= TOP_RING else vol["crown"]
    else:
        t = style["thick"]
    lumps = style.get("lumps")
    if lumps and ring >= lumps["from"]:
        t *= 1 + lumps["amount"] * math.cos(math.radians(angle * lumps["count"] + lumps.get("phase", 0)))
    strip = style.get("strip")             # hair only along the middle (a mohawk): close elsewhere
    if strip and abs(x) > strip["width"]:
        return strip["outside"]
    part = style.get("part")
    if part and ring >= part["from"] and abs(((angle - part["angle"]) + 180) % 360 - 180) < part["width"]:
        t *= part["depth"]
    return t


CLEARANCE = 0.006  # metres hair keeps off the skin


def body_trees(names):
    """BVH trees of the body parts hair must stay out of (torso, arms), in the rig's rest space."""
    trees = []
    for n in names:
        me = bpy.data.objects[n].data
        trees.append(BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [list(p.vertices) for p in me.polygons]))
    return trees


def depth(p, trees):
    """How far p is outside the body (negative: inside), and the skin's normal there."""
    best = None
    for t in trees:
        hit = t.find_nearest(p)
        if hit[0] is not None and (best is None or hit[3] < best[3]):
            best = hit
    if best is None:
        return 1.0, Vector((0, 0, 1))
    loc, normal, _, _ = best
    return (p - loc).dot(normal), normal


def clear_of_body(p, trees):
    """p, pushed out along the skin's normal until it is CLEARANCE outside the body."""
    for _ in range(4):
        d, n = depth(p, trees)
        if d >= CLEARANCE:
            break
        p = p + n * (CLEARANCE - d)
    return p


def passes_through(a, b, trees):
    """Whether the segment a-b enters the body, or ends inside it."""
    d = b - a
    length = d.length
    if length < 1e-9:
        return False
    for t in trees:
        for p, q in ((a, b), (b, a)):
            hit = t.ray_cast(p, (q - p).normalized(), length)
            if hit[0] is not None:
                return True
    return depth(a, trees)[0] < 0 or depth(b, trees)[0] < 0


def clipping(ob, trees):
    """Points on the hair (corners, edge middles, face middles) that lie inside the body."""
    me = ob.data
    pts = [v.co for v in me.vertices]
    pts += [(me.vertices[e.vertices[0]].co + me.vertices[e.vertices[1]].co) / 2 for e in me.edges]
    pts += [p.center for p in me.polygons]
    return sum(1 for p in pts if depth(p, trees)[0] < 0)


def drape(root, length, segments, lean, trees, rest):
    """A lock's middle line: from the root, step by step under gravity (leaning `lean`, a
    horizontal vector), and wherever a step would come closer to the body than `rest`, held
    out on its surface and sliding along it instead. Returns segments + 1 points."""
    pts = [root.copy()]
    step = length / segments
    down = Vector((0, 0, -1))
    for _ in range(segments):
        c = pts[-1]
        d = (down + lean).normalized()
        dist, n = depth(c, trees)
        if dist < rest * 1.5:                      # in contact: slide along the surface
            d = d - n * min(0.0, d.dot(n))
            if d.length < 1e-6:
                d = lean.normalized() if lean.length else Vector((0, 0, -1))
            d.normalize()
        p = c + d * step
        for _ in range(6):
            dist, n = depth(p, trees)
            if dist >= rest:
                break
            p = p + n * (rest - dist)
        pts.append(p)
    return pts


def grow_lock(bm, edge, pts, height, trees, rest, widen=0.0):
    """A lock grown out of a free edge of the hair's shell, so the shell's surface runs on into
    it: the edge's two ends are the lock's root corners, a new vertex in the edge's middle rises
    into the ridge, and the lock tapers along pts (its middle line) to a point. Triangular in
    section: the underside toward the body, two faces and a ridge outward. Returns its new
    vertices with each one's share of the way down."""
    a, b = edge.verts
    width = (a.co - b.co).length
    _, mid = bmesh.utils.edge_split(edge, a, 0.5)
    n = len(pts) - 1
    rings = [[a, mid, b]]
    shares = {mid: 0.0}
    for i in range(1, n):
        t = i / n
        c = pts[i]
        ahead = (pts[i + 1] - c).normalized()
        _, out = depth(c, trees)
        out = (out - ahead * out.dot(ahead)).normalized()
        side = ahead.cross(out).normalized()
        if side.dot(a.co - b.co) < 0:
            side = -side
        w = width * (1 + widen * min(1.0, 3 * t)) * (1 - t) ** 0.45 / 2   # spreading, then to a point
        h = height * min(1.0, 2 * t) * (1 - t) ** 0.45   # swelling out of the shell, then tapering
        ring = [bm.verts.new(c + side * w), bm.verts.new(c + out * h), bm.verts.new(c - side * w)]
        for v in ring:
            shares[v] = t
        rings.append(ring)
    tip = bm.verts.new(pts[-1])
    shares[tip] = 1.0
    for r0, r1 in zip(rings, rings[1:]):
        bm.faces.new((r0[0], r0[1], r1[1], r1[0]))
        bm.faces.new((r0[1], r0[2], r1[2], r1[1]))
        bm.faces.new((r0[2], r0[0], r1[0], r1[2]))   # the underside
    last = rings[-1]
    for k in range(3):
        bm.faces.new((last[k], last[(k + 1) % 3], tip))
    # Keep the whole lock clear of the body, not just its middle line: every corner rests at
    # least half the rest distance off it, and any edge that still cuts through lifts its ends.
    body = trees[:-1]                     # the body parts, without the scalp
    for v in shares:
        if v is mid:
            continue
        dist, nrm = depth(v.co, body)
        if dist < rest * 0.5:
            v.co += nrm * (rest * 0.5 - dist)
    edges = {e for v in shares for e in v.link_edges}
    for _ in range(12):
        cut = [e for e in edges if passes_through(e.verts[0].co, e.verts[1].co, body)]
        if not cut:
            break
        for e in cut:
            for v in e.verts:
                if v in shares and v is not mid:
                    v.co += depth(v.co, body)[1] * 0.003
    return shares


def ring_around(bm, centre, axis, radius, sides, turn=0.0):
    """A ring of new vertices round an axis."""
    axis = axis.normalized()
    u = axis.cross(Vector((0, 0, 1)))
    if u.length < 1e-6:
        u = axis.cross(Vector((1, 0, 0)))
    u.normalize()
    v = axis.cross(u)
    return [bm.verts.new(centre + (u * math.cos(a) + v * math.sin(a)) * radius)
            for a in (turn + 2 * math.pi * i / sides for i in range(sides))]


def bridge(bm, r0, r1):
    """Quads between two rings of the same size; returns them."""
    n = len(r0)
    return [bm.faces.new((r0[i], r0[(i + 1) % n], r1[(i + 1) % n], r1[i])) for i in range(n)]


def open_patch(bm, centre_vert):
    """Removes the faces round one vertex of the shell and returns the hole's rim, in order."""
    star = list(centre_vert.link_faces)
    rim_edges = [e for f in star for e in f.edges if centre_vert not in e.verts]
    nxt = {}
    for e in rim_edges:
        a, b = e.verts
        nxt.setdefault(a, []).append(b)
        nxt.setdefault(b, []).append(a)
    loop = [rim_edges[0].verts[0]]
    prev = None
    while True:
        cands = [v for v in nxt[loop[-1]] if v is not prev]
        if not cands or cands[0] is loop[0]:
            break
        prev = loop[-1]
        loop.append(cands[0])
    bmesh.ops.delete(bm, geom=[centre_vert], context="VERTS")
    return loop


def tie_and_tail(bm, base, out, tie, trees, rest, surface):
    """A tie and a tail growing out of the hair's own surface: `base` is the rim of a hole opened
    in the shell, which runs on into the gathered hair, the band and then the tail, one round
    bundle that leaves the head along `out` and curves down under gravity onto the body,
    tapering to a point. Returns the band's faces and the new vertices with each one's share
    of the way down."""
    sides = len(base)
    centre = sum((v.co for v in base), Vector()) / sides
    # Turn the rim to run anticlockwise round `out`, as ring_around's rings do.
    turn = sum(((base[i].co - centre).cross(base[(i + 1) % sides].co - centre)).dot(out) for i in range(sides))
    if turn < 0:
        base = list(reversed(base))

    # A small face of the tie's own size, lying in the hair's surface: the shell runs into it
    # over a ring of faces on the surface, and the tail grows from it alone.
    r = tie["radius"]
    seat = [bm.verts.new(surface(centre + (v.co - centre).normalized() * r)) for v in base]
    bridge(bm, base, seat)
    seat_centre = sum((v.co for v in seat), Vector()) / sides

    def lifted(ring_from, scale, lift):
        return [bm.verts.new(seat_centre + out * lift + (v.co - seat_centre) * scale) for v in ring_from]
    gathered = lifted(seat, tie.get("gather", 0.85), tie.get("out", 0.010))
    band = lifted(seat, tie.get("gather", 0.85) * 0.92, tie.get("out", 0.010) + tie.get("band", 0.008))
    bridge(bm, seat, gathered)
    band_faces = bridge(bm, gathered, band)
    shares = {v: 0.0 for v in seat + gathered + band}

    bun = tie.get("bun")
    if bun:
        # A knot: rings swelling out from the band and closing to a point, each turned a little
        # from the last so the flat shading reads as hair coiled round.
        band_centre = sum((v.co for v in band), Vector()) / sides
        prev = band
        profile = bun.get("profile", [0.75, 1.0, 0.95, 0.65])   # radius by ring, as shares of bun radius
        count = len(profile)
        for i, share in enumerate(profile):
            t = (i + 1) / (count + 1)
            ring = ring_around(bm, band_centre + out * bun["depth"] * t, out, bun["radius"] * share, sides,
                               turn=bun.get("twist", 0.35) * (i + 1))
            best = min(range(sides), key=lambda k: (ring[k].co - prev[0].co).length)
            ring = ring[best:] + ring[:best]
            bridge(bm, prev, ring)
            for v in ring:
                shares[v] = 0.0
            prev = ring
        top = bm.verts.new(band_centre + out * bun["depth"])
        shares[top] = 0.0
        for i in range(sides):
            bm.faces.new((prev[i], prev[(i + 1) % sides], top))
        return band_faces, shares

    tail = tie["tail"]
    n = tail.get("segments", 6)
    step = tail["length"] / n
    pts = [sum((v.co for v in band), Vector()) / sides]
    heading = out.copy()
    for i in range(n):
        s = min(1.0, (i + 1) / tail.get("curl", 2.0))        # how far it has turned to fall
        d = (heading * (1 - s) + Vector((0, 0, -1)) * s).normalized()
        d = (d - Vector((heading.x, heading.y, 0)) * tail.get("settle", 0.0) * s).normalized()   # in toward the body
        d = (d + Vector((0, tail.get("forward", 0.0) * s, 0))).normalized()   # over the shoulder to the front
        dist, nrm = depth(pts[-1], trees)
        if dist < (rest + tail["radius"]) * 1.5:          # resting on the body: slide along it
            d = d - nrm * min(0.0, d.dot(nrm))
            if d.length > 1e-6:
                d.normalize()
        p = pts[-1] + d * step
        for _ in range(6):
            dist, nrm = depth(p, trees)
            if dist >= rest + tail["radius"]:
                break
            p = p + nrm * (rest + tail["radius"] - dist)
        pts.append(p)
    prev = band
    for i in range(1, n):
        t = i / n
        ahead = (pts[i + 1] - pts[i - 1]).normalized()
        rad = tail["radius"] * (1 + tail.get("widen", 0.3) * min(1.0, 3 * t)) * (1 - t) ** tail.get("taper", 0.5)
        braid = tail.get("braid", 0.0)          # a braid: alternately fat and pinched, each ring turned
        if braid:
            rad *= 1 + braid * (1 if i % 2 else -1)
        ring = ring_around(bm, pts[i], ahead, rad, sides, turn=(math.pi / sides) * (i % 2) if braid else 0.0)
        # Keep the ring's corners turned the same way as the one before, so the quads don't twist.
        best = min(range(sides), key=lambda k: (ring[k].co - prev[0].co).length)
        ring = ring[best:] + ring[:best]
        faces = bridge(bm, prev, ring)
        if tail.get("end_band") and i == n - 1:  # a band tying off the end
            band_faces = band_faces + faces
        for v in ring:
            shares[v] = t
        prev = ring
    tip = bm.verts.new(pts[-1])
    shares[tip] = 1.0
    for i in range(sides):
        bm.faces.new((prev[i], prev[(i + 1) % sides], tip))
    for v in shares:                                         # every corner clear of the body
        dist, nrm = depth(v.co, trees[:-1])
        if dist < rest * 0.5:
            v.co += nrm * (rest * 0.5 - dist)
    return band_faces, shares


def fall_groups(style):
    """A style's groups of falling locks: "falls" (a list), or one "fall" covering everything
    at least its "from" degrees from the face."""
    if style.get("falls"):
        return style["falls"]
    f = style.get("fall")
    if f:
        return [dict(f, span=f.get("span", (f["from"], 360 - f["from"])))]
    return []


def group_for(groups, angle):
    """The first group of falling locks whose span holds this angle round the head."""
    return next((g for g in groups if in_span(angle, g["span"])), None)


def at(value, angle):
    """A setting that is either one number or a table by angle from the face (by_angle)."""
    return by_angle(value, angle) if isinstance(value, (list, tuple)) else value


def by_angle(table, angle):
    """A value from a table of (degrees from the face, value), interpolated; either side alike."""
    a = abs(((angle + 180) % 360) - 180)
    for (a0, v0), (a1, v1) in zip(table, table[1:]):
        if a0 <= a <= a1:
            return v0 + (v1 - v0) * (a - a0) / (a1 - a0)
    return table[0][1] if a < table[0][0] else table[-1][1]


def widest_ring(hv, columns):
    """The ring where the skull reaches furthest out over the given columns: hair hangs from it."""
    centre_y = sum(hv[r * RING + k].co.y for r in range(5, TOP_RING + 1) for k in range(RING)) / (6 * RING)

    def reach(r):
        return sum(Vector((hv[r * RING + k].co.x, hv[r * RING + k].co.y - centre_y)).length for k in columns)
    return max(range(5, TOP_RING), key=reach), centre_y


def build(head_name, hair_name, style, body=()):
    head = bpy.data.objects[head_name]
    bodies = body_trees(body)
    hv = head.data.vertices
    start = column_start(style["hairline"])
    hang = style.get("hang")
    chain = []                               # the hanging columns in order round the head
    if hang:
        right = hang["columns"]
        chain = right + [RING - k for k in reversed(right) if k not in (0, 6)]
        w, centre_y = widest_ring(hv, chain)
        for k in chain:
            start[k] = w                     # the shell stops where the curtain takes over
    groups = fall_groups(style)
    falls_from = set()                       # columns whose hair falls in locks from the widest ring
    if groups:
        cy0 = sum(hv[r * RING + k].co.y for r in range(5, TOP_RING + 1) for k in range(RING)) / (6 * RING)
        for k in range(RING):
            g = group_for(groups, angle_round(hv[8 * RING + k].co, cy0))
            if g and g.get("from_widest", True):
                falls_from.add(k)
        # Locks start at the skull's widest ring, so they carry its outline on down instead of
        # tucking in under it (a ball on a fringe of strands).
        if falls_from:
            w, _ = widest_ring(hv, sorted(falls_from))
            for k in falls_from:
                start[k] = max(start[k], w)

    def in_hair(r, k):
        return r >= start[k % RING]

    bm = bmesh.new()
    layer = bm.verts.layers.int.new("head_index")   # the head vertex a shell vertex stands on
    made = {}

    def v(r, k):
        key = (r, k % RING)
        if key not in made:
            made[key] = bm.verts.new(hv[r * RING + k % RING].co)
            made[key][layer] = r * RING + k % RING
        return made[key]

    for r in range(min(start), TOP_RING):
        for k in range(RING):
            quad = [(r, k), (r, k + 1), (r + 1, k + 1), (r + 1, k)]
            inside = [q for q in quad if in_hair(*q)]
            if len(inside) == 4:
                bm.faces.new([v(*q) for q in quad])
            elif len(inside) == 3:
                bm.faces.new([v(*q) for q in inside])
    top = bm.verts.new(hv[CROWN].co)
    top[layer] = CROWN
    for k in range(RING):
        bm.faces.new((v(TOP_RING, k), v(TOP_RING, k + 1), top))

    # The curtain: an outer and an inner sheet from the widest ring down to each column's hem,
    # closed along the hem and the two front edges. Its top is the shell's own edge.
    curtain = []
    if hang:
        rows = hang["rows"]
        outer = {(0, k): v(w, k) for k in chain}
        inner = {}
        for k in chain:
            inner[(0, k)] = bm.verts.new(hv[w * RING + k].co)
            inner[(0, k)][layer] = -1
            for j in range(1, rows + 1):
                outer[(j, k)] = bm.verts.new((0, 0, 0))
                inner[(j, k)] = bm.verts.new((0, 0, 0))
                outer[(j, k)][layer] = inner[(j, k)][layer] = -1
        for a, b in zip(chain, chain[1:]):
            for j in range(rows):
                bm.faces.new((outer[(j, a)], outer[(j + 1, a)], outer[(j + 1, b)], outer[(j, b)]))
                bm.faces.new((inner[(j, a)], inner[(j, b)], inner[(j + 1, b)], inner[(j + 1, a)]))
            bm.faces.new((outer[(rows, a)], inner[(rows, a)], inner[(rows, b)], outer[(rows, b)]))
        for k, flip in ((chain[0], False), (chain[-1], True)):
            for j in range(rows):
                q = (outer[(j, k)], inner[(j, k)], inner[(j + 1, k)], outer[(j + 1, k)])
                bm.faces.new(tuple(reversed(q)) if flip else q)
        curtain = (outer, inner)

    # Stand the shell off the scalp along the head's own normals, thinner at its free edge.
    cy = sum(hv[r * RING + k].co.y for r in range(5, TOP_RING + 1) for k in range(RING)) / (6 * RING)
    edge = {x for x in bm.verts if x.is_boundary}
    for x in bm.verts:
        if x[layer] >= 0:
            i = x[layer]
            ring = i // RING if i != CROWN else TOP_RING + 1
            t = thickness(style, ring, angle_round(hv[i].co, cy), hv[i].co.x)
            thin = x in edge and (i % RING) not in falls_from   # full where locks grow, thin at the fringe
            x.co += hv[i].normal * t * (style["edge"] if thin else 1.0)

    # The free edges of the shell: those in the fall's span grow locks that fall from them (below);
    # the rest each get a pointed tip lying on the skin, so the hairline is a row of points (a
    # fringe, sideburns, the nape) and never a smooth hem.
    locks = style.get("locks") or []
    rims = [e for e in bm.edges if e.is_boundary and all(v[layer] >= 0 for v in e.verts)]
    falling = []
    scalp = body_trees([head_name])[0]
    for e in rims:
        a, b = e.verts
        mid = (a.co + b.co) / 2
        angle = angle_round(mid, cy)
        g = group_for(groups, angle)
        if g:
            falling.append((e, angle, g))
            continue
        rule = next((r for r in locks if in_span(angle, r["span"])), None)
        if rule is None or rule["length"] <= 0:
            continue
        radial = Vector((mid.x, mid.y - cy, 0)).normalized()
        across = Vector((radial.y, -radial.x, 0))            # toward the right ear at the face
        tip = mid + Vector((0, 0, -rule["length"])) + across * rule.get("sweep", 0)
        loc, normal, _, _ = scalp.find_nearest(tip)
        tip = loc + normal * rule.get("lift", 0.003)
        t = bm.verts.new(tip)
        t[layer] = -1
        bm.faces.new((a, b, t))

    # Then hang the curtain from the shell's edge.
    weights = {}
    if hang:
        outer, inner = curtain
        crown_z = hv[CROWN].co.z
        height = crown_z - hv[3 * RING].co.z
        hems = dict(zip(hang["columns"], hang["hem"]))
        fall = {}                            # each column's drop, in head heights below the crown
        for k in chain:
            col = k if k <= 6 else RING - k
            fall[k] = hems[col] + (hang.get("tips", 0) if col in hang.get("ridges", ()) else 0)

        def place():
            for k in chain:
                col = k if k <= 6 else RING - k
                ridge = col in hang.get("ridges", ())
                hem_z = crown_z - fall[k] * height
                top_out = outer[(0, k)].co.copy()
                out = Vector((top_out.x, top_out.y - centre_y, 0)).normalized()
                back = (col - hang["columns"][0]) / max(1, 6 - hang["columns"][0])   # 0 by the face, 1 behind
                for j in range(1, rows + 1):
                    t = j / rows
                    p = top_out + out * (hang["flare"] + (hang.get("ridge", 0) if ridge else 0)) * t
                    p.x *= 1 - hang.get("gather", 0) * back * t
                    p.z = top_out.z + (hem_z - top_out.z) * t
                    q = clear_of_body(p - out * hang["thick"], bodies)   # the inner sheet rests on the body
                    inner[(j, k)].co = q
                    outer[(j, k)].co = q + out * hang["thick"]
                    for x in (outer[(j, k)], inner[(j, k)]):
                        weights[x] = hang["weight"] * t

        def strands_through_body():
            """The columns whose sheets pass through the body: every edge and face diagonal, cast."""
            bad = set()
            for sheet in (outer, inner):
                for a, b in zip(chain, chain[1:]):
                    for j in range(rows + 1):
                        pairs = [((j, a), (j, b))]
                        if j < rows:
                            pairs += [((j, a), (j + 1, a)), ((j, b), (j + 1, b)),
                                      ((j, a), (j + 1, b)), ((j, b), (j + 1, a))]
                        for p, q in pairs:
                            if passes_through(sheet[p].co, sheet[q].co, bodies):
                                bad.update((a, b) if p[1] != q[1] else (p[1],))
            return bad

        place()
        for _ in range(40):                  # a strand that meets the body ends where it rests on it
            bad = strands_through_body() if bodies else set()
            if not bad:
                break
            for k in bad:
                fall[k] -= 0.04
            place()

    # Spikes: the middle of each shell face over the top pulled out into a point, so the faces
    # round it rise into a pyramid. Still one surface; no pieces stuck on.
    spikes = style.get("spikes")
    if spikes:
        top = [f for f in bm.faces
               if all(v[layer] >= 0 and (v[layer] == CROWN or v[layer] // RING >= spikes["from"]) for v in f.verts)
               and abs(f.calc_center_median().x) <= spikes.get("width", 1.0)]
        centre_z = sum(hv[r * RING].co.z for r in (7, 8)) / 2
        hub = Vector((0, cy, centre_z))
        poked = bmesh.ops.poke(bm, faces=top)["verts"]
        poked.sort(key=lambda v: (round(v.co.z, 3), angle_round(v.co, cy)))
        for n, v in enumerate(poked):
            v[layer] = -1
            angle = angle_round(v.co, cy)
            out = (v.co - hub).normalized()
            back = Vector((0, -1, 0)) if abs(angle) < 90 else Vector((0, 0, 0))
            d = out + Vector((0, 0, spikes.get("up", 0.6))) + back * spikes.get("back", 0.25)
            if spikes.get("flat"):                         # a crest: spikes stay in the middle plane
                d.x = 0.0
            d.normalize()
            length = at(spikes["length"], angle) * (1 + spikes.get("vary", 0.3) * (1 if n % 2 else -1))
            v.co += d * length

    # Falling locks, grown from the shell's lower edge and draped over the body: the hair is one
    # piece at the top that parts into locks as it falls.
    lock_share = {}
    if falling:
        trees = bodies + [scalp]
        split = []
        for e, angle, g in falling:
            pieces = g.get("per_edge", 1)            # locks per edge of the shell's rim
            a, b = e.verts
            chain = [a]
            rest_edge = e
            for p in range(pieces - 1, 0, -1):       # cut the remainder into equal parts
                _, v = bmesh.utils.edge_split(rest_edge, chain[-1], 1.0 / (p + 1))
                v[layer] = -1
                chain.append(v)
                rest_edge = bm.edges.get((v, b))
            chain.append(b)
            for u, w in zip(chain, chain[1:]):
                split.append((bm.edges.get((u, w)), angle_round((u.co + w.co) / 2, cy), g))
        split.sort(key=lambda eag: eag[1])
        for n, (e, angle, g) in enumerate(split):
            a, b = e.verts
            root = (a.co + b.co) / 2
            radial = Vector((root.x, root.y - cy, 0)).normalized()
            across = Vector((radial.y, -radial.x, 0))   # toward the right ear at the face
            rest = g.get("rest", 0.008)
            length = at(g["length"], angle) * (1 + g.get("vary", 0) * (1 if n % 2 else -1))
            lean = (Vector((0, at(g.get("forward", 0.0), angle), 0)) + radial * g.get("spread", 0.03)
                    + across * (at(g.get("sweep", 0.0), angle)
                                + g.get("away", 0.0) * (1 if angle > g.get("from_part", 0.0) else -1)))
            pts = drape(root, length, g.get("segments", 5), lean, trees, rest)
            lock_share.update(grow_lock(bm, e, pts, at(g["height"], angle), trees, rest, g.get("widen", 0.0)))

    # A tie and its tail (a ponytail, a low tie...), grown out of the shell at the shell vertex
    # nearest the tie's place.
    ties = style.get("ties") or ([style["tie"]] if style.get("tie") else [])
    for tie in ties:
        lo = int(tie["ring"])
        z_of = lambda r: sum(hv[r * RING + k].co.z for k in range(RING)) / RING
        z = z_of(lo) + (z_of(min(lo + 1, TOP_RING)) - z_of(lo)) * (tie["ring"] - lo)
        a = math.radians(tie["angle"])
        d = Vector((math.sin(a), math.cos(a), 0))
        hit = scalp.ray_cast(Vector((0, cy, z)) + d * 0.3, -d, 0.3)
        centre = min((v for v in bm.verts if v[layer] >= 0 and not v.is_boundary),
                     key=lambda v: (v.co - hit[0]).length)
        out = (hv[centre[layer]].normal + Vector((0, 0, tie.get("rise", 0.3)))).normalized()
        base = open_patch(bm, centre)
        seat_thick = thickness(style, lo, tie["angle"])

        def on_hair(p):                       # a point moved onto the hair's surface
            loc, nrm, _, _ = scalp.find_nearest(p)
            return loc + nrm * seat_thick
        band_faces, shares = tie_and_tail(bm, base, out, tie, bodies + [scalp], tie.get("rest", 0.008), on_hair)
        for f in band_faces:
            f.material_index = 1
        lock_share.update(shares)

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.verts.index_update()
    neck_share = {x.index: s for x, s in weights.items()}
    lock_share = {x.index: s for x, s in lock_share.items()}

    old = bpy.data.objects.get(hair_name)
    me = bpy.data.meshes.new(hair_name)
    bm.to_mesh(me)
    bm.free()
    centre = sum((p.co for p in me.vertices), Vector()) / len(me.vertices)
    out = sum((p.normal.dot(p.center - centre) for p in me.polygons))
    if out < 0:
        for p in me.polygons:
            p.flip()
    me.materials.append(bpy.data.materials["hair"])
    if ties:
        me.materials.append(bpy.data.materials[ties[0].get("material", "secondary")])   # the band
    me.use_mirror_x = len(style["hairline"]) != RING and "part" not in style

    if old:
        old_me = old.data
        old.data = me
        bpy.data.meshes.remove(old_me)
        ob = old
    else:
        ob = bpy.data.objects.new(hair_name, me)
        for c in head.users_collection:
            c.objects.link(ob)
        ob.parent = head.parent
        ob.matrix_parent_inverse = head.matrix_parent_inverse.copy()
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = head.parent
    me.name = hair_name
    ob.vertex_groups.clear()
    head_group = ob.vertex_groups.new(name="head")
    neck_group = ob.vertex_groups.new(name="neck")
    chest_group = ob.vertex_groups.new(name="chest") if lock_share else None
    for i in range(len(me.vertices)):
        if i in lock_share:                  # a lock: from the head at its root toward the chest at its tip
            t = lock_share[i]
            head_group.add([i], 1.0 - 0.7 * t, "REPLACE")
            if t:
                neck_group.add([i], 0.35 * t, "REPLACE")
                chest_group.add([i], 0.35 * t, "REPLACE")
            continue
        share = neck_share.get(i, 0.0)
        head_group.add([i], 1.0 - share, "REPLACE")
        if share:
            neck_group.add([i], share, "REPLACE")
    return {"object": ob.name, "verts": len(me.vertices), "faces": len(me.polygons),
            "clipping": clipping(ob, bodies) if bodies else None}


# HEADS: head object -> hair object name; BODY: head object -> the parts its hair must clear.
style = STYLES[globals().get("STYLE", "crop")]
HAIR_RESULT = [build(h, n, style, globals().get("BODY", {}).get(h, ()))
               for h, n in globals().get("HEADS", {}).items()]
