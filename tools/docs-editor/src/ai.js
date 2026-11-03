// The AI tab: Outline · Links · AI (PLAN.md, "AI, built in"). A conversation beside the page.
//
// It knows what you're looking at through chips over its box: the page, the passage you selected, the
// comment thread you have open; click one away to leave it out. Ctrl+J opens it and puts the cursor
// in its box; Ask on a selection does the same with that passage. Enter sends, Shift+Enter is a new
// line. Conversations are kept on this computer (src/assistant.js), with a list of earlier ones.
// This step only talks; edits and Show changes are step 4.2.

import { discordConversation } from "./discord-extras.js";

const ICON = {
  send: '<svg viewBox="0 0 24 24"><path d="M5 12h13M13 6l6 6-6 6"/></svg>',
  stop: '<svg viewBox="0 0 24 24"><rect x="7" y="7" width="10" height="10" rx="1.5"/></svg>',
  new: '<svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>',
  list: '<svg viewBox="0 0 24 24"><path d="M12 7v5l3 2"/><circle cx="12" cy="12" r="8"/></svg>',
  close: '<svg viewBox="0 0 24 24"><path d="m7 7 10 10M17 7 7 17"/></svg>',
  page: '<svg viewBox="0 0 24 24"><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/></svg>',
  quote: '<svg viewBox="0 0 24 24"><path d="M5 7h14M5 12h10M5 17h12"/></svg>',
  thread: '<svg viewBox="0 0 24 24"><path d="M4 5h16v11H9l-5 4z"/></svg>',
};
const KEY = "codex.ai.conversation";

