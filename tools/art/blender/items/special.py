"""Special weapons: one-off pieces designed on their own, outside the smithed tiers.

Sealbreaker (a concept): a two-handed maul made of the Keystone, the guardian bound into one of
the deep's seals. Its head is a keystone of seal-stone split through the middle, the seal's
glowing core showing in the split, held together by two clasps of chain laid flat against it; the
far end bristles with four spikes. Three pieces drop separately (the upper half of the head, the
lower half, the chain clasps); the shaft is smithed, and the four are assembled.

Same held-art convention as held.py: origin at the grip, the handle along Blender -Y, the
business side (the striking face) on +Z."""
import math
import os
import sys

import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

EXPORT = True
STONE, IRON, GLOW, GRIP = range(4)
COLOURS = ["Slate", "Blackiron", "Lye", "Umber"]

HEAD_AT = 1.05                   # along the haft, the middle of the head (and of its split)
HALF_LEN, HALF_HEIGHT, HALF_THICK = 0.12, 0.26, 0.11
FLARE = 0.075                    # the keystone: the striking face is this much longer than the back
BACK = -HALF_HEIGHT * 0.45       # the back of the head (the spikes stand on it)
GAP = 0.022                      # the split between the halves
CLASPS_AT = (-0.02, 0.15)        # heights of the two clasps


def F(along, up=0.0, side=0.0):
    return (side, -along, up)


def half_len(u):
    """Half the head's length along the haft at height u: it widens toward the striking face."""
    t = (u - BACK) / (HALF_HEIGHT - BACK)
    return HALF_LEN - FLARE * 0.4 + FLARE * 1.4 * t


# ---- the head ----

def head_half(bm, which):
    """One half of the split keystone: which = +1 the upper half (away from the hands), -1 the
    lower. Its face in the split glows (the seal's core)."""
    hs = HALF_THICK
    pts = []
    for u in (BACK, HALF_HEIGHT):
        for a_off in (half_len(u), GAP / 2):
            a = HEAD_AT + which * a_off
            for s in (-hs * 0.8, hs * 0.8):
                pts.append(F(a, u, s))
            for s in (-hs, hs):
                pts.append(F(a, u * 0.93 if u > 0 else u, s))
        if u > 0:
            for a_off in (half_len(u) * 0.8, GAP / 2):
                for s in (-hs * 0.7, hs * 0.7):
                    pts.append(F(HEAD_AT + which * a_off, u * 1.04, s))
    A.hull(bm, pts, STONE)
    # The core: a thin glowing slab on the split face.
    a0 = HEAD_AT + which * (GAP / 2 - 0.004)
    A.hull(bm, [F(a, u, s) for a in (a0, a0 + which * 0.006) for u in (BACK + 0.01, HALF_HEIGHT * 0.95)
                for s in (-hs * 0.85, hs * 0.85)], GLOW)
    # Two spikes on the back, at this half's corners.
    a_corner = HEAD_AT + which * half_len(BACK) * 0.78
    for s in (-1, 1):
        base = [F(a_corner + x, BACK + 0.04, s * hs * 0.72 + z) for x in (-0.03, 0.03) for z in (-0.03, 0.03)]
        tip = F(a_corner + which * 0.05, BACK - 0.14, s * (hs * 0.72 + 0.05))
        A.hull(bm, base + [tip], STONE)


def head_marks(bm, stone):
    """Glowing cracks on the broad faces, cast onto the stone only."""
    for side in (-1, 1):
        toward = (side, 0, 0)
        for crack in ([(HEAD_AT + 0.03, 0.05), (HEAD_AT + 0.08, 0.0), (HEAD_AT + 0.06, -0.05),
                       (HEAD_AT + 0.10, -0.09)],
                      [(HEAD_AT - 0.03, 0.04), (HEAD_AT - 0.08, 0.09), (HEAD_AT - 0.06, 0.13),
                       (HEAD_AT - 0.11, 0.20)]):
            for (a0, u0), (a1, u1) in zip(crack, crack[1:]):
                w0, w1 = 0.010, 0.006
                A.decal(bm, [F(a0 - w0, u0, side * 0.4), F(a0 + w0, u0, side * 0.4),
                             F(a1 + w1, u1, side * 0.4), F(a1 - w1, u1, side * 0.4)], toward,
                        mat=GLOW, onto=stone)


# ---- the clasps ----

def link(bm, stone, centre, along_dir, out_dir, length, width, face_on, mat=IRON):
    """One chain link lying flat on the stone: cast onto the surface at `centre` from `out_dir`.
    face_on: a flat plate with a slot showing the stone through it; else a narrow bar on edge.
    The link follows the surface's slant there."""
    out_dir = Vector(out_dir).normalized()
    hit, normal, _, _ = stone.ray_cast(Vector(centre) + out_dir * 0.4, -out_dir)
    if hit is None:
        return
    t = Vector(along_dir)
    t = (t - normal * t.dot(normal)).normalized()
    b = normal.cross(t).normalized()
    w = width if face_on else width * 0.35
    h = 0.010 if face_on else 0.016
    pts = []
    for dl in (-length / 2, length / 2):
        for dw in (-w / 2, w / 2):
            for dh in (-0.004, h):
                k = 0.8 if (dh > 0 and face_on) else 1.0          # a rounded-off top
                pts.append(hit + t * dl * k + b * dw * k + normal * dh)
    A.hull(bm, pts, mat)
    if face_on:                                                     # the slot: stone showing
        top = hit + normal * (h + 0.0015)
        sl, sw = length * 0.25, width * 0.12
        f = bm.faces.new([bm.verts.new(top + t * x + b * y)
                          for x, y in ((-sl, -sw), (sl, -sw), (sl, sw), (-sl, sw))])
        f.normal_update()
        if f.normal.dot(normal) < 0:
            f.normal_flip()
        f.material_index = STONE


