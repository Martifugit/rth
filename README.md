# R'th

A private multiplayer RPG for a group of friends: RuneScape 3-style play, Old School RuneScape-style
looks, in an original world. See `CLAUDE.md` for the architecture and `docs/` for the design.

## What you need

- .NET SDK 10
- Unity 6.6 (6000.6.6f1) with a Unity Personal licence

## First-time setup

```powershell
dotnet build        # builds everything and copies the shared DLLs into client/Assets/Plugins/Rth
dotnet test         # ~95 tests, about a second, no Unity needed
```

Run `dotnet build` **before** opening the Unity project the first time, and again whenever you
change anything under `/shared` or `/server`. Unity uses the built DLLs, and without them it opens
in Safe Mode with missing-type errors.

Then open `client/` in Unity and load `Assets/Scenes/TestMap.unity`.

## Playing in the Editor

Press Play. The Editor exports the scene's map and starts a local server in the same process
(turn this off under **R'th > Start Local Server On Play**). Type a name and log in.

- Left-click a tile to walk there. **R** toggles running (2 tiles per tick).
- Left-click a creature to attack it. **1–8** (or clicking the action bar) queue Occult
  abilities; hover a slot to see what it does and what bile or phlegm it needs.
- **Z / X / C** toggle deflecting melee, ranged or occult attacks, which spends breath.
- Arrow keys or middle-drag rotate the camera; the scroll wheel zooms.

The test map has fen rats (south-west, passive), vitriol mites (north-east, aggressive, they spit
from range) and a straw penitent beside the spawn, a training effigy that never fights back.

## Tuning content

Abilities, creatures, combat rules and the Assayer's lines are JSON in `server/data/content/`.
Edit and save them while the server runs: the server reloads them within a second and logs
`Content reloaded.` If a file has a mistake, it logs what's wrong and keeps the last good version.
- A second player can join from a standalone build, a Multiplayer Play Mode virtual player, or
  another machine pointed at your IP. Anything that finds the port in use connects to the server
  already running there.

## Running the standalone server

```powershell
dotnet run --project server/Rth.Server
# options: --port 43600  --map server/data/maps/test.rthmap  --saves server/saves  --tick-ms 600
```

Type `players` to list who is online, and `stop` (or Ctrl+C) to save everyone and shut down. If
it's running when you press Play, the Editor connects to it instead of starting its own.
Friends connect on TCP port **43600**, so open that port on your router if they're not on your LAN.

Player saves are plain JSON files in `server/saves/`, one per player.

## Editing the map

The map is designed in the Unity scene. Mark things with the components from the
**R'th Map Tools** package:

| Component   | Effect |
|-------------|--------|
| `MapArea`   | The rectangle that's exported (1 Unity unit = 1 tile). Everything below goes under it. |
| `MapBlocker`| Nobody can stand on the tiles its colliders cover (rocks, trees, water). |
| `MapWall`   | A thin wall or fence on a grid line; blocks crossing that tile edge. |
| `MapSpawn`  | Where new players appear. |
| `MapNpcSpawn` | A creature lives here (`npcId` from `npcs.json`, optional wander radius). |

Tiles with no ground collider are blocked. **R'th > Export Map** writes
`server/data/maps/<name>.rthmap`, and the Scene view then shows blocked tiles in red and walls
in yellow (**R'th > Show Collision Preview** toggles it). Commit the exported map along with
the scene.
