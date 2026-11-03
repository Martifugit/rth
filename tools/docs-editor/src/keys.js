// Keys on any keyboard layout. On Norwegian (and most European) layouts, AltGr is Ctrl+Alt to the
// browser, so AltGr+2 (@), AltGr+7 ({) and the like look like Ctrl+Alt shortcuts: the editor's
// heading keys (Ctrl+Alt+1-6) turned a line into a heading when you typed @. And keys named by their
// US position (Ctrl+\) sit somewhere else, or behind AltGr. This asks the browser for the layout
// (navigator.keyboard.getLayoutMap, in Chromium and the desktop app) and falls back to US positions.

let layout = null; // code ("Digit2") -> what the key types unshifted ("2")

async function loadLayout() {
  try { layout = await navigator.keyboard?.getLayoutMap?.(); } catch { layout = null; }
}
loadLayout();
try { navigator.keyboard?.addEventListener?.("layoutchange", loadLayout); } catch {}

/** What the key types with no modifiers, by its position: "2" for Digit2, "m" for KeyM. */
function base(code) {
  const v = layout?.get?.(code);
  if (v) return v;
  let m = /^Key([A-Z])$/.exec(code);
  if (m) return m[1].toLowerCase();
  m = /^Digit(\d)$/.exec(code);
  return m ? m[1] : null;
}

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
