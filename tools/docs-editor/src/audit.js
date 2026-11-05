// The GDD's health check (PLAN: R'th AI's tools, step 6). While this Codex has Claude and nobody has
// done anything in it for a while, R'th AI reads the pages changed since they were last checked against
// the pages round them, and reports what's wrong across pages: contradictions, facts copied instead of
// linked, open questions another page has settled, things named but not linked, references gone stale.
// It changes nothing; each problem comes with a plan, ready for "Fix with R'th AI" on the To-do page.
//
// The problems are shared: one file per page, docs/.codex/problems/<path under docs/gdd>.json, sent like
// the word list (src/sync.js always sends docs/.codex/), so everyone sees them and anyone can fix them,
// with Claude or OpenRouter. The file also says which version of the page was checked (auditedHash), so
// two Codexes don't check the same change.
//
// What counts as changed: pages whose text differs from what was last checked and from how they were
// when this Codex first ran the check (its baseline, kept on this computer), so the first run doesn't
// read the whole GDD; "Check the whole GDD" on the To-do page does that on purpose, a few pages a run.

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { GDD, TEMPLATES, isMeta } from "./gdd.js";
import { textHash } from "./section-index.js";

export const PROBLEMS = "docs/.codex/problems";
const PER_RUN = 5;
export const KINDS = {
  "contradiction": "Contradiction",
  "copied-fact": "Copied, not linked",
  "settled-question": "Question settled elsewhere",
  "missing-link": "Named, not linked",
  "stale-reference": "Out of date",
};

/**
 * repo, dataDir: where the docs and this computer's state are. pages(), read(rel): the docs.
 * audit({ pages }): runs R'th AI's check on those pages → { problems, error } (src/assistant.js).
 * canRun(): Claude is here, the check is on, no answer is running. idleFor(): ms since anything was done.
 * who(): the person's name. ownChanged(rel): a problems file was written (sent). emit(type, data).
 */
