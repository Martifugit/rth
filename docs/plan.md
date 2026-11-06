# R'th: the plan for what comes next

Written 2026-10-22. A working plan, split into **workstreams** that separate agents (and the owner)
can run at the same time. Each workstream says where it runs, what it depends on, which files it
owns, and when it is done. Update the status column as work lands.

## Where things stand

- **The game:** milestones 1-10 are done (see `CLAUDE.md`): the tick server, regions, items,
  skills, the town, quests, the interface, the skills rework and melee. Two regions exist,
  Tolling and Palmerway, plus the Unquiet Saint's crypt (a Religion skilling boss).
- **The lore has been rebuilt** in `docs/lore-rework/`. Decided so far: an unfinished world revised
  in ages (a thousand-year stillness, now stirring), p-spoil, death through the ferryman, the first
  gods (p-sworn, the betrayers, p-scattered, p-head, the three constants), the powers and a
  concept map. `docs/lore.md` and `docs/world-map.md` are now canon, rewritten from the rework, and the
  other docs follow them (A1). Names are being picked (A2): Lowe and Pennick so far.
- **The old area plan** (`docs/areas/skeleton.*`) is drawn on the old map and is superseded. Its
  rulebook (`docs/areas/rules.md`, `targets.json`) and research still apply.
- **Everything is on local `main`** (2026-10-23), not yet pushed: this plan and the new canon, the
  art branch, the capitals work, L2's blockout, the U2 panels and the terrain work. The old local
  branches (`claude/l2-blockout`, `claude/terrain-data`, `claude/u2-*`) are merged and can go.
- **The world has a guide:** `docs/world-guide.md` (read before any world work: scale, from lore to
  ground, how a place is made in stages, the checklist, working together) and a worked example,
  `docs/world-example.md` (the salt road). The world is hand-crafted and fixed; generated land is
  scaffolding that a region leaves when hand work starts.
- **Terrain is data** (2026-10-23): a region's `terrain` holds its heights as text, the compiler
  blocks cliffs, `TileGround` drapes over it, and **Sculpt Ground** edits it in Unity and writes it
  back (C5). Not yet tried in the Editor. The older `MapTerrain` (stamps, baked in scenes) is still
  there for the capitals. The scale decisions D1-D4 and the layout prototype's W1-W2 are still open.
- **The capital blockouts** (Cruor, Tithe, Cistern, Meridian: their builders and `docs/regions/`)
  were made on the old map. Keep the kits they use (`WorldKit.City`, `.Interiors`, `.Terraces`);
  the areas themselves are **rebuilt later**, to the new lore and through the new workflow (C3),
  not patched now.

## Rules for working in parallel

1. **One branch per workstream:** `claude/<workstream-id>-<short-name>`, from `main`. Merge
   `main` into it often. Owner reviews and merges.
2. **Stay inside your files.** Each workstream lists the files it owns. To change someone else's,
   leave a note in the **Handoffs** section at the bottom of this file instead.
3. **Shared files are edited narrowly.** Content JSON (`server/data/content/*.json`) is shared by
   many workstreams: add entries rather than reorder, change only the entries your work is about,
   and merge `main` before you push.
4. **Protocol bumps happen at merge.** If your work changes a message, bump `Protocol.Version` in
   your branch; whoever merges second re-bumps. Say so in the PR.
5. **`CLAUDE.md` is updated once per finished workstream**, in the same PR, in the milestone list.
6. **Tests pass on every push** (`dotnet test server/Rth.Tests`). Game rules get simulation tests;
   tests use `TestContent`, never the real data files.
7. **Cloud agents have no Unity Editor.** They don't hand-edit `.unity`, `.prefab`, `.asset` or
   `.meta` files. Client C# they write is compiled and checked by the local agent before merge.
8. **Lore placeholders never reach players.** `p-` names live in docs. Player-facing text keeps an
   old name until the new one is chosen.
9. **Ask the owner** before changing the architecture, and when a workstream reaches a decision
   listed under **Decisions waiting on the owner**.

## The tracks at a glance

| Track | Workstreams | Runs | Starts |
| --- | --- | --- | --- |
| **A. Lore and world design** | A1 canon, A2 names, A3 sheets, A4 area plan | cloud, owner in the loop | now |
| **L. Continent layout** | L1 rough layout at tile scale, L2 walkable blockout, L3 maps from the layout, L4 the first area by hand (Selenfall), L5 the spine's first terrain | cloud, then local | now |
| **B. Text pass** | B1 now, B2 after names | cloud | now / after A2 |
| **C. Workflow** | C0 merge art branch, C1 audit, C2 snapshots, C3 world as data, C4 migrate regions, C5 terrain and incremental sync, C6 the guide's tooling | local (Unity MCP) | now |
| **D. Death v1** | D1 server, D2 region and client, D3 the long way back | cloud, then local | now / after C3 |
| **E. Gameplay** | E1 tools, Mining, Woodcutting; E2 ranged; E3 item-on-object use | cloud (client parts checked locally) | now |
| **F. Art** | F1 scale and style applied, F2 Tolling art pass | local | after C0 |
| **U. Interface** | U1 a robust UI framework, then U2 the panels on it | local, then agents | now |
| **G. Integration** | merges, `CLAUDE.md`, this file | owner | always |

## Order and dependencies

```
Step 0: merge claude/area-plan into main (owner)  [done]

Wave 1 (now, in parallel)
  A1 canon ──────────────┬──> B1 text pass (old-lore strings that need no new names)
  A2 names ──────────────┼──> B2 text pass (church, spine people, Scalth's replacement)
  L1 rough continent layout ──> A4 area plan v2 (passes 2-7 on top of it)
  C0 art branch [done] ──> C1-C4 world as data [done] (docs/workflow.md)
  D1 death (server) ─────────────────────────────────────────────┐
  E1 tools, Mining, Woodcutting    E2 ranged    E3 item-on-object│
                                                                 v
Wave 2                                            D2 death region and client (needs C3, D1)
  A3 sheets, timeline, clocks                     F1 scale and style applied (needs C0)
Wave 3
  L2 walkable blockout of the whole continent (needs L1, C3, F1)
  L3 world map and minimap drawn from the layout (needs L1)
  Regions from A4, built the new way (needs C3, A4)   D3 the long way back   F2 Tolling art pass
```

---

## Track A: lore and world design

