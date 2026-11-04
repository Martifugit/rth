// The assistant behind the AI tab (PLAN.md, "AI, built in").
//
// A conversation is a list of messages, kept on this computer only (CODEX_DATA, else .local/ beside
// this tool; git-ignored). Each message from the person comes with what the AI tab's chips said it
// may read: the page, the selected passage, the comment thread that's open. The assistant answers in
// streamed text through a provider.
//
// When asked for a change it edits pages under docs/gdd/ itself. Changes are found, not trusted: before a
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
import { writingBrief } from "./writing-brief.js";
import { TEMPLATES } from "./gdd.js";

const STREAM_EVERY_MS = 120;

/**
 * pages: () => every page's repository path; read(rel): its text or null; write(rel, text): write it
 * back (null deletes) as the Codex saves a page; changed(rels): the AI changed these (sync sends them).
 */
/**
 * linksTo(page): the pages that link to it, [{ path, title }] (the Links tab's index), so the AI knows
 * where a page sits in the GDD before it changes it.
 */
export function makeAssistant({ repo, dir, store, emit, pages, read, write, changed = () => {}, openrouterKey = () => null, linksTo = async () => [], claude = process.env.CODEX_CLAUDE || "claude" }) {
  // Claude when it's on this computer (it reads the repository itself); otherwise OpenRouter's free
  // models, once signed in (CODEX_AI=openrouter tries OpenRouter even with Claude here).
  // The AI tab's cog (AI setup) can choose one instead, and OpenRouter's model; kept in ai.json beside
  // the conversations.
  const settingsFile = path.join(dir, "..", "ai.json");
  let settings = { use: "auto", model: "", claudeModel: "", claudeEffort: "" }; // claude*: defaults for /model and /effort
  try { settings = { ...settings, ...JSON.parse(fs.readFileSync(settingsFile, "utf8")) }; } catch {}
  const models = () => (settings.model ? [settings.model] : OPENROUTER_MODELS);
  const claudeP = claudeProvider({ repo, claude, systemFile: path.join(dir, "..", "ai-instructions.md") });
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
      claudeDefaults: { model: settings.claudeModel || "", effort: settings.claudeEffort || "" },
      provider: await (using).check(),
    };
  }
  async function configure({ use, model, claudeModel, claudeEffort }) {
    if (use !== undefined) {
      if (!["auto", "claude", "openrouter"].includes(use)) return { error: "Unknown choice." };
      settings.use = use;
    }
    if (model !== undefined) settings.model = String(model || "").trim().slice(0, 200);
    if (claudeModel !== undefined) settings.claudeModel = /^(fable|opus|sonnet|haiku|claude-[\w.-]+)?$/i.test(claudeModel || "") ? String(claudeModel || "").toLowerCase() : settings.claudeModel;
    if (claudeEffort !== undefined) settings.claudeEffort = /^(low|medium|high|xhigh|max)?$/i.test(claudeEffort || "") ? String(claudeEffort || "").toLowerCase() : settings.claudeEffort;
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
    return r ? { id, state: "running", text: withoutTitle(withoutComments(r.open ? shown(r.text) : r.text)), activity: r.activity } : { id, state: "idle" };
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
    const now = new Date().toISOString();
    const fresh = () => ({ id: "c-" + crypto.randomBytes(5).toString("hex"), title: "New conversation", created: now, updated: now, page: context.page || null, session: null, messages: [], context: pinned(context), settings: {} });
    // /model, /effort, /help: settings for this conversation, as in Claude Code; no AI turn.
    if (text.startsWith("/")) {
      if (!c) c = fresh();
      const said = await command(c, text);
      if (said.error) return said;
      c.messages.push({ by: "you", at: now, text }, { by: "codex", at: now, text: said.text });
      c.updated = now;
      await writeConv(c);
      return { ok: true, id: c.id, command: true };
    }
    const ai = await pick();
    const ready = await ai.check();
    if (!ready.ok) return { error: ready.error, needsSignIn: ready.needsSignIn };
    if (!c) c = fresh();
    if (!c.context) c.context = pinned(context); // a conversation from before context was pinned: pinned now
    if (!c.messages.some((m) => m.by === "you" && !m.text.startsWith("/"))) c.title = titleFrom(text);
    const ctx = await gather(c.context);
    c.messages.push({ by: "you", at: now, text, context: ctx.chips });
    c.updated = now;
    // What this conversation was given before (Claude remembers it across turns): only what's new is said.
    const told = new Set(c.told || []);
    const fresh_ = ctx.items.filter((x) => !told.has(x.key));
    c.told = [...told, ...fresh_.map((x) => x.key)];
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
    const brief = await writingBrief(read);
    const naming = !c.messages.some((m) => m.by === "ai") ? TITLE_NOTE : ""; // its first answer names the conversation
    const set = { ...{ model: settings.claudeModel || "", effort: settings.claudeEffort || "" }, ...(c.settings || {}) };
    if (ai.kind === "openrouter") {
      // One request: the standing instructions, the engine's gathered reading, the conversation so far, then this message.
      r.open = true;
      const history = c.messages.slice(0, -1).filter((m) => m.by !== "codex" && !(m.by === "you" && m.text.startsWith("/"))).slice(-12)
        .map((m) => ({ role: m.by === "you" ? "user" : "assistant", content: m.text || "" })).filter((m) => m.content);
      const system = standing(brief, false) + "\n\n" + EDIT_FORMAT + "\n\n" + (await reading(c.context));
      r.handle = ai.run({ model: c.settings?.openrouterModel || null, messages: [{ role: "system", content: system }, ...history, { role: "user", content: (ctx.text ? "This conversation is about:\n\n" + ctx.text + "\n\n" : "") + "They say:\n\n" + text + (suggest ? SUGGEST_NOTE : "") + naming }], ...handlers });
    } else {
      const said = fresh_.length ? (c.session ? "Added to this conversation since:\n\n" : "This conversation is about:\n\n") + fresh_.map((x) => x.text).join("\n\n") + "\n\n" : (c.session ? "" : "They have no page open.\n\n");
      const prompt = said + "They say:\n\n" + text + (suggest ? SUGGEST_NOTE : "") + naming;
      r.handle = ai.run({ prompt, session: c.session, system: standing(brief, true), model: set.model, effort: set.effort, ...handlers });
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
            if (!/^docs\/gdd\/[^]+\.md$/.test(x.page) || x.page.includes("..")) throw new Error("not a page in the GDD (docs/gdd/)");
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
      let answer = res.error ? r.text : (res.text || r.text || "");
      const named = titleIn(answer);
      if (named) { fresh.title = named; answer = withoutTitle(answer); }
      const msg = { by: "ai", at: new Date().toISOString(), text: answer + note };
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
  async function reading({ page, pages: extra = [] } = {}) {
    const parts = [];
    let room = 90000;
    const add = (title, body, cap) => {
      if (!body || room <= 0) return;
      const t = body.length > Math.min(cap, room) ? body.slice(0, Math.min(cap, room)) + "\n[…cut short]" : body;
      parts.push(`=== ${title} ===\n${t}`);
      room -= t.length;
    };
    let pageText = null;
    if (page) { pageText = await read(page); add(`The page this conversation is about: ${page}`, pageText, 50000); }
    for (const p of extra) add(`A page they added to the conversation: ${p}`, await read(p), 14000);
    if (page) { const ins = await linksTo(page).catch(() => []); if (ins.length) add(`Pages that link to ${page}`, ins.map((x) => `- ${x.title || x.path} (${x.path})`).join("\n"), 4000); }
    add("How to write pages", await writingBrief(read), 24000);
    add("docs/gdd/how-the-gdd-works.md (pages, types and components)", await read("docs/gdd/how-the-gdd-works.md"), 16000);
    const type = pageText?.match(/^---[\s\S]*?^type:\s*([\w-]+)/m)?.[1];
    if (type) add(`The template for its type: ${TEMPLATES}/${type}.md`, await read(`${TEMPLATES}/${type}.md`), 4000);
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
  async function gather({ page, pages: extra = [], selection, thread } = {}) {
    const chips = {}, items = [];
    if (page) {
      chips.page = page;
      const ins = await linksTo(page).catch(() => []);
      items.push({ key: "page:" + page, text: `The page it's about: ${page}. Read all of it before changing any part.` +
        (ins.length ? `\nPages that link to it: ${ins.slice(0, 20).map((x) => x.path).join(", ")}${ins.length > 20 ? ` and ${ins.length - 20} more` : ""}.` : "\nNo other page links to it yet.") });
    }
    if (extra.length) {
      chips.pages = extra;
      for (const p of extra) items.push({ key: "pages:" + p, text: `A page they added to the conversation: ${p}` });
    }
    if (selection?.quote) {
      chips.selection = { quote: String(selection.quote).slice(0, 4000), section: selection.section || [] };
      items.push({ key: "sel:" + chips.selection.quote.slice(0, 200), text: `The passage they pointed at${selection.page ? ` on ${selection.page}` : ""}${selection.section?.length ? ` (in ${selection.section.join(" › ")})` : ""}:\n> ${chips.selection.quote.replace(/\n/g, "\n> ")}` });
    }
    if (thread && store) {
      const hit = await store.find(thread).catch(() => null);
      if (hit) {
        const n = hit.note;
        chips.thread = { id: n.id, quote: n.anchor?.quote?.slice(0, 200) || "" };
        const lines = n.thread.map((e) => `${e.by === "agent" ? "R'th AI (you)" : e.who?.name || "The owner"}: ${e.text}`).join("\n\n");
        items.push({ key: "thread:" + n.id + ":" + n.thread.length, text: `The comment thread they pointed at (thread ID ${n.id})${n.anchor?.quote ? `, on "${n.anchor.quote.slice(0, 300)}"` : ""}:\n${lines}` });
      }
    }
    return { chips, items, text: items.map((x) => x.text).join("\n\n") };
  }

  /** The context a conversation keeps (pinned when it starts): its page, pages added, a passage, a thread. */
  function pinned(x = {}) {
    const isPage = (p) => typeof p === "string" && p.startsWith("docs/") && p.endsWith(".md") && !p.includes("..");
    return {
      page: isPage(x.page) ? x.page : null,
      pages: [...new Set((Array.isArray(x.pages) ? x.pages : []).filter(isPage))].filter((p) => p !== x.page).slice(0, 12),
      selection: x.selection?.quote ? { quote: String(x.selection.quote).slice(0, 4000), section: Array.isArray(x.selection.section) ? x.selection.section.map(String) : [], page: isPage(x.selection.page) ? x.selection.page : isPage(x.page) ? x.page : null } : null,
      thread: typeof x.thread === "string" && x.thread ? x.thread : null,
    };
  }

  /** Change what a conversation keeps: add a page (an @mention, + Add), the page on screen, a passage or a
   *  thread; or drop one ("page", "selection", "thread", or a page's path). */
  async function setContext({ id, add = {}, drop = [] }) {
    const c = await readConv(id);
    if (!c) return { error: "No such conversation." };
    const now = pinned(c.context || {});
    for (const d of Array.isArray(drop) ? drop : []) {
      if (d === "page") now.page = null; else if (d === "selection") now.selection = null; else if (d === "thread") now.thread = null;
      else now.pages = now.pages.filter((p) => p !== d);
    }
    const more = pinned({ ...add, page: null });
    if (add.page && !now.page) now.page = pinned({ page: add.page }).page;
    else if (add.page && add.page !== now.page) more.pages.unshift(...pinned({ pages: [add.page] }).pages);
    now.pages = [...new Set([...now.pages, ...more.pages])].filter((p) => p !== now.page).slice(0, 12);
    if (more.selection) now.selection = more.selection;
    if (add.thread) now.thread = String(add.thread);
    c.context = now;
    await writeConv(c);
    return { ok: true, context: now };
  }

  /** /model, /effort, /help: this conversation's settings, as Claude Code's commands are. */
  async function command(c, text) {
    const [cmd, ...rest] = text.slice(1).trim().split(/\s+/);
    const arg = rest.join(" ").trim();
    const ai = await pick();
    c.settings = c.settings || {};
    const claudeNow = () => `model ${c.settings.model || settings.claudeModel || "Claude Code's default"}, effort ${c.settings.effort || settings.claudeEffort || "Claude Code's default"}`;
    if (cmd === "help" || !cmd) return { text: "Commands for this conversation: **/model** *name* (Claude: fable, opus, sonnet, haiku or a full model id; OpenRouter: a model id), **/effort** *low, medium, high, xhigh or max* (Claude), **/model** or **/effort** alone to see what's set. Defaults for new conversations are in AI setup." };
    if (cmd === "model") {
      if (!arg) return { text: ai === openP ? `This conversation uses ${c.settings.openrouterModel || settings.model || "OpenRouter's free models"}.` : `This conversation uses ${claudeNow()}.` };
      if (ai === openP) { c.settings.openrouterModel = arg.slice(0, 200); return { text: `From now on this conversation uses **${c.settings.openrouterModel}** on OpenRouter.` }; }
      if (!/^(fable|opus|sonnet|haiku|default|claude-[\w.-]+)$/i.test(arg)) return { error: `Unknown model "${arg}". Try fable, opus, sonnet, haiku or a full id like claude-sonnet-5-5.` };
      c.settings.model = /^default$/i.test(arg) ? "" : arg.toLowerCase();
      return { text: `From now on this conversation uses ${claudeNow()}.` };
    }
    if (cmd === "effort") {
      if (ai === openP) return { error: "Effort is a Claude setting; OpenRouter's models don't have it." };
      if (!arg) return { text: `This conversation uses ${claudeNow()}.` };
      if (!/^(low|medium|high|xhigh|max|default)$/i.test(arg)) return { error: `Effort is low, medium, high, xhigh or max (or default), not "${arg}".` };
      c.settings.effort = /^default$/i.test(arg) ? "" : arg.toLowerCase();
      return { text: `From now on this conversation uses ${claudeNow()}.` };
    }
    return { error: `Unknown command /${cmd}. Try /model, /effort or /help.` };
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

  return { list, get, send, stop, remove, changesFor, reviewed, undo, setup, configure, setContext, signInClaude: () => claudeP.signIn(), check: provider.check, status: () => ({ running: [...runs.keys()] }) };
}

const titleFrom = (text) => {
  const t = text.replace(/\s+/g, " ").trim();
  return t.length > 60 ? t.slice(0, 57).replace(/\s+\S*$/, "") + "…" : t;
};

// ----------------------------------------------------------------------------- prompts


/**
 * R'th AI's standing instructions: who it is, what the GDD is for, how to work before changing anything,
 * then the writing rules (src/writing-brief.js: the Charter and Writing a Page) and how to leave comments.
 * Given on every turn (Claude: --append-system-prompt-file; OpenRouter: the system message), so they don't
 * fade over a long conversation. tools: it can read the repository itself (Claude).
 */
function standing(brief, tools) {
  return [
    "You are R'th AI, the design partner built into the Codex: the app a small group of friends use to write the game design document (GDD) of R'th, a private multiplayer RPG of their own (it plays like RuneScape 3 and looks like Old School RuneScape, in its own world built around the four humours).",
    "The GDD is docs/gdd. It is the design: what the game should be, written for the friends who design it and for whoever builds it next, people and coding agents alike. docs/gdd/charter.md holds the rules every page follows; docs/gdd/how-the-gdd-works.md the pages, types and components.",
    "Your job is to help them think the design through and write it well: discuss, explain, question and argue; be concrete; say plainly when the GDD doesn't settle something or contradicts itself. Change pages only when they ask for a change; talking about a page never changes it.",
    "",
    "How to work:",
    "- Before you suggest or make any change, understand where it sits. Read the whole page, not just the section; its type's template in docs/gdd/templates/; the pages that section links to; and the pages that link to this one (listed with the context when known). A section only makes sense in its page, and a page only in the GDD around it.",
    "- Then change only what the request needs, in the page's own voice. If the request is unclear, or would contradict another page, say so and ask rather than guess.",
    tools
      ? "- The game's code and data are in the same repository (the rest of it besides docs/). Read them when a question is about how the game works today, and say what the code does and whether the GDD agrees; the GDD is the design, the code only the current build. Don't go to the code for design questions it can't answer. You can edit and create pages under docs/gdd/ only, never anything else."
      : "- You only have the pages given below; say so when you'd need one you haven't been given. You can edit and create pages under docs/gdd/ only.",
    "- After editing, say in a sentence or two what you changed and where: the Codex shows them each change.",
    "- Your reply shows in a narrow side panel as Markdown: short paragraphs, lists where they help, no big headings. Name pages by their repository paths.",
    "- The conversation keeps its context: the page it's about, pages they add (with @ or + Add page), a passage or a comment thread they point at. What they're looking at elsewhere while you work isn't part of it.",
    "",
    brief,
    "",
    COMMENT_FORMAT,
  ].join("\n");
}

// The conversation's name: its first answer ends with a hidden <title> line (2-6 words, the topic, not
// the question), taken off the answer by the Codex. Without one, the question names it, as before.
const TITLE_NOTE = "\n\n(This is the conversation's first message. After your answer, on a line of its own, name the conversation in 2 to 6 words, as a page heading would: the topic, not the question, no full stop. Write it as <title>The name</title>. The Codex takes that line off your answer and shows it as the conversation's name.)";
export const titleIn = (text) => {
  const m = String(text || "").match(/<title>\s*([^<\n]{1,80}?)\s*<\/title>/i);
  return m ? m[1].replace(/[*_`"“”]/g, "").replace(/[.\s]+$/, "").trim() || null : null;
};
export const withoutTitle = (text) => String(text || "").replace(/\n*\s*<title>[^<]*(<\/title>|$)\s*/gi, "").trimEnd();

const SUGGEST_NOTE ="\n\n(They asked for suggestions: make the edits in the pages as you would; the Codex shows each one as a suggestion for them to accept or skip.)";

// ----------------------------------------------------------------------------- Claude Code

const READ_TOOLS = ["Read", "Glob", "Grep"];
// Pages only: anything not allowed is refused in print mode.
const EDIT_TOOLS = ["Edit(docs/gdd/**)", "Write(docs/gdd/**)", "MultiEdit(docs/gdd/**)"];
// Comments go through the Codex (COMMENT_FORMAT), never as files: docs/.notes/ is only a copy, and
// what's shown comes from Discord when signed in.
const NEVER = ["Bash", "NotebookEdit", "WebFetch", "WebSearch", "Agent", "Task", "Edit(docs/.notes/**)", "Write(docs/.notes/**)", "MultiEdit(docs/.notes/**)"];

function claudeProvider({ repo, claude, systemFile }) {
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

  function run({ prompt, session, system = "", model = "", effort = "", onText, onBreak, onActivity }) {
    let proc = null, stopped = false;
    const done = (async () => {
      if (!available) await check();
      if (!available.ok) return { error: available.error };
      // --safe-mode: no CLAUDE.md (the repository's is a developer guide for the game's code, the wrong frame
      // for design writing), no hooks, skills or plugins; sign-in, models and the read tools work as usual,
      // so it can still read the game's code when it needs to. Its instructions are R'th AI's own, given
      // every turn as a file (on the command line they'd pass Windows' length limit).
      const args = ["-p", "--safe-mode", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
        "--allowedTools", ...READ_TOOLS, ...EDIT_TOOLS, "--disallowedTools", ...NEVER];
      if (system && systemFile) { fs.mkdirSync(path.dirname(systemFile), { recursive: true }); fs.writeFileSync(systemFile, system, "utf8"); args.push("--append-system-prompt-file", systemFile); }
      if (model) args.push("--model", model);
      if (effort) args.push("--effort", effort);
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
