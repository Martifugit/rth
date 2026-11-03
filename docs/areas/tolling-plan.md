# Tolling and Wennow: the plan

**Status:** draft (2026-11-03), stage 0 ([plan-template.md](plan-template.md)). The data is
`world/plans/tolling.json`, drawn by `tools/world plan render tolling`. The brief and the record of
the work so far are [tolling.md](tolling.md). Counts and levels are proposals, tuned at stage 3. Ids in
`code` exist in `server/data/content`; anything marked *new* doesn't yet.

Regions: `land_0_0` (the town, the mill, the river), `land_1_0` (Palmerway along Wennow's north
shore to the waygate), `land_0_m1` (the Sere road south, the edge farm), `land_1_m1` (Wennow's south
shore, the landing, the dry rise, the Wenn leaving).

---

## 1. The area in the world

- **Its part in the wider region.** The heartland is the empire's green river country: Tolling,
  Knellmere, Cruor and the land between. Its biome is good for low to mid Woodcutting, Foraging and
  Fishing, and low Mining (`skills.md` 2.2). **Tolling is where every one of the heartland's skills
  is met for the first time.** Each gathering, making and combat loop is here once, at tier 1 to 2,
  in small numbers, close to a bank:
  - Knellmere takes over fishing from tier 2 up;
  - the march takes mining (ironstone, hematite);
  - Cruor takes the shops and the higher monuments.
  - What Tolling must not do is be the best place for anything past level 15.
- **When players come:**
  - **Levels 1 to 15:** everything. The first hour is spent within about 60 tiles of the square.
  - **Mid:** passing through on the way west or south, the bank, Corvin's cellar.
  - **Late:** the respawn after a paid ferry (`death.json` `returnTo` is null, so the spawn), and the
    trophy perch.
- **The one picture:** from Wennow's south shore at the fishers' landing, the mere and, across it,
  the town's roofs with the bell tower standing over them. The landing to the tower is about 85
  tiles, right at the fog's edge, so this is the one long view the area is built around.

## 2. How players find it, and what catches the eye

- **Arrival:** a new character wakes in the dead-house under the bell tower and climbs the stairs
  into the tower's ground floor. The door opens onto the church close. The bell-ringer is at the foot
  of the stairs (The Bell, step 1).
- **The hook:** the bell tower itself. Walking out, the first thing you see is the tower over your
  head and the graves round it: you came up out of the dead-house. Then the square, 15 tiles south,
  with its stalls, the fingerpost and the notice board.
- **Players coming back,** or arriving from elsewhere (rare: Tolling is the start), come by four
  roads:
  - **Palmerway from the east:** you see the bell tower over the bridge from about 60 tiles out,
    past the toll-house.
  - **The Sere road from the south:** the tower over the rooftops from the edge farm.
  - **The west road:** the smithy's smoke first, then the tower.
  - **Hollow lane from the north:** the mill's wheel, then the tower.

## 3. Places