### A1. Canon rewrite

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | nothing |
| **Owns** | `docs/lore.md`, `docs/world-map.md`; lore notes in `docs/skills.md`, `docs/systems.md`, `docs/interface.md`, `CLAUDE.md`, `README.md`, `docs/ideas/*Unquiet Saint*` |
| **Status** | **done** (2026-10-22). `lore.md` and `world-map.md` are canon; the lore notes in the other docs follow them. |

**Done:**
- `docs/lore.md` rewritten from `docs/lore-rework/`.
- `docs/world-map.md` rewritten around the concept map, with every name sorted: kept, to be
  renamed, under review, retired, placeholders.

- The lore notes in the other docs (2026-10-23): `systems.md` (death as built), `skills.md`,
  `interface.md`, `CLAUDE.md` (the stale Tithe notes), `world-scale.md` and `areas/rules.md` (old
  realms), `world-building.md` marked partly superseded. The Unquiet Saint spec needed nothing.
- Left on purpose: the skill tiers placed in old regions in `skills.md` (the Veilwood, Candora,
  Slough...) move in A4's resources pass; `docs/regions/` and `docs/areas/decisions.md` are
  history (see Handoffs).

### A2. Names

| | |
| --- | --- |
| **Runs** | cloud proposes, owner decides |
| **Depends on** | nothing |
| **Owns** | `docs/lore-rework/names.md` (new) |
| **Status** | **in progress with the owner** (2026-10-23): the full list to name is at the top of `docs/lore-rework/names.md`; picked so far: Lowe (the realm of the dead), Pennick (the ferryman). |

- **Input:** `docs/world-map.md`'s lists (to be renamed, under review, placeholders in use).
- Propose three or four names each for: p-sworn's church; the spine's surface people; the
  realm of the dead; the ferryman; the walker; p-spoil; the gods (p-sworn, the betrayers,
  p-scattered, p-head, the three constants), so they sound like one family; the lesser occult
  houses' land; where Scalth's mites and salts now come from; the concept map's p- places.
- Follow `docs/world-map.md`'s naming style: a proper name with a meaning under it, short, easy to
  say aloud.
- **Done when** the owner has picked names and they are recorded. Then B2 and A1 apply them.

### A3. Entity sheets, timeline and clocks

| | |
| --- | --- |
| **Runs** | cloud, one subject at a time with the owner |
| **Depends on** | A2 helps but isn't required |
| **Owns** | `docs/lore-rework/entities/*.md`, `docs/lore-rework/timeline.md` |
| **Status** | not started |

- ~~Who the player is~~ Decided (2026-10-27): the stranger the bell tolled for (`docs/lore.md` VI,
  `docs/quests/the-bell.md`). Why they came back is sealed; who they were is open.
- One sheet per power, using the entity sheet in `docs/lore-rework/README.md`: the empire, House
  Gall, the lesser houses, the spine's surface people, the dwarves, Sere, the Faceless, the
  demigods' keepers, the village on p-head's brow, the frontier peoples.
- Still-open gods: the occultists' source; the range's secret (the treaty's hidden reason);
  what wounded p-sworn; the threat that makes p-scattered recombine.
- The timeline: founding events in order, checked against every sheet. Clocks: what each power
  does next if the player does nothing.
- **Done when** every power has a sheet the owner has approved, and the timeline is consistent.

### A4. Area plan v2

| | |
| --- | --- |
| **Runs** | cloud, owner reviews each pass |
| **Depends on** | L1 (pass 1 is read off the layout); names help |
| **Owns** | `docs/areas/*`, `tools/areas/*` |
| **Status** | not started |

- Pass 1 (the skeleton) is read off L1's layout: `skeleton.json` from the layout, with
  `tools/areas/plan.py`'s checks and render. Tolling stays at tile (0, 0) on the
  empire's western edge; Palmerway runs east towards Cruor.
- Then passes 2-7 as in `docs/areas/rules.md` (reasons to visit, resources, infrastructure,
  activities, quest hooks, build order), one at a time, with the owner's review after each.
- **Done when** pass 7 gives a build order of regions that track C can build.

---

## Track L: continent layout

The concept map (`docs/lore-rework/concept-map.png`) gives the shape. This track turns it into a
rough layout **at real tile scale**, then into land you can walk. The earlier "lay out the whole
continent" prompt was written for the old map; reuse its research numbers (scale, terrain,
readability), not its geography.

### L1. Rough layout at tile scale

| | |
| --- | --- |
| **Runs** | cloud, owner reviews |
| **Depends on** | the concept map and `docs/lore.md` (done) |
| **Owns** | `world/layout/*` (new), `shared/World/Layout/*` and `tools/world`'s `layout` command (instead of `tools/layout/*`), `docs/layout.md` (new) |
| **Status** | **ready for the owner's review** (2026-10-22) on `claude/l1-layout`: `world/layout/continent.json`, checked and drawn by `dotnet run --project tools/world -- layout`; 1.43M tiles of core land, 172 core regions, every check passing. The choices and the owner's decisions are in `docs/layout.md`. |

- **One layout, as data**, in absolute tiles with **Tolling's south-west corner at (0, 0)**, so the
  built regions sit inside it unchanged: Tolling on the empire's western edge, Palmerway running
  east towards Cruor.
