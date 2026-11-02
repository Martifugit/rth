# Character models

`build_humanoid.py` turns `source/human.blend` (Clint Bellanger's base human, CC BY 3.0; see
`CREDITS.md`) into the game's modular player body and exports it to
`client/Assets/Resources/Characters/HumanMale.fbx` (or `HumanFemale.fbx`). It also saves
`HumanMale.blend` / `HumanFemale.blend` here (git-ignored) to look at or hand-edit.

Run it in Blender 5.x: through the Blender MCP (exec the file), or
`blender --background --python tools/character-models/build_humanoid.py`. Set `SEX` (`"male"`,
the default, or `"female"`) and `PREVIEW` (build without exporting or saving) as globals before
running it; build both sexes after any change.

## The head

The source's head is not used: `make_head` lofts our own from `HEAD_GRID`, rows of points from
the neck's base to the top of the skull, each ten points from the front midline round to the
back, as shares of the head's height (chin 0, crown 1), mirrored to the left. Every column means
the same thing on every row (midline, side of the nose, eye, cheek, side, ear, back...), so a
feature is a few numbers. The rows slant: the jaw's edge runs from the chin up to its corner and
on to the nape, with a shelf under it, so a row's back sits higher than its front.

Features and proportions are kept apart:
- `HEAD_BASE` holds the features: the jaw's edge and the shelf under it, the nose between its base
  and bridge, the eyes set back under a brow, the lips. `female_base` is her variant (shallow
  sockets, a smooth brow, high cheekbones, a narrow nose, full lips, a rounder chin).
