// Answering @R'th AI on a Codex (src/discord-answer.js), against a pretend helper: claim first, answer
// from the thread, reply as R'th AI tagging the asker, then done; another Codex's claim means no answer.
import assert from "node:assert/strict";
import http from "node:http";
import { makeDiscordAnswers } from "../src/discord-answer.js";

let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

const calls = [];
let claimed = false;
const helper = http.createServer(async (req, res) => {
  let b = ""; for await (const c of req) b += c;
  const body = JSON.parse(b || "{}");
  calls.push([req.url, body, req.headers.authorization]);
  let out = { ok: true }, status = 200;
  if (req.url === "/mentions/claim") { if (claimed) { status = 409; out = { error: "Someone else is answering it." }; } else { claimed = true; out = { ok: true, thread: "T1", asker: { id: "111111", name: "Kari" } }; } }
  res.writeHead(status, { "Content-Type": "application/json" }); res.end(JSON.stringify(out));
});
await new Promise((r) => helper.listen(0, "127.0.0.1", r));
const url = `http://127.0.0.1:${helper.address().port}`;

const replies = [], asked = [];
const store = { fresh: async () => {}, find: async (id) => (id === "T1" ? { page: "docs/gdd/x.md", note: { id, thread: [{ by: "person", who: { name: "Kari" }, text: "@RthAI is this settled?" }] } } : null), reply: async (id, e) => { replies.push([id, e]); } };
const answers = makeDiscordAnswers({ helper: url, auth: async () => "Bearer s", store, enabled: () => true, answer: async (x) => { asked.push(x); return { text: "Yes: the page settles it." }; } });

await test("claimed, answered from the thread, replied as R'th AI tagging the asker, done", async () => {
  assert.deepEqual(await answers.handle({ id: "q1", thread: "T1" }), { ok: true });
  assert.equal(asked[0].page, "docs/gdd/x.md");
  assert.equal(asked[0].asker.name, "Kari");
  assert.deepEqual(replies, [["T1", { by: "agent", text: "Yes: the page settles it.", ping: "111111" }]]);
  assert.deepEqual(calls.map((c) => c[0]), ["/mentions/claim", "/mentions/done"]);
  assert.equal(calls[0][1].conn, answers.conn);
  assert.equal(calls[0][2], "Bearer s");
});

await test("another Codex has it: nothing said", async () => {
  const r = await answers.handle({ id: "q1", thread: "T1" });
  assert.equal(r.skipped, true);
  assert.equal(replies.length, 1);
});

await test("not answering here (AI setup): not even claimed", async () => {
  const off = makeDiscordAnswers({ helper: url, auth: async () => "Bearer s", store, enabled: () => false, answer: async () => ({ text: "x" }) });
  const n = calls.length;
  assert.equal((await off.handle({ id: "q2", thread: "T1" })).skipped, true);
  assert.equal(calls.length, n);
});

helper.close();
console.log(`discord answers: ${passed} passed`);
