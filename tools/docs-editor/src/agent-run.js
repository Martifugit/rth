// Asking Claude Code about a note, from the Codex.
//
// An "ask" runs read-only (Read, Glob, Grep). A "task" may also edit pages, but only under docs/gdd/
// (permission rules Edit(docs/gdd/**) and Write(docs/gdd/**); in print mode anything not allowed is refused).
// The run streams as JSON lines; its text and what it is doing go to the page as it arrives, and the
// finished answer becomes the agent's reply in the note's thread, with the session id (so a reply to
// it carries on the same conversation) and every edit it made (so the page can show them).

import { spawn, execFile } from "node:child_process";
import path from "node:path";
import fsp from "node:fs/promises";
import { writingBrief, WIKI_REMINDER } from "./writing-brief.js";

const READ_TOOLS = ["Read", "Glob", "Grep"];
const EDIT_TOOLS = ["Edit(docs/gdd/**)", "Write(docs/gdd/**)", "MultiEdit(docs/gdd/**)"];
const NEVER = ["Bash", "WebFetch", "WebSearch", "NotebookEdit", "Agent", "Task"];
const MAX_RUNNING = 2;

export function makeRunner({ repo, store, emit, claude = process.env.CODEX_CLAUDE || "claude" }) {
  const runs = new Map();   // note id -> run
  const queue = [];
  let available = null;     // { ok, version } once checked

  function check() {
    return new Promise((resolve) => {
      execFile(claude, ["--version"], { timeout: 15000, windowsHide: true }, (err, stdout) => {
        available = err ? { ok: false, error: "The claude command wasn't found (set CODEX_CLAUDE to its path)." } : { ok: true, version: stdout.trim() };
        resolve(available);
      });
    });
  }

  const running = () => [...runs.values()].filter((r) => r.state === "running").length;
  const status = () => ({ agent: available, runs: [...runs.values()].map(view) });
  const view = (r) => ({ id: r.id, state: r.state, text: r.text, activity: r.activity, queued: r.state === "queued" ? queue.indexOf(r) + 1 : 0 });
  const send = (r) => emit("run", view(r));

  /** Start (or queue) a run for a note. `prompt` is the owner's latest words. */
  async function start(id, { prompt } = {}) {
    if (!available) await check();
    if (!available.ok) return { error: available.error };
    if (runs.get(id)?.state === "running" || runs.get(id)?.state === "queued") return { error: "Already running." };
    const hit = await store.find(id);
    if (!hit) return { error: "No such note." };
    const r = { id, state: "queued", text: "", activity: "Waiting for another run to finish", proc: null, edits: [], pending: new Map(), session: null, page: hit.page };
    runs.set(id, r);
    queue.push(r);
    send(r);
    pump();
    return { ok: true };
  }

  function pump() {
    while (queue.length && running() < MAX_RUNNING) {
      const r = queue.shift();
      if (r.state !== "queued") continue;
      launch(r).catch((e) => finish(r, { error: e.message }));
    }
    for (const q of queue) send(q);
  }

  async function launch(r) {
    const hit = await store.find(r.id);
    if (!hit) return finish(r, { error: "The note was deleted." });
    const { note, page } = hit;
    const last = [...note.thread].reverse().find((e) => e.run?.session);
    const resume = last ? last.run.session : null;
    const task = note.kind === "task";
    const args = ["-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
      "--allowedTools", ...READ_TOOLS, ...(task ? EDIT_TOOLS : []),
      "--disallowedTools", ...NEVER, ...(task ? [] : ["Edit", "Write", "MultiEdit"])];
    if (resume) args.push("--resume", resume);
    r.state = "running"; r.activity = resume ? "Carrying on the conversation" : "Starting"; send(r);

    const proc = spawn(claude, args, { cwd: repo, windowsHide: true, stdio: ["pipe", "pipe", "pipe"] });
    r.proc = proc;
    const brief = resume ? "" : await writingBrief((rel) => fsp.readFile(path.join(repo, rel), "utf8"));
    proc.stdin.end(resume ? followUp(note, page) : firstPrompt(note, page, brief));
    let buf = "", errText = "", result = null;
    proc.stdout.setEncoding("utf8");
    proc.stdout.on("data", (chunk) => {
      buf += chunk;
      let nl;
      while ((nl = buf.indexOf("\n")) >= 0) {
        const line = buf.slice(0, nl).trim(); buf = buf.slice(nl + 1);
        if (!line) continue;
        let ev; try { ev = JSON.parse(line); } catch { continue; }
        const got = onEvent(r, ev);
        if (got) result = got;
      }
    });
    proc.stderr.setEncoding("utf8");
    proc.stderr.on("data", (c) => { errText += c; });
    proc.on("error", (e) => finish(r, { error: e.message }));
    proc.on("close", (code) => {
      if (r.state !== "running") return;
      if (result && !result.is_error) finish(r, { text: result.result, session: result.session_id || r.session, cost: result.total_cost_usd });
      else finish(r, { error: (result && result.result) || errText.trim().split("\n").slice(-3).join(" ") || `claude exited with ${code}` });
    });
  }

  let throttle = new Map();
  function onEvent(r, ev) {
    if (ev.session_id) r.session = ev.session_id;
    if (ev.type === "stream_event") {
      const d = ev.event?.delta;
      if (ev.event?.type === "content_block_start" && ev.event.content_block?.type === "text" && r.text) r.text += "\n\n";
      if (d?.type === "text_delta") {
        r.text += d.text;
        r.activity = "Writing";
        // At most ~8 updates a second.
        if (!throttle.get(r.id)) { throttle.set(r.id, setTimeout(() => { throttle.delete(r.id); send(r); }, 120)); }
      }
    } else if (ev.type === "assistant") {
      for (const c of ev.message?.content || []) {
        if (c.type !== "tool_use") continue;
        r.activity = describeTool(c);
        const edit = editOf(repo, c);
        if (edit) r.pending.set(c.id, edit); // counted once its result says it worked
        send(r);
      }
    } else if (ev.type === "user") {
      for (const c of ev.message?.content || []) {
        if (c.type !== "tool_result" || !r.pending.has(c.tool_use_id)) continue;
        if (!c.is_error) r.edits.push(r.pending.get(c.tool_use_id));
        r.pending.delete(c.tool_use_id);
      }
    } else if (ev.type === "result") {
      return ev;
    }
    return null;
  }

  async function finish(r, { text, session, cost, error }) {
    if (r.state === "done") return;
    r.state = "done";
    clearTimeout(throttle.get(r.id)); throttle.delete(r.id);
    const run = { session: session || r.session || undefined };
    if (cost) run.cost = Math.round(cost * 10000) / 10000;
    if (r.edits.length) run.edits = r.edits;
    if (error) run.error = error;
    const body = error ? (r.text ? r.text + "\n\n" : "") + "*The run stopped: " + error + "*" : (text || r.text || "(no answer)");
    try { await store.reply(r.id, { by: "agent", text: body, run }); } catch {}
    runs.delete(r.id);
    emit("run", { id: r.id, state: "done", error: error || null, edits: r.edits.length });
    emit("notes", { page: r.page });
    pump();
  }

  function stop(id) {
    const r = runs.get(id);
    if (!r) return false;
    if (r.state === "queued") { r.state = "done"; queue.splice(queue.indexOf(r), 1); runs.delete(id); emit("run", { id, state: "done", error: "stopped" }); return true; }
    try { r.proc?.kill(); } catch {}
    finish(r, { error: "stopped by the owner" });
    return true;
  }

  return { check, start, stop, status };
}