| Place | Where | Levels | Why here | Found by | Hook |
| --- | --- | --- | --- | --- | --- |
| **Tolling** (town) | (30, 48), within [8, 26, 50, 66] | 1-15 | The crossroads of four roads on dry ground above the river and its bridge: a toll town grown round a church. Everything a new player needs, one of each. | They wake here. | The bell tower over the graves; the square's stalls. |
| **The church close** | [19, 53, 34, 64] | 1 | The chapel, the bell tower and the graves together, on the terrace's highest corner. | They wake in it. | The tower. |
| **Widdermill** | (35, 80) | 1-10 | The river runs fastest past the terrace's north end; the town's grain is ground here. | Hollow lane runs to it; the wheel is seen from the north edge of town; the miller's errand. | The wheel turning against the river (widdershins). |
| **The quarry** | [2, 69, 14, 79] | 1-15 | The stone the chapel and the church house are built of came from here; copper and ironstone show in its faces. | A track off the west road past Edda's house; the cart ruts; Mining's first lesson sends you. | A raw grey cut in the heath, a cart and a crane at its lip. |
| **Sour Hollow** | [82, 64, 94, 76] | 8-15 | A damp hollow in the heath where someone dumped a leaking cask. The ground round it is stained, and vitriol mites breed in it. | Seen from Palmerway as a yellow-stained dip 15 tiles north of the road; Corvin's mite tasks; Wenna's "mites for the yard". | Sickly yellow ground and a split cask in the middle of green heath. |
| **The toll-house** | (60, 49) | 1 | Palmerway was a toll road; the bar lies in the verge. | You walk past it on Palmerway. | The toll bar lying broken in the grass, and the board of old tolls. |
| **The monument's plot** | (78, 50) | 1-10 | The church raises its monuments along the empire's roads; this is the first on Palmerway. | On Palmerway, seen from the toll-house; Brother Hollis. | Four founts and a tall stone. |
| **The waygate** | (180, 48) | 5+ | Where Palmerway crests the heath ridge and leaves the start area; the wardens watch the road. | At the end of Palmerway, standing on the skyline from about 60 tiles back. | A gatehouse on the crest with flags; beyond it the land drops away to the lake. |
| **The fishers' landing** | (109, -31) | 1-15 | The one place on Wennow where boats are kept; the fishers who pulled the player out. | The Bell, step 4; the south-shore path from the Sere road. | Boats and a jetty, and the town across the water. |
| **The dry rise** | (123, -49) | 5-15 | The first dry ground south of the mere: where the green ends. | A path from the landing; adders on the slope; seen from the south shore. | A rock tor on top with a cairn, gravel instead of grass. |
| **The edge farm** | (50, -50) | 1-10 | The last green farm on the Sere road, where the meadow meets the gravel. | On the Sere road, 50 tiles past the town. | A longhouse in a walled yard; field walls broken where the gravel took the fields. |

## 4. Buildings

All in `land_0_0` unless noted. The rects are the pieces already blocked out at stage 2.

| Building | Rect | Storeys | Use | Inside | Why |
| --- | --- | --- | --- | --- | --- |
| `chapel` | [27, 54, 33, 58] | 1 | church | `chantry_altar` (pray: devotion); the ledger chest; a trapdoor to the crypt | The church is why the town is here; prayer restores devotion. |
| `bell-tower` | [21, 54, 24, 57] | 3 | landmark | ground floor: stairs down to the dead-house; ladder to the ringing chamber (plane 1); ladder to the bell chamber (plane 2), with a view point | The spawn's way up, and the town's height. |
| `church-house` | [35, 36, 39, 40] | 2 | the bank | `synod_strongbox` at a counter, Clerk Ambrose | The church keeps the strongbox; one bank, on the square. |
| `apothecary` | [24, 36, 29, 40] | 2 | apothecary | Corvin (shop); `apothecary_bench`; a trapdoor behind the counter to his cellar (after Dreamless) | Medicine's first station and Corvin's thread. |
| `smithy` | [17, 43, 21, 47] | 1 | smithy | `furnace`, `forge`; the `tinker` (tools) | Smithing's first station, on the west road towards the quarry. |
| `house-sere-east` | [37, 28, 41, 32] | 1 | the bakehouse | a `range`; the baker (*new*) | Cooking needs a range in town; the baker uses the mill's flour. |
| `bellringer` | [14, 54, 18, 58] | 1 | house | the bell-ringer's home | He lives by the churchyard's west gate. |
| `edda` | [13, 60, 17, 64] | 1 | house | Edda Marsh (Dreamless) | Beside her dew pond, which she sleepwalks to. |
| `house-west-road` | [9, 43, 13, 47] | 1 | house | a townsperson (*new*): the quarry's carter, who sends you to the quarry | Points the way to the quarry. |
| `house-sere-west` | [26, 29, 30, 33] | 1 | house | empty at stage 3, kept for a quest-giver | A house on the main road out. |
| `skinning-shed` | [45, 27, 49, 29] | 1 | skinning yard | `skinning_rack`, `lye_vat`; Wenna Thole (tasks, token shop) | Downstream and away from the square because of the smell; water for the vats. |
| `widdermill` | [33, 78, 37, 82] | 2 | mill | the miller (*new*); the millstones upstairs (plane 1) | The mill's errand; a station for flour when bread exists. |
| `toll-house` | [60, 49, 64, 53] | 1 | empty | the board of old tolls (a readable) | History on the road; kept for an inn (section 13). |
| `fishers-hut` (`land_1_m1`) | [103, -33, 107, -31] | 1 | fishers | two fishers (*new*), nets on the walls | The Bell's fourth step. |
| `wardens-hut` (`land_1_0`) | [184, 41, 188, 45] | 1 | wardens | a warden (*new*) | The watch on the road. |
| `edge-farm` (`land_0_m1`) | [48, -51, 55, -47] | 1 | farm | the farmer (*new*), a byre | The Sere road's first stop. |

