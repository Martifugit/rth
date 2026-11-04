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

## Templates and components

`templates/` holds one template per type (`entity.md`) and one snippet per component (`action.md`).
Change a template and every new page of that type follows it.

- **Component** (in the toolbar) adds a component where the cursor is: its snippet goes into the page,
  and its name into the page's frontmatter. The components that belong on the page's type come first.
- **<>** (top right) shows the page's raw text, frontmatter included: its type, components, groups,
  based-on and status.

## Links

- Link to another page with Ctrl+K, or type `[Goblin](../entities/goblin.md)`.
- **Outline and Links** (right) shows what links here, named by what the link means: a link from a
  rewards section shows as *Dropped by*, from spawns as *Found in*, from a quest's steps as *Appears
  in*. Whoever owns the relationship writes the link; the other page shows it by itself.

## Lists that write themselves

Lists that gather things from other pages are never typed by hand. Write a code block of the language
`query`, and Codex shows what it finds under it:

```
groups: this
```

Each line is a filter: `type`, `status`, `groups`, `components`, `based-on`, `links-to`, or
`<component>.<field>` for what pages write in their component sections (`action.skill: this` is a
skill's training table). `this` means the page the list is on; `sort: level` orders it. A new group
page gets its members list by itself.

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
