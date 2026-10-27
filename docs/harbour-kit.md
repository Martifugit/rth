# The harbour kit: design

**Status:** design written 2026-10-27; blockout passed for scale; first detail pass done (structures.py, vessels.py). Owner's decisions: generic kit first (each
port's identity later, as a set on top, the way the caves kit works); everything people stand on is
walkable (jetties, quays, stairs, decks); ships are walkable on their decks and below (a hold you go
down into); ships are moored set pieces, and travel is a voyage between twin ships at two ports.

**Ports it serves** (`docs/world-plan.md`): p-lakeport's quay on Knellmere (fishing boats, lake
traders), the empire's walled port at the great river's mouth, Sere's port and the ferry to the
islands, Cinderport on the black coast; river boats on the great river.

## 1. What the kit holds

**Waterfront structure** (all walkable, tile-aligned, built to be repeated):

| Part | Size | What it is |
| --- | --- | --- |
| Jetty span | 1 x 1 tile, any number in a row; 2-wide variant | Plank deck on a pair of piles per tile, a half tile above the water. End with a ladder down to the water. |
| Jetty head | 3 x 3 | A wider platform at a jetty's end, with a mooring post at each corner. |
| Jetty stairs | 1 x 2 | Steps from the bank down onto a jetty that sits lower than the bank. |
| Quay edge | 1 tile along the water, any length | Stone (or timber-faced) wall from the bed up to the ground, a coping along its top, bollards and rings; the ground behind it is ordinary ground. |
| Quay steps | 1 x 3 | Water stairs cut into the quay face, down to the water. |
| Slipway | 2 x 5 | A ramp of timbers down into the water, for hauling boats out. |
| Pontoon | 2 x 4 | A floating platform low on the water, a gangway hinged to the jetty. |
| Gangplank | 1 x 2-3 | A plank with cleats from the quay or jetty onto a ship's deck. |

**Waterfront dressing** (not walkable, blocking their tile where they stand): mooring post,
bollard, capstan, crane (a timber jib), net rack with drying nets, fish-gutting table, lobster/eel
pots stacked, coiled rope, cargo (crates, barrels, sacks reused from the nature kit), a harbour lamp,
a buoy, a channel marker post in the water.

**Boats and ships**:

| Vessel | Size (tiles) | Walkable | Use |
| --- | --- | --- | --- |
| Rowing boat | 1.5 x 3.5 | no (scenery; "board" for short crossings) | The ferryman's kind of boat; lakes and rivers. |
| Fishing boat | 2 x 5, one mast, a lug sail | no | Moored along jetties; drawn up on the shingle. |
| River barge | 3 x 9 | deck | Flat-bottomed, a steering oar, a deckhouse, cargo; the great river. |
| Sea ship | 6 x 20 | main deck, raised stern deck (plane 1), hold (own region) | A two-masted trader; the voyage ships. |

Every vessel also has a **wreck/drawn-up** look later if a place needs it, not now.

## 2. Walking on it: the walk layout

Water blocks its tiles, and nothing unblocks them; models placed as props have no colliders. So a
walkable structure carries a **walk layout**, written by the same Blender script that builds its
model, beside the FBX (`Resources/Harbour/<Name>.walk.json`):

