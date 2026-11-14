# How we build the world together

R'th is built by three kinds of contributors at once:

- **the owner**, who decides and works by hand in the Unity Editor;
- **agents with Unity** (one at a time, on the owner's machine), who drive the open Editor through
  Unity's CLI, run Sync World, check in Play mode and merge;
- **agents without Unity** (any number, in their own worktrees or in the cloud), who have the
  repository, `dotnet` and Python but can't open a scene.

The rule that makes this work: **the world is text.** Every region is a data file anyone can
edit, read, diff and merge. Unity draws it, and whatever is made by hand in Unity is written back
into text too, so no one ever has to edit a `.unity` file to change the world.

## The pieces

```
world/regions/<id>.json            the region: rectangle, title, light, materials, pieces   (everyone)
world/regions/<id>.handmade.json   what the scene adds by hand: collision as text, objects   (Sync only)
world/snapshots/<id>.json          what the scene contains, group by group                   (Sync only)
world/snapshots/index.json         every region, its rectangle, scene and counts              (Sync only)
world/screens/<id>/                top.png, from-south.png, from-east.png                     (Sync only)
world/renders/                     maps drawn by `tools/world render` (not committed)          (anyone)
server/data/maps/<id>.*            what the server loads; compiled from the two region files  (export only)
client/Assets/Scenes/<Region>.unity                                                           (Unity only)
   <MapArea>
     Data       drawn from world/regions/<id>.json by Sync World; rebuilt every sync
     Handmade   anything made by hand in Unity; Sync never changes it
```

**A region file** (`world/regions/tolling.json`) holds the region's header and a list of
**pieces**, one per line. Coordinates are absolute tiles (Tolling's south-west corner is (0, 0)),
rectangles are `[x0, y0, x1, y1]` with the far edges excluded. The kinds
(`shared/World/Data/RegionData.cs` documents every field):

| Kind | What it is | Collision |
|---|---|---|
| `patch` | Ground in a material over `rect` (a square, a field; with `plane`, an upper floor) | gives ground |
| `road` | Ground along `line` (points), `width` (half-width) | gives ground |
| `water` | Water over `rect` or along `line` | blocks |
| `wall`, `fence` | Along a grid line `from`-`to`, open at `gaps`. A fence with a `model` (a kit's span centred on its tile, along its +X: `HurdlePanel`, `BuckFenceRails`, `WallSpan`) is laid span by span from `from` towards `to` on the terrain, and its `post` (`HurdleStake`, `BuckFenceBuck`, `WallPier`) stands on its grid corners, at `"posts": "every"` one (the default) or only the `"ends"` of its runs (walls), once a corner however many fences meet there | blocks edges |
| `house` | Floor, four walls, a `door` (N/E/S/W) at `doorAt`, a roof that hides inside (the old boxes) | ground, walls |
| `building` | A building from a kit's models over `rect` (its walls on the rect's edges): `building` names the `kit` (`world/kits`), `storeys`, the `ridge`, the `faces` (a piece a tile, `"S"` from the west, `"W"` from the south, `"S1"` for the storey above; `"WallDoorway+Lantern"`), `colors` per storey, `roofColors`, `jetty`, `slit`, `bellcote`, `chimney` (`"W1"`), `floor`. Stands level on the terrain under it, with a foundation. A doorway with a leaf gets a door (the kit's door object on the tile inside) | ground, walls open at its doorways (their doors close them on the server); a chimney or a woodpile blocks the tile outside |
| `blocker` | An invisible barrier over `rect` | blocks |
| `tree`, `rock` | On tile `at`; look from `parts`, or grown from the seed | blocks its tile |
| `prop` | Anything on `at`: `parts` (boxes, cylinders) or a `model` prefab | `footprint` (objects: own tile) |
| `npc` | A creature or person (`npc`, `wander`) living at `at` | none |
| `spawn` | Where new players appear | none |
| `tiles` | Ground tile by tile over `rect`: `rows` of characters from the north, each one a `legend` entry (`material`, `blocks`, `water`, a drawn `height`, a `cliff` material for its steep faces); a space is no ground. Drawn by `TileGround` | ground; blocks where the legend says |

**The land first laid out from the continent layout** (`land_*.json`, made by a generator retired on
2026-11-14) is ordinary regions now. Their scenes (`Assets/Scenes/Land/`) are still ignored by git:
Sync World builds them on each machine from the data, since they hold nothing made by hand. A region
that gets hand work in its scene needs its area's folder (`"scene": "Assets/Scenes/<Area>/Land_x_y.unity"`),
and the next Sync World moves the scene there, where it is committed. The `Land*` materials and every
region's snapshot are committed as usual.

**Ground and terrain are Unity terrain** (decided 2026-11-14). The world's ground is sculpted and
painted on Unity terrains, one per 128-tile square (`client/Assets/World/Terrain/Terrain_i_j.unity`
and `.asset`): a heightmap sample on every tile corner, a paint texel on every tile, and a terrain
layer for each kind of ground in `world/grounds.json` (stones are layers too, `stones-<id>`). A tile
is the ground layer painted heaviest there. Saving writes each region's `tiles` piece (rows, legend,
stones, the ring of the neighbours' tiles) and its `terrain` (corner heights in hundredths of a tile,
one row of text per row of corners from the north) from the terrains; the compiler reads those as
before, so a tile rising more than the region's `cliffStep` across is a cliff and blocked. Never edit
a region's tiles or terrain by hand: the next save from the terrains replaces them. A building stands
level on the highest corner under it, on a foundation; other area kinds (patches, roads, houses) are
still drawn flat and get a warning on terrain. A water tile's height is the water's surface, so what is
placed on it (a punt, a jetty) floats.

Any piece placed on a tile becomes **interactive** with `"object": "<id in objects.json>"` (a rock
you mine, a tree you cut, a counter that is a bank), and a stair or ladder adds `climbTo`.
Placed pieces turn with `rot` (degrees, clockwise from above; footprints turn in quarter turns).
Materials are named in the region's `materials` (a colour or a palette name from
`client/Assets/Resources/Art/palette.json`); material names are shared across regions.

**Hand-made content** is for what isn't worth turning into data: unique set dressing, a one-off
mesh, terrain, everything the old builders made. It lives under the scene's `Handmade` root.
Sync World reads its collision (as rows of `.`, `#` and spaces, and wall edges), objects,
creatures and spawn into `<id>.handmade.json`, so the region still compiles without Unity.

## Commands

Without Unity (anyone):

```powershell
dotnet run --project tools/world -- check     # is the data valid, and are the server maps up to date?
dotnet run --project tools/world -- export    # write server/data/maps from world/regions
dotnet run --project tools/world -- render    # draw every region to world/renders/<id>.png and .txt
dotnet run --project tools/world -- format    # put region files in the standard layout
dotnet run --project tools/world -- paint area <id> [px]   # an area's regions as the client paints their ground, to world/renders
dotnet run --project tools/world -- grounds   # gather the regions' kinds of ground into world/grounds.json
dotnet run --project tools/world -- areas [check]                    # the world's areas: check world/areas.json, each area's stage, the coverage map
dotnet test                                   # includes the same check (WorldDataTests)
```

`check` fails when a piece is broken, names an object or creature the content doesn't have,
regions overlap, or `server/data/maps` is out of date with the data. `render` draws a region from
above (ground colours, blocked tiles darker, walls as lines, trees, rocks, objects cyan, creatures
orange, the spawn magenta, the 8-tile zone grid) and as text with coordinates; open the PNG to look.

With Unity (the owner, or the agent with Unity):

- **R'th > World > Sync World** (or `tools/unity-sync.ps1` with the Editor closed): for every
  region that changed since this machine last synced it, redraw `Data` from the data, capture
  `Handmade`, export through the C# compiler, check that exporting the whole scene the Unity way
  gives the same map, and write the snapshot and screenshots. A region counts as changed when its
  data, its hand-made file, its scene or the code that draws data changed, or its server maps are
  out of date (the record is `client/Library/RthSync.json`, per machine). **Sync World (no
  screenshots)** is quicker; **Sync World (everything)** redoes every region.
- **Shaping and painting the ground:** open an area (**R'th > Open Selected Area**, Alt+Shift+O); the
  terrains under its regions open beside them. Select a terrain and use Unity's own terrain tools:
  Raise or Lower Terrain, Set Height, Smooth Height, Stamp, and Paint Texture with the ground layers in
  `Assets/World/Terrain/Layers` (the Terrain Tools package adds more brushes). Brushes work across
  terrains. The game's ground redraws a moment after each stroke, as the game draws it (blended
  ground, crisp roads, water with banks dipping under it); **R'th > World > Show Unity Terrain** shows
  Unity's own view of the terrain instead. Ctrl+Z undoes a stroke. Saving (Ctrl+S) writes the ground
  of every region under the saved terrains into its data and exports the maps. A new kind of ground is
  an entry in `world/grounds.json`; its layer is made the first time it is painted from data.
  **R'th > World > Terrain > Write All Ground To Data** rewrites every region from the terrains.
- **Water** is drawn as one flat surface over the water's tiles and the ring round them, with the
  bank dipping under it by how much water lies near (`Shore`), so the waterline is curved wherever
  the bank meets it; the surface (`Resources/Shaders/Water.shader`) goes from shallow to deep, with
  glints that drift on lakes and run downstream on rivers. Walking stays tile by tile.
- **Saving a region scene** writes back what you did to data pieces in the Editor: moving one by
  whole tiles, turning a placed one, deleting one. Then it captures `Handmade` and re-exports.
  Pressing Play and **R'th > Export Map** do the same capture and export.
- **R'th > World > Convert Open Region To Data**: once, for a region made the old way. It
  lifts creatures, the spawn, objects, trees and rocks into data, keeps the rest as hand-made,
  and checks that the map, objects and creatures are exactly what they were.

## Who edits what

| Path | Owner | Agent with Unity | Agent without Unity |
|---|---|---|---|
| `world/regions/<id>.json` | yes (or in the Editor, written back on save) | yes | **yes: this is how the world changes** (except its ground: `tiles` and `terrain` are written from the terrains) |
| `client/Assets/World/Terrain/*` (the ground) | yes, with Unity's terrain tools | yes, through the Editor | **never** |
| `world/grounds.json` (the kinds of ground) | yes | yes | yes |
| `world/regions/<id>.handmade.json`, `world/snapshots/`, `world/screens/` | no (Sync writes them) | via Sync | no |
| `server/data/maps/*` | via export | via export | via `tools/world export` |
| `server/data/content/*.json` (items, objects, npcs...) | yes | yes | yes: add entries, don't reorder |
| `/shared`, `/server`, `/tools` (C#) | yes | yes | yes, with tests |
| `client/Assets/**/*.cs` | yes | yes | small changes, compiled by the agent with Unity before merge |
| `.unity`, `.prefab`, `.asset`, `.mat`, `.meta` | yes | yes, through the Editor | **never** |
| A scene's `Handmade` root | yes | yes | no: ask in the PR |
| A scene's `Data` root | only move, turn or delete pieces | the same | no (edit the data) |

## The flow

1. **One branch per piece of work**, `claude/<workstream>-<name>`, from `main`. One region per
   branch at a time: two branches changing the same region file conflict, everything else merges.
2. **An agent without Unity** changes data and code, runs `tools/world check`, `export`,
   `render` (and looks at the PNG) and `dotnet test`. If the change needs Unity (new pieces to
   see, client code, hand-made work), it says so in the PR's "Needs Unity" list.
3. **The agent with Unity** checks the branch out, runs **Sync World**, looks at the screenshots
   and in Play mode, compiles any client code, and commits what Sync and Unity wrote (scenes,
   `.meta` files, materials, handmade files, snapshots, screens, maps) to the same branch.
4. **Merge** when `dotnet test` passes and the sync report is clean. The owner (or the
   coordinating agent) merges and pushes `main`.

Several agents at once: give each its own worktree and branch, one region or one system each.
Content JSON is shared: add entries rather than move them, and merge `main` before pushing.

## A worked example: rocks and trees for Mining and Woodcutting

Workstream E1 added the tools and the nodes (`copper_seam`, `alder_tree`...) but couldn't place
them, since it had no Unity. With the world as data, placing them was a text change:

```json
    { "id": "copper-seam-8-12", "kind": "rock", "at": [8, 12], "object": "copper_seam" },
    { "id": "alder-tree-38-88", "kind": "tree", "at": [38, 88], "object": "alder_tree", "variant": "Alder" },
```

`tools/world check` then reported `server/data/maps/tolling.rthmap is out of date`; `export`
rewrote it; `dotnet test` walked every region and confirmed each new rock and tree can be stood
beside. Then Sync World drew them in the scenes and confirmed the Unity export agrees.

## What still needs Unity

- Seeing the real look: run Sync World, then the screenshots in `world/screens/` show it.
- New models and textures (there is no asset inbox yet: hand them to the agent with Unity).
- Hand-made work and terrain (`MapTerrain`, the layout bake) under `Handmade`.
- Compiling client C# (the agent with Unity compiles it before merging).

## Not built yet

- **An asset inbox** (`art-inbox/` plus a manifest), so agents without Unity can contribute models
  and textures that Sync imports.
- **More kinds of piece** as regions need them: stairs and floors as pieces (today they are
  hand-made or a `patch` with `plane` plus a `prop` with `climbTo`), terrain stamps, scatter.
- **Unity in CI** (GameCI), which needs a licence in CI: a decision for the owner.
