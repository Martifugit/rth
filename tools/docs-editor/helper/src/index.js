// The Codex's helper (PLAN.md, "The helper"): a Cloudflare Worker that holds the keys, so nobody's
// computer has to. It wakes per request and keeps nothing between them.
//
//   GET  /open?p=docs/...&h=heading     a link Discord can show, that opens the page in the Codex
//   POST /github                         GitHub tells it about pushes; it posts "what changed" to #codex
//   GET  /auth/discord/start?redirect=   Sign in with Discord (the Codex opens this in the browser)
//   GET  /auth/discord/callback          ...Discord sends the person back here, then to the Codex
//   GET  /me                             who a session is, and whether they still have the Codex role
//   POST /git-token                      an hour's key to send docs to GitHub, for someone with the role
//   POST /announce                       a new Codex release, posted in #codex (the agent key only)
//   /comments...                         comments in the Discord forum (src/comments.js)
//   GET  /live                           a WebSocket to the room of who's here (src/live.js)
//
// Comments take a session (a person) or, for agents on the owner's computer, "Authorization: Agent
// <AGENT_KEY>" (they post as Claude).
//
// Secrets (wrangler secret): DISCORD_BOT_TOKEN, DISCORD_CLIENT_SECRET, GITHUB_WEBHOOK_SECRET,
// GITHUB_APP_KEY (PKCS#8), SESSION_SECRET, AGENT_KEY. Everything else is in wrangler.toml's [vars].

import { onPush } from "./notices.js";
import { openPage } from "./open.js";
import { signIn, callback, sessionFrom } from "./auth.js";
import { member, post } from "./discord.js";
import { installationToken, verifyWebhook } from "./github.js";
import { comments } from "./comments.js";
import { same } from "./crypto.js";
import { Live, room } from "./live.js";

export { Live };

export default {
  async fetch(req, env, ctx) {
    const url = new URL(req.url);
    const route = `${req.method} ${url.pathname}`;
    try {
      switch (route) {
        case "GET /": return text("R'th Codex helper");
        case "GET /open": return openPage(url, env);
        case "GET /live": return room(env).fetch(req); // who's here (src/live.js)
        case "POST /github": {
          const body = await req.text();
          if (!(await verifyWebhook(env, body, req.headers.get("x-hub-signature-256")))) return text("bad signature", 401);
          const event = req.headers.get("x-github-event");
          if (event === "push") ctx.waitUntil(onPush(env, JSON.parse(body), url.origin).catch((e) => console.error("push", e.stack || e)));
          return text("ok");
        }
        // A new Codex release (scripts/announce.mjs, on the owner's computer): posted in #codex.
        case "POST /announce": {
          const h = req.headers.get("authorization") || "";
          if (!h.startsWith("Agent ") || !env.AGENT_KEY || !same(h.slice(6), env.AGENT_KEY)) return json({ error: "Only the owner's computer announces." }, 401);
          const b = await req.json().catch(() => ({}));
          if (typeof b.text !== "string" || !b.text.trim()) return json({ error: "Nothing to announce." }, 400);
          await post(env, env.DISCORD_NOTICES_CHANNEL, b.text.slice(0, 1990));
          return json({ ok: true });
        }
        case "GET /auth/discord/start": return signIn(url, env);
        case "GET /auth/discord/callback": return callback(url, env);
        case "GET /me": {
          const s = await sessionFrom(req, env);
          if (!s) return json({ error: "Not signed in." }, 401);
          const m = await member(env, s.sub);
          return json({ user: { id: s.sub, name: m?.name || s.name, avatar: m?.avatar || s.avatar }, member: !!m?.codex });
        }
        case "POST /git-token": {
          const s = await sessionFrom(req, env);
          if (!s) return json({ error: "Not signed in." }, 401);
          const m = await member(env, s.sub);
          if (!m?.codex) return json({ error: "You need the Codex role in the R'th Discord to send changes." }, 403);
          const t = await installationToken(env);
          return json({ token: t.token, expires: t.expires_at, name: m.name, email: `${s.sub}+discord@users.rth-codex` });
        }
        default: {
          if (!url.pathname.startsWith("/comments")) return text("not found", 404);
          const as = await whoAsks(req, env);
          if (!as) return json({ error: "Not signed in." }, 401);
          const r = await comments(req, env, url, as);
          if (!r) return text("not found", 404);
          // A comment changed: every open Codex fetches comments now, not at its next look.
          if (req.method === "POST" && !r.status) ctx.waitUntil(room(env).fetch("https://room/live/notify", { method: "POST", body: JSON.stringify({ type: "comments" }) }).catch(() => {}));
          return r.status ? json({ error: r.error }, r.status) : json(r);
        }
      }
    } catch (e) {
      console.error(route, e.stack || e);
      return json({ error: "The helper failed: " + e.message }, 500);
    }
  },
};

/** A person (by their session) or an agent (by the owner's agent key), or null. */
async function whoAsks(req, env) {
  const h = req.headers.get("authorization") || "";
  if (h.startsWith("Agent ")) return env.AGENT_KEY && same(h.slice(6), env.AGENT_KEY) ? { kind: "agent" } : null;
  const s = await sessionFrom(req, env);
  return s ? { kind: "person", user: { id: s.sub, name: s.name, avatar: s.avatar } } : null;
}

export const text = (body, status = 200) => new Response(body, { status, headers: { "content-type": "text/plain; charset=utf-8" } });
export const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
