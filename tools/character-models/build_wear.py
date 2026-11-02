"""
Builds garments for R'th's player bodies, starting from the body mesh itself.

A garment **replaces** the body parts it covers (the client hides them when it is worn: `hides`),
so nothing of the body is left underneath to poke through. It is built in steps:

1. Copy the body faces it covers and weld them into one piece (the parts meet on shared rings:
   the torso's armholes are the arms' first rings, its bottom ring the legs' top). Every vertex
   keeps the bone weights of the body vertex it came from, so it moves with the body.
2. Shape it, only ever outward: `hang` (below the chest the cloth falls straight from the widest
   part above, a little flare toward the hem), `loose` (a sleeve widening round the arm's bone
   from the shoulder seam), `straight` (trouser legs falling from the thigh round the leg bones).
3. Stand it off the body by its thickness; a collar band; hems stepping out.
4. Drape: a few steps of gravity with the cloth pinned where it hangs from, edges kept from
   stretching, pushed out of the body wherever it touches, so it falls naturally.
5. Seal every opening back onto the body ring it came from (a short lip turning in: collar to the
   neck, sleeve to the arm, hem to the hips, cuff to the ankle), so with the covered parts hidden
   there is no hole into the garment and the edges meet the parts still shown exactly.
6. Simplify: faces that came out nearly flat are merged; only the edges the shape needs stay
   (never the edges meeting the body).

Boots are not copied from the foot: `boot` builds a simple boot round the foot (a shaft from the
ankle, an instep, a toe and heel on a flat sole, outlined by the foot's own extent), sealed to the
ankle ring.

Run inside Blender with the bodies open (BodyReview.blend's scene), through the Blender MCP; exec
this file, optionally with WEAR (garment names) and SEXES. Each garment becomes "wear_<name>"
(".001" for the female), parented to the body's rig with an Armature modifier, in its collection.
WEAR_RESULT reports vertex counts and any body point outside the garment ("poking").
"""

import importlib.util
import math
import os

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

# Exec this file with __file__ set to its path, so its neighbours load.
_spec = importlib.util.spec_from_file_location(
    "posing", os.path.join(os.path.dirname(os.path.abspath(__file__)), "posing.py"))
posing = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(posing)

# The body parts of each body, by role.
BODIES = globals().get("BODIES", {
    "male": dict(rig="Rig.004", torso="torso_draft", arms="body_arms.003", legs="wear_trousers",
                 feet="wear_boots", head="body_head.003", suffix=""),
    "female": dict(rig="Rig.005", torso="torso_draft.001", arms="body_arms.004", legs="wear_trousers.001",
                   feet="wear_boots.001", head="body_head.004", suffix=".001"),
})

ARMS_DOWN = {"upper_arm.L": posing.R("Y", -70), "upper_arm.R": posing.R("Y", 70)}   # shoulders' design pose

GARMENTS = {
    # A loose t-shirt: hangs straight from the chest (and her bust), loose short sleeves, a crew
    # neck band, the hem stepping out over the trousers.
    "shirt_loose": dict(
        parts=[dict(role="torso", drop=dict(flare=0.03)),
               dict(role="arms", sleeve=0.30, loose=dict(end=1.30))],
        thick=0.004, material="primary", hides=["torso"],
        collar=dict(height=0.010, out=0.002), hem=0.003,
        simplify=2.0,                   # the hang already makes straight panels: no drape, only flat merges
    ),
    # Straight trousers: the legs fall from the thigh, tapering a little, a cuff over the boots.
    "trousers_straight": dict(
        parts=[dict(role="legs", caps=False, straight=dict(taper=0.78))],
        thick=0.004, material="secondary", hides=["legs"], hem=0.004, waist=True,
        drape=dict(pin="crotch", steps=24), simplify=6.0,
    ),
    # --- Medieval (2026-10-28) ---
    # A linen shirt with long sleeves: the loose shirt, sleeves to the wrist.
    "shirt_long": dict(
        parts=[dict(role="torso", drop=dict(flare=0.03)),
               dict(role="arms", sleeve=1.0, loose=dict(end=1.18))],
        thick=0.004, material="primary", hides=["torso", "arms"],
        collar=dict(height=0.008, out=0.002), hem=0.003, simplify=2.0,
    ),
    # A belted tunic to mid-thigh: long sleeves, cloth gathered over a belt that cinches the
    # waist, then a skirt flaring over the legs. No legs under the skirt: the tunic carries its
    # own hose from a little above the hem down (`below`), and its skirt hangs from the skirt
    # chains (`skirt`, posing.py).
    "belted_tunic": dict(
        parts=[dict(role="torso", drop=dict(rings=[
                    dict(at=("waist", 0.030), fit="blouse", out=0.014),
                    dict(at=("waist", 0.013), fit="cinch", out=0.005),
                    dict(at=("waist", -0.013), fit="cinch", out=0.005, band="leather"),   # the belt
                    dict(at="hips", fit="hang", flare=0.02),
                    dict(at="crotch", fit="hang", flare=0.04),
                    dict(at=("crotch", -0.075), fit="hang", flare=0.06),                   # the hip joint's ring
                    dict(at=("crotch", -0.17), fit="hang", flare=0.10),                    # the hem
               ])),
               dict(role="arms", sleeve=1.0, loose=dict(end=1.22)),
               dict(role="legs", caps=False, below=("crotch", -0.12), material="secondary")],   # the hose
        thick=0.005, material="primary", hides=["torso", "arms", "legs"], skirt=True, lined=0.004,
        collar=dict(height=0.006, out=0.002), hem=0.004, simplify=2.0,
    ),
    # A robe to the ankles: wide bell sleeves, a cord at the waist. Nothing under it but the
    # shoes; its skirt hangs from the skirt chains (`skirt`).
    "robe": dict(
        parts=[dict(role="torso", drop=dict(rings=[
                    dict(at=("waist", 0.020), fit="blouse", out=0.012),
                    dict(at=("waist", 0.006), fit="cinch", out=0.004),
                    dict(at=("waist", -0.006), fit="cinch", out=0.004, band="trim"),      # the cord
                    dict(at="hips", fit="hang", flare=0.02),
                    dict(at="crotch", fit="hang", flare=0.05),
                    dict(at=("crotch", -0.075), fit="hang", flare=0.07),                   # the hip joint's ring
                    dict(at="knee", fit="hang", flare=0.10),
                    dict(at=("ankle", 0.012), fit="hang", flare=0.16),                     # the hem, over the shoes
               ])),
               dict(role="arms", sleeve=1.0, loose=dict(end=1.75))],
        thick=0.006, material="primary", hides=["torso", "arms", "legs"], skirt=True, lined=0.005,
        collar=dict(height=0.010, out=0.004), hem=0.004, simplify=2.0,
    ),
    # Hose: close-fitting legwear, worn under a jerkin's shirt (skirts carry their own).
    "hose": dict(
        parts=[dict(role="legs", caps=False)],
        thick=0.003, material="secondary", hides=["legs"], hem=0.002, waist=True, simplify=4.0,
    ),
    # A sleeveless leather jerkin worn over the long shirt, to the hips.
    "jerkin": dict(
        parts=[dict(role="torso", drop=dict(rings=[dict(at="hips", fit="hang", flare=0.05)]))],
        thick=0.011, material="leather", hides=[], over=["shirt_long"], hem=0.003, simplify=2.0,
        armhole=0.045,                  # cut deep, so the arm comes down clear of it
    ),
    # Tall riding boots to below the knee.
    "boots_tall": dict(boot=dict(shaft_to=("knee", -0.14), shaft_gap=0.02, shaft_rings=3, instep=0.085, toe=0.045, heel=0.04,
                                 welt=0.014, welt_out=0.003, margin=0.006, flare=1.15),
                       material="leather", hides=["feet"], contain=["legs"], under=["hose"]),
    # Low turnshoes, below the ankle.
    "turnshoes": dict(boot=dict(shaft_above=-0.10, instep=0.06, toe=0.038, heel=0.032, welt=0.007,
                                welt_out=0.002, margin=0.005, flare=1.22),        # wide enough round hose
                      material="leather", hides=["feet"]),
    # Simple boots round the foot.
    "boots_simple": dict(boot=dict(shaft_above=0.035, instep=0.085, toe=0.045, heel=0.04, welt=0.014,
                                   welt_out=0.003, margin=0.006, flare=1.15),
                  material="leather", hides=["feet"]),
    # --- Plate: the first smithable melee set (2026-10-29). Simple and iconic, by proven methods:
    # the body and legs from the body (straight panels, a lip at the hips, plain tubes), the
    # rest rigid pieces on one bone each (pauldrons on the upper arms, cops on the knees' half
    # bones, the helm on the head). Tiers are recolours of the "metal" channel. ---
    "platebody": dict(
        parts=[dict(role="torso", drop=dict(rings=[
                    dict(at="hips", fit="hang", flare=0.03),
                    dict(at=("hips", -0.08), fit="hang", flare=0.18),                   # the lip
               ])),
               dict(role="arms", sleeve=1.0, loose=dict(end=1.10))],
        thick=0.012, material="metal", hides=["torso", "arms"], skirt=True, lined=0.006,
        collar=dict(height=0.022, out=0.012), hem=0.004, simplify=2.0,
    ),
    "platelegs": dict(
        parts=[dict(role="legs", caps=False, straight=dict(taper=0.86))],
        thick=0.010, material="metal", hides=["legs"], hem=0.004, waist=True, simplify=6.0,
    ),
    "knee_cops": dict(hulls=[dict(bone="shin", follow="half.shin", part="legs", over=["platelegs"], radius=0.11,
                                  keep=((0, 1, 0), 0.01), gap=0.028, stretch=(1.1, 1.45, 1.15))],
                      material="metal", hides=[]),
    "plate_pauldrons": dict(hulls=[dict(bone="upper_arm", part="arms", over=["platebody"], radius=0.16,
                                        pose=ARMS_DOWN, keep=((0, 0, 1), 0.09), outward=0.04, gap=0.045,
                                        stretch=(1.55, 1.25, 1.12))],
                            material="metal", hides=[]),
    "full_helm": dict(hulls=[dict(bone="head", part="head", radius=0.3, offset=(0, 0, 0.09),
                                  keep=((0, 0, 1), 0.11), gap=0.04, stretch=(1.12, 1.12, 1.06),
                                  zones=[dict(facing=(0, 1, 0), z=(-0.035, 0.012), material="shadow", cos=0.55)])],
                      material="metal", hides=["head"]),
    "plate_boots": dict(boot=dict(shaft_above=0.05, instep=0.09, toe=0.05, heel=0.045, welt=0.016,
                                  welt_out=0.004, margin=0.009, flare=1.22),
                        material="metal", hides=["feet"]),
    # --- Armour: rigid plates (2026-10-28) ---
    # Greaves round the shins, over the hose.
    "greaves": dict(plates=[dict(bone="shin", t=(0.14, 0.86), over=["hose"], gap=0.008, notch=(180, 0.16))],
                    material="metal", hides=[]),
    # Poleyns: caps over the front of the knees, on the knees' half bones.
    "poleyns": dict(plates=[dict(bone="shin", follow="half.shin", t=(-0.14, 0.12), arc=(-75, 75), sides=5,
                                 over=["hose", "greaves"], gap=0.010, flare=1.08)],
                    material="metal", hides=[]),
    # Vambraces round the forearms, over the sleeves.
    "vambraces": dict(plates=[dict(bone="forearm", part="arms", t=(0.2, 0.9), over=["shirt_long"], gap=0.008,
                                   notch=(0, 0.15))],
                      material="metal", hides=[]),
    # Couters: caps behind the elbows, on the elbows' half bones.
    "couters": dict(plates=[dict(bone="forearm", follow="half.forearm", part="arms", t=(-0.22, 0.12), arc=(105, 255),
                                 sides=5, over=["shirt_long", "vambraces"], gap=0.016, flare=1.08)],
                    material="metal", hides=[]),
    # Pauldrons: over the top of the shoulders, hinged above the shoulder (the half bone's pivot).
    # Still designed in the T-pose: they sit right with the arms down but float with them raised.
    # Next: design shoulder pieces in an arms-down pose (as games author armour in an A-pose).
    "pauldrons": dict(plates=[dict(bone="upper_arm", follow="half.upper_arm", part=["arms", "torso"],
                                   t=(-0.06, 0.45), arc=(-25, 205), sides=7, rings=3, reach=0.16,
                                   over=["shirt_long"], gap=0.012, flare=1.12, thick=0.008)],
                      material="metal", hides=[]),
}


