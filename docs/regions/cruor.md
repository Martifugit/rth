# Cruor

The first see of Blood, the richest and most populous city in the basin (`docs/lore.md`), and the
biggest city in the game. **It is to be laid out again** (below). The old blockout was judged far
too dense: unpleasant to cross, and impossible to fill with content.

- **How its buildings look:** [cruor-architecture.md](cruor-architecture.md) (decided, with the kit
  and reference renders).
- **The layout:** not made yet. This page holds the direction and the budget to make it from.

![The kit at ordinary sizes, the cathedral behind](img/cruor-kit.png)

## Direction for the layout

Decided by the owner (2026-10-23):
- **The whole city is walled, and the wall is not a rectangle.** It follows the ground, the river
  and the bay.
- **The plaza and the cathedral are the centre.** The cathedral is massive and stands in open
  ground of its own; the city is arranged round it.
- **Lots of open space** gives the impression of a large place: wide streets, plazas, gardens, the
  quays and the Watch.
- **About 25–30 buildings and 80–100 people**, at Varrock's scale (a Paris- or London-sized capital
  in its lore, about 30 buildings and 100 people in the game). Homes are a handful, and most are a
  single storey. Every building earns its place with a shop, a service, a quest or a landmark.

The building budget, a working list:

| Kind | Count | Examples |
| --- | --- | --- |
| The cathedral | 1 | In its own open precinct |
| Faith and state | 3–4 | Chapter house, letting house, the Pontiff's palace |
| Military | 3–4 | Gatehouse, barracks, armoury (wall towers are part of the wall, not counted) |
| Services and shops | 8–10 | The Tithe counting-house (bank), tavern, a few shops, a smithy, the market (counted as one) |
| Harbour | 2–3 | A warehouse, the quay, the Watch |
| Homes | 6–8 | In two or three short rows, each able to hold a quest-giver or shop later |

## Mood

A late golden haze over pale stone and dark slate. Banners and braziers everywhere, always lit.
Every street runs down to the water, and the whole city faces the bay and what lies in it: the
Quaere, a dark shape on the horizon with a faint red seam, visible from the Watch. Cruor feels
rich, devout and proud, a little feverish. Red is for the faith (the cathedral's glass, banners,
the runner, the Letting House), gold for Cruor's chalice, dark cypresses for gardens and graves.

Region atmosphere (`MapArea.ownAtmosphere`): a warm sun (1, 0.82, 0.6) at 1.2, warm ambient,
fog (0.87, 0.73, 0.56) from 55 to 118 tiles. Music track `cruor` (silent until a track exists).

## Where it is

Tiles 384-527 x 40-175 (144 x 136, zone-aligned), well east of Palmerway. The land between is
not built yet, so for now reach it with the dev command `::tele cruor`. The west road enters at
(385, 114).

## Places (from the old blockout)

What the city should contain, kept from the old blockout as a list of content, not a layout. The
new layout picks from it within the budget.

| Place | What's there |
|---|---|
| West gate and pilgrims' camp | Tents outside the walls, a roadside needle (`cruorian_monument`), cypresses; the gatehouse with portcullis, braziers, banners. |
| The Processional Way | The red runner from the west gate over the Pilgrims' Bridge to the plaza, banner poles, knight statues, a proclamation board (`cruor_proclamation`). |
| The river | Runs through the city between stone quays; the Pilgrims' Bridge crosses it. |
| Plaza of the Keeping | The city's heart: the red runner, statues, braziers, the chapter house. |
| The First Monument | The blood needle with four founts (`monument_fount`) and glowing channels. |
| The Watch | A terrace over the bay with a rail (`watch_rail`, Keep-vigil) and two beacon towers, facing the Quaere. |
| The cathedral (the Vigil) | Pews, an altar (`vigil_altar`, pray), stained glass. Walk-in. Now the massive cathedral of the architecture doc. |
| The Letting House | Walk-in hall where the faithful give blood: six chairs (`letting_chair`), a cistern. Letting sisters. |
| Knights' yard | Dummies, weapon racks, a sparring ring, barracks, armoury, stables. Blood-knights. |
| Pontiff's palace | Towers, a walled garden with hedges, cypresses and a fountain. |
| The quays | Jetties, bollards, moored boats, cranes, a warehouse. Dockhands. |
| The bay road | The Tithe counting-house (walk-in, `synod_strongbox` bank and counter); the Concordian consulate with its observation tower (`concord_skyglass`). |
| The market | Stalls, a well, the tavern "the Opened Vein". |
| The lower town | A few houses, fish racks, street lamps, the shuttered apothecary (`cruor_shuttered_shop`). |
| The aqueduct | Piers and arches carrying the channel to a conduit house and fountain. |

The object ids above are in the content files and the current scene.

## The old blockout (history)

`client/Assets/Editor/CruorBuilder.cs` (**R'th > Dev > Rebuild Cruor Scene**) built the old
blockout into `Assets/Scenes/Cruor.unity`, which is still what the game loads, and wrote
[cruor-assets.md](cruor-assets.md). Both are superseded: don't extend the builder or build from
the list's counts (about 50 houses, 12 stalls). Rebuilding the scene rewrites the list without its
superseded banner.

![The old blockout from the bay](img/cruor-view-bay.png)
