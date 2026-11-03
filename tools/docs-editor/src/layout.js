// How the Codex is laid out: whether the Pages pane (left) and the Outline and links pane (right)
// sit beside the page, and whether comments sit in the margin. Chosen in the menu on the logo, kept
// in localStorage, and the same at any window size: a pane you've chosen to show stays docked while
// there's room, and when the window is too narrow for it (or you've hidden it) it opens over the page
// as a sheet instead, from its button in the title bar or Ctrl+, (Pages) / Ctrl+. (Outline).

const KEY = "codex.layout";
// Room for a docked pane: the page keeps at least about 500 px beside them.
const PAGES_ROOM = 800, OUTLINE_ROOM_WITH_PAGES = 1250, OUTLINE_ROOM_ALONE = 980;

const ICON = {
  pages: '<svg viewBox="0 0 24 24"><rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M9 4.5v15M5.8 8.5h1.4M5.8 11.5h1.4"/></svg>',
  outline: '<svg viewBox="0 0 24 24"><rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M15 4.5v15M17 8.5h1.4M17 11.5h1.4"/></svg>',
  caret: '<svg viewBox="0 0 24 24"><path d="m7 10 5 5 5-5"/></svg>',
};

export function makeLayout(ctx) {
  const { $, h, notes, help, presence, online } = ctx;
  let saved = {};
  try { saved = JSON.parse(localStorage.getItem(KEY) || "{}"); } catch {}
  const L = { pages: saved.pages !== false, outline: saved.outline !== false, sheet: null, docked: { pages: false, outline: false } };

  const store = () => localStorage.setItem(KEY, JSON.stringify({ pages: L.pages, outline: L.outline }));
  const room = (pane, pagesDocked = L.docked.pages) => {
    const w = window.innerWidth;
    return pane === "pages" ? w >= PAGES_ROOM : w >= (pagesDocked ? OUTLINE_ROOM_WITH_PAGES : OUTLINE_ROOM_ALONE);
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
    // The page's width changed: put the comments level with their text again.
    requestAnimationFrame(() => notes.layout());
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
    const check = (on) => h("span", { class: "lm-check" }, on ? "✓" : "");
    const item = (on, label, hint, keys, fn) => h("button", { class: "cm-menu-item lm-item", onclick: (ev) => { ev.stopPropagation(); closeMenu(); fn(); } },
      check(on), h("span", { class: "lm-label" }, label, hint ? h("span", { class: "lm-hint" }, hint) : null), keys ? h("kbd", {}, keys) : null);
    const m = h("div", { class: "cm-menu layout-menu" },
      h("div", { class: "lm-head" }, "Layout"),
      item(L.pages, "Pages", narrow("pages") ? "Too narrow to keep beside the page: opens over it" : "The list of pages, on the left", "Ctrl ,", () => choose("pages", !L.pages)),
      item(L.outline, "Outline and links", narrow("outline") ? "Too narrow to keep beside the page: opens over it" : "This page's headings and links, on the right", "Ctrl .", () => choose("outline", !L.outline)),
      item(!notes.state.hidden, "Comments beside the page", "Off: folded to small markers", null, () => notes.toggleMargin()),
      online ? item(online.on(), "Show me as online", "Others in the Codex see where you are", null, () => online.toggle()) : null,
      presence ? item(presence.on(), "Show what I'm editing on Discord", "“Editing …” on your Discord profile", null, () => presence.toggle()) : null,
      h("div", { class: "spell-sep" }),
      item(false, "Show everything", null, null, () => { L.pages = L.outline = true; store(); if (notes.state.hidden) notes.toggleMargin(); L.sheet = null; apply(); }),
      item(false, "Just the page", null, null, () => { L.pages = L.outline = false; store(); L.sheet = null; apply(); }),
      h("div", { class: "spell-sep" }),
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
    let t = null;
    window.addEventListener("resize", () => { clearTimeout(t); t = setTimeout(apply, 50); });
    apply();
  }

  return {
    init, toggle, show, closeSheet,
    escape: () => closeMenu() || closeSheet(),
    get state() { return L; },
  };
}
