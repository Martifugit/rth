// R'th Codex: the page. Reads and edits the repository's Markdown through server.js.
// Read and Edit are the same editor (TipTap), so what you edit looks exactly like what you read;
// Source is the raw Markdown. Saving writes back only the blocks you changed (merge.js).

import { Editor, Extension, mergeAttributes } from "@tiptap/core";
import StarterKit from "@tiptap/starter-kit";
import Link from "@tiptap/extension-link";
import Image from "@tiptap/extension-image";
import Table from "@tiptap/extension-table";
import TableRow from "@tiptap/extension-table-row";
import TableHeader from "@tiptap/extension-table-header";
import TableCell from "@tiptap/extension-table-cell";
import TaskList from "@tiptap/extension-task-list";
import TaskItem from "@tiptap/extension-task-item";
import Placeholder from "@tiptap/extension-placeholder";
import Code from "@tiptap/extension-code";
import Paragraph from "@tiptap/extension-paragraph";
import Text from "@tiptap/extension-text";
import { Markdown } from "tiptap-markdown";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { mergeEdit, renderHtml } from "./merge.js";
import { makeNotes, NoteMarks } from "./notes.js";
import { makePanels } from "./panels.js";
import { slug } from "./links.js";
import { makeLayout } from "./layout.js";
import { makeAi } from "./ai.js";
import { makeAccountButton } from "./account-button.js";
import { makePasteHandler, forDiscord } from "./discord-extras.js";
import { AiMarks, makeAiChanges } from "./ai-changes.js";
import { altGrChar, isKeyFor } from "./keys.js";
import { makeTogetherUi } from "./together-ui.js";
import { Spelling, configureSpelling } from "./spellcheck.js";
import { GDD, README, TOP, TEMPLATES, isMeta, undeletable, folderPath, typeAt } from "./gdd.js";
import { split as splitFront, make as makeFront } from "./frontmatter.js";
import { makeGddUi } from "./gdd-ui.js";
import { pagePicker } from "./page-picker.js";
import { failed } from "./plain-errors.js";

const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => [...el.querySelectorAll(s)];
// ?preview=... shows how updating looks (startPreview); read at once, as opening the start page replaces the address.
const PREVIEW = new URLSearchParams(location.search).get("preview");
let previewing = null; // up here: the start-up code below runs before the update section is reached
const h = (tag, props = {}, ...kids) => {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (v == null || v === false) continue;
    if (k === "class") el.className = v;
    else if (k.startsWith("on")) el.addEventListener(k.slice(2), v);
    else if (k === "html") el.innerHTML = v;
    else el.setAttribute(k, v === true ? "" : v);
  }
  for (const kid of kids.flat(Infinity)) if (kid != null && kid !== false) el.append(kid.nodeType ? kid : document.createTextNode(kid));
  return el;
};
const esc = (s) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
const collator = new Intl.Collator("en", { numeric: true, sensitivity: "base" });

// The Codex opens on the GDD's start page: a welcome, the reading order, the way to the Charter.
const DEFAULT_DOC = README;

const S = {
  files: [],          // [{path, title, git}]
  fileSet: new Set(),
  path: null,
  disk: "",           // the text on disk as last loaded or saved
  diskHash: null,
  eol: "\n",
  base: "",           // the text the editor was loaded from
  baseline: "",       // the editor's Markdown right after loading `base`
  mode: "edit",       // "edit" (the page, always editable) or "source" (the Markdown)
  editor: null,
  dirty: false,
  open: new Set(JSON.parse(localStorage.getItem("codex.open") || '["docs","docs/gdd"]')),
  types: [],          // the GDD's types, read from How the GDD Works: [{ type, folder, covers, examples, headings }]
  components: [],     // and its components: [{ name, group, answers, fields, foundOn, generated }]
  front: "",          // the page's frontmatter as written (never in the editor; put back in front when saving)
};

// ----------------------------------------------------------------------------- paths

const dirOf = (p) => (p.includes("/") ? p.slice(0, p.lastIndexOf("/")) : "");
function normalize(p) {
  const out = [];
  for (const part of p.split("/")) {
    if (part === "" || part === ".") continue;
    if (part === "..") out.pop(); else out.push(part);
  }
  return out.join("/");
}
function resolveHref(from, href) {
  let [path, hash] = href.split("#");
  path = decodeURIComponent(path.split("?")[0]);
  if (!path) return { path: from, hash: hash || "" };
  const abs = path.startsWith("/") ? normalize(path) : normalize(dirOf(from) + "/" + path);
  return { path: abs, hash: hash || "", dir: path.endsWith("/") };
}
function relativePath(from, to) {
  const a = dirOf(from).split("/").filter(Boolean), b = to.split("/");
  let i = 0;
  while (i < a.length && i < b.length - 1 && a[i] === b[i]) i++;
  return "../".repeat(a.length - i) + b.slice(i).join("/");
}
const isExternal = (href) => /^[a-z][a-z0-9+.-]*:/i.test(href);
const assetUrl = (src) => (!src || isExternal(src) ? src : "/repo/" + resolveHref(S.path || "", src).path);
function folderPage(dir) {
  for (const n of ["README.md", "index.md"]) if (S.fileSet.has(dir + "/" + n)) return dir + "/" + n;
  return null;
}

export { slug };

// ----------------------------------------------------------------------------- editor

const DocImage = Image.extend({
  renderHTML({ HTMLAttributes }) {
    return ["img", mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, { src: assetUrl(HTMLAttributes.src), loading: "lazy" })];
  },
});

function linkClass(href) {
  if (!href) return null;
  if (isExternal(href)) return /^mailto:/.test(href) ? null : "link-ext";
  if (href.startsWith("#")) return null;
  const { path, dir } = resolveHref(S.path, href);
  if (dir || !/\.[a-z0-9]+$/i.test(path)) return null;
  if (path.endsWith(".md") && !S.fileSet.has(path) && path.startsWith(GDD + "/")) return "link-todo";
  return null;
}

// Marks links to pages that don't exist (red, wavy) and links out of the repository (↗).
const LinkMarks = Extension.create({
  name: "codexLinkMarks",
  // tiptap-markdown drops a line break that follows an inline element (a link at the end of a
  // wrapped line ran into the next word): a soft line break is only a space, so render it as one.
  addStorage() {
    return { markdown: { parse: { setup(md) { md.renderer.rules.softbreak = () => " "; } } } };
  },
  addProseMirrorPlugins() {
    return [new Plugin({
      key: new PluginKey("codexLinkMarks"),
      state: {
        init: (_, state) => linkDecorations(state.doc),
        apply: (tr, old) => (tr.docChanged ? linkDecorations(tr.doc) : old),
      },
      props: { decorations(state) { return this.getState(state); } },
    })];
  },
});
function linkDecorations(doc) {
  const decos = [];
  doc.descendants((node, pos) => {
    if (!node.isText) return;
    const link = node.marks.find((m) => m.type.name === "link");
    if (!link) return;
    const cls = linkClass(link.attrs.href);
    if (cls) decos.push(Decoration.inline(pos, pos + node.nodeSize, { class: cls, title: cls === "link-todo" ? "Not written yet: click to make it" : null }));
  });
  return DecorationSet.create(doc, decos);
}

// tiptap-markdown trims the space inside bold or italic, measured from where the mark opens; in a
// quote that was before the "> " of the line was written, so "> **A**" lost its line break. Write
// the pending line start first.
const FlushedParagraph = Paragraph.extend({
  addStorage() {
    return {
      markdown: {
        serialize(state, node) {
          state.flushClose(state.inTightList ? 1 : 2);
          state.write(); // the line's prefix ("> ", a list's indent)
          state.renderInline(node);
          state.closeBlock(node);
        },
        parse: {},
      },
    };
  },
});

// tiptap-markdown writes "<" and ">" in text as "&lt;" and "&gt;". The docs mean them literally
// ("<Area name>" in the templates) and the editor reads them as text, so write them as they are.
const PlainText = Text.extend({
  addStorage() {
    return { markdown: { serialize(state, node) { state.text(node.text); }, parse: {} } };
  },
});

// The GDD's card over the page, component sections and generated lists (src/gdd-ui.js).
const gddUi = makeGddUi(() => ({
  S, h, api, toast, modal, openDoc, editor: () => S.editor,
  menu: (ev, items) => rowMenu(ev, ev.currentTarget, items),
  setFront: (front) => { S.front = front; scheduleDirty(); },
  newPage: (prefill) => newPageDialog(null, prefill),
  ask: (title, html, buttons) => ask(title, html, buttons),
  save: () => save(false, { quiet: true }),
  loadTree: () => loadTree(),
  typeIcon: (type) => TYPE_ICONS[type] || "journal",
}));

function makeEditor(text) {
  if (S.editor) S.editor.destroy();
  // Frontmatter stays out of the editor (it would read as a rule and a heading): kept aside, shown as
  // the strip under the title, and written back in front of the page when saving.
  const parts = splitFront(text);
  text = parts.body;
  const page = $("#page");
  page.innerHTML = "";
  // Nothing about the page is taken as current until its editor exists: if making it fails, the old
  // page's text must never be saved under this page's name (or the other way round).
  S.editor = null;
  S.unsafe = true;
  S.front = parts.front;
  S.editor = new Editor({
    element: page,
    editable: S.mode === "edit" && !S.readOnly,
    extensions: [
      StarterKit.configure({ heading: { levels: [1, 2, 3, 4, 5, 6] }, code: false, paragraph: false, text: false }),
      PlainText,
      FlushedParagraph,
      // The docs put code inside links, bold and italics: let code sit with other marks.
      Code.extend({ excludes: "" }),
      Link.configure({ openOnClick: false, autolink: false, linkOnPaste: true, isAllowedUri: () => true, HTMLAttributes: { rel: null, target: null, class: null } }),
      DocImage.configure({ inline: true, allowBase64: false }),
      Table.configure({ resizable: false }), TableRow, TableHeader, TableCell,
      // Checklists are written tight, like the other lists (tiptap-markdown only tightens those).
      TaskList.extend({ addAttributes() { return { ...this.parent?.(), tight: { default: true, rendered: false } }; } }),
      TaskItem.configure({ nested: true }),
      Placeholder.configure({ placeholder: "Write something worth reading…" }),
      Markdown.configure({ html: false, tightLists: true, bulletListMarker: "-", linkify: false, breaks: false, transformPastedText: true, transformCopiedText: true }),
      LinkMarks,
      NoteMarks,
      AiMarks,
      Spelling,
      gddUi.Extension,
    ],
    content: text,
    onUpdate: () => { clearHits(); scheduleDirty(); scheduleOutline(); notes?.docChanged(); aiChanges?.docChanged(); together?.typed(); },
    editorProps: {
      attributes: { spellcheck: "false" }, handlePaste: (view, event) => discordPaste(view, event),
      // AltGr is Ctrl+Alt: a character typed with it (@, {, [, $ on Norwegian) is typed, never taken as
      // one of the editor's Ctrl+Alt shortcuts (headings). src/keys.js.
      handleKeyDown: (view, e) => {
        const ch = altGrChar(e);
        if (!ch || !view.editable) return false;
        view.dispatch(view.state.tr.insertText(ch).scrollIntoView());
        return true;
      },
    },
    onTransaction: () => updateToolbar(),
  });
  S.base = text;
  S.baseline = editorMarkdown();
  S.unsafe = false; // the page is whole: saving may go on
  if (notes && notes.state.page === S.path) notes.place(true);
}

// Focus now, not on the next animation frame as TipTap's focus() does: keys pressed straight after
// switching to Edit must land in the page.
function focusEditor(pos = null) {
  const view = S.editor.view;
  view.focus();
  if (pos != null) S.editor.commands.setTextSelection(pos);
}

const editorMarkdown = () => S.editor.storage.markdown.getMarkdown();

function draft() {
  if (S.mode === "source") return $("#source").value;
  if (S.unsafe || !S.editor || S.editor.isDestroyed) return S.disk; // nothing to save from a page that didn't open
  const cur = editorMarkdown();
  return S.front + (cur === S.baseline ? S.base : mergeEdit(S.base, S.baseline, cur));
}

// Pages save themselves a moment after you stop typing, as in Google Docs (Ctrl+S saves at once).
let dirtyTimer = null, autoTimer = null;
const AUTOSAVE_MS = 1200;
function scheduleDirty() {
  clearTimeout(dirtyTimer); dirtyTimer = setTimeout(refreshDirty, 250);
  clearTimeout(autoTimer); autoTimer = setTimeout(autosave, AUTOSAVE_MS);
}
async function autosave() {
  if (S.saving || S.conflict || S.noSave) return; // noSave: checks that edit pages without saving them
  refreshDirty();
  if (S.dirty) await save(false, { quiet: true });
}
function refreshDirty() {
  S.dirty = !!S.path && draft() !== S.disk;
  document.title = S.view ? VIEWS[S.view].title + " - Codex" : (S.dirty ? "● " : "") + (pageTitle() || "R'th Codex") + " - Codex";
  updateStatus();
}

// ----------------------------------------------------------------------------- loading pages

async function api(url, opts) {
  let r;
  try { r = await fetch(url, opts && { headers: { "Content-Type": "application/json" }, ...opts }); }
  catch { return { ok: false, status: 0, error: "The Codex's engine isn't answering." }; } // src/plain-errors.js says what to do

  const j = await r.json().catch(() => ({}));
  return { ok: r.ok, status: r.status, ...j };
}

async function loadTree() {
  const [r, g] = await Promise.all([api("/api/tree"), api("/api/gdd")]);
  if (g.ok && Array.isArray(g.types)) { S.types = g.types; S.components = g.components || []; S.prompts = g.prompts || null; }
  // An engine older than the page (a Codex left running across an update) doesn't know the GDD's calls.
  else showBanner("Codex needs a restart: its engine is older than this page. Close Codex and open it again.", [{ label: "OK", run: hideBanner }]);
  S.files = r.files || [];
  S.fileSet = new Set(S.files.map((f) => f.path));
  renderTree();
  gddUi.refresh(); // the strip's group chips and type label read the page list and the types
}

