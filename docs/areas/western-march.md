# The western march: the brief

**Status:** agreed (2026-10-27); the cave's crag 2026-11-02 (findings 4-6); the regions are taken over and have had a
first ground pass (below). Wave 1. Ground only this pass: no assets, buildings, people or objects. Template: [world-example.md](../world-example.md); the area next
door, [tolling.md](tolling.md).

## The regions

| Region | Tiles | Now | Role in the area |
| --- | --- | --- | --- |
| `land_m1_m1` | x -96..-1, y -96..-1 | taken over | the dry south-west: heath and bracken falling to Sere's river, which crosses it from the foothills (north-west) to the south-east; the spine-beck joins it |
| `land_m1_0` | x -96..-1, y 0..95 | taken over | the march proper: heath, bracken and the first foothills; the salt road west from Tolling, and the well road coming up from the south to meet it |
| `land_m1_1` | x -96..-1, y 96..191 | taken over | the north: p-west-march (the last farm), the observatory spur at its west edge, the cave in the first foothills, Tolling's river coming down from the north-west |

**Neighbours, read-only:** Tolling and Wennow (`land_0_*`) east; the salt pass and the high
valley (`land_m2_*`) west; Selenfall's approach (`land_m1_2`) north; Sere's river, the salient
(`land_m1_m2`) south.

## The brief

**For:** the first walk west, early game. Where a new player first sees the mountains close and
leaves the green behind: the road to the salt pass, the first dungeon (the cave), the first
farm beyond the town, and a village of the surface people on its spur.

