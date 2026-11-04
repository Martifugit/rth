// An out-of-date Codex is refused and told to update (helper/src/version.js, src/codex-version.js).
// Run: node test/version.test.js
import assert from "node:assert/strict";
import { compare as helperCompare, tooOld, outdated, updatePage } from "../helper/src/version.js";
import { compare, VERSION, VERSION_HEADERS, noteMinimum, noteAnswer, required, onRequired } from "../src/codex-version.js";

let n = 0;
const t = async (name, fn) => { await fn(); n++; };
const ask = (version) => new Request("https://helper/git-token", { method: "POST", headers: version ? { "x-codex-version": version } : {} });

await t("versions compare by number, not by text", () => {
  for (const c of [compare, helperCompare]) {
    assert.equal(c("0.3.0", "0.3.0"), 0);
    assert.equal(c("0.2.9", "0.3.0"), -1);
    assert.equal(c("0.10.0", "0.9.0"), 1);
    assert.equal(c("1.0", "1.0.0"), 0);
    assert.equal(c("0.3.0-beta", "0.3.0"), 0);
  }
});

await t("the helper refuses older and unnamed Codexes, and lets the rest through", async () => {
  const env = { MIN_CODEX: "0.3.0" };
  assert.equal(tooOld(ask("0.3.0"), env), null);
  assert.equal(tooOld(ask("0.4.1"), env), null);
  for (const r of [tooOld(ask("0.2.0"), env), tooOld(ask(null), env)]) { // 0.1.x sent no version at all
    assert.equal(r.status, 426);
    const j = await r.json();
    assert.equal(j.update, true); assert.equal(j.min, "0.3.0");
    assert.match(j.error, /out of date/);
  }
  assert.equal(tooOld(ask(null), {}), null, "no minimum set: nobody is refused");
});

await t("an out-of-date Codex's Sign in leads to the update page, not Discord", async () => {
  const env = { MIN_CODEX: "0.3.0" };
  assert.equal(outdated(null, env), true, "0.1.x put no version in its sign-in link");
  assert.equal(outdated("0.2.0", env), true);
  assert.equal(outdated("0.3.0", env), false);
  assert.equal(outdated(null, {}), false);
  const r = updatePage(env);
  assert.equal(r.status, 200);
  const html = await r.text();
  assert.match(html, /An update is needed/);
  assert.match(html, /releases\/latest/);
  assert.match(html, /0\.3\.0 or newer/);
});

await t("this Codex names its version, and knows when it's out of date", () => {
  assert.equal(VERSION_HEADERS["X-Codex-Version"], VERSION);
  assert.match(VERSION, /^\d+\.\d+\.\d+/);
  const seen = [];
  onRequired((r) => seen.push(r.outdated));
  noteMinimum(VERSION);
  assert.equal(required().outdated, false);
  noteMinimum("999.0.0");
  assert.equal(required().outdated, true);
  noteMinimum(null);
  assert.equal(required().outdated, false);
  noteAnswer(426, { min: "999.0.0" }); // a refusal says so too, whatever the last check said
  assert.equal(required().outdated, true);
  assert.deepEqual(seen, [false, true, false, true]); // every change of minimum is told, even when it changes nothing
});

console.log(`version: ${n} passed`);