# ---------------------------------------------------------------------------------------------
# Body helpers.

def tree_of(names):
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
    return (p - best[0]).dot(best[1]), best[1]


def sleeve_limit(arm, share):
    """How far from the middle (|x|) a sleeve reaching `share` of the way from shoulder to wrist
    ends: snapped to the nearest whole ring of the arm, so its hem is clean."""
    xs = [abs(v.co.x) for v in arm.data.vertices]
    shoulder, wrist = min(xs), max(xs)
    rings = {}
    for x in xs:
        rings[round(x, 3)] = rings.get(round(x, 3), 0) + 1
    whole = [x for x, n in rings.items() if n >= 12]          # both arms: six points each
    target = shoulder + share * (wrist - shoulder)
    return min(whole, key=lambda x: abs(x - target)) + 0.0015


def chest_ring(ob, below_z):
    """The height of the torso's widest whole ring below the armpits."""
    levels = {}
    cy = sum(v.co.y for v in ob.data.vertices) / len(ob.data.vertices)
    for v in ob.data.vertices:
        if v.co.z < below_z - 0.005:
            levels.setdefault(round(v.co.z, 3), []).append(Vector((v.co.x, v.co.y - cy, 0)).length)
    whole = {z: r for z, r in levels.items() if len(r) >= 8}
    return max(whole, key=lambda z: sum(whole[z]) / len(whole[z]))


def faces_of(ob, part, body=None):
    """The faces of a body part a garment covers."""
    me = ob.data
    limit = sleeve_limit(ob, part["sleeve"]) if "sleeve" in part else None
    chosen = []
    for p in me.polygons:
        if limit is not None and any(abs(me.vertices[i].co.x) > limit for i in p.vertices):
            continue
        if not part.get("caps", True) and len(p.vertices) > 4:   # a closing cap inside the body
            continue
        chosen.append(p.index)
    return chosen


def armpit_z(body):
    torso = bpy.data.objects[body["torso"]].data
    arms = bpy.data.objects[body["arms"]].data
    shared = [v.co for v in torso.vertices if any((v.co - a.co).length < 1e-5 for a in arms.vertices)]
    return min(c.z for c in shared)


def crotch_z(body):
    """The lowest point weighted at least 40% to the hips: the legs' first shared ring."""
    legs = bpy.data.objects[body["legs"]]
    hips = legs.vertex_groups["hips"].index
    return min(v.co.z for v in legs.data.vertices if any(g.group == hips and g.weight >= 0.4 for g in v.groups))


def height(body, at):
    """A named height on the body, optionally with an offset: "hips" (the torso's bottom), "waist"
    (its narrowest level between the hips and the chest), "crotch", "knee", "ankle"."""
    name, off = (at, 0.0) if isinstance(at, str) else at
    torso = bpy.data.objects[body["torso"]].data
    if name == "hips":
        z = min(v.co.z for v in torso.vertices)
    elif name == "waist":
        cy = sum(v.co.y for v in torso.vertices) / len(torso.vertices)
        lo, hi = min(v.co.z for v in torso.vertices), armpit_z(body) - 0.06
        levels = {}
        for v in torso.vertices:
            if lo + 0.02 < v.co.z < hi:
                levels.setdefault(round(v.co.z, 2), []).append(horizontal(v.co, cy)[0])
        z = min(levels, key=lambda h: sum(levels[h]) / len(levels[h]))
    elif name == "crotch":
        z = crotch_z(body)
    elif name == "knee":
        z = bpy.data.objects[body["rig"]].data.bones["shin.R"].head_local.z
    elif name == "ankle":
        z = min(v.co.z for v in bpy.data.objects[body["legs"]].data.vertices)
    else:
        raise ValueError(name)
    return z + off


# ---------------------------------------------------------------------------------------------
# Shaping: each takes the body part's vertex positions (changed in place), only ever outward.

def horizontal(p, cy):
    d = Vector((p.x, p.y - cy, 0))
    return d.length, math.degrees(math.atan2(p.x, p.y - cy))


def hang(cos, opts, top_z):
    """Cloth below the chest falls straight from the widest part above it (and flares a little
    toward the hem) instead of following the body back in."""
    cy = sum(c.y for c in cos) / len(cos)
    levels = {}
    for c in cos:
        if c.z <= top_z:
            levels.setdefault(round(c.z, 2), []).append(horizontal(c, cy)[0])
    chest = max(levels, key=lambda z: sum(levels[z]) / len(levels[z]))
    hem = min(c.z for c in cos)
    # A straight panel from the chest to the hem at every angle: the chest's outline at the top,
    # at the hem as wide as anything below the chest reaches there (and a little flare). Nothing
    # of the body between (ribs, waist, hips) shapes it, so its in-between rings lie flat.
    win = opts.get("window", 25.0)
    pts = [(c, *horizontal(c, cy)) for c in cos]
    near = lambda a, a2: abs(((a2 - a) + 180) % 360 - 180) < win

    def chest_r(a):
        return max((r2 for c2, r2, a2 in pts if chest - 0.005 <= c2.z <= top_z + 1e-6 and near(a, a2)), default=0.0)

    def hem_r(a):
        return max((r2 for c2, r2, a2 in pts if c2.z < chest and near(a, a2)), default=0.0)
    for c in cos:
        if c.z >= chest:
            continue
        r, a = horizontal(c, cy)
        top, bottom = chest_r(a), max(chest_r(a), hem_r(a)) * (1 + opts.get("flare", 0.0))
        t = (chest - c.z) / max(1e-6, chest - hem)
        target = top + (bottom - top) * t
        if r > 1e-6:
            s = target / r
            c.x *= s
            c.y = cy + (c.y - cy) * s


