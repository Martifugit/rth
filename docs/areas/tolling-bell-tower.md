# Tolling's bell tower and the undercroft

**Status:** design (2026-10-28), for the owner's review. Decided with the owner so far: the
dead-house is a cool undercroft below the tower, not a room in its base or a house beside it; the
stone is the soft coursed `Fieldstone` chosen on the tower's studies (`docs/assets.md` section 4).
Everything else here is a proposal.

## What it is for

- **The town's mark on the skyline.** It stands on the knoll's crown and reads from everywhere in
  the basin: the dry rise, Palmerway, across Wennow (`tolling.md`, "Landmarks and views").
- **Every player's first moment.** New characters wake in the undercroft, on a bier, under a
  shroud, by candlelight. They climb the stair, come up into the tower's hall and walk out through
  its door into daylight, the square and the bell overhead. There is no opening scene; the walk
  up and out is the opening (`docs/quests/the-bell.md`).
- **The Bell's first step.** The bell-ringer finds the "body" up and about.
- **A place to go back to.** The ringing chamber is the bell-ringer's; the belfry is a view
  over the whole basin, the mere, the mill and the spine to the west.

## The tower

**Kind:** a plain parish tower of the Wennow basin: rubble stone in courses, not Cruor's dressed
limestone, spires and red tile. Humble and old, built by a village, made taller than anything round
it because the bell had to be heard across the water.

**Size:** within the scale rules (`docs/world-scale.md`: a landmark is at most three storeys and a
roof, and stands on open ground at least as wide as it is tall).

| Part | Height | Notes |
| --- | --- | --- |
| Plinth | 0.3 | a battered (sloping) footing, darker stone (`Granite`), wider than the wall |
| Hall (plane 0) | 2.6 | the door, the stair down, a ladder up |
| Ringing chamber (plane 1) | 2.6 | the bell ropes come down through the floor above; the bell-ringer's |
| Belfry (plane 2) | 2.4 | open on all four sides, the bell in a timber frame |
| Roof | about 2.0 | a low slate pyramid with a finial |
| **Total** | **about 9.9** | over the rule's 8, which was set at the old 2.3 storey: 8 x 2.6 / 2.3 is about 9 |

**Footprint:** 3 x 3 tiles, walls on the grid lines (as every building is), so the hall inside is 3 x 3
tiles less the walls' thickness. Slim, about three times as tall as it is wide: it reads as a tower,
not a keep.

**Shape, bottom to top** (silhouette first; detail only where it reads from the game's camera):
- the plinth, sloping out at the foot;
- the walls rising straight, a string course at each floor (a stone band standing proud), so the
  height reads as storeys from far away;
- the door on the south face: a round-headed opening, a plank door, a stone step;
- a slit window per storey, one face each, staggered round the tower;
- the belfry: an arched opening on each face (two on the faces seen most), the sky showing through
  them from below: the one feature that says "bell tower" at any distance;
- the bell visible in the openings, bronze, the brightest thing on the tower;
- a slate pyramid, a little overhang, a plain iron finial. Not a cross: the faith's symbol is not
  decided yet (owner, 2026-10-28), so nothing carries one until it is.

**Colours** (palette): walls `Stone` on `Fieldstone`, footing and quoin-free corners `Granite`,
roof `Slate`, timber `Oak` / `Timber`, the bell `Bronze`, the door `Planks` with `Iron`. No red: the
village kit keeps red for the faith's banners and glass, and the tower is older than the banners.

**Budget:** a landmark; the guide's soft limit for a large prop is 600 triangles
(`client/Assets/Art/art-budgets.json`), and triangle counts are advisory. Expect 300 to 600 for the
shell and belfry, the interior floors and ladders extra.

## Inside

Every floor is walkable (`CLAUDE.md`, "A floor above the ground"): each upper floor is its own plane,
hidden from below, and ladders join them.

- **Hall (plane 0):** the door in the middle of the south wall; the stair down along the north
  wall, a dark opening with steps going down (a `climb` object to the undercroft); a ladder in the
  north-east corner up to the ringing chamber. Flagstone floor (`Flagstone`). A bench, a lantern.
- **Ringing chamber (plane 1):** the bell rope (or two) hanging through a hole in the ceiling, its
  sally (the woollen grip) at hand height; a coil of spare rope, a stool; a ladder on up. The
  bell-ringer stands here when he isn't in the square. A slit window looks out over the square.
- **Belfry (plane 2):** a timber deck round the bell frame, the openings at waist height with a low
  stone sill, so standing there you see the basin. A **view point** object (as Meridian has):
  "Look out" names what is in sight. The bell itself is an object: "Ring" is the bell-ringer's,
  not the player's, until a quest says otherwise.

## The undercroft (the dead-house)

**Its own region**, as every cellar and crypt is (`CLAUDE.md`: there is no plane below the
ground; cellars are small instanced regions north of the surface place, joined by a `climb` object
at each end). About 8 x 10 tiles: larger than the tower's footprint, because a crypt runs under the
church as well, and the dead-house serves the whole churchyard (the drowned from Wennow are buried
there).

- **The stair up** at one end, from a short passage into the vault; light falls down it.
- **The vault:** low, round arches on squat piers, dressed in the same stone; a groined ceiling
  isn't drawn (the camera looks down, as in the Saint's crypt), so the vault reads from its
  arches and piers along the walls.
