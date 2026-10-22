# R'th: project guide for Claude Code

A private, non-commercial multiplayer RPG for an invited group of friends (at most ~200 players).
It plays like **RuneScape 3** (Evolution of Combat abilities, the skilling loops, a sandbox with no
forced faction) and looks like **Old School RuneScape** (low-poly models, chunky flat textures,
classic layout). It is set in its own world, **R'th**, built around the four humours.

**Status:** Milestones 1 (architecture), 2 (combat core: Occultism, bile, phlegm, breath),
3 (items, equipment, consumables, loot), 4 (skills, workstations, carcass butchering) and
5 (townsfolk: dialogue, bank, shops, tasks; Distillation and reagents), 6 (quests), 7 (a
multi-region world streamed by the client), 8 (the window-based interface, chat), 9 (the
skills rework: Religion's hymns replace breath, the v1 skill roster) and 10 (melee: weapon
styles, melee abilities, Cruor's verses, gathering nodes and the first monument) are done, and
death v1 (D1 and D2 in `docs/plan.md`: the ferryman holds what you die with, on a dark landing
built as region data) and workstreams E1 (gathering tools, the first Mining and Woodcutting) and
E3 (using an item on an object, an NPC or another item) have landed. The v1 skills are
designed in `docs/skills.md`; most of them exist only as a skill and a level so far.

**Build everything as extensible systems.** New features plug into the mechanisms under
"Extending the server" below (systems, events, interactions, stats, item components, effect ops,
save sections, data files) rather than adding special cases. Prefer a general mechanism plus
data over code for one case.
See `README.md` for how to build, run and test.

Design docs:
- `docs/lore.md`: **the canon** for the world, its powers, death and characters (rewritten
  2026-10-22 from `docs/lore-rework/`). Place names are listed in `docs/world-map.md`. The game
  still carries some old lore (Tithe's hymns, the Assayer, Scalth); `docs/plan.md` says what
  replaces it and when.
- `docs/systems.md`: combat resources and the rules of a fight.
- `docs/skills.md`: the v1 skill roster, the rules every skill follows, and each skill in full.
- `docs/world-scale.md`: sizes, terrain, town and camera rules from the measured 2004 game
  (`docs/research/`); decisions D1-D4 wait on the owner and the scale test region.
- `docs/assets.md`: triangle budgets, the palette, textures, leafy trees, recolouring.
- `docs/world-building.md`: the world layout, region bakes and stamps (a prototype; W1, W2 open).
- `docs/workflow.md`: how the owner and agents (with and without Unity) build the world together, as data.
- `docs/areas/`: the area plan for the whole continent, built in passes. Start with `rules.md` (how the research targets are used); the evidence is in `docs/research/`.

---

## Architecture (decided)

**A standalone C# server owns the game. Unity is only the client.**

```
/server     Plain C# console app (.NET, current LTS). Runs the tick loop, persistence, networking.
  Rth.Server.Core   The server itself (netstandard2.1, so the Unity Editor can host it in-process).
  Rth.Server        Thin console host (net10.0).
  Rth.Tests         xUnit tests for /shared and the server; run without Unity.
  data/maps         Exported regions (*.rthmap + sidecars). Written only by the exporter.
/shared     C# class library with NO UnityEngine references. Game rules, definitions,
            message types, map/tile data structures. Used by both server and client.
/client     Unity project (Unity 6.6, URP). Rendering, input, UI, animation. Sends intents, draws state.
/tools      Small utilities. unity-map-tools: a local UPM package (map markers + exporter).
/docs       Design documents.
```

Build output goes to `/artifacts`. Building `Rth.Shared` and `Rth.Server.Core` copies their DLLs into
`client/Assets/Plugins/Rth/` (git-ignored), so **run `dotnet build` before opening Unity** and after
changing `/shared` or `/server`. Unity loads the DLLs; it never compiles that source itself.

Why: the game rules are tick-and-tile maths with no physics, so the server has no reason to carry
Unity. A plain C# server is light enough to run 24/7 on a home PC or a free/cheap ARM machine, and
its logic can be unit-tested in milliseconds without the Editor.

### Rules that follow from this

- **The server is authoritative for everything.** The client sends intents ("walk to tile x,y",
  "use ability 3", "click rock 1234") and never decides outcomes. This is for consistency and
  persistence, not anti-cheat: players are trusted friends, so do **not** build anti-cheat,
  anti-bot, rate limiting beyond basic sanity, or sharding.
- **Game logic never lives in Unity MonoBehaviours.** If it changes game state, it belongs in
  `/shared` or `/server`.
- **`/shared` must compile for Unity.** Target `netstandard2.1` and a C# language version Unity
  supports (check the installed Unity version's API compatibility level before using newer
  language features). No `UnityEngine` references in `/shared` or `/server`.
- **Dev convenience: the server can run inside the Unity Editor.** During development, pressing
  Play should start the server core on a background thread in the same process and connect the
  client to localhost. The same server core also runs as the standalone console app for real play.
  Keep Unity API calls on the main thread; the server core must not touch Unity objects.

### Tick loop

A fixed **600 ms tick**. Everything (movement, combat, skilling, regeneration, timers) advances
in whole ticks. Each tick runs fixed phases in order, modelled on the Lost City engine:

1. world/global timers
2. read client input (queued since last tick)
3. NPC processing
4. player processing (queues, actions, combat, movement)
5. logouts, then logins
6. zone/ground-item updates
7. send each client what changed **near them**
8. cleanup

Correct for drift so ticks stay on a 600 ms grid. Allow a faster tick rate in tests and debug.

### World model

- Tile grid, click-to-move, server-side pathfinding. Walking is 1 tile per tick, running 2.
- Each client only receives entities within a view radius ("interest management").
- **Height and floors (decided):** the server is flat; the client draws height by casting down onto the ground colliders of the entity's floor. A floor above the ground is a plane (`MapLevel`, up to 4), with its own collision, and stairs and ladders move you between planes (`ClimbSystem`). `Assets/Scenes/LevelTest.unity` (`::tele leveltest`) is the worked example.
- **Map pipeline (decided, the world as data):** each region is text, `world/regions/<id>.json`
  (pieces: ground, roads, water, walls, fences, houses, trees, rocks, props, objects, creatures,
  the spawn), plus what its scene adds by hand, captured into `<id>.handmade.json`. A plain C#
  compiler (`shared/World/Data`, run by `tools/world`) turns both into the server's
  `server/data/maps` files, so anyone can change and check the world without Unity. Unity draws
  the data under each scene's `Data` root (**Sync World**), keeps hand work under `Handmade`, and
  checks its own export agrees. Never edit `server/data/maps` or the handmade files by hand. See
  `docs/workflow.md`.
- **Regions (decided, Milestone 7):** the world is one absolute tile grid (Tolling's south-west
  corner is (0, 0); coordinates may go negative). It is built from **regions**, each one Unity
  scene with one `MapArea`, zone-aligned (origin and size multiples of 8) and not overlapping.
  Modelled on Lost City: the server loads **all** regions at startup into a sparse `WorldMap`
  stored per 8x8 zone (missing zones are blocked, so empty sea or unbuilt land costs nothing),
  and keeps a per-zone entity index (`World.Zones`) for "who is near". The client streams region
  scenes additively around the player (`RegionStreamer`: load within 48 tiles, unload beyond
  72); the camera, light, fog and HUD live in `Game.unity`. Most regions between places should
  be sparse (a road, a few trees, a landmark), at RuneScape's compressed scale.

### Networking

Start simple: TCP with length-prefixed binary messages, with message types defined in `/shared`.
The client interpolates smoothly between tick snapshots so movement doesn't look steppy.
Handle reconnects cleanly. A library such as LiteNetLib is fine if it saves real work, but don't
add complexity before Milestone 1 proves the need.

### Persistence

Players are saved on logout (including dropped connections), on autosave every 300 ticks
(3 minutes) and on server shutdown. The world state that must persist (e.g. player-built or
long-lived changes) gets designed when such features exist.

**Decided (Milestone 1):** saves are one JSON file per player in `server/saves/` (`JsonPlayerStore`,
written atomically through a temp file), not SQLite. SQLite needs a native library, which is
awkward inside the in-Editor server, and ~200 small files don't need a database. Everything goes
through `IPlayerStore`, so a SQLite store can replace it later without touching the world. The
Editor-hosted and standalone servers share the same save folder.

### Content as data

Items, NPCs, drop tables, skills and abilities should be data files (e.g. JSON) loaded by the
server, with hot-reload in development, so tuning doesn't need a restart. Code handles the
mechanics; data handles the content.

---

## Milestone 1: prove the architecture

Build only this, and make it feel good:

- [x] Repo layout as above; `/shared` builds for both the server and Unity.
- [x] Server runs a drift-corrected 600 ms tick loop with the phases above.
- [x] One small test map designed in Unity, exported by the tool, loaded by the server.
- [x] Two players can log in (name only, no passwords; it's friends-only, add auth later if wanted).
- [x] Click a tile: the server pathfinds and the player walks there; walls block movement.
- [x] Both players see each other move, with smooth interpolation between ticks.
- [x] Log out and back in: the player is where they left off (persistence).
- [x] Pressing Play in the Editor starts an in-process server automatically; the standalone
      server also works.
- [x] Unit tests for pathfinding, tick timing and save/load run without Unity.

How it works, briefly:
- **Map pipeline.** Scenes mark collision with components from `tools/unity-map-tools`: `MapArea`
  (the exported rectangle; 1 Unity unit = 1 tile, tile (x,y) covers world X [x,x+1], Z [y,y+1]),
  `MapBlocker` (tiles under its colliders are blocked), `MapWall` (thin colliders snapped to grid
  lines block tile edges) and `MapSpawn`. A tile with no ground collider is blocked. Use
  **R'th > Export Map**; pressing Play also re-exports the open scene's map automatically.
  The exporter is the only thing that writes `server/data/maps/*.rthmap`.
- **Protocol.** `shared/Net/Messages.cs`. Bump `Protocol.Version` whenever a message layout
  changes. The server sends every client a `WorldUpdateMessage` every tick, with entities added,
  moved (with each step, for interpolation) and removed within `ViewRadius` (15 tiles).
- **Editor server.** `client/Assets/Editor/LocalServerLauncher.cs` starts `ServerHost` on Play
  (toggle under the R'th menu). If the port is taken, the client uses whatever server is there.
  The project has domain reload on Play turned off, so client code must not rely on statics
  being reset between Play sessions.
- **Scenes.** `Game.unity` (first in the build) holds the camera, sun, fog, an "unbuilt land"
  plane under the world, and the game objects; **R'th > Dev > Rebuild Game Scene**. Region
  scenes hold only their map: `Tolling.unity` (Phase 1 of `docs/world-map.md`), `Palmerway.unity`,
  `SaintsCrypt.unity` and `LevelTest.unity`, each drawn from `world/regions/<id>.json` by Sync World
  (`docs/workflow.md`), with what the first builder scripts made kept under its `Handmade` root
  (those builders are gone). `WorldKit` (houses with hideable roofs, water from tile sets, roads,
  fences, trees) draws the data's pieces. Pressing Play in a region scene loads the Game scene
  beside it (`GameBootstrap`).
  **R'th > World > Open Whole World** opens every exported region (plus the Game scene's sun and
  fog) side by side for editing across borders; each scene still saves to its own file, and
  Play and Export Map handle every open region. Exporting a
  region writes `<name>.rthmap`, `.region.json` (scene, title, spawn), `.spawns.json` and
  `.objects.json`, and adds the scene to the build so the client can stream it. Saves hold
  absolute positions; a save whose tile is no longer open ground starts at the spawn.

## Milestone 2: the combat core (done)

One style, **Occultism**, because its bile axis is the riskiest and most unusual system. Blood,
phlegm and breath came with it. The prototype numbers are in `docs/systems.md`.

- [x] Blood, phlegm (0-100), bile (-100..+100) and breath on every player, saved with them.
- [x] Occult auto-attack plus 8 abilities (builders, spenders, ultimates) across both poles,
      with phlegm and bile requirements, a 3-tick global cooldown and an ability queue.
- [x] Later: stalling (use out of range, fires on arrival; `keepsStall` abilities hold it),
      optional queueing and Revolution (Settings > Combat; see `docs/systems.md`).
- [x] Bile statuses (any lean: infused + choleric, or warding + melancholic, scaling with bile),
      settling out of combat. Buff and debuff bars on the client.
- [x] The target is kept while walking away in combat; an ability takes the fight back up.
- [x] Breath deflection against one style.
- [x] NPCs placed in the scene (`MapNpcSpawn`), with chase, melee/ranged rules, line of sight,
      aggression, retaliation, leashing, death and respawn.
- [x] Death; respawn with balanced humours. (The Assayer was replaced by the ferryman in D1.)
- [x] Content (abilities, NPCs, rules, death lines) as JSON in `server/data/content/`, hot-reloaded.
- [x] Client: click to attack, 1-8 action bar, resource bars and bile axis, hitsplats, projectiles.
- [x] Simulation tests (`CombatTests`), including 1000 ticks of fighting.

Notes: players only fight NPCs (no PvP). Damage lands on the tick it's used (no projectile delay
yet). There are no levels yet; base `PlayerAbilityDamage` plus gear stands in. The client HUD is
IMGUI (`CombatHud`), a placeholder until the classic-layout interface work.

## Milestone 3: items (done)

- [x] Item definitions with components (equipment, consumable), validated content.
- [x] 28-slot inventory and 10 equipment slots, saved per player; starter kit for new characters.
- [x] Consumables (food, potions) via generic effect ops; shared cooldown groups; eating delays attacks.
- [x] Equipment adds to named stats, which combat reads.
- [x] Nested, shareable drop tables; loot on the ground for the killer, then public, then gone.
- [x] Pick up (walk over), drop, examine, wield/remove; inventory drag to rearrange.
- [x] Client: pack/worn/stats panel, right-click menus in the world and on items, hover text.

Items are kept on death for now. Items have placeholder icons (tinted squares) until there's art.

## Milestone 4: skills and carcass butchering (done)

- [x] Skills with XP and levels (skills.json): RuneScape's XP formula run to level 100 (a skill
      may cap lower with `maxLevel`; 200M XP max), level-up event and
      message, stats per level, XP from damage by combat style.
- [x] "Needs level N" requirements on abilities (spenders occultism 5, ultimates 10),
      equipment and recipes.
- [x] Interactive world objects placed in the scene (`MapObject`), exported to
      `<map>.objects.json`; right-click/left-click them in the client.
- [x] Recipes at stations (recipes.json): semi-AFK processing with inputs, tools, outputs, an
      output table with extra rolls per levels, XP.
- [x] Carcass butchering: creatures drop carcasses; their loot comes from butchering them.
      Fen rats: skinning rack + skinning knife (starter kit). Vitriol mites: lye vat + glass
      flensing hook (an uncommon rat yield), butchering 10.
- [x] Client: Skills tab with levels and progress; locked abilities grey out.

Not yet: butchering task-givers and their points, the other four creature tiers, banks to
stockpile carcasses.

## Milestone 5: the town (done)

- [x] NPC menus (talk, bank, trade) from data; townsfolk can't be attacked; actions can be
      flag-gated with a locked message (objects too).
- [x] Data-driven dialogue (dialogues.json): branches, conditional options, actions, {tokens}.
- [x] Bank (400 slots, everything stacks), from a strongbox, a banker or dialogue.
- [x] Shops with shared, restocking stock, priced in items or points, optional buy-back.
- [x] Points pools and task-givers: tasks tracked by events, streak bonuses, cancel costs.
- [x] Distillation at Corvin's (secret) still. (Abilities no longer burn its reagents: they cost
      only phlegm and bile.)
