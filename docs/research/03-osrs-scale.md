# Research 03: Scale in the 2004-era game, measured

Status: complete (2026-10-22). The numbers come from the open-source
[2004scape](https://github.com/2004scape) project (also known as Lost City): its `Server` repository
holds the whole 2004 map as text (`data/src/maps/*.jm2`), every object definition
(`data/src/scripts/**/*.loc`) and every model (`data/src/models/**/*.ob2`); its `Client2`
repository is a TypeScript port of the 2004 client, which gives the camera. Nothing was copied
from either: `osrs_scale.py` reads the data and prints statistics, and `camera_compare.py` works
out what each camera shows. The measuring scripts live with the original research, in the `Martifugit/rpgish` repository
(branch `claude/pensive-cerf-6v0elo`, `tools/research/`), not in this one.

The goal was to replace "it should feel like OSRS" with numbers we can design to. The rules that
follow from them are in the [world scale guide](../world-scale.md), and R'th's own numbers are in
[scale-today.md](scale-today.md).

## 1. Units

- A tile is 128 units. The map is cut into 64 × 64 tile **map squares** (R'th's regions are usually
  96 × 96, cut into 8 × 8 zones).
- A tile's height is one byte `h`, placed `h × 8` units up, so heights step in **1/16 tile** and
  top out at **15.9 tiles**.
- A tile without a written height gets the client's procedural noise: a smoothed three-octave
  value noise clamped to 10–60, which is **0.6–3.75 tiles**. Nearly half of the surface tiles use
  it (43 %; 57 % have a written height). The ground is never flat unless someone flattened it.
- Upper floors sit a fixed **240 units (1.88 tiles)** above the floor below.

## 2. Bodies and objects

Measured from the model files, with each object's resize factors applied. Heights are above the
tile's ground.

| Thing | Height (tiles) | Footprint | Relative to the player |
| --- | --- | --- | --- |
| Player (head to feet, male base kit) | 1.56 | 1 × 1 | 1 |
| Wall piece, every material (brick, timber, castle, cave face) | 1.88 | edge of 1 tile | 1.2 |
| Upper floor (one storey) | 1.88 | | 1.2 |
| Ardougne city wall (the tallest common wall) | 2.75 | 1 × 1 | 1.8 |
| Dry-stone wall | 1.31 | edge | 0.84 |
| Gnome fence, posh fencing | 0.5–0.7 | edge | 0.4 |
| Bush | 0.7 | 1 × 1 | 0.45 |
| Dead trees | 2.3–2.7 | 1 × 1 or 2 × 2 | 1.5–1.7 |
| Normal tree (`tree`, `tree2`) | 2.3–2.9 | 2 × 2 | 1.5–1.9 |
| Jungle tree | 3.0 | 2 × 2 | 1.9 |
| Oak | 3.4 | 3 × 3 | 2.2 |
| Mountain and cliff "tops" (`cavewall_top_*`, 13 000+ on the surface) | 1.88 | 1 × 1 | 1.2 |

Over the surface (298 map squares, 311 000 placed objects on the ground floor):

| Kind (by placement shape) | Count | Median | 90th percentile | 99th percentile |
| --- | --- | --- | --- | --- |
| Walls | 36 170 | 1.88 | 1.88 | 3.0 |
| Free-standing objects (trees, rocks, furniture, stalls) | 69 738 | 1.88 | 2.9 | 7.4 * |
| Ground decoration (flowers, rubble, rugs) | 188 829 | 0.06 | 0.38 | 0.75 |

\* The tail is bridges and walkways that hang over chasms. Nothing ordinary on the surface is
much taller than the oak.

**What this says:** the whole world is squat. A wall is barely taller than a person, the biggest
tree is about twice a person, and "mountains" are rock blocks one storey high laid over raised
ground. Impassable terrain is shown by rock and texture, not by height.

## 3. Terrain

Surface map squares only (dungeons excluded):

| Measure | Median | 90th percentile | 99th percentile | Max |
| --- | --- | --- | --- | --- |
| Ground height above the lowest point | 2.0 | 5.0 | 9.4 | 15.9 |
| Height step between neighbouring tiles | 0.06 | 0.38 | 1.8 | 13.4 |
| Relief (highest minus lowest) in a 24 × 24 tile window, about one screen | 2.9 | 7.7 | 12.4 | 15.9 |
| Relief in a whole 64 × 64 map square | 4.3 | 8.9 | | 15.9 |

- **Only 2.5 % of neighbouring tile pairs differ by more than a tile.** Those are the cliffs. The
  rest is gentle rolling ground.
- The typical tile-to-tile step is 0.06 tiles: the ground rolls very softly almost everywhere, and
  the whole world fits in 16 tiles of height.

## 4. The camera

From `Client2/src/js/game.ts` (`orbitCamera`, the pitch clamp) and `World3D.init`:

- **No zoom.** The distance follows the pitch: `distance = pitch × 3 + 600` units, which is **7.7
  tiles at the lowest pitch and 13.7 at the highest**.
- **Pitch 22.5° to 67.3°**, starting at the lowest (22.5°). It looks at a point 0.39 tiles above
  the player's feet.
- **Vertical field of view 36°** (512 × 334 viewport, focal length 512). Nothing is drawn beyond
  **25 tiles**; fog hides the edge.
- **Terrain lifts the camera.** Every frame the client finds the highest ground within 4 tiles of
  the camera's focus. If it stands above the player, the lowest allowed pitch rises: 2 tiles
  higher gives at least 34°, about 4 tiles higher gives the maximum. It eases up quickly (1/24 of
  the gap per frame) and settles back slowly (1/80), so hills tilt the view without jerks.
  Precisely: the floor is **16.875° per tile** of height difference, never below 22.5° nor above
  67.3°, and a client frame is 20 ms.
- **Roofs hide along the line of sight.** Below 54° pitch, the client walks the tiles on the line
  from the camera to the player. If any carries the "roof" flag, or the player stands on one,
  every floor above the player's is hidden. Nothing else ever goes see-through: walls and trees
  are kept short instead.

What the 2004 camera shows, in tiles:

| Camera | Distance | Pitch | Ground towards the camera | Beyond the player | Width at the player | Wall hides the player within | Largest tree hides the player within |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2004, default (lowest) | 7.7 | 22.5° | 3.2 | about 18 (draw limit) | 7.7 | 3.6 | 7.3 |
| 2004, middle | 10.7 | 45° | 3.5 | 8.1 | 10.7 | 1.5 | 3.0 |
| 2004, highest | 13.7 | 67° | 4.2 | 5.9 | 13.6 | 0.6 | 1.3 |

R'th's own rows are in [scale-today.md](scale-today.md), section 4.

## 5. R'th today, for comparison

Measured separately in [scale-today.md](scale-today.md).

## 6. Size of the world

The 2004 surface is **298 map squares** (about 1.1 million tiles) in a block about 25 squares
wide; 116 more squares hold dungeons. In R'th's 96 × 96 regions that would be about 120 regions.
