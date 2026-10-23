# Selenfall's breach: the brief

**Status:** approved by the owner (2026-10-23), with a deeper crater and built ways down into
it; the owner's notes are worked in below. The first area built by hand through the process in
[world-guide.md](../world-guide.md), terrain included; [world-example.md](../world-example.md) is the
template. Nothing in the world changes until the owner approves this page.

## The regions

| Region | Tiles | Today (generated) | Role in the area |
| --- | --- | --- | --- |
| `land_m2_2` | x -192..-97, y 192..287 | highland, the crest, the scar's south half, the landmark, the pass | the crater's south half and the fragment; the east breach |
| `land_m2_3` | x -192..-97, y 288..383 | highland, the crest, the scar's north half | the crater's north half and rim |
| `land_m3_2` | x -288..-193, y 192..287 | frontier, highland, crest, the pass's west end | the west breach and the way down into the frontier |
| `land_m3_3` | x -288..-193, y 288..383 | frontier, highland, a sliver of scar | the crater's north-west rim; mostly edge |
| `land_m1_2` | x -96..-1, y 192..287 | scrub and farmland, p-dry-north, Tolling's river, the road up | the approach: from the village up the road to the foot of the breach |

**Margin, read-only:** `land_m1_3` (north of the approach), `land_m2_1`, `land_m3_1` and `land_m1_1`
(south: the road on to p-observatory). Each region in the area is joined to these as they stand.

## The brief

The sketch: [selenfall-sketch.png](selenfall-sketch.png), the places below marked on the
stage-0 render.

**What it is for:** the scar the first moon fragment tore through the spine, and the middle way
over it. A place you look at before you reach it: from the empire's foothills the crest has a bite
taken out of it. It tells the player the moon is cracked and still falling, years before they see
a fall (`docs/lore.md`, "The cracked moon").

**When players come:** early to mid. The approach and the village are early; the bowl is the first
place where the land itself feels wrong, and its creatures are a step up from the scrub's.

**Who lives here:**
- the empire's dry farmers at p-dry-north, at the foot of the road;
- the spine's surface people keep the breach as a crossing, with a post at the east breach;
- in the bowl, nothing that was born there before the fall: moonborn things, and the strange
  growth the lore promises around a fall.

**Palette and ground (four colours at most):**
- the approach's dry scrub (`LandScrub`);
- the highland's grey-green (`LandHighland`);
- the crest's rock (`LandCrest`);
- the scar itself: pale, cold, glassy slate (`LandScar`, today `Slate`).

The farmland belongs to the margin.

**Light (decided):** the bowl has its own, cold and pale: a cooler, weaker sun, a cold ambient and a
faint bluish fog on the crater regions (`ownAtmosphere`), blended in as you go down. The depth earns
it: the floor lies in the rim's shadow, and the only warm light down there is what people brought
(the lanterns of the way down). The fragment and the moon rock give a faint glow of their own
(stage 4).

**The shape (geography, stage 1):**
- **The crest** is the wall north and south: blocked rock standing about a storey above the
  highland, as the guide says.
- **The crater** is a deep, round pit about 64 × 52 tiles, cut straight through the crest.
  - Its **rim** is a ridge of thrown-up rock, higher than the crest it broke (9-10 tiles), lower
    only where the breaches cut it.
  - Its **inner wall** is a band about 12 tiles wide falling 10 tiles: cliffs (blocked), except
    where a way down is cut or has fallen.
  - Its **floor** is flat and broad, about 40 × 28 tiles at height 0, with the fragment in the
    middle and room around it for creatures, seams and a fight. A slight hollow round the fragment
    is the only unevenness.
- **Heights** (in tiles; the whole world stays within about 16, and a tile steeper than 1 is a
  cliff):

  | Where | Height |
  | --- | --- |
  | the approach and the village | 0-1 |
  | the highland foothills | 2-5, rising west |
  | the crest's rock tops | about 6-7 |
  | the rim | 9-10 |
  | the breach notches in the rim | about 8 |
  | the floor | 0 |
  | the frontier side, below the west breach | about 2 |

**The ways down (decided: people built them).** Ten tiles is too deep to walk straight down: at the
guide's 1 in 3, a way down is about 30 tiles long. So each way in is a route along the wall, not a
gap in it.

- **The Descent (east), built.** The spine's surface people cut a road into the inner wall. It
  starts from their post at the east breach and runs down along the wall as a ledge, three tiles
  wide, through a quarter of the crater. A turn halfway down is a landing with a view. A low wall
  stands on the drop side and lanterns line the way. It is the easy way, and the one the empire's
  road leads to.
- **The scree (west), natural.** Where the fall threw its rock out west, the wall collapsed into a
  long, broken slope of rubble. It is walkable but narrow between boulders, longer and rougher than
  the Descent, and leads up to the west breach and down to the frontier.
- **The rope (north), a shortcut.** A rope ladder hangs down the north wall where the rim is
  highest: a skill shortcut (a `climb` with requirements) from the rim path to the floor.
- **Along the rim:** a path follows the rim's top between the two breaches on the north side, for
  the view and to reach the rope.

**Landmarks and views:**
- **The bite in the crest,** seen from the road above p-dry-north: the one skyline of the area.
- **The fragment** at the crater's heart (the layout's landmark at -172, 286): a half-buried mass
  of moon rock on open floor, at least as wide around it as it is tall. It is the only thing in the
  bowl taller than a person. The moon materials already started under `Assets/Art/Fx` (`MoonFragment`,
  `MoonGlow`) are for it.
