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
import { makeLayout } from "./layout.js";
import { makeAi } from "./ai.js";
import { makeAccountButton } from "./account-button.js";
import { makePasteHandler, forDiscord } from "./discord-extras.js";
import { AiMarks, makeAiChanges } from "./ai-changes.js";
import { Spelling, configureSpelling } from "./spellcheck.js";

const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => [...el.querySelectorAll(s)];
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

const DEFAULT_DOC = "docs/gdd/README.md";

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

export function slug(text) {
  return text.toLowerCase().trim().replace(/[^\p{L}\p{N}\s_-]/gu, "").replace(/\s/g, "-");
}

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
  if (path.endsWith(".md") && !S.fileSet.has(path) && (path.startsWith("docs/") || !path.includes("/"))) return "link-broken";
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
    if (cls) decos.push(Decoration.inline(pos, pos + node.nodeSize, { class: cls, title: cls === "link-broken" ? "No such page: " + link.attrs.href : null }));
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

function makeEditor(text) {
  if (S.editor) S.editor.destroy();
  const page = $("#page");
  page.innerHTML = "";
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
    ],
    content: text,
    onUpdate: () => { clearHits(); scheduleDirty(); scheduleOutline(); notes?.docChanged(); aiChanges?.docChanged(); },
    editorProps: { attributes: { spellcheck: "false" }, handlePaste: (view, event) => discordPaste(view, event) },
    onTransaction: () => updateToolbar(),
  });
  S.base = text;
  S.baseline = editorMarkdown();
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
  const cur = editorMarkdown();
  return cur === S.baseline ? S.base : mergeEdit(S.base, S.baseline, cur);
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
  document.title = S.view === "status" ? "Open questions · Codex" : (S.dirty ? "● " : "") + (pageTitle() || "R'th Codex") + " · Codex";
  updateStatus();
}

// ----------------------------------------------------------------------------- loading pages

async function api(url, opts) {
  const r = await fetch(url, opts && { headers: { "Content-Type": "application/json" }, ...opts });
  const j = await r.json().catch(() => ({}));
  return { ok: r.ok, status: r.status, ...j };
}

async function loadTree() {
  const r = await api("/api/tree");
  S.files = r.files || [];
  S.fileSet = new Set(S.files.map((f) => f.path));
  renderTree();
}

