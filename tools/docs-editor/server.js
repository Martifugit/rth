// R'th Codex: a local reader and editor for the design docs. A dev tool, never shipped with the
// game: it serves the repository's Markdown on 127.0.0.1 only and writes back what you edit.
//
//   npm start            (from tools/docs-editor; builds the page, then serves it)
//   PORT=5178 npm start  (another port)

import http from "node:http";
import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { execFile } from "node:child_process";
import { fileURLToPath } from "node:url";
import { makeStore } from "./src/notes-store.js";
import { makeAssistant } from "./src/assistant.js";
import { makeAccount } from "./src/discord-account.js";
import { makeOpenRouterAccount } from "./src/openrouter-account.js";
import { makeCommentStore } from "./src/comment-store.js";
import { agentKey } from "./src/helper-comments.js";
import { makePresence } from "./src/discord-presence.js";
import { makeTogether } from "./src/together.js";
import { HELPER } from "./src/discord-account.js";
import { required, onRequired, watchMinimum } from "./src/codex-version.js";
import { codexPage, esc } from "./src/codex-page.js";
import { buildIndex, linksFor, pageState, brokenLinks } from "./src/links.js";
import { rewriteRefs, refsTo, retitle } from "./src/refs.js";
import { buildTodo, renamesFromLog } from "./src/todo.js";
import { planFix } from "./src/fixes.js";
import { LOG_FORMAT, parseLog, entries } from "./src/changelog.js";
import os from "node:os";
import { makeSpell } from "./src/spell.js";
import { GDD, SYSTEM, TEMPLATES, undeletable, inGdd, isMeta, readSystem, readPrompts, newPageText, fromTemplate, withComponents } from "./src/gdd.js";
import { make as makeFront, split as splitFront, slugOf } from "./src/frontmatter.js";
import { buildGdd, runQuery, checks, labelFor } from "./src/gdd-index.js";
import { makeSync } from "./src/sync.js";
import { makeAuditor } from "./src/audit.js";
import { makeDiscordAnswers } from "./src/discord-answer.js";
import { findInMarkdown, replaceInMarkdown, lineOf } from "./src/find.js";

const HERE = path.dirname(fileURLToPath(import.meta.url));
// The repository the Codex works on: the one it sits in, or CODEX_REPO (checks use a throwaway copy).
const REPO = process.env.CODEX_REPO ? path.resolve(process.env.CODEX_REPO) : path.resolve(HERE, "..", "..");
const PUBLIC = path.join(HERE, "public");
// The Codex's own look (the game's fonts, frames and icons) comes from where the Codex is, not from the
// repository it works on: a contributor's copy holds only the docs. (Shipping copies these into the app.)
// (The installed app brings its own copy: CODEX_LOOK.)
const LOOK = process.env.CODEX_LOOK ? path.resolve(process.env.CODEX_LOOK) : path.resolve(HERE, "..", "..", "client", "Assets", "Resources");
const UI = path.join(LOOK, "UI");
const ICONS = path.join(LOOK, "Icons");
const PORT = Number(process.env.PORT || 5178);

// What the Codex shows: the GDD (docs/gdd) and nothing else. The rest of docs/ stays in the repository,
// out of the Codex's way.
const ROOTS = [GDD];
const SKIP_DIRS = new Set(["node_modules", ".git", "img-src"]);

const MIME = {
  ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8", ".map": "application/json", ".json": "application/json",
  ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".gif": "image/gif",
  ".webp": "image/webp", ".svg": "image/svg+xml", ".ttf": "font/ttf", ".md": "text/plain; charset=utf-8",
  ".txt": "text/plain; charset=utf-8",
};

const hash = (text) => crypto.createHash("sha1").update(text).digest("hex").slice(0, 16);

/** A repository-relative path made safe: forward slashes, inside the repository, or null. */
function safe(rel) {
  if (typeof rel !== "string" || rel === "") return null;
  const abs = path.resolve(REPO, rel.replace(/^\/+/, ""));
  if (abs !== REPO && !abs.startsWith(REPO + path.sep)) return null;
  return abs;
}
const relOf = (abs) => path.relative(REPO, abs).split(path.sep).join("/");
const isDoc = (rel) => rel.endsWith(".md") && inGdd(rel);

async function walk(dir, out) {
  for (const e of await fsp.readdir(dir, { withFileTypes: true })) {
    if (e.name.startsWith(".") || SKIP_DIRS.has(e.name)) continue;
    const abs = path.join(dir, e.name);
    if (e.isDirectory()) await walk(abs, out);
    else if (e.name.endsWith(".md")) out.push(relOf(abs));
  }
  return out;
}

async function allDocs() {
  const files = [];
  for (const r of ROOTS) { try { await walk(path.join(REPO, r), files); } catch {} }
  return files;
}

function gitStatus() {
  return new Promise((resolve) => {
    execFile(process.env.CODEX_GIT || "git", ["status", "--porcelain", "-z", "--untracked-files=all", "--", ...ROOTS],
      { cwd: REPO, maxBuffer: 16 << 20 }, (err, stdout) => {
        const status = {};
        if (!err) for (const entry of stdout.split("\0")) {
          if (entry.length < 4) continue;
          const code = entry.slice(0, 2), file = entry.slice(3);
          status[file] = code.includes("?") ? "new" : "changed";
        }
        resolve(status);
      });
  });
}

