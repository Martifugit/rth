# R'th: the plan for what comes next

Written 2026-10-22. A working plan, split into **workstreams** that separate agents (and the owner)
can run at the same time. Each workstream says where it runs, what it depends on, which files it
owns, and when it is done. Update the status column as work lands.

## Where things stand

- **The game:** milestones 1-10 are done (see `CLAUDE.md`): the tick server, regions, items,
  skills, the town, quests, the interface, the skills rework and melee. Two regions exist,
  Tolling and Palmerway, plus the Unquiet Saint's crypt (a Religion skilling boss).
- **The lore has been rebuilt** in `docs/lore-rework/`. Decided so far: an unfinished world revised
  in ages (a thousand-year stillness, now stirring), p-spoil, death through the ferryman, the first
  gods (p-sworn, the betrayers, p-scattered, p-head, the three constants), the powers and a
  concept map. `docs/lore.md` and `docs/world-map.md` are now canon, rewritten from the rework. The
  lore notes in other docs (`skills.md`, `systems.md`, `CLAUDE.md`, the Saint spec) still mention
  Tithe and the Assayer until A1 finishes.
- **The old area plan** (`docs/areas/skeleton.*`) is drawn on the old map and is superseded. Its
  rulebook (`docs/areas/rules.md`, `targets.json`) and research still apply.
- **This plan, the new canon and the rework live on `claude/area-plan`**, not `main` yet. Merge
  it into `main` before starting parallel workstreams, so every agent branches from the new canon
  (step 0 below).
- **Unmerged work:** the world-scale, camera and art-style session reported its changes on
  `claude/world-scale-camera-art`. That branch was not visible on GitHub when this plan was
  written; find out where it is before starting track F (workstream C0).

## Rules for working in parallel

1. **One branch per workstream:** `claude/<workstream-id>-<short-name>`, from `main`. Merge
   `main` into it often. Owner reviews and merges.
2. **Stay inside your files.** Each workstream lists the files it owns. To change someone else's,
   leave a note in the **Handoffs** section at the bottom of this file instead.
3. **Shared files are edited narrowly.** Content JSON (`server/data/content/*.json`) is shared by
   many workstreams: add entries rather than reorder, change only the entries your work is about,
   and merge `main` before you push.
4. **Protocol bumps happen at merge.** If your work changes a message, bump `Protocol.Version` in
   your branch; whoever merges second re-bumps. Say so in the PR.
5. **`CLAUDE.md` is updated once per finished workstream**, in the same PR, in the milestone list.
6. **Tests pass on every push** (`dotnet test server/Rth.Tests`). Game rules get simulation tests;
   tests use `TestContent`, never the real data files.
7. **Cloud agents have no Unity Editor.** They don't hand-edit `.unity`, `.prefab`, `.asset` or
   `.meta` files. Client C# they write is compiled and checked by the local agent before merge.
8. **Lore placeholders never reach players.** `p-` names live in docs. Player-facing text keeps an
   old name until the new one is chosen.
9. **Ask the owner** before changing the architecture, and when a workstream reaches a decision
   listed under **Decisions waiting on the owner**.

## The tracks at a glance

| Track | Workstreams | Runs | Starts |
| --- | --- | --- | --- |
| **A. Lore and world design** | A1 canon, A2 names, A3 sheets, A4 area plan | cloud, owner in the loop | now |
| **L. Continent layout** | L1 rough layout at tile scale, L2 walkable blockout, L3 maps from the layout | cloud, then local | now |
| **B. Text pass** | B1 now, B2 after names | cloud | now / after A2 |
| **C. Workflow** | C0 merge art branch, C1 audit, C2 snapshots, C3 world as data, C4 migrate regions | local (Unity MCP) | now |
| **D. Death v1** | D1 server, D2 region and client, D3 the long way back | cloud, then local | now / after C3 |
| **E. Gameplay** | E1 tools, Mining, Woodcutting; E2 ranged; E3 item-on-object use | cloud (client parts checked locally) | now |
| **F. Art** | F1 scale and style applied, F2 Tolling art pass | local | after C0 |
| **G. Integration** | merges, `CLAUDE.md`, this file | owner | always |

