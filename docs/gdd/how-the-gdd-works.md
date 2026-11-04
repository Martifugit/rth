# How the GDD Works

Everything in the game gets a page. Every page has a type. Pages gain components. Pages link to each
other. That's the whole system.

Start with [The Charter](charter.md): the ten rules this whole system rests on.

## 1. Pages

One page describes one thing you can name: Goblin, Oak tree, Woodcutting, Mossy Hollow, The Missing
Cart.

- If a title needs an "and", it's two pages.
- A page can start as two sentences: what it is, and why it exists. That's a complete page until
  there's a reason to say more.
- A link to a page that doesn't exist yet is fine. It's a to-do.

## 2. The two rules of the system

Types say what a thing is. Components say what it does.

Both stay generic. A type or component never names a specific skill, place or piece of content. The
specifics go in its fields.

- Not gathering, smithing, thieving: one action component, with skill: Woodcutting as a field.
- Not shop, general store, exchange: one trade component.
- Not teleport, ship, fairy ring: one travel component.

This keeps the list short and stable. A new skill, a new kind of shop or a new way to travel never
needs a new component, only new pages that use the existing ones.

## 3. Frontmatter

Every page starts with the same small block:

```yaml
---
type: object
components: [action, states, examine]
groups: [trees]
based-on:
status: draft
---
```

- **type**: exactly one.
- **components**: only the ones that apply.
- **groups**: the hub pages it belongs to.
- **based-on**: the page it's a variant of, if any.
- **status**: stub, draft, agreed or cut.

## 4. Types

A type decides a page's folder and its core headings, the ones every page of that type has. Keep
types few and broad.

| Type | Folder | What it covers | Core headings |
| --- | --- | --- | --- |
| overview | (top) | The game itself | Pitch · Pillars · Core loop |
| system | systems | Rules that apply everywhere: death, banking, trading, the XP curve | Intent · Rules · Interactions |
| skill | skills | A trainable skill | Intent · How it feels to train · What it unlocks |
| entity | entities | Anything that lives or acts on its own: monsters, NPCs, pets | What it is · Intent · Appearance |
| object | objects | Anything placed in the world that doesn't act on its own: trees, rocks, altars, doors, chests | What it is · Intent · Appearance |
| item | items | Anything that can be held in an inventory | What it is · Intent · How you get it |
| ability | abilities | Something the player can cast or activate: spells, prayers, special attacks | What it does · Intent · How you get it |
| effect | effects | A state applied to something: poison, stun, stat boost, freeze | What it does · How long · How it ends |
| quest | quests | A one-time, story-driven task | Summary · Intent · Steps |
| activity | activities | Anything with its own rules that can be done again: minigames, boss encounters, clue hunts, random events | Intent · How it works · How it starts |
| achievement | achievements | A set of goals: diaries, task lists, collection logs | Intent · Tasks |
| location | locations | Any place: region, town, dungeon, room | Intent and mood · Where it is · Connections |
| group | groups | A hub for things that belong together: a species, a faction, an item family, "all trees" | What unites them · Members |
| lore | lore | History, myths, in-world texts | What it is · Why it matters · Where the player learns it |
| media | media | Music tracks, cutscenes | What it is · Mood · Where it plays |
| guide | guides | Direction for art, audio, UI, writing | Intent · Do · Don't · Examples |
| decision | decisions | A choice the team made | Date · Decision · Why |

A group's Members section is generated from every page that lists it under groups. Species and
factions are groups that have also grown lore and relations sections.

## 5. Components

Each component answers one question, has a fixed set of fields, and looks the same on every page
that uses it. A page may hold several instances of one component: a cooking range has one action
per dish.

### Access

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| requirements | What do you need to use or reach this? | Levels, quests, items, membership | Almost anything |
| unlocks | What does this give access to? | Mostly generated from other pages' requirements | skill, quest, achievement, item |

### Doing

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| action | What can the player do with this? | Verb, skill (optional), level, tool, inputs, outputs, XP, time, success chance | object, item, entity |
| trade | What can be bought or sold here? | Stock, prices, currency, restock | entity, object |
| travel | Where can this take you? | Destinations, cost, requirements | object, item, ability, entity |
| storage | What can be kept here? | Capacity, what's allowed, who can access it | object, entity, item |
| dialogue | What does it say? | Personality, voice, key lines, tree link | entity, object, item |

An action with a skill is a skill-action: chopping a tree, smelting ore, picking a pocket, crossing
an obstacle, planting a seed. An action without a skill covers everything else you can do with
something: opening a chest, decanting potions, climbing a ladder.

