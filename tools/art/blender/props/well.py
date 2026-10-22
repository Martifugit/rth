"""A village well: stone ring, water, two posts, a windlass, a small gabled roof, rope and bucket.
Footprint 2 x 2 tiles, centred on (1, 1). First test prop of the Blender art pipeline."""
import math
import os
import random
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rth_art as A  # noqa: E402

C = (1.0, 1.0)          # centre of the 2 x 2 footprint
R_OUT, R_IN = 0.75, 0.52
WALL_H = 0.70
POST_X = 0.62           # posts stand on the ring's top, either side
POST_W = 0.12
RIDGE_BEAM_H = 0.10      # the ridge beam the roof rests on, carried by the posts
AXLE_Z = 1.30
RIDGE_Z, EAVE_Z = 2.20, 1.78
ROOF_HALF_X, ROOF_HALF_Y = 0.92, 0.62
ROOF_T = 0.05

COLOURS = ["Stone", "Timber", "Planks", "Mere"]
STONE, TIMBER, PLANKS, WATER = range(4)


def build():
    A.reset()
    rnd = random.Random(7)
    objs = []

    # Stone ring: octagonal, outer and inner walls and a top, slightly irregular.
    bm = bmesh.new()
    sides = 8
    jit = {}

    def j(i, ring):
        key = (i, ring)
        if key not in jit:
            jit[key] = (rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), rnd.uniform(-0.04, 0.02) if ring else 0)
        return jit[key]

    (ob, ot), _ = A.prism(bm, sides, R_OUT, R_OUT * 0.97, 0, WALL_H, C, rot=math.pi / 8, mat_index=STONE,
                          cap_top=False, jitter=j)
    (ib, it), inner = A.prism(bm, sides, R_IN, R_IN, 0.30, WALL_H, C, rot=math.pi / 8, mat_index=STONE,
                              cap_top=False)
    for f in inner:
        f.normal_flip()
    for i in range(sides):
        k = (i + 1) % sides
        bm.faces.new((ot[i], ot[k], it[k], it[i])).material_index = STONE
    water = bm.faces.new(ib)
    water.material_index = WATER
    A.finish(bm)
    objs.append(A.mesh_from("Well_Ring", bm, COLOURS, smooth_angle=50))

    # Wooden frame: two posts, the windlass axle with a crank, rope and bucket.
    bm = bmesh.new()
    roof_under = RIDGE_Z - ROOF_T          # underside of the slabs at the ridge
    beam_bottom = roof_under - RIDGE_BEAM_H
    for sx in (-1, 1):
        x = C[0] + sx * POST_X
        A.box(bm, (x - POST_W / 2, C[1] - POST_W / 2, WALL_H - 0.02), (x + POST_W / 2, C[1] + POST_W / 2, beam_bottom),
              TIMBER, skip=("-z", "+z"))
    # Ridge beam on top of the posts, running the roof's length; the slabs rest on it.
    A.box(bm, (C[0] - ROOF_HALF_X + 0.06, C[1] - 0.05, beam_bottom), (C[0] + ROOF_HALF_X - 0.06, C[1] + 0.05, roof_under),
          TIMBER)
    # Axle: a hexagonal log lying along X between the posts.
    ax0, ax1 = C[0] - POST_X + POST_W / 2, C[0] + POST_X - POST_W / 2
    ra = 0.09
    ring0 = [bm.verts.new((ax0, C[1] + ra * math.cos(a), AXLE_Z + ra * math.sin(a)))
             for a in [2 * math.pi * i / 6 for i in range(6)]]
    ring1 = [bm.verts.new((ax1, v.co.y, v.co.z)) for v in ring0]
    for i in range(6):
        k = (i + 1) % 6
        bm.faces.new((ring0[i], ring0[k], ring1[k], ring1[i])).material_index = TIMBER
    # Crank: an arm out through the right post and a handle.
    cx = C[0] + POST_X + POST_W / 2
    A.box(bm, (cx, C[1] - 0.03, AXLE_Z - 0.03), (cx + 0.08, C[1] + 0.03, AXLE_Z + 0.03), TIMBER, skip=("-x",))
    A.box(bm, (cx + 0.08, C[1] - 0.03, AXLE_Z - 0.28), (cx + 0.14, C[1] + 0.03, AXLE_Z + 0.03), TIMBER)
    # Rope: a thin square strand from under the middle of the axle down to the bucket.
    bucket_top = 0.95
    A.box(bm, (C[0] - 0.015, C[1] - 0.015, bucket_top), (C[0] + 0.015, C[1] + 0.015, AXLE_Z - ra * 0.8), PLANKS,
          skip=("-z", "+z"))
    # Bucket: a tapered octagon hanging centred in the ring.
    A.prism(bm, 8, 0.12, 0.15, bucket_top - 0.26, bucket_top, C, mat_index=PLANKS,
            cap_top=True, cap_bottom=True)
    A.finish(bm)
    objs.append(A.mesh_from("Well_Frame", bm, COLOURS))

    # Roof: two thin plank slabs meeting at a ridge along X, overhanging the posts.
    bm = bmesh.new()
    t = ROOF_T
    for sy in (-1, 1):
        ridge = (C[1], RIDGE_Z)
        eave = (C[1] + sy * ROOF_HALF_Y, EAVE_Z)
        quad = []
        for x in (C[0] - ROOF_HALF_X, C[0] + ROOF_HALF_X):
            quad.append([bm.verts.new((x, ridge[0], ridge[1])), bm.verts.new((x, eave[0], eave[1])),
                         bm.verts.new((x, eave[0], eave[1] - t)), bm.verts.new((x, ridge[0], ridge[1] - t))])
        a, b = quad
        for f in ((a[0], a[1], b[1], b[0]), (a[3], b[3], b[2], a[2]), (a[1], a[2], b[2], b[1]),
                  (a[0], a[3], a[2], a[1]), (b[0], b[1], b[2], b[3])):
            bm.faces.new(f).material_index = PLANKS
    A.finish(bm)
    objs.append(A.mesh_from("Well_Roof", bm, COLOURS))
    return objs


objs = build()
cam = A.preview_rig(focus=(C[0], C[1], 0.9), distance=8.0, pitch=40, yaw=-30)
result = A.report(objs)
