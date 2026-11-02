# Knellmere and the lake road: the brief

**Status:** agreed (2026-10-27); Knellmere reshaped 2026-11-02 (findings 4-7). Wave 1. Ground only this pass: no assets, buildings, people or
objects. The four regions are taken over and have had a first ground pass (path and river strokes,
`world/strokes/lake-road.json`, applied once; the tiles are the truth now). Template:
[world-example.md](../world-example.md); the area before it on the road, [tolling.md](tolling.md).

## The regions

| Region | Tiles | Now | Role in the area |
| --- | --- | --- | --- |
| `land_2_0` | x 192..287, y 0..95 | taken over | the lake road's west end: the reed fens, Knellmere opening out, p-lakeport on the north shore, the rise with the view (s-lake-view) |
| `land_2_m1` | x 192..287, y -96..-1 | taken over | the lake's south-west shore, p-lake-south under the Downs, the Wenn coming in |
| `land_3_0` | x 288..383, y 0..95 | taken over | the lake's east end, the drowned belfry's isle, the outflow and p-weir |
| `land_3_m1` | x 288..383, y -96..-1 | taken over | the Downs' white edge above the south shore, the hill figure (s-hill-figure), the downs brook |

**Neighbours, read-only:** Tolling and Wennow (`land_1_0`, `land_1_m1`) west; Cruor and the meander
(`land_2_1`, `land_3_1`) north; the Downs (`land_2_m2`, `land_3_m2`) south; the Wolds (`land_4_0`)
east.

## The brief

**For:** the first long road, early game (5 to 20). After Tolling's closed little basin, the world
opens: a great lake, the first boats and fishing, a market town, and Cruor's hill ahead. It is where
a player who followed the main road first feels how big the world is. The rhythm (world-plan
section 5): the reed fens, the rise with the lake view, p-lakeport, the outflow and p-weir, the
meander bridge into Cruor.

**Who:** p-lakeport's fishers, boatmen and traders; the south shore's fishers (p-lake-south); the
miller at p-weir. Stories: the church (the monument on the road into Cruor, in the next area); the
drowned belfry, whose bell is the twin of Tolling's (`docs/quests/the-bell.md`), so the player's
own story points here; the Downs' hill figure, older than the empire.

**Shape:** **a great lake under a white chalk edge.** Knellmere fills most of the area: reed fens on
its west shore, an isle off its south with a drowned belfry standing out of the water, the Downs'
steep north-facing edge rising over the south shore, the outflow leaving north-east for Cruor. The
lake is flat at 0, the north shore rolls gently (0 to 2), the Downs climb to 3 to 5 behind the south
shore. The one picture: coming over the rise at s-lake-view, the whole lake laid out below, the
belfry in the water, and Cruor's hill beyond.

**Levels** ([world-scale.md](../world-scale.md) section 3, planned 2026-11-02):

**The heartland's rim** (shared by the four wave-1 areas, 2026-11-02): Tolling's basin and
Knellmere lie inside a rim of high ground. West, **the march edge**: an east-facing escarpment about
6 high, the march's plateau behind it climbing to the spine. South, **the Edge**: one escarpment from
Tolling's dry rise east under Knellmere, gravel and dry in Tolling, the Downs' white chalk cliff at
the lake, 6 to 12 high, its lip a line of lookouts. East of Tolling, **the heath ridge** with the
waygate in its notch. North the land stays low and open towards the dry valley.

| Level | Height | What is there | What it sees |
| --- | --- | --- | --- |
| The lake | 0 to 2 | Knellmere, the reed fens, the shores, p-lakeport's quay, p-lake-south's landing | the Downs' white cliff over the south shore, the isle |
| The heath ridge's saddle | about 3.5 (the ridge 6 either side) | Palmerway cresting past the waygate (Tolling's area), s-lake-view on its east lip | the whole lake at once, the belfry, Cruor's hill beyond |
| p-lakeport's terrace | about 2.5 over its quay | the town above the quay on the north shore | the lake, the isle, the Downs' cliff |
| The isle | about 5 | rock, a cliff on its south | |
| The Downs | the Edge's chalk cliff of 10 to 12, the downs 12 to 15 behind | the hill figure cut in the cliff's face, s-hill-figure on its lip | the lake and everything north of it |

