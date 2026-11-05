// R'th AI's tools (src/ai-tools.js) on a throwaway GDD made from the real How the GDD Works: links in by
// meaning, queries, outlines, checks, making and renaming pages, reasons and summaries; and the MCP
// server (src/mcp-server.js) round trip: Claude's way in, through the engine, with a turn's key.
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

process.env.CODEX_AI = "openrouter";
const { makeAssistant } = await import("../src/assistant.js");

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REAL = path.join(HERE, "..", "..", "..", "docs", "gdd");
const repo = fs.mkdtempSync(path.join(os.tmpdir(), "codex-tools-"));
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }
const put = (rel, text) => { fs.mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true }); fs.writeFileSync(path.join(repo, rel), text); };
const get = (rel) => { try { return fs.readFileSync(path.join(repo, rel), "utf8"); } catch { return null; } };

put("docs/gdd/how-the-gdd-works.md", fs.readFileSync(path.join(REAL, "how-the-gdd-works.md"), "utf8").replace(/\r\n/g, "\n"));
for (const t of ["entity", "item", "rewards"]) put(`docs/gdd/templates/${t}.md`, fs.readFileSync(path.join(REAL, "templates", t + ".md"), "utf8").replace(/\r\n/g, "\n"));
put("docs/gdd/entities/fen-rat.md", "---\ntype: entity\ncomponents: [rewards]\nstatus: draft\n---\n\n# Fen Rat\n\n## What it is\n\nA rat of the fens. Teh rats bite.\n\n## Rewards\n\n| Item | Amount | Chance |\n| --- | --- | --- |\n| [Rat hide](../items/rat-hide.md) | 1 | Always |\n| [Rat gall](../items/rat-gall.md) | 1 | 1/8 |\n");
put("docs/gdd/items/rat-hide.md", "---\ntype: item\nstatus: stub\n---\n\n# Rat Hide\n\n## What it is\n\nA hide, skinned from a rat.\n");
put("docs/gdd/skills/skinning.md", "---\ntype: skill\nstatus: stub\n---\n\n# Skinning\n\n## What it is\n\nSkinning takes the [rat hide](../items/rat-hide.md) off a carcass.\n");

const all = () => { const out = []; const walk = (d) => { for (const e of fs.readdirSync(path.join(repo, d), { withFileTypes: true })) { const r = d + "/" + e.name; if (e.isDirectory()) walk(r); else if (r.endsWith(".md")) out.push(r); } }; walk("docs/gdd"); return out; };
const moved = [];
const store = { load: async (p) => (p === "docs/gdd/items/rat-hide.md" ? [{ id: "n-1", status: "open", anchor: { quote: "A hide" }, thread: [{ by: "person", who: { name: "Kari" }, text: "Which rats?" }] }] : []), move: async (id, to) => { moved.push([id, to]); return {}; } };
const written = [];
const ai = makeAssistant({
  repo, dir: path.join(repo, ".local", "conv"), store, emit: () => {},
  pages: async () => all(),
  read: async (p) => get(p),
  write: async (p, t) => { if (t === null) fs.rmSync(path.join(repo, p), { force: true }); else put(p, t); },
  spell: { check: async (words) => words.filter((w) => w === "Teh") },
  ownChanged: (rel) => written.push(rel),
});
const run = (name, args, turn = null) => ai.tools.run(name, args, turn);

await test("links in, named by what the link means there", async () => {
  const out = await run("links_in", { path: "docs/gdd/items/rat-hide.md" });
  assert.match(out, /Dropped by:\n  - Fen Rat \(docs\/gdd\/entities\/fen-rat\.md:\d+, in Rewards\)/);
  assert.match(out, /Linked from:\n  - Skinning/);
  assert.match(await ai.lookUp("links_to", { path: "docs/gdd/items/rat-hide.md" }), /Fen Rat/, "the old name still answers");
});

