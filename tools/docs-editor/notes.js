#!/usr/bin/env node
// The Codex's notes, for agents working in a session (and anyone at a terminal).
//
//   node tools/docs-editor/notes.js list [--open] [--kind note|ask|task] [--page <path>] [--json]
//   node tools/docs-editor/notes.js show <id>
//   node tools/docs-editor/notes.js reply <id> "text" [--done]
//   node tools/docs-editor/notes.js add --page <path> [--section "A > B"] [--quote "..."] [--kind ask] "text"
//   node tools/docs-editor/notes.js done <id>
//
// Notes are the Codex's comments: in the Discord forum (#codex-comments), reached through the helper
// with the owner's agent key (helper/.dev.vars; replies post as Claude), or in docs/.notes/ on a
// computer without that key. A "task" note is a request to an agent, an "ask" a question. Reply when
// done: the Codex shows it within seconds. `add` asks the owner something about a page.

import path from "node:path";
import fs from "node:fs";
import { fileURLToPath } from "node:url";
import { makeStore } from "./src/notes-store.js";
import { makeHelperComments, agentKey } from "./src/helper-comments.js";
import { locate } from "./src/anchor.js";
import { plain } from "./src/links.js";

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const KEY = agentKey(path.dirname(fileURLToPath(import.meta.url)));
const store = KEY && !process.env.CODEX_LOCAL_NOTES ? makeHelperComments({ auth: async () => "Agent " + KEY }) : makeStore(REPO);

const argv = process.argv.slice(2);
const cmd = argv.shift();
const flags = {}, rest = [];
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a.startsWith("--")) {
    const k = a.slice(2);
    if (["open", "done", "json"].includes(k)) flags[k] = true;
    else flags[k] = argv[++i];
  } else rest.push(a);
}

