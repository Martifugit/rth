# Fishing in depth

Fishing end to end: the three ways to fish, the ten species, trophy angling (finding, rigging,
the fight, the log, markets and the collector), how players learn it, and how it ties into the
other skills and the story. `docs/skills.md` (Fishing) keeps the summary; this file is the full
design. Work packages are AN1-AN9 in `docs/plan.md`.

**Status (2026-11-02).** Decided with the owner: the ladder fish are the trophy species; the fight
is a shared, deterministic simulation the client plays live and the server replays from the
client's inputs; the rig is per-player state, not an item; trophies come in size classes;
rare variants are **revised fish** and the collector is a scholar of the revision. New systems are
prefixed `Angling` so they can't collide with work elsewhere. All numbers are starting values to
tune by playing; names are provisional.

---

## 1. Three ways to fish

| Layer | What it is | Pace | Built |
|---|---|---|---|
| **Spots** | Cast at a spot and wait; the spot roams instead of running out. | AFK, about half the pace table | yes (E4) |
| **Reading the water** | Bubbles show where a spot is about to come back; be there first for a stack of Fish whisperer. | active, about 1x | yes (E4) |
| **Trophy angling** | Find a species' active trophy spot, rig for it, fight the fish in the reeling panel. | engaged: about 1.2-1.5x XP, plus the log, fish boxes and rare gear | AN1-AN8 |

On top of these, some tiers have a **method quirk** (eel traps, cutting ice for char, the
lightfin lantern, the hushfin event). Each quirk has a trophy form too, so trophy angling takes
players through the same places as ordinary fishing.

---

## 2. The species

The tier ladder's fish (`docs/skills.md` 2.2) are the trophy species. Each tier adds **one new
move** to the fight (or one rigging lesson), so climbing the ladder teaches the fight a piece at a
time. A species' trophy level is its tier's level plus 5 (perch, the first trophy, at 10).

| Tier | Fish | Trophy level | New in the fight | Rigging lesson | Its ripple (the sign) |
|---|---|---|---|---|---|
| 1 | Perch | 10 | turns left and right: pull against it | any rig lands one | small quick rings |
| 2 | Eel | 15 | **dives**: pull up, or it rests | bait-style lures | rings with bubbles |
| 3 | Pike | 25 | **runs**: let go and give line | a heavy reel; a wire leader (hook) or it bites through | a ripple drifting in a line |
| 4 | Lamprey | 35 | **latches** to a stone: go slack to free it | a strong hook | a still V |
| 5 | Cistern carp | 45 | **holds deep**: a long dive to wait out, pulling up | patience; thick line is fine | slow, wide rings and a little mud |
| 6 | Mullet | 55 | nothing new, but **line-shy** | thin line, or it never bites | a nervous scatter |
| 7 | Ice char | 65 | **jumps**: let go before it lands | a forgiving hook; you cut the hole yourself | breath-fog over the hole |
| 8 | Lightfin | 75 | **darkness**: its tells show only while your lantern is lit | the lantern is part of the rig | a faint glow under the surface |
| 9 | Hushfin | 85 | **no tells**: read its body alone | your best rig | none: event only |
| 10 | Sturgeon | 95 | every move, slow and very long | stamina parts | a huge slow swirl: event only |

Fight patterns in short:
- **Perch**: gentle turns, long tells. The tutorial fish.
- **Eel**: turns and dives, rolls out of a dive into a turn.
- **Pike**: quick switches left and right, sudden runs.
- **Lamprey**: turns, latches; tiring it is slow while it holds a stone.
- **Cistern carp**: dives often and holds there; strong but slow to turn.
- **Mullet**: quick and twitchy, short runs; the challenge is getting a bite on thin line.
- **Ice char**: swims straight, runs, and jumps.
- **Lightfin**: turns and runs; in the dark only your lantern shows its tells.
- **Hushfin**: everything quick, no tells shown at all.
- **Sturgeon**: dives, holds, runs, jumps, all slow and heavy. The 3-minute fight.

