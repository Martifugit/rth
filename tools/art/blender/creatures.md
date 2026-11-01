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
