// Keys on any keyboard layout. On Norwegian (and most European) layouts, AltGr is Ctrl+Alt to the
// browser, so AltGr+2 (@), AltGr+7 ({) and the like look like Ctrl+Alt shortcuts: the editor's
// heading keys (Ctrl+Alt+1-6) turned a line into a heading when you typed @. And keys named by their
// US position (Ctrl+\) sit somewhere else, or behind AltGr. This asks the browser for the layout
// (navigator.keyboard.getLayoutMap, in Chromium and the desktop app) and falls back to US positions.
//
// Shortcuts are written as specs: "mod+shift+f" (a letter, by what it types), "mod+shift+Digit1" (a
// key by its place, for digits and punctuation that Shift changes), "alt+ArrowLeft" (a named key),
// "?" (a character, however it's typed). is(e, spec) tests a key press; label(spec) says it for the
// Keys window, with this layout's characters. Tested in test/keys.test.js.

let layout = null; // code ("Digit2") -> what the key types unshifted ("2")

async function loadLayout() {
  try { layout = await navigator.keyboard?.getLayoutMap?.(); } catch { layout = null; }
}
loadLayout();
try { navigator.keyboard?.addEventListener?.("layoutchange", loadLayout); } catch { /* no layout events: US positions */ }

/** For tests: a layout map as the browser gives it, or null for US positions. */
export function useLayout(map) { layout = map; }

/** What the key types with no modifiers, by its position: "2" for Digit2, "m" for KeyM. */
function base(code) {
  const v = layout?.get?.(code);
  if (v) return v;
  let m = /^Key([A-Z])$/.exec(code);
  if (m) return m[1].toLowerCase();
  m = /^Digit(\d)$/.exec(code);
  if (m) return m[1];
  return US[code] || null;
}
const US = { Comma: ",", Period: ".", Slash: "/", Backslash: "\\", Minus: "-", Equal: "=", Semicolon: ";", Quote: "'", BracketLeft: "[", BracketRight: "]", Backquote: "`" };

/**
 * A character typed with AltGr (Ctrl+Alt with a key that, so held, types something else): it should
 * be typed, not taken as a shortcut.
 */
export function altGrChar(e) {
  if (!e.ctrlKey || !e.altKey || e.metaKey || !e.key || [...e.key].length !== 1) return null;
  if (e.getModifierState?.("AltGraph")) return e.key;
  const b = base(e.code);
  return b != null && e.key.toLowerCase() !== b.toLowerCase() ? e.key : null;
}

/** Is this the key that types ch on this layout (Shift or not)? Falls back to its US position. */
export function isKeyFor(e, ch, usCode) {
  if (layout) {
    for (const [code, v] of layout) if (v === ch) return e.code === code;
  }
  return e.code === usCode || e.key === ch;
}

const NAMED = new Set(["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End", "Enter", "Escape", "Backspace", "Tab"]);
const parse = (spec) => {
  const parts = spec.split("+").filter(Boolean);
  const key = spec.endsWith("++") ? "+" : parts.pop();
  const mods = new Set(parts.map((p) => p.toLowerCase()));
  return { key, mod: mods.has("mod"), shift: mods.has("shift"), alt: mods.has("alt") };
};
const isCode = (k) => /^(Digit\d|Key[A-Z]|Comma|Period|Slash|Backslash|Minus|Equal|Semicolon|Quote|Bracket(Left|Right)|Backquote)$/.test(k);

/** Does this key press match the spec? Modifiers must match exactly, except Shift for a bare character. */
export function is(e, spec) {
  const s = parse(spec);
  const mod = e.ctrlKey || e.metaKey;
  if (mod !== s.mod || e.altKey !== s.alt) return false;
  if (s.alt && s.mod && altGrChar(e)) return false; // AltGr typing a character, not Ctrl+Alt
  if (isCode(s.key)) return e.shiftKey === s.shift && e.code === s.key;
  if (NAMED.has(s.key)) return e.shiftKey === s.shift && e.key === s.key;
  if (/^[a-z]$/.test(s.key)) return e.shiftKey === s.shift && (e.key || "").toLowerCase() === s.key;
  return e.key === s.key; // "?", "/": whatever it takes to type them
}

const SHOWN = { mod: "Ctrl", shift: "Shift", alt: "Alt", ArrowLeft: "←", ArrowRight: "→", ArrowUp: "↑", ArrowDown: "↓", Escape: "Esc", PageUp: "Page Up", PageDown: "Page Down" };

/** The keys of a spec as they're labelled on this keyboard: ["Ctrl", "Shift", "1"]. */
export function label(spec) {
  const s = parse(spec);
  const out = [];
  if (s.mod) out.push(SHOWN.mod);
  if (s.shift) out.push(SHOWN.shift);
  if (s.alt) out.push(SHOWN.alt);
  const b = isCode(s.key) ? base(s.key) : null;
  out.push(SHOWN[s.key] || (b ? b.toUpperCase() : s.key.length === 1 ? s.key.toUpperCase() : s.key));
  return out;
}
