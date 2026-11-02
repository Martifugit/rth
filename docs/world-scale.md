# World scale guide

Status: **proposed** (2026-10-22). Nothing existing has been rescaled. Every number marked
**Decision** changes existing art or scenes and waits on the owner, who walks the scale test
region (section 7) first. The measurements behind the numbers are in
[research 03](research/03-osrs-scale.md) (the 2004 reference) and
[scale-today.md](research/scale-today.md) (this game).

## 1. The idea

R'th is seen from above and from a distance, and the camera turns all the way round. Anything big
gets between the camera and the player. The 2004 game handled this by making **the whole world
squat**:

- a wall is barely taller than a person;
- the biggest tree is about twice a person;
- the ground rolls gently and rises only a few tiles per screen;
- "mountains" are one-storey rock blocks on raised ground.

On top of that it used two cheap camera rules: the camera tilts up over hills, and roofs vanish
when they are in the line of sight. It never made walls or trees see-through.

R'th already has most of the squatness (trees 2–4 tiles, a 5.8-tile bell tower as the tallest
thing in Tolling). What is out of proportion is the player, who is **1.94 tiles tall**, 1.24 ×
the reference's 1.56, so today's walls (1.7) stop at their shoulders.

**We copy proportions, not absolute sizes.** Sizes below are the reference's ratios to its player,
times our player. Terrain is the exception: slopes and cliffs are about walking from tile to tile,
so terrain rules stay in tiles.

## 2. Sizes

`k` is our player ÷ the reference's: 1.94 ÷ 1.56 = **1.24**.

