# Camera

*Draft (2026-11-03). From the owner's decisions on scale and camera (`docs/world-scale.md`).*

The camera looks down on the player from above and behind, as in RuneScape, and turns round them. Nothing in
the world fully blocks it: what stands between the camera and the player fades smoothly, and roofs hide
when you go inside. It rises with the ground. When a trophy fish bites, it swings round behind you.

## Open questions

1. The detailed camera rules live in code (`CameraRules`); they are not designed here yet.
