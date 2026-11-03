# The heartland (wave 1): what is there, what players do, and the land for it

**Status:** proposed (2026-11-03, second draft), for the owner. Replaces the first levels pass
(2026-11-02), which was rejected as arbitrary height. The first draft of this plan was rejected too:
it described story instead of things in the world. This draft lists only **what is physically
there and what a player does with it**: buildings, stations, nodes, creatures, entrances, routes,
the levels they are for, and the ground they stand on. Story belongs to dialogue and quests; it is
left out here except where it decides an object.

Counts and levels are proposals, tuned at stage 3. Object ids in `code` exist in
`server/data/content` today; anything else is new. Coordinates are tiles.

The areas: **Tolling and Wennow** (the start), **the western march** (the first walk west),
**Knellmere and the lake road** (the first long road), **Cruor and the meander** (the capital).

---

## 1. Rules for height in these areas

- **Busy ground is level:** a town, yard or camp changes by a tile or so across it. Height in a town
  is built: towers with a ladder to a view point, upper floors, wall-walks, quay steps.
- **Below ground is where the big depth goes:** cellars, crypts, caves, drains, as instanced
  regions entered by a `climb` object.
- **Every height outside a town does a job you can name in play:**
  - it makes a destination (a gorge with a cave at its end);
  - it makes a shortcut (steps out of it);
  - it holds nodes (a quarry face, a gorge wall);
  - it makes a view point you climb to;
  - it gates a place behind one or two routes (a cliff with a combe);
  - it keeps ground clear for a later event.
- If a height does none of these, it isn't there.
- **The fog reaches 85 tiles,** so a view point shows what is within about 85 tiles of it. Each view
  point here lists what is actually in range.

---

## 2. Tolling and Wennow (levels 1 to 15)

### What a player does here

| Activity | Where | What is physically there | Levels |
| --- | --- | --- | --- |
| Wake up, first steps | the dead-house under the bell tower (below) | the spawn; stairs up into the tower's ground floor, a door onto the church close | new |
| Bank | the church house, south side of the square | `synod_strongbox` behind a counter, Clerk Ambrose | all |
| Buy potions, tools | the apothecary; the smithy | Corvin (shop); the `tinker` in the smithy (tools) | 1+ |
| Smithing | the smithy, on the west road | `furnace`, `forge`, an anvil; copper and iron bars, tier 1 to 2 tools and gear | 1 to 20 |
| Cooking | the church house's back kitchen; campfires anywhere | `range` | 1 to 20 |
| Butchering | the skinning yard, down by the river | `skinning_rack`, `lye_vat`, Wenna (tasks, token shop) | 1 to 15 |
| Medicine | the apothecary | `apothecary_bench` | 1 to 20 |
| Mining | **the quarry**, north of town on Hollow lane (below) | 5 `copper_seam`, 3 `ironstone_seam` in its face | 1 to 15 |
| Woodcutting | the river below the mill; the west road's hedgerow | 6 `alder_tree` in one stand (tending), 3 `ash_tree` | 1 to 15 |
| Fishing | the fishers' landing; the river by the bridge; the Wenn's outlet | 2 `river_spot` (perch), 1 `eel_weir` at the outlet, 1 `perch_rise` (trophy) off the landing | 1 to 15 |
| Foraging | verges of the four roads; the river mouth | 6 `yarrow_patch`, 2 `rue_patch` at the edge farm, 2 `berry_bush`, 1 `reed_bed` | 1 to 15 |
| Combat | the river mouth's reed margin; the dry rise | 9 `fen_rat` (reed margin); 6 of a new tier-2 creature on the dry rise (about level 8, gives a carcass) | 1 to 10 |
| Religion | the monument plot on Palmerway (78, 50) | `monument_fount` x4, Brother Hollis (pledges for scriptures) | 1 to 10 |
| Quests | the town | The Bell (dead-house, church house, apothecary, landing, pond, church); Dreamless (the pond, Corvin's still) | new |
| Climb the bell tower | the church close | a ladder in the tower to the bell chamber; a view point at the top | after The Bell |

### Places and what they hold
- **The town:** 13 buildings (as built, finding 12). Each building that matters gets a job above. The
  houses hold Edda, the bell-ringer and stage-3 townsfolk. The square has the fingerpost, the notice
  board and two stalls.
- **The quarry** (new, about (40, 70), east of Hollow lane): an open pit about 14 by 10, 2.5 deep,
  with a cart ramp in from the lane. The rocks are in its north and east faces. It's the town's one
  dug-out place, and it gives Mining a home that isn't a rock dropped on grass.
- **The bell tower:** three floors:
  - the ground floor, with the stairs down to the dead-house;
  - the ringing chamber (plane 1);
  - the bell chamber (plane 2), with a view point (`view` action, a message).
  - In range from the top (85 tiles): the whole town, the mill, the quarry, the monument on
    Palmerway, Wennow's west half and the fishers' landing.
- **Widdermill** (35, 80): the miller (an errand), the wheel, an upper floor (plane 1) with the
  millstones. No station until there is something to grind.
- **The fishers' landing** (109, -31): a hut, a jetty, two boats; the fishers (The Bell's step 4);
  the perch spots and the trophy rise.
