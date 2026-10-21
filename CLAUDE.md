# R'th: project guide for Claude Code

A private, non-commercial multiplayer RPG for an invited group of friends (at most ~200 players).
It plays like **RuneScape 3** (Evolution of Combat abilities, the skilling loops, a sandbox with no
forced faction) and looks like **Old School RuneScape** (low-poly models, chunky flat textures,
classic layout). It is set in its own world, **R'th**, built around the four humours.

**Status:** Milestones 1 (architecture), 2 (combat core: Occultism, bile, phlegm, breath),
3 (items, equipment, consumables, loot), 4 (skills, workstations, carcass butchering) and
5 (townsfolk: dialogue, bank, shops, tasks; Distillation and reagents), 6 (quests), 7 (a
multi-region world streamed by the client) and 8 (the window-based interface, chat) are done.

**Build everything as extensible systems.** New features plug into the mechanisms under
"Extending the server" below (systems, events, interactions, stats, item components, effect ops,
save sections, data files) rather than adding special cases. Prefer a general mechanism plus
data over code for one case.
See `README.md` for how to build, run and test.

Design docs:
- `docs/lore.md`: the world, history, factions and characters. Place names are listed in `docs/world-map.md`.
- `docs/systems.md`: combat resources and the rules of a fight.
- `docs/skills.md`: the v1 skill roster, the rules every skill follows, and each skill in full.

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
- **Map pipeline:** the map is designed in Unity scenes. A `/tools` exporter writes tile data
  (walkability, wall edges, object footprints, plane/level) to a file the server loads. The
  client renders the scene and the server reads the export. Keep them in sync through this one
  path, never by hand.
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
  scenes hold only their map: `Tolling.unity` (Phase 1 of `docs/world-map.md`) and
  `PilgrimRoad.unity`, generated by the **R'th > Dev > Rebuild ... Scene** menus from
  `TollingBuilder` / `PilgrimRoadBuilder` using the reusable `WorldKit` (`BeginRegion` /
  `SaveRegion`, houses with hideable roofs, water from tile sets, roads, fences, trees, stations,
  spawns). Pressing Play in a region scene loads the Game scene beside it (`GameBootstrap`).
  Once a scene exists it is the source of truth: rebuilding overwrites hand edits.
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
- [x] Death and the Assayer; respawn with balanced humours.
- [x] Content (abilities, NPCs, rules, Assayer lines) as JSON in `server/data/content/`, hot-reloaded.
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
stockpile carcasses, and old-blood.

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
      Touches the Assayer and the Dreamweaver threads from the lore, and Corvin's curiosity.

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

**Next: the skills rework** (`docs/skills.md`, section 9): breath removed, focuses become
Religion's petitions, the v1 skill roster. Other candidates: an art
pass on Tolling, more quests and achievements, a second combat style.

---

## Extending the server

