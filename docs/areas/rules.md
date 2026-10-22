# The area plan: rulebook

How the area plan uses the RuneScape research, so that everyone (the owner, a local agent, cloud
agents) applies it the same way. The evidence is in
[`docs/research/world-density.md`](../research/world-density.md). The numbers live in
[`targets.json`](targets.json), which the plan's checks read. Decisions and set-aside questions
are in [`decisions.md`](decisions.md).

## 1. Principles

1. **Targets describe the whole world, never one place.** RuneScape's averages hide a very uneven
   world:
   - 23 of its 53 cities have no bank nearby;
   - the median settlement starts no quests;
   - only 38% of the 2004 land is within 100 tiles of a bank.

   A target is a range for the world (or a realm) as a whole, for example "40–65% of settlements
   have a bank". It is never a quota for a place ("this village needs a bank"). An absence counts
   towards the target; it isn't a failure.
2. **Character comes before services.** Every place declares its character (section 3), and what
   it has follows from that. A frontier camp without a bank, a city that won't let you teleport
   in, a wild valley with nothing but a path: those are design, not exceptions, and need no
   justification beyond their character.
3. **Only a few rules are firm, and they are about the game working** (section 4), not about how
   the world is laid out.
4. **Variety is checked as well as shortfall.** A world where every village has the same four
   services is as wrong as one with no banks (section 6).
5. **Precedence when sources disagree:** the owner's decisions, then the lore and
   `world-map.md`, then `skills.md`, then the targets. A target never overrides a decision; it
   only flags it.
6. **One subject at a time.** The area plan is built in passes (section 7), and each pass only
   uses its own targets. Effects on other subjects get one line in the parking lot, not a
   discussion.
7. **Placeholders for what isn't known yet.** Things that can't be decided now are typed
   placeholders, for example "a late-game fishing spot, tier 9", "a mid-game boss whose special
   drop is a melee weapon", "an early quest hook on the Sallow thread". They are filled in later,
   one at a time. New places are named `p-{suggestedName}` until the owner names them.

## 2. Definitions

| Term | Meaning |
| --- | --- |
| **Realm** | A large region with its own identity: Venaria, Slough, Sere, Calcinia, Concordia and Candora, Nivil, the Cradle. Undermost and other underground layers are realms of their own, below the surface. |
| **Area** | The unit the plan is built from: a named stretch of world, about 1 to 6 regions of 96 tiles (2 to 5 minutes of running across). Cruor and the Candora range are larger. |
| **Place** | A named spot inside an area: a settlement, a landmark, a dungeon, a boss site, a resource ground. |
| **Site** | One instance of a service or resource, however many pieces it has. Six bank booths are one bank. |
| **Settlement tier** | By size and role, not by services: **city** (Cruor and the capitals), **town**, **village**, **hamlet** (a farm, an inn, a camp, an outpost). |
| **Route distance** | Tiles along walkable routes, counted the way the game counts (Chebyshev, one tile per step). A tile and a tick are the same gameplay units as in RuneScape, so the research numbers carry over without conversion. |
| **Stage** | Rough account progress, used only to check spread, never as a gate. **Early:** novice and intermediate quests, tiers 1–3. **Mid:** experienced quests, tiers 4–6. **Late:** master and grandmaster quests, tiers 7–10. |
| **Reason to visit** | One of **Q** (quest), **S** (skilling), **N** (an NPC or service: shop, bank, trainer, faction), **A** (an activity: a boss or a repeatable minigame), **T** (travel: a transport hub). Each is tagged with its stage. |
| **Heartland and periphery** | The dense centre (Venaria) and the specialised realms around it. |

## 3. Characters

Every place gets one main character, and an area gets the mix of its places. A character
describes what a place **tends** to have. It is a starting point for its design, not a checklist.

| Character | The player should feel | Tends to have | Tends to lack |
| --- | --- | --- | --- |
| **Hub** | At home, provided for | bank, shops, fast travel, many quest starts | danger |
| **Market or port** | Busy, foreign goods, comings and goings | shops, transport, trade NPCs | quiet |
| **Village** | Ordinary life | a shop or two, cooking, local quests | a bank (often), fast travel |
| **Frontier** | At the edge, self-reliant | a camp, a trader, a guard post | bank, fast travel, comfort |
| **Wild** | Alone, exposed | resources, creatures, a path | services of any kind |
| **Occupied or hostile** | Not in control, watched | guards, closed doors, forbidden areas | your bank, your teleports, welcome |
| **Sacred** | Small, observed | a monument or shrine, pilgrims, rites | commerce |
| **Industrial** | Work, noise, smoke | workstations, raw materials, workers | comfort |
| **Ruin** | Something happened here | lore, dungeons, scavenging | people |
| **Hidden** | You found something | a secret, a shortcut, a reward | signposts |

