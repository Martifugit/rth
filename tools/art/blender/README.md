# Blender props

Props for R'th built as Python scripts in Blender 5.x (through the Blender MCP or
`blender --python`). Each prop is a script in `props/`, so it can be re-run, tweaked by a number
and reviewed in a diff. The rules the art must follow are in [docs/assets.md](../../../docs/assets.md).

- `rth_art.py`: shared helpers. Palette materials (`mat("Stone")`, colours from
  `client/Assets/Resources/Art/palette.json`), geometry (`prism`, `box`, `finish`), a preview rig
  (tiled ground, the game's sun, a camera at the game's pitch, the player model for scale) and
  `report` (triangles and colours, to check against `client/Assets/Art/art-budgets.json`).
- `props/well.py`: the first test prop, a village well (172 triangles, 4 colours).
- `creatures/fen_rat.py`: the fen rat (136 triangles, 3 colours), in the game.
- `items/held.py`: held weapons and tools. The smithed roster (all tier-1 copper, one metal
  material): knife, sword (a gladius), falcata, longsword (a claymore with a church hilt),
  warhammer, hatchet, pickaxe. Plus the athame and the penitent's mace. Origin at the grip,
  handle along -Y, blades in the vertical plane. A variant in another metal is `"recolor"` on
  the item in items.json, not a new model. `strip` builds curved and wavy blades.

Run a prop from the MCP:

```python
import runpy, sys; sys.path.insert(0, r"C:\dev\rth\tools\art\blender")
ns = runpy.run_path(r"C:\dev\rth\tools\art\blender\props\well.py"); result = ns["result"]
```

then `rth_art.render(path)` for the game view, or pass `focus`, `distance`, `pitch` and `yaw`
for another angle.

**Into the game:** `rth_art.export(objs, "Resources/Creatures/FenRat.fbx")` writes the FBX into the
client (the model faces Blender -Y, which becomes Unity +Z; its origin is the middle of its
tile). Name the NPC's model in `server/data/content/npcs.json` (`"model": "FenRat"`) and the
client's `CreatureBody` draws it, colouring each material by the palette colour it is named
after. `creatures/fen_rat.py` exports itself when run.

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

**Faces (learned on the player)**
- **Do** look at the reference image before starting, and match its amount of detail, not more.
  The reference player's face is a plain smooth head, a pointed chin and two thin dark strokes for
  eyes. No modelled nose, no ears, no mouth (the female has a tiny mouth mark).
- **Don't** give a person a creature's toolkit. Spikes for a nose, triangles for eyes and ears
  that stick out read as a monster. A sculpted, realistic face fails just as badly the other way.
- **Do** paint eye strokes with `decal`: a thin quad cast onto the face from the front, so it
  follows the surface. Inner ends a little lower than the outer ones.
- **Do** shade heads smooth. The reference's characters are lit softly; the facets come from the
  low triangle count.

**Weapons (learned on the smithed set)**
- **Do** start from real weapons of the age of blades: their shapes were tested in war and read
  instantly. Leave out what is iconic to another game (the scimitar).
- **Do** give every weapon in a set its own silhouette. A sword and a longsword that differ only
  in length read as the same weapon; a gladius and a claymore don't.
- **Do** show the owner a line-up of candidates beside the player and let them pick. Taste
  decides here: of eleven shapes, four were kept and one was asked for a second pass.
- **Do** tie a high-end piece to the world through its ornament, not its outline (the
  longsword's wheel-cross ring and quatrefoils for the church).

**Special weapons (learned on Sealbreaker)**
- **Do** push a special piece's outline to the extreme first (a split head, spikes, chain): a
  safe shape with carving on it reads as "a sledgehammer with a sign".
- **Do** build a special weapon as the pieces it drops as, and assemble it from them
  (`items/special.py`), so every piece is a real part and the drops show progress.
- **Do** cast anything that sits on a surface onto it (`decal`, links cast per piece), and let a
  mark that would hang off an edge be dropped rather than placed.
- **Do** give ornament contrast in brightness, not just in shape: a black iron boss on a black
  iron plate disappears. One warm accent metal (bronze) carries it.
- **Don't** repeat a striking idea four times (four glowing keyholes): one idea per piece, the
  rest plain.
- **Do** expect special weapons to need their own triangle budget (Sealbreaker is about 1 150);
  chains and rings are where triangles go.

**Buildings (learned on Venaria's village kit, `buildings/venaria.py`)**
- **Do** split a realm's architecture into layers: the realm's shared language, each city's own
  twist, and the common village buildings. A village is low, thatched and humble; two storeys,
  cut stone and much leaded glass belong to towns.
- **Do** let the faith show in rationed signatures, not everywhere: in the empire, red only where
  the faith or the regime is (church glass, banners, the blood in a font); leaded glass in
  growing amounts from poor house to chapel; the lancet (the wound's shape).
- **Do** give dressing rules for where it may go (a flower box under a window, a lantern by a
  door, firewood, benches and ivy on plain walls), so nothing covers a door or window.
- **Don't** add a chimney for looks: build it where the hearth is, and only when there is one.
- **Do** rest roofs on the walls' outer edge, and close every eave and corner (wall plates,
  fillers where a jetty opens). Look at a building from every side for gaps.
- **Do** bring buildings to life with weathering in the textures (damp along the foot of walls,
  moss on slate, worn thatch), thick rolled thatch, and dressing round every building.
  Clean, flat surfaces look raw.
- **Do** make surface textures light grey patterns that the palette colour tints
  (`tools/art/surface_textures.py`, `surfaces.json`), so recolours keep their pattern.

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
