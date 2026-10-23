# Paint lab

A place to try ways of painting building pieces, kept apart from the reference kit
(`../venaria.py`, the plain village, left as it is for comparison).

- `paint_textures.py` paints the lab's textures into `textures/`.
- `paint_lab.py` (run in Blender) builds one shell from the kit's own pieces: the bland reference
  in front, and the same shell in three themes.

## What the references taught (images/rs3-*-exterior)

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

Nothing here reaches the game. When a style is chosen it moves into the kit and the client.
