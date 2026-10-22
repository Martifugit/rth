# Blender props

Props for R'th built as Python scripts in Blender 5.x (through the Blender MCP or
`blender --python`). Each prop is a script in `props/`, so it can be re-run, tweaked by a number
and reviewed in a diff. The rules the art must follow are in [docs/assets.md](../../../docs/assets.md).

- `rth_art.py`: shared helpers. Palette materials (`mat("Stone")`, colours from
  `client/Assets/Resources/Art/palette.json`), geometry (`prism`, `box`, `finish`), a preview rig
  (tiled ground, the game's sun, a camera at the game's pitch, the player model for scale) and
  `report` (triangles and colours, to check against `client/Assets/Art/art-budgets.json`).
- `props/well.py`: the first test prop, a village well (172 triangles, 4 colours).

Run a prop from the MCP:

```python
import runpy, sys; sys.path.insert(0, r"C:\dev\rth\tools\art\blender")
ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\props\well.py"); result = ns["result"]
```

then `rth_art.render(path)` for the game view, or pass `focus`, `distance`, `pitch` and `yaw`
for another angle.

## Dos and don'ts

Learned by making the well with the owner. Add to this list as more props are made.

**Construction**
- **Do** make every part rest on, hang from or join something. A roof sits on posts or a beam, a
  rope hangs from the axle. Floating parts are the first thing a viewer notices.
- **Do** derive heights from each other in the script (`beam_bottom = roof_under - beam_h`), not
  from separate constants. When one number changes, the parts still meet.
- **Do** centre hanging and symmetric details (rope, bucket, sign) on what carries them, unless
  there is a reason not to.

**Simplicity (learned on the fen rat)**
- **Do** draw the silhouette, not the anatomy. The reference's rat is one hunched wedge, four
  spikes, a stick tail and two ear spikes. Find the 3–5 shapes that say "rat" and stop there.
  Fewer parts also means fewer joints that can go wrong.
- **Do** make one continuous mesh for the main mass (head and body are one `loft`), so there is no
  seam or crack where a neck would join.
- **Do** bury what grows from the body: a leg's, ear's or tail's base sits well inside the body
  (`spike` leaves its base open for this). Nothing balances on a surface or touches it at a point.
- **Do** paint small details onto faces (`paint`, with `size` to make the mark smaller than the
  face): an eye is one dark triangle on the head, not a bead stuck to it.
- **Don't** add feet, toes, teeth, whiskers or separate paws. At game distance they vanish, and up
  close they are where the mistakes show.
- **Don't** go for realistic colour. Bare skin in a realistic pink, and strong contrast between
  parts, look like plastic or playdough. Use dull, close colours (dark grey fur, dull brown tail)
  and let the shape carry it.
- **Do** start well under budget. The first rat was 378 triangles and worse; the second is 136.

**Checking**
- **Do** render two views every round: the game view (pitch about 40°) and a low side view
  (pitch about 8°). The game view hides gaps between parts; the side view shows them.
- **Do** keep the player model in the preview. Size is only judged against the player.
- **Do** run `report` and stay under the kind's target triangles and 2–4 colours.

**Process**
- **Do** show the owner a first pass early, with your own critique, and ask concrete questions
  (size, style, what's wrong). The owner's eye catches what renders don't.
- **Don't** fix more than was asked in a feedback round. Offer further changes separately so
  each round's change is easy to judge.
