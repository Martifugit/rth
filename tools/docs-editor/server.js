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
import { makeRunner } from "./src/agent-run.js";
import { makeAssistant } from "./src/assistant.js";
import { makeAccount } from "./src/discord-account.js";
import { makeOpenRouterAccount } from "./src/openrouter-account.js";
import { makeCommentStore } from "./src/comment-store.js";
import { agentKey } from "./src/helper-comments.js";
import { makePresence } from "./src/discord-presence.js";
import { buildIndex, linksFor, pageState } from "./src/links.js";
import { makeSpell } from "./src/spell.js";
import { makeSync } from "./src/sync.js";

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

// What the Codex shows: everything under docs/, and the Markdown at the repository's root.
const ROOTS = ["docs"];
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
const isDoc = (rel) => rel.endsWith(".md") && (ROOTS.some((r) => rel.startsWith(r + "/")) || !rel.includes("/"));

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
  for (const r of ROOTS) await walk(path.join(REPO, r), files);
  for (const e of await fsp.readdir(REPO, { withFileTypes: true }))
    if (e.isFile() && e.name.endsWith(".md")) files.push(e.name);
  return files;
}

function gitStatus() {
  return new Promise((resolve) => {
    execFile(process.env.CODEX_GIT || "git", ["status", "--porcelain", "-z", "--untracked-files=all", "--", ...ROOTS, "*.md"],
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

/** First heading of a file, for titles in the tree and quick open. */
async function titleOf(rel) {
  try {
    const fd = await fsp.open(path.join(REPO, rel));
    const { buffer, bytesRead } = await fd.read(Buffer.alloc(2048), 0, 2048, 0);
    await fd.close();
    const m = buffer.slice(0, bytesRead).toString("utf8").match(/^#\s+(.+)$/m);
    return m ? m[1].replace(/[*_`]/g, "").trim() : null;
  } catch { return null; }
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
const account = await makeAccount({ dir: DATA, contributor: process.env.CODEX_CONTRIBUTOR === "1", emit: (type, data) => { broadcast(type, data); if (type === "account") { try { store.reset(); } catch {} } } });
// Sign in with OpenRouter: free AI for anyone without Claude on their computer (src/openrouter-account.js).
const openrouter = await makeOpenRouterAccount({ dir: DATA, emit: (type, data) => broadcast(type, data) });
// Comments: in the Discord forum when signed in, in docs/.notes/ when not (src/comment-store.js). On
// the owner's computer (with the helper's agent key) the forum's comments are copied into docs/.notes/.
const store = makeCommentStore({
  repo: REPO, local: localNotes, account, emit: (type, data) => broadcast(type, data),
  titleOf: (p) => titleOf(p), watching: () => listeners.size > 0, exportCopy: !!agentKey(HERE),
});
// "Editing <page> in the Codex" on the person's Discord profile, through their Discord app.
const presence = makePresence({ clientId: process.env.CODEX_DISCORD_APP || "1567232969637109922" });
const runner = makeRunner({ repo: REPO, store, emit: (type, data) => broadcast(type, data) });
runner.check().then((a) => console.log(a.ok ? `Agents: ${a.version}` : `Agents: off (${a.error})`));
const assistant = makeAssistant({
  repo: REPO, dir: path.join(DATA, "conversations"), store, emit: (type, data) => broadcast(type, data),
  pages: allDocs,
  read: async (rel) => { try { return (await readText(path.join(REPO, rel))).replace(/\r\n/g, "\n"); } catch { return null; } },
  // Undo writes a page back as it was (keeping the file's line endings), or deletes one the AI made.
  write: async (rel, text) => {
    const abs = safe(rel);
    if (!abs || !isDoc(rel)) return;
    if (text === null) { await fsp.rm(abs, { force: true }); return; }
    let eol = "\n";
    try { if ((await fsp.readFile(abs, "utf8")).includes("\r\n")) eol = "\r\n"; } catch {}
    const tmp = abs + ".codex-tmp";
    await fsp.writeFile(tmp, eol === "\r\n" ? text.replace(/\n/g, "\r\n") : text, "utf8");
    await fsp.rename(tmp, abs);
  },
  // The AI's changes are sent like the Codex's own saves, and their commit says "with AI".
  changed: (rels) => { for (const r of rels) syncer.saved(r, { ai: true }); },
  openrouterKey: () => openrouter.key(),
});

// The link index over every page, rebuilt on demand after any page changes.
let index = null;
async function linkIndex() {
  if (index) return index;
  const files = await allDocs();
  const pages = await Promise.all(files.map(async (p) => ({ path: p, text: (await readText(path.join(REPO, p))).replace(/\r\n/g, "\n") })));
  index = buildIndex(pages);
  return index;
}

// Spelling: British English, the docs' own repeated words, and words.txt beside this tool.
const speller = makeSpell({ wordsFile: process.env.CODEX_WORDS || path.join(HERE, "words.txt"), texts: async () => (await linkIndex()).texts });

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

// ----------------------------------------------------------------------------- live changes

const listeners = new Set();
function broadcast(type, data) {
  const msg = `event: ${type}\ndata: ${JSON.stringify(data)}\n\n`;
  for (const res of listeners) res.write(msg);
}
const recentWrites = new Map(); // rel -> { hash, until }: our own saves, so they don't echo back as changes
let pending = new Set(), timer = null;
function changed(rel) {
  index = null;
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
for (const r of ROOTS) {
  try {
    fs.watch(path.join(REPO, r), { recursive: true }, (_, name) => {
      if (!name) return;
      const rel = r + "/" + name.toString().split(path.sep).join("/");
      if (rel.endsWith(".md")) changed(rel);
      else if (rel.endsWith(".json") && rel.startsWith("docs/.notes/")) notesChanged(rel);
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
    const titles = await Promise.all(files.map(titleOf));
    return { files: files.map((p, i) => ({ path: p, title: titles[i], git: status[p] || null, notes: counts[p]?.total || 0, answered: counts[p]?.answered || 0 })) };
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
    const text = eol === "\r\n" ? content.replace(/\n/g, "\r\n") : content;
    const tmp = abs + ".codex-tmp";
    await fsp.writeFile(tmp, text, "utf8");
    await fsp.rename(tmp, abs);
    const h = hash(text);
    recentWrites.set(relOf(abs), { hash: h, until: Date.now() + 3000 });
    syncer.saved(relOf(abs));
    return { ok: true, hash: h };
  },

  async "POST /api/new"(url, req) {
    if (!account.canEdit()) return [403, { error: "Sign in with Discord (with the Codex role) to make pages." }];
    const { path: rel, template, title } = await body(req);
    const abs = safe(rel);
    if (!abs || !isDoc(rel)) return [400, { error: "New pages go in a folder under docs/." }];
    if (fs.existsSync(abs)) return [409, { error: "A page with that name already exists in that folder." }];
    let text = "# " + (String(title || "").trim() || path.basename(rel, ".md").replace(/[-_]/g, " ").replace(/^./, (c) => c.toUpperCase())) + "\n";
    if (template) {
      const t = safe(template);
      if (!t || !template.endsWith(".md")) return [400, { error: "bad template" }];
      text = await readText(t);
    }
    await fsp.mkdir(path.dirname(abs), { recursive: true });
    await fsp.writeFile(abs, text, "utf8");
    recentWrites.set(relOf(abs), { hash: hash(text), until: Date.now() + 3000 });
    syncer.saved(relOf(abs));
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
    const { page = null, kind, text, anchor, run } = await body(req);
    if (page && !(safe(page) && page.endsWith(".md"))) return [400, { error: "notes go on pages" }];
    if (!text || !String(text).trim()) return [400, { error: "an empty note" }];
    const made = await store.create(page, { kind, text, anchor });
    if (run && (made.note.kind === "ask" || made.note.kind === "task")) {
      const r = await runner.start(made.note.id);
      if (r.error) made.runError = r.error;
    }
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
    const { id, text, context } = await body(req);
    const r = await assistant.send({ id, text, context });
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

  async "POST /api/presence"(url, req) {
    const { page } = await body(req);
    presence.set(page && safe(page) ? { page, title: (await titleOf(page)) || page.split("/").pop() } : null);
    return { ok: true };
  },

  // A pasted image (a file, or a Discord image link, which expires within a day): saved under
  // docs/images/pasted/, and its path relative to the page given back for the editor to insert.
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
    const rel = `docs/images/pasted/${new Date().toISOString().slice(0, 10)}-${stem}-${crypto.randomBytes(2).toString("hex")}.${ext}`;
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
  async "POST /api/account/signin"(url, req) { return { url: account.signInUrl("http://" + req.headers.host) }; },
  async "POST /api/account/signout"() { return account.signOut(); },

  async "GET /api/sync"() { return syncer.refresh(); },
  async "POST /api/sync/resolve"(url, req) { const { keep } = await body(req); return syncer.resolve(keep); },
  async "POST /api/sync"(url) { return syncer.sync({ everything: url.searchParams.get("all") === "1", why: url.searchParams.get("why") || "button" }); },

  async "GET /api/agent"() { return { ...runner.status(), owner: await ownerName() }; },

  async "GET /api/links"(url) {
    const page = url.searchParams.get("page");
    if (!page) return [400, { error: "which page?" }];
    return linksFor(await linkIndex(), page);
  },

  async "GET /api/board"() {
    const [idx, status, counts] = await Promise.all([linkIndex(), gitStatus(), noteCounts()]);
    const pages = [];
    for (const [p, text] of idx.texts) {
      if (!p.startsWith("docs/gdd/") || p.startsWith("docs/gdd/templates/")) continue;
      let mtime = null;
      try { mtime = (await fsp.stat(path.join(REPO, p))).mtimeMs; } catch {}
      pages.push({ path: p, title: idx.titles.get(p), ...pageState(text), git: status[p] || null, notes: counts[p] || null, mtime });
    }
    pages.sort((a, b) => a.path.localeCompare(b.path, "en", { numeric: true }));
    return { pages };
  },

  async "GET /api/search"(url) {
    const q = (url.searchParams.get("q") || "").trim();
    if (q.length < 2) return { results: [] };
    const needle = q.toLowerCase();
    const results = [];
    let total = 0;
    for (const file of await allDocs()) {
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
};

async function noteApi(req, id, action) {
  const m = req.method;
  const missing = [404, { error: "no such note" }];
  if (!action && m === "GET") return (await store.find(id)) || missing;
  if (!action && m === "PATCH") return (await store.update(id, await body(req))) || missing;
  if (!action && m === "DELETE") { runner.stop(id); return (await store.remove(id)) ? { ok: true } : missing; }
  if (action === "reply" && m === "POST") {
    const { text, run, status } = await body(req);
    if (!text || !String(text).trim()) return [400, { error: "an empty reply" }];
    const done = await store.reply(id, { by: "owner", text, status });
    if (!done) return missing;
    if (run) { const r = await runner.start(id); if (r.error) done.runError = r.error; }
    return done;
  }
  if (action === "move" && m === "POST") {
    const { page = null } = await body(req);
    if (page && !(safe(page) && page.endsWith(".md"))) return [400, { error: "not a page" }];
    return (await store.move(id, page)) || missing;
  }
  if (action === "run" && m === "POST") { const r = await runner.start(id); return r.error ? [409, r] : r; }
  if (action === "stop" && m === "POST") return { ok: runner.stop(id) };
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
      return res.end(`<!doctype html><meta charset="utf-8"><title>R'th Codex</title><body style="font:16px system-ui;background:#0c0b09;color:#ddd3bf;display:grid;place-items:center;height:90vh;text-align:center"><div><p>${String(r.message).replace(/[<&]/g, "")}</p><p style="color:#a89c86">You can close this tab and go back to the Codex.</p></div>`);
    }
    const noteRoute = url.pathname.match(/^\/api\/notes\/(n-[\w-]+)(?:\/(reply|move|run|stop))?$/);
    if (noteRoute) {
      const result = await noteApi(req, noteRoute[1], noteRoute[2] || null);
      return Array.isArray(result) ? send(res, result[0], result[1]) : send(res, 200, result);
    }
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
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, "127.0.0.1", () => {
      const actual = server.address().port;
      console.log(`R'th Codex: http://127.0.0.1:${actual}/  (docs from ${REPO})`);
      resolve(actual);
    });
  });
}

// Run on its own (npm start, the browser version): start straight away. The desktop app
// (electron/main.js) imports this file and starts it itself.
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) startServer();
