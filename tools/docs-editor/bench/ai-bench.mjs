// R'th AI's benchmark: does it answer better with the Codex's tools than without? Runs the same questions
// and tasks through two versions of the assistant (src/assistant.js: this folder's, and an older copy
// given with --old), each on a fresh throwaway copy of the docs, and checks the answers and the pages
// against what the Codex itself works out. Uses Claude Code on this computer.
//
//   node bench/ai-bench.mjs --old <folder with an older src/> [--runs 2] [--model claude-sonnet-5-5]
//        [--effort low] [--only q-pennick,t-rename] [--jobs 3] [--out results.json]
//
// An older src/: git archive <commit> tools/docs-editor/src | tar -x -C <folder> (then <folder>/tools/docs-editor).
// Cases are in bench/cases.mjs. Each run's answer, time, cost and tool calls go to --out.

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import http from "node:http";
import { execFileSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";
import { CASES } from "./cases.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const TOOL = path.resolve(HERE, "..");
const REPO = path.resolve(TOOL, "..", "..");
const arg = (k, d = null) => { const i = process.argv.indexOf("--" + k); return i > 0 ? process.argv[i + 1] : d; };
const OLD = arg("old"), RUNS = Number(arg("runs", 2)), MODEL = arg("model", "claude-sonnet-5-5"), EFFORT = arg("effort", "low"), JOBS = Number(arg("jobs", 3));
const ONLY = arg("only") ? new Set(arg("only").split(",")) : null;
const OUT = arg("out", path.join(os.tmpdir(), "codex-bench-" + Date.now() + ".json"));
if (!OLD) { console.error("Give --old <folder holding an older tools/docs-editor/src>."); process.exit(2); }

const versions = {
  new: path.join(TOOL, "src"),
  old: fs.existsSync(path.join(OLD, "tools", "docs-editor", "src")) ? path.join(OLD, "tools", "docs-editor", "src") : path.join(OLD, "src"),
};
const { makeTools } = await import(pathToFileURL(path.join(versions.new, "ai-tools.js")));

// The docs as committed here, unpacked once; each run copies them.
const SNAP = fs.mkdtempSync(path.join(os.tmpdir(), "codex-bench-docs-"));
execFileSync("git", ["-C", REPO, "archive", "HEAD", "docs", "-o", path.join(SNAP, "docs.tar")]);
execFileSync("tar", ["-xf", "docs.tar"], { cwd: SNAP });
fs.rmSync(path.join(SNAP, "docs.tar"));
for (const d of ["docs/.codex/index", "docs/.codex/problems"]) fs.rmSync(path.join(SNAP, d), { recursive: true, force: true });

function fixture() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "codex-bench-run-"));
  fs.cpSync(path.join(SNAP, "docs"), path.join(dir, "docs"), { recursive: true });
  execFileSync("git", ["init", "-q"], { cwd: dir });
  return dir;
}
const walk = (repo) => { const out = []; const go = (d) => { let es = []; try { es = fs.readdirSync(path.join(repo, d), { withFileTypes: true }); } catch {} for (const e of es) { if (e.name.startsWith(".")) continue; const r = d + "/" + e.name; if (e.isDirectory()) go(r); else if (r.endsWith(".md")) out.push(r); } }; go("docs/gdd"); return out; };
const reader = (repo) => async (p) => { try { return fs.readFileSync(path.join(repo, p), "utf8").replace(/\r\n/g, "\n"); } catch { return null; } };
const writer = (repo) => async (p, t) => { const a = path.join(repo, p); if (t === null) { fs.rmSync(a, { force: true }); return; } fs.mkdirSync(path.dirname(a), { recursive: true }); fs.writeFileSync(a, t, "utf8"); };