`World` (server/Rth.Server.Core/Game/World.cs) owns entities, connections, movement, visibility
and saving. Everything else is a `GameSystem` listed in `World.StandardSystems` (order matters:
systems run in that order within each phase, and a system may use those before it while
attaching). Today: `SkillSystem`, `FlagSystem`, `ItemSystem`, `CombatSystem`, `DeathSystem`,
`NpcSystem`, `GroundItemSystem`, `LootSystem`, `ObjectSystem`, `RecipeSystem`, `InterfaceSystem`,
`DialogueSystem`, `BankSystem`, `ShopSystem`, `PointsSystem`, `TaskSystem`, `QuestSystem`.

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
| A skill | An entry in skills.json; award XP with `SkillSystem.AddXp`, check with `Level`/`Check`; react to `LevelUp`. |
| A level requirement | A `requirements` list (`SkillRequirement`) on the def, checked with `SkillSystem.Check`. |
| A processing skill (distilling, brewing, cooking) | Recipes in recipes.json and a station in objects.json with a `process` action; place the station in the scene with `MapObject` + `MapBlocker`. No code. |
| A new object behaviour (mine, open, pray) | An action on the object def, a handler via `ObjectSystem.RegisterAction`, usually starting an `ObjectInteraction`. |
| An NPC menu option (pickpocket, follow) | An action on the NPC def; a handler via `NpcSystem.RegisterAction` (runs once the player is beside the NPC). |
| Something said or done in dialogue | `DialogueSystem.RegisterCondition` / `RegisterAction` / `RegisterToken`. Conversations themselves are data. |
| A quest | An entry in quests.json (stages, objectives, rewards) and dialogue that starts/advances/completes it. New objective kinds: count an event in `QuestSystem`. |
| Remembering story progress | `FlagSystem` (persistent per-player key/values); gate actions with `requiresFlag`. |
| A window the server opens | A value in `InterfaceKind`; `InterfaceSystem.Open/IsOpen/Close`; draw it in the client's `TownWindows`. |
| A currency | `ShopSystem.RegisterCurrency(prefix, ICurrency)`; shops then price in "prefix:id". |
| A kind of task | Subscribe to the event that proves it and advance `TaskSystem` progress for a new `kind`. |
| An ability cost | `reagents` on the ability (items), checked by the shared requirement rule and consumed on use. Occult abilities use none: phlegm and bile only. |
| A status (buff or debuff icon) | An entry in statuses.json (name, icon, `{value}` text, `debuff`); a system implementing `IStatusSource` lists who has it each tick. Timed ability effects show as the status named after their `EffectType`. |
| A new kind of map placement | A marker component in tools/unity-map-tools, an export file in `MapExporter`, a field on `MapPlacements` (merged across regions by `WorldLoader`). |
| A new region of the world | A scene with a `MapArea` at its absolute, zone-aligned rectangle (a builder using `WorldKit.BeginRegion`/`SaveRegion`); export it. No server code. |
| Something per region (music, rules, title) | A field on `WorldRegion`, written to `<name>.region.json` by `MapExporter`; it reaches clients in `RegionsMessage` as JSON. |
| "Who is near this tile" | `World.EntitiesNear(plane, tile, radius)` (zone index), not a scan of every entity. |
| A sign, notice or flavour action | An object action with a `message` in objects.json (no handler needed). |
| An interface panel | A `UiPanel` subclass in `client/Assets/Scripts/Ui/Panels/` (id, title, icon, default window, `Build`, `Refresh`); it registers itself. Build it from `UiKit` pieces so it matches the theme (`Resources/UI/Rth.uss`). A new default spot goes in `UiDefaultLayout`. A window the server opens overrides `ServerOpen` and `OnClosedByPlayer`. |
| A model for players or creatures | An `EntityBody` subclass in `client/Assets/Scripts/Characters/` (height, radius, `Animate`, action/hit/death, `InCombat`), picked in `EntityBody.Create`. Players use `ModelBody` (skinned art + clips) when the clips exist, else `HumanoidBody` (code-built, procedural). |
| Character art (body parts, hair, equipment) | Skinned meshes on the shared skeleton, made in Blender (`tools/character-models/`, see its README for the bone, part and colour-channel contract), exported to `client/Assets/Resources/Characters/`. `CharacterModel` assembles them: `Attach` (with the kit parts it hides, or a bone socket), `SetColor(channel)`, `SetHair`. |
| A character animation | A humanoid clip file in `client/Assets/Resources/Characters/Animations/` named by what it is (Mixamo works; `CharacterImporter` sets it up); tune its strength, speed, trim and upper-body masking in `Resources/Characters/animations.json`. `ClipAnimator.Play(name)` plays any clip by name. |
| An icon | 16 rows of pixel art in `client/Assets/Resources/UI/pixelicons.txt` (or a PNG at `Resources/Icons/<id>.png`); name it with `"icon"` on the item, ability or skill. |
| A modal (big tabbed window) | A `UiModal` subclass in `client/Assets/Scripts/Ui/Modals/` listing `ModalTab`s; it registers itself. `MasterDetail` gives the list-and-detail layout. |
| Something draggable, or a place to drop it | `UiRoot.BeginDrag` with a payload from `DragData.cs` (or a new one); `UiRoot.AddDropTarget` on the receiving element. |
| An achievement | An entry in achievements.json (kind kill, talk, recipe, level, quest, object, region or obtain). A new kind: count the event that proves it in `AchievementSystem`. |
| Player state the interface shows | Send it as `JsonStateMessage.Of(key, value)` from the system's `SendState`; the client reads `GameSession.ServerState<T>(key)`. |
| Client settings that follow the player | `GameSession.SetClientState(key, json)`; read `GameSession.ClientState`. |
| A chat channel (private, group) | A `ChatChannel` value and its routing in `ChatSystem`; the chat panel shows `ChatLineMessage`s. |
| A new kind of content file | A field on `ContentParts`, load it in `GameContent.Load`, validate in `Create`. Client-visible content goes in `ClientContent` (sent as JSON, so no codec changes). |
| A client message | A message class in `shared/Net/Messages.cs`, a case in `MessageCodec.Decode`, bump `Protocol.Version`. |

Tests: build worlds with `WorldFixture` and `TestContent` (server/Rth.Tests/TestSupport.cs), which
uses fixed content so tuning the real data files never breaks tests. `World` can also be built
with an explicit list of systems.

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

- Commit early and often with clear messages; keep changes reviewable.
- Write tests for game rules in `/shared` and `/server`. Prefer fast simulation tests
  ("run 1000 ticks, assert XP") over manual Editor checks.
- Ask before big structural changes to this architecture, and keep this file up to date when
  decisions change.
