// How the Codex is laid out: whether the Pages pane (left) and the Outline and links pane (right)
// sit beside the page, and whether comments sit in the margin. Chosen in the menu on the logo, kept
// in localStorage, and the same at any window size: a pane you've chosen to show stays docked while
// there's room, and when the window is too narrow for it (or you've hidden it) it opens over the page
// as a sheet instead, from its button in the title bar or Ctrl+, (Pages) / Ctrl+. (Outline).
// A docked pane is resized by dragging the gap between it and the page (double-click: back to its
// default width); its width is kept too.

const KEY = "codex.layout";
// Pane widths: default, least and most. The right-hand pane is as wide as talking to the AI needs,
// whichever tab is open.
const WIDTH = { pages: { def: 240, min: 200, max: 340 }, outline: { def: 360, min: 280, max: 480 } };
// Room for docked panes: the page keeps at least this much beside them (less with only Pages docked,
// where the page is the only other thing); dragging a pane wider stops at PAGE_LEAST.
const PAGE_KEEPS = 700, PAGE_KEEPS_PAGES_ONLY = 520, PAGE_LEAST = 480;
const GRIP_WAKE_MS = 350; // the pointer rests on a pane's edge this long before it can be dragged

const ICON = {
  pages: '<svg viewBox="0 0 24 24"><rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M9 4.5v15M5.8 8.5h1.4M5.8 11.5h1.4"/></svg>',
  outline: '<svg viewBox="0 0 24 24"><rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M15 4.5v15M17 8.5h1.4M17 11.5h1.4"/></svg>',
  caret: '<svg viewBox="0 0 24 24"><path d="m7 10 5 5 5-5"/></svg>',
};

