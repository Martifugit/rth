# Gameplay systems

Working notes on combat resources and skills. All names and numbers are provisional. Sections
marked **Proposal** are suggestions that the designer has not yet confirmed.

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
| **Blood** | Health pool | Restored by food; Medicine may add a mild tick-heal brew. |
| **Phlegm** | Adrenaline (RS3-style) | Universal across all styles. Builder abilities build it, spenders and ultimates spend it. Nothing else uses it. |
| **Bile** | Occultism's live axis, from −100 to +100 | Every player has it. Occult abilities push it either way. Some abilities also need a bile level, on top of phlegm. Holding a position (e.g. +30) gives a passive bonus. |
| **Breath** | Deflection (overhead-prayer analogue) | Available to every style. Must not use phlegm. Ties to the Pneuma ascetics. Bar clutter was considered and accepted. |
| **Verses** | Religion's output, spent by melee | Spent on top of normal abilities. |
| **Calibration** | Ranged's precision resource | Rewards sustained accuracy. |

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
  tick, and blood and breath regenerate.
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
- **Breath:** 300 max; deflecting one style drains 1 per tick and halves that style's damage.
- **Death:** the Assayer resets bile to 0 on respawn. That is the cost of dying in the
  prototype; there is nothing to lose until items exist.
- **Abilities:** Gall Spark (+10), Black Draught (−10, heals 25% of damage), Still the Humours
  (25 towards zero), Bitter Salt (needs +20), Choleric Flare (spender, needs +30), Melancholy
  Shroud (spender, needs −30, 30% ward), Vitriol Rain (ultimate, needs +60), Black Sun
  (ultimate, needs −60, heals 20% and wards). **Abilities cost no items:** spenders and
  ultimates need phlegm, and some need bile at a level. (The `reagents` field on an ability
  still works, for a future style that wants ammunition-like costs.)

## Combat styles

- **Melee: Blood / the Sanguine Synod.** Plate, close quarters. Spends verses from Religion.
- **Ranged: the Rationalists (phlegm).** Calibration resource. Ammo comes from an ammo-crafting
  skill (a reflavoured fletching or engineering), with moon-touched materials at higher tiers,
  following RS ammo progression.
- **Occultism: the occult houses (bile).** Moves the bile axis. Abilities cost phlegm and bile,
  not items. On top of the abilities sit **spellbooks** (below), whose spells do burn reagents.

### Spellbooks (Occultism)

Works like magic in RuneScape 3: the ability bar and the spellbook are separate layers.

- **An active spell is required.** An occultist chooses one spell from a spellbook as their
  active spell, and their auto-attacks cast it. Without one, occultism has nothing to attack with.
- **Spells burn reagents.** Each cast of the active spell uses reagents (the rune equivalent),
  made with Distillation. Abilities never do.
- **Tier matches the spell.** Low-level spells use reagents from early Distillation sites;
  higher-level spells need reagents from sites like the Scald, the craters or the deep, so
  occultists have a reason to travel.
- **Polarity follows geography.** A yellow-leaning spell uses a yellow-leaning reagent (from
  fire-and-ash sites like the Scald); a black-leaning one uses a black-leaning reagent (flooded
  fens and caves). Casting across both poles means reagents from both kinds of place; sticking
  to one pole means training at fewer places.
- **One spellbook per house.** Each occult house in the fens could own a spellbook built on its
  own philosophy of finishing the flesh: one favouring explosive yellow spells, one corrosive
  black, one hybrids that straddle the axis.
- **Sandbox freedom.** No house locks you out: learn from any spellbook, swap between them, mix
  with other combat styles. Corvin Sallow teaches the first spells; the rest come from finding
  house contacts.
- **How spells look.** Each spell (like each ability) names its character animation; for now
  the auto-attack uses a one-handed cast and spenders and ultimates a two-handed one (see
  `client/Assets/Resources/Characters/animations.json`).
- **Not built yet.** Today the auto-attack is the fixed `bile_lash` ability. The first spellbook
  replaces it: active spell selection, reagent costs per cast, and the spell's animation.

## Skills

### Religion (prep skill)
Like RS3 Necromancy's ritual layer, but with no combat of its own. You perform rituals to create
verses, which melee spends. **Cruor** and **Tithe** each have their own verse lists,
and styles can cross-pollinate.

### Carcass butchering (Slayer analogue)

**Prototype (Milestone 4):** the core loop below is playable for two tiers. Fen rats (basin
wildlife: skinning rack + skinning knife) and vitriol mites (Scald: lye vat + glass flensing
hook, level 10) drop carcasses; their old loot tables are now the butchering yield. Each
completion rolls the yield table once, plus once more per 15 levels above the requirement.
Task-givers, points, the other tiers and old-blood are still to come.

- You kill a creature and it drops a carcass. Its loot table is gated behind processing the
  carcass at a workstation.
- Processing is semi-AFK, like RS Masterwork. Carcasses can be stockpiled.
- Combat level gates what you can kill; the skill gates your yield. Tools may help with slaying,
  but fights use normal gear.
