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

## The contract with the client

- **Skeleton:** `hips > spine > chest > neck > head`, `upper_arm/forearm/hand.L|R` off the chest,
  `thigh/shin/foot/toe.L|R` off the hips. T-pose rest. Unity maps these to its Humanoid bones in
  `client/Assets/Editor/CharacterImporter.cs`, so any humanoid animation retargets onto it.
- **Kit parts:** one skinned mesh each: `body_head`, `body_torso`, `body_arms`, `body_hands`,
  `body_legs`, `body_feet`, and hair styles `hair_<style>`. No mesh may share a bone's name (Unity's
  Humanoid mapping fails). Equipment hides kit parts it covers (by part name: `torso`, `arms`...).
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
