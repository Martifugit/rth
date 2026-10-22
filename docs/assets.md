# Art style and asset rules

Status: **proposed** (2026-10-22). R'th should look like Old School RuneScape (iconic, low-poly,
simple, a bit of fantasy) and be makeable by two people. These rules come from measuring how
the 2004-era game built its art ([research 04](research/04-osrs-assets.md)), at about three times
its budgets. Today's art is measured in [scale-today.md](research/scale-today.md); sizes are in
[world-scale.md](world-scale.md).

**Check it:** **R'th > Art > Validate Art** in the Editor reports every model over budget, models
with too many colours, material colours off the palette and textures that break the rules
(Console, and `client/Logs/art-validation.md`). The budgets are data:
`client/Assets/Art/art-budgets.json`. It also runs in batch mode:
`-executeMethod Rth.Client.Editor.ArtValidator.ValidateFromCommandLine -rthScenes Assets/Scenes/Tolling.unity`.

## 1. The look in eight rules

1. **Few triangles.** Detail goes into silhouette, not surface.
2. **One flat colour per face**, two to four colours per model (eight for characters).
3. **Smooth light over those faces**, from one sun. The look is faceted because there are few
   faces, not because shading is flat.
4. **Muted models on coloured ground.** Most of a model is dark wood, stone and cloth; green
   comes from the ground and the leaves; strong colour marks what matters.
5. **A tiny texture set**, pixel art, used only where a pattern carries meaning.
6. **Reuse by recolouring.** One rock is every ore; one sword is every metal.
7. **Modular pieces** a tile wide: wall runs, roof slabs, fence spans.
8. **Characters get the detail** (several times the density of scenery), because players look
   at them.

## 2. Triangle budgets

| Kind | Target | Hard cap | Reference median | Today |
| --- | --- | --- | --- | --- |
| Small prop (crate, barrel, sign, tool) | ≤ 150 | 400 | 72 (items) | 12–48 (primitives) |
| Rock, furniture, stump, statue | ≤ 250 | 600 | 56 (rock), 84 (anvil) | 12–312 |
| Tree | ≤ 400 | 1 000 | 122 (oak) | 36–48 (block trees); leafy trees 124 (small), 340 (large) |
| Wall, floor, roof piece, fence span | ≤ 60 | 150 | 8 (door) | 12 |
| Player body (whole kit) | ≤ 1 500 | 2 500 | 488 | 856 |
| NPC | ≤ 1 500 | 3 000 | 525 | primitives |
| Large creature or boss | ≤ 3 000 | 6 000 | | |

Over the target is a warning, over the cap an error. A model is what a builder makes as one
unit (anything with an `Occluder`, `MapObject`, `MapBlocker`, `MapWall`, `RoofHider` or `ArtKind`)
or a model file under `Assets/Art` or `Assets/Resources/Characters`. Its kind is its `ArtKind`, or
else guessed from its name.

## 3. Colour

- **The palette** is `client/Assets/Resources/Art/palette.json`: **112 named colours** in groups
  (ground, stone, wood, foliage, building, metal, ore, cloth, skin, hair, humour, water, accent).
  The first 49 are the colours today's scenes already use, so nothing existing is off-palette.
  Name a material after its palette colour (`Bark`, `CopperOre`); the validator warns about
  material colours more than a few percent from every palette entry.
- **Per model:** 2–4 colours, characters up to 8. URP's Lit shader has no vertex colours, so
  each colour is its own material (a submesh). That keeps colours nameable, which is what
  recolouring swaps.
- **Muted.** Most faces low saturation and darker than mid (today 78 % of materials are darker
  than mid). Keep strong colours for what the player must notice: ore, loot, quest objects,
  danger, the Synod's red.
- **Grass is not in the models.** The ground carries most of the world's green.

## 4. Textures

- **At most about 40** world textures, **128 × 128** (64 for small things), **16–32 colours**,
  pixel art rather than painted.
- Only for **leaves** (cut-out), roofs and thatch, planks, brick and stone courses, water, and a
  few signature surfaces. Everything else is flat colour.