## 5. Skills: what content is here

| Skill | What | Count | Where | Levels | Why here | Supports |
| --- | --- | --- | --- | --- | --- | --- |
| Melee | creatures below; no trainer | | | 1-15 | Training is at Cruor's knights' yard. | combat XP from the rats, adders and mites |
| Ranged | none | | | | The surface people's style, met at the observatory in the march. | |
| Occultism | `apothecary_still` in Corvin's cellar | 1 | the cellar | 10+ | Corvin is the last of the house of Sallow; his still is hidden. | Corvin's tasks (`salts_for_corvin`) |
| Defence | from combat | | | 1-15 | | |
| Religion | `monument_fount` | 4 | the monument's plot, [74, 46, 82, 54] | 1-10 | The church's monuments line the empire's roads; this is the first on Palmerway. | Brother Hollis (pledges for scriptures) |
| Religion | `chantry_altar` | 1 | the chapel | all | Devotion is restored at an altar. | |
| Butchering | `skinning_rack`, `lye_vat` | 1 each | the skinning yard | 1-15 | Wildlife and Scalth-touched carcasses, the two families met first. | Wenna's and Corvin's tasks |
| Mining | `copper_seam` | 5 | the quarry, [2, 69, 14, 79] | 1-10 | The chapel's stone quarry; copper shows in its lower face. | the smithy's copper bars |
| Mining | `ironstone_seam` | 3 | the quarry | 10-15 | The same quarry's upper face. The march has more. | the smithy's iron bars |
| Woodcutting | `alder_tree` | 6 | the river's east bank opposite the mill, [44, 76, 52, 86] | 1-10 | Alder grows on riverbanks (skills.md: Venaria's riverbanks). One stand, so tending works. | logs for campfires, charcoal |
| Woodcutting | `ash_tree` | 3 | the west road's hedgerow, [0, 52, 12, 55] | 10-15 | Ash grows in hedgerows. | |
| Fishing | `river_spot` (perch) | 2 | the river above the bridge, [40, 50, 48, 58] | 1-10 | Perch: Venaria's rivers. | the bakehouse's range |
| Fishing | `river_spot` (perch) | 2 | off the fishers' landing, [104, -30, 114, -22] | 1-10 | The landing is where Wennow is fished. | |
| Fishing | `eel_weir` | 1 | the Wenn's outlet, [140, -18, 152, -6] | 10-15 | Eels run where a mere drains into a river; traps. | |
| Fishing | `perch_rise` (trophy) | 1 | off the landing, [108, -26, 118, -18] | 1+ | Trophy angling's first species, close to the start. | the fishing log |
| Foraging | `yarrow_patch` | 6 | the Sere road's verges (3) and Palmerway's (3) | 1-10 | Yarrow grows on verges. | Medicine's first potions at the apothecary |
| Foraging | `rue_patch` | 2 | the edge farm's yard | 10-15 | Kept in a farm garden. | |
| Foraging | `berry_bush` | 2 | the hedges by the west road | 1-5 | Hedgerow food. | |
| Foraging | `reed_bed` | 1 | the river mouth | 1 | Reeds where the river meets the mere. | |
| Dreaming | none yet | | | | The skill arrives with the first way into the Dreaming. Edda's pond is kept for its first orb (section 13). | Dreamless's follow-up |
| Smithing | `furnace`, `forge` | 1 each | the smithy | 1-20 | Copper and iron from the quarry, a five-minute walk. | |
| Artifice | none | | | | The surface people's craft (the observatory). | |
| Tailoring | none | | | | Hides from the rats and adders go to the next town that tans. | |
| Cooking | `range` | 1 | the bakehouse | 1-20 | Perch and rat haunch from here. | |
| Medicine | `apothecary_bench`; the `well` in the square (water for vials) | 1 each | the apothecary; the square | 1-20 | Yarrow from the verges, water from the well, potions at the bench. | Corvin's shop |

