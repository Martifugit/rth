// Sign in with Discord. The Codex opens /auth/discord/start in the browser with `redirect`, its own
// address on this computer (127.0.0.1 only). Discord asks the person, sends them back to the
// callback, and the helper checks they're in the R'th server with the Codex role, then hands the Codex
// a session (signed, 90 days) by sending the browser to that redirect with it.

import { seal, unseal } from "./crypto.js";
import { member } from "./discord.js";
import { codexPage, REPO, esc } from "../../src/codex-page.js";

const SESSION_DAYS = 90;

const local = (u) => {
  try { const x = new URL(u); return x.protocol === "http:" && (x.hostname === "127.0.0.1" || x.hostname === "localhost"); } catch { return false; }
};

export async function signIn(url, env) {
  const redirect = url.searchParams.get("redirect");
  if (!local(redirect)) return page("Sign-in has to start from Codex.", 400);
  const state = await seal(env.SESSION_SECRET, { redirect }, 600);
  const q = new URLSearchParams({
    client_id: env.DISCORD_APP_ID, response_type: "code", scope: "identify",
    redirect_uri: url.origin + "/auth/discord/callback", state, prompt: "none",
  });
  return Response.redirect("https://discord.com/oauth2/authorize?" + q, 302);
}

export async function callback(url, env) {
  const st = await unseal(env.SESSION_SECRET, url.searchParams.get("state"));
  if (!st || !local(st.redirect)) return page("That sign-in link has expired. Start again from Codex.", 400);
  const back = (params) => Response.redirect(st.redirect + (st.redirect.includes("?") ? "&" : "?") + new URLSearchParams(params), 302);
  if (url.searchParams.get("error")) return back({ error: "You didn't allow the sign-in." });

  const tok = await fetch("https://discord.com/api/v10/oauth2/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded", Authorization: "Basic " + btoa(env.DISCORD_APP_ID + ":" + env.DISCORD_CLIENT_SECRET) },
    body: new URLSearchParams({ grant_type: "authorization_code", code: url.searchParams.get("code") || "", redirect_uri: url.origin + "/auth/discord/callback" }),
  });
  if (!tok.ok) return back({ error: "Discord didn't accept the sign-in. Try again." });
  const { access_token } = await tok.json();
  const me = await fetch("https://discord.com/api/v10/users/@me", { headers: { Authorization: "Bearer " + access_token } });
  if (!me.ok) return back({ error: "Couldn't read who you are from Discord." });
  const user = await me.json();

  const m = await member(env, user.id);
  if (!m) return back({ error: "You're not in the R'th Discord server." });
  if (!m.codex) return back({ error: "You need the Codex role in the R'th Discord. Ask the owner for it." });
  const session = await seal(env.SESSION_SECRET, { sub: user.id, name: m.name, avatar: m.avatar }, SESSION_DAYS * 86400);
  return back({ session });
}

/** The session in an Authorization: Bearer header, or null. */
export async function sessionFrom(req, env) {
  const h = req.headers.get("authorization") || "";
  return h.startsWith("Bearer ") ? unseal(env.SESSION_SECRET, h.slice(7)) : null;
}

function page(message, status = 200) {
  const html = codexPage({ title: "Signing in - R'th Codex", heading: "Signing in", assets: REPO,
    body: `<p class="bad">${esc(message)}</p><p>You can close this tab.</p>` });
  return new Response(html, { status, headers: { "content-type": "text/html; charset=utf-8" } });
}
