#!/usr/bin/env python3
"""
Cruor's painted textures (docs/regions/cruor-architecture.md). One stone carries every building;
red is only the faith's glass.

  CruorStone     the one stone: small bricks, a quarter tile tall and half a tile long, dark
                 mortar outlines, light edges, strong contrast between bricks (after Varrock
                 castle's wall, docs/research/osrs-varrock-castle.md), in the pale warm grey the
                 owner chose (repeat 2 x 2 tiles)
  CruorSlate     dark slate in neat rows (2 x 1)
  CruorGlass     red leaded glass in diamond quarries, for buildings of the faith (1 x 1)
  CruorFloor     big dark flagstones (4 x 4)
  CruorPlaster   limewashed plaster for houses, one storey a repeat (1 x 2.3)

    python3 tools/art/blender/buildings/cruor/textures.py     (or runpy in Blender)

Writes PNGs into textures/ beside this script. The painters are in ../painting.py.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from painting import ashlar, glass, plaster_wall, roof_rows  # noqa: E402

OUT = os.path.join(HERE, "textures")


def main():
    ashlar(37, ["Stone", "Gravestone", "Driftwood"], courses=8, blocks=4, joint=0.6, lit=1.08, spread=1.0).save(OUT, "CruorStone")
    roof_rows(33, ["Iron", "Slate", "Ashgrey"], run0=0.0, run1=1.0, rows_per_tile=6, unit_w=(14, 22)).save(OUT, "CruorSlate")
    glass(34, ["FountBlood", "SynodRed", "Blood"], "CryptIron").save(OUT, "CruorGlass")
    ashlar(35, ["CryptFlagstone", "CryptFloor", "CryptDais"], courses=2, blocks=2, joint=0.65, lit=1.03).save(OUT, "CruorFloor")
    plaster_wall(10, ["Linen", "Plaster", "CandleWax"]).save(OUT, "CruorPlaster")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
