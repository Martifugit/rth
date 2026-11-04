// The assistant behind the AI tab (PLAN.md, "AI, built in").
//
// A conversation is a list of messages, kept on this computer only (CODEX_DATA, else .local/ beside
// this tool; git-ignored). Each message from the person comes with what the AI tab's chips said it
// may read: the page, the selected passage, the comment thread that's open. The assistant answers in
// streamed text through a provider.
//
// When asked for a change it edits pages under docs/ itself. Changes are found, not trusted: before a
// turn the engine keeps a copy of every page, and after it compares, so the message records each page
// it changed (before and after). The page draws that as Show changes; Undo puts it back.
//
// Providers are modules with one shape: run({ prompt, session, onText, onActivity }) resolves to
// { text, session, cost } or { error }, and returns a handle with stop(). Today: Claude Code, found on
// the computer. (OpenRouter is step 4.4.)

import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { spawn, execFile } from "node:child_process";
import { places } from "./ai-diff.js";
import { openRouterProvider, OPENROUTER_MODELS, EDIT_FORMAT, editsIn, applyEdits, shown } from "./openrouter-provider.js";
import { COMMENT_FORMAT, commentsIn, withoutComments, anchorFor } from "./ai-comments.js";

const STREAM_EVERY_MS = 120;

/**
 * pages: () => every page's repository path; read(rel): its text or null; write(rel, text): write it
 * back (null deletes) as the Codex saves a page; changed(rels): the AI changed these (sync sends them).
 */