- [x] Townsfolk: Clerk Ambrose (Synod bank), Corvin Sallow (apothecary, back room, Scald tasks),
      Wenna Thole (skinning yard tasks and token shop).
- [x] Client: dialogue box (number keys / space), bank and shop windows, pack clicks deposit or
      sell while those are open, task and points in the Stats tab.

## Milestone 6: quests (done)

- [x] `QuestSystem` (quests.json): stages with journal text; stage objectives (kill, recipe,
      item) that auto-advance; rewards (XP, items); level requirements; saved per player;
      `QuestStageChanged` event. Dialogue conditions `questStage`/`questStarted`/`questComplete`,
      actions `startQuest`/`setQuestStage`/`completeQuest`, token `{questProgress:id}`.
- [x] Client Quests tab (red / yellow / green, hover for the journal entry).
- [x] First quest, **Dreamless**: Edda Marsh sleepwalks by the pond; Corvin brews her a
      dreamless draught from rat galls and a saltfig; she says something she shouldn't know.
      Touches Corvin's curiosity and, in the canon, the Dreaming and the nightmare's thread
      (`docs/lore.md` II, V); it was planned against the old Assayer and Dreamweaver threads,
      which its text never names.

Not yet: item-on-object/item-on-NPC "use", noted items, the other butchering tiers, a larger map.

