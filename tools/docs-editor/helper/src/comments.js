// Comments in Discord (PLAN.md, "Comments in Discord"). Each comment is a post in the forum channel;
// its replies are messages in the post. The Codex posts through one webhook, which carries each
// person's Discord name and avatar (and "R'th AI" for the AI), so it reads as them in Discord. Discord
// holds the words; the database (schema.sql) holds what it can't: the page and passage, kind, status.
//
// Reading: Discord only tells programs that stay connected about new messages, and the helper doesn't,
// so a read first looks at the server's active threads (one request, shared for a few seconds by
// everyone who asks) and re-reads only the posts whose last message changed.
//
//   GET  /comments?since=ISO     comments changed since then: [{ page, note }], in the Codex's shape
//   POST /comments               { page, title, kind, text, anchor }       a new comment
//   POST /comments/reply         { id, text, status?, run? }               a reply
//   POST /comments/update        { id, kind?, status?, anchor?, page?, tags?, text?, touch? }
//   POST /comments/delete        { id }

const API = "https://discord.com/api/v10";
const POLL_EVERY_MS = 5000;
const RESOLVED_EMOJI = "✅";

const bot = (env) => ({ Authorization: "Bot " + env.DISCORD_BOT_TOKEN, "Content-Type": "application/json", "User-Agent": "DiscordBot (https://github.com/Martifugit/rth, 1)" });
async function discord(env, path, { method = "GET", body, auth = true } = {}) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const r = await fetch(API + path, { method, headers: auth ? bot(env) : { "Content-Type": "application/json" }, body: body ? JSON.stringify(body) : undefined });
    if (r.status === 429) { const j = await r.json().catch(() => ({})); await new Promise((s) => setTimeout(s, Math.min(5000, (j.retry_after || 1) * 1000))); continue; }
    if (r.status === 204) return null;
    const j = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(`Discord ${method} ${path.split("?")[0]}: ${r.status} ${j.message || ""}`);
    return j;
  }
  throw new Error("Discord kept asking to slow down");
}

const now = () => new Date().toISOString().replace(/\.\d+Z$/, "Z");
const clip = (s, n) => { s = String(s || ""); return s.length > n ? s.slice(0, n - 1).trimEnd() + "…" : s; };
const one = (s) => String(s || "").replace(/\s+/g, " ").trim();

async function config(env, k) { return (await env.DB.prepare("SELECT v FROM config WHERE k = ?").bind(k).first())?.v ?? null; }
async function setConfig(env, k, v) { await env.DB.prepare("INSERT INTO config (k, v) VALUES (?, ?) ON CONFLICT(k) DO UPDATE SET v = excluded.v").bind(k, v).run(); }

/** The forum's webhook (made once by the bot): { id, token }. */
async function webhook(env) {
  const saved = await config(env, "webhook");
  if (saved) return JSON.parse(saved);
  const w = await discord(env, `/channels/${env.DISCORD_COMMENTS_CHANNEL}/webhooks`, { method: "POST", body: { name: "R'th Codex" } });
  const v = { id: w.id, token: w.token };
  await setConfig(env, "webhook", JSON.stringify(v));
  return v;
}

/** Post through the webhook as someone. `thread`: reply in that post; else `name` starts a new post. */
async function postAs(env, who, content, { thread, name } = {}) {
  const w = await webhook(env);
  const q = new URLSearchParams({ wait: "true" });
  if (thread) q.set("thread_id", thread);
  const body = {
    content: clip(content, 2000),
    username: clip((who.name || "Someone").replace(/discord|clyde/gi, (m) => m[0] + "​" + m.slice(1)), 80),
    avatar_url: who.avatar || undefined,
    allowed_mentions: { parse: [] },
    flags: 4, // no link previews
  };
  if (name) body.thread_name = clip(name, 100);
  return discord(env, `/webhooks/${w.id}/${w.token}?${q}`, { method: "POST", body, auth: false });
}

// The AI's comments, whichever AI wrote them (Claude, an OpenRouter model, an agent session): one name
// and the R'th logo (from the public repository) as its avatar.
const AI = { name: "R'th AI", avatar: "https://raw.githubusercontent.com/Martifugit/rth/main/tools/docs-editor/assets/logo-96.png" };

