# Selenfall's breach: the brief

**Status:** stage 1 of the surface approved by the owner (2026-10-23): "a good foundation for
what it'll become". Next: the lift and the first level. The brief was approved and then revised
the same day after the first walk: the
crater is a shaft you descend by lift through three levels to the heart, not a pit walked to the
bottom. The first area built by hand through the process in [world-guide.md](../world-guide.md),
terrain included; [world-example.md](../world-example.md) is the template.

## The regions

**The surface** (taken over from the generator):

| Region | Tiles | Role in the area |
| --- | --- | --- |
| `land_m2_2` | x -192..-97, y 192..287 | the crater's south half, the shaft's mouth; the east breach and the lift |
| `land_m2_3` | x -192..-97, y 288..383 | the crater's north half and rim |
| `land_m3_2` | x -288..-193, y 192..287 | the west breach and the way down into the frontier |
| `land_m3_3` | x -288..-193, y 288..383 | the crater's north-west rim; mostly edge |
| `land_m1_2` | x -96..-1, y 192..287 | the approach: from p-dry-north up the road to the east breach |

**Margin, read-only:** `land_m1_3` (north of the approach), `land_m2_1`, `land_m3_1` and `land_m1_1`
(south: the road on to p-observatory). Each region in the area is joined to these as they stand.

**Below** (new regions, one per level, built one at a time). They follow the rule for cellars and
caves: away from the continent, 1024 tiles north of the place for the first level and 1024 further
for each level below, so the shaft stands directly "under" itself in the layout:

| Level | Centre | Region |
| --- | --- | --- |
| 1, the Works | (-172, 1310) | `selenfall_1`, sized when it's built (about a region) |
| 2, the Glass | (-172, 2334) | `selenfall_2` |
| 3, the Hollows | (-172, 3358) | `selenfall_3` |
| the heart | (-172, 4382) | `selenfall_heart` |

## The brief

The sketch: [selenfall-sketch.png](selenfall-sketch.png).

**What it is for:** the scar the first moon fragment tore through the spine. From the empire's
foothills the crest has a bite taken out of it; inside, the bite is a shaft without a visible
bottom, and going down it is going somewhere the world wasn't made to go. It tells the player the
moon is cracked and still falling (`docs/lore.md`, "The cracked moon"). It is also the middle way
over the spine, but a guarded one.

**When players come:** early to mid for the surface and the first level; each level below is a
step harder; the heart is late.

**Who lives here:**
- the empire's dry farmers at p-dry-north, at the foot of the road;
- the spine's surface people: they keep the crossing and the lift, and work the first level;
- below, fewer people and more moonborn things, and the strange growth the lore promises round a
  fall, stranger the deeper you go.