def clasps(bm, stone):
    """Two clasps round the head, each an unbroken loop of flat chain links: links are spaced
    evenly round the head's outline (across the broad faces, round the corners, over the slanted
    top and bottom of the keystone), each cast onto the stone from its outward direction."""
    hs = HALF_THICK
    link_len = 0.052
    for u in CLASPS_AT:
        hl = half_len(u) + 0.004
        # The outline at this height, as corner points in (along, side), going round.
        corners = [(-hl, -hs), (hl, -hs), (hl, hs), (-hl, hs)]
        edges = [(corners[i], corners[(i + 1) % 4]) for i in range(4)]
        lengths = [math.dist(a, b) for a, b in edges]
        total = sum(lengths)
        n = max(4, round(total / link_len))
        if n % 2:
            n += 1                                                  # plates and bars alternate all round
        for i in range(n):
            d = total * (i + 0.5) / n
            for (a, b), length in zip(edges, lengths):
                if d <= length:
                    break
                d -= length
            t = d / length
            pa, ps = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
            ta, ts = (b[0] - a[0]) / length, (b[1] - a[1]) / length
            # Outward: the edge's normal, turning round the corner near its ends.
            na, ns = ts, -ta
            if na * pa + ns * ps < 0:
                na, ns = -na, -ns
            near_end = min(d, length - d)
            if near_end < link_len * 0.6:
                ca, cs = (a if d < length / 2 else b)
                k = 1 - near_end / (link_len * 0.6)
                na, ns = na + (ca / hl) * 0.7 * k, ns + (cs / hs) * 0.7 * k
            link(bm, stone, F(HEAD_AT + pa, u, ps), F(ta, 0, ts), F(na, 0, ns), link_len * 1.12, 0.05,
                 face_on=i % 2 == 0)


# ---- the shaft (smithed) ----

def shaft(bm):
    """The smithed shaft: iron with three raised rings, a leather grip for two hands, a capped
    end. It stops inside the head."""
    A.loft(bm, [(F(-0.36), 0.0, 0.0), (F(-0.34), 0.034, 0.034), (F(-0.30), 0.034, 0.034)],
           sides=8, ref=(0, 0, 1), mats=IRON)
    A.loft(bm, [(F(-0.30), 0.024, 0.024), (F(0.16), 0.024, 0.024)], sides=8, ref=(0, 0, 1), mats=GRIP)
    A.loft(bm, [(F(0.16), 0.022, 0.022), (F(HEAD_AT + 0.06), 0.022, 0.022)], sides=6, ref=(0, 0, 1), mats=IRON)
    for a in (0.20, 0.42, 0.64):
        A.loft(bm, [(F(a - 0.025), 0.022, 0.022), (F(a), 0.036, 0.036), (F(a + 0.025), 0.022, 0.022)],
               sides=6, ref=(0, 0, 1), mats=IRON)


# ---- assembly ----

def stone_of(*halves):
    """The head's stone (no marks or clasps), for casting marks and links onto."""
    bm = bmesh.new()
    for which in halves:
        head_half(bm, which)
    A.finish(bm)
    return bm, BVHTree.FromBMesh(bm)


def make(name):
    bm = bmesh.new()
    if name in ("Sealbreaker", "SealUpperHalf", "SealLowerHalf"):
        halves = {"Sealbreaker": (1, -1), "SealUpperHalf": (1,), "SealLowerHalf": (-1,)}[name]
        stone_bm, stone = stone_of(*halves)
        for which in halves:
            head_half(bm, which)
        head_marks(bm, stone)
        if name == "Sealbreaker":
            clasps(bm, stone)
            shaft(bm)
        stone_bm.free()
    elif name == "SealClasps":
        stone_bm, stone = stone_of(1, -1)
        clasps(bm, stone)
        stone_bm.free()
    elif name == "SealbreakerShaft":
        shaft(bm)
    A.finish(bm)
    return A.mesh_from(name, bm, COLOURS, coll="Items")


PIECES = ["Sealbreaker", "SealUpperHalf", "SealLowerHalf", "SealClasps", "SealbreakerShaft"]


def build():
    A.reset()
    parts = {name: make(name) for name in PIECES}
    result = {name: A.report([o]) for name, o in parts.items()}
    if EXPORT:
        result["export"] = A.export([parts["Sealbreaker"]], "Resources/Items/Sealbreaker.fbx")
    # The preview, seen from the side: the player, the maul standing head up, then the pieces.
    for name, (y, z) in {"Sealbreaker": (0.0, 0.4), "SealUpperHalf": (1.0, -0.75), "SealLowerHalf": (1.5, -0.55),
                         "SealClasps": (2.1, -0.65), "SealbreakerShaft": (2.8, 0.4)}.items():
        o = parts[name]
        o.rotation_euler = (-math.pi / 2, 0, 0)
        o.location = (0.6, y, z)
    return parts, result


parts, result = build()
cam = A.preview_rig(focus=(0.6, 1.0, 0.95), distance=5.6, pitch=6, yaw=-90, human_at=(0.6, -0.8))