export function makeLayout(ctx) {
  const { $, h, notes, help, presence, online } = ctx;
  let saved = {};
  try { saved = JSON.parse(localStorage.getItem(KEY) || "{}"); } catch {}
  const clampW = (pane, v) => Math.round(Math.min(WIDTH[pane].max, Math.max(WIDTH[pane].min, Number(v) || WIDTH[pane].def)));
  const L = { pages: saved.pages !== false, outline: saved.outline !== false, sheet: null, docked: { pages: false, outline: false },
    width: { pages: clampW("pages", saved.widths?.pages), outline: clampW("outline", saved.widths?.outline) } };

  const store = () => localStorage.setItem(KEY, JSON.stringify({ pages: L.pages, outline: L.outline, widths: L.width }));
  const room = (pane, pagesDocked = L.docked.pages) => {
    const w = window.innerWidth;
    return pane === "pages" ? w >= L.width.pages + PAGE_KEEPS_PAGES_ONLY
      : w >= L.width.outline + (pagesDocked ? L.width.pages : 0) + PAGE_KEEPS;
  };
  const setWidths = () => {
    const s = document.documentElement.style;
    s.setProperty("--pages-w", L.width.pages + "px");
    s.setProperty("--outline-w", L.width.outline + "px");
  };

  /** Dock what's chosen and fits; everything else is reachable as a sheet. */
  function apply() {
    L.docked.pages = L.pages && room("pages", false);
    L.docked.outline = L.outline && room("outline");
    if (L.sheet && L.docked[L.sheet]) L.sheet = null;
    const b = document.body.classList;
    b.toggle("pages-docked", L.docked.pages);
    b.toggle("outline-docked", L.docked.outline);
    b.toggle("sheet-pages", L.sheet === "pages");
    b.toggle("sheet-outline", L.sheet === "outline");
    $("#sheetBackdrop").classList.toggle("hidden", !L.sheet);
    for (const pane of ["pages", "outline"]) {
      const btn = $("#" + pane + "Btn"), shown = L.docked[pane] || L.sheet === pane;
      btn.classList.toggle("on", shown);
      btn.title = (shown ? "Hide " : "Show ") + (pane === "pages" ? "the pages" : "the outline and links") + (pane === "pages" ? " (Ctrl+,)" : " (Ctrl+.)");
    }
    setWidths();
    // The page's width changed: put the comments level with their text again.
    requestAnimationFrame(() => { placeGrips(); notes.layout(); });
  }

  // ----------------------------------------------------------------------------- resizing

  // A grip over the gap between each docked pane and the page: drag to resize, double-click to reset.
  const grips = {};
  function grip(pane) {
    const el = h("div", { class: "pane-grip hidden" });
    // Quiet until the pointer rests on it a moment: then the line, the resize cursor and the tooltip.
    let wake = null;
    el.addEventListener("pointerenter", () => { clearTimeout(wake); wake = setTimeout(() => { el.classList.add("ready"); el.title = "Drag to resize, double-click for the usual width"; }, GRIP_WAKE_MS); });
    el.addEventListener("pointerleave", () => { clearTimeout(wake); if (!el.classList.contains("on")) { el.classList.remove("ready"); el.title = ""; } });
    el.addEventListener("pointerdown", (ev) => {
      if (ev.button !== 0 || !el.classList.contains("ready")) return;
      ev.preventDefault();
      el.setPointerCapture(ev.pointerId);
      const r = paneEl(pane).getBoundingClientRect();
      document.body.classList.add("panes-resizing");
      el.classList.add("on");
      const move = (e) => {
        const want = pane === "pages" ? e.clientX - r.left : r.right - e.clientX;
        // Never so wide the page drops under its least width.
        const other = pane === "pages" ? (L.docked.outline ? L.width.outline : 0) : (L.docked.pages ? L.width.pages : 0);
        const most = Math.max(WIDTH[pane].min, window.innerWidth - other - PAGE_LEAST - 32);
        L.width[pane] = Math.min(most, clampW(pane, want));
        setWidths(); placeGrips();
      };
      const up = () => {
        el.removeEventListener("pointermove", move);
        el.removeEventListener("pointerup", up);
        el.removeEventListener("pointercancel", up);
        document.body.classList.remove("panes-resizing");
        el.classList.remove("on");
        if (!el.matches(":hover")) el.classList.remove("ready");
        store();
        notes.layout();
      };
      el.addEventListener("pointermove", move);
      el.addEventListener("pointerup", up);
      el.addEventListener("pointercancel", up);
    });
    el.addEventListener("dblclick", () => { if (!el.classList.contains("ready")) return; L.width[pane] = WIDTH[pane].def; store(); apply(); });
    document.body.append(el);
    return el;
  }
  const paneEl = (pane) => (pane === "pages" ? $("#sidebar") : $("#outlineWin"));
  function placeGrips() {
    for (const pane of ["pages", "outline"]) {
      const el = grips[pane];
      if (!el) continue;
      el.classList.toggle("hidden", !L.docked[pane]);
      if (!L.docked[pane]) continue;
      const r = paneEl(pane).getBoundingClientRect();
      el.style.top = r.top + "px";
      el.style.height = r.height + "px";
      el.style.left = (pane === "pages" ? r.right : r.left - 8) + "px"; // over the 8 px gap beside the page
    }
  }

  /** The pane's button (or key): hide it if docked, dock it if it fits, else open or close its sheet. */
  function toggle(pane) {
    if (L.docked[pane]) { L[pane] = false; store(); }
    else if (!L[pane] && room(pane)) { L[pane] = true; store(); }
    else L.sheet = L.sheet === pane ? null : pane;
    apply();
  }

  /** From the menu: whether the pane is kept beside the page. Too narrow for it: show it as a sheet. */
  function choose(pane, on) {
    L[pane] = on; store();
    L.sheet = on && !room(pane) ? pane : null;
    apply();
  }

  /** Make sure the pane is on screen: docked if it fits (choosing it if it was off), else as a sheet. */
  function show(pane) {
    if (L.docked[pane] || L.sheet === pane) return;
    if (!L[pane] && room(pane)) { L[pane] = true; store(); }
    else L.sheet = pane;
    apply();
  }

  function closeSheet() { if (!L.sheet) return false; L.sheet = null; apply(); return true; }

  // ----------------------------------------------------------------------------- the logo's menu

  function menu() {
    if (closeMenu()) return;
    const narrow = (pane) => L[pane] && !room(pane);
    // A classic title-bar menu: one line an item, a tick column, the keys on the right; what an item
    // does is its tooltip, not a second line.
    const item = (on, label, hint, keys, fn) => h("button", { class: "cm-menu-item lm-item", title: hint || null, onclick: (ev) => { ev.stopPropagation(); closeMenu(); fn(); } },
      h("span", { class: "lm-check" }, on ? "✓" : ""), h("span", { class: "lm-label" }, label), h("span", { class: "lm-keys" }, keys || ""));
    const head = (text) => h("div", { class: "lm-head" }, text);
    const sep = () => h("div", { class: "lm-sep" });
    const tooNarrow = "Too narrow to keep beside the page: opens over it";
    const m = h("div", { class: "cm-menu layout-menu" },
      head("Panes"),
      item(L.pages, "Pages", narrow("pages") ? tooNarrow : "The list of pages, on the left", "Ctrl+,", () => choose("pages", !L.pages)),
      item(L.outline, "Outline and links", narrow("outline") ? tooNarrow : "This page's headings and links, on the right", "Ctrl+.", () => choose("outline", !L.outline)),
      item(!notes.state.hidden, "Comments beside the page", "Off: folded to small markers", null, () => notes.toggleMargin()),
      sep(),
      item(false, "Show everything", "Pages, outline and comments", null, () => { L.pages = L.outline = true; store(); if (notes.state.hidden) notes.toggleMargin(); L.sheet = null; apply(); }),
      item(false, "Just the page", "Hide both side panes", null, () => { L.pages = L.outline = false; store(); L.sheet = null; apply(); }),
      online || presence ? [sep(), head("Presence")] : null,
      online ? item(online.on(), "Show me as online", "Others in the Codex see where you are", null, () => online.toggle()) : null,
      presence ? item(presence.on(), "Show what I'm editing on Discord", "“Editing …” on your Discord profile", null, () => presence.toggle()) : null,
      sep(),
      item(false, "Keys and help", null, "?", help));
    document.body.append(m);
    const r = $("#brandBtn").getBoundingClientRect();
    m.style.left = r.left + "px";
    m.style.top = r.bottom + 4 + "px";
    $("#brandBtn").classList.add("on");
    setTimeout(() => document.addEventListener("mousedown", outside, true), 0);
  }
  const outside = (ev) => { if (!ev.target.closest(".layout-menu, #brandBtn")) closeMenu(); };
  function closeMenu() {
    const m = document.querySelector(".layout-menu");
    if (!m) return false;
    m.remove();
    $("#brandBtn").classList.remove("on");
    document.removeEventListener("mousedown", outside, true);
    return true;
  }

  function init() {
    $("#brandBtn").addEventListener("click", menu);
    $("#pagesBtn").addEventListener("click", () => toggle("pages"));
    $("#outlineBtn").addEventListener("click", () => toggle("outline"));
    $("#pagesBtn").innerHTML = ICON.pages;
    $("#outlineBtn").innerHTML = ICON.outline;
    $("#brandBtn").insertAdjacentHTML("beforeend", `<span class="brand-caret">${ICON.caret}</span>`);
    $("#sheetBackdrop").addEventListener("click", closeSheet);
    // Opening a page, a heading or a link from a sheet closes it.
    $("#tree").addEventListener("click", (ev) => { if (L.sheet === "pages" && ev.target.closest(".t-row.file")) closeSheet(); });
    $("#outlineWin").addEventListener("click", (ev) => { if (L.sheet === "outline" && ev.target.closest(".o-item, .lk-item, .ai-text code.doc, .ai-text a")) closeSheet(); });
    grips.pages = grip("pages");
    grips.outline = grip("outline");
    let t = null;
    window.addEventListener("resize", () => { placeGrips(); clearTimeout(t); t = setTimeout(apply, 50); });
    apply();
  }

  return {
    init, toggle, show, closeSheet,
    escape: () => closeMenu() || closeSheet(),
    get state() { return L; },
  };
}
