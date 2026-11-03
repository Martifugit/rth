---
id: gdd-rules
kind: rules
status: proposed
updated: 2026-11-03
---

# Writing and editing the GDD

**Status:** agreed in outline (2026-11-03). These rules are for everyone who touches the GDD: the
owner, a local agent, a cloud agent.

**The GDD is the truth** (the owner, 2026-11-03). What a GDD page says is the game's design. Where
any other doc, an idea note, a brief or an old plan disagrees with it, the GDD wins and the other
doc is out of date. Ideas still sprinkled through the old docs are raw material: they are taken in,
rewritten or dropped as each part of the GDD is written, and the old doc is cut down or deleted once
nothing in it is left to take. A page's status says how settled its design is, not whether it is
true: an `idea` in the GDD is the current idea, and nothing outside the GDD outranks it.

## 1. What belongs here

The GDD holds **what the game is**: what exists in it, where, what it does, how it plays, and why.

| In the GDD | Not in the GDD (production docs) |
| --- | --- |
| A place, what is in it and why | How its ground is painted, its stage of work, its brief's log |
| A creature: what it is, levels, behaviour, drops | Its Blender method, triangle count, rig |
| A quest's steps and what each needs | Which branch it was built on |
| A skill's levels, tiers and where they are | The code of the system that runs it |
| How combat works, as the player meets it | `CombatSystem`'s classes, the protocol |
| The look and mood of an area, in words | Art budgets, palette files, kit scripts |
| What a window shows the player | UI Toolkit, USS, panel classes |

The test: **would this still be true if the game were rebuilt on another engine?** If yes, it is
design and belongs here. If it is about how we make it, it belongs in a production doc, which may
link here for the why.

Research (`docs/research/`) stays outside as evidence. A GDD page that rests on it links to it.

## 2. One fact, one home

Each kind of fact lives on exactly one page. Everywhere else **links to it and doesn't restate it**.
Restated facts drift apart; that is how the old docs came to disagree with each other.

| The fact | Its home |
| --- | --- |
| Where something is, how many, at what level, and why there | the **area** page (or its place page) |
| What a person is like, wants, knows and offers; which quests they are in | the **person** page |
| A person with no name and no part in a quest (a warden, a farmer with an errand) | the area page's cast list; they get a page when they gain a name or a quest part |
| What a creature is, its kinds, levels, behaviour, drops and yields | the **creature** family page |
| What an item is, its tier, where it comes from, what uses it | the **item** family page |
| What a skill unlocks at each level, and which places hold each tier | the **skill** page |
| How a mechanic works | the **system** page |
| How a quest goes, step by step | the **quest** page |
| What is true about the world, and what is sealed | the **setting** page |
| A decision and its date | [decisions.md](decisions.md) (one line), and the page it settled (the decision itself) |
| Exact tuned numbers of something built | the content data (`server/data/content/*.json`); the page names the id and gives the intent ("dies in about 4 hits at level 1"), not the stat block |

**Both ends name each other, briefly.** An area page lists its creatures with count, level, where
and why; the creature page lists the areas it lives in as links, nothing more. A quest page lists
the people it uses as links; the person page lists their quests as links. The checker (section 9)
holds the two ends together.

When you catch yourself writing something that belongs to another page: put it there and link.

## 3. Pages

- **Every page is copied from a template** in [templates/](templates/). Keep its sections in order;
  write "none" with a reason rather than dropping a section, so a reader can tell "nothing here"
  from "not thought about".
- **The header** (YAML front matter) on every page:

  ```yaml
  ---
  id: corvin-sallow          # the page's id: kebab-case, the file name without .md
  kind: person               # area, place, dungeon, realm, person, creature, quest, quest-line,
                             # achievements, items, skill, system, activity, power, thread, topic, index
  status: proposed           # idea | proposed | agreed | built
  areas: [tolling]           # area ids from world/areas.json, where it applies
  data: [corvin]             # ids in server/data/content (or world/regions) it is built as
  updated: 2026-11-03        # the last real change
  ---
  ```

- **At a glance** comes first: a short box of the facts most people want (where, levels, who, what
  for). Then the detail. Then **Set aside** (if any), then **Not decided**.