**New characters can be added** when a place needs one. Keep the list short.

## 4. Invariants (firm: break only with an owner's decision)

| Id | Invariant |
| --- | --- |
| **I-REACH** | Every place is reachable on foot from Tolling, except places the plan marks as closed (locked by quest, or later). |
| **I-RETURN** | From anywhere a player can stand, there is a way back to safety and a bank: a route, a home teleport, or the game's death rules. |
| **I-TIERS** | Every tier of every gathering skill exists somewhere, and each skill's best tiers are where `skills.md` puts them. |
| **I-SUPPLY** | For every stage, the supplies a fight needs (food, potions, ammunition, reagents) can be obtained and banked somewhere reachable at that stage. This is world-wide, not per area. |
| **I-NAMES** | Every name in `world-map.md` is placed exactly once, and every new place has a `p-` name until the owner names it. |

## 5. Targets (ranges for the world or a realm)

The numbers, tolerances and evidence are in `targets.json`. This table is the readable index.

| Id | What | Scope | Range | Strength |
| --- | --- | --- | --- | --- |
| T-LAND-SPARSE | Share of land that is empty or sparse | world | 55–75% | guide |
| T-LAND-BUILT | Share of land inside settlements | world | 10–20% | guide |
| T-UNDER | Underground area against the surface | world (Candora much more) | 0.3–0.6 | guide |
| T-CITY-DOMINANCE | Cruor's shops and quest starts against the next city's | world | 1.5–3 × | firm |
| T-LADDER | Towns and villages per 100,000 land tiles | world | 0.5–1.5 towns, 1.5–4 villages | guide |
| T-BANK-SHARE | Share of villages, towns and cities with a bank | world | 40–65% | guide |
| T-BANK-REACH | Share of land within 100 route tiles of a bank | world | 30–55% | guide |
| T-TRAVEL | Fast-travel points: route distance between neighbours | world | 120–200 median | guide |
| T-DUNGEON | Dungeons and caves per named place | world, and per realm except Candora | 0.15–0.35 | guide |
| T-BOSS | Bosses per realm | realm | at least 2 | guide |
| T-ACTIVITY | Repeatable activities | realm | heartland at least 3, periphery at least 1 | guide |
| T-STAGES | Stages a realm has reasons in | realm | at least 2, unless marked a destination | firm |
| T-STAGE-MIX | World-wide share of quests early / mid / late | world | 50–65 / 15–25 / 15–30% | guide |
| T-HEARTLAND | Heartland's share of quests and shops | world | 45–70% | guide |
| T-QUEST-SPREAD | Quest starts per settlement: median against busiest | world | median 0–2, busiest at least 8 | info |

- **Firm:** breaking it needs a decision in `decisions.md`.
- **Guide:** aim for it, and note when the plan sits outside the range.
- **Info:** shown for context, never flagged.

## 6. Variety checks (against monotony)

| Id | Warns when |
| --- | --- |
| V-SAME-SERVICES | More than 40% of the settlements of one tier have exactly the same set of services. |
| V-ABSENCE | Fewer than 30% of settlements lack a bank, or fewer than 50% lack fast travel. The world is too comfortable. |
| V-CHARACTER | A realm uses fewer than 3 different characters, or one character makes up more than half its places. |
| V-EVEN | Banks or fast travel are spaced too evenly: the spread of gaps is narrow. The threshold gets calibrated on the first plan. |
| V-REALM-SHAPE | Two periphery realms have the same profile (the same reasons in the same stages). Each realm should lean towards something. |

## 7. How a pass uses this

1. **Pass 0 (the research) is done.**
2. **Each pass changes only its own subject:**

   | Pass | Subject | Targets it uses |
   | --- | --- | --- |
   | 1 | Skeleton | land, ladder, underground, city dominance |
   | 2 | Reasons to visit | stages, heartland, activities, bosses |
   | 3 | Resources | I-TIERS |
   | 4 | Infrastructure | banks, fast travel, I-RETURN, I-SUPPLY |
   | 5 | Activities | bosses, activities |
   | 6 | Quest hooks | stage mix, quest spread |

3. **At the end of a pass,** run the checks:
   - an invariant or firm target that fails is fixed, or gets a decision;
   - a guide outside its range is fixed or accepted, with one line in the pass's notes;
   - a variety warning is looked at.
4. **A single place that differs from the norm needs nothing but its character.** The decision
   log is for world-wide choices: moving a range, dropping a target, adding a character.
5. **The owner reviews each pass** before the next one starts.