**Pace.** 1 to 10 in a skill takes about 18 minutes (skills.md 2.1). Five copper rocks and six
alders hold two or three players at once without a wait. The town expects up to about 10 new players
at a time.

## 6. Creatures and people

| Creatures | Count | Level | Where | Aggressive | Gives | Why here |
| --- | --- | --- | --- | --- | --- | --- |
| `fen_rat` | 9 | 2 | the reed margin at the river's mouth, [60, 2, 82, 18] | no | rat carcass (Butchering: wildlife), rat galls (Dreamless) | Rats in the reeds where the river meets the mere. |
| `vitriol_mite` | 5 | about 10 | Sour Hollow | yes | mite carcass (Butchering: Scalth-touched) | They breed in the vitriol from the cask. |
| adder (*new*) | 6 | about 8 | the dry rise's slopes, [112, -62, 136, -40] | no | adder carcass (hide; a venom gland for Medicine) | Adders bask on dry, sunny gravel. |

**People:**

| Person | Where | Role |
| --- | --- | --- |
| Clerk Ambrose | the church house | bank; The Bell |
| Corvin Sallow | the apothecary | shop, tasks; The Bell, Dreamless |
| Edda Marsh | her house and the dew pond | Dreamless |
| Wenna Thole | the skinning yard | Butchering tasks, token shop |
| Brother Hollis | the monument's plot | pledges for scriptures |
| the bell-ringer (*new*) | the bell tower | The Bell; lets you up the tower |
| the tinker | the smithy | tools |
| the baker (*new*) | the bakehouse | a short errand |
| the miller (*new*) | Widdermill | the first errand |
| the carter (*new*) | the house on the west road | sends you to the quarry |
| two fishers (*new*) | the landing | The Bell step 4 |
| the farmer (*new*) | the edge farm | errands |
| two wardens (*new*) | the waygate | talk about the road east |

## 7. Quests and activities

- **The Bell** ([../quests/the-bell.md](../quests/the-bell.md)): the dead-house (step 1), the
  church house's strongbox (2), the apothecary (3), the landing (4: rats on the way), Edda's pond
  (5), and the chapel and its crypt (6). It needs the dead-house, the crypt's trapdoor and the
  landing's fishers.
