// @R'th AI in a comment thread (PLAN.md, R'th AI's tools, step 7). Someone tags R'th AI in a post in
// #codex-comments (a real mention of the bot, or "@R'th AI" written out), in Discord or from the Codex:
// exactly one open Codex that has Claude and has said it will answer (AI setup) gets the question and
// replies in the thread as R'th AI. With none open, R'th AI says once that it's offline, and the first
// such Codex to open answers it then, tagging whoever asked.
//
// The helper keeps no connection to Discord, so tags are noticed when it reads the forum: whenever a
// Codex asks for comments, and on the 5-minute timer (so the offline reply comes even with no Codex
// open). Each tag is a row in `mentions` (schema.sql), inserted once, whatever reads it again; a Codex
// claims it before answering (one UPDATE that only one can win), for a few minutes it renews while
// working; a claim that runs out goes back to waiting and is offered again.
//
//   POST /mentions/claim  { id, conn }   → { ok, thread, asker: { id, name } } or 409
//   POST /mentions/renew  { id, conn }
//   POST /mentions/done   { id, conn }

import { aiSays } from "./comments.js";

const LEASE_MS = 3 * 60 * 1000;
export const OFFLINE = "I'm offline right now: no Codex with Claude is open. I'll answer here as soon as one opens.";

/** Does a message tag R'th AI? A mention of the bot, or the name written with an @. */
export function tagsAi(env, text) {
  const s = String(text || "");
  if (env.DISCORD_APP_ID && new RegExp(`<@!?${env.DISCORD_APP_ID}>`).test(s)) return true;
  return /(^|[^\w])@\s?(R['’]?th\s?AI|RthAI)\b/i.test(s);
}

/** A tag seen for the first time: kept, and offered to a Codex (or answered "offline"). */
export async function noticed(env, ctx, { msgId, thread, asker, at }) {
  const since = await sinceWhen(env);
  if (at && at < since) return false; // from before this was switched on: never answered out of the blue
  const r = await env.DB.prepare("INSERT INTO mentions (msg_id, thread, asker_id, asker_name, status, at) VALUES (?, ?, ?, ?, 'waiting', ?) ON CONFLICT(msg_id) DO NOTHING")
    .bind(msgId, thread, asker?.id || null, asker?.name || null, at || new Date().toISOString()).run();
  if (!r.meta?.changes) return false;
  const job = route(env, msgId);
  if (ctx?.waitUntil) ctx.waitUntil(job.catch((e) => console.error("mention", e.message))); else await job;
  return true;
}

/** The first time tags were looked for: older messages are never answered. */
async function sinceWhen(env) {
  const row = await env.DB.prepare("SELECT v FROM config WHERE k = 'mentions_since'").first();
  if (row?.v) return row.v;
  const v = new Date().toISOString();
  await env.DB.prepare("INSERT INTO config (k, v) VALUES ('mentions_since', ?) ON CONFLICT(k) DO NOTHING").bind(v).run();
  return v;
}

/** Offer a waiting tag to one Codex that answers; with none, say once that R'th AI is offline. */
export async function route(env, msgId) {
  const m = await env.DB.prepare("SELECT * FROM mentions WHERE msg_id = ?").bind(msgId).first();
  if (!m || m.status !== "waiting") return;
  const r = await env.LIVE.get(env.LIVE.idFromName("codex")).fetch("https://room/live/ask", { method: "POST", body: JSON.stringify({ type: "ask", id: m.msg_id, thread: m.thread }) });
  const sent = (await r.json().catch(() => ({}))).sent;
  if (!sent && !m.told) {
    await env.DB.prepare("UPDATE mentions SET told = 1 WHERE msg_id = ?").bind(msgId).run();
    await aiSays(env, m.thread, OFFLINE);
  }
}

/** The timer: claims that ran out go back to waiting and are offered again; waiting ones too. */
export async function sweep(env) {
  const t = Date.now();
  await env.DB.prepare("UPDATE mentions SET status = 'waiting', conn = NULL WHERE status = 'claimed' AND until < ?").bind(t).run();
  const waiting = (await env.DB.prepare("SELECT msg_id FROM mentions WHERE status = 'waiting' ORDER BY at LIMIT 10").all()).results;
  for (const w of waiting) await route(env, w.msg_id);
}

/** The waiting tags, for a Codex that has just said it answers (the room hands them to it). */
export async function waiting(env) {
  return (await env.DB.prepare("SELECT msg_id AS id, thread FROM mentions WHERE status = 'waiting' ORDER BY at LIMIT 10").all()).results;
}

/** The Codex's side: claim, renew, done. Only one claim of a tag can win. */
export async function mentions(req, env, url) {
  if (req.method !== "POST") return null;
  const b = await req.json().catch(() => ({}));
  const id = String(b.id || ""), conn = String(b.conn || "").slice(0, 80);
  if (!id || !conn) return { status: 400, error: "Which tag, and which Codex?" };
  const t = Date.now();
  if (url.pathname === "/mentions/claim") {
    const r = await env.DB.prepare("UPDATE mentions SET status = 'claimed', conn = ?, until = ? WHERE msg_id = ? AND (status = 'waiting' OR (status = 'claimed' AND until < ?))")
      .bind(conn, t + LEASE_MS, id, t).run();
    if (!r.meta?.changes) return { status: 409, error: "Someone else is answering it." };
    const m = await env.DB.prepare("SELECT * FROM mentions WHERE msg_id = ?").bind(id).first();
    return { ok: true, thread: m.thread, asker: { id: m.asker_id, name: m.asker_name } };
  }
  if (url.pathname === "/mentions/renew") {
    const r = await env.DB.prepare("UPDATE mentions SET until = ? WHERE msg_id = ? AND status = 'claimed' AND conn = ?").bind(t + LEASE_MS, id, conn).run();
    return r.meta?.changes ? { ok: true } : { status: 409, error: "Not yours any more." };
  }
  if (url.pathname === "/mentions/done") {
    await env.DB.prepare("UPDATE mentions SET status = 'answered', until = NULL WHERE msg_id = ? AND conn = ?").bind(id, conn).run();
    return { ok: true };
  }
  return null;
}
