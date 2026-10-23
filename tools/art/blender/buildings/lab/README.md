# Paint lab

A place to try ways of painting building pieces, kept apart from the reference kit
(`../venaria.py`, the plain village, left as it is for comparison).

- `paint_textures.py` paints the lab's textures into `textures/`: several palette colours per
  surface, one texture per whole piece (a wall tile, a roof piece's run), roof courses that
  continue from eave to ridge. Standard library; run it after changing the painting.
- `paint_lab.py` (run in Blender) builds small blocks from the kit's own pieces, bland on the left
  and painted on the right, thatch in front and slate behind.

Nothing here reaches the game. When a style is chosen it moves into the kit and the client.
