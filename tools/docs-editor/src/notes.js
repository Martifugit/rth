// Comments on the docs, the way Google Docs does them: select text, add a comment, and it sits in
// the margin beside the passage, which is highlighted. Reply, resolve with the tick, more in the ⋮
// menu. Comments are between people; Claude is asked in the AI tab (Ctrl+J), not here (older threads
// with Claude's answers still show them). Mention @agent and it waits for the next agent session. The
// comment history (top right) lists every comment across the docs. Closed comments sit in the margin as
// short cards (two lines and a reply count), or as small markers when the margin is folded ("Hide").
//
// Comments are kept in docs/.notes/ by the server (notes-store.js), never in the page: the highlight
// is a ProseMirror decoration, which is drawn but never written back. anchor.js finds each comment's
// passage again when the page changes.
//
// Stored kinds: "note" (a plain comment), "task" (@agent: left for the next agent session), and "ask"
// (an @Claude question, from before the AI tab). The CLI (notes.js) shows them to agents by those names.

import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { capture, sectionsOf, locate } from "./anchor.js";
import { failed } from "./plain-errors.js";

const KEY = new PluginKey("codexComments");

/** Draws the comments' highlights; the set is replaced by the comments code and mapped through edits. */
export const NoteMarks = Extension.create({
  name: "codexCommentMarks",
  addProseMirrorPlugins() {
    return [new Plugin({
      key: KEY,
      state: {
        init: () => DecorationSet.empty,
        apply: (tr, old) => { const m = tr.getMeta(KEY); return m !== undefined ? m : old.map(tr.mapping, tr.doc); },
      },
      props: { decorations(state) { return KEY.getState(state); } },
    })];
  },
});

const ICON = {
  add: '<svg viewBox="0 0 24 24"><path d="M4 5h16v11H9l-5 4z"/><path d="M12 8v5M9.5 10.5h5"/></svg>',
  history: '<svg viewBox="0 0 24 24"><path d="M4 5h16v11H9l-5 4z"/><path d="M12 8v3l2 1"/></svg>',
  check: '<svg viewBox="0 0 24 24"><path d="m5 12 5 5 9-10"/></svg>',
  ask: '<svg viewBox="0 0 24 24"><path d="M12 3.5 13.9 9l5.6 1.9-5.6 1.9L12 18.5l-1.9-5.7-5.6-1.9L10.1 9z"/><path d="M18.5 3.5v3M17 5h3"/></svg>',
  bubble: '<svg viewBox="0 0 24 24"><path d="M4 5h16v11H9l-5 4z"/></svg>',
  dots: '<svg viewBox="0 0 24 24"><circle cx="12" cy="5" r="1.6" fill="currentColor"/><circle cx="12" cy="12" r="1.6" fill="currentColor"/><circle cx="12" cy="19" r="1.6" fill="currentColor"/></svg>',
};

