# World map

The reference sketch is `images/map.png`. Its shapes are the plan, and its names are all
placeholders. This file records the adopted names and how the world gets built in phases.
Concept art in `images/` (Cruor, Meridian, Frostpeak Forge) sets the mood. The game itself
should still look like Old School RuneScape.

## The shape of the land

- **The range.** A great mountain range curves around the north and west of the continent and
  slopes into the sea at both ends. On the sketch its peaks look like teeth on a wheel; in the
  game they are mountains: a high, broken ridge with passes (the "gaps") through it. The
  Rationalists hold the surface, the dwarves the caves, older peoples the depths.
- **The north-east** is where the range gives out: eroded, broken, and finally eaten by the
  Scalding. Fen and scar, inlets and sea caves. The occult houses.
- **The heartland** is the green river country around the bay. Every river runs to the bay.
- **The west** is Sere: dry rain-shadow country under the range.
- **The south-east** is Tithe's: salt coast, brine pans, bare stone, and the bay's only mouth.
- **Beyond the range** lies frozen country (the white band on the sketch).

Why each power holds its ground is written up in `docs/lore.md`, section I.

## Names

Every name on the sketch was a placeholder. The names below are **adopted for now** (approved
2026-10-20) and used in lore and content; any of them can still change. Cruor, Tithe and
Meridian are settled. A good name here is short, easy to say aloud, and hints at what the place
is for. A player should be able to shout it across a chat channel.

### Settled

| Name | What it is |
|---|---|
| **Cruor** | Capital of the Blood faith and first of its two sees, on the bay. Replaces "Brazenhithe". |
| **Tithe** | Second see. It guards the bay's mouth and taxes every ship. |
| **Meridian** | Capital of the Rationalists, where the range comes down to the sea. |

### realms and capitals

| Realm | Capital | Why |
|---|---|---|
| **The Keeping** (the Blood see's lands) | Cruor | The covenant of blood is a *keeping*, so the realm is what the faith keeps. The phrase "the Keeping's edge" also names where Tolling sits. |
| **The Saltmarch** (Tithe's lands) | Tithe | A border march of brine pans and bare stone. |
| **Sere** (the dry western kingdom, replaces "Lammas") | **Cistern** | *Sere* means dried up. A capital named for its water store says what the kingdom fights over. |
| **The Concord** (the Rationalists) | Meridian | They call themselves an agreement, not a nation. Fits their neutrality. |
| **Holdfast** (the dwarven realm in the caves) | **Frostpeak Forge** (the trade hall, from the concept art) | A holdfast is a grip on rock, and the name says why nobody dislodges them. |
| **The Wrack** (the occult north-east, replaces the unnamed "broken country") | none: the houses hold it between them | *Wrack* means wreckage and seaweed: broken land eaten by the sea. |

### the occult houses of the Wrack

Each house leans towards one bile and practises one art. Names are old words for bitter or
staining things.

| House | Leaning | Character |
|---|---|---|
| **Gall** | Yellow bile, fire | The largest and loudest house. Burns first, argues later. |
| **Umber** | Black bile, shadow | Old, quiet, patient. Keeps the archives and the grudges. |
| **Mordant** | Both biles, corrosion | Acids, vitriols, the arts that eat. Their workshops gave Sour Hollow its cask. |
| **Sallow** | Neither: refinement, the still | A minor house fallen on hard times. Corvin Sallow is its last son in the Keeping. Its rites are the first a player learns. |

### the range

| Name | What it is |
|---|---|
| **The Pale** | The range itself. White peaks around the edge of the known world, and the old phrase "beyond the Pale" comes for free. |
| **The Rime** | The frozen country beyond the Pale. |
| **The Undermost** | The depths beneath the dwarves, held by the oldest peoples. |
| **The Moon Breach** | Keep. The gap the first moon fragment tore through the Pale in the north-west. |
| **The Verdigris Moon** | Keep. The cracked moon. |
| **The Lantern Pass** | The main western pass from Sere through the Pale, lit by Concord beacon towers. |
| **The Orrery** | The Concord's great observatory on the high slopes above Meridian, where moonfall forecasts are made. |
| **The Stair** | The dwarven cart-line climbing from the lowlands to Frostpeak Forge. |
| **The Scald** | Keep. Where the Pale finally breaks apart in the north-east and the ground still burns. |
| **The Gnaw** | A great sinkhole in the broken Pale (replaces "the Mainspring Pit"). |
| **The Teeth** | Sea stacks where the Pale crumbles into the north-east sea (replaces "the Escapement"). |

### the bay and the sea

| Name | What it is |
|---|---|
| **The Cradle** | The round bay. Its water rises and falls like breathing. |
| **The Question** | The shape in the bay (replaces "the Axle"). The Synod calls it *the Answer*. Which name you use says which side you're on. |
| **The Ebb** | Keep for now. The open sea outside Tithe's mouth. |

### Kept as they are

| Name | Notes |
|---|---|
| **Tolling** | The starting town. The bell tolls for each sleeper. |
| **The Backwards Mill** | The mill north of Tolling whose wheel turns against the river. |
| **The Sanguine Synod** | The church of Blood. |
| Sodden, the Drowned Belfry, the Old Causey, Mereling | Places in the Wrack's fens. |
| The Brine Pans, Saltglare, the Shadowless Stones | Places in the Saltmarch. |

### Where Tolling sits

Tolling is a small river town at the far western edge of **the Keeping**, many days upstream of
Cruor, where the green heartland starts to dry out towards Sere. It is the Keeping's outskirts:
the Synod keeps a clerk and a strongbox here and not much else. Trade from Sere passes
through (hence Corvin's saltfigs). The open water at the map's south-east corner is **the Wash**, a
broad mere where the river widens before it runs on east towards the Cradle. It is not the bay.

## Build phases

The world is one tile grid built from regions, one Unity scene each (see CLAUDE.md,
"Regions"). Tolling's south-west corner is (0, 0); east is +x, north is +y. Regions are
usually 96 x 96 tiles. Most of the land between places should be sparse: a road, a few trees
and rocks, one landmark. Unbuilt land is impassable and fades into the fog.

| Region | Tiles (x, y) | Scene |
|---|---|---|
| Tolling | 0-95, 0-95 | `Tolling.unity` |
| The Pilgrim Road | 96-191, 0-95 | `PilgrimRoad.unity` |

1. **Tolling and the river** (built: `client/Assets/Scenes/Tolling.unity`). One riverside town
   on the western outskirts of the Keeping: the river and its bridge, the Backwards Mill and
   millpond, fields, reed beds with fen rats, a lightly wooded east bank with Sour Hollow (vitriol
   mites around a cask that leaked after the Scalding), the Wash at the south-east corner, and
   the sleepers' graves south of town. The townsfolk live here. 96 x 96 tiles.
   **The Pilgrim Road** (built: `PilgrimRoad.unity`) runs east from Tolling along the Wash's
   north shore: open heath, a waystone, the old toll-house, and the wardens' palisade with
   the barred waygate (flag `pilgrim_road_open`), the current edge of the world.
2. **Cruor and the bay shore.** The first capital, days east along the Pilgrim Road. The
   region engine is built; what's left is the land between (mostly sparse road regions) and
   the city itself (about 30 buildings at RuneScape scale).
3. The fens and the occult north-east.
4. Sere and the western passes (the Lantern Pass).
5. Tithe and the south-east coast.
6. The range: passes, Meridian, the dwarven cart-lines and the depths (the deep storyline).
