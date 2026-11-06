// The list that opens while you type in a text box: @ pages in the AI box. One component, so any
// box that gets one behaves the same; when it opens and closes is
// src/mention-query.js. Arrows move, Enter or Tab picks, Escape closes it for that character.
//
//   textPicker({ h, input, pop, triggers, onPick }) → { key(ev), draw(), close(), reset() }
//
// triggers: [{ char: "@", startOnly?, find(q) → items, label(item) → node(s), token(item) → "@Title",
//              none: "No page by that name" }]. onPick(item, trigger, token) runs after the text is in.
// The box's own keydown handler calls key(ev) first and stops if it returns true.

import { mentionQuery } from "./mention-query.js";

export function textPicker({ h, input, pop, triggers, onPick = () => {} }) {
  let items = [], at = 0, picked = [], dismissed = -1, open = null; // open: { trigger, start, q }

  const caret = () => input.value.slice(0, input.selectionStart);
  function current() {
    if (input.selectionStart !== input.selectionEnd) return null;
    const before = caret();
    // Escape's character is forgotten once it's gone or the cursor is before it.
    if (dismissed >= 0 && (dismissed >= before.length || !triggers.some((t) => t.char === before[dismissed]))) dismissed = -1;
    for (const t of triggers) {
      const m = mentionQuery(before, { char: t.char, startOnly: t.startOnly, picked, dismissed, matches: (q) => t.find(q).length > 0 });
      if (m) return { trigger: t, ...m };
    }
    return null;
  }

  function close() { pop.classList.add("hidden"); items = []; at = 0; open = null; }

  function draw() {
    open = current();
    if (!open) return close();
    items = open.none ? [] : open.trigger.find(open.q).slice(0, 8);
    at = Math.min(at, Math.max(0, items.length - 1));
    pop.replaceChildren(...(items.length
      ? items.map((it, i) => h("div", { class: "picker-item" + (i === at ? " on" : ""), onmousedown: (e) => { e.preventDefault(); pick(i); }, onmouseenter: () => { at = i; mark(); } }, open.trigger.label(it)))
      : [h("div", { class: "picker-none" }, open.trigger.none || "Nothing by that name")]));
    pop.classList.remove("hidden");
  }
  const mark = () => [...pop.children].forEach((el, i) => el.classList.toggle("on", i === at));

  function pick(i) {
    const it = items[i], o = open;
    if (!it || !o) return;
    const token = o.trigger.token(it);
    const before = caret().slice(0, o.start) + token + " ";
    input.value = before + input.value.slice(input.selectionStart);
    input.setSelectionRange(before.length, before.length);
    if (!picked.includes(token)) picked.push(token);
    close();
    input.dispatchEvent(new Event("input", { bubbles: true })); // the box grows, drafts are kept
    onPick(it, o.trigger, token);
  }

  function key(ev) {
    if (pop.classList.contains("hidden")) return false;
    if (ev.key === "Escape") { ev.preventDefault(); ev.stopPropagation(); dismissed = open?.start ?? -1; close(); return true; }
    if (!items.length) return false;
    if (ev.key === "ArrowDown" || ev.key === "ArrowUp") { ev.preventDefault(); at = (at + (ev.key === "ArrowDown" ? 1 : items.length - 1)) % items.length; mark(); return true; }
    if (ev.key === "Enter" || ev.key === "Tab") { ev.preventDefault(); pick(at); return true; }
    return false;
  }

  input.addEventListener("input", draw);
  input.addEventListener("blur", () => setTimeout(close, 150));
  // The cursor leaving the word (a click, the arrows, Home/End) closes it; coming back opens it.
  input.addEventListener("click", draw);
  input.addEventListener("keyup", (ev) => { if (/^(ArrowLeft|ArrowRight|Home|End)$/.test(ev.key)) draw(); });

  /** A new message: nothing picked or dismissed any more. */
  function reset() { picked = []; dismissed = -1; close(); }
  return { key, draw, close, reset };
}