def loosen_sleeve(cos, opts, limit, rig):
    """A sleeve: scaled out round the upper arm's bone, from 1 at the shoulder seam (so it stays
    joined to the shirt) to `end` at the sleeve's hem."""
    bones = rig.data.bones
    shoulder = min(abs(c.x) for c in cos)
    for c in cos:
        b = bones["upper_arm." + ("R" if c.x > 0 else "L")]
        head, tail = b.head_local, b.tail_local
        along = (c - head).dot((tail - head).normalized())
        mid = head + (tail - head).normalized() * along
        t = max(0.0, min(1.0, (abs(c.x) - shoulder - opts.get("seam", 0.012)) / max(1e-6, limit - shoulder)))
        s = 1 + (opts.get("end", 1.25) - 1) * t ** 0.7
        off = c - mid
        off.x = 0.0
        c.y, c.z = mid.y + off.y * s, mid.z + off.z * s


def straighten_legs(cos, opts, rig, crotch):
    """Trouser legs: below the crotch each leg falls from the thigh's widest outline round its
    own bones, tapering a little toward the ankle."""
    bones = rig.data.bones
    ankle = min(c.z for c in cos)

    def axis(side, z):
        s = "L" if side < 0 else "R"
        pts = [bones["thigh." + s].head_local, bones["shin." + s].head_local, bones["shin." + s].tail_local]
        for a, b in zip(pts, pts[1:]):
            if b.z <= z <= a.z:
                return a.lerp(b, (a.z - z) / max(1e-6, a.z - b.z))
        return pts[0] if z > pts[0].z else pts[-1]

    def polar(c):
        m = axis(c.x, c.z)
        d = Vector((c.x - m.x, c.y - m.y, 0))
        return m, d.length, math.degrees(math.atan2(d.x, d.y))
    thigh = [polar(c)[1:] for c in cos if crotch - opts.get("thigh", 0.15) <= c.z <= crotch and abs(c.x) > 0.01]
    for c in cos:
        if c.z >= crotch - 0.005:
            continue
        m, r, a = polar(c)
        env = max((r2 for r2, a2 in thigh if abs(((a2 - a) + 180) % 360 - 180) < 30), default=r)
        t = (crotch - c.z) / max(1e-6, crotch - ankle)
        target = max(r, env * (1 + (opts.get("taper", 0.75) - 1) * t))
        if r > 1e-6:
            s = target / r
            nx = m.x + (c.x - m.x) * s
            if nx * c.x <= 0 or abs(nx) < opts.get("gap", 0.004):   # the legs never cross
                nx = math.copysign(max(abs(c.x), opts.get("gap", 0.004)), c.x)
            c.x = nx
            c.y = m.y + (c.y - m.y) * s


def shaped(ob, part, body):
    cos = [v.co.copy() for v in ob.data.vertices]
    rig = bpy.data.objects[body["rig"]]
    if "hang" in part:
        hang(cos, part["hang"], armpit_z(body))
    if "loose" in part:
        loosen_sleeve(cos, part["loose"], sleeve_limit(ob, part["sleeve"]), rig)
    if "straight" in part:
        straighten_legs(cos, part["straight"], rig, crotch_z(body))
    return cos


# ---------------------------------------------------------------------------------------------
# Edges, drape, seals, simplifying.

def rim_loops(bm):
    edges = [e for e in bm.edges if e.is_boundary]
    seen, loops = set(), []
    for e0 in edges:
        if e0 in seen:
            continue
        loop, e, v = [], e0, e0.verts[0]
        while e and e not in seen:
            seen.add(e)
            loop.append(v)
            v = e.other_vert(v)
            nxt = [x for x in v.link_edges if x.is_boundary and x not in seen]
            e = nxt[0] if nxt else None
        loops.append(loop)
    return loops


def copy_weights(bm, src, dst):
    dl = bm.verts.layers.deform.active
    for k, w in src[dl].items():
        dst[dl][k] = w


def finish_edges(bm, garment, origin, body):
    """A collar band standing up round the neck; hems stepping out at the other openings."""
    loops = rim_loops(bm)
    highest = max(loops, key=lambda lp: sum(v.co.z for v in lp) / len(lp)) if loops else None
    for loop in loops:
        mid = sum((v.co for v in loop), Vector()) / len(loop)
        zs = [v.co.z for v in loop]
        flat = max(zs) - min(zs) < 0.06
        neck = flat and abs(mid.x) < 0.05 and mid.z > 1.3
        if neck and "collar" in garment:
            c = garment["collar"]
            band = []
            for v in loop:
                b = bm.verts.new(v.co + Vector((0, 0, c["height"])) +
                                 Vector((v.co.x - mid.x, v.co.y - mid.y, 0)).normalized() * c.get("out", 0.0))
                set_weights(bm, b, nearest_weights(body["head"], b.co))   # it moves with the neck
                b[origin] = v[origin]
                band.append(b)
            n = len(loop)
            for i in range(n):
                bm.faces.new((loop[i], loop[(i + 1) % n], band[(i + 1) % n], band[i]))
            continue
        if loop is highest and garment.get("waist"):     # a waistband goes under the shirt: no hem,
            rise = []                                       # and it rises inside it (tucked), so
            for v in loop:                                  # bending never opens a gap between them
                w = bm.verts.new(v.co + Vector((0, 0, garment.get("tuck", 0.025))) -
                                 Vector((v.co.x - mid.x, v.co.y - mid.y, 0)).normalized() * 0.008)
                copy_weights(bm, v, w)
                w[origin] = w.co
                rise.append(w)
            n = len(loop)
            for i in range(n):
                bm.faces.new((loop[i], loop[(i + 1) % n], rise[(i + 1) % n], rise[i]))
            free = bm.verts.layers.int.get("free")
            for w in rise:
                w[free] = 1                                 # its top stays open, inside the shirt
            continue
        if "hem" in garment:
            for v in loop:
                out = Vector((v.co.x - mid.x, v.co.y - mid.y, 0)).normalized() if flat else v.normal
                v.co += out * garment["hem"]


def nearest_weights(name, p):
    """The bone weights of the vertex of an object nearest a point."""
    ob = bpy.data.objects[name]
    q = min(ob.data.vertices, key=lambda v: (v.co - p).length)
    return {ob.vertex_groups[g.group].name: g.weight for g in q.groups}


def transfer_weights(bm, names, reach=0.04):
    """Every vertex within `reach` of one of these meshes takes the weights of the nearest point
    on it (its face's corners, by distance), so a layer worn over another moves exactly with it."""
    dl = bm.verts.layers.deform.active
    for name in names:
        ob = bpy.data.objects[name]
        me = ob.data
        t = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [list(p.vertices) for p in me.polygons])
        gname = {g.index: g.name for g in ob.vertex_groups}
        for v in bm.verts:
            loc, _, fi, dist = t.find_nearest(v.co)
            if loc is None or dist > reach:
                continue
            corners = me.polygons[fi].vertices
            ws = {}
            inv = [1.0 / max(1e-6, (me.vertices[i].co - loc).length) for i in corners]
            total = sum(inv)
            for i, k in zip(corners, inv):
                for g in me.vertices[i].groups:
                    ws[gname[g.group]] = ws.get(gname[g.group], 0.0) + g.weight * k / total
            for key in list(v[dl].keys()):
                del v[dl][key]
            set_weights(bm, v, ws)


def set_weights(bm, v, weights):
    """Gives a new vertex these bone weights ({group name: weight})."""
    dl = bm.verts.layers.deform.active
    for name, w in weights.items():
        if w <= 0:
            continue
        if name not in GROUPS:
            GROUPS.append(name)
        v[dl][GROUPS.index(name)] = w


