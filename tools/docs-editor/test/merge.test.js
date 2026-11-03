import assert from "node:assert/strict";
import { mergeEdit, wrapBlock, splitBlocks } from "../src/merge.js";

// Simulates the editor: paragraphs unwrapped onto one line, everything else as is.
const fakeSerialize = (src) =>
  splitBlocks(src).map((b) => (/^[A-Za-z*[]/.test(b.text) ? b.text.replace(/\n/g, " ") : b.text)).join("\n\n");

const source = `# Title

First paragraph, wrapped
over two lines.

- a list
- of things

| a | b |
| --- | --- |
| 1 | 2 |

Last paragraph.
`;
const baseline = fakeSerialize(source);

// No change: identical output.
assert.equal(mergeEdit(source, baseline, baseline), source);

// Change one block: the others stay byte for byte.
const cur = baseline.replace("Last paragraph.", "Last paragraph, edited.");
assert.equal(mergeEdit(source, baseline, cur), source.replace("Last paragraph.", "Last paragraph, edited."));

// Insert a block in the middle.
const ins = baseline.replace("- a list", "New block.\n\n- a list");
assert.equal(mergeEdit(source, baseline, ins), source.replace("- a list", "New block.\n\n- a list"));

// Delete a block.
const del = baseline.replace(/\| a[\s\S]*?\| 2 \|\n\n/, "");
assert.equal(mergeEdit(source, baseline, del), source.replace(/\| a[\s\S]*?\| 2 \|\n\n/, ""));

// Wrapping: long lines wrap at 100, list continuation indented, never before a marker.
const long = "- " + "word ".repeat(40).trim();
const w = wrapBlock(long);
for (const l of w.split("\n")) assert.ok(l.length <= 100, l);
assert.ok(w.split("\n").slice(1).every((l) => l.startsWith("  ")));
const tricky = "x".repeat(97) + " - not a list";
assert.ok(!wrapBlock(tricky).split("\n").some((l) => l.startsWith("- ")));
assert.equal(wrapBlock("| " + "a ".repeat(80) + "|"), "| " + "a ".repeat(80) + "|");
const quote = "> " + "word ".repeat(40).trim();
assert.ok(wrapBlock(quote).split("\n").every((l) => l.startsWith("> ")));
console.log("merge tests passed");

// Editing one list item or table row keeps the others as written.
{
  const src = "- first item, wrapped\n  over two lines\n- second\n- third\n\n| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n";
  const base = "- first item, wrapped over two lines\n- second\n- third\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |";
  const cur = base.replace("- second", "- second, edited").replace("| 3 | 4 |", "| 3 | 5 |");
  assert.equal(mergeEdit(src, base, cur), src.replace("- second", "- second, edited").replace("| 3 | 4 |", "| 3 | 5 |"));
  const added = base.replace("- third", "- third\n- fourth");
  assert.equal(mergeEdit(src, base, added), src.replace("- third", "- third\n- fourth"));
}
console.log("list and table merge tests passed");

// A long numbered list: the editor pads " 2." to line up with "10.".
{
  const items = Array.from({ length: 10 }, (_, i) => `${i + 1}. Item ${i + 1}`);
  const src = items.join("\n") + "\n";
  const base = items.map((l) => (l.startsWith("10") ? l : " " + l)).join("\n");
  const cur = base.replace(" 2. Item 2", " 2. Item 2, edited");
  assert.equal(mergeEdit(src, base, cur), src.replace("2. Item 2", "2. Item 2, edited"));
}
console.log("padded list test passed");
