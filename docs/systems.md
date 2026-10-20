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
| **Phlegm** | Adrenaline (RS3-style) | Universal across all styles. Basic abilities build it, thresholds and ultimates spend it. Nothing else uses it. |
| **Bile** | Occultism's live axis, from −100 to +100 | Every player has it. Occult abilities push it either way. Some abilities also need a bile level, on top of phlegm. Holding a position (e.g. +30) gives a passive bonus. |
| **Breath** | Deflection (overhead-prayer analogue) | Available to every style. Must not use phlegm. Ties to the Pneuma ascetics. Bar clutter was considered and accepted. |
| **Verses** | Religion's output, spent by melee | Spent on top of normal abilities. |
| **Calibration** | Ranged's precision resource | Rewards sustained accuracy. |

**Proposal: bile poles.** Humoral theory has two biles, and they map onto the axis.
- **+ pole: yellow bile (choleric).** Hot, angry, offensive.
- **− pole: black bile (melancholic).** Cold, inward, defensive and utility.

**Prototype (Milestone 2).** The poles above are playable, with placeholder numbers. All of them
live in `server/data/content/` and hot-reload, so they're meant to be tuned by playing:
- **Passives** from ±30: choleric deals 5% more damage at +30, rising to 20% at +100;
  melancholic takes 5% less damage at −30, rising to 15% at −100.
- **Extremes** from ±80: choleric **fever** burns 0.5% of max blood per tick (it never kills);
  melancholic **torpor** halves phlegm gained from basics.
- **Settling:** after 10 ticks out of combat, bile drifts back 1 per tick, phlegm fades 2 per
  tick, and blood and breath regenerate.
- **Phlegm:** basics build 6–8; thresholds need 50 and spend 15; ultimates need and spend 100.
  3-tick global cooldown; one ability can be queued.
- **Breath:** 300 max; deflecting one style drains 1 per tick and halves that style's damage.
- **Death:** the Assayer resets bile to 0 on respawn. That is the cost of dying in the
  prototype; there is nothing to lose until items exist.
- **Abilities:** Gall Spark (+10), Black Draught (−10, heals 25% of damage), Still the Humours
  (25 towards zero), Bitter Salt (needs +20), Choleric Flare (threshold, needs +30), Melancholy
  Shroud (threshold, needs −30, 30% ward), Vitriol Rain (ultimate, needs +60), Black Sun
  (ultimate, needs −60, heals 20% and wards). Reagent costs wait for items and Distillation.

## Combat styles

- **Melee: Blood / the Sanguine Synod.** Plate, close quarters. Spends verses from Religion.
- **Ranged: the Rationalists (phlegm).** Calibration resource. Ammo comes from an ammo-crafting
  skill (a reflavoured fletching or engineering), with moon-touched materials at higher tiers,
  following RS ammo progression.
- **Occultism: the occult houses (bile).** Moves the bile axis. Casting also needs a rune-like
  reagent (see Distillation).

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

### Ammo crafting (fletching or engineering analogue)
Makes Rationalist ammunition; moon-touched materials at higher tiers.

### Distillation (runecrafting analogue)
Makes Occultism's casting reagents. Butchering byproducts and Dreaming fragments feed it.

**Proposal: how Distillation works.**
- **Two reagent families:** *choleric salts* for + pole spells and *melancholic tinctures* for
  − pole spells. Some spells need both; those are the hardest and most expensive casts.
- **Inputs:** butchering byproducts (gall, glands, rendered fat) as the base, like rune essence.
  Dreaming fragments as the rare catalyst for high tiers. **Vitriol** from springs in the Scald
  for the top tiers. (Oil of vitriol is a real historical alchemy term, and it ties the skill to
  the occultists' own war crime.)
- **Places (the runecrafting trip):** hidden stills. Low tiers sit under the counters of city
  apothecaries (Corvin's shop could be the first), mid tiers in fen houses and sea caves the
  Synod can't hold, and the top tier at the edge of the Scald. Gaining access to each still
  could be a small quest.

## Death

Players meet **the Assayer** each time they die (see `docs/lore.md`). It replaces the RS grave
screen and comments on how the player's humour balance is changing.

## Open questions

- Distillation: confirm or revise the proposal above.
- The exact rune-like reagent requirements per occult spell tier.
- Calibration: how exactly it builds and decays.
- Religion: the ritual mechanic itself, and how many verses melee can hold.
- Final names for skills, resources and places.
