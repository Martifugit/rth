# R'th's scale today, measured

Status: measured 2026-10-22 from the committed scenes and assets. The reference numbers come from
the 2004-era game ([research 03](03-osrs-scale.md), [research 04](04-osrs-assets.md)); the rules
that follow are in [the world scale guide](../world-scale.md).

**Re-run:** `python3 tools/scale-report/scale_report.py` (standard library only, no Unity). It
reads the region scenes as Unity YAML, works out every primitive's world bounds through its
parents, reads the player's FBX, the materials and textures, and takes the camera defaults from
`CameraRig.cs` and `Game.unity`. In the Editor, **R'th > Art > Validate Art** checks the same
art against the budgets in [the assets guide](../assets.md).

Units are tiles (1 Unity unit, `TileMath`). "÷ player" divides by the player's height.

## 1. Sizes

| Thing | 2004 reference | ÷ its player | R'th today | ÷ our player | Where |
| --- | --- | --- | --- | --- | --- |
| Player | 1.56 | 1 | **1.94** (top of the hair) | 1 | `Resources/Characters/HumanMale.fbx` |
| Wall piece, one storey | 1.88 | 1.2 | **1.70** | **0.88** | `WorldKit.Wall` default, every house in Tolling (30 walls) |
| Doorway | 1.88 (a wall-height opening) | 1.2 | 1.70: a gap in the wall, no door model | 0.88 | `WorldKit.House` |
| Roof eaves / ridge | (not measured) | | 2.05 / 3.27–3.58 | 1.06 / 1.69–1.85 | the six houses in Tolling |
| Upper floors | 1.88 per storey | 1.2 | none (plane 0 only) | | `MapArea.plane` |
| Low walls (toll-house ruin) | dry stone 1.31 | 0.84 | 0.6–1.4 | 0.3–0.7 | Palmerway |
| Fences | 0.5–0.7 | 0.3–0.45 | 0.61 (rail), posts 0.8 | 0.31 | `WorldKit.Fence` |
| Palisade stakes | | | 1.8 | 0.93 | Palmerway's waygate |
| Ordinary trees | 2.3–3.0 on 2 × 2 | 1.5–1.9 | **2.1–3.7** on **1 × 1** (crown up to 2.6 wide) | 1.1–1.9 | `WorldKit.Tree`, 27 in Tolling and Palmerway |
| Biggest tree | oak 3.4 on 3 × 3 | 2.2 | oak **3.9** on 1 × 1 (crown 2.7) | 2.0 | Tolling |
| Tallest common city wall | 2.75 | 1.8 | none yet | | |
| Landmark | | | Tolling's bell tower **5.8** (2 × 2), Cruor's monument 3.8 | 3.0, 2.0 | Tolling, Palmerway |
| The Saint's crypt (an instanced arena) | | | piers 14, walls 20–22 | 7–11 | `SaintCryptBuilder` (the scene file is not committed, so the tool measures the builder's constants by hand) |

**What this says.** Tile for tile, R'th is already about as squat as the reference: trees are
2–4 tiles, the tallest building on the surface is a 5.8-tile tower. The odd one out is the
**player**: at 1.94 tiles they are 1.24 × the reference's, so **walls come up only to their
shoulders** (0.88 × the player, against 1.2 × in the reference). Roofs, at 1.7–1.85 × the
player, make houses read as huts. Trees stand on one tile with a crown two or three wide, where
the reference gives a big tree a 3 × 3 footprint.

## 2. Building footprints

Measured from each `RoofHider`'s interior rectangle (tiles inside the walls).

| Region | Building | Footprint |
| --- | --- | --- |
| Tolling | Sallow's apothecary | 7 × 6 |
| Tolling | Synod treasury | 6 × 5 |
| Tolling | Mill house (Widdermill) | 5 × 6 |
| Tolling | Cottage | 5 × 4 |
| Tolling | Marsh cottage, Cottage | 4 × 4 |
| Tolling | Bell tower (solid) | 2 × 2 |
| Palmerway | Old toll-house (roofless ruin) | 6 × 5 |
| Palmerway | Cruor's monument (plinth on two steps) | 1 × 1 on 5 × 5 |

Streets: Tolling's roads are about 2 tiles wide (`Along(..., 0.9f)`), the square 11 × 11. No
building has an upper floor.

`Game.unity` has no buildings: it holds the camera, the sun and the "unbuilt land" plane.

## 3. Terrain

| Measure | 2004 reference | R'th today |
| --- | --- | --- |
| Ground height (median / max) | 2.0 / 15.9 | **0 / 0.12** (road and floor patches) |
| Relief per 24 × 24 window (median / max) | 2.9 / 15.9 | 0.01 / 0.12 |
| Neighbouring steps over 1 tile (cliffs) | 2.5 % | 0 % |
| Base noise where no height is set | 0.6–3.75 | none |

