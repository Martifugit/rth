# Tolling and Wennow: the plan

**Status:** draft 2 (2026-11-03), stage 0 ([plan-template.md](plan-template.md)), for the owner's
review. The data is `world/plans/tolling.json`, drawn by `tools/world plan render tolling 8`. The
brief and the record of the work so far are [tolling.md](tolling.md).

Everything here is a proposal, with its reasons and the assumptions under it. Counts and levels are
first guesses, tuned at stage 3. Ids in `code` exist in `server/data/content`; anything marked *new*
doesn't. The plan decides only this area. Where it touches a neighbour, it says what crosses and
leaves the rest as a question for that area's plan.

Regions:
- `land_0_0`: the town, the mill, the river;
- `land_1_0`: Palmerway along Wennow's north shore to the waygate;
- `land_0_m1`: the Sere road south, the edge farm;
- `land_1_m1`: Wennow's south shore, the landing, the dry rise, the Wenn leaving.

---

## 1. What the area is for

### Why players come

**Early.**
- Players start here. Tolling introduces **a few** of the game's basics through its people:
  - talking and the bank (the clerk);
  - potions (Corvin);
  - fighting and butchering (Wenna and the rats);
  - fishing and cooking (the fishers);
  - the church's monuments (Brother Hollis).
- It also introduces **the first mysteries**:
  - the bell that rang for a stranger (The Bell);
  - Edda's sleepwalking (Dreamless);
  - Corvin's curiosity;
  - the ledger that had the player down as dead.
- The town isn't where those skills are trained up. Each person teaches one thing and points
  somewhere: the landing, the river mouth, the monument on the road east. **Once players understand
  a little of how things work and where they are, the quests, the roads out and what they can see
  from the town should make them want to leave and explore.**

**Mid.** Players come back for:
- **Tolling's people, who turn up in longer quest lines:**
  - Corvin: the house of Sallow and the occult;
  - Edda: the Dreaming;
  - the bell-ringer and the clerk: why the bell rang, and the ledger;
  - Wenna: tasks for the carcasses players bring back from further out.
- **Not a door to the realm of the dead** (decided). Why the player woke in the dead-house can be
  answered in a later quest (the owner's idea: the ferryman found it funny to have them wake there,
  because it made them come back to him with interesting stories). The dead-house needs nothing more
  for that.
- **Resources that fit the area at mid levels:**
  - hematite in the old church quarry (tier 3, skills.md: "old church quarries");
  - bog myrtle on Wennow's shores (tier 4; skills.md names Wennow's shores as one of its homes);
  - pike in Wennow (tier 3, "Venaria's lakes");
  - an old elm on the road (tier 3, "old fields and roadsides").
  - None of these are taught in town. They are found by going out into the area.

**Late.** The wider area matters more than the town:
- **The area's achievement set** (section 12a): its higher tiers bring late players back across the
  whole area, to Wennow, the quarry, Sour Hollow and the dry rise.
- **Wennow's trophy fishing:**
  - Perch are tier 1, but a record perch needs top tackle and skill (`docs/fishing.md`).
  - A **revised** fish from Wennow, a rare variant for the collector, is worth the trip for any
    angler.
  - Wennow is where the player was pulled from the water, which gives its fish a reason to be
    strange.

### What makes players leave, and which way

| Way | What pulls them | Seen from the town |
| --- | --- | --- |
| **East**, Palmerway | Brother Hollis's monument (Religion); Wenna's errand to the reed fens beyond the waygate; The Bell's last line about the bell's twin in a drowned belfry | the monument's stone on the road, from the bridge (about 45 tiles) |
| **South**, the Sere road and the south shore | The Bell's fourth step at the fishers' landing (where you were found); the dry rise's tor | the dry rise is beyond the fog from town; the landing is seen across the water from the river mouth |
| **North-east**, the heath | Sour Hollow: stained yellow ground seen from Palmerway; Corvin's mite tasks | from Palmerway, about 15 tiles off the road |
| **North**, Hollow lane | the miller; the mill wheel | the wheel, from the church close (about 25 tiles) |
| **West**, the west road | the old quarry off it; the road on towards the march | the quarry's crane over the heath (about 20 tiles) |

**Long views** (decided): the fog stays at 85 tiles and view points don't pull it back. Landmarks come
into view as players explore out of Tolling, and draw them on from there.

### The one picture

From the fishers' landing on Wennow's south shore: the mere, and across it the town's roofs with the
bell tower standing over them. That's about 85 tiles, at the fog's edge, so it needs checking in the
Editor.

## 2. How players find it, and what catches the eye