- **The dry rise** (123, -49): a gravel hill about 6 high, with a rock tor on top and the cairn as a
  view point (in range: Wennow, the landing, the edge farm). Its slopes are the area's one hunting
  ground: the tier-2 creature.
- **The edge farm** (38, -50): a longhouse and byre in a walled yard. The farmer gives small
  errands, and 2 rue patches stand in the yard.
- **The waygate** (180, 48) on the heath ridge's crest: the gatehouse (open) and the wardens' hut.

### Below ground

| Place | Entrance | Size | Inside | For |
| --- | --- | --- | --- | --- |
| The dead-house | stairs in the bell tower's ground floor | about 10 x 8 | the bier where new characters appear; the bell-ringer at the foot of the stairs (The Bell step 1) | every new player |
| The church crypt | a trapdoor in the chapel's floor, opened in The Bell | about 16 x 12 | ossuary niches, the ledger chest (step 6's object) | The Bell, Dreamless's follow-up |
| Corvin's cellar | a trapdoor behind the apothecary's counter, after Dreamless | about 10 x 10 | the hidden `apothecary_still` (recipes that need it: Occultism's first reagents), shelves to read | 10+ |

### Why each kind of player comes
- **New players:** everything above. The ring round the town is walked in the first hour.
- **Mid players:** the bank and smithy when they're passing; Corvin's cellar still; respawning after
  the ferry (`death.json` `returnTo` is null, so a paid ferry brings them to the spawn here).
- **Late players:** rarely, and that's accepted. The respawn, quests that come back to the town, and
  the trophy perch.

### Later content slots (space kept, nothing built)
- **The edge farm's yard:** room for 4 farming plots if Farming is ever added.
- **The square's east side:** a plot of about 8 by 6 for one more building (an inn) when Tolling
  needs a place for players to meet.
- **The crypt:** a wall to open into a second, older vault for a later quest.

### The ground for it
- **The town on a level river terrace,** 1.5 to 2.5 over the river (0.1), varying by about a tile
  across the town:
  - the church close on the highest corner (about 2.5);
  - a grassy bank of about 1.5 down to the river on the east, with Palmerway rising gently off the
    bridge into the square;
  - the skinning yard down on the meadow at about 0.5;
  - the quarry dug into the terrace north of town.
  - The first pass's 4.6 knoll, its cliff bank and its cutting go.
- **The dry rise:** a gravel hill about 6 high with slopes of about 1 in 8, so it's walkable from
  every side, and a tor of rock about 2 high on top. The first pass's 7-tile face goes.
- **The heath ridge:** a broad swell with its crest about 4.5 and slopes of about 1 in 10. Palmerway
  climbs it in a straight line to the waygate on the crest. The rock face goes.
- **Everything else** is the basin floor at 0.1 to 1.5, with Wennow as reshaped (finding 11).

---

## 3. The western march (levels 5 to 30)

### What a player does here

| Activity | Where | What is physically there | Levels |
| --- | --- | --- | --- |
| The first dungeon | the cave at the head of the gorge (below) | an entrance by the fall | 5 to 15 |
| Mining | **the gorge's walls**; the salt road's cutting | in the gorge: 4 `copper_seam`, 4 `ironstone_seam`, 3 `hematite_seam` (the nearest hematite to Tolling); in the cutting: 3 `ironstone_seam` | 1 to 30 |
| Hunting | the plateau | 8 of a new tier-2 creature (about level 12, a carcass for Butchering); 4 of a tier-3 one near the spur (about level 22) | 10 to 25 |
| Foraging | the plateau and the farm | 4 `rue_patch`, 3 `feverfew_patch`, 2 `flax_patch` at the farm | 10 to 25 |
| Farm errands | p-west-march (-30, 110) | a farmhouse, a barn; the farmer; a bank chest (`bank_chest`) in the farmhouse, the area's only bank | 5+ |
| The observatory | p-observatory (-94, 140) | 3 buildings: the dome tower (a ladder to the telescope room, a view point), the watchers' hall, a workshop (`instrument_bench`, for Artifice when it exists); 2 to 3 watchers | 10+ |
| Ranged practice (when Ranged exists) | the spur, by the observatory | a row of targets along the spur's level top | 1 to 30 |
| The salt road | from Tolling west to the pass | caravans (`caravaneer`) walking it on a patrol; a waystation at the crest (a shelter, a well, a cairn as a view point) | all |
| The vault | s-observatory-vault (-112, 124), under the spur (below) | a sealed door in the cliff below the spur | 30 to 50 |

### Places and what they hold
- **The gorge** (about x -45 to -15, y 135 to 175): Tolling's river's cut into the march.
  - A bank path 3 to 4 wide runs along the river, the rocks along the foot of its walls.
  - At its head is a fall of about 5, with the cave entrance beside it.
  - Steps cut beside the fall climb out of the gorge's head onto the plateau. They make a loop:
    walk in along the river, mine or do the cave, and leave by the steps onto the plateau without
    walking back out.
  - A fallen ash across the gorge halfway along is a Woodcutting shortcut (level 15) between the
    two rims.
- **The salt road's crest** (about (-60, 50)): the waystation and the cairn view point. In range:
  the plateau, the gorge's rim, the observatory, and the high valley falling away west.
- **The observatory's spur:** the three buildings on its level top. The dome's telescope room is
  the highest point in the area. In range: the whole march, Tolling's west edge and the high
  valley.

### Below ground

| Place | Entrance | Size | Inside | For |
| --- | --- | --- | --- | --- |
| The cave | a mouth in the gorge wall beside the fall | about 48 x 40, two levels | upper: a mouth chamber and passages, 8 cave rats (tier 1 to 2); a ladder down about 4 to a lower chamber with an underground pool (1 `river_spot`, eels) and 4 of a tier-2 cave creature; at the far end a named tier-2 creature (about level 15) guarding Corvin's item, and a dressed-stone door that doesn't open (an examine line; kept for later) | 5 to 15 |
| The observatory vault | a door in the spur's cliff, reached from the high valley below; a hatch in the dome's floor opens it from above once solved | about 32 x 32 | instrument rooms, a puzzle (align three telescopes), guardians (tier 4, about level 35), a chest | 30 to 50 |

### Why each kind of player comes
- **Early:** the cave, the first hematite, the farm's bank and errands.
- **Mid:** the plateau's tier-3 creature, the vault, the observatory's Artifice and Ranged when
  they exist, and the salt road through to the pass.
- **Late:** passing through on the salt road, and the moonfall event below.

### Later content slots (space kept, nothing built)
- **A moonfall site:** an open stretch of plateau about 60 by 40, north of the salt road between x
  about -80 and -40, with no roads, sites or nodes in it. When moonfalls exist it holds the event:
  a crater with verdigris rocks (Mining tier 8), a crater pool with lightfin spots (Fishing tier 8),
  moonborn creatures, and a world boss for the largest.
- **The cave's far door:** opens into a later dungeon (a mine) without changing the cave.
- **The crest:** a plot about 20 by 20 beside the waystation for a fort, if the salt road later gets
  a garrison and tasks.

### The ground for it
- **The march climbs openly** from Tolling's west edge (about 1.5) to about 10 at the salt road's
  crest, about 1 in 7 on average, in long swells. It then falls to about 4 in the high valley before
  the salt pass. The salt road climbs it with no switchbacks.
  - The first pass's escarpment, a 6-tile face down the march's whole east side, goes.
- **The gorge:** walls of 8 to 12, about 60 tiles long, the river and its bank path inside, the fall
  of about 5 at the head, and the steps beside it.
- **The spur:** a promontory off the plateau about 15 high, its level top holding the observatory.
  Its west and north sides are cliffs of about 10 over the high valley, with the vault's door at
  their foot. A lane runs along the spur from the plateau.
- **The moonfall site:** level, open plateau.
- **Sere's valley** low and green in the south-west.

---

## 4. Knellmere and the lake road (levels 5 to 50, fishing to 99)

### What a player does here

| Activity | Where | What is physically there | Levels |
| --- | --- | --- | --- |
| Fishing | the whole shore | about 10 `river_spot` (perch), 3 `eel_weir` in the fens, 4 pike spots (new, a lake spot like `river_spot` with pike) off the north shore and the isle | 1 to 30 |
| **Trophy angling** | Knellmere | `perch_rise`, `eel_rise` and `pike_rise` trophy spots round the lake, a few active at a time; the deep water east of the isle for the high species | 1 to 99 |
| Sell and rig | p-lakeport | the `fishmonger` (trophies to fish boxes), the `tackle_maker` (tackle, mending), the `collector` (revised fish) | all |
| Bank, cook | p-lakeport | a `bank_chest` in the harbour office; a `range` in the inn | all |
| Boats | p-lakeport's quay; p-lake-south's landing | a boatman at each: a ferry across the lake (p-lakeport to p-lake-south), and to the isle once The Bell's sequel opens it | 5+ |
| Fen rats, reeds | the reed fens (s-reed-fens, (210, 36)) | 10 `fen_rat`, 2 `reed_bed`, the eel weirs | 1 to 10 |
| The Downs | above the chalk cliff | 8 of a tier-3 downland creature (about level 22); 4 `feverfew_patch`; s-downs-fort (420, -140) with tier-4 enemies (about level 35) | 20 to 40 |
| The drowned village | under the lake, through the belfry (below) | a dungeon | 25 to 45 |
| The figure's eye | the hill figure on the cliff face (below) | a small chamber and a view point | 15+ |
| Milling | p-weir (356, 92) | the miller, the mill, `eel_weir` at the weir | 10+ |

### Places and what they hold
- **p-lakeport** (262, 70), level on the north shore, about 8 buildings:
  - the harbour office (bank chest);
  - the fishmonger's, the tackle shop and the collector's house;
  - the inn (range);
  - two houses and a boathouse.
  - The quay is a stone edge about 1.2 high with steps down to the boats and the jetty. It's the
    best place to watch the lake.
- **The isle** (300, -15): a low island of shingle and rock with the belfry standing in shallow
  water off its east end. On the isle are a landing, two pike spots and a pike trophy rise.
- **p-lake-south** (262, -28): a fishing village of 3 or 4 buildings at the foot of the combe. Its
  landing has the ferry.
- **The chalk cliff:** about 9 to 10 high along the lake's south-east lobe, with a shingle strip 8
  to 12 wide under it. The ways up:
  - **the combe** at p-lake-south: a dry valley breaking the cliff, the camp lane climbing it at
    about 1 in 8;
  - **the figure path:** a ledge 2 to 3 wide cut along the face from the combe's top to the hill
    figure;
  - **chalk steps** from the figure's foot down to the shingle: a Mining shortcut (level 20), which
    saves walking back round by the combe.
- **The hill figure** (330, -54): cut into the cliff's face and seen from p-lakeport's quay (about
  70 tiles away, in range). The figure path reaches its eye.

### Below ground and beyond

| Place | Entrance | Size | Inside | For |
| --- | --- | --- | --- | --- |
| The drowned village | the boat to the belfry; a ladder down inside the tower | about 64 x 64 | a village street on the lake bed, the water as a ceiling (green light): 8 house shells, a church, a well; 15 drowned creatures (about levels 25 to 40); lamprey spots in flooded cellars (Fishing tier 4); a boss in the church (about level 45); the twin bell, a quest item | 25 to 45 |
| The figure's eye | the end of the figure path | about 12 x 8 | a chamber behind the eye with an opening in the face, a view point over the lake, a shrine object (lore text) | 15+ |
| The chalk galleries | a shaft on the downs behind the cliff | about 40 x 40 | old flint workings: `hematite_seam`s and a few gem-rich rocks, tier-3 creatures | 20 to 35 |

### Why each kind of player comes
- **Early:** the road, the ferry, perch and eels, fen rats, the market.
- **Mid:** pike, the Downs' creatures, the chalk galleries, the drowned village.
- **Late:** trophy angling, which runs to 99 and is the area's reason to come back all game; and the
  Downs fort.

### Later content slots (space kept, nothing built)
- **A fishing contest at p-lakeport's quay:** a timed trophy-angling competition, a minigame with a
  leaderboard (a trial-like mechanism). The quay and the water in front of it stay clear of other
  spots.
- **The lake bed by the isle:** a shelf about 40 by 20, at a depth that a "low water" version of the
  region could uncover, with village ruins and nodes on it. That needs a region that can switch
  between two states, which is a new mechanism: only if such an event is wanted.

### The ground for it
- **Knellmere** at 0.1 as reshaped (finding 4), with the shelf by the isle and deep water east of it.
- **The north shore** rolls gently, 0.5 to 3. p-lakeport is level about 1.2 over the water. The
  first pass's terrace (about 2 over the quay) goes.
- **The isle** low, about 2 to 2.5. The first pass's height of 5 and its rock face go.
- **The reed fens** flat (0.1 to 0.4), with Palmerway on a causeway about 0.6 through them.
- **The Downs:**
  - the cliff of about 9 to 10 along the south-east lobe only, its ends dying into slopes where the
    shore turns away;
  - the top at 12 to 13, falling south to about 8;
  - the combe, the figure path, the brook's fall.
  - Most of this was built in the first pass (finding 8) and stays.

---

## 5. Cruor and the meander (levels 10 to 99)

### What a player does here

| Activity | Where | What is physically there | Levels |
| --- | --- | --- | --- |
| Bank | the counting-house on the main square | a strongbox counter, two bankers | all |
| Shops | the market square | about 6 stalls and shops: weapons (melee), armour, robes and occult supplies, tools, food, a general store | all |
| Smithing | the city smithy by the east gate | `furnace`, `forge`, anvils x2 (a busier forge: the draw shared by more players) | 10 to 50 |
| Cooking | the tavern | `range` | all |
| Religion | the First Monument in the main square; the cathedral | founts for Religion tiers 2 to 4 (levels 10 to 40); `vigil_altar` (pray, devotion); the cantor's hymn lessons | 10 to 40 |
| Melee training | the knights' yard by the west gate | training dummies (low Melee XP to level 20), the knights, Sister Odile (quests) | 1 to 20 |
| Quests | the notice board in the square; the cathedral; the knights | the empire's quests, sending players out along each road | 10+ |
| The cathedral tower | the cathedral | stairs to a gallery (plane 2) with a view point. In range: the loop, the confluence, the meander's monument, the lake's north shore | all |
| The wall-walk | the city wall | a walk along the wall top (plane 1) round the city, up and down by stairs in 4 towers | all |
| The drains | grates in the lower streets; outfalls in the bluff (below) | a dungeon | 25 to 40 |
| The old city | below the drains, and from the cathedral crypt (below) | a dungeon | 40 to 70 |

### Places and what they hold
- **The city** (322, 176): about 30 buildings within the budget of `docs/regions/cruor.md`:
  - the cathedral in its open precinct;
  - the main square with the First Monument, the counting-house and the market;
  - the knights' yard by the west gate;
  - the smithy by the east gate;
  - the tavern, the Letting House, the chapter house;
  - 6 to 8 homes in short rows.
- **The walls** follow the hill's brow, with 4 gates (west, south, north, east), each at least 2 wide
  with no roof over the gateway.
- **The Watch:** a terrace on the east bluff over the great river, with a view point.
- **The north stair:** a stair cut down the north bluff from a postern to the north bridge. It's the
  short way to the north road, saving the walk round the hill's west shoulder.
- **The meander's monument** (s-lake-monument, (282, 132)): founts for Religion tier 1 to 2, on the
  road in.
- **The towpath:** a path along the great river under the north and east bluffs, from the north
  bridge to the confluence. It reaches the drains' outfalls.

### Below ground

| Place | Entrance | Size | Inside | For |
| --- | --- | --- | --- | --- |
| The drains | 3 grates in the city's lower streets; 2 outfalls in the bluff face, reached from the towpath | about 96 x 64 | brick tunnels with walkways beside the channels; 15 drain rats and thieves (about levels 25 to 40); a smugglers' room with a shop that buys anything (a fence); a way down to the old city | 25 to 40 |
| The old city | from the drains' lowest point; a stair in the cathedral crypt | about 96 x 96 | buried streets and houses, the catacombs; undead (about levels 40 to 70); `hematite_seam`s in an old quarry; the Saint's crypt (`SaintsCrypt`, existing) joined at its door | 40 to 70 |
| The deepest | a sealed door in the old city | not built | kept for an end-game dungeon and boss | 80+ |

### Why each kind of player comes
- **Early:** arrival, the bank, the shops, the knights' yard, the meander's monument.
- **Mid:** the drains and the old city, the empire's quests, Religion at the First Monument, the
  busy forge.
- **Late:** the old city's depths, the end-game door, and the city as the place players meet: every
  road out starts at one of its gates.

### Later content slots (space kept, nothing built)
- **The south slope outside the walls:** about 100 by 40, kept as open gardens. It's room for an
  outer district (more shops, guild halls, player-facing services) when the city needs it.
- **The deepest undercroft:** the sealed door, for an end-game dungeon.

### The ground for it
- **One broad hill:**
  - the meadows at 0.3 to 1;
  - the walled top at about 7 to 8, varying by a tile or so across the city;
  - the cathedral precinct on a gentle crown at about 8.5.
  - The first pass's three tiers (6, 10.5, 14) and their inner cliffs go.
- **The rise happens outside the walls:**
  - **the neck** on the west, a ramp of about 1 in 12 over about 100 tiles, which the lake road
    climbs to the west gate;
  - **the south slope** to the meander at about 1 in 8, kept open;
  - **the bluffs** on the north and east, about 7 straight down to the great river, with the walls
    along their brow, the towpath at their foot, the drains' outfalls in their face and the north
    stair cut down them.
- **Roads:**
  - the lake road up the neck;
  - the south road down the slope to the south bridge;
  - the north road round the west shoulder to the north bridge;
  - the east road from the east gate down the river's valley.

---

## 6. What changes in the data, once agreed

Applied as new strokes plans over today's tiles (they stay the truth), then walked in Play mode.

| Area | Lowered or softened | Kept | Added |
| --- | --- | --- | --- |
| Tolling | the knoll back to a level terrace at 1.5 to 2.5; the river bank and the cutting; the dry rise's 7-tile face to a hill with slopes of 1 in 8 and a tor; the heath ridge's rock face to a broad swell | Wennow, the town's pieces, the roads' lines | the quarry pit |
| Western march | the escarpment to long swells climbing west | the spur, Sere's valley, the roads' lines (regraded) | the gorge with its fall and steps; the moonfall site kept clear |
| Knellmere | p-lakeport's terrace; the isle's height and rock face | the Downs' cliff, the combe, the figure path, the brook's fall | the cliff's ends softened; the shelf by the isle |
| Cruor | the three tiers and inner cliffs, to one level top on a broad hill | the meander, its bridges, the river bluffs | the neck's ramp; the south slope kept open; the towpath |

Buildings, view points and dungeons come at stages 2 and 3 as the briefs' work packages.

## 7. Questions for the owner

1. **Are the activity tables right for each area?** What's missing, or in the wrong place?
2. **The quarry, the gorge, the chalk cliff, Cruor's bluffs and the spur:** are these the right
   places for height?
3. **View points** are a `view` action with a message today. Should they also pull the fog back
   while you stand there, so the views reach further than 85 tiles? That would be new client work.
4. **The later content slots** (a moonfall site, the fishing contest, Cruor's outer district, the
   sealed doors): keep the space for them now?
