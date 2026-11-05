// The benchmark's cases (bench/ai-bench.mjs): questions answered in Chat mode, checked against what the
// Codex works out from the same docs, and tasks done in Work mode, checked in the pages they leave.
// check({ answer, repo, read, pages, tools, changes }) → { pass, notes }.

import fs from "node:fs";
import path from "node:path";
import { split } from "../src/frontmatter.js";
import { checks } from "../src/gdd-index.js";

const has = (answer, word) => answer.toLowerCase().replace(/[*_`]/g, "").includes(String(word).toLowerCase());
/** Every one of these must be named in the answer: a word, or a page { title, path } by its title or its
 *  path (R'th AI names pages by their paths). */
function names(answer, list) {
  const named = (w) => (typeof w === "string" ? has(answer, w) : has(answer, w.title) || has(answer, w.path) || has(answer, w.path.replace(/^docs\/gdd\//, "")));
  const missing = list.filter((w) => !named(w)).map((w) => (typeof w === "string" ? w : w.title));
  return { pass: !missing.length, notes: missing.length ? ["missing: " + missing.join(", ")] : [] };
}
const titleWord = (t) => String(t || "").replace(/^p-/i, "").replace(/-/g, " ");
async function inbound(tools, page) {
  const idx = await tools.engine();
  return (idx.in.get(page) || []).map((l) => ({ page: { title: idx.titles.get(l.from), path: l.from }, section: (l.section || "").split(" › ").pop() }));
}

export const CASES = [
  // ----------------------------------------------------------------------------- questions (Chat)
  {
    id: "q-pennick", page: "docs/gdd/entities/pennick.md",
    ask: "Which pages link to this page, and from which section of each? Just the list.",
    async check({ answer, tools }) {
      const ins = await inbound(tools, "docs/gdd/entities/pennick.md");
      return names(answer, ins.flatMap((x) => [x.page, x.section]));
    },
  },
  {
    id: "q-gall",
    ask: "Which pages in the GDD link to the Gall location page? Just the list.",
    async check({ answer, tools }) {
      const ins = await inbound(tools, "docs/gdd/locations/gall.md");
      return names(answer, [...new Map(ins.map((x) => [x.page.path, x.page])).values()]);
    },
  },
  {
    id: "q-trees",
    ask: "Which pages are members of the Trees group (they name it in their groups)? Just the list.",
    async check({ answer, tools }) {
      const idx = await tools.engine();
      const members = [...idx.gdd.pages.values()].filter((p) => (p.front?.groups || []).includes("trees")).map((p) => ({ title: titleWord(p.title).split(" ")[0], path: p.path }));
      return names(answer, members);
    },
  },
  {
    id: "q-placeholders",
    ask: "Which GDD pages still use p- placeholder names somewhere in their text? Just the list of pages.",
    async check({ answer, tools }) {
      const idx = await tools.engine();
      const ps = [...new Set(checks(idx.gdd, idx.system, idx.texts).filter((x) => x.kind === "placeholder").map((x) => x.path))];
      return names(answer, ps.map((p) => ({ title: titleWord(idx.titles.get(p)), path: p })));
    },
  },
  {
    id: "q-stubs",
    ask: "Which skill pages are still at status stub? Just the list.",
    async check({ answer, tools }) {
      const idx = await tools.engine();
      const stubs = [...idx.gdd.pages.values()].filter((p) => p.front?.type === "skill" && p.front?.status === "stub").map((p) => ({ title: p.title, path: p.path }));
      const r = names(answer, stubs);
      const others = [...idx.gdd.pages.values()].filter((p) => p.front?.type === "skill" && p.front?.status !== "stub").filter((p) => new RegExp(`(^|[\\s/\`*(])(${p.title}|${p.path.split("/").pop()})`, "i").test(answer.split(/\n/).filter((l) => /^\s*[-*\d]/.test(l)).join("\n"))).map((p) => p.title);
      if (others.length) { r.pass = false; r.notes.push("not stubs but listed: " + others.join(", ")); }
      return r;
    },
  },
  {
    id: "q-components",
    ask: "Which GDD pages declare components in their frontmatter, and which components does each declare? Just the list.",
    async check({ answer, tools }) {
      const idx = await tools.engine();
      const pages = [...idx.gdd.pages.values()].filter((p) => (p.front?.components || []).length);
      return names(answer, pages.flatMap((p) => [{ title: p.title, path: p.path }, ...p.front.components]));
    },
  },
  {
    id: "q-bile", page: "docs/gdd/systems/bile.md",
    ask: "Which pages link to this page, and from which section? Just the list.",
    async check({ answer, tools }) {
      const ins = await inbound(tools, "docs/gdd/systems/bile.md");
      return names(answer, ins.flatMap((x) => [x.page, x.section]));
    },
  },

  // Harder: what the Codex works out (links not written yet, structure against the type, groups).
  {
    id: "q-broken", page: "docs/gdd/activities/wildfires.md",
    ask: "Which links on this page go to pages that aren't written yet? Just the list.",
    async check({ answer, tools }) {
      const idx = await tools.engine();
      const missing = [...new Set((idx.out.get("docs/gdd/activities/wildfires.md") || []).map((l) => l.to).filter((to) => to.startsWith("docs/") && !idx.files.has(to)))];
      return names(answer, missing.map((to) => ({ title: titleWord(to.split("/").pop().replace(/\.md$/, "")), path: to })));
    },
  },
  {
    id: "q-structure", page: "docs/gdd/activities/distillation.md",
    ask: "Is anything wrong with this page's structure, as How the GDD Works sets it out? Be brief.",
    // Its declared component's section taken out first.
    async setup(repo) {
      const f = path.join(repo, "docs/gdd/activities/distillation.md");
      const t = fs.readFileSync(f, "utf8").replace(/\r\n/g, "\n");
      const at = t.indexOf("## Tunables");
      const next = t.indexOf("\n## ", at + 5);
      fs.writeFileSync(f, t.slice(0, at) + (next > 0 ? t.slice(next + 1) : ""));
    },
    async check({ answer }) {
      const ok = /tunables/i.test(answer) && /(missing|no |lacks|without|gone|removed|isn't there|doesn't have|has no)/i.test(answer);
      return { pass: ok, notes: ok ? [] : ["didn't say the Tunables section is missing"] };
    },
  },
  {
    id: "q-empty-groups",
    ask: "Which group pages have no members, that is, no page lists that group in its frontmatter groups? Just the list.",
    async check({ answer, tools }) {
      const idx = await tools.engine();
      const pages = [...idx.gdd.pages.values()];
      const used = new Set(pages.flatMap((p) => p.front?.groups || []));
      const empty = pages.filter((p) => p.front?.type === "group" && !used.has(p.slug)).map((p) => ({ title: p.title, path: p.path }));
      const r = names(answer, empty);
      const wrong = pages.filter((p) => p.front?.type === "group" && used.has(p.slug)).filter((p) => answer.split("\n").some((l) => /^\s*[-*\d]/.test(l) && (l.toLowerCase().includes(p.path.split("/").pop()) || l.toLowerCase().includes(String(p.title).toLowerCase())))).map((p) => p.title);
      if (wrong.length) { r.pass = false; r.notes.push("has members but listed: " + wrong.join(", ")); }
      return r;
    },
  },

  // ----------------------------------------------------------------------------- tasks (Work)
  {
    id: "t-component", page: "docs/gdd/items/rope.md", mode: "work",
    ask: "Add the equippable component to this page.",
    async check({ read, tools }) {
      const text = await read("docs/gdd/items/rope.md");
      const notes = [];
      if (!(split(text).data?.components || []).includes("equippable")) notes.push("not in the frontmatter's components");
      if (!/^## Equippable\s*$/m.test(text)) notes.push("no Equippable section");
      const bad = (await tools.problemsOn("docs/gdd/items/rope.md")).filter((p) => ["missing-section", "undeclared-section", "section-order", "unknown-component"].includes(p.kind));
      if (bad.length) notes.push("structure: " + bad.map((p) => p.kind).join(", "));
      return { pass: !notes.length, notes };
    },
  },
  {
    id: "t-rename", page: "docs/gdd/items/rope.md", mode: "work",
    ask: "Rename this page to Hemp Rope.",
    async check({ read, pages }) {
      const notes = [];
      const text = await read("docs/gdd/items/hemp-rope.md");
      if (text == null) notes.push("no docs/gdd/items/hemp-rope.md");
      else if (!/^# Hemp rope\s*$/mi.test(text)) notes.push("title isn't Hemp Rope");
      if ((await read("docs/gdd/items/rope.md")) != null) notes.push("rope.md still there");
      for (const p of pages()) { const t = await read(p); if (/\]\((?:[^)]*\/)?rope\.md[)#]/.test(t)) notes.push("still links rope.md: " + p); }
      return { pass: !notes.length, notes };
    },
  },
  {
    id: "t-create", page: "docs/gdd/objects/pine-tree.md", mode: "work",
    ask: "Make a new item page called Pine Resin, and link to it from this page where it fits.",
    async check({ read }) {
      const notes = [];
      const text = await read("docs/gdd/items/pine-resin.md");
      if (text == null) notes.push("no docs/gdd/items/pine-resin.md");
      else {
        if (split(text).data?.type !== "item") notes.push("not type item");
        if (!/^# Pine Resin\s*$/mi.test(text)) notes.push("title isn't Pine Resin");
        if (!/^## /m.test(split(text).body)) notes.push("no template sections");
      }
      if (!/\]\([^)]*pine-resin\.md[)#]/.test(await read("docs/gdd/objects/pine-tree.md"))) notes.push("Pine Tree doesn't link it");
      return { pass: !notes.length, notes };
    },
  },
  {
    id: "t-typos", page: "docs/gdd/objects/pine-tree.md", mode: "work",
    ask: "Fix the spelling mistakes on this page. Change nothing else.",
    // Two typos put into the page first.
    async setup(repo) {
      const f = path.join(repo, "docs/gdd/objects/pine-tree.md");
      const t = fs.readFileSync(f, "utf8");
      const body = t.indexOf("\n# ");
      let u = t.slice(0, body) + t.slice(body).replace(" the ", " teh ").replace(" and ", " adn ");
      fs.writeFileSync(path.join(repo, "pine-tree.orig"), t);
      fs.writeFileSync(f, u);
    },
    async check({ repo, read }) {
      const want = fs.readFileSync(path.join(repo, "pine-tree.orig"), "utf8").replace(/\r\n/g, "\n");
      const got = await read("docs/gdd/objects/pine-tree.md");
      const notes = [];
      if (/\bteh\b|\badn\b/.test(got)) notes.push("a typo is still there");
      if (got.trim() !== want.trim()) notes.push("changed more than the typos");
      return { pass: !notes.length, notes };
    },
  },
];
