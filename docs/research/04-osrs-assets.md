# Research 04: How the 2004-era game's assets are built, measured

Status: complete (2026-10-22). This is the companion to [research 03](03-osrs-scale.md), which
measured sizes. This one measures how the assets themselves are made, so R'th can make its own
in the same spirit. Nothing is copied: the aim is the decisions behind the look, not the models.

The sources are the same as research 03: the open-source [2004scape](https://github.com/2004scape)
`Server` data (every model, object, NPC, item, texture and floor definition) and the `Client2`
renderer. `osrs_assets.py` produces every number below. The measuring scripts live with the original research, in the `Martifugit/rpgish` repository
(branch `claude/pensive-cerf-6v0elo`, `tools/research/`), not in this one.
R'th's own numbers come from `tools/scale-report/scale_report.py` and are in
[scale-today.md](scale-today.md). The rules that follow are in [the assets guide](../assets.md).

## 1. The look in eight decisions

1. **Very few triangles.** Scenery has a median of 82, a person about 490.
2. **One flat colour per face**, and only 2–3 colours per model.
3. **Smooth lighting across those faces**: one fixed sun, baked into vertex colours. The look is
   faceted because of the low triangle count, not because of flat shading.
4. **Muted models on a coloured ground.** Models are mostly greys and browns. Green comes from the
   ground and a few leaf textures; strong colour is kept for things that matter.
5. **A tiny texture set**: 50 textures of 128 × 128 or 64 × 64 pixels, with a median of **9
   colours each**. They are used only where a pattern carries meaning: leaves, roofs, planks,
   bricks, water.
6. **Reuse through recolouring.** About half of all objects, NPCs and items are another
   definition's model with colours swapped, or the same body kit assembled differently.
7. **Modular pieces.** Wall segments, corners and roof pieces are one tile each. Floors are a
   blended colour layer plus crisp overlays with diagonal shapes.
8. **Detail goes to characters.** Characters have 5–7 × the triangle density of scenery, because
   players look at them.

## 2. Triangle budgets

| Kind | Models | Median | 90th percentile | Max |
| --- | --- | --- | --- | --- |
| Scenery (walls, trees, furniture, rocks) | 1 800 | 82 | 364 | 1 000 |
| Items (ground and inventory models) | 747 | 72 | 172 | 489 |
| NPC model parts | 335 | 146 | 474 | 949 |
| Whole NPC (all parts) | 179 measured | 525 | 781 | 1 084 |
| Player body kit (one each of head, jaw, torso, arms, hands, legs, feet) | 126 parts | **488** for a full body | | |
| Spell and hit effects | 104 | 172 | 512 | 1 248 |

Single examples:

| Model | Triangles |
| --- | --- |
| Door | 8 |
| Mining rock | 56 |
| Anvil | 84 |
| Oak (3 × 3 tiles, 3.4 tall) | 122 |
| Fountain | 302 |
| Man | 494 |
| Goblin | 641 |
| Cow | 449 |
| Chicken | 134 |

**Detail density.** The median triangle edge is **0.23 tiles on scenery** but **0.08–0.09 on
characters**. Scenery has 35 triangles per square tile of surface; NPCs have 257 and items 381.
Small things and people get the detail; big things stay plain.

## 3. Colour

- Colours are 16-bit HSL: **64 hues × 8 saturations × 128 lightnesses**. Each untextured face has
  exactly one.
- **Per model:** a median of **2 colours** (scenery, items) or **3** (characters). The 90th
  percentile is 5 (scenery, items) and 7–8 (characters).
- **The whole scenery set** uses only 756 distinct colours over 220 000 faces.

What the scenery's faces look like (shares weighted by face count):

| | Share |
| --- | --- |
| Hue 0–60° (reds, browns, ochres, yellows) | 88 % |
| Hue 90–150° (greens) | 4 % |
| Hue 180–240° (blues) | 5 % |
| Saturation 0 (pure grey) | 34 % |
| Saturation 7 (the most saturated step) | 25 % |
| Lightness under half | 80 % |

Characters and items look the same. A model is mostly dark, muted wood, stone and cloth, with a
small amount of strong accent colour. Grass is not in the models at all: it is the ground's
underlay colour. Leaves are a texture. The oak, for example, is 74 dark brown faces of bark and
48 faces of leaf texture.

## 4. Textures

- **50 textures in the whole game:** 44 at 128 × 128 pixels and 6 at 64 × 64.
- **Colours per texture:** a median of **9**, 90th percentile 32, max 64. They are pixel-art
  palettes, not painted images.
- **What they are:** bark, leaves (with cut-out transparency), roof tiles, thatch, planks, brick
  and stone walls, marble, water (one animated), doors, paintings, books, webs, lava.
- **How much they are used:** 36 % of scenery models have any textured face, but only **7 % of
  scenery faces** are textured. Characters and items are almost never textured (0–1 %). The rest
  is per-face colour.

A texture is chosen when the surface needs a pattern that would cost too many triangles to model
(courses of brick, roof tiles, leaf clusters) and is otherwise avoided.

## 5. Shading and light

- **Mostly smooth lighting.** 89 % of scenery faces use smooth (per-vertex) lighting, and only
  11 % are lit flat for hard edges. Each face keeps one colour, so the result reads as faceted
  while light still rolls softly over rounded shapes.
- **One fixed light**, from the same direction everywhere, plus ambient light. It is computed
  once when the scene loads and baked into the vertex colours.
- **No normal maps and no real-time shadows.** A few objects (37) darken the ground under
  themselves.
- **Per-object controls:**
  - `ambient` and `contrast` (on 294 objects) brighten or flatten an object's lighting.
  - `sharelight` (723) averages normals with neighbouring pieces, so a run of wall segments
    lights as one wall with no seams.
  - `hillskew` (515) bends a model to follow the slope it stands on, so fences and walls sit on
    hills without floating.

## 6. Reuse

| | Definitions | Distinct models | Notes |
| --- | --- | --- | --- |
| Scenery objects | 3 387 | 1 620 | 68 % share a model with another definition. 26 % recolour, 8 % retexture, 210 mirror, 37 resize. |
| NPCs | 1 017 | | Built from a median of **7 parts**: the same body kit as players. 77 % recolour their parts. |
| Items | 1 869 | 547 | 58 % recolour. A sword is one model in many metals. |

The most reused scenery model (a door) backs 134 definitions. Every ore rock is one of four
plain rock models (the commonest is 56 triangles) with its colours swapped.

**Recolouring is the main way new content gets made**: a definition names a source colour and
the colour to replace it with, up to four pairs. Retexturing does the same for textures.

## 7. The ground

- **29 underlays**: plain colours (grass greens, mud browns, sand, rock greys). The client blends
  each tile's colour over an **11 × 11 tile window**, so grass shades into mud over several tiles
  with no visible edge.
- **49 overlays** (15 of them textured: planks, water, roof) cover 28 % of surface tiles. They are
  crisp: a path is a path. **11 % of overlay tiles use one of 12 shapes in 4 rotations** (half
  tiles, diagonals, corners), so roads and shorelines run diagonally without stair-steps.
- Height noise, colour blending and crisp overlays together make the ground. It carries most of
  the world's colour.

## 8. Characters

- **The body is a kit of seven parts** (head, jaw, torso, arms, hands, legs, feet), about 490
  triangles in all. Equipment swaps parts or adds models; NPCs reuse the same kit with recolours.
- **Animation is rigid.** Every vertex belongs to exactly one group, and frames move groups. There
  is no skin weighting, so a limb is a solid piece and joints are hidden by the shapes. It is
  cheap to author and reads clearly at a distance.
- Semi-transparent faces: 11 % of the player kit's faces, 3 % of NPCs', 4 % of scenery's and 27 %
  of effects'.

## 9. R'th today

See [scale-today.md](scale-today.md), section 5. In short: the world is a block-out of Unity
primitives (median 12 triangles an object, one flat material each, no textures), and the player
is a 6-part skinned kit of 856 triangles. It is under every budget; what it lacks is colours per
model, a few textures (leaves first) and recolouring.

## 10. What R'th should decide

The rules these measurements suggest are written up in [the assets guide](../assets.md):
triangle budgets at about 3 × the reference, a shared palette of named muted colours, a small set
of 128-pixel textures, smooth lighting with per-face colour, and recolouring as the main way to
make variants.