## Milestone 7: a world of regions (done)

- [x] `WorldMap`: every exported region stitched into one sparse, zone-stored collision map at
      startup; overlap and alignment checked; paths and line of sight cross borders.
- [x] `World.Zones`: per-zone entity index, rebuilt each tick, used for visibility.
- [x] `RegionsMessage` at login (protocol 7); the client streams region scenes in and out with
      hysteresis, shows a loading screen until the ground under the player exists, and
      announces each region's title on entering.
- [x] Game scene split from region scenes; Play from any region scene; unbuilt land drawn to
      the fog.
- [x] Second region, **the Pilgrim Road** (east of Tolling): sparse heath, the Wash's shore, a
      waystone and the barred waygate. Object actions with a `message` need no code (signs).

Not yet: teleports and the client's handling of long jumps, multiple planes in practice, a
region-level "music/area name" finer than a whole region, world-map/minimap.

## Milestone 8: the interface (done)

Movable windows of panels, fixed-size bars and server windows, and tabbed modals, in a modern
dark-fantasy style; see `docs/interface.md` (its "Window kinds" table says which is which).

- [x] UI Toolkit runtime interface (`client/Assets/Scripts/Ui/`): `UiRoot` (document, layout,
      hotkeys, pointer-over-UI and typing checks), `UiWindow` (drag with snapping, resize,
      tabs that pull out into their own window or drop onto another, close), `UiPanel` (one
      class per interface, found by reflection), `UiDefaultLayout` (the classic arrangement).
      Layout saved in PlayerPrefs; lock, scale and reset in Settings. Theme in
      `Resources/UI/Rth.uss`.
- [x] Panels: action bar with blood/phlegm/breath/bile, target, minimap (turns with the camera,
      dots, click to walk, compass, run, world map, settings, every-panel menu, clock), pack,
      worn + bonuses, ability book, focuses (deflection), skills (total level, targets),
      quests, task, XP metrics, chat, world map (regions to scale), settings.