// ----------------------------------------------------------------------------- reading from Discord

/** Bring the database up to date with the forum (at most every few seconds, whoever asks). */
async function poll(env) {
  const last = Number(await config(env, "last_poll")) || 0;
  if (Date.now() - last < POLL_EVERY_MS) return;
  await setConfig(env, "last_poll", String(Date.now()));

  const active = (await discord(env, `/guilds/${env.DISCORD_GUILD_ID}/threads/active`)).threads
    .filter((t) => t.parent_id === env.DISCORD_COMMENTS_CHANNEL);
  if (!active.length) return;
  const ids = active.map((t) => t.id);
  const rows = new Map((await env.DB.prepare(`SELECT id, last_msg, status FROM comments WHERE id IN (${ids.map(() => "?").join(",")})`).bind(...ids).all()).results.map((r) => [r.id, r]));

  for (const t of active) {
    const row = rows.get(t.id);
    const tagged = (t.applied_tags || []).includes(env.DISCORD_RESOLVED_TAG);
    if (row && row.last_msg === t.last_message_id) {
      // No new messages; only the Resolved tag may have changed in Discord.
      if (tagged && row.status !== "done") await env.DB.prepare("UPDATE comments SET status = 'done', updated = ? WHERE id = ?").bind(now(), t.id).run();
      else if (!tagged && row.status === "done" && t.thread_metadata && !t.thread_metadata.archived) await env.DB.prepare("UPDATE comments SET status = 'open', updated = ? WHERE id = ?").bind(now(), t.id).run();
      continue;
    }
    await reread(env, t, row, tagged);
  }
}

/** Read a post's messages again and store them in the Codex's shape. */
async function reread(env, t, row, tagged) {
  const msgs = (await discord(env, `/channels/${t.id}/messages?limit=100`)).reverse();
  const posted = new Map((await env.DB.prepare("SELECT * FROM posted WHERE thread_id = ?").bind(t.id).all()).results.map((p) => [p.msg_id, p]));
  const thread = [];
  for (const m of msgs) {
    if (m.type !== 0 && m.type !== 19) continue; // ordinary messages and replies only
    const p = posted.get(m.id);
    if (p) {
      const e = { by: p.by, at: m.timestamp.replace(/\.\d+/, ""), text: p.text };
      if (p.by === "person") e.who = { id: p.user_id, name: p.name, avatar: p.avatar };
      if (p.run) e.run = JSON.parse(p.run);
      thread.push(e);
    } else {
      const u = m.author;
      let text = m.content || "";
      for (const a of m.attachments || []) text += (text ? "\n\n" : "") + (a.content_type?.startsWith("image/") ? `![${a.filename}](${a.url})` : `[${a.filename}](${a.url})`);
      if (!text.trim()) continue;
      thread.push({ by: "person", who: { id: u.id, name: m.member?.nick || u.global_name || u.username, avatar: avatar(u) }, at: m.timestamp.replace(/\.\d+/, ""), text });
    }
  }
  if (!thread.length) return;
  const starter = msgs.find((m) => m.id === t.id);
  const ticked = starter?.reactions?.some((r) => r.emoji?.name === RESOLVED_EMOJI);
  const latest = thread[thread.length - 1].at;
  if (row) {
    let status = row.status;
    if (tagged || ticked) status = "done";
    else if (status === "done") status = "open"; // someone wrote in a resolved post: it's open again
    else if (status === "answered" && thread[thread.length - 1].by === "person") status = "open";
    await env.DB.prepare("UPDATE comments SET thread = ?, last_msg = ?, status = ?, updated = ? WHERE id = ?")
      .bind(JSON.stringify(thread), t.last_message_id, status, latest, t.id).run();
  } else {
    // A post started in Discord: unfiled until someone attaches it to a page in the Codex.
    await env.DB.prepare("INSERT INTO comments (id, page, kind, status, anchor, tags, created, updated, last_msg, thread) VALUES (?, NULL, 'note', ?, ?, '[]', ?, ?, ?, ?)")
      .bind(t.id, tagged || ticked ? "done" : "open", JSON.stringify({ section: [], quote: "", prefix: "", suffix: "", title: t.name }), thread[0].at, latest, t.last_message_id, JSON.stringify(thread)).run();
  }
}

