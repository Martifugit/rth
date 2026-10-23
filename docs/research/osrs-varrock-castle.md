# Research: Varrock castle, a big centrepiece measured

Status: complete (2026-10-23). [Research 04](04-osrs-assets.md) measured the 2004 game's assets in
general (all scenery at once). This note measures **one large centrepiece**, Varrock castle, so a
monumental building of our own (Cruor's great church, `tools/art/blender/buildings/lab/monumental.py`)
can be compared like for like. Nothing is copied: only numbers come out of the data.

**Source:** the open-source 2004scape `Server` data (maps, loc configs, loc models, textures). Get
it with:

```
git clone --depth 1 --filter=blob:none --sparse https://github.com/2004scape/Server.git
git sparse-checkout set data/src/maps data/src/scripts data/src/textures data/src/pack data/src/models/loc
```

**Scripts:** in `tools/research/`. Each prints numbers only:
- `castle_extent.py` maps the castle's pieces per level;
- `castle_measure.py` lists every placed piece and totals them;
- `castle_more.py` covers floors, textures and piece shapes;
- `castle_shell.py` gives the structure alone;
- `castle_floors.py` gives the floor overlays.

The castle is the box x 3200–3232, z 3455–3500, levels 0–3.

## 1. The castle in numbers

| | Whole castle | Its shell (walls, battlements, edges, windows, roofs, doors) |
| --- | --- | --- |
| Placed pieces | 1 755 | 864 |
| Distinct models | | 28 |
| Triangles | 130 910 | **24 836** |
| Textured faces | 10 % | 37 % |
| Surface | 4 285 sq tiles | **2 936 sq tiles** |
| Triangles per sq tile of surface | 30.5 | 8.5 |

- **By level:** 54k triangles on level 0, 39k on level 1, 32k on level 2 and 5k on level 3.
- **Footprint:** 631 tiles hold a piece on level 0, about 25 × 30 tiles.

**Most triangles are dressing, not building.** The 106k triangles that aren't shell are:
- suits of armour, 31 × 448;
- torches, 74 × 150;
- wooden railings, 75 × 124;
- cannons, 16 × 488;
- shields, banners, statues, furniture, grass and twigs.

So the structure is cheap and plain, and the detail sits in the small things a player walks up to.

## 2. The pieces

| Piece | Count | Triangles each | Size (tiles) | Notes |
| --- | --- | --- | --- | --- |
| Castle wall | 424 | 10 | 3.0 tall, 1 long, 0.25 thick | One texture repeat over the whole piece; `sharelight` |
| Battlement | 165 | 24 | One merlon a tile: 0.5 wide, 0.25 thick, 0.5–0.75 above the walk | Textured like the wall; `sharelight` |
| Castle edge / corner | 39 | 195 (average) | 2 × 2.5 | Where the geometry goes: corners and tower turns |
| Arrow slit | 58 | 59 | 0.46 wide, 1.06 tall, 0.09 deep | A thin decoration on the wall's face, `ambient=20` |
| Church window | 12 | 87 | 0.49 wide, 1.25 tall | `ambient=100` |
| Large window | 12 | 118–170 | 1.0 wide, 0.9 tall | |
| Roof (red slate) | 42 | 34 | 1 × 2 | Only 1.4k triangles of roof in the whole castle |
| Diagonal walls | 100 placements | | | The octagonal towers are square plans with their corners cut by diagonal wall pieces |

**Wall thickness:** 0.25 tiles. A wall is a skin, not a mass.

**Height:**
- The castle wall piece is 3.0 tiles tall (a step at 1.88); a house's brick wall is 1.88.
- The castle has 3 levels of floor plus tower tops.
- The battlement tops sit about two levels up, so roughly **4–6 tiles high** at most.
- Width to height is about 30 : 5.

## 3. Surfaces and colour

- **One wall texture carries the whole castle.** It is 128 × 128 pixels in 7 greys (luminance 39–110: strong contrast).
  - **Pattern:** small bricks with dark mortar outlines and light edges.
  - **Scale:** one repeat spans about 3 × 2 tiles, so bricks are about **0.25 tiles tall and 0.5 long** (7–8 courses a storey).
  - **Shared:** the wall, battlement and edge pieces all wear it, so the castle reads as one material.
- **Untextured colours are greys** (saturation 0). The most used are lightness 61, 37, 45 and 28 of 127.
- **Strong colour is only in the dressing:** banners and standards (saturation 7, `contrast=100`), shields and the orange furniture.
- **Floors:** level 0 is a grey stone "road" overlay (#505050). Level 1 is wooden planks. Levels 2 and 3 are a flat grey roof floor (#5B5B5B) you walk on. There are **no pitched roofs** over the castle, only a few slate pieces (1.4k triangles).
- **Light:** every structural piece has `sharelight=yes`, so a run of wall lights as one surface. Windows and slits get `ambient` (20–100) so they stay bright.

## 4. Against Cruor's great church (outside view, after the polish pass)

| | Varrock castle shell | Great church (outside) |
| --- | --- | --- |
| Triangles | 24 836 | 23 556 |
| Surface | 2 936 sq tiles | about 50 000 sq tiles (some buried inside other pieces) |
| Triangles per sq tile | 8.5 | 0.5 |
| Height | about 5 tiles (towers a little more) | nave walls 22, towers 42, spires to about 70 |
| Footprint | about 25 × 30 | about 44 × 75 |
| Wall thickness | 0.25 | 1.0 (screen and towers solid) |
| Materials on the structure | one brick texture, greys | 9 materials, five painted textures |
| Brick or block size | 0.25 × 0.5, high contrast, dark outlines | 0.5 × 1.0, low contrast, thin joints |
| Windows | 0.5–1.0 wide, at most 1.25 tall, few | 2.4–11 wide, up to 10 tall, one in every bay |
| Strong colour | in the dressing (banners, shields) | on the structure (glass, courses, rails, portal) |
| Small structural pieces | merlons, one a tile, all alike | about 900 pieces under 0.7 tiles across, of many kinds |
| Tops | flat floors you walk on | pitched slate roofs and spires |
| Where the fine detail is | dressing: armour, torches, railings, cannons | the shell itself |

**What this says:**
1. **The triangle total matches; the size does not.** The church spends the same triangles on about
   17 times the surface. It is the size, not the count, that sets it apart.
2. **The reference building is a plain, cheap, one-material skin.** Its look comes from:
   - one strong brick texture;
   - a single repeated silhouette element (the merlon);
   - corners that carry the geometry;
   - small windows as thin decorations on the wall face.
3. **Richness comes from dressing and colour accents placed on the structure, not built into it.**
   Banners, shields, torches and armour carry the colour and the fine detail, and the player can
   walk up to all of them.
4. **The tops are part of the place.** Levels 2–3 are floors with people on them.

Possible directions for our monumental kits follow from this (to discuss, not decided):
- one dominant wall texture with smaller, higher-contrast courses;
- red kept for dressing (banners, glass) rather than structure;
- one repeated silhouette element instead of many kinds of spike;
- detail moved into furnishing models;
- a lower, wider mass, unless the height is the point;
- walkable tops where they make sense.
