# R'th

A private multiplayer RPG for a group of friends: RuneScape 3-style play, Old School RuneScape-style
looks, in an original world. See `CLAUDE.md` for the architecture, `docs/` for the design and
`docs/lore.md` for the world (the canon; the game still carries some older lore until it catches up).

## What you need

- .NET SDK 10
- Unity 6.6 (6000.6.6f1) with a Unity Personal licence

## First-time setup

```powershell
dotnet build        # builds everything and copies the shared DLLs into client/Assets/Plugins/Rth
dotnet test         # server, shared and the client's Unity-free maths; a few seconds, no Unity needed
```

Run `dotnet build` **before** opening the Unity project the first time, and again whenever you
change anything under `/shared` or `/server`. Unity uses the built DLLs, and without them it opens
in Safe Mode with missing-type errors.

Then open `client/` in Unity and load `Assets/Scenes/Game.unity` (or any region scene, such as
`Assets/Scenes/Tolling.unity`).

## Playing in the Editor

Press Play. The Editor exports any open region scenes and starts a local server in the same
process, which loads every region in `server/data/maps/` (turn this off under **R'th > Start
Local Server On Play**). Type a name and log in. Region scenes stream in and out as you walk;
pressing Play in a region scene loads the Game scene beside it.

- Left-click a tile (or a spot on the minimap) to walk there. **R** toggles running (2 tiles per tick).
- **The interface is made of windows**: drag a title bar to move one (edges snap), drag the
  corner of a resizable one to resize it, drag a tab out to make its own window or onto
  another window to combine them. The buttons under the minimap open the world map, the
  **journal** (quests, achievement diary, bestiary), the **guide** (skill unlocks, abilities),
  settings and a menu of every window. Keys: **B** pack, **E** worn, **A** abilities,
  **F** religion, **K** skills, **Q** quests, **J** journal, **G** guide, **M** world map,
  **Enter** to chat, **Escape** closes. Settings > Controls lists them all.
- Left-click a creature to attack it. **1 to 0, -, =** (or clicking the action bar) use what's
  on the main bar. Drag abilities from the book, or items from your pack, onto any bar; drag
  a slot off the bars to clear it. Hover a slot to see what it does.
- **Hymns** (F, the Religion tab) are prayers for every style: protection from melee, ranged or
  occult, boosts, life steal. Click one to sing it (click again to stop), or drag it onto an
  action bar. They drain **devotion**, which only comes back by praying (the waystone east of
  Tolling has a Pray option), drinking a devotion draught, or dying. Higher hymns need Religion
  levels; singing in combat trains Religion a little.
