// The Codex's helper (PLAN.md, "The helper"): a Cloudflare Worker that holds the keys, so nobody's
// computer has to. It wakes per request and keeps nothing between them.
//
//   GET  /open?p=docs/...&h=heading     a link Discord can show, that opens the page in the Codex
//   POST /github                         GitHub tells it about pushes; it queues "what changed" for #codex
//                                        and tells every open Codex to fetch (the live room, "pages")
//   GET  /auth/discord/start?redirect=   Sign in with Discord (the Codex opens this in the browser)
//   GET  /auth/discord/callback          ...Discord sends the person back here, then to the Codex
//   GET  /me                             who a session is, and whether they still have the Codex role
//   GET  /version                        the oldest Codex that may change things (MIN_CODEX; src/version.js)
//   POST /git-token                      an hour's key to send docs to GitHub, for someone with the role
//   POST /announce                       a new Codex release, posted in #codex (the agent key only), and
//                                        every open Codex told to look for the update (the live room, "release")
//   /comments...                         comments in the Discord forum (src/comments.js)
//   GET  /live                           a WebSocket to the room of who's here (src/live.js)
//   POST /interactions                   /ask and "Ask R'th AI" from Discord, signed (src/interactions.js)
//   POST /admin/commands                 registers those two commands in the server (the agent key only)
//
// Comments take a session (a person) or, for agents on the owner's computer, "Authorization: Agent
// <AGENT_KEY>" (they post as Claude).
//
// Secrets (wrangler secret): DISCORD_BOT_TOKEN, DISCORD_CLIENT_SECRET, GITHUB_WEBHOOK_SECRET,
// GITHUB_APP_KEY (PKCS#8), SESSION_SECRET, AGENT_KEY. Everything else is in wrangler.toml's [vars].

import { onPush, sendDue } from "./notices.js";
import { openPage } from "./open.js";
import { signIn, callback, sessionFrom } from "./auth.js";
import { member, post, pinOnly, findOwn } from "./discord.js";
import { installationToken, verifyWebhook } from "./github.js";
import { comments, poll } from "./comments.js";
import { mentions, sweep as sweepMentions } from "./mentions.js";
import { same } from "./crypto.js";
import { Live, room } from "./live.js";
import { interaction, registerCommands } from "./interactions.js";
import { tooOld, outdated, updatePage, HEADER } from "./version.js";

export { Live };

