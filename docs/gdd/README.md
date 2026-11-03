# R'th: Game Design Document

*A private multiplayer RPG for an invited group of friends. Plays like RuneScape 3, looks like Old
School RuneScape, set in its own world built around the four humours.*

| | |
| --- | --- |
| Version | 0.1, structure only (2026-11-03) |
| Owner | Martin (the game's designer; approves every design) |
| Design history | [design-history.md](design-history.md) |

This is the game's design: **what the player experiences and why**. It is the truth: where any other
doc in the repository disagrees with it, this wins and the other doc is out of date. How the game is
*built* (engine, code, tools, the art pipeline) is in the technical and art bibles it links to, not
here.

## How to use this document

- **Start with the [one-sheet](one-sheet.md).** The whole game on one page. Then read the section
  you need; every section and every page opens with a summary paragraph, so reading the first
  paragraph of each is a quick tour of the game.
- **It is a reference, not a novel.** Look things up through the contents below. Each subject has
  one page, and other pages link to it rather than repeat it.
- **It is a living document.** It changes as the game is designed, built and played. Every change
  that alters the design is dated in the [design history](design-history.md).
- **Before writing in it, read [writing.md](writing.md)** (how pages are written and changed) and copy
  a [template](templates/).

## Contents

**[One-sheet](one-sheet.md)**: the game on one page.

**[I. Game overview](1-overview/index.md)**
- Concept · Pillars and non-goals · Feature set · Genre · Audience · Game flow summary · Look and feel · Scope

**[II. Gameplay and mechanics](2-gameplay/index.md)**
- Gameplay: progression and the beat chart · challenge structure (quests, tasks, achievements) · play flow
- Mechanics: movement · actions and objects · combat · the humours · Religion · skills · items · economy · death
- Saving · options · easter eggs

**[III. Story, setting and characters](3-story/index.md)**
- Story: back story · threads · how the story unfolds
- Setting: the universe · the powers · the peoples
- Game world: look and feel · the continent · realms · every area in brief
- Characters: people · creatures

**[IV. Areas, dungeons and quests](4-content/index.md)** (the game's "levels")
- An area's design: synopsis, objectives, physical description, map, critical path, encounters, walkthrough
- Dungeons · Quests · Activities (bosses, trials)

**[V. Interface](5-interface/index.md)**
- The windows and HUD · camera · controls · audio (music, sound) · help

**[VI. Behaviour](6-behaviour/index.md)**
- Creatures · non-combat characters · pathfinding as the player sees it

**[VII. Technical](7-technical.md)** · **[VIII. Art](8-art.md)** · **[IX. Tools](9-tools.md)** · **[X. Management](10-management.md)**: short pages pointing to the bibles and the plan.

**Appendices:** [asset lists](appendices/assets.md) · [glossary](appendices/glossary.md)

## Where this structure comes from

The order and contents follow Mark Baldwin's *Game Design Document Outline*, the most complete
published GDD template, cut and extended for an online RPG as Baldwin himself advises ("no generic
outline fits every genre"). The writing rules come from Tim Ryan's *The Anatomy of a Design
Document* (Gamasutra, 1999). The one-sheet and the beat chart are Scott Rogers' (*Level Up!*); the
pillars follow how Jagex states pillars for new Old School content; one-page diagrams are Stone
Librande's; setting before story before systems, with every mechanic's goals stated, is Tim Cain's.
Sources are listed in [writing.md](writing.md).
