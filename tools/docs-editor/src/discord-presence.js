// "Editing Combat in the Codex" on your Discord profile (Rich Presence), through the Discord app on
// this computer: a local pipe, no server and no sign-in. If Discord isn't running, nothing happens and
// it tries again later. The page says what's open (POST /api/presence) and whether to show it at all
// (the logo's menu); with no Codex window open it clears.
//
// The pipe speaks Discord's RPC: frames of [opcode u32 LE, length u32 LE, JSON]. 0 is the handshake,
// 1 a command or event, 2 close, 3/4 ping/pong.

import net from "node:net";

const RETRY_MS = 60 * 1000;

export function makePresence({ clientId }) {
  let sock = null, ready = false, connecting = false, want = null, since = null, buf = Buffer.alloc(0), retry = null;

  function frame(op, data) {
    const json = Buffer.from(JSON.stringify(data), "utf8");
    const head = Buffer.alloc(8);
    head.writeUInt32LE(op, 0);
    head.writeUInt32LE(json.length, 4);
    return Buffer.concat([head, json]);
  }

  const pipe = (i) => (process.platform === "win32" ? `\\\\?\\pipe\\discord-ipc-${i}` : `${process.env.XDG_RUNTIME_DIR || process.env.TMPDIR || "/tmp"}/discord-ipc-${i}`);

  function connect(i = 0) {
    if (sock || connecting || i > 9) { if (i > 9) later(); return; }
    connecting = true;
    const s = net.createConnection(pipe(i));
    s.once("connect", () => {
      connecting = false;
      sock = s;
      s.write(frame(0, { v: 1, client_id: clientId }));
    });
    s.once("error", () => { connecting = false; if (sock !== s) connect(i + 1); else drop(); });
    s.on("data", (chunk) => {
      buf = Buffer.concat([buf, chunk]);
      while (buf.length >= 8) {
        const op = buf.readUInt32LE(0), len = buf.readUInt32LE(4);
        if (buf.length < 8 + len) break;
        let msg = null;
        try { msg = JSON.parse(buf.subarray(8, 8 + len).toString("utf8")); } catch {}
        buf = buf.subarray(8 + len);
        if (op === 3) s.write(frame(4, msg));
        else if (op === 2) drop();
        else if (msg?.evt === "READY") { ready = true; send(); }
      }
    });
    s.on("close", drop);
  }

  function drop() {
    if (sock) { try { sock.destroy(); } catch {} }
    sock = null; ready = false; connecting = false; buf = Buffer.alloc(0);
    if (want) later();
  }
  function later() { clearTimeout(retry); retry = setTimeout(() => { if (want) connect(); }, RETRY_MS); }

  function send() {
    if (!sock || !ready) return;
    const activity = want ? { details: clip("Editing " + want.title), state: "in the R'th Codex", timestamps: { start: since }, instance: false } : undefined;
    sock.write(frame(1, { cmd: "SET_ACTIVITY", args: { pid: process.pid, activity }, nonce: String(Date.now()) }));
  }

  return {
    /** What's being edited ({ page, title }), or null to clear. */
    set(v) {
      const before = want?.page;
      want = v && v.page ? v : null;
      if (want && want.page !== before) since = Math.floor(Date.now() / 1000);
      if (want && !sock) connect(); else send();
    },
  };
}

const clip = (s) => (s.length > 120 ? s.slice(0, 119) + "…" : s);
