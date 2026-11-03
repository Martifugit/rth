# R'th: Game Design Document

*A private multiplayer RPG for an invited group of friends. Plays like RuneScape 3, looks like Old
School RuneScape, set in its own world built around the four humours.*

| | |
| --- | --- |
| Version | 0.2, first draft of every section (2026-11-03): every page a draft until the owner approves it |
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
- [Concept](1-overview/concept.md) · [Pillars and non-goals](1-overview/pillars.md) (for the owner) · [Features](1-overview/features.md) · [Genre and audience](1-overview/genre-and-audience.md) · [Game flow](1-overview/game-flow.md) · [Look and feel](1-overview/look-and-feel.md) · [Scope](1-overview/scope.md)

**[II. Gameplay and mechanics](2-gameplay/index.md)**
- Gameplay: [progression](2-gameplay/progression.md) · [beat chart](2-gameplay/beat-chart.md) · [challenges](2-gameplay/challenges.md) · [play flow](2-gameplay/play-flow.md)
- Mechanics: [movement](2-gameplay/movement.md) · [actions](2-gameplay/actions.md) · [combat](2-gameplay/combat.md) · [the humours](2-gameplay/humours.md) · [Religion](2-gameplay/religion.md) · [skills](2-gameplay/skills/index.md) · [items](2-gameplay/items/index.md) · [economy](2-gameplay/economy.md) · [death](2-gameplay/death.md)
- [Saving](2-gameplay/saving.md) · [options](2-gameplay/options.md)

**[III. Story, setting and characters](3-story/index.md)**
- Story: [back story](3-story/back-story.md) · [threads](3-story/threads.md) · [how it unfolds](3-story/unfolding.md)
- Setting: [the universe](3-story/universe.md) · [the powers](3-story/powers/index.md) · [peoples](3-story/peoples.md) · [names](3-story/names.md)
- Game world: [the world](3-story/world.md) · [realms](3-story/realms/index.md) · [every area in brief](3-story/areas.md)
- Characters: [people](3-story/people/index.md) · [creatures](3-story/creatures/index.md)

**[IV. Areas, dungeons and quests](4-content/index.md)** (the game's levels)
- [Tolling and Wennow](4-content/areas/tolling/index.md) · [The Bell](4-content/quests/the-bell.md) · [Dreamless](4-content/quests/dreamless.md) · [the long way back](4-content/activities/the-long-way-back.md) · [the Unquiet Saint](4-content/activities/the-unquiet-saint.md)

**[V. Interface](5-interface/index.md)**: [windows](5-interface/windows.md) · [controls](5-interface/controls.md) · [camera](5-interface/camera.md) · [help](5-interface/help.md)

**[VI. Behaviour](6-behaviour/index.md)**: [creatures](6-behaviour/creatures.md) · [people](6-behaviour/people.md)

**[VII. Technical](7-technical.md)** · **[VIII. Art](8-art.md)** · **[IX. Tools](9-tools.md)** · **[X. Management](10-management.md)**: short pages pointing to the bibles and the plan; X also maps every old doc to the page that replaces it.

**Appendices:** [asset lists](appendices/assets.md) · [glossary](appendices/glossary.md)

## Where this structure comes from

The order and contents follow Mark Baldwin's *Game Design Document Outline*, the most complete
published GDD template, cut and extended for an online RPG as Baldwin himself advises ("no generic
outline fits every genre"). The writing rules come from Tim Ryan's *The Anatomy of a Design
Document* (Gamasutra, 1999). The one-sheet and the beat chart are Scott Rogers' (*Level Up!*); the
pillars follow how Jagex states pillars for new Old School content; one-page diagrams are Stone
Librande's; setting before story before systems, with every mechanic's goals stated, is Tim Cain's.
Sources are listed in [writing.md](writing.md).
