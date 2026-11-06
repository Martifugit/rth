# R'th Codex

A local reader and editor for the design docs, in the game's own look (the bronze frames, Cinzel
and Alegreya Sans, the icons from `client/Assets/Resources/UI`). A dev tool: nothing here is part
of the game, the client or the server, and it serves on `127.0.0.1` only.

**Start it:** double-click `codex.cmd`. The first time on a computer without the game's repository, it
fetches just `docs/` into its own folder (`%APPDATA%
th-docs-editorDocs`), with a progress bar and no
questions (`electron/firstrun.js`; a small link lets someone with the game's repository use that
instead). That copy is read-only until you sign in with Discord with the Codex role. It opens the Codex as a desktop app (Electron; `npm run app`). On Windows it runs its own copy of Electron, `node_modules/.codex-app/Codex.exe`, stamped "R'th Codex" with the logo so Task Manager names it (`electron/launch.js`, made once and again when Electron or the logo changes).
To try changes beside an installed Codex, `npm run dev` (add `-- --people` for pretend people in the
room): its own settings folder, your sign-in copied from the installed one the first time, and the
`rth-codex://` links left to the installed Codex (`scripts/dev.mjs`). It's a real Codex on the same docs.
Closing it saves anything still being typed. The same Codex also runs in a browser: `npm start`, then
<http://127.0.0.1:5178/> (another port: `set PORT=5179` first).

Where it's going (an app anyone can download, synced through git, with AI built in): [PLAN.md](PLAN.md).

## Syncing

There's no Sync button: what's saved is shared. The Codex only ever commits files under `docs/`: code,
scenes and any other uncommitted work in the folder are never staged, committed, set aside or
overwritten. The status line says how the open page stands: Saving…, Saved (everyone has it), Saved
here (only on this computer for now: offline, or GitHub trouble; hover for why), or Not saved yet.

- **What you type in the Codex** is sent by itself, once you pause for about a minute (at most ten
  minutes after the first unsent save, however long the typing goes on), so a burst of editing is one
  commit. A page made or deleted goes within seconds; closing the app sends everything.
- **Others' changes** arrive within seconds: when anyone sends, the helper tells every open Codex
  (the live room) and it fetches at once; a check every minute is the safety net. They come in with a
  merge, and the Codex says what came.
- **Edits the Codex won't share by itself**, R'th AI's and those made outside it (an agent, another
  editor), are asked about in a panel in the bottom-left corner: **Save**, **Review** (the page with
  its changes marked, and Save in its bar) or **Undo** (back to how it was last shared). Unanswered,
  they stay on this computer and are asked about again next time. Comments (`docs/.notes/`) are
  always sent.
- **Two people changing the same lines** of a page: nothing is merged, and the same panel asks **Keep
  mine** or **Use theirs**. Sending that fails for another reason is asked there too, with Try again.
- The browser version (`npm start`) doesn't sync by itself. `src/sync.js`; tests in
  `test/sync.test.js` (on throwaway repositories).

## Using it

