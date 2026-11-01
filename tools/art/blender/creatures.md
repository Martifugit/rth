# Growing a creature

How to build a creature in code, learned on the dragon (`assets/creatures/common/dragon.py`,
November 2026) after the wolf, goblin and Cerberus rounds. The dragon took about twenty rounds with
the owner; nearly every one corrected a mistake that the rules below now prevent. Read this before
starting any creature, and add to it when a round teaches something new.

The tools are in `artlib/grow.py`. The dragon is the worked example of every rule here.

## What failed (don't try these again)

- **Reducing high-poly models** (Quaternius, 2026-10-27): decimate, remesh and shrinkwrap gave soup.
- **A design proxy** (primitives placed by numbers, then a low-poly mesh fitted over them): misaligned
  pieces and boxy edges. The owner has never seen it work. Don't propose it.
- **Tubes of round sections on a skeleton** for mammals and humanoids (the creature study): they
  read only for animals that really are sticks and blobs (birds, arthropods).
- **Patching a construction instead of rebuilding it.** The wing wrist went through five fixes
  (grid caps, turning lofts, a strip for one finger, pushing rings out to a line). Each fixed the
  last one's side effect; together they gave slits, a mismatched finger and broken edge flow. The
  clean rebuild fixed all of it at once and was shorter.

## The order of work

1. **Feeling and proportions first, in numbers.** Size it against the player (1.8 m): the dragon's
   crown is at 1.8x the player and every other length follows from that. Write down what must read,
   in order (for the dragon: the wings, the keeled chest over a narrow waist, the S neck and wedge
   skull with swept horns, the long tail, the crouch).
2. **Grow one closed body mesh**, box-modelled in code: a body tube of rings, then the neck, head,
   tail and legs extruded out of its faces (`Grow.tube`, `Grow.extrude`). Review side, front,
   three-quarter and the game camera every round.
3. **Neutral pose and exact symmetry.** Model it straight (a tail running back along the centre line,
   no curl): the rig poses it. `bmesh.ops.symmetrize`, then keep only the +X half with a Mirror
   modifier (clip and merge on), so the owner can edit one side with the other following.
4. **Parts the owner can work on**: a root object and a few children, one per thing that is built
   differently (the dragon: the body, and the wing as bone plus membrane). Each part carries its own
   Mirror. The family exporter (`artlib/family.py`) exports a model with its child parts.
5. **Topology before paint.** One colour per part until the owner passes the shape. No markings.
6. **All quads, closed, no tangled edges.** Check every round with a script (faces per side count,
   edges with one face or more than two). Triangles come from pointed tips: end them blunt.
7. **Joints**: a ring either side of every joint that will bend, and bend-test it (below).
8. **The owner's hand edits are data.** Record them as offsets in `<creature>.tuning.json`, matched by
   position against a fresh build (never by vertex index: indices shuffle and the edits land on the
   wrong vertices), and have `build()` apply them. Save a copy of their scene first.

## Construction rules

**One clean construction per part.** Lay out every ring explicitly with the same corner order, so
nothing is re-sorted, fitted or twisted afterwards and the edge flow runs unbroken (`Grow.ring_at`,
`Grow.seam_ring`, `Grow.rings_of`). When a part needs a third fix, redesign how it's built instead
of adding a fourth.

**Everything that sticks out grows out of the surface it belongs to.** Horns, spines, claws and the
wing's thumb are extruded from a face (`Grow.inset` first shrinks it to a small square, so a thin
spike grows out of the surface instead of tenting it). Nothing is a separate piece set on or near the
surface; nothing hovers.

**Rings follow the real edge loop.** Every ring the kit makes is ordered along the region's actual
boundary, read in the ring's own plane, never by sorting corners by angle: an outline that isn't
convex (an L of three faces) sorted by angle crosses its own faces and splits the limb. `extrude` and
`split_cap` both work this way now (`Grow.boundary`).

**Sharp bends need room.** Where a leg folds hard (a crouched knee and hock), keep the segments long
and the limb thinner at the bend, so the rings on the inside of the bend never pass each other. Check
by script for faces of a limb crossing each other, not only by eye.

**Grow side parts on the left only** when the body is made symmetric afterwards (`beast.symmetric`
keeps the +X half): growing them on the right too is thrown away, and near the centre line it can alter
faces that the cut then splits into triangles.

