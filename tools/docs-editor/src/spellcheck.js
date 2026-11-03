// The page's spelling check: words the server's speller (spell.js) doesn't know get a red wavy
// underline, drawn as decorations (never written into the page). Right-click one for suggestions,
// "Add to dictionary" or "Ignore". The browser's own check is off, because it follows the browser's
// language rather than the docs'.

import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";

const KEY = new PluginKey("codexSpelling");
const WORD = /[\p{L}][\p{L}'’]*/gu;
const known = new Map();      // word -> correct? (asked once a session)
const ignored = new Set();    // "Ignore" for this session

let api = null, toast = null;
export function configureSpelling(ctx) { api = ctx.api; toast = ctx.toast; }

/** Every word in the page's prose (not code), with its document range. */
function words(doc) {
  const out = [];
  doc.descendants((node, pos, parent) => {
    if (node.type.name === "codeBlock") return false;
    if (!node.isText) return;
    if (node.marks.some((m) => m.type.name === "code")) return;
    if (node.marks.some((m) => m.type.name === "link" && /^[a-z]+:|\.md/i.test(m.attrs.href || "") && node.text.includes("/"))) return;
    // File names and paths ("design-history.md", "docs/gdd") aren't prose: skip the words inside them.
    const skip = [...node.text.matchAll(/[\w./-]*\w\.[a-z]{1,5}\b|[\w.-]+\/[\w./-]+/gi)].map((m) => [m.index, m.index + m[0].length]);
    for (const m of node.text.matchAll(WORD)) {
      if (skip.some(([a, b]) => m.index >= a && m.index < b)) continue;
      const w = m[0].replace(/['’]+$/, "");
      out.push({ w, from: pos + m.index, to: pos + m.index + w.length });
    }
  });
  return out;
}

function build(doc) {
  const decos = [];
  for (const x of words(doc)) if (known.get(x.w) === false && !ignored.has(x.w.toLowerCase())) decos.push(Decoration.inline(x.from, x.to, { class: "spell-x", "data-word": x.w }));
  return DecorationSet.create(doc, decos);
}

export const Spelling = Extension.create({
  name: "codexSpelling",
  addProseMirrorPlugins() {
    let timer = null;
    return [new Plugin({
      key: KEY,
      state: {
        init: () => DecorationSet.empty,
        apply: (tr, old) => { const m = tr.getMeta(KEY); return m !== undefined ? m : old.map(tr.mapping, tr.doc); },
      },
      props: {
        decorations(state) { return KEY.getState(state); },
        handleDOMEvents: {
          contextmenu(view, ev) {
            const el = ev.target.closest?.(".spell-x");
            if (!el) return false;
            ev.preventDefault();
            menu(view, el, ev);
            return true;
          },
        },
      },
      view(view) {
        // Check after the page loads and whenever typing pauses.
        const run = async () => {
          if (!api) return;
          const doc = view.state.doc;
          const ask = [...new Set(words(doc).map((x) => x.w))].filter((w) => !known.has(w));
          if (ask.length) {
            const r = await api("/api/spell", { method: "POST", body: JSON.stringify({ words: ask }) });
            if (!r.ok) return;
            const wrong = new Set(r.wrong || []);
            for (const w of ask) known.set(w, !wrong.has(w));
          }
          if (view.isDestroyed) return;
          view.dispatch(view.state.tr.setMeta(KEY, build(view.state.doc)).setMeta("addToHistory", false));
        };
        const schedule = (delay) => { clearTimeout(timer); timer = setTimeout(run, delay); };
        schedule(50);
        return {
          update(v, prev) { if (!v.state.doc.eq(prev.doc)) schedule(700); },
          destroy() { clearTimeout(timer); },
        };
      },
    })];
  },
});

function closeMenu() { document.querySelector(".spell-menu")?.remove(); document.removeEventListener("mousedown", outside, true); }
const outside = (ev) => { if (!ev.target.closest(".spell-menu")) closeMenu(); };

async function menu(view, el, ev) {
  closeMenu();
  const word = el.dataset.word;
  const pos = view.posAtDOM(el.firstChild || el, 0);
  const m = document.createElement("div");
  m.className = "spell-menu cm-menu";
  const item = (label, fn, cls = "") => {
    const b = document.createElement("button");
    b.className = "cm-menu-item " + cls;
    b.textContent = label;
    b.addEventListener("mousedown", (e) => e.preventDefault());
    b.addEventListener("click", () => { closeMenu(); fn(); });
    m.append(b);
    return b;
  };
  const hint = document.createElement("div");
  hint.className = "spell-hint";
  hint.textContent = "Looking for suggestions…";
  m.append(hint);
  const sep = document.createElement("div"); sep.className = "spell-sep"; m.append(sep);
  item(`Add “${word}” to the dictionary`, async () => {
    await api("/api/spell/add", { method: "POST", body: JSON.stringify({ word }) });
    known.set(word, true);
    refresh(view);
    toast?.(`“${word}” added to the dictionary`);
  });
  item("Ignore it for now", () => { ignored.add(word.toLowerCase()); refresh(view); });
  document.body.append(m);
  m.style.left = Math.min(ev.clientX, window.innerWidth - m.offsetWidth - 8) + "px";
  m.style.top = Math.min(ev.clientY + 4, window.innerHeight - m.offsetHeight - 8) + "px";
  setTimeout(() => document.addEventListener("mousedown", outside, true), 0);

  const r = await api("/api/spell/suggest?w=" + encodeURIComponent(word));
  if (!m.isConnected) return;
  const list = r.suggestions || [];
  if (!list.length) { hint.textContent = "No suggestions"; return; }
  hint.remove();
  for (const s of list.reverse()) {
    const b = document.createElement("button");
    b.className = "cm-menu-item spell-suggestion";
    b.textContent = s;
    b.addEventListener("mousedown", (e) => e.preventDefault());
    b.addEventListener("click", () => {
      closeMenu();
      // Replace the word where it is now (the page may have changed since).
      const text = view.state.doc.textBetween(pos, Math.min(pos + word.length, view.state.doc.content.size));
      if (text === word) view.dispatch(view.state.tr.insertText(s, pos, pos + word.length));
    });
    m.prepend(b);
  }
}

function refresh(view) { view.dispatch(view.state.tr.setMeta(KEY, build(view.state.doc)).setMeta("addToHistory", false)); }
