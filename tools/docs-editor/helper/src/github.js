// GitHub, as the R'th Codex GitHub App: its webhook's signature, and short-lived installation tokens
// (this repository only, contents read and write, an hour).

import { b64url, hmacHex, same } from "./crypto.js";

const API = "https://api.github.com";
const headers = (auth) => ({ Authorization: auth, Accept: "application/vnd.github+json", "User-Agent": "rth-codex-helper", "X-GitHub-Api-Version": "2022-11-28" });

export async function verifyWebhook(env, body, signature) {
  if (!signature || !env.GITHUB_WEBHOOK_SECRET) return false;
  return same(signature, "sha256=" + (await hmacHex(env.GITHUB_WEBHOOK_SECRET, body)));
}

/** The App's own JWT (ten minutes), signed with its private key. */
async function appJwt(env) {
  const der = Uint8Array.from(atob(env.GITHUB_APP_KEY.replace(/-----[^-]+-----/g, "").replace(/\s+/g, "")), (c) => c.charCodeAt(0));
  const key = await crypto.subtle.importKey("pkcs8", der, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["sign"]);
  const now = Math.floor(Date.now() / 1000);
  const part = (o) => b64url(JSON.stringify(o));
  const unsigned = part({ alg: "RS256", typ: "JWT" }) + "." + part({ iat: now - 60, exp: now + 540, iss: env.GITHUB_APP_ID });
  const sig = await crypto.subtle.sign("RSASSA-PKCS1-v1_5", key, new TextEncoder().encode(unsigned));
  return unsigned + "." + b64url(sig);
}

/** A token for the repository, good for an hour: { token, expires_at }. */
export async function installationToken(env) {
  const jwt = await appJwt(env);
  const inst = await fetch(`${API}/repos/${env.REPO}/installation`, { headers: headers("Bearer " + jwt) });
  if (!inst.ok) throw new Error(`the GitHub App isn't installed on ${env.REPO} (${inst.status})`);
  const { id } = await inst.json();
  const r = await fetch(`${API}/app/installations/${id}/access_tokens`, {
    method: "POST", headers: headers("Bearer " + jwt),
    body: JSON.stringify({ repositories: [env.REPO.split("/")[1]], permissions: { contents: "write" } }),
  });
  if (!r.ok) throw new Error(`GitHub wouldn't give a token (${r.status}): ${await r.text()}`);
  return r.json();
}

/** GitHub's REST API with an installation token. */
export async function gh(env, path, token) {
  const r = await fetch(API + path, { headers: headers("token " + token) });
  if (!r.ok) throw new Error(`GitHub ${path}: ${r.status}`);
  return r.json();
}
