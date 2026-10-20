# Interface

The game looks like Old School RuneScape but plays like RS3, with more combat resources than
either. That means more interface than OSRS has. This file lists every piece and how they are
organised.

**Built (Milestone 8):** the window system and the panels marked ✓ below. Bank, shop and
dialogue are still the old IMGUI placeholders (`TownWindows`).

## Always on screen

| Piece | Contents |
|---|---|
| **Main action bar** ✓ | Ability slots and the key combat resource bars: blood, phlegm, the bile axis, breath. |
| **Secondary action bars** | Several more slot bars; can be shown or hidden. |
| **Minimap** ✓ | Compass, run energy (an indicator and the run toggle; energy comes with the breath skill), world map button, in-game clock, a menu of every panel. |
| **Chat** ✓ | Game messages and public chat (Enter to type). Private messages and group chat to come. |
| **Target** ✓ | What you are fighting, and its health. |
| **Side panel** | Tabs for the small panels below. |

## Side panel tabs (small panels)

- **Inventory** (28 slots). ✓
- **Worn equipment**, with bonuses. ✓
- **Ability books:** one per combat style (Occultism first). ✓
- **Verses** (melee's resource, from Religion).
- **Focuses:** breath's deflections and similar stances (the prayer analogue). ✓
- **Grimoires:** the occultists' *workings* (their spells), one grimoire per occult house.
- **Quest list** (simple). ✓
- **Skills:** every level at a glance, total level, XP and level targets (right-click). ✓
- **Friends.**
- **Group** (party).
- **Metrics:** XP gained, rates, time to target. ✓
- **Task:** the current task and points. ✓
- **Settings** shortcut.

## Larger windows (opened when needed)

- **World map** ✓ (regions to scale, where you are), and later travel by **waystone** (the lodestone analogue): you attune to
  waystones on the roads, then travel between them from the map.
- **Quest journal.**
- **Bestiary** with drop logs (creatures and bosses).
- **Achievement diary.**
- **Settings** ✓ (roofs, lock, scale, reset layout, log out).
- **Announcements:** level up, quest complete, new drop log entry and similar.
- **Skill unlocks:** what each skill unlocks at each level.
- **Ability reference:** a fuller view of each style's abilities.
- **Player examine:** a summary of another player.
- Already exist as placeholders: bank, shop, dialogue.

## Layout (decided)

**RS3-style from the start:** every panel lives in a window you can drag, resize, combine
with other panels as tabs, or split out again. The look is still OSRS. The **default layout
is carefully arranged** to feel like a classic fixed layout: minimap top right, a tabbed
side window bottom right, chat bottom left, and the action bars bottom centre. Players can
lock the interface, and reset it to the default.

## Architecture

- **One panel registry.** Every piece above is a panel with an id, a title, an icon, a default
  place (side tab, bar, centre window, overlay), an optional hotkey and an optional
  server-driven open state. New interfaces register themselves; nothing lists them by hand.
  This is the same pattern as the server's systems.
- **Panels read client state and send intents.** They never decide outcomes. Windows the
  server opens (bank, shop, dialogue) keep going through `InterfaceSystem`.
- **Layout is saved per player.** Start in PlayerPrefs. Later, use a save section so it follows
  the player between machines.
- **Theme:** chunky OSRS-style stone and leather frames and a pixel-style font, all from one
  stylesheet, with original art only.
