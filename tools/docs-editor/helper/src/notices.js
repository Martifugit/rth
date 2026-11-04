// "What changed": GitHub tells the helper about each push to the main branch (the Codex's syncs, an
// agent's commits, anyone's), and the helper posts short messages in #codex naming who changed which
// pages, each a link that opens it in the Codex. Comment files (docs/.notes) and anything outside the
// docs are left out; a push that touches no page posts nothing.
//
// Batched per person: the Codex sends after every pause in typing (so others see pages live), which
// made a message per pause. Now a push only queues its pages (D1: notice_pages, notice_why), and the
// helper's timer (index.js, scheduled, every 5 minutes) posts someone's batch once they've been quiet
// for QUIET_MS, or once their oldest queued change is MAX_WAIT_MS old if they keep going.

import { installationToken, gh } from "./github.js";
import { post } from "./discord.js";

const MAX_PAGES = 12;
export const QUIET_MS = 30 * 60 * 1000;    // posted once the person has stopped for half an hour...
export const MAX_WAIT_MS = 2 * 60 * 60 * 1000; // ...or every two hours while they keep going

/** A push to main: queue who changed which pages, to post later (sendDue). */
export async function onPush(env, push, origin, now = Date.now()) {
  const branch = push.repository?.default_branch || "main";
  if (push.ref !== `refs/heads/${branch}` || push.deleted || /^0+$/.test(push.before || "0")) return;
  const isPage = (f) => f.endsWith(".md") && (f.startsWith("docs/") || !f.includes("/")) && !f.startsWith("docs/.notes/");

  const token = (await installationToken(env)).token;
  const cmp = await gh(env, `/repos/${env.REPO}/compare/${push.before}...${push.after}`, token);
  const stat = new Map((cmp.files || []).filter((f) => isPage(f.filename)).map((f) => [f.filename, f]));
  if (!stat.size) return;

  // Who changed what, from the commits. GitHub's message names at most 20 commits: for a bigger push,
  // the pages come from the whole comparison, under everyone who wrote its commits.
  const authors = new Map(); // name -> Set of files
  const commits = push.commits || [];
  if (commits.length >= 20 || (push.size && push.size > commits.length)) {
    const names = [...new Set((cmp.commits || []).map((c) => codexName(c.commit?.author?.name || "Someone")))];
    authors.set(names.length > 3 ? names.slice(0, 3).join(", ") + " and others" : join(names), new Set(stat.keys()));
  } else {
    for (const c of commits) {
      const files = [...(c.added || []), ...(c.modified || []), ...(c.removed || [])].filter(isPage);
      if (!files.length) continue;
      const name = codexName(c.author?.name || c.committer?.name || "Someone");
      if (!authors.has(name)) authors.set(name, new Set());
      for (const f of files) authors.get(name).add(f);
    }
  }
  for (const [name, files] of authors)
    for (const f of files) if (stat.has(f)) await queuePage(env.DB, name, f, stat.get(f).status, origin, now);

  // A commit written by a person (not the Codex's "Docs: ..." ones) says why: kept with the batch.
  for (const c of commits) {
    const msg = (c.message || "").split("\n")[0];
    if (!msg || /^Docs: /.test(msg)) continue;
    const name = codexName(c.author?.name || c.committer?.name || "Someone");
    if (authors.has(name)) await env.DB.prepare("INSERT INTO notice_why (author, msg, at) VALUES (?, ?, ?)").bind(name, clip(msg, 160), now).run();
  }
}

/** One page in someone's batch. Its status across the batch: a page made then changed is still new; made
 *  then removed was never there; removed then made again is just changed. */
export async function queuePage(db, author, path, status, origin, now) {
  const was = await db.prepare("SELECT status FROM notice_pages WHERE author = ? AND path = ?").bind(author, path).first();
  const s = mergeStatus(was?.status || null, status);
  if (!s) { await db.prepare("DELETE FROM notice_pages WHERE author = ? AND path = ?").bind(author, path).run(); return; }
  await db.prepare(
    `INSERT INTO notice_pages (author, path, status, first_at, last_at, origin) VALUES (?, ?, ?, ?, ?, ?)
     ON CONFLICT (author, path) DO UPDATE SET status = excluded.status, last_at = excluded.last_at, origin = excluded.origin`,
  ).bind(author, path, s, now, now, origin).run();
}

