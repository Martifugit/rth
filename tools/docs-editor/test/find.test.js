// Find and replace (src/find.js): what matches, and what in a page's Markdown is never replaced.
import assert from "node:assert/strict";
import { findAll, findInMarkdown, replaceInMarkdown, lineOf } from "../src/find.js";

let passed = 0;
function test(name, fn) { try { fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

test("ignores case unless asked; never overlaps", () => {
  assert.deepEqual(findAll("Rat rat RAT", "rat").map((m) => m.start), [0, 4, 8]);
  assert.deepEqual(findAll("Rat rat RAT", "rat", { matchCase: true }).map((m) => m.start), [4]);
  assert.deepEqual(findAll("aaaa", "aa").map((m) => m.start), [0, 2]);
  assert.deepEqual(findAll("anything", ""), []);
});
test("whole words: letters on either side don't count, Norwegian letters included", () => {
  assert.deepEqual(findAll("rat rats pirate rat.", "rat", { wholeWord: true }).map((m) => m.start), [0, 16]);
  assert.deepEqual(findAll("ørat rat", "rat", { wholeWord: true }).map((m) => m.start), [5]);
});
test("never in the frontmatter, a link's target, an image's file, code or a comment", () => {
  const md = "---\ntype: rat\n---\n\n# Fen rat\n\nA [rat](rat.md) and ![rat](img/rat.png), `rat` too.\n\n```\nrat\n```\n\n<!-- rat -->\n[r]: rat.md\nLast rat.\n";
  const hits = findInMarkdown(md, "rat").map((m) => md.slice(m.start - 2, m.end + 1));
  assert.deepEqual(hits, ["n rat\n", " [rat]", "![rat]", "t rat."]);
});
test("replacing keeps the links working", () => {
  const md = "See [Fen rat](fen-rat.md): the fen rat bites.\n";
  const r = replaceInMarkdown(md, "fen rat", "marsh rat");
  assert.equal(r.text, "See [marsh rat](fen-rat.md): the marsh rat bites.\n");
  assert.equal(r.count, 2);
});
test("an unclosed code fence protects to the end", () => {
  assert.equal(findInMarkdown("rat\n```\nrat\nrat", "rat").length, 1);
});
test("the line a match is on", () => {
  assert.deepEqual(lineOf("one\ntwo rat\nthree", 8), { line: 2, text: "two rat", at: 4 });
});

console.log(`find: ${passed} passed`);
