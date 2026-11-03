// Comments from the AI tab (src/ai-comments.js). Run: node test/ai-comments.test.js
import assert from "node:assert/strict";
import { commentsIn, withoutComments, anchorFor } from "../src/ai-comments.js";

let n = 0;
const t = (name, fn) => { fn(); n++; };

const answer = `Here's where each skill stands; I left it as a comment.

<comment page="docs/gdd/2-gameplay/skills/index.md" section="The roster" quote="Ranged">**Where each skill stands**

Far behind: Ranged.</comment>

<reply to="n-abc123">Done: the table is updated.</reply>

Anything else?`;

t("comment and reply blocks are found and taken out", () => {
  const r = commentsIn(answer);
  assert.equal(r.comments.length, 1);
  assert.deepEqual(r.comments[0], { page: "docs/gdd/2-gameplay/skills/index.md", section: "The roster", quote: "Ranged", text: "**Where each skill stands**\n\nFar behind: Ranged." });
  assert.deepEqual(r.replies, [{ to: "n-abc123", text: "Done: the table is updated." }]);
  assert.equal(r.text, "Here's where each skill stands; I left it as a comment.\n\nAnything else?");
});

t("a block still being written is hidden while it streams", () => {
  assert.equal(withoutComments('Leaving it as a comment.\n\n<comment page="docs/x.md">Half of the'), "Leaving it as a comment.");
  assert.equal(withoutComments("No blocks at all."), "No blocks at all.");
});

t("the anchor gets the page title in front, and drops a quote that isn't on the page", () => {
  const page = "# Skills\n\n## The roster\n\nRanged is far behind.\n";
  assert.deepEqual(anchorFor(page, "The roster", "Ranged is").anchor, { section: ["Skills", "The roster"], quote: "Ranged is", prefix: "", suffix: "" });
  assert.deepEqual(anchorFor(page, "Skills > The roster", "not there").anchor.quote, "");
  assert.deepEqual(anchorFor(page, "", "").anchor.section, []);
  assert.equal(anchorFor(page, "", "").title, "Skills");
});

console.log(`ai-comments: ${n} passed`);