// ----------------------------------------------------------------------------- back and forward

// Following a link, a search hit or a page in the list adds a step to the history; Back (Alt+←, the
// mouse's back button, the arrow in the title bar) goes back to where you were, scrolled as you left it.
// Each step keeps its scroll position, written as you scroll.
let scrollTimer = null;
function rememberScroll() {
  clearTimeout(scrollTimer); scrollTimer = null;
  if (!S.path || S.view || !history.state) return;
  history.replaceState({ ...history.state, scroll: $("#scroller").scrollTop }, "");
}
function go(dir) {
  rememberScroll();
  if (dir < 0) history.back(); else history.forward();
}
function drawNav() {
  const nav = window.navigation;
  const b = $("#backBtn"), f = $("#fwdBtn");
  if (!b || !f) return;
  b.disabled = nav ? !nav.canGoBack : false;
  f.disabled = nav ? !nav.canGoForward : false;
}

async function openDoc(path, { hash = "", push = true, find = null, keepScroll = false, mode = null, scroll = null } = {}) {
  S.reloadOpen = false;
  if (S.path && path !== S.path) {
    clearTimeout(autoTimer);
    refreshDirty();
    if (S.dirty && !S.noSave && !(await save(false, { quiet: true }))) return;
  }
  if (push) rememberScroll();
  const r = await api("/api/doc?path=" + encodeURIComponent(path));
  hideView();
  if (!r.ok) {
    toast(`Can't open ${path}: ${r.error || r.status}`, "bad");
    if (!S.path && path !== DEFAULT_DOC) return openDoc(DEFAULT_DOC, { push: false });
    return;
  }
  const before = $("#scroller").scrollTop;
  const samePage = r.path === S.path;
  S.path = r.path; S.disk = r.content; S.diskHash = r.hash; S.eol = r.eol;
  hideBanner();
  if (mode) S.mode = mode;
  else if (!samePage) S.mode = "edit";
  S.conflict = false;
  makeEditor(r.content);
  $("#source").value = r.content;
  applyMode();
  if (push) history.pushState({ path: S.path, hash }, "", "?p=" + encodeURI(S.path) + (hash ? "#" + hash : ""));
  drawNav();
  tellPresence();
  together.pageChanged();
  renderCrumbs(); markTreeCurrent(); buildOutline(); refreshDirty();
  notes.load(S.path);
  ai.pageChanged();
  aiChanges.pageOpened(S.wantChanges === S.path);
  S.wantChanges = null;
  if (rightTab === "links") panels.renderLinks(); else panels.linksFor(S.path).then((r) => r && setLinksCount(r.inbound.length));
  requestAnimationFrame(() => {
    if (keepScroll) $("#scroller").scrollTop = before;
    else if (scroll != null) $("#scroller").scrollTop = scroll;
    else if (hash) scrollToHeading(hash);
    else $("#scroller").scrollTop = 0;
    if (find) findInPage(find.q, find.occurrence);
  });
}

function pageTitle() {
  if (!S.editor) return null;
  let t = null;
  S.editor.state.doc.descendants((n) => { if (!t && n.type.name === "heading") t = n.textContent; return !t; });
  return t || (S.path ? S.path.split("/").pop() : null);
}

// ----------------------------------------------------------------------------- modes

function setMode(mode) {
  if (!S.editor || mode === S.mode) return;
  // Keep roughly the same place in the page across the switch.
  const sc = $("#scroller"), ta = $("#source");
  const from = S.mode === "source" ? ta : sc;
  const ratio = from.scrollTop / Math.max(1, from.scrollHeight - from.clientHeight);
  if (S.mode === "source") {
    const text = $("#source").value;
    S.mode = mode;
    if (text !== S.front + S.base) { makeEditor(text); buildOutline(); }
  } else if (mode === "source") {
    $("#source").value = draft();
    S.mode = mode;
  } else {
    S.mode = mode;
  }
  applyMode();
  refreshDirty();
  if (mode === "source") { ta.setSelectionRange(0, 0); ta.focus({ preventScroll: true }); }
  const to = mode === "source" ? ta : sc;
  to.scrollTop = ratio * (to.scrollHeight - to.clientHeight);
}

function applyMode() {
  const main = $("#main");
  main.classList.remove("mode-read", "mode-edit", "mode-source");
  main.classList.add("mode-" + S.mode);
  $("#sourceBtn").classList.toggle("on", S.mode === "source");
  $("#sourceBtn").title = S.mode === "source" ? "Back to the page (Esc)" : "Edit the Markdown source";
  S.editor.setEditable(S.mode === "edit" && !S.readOnly);
  $("#page").classList.toggle("hidden", S.mode === "source");
  $("#source").classList.toggle("hidden", S.mode !== "source");
  $("#toolbar").classList.toggle("hidden", S.mode !== "edit" || !!S.readOnly);
  $("#readOnlyBar").classList.toggle("hidden", !S.readOnly);
  $("#deleteBtn").classList.toggle("hidden", !!S.readOnly || undeletable(S.path)); // the top pages and templates stay
  gddUi.refresh(); // "+ Add action" shows only while editing
  $("#docWrap").classList.toggle("source", S.mode === "source");
  updateToolbar();
  if (typeof notes !== "undefined") requestAnimationFrame(() => notes.layout());
}

// ----------------------------------------------------------------------------- saving

async function save(force = false, { quiet = false } = {}) {
  if (!S.path || S.saving) return false;
  // A page whose editor failed to open is never saved: what's in the editor may be another page.
  if (S.unsafe || !S.editor || S.editor.isDestroyed) { if (!quiet) toast("This page didn't open properly, so it isn't saved. Reload Codex.", "bad"); return false; }
  clearTimeout(autoTimer);
  const content = draft();
  if (content === S.disk && !force) { if (!quiet) toast("All changes saved"); return true; }
  S.saving = true; updateStatus();
  let r;
  try { r = await api("/api/doc", { method: "PUT", body: JSON.stringify({ path: S.path, content, eol: S.eol, baseHash: S.diskHash, force }) }); }
  finally { S.saving = false; }
  if (r.status === 409) {
    S.conflict = true; // no more autosaving until this is settled
    updateStatus();
    const choice = await ask("This page changed somewhere else", `<b>${esc(S.path)}</b> was saved by something else (an agent, git, or another window) while you were editing it.`, [
      { key: "theirs", label: "Use theirs (lose my edits)", cls: "btn-danger" }, { key: "mine", label: "Keep mine (overwrite theirs)", cls: "btn-gold" },
    ]);
    if (choice === "mine") { S.conflict = false; return save(true); }
    if (choice === "theirs") { S.conflict = false; S.dirty = false; await openDoc(S.path, { push: false, keepScroll: true }); }
    return false;
  }
  if (!r.ok) { toast(failed("Couldn't save the page", r), "bad"); updateStatus(); return false; }
  S.conflict = false;
  S.disk = content; S.diskHash = r.hash;
  if (S.mode !== "source") { S.base = content.slice(S.front.length); S.baseline = editorMarkdown(); notes.saved(); }
  panels.invalidate();
  gddUi.refresh({ lists: true });
  hideBanner();
  refreshDirty();
  if (!quiet) toast("Saved", "good");
  const f = S.files.find((x) => x.path === S.path);
  if (f) {
    const fm = splitFront(content).data;
    const moved = f.status !== (fm?.status || null) || f.type !== (fm?.type || null) || !f.git;
    f.status = fm?.status || null; f.type = fm?.type || null; f.git = f.git || "changed";
    if (moved) renderTree();
  }
  return true;
}

// ----------------------------------------------------------------------------- live changes from disk

function listen() {
  const es = new EventSource("/api/events");
  es.addEventListener("notes", (e) => notes.onNotes(JSON.parse(e.data)));
  // A waiting comment change that Discord then refused outright (src/comment-store.js, flush): said once.
  es.addEventListener("notes-failed", (e) => { const v = JSON.parse(e.data); toast("A comment change that was waiting to be posted was refused by Discord, so it was dropped: " + v.error, "bad"); });
  es.addEventListener("run", (e) => notes.onRun(JSON.parse(e.data)));
  es.addEventListener("sync", (e) => showSync(JSON.parse(e.data)));
  es.addEventListener("news", (e) => showNews(JSON.parse(e.data)));
  es.addEventListener("here", (e) => together.onEvent(JSON.parse(e.data)));
  es.addEventListener("ai", (e) => { const ev = JSON.parse(e.data); ai.onEvent(ev); if (ev.state === "reviewed" || ev.pages?.includes(S.path)) aiChanges.load(); });
  es.addEventListener("account", (e) => account.onEvent(JSON.parse(e.data)));
  es.addEventListener("required", () => api("/api/update").then(showUpdate, () => {}));
  es.onmessage = (e) => {
    const events = JSON.parse(e.data);
    panels.invalidate();
    gddUi.refresh({ lists: true });
    notesChanged();
    if (S.view) VIEWS[S.view].render();
    refreshSpecialCounts();
    for (const ev of events) {
      if (ev.path !== S.path || ev.hash === S.diskHash || ev.path === S.deleting) continue;
      if (!ev.hash) { showBanner(`This page was deleted or moved on disk.`, []); continue; }
      clearTimeout(dirtyTimer); refreshDirty(); // count keystrokes from the last moment too
      if (!S.dirty) { openDoc(S.path, { push: false, keepScroll: true }); toast("Reloaded: the page changed on disk"); }
      else showBanner("This page changed on disk while you were editing.", [
        { label: "Load theirs", cls: "btn-danger", run: () => { S.dirty = false; openDoc(S.path, { push: false, keepScroll: true }); } },
        { label: "Keep mine", run: hideBanner },
      ]);
    }
  };
}

function showBanner(text, actions) {
  const b = $("#banner");
  b.replaceChildren(h("span", { class: "grow" }, text), ...actions.map((a) => h("button", { class: "btn " + (a.cls || ""), onclick: a.run }, a.label)));
  b.classList.remove("hidden");
}
function hideBanner() { $("#banner").classList.add("hidden"); }

// ----------------------------------------------------------------------------- the tree

// Each type's folder icon (the folders themselves come from How the GDD Works).
const TYPE_ICONS = {
  system: "settings", skill: "skills", entity: "bestiary", object: "compass", item: "pack",
  ability: "spark", effect: "dreaming", quest: "quests", activity: "fishing", achievement: "task", location: "map",
  group: "journal", lore: "scripture", media: "eye", guide: "guide", decision: "note",
};
const folderIcon = (d) => TYPE_ICONS[S.types.find((t) => t.folder && folderPath(t) === d.path)?.type] || "journal";

function buildTreeData(files) {
  const root = { name: "", path: "", dirs: new Map(), files: [] };
  for (const f of files) {
    const parts = f.path.split("/");
    let node = root;
    for (let i = 0; i < parts.length - 1; i++) {
      const p = parts.slice(0, i + 1).join("/");
      if (!node.dirs.has(parts[i])) node.dirs.set(parts[i], { name: parts[i], path: p, dirs: new Map(), files: [] });
      node = node.dirs.get(parts[i]);
    }
    node.files.push(f);
  }
  return root;
}
const fileOrder = (a, b) => {
  const rank = (f) => (/\/(README|index)\.md$/i.test("/" + f.path) ? 0 : 1);
  return rank(a) - rank(b) || collator.compare(a.path.split("/").pop(), b.path.split("/").pop());
};
const countFiles = (n) => n.files.length + [...n.dirs.values()].reduce((s, d) => s + countFiles(d), 0);

// ----------------------------------------------------------------------------- right-click in the page list

/** A small menu where the mouse is: [label, run, { danger, disabled, hint }], or "-" for a line. */
function rowMenu(ev, rowEl, items) {
  ev.preventDefault();
  document.querySelector(".tree-menu")?.remove();
  const close = () => { m.remove(); rowEl.classList.remove("menu-on"); document.removeEventListener("mousedown", outside, true); document.removeEventListener("keydown", onKey, true); };
  const outside = (e) => { if (!e.target.closest(".tree-menu")) close(); };
  const onKey = (e) => { if (e.key === "Escape") { e.stopPropagation(); close(); } };
  const m = h("div", { class: "cm-menu tree-menu" }, items.filter(Boolean).map((x) => x === "-" ? h("div", { class: "spell-sep" }) :
    h("button", { class: "cm-menu-item" + (x[2]?.danger ? " danger" : ""), disabled: x[2]?.disabled || null, title: x[2]?.hint || null, onclick: () => { close(); x[1](); } }, x[0])));
  document.body.append(m);
  m.style.left = Math.min(ev.clientX, window.innerWidth - m.offsetWidth - 8) + "px";
  m.style.top = Math.min(ev.clientY, window.innerHeight - m.offsetHeight - 8) + "px";
  rowEl.classList.add("menu-on");
  setTimeout(() => { document.addEventListener("mousedown", outside, true); document.addEventListener("keydown", onKey, true); }, 0);
}
const codexLink = (page) => `https://rth-codex.rthapp.workers.dev/open?p=${encodeURIComponent(page)}`;
const copyText = (text, what) => navigator.clipboard.writeText(text).then(() => toast("Copied " + what, "good"), () => toast("Couldn't copy", "bad"));
const signInHint = (what) => (S.readOnly ? { disabled: true, hint: `Sign in with Discord (with the Codex role) to ${what}` } : {});

function pageMenu(ev, f) {
  const inDocs = f.path.startsWith(GDD + "/");
  const name = f.title || f.path.split("/").pop().replace(/\.md$/, "");
  rowMenu(ev, ev.currentTarget, [
    ["Open", () => openDoc(f.path)],
    inDocs && ["New page in this folder…", () => newPageDialog(dirOf(f.path)), signInHint("make pages")],
    "-",
    ["Copy link for Discord", () => copyText(`[${name.replace(/[\[\]]/g, "")}](<${codexLink(f.path)}>)`, "a link to " + name)],
    ["Copy path", () => copyText(f.path, f.path)],
    inDocs && !undeletable(f.path) && "-",
    inDocs && !undeletable(f.path) && ["Delete page…", () => deletePage(f.path), { danger: true, ...signInHint("delete pages") }],
  ]);
}

