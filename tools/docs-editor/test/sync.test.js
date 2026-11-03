// Syncing, on throwaway repositories in a temp folder: a bare "GitHub", and two people's copies. The
// first copy also has uncommitted work outside docs/ (code, a scene), as the owner's copy does.
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { makeSync } from "../src/sync.js";

const root = fs.mkdtempSync(path.join(os.tmpdir(), "codex-sync-"));
const g = (cwd, ...args) => execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
const write = (dir, rel, text) => { fs.mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true }); fs.writeFileSync(path.join(dir, rel), text); };
const read = (dir, rel) => fs.readFileSync(path.join(dir, rel), "utf8");
const ident = (dir, name) => { g(dir, "config", "user.name", name); g(dir, "config", "user.email", name.toLowerCase() + "@example.com"); };

let passed = 0;
async function test(name, fn) { try { await fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

// The remote, seeded with a page and some code.
const remote = path.join(root, "remote.git");
g(root, "init", "--bare", "--initial-branch=main", remote);
const seed = path.join(root, "seed");
g(root, "clone", "--quiet", remote, seed); ident(seed, "Seed");
write(seed, "docs/combat.md", "# Combat\n\nFirst line.\n\nSecond line.\n");
write(seed, "docs/death.md", "# Death\n\nYou die.\n");
write(seed, "code/game.cs", "class Game {}\n");
g(seed, "add", "-A"); g(seed, "commit", "--quiet", "-m", "seed"); g(seed, "push", "--quiet", "origin", "main");

const a = path.join(root, "a"), b = path.join(root, "b");
g(root, "clone", "--quiet", remote, a); ident(a, "Owner");
g(root, "clone", "--quiet", remote, b); ident(b, "Friend");
write(a, "code/game.cs", "class Game { /* owner's uncommitted work */ }\n");
write(a, "client/scene.unity", "uncommitted new scene\n");
const sync = makeSync({ repo: a });

const lastFiles = (dir) => g(dir, "show", "--name-only", "--format=", "HEAD").split("\n").filter(Boolean);
const remoteFiles = () => g(remote, "show", "--name-only", "--format=", "main").split("\n").filter(Boolean);

await test("sends a page saved in the Codex, and only docs", async () => {
  write(a, "docs/combat.md", "# Combat\n\nFirst line, edited by the owner.\n\nSecond line.\n");
  sync.saved("docs/combat.md");
  const v = await sync.sync({ why: "auto" });
  assert.equal(v.problem, null);
  assert.deepEqual(remoteFiles(), ["docs/combat.md"]);
  assert.match(g(remote, "log", "-1", "--format=%s %an", "main"), /^Docs: Combat Owner$/);
  assert.match(g(a, "status", "--porcelain"), /code\/game\.cs/, "the owner's code change is still uncommitted");
  assert.match(read(a, "code/game.cs"), /owner's uncommitted work/);
  assert.ok(fs.existsSync(path.join(a, "client/scene.unity")));
});

await test("automatic sync leaves doc changes made outside the Codex; the button sends them", async () => {
  write(a, "docs/death.md", "# Death\n\nYou die. (An agent was here.)\n");
  let v = await sync.sync({ why: "auto" });
  assert.equal(v.waiting, 1);
  assert.equal(v.outside, 1);
  assert.doesNotMatch(g(remote, "show", "main:docs/death.md"), /agent/);
  v = await sync.sync({ everything: true, why: "button" });
  assert.equal(v.waiting, 0);
  assert.match(g(remote, "show", "main:docs/death.md"), /agent/);
  assert.match(g(a, "status", "--porcelain"), /code\/game\.cs/);
});

await test("takes in others' changes", async () => {
  g(b, "pull", "--quiet");
  write(b, "docs/combat.md", read(b, "docs/combat.md").replace("Second line.", "Second line, by a friend."));
  write(b, "docs/new-page.md", "# New page\n");
  g(b, "add", "-A"); g(b, "commit", "--quiet", "-m", "friend"); g(b, "push", "--quiet");
  const v = await sync.sync({ why: "auto" });
  assert.equal(v.problem, null);
  assert.equal(v.lastResult.got, 1);
  assert.match(read(a, "docs/combat.md"), /by a friend/);
  assert.match(read(a, "docs/combat.md"), /edited by the owner/);
  assert.ok(fs.existsSync(path.join(a, "docs/new-page.md")));
  assert.match(read(a, "code/game.cs"), /owner's uncommitted work/);
});

await test("the same line changed on both sides: nothing half-merged, the page named", async () => {
  g(b, "pull", "--quiet");
  write(b, "docs/death.md", "# Death\n\nYou die, says the friend.\n");
  g(b, "commit", "--quiet", "-am", "friend again"); g(b, "push", "--quiet");
  write(a, "docs/death.md", "# Death\n\nYou die, says the owner.\n");
  sync.saved("docs/death.md");
  const v = await sync.sync({ why: "auto" });
  assert.match(v.problem, /same lines of Death/);
  assert.ok(!fs.existsSync(path.join(a, ".git/MERGE_HEAD")), "no merge left in progress");
  assert.match(read(a, "docs/death.md"), /says the owner/);
  assert.doesNotMatch(read(a, "docs/death.md"), /<<<<<<<|>>>>>>>/);
  assert.match(read(a, "code/game.cs"), /owner's uncommitted work/);
  assert.deepEqual(v.clash, ["Death"]);
  // Settle it: keep mine. Merged, sent, and the code change still uncommitted.
  const r = await sync.resolve("mine");
  assert.equal(r.problem, null);
  assert.match(g(remote, "show", "main:docs/death.md"), /says the owner/);
  assert.match(read(a, "code/game.cs"), /owner's uncommitted work/);
  assert.equal(r.ahead, 0);
  assert.equal(r.behind, 0);
});

await test("others' changes touching the owner's uncommitted files: refused safely, work intact", async () => {
  g(b, "pull", "--quiet");
  write(b, "code/game.cs", "class Game { /* friend's code */ }\n");
  g(b, "commit", "--quiet", "-am", "code"); g(b, "push", "--quiet");
  const v = await sync.sync({ why: "auto" });
  assert.match(v.problem, /uncommitted changes/);
  assert.match(read(a, "code/game.cs"), /owner's uncommitted work/);
  assert.ok(!fs.existsSync(path.join(a, ".git/MERGE_HEAD")));
  assert.ok(fs.existsSync(path.join(a, "client/scene.unity")));
});

await test("the owner's own unsent commits are never sent, nor anything on top of them", async () => {
  g(a, "checkout", "--quiet", "--", "code/game.cs"); // (the previous test's file, back as on GitHub)
  g(a, "pull", "--quiet");
  write(a, "code/engine.cs", "class Engine {}\n");
  g(a, "add", "code/engine.cs"); g(a, "commit", "--quiet", "-m", "owner's work, not pushed yet");
  const before = g(remote, "rev-parse", "main");
  write(a, "docs/combat.md", read(a, "docs/combat.md") + "\nA new line.\n");
  sync.saved("docs/combat.md");
  const v = await sync.sync({ why: "auto" });
  assert.match(v.problem, /1 commit not on GitHub yet that the Codex didn't make/);
  assert.equal(g(remote, "rev-parse", "main"), before, "nothing was sent");
  assert.match(g(a, "log", "-1", "--format=%s"), /^Docs: Combat$/, "the page is still committed here");
  // Once the owner has pushed their own work, the Codex sends its own again.
  g(a, "push", "--quiet", "origin", "HEAD~1:main");
  const w = await sync.sync({ why: "button" });
  assert.equal(w.problem, null);
  assert.equal(w.ahead, 0);
  assert.match(g(remote, "show", "main:docs/combat.md"), /A new line/);
});

await test("says what others sent: a page made, one changed, one deleted, and by whom", async () => {
  const d = path.join(root, "d"), e = path.join(root, "e");
  g(root, "clone", "--quiet", remote, d); ident(d, "Me");
  g(root, "clone", "--quiet", remote, e); ident(e, "Kari (Discord)");
  write(e, "docs/fun.md", "# Just for fun\n\nHello.\n");
  write(e, "docs/combat.md", read(e, "docs/combat.md") + "\nKari was here.\n");
  fs.rmSync(path.join(e, "docs/death.md"));
  g(e, "add", "-A"); g(e, "commit", "--quiet", "-m", "Docs: fun"); g(e, "push", "--quiet", "origin", "main");
  const news = [];
  const v = await makeSync({ repo: d, emit: (type, data) => { if (type === "news") news.push(data); } }).sync({ why: "auto" });
  assert.equal(v.problem, null);
  assert.equal(news.length, 1);
  const by = Object.fromEntries(news[0].pages.map((x) => [x.path, x]));
  assert.deepEqual(by["docs/fun.md"], { path: "docs/fun.md", title: "Just for fun", status: "added", by: ["Kari"] });
  assert.equal(by["docs/combat.md"].status, "changed");
  assert.equal(by["docs/death.md"].status, "deleted");
});

await test("a page made or deleted is sent within seconds when automatic sync is on", async () => {
  const f = path.join(root, "f");
  g(root, "clone", "--quiet", remote, f); ident(f, "Quick");
  const s = makeSync({ repo: f });
  s.startAuto();
  write(f, "docs/quick.md", "# Quick\n");
  s.saved("docs/quick.md", { soon: true });
  await new Promise((r) => setTimeout(r, 9000));
  s.stopAuto();
  assert.match(g(remote, "show", "main:docs/quick.md"), /# Quick/);
});

await test("a branch with no remote says so instead of syncing", async () => {
  const c = path.join(root, "c");
  g(root, "init", "--quiet", "--initial-branch=main", c);
  const v = await makeSync({ repo: c }).sync({ why: "auto" });
  assert.equal(v.enabled, false);
  assert.match(v.reason, /isn't connected to GitHub/);
});

fs.rmSync(root, { recursive: true, force: true });
console.log(`sync: ${passed} passed`);