## Order and dependencies

```
Step 0: merge claude/area-plan into main (owner)

Wave 1 (now, in parallel)
  A1 canon ──────────────┬──> B1 text pass (old-lore strings that need no new names)
  A2 names ──────────────┼──> B2 text pass (church, spine people, Scalth's replacement)
  L1 rough continent layout ──> A4 area plan v2 (passes 2-7 on top of it)
  C0 art branch ──> C1 audit ──> C2 snapshots ──> C3 world as data ──> C4 migrate regions
  D1 death (server) ─────────────────────────────────────────────┐
  E1 tools, Mining, Woodcutting    E2 ranged    E3 item-on-object│
                                                                 v
Wave 2                                            D2 death region and client (needs C3, D1)
  A3 sheets, timeline, clocks                     F1 scale and style applied (needs C0)
Wave 3
  L2 walkable blockout of the whole continent (needs L1, C3, F1)
  L3 world map and minimap drawn from the layout (needs L1)
  Regions from A4, built the new way (needs C3, A4)   D3 the long way back   F2 Tolling art pass
```

---

## Track A: lore and world design

### A1. Canon rewrite

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | nothing |
| **Owns** | `docs/lore.md`, `docs/world-map.md`; lore notes in `docs/skills.md`, `docs/systems.md`, `docs/interface.md`, `CLAUDE.md`, `README.md`, `docs/ideas/*Unquiet Saint*` |
| **Status** | **half done.** `lore.md` and `world-map.md` are canon (2026-10-22). The rest remains. |

**Done:**
- `docs/lore.md` rewritten from `docs/lore-rework/`.
- `docs/world-map.md` rewritten around the concept map, with every name sorted: kept, to be
  renamed, under review, retired, placeholders.

**Remaining** (old-lore mentions counted on 2026-10-22):
- `docs/skills.md` (about 64): Religion's hymns "of Tithe" (4.1), the Dreaming in the Veilwood, the
  Rationalists, the Synod. Fix the *lore* wording only. The skill tiers placed in old regions
  (the Veilwood, Candora, Slough...) are not lore fixes: they move in A4's resources pass.
- `docs/systems.md` (6): the Assayer as current death design; point at `docs/lore.md` III and the
  coming D1.
- `CLAUDE.md` (6), `README.md` (2), `docs/interface.md` (2): Tithe's hymns, the Assayer, the
  Dreamweaver. Describe what the game does today and note the planned change.
- The Unquiet Saint spec (5): a martyr of p-sworn's church, not "of the Keeping"; no bay.
- Mark `docs/areas/skeleton.md` superseded (one line at the top).
- **Done when** no doc describes Tithe as a power, the bay, the Assayer as the design, the
  Physician or the Dreamweaver, and the docs link `docs/lore.md` as canon.

### A2. Names

| | |
| --- | --- |
| **Runs** | cloud proposes, owner decides |
| **Depends on** | nothing |
| **Owns** | `docs/lore-rework/names.md` (new) |
| **Status** | not started |

- **Input:** `docs/world-map.md`'s lists (to be renamed, under review, placeholders in use).
- Propose three or four names each for: p-sworn's church; the spine's surface people; the
  realm of the dead; the ferryman; the walker; p-spoil; the gods (p-sworn, the betrayers,
  p-scattered, p-head, the three constants), so they sound like one family; the lesser occult
  houses' land; where Scalth's mites and salts now come from; the concept map's p- places.
- Follow `docs/world-map.md`'s naming style: a proper name with a meaning under it, short, easy to
  say aloud.
- **Done when** the owner has picked names and they are recorded. Then B2 and A1 apply them.

### A3. Entity sheets, timeline and clocks