function folderMenu(ev, d) {
  const inDocs = d.path === GDD || d.path.startsWith(GDD + "/");
  rowMenu(ev, ev.currentTarget, [
    inDocs && ["New page here…", () => newPageDialog(d.path), signInHint("make pages")],
    ["Copy path", () => copyText(d.path, d.path)],
  ]);
}

function renderTree() {
  const filter = $("#treeFilter").value.trim().toLowerCase();
  const tree = $("#tree");
  const matches = (f) => !filter || f.path.toLowerCase().includes(filter) || (f.title || "").toLowerCase().includes(filter);
  // The templates stay out of the list: they work behind New page and + component, not as pages to read.
  const files = S.files.filter((f) => !f.path.startsWith(TEMPLATES + "/") && matches(f));
  const data = buildTreeData(files);
  const mark = (text) => {
    if (!filter) return text;
    const i = text.toLowerCase().indexOf(filter);
    return i < 0 ? text : [text.slice(0, i), h("mark", {}, text.slice(i, i + filter.length)), text.slice(i + filter.length)];
  };
  const fileRow = (f) => h("div", {
    class: "t-row file" + (f.path === S.path ? " current" : ""), "data-path": f.path, title: (f.title ? f.title + "\n" : "") + f.path,
    onclick: () => openDoc(f.path),
    oncontextmenu: (ev) => pageMenu(ev, f),
  }, h("span", { class: "caret" }), h("span", { class: "ico ico-note" }), h("span", { class: "t-name" }, mark((!f.path.startsWith(TEMPLATES + "/") && f.title) || f.path.split("/").pop().replace(/\.md$/, ""))), // templates by their type or component
  f.git ? h("span", { class: "t-git " + f.git, title: f.git === "new" ? "New, not in git yet" : "Changed since the last commit" }) : null);
  const dirNode = (d) => {
    const open = filter || S.open.has(d.path);
    const kids = h("div", { class: "t-children" + (open ? "" : " closed") },
      [...d.dirs.values()].sort((a, b) => collator.compare(a.name, b.name)).map(dirNode), d.files.sort(fileOrder).map(fileRow));
    const n = countFiles(d);
    const row = h("div", {
      class: "t-row folder" + (open ? " open" : "") + (n ? "" : " empty"), title: d.path,
      oncontextmenu: (ev) => folderMenu(ev, d),
      onclick: () => {
        const isOpen = !kids.classList.toggle("closed");
        row.classList.toggle("open", isOpen);
        if (isOpen) S.open.add(d.path); else S.open.delete(d.path);
        localStorage.setItem("codex.open", JSON.stringify([...S.open]));
      },
    }, h("span", { class: "caret" }), h("span", { class: "ico ico-" + folderIcon(d) }), h("span", { class: "t-name" }, mark(d.name)),
    h("span", { class: "t-count" }, n ? String(n) : ""));
    return h("div", {}, row, kids);
  };
  // The GDD, flat: its start page and the pages about it, templates/, then one folder per type in the
  // order How the GDD Works lists them, shown even before they hold a page.
  const gdd = data.dirs.get("docs")?.dirs.get("gdd") || { name: "gdd", path: GDD, dirs: new Map(), files: [] };
  if (!filter) for (const name of S.types.filter((t) => t.folder).map((t) => t.folder)) // templates/ isn't listed
    if (!gdd.dirs.has(name)) gdd.dirs.set(name, { name, path: GDD + "/" + name, dirs: new Map(), files: [] });
  const specialRow = (kind, icon, help) => h("div", { class: "t-row file t-special" + (S.view === kind ? " current" : ""), "data-view": kind, title: help, onclick: () => showView(kind) },
    h("span", { class: "caret" }), h("span", { class: "ico ico-" + icon }), h("span", { class: "t-name" }, VIEWS[kind].title),
    h("span", { class: "t-count t-special-n" + (VIEWS[kind].hot ? " hot" : "") }, VIEWS[kind].count || ""));
  const special = !filter ? h("div", { class: "t-specials" },
    specialRow("changes", "scripture", "Who changed which page, and where, newest first"),
    S.brokenCount ? specialRow("broken", "compass", "Pages to write, and pages whose structure needs a look") : null) : null;
  const rows = gddOrder(gdd).map((x) => (x.dirs ? dirNode(x) : fileRow(x)));
  tree.replaceChildren(...(rows.length || special ? [special, ...rows].filter(Boolean) : [h("div", { class: "o-empty" }, "No pages match.")]));
}

/** The GDD's own order: the top pages (README, the Charter...), templates/, the type folders as How the
 *  GDD Works lists them, then any other folder or page alphabetically. */
function gddOrder(gdd) {
  const name = (x) => (x.dirs ? x.name : x.path.split("/").pop());
  const folders = ["templates", ...S.types.filter((t) => t.folder).map((t) => t.folder)];
  const rank = (x) => {
    if (!x.dirs) return TOP.includes(x.path) ? TOP.indexOf(x.path) : 50; // other pages at the top (the game page) after the top pages
    const i = folders.indexOf(x.name);
    return 100 + (i >= 0 ? i : 500);
  };
  return [...gdd.files, ...gdd.dirs.values()].sort((a, b) => rank(a) - rank(b) || collator.compare(name(a), name(b)));
}

function markTreeCurrent() {
  if (S.path) {
    const parts = S.path.split("/");
    let changed = false;
    for (let i = 1; i < parts.length; i++) { const p = parts.slice(0, i).join("/"); if (!S.open.has(p)) { S.open.add(p); changed = true; } }
    if (changed) { localStorage.setItem("codex.open", JSON.stringify([...S.open])); renderTree(); }
  }
  $$(".t-row.file").forEach((r) => r.classList.toggle("current", r.dataset.path === S.path));
  const cur = $(".t-row.current");
  if (cur) cur.scrollIntoView({ block: "nearest" });
}

// ----------------------------------------------------------------------------- breadcrumbs, outline, status

