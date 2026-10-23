# Building the world: the reference

Read this before working on any part of the game world: the continent, a region, a town, a
road, terrain. It collects what has been decided, what is proposed, and how we work together,
and points to the documents that hold the detail. Keep it short: when a rule needs more than a
few lines, it belongs in the detailed document and gets a line here.

Each rule is marked **Decided** (by the owner; "not built" when the tools for it don't exist
yet), **Proposed** (suggested, waiting on the owner) or **Open** (a decision still to make).
Started 2026-10-23.

| Detail | Where |
| --- | --- |
| The lore and places | [lore.md](lore.md), [world-map.md](world-map.md) |
| The continent layout | [layout.md](layout.md) (`world/layout/continent.json`) |
| Sizes, terrain rules, camera | [world-scale.md](world-scale.md) |
| Art rules and budgets | [assets.md](assets.md) |
| How content is spread (banks, quests, dungeons) | [areas/rules.md](areas/rules.md), [research/world-density.md](research/world-density.md) |
| Regions as data, commands, who edits what | [workflow.md](workflow.md) |
| What is being worked on | [plan.md](plan.md) |

---

## 1. What the world is

- **Hand-crafted and fixed.** *Decided.* Every player walks the same world, and it changes only
  when we change it. Generators are scaffolding: the blockout from the layout is a sketch at real
  scale, never content. When hand work starts on a region, its generated base is frozen into the
  region's own data, so a later change to the layout or a generator never moves a finished hill,
  tree or road.
- **Plays like RuneScape 3, looks like Old School RuneScape.** *Decided.* Low-poly, few colours,
  chunky; the art rules are in [assets.md](assets.md). No Jagex assets, text or distinctive names.
