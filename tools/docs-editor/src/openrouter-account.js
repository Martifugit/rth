// Sign in with OpenRouter (PLAN.md, "AI, built in"): free models for anyone without Claude on their
// computer, each person on their own daily allowance. OpenRouter's OAuth with PKCE: the page opens
// OpenRouter's sign-in, which comes back to /api/openrouter/done here with a code; the engine trades it
// for the person's own key. The key stays on this computer, encrypted in the desktop app.

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

export async function makeOpenRouterAccount({ dir, emit }) {
  fs.mkdirSync(dir, { recursive: true });
  const file = path.join(dir, "openrouter.bin");
  let safe = null;
  try { const e = await import("electron"); if (e.safeStorage?.isEncryptionAvailable?.()) safe = e.safeStorage; } catch {}

  let key = load();
  let verifier = null; // the PKCE secret of the sign-in under way

  function load() {
    try {
      const raw = fs.readFileSync(file);
      return (safe ? safe.decryptString(raw) : raw.toString("utf8")).trim() || null;
    } catch { return null; }
  }
  function store(v) {
    key = v;
    if (!v) { fs.rmSync(file, { force: true }); return; }
    fs.writeFileSync(file, safe ? safe.encryptString(v) : v);
  }

  const view = () => ({ signedIn: !!key });

  /** OpenRouter's sign-in page, coming back to this engine. */
  function signInUrl(origin) {
    verifier = crypto.randomBytes(32).toString("base64url");
    const challenge = crypto.createHash("sha256").update(verifier).digest("base64url");
    return "https://openrouter.ai/auth?" + new URLSearchParams({ callback_url: origin + "/api/openrouter/done", code_challenge: challenge, code_challenge_method: "S256" });
  }

  async function done(params) {
    const code = params.get("code");
    if (!code || !verifier) return { ok: false, message: "That sign-in didn't finish. Start again from the Codex." };
    const r = await fetch("https://openrouter.ai/api/v1/auth/keys", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ code, code_verifier: verifier, code_challenge_method: "S256" }),
    });
    verifier = null;
    const j = await r.json().catch(() => ({}));
    if (!r.ok || !j.key) return { ok: false, message: "OpenRouter didn't accept the sign-in. Try again." };
    store(j.key);
    emit("ai", { state: "provider" });
    return { ok: true, message: "Signed in with OpenRouter. The AI tab is ready." };
  }

  function signOut() { store(null); emit("ai", { state: "provider" }); return view(); }

  /** Another way in (AI setup): a key the person made on openrouter.ai themselves, checked first. */
  async function useKey(k) {
    k = String(k || "").trim();
    if (!/^sk-or-[\w-]{10,}$/.test(k)) return { ok: false, error: "That doesn't look like an OpenRouter key (they start with sk-or-)." };
    const r = await fetch("https://openrouter.ai/api/v1/key", { headers: { Authorization: "Bearer " + k } }).catch(() => null);
    if (!r) return { ok: false, error: "Couldn't reach OpenRouter to check the key." };
    if (!r.ok) return { ok: false, error: "OpenRouter didn't accept that key." };
    store(k);
    emit("ai", { state: "provider" });
    return { ok: true, ...view() };
  }

  return { view, signInUrl, done, signOut, useKey, key: () => key };
}
