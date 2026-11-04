// "What changed" in #codex, batched per person (helper/src/notices.js), on an in-memory SQLite standing in
// for D1 and a fetch that records what would be posted to Discord instead of posting it.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { DatabaseSync } from "node:sqlite";
import { fileURLToPath } from "node:url";
import { mergeStatus, dueAuthors, queuePage, sendDue, QUIET_MS, MAX_WAIT_MS } from "../helper/src/notices.js";

const HERE = path.dirname(fileURLToPath(import.meta.url));
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

// D1's shape over node:sqlite: prepare(sql).bind(...).first() / all() / run().
function d1() {
  const db = new DatabaseSync(":memory:");
  db.exec(fs.readFileSync(path.join(HERE, "..", "helper", "schema.sql"), "utf8"));
  return {
    prepare(sql) {
      const st = db.prepare(sql);
      let args = [];
      const q = { bind: (...a) => { args = a; return q; }, first: async () => st.get(...args) ?? null, all: async () => ({ results: st.all(...args) }), run: async () => st.run(...args) };
      return q;
    },
  };
}

const posts = [];
globalThis.fetch = async (url, opts) => {
  if (String(url).includes("raw.githubusercontent.com")) return { ok: true, text: async () => "# " + decodeURIComponent(String(url).split("/").pop()).replace(".md", "") + " page\n" };
  if (String(url).includes("/channels/")) { posts.push(JSON.parse(opts.body).content); return { ok: true, json: async () => ({}) }; }
  throw new Error("unexpected fetch " + url);
};
const env = () => ({ DB: d1(), REPO: "o/r", DISCORD_NOTICES_CHANNEL: "c", DISCORD_BOT_TOKEN: "t" });
const O = "https://helper.example";

await test("a page's status across a batch", () => {
  assert.equal(mergeStatus(null, "modified"), "modified");
  assert.equal(mergeStatus("added", "modified"), "added");
  assert.equal(mergeStatus("added", "removed"), null, "made and removed in one batch: never mentioned");
  assert.equal(mergeStatus("removed", "added"), "modified");
  assert.equal(mergeStatus("modified", "removed"), "removed");
});

await test("a batch is ready once its author is quiet, or has waited long enough", () => {
  const t = 10 * MAX_WAIT_MS;
  assert.deepEqual(dueAuthors([{ author: "Kari", first_at: t - 1000, last_at: t - 1000 }], t), [], "still typing");
  assert.deepEqual(dueAuthors([{ author: "Kari", first_at: t - QUIET_MS, last_at: t - QUIET_MS }], t), ["Kari"], "quiet");
  assert.deepEqual(dueAuthors([{ author: "Kari", first_at: t - MAX_WAIT_MS, last_at: t - 1000 }], t), ["Kari"], "kept going");
});

await test("many sends by one person become one message, after they stop", async () => {
  const e = env(); posts.length = 0;
  let t = 1_000_000_000;
  for (let i = 0; i < 12; i++) { await queuePage(e.DB, "Kari", "docs/gdd/combat.md", "modified", O, t); t += 60_000; } // a send a minute
  await queuePage(e.DB, "Kari", "docs/gdd/bile.md", "added", O, t);
  await queuePage(e.DB, "Kari", "docs/gdd/bile.md", "modified", O, t);
  await sendDue(e, t + 60_000);
  assert.equal(posts.length, 0, "nothing while she's still at it");
  await sendDue(e, t + QUIET_MS);
  assert.equal(posts.length, 1);
  assert.match(posts[0], /^\*\*Kari\*\* edited \[combat page\]\(<https:\/\/helper\.example\/open\?p=docs%2Fgdd%2Fcombat\.md>\) and \[bile page\]\(.*\) \(new page\)$/);
  await sendDue(e, t + 2 * QUIET_MS);
  assert.equal(posts.length, 1, "posted once, then forgotten");
});

await test("each person's batch goes when they stop, not when someone else does", async () => {
  const e = env(); posts.length = 0;
  const t = 2_000_000_000;
  await queuePage(e.DB, "Kari", "docs/gdd/combat.md", "modified", O, t);
  await queuePage(e.DB, "Ines", "docs/gdd/death.md", "modified", O, t + QUIET_MS - 60_000);
  await sendDue(e, t + QUIET_MS);
  assert.equal(posts.length, 1);
  assert.match(posts[0], /Kari/); assert.doesNotMatch(posts[0], /Ines/);
  await sendDue(e, t + 2 * QUIET_MS);
  assert.equal(posts.length, 2);
  assert.match(posts[1], /^\*\*Ines\*\* edited/);
});

await test("a page made and removed in one batch posts nothing", async () => {
  const e = env(); posts.length = 0;
  await queuePage(e.DB, "Kari", "docs/gdd/oops.md", "added", O, 0);
  await queuePage(e.DB, "Kari", "docs/gdd/oops.md", "removed", O, 1);
  await sendDue(e, QUIET_MS * 10);
  assert.equal(posts.length, 0);
});

console.log(`notices: ${passed} passed`);
