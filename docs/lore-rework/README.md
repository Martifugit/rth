# The lore rework

A rebuild of `docs/lore.md` so that every power that survives it has a strong foundation: where
it came from, what it wants, what it holds and why, and where it is heading. The goal is lore you
can build places and quests from, not lore to read. `docs/lore.md` stays the current canon until
the rework replaces it.

## Method

The same working rules as the area plan (`docs/areas/rules.md`): one subject at a time, a fast
back-and-forth, placeholders for what can't be decided yet, new names as `p-{suggestedName}`.

Work goes top-down, because each layer hangs off the one above it:

| Step | Subject | Output |
| --- | --- | --- |
| 1 | **The universe**: what kind of world R'th is, its great mysteries and what stays sealed | `universe.md` |
| 2 | **Triage**: which existing entities survive into that universe, and which are new | a table, agreed |
| 3 | **The board**: the powers, how much land each holds, where their borders press, who wants what from whom | `powers.md` with a relations matrix |
| 4 | **One sheet per entity**, one at a time, in an agreed order | `entities/<id>.md` |
| 5 | **The timeline**: founding events in order, checked against every sheet | `timeline.md` |
| 6 | **Clocks**: what each power does next if the player does nothing | in each sheet, summarised in `timeline.md` |

Then the area plan's pass 1 is redone on the new map, and `lore.md` and `world-map.md` are
rewritten from the sheets.

## The entity sheet

Every sheet answers the same questions, briefly. A section that can't be answered yet gets a
typed placeholder, not prose.

1. **Origin.** The founding event: who, when, why, and what it cost.
2. **Belief.** What it holds true, in one paragraph. What it calls itself and what others call it.
3. **Ground.** What land or leverage it holds, and *why it can hold it* (the land's reason).
4. **How it lives.** Rule, economy, daily life, what its places look like. Enough to build a
   town from.
5. **Wants.** Its goal for the next generation.
6. **Fears and weakness.** What could end it, and what it won't admit.
7. **Secret.** What players find out about it, and roughly when (early, mid, late).
8. **Clock.** What it is doing now and what happens if nobody stops it.
9. **Relations.** One line to each other power.
10. **In the game.** Skills, combat styles, resources, creatures, bosses, places and quest hooks
    (typed placeholders).

## Constraints from what's already built

The rework may change anything, but these exist in the game and changing them costs work:

- **Tolling** (start town), **Palmerway**, **Widdermill**, **Wennow**, **Sour Hollow**: built
  scenes and content.
- **Corvin Sallow** and the **Dreamless** quest (Edda Marsh, the dreamless draught).
- **Religion** as one skill with **hymns** (sung, devotion) and **Cruor's verses** (blood,
  pledges, monuments). Hymns are attributed to Tithe in a few flavour strings only
  (`skills.json`, `dialogues.json`, the client's `Hymns.cs` and `ReligionPanel.cs`).
- **Cruor's monument** in Palmerway and **Brother Hollis**.
- The combat styles: **Occultism** (bile) and **melee** (Cruor's verses); ranged belongs to the
  Rationalists (phlegm).
- **The Assayer** (death) is in the game.

## Triage (proposed, against the new universe)

What each existing entity becomes in the universe of `universe.md` and `gods.md`. **Keep**: stays,
gets a sheet. **Rework**: the idea survives, changed. **Absorbed**: its job is done by something
new. **Cut**: gone. **New**: didn't exist before.

### Peoples and powers

| Entity | Proposal | Why |
| --- | --- | --- |
| **Cruor's empire** | **Keep, grown.** The largest power, about a third of the land, with salients pressing into every neighbour. Worships p-sworn: the blood tithes feed its god. Source of melee's verses and pledges, and of the Religion skill. | Owner. |
| **The Sanguine Synod** | **Rework**: the empire's church of p-sworn. Name may change. | Its god changed. |
| **Tithe, its see, its doctrine** | **Cut** as a power. Brother Cassius Ember can survive as a Cruorian auditor. Hymns become a practice of p-sworn's church. | Owner. |
| **The occultists** | **Keep, grown and spread**: much land, scattered across the map, **one dominant house**. Their source: *open* (a god for them is a later sheet; the betrayers' Faceless are a separate faith). | Owner. |
| **The occult houses** (Gall, Umber, Mordant, Sallow) | **Rework**: kept as houses, re-founded in the new history; which one dominates is decided on the board. Sallow (Corvin's house) stays fallen. | Corvin and Dreamless are built. |
| **The range: the surface people and the dwarves under a treaty** | **Keep**, along a long range (spine or other shape, decided on the map). The treaty's hidden reason: *open*. | Owner. |
| **Concordia, the Rationalists** | **Rework**: the range's surface people. Their creed (reason over faith) needs grounding in a world where gods are plainly real: perhaps they don't deny gods, they refuse to serve them. Source of ranged. | Gods exist now. |
| **The dwarves** | **Keep**, inside the range. | |
| **Undermost and the older peoples** | **Keep** as the deep. What they guard or flee: *open*. | |
| **Sere** | **Keep**, rethought on the board: a kingdom pressed by the empire's salients. | |
| **The Faceless** | **New**: followers of the betrayers, in every land, hidden. | `gods.md`. |
| **Keepers of the pieces** | **New**: each of p-scattered's demigods has followers and a place. | `gods.md`. |
| **The village on the brow** | **New**: the people living on p-head. | `gods.md`. |
| **Peoples of the frontiers** | **New, placeholders**: whoever lives where the world grows next. | Extendable world. |

### Beings and threads

| Entity | Proposal | Why |
| --- | --- | --- |
| **The Assayer** | **Absorbed** by death v1 (the ferryman). The idea of something weighing the dead can return in the realm of the dead later. | `death.md`. |
| **The Dreamweaver and her archive** | **Absorbed** by the dreamer (a constant) and the Dreaming. | `gods.md`. |
| **The nightmare loose in the archive** | **Keep** as a thread: something loose from the Dreaming. Details reworked with the dreamer. | Strong boss idea (rotating orbs). |
| **The Physician** | **Cut.** | Owner. |
| **The bay, the Cradle and the Quaere** | **Cut.** | Owner. |
| **The cracked moon and moonfalls** | **Keep** as a mystery and an event system. What cracked it: *open*. | Moonfalls are a strong repeatable activity. |
| **The bound thing in the deep** | **Keep** as a mystery. Candidates: a reject that never reached p-spoil, or whatever wounded p-sworn. | Fits the universe. |
| **The Covenant, the Schism, the Scalding** | **Cut** as written; history is rewritten in the timeline step. The Scalding's scar (Scalth) can survive as a place with a new cause. | The history depended on the bay and Tithe. |

### Characters

| Character | Proposal |
| --- | --- |
| **Corvin Sallow** | **Keep** (built). |
| **Sister Odile Vane** | **Keep**: a blood-knight of the empire. |
| **Brother Cassius Ember** | **Rework** into a Cruorian auditor, or cut with Tithe. |
| **Magister Ilse Varrow** | **Keep** with the range's surface people. |
| **Brannoch** | **Keep** with the dwarves. |
| **Queen Saffiya of Sere** | **Keep** with Sere. |
