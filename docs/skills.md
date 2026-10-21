# Skills (v1)

The reference for every skill R'th launches with: what it is for, how it is trained, what it
takes in and gives out, and what each level band unlocks. Combat resources and the rules of a
fight are in `docs/systems.md`; this file says which skill feeds which of them.

**Status.** The roster and the combat split are **decided** (2026-10-21): melee, ranged,
occultism and defence; Religion's hymns and verses; Breath is gone; Medicine is the herblore.
Everything else here is a **proposal** made so that building content doesn't keep raising new
questions. Numbers are starting values to tune by playing, and every material name is
provisional.

---

## 1. The roster

| Group | Skill | In one line | Feeds |
|---|---|---|---|
| Combat | **Melee** | Attack and strength in one: swords, maces, spears, shields. | Melee damage |
| Combat | **Ranged** | Rationalist crossbows and slings; calibration. | Ranged damage |
| Combat | **Occultism** | Bile abilities, house spellbooks, and the rites that make reagents. | Occult damage, reagents |
| Combat | **Defence** | Armour, max blood, shared defensive abilities. Trained by any style. | Survival |
| Faith | **Religion** | Tithe's hymns (prayers, any style) and Cruor's verses (melee's power), gathered at monuments. | Hymns, verses |
| Hub | **Butchering** | Carcasses into everything else: meat, hide, gall, bone, old-blood. | Nearly every skill |
| Gathering | **Mining** | Ore, stone, salt, gems. | Smithing, Occultism, Medicine |
| Gathering | **Woodcutting** | Logs, charcoal, resin. | Artifice, Smithing, Cooking, Medicine, Religion (casings) |
| Gathering | **Fishing** | Fish, shells, brine. | Cooking, Medicine |
| Gathering | **Foraging** | Herbs, fibres, fungi, fruit. | Medicine, Tailoring, Cooking |
| Gathering | **Dreaming** | Fragments pulled from memory orbs in the Veilwood. | Occultism, Medicine, lore |
| Making | **Smithing** | Metal into melee weapons, plate, tools, jewellery, bolt heads, reliquary casings. | Melee, Defence, Religion, everyone's tools |
| Making | **Artifice** | Crossbows, bolts and sights (the Rationalist craft). | Ranged |
| Making | **Tailoring** | Hide into leather, fibre into cloth: leathers, robes, capes. | Ranged, Occultism |
| Making | **Cooking** | Fish and meat into food. | Blood |
| Making | **Medicine** | Herbs and extracts into potions. | Phlegm, bile, boosts, cures |

Sixteen skills. RuneScape 3 has 29. With a small group and no real player economy, each skill
is a pipeline someone trains for themselves, so the list stays short and every skill feeds
something that matters in a fight.

**Not in v1:** Farming (a big system; Foraging covers herbs for now), Firemaking (anyone can
light a fire), Construction, Agility (went with Breath; see 2.8), Thieving, Summoning, Hunter
(Butchering and Foraging cover it), Runecrafting (Occultism's rites), Divination (Dreaming is
its stranger cousin), Archaeology, Invention, Slayer (Butchering).

### Does every fight have a solo supply?

| A fight needs... | Made by | From |
|---|---|---|
| Food (blood) | Cooking | Fishing, Butchering (meat), Foraging (fruit) |
| Potions (phlegm, bile, boosts) | Medicine | Foraging (herbs), Butchering, Fishing, Mining |
| Reagents (occult spells) | Occultism's rites | Butchering (extracts), Mining (salts), Woodcutting (ash), Dreaming |
| Ammunition (ranged) | Artifice | Smithing (heads), Woodcutting (shafts), Butchering (fletching) |
| Verses (melee's power) | Religion: pledges from monuments buy scriptures | Monuments; Butchering (remains), Smithing and Woodcutting (casings) for reliquaries |
| Hymns (any style) | Religion (devotion) | To be decided (4.1) |
| Weapons and armour | Smithing, Artifice, Tailoring | Mining, Woodcutting, Butchering, Foraging |
| Tools | Smithing (+ Artifice for the fine ones) | Mining |

Butchering sits in the middle on purpose: killing things is how most materials enter the world,
and the carcass is where combat and skilling meet.

---

## 2. Rules every skill follows

### 2.1 Levels and pace
- Every skill uses the existing curve (RuneScape's formula) to **level 100** (14,391,160 XP);
  XP keeps counting to 200M. A skill may cap lower with `maxLevel`, but none in v1 does.
- **Target pace: about 50 hours of focused play from 1 to 100**, front-loaded so the early game
  moves fast. Content should be tuned so that the *best ordinary method* in a band gives roughly:

| Band | Hours (cumulative) | XP/hour to aim for |
|---|---|---|
| 1–10 | 0.3 (0.3) | ~4k |
| 10–20 | 0.7 (1) | ~5k |
| 20–30 | 1.5 (2.5) | ~6k |
| 30–40 | 2.5 (5) | ~10k |
| 40–50 | 3.5 (8.5) | ~18k |
| 50–60 | 4.5 (13) | ~38k |
| 60–70 | 6 (19) | ~77k |
| 70–80 | 8 (27) | ~155k |
| 80–90 | 10 (37) | ~335k |
| 90–100 | 13 (50) | ~700k |

  AFK methods should give about half of that, and dangerous or attentive ones up to 1.5x.
  A test can check a recipe or node's XP per hour against this table.
- **Level 100** in any skill gives its cape (Tailoring makes ordinary capes; skill capes are
  awarded). No other "max" rewards in v1.

### 2.2 The tier ladder
Every making and gathering skill climbs **the same ten tiers**, one per ten levels, so a tier-5
pickaxe, tier-5 ore, tier-5 bar and tier-5 sword all ask for level 40.

**Tiers are not tied to the build order.** A region's resources come from its **biome**:
mountains are good for ore, forests for trees, coasts for fish. So each region is great for one
or two skills and ordinary for the rest, and the important resources end up spread across the
world because each belongs to a different kind of place. No new region makes an old one
obsolete, because the old one is still the best place for something.

- **Abundant at home, sparse elsewhere.** A resource is abundant where its biome is strongest.
  Lower-level resources also turn up **in small numbers** in other regions where they make sense
  (a few iron rocks in a river valley, a stand of ash on a mountain's lower slopes), so a player
  is rarely far from something to gather.
- **Each skill's best tiers sit in a different region** from the other skills' best tiers, so no
  region is the endgame region.
- A material's name can say where it's most at home (sere cedar, glare salt) without that being
  the only place it grows.

**Where each region is strongest** (names from `docs/world-map.md`):

| Region | Biome | Great for (its high tiers) | Decent for |
|---|---|---|---|
| **Venaria** (Tolling, Cruor, the heartland) | green river country | Woodcutting low to mid (alder, ash, elm), Foraging (herbs, flax) | Fishing low to mid, Mining low |
| **Slough** | fens, broken coast, sea caves | **Foraging top** (deep fen herbs), Fishing mid (lamprey) | bog iron, black poplar |
| **Sere** | dry hills, wells | Mining mid (cinnabar), Fishing mid (cistern carp) | sere cedar, sere salt |
| **Calcinia** | salt coast and flats | salt, Mining (glare-silver), Fishing (mullet) | tamarisk |
| **Candora** (the range's surface) | mountains, high forests | **Mining high** (frostiron), Woodcutting high (pale pine) | ice char |
| **The Veilwood** | the mist forest | **Woodcutting top**, Dreaming | moonbark |
| **The Cradle's shores** | the bay | **Fishing top** (hushfin, during the Cradle's event) | |
| **Undermost** | the depths | **Mining top** (deepglass) | |
| **Craters** (anywhere, as events) | moonfall | verdigris, lightfin (both temporary) | |

| Tier | Level | Metal | Wood | Fish | Herb | Fibre |
|---|---|---|---|---|---|---|
| 1 | 1 | copper | alder | perch | yarrow | reed |
| 2 | 10 | iron | ash | eel | rue | reed |
| 3 | 20 | steel (hematite + charcoal) | elm | pike | feverfew | flax |
| 4 | 30 | bog iron | black poplar | lamprey | bog myrtle | flax |
| 5 | 40 | cinnabar | sere cedar | cistern carp | sere sage | silkweed |
| 6 | 50 | glare-silver | tamarisk | mullet | saltwort | silkweed |
| 7 | 60 | frostiron | pale pine | ice char | frostcap | pale wool |
| 8 | 70 | verdigris (moon-touched) | moonbark | lightfin | moonmoss | pale wool |
| 9 | 80 | deepglass | mistwood | hushfin | deepbloom | threadmoss |
| 10 | 90 | old-blood alloy | (old-blood) | (old-blood) | (old-blood) | (old-blood) |

- Tier 10 isn't gathered. Its recipes take **old-blood** (Butchering's top yield, section 5) on
  top of tier-9 materials, so every style's best gear goes through the same resource.
- Gear stats grow by tier from one formula (a tier-N item gets the tier's base value times the
  slot's share), so a new item needs only a tier and a slot in data. The formula is a combat
  tuning task, not a per-item one.
- Danger and rarity go together: within a region, the richest spots tend to be its dangerous
  places, and safer, sparser ones exist too.
- When a region is designed, its nodes are checked against the ladder so that, across the
  regions built so far, every tier of every gathering skill exists somewhere.

### 2.3 Gathering nodes
One mechanism for ore, trees, fishing spots, herb patches and dream orbs (data on the object):

- **Every attempt** (every `ticks`, usually 4 ticks, 2.4 s) succeeds with chance
  `clamp(base + perLevel x (level − nodeLevel) + the tool's success bonus, min, max)`.
- **Depletion:** each success depletes the node with `depleteChance` (ore 1/1, trees 1/8,
  fishing spots move instead, herb patches after `yields` picks). It comes back after
  `respawnTicks`. Nodes are **shared**: with at most ~200 players, competing for a rock is part
  of the world, and dense spots stop it being a problem.
- **Rare extras** roll on each success from a drop table on the node (gems from ore, galls from
  trees, shells from fish), with the existing nested drop tables.
- **Tools** are carried in the pack or worn (a pickaxe or hatchet can be wielded), and the best
  one you can use is picked automatically. No tool belt for now. A tool has a kind (pickaxe,
  hatchet, rod...) and a tier, and then **any of these effects**, each optional, so two tools of
  the same tier can feel different:

| Effect | What it does | Example |
|---|---|---|
| **Success** | adds to the chance of each attempt | a keener pickaxe |
| **Conservation** | a chance that a success doesn't deplete the node | a careful hatchet that leaves the tree standing |
| **Transformation** | a chance to change what's gathered as it comes, with that work's XP if it belongs to another skill | a hatchet that chars the log to charcoal, a brazier-net that cooks the fish |
| **Extra** | adds to or replaces the node's rare extras | a pick that turns up more gems |

  Effects are data on the tool (an item component, like other item capabilities), so a new tool
  is a content change. A transformation only happens if you meet the level the other skill would
  need for it (2.5).
- XP per success is set on the node; a test checks it against the pace table.

### 2.4 Processing
Every making skill uses the **recipe system** that already exists (inputs, tools, a station,
outputs, an output table, extra rolls per levels, XP). Rules on top of it:

- **Make-X:** choose an amount and it runs semi-AFK until done or interrupted. A batch of 28
  takes roughly a minute at most stations.
- **Stations** are objects with a `process` action: furnace, anvil, range, campfire, tannery
  vat, loom, spinning wheel, workbench, apothecary bench, lectern. Each tier doesn't need a new
  station; tiers 7+ may ask for a better one (the Frostpeak Forge's anvils, for example).
- **Failure** only where it reads naturally, within what you're allowed to make: Cooking can
  burn food you have the level to cook, less as you level. Everything else always succeeds.
- **Better outputs:** a recipe may name better or different outputs, not necessarily rare, each
  behind a condition: made **at a particular station** (a potion brewed at a house's bench, a
  blade forged at the Frostpeak Forge), with an **unlock** (a quest, house favour, a learned
  technique), with a particular **tool**, or a set number of levels above the recipe. The
  existing output table does the rolling; the condition decides which table applies.

### 2.5 Requirements
**Requirements are hard limits.** Without the level you can't do it at all: you can't mine the
rock, make the potion or wield the sword. A level never becomes a chance of failure. The only
way past a requirement is a **boost** that raises your level for a while (a potion, a food, a
hymn), and requirements check the boosted level.

- **Weapons** need their style skill (a tier-5 sword needs Melee 40).
- **Armour** needs Defence (a tier-5 plate body needs Defence 40), whichever style it is for.
- **Tools** need the gathering skill they're for (a tier-5 pickaxe needs Mining 40).
- **Jewellery and capes** need nothing, or the skill whose bonus they give.
- Making an item needs the making skill at its tier, never the skill that wears it.

### 2.6 Armour by style
Armour comes in three families. Each gives defence and adds to its own style:

| Family | Style | Made by | Inputs |
|---|---|---|---|
| Plate | Melee | Smithing | bars |
| Leathers | Ranged | Tailoring | hides (Butchering), thread |
| Robes | Occultism | Tailoring | cloth, steeped in reagents for the higher tiers |

Wearing another style's armour works but costs that style's accuracy (a light combat triangle,
done through stats, with no special code).

### 2.7 Every unlock is data
Every level-gated thing (node, recipe, ability, hymn, verse, monument, item, shortcut) keeps its level in
content, so the Guide's skill pages fill themselves (that already works) and nothing is listed by
hand.

### 2.8 Shortcuts without Agility
Shortcuts still exist, but each is gated by **the skill that fits it**: Mining clears rubble,
Woodcutting cuts through a thicket, Defence forces a jammed gate, Religion opens a Synod-sealed
door, Fishing wades a ford, Dreaming walks through a place that remembers being open. A shortcut
is an object action with a requirement and a destination, so it needs no new system.

**Running is free** in v1: there is no run energy now that Breath is gone. (Obstacle courses
with leaderboards may come back later as a minigame.)

---

## 3. Combat

### 3.1 Combat level
`combat = floor((1.3 x max(Melee, Ranged, Occultism) + Defence + Religion / 2) / 2.8)`

It runs from 1 (all level 1) to 100 (all level 100). NPCs show their own combat level.
Butchering's "combat gates what you can kill" means a creature's level, not a lock: you can try
anything.

### 3.2 Where combat XP goes
Damage XP goes to the style that dealt it, as now (0.25 XP per damage for occult). A combat
setting, **Train defence**, splits it:

| Setting | Style | Defence |
|---|---|---|
| Offence | 100% | 0% |
| Balanced (default) | 50% | 50% |
| Defence | 0% | 100% |

On top of that, any style gets Defence XP for damage it **prevents** with defensive abilities
(0.1 XP per point prevented). The style is whoever held the weapon that dealt the hit.

### 3.3 Melee
Attack and strength in one skill. Plays as the close, hard-hitting style that wants to be hit:
its power comes from blood lost.

- **Weapons:** one-handed swords and maces (with a shield or a second weapon), two-handed
  spears and greatswords. Ten tiers from Smithing.
- **Abilities:** builders, spenders and ultimates, as with Occultism. About eight at v1, more
  later. Spenders need Melee 5, ultimates Melee 10, the same steps as Occultism.
- **Its power source is Religion's verses** (section 4.2), as bile is Occultism's.
- **Trained by** melee damage.

### 3.4 Ranged
The Rationalists' style: patient, from a distance, precise.

- **Weapons:** slings (tier 1–2, they throw stones you mine, so they cost nothing to run),
  one-handed hand crossbows (paired, or with a sight in the off-hand), two-handed crossbows.
  All from Artifice.
- **Ammunition:** bolts (Artifice), one used per shot, some recovered from the ground at the
  target's tile (one in three). Bolts are tiered by their heads. Special bolts (barbed, salted,
  moon-touched) carry an effect.
- **Its live meter is calibration** (`systems.md`): it builds while you keep shooting the same
  target without moving, and ranged spenders cash it in.
- **Trained by** ranged damage.

### 3.5 Occultism
Everything in `systems.md` (bile, the eight abilities, spellbooks), plus the rites of
Distillation, which are how occultists make their own reagents.

**Spellbooks.** One per house, as decided. At v1:

| Spellbook | House | Spells (Occultism level) | Burns |
|---|---|---|---|
| **The Sallow Primer** | Sallow (Corvin) | 1, 10, 20, 30 | crude salt |
| **The Book of Gall** | Gall | 15, 35, 55, 75, 90 | Gall salts |
| **The Umber Codex** | Umber | 15, 35, 55, 75, 90 | Umber inks |
| **The Mordant Formulary** | Mordant | 25, 45, 65, 85 | vitriols (both poles) |

Each cast of the active spell burns 1–3 reagents. Higher spells hit harder and burn
higher-tier reagents. Spells are learned once (a quest or a house contact), then kept forever.

**Rites (Distillation).** Decided: an activity inside Occultism; rites give Occultism XP and
reagents.

- **Inputs:** a rite takes a **primary** (a humour extract from Butchering: gall, gland,
  tallow, bile-salt), a **base** (gathered: salt from Mining, ash from Woodcutting, brine from
  Fishing) and, from tier 7, a **catalyst** (a Dreaming fragment or a rare drop).
- **Sites:** each house's rites are performed only at that house's sites, and **each site makes
  one family of reagents**, like Runecrafting's altars. The first site is Corvin's still in
  Tolling (Sallow, crude salt).
- **How it plays (the Necromancy rite model):** set the inputs in the site's vessel and start
  the rite. It runs on its own for about 30–60 s per batch. Every so often a **disturbance**
  appears (a boil-over, a crack in the glass, a flare, a black bloom). Clicking it within a few
  ticks keeps the batch whole and earns bonus XP; ignoring it loses part of the output. AFK
  gives about half the XP; tending gives all of it.
- **Reagents at v1:**

| Reagent | House | Level | Pole |
|---|---|---|---|
| Crude salt | Sallow | 1 | none |
| Spark salt / Flare salt / Sunfire salt | Gall | 15 / 45 / 75 | yellow |
| Lampblack / Pitch / Night ink | Umber | 15 / 45 / 75 | black |
| Green vitriol / Aqua fortis / Red vitriol | Mordant | 25 / 55 / 85 | both |
| Old-blood tincture | any house's highest site | 90 | both |

- **Favour:** each house keeps a favour score per player (points, which exist already), earned
  by its rites and quests. Its higher sites and spells ask for favour as well as a level.
- **Steeped robes:** reagents also go into Tailoring's higher-tier robes.

**Training:** occult damage (main), rites (about a third as fast as combat, but it also makes
your ammunition).

### 3.6 Defence
- **Gives:** the armour requirement for all three families, and **max blood**:
  `1000 + 90 x (Defence − 1)`, so 1,000 at level 1 and 9,910 at 100.
- **Defensive abilities** usable by any style, on their own tab of the ability book: Brace
  (block part of the next hit, level 1), Steel the Flesh (shorter damage reduction, 20),
  Riposte (reflect part of a melee hit, 40), Unyielding (survive one lethal hit, long
  cooldown, 70). They cost phlegm like other spenders.
- **Trained by** the XP split (3.2) and damage prevented.

---

## 4. Religion

**Decided (2026-10-21).** Religion is the faith of Blood, in two halves that belong to the two
sees. It replaces the breath skill: focuses become its hymns.

| Half | See | What it is | Who it serves | Paid with |
|---|---|---|---|---|
| **Hymns** | Tithe | Prayers, our equivalent of RuneScape's | Every style | **Devotion** |
| **Verses** | Cruor | Melee's power engine, as bile is Occultism's and special ammo is Ranged's | Melee | Blood, **scriptures**, long cooldowns |

### 4.1 Hymns (Tithe)
Plain prayers, deliberately: nothing fancier than RuneScape's.

- **Singing a hymn** turns it on; it does one specific thing while it lasts and drains
  **devotion**. Any number can be sung at once, except that **only one overhead** can be up at a
  time (Bulwark, Glare, Salt Ward, Sacrament).
- **Devotion** is the bar that replaces breath on the main action bar.
- **What hymns cover:** protection (deflecting melee, ranged or occult attacks) and boosts
  (damage, accuracy, defence). They work for every combat style, and unlock with Religion level.
- **Their themes are Tithe's faith, not its treasury:** being found worthy (steadfastness,
  zeal, judgement, revelation, penance), with a little of the salt coast's harsh light. Names are
  short and iconic, like RuneScape's best prayers.
**Devotion** works like RuneScape's prayer points:
- **Size:** `10 x Religion level` (10 at level 1, 1,000 at 100), plus gear.
- **Drain:** each hymn drains its rate every tick it is sung; rates add up. A **devotion bonus**
  on gear (Tithe's vestments, holy symbols) slows all drain by 1% per point.
- **No natural regeneration.** It comes back by praying at an **altar** (any altar of Blood:
  Tithe's chapels, wayside chapels, the Synod's churches; it refills completely), by
  **devotion draughts** from Medicine (`70 + 25%` of max per dose), by a slow **renewal
  draught** (1% of max per tick for 50 ticks), and in full on respawn.
- With an overhead and a Turmoil-type hymn up (1.6 per tick), a level-100 player's 1,000
  devotion lasts about 6 minutes without bonuses or draughts: the same feel as RuneScape.

**The hymns.** Names decided (2026-10-21). Numbers are starting values to balance in play;
effects follow the RuneScape prayer each one is based on.

| Level | Hymn | Based on | Effect | Drain/tick |
|---|---|---|---|---|
| 1 / 25 / 50 | **Steadfast**, **Unbowed**, **Unbroken** | Thick Skin and its tiers | +5% / +10% / +15% armour | 0.1 / 0.2 / 0.3 |
| 5 / 30 / 70 | **Zeal**, **Fervour**, **Judgement** | the melee boosts, up to Piety | melee accuracy and damage +3% / +5% / +8%; Judgement also +4% armour | 0.1 / 0.2 / 0.5 |
| 8 / 33 / 70 | **Clarity**, **Vigil**, **Revelation** | the ranged boosts, up to Rigour | the same for ranged | 0.1 / 0.2 / 0.5 |
| 11 / 36 / 70 | **Insight**, **Discernment**, **Anathema** | the magic boosts, up to Augury | the same for occult | 0.1 / 0.2 / 0.5 |
| 35 | **Bulwark** | Protect from Melee | 50% less damage from melee (NPCs) | 0.5 |
| 38 | **Glare** | Protect from Missiles | the same for ranged | 0.5 |
| 41 | **Salt Ward** | Protect from Magic | the same for occult | 0.5 |
| 80 | **Penance** / **Contrition** / **Atonement** | Turmoil / Anguish / Torment | melee / ranged / occult accuracy and damage +10%, armour +4%; each of your hits lowers the target's armour by 1% (to −10%) | 0.8 |
| 90 | **Sacrament** | Soul Split | 7.5% of the damage you deal returns as blood | 0.8 |

**Only one overhead at a time:** Bulwark, Glare, Salt Ward and Sacrament. Everything else can be
sung together. Each style's hymns form one line, and a higher one replaces the lower: Fervour
replaces Zeal, and Penance is the top of the melee line (it replaces Judgement). The defence
tiers form a line too, so the most anyone sings is one defence hymn, one hymn per style and one
overhead.

### 4.2 Verses (Cruor)
Verses take melee beyond the rotation its abilities settle into: they raise its skill ceiling
and its power. They **complement** melee abilities and never compete with them.

- **Never a better damage button.** A verse changes how the fight goes (blood, damage taken,
  phlegm, timing); its value shows in the melee abilities around it. A very few verses deal
  damage in narrow cases (like Tuska's wrath in RuneScape), and are only worth reading then.
- **Read on the global cooldown.** Each costs **blood** and **scriptures** (4.3), and most have
  long cooldowns. Action bar room isn't a constraint: there are several bars.
- **Melee only:** reading a verse needs a melee weapon. Some verses also need a Melee level.
- **Unlocks differ per verse:** a Religion level always, and possibly a quest, or a codex-like
  drop from a boss.
- **"Taking damage" means hits and bleeds** (including the bleed from Death's Dance), never a
  verse's own blood cost. This one rule applies to every verse that reacts to damage taken.
- **The low-blood way.** Most of the set rewards fighting low on blood, Cruor's martyrdom as a
  playstyle: Stigmata wants you under 30%, Death's Dance and Redemption keep you there, Siphon
  keeps you alive, and healing back above the line costs you Stigmata's bonus.

**The first verses.** Numbers are starting values to balance in play. Durations and cooldowns
are in ticks (0.6 s). A verse can't be read if its blood cost would take your last blood.

| Level | Verse | Blood | Scripture | Cooldown | Effect |
|---|---|---|---|---|---|
| 1 | **Siphon** | 2% current | I | 75 (45 s) | 20% life steal on your melee damage for 10 ticks. |
| 15 (+ Melee 15) | **Abjuration** | 5% max | I | 50 (30 s) | 400% ability damage, but **only against occult-made creatures**; nothing against anything else. |
| 40 | **Death's Dance** | 5% max | II | 100 (60 s) | For 20 ticks, every hit you take is reduced by 20% up front; the damage it stopped is applied to you as a bleed over the next 10 ticks. |
| 55 | **Hemospike** | 2% max per ability cast | III | 100 (60 s) | A bloody frenzy for 12 ticks: basic abilities generate 2 more phlegm, and every ability cast costs 2% blood. |
| 70 | **Redemption** | 70% current | III | 200 (2 min) | The next incoming attack within 10 ticks is blocked completely; 1 phlegm for each 2% of max blood it would have dealt (at most 50). |
| 85 | **Martyrdom** | 20% max | IV | 200 (2 min) | For 50 ticks, a stack of **Scar** each time you take damage. At 10 stacks, your next ultimate costs no phlegm and consumes the stacks. |
| 90 | **Stigmata** | needs and consumes 80% max | IV | 300 (3 min) | For 20 ticks, while you're below 30% blood, you deal 30% more damage. |

### 4.3 Pledges and scriptures
- **Pledges** are what Religion gathers. Empowering a monument (4.4) has a chance on each
  success to give pledges, the way a rock gives ore. They are points (the existing points
  system).
- **Scriptures** are what verses consume. Players can't make them: they hold the perfect mix of
  the blood of hundreds of thousands of the faithful, which only Cruor can concentrate. They are
  **bought from Cruorian characters with pledges**. That is Cruor's bargain: power for the people
  who carry its faith across the world.
- **Scriptures are tiered:** each read uses one scripture of the verse's tier.

| Scripture | Price (pledges) | Read by |
|---|---|---|
| I | 2 | Siphon, Abjuration |
| II | 5 | Death's Dance |
| III | 10 | Hemospike, Redemption |
| IV | 25 | Martyrdom, Stigmata |

- **Pledge rates:** each successful attempt at a fount has a 10% chance to give pledges equal to
  the monument's tier (1–10). AFK at a tier-5 monument that is about 350 pledges an hour; a
  ten-minute boss fight reading verses on cooldown spends roughly 150. Those two numbers are the
  balance to keep: an hour of empowering should pay for a few long fights.

### 4.4 Monuments
Cruor keeps trying to expand, and its monuments are how that shows in the world.

- **All over the world,** in every region, each with a Religion level requirement. Many also
  need something else from Cruor's expansion: a quest, a region opened, standing earned. Where a
  monument stands and what it took to raise it tells the story of Cruor pushing outward.
- **Tiers:** monuments come in the same ten tiers as everything else (2.2), one per ten
  levels, and like other nodes they are scattered: a region can hold a tier-2 and a tier-8
  monument side by side.
- **Raising:** each monument is raised once, when you first meet its requirements, for a one-off
  XP reward (about ten minutes of AFK empowering there). Some cost **remains from bosses or other important creatures**, some of them quest
  rewards.
- **Empowering (the main method, AFK):** the monuments are strongest with fresh blood. A monument
  has several **founts**, which work like gathering nodes (`skills.md` 2.3): your character
  pushes an arm into one, the game says the fount cuts you open, your character says "Ouch!", and
  you take a symbolic 5 damage (regenerated well before the fount changes). Then it runs like
  woodcutting or Divination: an attempt every 4 ticks, succeeding by level, gives Religion XP and
  sometimes pledges, and after 20–40 successes the fount is sated and another one wakes, so you
  move. XP per success is set so AFK empowering earns half the pace table (2.1).
- **Hungry founts (active):** about every 150 ticks (90 s) a fount wakes hungry somewhere on the
  monument for 15 ticks: 4x XP and 4x the pledge chance. Moving to each one brings an attentive
  player up to the pace table's full rate.
- **Reliquaries (the costly, fast method):** remains sealed in a **casing** made from other
  skills' materials (metal from Smithing, wood from Woodcutting, more at higher tiers), assembled
  as an ordinary recipe at a workbench or chapel. Placed in a monument, a reliquary gives
  **everyone** empowering it +50% XP and +50% pledges for 500 ticks (5 minutes); one reliquary
  per monument at a time. Most of the XP comes from using it; assembling one gives a tenth of
  the bonus XP it is expected to earn. Better remains (from stronger creatures, through Butchering) make
  stronger reliquaries, which need higher Religion. This is the game's main use of bones and
  remains, and a reason to train together.

### 4.5 Training Religion
- **Empowering monuments** (AFK, low XP, free) and **hungry founts** (active, more).
- **Reliquaries** (fastest, costs remains and casings).
- **Raising monuments** (one-off rewards).
- **A trickle from fighting:** 1 XP per 10 devotion drained in combat, and 5 XP per scripture
  tier for each verse read.

**Why it doesn't play like Distillation:** no rites. Empowering is gathering at a fixed place,
reliquaries are an ordinary recipe, and what it produces (pledges) is spent at a shop, not burned
by a spell.

---

## 5. Butchering
As built (Milestone 4), extended:

- **Six creature families,** each with its own tool and workstation. A family spans many
  levels: its tool and station come at its start, and stronger creatures of that family ask for
  more.

| Family | From level | Tool | Station | Example creatures |
|---|---|---|---|---|
| Basin wildlife | 1 | skinning knife | skinning rack | fen rats, herons, boars, river wolves |
| Scalth-touched | 10 | glass flensing hook | lye vat | vitriol mites, cask toads, Scalth wyrms (80) |
| Occult-made | 30 | bone saw | rendering pot | failed workings, house familiars |
| War-beasts | 45 | cleaver | flaying frame | Synod destriers, Sere's war-lizards |
| Moonborn | 60 | moonglass knife | cold cellar | crater crawlers, anima-things |
| The deep | 75 | **Frostpeak Forge** tools | **the Frostpeak Forge** | Undermost's things |

- **Yields** feed everyone: meat (Cooking), hide and sinew (Tailoring, Artifice), feathers
  (Artifice), gall, glands, tallow and bile-salt (Occultism, Medicine), remains (Religion's
  reliquaries), and rarely tools, gear pieces and **old-blood**.
- **Old-blood:** a rare yield from any family's creatures at level 85 and above, more from the
  strongest. Every tier-10 recipe in every skill takes it.
- **Task-givers** per family: Wenna Thole (wildlife, built), Corvin Sallow (Scalth-touched,
  built), a Synod quartermaster (war-beasts), a Rationalist in a crater camp (moonborn), Brannoch
  (the deep). Tasks pay points per family, spent at their shops on tools, gear and yield
  boosts.
- **Stockpiling:** carcasses are unstackable but can be noted, and any butchering station takes
  notes, so a long session needs one bank trip.
- **Trained by** butchering carcasses (main) and task completions.

## 6. Medicine
The herblore. Not for healing (food does that), apart from one mild tick-heal.

- **Potions** are made at an apothecary bench: a herb (Foraging) into a vial of water, then a
  **secondary** from another skill. Vials are sold cheaply by apothecaries (no glassmaking).
  Potions have 4 doses.

| Level | Potion | Secondary | Effect |
|---|---|---|---|
| 1 | Bracing tonic | perch scale | start a fight with 10 phlegm |
| 5 | Antivenom | rat gall | cures and protects against venom |
| 8 | Dreamless draught | rat gall + saltfig | the Dreamless quest's draught; later, protection against nightmare effects |
| 10 | Melee / ranged / occult draught | herb-specific | +3 + 5% to that style |
| 20 | Defence draught | eel slime | +3 + 5% defence |
| 25 | **Yellow draught** / **black draught** | spark salt / lampblack | sets bile to +30 / −30 and holds it there for 30 ticks |
| 20 | Devotion draught | salt | restores 70 + 25% of max devotion |
| 30 | Tisane of mending | fen honey | heals 1% blood per tick for 20 ticks (the tick-heal) |
| 35 | Vitriol ward | lye | resist Scalth's corrosion (area hazard) |
| 60 | Renewal draught | mullet oil | restores 1% of max devotion per tick for 50 ticks |
| 50 | Strong tonic | lamprey tooth | start with 25 phlegm |
| 50–80 | Super draughts (every style, defence) | tier herbs | +5 + 10% |
| 70 | **Choler** / **Melancholia** (the overloads) | sunfire salt / night ink | holds bile at ±60 for 5 minutes and boosts that pole's abilities |
| 90 | **Panacea** | old-blood | all super draughts in one, 5 minutes |

- **Trained by** making potions (main) and cleaning herbs (light).

## 7. Gathering

### Mining
**Decided (2026-10-21).** Ore, gems, salt and stone. Tool: pickaxe (Smithing); no station. Ore
rocks deplete on every success (2.3) and come back after their respawn time.

**Ores.** Most ores are plain; a few tiers have a quirk that makes them different to mine, built
from mechanics that already exist (nodes, tools, world events). Quirks are kept to three or four
per gathering skill so they stay special. "Where it tends to be" is the kind of place, not a
fixed region (2.2).

| Tier | Ore | Where it tends to be | Its quirk |
|---|---|---|---|
| 1 | **Copper** | river cliffs, road cuttings | none: the plain one, to learn on |
| 2 | **Iron** | hills, quarries | none: the workhorse |
| 3 | **Hematite** | old Synod quarries, red hills | none (its rust-red streaks are why miners say "the rock bleeds"); smelted with charcoal into steel |
| 4 | **Bog iron** | fens, marsh pits | not in rock: **dredged from wet pits**, a node that refills over time instead of depleting |
| 5 | **Cinnabar** | dry red country | none |
| 6 | **Glare-silver** | salt flats, white stone | none |
| 7 | **Frostiron** | Candora, dwarven halls | none |
| 8 | **Verdigris** | moonfall craters, which can open anywhere | **appears only after a moonfall** (a world event), then fades |
| 9 | **Deepglass** | Undermost | **brittle**: a rock can shatter before it's worked out, unless your pickaxe has conservation |
| 10 | (none) | | tier 10 is old-blood alloy (2.2) |

**Gems** are a rare extra from any rock, and the one place Mining touches the humours: each gem
carries one, and jewellery set with it leans towards that humour's style (Smithing).

| Gem | Humour | Jewellery leans to | Found from |
|---|---|---|---|
| **Garnet** | blood | melee | low tiers up |
| **Amber** | yellow bile | occult, the yellow pole | low tiers up |
| **Jet** | black bile | occult, the black pole | low tiers up |
| **Moonstone** | phlegm | ranged | mid tiers up |
| **Bloodstone** | blood, pure | melee, top tier | high tiers, rarely |

**Salt** is its own line: **rock salt** (river cliffs; low), **sere salt** (dry country; mid) and
**glare salt** (raked from white flats; high). It feeds Cooking, Medicine and Occultism's rites,
and the higher rites ask for the purer salts.

**Stone** never depletes: sling ammunition and shortcuts.

**Trained by** mining ore (main), salt and gems (extras).

### Woodcutting
**Decided (2026-10-21).** Logs for Artifice (crossbow stocks, bolt shafts), Cooking (campfires)
and Religion (reliquary casings), and charcoal for Smithing. Tool: hatchet (Smithing). Trees
fall by chance (2.3) and regrow.

**Trees.** Mostly plain, with a quirk on a few tiers, as with Mining's ores.

| Tier | Tree | Where it tends to be | Its quirk |
|---|---|---|---|
| 1 | **Alder** | Venaria's riverbanks | none |
| 2 | **Ash** | Venaria's hedgerows and woods | none: the staple (bolt shafts) |
| 3 | **Elm** | Venaria's old fields and roadsides | **a big shared tree**: many players chop one, and it falls when their total is reached |
| 4 | **Black poplar** | Slough's fens | none |
| 5 | **Sere cedar** | Sere's dry hills | **the AFK tree**: slow attempts, but it almost never falls |
| 6 | **Tamarisk** | Calcinia's coast | none |
| 7 | **Pale pine** | Candora's high forests | none (resin is a common extra) |
| 8 | **Moonbark** | the Veilwood's edges, old moonfall scars | none |
| 9 | **Mistwood** | the deep Veilwood | chopping **draws the attention** of things in the mist, and they come now and then |

- **Extras:** resin (Medicine), bird nests (feathers now; seeds if Farming ever comes).
- **Charcoal:** burn logs at a kiln. Better logs give more charcoal per log. Steel and the higher
  smelts need it.
- **Tools:** the agreed tool effects (2.3); a charring hatchet, for example, sometimes turns a
  log to charcoal as it's cut.
- **Trained by** cutting.

### Fishing
**Decided (2026-10-21).** Fish for Cooking, brine for rites, and extras for Medicine. Tools: rod,
net, trap and lantern (Artifice: they're fine work); a spot offers one or two methods. Spots move
instead of depleting. Quirks are kept to a few tiers, so most fishing is plain.

| Tier | Fish | Where it's most at home | Quirk |
|---|---|---|---|
| 1 | **Perch** | Venaria's rivers | none |
| 2 | **Eel** | rivers, Wennow | **traps**: set one, leave, and come back to collect |
| 3 | **Pike** | Venaria's lakes and slow rivers | none |
| 4 | **Lamprey** | Slough's fens and river mouths | none (its teeth are an extra, for Medicine) |
| 5 | **Cistern carp** | Sere's wells and cisterns | none |
| 6 | **Mullet** | Calcinia's salt lagoons | none |
| 7 | **Ice char** | Candora's tarns, Nivil's edge | **cut your own hole**: you make the spot, and it freezes over after a while |
| 8 | **Lightfin** | crater lakes, moonlit pools | **follows light**: a lantern in your off-hand draws the school to you |
| 9 | **Hushfin** | the Cradle's shores | **only during the Cradle's event** (below) |

**The Cradle's event.** Every few hours (about every 3, to tune) the bay draws in its breath. The
game announces it to everyone a few minutes ahead, and for about 15 minutes hushfin spots appear
along the Cradle's shores. It is a world event, like a moonfall, and it's the only time tier-9
fish can be caught, which makes the shore a place people gather.

- **Brine** comes from any salt-water spot, for rites.
- **Extras:** shells, eel slime, scales and lamprey teeth (Medicine).
- **Trained by** fishing.

### Foraging
- Herb patches, fibre beds (reed, flax, silkweed), fungi rings and fruit bushes along roads,
  in fens and in woods, so the sparse regions between places have something to stop for.
- A patch gives several picks, then regrows. Tool: a sickle (Smithing), which only speeds it up.
- Gives herbs (Medicine), fibre (Tailoring), fruit (Cooking: small heals, pies), fen honey, and
  the saltfig trees of Sere.

### Dreaming
The skill closest to the story: it pulls fragments out of memory orbs in the Veilwood, the
Dreamweaver's archive. It arrives with the Veilwood region; the design is ready before that.

- **How it plays:** an orb opens a small instanced memory scene. Threads (glowing motes) drift
  through it; clicking one starts pulling it, which takes a few ticks and can fail by level.
  Now and then the memory **notices you**: an echo walks towards you, and you have to step off
  its path or the pull breaks and the orb closes early. Active by nature, though slow orbs can be
  semi-AFK.
- **Yields:** dream fragments by tier (catalysts for tier 7+ rites, rare Medicine secondaries),
  **lucid draught** ingredients, and **vignettes**: short lore scenes, one per orb type, kept in
  the journal. A full set of an orb's vignettes is a collection log.
- **Unlocks:** deeper orbs by level (orbs of the town, the Schism, the Scalding, the Physician's
  edges). Higher orbs are where the nightmare boss is fought (`lore.md`), so Dreaming gives
  utility there: dreamwards (charms against its effects) and more pulls per orb.
- **Trained by** pulling threads.

## 8. Making

### Smithing
- **Smelting** at a furnace (ore + charcoal from tier 3), **smithing** at an anvil: melee
  weapons, plate, shields, all tools (pickaxe, hatchet, sickle, knives, cleaver), bolt heads,
  and jewellery settings (gems from Mining).
- **Jewellery** (neck and ring): metal + gem. The gem's humour decides the style it leans to
  (garnet and bloodstone melee, amber and jet the two occult poles, moonstone ranged; see
  Mining), and the metal's tier decides how much. At higher tiers a ring or amulet is steeped in
  a reagent to strengthen its humour.
- **Trained by** smelting (a little) and smithing (most).

### Artifice
The Rationalist craft: precision work in wood, metal, horn and glass.

- **Ranged weapons:** sling (cord + leather), hand crossbow, crossbow (stock + Smithing limbs +
  sinew string).
- **Bolts:** shaft (wood) + head (Smithing) + fletching (feathers); 15 per set. Special bolts at
  higher levels (barbed, salted, moon-touched, deepglass).
- **Sights** (ranged off-hand), and the finer tools: fishing gear, the moonglass knife.
- **Trained by** making bolts (main) and weapons.

### Tailoring
- **Leather:** tan hides at a tannery vat, then cut them at a workbench: ranged leathers
  (body, legs, hands, feet, coif), quivers, pouches.
- **Cloth:** spin fibre into thread on a wheel, weave it on a loom, then sew robes (hat, robe
  top, skirt, gloves, boots). From tier 4, robes are **steeped** in a reagent from Occultism's
  rites, which is where the house flavour (and pole bonus) comes from.
- **Capes**, and dyes later.
- **Trained by** making leathers and robes.

### Cooking
- **Fish and meat** on a range (in town) or a campfire (anywhere, burns more often). Meat comes
  from Butchering, so killing things can feed you.
- **Heals scale with tier:** a tier-N fish or cut heals roughly `100 + 150 x (N − 1)` blood
  (100 at tier 1, 1,450 at tier 10), about one hit's worth at that tier.
- **Burn chance** falls with level and stops entirely 15 levels above the food's tier. A range
  burns less than a fire.
- Combined foods: pies and stews (two portions, fruit or herbs added), from tier 3.
- **Trained by** cooking.

---

## 9. Moving from today's prototype
The code rework that follows this document, in short:

- **Remove Breath:** `playerMaxBreath`, breath regen and drain, the breath gauge, the
  `maxBreath` stat on two items. The bar's place goes to **devotion**.
- **Focuses become hymns** (Religion), drained from devotion; deflection is three of them.
- **Add verses, scriptures, pledges and monuments** with melee, and calibration with ranged.
- **skills.json:** add melee, ranged, defence, religion, mining, woodcutting, fishing, foraging,
  dreaming, smithing, artifice, tailoring, cooking, medicine. Remove **distillation**: its XP in
  saves is **added to Occultism** on login, its recipes become Sallow rites, and the two
  requirements on it (gloves, recipes) become Occultism.
- **Max blood** comes from Defence (3.6) instead of the fixed 1,000.
- **Combat level** and the **Train defence** setting.
- **Place names** (`docs/world-map.md`): the Pilgrim Road becomes Palmerway (scene, builder, flag,
  region title), the Backwards Mill Widdermill, the Wash Wennow, and "Scald" becomes Scalth in
  content ids and text (Corvin's Scald tasks, the Scald-touched creatures).

## 10. Still open
Smaller decisions that can wait until the content that needs them:

- Religion's numbers: all starting values; balance them in play (4.1–4.4).
- Verses: which ones come from quests or boss codices; more verses later.
- What the monuments are (their look, and Cruor's reason for each).
- Exact gear stat formula per tier and slot (2.2).
- The names of the melee and ranged abilities, and the Defence ability list's numbers.
- Which quests teach each spellbook, and the favour costs per house.
- Whether Farming joins later (as a slower, steadier herb and food source).
