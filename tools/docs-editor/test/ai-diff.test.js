// What changed, for Show changes and the AI's replies (src/ai-diff.js). Run: node test/ai-diff.test.js
import assert from "node:assert/strict";
import { diffSeq, diffWords, blocks, places } from "../src/ai-diff.js";

let n = 0;
const t = (name, fn) => { fn(); n++; };

t("no difference, no hunks", () => assert.deepEqual(diffSeq([1, 2, 3], [1, 2, 3]), []));
t("a replacement in the middle", () => assert.deepEqual(diffSeq(["a", "b", "c"], ["a", "x", "c"]), [{ a: [1, 2], b: [1, 2] }]));
t("an insertion and a deletion", () => {
  assert.deepEqual(diffSeq(["a", "c"], ["a", "b", "c"]), [{ a: [1, 1], b: [1, 2] }]);
  assert.deepEqual(diffSeq(["a", "b", "c"], ["a", "c"]), [{ a: [1, 2], b: [1, 1] }]);
});
t("two separate places", () => assert.equal(diffSeq(["a", "b", "c", "d", "e"], ["a", "B", "c", "d", "E"]).length, 2));
t("a custom sameness", () => assert.deepEqual(diffSeq(["A"], ["a"], (x, y) => x.toLowerCase() === y.toLowerCase()), []));

t("word diff rebuilds both texts", () => {
  const a = "Bile settles toward zero, a point every ten ticks.";
  const b = "Out of combat, bile settles toward zero every ten ticks.";
  const d = diffWords(a, b);
  assert.equal(d.filter((x) => x.op !== "+").map((x) => x.text).join(""), a);
  assert.equal(d.filter((x) => x.op !== "-").map((x) => x.text).join(""), b);
  assert.ok(d.some((x) => x.op === "+" && /Out/.test(x.text)));
});
t("blocks: paragraphs, and a code block with blank lines as one", () => {
  assert.deepEqual(blocks("# T\n\nOne\ntwo\n\n```\na\n\nb\n```\n\n- x\n- y\n"), ["# T", "One\ntwo", "```\na\n\nb\n```", "- x\n- y"]);
});
t("places counts changed runs of blocks", () => {
  assert.equal(places("# T\n\nA\n\nB\n\nC\n", "# T\n\nA2\n\nB\n\nC2\n"), 2);
  assert.equal(places("x", "x"), 0);
  assert.equal(places("", "# New\n\nText\n"), 1);
});
console.log(`ai-diff: ${n} passed`);
