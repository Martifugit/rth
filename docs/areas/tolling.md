# Tolling and Wennow: the brief

**Status:** agreed (2026-10-27; the owner left the open calls to the brief, decided at the end).
Wennow reshaped and stage 2's structure blocked out as data (2026-11-02, findings 11-17): the town on
its knoll, the mill, the toll-house, the monument's plot, the waygate, the fishers' landing, the dry
rise's cairn and the edge farm. No people or objects yet; stage 3 puts the town's content back.
Wave 1: the first area taken to stage 2 ([world-areas.md](../world-areas.md)). Template:
[world-example.md](../world-example.md).

## The regions

| Region | Tiles | Now | Role in the area |
| --- | --- | --- | --- |
| `land_0_0` | x 0..95, y 0..95 | taken over (was `tolling`) | the start: the town's square and fields, the river, the spawn (the layout's start place), Wennow's north-west shore |
| `land_1_0` | x 96..191, y 0..95 | taken over (was `palmerway`) | the heath road east along Wennow's north shore to the waygate's place |
| `land_0_m1` | x 0..95, y -96..-1 | taken over | south of the town: the Sere road leaving, meadow and grass rising gently to the first dry ground |
| `land_1_m1` | x 96..191, y -96..-1 | taken over | Wennow's south half, the Wenn leaving east for Knellmere, and the edge of the green: gravel rising to about 4 in the south-east |

**Neighbours, read-only:** the western march (`land_m1_0`, `land_m1_m1`) west; the great bridge
and the dry valley (`land_0_1`, `land_1_1`) north; the lake road (`land_2_0`, `land_2_m1`) east;
Sere's river, the salient (`land_0_m2`, `land_1_m2`) and the Downs (`land_2_m2`) south.

## The brief

**For:** every player's first hour, and the feeling they keep of it: **calm, a little strange,
and open on every side.** Nothing here hurries the player or threatens them; one odd thing waits
down each road, and none of them is explained. The town teaches the game (bank, apothecary, skinning yard,
the first quest, the first rocks and trees, rats and mites), Palmerway is the first walk out and
the first monument, and the south is the first sight of where the green ends. Early only:
nothing here should need more than level 10 to 15 in anything.

