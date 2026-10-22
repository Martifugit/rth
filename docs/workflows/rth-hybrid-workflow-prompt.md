# Task: reshape R'th so a local Unity agent and cloud agents can build the world together

You are Claude Code running **locally** on the owner's machine, in `Martifugit/rth` (C# server,
plain C# `/shared` library, Unity 6 URP client), with a **Unity MCP server** connected to the
open Unity Editor. Read `CLAUDE.md`, `README.md` and `docs/world-map.md` first, and follow
`CLAUDE.md`'s rules:
- the server is authoritative;
- there is no game logic in MonoBehaviours;
- everything is built as extensible systems plus data;
- small commits that build and pass `dotnet test`.

## The goal

The owner wants three kinds of contributors on the same world:

1. **The owner**, working by hand in the Unity Editor.
2. **A local agent (you)**, driving the live Editor through the Unity MCP: placing and editing
   objects, running Editor tools, taking screenshots, importing assets, verifying in play mode.
3. **Cloud agents** (Claude Code on the web, several at once). They have the repository, `dotnet`
   and Python, but **no Unity Editor**. They can't compile Unity scripts, can't import assets,
   and can't see a scene. Writing Unity's own files by hand (`.unity`, `.prefab`, `.asset`,
   `.meta`) is fragile: GUIDs and fileIDs break silently.

Today most region content is made by bespoke Editor build scripts (`client/Assets/Editor/TollingBuilder.cs`,
`PalmerwayBuilder.cs`, `WorldKit.cs`). Rebuilding a scene throws away hand edits, so the project
forces a choice between "an agent can change it" and "a person can touch it".

**Reshape the project so that:**
- **The world's source of truth is text data** that every contributor can edit: region layout,
  ground, placements of objects, and object definitions. Cloud agents edit the data and can check
  their work without Unity.
- **The owner and the local agent can still work directly in Unity**, and what they do there is
  written back to the same data (or kept in clearly separated hand-made content that nothing
  overwrites).
- **One "Sync World" step** run locally turns everyone's data and asset contributions into
  scenes, prefabs, imported assets and exported server maps. It also writes back a readable
  snapshot and screenshots, so cloud agents can see the result.
- **A clear workflow** says who may edit what, how work is handed between cloud and local, and
  how to avoid merge conflicts in Unity files.

Nothing about gameplay changes. Tolling, Palmerway, the Saint's crypt and every player save must
behave exactly as before.

## Phase 0: orient (report before changing anything)

- **List the Unity MCP tools you have** and test them: open a scene, list objects, take a
  screenshot, enter and leave play mode. Note anything they can't do.
- **Audit how content flows today:**
  - the builder scripts and `WorldKit`;
  - `tools/unity-map-tools` (`MapArea`, `MapObject`, `MapNpcSpawn`, `MapWall`, `MapBlocker`, `MapExporter`);
  - how the server loads `server/data/maps`;
  - how prefabs and art are organised;
  - what in scenes is generated and what is hand-placed.
- **Check repository hygiene:**
  - Asset Serialization is Force Text;
  - meta files are visible;
  - `.gitignore` covers `Library/`, `Temp/` and `Logs/`;
  - `.gitattributes` sets merge rules for Unity YAML;
  - whether Git LFS is used, and repository size.
