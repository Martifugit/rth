import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { makeSpell } from "../src/spell.js";

const dir = fs.mkdtempSync(path.join(os.tmpdir(), "codex-spell-"));
const wordsFile = path.join(dir, "words.txt");
const texts = async () => new Map([["a.md", "Pennick rows. Pennick waits. Pennick takes the obol. `codeword codeword codeword`"]]);
const s = makeSpell({ wordsFile, texts });

assert.deepEqual(await s.check(["the", "color", "teh", "qwertyx"]), ["teh", "qwertyx"], "US English, typos flagged");
assert.deepEqual(await s.check(["Pennick", "Pennick's"]), [], "words the docs repeat are known");
assert.deepEqual(await s.check(["codeword"]), ["codeword"], "words only in code don't count");
assert.deepEqual(await s.check(["GDD", "NPCs", "camelCase", "x2"]), [], "acronyms, code and numbers are skipped");
assert.equal(s.suggest("teh")[0], "the", "swapped letters come first");
await s.add("Wennow");
assert.deepEqual(await s.check(["Wennow", "wennow"]), [], "added words are known");
assert.match(fs.readFileSync(wordsFile, "utf8"), /^wennow$/m);

// The shared list: writing it says so (to be sent), an older Codex's own list moves in once, and
// words others added (the file changed under it) are read again.
{
  const shared = path.join(dir, "docs", ".codex", "words.txt"), old = path.join(dir, "old-words.txt");
  fs.writeFileSync(old, "# an older Codex's list\nfavour\nWennow\n");
  let writes = 0;
  const t = makeSpell({ wordsFile: shared, texts, legacy: [old], onWrite: () => writes++ });
  assert.deepEqual(await t.check(["favour"]), [], "an older list's words are known");
  assert.match(fs.readFileSync(shared, "utf8"), /^favour$/m, "and moved into the shared list");
  assert.equal(fs.existsSync(old), false, "the old list is set aside, so it moves only once");
  assert.ok(fs.existsSync(path.join(dir, "old-words-moved-to-docs.txt")));
  assert.equal(writes, 1, "moving them in writes the shared list, to be sent");
  await t.add("Tolling");
  assert.equal(writes, 2, "adding a word writes it, to be sent");
  fs.appendFileSync(shared, "qorvath\n"); // someone else's word, fetched
  assert.deepEqual(await t.check(["qorvath"]), ["qorvath"], "not known until read again");
  await t.reload();
  assert.deepEqual(await t.check(["qorvath", "Tolling"]), [], "read again: theirs and ours");
}
fs.rmSync(dir, { recursive: true, force: true });
console.log("spelling tests passed");
