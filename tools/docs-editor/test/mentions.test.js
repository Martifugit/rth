// @R'th AI in a comment thread (helper/src/mentions.js), on an in-memory SQLite standing in for D1, a
// pretend room and a fetch that records what would be posted: each tag once, "offline" said once when
// no Codex answers, exactly one claim wins, a lapsed claim can be taken again, old messages are ignored.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { DatabaseSync } from "node:sqlite";
import { fileURLToPath } from "node:url";
import { tagsAi, noticed, mentions, sweep, waiting, OFFLINE } from "../helper/src/mentions.js";

const HERE = path.dirname(fileURLToPath(import.meta.url));
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

function d1() {
  const db = new DatabaseSync(":memory:");
  db.exec(fs.readFileSync(path.join(HERE, "..", "helper", "schema.sql"), "utf8"));
  const prep = (sql) => {
    const st = db.prepare(sql);
    let args = [];
    const q = { bind: (...a) => { args = a; return q; }, first: async () => st.get(...args) ?? null, all: async () => ({ results: st.all(...args) }), run: async () => ({ meta: { changes: Number(st.run(...args).changes) } }) };
    return q;
  };
  return { prepare: prep, batch: async (qs) => { for (const q of qs) await q.run(); } };
}

const posts = [];
let n = 0;
globalThis.fetch = async (url, opts) => {
  if (String(url).includes("/webhooks/")) { const b = JSON.parse(opts.body); posts.push(b); return new Response(JSON.stringify({ id: "m" + ++n, channel_id: new URL(url).searchParams.get("thread_id") }), { status: 200 }); }
  throw new Error("unexpected fetch " + url);
};
let answerers = 0;
const asked = [];
const env = { DB: d1(), DISCORD_APP_ID: "1567232969637109922", LIVE: { idFromName: () => "codex", get: () => ({ fetch: async (u, o) => { asked.push(JSON.parse(o.body)); return Response.json({ sent: answerers > 0 }); } }) } };
await env.DB.prepare("INSERT INTO config (k, v) VALUES ('webhook', ?)").bind(JSON.stringify({ id: "w", token: "t" })).run();
await env.DB.prepare("INSERT INTO comments (id, page, kind, status, anchor, tags, created, updated, last_msg, thread) VALUES ('T1', NULL, 'note', 'open', '{}', '[]', 'x', 'x', 'm0', '[]')").run();

await test("what counts as tagging R'th AI", () => {
  assert.ok(tagsAi(env, "<@1567232969637109922> what do you think?"));
  assert.ok(tagsAi(env, "Hey @R'th AI, is this settled?"));
  assert.ok(tagsAi(env, "@rthai check this"));
  assert.ok(!tagsAi(env, "R'th AI said so"));
  assert.ok(!tagsAi(env, "mail me at x@rthai.com"));
  assert.ok(tagsAi(env, "/ask is this settled?"));
  assert.ok(tagsAi(env, "  /ASK what about fen rats"));
  assert.ok(!tagsAi(env, "see /asking for it"));
  assert.ok(!tagsAi(env, "type /ask in Discord"));
});

await test("a tag with no Codex to answer: offline, said once; the same tag again is nothing new", async () => {
  // The first look sets when tags start counting; a message from before is never answered.
  assert.equal(await noticed(env, null, { msgId: "old", thread: "T1", asker: { id: "1" }, at: "2000-01-01T00:00:00Z" }), false);
  assert.equal(await noticed(env, null, { msgId: "q1", thread: "T1", asker: { id: "111111", name: "Kari" } }), true);
  assert.equal(posts.length, 1);
  assert.equal(posts[0].username, "R'th AI");
  assert.equal(posts[0].content, OFFLINE);
  assert.deepEqual(posts[0].allowed_mentions, { parse: [] });
  assert.equal(await noticed(env, null, { msgId: "q1", thread: "T1" }), false);
  await sweep(env);
  assert.equal(posts.length, 1, "offline is said once, however often it's offered again");
  assert.deepEqual((await waiting(env)).map((w) => w.id), ["q1"]);
});

await test("one claim wins; it can be renewed and finished; a lapsed one can be taken again", async () => {
  const req = (route, body) => mentions(new Request("https://h" + route, { method: "POST", body: JSON.stringify(body) }), env, new URL("https://h" + route));
  const a = await req("/mentions/claim", { id: "q1", conn: "A" });
  assert.deepEqual(a, { ok: true, kind: "forum", thread: "T1", asker: { id: "111111", name: "Kari" } });
  assert.equal((await req("/mentions/claim", { id: "q1", conn: "B" })).status, 409);
  assert.deepEqual(await req("/mentions/renew", { id: "q1", conn: "A" }), { ok: true });
  assert.equal((await req("/mentions/renew", { id: "q1", conn: "B" })).status, 409);
  await env.DB.prepare("UPDATE mentions SET until = 1 WHERE msg_id = 'q1'").run(); // A went quiet
  assert.equal((await req("/mentions/claim", { id: "q1", conn: "B" })).ok, true);
  assert.deepEqual(await req("/mentions/done", { id: "q1", conn: "B" }), { ok: true });
  assert.deepEqual(await waiting(env), []);
});

await test("with a Codex that answers, the tag goes to it and nothing is said", async () => {
  answerers = 1;
  const before = posts.length;
  assert.equal(await noticed(env, null, { msgId: "q2", thread: "T1", asker: { id: "2" } }), true);
  assert.equal(posts.length, before);
  assert.deepEqual(asked.at(-1), { type: "ask", id: "q2", thread: "T1" });
});

console.log(`mentions: ${passed} passed`);