**Rare variants: revised fish.** About one bite in 60 is a fish the revision is partly remaking: a
second dorsal fin, scales that catch a colour that isn't there, eyes on the wrong side. It shows
from the moment it bites (the panel's fish is visibly wrong). It is the same species' moves,
faster and more often, with shorter tells, and more energy. Each species' revised fish is wanted by
the collector (section 7).

**Where they are.** Species follow their biome (`docs/skills.md` 2.2): low tiers in lowland rivers
and lakes, high tiers at the edges of the map. Each species has **5 trophy spots** across its
home waters, **2 active** at a time. Placement comes with the resources pass on the new map (A4);
until then the test spots are in `kittest`.

---

## 3. Trophy angling

### 3.1 Finding the fish
- A trophy spot shows **its species' ripple** while it is active, and nothing while it isn't.
  Signs show the species, never the size.
- Casting at a species you're too low for: "Pike requires level 25 Fishing to hook." This teaches
  which ripple is which long before you can catch it.
- **Active and inactive.** Each species keeps 2 of its 5 spots active. A spot stays active for
  20-40 minutes; when it ends, another of that species' inactive spots becomes active, so the count
  stays the same. The same for everyone.
- **Time left** shows on the water: plenty of ripples, thinning, nearly gone. No numbers.
- **Spooking.** After 4 catches at a spot, its fish are spooked **for you** for 10 minutes: you
  see no signs there. Other players are unaffected, and the spot's own time doesn't change.

### 3.2 The session
1. Visit a fish market: hand in trophies, pick up the lead for an active spot.
2. Travel there; look for the ripple.
3. Rig for the species and cast.
4. A fish bites after a short wait; the reeling panel opens. Fight it and land it: its weight is
   shown, with "New personal best!" when it beats your record.
5. Fish on until the spot is spooked or ends, or your pack is full of trophies (they don't stack).
6. Move on, or go back to a market.

About 15-25 trophy attempts an hour, counting searching, travel and waiting.

### 3.3 The fight

**In the world, not in a box (owner, 2026-11-02).** The first version put the fight in a little
panel with keyboard controls; the owner rejected it outright (disconnected from the game, awkward
controls, ugly animation). The fight is now drawn in the world from effects, like a fishing spot:
the **fish** is a dark shape under the water, nearer as it comes in, swinging to the side it swims,
sinking as it dives, gone in a splash as it leaps (revised fish are tinted wrong); the **line** runs
from the rod tip to it and *is* the tension gauge (sagging when slack, straight when taut, amber,
then red and trembling near snapping); **tells** are water effects (a swirl on the side it will turn
to, bubbles before a dive, a wake opening before a run, the surface boiling before a leap); and how
much **fight** it has left shows in how often and how hard it thrashes. No panel.

**Legible first (owner, 2026-11-02, after trying it):** everything is drawn over the scene
(`Shaders/AnglingOverlay`), so trees and roofs never hide the line or the fish; the line starts at the
held rod's real tip and has a dark edge; the camera swings round behind you when a fish bites; over the
you, in the interface's own window and bars (owner: the first gauges and line looked out of place, and
followed the fish about), one gauge over your head (tension bar up its left side, owner: one piece, not two): the fish's **energy** with **chevrons** pointing the way
to pull against it (amber for its tell, green while you answer it, as many lit as you pull hard; a pause
mark when it runs or leaps: let go) and below it **how much line is in** (full at the net: landed); and
beside you a vertical **tension bar** with its zones (slack, safe, strained, snapping) and a mark for where
the line is. Nothing flashes. The line is a plain thin line that bows out to the side the fish pulls; you
pull the other way, harder the further out the cursor is. **The fish tires only from being fought** (owner: it was impossible to fail by keeping the line green and ignoring it): pulling against it drains it, harder the harder you pull, faster while you reel and the nearer the line is to the red; a turn you don't answer yanks the line towards the red. **The challenge is tiring the fish** (owner): a
spent fish gives up whatever it was doing, never gets its breath back, and is reeled in within seconds,
while a fresh one comes in only slowly; and spray off the fish when you pull it the right way. Tension moves slowly enough for people (reel +5,
give -6 a step; a rod's red allowance and a hook's slack allowance count double and triple), and
letting go during a leap drops the line slack fast. **One fish a cast:** after a fight, landed or
lost, you cast again yourself.

**Controls are an experiment** (`AnglingFightView`, Settings > Fishing), to be judged by playing
and against other games (Sea of Thieves and Red Dead Redemption 2 keep the fish and line in the
world and have you push against the fish; WoW and Animal Crossing are pure timing; Stardew,
Albion and Dredge use a small minigame box, the approach rejected here):
- **Steer (default):** the cursor is the rod while the fight lasts, without clicking (a click is the
  world's). Hold **Space** to reel (owner, 2026-11-02: not a mouse button);
  sweep the cursor across the line to the side the fish isn't swimming; pull it back towards
  yourself to lift against a dive; let go when it runs or leaps.
- **Reel only:** hold Space to reel and let go on strain, runs and leaps; your angler leans against the
  fish by itself.

Both send the same inputs to the same simulation, so a new scheme is client code only.

The rules underneath, whatever the controls:
Controls: **hold to reel** (it comes in, tension rises); **let go** to give line (tension falls,
it drifts out a little); **pull left, right or up** to work against it.

| The fish | You | Get it wrong |
|---|---|---|
| faces left | pull right | pulling its way barely tires it |
| faces right | pull left | pulling its way barely tires it |
| dives | pull up | left slack, it rests and gets energy back |
| runs (faces away) | let go | reeling into a run sends tension into the red |
| jumps | let go before it lands | too tight on landing and the hook pulls free |
| latches (lamprey) | go slack until it lets go | reeling against the stone frays the line |

Every move has a short **tell** first (a flick of the tail before a turn). Correct pulls are the
main way to drain energy. A fresh fish can't be reeled straight in: reeling brings it closer only
as fast as its energy allows.

- **Losing it:** the line snaps when tension stays in the red longer than the rod allows (a short
  buffer, so one bad moment is not the end); the hook pulls free after too much slack for too long,
  or if the line is tight when a jump lands. Doing nothing lets the fish rest and drift away; if it
  gets far enough out, it's gone.
- **Long fights stay fun:** after a run it swims straight and slowly for a few seconds (a breather,
  reel in calmly); a fresh fish runs often and a tired one hardly at all; **a spent fish (energy gone) stops fighting**
  and swims where it's led, so the end of a fight is reeling it in (owner, 2026-11-02: the end got harder, not easier).

| Fish | Fight length |
|---|---|
| small for its species | 15-25 s |
| average trophy | 30-60 s |
| big trophy | 1-2 min |
| revised | 1.5-3 min |

**How it runs (decided).** The fight is a deterministic simulation in `/shared` (`AnglingFight`),
in whole integers on fixed 50 ms steps, so .NET on the server and Mono/IL2CPP on the client give
exactly the same result. The server rolls the fish (species, weight, revised or not, a seed) and
sends it; the client plays the fight live and sends its inputs in small batches as the "angling"
intent; the server steps its own copy of the fight through those inputs and decides the outcome.
No anti-cheat: the replay keeps client and server agreeing, nothing more.

### 3.4 Levels
- Each species needs its trophy level to hook (below it you still see its ripple).
- **Reeling bonus:** each level above a species' trophy level makes reeling a little faster
  (1.5% a level), up to 10 levels. Fish make the same moves, so skill still matters; high levels
  can't land low fish almost at once. Revised fish get the same bonus and stay hard.

---

## 4. Rigging

**The rod is the rig (owner, 2026-11-02).** A rod is a tiered item you **wield in the main hand**
(people want to show off a good rod, or stand at the bank with one) or carry. Right-click it,
**Rig**, and a rig window opens: the rod drawn across it with its slots along it. Parts go in from
the pack (dragged onto a slot, or "Rig" on the part) and come out with a click. Each kind of rod
keeps its own rig. The rod fished with is the one in hand, else the best in the pack.

**How a rod can be rigged:**
- Its **tier** caps its parts' tier: a tier-3 rod takes parts of tier 3 and below, so rods carry the
  progression.
- Its **kind** decides what fits (each part lists the rod kinds it fits; none listed fits all):
  a **fly rod** (long and whippy: the most time in the red; flies and fine lines), a **spinning
  rod** (the all-rounder: spinners and lures, the starter rod) and a **ledger rod** (short and heavy:
  bait on the bottom, heavy reels with drag). Choosing the rod is the first rigging decision.
- Every rod has a reel, line, hook and lure slot. From tier 5, rods gain a **leader**, **float** or
  **lantern** slot (the slots exist; the parts come with those tiers). Collector rods may have slots
  no crafted rod has.

| Slot | Made by | From | What it does in the fight |
|---|---|---|---|
| **Rod** (the rig itself) | Artifice | a Woodcutting blank (alder, ash, then sere cedar, pale pine, moonbark…) | how long tension may stay in the red before the line snaps; its kind and tier decide what fits |
| **Reel** | Artifice, with Smithing fittings | wood, bars | how fast you reel; how much line a run pulls out |
| **Line** | Tailoring | Foraging fibre (reed, flax, silkweed, threadmoss, gossamer); Butchering gut for leaders | how much tension before it snaps; its **thickness** (wary fish won't bite thick line) |
| **Hook** | Smithing | bars | how forgiving it is of slack and jumps; a wire leader for pike |
| **Lure** | Tailoring (flies: Butchering feathers and fur), Smithing (spinners), Foraging/Butchering (bait) | | which species bite at all |

- **Trade-offs, not a ladder.** Thick line is strong but line-shy fish won't bite it; a heavy reel
  handles pike runs but reels slowly. A mid-level rig made right beats a top rig made wrong.
- A rod and a lure are the least you can cast with; an empty slot fights with a poor bare part.
  Anyone can trophy-fish without high making levels; the big catches are harder and rarer without
  good parts.
- Higher-tier parts need the matching making skill to make, never Fishing; using a part needs
  Fishing at its tier (rule 2.5: requirements are hard limits).

### 4.1 Wear and upkeep
Parts wear mostly from **how** you fish, so clean fights save upkeep.

- **Line** frays while tension is in the red. **Hooks** dull a little each catch, more on hard
  fights. **Reels** wear from long runs. **Rods** wear least. Lures wear like hooks.
- A part's condition (0-100) shows on the rig; it **loses its bonus gradually** (at 0 it gives
  60% of its stats) and never breaks mid-fight.
- Higher tiers wear a little faster and need rarer materials to mend.
- **Mending (decided):** anyone can pay a market's **tackle-maker** (coins, by the part's value and
  wear). With the part's making skill at its tier you can mend it yourself at a **tackle bench**
  from materials, much cheaper. Never a chance of failure.
- A worn part taken off a rod keeps its condition (remembered by item kind); rods wear too.
- **Collector parts** can't be made; the collector mends them, for a fee or materials from rare fish.

---

## 5. Rewards

Every catch gives **XP**, a **weight** for the log and a **trophy** to hand in.

- **XP** scales with the fish's size (and three times for a revised fish), so a good trophy is
  worth much more than the same time at an ordinary spot.
- **Trophies come in size classes** (small, good, great, record; and revised), each its own item,
  generated from the species (`trophy_pike_great`). The exact weight goes into the log the moment
  the fish is landed. Trophies don't stack, so a trip ends when the pack is full.
- **Two reveals:** on the bite (is it revised?) and on landing (the weight, and a personal best).

### 5.1 The fishing log
A journal tab:
- **Trophies:** each species' catches, personal best, revised fish caught. Handing a fish in never
  removes it from the log.
- **Spots:** every trophy spot you've seen, by species (never marked active, except a market's lead).
- **Angler's notes:** per species, written as you learn: the moves you've seen it make, the lures
  you've landed it on, and lines unlocked by catches and by people (`notes` in the species data:
  "after 3 catches", or a fishmonger's tip given in dialogue).