- **Find other open work** (branches and PRs from cloud sessions), especially:
  - a scale, camera and art-style branch (adding `docs/world-scale.md` and `docs/assets.md`);
  - possibly a continent-layout branch (`docs/world-building.md`, a world layout and generator
    in plain C#).

  Your design must fit them; don't duplicate them.

Write the audit to `docs/workflow-audit.md` and summarise it to the owner.

## Phase 1: let cloud agents see the world (the cheapest, highest-value step)

Add Editor tooling, available from the menu and in batch mode, that writes a **readable snapshot**
of every region into the repository:

- **`world/snapshots/<region>.json`**: every object in the region scene, with its name, hierarchy
  path, prefab or definition id, tile position and rotation, fine offset and scale, the
  components that matter (map markers, NPC spawns, objects, walls and blockers, occluders, roof
  hiders, lights), and its world bounds.
  - Sorted and stable, so diffs read well.
  - Small enough to read: summarise meshes; don't dump them.
- **`world/screens/<region>/`**:
  - a top-down orthographic map render of the whole region with the tile grid and coordinates;
  - a few views from the game camera at named viewpoints;
  - all at a fixed resolution.

  Keep the files small (PNG, reasonable size) and regenerate them on every sync.
- **`world/snapshots/index.json`**: the regions, their rectangles, scenes, titles and object
  counts.

Document in `docs/workflow.md` that cloud agents read these to know what a scene contains and
looks like.

## Phase 2: make world content data, editable from both sides

Design this carefully and write it into `docs/world-data.md` before building. Recommended shape
(adapt it if the audit shows something better):

1. **Object definitions as data**, for example `data/world/objects/*.json`. Each definition gives:
   - an id;
   - a display name;
   - the model asset (path or address);
   - material or recolour choices;
   - a **collision footprint**: kind (solid, wall on an edge, none), width and length;
   - the interaction type the server knows (if any);
   - occluder and roof flags;
   - tags (biome, scatter kit).

   An Editor importer builds or updates Unity prefabs from definitions, and never hand-edits
   generated prefabs. Existing prefabs get definitions gradually: first the ones that are placed
   in regions.
2. **Region placements as data**, for example `data/world/regions/<region>.json`. These are
   instances of definitions with a stable id, tile, rotation, fine offset and scale, and
   per-instance overrides (name, examine, recolour, NPC id, wander radius), plus markers (spawn
   points, areas, music zones).
   - In the scene they appear under a `Placed` root, kept in sync both ways:
     - **data → scene** on sync, and when the file changes;
     - **scene → data** when the owner or the local agent moves, adds or deletes placed objects
       and saves (an Editor save hook, plus an explicit "Save placements" command).
   - A round-trip test proves that loading, saving and loading again changes nothing.
3. **Hand-made content stays possible.** Each region scene keeps a `Handmade` root for anything
   not worth turning into data: unique set dressing, lighting tweaks, one-off meshes. Sync never
   touches it, and the snapshot still describes it.
4. **Generated content** (ground, water, roads, scatter) lives under a `Generated` root, and only
   generators write it. If the continent-layout work exists, plug its generator in here.
   Otherwise, move today's `WorldKit` ground, roads and water for Tolling and Palmerway into data,
   so their builders stop owning the scenes.
5. **Migrate Tolling and Palmerway** from builder scripts to data plus these roots, as a one-time
   conversion that keeps every object where it is. Keep the old builders until the conversion is
   verified, then remove them.

**The test that matters:** the exported collision maps and spawn/object files for every region
are byte-identical (or differ only as intended and listed) before and after the migration.

## Phase 3: collision and server data without Unity

Today collision comes only from scene colliders through `MapExporter`, so only Unity can produce
it. With footprints in definitions and placements in data, **compute the server's maps (`.rthmap`,
spawns, objects, region info) in plain C# from the data**, in `/shared` or `/tools` with no
UnityEngine references:
- covered by `dotnet test`;
- runnable as a CLI (`dotnet run --project tools/... -- export-maps`).

Golden tests compare it with the current exported maps. Hand-made colliders that aren't in data
are a problem to solve explicitly, for example:
- a `MapBlocker` or `MapWall` placed as data;
- or the Unity exporter merging the hand-made extras into a small committed overrides file the
  C# exporter reads.

