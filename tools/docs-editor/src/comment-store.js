// Where the Codex's comments are: in the Discord forum once you're signed in (through the helper,
// src/helper-comments.js), in docs/.notes/ files when you're not (src/notes-store.js, as before). The
// engine and the agents' runner use this one object either way; it has the local store's calls.
//
// Signed in, it keeps a copy of every comment and asks the helper for what changed every 10 seconds
// while a Codex page is open (every minute otherwise), telling the page when a page's comments change.
// On the owner's computer (the one with the helper's agent key) it also writes that copy into
// docs/.notes/, so the repository keeps the comments' history and agents can read them offline.
//
// The outbox: when the helper or Discord can't be reached (no connection, their servers failing), a
// comment, reply, edit, move or deletion isn't lost or refused: it waits in outboxFile (on this computer,
// so it survives a restart) and is sent in order at the next look. Until then it shows as if done,
// marked `pending` (the card says "Not posted yet"): the copy from Discord stays as Discord has it, and
// what's waiting is laid over it whenever comments are read, so a refresh never loses or doubles it. A
// new comment has a temporary id ("pending-...") until Discord gives it its own; what's queued for it
// follows. Something Discord refuses outright (not a passing failure) is dropped and said once.

import fs from "node:fs";
import path from "node:path";
import { makeHelperComments } from "./helper-comments.js";

const FAST_MS = 10 * 1000, SLOW_MS = 60 * 1000, FULL_MS = 5 * 60 * 1000;

/** Worth trying again later: no answer at all, or the helper/Discord failing (5xx), or too busy (429). */
export const passing = (e) => !e?.status || e.status >= 500 || e.status === 429;

