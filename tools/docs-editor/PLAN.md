# The Codex as a desktop app (plan)

*Planned 2026-11-03; revised the same day (AI built in; then comments and sign-in moved to Discord,
through a small serverless helper, because contributors all have Discord and not all have GitHub).
Being built: steps 1 and 2 are done (2026-11-03), except bundling git, which comes with shipping.*

The Codex is Google Docs for the GDD. It's an app you download and open like any other. The pages
are the game's own files, kept in its repository and synced by git behind the scenes; comments sit
in the margin and live in the group's Discord; an AI helps wherever you are. Nobody needs a GitHub
account, nobody looks after a server, and nobody manages anything after the first setup.

## How it works, simply

| Piece | What it is | Where it lives |
|---|---|---|
| **The window** | What you see: pages, editor, comments, search, AI. Today's Codex page. | Inside the app |
| **The engine** | Does the work: reads and saves pages, searches, checks spelling, syncs, shows comments, talks to the AI. Today's `server.js`. | Inside the app, on your computer |
| **The pages** | The GDD's Markdown files. The game owns them; the Codex edits a copy. | A folder on your computer, synced with the game's repository on GitHub by git (inside the app) |
| **Comments** | People talking about the pages. | A forum channel in the group's Discord: one post per comment, replies in the post |
| **The helper** | A small program that holds the keys (Discord's and GitHub's), carries comments between the Codex and Discord, and remembers where each comment belongs. Wakes when asked, sleeps otherwise. | Cloudflare Workers, free plan |
| **The AI** | Claude for the owner (Claude Code on their computer); free models through OpenRouter for everyone else. Swappable. | Claude Code locally, or OpenRouter online |

What happens:

- **Signing in**: *Sign in with Discord*. Anyone in the group's Discord server with the Codex role is in.
- **Opening the app**: it fetches what others did since you last looked.
- **Editing**: saved to your folder within a second. Every few minutes, and when you close the app,
  your edits are recorded under your name and sent to GitHub.
- **Others' edits**: fetched every few minutes and when you come back to the app; open pages update.
- **Two people, one paragraph**: different paragraphs combine by themselves; the same paragraph asks
  which to keep.
- **Comments**: posted from the Codex or from Discord, seen in both within seconds, under each
  person's own Discord name and avatar.
- **What changed**: a Discord channel gets a short notice for each sync and each new comment, with a
  link that opens the page in the Codex at that place.
- **AI**: suggests changes right in the page, which you keep or undo; answers questions where you ask.
- **Who can do what**: the Codex role in Discord.

Nobody ever installs git or Node, types a git command, copies a token, resolves a git conflict, or
visits GitHub.

## AI, built in