| | |
| --- | --- |
| **Runs** | cloud, one subject at a time with the owner |
| **Depends on** | A2 helps but isn't required |
| **Owns** | `docs/lore-rework/entities/*.md`, `docs/lore-rework/timeline.md` |
| **Status** | not started |

- **Who the player is** and why they matter: the old premise (the one sleeper who woke) is under
  review (`docs/lore.md` VI). Decide it early: it shapes the first quests.
- One sheet per power, using the entity sheet in `docs/lore-rework/README.md`: the empire, House
  Gall, the lesser houses, the spine's surface people, the dwarves, Sere, the Faceless, the
  demigods' keepers, the village on p-head's brow, the frontier peoples.
- Still-open gods: the occultists' source; the range's secret (the treaty's hidden reason);
  what wounded p-sworn; the threat that makes p-scattered recombine.
- The timeline: founding events in order, checked against every sheet. Clocks: what each power
  does next if the player does nothing.
- **Done when** every power has a sheet the owner has approved, and the timeline is consistent.

### A4. Area plan v2

| | |
| --- | --- |
| **Runs** | cloud, owner reviews each pass |
| **Depends on** | L1 (pass 1 is read off the layout); names help |
| **Owns** | `docs/areas/*`, `tools/areas/*` |
| **Status** | not started |

- Pass 1 (the skeleton) is read off L1's layout: `skeleton.json` from the layout, with
  `tools/areas/plan.py`'s checks and render. Tolling stays at tile (0, 0) on the
  empire's western edge; Palmerway runs east towards Cruor.
- Then passes 2-7 as in `docs/areas/rules.md` (reasons to visit, resources, infrastructure,
  activities, quest hooks, build order), one at a time, with the owner's review after each.
- **Done when** pass 7 gives a build order of regions that track C can build.

---

## Track L: continent layout

The concept map (`docs/lore-rework/concept-map.png`) gives the shape. This track turns it into a
rough layout **at real tile scale**, then into land you can walk. The earlier "lay out the whole
continent" prompt was written for the old map; reuse its research numbers (scale, terrain,
readability), not its geography.

### L1. Rough layout at tile scale

| | |
| --- | --- |
| **Runs** | cloud, owner reviews |
| **Depends on** | the concept map and `docs/lore.md` (done) |
| **Owns** | `world/layout/*` (new), `tools/layout/*` (new), `docs/layout.md` (new) |
| **Status** | not started |

- **One layout, as data**, in absolute tiles with **Tolling's south-west corner at (0, 0)**, so the
  built regions sit inside it unchanged: Tolling on the empire's western edge, Palmerway running
  east towards Cruor.
