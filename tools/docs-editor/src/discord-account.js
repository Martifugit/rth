// Who's using this Codex: Sign in with Discord, through the helper (PLAN.md, "The helper").
//
// The page asks for a sign-in link; the browser goes through Discord and the helper, which sends it
// back to /api/account/done here with a session (or a reason it failed). The session is kept on this
// computer: encrypted with the system's own storage in the desktop app (Electron safeStorage), as a
// plain file in the browser version. With it, the engine asks the helper for an hour's key to send
// docs to GitHub (gitAuth), so nobody needs a GitHub account.

import fs from "node:fs";
import path from "node:path";
import { VERSION, VERSION_HEADERS, noteAnswer } from "./codex-version.js";
import { secretFile } from "./secret-file.js";

export const HELPER = (process.env.CODEX_HELPER || "https://rth-codex.rthapp.workers.dev").replace(/\/+$/, "");

/** contributor: this is a copy of the docs the Codex fetched; editing waits for the Codex role. */
export async function makeAccount({ dir, emit, contributor = false }) {
  fs.mkdirSync(dir, { recursive: true });
  const secret = await secretFile(path.join(dir, "account.bin")); // locked on this computer (src/secret-file.js)

  let A = await load();    // { session, user: { id, name, avatar } } or null
  let member = null;       // whether the helper last said they have the Codex role
  let key = null;          // { token, expires, name, email }

  async function load() {
    try { const text = await secret.read(); return text ? JSON.parse(text) : null; } catch { return null; }
  }
  function store(v) {
    A = v;
    key = null;
    secret.write(v ? JSON.stringify(v) : null);
  }

  const canEdit = () => !contributor || (!!A && member !== false);
  const view = () => ({ signedIn: !!A, user: A?.user || null, member, helper: HELPER, contributor, canEdit: canEdit() });

  async function helper(route, { method = "GET" } = {}) {
    const r = await fetch(HELPER + route, { method, headers: { Authorization: "Bearer " + A.session, ...VERSION_HEADERS } });
    const j = await r.json().catch(() => ({}));
    noteAnswer(r.status, j); // 426: this Codex is out of date
    return { status: r.status, ...j };
  }

  /** Ask the helper who the session is (and refresh name and avatar). Signs out if it's no longer valid. */
  async function check() {
    if (!A) return view();
    try {
      const r = await helper("/me");
      if (r.status === 401) { store(null); member = null; }
      else if (r.user) { member = r.member; store({ ...A, user: r.user }); }
    } catch {} // offline: keep what we know
    return view();
  }

  /** The link that starts signing in, coming back to this engine's address. */
  // v: this Codex's version, so an out-of-date one is shown the update page instead (helper/src/version.js).
  const signInUrl = (origin) => HELPER + "/auth/discord/start?" + new URLSearchParams({ redirect: origin + "/api/account/done", v: VERSION });

  /** The browser came back from the helper. */
  async function done(params) {
    const err = params.get("error");
    if (err) { emit("account", { ...view(), error: err }); return { ok: false, message: err }; }
    const session = params.get("session");
    if (!session) return { ok: false, message: "The sign-in didn't finish." };
    A = { session, user: null };
    const v = await check();
    if (!v.signedIn) return { ok: false, message: "The helper didn't accept that sign-in." };
    emit("account", v);
    return { ok: true, message: `Signed in as ${v.user.name}.` };
  }

  function signOut() { store(null); member = null; emit("account", view()); return view(); }

  /**
   * What git needs to send docs as this person: an hour's key from the helper (kept until five minutes
   * before it runs out) and their name. Null when not signed in; { error } when the helper says no.
   */
  async function gitAuth() {
    if (!A) return null;
    if (key && Date.parse(key.expires) - Date.now() > 5 * 60 * 1000) return key;
    try {
      const r = await helper("/git-token", { method: "POST" });
      if (r.token) { key = r; return key; }
      if (r.status === 401) { store(null); emit("account", view()); }
      return { error: r.error || "The helper wouldn't give a key to send changes." };
    } catch (e) {
      return { error: "Couldn't reach Codex's helper: " + e.message };
    }
  }

  // (No check at start: the page asks with GET /api/account when it opens.)
  /** The Authorization header for the helper, or null when not signed in. */
  const authHeader = () => (A ? "Bearer " + A.session : null);

  /** The session itself, for the room of who's here (src/together.js). */
  const session = () => A?.session || null;

  return { view, check, signInUrl, done, signOut, gitAuth, authHeader, canEdit, session };
}
