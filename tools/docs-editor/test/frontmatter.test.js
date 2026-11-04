// Frontmatter (src/frontmatter.js) and what the GDD's pages say (src/gdd-index.js).
import assert from "node:assert/strict";
import fs from "node:fs";
import { split, setField, make, parse } from "../src/frontmatter.js";
import { readSystem } from "../src/gdd.js";
import { buildGdd, runQuery, checks, labelFor, pageInfo } from "../src/gdd-index.js";
import { buildIndex } from "../src/links.js";

let n = 0;
const t = (name, fn) => { fn(); n++; };

t("split keeps the block exactly as written", () => {
  const text = "---\ntype: object\ncomponents: [action, states]\ngroups: [trees]\nbased-on:\nstatus: draft\n---\n\n# Oak tree\n\nBody.\n";
  const { front, data, body } = split(text);
  assert.equal(front + body, text);
  assert.equal(body, "# Oak tree\n\nBody.\n");
  assert.deepEqual(data, { type: "object", components: ["action", "states"], groups: ["trees"], "based-on": null, status: "draft" });
  assert.equal(split("# No block\n").front, "");
  assert.equal(split("---\nunclosed\n").front, "");
});
t("lists as - lines", () => {
  assert.deepEqual(parse("components:\n  - action\n  - examine\nstatus: stub").components, ["action", "examine"]);
});
t("setField changes one line only", () => {
  const front = "---\ntype: entity\ncomponents: [dialogue]   \nstatus: stub\n---\n\n";
  assert.equal(setField(front, "status", "draft"), "---\ntype: entity\ncomponents: [dialogue]   \nstatus: draft\n---\n\n");
  assert.equal(setField(front, "groups", ["goblins"]), "---\ntype: entity\ncomponents: [dialogue]   \nstatus: stub\ngroups: [goblins]\n---\n\n");
  assert.equal(setField("---\ncomponents:\n  - a\n  - b\nstatus: stub\n---\n", "components", ["a"]), "---\ncomponents: [a]\nstatus: stub\n---\n");
  assert.equal(make({ type: "item", components: [], status: "stub" }), "---\ntype: item\ncomponents: []\ngroups: []\nbased-on:\nstatus: stub\n---\n\n");
});

const system = readSystem(fs.readFileSync(new URL("../../../docs/gdd/how-the-gdd-works.md", import.meta.url), "utf8"));
const pages = new Map(Object.entries({
  "docs/gdd/objects/oak-tree.md": "---\ntype: object\ncomponents: [action, examine]\ngroups: [trees]\nstatus: draft\n---\n\n# Oak tree\n\n## Action\n\n### Chop\n\n- Verb: chop\n- Skill: [Woodcutting](../skills/woodcutting.md)\n- Level: 15\n\n## Examine\n\nA sturdy oak.\n",
  "docs/gdd/objects/willow.md": "---\ntype: object\ncomponents: [action]\ngroups: [trees]\nstatus: stub\n---\n\n# Willow\n\n## Action\n\n- Verb: chop\n- Skill: Woodcutting\n- Level: 30\n",
  "docs/gdd/skills/woodcutting.md": "---\ntype: skill\ncomponents: [trade]\nstatus: stub\n---\n\n# Woodcutting\n\n## Intent\n",
  "docs/gdd/groups/trees.md": "---\ntype: group\ncomponents: []\nstatus: stub\n---\n\n# Trees\n\n## Members\n\n```query\ngroups: this\n```\n",
  "docs/gdd/entities/goblin.md": "---\ntype: entity\ncomponents: [rewards]\ngroups: [goblins]\nstatus: draft\n---\n\n# Goblin\n\n## Rewards\n\n- Drops: [Bones](../items/bones.md)\n",
  "docs/gdd/items/bones.md": "---\ntype: item\nstatus: stub\n---\n\n# Bones\n",
  "docs/gdd/items/notype.md": "# No type\n",
  "docs/gdd/charter.md": "# The Charter\n",
}));
const gdd = buildGdd(pages);
const links = buildIndex([...pages].map(([path, text]) => ({ path, text })));

t("component instances and fields", () => {
  const oak = gdd.pages.get("docs/gdd/objects/oak-tree.md");
  const action = oak.sections.find((s) => s.name === "action");
  assert.equal(action.instances.length, 1);
  assert.equal(action.instances[0].name, "Chop");
  assert.equal(action.instances[0].fields.level, "15");
  assert.equal(pageInfo("x.md", "# X\n\n## Trade\n\n- Stock: logs\n").sections[0].instances[0].fields.stock, "logs");
});
t("group members", () => {
  const r = runQuery(gdd, links, "docs/gdd/groups/trees.md", "groups: this");
  assert.equal(r.kind, "pages");
  assert.deepEqual(r.rows.map((x) => x.title), ["Oak tree", "Willow"]);
});
t("a skill's training table, by link or by name, sorted by level", () => {
  const r = runQuery(gdd, links, "docs/gdd/skills/woodcutting.md", "action.skill: this\nsort: level");
  assert.equal(r.kind, "instances");
  assert.deepEqual(r.rows.map((x) => x.title + " " + x.fields.level), ["Oak tree 15", "Willow 30"]);
  assert.ok(r.columns.includes("verb") && !r.columns.includes("skill"));
});
t("frontmatter filters", () => {
  assert.deepEqual(runQuery(gdd, links, null, "type: object\nstatus: stub").rows.map((x) => x.title), ["Willow"]);
  assert.deepEqual(runQuery(gdd, links, "docs/gdd/items/bones.md", "links-to: this").rows.map((x) => x.title), ["Goblin"]);
});
t("labels on links in", () => {
  assert.equal(labelFor(gdd, system, "docs/gdd/entities/goblin.md", "Rewards"), "Dropped by");
  assert.equal(labelFor(gdd, system, "docs/gdd/objects/oak-tree.md", "Action › Chop"), null);
});
t("structure checks", () => {
  const ps = checks(gdd, system).map((p) => p.path.split("/").pop() + " " + p.kind).sort();
  assert.ok(ps.includes("goblin.md missing-group"));       // groups: [goblins], no page
  assert.ok(ps.includes("notype.md no-frontmatter"));
  assert.ok(ps.includes("woodcutting.md missing-section")); // trade declared, no Trade section
  assert.ok(!ps.some((x) => x.startsWith("charter.md")));  // the top pages need no type
  assert.ok(!ps.some((x) => x.startsWith("oak-tree.md")));
});
t("file names unique across the GDD, placeholders are to-dos", () => {
  const L = (...lines) => lines.join("\n") + "\n";
  const more = new Map([
    ["docs/gdd/entities/oak.md", L("---", "type: entity", "status: stub", "---", "", "# Oak", "", "Sworn to p-sworn.", "", "```", "p-in-code", "```")],
    ["docs/gdd/objects/oak.md", L("---", "type: object", "status: stub", "---", "", "# Oak")],
    ["docs/gdd/workflow.md", L("# Workflow", "", "Placeholders start with p- (p-sworn).")],
  ]);
  const ps = checks(buildGdd(more), system, more);
  assert.equal(ps.filter((p) => p.kind === "duplicate-name").length, 2);
  const ph = ps.filter((p) => p.kind === "placeholder");
  assert.deepEqual(ph.map((p) => p.path.split("/").pop() + ":" + p.line), ["oak.md:8"]); // not in code, not on the Workflow page
});
console.log(`frontmatter and gdd index: ${n} passed`);
