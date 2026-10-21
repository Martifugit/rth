# Skills (v1)

The reference for every skill R'th launches with: what it is for, how it is trained, what it
takes in and gives out, and what each level band unlocks. Combat resources and the rules of a
fight are in `docs/systems.md`; this file says which skill feeds which of them.

**Status.** The roster and the combat split are **decided** (2026-10-21): melee, ranged,
occultism and defence; Religion absorbs focuses; Breath is gone; Medicine is the herblore.
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
| Faith | **Religion** | Tithe's petitions (prayers on credit) and Cruor's vows (melee's power). | Petitions, vows, martyrdom |
| Hub | **Butchering** | Carcasses into everything else: meat, hide, gall, bone, old-blood. | Nearly every skill |
| Gathering | **Mining** | Ore, stone, salt, gems. | Smithing, Religion, Occultism, Medicine |
| Gathering | **Woodcutting** | Logs, charcoal, resin, galls. | Artifice, Smithing, Cooking, Medicine |
| Gathering | **Fishing** | Fish, shells, brine. | Cooking, Medicine |
| Gathering | **Foraging** | Herbs, fibres, fungi, fruit. | Medicine, Tailoring, Cooking, Religion |
| Gathering | **Dreaming** | Fragments pulled from memory orbs in the Veilwood. | Occultism, Medicine, lore |
| Making | **Smithing** | Metal into melee weapons, plate, tools, jewellery, bolt heads. | Melee, Defence, everyone's tools |
| Making | **Artifice** | Crossbows, bolts and sights (the Rationalist craft). | Ranged |
| Making | **Tailoring** | Hide into leather, fibre into cloth: leathers, robes, vellum, capes. | Ranged, Occultism, Religion |
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
| Melee's power | Religion (vows) and blood lost | Food, which keeps you standing |
| Petitions (any style) | Religion (debt, paid off) | Butchering and Mining (offerings), Tailoring (vellum) |
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
pickaxe, tier-5 ore, tier-5 bar and tier-5 sword all ask for level 40. Each tier's materials are
found first in one part of the world, and the world map's build phases follow the same order.

| Tier | Level | First found | Metal | Wood | Fish | Herb | Fibre |
|---|---|---|---|---|---|---|---|
| 1 | 1 | Tolling and the river | copper | alder | perch | yarrow | reed |
| 2 | 10 | The Pilgrim Road, the Keeping's hills | iron | ash | eel | rue | reed |
| 3 | 20 | The heartland, Cruor, the bay shore | steel (iron + charcoal) | elm | pike | feverfew | flax |
| 4 | 30 | The Wrack's fens | bog iron | black poplar | lamprey | bog myrtle | flax |
| 5 | 40 | Sere and the Lantern Pass | cinnabar | sere cedar | cistern carp | sere sage | silkweed |
| 6 | 50 | The Saltmarch | glare-silver | tamarisk | glarefish | saltwort | silkweed |
| 7 | 60 | The Pale and Holdfast | frostiron | pale pine | ice char | frostcap | pale wool |
| 8 | 70 | Moonfall craters, the Scald's heart | verdigris (moon-touched) | moonbark | lightfin | moonmoss | pale wool |
| 9 | 80 | The Undermost | deepglass | stalkwood | blind cavefish | deepbloom | threadmoss |
| 10 | 90 | Convergence: no one place | old-blood alloy | (old-blood) | (old-blood) | (old-blood) | (old-blood) |

- Tier 10 isn't gathered. Its recipes take **old-blood** (Butchering's top yield, section 5) on
  top of tier-9 materials, so every style's best gear goes through the same resource.
- Gear stats grow by tier from one formula (a tier-N item gets the tier's base value times the
  slot's share), so a new item needs only a tier and a slot in data. The formula is a combat
  tuning task, not a per-item one.
- The rarest nodes of a tier sit in that tier's dangerous places (the Scald, crater edges, the
  Undermost, the bay), as `systems.md` already says.

### 2.3 Gathering nodes
One mechanism for ore, trees, fishing spots, herb patches and dream orbs (data on the object):

- **Every attempt** (every `ticks`, usually 4 ticks, 2.4 s) succeeds with chance
  `clamp(base + perLevel x (level − nodeLevel) + toolBonus, min, max)`. Better tools add
  `toolBonus` and can shorten `ticks`; they never replace the level requirement.