def weights_at(body, p, thigh_share, front_share=None):
    """Bone weights for a point of cloth below the chest: from the torso where the torso is; below
    the hips shared between the hips and the thigh on its side (both thighs at the middle), more
    of the thigh the lower it hangs: up to `front_share` at the front and back of a skirt (where
    the legs push it as they swing) and `thigh_share` at its sides."""
    torso = bpy.data.objects[body["torso"]]
    tv = torso.data.vertices
    bottom = min(v.co.z for v in tv)
    if p.z >= bottom - 0.005:
        q = min(tv, key=lambda v: (v.co - p).length)
        return {torso.vertex_groups[g.group].name: g.weight for g in q.groups}
    t = min(1.0, (bottom - p.z) / max(1e-6, bottom - height(body, "knee")))   # all of it from the knee down
    cy = sum(v.co.y for v in tv) / len(tv)
    d = Vector((p.x, p.y - cy, 0)).normalized()
    front = abs(d.y) ** 2                                   # 1 at the front and back, 0 at the sides
    share = thigh_share + ((front_share if front_share is not None else thigh_share) - thigh_share) * front
    s = share * t
    side = max(-1.0, min(1.0, p.x / 0.06))
    return {"hips": 1 - s, "thigh.R": s * (0.5 + 0.5 * side), "thigh.L": s * (0.5 - 0.5 * side)}


def drop(bm, garment, body, part, thick):
    """Below the chest the cloth is not the body's: from the chest ring's loop, rings at named
    heights (`rings`):
      "cinch"  tight to the body there plus `out` (a belt),
      "blouse" the body there plus a larger `out` (cloth gathered above a belt),
      "hang"   as wide as the body and legs reach there, and never narrower than a hanging ring
               above (plus `flare`): it falls clear of them.
    Every ring is a convex outline: cloth spans the body's dents (the abdominals, the small of
    the back, between the seat's cheeks) instead of following them. And only the columns the
    outlines need are kept: a column every ring runs straight past (within `straight`) is
    dropped, with its mirror, so each side is a few flat panels.
    A ring's `band` names the channel of the faces between it and the ring above (a belt's
    leather). The last ring is the hem: sealed onto the hips if it is at them, else a free edge
    whose weights share the thighs' (`thigh`; a `fit` garment refits them). The default is one
    hanging ring at the hips (a shirt)."""
    origin = bm.verts.layers.float_vector.get("origin")
    free = bm.verts.layers.int.get("free")
    torso = bpy.data.objects[body["torso"]]
    tv = torso.data.vertices
    cy = sum(v.co.y for v in tv) / len(tv)
    chest = chest_ring(torso, armpit_z(body))
    loop = min((lp for lp in rim_loops(bm) if abs(sum(v.co.x for v in lp) / len(lp)) < 0.05),
               key=lambda lp: abs(sum(Vector(v[origin]).z for v in lp) / len(lp) - chest))
    legs = bpy.data.objects[body["legs"]].data.vertices
    solid = [v.co for v in tv if v.co.z < chest - 0.001] + [v.co for v in legs]
    opts = part["drop"]
    win = opts.get("window", 22.0)
    rings = opts.get("rings", [dict(at="hips", fit="hang", flare=opts.get("flare", 0.0))])
    bottom = min(v.co.z for v in tv)
    hip_ring = [v for v in tv if v.co.z < bottom + 0.004]
    groups = [g.name for g in torso.vertex_groups]

    def near(a, a2):
        return abs(((a2 - a) + 180) % 360 - 180) < win

    def reach(z, a, band):
        rs = [horizontal(q, cy)[0] for q in solid if abs(q.z - z) < band and near(a, horizontal(q, cy)[1])]
        return max(rs) if rs else None

    def reach_span(lo, hi, a):
        """The furthest the body reaches at this angle anywhere between two heights."""
        rs = [horizontal(q, cy)[0] for q in solid if lo - 0.005 <= q.z <= hi + 0.005 and near(a, horizontal(q, cy)[1])]
        return max(rs) if rs else None

    n = len(loop)
    angle = [horizontal(v.co, cy)[1] for v in loop]
    dirs = [Vector((v.co.x, v.co.y - cy)).normalized() for v in loop]
    widest = [horizontal(v.co, cy)[0] for v in loop]
    prev_r, prev_z = list(widest), chest
    radii, heights = [], []
    for ring in rings:
        z = height(body, ring["at"])
        rs = []
        for i in range(n):
            body_r = reach(z, angle[i], 0.025) or reach(z, angle[i], 0.06) or prev_r[i]
            if ring["fit"] == "hang":
                # Cloth falling from the ring above drapes over the widest point on the way down
                # (her seat between the hips and the crotch), so a hanging ring is never narrower.
                body_r = max(body_r, reach_span(z, prev_z, angle[i]) or 0.0) + ring.get("clear", 0.004)
            if ring["fit"] == "cinch":
                rs.append(body_r + thick + ring.get("out", 0.0))
            elif ring["fit"] == "blouse":
                rs.append(body_r + thick + ring.get("out", 0.01))
            else:
                rs.append(max(body_r + thick, widest[i]) * (1 + ring.get("flare", 0.0)))
        rs = convex(rs, dirs)
        widest = [max(w, r) for w, r in zip(widest, rs)] if ring["fit"] == "hang" else rs
        radii.append(rs)
        heights.append(z)
        prev_r, prev_z = rs, z
    keep = straight_columns(radii, dirs, opts.get("straight", 0.004))
    if abs(heights[-1] - bottom) < 0.005:          # a hem sealed onto the hips meets the waistband below
        keep = list(range(n))                      # column for column, or gaps open between them
    prev = None
    for k, ring in enumerate(rings):
        z = heights[k]
        last = k == len(rings) - 1
        made = {}
        for i in keep:
            d, r = dirs[i], radii[k][i]
            w = bm.verts.new(Vector((d.x * r, cy + d.y * r, z)))
            if last and abs(z - bottom) < 0.005:                   # a shirt's hem: seal onto the hips
                src = min(hip_ring, key=lambda q: abs(((horizontal(q.co, cy)[1] - angle[i]) + 180) % 360 - 180))
                w[origin] = src.co
                set_weights(bm, w, {groups[g.group]: g.weight for g in src.groups})
            else:
                w[origin] = w.co
                set_weights(bm, w, weights_at(body, w.co, opts.get("thigh", 0.5), opts.get("thigh_front")))
                if last:
                    w[free] = 1                                     # a skirt's hem hangs free
            made[i] = w
        for a, b in zip(keep, keep[1:] + keep[:1]):
            if prev is None:                                        # onto the chest loop's own vertices
                span = [loop[(a + t) % n] for t in range((b - a) % n + 1)]
                f = bm.faces.new(span + [made[b], made[a]])
            else:
                f = bm.faces.new((prev[a], prev[b], made[b], made[a]))
            if ring.get("band"):
                f.material_index = material_slot(garment, ring["band"])
        prev = made


def convex(rs, dirs):
    """Radii pushed out to the convex outline of the points they make round the middle."""
    pts = [d * r for d, r in zip(dirs, rs)]
    order = sorted(range(len(pts)), key=lambda i: (pts[i].x, pts[i].y))

    def cross(o, a, b):
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x)
    hull = []
    for seq in (order, order[::-1]):
        part = []
        for i in seq:
            while len(part) >= 2 and cross(pts[part[-2]], pts[part[-1]], pts[i]) <= 0:
                part.pop()
            part.append(i)
        hull += part[:-1]
    edges = [(pts[a], pts[b]) for a, b in zip(hull, hull[1:] + hull[:1])]
    out = []
    for d, r in zip(dirs, rs):
        best = r
        for u, v in edges:                                          # where the ray leaves the outline
            e = v - u
            den = d.x * e.y - d.y * e.x
            if abs(den) < 1e-12:
                continue
            t = (u.x * e.y - u.y * e.x) / den
            s_ = (u.x * d.y - u.y * d.x) / den
            if -1e-6 <= s_ <= 1 + 1e-6 and t > best:
                best = t
        out.append(best)
    return out


def straight_columns(radii, dirs, tol):
    """The columns to keep: drops, in mirrored pairs, every column that each ring runs straight
    past (within `tol` of the line between its kept neighbours), the flattest first."""
    n = len(dirs)
    keep = list(range(n))

    def mirror(i):
        return min(range(n), key=lambda j: (dirs[j] - Vector((-dirs[i].x, dirs[i].y))).length)

    def deviation(i):
        k = keep.index(i)
        a, b = keep[k - 1], keep[(k + 1) % len(keep)]
        worst = 0.0
        for rs in radii:
            p, u, v = dirs[i] * rs[i], dirs[a] * rs[a], dirs[b] * rs[b]
            e = v - u
            t = max(0.0, min(1.0, (p - u).dot(e) / max(1e-12, e.dot(e))))
            worst = max(worst, (p - (u + e * t)).length)
        return worst
    while len(keep) > 8:
        i = min(keep, key=deviation)
        pair = {i, mirror(i)}
        if any(deviation(j) > tol for j in pair if j in keep):
            break
        keep = [j for j in keep if j not in pair]
    return keep


def material_slot(garment, name):
    """The slot of a channel on this garment: the main one is 0, others follow in order of use."""
    names = garment.setdefault("_slots", [garment["material"]])
    if name not in names:
        names.append(name)
    return names.index(name)