| Thing | Reference | × k | Proposed | Today | |
| --- | --- | --- | --- | --- | --- |
| Player | 1.56 | 1.94 | **1.94** | 1.94 | **Decision D1**: keep the player as is (the alternative is in D1 below). |
| One storey, wall pieces, doorways | 1.88 | 2.33 | **2.6** (doors 2.1 high, 0.84 to 0.9 wide) | 2.6 in the kits | **Decision D2**, revised 2026-10-27 by the owner: at 2.3 the player barely fitted (his head at the door's top), so the storey is 2.6 and doors fit his build. |
| Roof | | | eaves at the storey line, ridge ≤ 1.6 above it | eaves 2.05, ridge 3.3–3.6 | Follows D2. |
| Low walls (dry stone, garden, ruins) | 1.31 | 1.6 | **1.0–1.6** | 0.6–1.4 | Below the player's eyes, so they never hide a player. |
| Fences, hedges, railings | 0.5–0.7 | 0.6–0.9 | **0.6–0.9** | 0.6 rail, 0.8 posts | Unchanged. |
| Bushes, crates, barrels, mining rocks | 0.7 | 0.9 | **≤ 1.0** | 0.7–0.9 | Unchanged. |
| Ground decoration (flowers, rubble) | ≤ 0.75 | 0.9 | **≤ 0.9** | reeds ≤ 0.9 | Unchanged. |
| Ordinary trees | 2.3–3.0, 2 × 2 | 2.9–3.7 | **2.8–3.6**, block 1 × 1, crown ≤ 2.6 | 2.1–3.7 on 1 × 1 | **Decision D3** (footprints). Heights already fit. |
| Big trees (oak and up) | 3.4, 3 × 3 | 4.2 | **≤ 4.2**, block 2 × 2, crown ≤ 3.6 | oak 3.9 on 1 × 1 | **Decision D3.** |
| City walls, fortifications | ≤ 2.75 | 3.4 | **≤ 3.4** (about 1.5 storeys) | none yet | |
| Landmarks (towers, statues, monuments) | | | **up to 3 storeys plus roof (≤ 8)** | bell tower 5.8, monument 3.8 | Only with open ground around them (section 4). Both today's landmarks fit. |
| Cliff and mountain rock blocks | 1.88 | 2.3 | **one storey (2.3)** | none | Section 3. |

Free-standing things taller than about two storeys are rare and deliberate. If an asset needs to
look big, make it **wider, not taller**.

**Decision D1: the player.** Keep the player at 1.94 and build the world to it (this guide), or
shrink the player to about 1.6 (`HumanMale` at 0.82 scale) and keep today's walls and trees,
which then match the reference almost tile for tile (walls 1.06 × the player, the oak 2.4 ×).
Shrinking is one number but makes every 4 × 4 cottage roomier and every creature relatively
bigger; growing walls changes every house. **Proposal: keep the player, raise the storey.**

**Decision D2: the storey.** First 2.3, against 1.7 then; raised to **2.6** on 2026-10-27 so the player fits his doors (the kits in `tools/art/blender` follow it). The camera note below was worked out at 2.3. With the proposed camera (distance 12,
pitch 35°, focus 1 tile up) a 2.3 wall hides the camera's focus only from within 1.9 tiles in
front of the player; today's 1.7 wall from within 1.0.

**Decision D3: tree footprints.** A big tree blocks 2 × 2 tiles instead of 1, so a grove reads as
solid and the player can't stand inside a crown. Ordinary trees stay 1 × 1. Both kinds keep their
crowns inside a circle of their stated width, so a crown never reaches more than a tile past
the trunk's blocked tiles.

## 3. Terrain and verticality

**Decided (owner, 2026-11-02, corrected 2026-11-03): the world has real verticality, planned.**
Dramatic cliffs and heights are welcome; map verticality is part of what makes a world feel modern.
The old limits here (cliffs of 1 to 3 tiles, 16 tiles for the whole world) were what the 2004 game
could manage, not a design goal, and they are gone. What makes height work is **planning**: every
height is there for a reason in play or in the scene, and every climb is designed. "You can't just
slap on a few metres here and there and call it better." What we don't want is the first-person
open world's mountain range, big walkable slopes that exist for their own sake.

**The first pass was rejected** (2026-11-03): Tolling raised on a knoll with a cliff bank, Cruor in
three tiers of cliffs. Height was put where the content is, suddenly and over small areas. **Towns
and cities are relatively flat** (not machine-flat, but little change in elevation across them);
their verticality comes from **storeys, towers, walls, basements, crypts and sewers**, not from
uneven ground. A city may stand on a hill, but the hill rises outside it. The worked example is
[areas/heartland-vision.md](areas/heartland-vision.md).

**The rules**
1. **Every area has a vision before it has heights** (its brief, [world-areas.md](world-areas.md)):
   what is done there, its points of interest, what is below ground, its futures (a year, five, ten)
   and why low, mid and late players come. Its vertical plan follows from that: each height names
   its **job** (a reveal, a threshold, a wall with a way through, a narrow, a vantage, depth below,
   or room for a later change) and its routes.
2. **Busy ground stays level.** A town, a yard, a camp, a quest spot changes by about a tile or so
   across it, gently. Height there is built: towers to climb, upper floors (`MapLevel`), wall-walks,
   quays and stairs; and below: cellars, crypts, drains, undercrofts (small instanced regions).
3. **Relief is landscape-scale.** Land rises over long distances (slopes of about 1 in 7 or gentler
   on walkable ground) for a reason in how the land formed: rivers cut, lakes undercut, uplands rise
   towards mountains. Big shapes are few and read from far off.
4. **A sheer face is a set piece:** one or two an area at most, where there is room and little
   content, each with its routes (a combe, a ledge path, steps cut in the rock, a stair, a
   shortcut). A long cliff with one way up is a place; bumps are not.
5. **Every climb is a route,** and the top pays off: the landmarks below, the way ahead, the way
   back. Within the fog's reach (85 tiles), or through a lookout that earns a longer view.
6. **Mountains are mostly backdrop:** impassable rock crossed at passes, walkable high ground only
   where it has a purpose.
7. **Depth goes below.** The biggest vertical content is under the ground, where it costs the
   overworld nothing: dungeons as instanced regions, entered by a `climb`.

**Numbers** (guides for the plan, not caps):

| | Height (tiles) |
| --- | --- |
| Busy ground (a town, a yard, a camp): change across it | about 1 to 1.5 |
| Lowland: river meadows, fens, shores | 0 to 6 |
| Hills, downs, swells | 4 to 16, over long slopes |
| Uplands, plateaux, foothills | 10 to 24 |
| Mountains (backdrop, passes cut through) | up to about 60 |
| A low step: a bank, a terrace edge | 1 to 3 |
| A set piece: a river bluff, a gorge wall, a sea or lake cliff, a scarp | 6 to 30 |

The player is 1.94 tiles tall and a storey 2.6, so a cliff under about 4 tiles reads as a wall,
not a cliff. Heights are kept between -4 and 64 by every tool (`TerrainText.Lowest`/`Highest`).

**How it works in the game.** The server is flat: a tile rising more than its region's `cliffStep`
(1 tile by default) across its corners is a cliff and blocked, so a cliff of any height is one line
of blocked tiles at its face. A walkable slope rises less than a tile per tile; a road up a cliff
switches back along it. The camera tilts up over high ground near the player
(`CameraRules.LiftFloor`), so a tall cliff behind the player doesn't hide them; ledge paths are at
least 2 tiles wide. Nothing below -0.3 shows (the unbuilt land under the world), so depth is carved
into raised land: a gorge is cut into a plateau, not dug below the sea.

**Tools.** Strokes plans (`world/strokes/`): `shapes` raise or lower land (`raise`, a `cliffSide` for
one steep face, `rock` on what is steep), islands and lakes; rivers cut their beds and gather steep
stretches into falls. In Unity: **Edit Terrain** (Unity's terrain tools over the whole world) and
**Shape and Paint Ground**. Check a plan from the player's path in Play mode.

**Water** has **one sea level** for the whole world (proposed: height 0, so land is 0 and up).
Lakes and rivers sit at their own level, which steps down only at falls and weirs, and every
river runs downhill to the sea.

## 4. Towns and buildings

- **Mostly one storey**, a few two; three only for landmarks. Every roofed building has a
  `RoofHider` whose interior covers the tiles under its roof, so the roof hides when it is in the
  line of sight (section 5).
- **Streets are at least 2 tiles wide; main streets and squares 4 or more.** Tolling's roads are
  about 2 today, its square 11.
- **No alleys between buildings over one storey.** At the default camera the player would be
  hidden for the whole alley. Two storeys need 3 tiles between them, or a roof that hides.
- **Landmarks stand on open ground** (a square, a green, a crossroads) at least as wide as the
  landmark is tall. Players should see it from across town and never have to walk behind it.
  Tolling's bell tower (5.8) on its 11-tile square already does this.
- **City walls stay low** (≤ 3.4), with gates at least 2 tiles wide and no roof over the
  gateway, so the player is not hidden walking through.
- **There is no safe side.** The camera turns all the way round, so a tall thing hides the player
  from some angle wherever it stands, except at the edge of the world.
- **Interiors that are their own region** (the Saint's crypt: 14-tile piers, 20-tile walls) can
  break the size rules: they are seen from inside, keep their tall pieces against the walls, and
  fade what crosses the view (`Occluder` with a guard radius).

## 5. The camera

**Decision D4: camera limits.** `CameraRig` carries both sets as **profiles**, switched in
**Settings > General > Camera**, so the owner can compare them in play. "Today" stays the default
until this is decided.

| Setting | Today | Proposed | Why |
| --- | --- | --- | --- |
| Start distance | 14 | **12** | With pitch 35°: about 4.8 tiles of ground towards the camera, 26 beyond (into the fog) and 17.7 wide at the player (16:9), against 20.6 wide today. |
| Zoom | 5–30 | **6–30** | 5 puts the camera inside a two-storey house. 30 keeps the overview. |
| Start pitch | 45° | **35°** | More ground ahead of the player; more of the world's side faces. |
| Pitch range | 20°–80° | **22°–67°** | The reference's range. Below 22° walls and trees hide the player from far off and the far edge is the horizon; above 67° the view loses all depth. |
| Field of view | 45° | 45° | Kept: wider than the reference's 36°, for a 16:9 screen. |
| Focus height | 1.0 | 1.0 | Kept: the player's chest. |

With the proposed start and D2 sizes, a wall hides the focus only from within 1.9 tiles and the
biggest tree (4.2) from within 4.6. At today's start the figures are 1.3 (a 2.3 wall) and 3.2.

**Two rules from the reference**, both on whichever profile is chosen, each with its own
switch in the same settings card:

1. **The camera lifts over terrain.** The highest ground within 4 tiles of the focus, minus the
   ground under the player, sets a pitch floor of **16.875° per tile** (2 tiles: 34°; 4 or more:
   the maximum). The floor eases up quickly and back down slowly (1/24 and 1/80 of the gap per
   20 ms), and the camera uses the higher of the floor and the player's own pitch, so the
   player's chosen pitch comes back when the ground falls away. On flat ground it does nothing.
2. **Roofs on the line of sight hide.** Below 54.5° pitch, the tiles on the line from the camera
   to the player are walked; a roof whose interior contains one of them, and whose top is above
   the line of sight there, is hidden (as if the player were inside), along with any upper
   floor. Above 54.5° roofs only fade as before.

Trees, walls and landmarks still fade with `Occluder` when they cross the view. Once the sizes
in section 2 are in, the plan is to remove fading from walls and ordinary trees and keep it for
landmarks and the few big things (a later decision, after playing with it).

## 6. How ground gets height

- **`MapTerrain`** (in `tools/unity-map-tools`): a heightfield over a rectangle of tiles, one
  height per tile corner, with a colour per tile blended over a few tiles. It builds its own
  mesh and collider, so it is ground to the exporter like any other collider.
- **Cliffs come from the data:** the exporter blocks every tile whose corners differ by more than
  `cliffStep` (drawn in the cliff colour), so the server's collision matches the shape without
  hand-placed blockers.
- **The client reads heights from it directly** (`GroundHeight.At`): characters and ground items
  stand on it, and the camera's terrain lift reads it. Where there is no `MapTerrain` the
  ground is at 0, as today, so existing regions are unchanged.
- The server does not need heights: movement and line of sight are on the tile grid. If a rule
  ever needs height (ranged attacks downhill), export a height file beside the map then.

## 7. The scale test region

**R'th > Dev > Rebuild Scale Test Scene** builds `ScaleTest.unity` (`ScaleTestBuilder`), a region at
tiles **(0, 96)–(95, 191)**, just north of Tolling, through the normal pipeline (`WorldKit`, the
exporter, streaming). Its south edge meets Tolling's north edge: walk north from the meadow
west of the mill, up the road at x 28–29. The ground is a `MapTerrain`; the town part is flat.

| Area | Tiles | What to look at |
| --- | --- | --- |
| Storey street | street y 117–120, x 8–63 | Houses at today's 1.7 on the south side, the proposed 2.3 on the north, facing each other. |
| Alley | x 52–53, y 121–129 | Two two-storey houses (2 × 2.3) with a 2-tile alley between: walk it at both camera profiles and see how long the player is hidden. |
| Square | x 64–80, y 109–126 | A three-storey landmark tower (6.9, about 7.9 with its roof) alone on an open square. |
| Grove | x 10–33, y 138 (today) and 143–144 (leafy) | Today's block trees beside the leafy small (3) and large (4) trees. |
| Hill | centred (60, 146), radius 11 | A gentle 4-tile rise with a few trees on it: the camera tilts up as you walk towards it and settles back slowly after. |
| Cliff | plateau x 40–70, y 170–182; ramp x 66–69 from y 158 | A 2-tile cliff you cannot walk up, and a ramp at its east end. |
| Terraces | x 74–95; steps at y 148, 160, 172; ramps x 90–93 | Three 1.5-tile terraces joined by ramps. |
| Field | x 6–22, y 156–170 | A fenced field with a gap in the north fence. |
| Base noise | x 0–35, y 176–191 | Untouched ground with the reference's rolling noise. |

Change a constant at the top of `ScaleTestBuilder` (the storey, the hill, the cliff, the terrace
step) or a tree spec in `LeafyTree` and rebuild to try another value.

## 8. Work after the decisions

1. **D4:** make the chosen camera profile the default.
2. **D2:** `WorldKit.Wall`'s default height and `House`'s roof, then rebuild Tolling and
   Palmerway (this overwrites hand edits, so check for any first).
3. **D3:** `WorldKit.Tree` replaced by the leafy trees, with 2 × 2 blockers for big ones.
4. A scale check in the art validator that warns on a model taller than its kind allows, a
   region with more than 3 % cliff edges, or relief over 12 in any 24 × 24 window.
5. The world layout, with terrain ([world-building.md](world-building.md)).