- **Your weapon decides your style.** Empty hands or the athame fight with occultism; a sword
  or mace (you start with a watchman's sword in your pack) fights with **melee**: it changes
  your auto-attack, and abilities need a weapon of their style. The ability book (A) has a tab
  per style and opens on the one in your hand. Melee abilities hit harder the lower your blood,
  and some leave the target **bleeding**.
- **Verses** (the Religion tab, under the hymns) are Cruor's prayers for melee: read in combat
  with a melee weapon, each costs blood and a **scripture**, and changes how the fight goes (life
  steal, a blocked blow, damage held back as a bleed, scars that make an ultimate free...). Drag
  them onto a bar like abilities.
- **Cruor's monument** stands by the road in Palmerway. Empower its founts (Religion: they cut
  you a little, then it's AFK) for XP and sometimes **pledges**; now and then a fount wakes
  hungry and glows, and is worth moving to. **Brother Hollis** beside it trades scriptures for
  pledges, and arms for marks.
- **Right-click** anything in the world for all its options (attack, take each item on the tile,
  walk here). The top-left text shows what a left-click will do.
- The side window at bottom right has your **Pack** (click an item to use it: eat, drink, wield;
  right-click for drop and examine; drag to rearrange), what you've **Worn**, your
  **Character** stats, the ability **Book**, **Religion** (hymns and verses), **Skills**, **Quests**,
  your **Task** and **XP** gained this session.
- Creatures drop their **carcass**. Take it to the right workstation and click it to
  **butcher** it for the loot: fen rats at the skinning rack by the spawn (you start with a
  skinning knife), vitriol mites at the lye vat in the north-east (needs butchering 10 and a
  glass flensing hook, which rats sometimes yield). You keep going until you run out.
- Ground items are only visible to you at first; after a minute anyone can see them, and after
  three minutes they're gone.
- **Townsfolk**: left-click to talk (right-click for Bank or Trade). A conversation shows what
  they say (Space continues), then your answers (number keys or click); Escape closes.
  - **Clerk Ambrose** by the red strongbox runs your **bank** (the strongbox works too). Your
    pack and worn items show beside it: click to deposit. Choose how many a click moves (1, 5,
    10, X, All) and whether items come out as stackable **bank notes**.
  - **Corvin Sallow** in the hut sells food, draughts and reagents, and buys things back. Ask
    him about the humours once you're a decent butcher.
  - **Wenna Thole** at the skinning rack hands out butchering tasks for **yard tokens**,
    spent in her shop. Your current task and points are in the **Task** tab.
- Use an ability while running to your target and it **stalls**: it goes on cooldown now and
  fires when you arrive, so the next one can follow on the same tick. Walking away keeps your
  target while you're in combat: press an ability and you run back and fire it. **Settings > Combat**
  turns ability queueing on or off and sets **Revolution** (the main bar fires builders, or
  everything, by itself).
- Bile shows as **statuses** in the buff and debuff bars above the action bar: yellow bile makes
  you **infused** (more damage dealt) and **choleric** (more damage taken); black bile makes you
  **warding** (less damage taken) and **melancholic** (less damage dealt). Hover one for its numbers.
- **Distillation** at Corvin's still (part of Occultism now) turns butchering galls and glands
  into salts and tinctures (abilities cost only phlegm and bile).
- **Quests**: someone is talking in her sleep by the pond. The **Quests** tab lists them; click
  one for the journal, which tells the story so far. The journal also holds the **achievement
  diary** and the **bestiary** (kill counts and drop logs).
- The **Skills** tab shows all sixteen skills, your combat level and total level. Occultism and
  Melee (from fighting in that style) unlock spenders at 5 and ultimates at 10, and add that
  style's ability damage per level.
  Combat XP is split with **Defence** (more max blood per level) by Settings > Combat > Train
  defence. Most other skills have no content yet: see `docs/skills.md`.
- Arrow keys or middle-drag rotate the camera; the scroll wheel zooms.

**Tolling**, the riverside town where you wake: the square and bell tower, the Synod treasury,
Corvin's apothecary, Wenna's skinning yard south of town (and the sleepers' graves past it), the
training yard with its straw penitent, Widdermill and its millpond to the north, fen rats in
the reed beds along the river, and over the bridge, the east woods and Sour Hollow, where vitriol
mites nest around a leaking cask (aggressive; they spit from range). Wennow, a broad mere, is to the south-east.
Tolling sits at the far western edge of Cruor's lands.

**Palmerway** leaves Tolling to the east: open heath along Wennow's north shore, a
waystone, the shell of an old toll-house, and the wardens' palisade where the road to Cruor is
barred (for now, the edge of the built world).

## Tuning content

Abilities, creatures, combat rules, items, drop tables, the starter kit and death (the ferryman's
fee and lines, `death.json`) are JSON in `server/data/content/`.
Edit and save them while the server runs: the server reloads them within a second and logs
`Content reloaded.` If a file has a mistake, it logs what's wrong and keeps the last good version.
- A second player can join from a standalone build, a Multiplayer Play Mode virtual player, or
  another machine pointed at your IP. Anything that finds the port in use connects to the server
  already running there.

## Running the standalone server

```powershell
dotnet run --project server/Rth.Server
# options: --port 43600  --maps server/data/maps  --spawn tolling  --saves server/saves  --tick-ms 600
```

Type `players` to list who is online, and `stop` (or Ctrl+C) to save everyone and shut down. If
it's running when you press Play, the Editor connects to it instead of starting its own.
Friends connect on TCP port **43600**, so open that port on your router if they're not on your LAN.

Player saves are plain JSON files in `server/saves/`, one per player.

Fonts and icons are third-party, used under their licences: see `CREDITS.md`.

## Editing the map

The world is made of **regions**: each is one Unity scene with one `MapArea`, placed at absolute
tile coordinates (Tolling's south-west corner is (0, 0); Palmerway starts at (96, 0)).
Region origins and sizes are multiples of 8 and regions must not overlap. Space with no region
is blocked (and drawn as plain land in the fog). Region scenes hold only the map; the camera,
light, fog and HUD are in `Game.unity` (**R'th > Dev > Rebuild Game Scene**).

To add a region, write `world/regions/<id>.json` (`docs/workflow.md`) and run Sync World,
which creates its scene and adds it to the build, which is how the client finds it. Most regions between places should be
sparse: a road, a few trees and rocks, a landmark.

Mark things with the components from the **R'th Map Tools** package:

| Component   | Effect |
|-------------|--------|
| `MapArea`   | The region's rectangle (1 Unity unit = 1 tile), export name and title. Everything below goes under it. |
| `MapBlocker`| Nobody can stand on the tiles its colliders cover (rocks, trees, water). |
| `MapWall`   | A thin wall or fence on a grid line; blocks crossing that tile edge. |
| `MapSpawn`  | Where new players appear. |
| `MapNpcSpawn` | A creature lives here (`npcId` from `npcs.json`, optional wander radius). |
| `MapObject` | An interactive object, e.g. a workstation (`objectType` from `objects.json`). Pair with `MapBlocker`. |

Tiles with no ground collider are blocked. **R'th > Export Map** writes
`server/data/maps/<name>.rthmap` (plus `.region.json`, `.spawns.json`, `.objects.json`), and the Scene view then shows blocked tiles in red and walls
in yellow (**R'th > Show Collision Preview** toggles it). Commit the exported map along with
the scene.
