# Blender art

R'th's models built as Python scripts in Blender 5.x (through the Blender MCP or
`blender --python`), so each can be re-run, tweaked by a number and reviewed in a diff. The rules
the art must follow are in [docs/assets.md](../../../docs/assets.md). **Read the dos and don'ts
below before making anything**: they are what the owner has passed and rejected so far.

- `rth_art.py`: shared helpers.
  - **Materials:** palette colours (`mat("Stone")`, from `client/Assets/Resources/Art/palette.json`).
  - **Geometry:** `prism`, `box`, `loft`, `hull`, `decal`, `finish`.
  - **Preview:** a rig with tiled ground, the game's sun, a camera at the game's pitch and the
    player for scale; `figures` adds more players.
  - **Checks:** `report` (triangles and colours, against `client/Assets/Art/art-budgets.json`) and
    `floating_parts` (pieces hanging in the air).
- `props/well.py`: the first test prop, a village well (172 triangles, 4 colours).
- `buildings/venaria.py`: the village kit, the plain reference (cottage, longhouse, village chapel,
  townhouse).
- `buildings/cruor/`: **Cruor's architecture, the chosen style for the empire's capital.** The kit
  (`kit.py`), the cathedral built from it (`cathedral.py`), a scene with every part
  (`showcase.py`) and its textures (`textures.py`). Its rules are in
  [docs/regions/cruor-architecture.md](../../../docs/regions/cruor-architecture.md).
- `buildings/painting.py`: the pixel painters every building texture script uses.
- `buildings/lab/`: experiments and history (the paint lab, the first monumental passes). Not
  the chosen style; see its README for what each round taught and where it went.
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

**Buildings: what has passed**

Two kits so far. Start from the one that fits, and read its rules before adding to it:
- the village kit (`buildings/venaria.py`), for humble country buildings;
- Cruor's kit (`buildings/cruor/`, rules in `docs/regions/cruor-architecture.md`), for the
  capital's civic, military and religious buildings and its houses.

*Shape* (learned on both kits):
- **Do** split a realm's architecture into layers: the realm's shared language, each city's own
  twist, and the common village buildings. A village is low, thatched and humble; cut stone,
  storeys and leaded glass belong to towns.
- **Do** make the detail out of one repeated element and one ornament shape, not many kinds of
  small parts:
  - Cruor's are the merlon (one a tile, on every wall top) and one pinnacle.
  - The first great church's crockets, gablets, belfry spikes and spikes of several sizes read
    as noise.
- **Do** keep walls thin (a quarter tile): mass comes from height, buttresses and towers, not
  thickness.
- **Do** make a gable the end of its own roof (same eaves, same pitch). A screen standing taller
  and steeper than the roof behind it looks wrong.
- **Do** rest roofs on the walls, close every eave and corner, and look at a building from every
  side for gaps.
- **Do** seat every detail on what it stands on: a spike on a slope is cut to the slope, a
  pinnacle stands on a foot. Floating and half-sunk details were the commonest fault on the
  monumental pieces. Run `rth_art.floating_parts` on anything with many small parts.
- **Don't** add a chimney for looks: build it where the hearth is, and only when there is one.
- **Do** keep windows small, with at most one big statement per building (the cathedral's great
  window).

*Colour and painting*:
- **Do** paint the detail rather than model it: a few clean timber lines, plain roof slabs, and
  the texture carrying courses, slates and stones. Modelled thatch rolls, scalloped ridges and
  dense framing made buildings messy, not richer.
- **Do** keep surfaces clean: no damp, moss or grime. The village kit's first textures had
  weathering; the paint lab found that it reads as stains (`buildings/lab/README.md`).
- **Do** use one material for a whole building's stone. Richness comes from contrast *between*
  materials (pale stone, dark slate, red glass), not within one.
  - Roofs and plaster want close shades: strong variation turns a roof into a checkerboard.
  - A city's one stone can be a coarser, higher-contrast pattern (Cruor's small bricks, after
    Varrock castle's wall), because it has to carry the whole building alone.
- **Do** ration the faith's red: in the empire it is only for the faith and the regime (church
  glass, banners, the runner, the blood in a font), never on structure, trims or roofs.
- **Don't** colour doors, shutters and sills: plain wood and plaster read as one place.
- **Two texture systems.** The game today only has the first; the second waits on client work.
  - In the game: `tools/art/surface_textures.py` and `surfaces.json` make light grey patterns
    that the palette colour tints, so recolours keep their pattern.
  - Approved in Blender only: painted full-colour textures in a kit's own `textures/`, mapped per
    face by `paint()`.

*Scale and walking in* (learned on Cruor's cathedral):
- **Do** size a building from the floor a player walks on (nave and aisles about 40 tiles across
  for the cathedral), then the heights.
- **Do** keep ordinary buildings ordinary; only a centrepiece breaks the height limits, on
  purpose.
- **Do** build a walk-in building in layers (`Layered` in `cruor/kit.py`):
  - inside, the roofs and upper parts hide, and the far walls stand as the backdrop;
  - the walls toward the camera are cut at one storey under their merlons;
  - transparency alone is not enough.
- **Do** budget a big piece by surface, not just by count: Varrock castle's whole structure is
  about 25k triangles, the same as the cathedral (`docs/research/osrs-varrock-castle.md`).
  Fine detail and colour belong in dressing (banners, braziers, furniture), not in the shell.
- **Don't** run ahead: dressing, plants and extras come after the buildings themselves are right.

**Checking**
- **Do** render two views every round: the game view (pitch about 40°) and a low side view
  (pitch about 8°). The game view hides gaps between parts; the side view shows them.
- **Do** keep the player model in the preview (`figures` for more). Size is only judged against
  the player.
- **Do** run `report` and stay under the kind's target triangles and 2–4 colours.
- **Do** judge flat-coloured shapes with an outline-and-cavity render: Workbench, shading by
  material, cavity and outlines on. In flat colours every face toward the sun is one colour, and
  projecting parts vanish into the wall.
- **Do** check the game camera from inside a walk-in building and right beside its walls.
- **Do** leave the live viewport in Material Preview: the owner looks at it, not at your renders.

**Process**
- **Do** show the owner a first pass early, with your own critique, and ask concrete questions
  (size, style, what's wrong). The owner's eye catches what renders don't.
- **Don't** fix more than was asked in a feedback round. Offer further changes separately so
  each round's change is easy to judge.
- **Do** measure the reference like for like before restyling. Comparing the cathedral with
  Varrock castle in the 2004scape data (`tools/research/castle_*.py`) showed what was actually
  different (size, materials, where the detail goes) when guessing had not.
- **Do** offer variants side by side when the direction is unclear (the cathedral's A, B, C). The
  owner picks faster from a row than from descriptions.
- **Do** write rules down only once the owner has passed the thing they come from, and keep one
  canonical place for a style: the chosen kit, its rules doc, and saved reference renders
  (`docs/regions/img/`).