/** First heading of a file, and its frontmatter's type and status, for the page list and quick open. */
const titleOf = async (rel) => (await headOf(rel)).title;
async function headOf(rel) {
  try {
    const fd = await fsp.open(path.join(REPO, rel));
    const { buffer, bytesRead } = await fd.read(Buffer.alloc(4096), 0, 4096, 0);
    await fd.close();
    const text = buffer.slice(0, bytesRead).toString("utf8").replace(/^﻿/, "");
    const { data } = splitFront(text);
    const m = text.match(/^#\s+(.+)$/m);
    return { title: m ? m[1].replace(/[*_`]/g, "").trim() : null, type: data?.type || null, status: data?.status || null };
  } catch { return { title: null, type: null, status: null }; }
}

const readText = async (abs) => (await fsp.readFile(abs, "utf8")).replace(/^﻿/, "");

// Syncing through git (docs/ only): automatic, and the Sync button. See src/sync.js.
// Signed in with Discord, git sends docs with the helper's hour-long key and the person's Discord name.
const syncer = makeSync({ repo: REPO, emit: (type, data) => broadcast(type, data), auth: () => account.gitAuth() });
// Only a person's change to a comment (posting, replying, editing, deleting, an answer they asked for)
// counts as saved in the Codex for automatic sync; the Codex's own quiet upkeep of the files doesn't.
const localNotes = makeStore(REPO, { onWrite: (file, opts) => { if (!opts?.quiet) syncer.saved(file); } });

// The name on the owner's comments: git's user.name (the Codex is one person's tool).
let owner = null;
const ownerName = () => owner ?? new Promise((resolve) => execFile(process.env.CODEX_GIT || "git", ["config", "user.name"], { cwd: REPO }, (err, out) => resolve(owner = (!err && out.trim()) || "You")));
// The AI tab's conversations stay on this computer: CODEX_DATA, else .local/ beside this tool.
const DATA = process.env.CODEX_DATA ? path.resolve(process.env.CODEX_DATA) : path.join(HERE, ".local");
// Who's using this Codex (Sign in with Discord, through the helper): src/discord-account.js. Signing in
// or out moves the comments between the Discord forum and docs/.notes/.
const account = await makeAccount({ dir: DATA, contributor: process.env.CODEX_CONTRIBUTOR === "1", emit: (type, data) => { broadcast(type, data); if (type === "account") { try { store.reset(); } catch {} try { joinRoom(); } catch {} } } });
// Sign in with OpenRouter: free AI for anyone without Claude on their computer (src/openrouter-account.js).
const openrouter = await makeOpenRouterAccount({ dir: DATA, emit: (type, data) => broadcast(type, data) });
// Comments: in the Discord forum when signed in, in docs/.notes/ when not (src/comment-store.js). On
// the owner's computer (with the helper's agent key) the forum's comments are copied into docs/.notes/.
const store = makeCommentStore({
  repo: REPO, local: localNotes, account, emit: (type, data) => broadcast(type, data),
  titleOf: (p) => titleOf(p), watching: () => listeners.size > 0, exportCopy: !!agentKey(HERE),
  outboxFile: path.join(DATA, "comments-outbox.json"), // comments waiting for Discord (src/comment-store.js)
});
// "Editing <page> in the Codex" on the person's Discord profile, through their Discord app.
// Who's here: the room of everyone with a Codex open (src/together.js), while signed in.
const together = makeTogether({ helper: HELPER, session: () => account.session(), emit: (type, data) => broadcast(type, data), onComments: () => store.poke(), onPages: () => syncer.news(), onRelease: lookForRelease,
  // @R'th AI in Discord: whether this Codex answers (AI setup, and Claude here), and a tag handed to it.
  extra: () => ({ ai: assistant?.answersDiscord?.() || false }), onAsk: (m) => discordAnswers?.handle(m) });
let discordAnswers = null; // made once the assistant is (below)
// A release was just published (the room says so): look for it now, and twice more in case GitHub's
// "latest" hadn't caught up yet. The hourly check (electron/main.js) stays for anyone not in the room.
function lookForRelease(version) {
  console.log(`Codex ${version || "release"} is out: looking for the update.`);
  for (const ms of [0, 60 * 1000, 3 * 60 * 1000]) setTimeout(() => globalThis.codexUpdate?.check?.(), ms).unref?.();
}
let roomFor; // whose session is in the room (only signing in or out changes it)
function joinRoom() {
  const v = account.view();
  const who = v.signedIn ? v.user?.id || "signed-in" : null;
  if (who === roomFor) return;
  roomFor = who;
  if (who) together.restart(); else together.stop();
}
setTimeout(joinRoom); // once the engine is set up (its events go to the page)
const presence = makePresence({ clientId: process.env.CODEX_DISCORD_APP || "1567232969637109922" });
let engineBase = null; // http://127.0.0.1:<port>, once listening (startServer)
let assistant = null;
assistant = makeAssistant({
  repo: REPO, dir: path.join(DATA, "conversations"), store, emit: (type, data) => broadcast(type, data),
  pages: allDocs,
  read: async (rel) => { try { return (await readText(path.join(REPO, rel))).replace(/\r\n/g, "\n"); } catch { return null; } },
  // Undo writes a page back as it was (keeping the file's line endings), or deletes one the AI made.
  write: async (rel, text) => {
    const abs = safe(rel);
    if (!abs || !isDoc(rel)) return;
    index = null; // the link index is read again, so what's shown follows at once
    if (text === null) { await fsp.rm(abs, { force: true }); return; }
    await fsp.mkdir(path.dirname(abs), { recursive: true });
    let eol = "\n";
    try { if ((await fsp.readFile(abs, "utf8")).includes("\r\n")) eol = "\r\n"; } catch {}
    const tmp = abs + ".codex-tmp";
    await fsp.writeFile(tmp, eol === "\r\n" ? text.replace(/\n/g, "\r\n") : text, "utf8");
    await fsp.rename(tmp, abs);
  },
  // The AI's changes are sent like the Codex's own saves, and their commit says "with AI".
  changed: (rels) => { for (const r of rels) syncer.saved(r, { ai: true }); },
  // Suggestions someone accepted (Accept on every page, a suggested page created): their own save.
  accepted: (rels) => { for (const r of rels) syncer.saved(r, { accepted: true }); },
  hold: { start: () => aiHoldStart(), end: (pages) => aiHoldEnd(pages) },
  openrouterKey: () => openrouter.key(),
  // The tools' spelling check (made further down), the files they write under docs/.codex/ (sent like
  // the word list), and where Claude's MCP server finds the engine.
  spell: { check: (words) => speller.check(words) },
  ownChanged: (rel) => syncer.saved(rel, { soon: true }),
  engineUrl: () => engineBase,
  // A conversation started on To-do or Recent changes is told what that view lists.
  viewText: (view) => viewText(view),
  // Who links to a page (the Links tab's index), so R'th AI sees where it sits before changing it.
  linksTo: async (page) => {
    const idx = await gdd();
    const seen = new Map();
    for (const l of linksFor(idx, page).inbound) if (!seen.has(l.from)) seen.set(l.from, { path: l.from, title: l.title || null });
    return [...seen.values()];
  },
});

discordAnswers = makeDiscordAnswers({ auth: async () => account.authHeader(), store, answer: (x) => assistant.answerThread(x), enabled: () => assistant.answersDiscord() });
// Claude is looked for once at startup; then the room hears whether this Codex answers.
assistant.setup().catch(() => {}).finally(() => together.refresh());

// The GDD's health check (src/audit.js): while idle, with Claude, R'th AI checks pages changed since it
// last did; what it finds goes on the To-do page, for everyone. lastActivity: the last thing done here.
let lastActivity = Date.now();
const auditor = makeAuditor({
  repo: REPO, dataDir: DATA, pages: allDocs,
  read: async (rel) => { try { return (await readText(path.join(REPO, rel))).replace(/\r\n/g, "\n"); } catch { return null; } },
  audit: (x) => assistant.audit(x), canRun: () => assistant.canAudit(), idleFor: () => Date.now() - lastActivity,
  who: () => account.view()?.user?.name || owner || "R'th AI", ownChanged: (rel) => syncer.saved(rel, { soon: true }),
  emit: (type, data) => broadcast(type, data),
});
auditor.start();

// The link index over every page, rebuilt on demand after any page changes.
let index = null;
async function linkIndex() {
  if (index) return index;
  const files = await allDocs();
  const pages = await Promise.all(files.map(async (p) => ({ path: p, text: (await readText(path.join(REPO, p))).replace(/\r\n/g, "\n") })));
  index = buildIndex(pages);
  return index;
}

// The GDD's types, read from How the GDD Works (src/gdd.js).
// The GDD's system (types, components, who writes what), read from How the GDD Works (src/gdd.js), and
// what its pages say (src/gdd-index.js): both kept with the link index, rebuilt when a page changes.
async function gdd() {
  const idx = await linkIndex();
  if (!idx.gdd) { idx.system = readSystem(idx.texts.get(SYSTEM) || ""); idx.system.prompts = readPrompts(idx.texts, idx.system); idx.gdd = buildGdd(idx.texts); }
  return idx;
}

// Recent changes (src/changelog.js): the docs' git history, read again only when HEAD moves.
const ATTRS = path.join(os.tmpdir(), "codex-md-attributes");
let changeLog = { head: null, commits: [] };

// Pages renamed in git (for the To-do's "did you mean"), read again only when HEAD moves.
let renameLog = { head: null, map: new Map() };
async function gitRenames() {
  const head = (await gitOut(["rev-parse", "HEAD"]))?.trim();
  if (!head) return new Map();
  if (renameLog.head !== head) renameLog = { head, map: renamesFromLog(await gitOut(["-c", "core.quotePath=false", "log", "-M", "--name-status", "--format=", "-n", "500", "--", GDD])) };
  return renameLog.map;
}
// What each To-do fix changed, for Undo: token → [{ path, before, after }].
const fixUndo = new Map();
const gitOut = (args) => new Promise((resolve) => execFile(process.env.CODEX_GIT || "git", args, { cwd: REPO, maxBuffer: 128 << 20, windowsHide: true }, (err, out) => resolve(err ? null : out)));
async function recentChanges() {
  const head = (await gitOut(["rev-parse", "HEAD"]))?.trim();
  if (!head) return [];
  if (head !== changeLog.head) {
    if (!fs.existsSync(ATTRS)) fs.writeFileSync(ATTRS, "*.md diff=markdown\n");
    // diff=markdown makes each hunk header name the heading above it.
    const out = await gitOut(["-c", "core.attributesFile=" + ATTRS, "-c", "core.quotePath=false", "log", "-n", "400", "--no-merges", "--no-renames",
      "-U0", "--no-color", "--no-ext-diff", "--format=" + LOG_FORMAT, "--", ...ROOTS]);
    changeLog = { head, commits: out ? parseLog(out) : [] };
  }
  const idx = await linkIndex();
  const me = [await ownerName(), account.view()?.user?.name];
  return entries(changeLog.commits.map((c) => ({ ...c, files: c.files.filter((f) => !f.path.startsWith("docs/.notes/")) })), { titles: idx.titles, me });
}

// Spelling: US English, the docs' own repeated words, and the shared word list in the docs (src/spell.js),
// sent like comments. CODEX_WORDS is where an older installed Codex kept its own list: moved in once.
const WORDS_REL = "docs/.codex/words.txt";
const speller = makeSpell({
  wordsFile: path.join(REPO, WORDS_REL), legacy: [process.env.CODEX_WORDS],
  texts: async () => (await linkIndex()).texts, onWrite: () => syncer.saved(WORDS_REL, { soon: true }),
});

/** Open notes per page: { page: { note, ask, task, answered, total } } ("" is scratch). */
async function noteCounts() {
  const counts = {};
  for (const { page, note } of await store.all()) {
    if (note.status === "done") continue;
    const c = counts[page ?? ""] ||= { note: 0, ask: 0, task: 0, answered: 0, total: 0 };
    c[note.kind]++; c.total++;
    if (note.status === "answered") c.answered++;
  }
  return counts;
}

// The To-do (src/todo.js): missing pages, wrong references, placeholders and structure, as cards, and what
// R'th AI's health check found.
async function todoNow() {
  const idx = await gdd();
  const exists = (rel) => fs.existsSync(path.join(REPO, rel));
  const todo = buildTodo({
    idx, gdd: idx.gdd, system: idx.system, problems: checks(idx.gdd, idx.system, idx.texts),
    headings: brokenLinks(idx, exists).filter((l) => l.kind === "heading"), renames: await gitRenames(), exists,
  });
  // What R'th AI's health check found (src/audit.js), and whether this Codex can check.
  const found = (await auditor.list()).map((p) => ({ ...p, titles: p.pages.map((x) => idx.titles.get(x) || null) }));
  // Comments started in Discord that aren't on any page yet: to put on the page they're about.
  const unfiled = (await store.all().catch(() => [])).filter((x) => !x.page && x.note.status !== "done").map((x) => x.note);
  return { ...todo, found, unfiled, count: todo.count + found.length + unfiled.length, audit: { ...auditor.view(), available: await assistant.canAudit().catch(() => false) } };
}

/** What To-do or Recent changes lists now, as text for R'th AI (a conversation started on that view). */
async function viewText(view) {
  const clip = (s, n = 160) => String(s || "").replace(/\s+/g, " ").trim().slice(0, n);
  if (view === "broken") {
    const t = await todoNow();
    const lines = [];
    for (const c of t.missing.slice(0, 25)) lines.push(`- Not written yet: ${c.title} (${c.path || c.id}), linked from ${c.refs.slice(0, 4).map((r) => r.from).join(", ")}${c.refs.length > 4 ? " and more" : ""}`);
    for (const r of t.references.slice(0, 15)) lines.push(`- Wrong reference on ${r.from}: ${clip(r.text || r.to)}`);
    for (const p of t.placeholders.slice(0, 10)) lines.push(`- Placeholder name ${p.token || p.id} on ${p.pages} page(s)`);
    for (const s of t.structure.slice(0, 25)) lines.push(`- ${s.from}: ${clip(s.text)}`);
    for (const f of t.found.slice(0, 15)) lines.push(`- Found by the health check (${f.kind}) on ${f.pages.join(", ")}: ${clip(f.summary)}`);
    return `They're on the To-do page, which lists ${t.count} thing${t.count === 1 ? "" : "s"} to do in the GDD:\n` + (lines.join("\n") || "- Nothing: it's empty.");
  }
  if (view === "changes") {
    const list = (await recentChanges()).slice(0, 30);
    return "They're on Recent changes, the latest changes to the GDD:\n" + (list.map((e) => `- ${String(e.date).slice(0, 10)} ${e.author} ${e.status} ${e.title} (${e.path})${e.sections?.length ? " in " + e.sections.slice(0, 3).join(", ") : ""}`).join("\n") || "- None yet.");
  }
  return null;
}

// ----------------------------------------------------------------------------- live changes

const listeners = new Set();
function broadcast(type, data) {
  const msg = `event: ${type}\ndata: ${JSON.stringify(data)}\n\n`;
  for (const res of listeners) res.write(msg);
}
const recentWrites = new Map(); // rel -> { hash, until }: our own saves, so they don't echo back as changes
let pending = new Set(), timer = null;
// While a Chat turn runs, its edits are on disk for a moment before going back as suggestions
// (src/assistant.js): changes to pages are held, then told once it's done, less the turn's own pages
// (back as they were, so there's nothing to tell; a page it suggested making was never made).
let holds = 0, heldBack = new Set(), turnPages = new Set();
function aiHoldStart() { holds++; syncer.hold(true); }
function aiHoldEnd(pages = []) {
  for (const p of pages) turnPages.add(p);
  // A moment's wait: the file watcher reports putting the pages back a little after it's done.
  setTimeout(() => {
    if (--holds > 0) return;
    holds = 0;
    const tell = [...heldBack].filter((f) => !turnPages.has(f));
    heldBack = new Set(); turnPages = new Set();
    syncer.hold(false);
    for (const f of tell) changed(f);
  }, 400);
}
function changed(rel) {
  index = null;
  if (holds) { heldBack.add(rel); speller.invalidate(); return; }
  syncer.touched();
  speller.invalidate();
  pending.add(rel);
  clearTimeout(timer);
  timer = setTimeout(async () => {
    const files = [...pending]; pending = new Set();
    const events = [];
    for (const f of files) {
      let h = null;
      try { h = hash(await readText(path.join(REPO, f))); } catch {}
      const own = recentWrites.get(f);
      if (h && own && own.hash === h && Date.now() < own.until) continue;
      events.push({ path: f, hash: h });
    }
    if (!events.length) return;
    const data = `data: ${JSON.stringify(events)}\n\n`;
    for (const res of listeners) res.write(data);
  }, 150);
}
// docs/ is watched, not just the GDD, for the comment copies in docs/.notes/.
for (const r of ["docs"]) {
  try {
    fs.watch(path.join(REPO, r), { recursive: true }, (_, name) => {
      if (!name) return;
      const rel = r + "/" + name.toString().split(path.sep).join("/");
      if (rel.endsWith(".md") && isDoc(rel)) changed(rel);
      else if (rel.endsWith(".json") && rel.startsWith("docs/.notes/")) notesChanged(rel);
      else if (rel === WORDS_REL) speller.reload(); // others' words, fetched
    });
  } catch (e) { console.warn("Not watching", r, e.message); }
}
// Notes changed on disk (an agent's CLI, git): tell the page, unless it was our own write.
const notesPending = new Map();
function notesChanged(file) {
  clearTimeout(notesPending.get(file));
  notesPending.set(file, setTimeout(async () => {
    notesPending.delete(file);
    const page = store.pageFor(file);
    if (page === undefined) return;
    let text = null;
    try { text = await fsp.readFile(path.join(REPO, file), "utf8"); } catch {}
    const own = store.lastWrites();
    if (own.has(file) && own.get(file) === text) { own.delete(file); return; }
    broadcast("notes", { page });
  }, 120));
}
fs.watch(REPO, (_, name) => { if (name && name.toString().endsWith(".md")) changed(name.toString()); });

// ----------------------------------------------------------------------------- http

function send(res, code, body, type = "application/json") {
  const data = type === "application/json" ? JSON.stringify(body) : body;
  res.writeHead(code, { "Content-Type": type, "Cache-Control": "no-store" });
  res.end(data);
}

async function body(req) {
  const chunks = [];
  for await (const c of req) chunks.push(c);
  return JSON.parse(Buffer.concat(chunks).toString("utf8") || "{}");
}

async function serveFile(res, abs) {
  try {
    const data = await fsp.readFile(abs);
    res.writeHead(200, { "Content-Type": MIME[path.extname(abs).toLowerCase()] || "application/octet-stream", "Cache-Control": "no-cache" });
    res.end(data);
  } catch { send(res, 404, { error: "not found" }); }
}

const api = {
  async "GET /api/tree"() {
    const [files, status, counts] = await Promise.all([allDocs(), gitStatus(), noteCounts()]);
    const heads = await Promise.all(files.map(headOf));
    return { files: files.map((p, i) => ({ path: p, title: heads[i].title, type: heads[i].type, status: heads[i].status, git: status[p] || null, notes: counts[p]?.total || 0, answered: counts[p]?.answered || 0 })) };
  },

  async "GET /api/doc"(url) {
    const rel = url.searchParams.get("path"), abs = safe(rel);
    if (!abs || !rel.endsWith(".md")) return [400, { error: "not a document" }];
    try {
      const raw = await readText(abs);
      const eol = raw.includes("\r\n") ? "\r\n" : "\n";
      const content = raw.replace(/\r\n/g, "\n");
      return { path: relOf(abs), content, eol, hash: hash(raw) };
    } catch { return [404, { error: "no such document" }]; }
  },

  async "PUT /api/doc"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to edit." }];
    const { path: rel, content, eol, baseHash, force } = await body(req);
    const abs = safe(rel);
    if (!abs || !isDoc(rel)) return [400, { error: "can only write documents under docs/" }];
    if (typeof content !== "string") return [400, { error: "no content" }];
    let current = null;
    try { current = await readText(abs); } catch {}
    if (current !== null && baseHash && hash(current) !== baseHash && !force)
      return [409, { error: "changed on disk", content: current.replace(/\r\n/g, "\n"), hash: hash(current) }];
    return { ok: true, hash: await writeDoc(abs, content, eol) };
  },

  async "POST /api/new"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to make pages." }];
    const { path: rel, title, type, components: picked } = await body(req);
    const abs = safe(rel);
    if (!abs || !isDoc(rel)) return [400, { error: "New pages go in the GDD." }];
    if (fs.existsSync(abs)) return [409, { error: "A page with that name already exists in that folder." }];
    const name = String(title || "").trim() || path.basename(rel, ".md").replace(/[-_]/g, " ").replace(/^./, (c) => c.toUpperCase());
    const system = (await gdd()).system;
    const t = system.types.find((x) => x.type === type) || null;
    // Made from its type's template (templates/<type>.md); a type with no template yet gets its core headings.
    let text = null;
    if (type && /^[a-z-]+$/.test(type)) { try { text = fromTemplate(await readText(path.join(REPO, TEMPLATES, type + ".md")), name); } catch {} }
    if (text == null) text = newPageText(name, t, makeFront);
    // The components picked at creation (usually none): their sections from templates/<component>.md, in the canonical order.
    const names = (Array.isArray(picked) ? picked : []).filter((c) => typeof c === "string" && system.components.some((x) => x.name === c));
    if (names.length) {
      const templates = new Map();
      for (const c of names) { try { templates.set(c, await readText(path.join(REPO, TEMPLATES, c + ".md"))); } catch { templates.set(c, null); } }
      text = withComponents(text, names, system.components, t, templates);
    }
    await fsp.mkdir(path.dirname(abs), { recursive: true });
    await fsp.writeFile(abs, text, "utf8");
    recentWrites.set(relOf(abs), { hash: hash(text), until: Date.now() + 3000 });
    syncer.saved(relOf(abs), { soon: true });
    return { ok: true, path: relOf(abs) };
  },

  // Renaming or moving a page (a new title and file name, or a new type and so a new folder): written at
  // its new place and gone from the old, with every reference to it rewritten in every page (links,
  // groups, based-on, query blocks: src/refs.js) and its comments moved along. Sent within seconds.
  async "POST /api/move"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to move pages." }];
    const { from, to, title } = await body(req);
    const a = safe(from), b = safe(to);
    if (!a || !b || !isDoc(from) || !isDoc(to)) return [400, { error: "Pages move within the GDD." }];
    if (from === to && !title) return { ok: true, path: to, rewritten: [] };
    if (from !== to && fs.existsSync(b)) return [409, { error: "A page with that name is already there." }];
    const idx = await linkIndex();
    // File names are unique across the whole GDD: they are the pages' IDs.
    const id = slugOf(to);
    const clash = [...idx.texts.keys()].find((p) => p !== from && inGdd(p) && !p.startsWith(TEMPLATES + "/") && slugOf(p) === id);
    if (clash) return [409, { error: `“${id}” is already the name of ${clash.slice(GDD.length + 1)}: names are unique across the GDD.` }];
    let text;
    try { text = await readText(a); } catch { return [404, { error: "No such page." }]; }
    const write = async (abs, content) => {
      await fsp.mkdir(path.dirname(abs), { recursive: true });
      await fsp.writeFile(abs, content, "utf8");
      recentWrites.set(relOf(abs), { hash: hash(content), until: Date.now() + 3000 });
    };
    let moved = rewriteRefs(text, from, from, to);
    if (typeof title === "string" && title.trim()) moved = retitle(moved, title.trim());
    await write(b, moved);
    if (from !== to) {
      await fsp.rm(a);
      try { const dir = path.dirname(a); if (dir.startsWith(path.join(REPO, "docs") + path.sep) && !(await fsp.readdir(dir)).length) await fsp.rmdir(dir); } catch {}
    }
    const rewritten = [];
    if (from !== to) {
      for (const p of idx.texts.keys()) {
        if (p === from || p === to) continue;
        const abs = safe(p);
        if (!abs) continue;
        let t;
        try { t = await readText(abs); } catch { continue; }
        const next = rewriteRefs(t, p, from, to);
        if (next !== t) { await write(abs, next); rewritten.push(p); syncer.saved(p, { soon: true }); }
      }
      for (const n of await store.load(from).catch(() => [])) await store.move(n.id, to).catch(() => null);
    }
    index = null;
    syncer.saved(from, { soon: true });
    syncer.saved(to, { soon: true });
    return { ok: true, path: to, rewritten };
  },

  // Everything that refers to a page (src/refs.js): links, groups, based-on and query blocks, by page.
  async "GET /api/refs"(url) {
    const page = url.searchParams.get("page");
    if (!page || !safe(page)) return [400, { error: "which page?" }];
    const idx = await linkIndex();
    return { refs: refsTo(idx.texts, page).map((r) => ({ ...r, title: idx.titles.get(r.from) || null })) };
  },

  // Deleting a page: gone from the docs (git keeps its history), sent within seconds. The reply holds
  // its text, so the page can offer Undo (POST /api/doc/restore). Its comments stay, as comments on a
  // page that's gone (the comment history lists them, and they can be moved to another page).
  async "DELETE /api/doc"(url) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to delete pages." }];
    const rel = url.searchParams.get("path") || "";
    const abs = safe(rel);
    if (!abs || !isDoc(rel)) return [400, { error: "Only pages under docs/ can be deleted." }];
    if (undeletable(rel)) return [400, { error: "The top pages and the templates can't be deleted." }];
    let text;
    try { text = await fsp.readFile(abs, "utf8"); } catch { return [404, { error: "No such page." }]; }
    await fsp.rm(abs);
    // A folder left empty goes too (git doesn't keep empty folders anyway).
    try { const dir = path.dirname(abs); if (dir.startsWith(path.join(REPO, "docs") + path.sep) && !(await fsp.readdir(dir)).length) await fsp.rmdir(dir); } catch {}
    index = null;
    syncer.saved(relOf(abs), { soon: true });
    return { ok: true, path: relOf(abs), content: text };
  },
  async "POST /api/doc/restore"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to edit." }];
    const { path: rel, content } = await body(req);
    const abs = safe(rel);
    if (!abs || !isDoc(rel) || typeof content !== "string") return [400, { error: "bad page" }];
    if (fs.existsSync(abs)) return [409, { error: "A page with that name is there again already." }];
    await fsp.mkdir(path.dirname(abs), { recursive: true });
    await fsp.writeFile(abs, content, "utf8");
    recentWrites.set(relOf(abs), { hash: hash(content), until: Date.now() + 3000 });
    index = null;
    syncer.saved(relOf(abs), { soon: true });
    return { ok: true, path: relOf(abs) };
  },

  // ------------------------------------------------------------------- notes

  async "GET /api/notes"(url) {
    const page = url.searchParams.get("page") || null;
    if (page && !safe(page)) return [400, { error: "bad page" }];
    return { page, notes: await store.load(page) };
  },

  async "GET /api/notes/all"(url) {
    const status = url.searchParams.get("status"); // "open": everything not done
    const list = (await store.all()).filter(({ note }) => !status || (status === "open" ? note.status !== "done" : note.status === status));
    list.sort((a, b) => b.note.updated.localeCompare(a.note.updated));
    return { notes: list, orphans: (await store.orphans()).map((x) => x.note.id) };
  },

  async "POST /api/notes"(url, req) {
    const { page = null, kind, text, anchor } = await body(req);
    if (page && !(safe(page) && page.endsWith(".md"))) return [400, { error: "notes go on pages" }];
    if (!text || !String(text).trim()) return [400, { error: "an empty note" }];
    const made = await store.create(page, { kind, text, anchor });
    return made;
  },

  // ------------------------------------------------------------------- the AI tab (src/assistant.js)

  async "GET /api/ai"() {
    return { provider: await assistant.check(), conversations: await assistant.list(), ...assistant.status() };
  },

  async "GET /api/ai/conversation"(url) {
    const c = await assistant.get(url.searchParams.get("id"));
    return c ? c : [404, { error: "no such conversation" }];
  },

  async "POST /api/ai/send"(url, req) {
    const { id, text, context, mode, problem } = await body(req);
    const r = await assistant.send({ id, text, context, mode, problem });
    return r.error ? [400, r] : r;
  },

  // The Codex's tools for Claude (src/ai-tools.js), asked by its MCP server (src/mcp-server.js) with the
  // turn's key: which tools that turn has, and one of them run.
  async "POST /api/ai/tools"(url, req) { const r = assistant.toolList(await body(req)); return r.error ? [403, r] : r; },
  async "POST /api/ai/tool"(url, req) { const r = await assistant.tool(await body(req)); return r.error ? [403, r] : r; },

  // What a conversation keeps (src/assistant.js, setContext): a page added with @ or + Add page, one dropped.
  async "POST /api/ai/context"(url, req) {
    const r = await assistant.setContext(await body(req));
    return r.error ? [400, r] : r;
  },

  async "POST /api/ai/stop"(url, req) {
    const { id } = await body(req);
    return { ok: assistant.stop(id) };
  },

  async "GET /api/ai/changes"(url) {
    const page = url.searchParams.get("page");
    return { changes: page ? await assistant.changesFor(page) : [] };
  },
  async "POST /api/ai/reviewed"(url, req) { return assistant.reviewed(await body(req)); },
  async "POST /api/ai/problem"(url, req) { const r = await assistant.problemDone(await body(req)); return r.error ? [400, r] : r; },
  async "POST /api/ai/accept"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to change pages." }];
    const r = await assistant.acceptSuggestions(await body(req)); return r.error ? [400, r] : r;
  },
  async "POST /api/ai/create-page"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to make pages." }];
    const r = await assistant.createSuggested(await body(req));
    return r.error ? [400, r] : r;
  },
  async "POST /api/ai/undo"(url, req) { const r = await assistant.undo(await body(req)); return r.error ? [400, r] : r; },

  async "POST /api/ai/delete"(url, req) {
    const { id } = await body(req);
    return { ok: await assistant.remove(id) };
  },

  async "POST /api/spell"(url, req) {
    const { words } = await body(req);
    return { wrong: Array.isArray(words) ? await speller.check(words.slice(0, 5000)) : [] };
  },
  async "GET /api/spell/suggest"(url) { return { suggestions: speller.suggest(url.searchParams.get("w") || "") }; },
  async "POST /api/spell/add"(url, req) { const { word } = await body(req); return { ok: await speller.add(word) }; },

  // Where you are, for the others (src/together.js), and who they are.
  async "GET /api/here"() { return together.view(); },
  async "POST /api/here"(url, req) { together.set(await body(req)); return { ok: true }; },

  async "POST /api/presence"(url, req) {
    const { page } = await body(req);
    presence.set(page && safe(page) ? { page, title: (await titleOf(page)) || page.split("/").pop() } : null);
    return { ok: true };
  },

  // A pasted image (a file, or a Discord image link, which expires within a day): saved under
  // docs/gdd/images/, and its path relative to the page given back for the editor to insert.
  async "POST /api/image"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to add images." }];
    const { page, data, type, name, url: from } = await body(req);
    const pageAbs = safe(page);
    if (!pageAbs || !isDoc(page)) return [400, { error: "Images go in pages under docs/." }];
    let bytes, mime = String(type || "");
    if (from) {
      if (!/^https:\/\/(?:cdn\.discordapp\.com|media\.discordapp\.net)\//i.test(from)) return [400, { error: "Only Discord's image links are fetched." }];
      const r = await fetch(from);
      if (!r.ok) return [400, { error: `Discord didn't give the image (${r.status}); its link may have expired.` }];
      mime = r.headers.get("content-type") || "";
      bytes = Buffer.from(await r.arrayBuffer());
    } else bytes = Buffer.from(String(data || ""), "base64");
    const ext = { "image/png": "png", "image/jpeg": "jpg", "image/gif": "gif", "image/webp": "webp" }[mime.split(";")[0].trim()];
    if (!ext) return [400, { error: "That isn't a PNG, JPEG, GIF or WebP image." }];
    if (!bytes.length || bytes.length > 15 << 20) return [400, { error: "The image is empty or over 15 MB." }];
    const stem = String(name || (from ? new URL(from).pathname.split("/").pop() : "") || "image").replace(/\.[^.]+$/, "").replace(/[^\w-]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 40) || "image";
    const rel = `${GDD}/images/${new Date().toISOString().slice(0, 10)}-${stem}-${crypto.randomBytes(2).toString("hex")}.${ext}`;
    const abs = path.join(REPO, rel);
    await fsp.mkdir(path.dirname(abs), { recursive: true });
    await fsp.writeFile(abs, bytes);
    syncer.saved(rel);
    return { saved: rel, src: path.relative(path.dirname(pageAbs), abs).split(path.sep).join("/") };
  },

  // ------------------------------------------------------------------- the account (src/discord-account.js)

  async "GET /api/account"() { return account.check(); },
  async "GET /api/openrouter"() { return openrouter.view(); },
  async "POST /api/openrouter/signin"(url, req) { return { url: openrouter.signInUrl("http://" + req.headers.host) }; },
  async "POST /api/openrouter/signout"() { return openrouter.signOut(); },
  // Updates of the desktop app (electron/main.js): the title bar's Update button.
  // The update the installed Codex is downloading (electron/main.js), and whether this one is too old to
  // use (src/codex-version.js): then the page shows the update screen until it's installed.
  async "GET /api/update"() { return { ...(globalThis.codexUpdate ? globalThis.codexUpdate.view() : { state: "none" }), required: required(), installed: !!globalThis.codexUpdate }; },
  async "POST /api/update/install"() { return globalThis.codexUpdate ? globalThis.codexUpdate.install() : [400, { error: "Updates come with the installed Codex." }]; },
  async "POST /api/openrouter/key"(url, req) { const r = await openrouter.useKey((await body(req)).key); return r.ok ? r : [400, r]; },
  // AI setup (the AI tab's cog): which AI answers, and the ways in.
  async "GET /api/ai/setup"(url) { return assistant.setup({ recheck: url.searchParams.get("recheck") === "1" }); },
  async "POST /api/ai/setup"(url, req) { const r = await assistant.configure(await body(req)); together.refresh(); return r.error ? [400, r] : r; },
  async "POST /api/ai/claude-signin"() { return assistant.signInClaude(); },
  async "POST /api/account/signin"(url, req) { return { url: account.signInUrl("http://" + req.headers.host) }; },
  async "POST /api/account/signout"() { return account.signOut(); },

  async "GET /api/sync"() { return syncer.refresh(); },
  async "POST /api/sync/resolve"(url, req) { const { keep } = await body(req); return syncer.resolve(keep); },
  async "POST /api/sync"(url) { return syncer.sync({ why: url.searchParams.get("why") || "page" }); },
  // Held pages (the AI's, another program's): share them, or put them back (src/sync.js).
  async "POST /api/sync/share"(url, req) {
    const { files } = await body(req);
    for (const f of files || []) await assistant.reviewed({ page: f }); // shared: the AI's marks are done too
    return syncer.share(files);
  },
  async "POST /api/sync/discard"(url, req) {
    const { files } = await body(req);
    for (const f of files || []) await assistant.reviewed({ page: f });
    return syncer.discard(files);
  },
  async "GET /api/sync/base"(url) { return { text: await syncer.base(url.searchParams.get("page") || "") }; },

  async "GET /api/agent"() { return { owner: await ownerName() }; }, // who "You" is in comments

  async "GET /api/links"(url) {
    const page = url.searchParams.get("page");
    if (!page) return [400, { error: "which page?" }];
    const idx = await gdd();
    const r = linksFor(idx, page);
    // Links in, named by the section they sit in ("Dropped by" from a rewards section): How the GDD Works, "Who writes what".
    for (const l of r.inbound) l.label = labelFor(idx.gdd, idx.system, l.from, l.section);
    return r;
  },

  async "GET /api/gdd"() { return (await gdd()).system; },

  // A generated list: a query block's text, run from the page it's on (src/gdd-index.js).
  async "POST /api/query"(url, req) {
    const { page, q } = await body(req);
    const idx = await gdd();
    return runQuery(idx.gdd, idx, page, q);
  },

  async "GET /api/checks"() {
    const idx = await gdd();
    return { problems: checks(idx.gdd, idx.system, idx.texts) };
  },

  // What a page's bottom box shows, all computed from other pages: links in, by what they mean
  // ("Dropped by", from How the GDD Works' "Who writes what"), a group's members, a skill's training.
  async "GET /api/generated"(url) {
    const page = url.searchParams.get("page");
    if (!page) return [400, { error: "which page?" }];
    const idx = await gdd();
    const info = idx.gdd.pages.get(page);
    const groups = new Map();
    for (const l of linksFor(idx, page).inbound) {
      const label = labelFor(idx.gdd, idx.system, l.from, l.section) || "Linked from";
      if (!groups.has(label)) groups.set(label, new Map());
      groups.get(label).set(l.from, { path: l.from, title: l.title, section: l.section });
    }
    const type = info?.front?.type;
    const members = type === "group" ? runQuery(idx.gdd, idx, page, "groups: this").rows : null;
    const training = type === "skill" ? runQuery(idx.gdd, idx, page, "action.skill: this\nsort: level") : null;
    const order = (a, b) => (a === "Linked from") - (b === "Linked from") || a.localeCompare(b);
    return {
      links: [...groups].sort(([a], [b]) => order(a, b)).map(([label, m]) => ({ label, pages: [...m.values()] })),
      members, training: training && training.rows.length ? training : null,
    };
  },

  async "GET /api/changes"() { return { changes: await recentChanges() }; },

  async "GET /api/todo"() { return todoNow(); },
  async "POST /api/problems/dismiss"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to change the To-do." }];
    const r = auditor.dismiss(await body(req)); return r.error ? [400, r] : r;
  },
  async "POST /api/audit/sweep"() { return auditor.sweep(); },
  // Check now, whatever the idle time (for trying it out).
  async "POST /api/audit/run"() { const r = await auditor.runOnce(); return r.error ? [400, r] : r; },

  // A To-do fix (src/fixes.js). With `dry`, only which pages it would change; otherwise it changes them
  // (each keeping its line endings) and answers with a token for Undo.
  async "POST /api/fix"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to fix pages." }];
    const { fix, dry } = await body(req);
    const idx = await gdd();
    let plan;
    try { plan = planFix(fix || {}, { texts: idx.texts, system: idx.system, template: (name) => idx.texts.get(TEMPLATES + "/" + name + ".md") ?? null }); }
    catch (e) { return [400, { error: e.message }]; }
    const pages = [...plan.keys()].map((p) => ({ path: p, title: idx.titles.get(p) || null }));
    if (dry || !plan.size) return { ok: true, pages };
    const done = [];
    for (const [rel, next] of plan) {
      const abs = safe(rel);
      if (!abs) continue;
      const before = await readText(abs);
      const after = before.includes("\r\n") ? next.replace(/\r?\n/g, "\r\n") : next;
      await fsp.writeFile(abs, after, "utf8");
      recentWrites.set(rel, { hash: hash(after), until: Date.now() + 3000 });
      syncer.saved(rel, { soon: true });
      done.push({ path: rel, before, after });
    }
    index = null;
    const token = crypto.randomBytes(6).toString("hex");
    fixUndo.set(token, done);
    if (fixUndo.size > 50) fixUndo.delete(fixUndo.keys().next().value);
    return { ok: true, pages, token };
  },
  // Undo a fix: each page goes back unless someone changed it since.
  async "POST /api/fix/undo"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to fix pages." }];
    const { token } = await body(req);
    const done = fixUndo.get(token);
    if (!done) return [404, { error: "Nothing to undo." }];
    const skipped = [];
    for (const { path: rel, before, after } of done) {
      const abs = safe(rel);
      let now = null;
      try { now = await readText(abs); } catch {}
      if (now !== after) { skipped.push(rel); continue; }
      await fsp.writeFile(abs, before, "utf8");
      recentWrites.set(rel, { hash: hash(before), until: Date.now() + 3000 });
      syncer.saved(rel, { soon: true });
    }
    fixUndo.delete(token);
    index = null;
    return { ok: true, skipped };
  },

  async "GET /api/broken"() {
    const idx = await linkIndex();
    const links = brokenLinks(idx, (rel) => fs.existsSync(path.join(REPO, rel)));
    return { links };
  },

  async "GET /api/board"() {
    const [idx, status, counts] = await Promise.all([linkIndex(), gitStatus(), noteCounts()]);
    const pages = [];
    for (const [p, text] of idx.texts) {
      if (!inGdd(p)) continue;
      let mtime = null;
      try { mtime = (await fsp.stat(path.join(REPO, p))).mtimeMs; } catch {}
      pages.push({ path: p, title: idx.titles.get(p), ...pageState(text), git: status[p] || null, notes: counts[p] || null, mtime });
    }
    pages.sort((a, b) => a.path.localeCompare(b.path, "en", { numeric: true }));
    return { pages };
  },

  // Pages most like some text (a comment started in Discord, to put on a page): R'th AI's section index.
  async "GET /api/pages-like"(url) {
    const q = (url.searchParams.get("q") || "").trim().slice(0, 1000);
    if (q.length < 2) return { pages: [] };
    const idx = await assistant.tools.freshSections();
    // A page's score: its best section, and a little for every other section that matches too (a page
    // about the thing matches all over). The GDD's own pages (the Charter, How the GDD Works...) never.
    const by = new Map();
    for (const hit of idx.search(q, { limit: 80 })) {
      if (isMeta(hit.path)) continue;
      const x = by.get(hit.path) || { path: hit.path, title: hit.title || null, best: 0, rest: 0 };
      if (hit.score > x.best) { x.rest += x.best; x.best = hit.score; } else x.rest += hit.score;
      by.set(hit.path, x);
    }
    const pages = [...by.values()].map((x) => ({ path: x.path, title: x.title, score: x.best + 0.35 * x.rest }))
      .sort((a, b) => b.score - a.score).slice(0, 5);
    return { pages };
  },
  async "GET /api/search"(url) {
    const q = (url.searchParams.get("q") || "").trim();
    if (q.length < 2) return { results: [] };
    const needle = q.toLowerCase();
    const results = [];
    let total = 0;
    for (const file of await allDocs()) {
      if (file.startsWith(TEMPLATES + "/")) continue; // the templates aren't pages to find (New page uses them)
      const lines = (await readText(path.join(REPO, file))).split(/\r?\n/);
      const hits = [];
      let count = 0;
      lines.forEach((line, i) => {
        const lower = line.toLowerCase();
        let at = lower.indexOf(needle);
        while (at >= 0) {
          if (hits.length < 6) hits.push({ line: i + 1, text: line.trim().slice(0, 240), occurrence: count });
          count++;
          at = lower.indexOf(needle, at + needle.length);
        }
      });
      const nameHit = file.toLowerCase().includes(needle);
      if (count || nameHit) { results.push({ path: file, count, hits, nameHit }); total += count; }
    }
    results.sort((a, b) => (b.nameHit - a.nameHit) || (b.count - a.count));
    return { results: results.slice(0, 60), total };
  },

  // Find in all pages (src/find-all.js): the pages' text only (src/find.js), and Replace in them.
  async "GET /api/find"(url) {
    const q = url.searchParams.get("q") || "";
    const opts = { matchCase: url.searchParams.get("case") === "1", wholeWord: url.searchParams.get("word") === "1" };
    if (!q.trim()) return { results: [], total: 0 };
    const results = [];
    let total = 0;
    for (const file of await findablePages()) {
      const md = (await readText(path.join(REPO, file))).replace(/\r\n/g, "\n");
      const hits = findInMarkdown(md, q, opts);
      if (!hits.length) continue;
      total += hits.length;
      results.push({ path: file, count: hits.length, hits: hits.slice(0, 20).map((m) => ({ ...lineOf(md, m.start), len: m.end - m.start })) });
    }
    results.sort((a, b) => b.count - a.count || a.path.localeCompare(b.path));
    return { results, total };
  },
  async "POST /api/replace"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to edit." }];
    const { q, with: replacement = "", matchCase = false, wholeWord = false, pages = [] } = await body(req);
    if (!q) return [400, { error: "nothing to find" }];
    const findable = new Set(await findablePages());
    const done = [], before = new Map();
    for (const file of pages) {
      if (!findable.has(file)) continue;
      const abs = path.join(REPO, file);
      const raw = await readText(abs);
      const r = replaceInMarkdown(raw.replace(/\r\n/g, "\n"), q, replacement, { matchCase, wholeWord });
      if (!r.count) continue;
      before.set(file, raw);
      done.push({ path: file, count: r.count, hash: await writeDoc(abs, r.text, raw.includes("\r\n") ? "\r\n" : "\n") });
    }
    // Undo puts back what each page was, if nobody has changed it since.
    lastReplace = { id: crypto.randomUUID(), before, after: new Map(done.map((d) => [d.path, d.hash])) };
    return { ok: true, pages: done, count: done.reduce((n, d) => n + d.count, 0), undo: lastReplace.id };
  },
  async "POST /api/replace/undo"(url, req) {
    const { id } = await body(req);
    if (!lastReplace || lastReplace.id !== id) return [409, { error: "That replace can't be undone any more." }];
    const kept = [];
    for (const [file, raw] of lastReplace.before) {
      const abs = path.join(REPO, file);
      if (hash(await readText(abs)) !== lastReplace.after.get(file)) { kept.push(file); continue; } // changed since: left as it is
      await writeDoc(abs, raw.replace(/\r\n/g, "\n"), raw.includes("\r\n") ? "\r\n" : "\n");
    }
    lastReplace = null;
    return { ok: true, kept };
  },
};

