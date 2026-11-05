// Answering @R'th AI in a Discord comment thread (helper/src/mentions.js), on a Codex that has Claude and
// has said in AI setup that it answers. The helper's room hands this Codex a tag; it claims it (only one
// Codex's claim wins), renews the claim while it works, reads the thread, asks Claude (Sonnet, medium
// effort, read-only, with the Codex's tools), and replies in the thread as R'th AI, tagging whoever asked.

import crypto from "node:crypto";
import { HELPER } from "./discord-account.js";
import { VERSION_HEADERS } from "./codex-version.js";

/** auth(): the Authorization header (the signed-in session). store: the comments. answer({ note, page,
 *  asker }) → { text } or { error } (src/assistant.js). enabled(): this Codex answers. */
export function makeDiscordAnswers({ auth, store, answer, enabled = () => false, helper = HELPER }) {
  const conn = "codex-" + crypto.randomBytes(6).toString("hex"); // this Codex, for its claims
  const busy = new Set();

  async function call(route, body) {
    const a = await auth();
    if (!a) throw new Error("Not signed in.");
    const r = await fetch(helper + route, { method: "POST", headers: { Authorization: a, "Content-Type": "application/json", ...VERSION_HEADERS }, body: JSON.stringify(body) });
    const j = await r.json().catch(() => ({}));
    return r.ok ? j : { error: j.error || `The helper said ${r.status}`, status: r.status };
  }

  /** The room asks: { id: the message that tagged R'th AI, thread: its post }. */
  async function handle({ id, thread }) {
    if (!enabled() || !id || !thread || busy.has(id)) return { skipped: true };
    busy.add(id);
    try {
      const c = await call("/mentions/claim", { id, conn });
      if (c.error) return { skipped: true, why: c.error }; // another Codex is on it
      const renew = setInterval(() => call("/mentions/renew", { id, conn }).catch(() => {}), 60 * 1000);
      try {
        await store.fresh?.();
        const hit = await store.find(thread);
        if (!hit) return { error: "That post isn't there any more." };
        const r = await answer({ note: hit.note, page: hit.page ?? null, asker: c.asker || null });
        const text = r.error ? "I couldn't answer that just now (" + r.error + "). Ask me again in a while." : r.text;
        if (text?.trim()) await store.reply(thread, { by: "agent", text: text.trim(), ping: c.asker?.id || null });
      } finally { clearInterval(renew); }
      await call("/mentions/done", { id, conn });
      return { ok: true };
    } catch (e) {
      console.error("Answering @R'th AI:", e.message);
      return { error: e.message };
    } finally { busy.delete(id); }
  }

  return { handle, conn };
}
