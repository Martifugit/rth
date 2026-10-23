# Cruor: architecture style

The way Cruor's buildings are made, learnt from the great church (variant B in
`tools/art/blender/buildings/lab/monumental.py`, `plain_church`), the first monumental piece the
owner passed (2026-10-23). It was restyled after the measurements of Varrock castle
([research](../research/osrs-varrock-castle.md)); the rules below are what made it work. The city's
civic and military buildings (the list in [cruor-assets.md](cruor-assets.md)) follow them too.

## Decided by the owner

- **The cathedral is massive and intimidating**, the one building that dominates the city. It
  breaks the usual height limits on purpose (nave walls about 22 tiles, towers about 42 with their
  spires above). Civic buildings keep **ordinary sizes**: the halls, palace, towers and gates in
  the asset list. The city plan makes room for the cathedral, not the reverse.
- **Pale stone, red for the faith.** Walls are pale grey stone (the first church's shade on the small bricks). Red is kept for what must stand
  out and belongs to the empire's religion and to blood: the cathedral's glass first (the great
  window, every lancet), then banners and the red runner. Red is not used on structure, trims or
  roofs.

## The rules (from B)

1. **One stone.** Every stone surface wears the same texture (`CruorStone`, from
   `lab/church_textures.py`):
   - small bricks, about a quarter tile tall and half a tile long;
   - dark mortar outlines, light edges, strong contrast between bricks;
   - a recolour of the reference castle's idea, not a copy.

   Walls carry no painted trims or string courses; they read as one material.
2. **Thin walls.** A wall is a skin, a quarter tile thick (the clerestory half a tile). Mass comes
   from height, buttresses and towers, not from thickness.
3. **One repeated silhouette element: the merlon.** One a tile, half a tile long, as wide as the
   wall, along every wall top. The walls that are cut in the inside view get the same merlons, so
   a cut wall still looks finished.
4. **One pinnacle.** A square pyramid five half-widths tall. The same shape stands on every
   buttress, tower corner, gable peak and portal side. There are no other spikes: no crockets,
   gablets or spikes of mixed sizes.
5. **Small windows.**
   - lancets about a tile wide in a thin raised frame;
   - dark slits in the towers;
   - **one** big statement per building, at most (the cathedral's great window with its rose).
6. **Towers are chamfered**, square on the side they join and cut at the outer corners, so they
   read round from the camera. They end in battlements with a pinnacle at each corner, then (for
   the important ones) an octagonal belfry and a slate spire.
7. **Gables are the roof's own ends.** A gable wall follows its roof's eaves and pitch exactly. A
   screen never rises past the roof behind it.
8. **Dark slate roofs** in neat rows, plain, behind the merlons (no eaves overhang on stone
   buildings).
9. **Smooth lighting** across shallow edges (the reference's `sharelight`): octagons, apses and
   piers light softly; walls and corners stay crisp.
10. **Fine detail and colour go into dressing**: banners, braziers, lamps, pews, candles. These
    are separate models within the prop budgets, placed on and around the structure, not built
    into the shell.

## Inside

Buildings you walk into are layered (see `Church` in `monumental.py`):
- **Base:** always shown, the floor and one storey of every wall.
- **Wall tops:** per side.
- **Upper parts and roofs:** hidden inside.

Inside, the far walls stand to full height as the backdrop, and the walls toward the camera are
cut at one storey under their merlons, so nothing stands between the camera and the player.

## The kit (to make)

The parts B is built from, to be made reusable at any length and height:

| Part | Used for |
| --- | --- |
| Wall skin with merlons, lancets, plinth | Barracks, armoury, chapter house, hospice, letting house, palace, warehouses |
| Buttress with the pinnacle (with or without a flying arm) | Cathedral, chapter house, palace |
| Chamfered tower, battlemented, with slits | Wall, corner, beacon and palace towers, conduit house |
| Belfry and spire | Corner, palace and gatehouse spires, the cathedral's towers |
| Gable end with a window | Every hall's end wall, transepts |
| Portal, and doorways framed in dressed stone | Gatehouse arch, hall doors, the cathedral |
| City wall (thick, walkable, merlons on both sides) | Curtain wall, yard wall, water gate |
| Slate roof (pitched or lean-to) | Everything roofed |

Townhouses, the tavern and the stables keep the village kit's plaster and timber, with the same
limestone and slate, so the city still reads as one place.
