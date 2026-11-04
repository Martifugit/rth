# What's new in the R'th Codex

A few plain lines per version, for the people using it: `npm run release` puts the version's section in
the GitHub release and announces it in #codex (`scripts/release.mjs`, `scripts/announce.mjs`). Write the
section before releasing; the release refuses without one.

## 0.3.0

- Each page's type, status, components and groups sit in a card over it; change the status there.
- New pages start with their type's headings; + on the card adds a component and its section.
- A link to a page not written yet is a to-do: click it to make the page as a stub.
- Links in are named by what they mean: Dropped by, Found in, Appears in, Sold by.
- Lists that write themselves: a group's members, a skill's training, or any query, in a `query` block.
- To-do (in the page list): pages to write, and pages whose type or sections need a look.
- A coloured dot shows each page's status in the page list, with chips to show only stubs, drafts...
- Status (in the page list): every page by type and status, gathered from the pages themselves.
- File names are unique across the GDD, and every p- placeholder name shows in To-do.

## 0.2.0

- The Codex is now for the GDD alone: it opens on the Charter, and a new page asks for its type.

## 0.1.5

- See who else has the Codex open: their avatars by yours (hover to see where they are, click to go
  there), and beside the page's path for those on the same page.
- A note when you and someone else are editing the same section.
- Comments arrive straight away, with "… is typing" in a thread.
- Right-click in the page list: make a page in a folder, delete a page, copy a link for Discord.

## 0.1.4

- The first start asks nothing: it gets the docs ready by itself, with a progress bar.
- Delete a page with the bin in its title bar (Undo straight after).
- The Codex says when someone makes, changes or deletes pages, with a button to open them. New and
  deleted pages arrive within a minute or so.
- Uninstalling now removes everything the Codex kept on your computer.

## 0.1.3

- Comments in Discord can be deleted, edited and resolved again.
- The AI's comments are signed R'th AI, whichever AI wrote them.

## 0.1.2

- A blue Update button in the title bar when a new version is ready.
- Discord links to the Codex close their browser tab once the Codex has opened.

## 0.1.1

- The AI can leave comments when asked, and they appear in Discord.
- AI setup (the cog in the AI tab): choose Claude or OpenRouter, sign in, or paste a key.
- Back and forward (Alt+← and Alt+→, the mouse's buttons, the arrows by the page title).
- Keys that work on Norwegian keyboards: AltGr characters type, Ctrl+, and Ctrl+. show and hide the panes.

## 0.1.0

- The first Codex: read, edit and comment on the design docs, signed in with Discord.