/** A page's status in a batch, from what it was (null: not in the batch yet) and what this push did. */
export function mergeStatus(was, now) {
  if (!was) return now;
  if (was === "added") return now === "removed" ? null : "added";
  if (was === "removed") return now === "removed" ? "removed" : "modified";
  return now === "removed" ? "removed" : "modified";
}

/** Whose batches are ready: quiet for QUIET_MS, or waiting since MAX_WAIT_MS. Pure, for tests. */
export function dueAuthors(rows, now) {
  const by = new Map();
  for (const r of rows) {
    const a = by.get(r.author) || { first: Infinity, last: 0 };
    a.first = Math.min(a.first, r.first_at); a.last = Math.max(a.last, r.last_at);
    by.set(r.author, a);
  }
  return [...by].filter(([, a]) => now - a.last >= QUIET_MS || now - a.first >= MAX_WAIT_MS).map(([name]) => name);
}

/** The timer: post the batches that are ready, one message for all of them, and forget them. */
export async function sendDue(env, now = Date.now()) {
  const rows = (await env.DB.prepare("SELECT author, path, status, first_at, last_at, origin FROM notice_pages").all()).results || [];
  const due = dueAuthors(rows, now);
  if (!due.length) return;
  const lines = [];
  for (const name of due) {
    const list = rows.filter((r) => r.author === name).sort((a, b) => a.first_at - b.first_at);
    const shown = await Promise.all(list.slice(0, MAX_PAGES).map(async (r) => {
      if (r.status === "removed") return `~~${pretty(r.path)}~~ (removed)`;
      const title = (await titleOf(env, r.path)) || pretty(r.path);
      const link = `[${title.replace(/[\[\]]/g, "")}](<${r.origin}/open?p=${encodeURIComponent(r.path)}>)`;
      return r.status === "added" ? `${link} (new page)` : link;
    }));
    const more = list.length > MAX_PAGES ? ` and ${list.length - MAX_PAGES} more` : "";
    lines.push(`**${name}** edited ${join(shown)}${more}`);
    const why = (await env.DB.prepare("SELECT msg FROM notice_why WHERE author = ? ORDER BY at").bind(name).all()).results || [];
    lines.push(...why.slice(0, 3).map((w) => "> " + w.msg));
  }
  // Forgotten first, so a failed post never posts the same batch twice (a lost notice beats a repeat).
  for (const name of due) {
    await env.DB.prepare("DELETE FROM notice_pages WHERE author = ?").bind(name).run();
    await env.DB.prepare("DELETE FROM notice_why WHERE author = ?").bind(name).run();
  }
  await post(env, env.DISCORD_NOTICES_CHANNEL, clip(lines.join("\n"), 1990));
}

/** The page's own title, its first "# " heading, as it is on main now (the repository is public). */
async function titleOf(env, file) {
  try {
    const r = await fetch(`https://raw.githubusercontent.com/${env.REPO}/main/${file.split("/").map(encodeURIComponent).join("/")}`);
    if (!r.ok) return null;
    const m = (await r.text()).match(/^#\s+(.+)$/m);
    return m ? m[1].replace(/[*_`]/g, "").trim() : null;
  } catch { return null; }
}

// Codex commits from a Discord sign-in are "Name (Discord)"; the notice is in Discord, so drop that.
const codexName = (n) => n.replace(/\s*\(Discord\)$/, "");
const pretty = (f) => f.split("/").pop().replace(/\.md$/, "").replace(/[-_]/g, " ");
const clip = (s, n) => (s.length > n ? s.slice(0, n - 1) + "…" : s);
const join = (xs) => (xs.length < 2 ? xs.join("") : xs.slice(0, -1).join(", ") + " and " + xs[xs.length - 1]);