**Who:** Tolling's townsfolk (Clerk Ambrose for the church, Corvin Sallow, Edda Marsh, Wenna
Thole), Brother Hollis at the monument, the wardens at the waygate. Stories that pass through:
p-sworn's church (the clerk, the monument, the pledges), the occult houses (Corvin, the house of
Sallow), the Dreaming (Edda, the sleepers' graves), and Sere's trade coming up the Sere road
(Corvin's saltfigs).

**Shape:** a **low green basin round a mere, with the town on a knoll above it.** Tolling's river
comes down from the north-west, past the town and into **Wennow**, the broad mere in the middle of
the basin; the **Wenn** leaves it east for Knellmere. **Tolling stands on a low knoll** on the
river's bank, about 1 to 1.5 above the water meadows (the mere at 0), with the chapel and its bell
tower on the crown: high enough that the tower reads from anywhere in the basin and that the town
looks out over Wennow, low enough to stay a gentle river town (decided 2026-10-27). North and east
of the mere is heath; south of it the land rises very gently (0 to 1.5) through meadow, then
turns to dry gravel in the south-east corner, rising to about 4: the rain shadow beginning.
The one picture to remember: standing on the dry rise south of Wennow, the whole basin below
you, green, with the bell tower on its knoll and the mill on the far side of the water.

**Ground (five, from eleven):** grass and meadow (the basin), heath (north and east of the
mere), gravel (the dry edge), lake water; overlays: the Sere road and Palmerway, Tolling's
streets and fields. The generator's dust, redrock, sand, bracken and worn patches in the two
southern regions go: dust and sand become gravel or the shore, redrock goes (it belongs to the
badlands, not here), bracken becomes heath.

**Landmarks and views:**
- **Wennow** itself is the area's shape: seen from the town's south-east, from Palmerway along
  its north shore, and from the dry rise south.
- **Tolling's bell tower**, over the chapel and the dead-house where every player first wakes: the
  town's mark on the skyline, seen from the dry rise, from Palmerway and across Wennow. It is the
  player's story (`docs/lore.md` VI), so it should read from everywhere in the basin.
- **Widdermill's wheel**, turning against the river: seen from the bridge and from across the mere.
- **Cruor's monument** on Palmerway: seen along the road from the town's east edge.
- **The view south-west to the spine** from the dry rise: the mountains always west.
- Keep clear: the line from the town's south-east shore across Wennow to the dry rise, and the
  road east along the mere.

**Routes:**

| Route | Kind | In this area | Meets |
| --- | --- | --- | --- |
| Palmerway (the lake road) | main | Tolling's east edge along Wennow's north shore to the waygate, leaves at (192, 48) | the lake road (`land_2_0`) |
| The Sere road | main | leaves Tolling south at (27, -1), runs south through `land_0_m1` | the salient (`land_0_m2`), on to p-riverhold |
| The salt road and the west road | road | leave Tolling west at (-1, 51) | the western march (`land_m1_0`) |
| Hollow lane | track | leaves Tolling north at (30, 97) | the dry valley (`land_0_1`), p-hollow |
| Tolling's river | river | from the north at about (36, 96), through the town, into Wennow | `land_0_1` |
| The Wenn | river | out of Wennow at about (146, -12), east | Knellmere, via `land_2_m1`/`land_2_0` |

The Sere road is a road of the empire but not a highway: gravel, faded into the grass, any paving
ending where Tolling ends (finding 4).

**Wide and narrow:** wide almost everywhere, as a start should be. The narrows are the bridge
in the town, the road between the mere and the heath on Palmerway, and the waygate.

**Edges:** the waygate waits for stage 2 (finding 2). Inside the area the only real edge is water (Wennow, the river, the
Wenn); everything else is open to its neighbours.

**Places:**

| Place | Where | Holds |
| --- | --- | --- |
| Tolling (town) | (30, 48), the spawn | a plain square for now; at stage 2 the chapel, bell tower and dead-house (where new players appear); at stage 3 the bank, apothecary, skinning yard and the first quest, The Bell (`docs/quests/the-bell.md`) |
| Widdermill (landmark) | (30, 80) | the mill and its pond, on the river |
| The waygate (landmark) | (180, 48) | the wardens' gate on the road (finding 2) |
| Palmerway's monument | on the road east | Cruor's monument, Brother Hollis (stage 3) |
| **p-edge-farm** (hamlet) | the Sere road, about 60 tiles south of Tolling, where the meadow meets the gravel | the last green farm before Sere: the first beat on the Sere road (world-plan section 5, "the edge of the green") |
| **s-wennow-south** (site) | Wennow's south shore | early: where the fishers pulled the player out of the mere (The Bell's fourth step); something is still there |
| **s-dry-rise** (site) | the gravel rise in `land_1_m1`, about (130, -70) | early, a lookout: the view back over the basin |

p-edge-farm, s-wennow-south and s-dry-rise go into `continent.json` with the area's 1.3 work (the
area has no sites in the layout yet; the nearest are s-heath-ruin north of Palmerway and s-reed-fens
east of it, both in other areas).

**Outwards: how the player is nudged out.** Tolling is a home, not a pen. Every road leaves past
something seen from the bell tower, every townsperson's work points down a different road, and
each direction holds one quiet oddity that isn't explained. Nothing is forced: the pulls are
sightlines, errands and curiosity.

| Way | Seen from the town | The errand that sends you | The oddity | Leads on to |
| --- | --- | --- | --- | --- |
| **East**, Palmerway (the main pull) | the monument on the road, the heath, the waygate's flags | Brother Hollis (Religion, pledges); Wenna's fen rats at the reed fens; the bell-ringer, at The Bell's end: Tolling's bell was cast as a pair, and the other hangs in a belfry that is now in a lake | the old toll-house with no road to toll; the stones on the heath in a style nobody made (s-heath-ruin, seen from the road) | Knellmere, the drowned belfry, p-lakeport, Cruor |
| **West**, the salt road | the spine, always; the observatory on its spur | Corvin wants something from the cave (s-tolling-cave), the first dungeon | the cave itself; cairns on the downs; the observatory's watchers looking at the moon (the first hint of moonfall, which is later content) | the march, the salt pass, Selenfall |
| **South**, the Sere road | the dry rise, the gravel beyond the green | Corvin's saltfigs come up this road; the farm needs a hand | s-wennow-south, where you were found; field walls running out into the gravel | the edge of the green, the salient, Sere |
| **North**, Hollow lane | Widdermill's wheel | the miller, the first and gentlest errand | the dry valley's bridge over nothing (s-dry-bridge, the next area): a river that isn't there any more | the dry valley, the great bridge |

The ring round the town (about 60 tiles) is level 1 to 10 and walked in the first hour; the cave,
the waygate and the dry rise are its edge (5 to 15); beyond them the next areas take over. The
road east is the widest and best kept, so a player with no plan drifts towards Cruor; the other
three are for the curious.

**Counts** (stage 3 is checked against these; skeleton only, the kinds are filled in then):