- What it holds, roughly:
  - the coast, the sea and the islands; the frontier edges;
  - the spine as a band with its passes (where it can be crossed), running north into the cold;
  - rivers and lakes (Sere's river, the great river, the lake), and which way they flow;
  - biomes or ground kinds (farmland, forest, fen, heath, desert, volcanic, snow);
  - the powers' borders, with the empire's four salients;
  - settlements and landmarks as points, with a size tier (city, town, village, hamlet): Cruor,
    Cistern, Meridian, Frostpeak Forge, Gall's seat, the houses' places, p-head, the demigods'
    sites, Selenfall;
  - the main roads between them;
  - the **96-tile region grid** laid over it, with each region's realm and rough role.
- **Scale:** follow the research (`docs/research/world-density.md`, the art branch's scale doc if
  merged). The first build's core is about 1.2-1.5 million tiles, with frontiers beyond it.
  Distances between capitals in the range RuneScape uses (`docs/areas/scale-compare.png`).
- **Checks** (a script, like `tools/areas/plan.py`): everything reachable from Tolling over land or
  passes; travel times between capitals; region count; land per power against
  `docs/lore-rework/powers.md` (a guide); settlement ladder against `docs/areas/targets.json`.
- **A render** of the layout with the region grid and a scale bar, plus `docs/layout.md` explaining
  the data and the choices.
- **Done when** the owner approves the layout. A4's pass 1 (the skeleton) is then read off it,
  and A4 continues with passes 2-7.

### L2. Walkable blockout

| | |
| --- | --- |
| **Runs** | cloud (generator, data), local (sync and check in Unity) |
| **Depends on** | L1, C3 (the world as data), F1 (scale and style rules) |
| **Status** | **built and synced, waiting on the owner's walk** (2026-10-23, on `main`): 256 regions `world/regions/land_*.json` from the layout (`tools/world blockout`, `docs/layout.md` section 8); Sync World has made their scenes (`Assets/Scenes/Land/`). `BlockoutTests` walk from Tolling to Cruor, over every pass and into every power. Flat, no assets. |

- Generate every region of the core from the layout as sparse but real land: ground, water,
  roads, the spine's rock, a placeholder at each landmark. Tolling, Palmerway and the Saint's crypt
  keep their hand-built content.
- **Ground height:** terrain exists (`MapTerrain`; the exporter blocks cliffs, the camera lifts
  over hills; the server stays flat). What waits on the owner is how much of it to use: the scale
  decisions D1-D4 in `docs/world-scale.md`.
- **Done when** the owner can walk from Tolling to Cruor, over a pass of the spine, and into
  every power's land.

### L5. The spine's first terrain

| | |
| --- | --- |
| **Runs** | cloud (generator, data), local (sync and walk) |
| **Depends on** | L2, C5 |
| **Status** | **done in data, synced** (2026-10-23): `LandHeights` raises the generated land from continent.json's `terrain` (`docs/layout.md` section 8). The spine has foothills, a ridged crest and passes cut as climbs to a saddle, falling to 0 at flat built regions; Selenfall was refitted to meet it. To walk and judge: the salt pass west of Tolling (about x -144, y 40), Meridian's pass (about -131, 445). |

- The numbers (heights per ground, smoothing, ridge, saddle) are data, so tuning is a change to
  continent.json and `tools/world blockout`.
- Other land (volcanic Gall, the northern moors) could rise the same way, by listing its grounds.

### L4. The first area by hand: Selenfall's breach

| | |
| --- | --- |
| **Runs** | cloud (data, terrain text), local (sculpting, walking, Sync World) |
| **Depends on** | L2, C5; C6's exits and seam checks as it needs them |
| **Owns** | `land_m2_2`, `land_m2_3`, `land_m3_2`, `land_m3_3`, `land_m1_2` and the levels below, `selenfall_*` (claimed 2026-10-23; margin `land_m1_3`, `land_m1_1`, `land_m2_1`, `land_m3_1` read-only), `docs/areas/selenfall*` |
| **Status** | **stage 1 of the surface approved** (2026-10-23), after four passes in a day: a crater with a shelf round a bottomless shaft (its faces moonlight blue), a guarded gate across the shelf, and below it a cage lift down three levels to the heart, each level a region of its own (the brief's "Stage 1 log"). Lessons are in `docs/world-guide.md` section 5. Next: the lift (a fade to black), the first level, and the cut-off walls. |

- The test of the whole process in `docs/world-guide.md`, terrain included: brief, sketch, then
  stage 1 (geography: the crater, its rim and breaches, the approach from p-dry-north), walked at
  player height, before any structure or life.
- **Done when** stage 1 is walked and approved, and what the process got wrong is written back into
  the guide.

### L6. The world remade

| | |
| --- | --- |
| **Runs** | cloud or local (generator, data, client code); local (Sync World, the walk) |
| **Depends on** | L2, L5 |
| **Owns** | `docs/world-plan.md`, `world/layout/*`, `shared/World/Layout/*`, `shared/World/Ground/*`, `tools/world` (paint, probe), the painted branch of `TileGround`, `land_*` |
| **Status** | **built in data, waiting on the owner's walk** (2026-10-25, on `claude/world-remake`): the continent laid out from the land up (landforms, carved rivers and level lakes, routed roads of each power's making, settlement platforms, fields, 63 sites), and the ground painted the RuneScape way (blended underlays, crisp overlays and water with diagonal half tiles). All tests pass; the client compiles outside Unity. See Handoffs. |

- The plan and its reasons: `docs/world-plan.md`. The first layout's geography (L1) is superseded.
- Next: the owner walks it (Tolling to Cruor, the salt pass, the Scarp at the Frontline, Sere's
  river), then area briefs (A4) start from its areas, places and sites.

### L7. The world in areas

| | |
| --- | --- |
| **Runs** | everyone, one area per branch |
| **Depends on** | L6 |
| **Owns** | `world/areas.json` (each branch ticks only its own area), `docs/world-areas.md`, `docs/areas/<area>.md` briefs |
| **Status** | **started** (2026-10-25): 37 areas in five waves plus the held-back frontiers, a standard of 22 work packages over four stages, `tools/world areas` (check, progress, coverage map), **R'th > World > Open This Area**. Every area at stage 0. Next: wave 1 (Tolling, the lake road, Cruor, the western march) through stage 2. |

### L3. Maps from the layout

| | |
| --- | --- |
| **Runs** | cloud (data), local (client checks) |
| **Depends on** | L1 |
| **Status** | **world map done** on `claude/u2-world-map` (2026-10-22, with U2); the minimap is still to do |

- The world map modal and the minimap draw from the layout data, so the map in the game always
  matches the world.
- Done: the server compiles `world/layout/continent.json` at startup (`ServerConfig.LayoutDirectory`,
  `ChartLoader`) into a `WorldChart` (`shared/World/Layout/WorldChart.cs`: the zone grids run-length
  encoded, about 23 KB) and sends it in `ClientContent.Chart`, so no protocol change. The client
  paints its ground with `ChartPainter` (shared, so `dotnet run --project tools/world -- chart` draws
  the same picture to `world/renders/chart.png`). The minimap could read the same chart for what lies
  beyond the streamed regions.

---

## Track B: text pass

### B1. Old-lore strings that need no new names

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | A1 (for wording), can start in parallel |
| **Owns** | the strings listed in `docs/lore-rework/migration.md` |
| **Status** | **Done** (2026-10-22, `claude/b1-text`): Religion and Dreaming descriptions, Ambrose's covenant line, Hollis's bay line, the monument plaques, Wenna's Scalding, "basin wildlife", the world map's "The Basin" title, the hymn comments and the Religion tab. What is left is listed under B2. |

- Replace: "Tithe's prayers" (Religion's description in `skills.json`, `HymnDef.cs`,
  `ReligionSystem.cs` comments, the client's `Hymns.cs` and `ReligionPanel.cs`); Clerk Ambrose's
  covenant and bay line (`dialogues.json`); the Scalding in Corvin's and the skinning yard's
  lines; the Veilwood in Dreaming's description.
- **No ids change.** Names, examines and dialogue only, so saves need nothing.
- **Done when** tests pass and a search for Tithe, covenant, Keeping, Scalding, Veilwood and the
  bay finds nothing player-facing.

### B2. Strings that wait for names

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | A2 |
| **Status** | waiting |

- The Synod (strongbox, coins, bank notes, the watchman's sword, waystone and waygate, Clerk
  Ambrose, achievements), the Rationalists (Ranged, Artifice), Scalth's mites, lye vat, glass
  hook and salt. Scene-side text (signs, the `SynodRed` material, builders) goes to C4.
- From B1: Corvin's task line ("What I need comes from Scalth") and Wenna's "Where does the Scalth
  stuff go?" keep Scalth; the leveltest `concord_surveyor` ("a surveyor of the Concord") waits on
  the surface people's name.
- Left on purpose by B1: the old capitals' content (`tithe_*` NPCs and objects, Cruor's bay rail
  and "Keep-vigil" over the Quaere, the Concord and Orrery objects, the Lucern signposts and
  Meridian's milestone naming Tithe). None of it is placed in an exported region; it goes when
  those areas are rebuilt (C, A4), not patched now.

---

## Track C: workflow (local, with the Unity MCP)

The prompt for this track was written earlier ("reshape R'th so a local Unity agent and cloud
agents can build the world together"). Update it before use: the continent layout now comes from
`docs/lore-rework/concept-map.png` and track A4, not from a continent-layout branch.

| Id | What | Depends on | Done when |
| --- | --- | --- | --- |
| **C0** | Find and merge the world-scale, camera and art-style branch (`claude/world-scale-camera-art`), checking it in the Editor. | nothing | **Done** (2026-10-22): merged to `main`; the owner checked the new camera. |
| **C1** | Audit of how content flows. | C0 | **Folded into C3** (2026-10-22): the findings shaped `docs/workflow.md`. |
| **C2** | Snapshots: `world/snapshots/<region>.json` and `world/screens/<region>/` so agents without Unity can see regions. | C1 | **Done** (2026-10-22): Sync World writes them; `tools/world render` also draws any region from its data. |
| **C3** | World as data: regions as text (`world/regions`), a C# compiler to the server maps (`tools/world`), **Sync World** in Unity, and `docs/workflow.md` (who edits what). | C2 | **Done** (2026-10-22): E1's rocks and trees were placed in data alone, checked and exported without Unity, then synced. |
| **C4** | Migrate Tolling, Palmerway and the Saint's crypt to the new path, and carry the scene-side text from B2. | C3, B2 for names | **Migrated** (2026-10-22), with LevelTest: byte-identical maps; creatures, objects, trees, rocks and spawns are data, the rest hand-made. Left: B2's scene-side text, and removing the old builders (owner's call). |
| **C5** | Incremental Sync World and terrain as data: sync redoes only what changed (`SyncState`, per machine; **Sync World (everything)** redoes all); heights in the region's `terrain`, blocked cliffs, `TileGround` draped over them, **Sculpt Ground** with write-back, `tools/world terrain`, height seam warnings in `check`. | C3, L2 | **Done** (2026-10-23, on `main`), compile-checked outside Unity: check it in the Editor (see Handoffs). |
| **C6** | The rest of the guide's tooling (`docs/world-guide.md`): a stage per region and a coverage map, entrances and exits as data, seam checks for roads, rivers and walking, claims the tools check, static pieces combined at build time. | C5 | Decided, not built. Build each when the first test area needs it. |

---

## Track D: death v1

Design: `docs/lore-rework/death.md`.

### D1. Server and shared

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | nothing |
| **Owns** | `DeathSystem.cs`, a new ferryman system or component, `assayer.json` (replaced), `shared/Net/Messages.cs` (the death text kind), death-related tests |
| **Status** | **done** on `claude/d1-death` (2026-10-22): `FerrySystem`, `death.json`, `DeathTests`. The wake tile was a placeholder (Tolling's spawn) until D2 built the landing. |

- On death, everything the player carried and wore goes to the ferryman's hold; nothing is kept.
- The player wakes in the realm of the dead (a spawn named in data; a placeholder tile until D2
  builds the region).
- The ferryman: a coin fee from the value of what was held (rate and free threshold in data); an
  obol carried at death is taken automatically as payment (never a menu choice); unpaid gear is
  held about 24 hours of real time, then gone. Saved with the player.
- Paying returns the gear and sends the player back to their respawn point; not paying sends them
  back without it.
- Replace the Assayer's remarks with the ferryman's lines (data), keeping the death count.
- Tests: `DeathTests` for fees, obols, expiry, save and load, and that an obol in the bank does
  nothing.
- **Done when** tests pass and the old Assayer path is gone from the server.

### D2. Region and client

| | |
| --- | --- |
| **Runs** | local (region), cloud writes client code for local checking |
| **Depends on** | C3, D1 |
| **Status** | **done** on `claude/d2-realm` (2026-10-22), data and client code; needs Sync World to create `Assets/Scenes/RealmOfTheDead.unity` and a Unity compile. Region `realm_of_the_dead` ("The Landing", 32x32 at 8192,8192), wake (8208, 8206), bank object `grave_chest`; the first-death explanation is dialogue (flag `ferry_explained`); `AssayerPanel` removed (the remark is a chat line). No new interface pieces, per U1. |

- A small, very dark region: a landing, a bank and the ferryman. The first region built through
  the new data path.
- The client: replace `AssayerPanel` with the ferryman's conversation and payment, and an
  explanation the first time a player dies.

### D3. The long way back

| | |
| --- | --- |
| **Runs** | design in cloud with the owner, then build |
| **Depends on** | D2 |

- About three minutes, very hard at every level: damage scales with max blood, so progress and
  blood management matter, not stats. Success returns the gear free. The walker waits at the end.
- Design its hazards first (a short spec in `docs/lore-rework/death.md`), then build.
- **Status: done, first pass (2026-11-02).** Hazards decided with the owner: the cold, shades,
  grasping hands; lanterns; retry while held; return as the ferry does. Spec in
  `docs/lore-rework/death.md`; trials, tile triggers, patrols and percent hits as general
  mechanisms; `world/regions/realm_long_way.json`; first-pass art (style `dead`). Seen in Play.
- **Next, a cleanup pass over both dead regions (owner, 2026-11-02):** (1) the mist along the
  ground clips through it: find what draws it (most likely the flat water surface showing through
  shore corners the paint pass set below or near the waterline, since that pass dropped water's
  -0.15 height) and fix it everywhere; (2) the player must never see the end of the world: find a
  creative way to hide the region edges (an endless dark lake beyond them, fog that swallows the
  distance, far-off candle glows and silhouettes for depth, camera limits), then walk every edge.

---

## Track E: gameplay (independent of the lore)

| Id | What | Source | Notes |
| --- | --- | --- | --- |
| **E1** | **Tools for gathering, then Mining and Woodcutting** on the existing node system: tool items and tiers, tool requirements on nodes, the first rocks and trees in Tolling and Palmerway. | `docs/skills.md` 2.3, 7 | Mostly data plus a tool check in `GatheringSystem`. Tests like `GatheringTests`. **Status: done** on `claude/e1-gathering-tools` (tools, content, `ToolTests`); scene placements wait (see Handoffs). |
| **E2** | **Ranged**, the third combat style: weapons, ammunition, the auto-attack, abilities, power stats. | `docs/skills.md` 3.4 | **Status: done** (2026-11-05) on `claude/ranged-and-feel` (the quiver, calibration, the kit, `RangedTests`); see Handoffs. Tipped ammunition is still to design. |
| **E3** | **Using an item on an object or NPC** ("use"), a gap listed since milestone 6. | `CLAUDE.md` milestone 6 | A general mechanism: an interaction plus data hooks; no one-off cases. **Status: done** on `claude/e3-use` (uses.json, `UseSystem`, `UseItemMessage` at protocol 18, `UseTests`); client changes in `GameSession.cs` and `Ui/GameText.cs` to be compiled locally (see Handoffs). |

| **E4** | **The skilling loops**: make-X, failure, stations and the forge's draw, gear by tier, boosts, the gathering skills' own ways (catches, roaming spots, traps, tending, optional tools), campfires; tier 1-3 content for Smithing, Cooking, Fishing, Foraging, Medicine. | `docs/skills.md` 2.4, 7, 8 | **Status: done** on `claude/skilling-loops` (2026-10-28), server tested, client compile-checked outside Unity (see Handoffs). Protocol 20. |

Each owns its own system files and adds content entries; all of them touch `items.json` and
`objects.json`, so rule 3 applies.

---

## Track AN: Fishing in depth (trophy angling)

The design is `docs/fishing.md` (decided with the owner 2026-11-02). New systems are prefixed
`Angling` (shared types too) so they can't collide with parallel work. Branch `claude/fishing-depth`.

| Id | What | Depends on | Status |
| --- | --- | --- | --- |
| **AN1** | **The fight simulation** (shared): `AnglingFight`, integer maths on 50 ms steps, species fight patterns, the rig's stats, inputs, outcomes and what was seen; `AnglingBot` (a player that reacts to tells, for tests and pacing); `AnglingInputs` (the run-length input batches). Tests: fight lengths per size class, a bad rig loses, idle loses, replay is exact. | nothing || **done**: `AnglingFightTests`. |
| **AN2** | **Tackle**: `tackle` on items (slot, stats, bites, wear, repair); `AnglingTackleSystem` (the rig as player state and save section, "tackle" intent to rig and unrig, wear from a fight's result, mending at a tackle bench or from a tackle-maker); "tackle" state. | AN1 || **done**: `AnglingTests`. |
| **AN3** | **Trophy spots**: `angling.json` (species, size classes, rules); `AnglingSpotSystem`: each species' pool of placed spots, 2 active at a time on 20-40 min timers, per-player spooking, time-left phase, spots seen; "anglingSpots" state. | AN1 || **done**: `AnglingTests`. |
| **AN4** | **Casting and the fight on the server**: `AnglingSystem`: the `cast` action, level and lure checks, the bite, the fish rolled (weight, class, revised, seed), "anglingFight" state, the "angling" intent stepping the server's fight, the catch (trophy item, XP by size, wear), events `FishHooked` / `FishLanded` / `FishLost`; trophy items generated from species. | AN1-AN3 || **done**: `AnglingTests`. |
| **AN5** | **The fishing log**: `AnglingLogSystem`: trophies (PBs, revised), spots seen, angler's notes (moves seen, lures landed on, notes unlocked by catches or dialogue), "anglingLog" state; dialogue action `anglingNote` and condition `anglingCaught`. | AN4 || **done**: `AnglingTests`. |
| **AN6** | **Markets and the collector**: fish boxes generated per species and class, opened from the pack; the `hand-in` NPC action; the `{anglingLead:species}` token; collector trades as uses.json entries. | AN4, AN5 || **done**: `AnglingTests`. |
| **AN7** | **Client**: the reeling panel (plays `AnglingFight` live, sends input batches), trophy ripples per species and time-left, the rig panel, the log tab in the journal. | AN1-AN6; checked locally in the Editor || **built, compile-checked outside Unity**; not yet seen in the Editor (see Handoffs). |
| **AN8** | **Content, tiers 1-3**: perch, eel and pike as trophy species; tier 1-3 tackle parts and their recipes (Artifice, Smithing, Tailoring, Butchering feathers); boxes; a fishmonger, a tackle-maker and the collector, and trophy spots in `kittest` to try it all. `AnglingPaceTests` against the pace table. | AN2-AN6 || **done** (2026-11-02): `AnglingPaceTests` (about 1.3x the table). |
| **AN9** | **The ties**: phlegm potions from fish extracts (Medicine), gutting big fish (Butchering), bait for ordinary spots (`gatherSuccess`), the higher species (4-10) and their quirks as the regions arrive, Tolling's fishers' quest at Tolling's stage 3. | AN8; regions; A4 || open. Already there: the bracing tonic (phlegm) is brewed from perch scales. |

---

## Track U: the interface framework

### U1. A robust UI framework

| | |
| --- | --- |
| **Runs** | local (every fix is checked by eye in the Editor) |
| **Depends on** | nothing; **blocks U2, E2** and any new interface piece |
| **Owns** | `client/Assets/Scripts/Ui/*` (`UiRoot`, `UiWindow`, `UiPanel`, `UiKit`, `UiDefaultLayout`, `PopupMenu`), `Resources/UI/Rth.uss`, `docs/interface.md` |
| **Status** | in progress |

Audit (2026-10-22, every panel at its minimum, default and large size, 1080p and 720p): docked
windows overlap (a window that doesn't fit is pushed onto its neighbour; at 720p the default layout
overlaps itself); bank, shop and dialogue share the HUD's layer; panels overflow at their "minimum"
size (text over text, the skills footer over its tiles); nothing scrolls or wraps (the stat list
squashes, Religion cuts lines off); pack, worn and skills grids are fixed and don't grow; moving is
by the title bar and resizing from one corner, with an 8 px snap and no guides; the hover text
covers the chat title. The modals hold up.

The framework, decided with the owner:
1. **Two kinds of window.** Docked (pack, worn, skills, minimap, chat, bars, target) **never
   overlap**: a move or resize is resolved against the neighbours. Floating (modals, bank, shop,
   conversation, prompts) sit above, may overlap, open centred.
2. **Moving and resizing:** the title bar moves a window (with room kept between the tabs and
   the X); **any corner** resizes it. Snapping to a grid, screen edges and other windows' edges
   and centres, with guides drawn while dragging, and a preview of **where it will land and
   whether it will dock** (join another window as a tab) or undock. A cap on tabs per window.
3. **Two locks** (owner, 2026-11-02: they must be separate). The **window lock** (windows menu,
   Settings) stops windows being moved, resized or closed. The **action bar lock** (the toggle at
   the top right of the main bar, a bar slot's right-click, Settings) stops abilities, hymns and
   items being dragged onto, off or between bars and out of the ability book.
4. **A default layout that scales with the screen** (fits 720p to 1440p).
5. **A contract every panel follows:** content scrolls rather than overflows, text wraps, the
   minimum size is what the content needs, and one responsive slot grid (columns and slot size
   from the space) for pack, worn, skills, hymns and abilities.
6. **The right-click menu closes as soon as the pointer leaves it.**
7. **The window menu** (open/close windows) is redesigned: it is confusing today.
8. Less filler text; the hover text moves off the chat.

Placement rules (no overlap, snapping) are plain maths with tests that run without Unity, like
`CameraRules`. **Done when** every panel holds up at its smallest and largest size, docked windows
never overlap, and the owner finds laying out an interface easy.

### U2. Panels on the new framework

| | |
| --- | --- |
| **Runs** | agents (one panel each), checked locally |
| **Depends on** | U1 |

The owner's list (2026-10-22):
- **XP drops:** a piece you can place where XP gains appear as they happen: very nice to look at
  but subtle, no surface behind it, mostly the numbers coming in. *Built (`XpDropsPanel`, branch
  `claude/u2-xp-drops`); replaces the old drop beside the player's head. Check in the Editor.*
- **World map:** redo it (today's is "really, really bad"); draw it from the continent layout
  (`world/layout/`), so the map always matches the world (this is also L3). **Done** on
  `claude/u2-world-map` (2026-10-22), compiled outside Unity: check it in the Editor (see the
  handoff below).
- **Bestiary:** tabs inside, Bosses, then one per region (butchering's special creatures sit in
  their region). The **drop log hints** at what a creature can drop (its rare and unique drops),
  not only what you have had. **Built** on `claude/u2-journal` (to check in the Editor): tabs
  Bosses + one per region, carcass creatures in a section under their skill, notable drops as
  silhouettes with a rarity word until had. Notable = at most world.json `rareDropChance` (5%)
  per kill, or given by no other creature or boss and not guaranteed; worked out on the server
  (`BestiaryGuideBuilder`, "bestiaryGuide" state). See `docs/interface.md`.
- **Equipment tab:** a row of buttons at its bottom. One opens a modal-like window with your
  combat stats (today's draggable Character window goes) and your **inspect** view (what others
  see when they right-click and inspect you). **Done** on `claude/u2-character` (2026-10-22),
  compiled outside Unity; check it in the Editor. `CharacterModal` (Stats, Inspect), the button row
  (`WornPanel.Buttons`), right-click Inspect on players, and a general server question
  (`QueryMessage`, `QuerySystem`, protocol 19) that inspect is the first use of.
- **Minimap:** a fantasy RPG frame; today it looks like an old TV screen. **Done** on
  `claude/u2-minimap-guide` (compile-checked, not yet seen in the Editor): a round map in a bronze
  bezel (`Skin/minimap-bezel`, `orb`, `orb-hover`, `orb-on` from `make_skin.py`), the compass and
  clock in medallions on the ring, the six round buttons in a cradle around its bottom.
- **Guide:** rethink the skill guide's filter tabs (ALL, ABILITIES, EQUIPMENT...). **Done** on
  `claude/u2-minimap-guide`: one list in level order with a small kind badge per row, chips
  (only the kinds the skill has, with counts) to narrow it, a "Your level" line; no sort button.
- **Achievements:** the progress bar's text isn't centred on the bar and overflows it.
  **Fixed** on `claude/u2-journal` in `UiKit.Bar` (every labelled bar): the label lost the
  theme's Label margins and padding, and a labelled bar is at least 17 px tall.

## Track F: art (local)

| Id | What | Depends on |
| --- | --- | --- |
| **F1** | Apply the scale, camera and asset rules from the art branch to the existing regions. | C0 |
| **F2** | An art pass on Tolling: the first region that looks finished. | F1, C4 |

---

## Decisions waiting on the owner

| Decision | Needed by |
| --- | --- |
| Names (church, spine people, realm of the dead, ferryman, walker, p-spoil, gods, places), and which "under review" names come back | A2, B2 |
| ~~Who the player is~~ Decided 2026-10-27: the stranger the bell tolled for (`docs/lore.md` VI) | A3, the first quests |
| ~~Whether L1 extends the art branch's layout prototype or replaces it~~ L1 extended it: `world/layout/zones.json` is in its format and the bake reads it. Whether to delete the old `layout.json` and `tools/world-layout/` | L1 |
| The rough layout: size, Tolling-Cruor distance, Sere west of Tolling, the drainage, the passes, the cold north (`docs/layout.md` section 6) | L1 |
| How much terrain to use: the scale decisions D1-D4 (`docs/world-scale.md`) | the first test area |
| The coin fee's rate and free threshold; the obol's price and where it's sold | D1 (defaults in data until then) |
| The long way back's hazards | D3 |
| Each power's sheet | A3 |
| Each area-plan pass | A4 |
| The workflow design in `docs/world-data.md` before C3 builds it | C3 |

## Handoffs

Notes from one workstream to another ("A1 needs B1 to also change X"). Newest first.

| From | To | Note |
| --- | --- | --- |
| M11 (ranged and feel) | local, owner | Branch `claude/ranged-and-feel` (worktree `C:/dev/rth/.claude/worktrees/ranged-and-feel`), 2026-11-05, server tested, client compile-checked outside Unity, not seen in the Editor; not merged because `main`'s checkout had uncommitted changes (`CLAUDE.md`, scenes). **Try it** with a new character (the starter kit has a hunting sling and 60 stones): wield the sling, wield the stones (they go to the new quiver slot by the head on the worn figure), fight something from a distance; `::give alder_shortbow 1`, `::give copper_arrow 100`. Watch the Sighted status climb, Steady Aim, a Called Shot, a Pinning Shot on something that chases. Then the feel: press R mid-walk; click an item while walking elsewhere; try to log out mid-fight; Settings > Combat > Auto-retaliate while chopping a tree; right-click a pile of creatures; hover a creature for its level. Things to judge: the kit's names and numbers (provisional), whether calibration reads on the bar (it is only a status for now), the quiver's place on the figure, borrowed icons and cast animations on the ranged abilities, the logout refusal's wording. Protocol unchanged (20). |
| Wave 1 geography and levels | local, owner | On `main` (2026-11-02), data only, not yet seen in the Editor. Wave 1 now has real height, planned (`docs/world-scale.md` section 3, rewritten): the march edge (an escarpment about 6, the plateau 7-9.6, the observatory's spur about 15), Tolling's knoll (the town about 4.6 over the river), the heath ridge with the waygate in its saddle, the Edge (about 7 over Wennow; the Downs' white chalk cliff about 9 over Knellmere's shore), Cruor in three tiers (6, 10.5, 14.1) with bluffs into the river, and graded roads up every climb (switchbacks with level hairpins, cuttings, a hollow way). **Sync World (everything)** redraws all of wave 1 and its neighbours' edges. Walk from `::tele 30 48`: Palmerway's cutting from the bridge, the waygate's saddle (the lake comes into view), the salt road's hairpin up the march edge (about (-41, 55)), the observatory lane, the cave under the edge by Tolling's river (-40, 156), the lookout path up the Edge from the fishers' landing to the cairn (123, -49), p-lake-south's combe and hollow way onto the Downs, the hill figure's lip (330, -56), and Cruor from the monument up to the crown. Things to judge: cliff heights and how the faces read (rock, chalk), the road grades and hairpins, whether buildings on the knoll still sit right, the camera on the climbs. Briefs: the 'Levels' sections and latest findings in the four wave-1 briefs. |
| Tolling stage 2 | local, owner | Branch `claude/tolling-structure` (worktree `C:/dev/rth-tolling`), data and one small client change, not yet seen in the Editor. **Sync World** redraws `land_0_0`, `land_1_0`, `land_0_m1`, `land_1_m1` (and the rings of their neighbours); `PieceBuilder` now reads a piece's `offset` (Unity compiles it). Then `::tele 30 48` and walk: the square, the church close and the bell tower, the river and bridge, Widdermill (north, by Hollow lane), the skinning yard (south-east by the river), Palmerway east to the toll-house, the monument's plot and the waygate, and round Wennow to the fishers' landing (109, -30), the dry rise and the edge farm. Things to judge: Wennow's new outline, the town's size and spacing on the knoll, whether the even-footprint models (bridge, stalls, notice board, wheel-house, waygate) sit right with their offsets, the mill wheel's height (offset 1.1), building foundations on the knoll's slopes. Open: whether the waygate stays open, and the long views (`docs/areas/tolling.md` findings 16 and 18). |
| D3 | owner | On `main`. **Try it:** `::give watchmans_sword 1`, `::die`, then use the leaning stones on the Landing's south shore (the ferryman's "I'll walk back." explains it). Things to judge: how dark it is (the region's light was raised once: `atmosphere` in `realm_long_way.json`), whether it's hard enough (a perfect bot arrives with 20% blood; numbers in `trials.json`, `npcs.json` `shade`, `objects.json` `grasping_hands`/`soul_lantern`), the route's layout, and the first-pass art (the shade and walker read a bit generic; the gate is a plain trilithon). The Landing itself still uses its D2 box props. Sync World rewrote `RealmOfTheDead.unity` (it had uncommitted changes in your checkout before). The art agent's Blender run cleared your open scene; a copy was saved as `before_dead.blend` in the session scratchpad. |
| AN | local, owner | On `main`. **Reworked after the owner's first try (2026-11-02):** the reeling box and the rig panel are gone. Rods are tiered fly, spinning and ledger rods, wielded in the main hand; right-click one, **Rig**, for its rig window (drag parts from the pack onto the rod's slots). The fight is in the world (`AnglingFightView`): the fish a shape under the water, the line showing tension, tells and thrashing as water effects; two control schemes to compare in Settings > Fishing (steer with the mouse, or reel only). Trophy spots now use the fishing spots' effects. To try: **Sync World** for `kittest`, then `::tele kittest`, `::level fishing 25`, `::give alder_fly_rod 1`, `::give pelt_fly 1`, `::give reed_line 1`, `::give copper_hook 1` (or use the starter fishing rod with `::give copper_spinner 1`), right-click the rod, Rig, drag the parts on, wield it, and cast at the pond's trophy ripples. New client files (`AnglingFightView`, `RigPanel`) need their `.meta` made by Unity. The fight's look and feel are first passes to iterate on. |
| E4 | local, owner | `claude/skilling-loops`, compile-checked outside Unity, not seen in the Editor. New client files (`MakePanel`, `SkillingViews`) need their `.meta` made by Unity. Run **Sync World** for `kittest` (its skilling yard: stand-in stations from parts, fishing spots and eel weirs at the pond, a hematite crag, herbs, flax and reeds as nodes), then try with a new character (`::tele kittest`; the starter kit now has a tinderbox, a rod and a hammer): fish at the pond, light alder logs and cook, mine copper and smelt at the furnace, work the bellows and smith at the forge (the make window), pick and clean yarrow, fill a vial at the trough, mix at the bench. Things to judge: the make window's layout, the forge glow, the moved spots vanishing, the eel trap and bramble markers, the campfire. The protocol is 20: whoever merges second re-bumps. `HematiteOre` was appended to `palette.json`. |
| L6 | local, owner | `claude/world-remake`, compile-checked outside Unity, not yet seen in the Editor. Run **Sync World (everything)**: the 244 `land_*` scenes are redrawn (17 old ones are gone, new ones appear) and `TileGround` paints their ground when it loads (a texture per region, 8 pixels a tile, painted off the main thread in Play). New palette grounds were appended to `palette.json` (merge with the uncommitted metal shades on `claude/terrain-editor`). Compare with `docs/images/world-*.png` (or `tools/world paint`): the colours, the blend, the 45-degree water and road shapes, the Scarp and the spine's west face at player height. Walk Tolling to Cruor, the salt pass, the Frontline ramp. The old capital blockouts (Cruor, Tithe, Cistern, Meridian builders and scenes) are on the old map; delete them when convenient. |
| C5 | local, owner | Terrain and incremental sync, compile-checked only. The first Sync World after it redoes every region once (no record yet). Try **Sculpt Ground** on a spare `land_*` region first: select `Data > ground`, pick the tool in the Scene view (Raise, Lower, Flatten, Smooth; `[` `]` size), save the scene, and check the region file gained a `terrain` and lost `"generated"`. Placed pieces only stand on new heights after the next sync. `tools/world terrain <region> layout` is rough (it cut cliffs across the salt pass); `flat` is the safer start. |
| L2 | local, owner | *Synced (2026-10-23).* Was: run **Sync World (no screenshots)** (or `tools/unity-sync.ps1 -rthNoScreens`): it creates 256 scenes under `Assets/Scenes/Land/`, adds them to the build and checks each scene exports the same map. Generated regions skip screenshots in any case. New client code: `TileGround` (`Scripts/View`, builds each region's ground mesh and collider from its rows on load, never saved in the scene), its case in `PieceBuilder`, and `RegionStreamer` announcing a title only when it changes. Then walk from Tolling west over the salt pass (about x -144, y 40) or east to Cruor (`::tele 384 216` lands on its square). Things to judge: the 1.6 m rock shelf of the crest, the dark frontier shelf, road widths, how the warped coasts read. 25 new `Land*` materials appear under `Assets/Art/Materials`. |
| U2 (world map) | local, owner | `claude/u2-world-map`, compiled outside Unity; look at it in the Editor: M opens it centred on you at a tile a pixel; drag to pan, wheel or +/- to zoom (whole map, 0.75, 1, 1.5, 2, 3 px a tile); the compass button re-centres; the book button shows the legend and the toggles (place names, realm names, shade unexplored). The ground appears as a coarse draft first, then sharpens (painted on a worker thread; the Editor's Debug build of `Rth.Shared` may take a few seconds for the full picture). Things to judge: colours of the painting against the interface, label sizes, the unexplored shade's strength (`MapInk.Veil`), road widths. Places named `p-...` show as unnamed marks; `p-` landmarks are left off (the hidden occult cells among them); `"hidden": true` on a place in `continent.json` keeps any place off the map. |
| D2 | D3, U1 | The landing is `world/regions/realm_of_the_dead.json` (x 8192-8223, y 8192-8223), ringed by water; the long way back can start from its south or east shore (extend the region or add one beside it). Death has no window of its own now: the remark is a chat line (`chat-line--ferryman`) and the ferryman talks through the dialogue box. If U1 wants a death screen (or a richer payment view), it is new interface work. |
| E3 | U1, local | Use mode touches `client/Assets/Scripts/GameSession.cs` (use state, `UseOptions`, Escape/right-click) and, narrowly, U1's `Ui/GameText.cs` (`DefaultAction` and `ItemOptions` route clicks to the picked-up item). Compiled outside Unity against 6000.6.6f1's assemblies; check it in the Editor. U1 may want a highlight on the picked-up slot (`session.UseSlot`) and a "use" cursor. Plain items now left-click to Use (it is first in `ItemActions`, before Drop). |
| L1 | A4 | Pass 1 can be read off `world/layout/continent.json` (places, tiers, realms) and `world/layout/regions.txt` (every 96-tile region's realm and rough role). `p-` places there are suggestions; rename or drop freely. |
| L1 | L2, L3 | The layout's data: `zones.json` (a `WorldLayout`: ground and height per zone), and in `continent.json` the rivers, roads and passes as lines in tiles. Load it with `LayoutWorkspace.Load` / `ContinentCompiler.Compile` (`shared/World/Layout`). |
| L1 | local agent | `LayoutBaker` (client) now reads `world/layout/zones.json`, falling back to `Assets/World/layout.json`: compile it in the Editor. Its palette names were checked against `palette.json`. |
| L1 | owner, C | `docs/workflow.md`'s command list and `docs/world-building.md` (W1, W2, the layout section) could point at `docs/layout.md`: W2 is answered by the layout, W1 becomes its `scale`. Not edited here (not L1's files). |
| D1 | D2 | The ferryman is NPC `ferryman` (npcs.json, Talk-to opens dialogue `ferryman`); place him on the landing and set death.json `wake` to it. On waking the server opens his conversation with no NPC (`DialogueSystem.Start(player, id, speaker)`), so today's dialogue box already works. The death remark is `TextKind.Ferryman` (was `Assayer`, same value 2, no protocol bump); `AssayerPanel` still shows it. |
| E1 | C, L2 | Done in C3: the proposed rocks and trees are pieces in `world/regions/tolling.json` and `palmerway.json`. |
| A1 | L1, owner | `docs/world-building.md` (decision W2) and `docs/world-scale.md` still place Tolling and the rivers against the old map's bay and the Cradle, which are retired. Restate W2 against the new layout when L1 has one. |
| A1 | C, A4 | `docs/regions/` (the Cruor, Tithe, Cistern and Meridian blockouts) still describe Tithe, the bay, the Quaere and the Plaza of the Keeping as current. Left as is, since the areas are rebuilt rather than patched; give each a superseded line when that work starts. `docs/areas/decisions.md` also mentions the bay. |
| G | C, A4 | The capital blockouts on `main` are on the old map. Keep the `WorldKit` kits; rebuild the areas once A4 and C3 allow, not before. |
| A1 | A4 | `docs/skills.md` places skill tiers in old regions (the Veilwood, Candora, Slough...), and `docs/areas/rules.md` lists the old realms in its definitions. Both move with the new map: the tiers in pass 3 (resources), the realm list in pass 1. |
| A1 | B2 | `docs/world-map.md` lists the names to be replaced in the game (the Synod, the Rationalists, Scalth). |
