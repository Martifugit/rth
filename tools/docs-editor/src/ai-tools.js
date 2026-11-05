// R'th AI's tools: what the Codex knows about the GDD, offered to whichever AI answers (PLAN: R'th AI's
// tools). One list, served to OpenRouter as function definitions and to Claude Code through the Codex's
// MCP server (src/mcp-server.js, which hands each call to the engine: POST /api/ai/tool).
//
// Claude already reads the repository and edits pages itself; what it lacks is what the Codex works out
// from the files: links in by what they mean ("Dropped by"), query results, the To-do's structure checks,
// spelling, ranked sections, and a way to hand structured things back (a reason for a change, a problem,
// a page's summary). OpenRouter's models have no repository at all, so for them these are the reading.
//
// Every tool runs here, in the engine, on the docs as they are on disk at that moment. Writing tools go
// through the same write() as the AI's other edits, so the turn's before-and-after comparison finds
// them (Show changes, Undo, Chat mode's suggestions).

import fs from "node:fs";
import path from "node:path";
import { execFile } from "node:child_process";
import { buildIndex, linksFor, brokenLinks } from "./links.js";
import { buildGdd, runQuery, checks, labelFor, pageInfo } from "./gdd-index.js";
import { SYSTEM, TEMPLATES, GDD, readSystem, folderPath, fromTemplate, newPageText, inGdd, isMeta } from "./gdd.js";
import { make as makeFront, split as splitFront, slugOf } from "./frontmatter.js";
import { rewriteRefs, retitle } from "./refs.js";
import { makeSectionIndex, sectionsOf, textHash } from "./section-index.js";
import { wordsIn } from "./spell.js";