**Palette and ground (four colours at most on the surface):**
- the approach's dry scrub (`LandScrub`);
- the highland's grey-green (`LandHighland`);
- the crest's rock (`LandCrest`);
- the scar: pale, cold stone (`LandScar`, `Stone`; `Slate` read as dark blue on the owner's walk);
- in the hole only, moonlight blue (`LandMoonFace`, `Moonlight`).

Each level below has its own few colours.

**Light (decided):** the crater has its own, cold and pale (`ownAtmosphere`): a cooler, weaker sun,
a cold ambient and a faint bluish fog, blended in as you cross the rim. From the mouth rises a
glow, with mist over the shaft, so the bottom is never seen. Each level below is darker and
stranger, lit by the glow from beneath and what people brought (the lift's lamps).

### The surface (stage 1)

- **The crest** is the wall north and south: blocked rock standing about a storey above the
  highland, as the guide says.
- **The crater** is wide, about 96 × 76 tiles inside its rim, cut straight through the crest:
  - **The rim** is a ridge of thrown-up rock, higher than the crest it broke (9-10 tiles), lower
    only at the two breaches (8 east, 8.5 west).
  - **The shelf** is a ring inside the rim, about 8 tiles wide (halved on the owner's walk, so the
    hole is the bigger thing), sloping from the rim down to the mouth's lip (about 7). It holds
    the lift and its winch house, the guards' post, a camp and moon rock breaking through.
    Further content goes down the shaft.
  - **The crest's line goes on across the shelf**, splitting it into the empire's side (east,
    with the lift) and the frontier's (west): on the north a wall of thrown rock, on the south a
    ridge with one gap, the guarded gate.
  - **The mouth** of the shaft is about 80 × 62 tiles. Its lip falls away in a cliff into
    darkness, with no ground below what you can see. The faces in the hole are moonlight blue
    (`Moonlight`, given as the lip's and the shaft's `cliff` material), and the glow and the mist
    come later (stage 4).
- **Heights** (in tiles; the whole world stays within about 16, and a tile steeper than 1 is a
  cliff):

  | Where | Height |
  | --- | --- |
  | the approach and the village | 0-1 |
  | the highland foothills | 2-5 |
  | the crest's rock tops | about 6-7 |
  | the rim | 9-10 |
  | the breaches | 8 (east), 8.5 (west) |
  | the shelf | from the rim down to about 7 at the lip |
  | the mouth's wall | from the lip down to -0.2, its bottom moonlight blue (the Game scene's unbuilt land, at -0.3, would cover anything lower) |
  | the frontier side, below the west breach | about 2 |

### Going down (decided)

- **The cage lift on a winch.** The spine's people built it on a gantry over the lip on the east
  side, below the east breach. It stops at each level. The ride is a fade to black, and you stand
  beside the lift on the level below. Its gear is older and more makeshift at each stop down.
- **Each level is its own place**, with its own regions, light and terrain:
  - it keeps the shaft's outline, so you always know where you are in it;
  - it opens out sideways into ledges, galleries and hollows, so it has room for content;
  - its shaft walls rise a few tiles and end in a clean cut-off edge, the Castle Drakan trick: you
    see the walls around you, never all the way up, and the camera can always see in.
- **Never one path.** Some levels also have a second way down or up besides the lift (a rope or a
  scramble behind a skill requirement), so the descent isn't a single line.
- **The levels** (ideas for each level's own brief, in its turn):
  1. **The Works.** The surface people's level: the lift's first stage, a camp on the ledges, moon
     rock seams, the first strange growth.
  2. **The Glass.** Walls fused to glass by the fall, growth that shouldn't grow, wrong light.
  3. **The Hollows.** Where the moonborn nest. The lift's last stop is makeshift, and few go
     further.
  4. **The heart.** The fragment itself, kept for a boss later.

### Routes

- **In from the east:** the road from p-dry-north (`selenfall-road` in the layout) climbs the
  foothills to the east breach. It passes a spring where Tolling's river rises: a small pool beside
  the road (decided; no lore claim beyond "the river rises here").
- **Onto the shelf** through the east breach; the lift is just inside.
- **The guarded crossing (decided):** round the south shelf to the gate in the ridge (about
  -179, 256), kept by the surface people and opened only after a quest (a `requiresFlag` gate with
  a locked message, stage 2). Beyond it, the west shelf, the west breach and down into the frontier
  (about x -240). Until then Selenfall is a place you go down into, not through.
- The road on south from p-dry-north to p-observatory, Magister Varrow's country, is the margin's,
  not this area's. It is the reason for a later link: she forecasts falls.

**Wide and narrow:** wide on the approach and round the shelf; narrow at each breach (the
chokepoints); the shaft is the great void in the middle.

**Edges:**
- the crest's rock north and south, the rim, and the lip of the shaft;
- to the west, the frontier beyond the guarded gate is fenced by rough ground and rock;
- never an invisible line in open ground.

**Landmarks and views:**
- **The bite in the crest,** seen from the road above p-dry-north: the one skyline of the area.
- **The east breach,** the first view in: the shelf, the lift's gantry against the glow, and the
  mouth.
- **The glow and the mist** over the shaft, seen from anywhere on the rim.
- **Back east from the breach:** the empire's farmland and Tolling's river below.

*Stages 2-3 below are approved as written (2026-10-23), moved to fit the new shape, and revisited
after stage 1 is walked.*

**Something to find:** a small fragment hidden in a fold of the south rim, reached by a scramble
(a `climb` with requirements), with something growing around it.

**Resources and creatures (counts, the surface):**
- a few seams on the approach's foothills;
- moon rock breaking through the shelf (what it gives is for the skills pass);
- one early creature in the scrub;
- nothing moonborn above the first level, or only something that climbed out.

**History in the ground:**
- Before the fall there was no way over the crest here; the fall made one.
- Old cairns mark the first crossings along the shelf, older than the road.
- The surface people's post at the gate is newer, and the empire's road ends at the east breach.
- The lift's gantry stands on older foundations: they have been going down for a long time.

**Danger:**
- the approach is gentle;
- the highland is emptier and harsher;
- the shelf is cold and pale but kept;
- each level below is worse.

## How we build it

1. **The surface, stage 1:** the wider crater, the shelf and the mouth, as terrain in the five
   regions. Walked, then sculpted.
2. **The lift and the first level.** The lift as a `climb` object at each end, with its landing on
   the level below. The fade to black, the level regions, and the cut-off walls are the new client
   work.
3. **Each level in turn:** its own short brief, then the same stages (geography, structure, life,
   finish).
4. **The crossing** (a gate and its quest) when the story is ready for it.

Still open: the camera profile (D4); compare both in Settings while on the shelf, and decide then.

## Stage 1 log

**First pass (2026-10-23), replaced.** A pit of 64 × 52 with a floor at 0, the Descent cut into its
wall and the scree. The owner's walk: you understand the place at once, and sculpting works well,
but it felt small and not dramatic, with no room for content down there and one walkable path to
the bottom. This revision followed.

**Second pass, data (2026-10-23).** Drawn again from the generated ground by a one-off script.
- **The rim:** 96 × 76 at 9-10, with the breaches at 8 (east) and 8.5 (west).
- **The shelf:** 16-20 tiles wide all round, sloping from the rim to the lip at 5.
- **The mouth:** 64 × 48, the lip falling in a cliff to -4. Tiles `v` (`LandShaft`, dark, blocked)
  stand in for the glow and the mist.
- **The crest's line across the shelf:** the north wall, and the south ridge with the gate's gap
  at (-179, 256).
- **Kept:** the south rim as rock, the outside falling to 0 at the area's edges, and the spring.
- **The layout follows** (`continent.json`): the scar's ellipse is now 48 × 38, and the pass line
  runs through the gate, so `BlockoutTests` walk through it.
- **Walked by a script:** every walkable tile in the five regions can be reached from p-dry-north.
  With the gate shut, the west shelf, the west breach and the frontier side can't.

**What to judge in the Editor:**
- the size from the east breach;
- the shelf's width and slope;
- whether the mouth reads as a hole without a bottom, even before the glow;
- the two rock lines across the shelf.

**Third pass, data (2026-10-23), after the owner's second walk.**
- **The shelf** is halved to about 8 tiles, and the mouth grows to 80 × 62.
- **The lip** stands at 7, so the narrower shelf stays gently walkable, and falls 11 tiles to the
  shaft.
- **A new kind `l`** (the lip): scar ground whose cliff faces are moonlight blue. The shaft `v`
  has the same faces. This uses the new `cliff` field on a legend entry: a kind's own colour for
  its cliff faces, where the default is the ground's rock.
- **The gate** moves to (-179, 253).
- **Walked by a script** again: everything can be reached, and the gate shut still cuts off the
  frontier side.

**Fourth pass, colours (2026-10-23), after the owner's third walk.** The mouth's size is right.
The colours were wrong:
- **Purple walls:** a mistake. `LandMoonFace` was named as the cliff colour but not added to the
  regions' materials, so it was drawn magenta. The compiler now checks `cliff` names as it checks
  `material` (a test covers it).
- **Green bottom:** the Game scene's unbuilt land, a green plane at -0.3 under the whole world,
  covered the shaft's bottom at -4. The shaft now stops at -0.2, more than 7 tiles below the lip,
  and its bottom is moonlight blue as well: a stand-in for the glow and the mist.
- **Dark blue ground:** the shelf's `Slate` is now a pale `Stone`, so the blue in the hole stands
  out.