await test("queries, outlines, types, links out", async () => {
  assert.match(await run("query_gdd", { query: "type: item" }), /Rat Hide \(docs\/gdd\/items\/rat-hide\.md\), item, stub/);
  assert.match(await run("query_gdd", { query: "rewards.item: rat-hide" }), /rewards instances:\n- Fen Rat/);
  const outline = await run("page_outline", { path: "docs/gdd/entities/fen-rat.md" });
  assert.match(outline, /Frontmatter: type: entity; components: rewards; status: draft/);
  assert.match(outline, /- Rewards \(line \d+\): /);
  assert.match(await run("gdd_types", {}), /- entity: .*folder docs\/gdd\/entities/);
  assert.match(await run("links_out", { path: "docs/gdd/entities/fen-rat.md" }), /rat-gall\.md \(not written yet\)/);
  assert.match(await run("read_page", { path: "docs/gdd/entities/fen-rat.md", section: "rewards" }), /^## Rewards \(line \d+\)\n\| Item/);
});

await test("checking a page: structure, broken links, spelling", async () => {
  const out = await run("check_page", { path: "docs/gdd/entities/fen-rat.md" });
  assert.match(out, /rat-gall\.md, which isn't written yet/);
  assert.match(out, /"Teh" isn't a word/);
  const ok = await ai.tools.problemsOn("docs/gdd/items/rat-hide.md");
  assert.ok(!ok.some((p) => p.kind === "spelling"));
});

await test("comments on a page", async () => {
  assert.match(await run("comments_on", { path: "docs/gdd/items/rat-hide.md" }), /Thread n-1 on "A hide":\n  Kari: Which rats\?/);
});

await test("making a page from its template, never over a name the GDD uses", async () => {
  const out = await run("create_page", { type: "item", title: "Rat Gall" }, { provider: "claude", mode: "work", reasons: [] });
  assert.match(out, /^Made docs\/gdd\/items\/rat-gall\.md/);
  assert.match(get("docs/gdd/items/rat-gall.md"), /^---\ntype: item[\s\S]*# Rat Gall/);
  assert.match(await run("create_page", { type: "entity", title: "Rat gall" }, { provider: "claude" }), /already a page named "rat-gall"/);
  assert.match(await run("create_page", { type: "spell", title: "X" }, { provider: "claude" }), /isn't a type/);
  assert.match(await run("create_page", { type: "item", title: "X" }, { provider: "openrouter" }), /isn't available here/, "OpenRouter keeps its edit blocks");
});

await test("renaming: only in Work mode; references and comments follow", async () => {
  assert.match(await run("rename_page", { path: "docs/gdd/items/rat-hide.md", title: "Rat Pelt" }, { provider: "claude", mode: "chat" }), /only done in Work mode/);
  const out = await run("rename_page", { path: "docs/gdd/items/rat-hide.md", title: "Rat Pelt" }, { provider: "claude", mode: "work" });
  assert.match(out, /Renamed to "Rat Pelt" at docs\/gdd\/items\/rat-pelt\.md\. Rewrote what points at it in 2 pages/);
  assert.equal(get("docs/gdd/items/rat-hide.md"), null);
  assert.match(get("docs/gdd/items/rat-pelt.md"), /^# Rat Pelt$/m);
  assert.match(get("docs/gdd/entities/fen-rat.md"), /\(\.\.\/items\/rat-pelt\.md\)/);
  assert.deepEqual(moved, [["n-1", "docs/gdd/items/rat-pelt.md"]]);
});

await test("reasons go with the turn; summaries are written for everyone and found by search", async () => {
  const turn = { provider: "claude", mode: "work", reasons: [], problems: [] };
  await run("explain_change", { page: "docs/gdd/entities/fen-rat.md", quote: "A rat of the fens", reason: "Plainer" }, turn);
  assert.deepEqual(turn.reasons, [{ page: "docs/gdd/entities/fen-rat.md", quote: "A rat of the fens", reason: "Plainer" }]);
  assert.match(await run("report_problem", { kind: "contradiction", pages: ["docs/gdd/entities/fen-rat.md"], summary: "x", plan: "y" }, turn), /health check only/);
  assert.match(await run("search_gdd", { query: "vermin" }), /Nothing/);
  assert.equal(await run("index_page", { page: "docs/gdd/entities/fen-rat.md", summary: "A small marsh creature that drops pelts.", terms: ["vermin", "marsh"] }, turn), "Saved.");
  assert.deepEqual(written, ["docs/.codex/index/entities/fen-rat.json"]);
  assert.match(await run("search_gdd", { query: "vermin" }), /fen-rat\.md:\d+ \(Fen Rat[^\n]*\n[^\n]*\n  Page summary: A small marsh creature/);
  assert.match(await run("page_outline", { path: "docs/gdd/entities/fen-rat.md" }), /Summary: A small marsh creature/);
  put("docs/gdd/entities/fen-rat.md", get("docs/gdd/entities/fen-rat.md") + "\nMore.\n");
  assert.match(await run("page_outline", { path: "docs/gdd/entities/fen-rat.md" }), /Summary \(written before the page last changed\)/);
});

await test("OpenRouter is offered reading tools only; Claude the rest, report_problem only when auditing", async () => {
  const names = ai.tools.functions("openrouter").map((f) => f.function.name);
  assert.ok(names.includes("search_code") && names.includes("links_in") && !names.includes("create_page") && !names.includes("explain_change"));
  const t = ai.newTurn({ conv: "c-x", mode: "work" });
  const listed = ai.toolList({ token: t.key }).tools.map((x) => x.name);
  assert.ok(listed.includes("create_page") && listed.includes("explain_change") && !listed.includes("report_problem") && !listed.includes("search_code"));
  const audit = ai.newTurn({ conv: null, audit: true, readOnly: true });
  const auditing = ai.toolList({ token: audit.key }).tools.map((x) => x.name);
  assert.ok(auditing.includes("report_problem") && !auditing.includes("create_page"));
  assert.deepEqual(ai.toolList({ token: "nope" }), { error: "That turn is over." });
  ai.endTurn(t); ai.endTurn(audit);
});

await test("the MCP server: initialize, list and call, through the engine with the turn's key", async () => {
  const turn = ai.newTurn({ conv: "c-y", mode: "work" });
  const server = http.createServer(async (req, res) => {
    let b = ""; for await (const c of req) b += c;
    const body = JSON.parse(b || "{}");
    const r = req.url === "/api/ai/tools" ? ai.toolList(body) : await ai.tool(body);
    res.writeHead(r.error ? 403 : 200, { "Content-Type": "application/json" }); res.end(JSON.stringify(r));
  });
  await new Promise((r) => server.listen(0, "127.0.0.1", r));
  const url = `http://127.0.0.1:${server.address().port}`;
  const proc = spawn(process.execPath, [path.join(HERE, "..", "src", "mcp-server.js")], { env: { ...process.env, CODEX_URL: url, CODEX_TOKEN: turn.key }, stdio: ["pipe", "pipe", "inherit"] });
  const replies = new Map();
  let buf = "";
  proc.stdout.on("data", (d) => { buf += d; let i; while ((i = buf.indexOf("\n")) >= 0) { const m = JSON.parse(buf.slice(0, i)); buf = buf.slice(i + 1); replies.get(m.id)?.(m); } });
  let n = 0;
  const call = (method, params) => new Promise((resolve) => { const id = ++n; replies.set(id, resolve); proc.stdin.write(JSON.stringify({ jsonrpc: "2.0", id, method, params }) + "\n"); });
  try {
    const init = await call("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "test", version: "1" } });
    assert.equal(init.result.serverInfo.name, "codex");
    proc.stdin.write(JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" }) + "\n");
    const list = await call("tools/list", {});
    assert.ok(list.result.tools.some((t) => t.name === "links_in" && t.inputSchema.type === "object"));
    const got = await call("tools/call", { name: "links_in", arguments: { path: "docs/gdd/items/rat-pelt.md" } });
    assert.match(got.result.content[0].text, /Dropped by:/);
    const why = await call("tools/call", { name: "explain_change", arguments: { page: "docs/gdd/items/rat-pelt.md", quote: "Rat Pelt", reason: "Renamed" } });
    assert.equal(why.result.content[0].text, "Noted.");
    assert.equal(turn.reasons.length, 1, "the reason reached the turn that asked");
    ai.endTurn(turn);
    const over = await call("tools/call", { name: "links_in", arguments: { path: "docs/gdd/items/rat-pelt.md" } });
    assert.equal(over.result.isError, true);
  } finally { proc.kill(); server.close(); }
});

await test("Claude's MCP config for a turn: the Codex's server only, the engine's address and the turn's key", async () => {
  const noUrl = ai.mcpConfig(ai.newTurn({}));
  assert.equal(noUrl, null, "no engine address, no tools");
  const ai2 = makeAssistant({ repo, dir: path.join(repo, ".local", "conv2"), store: null, emit: () => {}, pages: async () => all(), read: async (p) => get(p), write: async () => {}, engineUrl: () => "http://127.0.0.1:1234" });
  const t = ai2.newTurn({});
  const file = ai2.mcpConfig(t);
  const cfg = JSON.parse(fs.readFileSync(file, "utf8")).mcpServers.codex;
  assert.equal(cfg.env.CODEX_URL, "http://127.0.0.1:1234");
  assert.equal(cfg.env.CODEX_TOKEN, t.key);
  assert.ok(fs.existsSync(cfg.args[0]) && cfg.args[0].endsWith("mcp-server.mjs"));
  ai2.endTurn(t);
  assert.ok(!fs.existsSync(file), "cleaned up when the turn ends");
});

console.log(`ai tools: ${passed} passed`);
