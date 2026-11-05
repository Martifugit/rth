# What's new in R'th Codex

A few plain lines per version, for the people using it: `npm run release` puts the version's section in
the GitHub release and announces it in #codex (`scripts/release.mjs`, `scripts/announce.mjs`). Write the
section before releasing; the release refuses without one.

## 0.4.0

- R'th AI has two modes, shown under its box: **Chat** (its changes wait for you to accept or skip) and **Work** (it edits the pages, with Show and Undo after). Shift+Tab cycles them, as in Claude Code. The word "suggest" in a message no longer decides it.
- R'th AI knows the GDD the way Codex does: who links to a page and what each link means, the generated lists, the To-do's checks and spelling. It can make a page from its template and rename one with every link following. In tests it got every question and task right, against 25 of 28 before, a little quicker and reading far less.
- Every change R'th AI makes says why, in a few words, in a small note beside the change as you go through them.
- Going through suggestions: Alt+↓ and Alt+↑ move between them, Alt+Enter accepts and Alt+Backspace skips. An answer that suggests changes on several pages takes you from one page to the next, and can accept them on every page at once. The bar has R'th AI's icon, and its buttons no longer wrap.
- After R'th AI changes pages, Codex checks what it changed (structure, links, spelling) and shows anything new it caused as a card under the answer, with Fix and Dismiss.
- The cards say what changed in plain words: "1 update in Skill Woodcutting", "Made Activity Wildfires".
- Select part of an answer and press the reply button: it's quoted above the box with a line for your question about it. Quote as many parts as you like in one message.
- The message box is lighter: no frame, with the mode and a smaller send button on the line under it. Every message has a small copy button.
- AI setup is a tidy settings list, and opens at once. Claude answers with **Sonnet 5.5 at medium effort** unless you pick another default there; /model and /effort change it for one conversation.
- The health check: with Claude, while you're away from Codex for a while, R'th AI reads the pages changed since they were last checked and lists problems across pages (contradictions, facts copied instead of linked, questions answered elsewhere) on the To-do page, for everyone, each with a plan. **Fix with R'th AI** starts a conversation that suggests the fix. "Check the whole GDD" on the To-do page goes through every page. It can be turned off in AI setup.
- @RthAI in a comment thread, in Discord or in Codex: R'th AI answers in the thread, from a Codex with Claude that has "Answer @R'th AI in Discord" on in AI setup (only one answers). With none open it says it's offline, and answers once one opens.
- The free models (OpenRouter) start from the parts of the GDD that match the question, and their lookups work more often: many wrote them as text, which now counts.
- It says R'th AI everywhere, never "the AI".

## 0.3.7

- No more silver frame round the Codex window: Windows 11's border is now the same near-black as Codex.
- Everything in the title bar has the logo button's quiet silver look: the buttons, the search box, and the minimise, maximise and close buttons.

## 0.3.6

- The conversation selector in the AI tab has a speech-bubble icon instead of a clock, and its title sits level with the icon.
- The logo in the taskbar, title bar and Start menu is about a quarter larger: zoomed in on its letters, so it no longer looks small beside other apps.

## 0.3.5