- **A compressed picture of the lore.** *Decided.* As Jagex does with Gielinor, the game shows a
  scaled-down version of the world the lore describes. The lore keeps its distances ("many days
  upstream of Cruor"); the map is sized for play and travel time. Not every place in the lore
  needs to exist on the map, and a city of tens of thousands is drawn with a few dozen buildings.
- **Sparse, at RuneScape's scale.** *Decided.* A huge city may have about 30 buildings; a large
  forest about 30 trees. Trees and rocks are placed deliberately (most are resources), in small
  groups, not as scatter. Dense forest only where the owner asks for it.
  - **The yardstick:** Varrock, a Paris- or London-sized capital in its lore, has about 30
    buildings and 100 people.
  - **Every building earns its place** with a shop, a service, a quest or a landmark. Homes are
    a handful, and most are a single storey.
  - **Open space is what makes a place feel big:** wide streets, plazas, gardens and views. A
    dense grid of houses makes a place tedious to cross and impossible to fill with content.
  - **Not rectangles:** a walled city's wall follows the ground and the water, not a rectangle.
- **Everywhere has features.** *Decided* ([world-map.md](world-map.md)). There is no generic
  lowland: every stretch of country has its own landmarks and points of interest.
- **Uneven on purpose.** *Decided* ([areas/rules.md](areas/rules.md)). Targets (banks, quests,
  dungeons) describe the whole world or a realm, never one place. A village with nothing is fine;
  a world where every village has the same four services is not.
- **The camera never loses the player.** *Decided.* Anything that can come between the camera
  and the player must be able to turn transparent when it does: roofs hide, trees, walls and
  landmarks fade smoothly (not popping). Build squat and wide rather than tall, so it happens
  rarely ([world-scale.md](world-scale.md) sections 1, 4, 5).

## 2. Scale

**Units.** One tile is one Unity unit. A tick is 0.6 s; walking is 1 tile a tick, running 2. A
**zone** is 8 × 8 tiles (the server's storage and the layout's grid). A **region** is 96 × 96
tiles (one data file, one scene when it is hand-made). An **area** is 1 to 6 regions with one
identity: the unit we design in.

**The continent** ([layout.md](layout.md)): *Proposed, under the owner's review.*

| | |
| --- | --- |
| Core land (the first build) | 1.43M tiles, about twice the 2004 game's main continent; 172 regions mostly core |
| Generated regions today | 256 (the core and a ring of sea and frontier) |
| Tolling to Cruor | 368 tiles by road: 3.7 minutes walking, 1.8 running |
| Between capitals | 330-500 tiles to the nearest other capital (Varrock-Falador is about 230) |
| Settlement ladder | 4 cities, 12 towns, 29 villages, 19 hamlets |

**Settlement sizes** (from the 2004 game, [research/world-density.md](research/world-density.md)):

| Tier | Built area | Across |
| --- | --- | --- |
| City | 13-14k tiles | 150-240 |
| Town | about 3k | 70-100 |
| Village | 1-1.5k | 40-60 |
| Hamlet, farm, outpost | under 1k | |

**Land use** in the 2004 game, in 8 × 8 patches: empty 30%, sparse (1-4 trees or rocks) 36%,
moderate 31%, dense 4%, inside settlements 14%.

**Sizes of things** ([world-scale.md](world-scale.md) section 2). The player is 1.94 tiles tall.
*Open:* decisions D1-D4 (the player's size, the storey height of 1.7 against 2.3, tree footprints,
the camera profile). Until they are made, build to today's sizes and keep tall things rare and
wide.

**Terrain** ([world-scale.md](world-scale.md) section 3). *Proposed.*
- Open ground rolls gently (0.6-3.75 tiles of base noise); towns, roads and plots are flattened.
- In any 24 × 24 window (about one screen): relief normally under 3 tiles, up to 8, rarely 12,
  never over 16.
- Cliffs are 1-3 tiles and block. A mountain is raised ground with rock blocks about a storey
  high, not tall geometry.
- One sea level; rivers step down only at falls; the whole world within about 16 tiles of height.

**Services and spacing** (world-wide, [areas/rules.md](areas/rules.md)): a bank about every 80
tiles along the ways people travel, fast travel about every 150, every settlement with dungeons or
caves within a short walk. Ranges, not quotas.

## 3. From the lore to the ground

```
lore.md, world-map.md        what exists and why                     (words)
concept-map.png              where, roughly                           (a picture)
world/layout/continent.json  where, at real scale: coasts, spine,    (shapes and lines in tiles)
                             rivers, roads, realms, places
world/regions/*.json         each region, tile by tile               (data)
scenes                       how it looks                            (Unity, from the data)
server/data/maps             what the server walks on                (compiled from the data)
```

- **Coordinates** are absolute tiles: Tolling's south-west corner is (0, 0), east +x, north +y.
- **Names:** places are `p-name` until the owner names them, and a `p-` name never reaches a
  player ([world-map.md](world-map.md) for the naming style). Some overlap with RuneScape words is
  fine; R'th should feel unique as a whole.
- **Instanced places** (boss rooms, the realm of the dead) sit far off the continent. Cellars,
  caves and dungeons are their own small regions, 1024 tiles north of their surface place,
  entered by a `climb` object.

## 4. The pieces of the world

*Decided* ([workflow.md](workflow.md)). **The world is text.** Everything that makes up the world
is a data file anyone can read, diff and merge; Unity draws it, and whatever is made by hand in
Unity is written back to text.

| Piece | What it is | Edited by |
| --- | --- | --- |
| `world/layout/continent.json` | The whole continent as shapes and lines | Anyone; `tools/world layout` checks it |
| `world/regions/<id>.json` | A region: rectangle, title, materials, pieces (ground, roads, water, walls, houses, trees, rocks, objects, creatures) | Anyone; the Editor writes moves and deletions back |
| `world/regions/<id>.handmade.json` | What the scene adds by hand, as collision and placements | Sync World only |
| `world/regions/land_*.json` | Generated regions (the blockout), marked `"generated"` | `tools/world blockout` only, until taken over |
| `server/data/maps/*` | What the server loads | `tools/world export` / Sync World only |
| `client/Assets/Scenes/...` | How a region looks | Unity only |

- **Taking a region over:** delete its `"generated"` line. From then on it is edited like any
  other region, and the generator leaves it alone.
- **Terrain belongs in the region's data.** *Decided* (2026-10-23), *built.* Heights per tile
  corner, as rows of text in the region file (`terrain`), so an agent without Unity can read and
  change them and git can merge them. The owner sculpts in Unity with **Sculpt Ground**; saving
  writes the heights back. Regions drawn with `tiles` pieces follow it; the older `MapTerrain`
  (the scale test region) still keeps its heights in its scene. How: [workflow.md](workflow.md).

## 5. How a place gets made

Modelled on Albion Online's map pipeline (a Unity MMO) and Blizzard's zone workflow (section 11).
*Decided* (2026-10-23); the stage field and the coverage map are not built yet. A worked example
of the whole flow: [world-example.md](world-example.md).

**Stages.** Every region records how far it has come, and a map of the world coloured by stage
shows where we stand. Work raises an area one stage at a time, so the whole world gets a stage
before any corner is polished.

| Stage | Done when |
| --- | --- |
| 0. Layout | Generated from the layout: ground, water, roads, a square per place. |
| 1. Geography | Terrain shaped; rivers and coasts read right; walked at player height. |
| 2. Structure | Roads and paths final, settlements blocked out, landmarks placed, views decided, edges fenced. |
| 3. Life | Creatures, resources, objects, people and quest hooks, checked against the brief. |
| 4. Finish | Art, set dressing, lighting, sound. |

**Per area:**
1. **A brief** (a short page, from the area plan, [areas/rules.md](areas/rules.md)): what the area
   is for and when players come; who lives there; its palette and ground; its landmarks and where
   each is seen from; the routes through it and where they enter and leave; its resources and
   creatures by count; the checklist in section 6.
2. **A sketch** from above: start, key places, roads, high and low ground.
3. **Passes, in order:** terrain and key structures; ground painted roughly; height detail and
   roads; a basic fill of rocks and trees; resources and creatures (counted against the brief and
   checked reachable); final dressing, including scenery you can see but not reach.
4. **Connect it:** its entrances and exits meet the neighbours (section 7).
5. **Walk it at player height** in Play mode, at both camera profiles. "Get down on the player
   level and see how the space feels" (Blizzard). A stage is signed off by walking it, not by
   looking at the scene from above.

**Layout before content.** *Decided.* Don't fill a region with readables and flavour objects while
its layout, look and assets are unsettled: they get thrown away when the layout moves.

## 6. Design checklist

Use it in every brief and every review.

- **Clarity over clutter.** Albion reworked its early maps because they felt cluttered. A
  memorable theme, quick to read, beats many small things.
- **Wide and narrow.** Mix open ground with chokepoints and enclosed spaces. Blizzard scrapped a
  layout as "too big of an open area", and widened narrow canyons that had no clear exit.
- **Ground with character.** Each kind of land plays differently: mountains vertical, plains open
  with nowhere to hide, fens flat and broken by water, forests mixing rivers, lakes and hills.
- **Landmarks shape the skyline.** Each area has one or two things seen from afar. Views can
  decide placement: move what would block a famous view. Landmarks stand on open ground at least
  as wide as they are tall ([world-scale.md](world-scale.md) section 4).
- **Edges are scenery.** The edge of what can be walked is rock, water, cliffs or dense
  undergrowth that looks impassable; never an invisible wall in open ground.
- **Few ground colours per area.** Blizzard limits itself to four terrain textures per grid
  square. An area's ground uses a handful of palette colours.
- **History in the ground.** Old roads under new ones, ruins, a border stone: the land shows who
  was here.
- **Something to find.** A few optional, hidden things that reward exploring and are fine to miss.
- **Danger reads before it bites.** Harsher, darker, emptier land as an area gets more dangerous.
- **Streets and squares** at least 2 tiles wide, main ones 4; no alleys between tall buildings
  ([world-scale.md](world-scale.md) section 4).

## 7. Where places meet

- **Entrances and exits as data.** *Decided, not built.* A detailed place declares where its roads, rivers
  and paths leave it, and at what height. Its neighbours are built (or generated) to meet those
  points, as Albion joins its maps at their entrances.
- **The seam check.** *Decided; heights built.* `tools/world check` warns where two regions side
  by side disagree on the heights along their border. Still to build: walkability, roads and
  rivers that stop dead at a border.
- **A busy place next to quiet land.** The busy region settles down near its own edges (the last
  few tiles close to its neighbour's ground and height), so it meets a neighbour at a lower stage
  cleanly. The quiet region carries the road, the river and a few deliberate things: a ruin, a
  waystone, a view.
- **Roads lead.** Approaches to a place show it before you arrive: a landmark on the skyline, a
  milestone, the road getting better.

## 8. Working together

*Decided* ([workflow.md](workflow.md)).

- **One area per branch**, `claude/<workstream>-<name>`; the regions around it are read-only for
  that branch. A change needed outside goes in [plan.md](plan.md)'s Handoffs.
- **Claims** *(decided, not built):* who holds which regions is recorded in one file the tools check, as
  Blizzard's editor locks the chunks a designer has claimed.
- **Agents without Unity** change data (`world/regions`, content JSON) and C#, and check with
  `tools/world check`, `export`, `render` and `dotnet test`. They never write `.unity`, `.prefab`,
  `.asset`, `.mat` or `.meta` files.
- **The owner and the agent with Unity** sculpt terrain, do the finish stage, run Sync World,
  walk it in Play mode and commit what Unity wrote.
- **Sync World is incremental.** *Decided, built.* It touches only regions whose data, scene or
  drawing code changed since this machine last synced them; **Sync World (everything)** redoes all.
- **Don't build or recompile while someone is in Play mode**: building `/shared` or `/server`
  reloads the Editor's scripts.

## 9. Performance

- **Data costs nothing at runtime.** Players load what is built from the data, never the data
  itself. The build steps (Sync World for scenes, `tools/world export` for the server) run every
  time; there is no one-way handover to Unity, since that would lock out everyone without it.
- **What the build produces is what matters:** combine each region's static pieces into a few
  meshes per material, mark them static, draw repeated trees and rocks as instances, add simpler
  far versions only if needed. Today some kinds are wasteful (a box per road tile, a box per row
  of water).
- **Measure before optimising:** profile a walk across several streamed regions in Play mode,
  then fix what shows.

## 10. Open decisions and planned work

| | Status |
| --- | --- |
| The continent layout (size, distances, passes, the cold north): [layout.md](layout.md) section 6 | Open: owner's review |
| Sizes and camera, D1-D4: [world-scale.md](world-scale.md) | Open |
| Incremental Sync World | Built |
| Terrain heights in region data, sculpted in Unity and written back | Built (check the sculpting tool in the Editor) |
| Stages per region and the coverage map | Decided, to build |
| Entrances and exits as data; the seam check for roads, rivers and walkability | Decided, to build (the height seam check is built) |
| Claims checked by the tools | Decided, to build |
| A slope overlay while sculpting (tiles too steep to walk shown red) | Built, in Sculpt Ground |
| Combining static pieces at build time | Decided, after measuring |
| The first area brief (Tolling's neighbourhood) | Next |

`world-building.md` (the three layers: layout, bake, detail) predates the layout and the world as
data; where they disagree, this guide and [layout.md](layout.md) win.

## 11. What other games taught us

- **RuneScape** ([research/world-density.md](research/world-density.md)): the game is a compressed
  picture of its lore; the world is very uneven; banks about every 80 tiles. Jagex builds in
  64 × 64 map squares, one file each, by environment modellers (OSRS dev blogs, 2013 and 2017).
- **Albion Online** (Unity): a requirements sheet per map, a sketch, an empty template, passes
  (terrain and key structures, rough painting, height and roads, basic fill, resources by count,
  creatures, decoration including unreachable scenery), a 2D world editor joining maps at their
  entrances, then testing; about 35 hours for a small map. Its biome rework: clarity, less
  clutter, wide and narrow areas, terrain character per biome, danger shown in the look.
  ([80.lv](https://80.lv/articles/albion-online-how-to-create-a-world-for-an-online-game),
  [Pocket Tactics](https://www.pockettactics.com/albion-online/update))
- **World of Warcraft:** a top-down plan, grey blockout walked at player height, placeholder roads
  and hubs, an agreed asset list, then art; chunks claimed in the editor; a steepness overlay; four
  terrain textures per square; landmarks and protected views; history in the roads.
  ([BlizzCon 2019 notes](https://www.mmo-champion.com/content/8822-BlizzCon-2019-Designers-at-Work-World-of-Warcraft-World-Creation),
  [Artcraft: level design](http://worldofwarcraft.blizzard.com/en-us/news/16595426/artcraft-level-design-part-4))
- **Unity:** several scenes open at once is how Unity expects large streaming worlds to be built
  and edited together; scene files merge poorly, so teams avoid two people editing one scene.
  ([Multi-scene editing](https://docs.unity3d.com/6000.0/Documentation/Manual/MultiSceneEditing.html),
  [Smart merge](https://docs.unity3d.com/6000.0/Documentation/Manual/SmartMerge.html))