- [x] Public chat: `ChatSystem` (protocol 8), overhead text, Enter to type.
- [x] Look: dark-fantasy theme (skin textures from `tools/ui-skin`), Cinzel and Alegreya Sans
      (OFL), game-icons.net silhouettes tinted in code (CC BY 3.0, `CREDITS.md`,
      `tools/icons`). Fixed-size bars, minimap, chat, bank, shop and conversation.
- [x] Action bars: a main bar (blood, phlegm and breath gauges, the bile axis, 14 slots on
      1-0 - =) and two secondary bars; abilities, focuses and items dragged onto them; radial
      cooldowns; lockable; layouts kept on the server (`ClientStateSystem`, "client" state).
      One drag-and-drop system for all slots. Skill targets (level or XP) are client state too.
- [x] Modals (`UiModal`, tabs, Escape): journal (quests in full, achievement diary, bestiary),
      guide (skill unlocks gathered from content, ability reference), world map, settings.
- [x] Bank: pack and worn tabs beside it, quantity buttons with withdraw-X, bank notes
      (generated for every unstackable item), deposit worn items.
- [x] Conversation in two parts (message, then reply). Server windows stay closed when closed.
- [x] Server: `AchievementSystem` (achievements.json; objectives counted from events),
      `BestiarySystem` (kills, drops and carcass yields per creature), `RegionSystem`,
      `ClientStateSystem`; events `ItemsObtained`, `ObjectUsed`, `NpcActionUsed`,
      `RegionEntered`; quests send their whole story (protocol 9).

Not yet: run energy, friends/group/private chat, announcements, player examine, waystones on
the world map, item art, world labels and the login box (still IMGUI).

## Milestone 9: the skills rework (done)

The design pass on every v1 skill (`docs/skills.md`), and the code changes it called for.

- [x] Breath, focuses and breath-based deflection removed. **Religion's hymns** (hymns.json,
      prayers for every style, still "Tithe's" in flavour text until the text pass; in the canon
      they are sung by p-sworn's church) drain **devotion**: 10 a Religion level, no natural
      regeneration; altars (the "pray" object action, on the waystone for now), devotion
      draughts (`restoreDevotion`, `restoreDevotionPercent`) and respawning restore it.
- [x] A hymn is only stats, so deflection, per-style boosts and life steal became stats that
      combat reads (`deflectMelee`, `occultAccuracy`, `meleeDamage`, `lifesteal`...). One
      overhead at a time; a hymn replaces the others in its `line`. Singing in combat trains
      Religion a little. `ReligionSystem` owns all of it (protocol 12: `SetHymnMessage`, devotion
      and sung hymns in the player state).
- [x] The 16 v1 skills in skills.json. **Distillation retired into Occultism** (`retired` in
      skills.json moves a save's XP on login). Defence raises max blood (90 a level) and takes a
      share of combat XP by the **Train defence** setting; combat level from a data formula
      (`combatLevel`).
- [x] Client: a Religion tab (F) with devotion, hymns, and a place for verses; hymn slots on
      the bars (old focus slots become the matching hymns); devotion on the main bar; combat
      level in the skills footer; icons for the hymns and skills.
- [x] Place names in the game: Palmerway (was the Pilgrim Road: scene, builder, export, flag
      `palmerway_open`), Widdermill, Wennow, Scalth.

Not yet: verses, pledges, scriptures and monuments (they come with melee); the melee and ranged
styles; the gathering and making skills' content; a gear sink; the Turmoil-type hymns' drain on
the target.

## Milestone 10: melee (done)

The second combat style, and the Religion half that powers it (`docs/skills.md` 3.3, 4.2-4.4).

- [x] Weapons have a combat style (`equipment.style`) and may name their own auto-attack; the
      auto-attack follows the weapon (combat.json `autoAttacks` per style), and abilities need
      a weapon of their style. Empty hands fight with the unarmed auto-attack's style (occult).
- [x] Eight melee abilities and a melee auto-attack (provisional names), the watchman's sword
      (starter kit) and the penitent's mace. Per-style power stats (`meleePower`,
      `occultPower`) from each style's levels and gear.
- [x] Ability data, for every style: blood costs, bleeds on the target, a low-blood damage
      bonus, timed buffs (`buff`: stats, a status, optionally only against a tag or below a
      blood line, or ending on the next hit), XP per use.
- [x] Cruor's seven verses as abilities of type `Verse` (used on yourself on the global
      cooldown, blood and scriptures, Religion XP), driven by new stats that combat reads:
      `deferDamage`, `blockHit`, `blockedPhlegm`, `scarOnHit`, `builderPhlegm`,
      `bloodPerAbility`. Bleeding and scar statuses; `HitKind.Bleed` (protocol 13).
- [x] Gathering nodes (`node` on an object type, the "gather" action, `GatheringSystem`):
      attempts by level, XP, items, extras, points, shared depletion, surges. Clients get
      nearby depleted or surging nodes as the "nodes" server state (`NodeViews` draws them).
- [x] Cruor's monument by the road in Palmerway: four founts (Religion, AFK, the cut and
      "Ouch!"), hungry founts, pledges; Brother Hollis trades scriptures for pledges.
- [x] Client: ability book tabs per style, verses in the Religion panel, tooltips for the new
      fields, bleed hitsplats, icons.
- [x] Tests: `MeleeTests` (every verse, bleeds, weapons, 1000 ticks of the shipped kit) and
      `GatheringTests`.

Not yet: weapon models in the hand, one- and two-handed kinds, shields and dual wield,
raising monuments and reliquaries, final ability names.

**World building (in progress):** the capitals blocked out as regions, one builder each, with an
asset list for the art pass. All four capitals are done: Cruor, Tithe, Cistern and Meridian (`docs/regions/`), on the old
map (Tithe is retired in the canon; the areas are rebuilt to the new lore later, see `docs/plan.md`). Meridian is being
rebuilt to `docs/regions/meridian-redesign.md`: phase 1 (terraces and routes), phase 2 (every building
enterable, upper floors included) and phase 3 (objects: readables, view points, stations, banks, gates,
gathering spots, skill shortcuts) are built; people and data (phase 4) are next.

## E1: gathering tools, Mining and Woodcutting (done)

