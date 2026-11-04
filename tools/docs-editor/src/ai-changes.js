// Show changes (PLAN.md, "AI, built in"): what the AI changed on the open page, the way Notion and
// ChatGPT's canvas show it. A page the AI changed says so in a bar at its top. Off, you read the new
// text clean; on, what it added is highlighted and what it removed is shown struck through, with
// arrows from one change to the next. Pointing at a change offers Undo for just that one. Done clears
// the marks.
//
// The comparison is between the page before the AI's first unreviewed change (the engine keeps it) and
// the editor as it is now, block by block (ProseMirror's own node equality), then word by word inside
// blocks that changed. Undoing a change puts the old blocks back, formatting and all, as an edit.

import { busy } from "./busy.js";
import { Extension, createDocument } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { DOMSerializer, Fragment } from "@tiptap/pm/model";
import { diffSeq, diffWords, sameShare } from "./ai-diff.js";
import { split as splitFront } from "./frontmatter.js";

const KEY = new PluginKey("aiChanges");

/** The decorations, set from outside with a transaction's meta. */
export const AiMarks = Extension.create({
  name: "aiMarks",
  addProseMirrorPlugins() {
    return [new Plugin({
      key: KEY,
      state: {
        init: () => DecorationSet.empty,
        apply: (tr, set) => (tr.getMeta(KEY) !== undefined ? tr.getMeta(KEY) : set.map(tr.mapping, tr.doc)),
      },
      props: { decorations: (state) => KEY.getState(state) },
    })];
  },
});