- **Dreamless** (`quests.json`): Edda by the pond; three rat galls from the river mouth's rats; a
  Sere saltfig (Corvin's stock); Corvin's still.
- **Errands:**
  - the miller: the wheel's sluice jammed, clear it;
  - the carter: take a cart of stone to the smithy, which teaches Mining;
  - the farmer: carry a sack, mend a wall.
  - These are short, one-step jobs. Each sends a new player to a different edge of the ring.
- **Tasks:** Wenna (rats, mites) and Corvin (mite glands, choleric salts, thinning the mites).
- **Content that supports nothing yet:**
  - the trophy perch (the fishing log is its own activity);
  - the eel weir (a gentler fishing method).

## 8. Below ground and indoors

| Place | Entrance | Size | Levels | Floor plan | Why |
| --- | --- | --- | --- | --- | --- |
| **The dead-house** | `stairs_down` in the bell tower, (22, 55) | 10 x 8 | new | one vaulted room: the bier where new characters appear, two more biers (empty), a shelf of winding sheets; the stairs up | Where the player starts. |
| **The church crypt** | `trapdoor_down` in the chapel, (30, 56), opened in The Bell | 16 x 12 | 1-15 | stairs into a passage; the ossuary (niches, a readable naming the drowned buried here); the ledger chest (The Bell step 6); a blocked arch at the far end (section 13) | The Bell's last step. |
| **Corvin's cellar** | `trapdoor_down` behind the apothecary's counter, (26, 38), after Dreamless | 10 x 10 | 10+ | one room: the `apothecary_still`, shelves of jars (readables), a locked chest (later) | Occultism's first station; Corvin's tasks. |
| **The bell tower's floors** | ladders inside the tower | 3 x 3 each | all | the ringing chamber (plane 1): the ropes; the bell chamber (plane 2): the bell, the view point | The town's height is the tower. |

## 9. Heights

| Height | Job | Rise | Section | Routes | Sees | Why |
| --- | --- | --- | --- | --- | --- | --- |
| **The town's terrace** | water | about 2 | level ground 1.5 to 2.5 over the river (0.1), varying by about a tile across the town; a grassy bank of about 1.5 down to the river on the east over 4 to 6 tiles; long gentle slopes elsewhere | Palmerway off the bridge; the Sere road, the west road and Hollow lane level out of it | | The town sits dry above the river. No cliff, no knoll. |
| **The quarry** | nodes | -2.5 | a pit about 12 by 10 cut into the heath; faces of bare rock about 2.5 high on the north and west holding the rocks; a cart ramp in on the south | the quarry track from the west road | | Mining's rocks need a face to stand in. |
| **Sour Hollow** | destination | -1.5 | a bowl about 12 across, gentle sides, stained ground at the bottom round the cask | walked into from any side; a trodden path from Palmerway | | The mites' place reads as a place: a dip you look down into. |
| **The dry rise** | view | +6 | gravel slopes of about 1 in 8 from Wennow's south shore; a tor of rock about 2 high on top with the cairn; the far side falls slowly south into the dry | the path up from the landing; open slopes on every side | Wennow's south half, the landing, the Wenn's outlet, the edge farm | The first climb, and a view of the mere. |
| **The heath ridge** | threshold | +4.5 | a broad swell between Wennow's east shore and Knellmere's fens; slopes of about 1 in 10, 40 to 50 tiles each side | Palmerway in a straight climb to the waygate on the crest | the lake opening ahead (the lake road's area) | Leaving the start area is a crest you see from far off. |
| **The Wenn's glen** | water | -3 | the Wenn leaving Wennow through the ridge's south end; steep banks about 3 high for about 20 tiles | a ford (a Fishing shortcut, level 10) | | The river needs a way through the ridge. |

## 10. Routes

| Route | Kind | Points | Why |
| --- | --- | --- | --- |
| Palmerway | main | the square, over the bridge (46, 46), east along Wennow's north shore past the toll-house and the monument, over the ridge at the waygate, out at (192, 48) | The road to Cruor; the widest and best kept, so new players drift east. |
| The Sere road | main | from the square south (30, 30), out of `land_0_0` at (28, -1), past the edge farm, on south | Sere's trade comes up it. |
| The west road | road | from the square west past the smithy, out at (0, 51) | To the march and the salt road. |
| Hollow lane | track | from the square north past the churchyard's east side to the mill, out at (31, 96) | To the dry valley. |
| The quarry track | track | off the west road at (12, 49) north past Edda's house to the quarry | The carts' way. |
| The south-shore path | path | off the Sere road at (34, -30) east along Wennow's south shore to the landing | The fishers' way to town. |
| The dry rise path | path | from the landing up to the cairn | The first climb. |
| Tolling's river | river | from the north (36, 95), past the mill and the town, into Wennow at about (70, 5) | |
| The Wenn | river | out of Wennow at (146, -12), east through the glen | |

**Walking times** (tiles, then seconds walking; half that running):

| From the square to | Tiles | Walking |
| --- | --- | --- |
| the bank, the apothecary, the smithy | 5 to 15 | under 10 s |
| the mill | 32 | 19 s |
| the quarry | 35 | 21 s |
| the monument | 48 | 29 s |
| Sour Hollow | 63 | 38 s |
| the edge farm | 100 | 60 s |
| the landing | 110 | 66 s |
| the dry rise | 135 | 81 s |
| the waygate | 150 | 90 s |

## 11. Neighbours and the wider region

| Neighbour | What crosses |
| --- | --- |
| **The western march** (west) | The west road and the salt road. Corvin's errand to the cave, the first dungeon (levels 5 to 15, a step up from the town's 1 to 10). The march takes over Mining with more ironstone and the first hematite. The spine seen west from the quarry and the west road. |
| **Knellmere and the lake road** (east) | Palmerway over the waygate's crest. Wenna's fen rat errand to the reed fens. The Bell's ending points at the drowned belfry. Fishing moves there from tier 2 (eels, pike, trophy angling). Levels 5 to 20 on the far side. |
| **The great bridge and the dry valley** (north) | Hollow lane to the dry valley and its bridge over a dry riverbed. Tolling's river comes from the north. |
| **Sere's river: the salient and the sluice** (south) | The Sere road to p-riverhold. Corvin's saltfigs come up it. The dry rise is the first of the dry ground the salient is made of. |

**The wider region.** Tolling holds tier 1 to 2 of every heartland skill and the first of each
station. It must not hold anything better: higher Woodcutting is round Cruor and Eller, fishing is
Knellmere's, mining is the march's.

## 12. Walkthroughs

- **A new player (about 60 to 90 minutes):**
  1. Wakes in the dead-house; the bell-ringer sends them to the clerk (2 min).
  2. Bank: the first withdrawal is their own belongings. Corvin: the first potions (5 min).
  3. The carter sends them to the quarry: copper to about level 8, smelted at the smithy, a first
     tool (20 min).
  4. Rats at the river mouth on the way to the landing; the fishers; perch to about level 8;
     cooked at the bakehouse (25 min).
  5. Edda at the pond; The Bell ends at the chapel and the crypt (10 min).
  6. The bell tower's view: the monument on Palmerway, the mill, the quarry. They head east to
     Brother Hollis, then on to the waygate, or stay for Dreamless.
- **Levels 8 to 15 (about an hour):** Dreamless; Wenna's and Corvin's tasks (rats, then mites at
  Sour Hollow); ash and ironstone; the eel weir; adders on the dry rise and its view. Then out: the
  cave in the march (Corvin), or the lake (Wenna's fen rats, The Bell's belfry).
- **Mid players:** through on the west road or the Sere road; the bank; Corvin's cellar; Wenna's
  tasks when they come back with carcasses.
- **Late players:** they arrive at the spawn after a paid ferry. The trophy perch is the only thing
  for them.

## 13. Kept for later

| What | Where | For | Needs | Why here |
| --- | --- | --- | --- | --- |
| The toll-house | [58, 47, 68, 56] | Tolling's inn: a room to meet, a range, a notice board of player-made notes | nothing new (a building and a range) | On the main road, by the bridge; the old toll-house is the obvious inn. |
| The edge farm's yard | [44, -62, 58, -56] | four farming plots | Farming (not in v1) | A farm already, in its walled yard. |
| Edda's pond | [5, 60, 13, 66] | the first Dreaming orb | the Dreaming | Edda sleepwalks to it; Dreamless leads there. |
| The crypt's blocked arch | inside the crypt | an older vault for a later quest | nothing new | Below the town, already entered in The Bell. |

## 14. Assets

From the village kit (first pass, `docs/areas/tolling-assets.md`):
- buildings, fences, lamps, stalls, graves;
- the bell tower, the mill and its wheel, the waygate;
- the jetty, the boats, the cairn.

New:
- the quarry: its faces, a cart, a hand crane;
- Sour Hollow's split cask and stained ground;
- the bakehouse's oven, and the toll board;
- the dead-house, the crypt and the cellar interiors (a small cellar and crypt kit, reused
  everywhere);
- the adder; the miller, baker, carter, fishers, farmer and wardens (people).

## 15. Not decided

1. **The waygate:** open, as built, or barred until a quest? Barred would need a line to water or
   rough ground.
2. **Long views:** should a view point pull the fog back (the bell tower, the dry rise)? The
   landing-to-tower view is at the fog's edge.
3. **The adder:** the dry rise's creature (name, drops).
4. **Copper in the quarry or the river bank:** copper "tends to be" in river cliffs (skills.md 2.2).
   The quarry keeps Mining in one place; a river bank by the mill would fit copper better.
5. **The bakehouse:** is a range in a baker's house right, or should Cooking wait for the inn?
