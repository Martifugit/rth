# Research: how dense a RuneScape world is (pass 0 of the area plan)

Status: complete (2026-10-22). Pass 0 of the area plan: the evidence behind the targets in
[`docs/areas/rules.md`](../areas/rules.md). This file is evidence only: **how** the numbers are
used, and why most of them are world-wide ranges rather than rules for each place, is in that
rulebook.

**Sources:**
- The 2004 world: measured from the 2004scape map data by
  [`tools/research/osrs_world_density.py`](../../tools/research/osrs_world_density.py), on the
  main continent only (Misthalin, Asgarnia, Kandarin, Karamja and the Wilderness).
- RuneScape 3: the RuneScape wiki's structured data (its bucket API: locations, quests, shops,
  monsters, coordinates, and the Bosses, Minigames, Distractions and Diversions, Dungeons, Banks,
  Guilds and Cities categories), fetched by [`rs3_fetch.py`](../../tools/research/rs3_fetch.py)
  and summarised by [`rs3_analyse.py`](../../tools/research/rs3_analyse.py).
- The wiki's [Scale theory](https://runescape.wiki/w/Scale_theory) article.

**Caveats:**
- The wiki data is incomplete: 73 of 337 quests, and about a fifth of the dungeons, bosses and
  activities, couldn't be placed in a region.
- A "site" groups pieces within a few tiles, so a bank with six booths is one bank.
- Counts are orders of magnitude, not exact.

## 1. Scale theory: the game is a compressed picture of the world

Jagex says outright that the game is a scaled-down picture of the lore. In the lore Falador has
about 40,000 people, teleports cross "hundreds of miles", and a forest that takes "a few days" to
cross takes minutes in game. Places that exist in the story don't all exist on the map.

**For R'th:** the lore can keep its distances ("many days upstream of Cruor"). The game shows a
compressed version, sized for play and travel time, not for realism.

## 2. The 2004 world's land

**Size:** the main continent is about 1,150 × 1,090 tiles, with 734,000 tiles of land and about
0.4 underground map squares for every surface one.

**How the land is used,** in 8 × 8 tile patches:

| | Share of land |
| --- | --- |
| Empty | 30% |
| Sparse (1–4 trees or rocks) | 36% |
| Moderate | 31% |
| Dense | 4% |
| Inside settlements | 14% |

**Settlements** form a clear ladder:

| Tier | Examples | Built area | Across | Share of settlements |
| --- | --- | --- | --- | --- |
| City | Ardougne, Varrock | 13–14k tiles | 150–240 | 2 |
| Large town | Falador | 8k | 170 × 110 | 1 |
| Town | Lumbridge, Yanille, Port Khazard | about 3k | 70–100 | about 5 |
| Village | Draynor, Rimmington, Edgeville, Catherby, Al Kharid | 1–1.5k | 40–60 | about 15 |
| Hamlet, farm, outpost | | under 1k | | the rest (about 110 small clusters) |

**Services**, as the gap between neighbouring sites and how much land is within reach:

| | Sites | Typical gap (tiles) | Land within 100 tiles |
| --- | --- | --- | --- |
| Bank | 14 | 84 | 38% |
| Altar | 15 | 120 | 46% |
| Anvil | 13 | 85 | 41% |
| Furnace | 8 | 140 | 29% |
| Cooking range | 52 | 30 | 61% |
| Shop | 67 | 16 (clustered in towns) | 65% |
| Water source | 68 | 17 | 65% |
| Way underground | 59 | 31 | 65% |

**Resources on the surface:**
- Trees are everywhere: normal and oak trees within 50 tiles of half the land.
- Low ores are common: iron at 18 sites, coal 19.
- Higher ores are scarce: mithril 11, adamantite 6, runite 1.
- Fishing has 28 sites.
- The best resources sit underground or in the Wilderness: richness goes with danger.

## 3. RuneScape 3: reasons to visit, by region

Quests are grouped by official difficulty: novice and intermediate count as early, experienced as
mid, master and grandmaster as late.

| Region | Early quests | Mid | Late | Bosses | Minigames + D&Ds | Dungeons | Banks | Lodestones | Shops | Named places |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Misthalin | 46 | 12 | 9 | 28 | 10 | 43 | 11 | 5 | 89 | 176 |
| Asgarnia | 32 | 11 | 3 | 18 | 13 | 55 | 15 | 5 | 88 | 163 |
| Kandarin | 31 | 9 | 11 | 16 | 9 | 43 | 11 | 6 | 86 | 178 |
| Desert | 14 | 2 | 6 | 27 | 5 | 26 | 7 | 3 | 55 | 91 |
| Morytania | 6 | 4 | 3 | 13 | 1 | 16 | 4 | 1 | 28 | 77 |
| Fremennik | 4 | 6 | 4 | 13 | 1 | 19 | 5 | 2 | 51 | 77 |
| Wilderness | 2 | 1 | 1 | 11 | 4 | 18 | 10 | 1 | 16 | 69 |
| Karamja | 3 | 3 | 2 | 8 | 6 | 13 | 2 | 1 | 27 | 42 |
| Tirannwn | 0 | 0 | 6 | 2 | 1 | 8 | 2 | 2 | 26 | 45 |
| Anachronia | 1 | 1 | 1 | 5 | 2 | 4 | 4 | 1 | 13 | 37 |
| Havenhythe | 4 | 1 | 0 | 2 | 1 | 0 | 1 | 1 | 21 | 41 |