- Six creature tiers, each with its own tool and workstation (from a skinning rack up to the
  **Frostpeak Forge**): basin wildlife, moonborn, occult, Scald, war-beasts, the deep.
- The top yield is **old-blood**, a convergence resource everyone wants.
- Task-giver NPCs hand out tasks per tier (e.g. Brannoch, Corvin, a Synod figure). They pay
  points spent on tools and gear.

### Gathering (mining, woodcutting, fishing)
Tiers are scattered across the whole basin, with several regions per tier. The rarest nodes sit
in dangerous places: the Scald, the deep, crater edges, the bay.

### Medicine (herblore analogue)
Not for healing (food does that), except perhaps a mild tick-heal brew. Makes adrenaline-style
potions, and overload-style potions that push your bile toward a pole before a fight.

### Dreaming (new skill)
Navigate memory orbs in the Veilwood and extract fragments.
- Supplies rare reagents for Medicine and Occultism.
- Gives utility against the nightmare boss.
- Unlocks lore vignettes as rewards.

### The breath skill (agility analogue; name to be decided)
The utility skill behind **breath**. It is to breath what Agility is to run energy, and more:
- **Shortcuts** around the world, gated by level, like agility shortcuts.
- **Focuses** (the prayer analogue): deflection and similar stances that spend breath. Higher
  levels unlock more focuses. Not called prayers.
- **Run energy:** higher levels raise run energy and how fast it recovers.
- **Obstacle courses that are designed on purpose:** fewer, more deliberate courses with
  completion times, personal bests and leaderboards, so friends can compete on the fastest runs.
  This is the part that should play better than traditional Agility.
- Ties to the Pneuma ascetics in the lore.

### Ammo crafting (fletching or engineering analogue)
Makes Rationalist ammunition; moon-touched materials at higher tiers.

### Distillation: reagent rituals (part of the Occultism skill, not a skill of its own)
Distillation is how occultists make the **reagents** that spellbook spells burn (see Spellbooks
above). It is not a separate skill: like rituals in RuneScape 3's Necromancy, it is part of the
**Occultism** skill and trains it. Occult abilities never consume reagents. There is no player
economy to buy from, so every occultist makes their own.

It plays like **Necromancy rituals** rather than runecrafting: you set a ritual up at a place,
it runs on its own while you tend it, and it turns raw input into reagents. Names below are
placeholders.

**Today's prototype differs:** Distillation exists as its own skill in `skills.json`, trained by
recipes at Corvin's still (semi-AFK processing), and is referenced by three recipes, the
Dreamless quest reward, an achievement reward and a pair of gloves' level requirement. Folding it
into Occultism is a content change (those references, plus what happens to Distillation XP
already in saves).

- **Gather gall.** Gall is the raw input: a residue of bile found in fen pools, carcasses and
  certain plants. It doesn't stack, so every piece takes an inventory slot; filling up takes
  real time, which sets the natural limit per trip.
- **Ritual sites.** Places where bile wells up from the ground are the sites. You prepare the
  ritual there and it renders your gall into reagents over its duration, the way a Necromancy
  ritual turns its inputs into its outputs.
- **Carry the reagents.** The output is compact and stackable, so you hold many casts' worth.
- **Two reagent families** remain an option: *choleric salts* for + pole spells and
  *melancholic tinctures* for − pole spells, with the hardest spells needing both. Dreaming
  fragments as a rare catalyst and Scald **vitriol** for the top tiers also still fit.

**Progression is geography.** Each site gives a different reagent. Your level decides which
sites you can use and how many reagents each gall yields, and higher tiers are farther away and
more dangerous. Roughly six tiers, several sites per tier across the whole basin so players
choose where to train (no skill is locked to one place):

1. Fen seeps in the occult north-east (starter)
2. Cellar sumps under city apothecaries (Corvin's shop could be the first)
3. Sea cave pools
4. The Scald, where the old vitriol still burns
5. Crater basins at moonfall sites
6. The deep

Houses could guard certain sites, so a high-tier site can be a quest and not just a level check.

**Links to other skills.**
- *Butchering:* occult-tier and Scald carcasses can yield several gall at once.
- *Dreaming:* could unlock hidden sites reached through memory orbs, or make a batch worth more.
- *Medicine:* stays separate. Its potions shift your bile before a fight; reagents fuel spells.

## Death

Players meet **the Assayer** each time they die (see `docs/lore.md`). It replaces the RS grave
screen and comments on how the player's humour balance is changing.

## Open questions

- Distillation: the ritual mechanic itself (setup, duration, what you do while it runs), and
  how it differs from Religion, which is also described as Necromancy-ritual-like.
- Distillation: when to fold today's separate Distillation skill into Occultism, and whether
  existing Distillation XP carries over.
- Distillation: is gall one flat item at every tier, or does the raw input vary by site tier
  (like RuneScape's rune essence versus pure essence)?
- Spellbooks: the first spellbook's spells, levels and reagent costs.
- Calibration: how exactly it builds and decays.
- Religion: the ritual mechanic itself, and how many verses melee can hold.
- Final names for skills, resources and places.
