# A worked example: building an area together

How the method in [world-guide.md](world-guide.md) plays out on one real stretch of the
continent, from the blockout to a finished area: who does what, which files change, and what you
should see when you walk it after each stage. Refer to it when starting an area.

The area, its regions and its layout are real (`world/layout/continent.json`, 2026-10-23). The
brief's content is **illustrative**: it shows the kind and amount of detail a brief needs, not
decisions about this area. `p-` names are placeholders, as everywhere.

Some steps lean on tools that are decided but not built yet; they are marked *(to build)*. Until
they exist, the same step is done by hand or skipped, as noted.

**Who:**

| | |
| --- | --- |
| **Owner** | Decides, sculpts and dresses in Unity, walks every stage and signs it off. |
| **Unity agent** | The agent on the owner's machine: drives the Editor, runs Sync World, compiles client code, commits what Unity writes. |
| **Data agent** | Any agent without Unity (a worktree or the cloud): briefs, data, C#, checks and renders. |

---

## The area: the salt road

West from Tolling along the salt road, through the foothills of the spine and over the salt pass
to the far side of the crest. Three regions, with Tolling as the busy neighbour.

| Region | Tiles | In the layout |
| --- | --- | --- |
| `land_m1_0` | x -96..-1, y 0..95 | Farmland giving way to Sere's dry scrub; the salt road from Tolling's west edge |
| `land_m2_0` | x -192..-97, y 0..95 | The spine's highland, the crest (blocked rock) and the salt pass cutting through it |
| `land_m3_0` | x -288..-193, y 0..95 | The far foothills, the hamlet p-west-toll at the pass's west end, frontier beyond |
| `tolling` (margin) | x 0..95, y 0..95 | Built by hand: the start. Read-only for this work. |

Stage 0, as generated (1 character = 4 × 8 tiles; west on the left, Tolling on the right):

```
%%%%%%%%%%%%%%%kkkkkkkkkkkkmmmmmmmmmmmkkkkkkkkkkkkkkksssssggggggggggggg-TTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%kkkkkkkkkkkkkkmmmmmmmmmmmkkkkkkkkkkkkkksssssgggggggggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%kkkkkkkkkkkkkkmmmmmmmmmmkkkkkkkkkkkkksssssggggggggggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%kkkkkkkkkkkkkkmmmmmmmmmkkkkkkkkkkkkkkkksssssggggggggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%kkkkkkkkkkkkkkkmmmmmmmmkkkkkkkkkkkkkkkkssssssssssgggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%kppkkkkkkkkkkkkmmmmmmmmmkkkkkkkkkkkkkksssssssssssgggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%--++ppppppppppppppppppppppppppppppppp--------------------TTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%%%kkkkkpppppppppppppppppppppppppppp---kksssssgggggggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%%%kkkkkkkkkkkkkkmmmmmmmmmmkkkkkkkk--kkksssssssssggggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%%%kkkkkkkkkkkkkkmmmmmmmmmmkkkkkkkk--kkkksssssssssgggggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%%%kkkkkkkkkkkkkmmmmmmmmmmmmkkkkkk--kkkkkkssssssssssgggggTTTTTTTTTTTTTTTTTTTTTTTT
%%%%%%%%%%%%%%%%%kkkkkkkkkkkkkkkkmmmmmmmmmkkkkkkk--kkkkkkssssssssssgggggTTTTTTTTTTTTTTTTTTTTTTTT
   land_m3_0               land_m2_0               land_m1_0               tolling
% frontier (blocked)  k highland  m crest (blocked)  p pass  s scrub  g farmland  - road  + hamlet
```

Why it makes a good example: it is a few regions with one identity (the way west over the
mountains), it is mostly about **geography** (foothills, a crest, a pass), and it joins the most
finished place in the game to land that has nothing yet.

---

## Step 0: claim it