**What this shows:**
- **A dense heartland and a specialised periphery.** Three central regions (Misthalin, Asgarnia,
  Kandarin) hold about two thirds of the quests and shops. Outer regions are thinner and lean
  towards one thing:
  - Tirannwn is late game only;
  - the Desert and the Wilderness lean on bosses;
  - Fremennik leans on the mid game.
- **Almost every region has something at every stage.** The exceptions are deliberate: Tirannwn
  is a late-game destination, and Havenhythe is new.
- **Bosses and dungeons are everywhere.** No region has fewer than two bosses. There are about
  1 dungeon for every 4 named places, and 1 boss for every 6.

**Spacing on RS3's main map:**

| | Typical gap (tiles) | 90th percentile |
| --- | --- | --- |
| Bank | 80 | 200 |
| Lodestone (fast travel) | 150 | 240 |
| Dungeon or cave | 34 | 77 |
| Minigame or D&D | 50 | 170 |
| Boss (on the main map) | 115 | 300 |

Both eras agree on banks: about one every **80 tiles** along the ways people travel.

**Cities** in RS3 (shops naming the city, quests starting there, and nearby things):

| City | Shops | Quests started | Banks within 64 | Dungeons within 128 | Activities within 128 |
| --- | --- | --- | --- | --- | --- |
| Varrock | 43 | 24 | 3 | 12 | 7 |
| Lumbridge | 29 | 10 | 0 (one close by) | 13 | 1 |
| Burthorpe | 26 | 10 | 2 | 25 | 6 |
| Falador | 17 | 12 | 2 | 25 | 6 |
| Ardougne | 16 | 15 | 1–4 | 12 | 5 |
| Taverley | 17 | 8 | 2 | 27 | 7 |
| Al Kharid | 9 | 9 | 2 | 10 | 3 |
| Rimmington (village) | 8 | 3 | 1 | 11 | 1 |

The biggest city has about twice the quests and shops of the next tier, and around 5 × a village.
Every settlement, even a village, has 10 or more dungeons and caves within a short walk.

## 4. How uneven it is (why the targets are world-wide ranges)

The averages above hide a very uneven world, and the unevenness is part of the design.

| Of the 53 RS3 settlements the wiki lists as cities | |
| --- | --- |
| Without a bank within 64 tiles | 23 (Lumbridge, Rellekka, Brimhaven, Sophanem, Pollnivneach, Port Khazard, Barbarian Village...) |
| Without a lodestone within 64 tiles | 32 |
| Without a boss within 128 tiles | 33 |
| Without a minigame or D&D within 128 tiles | 20 |

**Quests started per settlement:** the 10th percentile is 0, the median 0, the 90th percentile 9
and the maximum 24 (Varrock). **Shops:** 0, 5, 14 and 43.

**The 2004 world's banks:** only 38% of land is within 100 tiles of one, and some land is 470
tiles from the nearest.

A typical place has little. A few places have a lot. Whole kinds of place (frontiers, wilds,
occupied towns) have none of the comforts on purpose.

## 5. First reading for R'th (superseded by the rulebook)

The rulebook turns these into world-wide ranges with definitions and tolerances. The list below is
the first reading, kept for the record.

These are reference values to check the area plan against. They are placeholders: change them on
purpose when they're wrong.

**Land:**

| | Target |
| --- | --- |
| Empty or sparse land | about two thirds |
| Built land | about 15% |
| Underground | about 0.4 of the surface, much more under Candora |

**Settlements:**
- One very large city (Cruor): about 2 × the next city, and 3 to 5 × a town, in quests, shops
  and services.
- 1–2 other cities or large towns: the capitals Tithe, Meridian and Cistern.
- About 1 town and 3 villages per 100,000 land tiles, plus hamlets and outposts.

**Services, measured along the main routes:**

| | Target |
| --- | --- |
| Bank | about every 80 tiles; none more than about 200 from the next |
| Fast travel (lodestone equivalent) | about every 150 tiles; every realm has at least one |
| Smithing (anvil and furnace) | about every 85–140 tiles, in towns |
| Cooking, water, shops | in every settlement |

**Reasons to visit:**
- **Heartland and periphery.** Venaria is the dense heartland, like Misthalin, Asgarnia and
  Kandarin. Each outer realm is thinner and specialised, with one or two things it is the best
  place for.
- **Quests in every stage in every realm,** except realms that are deliberately late-game
  destinations, where that is a choice.
- **Bosses everywhere:** at least 2 per realm, and about 1 per 6 named places.
- **Repeatable activities** (minigames or D&D equivalents): about 1 per realm in the periphery,
  several in the heartland.
- **Dungeons and caves:** about 1 per 4 named places, and every settlement has several within a
  short walk. Candora is the exception: far more, with whole underground layers.
- **Resources:** low tiers everywhere, high tiers scarce and in dangerous or underground places.
  This matches `skills.md`'s tier ladder.
