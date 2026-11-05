// The Codex's MCP server for Claude Code (src/ai-tools.js): a few lines of JSON-RPC over stdin and stdout
// that hand every tool call to the running engine, which does the work. Claude Code starts it for each
// turn (--mcp-config, written by src/assistant.js) with the engine's address and that turn's key in its
// environment, so the tools act for the conversation that asked.
//
// Self-contained (Node's own modules only): the desktop app copies it out of its archive and runs it
// with its own Electron as Node (ELECTRON_RUN_AS_NODE), so nothing else needs to be installed.

import readline from "node:readline";

const URL_ = process.env.CODEX_URL || "http://127.0.0.1:5178";
const TOKEN = process.env.CODEX_TOKEN || "";
const PROTOCOL = "2025-06-18";

const send = (msg) => process.stdout.write(JSON.stringify({ jsonrpc: "2.0", ...msg }) + "\n");

async function engine(route, body) {
  const r = await fetch(URL_ + route, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ token: TOKEN, ...body }) });
  const j = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(j.error || `the Codex said ${r.status}`);
  return j;
}

async function handle(m) {
  switch (m.method) {
    case "initialize":
      return { protocolVersion: m.params?.protocolVersion || PROTOCOL, capabilities: { tools: {} }, serverInfo: { name: "codex", version: "1" },
        instructions: "The R'th Codex's own knowledge of the GDD: ranked search, outlines, links in by meaning, queries, checks, and ways to hand things back (reasons, summaries)." };
    case "ping": return {};
    case "tools/list": return { tools: (await engine("/api/ai/tools", {})).tools };
    case "tools/call": {
      try {
        const r = await engine("/api/ai/tool", { name: m.params?.name, args: m.params?.arguments || {} });
        return { content: [{ type: "text", text: String(r.text ?? "") }] };
      } catch (e) {
        return { content: [{ type: "text", text: "The Codex couldn't do that: " + e.message }], isError: true };
      }
    }
    default: throw Object.assign(new Error("Method not found: " + m.method), { code: -32601 });
  }
}

const rl = readline.createInterface({ input: process.stdin });
rl.on("line", async (line) => {
  if (!line.trim()) return;
  let m;
  try { m = JSON.parse(line); } catch { return send({ id: null, error: { code: -32700, message: "Parse error" } }); }
  if (m.id === undefined || m.id === null) return; // a notification (initialized, cancelled): nothing to answer
  try { send({ id: m.id, result: await handle(m) }); }
  catch (e) { send({ id: m.id, error: { code: e.code || -32603, message: e.message } }); }
});
rl.on("close", () => process.exit(0));
