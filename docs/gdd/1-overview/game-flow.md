# Game flow summary

*Draft (2026-11-03). How a player moves through a session and through the game (Baldwin's game
flow summary). The detail of each loop is in [II, play flow](../2-gameplay/play-flow.md); the
journey through the world is the [beat chart](../2-gameplay/beat-chart.md).*

## Through the game

```mermaid
flowchart LR
  A[Wake in the dead-house] --> B[Tolling: The Bell, first skills, first fights]
  B --> C[Walk out: the march, the lake road, the Sere road]
  C --> D[Cruor: the capital, the empire's quests]
  D --> E[The powers: the spine, Sere, Gall, Brack, the north]
  E --> F[Late: top tiers at the edges, bosses, the threads' ends]
  B -. comes back .-> B
  D -. every road starts here .-> E
```

- **Early** (tiers 1 to 3, novice and intermediate quests): Tolling and the heartland round it.
- **Mid** (tiers 4 to 6): the powers' lands; each holds the best of one or two skills.
- **Late** (tiers 7 to 10): the dangerous cores of every land, the deep, the moonfalls, the Dreaming.

Stages are for checking spread, never gates: a player can walk anywhere, and an area keeps reasons
to come back at every stage.

## Through a session

```mermaid
flowchart LR
  P[Plan: a quest step, a skill target, a boss] --> G[Go out: walk or travel]
  G --> W[Work: gather, fight, butcher]
  W --> R[Return: bank, process at stations]
  R --> U[Use: gear up, brew, cook]
  U --> P
```

A session can be one loop or many. Death breaks it: the ferryman holds what you carried until you
pay or walk back for it ([death](../2-gameplay/death.md)).
