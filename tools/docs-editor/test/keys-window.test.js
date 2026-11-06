// The Keys window (src/keys-window.js) lists every shortcut the app handles (src/app.js, SHORTCUTS),
// and every key it lists can be named.
import assert from "node:assert/strict";
import fs from "node:fs";
import { KEYS, MARKDOWN } from "../src/keys-window.js";
import { label, useLayout } from "../src/keys.js";

let passed = 0;
function test(name, fn) { try { fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

const listed = new Set(KEYS.flatMap((g) => g.parts.flatMap((p) => p.rows.flatMap(([, ...specs]) => specs))));

test("every SHORTCUTS key is in the Keys window", () => {
  const src = fs.readFileSync(new URL("../src/app.js", import.meta.url), "utf8");
  const block = src.slice(src.indexOf("const SHORTCUTS = ["), src.indexOf("].map((x) => ({ notTyping: false"));
  const specs = [...block.matchAll(/keys: \[([^\]]*)\]/g)].flatMap((m) => [...m[1].matchAll(/"([^"]+)"/g)].map((x) => x[1]));
  assert.ok(specs.length > 20);
  for (const s of specs) assert.ok(listed.has(s), `${s} isn't in the Keys window`);
});
test("one action a row: no brackets doing a second job", () => {
  for (const g of KEYS) for (const p of g.parts) for (const [what] of p.rows) assert.ok(!/[()]/.test(what), what);
});
test("every key can be named", () => {
  useLayout(null);
  for (const s of listed) {
    const parts = label(s.replace(/\*2$/, ""));
    assert.ok(parts.length && parts.every((x) => x && x.length <= 10), `${s}: ${parts}`);
  }
});
test("the Markdown sheet has what it shows and how", () => {
  for (const row of MARKDOWN) assert.equal(row.length, 3);
});

console.log(`keys-window: ${passed} passed`);
