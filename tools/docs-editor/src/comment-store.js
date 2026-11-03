// Where the Codex's comments are: in the Discord forum once you're signed in (through the helper,
// src/helper-comments.js), in docs/.notes/ files when you're not (src/notes-store.js, as before). The
// engine and the agents' runner use this one object either way; it has the local store's calls.
//
// Signed in, it keeps a copy of every comment and asks the helper for what changed every 10 seconds
// while a Codex page is open (every minute otherwise), telling the page when a page's comments change.
// On the owner's computer (the one with the helper's agent key) it also writes that copy into
// docs/.notes/, so the repository keeps the comments' history and agents can read them offline.

import fs from "node:fs";
import path from "node:path";
import { makeHelperComments } from "./helper-comments.js";

const FAST_MS = 10 * 1000, SLOW_MS = 60 * 1000, FULL_MS = 5 * 60 * 1000;

export function makeCommentStore({ repo, local, account, emit, titleOf, watching = () => true, exportCopy = false }) {
  const remote = makeHelperComments({ auth: async () => account.authHeader() });
  const cache = new Map();   // id -> { page, note }
  let until = "", loaded = null, lastFull = 0, timer = null;

  const online = () => !!account.view().signedIn;

  /** Ask the helper what changed (everything, now and then, so deletions elsewhere are seen too). */
  async function refresh({ full = false } = {}) {
    if (!online()) return;
    const everything = full || !until || Date.now() - lastFull > FULL_MS;
    const r = await remote.changes(everything ? "" : until);
    const pages = new Set();
    if (everything) {
      const seen = new Set(r.comments.map((x) => x.note.id));
      for (const [id, x] of cache) if (!seen.has(id)) { cache.delete(id); pages.add(x.page); }
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
    if (online()) ready().then(schedule, schedule);
    emit("notes", { page: null, all: true });
  }

  const put = (x) => { if (x?.note) { const before = cache.get(x.note.id); cache.set(x.note.id, x); if (before && before.page !== x.page) changed(before.page); changed(x.page); } return x; };

  if (online()) ready().then(schedule, schedule);

  return {
    // The local store's file helpers (the file watcher uses them for docs/.notes/).
    root: local.root, fileFor: local.fileFor, pageFor: local.pageFor, lastWrites: local.lastWrites,
    reset,
    /** Someone changed a comment (the room said so): ask the helper now, not at the next look. */
    poke() { if (online()) refresh().catch(() => {}); },
    get where() { return online() ? "discord" : "local"; },

    async load(page) {
      if (!online()) return local.load(page);
      await ready();
      return [...cache.values()].filter((x) => (x.page ?? null) === (page || null)).map((x) => x.note);
    },
    async all() {
      if (!online()) return local.all();
      await ready();
      return [...cache.values()];
    },
    async find(id) {
      if (!online()) return local.find(id);
      await ready();
      return cache.get(id) || null;
    },
    async create(page, fields) {
      if (!online()) return local.create(page, fields);
      return put(await remote.create(page, { ...fields, title: page ? await titleOf(page) : null }));
    },
    async update(id, patch) {
      if (!online()) return local.update(id, patch);
      return put(await remote.update(id, patch));
    },
    async reply(id, entry) {
      if (!online()) return local.reply(id, entry);
      return put(await remote.reply(id, entry));
    },
    async remove(id) {
      if (!online()) return local.remove(id);
      const x = cache.get(id);
      const ok = await remote.remove(id);
      if (ok) { cache.delete(id); changed(x?.page ?? null); }
      return ok;
    },
    async move(id, page) {
      if (!online()) return local.move(id, page);
      return put(await remote.move(id, page));
    },
    async orphans() {
      if (!online()) return local.orphans();
      await ready();
      return [...cache.values()].filter((x) => x.page && !fs.existsSync(path.join(repo, x.page)));
    },
  };
}
