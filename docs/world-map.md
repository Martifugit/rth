# World map

The reference sketch is `images/map.png`. Its shapes are the plan; many of its names are
placeholders. This file records which names are settled and how the world gets built in phases.

## The shape of the land

- **The range.** A great mountain range curves around the north and west of the continent and
  slopes into the sea at both ends. On the sketch its peaks look like teeth on a wheel; in the
  game they are mountains: a high, broken ridge with passes (the "gaps") through it. The
  Rationalists hold the surface, the dwarves the caves, older peoples the depths.
- **The north-east** is where the range gives out: eroded, broken, and finally eaten by the
  Scalding. Fen and scar, inlets and sea caves. The occult houses.
- **The heartland** is the green river country around the bay. Every river runs to the bay.
- **The west** is Lammas: dry rain-shadow country under the range.
- **The south-east** is Tithe's: salt coast, brine pans, bare stone, and the bay's only mouth.
- **Beyond the range** lies frozen country (the white band on the sketch).

Why each power holds its ground is written up in `docs/lore.md`, section I.

## Names

Settled:

| Place | What it is |
|---|---|
| **Cruor** | First see of the Blood faith, on the bay (the covenant is a *keeping*). Replaces "Brazenhithe". |
| **Tithe** | Second see, guarding the bay's mouth (the covenant is a *debt*). Replaces "Noonhaven". |
| **Meridian** | The Rationalist capital, where the range meets the sea. |

Provisional (fine for now, revisit with the region):

| Place | Notes |
|---|---|
| **Lammas** | The dry western kingdom. Good, but a real English word; revisit. |
| **Tolling** | The riverside town where the player wakes (Phase 1). The bell tolls for each sleeper. |
| **The Backwards Mill** | A mill north of Tolling whose wheel turns against the river. Keep the oddity. |
| The Moon Breach, the Verdigris Moon | Where the first fragment hit the range. Strong; keep. |
| Sodden, the Drowned Belfry, the Old Causey, Mereling | Fen places. Fine. |
| The Brine Pans, Saltglare, the Shadowless Stones | Tithe's hinterland. Fine. |

To replace (read too much like clockwork, which the world isn't):

| Place | Notes |
|---|---|
| The Axle | The shape in the bay. It should stay unexplained; a name people argue about. |
| The Mainspring Pit, the Escapement | North-east sites in the broken range. |
| The Ebb | Fine as a sea name; check once the bay is built. |

## Build phases

The engine handles one map area at a time well, so the world is built a region at a time.

1. **Tolling and the river** (built: `client/Assets/Scenes/Tolling.unity`). One riverside town west of the bay: the river and its
   bridge, the Backwards Mill and millpond, fields, reed beds with fen rats, a wooded east bank
   with Sour Hollow (vitriol mites around a cask that leaked after the Scalding), and the bay
   shore at the edge, and the sleepers' graves south of town. The townsfolk live here. 96 x 96 tiles.
2. **Cruor and the bay shore.** The first capital. Needs a second map area (regions and
   walking between them), so it's also the engine milestone for a multi-region world.
3. The fens and the occult north-east.
4. Lammas and the western passes.
5. Tithe and the south-east coast.
6. The range: passes, Meridian, the dwarven cart-lines and the depths (the deep storyline).