**The ways between them:** Palmerway crests the heath ridge's saddle and comes down past the fens to p-lakeport's terrace; the
south-shore road and the camp lane climb the Downs' cliff in combes (dry valleys cut into it) by
switchbacks; the downs brook falls over the cliff; boats to the isle.

**Ground (five):** grass and meadow (the north shore), the Downs' chalk grass, reeds and fen (the
west shore), lake and river water; overlays: the gravel road and dirt tracks, sand and shingle at
the shore. The strays (worn, broken ground, mud, heath scraps) go to the ground round them at 1.5.

**Landmarks and views:**
- **The lake from the rise** (s-lake-view, (232, 92)): keep the line from the rise across the
  water to Cruor's hill open; nothing tall on the north shore between them.
- **The drowned belfry** (s-belfry, (284, 24)): seen from the lake road, from p-lakeport's quay and
  from the south shore; the first thing a player from Tolling recognises, because they have been
  told about it.
- **The hill figure** on the Downs' edge (s-hill-figure, (330, -54)): seen across the water from
  the north shore and p-lakeport.

**Routes:**

| Route | Kind | In this area | Meets |
| --- | --- | --- | --- |
| Palmerway (the lake road) | main | from the waygate east past the fens and over the rise to p-lakeport, then north-east towards Cruor | Tolling and Wennow (`land_1_0`); Cruor (`land_2_1`) |
| The Gall road | main | from Cruor south past p-weir and the outflow | Cruor (`land_3_1`); the Downs (`land_3_m2`) |
| The south-shore road | road | from the lake road near (196, 52) round the west shore to p-lake-south, on south | the Downs, p-riverhold |
| The camp lane | track | p-lake-south south to p-camp | the Downs |
| The Wenn | river | into Knellmere's west end from Wennow | Tolling and Wennow (`land_1_m1`) |
| The outflow | river | out of the lake's north-east corner past p-weir to the great river | Cruor (`land_3_1`) |
| The downs brook | river | off the Downs into the south shore | the Downs |

**Wide and narrow:** the lake makes it wide; the narrows are the causeway through the reed fens,
the rise (one road over it, so the view arrives all at once), p-weir's bridge on the outflow, and
the strip of shore under the Downs' edge.

**Edges:** the lake is the area's edge and its middle: you go round it, or by boat. The Downs'
edge walls the south shore (climbable only where the roads go up). No other hard edges.

**Places:**

| Place | Where | Holds |
| --- | --- | --- |
| p-lakeport (town) | (262, 70), the north shore | boats on the lake, fishing, the first big market on the road; the quay looks at the belfry |
| p-weir (village) | (356, 92), on the outflow | a mill and weir; the Gall road's crossing |
| p-lake-south (village) | (262, -28), the south shore | fishers under the Downs; the nearest landing for the isle |
| s-lake-view (site) | (232, 92) | the lookout on the rise |
| s-reed-fens (site) | (210, 36) | fishing and fen rats: Wenna's errand from Tolling ends here |
| s-belfry (site) | (284, 24) | the drowned belfry on its isle, mid game |
| s-hill-figure (site) | (330, -54) | the figure cut in the chalk, a lookout |

**Outwards:** p-lakeport is the first town after Tolling, so it hands the player on: north-east to
Cruor (the road continues, the city in view); south round the shore to p-lake-south and the Downs
(the hill figure seen across the water); by boat to the belfry's isle when the player is ready for
it. The belfry is visible from the start of the area and reachable only later: a promise.

**Counts** (skeleton; stage 3 checks against these):

| | |
| --- | --- |
| Fishing | the area's main resource: spots round the lake (tiers 1 to 3), the reed fens and the outflow |
| Reeds and fen | a reed bed in the fens (when a skill uses reeds) |
| Rocks | none on the lake; a few chalk or flint seams on the Downs' edge |
| Trees | willows and alders along the shores, sparse |
| Creatures | fen rats in the fens (tier 1), something on the Downs' edge (tier 2), the isle's own (mid) |
| People | p-lakeport's market and boatmen, the fishers of both shores, the miller |

**History in the ground:** the belfry: a village stood where the lake is now (the water came, or the
village was moved; nobody agrees which); the hill figure, recut over centuries by people who no
longer know what it shows; old jetty posts along the north shore below today's.

**Something to find:** the belfry; the hill figure's eye (a lookout); a cut in the fens.

