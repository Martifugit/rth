# World map

The reference is the concept map, `docs/lore-rework/concept-map.png` (drawn by
`tools/areas/concept_map.py` from `docs/lore-rework/powers.md`). It is a concept, not a layout:
positions are rough and will be settled by the area plan. The old sketch, `images/map.png`, is
**superseded**: its ring of mountains, its bay and its realms are no longer canon. Concept art in
`images/` (Cruor, Meridian, Frostpeak Forge) still sets the mood. The game itself should look like
Old School RuneScape.

The lore behind every place is in `docs/lore.md`.

## The shape of the land

- **The spine** is a long mountain range from the warm south **north into the cold**, with land on
  both sides. Its surface people hold the passes and Meridian; the dwarves hold its caves and
  Frostpeak Forge, at its frozen northern end; Undermost lies beneath. Selenfall, the scar of the
  first moonfall, is on it.
- **Cruor's empire** fills the heart of the continent east of the spine: rivers, farmland, a great
  lake, a great forest and roads, with Cruor in the middle. Its border reaches into every
  neighbour: up Sere's river, into the spine's foothills, a fortress pocket in Gall's land, and a
  coastal enclave in the north-east fens.
- **House Gall's kingdom** is the volcanic south-east.
- **The lesser occult houses** hold the north-eastern fens, sea caves and broken coast.
- **Sere** is the dry country east of the spine's southern half, in its rain shadow, around
  Cistern.
- **The cold north** lies above the empire and the houses.
- **Frontiers**, where the world grows later: beyond the spine to the west, the far north, and
  islands to the south and east.
- **Everywhere has features.** There is no generic lowland: every stretch of country has its own
  landmarks and points of interest. The spine is the most extreme of them, not the only one.

## Names

**The naming style:** a place gets a **proper name with a meaning under it**, not a description
with "the" in front. It should be short, easy to say aloud, and something a player could shout
across a chat channel. Nicknames for things nobody understands, and buildings, may keep "the".

Placeholders are written `p-something` and never reach players. Until a place or power is named,
the game keeps its current wording.

### Kept

| Name | What it is |
|---|---|
| **Cruor** | The capital of the empire, and of p-sworn's church. |
| **Sere** | The dry kingdom in the spine's rain shadow. *Sere* means dried up. |
| **Cistern** | Sere's capital, built on its deepest wells. |
| **Meridian** | The capital of the spine's surface people, on the spine's slopes (no longer by the sea). |
| **Frostpeak Forge** | The dwarves' trading hall, at the spine's frozen northern end. |
| **Undermost** | The depths beneath the dwarves. |
| **Selenfall** | The scar the first moon fragment tore in the spine. |
| **House Gall** | The dominant occult house, and its kingdom. |
| **Umber, Mordant, Sallow** | Lesser occult houses. Sallow is fallen; Corvin is its last son in the empire. |
| **The Faceless** | The betrayers' followers. |
| **Tolling** | The starting town. |
| **Widdermill** | The mill north of Tolling whose wheel turns against the river. *Widdershins*: against the way of the sun. |
| **Wennow** | The broad mere where Tolling's river widens before running on east. |
| **Palmerway** | The road east from Tolling along Wennow's north shore, towards Cruor. A palmer is a pilgrim. |

### To be renamed

| Current name | What it is | Note |
|---|---|---|
| **The Sanguine Synod** | p-sworn's church | Stays in the game until renamed. |
| **The Rationalists** | The spine's surface people | Stays in the game until renamed. |
| **Scalth** | Where the vitriol mites and their salt come from | Gets an origin that fits the new map, likely the lesser houses' broken land. |

### Under review

Names from the old map whose places may survive in a new form: **Venaria** (for the empire's
heartland), **Candora** (for the spine), **Slough** (for the lesser houses' fens), **Nivil** (for the
cold north), **Concordia** (the surface people's realm), **Dunstair** (the dwarves' cart-line),
**Lucern Pass**, **the Orrery**, **the Veilwood**, **Sodden**, **Knellmere**, **Causeholm**,
**Mereling**, **Swallow**, **Skerrin**.

### Retired

**Tithe**, **Calcinia** and its places (**Salinas**, **Saltglare**, **Ascia**), **the Cradle** and
**the Quaere** (the bay and the shape in it), and **the Verdigris Moon** (the cracked moon has no
name yet).

### Placeholders in use

p-sworn, p-betrayers, p-scattered, p-head, p-constants, p-spoil; the realm of the dead, the
ferryman and the walker; the concept map's p-forest, p-lake, p-gall-seat, p-houses,
p-dwarf-hold and p-demigod sites.

## Where Tolling sits

Tolling is a small river town at the far western edge of **Cruor's empire**, near Sere, where the
green country starts to dry out. The church keeps a clerk and a strongbox here and not much else.
Trade from Sere passes through (hence Corvin's saltfigs). **Wennow**, at the region's south-east
corner, is a broad mere where the river widens before it runs on east. **Palmerway** runs east
from Tolling along Wennow's north shore, towards Cruor.

## Build phases

The world is one tile grid built from regions, one Unity scene each (see CLAUDE.md, "Regions").
Tolling's south-west corner is (0, 0); east is +x, north is +y. Regions are usually 96 x 96
tiles. Most of the land between places should be sparse: a road, a few trees and rocks, one
landmark. Unbuilt land is impassable and fades into the fog.

| Region | Tiles (x, y) | Scene |
|---|---|---|
| Tolling | 0-95, 0-95 | `Tolling.unity` |
| Palmerway | 96-191, 0-95 | `Palmerway.unity` |
| The Saint's Crypt | instanced | `SaintsCrypt.unity` |

1. **Tolling and Palmerway** (built). Tolling: the river and its bridge, Widdermill and its
   millpond, fields, reed beds with fen rats, Sour Hollow (vitriol mites around a leaking cask),
   Wennow, the sleepers' graves, and the townsfolk. Palmerway: open heath, a waystone, the old
   toll-house, the church's monument, and the wardens' palisade with the barred waygate (flag
   `palmerway_open`), the current edge of the world.
2. **The realm of the dead** (death v1): a small, very dark landing with a bank and the ferryman.
3. **The road to Cruor, and Cruor.**
4. **Everything after** comes in the order the area plan sets (`docs/areas/`, redone on the
   concept map).
