"""Every part of Cruor's kit in one scene, at ordinary sizes, with the cathedral behind for scale:
a run of city wall with wall and corner towers and the gatehouse, the barracks, the chapter house
(red glass: it belongs to the faith), the armoury with corner turrets, a palace tower with a
belfry, and a short street of houses, most a single storey. A sample, not a layout: the city's
layout and its building budget are in docs/regions/cruor.md.

    runpy.run_path(".../buildings/cruor/showcase.py", run_name="__main__")

Run textures.py first after changing the textures."""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", ".."))
import rth_art as A  # noqa: E402
import kit as K  # noqa: E402
from kit import GLASS, FLOOR, piece, city_wall, gatehouse, tower, hall, townhouse  # noqa: E402
from cathedral import cathedral, keep  # noqa: E402


def street(x0, y_north, y_south, houses):
    """Two rows of houses facing each other across a street running along x: houses =
    [(w, d, kwargs), ...], alternating north and south. y_north and y_south are the two rows'
    front lines. A house faces -y as made, so the north row stands as made and the south row
    is turned round."""
    objs = []
    xn = xs = x0
    for i, (w, d, kw) in enumerate(houses):
        o = piece(f"House{i}", townhouse, w, d, **kw)
        if i % 2 == 0:
            o.location = (xn, y_north, 0)
            xn += w
        else:
            o.rotation_euler.z = math.pi
            o.location = (xs + w, y_south, 0)
            xs += w
        objs.append(o)
    b = K.Build()                                                 # the street's paving
    b.box(x0 - 1, y_south, 0, max(xn, xs) + 1, y_north, 0.03, FLOOR)
    A.finish(b.bm)
    objs.append(A.mesh_from("Street", b.bm, K.MATS, coll="Cruor"))
    return objs


def build():
    A.reset()
    objs = keep(cathedral(0, 0))
    yw = -45.0
    objs.append(piece("CityWall_W", city_wall, (-48, yw), (-3.5, yw), out=-1))
    objs.append(piece("CityWall_E", city_wall, (3.5, yw), (48, yw), out=-1))
    objs.append(piece("Gatehouse", gatehouse, 0.0, yw))
    for i, x in enumerate((-26, 26)):
        objs.append(piece(f"WallTower{i}", tower, x, yw, 3.4, 9.5))
    for i, x in enumerate((-50, 50)):
        objs.append(piece(f"CornerTower{i}", tower, x, yw, 3.6, 11.0, top="spire", spire_h=7.0))
    objs.append(piece("Barracks", hall, -44, -38, 16, 10, 7.0))
    objs.append(piece("ChapterHouse", hall, 18, -38, 12, 8, 8.0, bay=3.0, win_w=0.55, glass=GLASS, gable_window=1.1))
    objs.append(piece("Armoury", hall, -22, -38, 9, 8, 6.0, bay=3.0, corner_towers=True))
    objs.append(piece("PalaceTower", tower, 38, -31, 3.6, 13.0, top="belfry", spire_h=6.0))
    P, S = dict(plaster=True), dict(plaster=False)
    houses = [(6, 7, dict(S, storeys=1)), (5, 7, dict(P, storeys=2)), (7, 6, dict(P, storeys=1, gable_front=False)),
              (5, 7, dict(S, storeys=2)), (6, 7, dict(P, storeys=1)), (6, 6, dict(S, storeys=1, gable_front=False))]
    objs += street(-14, -55.0, -65.0, houses)
    K.paint(objs)
    return {"triangles": A.report(objs)}


if __name__ == "__main__":
    result = build()
    cam = A.preview_rig(focus=(0, -30, 6), distance=120, pitch=28, yaw=-25, ground=100, human_at=(-2, -52))
    cam.data.clip_end = 3000
    A.figures([(0, -45, 0), (-36, -40, 0), (24, -40, 0)])
