// The GDD's system as Codex reads it from How the GDD Works (src/gdd.js).
import assert from "node:assert/strict";
import fs from "node:fs";
import { readTypes, newPageText } from "../src/gdd.js";

const page = `# How the GDD Works

## 4. Types

A type decides a page's folder.

| Type | Folder | What it covers | Core headings |
| --- | --- | --- | --- |
| entity | entities | Anything that lives or acts on its own: monsters, NPCs, pets | What it is · Intent · Appearance |
| skill | skills | A trainable skill | Intent · How it feels to train · What it unlocks |

## 5. Components

| Component | Answers |
| --- | --- |
| action | What can the player do with this? |
`;

const types = readTypes(page);
assert.equal(types.length, 2);
assert.deepEqual(types[0], { type: "entity", folder: "entities", covers: "Anything that lives or acts on its own: monsters, NPCs, pets", examples: "monsters, NPCs, pets", headings: ["What it is", "Intent", "Appearance"] });
assert.equal(types[1].examples, "A trainable skill");
assert.deepEqual(readTypes("# Nothing here\n"), []);
assert.equal(newPageText("Goblin", types[0]), "# Goblin\n\n## What it is\n\n## Intent\n\n## Appearance\n");
assert.equal(newPageText("Loose", null), "# Loose\n");

// The real page: every type has a folder and core headings.
const real = readTypes(fs.readFileSync(new URL("../../../docs/gdd/how-the-gdd-works.md", import.meta.url), "utf8"));
assert.ok(real.length >= 17 && real.every((t) => t.folder && t.headings.length), "How the GDD Works' Types table reads");
console.log("gdd: types read from How the GDD Works");
