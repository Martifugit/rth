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

const ICON = {
  send: '<svg viewBox="0 0 24 24"><path d="M5 12h13M13 6l6 6-6 6"/></svg>',
  stop: '<svg viewBox="0 0 24 24"><rect x="7" y="7" width="10" height="10" rx="1.5"/></svg>',
  new: '<svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>',
  list: '<svg viewBox="0 0 24 24"><path d="M12 7v5l3 2"/><circle cx="12" cy="12" r="8"/></svg>',
  cog: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M12 2.5v2.6M12 18.9v2.6M4.6 4.6l1.8 1.8M17.6 17.6l1.8 1.8M2.5 12h2.6M18.9 12h2.6M4.6 19.4l1.8-1.8M17.6 6.4l1.8-1.8"/><circle cx="12" cy="12" r="6.5"/></svg>',
  check: '<svg viewBox="0 0 24 24"><path d="m5 12.5 4.5 4.5L19 7.5"/></svg>',
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
    extra: [],           // pages added (with @ or + Add page) to a conversation not started yet
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
  const box = h("div", { class: "ai-box" }, mention, chips, h("div", { class: "ai-row" }, input, sendBtn));
  pane.append(head, log, box);

  input.addEventListener("keydown", (ev) => {
    if (mentionKey(ev)) return;
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

  // ----------------------------------------------------------------------------- the conversation

  function drawHead() {
    const title = A.conv?.title || "New conversation";
    head.replaceChildren(
      h("button", { class: "ai-title" + (A.listOpen ? " on" : ""), title: "Earlier conversations", onclick: toggleList },
        h("span", { class: "ai-title-ico", html: ICON.list }), h("span", { class: "ai-title-text" }, title)),
      h("button", { class: "ai-new", title: "New conversation", html: ICON.new, onclick: () => { A.setupOpen = false; newConversation(); input.focus(); } }),
      h("button", { class: "ai-new ai-cog" + (A.setupOpen ? " on" : ""), title: "AI setup: which AI answers, and ways to connect", html: ICON.cog, onclick: toggleSetup }));
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
    A.id = null; A.conv = null; A.run = null; A.listOpen = false; A.extra = []; A.left.clear(); mentions = [];
    localStorage.removeItem(KEY);
    drawHead(); drawLog(); syncSend();
  }

  async function openConversation(id) {
    const r = await api("/api/ai/conversation?id=" + encodeURIComponent(id));
    if (!r.ok) { if (id === A.id) newConversation(); return; }
    if (id !== A.id) mentions = []; // another conversation: the message's mentions were about this one
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

  function setupEl() {
    const st = A.setup;
    if (!st) return h("div", { class: "ai-setup" }, h("div", { class: "ai-note" }, "Looking…"));
    const choice = (value, label, note) => h("label", { class: "ai-choice" + (st.use === value ? " on" : "") },
      h("input", { type: "radio", name: "ai-use", checked: st.use === value ? "" : null, onchange: () => configure({ use: value }) }),
      h("span", {}, h("b", {}, label), note ? h("span", { class: "ai-choice-note" }, note) : null));
    const usingName = st.using === "claude" ? "Claude" : "OpenRouter";
    const ready = st.provider?.ok;
    const or = st.openrouter, cl = st.claude;
    const recheck = () => h("button", { class: "btn", onclick: async () => { A.setup = await api("/api/ai/setup?recheck=1"); A.provider = A.setup.provider || A.provider; drawLog(); } }, "Check again");
    const keyInput = h("input", { class: "field ai-key", type: "password", placeholder: "or paste a key (sk-or-…)", autocomplete: "off", spellcheck: "false" });
    const modelInput = h("input", { class: "field ai-model", value: or.model, placeholder: "Free (automatic)", spellcheck: "false" });
    return h("div", { class: "ai-setup" },
      h("div", { class: "ai-setup-now" + (ready ? "" : " warn") }, ready ? "Using " : "Not ready: ", h("b", {}, usingName),
        !ready && st.provider?.error ? h("div", { class: "ai-note" }, st.provider.error) : null),

      h("h4", {}, "Which AI"),
      choice("auto", "Automatic", "Claude if it's set up, otherwise OpenRouter"),
      choice("claude", "Claude", "Claude Code, on your Claude subscription"),
      choice("openrouter", "OpenRouter", "Free models, or any model with credit"),

      h("h4", {}, "Claude"),
      !cl.found
        ? [h("p", { class: "ai-note" }, "Claude Code isn't installed."),
           h("div", { class: "ai-setup-row" }, h("button", { class: "btn", onclick: () => window.open("https://claude.com/claude-code", "_blank") }, "Get Claude Code"), recheck())]
        : cl.signedIn === false
          ? [h("p", { class: "ai-note" }, "Installed, but not signed in."),
             h("div", { class: "ai-setup-row" },
               h("button", { class: "btn btn-gold", title: "Opens a terminal running Claude Code", onclick: async () => { const r = await api("/api/ai/claude-signin", { method: "POST" }); if (r.error) toast(failed("Couldn't open the sign-in", r), "bad"); else toast("Sign in in the terminal, then check again"); } }, "Sign in"),
               recheck())]
          : h("p", { class: "ai-note" }, (cl.signedIn ? "Signed in" : "Installed") + (cl.version ? ", " + cl.version : "")),

      h("h4", {}, "OpenRouter"),
      or.signedIn
        ? h("div", { class: "ai-setup-row" }, h("span", { class: "ai-note" }, "Connected."),
            h("button", { class: "btn", onclick: async () => { await api("/api/openrouter/signout", { method: "POST" }); A.setup = await api("/api/ai/setup"); drawLog(); } }, "Disconnect"))
        : [h("div", { class: "ai-setup-row" }, h("button", { class: "btn btn-gold", onclick: signInOpenRouter }, "Sign in with OpenRouter")),
           h("div", { class: "ai-setup-row" }, keyInput,
             h("button", { class: "btn", onclick: async () => {
               const r = await api("/api/openrouter/key", { method: "POST", body: JSON.stringify({ key: keyInput.value }) });
               keyInput.value = "";
               if (!r.ok) return toast(failed("Couldn't use that key", r), "bad");
               toast("Connected to OpenRouter", "good");
               A.setup = await api("/api/ai/setup"); drawLog();
             } }, "Use key"))],
      h("label", { class: "ai-setup-label" }, "Model ", h("a", { href: "https://openrouter.ai/models", target: "_blank", class: "ai-choice-note" }, "browse")),
      h("div", { class: "ai-setup-row" }, modelInput,
        h("button", { class: "btn", onclick: () => configure({ model: modelInput.value }) }, "Save")),
      h("p", { class: "ai-note" }, "Keys and conversations stay on this computer."));
  }

  function drawLog() {
    if (A.setupOpen) { log.replaceChildren(setupEl()); return; }
    const msgs = A.conv?.messages || [];
    const out = [];
    if (!msgs.length && !A.run) out.push(emptyState());
    for (const m of msgs) out.push(message(m));
    if (A.pending) out.push(message({ by: "you", text: A.pending.text, context: A.pending.context }));
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

  /** Ask something straight away (a starting prompt, or Ask AI from the search box). */
  function ask(text) {
    focus();
    input.value = text;
    grow();
    send();
  }

  function message(m) {
    if (m.by === "codex") return h("div", { class: "ai-msg ai-msg-codex" }, body(m.text));
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
      m.comments?.length ? h("div", { class: "ai-changes" }, m.comments.map(commentLine)) : null,
      m.error && m.error !== "Stopped." ? h("div", { class: "ai-warn" }, "It stopped: " + m.error) : null,
      m.error === "Stopped." ? h("div", { class: "ai-note" }, "Stopped.") : null);
  }

  /** A page the AI changed in a reply: what, with Show (the page, its changes marked) and Undo. */
  function changeLine(x, m) {
    const title = ctx.titleOf(x.page) || x.page.split("/").pop();
    const n = `${x.places || 1} change${x.places === 1 ? "" : "s"}`;
    const idx = A.conv.messages.indexOf(m);
    const dismiss = async (ev, label = "Dismissing…") => busy(ev, label, async () => { await api("/api/ai/reviewed", { method: "POST", body: JSON.stringify({ conv: A.id, msg: idx, page: x.page }) }); await openConversation(A.id); });
    // A new page it suggests: there's no page to review in yet, so it's previewed, then created or skipped.
    if (x.suggest && x.before === null) {
      const name = ctx.titleOf(x.page) || headingOf(x.after) || x.page.split("/").pop();
      return h("div", { class: "ai-change" + (x.reviewed ? " reviewed" : ""), title: x.reviewed ? "Handled" : x.page },
        x.reviewed ? h("span", { class: "ai-change-check", html: ICON.check }) : null,
        h("span", { class: "ai-change-text" }, "Suggests a new page, ", h("b", {}, name)),
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
      h("span", { class: "ai-change-text" }, `Suggested ${n} to `, h("b", {}, title)),
      x.reviewed ? null : h("button", { class: "ai-link", onclick: () => ctx.showChanges(x.page) }, "Review"),
      x.reviewed ? null : h("button", { class: "ai-link", onclick: dismiss }, "Dismiss"));
    const what = x.before === null ? "Made" : x.after === null ? "Deleted" : `Changed ${x.places || 1} place${x.places === 1 ? "" : "s"} in`;
    return h("div", { class: "ai-change" + (x.reviewed ? " reviewed" : "") },
      h("span", { class: "ai-change-text" }, what + " ", h("b", {}, title)),
      x.after !== null ? h("button", { class: "ai-link", onclick: () => ctx.showChanges(x.page) }, "Show") : null,
      h("button", { class: "ai-link", onclick: (ev) => busy(ev, "Undoing…", () => undoTurn(idx, x.page)) }, "Undo"));
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
    const text = input.value.trim();
    if (!text || A.run || A.sending) return;
    await ctx.saveNow(); // it reads the page as saved
    const context = started() ? {} : ctxSnapshot(); // a started conversation keeps its own (on the server)
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
    // Opening or closing a comment changes the thread chip.
    document.addEventListener("click", () => { if (!pane.classList.contains("hidden")) requestAnimationFrame(drawChips); });
    drawHead(); drawLog(); drawChips();
    const r = await api("/api/ai");
    A.provider = r.provider || null;
    if (A.id) await openConversation(A.id); else drawLog();
  }

  return {
    init, focus, onEvent, ask,
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
