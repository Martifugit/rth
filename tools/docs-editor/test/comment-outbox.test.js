// Comments that wait instead of failing (src/comment-store.js, the outbox), against a pretend Discord
// that can be switched off: down, refusing, and back.
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { makeCommentStore, passing } from "../src/comment-store.js";

const dir = fs.mkdtempSync(path.join(os.tmpdir(), "codex-outbox-"));
let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

/** A pretend helper/Discord: `down` fails like a server error, `refuse` like a 403. */
function discord() {
  const D = { notes: new Map(), down: false, refuse: false, calls: [], n: 0 };
  const fail = () => {
    if (D.down) { const e = new Error("The helper said 502"); e.status = 502; throw e; }
    if (D.refuse) { const e = new Error("You need the Codex role."); e.status = 403; throw e; }
  };
  const now = () => new Date().toISOString();
  D.api = {
    async changes() { fail(); return { comments: [...D.notes.values()].map((x) => structuredClone(x)), until: now() }; },
    async create(page, f) { fail(); D.calls.push(["create", f.text]); const id = "t" + ++D.n; const x = { page, note: { id, kind: f.kind || "note", status: "open", created: now(), updated: now(), anchor: {}, thread: [{ by: "owner", at: now(), text: f.text }], tags: [] } }; D.notes.set(id, x); return structuredClone(x); },
    async reply(id, e) { fail(); D.calls.push(["reply", id, e.text]); const x = D.notes.get(id); x.note.thread.push({ by: "owner", at: now(), text: e.text }); x.note.updated = now(); return structuredClone(x); },
    async update(id, p) { fail(); D.calls.push(["update", id]); const x = D.notes.get(id); Object.assign(x.note, p); return structuredClone(x); },
    async remove(id) { fail(); D.calls.push(["remove", id]); return D.notes.delete(id); },
    async move(id, page) { fail(); D.calls.push(["move", id, page]); const x = D.notes.get(id); x.page = page; return structuredClone(x); },
  };
  return D;
}
const account = { view: () => ({ signedIn: true, user: { id: "1", name: "Ibux" } }), authHeader: async () => "Bearer x" };
const local = { root: dir, fileFor: () => null, pageFor: () => null, lastWrites: new Map() };
function store(D, file, events = []) {
  return makeCommentStore({ repo: dir, local, account, emit: (type, data) => events.push([type, data]), titleOf: async (p) => p, watching: () => false, outboxFile: file, remote: D.api });
}
const P = "docs/gdd/combat.md";

await test("what's worth trying again", () => {
  assert.equal(passing({}), true, "no answer at all");
  assert.equal(passing({ status: 502 }), true);
  assert.equal(passing({ status: 429 }), true);
  assert.equal(passing({ status: 403 }), false);
  assert.equal(passing({ status: 404 }), false);
});

await test("Discord down: a comment and a reply to it wait, show, survive a restart, then go in order", async () => {
  const D = discord(), file = path.join(dir, "a.json");
  const s = store(D, file);
  D.down = true;
  const made = await s.create(P, { kind: "note", text: "Too fast?" });
  assert.match(made.note.id, /^pending-/);
  assert.equal(made.note.pending, true);
  await s.reply(made.note.id, { by: "owner", text: "Or too slow." });
  let shown = await s.load(P);
  assert.equal(shown.length, 1);
  assert.deepEqual(shown[0].thread.map((e) => [e.text, e.pending]), [["Too fast?", true], ["Or too slow.", true]]);
  assert.equal(JSON.parse(fs.readFileSync(file, "utf8")).length, 2, "kept on this computer");

  const again = store(D, file); // the Codex restarted, Discord still down
  assert.equal(again.waiting, 2);
  assert.equal((await again.load(P))[0].thread.length, 2);

  D.down = false;
  await again.flush();
  assert.equal(again.waiting, 0);
  assert.deepEqual(D.calls, [["create", "Too fast?"], ["reply", "t1", "Or too slow."]], "in order, the reply on Discord's own id");
  shown = await again.load(P);
  assert.equal(shown.length, 1, "no leftover pending copy");
  assert.equal(shown[0].id, "t1");
  assert.equal(shown[0].pending, undefined);
  assert.deepEqual(shown[0].thread.map((e) => e.text), ["Too fast?", "Or too slow."]);
});

await test("a reply waiting on an existing comment shows once, whatever Discord's copy says meanwhile", async () => {
  const D = discord(), file = path.join(dir, "b.json");
  const s = store(D, file);
  const made = await s.create(P, { kind: "note", text: "Bile cap?" });
  assert.equal(made.note.id, "t1", "Discord up: posted straight away");
  D.down = true;
  await s.reply("t1", { by: "owner", text: "100 feels right." });
  const shown = (await s.load(P))[0];
  assert.deepEqual(shown.thread.map((e) => e.text), ["Bile cap?", "100 feels right."]);
  assert.equal(shown.thread[1].pending, true);
  D.down = false;
  await s.flush();
  assert.deepEqual((await s.load(P))[0].thread.map((e) => e.text), ["Bile cap?", "100 feels right."], "once, not twice");
});

await test("nothing overtakes what's waiting", async () => {
  const D = discord(), file = path.join(dir, "c.json");
  const s = store(D, file);
  D.down = true;
  await s.create(P, { kind: "note", text: "First" });
  D.down = false;
  await s.create(P, { kind: "note", text: "Second" }); // Discord's back, but "First" is still waiting
  assert.equal(s.waiting, 2);
  await s.flush();
  assert.deepEqual(D.calls.map((c) => c[1]), ["First", "Second"]);
});

await test("a waiting comment deleted before it's posted is never sent", async () => {
  const D = discord(), file = path.join(dir, "d.json");
  const s = store(D, file);
  D.down = true;
  const made = await s.create(P, { kind: "note", text: "Oops" });
  await s.reply(made.note.id, { by: "owner", text: "and this" });
  assert.equal(await s.remove(made.note.id), true);
  assert.equal(s.waiting, 0);
  assert.equal((await s.load(P)).length, 0);
  D.down = false;
  await s.flush();
  assert.deepEqual(D.calls, []);
});

await test("refused outright while waiting: dropped and said once, the rest still goes", async () => {
  const D = discord(), file = path.join(dir, "e.json"), events = [];
  const s = store(D, file, events);
  D.down = true;
  await s.create(P, { kind: "note", text: "Refused" });
  D.down = false; D.refuse = true;
  await s.flush();
  assert.equal(s.waiting, 0, "not tried for ever");
  assert.equal(events.filter(([t]) => t === "notes-failed").length, 1);
});

await test("refused outright with nothing waiting: an error, as before (no queue)", async () => {
  const D = discord(), file = path.join(dir, "f.json");
  const s = store(D, file);
  D.refuse = true;
  await assert.rejects(() => s.create(P, { kind: "note", text: "No role" }), /Codex role/);
  assert.equal(s.waiting, 0);
});

fs.rmSync(dir, { recursive: true, force: true });
console.log(`comment outbox: ${passed} passed`);
process.exit(process.exitCode || 0); // the stores' next-look timers would keep it running
