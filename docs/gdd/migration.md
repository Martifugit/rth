---
id: migration
kind: topic
status: proposed
updated: 2026-11-03
---

# Moving the old docs into the GDD

**Status:** proposed (2026-11-03). Where every existing doc goes, what is kept, what is dropped, and
the order of the move. Deleted when the move is done.

Each old doc is one of:
- **Move:** design that holds; rewritten to [rules.md](rules.md) into its new pages, then the old
  doc is deleted (or cut down to its production part).
- **Split:** part design (moves), part production (stays where it is, trimmed, linking to the GDD).
- **Stays:** production or research; not part of the GDD. May gain links into it.
- **Mine, then delete:** superseded, but still holding a few ideas worth keeping as `idea` or
  `proposed` lines on the new pages.
- **Delete:** superseded with nothing left to keep. Deleted as soon as the GDD covers its subject
  (the owner, 2026-11-03: the GDD is the truth, and git keeps the rest).

## The map

### The game's rules

| Doc | What | Destination |
| --- | --- | --- |
| `docs/skills.md` | the v1 skills, rules every skill follows, each skill | **Move.** Section 1-2 → `skills/index.md` (the roster, levels and pace, the tier ladder and its homes, nodes, processing, requirements, shortcuts); sections 3-8 → one `skills/<skill>.md` each (combat styles' skills, Religion's hymns, verses, pledges and monuments partly to `systems/religion.md`); 9 (moving from the prototype) → drop or `docs/plan.md`; 10 → each page's Not decided. |
| `docs/systems.md` | combat resources, fight rules, death | **Move** → `systems/combat.md` (blood, phlegm, bile, devotion, the global cooldown, abilities, stalling, Revolution); its death part → `systems/death.md`. |
| `docs/occultism.md` | Distillation, spellbooks and reagents | **Mine, then delete.** Distillation is retired into Occultism (`CLAUDE.md`, milestone 9); spellbooks and reagents stay as `idea` lines in `skills/occultism.md`. |
| `docs/fishing.md` | fishing end to end, trophy angling | **Split.** Design → `skills/fishing.md`, `systems/angling.md` (the fight, rigging, the log), `items/tackle.md`, `items/trophies.md`, `people/collector.md`; section 9 (how it's built) stays in `CLAUDE.md`'s extension table. |
| `docs/interface.md` | the interface | **Split.** What the player sees (windows, bars, modals, the world map's design) → `systems/interface.md`; look rules and architecture stay in `docs/interface.md`. |
| `docs/lore-rework/death.md` | death v1, the long way back | **Move** → `systems/death.md` (the ferry, the fee, the hold), `activities/the-long-way-back.md`, `world/areas/the-landing/` (the Landing and the Long Way as places). |
| `docs/ideas/The Unquiet Saint Religion Skilling Boss Spec.md` | a boss design | **Move** → `activities/the-unquiet-saint.md` (`idea`). |
| `docs/creatures.md` | a brainstorm of creature families | **Move** → `creatures/index.md`, a menu of families at `idea`; each family gets a page when an area places it. |

### The setting

| Doc | What | Destination |
| --- | --- | --- |
| `docs/lore.md` | the canon | **Move** → `setting/`: `universe.md` (I, the humours, the constants, sealed), `gods.md` and one `power` page per god (II), `death.md` (III, the realm of the dead as the world sees it), `continent.md` (IV: the land and how the powers stand), `threads.md` (V; one `thread` page each once they grow), `the-player.md` (VI), `people.md` (VII: the peoples), `bosses.md` (VIII). All `agreed`. |
| `docs/lore-rework/gods.md`, `powers.md`, `universe.md`, `entities/*.md` | the workshop behind the canon | **Move** what is canon into the setting pages above; what is still proposed (the Athanor, the Reckoners, the Sherds, the Untroth's story, House Gall) → their `power` pages at `proposed`, kept apart from canon by status. |
| `docs/lore-rework/regions/*.md` | regional sheets | **Move** → `world/realms/<realm>.md` (Venaria, Sere, the spine, Brack, Gall's dominion, Undermost). |
| `docs/lore-rework/names.md` | name proposals | **Move** → `setting/names.md`, the one list of names waiting on the owner; picks go into [decisions.md](decisions.md). |
| `docs/lore-rework/README.md`, `migration.md` | the rework's method and the old lore's removal | **Stays** while the B2 text pass is open (`docs/plan.md`), then **delete**. |

### The world

| Doc | What | Destination |
| --- | --- | --- |
| `docs/world-plan.md` | the continent rethought | **Split.** Why the land is as it is, the continent, where places go, the main roads' rhythm → `world/index.md` and `world/continent.md`; painting, the generator and numbers stay as production. |
| `docs/world-map.md` | names and the shape of the land | **Move** → `world/index.md` (the shape, where Tolling sits) and `setting/names.md`. |
| `docs/areas/rules.md`, `targets.json`, `decisions.md` | the world's targets, invariants and variety checks | **Move** → `world/targets.md` (principles, definitions into the glossary, invariants, targets, variety checks); its decisions → [decisions.md](decisions.md). `targets.json` stays as data. |
| `docs/areas/plan-template.md` | the area template | **Replaced** by [templates/area.md](templates/area.md); its rules move to [rules.md](rules.md). The plan data's format stays documented, as production, in `docs/world-areas.md`. Tools that name the old path are updated. |
| `docs/areas/tolling-plan.md` | Tolling's plan | **Move** → `world/areas/tolling/index.md`, the first area page, enriched to the new template (look and feel, services, discoveries, how it changes). |
| `docs/areas/tolling-bell-tower.md` | the tower and the undercroft | **Split** → `world/areas/tolling/bell-tower.md` (place) and `.../dead-house.md` (dungeon); build notes stay. |
| `docs/quests/the-bell.md` | the first quest | **Move** → `quests/the-bell.md`. |
| `docs/areas/tolling.md`, `lake-road.md`, `cruor.md`, `western-march.md`, `selenfall.md` | the briefs and their stage logs | **Split.** The brief's design (what the area is, its places, outwards) → each `world/areas/<area>/index.md`; the log ("What the work found", checklists) stays as the area's production record. |
| `docs/areas/heartland-vision.md` | wave 1 by area, superseded as a form | **Mine, then delete:** its per-area content and heights go into the four wave 1 area pages. |
| `docs/areas/skeleton.md` (and `skeleton.json`, `tools/areas/plan.py`) | pass 1 on the old map | **Delete** (superseded 2026-10-22). |
| `docs/regions/cruor.md`, `cruor-altar.md`, `cruor-architecture.md` | Cruor's layout direction, the altar, the architecture | **Split.** Mood, places, the altar's design → `world/areas/cruor/` (index and place pages); architecture rules stay as production (art). |
| `docs/regions/meridian.md`, `meridian-redesign.md` | Meridian, the redesign brief | **Mine** its districts, content and secrets (7, 8) → `world/areas/meridian/` at `proposed`, flagged as drawn on the old map; the build parts stay until Meridian is rebuilt. |
| `docs/regions/cistern.md` | Cistern | **Mine** → `world/areas/cistern/` at `proposed`, old-map caveat. |
| `docs/regions/tithe.md`, `tithe-assets.md` | Tithe | **Delete** (Tithe is retired in the canon), after one look for anything that moved to another place. |
| `docs/regions/*-assets.md` | generated asset lists | **Stay** (generated, production); `cruor-assets.md` **delete** (superseded 2026-10-23). |
| `docs/world-building.md` | the first world method | **Mine, then delete:** what still holds is already in `docs/world-guide.md`. |
| `docs/layout.md` | the layout's data format | **Stays** (production). |

### Production and research (stay outside the GDD)

`docs/plan.md`, `docs/workflow.md`, `docs/workflows/`, `docs/world-guide.md`, `docs/world-areas.md`,
`docs/world-example.md`, `docs/world-scale.md`, `docs/assets.md`, `docs/harbour-kit.md`,
`docs/areas/tolling-assets.md`, `docs/research/`, `docs/images/`. Each gains a line saying the design
lives in `docs/gdd/`, and any design rules in them (town sizes in `world-scale.md`, the camera, the
look in eight rules in `assets.md`) are linked from the GDD rather than copied.

### The content data

The data (`server/data/content`) is the game as built. Each built thing gets its GDD page:
- **items** (159 ids) → item family pages;
- **npcs** (54 ids) → creature family pages for creatures, person pages or area cast lines for people;
- **quests, achievements, tasks, shops, dialogues** → their quest, achievement, person and area pages;
- **objects** (367 ids) → mostly not pages: listed where they are placed (area pages) and what they
  make (skill pages).

The audit will turn up content with no place in the design: old-map people (the Synod's clerks,
Tithe's auditors, Meridian's almanac clerks, Cistern's water-sellers), test-yard stand-ins, retired
names. Each is either given a page or cut from the data, and the report says which.

## The order

| Phase | Work | Needs the owner |
| --- | --- | --- |
| 0 | Agree the structure, the rules and the templates. **Done** (2026-11-03). | done |
| 1a | **The vision**: overview, pillars and non-goals, audience and scope, loops, progression, economy, look and sound ([vision/](vision/index.md)), drafted from what the docs already say. Everything after is checked against it. | yes: the pillars are the owner's |
| 1b | **Skeleton and checker.** The folders and their indexes; `tools/gdd check` and `index`; `world/plans`, `tools/world plan` and `CLAUDE.md` pointed at the area pages; a line in each production doc pointing here. | no |
| 2 | **The rules of play** (mostly built or agreed, so low risk): skills, systems, and a page for every built item family, creature family, person, quest and achievement, generated from the data, then written up. | no; the report lists what was cut |
| 3 | **The setting and the decisions:** lore.md and the rework into `setting/`; every decision gathered into [decisions.md](decisions.md). | conflicts found between docs |
| 4 | **The world:** `world/index.md`, the targets, the realms; then the areas, wave 1 first (Tolling, then the lake road, Cruor, the western march), then the old cities mined, then every other area in `world/areas.json` as an `idea` page with what the layout already knows. | each area page, as now |
| 5 | **Enrich:** the gaps the move shows (areas with no people, creatures with no home, skills with an empty bracket, quest lines with no quests), as proposals. | yes |
| 6 | **Delete** whatever old docs are left, once nothing in them is uncovered. | no |

Phases 2 and 3 can run side by side (different folders); 4 needs 2 and 3, because area pages link to
the skills, creatures and setting.