function renderCrumbs() {
  const parts = S.path.split("/");
  // "docs" and "gdd" say nothing (everything is in the GDD): the crumbs start at the type's folder.
  const skip = S.path.startsWith(GDD + "/") ? GDD.split("/").length : S.path.startsWith("docs/") ? 1 : 0;
  const out = [];
  parts.forEach((part, i) => {
    if (i < skip) return;
    if (i > skip) out.push(h("span", { class: "sep" }, "/"));
    if (i === parts.length - 1) { out.push(h("span", { class: "leaf" }, part.replace(/\.md$/, ""))); return; }
    const dir = parts.slice(0, i + 1).join("/");
    const page = folderPage(dir);
    out.push(page && page !== S.path ? h("a", { onclick: () => openDoc(page), title: page }, part) : h("span", { class: "leaf", style: "color:#8b806c" }, part));
  });
  out.push(h("button", { class: "tb copy", title: "Copy the path", onclick: () => { navigator.clipboard.writeText(S.path); toast("Copied " + S.path); } },
    h("span", { html: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="1.8"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V5a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3"/></svg>' })));
  $("#crumbs").replaceChildren(...out);
}

let headings = [];
let outlineTimer = null;
function scheduleOutline() { clearTimeout(outlineTimer); outlineTimer = setTimeout(buildOutline, 400); }
function buildOutline() {
  headings = [];
  const seen = new Map();
  S.editor.state.doc.descendants((node, pos) => {
    if (node.type.name !== "heading") return;
    let id = slug(node.textContent);
    const n = seen.get(id) || 0;
    seen.set(id, n + 1);
    if (n) id += "-" + n;
    headings.push({ level: node.attrs.level, text: node.textContent, pos, id });
    return false;
  });
  const shown = headings.filter((x) => x.level <= 4);
  $("#outline").replaceChildren(...(shown.length ? shown.map((x) => h("a", { class: "o-item l" + x.level, "data-id": x.id, title: x.text, onclick: () => scrollToHeading(x.id, true) }, x.text)) : [h("div", { class: "o-empty" }, "No headings")]));
  spy();
  updateStatus();
}
function headingEl(x) { const el = S.editor.view.nodeDOM(x.pos); return el && el.nodeType === 1 ? el : null; }
function scrollToHeading(id, push = false) {
  const x = headings.find((y) => y.id === id) || headings.find((y) => y.id === slug(decodeURIComponent(id)));
  if (!x) return;
  const el = headingEl(x);
  if (el) $("#scroller").scrollTop += el.getBoundingClientRect().top - $("#scroller").getBoundingClientRect().top - 12;
  if (push) history.replaceState({ path: S.path, hash: x.id }, "", "?p=" + encodeURI(S.path) + "#" + x.id);
}
function spy() {
  if (!headings.length || S.mode === "source") return;
  const top = $("#scroller").getBoundingClientRect().top + 60;
  let active = null;
  for (const x of headings) {
    if (x.level > 4) continue;
    const el = headingEl(x);
    if (el && el.getBoundingClientRect().top <= top) active = x.id; else if (el) break;
  }
  $$(".o-item").forEach((a) => a.classList.toggle("active", a.dataset.id === active));
  S.activeHeading = active;
  const act = $(".o-item.active");
  if (act) act.scrollIntoView({ block: "nearest" });
}

function updateStatus() {
  if (!S.editor) return;
  const words = (S.editor.state.doc.textContent.match(/[\p{L}\p{N}'’]+/gu) || []).length;
  $("#statusLeft").textContent = `${words.toLocaleString()} words, ${Math.max(1, Math.round(words / 230))} min read`;
  const sync = syncWords();
  const right = $("#statusRight");
  right.innerHTML = S.conflict ? '<span class="unsaved">Not saved: the page changed somewhere else</span>'
    : S.saving ? '<span class="muted">Saving…</span>'
    : S.dirty ? '<span class="muted">Editing…</span>'
    : `<span class="${sync.cls}">${sync.text}</span>`;
  right.title = !S.dirty && !S.saving ? sync.title || "" : "";
}


// ----------------------------------------------------------------------------- following links

function followLink(href, text = "") {
  if (!href) return;
  if (isExternal(href)) { window.open(href, "_blank", "noopener"); return; }
  if (href.startsWith("#")) { // a step in the history too, so Back returns to the link
    rememberScroll();
    history.pushState({ path: S.path, hash: href.slice(1) }, "", "?p=" + encodeURI(S.path) + href);
    scrollToHeading(href.slice(1));
    drawNav();
    return;
  }
  const { path, hash, dir } = resolveHref(S.path, href);
  if (path.endsWith(".md")) {
    if (S.fileSet.has(path)) return openDoc(path, { hash });
    // A link to a page not written yet is a to-do: write it, point it at the page meant, or unlink it.
    if (path.startsWith(GDD + "/")) return deadLinkDialog(path, text);
    toast("No such page: " + path, "bad");
    return;
  }
  const page = folderPage(path);
  if (dir || page) {
    if (page) return openDoc(page);
    S.open.add(path); renderTree(); toast("Folder " + path);
    return;
  }
  window.open("/repo/" + path, "_blank");
}

// ----------------------------------------------------------------------------- finding text

function clearHits() { if (window.CSS?.highlights) { CSS.highlights.delete("codex-hit"); CSS.highlights.delete("codex-current"); } }
function findInPage(q, occurrence = 0) {
  clearHits();
  if (!q || !window.CSS?.highlights) return;
  const root = $("#page .ProseMirror");
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const needle = q.toLowerCase();
  const ranges = [];
  for (let n = walker.nextNode(); n; n = walker.nextNode()) {
    const text = n.data.toLowerCase();
    for (let i = text.indexOf(needle); i >= 0; i = text.indexOf(needle, i + needle.length)) {
      const r = new Range(); r.setStart(n, i); r.setEnd(n, i + needle.length); ranges.push(r);
    }
  }
  if (!ranges.length) return;
  const cur = ranges[Math.min(occurrence, ranges.length - 1)];
  CSS.highlights.set("codex-hit", new Highlight(...ranges));
  CSS.highlights.set("codex-current", new Highlight(cur));
  const sc = $("#scroller");
  sc.scrollTop += cur.getBoundingClientRect().top - sc.getBoundingClientRect().top - sc.clientHeight / 3;
}

// ----------------------------------------------------------------------------- the toolbar

const I = {
  bold: '<b style="font-size:15px">B</b>', italic: '<i style="font-family:Georgia;font-size:15px">I</i>', strike: '<s style="font-size:14px">S</s>',
  code: '<svg viewBox="0 0 24 24"><path d="m8 7-5 5 5 5M16 7l5 5-5 5"/></svg>',
  link: '<svg viewBox="0 0 24 24"><path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/></svg>',
  ul: '<svg viewBox="0 0 24 24"><path d="M9 6h11M9 12h11M9 18h11"/><circle cx="4.5" cy="6" r="1" fill="currentColor"/><circle cx="4.5" cy="12" r="1" fill="currentColor"/><circle cx="4.5" cy="18" r="1" fill="currentColor"/></svg>',
  ol: '<svg viewBox="0 0 24 24"><path d="M10 6h10M10 12h10M10 18h10M4 5l1.5-1v5M3.5 13.5a1.5 1.5 0 1 1 2.5 1L3.5 17H6.5"/></svg>',
  task: '<svg viewBox="0 0 24 24"><rect x="3" y="4" width="6" height="6" rx="1"/><path d="m4.5 7 1 1 2-2M12 7h9M3 15h6v6H3zM12 18h9"/></svg>',
  quote: '<svg viewBox="0 0 24 24"><path d="M5 7v10M9 9h11M9 13h11M9 17h7"/></svg>',
  codeblock: '<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="m9 10-2 2 2 2M15 10l2 2-2 2"/></svg>',
  hr: '<svg viewBox="0 0 24 24"><path d="M3 12h18M7 7h10M7 17h10" opacity=".5"/><path d="M3 12h18"/></svg>',
  table: '<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 10h18M3 15h18M9 4v16M15 4v16"/></svg>',
  image: '<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="9" cy="10" r="2"/><path d="m4 19 6-6 4 4 3-3 3 3"/></svg>',
  undo: '<svg viewBox="0 0 24 24"><path d="M9 14 4 9l5-5"/><path d="M4 9h11a5 5 0 0 1 0 10h-3"/></svg>',
  redo: '<svg viewBox="0 0 24 24"><path d="m15 14 5-5-5-5"/><path d="M20 9H9a5 5 0 0 0 0 10h3"/></svg>',
  rowAdd: '<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="8" rx="1"/><path d="M12 15v6M9 18h6"/></svg>',
  colAdd: '<svg viewBox="0 0 24 24"><rect x="3" y="3" width="8" height="18" rx="1"/><path d="M15 12h6M18 9v6"/></svg>',
  rowDel: '<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="8" rx="1"/><path d="M9 18h6"/></svg>',
  colDel: '<svg viewBox="0 0 24 24"><rect x="3" y="3" width="8" height="18" rx="1"/><path d="M15 12h6"/></svg>',
  tableDel: '<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="m9 9 6 6M15 9l-6 6"/></svg>',
  clear: '<svg viewBox="0 0 24 24"><path d="M6 5h12M12 5l-3 14M4 19h8M15 14l5 5M20 14l-5 5"/></svg>',
};

const TOOLS = [
  { type: "select" },
  { sep: true },
  { id: "bold", icon: I.bold, title: "Bold (Ctrl+B)", run: (c) => c.toggleBold(), active: (e) => e.isActive("bold") },
  { id: "italic", icon: I.italic, title: "Italic (Ctrl+I)", run: (c) => c.toggleItalic(), active: (e) => e.isActive("italic") },
  { id: "strike", icon: I.strike, title: "Strikethrough", run: (c) => c.toggleStrike(), active: (e) => e.isActive("strike") },
  { id: "code", icon: I.code, title: "Inline code (Ctrl+E is Edit; use `)", run: (c) => c.toggleCode(), active: (e) => e.isActive("code") },
  { id: "link", icon: I.link, title: "Link (Ctrl+K)", run: () => linkDialog(), active: (e) => e.isActive("link"), raw: true },
  { id: "clear", icon: I.clear, title: "Clear formatting", run: (c) => c.unsetAllMarks() },
  { sep: true },
  { id: "ul", icon: I.ul, title: "Bulleted list", run: (c) => c.toggleBulletList(), active: (e) => e.isActive("bulletList") },
  { id: "ol", icon: I.ol, title: "Numbered list", run: (c) => c.toggleOrderedList(), active: (e) => e.isActive("orderedList") },
  { id: "task", icon: I.task, title: "Checklist", run: (c) => c.toggleTaskList(), active: (e) => e.isActive("taskList") },
  { id: "quote", icon: I.quote, title: "Quote", run: (c) => c.toggleBlockquote(), active: (e) => e.isActive("blockquote") },
  { id: "codeblock", icon: I.codeblock, title: "Code block", run: (c) => c.toggleCodeBlock(), active: (e) => e.isActive("codeBlock") },
  { id: "hr", icon: I.hr, title: "Divider", run: (c) => c.setHorizontalRule() },
  { id: "table", icon: I.table, title: "Insert a table", run: (c) => c.insertTable({ rows: 3, cols: 3, withHeaderRow: true }) },
  { id: "image", icon: I.image, title: "Insert an image", run: () => imageDialog(), raw: true },
  { type: "tableOps" },
  { sep: true, push: true },
  { id: "undo", icon: I.undo, title: "Undo (Ctrl+Z)", run: (c) => c.undo(), enabled: (e) => e.can().undo() },
  { id: "redo", icon: I.redo, title: "Redo (Ctrl+Y)", run: (c) => c.redo(), enabled: (e) => e.can().redo() },
];
const TABLE_OPS = [
  { icon: I.rowAdd, title: "Add a row below", run: (c) => c.addRowAfter() },
  { icon: I.colAdd, title: "Add a column to the right", run: (c) => c.addColumnAfter() },
  { icon: I.rowDel, title: "Delete this row", run: (c) => c.deleteRow() },
  { icon: I.colDel, title: "Delete this column", run: (c) => c.deleteColumn() },
  { icon: I.tableDel, title: "Delete the table", run: (c) => c.deleteTable() },
];
const BLOCKS = [["p", "Paragraph"], ["1", "Title"], ["2", "Heading"], ["3", "Subheading"], ["4", "Heading 4"], ["5", "Heading 5"]];

function buildToolbar() {
  const bar = $("#toolbar");
  const btn = (t) => h("button", {
    class: "tb", title: t.title, "data-id": t.id || null, html: t.icon,
    onmousedown: (e) => e.preventDefault(),
    onclick: () => { if (t.raw) t.run(); else t.run(S.editor.chain().focus()).run(); },
  });
  for (const t of TOOLS) {
    if (t.sep) { bar.append(h("span", { class: "tb-sep", style: t.push ? "margin-left:auto" : null })); continue; }
    if (t.type === "select") {
      const sel = h("select", { class: "field tb-select", id: "blockType", title: "Paragraph style" }, BLOCKS.map(([v, l]) => h("option", { value: v }, l)));
      sel.addEventListener("change", () => {
        const c = S.editor.chain().focus();
        (sel.value === "p" ? c.setParagraph() : c.setHeading({ level: Number(sel.value) })).run();
      });
      bar.append(sel);
      continue;
    }
    if (t.type === "tableOps") {
      bar.append(h("span", { class: "tb-group table-ops hidden", id: "tableOps" }, h("span", { class: "tb-label" }, "TABLE"), TABLE_OPS.map(btn)));
      continue;
    }
    bar.append(btn(t));
  }
}

function updateToolbar() {
  if (!S.editor || S.mode !== "edit") return;
  const e = S.editor;
  for (const t of TOOLS) {
    if (!t.id) continue;
    const b = $(`.tb[data-id="${t.id}"]`);
    if (!b) continue;
    if (t.active) b.classList.toggle("on", t.active(e));
    if (t.enabled) b.disabled = !t.enabled(e);
  }
  const sel = $("#blockType");
  const level = [1, 2, 3, 4, 5, 6].find((l) => e.isActive("heading", { level: l }));
  sel.value = level ? String(Math.min(level, 5)) : "p";
  $("#tableOps").classList.toggle("hidden", !e.isActive("table"));
}

// ----------------------------------------------------------------------------- dialogs

/**
 * The first time the Codex opens (PLAN.md, step 5): sign in with Discord, to edit and comment under
 * your name, and free AI through OpenRouter when Claude isn't on this computer. Each can wait.
 */
async function welcome() {
  if (localStorage.getItem("codex.welcomed")) return;
  localStorage.setItem("codex.welcomed", "1");
  const [acc, aiState] = await Promise.all([api("/api/account"), api("/api/ai")]);
  const needDiscord = !acc.signedIn, needAi = aiState.provider?.needsSignIn === "openrouter";
  if (!needDiscord && !needAi) return;
  const step = (title, text, label, go) => h("div", { class: "welcome-step" },
    h("div", {}, h("b", {}, title), h("p", {}, text)),
    h("button", { class: "btn btn-gold", onclick: go }, label));
  const close = modal("Welcome to the R'th Codex", [
    h("p", { class: "welcome-lead" }, "The game design document of R'th, grown page by page: read it, write it, comment and talk it through." + (needDiscord && needAi ? " Two things make it yours:" : "")),
    needDiscord ? step("Sign in with Discord", acc.contributor ? "To edit pages and comment under your name. You need the Codex role in the R'th Discord." : "To comment under your name, and send your changes without a GitHub account.", "Sign in", async () => {
      const r = await api("/api/account/signin", { method: "POST" });
      if (r.url) window.open(r.url, "_blank");
    }) : null,
    needAi ? step("Free AI", "R'th AI isn't set up on this computer yet. Sign in with OpenRouter and it runs on free models, about 50 messages a day.", "Sign in with OpenRouter", async () => {
      const r = await api("/api/openrouter/signin", { method: "POST" });
      if (r.url) window.open(r.url, "_blank");
    }) : null,
    h("div", { class: "modal-actions" }, h("button", { class: "btn", onclick: () => close() }, "Not now")),
  ], { width: "min(520px, 94vw)" });
}

function modal(title, body, { onClose, width, cls } = {}) { // cls: a class of its own (the link window)
  const host = $("#modalHost");
  const close = () => { host.replaceChildren(); document.removeEventListener("keydown", onKey, true); onClose && onClose(); };
  const onKey = (e) => { if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); close(); } };
  document.addEventListener("keydown", onKey, true);
  const win = h("div", { class: "window modal" + (cls ? " " + cls : ""), style: width ? `width:${width}` : null },
    h("div", { class: "titlebar" }, h("span", { class: "ico ico-scripture" }), h("span", { class: "win-title" }, title), h("button", { class: "close-x", title: "Close (Esc)", onclick: close }, "✕")),
    h("div", { class: "win-body" }, body));
  const back = h("div", { class: "backdrop", onmousedown: (e) => { if (e.target === back) close(); } }, win);
  host.replaceChildren(back);
  return close;
}

function ask(title, html, buttons) {
  return new Promise((resolve) => {
    let done = false;
    const finish = (k) => { if (done) return; done = true; close(); resolve(k); };
    const close = modal(title, [h("p", { class: "modal-text", html }),
      h("div", { class: "modal-actions" }, buttons.map((b) => h("button", { class: "btn " + (b.cls || ""), onclick: () => finish(b.key) }, b.label)))],
    { onClose: () => { if (!done) { done = true; resolve(null); } } });
    setTimeout(() => $(".modal .btn-gold")?.focus(), 0);
  });
}

/**
 * Add or change a link (Ctrl+K). The target is picked from the GDD's pages, never typed as a path, so a
 * link can't be misspelt into nowhere. A web address or a #heading is typed and offered as itself; a
 * page that isn't written yet can be linked on purpose, as a to-do in this page's folder.
 */
function linkDialog() {
  const e = S.editor;
  const { from, to, empty } = e.state.selection;
  const current = e.getAttributes("link").href || "";
  const text = h("input", { class: "field", placeholder: "Link text", value: empty ? "" : e.state.doc.textBetween(from, to, " ") });
  const apply = (href, title = "") => {
    close();
    const c = e.chain().focus();
    if (!href) { c.extendMarkRange("link").unsetLink().run(); return; }
    if (empty && !e.isActive("link")) {
      const label = text.value.trim() || title || href.split("/").pop().replace(/\.md$/, "");
      c.insertContent({ type: "text", text: label, marks: [{ type: "link", attrs: { href } }] }).run();
    } else c.extendMarkRange("link").setLink({ href }).run();
  };
  const slugOfText = (t) => t.trim().normalize("NFKD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/['’]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
  const pages = S.files.filter((f) => f.path !== S.path && !f.path.startsWith(TEMPLATES + "/"))
    .map((f) => ({ path: f.path, title: f.title || f.path.split("/").pop().replace(/\.md$/, ""), note: f.path.slice(GDD.length + 1).split("/").slice(0, -1).join("/") }));
  const picker = pagePicker({
    h, pages, placeholder: "Find a page, or type https://… or #a-heading", inline: true,
    onPick: (p) => apply(relativePath(S.path, p.path), p.title),
    create: {
      label: (q) => (/^(https?:|mailto:|#)/i.test(q) ? `Link to ${q}` : `Link to “${q}”, not written yet (a to-do)`),
      run: (q) => apply(/^(https?:|mailto:|#)/i.test(q) ? q : slugOfText(q) + ".md", q),
    },
  });
  const now = current ? resolveHref(S.path, current) : null;
  const nowPage = now && S.files.find((f) => f.path === now.path);
  const close = modal(current ? "Change link" : "Add a link", [
    current ? h("p", { class: "muted small" }, "Now: ", nowPage ? h("b", {}, nowPage.title) : current, nowPage ? ", " + current : "") : null,
    h("label", {}, "Where to"), picker,
    empty && !current ? [h("label", { style: "margin-top:12px" }, "Text"), text] : null,
    h("div", { class: "modal-actions", style: "margin-top:14px" },
      current ? h("button", { class: "btn btn-danger", onclick: () => { close(); e.chain().focus().extendMarkRange("link").unsetLink().run(); } }, "Remove link") : null,
      h("button", { class: "btn", onclick: () => close() }, "Cancel")),
  ], { width: "min(680px, 94vw)", cls: "link-modal" });
  setTimeout(() => picker.querySelector("input")?.focus(), 0);
}

/**
 * A link to a page that isn't written yet, clicked: write it, point it at the page that's meant, or keep
 * the words and drop the link. Pointing and unlinking change every link to it on this page.
 */
function deadLinkDialog(path, text) {
  const name = text.trim() || path.split("/").pop().replace(/\.md$/, "").replace(/-/g, " ");
  const picker = pagePicker({
    h, pages: S.files.filter((f) => f.path.startsWith(GDD + "/") && !isMeta(f.path)).map((f) => ({ path: f.path, title: f.title, note: f.path.slice(GDD.length + 1).split("/").slice(0, -1).join("/") })),
    placeholder: "The page this link means…",
    onPick: (p) => { close(); relinkOpenPage(path, p.path); toast(`Now links to “${p.title}”`, "good"); },
  });
  const close = modal("Not written yet", [
    h("p", { class: "modal-text" }, h("b", {}, name), " has no page yet (", path.slice(GDD.length + 1), "). A link to a page that isn't written is a to-do, not a mistake."),
    h("div", { class: "dl-options" },
      h("div", { class: "dl-option" }, h("div", {}, h("b", {}, "Write it"), h("p", { class: "muted small" }, "Make the page now, as a stub.")),
        h("button", { class: "btn btn-gold", onclick: () => { close(); newPageDialog(null, { path, title: text.trim() }); } }, "Write it")),
      h("div", { class: "dl-option" }, h("div", {}, h("b", {}, "It's another page"), h("p", { class: "muted small" }, "Point the link at the page that's meant."), picker)),
      h("div", { class: "dl-option" }, h("div", {}, h("b", {}, "Unlink"), h("p", { class: "muted small" }, "Keep the words, drop the link.")),
        h("button", { class: "btn", onclick: () => { close(); unlinkOpenPage(path); } }, "Unlink"))),
  ], { width: "min(560px, 94vw)" });
}

/** Links on the open page to `path` become plain text. */
function unlinkOpenPage(path) {
  const ed = S.editor;
  if (!ed) return;
  const link = ed.schema.marks.link;
  const tr = ed.state.tr;
  ed.state.doc.descendants((n, pos) => {
    const m = n.isText && n.marks.find((x) => x.type === link);
    if (m && resolveHref(S.path, m.attrs.href).path === path) tr.removeMark(pos, pos + n.nodeSize, link);
  });
  if (tr.docChanged) { ed.view.dispatch(tr); scheduleDirty(); }
}

function imageDialog() {
  const src = h("input", { class: "field", placeholder: "images/picture.png (relative to this page)", spellcheck: "false" });
  const alt = h("input", { class: "field", placeholder: "What it shows" });
  const apply = () => {
    close();
    if (src.value.trim()) S.editor.chain().focus().setImage({ src: src.value.trim(), alt: alt.value.trim() }).run();
  };
  const close = modal("Insert an image", [h("label", {}, "Image"), src, h("label", {}, "Description"), alt,
    h("div", { class: "modal-actions" }, h("button", { class: "btn", onclick: () => close() }, "Cancel"), h("button", { class: "btn btn-gold", onclick: apply }, "Insert"))]);
  alt.addEventListener("keydown", (ev) => { if (ev.key === "Enter") apply(); });
  setTimeout(() => src.focus(), 0);
}


// One search box for everything (Ctrl+P, Ctrl+Shift+F or the box at the top), as in VS Code or
// Notion: pages whose name or title match come first, then the lines in pages that contain the words.
function quickOpen() {
  const input = h("input", { class: "field qo-input", placeholder: "Search pages and text", spellcheck: "false" });
  const list = h("div", { class: "qo-list" });
  let items = [], sel = 0, seq = 0, timer = null, text = { q: "", results: [], total: 0, loading: false };

  // Lines as they read on the page: no Markdown marks, table cells joined.
  const tidy = (t) => t.replace(/!?\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]/g, "")
    .replace(/^\s*(?:#{1,6}|[-+*]|\d+[.)]|>)\s+/, "").replace(/^\s*\|\s*|\s*\|\s*$/g, "").replace(/\s*\|\s*/g, ", ").trim();
  const titleOf = (p) => S.files.find((f) => f.path === p)?.title || p.split("/").pop().replace(/\.md$/, "");
  const mark = (t, q) => {
    if (!q) return t;
    const out = [], lower = t.toLowerCase(), n = q.toLowerCase();
    let i = 0;
    for (let j = lower.indexOf(n); j >= 0; j = lower.indexOf(n, i)) { out.push(t.slice(i, j), h("b", {}, t.slice(j, j + n.length))); i = j + n.length; }
    out.push(t.slice(i));
    return out;
  };
  const clip = (t, q) => { const at = t.toLowerCase().indexOf(q.toLowerCase()); return at > 60 ? "…" + t.slice(at - 40) : t; };

  const render = () => {
    const raw = input.value.trim();
    // A page matches when every word typed starts a word of its title or file name (no scattered letters).
    const words = raw.toLowerCase().split(/\s+/).filter(Boolean);
    const score = (f) => {
      if (!words.length) return 0;
      const title = (f.title || "").toLowerCase(), name = f.path.toLowerCase();
      const starts = (t) => words.every((w) => new RegExp("(^|[^\\p{L}\\p{N}])" + w.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "u").test(t));
      if (title.startsWith(raw.toLowerCase())) return 4;
      if (starts(title)) return 3;
      if (starts(name)) return 2;
      return -1;
    };
    // Templates aren't pages to find, here as in the page list and the text search (they work behind New page).
    const pages = S.files.filter((f) => !f.path.startsWith(TEMPLATES + "/")).map((f) => ({ f, s: score(f) }))
      .filter((x) => x.s >= 0).sort((a, b) => b.s - a.s || collator.compare(a.f.path, b.f.path)).slice(0, raw ? 8 : 40);
    items = pages.map((x) => ({ kind: "page", path: x.f.path, el: h("div", { class: "qo-item" },
      h("span", { class: "ico ico-note qo-ico" }), h("span", { class: "qo-title" }, x.f.title || x.f.path.split("/").pop()), h("span", { class: "qo-path" }, x.f.path)) }));
    const rows = [];
    if (items.length) rows.push(h("div", { class: "qo-head" }, raw ? "Pages" : "All pages"));
    rows.push(...items.map((x) => x.el));
    if (raw.length >= 2 && text.q === raw) {
      const hits = [];
      for (const res of text.results) {
        const seen = new Set();
        for (const hit of res.hits) {
          if (seen.has(hit.line)) continue;
          seen.add(hit.line);
          hits.push({ kind: "text", path: res.path, find: { q: raw, occurrence: hit.occurrence }, el: h("div", { class: "qo-item qo-text" },
            h("span", { class: "qo-title" }, titleOf(res.path)), h("span", { class: "qo-snippet" }, mark(clip(tidy(hit.text), raw), raw))) });
        }
      }
      rows.push(h("div", { class: "qo-head" }, hits.length ? `In the text (${text.total} matches in ${text.results.length} pages)` : "In the text: nothing found"));
      items.push(...hits.slice(0, 80));
      rows.push(...hits.slice(0, 80).map((x) => x.el));
    } else if (raw.length >= 2) rows.push(h("div", { class: "qo-head" }, "Searching the text…"));
    // Ask the AI instead: last, or first when it reads as a question (so Enter asks).
    if (raw.length >= 3) {
      const ask = { kind: "ask", q: raw, el: h("div", { class: "qo-item qo-ask" }, h("span", { class: "qo-ask-ico" }, "✦"), h("span", { class: "qo-title" }, "Ask AI: "), h("span", { class: "qo-snippet" }, raw)) };
      const question = /\?\s*$/.test(raw) || /^(how|what|why|when|where|who|which|does|do|is|are|can|should|could|would)\b/i.test(raw);
      if (question) { items.unshift(ask); rows.unshift(ask.el); } else { items.push(ask); rows.push(ask.el); }
    }
    items.forEach((x, i) => {
      x.el.classList.toggle("sel", i === sel);
      x.el.onmousemove = () => { if (sel !== i) { sel = i; select(); } };
      x.el.onclick = () => go(i);
    });
    sel = Math.min(sel, Math.max(0, items.length - 1));
    list.replaceChildren(...rows);
    select();
  };
  const select = () => items.forEach((x, i) => { x.el.classList.toggle("sel", i === sel); if (i === sel) x.el.scrollIntoView({ block: "nearest" }); });
  const go = (i) => {
    const x = items[i];
    if (!x) return;
    close();
    if (x.kind === "ask") return ai.ask(x.q);
    openDoc(x.path, x.kind === "text" ? { find: x.find } : {});
  };
  const searchText = async () => {
    const raw = input.value.trim(), mine = ++seq;
    if (raw.length < 2) return;
    const r = await api("/api/search?q=" + encodeURIComponent(raw));
    if (mine !== seq) return;
    text = { q: raw, results: r.results || [], total: r.total || 0 };
    render();
  };
  input.addEventListener("input", () => { sel = 0; render(); clearTimeout(timer); timer = setTimeout(searchText, 200); });
  input.addEventListener("keydown", (e) => {
    if (e.key === "ArrowDown") { sel = Math.min(sel + 1, items.length - 1); select(); e.preventDefault(); }
    else if (e.key === "ArrowUp") { sel = Math.max(sel - 1, 0); select(); e.preventDefault(); }
    else if (e.key === "Enter") { go(sel); e.preventDefault(); }
  });
  const close = modal("Search", [input, list], { width: "min(720px, 94vw)" });
  render();
  setTimeout(() => input.focus(), 0);
}

/**
 * A new page. The type comes first, by a picker: it decides the folder and the core headings (How the
 * GDD Works, "Types"). Then the title, and optionally a few components: the ones usual for the type are
 * offered, none ticked, since most pages start as stubs. Made from a folder, the folder's type is
 * suggested. `prefill`: { title, path } from a link to a page not written yet: the path is kept when the
 * type picked is its folder's, else the page goes to the picked type's folder and the link follows it.
 */
function newPageDialog(inFolder = null, prefill = {}) {
  if (typeof inFolder !== "string") inFolder = null; // a button's click passes its event
  if (!S.types.length) { toast("No types found: How the GDD Works needs its Types table", "bad"); return; }
  const typeOf = (dir) => typeAt(S.types, dir);
  const fixed = prefill.path || null;
  const suggested = typeOf(fixed && dirOf(fixed)) || typeOf(inFolder) || null;
  const an = (w) => (/^[aeiou]/.test(w) ? "an " : "a ") + w;
  const cap = (w) => w.replace(/^./, (c) => c.toUpperCase());
  let type = null, title = prefill.title || "";
  const picked = new Set();
  const body = h("div", { class: "np" });

  // Step 1: the type.
  const pickType = () => {
    const filter = h("input", { class: "field", placeholder: "Filter: monster, sword, town…", spellcheck: "false" });
    const grid = h("div", { class: "np-types" });
    const draw = () => {
      const q = filter.value.trim().toLowerCase();
      const shown = S.types.filter((t) => !q || t.type.includes(q) || t.covers.toLowerCase().includes(q));
      grid.replaceChildren(...shown.map((t) => h("button", { class: "np-type" + (t === suggested ? " suggested" : ""), title: t.covers, onclick: () => choose(t) },
        h("span", { class: "ico ico-" + (TYPE_ICONS[t.type] || "journal") }),
        h("span", { class: "np-type-name" }, cap(t.type), t === suggested ? h("span", { class: "np-type-tag" }, fixed ? "from the link" : "this folder") : null),
        h("span", { class: "np-type-ex" }, t.examples))));
    };
    filter.addEventListener("input", draw);
    filter.addEventListener("keydown", (e) => {
      if (e.key === "Enter") { e.preventDefault(); (grid.querySelector(".np-type.suggested") || grid.querySelector(".np-type"))?.click(); }
      if (e.key === "ArrowDown") { e.preventDefault(); grid.querySelector(".np-type")?.focus(); }
    });
    grid.addEventListener("keydown", (e) => { // arrows move through the grid, two columns
      const all = [...grid.querySelectorAll(".np-type")], i = all.indexOf(document.activeElement);
      const step = { ArrowRight: 1, ArrowLeft: -1, ArrowDown: 2, ArrowUp: -2 }[e.key];
      if (i < 0 || !step) return;
      e.preventDefault();
      if (i + step < 0) filter.focus(); else all[Math.min(all.length - 1, i + step)]?.focus();
    });
    draw();
    body.replaceChildren(
      h("p", { class: "muted small np-lead" }, fixed ? "A link points here, but the page isn't written yet. What kind of page is it?" : "What kind of page is it? The type decides its folder and its headings."),
      filter, grid);
    setTimeout(() => (suggested ? grid.querySelector(".np-type.suggested") : filter)?.focus(), 0);
  };

  // Step 2: the title, and components if wanted.
  const fileName = () => title.trim().replace(/\.md$/i, "").normalize("NFKD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/['’]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
  const keepFixed = () => fixed && typeOf(dirOf(fixed)) === type;
  const target = () => (keepFixed() ? fixed
    : fixed ? folderPath(type) + "/" + fixed.split("/").pop()
    : fileName() ? (inFolder && typeOf(inFolder) === type ? inFolder : folderPath(type)) + "/" + fileName() + ".md" : null);
  const choose = (t) => {
    type = t;
    const name = h("input", { class: "field", placeholder: "The page's title", spellcheck: "true", value: title });
    const file = h("div", { class: "muted small new-file" });
    const showFile = () => { const p = target(); file.textContent = p ? `Saved as ${p.slice(GDD.length + 1)}, a stub` : ""; };
    name.addEventListener("input", () => { title = name.value; showFile(); });
    name.addEventListener("keydown", (e) => { if (e.key === "Enter") create(); });
    const usual = S.components.filter((c) => c.foundOn.includes(t.type));
    const chip = (c) => h("button", { class: "np-comp" + (picked.has(c.name) ? " on" : ""), title: c.answers,
      onclick: (e) => { if (picked.has(c.name)) picked.delete(c.name); else picked.add(c.name); e.currentTarget.classList.toggle("on", picked.has(c.name)); } },
      cap(c.name.replace(/-/g, " ")));
    body.replaceChildren(...[
      h("div", { class: "np-chosen" }, h("span", { class: "gdd-badge gdd-type-badge" }, h("span", { class: "ico ico-" + (TYPE_ICONS[t.type] || "journal") }), cap(t.type)),
        h("span", { class: "muted small np-heads" }, t.headings.join(", ")),
        h("button", { class: "gdd-link np-back", onclick: pickType }, "Change")),
      h("label", {}, "Title"), name, file,
      usual.length ? [h("label", { class: "np-comps-label" }, "Sections to start with ", h("span", { class: "np-optional" }, "optional")),
        h("div", { class: "np-comps" }, usual.map(chip)),
        h("p", { class: "muted small np-note" }, "Most pages start as a stub with none. Add them later in Page settings.")] : null,
      fixed && !keepFixed() ? h("p", { class: "muted small np-note" }, `The link points into ${dirOf(fixed).slice(GDD.length + 1) || "the top of the GDD"}; ${an(t.type)} page goes in ${t.folder || "the top"}, and the link on this page will follow it.`) : null,
      h("div", { class: "modal-actions" }, h("button", { class: "btn", onclick: () => close() }, "Cancel"), h("button", { class: "btn btn-gold", onclick: create }, "Create")),
    ].flat().filter(Boolean));
    showFile();
    setTimeout(() => { name.focus(); name.setSelectionRange(name.value.length, name.value.length); }, 0);
  };

  const create = async () => {
    const path = target();
    if (!path || !title.trim()) { body.querySelector("input.field")?.focus(); return; }
    // File names are unique across the whole GDD (Workflow, "Naming").
    const same = S.files.find((f) => f.path !== path && !f.path.startsWith(TEMPLATES + "/") && f.path.split("/").pop() === path.split("/").pop());
    if (same) { toast(`${path.split("/").pop()} is already ${same.title || same.path}: file names are unique across the GDD`, "bad"); return; }
    if (S.fileSet.has(path)) { toast(`There's already a page at ${path.slice(GDD.length + 1)}`, "bad"); return; }
    const r = await api("/api/new", { method: "POST", body: JSON.stringify({ path, title: title.trim(), type: type.type, components: [...picked] }) });
    if (!r.ok) { toast(failed("Couldn't make the page", r), "bad"); return; }
    close();
    if (fixed && r.path !== fixed) relinkOpenPage(fixed, r.path); // the link that asked for it follows it to its folder
    await loadTree();
    await openDoc(r.path, { mode: "edit" });
    focusEditor(S.editor.state.doc.content.size); // ready to type, as a new document is
    toast("Page created", "good");
  };

  const close = modal(fixed ? "Write this page" : "New page", body, { width: "min(620px, 94vw)" });
  // Made from a folder of a type, the page is that type (Change still picks another); otherwise the type comes first.
  if (inFolder && typeOf(inFolder) && !fixed) choose(typeOf(inFolder)); else pickType();
}

/** Links on the open page to `from` now point to `to` (a page made somewhere else than the link said). */
function relinkOpenPage(from, to) {
  const ed = S.editor;
  if (!ed) return;
  const link = ed.schema.marks.link;
  const tr = ed.state.tr;
  ed.state.doc.descendants((n, pos) => {
    const m = n.isText && n.marks.find((x) => x.type === link);
    if (!m || resolveHref(S.path, m.attrs.href).path !== from) return;
    const hash = m.attrs.href.includes("#") ? m.attrs.href.slice(m.attrs.href.indexOf("#")) : "";
    tr.removeMark(pos, pos + n.nodeSize, link).addMark(pos, pos + n.nodeSize, link.create({ ...m.attrs, href: relativePath(S.path, to) + hash }));
  });
  if (tr.docChanged) { ed.view.dispatch(tr); scheduleDirty(); }
}

function helpDialog() {
  const k = (keys, what) => [h("span", {}, keys.split(" ").map((x) => h("kbd", { style: "margin-right:3px" }, x))), h("span", {}, what)];
  modal("Keys", h("div", { class: "keys" },
    k("Ctrl P", "Search: pages by name, then the text of every page"),
    k("Ctrl M", "Comment on the selected text"), k("Ctrl Enter", "Post a comment. Write @agent in it to leave it for the next agent session"),
    k("Ctrl J", "Ask the AI (with the selected text, if any)"), k("Ctrl Shift C", "Copy for Discord: the selection, or the section you're in, with a link back"),
    k("Ctrl K", "Add or edit a link"), k("Ctrl B", "Bold"), k("Ctrl I", "Italic"), k("Ctrl Z", "Undo"),
    k("Ctrl S", "Save now (pages also save by themselves)"), k("Alt ←", "Back to where you were (also the mouse's back button)"), k("Alt →", "Forward again"),
    k("Ctrl ,", "Show or hide the pages"), k("Ctrl .", "Show or hide the outline, links and AI"),
    h("span", { class: "muted small", style: "grid-column:1/-1;margin-top:8px" },
      "Markdown shortcuts work as you type: ## for a heading, - or 1. for a list, > for a quote, ``` for code. Saving only rewrites the paragraphs you changed; the rest of the file stays exactly as written.")));
}

// ----------------------------------------------------------------------------- toasts

/**
 * A short message at the bottom. With actions ([{ label, run }]) it stays longer (ms), has buttons, and
 * an ✕; running an action closes it.
 */
// Toasts: a deck at the bottom centre, newest in front, older ones tucked behind (at most three show).
// Pointing at the deck spreads it out and holds every timer; the same message again only counts (×2).
const TOASTS = [];
let toastsHeld = false;
function toast(text, kind = "", { actions = null, ms = null } = {}) {
  const same = !actions && TOASTS.find((x) => !x.actions && x.text === text && x.kind === kind);
  if (same) { same.n++; same.count.textContent = "×" + same.n; startToast(same); return same.el; }
  const x = { text, kind, actions, n: 1, ms: ms || (actions ? 12000 : 2600) };
  x.count = h("span", { class: "toast-n" });
  x.el = h("div", { class: "toast " + kind, onmouseenter: () => holdToasts(true), onmouseleave: () => setTimeout(() => holdToasts(!!document.querySelector(".toast:hover")), 80) },
    h("span", { class: "toast-ico" }, kind === "good" ? "✓" : kind === "bad" ? "!" : "✦"),
    h("span", { class: "toast-text" }, text), x.count,
    actions ? actions.map((a) => h("button", { class: "toast-btn", onclick: () => { closeToast(x); a.run(); } }, a.label)) : null,
    actions ? h("button", { class: "toast-x", title: "Close", onclick: () => closeToast(x) }, "✕") : null);
  TOASTS.unshift(x);
  $("#toasts").append(x.el);
  startToast(x);
  return x.el;
}
function startToast(x) { clearTimeout(x.timer); if (!toastsHeld) x.timer = setTimeout(() => closeToast(x), x.ms); stackToasts(); }
function closeToast(x) {
  clearTimeout(x.timer);
  const i = TOASTS.indexOf(x);
  if (i < 0) return;
  TOASTS.splice(i, 1);
  x.el.classList.add("out");
  setTimeout(() => x.el.remove(), 220);
  stackToasts();
}
function holdToasts(on) {
  if (on === toastsHeld) return;
  toastsHeld = on;
  for (const x of TOASTS) { clearTimeout(x.timer); if (!on) x.timer = setTimeout(() => closeToast(x), Math.min(x.ms, 2600)); }
  stackToasts();
}
function stackToasts() {
  let y = 0;
  const front = TOASTS[0]?.el.offsetHeight || 0;
  TOASTS.forEach((x, i) => {
    const el = x.el;
    el.style.zIndex = String(100 - i);
    // Collapsed, each one behind shows its top edge a clear step above the one in front (whatever their
    // heights) and is a little smaller and dimmer, so the deck shows there are more.
    const scale = 1 - i * 0.04, lift = front + i * 10 - el.offsetHeight * scale;
    el.style.transform = toastsHeld ? `translate(-50%, ${-y}px)` : `translate(-50%, ${-lift}px) scale(${scale})`;
    el.style.opacity = toastsHeld || i < 3 ? "" : "0";
    el.style.filter = toastsHeld || i === 0 ? "" : `brightness(${1 - i * 0.22})`; // darker, but solid: see-through would show the next one's words
    el.style.pointerEvents = toastsHeld || i === 0 ? "auto" : "none";
    y += el.offsetHeight + 8;
  });
}

// ----------------------------------------------------------------------------- others' changes

// Sync brought in what others sent (src/sync.js, "news"): say who made, changed or deleted which pages,
// with a button to open each (as Google Docs and Notion say who changed what).
function showNews({ pages }) {
  refreshSpecialCounts();
  const who = (x) => x.by?.length ? x.by.join(" and ") : "Someone";
  const title = (x) => "“" + x.title + "”";
  for (const x of pages.filter((p) => p.status === "added").slice(0, 3))
    toast(`${who(x)} made a new page: ${title(x)}`, "", { actions: [{ label: "Open", run: () => openDoc(x.path) }] });
  for (const x of pages.filter((p) => p.status === "deleted").slice(0, 3)) toast(`${who(x)} deleted ${title(x)}`, "", { actions: [] });
  const changed = pages.filter((p) => p.status === "changed" && p.path !== S.path);
  if (changed.length === 1) toast(`${who(changed[0])} changed ${title(changed[0])}`, "", { actions: [{ label: "Open", run: () => openDoc(changed[0].path) }], ms: 8000 });
  else if (changed.length > 1) {
    const names = [...new Set(changed.flatMap((x) => x.by || []))];
    toast(`${names.length ? names.join(" and ") : "Others"} changed ${changed.length} pages: ${changed.slice(0, 3).map(title).join(", ")}${changed.length > 3 ? " and more" : ""}`, "", { actions: changed.slice(0, 3).map((x) => ({ label: x.title.length > 18 ? x.title.slice(0, 17) + "…" : x.title, run: () => openDoc(x.path) })), ms: 10000 });
  }
  // The open page was deleted by someone: go somewhere that exists.
  if (pages.some((x) => x.status === "deleted" && x.path === S.path)) openDoc(DEFAULT_DOC, { push: false });
}

// ----------------------------------------------------------------------------- deleting a page

async function deletePage(page = S.path) {
  if (!page || S.readOnly || undeletable(page)) return; // the top pages and templates can't be deleted
  const open = page === S.path;
  const title = (open ? pageTitle() : S.files.find((f) => f.path === page)?.title) || page.split("/").pop().replace(/\.md$/, "");
  // Everything that refers to it (links, groups, based-on, query blocks: src/refs.js) is left pointing at nothing.
  const refs = (await api("/api/refs?page=" + encodeURIComponent(page)).catch(() => null))?.refs || [];
  const by = (kind) => new Set(refs.filter((r) => r.kind === kind).map((r) => r.from)).size;
  const n = (k, one, many) => (k ? `${k} page${k > 1 ? "s " + many : " " + one}` : "");
  const said = [n(by("link"), "links to it", "link to it"), n(by("group"), "lists it as a group", "list it as a group"), n(by("variant"), "is a variant of it", "are variants of it"), n(by("query"), "has a list that names it", "have lists that name it")].filter(Boolean);
  const comments = open && notes.state.page === page ? notes.state.notes.length : 0;
  const more = [
    said.length ? said.join(", ").replace(/, ([^,]*)$/, " and $1") + "; all of those will point at nothing (To-do lists them)." : "",
    comments ? `Its ${comments} comment${comments > 1 ? "s stay" : " stays"} in the comment history.` : "",
  ].filter(Boolean).join(" ");
  const c = await ask("Delete this page?", `<b>${esc(title)}</b> (${esc(page)}) is deleted for everyone. ${more} You can undo it straight after, and its history stays in git.`,
    [{ key: "cancel", label: "Cancel" }, { key: "del", label: "Delete page", cls: "btn-danger" }]);
  if (c !== "del") return;
  if (open) await save(false, { quiet: true });
  S.deleting = page;
  const r = await api("/api/doc?path=" + encodeURIComponent(page), { method: "DELETE" });
  if (!r.ok) { S.deleting = null; return toast(failed("Couldn't delete the page", r), "bad"); }
  await loadTree();
  if (open) {
    S.dirty = false;
    const parent = page.replace(/\/[^/]+$/, "/index.md");
    await openDoc(S.fileSet.has(parent) && parent !== page ? parent : DEFAULT_DOC, { push: false });
    history.replaceState({ path: S.path }, "", "?p=" + encodeURI(S.path)); // the address no longer names the deleted page
  }
  S.deleting = null;
  toast(`Deleted “${title}”`, "", { actions: [{ label: "Undo", run: async () => {
    const u = await api("/api/doc/restore", { method: "POST", body: JSON.stringify({ path: page, content: r.content }) });
    if (!u.ok) return toast("Couldn't bring it back: " + (u.error || u.status), "bad");
    await loadTree();
    openDoc(page);
    toast("Brought it back", "good");
  } }] });
}

// ----------------------------------------------------------------------------- wiring

function wire() {
  buildToolbar();
  $("#sourceBtn").addEventListener("click", () => setMode(S.mode === "source" ? "edit" : "source"));
  $("#quickOpen").addEventListener("click", quickOpen);
  api("/api/update").then(showUpdate, () => {});
  startPreview();
  $("#newPage").addEventListener("click", newPageDialog);
  $("#deleteBtn").addEventListener("click", deletePage);
  $("#helpBtn").addEventListener("click", helpDialog);
  $("#treeFilter").addEventListener("input", renderTree);
  $("#treeFilter").addEventListener("keydown", (e) => {
    if (e.key === "Enter") { const first = $(".t-row.file"); if (first) first.click(); }
    if (e.key === "Escape") { e.target.value = ""; renderTree(); }
  });
  $$("#outlineWin .tab").forEach((t) => t.addEventListener("click", () => showRightTab(t.dataset.tab)));
  $("#source").addEventListener("input", scheduleDirty);
  $("#source").addEventListener("keydown", (e) => {
    if (e.key === "Tab") { e.preventDefault(); document.execCommand("insertText", false, "  "); }
  });
  $("#scroller").addEventListener("scroll", () => requestAnimationFrame(spy), { passive: true });

  const page = $("#page");
  page.addEventListener("click", (e) => {
    const a = e.target.closest("a");
    if (!a) return;
    // A link opens on a click, as in Notion; Ctrl+K edits it.
    if (window.getSelection()?.toString()) return;
    e.preventDefault();
    followLink(a.getAttribute("href"), a.textContent);
  });

  window.addEventListener("popstate", (e) => {
    const v = e.state?.view || viewOfUrl(new URLSearchParams(location.search).get("view"));
    if (v) { showView(v, false); return; }
    const p = e.state?.path || new URLSearchParams(location.search).get("p");
    const scroll = typeof e.state?.scroll === "number" ? e.state.scroll : null;
    if (p && (p !== S.path || S.view)) openDoc(p, { push: false, hash: e.state?.hash || location.hash.slice(1), scroll });
    else if (p && scroll != null) $("#scroller").scrollTop = scroll;
    else if (p) scrollToHeading(location.hash.slice(1));
    drawNav();
  });
  // The scroll position of each step, kept as you scroll (for Back).
  $("#scroller").addEventListener("scroll", () => { if (!scrollTimer) scrollTimer = setTimeout(rememberScroll, 250); }, { passive: true });
  // The mouse's back and forward buttons.
  window.addEventListener("mouseup", (e) => { if (e.button === 3 || e.button === 4) { e.preventDefault(); go(e.button === 3 ? -1 : 1); } });
  $("#backBtn").addEventListener("click", () => go(-1));
  $("#fwdBtn").addEventListener("click", () => go(1));
  drawNav();
  window.addEventListener("beforeunload", (e) => { if (S.dirty) { e.preventDefault(); e.returnValue = ""; } });

  document.addEventListener("keydown", (e) => {
    const mod = e.ctrlKey || e.metaKey;
    const key = e.key.toLowerCase();
    if ($("#modalHost").childElementCount) return;
    if (altGrChar(e)) return; // a character typed with AltGr, not a shortcut
    if (e.altKey && !mod && !e.shiftKey && (e.key === "ArrowLeft" || e.key === "ArrowRight")) { e.preventDefault(); go(e.key === "ArrowLeft" ? -1 : 1); return; }
    if (mod && key === "s") { e.preventDefault(); save(); }
    else if (mod && key === "p") { e.preventDefault(); quickOpen(); }
    else if (mod && e.shiftKey && key === "f") { e.preventDefault(); quickOpen(); }
    else if (mod && key === "k" && S.mode === "edit") { e.preventDefault(); linkDialog(); }
    else if (mod && key === "m") { e.preventDefault(); notes.compose(); } // Ctrl+M (Ctrl+Alt+M as in Google Docs, where Ctrl+Alt isn't AltGr)
    else if (mod && key === "j") { e.preventDefault(); ai.focus(); }
    else if (mod && e.shiftKey && key === "c" && S.editor && S.mode === "edit") { e.preventDefault(); copyForDiscord(); }
    // The panes: Ctrl+, (left) and Ctrl+. (right), on the same keys on every layout; Ctrl+ as in Notion,
    // on whichever key types  here.
    else if (mod && !e.altKey && (e.code === "Comma" || e.code === "Period")) { e.preventDefault(); layout.toggle(e.code === "Comma" ? "pages" : "outline"); }
    else if (mod && isKeyFor(e, "\\", "Backslash")) { e.preventDefault(); layout.toggle(e.shiftKey ? "outline" : "pages"); }
    else if (key === "escape") {
      if (account.escape() || aiChanges.escape() || layout.escape() || notes.escape()) e.preventDefault();
      else if (CSS.highlights?.has("codex-hit")) clearHits();
      else if (S.mode === "source") setMode("edit");
    } else if (!mod && e.key === "?" && !typing()) helpDialog();
    else if (!mod && e.key === "/" && !typing()) { e.preventDefault(); quickOpen(); }
  });
}

// ----------------------------------------------------------------------------- syncing

// Sharing (src/sync.js). There's no Sync button: what's typed here is sent by itself, and the status line
// says how it stands (updateStatus). Edits the Codex won't send by itself (R'th AI's, those made outside it)
// are asked about in the panel in the bottom-left corner: Share, Review or Undo. Problems that need
// someone (two people changing the same lines, sending failing) are asked there too. Unanswered, they
// stay on this computer and are asked again when the Codex opens next.
let syncState = null, syncAgo = null, heldTimer = null, heldShown = [];
function showSync(v) {
  // A sync just finished: the page list's "changed" dots follow git, so refresh them.
  if (syncState && v.lastSync && v.lastSync !== syncState.lastSync) { notesChanged(); refreshSpecialCounts(); }
  syncState = v;
  updateStatus();
  // Held pages are asked about once writing has settled for a moment, so a batch is one question.
  const held = v.held || [];
  clearTimeout(heldTimer);
  if (!held.length) { heldShown = []; drawSharePanel(); }
  else heldTimer = setTimeout(() => { heldShown = syncState.held || []; drawSharePanel(); }, 3000);
  drawSharePanel();
  aiChanges.heldChanged?.();
  clearInterval(syncAgo);
  syncAgo = setInterval(() => syncState && updateStatus(), 30000); // "sent 2 minutes ago" stays true
}

/** The open page's place in sharing, in a few words for the status line. */
function syncWords() {
  const v = syncState;
  if (!v || v.enabled !== true) return { text: "Saved", cls: "saved", title: v?.reason || "" };
  const heldHere = (v.held || []).some((x) => x.path === S.path);
  if (!v.auto && !heldHere) return { text: "Saved", cls: "saved", title: "This version doesn't share by itself" };
  if (heldHere) return { text: "Saved, not shared yet", cls: "unsaved", title: "Waiting for you to share it (bottom left)" };
  if (v.offline) return { text: "Saved, offline: sent when you're back", cls: "muted", title: v.problem };
  if (v.problem) return { text: "Saved, not sent", cls: "unsaved", title: v.problem };
  // Waiting on something done outside the Codex (src/sync.js, wait): nothing to click, so no box, just why.
  if (v.wait) return { text: v.waitShort || "Saved, not sent yet", cls: "muted", title: v.wait };
  if (v.busy || v.sending || v.ahead) return { text: "Sending…", cls: "muted" };
  return { text: "Saved and sent", cls: "saved", title: v.lastSync ? "Sent " + agoText(v.lastSync) : "" };
}

/** The panel in the bottom-left corner: what the Codex needs an answer about before it can share. */
function drawSharePanel() {
  let panel = $("#sharePanel");
  const v = syncState || {};
  const cards = [];
  const plural = (n, one, many) => `${n} ${n === 1 ? one : many}`;
  const groups = [["ai", "R'th AI"], ["other", "outside the Codex"]];
  for (const [by, who] of groups) {
    const pages = heldShown.filter((x) => x.by === by);
    if (!pages.length) continue;
    const files = pages.map((x) => x.path);
    cards.push(h("div", { class: "sp-card" },
      h("div", { class: "sp-title" }, by === "ai"
        ? `${who} changed ${plural(pages.length, "page", "pages")}. Share ${pages.length === 1 ? "it" : "them"}?`
        : `${plural(pages.length, "page was", "pages were")} changed outside the Codex. Share ${pages.length === 1 ? "it" : "them"}?`),
      // The first few by name; a long list ends "and 39 more" (all of them are in what Share and Undo do).
      h("div", { class: "sp-pages" }, pages.slice(0, 6).map((x, i) => [i ? ", " : "",
        x.gone ? h("span", { class: "sp-gone" }, "deleted " + x.title) : h("a", { onclick: () => reviewPages([x]) }, x.title)]),
        pages.length > 6 ? ` and ${pages.length - 6} more` : null),
      h("div", { class: "sp-actions" },
        h("button", { class: "btn btn-gold btn-small", onclick: () => sharePages(files) }, "Share"),
        pages.some((x) => !x.gone) ? h("button", { class: "btn btn-small", onclick: () => reviewPages(pages) }, "Review") : null,
        h("button", { class: "btn btn-small", onclick: () => undoPages(pages, who) }, "Undo"))));
  }
  if (v.clash) cards.push(h("div", { class: "sp-card" },
    h("div", { class: "sp-title" }, `You and someone else changed the same lines of ${v.clash.join(", ")}. Which version should stay?`),
    h("div", { class: "sp-actions" },
      h("button", { class: "btn btn-gold btn-small", onclick: () => settle("mine") }, "Keep mine"),
      h("button", { class: "btn btn-small", onclick: () => settle("theirs") }, "Use theirs"))));
  // Only what can be answered here goes in the box. Other trouble sending (no Codex role, GitHub or the
  // helper refusing, a stray git error) is on the status line, "Saved, not sent", why on hover, and the
  // Codex tries again every minute by itself: a Try again here never did more than that.
  else if (v.signIn && !v.offline) cards.push(h("div", { class: "sp-card" },
    h("div", { class: "sp-title" }, "Sign in to send your changes."),
    h("div", { class: "sp-text" }, "They're saved on this computer meanwhile, and go out once you're signed in with Discord."),
    h("div", { class: "sp-actions" },
      h("button", { class: "btn btn-gold btn-small", onclick: async () => { const r = await api("/api/account/signin", { method: "POST" }); if (r.url) window.open(r.url, "_blank"); } }, "Sign in"))));
  if (!cards.length) { panel?.remove(); return; }
  // Every sync (each save) redraws; rebuilding the same cards would replay their fade-in, so it blinked.
  const said = JSON.stringify([heldShown.map((x) => [x.path, x.by, x.title, x.gone]), v.clash || null,
    !v.clash && v.signIn && !v.offline]);
  if (panel && panel.dataset.said === said) return;
  if (!panel) { panel = h("div", { id: "sharePanel" }); document.body.append(panel); }
  panel.dataset.said = said;
  panel.replaceChildren(...cards);
}

async function sharePages(files) {
  await save(false, { quiet: true });
  const v = await api("/api/sync/share", { method: "POST", body: JSON.stringify({ files }) });
  showSync(v);
  // Only say so if they really went: still held means they didn't (the problem, if any, is in the panel).
  if (!v.problem && !(v.held || []).some((x) => files.includes(x.path))) toast(`Shared ${files.length === 1 ? "the page" : files.length + " pages"}`, "good");
  aiChanges.load();
}
/** Open the pages one by one with their changes marked; the page's own bar then offers Share. */
async function reviewPages(pages) {
  const first = pages.find((x) => !x.gone);
  if (!first) return;
  if (S.path !== first.path) await openDoc(first.path);
  aiChanges.show();
}
async function undoPages(pages, who) {
  const n = pages.length;
  const list = pages.map((x) => `<li>${esc(x.title)}</li>`).join("");
  const k = await ask(who === "outside the Codex" ? "Undo the changes made outside the Codex?" : `Undo ${who}'s changes?`, `${n === 1 ? "This page goes" : "These pages go"} back to how ${n === 1 ? "it was" : "they were"} last shared:<ul>${list}</ul>`,
    [{ key: "no", label: "Cancel" }, { key: "yes", label: "Undo the changes", cls: "btn-danger" }]);
  if (k !== "yes") return;
  const v = await api("/api/sync/discard", { method: "POST", body: JSON.stringify({ files: pages.map((x) => x.path) }) });
  showSync(v);
  if (pages.some((x) => x.path === S.path)) openDoc(S.path, { push: false, keepScroll: true });
  toast("Undone", "good");
}

async function settle(keep) {
  const v = await api("/api/sync/resolve", { method: "POST", body: JSON.stringify({ keep }) });
  showSync(v);
  if (v.problem) toast(v.problem, "bad"); else toast(keep === "mine" ? "Kept your version, and synced" : "Took their version, and synced", "good");
  if (S.path) openDoc(S.path, { push: false, keepScroll: true });
}

function agoText(t) {
  const s = (Date.now() - t) / 1000;
  return s < 60 ? "just now" : s < 3600 ? `${Math.round(s / 60)} min ago` : `${Math.round(s / 3600)} h ago`;
}
/** Is the keyboard busy with text (the page, a field)? Then single-key shortcuts stay out of the way. */
const typing = () => !!document.activeElement?.closest?.(".ProseMirror, input, textarea, select, [contenteditable]");

// The special pages in the page list, shown in the main window over the page (which stays loaded
// underneath): Open questions (every GDD page's, in one list), Recent changes (who changed what, from
// git) and To-do (pages to write and structure to fix; only listed while there's any).
const VIEWS = {
  changes: { title: "Recent changes", url: "changes", render: () => panels.renderChanges() },
  broken: { title: "To-do", url: "to-do", render: () => panels.renderBroken() },
};
const viewOfUrl = (v) => Object.keys(VIEWS).find((k) => VIEWS[k].url === v) || null;

function showView(kind, push = true) {
  if (!VIEWS[kind]) return;
  S.view = kind;
  document.body.classList.add("view-status");
  $("#main").classList.add("view-status");
  $("#statusView").classList.remove("hidden");
  $("#statusView").replaceChildren();
  $("#crumbs").replaceChildren(h("span", { class: "leaf" }, VIEWS[kind].title));
  document.title = VIEWS[kind].title + " - Codex";
  if (push) history.pushState({ view: kind }, "", "?view=" + VIEWS[kind].url);
  $$(".t-row.current").forEach((r) => r.classList.remove("current"));
  $(`.t-special[data-view="${kind}"]`)?.classList.add("current");
  VIEWS[kind].render();
}
function hideView() {
  if (!S.view) return;
  S.view = null;
  // A To-do fix changed the open page while it was hidden: read it again.
  if (S.reloadOpen && S.path) { S.reloadOpen = false; setTimeout(() => openDoc(S.path, { push: false, keepScroll: true }), 0); }
  document.body.classList.remove("view-status");
  $("#main").classList.remove("view-status");
  $("#statusView").classList.add("hidden");
  $$(".t-special").forEach((r) => r.classList.remove("current"));
}

// The counts beside Recent changes (others' changes since you last looked) and Broken links.
async function refreshSpecialCounts() {
  const [c, b] = await Promise.all([panels.changes(true), panels.broken(true)]);
  const fresh = c ? panels.unseen(c).length : 0;
  VIEWS.changes.count = fresh ? String(fresh) : "";
  VIEWS.changes.hot = !!fresh;
  const had = S.brokenCount || 0;
  S.brokenCount = b ? b.length || 0 : 0;
  VIEWS.broken.count = S.brokenCount ? String(S.brokenCount) : "";
  VIEWS.broken.hot = !!S.brokenCount;
  if (!!had !== !!S.brokenCount) { renderTree(); return; }
  for (const k of ["changes", "broken"]) {
    const n = $(`.t-special[data-view="${k}"] .t-special-n`);
    if (n) { n.textContent = VIEWS[k].count || ""; n.classList.toggle("hot", !!VIEWS[k].hot); }
  }
}

let rightTab = ["outline", "links", "ai"].includes(localStorage.getItem("codex.rightTab")) ? localStorage.getItem("codex.rightTab") : "outline";
function showRightTab(tab) {
  rightTab = tab;
  localStorage.setItem("codex.rightTab", tab);
  $$("#outlineWin .tab").forEach((t) => t.classList.toggle("active", t.dataset.tab === tab));
  $("#outline").classList.toggle("hidden", tab !== "outline");
  $("#linksPane").classList.toggle("hidden", tab !== "links");
  $("#aiPane")?.classList.toggle("hidden", tab !== "ai");
  // The AI tab widens the pane: ease it (style.css, cols-moving), then put the comments level again.
  if (rightTabShown && document.body.classList.contains("right-ai") !== (tab === "ai")) { // not on opening the Codex
    document.body.classList.add("cols-moving");
    clearTimeout(colsMoving);
    colsMoving = setTimeout(() => { document.body.classList.remove("cols-moving"); notes.layout(); }, 300);
  }
  document.body.classList.toggle("right-ai", tab === "ai");
  $("#outlineWin .win-body").classList.toggle("ai-body", tab === "ai");
  if (tab === "links") panels.renderLinks();
  if (tab === "ai") { ai.shown(); requestAnimationFrame(() => notes.layout()); }
  rightTabShown = true;
}
let colsMoving = null, rightTabShown = false;
function setCommentCount(n) { $("#statusComments").textContent = n ? `${n} comment${n > 1 ? "s" : ""}` : ""; }
function setLinksCount(n) { $("#linksCount").textContent = n ? String(n) : ""; }

let treeRefresh = null;
function notesChanged() { clearTimeout(treeRefresh); treeRefresh = setTimeout(loadTree, 400); }

function currentHeadingPos() {
  const x = headings.find((y) => y.id === S.activeHeading) || headings[0];
  return x ? x.pos : null;
}
/** Scroll a position into view (onlyIfHidden: leave it be if it's already on screen). */
function scrollToPos(pos, onlyIfHidden = false) {
  if (!S.editor || S.mode === "source") return;
  try {
    const c = S.editor.view.coordsAtPos(Math.min(pos, S.editor.state.doc.content.size));
    const sc = $("#scroller"), r = sc.getBoundingClientRect();
    if (onlyIfHidden && c.top > r.top + 20 && c.bottom < r.bottom - 20) return;
    sc.scrollTop += c.top - r.top - sc.clientHeight / 3;
  } catch {}
}

const panels = makePanels({ S, $, h, api, toast, isMeta, ask: (...a) => ask(...a), save: () => save(false, { quiet: true }), loadTree: () => loadTree(), openDoc: (...a) => openDoc(...a), newPage: (prefill) => newPageDialog(null, prefill), findInPage: (q) => findInPage(q), setLinksCount, countsChanged: () => refreshSpecialCounts() });
const notes = makeNotes({
  S, $, $$, h, esc, api, toast, modal, ask, renderHtml,
  openDoc: (...a) => openDoc(...a), refreshDirty: () => refreshDirty(), currentHeadingPos, scrollToPos,
  setCommentCount, notesChanged, saveNow: () => { refreshDirty(); return S.dirty ? save(false, { quiet: true }) : Promise.resolve(true); }, banner: (text) => showBanner(text, [{ label: "OK", run: hideBanner }]),
  hasPage: (p) => S.fileSet.has(p), titleOf: (p) => S.files.find((f) => f.path === p)?.title || null,
  askAi: () => ai.focus({ selection: true }),
  othersTyping: (id) => together.typingIn(id),
});

// Who's here (src/together-ui.js, src/together.js): the others with a Codex open.
const together = makeTogetherUi({
  S, $, h, api, currentHeadingPos: () => currentHeadingPos(), pageTitle: () => pageTitle(),
  goTo: (page, leaf) => { if (page === S.path) { if (leaf) scrollToHeading(slug(leaf), true); } else openDoc(page, { hash: leaf ? slug(leaf) : "" }); },
});

const layout = makeLayout({ $, h, notes, help: () => helpDialog(), online: together.online, presence: { on: () => presenceOn(), toggle: () => { localStorage.setItem("codex.presence", presenceOn() ? "off" : "on"); tellPresence(); } } });

// ----------------------------------------------------------------------------- Discord (src/discord-extras.js)

// Pasting a Discord conversation or an image (a file or a Discord image link).
const discordPaste = makePasteHandler({ S, api, toast });

// "Editing <page>" on your Discord profile, unless switched off in the logo's menu.
const presenceOn = () => localStorage.getItem("codex.presence") !== "off";
function tellPresence() {
  api("/api/presence", { method: "POST", body: JSON.stringify({ page: presenceOn() && S.path && !S.view ? S.path : null }) });
}

/** Ctrl+Shift+C: the selection, or else the section the cursor is in, as Discord's Markdown. */
function copyForDiscord() {
  const { state } = S.editor;
  let { from, to } = state.selection;
  let heading = null;
  const before = headings.filter((x) => x.pos <= from);
  const here = before[before.length - 1];
  if (from === to) {
    if (!here) return toast("Select some text, or put the cursor in a section", "bad");
    const next = headings.find((x) => x.pos > here.pos && x.level <= here.level);
    from = here.pos; to = next ? next.pos : state.doc.content.size;
  }
  if (here && here.level > 1) heading = here.id;
  const doc = state.schema.topNodeType.create(null, state.doc.slice(from, to).content);
  const md = S.editor.storage.markdown.serializer.serialize(doc);
  const title = S.files.find((f) => f.path === S.path)?.title || S.path;
  navigator.clipboard.writeText(forDiscord(md, { page: S.path, title, heading })).then(
    () => toast("Copied for Discord", "good"), () => toast("Couldn't copy", "bad"));
}
// Signing in or out moves the comments (Discord or docs/.notes/): the engine says so with a "notes" event.
// A contributor's copy is read-only until they sign in with Discord and have the Codex role.
const account = makeAccountButton({ $, h, api, toast, onChange: (v) => {
  notes.setMe(v.signedIn ? v.user : null);
  together.setMe(v.signedIn ? v.user : null);
  const ro = v.canEdit === false;
  if (ro !== !!S.readOnly) { S.readOnly = ro; if (S.editor) applyMode(); }
} });
// Show changes: what the AI changed on the open page (src/ai-changes.js).
const aiChanges = makeAiChanges({ S, $, h, api, toast, scrollToPos,
  setFront: (front) => { S.front = front; scheduleDirty(); gddUi.refresh?.(); }, // a suggested header change, accepted
  held: (page) => (syncState?.held || []).find((x) => x.path === page) || null, share: (page) => sharePages([page]) });
const ai = makeAi({
  S, $, h, api, toast, renderHtml, notes,
  openDoc: (...a) => openDoc(...a), hasPage: (p) => S.fileSet.has(p), titleOf: (p) => S.files.find((f) => f.path === p)?.title || null,
  saveNow: () => { refreshDirty(); return S.dirty ? save(false, { quiet: true }) : Promise.resolve(true); },
  show: () => { layout.show("outline"); showRightTab("ai"); },
  // From a reply: open the page the AI changed with its changes shown.
  showChanges: (page) => { if (page === S.path) aiChanges.show(); else { S.wantChanges = page; openDoc(page); } },
  pageChanges: () => aiChanges.load(),
});

async function start() {
  // In the desktop app the top row is the window's own title bar (style.css, body.in-app).
  if (/Electron/i.test(navigator.userAgent)) document.body.classList.add("in-app");
  configureSpelling({ api, toast });
  wire();
  ai.init($("#outlineWin .win-body"));
  account.init();
  together.init();
  showRightTab(rightTab);
  notes.init();
  aiChanges.init();
  layout.init();
  api("/api/sync").then((v) => v.ok !== false && showSync(v));
  await loadTree();
  const params = new URLSearchParams(location.search);
  const first = params.get("p") || DEFAULT_DOC; // opened on the Charter, unless a link asked for a page
  await openDoc(S.fileSet.has(first) ? first : DEFAULT_DOC, { push: false, hash: location.hash.slice(1) });
  const firstView = viewOfUrl(params.get("view"));
  if (firstView) { history.replaceState({ path: S.path }, "", "?p=" + encodeURI(S.path)); showView(firstView); }
  else history.replaceState({ path: S.path, hash: location.hash.slice(1) }, "", "?p=" + encodeURI(S.path) + location.hash);
  listen();
  welcome();
  refreshSpecialCounts();
}

start();

// For checking in the browser's console: the state, and the merge.
// ----------------------------------------------------------------------------- updates

// One way to update: the update screen, over the whole Codex, so it's always up to date when used.
// - A new release: the installed Codex downloads it quietly (electron/main.js); once it's here, the
//   screen says so and Restart and update is the only way on.
// - Too old to use (the helper's MIN_CODEX, helper/src/version.js; src/codex-version.js): the screen
//   comes at once, with the download's progress, and nothing more can be changed or sent.
// The open page is saved the moment the screen appears; Restart and update saves and sends everything
// before installing (electron/main.js, installUpdate).
let updateView = { state: "none" };
let requiredView = null; // { current, min, outdated }, from the engine (src/codex-version.js)
let installed = true;    // the desktop app (it can update itself), not the browser version
function showUpdate(v, fromPreview = false) {
  if (previewing && !fromPreview) return; // a preview holds the screen still (startPreview)
  if (v?.required) requiredView = v.required;
  if (v && "installed" in v) installed = v.installed;
  updateView = v || { state: "none" };
  showUpdateScreen();
}

function showUpdateScreen() {
  let el = $("#requiredScreen");
  const st = updateView.state;
  const required = !!requiredView?.outdated;
  const shown = required || st === "ready" || st === "installing";
  $("#app").inert = shown; // nothing under the screen can be clicked or typed into
  if (!shown) { el?.remove(); return; }
  if (!el && !previewing) window.__codex.flush().catch(() => {}); // save the open page first, whatever happens next
  // Where to get the new Codex by hand: the latest release's page (its installer updates this one in place).
  const download = (label) => h("a", { class: "rq-download", href: "https://github.com/Martifugit/rth/releases/latest", target: "_blank", rel: "noopener" }, label);
  const status = !installed
    ? h("p", { class: "rq-status" }, download("Download the new Codex"), ", run it, then open the Codex again.")
    : st === "ready" ? h("div", {}, h("button", { class: "btn btn-gold rq-go", onclick: installUpdate }, "Restart and update"),
        h("p", { class: "rq-alt" }, "Your work is saved before the Codex restarts."))
    : st === "installing" ? h("p", { class: "rq-status" }, "Saving your work and updating: the Codex opens again in a moment…")
    : st === "downloading" ? h("div", { class: "rq-progress" }, h("div", { class: "rq-bar" }, h("span", { style: `width:${updateView.percent || 0}%` })), h("p", { class: "rq-status" }, `Downloading ${updateView.version || "the update"}… ${updateView.percent || 0}%`))
    : h("div", {}, h("p", { class: "rq-status" }, "Looking for the update…"),
        h("p", { class: "rq-alt" }, "If it doesn't come, ", download("download the new Codex"), " and run it."));
  const [title, text] = required
    ? ["An update is needed", `A new release of the R'th Codex has changes this version (${requiredView.current}) can't work with. `
      + `Update to version ${requiredView.min} or newer to keep using the Codex.`]
    : ["An update is ready", `Version ${updateView.version || "a new version"} of the R'th Codex is here. Restart to start using it.`];
  // The same pane as the Codex's other pages (src/codex-page.js): a title bar, then the message.
  const box = h("div", { class: "rq-box" },
    h("div", { class: "rq-bar-top" }, h("img", { src: "/logo.png", alt: "" }), h("span", {}, "R'th Codex")),
    h("div", { class: "rq-body" }, h("h2", { class: "rq-title" }, title), h("p", { class: "rq-text" }, text), status));
  if (!el) { el = h("div", { id: "requiredScreen", class: "rq" }); document.body.append(el); }
  el.replaceChildren(box);
}

/**
 * How updating looks, without an update: ?preview=ready (a new release, ready), ?preview=required (too
 * old: the screen while it downloads, over and over) or ?preview=required-ready.
 * For judging the look only; nothing is downloaded or installed.
 */
function startPreview() {
  previewing = PREVIEW;
  if (!previewing) return;
  const required = previewing.startsWith("required") ? { current: "0.3.0", min: "0.4.0", outdated: true } : null;
  const show = (state, percent) => showUpdate({ state, version: "0.4.0", percent, installed: true, ...(required ? { required } : {}) }, true);
  if (previewing.endsWith("ready")) return show("ready", 100);
  let p = 0;
  show("downloading", 0);
  setInterval(() => { p = p >= 100 ? 0 : Math.min(100, p + 3 + Math.round(Math.random() * 6)); show("downloading", p); }, 350);
}

async function installUpdate() {
  if (previewing) return toast("A preview: nothing to install");
  if (updateView.state !== "ready") return;
  showUpdate({ ...updateView, state: "installing" });
  const r = await api("/api/update/install", { method: "POST" });
  if (!r.ok) { toast(failed("Couldn't update", r), "bad"); showUpdate({ ...updateView, state: "ready" }); }
}

window.__codex = {
  S, mergeEdit, renderHtml, editorMarkdown: () => editorMarkdown(), openDoc, notes, panels, toast, together,
  // The desktop app calls this before closing: save whatever is still being typed.
  // The desktop app tells the page about updates (electron/main.js).
  updateState: (v) => showUpdate(v),
  flush: async () => { clearTimeout(autoTimer); refreshDirty(); return !S.dirty || S.noSave || (await save(false, { quiet: true })); },
};
