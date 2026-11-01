# Boss design: research (2026-11-01)

*Why the first boss board failed, what studios that make memorable bosses actually do, and what that
means for R'th. Read before designing any boss.*

## Why the board failed

The owner called the 25 maquettes "a sloppy mess", terrible in concept as well as art. Both are true:

- **Concept.** Every concept was a one-line gimmick: "an animal, but its X is Y" (an anglerfish whose lure is
  a moon shard, a tortoise with a village on it). None had a role in the game, a story it ends, a place it
  belongs to, or a fight. They were creatures, not bosses.
- **Art.** Primitives (balls, cones, boxes, tubes) put together by numbers, judged at thumbnail size. No
  shape design, no proportion study, no material or colour thinking, no detail where the eye lands.
- **Process.** 25 at once guaranteed shallow work. Quantity was the brief's floor, and I treated it as the job.

## What the studios do

**Monster Hunter (Kaname Fujioka).** Every monster is designed as a living animal in an ecosystem: where it
lives, what it eats, what eats it, how it moves (studied from real animals). Designs that can't answer
those questions are thrown out, however striking. The game's structure comes first (what role each
monster plays at its stage), then concept and visuals together. Form follows behaviour: a charging
monster is reworked until its body says "charge". ([Vice](https://vice.com/en/article/the-developers-of-monster-hunter-explain-what-its-like-to-build-monsters),
[Game Informer](https://www.gameinformer.com/index.php/2019/07/09/monster-hunter-world-iceborne-bestiary-day-2-the-raging-grazer-banbaro),
[GameBanshee](https://gamebanshee.com/k4ak6), [OtakuKart](https://otakukart.com/monster-hunter-wilds-director-says-capcom-rejected-multi-headed-monsters-that-went-too-far-into-horror/))

**Shadow of the Colossus (Fumito Ueda).** A colossus's form is bound to how it is defeated; a design can't
be judged until it is built and played, and failed tests redesigned both colossus and arena. No two
colossi may share a way of being beaten. Of 48 planned, 16 survived.
([GlitterBerri: The Making of the Colossi](https://glitterberri.com/extended-developer-interview/making-of-the-colossi/),
[VGFacts](https://www.vgfacts.com/game/shadowofthecolossus))

**FromSoftware (Hidetaka Miyazaki).** Every boss carries meaning, and the character design holds a
contradiction: a pitiable monster, a beautiful horror, a hero become the obstacle. Bosses are the end of a
place's story; you learn who they are from the place before you meet them.
([Cook and Becker](https://www.cookandbecker.com/en/article/295/elden-ring-it-s-all-about-the-boss.html))

**World of Warcraft raids (Ion Hazzikostas).** Encounters often start from a single idea that is a mechanic
and a theme at once (a siege tank; what shamanism looks like in an army that despises demons), and avoid
"a boss standing in a square". ([Engadget](https://www.engadget.com/2014-04-28-blizzard-on-raid-design-evolution.html),
[Warcraft Wiki](https://warcraft.wiki.gg/wiki/WoW_Blog/Raid_Preview:_Siege_of_Orgrimmar))

**Readability (general).** A good fight telegraphs every attack so the player can read the wind-up; that
is what separates fair difficulty from cheap deaths. ([Bugnet](https://bugnet.io/blog/how-to-design-a-boss-that-teaches-its-own-pattern))

**Old School RuneScape (our style target).** Concept art feeds the model through hard limits: few colours
(dark tones especially), few animation transforms. ([summary of a Jagex concept-art talk](https://youtubesummary.com/summary/KKmjU_TiLA8))

## Lessons from specific bosses (from playing knowledge; check details before quoting)

| Boss | What makes it work |
|---|---|
| **Vorkath** (OSRS) | The end of a quest's story (Dragon Slayer II): an undead dragon frozen in a block of ice you break open. One body plan you know (a dragon) made specific by one state (undead, half-skeletal) and one staging (the ice). Each phase is a recognisable attack with its own colour. |
| **Zulrah** (OSRS) | A snake that dives and surfaces in a new spot in a new colour, each colour a different attack and a different answer. The whole fight is learning its rotation. The silhouette is simple; the identity is the rotation. |
| **Kalphite Queen** (OSRS) | A queen insect; killed once, it sheds into a second form. Body language says "hive queen" at a glance. |
| **Kraken** (OSRS) | Looks like four whirlpools; disturb them and the tentacles rise, then the body. The reveal is the arena. |
| **Abyssal Sire** (OSRS) | Starts asleep; you wake it. A demon fused to the floor of its lair by tentacles; the fight moves it from sleeping to rooted to free. |
| **Verzik Vitur** (OSRS, Theatre of Blood) | A character, not a beast: the vampyre who runs the theatre, enthroned at first, then standing, then a spider-like true form. Each phase is a reveal of what she is. |
| **Grotesque Guardians** (OSRS) | Gargoyles on a rooftop that are statues until they aren't; two of them, fought as a pair. |
| **The Nightmare** (OSRS) | Ties to a thread of the setting (dreams); the arena fills with things she makes; totems you charge to hurt her. |
| **Colossi** (Shadow of the Colossus) | Living architecture you climb; the body is the level. |
| **Rathalos** (Monster Hunter) | An ordinary-looking wyvern made iconic by behaviour: the king of the sky, poison talons, the dive. |
| **Ornstein and Smough** (Dark Souls) | Contrast as design: fast and thin with slow and huge, and the fight changes when one dies. |
| **Hornet** (Hollow Knight) | Small, fast, readable: every attack has a pose you learn; a rival before she's a boss. |

## What actually makes a boss (the brief for the next attempt)

1. **A role.** Where it sits in the game (a region's end, a quest's climax, a world event, a slayer-style
   endgame), and what level and group size it's for. Design follows the role.
2. **A story it ends.** Who it is, why it's there, what the player learned about it before the door.
   R'th's threads give every boss a reason (the bleeding god, the revision, the Dreaming, the moon, the
   seals). Contradiction makes it memorable: something pitiable, beautiful, wronged or sacred as well as
   dangerous.
3. **A fight bound to its form.** Its body says how it fights and how it's beaten; no two bosses share a
   way of being beaten. Phases reveal more of what it is. Attacks are readable at the game camera (pose,
   colour, projectile) and fit the tick system (600 ms, tiles, prayer-style answers).
4. **A place.** The arena is designed with it; often the reveal is the arena.
5. **A body you could believe.** Real anatomy and behaviour underneath (studied from references, as for
   the creatures), then one push into the fantastic. Few, strong shapes; a clear silhouette; detail only
   where the eye lands; a limited palette with one accent.
6. **Fewer, deeper.** One boss designed properly is worth more than a board of twenty-five ideas.

## The process to follow

1. Pick the slot (role and region) with the owner.
2. A one-page concept: role, story, contradiction, place, the fight in three beats (phases), the attacks and
   their answers, the body and why it moves as it does.
3. Reference boards: real animals or people for the body; game bosses for the finish and readability.
4. Silhouette studies: several, side by side, black against the sky at the game camera.
5. Owner picks; then a proper model, built by the creature method, reviewed in rounds.
