// A page's structure (src/gdd.js): suggested components, the fixed section order, content checks,
// pages made with components; and links put right when a page moves (src/links.js, relink).
import assert from "node:assert/strict";
import fs from "node:fs";
import { nameOf, sectionText, readSystem, componentsFor, sectionRank, insertIndex, canonicalOrder, inOrder, hasContent, withComponents, instanceText, newPageText } from "../src/gdd.js";
import { relink, hrefBetween } from "../src/links.js";
import { make, split } from "../src/frontmatter.js";

const sys = readSystem(fs.readFileSync(new URL("../../../docs/gdd/how-the-gdd-works.md", import.meta.url), "utf8").replace(/\r\n/g, "\n"));
const entity = sys.types.find((t) => t.type === "entity");
const C = sys.components;

// Repeats: read from the system page's column.
assert.ok(C.find((c) => c.name === "action").repeats, "action repeats");
assert.ok(!C.find((c) => c.name === "examine").repeats, "examine doesn't");

// Usual components for a type come first, canonical order kept in both lists.
const { common, more } = componentsFor(C, "entity");
assert.ok(common.some((c) => c.name === "combat") && common.some((c) => c.name === "requirements"), "entity: combat and the anything-components");
assert.ok(more.some((c) => c.name === "equippable") && !more.some((c) => c.name === "combat"));
assert.equal(common.length + more.length, C.length);
assert.deepEqual(common.map((c) => C.indexOf(c)), [...common.map((c) => C.indexOf(c))].sort((a, b) => a - b));

// Ranks: core headings, then components, then Open questions; the author's own sections have none.
assert.equal(sectionRank("What it is", entity, C), 0);
assert.ok(sectionRank("Appearance", entity, C) < sectionRank("Action", entity, C));
assert.ok(sectionRank("Action", entity, C) < sectionRank("Combat", entity, C));
assert.ok(sectionRank("Examine", entity, C) < sectionRank("Open questions", entity, C));
assert.equal(sectionRank("Notes from the playtest", entity, C), null);

// Where a new section goes.
const heads = ["What it is", "Intent", "Appearance", "Rewards", "Open questions"];
assert.equal(insertIndex(heads, "combat", entity, C), 3);           // before Rewards
assert.equal(insertIndex(heads, "examine", entity, C), 4);          // after Rewards, before Open questions
assert.equal(insertIndex(heads.slice(0, 4), "examine", entity, C), 4); // at the end
assert.equal(insertIndex(["Intent", "Appearance"], "What it is", entity, C), 0); // a missing core heading goes first

// Putting sections in order: an author's own section moves with the one before it.
const messy = ["What it is", "Combat", "Playtest notes", "Intent", "Appearance", "Action"];
assert.deepEqual(canonicalOrder(messy, entity, C).map((i) => messy[i]), ["What it is", "Intent", "Appearance", "Action", "Combat", "Playtest notes"]);
assert.ok(!inOrder(messy, entity, C));
assert.ok(inOrder(heads, entity, C));

// Content: empty template fields don't count.
assert.equal(hasContent(["Verb:", "Skill:", "", "Level:"]), false);
assert.equal(hasContent(["Verb: Chop", "Skill:"]), true);
assert.equal(hasContent(["It hits hard."]), true);
assert.equal(hasContent([]), false);

