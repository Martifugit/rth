// Recent changes and broken links (src/changelog.js, src/links.js). Run: node test/changelog.test.js
import assert from "node:assert/strict";
import { parseLog, entries } from "../src/changelog.js";
import { buildIndex, brokenLinks, headingIds } from "../src/links.js";

let n = 0;
const t = (name, fn) => { fn(); n++; };
const commit = (sha, author, date, subject, body, diff) => `\x1e${sha}\x1f${author}\x1f${date}\x1f${subject}\x1f${body}\x1d\n${diff}`;

const LOG = [
  commit("c3", "Ibux (Discord)", "2026-11-04T12:00:00+01:00", "Docs: Combat (with AI)", "Synced by the Codex.", [
    "diff --git a/docs/gdd/combat.md b/docs/gdd/combat.md",
    "--- a/docs/gdd/combat.md", "+++ b/docs/gdd/combat.md",
    "@@ -10,1 +10,1 @@ ## Bile", "-old", "+new",
    "@@ -20,1 +20,1 @@ ## Bile", "-## Phlegm", "+## Phlegm and breath",
  ].join("\n")),
  commit("c2", "Ibux (Discord)", "2026-11-04T09:00:00+01:00", "Docs: Combat", "", [
    "diff --git a/docs/gdd/combat.md b/docs/gdd/combat.md",
    "@@ -3,0 +3,1 @@ ## Blood", "+a line",
  ].join("\n")),
  commit("c1", "Martin", "2026-11-03T09:00:00+01:00", "Docs: new page", "Co-Authored-By: Claude Opus <noreply@anthropic.com>", [
    "diff --git a/docs/gdd/new.md b/docs/gdd/new.md", "new file mode 100644", "@@ -0,0 +1,2 @@", "+# New", "+text",
    "diff --git a/docs/old.md b/docs/old.md", "deleted file mode 100644", "@@ -1,1 +0,0 @@", "-# Old",
  ].join("\n")),
].join("");

t("parse: authors, AI, statuses and the section of each change", () => {
  const c = parseLog(LOG);
  assert.equal(c.length, 3);
  assert.equal(c[0].author, "Ibux");
  assert.equal(c[0].ai, true);
  assert.equal(c[0].subject, "Docs: Combat");
  assert.deepEqual(c[0].files[0].sections, ["Bile", "Phlegm and breath"]);
  assert.equal(c[2].ai, true, "a Co-Authored-By Claude trailer counts as AI");
  assert.deepEqual(c[2].files.map((f) => f.status), ["added", "deleted"]);
});

t("entries: one per person, page and day, newest first", () => {
  const titles = new Map([["docs/gdd/combat.md", "Combat"], ["docs/gdd/new.md", "New"]]);
  const e = entries(parseLog(LOG), { titles, me: ["Martin"] });
  assert.equal(e.length, 3);
  assert.deepEqual(e[0].sections, ["Bile", "Phlegm and breath", "Blood"]);
  assert.equal(e[0].ai, true);
  assert.equal(e[0].title, "Combat");
  assert.equal(e[1].mine, true);
  assert.equal(e[1].status, "added");
  assert.equal(e[2].exists, false);
  assert.equal(e[2].status, "deleted");
});

const idx = buildIndex([
  { path: "docs/a.md", text: "# A\n\n## Bile and blood\n\n## Bile and blood\n\n[ok](b.md) [ok2](b.md#the-end) [same](#bile-and-blood-1) [bad](missing.md) [bad heading](b.md#nowhere)\n\n```\n[in code](nope.md)\n```\n[out](https://example.com) [img]: ![x](pic.png)" },
  { path: "docs/b.md", text: "# B\n\n## The end\n" },
]);

t("heading ids number repeats as the outline does", () => assert.deepEqual([...headingIds(idx.texts.get("docs/a.md"))], ["a", "bile-and-blood", "bile-and-blood-1"]));
t("broken links: missing pages and headings, not code, images or the web", () => {
  const b = brokenLinks(idx);
  assert.deepEqual(b.map((l) => [l.kind, l.href]), [["page", "missing.md"], ["heading", "b.md#nowhere"]]);
  assert.equal(b[0].section, "Bile and blood");
});
t("a link to a file that exists outside the pages is fine", () => assert.equal(brokenLinks(idx, (p) => p === "docs/missing.md").length, 1));

console.log(`changelog: ${n} passed`);
