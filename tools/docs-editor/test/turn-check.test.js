// The check of what R'th AI changed (src/turn-check.js): only problems the turn brought in, never links
// to pages not written yet, with the To-do's fix where there is one.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { makeTools } from "../src/ai-tools.js";
import { checkTurn, afterTexts, quoteAt } from "../src/turn-check.js";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SYSTEM = fs.readFileSync(path.join(HERE, "..", "..", "..", "docs", "gdd", "how-the-gdd-works.md"), "utf8").replace(/\r\n/g, "\n");
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

const RAT = "---\ntype: entity\ncomponents: [rewards]\nstatus: draft\n---\n\n# Fen Rat\n\n## What it is\n\nA rat. Teh rats bite.\n\n## Rewards\n\n- A hide.\n";
const before = new Map([["docs/gdd/how-the-gdd-works.md", SYSTEM], ["docs/gdd/entities/fen-rat.md", RAT]]);
const tools = makeTools({ repo: HERE, pages: async () => [...before.keys()], read: async (p) => before.get(p) ?? null, write: async () => {},
  spell: { check: async (words) => words.filter((w) => /^(teh|wrongg)$/i.test(w)) } });

await test("only what the turn brought in: a dropped section and a new typo; not the old typo, not a to-do link", async () => {
  const after = RAT.replace("## Rewards\n\n- A hide.\n", "It drops a [gall](../items/rat-gall.md). Wrongg.\n");
  const changes = [{ page: "docs/gdd/entities/fen-rat.md", before: RAT, after }];
  const found = await checkTurn({ tools, before, after: afterTexts(before, changes), changes });
  const kinds = found.map((p) => p.kind).sort();
  assert.deepEqual(kinds, ["missing-section", "spelling"]);
  assert.equal(found.find((p) => p.kind === "spelling").word, "Wrongg");
  assert.equal(found.find((p) => p.kind === "spelling").quote, "Wrongg"); // what the card's link finds in the page
  assert.deepEqual(found.find((p) => p.kind === "missing-section").fix, { kind: "add-section", page: "docs/gdd/entities/fen-rat.md", component: "rewards" });
});

await test("nothing new, nothing found; a new page is checked whole", async () => {
  const same = [{ page: "docs/gdd/entities/fen-rat.md", before: RAT, after: RAT + "\nMore.\n" }];
  assert.deepEqual(await checkTurn({ tools, before, after: afterTexts(before, same), changes: same }), []);
  const made = [{ page: "docs/gdd/entities/bog-rat.md", before: null, after: "# Bog Rat\n\nTeh end.\n" }];
  const found = await checkTurn({ tools, before, after: afterTexts(before, made), changes: made });
  assert.ok(found.some((p) => p.kind === "no-frontmatter" || p.kind === "no-type"));
  assert.ok(found.some((p) => p.kind === "spelling" && p.word === "Teh"));
});

await test("a problem's quote is its line as the editor shows it, without Markdown", async () => {
  const text = "# T\n\n- **Tool:** a [hatchet](../items/hatchet.md) and `rope`\n## Notes on the rat\n";
  assert.equal(quoteAt(text, 3), "Tool: a hatchet and rope");
  assert.equal(quoteAt(text, 4), "Notes on the rat");
  assert.equal(quoteAt(text, 2), null);
  assert.equal(quoteAt(text, null), null);
});

console.log(`turn check: ${passed} passed`);
