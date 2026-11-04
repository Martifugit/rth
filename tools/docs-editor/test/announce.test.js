// The release message in #codex (scripts/announce.mjs): the link always there, never over Discord's limit.
import assert from "node:assert/strict";
import { message } from "../scripts/announce.mjs";

for (const v of ["0.3.2", "0.3.1", "0.3.0"]) {
  const m = message(v, "Martifugit/rth");
  assert.ok(m.length <= 2000, `${v}: within Discord's 2000 characters (${m.length})`);
  assert.match(m.split("\n")[0], new RegExp(`^\\*\\*R'th Codex ${v.replace(/\./g, "\\.")}\\*\\* is out: \\[release notes and installer\\]\\(<https://github\\.com/Martifugit/rth/releases/tag/codex-v${v.replace(/\./g, "\\.")}>\\)$`), `${v}: the link first`);
  assert.match(m.split("\n").at(-1), /^-# Installed Codexes update themselves/, `${v}: the footer last, whole`);
  assert.doesNotMatch(m, /…$/m, `${v}: no line cut mid-sentence`);
}
assert.match(message("0.3.2", "Martifugit/rth"), /…and \d+ more in the \[release notes\]/, "a long changelog ends in a link to the rest");
console.log("announce: passed");
