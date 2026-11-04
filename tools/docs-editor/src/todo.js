// The To-do: what the GDD still needs, as cards a person can act on where they read them. Built from
// the link index, the GDD's index and its checks (server.js keeps those fresh). Each card says what's
// wrong, where, how much it touches, and which fixes apply; the fixes themselves are done by the server
// (POST /api/fix), through the same reference machinery as renaming a page (src/refs.js).
//
// Kinds of card:
// - missing:     a page that's referred to (by links, groups or based-on) but not written. One card per
//                missing page, however many pages refer to it, with likely matches ("did you mean").
// - reference:   a reference that's wrong in another way: a #heading that isn't there, a link out of the
//                GDD, a field that names a page in plain text instead of linking it.
// - placeholder: a p- name still waiting for its real name, wherever it's used.
// - structure:   a page whose frontmatter and sections disagree, or are out of order.

import { linksOf, plain } from "./links.js";
import { slugOf } from "./frontmatter.js";
import { TEMPLATES, isMeta, inGdd, nameOf, folderPath } from "./gdd.js";

const words = (id) => String(id).replace(/-/g, " ").replace(/^./, (c) => c.toUpperCase());

/** How alike two names are, 0..1 (1 - edit distance over length). */
export function likeness(a, b) {
  a = String(a).toLowerCase(); b = String(b).toLowerCase();
  if (a === b) return 1;
  const m = a.length, n = b.length;
  if (!m || !n) return 0;
  let prev = Array.from({ length: n + 1 }, (_, j) => j);
  for (let i = 1; i <= m; i++) {
    const cur = [i];
    for (let j = 1; j <= n; j++) cur[j] = Math.min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
    prev = cur;
  }
  return 1 - prev[n] / Math.max(m, n);
}

/**
 * The To-do. `idx`: the link index (texts, titles, files, in); `gdd`: buildGdd's { pages, bySlug };
 * `system`: How the GDD Works; `problems`: checks(); `renames`: Map(old path → new path) from git;
 * `exists(rel)`: whether a path is on disk (folders, images); `headings`: brokenLinks() of kind "heading".
 */
