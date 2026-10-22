# World layout

`layout_from_sketch.py` made the first `client/Assets/World/layout.json` from `images/map.png`
(see [`docs/world-building.md`](../../docs/world-building.md)) and a preview at
`docs/images/world-layout.png`. It needs Pillow (`pip install pillow`).

    python3 tools/world-layout/layout_from_sketch.py --tiles-per-square 64

Once the layout has been edited by hand, don't run it again over the edits: it overwrites
`layout.json`. Bake regions from the layout in Unity with **R'th > World > Bake Terrain From Layout**.
