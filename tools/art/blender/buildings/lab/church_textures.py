#!/usr/bin/env python3
"""
Textures for the lab's first great church (monumental.py, the polished "A"), in the paint lab's
style: clean surfaces, a fine low-contrast pattern in close shades, richness from the contrast
between materials. Cruor's chosen style (church "B" and the city kit) has its own textures in
../cruor/textures.py.

  ChurchAshlar   pale dressed stone, big blocks a tile long (one repeat: 2 x 2 tiles)
  ChurchTrim     Synod red stone for string courses, drip courses, rails (1 x 1)
  ChurchSlate    dark slate in neat rows for the roofs and spires (2 x 1)
  ChurchGlass    red glass in diamond quarries, dark leads (1 x 1)
  ChurchFloor    big dark flagstones (4 x 4)

    python3 tools/art/blender/buildings/lab/church_textures.py

Writes PNGs into textures/ beside this script. The painters are in ../painting.py.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from painting import ashlar, glass, roof_rows  # noqa: E402

OUT = os.path.join(HERE, "textures")


def main():
    ashlar(31, ["Stone", "Gravestone", "Driftwood"], courses=4, blocks=2).save(OUT, "ChurchAshlar")
    ashlar(32, ["SaintRobe", "SynodRed", "Brick"], courses=2, blocks=2, joint=0.8, lit=1.04).save(OUT, "ChurchTrim")
    roof_rows(33, ["Iron", "Slate", "Ashgrey"], run0=0.0, run1=1.0, rows_per_tile=6, unit_w=(14, 22)).save(OUT, "ChurchSlate")
    glass(34, ["FountBlood", "SynodRed", "Blood"], "CryptIron").save(OUT, "ChurchGlass")
    ashlar(35, ["CryptFlagstone", "CryptFloor", "CryptDais"], courses=2, blocks=2, joint=0.65, lit=1.03).save(OUT, "ChurchFloor")
    print("wrote church textures")


if __name__ == "__main__":
    main()