GROUPS = []


def cut_armholes(bm, body, depth):
    """Deeper armholes for a sleeveless garment: faces within `depth` of the arm's shoulder
    opening are removed, so the arm can come down without pushing into the garment's side."""
    origin = bm.verts.layers.float_vector.get("origin")
    arms = bpy.data.objects[body["arms"]].data
    torso = bpy.data.objects[body["torso"]].data
    seam = [v.co.copy() for v in arms.vertices if any((v.co - t.co).length < 1e-5 for t in torso.vertices)]
    gone = [f for f in bm.faces
            if min((Vector(f.calc_center_median()) - p).length for p in seam) < depth
            and abs(f.calc_center_median().x) > 0.06]
    bmesh.ops.delete(bm, geom=gone, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")


def drape(bm, opts, trees, rest, pin_z):
    """A few steps of gravity: vertices above pin_z (by where they came from on the body) hang
    still; the rest fall, edges kept between 85% and 100% of their length, pushed out of the body."""
    origin = bm.verts.layers.float_vector.get("origin")
    free = [v for v in bm.verts if Vector(v[origin]).z < pin_z]
    lengths = {e: e.calc_length() for e in bm.edges}
    g = Vector((0, 0, -opts.get("gravity", 0.0015)))
    for _ in range(opts.get("steps", 20)):
        for v in free:
            v.co += g
        for _ in range(4):
            for e, l0 in lengths.items():
                a, b = e.verts
                d = b.co - a.co
                l = d.length
                if l < 1e-9:
                    continue
                target = min(max(l, 0.85 * l0), l0)
                if abs(l - target) < 1e-7:
                    continue
                fa, fb = (a in free_set), (b in free_set)
                if not (fa or fb):
                    continue
                corr = d * ((l - target) / l)
                if fa and fb:
                    a.co += corr * 0.5
                    b.co -= corr * 0.5
                elif fa:
                    a.co += corr
                else:
                    b.co -= corr
        for v in free:
            dist, n = depth(v.co, trees)
            if dist < rest:
                v.co += n * (rest - dist)


free_set = set()


def seal(bm, origin):
    """Every opening turned back in onto the body ring it came from: a short lip, so the garment
    is closed where the body parts it replaces used to be. A free hem (a skirt's) stays open."""
    free = bm.verts.layers.int.get("free")
    for loop in rim_loops(bm):
        if free is not None and any(v[free] for v in loop):
            continue
        inner = []
        partl = bm.verts.layers.int.get("part")
        for v in loop:
            w = bm.verts.new(Vector(v[origin]))
            copy_weights(bm, v, w)
            w[origin] = v[origin]
            if partl is not None:
                w[partl] = v[partl]                    # a lip is part of what it closes
            inner.append(w)
        n = len(loop)
        for i in range(n):
            edge = bm.edges.get((loop[i], loop[(i + 1) % n]))
            mat = edge.link_faces[0].material_index if edge and edge.link_faces else 0   # the edge's colour
            try:
                bm.faces.new((loop[i], loop[(i + 1) % n], inner[(i + 1) % n], inner[i])).material_index = mat
            except ValueError:
                pass
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)


def simplify(bm, degrees):
    """Merges faces that are nearly flat with their neighbours, keeping every edge of the lips
    that meet the body and every joint ring: a vertex shared between two bones is where the
    garment bends (removing the knee's ring left faces from thigh to shin that tore when bent)."""
    dl = bm.verts.layers.deform.active
    keep = set()
    for v in bm.verts:
        if sum(1 for w in v[dl].values() if w > 0.05) > 1:
            keep.add(v)
            continue
        if v.is_boundary:
            keep.add(v)
            keep.update(e.other_vert(v) for e in v.link_edges)
    verts = [v for v in bm.verts if v not in keep]
    edges = [e for e in bm.edges if not any(v in keep for v in e.verts)]
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(degrees), verts=verts, edges=edges)

# ---------------------------------------------------------------------------------------------
# Rigid plates (armour).

def plates(bm, body, garment):
    """Rigid armour: each plate a solid band (an outer and an inner wall, joined round its rims)
    round a bone, following that bone alone, so it never stretches: a greave on the shin, a
    vambrace on the forearm, a pauldron, couter or poleyn on a half bone over its joint
    (posing.HALVES). Joints are covered by plates overlapping, as real armour is, not by bending.
    A plate is built from cross-sections of what it is worn over (the body part, and the garments
    in `over`) across its bone at `rings` places from `t` = (start, end), fractions of the bone
    (below 0 is before its head): at each, the farthest reach in `sides` directions of everything
    from the ring before to the ring after
    (a convex outline: no body detail survives), plus `gap`; `arc` (degrees from the front: for
    a limb lying sideways, 90 is up; for a leg, 90 is outward) leaves an open plate (a pauldron
    over the top of the shoulder); `flare` widens its last ring; `notch` = (angle, depth) cuts its
    first edge back on one side, as real armour is cut where a joint folds (a greave behind the
    knee, a vambrace inside the elbow). Mirrored for both sides."""
    rig = bpy.data.objects[body["rig"]]
    posing.ensure_halves(rig)
    origin = bm.verts.layers.float_vector.get("origin")
    dl = bm.verts.layers.deform.active
    names = []
    for spec in garment["plates"]:
        single = spec["bone"] in rig.data.bones                               # the head: one, no sides
        for side in ((None,) if single else ("L", "R")):
            bone = rig.data.bones[spec["bone"] if single else spec["bone"] + "." + side]
            follow = spec.get("follow", spec["bone"])
            if not single and follow + "." + side in rig.data.bones:
                follow = follow + "." + side                                  # else "chest": no side
            parts = spec.get("part", "legs")
            pts = [v.co.copy() for r in ([parts] if isinstance(parts, str) else parts)
                   for v in bpy.data.objects[body[r]].data.vertices]
            for g in spec.get("over", []):
                ob = bpy.data.objects.get("wear_" + g + body["suffix"])
                if ob:
                    pts += [v.co.copy() for v in ob.data.vertices]
            sign = 1 if side != "L" else -1
            if not single:
                pts = [p for p in pts if p.x * sign > 0.02]                 # this side's
            head, tail = bone.head_local, bone.tail_local
            w = (tail - head).normalized()
            u = (Vector((0, 1, 0)) - w * w.y).normalized()                  # the front
            up = Vector((0, 0, 1)) - w * w.z
            v = up.normalized() if up.length > 0.5 else (Vector((sign, 0, 0)) - w * (w.x * sign)).normalized()
            n = spec.get("sides", 8)
            a0, a1 = spec.get("arc", (0, 360))
            full = a1 - a0 >= 360
            angles = [math.radians(a0 + (a1 - a0) * i / (n if full else n - 1)) for i in range(n)]
            dirs = [u * math.cos(a) + v * math.sin(a) for a in angles]
            t0, t1 = spec["t"]
            count = spec.get("rings", 3)
            length = (tail - head).length
            rings = []
            prev = None
            for k in range(count):
                t = t0 + (t1 - t0) * k / (count - 1)
                c = head + (tail - head) * t
                # everything up to the neighbouring rings: a straight panel between two rings
                # must clear what bulges between them (the calf), not only what is at each
                span = max(spec.get("band", 0.025), abs(t1 - t0) * length / (count - 1))
                near = [p for p in pts if abs((p - c).dot(w)) < span
                        and ((p - c) - w * (p - c).dot(w)).length < spec.get("reach", 0.14)]
                rs = [max(((p - c).dot(d) for p in near), default=None) for d in dirs]
                if None in rs:
                    rs = prev or [0.05] * n
                rs = [r + spec.get("gap", 0.010) for r in rs]
                if k == count - 1:
                    rs = [r * spec.get("flare", 1.0) for r in rs]
                if k == 0:
                    rs = [r * spec.get("flare_start", 1.0) for r in rs]
                rings.append((c, rs))
                prev = rs
            thick = spec.get("thick", 0.006)
            outer, inner = [], []
            for k, (c, rs) in enumerate(rings):
                o_ring, i_ring = [], []
                for i, (d, r) in enumerate(zip(dirs, rs)):
                    cc = c
                    if k == 0 and "notch" in spec:          # the top edge cut back on one side
                        at, depth = spec["notch"]
                        off = abs(((math.degrees(angles[i]) - at) + 180) % 360 - 180)
                        if off < 70:
                            cc = c + (tail - head) * depth * (1 - off / 70)
                    for out, lst in ((thick, o_ring), (0.0, i_ring)):
                        vtx = bm.verts.new(cc + d * (r + out))
                        vtx[origin] = vtx.co
                        if follow not in names:
                            names.append(follow)
                        vtx[dl][names.index(follow)] = 1.0
                        lst.append(vtx)
                outer.append(o_ring)
                inner.append(i_ring)
            m = n if full else n - 1
            mat = material_slot(garment, spec.get("material", garment["material"]))
            made = []
            for a, b in zip(range(count - 1), range(1, count)):
                for i in range(m):
                    j = (i + 1) % n
                    made.append(bm.faces.new((outer[a][i], outer[a][j], outer[b][j], outer[b][i])))
                    made.append(bm.faces.new((inner[a][j], inner[a][i], inner[b][i], inner[b][j])))
            ends = (0,) if spec.get("cap") else (0, count - 1)
            for ring in ends:                                               # the rims
                for i in range(m):
                    j = (i + 1) % n
                    made.append(bm.faces.new((outer[ring][i], outer[ring][j], inner[ring][j], inner[ring][i])))
            if spec.get("cap"):                                             # closed over the end (a helm's crown)
                c_last = rings[-1][0]
                tops = []
                for lift, ring in ((spec["cap"] + thick, outer[-1]), (spec["cap"], inner[-1])):
                    apex = bm.verts.new(c_last + w * lift)
                    apex[origin] = apex.co
                    apex[dl][names.index(follow)] = 1.0
                    tops.append((apex, ring))
                (ao, ro), (ai, ri) = tops
                for i in range(n):
                    j = (i + 1) % n
                    made.append(bm.faces.new((ro[i], ro[j], ao)))
                    made.append(bm.faces.new((ri[j], ri[i], ai)))
            for f in made:
                f.material_index = mat
            if not full:                                                    # an open plate's edges
                for i in (0, n - 1):
                    for a, b in zip(range(count - 1), range(1, count)):
                        bm.faces.new((outer[a][i], outer[b][i], inner[b][i], inner[a][i]))
    return names