- [x] `tool` on items (`ToolDef`): kind, tier, requirements to use, and optional effects
      (success bonus, conservation, extras added or replacing the node's, transformations
      with another skill's level and XP). Used from the pack or worn; the best usable one
      (highest tier, then success) is picked. Nodes name a `tool` kind, an optional `toolTier`
      and a `depleteChance` (ore 1, trees 1/8). Messages for a missing tool, a too-low tier and
      a tool's level.
- [x] Content: copper and ironshod pickaxes and hatchets (tiers 1-2; copper ones in the starter
      kit), copper and ironstone seams, alder and ash trees, rough garnet/amber/jet and resin as
      extras. Rates checked against the pace table (`ToolTests`).
- [x] Client: a tool line in item tooltips; tools and nodes in the Guide's skill unlocks.

Not yet: rocks and trees placed in the scenes (proposed tiles in `docs/plan.md` Handoffs), a
seller for tools (Smithing makes them later), mining/chopping animations (nodes use "work").

## E3: using an item on something (done)

- [x] "Use" on a pack item, then a click on an object, an NPC or another pack item
      (`UseItemMessage`, protocol 18). Objects and NPCs are walked up to (an interaction, so
      walking cancels it); item on item happens at once, either way round.
- [x] What happens is data: uses.json (`UseDef`, `UseSystem`). An entry names the `item` (or
      `tag:x`), one of `onObject` / `onNpc` (or `tag:x`) / `onItem` (or `tag:x`), `requirements`,
      dialogue `conditions` and a `failMessage`, then `consume`, `consumeTarget`, `animation`,
      `message` ({tokens}), `effects` (effect ops), dialogue `actions`, a `recipe` (repeated where
      the player stands while the inputs last; a recipe may now have no station) and a `dialogue`.
      The first entry that matches and passes wins; nothing listed: "Nothing interesting happens."
- [x] `ItemUsedOn` event; quest objective and achievement kind `use` (target: the use's id).
- [x] Content: a fen rat carcass on the skinning rack butchers it; the skinning knife on a
      carcass and a rat gall on Corvin say a line.
- [x] Client: the Use option, use mode ("Use X -> Y" in hover text and menus); Escape, Cancel, a
      right-click on nothing or walking put the item down. `UseTests`.

Not yet: a highlight on the picked-up slot (waits on the UI framework, U1), using items on
players or ground items.

**Death v1, server (D1, done):** `FerrySystem` and death.json. Everything carried and worn goes
to the ferryman's hold; the player wakes at death.json's `wake` tile and his conversation opens
(dialogue `ferryman`, no NPC needed).
The fee is `feePercent` of the held items' `value` (free under `freeBelow`), paid in `currency` from
the pack, then the bank; an `obol` in the pack at death is taken as the fee; unpaid gear is kept
`holdHours` of real time (`ServerConfig.UtcNow`), and dying again adds to it. Paying returns it
and sends the player to `returnTo` (null: the spawn); leaving sends them back without it.

**Death v1, the landing (D2, done; scene from Sync World):** `world/regions/realm_of_the_dead.json`,
"The Landing": an instanced 32x32 region at (8192, 8192), far from the continent. Ash ground in
dark water, a jetty with the ferryman (npc `ferryman`) at its landward end, his boat, lanterns, a
bench, and a `grave_chest` (a bank). Very low sun, near-black fog. The dead wake at (8208, 8206).
The shipped conversation explains death the first time (flag `ferry_explained`). The client has
no death window any more: the remark (text kind `Ferryman`) is a line in chat, and the
conversation is the dialogue box.

**The world as data (C1-C4, done):** regions are text (`world/regions`), compiled to the server maps by
`tools/world` without Unity, drawn and checked in Unity by **R'th > World > Sync World**; Tolling,
Palmerway, the Saint's crypt and LevelTest were converted with byte-identical maps, and E1's rocks
and trees were placed as data. How to work: `docs/workflow.md`.

**U2, the character window (on `claude/u2-character`):** buttons under the worn equipment open a
`CharacterModal` (stats grouped, and the inspect card); right-click another player to Inspect them.
The draggable Character panel is gone. A general client question, `QueryMessage` /
`QueryResultMessage` (protocol 19), answered by `QuerySystem`; `InspectSystem` answers "inspect".

**Next: not chosen yet.** Candidates: ranged, an art pass on Tolling, more quests and
achievements.

---

## Extending the server

`World` (server/Rth.Server.Core/Game/World.cs) owns entities, connections, movement, visibility
and saving. Everything else is a `GameSystem` listed in `World.StandardSystems` (order matters:
systems run in that order within each phase, and a system may use those before it while
attaching). Today: `SkillSystem`, `FlagSystem`, `ItemSystem`, `CombatSystem`, `DeathSystem`,
`NpcSystem`, `GroundItemSystem`, `LootSystem`, `ObjectSystem`, `ClimbSystem`, `ReligionSystem`, `RecipeSystem`, `GatheringSystem`, `InterfaceSystem`,
`DialogueSystem`, `BankSystem`, `FerrySystem`, `ShopSystem`, `PointsSystem`, `TaskSystem`, `QuestSystem`, `UseSystem`,
`QuerySystem`, `InspectSystem` (and more; the list in `World.StandardSystems` is the truth).

| To add... | Use |
|---|---|
| A feature (skill, quest, shop) | A new `GameSystem`: override the phase hooks you need; register client messages with `World.OnMessage<T>`. |
| Reacting to something another system did | `World.Events.Subscribe<T>` / `Publish`. Existing: `EntityDied`, `DamageDealt`. Add event classes in `GameEvents.cs`. |
| "Walk over there and do X" (pick up, mine, talk) | An `Interaction` subclass; start it with `World.StartInteraction`. Walking cancels it. |
| Per-player state for a system | `player.Component<T>()` (created on first use). |
| Saving that state | Override `OnLogin`/`OnSave`; use `save.Section<T>(key)` / `save.SetSection(key, …)`. Unknown sections are preserved. |
| A stat gear or buffs can change | Add the id to `shared/Combat/StatIds.cs`, read it where it matters; contributors implement `IStatContributor`; call `World.RecalculateStats` when inputs change. |
| A new item capability | A component class on `ItemDef`, an action provider in `shared/Items/ItemActions.cs`, and a handler via `ItemSystem.RegisterAction`. |
| A new consumable/ritual effect | A constant in `EffectOp`, validation in `GameContent.KnownEffectOps`, a handler via `ItemSystem.RegisterEffectOp`. |
| A hymn (prayer) | An entry in hymns.json: drain, `overhead`, `line`, `stats` and `requirements`. What it does is its stats; a new effect is a new stat that combat reads. |
| A verse (Cruor) | An ability of `"type": "Verse"` in abilities.json: blood costs (`bloodCostCurrent`/`bloodCostMax`), a scripture in `reagents`, a `buff` (status, ticks, stats; `targetTag`/`otherwise`, `whileBloodBelow`, `endsOnHitTaken`), `xp`. A new effect is a new stat read in `CombatSystem.Blood.cs`. |
| A timed buff on any ability | `buff` on the ability, and its status in statuses.json. Conditional stats are read at the moment of the hit (`CombatSystem.Stat`), the rest go into the stat block. |
| A weapon of a style | `equipment.style` (and optionally `autoAttack`) on a Weapon item; the style's auto-attack is in combat.json `autoAttacks`. |
| A bleed | `bleedPercent`/`bleedTicks` on an ability, or `CombatSystem.AddBleed`. |
| A character animation for skilling or an action | `World.Animate(entity, name, face)` sends "anim:<name>" (a clip or role in animations.json), optionally turning to face a tile; `World.Face` only turns. Recipes and nodes have an `animation` field. |
| A weapon's default bar | combat.json `actionBars` per style. Each style's main bar is client state `bars.main.<Style>`, swapped in with the weapon (client and Revolution both read it). |
| Something an entity visibly holds or wears | `Entity.SetHeld` (players: the weapon slot; NPCs: `held` in npcs.json) reaches clients in `EntityAdded.Held` and appearance events; the client's body draws it (`HeldItemArt` until items have models). |
| Fight pacing | Tune creatures in npcs.json; `PacingTests` simulates the starter kit against each and prints the numbers. |
| A gathering node (rock, tree, fount) | `node` on an object type in objects.json (skill, level, chance, XP, items, extras, points, yields, `depleteChance`, respawn, an optional surge, a `tool` kind and `toolTier`) and a `gather` action; place it as a `rock`, `tree` or `prop` piece with `"object"` in the region's data. No code. |
| A gathering tool (pickaxe, hatchet, rod) | `tool` on the item in items.json (kind, tier, requirements, `success`, `conservation`, `extras`/`replacesExtras`, `transforms`); add `equipment` (Weapon) to make it wieldable. `GatheringSystem.BestTool` picks it. A new effect is a field on `ToolDef` read in `GatheringSystem.Attempt`. |
| Something that restores devotion | An object action `pray` in objects.json (an altar), or the `restoreDevotion` / `restoreDevotionPercent` effect ops on a consumable. |
| A skill that replaced another | `retired` in skills.json (old id: new id); saves move the XP on login. |
| A skill | An entry in skills.json; award XP with `SkillSystem.AddXp`, check with `Level`/`Check`; react to `LevelUp`. |
| A level requirement | A `requirements` list (`SkillRequirement`) on the def, checked with `SkillSystem.Check`. |
| A processing skill (distilling, brewing, cooking) | Recipes in recipes.json and a station in objects.json with a `process` action; place the station in the scene with `MapObject` + `MapBlocker`. No code. |
| A new object behaviour (mine, open, pray) | An action on the object def, a handler via `ObjectSystem.RegisterAction`, usually starting an `ObjectInteraction`. |
| An NPC menu option (pickpocket, follow) | An action on the NPC def; a handler via `NpcSystem.RegisterAction` (runs once the player is beside the NPC). |
| Something said or done in dialogue | `DialogueSystem.RegisterCondition` / `RegisterAction` / `RegisterToken`. Conversations themselves are data. |
| A conversation with no NPC in the world (a voice, the ferryman before his landing) | `DialogueSystem.Start(player, dialogueId, speaker)`; it ends when finished, closed or on death, not by walking away. |
| What happens at death, the fee, the obol | death.json (`DeathRules`); the ferryman's conversation uses `ferryHolds` / `ferryFree` / `ferryCanPay` / `ferryObol` / `inRealm`, actions `ferryPay` / `ferryLeave`, tokens `{ferryFee}` `{ferryValue}` `{ferryHours}` `{deaths}`. Something else reacting to waking: subscribe to `PlayerRespawned`. |
| Using an item on something ("Use X -> Y") | An entry in uses.json: the item (or `tag:`), one of `onObject` / `onNpc` / `onItem`, checks (`requirements`, dialogue `conditions`, `failMessage`) and outcomes (`consume`, `message`, `effects`, dialogue `actions`, a `recipe`, a `dialogue`). No code; a new kind of outcome is usually a dialogue action. Counted by quests and achievements as kind `use`. |
| A quest | An entry in quests.json (stages, objectives, rewards) and dialogue that starts/advances/completes it. New objective kinds: count an event in `QuestSystem`. |
| Remembering story progress | `FlagSystem` (persistent per-player key/values); gate actions with `requiresFlag`. |
| A window the server opens | A value in `InterfaceKind`; `InterfaceSystem.Open/IsOpen/Close`; draw it in the client's `TownWindows`. |
| A currency | `ShopSystem.RegisterCurrency(prefix, ICurrency)`; shops then price in "prefix:id". |
| A kind of task | Subscribe to the event that proves it and advance `TaskSystem` progress for a new `kind`. |
| An ability cost | `reagents` on the ability (items), checked by the shared requirement rule and consumed on use. Occult abilities use none: phlegm and bile only. |
| A status (buff or debuff icon) | An entry in statuses.json (name, icon, `{value}` text, `debuff`); a system implementing `IStatusSource` lists who has it each tick. Timed ability effects show as the status named after their `EffectType`. |
| A new kind of map placement | A marker component in tools/unity-map-tools, an export file in `MapExporter`, a field on `MapPlacements` (merged across regions by `WorldLoader`). |
| A terraced landscape (a city on a hillside, a gorge with bridges) | A `Terraces` map (`WorldKit.Terraces.cs`): `Flat`, `Stair`, `Ramp`, `Deck` and `Void` tile by tile, then `Build`; it walls every drop (kerbs and parapets) and warns about steps over 0.5 m or ramps steeper than 1 in 3. Start the region with `BeginTerrainRegion`; `Viaduct` and `Stream` draw what goes under and through it. See `MeridianBuilder`. |
| A floor above the ground (upper storey, gallery, footbridge, tower room) | A `MapLevel` (1-3) on a group holding that floor's slab (ground collider), walls (`MapWall`), blockers, objects and spawns: the exporter writes one plane per level. `hiddenFromBelow` (default) switches the group off while the player is on a lower floor; turn it off for what must always show from outside (outer walls, an open-air deck). Planes are separate collision, and people only see people on their own plane. There is no plane below the ground: cellars, caves and dungeons are separate small regions (instanced, 1024 tiles north of the surface place) entered by a `climb` object whose landing is in that region (`trapdoor_down`/`trapdoor_up`, `cave_mouth`/`cave_exit`, or the stairs and ladders). |
| Stairs, ladders, trapdoors | An object with a `climb` action (`stairs_up`/`stairs_down`/`ladder_up`/`ladder_down` exist) placed with `MapObject` + a collider, and its `climbTo` set to an empty object on the landing tile under the target floor's `MapLevel`. One object per end. The exporter warns about a blocked landing; `ClimbSystem` moves the player. |
| A new region of the world | A file `world/regions/<id>.json` (id, title, scene path, zone-aligned origin and size, ground, materials, pieces); `tools/world export`, then Sync World creates its scene. No server code. |
| Something in a region (a house, a fence, a rock to mine, a creature) | A piece in `world/regions/<id>.json` (`docs/workflow.md` lists the kinds); `tools/world check`, `export`, `render`. Interactive with `"object"`. |
| A new kind of piece | A constant in `PieceKind`, its collision in `RegionCompiler.AddPiece`, its look in the client's `PieceBuilder`; Sync World checks the two agree. |
| Something only worth making by hand | Under the scene's `Handmade` root; saving the scene captures it into `<id>.handmade.json`. |
| A city or town blockout | A builder using `WorldKit.City.cs` (buildings with roofs, walk-in halls with doors, city walls, towers, gatehouse arches, bridges, statues, braziers, banners, stalls, boats, domes, colonnades, shade sails, windcatchers, water wheels, armillaries, rails, the Quaere's silhouette; `CutGround` for a pit); see `CruorBuilder`, `TitheBuilder`, `CisternBuilder`, `MeridianBuilder` and `docs/regions/`. |
| A building you walk into (on any terrace, with upper floors) | `WorldKit.Enterable` (`WorldKit.Interiors.cs`) returns a `Shell`: doors, a `MapLevel` per upper floor (outer walls always drawn, slab only from above, or a railed gallery), a roof that hides inside. `Shell.Stairs`/`Ladder` join floors with their landings set; `Table`, `Counter`, `Shelves`, `Hearth`, `Bed`, `Thing`, `Object` furnish any floor. `Pavilion` for open halls on posts (optionally a deck), `RowHouse` for a house repeated down a street, `CheckBuildings` for the three-tile gap and 25% rules. See `MeridianBuilder.Buildings.cs`. |
| A stand-in for an asset still to be made | `WorldKit.Asset(go, "region/id", note)` adds a `BlockoutAsset`; saving the region writes `docs/regions/<region>-assets.md` (count, size, notes). |
| A region's own light (sun colour and strength, ambient, fog) | `ownAtmosphere` and its fields on the `MapArea`; exported to `.region.json`, blended in by `RegionAtmosphereView`. |
| Something per region (music, rules, title) | A field on `WorldRegion`, written to `<name>.region.json` by `MapExporter`; it reaches clients in `RegionsMessage` as JSON. |
| A place, road or river on the world map | `world/layout/continent.json` (docs/layout.md); the server compiles it at startup into a `WorldChart` sent in `ClientContent.Chart`. `p-` names show as unnamed marks; `"hidden": true` keeps a place off the map. No code. |
| "Who is near this tile" | `World.EntitiesNear(plane, tile, radius)` (zone index), not a scan of every entity. |
| A sign, notice or flavour action | An object action with a `message` in objects.json (no handler needed). |
| An action only some players may use | `requiresFlag` + `lockedMessage` (story gates), or `requirements` (skill levels) on the object or NPC action; `ObjectSystem.Unlocked` checks both. |
| A skill shortcut (rubble, a ford, a thicket) | A `climb` object at each end with `requirements` on its action and `climbTo` on the far side; see `MeridianBuilder.Shortcut`. |
| Furniture you can use (a counter that's a bank, a shelf you read) | `Shell.Thing`/`Shelves`/`Table` plus a `MapObject` on the returned object (`MeridianBuilder.Use`); keep it one or two tiles deep so a tile beside its middle is free. |
| An interface panel | A `UiPanel` subclass in `client/Assets/Scripts/Ui/Panels/` (id, title, icon, default window, `Build`, `Refresh`); it registers itself. Build it from `UiKit` pieces so it matches the theme (`Resources/UI/Rth.uss`). A new default spot goes in `UiDefaultLayout`. A window the server opens overrides `ServerOpen` and `OnClosedByPlayer`. |
| An item's model (held now; worn later) | A function in `tools/art/blender/items/held.py` (origin at the grip, handle along Blender -Y) exports `client/Assets/Resources/Items/<Name>.fbx` with palette-named materials; `"model"` on the item in items.json, and `"recolor"` (`{ "Copper": "Iron" }`) for a variant. `HeldItemArt` draws it in the fist; items without a model keep the stand-in boxes. |
| A creature's model | A script in `tools/art/blender/creatures/` (rules in its README) exports an FBX to `client/Assets/Resources/Creatures/`, materials named after palette colours; `"model"` on the NPC in npcs.json. `CreatureBody` draws it (moved rigidly until it has animations); no model falls back to the primitive `shape`. |
| A model for players or creatures | An `EntityBody` subclass in `client/Assets/Scripts/Characters/` (height, radius, `Animate`, action/hit/death, `InCombat`), picked in `EntityBody.Create`. Players use `ModelBody` (skinned art + clips) when the clips exist, else `HumanoidBody` (code-built, procedural). |
| Character art (body parts, hair, equipment) | Skinned meshes on the shared skeleton, made in Blender (`tools/character-models/`, see its README for the bone, part and colour-channel contract), exported to `client/Assets/Resources/Characters/`. `CharacterModel` assembles them: `Attach` (with the kit parts it hides, or a bone socket), `SetColor(channel)`, `SetHair`. |
| A character animation | A humanoid clip file in `client/Assets/Resources/Characters/Animations/` named by what it is (Mixamo works; `CharacterImporter` sets it up); tune its strength, speed, trim and upper-body masking in `Resources/Characters/animations.json`. `ClipAnimator.Play(name)` plays any clip by name. |
| Ground with height (hills, cliffs, stairs, terraces) | Natural ground: a `MapTerrain` in the region (`WorldKit.Terrain`, or **R'th > World > Bake Terrain From Layout** from `client/Assets/World/layout.json`), shaped with `MapTerrainStamp`s, never by editing baked heights; the exporter blocks its steep tiles (over `cliffStep`) and impassable biomes. Built ground (terraces, stairs, decks): colliders at the real height; the exporter ignores their height, so every drop needs a `MapWall` (or the tiles beyond a `MapBlocker`), and a ramp climbs at most 1 m in 3 tiles (see `LevelTest.unity`). The client stands characters, items and markers on the ground colliders of their floor (`GroundHeight.Centre`); the camera reads the terrain alone (`GroundHeight.TerrainAt`). The server stays flat; heights are client and tooling only (`Heightfield`, `WorldLayout` in shared). |
| A colour variant of a model (an ore, a metal) | A `Recolor` on the placement or prefab, or `"recolor": { "Material": "PaletteName" }` on the object type in objects.json. Colours come from `Resources/Art/palette.json`. |
| A tree, rock or prop | Within the budgets in `Assets/Art/art-budgets.json` (check with **R'th > Art > Validate Art**), 2-4 palette colours as materials; leafy trees via `kit.LeafyTree` and `LeafyTree` specs; world textures in `Assets/Art/Textures/` (128 px, point filtered). |
| A camera rule | Plain maths in `client/Assets/Scripts/View/CameraRules.cs` with tests in `client/Assets/Tests/EditMode/` (also run by `dotnet test` through `tools/client-tests`); `CameraRig` applies it. Limit sets are `CameraProfile`s. |
| An icon | 16 rows of pixel art in `client/Assets/Resources/UI/pixelicons.txt` (or a PNG at `Resources/Icons/<id>.png`); name it with `"icon"` on the item, ability or skill. |
| A modal (big tabbed window) | A `UiModal` subclass in `client/Assets/Scripts/Ui/Modals/` listing `ModalTab`s; it registers itself. `MasterDetail` gives the list-and-detail layout. |
| Something draggable, or a place to drop it | `UiRoot.BeginDrag` with a payload from `DragData.cs` (or a new one); `UiRoot.AddDropTarget` on the receiving element. |
| An achievement | An entry in achievements.json (kind kill, talk, recipe, level, quest, object, region, obtain or use). A new kind: count the event that proves it in `AchievementSystem`. |
| Player state the interface shows | Send it as `JsonStateMessage.Of(key, value)` from the system's `SendState`; the client reads `GameSession.ServerState<T>(key)`. |
| Client settings that follow the player | `GameSession.SetClientState(key, json)`; read `GameSession.ClientState`. |
| A question the client asks the server (inspect, price check) | A kind in `QueryKinds`; register a handler with `QuerySystem.Register(kind, (asker, arg) => QueryAnswer.Of(value) / No(problem))`; the client calls `GameSession.Query` and reads `QueryResult(kind, arg)`. Public information only. |
| A line on the inspect card | `InspectSystem.AddHighlights(player => lines)` (label, value, icon). |
| A button under the worn equipment | An entry in `WornPanel.Buttons` (icon, tooltip, click). |
| A chat channel (private, group) | A `ChatChannel` value and its routing in `ChatSystem`; the chat panel shows `ChatLineMessage`s. |
| A new kind of content file | A field on `ContentParts`, load it in `GameContent.Load`, validate in `Create`. Client-visible content goes in `ClientContent` (sent as JSON, so no codec changes). |
| A client message | A message class in `shared/Net/Messages.cs`, a case in `MessageCodec.Decode`, bump `Protocol.Version`. |

Tests: build worlds with `WorldFixture` and `TestContent` (server/Rth.Tests/TestSupport.cs), which
uses fixed content so tuning the real data files never breaks tests. `World` can also be built
with an explicit list of systems. `ShippedMapTests` walks every exported region in `server/data/maps`
from its spawn and fails on any object nobody can stand beside.

---

## References and boundaries

- **Lost City / 2004Scape** (`github.com/LostCityRS/Engine-TS`, MIT): use the **engine** as an
  architecture reference: the tick loop, routefinder/collision, entity updates, engine/content
  split. Do **not** copy or adapt anything from its content (`data/`), its scripts, or any
  client/cache assets. Those reconstruct Jagex's game.
- **No Jagex assets.** The OSRS look is a style target only. Never reuse RuneScape models,
  textures, icons, sounds, interface art, item names or text. All content is original.
- **Unity licence:** Unity Personal (free). Drive the Editor through Unity's own CLI/MCP
  (`unity mcp configure claude-code`; the project needs `com.unity.pipeline`, Unity 6+).
  Unity's terms expect AI agents to use Unity's own framework, so don't add third-party
  Unity-MCP plugins.
- If the Editor stops responding to the CLI, check for **Safe Mode** (C# compile errors) first:
  fix the errors in files, then restart the Editor.
- Unity CLI notes (CLI 1.0 beta, `%LOCALAPPDATA%\Unity\bin\unity.exe`): commands are namespaced,
  e.g. `unity command editor.playmode start`, `unity command console get --tail 20`. From
  PowerShell, put C# for `eval` in a file and use `eval_file` (quotes get mangled otherwise); the
  file is a method body, so use fully qualified names, not `using` lines. `simulate_pointer` does
  not reach `Mouse.current` here, so drive gameplay checks through public methods
  (`GameSession.LogIn`, `WalkTo`, `Attack`, `UseAbility`) instead. `screenshot --view game`
  renders the camera only (no IMGUI); `game_view --source screen --save_path Temp/x.png` includes
  the HUD but writes into `Assets/Temp/`, so move the file out and delete that folder afterwards.
- `UnityEngine.CollisionFlags` clashes with `Rth.Shared.World.CollisionFlags`; alias it in Unity
  code. Unity 6.6 deprecates `FindFirstObjectByType` and the `FindObjectsSortMode` overloads; use
  `FindAnyObjectByType` and `FindObjectsByType<T>(FindObjectsInactive)`.

## Working conventions

- **The world is built together, as data** (`docs/workflow.md`): agents without Unity change
  `world/regions/*.json`, content JSON and C#, and run `dotnet run --project tools/world --
  check` / `export` / `render` and `dotnet test`; they never write `.unity`, `.prefab`, `.asset`,
  `.mat` or `.meta` files. The agent with Unity runs **R'th > World > Sync World**, compiles
  client code, and commits what Unity wrote. One region per branch at a time.
- Commit early and often with clear messages; keep changes reviewable.
- Write tests for game rules in `/shared` and `/server`. Prefer fast simulation tests
  ("run 1000 ticks, assert XP") over manual Editor checks.
- Ask before big structural changes to this architecture, and keep this file up to date when
  decisions change.