export function makeCommentStore({ repo, local, account, emit, titleOf, watching = () => true, exportCopy = false, outboxFile = null, remote: remoteIn = null }) {
  const remote = remoteIn || makeHelperComments({ auth: async () => account.authHeader() });
  const cache = new Map();   // id -> { page, note }
  let until = "", loaded = null, lastFull = 0, timer = null;

  const online = () => !!account.view().signedIn;

  // ------------------------------------------------------------------- the outbox
  let outbox = [];
  try { outbox = JSON.parse(fs.readFileSync(outboxFile, "utf8")); if (!Array.isArray(outbox)) outbox = []; } catch { /* nothing waiting */ }
  const saveOutbox = () => { if (!outboxFile) return; try { fs.mkdirSync(path.dirname(outboxFile), { recursive: true }); fs.writeFileSync(outboxFile, JSON.stringify(outbox, null, 1)); } catch (e) { console.warn("Codex: keeping unsent comments failed:", e.message); } };
  const me = () => account.view().user || null;
  const stamp = () => new Date().toISOString();
  const tempId = () => "pending-" + Date.now().toString(36) + Math.random().toString(36).slice(2, 7);
  const isTemp = (id) => typeof id === "string" && id.startsWith("pending-");

  /** Queue a change and say so; returns what the caller would have got, as it will be. */
  function queue(op) {
    outbox.push({ ...op, at: stamp() });
    saveOutbox();
    const x = viewOf(op.op === "create" ? op.temp : op.id);
    changed(x?.page ?? op.page ?? null);
    if (op.op === "move") changed(op.from ?? null);
    return x;
  }
  /** A comment as it will be once what's waiting is sent: Discord's copy (or a new one) with the outbox on top. */
  function viewOf(id) {
    let x = null;
    const base = cache.get(id);
    if (base) x = { page: base.page, note: { ...base.note, thread: [...base.note.thread] } };
    for (const op of outbox) {
      if (op.op === "create" && op.temp === id) {
        x = { page: op.page, note: { id, kind: op.fields.kind || "note", status: "open", created: op.at, updated: op.at, anchor: op.fields.anchor || {},
          thread: [{ by: op.fields.by === "agent" ? "agent" : "owner", who: op.who, at: op.at, text: String(op.fields.text || "").trim(), pending: true }], tags: [], pending: true } };
      }
      if (!x || op.id !== id) continue;
      if (op.op === "reply") {
        x.note.thread.push({ by: op.entry.by || "owner", who: op.who, at: op.at, text: String(op.entry.text || "").trim(), pending: true });
        if (op.entry.status) x.note.status = op.entry.status;
      } else if (op.op === "update") {
        const { text, ...rest } = op.patch || {};
        Object.assign(x.note, rest);
        if (text != null && x.note.thread[0]) x.note.thread[0] = { ...x.note.thread[0], text, pending: true };
        x.note.pending = true;
      } else if (op.op === "move") { x.page = op.page; x.note.pending = true; }
      else if (op.op === "remove") return null;
    }
    return x;
  }
  /** Every comment as it will be: Discord's, the new ones waiting, the outbox laid over all of them. */
  function everything() {
    const ids = [...cache.keys(), ...outbox.filter((o) => o.op === "create").map((o) => o.temp)];
    return [...new Set(ids)].map(viewOf).filter(Boolean);
  }

  let flushing = false;
  /** Send what's waiting, in order; stop at the first passing failure (tried again at the next look). */
  async function flush() {
    if (flushing || !outbox.length || !online()) return;
    flushing = true;
    try {
      while (outbox.length) {
        const op = outbox[0];
        try {
          let got;
          if (op.op === "create") got = await remote.create(op.page, { ...op.fields, title: op.page ? await titleOf(op.page) : null });
          else if (isTemp(op.id)) { outbox.shift(); saveOutbox(); continue; } // its comment never got made
          else if (op.op === "reply") got = await remote.reply(op.id, op.entry);
          else if (op.op === "update") got = await remote.update(op.id, op.patch);
          else if (op.op === "move") got = await remote.move(op.id, op.page);
          else if (op.op === "remove") { if (await remote.remove(op.id)) { const before = cache.get(op.id); cache.delete(op.id); changed(before?.page ?? null); } }
          outbox.shift();
          if (op.op === "create" && got?.note) {
            // Discord's own id from now on, for this comment and everything queued for it.
            for (const o of outbox) if (o.id === op.temp) o.id = got.note.id;
            changed(op.page ?? null);
          }
          saveOutbox();
          if (got?.note) put(got);
        } catch (e) {
          if (passing(e)) break; // later
          outbox.shift(); saveOutbox(); // refused for good: said once, not tried for ever
          emit("notes-failed", { what: op.op, error: e.message });
          changed(op.page ?? null);
        }
      }
    } finally { flushing = false; }
  }
  /** Do it now if Discord answers; queue it if it can't be reached; refused outright: the error, as before. */
  async function sendOrQueue(op, call) {
    if (outbox.length) return queue(op); // keep the order: nothing overtakes what's already waiting
    try { return await call(); } catch (e) { if (passing(e)) return queue(op); throw e; }
  }

  /** Ask the helper what changed (everything, now and then, so deletions elsewhere are seen too). */
  async function refresh({ full = false } = {}) {
    if (!online()) return;
    const everything = full || !until || Date.now() - lastFull > FULL_MS;
    const r = await remote.changes(everything ? "" : until);
    const pages = new Set();
    if (everything) {
      const seen = new Set(r.comments.map((x) => x.note.id));
      for (const [id, x] of cache) if (!seen.has(id)) { cache.delete(id); pages.add(x.page); } // (waiting new ones aren't in the cache)
      lastFull = Date.now();
    }
    for (const x of r.comments) {
      const before = cache.get(x.note.id);
      if (before && before.note.updated === x.note.updated && before.page === x.page && before.note.status === x.note.status) continue;
      if (before && before.page !== x.page) pages.add(before.page);
      cache.set(x.note.id, x);
      pages.add(x.page);
    }
    if (r.until) until = r.until;
    for (const p of pages) changed(p);
  }

  function ready() {
    if (!online()) return Promise.resolve();
    if (!loaded) loaded = refresh({ full: true }).catch((e) => { loaded = null; throw e; });
    return loaded;
  }

  // Asking again and again: quickly while a page is open, slowly otherwise.
  function schedule() {
    clearTimeout(timer);
    if (!online()) return;
    timer = setTimeout(async () => {
      try { await flush(); } catch (e) { console.warn("Codex: sending waiting comments failed:", e.message); }
      try { await refresh(); } catch (e) { if (process.env.CODEX_DEBUG) console.log("comments:", e.message); }
      schedule();
    }, watching() ? FAST_MS : SLOW_MS);
  }

  /** A page's comments changed: tell the page, and keep the copy in docs/.notes/ (owner's computer). */
  function changed(page) {
    emit("notes", { page: page ?? null });
    if (exportCopy) local.save(page ?? null, [...cache.values()].filter((x) => (x.page ?? null) === (page ?? null)).map((x) => x.note)).catch(() => {});
  }

  /** Signed in or out: start again from the right place. */
  function reset() {
    cache.clear(); until = ""; loaded = null; lastFull = 0;
    clearTimeout(timer);
    if (online()) ready().then(() => flush()).then(schedule, schedule);
    emit("notes", { page: null, all: true });
  }

  const put = (x) => { if (x?.note) { const before = cache.get(x.note.id); cache.set(x.note.id, x); if (before && before.page !== x.page) changed(before.page); changed(x.page); } return x; };

  if (online()) ready().then(() => flush()).then(schedule, schedule);

  return {
    // The local store's file helpers (the file watcher uses them for docs/.notes/).
    root: local.root, fileFor: local.fileFor, pageFor: local.pageFor, lastWrites: local.lastWrites,
    reset,
    /** Someone changed a comment (the room said so): ask the helper now, not at the next look. */
    poke() { if (online()) refresh().catch(() => {}); },
    /** The latest from the helper, waited for (answering @R'th AI reads the thread as it is now). */
    async fresh() { if (online()) await refresh().catch(() => {}); },
    get where() { return online() ? "discord" : "local"; },
    /** How many changes wait to be sent (the outbox); flush() sends them now if it can. */
    get waiting() { return outbox.length; },
    flush,

    async load(page) {
      if (!online()) return local.load(page);
      await ready().catch(() => {}); // Discord out of reach: what's known, and what's waiting
      return everything().filter((x) => (x.page ?? null) === (page || null)).map((x) => x.note);
    },
    async all() {
      if (!online()) return local.all();
      await ready().catch(() => {});
      return everything();
    },
    async find(id) {
      if (!online()) return local.find(id);
      await ready().catch(() => {});
      return viewOf(id);
    },
    async create(page, fields) {
      if (!online()) return local.create(page, fields);
      const op = { op: "create", temp: tempId(), page, fields, who: me() };
      return sendOrQueue(op, async () => put(await remote.create(page, { ...fields, title: page ? await titleOf(page) : null })));
    },
    async update(id, patch) {
      if (!online()) return local.update(id, patch);
      if (!viewOf(id)) return null;
      return sendOrQueue({ op: "update", id, patch }, async () => put(await remote.update(id, patch)));
    },
    async reply(id, entry) {
      if (!online()) return local.reply(id, entry);
      if (!viewOf(id)) return null;
      return sendOrQueue({ op: "reply", id, entry, who: me() }, async () => put(await remote.reply(id, entry)));
    },
    async remove(id) {
      if (!online()) return local.remove(id);
      const x = viewOf(id);
      if (!x) return false;
      if (isTemp(id)) { // never sent: it and everything queued for it just go
        outbox = outbox.filter((o) => o.temp !== id && o.id !== id); saveOutbox(); changed(x.page ?? null); return true;
      }
      const r = await sendOrQueue({ op: "remove", id, page: x.page }, async () => {
        const ok = await remote.remove(id);
        if (ok) { cache.delete(id); changed(x.page ?? null); }
        return ok;
      });
      return r === true || r === false ? r : true; // queued: gone as far as anyone here can see
    },
    async move(id, page) {
      if (!online()) return local.move(id, page);
      const x = viewOf(id);
      if (!x) return null;
      return sendOrQueue({ op: "move", id, page, from: x.page }, async () => put(await remote.move(id, page)));
    },
    async orphans() {
      if (!online()) return local.orphans();
      await ready().catch(() => {});
      return everything().filter((x) => x.page && !fs.existsSync(path.join(repo, x.page)));
    },
  };
}