- **Depletion:** each success depletes the node with `depleteChance` (ore 1/1, trees 1/8,
  fishing spots move instead, herb patches after `yields` picks). It comes back after
  `respawnTicks`. Nodes are **shared**: with at most ~200 players, competing for a rock is part
  of the world, and dense spots stop it being a problem.
- **Rare extras** roll on each success from a drop table on the node (gems from ore, galls from
  trees, shells from fish), with the existing nested drop tables.
- **Tools** go on a **tool belt** once added (right-click a tool: "Add to belt"). They no longer
  take pack slots, and the best one you can use is picked automatically. Tools on the belt can't
  be lost. The belt holds one of each tool kind.
- XP per success is set on the node; a test checks it against the pace table.

### 2.4 Processing
Every making skill uses the **recipe system** that already exists (inputs, tools, a station,
outputs, an output table, extra rolls per levels, XP). Rules on top of it:

- **Make-X:** choose an amount and it runs semi-AFK until done or interrupted. A batch of 28
  takes roughly a minute at most stations.
- **Stations** are objects with a `process` action: furnace, anvil, range, campfire, tannery
  vat, loom, spinning wheel, workbench, apothecary bench, lectern. Each tier doesn't need a new
  station; tiers 7+ may ask for a better one (the Frostpeak Forge's anvils, for example).
- **Failure** only where it reads naturally: Cooking burns, Medicine can spoil above-tier mixes.
  Everything else always succeeds.
- **Masterwork chance:** each recipe may name a rare better output (an extra roll of the output
  table, which already exists). It is a small reward for training a little above the tier.

### 2.5 Requirements
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
Every level-gated thing (node, recipe, ability, petition, vow, item, shortcut) keeps its level in
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
- **Its live meter is martyrdom** (`systems.md`), raised by blood lost, and shaped by the
  **vow** you hold from Religion (4.3). Melee works without a vow, more plainly.
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

The faith of Blood is one church with two sees, and they read the same covenant two ways
(`docs/lore.md`): **Cruor** says it is a *keeping* and the faithful are its guards; **Tithe**
says it is a *debt* that will be called in. Religion is both readings, and each one plays like
its doctrine.

| Half | See | What it is | Who it serves |
|---|---|---|---|
| **Petitions** | Tithe | Prayers taken **on credit**. You run up a debt and pay it off. | Every style |
| **Vows** | Cruor | Oaths that turn **blood spent** into melee power (martyrdom). | Melee |

The two halves feed each other through blood: what you pay to Tithe's debt counts as sacrifice
to Cruor's keeping.

### 4.1 Debt and credit (Tithe)
Petitions don't drain a pool that refills. They **add to your debt**, and debt stays until you
pay it.

- **Debt** starts at 0 and rises each tick by the sum of your active petitions' rates.
- **Credit** is how much debt you can carry: `100 + 10 x Religion` (110 at level 1, 1,100 at
  100). Some gear raises it (a Tithe signet, the Synod's vestments).
- **Debt never fades on its own,** in combat or out, and it survives logout and death. (The
  Assayer has things to say about debts that outlive their debtor.)
- **Called in:** when debt reaches credit, Tithe collects. Every petition ends, you lose 20% of
  max blood at once (never below 1), you are **Indebted** for 10 ticks (no petitions), and debt
  drops to half your credit. It hurts, but it is a choice: a player can overdraw on purpose
  (and a melee player gains martyrdom from it).
- **Paying off debt:**

| How | Where | Religion XP? |
|---|---|---|
| **Offering** (salt, hearts, tallow, relics, coin: each item has a *worth*) | Any shrine; a Tithe chapel counts 1.5x | Yes, from the worth, debt or not. **The main way to train.** |
| **Writ of indulgence** (a consumable you write) | Anywhere, even mid-fight; shares a cooldown with potions | XP when written, not when used |
| **Tithe blood** (an action for the action bar) | Anywhere: pay 10% of max blood to clear 10% of credit | None, but it raises martyrdom |

  An offering first pays off debt and then counts as devotion. Its XP is the same either way,
  so training never asks you to run up debt on purpose.

### 4.2 Petitions
Toggled like focuses are now (click, or from an action bar), any number at once, each adding its
rate to the debt. The three deflections are mutually exclusive, and so are the boosts for the
same style. Names are Tithe's language of law and money.

| Level | Petition | Effect | Debt/tick |
|---|---|---|---|
| 1 | **Surety** | 4% damage reduction | 0.5 |
| 5 | **Steady Hand** | +4% accuracy, all styles | 0.5 |
| 10 | **Levy of Arms / of Aim / of Gall** | +4% damage for melee / ranged / occult | 1 |
| 15 | **Forbearance** | out of combat, blood regenerates twice as fast | 0.3 |
| 20 | **Pardon of Goods** | keep one more item on death (once item loss exists) | 0.5 |
| 25 | **Greater Surety** | 8% damage reduction | 1 |
| 30 | **Levy of the Field** | +5% gathering yield (a skilling petition) | 0.5 |
| 35 | **Writ against Steel** | deflect melee (−50%; PvE) | 2 |
| 38 | **Writ against Distance** | deflect ranged | 2 |
| 41 | **Writ against Bile** | deflect occult | 2 |
| 45 | **Greater Levy** (each style) | +8% damage, +4% accuracy for that style | 2 |
| 50 | **Lien** | 5% of damage you deal returns as blood | 2 |
| 60 | **Audit** | the target's weaknesses and drop odds are shown; +5% accuracy against it | 1 |
| 70 | **Grand Levy** (each style) | +12% damage, +8% accuracy for that style | 3 |
| 80 | **Distraint** | each of your hits drains 1% of the target's defence (to −15%) | 3 |
| 90 | **Foreclosure** | the first time in a fight you'd die, you live at 10% blood; debt jumps by half your credit | 1 |

### 4.3 Martyrdom and vows (Cruor)
**Martyrdom** is melee's live meter (0–100; its rules are in `systems.md`): it rises as you
lose blood, from hits or from tithing it, and melee spenders and ultimates can need it or spend
it. **Vows** are how Religion shapes it. You hold **one vow at a time**, sworn at any altar of
Blood. A vow is a promise: it gives something and **forbids** something, and the game enforces
the ban (you simply can't do the forbidden thing while you hold the vow).

| Level | Vow | Gives | Forbids |
|---|---|---|---|
| 1 | **Vow of the Keeper** | martyrdom from blood lost; at 100, +20% melee damage (scaling) | nothing |
| 20 | **Vow of the Wall** | 1.5x martyrdom from hits taken; it gives damage reduction instead of damage | running |
| 35 | **Vow of the Open Vein** | melee hits bleed the target; bleeding you inflict also raises martyrdom | food heals 25% less |
| 50 | **Vow of the Red Fast** | martyrdom never settles out of combat | eating in combat |
| 65 | **Vow of the Last Rite** | melee damage grows as your blood falls (+1% per 2% missing) | Tithe petitions that reduce damage |
| 80 | **Vow of the Covenant** | spending martyrdom also wards allies within 3 tiles | holding more than one petition |
| 95 | **Vow of the Unbroken Line** | at 100 martyrdom your next ultimate costs no phlegm | changing vow for 30 minutes after swearing |

### 4.4 Training Religion
- **Offerings** at shrines (main; about 60% of a player's Religion XP). Butchering gives hearts
  and tallow, Mining gives salt (Tithe's old offering), quests give relics.
- **Writing writs** at a lectern: vellum (Tailoring) + iron-gall ink (oak galls from
  Woodcutting + iron from Mining, mixed at the lectern) → Writs of Indulgence (3 tiers: clears
  50 / 150 / 400 debt).
- **Pilgrimage:** each shrine in the world gives a one-off XP reward when first visited, and a
  small daily devotion bonus (first offering of the day there counts double).
- **Use in combat:** 0.1 XP per point of debt you take on, and per point of martyrdom you spend.
  Small, so the faith grows as you use it, but no one trains by praying.

**Why it doesn't play like RuneScape's Prayer:** nothing refills a bar. You choose when to
settle, the penalty for overdrawing is a decision, the money is real (offerings come from
Butchering and Mining), and blood ties it to melee and to food. **Why it doesn't play like
Distillation:** there are no rites to sit through. Religion is spent in fights and paid off at
shrines; Distillation is a session at a site.

---

## 5. Butchering
As built (Milestone 4), extended:

- **Six creature families,** each with its own tool and workstation. A family spans many
  levels: its tool and station come at its start, and stronger creatures of that family ask for
  more.

| Family | From level | Tool | Station | Example creatures |
|---|---|---|---|---|
| Basin wildlife | 1 | skinning knife | skinning rack | fen rats, herons, boars, river wolves |
| Scald-touched | 10 | glass flensing hook | lye vat | vitriol mites, cask toads, Scald wyrms (80) |
| Occult-made | 30 | bone saw | rendering pot | failed workings, house familiars |
| War-beasts | 45 | cleaver | flaying frame | Synod destriers, Sere's war-lizards |
| Moonborn | 60 | moonglass knife | cold cellar | crater crawlers, anima-things |
| The deep | 75 | **Frostpeak Forge** tools | **the Frostpeak Forge** | the Undermost's things |

- **Yields** feed everyone: meat (Cooking), hide and sinew (Tailoring, Artifice), feathers
  (Artifice), gall, glands, tallow and bile-salt (Occultism, Medicine), hearts and bones (Religion
  offerings), and rarely tools, gear pieces and **old-blood**.
- **Old-blood:** a rare yield from any family's creatures at level 85 and above, more from the
  strongest. Every tier-10 recipe in every skill takes it.
- **Task-givers** per family: Wenna Thole (wildlife, built), Corvin Sallow (Scald-touched,
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
| 30 | Tisane of mending | fen honey | heals 1% blood per tick for 20 ticks (the tick-heal) |
| 35 | Vitriol ward | lye | resist the Scald's corrosion (area hazard) |
| 50 | Strong tonic | lamprey tooth | start with 25 phlegm |
| 50–80 | Super draughts (every style, defence) | tier herbs | +5 + 10% |
| 70 | **Choler** / **Melancholia** (the overloads) | sunfire salt / night ink | holds bile at ±60 for 5 minutes and boosts that pole's abilities |
| 90 | **Panacea** | old-blood | all super draughts in one, 5 minutes |

- **Spoiling:** a mix above your level (where allowed) can spoil. At level or above, it never
  does.
- **Trained by** making potions (main) and cleaning herbs (light).

## 7. Gathering

### Mining
- Ore by tier (2.2), plus **salt** (rock salt by the river, brine salt in the Saltmarch, sere
  salt in Sere): offerings, rites, Cooking and Medicine. **Stone** for slings and
  shortcuts, **gems** as rare extras (jewellery).
- Tool: pickaxe (Smithing). Station-free.
- Moonfall craters spawn temporary tier-8 **verdigris** nodes when a fragment falls (a world
  event).

### Woodcutting
- Logs by tier: crossbow stocks and bolt shafts (Artifice), fuel for campfires (Cooking),
  **charcoal** (burned at a kiln) for steel and higher smelts.
- Extras: resin (Medicine), **oak galls** (ink for writs), bird nests (seeds for later, feathers
  now).
- Tool: hatchet (Smithing).

### Fishing
- Fish by tier, and **brine** (Saltmarch spots) for rites. Extras: shells (offerings), eel slime
  and scales (Medicine).
- Tools: rod, net, trap (Artifice: they're fine work). A spot offers one or two methods.
- Spots move instead of depleting.

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
- **Jewellery** (neck and ring): metal + gem, with small bonuses (one stat per gem kind). At
  higher tiers a ring or amulet is steeped in a reagent to add a humour effect.
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
- **Vellum** (for writs), **capes**, and dyes later.
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
  `maxBreath` stat on two items. Deflection becomes three Tithe petitions.
- **Focuses become petitions:** the Focuses panel becomes **Petitions** (Religion), the "f:"
  slot type stays the same idea. Add debt, credit, being called in, offerings at shrines, writs.
- **Add martyrdom and vows** for melee, and calibration for ranged, when those styles are built.
- **skills.json:** add melee, ranged, defence, religion, mining, woodcutting, fishing, foraging,
  dreaming, smithing, artifice, tailoring, cooking, medicine. Remove **distillation**: its XP in
  saves is **added to Occultism** on login, its recipes become Sallow rites, and the two
  requirements on it (gloves, recipes) become Occultism.
- **Max blood** comes from Defence (3.6) instead of the fixed 1,000.
- **Verses** are dropped (melee's power is martyrdom now).
- **Combat level** and the **Train defence** setting.

## 10. Still open
Smaller decisions that can wait until the content that needs them:

- Exact gear stat formula per tier and slot (2.2).
- The names of the melee and ranged abilities, and the Defence ability list's numbers.
- Which quests teach each spellbook, and the favour costs per house.
- Whether Farming joins later (as a slower, steadier herb and food source).
