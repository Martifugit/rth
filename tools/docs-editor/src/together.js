// Who's here, on the engine's side (PLAN.md, "Together"): while you're signed in with Discord, the
// engine keeps a WebSocket to the helper's room (helper/src/live.js), says where you are (what the page
// tells it: POST /api/here), and passes on who else is here ("here" events) and when comments changed.
// It reconnects by itself, more slowly each time, and stops when you sign out or lack the Codex role.

const PING_MS = 30 * 1000;

export function makeTogether({ helper, session, emit, onComments = () => {} }) {
  let ws = null, me = null, people = [], state = {}, wanted = false, retry = 2000, retryTimer = null, pinger = null, refused = null;

  const view = () => ({ connected: !!me, refused, people: people.filter((p) => p.conn !== me) });
  const tell = () => emit("here", view());

  function connect() {
    clearTimeout(retryTimer);
    const s = session();
    if (!wanted || !s || ws) return;
    let sock;
    try { sock = new WebSocket(helper.replace(/^http/, "ws") + "/live"); } catch { return later(); }
    ws = sock;
    sock.onopen = () => sock.send(JSON.stringify({ type: "hello", session: s }));
    sock.onmessage = (e) => {
      if (e.data === "pong") return;
      let m; try { m = JSON.parse(e.data); } catch { return; }
      if (m.type === "welcome") { me = m.conn; refused = null; retry = 2000; send(); tell(); }
      else if (m.type === "here") { people = Array.isArray(m.people) ? m.people : []; tell(); }
      else if (m.type === "comments") onComments();
    };
    sock.onclose = (e) => {
      if (ws === sock) ws = null;
      me = null; people = [];
      // Not signed in, or no Codex role: don't keep knocking (signing in again starts it afresh).
      if (e.code === 4001 || e.code === 4003) { refused = e.reason || "refused"; tell(); return; }
      tell();
      later();
    };
    sock.onerror = () => {};
  }
  function later() {
    if (!wanted) return;
    clearTimeout(retryTimer);
    retryTimer = setTimeout(connect, retry);
    retry = Math.min(retry * 2, 60 * 1000);
  }
  function send() {
    if (ws && ws.readyState === 1 && me) ws.send(JSON.stringify({ type: "state", state }));
  }

  return {
    /** Signed in (or the app started signed in): join the room. */
    start() {
      wanted = true; refused = null; retry = 2000;
      connect();
      clearInterval(pinger);
      pinger = setInterval(() => { if (ws && ws.readyState === 1) ws.send("ping"); }, PING_MS);
    },
    /** Signed out: leave it. */
    stop() {
      wanted = false;
      clearTimeout(retryTimer); clearInterval(pinger);
      try { ws?.close(); } catch {}
      ws = null; me = null; people = [];
      tell();
    },
    restart() { this.stop(); this.start(); },
    /** Where you are now (from the page). */
    set(next) { state = next || {}; send(); },
    view,
  };
}
