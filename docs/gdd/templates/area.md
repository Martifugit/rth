---
id: area-id                 # as in world/areas.json
kind: area
status: proposed
areas: [area-id]
regions: [land_0_0]         # its regions, from world/areas.json
plan: world/plans/area-id.json
data: []
updated: 2026-11-03
---

<!-- Copy to docs/gdd/world/areas/<area-id>/index.md. Every section stays; write "none" and why.
     Counts and levels are first guesses until built. Ids in `code` exist in server/data/content;
     anything not made yet is marked *new*. The area decides only itself: what it needs from a
     neighbour is a question on the neighbour's page. Rules: ../../rules.md -->

# <Area name>

## At a glance

| | |
| --- | --- |
| Realm | the realm it belongs to, linked |
| Regions | `land_x_y` (what is in each, a few words) |
| Serves | early / mid / late, and the brackets of each skill it holds |
| Arrive from | the roads and areas players come in by |
| Places | the main places, linked |
| People | the named people, linked |
| Quests | the quests that touch it, linked |
| The one picture | the view a player remembers, from where |

## The map

The plan drawn over the ground (`tools/world plan render <area>`, written to
`world/renders/plan-<area>.png`), regenerated from the plan data whenever it changes.

## 1. What the area is for

- **Goals:** two to four, each naming the pillar it serves ([../../rules.md](../../rules.md) section 5).
- **Its part in the realm:** what this area gives its realm that its other areas don't.
- **Why players come, early, mid and late:** the incentives at each stage (a quest, a person, a
  resource worth the trip, a way onward, an achievement, something to find). What it introduces,
  what brings players back, what draws late players.
- **What makes players leave, and which way:** a table of ways out, what pulls along each, and what
  can be seen of it from here (and from how far; the fog closes at 85 tiles).

## 2. Look and feel

What it is like to stand here, in words, for the art and sound to follow:
- **The land:** its ground, its water, its plants, how open or closed it is.
- **The built:** what its buildings are made of and how old and kept they look.
- **Light and weather:** time of day it reads best at, fog, rain, wind.
- **Sound:** what you hear (bells, water, wind in reeds), and the music's mood.
- **What makes it unlike its neighbours:** the one or two things that tell a player where they are.

## 3. Arriving, and what catches the eye

For the area as a whole, then each place: how a player first arrives (which road, sent by whom),
what they see first and from how far, and **the hook**: the first thing that grabs them and what it
makes them want to do. Specific: "the bell tower over the roofs", not "an air of mystery".

## 4. Places

One block a place. A place with buildings that have insides, several people, or a dungeon gets its
own page (`templates/place.md`) and a short block here that links it.

**<Place name>** (tile or rect; kind: town, village, farm, site, landmark, camp)
- **For:** the stages and what it gives them.
- **Why here:** why this spot, from the land, its history or its people.
- **Found by / hook:** how a player finds it; what catches their eye.
- **Holds:** its buildings, stations, people and objects (detailed below).
- **Leads to:** where it sends players.

## 5. Buildings

| Building | Place | Storeys | Use | Inside, floor by floor | Why |
| --- | --- | --- | --- | --- | --- |

## 6. People

**The cast.** Every person here. Named people with a part to play link to their page; the rest are
described here in a line until they need more.

| Who | Where | Role (shop, bank, tasks, quest, service, teacher, flavour) | What they introduce or lead to | Page |
| --- | --- | --- | --- | --- |

## 7. Creatures

| Creature | Count | Level | Where (place, tiles) | Aggressive | Why here | What players get (XP, carcass, quest kill) |
| --- | --- | --- | --- | --- | --- | --- |

Each links to its family page in `creatures/`; what it is and drops lives there.

## 8. Skills: what content is here

**Every skill in skills.json is a row**, including those with nothing here ("none: no ore in fen
country").

| Skill | What (object, creature or recipe id) | Count | Where | Levels | Why here | Supports |
| --- | --- | --- | --- | --- | --- | --- |

Then, briefly:
- **Fit:** each resource against the land and its home in `skills/` ("where each tier tends to be").
  A resource out of its home says why.
- **Every bracket has something worth reaching:** what is exciting here at each bracket it serves.
- **Spread:** how far the content is from the centre and from each other; doing a skill means going
  out, not standing in town.
- **Pace:** the counts against the pace table, for the players expected here at once.

## 9. Services

| Service | Where | Who or what | Notes |
| --- | --- | --- | --- |

Banks, shops (and what kinds of stock: the stock itself is the shop's data), stations open to all,
altars, task-givers, travel (boats, waystones), places to rest or meet. Absence is fine and counts
toward the world's targets: say which are missing and if that is the point.

## 10. Quests and activities

- **Quests:** each quest that touches the area, which of its steps happen here, and what each step
  needs here (place, person, object). The quest's page holds the rest.
- **Quest lines kept for later:** threads this area's people carry, as `idea` or `proposed`.
- **Activities:** repeatable things (tasks, a boss, a trial, a minigame, a fishing record): what the
  player does, where, and why they come back to it.
- **Content that supports nothing yet:** list it and say why it is still worth placing.

## 11. Discoveries and secrets

Things to find that no one points at: hidden paths, readables (signs, notices, books, graves with
names), rare spawns, odd details that reward looking, lore set into the land. Each with where it is
and what it gives (a line of story, an achievement, a key to something later). Sealed truths they
hint at link to the setting page; they never state them.

## 12. How the area changes

States of the area through play: before and after a quest, during an event, as a thread advances.
For each: what changes (a door opens, a person moves, a creature appears), when, and for whom
(everyone, or only the player who did it).

## 13. Below ground and indoors

Each dungeon, cellar, crypt, cave or interior region: its entrance (object and tile) and how it is
found, its size, its stages, and a link to its page (`templates/dungeon.md`) if it has rooms with
content. Below ground costs the overworld nothing, so big depth goes here.

## 14. Heights

| Height | Job | What it is (a section in words) | Routes up, down, through | What is seen from it |
| --- | --- | --- | --- | --- |

Jobs: destination, shortcut, nodes, view, gate, threshold, reserve, water. A height with no job isn't
in the plan. Busy ground stays level.

## 15. Routes

Every road, path and river: kind, where it runs, where it meets the neighbour, why it goes that way.
Then **walking times** from the centre to the main places (tiles, then seconds: a tile a tick of
0.6 s walking).

## 16. Neighbours

| Neighbour | What crosses (roads, rivers, views, errands, resources) | Level step at the border | Questions for its page |
| --- | --- | --- | --- |

## 17. Flow and walkthroughs

**The flow:** the path most players take through the area the first time (the critical path: the
beats in order, from arrival to leaving), and the branches off it, as a short diagram or list. Each
beat names its place, what happens, and what pulls the player to the next.

**Walkthroughs:** one per stage the area serves, as a player would play it: where they arrive from,
what they see and do in order, where they go next, and why they come back.

## 18. Achievements

The area's set by tier (easy to master), linking its page in `achievements/`: what each tier asks
across the area's places, so the higher tiers bring late players back.

## 19. Kept for later

| What | Rect or object | For (and the mechanism it would need, if that doesn't exist) | Why here |
| --- | --- | --- | --- |

## 20. Assets

What the area needs made, by kit: existing kits that cover it, and each new building type, landmark,
prop and creature model. The asset work itself is tracked in production docs.

## Set aside

Alternatives weighed and turned down, one line each with why.

## Not decided

Numbered questions for the owner.

**Sources:** the old docs this page was moved from, until they are gone.