export function makeNotes(ctx) {
  const { S, $, h, api, toast, ask } = ctx;
  const N = {
    page: undefined,
    notes: [],
    placed: new Map(),   // id -> { how, from, to }
    active: null,        // the comment whose card is open
    runs: new Map(),     // id -> { state, text, activity }
    agent: null,         // { ok, version | error }
    owner: "You",
    drafts: new Map(),   // id -> unsent reply
    draft: null,         // a new comment being written: { anchor, page, from, label }
    loading: null,
    history: { open: false, filter: "open", all: [] },
    hidden: localStorage.getItem("codex.commentsHidden") === "1", // the margin folded to markers
    expanded: new Set(), // "id:i": a long message shown in full
    allReplies: new Set(), // threads showing their earlier replies
  };

  // ----------------------------------------------------------------------------- text <-> positions

  function textMap() {
    const doc = S.editor.state.doc;
    let text = "";
    const segs = [], heads = [];
    let first = true;
    doc.descendants((node, pos) => {
      if (!node.isTextblock) return true;
      if (!first) text += "\n";
      first = false;
      const start = text.length;
      node.forEach((child, offset) => {
        const p = pos + 1 + offset;
        if (child.isText) { segs.push({ t: text.length, p, n: child.text.length }); text += child.text; }
        else { segs.push({ t: text.length, p, n: 1 }); text += child.type.name === "hardBreak" ? "\n" : " "; }
      });
      if (!segs.length || segs[segs.length - 1].t + segs[segs.length - 1].n <= start) segs.push({ t: start, p: pos + 1, n: 0 });
      if (node.type.name === "heading") heads.push({ level: node.attrs.level, text: text.slice(start), start, pos });
      return false;
    });
    return { text, segs, heads, sections: sectionsOf(heads, text.length) };
  }
  function toPos(map, off) {
    let lo = 0, hi = map.segs.length - 1, best = 0;
    while (lo <= hi) { const mid = (lo + hi) >> 1; if (map.segs[mid].t <= off) { best = mid; lo = mid + 1; } else hi = mid - 1; }
    const s = map.segs[best];
    if (!s) return 0;
    if (off <= s.t + s.n) return s.p + Math.max(0, off - s.t);
    const next = map.segs[best + 1];
    return next ? next.p : s.p + s.n;
  }
  function toOff(map, pos) {
    for (const s of map.segs) {
      if (pos < s.p) return s.t;
      if (pos <= s.p + s.n) return s.t + (pos - s.p);
    }
    return map.text.length;
  }

  /** The selection inside the page, as document positions (works in Read too). */
  function pageSelection() {
    const sel = window.getSelection();
    const root = $("#page .ProseMirror");
    if (!sel || sel.isCollapsed || !root || !root.contains(sel.anchorNode) || !root.contains(sel.focusNode)) return null;
    try {
      const a = S.editor.view.posAtDOM(sel.anchorNode, sel.anchorOffset);
      const b = S.editor.view.posAtDOM(sel.focusNode, sel.focusOffset);
      return a === b ? null : { from: Math.min(a, b), to: Math.max(a, b) };
    } catch { return null; }
  }

  /** What a new comment is on: the selection, else the paragraph being edited, else the section in view. */
  /** What a new comment is on: the selected text, or else the whole page. */
  function target({ wholePage = false } = {}) {
    const sel = wholePage ? null : pageSelection();
    if (sel) {
      const map = textMap();
      const a = capture(map.text, toOff(map, sel.from), toOff(map, sel.to), map.sections);
      if (a.quote) return { anchor: a, from: sel.from, to: sel.to };
    }
    return { anchor: { section: [], quote: "", prefix: "", suffix: "" }, from: 0, to: 0, wholePage: true };
  }

  // ----------------------------------------------------------------------------- loading and placing

  function load(page = S.path) {
    const job = (async () => {
      const r = await api("/api/notes?page=" + encodeURIComponent(page || ""));
      if (page !== S.path) return;
      N.page = page;
      N.notes = r.notes || [];
      if (N.draft && N.draft.page !== page) N.draft = null;
      place(true);
      outdated();
    })();
    N.loading = job;
    return job;
  }

  // A server started before comments existed serves this page but not the comments: say so (opening a
  // page clears banners, so this runs again after each one loads).
  function outdated() {
    if (N.outdated) ctx.banner("Codex's server is older than this page, so comments won't work until you restart it: close Codex's window and run codex.cmd again.");
  }

  function place(fromAnchors = false) {
    if (!S.editor || N.page !== S.path) return;
    if (fromAnchors) {
      const map = textMap();
      N.placed = new Map();
      for (const n of N.notes) {
        const r = locate(map.text, map.sections, n.anchor);
        if (r.how === "lost") { N.placed.set(n.id, { how: "lost" }); continue; }
        N.placed.set(n.id, { how: r.how, from: toPos(map, r.start), to: toPos(map, r.end), gone: r.how === "section" && !!n.anchor?.quote });
        // Found by likeness: take up the passage's new wording, so it doesn't drift further.
        if (r.how === "fuzzy" && !S.dirty) {
          n.anchor = capture(map.text, r.start, r.end, map.sections);
          api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ anchor: n.anchor, touch: false }) });
        }
      }
    }
    draw();
    render();
  }

  const visible = () => N.notes.filter((n) => n.status !== "done");

  function draw() {
    if (!S.editor) return;
    const doc = S.editor.state.doc;
    const decos = [];
    const add = (id, from, to, cls) => {
      from = Math.max(0, Math.min(from, doc.content.size)); to = Math.max(from, Math.min(to, doc.content.size));
      if (to > from) decos.push(Decoration.inline(from, to, { class: cls, "data-comment": id }, { id }));
    };
    for (const n of visible()) {
      const p = N.placed.get(n.id);
      if (!p || p.how === "lost" || p.how === "page" || p.how === "section") continue;
      add(n.id, p.from, p.to, "cmt-hl" + (n.id === N.active ? " active" : "") + (n.id === N.hover ? " cm-hover" : ""));
    }
    if (N.draft && N.draft.to > N.draft.from) add("draft", N.draft.from, N.draft.to, "cmt-hl active");
    S.editor.view.dispatch(S.editor.state.tr.setMeta(KEY, DecorationSet.create(doc, decos)).setMeta("addToHistory", false));
  }

  /** Where each comment is now (the highlights follow edits as they happen). */
  function livePlaces() {
    const set = S.editor ? KEY.getState(S.editor.state) : null;
    const out = new Map(N.placed);
    if (set) for (const d of set.find()) if (d.spec.id && d.spec.id !== "draft") out.set(d.spec.id, { ...out.get(d.spec.id), from: d.from, to: d.to });
    return out;
  }

  /** After a save: comments whose passage was reworded take up the new words. */
  function saved() {
    if (!S.editor || N.page !== S.path) return;
    const places = livePlaces();
    const map = textMap();
    for (const n of N.notes) {
      const p = places.get(n.id);
      if (!p || p.how !== "exact" && p.how !== "fuzzy" || !n.anchor?.quote) continue;
      const now = map.text.slice(toOff(map, p.from), toOff(map, p.to));
      if (now && now !== n.anchor.quote) {
        n.anchor = capture(map.text, toOff(map, p.from), toOff(map, p.to), map.sections);
        api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ anchor: n.anchor, touch: false }) });
      }
    }
    N.placed = places;
  }

  // ----------------------------------------------------------------------------- the margin

  const rail = () => $("#commentRail");

  function render() {
    const r = rail();
    if (!r || !S.editor) return;
    // The open comment in full; the rest as short cards, or as markers when the margin is folded.
    const cards = visible().map((n) => (n.id === N.active ? card(n) : N.hidden ? marker(n) : compact(n)));
    if (N.draft) cards.push(draftCard());
    const wrap = $("#docWrap");
    wrap.classList.toggle("has-comments", cards.length > 0 && S.mode !== "source");
    wrap.classList.toggle("comments-slim", N.hidden && !N.draft && !N.active);
    // Keep what's being typed across re-renders.
    const activeEl = document.activeElement;
    const typing = activeEl && r.contains(activeEl) && activeEl.tagName === "TEXTAREA" ? { key: activeEl.dataset.key, s: activeEl.selectionStart, e: activeEl.selectionEnd } : null;
    r.replaceChildren(railToggle(), ...cards);
    N.hover = null;
    clampLong(r);
    if (typing) {
      const el = r.querySelector(`textarea[data-key="${typing.key}"]`);
      if (el) { el.focus(); el.setSelectionRange(typing.s, typing.e); }
    }
    layout();
    ctx.setCommentCount(visible().length);
    updateHistoryBadge();
  }

  /** Put each card level with its passage; the open card exactly, the rest pushed clear of it. */
  function layout() {
    const r = rail();
    if (!r || !S.editor || S.mode === "source") return;
    const base = r.getBoundingClientRect().top;
    const places = livePlaces();
    const items = [...r.children].filter((el) => el.dataset.id).map((el) => {
      const id = el.dataset.id;
      let pos = id === "draft" ? N.draft?.from : places.get(id)?.from;
      const how = id === "draft" ? "exact" : places.get(id)?.how;
      let want = 0;
      if (pos != null && how !== "lost" && how !== "page") {
        try { want = S.editor.view.coordsAtPos(Math.min(Math.max(pos, 1), S.editor.state.doc.content.size)).top - base - 4; } catch {}
      }
      return { el, id, want, h: el.offsetHeight };
    }).sort((a, b) => a.want - b.want);
    const GAP = 6;
    const focus = items.findIndex((x) => x.id === (N.draft ? "draft" : N.active));
    if (focus < 0) {
      let y = 0;
      for (const x of items) { x.top = Math.max(x.want, y); y = x.top + x.h + GAP; }
    } else {
      items[focus].top = Math.max(0, items[focus].want);
      let y = items[focus].top + items[focus].h + GAP;
      for (let i = focus + 1; i < items.length; i++) { items[i].top = Math.max(items[i].want, y); y = items[i].top + items[i].h + GAP; }
      let floor = items[focus].top - GAP;
      for (let i = focus - 1; i >= 0; i--) { items[i].top = Math.min(items[i].want, floor - items[i].h); floor = items[i].top - GAP; }
      // Nothing above the top: if pushed past it, shift everything down.
      const over = items.length ? -Math.min(0, items[0].top) : 0;
      if (over) for (const x of items) x.top += over;
    }
    let bottom = 0;
    for (const x of items) { x.el.style.top = x.top + "px"; bottom = Math.max(bottom, x.top + x.h); }
    r.style.minHeight = bottom + 40 + "px";
    requestAnimationFrame(link);
  }

  /** Thin lines from passages to their cards, as a word processor joins a passage to its balloon: along
   *  under the passage's last line out to the margin, then an elbow with rounded corners into the card. The open comment
   *  (or the one being written) always has one; a card or passage under the pointer gets one too, and
   *  its passage and card are lit, so a card pushed far down by its neighbours is easy to place. */
  function link() {
    const wrap = $("#docWrap");
    if (!wrap) return;
    let svg = wrap.querySelector(".cm-link-line");
    const open = N.draft ? "draft" : N.active;
    const ids = [...new Set([open, N.hover].filter(Boolean))];
    const paths = S.mode === "source" || N.hidden && !open ? [] : ids.map((id) => linePath(wrap, id)).filter(Boolean);
    if (!paths.length) { svg?.remove(); return; }
    if (!svg) {
      svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      svg.setAttribute("class", "cm-link-line");
      wrap.append(svg);
    }
    svg.setAttribute("width", wrap.scrollWidth); svg.setAttribute("height", wrap.scrollHeight);
    svg.innerHTML = paths.map((p) => `<path d="${p.d}"/><circle r="2.5" cx="${p.x1}" cy="${p.y1}"/>`).join("");
  }

  function linePath(wrap, id) {
    const cardEl = rail()?.querySelector(`[data-id="${id}"]`);
    const hls = [...(S.editor?.view.dom.querySelectorAll(`[data-comment="${id}"]`) || [])];
    if (!cardEl || !hls.length) return null;
    const box = wrap.getBoundingClientRect();
    const rects = hls.flatMap((el) => [...el.getClientRects()]).filter((r) => r.width > 0);
    if (!rects.length) return null;
    const last = rects.reduce((a, b) => (b.bottom > a.bottom + 2 || Math.abs(b.bottom - a.bottom) <= 2 && b.right > a.right ? b : a));
    const prose = wrap.querySelector(".prose")?.getBoundingClientRect();
    const card = cardEl.getBoundingClientRect();
    // The underline is the passage's bottom border, which the browser snaps to a whole pixel row (the last
    // of its box): run along the middle of that same row, so the two read as one line.
    const x0 = Math.round(last.right) - box.left, y0 = Math.round(last.bottom) - 0.5 - box.top;
    const xm = Math.max(x0 + 8, (prose ? prose.right : last.right) - box.left - 4);
    const x1 = card.left - box.left, y1 = card.top - box.top + 13;
    if (x1 <= xm + 4) return null;
    // An elbow: along to the middle of the gutter, straight up or down, then along into the card, its two
    // corners rounded (smaller when the card sits nearly level with the passage).
    const xv = Math.round(xm + (x1 - xm) / 2), dy = y1 - y0, dir = Math.sign(dy);
    const r = Math.min(8, Math.abs(dy) / 2, (x1 - xm) / 2);
    if (r < 1) return { d: `M${x0},${y0} H${xm} L${x1},${y1}`, x1, y1 };
    return { d: `M${x0},${y0} H${xv - r} Q${xv},${y0} ${xv},${y0 + dir * r} V${y1 - dir * r} Q${xv},${y1} ${xv + r},${y1} H${x1}`, x1, y1 };
  }

  /** The comment under the pointer (its card or its passage): lit on both sides, with its line. */
  function hover(id) {
    if (N.hover === id) return;
    N.hover = id;
    for (const el of rail()?.querySelectorAll(".cm-hover") || []) el.classList.remove("cm-hover");
    if (id) rail()?.querySelector(`[data-id="${id}"]`)?.classList.add("cm-hover");
    draw(); // the passage is lit as a decoration: ProseMirror undoes classes set on its own DOM
    link();
  }

  // ----------------------------------------------------------------------------- cards

  // Who wrote an entry: the AI (R'th AI, with the logo, whichever AI it was), someone signed in with Discord (their name and avatar, `who`), or, for
  // comments kept in docs/.notes/, the owner (git's name). A draft is by whoever is signed in here.
  const person = (by, who) => {
    if (by === "agent") return { name: "R'th AI", cls: "claude", initial: "R", avatar: "/logo.png" };
    const w = who || (N.me && by === "owner" ? N.me : null);
    if (w) return { name: w.name, cls: "person", initial: (w.name?.[0] || "?").toUpperCase(), avatar: w.avatar };
    return { name: N.owner, cls: "owner", initial: (N.owner[0] || "Y").toUpperCase() };
  };

  /** Who wrote it and when; "inline" puts the name and time on one line beside a small avatar. */
  function head(e, tools, inline = false) {
    const p = person(e.by, e.who);
    return h("div", { class: "cm-head" + (inline ? " inline" : "") },
      p.avatar ? h("img", { class: "cm-avatar img " + (inline ? " small" : ""), src: p.avatar, alt: "" }) : h("span", { class: "cm-avatar " + p.cls + (inline ? " small" : "") }, p.initial),
      h("div", { class: "cm-who" }, h("span", { class: "cm-name" }, p.name),
        // Waiting in the outbox (src/comment-store.js): written here, posted once Discord can be reached.
        e.pending ? h("span", { class: "cm-time cm-pending", title: "Discord can't be reached just now. It's saved on this computer and posted by itself once Discord answers." }, "Not posted yet")
          : h("span", { class: "cm-time", title: e.at }, when(e.at))),
      tools || null);
  }

  /** A message; a long one is cut short (with "Show more") unless it has been opened in full. */
  function body(text, key) {
    const el = h("div", { class: "cm-text" + (key && !N.expanded.has(key) ? " clamp" : ""), "data-key": key || null, html: ctx.renderHtml(text || "") });
    for (const c of el.querySelectorAll("code")) {
      const t = c.textContent.trim().replace(/^\.\//, "").replace(/[:#].*$/, "");
      if (ctx.hasPage(t)) { c.classList.add("doc"); c.title = "Open " + t; c.addEventListener("click", (ev) => { ev.stopPropagation(); ctx.openDoc(t); }); }
    }
    for (const a of el.querySelectorAll("a")) a.addEventListener("click", (ev) => {
      ev.preventDefault(); ev.stopPropagation();
      const href = a.getAttribute("href") || "";
      if (/^[a-z]+:/i.test(href)) window.open(href, "_blank", "noopener"); else ctx.openDoc(href.replace(/^\.\//, "").split("#")[0]);
    });
    // "@RthAI", "@Claude" and "@agent" stand out, as mentions do.
    el.innerHTML = el.innerHTML.replace(/(^|[\s(>])@(RthAI|R['’]th AI|Claude|agent)\b/gi, (m, pre, who) => `${pre}<span class="cm-mention">@${who}</span>`);
    return el;
  }

  /** Cut-short messages that really overflow get a "Show more"; the rest are shown whole. */
  function clampLong(root) {
    for (const el of root.querySelectorAll(".cm-text.clamp")) {
      if (el.scrollHeight <= el.clientHeight + 4) { el.classList.remove("clamp"); continue; }
      el.after(h("button", { class: "cm-link cm-expand", onclick: (ev) => { ev.stopPropagation(); N.expanded.add(el.dataset.key); render(); } }, "Show more"));
    }
  }

  const plain = (text) => { const d = document.createElement("div"); d.innerHTML = ctx.renderHtml(text || ""); return d.textContent.replace(/\s+/g, " ").trim(); };
  const plural = (n, one, many) => `${n} ${n === 1 ? one : many}`;

  /** Where the comment is, when it isn't simply on a passage that's highlighted. */
  function whereLine(n) {
    const p = N.placed.get(n.id);
    if (p?.how === "page") return h("div", { class: "cm-on" }, "On this page");
    if (p?.how === "section" && !p.gone) return h("div", { class: "cm-on" }, "On “" + (n.anchor.section[n.anchor.section.length - 1] || "") + "”");
    if (p?.how === "lost") return h("div", { class: "cm-lost", title: n.anchor?.quote || "" }, "The text this was on was removed: “" + clip(n.anchor?.quote || "", 60) + "”");
    if (p?.gone) return h("div", { class: "cm-lost" }, "The passage was removed; it's kept by its section.");
    return null;
  }

  function tools(n) {
    return h("div", { class: "cm-tools" },
      iconBtn(ICON.check, "Resolve", () => resolve(n)),
      iconBtn(ICON.dots, "More", (ev) => menu(ev.currentTarget, n)));
  }

  const openOnClick = (n) => (ev) => { if (ev.target.closest("button, a, textarea, input, label, code.doc")) return; open(n.id); };

  /** A closed comment: who, two lines of it, and how its thread stands. */
  function compact(n) {
    const first = n.thread[0], replies = n.thread.length - 1, run = N.runs.get(n.id);
    const last = n.thread[n.thread.length - 1];
    const foot = [];
    if (replies) foot.push(h("span", { class: "cm-count" }, plural(replies, "reply", "replies")), h("span", { class: "cm-last" }, "· last from " + person(last.by, last.who).name));
    if (n.status === "answered") foot.push(h("span", { class: "cm-new" }, "New answer"));
    if (run) foot.push(h("span", { class: "cm-busy" }, h("span", { class: "dots" }, h("i"), h("i"), h("i")), "R'th AI is answering"));
    return h("div", { class: "cm-card compact" + (n.status === "answered" ? " new" : ""), "data-id": n.id, onclick: openOnClick(n),
      onmouseenter: () => hover(n.id), onmouseleave: () => hover(null) },
      whereLine(n),
      head(first, tools(n), true),
      h("div", { class: "cm-snippet" }, plain(first.text)),
      n.kind === "task" ? assignedLine(n) : null,
      foot.length ? h("div", { class: "cm-foot" }, foot) : null);
  }

  /** A folded margin's mark: a small bubble level with the passage. */
  function marker(n) {
    const replies = n.thread.length - 1;
    return h("button", {
      class: "cm-marker" + (n.status === "answered" ? " new" : "") + (N.runs.has(n.id) ? " busy" : ""), "data-id": n.id,
      title: clip(plain(n.thread[0].text), 120) + (replies ? `  (${plural(replies, "reply", "replies")})` : ""),
      onclick: (ev) => { ev.stopPropagation(); open(n.id); },
    }, h("span", { class: "cm-marker-ico", html: ICON.bubble }), replies ? h("span", { class: "cm-marker-n" }, String(replies + 1)) : null);
  }

  /** Fold the margin to markers, or open it again. */
  function railToggle() {
    return h("button", {
      class: "cm-rail-toggle", title: N.hidden ? "Show the comments beside the page" : "Fold the comments to markers",
      onclick: (ev) => { ev.stopPropagation(); toggleMargin(); },
    }, N.hidden ? "Show" : "Hide");
  }
  function toggleMargin() {
    N.hidden = !N.hidden;
    localStorage.setItem("codex.commentsHidden", N.hidden ? "1" : "0");
    if (!N.draft) N.active = null;
    draw(); render();
  }

  /** The open comment: the comment, then its replies as one indented thread. Its passage is lit in the
   *  page and joined to it by a line (link()), so it isn't quoted here. */
  function card(n) {
    const p = N.placed.get(n.id);
    const run = N.runs.get(n.id);
    const first = n.thread[0];
    const replies = n.thread.slice(1).map((e, i) => ({ e, i: i + 1 }));
    const fold = replies.length > 3 && !N.allReplies.has(n.id);
    const shown = fold ? replies.slice(-2) : replies;
    const edits = (e) => {
      const files = e.run?.edits?.length ? new Set(e.run.edits.map((d) => d.file)).size : 0;
      return files ? h("button", { class: "cm-link", onclick: (ev) => { ev.stopPropagation(); showChanges(e.run.edits); } }, `See what R'th AI changed (${plural(files, "page", "pages")})`) : null;
    };
    return h("div", { class: "cm-card active", "data-id": n.id, onclick: openOnClick(n) },
      whereLine(n),
      head(first, tools(n)),
      body(first.text, n.id + ":0"),
      edits(first),
      assignedLine(n),
      h("div", { class: "cm-replies" + (replies.length || run ? "" : " empty") },
        fold ? h("button", { class: "cm-link cm-earlier", onclick: (ev) => { ev.stopPropagation(); N.allReplies.add(n.id); render(); } }, `Show ${plural(replies.length - 2, "earlier reply", "earlier replies")}`) : null,
        shown.map(({ e, i }) => h("div", { class: "cm-reply" }, head(e, null, true), body(e.text, n.id + ":" + i), edits(e))),
        run ? h("div", { class: "cm-reply" }, head({ by: "agent", at: new Date().toISOString() }, null, true),
          run.text ? h("div", { class: "cm-text streaming", html: ctx.renderHtml(run.text) }) : null,
          h("div", { class: "cm-typing" }, h("span", { class: "dots" }, h("i"), h("i"), h("i")),
            run.state === "queued" ? "Waiting for another answer to finish" : run.activity || "Thinking",
            h("button", { class: "cm-link", onclick: (ev) => { ev.stopPropagation(); api(`/api/notes/${n.id}/stop`, { method: "POST" }); } }, "Stop"))) : null,
        h("div", { class: "cm-others-typing" + (ctx.othersTyping?.(n.id) ? "" : " hidden"), "data-for": n.id }, ctx.othersTyping?.(n.id) || ""),
        replyBox(n)));
  }

  function assignedLine(n) {
    if (n.kind !== "task") return null;
    const ran = n.thread.some((e) => e.by === "agent" && e.run);
    const toClaude = ran || /@claude\b/i.test(n.thread.map((e) => e.text).join(" "));
    return h("div", { class: "cm-assigned" }, toClaude ? "Assigned to R'th AI" : "Assigned to an agent, for the next session");
  }

  function iconBtn(svg, title, fn) {
    return h("button", { class: "cm-icon", title, html: svg, onclick: (ev) => { ev.stopPropagation(); fn(ev); } });
  }

  // ----------------------------------------------------------------------------- writing

  /** A comment box: text with @-mentions, "Assign to Claude" once Claude is mentioned, two buttons. */
  function editor({ key, placeholder, submitLabel, onSubmit, onCancel, initial = "", allowAssign = true }) {
    const ta = h("textarea", { class: "cm-input", rows: "1", placeholder, "data-key": key, spellcheck: "true" });
    ta.value = initial;
    const assign = h("input", { type: "checkbox" });
    const assignRow = h("label", { class: "cm-assign hidden", title: "R'th AI may edit pages under docs/ to do what this asks" }, assign, "Assign to R'th AI");
    const warn = h("div", { class: "cm-warn hidden" });
    const pop = h("div", { class: "cm-mentions hidden" });
    const submit = h("button", { class: "btn btn-gold btn-small", disabled: true, onclick: (ev) => { ev.stopPropagation(); go(); } }, submitLabel);
    const cancel = h("button", { class: "cm-cancel", onclick: (ev) => { ev.stopPropagation(); onCancel(); } }, "Cancel");
    // @agent only: Claude is asked in the AI tab (PLAN.md, "AI, built in"), so @Claude points there.
    const mentions = () => ({ claude: false, agent: /(^|\s)@agent\b/i.test(ta.value) });
    const askedClaude = () => /(^|\s)@claude\b/i.test(ta.value);
    const refresh = () => {
      ta.style.height = "auto"; ta.style.height = Math.min(220, ta.scrollHeight + 2) + "px";
      const m = mentions();
      assignRow.classList.toggle("hidden", !(allowAssign && m.claude));
      warn.classList.toggle("hidden", !askedClaude());
      warn.textContent = "Tag @RthAI to have R'th AI answer in this thread, or ask it in the AI tab (Ctrl+J).";
      submit.disabled = !ta.value.trim();
      submit.textContent = m.claude && N.agent?.ok ? (assign.checked ? "Assign" : "Ask R'th AI") : m.agent ? "Assign" : submitLabel;
      layout();
    };
    let picks = [], sel = 0;
    const word = () => { const m = ta.value.slice(0, ta.selectionStart).match(/(^|\s)@(\w*)$/); return m ? m[2] : null; };
    const showPop = () => {
      const w = word();
      const all = [["RthAI", "answers in the thread (an open Codex with Claude)"], ["agent", "picks it up in the next agent session"]];
      picks = w === null ? [] : all.filter(([n]) => n.toLowerCase().startsWith(w.toLowerCase()));
      pop.classList.toggle("hidden", !picks.length);
      sel = Math.min(sel, Math.max(0, picks.length - 1));
      pop.replaceChildren(...picks.map(([n, d], i) => h("div", { class: "cm-mention-item" + (i === sel ? " sel" : ""), onmousedown: (ev) => { ev.preventDefault(); pick(i); } },
        h("span", { class: "cm-avatar small " + (n === "RthAI" ? "claude" : "agent") }, n === "RthAI" ? "R" : n[0].toUpperCase()), h("b", {}, n), h("span", {}, d))));
    };
    const pick = (i) => {
      const [n] = picks[i];
      const before = ta.value.slice(0, ta.selectionStart).replace(/@\w*$/, "@" + n + " ");
      ta.value = before + ta.value.slice(ta.selectionStart);
      ta.setSelectionRange(before.length, before.length);
      picks = []; pop.classList.add("hidden");
      refresh();
    };
    const go = () => {
      if (!ta.value.trim()) return;
      const m = mentions();
      onSubmit({ text: ta.value.trim(), claude: m.claude, agent: m.agent, assign: assign.checked && m.claude });
    };
    ta.addEventListener("input", () => { refresh(); showPop(); });
    ta.addEventListener("keydown", (ev) => {
      ev.stopPropagation();
      if (picks.length && (ev.key === "ArrowDown" || ev.key === "ArrowUp")) { ev.preventDefault(); sel = (sel + (ev.key === "ArrowDown" ? 1 : picks.length - 1)) % picks.length; showPop(); return; }
      if (picks.length && (ev.key === "Enter" || ev.key === "Tab")) { ev.preventDefault(); pick(sel); return; }
      if (ev.key === "Escape") { ev.preventDefault(); if (picks.length) { picks = []; pop.classList.add("hidden"); } else onCancel(); return; }
      if (ev.key === "Enter" && (ev.ctrlKey || ev.metaKey)) { ev.preventDefault(); go(); }
    });
    assign.addEventListener("change", refresh);
    setTimeout(refresh, 0);
    return { el: h("div", { class: "cm-editor", onclick: (ev) => ev.stopPropagation() }, h("div", { class: "cm-input-wrap" }, ta, pop), assignRow, warn, h("div", { class: "cm-actions" }, cancel, submit)), focus: () => ta.focus() };
  }

  function draftCard() {
    const ed = editor({
      key: "draft", placeholder: N.draft?.wholePage ? "Comment on this page" : "Comment", submitLabel: "Comment",
      initial: N.draft?.text || "", // a comment that couldn't be posted comes back with its words
      onCancel: () => { N.draft = null; draw(); render(); },
      onSubmit: async ({ text, claude, agent, assign }) => {
        const kind = assign || (agent && !claude) ? "task" : claude ? "ask" : "note";
        const run = claude && N.agent?.ok;
        const d = N.draft;
        if (run && assign && d.page === S.path && !(await ctx.saveNow())) return;
        N.draft = null;
        const r = await api("/api/notes", { method: "POST", body: JSON.stringify({ page: d.page, kind, text, anchor: d.anchor, run }) });
        // Not posted (the helper or Discord down, say): the draft and its words come back, to try again.
        if (!r.ok) { N.draft = { ...d, text }; toast(failed("Couldn't post the comment, so it's kept here to try again", r), "bad"); draw(); render(); return; }
        if (r.runError) toast(r.runError, "bad");
        N.active = r.note.id;
        if (d.page === S.path) { N.notes.push(r.note); place(true); }
        ctx.notesChanged();
      },
    });
    setTimeout(ed.focus, 0);
    return h("div", { class: "cm-card active draft", "data-id": "draft" }, N.draft?.wholePage ? h("div", { class: "cm-on" }, "On this page") : null, head({ by: "owner", at: new Date().toISOString() }, null, true), ed.el);
  }

  function replyBox(n) {
    const running = N.runs.has(n.id);
    const ed = editor({
      key: "reply-" + n.id, placeholder: running ? "R'th AI is answering…" : "Reply", submitLabel: "Reply",
      initial: N.drafts.get(n.id) || "",
      onCancel: () => { N.drafts.delete(n.id); N.active = null; draw(); render(); },
      onSubmit: async ({ text, claude, assign }) => {
        // A reply to Claude carries on the conversation without naming it again.
        const talkingToClaude = false; // Claude is asked in the AI tab now
        const run = talkingToClaude && N.agent?.ok && !running;
        if (run && (assign || n.kind === "task") && N.page === S.path && !(await ctx.saveNow())) return; // Claude edits the saved page
        if (assign && n.kind !== "task") await api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ kind: "task" }) });
        else if (claude && n.kind === "note") await api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ kind: "ask" }) });
        const r = await api(`/api/notes/${n.id}/reply`, { method: "POST", body: JSON.stringify({ text, run }) });
        if (!r.ok) { toast(failed("Couldn't send the reply", r), "bad"); return; }
        if (r.runError) toast(r.runError, "bad");
        N.drafts.delete(n.id);
        await reload();
      },
    });
    ed.el.querySelector("textarea").addEventListener("input", (ev) => N.drafts.set(n.id, ev.target.value));
    return h("div", { class: "cm-reply-box" }, ed.el);
  }

  /** Start a comment on what you're on (Ctrl+M, or the button beside a selection). */
  function compose({ wholePage = false } = {}) {
    if (!S.editor || !S.path || S.mode === "source") return;
    hideAdd();
    const t = target({ wholePage });
    N.draft = { page: S.path, ...t };
    N.active = null;
    draw(); render();
    window.getSelection()?.removeAllRanges();
    requestAnimationFrame(() => rail().querySelector('[data-id="draft"]')?.scrollIntoView({ block: "nearest" }));
  }

  // The "add comment" button beside a selection, in the margin.
  const addBtn = h("button", { class: "cm-add hidden", title: "Add comment (Ctrl+M)", html: ICON.add, onmousedown: (ev) => ev.preventDefault(), onclick: () => compose() });
  // ...and Ask beside it: the AI tab, with that passage as a chip (src/ai.js).
  const askBtn = h("button", { class: "cm-add cm-ask hidden", title: "Ask the AI about this (Ctrl+J)", html: ICON.ask, onmousedown: (ev) => ev.preventDefault(), onclick: () => { ctx.askAi?.(); hideAdd(); } });
  document.body.append(addBtn, askBtn);
  function hideAdd() { addBtn.classList.add("hidden"); askBtn.classList.add("hidden"); }
  function showAdd() {
    const sel = pageSelection();
    if (!sel || N.draft) return hideAdd();
    const range = window.getSelection().getRangeAt(0);
    const r = range.getClientRects()[0] || range.getBoundingClientRect();
    const page = $("#page").getBoundingClientRect();
    addBtn.classList.remove("hidden");
    addBtn.style.left = Math.min(window.innerWidth - 44, page.right - 6) + "px";
    addBtn.style.top = r.top - 4 + "px";
    if (ctx.askAi) {
      askBtn.classList.remove("hidden");
      askBtn.style.left = addBtn.style.left;
      askBtn.style.top = r.top + 34 + "px";
    }
  }

  // ----------------------------------------------------------------------------- doing things

  async function open(id) {
    if (N.active === id) return;
    N.active = id;
    N.draft = null;
    N.allReplies.delete(id);
    draw(); render();
    const n = N.notes.find((x) => x.id === id);
    // Seen: an answer you've opened is no longer "new from Claude".
    if (n && n.status === "answered") { n.status = "open"; api("/api/notes/" + id, { method: "PATCH", body: JSON.stringify({ status: "open", touch: false }) }).then(loadHistory); }
    const p = livePlaces().get(id);
    if (p && p.from != null && p.how !== "lost") ctx.scrollToPos(p.from, true);
  }

  async function focus(id, page) {
    if (page && page !== S.path) { await ctx.openDoc(page); await N.loading; }
    N.active = null;
    await open(id);
    requestAnimationFrame(() => rail().querySelector(`[data-id="${id}"]`)?.scrollIntoView({ block: "center" }));
  }

  async function resolve(n) {
    const r = await api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ status: "done" }) });
    if (!r.ok) return toast(failed("Couldn't resolve it", r), "bad");
    if (N.active === n.id) N.active = null;
    toast("Resolved. It's in the comment history.");
    await reload();
  }

  async function reload() {
    if (S.path) {
      const r = await api("/api/notes?page=" + encodeURIComponent(S.path));
      N.page = S.path; N.notes = r.notes || [];
      place(true);
    }
    ctx.notesChanged();
    loadHistory();
  }

  function menu(anchorEl, n) {
    closeMenu();
    const items = [
      ["Edit", () => editFirst(n)],
      ["Attach to the selected text", () => reattach(n), !pageSelection()],
      ["Move to another page…", () => moveDialog(n)],
      ["Copy comment id", () => { navigator.clipboard.writeText(n.id); toast("Copied " + n.id + " (agents: notes.js show " + n.id + ")"); }],
      ["Delete", () => del(n), false, "danger"],
    ].filter(Boolean);
    const m = h("div", { class: "cm-menu" }, items.map(([label, fn, disabled, cls]) =>
      h("button", { class: "cm-menu-item " + (cls || ""), disabled: disabled || null, title: disabled ? "Select some text in the page first" : null, onclick: (ev) => { ev.stopPropagation(); closeMenu(); fn(); } }, label)));
    document.body.append(m);
    const r = anchorEl.getBoundingClientRect();
    m.style.top = Math.min(r.bottom + 4, window.innerHeight - m.offsetHeight - 8) + "px";
    m.style.left = Math.max(8, r.right - m.offsetWidth) + "px";
    setTimeout(() => document.addEventListener("mousedown", outside, true), 0);
  }
  const outside = (ev) => { if (!ev.target.closest(".cm-menu")) closeMenu(); };
  function closeMenu() { document.querySelector(".cm-menu")?.remove(); document.removeEventListener("mousedown", outside, true); }

  function editFirst(n) {
    const el = rail().querySelector(`[data-id="${n.id}"] .cm-text`);
    if (!el) return;
    const ed = editor({
      key: "edit-" + n.id, placeholder: "", submitLabel: "Save", initial: n.thread[0].text, allowAssign: false,
      onCancel: () => render(),
      onSubmit: async ({ text }) => { await api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ text }) }); await reload(); },
    });
    el.replaceWith(ed.el);
    setTimeout(ed.focus, 0);
    layout();
  }

  async function del(n) {
    const c = await ask("Delete this comment?", `“${ctx.esc(clip(n.thread[0]?.text || "", 140))}”` + (n.thread.length > 1 ? ` and its ${n.thread.length - 1} ${n.thread.length === 2 ? "reply" : "replies"}` : "") + ". Resolving keeps it in the history; deleting doesn't.",
      [{ key: "cancel", label: "Cancel" }, { key: "del", label: "Delete", cls: "btn-danger" }]);
    if (c !== "del") return;
    const r = await api("/api/notes/" + n.id, { method: "DELETE" });
    if (!r.ok) return toast(failed("Couldn't delete the comment", r), "bad");
    if (N.active === n.id) N.active = null;
    await reload();
  }

  async function reattach(n) {
    const t = target();
    await api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ anchor: t.anchor }) });
    window.getSelection()?.removeAllRanges();
    await reload();
  }

  function moveDialog(n) {
    const input = h("input", { class: "field", placeholder: "docs/gdd/…", list: "commentPages", spellcheck: "false", value: N.page || "" });
    const go = async () => {
      const page = input.value.trim();
      if (!ctx.hasPage(page)) { toast("No such page: " + page, "bad"); return; }
      close();
      await api(`/api/notes/${n.id}/move`, { method: "POST", body: JSON.stringify({ page }) });
      N.active = null;
      await reload();
      toast("Moved to " + page);
    };
    input.addEventListener("keydown", (ev) => { if (ev.key === "Enter") go(); });
    const close = ctx.modal("Move the comment", [h("label", {}, "To page"), input, h("datalist", { id: "commentPages" }, S.files.map((f) => h("option", { value: f.path }, f.title || ""))),
      h("div", { class: "modal-actions" }, h("button", { class: "btn", onclick: () => close() }, "Cancel"), h("button", { class: "btn btn-gold", onclick: go }, "Move"))]);
    setTimeout(() => input.select(), 0);
  }

  function showChanges(edits) {
    const byFile = new Map();
    for (const e of edits) { if (!byFile.has(e.file)) byFile.set(e.file, []); byFile.get(e.file).push(e); }
    const close = ctx.modal("What R'th AI changed", h("div", { class: "changes" }, [...byFile].map(([file, list]) => h("div", { class: "ch-file" },
      h("div", { class: "ch-name" }, ctx.hasPage(file) ? h("a", { onclick: () => { close(); ctx.openDoc(file); } }, file) : file),
      list.map((e) => h("div", { class: "ch-edit" },
        e.old != null ? h("pre", { class: "ch-old" }, e.old) : h("div", { class: "ch-new-file" }, "A new page:"),
        h("pre", { class: "ch-new" }, e.new ?? "")))))), { width: "min(900px, 94vw)" });
  }

  // ----------------------------------------------------------------------------- comment history

  async function loadHistory() {
    const r = await api("/api/notes/all");
    N.history.all = r.notes || [];
    updateHistoryBadge();
    if (N.history.open) renderHistory();
  }
  const forYou = (x) => x.note.status !== "done" && (x.note.status === "answered" || (x.note.thread[0]?.by === "agent" && x.note.thread.length === 1));
  function updateHistoryBadge() {
    const n = N.history.all.filter(forYou).length;
    const b = $("#historyCount");
    if (b) { b.textContent = n ? String(n) : ""; b.title = n ? `${n} new from the AI: answers you haven't opened yet, or questions it left you` : ""; }
  }

  function toggleHistory() {
    N.history.open = !N.history.open;
    $("#historyPanel").classList.toggle("hidden", !N.history.open);
    $("#historyBtn").classList.toggle("on", N.history.open);
    if (N.history.open) { renderHistory(); loadHistory(); }
  }

  function renderHistory() {
    const p = $("#historyPanel");
    const f = N.history.filter;
    const list = N.history.all.filter((x) => f === "all" || (f === "open" && x.note.status !== "done") || (f === "resolved" && x.note.status === "done") || (f === "you" && forYou(x)));
    const select = h("select", { class: "field hs-filter", onchange: (ev) => { N.history.filter = ev.target.value; renderHistory(); } },
      [["open", "Open"], ["you", "New from R'th AI"], ["resolved", "Resolved"], ["all", "All comments"]].map(([v, l]) => h("option", { value: v, selected: v === f || null }, l)));
    p.replaceChildren(
      h("div", { class: "hs-head" }, h("span", { class: "win-title" }, "Comments"), select, h("button", { class: "close-x", title: "Close", onclick: toggleHistory }, "✕")),
      h("div", { class: "hs-list" }, list.length ? list.map((x) => {
        const n = x.note, last = n.thread[n.thread.length - 1];
        return h("div", { class: "hs-item" + (forYou(x) ? " you" : ""), onclick: async () => {
          if (n.status === "done") { await api("/api/notes/" + n.id, { method: "PATCH", body: JSON.stringify({ status: "open" }) }); toast("Reopened"); }
          toggleHistory(); await focus(n.id, x.page); if (n.status === "done") reload();
        } },
          h("div", { class: "hs-page" }, ctx.titleOf(x.page) || x.page || "No page", n.status === "done" ? h("span", { class: "hs-resolved" }, "Resolved") : null),
          n.anchor?.quote ? h("div", { class: "hs-quote" }, clip(n.anchor.quote, 90)) : null,
          head(n.thread[0]), h("div", { class: "hs-text" }, clip(n.thread[0].text, 160)),
          n.thread.length > 1 ? h("div", { class: "hs-last" }, `${person(last.by, last.who).name}: ${clip(last.text, 110)}`) : null);
      }) : h("div", { class: "np-empty" }, f === "you" ? "Nothing new from R'th AI. Its answers show here until you open them, and so do questions it leaves you." : "No comments.")));
  }

  // ----------------------------------------------------------------------------- live events

  function onRun(ev) {
    if (ev.state === "done") {
      N.runs.delete(ev.id);
      if (ev.error && ev.error !== "stopped") toast("R'th AI stopped: " + ev.error, "bad");
      return; // the notes event that follows brings the answer
    }
    const first = !N.runs.has(ev.id);
    N.runs.set(ev.id, ev);
    const el = rail()?.querySelector(`[data-id="${ev.id}"]`);
    if (!el || first) { render(); return; }
    // Update just the streaming text, so typing elsewhere isn't disturbed.
    const n = N.notes.find((x) => x.id === ev.id);
    if (n && n.id === N.active) {
      const old = el.querySelector(".cm-typing")?.parentElement, now = card(n).querySelector(".cm-typing")?.parentElement;
      if (old && now) { old.replaceWith(now); layout(); } else render();
    }
  }

  let notesTimer = null;
  function onNotes(ev) {
    clearTimeout(notesTimer);
    notesTimer = setTimeout(() => { if (ev.all || ev.page === S.path) reload(); else { loadHistory(); ctx.notesChanged(); } }, 80);
  }

  let layoutTimer = null;
  function docChanged() { clearTimeout(layoutTimer); layoutTimer = setTimeout(layout, 60); }

  async function init() {
    const r = await api("/api/agent");
    // A server started before comments existed serves this page but not the comments.
    if (!r.ok || !("agent" in r)) { N.outdated = true; outdated(); }
    N.agent = r.agent || null;
    N.owner = r.owner || "You";
    for (const run of r.runs || []) N.runs.set(run.id, run);
    if (!N.agent) setTimeout(async () => { const again = await api("/api/agent"); N.agent = again.agent; }, 4000);
    let t = null;
    document.addEventListener("selectionchange", () => { clearTimeout(t); t = setTimeout(showAdd, 150); });
    $("#scroller").addEventListener("scroll", () => { if (!addBtn.classList.contains("hidden")) showAdd(); }, { passive: true });
    // Clicking highlighted text opens its comment; clicking anywhere else in the page closes it.
    $("#page").addEventListener("click", (ev) => {
      const el = ev.target.closest("[data-comment]");
      if (el && el.dataset.comment !== "draft") open(el.dataset.comment);
      else if (N.active && !N.draft) { N.active = null; draw(); render(); }
    });
    $("#page").addEventListener("mouseover", (ev) => {
      const id = ev.target.closest?.("[data-comment]")?.dataset.comment;
      hover(id && id !== "draft" ? id : null);
    });
    $("#page").addEventListener("mouseleave", () => hover(null));
    $("#historyBtn").addEventListener("click", toggleHistory);
    $("#statusComments").addEventListener("click", toggleMargin);
    $("#statusComments").title = "Show the comments beside the page, or fold them to markers";
    $("#commentBtn").addEventListener("click", () => compose({ wholePage: true }));
    new ResizeObserver(() => layout()).observe($("#page"));
    window.addEventListener("resize", () => layout());
    loadHistory();
  }

  return {
    init, load, place, saved, compose, onRun, onNotes, docChanged, layout, focus, toggleMargin,
    escape: () => { if (N.draft) { N.draft = null; draw(); render(); return true; } if (N.history.open) { toggleHistory(); return true; } if (N.active) { N.active = null; draw(); render(); return true; } return false; },
    target,
    /** Who's signed in with Discord (src/account-button.js), for drafts; null when nobody is. */
    setMe(user) { N.me = user || null; },
    get state() { return N; },
  };
}

// ----------------------------------------------------------------------------- helpers

function clip(s, n) { s = String(s).replace(/\s+/g, " ").trim(); return s.length > n ? s.slice(0, n - 1) + "…" : s; }

function when(iso) {
  const d = new Date(iso), now = new Date();
  const s = (now - d) / 1000;
  if (!isFinite(s)) return "";
  if (s < 60) return "Just now";
  const time = d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hourCycle: "h23" }); // the 24-hour clock
  if (d.toDateString() === now.toDateString()) return "Today " + time;
  const y = new Date(now); y.setDate(now.getDate() - 1);
  if (d.toDateString() === y.toDateString()) return "Yesterday " + time;
  return d.toLocaleDateString("en-GB", { day: "numeric", month: "short" }) + " " + time;
}