const page = (p) => (p ? p.replace(/\\/g, "/").replace(/^\.\//, "") : null);
const where = (x) => (x.page || "(scratch)") + (x.note.anchor?.section?.length ? "  ›  " + x.note.anchor.section.slice(1).join(" › ") : "");
const short = (s, n = 90) => { s = s.replace(/\s+/g, " ").trim(); return s.length > n ? s.slice(0, n - 1) + "…" : s; };

/** About which line of the page a note's passage is on (the page is Markdown, the quote is plain text). */
function lineOf(x) {
  if (!x.page) return null;
  const file = path.join(REPO, x.page);
  if (!fs.existsSync(file)) return "the page no longer exists";
  const lines = fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n").split("\n");
  const flat = lines.map(plain);
  const text = flat.join("\n");
  const starts = []; let at = 0;
  for (const l of flat) { starts.push(at); at += l.length + 1; }
  const heads = [];
  lines.forEach((l, i) => { const m = l.match(/^(#{1,6})\s+(.*)/); if (m) heads.push({ level: m[1].length, text: plain(m[2]).replace(/\s*#*$/, ""), start: starts[i] }); });
  const sections = [];
  const stack = [];
  heads.forEach((hd, i) => {
    while (stack.length && stack[stack.length - 1].level >= hd.level) stack.pop();
    stack.push(hd);
    let end = text.length;
    for (let j = i + 1; j < heads.length; j++) if (heads[j].level <= hd.level) { end = heads[j].start; break; }
    sections.push({ path: stack.map((s) => s.text), level: hd.level, start: hd.start, end, headingEnd: hd.start + hd.text.length });
  });
  const hit = locate(text, sections, x.note.anchor);
  if (hit.how === "lost") return "its place in the page is lost";
  let line = 0;
  while (line + 1 < starts.length && starts[line + 1] <= hit.start) line++;
  return `line ${line + 1}` + (hit.how === "exact" || hit.how === "page" ? "" : ` (${hit.how} match)`);
}

async function main() {
  if (cmd === "list") {
    let all = await store.all();
    if (flags.open) all = all.filter((x) => x.note.status !== "done");
    if (flags.kind) all = all.filter((x) => x.note.kind === flags.kind);
    if (flags.page) all = all.filter((x) => x.page === page(flags.page));
    all.sort((a, b) => (a.page || "").localeCompare(b.page || "") || a.note.created.localeCompare(b.note.created));
    if (flags.json) return console.log(JSON.stringify(all, null, 2));
    if (!all.length) return console.log("No notes.");
    for (const x of all) {
      const n = x.note;
      console.log(`${n.id}  ${n.kind.padEnd(4)} ${n.status.padEnd(8)} ${where(x)}`);
      if (n.anchor?.quote) console.log(`    on "${short(n.anchor.quote, 70)}"`);
      console.log(`    ${short(n.thread[0]?.text || "")}` + (n.thread.length > 1 ? `  (+${n.thread.length - 1} replies)` : ""));
    }
    return;
  }
  if (cmd === "show") {
    const x = await store.find(rest[0]);
    if (!x) return fail("No such note: " + rest[0]);
    const n = x.note;
    console.log(`${n.id}: ${n.kind === "ask" ? "an ask" : "a " + n.kind}, ${n.status}`);
    console.log(`Page: ${x.page || "(scratch)"}` + (x.page ? `, ${lineOf(x)}` : ""));
    if (n.anchor?.section?.length) console.log(`Section: ${n.anchor.section.join(" › ")}`);
    if (n.anchor?.quote) console.log(`Passage: "${n.anchor.quote}"`);
    console.log("");
    for (const e of n.thread) {
      console.log(`-- ${e.by} (${e.at})`);
      console.log(e.text);
      if (e.run?.edits?.length) console.log(`   [edited: ${[...new Set(e.run.edits.map((d) => d.file))].join(", ")}]`);
      console.log("");
    }
    return;
  }
  if (cmd === "reply") {
    const [id, ...words] = rest;
    const text = words.join(" ");
    if (!id || !text) return fail('Usage: reply <id> "text" [--done]');
    const r = await store.reply(id, { by: "agent", text, status: flags.done ? "done" : undefined });
    if (!r) return fail("No such note: " + id);
    return console.log(`Replied on ${id} (${r.note.status}).`);
  }
  if (cmd === "done") {
    const r = await store.update(rest[0], { status: "done" });
    if (!r) return fail("No such note: " + rest[0]);
    return console.log(`${rest[0]} is done.`);
  }
  if (cmd === "add") {
    const text = rest.join(" ");
    if (!text) return fail('Usage: add --page <path> [--section "A > B"] [--quote "..."] [--kind ask] "text"');
    const p = page(flags.page);
    if (p && !fs.existsSync(path.join(REPO, p))) return fail("No such page: " + p);
    const section = flags.section ? flags.section.split(/\s*[>›]\s*/).filter(Boolean) : [];
    const anchor = { section, quote: flags.quote || "", prefix: "", suffix: "" };
    // A section path may leave out the page title: put it in front if the page has one.
    if (p && section.length) {
      const title = (fs.readFileSync(path.join(REPO, p), "utf8").match(/^#\s+(.+)$/m) || [])[1];
      if (title && plain(title) !== section[0]) anchor.section = [plain(title), ...section];
    }
    const title = p ? plain((fs.readFileSync(path.join(REPO, p), "utf8").match(/^#\s+(.+)$/m) || [])[1] || "") || null : null;
    const made = await store.create(p, { kind: flags.kind || "note", text, anchor, by: "agent", title });
    return console.log(`Added ${made.note.id} on ${p || "(scratch)"}.`);
  }
  console.log(fs.readFileSync(fileURLToPath(import.meta.url), "utf8").split("\n").slice(1, 13).map((l) => l.replace(/^\/\/ ?/, "")).join("\n"));
}

function fail(msg) { console.error(msg); process.exitCode = 1; }
main().catch((e) => fail(e.stack || e.message));
