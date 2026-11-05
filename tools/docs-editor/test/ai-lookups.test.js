// OpenRouter's lookups (src/assistant.js, lookUp) on a throwaway repository, and the provider's loop that
// runs them (src/openrouter-provider.js) against a pretend OpenRouter: a tool call, then an answer; and a
// model that can't use tools, which answers without them.
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

process.env.CODEX_AI = "openrouter";
const { makeAssistant } = await import("../src/assistant.js");
const { openRouterProvider } = await import("../src/openrouter-provider.js");

const repo = fs.mkdtempSync(path.join(os.tmpdir(), "codex-lookups-"));
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }
const put = (rel, text) => { fs.mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true }); fs.writeFileSync(path.join(repo, rel), text); };
put("docs/gdd/systems/bile.md", "# Bile\n\nBile drifts back to zero out of combat.\n");
put("docs/gdd/skills/occultism.md", "# Occultism\n\nLeans [bile](../systems/bile.md).\n");
put("server/Combat/Bile.cs", "class Bile {\n  const int SettlePerTick = 2; // bile drifts back\n}\n");
put("tools/docs-editor/helper/.dev.vars", "AGENT_KEY=secret\n");
execFileSync("git", ["init", "-q"], { cwd: repo }); execFileSync("git", ["add", "-A"], { cwd: repo });

const ai = makeAssistant({
  repo, dir: path.join(repo, ".conv"), store: null, emit: () => {},
  pages: async () => ["docs/gdd/systems/bile.md", "docs/gdd/skills/occultism.md"],
  read: async (p) => { try { return fs.readFileSync(path.join(repo, p), "utf8"); } catch { return null; } },
  write: async () => {}, linksTo: async (p) => (p.endsWith("bile.md") ? [{ path: "docs/gdd/skills/occultism.md" }] : []),
});

