// Who's here (PLAN.md, "Together"): one always-on room (a Cloudflare Durable Object) that every open
// Codex keeps a WebSocket to. Each connection says who it is (its Discord session; only people with
// the Codex role get in) and where it is: the page, the section, whether it's editing there, which
// comment it's typing in, whether it's away. The room tells everyone the list whenever it changes, and
// passes on "comments changed" so comments show up at once. It keeps nothing: when a Codex closes or
// drops, it's gone from the list.
//
// Hibernation: the room sleeps between messages (Cloudflare keeps the sockets), and answers the
// Codex's "ping" itself, so a quiet room costs nothing.
//
//   Codex -> room   { type: "hello", session }      then   { type: "state", state: {...} }
//   room -> Codex   { type: "welcome", conn }  { type: "here", people: [...] }  { type: "comments" }
//                   { type: "pages" }  { type: "release", version }   (from the Worker, through /live/notify)

import { DurableObject } from "cloudflare:workers";
import { unseal } from "./crypto.js";
import { member } from "./discord.js";
import { waiting } from "./mentions.js";

const str = (v, n) => (typeof v === "string" ? v.slice(0, n) : null);

/** Only what the room passes on, and only so much of it. */
function clean(s = {}) {
  return {
    page: str(s.page, 300), title: str(s.title, 200),
    section: Array.isArray(s.section) ? s.section.slice(0, 6).map((x) => String(x).slice(0, 120)) : [],
    editing: !!s.editing, typingIn: str(s.typingIn, 40), away: !!s.away, hidden: !!s.hidden,
    ai: !!s.ai, // this Codex answers @R'th AI (it has Claude, and AI setup says so): mentions.js
  };
}

export class Live extends DurableObject {
  constructor(ctx, env) {
    super(ctx, env);
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair("ping", "pong"));
  }

  async fetch(req) {
    const url = new URL(req.url);
    if (url.pathname === "/live/notify") { // from the Worker: comments changed
      this.send(await req.text());
      return new Response("ok");
    }
    // From the Worker (mentions.js): give a tag of R'th AI to one Codex that answers. The one asked least
    // lately is picked, so two open Codexes take turns; it claims the tag before it answers.
    if (url.pathname === "/live/ask") {
      const m = await req.text();
      const answerers = this.ctx.getWebSockets().filter((w) => { const a = w.deserializeAttachment(); return a?.ok && a.state?.ai; });
      if (!answerers.length) return Response.json({ sent: false });
      const pick = answerers.sort((x, y) => (x.deserializeAttachment().asked || 0) - (y.deserializeAttachment().asked || 0))[0];
      const a = pick.deserializeAttachment();
      a.asked = Date.now();
      pick.serializeAttachment(a);
      try { pick.send(m); } catch { return Response.json({ sent: false }); }
      return Response.json({ sent: true });
    }
    if (req.headers.get("Upgrade") !== "websocket") return new Response("expected a WebSocket", { status: 426 });
    const [client, server] = Object.values(new WebSocketPair());
    this.ctx.acceptWebSocket(server);
    server.serializeAttachment({ ok: false });
    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(ws, raw) {
    let m;
    try { m = JSON.parse(raw); } catch { return; }
    const a = ws.deserializeAttachment() || {};
    if (m.type === "hello") {
      const s = await unseal(this.env.SESSION_SECRET, String(m.session || "")).catch(() => null);
      if (!s) return ws.close(4001, "Not signed in.");
      const who = await member(this.env, s.sub).catch(() => null);
      if (!who?.codex) return ws.close(4003, "No Codex role.");
      const conn = crypto.randomUUID();
      ws.serializeAttachment({ ok: true, conn, user: { id: s.sub, name: who.name, avatar: who.avatar }, state: {} });
      ws.send(JSON.stringify({ type: "welcome", conn }));
      return this.tell();
    }
    if (!a.ok) return;
    if (m.type === "state") {
      const was = !!a.state?.ai;
      a.state = clean(m.state);
      ws.serializeAttachment(a);
      this.tell();
      // A Codex that answers has just come: the tags that waited for one are offered to it.
      if (a.state.ai && !was) for (const t of await waiting(this.env).catch(() => [])) { try { ws.send(JSON.stringify({ type: "ask", id: t.id, thread: t.thread })); } catch {} }
    }
  }

  async webSocketClose(ws) { try { ws.close(); } catch {} this.tell(ws); }
  async webSocketError(ws) { this.tell(ws); }

  /** Everyone here (not those who chose to be hidden), to everyone. */
  tell(gone = null) {
    const open = this.ctx.getWebSockets().filter((w) => w !== gone);
    const people = open.map((w) => w.deserializeAttachment()).filter((a) => a?.ok && !a.state.hidden)
      .map((a) => { const { ai, ...state } = a.state; return { conn: a.conn, ...a.user, ...state }; });
    this.send(JSON.stringify({ type: "here", people }), gone);
  }

  send(msg, skip = null) {
    for (const w of this.ctx.getWebSockets()) {
      if (w === skip || !w.deserializeAttachment()?.ok) continue;
      try { w.send(msg); } catch {}
    }
  }
}

/** The one room. */
export const room = (env) => env.LIVE.get(env.LIVE.idFromName("codex"));
