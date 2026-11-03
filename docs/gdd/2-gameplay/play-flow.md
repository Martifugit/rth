# Play flow

*Draft (2026-11-03). The loops a session is made of, one diagram each (Librande). The rules are on
each mechanic's and skill's page.*

## The core loop

```mermaid
flowchart LR
  Gather[Gather: ore, logs, fish, herbs] --> Make[Make: bars, gear, food, potions]
  Fight[Fight creatures] --> Carcass[Butcher the carcass]
  Carcass --> Make
  Make --> Fight
  Fight --> Drops[Drops: rare items, remains]
  Drops --> Religion[Remains: reliquaries at monuments]
```

Killing things is how most materials enter the world, and the carcass is where combat and skilling
meet. Everything a fight needs comes back out of the skills.

## A fight's supply

| A fight needs | Made by | From |
| --- | --- | --- |
| Food (blood) | Cooking | Fishing, Butchering, Foraging |
| Potions | Medicine | Foraging, Butchering, Fishing, Mining |
| Reagents (occult spells) | Occultism's rites | Butchering, Mining, Woodcutting, Dreaming |
| Ammunition | Artifice | Smithing, Woodcutting, Butchering |
| Verses (melee's power) | bought with pledges | Religion at monuments |
| Hymns | devotion, restored at altars and by draughts | Religion, Medicine |
| Weapons and armour | Smithing, Artifice, Tailoring | Mining, Woodcutting, Butchering, Foraging |

## The skilling session

```mermaid
flowchart LR
  A[Walk to the node or station] --> B[Work it AFK at the base rate]
  B --> C{Attend to the place?}
  C -- yes --> D[Douse the rock, tend the tree, work the bellows, read the water]
  D --> B
  C -- no --> B
  B --> E[Pack full: bank or process]
  E --> A
```

## The Religion loop

```mermaid
flowchart LR
  M[Empower a monument: the fount cuts you] --> P[Pledges]
  P --> S[Buy scriptures from Cruor's people]
  S --> V[Read verses in fights]
  K[Kill and butcher] --> R[Remains] --> Q[Reliquary: everyone at the monument gains] --> M
```
