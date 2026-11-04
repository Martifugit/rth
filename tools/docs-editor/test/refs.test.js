// References between pages (src/refs.js): renaming or moving a page rewrites every reference to it.
import assert from "node:assert/strict";
import { rewriteRefs, refsTo, retitle } from "../src/refs.js";

const front = (groups, basedOn) => `---\ntype: entity\ncomponents: []\ngroups: [${groups.join(", ")}]\nbased-on:${basedOn ? " " + basedOn : ""}\nstatus: stub\n---\n\n`;
const from = "docs/gdd/groups/goblins.md", to = "docs/gdd/groups/greenkin.md";

// A member of the group: its frontmatter, a link and a query all follow the rename.
const member = front(["goblins", "monsters"]) + "# Goblin\n\nOne of the [goblins](../groups/goblins.md#members).\n\n```query\ngroups: goblins\nlinks-to: goblins\ntype: entity\n```\n";
const after = rewriteRefs(member, "docs/gdd/entities/goblin.md", from, to);
assert.ok(after.includes("groups: [greenkin, monsters]"));
assert.ok(after.includes("[goblins](../groups/greenkin.md#members)"), "the link's text stays as written");
assert.ok(after.includes("groups: greenkin\nlinks-to: greenkin\ntype: entity"));

// based-on, and CRLF text kept CRLF.
const crlf = (front([], "goblins") + "# Goblin chief\n\n```query\nbased-on: goblins\n```\n").replace(/\n/g, "\r\n");
const c2 = rewriteRefs(crlf, "docs/gdd/entities/goblin-chief.md", from, to);
assert.ok(c2.includes("based-on: greenkin\r\n") && c2.includes("```query\r\nbased-on: greenkin\r\n```"));
assert.ok(!/[^\r]\n/.test(c2), "still CRLF");

// Only whole IDs: "goblins-lair" and text that merely says goblins stay.
const other = front(["goblins-lair"]) + "# Lair\n\nThe goblins live here.\n\n```query\ngroups: goblins-lair\n```\n\nNot a query:\n\ngroups: goblins\n";
assert.equal(rewriteRefs(other, "docs/gdd/locations/lair.md", from, to), other);

// A move into another folder with the same name: links follow, IDs stay.
const moved = rewriteRefs(member, "docs/gdd/entities/goblin.md", from, "docs/gdd/lore/goblins.md");
assert.ok(moved.includes("../lore/goblins.md#members") && moved.includes("groups: [goblins, monsters]"));

// Everything that refers to a page.
const texts = new Map([
  ["docs/gdd/entities/goblin.md", member],
  ["docs/gdd/entities/goblin-chief.md", crlf],
  ["docs/gdd/locations/lair.md", other],
  [from, "# Goblins\n"],
]);
const refs = refsTo(texts, from).map((r) => r.from.split("/").pop() + ":" + r.kind).sort();
assert.deepEqual(refs, ["goblin-chief.md:query", "goblin-chief.md:variant", "goblin.md:group", "goblin.md:link", "goblin.md:query"]);

assert.equal(retitle("---\ntype: x\n---\n\n# Old\n\n## Old\n", "New"), "---\ntype: x\n---\n\n# New\n\n## Old\n");
assert.equal(retitle("# Old\r\nText\r\n", "New"), "# New\r\nText\r\n");

console.log("refs: renames and moves rewrite every reference");
