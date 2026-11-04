// The GDD in the editor, after the OSRS Wiki's infobox but much slimmer: the top of a page answers
// "what is this, is it settled, what does it belong to" at a glance, and the bottom shows what other
// pages say about it, computed and kept apart from what the author wrote.
//
// - A label beside the title: the page's type (Entity, Location).
// - A strip under the title: the status as a coloured badge (click to change it), "Variant of …" when
//   it has one, the groups as chips (a picker adds and removes them), and the components as jump links
//   to their sections, with "+ component" to add one from its template.
// - A cut page's title is struck through.
// - At the bottom, a muted "Links to this page" box: Dropped by, Found in, Appears in…, a group's
//   members, a skill's training.
// - Generated lists (query blocks) drawn under their block.
//
// The frontmatter itself never goes into the editor: app.js keeps it aside (S.front) and writes it back
// in front of the page. Everything here edits it through setField, so nobody types YAML.

import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { split, setField, make, slugOf, STATUSES } from "./frontmatter.js";
import { GDD, TEMPLATES, isMeta, inGdd, headingOf, nameOf, componentSection } from "./gdd.js";

const cap = (s) => String(s || "").replace(/^./, (c) => c.toUpperCase());

/** `get()` gives the app's context: { S, h, api, toast, modal, openDoc, editor(), menu(ev, items), setFront(front), newPage(prefill) }. */
export function makeGddUi(get) {
  const refreshKey = new PluginKey("gddRefresh");
  let version = 0;                 // bumped when any page may have changed: computed parts are asked again
  const results = new Map();       // query lists: page + "\n" + query → result (or "pending")
  const generated = new Map();     // the bottom box: page → result (or "pending")

  /** Redraw the strip and the box (after the frontmatter changed), and with `lists`, ask again for what's computed. */
  function refresh({ lists = false } = {}) {
    if (lists) { version++; results.clear(); generated.clear(); }
    const ed = get().editor();
    if (ed && !ed.isDestroyed) ed.view.dispatch(ed.state.tr.setMeta(refreshKey, true));
  }

  const front = () => split(get().S.front + "\n").data;
  const shown = () => { const p = get().S.path; return !!p && inGdd(p) && !isMeta(p); };
  const typeOfFolder = (path) => get().S.types.find((t) => path.startsWith(GDD + "/" + t.folder + "/"));
  const pageOfSlug = (slug) => get().S.files.find((f) => slugOf(f.path) === slug && !f.path.startsWith(TEMPLATES + "/")) || null;
  const setFront = (key, value) => { get().setFront(setField(get().S.front, key, value)); refresh(); };

  // ----------------------------------------------------------------------------- the title and the strip

  function typeLabel(fm) {
    const { S, h } = get();
    const t = S.types.find((x) => x.type === fm.type);
    return h("span", { class: "gdd-type" + (t ? "" : " bad"), contenteditable: "false", title: t ? t.covers : "Not a type in How the GDD Works" }, cap(fm.type || "no type"));
  }

  function strip(fm) {
    const { S, h } = get();
    const ro = !!S.readOnly;
    const el = h("div", { class: "gdd-strip", contenteditable: "false" });
    if (!fm) {
      const t = typeOfFolder(S.path);
      el.append(h("span", { class: "gdd-dim" }, "This page has no header yet."),
        ro || !t ? null : h("button", { class: "gdd-link", onclick: () => { get().setFront(make({ type: t.type, components: [], groups: [], "based-on": null, status: "stub" })); refresh(); } }, `Make it a ${t.type} page`));
      return el;
    }
    // Status: the most important signal on the page.
    const st = fm.status || "none";
    el.append(h("button", { class: "gdd-status st-" + st, disabled: ro || null, title: ro ? "Status" : "Change the status",
      onclick: (e) => get().menu(e, STATUSES.map((s) => [h("span", { class: "gdd-status-item st-" + s }, cap(s)), () => setFront("status", s)])) }, fm.status ? cap(fm.status) : "No status"));
    // Groups: chips to the group pages, and a picker.
    const chip = (g) => {
      const p = pageOfSlug(g);
      return h("a", { class: "gdd-chip" + (p ? "" : " todo"), title: p ? p.path : `No page for “${g}” yet: click to write it`,
        onclick: () => (p ? get().openDoc(p.path) : get().newPage({ path: GDD + "/" + (S.types.find((t) => t.type === "group")?.folder || "groups") + "/" + g + ".md", title: cap(g.replace(/-/g, " ")) })) }, p?.title || cap(g.replace(/-/g, " ")));
    };
    el.append(...fm.groups.map(chip));
    if (!ro) el.append(h("button", { class: "gdd-add", title: "Groups this page belongs to, and what it's a variant of", onclick: (e) => groupPicker(e, fm) }, fm.groups.length ? "+" : "+ group"));
    // Components: the page's section navigation.
    const sections = h("span", { class: "gdd-sections" },
      fm.components.map((c) => h("a", { class: "gdd-jump", title: `Go to ${headingOf(c)}`, onclick: () => goToSection(c) }, headingOf(c))),
      ro ? null : h("button", { class: "gdd-add", title: "Add a component: its section, from its template", onclick: (e) => componentMenu(e, fm) }, "+ component"));
    el.append(sections);
    return el;
  }

  function variantLine(fm) {
    const { h } = get();
    const p = pageOfSlug(fm["based-on"]);
    return h("div", { class: "gdd-variant", contenteditable: "false" }, "Variant of ",
      h("a", { class: p ? "" : "todo", onclick: () => (p ? get().openDoc(p.path) : get().newPage({ title: cap(fm["based-on"].replace(/-/g, " ")) })) }, p?.title || fm["based-on"]));
  }

  function groupPicker(ev, fm) {
    const { S } = get();
    const folder = GDD + "/" + (S.types.find((t) => t.type === "group")?.folder || "groups") + "/";
    const groups = S.files.filter((f) => f.path.startsWith(folder)).sort((a, b) => String(a.title).localeCompare(String(b.title)));
    const have = new Set(fm.groups);
    const toggle = (slug) => setFront("groups", have.has(slug) ? fm.groups.filter((g) => g !== slug) : [...fm.groups, slug]);
    get().menu(ev, [
      ...groups.map((g) => [(have.has(slugOf(g.path)) ? "✓ " : "") + (g.title || slugOf(g.path)), () => toggle(slugOf(g.path))]),
      ...fm.groups.filter((g) => !groups.some((x) => slugOf(x.path) === g)).map((g) => ["✓ " + cap(g.replace(/-/g, " ")) + " (not written yet)", () => toggle(g)]),
      groups.length || fm.groups.length ? "-" : null,
      ["New group…", () => ask("New group", "Its name, plural: Goblins, Trees", "", (name) => toggle(slugify(name)))],
      "-",
      [fm["based-on"] ? "Variant of…  (now " + fm["based-on"] + ")" : "Variant of…", () => variantPicker(fm)],
    ].filter(Boolean));
  }

  function variantPicker(fm) {
    const { S, h } = get();
    const list = h("datalist", { id: "gddPages" }, S.files.filter((f) => !isMeta(f.path) && f.path !== S.path).map((f) => h("option", { value: slugOf(f.path) }, f.title || "")));
    ask("Variant of", "The page this one is a variant of (empty for none)", fm["based-on"] || "", (v) => setFront("based-on", v ? slugify(v) : null), list);
  }

  const slugify = (s) => String(s).trim().normalize("NFKD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/['’]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

  function ask(title, hint, value, done, extra = null) {
    const { h, modal } = get();
    const input = h("input", { class: "field", value, placeholder: hint, list: extra ? "gddPages" : null });
    const ok = () => { close(); done(input.value.trim()); };
    input.addEventListener("keydown", (e) => { if (e.key === "Enter") ok(); });
    const close = modal(title, [h("label", {}, hint), input, extra, h("div", { class: "modal-actions" },
      h("button", { class: "btn", onclick: () => close() }, "Cancel"), h("button", { class: "btn btn-gold", onclick: ok }, "OK"))]);
    setTimeout(() => input.focus(), 0);
  }

  function componentMenu(ev, fm) {
    const { S } = get();
    const have = new Set(fm.components.map(nameOf));
    const fits = (c) => c.foundOn.includes(fm.type) || c.foundOn.some((x) => /almost anything/.test(x));
    const item = (c) => [(have.has(c.name) ? "✓ " : "") + headingOf(c.name) + "  ·  " + c.answers, () => (have.has(c.name) ? goToSection(c.name) : addComponent(c))];
    const usual = S.components.filter(fits), rest = S.components.filter((c) => !fits(c));
    get().menu(ev, [...usual.map(item), usual.length && rest.length ? "-" : null, ...rest.map(item)].filter(Boolean));
  }

  function goToSection(name) {
    const ed = get().editor();
    let at = null;
    ed.state.doc.forEach((n, pos) => { if (at == null && n.type.name === "heading" && n.attrs.level === 2 && nameOf(n.textContent) === nameOf(name)) at = pos; });
    if (at == null) { get().toast(`No “${headingOf(name)}” section yet`); return; }
    const dom = ed.view.nodeDOM(at);
    if (dom?.scrollIntoView) dom.scrollIntoView({ block: "start", behavior: "smooth" });
    ed.commands.setTextSelection(at + 1);
  }

  /**
   * Add a component: its name into the frontmatter, and its section from templates/<name>.md (else its
   * heading and fields) at the end of the page, before Open questions.
   */
  async function addComponent(c) {
    const { S, api } = get();
    const fm = front();
    if (fm && !fm.components.map(nameOf).includes(c.name)) get().setFront(setField(S.front, "components", [...fm.components, c.name]));
    const r = await api("/api/doc?path=" + encodeURIComponent(TEMPLATES + "/" + c.name + ".md"));
    const snippet = (r.ok ? r.content : componentSection(c)).replace(/^\s+|\s+$/g, "") + "\n";
    const ed = get().editor();
    let at = ed.state.doc.content.size;
    ed.state.doc.forEach((n, pos) => { if (n.type.name === "heading" && n.attrs.level === 2 && /^open questions$/i.test(n.textContent.trim()) && at === ed.state.doc.content.size) at = pos; });
    ed.chain().insertContentAt(at, snippet).run();
    refresh();
    requestAnimationFrame(() => goToSection(c.name));
  }

  // ----------------------------------------------------------------------------- the bottom box

  function linksBox() {
    const { S, h, api } = get();
    const page = S.path;
    const r = generated.get(page);
    if (!r) {
      generated.set(page, "pending");
      const v = version;
      api("/api/generated?page=" + encodeURIComponent(page)).then((x) => { if (v === version && x.ok) { generated.set(page, x); refresh(); } });
    }
    const box = h("aside", { class: "gdd-box", contenteditable: "false" });
    if (!r || r === "pending") return box;
    const open = (p) => h("a", { onclick: () => get().openDoc(p.path), title: p.path }, p.title || slugOf(p.path));
    const row = (label, pages) => h("div", { class: "gdd-box-row" }, h("span", { class: "gdd-box-label" }, label), h("span", { class: "gdd-box-pages" }, pages.flatMap((p, i) => (i ? [", ", open(p)] : [open(p)]))));
    const parts = [];
    if (r.members) parts.push(row("Members", r.members));
    if (r.training) {
      const cols = r.training.columns.filter((c) => ["level", "verb", "xp", "tool"].includes(c));
      parts.push(h("div", { class: "gdd-box-row" }, h("span", { class: "gdd-box-label" }, "Trained by"),
        h("table", { class: "gdd-box-table" }, r.training.rows.map((x) => h("tr", {}, h("td", {}, open(x)), x.name ? h("td", {}, x.name) : null, cols.map((c) => h("td", {}, plain(x.fields[c] || ""))))))));
    }
    for (const g of r.links) parts.push(row(g.label, g.pages));
    if (!parts.length) parts.push(h("div", { class: "gdd-dim" }, "Nothing links here yet."));
    box.append(h("div", { class: "gdd-box-title" }, "Links to this page"), ...parts);
    return box;
  }
  const plain = (md) => md.replace(/\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]/g, "");

  // ----------------------------------------------------------------------------- generated lists

  function listView(node) {
    const { S, h, api } = get();
    const q = node.textContent;
    const key = S.path + "\n" + q;
    const box = h("div", { class: "q-result", contenteditable: "false" });
    const r = results.get(key);
    if (!r) {
      results.set(key, "pending");
      const v = version;
      api("/api/query", { method: "POST", body: JSON.stringify({ page: S.path, q }) }).then((x) => {
        if (v !== version) return;
        results.set(key, x.ok ? x : { error: x.error || "couldn't run it" });
        refresh();
      });
    }
    if (!r || r === "pending") { box.append(h("div", { class: "q-note" }, "Gathering…")); return box; }
    if (r.error) { box.append(h("div", { class: "q-note bad" }, r.error)); return box; }
    const open = (p) => h("a", { class: "q-page", onclick: () => get().openDoc(p.path), title: p.path }, p.title || slugOf(p.path));
    if (!r.rows.length) box.append(h("div", { class: "q-note" }, "Nothing yet. Pages appear here as they're written."));
    else if (r.kind === "pages") box.append(h("ul", { class: "q-list" }, r.rows.map((p) => h("li", {}, open(p), p.type ? h("span", { class: "q-type" }, p.type) : null))));
    else {
      const named = r.rows.some((x) => x.name);
      box.append(h("table", { class: "q-table" },
        h("thead", {}, h("tr", {}, h("th", {}, "Page"), named ? h("th", {}, headingOf(r.component)) : null, r.columns.map((c) => h("th", {}, cap(c))))),
        h("tbody", {}, r.rows.map((x) => h("tr", {}, h("td", {}, open(x)), named ? h("td", {}, x.name || "") : null, r.columns.map((c) => h("td", {}, plain(x.fields[c] || ""))))))));
    }
    return box;
  }

  // ----------------------------------------------------------------------------- decorations

  function decorations(doc) {
    const decos = [];
    const { S } = get();
    if (shown()) {
      const fm = front();
      const key = S.front + "|" + S.types.length + "|" + S.components.length + "|" + S.files.length + "|" + !!S.readOnly;
      let title = null;
      doc.forEach((n, pos) => { if (!title && n.type.name === "heading" && n.attrs.level === 1) title = { n, pos }; });
      if (title) {
        if (fm?.type) decos.push(Decoration.widget(title.pos + title.n.nodeSize - 1, () => typeLabel(fm), { side: 1, key: "type:" + key, ignoreSelection: true, stopEvent: () => true }));
        if (fm?.status === "cut") decos.push(Decoration.node(title.pos, title.pos + title.n.nodeSize, { class: "gdd-cut" }));
      }
      const at = title ? title.pos + title.n.nodeSize : 0;
      if (fm?.["based-on"]) decos.push(Decoration.widget(at, () => variantLine(fm), { side: -2, key: "variant:" + key, ignoreSelection: true, stopEvent: () => true }));
      decos.push(Decoration.widget(at, () => strip(fm), { side: -1, key: "strip:" + key, ignoreSelection: true, stopEvent: () => true }));
      const g = generated.get(S.path);
      decos.push(Decoration.widget(doc.content.size, () => linksBox(), { side: 1, key: "box:" + version + ":" + S.path + ":" + (g ? (g === "pending" ? "p" : "r") : "n"), ignoreSelection: true, stopEvent: () => true }));
    }
    doc.descendants((node, pos) => {
      if (node.type.name !== "codeBlock" || node.attrs.language !== "query") return node.isBlock && node.type.name !== "codeBlock";
      const key = S.path + "\n" + node.textContent;
      const r = results.get(key);
      decos.push(Decoration.widget(pos + node.nodeSize, () => listView(node), { side: 1, key: "q:" + version + ":" + key + ":" + (r ? (r === "pending" ? "p" : "r") : "n"), ignoreSelection: true, stopEvent: () => true }));
      return false;
    });
    return DecorationSet.create(doc, decos);
  }

  const Extension_ = Extension.create({
    name: "gddUi",
    addProseMirrorPlugins() {
      return [new Plugin({
        key: refreshKey,
        state: {
          init: (_, state) => decorations(state.doc),
          apply: (tr, old) => (tr.docChanged || tr.getMeta(refreshKey) ? decorations(tr.doc) : old.map(tr.mapping, tr.doc)),
        },
        props: { decorations(state) { return this.getState(state); } },
      })];
    },
  });

  return { Extension: Extension_, refresh, addComponent, front };
}