**The world is flat.** Each region's ground is one box at y = 0 (`WorldKit.BeginRegion`), with
thin recoloured patches on it; water is a box a few centimetres down. There is no heightfield in
the client, the exporter or the server: `MapExporter` only asks "is there a ground collider
under this tile centre". Anything in the guide about hills, cliffs and the camera lifting over
terrain needs ground with height first (section 3 of [the guide](../world-scale.md)).

## 4. Camera

From `CameraRig.cs` and the camera in `Game.unity`:

| Setting | 2004 reference | R'th today |
| --- | --- | --- |
| Distance | 7.7–13.7, tied to pitch (no zoom) | 14 (zoom 5–30) |
| Pitch | 22.5°–67.3° (starts at 22.5°) | 45° (20°–80°) |
| Focus | 0.39 above the feet | 1.0 above the feet |
| Vertical field of view | 36° | 45° |
| Draw distance | 25, fog at the edge | far plane 120; fog from the region (45–85 by default) |
| Over hills | the pitch floor rises with ground near the player | nothing (there are no hills) |
| See-through | roofs on the line of sight hide; nothing else | anything with an `Occluder` fades (trees, walls, roofs, the tower); roofs hide while you are inside |

What one screen shows (16:9), from `scale_report.py`. "Hides the focus within" is how close in
front of the player (towards the camera) a thing of that height has to stand to cover the point
the camera looks at:

| Camera | Distance | Pitch | Ground towards the camera | Beyond the player | Width at the player | 1.7 wall hides the focus within | 3.0 tree hides it within |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2004 default (lowest) | 7.7 | 22.5° | 3.2 | about 18 (draw limit) | 7.7 (512 × 334) | 3.2 | 6.3 |
| 2004 middle | 10.7 | 45° | 3.5 | 8.1 | 10.7 (512 × 334) | 1.3 | 2.6 |
| **R'th default** | 14 | 45° | 5.4 | 16.4 | 20.6 | 0.7 | 2.0 |
| R'th closest, lowest | 5 | 20° | 1.7 | horizon | 7.4 | 1.9 | 5.5 |
| R'th farthest | 30 | 45° | 12.0 | 32.4 | 44.2 | 0.7 | 2.0 |
| R'th default distance, lowest pitch | 14 | 20° | 6.8 | horizon | 20.6 | 1.9 | 5.5 |

The default view is close to the reference's middle setting, about twice as wide (a 16:9 screen
and a farther camera). Low walls and the 1-tile focus height mean walls hardly ever hide the
player today; trees do, which is why everything tall fades.

## 5. Art

| | 2004 reference | R'th today |
| --- | --- | --- |
| Scenery | median 82 triangles, 90th percentile 364 | built from Unity primitives (a cube is 12 triangles, a cylinder 80): per object median **12**, 90th percentile 48, max 312 |
| Player | 7 rigid parts, about 490 triangles | 6 skinned parts and hair, **856** triangles (head 60, torso 128, arms 120, hands 264, legs 164, feet 80, short hair 40) |
| Colours per model | 2 (scenery), 3 (characters) | 1 (median), at most 4: one flat material per primitive |
| Materials and palette | 756 colours across all scenery | 36 flat URP Lit materials in the scenes: 50 % warm (red to ochre), 33 % green, 8 % blue, 8 % grey; 78 % darker than mid |
| Textures | 50, 128 px, median 9 colours | **none** on world art (one 64 px soft dot for effects). 141 interface images (icons 96 px) |
| Lighting | baked smooth light, one sun, no shadows | real-time URP sun with shadows, fog per region |
| Recolouring | 26 % of scenery, 77 % of NPCs, 58 % of items | `CharacterModel.SetColor(channel)` recolours the body; scenery has none (each colour is its own material) |

The world is a block-out of primitives, so it is already far under every triangle budget. The
gaps are the other way round: **too few colours per model** (one per box), no textures at all
(so no leaves), green carried by the models rather than the ground, and no way to recolour a
scenery model.

## 6. The short version

| | Reference | Today | What to decide (see the guide) |
| --- | --- | --- | --- |
| Wall ÷ player | 1.2 | 0.88 | Raise walls to about 2.3, or shrink the player |
| Tree ÷ player | up to 2.2 | up to 2.0 | Keep; give big trees 2 × 2 and 3 × 3 footprints |
| Landmark ÷ player | | 3.0 (bell tower) | Keep, with open ground around it |
| Ground | rolling, 2.5 % cliffs | flat | Add a heightfield (client and exporter) |
| Camera | 22.5–67°, no zoom | 20–80°, zoom 5–30 | Narrow the pitch range, add terrain lift and roof hiding |
| Colours per model | 2–3 | 1 | Model props with 2–4 palette colours |
| Textures | 50 | 0 | A small set: leaves first |