- **Biers**, three or four, stone, in a row: one is the player's, its shroud thrown back; the
  others shrouded, still. **Candles** in niches and on the floor at the biers' heads (the caves
  kit's candles are a first pass). A **ledger** on a lectern by the stair: who lies here, and
  the player's name struck through later in The Bell.
- **Light:** its own atmosphere (as a region can have): cold blue-grey ambient, near-black fog
  past a few tiles, warm candlelight in pools. Cool: the reason it is down here.
- **The spawn** for a new character is beside the player's bier (the layout's start place moves
  here; `ServerConfig.SpawnRegion`). Death still wakes you at the ferryman's landing, never here.

## How it is built

- **The tower** is a landmark asset: `tools/art/blender/assets/landmarks/village/bell_tower.py`,
  exporting `Art/Assets/Landmarks/Village/BellTower.fbx` and, as separate models, its upper floors
  (each plane's slab and furniture, so the game can hide them from below) and the bell (so it can
  swing). Its collision is region data: walls along the shell's faces open at the door, the
  ladders and the stair as `climb` objects, the upper floors as planes. Checking at blockout: how a
  data region gives a model upper planes and walls (the `building` piece does both for kit
  buildings; the tower may need the same for one model).
- **The undercroft** is a region of its own in `world/regions/`, built from parts: a vault family
  for the arches and piers (`parts/walls/village` or a new `crypt` style), biers and shrouds in
  `assets/furniture/village`, the ledger and lectern, the candles.
- **New models** go in the catalogue as they are made: `BellTower`, `Bier`, `Lectern`, the vault
  parts, `BellRope`, a ladder and a trapdoor-stair if the existing ones don't suit.

## Order

1. **Blockout** at true scale in Blender: the tower's shell, its three floors and belfry, the
   ladders and stair; the undercroft's vault and biers; a player on every floor. Judged by the
   owner in the viewport, from the game camera and from inside.
2. **Collision and planes** worked out against the blockout (what the region data needs).
3. **Detail** on the tower (string courses, door, slits, belfry openings, roof) and the
   undercroft (arches, biers, candles), then export.
4. **Placed** at stage 2 of Tolling, with the chapel beside it and open ground round it.

## Open

- The chapel beside it: the tower at the chapel's west end, joined to it, or standing free in the
  churchyard? (Proposal: free-standing, a few tiles off the chapel's west end, so it has open ground
  on three sides.)
- The faith's mark for the finial.
- Whether the player may ring the bell (and what it does: a quest, a once-a-day toll, nothing).