# ---------------------------------------------------------------------------------------------
# Hull pieces (pauldrons, helms, knee cops).

def posed_points(body, pose, names):
    """The points of these objects (armature space) and the bones' rest-to-posed matrices with the
    body in a pose, from throwaway copies (nothing of the scene is changed)."""
    rig = bpy.data.objects[body["rig"]]
    tmp = rig.copy()
    tmp.data = rig.data.copy()
    tmp.data.pose_position = "POSE"
    bpy.context.scene.collection.objects.link(tmp)
    made, out = [], {}
    try:
        posing.set_pose(tmp, pose)
        for n in names:
            ob = bpy.data.objects[n]
            c = ob.copy()
            bpy.context.scene.collection.objects.link(c)
            made.append(c)
            c.parent = tmp
            c.matrix_parent_inverse = ob.matrix_parent_inverse.copy()
            for m in c.modifiers:
                if m.type == "ARMATURE":
                    m.object = tmp
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        for n, c in zip(names, made):
            ev = c.evaluated_get(dg)
            me = ev.to_mesh()
            out[n] = [v.co.copy() for v in me.vertices]
            ev.to_mesh_clear()
        mats = {pb.name: pb.matrix @ pb.bone.matrix_local.inverted() for pb in tmp.pose.bones}
    finally:
        for c in made:
            bpy.data.objects.remove(c)
        data = tmp.data
        bpy.data.objects.remove(tmp)
        bpy.data.armatures.remove(data)
    return out, mats


def hulls(bm, body, garment):
    """Solid armour pieces made where they are mostly seen, then bound to one bone: the points of
    what the piece sits on (the body part, the garments in `over`) round a joint, with the body in
    `pose` (a design pose: shoulders with the arms down, as games author armour in an A-pose, not
    the T-pose rest); those within `radius` of `at` (a bone's head plus `offset`) and on its side
    `keep` (a direction, and how far behind it they may be); pushed out from `at` by `gap` and
    stretched by `stretch` (x outward, y forward, z up); wrapped in a convex hull, its nearly flat
    faces merged (`facet` degrees). Then moved back to the rest skeleton through the inverse of
    `follow`'s pose transform and weighted to it alone. `zones`: faces facing a direction within
    a height range take another channel (a visor). Mirrored for both sides unless `bone` has
    none."""
    rig = bpy.data.objects[body["rig"]]
    posing.ensure_halves(rig)
    dl = bm.verts.layers.deform.active
    origin = bm.verts.layers.float_vector.get("origin")
    names = []
    for spec in garment["hulls"]:
        single = spec["bone"] in rig.data.bones
        sources = [body[r] for r in ([spec["part"]] if isinstance(spec["part"], str) else spec["part"])]
        sources += ["wear_" + g + body["suffix"] for g in spec.get("over", [])
                    if bpy.data.objects.get("wear_" + g + body["suffix"])]
        pts, mats = posed_points(body, spec.get("pose", {}), sources)
        for side in ((None,) if single else ("L", "R")):
            sign = -1 if side == "L" else 1
            bone = spec["bone"] if single else spec["bone"] + "." + side
            follow = spec.get("follow", spec["bone"])
            follow = follow if single or follow in rig.data.bones else follow + "." + side
            m = mats[bone]
            at = m @ (rig.data.bones[bone].head_local + Vector(spec.get("offset", (0, 0, 0))))
            kd = Vector(spec.get("keep", ((0, 0, 1), 1.0))[0])
            keep = Vector((kd.x * sign, kd.y, kd.z)).normalized()
            behind = spec.get("keep", ((0, 0, 1), 1.0))[1]
            sx, sy, sz = spec.get("stretch", (1, 1, 1))
            cloud = []
            for p in (q for n in sources for q in pts[n]):
                d = p - at
                if d.length > spec["radius"] or d.dot(keep) < -behind:
                    continue
                if not single and p.x * sign < 0.02:
                    continue
                if "outward" in spec and (p.x - at.x) * sign < -spec["outward"]:
                    continue                                    # not onto the chest
                q = d + (d.normalized() * spec.get("gap", 0.02) if d.length > 1e-6 else Vector())
                cloud.append(Vector((q.x * sx, q.y * sy, q.z * sz)) + at)
            hb = bmesh.new()
            vs = [hb.verts.new(p) for p in cloud]
            bmesh.ops.convex_hull(hb, input=vs)
            bmesh.ops.delete(hb, geom=[v for v in hb.verts if not v.link_faces], context="VERTS")
            bmesh.ops.dissolve_limit(hb, angle_limit=math.radians(spec.get("facet", 7)),
                                     verts=list(hb.verts), edges=list(hb.edges))
            hb.normal_update()
            back = mats[follow].inverted()
            if follow not in names:
                names.append(follow)
            new = {}
            for v in hb.verts:
                w = bm.verts.new(back @ v.co)
                w[origin] = w.co
                w[dl][names.index(follow)] = 1.0
                new[v] = w
            mat = material_slot(garment, garment["material"])
            for f in hb.faces:
                nf = bm.faces.new([new[v] for v in f.verts])
                nf.material_index = mat
                for z in spec.get("zones", []):
                    dz = Vector(z["facing"])
                    hz = (f.calc_center_median() - at).z
                    if f.normal.dot(dz) > z.get("cos", 0.6) and z["z"][0] <= hz <= z["z"][1]:
                        nf.material_index = material_slot(garment, z["material"])
            hb.free()
    return names


# ---------------------------------------------------------------------------------------------
# Boots.

