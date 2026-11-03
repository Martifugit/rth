// Notes on the docs, kept beside them and never in them.
//
// One JSON file per page, mirroring its path under docs/.notes/:
//   docs/gdd/x.md      -> docs/.notes/gdd/x.md.json
//   README.md (root)   -> docs/.notes/_root/README.md.json
//   no page (scratch)  -> docs/.notes/_scratch.json
// Keys are written in a fixed order and notes in creation order, so git diffs stay small. A page
// with no notes left has no file. Used by server.js and the notes.js CLI (agents use the CLI).

import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";

export const KINDS = ["note", "ask", "task"];
export const STATUSES = ["open", "answered", "done"];

export function makeStore(repo, { onWrite = () => {} } = {}) {
  const ROOT = path.join(repo, "docs", ".notes");
  const SCRATCH = "_scratch.json";

  /** The notes file (repository-relative, forward slashes) for a page, or for scratch (null). */
  function fileFor(page) {
    if (!page) return "docs/.notes/" + SCRATCH;
    if (page.startsWith("docs/")) return "docs/.notes/" + page.slice(5) + ".json";
    return "docs/.notes/_root/" + page + ".json";
  }
  /** The page a notes file is for (null: scratch), or undefined if it isn't a notes file. */
  function pageFor(file) {
    file = file.split(path.sep).join("/");
    if (!file.startsWith("docs/.notes/") || !file.endsWith(".json")) return undefined;
    const rest = file.slice("docs/.notes/".length);
    if (rest === SCRATCH) return null;
    if (rest.startsWith("_root/")) return rest.slice(6, -5);
    return "docs/" + rest.slice(0, -5);
  }

  const abs = (rel) => path.join(repo, ...rel.split("/"));
  const recent = new Map(); // file -> text we wrote, so the watcher can tell our writes from others'
  const lastWrites = () => recent;

  async function load(page) {
    try {
      const data = JSON.parse(await fsp.readFile(abs(fileFor(page)), "utf8"));
      return Array.isArray(data.notes) ? data.notes : [];
    } catch { return []; }
  }

  /** opts.quiet: upkeep by the Codex itself (a status or position fix), not a person's change. */
  async function save(page, notes, opts = {}) {
    const file = fileFor(page), target = abs(file);
    if (!notes.length) {
      try { await fsp.unlink(target); } catch {}
      recent.set(file, null);
      onWrite(file, opts);
      return;
    }
    const text = serialize(page, notes);
    await fsp.mkdir(path.dirname(target), { recursive: true });
    const tmp = target + ".codex-tmp";
    await fsp.writeFile(tmp, text, "utf8");
    await fsp.rename(tmp, target);
    recent.set(file, text);
    onWrite(file, opts);
  }

  async function walk(dir, out) {
    let entries;
    try { entries = await fsp.readdir(dir, { withFileTypes: true }); } catch { return out; }
    for (const e of entries) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) await walk(p, out);
      else if (e.name.endsWith(".json")) out.push(path.relative(repo, p).split(path.sep).join("/"));
    }
    return out;
  }

  /** Every note: [{ page, note }]. */
  async function all() {
    const out = [];
    for (const file of await walk(ROOT, [])) {
      const page = pageFor(file);
      if (page === undefined) continue;
      for (const note of await load(page)) out.push({ page, note });
    }
    return out;
  }

  async function find(id) {
    for (const x of await all()) if (x.note.id === id) return x;
    return null;
  }

  // One change at a time per file, so two quick saves never interleave.
  const locks = new Map();
  function withPage(page, fn, opts = {}) {
    const key = fileFor(page);
    const run = (locks.get(key) || Promise.resolve()).then(async () => {
      const notes = await load(page);
      const result = await fn(notes);
      await save(page, notes, opts);
      return result;
    });
    locks.set(key, run.catch(() => {}));
    return run;
  }

  async function create(page, fields) {
    const now = stamp();
    const note = {
      id: newId(), kind: KINDS.includes(fields.kind) ? fields.kind : "note", status: "open",
      created: now, updated: now,
      anchor: cleanAnchor(fields.anchor),
      thread: [{ by: fields.by === "agent" ? "agent" : "owner", at: now, text: String(fields.text || "").trim() }],
      tags: Array.isArray(fields.tags) ? fields.tags.map(String) : [],
    };
    await withPage(page, (notes) => { notes.push(note); });
    return { page, note };
  }

  async function update(id, patch) {
    const hit = await find(id);
    if (!hit) return null;
    return withPage(hit.page, (notes) => {
      const note = notes.find((n) => n.id === id);
      if (!note) return null;
      if (patch.kind && KINDS.includes(patch.kind)) note.kind = patch.kind;
      if (patch.status && STATUSES.includes(patch.status)) note.status = patch.status;
      if (patch.anchor !== undefined) note.anchor = cleanAnchor(patch.anchor);
      if (Array.isArray(patch.tags)) note.tags = patch.tags.map(String);
      if (typeof patch.text === "string" && note.thread.length) note.thread[0].text = patch.text.trim();
      if (patch.touch !== false) note.updated = stamp();
      return { page: hit.page, note };
    }, { quiet: patch.touch === false });
  }

  /** Add to a note's thread. An agent's reply marks the note answered unless it says otherwise. */
  async function reply(id, entry) {
    const hit = await find(id);
    if (!hit) return null;
    return withPage(hit.page, (notes) => {
      const note = notes.find((n) => n.id === id);
      if (!note) return null;
      const by = entry.by === "agent" ? "agent" : "owner";
      const e = { by, at: stamp(), text: String(entry.text || "").trim() };
      if (entry.run) e.run = entry.run;
      note.thread.push(e);
      note.updated = e.at;
      if (entry.status && STATUSES.includes(entry.status)) note.status = entry.status;
      else if (by === "agent" && note.status === "open") note.status = "answered";
      else if (by === "owner" && note.status === "answered") note.status = "open";
      return { page: hit.page, note };
    });
  }

  async function remove(id) {
    const hit = await find(id);
    if (!hit) return false;
    await withPage(hit.page, (notes) => { const i = notes.findIndex((n) => n.id === id); if (i >= 0) notes.splice(i, 1); });
    return true;
  }

  async function move(id, toPage) {
    const hit = await find(id);
    if (!hit) return null;
    if (hit.page === toPage) return hit;
    await withPage(hit.page, (notes) => { const i = notes.findIndex((n) => n.id === id); if (i >= 0) notes.splice(i, 1); });
    hit.note.updated = stamp();
    await withPage(toPage, (notes) => { notes.push(hit.note); });
    return { page: toPage, note: hit.note };
  }

  /** Notes whose page no longer exists (renamed or deleted). */
  async function orphans() {
    return (await all()).filter((x) => x.page && !fs.existsSync(abs(x.page)));
  }

  return { root: ROOT, fileFor, pageFor, load, save, all, find, create, update, reply, remove, move, orphans, lastWrites };
}