**Data agent:**
- Branch `claude/area-salt-road` from `main`.
- Claim `land_m1_0`, `land_m2_0`, `land_m3_0`; the margin (`tolling`, `land_m1_1`, `land_m2_1`,
  `land_m1_m1`, `land_m2_m1`) is read-only. *(To build: the claims file the tools check. Until
  then, a line in [plan.md](plan.md)'s workstreams.)*

**Result:** everyone can see who holds these regions; nobody else edits them until the branch merges.

## Step 1: the brief

**Data agent** drafts `docs/areas/salt-road.md` from the layout and the area plan; **the owner**
corrects and approves it. Nothing in the world changes yet.

> **The salt road** (illustrative)
>
> **What it is for:** the way west from Tolling over the spine. Early-game: the first time a
> player sees the mountains up close and the first long walk away from home.
>
> **When players come:** early (the road, the foothills), early to mid (the pass and beyond).
>
> **Who lives here:** a few herders in the foothills; the people of p-west-toll at the far end of
> the pass. Whatever lives in the high rocks.
>
> **Palette and ground:** farmland's green fading into Sere's dry brown, then grey-green
> highland, bare rock on the crest. Four ground colours at most.
>
> **Landmarks and views:**
> - The crest, seen from Tolling's west edge: the first view of the mountains.
> - A waymarker at the foot of the pass (where the road meets the highland), seen from the scrub.
> - From the top of the pass, a view back east over the farmland towards Tolling.
>
> **Routes:** the salt road enters from Tolling's west edge and becomes the pass; a side track
> south to the high valley (`land_m2_m1`, not this area). Leaves west at p-west-toll.
>
> **Wide and narrow:** open scrub and foothills; the pass narrows to a few tiles at the crest (the
> chokepoint), then opens again on the far side.
>
> **Edges:** the crest is the wall north and south of the pass; the frontier beyond p-west-toll
> is fenced by rough ground and rock, not an invisible line.
>
> **Something to find:** one hidden thing off the road in the foothills.
>
> **Resources and creatures (counts):** a few copper and one iron seam in the foothills; a
> handful of trees near the farmland only; one kind of early creature in the scrub, one tougher
> kind near the crest.
>
> **History in the ground:** an older, narrower track beside the road through the pass.
>
> **Danger:** gentle by the farmland, harsher and emptier towards the crest.

**Result:** one page everyone checks their work against. A brief is about this long: enough to
judge the work by, not a design of every object.

## Step 2: the sketch

**Data agent** marks the brief's places on the stage-0 render (`tools/world render land_m1_0
land_m2_0 land_m3_0`): where the waymarker stands, where the pass narrows, where the view back is,
where the old track runs. **The owner** adjusts.

**Result:** the brief on a map: a picture of the area from above with the few things that matter.

## Step 3: stage 1, geography

The area's generated base is **frozen** into its three region files first: from now on they belong
to this work, not the generator (delete each file's `"generated"` line).

**In parallel:**

| Owner (with the Unity agent) | Data agent |
| --- | --- |
| Sculpts in Unity with **Sculpt Ground**: foothills rising west of the scrub, the crest as a raised ridge of rock blocks, the pass as a notch with a gentle climb, the far side falling away. Saving writes the heights to the region files. | Declares the area's **exits** as data: the salt road meets Tolling's west edge at Tolling's road (its tiles and height read off Tolling), and leaves at p-west-toll into the frontier. *(To build: exits as data.)* |
| Watches the slope overlay: tiles too steep to walk turn red under the brush, so cliffs are where they are meant to be. | Runs the **seam check** (`tools/world check` warns where heights step at a border) against the neighbours: Tolling is flat, so the last 8 or so tiles of `land_m1_0` come down to Tolling's height at x 0. *(Built for heights; roads and rivers to build.)* |
| | Checks the terrain rules: relief per 24 × 24 window, cliffs 1-3 tiles, the road never steeper than walkable. |

**Commands:** `tools/world check`, `export`, `render` (hill-shaded once there is terrain),
`dotnet test` (the data agent; `tools/world terrain <region> flat` gives a region terrain to start
from without Unity); **Sync World** (only these three regions have changed, so only they sync) and
Play (the Unity agent and the owner).

**Walk it at player height,** at both camera profiles, from Tolling's west gate to p-west-toll.

**Expected result:** still no trees or buildings, but it reads as a place: you walk out of Tolling
over flat farmland, the land starts to rise in the scrub, the crest stands as a wall ahead with a
visible notch, the pass climbs and narrows, and you look back down over the farmland from the top.
No seams at the borders; the road is walkable everywhere; the camera lifts over the foothills.

**Sign-off:** the owner walks it and the region files record `stage 1`. *(To build: the stage
field and the coverage map.)*

## Step 4: stage 2, structure

**Data agent,** as data: the road and the old track as final lines; the waymarker, a placeholder at
its tile; p-west-toll blocked out as a few houses and a fence at the size the brief gives; the
edges (rough ground and rock pieces where the frontier begins). **The owner** places what is easier
by hand in Unity (captured to the handmade file) and checks the views from the brief.

**Expected result:** the bones of the area: you can see where everything will be. The waymarker is
seen from the scrub, the pass's narrowing works as a chokepoint, p-west-toll reads as a hamlet from
the top of the pass. Grey boxes are fine; the layout is what is judged.

**Sign-off:** a walk, `stage 2`.

## Step 5: stage 3, life

Only now does content go in (layout before content).

**Data agent:** the seams, trees, creatures and objects from the brief, as pieces in the region
files, against content in `server/data/content` (new creature or object types added there).
Every resource placed is checked reachable (`ShippedMapTests`) and counted against the brief.
The hidden thing goes in. **The owner** plays the area as a new player would: does the walk west
feel like the first adventure?

**Expected result:** things to do: mine in the foothills, fight on the way to the crest, find the
hidden thing, reach p-west-toll. Counts match the brief.

**Sign-off:** a walk and a play, `stage 3`.

## Step 6: stage 4, finish

**Owner and Unity agent:** models from the asset list (`docs/regions/<region>-assets.md`) replace
placeholders; ground colours and lighting tuned; set dressing kept sparse (RuneScape's scale);
every tall thing given its fade. **Data agent:** anything that is data (sounds, region titles,
object text).

**Expected result:** an area that looks finished and matches the brief.

**Sign-off:** `stage 4`; the branch merges and the claim is released.

---

## What changes where

| Step | Data agent writes | Owner / Unity agent writes |
| --- | --- | --- |
| 0. Claim | the claims file (or plan.md) | |
| 1. Brief | `docs/areas/salt-road.md` | corrections to it |
| 2. Sketch | a marked-up render in the brief | corrections |
| 3. Geography | exits, region files' `generated` line removed, seam fixes | heights (saved into the region files), Sync World output |
| 4. Structure | road, track, blockout pieces in region files | hand placements (handmade files), scenes |
| 5. Life | pieces in region files, content JSON | play-test notes |
| 6. Finish | data-side text and sounds | scenes, materials, models, `.meta` files |

## Joining the busy place to the quiet one

Tolling is at a far later stage than its neighbours. The rules from the guide, as they apply here:

- **The area meets Tolling, not the other way round.** Tolling is not changed by this work. The
  area takes its road exit and its height at the border from Tolling.
- **The last few tiles are quiet.** Near Tolling's edge, `land_m1_0` is farmland like Tolling's
  own, at Tolling's height, with no landmark or content crowding the border.
- **The road leads.** From Tolling's west gate the crest is the view ahead; from the pass, Tolling
  is the view back.
- **The neighbours at stage 0** (`land_m1_1` north, `land_m1_m1` south) are untouched: the seam
  check keeps this area's borders with them clean, at the generated heights, until their turn.