**Who:** a last farm (p-west-march); the surface people at p-observatory; herders. Stories: the
surface people (the observatory, moonfall forecasts: Magister Varrow's thread), the salt trade
over the pass.

**Shape:** **rough dry downs climbing west.** From Tolling's basin (about 0.7) the land rises
through heath and bracken to the first foothills (1.5 to 2), with the spine standing behind as
a wall. Two rivers frame it: Tolling's river comes down out of the foothills in the north-east,
Sere's river runs out of them in the south-west, so the march is the dry high ground between two
green valleys. The one picture: from the salt road's crest at about x -60, the spine filling the
west and the observatory on its spur to the north-west.

**Ground (five):** heath, bracken, foothills (the rising ground), meadow by the rivers, river
water; overlays: the gravel path and dirt tracks, a few fields at the farm. The generator's dust,
shingle, scree, sand and small grass patches in `land_m1_m1` go to heath or the bank.

**Landmarks and views:**
- **The spine**, west, from everywhere: keep the salt road's line west clear.
- **The observatory on its spur** (p-observatory, about (-94, 140)), seen from the salt road and
  from the farm.
- Looking back east from the salt road's crest: the Wennow basin and the town.

**Routes:**

| Route | Kind | In this area | Meets |
| --- | --- | --- | --- |
| The salt road | road | from Tolling's west edge (-1, 51) west over the march to about (-60, 50), on to the pass | Tolling; the salt pass (`land_m2_0`) |
| The west road | road | from the salt road at the town's edge north-west to p-west-march, on to p-dry-north | Selenfall's approach (`land_m1_2`) |
| The observatory lane | track | p-west-march to p-observatory | |
| The well road | road | up from p-riverhold through the south-west to join the salt road near (-94, 50) | the salient (`land_m1_m2`), the high valley |
| Tolling's river | river | from the north-west, through `land_m1_1` | Tolling (`land_0_1`) |
| Sere's river | river | across `land_m1_m1`, north-west to south-east | the salient (`land_0_m2`, `land_m1_m2`) |

**Wide and narrow:** open downs; the narrows are the river crossings and the climb into the
foothills where the salt road leaves.

**Edges:** the spine's foothills west (rising into the next area, not an edge here); the rivers
cross it. No hard edges inside the area.

**Places:**

| Place | Where | Holds |
| --- | --- | --- |
| p-west-march (hamlet) | (-30, 110) | the last farm west of Tolling |
| p-observatory (village) | (-94, 140) | the surface people's observatory on a spur |
| s-tolling-cave (site) | (-18, 150) | the first dungeon: a cave in the first foothills |

**Counts** (skeleton; stage 3 checks against these): a few early rocks in the foothills (the
first after the town's); trees only along the rivers and at the farm; one early creature of the
downs and one of the cave; the farm's people and the observatory's.

**History in the ground:** an older track beside the salt road (the salt was carried before the
road was laid); cairns on the downs.

**Something to find:** off the observatory lane; the cave itself is the obvious one.

**Danger:** gentle by the town, emptier and harder towards the foothills and the cave (5 to 15,
the edge of Tolling's first ring).

## Checklist ([world-guide.md](../world-guide.md) section 6)

| | |
| --- | --- |
| Clarity over clutter | Dry downs between two green valleys, the spine behind. |
| Wide and narrow | Open; narrows at the crossings and the climb. |
| Ground with character | Heath and bracken rising to foothills; river meadows. |
| Landmarks shape the skyline | The spine; the observatory. |
| Edges are scenery | No inner edges; the spine is the next area. |
| Few ground colours | Five, plus the fields (down from sixteen in `land_m1_m1`). |
| Paint the RuneScape way | Generated and painted; paths speckled. |
| History in the ground | Old salt track, cairns. |
| Something to find | Off the observatory lane. |
| Danger reads before it bites | Harder towards the foothills. |
| Streets and squares | No town here. |

## What the work found

1. **The dirt tracks were blocked** (fixed 2026-10-26): the generator's characters for land beyond
   the first build included `&`, the dirt track's letter, so once the empire's tracks became dirt
   tracks the salt road read back as blocked far land in `land_m1_0`. The twins now skip every
   ground's letter (`BlockoutTests.EveryGroundsLetterReadsBackAsThatGroundNotLandBeyond`).
2. **First ground pass** (2026-10-26, `world/strokes/western-march.json`, applied once; the tiles
   are the truth now; scenes in `Assets/Scenes/WesternMarch/`):
   - **Rivers meander** instead of running ruler-straight: Tolling's river in `land_m1_1`, Sere's
     river and the spine-beck in `land_m1_m1`, at the generator's widths, each meeting its
     neighbours where it did. The bright shingle and sand strips along them became meadow banks
     with a little shingle; islets of a few tiles went back to the water.
   - **The west road** crosses Tolling's river once, on a short bridge at about (-58, 177); the
     generator had it running along the water on a long one.
   - **Grounds:** the stray dust, gravel, olive, scree and sand went to the ground round them, and
     scraps of field (furrow under 45 tiles, ley and wheat under 25) to the land round them. Left:
     heath, bracken, foothills, grass and meadow, the rivers, the dirt tracks, and the fields of the
     high valley (south-west) and the observatory (north-west). `land_m1_0` had only five already.
3. **Selenfall's approach doesn't meet the march** (not changed; Selenfall's work, L4): `land_m1_2`'s
   river reaches the border at x -33, about 40 tiles east of where the layout (and this area) has it
   at x -73, and its road arrives at x -76 where the march's west road has it at x -61. Decided
   (2026-10-27): keep the approach (part of L4's approved stage 1) and move its river and road at the
   southern border to meet the march's, rather than scrapping and regenerating it.

4. **The cave's crag** (2026-11-02, `world/strokes/western-march-cave.json`, applied once): s-tolling-cave
   stood on flat meadow by Tolling's river, with nothing for a cave to be in. A crag of the first
   foothills now rises north-east of the river, about 2.6 at its top: gentle on its north, east and
   south where it meets the land climbing towards the dry valley, a rock face (scree, cliffs) on its
   west over the river and towards the town, and the site at the face's southern foot, where the
   mouth goes at stage 2. Made with the strokes shapes' `raise` and a `cliffSide`.
5. **Landforms, water and ground otherwise as the first pass left them** (1.3-1.5): the land climbs
   west from about 0.7 to the foothills' 2 to 4, the observatory's spur stands at about 4 in the
   north-west, the two rivers meander, the west road crosses Tolling's river on its short bridge and
   the well road Sere's river on one. The only other cliffs are on the area's west edge, where the
   foothills rise into the salt pass. Grounds: foothills, heath, bracken, grass and meadow, the rivers,
   the tracks and the two farms' fields.
6. **The well road** (2.1, open): only its south-west end is in the data (a short stretch of caravan
   road by Sere's river); the line up to the salt road near (-94, 50) is still to draw.

## Decided (2026-10-27)

The march is the dry high ground between two green river valleys, as above: the contrast is what
makes leaving Tolling west feel like leaving. It is the west pull in Tolling's "Outwards" table
(`tolling.md`): the spine and the observatory seen from the town, Corvin's errand to the cave, the
observatory's watchers as the first, unexplained hint of moonfall (which belongs to later, higher
content). The the cave (s-tolling-cave) stays as the first dungeon, close to the town, so
players see early that dungeons exist; Selenfall's approach is fixed at the seam, not scrapped
(finding 3).