await test("searching and reading the GDD", async () => {
  assert.match(await ai.lookUp("search_gdd", { query: "drifts zero" }), /docs\/gdd\/systems\/bile\.md:1 \(Bile\)\n  Bile drifts back to zero/);
  assert.match(await ai.lookUp("read_page", { path: "docs/gdd/skills/occultism.md" }), /^# Occultism/);
  assert.match(await ai.lookUp("read_page", { path: "server/Combat/Bile.cs" }), /Only GDD pages/);
  assert.match(await ai.lookUp("links_to", { path: "docs/gdd/systems/bile.md" }), /occultism\.md/);
});

await test("searching and reading the game's code, never the docs through it, never secrets", async () => {
  const hits = await ai.lookUp("search_code", { query: "SettlePerTick" });
  assert.match(hits, /server\/Combat\/Bile\.cs:2:/);
  assert.doesNotMatch(await ai.lookUp("search_code", { query: "drifts back" }), /docs\//, "the docs aren't code");
  assert.match(await ai.lookUp("read_file", { path: "server/Combat/Bile.cs", from: 2, lines: 1 }), /^2:\s+const int SettlePerTick = 2;/);
  assert.match(await ai.lookUp("read_file", { path: "tools/docs-editor/helper/.dev.vars" }), /can't be read/);
  assert.match(await ai.lookUp("read_file", { path: "../outside.txt" }), /can't be read/);
  assert.match(await ai.lookUp("read_file", { path: "docs/gdd/systems/bile.md" }), /Use read_page/);
});

// A pretend OpenRouter: answers in turn, as server-sent events.
const sse = (events) => new Response(events.map((e) => "data: " + JSON.stringify(e) + "\n\n").join("") + "data: [DONE]\n\n", { status: 200 });
function pretend(replies) {
  const sent = [];
  globalThis.fetch = async (url, opts) => { sent.push(JSON.parse(opts.body)); return replies.shift()(); };
  return sent;
}

await test("a tool call, its result handed back, then the answer", async () => {
  const sent = pretend([
    () => sse([{ choices: [{ delta: { tool_calls: [{ index: 0, id: "c1", function: { name: "search_code", arguments: "{\"query\":" } }] } }] },
               { choices: [{ delta: { tool_calls: [{ index: 0, function: { arguments: "\"SettlePerTick\"}" } }] } }] }]),
    () => sse([{ model: "free/x", choices: [{ delta: { content: "The code settles bile by 2 a tick." } }] }]),
  ]);
  const used = [], acts = [];
  const p = openRouterProvider({ key: () => "k" });
  const res = await p.run({ messages: [{ role: "user", content: "How fast?" }], onText: () => {}, onActivity: (a) => acts.push(a),
    tools: [{ type: "function", function: { name: "search_code" } }], useTool: async (n, a) => { used.push([n, a]); return "server/Combat/Bile.cs:2: SettlePerTick = 2"; }, describe: (n) => "Searching " + n }).done;
  assert.equal(res.text, "The code settles bile by 2 a tick.");
  assert.deepEqual(used, [["search_code", { query: "SettlePerTick" }]]);
  assert.ok(acts.includes("Searching search_code"));
  assert.equal(sent.length, 2, "two requests: the lookup, then the answer");
  assert.deepEqual(sent[1].messages.slice(-2).map((m) => m.role), ["assistant", "tool"]);
  assert.match(sent[1].messages.at(-1).content, /SettlePerTick = 2/);
});

await test("a model that can't use tools answers without them, as before", async () => {
  const sent = pretend([
    () => new Response(JSON.stringify({ error: { message: "No endpoints found that support tool use" } }), { status: 404 }),
    () => sse([{ choices: [{ delta: { content: "Without lookups." } }] }]),
  ]);
  const p = openRouterProvider({ key: () => "k" });
  const res = await p.run({ messages: [{ role: "user", content: "Hi" }], onText: () => {}, onActivity: () => {},
    tools: [{ type: "function", function: { name: "search_gdd" } }], useTool: async () => "" }).done;
  assert.equal(res.text, "Without lookups.");
  assert.ok(sent[0].tools && !sent[1].tools, "asked again without tools");
});

await test("never more than the allowed rounds of lookups: the last request can only answer", async () => {
  const call = () => sse([{ choices: [{ delta: { tool_calls: [{ index: 0, id: "c", function: { name: "search_gdd", arguments: "{\"query\":\"x\"}" } }] } }] }]);
  const sent = pretend([call, call, call, call, () => sse([{ choices: [{ delta: { content: "Done." } }] }])]);
  const p = openRouterProvider({ key: () => "k" });
  const res = await p.run({ messages: [{ role: "user", content: "?" }], onText: () => {}, onActivity: () => {},
    tools: [{ type: "function", function: { name: "search_gdd" } }], useTool: async () => "nothing" }).done;
  assert.equal(res.text, "Done.");
  assert.equal(sent.length, 5);
  assert.ok(!sent[4].tools, "the fifth request offers no tools");
});

await test('a call written into the text is read as one; a garbled one means answering without lookups', async () => {
  const sent = pretend([
    () => sse([{ choices: [{ delta: { content: '<tool_call>{\"name\": \"read_page\", \"arguments\": {\"path\": \"docs/gdd/systems/bile.md\"}}</tool_call>' } }] }]),
    () => sse([{ choices: [{ delta: { content: 'Read it.' } }] }]),
    () => sse([{ choices: [{ delta: { content: '<tool_call>' } }] }]),
    () => sse([{ choices: [{ delta: { content: 'Answered without lookups.' } }] }]),
  ]);
  const used = [];
  const p = openRouterProvider({ key: () => 'k' });
  const tools = [{ type: 'function', function: { name: 'read_page' } }];
  const a = await p.run({ messages: [{ role: 'user', content: '?' }], onText: () => {}, onActivity: () => {}, tools, useTool: async (n, x) => { used.push([n, x]); return '# Bile'; } }).done;
  assert.equal(a.text, 'Read it.');
  assert.deepEqual(used, [['read_page', { path: 'docs/gdd/systems/bile.md' }]]);
  const b = await p.run({ messages: [{ role: 'user', content: '?' }], onText: () => {}, onActivity: () => {}, tools, useTool: async () => '' }).done;
  assert.equal(b.text, 'Answered without lookups.', 'the garbled call is never the answer');
  assert.ok(sent[2].tools && !sent[3].tools);
});

fs.rmSync(repo, { recursive: true, force: true });
console.log(`ai lookups: ${passed} passed`);
