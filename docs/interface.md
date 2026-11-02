# Interface

The world looks like Old School RuneScape; the interface is a modern dark-fantasy RPG interface
(the designer's RS3 screenshots in `images/` are references, not templates). The game has more
combat resources than either RuneScape, so it has more interface than OSRS. This file lists
every piece and how they are organised.

## Look (decided)

- **Dark fantasy, not retro:** near-black charcoal-brown panels inside thin bronze frames with
  small gold corner accents, recessed wells for slots and lists, raised cards, gold primary
  buttons. Theme: `client/Assets/Resources/UI/Rth.uss`; textures in `Resources/UI/Skin`, drawn
  by `tools/ui-skin/make_skin.py`. Every surface uses the same style (the chat included).
- **Fonts:** Cinzel (titles, headings, tabs: all capitals, letter-spaced) and Alegreya Sans
  (everything you read), both SIL Open Font License. Small text stays readable.
- **Icons:** silhouettes from game-icons.net (CC BY 3.0, see `CREDITS.md`), tinted in code: an
  item in its own colour, an ability by its bile pole, interface icons in gold.
  `tools/icons/icons.json` maps our ids to them. A PNG at `Resources/Icons/<id>.png` is the
  icon, so real art replaces them one at a time.
- **Resources** are shown the modern way: an icon, the numbers above, a slim bar beneath.

## Window kinds

| Kind | What it is | Examples |
|---|---|---|
| **Panel** (`UiPanel`) | Lives in a movable window; can share it as a tab. Resizable unless it has a fixed size. `MinSize` is its content's minimum: the window adds its frame. | Pack, worn, abilities, hymns, skills, quests, task, XP tracker, chat |
| **Fixed panel** (`FixedSize`) | Movable, never resized: the size it has to be (the whole window). | Main action bar, minimap, target, buffs, bank, shop, conversation |
| **Shaped panel** (`AllowedSizes`) | Resized between a few sizes only (the window snaps to the nearest), laying itself out to each. | Secondary action bars |
| **Server window** (`ServerOpen`) | Opens and closes when the server says; closing it tells the server. | Bank, shop, conversation (the ferryman's too: death has no window of its own) |
| **Modal** (`UiModal`) | Centred, fixed size, tabs of related pages, one at a time; Escape or a click outside closes it. | Journal, guide, world map, settings, character |
| **Overlay** (`UiRoot.RegisterOverlay`) | Drawn by its own code outside the windows; the player places it while editing the layout, and the code reads `OverlayRect`. | The trophy fishing gauge |

## Always on screen

| Piece | Contents |
|---|---|
| **Main action bar** ✓ | Blood, phlegm and breath as three equal gauges; the bile axis (two halves filling outward from a black centre that holds the value); 14 slots on keys 1 to 0, - and =. Fixed size: 580 px wide, slots 38 px at a 40 px pitch (`ActionBarRow.Pitch`), so every bar lines up with it slot for slot. Devotion (hymns) replaced breath. **To change:** the style meter shown may follow the weapon you hold. |
| **Secondary action bars** ✓ | Up to four more bars of 14 slots (bars 2 to 5, from the window menu), no keys. Each is a full row as wide as the main bar (to snap flush under or over it), or resized to exactly half that width in two rows of seven, or two columns of seven standing tall (`AllowedSizes`); the slots are laid out, centred, to whichever size the window has. Abilities and items are dragged onto any bar; layouts are kept on the server (client state `bars`; a layout saved with two secondaries loads with the new bars empty). |
| **Minimap** ✓ | Round, in a bronze bezel with a gold crest (`Skin/minimap-bezel`); it turns with the camera, click to walk. The compass (click to face north) and the clock (the real local time: a day in R'th is a real day) sit in medallions on the ring's upper corners, the region's name low on the map. A red flag marks where a click sent you (on the map or in the world) until you get there or stop; the world map's marker shows as a small gold diamond. While either is further off than the map shows, an arrow at the edge points its way, slowly fading in and out (gold for the marker, red for the flag). Six round buttons sit in a cradle around the bottom of the ring: run (remembered between sessions, client state `run`), world map, "Journal (J)", "Guides (G)", settings and a menu of every window. Shown at 270 px: its geometry is shared by `MinimapPanel.cs` and `make_skin.py` in 240-pixel design units, which the panel multiplies by `MinimapPanel.Scale`, so the bezel and buttons stretch evenly. No blood orb: blood is on the action bar. |
| **Chat** ✓ | Filters (all, game, public) and the line you type on (Enter). Resizable (at least 260 x 120) and can share a window as a tab; the lines scroll and fill whatever size it has. Private and group chat to come. |
| **Target** ✓ | What you are fighting, and its health. Kept while you walk away in combat. |
| **Buffs / debuffs** ✓ | Two fixed rows of status icons above the action bar, each half its width (bile's four statuses, wards from abilities), each with its % or time left; hover for what it does. |
| **XP drops** ✓ | No surface, only the numbers: each XP gain rises as the skill's icon and "+123" and fades (about 2 s); gains landing together stack, a level-up lingers with "Level N" and a warm glow. Frameless and fixed size (move it by its handle, which also outlines its reach); left of the minimap by default; close it from the window menu. Catches no clicks. |
| **Side window** | Tabs for the panels below. |

## Side window tabs

- **Pack** (28 slots; drag to move, onto a bar, onto the bank). Standard slots, as many columns
  as the window is wide and centred; the smallest window is the classic 4 x 7, so it never scrolls. ✓
- **Worn equipment** ✓: the figure (standard slots, centred; it no longer grows with the window) and, under it, a row of icon
  buttons (`WornPanel.Buttons`, a list: add one for a new view of your gear, such as items kept on
  death or a price check): **combat stats** and **inspect**, both opening the character modal.
  The draggable Character panel is gone (a saved layout that names it just skips it).
- **Ability books:** one per combat style (Occultism first) as quiet inner tabs, the guide's link
  on the same row; drag onto a bar (the one line of help, pinned to the bottom). No level counter:
  levels are in the skills panel. ✓
- **Religion** (F): the **hymns** (prayers, sung by p-sworn's church) as a book of icons
  in level order: click to sing or stop, drag onto an action bar; a sung one glows, a locked one
  shows its level. Below them, **verses** (the church's, for melee) when melee exists. Devotion
  is on the main action bar, not here. ✓
- **Skills:** narrow tiles (three across in a modest width): icon, then level and the skill's cap
  parted by a small crack cut into the card (37 and 100, no slash); the combat and total levels on a footer row; click for the guide, right-click to
  "Set level target" or "Set exp target" (each asks for a number). Targets follow the player. ✓
- **Quests:** how many are done and the journal's link on one quiet row, then the list; click a
  quest for the journal. ✓
- **Task** and **XP tracker**. ✓
- To come: **verses** (melee; drag onto a bar like abilities, with the scriptures you hold),
  **spellbooks** (the occult houses' spells; choose the active one), **friends**, **group**.

## Modals

- **Journal** (J) ✓: **quests** (summary, requirements, the story so far, rewards),
  **achievements** (the diary by area and tier, with progress and rewards) and the
  **bestiary**. The bestiary has tabs of its own: **Bosses**, then one per **region** a creature
  spawns in (a creature in several regions is in each; the region you stand in opens first;
  "Elsewhere" holds creatures placed nowhere on the map). In a region, creatures whose carcass
  feeds a skill (butchering) have a section under that skill's name and icon, and their page
  says what carcass they leave and where to work it. Each creature: how dangerous it is, where
  it lives, how many you've killed, and a **drop log that hints**: its notable drops come
  first, as a silhouette under a "?" with a rarity word (Common, Uncommon, Rare, Very rare)
  until you have had one, then with the count; the tooltip gives the odds. Everything else it
  has given you (drops and carcass yields) follows under "Also obtained". **Notable** means
  rare (at most world.json `rareDropChance`, 5% by default, per kill or completion) or
  **unique** (no other creature or boss gives it, and it isn't guaranteed); the server works
  this, and where creatures live, out from content and spawns (`BestiaryGuideBuilder`, the
  "bestiaryGuide" state). Bosses show completions, attempts, times and the same drop log.
- **Guide** (G) ✓: **skills** (master-detail: every skill in a list with your level; the chosen
  skill's unlocks as one list in level order, each with a small badge for its kind (builder,
  equipment, tool, recipe, gathering...), what you have reached drawn plainly and what is ahead
  dimmed; gathered from the content itself) and **abilities** (every ability as a full card).
- **World map** (M) ✓: the continent, drawn from the layout the world is built from
  (`world/layout/`, sent as `ClientContent.Chart`), so it always matches the world. The ground is
  painted once (each ground in its colour, wandering edges, hill shading, shallows, an inked coast,
  faint borders between the powers); over it, as vectors that stay crisp at every zoom, rivers,
  main roads, roads, passes (dashed) and ferries (dotted), the built regions outlined faintly in gold
  (**Region borders** in the legend hides them) with the rest shaded as unexplored, and the places' marks by tier. Names by tier (cities and towns in
  Cinzel, villages smaller, hamlets and landmarks only close in), realm names faint across their
  land, named rivers and passes close in; a place that has no name yet is an unnamed mark. Drag to
  pan, the wheel or +/- to zoom in fixed steps, "you are here" with your facing, a button to come
  back to it, a legend with toggles, and the ground, realm and region under the pointer at the
  bottom. A right-click sets **your marker** (a gold pin, kept in the client state `map.waypoint`, so
  it follows the player); a right-click on it or the clear button removes it, and reaching it clears
  it. Waystone travel will be a tab. Where the map is going: "The world map (design)" below.
- **Settings** ✓: general (roofs, lock, scale, reset layout, log out) and controls.
- **Character** ✓ (from the worn equipment's buttons): **stats** (every combat stat, grouped:
  offence, defence, devotion, a per-style table of power, accuracy, damage and deflection, and the
  verse and buff stats only while one is active; any stat no group names shows under Other) and
  **inspect** (what anyone sees of a player: name, combat and total level, the worn figure
  read-only, and highlights: best three skills, quests and achievements done). Right-clicking
  another player offers **Inspect** (after Walk here, so a left click still walks), which opens
  the same page on them. The server answers with public information only.
- To come: announcements (level up, quest complete, new drop).

## The world map (design)

Agreed 2026-10-26; not built yet beyond the marker and the toggles above.

**Why the map goes stale today.** `WorldChart` is the layout's plan (one ground per 8x8 zone from
`continent.json`, lines from the blockout's routes), compiled once when the server starts. Hand
painting, strokes, terrain and taken-over regions never reach it, and a server left running keeps
the old chart.

**The picture comes from the world itself.** The export (`tools/world export`, also run by Sync
World) bakes a chart from the compiled regions: each tile's ground in its map colour (a ground's
`map` colour, else its palette colour), hill shading from the terrain, water and shores as the game
draws them, house footprints filled and walls and fences as thin lines. Land nobody has built yet is
painted from the layout as now. It is a new file beside `server/data/maps`; nothing existing changes.
Objects are never drawn from their models: a thing shows only if it says how (below).

**Names stay design data:** places, tiers, realms and river names come from `continent.json`;
area names (forests, marshes, ranges, seas) from named `relief` and grounds; districts close in.

**Markers.** Every icon is a marker of a **kind**, described once in a data file (`map.json`):
icon, category, zoom band, priority, and the detail shown on hover. Markers come from providers,
all through one pipeline:

| Provider | Example |
|---|---|
| Placed by hand | `"map": { "marker": "minigame" }` on a piece in a region's data |
| Object and NPC types | `"map": { "marker": "dungeon" }` on `cave_mouth`: every placed one |
| Actions and menus | anything with `bank`, `pray`, a shop, a task-giver |
| Quests | whoever runs `startQuest` |
| Gathering clusters | enough nodes of one skill close together: one marker at their centre, listing what and the levels |
| Instances | a way into an instanced region: dungeon, boss arena |
| Creatures | where a kind lives (spawns and the bestiary): an area with its name and level range |

The most specific setting wins: a ground's `map` colour, then an object type's `map`, then a placed
piece's `map`; `"map": false` hides it. A tint (`"map": { "tint": "Wheat" }`) colours a footprint
instead of an icon, so a wheat field reads as wheat. Markers from content are made when the server
starts, so they follow hot-reload; only the ground picture is baked.

**Legend categories** (each a toggle; a search later):

- **Travel:** waystones, docks and ferries, shortcuts (the skill's icon, its level on hover), gates
  and barriers (locked by a quest or flag), dungeon and cave entrances.
- **Services:** bank, shops (one icon, a badge for what they sell), altars, monuments, task-givers,
  workstations (forge, furnace, range, rack, vat, still, loom, bench; close in only).
- **Skills:** gathering clusters per skill (Mining, Woodcutting, Fishing, Foraging, Dreaming),
  Butchering's hunting grounds.
- **Combat:** creature areas (level range; aggressive ones tinted), bosses, demigods, dungeons.
  Danger shading by level later, once there are enough creatures.
- **Quests:** quest starts by state for this player (available, locked with what's missing,
  in progress, done hidden); readables and view points close in.
- **Activities:** minigames, arenas, group content.
- **Personal:** you, your marker (done), where you last died, your own pins and notes, friends and
  group later.

**Zoom bands**, named rather than numbers: **continent** (realms, cities, a few landmarks),
**realm** (towns, bosses, minigames), **region** (villages, quest starts, banks), **area**
(entrances, resources, shortcuts, workstations, hamlets). A kind says the band it appears from; one
marker may override it (a famous dungeon from `realm`). Place tiers move onto the same bands.
Where icons would overlap, priority decides. Zoomed out, a town may show a small strip of what it
has instead of hiding it all.

**Per player:** done quests drop their marker; later, markers may stay hidden until discovered.
Inside a dungeon or instance, M shows that region's own chart.

**Kept off the map:** single creatures and NPCs, single trees and rocks, anything marked secret,
and the layout's reserved `sites` until their content exists.

**Order:** (1) bake the ground from the tiles; (2) markers, kinds and zoom bands, placed and by
object type; (3) the other providers and the legend's categories.

## Server windows

- **Bank** ✓: search; how many a click moves (1, 5, 10, X, All; X asks for a number); withdraw
  as **notes** (the server makes a note for every unstackable item; notes stack, and any bank
  takes them back as the item); on the right, tabs for the **pack** and **worn equipment**
  (click to deposit; a beast of burden would be another tab). Drag from the pack onto the
  bank to deposit.
- **Shop** ✓: stock, prices, buy 1 / 5 / 10 / X.
- **Conversation** ✓: two parts, as in the modern game. The **message** (portrait, name, the
  words, continue with Space) and then, if there's anything to answer, the **reply** ("What
  would you like to say?", the answers in one column, each a row with its number key in a chip).
  The conversation window has no frame of its own.

## Laying windows out

The owner's rule: **no gaps or margins anywhere.** Windows sit flush against each other and the
screen's edges, so a clean interface is easy to build. The rules are plain maths in
`WindowRules` (tested by `dotnet test tools/client-tests`).

- **Moving.** The title bar moves a window (a lone panel's icon too; a bare window by the grip of
  dots at its top left, faint until the pointer is near, hidden while locked). Every drag, by the
  title bar, the icon or a tab pulled out of a strip, behaves the same way: edges snap flush onto the
  screen's edges and onto a neighbour's facing edge, or in line with a neighbour's same edge (a bar
  stacked under another lines up with it), and the window's centre onto the screen's centre. Only
  neighbours level with it count, so nothing across the screen pulls. The snap distance is 8 screen
  pixels at every interface scale. **Hold Alt** to place freely. There is no grid. The window is
  drawn where it will land (a docked window that would overlap moves to the nearest free place),
  so nothing jumps on release; red means no room here. The only guide line drawn is the screen's
  centre, while the window sits on it (the owner found guides on every flush edge in the way).
  Dropped beside a window of nearly the same height (or width), within 48 screen pixels, it takes
  that window's size and lines up with it, so a column or row of windows comes out even.
- **Resizing:** any edge or corner. The moving edges snap flush the same way and stop flush at a
  neighbour (they never jump back). An edge shared with a flush neighbour is a splitter: dragging
  it moves the neighbour's edge too, so the two stay flush (`WindowRules.ResizeShared`); **hold
  Alt** to resize the one window alone. A window never goes under its minimum, which is its
  panels' content minimum plus its frame (and never narrower than its title bar's icons). A panel
  with `AllowedSizes` resizes between those sizes only (the nearest that fits), even if it is bare
  or fixed: extra action bars use it.
- **Tabs.** A tab pulled out becomes a window of its own at its panel's own default size. Dropped
  on another window's title bar it joins it: the window is outlined (its size never changes) and a
  marker shows where in the strip; a window that already holds six shows a red "FULL". A lone
  window dragged by its title bar onto another's title bar joins it too. Title bars show icons only
  (the name is the tooltip); the icons keep their size however narrow the window.
- **Presets** (Settings, and the windows menu): **Classic** (minimap top right, XP drops beside it,
  the side windows bottom right, chat bottom left, the bars bottom centre with the buff rows and any
  extra bars stacked on the main bar, target top centre), **Compact** (a smaller side window and
  chat; task and XP tracker closed), **Wide** (the second side window top left, a taller side
  window, a wider chat) and **Mirrored** (Classic with left and right swapped). Each placement is
  anchored to the screen or attached to a side of another window (`UiDefaultLayout.Placement.By`),
  so they stay flush whatever the panels' sizes; the tests check every preset is flush and clear at
  720p, 1080p and 1440p. Reset layout returns to the chosen preset.
- **Editing the layout** (windows menu or Settings; Escape or Done ends it, and it works while
  locked): every window shows an outline, its name and its handles, and the whole window drags.
  Stand-ins (`PlaceholderPanel`) show where the windows that open later will go (bank, shop, make,
  rig, conversation, boss timer) and the placeable overlays (the fishing gauge); where you leave
  them is where they open.
- **Sizes and screens.** A window keeps the size you chose (`Preferred`) apart from the size the
  screen allows, and grows back when there is room. Layouts are saved per screen size (in panel
  pixels, so the scale counts, rounded to 64 px buckets: `rth.ui.layout.v4.<bucket>`); a screen
  with no layout of its own starts from the nearest one (positions are anchored, so it adapts),
  and the old single layout (`rth.ui.layout.v3`) is moved over once. Settling (after a screen change
  or loading) moves only the windows that overlap, never resizes them.

## Architecture

- **Registries.** Panels and modals register themselves (any concrete subclass); modals list
  their tabs. `UiDefaultLayout` holds where panels start. Nothing lists interfaces by hand.
- **Panels read state and send intents.** They never decide outcomes.
- **Drag and drop** goes through `UiRoot.DragSource` (drag or click) / `BeginDrag` and drop
  targets (`AddDropTarget`), with payloads in `DragData.cs`; any slot can offer or accept
  drags. Dropping a pack item on another swaps them.
- **Action bar slots** hold "a:" abilities, "f:" focuses or "i:" items. A radial sweep shows
  the slot's cooldown: the global cooldown, or the ability's own when longer. Bars can be
  locked (right-click a slot, or Settings): no dragging, no tooltips.
- **Server state for the interface** arrives as `JsonStateMessage`s ("achievements",
  "bestiary", "bestiaryGuide", "client") read with `GameSession.ServerState<T>`. The client keeps its own
  state (action bar layouts) on the server with `SetClientState`; it follows the player.
- **Window layout** is saved per machine and per screen size in PlayerPrefs, with the chosen
  preset (the world map's legend and toggles are kept there too). See "Laying windows out".
- **Questions to the server** (things the client doesn't keep, such as another player's inspect
  card) go through `GameSession.Query(kind, arg)`; the answer lands in `GameSession.QueryResults`
  (`QueryResult(kind, arg)`, `QueryVersion` to know when to redraw). Server side, a kind is a
  handler registered with `QuerySystem.Register`.
- **Slots** (`UiKit.Slots`): one size for every slot inside a panel (pack, worn, bank, ability
  book, hymns, shop): 36 px with a 28 px icon, 4 px apart (the `slot--std` class). Grids are whole
  pixels wide plus a pixel of slack, because at a fractional interface scale (1.25 at 1440p)
  layout snaps widths to screen pixels and an exact fit can wrap its last column off centre. Panels
  ask for a minimum window that fits their content (`UiKit.Chrome.Around`). The action bars and
  buff icons keep their own, larger slots.
- **Inner tabs** (`UiKit.SubTabs`): tabs inside a panel (the ability book's styles) are small
  words over a faint line, never buttons; a panel's link to its guide or journal is a small icon at
  the right end of the same row. Help text is kept to one line at most, pinned to the bottom.
- **Bars** (`UiKit.Bar`): the label fills the bar with no margins and never wraps, and a bar
  that shows text is at least `Bar.LabelledHeight` (17 px) tall, so the text is centred inside
  it at any requested height.
- **The world map's data** is the server's `WorldChart` (from `world/layout/continent.json`;
  `shared/World/Layout/WorldChart.cs`). The client paints it with `ChartPainter` on a worker
  thread and draws the rest with `Painter2D` (`WorldMapModal`); zoom and pan are plain maths
  (`WorldMapView`, EditMode tests). `dotnet run --project tools/world -- chart` paints the same
  ground to `world/renders/chart.png` to look at without Unity.