// A page waiting to be shared (src/sync.js, held) has Share in place of Done; one changed by another
// program is compared with how it was last shared (GET /api/sync/base), the same way.
export function makeAiChanges({ S, $, h, api, toast, scrollToPos, held = () => null, share = () => {}, setFront = null }) {
  const C = { changes: [], showing: false, hunks: [], at: -1, base: null, page: null, suggest: false, target: null, skipped: new Set() };
  const bar = h("div", { id: "aiBar", class: "ai-bar hidden" });
  const undoBtn = h("button", { class: "ai-undo-one hidden", title: "Undo this change", onmousedown: (e) => e.preventDefault(), onclick: () => undoHunk(Number(undoBtn.dataset.hunk)) }, "Undo");
  document.body.append(undoBtn);

  // ----------------------------------------------------------------------------- the bar

  /** The AI's suggested frontmatter, when it differs from the page's: { front, says: "status stub → draft" }. */
  function frontSuggestion() {
    if (!C.suggest || C.frontDone || !setFront) return null;
    const want = splitFront(C.changes[C.changes.length - 1]?.after ?? "");
    if (!want.front || want.front.trim() === String(S.front || "").trim()) return null;
    const now = splitFront((S.front || "") + "\n").data || {}, next = want.data || {};
    const show = (v) => (Array.isArray(v) ? (v.length ? v.join(", ") : "none") : v == null || v === "" ? "none" : String(v));
    const says = [...new Set([...Object.keys(now), ...Object.keys(next)])]
      .filter((k) => show(now[k]) !== show(next[k])).map((k) => `${k} ${show(now[k])} → ${show(next[k])}`).join(", ");
    return says ? { front: want.front, says } : null;
  }
  function acceptFront() { const f = frontSuggestion(); if (f) setFront(f.front); C.frontDone = true; after(); }
  function skipFront() { C.frontDone = true; after(); }

  function drawBar() {
    const has = C.changes.length > 0 && C.page === S.path && S.mode !== "source";
    bar.classList.toggle("hidden", !has);
    if (!has) return;
    const n = C.hunks.length;
    if (C.suggest) {
      // Suggestions, gone through top to bottom: Accept or Skip each.
      const cur = C.at >= 0 ? C.at : 0;
      const fs_ = frontSuggestion();
      bar.replaceChildren(...[
        h("span", { class: "ai-bar-ico" }, "✦"),
        h("span", { class: "ai-bar-text" }, n ? `The AI suggests ${n} change${n > 1 ? "s" : ""}` : fs_ ? "One more suggestion" : "No suggestions left"),
        // The page's header (status, type, groups...) changed too: said plainly, answered on its own.
        fs_ ? h("span", { class: "ai-bar-front" }, "Also: ", h("b", {}, fs_.says),
          h("button", { class: "ai-link", title: "Leave the page's header as it is", onclick: skipFront }, "Skip"),
          h("button", { class: "ai-link", title: "Change the page's header", onclick: acceptFront }, "Accept")) : null,
        n ? h("span", { class: "ai-bar-nav" },
          h("button", { class: "ai-bar-btn", title: "Previous suggestion", onclick: () => step(-1) }, "‹"),
          h("span", { class: "ai-bar-pos" }, `${cur + 1} of ${n}`),
          h("button", { class: "ai-bar-btn", title: "Next suggestion", onclick: () => step(1) }, "›")) : null,
        h("span", { class: "ai-bar-gap" }),
        n ? h("button", { class: "btn", title: "Leave this one as it is", onclick: () => skip(cur) }, "Skip") : null,
        n ? h("button", { class: "btn btn-gold", title: "Make this change", onclick: () => accept(cur) }, "Accept") : null,
        n > 1 ? h("button", { class: "btn", title: "Make every suggested change", onclick: acceptAll }, "Accept all") : null,
        h("button", { class: "btn", title: "Drop the suggestions that are left", onclick: done }, "Done"),
      ].filter(Boolean));
      return;
    }
    bar.replaceChildren(...[
      h("span", { class: "ai-bar-ico" }, "✦"),
      h("span", { class: "ai-bar-text" }, C.other
        ? (C.showing ? (n ? `${n} change${n > 1 ? "s" : ""} not saved yet` : "No changes left") : "Changed outside the Codex")
        : C.showing ? (n ? `${n} change${n > 1 ? "s" : ""} by the AI` : "No changes left") : "The AI changed this page"),
      C.showing && n ? h("span", { class: "ai-bar-nav" },
        h("button", { class: "ai-bar-btn", title: "Previous change", onclick: () => step(-1) }, "‹"),
        h("span", { class: "ai-bar-pos" }, C.at >= 0 ? `${C.at + 1} of ${n}` : ""),
        h("button", { class: "ai-bar-btn", title: "Next change", onclick: () => step(1) }, "›")) : null,
      h("span", { class: "ai-bar-gap" }),
      h("button", { class: "btn" + (C.showing ? " on" : ""), onclick: () => toggle() }, C.showing ? "Hide changes" : "Show changes"),
      held(S.path)
        ? h("button", { class: "btn btn-gold", title: "Save this page for everyone, as it is now", onclick: (ev) => busy(ev, "Saving…", () => share(S.path)) }, "Save")
        : h("button", { class: "btn", title: "Stop marking these changes", onclick: done }, "Done"),
    ].filter(Boolean));
  }

  // ----------------------------------------------------------------------------- comparing

  /** The page as it was before the AI's changes, as the editor would have it. */
  function baseDoc() {
    if (C.base) return C.base;
    const md = splitFront(C.changes[0]?.before ?? "").body; // the editor holds the page's frontmatter apart, so leave it out here too
    const html = S.editor.storage.markdown.parser.parse(md);
    C.base = createDocument(html, S.editor.schema);
    return C.base;
  }

  /**
   * Neighbouring blocks each edited in place come out of the comparison as one run: split it into one
   * change per block, so each reads (and is undone or accepted) on its own, marked word by word.
   */
  function pairUp(hunks, A, B) {
    const out = [];
    for (const hk of hunks) {
      const n = hk.a[1] - hk.a[0];
      if (n > 1 && n === hk.b[1] - hk.b[0] && A.slice(hk.a[0], hk.a[1]).every((x, i) => x.type === B[hk.b[0] + i].type))
        for (let i = 0; i < n; i++) out.push({ a: [hk.a[0] + i, hk.a[0] + i + 1], b: [hk.b[0] + i, hk.b[0] + i + 1] });
      else out.push(hk);
    }
    return out;
  }

  /** Where each top-level node of a doc starts. */
  const starts = (doc) => { const out = []; doc.forEach((node, offset) => out.push(offset)); return out; };

  /** Positions of a block's text offsets (its textContent), for marking words inside it. */
  function textPositions(node, start) {
    const map = [];
    node.descendants((n, pos) => { if (n.isText) for (let i = 0; i < n.text.length; i++) map.push(start + 1 + pos + i); });
    return map;
  }

  // The change being looked at (stepped to with the bar's arrows): its highlights say "ai-now", the
  // others "ai-later" (faded), and its paragraph gets a mark in the margin (currentMark).
  const curIndex = () => (C.at < 0 ? 0 : C.at);
  const tag = (id) => (id === curIndex() ? " ai-now" : " ai-later");

  function compute() {
    const doc = S.editor.state.doc, base = baseDoc();
    const A = [], B = [];
    base.forEach((n) => A.push(n));
    doc.forEach((n) => B.push(n));
    const at = starts(doc);
    const end = doc.content.size;
    const decos = [], hunks = [];
    for (const hk of pairUp(diffSeq(A, B, (x, y) => x.eq(y)), A, B)) {
      const id = hunks.length;
      const from = hk.b[0] < B.length ? at[hk.b[0]] : end;
      const to = hk.b[1] < B.length ? at[hk.b[1]] : end;
      hunks.push({ from, to, old: A.slice(hk.a[0], hk.a[1]) });
      const removed = A.slice(hk.a[0], hk.a[1]), added = B.slice(hk.b[0], hk.b[1]);
      // One block for one of the same kind: mark the words inside it.
      // (Unless it was mostly rewritten: then the old block, struck through, above the new one reads better.)
      const words = removed.length === 1 && added.length === 1 && removed[0].type === added[0].type && removed[0].textContent !== added[0].textContent
        ? diffWords(removed[0].textContent, added[0].textContent) : null;
      if (words && sameShare(words) >= 0.25) {
        const pos = textPositions(added[0], from);
        let i = 0, marked = 0;
        for (const part of words) {
          if (part.op === "=") { i += part.text.length; continue; }
          if (part.op === "+") {
            const a = pos[i], b = pos[i + part.text.length - 1];
            if (a != null && b != null) { decos.push(Decoration.inline(a, b + 1, { class: "ai-add" + tag(id), "data-hunk": String(id) })); marked++; }
            i += part.text.length;
          } else {
            const p = pos[i] ?? (pos[i - 1] != null ? pos[i - 1] + 1 : from + 1);
            const text = part.text;
            decos.push(Decoration.widget(p, () => h("span", { class: "ai-del" + tag(id), "data-hunk": String(id) }, text), { side: -1, key: `del-${id}-${i}${tag(id)}` }));
            marked++;
          }
        }
        continue;
      }
      if (!words && removed.length === 1 && added.length === 1 && removed[0].type === added[0].type) {
        // Same words, other formatting (bold, a link): mark the block.
        decos.push(Decoration.node(from, to, { class: "ai-add-block" + tag(id), "data-hunk": String(id) }));
        continue;
      }
      // Otherwise whole blocks: what was removed shown struck through, what was added highlighted.
      if (removed.length) {
        decos.push(Decoration.widget(from, () => {
          const box = h("div", { class: "ai-del-block" + tag(id), "data-hunk": String(id), contenteditable: "false" });
          box.append(DOMSerializer.fromSchema(S.editor.schema).serializeFragment(Fragment.from(removed)));
          return box;
        }, { side: -1, key: `delb-${id}${tag(id)}` }));
      }
      let p = from;
      for (const n of added) { decos.push(Decoration.node(p, p + n.nodeSize, { class: "ai-add-block" + tag(id), "data-hunk": String(id) })); p += n.nodeSize; }
    }
    if (hunks.length) decos.push(...currentMark(hunks[Math.min(curIndex(), hunks.length - 1)], doc));
    C.hunks = hunks;
    if (C.at >= hunks.length) C.at = hunks.length - 1;
    return DecorationSet.create(doc, decos);
  }

  // ----------------------------------------------------------------------------- suggestions

  /** The page as the AI would have it (its latest suggestions). */
  function targetDoc() {
    if (C.target) return C.target;
    // Without its frontmatter, as the editor holds a page: left in, Markdown read the block as a rule and a
    // heading ("type: system components: [] groups: [] ... status: draft"), shown as a suggested title.
    const md = splitFront(C.changes[C.changes.length - 1]?.after ?? "").body;
    C.target = createDocument(S.editor.storage.markdown.parser.parse(md), S.editor.schema);
    return C.target;
  }

  /** The page as it is, marked with what each suggestion would change: removals struck, additions green. */
  function computeSuggest() {
    const doc = S.editor.state.doc, target = targetDoc();
    const A = [], B = [];
    doc.forEach((n) => A.push(n));
    target.forEach((n) => B.push(n));
    const at = starts(doc), end = doc.content.size;
    const decos = [], hunks = [];
    const ser = DOMSerializer.fromSchema(S.editor.schema);
    for (const hk of pairUp(diffSeq(A, B, (x, y) => x.eq(y)), A, B)) {
      const removed = A.slice(hk.a[0], hk.a[1]), added = B.slice(hk.b[0], hk.b[1]);
      const key = removed.map((n) => n.textContent).join("\u0001") + "\u0002" + added.map((n) => n.textContent).join("\u0001");
      if (C.skipped.has(key)) continue;
      const id = hunks.length;
      const from = hk.a[0] < A.length ? at[hk.a[0]] : end;
      const to = hk.a[1] < A.length ? at[hk.a[1]] : end;
      hunks.push({ from, to, now: added, key });
      const words = removed.length === 1 && added.length === 1 && removed[0].type === added[0].type && removed[0].textContent !== added[0].textContent
        ? diffWords(removed[0].textContent, added[0].textContent) : null;
      if (words && sameShare(words) >= 0.25) {
        const pos = textPositions(removed[0], from);
        let i = 0;
        for (const part of words) {
          if (part.op === "=") { i += part.text.length; continue; }
          if (part.op === "-") {
            const a = pos[i], b = pos[i + part.text.length - 1];
            if (a != null && b != null) decos.push(Decoration.inline(a, b + 1, { class: "ai-del" + tag(id), "data-hunk": String(id) }));
            i += part.text.length;
          } else {
            const p = pos[i] ?? (pos[i - 1] != null ? pos[i - 1] + 1 : from + 1);
            const text = part.text;
            decos.push(Decoration.widget(p, () => h("span", { class: "ai-add" + tag(id), "data-hunk": String(id) }, text), { side: 1, key: `sug-${id}-${i}${tag(id)}` }));
          }
        }
        continue;
      }
      let p = from;
      for (const n of removed) { decos.push(Decoration.node(p, p + n.nodeSize, { class: "ai-del-live" + tag(id), "data-hunk": String(id) })); p += n.nodeSize; }
      if (added.length) decos.push(Decoration.widget(to, () => {
        const box = h("div", { class: "ai-add-widget" + tag(id), "data-hunk": String(id), contenteditable: "false" });
        box.append(ser.serializeFragment(Fragment.from(added)));
        return box;
      }, { side: -1, key: `sugb-${id}${tag(id)}` }));
    }
    if (hunks.length) decos.push(...currentMark(hunks[Math.min(curIndex(), hunks.length - 1)], doc));
    C.hunks = hunks;
    if (C.at >= hunks.length) C.at = hunks.length - 1;
    return DecorationSet.create(doc, decos);
  }

  /** The change being looked at: a mark in the margin by every block it touches. */
  function currentMark(x, doc) {
    const out = [];
    doc.forEach((n, pos) => {
      const end = pos + n.nodeSize;
      const touches = x.to > x.from ? pos < x.to && end > x.from : pos === x.from || (pos < x.from && end > x.from);
      if (touches) out.push(Decoration.node(pos, end, { class: "ai-current" }));
    });
    return out;
  }

  function accept(i) {
    const x = C.hunks[i];
    if (!x) return;
    C.at = i;
    S.editor.view.dispatch(S.editor.state.tr.replaceWith(x.from, x.to, Fragment.from(x.now)));
    after();
  }
  function skip(i) {
    const x = C.hunks[i];
    if (!x) return;
    C.at = i;
    C.skipped.add(x.key);
    redraw();
    after();
  }
  function acceptAll() {
    // From the bottom up, so earlier positions stay true.
    let tr = S.editor.state.tr;
    for (const x of [...C.hunks].reverse()) tr = tr.replaceWith(x.from, x.to, Fragment.from(x.now));
    S.editor.view.dispatch(tr);
    const f = frontSuggestion(); if (f) setFront(f.front); C.frontDone = true; // the header too
    after();
  }
  /** After Accept or Skip: on to the next one; with none left, the suggestions are done. */
  function after() {
    redraw();
    if (!C.hunks.length && !frontSuggestion()) { toast("All suggestions handled"); done(); return; }
    if (!C.hunks.length) return; // only the header's suggestion left: it's in the bar
    scrollToPos(C.hunks[Math.min(C.at < 0 ? 0 : C.at, C.hunks.length - 1)].from + 1);
  }

  function redraw() {
    if (!S.editor) return;
    const on = C.showing && C.changes.length && C.page === S.path;
    const set = on ? (C.suggest ? computeSuggest() : compute()) : DecorationSet.empty;
    S.editor.view.dispatch(S.editor.state.tr.setMeta(KEY, set).setMeta("addToHistory", false));
    drawBar();
  }

  function step(d) {
    if (!C.hunks.length) return;
    C.at = C.at < 0 ? (d > 0 ? 0 : C.hunks.length - 1) : (C.at + d + C.hunks.length) % C.hunks.length;
    redraw(); // the highlights follow: this one stands out, the rest fade
    scrollToPos(C.hunks[C.at].from + 1);
  }

  function toggle(on = !C.showing) {
    C.showing = on;
    C.at = -1;
    redraw();
    if (on && C.hunks.length) step(1);
  }

  // ----------------------------------------------------------------------------- undo, done

  /** Put one change's old blocks back, as an ordinary edit (saved, and undoable with Ctrl+Z). */
  function undoHunk(i) {
    const hk = C.hunks[i];
    undoBtn.classList.add("hidden");
    if (!hk) return;
    const tr = S.editor.state.tr.replaceWith(hk.from, hk.to, Fragment.from(hk.old));
    S.editor.view.dispatch(tr);
    toast("Undone");
  }

  async function done() {
    await api("/api/ai/reviewed", { method: "POST", body: JSON.stringify({ page: S.path }) });
    C.changes = []; C.base = null; C.target = null; C.showing = false; C.suggest = false; C.frontDone = false;
    redraw();
  }

  // Pointing at a change: a small Undo beside it.
  $("#page").addEventListener("mousemove", (ev) => {
    if (!C.showing || C.suggest) return;
    const el = ev.target.closest?.("[data-hunk]");
    if (!el) return;
    const r = el.getBoundingClientRect();
    undoBtn.dataset.hunk = el.dataset.hunk;
    undoBtn.style.left = Math.min(window.innerWidth - 70, r.right + 6) + "px";
    undoBtn.style.top = r.top - 2 + "px";
    undoBtn.classList.remove("hidden");
  });
  $("#scroller").addEventListener("scroll", () => undoBtn.classList.add("hidden"));
  $("#page").addEventListener("mouseleave", (ev) => { if (ev.relatedTarget !== undoBtn) undoBtn.classList.add("hidden"); });

  // ----------------------------------------------------------------------------- loading

  /** The open page's unreviewed changes, from the engine. `show`: turn the marks on. */
  async function load({ show = false } = {}) {
    const page = S.path;
    const r = await api("/api/ai/changes?page=" + encodeURIComponent(page || ""));
    if (page !== S.path) return;
    let all = r.changes || [];
    C.other = false;
    // Changes made outside the Codex, waiting to be shared: compared with how the page was last shared.
    const hp = held(page);
    if (!all.length && hp?.by === "other") {
      const b = await api("/api/sync/base?page=" + encodeURIComponent(page));
      if (page !== S.path) return;
      all = [{ before: b.text || "", suggest: false }];
      C.other = true;
    }
    // Suggestions waiting come first, and show straight away; otherwise the AI's edits.
    const sugg = all.filter((x) => x.suggest);
    C.suggest = sugg.length > 0;
    C.page = page; C.changes = C.suggest ? sugg : all.filter((x) => !x.suggest); C.base = null; C.target = null; C.skipped = new Set(); C.frontDone = false;
    if (!C.changes.length) C.showing = false;
    else if (show || C.suggest) C.showing = true;
    C.at = -1;
    redraw();
    if ((show || C.suggest) && C.hunks.length) step(1);
  }

  return {
    init() { $("#main .toolbar").after(bar); },
    load,
    /** The editor changed: keep the marks true to it. */
    docChanged() { if (C.showing && C.changes.length) { clearTimeout(C.t); C.t = setTimeout(redraw, 120); } },
    /** The page was opened: its changes, if any (shown at once when the AI tab asked). */
    pageOpened(show = false) { C.showing = false; C.changes = []; C.base = null; C.target = null; C.suggest = false; drawBar(); return load({ show }); },
    show: () => load({ show: true }),
    /** What's held changed (shared, undone): the bar's Share or Done follows. */
    heldChanged: () => drawBar(),
    escape() { if (!C.showing || C.suggest) return false; toggle(false); return true; },
  };
}
