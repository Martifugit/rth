// R'th AI anywhere in the Discord (PLAN.md, 0.4.1): `/ask` in any channel, and "Ask R'th AI" on a
// message's Apps menu. Discord sends these straight here (the application's Interactions Endpoint URL,
// https://<helper>/interactions), so the helper needs no connection to Discord to see them: it answers at
// once with "R'th AI is thinking…" (a deferred response), keeps the question as a tag in `mentions`
// (kind "channel"), and offers it to one open Codex that answers, as an @R'th AI in a comment post is
// (src/mentions.js). The Codex's answer replaces "thinking…" while Discord still lets it (15 minutes);
// later, or after "offline" was said, it's posted as a reply that tags whoever asked.
//
//   POST /interactions        from Discord, signed (Ed25519, DISCORD_PUBLIC_KEY)
//   POST /admin/commands      registers the two commands in the server (the agent key only)

import { noticed } from "./mentions.js";

const API = "https://discord.com/api/v10";
const TOKEN_MS = 14 * 60 * 1000; // an interaction's token lasts 15 minutes; a little short of it, to be safe
export const COMMANDS = [
  { name: "ask", type: 1, description: "Ask R'th AI about the game's design (it reads the GDD)",
    options: [{ type: 3, name: "question", description: "What you want to know", required: true, max_length: 1500 }] },
  { name: "Ask R'th AI", type: 3 },
];

const hex = (s) => Uint8Array.from(String(s || "").match(/../g) || [], (b) => parseInt(b, 16));

/** Discord's signature on an interaction: Ed25519 over the timestamp and the body. */
export async function verified(env, body, signature, timestamp) {
  if (!env.DISCORD_PUBLIC_KEY || !signature || !timestamp) return false;
  // The standard name, then the one older Workers runtimes knew it by.
  for (const alg of [{ name: "Ed25519" }, { name: "NODE-ED25519", namedCurve: "NODE-ED25519" }]) {
    try {
      const key = await crypto.subtle.importKey("raw", hex(env.DISCORD_PUBLIC_KEY), alg, false, ["verify"]);
      return await crypto.subtle.verify(alg, key, hex(signature), new TextEncoder().encode(timestamp + body));
    } catch {}
  }
  return false;
}

const reply = (data) => Response.json(data);
const privately = (content) => reply({ type: 4, data: { content, flags: 64 } }); // seen only by who asked

/** An interaction from Discord. */
export async function interaction(req, env, ctx) {
  const body = await req.text();
  if (!(await verified(env, body, req.headers.get("x-signature-ed25519"), req.headers.get("x-signature-timestamp")))) return new Response("bad signature", { status: 401 });
  const i = JSON.parse(body);
  if (i.type === 1) return reply({ type: 1 }); // Discord checking the endpoint
  if (i.type !== 2) return privately("R'th AI only answers /ask and “Ask R'th AI”.");
  const user = i.member?.user || i.user || {};
  const asker = { id: user.id || null, name: i.member?.nick || user.global_name || user.username || "Someone" };
  let question = "", ref = null;
  if (i.data?.type === 3) { // "Ask R'th AI" on a message: that message is the question
    const m = i.data.resolved?.messages?.[i.data.target_id];
    const by = m?.member?.nick || m?.author?.global_name || m?.author?.username || "Someone";
    question = m?.content ? `${by} wrote: ${m.content}` : "";
    ref = m?.id || null;
  } else question = String(i.data?.options?.find((o) => o.name === "question")?.value || "");
  if (!question.trim()) return privately("There's no text there for R'th AI to answer.");
  const job = noticed(env, null, { msgId: "i" + i.id, thread: i.channel_id, asker, at: new Date().toISOString(), kind: "channel", token: i.token, question: question.slice(0, 4000), refMsg: ref });
  ctx.waitUntil(job.catch((e) => console.error("ask", e.stack || e)));
  return reply({ type: 5 }); // "R'th AI is thinking…", in the channel, until the answer replaces it
}

const bot = (env) => ({ Authorization: "Bot " + env.DISCORD_BOT_TOKEN, "Content-Type": "application/json" });

