# Using Codex

Codex is where the GDD is written. It exists to make [The Charter](charter.md) easy to follow: one page
per thing, every page typed, every fact written once and linked everywhere else. Everything below is
in service of that.

## Making a page

- **New page** (top bar): first pick its type. The type decides its folder and its core headings, and
  the page is made from the type's [template](#templates). Then give it a title. Right-click a
  type's folder in the page list to make a page of that type straight away. The components usual for that type are offered as optional
  sections, none ticked: most pages start as a stub.
- **From a link.** Link to a page that isn't written yet and the link shows gold and dashed: a to-do,
  not a mistake. Click it for its three ways forward: **Write it** (pick the type; the link's folder
  suggests one, and picking another moves the page there with the link following it), **It's another
  page** (point the link at the page that's meant), or **Unlink** (keep the words).
- **To-do** in the page list is everything the GDD still needs, as cards you fix where you read them:
  - **Missing pages**: one card per page that's referred to but not written, however many pages refer
    to it, most wanted first. Write it; or say it's another page (Codex suggests likely ones, such as a
    page git saw it renamed to) and every reference moves there; or deal with one reference at a time.
  - **References**: a link to a heading that isn't there, a link out of the GDD, or a field that names
    a page in plain text (lists match plain text by title, so a rename would quietly break it): Make
    it a link.
  - **Placeholders**: a `p-` name, named for real everywhere at once.
  - **Structure**: sections and components that disagree, out of order, or a page in the wrong folder,
    each with its fix.

  A fix that changes more than one page lists them first, and every fix can be undone straight after.

## The top of a page

Above the title, one slim row of badges answers what the page is, whether it's settled, and what it
belongs to:

- **The type** (Entity, Location): what kind of page this is, and which headings to expect.
- **The status**: gray for stub, yellow for draft, green for agreed, red for cut (and a cut page's
  title is struck through). Click it to change it.
- **Variant of…** and the **groups** link to those pages.
- **The components** are the page's sections: click one to jump to it.

The cog at the end of the row opens **Page settings**: the status; the
components as a checklist (the ones usual for the type first, each with its question, the rest under
More); the groups and what the page is a variant of; and the type.

- Ticking a component adds its section from its template, in its place. Sections always come in the
  same order: core headings, then components in the order of How the GDD Works' tables, then Open
  questions.
- Unticking one removes its section. If something is written there, Codex asks whether to move it
  into Open questions or delete it; it never deletes without asking.
- A component that repeats (actions, trades, travel, rewards) gets **+ Add action** at the end of its
  section: each one gets its own heading. Settings only say whether the page has actions at all.
- Changing the type is rare and deliberate: the page moves to the new type's folder, links to it are
  updated, and its headings stay as they were, so Codex offers to add the new type's missing ones.
- **Rename…** changes the title and the file name together. A page's file name is its ID, unique
  across the GDD, and every reference to it (links, groups, Variant of, lists) is rewritten in every
  page, so nothing is left pointing at the old name.
- Groups and Variant of are picked from pages that exist, never typed. If the page isn't written yet,
  **Make …** in the list makes it as a stub.

You never have to type the page's header by hand. **<>** (top right) shows the raw text for when you
want it.

## What links here

The **Links** tab (right pane) gathers what other pages say about this one: a group's members, a
skill's training, then links in by what they mean (*Dropped by*, *Found in*, *Appears in*), the rest
of the links in and out, and pages that name this one without linking. It's computed, never written,
so it stays out of the page's own words. To change it, change the page that owns the relationship.

## Templates

Pages of the same kind always read the same, because each is put together from templates. The page's
**type** gives it its skeleton: the core headings every entity, location or quest has. Each
**component** it takes on (an action, a shop, rewards) adds its own section, with the fields that
section always asks for. A template is the shape of one type or one component, and Codex fills a page
from them when you make it or tick a component.

- **Prompts.** An empty section or field shows a faint line saying what belongs there. It goes as soon
  as you write, and it's never part of the page.
- **Tables** are for things that come in rows: rewards, spawns, stock, states. **+ Add a row** sits
  under the table, and each row counts on its own, so a list elsewhere can gather them.
- **When a template changes**, new pages and new sections follow it, and every page shows its newest
  prompts. Templates aren't pages you read or search for: changing one changes the shape of the GDD,
  so it's agreed in [How the GDD Works](how-the-gdd-works.md), not made on a page.

## Links

- Link to another page with Ctrl+K: pick the page from the list (or type a web address, or a
  `#heading`). Links are never typed as paths, so they can't point at nothing by mistake. To link a
  page that isn't written yet, say so: it becomes a to-do.
- A link is named by what it means on the page it points to: a link from a rewards section shows
  there as *Dropped by*, from spawns as *Found in*, from a quest's steps as *Appears in*. Whoever owns
  the relationship writes the link; the other page shows it by itself, in its **Links** tab.

## Lists of your own

For any other list gathered from other pages, write a code block of the language `query`, and Codex
shows what it finds under it:

```
type: entity
groups: goblins
```

Each line is a filter: `type`, `status`, `groups`, `components`, `based-on`, `links-to`, or
`<component>.<field>` for what pages write in their component sections (`action.level: 15`). `this`
means the page the list is on; `sort: level` orders it.

## Comments and Discord

- Select text and press the comment button (Ctrl+M) to comment on it, or comment on the whole page.
  Comments sit in the margin and never go into the page.
- Every comment is a post in the Discord forum, under your name; reply there or here. Resolving it in
  either place resolves it in both.
- Ctrl+Shift+C copies the selection, or the section you're in, for Discord, with a link back.
  Right-click a page for a link to it.

## Working together

- Your avatar is top right; others with Codex open sit beside it. Hover to see where they are, click
  to go there. A note says when you're editing the same section as someone.
- Pages save themselves as you type and are sent to everyone every minute or so. If two people
  change the same lines, Codex asks whose to keep.
- Every change to a page is posted in Discord's #codex.
- When a new version of Codex is ready, a blue **Update** button appears in the title bar.

## The AI

The AI tab (Ctrl+J) reads the Charter and How the GDD Works with the page you're on. Talk an idea
through, or ask it to change something: it edits GDD pages only, and **Show changes** marks what it
did, with **Undo**.
