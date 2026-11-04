# Using Codex

Codex is where the GDD is written. It exists to make [The Charter](charter.md) easy to follow: one page
per thing, every page typed, every fact written once and linked everywhere else. Everything below is
in service of that.

## Making a page

- **New page** (top bar), or right-click a folder in the page list. Give it a title and pick its type:
  the type decides its folder, and the page is made from the type's template in `templates/`. It
  starts as a stub.
- **From a link.** Link to a page that isn't written yet and the link shows gold and dashed: a to-do,
  not a mistake. Click it to make the page, already titled and in its folder.
- **To-do** in the page list gathers every page that something links to but nobody has written, and
  every page whose structure needs a look.

## The top of a page

The top of every page answers what it is, whether it's settled, and what it belongs to.

- **The label beside the title** is the page's type (Entity, Location): what kind of page this is, and
  which headings to expect.
- **The status badge**: grey for stub, yellow for draft, green for agreed, red for cut (and a cut
  page's title is struck through). Click it to change it.
- **Variant of…** shows under the title when the page is a variant of another.
- **Groups** are chips that link to the group pages. **+** adds or removes one, makes a new group, or
  sets what the page is a variant of.
- **The page's components** are its section links, on the right: click one to jump to it.
  **+ component** adds one: its section comes from its template, at the end of the page.

You never have to type the page's header by hand. **<>** (top right) shows the raw text for when you
want it.

## The bottom of a page

**Links to this page** gathers what other pages say about this one: *Dropped by*, *Found in*,
*Appears in*, a group's members, a skill's training. It's computed, never written, so it sits apart
from the page's own words. To change it, change the page that owns the relationship.

## Templates

`templates/` holds one template per type (`entity.md`) and one snippet per component (`action.md`).
Change a template and every new page of that type, or every new section of that component, follows it.

## Links

- Link to another page with Ctrl+K, or type `[Goblin](../entities/goblin.md)`.
- A link is named by what it means on the page it points to: a link from a rewards section shows
  there as *Dropped by*, from spawns as *Found in*, from a quest's steps as *Appears in*. Whoever owns
  the relationship writes the link; the other page shows it by itself, in **Links to this page**.

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
