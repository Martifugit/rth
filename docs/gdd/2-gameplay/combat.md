# Combat

*Draft (2026-11-03). From `docs/systems.md`, `docs/skills.md` 3 and `CLAUDE.md` (M2, M10).*

## Summary

Fights are in real time on the tick: an auto-attack runs while you hold a target, and you fire
abilities from your action bars on a short global cooldown. Builders build phlegm, spenders and
ultimates spend it. Each style adds an engine of its own. Players fight creatures, never each other.

## Goals

- RuneScape 3's ability combat, rebuilt on the humours ([pillar 4](../1-overview/pillars.md)).
- Three styles that play differently, each supplied by the player's own skills
  ([pillar 1](../1-overview/pillars.md)).

## Ties to the setting

Each style belongs to a power: melee to Cruor's empire and its church, ranged to the spine's
surface people, occultism to the occult houses. Nothing locks a player to one; the styles are what
each power teaches.

## How it works

- **Styles.** Your weapon decides your style, your auto-attack and which abilities you can use.
  Empty hands fight with occultism.
- **Abilities** come in builders, spenders (need half a bar of phlegm) and ultimates (a full bar).
  Spenders need level 5 in the style and ultimates level 10. Abilities cost no items.
- **The global cooldown** is three ticks. Abilities pressed during it wait (queueing, a setting).
- **Holding a fight.** Walking away keeps your target while you are in combat; using an ability
  takes the fight back up, and if you are out of range you run back and it fires on arrival.
- **Revolution** (a setting) fires the leftmost ready ability on your main bar when you press
  nothing.

### The styles

| Style | Plays as | Engine | State |
| --- | --- | --- | --- |
| **Occultism** | Push your bile hot for damage or cold for safety; abilities need a lean. | The bile axis; later a house spellbook, whose spells burn reagents from your own rites | Built (eight abilities); spellbooks designed |
| **Melee** | Close and heavy, and it wants to be hit: most of its power comes from blood lost. | Cruor's verses, read for blood and scriptures ([Religion](religion.md)) | Built (eight abilities, seven verses) |
| **Ranged** | Patient and precise from a distance: plant your feet and keep a line of sight. | Tipped ammunition; perhaps a meter that builds while you keep shooting one target from one tile | Designed in outline |
| **Defence** | Not a style: armour, max blood and four defensive abilities any style can use (brace, damage reduction, reflect, survive one lethal hit). | | Built |

**Armour by style.** Plate for melee, leathers for ranged, robes for occultism. Wearing another
style's armour works but costs that style's accuracy.

**Where experience goes.** Damage trains the style that dealt it; the **Train defence** setting
splits it with Defence (half each by default).

**Creatures** chase, give up when led too far, may attack on sight, and come back after a while.
How each behaves is in [VI](../6-behaviour/index.md).

## What the player sees

Click a creature to attack. Hitsplats, bleeds and projectiles; the target panel; action bars with
radial cooldowns; abilities you can't use yet greyed out.

## Odd cases

- Eating delays your next attack.
- Damage lands on the tick it is dealt.

## Open questions

1. Ranged: its tips, their effects, and whether the calibration meter stays.
2. Where ranged keeps its ammunition (a quiver slot or the pack).
3. Spellbooks: the first spells, and which quests teach each house's book.
4. Final names for the melee and ranged abilities.