**Limbs grow from edge-connected faces.** `Grow.patch` picks faces sharing edges; faces touching only
at a corner extrude as two prongs (a leg split to the foot). `Grow.extrude` refuses them.

**A big limb leaves the body through a long, low collar**, grown from a strip of several faces, and its
first joint sits out and up so the limb only ever climbs away. A joint ring that dips back toward the
body makes a crease.

**Ends.** A limb's end becomes digits by turning its cap into a row of quads (`Grow.split_cap`: three
toes, a claw grown out of the end of each). A pointed tip closes as a fan of triangles; `extrude(...,
blunt=size)` ends it on a tiny ring closed with quads instead.

## Hands, digits and joints

- **The hand is the limb changing shape.** The wing bone is one tube whose last rings blend, corner by
  corner, into a fan of one ring per digit at each digit's root, square to it (`Grow.fan_end`). Each
  corner travels in a straight line, so the hand's edges run straight from the forearm to the outer
  digits: the brace that stops a trailing digit looking as if it would snap falls out of this.
- **Roots fan round the joint**, each a fixed distance from the wrist toward its tip, like metacarpals.
- **A web face between neighbouring digits** (`Grow.fan_cap`): digits never share an edge, so there are
  no slits in the crotches.
- **Every digit is built the same way:** the same ring count, the same thickness and taper profile, and
  a straight run from its root to its tip. A digit that leaves at an angle must not kink back toward
  a line planned from the wrist.
- **Long levers are as stout as short ones**, and the wrist as thick as the hand it carries.
- **Bend-test every joint**: bend a copy with a smooth falloff across its rings, both ways (35° and
  25° for the wing wrist), and render square to the bend. Look for creases, pinches and folds.

## Webs and membranes

- **The membrane and the bones are one surface.** Every bone ring is a seam ring: a pair of corners
  just above and below the wing's plane on each side (`Grow.seam_ring`). The membrane's two skins grow
  from those seam rows (`Grow.open_seam` removes the bone faces between the pairs, `Grow.web` fills the
  panel). Nothing clips, floats or hangs loose, and the sheet bends with the bones.
- **Each panel is a quad patch spanned between its four sides** (a Coons patch): two seams, or a seam
  and a body line or free edge, with a start and an end. Free edges get a rim joining the skins.
- **Panels are laid out from where the bones really are**, never from points the bones were planned
  from. When a joint moves, the membrane follows it.
- **Sag** grows with distance from the bones (none on a bone), so the sheet never drops out of one.
- **Where a web meets the body**, its seams start at the limb's hidden root ring inside the body, and
  its line along the body starts right beside the limb's root and is sunk just under the skin. A first
  edge left in the air between the limb and the flank is a visible gap.
- **A wing must carry its weight:** about 12 m across for the dragon's 3.3 m height. A short span on a
  heavy body reads as wrong at once.

## Checking a round

- Render the side, front, three-quarter and game views, plus close-ups of every joint and attachment,
  including from below and straight down (gaps show as ground through the model).
- Render with a wireframe overlay to judge edge flow, not only the shaded result.
- Count by script: faces by side count, open and tangled edges, connected pieces per part.
- Keep the current player body (`Body_Male_new` in `tools/character-models/BodyReview.blend`) beside the
  creature for scale.
- Never run another creature's family script in the owner's open scene: running a family script
  resets the scene and shows only its own model. Build probes with `run_name="probe"` and call the
  builder directly.

## The grow kit at a glance

| Tool | What it does |
|---|---|
| `tube`, `extrude` | rings along a path; extrude a region of faces through rings (`tip`, `blunt`) |
| `patch`, `face`, `band` | pick edge-connected faces to grow from |
| `split_cap` | a limb's end as a row of quads (toes) |
| `inset` | shrink a face to a small square (a spike's base) |
| `ring_at`, `seam_ring`, `rings_of`, `ladder` | lay out a limb ring by ring; close an end with quads |
| `fan_end`, `fan_cap` | a hand: the limb's end as one ring per digit, with web faces between them |
| `open_seam`, `web` | a membrane or web grown from seams as a closed sheet of quads |

## Colours

Creature colours are untextured palette entries. These carry a texture in `surfaces.json` and must
not be used on a creature: Umber, Slate, DarkRock, Bloodstone, Granite, Stone, Linen, Plaster and
the woods (Oak, Yew, Timber, Planks, Boards, Fence, DeadWood, Driftwood, Thatch). Check the file
for the current list.

## Shared parts

`artlib/beast.py` holds what creatures share: `to_object` (a part with its Mirror), `keep_left`,
`symmetric`, `tuning`/`tune` (hand edits in `<creature>.tuning.json`), `foot` (three toes, claws) and
`spines` (blades along the top line). The dragon and the drake use them; reach for them first.

## Designing a family (learned on the drake)

- **Kin first, then two or three signatures.** A family keeps the parent's anatomy (for dragon-kin:
  the wedge skull with brow and deep jaw, dragon legs and claws, a ridge of blades) and changes only a
  few things that identify it at a glance (the drake: wingless and crouched like a stalking cat, a
  crown of swept-back spikes, a whip tail with blades at its end).
