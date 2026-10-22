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

## Triage (superseded: redone after the universe is agreed)

| Entity | Proposal | Why |
| --- | --- | --- |
| **Cruor and the Sanguine Synod** | **Grows**: an empire, a third of the land, with a ragged expansionist border. The sole source of the Religion skill: hymns and verses become two practices of one church. | Owner. One faith is easier for players to grasp than two sees. |
| **Tithe** | **Goes as a power.** Its best ideas move: the debt doctrine becomes a heresy *inside* Cruor (the schism thread survives as an internal split; two pontiffs still possible); the toll on the bay's mouth goes to whoever replaces it. Brother Cassius Ember stays, as a Cruorian auditor. | Owner. |
| **Calcinia** | **Changes hands.** The salt coast and the bay's mouth become the seat of the dominant occult house. | Gives the occultists real land and one dominant house. |
| **The occult houses** | **Grow and spread.** One dominant house (Gall, already "the largest") rules a kingdom; the lesser houses hold Slough's fens, sea caves and hidden cells inside the empire. Sallow stays fallen. | Owner: occult land spread out, quite a lot of it, one house clearly dominant. |
| **Concordia, the dwarves, the Under-Treaty** | **Stay**, along the range, which becomes a long spine with land on both sides instead of a ring. | Owner: keep the range and the treaty. |
| **Undermost and the older peoples** | Stay. | The deep thread depends on them. |
| **Sere** | Stays; gets a sheet like everyone else. Its borders now press against the empire's salients. | |
| **Nivil** | Becomes one of several **frontiers** at the map's edges, each a later expansion. | Extendable world. |
| **New: what lies beyond the range** | A placeholder frontier power or wild land, decided in step 3. | Extendable world. |
| **The Dreamweaver, the Assayer, the Physician, the Quaere, the broken moon, the nightmare** | Stay; they get sheets in step 4 and their truths are fixed in step 2. | They are the world's deep story. |