def boot(bm, body, opts):
    """A plain low-poly boot round each foot, L-shaped: a short shaft round the ankle (sealed to
    the ankle ring), the instep sloping down from the shaft's front to a toe box, the heel flush
    under the shaft, a welt and a flat sole. Its six corners follow the ankle ring's (two front,
    two outer side, two back); its size comes from the foot's extent."""
    legs = bpy.data.objects[body["legs"]].data
    feet = bpy.data.objects[body["feet"]]
    fme = feet.data
    origin = bm.verts.layers.float_vector.get("origin")
    dl = bm.verts.layers.deform.active
    names = []                                           # deform index -> group name
    fidx = {g.index: g.name for g in feet.vertex_groups}
    src = [(v.co.copy(), [(fidx[g.group], g.weight) for g in v.groups]) for v in fme.vertices]

    lidx = {g.index: g.name for g in bpy.data.objects[body["legs"]].vertex_groups}
    shin_src = [(v.co.copy(), [(lidx[g.group], g.weight) for g in v.groups]) for v in legs.vertices]
    ankle_z = min(v.co.z for v in legs.vertices)

    def weigh(v, p):
        if p.z > ankle_z + 0.03:                                    # a shaft is stiff: the shin alone
            name = "shin." + ("R" if p.x > 0 else "L")
            if name not in names:
                names.append(name)
            v[dl][names.index(name)] = 1.0
            return
        for name, w in min(src, key=lambda s: (s[0] - p).length)[1]:
            if name not in names:
                names.append(name)
            v[dl][names.index(name)] = w
    bmv = bmesh.new()
    bmv.from_mesh(legs)
    ankles = [[v.co.copy() for v in loop] for loop in rim_loops(bmv) if max(v.co.z for v in loop) < 0.3]
    bmv.free()
    for ring in ankles:
        c = sum(ring, Vector()) / len(ring)
        side = 1 if c.x > 0 else -1                      # +1: the outside is +x
        foot = [v.co for v in fme.vertices if v.co.x * side > 0]
        ground = min(p.z for p in foot)
        x_in = min(p.x * side for p in foot) * side      # the foot's inner and outer edge, heel, toe
        x_out = max(p.x * side for p in foot) * side
        heel = min(p.y for p in foot) - opts["margin"]
        toe = max(p.y for p in foot) + opts["margin"]
        m = opts["margin"]
        shaft = [c + (p - c) * opts["flare"] for p in ring]           # a little wider than the ankle
        role = []
        for p in ring:
            d = (p - c).normalized()
            role.append("front" if d.y > 0.5 else "back" if d.y < -0.5 else "side")

        def foot_xy(p, r):
            """Where this corner of the shaft goes at the sole: fronts to the toe, the side to the
            ball and heel, the back to the heel."""
            inner = (p.x - c.x) * side < 0
            if r == "front":
                x = (x_in + side * 0.012) if inner else (x_out - side * 0.010)
                return Vector((x, toe - (0.0 if inner else 0.012), 0))
            if r == "side":
                fore = p.y > c.y
                return Vector((x_out + side * m, (c.y + 0.065) if fore else (c.y - 0.005), 0))
            x = c.x + (p.x - c.x) * 1.1
            return Vector((x, heel, 0))
        sole_xy = [foot_xy(p, r) for p, r in zip(ring, role)]
        leg_tree = BVHTree.FromPolygons([v.co.copy() for v in legs.vertices], [list(p.vertices) for p in legs.polygons])
        if opts.get("shaft_to"):
            # A tall shaft: its top is the leg's own outline at that height, a little clear of it.
            top_z = height(body, opts["shaft_to"])
            band = [v.co for v in legs.vertices if v.co.x * side > 0 and abs(v.co.z - top_z) < 0.03]
            ct = sum(band, Vector()) / len(band)
            top = []
            for p in ring:
                d = (p - c).to_2d().normalized()
                r = max((q - ct).to_2d().dot(d) for q in band) + opts.get("shaft_gap", 0.012)
                top.append(Vector((ct.x + d.x * r, ct.y + d.y * r, top_z)))
            upper = [top]
            for k in range(1, opts.get("shaft_rings", 3) + 1):            # down the calf, following it
                zk = top_z + (c.z + 0.02 - top_z) * k / (opts.get("shaft_rings", 3) + 1)
                # the leg's outline at this height, cast from the leg's middle outward
                ck = Vector((c.x, c.y, zk)).lerp(Vector((ct.x, ct.y, zk)), (zk - c.z) / max(1e-6, top_z - c.z))
                ring_k = []
                for p in ring:
                    d = Vector(((p - c).x, (p - c).y, 0)).normalized()
                    hit = leg_tree.ray_cast(ck + d * 0.25, -d, 0.25)
                    r = ((hit[0] - ck).length if hit[0] is not None else (p - c).length) + opts.get("shaft_gap", 0.012)
                    ring_k.append(Vector((ck.x + d.x * r, ck.y + d.y * r, zk)))
                upper.append(ring_k)
            upper.append([Vector((p.x, p.y, c.z + 0.02)) for p in shaft])   # round the ankle
        else:
            top_z = c.z + opts["shaft_above"]
            upper = [[Vector((p.x, p.y, top_z)) for p in shaft]]
        rings = [ring] + upper + [                                          # the ankle (the seal), the shaft
            [Vector((p.x, p.y, ground + opts["instep"])) for p in shaft],   # where the instep starts
            [Vector((q.x, q.y, ground + (opts["toe"] if r == "front" else opts["heel"])))
             for q, r in zip(sole_xy, role)],                               # the toe box and heel
            [Vector((q.x, q.y, ground + opts["welt"])) for q in sole_xy],   # the welt
            [Vector((q.x, q.y, ground)) for q in sole_xy],                  # the sole
        ]
        made = []
        for pts in rings:
            rr = []
            for p in pts:
                v = bm.verts.new(p)
                v[origin] = p
                weigh(v, p)
                rr.append(v)
            made.append(rr)
        # the welt stands a little out from the upper
        for v, q in zip(made[-2], sole_xy):
            out = Vector((q.x - c.x, q.y - c.y, 0)).normalized()
            v.co += out * opts["welt_out"]
        for v, w in zip(made[-1], made[-2]):
            v.co.x, v.co.y = w.co.x, w.co.y
        n = len(ring)
        for a, b2 in zip(made, made[1:]):
            for i in range(n):
                bm.faces.new((a[i], a[(i + 1) % n], b2[(i + 1) % n], b2[i]))
        bm.faces.new(made[-1])                           # the sole
        bm.faces.new(made[0])                            # closed at the ankle, for when the leg is hidden
    return names


# ---------------------------------------------------------------------------------------------

def build(sex, name, garment):
    global free_set
    body = BODIES[sex]
    bm = bmesh.new()
    deform = bm.verts.layers.deform.verify()
    origin = bm.verts.layers.float_vector.new("origin")   # where on the body each vertex came from
    bm.verts.layers.int.new("free")                     # 1 on a hem that hangs free
    partl = bm.verts.layers.int.new("part")             # which part a vertex was copied from (1, 2...)
    garment.pop("_slots", None)
    GROUPS.clear()                                # vertex group names, by deform index (shared with drop)
    groups = GROUPS
    if "boot" in garment:
        groups.extend(boot(bm, body, garment["boot"]))
    elif "plates" in garment:
        groups.extend(plates(bm, body, garment))
    elif "hulls" in garment:
        groups.extend(hulls(bm, body, garment))
    else:
        for k, part in enumerate(garment["parts"]):
            ob = bpy.data.objects[body[part["role"]]]
            me = ob.data
            cos = shaped(ob, part, body)
            index = {g.index: g.name for g in ob.vertex_groups}
            made = {}
            for fi in faces_of(ob, part, body):
                vs = []
                for i in me.polygons[fi].vertices:
                    if i not in made:
                        v = bm.verts.new(cos[i])
                        v[origin] = me.vertices[i].co
                        v[partl] = k + 1
                        for g in me.vertices[i].groups:
                            gname = index[g.group]
                            if gname not in groups:
                                groups.append(gname)
                            v[deform][groups.index(gname)] = g.weight
                        made[i] = v
                    vs.append(made[i])
                try:
                    f = bm.faces.new(vs)
                    if part.get("material"):
                        f.material_index = material_slot(garment, part["material"])
                except ValueError:
                    pass
        for k, part in enumerate(garment["parts"]):      # each part cut on its own, before welding
            mine = [v for v in bm.verts if v[partl] == k + 1]
            geom = mine + list({e for v in mine for e in v.link_edges}) + list({f for v in mine for f in v.link_faces})
            if "drop" in part:                        # below the chest the drop builds the shirt:
                z = chest_ring(bpy.data.objects[body[part["role"]]], armpit_z(body))   # slice the body copy there
                bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1), clear_inner=True)
            if "below" in part:                       # only what shows below a skirt: cut, and closed on top
                z = height(body, part["below"])
                cut = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1), clear_outer=True)
                for v in cut["geom_cut"]:
                    if isinstance(v, bmesh.types.BMVert):
                        v[partl] = k + 1                      # new points on the cut are still this part's
                for loop in rim_loops(bm):
                    if all(v[partl] == k + 1 for v in loop) and all(abs(v.co.z - z) < 0.002 for v in loop):
                        f = bm.faces.new(loop)
                        f.material_index = material_slot(garment, part.get("material", garment["material"]))
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
        orient(bm)
        for v in bm.verts:
            v.co += v.normal * garment["thick"]
        for part in garment["parts"]:
            if "drop" in part:
                drop(bm, garment, body, part, garment["thick"])
        if garment.get("armhole"):
            cut_armholes(bm, body, garment["armhole"])
        finish_edges(bm, garment, origin, body)
        covered = [body[p["role"]] for p in garment["parts"]]
        trees = tree_of(set(covered + [body["arms"]]))
        if "drape" in garment:
            d = garment["drape"]
            pin = {"armpit": armpit_z(body) - 0.05, "crotch": crotch_z(body) + 0.02}[d["pin"]]
            free_set = {v for v in bm.verts if Vector(v[origin]).z < pin}
            drape(bm, d, trees, garment["thick"], pin)
        if not garment.get("over"):                # an over-garment's openings show what is under it
            seal(bm, origin)
        if garment.get("simplify"):
            simplify(bm, garment["simplify"])
    orient(bm)
    beneath = ["wear_" + g + body["suffix"] for g in garment.get("over", [])
               if bpy.data.objects.get("wear_" + g + body["suffix"])]
    if beneath:
        transfer_weights(bm, beneath)
    for under_name, reach in garment.get("follow", {}).items():   # close over another garment: move with it
        target = "wear_" + under_name + body["suffix"]
        if bpy.data.objects.get(target):
            transfer_weights(bm, [target], reach)
    inside = [body[r] for r in garment["hides"] + garment.get("contain", [])]
    inside += ["wear_" + g + body["suffix"] for g in garment.get("over", []) + garment.get("under", [])
               if bpy.data.objects.get("wear_" + g + body["suffix"])]   # what it is worn over
    contain(bm, inside)
    if garment.get("skirt"):
        skirt = skirt_of(bm, body, garment)
        skirt_weights(bm, body, skirt)
        if garment.get("lined"):
            line(bm, skirt, garment["lined"])
            orient(bm)

    obj_name = "wear_" + name + body["suffix"]
    parts = {v for b in BODIES.values() for k, v in b.items() if k not in ("suffix",)}
    if obj_name in parts:                         # never overwrite a body part (or a rig)
        raise ValueError(f"{obj_name} is a body part's name; give the garment another name")
    old = bpy.data.objects.get(obj_name)
    if old and old.parent and old.parent.name != body["rig"]:   # someone else's object of that name
        raise ValueError(f"{obj_name} already exists on {old.parent.name}; give the garment another name")
    # Rebuilt in place, so copies sharing the mesh (pose_review.py's posed copies) follow.
    me = old.data if old else bpy.data.meshes.new(obj_name)
    me.clear_geometry()
    me.materials.clear()
    bm.verts.index_update()
    dl = bm.verts.layers.deform.active
    weights = [(v.index, dict(v[dl].items())) for v in bm.verts]   # kept aside: groups are re-made below
    bm.to_mesh(me)
    bm.free()
    for slot in garment.get("_slots", [garment["material"]]):
        me.materials.append(bpy.data.materials[slot])
    me.use_mirror_x = True
    rig = bpy.data.objects[body["rig"]]
    if old:
        ob = old
        ob.vertex_groups.clear()
    else:
        ob = bpy.data.objects.new(obj_name, me)
        for c in rig.users_collection:
            c.objects.link(ob)
        ob.parent = rig
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    me.name = obj_name
    made = [ob.vertex_groups.new(name=gname) for gname in groups]   # the deform layer's indices, in order
    for i, ws in weights:                        # Blender 5 keeps weights with the groups: add them after
        for k, w in ws.items():
            made[k].add([i], w, "REPLACE")
    ob["hides"] = ",".join(garment["hides"])
    unweighted = sum(1 for v in me.vertices if not v.groups)
    out = {"object": ob.name, "verts": len(me.vertices), "faces": len(me.polygons),
           "poking": poking(body, garment, ob), "unweighted": unweighted}
    return out


