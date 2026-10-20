# R'th: project guide for Claude Code

A private, non-commercial multiplayer RPG for an invited group of friends (at most ~200 players).
It plays like **RuneScape 3** (Evolution of Combat abilities, the skilling loops, a sandbox with no
forced faction) and looks like **Old School RuneScape** (low-poly models, chunky flat textures,
classic layout). It is set in its own world, **R'th**, built around the four humours.

**Status:** pre-prototype. Nothing is built yet. Start with Milestone 1 below.

Design docs:
- `docs/lore.md`: the world, history, factions and characters. All names are provisional.
- `docs/systems.md`: combat resources, skills, and open design questions.

---

## Architecture (decided)

**A standalone C# server owns the game. Unity is only the client.**

```
/server     Plain C# console app (.NET, current LTS). Runs the tick loop, persistence, networking.
/shared     C# class library with NO UnityEngine references. Game rules, definitions,
            message types, map/tile data structures. Used by both server and client.
/client     Unity project (Unity 6+). Rendering, input, UI, animation. Sends intents, draws state.
/tools      Small utilities, e.g. the map exporter.
/docs       Design documents.
```

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

### Networking

Start simple: TCP with length-prefixed binary messages, with message types defined in `/shared`.
The client interpolates smoothly between tick snapshots so movement doesn't look steppy.
Handle reconnects cleanly. A library such as LiteNetLib is fine if it saves real work, but don't
add complexity before Milestone 1 proves the need.

### Persistence

Players are saved on logout and on a periodic autosave (every few minutes). SQLite is a sensible
default. Save on server shutdown. The world state that must persist (e.g. player-built or
long-lived changes) gets designed when such features exist.

### Content as data

Items, NPCs, drop tables, skills and abilities should be data files (e.g. JSON) loaded by the
server, with hot-reload in development, so tuning doesn't need a restart. Code handles the
mechanics; data handles the content.

---

## Milestone 1: prove the architecture

Build only this, and make it feel good:

- [ ] Repo layout as above; `/shared` builds for both the server and Unity.
- [ ] Server runs a drift-corrected 600 ms tick loop with the phases above.
- [ ] One small test map designed in Unity, exported by the tool, loaded by the server.
- [ ] Two players can log in (name only, no passwords; it's friends-only, add auth later if wanted).
- [ ] Click a tile: the server pathfinds and the player walks there; walls block movement.
- [ ] Both players see each other move, with smooth interpolation between ticks.
- [ ] Log out and back in: the player is where they left off (persistence).
- [ ] Pressing Play in the Editor starts an in-process server automatically; the standalone
      server also works.
- [ ] Unit tests for pathfinding, tick timing and save/load run without Unity.

**Milestone 2 (next):** the combat core with one style, **Occultism**, because its bile axis is
the riskiest and most unusual system. Blood, phlegm and Breath come with it. See
`docs/systems.md`.

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

## Working conventions

- Commit early and often with clear messages; keep changes reviewable.
- Write tests for game rules in `/shared` and `/server`. Prefer fast simulation tests
  ("run 1000 ticks, assert XP") over manual Editor checks.
- Ask before big structural changes to this architecture, and keep this file up to date when
  decisions change.