- Later, **lost species** (section 8).

---

## 6. Fish markets

A market is a fishmonger NPC in a fishing town, one per region with good water.
- **Hand in** trophies: each becomes a **fish box** for its species and size class
  (`fishbox_pike_great`), opened from the pack. A box holds fish centred on the species' tier, some
  a little below and a few above; bigger classes give bigger boxes. On average a box matches what
  you'd have caught AFK in the time the trophy took.
- **The lead:** each market names one active spot for each species its region trades in ("Pike
  have been rising to the north-east of here, a good walk off"), and adds it to your log.
- **Tips:** the fishmonger also unlocks angler's notes in conversation.
- A **tackle-maker** stands by most markets (mending, section 4.1, and the low tiers' parts for sale).

---

## 7. The collector and rare gear

**The collector is a scholar of the revision**, who wants revised fish as evidence, not as trophies:
each is proof that the world is being remade, caught in the act. That draws anglers into the main
mystery (`docs/lore.md` I, V).

- Each **revised species maps to one piece of gear**, so you know what you're hunting for. Handing
  one over gives the piece and a large fish box. Duplicates are possible (a box only, after the first).
- Collector gear slots into the rig like crafted parts. It's best when it **removes a trade-off**
  rather than adding bigger numbers:

| Revised… | Gives | The trade-off it removes |
|---|---|---|
| perch | a lucky float lure: perch, eel and pike all bite | carrying three lures |
| eel | a slipping reel: runs pull out less line | reel speed against run drag |
| pike | a steel-trace hook that never dulls on teeth | hook wear on toothy fish |
| lamprey | a stone-shy line: frays far less when latched or in the red | line wear |
| cistern carp | a patient rod: a long buffer in the red | rod stiffness against buffer |
| mullet | ghost line: thin and strong | thickness against strength |
| ice char | a sprung hook: jumps never pull it free | slack against jumps |
| lightfin | a lantern-lure: lights the fight and draws the bite | a lantern and a lure in one |
| hushfin | a listening reel: shows a faint tell for every move | reading without tells |
| sturgeon | **the keep net**: holds 4 extra trophies | trip length |

---

## 8. Fishing and the rest of the world

### 8.1 The other skills

| Skill | Gives Fishing | Takes from Fishing |
|---|---|---|
| **Cooking** | | fish (heals by tier), fish boxes, roe |
| **Medicine** | reel grease | fish are **phlegmatic** (cold and wet), so fish extracts are the natural base for **phlegm** potions; isinglass from swim bladders; lamprey teeth, eel slime, scales |
| **Butchering** | feathers and fur for flies, gut leaders, offal bait, tallow for grease | big catches (pike, sturgeon) gutted for roe, bladder, bone |
| **Artifice** | rods, reels, traps, lanterns, nets | its supplier market (already decided) |
| **Tailoring** | lines, flies, nets | |
| **Smithing** | hooks, spinners, reel fittings, the ice auger | |
| **Woodcutting** | rod blanks, bark floats, resin | |
| **Foraging** | fibre for line; worms and grubs as bait | |
| **Occultism** | | brine for rites (already decided) |
| **Shortcuts** | | Fishing wades fords (already decided) |

**Bait** is the light, everyday tie: ordinary spots work without it, and bait from Butchering or
Foraging raises the catch chance a little through the existing `gatherSuccess` stat.

### 8.2 The story
- **Tolling's fishers** pulled the player out of Wennow (`docs/lore.md` VI), so they're the first
  people who know them. A short quest with them teaches spots, reading the water and the first
  perch trophy, and hands over a starter rig and the log. (Tolling has no townsfolk yet; the quest
  waits for its stage 3.)
- **Revised fish and the collector** (section 7).
- **Lost species:** species taken out in the last revision still swim in the Dreaming, in rivers
  that have since moved. A later Dreaming tie-in can let you fish a memory pool for log-only entries.
- **The landing's dark water:** one odd, log-only catch from the ferryman's jetty.
- **Moonfalls:** a crater lake can briefly hold lightfin trophy spots.

---

## 9. How it's built (extending it)

| To add... | Use |
|---|---|
| A species | An entry in `angling.json` `species`: tier, level, trophy spot object type, spots active, active ticks, spook rules, weight range, energy, its fight (`moves`: weights, tell and move lengths; `latch`, `jump`, `dark`, `noTells`), XP, box contents, notes. Trophy items and fish boxes are generated from it. |
| A trophy spot | A placed object whose type has a `cast` action and is a species' `spotType`. Place it in a region's data like any prop. |
| A rod | `tackle` with `"slot": "Rod"`, a `kind`, a `tier`, its `slots` (default reel, line, hook, lure), its `snapBuffer`; plus `equipment` in the Weapon slot (it is wielded) and `tool` (it fishes ordinary spots too). |
| A tackle part | `tackle` on an item in items.json: slot, `rods` (the kinds it fits), stats (`snapBuffer`, `maxTension`, `reelSpeed`, `runDrag`, `hookHold`, `thickness`), `bites` (lures), wear, `repair` (materials and the skill to mend it yourself). |
| A market | An NPC with the `hand-in` action (and `{anglingLead:species}` in its dialogue). |
| A collector trade | An entry in uses.json: the revised trophy on the collector. |
| A new fight move | A value in `AnglingMove` and its rule in `AnglingFight.Step` (shared), its counter in `AnglingBot`, and its look in the reeling panel. |

Systems: `AnglingTackleSystem` (rods and their rigs, wear, mending), `AnglingSpotSystem` (spots active,
spooking, spots seen), `AnglingSystem` (casting, bites, the fight, catches, hand-ins) and
`AnglingLogSystem` (the log and notes). Server state keys: "tackle", "anglingSpots", "anglingFight",
"anglingLog". The intent kinds: "tackle" and "angling".
