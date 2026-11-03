import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { capture, locate, sectionsOf } from "../src/anchor.js";
import { makeStore, serialize } from "../src/notes-store.js";
import { buildIndex, linksFor, pageState, linksOf } from "../src/links.js";

let failed = 0, passed = 0;
async function test(name, fn) {
  try { await fn(); passed++; } catch (e) { failed++; console.error("FAIL", name, "\n ", e.message); }
}

// A page as the editor's text: blocks joined by "\n", headings with their offsets.
function page(blocks) {
  let text = "";
  const heads = [];
  for (const b of blocks) {
    if (text) text += "\n";
    const m = b.match(/^(#+) (.*)$/);
    if (m) { heads.push({ level: m[1].length, text: m[2], start: text.length }); text += m[2]; } else text += b;
  }
  return { text, sections: sectionsOf(heads, text.length) };
}

const v1 = page([
  "# Combat", "Fights are in real time on the tick.",
  "## Styles", "Three styles that play differently, each supplied by the player's own skills.",
  "## Open questions", "Ranged: its tips, their effects, and whether the calibration meter stays.", "Where ranged keeps its ammunition.",
  "## Sources", "Old docs.",
]);
const at = (p, s) => { const i = p.text.indexOf(s); return capture(p.text, i, i + s.length, p.sections); };

await test("capture records the section path and context", () => {
  const a = at(v1, "whether the calibration meter stays");
  assert.deepEqual(a.section, ["Combat", "Open questions"]);
  assert.equal(a.quote, "whether the calibration meter stays");
  assert.ok(a.prefix.endsWith("and "));
  assert.ok(a.suffix.startsWith("."));
});

await test("exact: found where it is", () => {
  const a = at(v1, "whether the calibration meter stays");
  const r = locate(v1.text, v1.sections, a);
  assert.equal(r.how, "exact");
  assert.equal(v1.text.slice(r.start, r.end), a.quote);
});

await test("exact: the passage moved to another section", () => {
  const a = at(v1, "whether the calibration meter stays");
  const v2 = page(["# Combat", "## Ranged", "Its tips, and whether the calibration meter stays, are open.", "## Sources", "x"]);
  const r = locate(v2.text, v2.sections, a);
  assert.equal(r.how, "exact");
});

await test("exact: of several copies, the one in the right section", () => {
  const p = page(["# P", "## A", "the same words here", "## B", "the same words here"]);
  const i = p.text.lastIndexOf("the same words here");
  const a = capture(p.text, i, i + 19, p.sections);
  const r = locate(p.text, p.sections, a);
  assert.equal(r.start, i);
});

await test("fuzzy: reworded a little", () => {
  const a = at(v1, "whether the calibration meter stays");
  const v2 = page(["# Combat", "## Open questions", "Ranged: its tips, their effects, and whether its calibration meter should stay.", "## Sources", "x"]);
  const r = locate(v2.text, v2.sections, a);
  assert.equal(r.how, "fuzzy");
  assert.match(v2.text.slice(r.start, r.end), /calibration meter/);
});

await test("section: the passage is gone, the section isn't", () => {
  const a = at(v1, "whether the calibration meter stays");
  const v2 = page(["# Combat", "## Open questions", "Nothing about that any more.", "## Sources", "x"]);
  const r = locate(v2.text, v2.sections, a);
  assert.equal(r.how, "section");
  assert.equal(v2.text.slice(r.start, r.end), "Open questions");
});

await test("lost: passage and section both gone", () => {
  const a = at(v1, "whether the calibration meter stays");
  const v2 = page(["# Other", "Nothing here."]);
  assert.equal(locate(v2.text, v2.sections, a).how, "lost");
});

await test("a short quote is never fuzzy-matched", () => {
  const a = at(v1, "real time");
  const v2 = page(["# Combat", "Fights are slow."]);
  assert.notEqual(locate(v2.text, v2.sections, a).how, "fuzzy");
});

await test("page notes and section notes", () => {
  assert.equal(locate(v1.text, v1.sections, { section: [], quote: "" }).how, "page");
  assert.equal(locate(v1.text, v1.sections, { section: ["Combat", "Styles"], quote: "" }).how, "section");
});

// ----------------------------------------------------------------------------- the store

const repo = fs.mkdtempSync(path.join(os.tmpdir(), "codex-notes-"));
fs.mkdirSync(path.join(repo, "docs", "gdd"), { recursive: true });
fs.writeFileSync(path.join(repo, "docs", "gdd", "combat.md"), "# Combat\n");
const store = makeStore(repo);

await test("files mirror the pages", () => {
  assert.equal(store.fileFor("docs/gdd/combat.md"), "docs/.notes/gdd/combat.md.json");
  assert.equal(store.fileFor("README.md"), "docs/.notes/_root/README.md.json");
  assert.equal(store.fileFor(null), "docs/.notes/_scratch.json");
  for (const p of ["docs/gdd/combat.md", "README.md", null]) assert.equal(store.pageFor(store.fileFor(p)), p);
  assert.equal(store.pageFor("docs/gdd/combat.md"), undefined);
});

await test("create, reply, update, move, remove", async () => {
  const a = await store.create("docs/gdd/combat.md", { kind: "ask", text: " Why three styles? ", anchor: { section: ["Combat"], quote: "x" } });
  assert.match(a.note.id, /^n-\d{8}-[0-9a-f]{6}$/);
  assert.equal(a.note.thread[0].text, "Why three styles?");
  const file = path.join(repo, "docs", ".notes", "gdd", "combat.md.json");
  assert.ok(fs.existsSync(file));

  const r = await store.reply(a.note.id, { by: "agent", text: "Pillar 1.", run: { session: "s1", edits: [] } });
  assert.equal(r.note.status, "answered");
  assert.equal(r.note.thread.length, 2);
  const o = await store.reply(a.note.id, { by: "owner", text: "And ranged?" });
  assert.equal(o.note.status, "open");

  const u = await store.update(a.note.id, { status: "done", kind: "bogus" });
  assert.equal(u.note.status, "done");
  assert.equal(u.note.kind, "ask");

  await store.create(null, { text: "scratch" });
  assert.equal((await store.all()).length, 2);

  const m = await store.move(a.note.id, "README.md");
  assert.equal(m.page, "README.md");
  assert.ok(!fs.existsSync(file), "an emptied page loses its file");
  assert.equal((await store.load("README.md")).length, 1);

  assert.ok(await store.remove(a.note.id));
  assert.equal((await store.all()).length, 1);
});

await test("concurrent writes to one page all land", async () => {
  await Promise.all(Array.from({ length: 8 }, (_, i) => store.create("docs/gdd/combat.md", { text: "n" + i })));
  assert.equal((await store.load("docs/gdd/combat.md")).length, 8);
});

await test("the file's keys come in a fixed order", () => {
  const text = serialize("p.md", [{ thread: [{ text: "t", at: "a", by: "owner" }], id: "n-1", anchor: { quote: "q", section: [] }, kind: "note" }]);
  assert.ok(text.indexOf('"id"') < text.indexOf('"kind"'));
  assert.ok(text.indexOf('"kind"') < text.indexOf('"anchor"'));
  assert.ok(text.indexOf('"section"') < text.indexOf('"quote"'));
  assert.ok(text.indexOf('"by"') < text.indexOf('"text"'));
  assert.ok(text.endsWith("}\n"));
});

await test("only a person's changes count as writes for syncing, not the Codex's quiet upkeep", async () => {
  const writes = [];
  const s2 = makeStore(repo, { onWrite: (file, opts) => writes.push(opts?.quiet ? "quiet" : "person") });
  const made = await s2.create("docs/gdd/combat.md", { text: "a person's comment" });
  await s2.update(made.note.id, { status: "open", touch: false });       // e.g. a status or position fix
  await s2.update(made.note.id, { anchor: { quote: "x" }, touch: false });
  await s2.reply(made.note.id, { by: "owner", text: "a reply" });
  await s2.update(made.note.id, { text: "edited" });
  assert.deepEqual(writes, ["person", "quiet", "quiet", "person", "person"]);
});

await test("orphans: notes whose page is gone", async () => {
  const n = await store.create("docs/gdd/gone.md", { text: "lost" });
  assert.deepEqual((await store.orphans()).map((x) => x.note.id), [n.note.id]);
});

fs.rmSync(repo, { recursive: true, force: true });

// ----------------------------------------------------------------------------- links and the board

await test("links: in, out, broken, folders and mentions", () => {
  const idx = buildIndex([
    { path: "docs/gdd/a.md", text: "# Alpha page\n\n## Uses\n\nSee [b](b.md#x), [gone](nope.md), [dir](sub/) and ![img](i.png).\n\n```\n[not](b.md)\n```\n" },
    { path: "docs/gdd/b.md", text: "# Beta things\n\nBack to [a](./a.md).\n" },
    { path: "docs/gdd/sub/README.md", text: "# Sub\n\nAlpha page is mentioned here.\n" },
  ]);
  const a = linksFor(idx, "docs/gdd/a.md");
  assert.deepEqual(a.outbound.map((l) => [l.to, !!l.broken]), [["docs/gdd/b.md", false], ["docs/gdd/nope.md", true], ["docs/gdd/sub/README.md", false]]);
  assert.equal(a.outbound[0].section, "Uses");
  assert.deepEqual(a.inbound.map((l) => l.from), ["docs/gdd/b.md"]);
  assert.deepEqual(a.mentions.map((m) => m.from), ["docs/gdd/sub/README.md"]);
});

await test("linksOf skips images and code", () => {
  const out = linksOf("docs/x.md", "![a](b.md)\n```\n[c](d.md)\n```\n[e](f.md)", new Set());
  assert.deepEqual(out.map((l) => l.to), ["docs/f.md"]);
});

await test("the board reads status and open questions", () => {
  const st = pageState("# Combat\n\n*Draft (2026-11-03). From `docs/systems.md`.*\n\n## Summary\n\nx\n\n## Open questions\n\n1. Ranged: its **tips**.\n2. Where [ammo](a.md) goes,\n   and how.\n\n## Sources\n\n- y\n");
  assert.equal(st.status, "Draft");
  assert.equal(st.statusLine.date, "2026-11-03");
  assert.deepEqual(st.questions.map((q) => q.text), ["Ranged: its tips.", "Where ammo goes, and how."]);
  assert.equal(pageState("# X\n\nNo status.\n").status, null);
});

console.log(`${passed} passed, ${failed} failed`);
if (failed) process.exit(1);
