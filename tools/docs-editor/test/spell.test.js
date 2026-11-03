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
fs.rmSync(dir, { recursive: true, force: true });
console.log("spelling tests passed");