export default {
  async fetch(req, env, ctx) {
    const url = new URL(req.url);
    const route = `${req.method} ${url.pathname}`;
    try {
      switch (route) {
        case "GET /": return text("R'th Codex helper");
        case "GET /open": return openPage(url, env);
        // The Codex has a link's page open (electron/main.js): the link's page may close its tab (open.js).
        case "POST /open/ack": case "GET /open/ack": return room(env).fetch(new Request(new URL("/live/open-ack" + url.search, url), { method: req.method, body: req.method === "POST" ? await req.text() : null }));
        case "GET /live": return room(env).fetch(req); // who's here (src/live.js)
        case "POST /interactions": return interaction(req, env, ctx); // from Discord, signed
        case "POST /admin/commands": {
          const h = req.headers.get("authorization") || "";
          if (!h.startsWith("Agent ") || !env.AGENT_KEY || !same(h.slice(6), env.AGENT_KEY)) return json({ error: "Only the owner's computer registers commands." }, 401);
          return json({ ok: true, commands: await registerCommands(env) });
        }
        case "POST /github": {
          const body = await req.text();
          if (!(await verifyWebhook(env, body, req.headers.get("x-hub-signature-256")))) return text("bad signature", 401);
          const event = req.headers.get("x-github-event");
          if (event === "push") {
            ctx.waitUntil(onPush(env, JSON.parse(body), url.origin).catch((e) => console.error("push", e.stack || e)));
            // Every open Codex fetches now, so what someone sent shows within seconds (src/sync.js, news).
            ctx.waitUntil(room(env).fetch("https://room/live/notify", { method: "POST", body: JSON.stringify({ type: "pages" }) }).catch(() => {}));
          }
          return text("ok");
        }
        // A new Codex release (scripts/announce.mjs, on the owner's computer): posted in #codex.
        case "POST /announce": {
          const h = req.headers.get("authorization") || "";
          if (!h.startsWith("Agent ") || !env.AGENT_KEY || !same(h.slice(6), env.AGENT_KEY)) return json({ error: "Only the owner's computer announces." }, 401);
          const b = await req.json().catch(() => ({}));
          if (typeof b.text !== "string" || !b.text.trim()) return json({ error: "Nothing to announce." }, 400);
          const msg = await post(env, env.DISCORD_NOTICES_CHANNEL, b.text.slice(0, 1990));
          // The latest release stays pinned (the one before is unpinned), so it isn't lost among the notices.
          const pinned = await pinRelease(env, msg.id).then(() => true, (e) => e.message);
          // Every open Codex looks for the update now, instead of at its next hourly check (src/together.js).
          if (typeof b.version === "string" && /^\d+\.\d+\.\d+$/.test(b.version))
            ctx.waitUntil(room(env).fetch("https://room/live/notify", { method: "POST", body: JSON.stringify({ type: "release", version: b.version }) }).catch(() => {}));
          return json({ ok: true, pinned });
        }
        // Pin a release that was announced already (the owner's computer: scripts/announce.mjs --pin).
        case "POST /announce/pin": {
          const h = req.headers.get("authorization") || "";
          if (!h.startsWith("Agent ") || !env.AGENT_KEY || !same(h.slice(6), env.AGENT_KEY)) return json({ error: "Only the owner's computer pins releases." }, 401);
          const b = await req.json().catch(() => ({}));
          if (typeof b.version !== "string" || !/^\d+\.\d+\.\d+$/.test(b.version)) return json({ error: "Which version?" }, 400);
          const msg = await findOwn(env, env.DISCORD_NOTICES_CHANNEL, `**R'th Codex ${b.version}** is out.`);
          if (!msg) return json({ error: `No announcement of ${b.version} among the last 100 messages.` }, 404);
          await pinRelease(env, msg.id);
          return json({ ok: true, pinned: msg.id });
        }
        // An update the Codex must have before it changes anything (src/version.js).
        case "GET /version": return json({ min: env.MIN_CODEX || null });
        // An out-of-date Codex's Sign in button lands here: the update page instead of Discord (version.js).
        case "GET /auth/discord/start": return outdated(url.searchParams.get("v"), env) ? updatePage(env) : signIn(url, env);
        case "GET /auth/discord/callback": return callback(url, env);
        case "GET /me": {
          // A Codex from before versions were sent (0.1.x), once a minimum is set: signed out, so it opens
          // reading only and its Sign in button shows the update page (version.js, updatePage).
          if (env.MIN_CODEX && !req.headers.get(HEADER)) return json({ error: "This version of Codex is out of date. Close it and open it again to update it." }, 401);
          const s = await sessionFrom(req, env);
          if (!s) return json({ error: "Not signed in." }, 401);
          const m = await member(env, s.sub);
          // An out-of-date Codex is told it has no Codex role: that is what makes a 0.1.x Codex read-only
          // the moment it opens (it can't be told anything else). Newer ones show the update screen.
          return json({ user: { id: s.sub, name: m?.name || s.name, avatar: m?.avatar || s.avatar }, member: !!m?.codex && !tooOld(req, env) });
        }
        case "POST /git-token": {
          const old = tooOld(req, env);
          if (old) return old;
          const s = await sessionFrom(req, env);
          if (!s) return json({ error: "Not signed in." }, 401);
          const m = await member(env, s.sub);
          if (!m?.codex) return json({ error: "You need the Codex role in the R'th Discord to send changes." }, 403);
          const t = await installationToken(env);
          return json({ token: t.token, expires: t.expires_at, name: m.name, email: `${s.sub}+discord@users.rth-codex` });
        }
        default: {
          if (url.pathname.startsWith("/mentions/")) {
            // A Codex answering @R'th AI claims the tag first (mentions.js): signed in with the Codex role.
            const as = await whoAsks(req, env);
            if (!as) return json({ error: "Not signed in." }, 401);
            if (as.kind === "person") { const m = await member(env, as.user.id); if (!m?.codex) return json({ error: "You need the Codex role." }, 403); }
            const r = await mentions(req, env, url);
            if (!r) return text("not found", 404);
            return r.status ? json({ error: r.error }, r.status) : json(r);
          }
          if (!url.pathname.startsWith("/comments")) return text("not found", 404);
          const as = await whoAsks(req, env);
          if (!as) return json({ error: "Not signed in." }, 401);
          // An out-of-date Codex may read comments but not write them (agents on the owner's computer may).
          if (req.method !== "GET" && as.kind === "person") { const old = tooOld(req, env); if (old) return old; }
          const r = await comments(req, env, url, as, ctx);
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

  // Every 5 minutes (wrangler.toml, triggers): post the "what changed" batches that are ready (notices.js).
  async scheduled(event, env, ctx) {
    ctx.waitUntil(sendDue(env).catch((e) => console.error("notices", e.stack || e)));
    // @R'th AI: read the forum even with no Codex open (so "offline" is said), and offer again what waits.
    ctx.waitUntil(poll(env, ctx).then(() => sweepMentions(env)).catch((e) => console.error("mentions", e.stack || e)));
  },
};

/** Pin a release announcement in #codex in place of the last one (remembered in D1's config table). */
async function pinRelease(env, id) {
  const before = (await env.DB.prepare("SELECT v FROM config WHERE k = 'release_pin'").first())?.v || null;
  await pinOnly(env, env.DISCORD_NOTICES_CHANNEL, id, before);
  await env.DB.prepare("INSERT INTO config (k, v) VALUES ('release_pin', ?) ON CONFLICT(k) DO UPDATE SET v = excluded.v").bind(id).run();
}

/** A person (by their session) or an agent (by the owner's agent key), or null. */
async function whoAsks(req, env) {
  const h = req.headers.get("authorization") || "";
  if (h.startsWith("Agent ")) return env.AGENT_KEY && same(h.slice(6), env.AGENT_KEY) ? { kind: "agent" } : null;
  const s = await sessionFrom(req, env);
  return s ? { kind: "person", user: { id: s.sub, name: s.name, avatar: s.avatar } } : null;
}

export const text = (body, status = 200) => new Response(body, { status, headers: { "content-type": "text/plain; charset=utf-8" } });
export const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