### Fighting

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| combat | How does it fight? | Stats, attack styles, weaknesses, effects it applies | entity |
| behaviour | How does it move and react? | Aggression, movement, triggers, phases | entity |
| assignable | Can it be given as a task? | Who assigns it, amounts, task requirements | entity, object |

### Items

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| equippable | What happens when you wear or wield it? | Slot, bonuses, effects, requirements | item |
| consumable | What happens when you use it up? | Effect, amount, doses | item |
| charges | Does it wear out or run out? | Charges, how it's recharged, what it becomes | item, object |
| value | What is it worth? | Value, tradeable or not, stackable or not | item |

### Outcomes

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| rewards | What do you get from this? | Fixed rewards and chance-based rewards, with what triggers them (kill, completion, opening) | entity, object, quest, activity, achievement |

A drop table is a rewards component triggered by a kill.

### World

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| spawns | What appears here? | What, how many, where, when, respawn time | location |
| area-rules | What works differently here? | Combat rules, restrictions, hazards, effects applied | location |
| states | How does it change? | States, what moves it between them, how long each lasts | object, entity, location |

states covers anything that changes form: a tree that becomes a stump, a crop that grows, a door
that opens, a town that changes after a quest.

### Structure

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| branching | Can the player choose? | Choices, outcomes | quest, activity |
| repeatable | Can it be done again? | How often, what resets, what changes | activity, quest, achievement |
| timed | Is time a factor? | Limits, schedules | quest, activity |
| players | Who takes part? | Solo, group, versus, sizes | activity, quest |

### Design

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| tunables | Which numbers can be adjusted? | A table of values and their purpose | system, skill, activity |
| progression | How does it grow over the game? | Stages, curves, milestones | system, skill |

### Flavour

| Component | Answers | Fields | Found on |
| --- | --- | --- | --- |
| examine | What does examining it say? | One line | entity, object, item |

## 6. Who writes what

When two pages are connected, the page that owns the relationship describes it. The other only shows
it, ideally generated.

| Question | Described on | Shown elsewhere as |
| --- | --- | --- |
| How do you train a skill? | Each object's or item's action with that skill | The skill's training table |
| What does it drop? | Its rewards | "Dropped by" on the item |
| Where is it found? | The location's spawns | "Found in" |
| What does an NPC do in a quest? | The quest's Steps | "Appears in" |
| What does it sell? | Its trade | "Sold by" on the item |
| What does it require? | Its own requirements | "Unlocks" |
| What applies an effect? | The combat, consumable or ability doing it | "Applied by" on the effect |

A reward table, stock list or spawn list shared by several things gets its own page, and each of
them links to it.

## 7. Lists are generated, not written

Any list that gathers things from other pages, like a skill's training table, a group's members,
"Dropped by" or "Everything that needs Woodcutting 30", should be built from links and frontmatter,
never typed by hand.

Write each fact once. Let the tooling gather it.

## 8. Variants

- A **variant** is the same thing in another form: an elite goblin, a willow tree next to an oak.
  Set based-on, and write only what differs.
- A **separate thing** is something a player would think of as its own: a named goblin with a story.
  It gets its own full page and joins the Goblins group, but inherits nothing it doesn't use.

## 9. Examples

- **Goblin** (entity): combat, behaviour, rewards, assignable, examine. Groups: goblins.
- **Tired Goblin** (entity): dialogue, examine. Groups: goblins. No combat, no rewards.
- **Old Mara** (entity): dialogue, trade. Her quests link to her from their steps.
- **Oak tree** (object): action (chop, Woodcutting 15, axe, oak logs), states (tree, stump),
  examine. Groups: trees.
- **Cooking range** (object): one action per dish, all using Cooking.
- **Bronze sword** (item): equippable, value. Made by an action on the anvil's page.
- **Teleport tablet** (item): consumable, travel, value.
- **Mossy Hollow** (location): spawns, area-rules.
- **Poison** (effect): what it does, how long, how it ends. Applied-by list is generated.

## 10. Growing the system

- **New type or new component?** If the core headings would differ, it's a new type. If it's the
  same kind of thing doing something extra, it's a component.
- Before adding a component, check whether an existing one fits with a new field value. Most new
  ideas turn out to be an action, a rewards table or a state.
- A new component must be generic: named after the question it answers, never after a skill or piece
  of content, and useful on more than one page.
- Fields can grow; names should not change. Adding a field to a component is cheap. Renaming one
  touches every page.

Pick the type. Add only what applies. Keep it generic. Write each fact once. Link everything else.
