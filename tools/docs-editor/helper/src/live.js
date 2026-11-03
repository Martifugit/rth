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

import { DurableObject } from "cloudflare:workers";
import { unseal } from "./crypto.js";
import { member } from "./discord.js";

const str = (v, n) => (typeof v === "string" ? v.slice(0, n) : null);

/** Only what the room passes on, and only so much of it. */
function clean(s = {}) {
  return {
    page: str(s.page, 300), title: str(s.title, 200),
    section: Array.isArray(s.section) ? s.section.slice(0, 6).map((x) => String(x).slice(0, 120)) : [],
    editing: !!s.editing, typingIn: str(s.typingIn, 40), away: !!s.away, hidden: !!s.hidden,
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
      a.state = clean(m.state);
      ws.serializeAttachment(a);
      this.tell();
    }
  }

  async webSocketClose(ws) { try { ws.close(); } catch {} this.tell(ws); }
  async webSocketError(ws) { this.tell(ws); }

  /** Everyone here (not those who chose to be hidden), to everyone. */
  tell(gone = null) {
    const open = this.ctx.getWebSockets().filter((w) => w !== gone);
    const people = open.map((w) => w.deserializeAttachment()).filter((a) => a?.ok && !a.state.hidden)
      .map((a) => ({ conn: a.conn, ...a.user, ...a.state }));
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
