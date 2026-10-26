# Tolling and Wennow: the brief

**Status:** draft (2026-10-26), waiting on the owner. Package 1.1 is ticked in `world/areas.json`
once the questions at the end are answered and the brief agreed. **This pass is ground only**: no
assets, buildings, people or objects; stage 3 puts the town's content back. Wave 1: the first area taken to
stage 2 ([world-areas.md](../world-areas.md)). Template: [world-example.md](../world-example.md).

## The regions

| Region | Tiles | Now | Role in the area |
| --- | --- | --- | --- |
| `land_0_0` | x 0..95, y 0..95 | generated (was `tolling`) | the start: the town's square and fields, the river, the spawn (the layout's start place), Wennow's north-west shore |
| `land_1_0` | x 96..191, y 0..95 | generated (was `palmerway`) | the heath road east along Wennow's north shore to the waygate's place |
| `land_0_m1` | x 0..95, y -96..-1 | generated | south of the town: the Sere road leaving, meadow and grass rising gently to the first dry ground |
| `land_1_m1` | x 96..191, y -96..-1 | generated | Wennow's south half, the Wenn leaving east for Knellmere, and the edge of the green: gravel rising to about 4 in the south-east |

**Neighbours, read-only:** the western march (`land_m1_0`, `land_m1_m1`) west; the great bridge
and the dry valley (`land_0_1`, `land_1_1`) north; the lake road (`land_2_0`, `land_2_m1`) east;
Sere's river, the salient (`land_0_m2`, `land_1_m2`) and the Downs (`land_2_m2`) south.

## The brief

**For:** every player's first hour. The town teaches the game (bank, apothecary, skinning yard,
the first quest, the first rocks and trees, rats and mites), Palmerway is the first walk out and
the first monument, and the south is the first sight of where the green ends. Early only:
nothing here should need more than level 10 to 15 in anything.

**Who:** Tolling's townsfolk (Clerk Ambrose for the church, Corvin Sallow, Edda Marsh, Wenna
Thole), Brother Hollis at the monument, the wardens at the waygate. Stories that pass through:
p-sworn's church (the clerk, the monument, the pledges), the occult houses (Corvin, the house of
Sallow), the Dreaming (Edda, the sleepers' graves), and Sere's trade coming up the Sere road
(Corvin's saltfigs).

**Shape:** a **low green basin round a mere.** Tolling's river comes down from the north-west,
through the town and into **Wennow**, the broad mere that lies across the border between the
built north and the generated south; the **Wenn** leaves it east for Knellmere. North and east
of the mere is heath; south of it the land rises very gently (0 to 1.5) through meadow, then
turns to dry gravel in the south-east corner, rising to about 4: the rain shadow beginning.
The one picture to remember: standing on the dry rise south of Wennow, the whole basin below
you, green, with the town and the mill on the far side of the water.

**Ground (five, from eleven):** grass and meadow (the basin), heath (north and east of the
mere), gravel (the dry edge), lake water; overlays: the Sere road and Palmerway, Tolling's
streets and fields. The generator's dust, redrock, sand, bracken and worn patches in the two
southern regions go: dust and sand become gravel or the shore, redrock goes (it belongs to the
badlands, not here), bracken becomes heath.

**Landmarks and views:**
- **Wennow** itself is the area's shape: seen from the town's south-east, from Palmerway along
  its north shore, and from the dry rise south.
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

The Sere road is drawn as cobble in the south today. Out of town it should be a road of the
empire but not a highway: gravel, faded into the grass, the paving
ending where Tolling ends.

**Wide and narrow:** wide almost everywhere, as a start should be. The narrows are the bridge
in the town, the road between the mere and the heath on Palmerway, and the waygate.

**Edges:** see question 1. Inside the area the only real edge is water (Wennow, the river, the
Wenn); everything else is open to its neighbours.

**Places:**

| Place | Where | Holds |
| --- | --- | --- |
| Tolling (town) | (30, 48), the spawn | a plain square for now; at stage 3 the bank, apothecary, skinning yard and the first quest |
| Widdermill (landmark) | (30, 80) | the mill and its pond, on the river |
| The waygate (landmark) | (180, 48) | the wardens' gate on the road (finding 2) |
| Palmerway's monument | on the road east | Cruor's monument, Brother Hollis (stage 3) |
| **p-edge-farm** (hamlet, proposed) | the Sere road, about 60 tiles south of Tolling, where the meadow meets the gravel | the last green farm before Sere: the first beat on the Sere road (world-plan section 5, "the edge of the green") |
| **s-wennow-south** (site, proposed) | Wennow's south shore | early, kind open: something to find off every road (a drowned thing, a shrine, a fishing spot) |
| **s-dry-rise** (site, proposed) | the gravel rise in `land_1_m1`, about (130, -70) | early, a lookout: the view back over the basin |

The area has no sites in the layout today (the nearest are s-heath-ruin north of Palmerway and
s-reed-fens east of it, both in other areas).

**Counts** (stage 3 is checked against these; skeleton only, the kinds are filled in then):

| | In the old blockout (back at stage 3) | Proposed for the south |
| --- | --- | --- |
| Rocks | copper 5, ironstone 3 | none: the first rocks stay in town and on Palmerway |
| Trees | alder 6, ash 3 (and scenery trees) | a few by the water and the farm; no new kind |
| Fishing | none | one spot on Wennow's south shore, tier 1, when Fishing exists |
| Creatures | fen rats 9, vitriol mites 4 | one early creature of the dry edge, tier 1-2, a handful |
| People | 6 townsfolk, the wardens | the farm's people (p-edge-farm), when it is built |

**History in the ground:** the old toll-house on Palmerway (the road was a toll road before the
waygate); the sleepers' graves; at the edge of the green, field walls running out into the
gravel where the farms used to reach further (the dry is spreading).

**Something to find:** s-wennow-south, and whatever is in Sour Hollow's cask.

**Danger:** none in the town but rats and mites; a little more at the dry edge. Danger reads
before it bites: the dry ground is where it starts.

## Checklist ([world-guide.md](../world-guide.md) section 6)

| | |
| --- | --- |
| Clarity over clutter | The mere is the theme; the south stays sparse (a road, a farm, a rise). |
| Wide and narrow | Wide; narrows at the bridge, the shore road and the waygate. |
| Ground with character | Basin, mere, heath, dry rise: each walks differently. |
| Landmarks shape the skyline | Wennow, the mill, the monument; the spine to the west. |
| Edges are scenery | **Fails today** (question 1). |
| Few ground colours | Five, down from eleven in the south. |
| Paint the RuneScape way | The south is painted; Tolling and Palmerway are not yet (question 2). |
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

## Questions for the owner

1. **The farm and the two sites:** keep p-edge-farm, s-wennow-south and s-dry-rise?
2. **Height:** the basin is low (0 to 1.5) and rises only to the south and the dry edge. Right, or
   should the town stand on a gentle rise above the river?
