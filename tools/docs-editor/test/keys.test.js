// Shortcuts on any keyboard layout (src/keys.js), with a Norwegian layout as the browser reports it.
import assert from "node:assert/strict";
import { is, label, altGrChar, useLayout } from "../src/keys.js";

let passed = 0;
function test(name, fn) { try { fn(); passed++; } catch (e) { console.error("FAIL", name, "\n ", e.stack); process.exitCode = 1; } }

// What Norwegian keys type unshifted, by position (the keys that differ from US).
const NO = new Map([
  ...[..."abcdefghijklmnopqrstuvwxyz"].map((c) => ["Key" + c.toUpperCase(), c]),
  ...[..."0123456789"].map((c) => ["Digit" + c, c]),
  ["Minus", "+"], ["Equal", "\\"], ["BracketLeft", "å"], ["BracketRight", "¨"], ["Semicolon", "ø"], ["Quote", "æ"],
  ["Backslash", "'"], ["Backquote", "|"], ["Comma", ","], ["Period", "."], ["Slash", "-"], ["IntlBackslash", "<"],
]);
const key = (code, k, mods = {}) => ({ code, key: k, ctrlKey: !!mods.ctrl, metaKey: false, altKey: !!mods.alt, shiftKey: !!mods.shift, getModifierState: () => !!mods.altGraph });

test("letters and Ctrl: the same everywhere", () => {
  useLayout(NO);
  assert.ok(is(key("KeyF", "f", { ctrl: true }), "mod+f"));
  assert.ok(!is(key("KeyF", "F", { ctrl: true, shift: true }), "mod+f"));
  assert.ok(is(key("KeyF", "F", { ctrl: true, shift: true }), "mod+shift+f"));
});
test("Ctrl+Shift+1 is a heading on Norwegian, where Shift+1 types !", () => {
  useLayout(NO);
  assert.ok(is(key("Digit1", "!", { ctrl: true, shift: true }), "mod+shift+Digit1"));
  assert.deepEqual(label("mod+shift+Digit1"), ["Ctrl", "Shift", "1"]);
});
test("? and / however they're typed (Shift++ and Shift+7 on Norwegian)", () => {
  useLayout(NO);
  assert.ok(is(key("Minus", "?", { shift: true }), "?"));
  assert.ok(is(key("Digit7", "/", { shift: true }), "/"));
  assert.ok(!is(key("Digit7", "/", { ctrl: true, shift: true }), "/")); // Ctrl+Shift+7 is the numbered list
});
test("AltGr typing a character is never Ctrl+Alt", () => {
  useLayout(NO);
  const at = key("Digit2", "@", { ctrl: true, alt: true });
  assert.equal(altGrChar(at), "@");
  assert.ok(!is(at, "mod+alt+Digit2"));
  assert.equal(altGrChar(key("KeyC", "c", { ctrl: true, alt: true })), null); // Ctrl+Alt+C: a code block
});
test("the Keys window names keys as this keyboard has them", () => {
  useLayout(NO);
  assert.deepEqual(label("mod+Backslash"), ["Ctrl", "'"]);
  assert.deepEqual(label("mod+Comma"), ["Ctrl", ","]);
  assert.deepEqual(label("alt+PageDown"), ["Alt", "Page Down"]);
  useLayout(null);
  assert.deepEqual(label("mod+Backslash"), ["Ctrl", "\\"]);
});
test("without the layout API: US places, and AltGr on Norwegian still types", () => {
  useLayout(null);
  assert.equal(altGrChar(key("Digit2", "@", { ctrl: true, alt: true })), "@");
  assert.ok(is(key("Comma", ",", { ctrl: true }), "mod+Comma"));
});

console.log(`keys: ${passed} passed`);