export const SUMMARIES = "docs/.codex/index"; // a page's summary: docs/.codex/index/<path under docs/gdd>.json
const SECRET = /(^|\/)(\.git(\/|$)|\.dev\.vars$|\.env(\.|$)|[^/]*\.pem$|account\.bin$|openrouter\.bin$|node_modules\/)/i;
export const isGddPage = (p) => typeof p === "string" && /^docs\/gdd\/[^]+\.md$/.test(p) && !p.includes("..");
const nameOfPath = (p) => String(p).split("/").pop().replace(/\.md$/, "").replace(/-/g, " ");
const fileNameOf = (title) => String(title).trim().replace(/\.md$/i, "").normalize("NFKD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/['’]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const cap = (s) => String(s || "").replace(/^./, (c) => c.toUpperCase());

/**
 * repo: the repository; pages(), read(rel), write(rel, text|null): the docs, as the assistant has them.
 * store: the comments (optional). spell: src/spell.js's checker (optional). ownChanged(rel): a Codex file
 * under docs/.codex/ was written (server.js sends it).
 */
export function makeTools({ repo, pages, read, write, store = null, spell = null, ownChanged = () => {} }) {
  const GIT = process.env.CODEX_GIT || "git";

  // ------------------------------------------------------------------- the GDD as the Codex sees it

  // Built from the files for each call: Claude edits pages itself between calls, so a kept copy could be
  // a moment behind (building it is a few milliseconds).
  let built = null;
  async function engine() {
    const files = await pages();
    const list = await Promise.all(files.map(async (p) => ({ path: p, text: (await read(p)) ?? "" })));
    const idx = buildIndex(list);
    idx.system = readSystem(idx.texts.get(SYSTEM) || "");
    idx.gdd = buildGdd(idx.texts);
    built = idx;
    return idx;
  }
  const forget = () => { built = null; };

  // ------------------------------------------------------------------- summaries and the section index

  const summaryFile = (page) => path.join(repo, SUMMARIES, page.slice(GDD.length + 1).replace(/\.md$/, ".json"));
  function summaryOf(page) {
    try { return JSON.parse(fs.readFileSync(summaryFile(page), "utf8")); } catch { return null; }
  }
  const sections = makeSectionIndex({ summaryOf });
  async function freshSections() {
    const files = (await pages()).filter((p) => isGddPage(p) && !p.startsWith(TEMPLATES + "/"));
    sections.keep(files);
    for (const p of files) sections.update(p, await read(p));
    return sections;
  }

  // ------------------------------------------------------------------- the tools

  const T = [];
  const tool = (name, description, properties, required, run, { mode = "read", only = null } = {}) =>
    T.push({ name, description, schema: { type: "object", properties, required }, run, mode, only });
  const str = (description) => ({ type: "string", description });
  const PAGE = str("the page's repository path, e.g. docs/gdd/skills/woodcutting.md");

  tool("search_gdd", "Search the GDD for the sections about something. Ranked, best first: each hit is a section of a page (its heading path and line) with the lines that matched, and the page's summary when it has one.",
    { query: str("what to find, in words"), limit: { type: "integer", description: "how many sections, at most 25 (12 if left out)" } }, ["query"],
    async (a) => {
      const idx = await freshSections();
      const hits = idx.search(String(a.query || ""), { limit: Math.min(25, Math.max(1, Number(a.limit) || 12)) });
      if (!hits.length) return "Nothing in the GDD matches.";
      const seen = new Set();
      return hits.map((h) => {
        const sum = !seen.has(h.path) && summaryOf(h.path)?.summary;
        seen.add(h.path);
        return `${h.path}:${h.line} (${h.heading.join(" › ")})\n  ${h.snippet}${sum ? `\n  Page summary: ${sum}` : ""}`;
      }).join("\n");
    });

  tool("read_page", "Read a GDD page in full, or one section of it.",
    { path: PAGE, section: str("optional: a heading on the page; only that section (with its subsections) is returned") }, ["path"],
    async (a) => {
      if (!isGddPage(a.path)) return "Only GDD pages (docs/gdd/...md) can be read this way.";
      const text = await read(a.path);
      if (text == null) return "There's no page at " + a.path + " (yet).";
      if (!a.section) return text;
      const want = String(a.section).toLowerCase().trim();
      const secs = sectionsOf(text);
      const i = secs.findIndex((s) => s.heading.slice(1).some((x) => x.toLowerCase() === want)) >= 0
        ? secs.findIndex((s) => s.heading.slice(1).some((x) => x.toLowerCase() === want))
        : secs.findIndex((s) => s.heading.slice(1).join(" ").toLowerCase().includes(want));
      if (i < 0) return `No section "${a.section}" on ${a.path}. Its sections: ${secs.slice(1).map((s) => s.heading.slice(1).join(" › ")).join("; ")}`;
      const depth = secs[i].heading.length;
      const parts = [secs[i]];
      for (let j = i + 1; j < secs.length && secs[j].heading.length > depth; j++) parts.push(secs[j]);
      return parts.map((s) => `${"#".repeat(s.heading.length)} ${s.heading[s.heading.length - 1]} (line ${s.line})\n${s.text}`).join("\n\n");
    });

  tool("page_outline", "A GDD page's shape without its text: its frontmatter (type, status, groups, components, based-on), its sections with their lines, the component instances in them (named entries and table rows), and its summary.",
    { path: PAGE }, ["path"],
    async (a) => {
      if (!isGddPage(a.path)) return "Give a GDD page's path.";
      const idx = await engine();
      const text = idx.texts.get(a.path);
      if (text == null) return "There's no page at " + a.path + ".";
      const info = idx.gdd.pages.get(a.path) || pageInfo(a.path, text);
      const fm = info.front || {};
      const lines = [`${info.title || nameOfPath(a.path)} (${a.path})`, `Frontmatter: ${Object.keys(fm).length ? Object.entries(fm).filter(([, v]) => v != null && v !== "" && !(Array.isArray(v) && !v.length)).map(([k, v]) => `${k}: ${Array.isArray(v) ? v.join(", ") : v}`).join("; ") : "none"}`];
      const sum = summaryOf(a.path);
      if (sum?.summary) lines.push(`Summary${sum.hash !== textHash(text) ? " (written before the page last changed)" : ""}: ${sum.summary}`);
      lines.push("Sections:");
      for (const s of info.sections) {
        const named = s.instances.filter((x) => x.name || Object.keys(x.fields).length);
        lines.push(`- ${s.heading} (line ${s.line})${named.length ? ": " + named.slice(0, 30).map((x) => x.name || Object.values(x.fields)[0] || "a row").join(", ") + (named.length > 30 ? `, and ${named.length - 30} more` : "") : ""}`);
      }
      return lines.join("\n");
    });

  tool("links_in", "Which GDD pages link to a page, grouped by what the link means there (as the page's Links tab shows them: \"Dropped by\", \"Found in\", or plain \"Linked from\"), with the section and line each link is in. Also pages that name it without a link.",
    { path: PAGE }, ["path"],
    async (a) => {
      if (!isGddPage(a.path)) return "Give a GDD page's path.";
      const idx = await engine();
      const r = linksFor(idx, a.path);
      const groups = new Map();
      for (const l of r.inbound) {
        const label = labelFor(idx.gdd, idx.system, l.from, l.section) || "Linked from";
        if (!groups.has(label)) groups.set(label, []);
        groups.get(label).push(`  - ${l.title || nameOfPath(l.from)} (${l.from}:${l.line}${l.section ? ", in " + l.section : ""})`);
      }
      const out = [...groups].map(([label, rows]) => `${label}:\n${rows.join("\n")}`);
      if (r.mentions.length) out.push("Named without a link:\n" + r.mentions.map((m) => `  - ${m.title || nameOfPath(m.from)} (${m.from}:${m.line}, ${m.count}×)`).join("\n"));
      return out.length ? out.join("\n") : "No page links to it or names it.";
    });

  tool("links_out", "The links on a GDD page: where each goes, and whether it's broken (a page not written yet is a to-do).",
    { path: PAGE }, ["path"],
    async (a) => {
      if (!isGddPage(a.path)) return "Give a GDD page's path.";
      const idx = await engine();
      if (!idx.texts.has(a.path)) return "There's no page at " + a.path + ".";
      const seen = new Set();
      const rows = linksFor(idx, a.path).outbound.filter((l) => !seen.has(l.to) && seen.add(l.to))
        .map((l) => `- ${l.title || l.text || nameOfPath(l.to)} → ${l.to}${l.broken ? " (not written yet)" : ""}${l.section ? `, from ${l.section}` : ""}`);
      return rows.length ? rows.join("\n") : "It links to nothing.";
    });

  tool("query_gdd", "Run a query over the GDD, in the language of the GDD's generated lists (query blocks). One filter a line, e.g. \"type: skill\", \"status: draft\", \"groups: tools\", \"links-to: fen-rat\", \"components: action\", or a component's field to list instances across pages, e.g. \"action.skill: woodcutting\" (with \"sort: level\"). `this` means the page given as `page`.",
    { query: str("the filters, one a line"), page: str("optional: the page `this` stands for") }, ["query"],
    async (a) => {
      const idx = await engine();
      let r;
      try { r = runQuery(idx.gdd, idx, isGddPage(a.page) ? a.page : null, String(a.query || "")); } catch (e) { return "That query didn't work: " + e.message; }
      if (!r.rows.length) return "Nothing matches.";
      if (r.kind === "pages") return r.rows.slice(0, 80).map((x) => `- ${x.title || nameOfPath(x.path)} (${x.path})${x.type ? `, ${x.type}` : ""}${x.status ? `, ${x.status}` : ""}`).join("\n") + (r.rows.length > 80 ? `\n…and ${r.rows.length - 80} more` : "");
      return `${r.component} instances:\n` + r.rows.slice(0, 80).map((x) => `- ${x.title || nameOfPath(x.path)}${x.name ? ` › ${x.name}` : ""} (${x.path}): ${Object.entries(x.fields).map(([k, v]) => `${k} ${v}`).join("; ")}`).join("\n") + (r.rows.length > 80 ? `\n…and ${r.rows.length - 80} more` : "");
    });

  tool("gdd_types", "The GDD's page types and components (from How the GDD Works): what each type covers, its folder and headings, and where each component is found. Templates are docs/gdd/templates/<name>.md.",
    {}, [],
    async () => {
      const { system } = await engine();
      return ["Types:", ...system.types.map((t) => `- ${t.type}: ${t.covers || ""} (folder ${folderPath(t)}; headings: ${(t.headings || []).join(", ")})`),
        "Components:", ...system.components.map((c) => `- ${c.name}: on ${(c.foundOn || []).join(", ")}`)].join("\n");
    });

  tool("check_page", "What the Codex finds wrong on a GDD page: its structure (as the To-do list checks it: type, folder, component sections, section order, unknown status...), links that go nowhere, and words its spelling check doesn't know.",
    { path: PAGE }, ["path"],
    async (a) => {
      if (!isGddPage(a.path)) return "Give a GDD page's path.";
      const found = await problemsOn(a.path);
      return found.length ? found.map((p) => `- line ${p.line || "?"}: ${p.text}`).join("\n") : "Nothing wrong found.";
    });

  tool("comments_on", "The open comment threads on a GDD page: who said what, and the passage each is on.",
    { path: PAGE }, ["path"],
    async (a) => {
      if (!store) return "Comments aren't available here.";
      const notes = (await store.load(a.path).catch(() => [])).filter((n) => n.status !== "done");
      if (!notes.length) return "No open comments on it.";
      return notes.slice(0, 20).map((n) => `Thread ${n.id}${n.anchor?.quote ? ` on "${n.anchor.quote.slice(0, 200)}"` : " (on the page)"}:\n` +
        (n.thread || []).map((e) => `  ${e.by === "agent" ? "R'th AI" : e.who?.name || "Someone"}: ${String(e.text).slice(0, 600)}`).join("\n")).join("\n\n");
    });

  // The game's code, for a model without the repository (Claude reads it itself).
  tool("search_code", "Search the game's code and data (everything outside docs/) for text, to check how the game works today. Returns matching lines with file and line number.",
    { query: str("text to find, e.g. a class, a stat or a content id") }, ["query"],
    async (a) => {
      const q = String(a.query || "").trim();
      if (!q) return "Give some text to search for.";
      const got = await new Promise((res) => execFile(GIT, ["grep", "-n", "-I", "-i", "-F", "--max-count=4", "-e", q, "--", ".", ":(exclude)docs", ":(exclude)tools/docs-editor", ":(exclude)*.unity", ":(exclude)*.meta", ":(exclude)*.asset", ":(exclude)*.prefab", ":(exclude)*.mat", ":(exclude)*.rthmap"],
        { cwd: repo, maxBuffer: 8 << 20, windowsHide: true }, (err, out) => res(err && !out ? "" : String(out || ""))));
      const lines = got.split("\n").filter(Boolean);
      if (!lines.length) return fs.existsSync(path.join(repo, "server")) ? "Nothing in the code or data matches." : "This copy of the repository holds only the docs, so there's no code to search here.";
      return lines.slice(0, 40).map((l) => l.slice(0, 240)).join("\n") + (lines.length > 40 ? `\n…and ${lines.length - 40} more lines` : "");
    }, { only: "openrouter" });

  tool("read_file", "Read part of a file of the game's code or data (not a GDD page: use read_page).",
    { path: str("repository path"), from: { type: "integer", description: "first line (1 if left out)" }, lines: { type: "integer", description: "how many lines, at most 300" } }, ["path"],
    async (a) => {
      const rel = path.posix.normalize(String(a.path || "").replace(/\\/g, "/")).replace(/^\.\//, "");
      if (!rel || rel.startsWith("..") || path.isAbsolute(rel) || SECRET.test(rel)) return "That file can't be read.";
      if (rel.startsWith("docs/")) return "Use read_page for GDD pages.";
      const abs = path.join(repo, rel);
      let st; try { st = fs.statSync(abs); } catch { return "There's no file at " + rel + "."; }
      if (!st.isFile() || st.size > 2 << 20) return "That isn't a file that can be read here (too big, or a folder).";
      const text = fs.readFileSync(abs, "utf8");
      if (text.includes("\u0000")) return "That's not a text file.";
      const all = text.split(/\r?\n/), from = Math.max(1, Number(a.from) || 1), n = Math.min(300, Math.max(1, Number(a.lines) || 200));
      return all.slice(from - 1, from - 1 + n).map((l, i) => `${from + i}: ${l}`).join("\n") + (from - 1 + n < all.length ? `\n(${all.length} lines in all)` : "");
    }, { only: "openrouter" });

  // ------------------------------------------------------------------- writing (Claude)

  tool("create_page", "Make a new GDD page the way the Codex's New page does: from its type's template, in its type's folder, with a file name from its title (file names are unique across the GDD). Then fill it in with your own edits.",
    { type: str("its type, from How the GDD Works (see gdd_types)"), title: str("its title, e.g. \"Fen Rat\"") }, ["type", "title"],
    async (a) => {
      const idx = await engine();
      const t = idx.system.types.find((x) => x.type === String(a.type || "").trim().toLowerCase());
      if (!t) return `"${a.type}" isn't a type in How the GDD Works. The types: ${idx.system.types.map((x) => x.type).join(", ")}.`;
      const title = String(a.title || "").trim();
      const name = fileNameOf(title);
      if (!name) return "Give the page a title.";
      const rel = folderPath(t) + "/" + name + ".md";
      const clash = [...idx.texts.keys()].find((p) => inGdd(p) && !p.startsWith(TEMPLATES + "/") && slugOf(p) === name);
      if (clash) return `There's already a page named "${name}": ${clash}. File names are unique across the GDD.`;
      let text = null;
      const tpl = await read(`${TEMPLATES}/${t.type}.md`);
      if (tpl) text = fromTemplate(tpl, title);
      if (text == null) text = newPageText(title, t, makeFront);
      await write(rel, text);
      forget();
      return `Made ${rel} from the ${t.type} template. It reads:\n\n${text}`;
    }, { mode: "write", only: "claude" });

  tool("rename_page", "Rename a GDD page: a new title and file name (in the same folder), with every link, group, based-on and query that points at it rewritten in every page, and its comments moved along. Only in Work mode.",
    { path: PAGE, title: str("the new title") }, ["path", "title"],
    async (a, turn) => {
      if (turn?.mode === "chat") return "Renaming changes many pages at once, so it's only done in Work mode. Suggest the rename in your answer instead; they can switch to Work (Shift+Tab) and ask again.";
      if (!isGddPage(a.path) || isMeta(a.path)) return "Give a GDD page's path (not the start page, a template or the log).";
      const idx = await engine();
      const text = idx.texts.get(a.path);
      if (text == null) return "There's no page at " + a.path + ".";
      const title = String(a.title || "").trim();
      const name = fileNameOf(title);
      if (!name) return "Give the new title.";
      const to = path.posix.dirname(a.path) + "/" + name + ".md";
      if (to !== a.path) {
        const clash = [...idx.texts.keys()].find((p) => p !== a.path && inGdd(p) && !p.startsWith(TEMPLATES + "/") && slugOf(p) === name);
        if (clash) return `"${name}" is already the name of ${clash}: names are unique across the GDD.`;
      }
      await write(to, retitle(rewriteRefs(text, a.path, a.path, to), title));
      const rewritten = [];
      if (to !== a.path) {
        await write(a.path, null);
        for (const [p, t] of idx.texts) {
          if (p === a.path || p === to) continue;
          const next = rewriteRefs(t, p, a.path, to);
          if (next !== t) { await write(p, next); rewritten.push(p); }
        }
        if (store) for (const n of await store.load(a.path).catch(() => [])) await store.move(n.id, to).catch(() => null);
      }
      forget();
      return `Renamed to "${title}" at ${to}.${rewritten.length ? ` Rewrote what points at it in ${rewritten.length} page${rewritten.length === 1 ? "" : "s"}: ${rewritten.join(", ")}.` : ""}`;
    }, { mode: "write", only: "claude" });

  // ------------------------------------------------------------------- handing things back

  tool("explain_change", "Say why you made one change, in a short sentence (at most about 15 words). Call it once for each change you make to a page; the Codex shows it beside that change when they review it.",
    { page: PAGE, quote: str("a few words of the text you wrote (or, for a deletion, removed), exactly as in the page"), reason: str("why, briefly: \"Linked instead of explaining Bile again\"") }, ["page", "quote", "reason"],
    async (a, turn) => {
      if (!turn) return "Noted.";
      turn.reasons.push({ page: String(a.page || ""), quote: String(a.quote || "").slice(0, 300), reason: String(a.reason || "").slice(0, 200) });
      return "Noted.";
    }, { mode: "meta", only: "claude" });

  tool("index_page", "Write or refresh a GDD page's summary for the Codex's search (shared with everyone, so the free models find the right pages too). Call it when you've read a page in depth and page_outline says it has no summary, or one written before the page last changed. The summary: two or three plain sentences on what the page is about and what it decides. Terms: other words people would search for it by (synonyms, related names), not words already in its title.",
    { page: PAGE, summary: str("two or three sentences"), terms: { type: "array", items: { type: "string" }, description: "up to 12 search terms" } }, ["page", "summary"],
    async (a) => {
      if (!isGddPage(a.page)) return "Give a GDD page's path.";
      const text = await read(a.page);
      if (text == null) return "There's no page at " + a.page + ".";
      const rel = SUMMARIES + "/" + a.page.slice(GDD.length + 1).replace(/\.md$/, ".json");
      const data = { page: a.page, hash: textHash(text), summary: String(a.summary || "").trim().slice(0, 600), terms: (Array.isArray(a.terms) ? a.terms : []).map(String).map((s) => s.trim()).filter(Boolean).slice(0, 12), at: new Date().toISOString() };
      fs.mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true });
      fs.writeFileSync(path.join(repo, rel), JSON.stringify(data, null, 1) + "\n", "utf8");
      ownChanged(rel);
      return "Saved.";
    }, { mode: "meta", only: "claude" });

  tool("report_problem", "Report one problem you found in the GDD (the health check). Each is shown to everyone on the To-do page with your plan, ready to fix with R'th AI.",
    {
      kind: { type: "string", enum: ["contradiction", "copied-fact", "settled-question", "missing-link", "stale-reference"], description: "what kind of problem" },
      pages: { type: "array", items: { type: "string" }, description: "the pages it involves (repository paths), the one to change first" },
      quotes: { type: "array", items: { type: "string" }, description: "a few exact words from each page where it is, in the same order as pages" },
      summary: str("one line: what's wrong, e.g. \"Fen Rat says 12 hides, Butchering says 8\""),
      detail: str("a short paragraph: what each page says and why it's a problem"),
      plan: str("the fix, concretely: which page changes and how; written as an instruction R'th AI can carry out"),
    }, ["kind", "pages", "summary", "plan"],
    async (a, turn) => {
      if (!turn?.audit) return "Problems are reported by the health check only.";
      const pagesOf = (Array.isArray(a.pages) ? a.pages : []).filter(isGddPage);
      if (!pagesOf.length) return "Name at least one GDD page.";
      turn.problems.push({ kind: String(a.kind || "contradiction"), pages: pagesOf, quotes: (Array.isArray(a.quotes) ? a.quotes : []).map((q) => String(q).slice(0, 300)), summary: String(a.summary || "").slice(0, 200), detail: String(a.detail || "").slice(0, 1500), plan: String(a.plan || "").slice(0, 1500) });
      return "Reported.";
    }, { mode: "meta", only: "claude" });

  // ------------------------------------------------------------------- what's wrong on a page

  /** Structure, broken links and spelling on one page: [{ kind, line, text }] (check_page, turn checks). */
  async function problemsOn(page, { idx = null } = {}) {
    idx = idx || (await engine());
    const text = idx.texts.get(page);
    if (text == null) return [];
    const out = [];
    for (const p of checks(idx.gdd, idx.system, idx.texts)) if (p.path === page) out.push({ kind: p.kind, line: p.line || null, text: p.text, component: p.component || null });
    const exists = (rel) => fs.existsSync(path.join(repo, rel));
    for (const l of brokenLinks(idx, exists)) if (l.from === page) out.push({ kind: l.kind === "heading" ? "broken-heading" : "missing-page", line: l.line, text: l.kind === "heading" ? `The link "${l.text}" points at a heading that isn't on ${l.to}.` : `The link "${l.text}" goes to ${l.to}, which isn't written yet.`, to: l.to, href: l.href });
    if (spell) {
      const words = wordsIn(text);
      const wrong = new Set(await spell.check([...new Set(words.map((w) => w.word))]).catch(() => []));
      const seen = new Set();
      for (const w of words) if (wrong.has(w.word) && !seen.has(w.word)) { seen.add(w.word); out.push({ kind: "spelling", line: w.line, text: `"${w.word}" isn't a word the spelling check knows.`, word: w.word }); }
    }
    return out;
  }

  // ------------------------------------------------------------------- serving them

  const forProvider = (provider) => T.filter((t) => !t.only || t.only === provider);

  /** The tools for a provider, as OpenAI-style function definitions (OpenRouter). */
  function functions(provider = "openrouter") {
    return forProvider(provider).filter((t) => t.mode === "read" || provider !== "openrouter").map((t) => ({ type: "function", function: { name: t.name, description: t.description, parameters: t.schema } }));
  }
  /** The tools for MCP's tools/list (Claude). The health check's report_problem only in audit runs. */
  function mcpList(turn) {
    return forProvider("claude").filter((t) => t.name !== "report_problem" || turn?.audit).filter((t) => t.mode !== "write" || !turn?.readOnly)
      .map((t) => ({ name: t.name, description: t.description, inputSchema: t.schema }));
  }
  /** Run one. turn: { mode, reasons, problems, audit, readOnly, provider } or null. Always text. */
  async function run(name, args = {}, turn = null) {
    const t = T.find((x) => x.name === name) || (name === "links_to" ? T.find((x) => x.name === "links_in") : null); // links_to: the old name
    if (!t) return "No such tool.";
    if (turn?.provider && t.only && t.only !== turn.provider) return "That tool isn't available here.";
    if (t.mode === "write" && turn?.readOnly) return "This run can't change pages.";
    try { return String(await t.run(args || {}, turn)); } catch (e) { return "That didn't work: " + e.message; }
  }
  function describe(name, a = {}) {
    const n = (p) => nameOfPath(String(p || "a page"));
    switch (name) {
      case "search_gdd": return `Searching the GDD for “${String(a.query || "").slice(0, 40)}”`;
      case "read_page": return a.section ? `Reading ${n(a.path)} › ${a.section}` : `Reading ${n(a.path)}`;
      case "page_outline": return `Looking over ${n(a.path)}`;
      case "links_in": case "links_to": return `Seeing what links to ${n(a.path)}`;
      case "links_out": return `Following ${n(a.path)}'s links`;
      case "query_gdd": return "Querying the GDD";
      case "gdd_types": return "Looking up the GDD's types";
      case "check_page": return `Checking ${n(a.path)}`;
      case "comments_on": return `Reading the comments on ${n(a.path)}`;
      case "search_code": return `Searching the game's code for “${String(a.query || "").slice(0, 40)}”`;
      case "read_file": return `Reading ${String(a.path || "a file").split("/").pop()}`;
      case "create_page": return `Making ${cap(a.type)} ${a.title || "a page"}`;
      case "rename_page": return `Renaming ${n(a.path)}`;
      case "explain_change": return "Noting why";
      case "index_page": return `Summing up ${n(a.page)}`;
      case "report_problem": return "Noting a problem";
      default: return "Looking things up";
    }
  }

  return { functions, mcpList, run, describe, problemsOn, engine, forget, summaryOf, freshSections, names: () => T.map((t) => t.name) };
}
