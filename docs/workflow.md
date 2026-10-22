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
| `wall`, `fence` | Along a grid line `from`-`to`, open at `gaps` | blocks edges |
| `house` | Floor, four walls, a `door` (N/E/S/W) at `doorAt`, a roof that hides inside | ground, walls |
| `blocker` | An invisible barrier over `rect` | blocks |
| `tree`, `rock` | On tile `at`; look from `parts`, or grown from the seed | blocks its tile |
| `prop` | Anything on `at`: `parts` (boxes, cylinders) or a `model` prefab | `footprint` (objects: own tile) |
| `npc` | A creature or person (`npc`, `wander`) living at `at` | none |
| `spawn` | Where new players appear | none |

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
dotnet test                                   # includes the same check (WorldDataTests)
```

`check` fails when a piece is broken, names an object or creature the content doesn't have,
regions overlap, or `server/data/maps` is out of date with the data. `render` draws a region from
above (ground colours, blocked tiles darker, walls as lines, trees, rocks, objects cyan, creatures
orange, the spawn magenta, the 8-tile zone grid) and as text with coordinates; open the PNG to look.

With Unity (the owner, or the agent with Unity):

- **R'th > World > Sync World** (or `tools/unity-sync.ps1` with the Editor closed): for every
  region, redraw `Data` from the data, capture `Handmade`, export through the C# compiler, check
  that exporting the whole scene the Unity way gives the same map, and write the snapshot and
  screenshots. **Sync World (no screenshots)** is quicker.
- **Saving a region scene** writes back what you did to data pieces in the Editor: moving one by
  whole tiles, turning a placed one, deleting one. Then it captures `Handmade` and re-exports.
  Pressing Play and **R'th > Export Map** do the same capture and export.
- **R'th > World > Convert Open Region To Data**: once, for a region made the old way. It
  lifts creatures, the spawn, objects, trees and rocks into data, keeps the rest as hand-made,
  and checks that the map, objects and creatures are exactly what they were.

## Who edits what

| Path | Owner | Agent with Unity | Agent without Unity |
|---|---|---|---|
| `world/regions/<id>.json` | yes (or in the Editor, written back on save) | yes | **yes: this is how the world changes** |
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