- **Every new player** wakes in the dead-house under the bell tower and climbs the stairs into the
  tower's ground floor. The bell-ringer is at the foot of the stairs (The Bell, step 1). Outside are
  the graves of the church close and, 15 tiles south, the square.
- **The hook** is the place they woke: a bier in a vault, the tower overhead, graves round the door.
  The first thing the game shows them is that they were dead.
- **The square** holds what points outward:
  - **the fingerpost**, naming the four roads and where they go;
  - **the notice board**, with work wanted round the area;
  - **the well.**
  - There are no stations in the square.

## 3. Places

Each with its purpose, why it is here, what the player does, and what it leads to.

**Tolling** (the town, about (30, 48))
- **For:** early (the start, the people who introduce things); mid (the same people's quest lines);
  the bank.
- **Why here:** four roads cross on dry ground above a river and its bridge. A toll was taken on the
  road, and a church was built at the crossing. That's what the town is: a church, a toll-keeper's
  road, the trades that serve travellers, and a few homes.
- **What it holds:** the church close; the square; Corvin's apothecary; the church house (the bank);
  the skinning yard downstream; homes. **No forge** (decided).
- **Leads to:** every way out (section 1).

**The church close** ([19, 53, 34, 64])
- **For:** early (the dead-house, The Bell's last step); mid (the crypt, the way to the dead).
- **Why here:** the oldest ground in town, on the terrace's highest corner, where the chapel and the
  bell tower stand together. The graves hold Wennow's drowned, which ties the church to the mere.

**The fishers' landing** ((109, -31), across the mere)
- **For:** early (The Bell step 4; fishing and cooking taught by the fishers); late (trophy and
  revised fish from Wennow).
- **Why here:** the south shore has the one sheltered bay on Wennow (finding 11), so the boats are
  kept there. The fishers pulled the player out near it.
- **What you do:** talk to the fishers, fish perch off the jetty, cook at their fire.
- **Leads to:** the dry rise, up the path behind the hut; the south shore's bog myrtle.

**Sour Hollow** ([82, 64, 94, 76], in the heath north of Palmerway)
- **For:** early to mid (Corvin's and Wenna's mite tasks; vitriol mites, the first creatures that
  attack on sight).
- **Why here:** a damp hollow off the road, where a leaking cask was dumped out of sight of the
  town. The vitriol stains the ground and the mites breed in it.
- **What you do:** fight mites, collect carcasses.
- **Hook:** yellow ground in green heath, seen from the road.

**The old quarry** ([2, 69, 14, 79], off the west road)
- **For:** mid (hematite, Mining tier 3).
- **Why here:** the chapel and the church house are stone, and the stone came from here. It's an old
  church quarry, now mostly worked out, and skills.md puts hematite in old church quarries. Its
  rust-red streaks are where miners say "the rock bleeds".
- **Why not taught:** it's a mid-level spot in a start area, found by walking out the west road and
  seeing the crane.

**Widdermill** ((35, 80))
- **For:** early (the miller's errand).
- **Why here:** the river runs fastest past the terrace's north end. The wheel turns against it.
- **What it holds:** the miller; the alders along its bank.

**The monument's plot** ((78, 50), on Palmerway)
- **For:** early to mid (Religion: the founts, Brother Hollis's pledges).
- **Why here:** the church raises its monuments along the empire's roads, east of town on the main
  road. Putting it on the way east is what draws players along Palmerway.

**The toll-house and the old elm** ((60, 49))
- **For:** mid (the elm, Woodcutting tier 3: a big shared tree many chop at once).
- **Why here:** an old roadside elm by the toll-house where the bar stood, which skills.md says is
  where elms grow. The toll-house is empty, its bar lying in the verge.

**The waygate** ((180, 48), on the heath ridge's crest)
- **For:** the edge of the start area.
- **Why here:** Palmerway crests the ridge here, and the wardens watch the road. The gate is the
  threshold, seen on the skyline as you walk east.

**The dry rise** ((123, -49))
- **For:** early to mid (adders, *new*, proposed; the cairn's view of the mere).
- **Why here:** the first dry ground south of Wennow, gravel where the grass ends. Adders bask on dry,
  sunny gravel.

**The edge farm** ((50, -50), on the Sere road)
- **For:** early (the farmer's errand); the Sere road's first stop.
- **Why here:** the last green farm before the gravel. Its field walls run out into the dry, broken
  where the fields were given up.

## 4. Buildings

Only what the town's purpose needs (section 3). The rects are the stage 2 blockout's.

| Building | Use | Inside | Why |
| --- | --- | --- | --- |
| `chapel` | church | `chantry_altar` (pray), the ledger chest, a trapdoor to the crypt | the church the town grew round; The Bell ends here |
| `bell-tower` | landmark | stairs down to the dead-house; ladders up to the ringing chamber and the bell chamber | the spawn's way up |
| `church-house` | the bank | `synod_strongbox`, Clerk Ambrose | the church keeps the town's money and its ledger |
| `apothecary` | Corvin's shop | Corvin; `apothecary_bench`; a trapdoor to his cellar (later) | Corvin introduces potions; his cellar is his secret |
| `skinning-shed` | the skinning yard | `skinning_rack`, `lye_vat`, Wenna Thole | Butchering is Tolling's own trade: a road town on the edge of rough country takes in carcasses; downstream for the smell |
| `smithy` (the blockout's name) | unassigned | | Tolling has no forge (decided); the building stays, kept for stage 3's quests, and gets its name then |
| `bellringer`, `edda` | homes | the bell-ringer; Edda Marsh | both in The Bell and Dreamless |
| `house-west-road`, `house-sere-west`, `house-sere-east` | homes | unassigned | kept for people stage 3's quests need; no use invented now |
| `widdermill` | mill | the miller (*new*); the millstones upstairs | the miller's errand |
| `toll-house` | empty | the board of old tolls (a readable) | history on the road; kept for an inn (section 13) |
| `fishers-hut` | the fishers | two fishers (*new*); a fire outside | The Bell step 4; fishing and cooking |
| `wardens-hut` | the wardens | a warden (*new*) | the watch on the road |
| `edge-farm` | farm | the farmer (*new*) | the farmer's errand |

## 5. Skills: what content is here

| Skill | Here? | What, where, levels | Why |
| --- | --- | --- | --- |
| Melee, Defence | yes | rats (river mouth), mites (Sour Hollow), adders (the dry rise) | the fights Wenna's and Corvin's tasks need |
| Ranged | no | | no one here teaches it, and nothing in the land calls for it |
| Occultism | later | the still in Corvin's cellar (mid) | Corvin's secret; his quest line |
| Religion | yes | the chapel's altar; 4 `monument_fount` on Palmerway (levels 1 to 10) | the church is why the town exists; the monument draws players east |
| Butchering | yes | `skinning_rack`, `lye_vat` at the skinning yard; rats, mites, adders | the town's own trade |
| Mining | yes, mid | 4 `hematite_seam` in the old quarry (levels 20 to 30) | an old church quarry (skills.md 2.2) |
| Woodcutting | yes | 6 `alder_tree` on the mill's bank (1 to 10); the old elm by the toll-house (*new*, 20 to 30) | alders grow on riverbanks, elms by old roads |
| Fishing | yes, early to late | perch off the landing (`river_spot`, 1 to 10); an `eel_weir` where the Wenn leaves (10 to 20); pike in Wennow (*new* spot, 20 to 30); `perch_rise` and `eel_rise` trophy spots (any level, records late) | taught by the fishers; Wennow holds fish at every level, and its records and revised fish draw late players |
| Foraging | yes | yarrow on the verges (1 to 10); bog myrtle on Wennow's south shore (*new* patch, 30 to 40) | yarrow for Corvin's potions; bog myrtle's home includes Wennow's shores (skills.md) |
| Dreaming | later | Edda's pond kept for an orb (section 13) | Edda's thread |
| Smithing | no (decided) | | Tolling has no forge. Smithing may be introduced by a quest that sends the player to a smith in another town: a question for that town's plan |
| Artifice, Tailoring | no | | nothing here makes or needs them |
| Cooking | yes | the fishers' fire at the landing | the fishers cook their catch; cooking is taught where the fish are, not in town |
| Medicine | yes | `apothecary_bench` (Corvin), the `well` in the square (water for vials) | Corvin introduces it |

**Spread:**
- Early content is at the landing, the river mouth and the monument, each 45 to 110 tiles from the
  square, so learning it means going out.
- Mid content is at the quarry, the elm, Sour Hollow, the eel weir, the pike and the bog myrtle.
- Late content is Wennow's records and revised fish, and the achievement set.

## 6. Creatures and people

| Creatures | Count | Level | Where | Aggressive | Why here |
| --- | --- | --- | --- | --- | --- |
| `fen_rat` | about 9 | 2 | the reeds at the river's mouth | no | rats in the reeds; Wenna's first task and Dreamless's galls |
| `vitriol_mite` | about 5 | about 10 | Sour Hollow | yes | they breed in the vitriol; the first creatures that attack, kept away from the town and the roads |
| adder (*new*) | about 6 | about 8 | the dry rise | no | dry, sunny gravel; hide and a venom gland (Butchering, Medicine) |

**People** (each with what they introduce or lead to):
- **The bell-ringer:** The Bell.
- **Clerk Ambrose:** the bank, the ledger.
- **Corvin Sallow:** potions, then his tasks, then his cellar.
- **Edda Marsh:** Dreamless, then the Dreaming.
- **Wenna Thole:** Butchering tasks.
- **Brother Hollis:** Religion.
- **The fishers:** The Bell step 4; fishing and cooking.
- **The miller, the farmer:** errands.
- **The wardens:** the road east.

## 7. Quests and activities

- **The Bell:** the dead-house, the bank, the apothecary, the landing, Edda's pond, and the chapel
  and its crypt.
- **Dreamless:** Edda, the river mouth's rats (galls), Corvin's still.
- **Mid quest lines (proposed, to be written):**
  - Corvin's: the cellar and the house of Sallow;
  - Edda's: the Dreaming;
  - the bell's: why it rang, the ledger, and in time why the player woke in the dead-house.
- **Errands:** the miller (the sluice), the farmer (a wall).
- **Tasks:** Wenna (rats, mites, adders), Corvin (mites).

## 8. Below ground and indoors

| Place | Entrance | Size | For | Plan | Why |
| --- | --- | --- | --- | --- | --- |
| The dead-house | stairs in the bell tower | about 10 x 8 | every new player; the bell's quest line later | a vaulted room with three biers (the player's, two empty); a shelf of winding sheets; the stairs up | where the player wakes |
| The church crypt | a trapdoor in the chapel, opened in The Bell | about 16 x 12 | early (The Bell step 6); later quests | stairs into a passage; the ossuary (the drowned buried here, named); the ledger chest; a blocked arch at the far end, kept for later | The Bell's end |
| Corvin's cellar | a trapdoor behind his counter, after Dreamless | about 10 x 10 | mid | the still, shelves of jars, a locked chest | his secret, and his quest line's start |
| The bell tower | ladders inside | 3 x 3 a floor | everyone | the ringing chamber (plane 1); the bell chamber (plane 2), looking over the town, the mill, the quarry, the monument and Sour Hollow (within the fog) | the town's height is the tower, not the ground |

## 9. Heights

| Height | Job | What it is | Routes | Why |
| --- | --- | --- | --- | --- |
| The town's terrace | water | level ground about 1.5 to 2.5 over the river, varying by about a tile across the town; a grassy bank down to the river on the east | every road out | the town sits dry above the river; no knoll, no cliff |
| The old quarry | nodes | a pit about 12 by 10, 2.5 deep, rock faces on its north and west holding the hematite; a cart ramp on the south | a track off the west road | the rocks need a face; the crane at its lip is seen from the road |
| Sour Hollow | destination | a bowl about 12 across with gentle sides, the cask at the bottom | from any side; a trodden path from Palmerway | a place you look down into |
| The dry rise | view | gravel slopes of about 1 in 8 to about 6, a rock tor on top with a cairn | a path from the landing; open slopes | a first climb with a view back over the mere and the landing |
| The heath ridge | threshold | a broad swell, crest about 4.5, slopes of about 1 in 10 | Palmerway to the waygate on the crest | leaving the start area is a crest seen from far off |
| The Wenn's glen | water | the Wenn through the ridge's south end, banks about 3 | the river; a ford (Fishing 10, a shortcut) | the river needs a way through |

## 10. Routes

Palmerway (east), the Sere road (south), the west road, Hollow lane (north), as they are now. New:
- **the quarry track** off the west road;
- **the south-shore path** from the Sere road to the landing;
- **the dry rise path.**

**Walking times from the square** (seconds at walking pace):

| To | Tiles | Walking |
| --- | --- | --- |
| the bank, the apothecary | about 10 | 6 s |
| the mill | 32 | 19 s |
| the quarry | 35 | 21 s |
| the monument | 48 | 29 s |
| Sour Hollow | 63 | 38 s |
| the river mouth | about 60 | 36 s |
| the edge farm | 100 | 60 s |
| the landing | 110 | 66 s |
| the dry rise | 135 | 81 s |
| the waygate | 150 | 90 s |

## 11. Neighbours and the wider region

| Neighbour | What crosses from this side | Questions for its plan |
| --- | --- | --- |
| The western march | the west road and the salt road; the spine seen west | Corvin's errand to the cave (s-tolling-cave) is assumed in the brief: does the march's plan keep it? |
| Knellmere and the lake road | Palmerway over the waygate's crest; Wenna's errand to the reed fens; The Bell's last line about the drowned belfry | does the lake road's plan take up the belfry's twin bell? |
| The great bridge and the dry valley | Hollow lane north; Tolling's river comes from the north | what does the dry valley offer someone coming up Hollow lane? |
| Sere's river: the salient and the sluice | the Sere road south; Corvin's saltfigs come up it | |

**The wider region:**
- Tolling is the heartland's start.
- Its resources are the ones its land gives at every level: hematite, bog myrtle, Wennow's fish, an
  old elm.
- It doesn't try to hold one of everything.

## 12. Walkthroughs

No times: how long each part takes is for play to show.
- **A new player:**
  1. Wakes in the dead-house and is sent by the bell-ringer to the clerk, then to Corvin.
  2. The Bell sends them across the river to the river mouth's rats (Wenna's first task on the way)
     and round the mere to the landing, where the fishers teach them to fish and cook.
  3. From the landing they see the dry rise and walk up it; or they go back round to Edda and the
     chapel to finish The Bell.
  4. The fingerpost, the notice board, the monument on the road east and the bell's twin send them
     out.
- **Early to mid:**
  - Dreamless.
  - Corvin's and Wenna's tasks take them to Sour Hollow and the dry rise.
  - Then they leave, east to the lake (the main road, the monument on the way), west, or south.
- **Mid, coming back:** Corvin's, Edda's and the bell's quest lines; the quarry's hematite; the
  elm; pike and bog myrtle; Wenna for carcasses from further out.
- **Late:** the achievement set's higher tiers across the area; Wennow's records and revised fish.

## 12a. Achievements

The Tolling and Wennow set (proposed; kinds that exist in achievements.json):

| Tier | Asks for |
| --- | --- |
| Easy | finish The Bell; climb the bell tower; catch a perch at the landing; butcher a rat for Wenna; empower the monument once |
| Medium | finish Dreamless; clear Sour Hollow's mites; set an eel trap at the Wenn; mine hematite in the old quarry; reach the dry rise's cairn |
| Hard | catch a pike in Wennow; pick bog myrtle on the south shore; fell the old elm with others; Corvin's quest line |
| Elite | Edda's quest line; the bell's quest line |
| Master | a record perch from Wennow; a revised fish from Wennow for the collector |

## 13. Kept for later

| What | Where | For | Why here |
| --- | --- | --- | --- |
| The toll-house | [58, 47, 68, 56] | an inn, where players meet | on the main road by the bridge |
| The edge farm's yard | [44, -62, 58, -56] | farming plots, if Farming is ever added | already a farm |
| Edda's pond | [5, 60, 13, 66] | the first Dreaming orb | Edda's thread leads there |
| The crypt's blocked arch | inside the crypt | an older vault | below the town, already visited in The Bell |

## 14. Assets

From the village kit (first pass, [tolling-assets.md](tolling-assets.md)): buildings, the bell tower,
the mill, the waygate, the jetty and boats, graves, the cairn.

New:
- the quarry's faces and its crane;
- Sour Hollow's cask;
- the old elm;
- the dead-house, crypt and cellar interiors (a small crypt and cellar kit);
- the adder;
- the new people.

## 15. Decided and not decided

**Decided (the owner, 2026-11-03):**
- **Pace** varies from skill to skill (skills.md 2.1): neither too fast nor mind-numbingly slow; each
  new tier should feel like an accomplishment, with time to explore it before outlevelling it.
  Something exciting in each level bracket; the end game denser, since most time is spent there.
- **The dead-house** is not a door to the realm of the dead.
- **Tolling has no forge.**
- **Late resources:** Wennow's records and revised fish are enough for now, to be rebalanced later.
- **View points don't pull the fog back:** landmarks come into view as players explore.

**Not decided:**
1. **The waygate: why is it there?** Whether it is open or barred follows from that. Options:
   - **A watch and toll for the road's traffic,** open to walkers. The toll moved from the old
     toll-house when the gate was built on the ridge, which is why the toll-house stands empty. The
     wardens stop carts, talk to travellers, and are where a player first hears about the road east.
     Nothing is barred, so it doesn't hold players back.
   - **A cordon** between the empire's frontier (Tolling, near Sere and the spine) and its
     heartland: the wardens watch who comes in from the west. It would only bar anyone if a quest
     needed it.
   - **Nothing:** no content uses the wardens, so the gate goes. The ridge's crest alone is the
     threshold, with something small on it (a waystone).
   - Barring it would keep new players in the start area, which works against making them want to
     leave. So if it stays, it's proposed open.
2. **The adder** (a small venomous snake of dry heaths, the European adder) as the dry rise's creature.
