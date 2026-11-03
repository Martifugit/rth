# Pillars and non-goals

*Draft (2026-11-03), for the owner: the pillars are the owner's to decide. These candidates are
each drawn from decisions already written down (sources under each); none is new. Accept, cut or
rewrite them.*

**Pillars** are the few experiences R'th delivers. Every feature serves at least one
([writing.md](../writing.md), rule 6). Each pillar rules real ideas out, and says what.

## Candidate pillars

| # | Pillar | What it means in play | It rules out |
| --- | --- | --- | --- |
| 1 | **Your own supply** | A player can supply every fight they reach by themselves: food, potions, reagents, ammunition, verses, gear and tools all come from their own skills. Every skill is a pipeline that feeds a fight. | Content whose supplies can only be bought from other players; a skill that feeds nothing in a fight; a requirement chain that can't be completed alone. |
| 2 | **A world that is being revised** | The world was made unfinished and is changing again. Its people barely understand it; the truth comes out slowly, in three layers (believed, suspected, true), and some of it stays sealed. Quests end with more questions than they started with. | A lore dump; explaining the maker; a quest that answers a sealed question; anything that removes a place from the game. |
| 3 | **Every place has its reasons** | The world is hand-made, land first: every town sits where a player can guess why, every place is worth visiting at more than one stage, and resources come from the kind of land they belong to. | Level-banded zones; places tiered by the build order; a region that makes an older one obsolete; generated land left as the final world. |
| 4 | **Fights shaped by the humours** | Combat runs on the body: blood, phlegm and bile. Each style has its own engine (the bile axis, Cruor's verses, ranged's meter) and feels different, not just looks different. | A style that is another one recoloured; a damage button with nothing to read or decide. |
| 5 | **Hours that feel earned** | Every skill climbs ten tiers. Reaching a tier is an accomplishment, with time to explore it; anything can be done AFK at a base rate, and attention pays more through a relaxed act that works with the place. | Twitch minigames for skilling; stamina meters; luck as the gate to a resource; power-training the lowest tier. |

**Where they come from:**
1. `docs/systems.md`, "Design principles" ("every combat resource needs its own solo skill
   pipeline"); `docs/skills.md` 1 ("Does every fight have a solo supply?"); `docs/areas/rules.md`
   I-NO-DEAD-ENDS.
2. `docs/lore-rework/universe.md` ("its creatures barely know"; the three layers; the game never
   removes places); `docs/quests/the-bell.md` ("it ends with more questions than it started with").
3. `docs/world-plan.md` section 2 (land first; every settlement sited for a reason a player can
   see); `docs/areas/decisions.md` (2026-10-22: no level bands, reasons to visit across the stages);
   `docs/skills.md` 2.2 (resources from the biome; no region makes an old one obsolete); the owner's
   rule that the world is hand-made and generation is only scaffolding.
4. `docs/systems.md` ("the twist is the world and the humour-based resources"); `docs/skills.md`
   3.3-3.5 (each style's engine).
5. `docs/skills.md` 2.1 (the owner, 2026-11-03: each tier an accomplishment, time to explore it,
   something exciting in every bracket) and 2.3 ("Activity": AFK base rate, a relaxed active layer,
   "not stamina, not spots to click, not rhythm"); 2.2 ("resources aren't rare; drops are").

## Non-goals

Things R'th deliberately doesn't do. Each is already decided.

| Non-goal | Why | Source |
| --- | --- | --- |
| **Player against player** | Friends play together; there is nothing to protect against each other. So no kept items on death either. | `CLAUDE.md` M2; `docs/lore-rework/death.md` |
| **Anti-cheat, anti-bot, sharding** | Trusted friends; the server is authoritative for consistency, not policing. | `CLAUDE.md` |
| **A big player economy** | At most about 200 players: every fight has a solo supply instead. | `docs/systems.md` |
| **Level-banded zones** | A sandbox: areas stay relevant through reasons to visit at every stage. | `docs/areas/decisions.md` |
| **A forced faction** | The player is unaligned and can work with every power. | `docs/lore.md` VI |
| **Selling the game** | Private and non-commercial. | `CLAUDE.md` |
| **RuneScape's content** | RuneScape sets the style and the feel, never names, items, quests, creatures, art or text. | `CLAUDE.md` |
| **A generated world** | Generation is scaffolding; every area is finished by hand. | the owner (memory: hand-crafted world) |
| **Many skills for their own sake** | Sixteen skills, not 29: each must feed something in a fight. Farming, Construction, Agility, Thieving, Summoning and the rest are out of v1. | `docs/skills.md` 1 |
| **Run energy** | Running is free now that Breath is gone. | `docs/skills.md` 2.8 |
| **An opening cutscene** | The player simply wakes in the dead-house; the walk up and out is the opening. | `docs/quests/the-bell.md` |

## Open questions

1. Which of the five pillars stand, and in what words?
2. Is "Your own supply" a pillar or a rule under one? It decides the most content, which argues for a pillar.