// A new page with components: in the frontmatter and in canonical order after the core headings.
const base = make({ type: "entity", components: [], groups: [], "based-on": null, status: "stub" }) + newPageText("Goblin", entity).replace(/^/, "");
const page = withComponents(base, ["rewards", "combat"], C, entity, new Map([["combat", "## Combat\n\n- Stats:\n"]]));
assert.deepEqual(split(page).data.components, ["combat", "rewards"]);
const order = [...page.matchAll(/^## (.*)$/gm)].map((m) => m[1]);
assert.deepEqual(order, ["What it is", "Intent", "Appearance", "Combat", "Rewards"]);
assert.ok(page.includes("## Combat\n\n- Stats:\n"), "from its template");
assert.equal(withComponents(base, [], C, entity), base);

// Another instance of a repeating component.
assert.equal(instanceText(C.find((c) => c.name === "action"), "## Action\n\n- Verb:\n- Skill:\n", "Cook shrimp"), "### Cook shrimp\n\n- Verb:\n- Skill:\n");

// A page moved from entities/ to objects/: links to it follow, and its own links are rebased.
assert.equal(hrefBetween("docs/gdd/entities/goblin.md", "docs/gdd/items/axe.md"), "../items/axe.md");
assert.equal(hrefBetween("docs/gdd/rth-overview.md", "docs/gdd/items/axe.md"), "items/axe.md");
const from = "docs/gdd/entities/tree.md", to = "docs/gdd/objects/tree.md";
assert.equal(relink("See [the tree](../entities/tree.md#states) and [Lowe](lowe.md).", "docs/gdd/locations/forest.md", from, to),
  "See [the tree](../objects/tree.md#states) and [Lowe](lowe.md).");
assert.equal(relink("[x](tree.md) `[y](tree.md)`", "docs/gdd/entities/goblin.md", from, to), "[x](../objects/tree.md) `[y](tree.md)`");
assert.equal(relink("```\n[x](../entities/tree.md)\n```", "docs/gdd/locations/forest.md", from, to), "```\n[x](../entities/tree.md)\n```");
// The moved page itself, into a folder at another depth.
assert.equal(relink("[Goblin](goblin.md), [Lowe](../locations/lowe.md), [web](https://x.y), [here](#a), ![pic](../images/t.png)", from, from, "docs/gdd/tree.md"),
  "[Goblin](entities/goblin.md), [Lowe](locations/lowe.md), [web](https://x.y), [here](#a), ![pic](images/t.png)");
assert.equal(relink("nothing to see", "docs/gdd/a/b.md", from, to), "nothing to see");

console.log("gdd: page structure, section order, moves");

// Templates' guidance: prompts under headings, hints after fields; never in a page made from them.
import { promptsOf, stripPrompts, fromTemplate, readPrompts } from "../src/gdd.js";
import { pageInfo } from "../src/gdd-index.js";
const tpl = "## Action\n\n<!-- What a player can\n do with this. -->\n\n- Verb: <!-- the menu option -->\n- Skill:\n";
assert.deepEqual(promptsOf(tpl), { sections: { action: "What a player can do with this." }, fields: { verb: "the menu option" } });
assert.equal(stripPrompts(tpl), "## Action\n\n- Verb:\n- Skill:\n");
assert.equal(sectionText(C.find((c) => c.name === "action"), tpl), "## Action\n\n- Verb:\n- Skill:\n");
assert.equal(fromTemplate("# Title\n\n## Intent\n\n<!-- Why. -->\n", "Goblin"), "# Goblin\n\n## Intent\n\n");
// Every shipped template has guidance, and none of it survives into a page.
const texts = new Map(fs.readdirSync(new URL("../../../docs/gdd/templates/", import.meta.url)).map((f) => ["docs/gdd/templates/" + f, fs.readFileSync(new URL("../../../docs/gdd/templates/" + f, import.meta.url), "utf8")]));
const P = readPrompts(texts, sys);
for (const t of sys.types) for (const hd of t.headings) assert.ok(P.types[t.type].sections[nameOf(hd)], `a prompt for ${t.type}'s ${hd}`);
for (const c of C) assert.ok(P.components[c.name].sections[c.name], `a prompt for ${c.name}`);
for (const [p, text] of texts) assert.ok(!stripPrompts(text).includes("<!--"), p);

// A table in a section: each row an instance; the empty template row isn't one.
const rewardsPage = "---\ntype: entity\ncomponents: [rewards]\n---\n\n# Goblin\n\n## Rewards\n\n| Item | Amount | Chance | When |\n| --- | --- | --- | --- |\n| [Bones](../items/bones.md) | 1 | always | kill |\n| Coins | 5-20 | 1/4 | kill |\n|   |   |   |   |\n";
const rw = pageInfo("docs/gdd/entities/goblin.md", rewardsPage).sections.find((s) => s.name === "rewards");
assert.equal(rw.instances.length, 2);
assert.deepEqual(rw.instances[1].fields, { item: "Coins", amount: "5-20", chance: "1/4", when: "kill" });
console.log("gdd: templates' guidance, tables as instances");
