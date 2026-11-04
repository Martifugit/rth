// OpenRouter's edit blocks (src/openrouter-provider.js). Run: node test/openrouter.test.js
import assert from "node:assert/strict";
import { editsIn, applyEdits, shown } from "../src/openrouter-provider.js";

let n = 0;
const t = async (name, fn) => { await fn(); n++; };

const answer = `I tightened the rule.

<edit page="docs/gdd/x.md">
<find>Bile drifts back toward zero once a fight ends.</find>
<replace>Out of combat, bile settles toward zero.</replace>
</edit>

And added a note.
<edit page="docs/gdd/x.md"><find>## Notes
</find><replace>## Notes

Bile settles so each fight starts fresh.
</replace></edit>`;

const page = "# X\n\nBile drifts back toward zero once a fight ends.\n\n## Notes\n";

await t("blocks are found and taken out of the answer", () => {
  const { edits, text } = editsIn(answer);
  assert.equal(edits.length, 2);
  assert.equal(edits[0].find, "Bile drifts back toward zero once a fight ends.");
  assert.equal(text, "I tightened the rule.\n\nAnd added a note.");
});
await t("edits apply in order to the page", async () => {
  const { texts, failed } = await applyEdits(editsIn(answer).edits, async () => page);
  assert.deepEqual(failed, []);
  assert.equal(texts.get("docs/gdd/x.md"), "# X\n\nOut of combat, bile settles toward zero.\n\n## Notes\n\nBile settles so each fight starts fresh.\n");
});
await t("text not found, found twice, or outside docs/gdd/ is refused", async () => {
  const { texts, failed } = await applyEdits([
    { page: "docs/gdd/x.md", find: "nowhere", replace: "x" },
    { page: "docs/gdd/x.md", find: "Bile", replace: "x" },
    { page: "src/app.js", find: "", replace: "x" },
    { page: "docs/gdd/../secret.md", find: "", replace: "x" },
  ], async () => "Bile and Bile");
  assert.equal(texts.size, 0);
  assert.deepEqual(failed.map((f) => f.why), ["text not found", "text found more than once", "not a page in the GDD (docs/gdd/)", "not a page in the GDD (docs/gdd/)"]);
});
await t("a new page from an empty find", async () => {
  const { texts } = await applyEdits([{ page: "docs/gdd/new.md", find: "", replace: "# New" }], async () => null);
  assert.equal(texts.get("docs/gdd/new.md"), "# New\n");
});
await t("while streaming, a half-written block isn't shown", () => {
  assert.equal(shown("Done.\n<edit page=\"docs/gdd/x.md\">\n<find>Bile dr"), "Done.");
});
console.log(`openrouter: ${n} passed`);