| | In the old blockout (back at stage 3) | Proposed for the south |
| --- | --- | --- |
| Rocks | copper 5, ironstone 3 | none: the first rocks stay in town and on Palmerway |
| Trees | alder 6, ash 3 (and scenery trees) | a few by the water and the farm; no new kind |
| Fishing | none | one spot on Wennow's south shore, tier 1, when Fishing exists |
| Creatures | fen rats 9, vitriol mites 4 | one early creature of the dry edge, tier 1-2, a handful |
| People | 6 townsfolk, the wardens | the farm's people (p-edge-farm), when it is built |

**History in the ground:** the old toll-house on Palmerway (the road was a toll road before the
waygate); the churchyard under the bell tower, where the drowned from Wennow are buried (the old
plan's sleepers' graves); at the edge of the green, field walls running out into the
gravel where the farms used to reach further (the dry is spreading).

**Something to find:** s-wennow-south (where you were found), and whatever is in Sour Hollow's cask.

**Danger:** none in the town but rats and mites; a little more at the dry edge. Danger reads
before it bites: the dry ground is where it starts.

## Checklist ([world-guide.md](../world-guide.md) section 6)

| | |
| --- | --- |
| Clarity over clutter | The mere is the theme; the south stays sparse (a road, a farm, a rise). |
| Wide and narrow | Wide; narrows at the bridge, the shore road and the waygate. |
| Ground with character | Basin, mere, heath, dry rise: each walks differently. |
| Landmarks shape the skyline | The bell tower on its knoll; Wennow, the mill, the monument; the spine to the west. |
| Edges are scenery | **Fails today**: the waygate can be walked round (finding 2, stage 2). |
| Few ground colours | Five, down from eleven in the south. |
| Paint the RuneScape way | All four regions are painted and taken over. |
| History in the ground | Toll-house, graves, field walls in the gravel. |
| Something to find | s-wennow-south, Sour Hollow. |
| Danger reads before it bites | Rats in town, harder things on the dry edge. |
| Streets and squares | Tolling's square and streets at stage 2. |

## What the work found

1. **Tolling and Palmerway were blockouts of an older style** (flat, hand-placed, not painted), so
   the mere across their southern border was half old water and half painted. Decided
   (2026-10-26): both scrapped; the blockout now generates `land_0_0` and `land_1_0` like the rest
   of the world, and Wennow is one mere. Their buildings, people and objects are in git history
   (`world/regions/tolling.json`, `palmerway.json` and their hand-made files) for stage 3.
2. **The waygate could be walked round:** its palisade was one wall across open land. With
   Palmerway gone the question waits for stage 2 (2.4 Edges): an open gate on the road, a real
   line to water or rough ground, or a gate moved to where the land already narrows.
3. **Too many grounds in the south:** eleven in `land_0_m1`, nine in `land_1_m1` (the standard is
   four or five plus one or two overlays).
4. **The Sere road was cobble** out into the country: now the empire's gravel path, drifting.
5. **A cliff across the south** (fixed 2026-10-26): a straight-edged step two tiles high in
   `land_0_m1` and `land_1_m1`, drawn in red scree. The Downs' white edge (a scarp) went on raising
   the land past the west end of its line until its bounding box cut it off; scarps now die away
   over 40 tiles past their ends (`ReliefTests`).
6. **Red stains on the gravel** (fixed): gravel no longer mixes in 3% red rock.
7. **The knoll** (2026-10-27): a `hill` in the layout, `tolling-knoll` (height 1.2, an ellipse
   24 by 22 tiles centred just west of the town), and the town's platform narrowed to 12 (`"flat"`
   on Tolling in `continent.json`), then `land_0_0` taken over. The town stands at about 2.1, the
   river below it at 0.1 and the water meadows east of it at 0.4 to 0.6; nothing steeper than 0.7
   a tile, so no cliffs, and the roads out wind down off it. The roads were re-routed over it in
   `land_0_0`; on the world map Palmerway's line now bends slightly off the painted road in
   `land_1_0` (taken over earlier), which 2.1 Routes settles.
8. **First ground pass** (2026-10-27, `world/strokes/tolling.json`, applied once; the tiles are the
   truth now): Tolling's river, ruler-straight before, bends gently past the knoll (held off it, so
   its bank never stands as a cliff: nothing steeper than 0.72 a tile) and meanders more freely
   through the meadows to Wennow; Palmerway's bridge is where the new line crosses. Bracken went
   to heath, dust to gravel, fen, reeds and mud away from the water and scraps of field to the
   land round them: each region now has grass and meadow, heath, gravel (south), the lake and the
   river, the roads and paths, and Tolling's fields.
9. **Wennow was the generator's round blob** (1.4): reshaped in finding 11.
10. **The dry rise** reaches about 2.9, not 4; enough for the view. p-edge-farm (38, -50),
    s-wennow-south (108, -38, a new site kind `story`) and s-dry-rise (112, -72) are in
    `continent.json`.
