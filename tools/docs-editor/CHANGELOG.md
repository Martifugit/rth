# What's new in the R'th Codex

A few plain lines per version, for the people using it: `npm run release` puts the version's section in
the GitHub release and announces it in #codex (`scripts/release.mjs`, `scripts/announce.mjs`). Write the
section before releasing; the release refuses without one.

## 0.3.1

- Share works for every page in the bottom-left box again: a page deleted outside the Codex made the whole Share fail.
- Share and Undo no longer get lost when you click them while the Codex is sending something; they wait their turn.
- The box says pages were "changed outside the Codex", and no longer blinks each time a page saves.
- The AI is called R'th AI everywhere outside AI setup: in comments, the AI tab and the welcome screen.
- Your messages in the AI tab are chat bubbles, and comments use the same soft grey cards; AI setup's fields line up with their buttons.
- Hovering someone's avatar in the header says where they are again, and clicking it takes you there.
- Switching to or from the AI tab eases the pane wider or narrower instead of jumping.
- Search no longer offers the templates as pages.

## 0.3.0

- The GDD in its own order: the start page, the Charter, How the GDD Works, Using Codex, Workflow, templates, then a folder for every type.
- The top of a page says what it is: its type beside the title, its status as a coloured badge (click to change it), its groups as chips, and Variant of… when it has one. A cut page's title is struck through.
- The page's components are its section links; + component adds one from its template.
- New pages are made from their type's template in templates/.
- Links to this page, at the bottom: Dropped by, Found in, Appears in, a group's members, a skill's training. Computed, never written.
- A link to a page not written yet is a to-do: click it to make the page as a stub.
- Lists of your own, gathered from the pages, in a `query` block.
- To-do (in the page list): pages to write, placeholder names, and pages whose type or sections need a look.
- File names are unique across the GDD.
- Open questions is gone from the page list: each page keeps its own.
- The templates are no longer in the page list or search: New page and + component still use them.
- No more Sync button: what you type is shared by itself a few seconds after you pause, and others' changes arrive within seconds. The bottom line says Saved and sent, or why not.
- Changes by R'th AI or another program are asked about in the bottom-left corner before they're shared: Share, Review or Undo.
- Updates come as a screen over the Codex: once a new version has downloaded, Restart and update saves your work and updates. The Update button in the title bar is gone.
- When a release has changes older Codexes can't work with, an older Codex stops and asks to be updated straight away.

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