async function openDoc(path, { hash = "", push = true, find = null, keepScroll = false, mode = null } = {}) {
  if (S.path && path !== S.path) {
    clearTimeout(autoTimer);
    refreshDirty();
    if (S.dirty && !S.noSave && !(await save(false, { quiet: true }))) return;
  }
  const r = await api("/api/doc?path=" + encodeURIComponent(path));
  hideStatus();
  if (!r.ok) {
    toast(`Can't open ${path}: ${r.error || r.status}`, "bad");
    if (!S.path && path !== DEFAULT_DOC) return openDoc(DEFAULT_DOC, { push: false });
    return;
  }
  const scroll = $("#scroller").scrollTop;
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
  localStorage.setItem("codex.last", S.path);
  tellPresence();
  renderCrumbs(); markTreeCurrent(); buildOutline(); refreshDirty();
  notes.load(S.path);
  ai.pageChanged();
  aiChanges.pageOpened(S.wantChanges === S.path);
  S.wantChanges = null;
  if (rightTab === "links") panels.renderLinks(); else panels.linksFor(S.path).then((r) => r && setLinksCount(r.inbound.length));
  requestAnimationFrame(() => {
    if (keepScroll) $("#scroller").scrollTop = scroll;
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
    if (text !== S.base) { makeEditor(text); buildOutline(); }
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
  $("#docWrap").classList.toggle("source", S.mode === "source");
  updateToolbar();
  if (typeof notes !== "undefined") requestAnimationFrame(() => notes.layout());
}

// ----------------------------------------------------------------------------- saving

async function save(force = false, { quiet = false } = {}) {
  if (!S.path || S.saving) return false;
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
  if (!r.ok) { toast("Couldn't save: " + (r.error || r.status), "bad"); updateStatus(); return false; }
  S.conflict = false;
  S.disk = content; S.diskHash = r.hash;
  if (S.mode !== "source") { S.base = content; S.baseline = editorMarkdown(); notes.saved(); }
  panels.invalidate();
  hideBanner();
  refreshDirty();
  if (!quiet) toast("Saved", "good");
  const f = S.files.find((x) => x.path === S.path);
  if (f && !f.git) { f.git = "changed"; renderTree(); }
  return true;
}

// ----------------------------------------------------------------------------- live changes from disk

function listen() {
  const es = new EventSource("/api/events");
  es.addEventListener("notes", (e) => notes.onNotes(JSON.parse(e.data)));
  es.addEventListener("run", (e) => notes.onRun(JSON.parse(e.data)));
  es.addEventListener("sync", (e) => showSync(JSON.parse(e.data)));
  es.addEventListener("ai", (e) => { const ev = JSON.parse(e.data); ai.onEvent(ev); if (ev.state === "reviewed" || ev.pages?.includes(S.path)) aiChanges.load(); });
  es.addEventListener("account", (e) => account.onEvent(JSON.parse(e.data)));
  es.onmessage = (e) => {
    const events = JSON.parse(e.data);
    panels.invalidate();
    notesChanged();
    if (S.view === "status") panels.renderStatus();
    for (const ev of events) {
      if (ev.path !== S.path || ev.hash === S.diskHash) continue;
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

const FOLDER_ICONS = {
  gdd: "book", areas: "map", "lore-rework": "scripture", lore: "scripture", quests: "quests", regions: "compass",
  research: "eye", ideas: "spark", workflows: "task", templates: "note", entities: "bestiary", "1-overview": "guide",
  "2-gameplay": "skills", "3-story": "scripture", "4-content": "quests", "5-interface": "settings", "6-behaviour": "bestiary", appendices: "pack",
};

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

function renderTree() {
  const filter = $("#treeFilter").value.trim().toLowerCase();
  const tree = $("#tree");
  const matches = (f) => !filter || f.path.toLowerCase().includes(filter) || (f.title || "").toLowerCase().includes(filter);
  const files = S.files.filter(matches);
  const data = buildTreeData(files);
  const mark = (text) => {
    if (!filter) return text;
    const i = text.toLowerCase().indexOf(filter);
    return i < 0 ? text : [text.slice(0, i), h("mark", {}, text.slice(i, i + filter.length)), text.slice(i + filter.length)];
  };
  const fileRow = (f) => h("div", {
    class: "t-row file" + (f.path === S.path ? " current" : ""), "data-path": f.path, title: (f.title ? f.title + "\n" : "") + f.path,
    onclick: () => openDoc(f.path),
  }, h("span", { class: "caret" }), h("span", { class: "ico ico-note" }), h("span", { class: "t-name" }, mark(f.path.split("/").pop().replace(/\.md$/, ""))),
  f.git ? h("span", { class: "t-git " + f.git, title: f.git === "new" ? "New, not in git yet" : "Changed since the last commit" }) : null);
  const dirNode = (d) => {
    const open = filter || S.open.has(d.path);
    const kids = h("div", { class: "t-children" + (open ? "" : " closed") },
      [...d.dirs.values()].sort((a, b) => collator.compare(a.name, b.name)).map(dirNode), d.files.sort(fileOrder).map(fileRow));
    const row = h("div", {
      class: "t-row folder" + (open ? " open" : ""), title: d.path,
      onclick: () => {
        const isOpen = !kids.classList.toggle("closed");
        row.classList.toggle("open", isOpen);
        if (isOpen) S.open.add(d.path); else S.open.delete(d.path);
        localStorage.setItem("codex.open", JSON.stringify([...S.open]));
      },
    }, h("span", { class: "caret" }), h("span", { class: "ico ico-" + (FOLDER_ICONS[d.name] || "journal") }), h("span", { class: "t-name" }, mark(d.name)),
    h("span", { class: "t-count" }, String(countFiles(d))));
    return h("div", {}, row, kids);
  };
  const docs = data.dirs.get("docs");
  const groups = [];
  // Design docs: the GDD's own contents, straight away. General: everything else under docs/, then
  // the repository's own Markdown (README, CLAUDE.md...) in a folder of its own.
  const byName = (a, b) => collator.compare(a.name, b.name);
  const gdd = docs?.dirs.get("gdd");
  const special = !filter ? h("div", { class: "t-row file t-special" + (S.view === "status" ? " current" : ""), title: "Every open question in the GDD, in one list", onclick: showStatus },
    h("span", { class: "caret" }), h("span", { class: "ico ico-quests" }), h("span", { class: "t-name" }, "Open questions")) : null;
  if (gdd || special) groups.push(h("div", { class: "t-group" }, h("div", { class: "t-group-title" }, "Design docs"), special,
    gdd ? gddOrder(gdd).map((x) => (x.dirs ? dirNode(x) : fileRow(x))) : null));
  const otherDirs = docs ? [...docs.dirs.values()].filter((d) => d.name !== "gdd").sort(byName) : [];
  const otherFiles = docs ? docs.files.sort(fileOrder) : [];
  const root = data.files.length ? { name: "repository root", path: ":root", dirs: new Map(), files: data.files } : null;
  if (otherDirs.length || otherFiles.length || root) groups.push(h("div", { class: "t-group" }, h("div", { class: "t-group-title" }, "General"),
    otherDirs.map(dirNode), otherFiles.map(fileRow), root ? dirNode(root) : null));
  if (!groups.length) groups.push(h("div", { class: "o-empty" }, "No pages match."));
  tree.replaceChildren(...groups);
}

/** The GDD's pages and folders in its own order: its README, then I to X (1-overview … 10-management,
 *  pages and folders together), then the rest alphabetically. */
function gddOrder(gdd) {
  const name = (x) => (x.dirs ? x.name : x.path.split("/").pop());
  const rank = (x) => (/^(readme|index)\.md$/i.test(name(x)) ? 0 : /^\d/.test(name(x)) ? 1 : 2);
  return [...gdd.files, ...gdd.dirs.values()].sort((a, b) => rank(a) - rank(b) || collator.compare(name(a), name(b)));
}

function markTreeCurrent() {
  if (S.path) {
    const parts = S.path.split("/");
    let changed = false;
    for (let i = 1; i < parts.length; i++) { const p = parts.slice(0, i).join("/"); if (!S.open.has(p)) { S.open.add(p); changed = true; } }
    if (parts.length === 1 && !S.open.has(":root")) { S.open.add(":root"); changed = true; } // the repository's own pages
    if (changed) { localStorage.setItem("codex.open", JSON.stringify([...S.open])); renderTree(); }
  }
  $$(".t-row.file").forEach((r) => r.classList.toggle("current", r.dataset.path === S.path));
  const cur = $(".t-row.current");
  if (cur) cur.scrollIntoView({ block: "nearest" });
}

// ----------------------------------------------------------------------------- breadcrumbs, outline, status

function renderCrumbs() {
  const parts = S.path.split("/");
  const out = [];
  parts.forEach((part, i) => {
    if (i) out.push(h("span", { class: "sep" }, "›"));
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
  $("#statusLeft").textContent = `${words.toLocaleString()} words · ${Math.max(1, Math.round(words / 230))} min read · ${headings.length} headings`;
  const git = S.files.find((f) => f.path === S.path)?.git;
  $("#statusRight").innerHTML = S.conflict ? '<span class="unsaved">Not saved: the page changed somewhere else</span>'
    : S.saving ? '<span class="muted">Saving…</span>'
    : S.dirty ? '<span class="muted">Editing…</span>'
    : `<span class="saved">All changes saved</span>${git ? ` · ${git === "new" ? "not in git yet" : "changed since the last commit"}` : ""}`;
}


// ----------------------------------------------------------------------------- following links

function followLink(href) {
  if (!href) return;
  if (isExternal(href)) { window.open(href, "_blank", "noopener"); return; }
  if (href.startsWith("#")) { scrollToHeading(href.slice(1), true); return; }
  const { path, hash, dir } = resolveHref(S.path, href);
  if (path.endsWith(".md")) {
    if (S.fileSet.has(path) || !path.startsWith("docs/")) return openDoc(path, { hash });
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
    h("p", { class: "welcome-lead" }, "The design docs for R'th: read them, write in them, comment and talk them through." + (needDiscord && needAi ? " Two things make it yours:" : "")),
    needDiscord ? step("Sign in with Discord", acc.contributor ? "To edit the docs and comment under your name. You need the Codex role in the R'th Discord." : "To comment under your name, and send your changes without a GitHub account.", "Sign in", async () => {
      const r = await api("/api/account/signin", { method: "POST" });
      if (r.url) window.open(r.url, "_blank");
    }) : null,
    needAi ? step("Free AI", "Claude isn't on this computer. OpenRouter gives you free models, about 50 messages a day.", "Sign in with OpenRouter", async () => {
      const r = await api("/api/openrouter/signin", { method: "POST" });
      if (r.url) window.open(r.url, "_blank");
    }) : null,
    h("div", { class: "modal-actions" }, h("button", { class: "btn", onclick: () => close() }, "Not now")),
  ], { width: "min(520px, 94vw)" });
}

function modal(title, body, { onClose, width } = {}) {
  const host = $("#modalHost");
  const close = () => { host.replaceChildren(); document.removeEventListener("keydown", onKey, true); onClose && onClose(); };
  const onKey = (e) => { if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); close(); } };
  document.addEventListener("keydown", onKey, true);
  const win = h("div", { class: "window modal", style: width ? `width:${width}` : null },
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

function docOptions() {
  return h("datalist", { id: "docList" }, S.files.map((f) => h("option", { value: relativePath(S.path, f.path) }, f.title || "")));
}

function linkDialog() {
  const e = S.editor;
  const { from, to, empty } = e.state.selection;
  const current = e.getAttributes("link").href || "";
  const url = h("input", { class: "field", value: current, placeholder: "A page (start typing), #a-heading or https://…", list: "docList", spellcheck: "false" });
  const text = h("input", { class: "field", placeholder: "Link text", value: empty ? "" : e.state.doc.textBetween(from, to, " ") });
  const apply = () => {
    const href = url.value.trim();
    close();
    const c = e.chain().focus();
    if (!href) { c.extendMarkRange("link").unsetLink().run(); return; }
    if (empty && !e.isActive("link")) {
      const label = text.value.trim() || href.split("/").pop().replace(/\.md$/, "");
      c.insertContent({ type: "text", text: label, marks: [{ type: "link", attrs: { href } }] }).run();
    } else c.extendMarkRange("link").setLink({ href }).run();
  };
  const onEnter = (ev) => { if (ev.key === "Enter") { ev.preventDefault(); apply(); } };
  url.addEventListener("keydown", onEnter); text.addEventListener("keydown", onEnter);
  const close = modal(current ? "Edit link" : "Add a link", [
    h("label", {}, "Where to"), url, docOptions(),
    empty && !current ? [h("label", {}, "Text"), text] : null,
    h("div", { class: "modal-actions" },
      current ? h("button", { class: "btn btn-danger", onclick: () => { close(); e.chain().focus().extendMarkRange("link").unsetLink().run(); } }, "Remove link") : null,
      h("button", { class: "btn", onclick: () => close() }, "Cancel"), h("button", { class: "btn btn-gold", onclick: apply }, "Apply")),
  ]);
  setTimeout(() => url.focus(), 0);
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
    .replace(/^\s*(?:#{1,6}|[-+*]|\d+[.)]|>)\s+/, "").replace(/^\s*\|\s*|\s*\|\s*$/g, "").replace(/\s*\|\s*/g, "  ·  ").trim();
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
    const pages = S.files.map((f) => ({ f, s: score(f) }))
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

function newPageDialog() {
  const dirs = [...new Set(S.files.map((f) => dirOf(f.path)).filter((d) => d.startsWith("docs")))].sort(collator.compare);
  const here = S.path && dirOf(S.path).startsWith("docs") ? dirOf(S.path) : "docs/gdd";
  const folder = h("select", { class: "field" }, dirs.map((d) => h("option", { value: d, selected: d === here }, d)));
  const name = h("input", { class: "field", placeholder: "The page's title", spellcheck: "true" });
  const file = h("div", { class: "muted small new-file" });
  const fileName = () => name.value.trim().replace(/\.md$/i, "").normalize("NFKD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/['’]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
  const showFile = () => { file.textContent = fileName() ? `Saved as ${folder.value}/${fileName()}.md` : ""; };
  name.addEventListener("input", showFile); folder.addEventListener("change", showFile);
  const templates = S.files.filter((f) => f.path.startsWith("docs/gdd/templates/"));
  const template = h("select", { class: "field" }, h("option", { value: "" }, "Blank page"), templates.map((t) => h("option", { value: t.path }, (t.title || t.path).replace(/^Template:\s*/i, "") + "  (" + t.path.split("/").pop() + ")")));
  const create = async () => {
    const n = fileName();
    if (!n) { name.focus(); return; }
    const path = folder.value + "/" + n + ".md";
    const r = await api("/api/new", { method: "POST", body: JSON.stringify({ path, title: name.value.trim(), template: template.value || null }) });
    if (!r.ok) { toast(r.error || "Couldn't create it", "bad"); return; }
    close();
    await loadTree();
    await openDoc(r.path, { mode: "edit" });
    focusEditor(S.editor.state.doc.content.size); // ready to type, as a new document is
    toast("Page created", "good");
  };
  name.addEventListener("keydown", (e) => { if (e.key === "Enter") create(); });
  const close = modal("New page", [h("label", {}, "Title"), name, h("label", {}, "Folder"), folder, file, h("label", {}, "Start from"), template,
    h("div", { class: "modal-actions" }, h("button", { class: "btn", onclick: () => close() }, "Cancel"), h("button", { class: "btn btn-gold", onclick: create }, "Create"))]);
  setTimeout(() => name.focus(), 0);
}

function helpDialog() {
  const k = (keys, what) => [h("span", {}, keys.split(" ").map((x) => h("kbd", { style: "margin-right:3px" }, x))), h("span", {}, what)];
  modal("Keys", h("div", { class: "keys" },
    k("Ctrl P", "Search: pages by name, then the text of every page"),
    k("Ctrl Alt M", "Comment on the selected text"), k("Ctrl Enter", "Post a comment. Write @agent in it to leave it for the next agent session"),
    k("Ctrl J", "Ask the AI (with the selected text, if any)"), k("Ctrl Shift C", "Copy for Discord: the selection, or the section you're in, with a link back"),
    k("Ctrl K", "Add or edit a link"), k("Ctrl B", "Bold"), k("Ctrl I", "Italic"), k("Ctrl Z", "Undo"),
    k("Ctrl S", "Save now (pages also save by themselves)"), k("Alt ←", "Back"),
    k("Ctrl \\", "Show or hide the pages"), k("Ctrl Shift \\", "Show or hide the outline and links"),
    h("span", { class: "muted small", style: "grid-column:1/-1;margin-top:8px" },
      "Markdown shortcuts work as you type: ## for a heading, - or 1. for a list, > for a quote, ``` for code. Saving only rewrites the paragraphs you changed; the rest of the file stays exactly as written.")));
}

// ----------------------------------------------------------------------------- toasts

function toast(text, kind = "") {
  const t = h("div", { class: "toast " + kind }, text);
  $("#toasts").append(t);
  setTimeout(() => { t.style.transition = "opacity .3s"; t.style.opacity = "0"; setTimeout(() => t.remove(), 300); }, 2200);
}

// ----------------------------------------------------------------------------- wiring

function wire() {
  buildToolbar();
  $("#sourceBtn").addEventListener("click", () => setMode(S.mode === "source" ? "edit" : "source"));
  $("#quickOpen").addEventListener("click", quickOpen);
  $("#syncBtn").addEventListener("click", syncNow);
  $("#newPage").addEventListener("click", newPageDialog);
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
    followLink(a.getAttribute("href"));
  });

  window.addEventListener("popstate", (e) => {
    if (e.state?.view === "status" || /^(status|questions)$/.test(new URLSearchParams(location.search).get("view") || "")) { showStatus(false); return; }
    const p = e.state?.path || new URLSearchParams(location.search).get("p");
    if (p && (p !== S.path || S.view === "status")) openDoc(p, { push: false, hash: e.state?.hash || location.hash.slice(1) });
    else if (p) scrollToHeading(location.hash.slice(1));
  });
  window.addEventListener("beforeunload", (e) => { if (S.dirty) { e.preventDefault(); e.returnValue = ""; } });

  document.addEventListener("keydown", (e) => {
    const mod = e.ctrlKey || e.metaKey;
    const key = e.key.toLowerCase();
    if ($("#modalHost").childElementCount) return;
    if (mod && key === "s") { e.preventDefault(); save(); }
    else if (mod && key === "p") { e.preventDefault(); quickOpen(); }
    else if (mod && e.shiftKey && key === "f") { e.preventDefault(); quickOpen(); }
    else if (mod && key === "k" && S.mode === "edit") { e.preventDefault(); linkDialog(); }
    else if (mod && key === "m") { e.preventDefault(); notes.compose(); } // Ctrl+Alt+M, as in Google Docs (Ctrl+M too)
    else if (mod && key === "j") { e.preventDefault(); ai.focus(); }
    else if (mod && e.shiftKey && key === "c" && S.editor && S.mode === "edit") { e.preventDefault(); copyForDiscord(); }
    else if (mod && e.code === "Backslash") { e.preventDefault(); layout.toggle(e.shiftKey ? "outline" : "pages"); }
    else if (key === "escape") {
      if (account.escape() || aiChanges.escape() || layout.escape() || notes.escape()) e.preventDefault();
      else if (CSS.highlights?.has("codex-hit")) clearHits();
      else if (S.mode === "source") setMode("edit");
    } else if (!mod && e.key === "?" && !typing()) helpDialog();
    else if (!mod && e.key === "/" && !typing()) { e.preventDefault(); quickOpen(); }
  });
}

// ----------------------------------------------------------------------------- syncing

// The Sync icon in the title bar: its tooltip says where things stand; a click syncs now, including
// doc changes made outside the Codex (automatic sync only sends what was saved here). src/sync.js.
let syncState = null, syncAgo = null;
function showSync(v) {
  // A sync just finished: the page list's "changed" dots follow git, so refresh them.
  if (syncState && v.lastSync && v.lastSync !== syncState.lastSync) notesChanged();
  syncState = v;
  const btn = $("#syncBtn");
  btn.classList.toggle("hidden", v.enabled === null);
  btn.classList.toggle("busy", !!v.busy);
  btn.classList.toggle("warn", !!(v.problem || v.enabled === false));
  const waiting = v.waiting || 0;
  $("#syncLabel").textContent = v.busy ? "Syncing…" : waiting ? String(waiting) : "";
  const lines = [];
  if (v.enabled === false) lines.push(v.reason);
  else {
    if (v.busy) lines.push("Syncing…");
    else if (v.lastSync) lines.push("Synced " + agoText(v.lastSync));
    else lines.push("Not synced yet this session");
    if (waiting) lines.push(`${waiting} change${waiting > 1 ? "s" : ""} in the docs waiting to be sent` + (v.outside ? ` (${v.outside} made outside the Codex: sent when you press Sync)` : ""));
    if (v.behind) lines.push(`${v.behind} change${v.behind > 1 ? "s" : ""} from others to fetch`);
    if (v.problem) lines.push(v.problem);
    lines.push("Click to sync now");
  }
  btn.title = lines.join("\n");
  clearInterval(syncAgo);
  syncAgo = setInterval(() => syncState && !syncState.busy && showSync(syncState), 30000);
}
// Two people changed the same lines of a page: keep one version whole.
function clashBanner(v) {
  showBanner(`${v.clash.join(", ")} ${v.clash.length > 1 ? "were" : "was"} changed in the same place by you and someone else. Which version should stay?`, [
    { label: "Keep mine", cls: "btn-gold", run: () => settle("mine") },
    { label: "Use theirs", run: () => settle("theirs") },
    { label: "Decide later", run: hideBanner },
  ]);
}
async function settle(keep) {
  hideBanner();
  const v = await api("/api/sync/resolve", { method: "POST", body: JSON.stringify({ keep }) });
  showSync(v);
  if (v.problem) toast(v.problem, "bad"); else toast(keep === "mine" ? "Kept your version, and synced" : "Took their version, and synced", "good");
  if (S.path) openDoc(S.path, { push: false, keepScroll: true });
}

function agoText(t) {
  const s = (Date.now() - t) / 1000;
  return s < 60 ? "just now" : s < 3600 ? `${Math.round(s / 60)} min ago` : `${Math.round(s / 3600)} h ago`;
}
async function syncNow() {
  if (syncState?.enabled === false) { toast(syncState.reason, "bad"); return; }
  if (syncState?.busy) return;
  await save(false, { quiet: true }); // what's being typed goes too
  const v = await api("/api/sync?all=1&why=button", { method: "POST" });
  showSync(v);
  if (v.clash) clashBanner(v);
  else if (v.problem) toast(v.problem, "bad");
  else if (v.lastResult) {
    const { sent, got } = v.lastResult;
    toast(sent || got ? `Synced: ${sent ? `sent ${sent} change${sent > 1 ? "s" : ""}` : ""}${sent && got ? ", " : ""}${got ? `fetched ${got} from others` : ""}` : "Synced: everything is up to date", "good");
  }
}

/** Is the keyboard busy with text (the page, a field)? Then single-key shortcuts stay out of the way. */
const typing = () => !!document.activeElement?.closest?.(".ProseMirror, input, textarea, select, [contenteditable]");

// Open questions: every page's "Open questions" in one list, shown in the main window over the page
// (which stays loaded underneath).
function showStatus(push = true) {
  S.view = "status";
  document.body.classList.add("view-status");
  $("#main").classList.add("view-status");
  $("#statusView").classList.remove("hidden");
  $("#crumbs").replaceChildren(h("span", { class: "leaf" }, "Open questions"));
  document.title = "Open questions · Codex";
  if (push) history.pushState({ view: "status" }, "", "?view=questions");
  $$(".t-row.current").forEach((r) => r.classList.remove("current"));
  $(".t-special")?.classList.add("current");
  panels.renderStatus();
}
function hideStatus() {
  if (S.view !== "status") return;
  S.view = null;
  document.body.classList.remove("view-status");
  $("#main").classList.remove("view-status");
  $("#statusView").classList.add("hidden");
  $(".t-special")?.classList.remove("current");
}

let rightTab = ["outline", "links", "ai"].includes(localStorage.getItem("codex.rightTab")) ? localStorage.getItem("codex.rightTab") : "outline";
function showRightTab(tab) {
  rightTab = tab;
  localStorage.setItem("codex.rightTab", tab);
  $$("#outlineWin .tab").forEach((t) => t.classList.toggle("active", t.dataset.tab === tab));
  $("#outline").classList.toggle("hidden", tab !== "outline");
  $("#linksPane").classList.toggle("hidden", tab !== "links");
  $("#aiPane")?.classList.toggle("hidden", tab !== "ai");
  document.body.classList.toggle("right-ai", tab === "ai");
  $("#outlineWin .win-body").classList.toggle("ai-body", tab === "ai");
  if (tab === "links") panels.renderLinks();
  if (tab === "ai") { ai.shown(); requestAnimationFrame(() => notes.layout()); }
}
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

const panels = makePanels({ S, $, h, api, toast, openDoc: (...a) => openDoc(...a), findInPage: (q) => findInPage(q), setLinksCount });
window.__showStatus = showStatus;
const notes = makeNotes({
  S, $, $$, h, esc, api, toast, modal, ask, renderHtml,
  openDoc: (...a) => openDoc(...a), refreshDirty: () => refreshDirty(), currentHeadingPos, scrollToPos,
  setCommentCount, notesChanged, saveNow: () => { refreshDirty(); return S.dirty ? save(false, { quiet: true }) : Promise.resolve(true); }, banner: (text) => showBanner(text, [{ label: "OK", run: hideBanner }]),
  hasPage: (p) => S.fileSet.has(p), titleOf: (p) => S.files.find((f) => f.path === p)?.title || null,
  askAi: () => ai.focus({ selection: true }),
});

const layout = makeLayout({ $, h, notes, help: () => helpDialog(), presence: { on: () => presenceOn(), toggle: () => { localStorage.setItem("codex.presence", presenceOn() ? "off" : "on"); tellPresence(); } } });

// ----------------------------------------------------------------------------- Discord (src/discord-extras.js)

// Pasting a Discord conversation or an image (a file or a Discord image link).
const discordPaste = makePasteHandler({ S, api, toast });

// "Editing <page>" on your Discord profile, unless switched off in the logo's menu.
const presenceOn = () => localStorage.getItem("codex.presence") !== "off";
function tellPresence() {
  api("/api/presence", { method: "POST", body: JSON.stringify({ page: presenceOn() && S.path && S.view !== "status" ? S.path : null }) });
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
  const ro = v.canEdit === false;
  if (ro !== !!S.readOnly) { S.readOnly = ro; if (S.editor) applyMode(); }
} });
// Show changes: what the AI changed on the open page (src/ai-changes.js).
const aiChanges = makeAiChanges({ S, $, h, api, toast, scrollToPos });
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
  showRightTab(rightTab);
  notes.init();
  aiChanges.init();
  layout.init();
  api("/api/sync").then((v) => v.ok !== false && showSync(v));
  await loadTree();
  const params = new URLSearchParams(location.search);
  const first = params.get("p") || localStorage.getItem("codex.last") || DEFAULT_DOC;
  await openDoc(S.fileSet.has(first) ? first : DEFAULT_DOC, { push: false, hash: location.hash.slice(1) });
  if (/^(status|questions)$/.test(params.get("view") || "")) { history.replaceState({ path: S.path }, "", "?p=" + encodeURI(S.path)); showStatus(); }
  else history.replaceState({ path: S.path, hash: location.hash.slice(1) }, "", "?p=" + encodeURI(S.path) + location.hash);
  listen();
  welcome();
}

start();

// For checking in the browser's console: the state, and the merge.
window.__codex = {
  S, mergeEdit, renderHtml, editorMarkdown: () => editorMarkdown(), openDoc, notes, panels, toast,
  // The desktop app calls this before closing: save whatever is still being typed.
  flush: async () => { clearTimeout(autoTimer); refreshDirty(); return !S.dirty || S.noSave || (await save(false, { quiet: true })); },
};