- They live in **`client/Assets/Art/Textures/`**, where `ArtTextureImporter` imports them point
  filtered, without mipmaps (they would eat the cut-out edges), alpha as transparency,
  uncompressed, capped at 128.
- Cut-out surfaces use **alpha clip** (URP Lit `_AlphaClip`, cutoff 0.5), drawn on both sides.
  URP's shadow pass honours the clip, so leaf shadows are ragged too.
- Interface art (`Resources/UI`, `Resources/Icons`) has its own rules (`docs/interface.md`).

## 5. Light

- **Smooth normals on rounded parts** (canopies, rocks, bodies), split normals on hard edges
  (walls, crates). Per-face colour either way.
- **One sun.** The real-time URP sun stays, with fog per region.
- **Decision A1 (owner): softer shadows.** A strong shadow under every prop departs most from
  the reference look and adds noise at our camera distance. Proposal: shadow strength about 0.5
  on the sun, and no shadows from ground decoration. Try it in the scale test region first.

## 6. Leafy trees

The reference's trees are a plain trunk and a canopy of a few dozen big faces wearing a
leaf-clump texture. Ours follow the same idea (`client/Assets/Editor/LeafyTree.cs`):

- a **trunk**: a six-sided tapering prism (and four-sided branches to side lobes), bark colour;
- per canopy lobe, a **dark solid core** (an icosahedron, 20 faces, `LeafCore`) inside a slightly
  larger **shell** (80 faces, `LeafClump`);
- **every shell face shows the whole texture**: the triangle's corners map to the texture's
  bottom-left, bottom-right and top-middle, and the bottom edge of the texture is a ragged fringe
  of hanging leaves with transparent pixels below it. Each face puts the fringe on its lowest
  edge (one in five on another), and faces are enlarged a little so they overlap. The outline
  breaks up and the core shows through the gaps;
- **the texture is ours**: `tools/art/leaf_texture.py` paints it (128 px, the palette's seven
  `Leaf1`–`Leaf7` greens, generated, nothing copied). Re-run it with another `--seed` for a
  variant; `--preview` writes an enlarged copy to look at.

| Tree | Height | Blocks | Crown | Triangles |
| --- | --- | --- | --- | --- |
| `LeafyTree.Small` | about 3.0 | 1 × 1 | about 2.2 | 124 |
| `LeafyTree.Large` | about 4.0 | 2 × 2 | about 3.4 | 340 |

Place them with `kit.LeafyTree(parent, x, z, LeafyTree.Small, groundHeight)`. The meshes are
saved to `Assets/Art/Trees/` each time a builder runs, so changing a spec and rebuilding updates
every tree. An autumn or dead variant is a recolour of `LeafClump` and `LeafCore`, not a new tree.

## 7. Recolouring

| What | How |
| --- | --- |
| A placed object or prefab | A **`Recolor`** component: material name → colour. Every renderer below it gets a shared copy of that material in the new colour. Works with fading (`Occluder.Remap`). |
| Every placement of an object type | **`"recolor"` in objects.json**: `"recolor": { "Stone": "CopperOre" }` (palette names or `#RRGGBB`). `ObjectRecolors` applies it as content arrives and as regions stream in. Client only: the server ignores it. |
| A held weapon | The item's `color` already tints the held art (`HeldItemArt.Tint`). |
| A character | `CharacterModel.SetColor(channel)` (skin, hair, clothes channels). |

So one rock model with a `Stone` and an `Ore` material serves every ore; a new ore is a line of
JSON.

## 8. Characters

- A **body kit** of parts on one skeleton (`tools/character-models`), recoloured per character.
  Today: 6 skinned parts and hair, 856 triangles, within budget.
- Equipment swaps or adds parts. NPCs reuse the kit with other colours before they get their own
  models.

## 9. What changes today's art (owner's call)

Nothing existing has been changed. When the sizes in `world-scale.md` are decided:

- the block trees (`WorldKit.Tree`, three stacked boxes) give way to the leafy trees;
- primitives that make one thing (the bell tower, the monument, stations) become modelled
  props with 2–4 palette colours each, recolourable;
- the ground moves from flat patches to `MapTerrain` colours (see
  [world-building.md](world-building.md)).
