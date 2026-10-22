# Phasing the rework into the game

How the old lore leaves the code and content, and the new comes in. Measured on 2026-10-22.

## What the old lore touches

| Old element | Where it is | Kind of change |
| --- | --- | --- |
| **Tithe** (hymns are "Tithe's prayers") | `skills.json` (Religion description), `dialogues.json` (Clerk Ambrose's covenant line), comments in `HymnDef.cs` and `ReligionSystem.cs`, the client's `Hymns.cs` and `ReligionPanel.cs` | Text only |
| **The Sanguine Synod** | Strongbox, bank notes, coins, the watchman's sword, waystone and waygate examines, Clerk Ambrose, achievements, the `SynodRed` material, scene builders | Text only, if the name stays (question 1) |
| **The covenant, the Keeping, the bay** | Clerk Ambrose's dialogue; the Unquiet Saint spec ("martyred for the Keeping", "on its way to the bay") | Text only |
| **The Scalding and Scalth** | Corvin's and the skinning yard's dialogue (Sour Hollow's cask "after the Scalding"), lye vat, glass hook and Scalth salt examines | Text only (question 3) |
| **The Assayer** | `DeathSystem` (remarks, balanced respawn), `assayer.json`, `TextKind.Assayer` in the protocol, the client's `AssayerPanel`, the death count in saves, tests | **A system**: replaced by the ferryman (death v1) |
| **The Veilwood** (Dreaming gathers from its orbs) | `skills.json` (Dreaming's description) | Text only |
| **The Rationalists** | `skills.json` (Ranged, Artifice) | None if the name stays (question 2) |
| **Sere**, the saltfig | Dreamless quest, items | None: Sere stays |
| **Cruor**, verses, pledges, monuments, Brother Hollis, the Unquiet Saint | Abilities, objects, NPCs, bosses, scenes | None: they fit p-sworn's church |
| **Gall Spark** (an occult ability) | `abilities.json` | None: House Gall stays |
| **Tolling, Palmerway, Wennow, Widdermill, Corvin, Dreamless** | Scenes and content | None: they fit the new map (Tolling on the empire's western edge, Palmerway east towards Cruor) |

**No item, NPC or object ids change**, so saves need no migration: only names, examines and
dialogue text. If an id ever has to change, it goes through the existing retired-id path.

## Phases

| Phase | What | Size |
| --- | --- | --- |
| **1. Canon** | Rewrite `docs/lore.md` and `docs/world-map.md` from the rework (`universe.md`, `gods.md`, `death.md`, `powers.md`, the concept map). The old versions stay in git history. Mark `docs/areas/skeleton.*` as superseded. Update the lore notes in `skills.md`, `systems.md`, `CLAUDE.md` and the Unquiet Saint spec. | Docs only. Done first so no session builds from the old lore. |
| **2. Text pass** | Change every old-lore string in content and code (the table above). Run the tests. | One commit, low risk. |
| **3. Death v1** | Replace the Assayer: a small dark realm-of-the-dead region with a bank and the ferryman; gear held for 24 hours; coin fee by value; obols paid automatically on death; nothing kept. The long way back can follow as its own step. Protocol bump, tests (`DeathTests`). | A milestone-sized feature. |
| **4. The map** | Redo the area plan's pass 1 on the concept map: `skeleton.json` from `powers.md`, then the checks and render. | Planning, like the old pass 1. |
| **5. New content** | Gods, the Faceless, the demigods, p-head and the rest arrive with the areas and quests that use them. | Ongoing. |

## Decided

- **The church gets a new name** (not "the Sanguine Synod"). Until it has one, player-facing text
  keeps the old name rather than showing a placeholder.
- **The surface people of the spine get a new name** (not "Rationalists"), same rule.
- **Scalth isn't kept for its own sake.** The concept map decides: the mites, the lye vat and the
  salt get an origin that fits it (likely the lesser houses' broken land), named with it.
- **Order** (with the workflow rework in it):
  1. **Canon and text pass** (cloud): docs, plus every old-lore string in content and code that
     doesn't need a scene. Independent of the workflow rework.
  2. **The workflow rework** (local, with the Unity MCP): its prompt is updated to point at the
     new map and to carry the scene-side text (signs, the `SynodRed` material, builders).
  3. **Death v1**: its realm-of-the-dead region is the first region built the new way.
  4. **The map and regions**, built the new way.