- Updates arrive as soon as they're released: Codex starts downloading a new version within moments instead of within the hour.
- While Codex updates, a small window with the logo and a progress bar shows until it opens again, so it no longer just vanishes for a few seconds. (You'll see it from the next update on.)
- Codex calls itself by its name everywhere: "Codex", not "the Codex".

## 0.3.4

- R'th AI's answers flow in smoothly, each new bit fading in. While it works, a line says what it's doing, with a soft glint running along it: thinking, reading a page, or its own thinking when the model shares it.
- When R'th AI suggests a new page, you can preview it and create it (or skip it). Before, Review said the page didn't exist.
- A suggestion card you've finished with shows a check instead of its buttons, and updates as soon as you're done.
- Share is now Save, and the status line speaks of saving: "Saved" means everyone has it, "Saved here" means only your computer has it so far (hover for why).
- Buttons that take a moment (Save, Undo, Create page, Skip) say what they're doing while they work, and can't be pressed twice.
- Deleting an @mention from your message takes its page out of the conversation's context again.
- R'th AI writes in US English, as the spelling check does.
- Drag the edge of the Pages pane or the right-hand pane to resize it (double-click for the usual width). The Pages pane is a little narrower by default, and the right-hand pane keeps one width whichever tab is open.
- The search box sits in the middle of the title bar, and your account is the rightmost button.
- New icons for the Links and AI tabs and for the effects, media, objects and locations folders, and every icon is crisp again (they had gone blurry).
- A darker look: a solid near-black behind the panes, darker stone in them, and a Celtic knot on the line under a page's title.
- Smaller things: a shorter hint in the AI box, a tidier /help, the AI's conversation menu matches the logo's, headings have their gold detail at both ends, and the body text is a shade softer.

## 0.3.3

- R'th AI's suggestions no longer show a made-up heading like "type: system components: [] groups: []" at the top of the page. When it suggests changing the page's header (its status, say), that's a line of its own in the bar: "Also: status stub → draft", to accept or skip.
- R'th AI keeps a conversation's context: it starts with what's on screen and stays on it while you work elsewhere. Type @ to add a page, or click a faint + chip to add the page, passage or comment you're on.
- R'th AI knows its job better: what the GDD is for, and to read the whole page, its template and the pages around it before changing a section. On Claude it no longer works from the game code's developer guide, but still checks the code when a question is about how the game works today.
- On OpenRouter, R'th AI can look things up when it needs to: search the GDD, open pages, see what links where, and search and read the game's code. Each lookup uses one of your free daily requests, so it gets the page up front and looks up at most four times a message; a model that can't look things up answers as before.
- /model and /effort set a conversation's model and effort, as in Claude Code (/help lists them).
- When R'th AI answers while you're elsewhere, the AI tab gets a gold dot and a notice says so, with Open.
- Conversations get a short name from R'th AI's first answer, instead of the question.
- Pages no longer end in a "Links to this page" box that repeated the Links tab. The Links tab now also lists a group's members and how a skill is trained, which only the box had.
- In a review, the old text looks just like the suggestion, tinted red where the suggestion is green (it had its own colour and no underline).
- Stepping through R'th AI's changes (or changes made outside the Codex) shows which one you're on: it stands out, ringed in gold, with a gold mark in the margin by its paragraph, and the others fade back.
- The Add a link window is bigger, and its page list is part of it: always showing, with room for about fourteen results before it scrolls.

## 0.3.2

- The page path at the top sits level with the buttons beside it (it was about 2px high).
- When GitHub itself is having trouble, the status line says so in plain words ("GitHub is down, sending later"), with more on hover: your changes are safe and go out by themselves. It no longer looks like something is wrong with your Codex or your connection.
- Comments, replies and edits wait on your computer when Discord can't be reached, marked "Not posted yet", and post by themselves once it answers. Before, they failed, and a new comment's text was lost.
- Error messages say in plain words what went wrong, whether your work is safe and what to do, instead of codes like "Couldn't save: 500".
- Your edits go out about a minute after you stop typing (it was ten seconds), so the history is one change per sitting rather than one per pause.
- One dictionary for everyone: a word you add is accepted in everyone's Codex (it was kept on your own computer only). Words you added before move into it by themselves.
- An open Codex looks for a new version every hour (it was every 6 hours), so updates reach you sooner.
- R'th AI writes pages like a wiki: each page keeps to its own thing and links out to everything else instead of explaining it in place. It now gets the Charter and the new Writing a Page in full every time (it was only told to go and read them), and you can change how it writes by editing those two pages.
- Things only fixed outside the Codex (a commit someone hasn't pushed, a merge in progress) no longer sit in the bottom-left box with a Try again that can't help: the status line says "Saved, not sent yet", with why on hover, and it clears by itself.
- The bottom-left box only asks what you can answer in the Codex: pages to share, a clash between versions, or signing in when that's what stops your changes going out. Any other trouble sending is on the status line, with why on hover, and a send GitHub refuses is no longer retried every few seconds for ever.

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
