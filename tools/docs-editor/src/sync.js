// Syncing the docs through git, so nobody has to use git.
//
// One rule above all: the Codex only ever commits files under docs/. Whatever else is in the folder
// (code, Unity scenes, your uncommitted work) is never staged, committed, set aside or overwritten.
//
//   saved is sent  a page saved *in the Codex* goes to GitHub once typing pauses (SEND_AFTER_MS), or
//               at the latest SEND_AT_MOST_MS after its first unsent save while typing goes on: about one
//               commit per burst of editing. A page made or deleted goes within seconds; closing the
//               app sends everything. There is no Sync button: the status line says how it stands.
//   others' work  when anyone sends, the helper tells every open Codex through the live room
//               (src/together.js, "pages"), and it fetches at once (news()); a check every minute is
//               the safety net. What others sent is told to the page ("news").
//   asked first  pages R'th AI changed, or another program did (an agent, another editor), are held:
//               the page asks (src/app.js, the share panel) and share() sends them, or discard() puts
//               them back as they were last shared. They stay held until answered, across restarts.
//               Comments (docs/.notes/) are always sent: they aren't pages, and agents write them too.
//
// Fetching merges others' commits into yours with `git merge` (never `--autostash`, never a rebase,
// which both need a clean folder). git refuses on its own if incoming changes touch a file with
// uncommitted changes; then the Codex says so and tries again later. Two people changing the same
// lines of a page: the merge is undone and the pages named; nothing is left half-merged.

import { execFile } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const MARK = "Synced by the Codex."  // every commit the Codex makes says so: it only ever sends those
const AUTO_EVERY_MS = 60 * 1000;       // the safety net: how often it looks anyway
const SEND_AFTER_MS = 10 * 1000;       // a saved page is sent once typing has paused this long...
const SEND_AT_MOST_MS = 2 * 60 * 1000; // ...or this long after its first unsent save, whatever the typing
const SOON_MS = 4 * 1000;              // a page made or deleted: sent this soon