**Result:** a cloud agent can add a fence or a house as data, run the exporter and server tests,
and know it's right before anyone opens Unity. The Unity exporter remains as a verification step
in Sync (it must agree with the C# exporter), or becomes a thin wrapper.

## Phase 4: let cloud agents contribute assets

- **An inbox:** `client/Assets/Art/Incoming/`, or better outside `Assets/` (for example
  `art-inbox/`), so Unity doesn't import half-specified files. Cloud agents drop source files
  there (GLB or FBX models, PNG textures) together with a manifest entry: target folder, import
  settings (scale, point filtering for pixel-art textures, alpha clip, material from the shared
  palette) and the definition that uses it.
- **Sync** moves them into `Assets/`, applies the import settings, lets Unity create `.meta`
  files, builds prefabs from definitions, runs the asset validators (triangle and colour budgets
  from `docs/assets.md`, if the scale and art-style work has landed) and reports problems.
- **Rule:** cloud agents never write `.meta`, `.prefab`, `.unity` or `.asset` files. Put this in
  `CLAUDE.md`.

## Phase 5: the Sync World command

One Editor command (menu **R'th → Sync World**), also runnable in batch mode with a script
(`tools/unity-sync.ps1` / `.sh` calling `Unity -batchmode -executeMethod ...`) for when the
Editor is closed. Steps, in order, each logged:

1. import the inbox;
2. build or update prefabs from definitions;
3. regenerate generated content;
4. apply placements to scenes;
5. export maps with both exporters and compare them;
6. write snapshots and screenshots;
7. run validators (data, assets, world rules, the continent validators if they exist);
8. print a report: what changed, what failed, and which Unity-created files to commit.

**Make it safe:**
- it never touches `Handmade` roots;
- a dry-run mode lists changes without saving;
- it refuses to run with unsaved scene changes.

## Phase 6: guardrails against conflicts and breakage

- `.gitattributes`: a merge driver for UnityYAMLMerge on `.unity` and `.prefab` (document the
  one-time local git config it needs); text eol rules; LFS only if the audit shows it's warranted
  (ask before adding).
- Keep scenes thin. With content in data, most conflicts move to JSON, which merges and diffs
  well. Keep data files sorted and formatted consistently, with one object per line where
  practical.
- **CI** (GitHub Actions) on every PR: `dotnet build`, `dotnet test`, data validation, the C#
  map export and its golden checks, and asset-manifest checks.
- **Unity in CI is optional.** With a Unity licence, GameCI can compile the client and run EditMode
  tests and Sync in batch mode. Present it as a decision for the owner (licence activation,
  minutes) rather than adding it unasked.
- Client C# that cloud agents change can't be compiled in the cloud. Keep such changes small, and
  have Sync or a local compile check (or GameCI, if chosen) verify them before merge.

## Phase 7: the workflow, written down

Write `docs/workflow.md` and update `CLAUDE.md` with a short section linking to it. Cover:

**Roles:**
- **Owner:** decides and hand-crafts in Unity.
- **Local agent:** runs Sync, merges cloud work, does in-Editor detailing and verification.
- **Cloud agents** work in:
  - data;
  - plain C# (`/shared`, `/server`, `/tools`), with careful, small client C#;
  - inbox assets;
  - docs;
  - previews.

**What each may edit:** a table of paths, saying who edits each, whether it is generated or
hand-made, and whether Sync owns it.

**The handoff:**
- A cloud agent opens a PR labelled `needs-unity-sync` with a checklist of what to look at.
- The local agent checks out the branch, runs Sync, commits the Unity-created files (meta, prefabs,
  scenes, exported maps, snapshots, screens) to the same branch, looks at the screenshots and in
  play mode, and reports back on the PR.
- Merge once CI is green and the sync report is clean.
- Add a PR template with these sections.

**Seeing without Unity:** where snapshots and screens live, and how fresh they are (the sync
commit).

**How to run several cloud agents** without them colliding: one region or system per branch;
data files split per region; no two agents on the same region at once.

## Phase 8: prove it end to end

Do one small round trip yourself and document it in `docs/workflow.md` as the worked example:
1. As a "cloud" change made only in data and plain C# (no Unity), add a new object definition and
   a few placements in Palmerway, for example a waystone bench and three fence segments. Run the
   C# exporter and the tests.
2. As the local agent, run Sync with Unity open through the MCP. Check the placed objects appear,
   collision matches, the snapshot and screenshots update, and play mode works.
3. Move one of those objects by hand in the Editor, save, and confirm the placement file changed
   accordingly.

## Ground rules

- **Work incrementally**, in small verified commits. Phases 0, 1, 5 (a first Sync covering what
  exists) and 7 are the minimum valuable set. Do them first if time or usage runs short, and
  leave the rest planned in `docs/workflow.md` with clear next steps.
- **Never lose hand work.** Before any scene migration, commit the current state. After it,
  compare snapshots and exported maps.
- **Gameplay and saves stay unchanged:** same tiles walkable, same spawns, same objects at the
  same coordinates.
- **Coordinate with other branches.** Rebase or merge carefully, and don't reformat files other
  open work is changing.
- **Ask the owner** before:
  - adding Git LFS or GameCI;
  - deleting the old builder scripts;
  - restructuring folders that hold a lot of art.
- **End with a report:**
  - what now lives in data;
  - how to run Sync;
  - what a cloud agent can do today without Unity;
  - what still needs the Editor;
  - the decisions waiting on the owner.
