// Find and replace in the open page (Ctrl+F, Ctrl+H): a small bar at the top right of the page, as in
// any editor. Every match is lit, the current one brighter; Enter goes to the next, Shift+Enter the one
// before, Escape closes it. Replace changes the current match, Replace all every one, as one step that
// Ctrl+Z undoes. In the Markdown view it works on the source. The rules of a match are src/find.js.
//
//   makeFindBar({ S, $, h, onAll }) → { Extension, open({ replace }), close(), refresh(), isOpen() }
//
// Extension goes in the editor's extensions (it draws the lit matches as decorations, never written
// to the page). onAll(q) opens Find in all pages with what's typed (src/find-all.js).

import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { findAll } from "./find.js";

const KEY = new PluginKey("codexFind");

/** Every match in a ProseMirror document: [{ from, to }], across marks within a paragraph. */
export function docMatches(doc, q, opts) {
  const out = [];
  if (!q) return out;
  doc.descendants((node, pos) => {
    if (!node.isTextblock) return true;
    let text = "";
    const map = []; // map[i]: the document position of character i
    node.forEach((child, offset) => {
      const at = pos + 1 + offset;
      if (child.isText) { for (let i = 0; i < child.text.length; i++) map.push(at + i); text += child.text; }
      else { map.push(at); text += "￼"; } // an image or a line break: never part of a match
    });
    for (const m of findAll(text, q, opts)) out.push({ from: map[m.start], to: map[m.end - 1] + 1 });
    return false;
  });
  return out;
}