export function makeAi(ctx) {
  const { S, $, h, api, toast } = ctx;
  const A = {
    id: localStorage.getItem(KEY) || null,
    conv: null,          // the open conversation, with its messages
    run: null,           // { text, activity } while it's answering
    provider: null,
    selection: null,     // { quote, section } of the last passage selected in the page
    left: new Set(),     // chips clicked away ("page", "selection", "thread"), until the next send
    listOpen: false,
  };

  // ----------------------------------------------------------------------------- the pane

  const pane = h("div", { id: "aiPane", class: "ai hidden" });
  const head = h("div", { class: "ai-head" });
  const log = h("div", { class: "ai-log" });
  const chips = h("div", { class: "ai-chips" });
  const input = h("textarea", { class: "ai-input", rows: "1", placeholder: "Ask, or talk an idea through…", spellcheck: "true" });
  const sendBtn = h("button", { class: "ai-send", title: "Send (Enter)", html: ICON.send, onclick: () => (A.run ? stop() : send()) });
  const box = h("div", { class: "ai-box" }, chips, h("div", { class: "ai-row" }, input, sendBtn));
  pane.append(head, log, box);

  input.addEventListener("keydown", (ev) => {
    if (ev.key === "Enter" && !ev.shiftKey && !ev.isComposing) { ev.preventDefault(); send(); }
    else if (ev.key === "Escape" && A.listOpen) { ev.stopPropagation(); A.listOpen = false; drawHead(); }
  });
  input.addEventListener("input", () => { grow(); pane.classList.toggle("typing", !!input.value.trim()); });
  // A conversation pasted from Discord: who said what, so "turn this into the decision" works on it.
  input.addEventListener("paste", (ev) => {
    const chat = discordConversation(ev.clipboardData?.getData("text/plain") || "");
    if (!chat) return;
    ev.preventDefault();
    const text = "A conversation from Discord:\n" + chat.map((x) => `${x.name}: ${x.text}`).join("\n") + "\n";
    input.setRangeText(text, input.selectionStart, input.selectionEnd, "end");
    grow();
    pane.classList.add("typing");
  });
  function grow() { input.style.height = "auto"; input.style.height = Math.min(input.scrollHeight, 220) + "px"; }

  // ----------------------------------------------------------------------------- chips

  /** What the next message may read, from what's on screen, minus what was clicked away. */
  function context() {
    const c = {};
    if (S.path && S.view !== "status" && !A.left.has("page")) c.page = S.path;
    if (A.selection && A.selection.page === S.path && !A.left.has("selection")) c.selection = { quote: A.selection.quote, section: A.selection.section };
    const thread = ctx.notes.state.active;
    if (thread && thread !== "draft" && !A.left.has("thread")) c.thread = thread;
    return c;
  }

  function drawChips() {
    const c = context(), out = [];
    const chip = (kind, icon, label, title) => h("span", { class: "ai-chip", title },
      h("span", { class: "ai-chip-ico", html: icon }), h("span", { class: "ai-chip-label" }, label),
      h("button", { class: "ai-chip-x", title: "Leave this out", html: ICON.close, onclick: () => { A.left.add(kind); drawChips(); input.focus(); } }));
    if (c.page) out.push(chip("page", ICON.page, ctx.titleOf(c.page) || c.page.split("/").pop(), c.page));
    if (c.selection) out.push(chip("selection", ICON.quote, "“" + clip(c.selection.quote, 38) + "”", c.selection.quote));
    if (c.thread) {
      const n = ctx.notes.state.notes.find((x) => x.id === c.thread);
      out.push(chip("thread", ICON.thread, "Comment: " + clip(n?.thread?.[0]?.text || "thread", 28), "The comment thread that's open"));
    }
    chips.replaceChildren(...out);
    chips.classList.toggle("hidden", !out.length);
  }

  // The passage last selected in the page stays a chip while you type in the box.
  document.addEventListener("selectionchange", () => {
    const sel = window.getSelection();
    const root = $("#page .ProseMirror");
    if (!sel || !root || !root.contains(sel.anchorNode)) return;
    if (sel.isCollapsed) { if (A.selection) { A.selection = null; A.left.delete("selection"); drawChips(); } return; }
    const t = ctx.notes.target();
    if (t.wholePage) return;
    A.selection = { page: S.path, quote: t.anchor.quote, section: t.anchor.section };
    A.left.delete("selection");
    drawChips();
  });

  // ----------------------------------------------------------------------------- the conversation

  function drawHead() {
    const title = A.conv?.title || "New conversation";
    head.replaceChildren(
      h("button", { class: "ai-title" + (A.listOpen ? " on" : ""), title: "Earlier conversations", onclick: toggleList },
        h("span", { class: "ai-title-ico", html: ICON.list }), h("span", { class: "ai-title-text" }, title)),
      h("button", { class: "ai-new", title: "New conversation", html: ICON.new, onclick: () => { newConversation(); input.focus(); } }));
    if (A.listOpen) head.append(listEl());
  }

  let listCache = [];
  async function toggleList() {
    A.listOpen = !A.listOpen;
    if (A.listOpen) { const r = await api("/api/ai"); listCache = r.conversations || []; A.provider = r.provider || A.provider; }
    drawHead();
  }
  function listEl() {
    const items = listCache.map((c) => h("div", { class: "ai-li" + (c.id === A.id ? " current" : ""), onclick: () => { A.listOpen = false; openConversation(c.id); } },
      h("span", { class: "ai-li-title" }, c.title),
      h("span", { class: "ai-li-meta" }, ago(c.updated) + (c.page ? " · " + (ctx.titleOf(c.page) || c.page.split("/").pop()) : "")),
      h("button", { class: "ai-li-x", title: "Delete this conversation", html: ICON.close, onclick: async (ev) => {
        ev.stopPropagation();
        await api("/api/ai/delete", { method: "POST", body: JSON.stringify({ id: c.id }) });
        listCache = listCache.filter((x) => x.id !== c.id);
        if (c.id === A.id) newConversation(); else drawHead();
      } })));
    return h("div", { class: "ai-list" }, items.length ? items : h("div", { class: "ai-li-none" }, "No earlier conversations."));
  }

  function newConversation() {
    A.id = null; A.conv = null; A.run = null; A.listOpen = false;
    localStorage.removeItem(KEY);
    drawHead(); drawLog(); syncSend();
  }

  async function openConversation(id) {
    const r = await api("/api/ai/conversation?id=" + encodeURIComponent(id));
    if (!r.ok) { if (id === A.id) newConversation(); return; }
    A.id = id; A.conv = r;
    A.run = r.run?.state === "running" ? { text: r.run.text, activity: r.run.activity } : null;
    localStorage.setItem(KEY, id);
    drawHead(); drawLog(); syncSend();
  }

  function drawLog() {
    const msgs = A.conv?.messages || [];
    const out = [];
    if (!msgs.length && !A.run) out.push(emptyState());
    for (const m of msgs) out.push(message(m));
    if (A.pending) out.push(message({ by: "you", text: A.pending.text, context: A.pending.context }));
    if (A.run) out.push(h("div", { class: "ai-msg ai-msg-ai" },
      A.run.text ? body(A.run.text, true) : null,
      h("div", { class: "ai-activity" }, h("span", { class: "ai-dots" }), A.run.activity || "Thinking")));
    const atEnd = log.scrollHeight - log.scrollTop - log.clientHeight < 40;
    log.replaceChildren(...out);
    if (atEnd || !A.run) log.scrollTop = log.scrollHeight;
  }

  function emptyState() {
    const p = A.provider;
    return h("div", { class: "ai-empty" },
      h("div", { class: "ai-empty-title" }, "Talk it through"),
      h("p", {}, "Ask about the docs, or talk an idea through. It reads what the chips below say: the page, the passage you selected, the comment you have open. Ask it to change something and it will."),
      p && !p.ok && p.needsSignIn !== "openrouter" ? h("p", { class: "ai-warn" }, p.error) : null,
      p && p.needsSignIn === "openrouter" ? h("div", { class: "ai-signin" },
        h("p", {}, "Claude isn't on this computer. OpenRouter gives you free AI models, on your own daily allowance (about 50 messages a day)."),
        h("button", { class: "btn btn-gold", onclick: signInOpenRouter }, "Sign in with OpenRouter"),
        h("p", { class: "ai-note" }, "Not now? Everything else in the Codex works without it.")) : null,
      p?.ok ? h("div", { class: "ai-starts" }, starters().map((t) => h("button", { class: "ai-start", onclick: () => ask(t) }, t))) : null,
      p?.ok && p.name === "OpenRouter" ? h("p", { class: "ai-note ai-provider" }, "Using OpenRouter's free models. ",
        h("button", { class: "ai-link", onclick: async () => { await api("/api/openrouter/signout", { method: "POST" }); } }, "Sign out of OpenRouter")) : null);
  }

  async function signInOpenRouter() {
    const r = await api("/api/openrouter/signin", { method: "POST" });
    if (!r.url) return toast("Couldn't start signing in", "bad");
    window.open(r.url, "_blank");
    toast("Finish signing in in your browser");
  }
  async function refreshProvider() {
    const r = await api("/api/ai");
    A.provider = r.provider || null;
    if (!A.conv?.messages?.length && !A.run) drawLog();
  }

  /** Two or three things worth asking where you are (as Gemini's panel offers); they go once you type. */
  function starters() {
    const c = context(), out = [];
    if (c.thread) out.push("Draft the decision from this thread");
    if (c.page && S.editor) {
      let words = 0, heads = 0;
      S.editor.state.doc.descendants((n) => { if (n.type.name === "heading") heads++; else if (n.isText) words += n.text.split(/\s+/).filter(Boolean).length; });
      if (heads >= 3 && words < heads * 12) out.push("Draft this page from what the docs already say");
      out.push("Review this page against writing.md and the pages it links to");
      if (!c.thread) out.push("What does this page leave open?");
    }
    if (c.selection) out.unshift("Is the selected passage clear and consistent with the rest of the docs?");
    if (!out.length) out.push("What's decided so far about combat?");
    return out.slice(0, 3);
  }

  /** Ask something straight away (a starting prompt, or Ask AI from the search box). */
  function ask(text) {
    focus();
    input.value = text;
    grow();
    send();
  }

  function message(m) {
    if (m.by === "you") {
      const c = m.context || {};
      const bits = [];
      if (c.selection?.quote) bits.push("“" + clip(c.selection.quote, 60) + "”");
      if (c.thread) bits.push("a comment");
      return h("div", { class: "ai-msg ai-msg-you" },
        bits.length ? h("div", { class: "ai-msg-ctx" }, "On " + bits.join(" and ")) : null,
        h("div", { class: "ai-msg-text" }, m.text));
    }
    return h("div", { class: "ai-msg ai-msg-ai" },
      m.text ? body(m.text) : null,
      m.changes?.length ? h("div", { class: "ai-changes" }, m.changes.map((x) => changeLine(x, m))) : null,
      m.error && m.error !== "Stopped." ? h("div", { class: "ai-warn" }, "It stopped: " + m.error) : null,
      m.error === "Stopped." ? h("div", { class: "ai-note" }, "Stopped.") : null);
  }

  /** A page the AI changed in a reply: what, with Show (the page, its changes marked) and Undo. */
  function changeLine(x, m) {
    const title = ctx.titleOf(x.page) || x.page.split("/").pop();
    const n = `${x.places || 1} change${x.places === 1 ? "" : "s"}`;
    const idx = A.conv.messages.indexOf(m);
    if (x.suggest) return h("div", { class: "ai-change" + (x.reviewed ? " reviewed" : "") },
      h("span", { class: "ai-change-text" }, `Suggested ${n} to `, h("b", {}, title)),
      x.reviewed ? null : h("button", { class: "ai-link", onclick: () => ctx.showChanges(x.page) }, "Review"),
      x.reviewed ? null : h("button", { class: "ai-link", onclick: async () => { await api("/api/ai/reviewed", { method: "POST", body: JSON.stringify({ conv: A.id, msg: idx, page: x.page }) }); openConversation(A.id); } }, "Dismiss"));
    const what = x.before === null ? "Made" : x.after === null ? "Deleted" : `Changed ${x.places || 1} place${x.places === 1 ? "" : "s"} in`;
    return h("div", { class: "ai-change" + (x.reviewed ? " reviewed" : "") },
      h("span", { class: "ai-change-text" }, what + " ", h("b", {}, title)),
      x.after !== null ? h("button", { class: "ai-link", onclick: () => ctx.showChanges(x.page) }, "Show") : null,
      h("button", { class: "ai-link", onclick: () => undoTurn(idx, x.page) }, "Undo"));
  }

  async function undoTurn(msg, page) {
    const r = await api("/api/ai/undo", { method: "POST", body: JSON.stringify({ conv: A.id, msg, page }) });
    if (!r.ok) return toast(r.error || "Couldn't undo", "bad");
    if (r.open?.length) {
      toast("That page was edited since: undo its changes one by one where they're marked", "bad");
      ctx.showChanges(r.open[0]);
    } else toast("Undone");
    openConversation(A.id);
  }

  /** An answer as Markdown, with repository paths that are pages opening them. */
  function body(text, streaming = false) {
    const el = h("div", { class: "cm-text ai-text" + (streaming ? " streaming" : ""), html: ctx.renderHtml(text) });
    for (const c of el.querySelectorAll("code")) {
      const t = c.textContent.trim().replace(/^\.\//, "").replace(/[:#].*$/, "");
      if (ctx.hasPage(t)) { c.classList.add("doc"); c.title = "Open " + t; c.addEventListener("click", () => ctx.openDoc(t)); }
    }
    for (const a of el.querySelectorAll("a")) a.addEventListener("click", (ev) => {
      ev.preventDefault();
      const href = a.getAttribute("href") || "";
      if (/^[a-z]+:/i.test(href)) window.open(href, "_blank", "noopener"); else ctx.openDoc(href.replace(/^\.\//, "").split("#")[0]);
    });
    return el;
  }

  function syncSend() {
    sendBtn.innerHTML = A.run ? ICON.stop : ICON.send;
    sendBtn.title = A.run ? "Stop" : "Send (Enter)";
    sendBtn.classList.toggle("stop", !!A.run);
  }

  // ----------------------------------------------------------------------------- sending

  async function send() {
    const text = input.value.trim();
    if (!text || A.run || A.sending) return;
    await ctx.saveNow(); // it reads the page as saved
    const context = ctxSnapshot();
    A.sending = true;
    A.pending = { text, context: { selection: context.selection, thread: context.thread } };
    A.run = { text: "", activity: "Reading" };
    input.value = ""; grow(); pane.classList.remove("typing");
    drawLog(); syncSend();
    const r = await api("/api/ai/send", { method: "POST", body: JSON.stringify({ id: A.id, text, context }) });
    A.sending = false;
    if (!r.ok) {
      A.pending = null; A.run = null;
      input.value = text; grow();
      toast(r.error || "Couldn't send", "bad");
      if (r.needsSignIn) refreshProvider();
      drawLog(); syncSend();
      return;
    }
    A.left.clear();
    if (r.id !== A.id) { A.id = r.id; localStorage.setItem(KEY, r.id); }
    A.pending = null;
    await openConversation(r.id);
    if (!A.run) A.run = { text: "", activity: "Reading" };
    drawLog(); syncSend(); drawChips();
  }
  const ctxSnapshot = () => context();

  async function stop() {
    if (A.id) await api("/api/ai/stop", { method: "POST", body: JSON.stringify({ id: A.id }) });
  }

  /** From the server: the answer streaming in, or finished. */
  function onEvent(ev) {
    if (ev.state === "provider") return refreshProvider(); // signed in or out of OpenRouter
    if (ev.id !== A.id) return;
    if (ev.state === "running") {
      A.run = { text: ev.text, activity: ev.activity };
      drawLog(); syncSend();
    } else if (ev.state === "done") {
      A.run = null;
      openConversation(ev.id);
    }
  }

  // ----------------------------------------------------------------------------- opening it

  /** Ctrl+J, or Ask on a selection: show the tab and put the cursor in the box. */
  function focus({ selection = false } = {}) {
    if (selection) {
      const t = ctx.notes.target();
      if (!t.wholePage) { A.selection = { page: S.path, quote: t.anchor.quote, section: t.anchor.section }; A.left.delete("selection"); }
    }
    ctx.show();
    drawChips();
    requestAnimationFrame(() => input.focus());
  }

  async function init(host) {
    host.append(pane);
    // Opening or closing a comment changes the thread chip.
    document.addEventListener("click", () => { if (!pane.classList.contains("hidden")) requestAnimationFrame(drawChips); });
    drawHead(); drawLog(); drawChips();
    const r = await api("/api/ai");
    A.provider = r.provider || null;
    if (A.id) await openConversation(A.id); else drawLog();
  }

  return {
    init, focus, onEvent, ask,
    shown: () => { drawChips(); if (!A.conv && !A.run) drawLog(); },
    pageChanged: () => { if (A.selection && A.selection.page !== S.path) A.selection = null; A.left.delete("page"); drawChips(); if (!A.conv?.messages?.length && !A.run) drawLog(); },
    notesChanged: () => drawChips(),
    get state() { return A; },
  };
}

const clip = (s, n) => { s = String(s || "").replace(/\s+/g, " ").trim(); return s.length > n ? s.slice(0, n - 1).trimEnd() + "…" : s; };

function ago(iso) {
  const s = (Date.now() - Date.parse(iso)) / 1000;
  if (s < 60) return "just now";
  if (s < 3600) return Math.floor(s / 60) + " min ago";
  if (s < 86400) return Math.floor(s / 3600) + " h ago";
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, { day: "numeric", month: "short" });
}
