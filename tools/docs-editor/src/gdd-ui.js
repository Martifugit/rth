// The GDD in the editor: the card over each page that shows its frontmatter (type, status, components,
// groups, based-on), adding a component's standard section, and generated lists (query blocks) drawn
// live under their block. The frontmatter itself never goes into the editor: app.js keeps it aside
// (S.front) and writes it back in front of the page when saving.

import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { split, setField, make, STATUSES, slugOf } from "./frontmatter.js";
import { GDD, isMeta, inGdd, headingOf, nameOf, componentSection } from "./gdd.js";

/** `get()` gives the app's context: { S, h, api, openDoc, modal, toast, menu, setFront, editor() }. */
export function makeGddUi(get) {
  const refreshKey = new PluginKey("gddRefresh");
  let version = 0;                 // bumped when any page may have changed: lists are asked again
  const results = new Map();       // page + "\n" + query → result (or "pending")

  /** Redraw the card and the lists (after the frontmatter changed, or other pages did). */
  function refresh({ lists = false } = {}) {
    if (lists) { version++; results.clear(); }
    const ed = get().editor();
    if (ed && !ed.isDestroyed) ed.view.dispatch(ed.state.tr.setMeta(refreshKey, true));
  }

  // ----------------------------------------------------------------------------- the card

  const typeFor = (path) => get().S.types.find((t) => path.startsWith(GDD + "/" + t.folder + "/"));
  const pathOfSlug = (slug) => get().S.files.find((f) => slugOf(f.path) === slug)?.path || null;

  function card() {
    const { S, h } = get();
    if (!S.path || !inGdd(S.path) || isMeta(S.path)) return null;
    const fm = split(S.front + "\n").data;
    const ro = !!S.readOnly;
    const el = h("div", { class: "fm-card", contenteditable: "false" });
    if (!fm) {
      const t = typeFor(S.path);
      el.append(h("span", { class: "fm-none" }, "No type yet."));
      if (!ro) el.append(h("button", { class: "fm-btn", onclick: () => setType(t) }, t ? `Make it a ${t.type} page` : "Set its type…"));
      return el;
    }
    const type = S.types.find((t) => t.type === fm.type);
    const link = (slug, cls = "") => {
      const p = pathOfSlug(slug);
      return h("a", { class: "fm-link " + cls + (p ? "" : " fm-missing"), title: p || `No page “${slug}” yet: click to make it`, onclick: () => (p ? get().openDoc(p) : stubFor(slug)) }, slug);
    };
    el.append(...[
      h("span", { class: "fm-type" + (type ? "" : " fm-bad"), title: type ? type.covers : "Not a type in How the GDD Works" }, fm.type || "no type"),
      h("select", { class: "fm-status st-" + (fm.status || "none"), disabled: ro || null, title: "Status", onchange: (e) => setFront("status", e.target.value) },
        STATUSES.map((s) => h("option", { value: s, selected: s === fm.status || null }, s)),
        fm.status && !STATUSES.includes(fm.status) ? h("option", { value: fm.status, selected: true }, fm.status) : null),
      h("span", { class: "fm-label" }, "Components"),
      h("span", { class: "fm-chips" },
        fm.components.length ? fm.components.map((c) => h("a", { class: "fm-chip", title: "Go to its section", onclick: () => goToSection(c) }, c)) : h("span", { class: "fm-dim" }, "none"),
        ro ? null : h("button", { class: "fm-btn fm-add", title: "Add a component and its section", onclick: (e) => componentMenu(e, fm) }, "+")),
      fm.groups.length ? [h("span", { class: "fm-label" }, "Groups"), h("span", { class: "fm-chips" }, fm.groups.map((g) => link(g, "fm-chip")))] : null,
      fm["based-on"] ? [h("span", { class: "fm-label" }, "Based on"), link(fm["based-on"])] : null,
      ro ? null : h("button", { class: "fm-btn fm-more", title: "Groups, based on, and generated lists", onclick: (e) => moreMenu(e, fm) }, "⋯"),
    ].flat().filter(Boolean));
    return el;
  }

  function setFront(key, value) {
    get().setFront(setField(get().S.front, key, value));
    refresh();
  }
  function setType(t) {
    const pick = (type) => { get().setFront(make({ type: type.type, components: [], groups: [], "based-on": null, status: "stub" })); refresh(); };
    if (t) return pick(t);
    const { S, h, modal } = get();
    const sel = h("select", { class: "field" }, S.types.map((x) => h("option", { value: x.type }, x.type + "  |  " + x.examples)));
    const close = modal("Its type", [sel, h("div", { class: "modal-actions" },
      h("button", { class: "btn", onclick: () => close() }, "Cancel"),
      h("button", { class: "btn btn-gold", onclick: () => { close(); pick(S.types.find((x) => x.type === sel.value)); } }, "Set"))]);
  }

  function goToSection(name) {
    const ed = get().editor();
    let at = null;
    ed.state.doc.descendants((n, pos) => { if (at == null && n.type.name === "heading" && n.attrs.level === 2 && nameOf(n.textContent) === nameOf(name)) at = pos; return at == null; });
    if (at == null) { get().toast(`No “${headingOf(name)}” section yet`); return; }
    ed.commands.setTextSelection(at + 1);
    ed.view.domAtPos(at + 1).node?.parentElement?.scrollIntoView?.({ block: "start", behavior: "smooth" });
  }

  /** Add a component: into the frontmatter, and its standard section into the page (before Open questions). */
  function addComponent(fm, c) {
    if (fm.components.map(nameOf).includes(c.name)) { goToSection(c.name); return; }
    get().setFront(setField(get().S.front, "components", [...fm.components, c.name]));
    const ed = get().editor();
    let at = ed.state.doc.content.size;
    ed.state.doc.forEach((n, pos) => { if (n.type.name === "heading" && n.attrs.level === 2 && /^open questions$/i.test(n.textContent.trim()) && at === ed.state.doc.content.size) at = pos; });
    ed.chain().insertContentAt(at, componentSection(c) + "\n").run();
    refresh();
    requestAnimationFrame(() => goToSection(c.name));
  }

  function componentMenu(ev, fm) {
    const { S } = get();
    const have = new Set(fm.components.map(nameOf));
    const fits = (c) => c.foundOn.includes(fm.type) || c.foundOn.some((x) => /almost anything/.test(x));
    const item = (c) => [(have.has(c.name) ? "✓ " : "") + c.name + "  —  " + c.answers, () => addComponent(fm, c)];
    const usual = S.components.filter(fits), rest = S.components.filter((c) => !fits(c));
    get().menu(ev, [...usual.map(item), usual.length && rest.length ? "-" : null, ...rest.map(item)].filter(Boolean));
  }

  function moreMenu(ev, fm) {
    const { S, h, modal } = get();
    const edit = () => {
      const groups = h("input", { class: "field", value: fm.groups.join(", "), placeholder: "trees, goblins" });
      const based = h("input", { class: "field", value: fm["based-on"] || "", placeholder: "the page it's a variant of (its file name)" });
      const close = modal("Groups and variant", [h("label", {}, "Groups (the hub pages it belongs to)"), groups, h("label", {}, "Based on"), based,
        h("div", { class: "modal-actions" }, h("button", { class: "btn", onclick: () => close() }, "Cancel"), h("button", { class: "btn btn-gold", onclick: () => {
          let front = setField(S.front, "groups", groups.value.split(",").map((x) => x.trim()).filter(Boolean));
          front = setField(front, "based-on", based.value.trim() || null);
          get().setFront(front); close(); refresh();
        } }, "Save"))]);
      setTimeout(() => groups.focus(), 0);
    };
    const list = (q) => () => insertQuery(q);
    get().menu(ev, [
      ["Groups and based on…", edit],
      "-",
      fm.type === "group" && ["Insert list: members of this group", list("groups: this")],
      fm.type === "skill" && ["Insert list: training (every action with this skill)", list("action.skill: this\nsort: level")],
      ["Insert list: what links here", list("links-to: this")],
      ["Insert list: a query of your own…", list("type: \nstatus: ")],
    ].filter(Boolean));
  }

  function insertQuery(q) {
    const ed = get().editor();
    ed.chain().focus().insertContent("\n```query\n" + q + "\n```\n").run();
  }

  /** A page for a name that has none yet: the new page dialog, filled in. */
  function stubFor(slug) {
    get().newPage({ title: slug.replace(/-/g, " ").replace(/^./, (c) => c.toUpperCase()) });
  }

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
    const dot = (st) => h("span", { class: "st-dot st-" + (st || "none"), title: st || "no status" });
    if (!r.rows.length) box.append(h("div", { class: "q-note" }, "Nothing yet. Pages appear here as they're written."));
    else if (r.kind === "pages") box.append(h("ul", { class: "q-list" }, r.rows.map((p) => h("li", {}, dot(p.status), open(p), p.type ? h("span", { class: "q-type" }, p.type) : null))));
    else {
      const named = r.rows.some((x) => x.name);
      box.append(h("table", { class: "q-table" },
        h("thead", {}, h("tr", {}, h("th", {}, "Page"), named ? h("th", {}, headingOf(r.component)) : null, r.columns.map((c) => h("th", {}, c.replace(/^./, (x) => x.toUpperCase()))))),
        h("tbody", {}, r.rows.map((x) => h("tr", {}, h("td", {}, open(x)), named ? h("td", {}, x.name || "") : null, r.columns.map((c) => h("td", {}, plainText(x.fields[c] || ""))))))));
    }
    box.append(h("div", { class: "q-note small" }, `Generated from the pages: ${r.rows.length} found`));
    return box;
  }
  const plainText = (md) => md.replace(/\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]/g, "");

  function decorations(doc) {
    const decos = [];
    const c = card();
    if (c) decos.push(Decoration.widget(0, () => card(), { side: -1, key: "fm:" + get().S.front + ":" + get().S.types.length + ":" + get().S.files.length + ":" + !!get().S.readOnly, ignoreSelection: true, stopEvent: () => true }));
    doc.descendants((node, pos) => {
      if (node.type.name !== "codeBlock" || node.attrs.language !== "query") return node.isBlock && node.type.name !== "codeBlock";
      const key = get().S.path + "\n" + node.textContent;
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

  return { Extension: Extension_, refresh, stubFor, typeFor };
}
