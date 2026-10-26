# Knellmere and the lake road: the brief

**Status:** draft (2026-10-26), waiting on the owner. Wave 1. Ground only this pass. The four
regions are **already taken over and being reworked** (uncommitted: path and river strokes in
`world/strokes/lake-road.json`, regions no longer generated); this brief records what the area is
for and what the standard still asks, and changes none of that work.

## The regions

| Region | Tiles | Now | Role in the area |
| --- | --- | --- | --- |
| `land_2_0` | x 192..287, y 0..95 | taken over | the lake road's west end: the reed fens, Knellmere opening out, p-lakeport on the north shore, the rise with the view (s-lake-view) |
| `land_2_m1` | x 192..287, y -96..-1 | taken over | the lake's south-west shore, p-lake-south under the Downs, the Wenn coming in |
| `land_3_0` | x 288..383, y 0..95 | taken over | the lake's east end, the drowned belfry's isle, the outflow (p-lade) and p-weir |
| `land_3_m1` | x 288..383, y -96..-1 | taken over | the Downs' white edge above the south shore, the hill figure (s-hill-figure), the downs brook |

**Neighbours, read-only:** Tolling and Wennow west; Cruor and the meander north; the Downs south.

## The brief

**For:** the first long road, early game: from the waygate to the lake, a market town, the first
boats and fishing, and on to Cruor. The rhythm (world-plan section 5): the river villages'
fields, the rise with the lake view, p-lakeport, the outflow and p-weir, the meander bridge.

**Who:** p-lakeport's fishers, boatmen and traders; the south shore's fishers (p-lake-south);
the miller at p-weir. Stories: the church (the monument on the road into Cruor, in the next
area), the drowned belfry (a mid-game ruin).

**Shape:** **a great lake under a white chalk edge.** Knellmere fills most of the area; reed fens
on its west shore, an isle with a drowned belfry off its south, the Downs' steep north-facing edge
rising over the south shore, the outflow leaving north-east for Cruor. The one picture: coming
over the rise at s-lake-view, the whole lake laid out with Cruor's hill beyond.

**Ground:** grass and meadow, the Downs' chalk grass, fen and reeds at the west shore, lake and
river water; overlays: the gravel paths, dirt tracks, sand and shingle at the shore.

**Landmarks and views:** the lake from the rise (s-lake-view, (232, 92)); the hill figure on the
Downs' edge (s-hill-figure, (330, -54)), seen across the water from the north shore; the belfry
in the lake (s-belfry, (284, 24)).

**Routes:**

| Route | Kind | In this area | Meets |
| --- | --- | --- | --- |
| Palmerway (the lake road) | main | from the waygate east past the fens to p-lakeport, then north-east towards Cruor | Tolling and Wennow; Cruor |
| The Gall road | main | from Cruor south past p-weir | Cruor; the Downs (south) |
| The south-shore road | road | from the lake road near (196, 52) round the west shore to p-lake-south, on south | the Downs, p-riverhold |
| The camp lane | track | p-lake-south south to p-camp | the Downs |
| The Wenn, the outflow, the downs brook | rivers | into and out of Knellmere | Tolling and Wennow; Cruor |

**Places:** p-lakeport (town), p-weir (village), p-lake-south (village); sites s-lake-view,
s-reed-fens, s-belfry, s-hill-figure.

**Counts** (skeleton): fishing round the lake (the area's main resource), reeds and fen rats in
the fens; little else to gather; few creatures (the fens' and the Downs' edge).

**Danger:** safe by the road and the towns; the fens and the isle a little less so.

## What the standard still asks

1. **Squares (cleared 2026-10-26):** the generator's paved octagons at p-lakeport (146 tiles),
   p-weir (58) and p-lake-south (56) were turned back into the ground round them, tile for tile;
   nothing else in the regions changed. A square comes with the settlement at stage 2.
2. **Ground counts:** about ten kinds in `land_2_0`, nine in `land_2_m1`: within reach of the
   standard once the paving goes.

## Questions for the owner

1. Is the uncommitted work in `world/strokes/lake-road.json` and the four regions yours? (The
   squares were cleared on top of it; the files before that are kept outside the repo.)