- for each plane it uses: the tiles you can stand on (a grid of characters relative to the model's
  origin, as a region's `tiles` piece is written), and the height of the deck over each one;
- the tile edges it blocks (rails, gunwales, the quay's drop into the water);
- its ways between planes and places: stairs (a `climb` from a tile on one plane to a tile on
  another) and the hatch into the hold (a `climb` into the ship's hold region).

A region places the model as one piece (`"kind": "prop", "model": ".../ShipA", "walk": true`, a
quarter-turn rotation); the region compiler reads the layout, turns it, clears the water's block on
its tiles, sets them as ground on their planes and adds its walls and climbs. Unity's Sync World
gives the client a collider under each walkable tile at its deck height, so people stand on the
boards. One source for the look and the walking; nothing is kept in step by hand.

This is a new general mechanism (any walkable model: bridges, scaffolds, a siege tower), so it
comes with tests (`WalkLayoutTests`: a jetty over water is walkable, its rails block, a ship's
stairs and hatch climb).

## 3. How it should look

- **Scale:** RuneScape's compressed scale (`docs/world-scale.md`): a jetty deck half a tile above
  the water; a sea ship 6 tiles in the beam and 20 long, its main deck 2 above the water, the stern
  deck one storey (2.3) above that; masts about 12. Ships are the one waterfront thing allowed to be
  tall.
- **Construction reads:** piles under every jetty, planks across the run of the deck, a stringer
  along each side; quays battered (narrower at the top) with a pale coping. Timber is flat coloured
  and a little crooked, as in the nature kit; the deck texture carries the planks.
- **Ships are crafted, not jittered** (the altar's lesson): a clean, symmetric hull with a strong
  sheer (the deck line rising to bow and stern), a few broad strakes as painted bands, rails, a
  raised stern castle, masts and furled or set sails as simple big faces. Detail is in the
  silhouette and a few repeated elements (deadeyes along the rail, a row of gunports or shields).
- **Identity later:** colours of hull bands and sails, figureheads, the stone of the quays, lamps and
  banners come as each port's set (the empire's red and its sealed drop; Cinderport's black stone).

## 3a. Drawing a harbour

A whole waterfront is drawn as text, one character a tile, rows from the north
(`tools/art/blender/harbour/layout.py`): `S` stone (quay, pier, mole) at the quay's height, `J` a
timber jetty, `W` worn timber, `X` broken timber, arrows for stairs going down (timber from stone to
a jetty, stone steps from stone to the water), `b` bollards, `p` mooring posts, `l` lamps, `.` water.
The composer builds the masonry (battered only where it faces water, coping and buttresses along
those edges, corners resolved), the timber (piles round the outline and through the middle,
headstocks and joists, kerbs on open edges), the stairs and the dressing, and one walk layout for the
whole harbour (heights per tile, a block wherever two heights meet without stairs). Boats and ships
are placed on it as berths. `harbours.py` draws three: finger jetties off a quay, a mole closing a
basin, a ferry jetty zig-zagging out past a broken stretch. Any formation is a new drawing.

**Style:** the harbour wears chunky textures (`tools/art/surface_textures.py`: Boards, Blocks,
Flags, painted on a 32-cell grid and blown up four times) on the palette's Boards, Harbourstone and
Flagstone; posts, rails and trim are flat colours. The fine 128-pixel textures read too modern and
smooth next to the references.

## 3b. The gallery and styles

`tools/art/blender/harbour/gallery.py` builds the example harbours and, south of them, every kit as
its own labelled cluster (timber jetty, stone pier, worn and broken timber, waterfront dressing,
vessels), each built once per style in rows; in the Outliner, Gallery > kit > kit . style. A style
(`styles.py`) is a set of colour swaps that keep each texture's pattern, as objects.json `recolor` does
in the game: Plain, Weathered, Dark and Pale so far, starting points for each port's identity. A new
piece is a line in its kit's list in the gallery; a new style is an entry in `styles.py`, and every
kit appears in it.

## 3c. Wear, and joining piers

**Wear** (`common.WEAR`): every piece can be built with the flaws of use, the same each time (seeded
by its name), deliberate rather than jittered: a deck board missing, snapped and sagging, split off
short or patched with an older length (on the Boards texture's seams); a length of kerb gone; a
ladder rung gone; a stair tread missing or split; a coping block chipped with the chip lying by it, now
and then one gone and left askew; weeds in the paving's cracks; stones fallen at a wall's foot; a
fender gone; a step's nosing chipped; patched planks on hulls, a stretch of the ship's balustrade
stove in, a porthole smashed; a crate's slat gone, a pot crushed, a net torn. The gallery shows a
worn row per kit; the example harbours are built worn.

**Joining piers:** a pier head's back is finished either side of its neck (`stone_head(width,
neck)`), so a 4-wide head makes a T on a 2-wide pier; `stone_head(2, 2)` ends a narrow pier in its
own round nose; `stone_splay(2, 4)` widens a pier over two tiles into a 4-wide run and head.

## 3d. The showcase

`tools/art/blender/harbour/showcase.py` is the preview at the top of the gallery: one working
waterfront built from the kits. A long stone quay (a composer drawing) with bollards, lamps and water
stairs; the sea ship alongside with a gangplank, a crane and cargo (the nature kit's barrels, sacks and
crates, a cart); a stone mole closing the basin, carried on an arch, widening through a splay into the
lamp head at the mouth, channel markers outside; two timber jetties down stairs off the quay, an L
with a lamp and a pontoon, a T-head with mooring posts, the river boat, fishing and rowing boats
between them; past the quay's end a natural shore with a slipway cut into the bank, a boat drawn up,
nets drying, the old jetty (Weathered) rotting into the reeds, trees and a fingerpost behind. All worn.
In a drawing, `#` and `=` mark tiles a placed piece carries (an arch, splay, head, pontoon), so the
drawn stone and timber join them without walls between. `harbours.py` (three small examples) is kept
as a reference for drawings.

## 4. Order of work

1. **Blockout** in Blender (`tools/art/blender/harbour/`): every part at its final size, plain
   colours, with its walk layout written and drawn on the board (walkable tiles shown as a grid), the
   player for scale. Approve sizes and the walking.
2. **The walk mechanism** in `/shared` and the compiler (with tests), Sync World's colliders.
3. **Detail** the structures and boats; then the sea ship, with references side by side.
4. **The hold** as a small instanced region, the voyage (dialogue action moving the player between
   twin ships).

**References wanted:** RuneScape harbours and ships (Port Sarim, Catherby, Rimmington's jetty, the
Charter ships), in `images/`, to set the technique (as the trees' references did), never the
shapes.