function avatar(u) {
  if (u.avatar) return `https://cdn.discordapp.com/avatars/${u.id}/${u.avatar}.png?size=64`;
  return `https://cdn.discordapp.com/embed/avatars/${Number((BigInt(u.id) >> 22n) % 6n)}.png`;
}

// ----------------------------------------------------------------------------- the Codex's shape

const shape = (r) => ({
  page: r.page,
  note: {
    id: r.id, kind: r.kind, status: r.status, created: r.created, updated: r.updated,
    anchor: JSON.parse(r.anchor), thread: JSON.parse(r.thread), tags: JSON.parse(r.tags),
  },
});
async function get(env, id) {
  const r = await env.DB.prepare("SELECT * FROM comments WHERE id = ?").bind(id).first();
  return r ? shape(r) : null;
}

// ----------------------------------------------------------------------------- the routes

/** `as`: { kind: "person", user: { id, name, avatar } } or { kind: "agent" }. */
export async function comments(req, env, url, as) {
  const path = url.pathname;
  if (req.method === "GET" && path === "/comments") {
    try { await poll(env); } catch (e) { console.error("poll", e.message); }
    const since = url.searchParams.get("since") || "";
    const rows = (await env.DB.prepare("SELECT * FROM comments WHERE updated > ? ORDER BY updated").bind(since).all()).results;
    const latest = rows.length ? rows[rows.length - 1].updated : since;
    return { comments: rows.map(shape), until: latest };
  }
  if (req.method !== "POST") return null;
  const b = await req.json().catch(() => ({}));
  // The AI posts as R'th AI: with the owner's agent key, or from someone's Codex running Claude for them.
  const by = as.kind === "agent" || b.by === "agent" ? "agent" : "person";
  const who = by === "agent" ? AI : as.user;

  if (path === "/comments") {
    const text = String(b.text || "").trim();
    if (!text) return { status: 400, error: "An empty comment." };
    const anchor = cleanAnchor(b.anchor);
    const title = one(b.title) || (b.page ? b.page.split("/").pop().replace(/\.md$/, "") : "Comment");
    const name = anchor.quote ? `${title}: “${one(anchor.quote)}”` : title;
    const link = b.page ? `\n-# [Open in Codex](<${url.origin}/open?${new URLSearchParams({ p: b.page })}>)` : "";
    const quote = anchor.quote ? clip(anchor.quote, 600).split("\n").map((l) => "> " + l).join("\n") + "\n" : "";
    const msg = await postAs(env, who, quote + text + link, { name });
    const at = now();
    const entry = { by, at, text, ...(by === "person" ? { who } : {}) };
    await env.DB.batch([
      env.DB.prepare("INSERT INTO posted (msg_id, thread_id, by, user_id, name, avatar, text) VALUES (?, ?, ?, ?, ?, ?, ?)").bind(msg.id, msg.channel_id, by, who.id || null, who.name, who.avatar || null, text),
      env.DB.prepare("INSERT INTO comments (id, page, kind, status, anchor, tags, created, updated, last_msg, thread) VALUES (?, ?, ?, 'open', ?, ?, ?, ?, ?, ?)")
        .bind(msg.channel_id, b.page || null, ["note", "ask", "task"].includes(b.kind) ? b.kind : "note", JSON.stringify(anchor), JSON.stringify(Array.isArray(b.tags) ? b.tags : []), at, at, msg.id, JSON.stringify([entry])),
    ]);
    return get(env, msg.channel_id);
  }

  const cur = await get(env, String(b.id || ""));
  if (!cur) return { status: 404, error: "No such comment." };
  const id = cur.note.id;

  if (path === "/comments/reply") {
    const text = String(b.text || "").trim();
    if (!text) return { status: 400, error: "An empty reply." };
    if (cur.note.status === "done") await setResolved(env, id, false);
    let msg;
    try { msg = await postAs(env, who, text, { thread: id }); }
    catch (e) {
      // Discord archives quiet posts by itself after a while: open it again and post.
      await discord(env, `/channels/${id}`, { method: "PATCH", body: { archived: false } });
      msg = await postAs(env, who, text, { thread: id });
    }
    const at = now();
    const entry = { by, at, text, ...(by === "person" ? { who } : {}), ...(b.run ? { run: b.run } : {}) };
    let status = cur.note.status === "done" ? "open" : cur.note.status;
    if (["open", "answered", "done"].includes(b.status)) status = b.status;
    else if (by === "agent" && status === "open") status = "answered";
    else if (by === "person" && status === "answered") status = "open";
    if (status === "done") await setResolved(env, id, true);
    await env.DB.batch([
      env.DB.prepare("INSERT INTO posted (msg_id, thread_id, by, user_id, name, avatar, text, run) VALUES (?, ?, ?, ?, ?, ?, ?, ?)").bind(msg.id, id, by, who.id || null, who.name, who.avatar || null, text, b.run ? JSON.stringify(b.run) : null),
      env.DB.prepare("UPDATE comments SET thread = ?, status = ?, updated = ?, last_msg = ? WHERE id = ?").bind(JSON.stringify([...cur.note.thread, entry]), status, at, msg.id, id),
    ]);
    return get(env, id);
  }

  if (path === "/comments/update") {
    const n = cur.note, sets = [], vals = [];
    if (["note", "ask", "task"].includes(b.kind)) { sets.push("kind = ?"); vals.push(b.kind); }
    if (["open", "answered", "done"].includes(b.status) && b.status !== n.status) {
      if ((b.status === "done") !== (n.status === "done")) await setResolved(env, id, b.status === "done");
      sets.push("status = ?"); vals.push(b.status);
    }
    if (b.anchor !== undefined) { sets.push("anchor = ?"); vals.push(JSON.stringify(cleanAnchor(b.anchor))); }
    if (b.page !== undefined) { sets.push("page = ?"); vals.push(b.page || null); }
    if (Array.isArray(b.tags)) { sets.push("tags = ?"); vals.push(JSON.stringify(b.tags.map(String))); }
    if (typeof b.text === "string" && b.text.trim() && n.thread.length) {
      // The first message: edit what was posted (only the Codex's own posts can be edited).
      const p = await env.DB.prepare("SELECT * FROM posted WHERE msg_id = ?").bind(id).first();
      if (p) {
        const w = await webhook(env);
        const old = await discord(env, `/webhooks/${w.id}/${w.token}/messages/${id}?thread_id=${id}`, { auth: false });
        const content = old.content.replace(p.text, b.text.trim());
        await discord(env, `/webhooks/${w.id}/${w.token}/messages/${id}?thread_id=${id}`, { method: "PATCH", body: { content: clip(content, 2000) }, auth: false });
        await env.DB.prepare("UPDATE posted SET text = ? WHERE msg_id = ?").bind(b.text.trim(), id).run();
      }
      const thread = n.thread.slice(); thread[0] = { ...thread[0], text: b.text.trim() };
      sets.push("thread = ?"); vals.push(JSON.stringify(thread));
    }
    if (b.touch !== false) { sets.push("updated = ?"); vals.push(now()); }
    if (sets.length) await env.DB.prepare(`UPDATE comments SET ${sets.join(", ")} WHERE id = ?`).bind(...vals, id).run();
    return get(env, id);
  }

  if (path === "/comments/delete") {
    try { await discord(env, `/channels/${id}`, { method: "DELETE" }); } catch (e) { if (!/404/.test(e.message)) throw e; }
    await env.DB.batch([
      env.DB.prepare("DELETE FROM comments WHERE id = ?").bind(id),
      env.DB.prepare("DELETE FROM posted WHERE thread_id = ?").bind(id),
    ]);
    return { ok: true };
  }
  return null;
}

/** Resolve in Discord: the Resolved tag, and the post closed. Reopening: the reverse. */
async function setResolved(env, id, done) {
  if (done) await discord(env, `/channels/${id}`, { method: "PATCH", body: { applied_tags: [env.DISCORD_RESOLVED_TAG] } });
  await discord(env, `/channels/${id}`, { method: "PATCH", body: done ? { archived: true } : { archived: false, applied_tags: [] } });
}

function cleanAnchor(a) {
  if (!a || typeof a !== "object") return { section: [], quote: "", prefix: "", suffix: "" };
  return {
    section: Array.isArray(a.section) ? a.section.map(String) : [],
    quote: String(a.quote || ""), prefix: String(a.prefix || ""), suffix: String(a.suffix || ""),
  };
}
