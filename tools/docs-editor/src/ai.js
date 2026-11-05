// The AI tab: Outline · Links · AI (PLAN.md, "AI, built in"). A conversation beside the page.
//
// It knows what you're looking at through chips over its box: the page, the passage you selected, the
// comment thread you have open; click one away to leave it out. Ctrl+J opens it and puts the cursor
// in its box; Ask on a selection does the same with that passage. Enter sends, Shift+Enter is a new
// line. Conversations are kept on this computer (src/assistant.js), with a list of earlier ones.
// This step only talks; edits and Show changes are step 4.2.

import { discordConversation } from "./discord-extras.js";
import { failed } from "./plain-errors.js";
import { answerStream } from "./ai-stream.js";
import { busy } from "./busy.js";
import { split as splitFront } from "./frontmatter.js";
import { typeAt } from "./gdd.js";

const ICON = {
  send: '<svg viewBox="0 0 24 24"><path d="M5 12h13M13 6l6 6-6 6"/></svg>',
  stop: '<svg viewBox="0 0 24 24"><rect x="7" y="7" width="10" height="10" rx="1.5"/></svg>',
  new: '<svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>',
  // Two speech bubbles: the conversations (the selector's list of them).
  list: '<svg viewBox="0 0 24 24"><path d="M16.5 8V5.5a2 2 0 0 0-2-2h-9a2 2 0 0 0-2 2V11a2 2 0 0 0 2 2H6v2.5L8.5 13"/><path d="M10.5 10h8a2 2 0 0 1 2 2v4.5a2 2 0 0 1-2 2H18V21l-2.5-2.5h-5a2 2 0 0 1-2-2V12a2 2 0 0 1 2-2z"/></svg>',
  cog: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M12 2.5v2.6M12 18.9v2.6M4.6 4.6l1.8 1.8M17.6 17.6l1.8 1.8M2.5 12h2.6M18.9 12h2.6M4.6 19.4l1.8-1.8M17.6 6.4l1.8-1.8"/><circle cx="12" cy="12" r="6.5"/></svg>',
  check: '<svg viewBox="0 0 24 24"><path d="m5 12.5 4.5 4.5L19 7.5"/></svg>',
  close: '<svg viewBox="0 0 24 24"><path d="m7 7 10 10M17 7 7 17"/></svg>',
  page: '<svg viewBox="0 0 24 24"><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/></svg>',
  quote: '<svg viewBox="0 0 24 24"><path d="M5 7h14M5 12h10M5 17h12"/></svg>',
  thread: '<svg viewBox="0 0 24 24"><path d="M4 5h16v11H9l-5 4z"/></svg>',
  copy: '<svg viewBox="0 0 24 24"><rect x="8.5" y="8.5" width="11" height="11" rx="2"/><path d="M15.5 8.5V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v7.5a2 2 0 0 0 2 2h2.5"/></svg>',
  reply: '<svg viewBox="0 0 24 24"><path d="M9.5 7 4.5 12l5 5"/><path d="M5 12h9a5.5 5.5 0 0 1 5.5 5.5V19"/></svg>',
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
    extra: [],           // pages added (with @ or + Add page) to a conversation not started yet
    mode: "chat",        // Chat (its changes wait as suggestions) or Work (it edits): the line under the box
    defaultMode: "chat",
    listOpen: false,
  };

  // ----------------------------------------------------------------------------- the pane

  const pane = h("div", { id: "aiPane", class: "ai hidden" });
  const head = h("div", { class: "ai-head" });
  const log = h("div", { class: "ai-log" });
  const chips = h("div", { class: "ai-chips" });
  const input = h("textarea", { class: "ai-input", rows: "1", placeholder: "Ask anything…",spellcheck: "true" });
  const sendBtn = h("button", { class: "ai-send", title: "Send (Enter)", html: ICON.send, onclick: () => (A.run ? stop() : send()) });
  const mention = h("div", { class: "ai-mention hidden" });
  // The mode, under the box on the left (as Claude Code's): click it, or Shift+Tab in the box, to switch.
  const modeLine = h("button", { class: "ai-mode", onclick: () => { switchMode(); input.focus(); } });
  // Quotes from its answers (select, then the reply button): cards above the box, each quoting the part in
  // the look of a sent quote, with its own line for what to ask about it (src/ai.js, quoteCard).
  const quotes = h("div", { class: "ai-quotes hidden" });
  // One frame for the message: its quotes, the box, then the mode and a small send button on one line.
  const compose = h("div", { class: "ai-compose", onmousedown: (ev) => { if (ev.target === compose) { ev.preventDefault(); input.focus(); } } },
    quotes, input, h("div", { class: "ai-foot" }, modeLine, sendBtn));
  const box = h("div", { class: "ai-box" }, mention, chips, compose);
  pane.append(head, log, box);

  input.addEventListener("keydown", (ev) => {
    if (mentionKey(ev)) return;
    if (ev.key === "Tab" && ev.shiftKey && !ev.ctrlKey && !ev.altKey) { ev.preventDefault(); switchMode(); return; }
    if (ev.key === "Enter" && !ev.shiftKey && !ev.isComposing) { ev.preventDefault(); send(); }
    else if (ev.key === "Escape" && A.listOpen) { ev.stopPropagation(); A.listOpen = false; drawHead(); }
  });
  input.addEventListener("input", () => { grow(); pane.classList.toggle("typing", !!input.value.trim()); drawMention(); mentionsEdited(); });
  input.addEventListener("blur", () => setTimeout(() => mention.classList.add("hidden"), 150));
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
  function grow() {
    input.style.height = "auto";
    input.style.height = Math.min(input.scrollHeight, 220) + "px";
    input.style.overflowY = input.scrollHeight > 220 ? "auto" : "hidden"; // a scrollbar only once it's full
  }

  // ----------------------------------------------------------------------------- Chat and Work

  const MODES = {
    chat: { label: "Chat", note: "its changes wait for you" },
    work: { label: "Work", note: "it edits the pages" },
  };
  function drawMode() {
    const m = MODES[A.mode] || MODES.chat;
    modeLine.className = "ai-mode ai-mode-" + A.mode;
    modeLine.title = `${m.label}: ${m.note}. Click, or Shift+Tab in the box, to switch.`;
    modeLine.replaceChildren(h("span", { class: "ai-mode-name" }, m.label), h("span", { class: "ai-mode-note" }, " (Shift+Tab to cycle)"));
  }
  /** Chat and Work. Kept with the conversation when the next message goes (src/assistant.js). */
  function switchMode() {
    A.mode = A.mode === "work" ? "chat" : "work";
    drawMode();
  }

  // ----------------------------------------------------------------------------- chips

  // Context belongs to the conversation (src/assistant.js, pinned): it starts with what's on screen and is
  // kept, whatever is opened while waiting for an answer; more is added on purpose (@ or + Add). Before a
  // conversation starts, the chips are what's on screen now, as they will be pinned.
  const started = () => !!A.conv?.context && !!A.id;

  /** What's on screen, minus what was clicked away: the context a new conversation starts with. */
  function context() {
    const c = {};
    if (S.path && !S.view && !A.left.has("page")) c.page = S.path;
    if (A.selection && A.selection.page === S.path && !A.left.has("selection")) c.selection = { quote: A.selection.quote, section: A.selection.section, page: S.path };
    const thread = ctx.notes.state.active;
    if (thread && thread !== "draft" && !A.left.has("thread")) c.thread = thread;
    if (A.extra.length) c.pages = A.extra.filter((p) => p !== c.page);
    return c;
  }

  /** Change what the open conversation keeps (or, before it starts, what it will start with). */
  async function changeContext({ add = null, drop = null }) {
    if (!started()) {
      if (add?.page) { if (!A.extra.includes(add.page)) A.extra.push(add.page); A.left.delete("page"); }
      if (drop) { if (drop.startsWith?.("docs/")) A.extra = A.extra.filter((p) => p !== drop); else A.left.add(drop); }
      drawChips(); return;
    }
    const r = await api("/api/ai/context", { method: "POST", body: JSON.stringify({ id: A.id, add: add || {}, drop: drop ? [drop] : [] }) });
    if (!r.ok) return toast(failed("Couldn't change what the conversation is about", r), "bad");
    A.conv.context = r.context;
    drawChips();
  }

  const pageTitle = (p) => ctx.titleOf(p) || p.split("/").pop().replace(/\.md$/, "");
  function drawChips() {
    const out = [];
    const chip = (icon, label, title, onX) => h("span", { class: "ai-chip", title },
      h("span", { class: "ai-chip-ico", html: icon }), h("span", { class: "ai-chip-label" }, label),
      h("button", { class: "ai-chip-x", title: "Leave this out", html: ICON.close, onclick: () => { onX(); input.focus(); } }));
    // A faint offer: what's on screen that the conversation doesn't have yet. Nothing is added without a click.
    const offer = (icon, label, title, onAdd) => h("button", { class: "ai-chip ai-chip-offer", title, onclick: () => { onAdd(); input.focus(); } },
      h("span", { class: "ai-chip-plus" }, "+"), h("span", { class: "ai-chip-ico", html: icon }), h("span", { class: "ai-chip-label" }, label));
    const threadLabel = (id) => "Comment: " + clip(ctx.notes.state.notes.find((x) => x.id === id)?.thread?.[0]?.text || "thread", 28);
    if (!started()) {
      const c = context();
      if (c.page) out.push(chip(ICON.page, pageTitle(c.page), c.page, () => changeContext({ drop: "page" })));
      for (const p of c.pages || []) out.push(chip(ICON.page, pageTitle(p), p, () => changeContext({ drop: p })));
      if (c.selection) out.push(chip(ICON.quote, "“" + clip(c.selection.quote, 38) + "”", c.selection.quote, () => changeContext({ drop: "selection" })));
      if (c.thread) out.push(chip(ICON.thread, threadLabel(c.thread), "The comment thread that's open", () => changeContext({ drop: "thread" })));
    } else {
      const k = A.conv.context;
      if (k.page) out.push(chip(ICON.page, pageTitle(k.page), k.page + " (what this conversation is about)", () => changeContext({ drop: "page" })));
      for (const p of k.pages || []) out.push(chip(ICON.page, pageTitle(p), p, () => changeContext({ drop: p })));
      if (k.selection) out.push(chip(ICON.quote, "“" + clip(k.selection.quote, 38) + "”", k.selection.quote, () => changeContext({ drop: "selection" })));
      if (k.thread) out.push(chip(ICON.thread, threadLabel(k.thread), "The comment thread it's about", () => changeContext({ drop: "thread" })));
      const has = new Set([k.page, ...(k.pages || [])]);
      if (S.path && !S.view && !has.has(S.path)) out.push(offer(ICON.page, pageTitle(S.path), "Add the page you're on to this conversation", () => changeContext({ add: { page: S.path } })));
      if (A.selection && A.selection.page === S.path && A.selection.quote !== k.selection?.quote)
        out.push(offer(ICON.quote, "“" + clip(A.selection.quote, 30) + "”", "Point the conversation at the passage you've selected", () => changeContext({ add: { selection: { ...A.selection } } })));
      const open = ctx.notes.state.active;
      if (open && open !== "draft" && open !== k.thread) out.push(offer(ICON.thread, threadLabel(open), "Add the open comment thread to this conversation", () => changeContext({ add: { thread: open } })));
    }
    chips.replaceChildren(...out);
    chips.classList.toggle("hidden", !out.length);
  }

  // ----------------------------------------------------------------------------- @ a page

  // Typing @ offers pages by title (as the link picker does); picking one adds it to the conversation
  // and leaves "@Its title" in the message, so the request reads naturally.
  let picks = [], at = 0;
  const mentionAt = () => { const m = input.value.slice(0, input.selectionStart).match(/(^|\s)@([^@\n]{0,40})$/); return m ? m[2] : null; };
  function drawMention() {
    const q = mentionAt();
    if (q === null) { mention.classList.add("hidden"); picks = []; return; }
    const norm = (s) => String(s || "").toLowerCase();
    const all = (S.files || []).filter((f) => f.path.startsWith("docs/gdd/") && !f.path.includes("/templates/"));
    picks = all.filter((f) => !q || norm(f.title || f.path).includes(norm(q).trim())).slice(0, 8);
    at = Math.min(at, Math.max(0, picks.length - 1));
    mention.replaceChildren(...(picks.length ? picks.map((f, i) => h("div", { class: "ai-mention-item" + (i === at ? " on" : ""), onmousedown: (e) => { e.preventDefault(); pickMention(i); } },
      h("span", {}, f.title || pageTitle(f.path)), h("span", { class: "ai-mention-where" }, f.path.slice("docs/gdd/".length).split("/").slice(0, -1).join("/")))) : [h("div", { class: "ai-mention-none" }, "No page by that name")]));
    mention.classList.remove("hidden");
  }
  function pickMention(i) {
    const f = picks[i];
    if (!f) return;
    const before = input.value.slice(0, input.selectionStart).replace(/@[^@\n]{0,40}$/, "@" + (f.title || pageTitle(f.path)) + " ");
    input.value = before + input.value.slice(input.selectionStart);
    input.setSelectionRange(before.length, before.length);
    mention.classList.add("hidden"); picks = [];
    const token = "@" + (f.title || pageTitle(f.path));
    // Remember what this mention brought in, so deleting it takes back that and nothing more: a page
    // that was in the context already stays.
    if (!inContext(f.path)) mentions.push({ token, page: f.path, drop: started() && !A.conv.context.page ? "page" : f.path, wasLeft: A.left.has("page") });
    changeContext({ add: { page: f.path } });
    grow();
  }

  // The pages @mentions added while writing this message: { token, page, drop, wasLeft }.
  let mentions = [];
  function inContext(page) {
    if (started()) { const k = A.conv.context; return k.page === page || (k.pages || []).includes(page); }
    const c = context();
    return c.page === page || (c.pages || []).includes(page);
  }
  /** An @mention deleted from the message: the page it added leaves the context (and its chip), unless
   *  another mention of it is still there. */
  function mentionsEdited() {
    const text = input.value;
    const gone = mentions.filter((m) => !text.includes(m.token));
    if (!gone.length) return;
    mentions = mentions.filter((m) => text.includes(m.token));
    for (const m of gone) {
      if (mentions.some((x) => x.page === m.page)) continue;
      if (!started() && m.page === S.path && m.wasLeft) { A.extra = A.extra.filter((p) => p !== m.page); A.left.add("page"); drawChips(); }
      else changeContext({ drop: m.drop });
    }
  }
  /** Arrow keys, Enter and Tab work the @ list while it's open. */
  function mentionKey(ev) {
    if (mention.classList.contains("hidden") || !picks.length) return false;
    if (ev.key === "ArrowDown" || ev.key === "ArrowUp") { ev.preventDefault(); at = (at + (ev.key === "ArrowDown" ? 1 : picks.length - 1)) % picks.length; drawMention(); return true; }
    if (ev.key === "Enter" || ev.key === "Tab") { ev.preventDefault(); pickMention(at); return true; }
    if (ev.key === "Escape") { ev.preventDefault(); ev.stopPropagation(); mention.classList.add("hidden"); return true; }
    return false;
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

  // Select part of one of its answers, then the reply button over it: the part is quoted in a card above
  // the box, with its own line for the question about it. Several quotes, each with its own question, go
  // in one message; the AI reads them as Markdown quotes, each followed by what was asked about it.
  const askBtn = h("button", { class: "ai-ask-float hidden", title: "Reply to this part",
    onmousedown: (ev) => ev.preventDefault(), onclick: () => askAboutSelection(), html: ICON.reply });
  function replySelection() {
    const sel = window.getSelection();
    if (!sel || sel.isCollapsed || !sel.rangeCount) return null;
    const r = sel.getRangeAt(0).cloneRange();
    const at = (n) => (n?.nodeType === 1 ? n : n?.parentElement)?.closest?.(".ai-text");
    // One answer's text, even when the selection runs on past it (a triple click reaches into what follows).
    const a = at(r.startContainer) || at(r.endContainer);
    if (!a || !log.contains(a)) return null;
    if (!a.contains(r.startContainer)) r.setStart(a, 0);
    if (!a.contains(r.endContainer)) r.setEnd(a, a.childNodes.length);
    const msg = a.closest(".ai-msg-ai");
    if (!msg || msg.classList.contains("ai-streaming")) return null;
    const text = r.toString().trim();
    if (!text) return null;
    const i = Number(msg.dataset.msg);
    return { text: text.slice(0, 4000), msg: Number.isInteger(i) ? i : null, rect: r.getBoundingClientRect() };
  }
  function placeAsk() {
    const s = replySelection();
    if (!s) { askBtn.classList.add("hidden"); return; }
    askBtn.classList.remove("hidden");
    const w = askBtn.offsetWidth || 64, hgt = askBtn.offsetHeight || 26;
    const top = s.rect.top - hgt - 6 < 8 ? s.rect.bottom + 6 : s.rect.top - hgt - 6;
    askBtn.style.top = Math.round(top) + "px";
    askBtn.style.left = Math.round(Math.min(window.innerWidth - w - 8, Math.max(8, s.rect.left + s.rect.width / 2 - w / 2))) + "px";
  }
  function askAboutSelection() {
    const s = replySelection();
    askBtn.classList.add("hidden");
    if (!s) return;
    window.getSelection()?.removeAllRanges();
    quoteInBox(s.text);
  }
  // The quotes waiting to go with the next message: [{ text, ask }], drawn as cards above the box.
  let quoted = [];
  function quoteInBox(text) {
    const one = text.replace(/\s+/g, " ").trim();
    quoted.push({ text: one.length > 600 ? one.slice(0, 599).replace(/\s+\S*$/, "") + "…" : one, ask: "" });
    drawQuotes(quoted.length - 1);
    pane.classList.add("typing");
  }
  function drawQuotes(focusAt = null) {
    quotes.replaceChildren(...quoted.map((q, i) => {
      const ask = h("textarea", { class: "ai-quote-ask", rows: "1", placeholder: "Ask about this…", spellcheck: "true" });
      ask.value = q.ask;
      const fit = () => { ask.style.height = "auto"; ask.style.height = Math.min(ask.scrollHeight, 120) + "px"; };
      ask.addEventListener("input", () => { q.ask = ask.value; fit(); });
      ask.addEventListener("keydown", (ev) => {
        if (ev.key === "Enter" && !ev.shiftKey && !ev.isComposing) { ev.preventDefault(); send(); }
        else if (ev.key === "Tab" && ev.shiftKey) { ev.preventDefault(); switchMode(); }
        else if (ev.key === "Backspace" && !ask.value) { ev.preventDefault(); quoted.splice(i, 1); drawQuotes(Math.min(i, quoted.length - 1)); if (!quoted.length) input.focus(); }
      });
      queueMicrotask(fit);
      return h("div", { class: "ai-quote" },
        h("div", { class: "ai-quote-head" },
          h("div", { class: "ai-quote-text", title: q.text }, q.text),
          h("button", { class: "ai-quote-x", title: "Leave this quote out", html: ICON.close, onclick: () => { quoted.splice(i, 1); drawQuotes(); input.focus(); } })),
        ask);
    }));
    quotes.classList.toggle("hidden", !quoted.length);
    if (focusAt != null && focusAt >= 0) requestAnimationFrame(() => quotes.querySelectorAll(".ai-quote-ask")[focusAt]?.focus());
  }
  /** The message as sent: each quote ("> ...") with its question under it, then what's in the box. */
  function composed() {
    const parts = quoted.map((q) => "> " + q.text + (q.ask.trim() ? "\n" + q.ask.trim() : ""));
    if (input.value.trim()) parts.push(input.value.trim());
    return parts.join("\n\n");
  }
  log.addEventListener("mouseup", () => setTimeout(placeAsk));
  log.addEventListener("scroll", () => askBtn.classList.add("hidden"));
  document.addEventListener("selectionchange", () => { if (!askBtn.classList.contains("hidden") && !replySelection()) askBtn.classList.add("hidden"); });

  // ----------------------------------------------------------------------------- the conversation

  function drawHead() {
    const title = A.conv?.title || "New conversation";
    head.replaceChildren(
      h("button", { class: "ai-title" + (A.listOpen ? " on" : ""), title: "Earlier conversations", onclick: toggleList },
        h("span", { class: "ai-title-ico", html: ICON.list }), h("span", { class: "ai-title-text" }, title)),
      h("button", { class: "ai-new", title: "New conversation", html: ICON.new, onclick: () => { A.setupOpen = false; newConversation(); input.focus(); } }),
      h("button", { class: "ai-new ai-cog" + (A.setupOpen ? " on" : ""), title: "AI setup: how R'th AI answers", html: ICON.cog, onclick: toggleSetup }));
    if (A.listOpen) head.append(listEl());
  }

  let listCache = [];
  async function toggleList() {
    A.listOpen = !A.listOpen;
    if (A.listOpen) { const r = await api("/api/ai"); listCache = r.conversations || []; A.provider = r.provider || A.provider; }
    drawHead();
  }
  function listEl() {
    // As the logo's menu: one line a conversation, a tick by the open one, when on the right; the page
    // it's about is its tooltip.
    const items = listCache.map((c) => h("div", { class: "cm-menu-item lm-item ai-li" + (c.id === A.id ? " current" : ""),
      title: c.page ? "About " + (ctx.titleOf(c.page) || c.page.split("/").pop()) : null,
      onclick: () => { A.listOpen = false; openConversation(c.id); } },
      h("span", { class: "lm-check" }, c.id === A.id ? "✓" : ""),
      h("span", { class: "lm-label ai-li-title" }, c.title),
      h("span", { class: "lm-keys ai-li-meta" }, ago(c.updated)),
      h("button", { class: "ai-li-x", title: "Delete this conversation", html: ICON.close, onclick: async (ev) => {
        ev.stopPropagation();
        await api("/api/ai/delete", { method: "POST", body: JSON.stringify({ id: c.id }) });
        listCache = listCache.filter((x) => x.id !== c.id);
        if (c.id === A.id) newConversation(); else drawHead();
      } })));
    return h("div", { class: "cm-menu ai-list" }, h("div", { class: "lm-head" }, "Conversations"),
      items.length ? items : h("div", { class: "ai-li-none" }, "No earlier conversations."));
  }

  function newConversation() {
    A.id = null; A.conv = null; A.run = null; A.listOpen = false; A.extra = []; A.left.clear(); mentions = []; quoted = []; drawQuotes();
    A.mode = A.defaultMode; drawMode();
    localStorage.removeItem(KEY);
    drawHead(); drawLog(); syncSend();
  }

  async function openConversation(id) {
    const r = await api("/api/ai/conversation?id=" + encodeURIComponent(id));
    if (!r.ok) { if (id === A.id) newConversation(); return; }
    // Another conversation: the message's mentions were about this one, and it has its own mode.
    if (id !== A.id) mentions = [];
    if (A.conv?.id !== id) { A.mode = r.settings?.mode || A.defaultMode; drawMode(); } // opened (or first loaded): its own mode
    A.id = id; A.conv = r; A.setupOpen = false;
    queueMicrotask(drawChips); // its own context, not the screen's
    A.run = r.run?.state === "running" ? { text: r.run.text, activity: r.run.activity } : null;
    localStorage.setItem(KEY, id);
    drawHead(); drawLog(); syncSend();
  }

  // ----------------------------------------------------------------------------- AI setup (the cog)

  async function toggleSetup() {
    A.setupOpen = !A.setupOpen;
    A.listOpen = false;
    drawHead();
    if (A.setupOpen) { A.setup = await api("/api/ai/setup"); }
    drawLog();
  }
  async function configure(change) {
    const r = await api("/api/ai/setup", { method: "POST", body: JSON.stringify(change) });
    if (!r.ok) return toast(failed("Couldn't change the AI setup", r), "bad");
    A.setup = r; A.provider = r.provider || A.provider;
    drawLog();
  }

  // AI setup (the cog): a settings list. At the top, what answers now; then the choices everyone has, then
  // Claude's and OpenRouter's own. Each row: a label and a short note on the left, its control on the right
  // (or under it, for the wider ones).
  // The models the choices stand for today (Claude Code picks the latest of each).
  const MODEL_NAME = { fable: "Fable 5.1", opus: "Opus 5.5", sonnet: "Sonnet 5.5", haiku: "Haiku 5.5" };
  /** "claude-fable-5-1" as people say it: "Fable 5.1". */
  const modelName = (id) => { const m = String(id || "").replace(/^claude-/, "").match(/^([a-z]+)-(\d+)(?:-(\d+))?/i); return m ? m[1][0].toUpperCase() + m[1].slice(1) + " " + m[2] + (m[3] ? "." + m[3] : "") : MODEL_NAME[id] || id; };
  const EFFORT_NAME = { low: "Low", medium: "Medium", high: "High", xhigh: "Extra high", max: "Max" };
  /** A small menu of choices under `anchor` (a badge), in Codex's menu look; closed by a pick, a click
   *  elsewhere or Escape. */
  let menuEl = null;
  function closeMenu() { menuEl?.remove(); menuEl = null; document.removeEventListener("mousedown", outsideMenu, true); document.removeEventListener("keydown", menuKey, true); }
  const outsideMenu = (ev) => { if (menuEl && !menuEl.contains(ev.target)) closeMenu(); };
  const menuKey = (ev) => { if (ev.key === "Escape") { ev.stopPropagation(); closeMenu(); } };
  function openMenu(anchor, value, options, onPick) {
    closeMenu();
    menuEl = h("div", { class: "cm-menu ais-menu" }, options.map(([v, label]) => h("div", { class: "cm-menu-item lm-item" + (v === value ? " current" : ""), onclick: () => { closeMenu(); if (v !== value) onPick(v); } },
      h("span", { class: "lm-check" }, v === value ? "✓" : ""), h("span", { class: "lm-label" }, label))));
    document.body.append(menuEl);
    const r = anchor.getBoundingClientRect();
    const w = menuEl.offsetWidth, hgt = menuEl.offsetHeight;
    menuEl.style.left = Math.round(Math.max(8, Math.min(r.right - w, window.innerWidth - w - 8))) + "px";
    menuEl.style.top = Math.round(r.bottom + hgt + 4 > window.innerHeight ? r.top - hgt - 4 : r.bottom + 4) + "px";
    setTimeout(() => { document.addEventListener("mousedown", outsideMenu, true); document.addEventListener("keydown", menuKey, true); });
  }

  function setupEl() {
    const st = A.setup;
    if (!st) return h("div", { class: "ai-setup" }, h("div", { class: "ai-note" }, "Looking…"));
    const or = st.openrouter, cl = st.claude, cd = st.claudeDefaults || {};
    const ready = st.provider?.ok;
    const reload = async (q = "") => { A.setup = await api("/api/ai/setup" + q); A.provider = A.setup.provider || A.provider; drawLog(); };

    const section = (title, ...rows) => h("section", { class: "ais-section" }, h("h4", { class: "ais-title" }, title), h("div", { class: "ais-card" }, ...rows.flat().filter(Boolean)));
    // badge: something small on the title's own line, at its right (Model's "Default: Sonnet 5.5").
    const row = (label, note, control, { wide = false, badge = null } = {}) => h("div", { class: "ais-row" + (wide ? " wide" : "") },
      h("div", { class: "ais-label" }, h("div", { class: "ais-name-row" }, h("div", { class: "ais-name" }, label), badge), note ? h("div", { class: "ais-note" }, note) : null),
      control ? h("div", { class: "ais-control" }, control) : null);
    // A default as a small badge you can change: "Default: Sonnet 5.5 ▾".
    // Its list is Codex's own menu (the conversations' look), under the badge.
    const badgePick = (shown, value, options, onPick) => {
      const badge = h("button", { class: "ais-badge", title: "Change the default", onclick: (ev) => { ev.stopPropagation(); openMenu(badge, value, options, onPick); } },
        h("span", {}, "Default: "), h("span", { class: "ais-badge-value" }, shown), h("span", { class: "ais-badge-caret" }, "▾"));
      return badge;
    };
    // A choice of a few: one joined control, in the badges' colours.
    const segmented = (value, options, onPick) => h("div", { class: "ais-seg", role: "radiogroup" },
      options.map(([v, label]) => h("button", { class: "ais-seg-btn" + (value === v ? " on" : ""), role: "radio", "aria-checked": value === v ? "true" : "false", onclick: () => value !== v && onPick(v) }, label)));
    const toggle = (on, onChange, title) => h("button", { class: "ais-switch" + (on ? " on" : ""), role: "switch", "aria-checked": on ? "true" : "false", title, onclick: () => onChange(!on) }, h("span", { class: "ais-knob" }));
    const select = (value, options, onPick) => {
      const el = h("select", { class: "field ais-select", onchange: () => onPick(el.value) }, options.map(([v, label]) => h("option", { value: v, selected: value === v ? "" : null }, label)));
      return el;
    };

    const general = section("General",
      row("Which AI", st.use === "auto" ? "Claude when it's set up here, otherwise OpenRouter" : null,
        segmented(st.use, [["auto", "Automatic"], ["claude", "Claude"], ["openrouter", "OpenRouter"]], (v) => configure({ use: v })), { wide: true }),
      row("New conversations start in", st.mode === "work" ? "Work: it edits the pages; Show and Undo after" : "Chat: its changes wait for you to accept or skip",
        segmented(st.mode === "work" ? "work" : "chat", [["chat", "Chat"], ["work", "Work"]], (v) => { A.defaultMode = v; configure({ mode: v }); })));

    // Claude: how it stands, then the model and effort it answers with, and what it does by itself.
    const claudeState = !cl.found
      ? row("Claude Code", "Not installed on this computer.", h("div", { class: "ais-buttons" },
          h("button", { class: "btn btn-small", onclick: () => window.open("https://claude.com/claude-code", "_blank") }, "Get it"),
          h("button", { class: "btn btn-small", onclick: () => reload("?recheck=1") }, "Check again")))
      : cl.signedIn === false
        ? row("Claude Code", "Installed, but not signed in.", h("div", { class: "ais-buttons" },
            h("button", { class: "btn btn-gold btn-small", title: "Opens a terminal running Claude Code", onclick: async () => { const r = await api("/api/ai/claude-signin", { method: "POST" }); if (r.error) toast(failed("Couldn't open the sign-in", r), "bad"); else toast("Sign in in the terminal, then check again"); } }, "Sign in"),
            h("button", { class: "btn btn-small", onclick: () => reload("?recheck=1") }, "Check again")))
        : row("Claude Code", (cl.signedIn ? "Signed in" : "Installed") + (cl.version ? " · " + cl.version.replace(/\s*\(Claude Code\)/, "") : ""), null);
    const conv = A.conv?.settings || {};
    const claudeOn = cl.found && cl.signedIn !== false;
    const claude = section("Claude",
      claudeState,
      claudeOn ? row("Model", `/model changes it for one conversation${conv.model ? ` (this one: ${modelName(conv.model)})` : ""}.`, null,
        { badge: badgePick(modelName(cd.lastModel && cd.lastModel.includes(cd.model) ? cd.lastModel : cd.model), cd.model, [["sonnet", MODEL_NAME.sonnet], ["opus", MODEL_NAME.opus], ["fable", MODEL_NAME.fable], ["haiku", MODEL_NAME.haiku]], (v) => configure({ claudeModel: v })) }) : null,
      claudeOn ? row("Effort", `/effort changes it for one conversation${conv.effort ? ` (this one: ${EFFORT_NAME[conv.effort] || conv.effort})` : ""}.`, null,
        { badge: badgePick(EFFORT_NAME[cd.effort] || cd.effort, cd.effort, [["low", "Low"], ["medium", "Medium"], ["high", "High"], ["xhigh", "Extra high"], ["max", "Max"]], (v) => configure({ claudeEffort: v })) }) : null,
      row("Health check", "While Codex is idle, R'th AI checks changed pages for problems across pages and lists them on the To-do page",
        toggle(!!st.audit, (v) => configure({ audit: v }), "Check the GDD while idle")),
      row("Answer @R'th AI in Discord", "This Codex may answer when someone tags R'th AI in a comment thread (only one Codex does)",
        toggle(!!st.answerDiscord, (v) => configure({ answerDiscord: v }), "Answer @R'th AI in Discord")));

    // OpenRouter: the account, its model, and a key pasted by hand (folded away).
    const keyInput = h("input", { class: "field", type: "password", placeholder: "sk-or-…", autocomplete: "off", spellcheck: "false" });
    const modelInput = h("input", { class: "field", value: or.model, placeholder: "Free (automatic)", spellcheck: "false" });
    const useKey = async () => {
      const r = await api("/api/openrouter/key", { method: "POST", body: JSON.stringify({ key: keyInput.value }) });
      keyInput.value = "";
      if (!r.ok) return toast(failed("Couldn't use that key", r), "bad");
      toast("Connected to OpenRouter", "good");
      reload();
    };
    const openrouter = section("OpenRouter",
      or.signedIn
        ? row("Account", "Connected", h("button", { class: "btn btn-small", onclick: async () => { await api("/api/openrouter/signout", { method: "POST" }); reload(); } }, "Disconnect"))
        : row("Account", "Free models, on your own daily allowance", h("button", { class: "btn btn-gold btn-small", onclick: signInOpenRouter }, "Sign in")),
      !or.signedIn ? (A.keyOpen
        ? row("Key", null, h("div", { class: "ais-inline" }, keyInput, h("button", { class: "btn btn-small", onclick: useKey }, "Use")), { wide: true })
        : h("button", { class: "ais-more", onclick: () => { A.keyOpen = true; drawLog(); } }, "Use a key instead")) : null,
      row("Model", h("span", {}, "Free (automatic) unless you name one · ", h("a", { href: "https://openrouter.ai/models", target: "_blank" }, "browse")),
        h("div", { class: "ais-inline" }, modelInput, h("button", { class: "btn btn-small", onclick: () => configure({ model: modelInput.value }) }, "Save")), { wide: true }));

    return h("div", { class: "ai-setup" }, general, claude, openrouter,
      h("p", { class: "ais-foot" }, "Keys and conversations stay on this computer."));
  }

  function drawLog() {
    if (A.setupOpen) { log.replaceChildren(setupEl()); return; }
    const msgs = A.conv?.messages || [];
    const out = [];
    if (!msgs.length && !A.run) out.push(emptyState());
    msgs.forEach((m, i) => out.push(message(m, i)));
    if (A.pending) out.push(message({ by: "you", text: A.pending.text, context: A.pending.context, pending: true }));
    if (A.run) out.push(streamFor().el);
    else dropStream();
    const atEnd = log.scrollHeight - log.scrollTop - log.clientHeight < 40;
    log.replaceChildren(...out);
    if (atEnd || !A.run) log.scrollTop = log.scrollHeight;
  }

  // The answer under way (src/ai-stream.js): one block kept across redraws, so its reveal runs on.
  let stream = null;
  function streamFor() {
    if (stream && stream.id !== A.id) dropStream();
    if (!stream) {
      stream = answerStream({ h, render: ctx.renderHtml, scroller: log });
      stream.id = A.id;
      stream.set(A.run, { instant: true }); // an answer already under way when it's opened: shown as it stands
    } else stream.set(A.run);
    return stream;
  }
  function dropStream() { stream?.destroy(); stream = null; }

  function emptyState() {
    const p = A.provider;
    return h("div", { class: "ai-empty" },
      h("div", { class: "ai-empty-title" }, "Talk it through"),
      h("p", {}, "Ask about the GDD, or talk an idea through. It reads what the chips below say: the page, the passage you selected, the comment you have open. Ask it to change something and it will."),
      p && !p.ok && p.needsSignIn !== "openrouter" ? h("p", { class: "ai-warn" }, "R'th AI isn't ready on this computer. ", // what's wrong is told in AI setup
        h("button", { class: "ai-link", onclick: toggleSetup }, "Open AI setup")) : null,
      p && p.needsSignIn === "openrouter" ? h("div", { class: "ai-signin" },
        h("p", {}, "R'th AI isn't set up on this computer yet. Sign in with OpenRouter and it runs on free models, on your own daily allowance (about 50 messages a day)."),
        h("button", { class: "btn btn-gold", onclick: signInOpenRouter }, "Sign in with OpenRouter"),
        h("p", { class: "ai-note" }, "Not now? Everything else in Codex works without it.")) : null,
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
      if (heads >= 3 && words < heads * 12) out.push("Draft this page from what the GDD already says");
      out.push("Review this page against the Charter and the pages it links to");
      if (!c.thread) out.push("What does this page leave open?");
    }
    if (c.selection) out.unshift("Is the selected passage clear and consistent with the rest of the GDD?");
    if (!out.length) out.push("What's decided so far about combat?");
    return out.slice(0, 3);
  }

  /** Fix with R'th AI (a health-check problem on the To-do): a new conversation in Chat mode, about the
   *  problem's pages, its plan as the message; the suggestions are reviewed in the pages. */
  function fixFound(p) {
    newConversation();
    A.mode = "chat"; drawMode();
    A.extra = p.pages.filter((x) => x !== S.path);
    A.problem = p.id;
    focus();
    input.value = `Fix this problem the health check found: ${p.summary}\n\n${p.detail ? p.detail + "\n\n" : ""}The plan: ${p.plan}`;
    grow();
    send();
  }

  /** Ask something straight away (a starting prompt, or Ask AI from the search box). */
  function ask(text) {
    focus();
    input.value = text;
    grow();
    send();
  }

  /** Your message, its quote lines ("> ...") drawn as quotes, as they looked in the box. */
  function youText(text) {
    const el = h("div", { class: "ai-msg-text" });
    let para = [];
    const flush = () => { if (para.length) { el.append(h("div", {}, para.join("\n"))); para = []; } };
    for (const line of String(text || "").split("\n")) {
      if (/^>\s?/.test(line)) { flush(); el.append(h("div", { class: "ai-msg-quote" }, line.replace(/^>\s?/, ""))); }
      else para.push(line);
    }
    flush();
    return el;
  }

  /** Copy a message as written (its Markdown): a small button that shows when pointing at it. */
  const copyBtn = (m) => (m.text && !m.pending ? h("button", { class: "ai-copy", title: "Copy", html: ICON.copy, onclick: (ev) => { ev.stopPropagation(); ctx.copyText(m.text, m.by === "you" ? "your message" : "the answer"); } }) : null);

  function message(m, i = null) {
    if (m.by === "codex") return h("div", { class: "ai-msg ai-msg-codex" }, body(m.text));
    if (m.by === "you") {
      const c = m.context || {};
      const bits = [];
      if (c.selection?.quote) bits.push("“" + clip(c.selection.quote, 60) + "”");
      if (c.thread) bits.push("a comment");
      return h("div", { class: "ai-msg ai-msg-you" },
        bits.length ? h("div", { class: "ai-msg-ctx" }, "On " + bits.join(" and ")) : null,
        youText(m.text), copyBtn(m));
    }
    return h("div", { class: "ai-msg ai-msg-ai", "data-msg": i == null ? null : String(i) },
      m.text ? body(m.text) : null, copyBtn(m),
      m.changes?.length ? h("div", { class: "ai-changes" }, m.changes.map((x) => changeLine(x, m))) : null,
      m.comments?.length ? h("div", { class: "ai-changes" }, m.comments.map(commentLine)) : null,
      problemCards(m, i),
      m.error && m.error !== "Stopped." ? h("div", { class: "ai-warn" }, "It stopped: " + m.error) : null,
      m.error === "Stopped." ? h("div", { class: "ai-note" }, "Stopped.") : null);
  }

  /** A page's type, as its type badge says it ("Skill"): from its frontmatter, else its folder's type. */
  function typeName(x) {
    let t = null;
    try { t = splitFront(x.after ?? x.before ?? "").data?.type || null; } catch {}
    if (!t) t = typeAt(S.types || [], x.page)?.type || null;
    return t ? t.replace(/^./, (c) => c.toUpperCase()) : null;
  }
  /** "Skill Woodcutting": the type, then the page's title in bold. */
  const named = (x, title) => { const t = typeName(x); return [t ? h("span", { class: "ai-change-type" }, t + " ") : null, h("b", {}, title)]; };
  const updates = (n) => `${n} update${n === 1 ? "" : "s"}`;

  /** A page the AI changed in a reply: what, with Show (the page, its changes marked) and Undo. */
  function changeLine(x, m) {
    const title = ctx.titleOf(x.page) || headingOf(x.after ?? x.before) || x.page.split("/").pop();
    const n = updates(x.places || 1);
    const idx = A.conv.messages.indexOf(m);
    const dismiss = async (ev, label = "Dismissing…") => busy(ev, label, async () => { await api("/api/ai/reviewed", { method: "POST", body: JSON.stringify({ conv: A.id, msg: idx, page: x.page }) }); await openConversation(A.id); });
    // A new page it suggests: there's no page to review in yet, so it's previewed, then created or skipped.
    if (x.suggest && x.before === null) {
      const name = ctx.titleOf(x.page) || headingOf(x.after) || x.page.split("/").pop();
      return h("div", { class: "ai-change" + (x.reviewed ? " reviewed" : ""), title: x.reviewed ? "Handled" : x.page },
        x.reviewed ? h("span", { class: "ai-change-check", html: ICON.check }) : null,
        h("span", { class: "ai-change-text" }, "Suggests a new page, ", ...named(x, name)),
        x.reviewed ? null : h("button", { class: "ai-link", onclick: () => ctx.previewNewPage({ page: x.page, name, text: x.after,
          create: async () => {
            const r = await api("/api/ai/create-page", { method: "POST", body: JSON.stringify({ conv: A.id, msg: idx, page: x.page }) });
            if (!r.ok) { toast(failed("Couldn't make the page", r), "bad"); return false; }
            openConversation(A.id);
            return true;
          },
          skip: () => dismiss(null) }) }, "Preview"),
        x.reviewed ? null : h("button", { class: "ai-link", onclick: (ev) => dismiss(ev, "Skipping…") }, "Skip"));
    }
    // Suggestions reviewed (accepted, skipped or dismissed): done, with a check and no actions.
    if (x.suggest) return h("div", { class: "ai-change" + (x.reviewed ? " reviewed" : ""), title: x.reviewed ? "Reviewed" : null },
      x.reviewed ? h("span", { class: "ai-change-check", html: ICON.check }) : null,
      h("span", { class: "ai-change-text" }, `Suggested ${n} for `, ...named(x, title)),
      x.reviewed ? null : h("button", { class: "ai-link", onclick: () => ctx.showChanges(x.page) }, "Review"),
      x.reviewed ? null : h("button", { class: "ai-link", onclick: dismiss }, "Dismiss"));
    const what = x.before === null ? "Made" : x.after === null ? "Deleted" : `${n} in`;
    return h("div", { class: "ai-change" + (x.reviewed ? " reviewed" : "") },
      h("span", { class: "ai-change-text" }, what + " ", ...named(x, title)),
      x.after !== null ? h("button", { class: "ai-link", onclick: () => ctx.showChanges(x.page) }, "Show") : null,
      h("button", { class: "ai-link", onclick: (ev) => busy(ev, "Undoing…", () => undoTurn(idx, x.page)) }, "Undo"));
  }

  // ----------------------------------------------------------------------------- problems it brought in

  // The Codex checked what the answer changed (src/turn-check.js): a card per page with problems, its
  // count; a click opens what each one is, with Fix (the To-do's fix, or asking R'th AI) and Dismiss.
  const openProblems = new Set(); // "msg:page" cards opened
  function problemCards(m, i) {
    const live = (m.problems || []).map((p, j) => ({ ...p, j })).filter((p) => !p.done);
    if (!live.length || i == null) return null;
    const byPage = new Map();
    for (const p of live) { if (!byPage.has(p.page)) byPage.set(p.page, []); byPage.get(p.page).push(p); }
    return h("div", { class: "ai-changes" }, [...byPage].map(([page, ps]) => {
      const k = i + ":" + page, open = openProblems.has(k);
      const title = ctx.titleOf(page) || page.split("/").pop();
      const head = h("div", { class: "ai-change ai-problem" + (open ? " open" : ""), title: open ? "Hide" : "What's wrong", onclick: () => { open ? openProblems.delete(k) : openProblems.add(k); drawLog(); } },
        h("span", { class: "ai-problem-ico" }, "!"),
        h("span", { class: "ai-change-text" }, `${ps.length} problem${ps.length === 1 ? "" : "s"} in `, ...named({ page, after: null, before: null }, title)),
        h("span", { class: "ai-problem-caret" }, open ? "▾" : "▸"));
      if (!open) return head;
      return h("div", { class: "ai-problem-group" }, head, ...ps.map((p) => h("div", { class: "ai-problem-row" },
        h("span", { class: "ai-problem-text" }, (p.line ? `Line ${p.line}: ` : "") + p.text),
        h("span", { class: "ai-problem-acts" },
          h("button", { class: "ai-link", title: p.fix ? "Fix it the way the To-do does" : "Ask R'th AI to fix it", onclick: (ev) => busy(ev, "Fixing…", () => fixProblem(i, p)) }, "Fix"),
          h("button", { class: "ai-link", onclick: (ev) => busy(ev, "…", () => problemDone(i, p.j, "dismissed")) }, "Dismiss")))));
    }));
  }
  async function problemDone(msg, j, how) {
    const r = await api("/api/ai/problem", { method: "POST", body: JSON.stringify({ conv: A.id, msg, i: j, how }) });
    if (!r.ok) return toast(failed("Couldn't do that", r), "bad");
    await openConversation(A.id);
  }
  async function fixProblem(msg, p) {
    if (p.fix) {
      if (S.path === p.page && !(await ctx.saveNow())) return toast("Couldn't save the page first", "bad");
      const r = await api("/api/fix", { method: "POST", body: JSON.stringify({ fix: p.fix }) });
      if (r.ok) { toast("Fixed", "good"); if (S.path === p.page) S.reloadOpen = true; return problemDone(msg, p.j, "fixed"); }
      // No fix of its own after all (or not allowed): R'th AI is asked instead.
    }
    await problemDone(msg, p.j, "fixed");
    input.value = `Fix this problem your change brought in on \`${p.page}\`${p.line ? ` (line ${p.line})` : ""}: ${p.text}`;
    grow();
    send();
  }

  /** A page's title from its text: its first heading. */
  function headingOf(text) {
    const m = String(text || "").match(/^#\s+(.+)$/m);
    return m ? m[1].trim() : null;
  }

  /** A comment the AI left (or a reply it posted): where, and Show to open it beside its page. */
  function commentLine(x) {
    const title = x.page ? ctx.titleOf(x.page) || x.page.split("/").pop() : "a thread";
    return h("div", { class: "ai-change" },
      h("span", { class: "ai-change-text" }, (x.reply ? "Replied in a comment on " : "Commented on ") + "", h("b", {}, title)),
      h("button", { class: "ai-link", onclick: () => ctx.notes.focus(x.id, x.page) }, "Show"));
  }

  async function undoTurn(msg, page) {
    const r = await api("/api/ai/undo", { method: "POST", body: JSON.stringify({ conv: A.id, msg, page }) });
    if (!r.ok) return toast(failed("Couldn't undo it", r), "bad");
    if (r.open?.length) {
      toast("That page was edited since: undo its changes one by one where they're marked", "bad");
      ctx.showChanges(r.open[0]);
    } else toast("Undone");
    openConversation(A.id);
  }

  /** An answer as Markdown, with repository paths that are pages opening them. */
  function body(text) {
    const el = h("div", { class: "cm-text ai-text", html: ctx.renderHtml(text) });
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
    const text = composed();
    if (!text || A.run || A.sending) return;
    const kept = { input: input.value, quoted: quoted.map((q) => ({ ...q })) };
    await ctx.saveNow(); // it reads the page as saved
    const context = started() ? {} : ctxSnapshot(); // a started conversation keeps its own (on the server)
    A.sending = true;
    A.pending = { text, context: { selection: context.selection, thread: context.thread } };
    A.run = { text: "", activity: "Reading" };
    input.value = ""; quoted = []; drawQuotes(); grow(); pane.classList.remove("typing");
    drawLog(); syncSend();
    const problem = A.problem; A.problem = null;
    const r = await api("/api/ai/send", { method: "POST", body: JSON.stringify({ id: A.id, text, context, mode: A.mode, problem }) });
    A.sending = false;
    if (!r.ok) {
      A.pending = null; A.run = null;
      input.value = kept.input; quoted = kept.quoted; drawQuotes(); grow();
      toast(failed("Couldn't send your message", r), "bad");
      if (r.needsSignIn) refreshProvider();
      drawLog(); syncSend();
      return;
    }
    A.left.clear(); A.extra = []; mentions = []; // sent: what the mentions added is the conversation's now
    if (r.id !== A.id) { A.id = r.id; localStorage.setItem(KEY, r.id); }
    A.pending = null;
    await openConversation(r.id);
    if (r.command) { A.run = null; drawLog(); syncSend(); drawChips(); return; } // /model, /effort: no answer to wait for
    if (!A.run) A.run = { text: "", activity: "Reading" };
    drawLog(); syncSend(); drawChips();
  }
  const ctxSnapshot = () => context();

  async function stop() {
    if (A.id) await api("/api/ai/stop", { method: "POST", body: JSON.stringify({ id: A.id }) });
  }

  async function answered(ev) {
    ctx.markTab(true);
    const c = await api("/api/ai/conversation?id=" + encodeURIComponent(ev.id));
    const name = c.ok ? c.title : "your question";
    const go = () => { ctx.show(); if (ev.id !== A.id) openConversation(ev.id); };
    toast(ev.error && ev.error !== "Stopped." ? `R'th AI couldn't finish “${name}”` : `R'th AI answered “${name}”`, ev.error ? "bad" : "", { actions: [{ label: "Open", run: go }] });
  }

  /** From the server: the answer streaming in, or finished. */
  function onEvent(ev) {
    if (ev.state === "provider") { // signed in or out of OpenRouter, or AI setup changed
      if (A.setupOpen) api("/api/ai/setup").then((r) => { A.setup = r; drawLog(); });
      return refreshProvider();
    }
    // An answer that lands while you're elsewhere (another tab, another conversation): a mark on the AI
    // tab and a notice that opens it, so working on while it thinks doesn't mean checking back.
    if (ev.state === "done" && (ev.id !== A.id || !ctx.visible())) answered(ev);
    // Changes reviewed (in the page, or dismissed here): the open conversation's cards show it at once.
    if (ev.state === "reviewed" && A.id && !A.run && (ev.id === A.id || !ev.id) &&
      A.conv?.messages?.some((m) => (m.changes || []).some((x) => !x.reviewed && (!ev.page || x.page === ev.page)))) return openConversation(A.id);
    if (ev.id !== A.id) return;
    if (ev.state === "checked") { if (!A.run) openConversation(A.id); return; } // the check found problems: their cards
    if (ev.state === "running") {
      A.run = { text: ev.text, activity: ev.activity };
      if (stream?.el.isConnected && stream.id === A.id) stream.set(A.run); else drawLog();
      syncSend();
    } else if (ev.state === "done") {
      // Let the reveal finish what has arrived, then draw the finished message in its place.
      const s = stream?.el.isConnected && stream.id === ev.id ? stream : null;
      const open = () => { if (A.id === ev.id) { A.run = null; openConversation(ev.id); } };
      if (s) s.finish(open); else open();
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
    document.body.append(askBtn);
    // Opening or closing a comment changes the thread chip.
    document.addEventListener("click", () => { if (!pane.classList.contains("hidden")) requestAnimationFrame(drawChips); });
    drawHead(); drawLog(); drawChips(); drawMode();
    const r = await api("/api/ai");
    A.provider = r.provider || null;
    if (r.defaultMode) { A.defaultMode = r.defaultMode; if (!A.id) { A.mode = r.defaultMode; drawMode(); } }
    if (A.id) await openConversation(A.id); else drawLog();
  }

  return {
    init, focus, onEvent, ask, fixFound,
    shown: () => { ctx.markTab(false); drawChips(); if (!A.conv && !A.run) drawLog(); },
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
  return d.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
}
