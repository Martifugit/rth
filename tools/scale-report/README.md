# Scale report

`python3 tools/scale-report/scale_report.py` measures the world's scale from the committed
scenes and assets (no Unity needed) and prints the tables behind
[`docs/research/scale-today.md`](../../docs/research/scale-today.md). `--json` gives the raw numbers.

It understands Unity's built-in primitive meshes (what `WorldKit` builds with), binary FBX for the
character, PNG headers for textures, and the camera defaults in `CameraRig.cs`. When scenes start
using imported meshes, use **R'th > Art > Validate Art** in the Editor for their triangle counts.