/** Text in pieces Discord takes (2000 characters a message), split at paragraphs or lines where it can. */
export function pieces(text, max = 1900) {
  const out = [];
  let rest = String(text || "").trim();
  while (rest.length > max) {
    let at = rest.lastIndexOf("\n\n", max);
    if (at < max / 2) at = rest.lastIndexOf("\n", max);
    if (at < max / 2) at = rest.lastIndexOf(" ", max);
    if (at < 1) at = max;
    out.push(rest.slice(0, at).trim());
    rest = rest.slice(at).trim();
  }
  if (rest) out.push(rest);
  return out;
}

/** "thinking…" replaced by `content` (the token's own message); its message id. */
export async function editThinking(env, token, content) {
  const r = await fetch(`${API}/webhooks/${env.DISCORD_APP_ID}/${token}/messages/@original`, {
    method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ content, allowed_mentions: { parse: [] }, flags: 4 }) });
  if (!r.ok) throw new Error(`Discord refused the edit (${r.status}): ${await r.text()}`);
  return (await r.json()).id;
}

/** A message in a channel from the bot, optionally a reply to a message, tagging `ping` alone. */
export async function botSay(env, channel, content, { replyTo = null, ping = null } = {}) {
  const r = await fetch(`${API}/channels/${channel}/messages`, { method: "POST", headers: bot(env), body: JSON.stringify({
    content: (ping ? `<@${ping}> ` : "") + content, flags: 4,
    allowed_mentions: ping ? { users: [ping], replied_user: false } : { parse: [], replied_user: false },
    ...(replyTo ? { message_reference: { message_id: replyTo, fail_if_not_exists: false } } : {}),
  }) });
  if (!r.ok) throw new Error(`Discord refused the message (${r.status}): ${await r.text()}`);
  return (await r.json()).id;
}

/** Offline: "thinking…" becomes the offline line; the message is kept to reply to later. */
export async function sayOffline(env, m, text) {
  if (!m.token || Date.now() - Date.parse(m.at) > TOKEN_MS) return null;
  return editThinking(env, m.token, text);
}

/** A Codex's answer to a channel tag: in place of "thinking…" while that's still allowed and nobody was
 *  told R'th AI is offline; otherwise a reply that tags whoever asked. Long answers in several messages. */
export async function answerInChannel(env, m, text) {
  const parts = pieces(text);
  if (!parts.length) return;
  const fresh = m.token && !m.told && Date.now() - Date.parse(m.at) < TOKEN_MS;
  if (fresh) {
    await editThinking(env, m.token, parts[0]);
    for (const p of parts.slice(1)) await fetch(`${API}/webhooks/${env.DISCORD_APP_ID}/${m.token}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ content: p, allowed_mentions: { parse: [] }, flags: 4 }) });
    return;
  }
  // The first part replies to the question (or the offline line) and tags whoever asked; the rest follow.
  for (const [k, p] of parts.entries()) await botSay(env, m.thread, p, k ? {} : { replyTo: m.ref_msg || null, ping: m.asker_id || null });
}

/** What a Codex is told about a channel tag: the channel's name and its last messages, for context. */
export async function channelContext(env, channel) {
  const [c, msgs] = await Promise.all([
    fetch(`${API}/channels/${channel}`, { headers: bot(env) }).then((r) => (r.ok ? r.json() : null)).catch(() => null),
    fetch(`${API}/channels/${channel}/messages?limit=15`, { headers: bot(env) }).then((r) => (r.ok ? r.json() : [])).catch(() => []),
  ]);
  const recent = (Array.isArray(msgs) ? msgs : []).reverse().filter((x) => x.content).map((x) => ({ by: x.member?.nick || x.author?.global_name || x.author?.username || "Someone", bot: !!x.author?.bot, text: String(x.content).slice(0, 800) }));
  return { name: c?.name || null, recent };
}

/** Register /ask and "Ask R'th AI" in the server (instant, unlike global commands). */
export async function registerCommands(env) {
  const r = await fetch(`${API}/applications/${env.DISCORD_APP_ID}/guilds/${env.DISCORD_GUILD_ID}/commands`, { method: "PUT", headers: bot(env), body: JSON.stringify(COMMANDS) });
  if (!r.ok) throw new Error(`Discord refused the commands (${r.status}): ${await r.text()}`);
  return (await r.json()).map((c) => c.name);
}
