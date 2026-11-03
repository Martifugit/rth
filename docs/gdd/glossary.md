---
id: glossary
kind: topic
status: proposed
updated: 2026-11-03
---

# Glossary

The words the GDD uses with one meaning. Add a term when two pages would otherwise use it two ways.

## The world

| Term | Meaning |
| --- | --- |
| **Tile** | The unit of the world: one step. Walking is a tile a tick, running two. |
| **Tick** | 600 ms, the step everything advances by. |
| **Region** | A square of the world built as one scene and one data file (`world/regions/<id>.json`), 96 tiles on a side in the open land. A build unit, not a design unit: design talks in areas and places. |
| **Realm** | A large region of the continent with its own identity and holder: Venaria, Sere, the spine, Brack, Undermost. |
| **Area** | The unit the world is designed and built in: 3 to 8 regions with one identity (`world/areas.json`), e.g. *Tolling and Wennow*. |
| **Place** | A named spot in an area: a settlement, a landmark, a dungeon, a resource ground. A large place holds parts of its own (districts, levels). |
| **Site** | A spot kept in the layout for future content (`sites` in continent.json); also one instance of a service (six bank booths are one bank). |
| **Settlement size** | City (Cruor and the capitals), town, village, hamlet (a farm, an inn, a camp). By size, never by services. |
| **Height job** | What a height does in play: destination, shortcut, nodes, view, gate, threshold, reserve, water. |
| **The fog** | Closes at 85 tiles: the furthest anything can be seen from. |
| **`p-` name** | A placeholder name (`p-sworn`), until the owner names it. Never shown to players as a name. |

## Progress

| Term | Meaning |
| --- | --- |
| **Level** | A skill's level, 1 to 100 (some skills cap lower). |
| **Tier** | A step on a skill's ladder of resources and gear, 1 to 10 (copper, iron, steel...). Each tier has a home in the land. |
| **Bracket** | A span of levels in one skill (1-10, 10-20...), used to check that each holds something worth reaching. |
| **Stage** | Rough account progress, for checking spread, never a gate: **early** (tiers 1-3, the first quests), **mid** (tiers 4-6), **late** (tiers 7-10, the hardest quests). |
| **Reason to visit** | What draws a player to a place: a quest, skilling, a person or service, an activity, travel. Tagged with its stage. |
| **Committed** | Content you can't walk out of once in (a boss instance, a quest's point of no return). Marked, and entered by choice. |

## Combat and the humours

| Term | Meaning |
| --- | --- |
| **Blood** | Life. |
| **Phlegm** | The resource every combat style builds and spends (0-100). |
| **Bile** | A meter from -100 (black) to +100 (yellow) that Occultism pushes either way. |
| **Devotion** | What Religion's hymns drain. |
| **Hymn** | A prayer: stats while it is sung. |
| **Verse** | One of Cruor's seven verses: a Religion ability, paid in blood and scriptures. |

## Status words

`idea`, `proposed`, `agreed`, `built`: see [README.md](README.md). **Canon** is lore that is agreed.
**Sealed** is a truth the game holds but doesn't tell the player yet.
