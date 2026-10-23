# Building lab

Experiments, and the history of how the building styles were found. **Nothing here is the chosen
style.** What passed has moved out:
- the village kit is `../venaria.py`;
- Cruor's kit and cathedral are in `../cruor/`.

The lessons are in `tools/art/blender/README.md` ("Buildings: what has passed").

| Round | Files | What it taught | Where it went |
| --- | --- | --- | --- |
| Paint lab | `paint_lab.py`, `paint_textures.py` | One shell, many material themes; clean surfaces; contrast between materials; crisp trims (below). The owner: "I'd happily use these." | Painting rules in the README; the plaster in Cruor's houses. Not yet in the game (needs client work). |
| Monumental, first passes | `monumental.py` ("A"), `church_textures.py` | Plan a walk-in building from its floor; build it in layers for the inside view; seat details on their surfaces. Its many kinds of spike and red trims read as noise. | Layering and scale rules in the README |
| Restyle after Varrock castle | (B and C were in `monumental.py`; the research is `docs/research/osrs-varrock-castle.md`) | One stone, thin walls, one repeated element, one pinnacle, small windows, red only for the faith. The owner chose B. | `../cruor/cathedral.py`, `../cruor/kit.py`, `docs/regions/cruor-architecture.md` |
| A castle | `monumental.py` (`castle`) | First massing only, not reviewed yet | Cruor's kit has its own walls, towers and gatehouse |

## What the paint lab's references taught (images/rs3-*-exterior)

- **A theme is a set of materials over the same shell.** Timber and thatch; plaster, orange timber
  and red tile; dark stone and slate; pale stone and blue slate: one building, four looks.
- **Surfaces are clean.** An even colour with a fine, low-contrast pattern (small roof tiles in
  rows, big ashlar blocks, thin joints). No grime, damp or moss: those read as stains.
- **Close shades.** Tiles or blocks vary only a little from the material's middle colour; strong
  variation turns a roof into a checkerboard.
- **Contrast between materials, not within them:** pale wall, saturated timber, a dark or
  strongly coloured roof.
- **Crisp trims:** a dark ridge, a dark board down each gable edge, corner posts as quoins.
- **Paint per piece:** a wall tile's texture covers the tile, a roof piece's its run, so roof rows
  continue from eave to ridge.

The lab's textures in `textures/` are kept as they were when the owner reviewed them. Re-running
`paint_textures.py` now gives slightly different roofs, because a bug in `roof_rows` (a short last
tile merged into the wrong neighbour) has since been fixed in `../painting.py`.
