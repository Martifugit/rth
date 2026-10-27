# Tolling and Wennow: what the art kits cover

**Status:** inventory (2026-10-27). For the starter scenes in [tolling.md](tolling.md): what each place
needs, what the Blender kits already make, and what is missing, in art and in the pipeline that puts
art into a region. Nothing here is placed yet; the area is at stage 1 (ground).

Every kit was brought to the player's scale on 2026-10-27. He is 1.9 tall and 0.5 across the
shoulders, and the buildings now fit him:
- a storey is 2.6 in the village, Cruor and harbour kits;
- doors are 2.1 high and 0.84 to 0.9 wide;
- window sills sit near 1.0;
- field walls come to his waist.

The trees keep the reference's squat look (4 to 5.4).

## The kits

| Kit | Script | In the game? | What it makes |
| --- | --- | --- | --- |
| Nature | `tools/art/blender/nature/basin.py`, `mining.py`, `wilds.py` | yes, `Resources/Nature` (89 models) | trees (alder, ash, hawthorn, pollard willow, birch, elm, pine, snag) and their stumps; bushes and berry bushes; reeds, lily pads, tussocks, dock, bracken, heather, gorse, thistles, wildflowers, flax, herbs, mushrooms; boulders, scree, cairn, standing stones; buck fence, hurdles, dry-stone wall and stile, hedge, field gate; graves; hay stook, farm cart, scarecrow, skeps, trough, eel traps, woodpile, chopping block, campfire, lean-to; punt, a timber jetty span, mooring post, stepping stones, footbridge; fingerpost, milestone; barrel, crate, sacks; the mining nodes (three kinds, two variants each, depleted) |
| Caves | `tools/art/blender/caves/entrances.py` | yes, `Resources/Caves` | cave and dungeon mouths dressed seven ways (plain, mine, overgrown, shrine, lair, the makers', a sealed crypt), ways down (pit, sinkhole, fissure, shaft, stairwell, squeeze, sump, burrow), doors, gates, rubble, a hide rack, candles, bones |
| Village (Venaria) | `tools/art/blender/buildings/venaria.py` | pieces only, `Art/Buildings/Venaria` | tile pieces for timber-framed and stone buildings: walls (plain, braced, shuttered, leaded, lancet), doorway and door, barn door, jettied storey, thatch and slate roofs, gables, chimneys, font, bellcote, banner, lantern, bench; assembled in Blender into a cottage, a longhouse, a village chapel and a townhouse |
| Cruor | `tools/art/blender/buildings/cruor/` | no | the capital's stone architecture: walls, towers with belfries and spires, halls, a gatehouse, townhouses; the cathedral and the altar |
| Harbour | `tools/art/blender/harbour/` | no | timber jetties, stairs, pontoon, gangplank, slipway; stone quays and piers; worn and broken timber; bollards, capstan, crane, net rack, lamps, markers, fish crates, lobster pots; rowing boat, fishing boat, river barge, a walkable ship |
| Creatures | `tools/art/blender/creatures/` | the fen rat | |
| Characters | `tools/character-models/` | a male and a female body, hair | townsfolk wear the player's body for now |

## The places

Kit: what exists. **Missing** is art still to make. Pieces marked *(V)* can be put together from the
village kit's pieces without new art.

### Tolling, the town on the knoll

| Needs | Kit | Missing |
| --- | --- | --- |
| The chapel | village chapel *(V)*, font, banner | |
| The bell tower, the landmark seen across the basin | | **a village bell tower**: stone, three storeys, an open belfry with the bell. The chapel's bellcote is too small, and Cruor's towers belong to the capital |
| The dead-house under the tower, where every player first wakes | a small stone building *(V)* | **a bier**, shrouds, candles (the caves kit has candles) |
| The churchyard | graves, old graves, a dry-stone wall, a hedge | **a lychgate** |
| The church's strongbox (the bank) and Clerk Ambrose | | **a strongbox**, a counter, a ledger on a lectern |
| Corvin's apothecary, with its back room and still | townhouse *(V)* | **shelves of jars, a counter, a still**, a hanging shop sign |
| Wenna's skinning yard | hurdles, a hide rack (caves kit), a trough, barrels | **a skinning rack and a lye vat** (both are stations), drying hides on a frame, a lean-to shed |
| Houses: about 8 to 12 for a town this size | cottage, longhouse and townhouse *(V)* | more variety: a two-storey house and a house end-on to the street, for the same pieces to read as a street |
| The square and streets | barrels, crates, sacks, a cart, a bench, a lantern | **a well**, a notice board, a market stall or two |
| The bridge where Palmerway crosses the river | a footbridge (too narrow for a road) | **a road bridge**: stone, one or two arches, cart-wide |
| The river through the town and Edda's pond | reeds, lily pads, pollard willows, stepping stones | |
| Interiors | a bench | **furniture**: a table, a bed, a chest, a hearth, a stool, shelves. Interiors today are `WorldKit` boxes |
| The first rocks and trees (copper 5, ironstone 3, alder 6, ash 3) | the mining nodes, alder, ash and their felled states | |

### Widdermill, north on the river

| Needs | Kit | Missing |
| --- | --- | --- |
| The mill, its wheel turning against the river | | **a water mill**: a building *(V)* with a wheel-house, an undershot wheel turning in the game, a leat and a sluice |
| The millpond and race | reeds, lily pads, willows | a timber sluice gate |
| The miller's yard | sacks, a cart, a trough, a woodpile | millstones leaning against the wall |

### Palmerway, the road east along Wennow

| Needs | Kit | Missing |
| --- | --- | --- |
| The road, gravel and drifting | fingerpost, milestone, hedges, fences | |
| The old toll-house with no road to toll | a cottage *(V)* | **a toll bar** (a pole gate) lying in the verge |
| Cruor's monument and Brother Hollis | | **the monument with its four founts**. The old blockout's boxes are in git history |
| The stones on the heath (s-heath-ruin) | standing stones, a fallen one, the caves kit's makers' stonework | |
| The reed fens, where the fen rats are | reeds, tussocks, dock, eel traps, a punt | |
| The waygate, the wardens' gate | | **a gate on the road**: a timber gatehouse or a barred gate, a palisade that runs to water or rough ground, and the wardens' hut. Cruor's gatehouse is the capital's, too grand |

### Wennow's shore and the fishers

| Needs | Kit | Missing |
| --- | --- | --- |
| A fishers' landing on the mere, where the player was pulled out (s-wennow-south) | the harbour kit's timber jetty, rowing and fishing boats, net racks, fish crates, eel traps, a punt | **the harbour kit in the game** (see the pipeline), and a fishers' hut *(V)* |
| The fishing spot (when Fishing exists) | | a node look (ripples, a bobbing float) |

### The Sere road south: the farm and the dry rise

| Needs | Kit | Missing |
| --- | --- | --- |
| p-edge-farm, the last green farm | longhouse with byre *(V)*, field gate, walls, hedges, hay stooks, scarecrow, skeps, a cart | **a barn** (the longhouse's byre end is the start), crops in rows (only flax exists), a haystack |
| Field walls running out into the gravel | dry-stone wall and stile | **a broken wall span**, half buried |
| The gravel edge and the dry rise (s-dry-rise) | scree, boulders, a cairn, gorse, thistles | dry ground cover: dead grass, a dead bush, a wind-bent thorn |
| The first creature of the dry edge | | **its model** |

### West and north

| Needs | Kit | Missing |
| --- | --- | --- |
| s-tolling-cave, the first dungeon | the caves kit (a plain or overgrown mouth) | its inside is its own region, later |
| Hollow lane north | hedges, a fingerpost, field gates | |
| The observatory on the spine's spur (seen, not visited) | | a distant silhouette only, later |

### People and creatures

| Needs | Have | Missing |
| --- | --- | --- |
| Rats and mites in town | the fen rat | **the vitriol mite** |
| Townsfolk: clerk, apothecary, skinner, bell-ringer, fishers, miller, farmer, wardens | the player's bodies | **clothes that tell them apart**: robes, an apron, a smock, a warden's tabard, hoods and hats |

## The pipeline: what is missing to put the art into a region

What works today:
- A region's `prop`, `tree` or `rock` piece takes `"model": "Assets/Resources/Nature/Alder.fbx"`.
  Sync World stands it on the terrain, and its `footprint` blocks tiles.
- The models get their textures and colours from `NatureImporter`, fixed on 2026-10-27.
- Gathering nodes name their depleted model in objects.json.

What is missing, in order of need:

1. **Buildings.**
   - A region's `house` piece still draws the old `WorldKit` boxes.
   - The village kit's pieces are in Unity, but they can only be put together in Blender (`volume()`).
   - The general fix is a building piece in the region data: a footprint, its storeys and roof, and a
     line of pieces per wall face (as `volume()` takes now). Unity would assemble it from the kit's
     pieces, with walk-in collision, the door, the upper floors and a roof that hides.
   - One description serves every house, and it keeps the world as data.
   - The quicker fix: export each finished building as one model and let a `house` piece take a
     `model`.
2. **The harbour kit in the game.**
   - Export it.
   - Teach the compiler its walk layouts (`world/walk/<Name>.json`), and give the decks colliders, so
     jetties and boats can be walked on.
3. **Recolour per placement.** objects.json's `recolor` covers objects only. A plain piece needs it
   too (a dark fence, a weathered jetty, a stone house in another stone).
4. **Scatter for small things.** Tussocks, flowers, reeds and stones by the hundred are too many to
   write as pieces. Options:
   - a scatter layer painted like the Stones layer in **Shape and Paint Ground**;
   - hand placement under `Handmade`. Whether a placed model comes back through the hand-made
     capture still needs checking.
5. **Animation:** the mill's wheel, the bell, smoke and water at the sluice.

## Proposed order

1. **The building piece, and the village kit through it** (pipeline 1). The town is mostly
   buildings, and nothing else waits on so much.
2. **A kit test next to the spawn:**
   - two or three walk-in houses, a garden, fences and a path;
   - trees and undergrowth, a woodpile, a pond with a jetty;
   - walked in the game, to judge whether the kits make a convincing place before Tolling's layout
     is fixed.
3. **The missing landmarks:** the bell tower, the mill and its wheel, the road bridge, the waygate.
   They decide Tolling's layout (stage 2), so they come before the furniture.
4. **Stage 2 for the area:** the town, the routes and the landmarks placed (`docs/world-areas.md`).
5. **The rest of the missing art:**
   - stations: the skinning rack, the lye vat, the still, the strongbox;
   - furniture;
   - the harbour kit's walkable decks;
   - creatures and clothes.

   These come in the order stage 3 (people, objects, creatures) needs them.
