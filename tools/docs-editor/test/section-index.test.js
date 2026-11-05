// The GDD's section index (src/section-index.js): sections by heading, BM25 ranking with the title,
// headings and shared summaries counting extra, and pages kept fresh by hash.
import assert from "node:assert/strict";
import { makeSectionIndex, sectionsOf, terms } from "../src/section-index.js";

let passed = 0;
const test = (name, fn) => { try { fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } };

const RAT = "---\ntype: entity\n---\n\n# Fen Rat\n\nA rat of the fens.\n\n## Rewards\n\nIt drops a hide.\n\n### Rare\n\nA gall, sometimes.\n\n```\n# not a heading\n```\n";
const BILE = "# Bile\n\nBile leans choleric or melancholic.\n\n## Settling\n\nOut of combat bile drifts back to zero.\n";

test("sections: heading paths and lines, code isn't a heading", () => {
  const s = sectionsOf(RAT);
  assert.deepEqual(s.map((x) => x.heading.join(" › ")), ["Fen Rat", "Fen Rat › Rewards", "Fen Rat › Rewards › Rare"]);
  assert.equal(s[1].line, 9);
  assert.match(s[2].text, /# not a heading/);
});

test("terms: lowercase, no common words, plurals folded", () => {
  assert.deepEqual(terms("The rats drop Hides and berries"), ["rat", "drop", "hide", "berry"]);
});

test("ranking: the section that holds the words, the title counting extra", () => {
  const sums = new Map();
  const idx = makeSectionIndex({ summaryOf: (p) => sums.get(p) || null });
  idx.update("rat.md", RAT);
  idx.update("bile.md", BILE);
  const hits = idx.search("bile drifts zero");
  assert.equal(hits[0].path, "bile.md");
  assert.deepEqual(hits[0].heading, ["Bile", "Settling"]);
  assert.match(hits[0].snippet, /drifts back to zero/);
  assert.equal(idx.search("rat")[0].path, "rat.md");
  assert.deepEqual(idx.search("nothing like this"), []);
  // A summary's terms find a page by what it's about.
  assert.deepEqual(idx.search("vermin"), []);
  sums.set("rat.md", { summary: "A marsh creature.", terms: ["vermin"] });
  idx.update("rat.md", RAT);
  assert.equal(idx.search("vermin")[0].path, "rat.md");
  // Deleted pages leave.
  idx.keep(["bile.md"]);
  assert.equal(idx.size(), 1);
  idx.update("bile.md", null);
  assert.equal(idx.size(), 0);
});

console.log(`section index: ${passed} passed`);