export function buildTodo({ idx, gdd, system, problems, headings = [], renames = new Map(), exists = () => false }) {
  const pages = [...gdd.pages.values()].filter((p) => !isMeta(p.path));
  const written = new Set(idx.files);
  const missing = new Map(); // id → card
  const card = (id, path) => {
    if (!missing.has(id)) missing.set(id, { kind: "missing", id, title: words(id), path, refs: [], pages: 0, suggestions: [] });
    const c = missing.get(id);
    if (path && !c.path) c.path = path;
    return c;
  };
  const references = [];

  // Links: to a page not written, to a heading that isn't there, or out of the GDD.
  for (const [from, text] of idx.texts) {
    if (!inGdd(from) || from.startsWith(TEMPLATES + "/")) continue;
    for (const l of linksOf(from, text, written)) {
      if (written.has(l.to)) continue;
      if (!l.to.endsWith(".md") || exists(l.to)) continue;
      if (inGdd(l.to)) {
        const c = card(slugOf(l.to), l.to);
        if (c.title === words(c.id) && l.text) c.title = l.text.replace(/^./, (x) => x.toUpperCase());
        c.refs.push({ from, fromTitle: idx.titles.get(from), kind: "link", line: l.line, text: l.text, context: l.context, to: l.to });
      } else {
        references.push({ kind: "reference", problem: "outside", from, fromTitle: idx.titles.get(from), line: l.line, text: l.text, context: l.context, to: l.to });
      }
    }
  }
  // Groups and based-on that name no page: a page to write too, in the group folder or beside the variant.
  const groupType = system.types.find((t) => t.type === "group");
  for (const p of pages) {
    const fm = p.front || {};
    for (const g of fm.groups || []) if (!gdd.bySlug.has(g)) card(g, groupType ? folderPath(groupType) + "/" + g + ".md" : null).refs.push({ from: p.path, fromTitle: p.title, kind: "group" });
    const b = fm["based-on"];
    if (b && !gdd.bySlug.has(b)) card(b, p.path.slice(0, p.path.lastIndexOf("/") + 1) + b + ".md").refs.push({ from: p.path, fromTitle: p.title, kind: "variant" });
  }
  // Likely matches: a page git saw this one renamed to, then pages with a similar name.
  const all = pages.map((p) => ({ path: p.path, title: p.title || words(p.slug), id: p.slug }));
  for (const c of missing.values()) {
    c.pages = new Set(c.refs.map((r) => r.from)).size;
    let moved = c.path;
    for (let i = 0; moved && renames.has(moved) && i < 10; i++) moved = renames.get(moved);
    if (moved && moved !== c.path && written.has(moved)) c.suggestions.push({ path: moved, title: idx.titles.get(moved) || slugOf(moved), why: "renamed to this" });
    const scored = all.map((p) => ({ ...p, score: Math.max(likeness(p.id, c.id), likeness(p.title, c.title)) }))
      .filter((p) => p.score >= 0.6 && !c.suggestions.some((s) => s.path === p.path) && !c.refs.some((r) => r.from === p.path)) // a page that refers to it isn't it.sort((a, b) => b.score - a.score).slice(0, 3);
    for (const p of scored) c.suggestions.push({ path: p.path, title: p.title, why: "similar name" });
  }

  // Links to a heading that isn't there (links.js, brokenLinks, kind "heading").
  for (const l of headings) {
    if (!inGdd(l.from) || l.from.startsWith(TEMPLATES + "/")) continue;
    references.push({ kind: "reference", problem: "heading", from: l.from, fromTitle: l.title, line: l.line, text: l.text, to: l.to, anchor: l.href.split("#").pop(), context: l.section || "" });
  }

  // Fields that name a page in plain text: lists match them by title, which breaks the day it's renamed.
  const byName = new Map();
  for (const p of all) { byName.set(p.title.toLowerCase(), p); byName.set(p.id, p); }
  const compNames = new Set(system.components.map((c) => c.name));
  for (const p of pages) {
    const text = idx.texts.get(p.path) || "";
    let sec = null, fence = null;
    text.split("\n").forEach((line, i) => {
      const f = line.match(/^\s*(`{3,}|~{3,})/);
      if (fence) { if (f && f[1][0] === fence[0]) fence = null; return; }
      if (f) { fence = f[1]; return; }
      const hd = line.match(/^##\s+(.*?)\s*$/);
      if (hd) { sec = nameOf(plain(hd[1])); return; }
      if (!sec || !compNames.has(sec)) return;
      const field = line.match(/^\s*[-*]\s+(?:\*\*)?([^:*]+?)(?:\*\*)?\s*:\s*(.*)$/);
      const values = field ? [[field[1].trim(), field[2]]] : /^\s*\|/.test(line) && !/^\s*\|[\s|:-]+\|\s*$/.test(line)
        ? line.trim().replace(/^\||\|$/g, "").split("|").map((c) => [null, c]) : [];
      for (const [name, value] of values) {
        for (const part of value.replace(/\[[^\]]*\]\([^)]*\)/g, "").split(/[,;]/).map((x) => x.replace(/[*_`]/g, "").trim()).filter(Boolean)) {
          const hit = byName.get(part.toLowerCase());
          if (!hit || hit.path === p.path) continue;
          references.push({ kind: "reference", problem: "plain", from: p.path, fromTitle: p.title, line: i + 1, field: name, text: part, to: hit.path, toTitle: hit.title, context: plain(line).slice(0, 200) });
        }
      }
    });
  }

  // Placeholders, by name.
  const holders = new Map();
  for (const x of problems.filter((x) => x.kind === "placeholder")) {
    const token = x.text.split(" ")[0];
    if (!holders.has(token)) holders.set(token, { kind: "placeholder", token, refs: [], page: gdd.bySlug.get(token)?.[0] || null });
    holders.get(token).refs.push({ from: x.path, fromTitle: x.title, line: x.line });
  }
  for (const h of holders.values()) h.pages = new Set(h.refs.map((r) => r.from)).size;

  // Structure: each problem with the fixes that apply to it.
  const FIX = {
    "undeclared-section": "declare", "missing-section": "add-section", "unknown-component": "undeclare",
    "wrong-folder": "move-folder", "section-order": "reorder",
  };
  const skip = new Set(["placeholder", "missing-group", "missing-based-on"]);
  // A page in the wrong folder: where its type's pages go.
  const home = (p) => { const t = system.types.find((x) => x.type === gdd.pages.get(p)?.front?.type); return t ? folderPath(t) + "/" + p.split("/").pop() : null; };
  const structure = problems.filter((x) => !skip.has(x.kind)).map((x) => ({ kind: "structure", problem: x.kind, from: x.path, fromTitle: x.title, line: x.line, text: x.text, component: x.component || null, fix: FIX[x.kind] || null, ...(x.kind === "wrong-folder" ? { to: home(x.path) } : {}) }));

  const missingCards = [...missing.values()].sort((a, b) => b.pages - a.pages || a.title.localeCompare(b.title));
  return {
    missing: missingCards,
    references,
    placeholders: [...holders.values()].sort((a, b) => b.pages - a.pages),
    structure,
    count: missingCards.length + references.length + holders.size + structure.length,
  };
}

/** Pages renamed in git, from `git log --name-status` output: Map(old path → new path), the latest first wins. */
export function renamesFromLog(out) {
  const map = new Map();
  for (const line of String(out || "").split("\n")) {
    const m = line.match(/^R\d*\t(.+?)\t(.+)$/);
    if (m && inGdd(m[1]) && !map.has(m[1])) map.set(m[1], m[2]);
  }
  return map;
}

