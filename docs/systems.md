# Gameplay systems

Working notes on combat resources and the rules of a fight. All names and numbers are
provisional. Sections marked **Proposal** are suggestions that the designer has not yet
confirmed. Every skill (what it does, how it trains, what it feeds) is in `docs/skills.md`; the
world and its powers are in `docs/lore.md` (canon).

## Design principles

- **Sandbox, like RuneScape.** Nothing is locked to a faction, and no skill is tied to one place.
- **No big player economy.** The player count is small (invited friends), so every combat
  resource needs its own solo skill pipeline. A player should be able to supply their own combat.
- **Same gameplay pillars as RuneScape:** tick-based EoC combat, long gathering, processing and
  using loops, levels gating content, quests and bosses. The twist is the world and the
  humour-based resources.

## Combat resources

| Resource | Role | Notes |
| --- | --- | --- |
| **Blood** | Health pool | Max from Defence. Restored by food; Medicine has one mild tick-heal. |
| **Phlegm** | Adrenaline (RS3-style) | Universal across all styles. Builder abilities build it, spenders and ultimates spend it. Nothing else uses it. |
| **Bile** | Occultism's live axis, from −100 to +100 | Every player has it. Occult abilities push it either way. Some abilities also need a bile level, on top of phlegm. Holding a position (e.g. +30) gives a passive bonus. |
| **Verses** | Melee's power engine (Religion, p-sworn's church) | **Decided (2026-10-21):** to melee what bile is to occultism. Read on the global cooldown for blood and scriptures; they shape the fight rather than deal damage. See `skills.md` 4.2. Built in Milestone 10. |
| **Calibration** | Ranged's live meter, 0–100 | Builds while you keep shooting one target without moving. |
| **Devotion** | Hymns (Religion, p-sworn's church), any style | **Decided:** replaces breath. Hymns are plain prayers (protection, boosts, utility) that drain it. |

**Removed (2026-10-21):** **breath** (the deflection resource, and the skill and faction behind
it). Deflection and the other focuses become Religion's hymns, on devotion.

**Proposal: bile poles.** Humoral theory has two biles, and they map onto the axis.
- **+ pole: yellow bile (choleric).** Hot, angry, offensive.
- **− pole: black bile (melancholic).** Cold, inward, defensive and utility.

**Prototype (Milestone 2).** The poles above are playable, with placeholder numbers. All of them
live in `server/data/content/` and hot-reload, so they're meant to be tuned by playing:
- **Statuses:** any lean away from 0 gives that pole's buff and debuff, shown in the buff and
  debuff bars, scaling in proportion to bile up to these at ±100 (`combat.json`):
  - yellow bile: **infused** (buff), 20% more damage dealt; **choleric** (debuff), 10% more
    damage taken.
  - black bile: **warding** (buff), 20% less damage taken; **melancholic** (debuff), 10% less
    damage dealt.
  Status names, icons and text are in `statuses.json`; the server decides who has which.
- **Only these four.** Bile has no other effects: no extremes (fever and torpor were removed).
- **Settling:** after 10 ticks out of combat, bile drifts back 1 per tick, phlegm fades 2 per
  tick, and blood regenerates. Devotion never regenerates by itself.
- **Phlegm:** builders build 6–8; spenders need 50 and spend 15; ultimates need and spend 100.
  3-tick global cooldown.
- **Using abilities (RS3-style):**
  - **In combat** means engaged with a target (even while still running to it) or hit or
    hitting within the last 10 ticks.
  - **Keeping the target:** walking away stops attacking but keeps the target for as long as
    you stay in combat. Using an ability then takes the fight back up: in range it fires, out of
    range it stalls and you run back. The target is lost only by attacking something else, by
    its death or leaving, or by dropping out of combat.
  - **Stalling:** an ability used while in combat but out of range goes on cooldown (and starts
    the global cooldown) at once, but fires only when the player reaches the target. If the
    global cooldown has passed by then, the next ability fires on the same tick: two at once.
    One stall at a time. It carries over to a new target and survives walking away, and is lost
    on dropping out of combat (its cooldown stays spent). Abilities marked `keepsStall` are used on yourself, need no
    target, deal no damage, don't release the stall and keep you in combat.
  - **Queueing** (on by default, a setting): an ability pressed during the global cooldown fires
    when it ends. Off: the press is ignored unless the ability is ready.
  - **Revolution** (a setting: off, builders, or builders + spenders + ultimates): when nothing
    is pressed, the leftmost ready, usable, in-range ability of those types on the main bar
    fires after each global cooldown. Items and focuses on the bar are skipped.
  - The options are client state (`combat.queue`, `combat.revolution`); the server reads them,
    and the main bar from `bars`.
- **Hymns (Milestone 9):** deflection is three overhead hymns (Bulwark, Glare, Salt Ward), each
  halving one style's damage while sung; breath is gone.
- **Death (today):** the Assayer resets bile to 0 on respawn, and items are kept. That is the
  prototype's stand-in; death v1 replaces it (see Death, below).
- **Abilities:** Gall Spark (+10), Black Draught (−10, heals 25% of damage), Still the Humours
  (25 towards zero), Bitter Salt (needs +20), Choleric Flare (spender, needs +30), Melancholy
  Shroud (spender, needs −30, 30% ward), Vitriol Rain (ultimate, needs +60), Black Sun
  (ultimate, needs −60, heals 20% and wards). **Abilities cost no items:** spenders and
  ultimates need phlegm, and some need bile at a level. (The `reagents` field on an ability
  still works, for a future style that wants ammunition-like costs.)

## Calibration (Proposal)

Ranged's meter, in the same shape as bile. Starting numbers, to tune in play. (Melee's verses
and Religion's hymns are in `skills.md`, section 4.)

**Calibration (ranged).**
- 0–100. **+10** for each hit on the same target from the same tile. A miss costs 20, a step
  costs half of it, a new target clears it.
- **Passive:** up to +15% accuracy and +10% critical chance at 100 (**sighted** buff).
- **Spent** by some ranged spenders (a called shot that spends it all for damage in
  proportion).
- So ranged wants to plant its feet and keep a line of sight: patient and precise.

## Combat styles

**Decided (2026-10-21):** four combat skills. **Melee** (attack and strength in one),
**Ranged**, **Occultism**, and **Defence**, which any style trains (see `skills.md`, 3.2).

- **Melee: Blood / Cruor's empire and p-sworn's church** (still the Sanguine Synod in the game).
  Plate, close quarters. Its power comes from the church's verses (Religion), read for blood and
  scriptures; most of them reward fighting low on blood.
- **Ranged: the spine's surface people (phlegm)** (still the Rationalists in the game). Its meter
  is calibration. Bolts come from **Artifice**,
  with moon-touched materials at higher tiers.
- **Occultism: the occult houses (bile).** Moves the bile axis. Abilities cost phlegm and bile,
  not items. On top of the abilities sit **spellbooks** (below), whose spells do burn reagents.

### Spellbooks (Occultism)

Works like magic in RuneScape 3: the ability bar and the spellbook are separate layers.

- **An active spell is required.** An occultist chooses one spell from a spellbook as their
  active spell, and their auto-attacks cast it. Without one, occultism has nothing to attack with.
- **Spells burn reagents.** Each cast of the active spell uses reagents (the rune equivalent),
  made with Distillation. Abilities never do.
- **Tier matches the spell.** Low-level spells use reagents from early rites; higher-level spells
  need reagents from harder rites at farther sites, so occultists have a reason to travel.
- **One spellbook per house.** Each occult house owns a spellbook built on its own philosophy of
  finishing the flesh (explosive yellow, corrosive black, or hybrids that straddle the axis), and
  its spells burn the reagents that house's rites make. A player who casts across both poles
  needs rites from more than one house.
- **Sandbox freedom.** No house locks you out: learn from any spellbook, swap between them, mix
  with other combat styles. Corvin Sallow teaches the first spells; the rest come from finding
  house contacts.
- **How spells look.** Each spell (like each ability) names its character animation; for now
  the auto-attack uses a one-handed cast and spenders and ultimates a two-handed one (see
  `client/Assets/Resources/Characters/animations.json`).
- **Not built yet.** Today the auto-attack is the fixed `bile_lash` ability. The first spellbook
  replaces it: active spell selection, reagent costs per cast, and the spell's animation.

## Skills

Moved to `docs/skills.md`: the v1 roster, the rules every skill follows, Religion (hymns, verses, monuments),
Butchering, Medicine, the gathering and making skills, and Distillation as Occultism's
rites.

## Death

**The design** is death through **the ferryman** in the realm of the dead (`docs/lore.md`,
section III): he holds everything you died with until you pay, in coin scaled by the gear's value
or with an obol you carried; with nothing to pay, he keeps it about a day; and the long way back
gets it free. The details are in `docs/lore-rework/death.md`, and the build is workstream D
(D1 server, D2 region and client, D3 the long way back) in `docs/plan.md`.

**In the game today** players still meet **the Assayer**, the prototype's stand-in: it comments
on the player's humour balance and respawns them with bile at 0, and nothing is lost. D1
replaces it.

## Open questions

- Hymns and verses: numbers and lists (`skills.md`, section 10).
- Calibration: numbers, once ranged is playable.
- Spellbooks: the first spellbook's spells and reagent costs (outline in `skills.md`, 3.5).
- Final names for skills, resources and places.
- The rest are listed at the end of `docs/skills.md`.