export function makeAssistant({ repo, dir, store, emit, pages, read, write, changed = () => {}, openrouterKey = () => null, claude = process.env.CODEX_CLAUDE || "claude" }) {
  // Claude when it's on this computer (it reads the repository itself); otherwise OpenRouter's free
  // models, once signed in (CODEX_AI=openrouter tries OpenRouter even with Claude here).
  // The AI tab's cog (AI setup) can choose one instead, and OpenRouter's model; kept in ai.json beside
  // the conversations.
  const settingsFile = path.join(dir, "..", "ai.json");
  let settings = { use: "auto", model: "" };
  try { settings = { ...settings, ...JSON.parse(fs.readFileSync(settingsFile, "utf8")) }; } catch {}
  const models = () => (settings.model ? [settings.model] : OPENROUTER_MODELS);
  const claudeP = claudeProvider({ repo, claude });
  const openP = openRouterProvider({ key: openrouterKey, models });
  let hasClaude = null; // looked for once (Check again in AI setup looks again)
  async function pick() {
    if (settings.use === "openrouter" || process.env.CODEX_AI === "openrouter") return openP;
    if (hasClaude === null) hasClaude = (await claudeP.check()).ok;
    if (settings.use === "claude") return claudeP;
    return hasClaude ? claudeP : openP;
  }
  const provider = { check: async () => (await pick()).check() };

  /** What AI setup shows: which AI answers, and how each way in stands. */
  async function setup({ recheck = false } = {}) {
    if (recheck) hasClaude = null;
    const c = await claudeP.check();
    hasClaude = c.ok;
    const using = await pick();
    return {
      use: settings.use, using: using === claudeP ? "claude" : "openrouter",
      claude: { found: !!c.installed, signedIn: c.signedIn ?? null, version: c.version || null, error: c.ok ? null : c.error },
      openrouter: { signedIn: !!openrouterKey(), model: settings.model || "", defaultModel: OPENROUTER_MODELS.join(", ") },
      provider: await (using).check(),
    };
  }
  async function configure({ use, model }) {
    if (use !== undefined) {
      if (!["auto", "claude", "openrouter"].includes(use)) return { error: "Unknown choice." };
      settings.use = use;
    }
    if (model !== undefined) settings.model = String(model || "").trim().slice(0, 200);
    fs.mkdirSync(path.dirname(settingsFile), { recursive: true });
    fs.writeFileSync(settingsFile, JSON.stringify(settings, null, 1));
    emit("ai", { state: "provider" });
    return setup();
  }
  const runs = new Map(); // conversation id -> { text, activity, handle }
  fs.mkdirSync(dir, { recursive: true });

  const file = (id) => path.join(dir, id + ".json");
  const validId = (id) => typeof id === "string" && /^c-[\w-]+$/.test(id);

  async function readConv(id) {
    if (!validId(id)) return null;
    try { return JSON.parse(await fsp.readFile(file(id), "utf8")); } catch { return null; }
  }
  async function writeConv(c) {
    const tmp = file(c.id) + ".tmp";
    await fsp.writeFile(tmp, JSON.stringify(c, null, 1), "utf8");
    await fsp.rename(tmp, file(c.id));
  }

  /** Every conversation, newest first, without their messages. */
  async function list() {
    const out = [];
    for (const name of await fsp.readdir(dir).catch(() => [])) {
      if (!name.endsWith(".json")) continue;
      const c = await readConv(name.slice(0, -5));
      if (c) out.push({ id: c.id, title: c.title, page: c.page, updated: c.updated, count: c.messages.length });
    }
    return out.sort((a, b) => b.updated.localeCompare(a.updated));
  }

  const view = (id) => {
    const r = runs.get(id);
    return r ? { id, state: "running", text: withoutComments(r.open ? shown(r.text) : r.text), activity: r.activity } : { id, state: "idle" };
  };

  async function get(id) {
    const c = await readConv(id);
    return c && { ...c, run: view(id) };
  }

  /** The person says something. Starts a conversation when there's no id. */
  async function send({ id, text, context = {} }) {
    text = String(text || "").trim();
    if (!text) return { error: "Nothing to send." };
    let c = id ? await readConv(id) : null;
    if (id && !c) return { error: "No such conversation." };
    if (c && runs.has(c.id)) return { error: "Still answering the last message." };
    const ai = await pick();
    const ready = await ai.check();
    if (!ready.ok) return { error: ready.error, needsSignIn: ready.needsSignIn };
    const now = new Date().toISOString();
    if (!c) c = { id: "c-" + crypto.randomBytes(5).toString("hex"), title: titleFrom(text), created: now, updated: now, page: context.page || null, session: null, messages: [] };
    const ctx = await gather(context);
    c.messages.push({ by: "you", at: now, text, context: ctx.chips });
    c.updated = now;
    await writeConv(c);

    const r = { text: "", activity: "Reading", handle: null, timer: null };
    runs.set(c.id, r);
    const before = await snapshot();
    // "Suggest ...": the AI edits as usual, then the pages go back as they were and its edits wait as
    // suggestions, accepted or skipped one by one in the page (Notion's suggested edits, Word's tracked
    // changes).
    const suggest = /\bsuggest/i.test(text);
    const push = () => { if (!r.timer) r.timer = setTimeout(() => { r.timer = null; emit("ai", view(c.id)); }, STREAM_EVERY_MS); };
    emit("ai", view(c.id));
    const handlers = {
      onText: (t) => { r.text += t; r.activity = "Writing"; push(); },
      onBreak: () => { if (r.text) r.text += "\n\n"; },
      onActivity: (a) => { r.activity = a; push(); },
    };
    if (ai.kind === "openrouter") {
      // One request: the engine's gathered reading, the conversation so far, then this message.
      r.open = true;
      const history = c.messages.slice(0, -1).slice(-12).map((m) => ({ role: m.by === "you" ? "user" : "assistant", content: m.text || "" })).filter((m) => m.content);
      const system = SYSTEM_OPEN + "\n\n" + EDIT_FORMAT + "\n\n" + COMMENT_FORMAT + "\n\n" + (await reading(context));
      r.handle = ai.run({ messages: [{ role: "system", content: system }, ...history, { role: "user", content: (ctx.text ? "What they're looking at:\n\n" + ctx.text + "\n\n" : "") + "They say:\n\n" + text + (suggest ? SUGGEST_NOTE : "") }], ...handlers });
    } else {
      const prompt = (c.session ? followUp : firstPrompt)(text, ctx) + (suggest ? SUGGEST_NOTE : "");
      r.handle = ai.run({ prompt, session: c.session, ...handlers });
    }
    r.handle.done.then(async (res) => {
      clearTimeout(r.timer);
      let note = "";
      if (r.open) {
        // OpenRouter's edits are blocks in its answer: apply them, and show the answer without them.
        const { edits, text: clean } = editsIn(res.text || r.text || "");
        const { texts, failed } = await applyEdits(edits, read);
        for (const [p, t] of texts) await write(p, t);
        if (failed.length) note = "\n\n*Some edits couldn't be made: " + failed.map((f) => `${f.page} (${f.why})`).join("; ") + ".*";
        res = { ...res, text: clean };
        r.text = clean;
      }
      // Comments it was asked to leave: posted where comments are (Discord when signed in).
      const posted = [];
      if (!res.error) {
        const { comments, replies, text: clean } = commentsIn(res.text || r.text || "");
        res = { ...res, text: clean };
        r.text = clean;
        const failed = [];
        for (const x of comments) {
          try {
            if (!/^docs\/[^]+\.md$/.test(x.page) || x.page.includes("..")) throw new Error("not a page under docs/");
            const pageText = await read(x.page);
            if (pageText == null) throw new Error("no such page");
            const { anchor, title } = anchorFor(pageText, x.section, x.quote);
            const made = await store.create(x.page, { kind: "note", text: x.text, anchor, by: "agent", title });
            posted.push({ id: made.note.id, page: x.page, quote: anchor.quote || null });
          } catch (e) { failed.push(`a comment on ${x.page} (${e.message})`); }
        }
        for (const x of replies) {
          try {
            const made = await store.reply(x.to, { by: "agent", text: x.text });
            if (!made) throw new Error("no such thread");
            posted.push({ id: x.to, page: made.page ?? null, reply: true });
          } catch (e) { failed.push(`a reply (${e.message})`); }
        }
        if (failed.length) note += "\n\n*Couldn't post " + failed.join("; ") + ".*";
      }
      runs.delete(c.id);
      const fresh = (await readConv(c.id)) || c;
      const msg = { by: "ai", at: new Date().toISOString(), text: (res.error ? r.text : (res.text || r.text || "")) + note };
      if (res.model) msg.model = res.model;
      if (posted.length) msg.comments = posted;
      const changes = await compare(before);
      if (changes.length && suggest) {
        for (const x of changes) { x.suggest = true; await write(x.page, x.before); }
        msg.changes = changes;
      } else if (changes.length) { msg.changes = changes; changed(changes.map((x) => x.page)); }
      if (res.error) msg.error = res.error;
      if (res.cost) msg.cost = Math.round(res.cost * 10000) / 10000;
      fresh.messages.push(msg);
      if (res.session) fresh.session = res.session;
      fresh.updated = msg.at;
      await writeConv(fresh);
      emit("ai", { id: c.id, state: "done", error: res.error || null, pages: changes.map((x) => x.page) });
    });
    return { ok: true, id: c.id };
  }

  function stop(id) {
    const r = runs.get(id);
    if (!r) return false;
    r.handle?.stop();
    return true;
  }

  async function remove(id) {
    if (!validId(id)) return false;
    stop(id);
    await fsp.rm(file(id), { force: true });
    return true;
  }

  /**
   * What a model without the repository needs to read (OpenRouter): the open page in full, how the docs
   * are written, and the pages it links to, trimmed to what a free model can take.
   */
  async function reading({ page }) {
    const parts = [];
    let room = 90000;
    const add = (title, body, cap) => {
      if (!body || room <= 0) return;
      const t = body.length > Math.min(cap, room) ? body.slice(0, Math.min(cap, room)) + "\n[…cut short]" : body;
      parts.push(`=== ${title} ===\n${t}`);
      room -= t.length;
    };
    let pageText = null;
    if (page) { pageText = await read(page); add(`The open page: ${page}`, pageText, 50000); }
    add("docs/gdd/writing.md (how the docs are written)", await read("docs/gdd/writing.md"), 12000);
    if (pageText) {
      const seen = new Set();
      for (const m of pageText.matchAll(/\]\(([^)#\s]+\.md)(?:#[^)]*)?\)/g)) {
        const rel = path.posix.normalize(path.posix.join(path.posix.dirname(page), m[1]));
        if (seen.has(rel) || rel === page || rel.startsWith("..")) continue;
        seen.add(rel);
        if (seen.size > 4) break;
        add(`A page it links to: ${rel}`, await read(rel), 8000);
      }
    }
    return "What you can read (the docs aren't otherwise available to you):\n\n" + parts.join("\n\n");
  }

  /** What the chips point at, as text for the prompt, and as a short record kept with the message. */
  async function gather({ page, selection, thread }) {
    const chips = {}, parts = [];
    if (page) {
      chips.page = page;
      parts.push(`The page open in the Codex: ${page}`);
    }
    if (selection?.quote) {
      chips.selection = { quote: String(selection.quote).slice(0, 4000), section: selection.section || [] };
      parts.push(`The passage selected${selection.section?.length ? ` (in ${selection.section.join(" › ")})` : ""}:\n> ${chips.selection.quote.replace(/\n/g, "\n> ")}`);
    }
    if (thread && store) {
      const hit = await store.find(thread).catch(() => null);
      if (hit) {
        const n = hit.note;
        chips.thread = { id: n.id, quote: n.anchor?.quote?.slice(0, 200) || "" };
        const lines = n.thread.map((e) => `${e.by === "agent" ? "R'th AI (you)" : e.who?.name || "The owner"}: ${e.text}`).join("\n\n");
        parts.push(`The comment thread open beside the page (thread ID ${n.id})${n.anchor?.quote ? `, on "${n.anchor.quote.slice(0, 300)}"` : ""}:\n${lines}`);
      }
    }
    return { chips, text: parts.join("\n\n") };
  }

  // ----------------------------------------------------------------------------- what a turn changed

  async function snapshot() {
    const m = new Map();
    for (const p of await pages()) m.set(p, await read(p));
    return m;
  }
  async function compare(before) {
    const out = [];
    const now = new Set(await pages());
    for (const p of new Set([...before.keys(), ...now])) {
      const a = before.get(p) ?? null, b = now.has(p) ? await read(p) : null;
      if (a === b) continue;
      out.push({ page: p, before: a, after: b, places: places(a || "", b || ""), reviewed: false });
    }
    return out;
  }

  async function conversations() {
    const out = [];
    for (const name of await fsp.readdir(dir).catch(() => [])) if (name.endsWith(".json")) { const c = await readConv(name.slice(0, -5)); if (c) out.push(c); }
    return out;
  }

  /** A page's changes by the AI not yet marked done, oldest first: { conv, msg, at, before, after }. */
  async function changesFor(page) {
    const out = [];
    for (const c of await conversations())
      c.messages.forEach((m, i) => { for (const x of m.changes || []) if (x.page === page && !x.reviewed) out.push({ conv: c.id, msg: i, at: m.at, before: x.before, after: x.after, suggest: !!x.suggest }); });
    return out.sort((a, b) => a.at.localeCompare(b.at));
  }

  /** Done with a page's changes (or one turn's): Show changes stops marking them. */
  async function reviewed({ page, conv, msg }) {
    for (const c of await conversations()) {
      if (conv && c.id !== conv) continue;
      let touched = false;
      c.messages.forEach((m, i) => { if (msg != null && i !== msg) return; for (const x of m.changes || []) if ((!page || x.page === page) && !x.reviewed) { x.reviewed = true; touched = true; } });
      if (touched) await writeConv(c);
    }
    emit("ai", { id: conv || null, state: "reviewed", page: page || null });
    return { ok: true };
  }

  /**
   * Undo a turn's changes (to one page, or all it changed): a page still as the AI left it goes back to
   * how it was; one edited since is left for the page itself to undo, change by change (`open`).
   */
  async function undo({ conv, msg, page }) {
    const c = await readConv(conv);
    const m = c?.messages[msg];
    if (!m?.changes) return { error: "Nothing to undo." };
    const done = [], open = [];
    for (const x of m.changes) {
      if (page && x.page !== page) continue;
      const cur = await read(x.page);
      if (cur === x.after) { await write(x.page, x.before); done.push(x.page); x.reviewed = true; }
      else if (cur !== x.before) open.push(x.page);
      else x.reviewed = true;
    }
    await writeConv(c);
    if (done.length) changed(done);
    emit("ai", { id: conv, state: "reviewed" });
    return { ok: true, undone: done, open };
  }

  return { list, get, send, stop, remove, changesFor, reviewed, undo, setup, configure, signInClaude: () => claudeP.signIn(), check: provider.check, status: () => ({ running: [...runs.keys()] }) };
}

const titleFrom = (text) => {
  const t = text.replace(/\s+/g, " ").trim();
  return t.length > 60 ? t.slice(0, 57).replace(/\s+\S*$/, "") + "…" : t;
};

// ----------------------------------------------------------------------------- prompts

// For a model that can't read the repository itself (OpenRouter): the same job, the reading given.
const SYSTEM_OPEN = "You are the assistant in the Codex, the reader and editor for the design docs of R'th (a private RPG made by a group of friends). docs/gdd is the truth; docs/gdd/writing.md says how it's written. You're talking with someone in the Codex's side panel: discuss, explain and argue the design with them, from the pages given below. Say plainly when they don't settle something. Change pages only when they ask you to change something; discussing never changes a page. Change only what the request needs, in the page's own style. Your reply shows in a narrow side panel as Markdown: short paragraphs, lists where they help, no big headings.";

const SUGGEST_NOTE ="\n\n(They asked for suggestions: make the edits in the pages as you would; the Codex shows each one as a suggestion for them to accept or skip.)";

function firstPrompt(text, ctx) {
  return [
    "You are the assistant in the Codex, the reader and editor for the design docs of R'th (a private RPG made by a group of friends). The docs are under docs/; docs/gdd is the truth, and docs/gdd/writing.md says how it's written.",
    "You're talking with someone in the Codex's side panel. Discuss, explain and argue the design with them; look things up in the repository whenever it helps (you can read the docs, code and data). Say plainly when the docs don't settle something.",
    "You may change pages, but only when they ask you to change something (tighten this, write the missing sections, draft the decision). Discussing, explaining or arguing never changes a page. You can edit and create files under docs/ only; read docs/gdd/writing.md first and follow it and the page's own conventions, and change only what the request needs. The Codex shows them exactly what you changed, so after editing just say in a sentence or two what you did and where.",
    "Your reply shows in a narrow side panel as Markdown: short paragraphs, lists where they help, no big headings. Cite pages as repository paths.",
    COMMENT_FORMAT,
    "",
    ctx.text ? "What they're looking at:\n\n" + ctx.text : "They have no page open.",
    "",
    "They say:",
    "",
    text,
  ].join("\n");
}

function followUp(text, ctx) {
  return [
    ctx.text ? "What they're looking at now:\n\n" + ctx.text + "\n" : "",
    "(As before: change pages only if this asks for a change.)",
    "They say:",
    "",
    text,
  ].join("\n");
}

// ----------------------------------------------------------------------------- Claude Code

const READ_TOOLS = ["Read", "Glob", "Grep"];
// Pages only: anything not allowed is refused in print mode.
const EDIT_TOOLS = ["Edit(docs/**)", "Write(docs/**)", "MultiEdit(docs/**)"];
// Comments go through the Codex (COMMENT_FORMAT), never as files: docs/.notes/ is only a copy, and
// what's shown comes from Discord when signed in.
const NEVER = ["Bash", "NotebookEdit", "WebFetch", "WebSearch", "Agent", "Task", "Edit(docs/.notes/**)", "Write(docs/.notes/**)", "MultiEdit(docs/.notes/**)"];

function claudeProvider({ repo, claude }) {
  let available = null;
  function check() {
    return new Promise((resolve) => {
      execFile(claude, ["--version"], { timeout: 15000, windowsHide: true }, (err, stdout) => {
        if (err) { available = { ok: false, installed: false, error: "Claude Code isn't installed (or set CODEX_CLAUDE to its path)." }; return resolve(available); }
        const version = stdout.trim();
        // `claude auth status` prints JSON with loggedIn; older versions lack it, so unknown counts as signed in.
        execFile(claude, ["auth", "status"], { timeout: 15000, windowsHide: true }, (err2, out2) => {
          let signedIn = null;
          try { signedIn = JSON.parse(out2).loggedIn === true; } catch { signedIn = null; }
          available = signedIn === false
            ? { ok: false, installed: true, signedIn: false, version, error: "Claude Code isn't signed in." }
            : { ok: true, installed: true, signedIn, name: "Claude", version };
          resolve(available);
        });
      });
    });
  }

  function run({ prompt, session, onText, onBreak, onActivity }) {
    let proc = null, stopped = false;
    const done = (async () => {
      if (!available) await check();
      if (!available.ok) return { error: available.error };
      const args = ["-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
        "--allowedTools", ...READ_TOOLS, ...EDIT_TOOLS, "--disallowedTools", ...NEVER];
      if (session) args.push("--resume", session);
      return await new Promise((resolve) => {
        proc = spawn(claude, args, { cwd: repo, windowsHide: true, stdio: ["pipe", "pipe", "pipe"] });
        proc.stdin.end(prompt);
        let buf = "", err = "", result = null, sid = session;
        proc.stdout.setEncoding("utf8");
        proc.stdout.on("data", (chunk) => {
          buf += chunk;
          let nl;
          while ((nl = buf.indexOf("\n")) >= 0) {
            const line = buf.slice(0, nl).trim(); buf = buf.slice(nl + 1);
            if (!line) continue;
            let ev; try { ev = JSON.parse(line); } catch { continue; }
            if (ev.session_id) sid = ev.session_id;
            if (ev.type === "stream_event") {
              if (ev.event?.type === "content_block_start" && ev.event.content_block?.type === "text") onBreak();
              const d = ev.event?.delta;
              if (d?.type === "text_delta") onText(d.text);
            } else if (ev.type === "assistant") {
              for (const c of ev.message?.content || []) if (c.type === "tool_use") onActivity(describe(c));
            } else if (ev.type === "result") result = ev;
          }
        });
        proc.stderr.setEncoding("utf8");
        proc.stderr.on("data", (c) => { err += c; });
        proc.on("error", (e) => resolve({ error: e.message }));
        proc.on("close", (code) => {
          if (stopped) return resolve({ error: "Stopped.", session: sid });
          if (result && !result.is_error) resolve({ text: result.result, session: result.session_id || sid, cost: result.total_cost_usd });
          else resolve({ error: (result && result.result) || err.trim().split("\n").slice(-3).join(" ") || `claude exited with ${code}`, session: sid });
        });
      });
    })();
    return { done, stop: () => { stopped = true; try { proc?.kill(); } catch {} } };
  }

  /** Claude signs in in its own terminal (its first run asks); the Codex can open one for it. */
  function signIn() {
    if (process.platform !== "win32") return { ok: false, error: "Open a terminal and run claude once to sign in." };
    const p = spawn("cmd.exe", ["/c", `start "Sign in to Claude" cmd.exe /k "${claude}"`], { cwd: repo, detached: true, stdio: "ignore", windowsVerbatimArguments: true });
    p.on("error", () => {});
    p.unref();
    return { ok: true };
  }

  return { check, run, signIn };
}

function describe(c) {
  const i = c.input || {};
  switch (c.name) {
    case "Read": return "Reading " + String(i.file_path || "").split(/[\\/]/).pop();
    case "Grep": return "Searching the docs";
    case "Glob": return "Looking through the files";
    case "Edit": case "MultiEdit": case "Write": return "Changing " + String(i.file_path || "").split(/[\\/]/).pop();
    default: return c.name;
  }
}