Built the way the apps people already like do it (Notion's agent, Gemini in Google Docs, ChatGPT's
canvas, Word's Copilot, Cursor, iA Writer), not invented here: one assistant at the side that you talk
to, which edits the page when you ask it to, and shows what it changed.

What you see:

- **The AI tab.** A third tab in the right-hand window: *Outline · Links · AI*. It docks beside the
  page when there's room and opens over it as a sheet when there isn't, like the other panes (never
  below the editor). **Ctrl+J** opens it and puts the cursor in its box, from anywhere.
- **Just talk.** It's a conversation. Discussing an idea, asking how something works elsewhere in the
  docs or arguing a design changes nothing, so there's nothing to accept or turn down.
- **It knows what you're looking at.** Small chips over the box say what it will read: the page, your
  selection, the comment thread you have open. Click one away to leave it out.
- **Ask about a selection.** The bubble over selected text gets **Ask** beside **Comment**: it opens the
  AI tab with that text as a chip. No menu of preset actions; you say what you want.
- **When you ask for a change, it makes it.** "Tighten this", "write the missing sections from the
  docs", "draft the decision from this thread" edit the page directly (several pages, for Claude). The
  conversation shows what it did, *Changed 3 places in Combat*, with **Undo**.
- **Show changes.** A page the AI changed says so at its top, with **Show changes**. Off, you read the
  new text clean; on, what it added is highlighted and what it removed is shown struck through, with
  arrows to step from one change to the next. Pointing at a change offers **Undo** for just that one.
  **Done** clears the marks. (Notion's show/hide changes, canvas's Show changes, Cursor's undo per
  change.)
- **Suggest instead, when you want it.** Ask it to *suggest* edits (a grammar pass, a careful review)
  and nothing changes yet: you go through them top to bottom, **Accept** or **Skip** each, in the same
  highlighting. (Notion's suggested edits, Word's tracked changes.) The exception, not the normal way.
- **Getting started.** An empty AI tab offers two or three things to ask that fit where you are: *Draft
  this page from the docs* on a new page from a template, *Draft the decision* with a thread open,
  *Review this page* otherwise. They go once you type.
- **From the search box**, the last row is *Ask AI: "how does death work?"*; Enter asks it in the AI
  tab, where the answer links to the pages it used.
- **Pasted conversations.** A conversation pasted into the AI tab from Discord (its "Alice — Today at
  5:12 PM" lines) is read as who said what, so "turn this into the decision" works on chat. (iA
  Writer recognises pasted chats the same way.)
- **Your conversations** stay on your computer, with a list of earlier ones and **New**.

An AI edit is an ordinary edit under your name: it saves and syncs like your typing (its commit says
"with AI"), and Undo works after it's shared too, as a new edit.

How it's built:

- **One assistant in the engine.** The AI tab asks it with a message, the chips' context, and the
  conversation so far. It answers in streamed text, and changes pages only through one edit tool it
  is told to use only when asked for a change (or, suggesting, to hold its edits back).
- **Context is gathered by the engine**, so any model can do the job: the page, the selection, the
  thread, the outline, linked pages, the best search matches, `writing.md` and the glossary, trimmed
  to what the model can take.
- **Changes are found, not trusted.** Before a turn the engine keeps a copy of every page it may
  touch; after it, each changed page is compared with its copy, word by word. That comparison is what
  *Show changes* draws and what *Undo* puts back (one change, or the turn; if you've edited since,
  only the AI's words are undone, through the paragraph merge in `src/merge.js`). It works whatever
  made the change: Claude Code editing the files itself, or an OpenRouter model calling the edit tool.
- **Providers are swappable modules** behind one interface. **Claude** (found on the computer, no
  setup; it may also explore the whole repository and change several pages) and **OpenRouter** (Sign
  in with OpenRouter, free models with fallbacks, each person's own daily allowance). Gemini, a local
  model or another service would be one new module.
- Your unsaved typing is saved before a turn starts, as `@Claude` does today.
- When the daily allowance runs out, the AI says so plainly; everything else keeps working.

Later, if wanted: marking text the AI wrote until a person rewrites it (iA Writer's authorship), which
needs a place to keep who wrote what beside each page.

## Comments in Discord

- **A forum channel**, `#codex-comments`. Each comment is its own forum post, so each conversation is
  a thread, as in Google Docs. The post's title names the page and the start of the passage
  ("Combat: “bile settles toward…”"); its first message quotes the passage, then the comment, then a
  link that opens it in the Codex.
- **Posted as the person.** The Codex posts through a Discord webhook, which sets each message's name
  and avatar, so a comment from the Codex looks like Alice wrote it in Discord. Claude's answers post
  as "Claude". People in Discord simply reply in the post.
- **Resolving** gives the post a *Resolved* tag and closes it, from either side (in Discord: the tag;
  a ✅ on the first message counts the next time the post changes). Writing in a resolved post, or
  reopening it, opens it again. Comment history shows closed ones.
- **Where it's anchored** (the quoted passage and its surroundings, as `src/anchor.js` does today) is
  kept by the helper, keyed by the post. A Discord message can't carry hidden data, so this is the
  one thing Discord doesn't hold.
- **A post started in Discord** (not from the Codex) shows in the Codex's comment history as
  *unfiled*; ⋮ > attach to the selected text files it, as today.
- **Seeing new comments**: Discord only pushes messages to programs that stay connected, and the
  helper doesn't. So the Codex asks the helper every 10 seconds while it's in front, every minute in
  the background. The helper answers from one Discord request (the server's active threads, each with
  its latest message) shared by everyone for a few seconds, and fetches only the threads that changed.
- **A copy in git.** When the owner's Codex syncs, it writes the comments into `docs/.notes/` as
  today, so the repository keeps their history and agents can read them offline.
- **Agents** use `notes.js` as now; it talks to the helper (with a key only the owner's machine
  has) instead of the files, so an agent's reply appears in Discord as "Claude".
- Today's `docs/.notes/` comments are posted to the channel once, with their anchors.

## The helper

A Cloudflare Worker: one small JavaScript program, plus Cloudflare's free database (D1) for the
anchors and sign-ins. It runs only when something asks it to.

| It... | How |
|---|---|
| Signs people in | Discord's own sign-in: the app opens the browser, Discord sends the person back to the helper, which checks they have the Codex role, then hands the app a session. The app keeps it in the computer's secure storage (Electron `safeStorage`). |
| Lets git reach GitHub | The repository has a GitHub App (made once by the owner). The helper holds its private key and gives a signed-in person's app a token for this one repository, good for an hour, to send their changes. (The repository is public, so fetching needs no token.) The app's git (`src/sync.js`, as built) uses it; commits carry the person's Discord name. Nobody needs a GitHub account. |
| Carries comments | Posts through the webhook as the person; reads the forum with the bot's token; keeps the anchors. |
| Posts what changed | GitHub tells the helper about every push (agents' too); the helper posts a short notice to `#codex` ("Alice edited *Combat*: 3 paragraphs in *Bile*"), and one for each new comment. Bursts are gathered into one message. |
| Opens links | Discord only makes `https` links clickable, so `https://<helper>/open?p=…` is a tiny page that hands over to the app (`rth-codex://…`, registered by Electron), at the heading or comment. |

Its limits: the free plan allows 100,000 requests a day. Ten people with the Codex open eight hours a
day, asking every 10 seconds, is under 30,000. If the helper is down, editing and saving keep working
and comments wait in the app until it's back.

Costs: nothing. Discord's API, bots, webhooks and forum channels are free; reading message text
needs Discord's *Message Content* permission, free and without approval for bots in fewer than 100
servers. GitHub Apps are free.

## Discord, beyond comments

Small things that make the two feel like one place:

- **Copy for Discord**: on a selection or a section, copies it as Discord's Markdown (tables become
  code blocks; Discord has none), under its 2,000-character limit, with a link back.
- **Pasting from Discord**: a copied conversation ("Alice — Today at 5:12 PM" lines) is recognised:
  paste it as a comment with its authors, as an Open question, or hand it to **Suggest a decision**.
- **Images from Discord** are downloaded into `docs/images/` when pasted or dropped, because Discord's
  attachment links expire within a day.
- **"Editing *Combat* in the Codex"** on each person's Discord profile, through the Discord app on their
  computer (Rich Presence). No server; off in Settings for anyone who prefers.

Later, only if wanted (the helper can answer Discord's commands the same way): `/codex search`,
`/codex page` as embeds; *Send to Codex* on a message's menu, filing it as a comment on a page.

## Setup, once

| Who | What |
|---|---|
| Building the app | Register the app with Discord (for sign-in and the bot) and deploy the helper to Cloudflare. Publishing new versions is automated. |
| The owner | Create the repository's GitHub App (the Codex walks through it); add the bot to the Discord server; make the Codex role and the two channels. |
| The owner, after that | Nothing: give someone the Codex role in Discord. |
| A contributor | Has the Discord account they already use. Downloads the app, signs in with Discord. |

## Decisions

- **Electron.** The Codex is already a Node program with a web page for a screen; Electron packs both
  into one desktop app (as VS Code and Slack are), so the code barely changes. About 150 MB.
- **Everything bundled.** Git is MinGit on Windows (the official small Git for Windows made for
  bundling; GPL, its licence shipped with it); Macs use their own git. Claude Code is used only if it
  is already installed.
- **Discord for people, GitHub for the docs.** Sign-in, who's allowed in and comments are Discord's,
  where the group already is; the pages and their history stay in git on GitHub, reached through the
  helper's short-lived tokens. (GitHub sign-in and Discussions were planned first and dropped: not
  everyone has a GitHub account.)
- **Serverless, not a bot that's always on.** Cloudflare Workers wake per request, so there's no
  machine to keep running. The cost is that Discord replies reach the Codex when it next asks (within
  10 seconds while open), not the instant they're sent; for comments, that's enough.
- **Only the docs are fetched**: a sparse, partial clone of `docs/`. Someone with the full repository
  (the owner, agents) points the app at that folder instead.
- **Commits** (built): only files under `docs/`, ever. Automatic (every few minutes once typing stops,
  and on closing) sends the pages saved in the Codex; the Sync button also sends doc changes made
  outside it. One commit per sync, by its author ("Docs: Combat, Death"). Others' work comes in by
  `git merge` (no rebase or autostash, which need a clean folder): git itself refuses if it would touch
  uncommitted files, and the Codex says so and retries; a same-line clash is undone and the person
  chooses **Keep mine** or **Use theirs**.
- **Updates** come from releases published somewhere public (the app holds only code, never docs).
- **The code** stays in `tools/docs-editor` for now; the helper in `tools/docs-editor/helper/`.

## Work, in order

1. **The app.** *(Done.)* Electron around today's engine and window, with the title bar as the app's
   own (search, New page, Sync, help); replaces `codex.cmd`; uses an existing checkout.
2. **Sync.** *(Done; bundled git with shipping.)* Commit, fetch and send on schedule and on the button;
   clash handling; the sync status in the Sync icon.
3. **The helper and Discord.** In this order, each useful alone:
   1. *(Done.)* *What changed* notices in Discord, and `https` links that open the Codex.
   2. *(Done; waiting on the owner's first sign-in.)* Sign in with Discord, and git through the helper's tokens.
   3. *(Done; nothing to move, `docs/.notes/` was empty.)* Comments in the forum channel, and `notes.js` over the helper.
   4. *(Done.)* Copy for Discord (Ctrl+Shift+C), pasting from Discord, images from Discord, Rich Presence.
4. **AI**, each step useful alone:
   1. *(Done.)* The AI tab and talking (Claude first): chips, Ask on a selection, Ctrl+J, conversations kept.
   2. *(Done.)* Edits: copies before a turn, *Show changes* (word marks; a mostly rewritten block shown whole), Undo per change and per turn.
   3. *(Done.)* Suggesting instead (a request that says "suggest"), the starting prompts, *Ask AI* from search, pasted conversations.
   4. *(Done; waiting on a first real sign-in.)* OpenRouter, with Sign in with OpenRouter: one request a message (the engine gathers the reading), edits as blocks in the answer that the engine applies, and OpenRouter's free router (`openrouter/free`) as the model, so no list of free models to keep.
   `@Claude`, "Assign to Claude" and @agent leave the comments once the AI tab does their work.
5. **First run** *(done)*: choose a folder (or fetch the docs: a blobless, sparse clone of `docs/`, about
   12 MB and seconds), a welcome offering Sign in with Discord and, without Claude, Sign in with
   OpenRouter (or Not now); a fetched copy is read-only until signed in with the Codex role.
6. **Shipping**: installers and automatic updates.

## Open questions

1. Where releases are published.
2. Whether anyone will use it on a Mac.
3. Which free models to start with, chosen when building by measuring real questions.
4. One Codex role, or a second one that can read and comment but not edit.
5. The channels' names, and whether *what changed* notices go to their own channel or the comments'.
6. Whether `@Claude` in a Discord comment thread should get an answer there (the helper, through
   OpenRouter), once comments are in Discord.
