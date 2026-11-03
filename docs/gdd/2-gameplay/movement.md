# Movement

*Draft (2026-11-03). From `CLAUDE.md` (world model, regions, floors) and `docs/skills.md` 2.8.*

## Summary

The world is a grid of tiles. You click where you want to go and your character finds the way and
walks there, one tile a tick, or runs, two tiles a tick. Floors above the ground are reached by
stairs and ladders; cellars, crypts, caves and dungeons are their own places, reached by a trapdoor,
a stair or a cave mouth.

## Goals

- Make the world feel the size it is: distances are counted in tiles and seconds, and the land
  between places takes real time to cross ([pillar 3](../1-overview/pillars.md)).
- Keep moving simple and readable, as in RuneScape: click, walk, arrive.

## How it works

- **Walking and running.** Running is free: there is no run energy. Running is a toggle that is
  remembered. Some places don't let you run (the long way back: the cold is in your legs).
- **Pathfinding.** The game finds the shortest way round walls, water and cliffs. Walls block the
  edges of tiles, so a fence or a doorway matters.
- **Floors.** A building can have up to three floors above the ground. People only see people on
  their own floor.
- **Below ground.** Never a floor under the ground: every cellar, crypt and cave is a small place of
  its own, entered by something you climb.
- **Doors** open and shut for everyone, and shut themselves after a while.
- **Shortcuts.** There is no Agility skill; each shortcut asks for the skill that fits it: Mining
  clears rubble, Woodcutting cuts a thicket, Defence forces a jammed gate, Religion opens a
  church-sealed door, Fishing wades a ford, Dreaming walks through a place that remembers being open.
- **One world.** The land is continuous, from Tolling to the frozen north, without loading screens
  between areas.

## What the player sees

A flag where you clicked until you arrive; the minimap turns with the camera and can be clicked to
walk. The region's name shows when you enter it.

## Open questions

1. Fast travel: none is designed yet. The area plan's target is fast-travel points 120 to 200 tiles
   apart along routes, and it wants fewer than half of settlements to have one.
