// Copy for Discord and pasting from Discord (src/discord-extras.js). Run: node test/discord.test.js
import assert from "node:assert/strict";
import { toDiscord, forDiscord, discordConversation, conversationMarkdown, isDiscordImage } from "../src/discord-extras.js";

let n = 0;
const t = (name, fn) => { fn(); n++; };

t("tables become code blocks", () => {
  assert.equal(toDiscord("Before\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\nAfter"), "Before\n\n```\n| a | b |\n|---|---|\n| 1 | 2 |\n```\n\nAfter");
});
t("deep headings become bold, images and page links lose their targets", () => {
  assert.equal(toDiscord("#### Small"), "**Small**");
  assert.equal(toDiscord("## Kept"), "## Kept");
  assert.equal(toDiscord("See ![a map](img/x.png) and [Combat](combat.md) or [web](https://x.y)"), "See [image: a map] and Combat or [web](https://x.y)");
  assert.equal(toDiscord("- [ ] todo\n- [x] done"), "- ☐ todo\n- ☑ done");
});
t("the link back, and the 2,000-character limit with code blocks closed", () => {
  const out = forDiscord("Text", { page: "docs/systems.md", title: "Gameplay [systems]", heading: "bile" });
  assert.match(out, /^Text\n-# \[Gameplay systems\]\(<https:\/\/rth-codex\.rthapp\.workers\.dev\/open\?p=docs%2Fsystems\.md&h=bile>\)$/);
  const long = forDiscord("| a |\n".repeat(600), { page: "docs/x.md", title: "X" });
  assert.ok(long.length <= 2000, long.length);
  assert.equal((long.match(/```/g) || []).length % 2, 0);
});
t("a cozy Discord conversation", () => {
  const c = discordConversation("Alice — Today at 17:12\nShould bile settle faster?\nIt drags.\nBob — Today at 17:13\nNo, keep it.");
  assert.deepEqual(c, [{ name: "Alice", text: "Should bile settle faster?\nIt drags." }, { name: "Bob", text: "No, keep it." }]);
  assert.equal(conversationMarkdown(c), "> **Alice:** Should bile settle faster?\n> It drags.\n>\n> **Bob:** No, keep it.\n");
});
t("dated, 12-hour and compact forms", () => {
  assert.deepEqual(discordConversation("Alice — 11/03/2026 5:12 PM\nHi"), [{ name: "Alice", text: "Hi" }]);
  assert.deepEqual(discordConversation("[17:12] Alice: Hi\n[17:13] Bob: Yo"), [{ name: "Alice", text: "Hi" }, { name: "Bob", text: "Yo" }]);
});
t("ordinary text isn't a conversation", () => {
  assert.equal(discordConversation("Just a paragraph.\nAnother line — with a dash at 5:12"), null);
  assert.equal(discordConversation("Bile settles over time."), null);
});
t("Discord image links", () => {
  assert.ok(isDiscordImage("https://cdn.discordapp.com/attachments/1/2/map.png?ex=1"));
  assert.ok(!isDiscordImage("https://example.com/map.png"));
});
console.log(`discord: ${n} passed`);