- **Too close or too far both fail.** The first drake was the dragon without wings; the second (barrel
  body, toad head, tusks) read as boar, frog and lizard.
- **Watch for traits that belong to another animal.** A barrel body with a thick tail, a frill and
  nose horns read as a dinosaur at once. Check the silhouette against what else it could be.
- **Ask the owner for the family sheet** (`images/dragon-families.jpg` for dragon-kin) and take the
  family's style and signatures from it, never its shapes.

## Mammals (first pass, the grey wolf)

`assets/creatures/common/canine.py`, built in three rounds, all quads and closed from the first. A breed is
only a spec of numbers for one shared builder (StrayDog, Wolf): add a breed by adding a spec.
- **The masses that make a mammal, as numbers:** a level back (haunch top = withers), a deep but
  narrow keeled chest (0.3 m across, 0.4 m deep for a wolf), a tucked waist, a thick neck (wider than
  the head's back: a ruff) carrying the head just above the back, and a defined stop where the brow
  drops to a flat-topped muzzle.
- **Legs from four faces** (a 2x2 patch): the ankle's ring has ten corners, so `beast.paw` splits the
  paw into four toes. Toes are sized from their real spacing, so neighbours never touch.
- **Front leg:** the elbow tucked behind the chest, the forearm straight down. **Hind leg:** a big
  thigh, the stifle forward, the hock back, a long foot. Bend-tested both ways at the elbow and hock.
- **Keep the torso short.** The first round's body was a dachshund's; a wolf's nose-to-rump is about
  1.8x its withers.
- **Menace is posture and mass, not new parts:** the wolf differs from the stray only in numbers: a
  broader, deeper chest, hackles (taller rings over the neck and withers), the head carried low and
  level with the back, a broad skull and heavy blunt muzzle, ears pricked forward, thick legs on big
  paws, a thick tail held low and straight. The stray reads friendly because it is slender and carries
  its head up.
- **Hairpin folds** (a sitting hare's leg: forward to the stifle, back to the hock, forward again):
  a turn of about 150 degrees needs at least two rings round it, spaced more than the limb's radius,
  or the inside of the bend crosses itself. If the fold is too tight for that, change the
  construction: on the hare the thigh became part of the round haunch (a bigger torso ring) and the
  leg is only the shin and the long foot, one rounded hairpin at the hock.
- **`beast.quadruped(spec)`** builds any four-legged animal from a spec of numbers (the canines, the
  rat, the hare): a new animal is a new spec, checked by the crossing test, not new code.
- **Ears and growths** (`beast.grow_out`): an ear starts from a slim slot (`thin`), not a square
  inset, and its thickness is pinned front to back (`up`), so it reads as a flap. Tusks, antlers and
  tines are growths too, grown in order so a tine grows out of the beam before it. A growth that turns
  sharply needs a ring partway round the turn, or it crosses itself.
- **A leg leaves the body from the underside**, not the flank, when it folds close under the body:
  otherwise the first ring sweeps through the belly face beside it.
- **Hooves** are a foot type (`feet: "hoof"`): a dark block widening to a flat sole, closed with quads.
- **`beast.scaled(spec, k)`** makes a smaller animal from a larger one's spec (the fox from the dog).

## Bones: antlers, horns, tusks, spines, ribs (learned on the stag)

Everything bone-like is one tool, `beast.bone`, so a horn, a tine, a spine and a rib are built the same
way. The first antlers (one inset face, straight segments, tines pinned on) were square, kinked and
read as stuck on.
- **Root in a patch of faces, never one inset face:** two edge-joined faces give a six-sided bone,
  a 2x2 patch an eight-sided one. It is round, and it flares where it leaves the surface.
- **A low, wide collar just off the surface first** (a burr, a pedicle): without it the wall between the
  patch's outline and the first ring folds over.
- **It follows a curve** (`beast.curve`: three control points, rings evenly spaced by length) and tapers
  from root to tip (`ease` keeps it thick longer). No hand-placed rings, no kinks.
- **A branch is a bone grown out of its parent:** its root on the parent's surface at a fraction of the
  parent's curve, its patch laid along the parent (so it leaves round), its way bent away from the
  parent's line. A tine, a spine on a horn, a rib off a spine: the same call one level down.
- **A left-side bone never takes a face on the centre line** (`Grow.patch(..., allow=)`): the mirror
  would cut it into triangles.
- **Two growths never share a root area** (the stag's ear and antler fought for the same faces).

## Surface features: overlays (first pass, the drake's plating and the stag's pedicles)

Small or many features (plates, scutes, caps, armour, the root of an antler) are an **overlay part**:
their own closed pieces laid over the body, not geometry cut into it. Growing an antler or a crown out
of the body needs a dense patch of the body's faces exactly where it attaches, and painting showed the
body hasn't got it; an overlay needs nothing from the body.
- **`beast.plate`**: a grid of quads over a length and width of the body, each point cast onto the
  body's surface from its centre line (`axis_of`, or a list of (f, u) points), raised by a thickness
  shaped by `dome`, `ridge` (a keel) and `tilt` (the rear edge up, for shingles), its rim sunk under the
  skin and closed underneath. One closed piece of quads, no gap to the body. On the centre line only
  the left half is built and the part's Mirror closes it.
- **Shingles:** plates overlap their neighbour by design; the crossing check is per piece, so count
  crossings within one connected piece only.
- **Things that grow from an overlay grow from the plate, not the skin:** the stag's antler is a bone
  out of its pedicle cap (a plate in the fur's colour). Spines and horns that start under a plate punch
  through it (the drake's crown, still to fix).
- **The overlay is the second part** (`<Name>_Plates`, its own Mirror), built from the body before the
  body is cut in half (its BVH), like the wing.
- **Chunky, few, overlapping:** the reference look (OSRS's drake) is a handful of big domed plates in
  a darker bone colour, overlapping like shingles, not many small tiles.

## Plating study (2026-11-01, `boards/plating_study.py`, `artlib/plates.py`)

Six plated drakes, built in rounds, all quads, closed, no crossings. What they taught:
- **A plate is rings of one outline** (`plates.slab`): a rim sunk under the skin, a steep bevel up to
  the edge, a top rising to a crown, every point cast onto the body. Outline points: 14 (chunky
  facets); point 0 the front tip and n/2 the back, so a centre-line plate splits for the Mirror.
- **Shade plates nearly flat** (smooth angle 20): their facets catch the light like the reference's
  slabs. Smooth-shaded, the same plates read as rubber.
- **Outlines with intent** (`plates.outline`): pointed or round ends, `square`, `bulge`, and `lobes` (a
  symmetric scalloped edge) for broken, chunky slabs. No random jitter.
- **Faceted tops** (`facets`, `facet`): the top stepped into raised and sunken facets. With lobes, this
  is what moved the plates from "rubber tiles" toward the reference.
- **Features grow from a plate, out of a big inner ring** (`horn=`): the plate thickens into a horn, one
  surface. Started from a pinhole in the middle, a horn looks stuck on and folds. `r` sets the base
  radius for a boss on a big plate; `length`/`rake`/`spread` measure the horn from its own plate
  (absolute end points can land inside the plate and fold it).
- **Horn rings are spaced by arc length**, about half the base radius apart, the first ones keeping the
  base's outline before rounding off, the section frame carried ring to ring (it flipped otherwise).
- **Short features are profile, not horns:** a low knob (osteoderm) is the plate's profile rising
  (`plates.KNOB`); a horn shorter than its base folds.
- **Stacked plates** (`lift`): a boss on a saddle is a second plate lifted onto the first, its rim
  sunk into it.
- **What reads best:** few, large, faceted plates (F: a shoulder saddle and a hip saddle, neck and tail
  plates, lobed, faceted, a horned boss and a row of spike plates along the saddle's rear edge); then a
  ridge of keeled plates each thickening into a spike (B). Many small scales (C) read as texture at game
  distance, good for flanks, not the main read. A head crown of spike plates (D) is striking but heavy.

## Armour (capability tests, 2026-11-01: `boards/armour_tests.py`, `artlib/armour.py`)

Seven hard cases on the drake's bare body, each checked by `armour.report` (plate topology, crossings
within a plate, visible collisions between plates, floating plates, coverage by region).

| Test | Result | What it needed |
|---|---|---|
| T1 limbs (cuff, bracer, elbow/knee/hock caps, thigh plate) | pass, clean | an axis per leg; one straight local frame per plate |
| T2 wrap (neck collars, tail rings) | pass, clean | `band`: a closed ring of the profile; a lip for shingling |
| T3 flow (ridge down the curving neck and back) | pass (1 visible touch) | plates along the axis tangent; lip on keeled plates |
| T4 head (horned brow, cheek, jaw row with teeth, snout band) | pass (3 touches) | the spine axis through the head; plates at angles round it |
| T5 belly scutes between the legs | partial: reads right, some folds | radii limited and blended; still folds over the waist crease |
| T6 whole armour from rules | pass: cohesive | rows sized by girth, rows as wide as the arc between them, stagger, lips |
| T7 bent neck, tail and knees | pass: nothing floats or tears | `bend` moves body and plates together |

What the tests taught:
- **Plate a part from its own axis** (`armour.Axis`: a polyline with a carried frame; angle 0 up for the
  spine, forward for a leg). Projecting from the spine can't reach a leg, the jaw or the belly.
- **One straight local frame per plate** (`Frame.local`): casting each point from its own nearest axis
  segment folds a plate wherever the axis bends (every leg plate crossed itself until this).
- **Wrapping is a band, not a draped patch** (`armour.band`): rings of the profile round the axis, closed
  for a collar or tail ring, ends closed for a bracer or scute. Its radii are limited (a ray that slips
  past the body onto a leg is an outlier), smoothed round the band, and sampled only at its front and
  back and blended, so a crease under it can't fold it.
- **Shingles need a lip** (`lip`, `lip_back`, `lip_down`): the overlapping edge rests on the next plate.
  Without it every overlapping edge cuts down through the next plate. The lip lifts the whole back of
  the plate (raising only the rim folds it). Tail rings went from 23 visible collisions to none.
- **Cohesion comes from rules, not hand placement** (`armour.row`): each plate sized by the girth where
  it lies, overlapping the next by a quarter; flank rows each as wide as the arc to the next row, so they
  meet; alternate rows staggered half a plate; rows overlap downward (`lip_down`) like roof tiles. This
  turned F's scattered plates into armour (a pangolin's back).
- **Check what shows** (`visible_collisions`): shingles meeting under the skin are fine; only faces both
  outside the body count.

Still open:
- belly scutes over the waist and hip creases still fold a little (the body's own creases there);
- the thigh plate is cast partly onto the torso (the hind axis's first stretch runs inside the body);
- plates are one colour and similar in size; a design language (which plates are larger, where the
  armour stops, colour bands) is the next layer, built on these tools;
- joints aren't segmented: the plates bend with the skin; real armour would split at the joint.

## Large chunks (2026-11-01: `armour.chunk`, `boards/chunk_tests.py`)

One plate covering a large part of the body: a carapace from shoulders to hips and down both flanks, a
neck plate following its curve, a helm, long tail plates, limb guards.
- **Lay a big plate in the body's coordinates, not a flat plane** (`armour.chunk`): its outline runs in
  distance along the part's axis and angle round it, and every ring point is cast out from the axis, so
  it curls round the body however far it reaches. A flat slab the same size (C0) sits on the back as a
  stiff lid that can't come down the flanks.
- **Many rings** (`armour.CHUNK`, nine) keep a big plate's middle on the surface; few rings let the body
  push through its interior.
- **A chunk on a limb starts where the limb leaves the body.** Starting where the limb's axis still runs
  inside the torso folds it (101 crossings, then none).
- Results: carapace, neck, helm, tail plates and limb guards all clean; the whole armour in eight pieces.

Still open:
- **one chunk spanning two parts** (a pauldron from the back down onto the arm): a chunk follows one axis;
  spanning needs a blend between two axes across the plate;
- the chunk's middle closes as a fan of long faces (a visible star on the limb guards); a grid closing
  would read cleaner;
- a big plate is one surface: it can't bend at a joint (neck and tail plates are short for that reason).
