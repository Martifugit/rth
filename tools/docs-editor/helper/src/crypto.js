// Small signing helpers on the Worker's WebCrypto: base64url, HMAC tokens (sessions, sign-in state).

const enc = new TextEncoder();

export function b64url(bytes) {
  const s = typeof bytes === "string" ? bytes : String.fromCharCode(...new Uint8Array(bytes));
  return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
export function unb64url(s) {
  s = s.replace(/-/g, "+").replace(/_/g, "/");
  return atob(s + "===".slice((s.length + 3) % 4));
}
const utf8b64 = (str) => b64url(String.fromCharCode(...enc.encode(str)));
const fromUtf8b64 = (s) => new TextDecoder().decode(Uint8Array.from(unb64url(s), (c) => c.charCodeAt(0)));

async function hmacKey(secret) {
  return crypto.subtle.importKey("raw", enc.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign", "verify"]);
}

export async function hmacHex(secret, data) {
  const sig = await crypto.subtle.sign("HMAC", await hmacKey(secret), enc.encode(data));
  return [...new Uint8Array(sig)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

/** A signed, expiring token: payload.signature. `ttl` in seconds. */
export async function seal(secret, payload, ttl) {
  const body = utf8b64(JSON.stringify({ ...payload, exp: Math.floor(Date.now() / 1000) + ttl }));
  const sig = await crypto.subtle.sign("HMAC", await hmacKey(secret), enc.encode(body));
  return body + "." + b64url(sig);
}

/** The payload of a token made by seal(), or null if it's forged or expired. */
export async function unseal(secret, token) {
  if (typeof token !== "string" || !token.includes(".")) return null;
  const [body, sig] = token.split(".");
  let ok = false;
  try {
    ok = await crypto.subtle.verify("HMAC", await hmacKey(secret), Uint8Array.from(unb64url(sig), (c) => c.charCodeAt(0)), enc.encode(body));
  } catch { return null; }
  if (!ok) return null;
  const p = JSON.parse(fromUtf8b64(body));
  return p.exp > Date.now() / 1000 ? p : null;
}

/** Constant-time comparison of two strings. */
export function same(a, b) {
  if (typeof a !== "string" || typeof b !== "string" || a.length !== b.length) return false;
  let d = 0;
  for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return d === 0;
}
