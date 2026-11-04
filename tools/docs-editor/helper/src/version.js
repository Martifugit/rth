// The oldest Codex that may change shared things (send pages to GitHub, write comments): MIN_CODEX in
// wrangler.toml. Raise it when a release changes the GDD in ways older Codexes would damage, after that
// release is published (they update to it). Each Codex says its version in the X-Codex-Version header;
// those from before the header existed (0.1.x) send none and count as too old once a minimum is set.
// Reading is never refused: an old Codex can still show the pages and say it needs updating.

import { codexPage, REPO, esc } from "../../src/codex-page.js";

export const HEADER = "x-codex-version";

/** -1, 0 or 1 as version a is older than, the same as, or newer than b ("0.3.0"; missing parts are 0). */
export function compare(a, b) {
  const pa = String(a).split(/[.-]/).map((x) => parseInt(x, 10) || 0);
  const pb = String(b).split(/[.-]/).map((x) => parseInt(x, 10) || 0);
  for (let i = 0; i < 3; i++) if ((pa[i] || 0) !== (pb[i] || 0)) return (pa[i] || 0) < (pb[i] || 0) ? -1 : 1;
  return 0;
}

/** Whether a Codex of version `v` (null: one from before versions were sent) is older than MIN_CODEX. */
export const outdated = (v, env) => !!env.MIN_CODEX && !(v && compare(v, env.MIN_CODEX) >= 0);

/** A 426 to send back when the asking Codex is older than MIN_CODEX, or null when it may go on. */
export function tooOld(req, env) {
  const min = env.MIN_CODEX;
  const v = req.headers.get(HEADER);
  if (!outdated(v, env)) return null;
  // The words an old Codex shows as they are (as a sync problem, or with a comment that wasn't sent).
  const error = "This version of Codex is out of date: a new release has changes it can't work with, so it can't send anything. "
    + "Close Codex and open it again to update it (if it opens unchanged, wait a minute for the update to download, then close it again).";
  return new Response(JSON.stringify({ error, update: true, min }), { status: 426, headers: { "content-type": "application/json" } });
}

// A Codex from before versions were sent (0.1.x) can't be told anything on its own screen. So the helper
// signs it out (GET /me answers 401, index.js): it opens reading only, with a Sign in button, and that
// button opens /auth/discord/start in the browser. Newer Codexes add their version to that link (v=);
// without it, or with an old one, the browser gets this page instead of Discord.
const RELEASES = "https://github.com/Martifugit/rth/releases/latest";

export function updatePage(env) {
  const html = codexPage({
    title: "Update R'th Codex", heading: "An update is needed", assets: REPO,
    body: `<p>A new release of R'th Codex has changes this version can't work with. Update to the latest version to keep using Codex; until then it can only show pages.</p>
    <ol>
      <li>Close Codex.</li>
      <li>Open it again. It installs the update it has already downloaded; if it opens unchanged, wait a minute and close it once more.</li>
      <li>Sign in with Discord again, top right.</li>
    </ol>
    <p>This is the last time it works like this: from this update on, Codex tells you itself when an update is needed and installs it with one click.</p>
    <p class="alt">If it still doesn't update, <a href="${RELEASES}">download the new version of Codex</a> and run it.
      <small>Version ${esc(env.MIN_CODEX || "")} or newer is needed. You can close this page.</small></p>`,
  });
  return new Response(html, { status: 200, headers: { "content-type": "text/html; charset=utf-8" } });
}