// ----------------------------------------------------------------------------- prompts

function where(note, page) {
  const a = note.anchor || {};
  const lines = [];
  lines.push(page ? `Page: ${page}` : "Page: none (a scratch note, not tied to a page)");
  if (a.section?.length) lines.push(`Section: ${a.section.join(" › ")}`);
  if (a.quote) lines.push(`The passage the note is on:\n> ${a.quote.replace(/\n/g, "\n> ")}`);
  return lines.join("\n");
}

function threadText(note) {
  return note.thread.map((e) => `${e.by === "agent" ? "You (earlier)" : "The owner"}: ${e.text}`).join("\n\n");
}

function firstPrompt(note, page, brief) {
  const task = note.kind === "task";
  return [
    "You are answering a note the owner of R'th (a private RPG; its game design document is docs/gdd) left in Codex, the app the GDD is written in. The note is attached to a place in a page.",
    "",
    where(note, page),
    "",
    threadText(note),
    "",
    task
      ? "This is a task: do it by editing the docs (you may edit and create pages under docs/gdd/ only). Read docs/gdd/how-the-gdd-works.md and the page type's template (docs/gdd/templates/) first, and write as the rules below say. Change only what the task needs. Then reply in a few sentences: what you changed, where, and anything left open."
      : "This is a question: answer it. You can read the repository (docs, code, data) but not change it. Read the page first. Answer briefly and plainly, in the docs' own voice; cite pages as repository paths and say where in them. If the docs don't settle it, say so and what would.",
    "Your reply is shown in a narrow side panel as Markdown: short paragraphs, no big headings.",
    "Write in US English (color, armor, center, -ize), in your reply and in anything you write into a page, as Codex's spelling check does.",
    "",
    brief,
  ].join("\n");
}

function followUp(note, page) {
  const last = note.thread[note.thread.length - 1];
  return [
    `The owner replied on the same note (${page || "scratch"}${note.anchor?.section?.length ? ", " + note.anchor.section.join(" › ") : ""}):`,
    "",
    last.text,
    "",
    note.kind === "task" ? "It is still a task: you may edit files under docs/gdd/ only. Reply with what you changed. " + WIKI_REMINDER : "Answer in the same panel style. You can read but not change files.",
  ].join("\n");
}

// ----------------------------------------------------------------------------- tool calls

function rel(repo, file) {
  if (!file) return null;
  const r = path.relative(repo, path.resolve(repo, file)).split(path.sep).join("/");
  return r.startsWith("..") ? file : r;
}

function describeTool(c) {
  const i = c.input || {};
  const f = (x) => (x ? String(x).split(/[\\/]/).slice(-2).join("/") : "");
  switch (c.name) {
    case "Read": return "Reading " + String(i.file_path || "").split(/[\\/]/).pop();
    case "Grep": return "Searching the docs";
    case "Glob": return "Looking through the files";
    case "Edit": case "MultiEdit": return "Editing " + f(i.file_path);
    case "Write": return "Writing " + f(i.file_path);
    default: return c.name;
  }
}

const CAP = 20000;
function editOf(repo, c) {
  const i = c.input || {};
  const cut = (s) => (s == null ? null : String(s).length > CAP ? String(s).slice(0, CAP) + "\n…" : String(s));
  if (c.name === "Edit") return { file: rel(repo, i.file_path), old: cut(i.old_string), new: cut(i.new_string) };
  if (c.name === "Write") return { file: rel(repo, i.file_path), old: null, new: cut(i.content) };
  if (c.name === "MultiEdit") return { file: rel(repo, i.file_path), old: cut((i.edits || []).map((e) => e.old_string).join("\n⋯\n")), new: cut((i.edits || []).map((e) => e.new_string).join("\n⋯\n")) };
  return null;
}