- **Size.** A page that passes about 400 lines, or a section that grows its own sections, is split.
  A place gets its own page when it has buildings with insides, several people, or a dungeon. A
  dungeon gets its own page when it has more than one room of content; a single room (a cellar, a
  vault) is a row in its area's "Below ground" table, or part of the place page above it.
- **Who gets a person page.** Anyone with **a name or a part in a quest**: they have a voice, wants
  and a story that writers need in one place. A group that acts as one (the fishers) shares a page.
  Everyone else is a line in their area's cast list. The rule is mechanical on purpose, so no one
  has to judge who is important enough.
- **Files and ids.** File name = id, kebab-case, in its section's folder. An area is a folder,
  `world/areas/<area-id>/index.md`, with its places beside it. Area ids are the ones in
  `world/areas.json`. Data ids (`fen_rat`, `hematite_seam`) are written in `code` and never renamed
  in the GDD alone.
- **Indexes.** Every folder has an `index.md`: one line a page, with its status. Add the line in the
  same change as the page.

## 4. How to write

- **Concrete means physical.** Say what stands where and what a player does with it: a building,
  a station, five hematite seams in a quarry face, six adders of level 8 on a gravel slope, a
  trapdoor into a 10 by 10 cellar. "The dry is spreading" says nothing until it names what is built,
  where, and what a player does there. Lore belongs to setting pages, quests and dialogue.
- **Everything has a reason.** For each thing a reader should be able to ask "why is this here?"
  and find the answer: it supports a quest, an activity or a skill's progression; it fits the place
  (its land, history or people); or it ties the place to another. "Towns usually have one" is not a
  reason.
- **No labels for content.** "A frontier town", "a hub", "the tier 3 area" describe nothing. Show it
  as content: what is there, what isn't, and when.
- **Proposals, not decrees.** Little is fixed yet. Write what is proposed, why, and what it rests
  on. Avoid "must", "only", "never", "everything" unless the owner has decided it, and then link the
  decision.
- **Present tense, as the finished game.** "Corvin sells the first potions", not "Corvin will sell".
  The status says whether it is real yet.
- **Plain and short.** Short sentences, everyday words, British spelling. Tables for inventories
  (who, what, where, how many, why); prose for intent and feeling. One idea a bullet.
- **Numbers are first guesses** until built, and say so once at the top of the page ("counts and
  levels are first guesses, tuned when built"). Once built, the data holds them.
- **Dates are absolute** (2026-11-03), never "yesterday" or "last week".
- **Names.** The owner names things. A new place, person or thing is `p-suggestedName` until named
  ([decisions.md](decisions.md) 2026-10-22); a working name is marked *(working name)*. Pages use
  the canon's names and placeholders (p-sworn's church, not the Sanguine Synod); where the built
  game still says an old name, the page says so once, in its **Built** line.
- **Original only.** The OSRS and RS3 references set the style and the feel of play, never the
  content: no RuneScape names, items, quests, creatures or text (`CLAUDE.md`, "References and
  boundaries"). A little overlap of ordinary words is fine.
- **Sealed truths** (what the game knows but never tells the player yet) go in a **Sealed**
  section of a setting or quest page, so writers keep to them and quests don't give them away.

## 5. Goals, pillars and principles

**Every thing states its goal.** A system, skill, activity, quest or place says, near the top, what
it is for in play and which of the game's pillars ([vision/pillars.md](vision/)) it serves. Agree
the goals before arguing about the parts: if two people disagree about a detail, check first
whether they agree on what it is for. Something that serves no pillar, or works against one, says
why it is still worth having, or goes. Pillars and goals are only useful if they rule things out,
so each names an example of what it rules out.

**Systems serve the setting.** A mechanic should look and work like the world it is in: the humours
are combat's resources, the church's faith is paid in blood, death is a ferry. A system page says
how it ties to the setting; one that could sit in any fantasy game says so as a question.

**Pictures where they say more.** Loops, flows, maps and resource diagrams communicate faster than
prose. Keep them as source the repository can regenerate (a Mermaid or text diagram in the page,
or a picture drawn by a tool from the data, such as `tools/world plan render`), never a hand-made
image that silently goes out of date.

The principles below are settled by the owner; repeated here so pages can link one place. The full
reasoning stays on the linked pages.
- **Places aren't tiered.** Content and tiers are spread over the world where the land and people
  give them a reason. A start area can hold an end-game resource ([decisions.md](decisions.md)
  2026-10-22, 2026-11-03).
- **Not every skill everywhere.** A town is not a skilling hub, least of all the spawn. Doing a
  skill should mean going out into the land.
- **Reasons to come are incentives**, early, mid and late: a quest, a person, a resource worth the
  trip, a way onward, an achievement, a discovery. Passing through isn't one.
- **Pace varies by skill.** Each new tier is an accomplishment with time to explore it; something
  exciting in every bracket; the late game denser ([skills/](skills/), "Levels and pace").
- **A page decides only its own subject.** An area page says what its area offers and what it asks
  of its neighbours, as questions on their pages; never "the next area takes over X".
- **Targets describe the world, never one place** ([world/](world/), "Targets").
- **No dead ends, no accidental traps** ([world/](world/), "Invariants").

## 6. Editing

- **`idea` and `proposed`:** anyone may write, change or delete them. Say what changed in the commit.
- **`agreed`:** don't overturn it on your own. If a change is needed, make it as a proposal (a
  **Proposed changes** heading on the page, or the change on a branch) and say so in the report;
  the owner says yes, and a line goes into [decisions.md](decisions.md). Fixing wording, links or
  ids on an agreed page needs no proposal.