export function makeAuditor({ repo, dataDir, pages, read, audit, canRun = async () => false, idleFor = () => 0, who = () => "R'th AI", ownChanged = () => {}, emit = () => {}, idleMs = Number(process.env.CODEX_AUDIT_IDLE || 600) * 1000 }) {
  const stateFile = path.join(dataDir, "audit.json");
  let state = { baseline: null, sweep: false };
  try { state = { ...state, ...JSON.parse(fs.readFileSync(stateFile, "utf8")) }; } catch {}
  const saveState = () => { try { fs.mkdirSync(dataDir, { recursive: true }); fs.writeFileSync(stateFile, JSON.stringify(state)); } catch {} };
  let running = null, timer = null, lastError = null;

  const rel = (page) => PROBLEMS + "/" + page.slice(GDD.length + 1).replace(/\.md$/, ".json");
  const abs = (page) => path.join(repo, rel(page));
  function load(page) {
    try { return { page, auditedHash: null, problems: [], dismissed: [], ...JSON.parse(fs.readFileSync(abs(page), "utf8")) }; } catch { return { page, auditedHash: null, problems: [], dismissed: [] }; }
  }
  function store(file) {
    fs.mkdirSync(path.dirname(abs(file.page)), { recursive: true });
    fs.writeFileSync(abs(file.page), JSON.stringify(file, null, 1) + "\n", "utf8");
    ownChanged(rel(file.page));
  }
  const checkable = (p) => p.startsWith(GDD + "/") && p.endsWith(".md") && !p.startsWith(TEMPLATES + "/") && !isMeta(p);

  /** The pages due a check: changed since last checked (and since the baseline, unless sweeping). */
  async function due() {
    const all = (await pages()).filter(checkable);
    const texts = new Map();
    for (const p of all) texts.set(p, await read(p));
    if (!state.baseline) { state.baseline = Object.fromEntries([...texts].map(([p, t]) => [p, textHash(t)])); saveState(); }
    const out = [];
    for (const [p, t] of texts) {
      const h = textHash(t), file = load(p);
      if (file.auditedHash === h) continue;
      if (!state.sweep && state.baseline[p] === h) continue;
      out.push({ page: p, hash: h, mtime: (() => { try { return fs.statSync(path.join(repo, p)).mtimeMs; } catch { return 0; } })() });
    }
    return out.sort((a, b) => b.mtime - a.mtime);
  }

  /** One run: a few due pages checked, their problems written. */
  async function runOnce() {
    if (running) return { error: "Already checking." };
    const list = (await due()).slice(0, PER_RUN);
    if (!list.length) { if (state.sweep) { state.sweep = false; saveState(); } return { ok: true, checked: [] }; }
    running = { pages: list.map((x) => x.page), started: new Date().toISOString() };
    emit("audit", view());
    try {
      const r = await audit({ pages: running.pages });
      if (r.error) { lastError = r.error; return { error: r.error }; }
      lastError = null;
      const at = new Date().toISOString(), by = who();
      const checked = new Set(running.pages);
      // Each problem goes on the file of the page to change first; a fresh check of a page replaces what
      // the last check of it found.
      const files = new Map();
      const fileOf = (p) => { if (!files.has(p)) files.set(p, load(p)); return files.get(p); };
      for (const x of list) { const f = fileOf(x.page); f.problems = f.problems.filter((p) => p.of !== x.page); f.auditedHash = x.hash; f.auditedAt = at; }
      for (const p of r.problems || []) {
        const home = p.pages[0];
        if (!checkable(home)) continue;
        const f = fileOf(home);
        if (f.problems.some((q) => q.summary === p.summary)) continue;
        f.problems.push({ id: "p-" + crypto.randomBytes(5).toString("hex"), ...p, of: checked.has(home) ? home : [...checked].find((c) => p.pages.includes(c)) || null, found: { by, at } });
      }
      for (const f of files.values()) store(f);
      return { ok: true, checked: running.pages, found: (r.problems || []).length };
    } catch (e) { lastError = e.message; return { error: e.message }; }
    finally { running = null; emit("audit", view()); emit("todo", {}); }
  }

  /** Every open problem, for the To-do (those whose quoted text is gone from its page are left out). */
  async function list() {
    const out = [];
    const dir = path.join(repo, PROBLEMS);
    const walk = (d) => { let es = []; try { es = fs.readdirSync(d, { withFileTypes: true }); } catch {} for (const e of es) { const a = path.join(d, e.name); if (e.isDirectory()) walk(a); else if (e.name.endsWith(".json")) { try { out.push(JSON.parse(fs.readFileSync(a, "utf8"))); } catch {} } } };
    walk(dir);
    const live = [];
    for (const f of out) {
      const gone = new Set((f.dismissed || []).map((d) => d.id));
      for (const p of f.problems || []) {
        if (gone.has(p.id)) continue;
        let still = true;
        for (let i = 0; i < (p.quotes || []).length && still; i++) {
          const page = p.pages[i] || p.pages[0], q = String(p.quotes[i] || "").trim();
          if (!q) continue;
          const t = await read(page);
          if (t == null || !norm(t).includes(norm(q))) still = false;
        }
        if (still) live.push({ ...p, file: f.page });
      }
    }
    return live.sort((a, b) => String(b.found?.at).localeCompare(String(a.found?.at)));
  }

  /** Dismissed, for everyone. */
  function dismiss({ file, id }) {
    if (typeof file !== "string" || !checkable(file)) return { error: "Which page?" };
    const f = load(file);
    if (!f.problems.some((p) => p.id === id)) return { error: "That problem isn't there any more." };
    if (!f.dismissed.some((d) => d.id === id)) f.dismissed.push({ id, by: who(), at: new Date().toISOString() });
    store(f);
    emit("todo", {});
    return { ok: true };
  }

  /** "Check the whole GDD": every page not checked in its current version, a few a run while idle. */
  async function sweep() {
    state.sweep = true; saveState();
    const n = (await due()).length;
    setTimeout(tick, 1000);
    return { ok: true, pages: n };
  }

  async function tick() {
    try {
      if (running || idleFor() < idleMs || !(await canRun())) return;
      if (!(await due()).length) return;
      await runOnce();
    } catch (e) { lastError = e.message; }
  }

  function view() { return { running: running ? running.pages : null, sweep: !!state.sweep, error: lastError }; }

  return {
    start() { if (!timer) { timer = setInterval(tick, 60 * 1000); timer.unref?.(); } },
    stop() { clearInterval(timer); timer = null; },
    runOnce, list, dismiss, sweep, due, view, tick,
  };
}

const norm = (t) => String(t || "").replace(/!?\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]/g, "").replace(/\s+/g, " ").toLowerCase();
