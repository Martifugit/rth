# Writing a Page

How to write a GDD page so it reads like a wiki: short pages that point at each other, each holding
its own facts and nothing else. It's for everyone who writes here, R'th AI included. The rules
themselves are in [The Charter](charter.md); this page is how to keep them while you write.

## The one test

For every sentence, ask: **is this about the page's thing, or about something it touches?**

- About the page's thing: write it here.
- About something else: name it, link it, and stop. Its facts go on its own page.

A page that explains its neighbors is a page that will contradict them. When a reader wants to know
what bile is, they click the link to Bile. They don't need it explained on every page that mentions
it.

## Link, don't explain

- **Link the first mention** of every other thing in each section: a place, a creature, an item, a
  skill, a system, a faction. Link it again in a later section if a reader might land there first.
- **Say only how it touches this page**, in a clause.
  `Builders raise [phlegm](../systems/phlegm.md)` is right for a skill page. What phlegm is,
  how high it goes and how it drains belongs on Phlegm.
- **Link to a page that doesn't exist yet.** That's a to-do, not a mistake. Pick its path from its
  type's folder and the [naming rules](workflow.md): `../systems/phlegm.md`, `../entities/old-mara.md`.
- **A fact you have for another page goes on that page.** If it doesn't exist, make it a stub from
  its type's template: the frontmatter, the title, and the fact under the heading it belongs to.
  Then link to it from here.
- **Never copy a number, a list or a rule from another page.** Link to the heading that holds it
  (`../systems/bile.md#rules`).

## Fan out, don't fill in

Grow the web one step at a time ([Charter II](charter.md)).

- Write the page you're on as far as the next thing to build needs ([Charter VIII](charter.md)).
  Everything else is a link.
- When one section starts explaining a system, a place or a group of things, that's a new page.
  Cut it down to one sentence and a link.
- A list of things that each deserve a sentence of their own (spellbooks, houses, tiers of a skill)
  is a group page, linked from here. Don't write the members out on the page that mentions them.
- Lists that gather from other pages ("drops", "found in", "what needs level 30") aren't written by
  hand. See [Lists are generated](how-the-gdd-works.md#7-lists-are-generated-not-written).

## The shape of a page

- **Frontmatter first**, as in [How the GDD Works](how-the-gdd-works.md#3-frontmatter). Fill in
  `components` and `groups` as the page gains them.
- **The title** is the thing's name, singular, in sentence case: `# Occultism`, not `# occultism`.
- **The type's core headings, in the template's order**, then components, then Open questions.
  Don't invent headings; don't rename them. An empty heading is better than an extra one.
- **Intent comes first and says why** ([Charter VII](charter.md)): what the thing adds that nothing
  else does, and what the player should feel. Two or three sentences. No numbers there.
- **Numbers last**, and only when someone is about to build or argue about them.
- **Unsettled things go under Open questions**, one line each, written as a question.

## Sentences

- **Plain and short.** One idea a sentence. Present tense: "the ferryman holds what you die with",
  not "the ferryman will hold".
- **Say it as a decision.** "Bile drifts back out of combat", not "bile could perhaps drift". If
  it isn't decided, it's an open question, not a hedge.
- **The player, not "you", in rules;** "you" is fine in How it feels, where the reader is the player.
- **No selling.** Cut "exciting", "rich", "deep", "unique", "immersive". Show the choice the
  player makes instead.
- **No preamble or summary.** Don't open with "This page describes..." or close with "In summary".
- **Bold lead-ins only in lists**, to name each item. Never bold for emphasis in prose.
- **Names as they are.** A placeholder keeps its `p-` prefix. Don't invent a name the docs don't
  have: link `p-` or ask under Open questions.

## Before you finish

- Does every sentence pass [the one test](#the-one-test)?
- Is every other thing linked, at least once in each section that mentions it?
- Does any section explain something that has, or should have, its own page?
- Are the headings exactly the template's, in its order?
- Is anything said here that's also said on another page? Keep it in one place.
- Did you change only what was asked?

## Example

A skill page that explains everything in place:

```markdown
Mining is a gathering skill. Rocks hold ore: copper at level 1, iron at 15, and ore runs out
after a few strikes, then comes back after a minute. You need a pickaxe, and better pickaxes,
which smiths make from bars at an anvil, mine faster. The ore is smelted at a furnace into bars.
```

The same, as a wiki page:

```markdown
Mining is how ore and stone come into the world, so every [smith](smithing.md) depends on
someone swinging a [pickaxe](../groups/pickaxes.md) at a [rock](../groups/rocks.md).
```

Which rock needs which level, how fast it runs out, who makes pickaxes and where ore is smelted
are each on the page that owns them, and the Rocks group page gathers them.
