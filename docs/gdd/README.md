---
id: gdd
kind: index
status: proposed
updated: 2026-11-03
---

# R'th: the game design document

**Status:** structure proposed (2026-11-03), for the owner's review. Nothing has been moved in yet:
[migration.md](migration.md) maps every existing doc onto this structure, and the move starts once
the structure and [rules.md](rules.md) are agreed.

The GDD describes **the game as a player meets it**: the world and what is in it, the people, the
creatures, the quests, the items, the skills and the rules they play by, and why each thing is the
way it is. One place to look up "what is in Tolling", "who is Corvin Sallow", "where does hematite
come from" or "how does The Bell go", and find one answer.

It is not the plan of work, the pipeline or the engine. How we build the game (tools, art methods,
Unity, the server's code, the stages of an area's work) lives in the production docs outside this
folder (`docs/workflow.md`, `docs/world-guide.md`, `docs/assets.md`, `docs/plan.md`, `CLAUDE.md`).
The boundary is set out in [rules.md](rules.md) section 1.

## How to read it

- Start at the index of the section you need. Every folder has an `index.md` listing its pages, one
  line each, with their status.
- Every page opens with a header (its id, kind, status and what data it is built as), then an
  **at a glance** box, then the detail. Read the box first; most questions end there.
- Every page ends with **Not decided**: the open questions, for the owner.

### Status

Every page, and any entry on it that differs from the page, carries one of:

| Status | Means | Who sets it |
| --- | --- | --- |
| `idea` | A possibility, not yet argued for. A menu to choose from. | anyone |
| `proposed` | Argued for, with reasons, waiting on the owner. | anyone |
| `agreed` | The owner has said yes. Changed only through a proposal ([rules.md](rules.md) section 6). | the owner |
| `built` | In the game: in `server/data/content`, `world/regions` or code, with its ids named. | whoever builds it |

Lore that is `agreed` is canon. Something cut is deleted, not marked (git keeps it); an alternative
that was weighed and turned down can be kept in a page's **Set aside** with one line on why, so it
isn't argued again.

## The sections

| Folder | Holds | One page per |
| --- | --- | --- |
| [vision/](vision/) | What R'th is: the pillars, who it is for, what it plays and looks like, the shape of a player's progress (stages, tiers, pace). | topic |
| [setting/](setting/) | The canon: the universe, the gods and powers, the peoples and factions, history, death as the world understands it, the threads that run through the game. What is true, including what is sealed. | power, people or thread |
| [world/](world/) | The continent: its realms, its areas and the places in them. **Where everything is, how much of it, and why it is there.** | realm; area (a folder); large place or dungeon |
| [people/](people/) | Named people with a part to play: who they are, what they want, what they know, what they offer, where they appear. | person (minor folk stay in their area's cast list) |
| [creatures/](creatures/) | The bestiary: what each creature is, its kinds and levels, how it behaves, what it drops and yields. | family (rats, mites, shades...) |
| [quests/](quests/) | Every quest and quest line: premise, steps, what it needs, what it changes, what it leaves open. | quest; quest line |
| [achievements/](achievements/) | Achievement sets: an area's set by tier, a skill's, the game's. | set |
| [items/](items/) | Items by family: what they are, their tiers, where they come from, what uses them up. | family (bars, pickaxes, draughts, trophies...) |
| [skills/](skills/) | Every skill: what it is for, how it is trained, what each level bracket unlocks and where in the world that is. The rules every skill follows. | skill |
| [systems/](systems/) | The rules of play: combat and its resources, Religion's hymns and verses, death and the ferry, banks, shops and currencies, tasks, travel, the interface as the player sees it. | system |
| [activities/](activities/) | Repeatable things beyond a skill: bosses, trials, minigames, events. | activity |
| [templates/](templates/) | The page templates. Copy one to start a page. | kind |

Beside them:
- [rules.md](rules.md): how the GDD is written and edited. **Read it before changing anything here.**
- [decisions.md](decisions.md): every decision the owner has made, dated, one line each, with a link
  to the page it settled.
- [glossary.md](glossary.md): the words the game and its designers use (tier, stage, area, place,
  site, humour...).
- [migration.md](migration.md): where each existing doc goes. Deleted when the move is done.

## How the pieces fit

Each kind of fact has **one home**, and every other page links to it. The full table is
[rules.md](rules.md) section 2; in short:

```
          setting/  what is true                  vision/  what the game is
               \                                     /
   world/  where it is, how many, why here  ── links ──  people/ creatures/ items/
               |                                         what it is and does
   quests/ achievements/ activities/  what you do   skills/ systems/  the rules
               \___________________________________________/
                                    |
           server/data/content, world/regions, world/plans: the built numbers
```

An area page says *eight fen rats in the reeds at the river's mouth, because...* and links to
[creatures/rats](creatures/). The rats' page says what a fen rat is, its level, what it drops and
yields, and lists the areas it lives in. Neither repeats the other.