- **The rim at the east breach,** the first view into the bowl: the whole crater at once, ten
  tiles down, with the fragment in the middle, the Descent's lanterns curving away below and the
  scree opposite.
- **The landing halfway down the Descent:** the floor close enough to see what moves on it.
- **Back east from the breach:** the empire's farmland and Tolling's river below.

**Routes:**
- **In from the east:** the road from p-dry-north (`selenfall-road` in the layout) climbs the
  foothills to the east breach. It passes a spring where Tolling's river rises: a small pool beside
  the road, a landmark of the approach (decided; no lore claim beyond "the river rises here").
- **Through:** down the Descent, across the floor past the fragment, up the scree to the west
  breach.
- **Out:** down into the frontier (the pass's west end, about x -240).
- The road on south from p-dry-north to p-observatory, Magister Varrow's country, is the margin's,
  not this area's. It is the reason for a later link: she forecasts falls.

**Wide and narrow:** wide on the approach; narrow at each breach and along the Descent and the
scree (the chokepoints); wide on the floor, an open arena below two ways down.

**Edges:**
- the crest's rock north and south, and the crater's inner cliffs;
- to the west, the frontier beyond the foothills is fenced by rough ground and rock;
- never an invisible line in open ground.

*Stages 2-3 below are approved as written (2026-10-23) and revisited after stage 1 is walked.*

**Something to find:** a second, small fragment hidden in a fold of the south rim, reached by a
scramble (later a skill shortcut: a `climb` with requirements), with something growing around it.

**Resources and creatures (counts):**
- a few seams on the approach's foothills;
- one unusual seam in the bowl, where the moon rock broke (what it gives is for the skills pass);
- one early creature in the scrub;
- one moonborn kind in the bowl, a step tougher;
- no world boss now (the lore keeps that for the largest falls).

**History in the ground:**
- Before the fall there was no way over the crest here; the fall made one.
- Old cairns mark the first crossings along the breach's line, older than the road.
- The surface people's post at the east breach is newer, and the empire's road ends at it.
- The Descent is their work: older stretches of it, worn and fallen, show below the newer wall.

**Danger:**
- the approach is gentle;
- the highland is emptier and harsher;
- the Descent is safe while its lanterns last;
- the floor is cold and pale, quiet before it is dangerous.

## How we build it

The steps in [world-example.md](../world-example.md), adjusted for this area:

1. **Claim** these five regions (a line in [plan.md](../plan.md)) and work on `claude/area-selenfall`.
2. **The brief** (this page), approved by the owner.
3. **The sketch:** this page's places marked on the stage-0 render.
4. **Stage 1, geography:** take the five regions over (delete `"generated"`). Then:
   - a data agent writes a first terrain from the shape above (`tools/world terrain`, then the
     crater's heights in text);
   - the owner sculpts it in Unity with **Sculpt Ground**;
   - we walk it at player height;
   - `tools/world check` keeps the seams to the margin flat.

   Tools this stage needs that aren't built yet: entrances and exits as data (the road's two ends),
   and the seam check for roads and walking (C6).
5. **Stages 2-4:** structure (the post, the cairns, the village's edge), life (seams, creatures),
   finish (the fragment's art, the light).

What stage 1 needs that the tools can't do yet:
- **Terrain** writes the pit, the rim and the scree; the ledge of the Descent is terrain too (tiles
  stepping down gently beside steep ones).
- **The Descent's low wall and the landing** are built structure (stage 2), but its line is fixed
  in stage 1.
- **The rope** is an object (stage 2).
- **Entrances and exits as data** and the seam check for walking (C6) come when the margin is
  joined.

Still open: the camera profile (D4). Compare both profiles in Settings while walking the bowl, and
decide then.

## Stage 1 log

**First pass, data (2026-10-23).** The five regions were taken over (no `"generated"`) and given
a terrain and ground drawn from this brief by a one-off script, so the brief's numbers became heights.
From here the region files are the work: sculpt them, edit them, never regenerate them.

- **The pit:** the rim at 9-10 (8 at the east breach, 8.5 at the west), the inner cliff, a walkable
  apron at its foot, and a flat floor at 0 with a hollow of -0.4 round the fragment. Scar ground
  (`c`) inside the rim.
- **The Descent** (`d`, cobbles): a ledge cut into the wall from the east breach (8) round the
  south-east wall to the floor, with the landing halfway (4.4). Walkable about three tiles wide,
  rock above and a drop below.
- **The scree** (`u`, shingle, with boulders `o` that block): from the west breach down the
  south-west wall to the floor, with a way left between the boulders.
- **The south rim** is crest rock (blocked), so the hidden fragment will be a scramble.
- **The outside:** the rim falls away gently all round. Heights from the old ground kinds were
  blurred, highland about 4-5 and the crest about 7, then brought down to 0 at the area's edges so
  every border meets its flat neighbour (`check`: no seam warnings).
- **The spring:** a pool where Tolling's river rises, beside the road.
- **Walked by a script:** the floor, both ways down, the landing, the north rim and the frontier
  side can all be reached from p-dry-north. About 47 scattered tiles on the outer south cliff
  are flat enough to stand on but can't be reached; sculpt them away.

**What to judge in the Editor:**
- the depth from the rim and from the floor;
- whether the cliff reads as a cliff;
- the Descent's width and how steep it looks;
- whether the scree reads as rubble;
- whether the land falling to 0 at the area's edges looks wrong (a hill in a flat world, until its
  neighbours get terrain).
