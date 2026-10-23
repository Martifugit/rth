# Selenfall's breach: the brief

**Status:** draft for the owner (2026-10-23). The first area built by hand through the process in
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

The farmland belongs to the margin. Light could be the place's own: a cooler, paler sun in the bowl
(`ownAtmosphere` on the region).

**The shape (geography, stage 1):**
- **The crest** is the wall north and south: blocked rock standing about a storey above the
  highland, as the guide says.
- **The crater** is a round bowl about 64 × 52 tiles, cut straight through the crest.
  - Its **rim** is a raised ridge, highest where it meets the crest, lower and broken on the east
    and west where the breaches are.
  - Its **floor** sits below the highland on either side, so you walk *down* into it.
  - Its inner walls are cliffs (1-3 tiles, blocked), except on the two slopes the breaches make.
- **The breaches** are the only ways in: the east one from the empire's side (the road), the west
  one down to the frontier. Each is a narrow ramp through the rim, a few tiles wide, climbing at
  most what the guide allows.
- **Heights, roughly** (in tiles; the whole world stays within about 16):

  | Where | Height |
  | --- | --- |
  | the approach and the village | 0-1 |
  | the highland foothills | 2-4 |
  | the crest's rock tops | about 6 |
  | the rim | 6-8, highest at the crest |
  | the bowl's floor | about 2, with a lowest point near the fragment |

**Landmarks and views:**
- **The bite in the crest,** seen from the road above p-dry-north: the one skyline of the area.
- **The fragment** at the crater's heart (the layout's landmark at -172, 286): a half-buried mass
  of moon rock on open floor, at least as wide around it as it is tall. It is the only thing in the
  bowl taller than a person. The moon materials already started under `Assets/Art/Fx` (`MoonFragment`,
  `MoonGlow`) are for it.
- **The rim at the east breach,** the first view into the bowl: the whole crater at once, the
  fragment in the middle, the west breach opposite.
- **Back east from the breach:** the empire's farmland and Tolling's river below.

**Routes:**
- **In from the east:** the road from p-dry-north (`selenfall-road` in the layout) climbs the
  foothills to the east breach. It passes the spring where Tolling's river rises, so the river the
  player knows from home starts here.
- **Through:** down into the bowl, past the fragment, up the west breach.
- **Out:** down into the frontier (the pass's west end, about x -240).
- The road on south from p-dry-north to p-observatory, Magister Varrow's country, is the margin's,
  not this area's. It is the reason for a later link: she forecasts falls.

**Wide and narrow:** wide on the approach, narrow at each breach (the chokepoints), wide again on
the bowl's floor: an open arena between two gates.

**Edges:**
- the crest's rock north and south, and the crater's inner cliffs;
- to the west, the frontier beyond the foothills is fenced by rough ground and rock;
- never an invisible line in open ground.

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

**Danger:**
- the approach is gentle;
- the highland is emptier and harsher;
- the bowl is cold and pale, quiet before it is dangerous.

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

Decisions this area raises for the owner:
- the heights above;
- whether the bowl has its own light;
- the D4 camera profile, since walking the bowl is the first real test of looking across height.
