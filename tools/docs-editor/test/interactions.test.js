// /ask and "Ask R'th AI" (helper/src/interactions.js), on an in-memory SQLite standing in for D1, a pretend
// room and a fetch that records what would go to Discord: signatures checked, "thinking…" at once, the
// question kept as a channel tag, "offline" in place of "thinking…" when no Codex answers, and the answer
// in place of "thinking…" (fresh) or as a reply that tags whoever asked (later).
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { DatabaseSync } from "node:sqlite";
import { fileURLToPath } from "node:url";
import { interaction, verified, pieces } from "../helper/src/interactions.js";
import { mentions, OFFLINE } from "../helper/src/mentions.js";

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
  return { prepare: prep };
}

// Discord, as far as these calls go: what was sent, by method and path.
const sent = [];
let n = 0;
globalThis.fetch = async (url, opts = {}) => {
  const u = new URL(url), method = opts.method || "GET";
  sent.push({ method, path: u.pathname, body: opts.body ? JSON.parse(opts.body) : null });
  if (method === "GET" && /\/channels\/C1$/.test(u.pathname)) return Response.json({ id: "C1", name: "design-talk" });
  if (method === "GET" && /\/channels\/C1\/messages$/.test(u.pathname)) return Response.json([{ id: "2", content: "and the bile axis?", author: { username: "ola" } }, { id: "1", content: "how does occultism work", author: { username: "kari" } }]);
  return Response.json({ id: "msg" + ++n });
};

const keys = await crypto.subtle.generateKey({ name: "Ed25519" }, true, ["sign", "verify"]);
const pub = Buffer.from(await crypto.subtle.exportKey("raw", keys.publicKey)).toString("hex");
let answerers = 0;
const env = { DB: d1(), DISCORD_APP_ID: "APP", DISCORD_PUBLIC_KEY: pub, DISCORD_BOT_TOKEN: "bot",
  LIVE: { idFromName: () => "codex", get: () => ({ fetch: async () => Response.json({ sent: answerers > 0 }) }) } };

async function call(payload, { sign = true } = {}) {
  const body = JSON.stringify(payload), ts = String(Math.floor(Date.now() / 1000));
  const sig = Buffer.from(await crypto.subtle.sign("Ed25519", keys.privateKey, new TextEncoder().encode(ts + body))).toString("hex");
  const jobs = [];
  const res = await interaction(new Request("https://h/interactions", { method: "POST", body, headers: { "x-signature-ed25519": sign ? sig : "00".repeat(64), "x-signature-timestamp": ts } }), env, { waitUntil: (p) => jobs.push(p) });
  await Promise.all(jobs);
  return res;
}
const ask = (id, question) => ({ type: 2, id, token: "tok-" + id, channel_id: "C1", member: { nick: "Kari", user: { id: "111", username: "kari" } }, data: { type: 1, name: "ask", options: [{ name: "question", value: question }] } });
const req = (route, body) => mentions(new Request("https://h" + route, { method: "POST", body: JSON.stringify(body) }), env, new URL("https://h" + route));
const row = (id) => env.DB.prepare("SELECT * FROM mentions WHERE msg_id = ?").bind(id).first();

await test("only Discord's signature gets in; its check is answered", async () => {
  assert.equal((await call({ type: 1 }, { sign: false })).status, 401);
  assert.deepEqual(await (await call({ type: 1 })).json(), { type: 1 });
  assert.equal(await verified(env, "x", "zz", "1"), false);
});

await test("/ask with no Codex open: thinking at once, then offline in its place, kept to answer later", async () => {
  answerers = 0; sent.length = 0;
  const r = await (await call(ask("A1", "How does the bile axis work?"))).json();
  assert.deepEqual(r, { type: 5 });
  const m = await row("iA1");
  assert.equal(m.kind, "channel"); assert.equal(m.thread, "C1"); assert.equal(m.token, "tok-A1"); assert.equal(m.question, "How does the bile axis work?");
  assert.equal(m.told, 1);
  const edit = sent.find((x) => x.method === "PATCH");
  assert.equal(edit.path, "/api/v10/webhooks/APP/tok-A1/messages/@original");
  assert.equal(edit.body.content, OFFLINE);
  assert.equal(m.ref_msg, "msg" + n, "the offline line is what the answer replies to");
});

await test("the Codex claims it with the channel's context, and its late answer replies tagging the asker", async () => {
  sent.length = 0;
  const c = await req("/mentions/claim", { id: "iA1", conn: "X" });
  assert.equal(c.kind, "channel"); assert.equal(c.question, "How does the bile axis work?");
  assert.equal(c.channel.name, "design-talk");
  assert.deepEqual(c.channel.recent.map((x) => x.by), ["kari", "ola"], "oldest first");
  assert.equal((await req("/mentions/answer", { id: "iA1", conn: "Y", text: "No." })).status, 409, "only the claimer answers");
  sent.length = 0;
  assert.deepEqual(await req("/mentions/answer", { id: "iA1", conn: "X", text: "Bile leans between two poles." }), { ok: true });
  const post = sent.find((x) => x.method === "POST");
  assert.equal(post.path, "/api/v10/channels/C1/messages");
  assert.ok(post.body.content.startsWith("<@111> Bile"));
  assert.deepEqual(post.body.allowed_mentions.users, ["111"]);
  assert.equal(post.body.message_reference.message_id, (await row("iA1")).ref_msg);
  assert.equal((await row("iA1")).status, "answered");
});

await test("with a Codex open, the answer takes the place of thinking; Ask R'th AI asks about a message", async () => {
  answerers = 1; sent.length = 0;
  await call({ type: 2, id: "B1", token: "tok-B1", channel_id: "C1", member: { user: { id: "222", username: "ola" } },
    data: { type: 3, name: "Ask R'th AI", target_id: "M9", resolved: { messages: { M9: { id: "M9", content: "Is Defence a combat style?", author: { username: "kari" } } } } } });
  const m = await row("iB1");
  assert.equal(m.question, "kari wrote: Is Defence a combat style?");
  assert.equal(m.ref_msg, "M9"); assert.equal(m.told, 0);
  assert.ok(!sent.some((x) => x.method === "PATCH"), "nothing said while a Codex has it");
  await req("/mentions/claim", { id: "iB1", conn: "X" });
  sent.length = 0;
  await req("/mentions/answer", { id: "iB1", conn: "X", text: "No: Defence has abilities of no style." });
  assert.equal(sent.length, 1);
  assert.equal(sent[0].method, "PATCH"); assert.equal(sent[0].path, "/api/v10/webhooks/APP/tok-B1/messages/@original");
});

await test("long answers go in pieces Discord takes, split at paragraphs", () => {
  const text = ("A paragraph of words. ".repeat(40) + "\n\n").repeat(6);
  const ps = pieces(text);
  assert.ok(ps.length > 1 && ps.every((p) => p.length <= 1900));
  assert.equal(ps.join(" ").replace(/\s+/g, " ").trim(), text.replace(/\s+/g, " ").trim());
});

console.log(`interactions: ${passed} passed`);
