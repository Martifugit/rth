# Cruor and the meander: the brief

**Status:** agreed (2026-10-27). Wave 1. Ground only this pass: no assets, buildings, people or
objects. The four regions are taken over and have had a first ground pass (path and river strokes,
`world/strokes/cruor.json`, applied once; the tiles are the truth now). The city itself is designed
in [../regions/cruor.md](../regions/cruor.md) (on the old map; its layout direction and building
budget still hold: about 30 buildings, not dense). Template: [world-example.md](../world-example.md).

## The regions

| Region | Tiles | Now | Role in the area |
| --- | --- | --- | --- |
| `land_3_1` | x 288..383, y 96..191 | taken over | Cruor's hill in the great river's loop, the confluence with the outflow, the undercroft site |
| `land_2_1` | x 192..287, y 96..191 | taken over | the lake road's last stretch into the city, fields, the blood monument (s-lake-monument) |
| `land_4_1` | x 384..479, y 96..191 | taken over | the great river leaving east, heath and bracken towards the Wolds, the east road |
| `land_3_2` | x 288..383, y 192..287 | taken over | the river's northern loop, the north road out, Eller's first trees |

**Neighbours, read-only:** the lake road (`land_2_0`, `land_3_0`) south; the great bridge and the
dry valley (`land_1_1`, `land_2_2`) west and north-west; Eller (`land_3_3`) north; the east road
(`land_4_2`, `land_5_1`) north-east and east.

## The brief

**For:** the capital, early to late: every power's road passes it. A new player arrives from the
lake road in their first hours and should feel they have reached the centre of the world; they come
back all game, because every main road starts here. It is the one busy place in wave 1, after the
calm of Tolling and the lake.

**Who:** the empire and p-sworn's church: the cathedral, the blood-knights (Sister Odile Vane), the
monuments and their keepers; traders from every power; the poor under the hill. Stories: the
bleeding god (what the blood is really for), the Faceless (a city of trusted faces), the undercroft.

**Shape:** **a hill in a river's loop.** The great river comes from the north-west and the lake's
outflow from the south-west; they meet below the hill, and the river loops round it on three sides
before running east. The hill rises to 4 or 5 above water meadows at 0 to 1; everything round it is
low and open: fields, the monument road, gardens, heath to the east, Eller's first trees north. The
one picture: from the monument on the lake road, the city on its hill inside the bright loop of
water, the cathedral at the crown.

**Ground (five):** grass and meadow, fields (wheat, ley, furrow) round the city, heath and bracken
east, forest at Eller's edge north, river water; overlays: the gravel roads in, the bridges, the
city's own paving at stage 2. Strays (worn, broken ground, sand and shingle scraps, reeds away from
water) go at 1.5; the forest kinds in `land_3_2` (forest, deepwood, glade) count as one, Eller's.

**Landmarks and views:**
- **Cruor's hill and the cathedral** at its crown, seen from everywhere in the heartland: from the
  lake road's rise (s-lake-view), from the great bridge, from the Downs. Keep every road's last
  approach open towards it: no copse or rise on the last 60 tiles of any road in.
- **The confluence** below the hill, where the outflow meets the great river: seen from the
  monument and the south bridge.
- **The blood monument** on the lake road (s-lake-monument, (282, 132)): the gate before the gate.

**Routes:**

| Route | Kind | In this area | Meets |
| --- | --- | --- | --- |
| Palmerway (the lake road) | main | from the lake past the monument and over the outflow into the city's south gate | the lake road (`land_2_0`, `land_3_0`) |
| The north road | main | north out of the loop, over the great river | the great bridge (p-greatbridge), on to Meridian |
| The east road | main | east down the great river's valley | the Wolds, p-east-road, the enclave |
| The forest road | main | north-east into Eller | p-forestgate |
| The Gall road | main | south past p-weir | the lake road (`land_3_0`), on to the front |
| The great river | river | from the north-west, round the hill, east | the great bridge; the Wolds |
| The outflow | river | from the lake into the great river below the hill | the lake road |

Every road crosses water to reach the city: the bridges are its gates.

**Wide and narrow:** wide open round the city, as a capital's approaches should be; the narrows are
its bridges and gates, where every road funnels in.

**Edges:** the river is the city's wall on three sides; the open land beyond is scenery. No hard
edges elsewhere.

**Places:**

| Place | Where | Holds |
| --- | --- | --- |
| Cruor (city) | (322, 176) | the empire's capital and p-sworn's cathedral; streets and squares at stage 2 (`docs/regions/cruor.md`) |
| s-lake-monument (site) | (282, 132) | a blood monument on the road in, early: the first the player sees after Palmerway's |
| s-undercroft (site) | (330, 150) | the undercroft below the hill, mid game: the city's dungeon |

**Outwards:** Cruor is the hub the first hours lead to. Five main roads leave it, each to a
different power or region, each signposted at its gate and each with a beat within sight of the
city (the great bridge, Eller's edge, the Wolds' farms, p-weir), so leaving feels as clear as
arriving. Danger rises along each road, not round the city.

**Counts** (skeleton; stage 3 checks against these):

| | |
| --- | --- |
| Gathering | little round a capital: farmland, a few trees by the river, fishing at the confluence |
| Creatures | farmland vermin (tier 1); nothing aggressive in the loop |
| People | the city's, at stage 3 (about 100 at most, `docs/regions/cruor.md`) |

**History in the ground:** older walls inside the newer ones; a filled moat across the loop's neck;
monuments along the roads in, the oldest worn smooth.

**Something to find:** the undercroft's ways in; the oldest monument; what the confluence washes up.

**Danger:** none in the city's loop; the undercroft below it is mid game.

## Checklist ([world-guide.md](../world-guide.md) section 6)

| | |
| --- | --- |
| Clarity over clutter | One hill, one loop of river; the land round it open. |
| Wide and narrow | Wide approaches; narrows at the bridges and gates. |
| Ground with character | Water meadows, fields, the hill, heath east, forest north. |
| Landmarks shape the skyline | The cathedral on the hill, from the whole heartland. |
| Edges are scenery | The river. |
| Few ground colours | Five, after the strays go at 1.5. |
| Paint the RuneScape way | Painted; the city's paving at stage 2. |
| History in the ground | Old walls, the filled moat, the worn monuments. |
| Something to find | The undercroft's ways in, the oldest monument. |
| Danger reads before it bites | Safe city; the undercroft is entered on purpose. |
| Streets and squares | The city's, at stage 2. |

## What the work found

1. **The square** (cleared 2026-10-26): the generator's 364 tiles of paving at the city's centre in
   `land_3_1` were turned back into the ground round them. The city's streets and squares come at
   stage 2 with its blockout.
2. **Ground counts:** fifteen kinds in `land_3_1` and `land_3_2`, most a few tiles each: trimmed to
   the five above at 1.5.
3. **The first ground pass** (`world/strokes/cruor.json`) is kept as the base; the owner's walk
   (1.6) judges it.
