# Character models

`build_humanoid.py` turns `source/human.blend` (Clint Bellanger's base human, CC BY 3.0; see
`CREDITS.md`) into the game's modular player body and exports it to
`client/Assets/Resources/Characters/HumanMale.fbx`. It also saves `HumanMale.blend` here
(git-ignored) to look at or hand-edit.

Run it in Blender 5.x: through the Blender MCP (exec the file), or
`blender --background --python tools/character-models/build_humanoid.py`.

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