// ----------------------------------------------------------------------------- shape

const ANCHOR_KEYS = ["section", "quote", "prefix", "suffix"];
function cleanAnchor(a) {
  if (!a || typeof a !== "object") return { section: [], quote: "", prefix: "", suffix: "" };
  return {
    section: Array.isArray(a.section) ? a.section.map(String) : [],
    quote: String(a.quote || ""), prefix: String(a.prefix || ""), suffix: String(a.suffix || ""),
  };
}

const NOTE_KEYS = ["id", "kind", "status", "created", "updated", "anchor", "thread", "tags"];
const ENTRY_KEYS = ["by", "at", "text", "run"];
const RUN_KEYS = ["session", "cost", "edits", "error"];
const pick = (obj, keys) => {
  const out = {};
  for (const k of keys) if (obj[k] !== undefined) out[k] = obj[k];
  for (const k of Object.keys(obj).sort()) if (!keys.includes(k) && obj[k] !== undefined) out[k] = obj[k];
  return out;
};

/** The file's text: fixed key order, two-space indent, a newline at the end. */
export function serialize(page, notes) {
  const shaped = notes.map((n) => {
    const o = pick(n, NOTE_KEYS);
    if (o.anchor) o.anchor = pick(o.anchor, ANCHOR_KEYS);
    if (o.thread) o.thread = o.thread.map((e) => {
      const x = pick(e, ENTRY_KEYS);
      if (x.run) x.run = pick(x.run, RUN_KEYS);
      return x;
    });
    return o;
  });
  return JSON.stringify({ page, notes: shaped }, null, 2) + "\n";
}

export const stamp = () => new Date().toISOString().replace(/\.\d+Z$/, "Z");
export function newId() {
  const d = new Date();
  const day = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, "0")}${String(d.getDate()).padStart(2, "0")}`;
  return `n-${day}-${crypto.randomBytes(3).toString("hex")}`;
}
