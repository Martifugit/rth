# Religion

*Draft (2026-11-03). From `docs/skills.md` 4 and `CLAUDE.md` (M9, M10). The church is still
called the Sanguine Synod in the game until it is renamed.*

## Summary

Religion is the faith of Cruor's empire: the church of p-sworn, the god of blood oaths. One church,
two practices: its choirs sing **hymns**, prayers any style can use, paid with devotion; its
blood-knights read **verses**, melee's power, paid with blood and scriptures. The skill is trained
at the church's **monuments**, which cut you open and take your blood.

## Goals

- Give every style a prayer layer, and melee an engine as deep as occultism's bile ([pillar 4](../1-overview/pillars.md)).
- Put the empire's expansion on the map: a monument wherever the church has pushed ([pillar 3](../1-overview/pillars.md)).
- Make bones and remains worth something, and give players a reason to train together.

## Ties to the setting

p-sworn keeps every oath sworn in blood, and takes blood as collateral. The faithful are told the
blood at the monuments seals their oaths; in truth it keeps a wounded god from bleeding out
([p-sworn](../3-story/powers/sworn.md)). Hymns sing of being found worthy (steadfastness, zeal,
judgement, penance), never of the blood paid.

## How it works

### Hymns
- **Singing a hymn** turns it on; it drains devotion every tick while it is sung.
- **Devotion** is ten per Religion level and never returns by itself: pray at any of the church's
  altars, drink a devotion draught, or die.
- **What they do:** less damage taken; more accuracy and damage for one style; halving one style's
  damage (the **overheads**); at the top, returning some of your damage as blood.
- **Only one overhead at a time,** and each style's hymns form a line where a higher one replaces
  the lower.

| Religion level | Hymns |
| --- | --- |
| 1, 25, 50 | Steadfast, Unbowed, Unbroken: less damage taken |
| 5, 30, 70 | Zeal, Fervour, Judgement: melee |
| 8, 33, 70 | Clarity, Vigil, Revelation: ranged |
| 11, 36, 70 | Insight, Discernment, Anathema: occult |
| 35, 38, 41 | Bulwark, Glare, Salt Ward: halve melee, ranged, occult damage (overheads) |
| 80 | Penance, Contrition, Atonement: the strongest boost for each style |
| 90 | Sacrament: some of the damage you deal comes back as blood (overhead) |

### Verses
- **Read on the global cooldown,** with a melee weapon. Each costs blood and a scripture, and most
  have long cooldowns.
- **Never a better damage button:** a verse changes how the fight goes (blood, damage taken, phlegm,
  timing).
- **The low-blood way:** most of them reward fighting low on blood, the church's martyrdom as a way
  to fight.

| Religion level | Verse | What it does |
| --- | --- | --- |
| 1 | Siphon | your melee heals you for a while |
| 15 | Abjuration | enormous damage, but only against occult-made creatures |
| 40 | Death's Dance | hits are softened now and paid later as a bleed |
| 55 | Hemospike | a frenzy: more phlegm, every ability costs blood |
| 70 | Redemption | the next attack is blocked completely and turned into phlegm |
| 85 | Martyrdom | taking damage builds scars; ten make your next ultimate free |
| 90 | Stigmata | much more damage while you stay under a third of your blood |

### Monuments, pledges and scriptures
- **Monuments** stand all over the world in tiers like everything else, each with a Religion level
  and often another requirement from the empire's expansion. Each is **raised** once, for a one-off
  reward.
- **Empowering** is the main way to train: push your arm into a **fount**, it cuts you ("Ouch!"),
  and you give blood a little at a time, AFK, for experience and sometimes **pledges**. A fount is
  sated after a while and another wakes. Now and then a fount wakes **hungry** for a few seconds and
  pays four times over, so an attentive player moves to it.
- **Scriptures** can't be made: only Cruor can concentrate the blood of the faithful into them.
  They are bought with pledges from Cruor's people, in four tiers.
- **Reliquaries:** remains sealed in a casing (metal and wood from other skills), placed in a
  monument, boost everyone empowering it for five minutes.
- **A trickle from fighting:** singing hymns and reading verses give a little experience.

## What the player sees

A Religion tab with devotion, hymns and verses; hymns and verses on the action bars; devotion on the
main bar. At a monument, the founts, the hungry one marked, the cut.

## Odd cases

- A verse can't be read if its blood cost would kill you.
- "Taking damage" for verses means hits and bleeds, never a verse's own cost.

## Open questions

1. What the monuments look like, and the church's reason for each.
2. Which verses come from quests or boss codices.
3. The Turmoil-type hymns' armour drain is designed but not built.