11. **Wennow reshaped** (2026-11-02, `world/strokes/tolling-wennow.json`, applied once): a broad mere
    lying along the valley from the river's mouth (north-west) to where the Wenn leaves (south-east),
    about 2,200 tiles of water (was 2,800); a slim arm reaching north towards Palmerway, so the road
    runs between the mere and the heath; a bay on the south shore with a little sand, where
    s-wennow-south now is (109, -31); reeds at the river's mouth and in the outlet's corner. The old
    water and the generator's sand rim became meadow and grass, easing up from the new shore (no
    cliffs: nothing steeper than the dry rise's 0.8 a tile). Made with a new kind of strokes shape,
    `"water": true` (`GroundStrokes.FillWater`), so any lake can be redrawn the same way.
12. **The town** (2.2, `world/strokes/tolling-town.json` for its ground, the pieces in `land_0_0`).
    The square is the crossroads on the knoll's crown (gravel, about 13 by 9), where Palmerway, the
    Sere road, the west road and Hollow lane meet; the fingerpost, a notice board, two stalls and
    two lamps stand in it. **The church close** is north-west of it on the highest ground: the bell
    tower (the first-pass model, 3 by 3) standing free three tiles off the chapel's west end, the
    chapel (6 by 4, slate roof, lancets, its door facing the lychgate's gap in the south wall), the
    churchyard's dry-stone wall, graves and a ledger stone. **Round the square:** Corvin's apothecary
    (two storeys, jettied) and the church house (stone, the clerk's bank at stage 3) on its south
    side either side of the Sere road, the smithy on the west road. **Houses:** the bell-ringer's by
    the churchyard's west gate, Edda Marsh's by a dew pond west of it (the pond she sleepwalks to),
    one on the west road and two on the Sere road. **The skinning yard** is downstream by the river,
    a shed in a hurdle yard, away from the square. Thirteen buildings in all; the generated field
    strips were cleared from the town and the churchyard, and stay round it as farmland. Hollow
    lane was moved off the crown to run up the churchyard's east side, and the west road comes into
    the square's west side instead of cutting through the churchyard.
13. **Routes** (2.1): Palmerway crosses the river on the first-pass road bridge; the Sere road,
    the west road and Hollow lane leave by the points in the routes table. Lanes go to the skinning
    yard and the mill. Two milestones on Palmerway.
14. **Widdermill** (35, 80): a two-storey stone mill on the river's west bank, the first-pass wheel-
    house and wheel in the river against its east wall, millstones by its door, a lane from Hollow
    lane. The layout's mark moved from (30, 80) to the mill.
15. **Palmerway's places:** the old toll-house (60, 49) with its bar lying in the verge across the
    road; the monument's plot (78, 50) with the first-pass "monument not yet raised" model, where
    Brother Hollis and the founts go at stage 3.
16. **The waygate** (2.4, finding 2 settled): the first-pass gatehouse stands **open** across
    Palmerway at (180, 48) with the wardens' hut beside it: a watch on the road, not a barrier, so
    there is no palisade to walk round and no invisible wall. If the owner wants it barred until a
    quest, the gate needs a line to water or rough ground (the land there is open heath for 40 tiles
    either way), or it moves to a narrower place.
17. **Sites** (2.5): s-wennow-south is the fishers' landing (a hut, a jetty of three spans, a boat
    afloat and one upturned, a mooring post); s-dry-rise has a cairn on its top; p-edge-farm is a
    stone longhouse with its byre (7 by 4) in a walled yard, hay stooks, and two field walls running
    south into the gravel, broken where the dry has taken the fields. s-tolling-cave is the western
    march's.
18. **What can be seen from where.** The game's fog closes at 85 tiles from the camera and regions
    load within 48 of the player, so the brief's long views can't happen as written: the bell tower
    reads from the town's edges, Palmerway's first stretch and the mere's north-west shore, but not
    from the dry rise (about 155 tiles away) or the waygate. Either those views are given up, or
    landmarks get a far view of their own (a silhouette drawn beyond the fog). Waiting on the owner.

## Decided (2026-10-27)

1. **The farm and the dry rise stay.** p-edge-farm is the Sere road's first beat and a calm place to
   lend a hand; s-dry-rise is where the basin is seen whole and the dry beyond it begins. With
   s-wennow-south they give the south its three stops: a farm, where you were found, a view.
2. **The town stands on a low knoll** (1 to 1.5 above the water meadows), the bell tower on its
   crown, so the tower reads across the basin and the town looks out over Wennow. The rest of the
   basin stays low (0 to 1.5) and rises to the south and the dry edge as before.
3. **The basin stays calm:** no aggressive creatures inside it; danger starts at its edges (the
   cave, the dry rise, beyond the waygate).