- `HEAD_SHAPE` holds each sex's proportions, measured off the owner's references in `images/`
  (`male-face-reference*.jpg` and `male-bust-reference.webp`; `female-face-reference*.jpeg`, whose
  `-2` has a front and a side view). `z` moves the base's heights so the chin, mouth, nose, eyes and
  brow sit where the reference has them. `profile` is the front view's outline (the face's and the
  head's half width) and the side view's (the midline's front and the back) at each height.
- `fit_head` scales every vertex, at its own height, from the base's outline to the profile, and
  adds a dome row so the skull is an egg rather than flat.

To change a head, measure the reference (silhouette widths per height, as shares of chin to
crown), edit the profile, rebuild, and lay the reference's outline over orthographic front and
side renders at the same scale. That overlay is the check: the first attempts, judged by eye,
came out alien (big dark eyes, a sunken mouth) and then boxy (proportions fitted by row ignored
the slant).

`HEAD_SHARP` names the edges the mesh is split along so the shading breaks there (the jaw line,
the brow, the nose); `EAR` is a wedge each side, close to the skull; `HEAD_EYES` places the dark
almonds and `HEAD_MOUTH` her small mouth (the "lips" channel). Short hair (`make_hair`) is the
same grid above `HAIRLINE`, cut to a clean ring and stood off the skull. Longer styles
(`HAIR_STYLES`, `HANG`; the female's chin-length `hair_bob`) hang straight down once past the
skull's widest point and are closed along their cut edge. Hair lines are given in HEAD_BASE's
heights, and `zmap` moves them with the face.

## The body

The body is the source's, re-rigged. Two corrections for the female's narrower shoulders:
`SHOULDER_DROP` brings her shoulder tops down at the joint, sloping them from the neck (the
source's run level and stand high, which reads as a box and pokes up a corner when the arms come
down); and the shirt's neck stub is deleted only where a face lies wholly within the neck
(`NECK_HALF`). Judging by the face's middle had deleted her shoulder tops too, leaving a hole each
side of the collar.

## Hair (`build_hair.py`)

Hair styles are data (`STYLES` in `build_hair.py`), built from each head's own mesh, so one style
fits both heads (they share the head grid) and follows any later hand tuning of a head. Twelve so
far (2026-10-28): crop, parted, long, ponytail, bun, swept, spiky, braids, bob, mohawk, buzz, shaggy.
Build one with `hair_review.py`, which also renders a six-view contact sheet of both bodies.

**What makes it read as hair, not headwear** (learned on a first long style that came out as a
coif; the owner's references are `images/hair-l-*`, `hair-m-*`, `hair-s-*`):
- A jagged hairline: the front edge is points (a saw-tooth fringe, a swept lock, a temple wedge),
  never a smooth curve round the face. A smooth, even edge is a hood's opening.
- A parting (a dip at the forehead the hair rolls away from) and uneven volume: full at the crown,
  thin at the parting and temples, lumpy along the top. An even offset from the skull is a cap.
- Locks, not sheets: long hair is separate tapered locks ending in points, with gaps where neck and
  shoulders show. One closed curtain with a boxed hem is cloth (the coif, kept as the start of
  headwear).
- Short cuts go round above the ear and leave a pointed sideburn in front of it.
- Every style must differ **from the front** (hairline, fringe, what frames the face), not only
  behind: the ponytail and bun first looked the same from the front.

**How it is built** (and the mistakes that taught it):
- Everything grows out of one surface. Locks grow from the shell's own rim edges (the edge's ends
  are the lock's root corners), and ties open a small seat face in the shell sized to the tie.
  Separate pieces laid on the hair showed their root faces; a seat as wide as the shell's faces
  made the gather far too broad.
- Locks start at the skull's widest ring at full thickness. Starting lower, under the shell's
  curve, made a ball of hair on a fringe of straight strands ("a squid").
- Fewer, wider locks that spread and overlap read as hair; many narrow ones read as spaghetti.
- Hair never clips the body: locks and tails drape under gravity, slide along the torso, arms and
  face where they touch, and every edge is cast against them afterwards (vertices alone are not
  enough: edges cut through the shoulder between two clear vertices). `HAIR_RESULT` reports any
  point still inside; it should be 0 at rest pose. Clipping while animating is checked with the rig.
- Asymmetric styles (a side parting, a swept fringe) turn X-mirror off; the rest keep it.

## Clothing (`build_wear.py`)

Garments start from the body mesh: copy the faces a garment covers, then shape them as cloth.
Settled with the owner (2026-10-28) on a loose t-shirt, straight trousers and simple boots
(`GARMENTS`; objects `wear_<name>`, ".001" for the female). Check them in motion with
`pose_review.py` (posed copies sharing the meshes) before calling a garment done.

**What makes it read as clothing, not paint:**
- Cloth does not follow the body except where it hangs from it (shoulders, collar, waistband).
  Below the chest a shirt is straight panels from the chest to the hem (`drop`), trouser legs fall
  from the thigh round the leg bones (`straight`), sleeves are loose tubes round the arm's bone
  (`loose`). Copying the body there showed its ribs, waist and bust through the cloth.
- Boots are a boot shape round the foot (a shaft, an instep sloping to a toe box, heel, welt, flat
  sole), not the foot's own mesh.
- Keep geometry to what the shape needs: flat faces are merged (`simplify`), but never a vertex
  shared between two bones (removing the knee's ring left faces from thigh to shin that tore).
- Below the chest nothing of the body's shape survives (the owner saw the abdominals and the
  seat in the shirts, tunic and robe): every `drop` ring is a convex outline (cloth spans the
  body's dents), and only the columns the outlines need are kept (`straight`, a column every ring
  runs straight past goes, with its mirror). No ring over the seat. A hem sealed onto the hips
  keeps every column, to meet the waistband below it.

**How it is built (and what went wrong first):**
- A garment replaces the body parts it covers (`hides`); nothing of the body stays underneath to
  poke through. Each opening is sealed back onto the body ring it came from, so with the parts
  hidden there is no hole, and its edges meet the head, arms and hands exactly.
- Every vertex keeps its body vertex's bone weights, so cloth moves with the body. In Blender 5
  the weights live with the vertex groups: make the groups, then add the weights (re-making the
  groups after writing the mesh wiped them all; `unweighted` in WEAR_RESULT must be 0).
- Shaping only ever moves cloth outward; a final pass (`contain`) pushes out any face a body point
  still pokes through (`poking` must be 0). Cut the body copy with a flat slice (bisect), not by
  dropping faces: hand edits leave faces that span a ring.
- Garments are rebuilt in place, so posed copies sharing the mesh follow. A garment name must
  never be a body part's (`wear_trousers` is the bare legs): the builder refuses.
- Below the chest a garment is rings at named heights (`drop.rings`: waist, hips, crotch, knee,
  ankle, with offsets): `cinch` (a belt, its faces in another channel with `band`), `blouse`
  (gathered above it), `hang` (as wide as anything of the body between it and the ring above:
  her seat bulges between the hips and the crotch). A skirt's hem stays open.
- **Nothing under clothes.** A robe hides the legs entirely (only the shoes show below it); a
  tunic hides them too and carries its own hose from a little above its hem down (a `legs` part
  with `below`, cut and capped on top). Long sleeves run to the wrist and hide the arms. Body
  left under cloth is exactly what comes through it; the owner ruled it out three times. Shoes
  are closed at the ankle for when no leg is there. (Short sleeves still leave the upper arm under
  them: give those the same treatment when they are next touched.)
- A waistband worn under a shirt rises a little inside it (`tuck`): the two only met edge to edge,
  and bending opened a hole between them.
- **Skirts hang from a skirt rig, not the legs** (`skirt`; `posing.py` has the rule). Plain
  skinning cannot keep a skirt over two legs (the front, blended between the thighs, stays halfway
  and a leg comes through), and with no legs under it the skirt must still move as if they were
  there. Game skirts are done with a skirt rig moved at runtime by leg colliders (VRChat
  PhysBones, Dynamic Bone, Godot spring-bone capsules): a ring of 12 two-segment chains on the
  hips (`skirt.<k>.1` from the hips to the knees' height, `.2` on to the ankles). Each frame, after
  the animation, every segment hangs straight down and swings out in its own vertical plane only
  as far as it must to clear capsules round the hidden thighs and shins; the cloth between two
  neighbours must clear them too; no chain hangs more than 35 degrees below a neighbour (taut
  cloth). The skirt is weighted onto the chains by angle round the body and by height. No pose
  is special: it held for kneeling and a kick that nothing was tuned on.
- What went wrong on the way (each is in the rule now): weights fitted to a set of training poses
  (`cloth_fit.py`, removed) only held near those poses; capsules from a leg's farthest point were
  fat and ballooned the skirt standing (now a typical radius, never so fat the chains do not hang
  straight at rest); chains hanging from the hip joints are level with a raised thigh's axis and
  cannot lie over it sitting (they hang from the hips); a trailing shin passed between two chains
  each clear on its own (the panels between chains are tested); chains need the body's widest
  outline below them, not the pelvis's, or the capsules shrink to fit.
- **The client drives the same chains**: `client/Assets/Scripts/Characters/SkirtRules.cs` (the
  rule as plain maths, tested by `tools/client-tests`) and `SkirtRig.cs` (the component, on the
  character root; its capsule radii are the rig's `skirt_capsules`). Change one, change both.
- **Armour is rigid plates** (`plates`, `build_wear.plates`): each plate a solid band round one bone,
  following it alone, so it never stretches (greaves, vambraces); a plate over a joint follows a
  half bone (`posing.HALVES`: turned half as far as the joint; the client's `HalfBones`), so it
  sits over the bend from either side (poleyns, couters, pauldrons). Joints are covered by
  plates overlapping, as real armour is, not by bending. A plate is cross-sections of what it is
  worn over (`over`), each ring taking everything up to its neighbours (a ring measured only at
  its own height let the calf through the straight panel between rings), convex, `sides` facets.
  Cut a plate back where a joint folds (`notch`: a greave behind the knee, a vambrace inside the
  elbow), or its edge digs in. A pauldron hinges above the shoulder (`PIVOT`), not at the joint.
  Not yet right: shoulder pieces are designed in the T-pose and float with the arms raised;
  lames (overlapping bands) made from T-pose cross-sections were worse. Next: design them in an
  arms-down pose and bind them back to the rest skeleton, as games author armour in an A-pose.
- **The first plate set** (`platebody`, `platelegs`, `knee_cops`, `plate_pauldrons`, `full_helm`,
  `plate_boots`; review outfit "plate"). The references (OSRS's smithed tiers, `images/osrs-armour-*`,
  for technique only) are enlarged body parts with flat faces plus oversized simple solids stuck
  on, one metal colour, recoloured per tier. So: body and legs are garments from the body (as the
  clothes); pauldrons, knee cops and the helm are **hull pieces** (`hulls`): the points of what they
  sit on round a joint, in a design pose (shoulders with the arms down), pushed out and stretched,
  wrapped in a convex hull with its flat faces merged, then bound back to the rest skeleton through
  the inverse of their bone's pose transform, rigid on it. Plates built as rings round a bone in
  the T-pose (the earlier `plates`) made thin fins and floating shells at the shoulders.
  Exaggerate: modest sizes read as nothing at game distance.
- A lined skirt (`lined`) has its inside as faces too, joined at the hem and closed across the
  hips, so nothing shows through from below.
- Slits (each half of a skirt on its own leg) were tried and dropped: they only hid legs left under
  the cloth, and fell apart sitting and crouching.
- Over-garments (`over`: the jerkin over the shirt) hide nothing and stay open at their edges.
- `contain` only pushes for points the garment covers: a point whose nearest cloth is at an
  opening lies beyond it (sleeves past a jerkin, legs below a hem). Pushing for those blew faces
  metres out, so each vertex may also move at most 3 cm in all.
- Layers must move together: where cloth lies close over something it takes that thing's
  weights (`transfer_weights`): an over-garment from the garment under it (`over`), a garment
  from one it hugs (`follow`), a collar from the neck. Sleeveless garments get deep armholes
  (`armhole`) so the arm comes down clear.
- Give a skirt rings at the legs' joint heights (the hip joint, the knee): the fit can only bend
  it where it has vertices.
- A boot's shaft is stiff: all shin (not the hose's part-thigh knee weights, which tilt its top),
  and shaped down the calf by a few rings cast onto the leg.
- Containment skips a point only when its nearest cloth is within a centimetre of an opening's
  edge; skipping every face that touches an opening left body uncovered.
- A garment never takes over an object of its name on another rig: `wear_tunic` was an older
  preview garment on the old bodies, and rebuilding a garment called `tunic` overwrote it (now
  `belted_tunic`; the builder refuses).
- Exec the scripts with `__file__` set to their path (`exec(compile(src, path, "exec"), {"__file__": path})`):
  they load their neighbours (`posing.py`: the poses and the skirt rig). Scripted helpers must
  only ever delete objects they made themselves, by reference, never by name pattern.
- Measure clipping in the poses with `clip_check.py` (after `pose_review.py`): points that come
  out through the layer covering them, points that sink into another mesh, and faces of one mesh
  passing through each other ("self"; the armpit with the arm down and the crease behind a deeply
  bent knee are there on the bare body too, and a skirt's lining crossing its outer cloth faces
  inward, so it is not drawn from outside). Run it on its own call after `pose_review.py`, once
  Blender has evaluated the posed copies. Seal lips take the colour of the edge they close. Rebuild the posed
  copies after rebuilding a garment: each copy keeps its own vertex-group list.
- Bending well is the body's job: graded joint rings (`joint_loops.py`: knees, elbows, hip, the
  spine on the hips, the seat). Garments inherit them.

## The contract with the client

- **Skeleton:** `hips > spine > chest > neck > head`, `upper_arm/forearm/hand.L|R` off the chest,
  `thigh/shin/foot/toe.L|R` off the hips; the new bodies also carry the skirt rig
  (`skirt.<k>.1` off the hips, `skirt.<k>.2` below each, k = 0..11; see Clothing), which the client
  drives itself (`SkirtRig`), and half bones `half.<bone>` for the shoulders, elbows and knees
  (`HalfBones`). T-pose rest. Unity maps these to its Humanoid bones in
  `client/Assets/Editor/CharacterImporter.cs`, so any humanoid animation retargets onto it.
- **Kit parts:** one skinned mesh each: `body_head`, `body_torso`, `body_arms`, `body_hands`,
  `body_legs`, `body_feet`, and hair styles `hair_<style>`. No mesh may share a bone's name (Unity's
  Humanoid mapping fails). Equipment hides kit parts it covers (by part name: `torso`, `arms`...).
- **The bare body (`build_body.py`):** the kit parts are a bare body in plain underclothes (shorts,
  and a band top on the female, channel `under`), the base every garment and piece of armour is made
  on. The default outfit is built over it as garments in the same FBX (`wear_shirt`,
  `wear_trousers`, `wear_boots`); `Resources/Characters/outfit.json` (written by the build) lists
  what each covers, and `CharacterModel.Wear` / `WearDefaultOutfit` put them on. The bare body sits
  a little inside the outfit everywhere, and the shirt's sleeve ends on the bare arm's own ring.
  `body_tuning.json` holds hand tuning per part (the outfit's tuning is under `wear_*`).
- **Colour channels:** each material is named after a channel (`skin`, `hair`, `primary`,
  `secondary`, `leather`, ...). The game colours channels, never textures, so one mesh has many
  colourways.

## Equipment art (later)

Make it in the built `HumanMale.blend`: model on top of the body, skin it to `Rig` (same bone
names), name its materials by channel, and export just that mesh (with the armature) as its own
FBX under `client/Assets/Resources/Characters/`. `CharacterModel.Attach` binds it to a player's
skeleton by bone name. Rigid things (weapons) need no skinning: attach them to a bone (`hand.R`).

## Animations

Humanoid clips go in `client/Assets/Resources/Characters/Animations/`, one per file, named by
what they are. Mixamo: *FBX for Unity*, *Without Skin*, 30 fps; leave *In Place* off for walk/run
so the game can match stride to speed.

| File | Used for | Without it |
|---|---|---|
| `idle`, `walk` | standing and walking | the built-in (code) character is used instead |
| `run` | running | walk, sped up |
| `idle_combat` | standing within 8 s of attacking or being hit | the relaxed idle |
| `cast` | attacks and abilities | no gesture |
| `hit` | being hit | a small code-driven stagger (usually better: Old School hits are tiny) |
| `death` | dying (holds its last frame) | no fall |

`client/Assets/Resources/Characters/animations.json` tunes each clip by name, all fields optional:
`weight` (0-1, how much of the clip shows; below 1 tones an exaggerated clip down), `speed`,
`start`/`end` (seconds, to trim), `upperBody` (legs keep walking or standing underneath),
`clip` (use another file, so one download serves several roles) and `pose` (hold the clip still
at that many seconds: the combat stance is the first frame of `slash_sword_shield`).

**`client/Assets/Resources/Characters/animations.json` is the catalog:** every clip file and role
is listed there with its `source` (e.g. which Mixamo animation), its intended `use`, and whether
it `loop`s (the importer reads that). Add an entry whenever you add a clip. Mixamo files may be
used in the game but not shared on their own, so keep this repository private.

## Hands and held things

The hands are closed into a grip in the model itself (the old finger bones are posed and baked
in; our skeleton has no finger bones, so animations never open them). `grip.L` and `grip.R` sit
in the middle of each fist. Held art (weapons, tools) is modelled as if held by the T-posed body:
origin at the grip, handle along +Z (forward). `CharacterModel.Attach(art, socket: "grip.R")`
holds it there.
