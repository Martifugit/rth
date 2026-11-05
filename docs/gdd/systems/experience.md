---
type: system
components: [tunables, progression]
groups: []
based-on:
status: draft
---

# Experience

## Intent

Experience is how doing a thing turns into getting better at it. Every skill levels on the same
curve, so a level means the same effort in [Woodcutting](../skills/woodcutting.md) as in
[Occultism](../skills/occultism.md), and early levels come fast while the last ones are long.

## Rules

1. Every skill has its own experience (XP) total and a level that follows from it.
2. All skills share one curve. No skill has its own.
3. Skills start at level 1 with 0 XP and cap at level 100. No skill is an exception.
4. The skill page says how its XP is earned; the curve only says what a level costs.
5. XP stops at a ceiling. Past it, a skill gains nothing more.
6. Each level needs more XP than the one before it.

## Interactions

- [Occultism](../skills/occultism.md), [Woodcutting](../skills/woodcutting.md) and every other
  skill: each links here for what a level costs and never repeats the numbers.
- [Vitality](../skills/vitality.md): its level sets max [blood](blood.md), so its curve is the
  player's staying power.

## Tunables

| Value | Number | What it's for |
| --- | --- | --- |
| Level cap | 100 | Where every skill stops |
| XP ceiling | 200,000,000 | The most XP any skill holds |
| Curve | each level costs a quarter of (level + 300 × 2^(level / 7)) more than the one before | Makes each level take longer than the last |

## Progression

The total XP needed to reach a level, from the curve above.

| Level | Total XP | Level | Total XP |
| --- | --- | --- | --- |
| 1 | 0 | 51 | 111,945 |
| 2 | 83 | 52 | 123,660 |
| 3 | 174 | 53 | 136,594 |
| 4 | 276 | 54 | 150,872 |
| 5 | 388 | 55 | 166,636 |
| 6 | 512 | 56 | 184,040 |
| 7 | 650 | 57 | 203,254 |
| 8 | 801 | 58 | 224,466 |
| 9 | 969 | 59 | 247,886 |
| 10 | 1,154 | 60 | 273,742 |
| 11 | 1,358 | 61 | 302,288 |
| 12 | 1,584 | 62 | 333,804 |
| 13 | 1,833 | 63 | 368,599 |
| 14 | 2,107 | 64 | 407,015 |
| 15 | 2,411 | 65 | 449,428 |
| 16 | 2,746 | 66 | 496,254 |
| 17 | 3,115 | 67 | 547,953 |
| 18 | 3,523 | 68 | 605,032 |
| 19 | 3,973 | 69 | 668,051 |
| 20 | 4,470 | 70 | 737,627 |
| 21 | 5,018 | 71 | 814,445 |
| 22 | 5,624 | 72 | 899,257 |
| 23 | 6,291 | 73 | 992,895 |
| 24 | 7,028 | 74 | 1,096,278 |
| 25 | 7,842 | 75 | 1,210,421 |
| 26 | 8,740 | 76 | 1,336,443 |
| 27 | 9,730 | 77 | 1,475,581 |
| 28 | 10,824 | 78 | 1,629,200 |
| 29 | 12,031 | 79 | 1,798,808 |
| 30 | 13,363 | 80 | 1,986,068 |
| 31 | 14,833 | 81 | 2,192,818 |
| 32 | 16,456 | 82 | 2,421,087 |
| 33 | 18,247 | 83 | 2,673,114 |
| 34 | 20,224 | 84 | 2,951,373 |
| 35 | 22,406 | 85 | 3,258,594 |
| 36 | 24,815 | 86 | 3,597,792 |
| 37 | 27,473 | 87 | 3,972,294 |
| 38 | 30,408 | 88 | 4,385,776 |
| 39 | 33,648 | 89 | 4,842,295 |
| 40 | 37,224 | 90 | 5,346,332 |
| 41 | 41,171 | 91 | 5,902,831 |
| 42 | 45,529 | 92 | 6,517,253 |
| 43 | 50,339 | 93 | 7,195,629 |
| 44 | 55,649 | 94 | 7,944,614 |
| 45 | 61,512 | 95 | 8,771,558 |
| 46 | 67,983 | 96 | 9,684,577 |
| 47 | 75,127 | 97 | 10,692,629 |
| 48 | 83,014 | 98 | 11,805,606 |
| 49 | 91,721 | 99 | 13,034,431 |
| 50 | 101,333 | 100 | 14,391,160 |

## Open questions

- Is this the curve we want? The current build uses it, but it is RuneScape's own growth formula
  stretched to level 100, and R'th should have a curve of its own.
- What happens past level 100: nothing, or a use for the extra XP?
- Should there be a combat level, one number for how strong a fighter is? If so, which skills
  count toward it, and how is it described without copying RuneScape's?
