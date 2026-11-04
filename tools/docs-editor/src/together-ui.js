// Who's here, in the page (PLAN.md, "Together"; the engine's side is src/together.js):
//
//   - Small avatars of everyone else with a Codex open, tucked at the top-right corner of your own
//     avatar. Hovering them fans them out; hovering one says where they are, clicking it goes there.
//   - Right of the page's path: the avatars of those on this page.
//   - Typing in a section someone else is editing too: a quiet note says so (nothing is locked).
//   - In a comment thread: "Kari is typing…".
//
// This page tells the engine where you are: the page, the section (where you're typing, else what's at
// the top of the screen), whether you're editing, which comment you're typing in, and whether you're
// away (nothing touched for 5 minutes). "Show me as online" (the layout menu) hides you from the others.

const EDITING_MS = 20 * 1000;  // typed in the page this recently: editing
const TYPING_MS = 8 * 1000;    // typed in a comment this recently: typing
const AWAY_MS = 5 * 60 * 1000; // nothing touched for this long: away
const KEY = "codex.online";

export function makeTogetherUi(ctx) {
  const { S, $, h, api } = ctx;
  const T = { people: [], connected: false, me: null, lastTyped: 0, typingIn: null, lastTypingAt: 0, lastActive: Date.now(), sent: "", dismissed: new Set() };
  const visible = () => localStorage.getItem(KEY) !== "off";

  // ----------------------------------------------------------------------------- where I am

  /** The headings above a position in the page: ["Combat", "Bile"]. */
  function sectionAt(pos) {
    if (!S.editor || pos == null) return [];
    const path = [];
    S.editor.state.doc.descendants((node, p) => {
      if (p > pos) return false;
      if (node.type.name !== "heading") return;
      const lvl = node.attrs.level;
      path.length = Math.min(path.length, lvl - 1);
      path[lvl - 1] = node.textContent;
      return false;
    });
    return path.filter((x) => x != null);
  }

  const editing = () => Date.now() - T.lastTyped < EDITING_MS && !S.view;
  function myState() {
    const onPage = S.path && !S.view;
    const pos = editing() ? S.editor?.state.selection.from : ctx.currentHeadingPos();
    return {
      page: onPage ? S.path : null, title: onPage ? ctx.pageTitle() : null,
      section: onPage ? sectionAt(pos) : [],
      editing: onPage && editing(),
      typingIn: Date.now() - T.lastTypingAt < TYPING_MS ? T.typingIn : null,
      away: Date.now() - T.lastActive > AWAY_MS,
      hidden: !visible(),
    };
  }
  let sendTimer = null;
  function report() {
    clearTimeout(sendTimer);
    sendTimer = setTimeout(() => {
      const s = myState();
      const json = JSON.stringify(s);
      if (json === T.sent) return;
      T.sent = json;
      api("/api/here", { method: "POST", body: json });
      drawNote();
    }, 250);
  }

  // Typing in the page, in a comment, or doing anything at all.
  function typed() { T.lastTyped = Date.now(); T.lastActive = Date.now(); report(); }
  document.addEventListener("input", (ev) => {
    const ta = ev.target.closest?.("textarea.cm-input");
    if (!ta) return;
    const k = ta.dataset.key || "";
    T.typingIn = k === "draft" ? "new" : k.startsWith("reply-") ? k.slice(6) : null;
    T.lastTypingAt = Date.now();
    report();
  }, true);
  for (const ev of ["keydown", "mousedown", "wheel"]) document.addEventListener(ev, () => { const was = Date.now() - T.lastActive > AWAY_MS; T.lastActive = Date.now(); if (was) report(); }, { passive: true, capture: true });
  $("#scroller")?.addEventListener("scroll", () => report(), { passive: true });
  // Editing and typing end by themselves; away starts by itself.
  setInterval(report, 10 * 1000);

  // ----------------------------------------------------------------------------- others

  const others = () => {
    const seen = new Set(), out = [];
    for (const p of T.people) {
      if (T.me && p.id === T.me.id) continue; // yourself, in another window
      if (seen.has(p.id)) continue;
      seen.add(p.id); out.push(p);
    }
    return out;
  };

  /** Where someone is, in words. */
  function where(p) {
    if (!p.page) return p.away ? "Away" : "In the Codex";
    const t = p.title || p.page.split("/").pop().replace(/\.md$/, "");
    const sec = p.section?.length > 1 ? p.section[p.section.length - 1] : null;
    const what = p.away ? "Away, on" : p.editing ? "Editing" : "Reading";
    return sec ? `${what} “${sec}” in ${t}` : `${what} ${t}`;
  }
  function go(p) {
    if (!p.page) return;
    const leaf = p.section?.length > 1 ? p.section[p.section.length - 1] : null;
    ctx.goTo(p.page, leaf);
  }

  // A small card under an avatar, while hovering it.
  let card = null;
  function showCard(el, p) {
    hideCard();
    card = h("div", { class: "here-card" }, h("b", {}, p.name), h("div", {}, where(p)), p.typingIn ? h("div", { class: "here-card-sub" }, "Writing a comment") : null,
      p.page ? h("div", { class: "here-card-sub" }, "Click to go there") : null);
    document.body.append(card);
    const r = el.getBoundingClientRect();
    card.style.top = r.bottom + 8 + "px";
    card.style.left = Math.max(8, Math.min(window.innerWidth - card.offsetWidth - 8, r.left + r.width / 2 - card.offsetWidth / 2)) + "px";
  }
  function hideCard() { card?.remove(); card = null; }
  let hovered = null; // whose card is up: the avatars are redrawn as the room changes, and it follows them

  const avatar = (p, cls) => h("img", {
    class: "here-av " + cls + (p.editing ? " editing" : "") + (p.away ? " away" : ""), src: p.avatar || "/logo.png", alt: p.name, "data-who": p.id,
    onmouseenter: (ev) => { hovered = p.id; showCard(ev.currentTarget, p); }, onmouseleave: () => { hovered = null; hideCard(); },
    onclick: (ev) => { ev.stopPropagation(); hideCard(); go(p); },
  });

  function draw() {
    const all = others();
    // By your avatar: everyone else (newest last, so the first stays put).
    const stack = $("#here");
    if (stack) {
      const shown = all.slice(0, 8);
      stack.replaceChildren(...shown.map((p) => avatar(p, "")), ...(all.length > 8 ? [h("span", { class: "here-more", title: all.slice(8).map((p) => p.name).join(", ") }, "+" + (all.length - 8))] : []));
      stack.classList.toggle("hidden", !all.length || !T.connected);
      stack.title = "";
    }
    // Beside the page's path: those on this page.
    const crumb = $("#crumbHere");
    if (crumb) {
      const here = S.path && !S.view ? all.filter((p) => p.page === S.path) : [];
      crumb.replaceChildren(...here.map((p) => avatar(p, "on-page")));
      crumb.classList.toggle("hidden", !here.length);
    }
    drawNote();
    drawTyping();
    // Still hovering someone: their card moves to their new avatar, or goes with them.
    if (hovered) {
      const p = all.find((x) => x.id === hovered);
      const el = p && [...document.querySelectorAll(".here-av[data-who]")].find((x) => x.dataset.who === hovered && x.matches(":hover"));
      if (el) showCard(el, p); else { hovered = null; hideCard(); }
    }
  }

  // ----------------------------------------------------------------------------- "editing this section too"

  function drawNote() {
    const bar = $("#togetherBar");
    if (!bar) return;
    const me = myState();
    const key = (p) => p.id + "|" + p.page + "|" + (p.section || []).join(" › ");
    const clash = me.editing ? others().filter((p) => p.editing && p.page === me.page && (p.section || []).join("\u0001") === me.section.join("\u0001") && !T.dismissed.has(key(p))) : [];
    if (!clash.length) { bar.classList.add("hidden"); return; }
    const names = clash.map((p) => p.name);
    const sec = me.section.length > 1 ? `“${me.section[me.section.length - 1]}”` : "this page";
    bar.replaceChildren(
      h("img", { class: "here-av on-page editing", src: clash[0].avatar || "/logo.png", alt: "" }),
      h("span", { class: "ai-bar-text" }, `${names.join(" and ")} ${names.length > 1 ? "are" : "is"} editing ${sec} too. Both of your changes are kept if you write in different places; if you change the same lines, you'll be asked which version to keep.`),
      h("button", { class: "ai-link", onclick: () => { for (const p of clash) T.dismissed.add(key(p)); bar.classList.add("hidden"); } }, "OK"));
    bar.classList.remove("hidden");
  }

  // ----------------------------------------------------------------------------- "Kari is typing…" in a thread

  /** Who's typing in a comment thread (for the comment cards, src/notes.js). */
  function typingIn(id) {
    const names = others().filter((p) => p.typingIn === id).map((p) => p.name);
    return names.length ? `${names.join(" and ")} ${names.length > 1 ? "are" : "is"} typing…` : null;
  }
  function drawTyping() {
    for (const el of document.querySelectorAll(".cm-others-typing")) {
      const t = typingIn(el.dataset.for);
      el.textContent = t || "";
      el.classList.toggle("hidden", !t);
    }
  }

  // ----------------------------------------------------------------------------- wiring

  return {
    async init() {
      const v = await api("/api/here").catch(() => null);
      if (v) this.onEvent(v);
      report();
    },
    /** From the engine: who's here now. */
    onEvent(v) { T.people = v.people || []; T.connected = !!v.connected; draw(); },
    /** Signed in as (src/account-button.js): never shown to yourself. */
    setMe(user) { T.me = user || null; draw(); },
    typed,
    pageChanged() { T.dismissed.clear(); report(); draw(); },
    typingIn,
    drawTyping,
    online: { on: visible, toggle: () => { localStorage.setItem(KEY, visible() ? "off" : "on"); report(); } },
  };
}
