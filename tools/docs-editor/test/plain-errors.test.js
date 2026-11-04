// Errors in words anyone can act on (src/plain-errors.js).
import assert from "node:assert/strict";
import { failed } from "../src/plain-errors.js";

console.warn = () => {}; // failed() logs the technical cause for whoever fixes it; not needed here

const S = "Couldn't save the page";
assert.equal(failed(S, { status: 400, error: "The top pages and the templates can't be deleted." }),
  "Couldn't save the page: The top pages and the templates can't be deleted.", "a sentence for people is kept");
assert.match(failed(S, { status: 500, error: "EBUSY: resource busy or locked, open 'docs/gdd/combat.md'" }), /open in another program/);
assert.match(failed(S, { status: 500, error: "ENOSPC: no space left on device" }), /disk is full/);
assert.match(failed(S, { status: 500, error: "The helper failed: The helper said 502" }), /not on your side/);
assert.match(failed(S, { status: 500, error: "TypeError: fetch failed" }), /couldn't be reached/);
assert.match(failed(S, { status: 0 }), /engine isn't answering/);
assert.match(failed(S, { status: 500, error: "TypeError: Cannot read properties of undefined (reading 'x')" }), /went wrong inside Codex.*safe/);
assert.match(failed(S, { status: 500 }), /went wrong inside Codex/, "a bare status");
for (const r of [{ status: 500, error: "TypeError: x" }, { status: 0 }, { status: 502 }])
  assert.doesNotMatch(failed(S, r), /TypeError|\b50\d\b|ENO|EBUSY/, "no codes shown to people");
console.log("plain errors: passed");
