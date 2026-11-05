// The GDD's health check (src/audit.js), with a pretend R'th AI: which pages are due (changed since
// checked, and since the baseline unless sweeping), problems written per page and shared, a fresh check
// replacing the last, quotes that are gone dropping a problem, dismissing.
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { makeAuditor } from "../src/audit.js";

const repo = fs.mkdtempSync(path.join(os.tmpdir(), "codex-audit-"));
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }
const put = (rel, text) => { fs.mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true }); fs.writeFileSync(path.join(repo, rel), text); };
const get = (rel) => { try { return fs.readFileSync(path.join(repo, rel), "utf8"); } catch { return null; } };
put("docs/gdd/entities/fen-rat.md", "# Fen Rat\n\nDrops 12 hides.\n");
put("docs/gdd/skills/butchering.md", "# Butchering\n\nA fen rat gives 8 hides.\n");
put("docs/gdd/templates/entity.md", "# Title\n");

let asked = [], report = [];
const written = [];
const auditor = makeAuditor({
  repo, dataDir: path.join(repo, ".local"),
  pages: async () => ["docs/gdd/entities/fen-rat.md", "docs/gdd/skills/butchering.md", "docs/gdd/templates/entity.md"],
  read: async (p) => get(p), audit: async ({ pages }) => { asked.push(pages); return { problems: report }; },
  who: () => "Kari", ownChanged: (rel) => written.push(rel),
});

await test("the first run takes a baseline: nothing is due until a page changes", async () => {
  assert.deepEqual(await auditor.due(), []);
  put("docs/gdd/entities/fen-rat.md", "# Fen Rat\n\nDrops 12 hides, always.\n");
  assert.deepEqual((await auditor.due()).map((x) => x.page), ["docs/gdd/entities/fen-rat.md"]);
});

await test("a run: problems on the page to change first, shared; the checked version recorded", async () => {
  report = [{ kind: "contradiction", pages: ["docs/gdd/skills/butchering.md", "docs/gdd/entities/fen-rat.md"], quotes: ["8 hides", "12 hides"], summary: "Hides: 12 or 8?", detail: "d", plan: "Link instead." }];
  const r = await auditor.runOnce();
  assert.deepEqual(r.checked, ["docs/gdd/entities/fen-rat.md"]);
  assert.deepEqual(asked.at(-1), ["docs/gdd/entities/fen-rat.md"]);
  assert.ok(written.includes("docs/.codex/problems/skills/butchering.json") && written.includes("docs/.codex/problems/entities/fen-rat.json"));
  const list = await auditor.list();
  assert.equal(list.length, 1);
  assert.equal(list[0].found.by, "Kari");
  assert.equal(list[0].of, "docs/gdd/entities/fen-rat.md");
  assert.deepEqual(await auditor.due(), [], "checked in this version");
});

await test("checking a page again replaces what its last check found", async () => {
  put("docs/gdd/entities/fen-rat.md", "# Fen Rat\n\nDrops 12 hides, always and ever.\n");
  report = [];
  await auditor.runOnce();
  // The problem lives on butchering's file, found while checking fen rat: a fresh check of fen rat that finds nothing doesn't touch it there.
  assert.equal((await auditor.list()).length, 1);
  report = [{ kind: "missing-link", pages: ["docs/gdd/entities/fen-rat.md"], quotes: ["hides"], summary: "Hides not linked", plan: "Link hides." }];
  put("docs/gdd/entities/fen-rat.md", "# Fen Rat\n\nDrops 12 hides, always and forever.\n");
  await auditor.runOnce();
  put("docs/gdd/entities/fen-rat.md", "# Fen Rat\n\nDrops 12 hides; forever.\n");
  report = [];
  await auditor.runOnce();
  assert.ok(!(await auditor.list()).some((p) => p.summary === "Hides not linked"), "its own page's fresh check dropped it");
});

await test("a problem whose quoted words are gone drops off; dismissing is for everyone", async () => {
  put("docs/gdd/skills/butchering.md", "# Butchering\n\nSee [Fen Rat](../entities/fen-rat.md).\n");
  assert.equal((await auditor.list()).length, 0);
  put("docs/gdd/skills/butchering.md", "# Butchering\n\nA fen rat gives 8 hides.\n");
  const [p] = await auditor.list();
  assert.deepEqual(auditor.dismiss({ file: p.file, id: p.id }), { ok: true });
  assert.equal((await auditor.list()).length, 0);
  assert.match(get("docs/.codex/problems/skills/butchering.json"), /"by": "Kari"/);
});

await test("the whole GDD on purpose (never templates), and nothing while not idle", async () => {
  await auditor.sweep();
  const due = (await auditor.due()).map((x) => x.page);
  assert.ok(due.includes("docs/gdd/skills/butchering.md") && !due.some((p) => p.includes("templates")));
  const n = asked.length;
  await auditor.tick(); // not idle (idleFor is 0), so nothing runs
  assert.equal(asked.length, n);
});

console.log(`audit: ${passed} passed`);
