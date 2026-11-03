# An area's plan: the template

Every area is planned to this template **before any of its ground is changed** (stage 0 in
[../world-areas.md](../world-areas.md)). The plan is two files:
- **The design document,** `docs/areas/<area>-plan.md` (beside the brief, `<area>.md`, which records the stages' work): every question below answered, in plain, concrete
  terms;
- **The plan data,** `world/plans/<area>.json`: the same plan as things with positions, which the
  tools can check, draw and compare with what gets built.

Until the owner approves the plan (package 0.5), `tools/world strokes` refuses to change the area's
ground and no package past stage 0 counts.

**Concrete means physical.** A plan says what stands where and what a player does with it: a
building, a station, 5 copper rocks in a quarry face, 8 creatures of level 12 on a hillside, a
trapdoor into a cellar of 10 by 10. Story belongs to quests and dialogue. A line like "the dry is
spreading" says nothing about the world unless it names what is built, where, and what a player does
there.

**Every thing has a reason.** For everything in the plan, a reviewer should be able to ask "why is
this here?" and find the answer written down. A thing is there because:
- it **supports a quest**, an activity or a skill's progression;
- **it fits the place**: the resource belongs to this ground, the building to this town's trade;
- **it ties the area to another**: a road, an errand, a resource another area needs.

"It would be nice" isn't a reason.

---

## 1. The area in the world

- **Its part in the wider region.** Which region of the continent it belongs to (the heartland, the
  spine's foothills, Gall's coast...), and what this area gives the region that its other areas
  don't.
- **When players come:** the level bands it serves (early, mid, late), and the main reason at each.
- **The one picture:** the view a player remembers.

## 2. How players find it, and what catches the eye

Answer this for the area as a whole, then for each place in it.
- **How a player first arrives:** by which road and from where. Who or what sends them: a quest, an
  errand, a signpost, a view from somewhere else. What they see first, and from how far (the fog
  closes at 85 tiles).
- **The hook:** the first thing that grabs their attention, and what it makes them want to do.
  Be specific: "the bell tower over the roofs" or "a fall pouring out of a cleft, with a dark mouth
  beside it", not "an air of mystery".

## 3. Places

For each settlement, site, landmark or point of interest:

| | |
| --- | --- |
| Where | Tile, or rectangle |
| Kind | town, village, farm, site, landmark, camp |
| Levels | The band it serves |
| Why here | Why this spot: what it gives players, and why it fits the land and its neighbours |
| Found by | How a player first finds it |
| Hook | The first thing that catches their eye |
| Holds | Its buildings, stations, people and objects (each listed below) |

## 4. Buildings

For each building:
- its footprint and storeys;
- what it is used for;
- what is inside, floor by floor: stations, people, readables, a ladder or trapdoor;
- why it earns its place.

Counts follow [../world-scale.md](../world-scale.md) section 4 and, for cities,
`docs/regions/cruor.md`'s budget.

## 5. Skills: what content is here

A table with **every skill in skills.json as a row**, including the ones with nothing here (write
"none", and why: "no ore in fen country"). For each skill with content:

| Skill | What, by object or creature id | Count | Where (place and tiles) | Levels | Why here (the place fits it) | Supports |
| --- | --- | --- | --- | --- | --- | --- |

- **Thematic fit:** check every resource against the land and the region's biome, using
  [../skills.md](../skills.md) 2.2: where each tier "tends to be", and abundant at home, sparse
  elsewhere. A fishing village has fish and nets, not a gem mine. A quarry needs rock showing at the
  surface. A resource out of its biome needs a reason.
- **Pace:** check the counts against the pace table (skills.md 2.1). Is there enough of it, at
  these levels, for the time players are meant to spend here?
- **Supports:** for each piece of content, what it supports: a quest step (`quest:<id>`), a task
  giver, a station nearby that uses it, another area's need.

## 6. Creatures and people

**Creatures:** each group's kind, count, level, spawn area, aggression, and drops or carcass, plus
why it lives here and what it gives players (combat XP, Butchering, a quest's kill).

**People:** each person's role (a shop, a bank, tasks, a quest, a service, a teacher) and where
they stand.

## 7. Quests and activities

- **Every quest that touches the area:** which steps happen here, and the objects, people and
  places each step needs.
- **Every activity:** what the player does, at which objects, for how long, and why they would
  repeat it.
- **Content that supports nothing yet:** list it and say why it is still worth placing.

## 8. Below ground and indoors

For each dungeon, cellar, crypt, cave or interior region:
- **the entrance** (an object and its tile), and how a player finds it;
- **its size;**
- **its levels band;**
- **a floor plan:** the rooms in order, with what is in each (creatures, nodes, objects, the boss)
  and the way through to the exit;
- **what it supports and why it is here.**

Below ground costs the overworld nothing, so it is where big depth goes.

## 9. Heights

For each height, give:
- its job in play, one of:
  - **destination:** it makes somewhere to go;
  - **shortcut:** it saves walking;
  - **nodes:** a face or pit that holds resources;
  - **view:** a top worth climbing to;
  - **gate:** it closes ground off but for one or two routes;
  - **threshold:** a crest that marks leaving one place for the next;
  - **reserve:** ground kept for later;
  - **water:** it holds or shapes water;
- **a section in words:** how high it rises over what, how steep each side is, what is on its faces
  and its top;
- **its routes:** up, down or through;
- **what can be seen from its top** (within 85 tiles), if it has one.

Busy ground stays level (a tile or so across a town). A height with no job isn't in the plan. See
[../world-scale.md](../world-scale.md) section 3.

## 10. Routes

- **Every road, path and river:** its kind, its points, where it meets its neighbour area, and why
  it goes the way it does.
- **Walking times** between the main places: tiles, then seconds (walking 1 tile a tick of 0.6 s,
  running 2).

## 11. Neighbours and the wider region

For **every neighbouring area**, what crosses between the two:
- roads and rivers;
- views across the border;
- errands and quests that send players over;
- resources one area needs from the other;
- the level band on each side, so the step between them feels right.

Then **the wider region:** how this area fits its region's whole, and what it must not duplicate.

## 12. Walkthroughs

One per level band the area serves, as a player would play it:
- where they arrive from, what they see and do, in order, with rough minutes;
- where they go next, and why they come back.

## 13. Kept for later

Space or doors kept for content that isn't built yet. For each:
- the rectangle or object;
- what it is for (the event, the dungeon, the district), and the mechanism it would need if that
  doesn't exist;
- why here.

## 14. Assets

What the area needs made, and which existing kits cover it ([../assets.md](../assets.md)): every new
building type, landmark, prop and creature model.

## 15. Not decided

Every open question, for the owner.

---

## The plan data

`world/plans/<area>.json` is the same plan, as data. Fields are camelCase. Rects are `[x0, y0, x1,
y1)` in tiles; `at` is `[x, y]`. Everything has a `why`.

```json
{
  "area": "tolling",
  "status": "draft",
  "doc": "docs/areas/tolling-plan.md",
  "region": "the heartland: its start, the green river country at the empire's western edge",
  "places": [
    { "id": "town", "name": "Tolling", "kind": "town", "at": [30, 48], "levels": [1, 15],
      "why": "...", "foundBy": "...", "hook": "..." }
  ],
  "buildings": [
    { "id": "smithy", "place": "town", "rect": [12, 44, 18, 48], "storeys": 1, "use": "smithy",
      "contents": ["furnace", "forge", "anvil", "the tinker (tools)"], "why": "..." }
  ],
  "contents": [
    { "id": "quarry-copper", "kind": "node", "object": "copper_seam", "count": 5, "rect": [40, 68, 54, 78],
      "place": "town", "skill": "mining", "levels": [1, 10], "why": "...", "supports": ["quest:the-bell"] }
  ],
  "dungeons": [
    { "id": "dead-house", "name": "The dead-house", "entrance": [26, 56], "object": "stairs_down",
      "size": [10, 8], "levels": [1, 1], "rooms": [{ "name": "the vault", "contents": "..." }], "why": "..." }
  ],
  "routes": [ { "id": "palmerway", "kind": "main", "points": [[40, 48], [190, 48]], "why": "..." } ],
  "heights": [
    { "id": "quarry", "name": "The quarry", "job": "nodes", "rise": -2.5, "outline": [[40, 68], [54, 68], [54, 78], [40, 78]],
      "profile": "...", "routes": ["the cart ramp from Hollow lane"], "sees": [], "why": "..." }
  ],
  "reserved": [ { "id": "inn-plot", "rect": [36, 46, 44, 52], "for": "an inn", "why": "..." } ],
  "neighbours": [ { "area": "western-march", "ties": "..." } ]
}
```

- **Content kinds:** `node`, `creature`, `station`, `npc`, `object`, `view`, `shortcut`. A `node`
  or `creature` names its `skill` and `levels`.
- **Ids:** `object` and `npc` are ids in `server/data/content`. Something not made yet is marked
  `"new": true`.

**The tools:**
- `tools/world plan` lists every area's plan.
- `tools/world plan check <area>` fails until the plan is complete:
  - every place has a `foundBy` and a `hook`;
  - everything has a `why`;
  - every id exists (or is marked new);
  - every `quest:` exists in `quests.json` or `docs/quests`;
  - positions are inside the area;
  - every height has a job, routes and, for a view, what it sees;
  - every neighbouring area has its ties.
- `tools/world plan render <area>` draws the plan over the area's ground to
  `world/renders/plan-<area>.png`.
- `tools/world plan built <area>` compares what is built with the plan, at stages 2 and 3.
- `AreaPlanTests` checks every shipped plan.
