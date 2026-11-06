// When the AI box's @ page picker is open (src/mention-query.js).
import assert from "node:assert/strict";
import { mentionQuery } from "../src/mention-query.js";

let passed = 0;
function test(name, fn) { try { fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

const TITLES = ["Great Bridge", "Fen Rat", "Tolling"];
const matches = (q) => TITLES.some((t) => t.toLowerCase().includes(q.toLowerCase().trim()));
const at = (before, o = {}) => mentionQuery(before, { matches, ...o });

test("opens at an @ that starts the message or follows a space", () => {
  assert.deepEqual(at("@"), { start: 0, q: "", none: false });
  assert.deepEqual(at("look at @fen"), { start: 8, q: "fen", none: false });
  assert.equal(at("mail me@home"), null);
});
test("titles with spaces keep it open while they match", () => {
  assert.equal(at("@great bri").none, false);
});
test("a picked mention is finished: typing after it never reopens the picker", () => {
  const picked = ["@Great Bridge"];
  assert.equal(at("@Great Bridge ", { picked }), null);
  assert.equal(at("@Great Bridge and what crosses it", { picked }), null);
  assert.equal(at("see @Fen Rat. Also", { picked: ["@Fen Rat"] }), null);
});
test("editing back into a picked mention opens it again", () => {
  assert.equal(at("@Great Bridge", { picked: ["@Great Bridge"] }).q, "Great Bridge");
  assert.equal(at("@Great Bri", { picked: ["@Great Bridge"] }).q, "Great Bri");
});
test("a second @ after a picked one opens for that one", () => {
  assert.deepEqual(at("@Fen Rat and @tol", { picked: ["@Fen Rat"] }), { start: 13, q: "tol", none: false });
});
test("no match: 'No page' up to the first space, then closed", () => {
  assert.deepEqual(at("@zzz"), { start: 0, q: "zzz", none: true });
  assert.equal(at("@zzz "), null);
  assert.equal(at("@fen rats are"), null);
});
test("Escape closes it for that @ only", () => {
  assert.equal(at("@fen", { dismissed: 0 }), null);
  assert.equal(at("@fen and @tol", { dismissed: 0 }).start, 9);
});
test("a new line or too long closes it", () => {
  assert.equal(at("@fen\nrat"), null);
  assert.equal(at("@" + "a".repeat(41), { matches: () => true }), null);
});

console.log(`mention-query: ${passed} passed`);
