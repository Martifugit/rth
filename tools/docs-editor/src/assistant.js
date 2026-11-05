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
import { makeTools } from "./ai-tools.js";
import { fileURLToPath } from "node:url";

const MCP_SOURCE = fileURLToPath(new URL("./mcp-server.js", import.meta.url));

const STREAM_EVERY_MS = 120;

/**
 * pages: () => every page's repository path; read(rel): its text or null; write(rel, text): write it
 * back (null deletes) as the Codex saves a page; changed(rels): the AI changed these (sync sends them).
 */
/**
 * linksTo(page): the pages that link to it, [{ path, title }] (the Links tab's index), so the AI knows
 * where a page sits in the GDD before it changes it.
 */
/**
 * spell: the spelling check (src/spell.js), for the tools' page checks. ownChanged(rel): a Codex file under
 * docs/.codex/ was written by a tool (a page's summary), to be sent. engineUrl(): the engine's address, for
 * Claude's MCP server (null: Claude runs without the Codex's tools).
 */
export function makeAssistant({ repo, dir, store, emit, pages, read, write, changed = () => {}, openrouterKey = () => null, linksTo = async () => [], claude = process.env.CODEX_CLAUDE || "claude", spell = null, ownChanged = () => {}, engineUrl = () => null }) {
  // Claude when it's on this computer (it reads the repository itself); otherwise OpenRouter's free
  // models, once signed in (CODEX_AI=openrouter tries OpenRouter even with Claude here).
  // The AI tab's cog (AI setup) can choose one instead, and OpenRouter's model; kept in ai.json beside
  // the conversations.
  const settingsFile = path.join(dir, "..", "ai.json");
  let settings = { use: "auto", model: "", claudeModel: "", claudeEffort: "", mode: "chat" }; // claude*: defaults for /model and /effort; mode: a new conversation's
  try { settings = { ...settings, ...JSON.parse(fs.readFileSync(settingsFile, "utf8")) }; } catch {}
  const models = () => (settings.model ? [settings.model] : OPENROUTER_MODELS);
  const claudeP = claudeProvider({ repo, claude, systemFile: path.join(dir, "..", "ai-instructions.md") });
  const openP = openRouterProvider({ key: openrouterKey, models });
  // The Codex's tools (src/ai-tools.js), and the turns that may use them: a key per turn, handed to
  // Claude's MCP server, so a call is made for the conversation that asked (its mode, where its reasons go).
  const tools = makeTools({ repo, pages, read, write, store, spell, ownChanged });
  const turns = new Map(); // key -> { conv, mode, provider, reasons, problems, audit, readOnly }
  const newTurn = (x) => { const t = { key: crypto.randomBytes(12).toString("hex"), reasons: [], problems: [], provider: "claude", mode: "work", ...x }; turns.set(t.key, t); return t; };
  /** Where Claude's MCP server for a turn is described (--mcp-config), or null without an engine address. */
  function mcpConfig(turn) {
    const url = engineUrl();
    if (!url) return null;
    const folder = path.join(dir, "..", "mcp");
    fs.mkdirSync(folder, { recursive: true });
    // Copied out beside the settings, so the desktop app (whose files are in an archive) can run it with its own Electron as Node.
    const script = path.join(folder, "mcp-server.mjs");
    try { const src = fs.readFileSync(MCP_SOURCE, "utf8"); if (!fs.existsSync(script) || fs.readFileSync(script, "utf8") !== src) fs.writeFileSync(script, src, "utf8"); } catch { return null; }
    const file = path.join(folder, turn.key + ".json");
    const env = { CODEX_URL: url, CODEX_TOKEN: turn.key, ...(process.versions.electron ? { ELECTRON_RUN_AS_NODE: "1" } : {}) };
    fs.writeFileSync(file, JSON.stringify({ mcpServers: { codex: { type: "stdio", command: process.execPath, args: [script], env } } }, null, 1), "utf8");
    return file;
  }
  const endTurn = (t) => { turns.delete(t.key); try { fs.rmSync(path.join(dir, "..", "mcp", t.key + ".json"), { force: true }); } catch {} };
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
      mode: settings.mode || "chat",
      provider: await (using).check(),
    };
  }
  async function configure({ use, model, claudeModel, claudeEffort, mode }) {
    if (mode !== undefined && ["chat", "work"].includes(mode)) settings.mode = mode;
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
  async function send({ id, text, context = {}, mode = null }) {
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
    // Chat (changes wait as suggestions) or Work (it edits): the AI tab's mode line, kept by the conversation.
    c.settings = c.settings || {};
    if (mode === "chat" || mode === "work") c.settings.mode = mode;
    const turnMode = c.settings.mode || settings.mode || "chat";
    // A part of one of its own answers they pointed at (select, then Ask): this message only, not kept.
    const quote = context.quote?.text ? { text: String(context.quote.text).slice(0, 4000), msg: Number.isInteger(context.quote.msg) ? context.quote.msg : null } : null;
    c.messages.push({ by: "you", at: now, text, context: ctx.chips, mode: turnMode, ...(quote ? { quote } : {}) });
    c.updated = now;
    // What this conversation was given before (Claude remembers it across turns): only what's new is said.
    const told = new Set(c.told || []);
    const fresh_ = ctx.items.filter((x) => !told.has(x.key));
    c.told = [...told, ...fresh_.map((x) => x.key)];
    await writeConv(c);

    const r = { text: "", activity: "Reading", handle: null, timer: null };
    runs.set(c.id, r);
    const before = await snapshot();
    // Chat mode: the AI edits as usual, then the pages go back as they were and its edits wait as
    // suggestions, accepted or skipped one by one in the page (Notion's suggested edits, Word's tracked
    // changes). Work mode: its edits stay, with Show changes and Undo.
    const suggest = turnMode === "chat";
    const turn = newTurn({ conv: c.id, mode: turnMode, provider: null });
    const quoted = quote ? `About this part of your earlier answer:\n> ${quote.text.replace(/\n/g, "\n> ")}\n\n` : "";
    const modeNote = suggest ? CHAT_NOTE : WORK_NOTE;
    const push = () => { if (!r.timer) r.timer = setTimeout(() => { r.timer = null; emit("ai", view(c.id)); }, STREAM_EVERY_MS); };
    emit("ai", view(c.id));
    const handlers = {
      onText: (t) => { r.text += t; r.activity = "Writing"; push(); },
      onBreak: () => { if (r.text) r.text += "\n\n"; },
      onActivity: (a) => { r.activity = a; r.thinking = ""; push(); },
      // Its thinking, as it streams: the status line shows its latest whole sentence.
      onThought: (t) => { r.thinking = t === null ? "" : (r.thinking || "") + t; r.activity = thoughtLine(r.thinking) || "Thinking"; push(); },
    };
    const brief = await writingBrief(read);
    const naming = !c.messages.some((m) => m.by === "ai") ? TITLE_NOTE : ""; // its first answer names the conversation
    const set = { ...{ model: settings.claudeModel || "", effort: settings.claudeEffort || "" }, ...(c.settings || {}) };
    if (ai.kind === "openrouter") {
      // One request: the standing instructions, the engine's gathered reading, the conversation so far, then this message.
      r.open = true;
      turn.provider = "openrouter";
      const history = c.messages.slice(0, -1).filter((m) => m.by !== "codex" && !(m.by === "you" && m.text.startsWith("/"))).slice(-12)
        .map((m) => ({ role: m.by === "you" ? "user" : "assistant", content: m.text || "" })).filter((m) => m.content);
      const system = standing(brief, "engine") + "\n\n" + EDIT_FORMAT + "\n\n" + (await reading({ ...c.context, query: text }));
      r.handle = ai.run({ model: c.settings?.openrouterModel || null, tools: tools.functions("openrouter"), useTool: (name, a) => tools.run(name, a, turn), describe: tools.describe, messages: [{ role: "system", content: system }, ...history, { role: "user", content: (ctx.text ? "This conversation is about:\n\n" + ctx.text + "\n\n" : "") + quoted + "They say:\n\n" + text + modeNote + naming }], ...handlers });
    } else {
      turn.provider = "claude";
      const said = fresh_.length ? (c.session ? "Added to this conversation since:\n\n" : "This conversation is about:\n\n") + fresh_.map((x) => x.text).join("\n\n") + "\n\n" : (c.session ? "" : "They have no page open.\n\n");
      const prompt = said + quoted + "They say:\n\n" + text + modeNote + naming;
      const mcp = mcpConfig(turn);
      r.handle = ai.run({ prompt, session: c.session, system: standing(brief, mcp ? "codex" : "repo"), model: set.model, effort: set.effort, mcp, describeTool: tools.describe, ...handlers });
    }
    r.handle.done.then(async (res) => {
      clearTimeout(r.timer);
      let note = "";
      if (r.open) {
        // OpenRouter's edits are blocks in its answer: apply them, and show the answer without them.
        const { edits, text: clean } = editsIn(res.text || r.text || "");
        const { texts, failed } = await applyEdits(edits, read);
        for (const [p, t] of texts) await write(p, t);
        for (const e of edits) if (e.reason && texts.has(e.page)) turn.reasons.push({ page: e.page, quote: firstWords(e.replace || e.find), reason: e.reason.slice(0, 200) });
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
      // Its reasons (explain_change, or an edit block's reason=), each kept with the page it's about.
      for (const x of changes) { const rs = turn.reasons.filter((y) => y.page === x.page); if (rs.length) x.reasons = rs.map(({ quote, reason }) => ({ quote, reason })); }
      endTurn(turn);
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
  async function reading({ page, pages: extra = [], query = "" } = {}) {
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
    add("How to write pages", await writingBrief(read), 24000);
    add("docs/gdd/how-the-gdd-works.md (pages, types and components)", await read("docs/gdd/how-the-gdd-works.md"), 16000);
    const type = pageText?.match(/^---[\s\S]*?^type:\s*([\w-]+)/m)?.[1];
    if (type) add(`The template for its type: ${TEMPLATES}/${type}.md`, await read(`${TEMPLATES}/${type}.md`), 4000);
    // The pages round it, by their summaries (written by Claude, shared: src/ai-tools.js index_page): where
    // it sits, without spending the room on whole pages; read_page gives any of them in full.
    if (page) {
      const near = new Map();
      const ins = await linksTo(page).catch(() => []);
      for (const x of ins) near.set(x.path, "links to it");
      if (pageText) for (const m of pageText.matchAll(/\]\(([^)#\s]+\.md)(?:#[^)]*)?\)/g)) {
        const rel = path.posix.normalize(path.posix.join(path.posix.dirname(page), m[1]));
        if (rel !== page && !rel.startsWith("..") && !near.has(rel)) near.set(rel, "it links to it");
      }
      const lines = [];
      for (const [p, why] of near) {
        const sum = tools.summaryOf(p);
        lines.push(`- ${p} (${why})${sum?.summary ? ": " + sum.summary : ""}`);
        if (lines.length >= 40) break;
      }
      if (lines.length) add(`Pages round ${page}, with their summaries where they have one`, lines.join("\n"), 9000);
    }
    // The sections of the GDD that best match what they asked (src/section-index.js), whole.
    if (query) {
      const idx = await tools.freshSections().catch(() => null);
      const hits = idx ? idx.search(query, { limit: 8 }).filter((h) => h.path !== page) : [];
      for (const h of hits.slice(0, 5)) {
        const text = await tools.run("read_page", { path: h.path, section: h.heading.length > 1 ? h.heading[h.heading.length - 1] : undefined }, null);
        add(`A section that may matter: ${h.path} › ${h.heading.slice(1).join(" › ") || "(top)"}`, text, 6000);
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

  // ----------------------------------------------------------------------------- the Codex's tools

  /** A lookup as OpenRouter's models make it (also the tests' way in): src/ai-tools.js. */
  const lookUp = (name, a = {}) => tools.run(name, a, { provider: "openrouter", reasons: [], problems: [], mode: "work" });
  /** Claude's MCP server asks: the tools for its turn, or one of them run. */
  function toolList({ token }) {
    const t = turns.get(String(token || ""));
    return t ? { tools: tools.mcpList(t) } : { error: "That turn is over." };
  }
  async function tool({ token, name, args }) {
    const t = turns.get(String(token || ""));
    if (!t) return { error: "That turn is over." };
    return { text: await tools.run(String(name || ""), args || {}, t) };
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
    if (cmd === "help" || !cmd) return { text: [
      "**Commands** (this conversation only)",
      "",
      ai === openP ? "- `/model <id>`: an OpenRouter model id" : "- `/model <name>`: fable, opus, sonnet, haiku or a full id",
      ...(ai === openP ? [] : ["- `/effort <level>`: low, medium, high, xhigh or max"]),
      "- Alone, either one shows what's set",
      "",
      "Defaults for new conversations are in AI setup.",
    ].join("\n") };
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

  /**
   * A new page the AI suggested, created as it wrote it (with whatever else it wrote into it that turn).
   * Suggestions are put back as they were, which for a new page means not there, so it can't be reviewed
   * in the page like an edit: it's previewed, then created or skipped.
   */
  async function createSuggested({ conv, msg, page }) {
    const c = await readConv(conv);
    const x = c?.messages[msg]?.changes?.find((y) => y.page === page && y.suggest && y.before === null);
    if (!x || x.after == null) return { error: "That suggestion isn't there any more." };
    if (x.reviewed) return { error: "That suggestion was already handled." };
    if ((await read(page)) !== null) return { error: "There's already a page there." };
    await write(page, x.after);
    x.reviewed = true;
    await writeConv(c);
    changed([page]);
    emit("ai", { id: conv, state: "reviewed", page });
    return { ok: true, page };
  }

  return { list, get, send, stop, remove, changesFor, reviewed, undo, createSuggested, setup, configure, setContext, lookUp, tool, toolList, tools, newTurn, endTurn, mcpConfig, signInClaude: () => claudeP.signIn(), check: provider.check, status: () => ({ running: [...runs.keys()], defaultMode: settings.mode || "chat" }) };
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
function standing(brief, tools) { // tools: "repo" (Claude reads it itself), "codex" (and the Codex's MCP tools), "engine" (OpenRouter's lookups)
  return [
    "You are R'th AI, the design partner built into Codex: the app a small group of friends use to write the game design document (GDD) of R'th, a private multiplayer RPG of their own (it plays like RuneScape 3 and looks like Old School RuneScape, in its own world built around the four humours).",
    "The GDD is docs/gdd. It is the design: what the game should be, written for the friends who design it and for whoever builds it next, people and coding agents alike. docs/gdd/charter.md holds the rules every page follows; docs/gdd/how-the-gdd-works.md the pages, types and components.",
    "Your job is to help them think the design through and write it well: discuss, explain, question and argue; be concrete; say plainly when the GDD doesn't settle something or contradicts itself. Change pages only when they ask for a change; talking about a page never changes it.",
    "",
    "How to work:",
    "- Before you suggest or make any change, understand where it sits. Read the whole page, not just the section; its type's template in docs/gdd/templates/; the pages that section links to; and the pages that link to this one (listed with the context when known). A section only makes sense in its page, and a page only in the GDD around it.",
    "- Then change only what the request needs, in the page's own voice. If the request is unclear, or would contradict another page, say so and ask rather than guess.",
    tools !== "engine"
      ? "- The game's code and data are in the same repository (the rest of it besides docs/). Read them when a question is about how the game works today, and say what the code does and whether the GDD agrees; the GDD is the design, the code only the current build. Don't go to the code for design questions it can't answer. You can edit and create pages under docs/gdd/ only, never anything else."
      : "- You've been given the page, who links to it and the rules below. When you need more, look it up with your tools: search_gdd (ranked sections), read_page (a page or one section), page_outline, links_in (who links to a page, by what the link means), links_out, query_gdd (the GDD's query language), gdd_types, check_page, comments_on; search_code and read_file for the game's code and data when a question is about how the game works today (the GDD is the design, the code only the current build). Each lookup counts against a small daily allowance, so look up what you need and nothing you were given. You change pages only with the edit blocks described below, each with a short reason.",
    ...(tools === "codex" ? [
      "- You also have the Codex's own tools (the codex MCP server). It knows the GDD's structure, so prefer it to grepping the files: search_gdd for the sections about something (ranked), page_outline for a page's frontmatter, sections and entries, links_in for who links to a page and what each link means there (\"Dropped by\", \"Found in\"), links_out, query_gdd to list pages or component entries across the GDD (\"action.skill: woodcutting\"), gdd_types, check_page for what the Codex finds wrong on a page (run it on each page you changed, and fix what you caused), comments_on. Make a new page with create_page (its template, its folder, a unique file name), and rename one with rename_page (every reference follows); edit everything else with your own Edit.",
      "- For every change you make to a page, call explain_change once with a few of the words you wrote and a short reason (at most about 15 words); they see it beside the change when they review it.",
      "- When you've read a page in depth and page_outline shows it has no summary (or one written before the page last changed), call index_page with a two- or three-sentence summary and search terms. It helps everyone's search, the free models' most of all.",
    ] : []),
    "- Each message says its mode. Chat: when they ask for a change you make it as usual and Codex holds it as suggestions for them to accept or skip. Work: your changes go straight in. Either way, change pages only when they ask for a change.",
    "- Write in US English (color, armor, center, -ize), in your replies and in everything you suggest or write into a page, as Codex's spelling check does.",
    "- After editing, say in a sentence or two what you changed and where: Codex shows them each change.",
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
const TITLE_NOTE = "\n\n(This is the conversation's first message. After your answer, on a line of its own, name the conversation in 2 to 6 words, as a page heading would: the topic, not the question, no full stop. Write it as <title>The name</title>. Codex takes that line off your answer and shows it as the conversation's name.)";
/** The status line while it thinks: the latest whole sentence of its thinking, plain and short, or null. */
export function thoughtLine(thinking) {
  const plain = String(thinking || "").replace(/[`*_#>]+/g, "").replace(/\s+/g, " ");
  // Sentences end at . ! or ? before a capital (so "e.g. a" and "v1.2" don't); the last one counts once it's finished.
  const parts = plain.trim().split(/(?<=[.!?])\s+(?=[A-Z"“(])/);
  const whole = /[.!?]$/.test(parts[parts.length - 1]) ? parts : parts.slice(0, -1);
  if (!whole.length) return null;
  let s = whole[whole.length - 1].trim().replace(/[.!?]+$/, "");
  if (s.length > 90) s = s.slice(0, 89).replace(/\s+\S*$/, "") + "…";
  return s || null;
}

export const titleIn = (text) => {
  const m = String(text || "").match(/<title>\s*([^<\n]{1,80}?)\s*<\/title>/i);
  return m ? m[1].replace(/[*_`"“”]/g, "").replace(/[.\s]+$/, "").trim() || null : null;
};
export const withoutTitle = (text) => String(text || "").replace(/\n*\s*<title>[^<]*(<\/title>|$)\s*/gi, "").trimEnd();

// The two modes (the AI tab's line under the box, Shift+Tab).
const CHAT_NOTE = "\n\n(Chat mode: talk it through. When they ask for a change, make it in the pages as you would; Codex holds each one as a suggestion for them to accept or skip, so don't ask whether to go ahead.)";
const WORK_NOTE = "\n\n(Work mode: changes you make go straight into the pages; they can show and undo them.)";
/** The first words of some text, for a reason's quote: enough to find the change again. */
const firstWords = (t) => String(t || "").replace(/\s+/g, " ").trim().slice(0, 120);

// ----------------------------------------------------------------------------- Claude Code

const READ_TOOLS = ["Read", "Glob", "Grep"];
/** Claude Code on its own, as R'th AI: nothing from the computer's or repository's setup (see run()). */
export const ISOLATED = ["--setting-sources", "", "--disable-slash-commands"];
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

  // mcp: the turn's --mcp-config file (the Codex's tools, src/mcp-server.js) or null; describeTool(name,
  // args) says one of its calls in the activity line.
  function run({ prompt, session, system = "", model = "", effort = "", mcp = null, describeTool = null, readOnly = false, onText, onBreak, onActivity, onThought = () => {} }) {
    let proc = null, stopped = false;
    const done = (async () => {
      if (!available) await check();
      if (!available.ok) return { error: available.error };
      // No CLAUDE.md (the repository's is a developer guide for the game's code, the wrong frame for design
      // writing), no settings, hooks, skills or plugins from this computer: --setting-sources "" and
      // --disable-slash-commands. (Not --safe-mode, which also turns off MCP servers, so the Codex's tools
      // would be gone; not --bare, which skips the sign-in.) Sign-in, models and the read tools work as
      // usual, so it can still read the game's code when it needs to. Its instructions are R'th AI's own,
      // given every turn as a file (on the command line they'd pass Windows' length limit).
      const args = ["-p", ...ISOLATED, "--output-format", "stream-json", "--verbose", "--include-partial-messages",
        "--allowedTools", ...READ_TOOLS, ...(readOnly ? [] : EDIT_TOOLS), ...(mcp ? ["mcp__codex"] : []), "--disallowedTools", ...NEVER, ...(readOnly ? ["Edit", "Write", "MultiEdit"] : [])];
      // Only the Codex's own MCP server, never one configured elsewhere on this computer.
      if (mcp) args.push("--mcp-config", mcp, "--strict-mcp-config");
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
              if (ev.event?.type === "content_block_start" && ev.event.content_block?.type === "thinking") onThought(null);
              const d = ev.event?.delta;
              if (d?.type === "text_delta") onText(d.text);
              else if (d?.type === "thinking_delta" && d.thinking) onThought(d.thinking);
            } else if (ev.type === "assistant") {
              for (const c of ev.message?.content || []) if (c.type === "tool_use") onActivity(String(c.name).startsWith("mcp__codex__") && describeTool ? describeTool(c.name.slice(12), c.input || {}) : describe(c));
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
