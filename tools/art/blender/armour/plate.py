"""Smithed plate, band 1 (tiers 1-3): the soldier's harness, after the owner's reference
(images/low-level-armor.jpg), followed piece by piece:

  helm    a round skull with a comb, coming down to a point between the eyes; a brim from the
          temples back, turned down, widest behind; the face open
  body    a breastplate and a backplate (front and back only: the mail shows at the sides),
          neckline to the ribs, bound by a thin belt; a strap down the right of the chest and an
          H of straps on the back; a big shell on each shoulder; a mail shirt with short sleeves,
          to the hips; a wide belt with an oval buckle and a pouch behind; one plate hanging
          over the left hip; bare arms
  legs    dark trousers; plates on both thighs, knee cops with a wing outside; a greave on the
          right shin only; straps round the backs of the legs
  hands   long leather bracers with a plate at the wrist; leather gloves
  feet    dark shoes with a plate over the instep; a cuff round the left ankle

Layers, from the skin out, each at its own distance so nothing merges: cloth, mail, plate,
straps. Plates are thin sheets (PLATE thick) laid a fixed gap over what they cover.

Materials: "Copper" is the plate, the only thing a tier recolours. "Mail" is neutral, "Saddle"
leather, "Umber" cloth and shoes, "skin" the player's own skin channel. "Mail" and "Saddle" are
not in the palette yet.

Rig space (armour/fit.py): the body faces +Y, its left is -X, Z up, the T-pose's arms along X.
On the trunk, angles go round from the front (0) towards the left (+90); the back is 180."""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import fit  # noqa: E402
import rth_art as A  # noqa: E402

MATS = ["Copper", "Mail", "Saddle", "Umber", "skin"]
METAL, MAIL, LEATHER, CLOTH, SKIN = range(5)
EXTRA = {"Mail": ("#5f6166", "mail"), "Saddle": ("#6b4a2f", None), "skin": ("#d6a680", None)}
MAIL_REPEAT = 6.0

# Cloth and mail sit this far out from the skin; every other layer is laid on the one under it.
CLOTH_D, MAIL_D = 0.006, 0.012
PLATE, STRAP = 0.007, 0.006            # thicknesses

YC = -0.045                            # the trunk's middle, front to back
HEAD = Vector((0, -0.028, 1.70))
UP, FWD = (0, 0, 1), (0, 1, 0)
TORSO = ("body_torso",)


def trunk_axis():
    """The trunk's vertical axis, so that t along it is the height z."""
    return Vector((0, YC, 0)), Vector((0, YC, 1))


def B(name, side):
    return fit.bone(f"{name}.{'R' if side > 0 else 'L'}")


def along(a, b, t):
    return Vector(a).lerp(Vector(b), t)


def steps(a, b, n):
    return [a + (b - a) * i / n for i in range(n + 1)]


def ring(n=12):
    return [360 * i / n for i in range(n)]


def sheet(bm, parts, a, b, angs, lo, hi, rows, offset, thick, mat, up, outside=False,
          closed=False, reach=0.35, onto=None):
    """A thin plate laid over the body: columns at angles `angs` (degrees, round the line a->b
    from `up`), each running from t=lo to t=hi along the line (numbers, or functions of the
    angle) in `rows` rows. Each point sits `offset` (a number, or a function of (v, angle) with
    v from 0 at lo to 1 at hi) out from the body's surface, measured along the ray from the line
    (or from outside, `outside`, for the outermost surface across a gap). The sheet has an inner
    face `thick` below and walls round its edges, so its edges show as edges. onto: lay it on
    this BVHTree (armour already made) instead of the body."""
    bvh = onto or fit.body_bvh(parts)
    a, b = Vector(a), Vector(b)
    _, u, s = fit.frame(a, b, up)
    outer, inner = [], []
    for ang in angs:
        rad = math.radians(ang)
        d = u * math.cos(rad) + s * math.sin(rad)
        t0 = lo(ang) if callable(lo) else lo
        t1 = hi(ang) if callable(hi) else hi
        centres, rs = [], []
        for i in range(rows):
            c = a.lerp(b, t0 + (t1 - t0) * i / (rows - 1))
            if outside:
                hit = bvh.ray_cast(c + d * reach, -d, reach)
                rs.append(reach - hit[3] if hit[0] is not None else None)
            else:
                hit = bvh.ray_cast(c, d, reach)
                rs.append(hit[3] if hit[0] is not None else None)
            centres.append(c)
        # A point whose ray found nothing (past the edge of what it lies on) takes the nearest
        # found distance in its column.
        found = [i for i, r in enumerate(rs) if r is not None]
        co, ci = [], []
        for i, c in enumerate(centres):
            v = i / (rows - 1)
            r = rs[i] if rs[i] is not None else (rs[min(found, key=lambda k: abs(k - i))] if found else 0.06)
            off = offset(v, ang) if callable(offset) else offset
            co.append(bm.verts.new(c + d * (r + off)))
            ci.append(bm.verts.new(c + d * (r + off - thick)))
        outer.append(co)
        inner.append(ci)
    n = len(angs)

    def face(vs):
        bm.faces.new(vs).material_index = mat
    for j in range(n if closed else n - 1):
        k = (j + 1) % n
        for i in range(rows - 1):
            face((outer[j][i], outer[k][i], outer[k][i + 1], outer[j][i + 1]))
            face((inner[j][i + 1], inner[k][i + 1], inner[k][i], inner[j][i]))
        for i in (0, rows - 1):                       # walls along the top and bottom edges
            face((outer[j][i], inner[j][i], inner[k][i], outer[k][i]))
    if not closed:                                     # and up the two side edges
        for j in (0, n - 1):
            for i in range(rows - 1):
                face((outer[j][i], outer[j][i + 1], inner[j][i + 1], inner[j][i]))
    return outer


