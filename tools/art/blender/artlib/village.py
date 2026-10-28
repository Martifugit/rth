"""The village kit's shared sizes and colours (storey, doorway, pitch, wall thickness, the material
list every piece is painted from) and piece(), which makes a kit piece's object.

Every piece is one tile on the grid (Blender X along the wall, -Y outside, Z up; 1 unit = 1 tile)
and painted by palette name, so stone walls, slate roofs and red church glass are recolours of
the same pieces. boards/village.py puts buildings together from them and writes the kit's rules
(world/kits/venaria.json)."""

import math

import rth_art as A


STOREY = 2.6                    # fits the player (1.9) with room over his head
DOOR = 2.1                      # a doorway's clear height
PITCH = 42.0
RISE = math.tan(math.radians(PITCH))
T = 0.22                        # wall thickness, centred on the grid line

# The roof rests on the walls' outer edge (and the corner posts), not their centre line, so no
# rail or post pokes through the slope.
ROOF_BASE = STOREY + (T / 2 + 0.06) * RISE
MATS = ["Stone", "Plaster", "Timber", "Thatch", "Planks", "Iron", "Sable", "Glaze", "Blackiron",
        "Blood", "SynodRed", "Bronze", "Bark", "Ash", "Wheat", "CandleWax", "Oak"]
(STONE, PLASTER, TIMBER, THATCH, PLANKS, IRON, DARK, GLASS, LEAD, BLOOD, BANNER, BRONZE,
 BARK, LOG_END, HAY, CANDLE, OAK) = range(len(MATS))


def box(bm, x0, x1, y0, y1, z0, z1, mat):
    return A.box(bm, (x0, y0, z0), (x1, y1, z1), mat)


def piece(make):
    """A builder for a kit piece: make() returns its bmesh in the tile's frame; every piece shares
    the kit's materials (MATS), so a building recolours them by name."""
    def build():
        bm = make()
        A.finish(bm)
        return A.mesh_from("Kit_piece", bm, MATS, coll="Kit")
    return build
