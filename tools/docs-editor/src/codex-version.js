// This Codex's version, and whether it's too old to be used: the helper names the oldest Codex that may
// change things (helper/src/version.js, MIN_CODEX). The engine asks it at start and every few minutes,
// and any refusal from the helper (426) says so too. Out of date, the page covers itself with the
// update screen (src/app.js, showUpdateScreen) until the new Codex is installed.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const VERSION = (() => {
  try { return JSON.parse(fs.readFileSync(path.join(HERE, "..", "package.json"), "utf8")).version || "0.0.0"; } catch { return "0.0.0"; }
})();

/** Sent with every request to the helper, so it can refuse an out-of-date Codex. */
export const VERSION_HEADERS = { "X-Codex-Version": VERSION };

/** -1, 0 or 1 as version a is older than, the same as, or newer than b. */
export function compare(a, b) {
  const pa = String(a).split(/[.-]/).map((x) => parseInt(x, 10) || 0);
  const pb = String(b).split(/[.-]/).map((x) => parseInt(x, 10) || 0);
  for (let i = 0; i < 3; i++) if ((pa[i] || 0) !== (pb[i] || 0)) return (pa[i] || 0) < (pb[i] || 0) ? -1 : 1;
  return 0;
}

const state = { current: VERSION, min: null, outdated: false };
const listeners = new Set();

/** The helper named a minimum: out of date when this Codex is older. */
export function noteMinimum(min) {
  const outdated = !!min && compare(VERSION, min) < 0;
  const changed = outdated !== state.outdated || min !== state.min;
  state.min = min || null; state.outdated = outdated;
  if (changed) for (const fn of listeners) fn({ ...state });
}

/** A helper answer: a 426 means this Codex is out of date, whatever the last check said. */
export function noteAnswer(status, body) {
  if (status === 426) noteMinimum(body?.min || state.min || "999.0.0");
}

export const required = () => ({ ...state });
export const onRequired = (fn) => listeners.add(fn);

/** Ask the helper now, then every `everyMs`. */
export function watchMinimum(helper, everyMs = 10 * 60 * 1000) {
  const ask = async () => {
    try {
      const r = await fetch(helper + "/version", { headers: VERSION_HEADERS });
      if (r.ok) noteMinimum((await r.json()).min);
    } catch {} // offline: keep what we know
  };
  ask();
  setInterval(ask, everyMs).unref?.();
}