// The engine's address for the new version's MCP server: one server, whichever assistant owns the turn.
const live = new Set();
const server = http.createServer(async (req, res) => {
  let b = ""; for await (const c of req) b += c;
  const body = JSON.parse(b || "{}");
  let out = { error: "That turn is over." };
  for (const ai of live) { const r = req.url === "/api/ai/tools" ? ai.toolList(body) : await ai.tool(body); if (!r.error) { out = r; break; } }
  res.writeHead(out.error ? 403 : 200, { "Content-Type": "application/json" }); res.end(JSON.stringify(out));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const URL_ = `http://127.0.0.1:${server.address().port}`;

const transcriptDirs = () => { const root = path.join(os.homedir(), ".claude", "projects"); try { return fs.readdirSync(root).map((d) => path.join(root, d)); } catch { return []; } };
function toolCalls(session) {
  if (!session) return null;
  for (const d of transcriptDirs()) {
    const f = path.join(d, session + ".jsonl");
    if (!fs.existsSync(f)) continue;
    const names = [...fs.readFileSync(f, "utf8").matchAll(/"type":"tool_use","id":"[^"]*","name":"([^"]+)"/g)].map((m) => m[1]);
    const by = {}; for (const n of names) by[n] = (by[n] || 0) + 1;
    return { total: names.length, by };
  }
  return null;
}

async function runOne(version, c, n) {
  const repo = fixture();
  if (c.setup) await c.setup(repo);
  const { makeAssistant } = await import(pathToFileURL(path.join(versions[version], "assistant.js")) + "?v=" + version);
  const tools = makeTools({ repo, pages: async () => walk(repo), read: reader(repo), write: async () => {} });
  const linksTo = async (p) => { const idx = await tools.engine(); const seen = new Map(); for (const l of (idx.in.get(p) || [])) if (!seen.has(l.from)) seen.set(l.from, { path: l.from, title: idx.titles.get(l.from) || null }); return [...seen.values()]; };
  let done;
  const finished = new Promise((r) => { done = r; });
  const ai = makeAssistant({
    repo, dir: path.join(repo, ".local", "conv"), store: null, pages: async () => walk(repo), read: reader(repo), write: writer(repo), linksTo,
    emit: (type, d) => { if (type === "ai" && d.state === "done") done(d); },
    ...(version === "new" ? { engineUrl: () => URL_ } : {}),
  });
  await ai.configure({ use: "claude", claudeModel: MODEL, claudeEffort: EFFORT });
  if (version === "new") live.add(ai);
  const t0 = Date.now();
  const sent = await ai.send({ text: c.ask, context: c.page ? { page: c.page } : {}, mode: c.mode || "chat" });
  if (sent.error) { live.delete(ai); return { version, case: c.id, run: n, error: sent.error }; }
  await finished;
  const secs = (Date.now() - t0) / 1000;
  live.delete(ai);
  const conv = await ai.get(sent.id);
  const msg = conv.messages.at(-1);
  const answer = msg.text || "";
  let check;
  try { check = await c.check({ answer, repo, read: reader(repo), pages: () => walk(repo), tools: makeTools({ repo, pages: async () => walk(repo), read: reader(repo), write: async () => {} }), changes: msg.changes || [] }); }
  catch (e) { check = { pass: false, notes: ["check failed: " + e.message] }; }
  const result = { version, case: c.id, run: n, pass: check.pass, notes: check.notes || [], secs: Math.round(secs), cost: msg.cost ?? null, tools: toolCalls(conv.session), error: msg.error || null, answer: answer.slice(0, 3000) };
  fs.rmSync(repo, { recursive: true, force: true });
  return result;
}

const jobs = [];
for (const c of CASES) if (!ONLY || ONLY.has(c.id)) for (let n = 1; n <= RUNS; n++) for (const v of ["old", "new"]) jobs.push([v, c, n]);
console.log(`${jobs.length} runs, ${JOBS} at a time, ${MODEL} at ${EFFORT} effort. Results: ${OUT}`);
const results = [];
let next = 0;
async function worker() {
  while (next < jobs.length) {
    const [v, c, n] = jobs[next++];
    const r = await runOne(v, c, n).catch((e) => ({ version: v, case: c.id, run: n, error: e.message }));
    results.push(r);
    console.log(`${r.pass ? "PASS" : "fail"}  ${v.padEnd(3)} ${c.id.padEnd(16)} #${n}  ${r.secs ?? "?"}s  $${r.cost ?? "?"}  tools ${r.tools?.total ?? "?"}${r.error ? "  ERROR " + r.error : ""}${!r.pass && r.notes?.length ? "  (" + r.notes.join("; ") + ")" : ""}`);
    fs.writeFileSync(OUT, JSON.stringify(results, null, 1));
  }
}
await Promise.all(Array.from({ length: JOBS }, worker));
server.close();

// The summary: per version, and per case.
const sum = (rs) => ({ runs: rs.length, passed: rs.filter((r) => r.pass).length, secs: Math.round(rs.reduce((a, r) => a + (r.secs || 0), 0) / (rs.length || 1)), cost: Math.round(rs.reduce((a, r) => a + (r.cost || 0), 0) * 1000) / 1000, tools: Math.round(rs.reduce((a, r) => a + (r.tools?.total || 0), 0) / (rs.length || 1) * 10) / 10 });
console.log("\nversion  passed  avg s  total $  avg tool calls");
for (const v of ["old", "new"]) { const s = sum(results.filter((r) => r.version === v)); console.log(`${v.padEnd(8)} ${s.passed}/${s.runs}`.padEnd(17) + `${s.secs}`.padEnd(7) + `${s.cost}`.padEnd(9) + s.tools); }
console.log("\ncase              old     new");
for (const c of CASES) { if (ONLY && !ONLY.has(c.id)) continue; const f = (v) => { const rs = results.filter((r) => r.version === v && r.case === c.id); return `${rs.filter((r) => r.pass).length}/${rs.length}`; }; console.log(c.id.padEnd(18) + f("old").padEnd(8) + f("new")); }
fs.rmSync(SNAP, { recursive: true, force: true });
