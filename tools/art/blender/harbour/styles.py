"""The harbour kit's styles: each a set of colour swaps (a palette name to the palette colour it is
drawn in), the textures kept, as objects.json "recolor" does in the game. Plain is the kit as built;
the others are starting points for a port's identity. Add a style here and the gallery shows every
kit in it.
"""

STYLES = {
    "Plain": {},
    "Weathered": {                      # grey, sun-bleached timber; old green-stained stone
        "Boards": "Driftwood", "Fence": "DeadWood", "Ash": "Driftwood", "Planks": "Driftwood",
        "Harbourstone": "MossyStone", "Flagstone": "Granite", "Gravestone": "Stone",
    },
    "Dark": {                           # tarred timber and black stone (a volcanic coast)
        "Boards": "Bark", "Fence": "Charcoal", "Ash": "Umber", "DeadWood": "Charcoal",
        "Harbourstone": "DarkRock", "Flagstone": "CryptFloor", "Gravestone": "CliffRock", "Linen": "Wool",
    },
    "Pale": {                           # limed timber and sandstone (a southern port)
        "Boards": "Ash", "Fence": "Fence", "Ash": "Planks",
        "Harbourstone": "Sandstone", "Flagstone": "Dust", "Gravestone": "SaltFlat",
    },
}