/** The pages Find in all pages looks through: the docs, not the templates (New page uses those). */
async function findablePages() {
  return (await allDocs()).filter((f) => isDoc(f) && !f.startsWith(TEMPLATES + "/"));
}
let lastReplace = null; // { id, before: Map(page → text), after: Map(page → hash) }

/** Write a page as every save does: whole or not at all, in its own line endings, then shared. */
async function writeDoc(abs, content, eol) {
  const text = eol === "\r\n" ? content.replace(/\n/g, "\r\n") : content;
  const tmp = abs + ".codex-tmp";
  await fsp.writeFile(tmp, text, "utf8");
  await fsp.rename(tmp, abs);
  const h = hash(text);
  recentWrites.set(relOf(abs), { hash: h, until: Date.now() + 3000 });
  syncer.saved(relOf(abs));
  return h;
}

async function noteApi(req, id, action) {
  const m = req.method;
  const missing = [404, { error: "no such note" }];
  if (!action && m === "GET") return (await store.find(id)) || missing;
  if (!action && m === "PATCH") return (await store.update(id, await body(req))) || missing;
  if (!action && m === "DELETE") { return (await store.remove(id)) ? { ok: true } : missing; }
  if (action === "reply" && m === "POST") {
    const { text, status } = await body(req);
    if (!text || !String(text).trim()) return [400, { error: "an empty reply" }];
    const done = await store.reply(id, { by: "owner", text, status });
    if (!done) return missing;
    return done;
  }
  if (action === "move" && m === "POST") {
    const { page = null } = await body(req);
    if (page && !(safe(page) && page.endsWith(".md"))) return [400, { error: "not a page" }];
    return (await store.move(id, page)) || missing;
  }
  return [405, { error: "method not allowed" }];
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, "http://localhost");
  try {
    if (url.pathname === "/api/events") {
      res.writeHead(200, { "Content-Type": "text/event-stream", "Cache-Control": "no-store", Connection: "keep-alive" });
      res.write(": hello\n\n");
      listeners.add(res);
      const ping = setInterval(() => res.write(": ping\n\n"), 25000);
      req.on("close", () => { listeners.delete(res); clearInterval(ping); if (!listeners.size) presence.set(null); });
      return;
    }
    // The browser, back from signing in with Discord: a page to close.
    if (url.pathname === "/api/account/done" || url.pathname === "/api/openrouter/done") {
      const r = await (url.pathname === "/api/account/done" ? account : openrouter).done(url.searchParams);
      res.writeHead(r.ok ? 200 : 400, { "Content-Type": "text/html; charset=utf-8" });
      return res.end(codexPage({ heading: r.ok ? "Signed in" : "Couldn't sign in",
        body: `<p${r.ok ? "" : ' class="bad"'}>${esc(r.message)}</p><p>You can close this tab and go back to Codex.</p>` }));
    }
    // A comment's id: n-... in docs/.notes/, the forum post's number in Discord, pending-... while it waits
    // to be posted (src/comment-store.js, the outbox).
    const noteRoute = url.pathname.match(/^\/api\/notes\/(n-[\w-]+|\d{5,25}|pending-[a-z0-9]+)(?:\/(reply|move|run|stop))?$/);
    if (noteRoute) {
      const result = await noteApi(req, noteRoute[1], noteRoute[2] || null);
      return Array.isArray(result) ? send(res, result[0], result[1]) : send(res, 200, result);
    }
    // Something done here (not just looked at): the health check waits until things have been quiet a while.
    if (req.method !== "GET" && !/^\/api\/(here|presence|spell|ai\/tools?|audit)\b/.test(url.pathname)) lastActivity = Date.now();
    const handler = api[`${req.method} ${url.pathname}`];
    if (handler) {
      const result = await handler(url, req);
      return Array.isArray(result) ? send(res, result[0], result[1]) : send(res, 200, result);
    }
    if (req.method !== "GET") return send(res, 405, { error: "method not allowed" });
    if (url.pathname.startsWith("/repo/")) {
      const abs = safe(decodeURIComponent(url.pathname.slice(6)));
      return abs ? serveFile(res, abs) : send(res, 403, { error: "outside the repository" });
    }
    if (url.pathname.startsWith("/ui/")) {
      const abs = path.resolve(UI, decodeURIComponent(url.pathname.slice(4)));
      return abs.startsWith(UI + path.sep) ? serveFile(res, abs) : send(res, 403, {});
    }
    if (url.pathname.startsWith("/icons/")) {
      const abs = path.resolve(ICONS, decodeURIComponent(url.pathname.slice(7)));
      return abs.startsWith(ICONS + path.sep) ? serveFile(res, abs) : send(res, 403, {});
    }
    const file = url.pathname === "/" || !path.extname(url.pathname) ? "index.html" : url.pathname.slice(1);
    const abs = path.resolve(PUBLIC, file);
    return abs.startsWith(PUBLIC + path.sep) ? serveFile(res, abs) : send(res, 403, {});
  } catch (e) {
    console.error(e);
    send(res, 500, { error: e.message });
  }
});

/**
 * Start the engine on 127.0.0.1 (port 0: any free one). Resolves to the port it listens on.
 * autoSync: sync through git by itself (the desktop app turns it on; the browser version and checks
 * don't, unless CODEX_AUTOSYNC=1). The Sync button works either way.
 */
export function startServer(port = PORT, { autoSync = process.env.CODEX_AUTOSYNC === "1" } = {}) {
  if (autoSync) syncer.startAuto();
  // Out of date (the helper's minimum, or a refusal): the page shows the update screen.
  onRequired((r) => { broadcast("required", r); if (r.outdated) globalThis.codexUpdate?.check?.(); });
  if (!process.env.CODEX_NO_VERSION_CHECK) watchMinimum(HELPER);
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, "127.0.0.1", () => {
      const actual = server.address().port;
      engineBase = `http://127.0.0.1:${actual}`;
      console.log(`R'th Codex: http://127.0.0.1:${actual}/  (docs from ${REPO})`);
      resolve(actual);
    });
  });
}

// Run on its own (npm start, the browser version): start straight away. The desktop app
// (electron/main.js) imports this file and starts it itself.
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) startServer();