**Danger:** safe by the road and the towns; the fens and the Downs' edge a little less; the isle
mid game, read from the shore before anyone goes.

## Checklist ([world-guide.md](../world-guide.md) section 6)

| | |
| --- | --- |
| Clarity over clutter | The lake is the theme; the shores stay open. |
| Wide and narrow | Wide water; narrows at the fen causeway, the rise, the weir and under the Downs. |
| Ground with character | Fen, open shore, chalk downs: each walks differently. |
| Landmarks shape the skyline | The belfry in the water, the hill figure, Cruor's hill beyond. |
| Edges are scenery | The lake and the Downs' edge. |
| Few ground colours | Five, after the strays go at 1.5. |
| Paint the RuneScape way | Painted; shores to be checked against the water-edge rules. |
| History in the ground | The drowned village, the recut figure, old jetty posts. |
| Something to find | The belfry, the figure's eye, the fens. |
| Danger reads before it bites | The isle is seen long before it is reached. |
| Streets and squares | p-lakeport's square and quay at stage 2. |

## What the work found

1. **Squares** (cleared 2026-10-26): the generator's paved octagons at p-lakeport (146 tiles), p-weir
   (58) and p-lake-south (56) were turned back into the ground round them. A square comes with the
   settlement at stage 2.
2. **Ground counts:** fourteen kinds in `land_2_0`, thirteen in `land_2_m1`, most of them a few
   tiles each: trimmed to the five above at 1.5.
3. **The first ground pass** (`world/strokes/lake-road.json`) is kept as the base; the owner's walk
   (1.6) judges it.
4. **Knellmere reshaped** (2026-11-02, `world/strokes/lake-road-knellmere.json`, applied once). The
   generator's round lake had its isle in the middle and stopped about 40 tiles short of the Downs, so
   the white edge stood over nothing. Now the lake keeps its north shore by p-lakeport and the reed fens
   on the west (where the Wenn comes in), and reaches a broad lobe south-east under the Downs: the
   scarp stands over a strip of shingle shore 8 to 12 tiles wide, and the downs brook runs straight off
   the edge into it. About 7,900 tiles of water (was 5,700), level at 0.1.
5. **The isle moved off the south shore** (`belfry-isle`, about 290 tiles, rising to 1.4, steeper on
   its south side, a shingle edge): about 10 tiles of water from the south shore, 30 from
   p-lake-south's landing. s-belfry moved there, (300, -15) in `continent.json`; the belfry stands on
   or off it at stage 2. Seen from the lake road and p-lakeport across the whole lake (about 70 tiles:
   within the fog, at the edge of what loads).
6. **Ground (1.5):** worn and broken ground and heath scraps went to the land round them, mud away
   from the water likewise, the old sand rim with the old water; reeds along the west shore, shingle
   under the Downs, a little sand by p-lakeport. Left: grass, the Downs' grass and chalk, reeds and
   fen, lake and river, the road and tracks, shingle at the shore.
7. **Heights:** the only tiles steeper than a cliff are the Downs' scarp, as meant.
8. **The Edge and the Downs built** (2026-11-02, `world/strokes/heartland-edge-a.json`, `-b`): the
   Downs' face is now a white chalk cliff of about 9 over a shingle strip 8 tiles wide under it, the
   downs 12 to 13 behind, dipping south (the generated Downs meet them at the border). The camp lane
   climbs from p-lake-south up a combe and on in a deep hollow way through the chalk onto the top;
   the downs brook falls over the cliff into a pool and the lake; a shore path runs along the strip
   over the brook's mouth; a path leaves the camp lane at the top, crosses the brook above its fall
   and runs along the top to s-hill-figure on the lip. The north shore's rise, the isle's height and
   the outflow's weir are still to do.
9. **The north shore** (2026-11-02, `world/strokes/lake-road-levels.json`): the view arrives at the heath
   ridge's saddle, where Palmerway crests past the waygate, so s-lake-view moved there, (205, 51), and no
   second rise was built on the north shore; the pass road now ends where it meets Palmerway's old line.
   p-lakeport stands on a terrace about 2 above its quay (Palmerway graded up onto it); the belfry's isle
   rises to about 5, a rock face on its south. The outflow leaves the lake at the lake's own level, so
   p-weir's weir is a structure at stage 2, not a fall.
