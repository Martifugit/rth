// Who's here, on the engine's side (PLAN.md, "Together"): while you're signed in with Discord, the
// engine keeps a WebSocket to the helper's room (helper/src/live.js), says where you are (what the page
// tells it: POST /api/here), and passes on who else is here ("here" events) and when comments changed.
// It reconnects by itself, more slowly each time, and stops when you sign out or lack the Codex role.

const PING_MS = 30 * 1000;

// extra(): what the engine adds to where you are (ai: this Codex answers @R'th AI). onAsk(m): the room
// hands this Codex a tag of R'th AI to answer (helper/src/mentions.js, src/discord-answer.js).
export function makeTogether({ helper, session, emit, onComments = () => {}, onPages = () => {}, onRelease = () => {}, extra = () => ({}), onAsk = () => {} }) {
  let ws = null, me = null, people = [], state = {}, wanted = false, retry = 2000, retryTimer = null, pinger = null, refused = null;

  // CODEX_DEMO_PEOPLE=1 (a look at the header, never in a release): pretend people join the room.
  const demo = process.env.CODEX_DEMO_PEOPLE ? DEMO_PEOPLE : [];
  const view = () => ({ connected: !!me || demo.length > 0, refused, people: [...people.filter((p) => p.conn !== me), ...demo] });
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
      else if (m.type === "pages") onPages(); // someone sent pages: fetch them now
      else if (m.type === "release") onRelease(String(m.version || "")); // a new Codex is out: look for it now
      else if (m.type === "ask") onAsk({ id: String(m.id || ""), thread: String(m.thread || "") });
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
    if (ws && ws.readyState === 1 && me) ws.send(JSON.stringify({ type: "state", state: { ...state, ...extra() } }));
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
      try { ws?.close(); } catch { /* already closed */ }
      ws = null; me = null; people = [];
      tell();
    },
    restart() { this.stop(); this.start(); },
    /** Where you are now (from the page). */
    set(next) { state = next || {}; send(); },
    /** What the engine adds changed (AI setup): say it again. */
    refresh() { send(); },
    view,
  };
}

const DEMO_AV = (n) => `https://cdn.discordapp.com/embed/avatars/${n}.png`;
const DEMO_PEOPLE = [
  { id: "demo-kari", conn: "demo-1", name: "Kari", avatar: DEMO_AV(1), page: "docs/gdd/rth-overview.md", title: "R'th", section: ["R'th", "Pillars"], editing: true },
  { id: "demo-tobias", conn: "demo-2", name: "Tobias", avatar: DEMO_AV(2), page: "docs/gdd/rth-overview.md", title: "R'th", section: ["R'th"], editing: false },
  { id: "demo-ines", conn: "demo-3", name: "Ines", avatar: DEMO_AV(3), page: "docs/gdd/how-the-gdd-works.md", title: "How the GDD Works", section: [], editing: false },
  { id: "demo-oskar", conn: "demo-4", name: "Oskar", avatar: DEMO_AV(4), page: null, away: true },
];
