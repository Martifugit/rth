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
| **Panel** (`UiPanel`) | Lives in a movable window; can share it as a tab. Resizable unless it has a fixed size. | Pack, worn, character, abilities, focuses, skills, quests, task, XP tracker |
| **Fixed panel** (`FixedSize`) | Movable, never resized: the size it has to be. | Action bars, minimap, chat, target, bank, shop, conversation |
| **Server window** (`ServerOpen`) | Opens and closes when the server says; closing it tells the server. | Bank, shop, conversation, the Assayer |
| **Modal** (`UiModal`) | Centred, fixed size, tabs of related pages, one at a time; Escape or a click outside closes it. | Journal, guide, world map, settings |

## Always on screen

| Piece | Contents |
|---|---|
| **Main action bar** ✓ | Blood, phlegm and breath as three equal gauges; the bile axis (two halves filling outward from a black centre that holds the value); 14 slots on keys 1 to 0, - and =. Fixed size. |
| **Secondary action bars** ✓ | Two more bars of 14 slots (from the window menu). Abilities and items are dragged onto any bar; layouts are kept on the server. |
| **Minimap** ✓ | Square; compass and clock in its corners; beneath it the run toggle and buttons for the world map, journal, guide, settings and a menu of every window. No blood orb: blood is on the action bar. |
| **Chat** ✓ | Filters (all, game, public) and the line you type on (Enter). Fixed size. Private and group chat to come. |
| **Target** ✓ | What you are fighting, and its health. Kept while you walk away in combat. |
| **Buffs / debuffs** ✓ | Two fixed rows of status icons above the action bar (bile's four statuses, wards from abilities), each with its % or time left; hover for what it does. |
| **Side window** | Tabs for the panels below. |

## Side window tabs

- **Pack** (28 slots; drag to move, onto a bar, onto the bank). ✓
- **Worn equipment** (the figure only). ✓
- **Character:** total level and every combat stat. ✓
- **Ability books:** one per combat style (Occultism first); drag onto a bar. ✓
- **Focuses:** breath's stances (the prayer analogue). Click to hold or let go, or drag onto
  an action bar and use them like abilities; no keys of their own. ✓
- **Skills:** tiles, the total level on a footer row; click for the guide, right-click to
  "Set level target" or "Set exp target" (each asks for a number). Targets follow the player. ✓
- **Quests:** the list; click for the journal. ✓
- **Task** and **XP tracker**. ✓
- To come: **verses** (melee), **grimoires** (the occult houses' *workings*), **friends**,
  **group**.

## Modals

- **Journal** (J) ✓: **quests** (summary, requirements, the story so far, rewards),
  **achievements** (the diary by area and tier, with progress and rewards) and the
  **bestiary** (every creature: how dangerous, how many you've killed, a drop log that includes
  what its carcass yields).
- **Guide** (G) ✓: **skills** (pick a skill; its unlocks level by level, filterable and
  sortable, gathered from the content itself) and **abilities** (every ability as a full card).
- **World map** (M) ✓: the regions to scale and where you are. Waystone travel will be a tab.
- **Settings** ✓: general (roofs, lock, scale, reset layout, log out) and controls.
- To come: announcements (level up, quest complete, new drop), player examine.

## Server windows

- **Bank** ✓: search; how many a click moves (1, 5, 10, X, All; X asks for a number); withdraw
  as **notes** (the server makes a note for every unstackable item; notes stack, and any bank
  takes them back as the item); on the right, tabs for the **pack** and **worn equipment**
  (click to deposit; a beast of burden would be another tab). Drag from the pack onto the
  bank to deposit.
- **Shop** ✓: stock, prices, buy 1 / 5 / 10 / X.
- **Conversation** ✓: two parts, as in the modern game. The **message** (portrait, name, the
  words, continue with Space) and then, if there's anything to answer, the **reply** ("What
  would you like to say?", numbered answers).

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
  "bestiary", "client") read with `GameSession.ServerState<T>`. The client keeps its own
  state (action bar layouts) on the server with `SetClientState`; it follows the player.
- **Window layout** is saved per machine in PlayerPrefs.