def _egg(ang, front, side, back):
    rad = math.radians(ang)
    c, s = math.cos(rad), math.sin(rad)
    return 1 / math.sqrt((c / (front if c >= 0 else back)) ** 2 + (s / side) ** 2)


BREAST, BACK = 66, 60          # how far round the breastplate and backplate wrap (degrees)


def _lerp_table(x, table):
    for (x0, y0), (x1, y1) in zip(table, table[1:]):
        if x <= x1:
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0) if x1 > x0 else y1
    return table[-1][1]


def _from_front(ang):
    """0 in front, 180 behind, whichever way round."""
    return abs(((ang + 180) % 360) - 180)


# ---- helm ----

def helm():
    """After the reference: a fluted skull with a crest-like comb, a raised band round its foot,
    a small peak over the brow, and a brim sweeping steeply down past the cheeks, longest behind."""
    bm = bmesh.new()
    n = 16
    o = Vector((0, HEAD.y, 0))
    up = lambda z: Vector((0, 0, z))
    R = lambda ang: _egg(ang, 0.150, 0.130, 0.170) / math.cos(math.pi / n)
    edge_z = lambda f: _lerp_table(f, [(0, 1.742), (22.5, 1.79), (45, 1.775), (67.5, 1.748), (90, 1.735),
                                       (180, 1.728)])
    width = lambda f: _lerp_table(f, [(0, 0.002), (35, 0.002), (67.5, 0.034), (90, 0.048), (135, 0.056),
                                      (180, 0.064)])
    drop = lambda f: _lerp_table(f, [(0, 0.002), (35, 0.002), (67.5, 0.034), (90, 0.05), (135, 0.05),
                                     (180, 0.046)])
    crown = [(0.42, 1.948), (0.70, 1.930), (0.89, 1.895), (0.99, 1.85)]   # (share of R, height)
    top = bm.verts.new(o + up(1.952))
    cols = []
    for i in range(n):
        ang = 360 * i / n
        f = _from_front(ang)
        rad = math.radians(ang)
        d = Vector((-math.sin(rad), math.cos(rad), 0))
        r = R(ang)
        fl = 1.0 if i % 2 == 0 else 0.955          # flutes: ridge, groove, ridge... down the skull
        ez = edge_z(f)
        col = [o + d * (r * k * fl) + up(z) for k, z in crown]
        # The raised band round the skull's foot.
        col += [o + d * r + up(ez + 0.04), o + d * (r + 0.009) + up(ez + 0.036),
                o + d * (r + 0.009) + up(ez + 0.012)]
        # The edge (jutting a little at the peak over the brow), then the brim down and out,
        # and back underneath to the inside.
        edge = o + d * (r + 0.009 + (0.014 if f < 1 else 0)) + up(ez)
        w, dr = width(f), drop(f)
        mid = edge + d * (w * 0.5) + up(-dr * 0.4)
        rim = edge + d * w + up(-dr)
        col += [edge, mid, rim, rim + up(-PLATE) - d * 0.004, mid + up(-PLATE) - d * 0.004,
                edge + up(-PLATE) - d * (0.009 + PLATE)]
        cols.append([bm.verts.new(c) for c in col])
    for i in range(n):
        a, b = cols[i], cols[(i + 1) % n]
        bm.faces.new((top, a[0], b[0])).material_index = METAL
        for k in range(len(a) - 1):
            bm.faces.new((a[k], a[k + 1], b[k + 1], b[k])).material_index = METAL
    # The comb: a crest along the middle ridge, from the band in front, over the top, to the band
    # behind; lowest at its ends.
    front, back = cols[0], cols[n // 2]
    path = [front[4].co] + [front[k].co for k in (3, 2, 1, 0)] + [top.co] + [back[k].co for k in (0, 1, 2, 3)] + [back[4].co]
    centre = o + up(1.80)
    for j in range(len(path) - 1):
        seg = []
        for p, jj in ((path[j], j), (path[j + 1], j + 1)):
            h = 0.005 + 0.013 * math.sin(math.pi * jj / (len(path) - 1))
            nrm = (p - centre).normalized()
            for x in (-0.0055, 0.0055):
                for dh in (-0.006, h):
                    seg.append(p + Vector((x, 0, 0)) + nrm * dh)
        A.hull(bm, seg, METAL)
    return bm, None, ["hair"]


# ---- body ----

BOTTOM = 1.205                 # the breastplate's and backplate's lower edge


def plate_gap(ang, half, middle):
    """A trunk plate's outer face above the mail: close at its edges, a little more in its middle
    (its inner face always clears the mail)."""
    k = min(1.0, abs(_from_front(ang - middle)) / half)
    return PLATE + 0.005 + 0.007 * math.cos(k * math.pi / 2) ** 0.6


def on_surface(bvh, z, ang, axis_y=YC, reach=0.4):
    """Where a ray from outside, at height z and trunk angle ang, meets what is in bvh: (point,
    normal)."""
    rad = math.radians(ang)
    d = Vector((-math.sin(rad), math.cos(rad), 0))
    start = Vector((0, axis_y, z)) + d * reach
    hit = bvh.ray_cast(start, -d, reach)
    return hit[0], hit[1]


def body():
    bm = bmesh.new()
    a, b = trunk_axis()
    # Mail first, the layer everything else lies on: the shirt, short sleeves, a skirt to the hips.
    fit.inflate(bm, ["body_torso"], MAIL_D, MAIL)
    fit.inflate(bm, ["body_arms"], MAIL_D, MAIL, keep=lambda c: abs(c.x) < 0.40)
    sheet(bm, TORSO + ("body_legs",), a, b, ring(16), 0.93, 1.06, 3,
          lambda v, ang: MAIL_D + 0.004 + 0.022 * (1 - v) ** 2, 0.006, MAIL, FWD, outside=True, closed=True)
    mail = BVHTree.FromBMesh(bm)

    # The breastplate, laid on the mail: the neckline dips at the throat, rises to the
    # collarbones and falls to the armpits; it swells over the chest, with a low ridge, and
    # closes in to the mail at its edges.
    neck = lambda ang: _lerp_table(abs(ang), [(0, 1.507), (30, 1.515), (48, 1.49), (BREAST, 1.425)])
    breast = lambda v, ang: (plate_gap(ang, BREAST, 0)
                             + 0.014 * math.sin(math.pi * min(v, 0.85) / 0.85) * math.cos(math.radians(ang))
                             + 0.006 * max(0.0, 1 - abs(ang) / 25) + (0.004 if v == 1 else 0))
    sheet(bm, TORSO, a, b, steps(-BREAST, BREAST, 10), BOTTOM, neck, 6, breast, PLATE, METAL, FWD,
          onto=mail)
    back_top = lambda ang: 1.50 - 0.06 * (abs(ang - 180) / BACK) ** 1.6
    sheet(bm, TORSO, a, b, steps(180 - BACK, 180 + BACK, 10), BOTTOM, back_top, 5,
          lambda v, ang: plate_gap(ang, BACK, 180) + 0.008 * math.sin(math.pi * v), PLATE, METAL, FWD,
          onto=mail)

    # A rounded shell on each shoulder, on the mail sleeve: an oval outline, highest over the
    # shoulder's point, hugging the arm at its front and back edges.
    def shell(side):
        sh, el = B("upper_arm", side), B("forearm", side)
        top = lambda v, ang: (PLATE + 0.005 + 0.03 * math.sin(math.pi * (0.2 + 0.8 * v))
                              * math.cos(math.radians(ang) * 0.92) + (0.005 if v == 1 else 0))
        lo = lambda ang: -0.06 + 0.12 * (abs(ang) / 88) ** 2
        hi = lambda ang: 0.6 - 0.14 * (abs(ang) / 88) ** 2
        sheet(bm, (), sh, el, steps(-88, 88, 8), lo, hi, 5, top, PLATE, METAL, UP, reach=0.3, onto=mail)
    shell(1)
    shell(-1)

    # One plate over the left hip, hung from the waist belt (made next, over its top): a shield,
    # curved round the hip, pointed below.
    tip, half = 38, 24
    lo = lambda ang: 0.82 + 0.10 * (abs(ang - tip) / half) ** 1.5
    sheet(bm, TORSO + ("body_legs",), a, b, steps(tip - half, tip + half, 6), lo, 1.115, 5,
          lambda v, ang: PLATE + 0.012 + 0.008 * math.sin(math.pi * v) * math.cos(math.radians(ang - tip) * 3),
          PLATE, METAL, FWD, outside=True, onto=mail)

    # The waist belt, a little lower in front than behind, over the mail and the hip plate's top;
    # its oval buckle right of the middle, a pouch behind.
    under = BVHTree.FromBMesh(bm)
    belt_lo = lambda ang: 1.075 + 0.02 * _from_front(ang) / 180
    sheet(bm, (), a, b, ring(24), belt_lo, lambda ang: belt_lo(ang) + 0.055, 2, 0.012, 0.008, LEATHER, FWD,
          outside=True, closed=True, onto=under)
    belt = BVHTree.FromBMesh(bm)
    p, nrm = on_surface(belt, 1.103, -14)
    side = nrm.cross(Vector((0, 0, 1))).normalized()
    A.hull(bm, [p + nrm * dn + side * (0.042 * math.cos(t)) + Vector((0, 0, 0.026 * math.sin(t)))
                for t in [i * math.pi / 4 for i in range(8)] for dn in (0.0, 0.01)], METAL)
    p, _ = on_surface(belt, 1.11, 180)
    A.box(bm, (-0.08, p.y - 0.035, 1.065), (0.08, p.y + 0.003, 1.135), LEATHER)

    # Leather laid on the plates (and the mail between them): the thin belt round the plates'
    # lower edge and its buckle, a strap down the right of the chest, the H on the back.
    on_top = STRAP + 0.004
    under = BVHTree.FromBMesh(bm)
    sheet(bm, (), a, b, ring(32), BOTTOM - 0.01, BOTTOM + 0.02, 2, on_top, STRAP, LEATHER, FWD,
          outside=True, closed=True, onto=under)
    for mid in (-30, 180 - 32, 180 + 32):
        sheet(bm, (), a, b, [mid - 6, mid + 6], BOTTOM + 0.02, 1.47, 8, on_top, STRAP, LEATHER, FWD,
              outside=True, onto=under)
    sheet(bm, (), a, b, steps(180 - 38, 180 + 38, 8), 1.40, 1.43, 2, on_top, STRAP, LEATHER, FWD,
          outside=True, onto=BVHTree.FromBMesh(bm))
    p, nrm = on_surface(BVHTree.FromBMesh(bm), BOTTOM + 0.005, -16)
    side = nrm.cross(Vector((0, 0, 1))).normalized()
    A.hull(bm, [p + nrm * dn + side * dx + Vector((0, 0, dz)) for dn in (0.0, 0.009)
                for dx in (-0.013, 0.013) for dz in (-0.019, 0.019)], METAL)

    # Laid on last: the straps over the shoulders, and the bare arms (the player's own skin).
    over = bmesh.new()
    onto = BVHTree.FromBMesh(bm)
    for x in (0.10, -0.10):
        angs = [math.radians(v) for v in steps(38, 132, 10)]
        pts = [(x, YC + 0.3 * math.cos(t), 1.30 + 0.3 * math.sin(t)) for t in angs]
        fit.ribbon(over, pts, [(0, math.cos(t), math.sin(t)) for t in angs], 0.034, LEATHER, onto,
                   lift=0.008, across=(1, 0, 0))
    fit.inflate(over, ["body_arms"], 0.0005, SKIN)
    return bm, over, ["torso", "arms"]


# ---- legs ----

def legs():
    bm = bmesh.new()
    fit.inflate(bm, ["body_legs"], CLOTH_D, CLOTH)
    cloth = BVHTree.FromBMesh(bm)
    on = dict(onto=cloth, reach=0.15)

    def leg(side):
        hip, knee, ankle = B("thigh", side), B("shin", side), B("foot", side)
        out = 1 if side > 0 else -1           # the outside of this leg, as a sheet angle's sign
        # The thigh plate, on the front and outside of the thigh.
        plate = sorted([-20 * out, 75 * out])
        sheet(bm, (), hip, knee, steps(plate[0], plate[1], 6), 0.3, 0.84, 5,
              lambda v, ang: PLATE + 0.005 + 0.004 * math.sin(math.pi * v), PLATE, METAL, FWD, **on)
        # Straps round the rest of the thigh, their ends under the plate's edges.
        for t in (0.36, 0.7):
            sheet(bm, (), hip, knee, steps(plate[1] - 6, plate[0] + 366, 10), t - 0.03, t + 0.03, 2,
                  STRAP + 0.003, STRAP, LEATHER, FWD, **on)
        # The knee cop, close over the knee, and a small wing on the outside.
        sheet(bm, (), along(knee, hip, 0.14), along(knee, ankle, 0.12), steps(-85, 85, 6), 0.0, 1.0, 5,
              lambda v, ang: PLATE + 0.006 + 0.012 * math.sin(math.pi * v) * math.cos(math.radians(ang) * 0.8),
              PLATE, METAL, FWD, **on)
        sheet(bm, (), along(knee, hip, 0.08), along(knee, ankle, 0.08), sorted([80 * out, 100 * out, 120 * out]),
              0.0, 1.0, 3, lambda v, ang: PLATE + 0.006 + 0.016 * math.sin(math.pi * v), PLATE, METAL, FWD, **on)
        # The right shin's greave, strapped round the calf; the left shin is bare cloth.
        if side > 0:
            sheet(bm, (), knee, ankle, steps(-85, 85, 6), 0.14, 0.97, 6,
                  lambda v, ang: PLATE + 0.005 + 0.004 * v, PLATE, METAL, FWD, **on)
            for t in (0.45, 0.72):
                sheet(bm, (), knee, ankle, steps(80, 280, 8), t - 0.03, t + 0.03, 2, STRAP + 0.003, STRAP,
                      LEATHER, FWD, **on)
    leg(1)
    leg(-1)
    return bm, None, ["legs"]


# ---- hands and feet ----

def hands():
    bm = bmesh.new()
    fit.inflate(bm, ["body_hands"], 0.006, LEATHER)

    def bracer(side):
        el, wr = B("forearm", side), B("hand", side)
        sheet(bm, ("body_arms",), el, wr, ring(10), 0.22, 0.97, 3,
              lambda v, ang: 0.012 + (0.006 if v == 0 else 0), STRAP, LEATHER, UP, closed=True, reach=0.15)
        sheet(bm, ("body_arms",), el, wr, steps(-55, 55, 4), 0.62, 0.99, 3, 0.022, PLATE, METAL, UP,
              reach=0.15)
    bracer(1)
    bracer(-1)
    return bm, None, ["hands"]


def feet():
    bm = bmesh.new()
    fit.inflate(bm, ["body_feet"], 0.008, CLOTH)
    shoe = BVHTree.FromBMesh(bm)

    def foot(side):
        f, toe = B("foot", side), B("toe", side)
        sheet(bm, (), f, toe, steps(-65, 65, 4), -0.2, 0.75, 3, PLATE + 0.004, PLATE, METAL, UP,
              reach=0.15, onto=shoe)
        if side < 0:                          # the left ankle's cuff, over the trouser leg
            knee, ankle = B("shin", side), B("foot", side)
            sheet(bm, ("body_legs",), knee, ankle, ring(10), 0.84, 0.95, 2, CLOTH_D + PLATE + 0.006, PLATE,
                  METAL, FWD, closed=True, reach=0.15)
    foot(1)
    foot(-1)
    return bm, None, ["feet"]


PIECES = [("helm", helm), ("body", body), ("legs", legs), ("hands", hands), ("feet", feet)]


# ---- materials, building and the look ----

def mail_image():
    """Rings 8 pixels across in rows 5 apart, every other row shifted half a ring."""
    img = bpy.data.images.get("MailPattern")
    if img:
        return img
    n, w, step = 40, 8, 5
    img = bpy.data.images.new("MailPattern", n, n)
    grid = [[0.5] * n for _ in range(n)]
    for row in range(n // step + 1):
        shift = (w // 2) * (row % 2)
        cy = row * step
        for k in range(n // w + 1):
            cx = k * w + shift + w / 2 - 0.5
            for y in range(cy - 4, cy + 5):
                for x in range(int(cx) - 5, int(cx) + 6):
                    d = math.hypot(x - cx, (y - cy) * 1.15)
                    if 2.2 <= d <= 3.9:
                        grid[y % n][x % n] = 1.0 if y < cy else 0.8 if y == cy else 0.68
                    elif d < 2.2:
                        grid[y % n][x % n] = 0.5
    px = []
    for y in range(n):
        for x in range(n):
            g = grid[n - 1 - y][x]
            px += [g, g, g, 1.0]
    img.pixels = px
    return img


def material(name):
    if name not in EXTRA:
        return A.mat(name)
    hexs, pattern = EXTRA[name]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    rgb = [A._srgb_to_linear(int(hexs[i:i + 2], 16) / 255) for i in (1, 3, 5)] + [1.0]
    bsdf.inputs["Base Color"].default_value = rgb
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Specular IOR Level"].default_value = 0.0
    m.diffuse_color = rgb
    if pattern == "mail" and not nodes.get("Surface"):
        tex = nodes.new("ShaderNodeTexImage")
        tex.name = "Surface"
        tex.image = mail_image()
        tex.interpolation = "Closest"
        mix = nodes.new("ShaderNodeMix")
        mix.name = "Tint"
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        links.new(tex.outputs["Color"], mix.inputs[6])
        links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    if nodes.get("Tint"):
        nodes["Tint"].inputs[7].default_value = rgb
    return m


def mesh(name, bm, over, coll):
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
    A.finish(bm)
    if over is not None:
        tmp = bpy.data.meshes.new("_over")
        over.to_mesh(tmp)
        over.free()
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for n in MATS:
        me.materials.append(material(n))
    A.box_uvs(me)
    uv = me.uv_layers[0]
    for p in me.polygons:
        if p.material_index == MAIL:
            for li in p.loop_indices:
                uv.data[li].uv = uv.data[li].uv * MAIL_REPEAT
    ob = bpy.data.objects.new(name, me)
    fit.coll(coll).objects.link(ob)
    return ob


def figure(name, x, yaw=0.0):
    """A posed copy of the player at (x, 0), turned by yaw degrees; returns its rig."""
    base = bpy.data.objects["ArmourRig"]
    c = fit.coll("ArmourLineup")
    rig = base.copy()
    rig.name = "Fig_" + name
    c.objects.link(rig)
    rig.location = (x, 0, 0)
    rig.rotation_euler = (0, 0, math.radians(yaw))
    for p in base.children:
        q = p.copy()
        q.name = "Fig_" + name + "_" + p.name.replace("Armour_", "")
        c.objects.link(q)
        q.parent = rig
        q.hide_render = False
        for md in q.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
    fit.pose(rig)
    return rig


def dress(rig, pieces=PIECES):
    report = {}
    for piece, make in pieces:
        bm, over, hides = make()
        ob = mesh(f"{rig.name}_{piece}", bm, over, "ArmourLineup")
        fit.bind(ob, rig)
        ob["hides"] = ",".join(hides)
        report[piece] = A.report([ob])["triangles"]
        for part in hides:
            for ch in rig.children:
                if ch.name.endswith("body_" + part) or (part == "hair" and "_hair_" in ch.name):
                    ch.hide_render = True
                    ch.hide_set(True)
    return report


def build():
    fit.scene()
    base = fit.body()
    fit.clear("ArmourLineup")
    out = {}
    # As the reference stands: the front on the left, the back on the right.
    for name, x, yaw in (("front", 0.45, 0.0), ("back", -0.45, 180.0)):
        out[name] = dress(figure(name, x, yaw))
    base.hide_set(True)
    base.hide_render = True
    for ch in base.children:
        ch.hide_set(True)
        ch.hide_render = True
    return out["front"]


if __name__ == "__main__" or "result" not in globals():
    result = build()