- **In a design discussion, edit only after the owner decides.** Talk it through first; write down
  what was decided, not what was floated.
- **`built`:** when the game changes, the page changes **in the same commit**: status, ids, anything
  that no longer holds. A page that says `built` and disagrees with the data is a bug.
- **The owner's answers** move a question out of **Not decided** into the body, with the date.
- **Delete, don't strike through.** Superseded text goes; git has it. Keep a **Set aside** line only
  for an alternative worth not arguing again.
- **One subject at a time.** When a change ripples into another page, either make that change too
  or leave a question in that page's **Not decided**. Don't leave two pages disagreeing.
- **Moving content in** (from the old docs): never paste. Rewrite what still holds to these rules,
  putting each fact on its home page; drop the rest; and record the source in the page's
  **Sources** line until the old doc is gone. When old docs disagree, the newest decision wins; a
  disagreement nobody has decided becomes a question in **Not decided**.
- **Cleaning up after.** Once a GDD page covers an old doc's design, cut the old doc down to what
  is still production (or delete it) in the same change, and point its references at the GDD. No
  list for the owner first: git keeps everything.

## 7. Areas and their plan data

An area page **is** the area's design document (it replaces `docs/areas/<area>-plan.md`). Its plan
data stays `world/plans/<area>.json`, which the tools check, draw and compare with what is built.
The page and the data say the same thing: the page is the reasons and the words, the data the
positions and ids. A change to one is a change to the other, in the same commit. The approval gate
(stage 0, package 0.5 in `world/areas.json`) is unchanged: it now points at the area page.

The stage-by-stage record of building an area (its brief's findings, what was tried on the ground)
is production and stays in `docs/areas/<area>.md`.

## 8. How the GDD meets the data

- The GDD is the **design**; the content data is **what is built so far**. Where they differ, the
  data is behind (or ahead, if someone built without writing it down: then the page is updated).
- A `built` entry names its ids, so a reader can go straight to the data.
- **The Built line.** A page whose subject partly exists (Corvin is in npcs.json but stands in no
  region since Tolling was rebuilt) keeps the status of its design and adds a **Built** row to its
  at-a-glance box: what is in the data, what isn't placed, and where the built text differs (old
  names, old lore).
- Numbers on a built thing are described, not copied ("hits for a tenth of your blood"), except
  where the number *is* the design (a level requirement, a count placed in an area).
- Something in the data with no page is either undocumented (write the page) or trash: cut it from
  the data and say so in the report.

## 9. The checker (proposed)

`tools/gdd check` (to be built with the migration) fails on:
- a page without a valid header, or an id that doesn't match its file;
- a broken link;
- a `data` id that doesn't exist in the content, or a `built` page with none;
- an area id not in `world/areas.json`, or an area in it with no page;
- two ends that don't name each other (an area lists `fen_rat`, the rats' page doesn't list the area);
- a page missing a template section, or missing from its folder's index.

It warns on: `updated` older than the data it names; pages over 400 lines; "must", "never" and
"only" on a page that isn't `agreed`.

`tools/gdd index` rewrites every folder's `index.md` and the cross-reference lists from the headers,
so they can't drift.
