# X. Management

*Baldwin's schedule, risks and test plan.* The plan of work is `docs/plan.md`; the order areas are
built in is `docs/world-areas.md`.

## Filling in the GDD

The document is written top down, because each section is checked against the ones above it:
1. **I, the overview, and the one-sheet**, drafted from what is already decided; the pillars and
   non-goals agreed with the owner.
2. **II, gameplay and mechanics**, from what is built and decided; then the beat chart.
3. **III, story, setting and characters**, from the lore canon; then **every area's seed**.
4. **IV, paper designs**, one area at a time as each is next to be built, Tolling first.
5. V and VI from what is built.

**Where it stands (2026-11-03):** a first draft of every section, written only from decisions found in
the old docs (proposals are marked as proposals or listed as open questions). Waiting on the owner:
the pillars, then a review section by section. The seeds after wave 1 are mostly "not set".

## Where each page came from, and what to cut

The GDD is the truth. The old docs are raw material: once the owner approves a page, the docs it was
written from are cut down to what isn't design (how it is built, research, history) or deleted. Until
then they stay, because the drafts may still be wrong.

| Old doc | Written into | Once approved |
| --- | --- | --- |
| `docs/lore.md` | III: universe, back story, threads, powers, peoples, people, realms; death | delete; repoint `CLAUDE.md` to the GDD |
| `docs/lore-rework/` (universe, gods, powers, death, untroth, names) | III; II death | keep as the record of how the lore was decided, or delete; its undecided sheets (troth, house-gall, reckoners, athanor, sherds, regions/) stay raw material until decided |
| `docs/world-map.md` | III: names, world | delete |
| `docs/systems.md` | II: humours, combat, options | delete |
| `docs/occultism.md` | II: skills/occultism | delete |
| `docs/skills.md` | II: progression, skills, Religion, items, economy | cut to tuning notes and the build history (section 9), or delete |
| `docs/fishing.md` | II: skills/fishing (in brief) | keep the trophy-angling detail until it is moved in; then cut to the build notes |
| `docs/creatures.md` | III: creatures (the rules and sources) | keep as a brainstorm until each family is chosen |
| `docs/interface.md` | V: windows, controls | cut to how it is built |
| `docs/quests/the-bell.md` | IV: quests/the-bell | delete; repoint `tools/world` and `AreaPlanTests` (they read `docs/quests`) |
| `docs/ideas/The Unquiet Saint...` | IV: activities/the-unquiet-saint (in brief) | keep the detail until it is moved in |
| `docs/world-plan.md` | III: world, realms, area seeds | cut to how the layout is generated and changed (sections 7-10) |
| `docs/world-areas.md`, `world/areas.json` | III: area seeds | keep: they are the build order and the stage checklist |
| `docs/areas/tolling-plan.md`, `tolling.md`, `tolling-bell-tower.md` | IV: areas/tolling | cut to the build record; repoint `world/plans/tolling.json`'s `doc` to the GDD page |
| `docs/areas/heartland-vision.md` | IV: Tolling (as proposals); III seeds | superseded by each area's paper design as it is written |
| `docs/areas/rules.md`, `decisions.md` | II: progression (stages); pillar 3 | keep as the area plan's checks |

## Risks

1. **Drafts trusted too early.** Every page says "Draft" until the owner approves it.
2. **Two truths.** Until old docs are cut, a session may build from them. Each old doc gets a line at
   its top pointing to the GDD.
3. **Seeds left empty.** Ryan writes every seed early so the world is designed as a whole; wave 2 to 5
   seeds should be written as a set, before the next paper design.
