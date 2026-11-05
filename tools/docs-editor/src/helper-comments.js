// Comments in the Discord forum, through the helper (helper/src/comments.js), with the same calls as
// the local store (src/notes-store.js): all, find, create, update, reply, remove, move. Used by the
// engine when someone is signed in (src/comment-store.js, which keeps a copy), and by the agents' CLI
// (notes.js) on the owner's computer, with the agent key in helper/.dev.vars.

import fs from "node:fs";
import path from "node:path";
import { HELPER } from "./discord-account.js";
import { VERSION_HEADERS, noteAnswer } from "./codex-version.js";

/** `auth`: () => the Authorization header value, or null when it can't ask. */
export function makeHelperComments({ auth }) {
  async function call(route, body) {
    const a = await auth();
    if (!a) throw new Error("Not signed in to Discord, so comments can't be reached.");
    const r = await fetch(HELPER + route, {
      method: body ? "POST" : "GET",
      headers: { Authorization: a, "Content-Type": "application/json", ...VERSION_HEADERS },
      body: body ? JSON.stringify(body) : undefined,
    });
    const j = await r.json().catch(() => ({}));
    noteAnswer(r.status, j); // 426: this Codex is out of date
    if (r.status === 404 && /No such comment/.test(j.error || "")) return null;
    if (!r.ok) { const e = new Error(j.error || `The helper said ${r.status}`); e.status = r.status; throw e; }
    return j;
  }

  /** Comments changed since `since` (ISO; "" for all): { comments: [{ page, note }], until }. */
  const changes = (since = "") => call("/comments?since=" + encodeURIComponent(since));
  const all = async () => (await changes("")).comments;
  const find = async (id) => (await all()).find((x) => x.note.id === id) || null;

  return {
    changes, all, find,
    create: (page, fields) => call("/comments", { page, title: fields.title, kind: fields.kind, text: fields.text, anchor: fields.anchor, tags: fields.tags, by: fields.by }),
    update: (id, patch) => call("/comments/update", { id, ...patch }),
    reply: (id, entry) => call("/comments/reply", { id, text: entry.text, status: entry.status, run: entry.run, by: entry.by, ping: entry.ping || undefined }),
    remove: async (id) => !!(await call("/comments/delete", { id }))?.ok,
    move: (id, page) => call("/comments/update", { id, page }),
  };
}

/** The owner's agent key (helper/.dev.vars beside this tool), or null on anyone else's computer. */
export function agentKey(toolDir) {
  try {
    const m = fs.readFileSync(path.join(toolDir, "helper", ".dev.vars"), "utf8").match(/^AGENT_KEY=(\S+)/m);
    return m ? m[1] : null;
  } catch { return null; }
}
