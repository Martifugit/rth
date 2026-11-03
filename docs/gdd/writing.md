# Writing and changing the GDD

How this document is written, so it stays something people read and trust. The rules are taken from
designers who have shipped many games; the sources are at the end.

## The rules

1. **Write from the player's side.** Describe what the player sees and does and why, not how it is
   implemented. Use action words ("fish", "pray", "walk to") rather than interface words ("click",
   "drag"). Code, data files and tools belong to the technical bible. *(Ryan)*
2. **Open every section with a summary paragraph**, so a reader gets the gist without reading on.
   *(Ryan)*
3. **Be specific.** A vague word ("rich", "mysterious", "a hub") is only allowed if details follow
   at once: what is there, how many, where, what the player does with it. *(Ryan)*
4. **Ratings, not stat blocks.** Describe characters, creatures and items on a scale (low, medium,
   high; "dies in about four hits from a new player") until tuning. Exact numbers live in the game's
   data, where they are tuned. *(Ryan)*
5. **Every mechanic states its goals,** and the goals are agreed before its details are argued.
   Mechanics serve the setting: they look and work like the world they are in. *(Cain)*
6. **Everything serves a pillar.** A feature, place or quest names which [pillar](1-overview/pillars.md)
   it serves. One that serves none, or works against one, is cut or argued for. Pillars and non-goals
   are written specifically enough to rule real ideas out. *(Jagex; pillars practice)*
7. **Draw it when a picture says more.** Loops, flows, maps and progressions get a one-page diagram,
   kept as source that can be regenerated (text or Mermaid in the page, or drawn by a tool from
   data), so it never silently goes stale. *(Librande)*
8. **Stay consistent.** One subject, one page; others link to it. When pages disagree, fix it at once,
   or write the question in both pages' "Open questions". Nothing drifts from the pillars without a
   decision. *(Ryan)*
9. **Cut scope.** If a design would take too long for two people to build, cut it down. Write what
   can be made. *(Ryan)*
10. **Short pages that stand alone.** A page that grows past a few screens is split. Every page can be
    read on its own, with links for the rest. *(wiki practice; Librande)*

## Designing a level: seed first, paper design when it's next

Areas, dungeons and quests are designed in two steps *(Ryan)*:
1. **The seed**, for every one, early and short: its goals, its gameplay, its link to the story, what
   it needs, and its difficulty. All the seeds together are the game world in brief
   ([III](3-story/index.md)) and must fit the beat chart ([II](2-gameplay/index.md)).
2. **The paper design**, written only when it is next to be built, to the
   [area template](templates/area.md): the map to scale, objectives, the critical path, encounters,
   the walkthrough. Don't write all paper designs at once; each one built teaches something for the
   next.

Then it is built in broad strokes, played, refined, and the paper design is updated to what was
built.

## How it changes

- **The document records decisions; it doesn't replace discussion.** Talk a design through; write
  down what was decided. *(Ryan)*
- **The owner approves.** A page is a **draft** until the owner approves it, then **approved**. A
  change to an approved design is proposed, and approved again. The page's first line says which, and
  when.
- **Date every design change** in the [design history](design-history.md): one line, what changed
  and why. An undated design ends up trusted after it stops being true.
- **The GDD is the truth.** When it is written, older docs on the same subject are cut down to what
  isn't design, or deleted. Git keeps the history.
- **When the game is built, the page follows.** If what was built differs from the design and the
  owner keeps it, the page changes.

## Names and originals

- The owner names things. Unnamed places and people are `p-suggestedName` until named.
- All content is original. RuneScape sets the style and the feel of play, never the content: no
  RuneScape names, items, quests, creatures or text.

## Sources

- Mark Baldwin, *Game Design Document Outline* (the structure; the area and character contents).
- Tim Ryan, *The Anatomy of a Design Document*, parts 1 and 2 (Gamasutra, 1999; now on
  gamedeveloper.com): writing rules, the functional specification, level design seeds and paper
  designs.
- Scott Rogers, *Level Up! The Guide to Great Video Game Design*: the one-sheet, the beat chart.
- Stone Librande, *One-Page Designs* (GDC 2010) and *The Four One-Page Designs*: vision, pillars,
  loops, resource flow.
- Tim Cain on design documents: setting, story, systems; every mechanic's goals.
- Jagex, *Adding a New Skill: Our Approach* (Old School RuneScape): design pillars for new content.