def skirt_weights(bm, body, skirt):
    """A skirt hangs from the skirt chains (posing.py), not the legs: each vertex is shared between
    the two chains either side of it (by its angle round the body), the upper or lower segment by
    its height (blended across the knee), and at the very top blended back onto the hips."""
    rig = bpy.data.objects[body["rig"]]
    legs, torso = bpy.data.objects[body["legs"]], bpy.data.objects[body["torso"]]
    names = {g.index: g.name for g in legs.vertex_groups}
    leg_points = {n: [v.co.copy() for v in legs.data.vertices
                      if any(names[g.group] == n and g.weight >= 0.5 for g in v.groups)] for n in posing.LEG_BONES}
    top = height(body, "hips")
    posing.ensure_skirt(rig, [v.co.copy() for o in (legs, torso) for v in o.data.vertices], leg_points, top)
    knee_z = rig.data.bones["shin.L"].head_local.z
    dl = bm.verts.layers.deform.active
    n = posing.CHAINS
    for v in skirt:
        p = v.co
        k = (math.atan2(-p.x, p.y) % (2 * math.pi)) / (2 * math.pi / n)    # 0 at the front, round to the left
        k0, t = int(k) % n, k - int(k)
        s = max(0.0, min(1.0, (top - p.z) / 0.04))                         # onto the chains just below the hips
        lower = max(0.0, min(1.0, (knee_z + 0.04 - p.z) / 0.08))           # across the knee
        ws = {"hips": 1 - s}
        for kk, wk in ((k0, 1 - t), ((k0 + 1) % n, t)):
            n1, n2 = posing.chain_names(kk)
            ws[n1] = ws.get(n1, 0.0) + s * wk * (1 - lower)
            ws[n2] = ws.get(n2, 0.0) + s * wk * lower
        for key in list(v[dl].keys()):
            del v[dl][key]
        set_weights(bm, v, ws)


def skirt_of(bm, body, garment):
    """A skirt's vertices: below the hips, not the hose a garment carries under it."""
    partl = bm.verts.layers.int.get("part")
    hose = {k + 1 for k, p in enumerate(garment["parts"]) if "below" in p}
    hips = height(body, "hips")
    return [v for v in bm.verts if v.co.z < hips - 0.003 and v[partl] not in hose]


def line(bm, skirt, inset):
    """The skirt's inside: its faces again, `inset` within it and facing in, joined to it round the
    hem and closed across the top at the hips, so nothing shows through from below and there is
    no looking up into an empty body."""
    dl = bm.verts.layers.deform.active
    mine = set(skirt)
    top = {e.other_vert(v) for v in mine for e in v.link_edges} - mine      # the ring it hangs from
    faces = [f for f in bm.faces if any(v in mine for v in f.verts) and all(v in mine or v in top for v in f.verts)]
    bm.normal_update()
    twin = {}
    for f in faces:
        for v in f.verts:
            if v not in twin:
                t = bm.verts.new(v.co - v.normal * inset)
                for k, w in v[dl].items():
                    t[dl][k] = w
                twin[v] = t
    for f in faces:
        bm.faces.new([twin[v] for v in reversed(f.verts)]).material_index = f.material_index
    for e in [e for e in bm.edges if e.is_boundary and all(v in twin for v in e.verts)]:
        a, b = e.verts
        bm.faces.new((a, b, twin[b], twin[a]))                      # round the hem
    top = [lp for lp in rim_loops(bm) if all(v in twin.values() for v in lp)]
    for lp in top:
        if len(lp) > 2:
            bm.faces.new(lp)                                        # across the top, inside


def beside_opening(p, rims, within=0.01):
    """Whether a point lies within `within` of one of the garment's open edges."""
    for u, v in rims:
        d = v - u
        t = max(0.0, min(1.0, (p - u).dot(d) / max(1e-12, d.dot(d))))
        if (p - (u + d * t)).length < within:
            return True
    return False


def contain(bm, body_names, margin=0.002, rounds=12):
    """Any point of the hidden body parts still outside the garment pushes the face over it out
    until it is covered (vertices on the garment's edges, which meet the body, stay put)."""
    pts = [v.co.copy() for n in body_names for v in bpy.data.objects[n].data.vertices]
    start = {v: v.co.copy() for v in bm.verts}
    limit = 0.03                                  # no vertex moves further than this in all
    for _ in range(rounds):
        bm.normal_update()
        faces = list(bm.faces)
        t = BVHTree.FromBMesh(bm)
        rims = [(e.verts[0].co.copy(), e.verts[1].co.copy()) for e in bm.edges if e.is_boundary]
        moved = False
        for p in pts:
            loc, nrm, fi, _ = t.find_nearest(p)
            if loc is None:
                continue
            # A point whose nearest cloth is at an opening lies beyond it (a sleeve past a
            # sleeveless jerkin, a leg below a skirt's hem): it is not the garment's to cover.
            # "At" means within a centimetre of the opening's edge, not merely a face touching
            # it: faces touching an opening (a hem) can still lie over body that must be covered.
            if beside_opening(loc, rims):
                continue
            out = (p - loc).dot(nrm)
            if out > 0.0005:
                for v in faces[fi].verts:
                    step = nrm * (out + margin)
                    if (v.co + step - start[v]).length <= limit:
                        v.co += step
                        moved = True
        if not moved:
            break


def orient(bm):
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    centre = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    if sum(f.normal.dot(f.calc_center_median() - centre) for f in bm.faces) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
        bm.normal_update()


def poking(body, garment, ob):
    """Body vertices (of the parts the garment hides) that lie outside it."""
    me = ob.data
    t = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [list(p.vertices) for p in me.polygons])
    n = 0
    for role in garment["hides"]:
        part = bpy.data.objects[body[role]].data
        for v in part.vertices:
            loc, nrm, _, d = t.find_nearest(v.co)
            if loc is not None and (v.co - loc).dot(nrm) > 0.0005:
                n += 1
    return n


WEAR_RESULT = [build(sex, name, GARMENTS[name])
               for name in globals().get("WEAR", list(GARMENTS))
               for sex in globals().get("SEXES", list(BODIES))]