- **Layout:** click the logo (top left) to choose what sits beside the page: the Pages pane, the
  Outline and links pane, and the comments. The choice is kept and holds at any window size. When the
  window is too narrow for a pane, or you have hidden it, its button in the title bar (beside the logo
  for Pages, beside help for the Outline) opens it over the page as a sheet; picking a page or a heading
  closes it. Ctrl+, toggles Pages, Ctrl+. the Outline (Ctrl+\ and Ctrl+Shift+\ too, by that key's place);
  Alt+1, 2 and 3 pick Outline, Links or AI.
  Alt+← and Alt+→ (or the mouse's back button, or the arrows in the page's title bar) go back to where
  you were and forward again.
- **Pages** (left): the GDD (`docs/gdd/`) and nothing else, with *Recent changes* and *To-do* at the
  top. The rest of `docs/` stays in the repository, out of the Codex's way. It opens on the start
  page, `docs/gdd/README.md`. A gold
  dot is a page changed since the last commit, a green one a page not in git yet. Type to filter.
  Right-click a folder for **New page here**, or a page to open it, make a page beside it, copy a link
  for Discord or its path, or delete it.
- **Spelling** is checked by the Codex, not the browser (whose dictionary follows its language): US
  English, plus any word the docs use three times or more (R'th's names). Right-click an underlined
  word for suggestions, or to add it to the dictionary: the shared list in `docs/.codex/words.txt`,
  sent like comments, so a word anyone adds is accepted for everyone.
- **Search** (the box at the top, Ctrl+P or `/`): pages whose title or name match, then every line
  in every page with those words. Click one to go there, highlighted.
- **Find and replace** (`src/find-bar.js`, `src/find-all.js`, the rules in `src/find.js`): **Ctrl+F**
  finds in the page (match case, whole words; Enter and Shift+Enter step), **Ctrl+H** replaces (one, or
  all as one Ctrl+Z), in the Markdown view too. **Ctrl+Shift+F** finds in all pages, by page, and
  replaces in the pages ticked (two clicks, Undo in the message after). Across pages only the text
  changes: never frontmatter, where links point, or code (`/api/find`, `/api/replace`).
- **Keys:** `?` lists every shortcut, named as your keyboard has them. Shortcuts live in one table
  (`SHORTCUTS` in `src/app.js`), matched by `src/keys.js`, which knows Norwegian and other layouts
  (AltGr is Ctrl+Alt there, so headings are Ctrl+Shift+1-6). Ctrl+N makes a page, Ctrl+Shift+D opens
  the To-do page, Ctrl+Shift+E Recent changes, Ctrl+Shift+M all comments, Alt+Page Down/Up the next or
  previous comment.
- **Zoom:** Ctrl and + or - (or the number pad's, or the wheel), Ctrl+0 back; the desktop app shows the
  zoom at the top right for a moment (`src/zoom-badge.js`).
- **Editing:** the page is always editable, like a Google Doc, and saves itself a moment after
  you stop typing (Ctrl+S saves at once). The bottom right says when it's saved. Links open on a
  click (Ctrl+K edits one); a link to a page that doesn't exist is red. The **<>** button (top right)
  shows the raw text; Esc goes back.
- **Outline and Links** (right): the page's headings, following where you are; what links here,
  what it links to, and where it's mentioned without a link.
- **New page**: a title and a type ("entity | monsters, NPCs, pets"). The type decides the folder (the
  Types table in How the GDD Works, read by `src/gdd.js`), and the page is made from the type's
  template, `templates/<type>.md`, its title line set to the page's title.
- **The top of a page** (`src/gdd-ui.js`, after the OSRS Wiki's infobox, slimmer): the type as a label
  beside the title; a strip under it with the status badge (grey stub, yellow draft, green agreed,
  red cut, which also strikes the title through; click for a menu), the groups as chips to their pages
  (**+**: a picker, a new group, and Variant of…), and the components as jump links to their sections
  (**+ component**: its section from `templates/<component>.md`, at the end of the page, before Open
  questions, and its name in the frontmatter). Variant of… shows under the title when `based-on` is set.
  The frontmatter (`src/frontmatter.js`) never goes into the editor: it is kept aside, edited only
  through the strip (one field's line at a time), and written back in front of the page. The raw view
  (**<>**) shows it for power use.
- **What links here** (the Links tab, `src/panels.js`; `GET /api/links`, `GET /api/generated`): a group's
  members, a skill's training, links in by what they mean; computed, so kept apart from what the author
  wrote (it used to be repeated in a box at the bottom of the page).
- **To-dos:** a link to a page not written yet is gold and dashed; a click makes it, as a stub of its
  folder's type. **To-do** (in the page list) gathers them, groups named in frontmatter with no page,
  `p-` placeholders, and the structure checks: no type, an unknown type or component, a component
  without its section, a component section not in the frontmatter, a page outside its type's folder,
  two pages with one file name (`src/gdd-index.js`).
- **Problems found** (the To-do's first group): what R'th AI's health check found across pages (see "The AI tab").
- **Links in** are named by the section they come from, as How the GDD Works' "Who writes what" says:
  a link from a rewards section shows as *Dropped by*, from spawns as *Found in*.
- **Generated lists:** a code block of language `query` shows, under it, what it finds. Lines are
  filters: `type`, `status`, `groups`, `components`, `based-on`, `links-to`, and `<component>.<field>`
  for component instances (`action.skill: this` is a skill's training table); `this` is the page it's
  on; `sort: <field>`.
- **The page list** is the GDD, flat (`src/gdd.js`, `TOP`): the start page (README), the Charter, How
  the GDD Works, Using Codex and Workflow, then the pages of a type whose Folder is `(top)` (the game
  page, R'th overview), then `templates/`, then a folder per type in the Types
  table's order, shown even while empty. New page refuses a file name already used in the GDD.
- **Delete a page**: the bin in the page's title bar. It asks first (saying how many pages link to it),
  deletes it for everyone, and offers Undo straight after; git keeps its history. Its comments stay in
  the comment history.

Markdown shortcuts work as you type: `## ` for a heading, `- ` or `1. ` for a list, `> ` for a
quote, `**bold**`, `` `code` ``, three backticks for a code block. The toolbar has the rest
(links with a page picker, tables and their rows and columns, checklists, images).

## Signing in, and Discord

**Sign in** (top right) signs in with Discord, through the Codex's helper (`helper/`, a Cloudflare
Worker at `rth-codex.rthapp.workers.dev`): anyone in the R'th Discord with the **Codex** role. Signed in,
the Codex sends your changes to GitHub with an hour-long key from the helper (no GitHub account needed)
and commits them under your Discord name. Not signed in, it uses this computer's own git login, as before.
The session stays on this computer (encrypted in the desktop app). `src/discord-account.js`,
`src/account-button.js`.

Pages changed on `main` are announced in Discord's `#codex` (who edited which pages) in batches per
person: posted once they've been quiet for half an hour, or every two hours while they keep going
(`helper/src/notices.js`, the helper's 5-minute timer). Each page is a link that opens it in the Codex: the helper's `/open` page hands over to `rth-codex://`
links, which the desktop app registers for itself each time it starts.

**With Discord** (`src/discord-extras.js`, `src/discord-presence.js`): **Ctrl+Shift+C** copies the
selection, or the section you're in, as Discord's Markdown with a link back (tables as code blocks,
under 2,000 characters). A conversation pasted from Discord goes in as a quote with who said what
(Ctrl+Z takes it out); a pasted image, or a Discord image link, is saved under `docs/gdd/images/`,
because Discord's links expire. Your Discord profile shows "Editing <page> in the R'th Codex" while
the Codex is open (the Discord app on this computer; off in the logo's menu).

**The helper** (`helper/`): `npx wrangler deploy` from that folder (after a change to `schema.sql`, apply it first: `npx wrangler d1 execute rth-codex --remote --file schema.sql`). Its settings are in `wrangler.toml`;
its secrets live in `helper/.dev.vars` and `helper/github-app.pem` on the owner's computer (git ignores
both) and are sent to Cloudflare with `wrangler secret`. See [PLAN.md](PLAN.md), "The helper".

## Together

While you're signed in with the Codex role, the Codex shows who else has one open (`src/together.js`,
`src/together-ui.js`; the room is `helper/src/live.js`):

- **By your avatar** (top right): the others' small avatars. Hover them to fan them out; hover one to see
  where they are (reading, editing which section, away); click it to go there.
- **Beside the page's path**: who's on this page; a gold ring while they're editing.
- **Editing the same section** as someone: a quiet note says so. Nothing is locked.
- **Comments** arrive at once, with "Kari is typing…" in a thread.
- **Show me as online** in the layout menu (the logo) hides you from the others.

## The AI tab

**AI**, beside Outline and Links (Ctrl+J from anywhere, or **Ask** under the comment button beside a
selection): a conversation about the docs. **Its context belongs to the conversation:** it starts with
what's on screen (the page, a selected passage, the open comment thread) and keeps it, whatever is
opened while waiting for an answer. Chips over the box show it (× drops one); **@** in the box adds a
page by title, and a faint **+** chip offers the page, passage or thread on screen, added only on a
click (`POST /api/ai/context`). Enter sends, Shift+Enter is a new line. **/model** and **/effort** set
this conversation's model and effort as in Claude Code (`/help` lists them); defaults are in AI
setup. Its first answer names the conversation (a hidden `<title>` line the Codex takes off).
R'th AI's standing instructions (who it is, what the GDD is for, how to understand a page before
changing it, then the Charter and Writing a Page) come every turn (`standing()` in
`src/assistant.js`). Claude Code runs on its own (`--setting-sources ""`, `--disable-slash-commands`:
no CLAUDE.md, settings, hooks, skills or plugins from the computer; not `--safe-mode`, which also turns
MCP servers off), so the repository's developer guide doesn't steer it; it can still read the code and
data when a question is about how the game works today.

**The Codex's tools** (`src/ai-tools.js`): one list, served to Claude through the Codex's MCP server
(`src/mcp-server.js`, started by Claude Code for each turn with `--mcp-config`; it hands each call to the
engine, `POST /api/ai/tool`, with the turn's key) and to OpenRouter as function definitions. Reading:
search_gdd (ranked sections, `src/section-index.js`), read_page (a page or one section), page_outline,
links_in (by what each link means), links_out, query_gdd (the query-block language), gdd_types,
check_page (structure, broken links, spelling), comments_on; for OpenRouter also search_code and
read_file. For Claude: create_page and rename_page (Work mode only), explain_change (a reason for each
change), index_page (a page's summary for search, shared in `docs/.codex/index/`), and report_problem
for the health check. OpenRouter has at most four rounds of lookups a message, and its reading starts
from the open page, the summaries of the pages round it and the sections that match the question.

**Chat and Work**: the line under the box (Shift+Tab, or a click) says which. In Chat, a change it makes
is put back and waits as suggestions: the bar at the page's top goes through them, **Accept** or **Skip**
each (Alt+Enter, Alt+Backspace; Alt+↓/↑ to move), **Accept all**, and for an answer that suggested
changes on several pages, page by page ("page 1 of 3") or **Accept on all pages**. In Work, it edits pages
under `docs/gdd/` itself, and its reply says which pages it changed, with **Show** and **Undo**; a page it
changed says so in the bar (**Show changes** marks what it added and removed; pointing at one offers Undo
for just that one). Either way each change shows its reason. The engine finds the changes itself by
comparing each page before and after the turn (`src/ai-diff.js`, `src/ai-changes.js`); the commit that
sends them says "with AI". After a turn that changed pages, the Codex checks them in the background
(`src/turn-check.js`) and shows what the turn brought in (structure, headings that aren't there,
spelling) as cards under the answer, with **Fix** (the To-do's fix, or asking R'th AI) and **Dismiss**.

Select part of an answer and **Ask**: it goes into the box as a quote line (`> ...`), drawn as in the
sent bubble, with the cursor under it for the question. Each message has a copy button. An empty AI
tab offers a few things to ask where you are; a question typed in search (Ctrl+P) has **Ask AI** at the
top; a conversation pasted from Discord is read as who said what. Conversations stay on this computer
(`.local/`, or the app's own folder), listed under the conversation's title. It uses Claude Code when
it's on the computer (`claude` on the PATH, or `CODEX_CLAUDE`); otherwise **Sign in with OpenRouter** in
the AI tab gives free models on the person's own daily allowance (about 50 messages;
`src/openrouter-*.js`). OpenRouter's answers change pages through edit blocks the engine applies (each
with a `reason`), so Show changes and Undo work the same. The engine is `src/assistant.js`; the tab is
`src/ai.js`.

**The health check** (`src/audit.js`): with Claude, while nothing has been done in this Codex for ten
minutes (`CODEX_AUDIT_IDLE`, seconds), R'th AI reads the pages changed since they were last checked
(and, the first time, since this Codex started checking), a few a run, read-only, and reports problems
across pages with a plan for each. They're shared in `docs/.codex/problems/` (one file a page, with the
version of the page that was checked) and listed on the To-do page under Problems found: **Fix with
R'th AI** (a Chat-mode conversation with the plan), **More**, **Dismiss** (for everyone). A problem whose
quoted words are gone drops off. **Check the whole GDD** sweeps every page. Off in AI setup.

## Comments

Comments work as in Google Docs, and never go into the page.

- **On some text:** select it and click the comment button beside it (or Ctrl+M). The text is
  highlighted and the comment sits beside it in the margin.
- **The margin:** closed comments are short cards (two lines, the reply count, "New answer" when
  Claude has answered). Click one to open its thread: the passage it's on, the comment, and the replies
  hung from it on one line; long messages and long threads are cut short with *Show more* / *Show
  earlier replies*. **Hide** (above the margin, or the comment count in the status bar) folds the
  margin to small markers; it stays folded until you show it again.
- **On the whole page:** the comment button at the top of the page (or Ctrl+M with nothing selected).
  Page comments sit at the top of the margin, marked "On this page".
- **Reply, resolve, more:** click a comment to reply. The tick resolves it; ⋮ has edit, attach to the
  selected text, add to the page's Open questions (left unsaved for you to check), move, copy its
  id, delete. Ctrl+Enter posts. Escape closes the box; with words written it asks you to press it again.
- **/ask:** start a comment with /ask (offered as you type /) and R'th AI answers in the thread, as /ask
  does in Discord (in Discord, tagging the bot or writing "@R'th AI" works too). It answers there,
  tagging you, from an open Codex with Claude that has **AI on Discord** on in AI setup; only one answers (it claims the tag through the helper, `helper/src/mentions.js`,
  `src/discord-answer.js`). With none open, R'th AI says it's offline and answers once one opens. For
  changes, ask in the AI tab (Ctrl+J, or Ask beside a selection).
- **/ask in any Discord channel** (or **Apps > Ask R'th AI** on a message): the same, outside the
  comments forum. Discord sends these to the helper directly (`helper/src/interactions.js`; the app's
  Interactions Endpoint URL is `<helper>/interactions`), the answer replaces "R'th AI is thinking…", and
  a late one replies tagging whoever asked.
- **Comments started in Discord** aren't on a page: put them on one from the comment history or the
  To-do page's "Comments to place", which suggests the pages they're most likely about.
- **@agent:** assigns the comment to an agent in the next session (see the CLI below).
- **Comment history** (the speech-bubble button, top right): every comment across the docs; Open,
  New from Claude (answers you haven't opened yet, and questions Claude left you), Resolved, All.
  The badge counts what's new from Claude.
- **When a page changes**, comments find their text again: the same words, else the most similar
  words in the same section (taking up the new wording), else the section. If the text is gone, the
  card says so.
- **Where they're kept:** signed in, in Discord: each comment is a post in `#codex-comments` (the
  passage quoted, a link back to the Codex), under your Discord name and avatar; replies are messages
  in the post, and a reply typed in Discord shows in the margin within about ten seconds. Resolving
  tags the post *Resolved* and closes it; tagging it *Resolved* in Discord resolves it here too. A post
  started in Discord shows here with no page: ⋮ > attach it to the selected text. The helper keeps
  where each comment sits (`helper/src/comments.js`); `src/comment-store.js` keeps a copy and asks for
  changes. On the owner's computer that copy is also written to `docs/.notes/<page path>.json`, so git
  keeps their history. Not signed in, comments are only those files, as before. Claude needs `claude`
  on the PATH, or `CODEX_CLAUDE` set to it; its answers post in Discord as "Claude".

**For agents**, `node tools/docs-editor/notes.js` (kinds: `note` a plain comment, `ask` an @Claude question, `task` assigned): `list [--open] [--kind task|ask|note] [--page p]`,
`show <id>` (the note, its section, passage and line), `reply <id> "text" [--done]`, `done <id>`,
and `add --page p [--section "A > B"] [--quote "..."] [--kind ask] "text"` to ask the owner something.
On the owner's computer it works on the Discord comments through the helper (with the agent key in
`helper/.dev.vars`; replies post as Claude); elsewhere, or with `CODEX_LOCAL_NOTES=1`, on `docs/.notes/`.
Replies show in the Codex within seconds.

## How saving works

Saving never rewrites the whole file. The editor's Markdown is compared with what it made of the
page when it opened, block by block (and row by row in tables, item by item in lists); everything
you didn't touch is written back exactly as it was, and what you changed is wrapped at 100
columns like the rest of the docs. So a one-word edit is a one-paragraph diff.

If the file changes on disk while it's open (an agent, git, another window), a page with nothing
waiting to save just reloads; one you're editing asks which version to keep before anything is
overwritten.

## Shipping it

`npm run release` (after raising `version` in `package.json` and writing its section in `CHANGELOG.md`) builds the Windows installer into `dist/`
(`npm run dist`: the page, MinGit fetched by `scripts/fetch-mingit.mjs`, the game's look) and publishes
it on the repository's GitHub Releases (`scripts/release.mjs`, with `gh`). Announcing it (`scripts/announce.mjs`)
tells the helper's live room, which tells every signed-in open Codex to look for it at once; the rest
check the latest release every hour. They download it quietly; once it's here, an "An update is ready" screen
covers the Codex (the open page is saved at once), and Restart and update saves and sends everything,
installs it and opens the Codex again (closing it installs it too). There is no carrying on without it.

**Forcing an update.** When a release changes the GDD in ways older Codexes would damage, raise
`MIN_CODEX` in `helper/wrangler.toml` to that release's version and deploy the helper
(`npx wrangler deploy` in `helper/`), **after** the release is published, never before: older Codexes
update to it. From then on the helper refuses older Codexes anything that changes shared things
(sending pages, writing comments; reading still works), with a message to close and reopen the Codex,
and a 0.1.x Codex (which sends no version) is signed out when it opens: it shows pages read-only with
a Sign in button, and that button opens the helper's sign-in in the browser, where it gets an "Update
your Codex" page (close and reopen, or download) instead of Discord. Newer Codexes put their version in
the sign-in link (`v=`) to get through (`helper/src/version.js`, `updatePage`).
Codexes from 0.3.0 on also cover themselves with an "An update is needed" screen that downloads the
update and offers Restart and update (or, without the self-updater, a link to the latest release).
Each Codex sends its version with every request to the helper (`X-Codex-Version`); 0.1.x sent none,
so they count as too old once any minimum is set (`helper/src/version.js`, `src/codex-version.js`).

**Uninstalling** (Windows' Installed apps) leaves nothing behind: the program, the Codex's folder
(settings, sign-in, AI conversations, the docs it fetched), the updates it downloaded and the
`rth-codex://` link (`scripts/installer.nsh`). Pages not yet sent would go too, but the Codex sends
them whenever it closes. The installer isn't
signed, so Windows warns the first time (More info, Run anyway). The release is announced in #codex with its lines from
`CHANGELOG.md` and a download link (`scripts/announce.mjs`, through the helper with the owner's agent
key; `node scripts/announce.mjs <version>` announces one that's out already).

## Files

- `server.js`: a small Node server (no framework): the page, the docs, search, saving, and change
  events from watching the folders.
- `src/app.js`: the page (TipTap with `tiptap-markdown`, plus fixes noted in the code for where its
  Markdown didn't match the docs); `src/merge.js`: the block-preserving save.
- Comments: `src/anchor.js` (where a comment belongs, and finding it again), `src/notes-store.js` (the
  files in `docs/.notes`), `src/notes.js` (the highlights, margin, cards and history; /ask in a comment is answered by
  R'th AI through the helper), `notes.js` (the CLI). `src/text-picker.js` and `src/mention-query.js`:
  the list a text box opens as you type (the AI box's @ pages, a comment's /ask and @agent). `src/links.js` and `src/panels.js`:
  the link index, the Links tab, Recent changes and To-do. `src/layout.js`: the logo's layout menu,
  docked panes and sheets.
- `public/`: the page shell and its stylesheet; `npm run build` bundles `src/` into `public/app.js`
  (git-ignored, built by `npm start`).
- Tests: `npm test` (the merge, and the comments: anchoring, the store, links and open questions, in Node). `test/roundtrip.browser.js` edits every kind of block on
  every page in the browser and checks the saved Markdown renders the same; run it from the
  console with the Codex open (the file says how). Nothing is saved.
