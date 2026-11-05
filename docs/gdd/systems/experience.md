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

| Level | Total XP |
| --- | --- |
| 1 | 0 |
| 2 | 83 |
| 50 | 101,333 |
| 99 | 13,034,431 |
| 100 | 14,391,160 |

The full table comes from the curve, not from typing it out.

## Open questions

- Is this the curve we want? The current build uses it, but it is RuneScape's own growth formula
  stretched to level 100, and R'th should have a curve of its own.
- What happens past level 100: nothing, or a use for the extra XP?
- Should there be a combat level, one number for how strong a fighter is? If so, which skills
  count toward it, and how is it described without copying RuneScape's?
