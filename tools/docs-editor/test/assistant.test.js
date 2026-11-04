// R'th AI's conversations (src/assistant.js): context kept by the conversation, pages added and dropped,
// /model and /effort, and the name its first answer gives the conversation. Without starting an AI: the
// commands never ask one, and the OpenRouter path is chosen so Claude isn't looked for.
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

process.env.CODEX_AI = "openrouter";
const { makeAssistant, titleIn, withoutTitle } = await import("../src/assistant.js");

const dir = fs.mkdtempSync(path.join(os.tmpdir(), "codex-ai-"));
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

const docs = { "docs/gdd/systems/bile.md": "# Bile\n", "docs/gdd/skills/occultism.md": "# Occultism\n[Bile](../systems/bile.md)\n", "docs/gdd/systems/phlegm.md": "# Phlegm\n" };
const ai = makeAssistant({
  repo: dir, dir: path.join(dir, "conversations"), store: null, emit: () => {},
  pages: async () => Object.keys(docs), read: async (p) => docs[p] ?? null, write: async () => {},
  linksTo: async (p) => (p === "docs/gdd/systems/bile.md" ? [{ path: "docs/gdd/skills/occultism.md", title: "Occultism" }] : []),
});
const BILE = "docs/gdd/systems/bile.md", PHLEGM = "docs/gdd/systems/phlegm.md";

await test("a conversation keeps the context it started with, whatever is on screen later", async () => {
  const r = await ai.send({ text: "/model some/model", context: { page: BILE, selection: { quote: "drifts back", section: ["Bile", "Rules"] } } });
  assert.equal(r.ok, true);
  let c = await ai.get(r.id);
  assert.equal(c.context.page, BILE);
  assert.equal(c.context.selection.quote, "drifts back");
  // A later message from another page: the screen's context is ignored, the pinned one stays.
  await ai.send({ id: r.id, text: "/model", context: { page: PHLEGM } });
  c = await ai.get(r.id);
  assert.equal(c.context.page, BILE);
});

await test("pages are added (@, + Add) and dropped on purpose; nothing outside the docs gets in", async () => {
  const r = await ai.send({ text: "/help", context: { page: BILE } });
  let x = await ai.setContext({ id: r.id, add: { page: PHLEGM } });
  assert.deepEqual(x.context.pages, [PHLEGM]);
  x = await ai.setContext({ id: r.id, add: { page: PHLEGM } });
  assert.deepEqual(x.context.pages, [PHLEGM], "not twice");
  x = await ai.setContext({ id: r.id, add: { page: "../secrets.md" } });
  assert.deepEqual(x.context.pages, [PHLEGM], "only pages in the docs");
  x = await ai.setContext({ id: r.id, drop: [PHLEGM, "selection"] });
  assert.deepEqual(x.context.pages, []);
  x = await ai.setContext({ id: r.id, drop: ["page"] });
  assert.equal(x.context.page, null);
  x = await ai.setContext({ id: r.id, add: { page: PHLEGM } });
  assert.equal(x.context.page, PHLEGM, "with no main page, an added page becomes it");
});

await test("/model and /effort set this conversation, as in Claude Code; they're answered by the Codex", async () => {
  const r = await ai.send({ text: "/model mistralai/some-model:free", context: {} });
  const c = await ai.get(r.id);
  assert.equal(c.settings.openrouterModel, "mistralai/some-model:free");
  assert.deepEqual(c.messages.map((m) => m.by), ["you", "codex"]);
  assert.match(c.messages[1].text, /some-model/);
  const e = await ai.send({ id: r.id, text: "/effort high" });
  assert.match(e.error, /Claude setting/, "effort isn't an OpenRouter thing");
  const u = await ai.send({ id: r.id, text: "/frobnicate" });
  assert.match(u.error, /Unknown command/);
});

await test("the first answer's <title> names the conversation and is never shown", () => {
  const a = "It drifts back at 2 a tick.\n\n<title>Bile settling speed.</title>";
  assert.equal(titleIn(a), "Bile settling speed");
  assert.equal(withoutTitle(a), "It drifts back at 2 a tick.");
  assert.equal(withoutTitle("Still writing <title>Bile sett"), "Still writing", "half a title while it streams");
  assert.equal(titleIn("No title given."), null);
});

await test("a suggested new page is created as the AI wrote it, once, and never over a page", async () => {
  const pages = { [BILE]: "# Bile\n" };
  const changedPages = [];
  const convs = path.join(dir, "conversations-new");
  const a = makeAssistant({
    repo: dir, dir: convs, store: null, emit: () => {},
    pages: async () => Object.keys(pages), read: async (p) => pages[p] ?? null,
    write: async (p, t) => { if (t === null) delete pages[p]; else pages[p] = t; }, changed: (ps) => changedPages.push(...ps),
  });
  const NEW = "docs/gdd/systems/yellow-bile.md", TEXT = "# Yellow bile\n\nHot and dry.\n";
  const save = (c) => fs.writeFileSync(path.join(convs, "c-new.json"), JSON.stringify(c));
  fs.mkdirSync(convs, { recursive: true });
  save({ id: "c-new", title: "t", created: "2026-11-04T00:00:00Z", updated: "2026-11-04T00:00:00Z", messages: [
    { by: "you", at: "2026-11-04T00:00:00Z", text: "suggest a page" },
    { by: "ai", at: "2026-11-04T00:00:01Z", text: "Here.", changes: [{ page: NEW, before: null, after: TEXT, places: 1, reviewed: false, suggest: true }] },
  ] });
  const r = await a.createSuggested({ conv: "c-new", msg: 1, page: NEW });
  assert.equal(r.ok, true);
  assert.equal(pages[NEW], TEXT);
  assert.deepEqual(changedPages, [NEW], "shared like any change");
  assert.equal((await a.get("c-new")).messages[1].changes[0].reviewed, true, "the card shows it done");
  assert.match((await a.createSuggested({ conv: "c-new", msg: 1, page: NEW })).error, /already handled/);
  // Made meanwhile by someone else: never written over.
  const c = await a.get("c-new");
  c.messages[1].changes[0].reviewed = false; delete c.run; save(c);
  pages[NEW] = "# Someone else's\n";
  assert.match((await a.createSuggested({ conv: "c-new", msg: 1, page: NEW })).error, /already a page/);
  assert.equal(pages[NEW], "# Someone else's\n");
});

fs.rmSync(dir, { recursive: true, force: true });
console.log(`assistant: ${passed} passed`);
