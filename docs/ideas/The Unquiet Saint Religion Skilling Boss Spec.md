# The Unquiet Saint: Religion Skilling Boss Spec

Oct 21, 2026 · @Martin

## Overview

The Unquiet Saint is a repeatable, instanced skilling boss for the Religion skill. A martyr of
Cruor lies in a crypt beneath one of the see's monuments, and he will not stay at rest. You give
blood at the crypt's founts, carry the phials to his altar to sing his **Requiem**, and survive his
shouts until two cleanses lay him back down. A clean run takes 3 to 3.5 minutes. Fail too many
mechanics and the Saint wakes, which ends the run with no payout, the same as dying in a boss fight.

It rewards **pledges** (Religion's currency, spent on scriptures), a chance at a **verse codex**, a
chest of materials useful to other skills, a pet, cosmetics and collection log entries. All names,
lines and numbers in this spec are placeholders for tuning.

**Decided (2026-10-21):** founts and blood phials are the work; the Saint is Cruorian; a matching
overhead hymn blocks his projectiles completely; the fight is instanced; the bar is the Requiem.

## Design pillars

The encounter is built on the structure of RuneScape's skilling bosses, not their content: a
static boss, a work loop, a burst action and a pressure bar.

- **The work is the skill.** Giving blood at founts and carrying it to the altar is the main loop:
  Religion's own gathering (empowering, `skills.md` 4.4). Mechanics interrupt it and never replace it.
- **Static boss.** The Saint is one statue on a bier. Telegraphs are voice lines, lighting, floor
  decals, symbols and UI. No custom animation or rigging.
- **Self-contained.** The player brings food and devotion. Nothing depends on other players or a market.
- **Inventory pressure.** Phials are non-stackable and share slots with food, so you cannot eat
  through mistakes.
- **One demand at a time.** Shouts never overlap, and owed blood waits for a shout to finish.
- **Room to get good.** Routing, pre-stocking, reading tells and choosing how greedy to be all
  separate fast runs from slow ones.
- **Failure is readable.** Every failed mechanic has a fixed cost and fills the Wake bar by a known chunk.

Design references (structure only, nothing copied): [Croesus](https://runescape.wiki/w/Croesus)
and the [Gate of Elidinis](https://runescape.wiki/w/Gate_of_Elidinis).

## Who he is

A saint of Cruor, martyred for the Keeping and buried under a monument so the faithful's blood
would pass over him on its way to the bay. Something keeps waking him (the open question of the
lore: whether it is the dreams, the moons or the covenant itself). His shouts are Cruor's own
teaching turned sharp: the faithful *owe* their blood to the Keeping, the believer must prove it,
silence is reverence, and light reveals. Cruor keeps the crypt sealed and sends the faithful down
to sing him back to sleep. Placeholder name; to be named with the lore.

## Arena

A crypt nave beneath a Cruorian monument, about 28 tiles long and 18 wide, with the stair down at
the south end and the Saint's bier on a dais at the north end. The floor is open: no cover, no
line-of-sight puzzles. Everything in it reuses standard props.

| Prop | Count | Position | Role |
| --- | --- | --- | --- |
| The Saint | 1 | Bier on a raised dais, north end | Static model. His eyes open, the lamps gutter and a choir swells as the Wake bar fills |
| Holy symbols | 4 | On pillars, two each side of the dais | Light up in sequence for Are you a believer? Each has its own shape and sound |
| Altar | 1 | Centre of the nave | Pour phials into the Requiem; channel cleanses |
| Offertory | 1 | Beside the altar | Owed-blood deliveries, and the Pay what you owe payment option |
| Near founts | 4 | About 5 tiles from the altar | Normal phial yield |
| Far founts | 4 | About 11 tiles out, in the corners | Richer yield (value to be tuned) |
| Safe sigils | 6 | Floor, around the edges and between founts, 1 tile each | Pay what you owe lights 3 of them. No spot is more than about 5 tiles from a sigil |

Each fount has a 3x3 tile footprint, which is the ring that Let there be light uses. The altar
sits in the centre on purpose: every pour is a trip, and Hold your tongue turns into a walk back to
the middle. Believer's projectiles travel down the middle of the room, because deflecting is a
switch and not a position.

**Instanced.** Each run is its own copy of the crypt, so the Saint, the founts and every bar are
yours alone. Its region sits far from the rest of the world's map; you reach it through a sealed
crypt door at a monument.

## Core loop

You win by filling the Requiem before the Wake bar fills, using two cleanses at the altar.
Phials are the resource that links the two.

1. **Prepare.** Enter with 28 inventory slots and decide how many to leave for food. Arrive with
   devotion for your overheads.
2. **Give blood.** Work a fount: as on any monument, it cuts you open and you give a little at a
   time. Each success fills a **blood phial**, which is non-stackable and takes one slot. Far founts
   yield more.
3. **Pour.** Carry phials to the altar. Each one sings a little more of the Requiem and gives
   Religion XP.
4. **Cleanse.** At 50% Requiem, channel at the altar for 3 seconds. Shouts pause during the
   channel, and it restores a third of your devotion. At 100%, do it again: the second cleanse
   lays him to rest and wins the run.
5. **Survive.** Between pours, answer the shouts and owed blood (below).

Inventory is the central tension. A full pack means fewer trips, but it leaves no room for food,
and food is what keeps you alive through Pay what you owe and Are you a believer?.

The cleanse is a proposal: confirm the 3-second channel, the 50% and 100% thresholds and the
devotion it restores.

## Wake bar and fail states

The Wake bar is the pressure meter: it fills slowly by itself and jumps when you fail a mechanic.
If it reaches 100%, the run fails with no payout, the same as dying in a boss fight.

| Rule | Value |
| --- | --- |
| Passive fill | Empty to full in about 5 minutes |
| Failed mechanic | +15% in one chunk |
| Clean run | Ends at about 65 to 70% after 3 to 3.5 minutes |
| Full bar | Run fails, no payout |
| Payout | Scales with the bar remaining at the win |

Speed is the safety margin. A 3-minute run can absorb two slips. A 4:30 run meets more shouts and
an extra owed blood, so a single slip can end it. The Wake bar is shown in the UI and felt in the
room: the Saint's eyes open, the lamps gutter and the choir swells as it fills.

## Owed blood

Owed blood is the one mechanic on a fixed timer: about every 60 seconds the Saint marks you as
owing the Keeping, and you must pour phials into the offertory within a window.

- **See:** a marker appears over your character and a counter shows the amount owed.
- **Do:** pour 12 phials into the offertory within 15 seconds. Waiting until the last second is risky.
- **Fail:** +15% Wake and damage as well.
- **Cost and skill:** paying burns phials you could have poured into the Requiem, so it costs
  progress, but it frees slots. Keeping spare phials ready for the debt is how good runs avoid
  wasting trips.
- **Timing rule:** a marker never lands during an active shout. It waits until the shout resolves.
  Shouts also hold off while an owed-blood window is open.

In a clean run, expect three, at about 0:50, 1:50 and 2:50. Damage for a failed one is still to be tuned.

## Shout system

The Saint speaks one shout at a time, chosen at random from a pool of four, so no run can be
memorised. Escalation comes from a shrinking gap and harder variants, never from overlapping shouts.

- **One at a time.** A new shout never starts while another is active or while an owed-blood window is open.
- **Random order.** Shouts are drawn from the pool. Avoid the same shout twice in a row (to be confirmed).
- **Shrinking gap.** The pause between shouts falls as the fight goes on: 15 seconds, then 12, then 10.
- **Harder variants.** Later shouts use shorter delays, longer silences and longer sequences.
- **Every shout is telegraphed.** A voice line and a visible cue come first. The cue never changes,
  so players can learn it.
- **Failing any shout** adds a 15% Wake chunk, plus the shout's own damage.

## Shout: Pay what you owe

The Saint demands the blood the faithful owe, and you either move to a safe tile or pay in
phials before the delay ends.

- **See:** the Saint says "Pay what you owe." Three of the six safe sigils light up.
- **Do:** stand on a lit sigil within about 4 seconds. Alternatively, pour 10 phials into the
  offertory in the same window (the offertory option is a proposal, to be confirmed).
- **Fail:** 5 hits of 15% max blood, which is 75% in total, plus a 15% Wake chunk.
- **Skill:** work near a sigil, and decide whether running or paying costs less progress.
- **Assets:** a sigil glow decal, a voice line and a hit sound.

## Shout: Hold your tongue

The Saint demands reverent silence, and any skilling action during it is punished.

- **See:** the Saint says "Hold your tongue." The choir drops out and a hush effect shows.
- **Do:** stop all skilling for about 3 seconds. You can still walk, eat and pour.
- **Reward:** the first pour after the silence ends sings bonus Requiem.
- **Fail:** any skilling action in the window is a hit (about 20% max blood, to be tuned) and a 15% Wake chunk.
- **Skill:** time a full pack to arrive at the altar just as the silence ends, which turns the
  shout into route planning. It also fights the habit of tapping founts on autopilot.
- **Assets:** a hush effect and a voice line.

## Shout: Are you a believer?

The Saint lights four holy symbols in sequence, then fires four projectiles in that order. Sing
the matching overhead hymn as each one lands and it is **blocked completely** (in this fight only;
elsewhere deflection halves damage).

- **See:** the Saint says "Are you a believer?" The four symbols (two each side of him) light up
  one at a time, about 0.7 seconds each, in the colours of the three combat styles. Colours may
  repeat. Each symbol also has its own shape and sound so the sequence does not rely on colour alone.
- **Do:** after a 1.5-second gap, four projectiles come at about 0.8-second intervals in the same
  order. Switch to the matching overhead before each one lands: **Bulwark** (melee), **Glare**
  (ranged), **Salt Ward** (occult). Only one overhead is up at a time, so this is a switch. You
  can keep giving blood throughout, because singing a hymn does not interrupt a fount.
- **Damage:** each unblocked projectile deals 30% of max blood. Four misses deal 120%, which is
  lethal even from full blood. Taking any earlier damage makes three misses lethal.
- **Wake:** a 15% chunk if any projectile is missed (per miss is an open question).
- **Skill:** memorising a sequence under load, keeping devotion for the switches, and arriving at full blood.
- **Escalation:** longer sequences at higher tiers (4, then 5, then 6). Three symbols only: there
  are three deflection hymns.
- **Rules:** no fake projectiles, and no other shout overlaps it.
- **Requirement:** all three overheads, so the lowest tier needs Religion 41.
- **Assets:** four lit symbols, four projectiles and four sounds.

## Shout: Let there be light

Light rises around some of the founts and they give more the longer you stay, until it scorches
whoever is still inside. It is a harsher cousin of the monuments' hungry founts.

- **See:** the Saint says "Let there be light." Three of the eight founts get a ring of light on
  the 3x3 tiles around them. The other five stay normal.
- **Ramp:** the light brightens for exactly 10 seconds, with a rising hum. Yield from a lit fount
  climbs from 1x to 3x, reaching 3x in the last 2 seconds. The timing never varies, so it is a
  learnable skill.
- **Scorch:** a 0.5-second white flash, then a burst. Anyone still inside takes 25% max blood. The
  fount goes dry for about 20 seconds.
- **Wake:** a 15% chunk if you are caught (to be confirmed, so that every fail has the same cost).
- **Safe play:** work an unlit fount at the normal rate, or step out of the ring before the flash.
- **Skill:** work a lit fount until the last safe moment, with free slots ready for the 3x window.
  Which three founts light up is random, so route to the nearest one.
- **Assets:** a ring decal, a brightness effect, a dry-fount variant and four sounds.

## Optional risk: Bleed for it

Bleed for it is an optional deal that trades blood for speed, Cruor's bargain in miniature, and it
is only worth taking if you kept food slots free. A proposal, not yet confirmed for the first release.

- **Do:** pay 30% of max blood to double Requiem gain for 10 seconds.
- **How:** a blood *cost*, like a verse's: it can't take your last blood, and it isn't a hit, so
  nothing reacts to it. (Verses themselves can't be read here: they need a melee weapon and a fight.)
- **Why it works:** it competes with phials for slots, because the food you need to recover takes inventory space.
- **Interaction:** unavailable while a shout is active, so it never stacks with Pay what you owe or Believer.

## Reference run and skill expression

A clean run takes about 3:30 and finishes with the Wake bar near 65 to 70%. The shout order is
random, so the times below show pacing, not a script.

| Act | Time | What happens | Wake |
| --- | --- | --- | --- |
| 1. Learn the room | 0:00 to 1:10 | First pours, a first shout from the pool, owed blood at about 0:50 | About 20% |
| 2. Pressure | 1:10 to 2:20 | Cleanse #1 near 1:40, owed blood at about 1:50, shouts every 12 seconds | About 40% |
| 3. Finish | 2:20 to 3:30 | Shouts every 10 seconds, owed blood at about 2:50, cleanse #2 ends the run | About 65 to 70% |

A 4:30 run faces a fourth owed blood and many more shouts at the fast cadence, and ends with the
bar near 95%, so one slip fails it.

**What separates efficient from slow play**

- **Routing:** short paths between founts and the altar, and sending far trips to the richer founts only when the pack is empty.
- **Pre-stocking:** keeping the exact 12 phials for the next owed blood ready, then pouring them the moment the marker lands.
- **Reading tells:** standing near a sigil before a shout and near a lit fount before a ring.
- **Greed control:** staying in a light ring until the last safe moment.
- **Slot management:** balancing phials against the food needed for Believer and Pay what you owe.
- **Devotion:** overheads drain it and nothing restores it in the crypt but the cleanses, so sing them only for the projectiles.
- **Choosing risk:** taking Bleed for it only when food is in the pack.

## Rewards and progression

A win always pays pledges, so the scriptures your verses read are bought and never left to
chance. Chance only affects the extras.

- **Pledges:** guaranteed, scaling with score and the Wake bar remaining. A minimum score is
  needed for any reward. Spent on scriptures at Cruorian characters (`skills.md` 4.3).
- **A verse codex:** a rare drop that teaches one of the later verses (`skills.md` 4.2 leaves room
  for boss codices). Which verse is to be decided.
- **Offering chest:** more score means more rolls on a table of materials useful to other skills,
  such as logs, ore, fish, herbs, seeds and hides.
- **Extras:** a boss pet, cosmetics and collection log entries.
- **XP:** Religion XP for every phial filled and poured; a run should give about 1.5x the pace
  table (`skills.md` 2.1), three times AFK empowering at a monument.
- **Tiers:** the same encounter in higher tiers (for example Vigil, Matins, Compline), unlocked by
  Religion level on the tier ladder, for example 41, 61 and 81. Higher tiers add faster gaps,
  longer Believer sequences and better rewards. Level also raises fount success and phials per action.

Empowering a monument's founts gives the same currency at a lower rate, nearly AFK, so the boss is
the efficient route and not the only route.

## Group scaling

Solo first; group support is an open decision.

- **Solo:** the base numbers in this spec.
- **If groups are supported:** add founts (for example 12 or 16 in total) and more lit rings per
  Let there be light. The room itself does not change.
- **Per-player mechanics:** owed blood, Hold your tongue and Believer apply to every player
  separately. Pay what you owe lights extra sigils.

## Dependencies, assets and tuning

### What exists already

- **Overhead hymns** for each style (Bulwark, Glare, Salt Ward), on devotion, one at a time, shown above the singer's head.
- **Devotion:** 10 per Religion level, no natural regeneration.
- **Founts** as gathering nodes (the cut, yields, surges), pledges, Cruorian scripture vendors.
- **Blood costs** that can't take your last point; non-stackable items, a 28-slot pack, food.

### What it needs

- **Instancing:** a private copy of the crypt per run, entered through a door and left on win, loss or logout.
- **An encounter system:** a scripted fight with phases, timers, random shout draws, area effects
  (rings, sigils), telegraphed projectiles by style, and run state (Requiem, Wake, owed blood).
- **A "fully blocked" rule** for the Saint's projectiles when the matching overhead is up.
- **Run UI:** the Wake and Requiem bars and the owed-blood counter.

### Asset list

- The Saint on his bier, a dais and a crypt nave
- Four pillars with symbol glows, plus four symbol shapes
- One altar and one offertory
- Eight copies of one fount prop, plus a dry variant
- Decals: light ring, safe sigil, and an owed-blood marker
- Four projectiles
- Lighting states for the Wake bar, a choir loop and voice lines for four shouts
- UI: Wake bar, Requiem bar, owed-blood counter

### Tuning table

| Value | Placeholder |
| --- | --- |
| Nave size | 28 x 18 tiles |
| Founts | 4 near (5 tiles), 4 far (11 tiles) |
| Safe sigils | 6, with 3 lit per shout |
| Passive Wake fill | About 5 minutes empty to full |
| Failed mechanic | +15% Wake |
| Owed blood interval and window | 60 s and 15 s |
| Owed blood | 12 phials |
| Shout gap | 15 s, 12 s, 10 s |
| Pay what you owe | 4 s delay, 5 hits of 15% max blood |
| Hold your tongue | 3 s silence, about 20% max blood per hit |
| Believer | 4 symbols at 0.7 s, 1.5 s gap, projectiles every 0.8 s, 30% max blood each, blocked fully by the matching overhead |
| Light | 3 of 8 founts, 10 s ramp, 1x to 3x yield, 25% max blood scorch, 20 s dry fount |
| Bleed for it | 30% max blood for 2x Requiem for 10 s |
| Cleanse | 3 s channel at 50% and 100% Requiem, restores a third of devotion |
| Target run | 3 to 3.5 min, Wake at 65 to 70% |

## Open questions

- [ ] Should far founts be much richer than near founts, or just a bit richer?
- [ ] Confirm the cleanse: a 3-second channel at 50% and 100% Requiem, and the devotion it restores.
- [ ] Should Believer add a Wake chunk per miss, or once per sequence?
- [ ] Should being caught by the Light scorch add a Wake chunk as well as damage?
- [ ] Is the offertory option for Pay what you owe in the first release?
- [ ] Is Bleed for it in the first release?
- [ ] Solo only at launch, or groups too?
- [ ] Should the same shout be allowed twice in a row?
- [ ] Damage as a percentage of max blood, or flat per tier? Max blood grows with Defence while
      food heals flat amounts, so percentages cost high-Defence players more food.
- [ ] Which verse the codex teaches, and its drop rate.
- [ ] The Saint's name, which monument he lies under, and what wakes him.
- [ ] Exact payout bands, tier names and XP rates.
- [ ] Failed owed-blood damage and the Hold your tongue hit size.

Next steps: block out the crypt, build instancing and the encounter system, playtest the pacing
target, then tune the table above.
