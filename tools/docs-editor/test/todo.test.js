// The To-do (src/todo.js) and its fixes (src/fixes.js), on a small GDD.
import assert from "node:assert/strict";
import fs from "node:fs";
import { buildIndex, brokenLinks } from "../src/links.js";
import { buildGdd, checks } from "../src/gdd-index.js";
import { readSystem } from "../src/gdd.js";
import { buildTodo, likeness, renamesFromLog } from "../src/todo.js";
import { planFix } from "../src/fixes.js";

const system = readSystem(fs.readFileSync(new URL("../../../docs/gdd/how-the-gdd-works.md", import.meta.url), "utf8").replace(/\r\n/g, "\n"));
const fm = (type, extra = "") => `---\ntype: ${type}\ncomponents: [${extra}]\ngroups: []\nbased-on:\nstatus: stub\n---\n\n`;
const pages = {
  "docs/gdd/entities/goblin.md": fm("entity", "rewards").replace("groups: []", "groups: [goblins]") + "# Goblin\n\nLives in [the hollow](../locations/mossy-hollow.md) near the [chief](goblin-chief.md).\n\n## What it is\n\n## Rewards\n\n| Item | Amount |\n| --- | --- |\n| Bones | 1 |\n",
  "docs/gdd/entities/goblin-elite.md": fm("entity").replace("based-on:", "based-on: goblin-chief") + "# Goblin elite\n\nSee [the chief](goblin-chief.md#tactics) and [Goblin](goblin.md#nope). Named p-grubnik.\n",
  "docs/gdd/locations/mossy-hollows.md": fm("location") + "# Mossy Hollows\n\n## Intent and mood\n\nDamp. p-grubnik lives here.\n",
  "docs/gdd/items/bones.md": fm("item") + "# Bones\n\n## Combat\n\nIt rattles.\n",
};
const texts = Object.entries(pages);
const idx = buildIndex(texts.map(([path, text]) => ({ path, text })));
const gdd = buildGdd(new Map(texts));
const todo = buildTodo({ idx, gdd, system, problems: checks(gdd, system, new Map(texts)), headings: brokenLinks(idx).filter((l) => l.kind === "heading") });

// Missing pages: one card each, however many refer to them, most wanted first, with a likely match.
assert.deepEqual(todo.missing.map((c) => c.id), ["goblin-chief", "goblins", "mossy-hollow"]);
const chief = todo.missing[0];
assert.equal(chief.pages, 2);
assert.deepEqual(chief.refs.map((r) => r.kind).sort(), ["link", "link", "variant"]);
assert.equal(todo.missing[2].suggestions[0].path, "docs/gdd/locations/mossy-hollows.md", "did you mean Mossy Hollows");
// The other kinds.
assert.ok(todo.references.some((r) => r.problem === "heading" && r.anchor === "nope"));
assert.ok(todo.references.some((r) => r.problem === "plain" && r.text === "Bones" && r.to === "docs/gdd/items/bones.md"));
assert.deepEqual(todo.placeholders.map((p) => [p.token, p.pages]), [["p-grubnik", 2]]);
assert.ok(todo.structure.some((s) => s.problem === "undeclared-section" && s.component === "combat" && s.fix === "declare"));
assert.ok(likeness("mossy-hollow", "mossy-hollows") > 0.9 && likeness("goblin", "tree") < 0.5);
assert.deepEqual([...renamesFromLog("abc\nR100\tdocs/gdd/a/x.md\tdocs/gdd/a/y.md\nM\tdocs/gdd/a/y.md\n")], [["docs/gdd/a/x.md", "docs/gdd/a/y.md"]]);

// Fixes.
const T = new Map(texts);
const plan = (fix) => planFix(fix, { texts: T, system });
// Every reference to the missing chief goes to an existing page: links (anchors kept) and based-on.
const re = plan({ kind: "retarget", from: chief.refs.filter((r) => r.to).map((r) => r.to), id: "goblin-chief", to: "docs/gdd/entities/goblin.md" });
assert.deepEqual([...re.keys()].sort(), ["docs/gdd/entities/goblin-elite.md", "docs/gdd/entities/goblin.md"]);
assert.ok(re.get("docs/gdd/entities/goblin-elite.md").includes("based-on: goblin\n") && re.get("docs/gdd/entities/goblin-elite.md").includes("(goblin.md#tactics)"));
// One link only, or none.
const hollow = todo.missing[2].refs[0];
assert.ok(plan({ kind: "retarget-one", page: hollow.from, line: hollow.line, from: hollow.to, to: "docs/gdd/locations/mossy-hollows.md" }).get(hollow.from).includes("[the hollow](../locations/mossy-hollows.md)"));
assert.ok(plan({ kind: "unlink", page: hollow.from, line: hollow.line, target: hollow.to }).get(hollow.from).includes("Lives in the hollow near"));
// Frontmatter references, a heading anchor, a plain-text field, a placeholder.
assert.ok(plan({ kind: "drop-ref", page: "docs/gdd/entities/goblin.md", field: "groups", id: "goblins" }).get("docs/gdd/entities/goblin.md").includes("groups: []"));
const h = todo.references.find((r) => r.problem === "heading");
assert.ok(plan({ kind: "drop-anchor", page: h.from, line: h.line, target: h.to }).get(h.from).includes("[Goblin](goblin.md)"));
const pl = todo.references.find((r) => r.problem === "plain");
assert.ok(plan({ kind: "link-field", page: pl.from, line: pl.line, text: pl.text, to: pl.to }).get(pl.from).includes("| [Bones](../items/bones.md) | 1 |"));
const named = plan({ kind: "name-placeholder", token: "p-grubnik", name: "Grubnik" });
assert.equal(named.size, 2);
assert.ok([...named.values()].every((t) => t.includes("Grubnik") && !t.includes("p-grubnik")));
// Structure: declare, add a section (in its place), reorder.
const decl = plan({ kind: "declare", page: "docs/gdd/locations/mossy-hollows.md", component: "spawns" });
assert.ok(decl.get("docs/gdd/locations/mossy-hollows.md").includes("components: [spawns]"));
const added = plan({ kind: "add-section", page: "docs/gdd/locations/mossy-hollows.md", component: "spawns" }).get("docs/gdd/locations/mossy-hollows.md");
assert.ok(/## Intent and mood[\s\S]*## Spawns/.test(added));
assert.throws(() => plan({ kind: "explode" }));
console.log("todo: cards and fixes");