export function makeSync({ repo, git = process.env.CODEX_GIT || "git", emit = () => {}, scope = "docs", auth = async () => null }) {
  const S = {
    busy: false, enabled: null, reason: null, branch: null, upstream: null,
    lastSync: null, lastResult: null, problem: null, offline: false, again: false,
    ahead: 0, behind: 0, waiting: [], outside: [],
  };
  const saved = new Set();   // pages saved in the Codex since the last sync (relative paths)
  const byAi = new Set();    // ...of those, pages the AI changed (their commit says "with AI")
  let timer = null;

  function run(args, { ok = [0] } = {}) {
    return new Promise((resolve) => {
      execFile(git, args, { cwd: repo, maxBuffer: 32 << 20, windowsHide: true, env: { ...process.env, GIT_TERMINAL_PROMPT: "0", GIT_MERGE_AUTOEDIT: "no" } },
        (err, stdout, stderr) => resolve({ code: err ? (typeof err.code === "number" ? err.code : 1) : 0, out: stdout.toString(), err: stderr.toString(), ok: !err || ok.includes(err.code) }));
    });
  }

  // Signed in with Discord (src/discord-account.js): commits carry the person's Discord name, and
  // fetching and sending use the helper's hour-long key instead of this computer's own git login.
  let creds = null;
  const who = () => (creds ? ["-c", `user.name=${creds.name} (Discord)`, "-c", `user.email=${creds.email}`] : []);
  const key = () => (creds ? ["-c", "http.https://github.com/.extraheader=AUTHORIZATION: basic " + Buffer.from("x-access-token:" + creds.token).toString("base64")] : []);

  const midOperation = () => ["MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply"].some((f) => fs.existsSync(path.join(repo, ".git", f)));

  /** What's waiting to be sent, and how this copy stands against GitHub (without fetching). */
  async function refresh() {
    const top = await run(["rev-parse", "--show-toplevel"]);
    if (!top.ok) return disable("This folder isn't a git copy of the game's repository.");
    const br = await run(["rev-parse", "--abbrev-ref", "HEAD"]);
    S.branch = br.out.trim();
    const up = await run(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"]);
    if (!up.ok) return disable(`The branch “${S.branch}” isn't connected to GitHub, so there's nothing to sync with.`);
    S.upstream = up.out.trim();
    S.enabled = true; S.reason = null;
    const st = await run(["status", "--porcelain", "-z", "--untracked-files=all", "--", scope]);
    const changed = st.out.split("\0").filter((e) => e.length > 3).map((e) => e.slice(3)).filter((f) => !/\.codex-tmp$/.test(f));
    S.waiting = changed;
    // Held: the AI's or another program's changes to pages, which wait to be shared (or put back).
    S.heldPages = changed.filter((f) => !f.startsWith(NOTES) && (byAi.has(f) || !saved.has(f)));
    const ab = await run(["rev-list", "--left-right", "--count", "HEAD...@{u}"]);
    const [a, b] = ab.out.trim().split(/\s+/).map(Number);
    S.ahead = a || 0; S.behind = b || 0;
    return view();
  }
  function disable(reason) { S.enabled = false; S.reason = reason; return view(); }

  const NOTES = scope + "/.notes/";
  const sendable = (f) => f.startsWith(NOTES) || (saved.has(f) && !byAi.has(f));
  const view = () => ({
    enabled: S.enabled, reason: S.reason, busy: S.busy, clash: S.problem ? S.clash || null : null, branch: S.branch, lastSync: S.lastSync, lastResult: S.lastResult, problem: S.problem,
    offline: S.offline, auto: !!timer, sending: !!sendTimer || (!!timer && saved.size > 0), // auto: this Codex shares by itself (the app)
    waiting: S.waiting.length, ahead: S.ahead, behind: S.behind,
    held: (S.heldPages || []).map((f) => ({ path: f, title: titleOf(f), by: byAi.has(f) ? "ai" : "other", gone: !fs.existsSync(path.join(repo, f)) })),
  });
  const tell = () => emit("sync", view());

  const titleOf = (f) => {
    if (f.includes("/templates/")) return path.basename(f, ".md"); // a template's heading is a placeholder ("Title")
    try { const m = fs.readFileSync(path.join(repo, f), "utf8").match(/^#\s+(.+)$/m); if (m) return m[1].replace(/[*_`]/g, "").trim(); } catch {}
    return path.basename(f).replace(/\.(md|json)$/, "");
  };

  /** Commit (docs only), fetch, merge, send. `share`: held pages someone said to share (share()). */
  // The reply is built after the work and its `finally` (which marks it done), never inside them.
  async function sync(opts) { await doSync(opts); return view(); }
  // commit: false (news from others): fetch, merge and send what's committed, but leave pages being
  // typed in for their own send, so a burst of editing stays one commit.
  async function doSync({ share = [], why = "auto", commit = true } = {}) {
    if (S.busy) { S.again = true; return; } // one at a time: it runs again once this one is done
    S.busy = true; tell();
    try {
      await refresh();
      if (!S.enabled) return view();
      if (midOperation()) { S.problem = "git is in the middle of something else in this folder (a merge or rebase). Syncing waits until that's finished."; return view(); }
      let sent = 0, got = 0;
      const a = await auth();
      if (a?.error) { S.problem = a.error + " Pages are still saved and committed here."; creds = null; }
      else creds = a;

      // 1. Commit: only files under docs/, only those this sync may send.
      const files = !commit ? [] : S.waiting.filter((f) => sendable(f) || share.includes(f));
      if (files.length) {
        const add = await run(["add", "-A", "--", ...files]);
        if (!add.ok) throw new Error(add.err.trim() || "git couldn't stage the pages");
        // A deleted page is named by its title as it was ("deleted Combat").
        const named = await Promise.all(files.filter((f) => f.endsWith(".md")).map(async (f) => {
          if (fs.existsSync(path.join(repo, f))) return titleOf(f);
          const was = (await run(["show", `HEAD:${f}`])).out.match(/^#\s+(.+)$/m);
          return "deleted " + (was ? was[1].replace(/[*_`]/g, "").trim() : path.basename(f, ".md"));
        }));
        const pages = [...new Set(named)];
        const subject = pages.length
          ? "Docs: " + (pages.length > 3 ? pages.slice(0, 3).join(", ") + ` and ${pages.length - 3} more` : pages.join(", "))
          : "Docs: comments";
        const ai = files.some((f) => byAi.has(f)) ? " (with AI)" : "";
        const c = await run([...who(), "commit", "--quiet", "-m", subject + ai, "-m", MARK, "--only", "--", ...files]);
        if (!c.ok && !/nothing to commit|no changes added/i.test(c.out + c.err)) throw new Error(c.err.trim() || c.out.trim());
        for (const f of files) { saved.delete(f); byAi.delete(f); }
        if (!saved.size) { clearTimeout(sendTimer); sendTimer = null; firstUnsent = 0; }
      }

      // 2. Fetch and merge others' work.
      const [remote] = S.upstream.split("/");
      const f = await run([...key(), "fetch", "--quiet", remote]);
      if (!f.ok) { S.offline = true; S.problem = "Couldn't reach GitHub. Your pages are saved here and go out when it's back."; return view(); }
      S.offline = false;
      await refresh();
      if (S.behind) {
        const before = (await run(["rev-parse", "HEAD"])).out.trim();
        const m = await run([...who(), "merge", "--quiet", "-m", "Docs: take in others' changes", "-m", MARK, "@{u}"]);
        if (!m.ok) {
          const text = m.out + m.err;
          const conflicts = (await run(["diff", "--name-only", "--diff-filter=U"])).out.split("\n").filter(Boolean);
          if (fs.existsSync(path.join(repo, ".git", "MERGE_HEAD"))) await run(["merge", "--abort"]);
          S.clash = conflicts.length && conflicts.every((f) => f.startsWith(scope + "/")) ? conflicts.map(titleOf) : null;
          S.problem = conflicts.length
            ? `Someone else changed the same lines of ${conflicts.map(titleOf).join(", ")}. Nothing was merged; your version is as it was.`
            : /would be overwritten|untracked working tree files/i.test(text)
              ? "Others' changes touch files you have uncommitted changes in, outside the Codex. Syncing will try again after those are committed."
              : "Couldn't take in others' changes: " + text.trim().split("\n").pop();
          return view();
        }
        got = Number((await run(["rev-list", "--count", `${before}..HEAD`])).out.trim()) || 0;
        if (got) await tellNews(before);
      }

      // 3. Send, but only the Codex's own commits. Git sends a branch's history in order, so if commits
      // it didn't make are waiting (someone's unpushed work), it holds everything back and says so.
      await refresh();
      if (S.ahead) {
        const log = await run(["log", "--format=%B%n==codex-commit-end==", "@{u}..HEAD"]);
        const others = log.out.split("==codex-commit-end==").map((x) => x.trim()).filter(Boolean).filter((x) => !x.includes(MARK)).length;
        if (others) {
          S.problem = `This branch has ${others} commit${others > 1 ? "s" : ""} not on GitHub yet that the Codex didn't make. It only sends its own, and git sends a branch's history in order, so nothing is sent until you push ${others > 1 ? "those" : "that"} yourself. Pages are still saved and committed here.`;
          S.held = true;
          return view();
        }
        S.held = false;
        const p = await run([...key(), "push", "--quiet", remote, `HEAD:${S.upstream.slice(remote.length + 1)}`]);
        if (!p.ok) {
          S.problem = /rejected|fetch first|non-fast-forward/i.test(p.err)
            ? "Someone sent changes at the same moment. Syncing again shortly."
            : "Couldn't send to GitHub: " + p.err.trim().split("\n").pop();
          if (/rejected|fetch first/i.test(p.err)) setTimeout(() => sync({ why: "retry" }), 5000);
          return view();
        }
        sent = S.ahead;
      }
      S.problem = null;
      S.lastSync = Date.now();
      S.lastResult = { sent, got, why };
      await refresh();
      return view();
    } catch (e) {
      S.problem = "Syncing failed: " + e.message;
      return view();
    } finally {
      S.busy = false;
      tell();
      if (S.again) { S.again = false; setTimeout(() => sync({ why: "again" }), 500); }
    }
  }

  /**
   * What others' commits just brought (pages made, changed, deleted, and by whom), for the page to say:
   * emitted as "news" { pages: [{ path, title, status: "added" | "changed" | "deleted", by }] }.
   */
  async function tellNews(before) {
    const d = await run(["diff", "--name-status", "--no-renames", `${before}..HEAD`, "--", scope]);
    const pages = [];
    for (const line of d.out.split("\n")) {
      const [st, f] = line.split("\t");
      if (!f || !f.endsWith(".md") || f.startsWith(scope + "/.notes/")) continue;
      const names = (await run(["log", "--format=%an", `${before}..HEAD`, "--", f])).out.split("\n").map((n) => n.replace(/\s*\(Discord\)$/, "").trim()).filter(Boolean);
      const by = [...new Set(names)];
      pages.push({ path: f, title: st === "D" ? path.basename(f, ".md").replace(/[-_]/g, " ") : titleOf(f), status: st === "A" ? "added" : st === "D" ? "deleted" : "changed", by });
    }
    if (pages.length) emit("news", { pages });
  }

  /**
   * Settle a clash: merge others' work again, and for the pages changed on both sides keep "mine" or
   * "theirs" whole. Only ever for pages under docs/; anything else and the merge is undone untouched.
   */
  /** Send held pages: someone said so (the share panel, or the page's Share after reviewing). */
  async function share(files) {
    const ok = (files || []).filter((f) => typeof f === "string" && f.startsWith(scope + "/") && !f.includes(".."));
    return sync({ why: "share", share: ok });
  }

  /** Put held pages back as they were last shared: changed ones restored, new ones removed. */
  async function discard(files) {
    if (S.busy) return view();
    for (const f of (files || []).filter((x) => typeof x === "string" && x.startsWith(scope + "/") && !x.includes(".."))) {
      const known = await run(["cat-file", "-e", `HEAD:${f}`]);
      if (known.ok) await run(["checkout", "HEAD", "--", f]);
      else fs.rmSync(path.join(repo, f), { force: true });
      saved.delete(f); byAi.delete(f);
    }
    await refresh(); tell();
    return view();
  }

  /** A held page as it was last shared (for Review): its text at HEAD, or "" for a new page. */
  async function base(f) {
    if (!f.startsWith(scope + "/") || f.includes("..")) return "";
    const r = await run(["show", `HEAD:${f}`]);
    return r.ok ? r.out : "";
  }

  async function resolve(keep) { const again = await doResolve(keep); return again ? sync({ why: "resolve" }) : view(); }
  async function doResolve(keep) {
    if (S.busy || !["mine", "theirs"].includes(keep)) return false;
    S.busy = true; tell();
    try {
      await refresh();
      if (!S.enabled || midOperation()) return false;
      const a = await auth();
      creds = a && !a.error ? a : null;
      const m = await run([...who(), "merge", "--quiet", "-m", "Docs: take in others' changes", "-m", MARK, "@{u}"]);
      if (m.ok) { S.problem = null; return true; }
      const conflicts = (await run(["diff", "--name-only", "--diff-filter=U"])).out.split("\n").filter(Boolean);
      if (!conflicts.length || conflicts.some((f) => !f.startsWith(scope + "/"))) {
        if (fs.existsSync(path.join(repo, ".git", "MERGE_HEAD"))) await run(["merge", "--abort"]);
        S.problem = "That clash isn't only in the docs, so the Codex left it alone.";
        return false;
      }
      await run(["checkout", keep === "mine" ? "--ours" : "--theirs", "--", ...conflicts]);
      await run(["add", "--", ...conflicts]);
      const c = await run([...who(), "commit", "--quiet", "--no-edit"]);
      if (!c.ok) { await run(["merge", "--abort"]); S.problem = "Couldn't settle the clash: " + c.err.trim(); return false; }
      S.problem = null;
    } finally { S.busy = false; tell(); }
    return true;
  }

  return {
    refresh: async () => { const v = await refresh(); tell(); return v; },
    sync,
    resolve,
    share,
    discard,
    base,
    /** The Codex saved this page (or comment file): it's sent shortly (see SEND_AFTER_MS). */
    saved(rel, { ai = false, soon = false } = {}) {
      if (!rel.startsWith(scope + "/")) return;
      saved.add(rel); if (ai) byAi.add(rel);
      if (timer) {
        // A page made or deleted goes within seconds (it isn't being typed in); edits once typing pauses,
        // but never later than SEND_AT_MOST_MS after the first unsent save.
        firstUnsent ||= Date.now();
        const wait = soon ? SOON_MS : Math.max(0, Math.min(SEND_AFTER_MS, firstUnsent + SEND_AT_MOST_MS - Date.now()));
        clearTimeout(sendTimer);
        sendTimer = setTimeout(() => { sendTimer = null; firstUnsent = 0; sync({ why: soon ? "soon" : "saved" }); }, wait);
      }
      refresh().then(tell);
    },
    /** Someone sent changes (the helper said so through the live room): take them in now. */
    news() { if (timer) sync({ why: "news", commit: false }); },
    /** A doc changed on disk (anyone): the waiting count changes. */
    touched() { clearTimeout(touchTimer); touchTimer = setTimeout(() => refresh().then(tell), 400); },
    /** Automatic sync: shortly after starting, then every minute as a safety net (a page waiting to be
     *  sent is left to its own timer, so a burst of editing stays one commit). */
    startAuto() {
      const tick = () => sync({ why: "auto", commit: !sendTimer });
      firstTick = setTimeout(tick, 15 * 1000);
      timer = setInterval(tick, AUTO_EVERY_MS);
    },
    stopAuto() { clearInterval(timer); clearTimeout(firstTick); clearTimeout(sendTimer); timer = null; sendTimer = null; },
    view,
  };
}
let touchTimer = null, sendTimer = null, firstUnsent = 0, firstTick = null;