- What it holds, roughly:
  - the coast, the sea and the islands; the frontier edges;
  - the spine as a band with its passes (where it can be crossed), running north into the cold;
  - rivers and lakes (Sere's river, the great river, the lake), and which way they flow;
  - biomes or ground kinds (farmland, forest, fen, heath, desert, volcanic, snow);
  - the powers' borders, with the empire's four salients;
  - settlements and landmarks as points, with a size tier (city, town, village, hamlet): Cruor,
    Cistern, Meridian, Frostpeak Forge, Gall's seat, the houses' places, p-head, the demigods'
    sites, Selenfall;
  - the main roads between them;
  - the **96-tile region grid** laid over it, with each region's realm and rough role.
- **Scale:** follow the research (`docs/research/world-density.md`, the art branch's scale doc if
  merged). The first build's core is about 1.2-1.5 million tiles, with frontiers beyond it.
  Distances between capitals in the range RuneScape uses (`docs/areas/scale-compare.png`).
- **Checks** (a script, like `tools/areas/plan.py`): everything reachable from Tolling over land or
  passes; travel times between capitals; region count; land per power against
  `docs/lore-rework/powers.md` (a guide); settlement ladder against `docs/areas/targets.json`.
- **A render** of the layout with the region grid and a scale bar, plus `docs/layout.md` explaining
  the data and the choices.
- **Done when** the owner approves the layout. A4's pass 1 (the skeleton) is then read off it,
  and A4 continues with passes 2-7.

### L2. Walkable blockout

| | |
| --- | --- |
| **Runs** | cloud (generator, data), local (sync and check in Unity) |
| **Depends on** | L1, C3 (the world as data), F1 (scale and style rules) |
| **Status** | waiting |

- Generate every region of the core from the layout as sparse but real land: ground, water,
  roads, the spine's rock, a placeholder at each landmark. Tolling, Palmerway and the Saint's crypt
  keep their hand-built content.
- **Ground height needs an owner decision first**: the collision map and the ground are flat
  today. Gentle terrain (as the research describes) touches the exporter, the server map and the
  client camera.
- **Done when** the owner can walk from Tolling to Cruor, over a pass of the spine, and into
  every power's land.

### L3. Maps from the layout

| | |
| --- | --- |
| **Runs** | cloud (data), local (client checks) |
| **Depends on** | L1 |

- The world map modal and the minimap draw from the layout data, so the map in the game always
  matches the world.

---

## Track B: text pass

### B1. Old-lore strings that need no new names

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | A1 (for wording), can start in parallel |
| **Owns** | the strings listed in `docs/lore-rework/migration.md` |
| **Status** | not started |

- Replace: "Tithe's prayers" (Religion's description in `skills.json`, `HymnDef.cs`,
  `ReligionSystem.cs` comments, the client's `Hymns.cs` and `ReligionPanel.cs`); Clerk Ambrose's
  covenant and bay line (`dialogues.json`); the Scalding in Corvin's and the skinning yard's
  lines; the Veilwood in Dreaming's description.
- **No ids change.** Names, examines and dialogue only, so saves need nothing.
- **Done when** tests pass and a search for Tithe, covenant, Keeping, Scalding, Veilwood and the
  bay finds nothing player-facing.

### B2. Strings that wait for names

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | A2 |
| **Status** | waiting |

- The Synod (strongbox, coins, bank notes, the watchman's sword, waystone and waygate, Clerk
  Ambrose, achievements), the Rationalists (Ranged, Artifice), Scalth's mites, lye vat, glass
  hook and salt. Scene-side text (signs, the `SynodRed` material, builders) goes to C4.

---

## Track C: workflow (local, with the Unity MCP)

The prompt for this track was written earlier ("reshape R'th so a local Unity agent and cloud
agents can build the world together"). Update it before use: the continent layout now comes from
`docs/lore-rework/concept-map.png` and track A4, not from a continent-layout branch.

| Id | What | Depends on | Done when |
| --- | --- | --- | --- |
| **C0** | Find and merge the world-scale, camera and art-style branch (`claude/world-scale-camera-art`), checking it in the Editor. | nothing | merged to `main`, Tolling plays as before with the new camera. |
| **C1** | Audit: MCP tools, how content flows (builders, `WorldKit`, map markers, exporter), repository hygiene, open branches. Write `docs/workflow-audit.md`. | C0 | the owner has read the audit. |
| **C2** | Snapshots: `world/snapshots/<region>.json` and `world/screens/<region>/` so cloud agents can see regions without Unity. | C1 | every region has a fresh snapshot and screenshots. |
| **C3** | World as data: object definitions and region placements as text data, one **Sync World** step that builds scenes and exports maps, and `docs/workflow.md` (who edits what). | C2 | a cloud agent can add an object to a region in data, and one local sync puts it in the game. |
| **C4** | Migrate Tolling, Palmerway and the Saint's crypt to the new path, and carry the scene-side text from B2. | C3, B2 for names | the three play exactly as before, built from data. |

---

## Track D: death v1

Design: `docs/lore-rework/death.md`.

### D1. Server and shared

| | |
| --- | --- |
| **Runs** | cloud |
| **Depends on** | nothing |
| **Owns** | `DeathSystem.cs`, a new ferryman system or component, `assayer.json` (replaced), `shared/Net/Messages.cs` (the death text kind), death-related tests |
| **Status** | not started |

- On death, everything the player carried and wore goes to the ferryman's hold; nothing is kept.
- The player wakes in the realm of the dead (a spawn named in data; a placeholder tile until D2
  builds the region).
- The ferryman: a coin fee from the value of what was held (rate and free threshold in data); an
  obol carried at death is taken automatically as payment (never a menu choice); unpaid gear is
  held about 24 hours of real time, then gone. Saved with the player.
- Paying returns the gear and sends the player back to their respawn point; not paying sends them
  back without it.
- Replace the Assayer's remarks with the ferryman's lines (data), keeping the death count.
- Tests: `DeathTests` for fees, obols, expiry, save and load, and that an obol in the bank does
  nothing.
- **Done when** tests pass and the old Assayer path is gone from the server.

### D2. Region and client

| | |
| --- | --- |
| **Runs** | local (region), cloud writes client code for local checking |
| **Depends on** | C3, D1 |

- A small, very dark region: a landing, a bank and the ferryman. The first region built through
  the new data path.
- The client: replace `AssayerPanel` with the ferryman's conversation and payment, and an
  explanation the first time a player dies.

### D3. The long way back

| | |
| --- | --- |
| **Runs** | design in cloud with the owner, then build |
| **Depends on** | D2 |

- About three minutes, very hard at every level: damage scales with max blood, so progress and
  blood management matter, not stats. Success returns the gear free. The walker waits at the end.
- Design its hazards first (a short spec in `docs/lore-rework/death.md`), then build.

---

## Track E: gameplay (independent of the lore)

| Id | What | Source | Notes |
| --- | --- | --- | --- |
| **E1** | **Tools for gathering, then Mining and Woodcutting** on the existing node system: tool items and tiers, tool requirements on nodes, the first rocks and trees in Tolling and Palmerway. | `docs/skills.md` 2.3, 7 | Mostly data plus a tool check in `GatheringSystem`. Tests like `GatheringTests`. |
| **E2** | **Ranged**, the third combat style: weapons, ammunition, the auto-attack, abilities, power stats. | `docs/skills.md` 3.4 | Follow how melee was added (milestone 10). Client ability book tab checked locally. |
| **E3** | **Using an item on an object or NPC** ("use"), a gap listed since milestone 6. | `CLAUDE.md` milestone 6 | A general mechanism: an interaction plus data hooks; no one-off cases. |

Each owns its own system files and adds content entries; all three touch `items.json` and
`objects.json`, so rule 3 applies.

---

## Track F: art (local)

| Id | What | Depends on |
| --- | --- | --- |
| **F1** | Apply the scale, camera and asset rules from the art branch to the existing regions. | C0 |
| **F2** | An art pass on Tolling: the first region that looks finished. | F1, C4 |

---

## Decisions waiting on the owner

| Decision | Needed by |
| --- | --- |
| Names (church, spine people, realm of the dead, ferryman, walker, p-spoil, gods, places), and which "under review" names come back | A2, B2 |
| Who the player is (the sleeper premise: keep, change or drop) | A3, the first quests |
| The rough layout | L1 |
| Ground height (gentle terrain, or stay flat for now) | L2 |
| The coin fee's rate and free threshold; the obol's price and where it's sold | D1 (defaults in data until then) |
| The long way back's hazards | D3 |
| Each power's sheet | A3 |
| Each area-plan pass | A4 |
| The workflow design in `docs/world-data.md` before C3 builds it | C3 |

## Handoffs

Notes from one workstream to another ("A1 needs B1 to also change X"). Newest first.

| From | To | Note |
| --- | --- | --- |
| A1 | A4 | `docs/skills.md` places skill tiers in old regions (the Veilwood, Candora, Slough...), and `docs/areas/rules.md` lists the old realms in its definitions. Both move with the new map: the tiers in pass 3 (resources), the realm list in pass 1. |
| A1 | B2 | `docs/world-map.md` lists the names to be replaced in the game (the Synod, the Rationalists, Scalth). |
