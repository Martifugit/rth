---
type: activity
components: [timed, players]
groups: []
based-on:
status: draft
---

# Wildfires

## Intent

Wildfires is the fast, active way to train [Woodcutting](../skills/woodcutting.md), beside the calm
chopping of ordinary [trees](../groups/trees.md). A run is a burning forest to cross from start to
end. The player chops, solves and dodges while the fire presses in, and carries a growing haul the
fire can take back. It's for the player who wants to play hard for a while, alone but among others.

## How it works

- **The forest.** Each run is a [burning forest](../groups/burning-forests.md), an authored place
  outside the world, with one for each band of Woodcutting levels. The path runs between fire on
  both sides.
- **Smouldering trees.** Along the path, the player fells
  [smouldering trees](../objects/smouldering-tree.md) to stop the fire spreading. They give
  [smouldering logs](../items/smoldering-log.md), which the player stacks up through the run.
- **Near-crossings.** Where the fire has almost crossed the path, the player holds it back with a
  short movement puzzle.
- **Hazards.** A hazard that catches the player costs [blood](../systems/blood.md) and turns part of
  the stack to ash, which blows away.
- **The last stretch.** The end of the forest is the hardest part: the fire is closest and the
  hazards come fastest.
- **The source.** At the end stands the [p-burning-tree](../objects/p-burning-tree.md) the fire
  spreads from. The player fells it, then hands the stack in at the firebreak beyond.
- **The haul.** Handing in turns the stack into Woodcutting XP. The bigger the stack, the better the
  player's chances at the forest's drops. Logs count only at the end: the player can teleport out at
  any time, and loses the stack.
- **Collapsing.** At 1 blood the run ends. The screen goes dark and the player wakes at the
  [fire brigade](../groups/fire-brigades.md), where the chief had to bring them back to safety. The
  stack is lost. It isn't a death, and [Pennick](../entities/pennick.md) plays no part.

## How it starts

A [p-burning-tree](../objects/p-burning-tree.md) sprouts near a town or village that has a
[fire brigade](../groups/fire-brigades.md), and smoke rises over it. The player talks to the
brigade's chief and is taken to the burning forest for their level.

## Timed

- Limits: None. The fire never runs out on the player. Each run is timed, so players can race their
  own and each other's best times.
- Schedules: The burning tree sprouts near a different town every hour or so. Every level always has
  a fire to travel to.

## Players

- Solo: Yes. Each player's smouldering trees, near-crossings and personal hazards are their own.
- Group: Everyone of a level band runs the same forest and sees each other there. Some environmental
  hazards cycle on their own and are shared.
- Versus: Only through best times.
- Sizes:

## Open questions

- How many level bands are there, and so how many burning forests?
- Is one brigade active for every band at once, or does each band have its own fire somewhere?
- How do the near-crossing puzzles work?
- Which hazards are personal and which are shared, and what are they?
- How much of the stack does a hazard turn to ash?
- What does each forest drop, and how much does a bigger stack improve the chances?
- Where can players see best times?