export function makeFindBar({ S, $, h, onAll }) {
  const F = { open: false, matches: [], at: -1, matchCase: false, wholeWord: false };

  const plugin = new Plugin({
    key: KEY,
    state: {
      init: () => DecorationSet.empty,
      apply(tr, set) {
        const meta = tr.getMeta(KEY);
        if (meta) return DecorationSet.create(tr.doc, meta.map((m, i) => Decoration.inline(m.from, m.to, { class: "find-hit" + (i === F.at ? " current" : "") })));
        return set.map(tr.mapping, tr.doc);
      },
    },
    props: { decorations: (state) => KEY.getState(state) },
  });
  const FindMarks = Extension.create({ name: "codexFind", addProseMirrorPlugins: () => [plugin] });

  // ----------------------------------------------------------------------------- the bar

  const find = h("input", { class: "field find-input", placeholder: "Find", spellcheck: "false", "aria-label": "Find" });
  const count = h("span", { class: "find-count" });
  const toggle = (label, title, prop) => {
    const b = h("button", { class: "find-opt", title, onclick: () => { F[prop] = !F[prop]; b.classList.toggle("on", F[prop]); search(); find.focus(); } }, label);
    return b;
  };
  const caseBtn = toggle("Aa", "Match case", "matchCase");
  const wordBtn = toggle("W", "Whole words only", "wholeWord");
  const btn = (text, title, fn) => h("button", { class: "find-btn", title, onclick: fn }, text);
  const prev = btn("↑", "Previous (Shift+Enter)", () => step(-1));
  const next = btn("↓", "Next (Enter)", () => step(1));
  const showReplace = btn("⇅", "Replace (Ctrl+H)", () => setReplace(!F.replace));
  const all = btn("All pages", "Find in all pages (Ctrl+Shift+F)", () => { const q = find.value; close(); onAll(q); });
  const closeBtn = btn("✕", "Close (Esc)", () => close());
  const rep = h("input", { class: "field find-input", placeholder: "Replace with", spellcheck: "false", "aria-label": "Replace with" });
  const repOne = h("button", { class: "btn btn-small", onclick: () => replaceOne() }, "Replace");
  const repAll = h("button", { class: "btn btn-small", title: "Ctrl+Enter", onclick: () => replaceAll() }, "Replace all");
  const repRow = h("div", { class: "find-row find-replace hidden" }, rep, repOne, repAll);
  const bar = h("div", { class: "find-bar hidden", role: "search" },
    h("div", { class: "find-row" }, showReplace, find, count, caseBtn, wordBtn, prev, next, all, closeBtn), repRow);

  const source = () => S.mode === "source";
  const editable = () => (source() ? !S.readOnly : !!S.editor?.isEditable);

  function setReplace(on) {
    F.replace = on && editable();
    repRow.classList.toggle("hidden", !F.replace);
    showReplace.classList.toggle("on", F.replace);
  }

  /** Open the bar. q, matchCase, wholeWord and at: from Find in all pages, at its at-th match. */
  function open({ replace = false, q = null, matchCase, wholeWord, at = null } = {}) {
    if (!S.editor && !source()) return;
    if (!bar.isConnected) $("#main").append(bar);
    // What's selected is what to find, as in every editor.
    const picked = source() ? $("#source").value.slice($("#source").selectionStart, $("#source").selectionEnd) : selectedText();
    if (q != null) find.value = q;
    else if (picked && !picked.includes("\n") && picked.length <= 200) find.value = picked;
    if (matchCase != null) { F.matchCase = matchCase; caseBtn.classList.toggle("on", matchCase); }
    if (wholeWord != null) { F.wholeWord = wholeWord; wordBtn.classList.toggle("on", wholeWord); }
    F.open = true;
    bar.classList.remove("hidden");
    setReplace(replace);
    search({ keepAt: false });
    // (After the frame a page that just opened scrolls itself to the top in.)
    if (at != null && F.matches.length) { F.at = Math.min(at, F.matches.length - 1); paint(); requestAnimationFrame(() => requestAnimationFrame(reveal)); }
    (replace && find.value ? rep : find).focus();
    (replace && find.value ? rep : find).select();
  }

  function close() {
    if (!F.open) return;
    F.open = false;
    bar.classList.add("hidden");
    F.matches = []; F.at = -1;
    paint();
    if (!source()) S.editor?.view.focus(); else $("#source").focus();
  }

  function selectedText() {
    const ed = S.editor;
    if (!ed) return "";
    const { from, to } = ed.state.selection;
    return from === to ? "" : ed.state.doc.textBetween(from, to, "\n");
  }

  const opts = () => ({ matchCase: F.matchCase, wholeWord: F.wholeWord });

  /** Find again (typing, the page changing): stay on the match nearest where you were. */
  function search({ keepAt = true, from: start = null } = {}) {
    if (!F.open) return;
    const q = find.value;
    const before = F.matches[F.at];
    if (source()) F.matches = findAll($("#source").value, q, opts()).map((m) => ({ from: m.start, to: m.end }));
    else F.matches = S.editor ? docMatches(S.editor.state.doc, q, opts()) : [];
    const from = start != null ? start : keepAt && before ? before.from : source() ? $("#source").selectionStart : S.editor?.state.selection.from ?? 0;
    F.at = F.matches.length ? Math.max(0, F.matches.findIndex((m) => m.from >= from)) : -1;
    if (F.at < 0 && F.matches.length) F.at = 0;
    paint();
    if (!keepAt) reveal();
  }

  function paint() {
    count.textContent = !find.value ? "" : F.matches.length ? `${F.at + 1} of ${F.matches.length}` : "No results";
    count.classList.toggle("none", !!find.value && !F.matches.length);
    for (const b of [prev, next]) b.disabled = F.matches.length < 2;
    repOne.disabled = repAll.disabled = !F.matches.length;
    const view = S.editor?.view;
    if (view && !view.isDestroyed) view.dispatch(view.state.tr.setMeta(KEY, source() || !F.open ? [] : F.matches).setMeta("addToHistory", false));
  }

  /** Bring the current match into view (in the middle third, as the text search does). */
  function reveal() {
    const m = F.matches[F.at];
    if (!m) return;
    if (source()) {
      const ta = $("#source");
      ta.setSelectionRange(m.from, m.to);
      // Scroll the textarea to the match: measure the lines before it.
      const lines = ta.value.slice(0, m.from).split("\n").length;
      const lh = parseFloat(getComputedStyle(ta).lineHeight) || 22;
      ta.scrollTop = Math.max(0, (lines - 1) * lh - ta.clientHeight / 3);
      return;
    }
    const view = S.editor.view;
    const c = view.coordsAtPos(m.from);
    const sc = $("#scroller");
    sc.scrollTop += c.top - sc.getBoundingClientRect().top - sc.clientHeight / 3;
  }

  function step(d) {
    if (!F.matches.length) return;
    F.at = (F.at + d + F.matches.length) % F.matches.length;
    paint(); reveal();
  }

  function replaceOne() {
    const m = F.matches[F.at];
    if (!m || !editable()) return;
    if (source()) {
      const ta = $("#source");
      ta.setSelectionRange(m.from, m.to);
      ta.focus();
      document.execCommand("insertText", false, rep.value); // undoable, and fires input (saving)
      rep.focus();
    } else {
      const view = S.editor.view;
      view.dispatch(view.state.tr.insertText(rep.value, m.from, m.to));
    }
    search({ from: m.from + rep.value.length }); // on to the next, past what was put in
    reveal();
  }

  function replaceAll() {
    if (!F.matches.length || !editable()) return;
    const n = F.matches.length;
    if (source()) {
      const ta = $("#source");
      let text = ta.value;
      for (let i = F.matches.length - 1; i >= 0; i--) text = text.slice(0, F.matches[i].from) + rep.value + text.slice(F.matches[i].to);
      ta.focus();
      ta.select();
      document.execCommand("insertText", false, text);
      rep.focus();
    } else {
      const view = S.editor.view;
      const tr = view.state.tr;
      for (let i = F.matches.length - 1; i >= 0; i--) tr.insertText(rep.value, F.matches[i].from, F.matches[i].to);
      view.dispatch(tr);
    }
    search();
    count.textContent = `Replaced ${n}`;
  }

  find.addEventListener("input", () => search({ keepAt: false }));
  const keys = (e) => {
    e.stopPropagation(); // the page's own keys (Escape, ?) aren't for here
    if (e.key === "Escape") { e.preventDefault(); close(); }
    else if (e.key === "Enter" && e.target === rep && (e.ctrlKey || e.metaKey)) { e.preventDefault(); replaceAll(); }
    else if (e.key === "Enter" && e.target === rep) { e.preventDefault(); replaceOne(); }
    else if (e.key === "Enter") { e.preventDefault(); step(e.shiftKey ? -1 : 1); }
    else if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === "f" && !e.shiftKey) { e.preventDefault(); find.select(); }
    else if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === "h") { e.preventDefault(); setReplace(true); rep.focus(); }
  };
  find.addEventListener("keydown", keys);
  rep.addEventListener("keydown", keys);

  // The page changed under the bar (typing, a replace, another page opened): find again.
  let timer = null;
  function refresh() { if (!F.open) return; clearTimeout(timer); timer = setTimeout(() => search(), 120); }
  $("#source").addEventListener("input", refresh);

  return { Extension: FindMarks, open, close, refresh, isOpen: () => F.open };
}
