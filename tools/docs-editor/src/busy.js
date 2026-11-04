// A button whose action takes a moment (saving and sending, undoing, creating a page): pressed, it
// says what it's doing ("Saving…") with a small spinner, keeps its width, and can't be pressed again
// until the action is over. The Codex waits for the engine rather than guessing the outcome, so without
// this a slow action looked like a click that did nothing.

/**
 * Run `fn` with `btn` busy, labelled `label`. Returns what fn returns (undefined if already busy).
 * Pass the click's event or the button itself (or null: then it just runs fn).
 */
export async function busy(btnOrEvent, label, fn) {
  const btn = btnOrEvent?.currentTarget || btnOrEvent;
  if (!btn) return fn(); // no button to show it on (a keyboard shortcut, another window's button)
  if (btn.classList.contains("is-busy")) return;
  const before = [...btn.childNodes], width = btn.style.minWidth, wasDisabled = btn.disabled;
  btn.style.minWidth = btn.offsetWidth + "px"; // "Save" → "Saving…" never makes the row jump narrower
  btn.classList.add("is-busy");
  btn.disabled = true;
  btn.setAttribute("aria-busy", "true");
  const spin = document.createElement("span");
  spin.className = "busy-spin";
  btn.replaceChildren(spin, label);
  try {
    return await fn();
  } finally {
    // The action often redraws its own panel; a button still on screen goes back to how it was.
    if (btn.isConnected) {
      btn.replaceChildren(...before);
      btn.classList.remove("is-busy");
      btn.disabled = wasDisabled;
      btn.removeAttribute("aria-busy");
      btn.style.minWidth = width;
    }
  }
}
