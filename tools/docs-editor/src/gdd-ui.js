// The GDD in the editor: generated lists (query blocks) drawn live under their block, and adding a
// component (its snippet from templates/, and its name in the frontmatter). The frontmatter itself
// never goes into the editor: app.js keeps it aside (S.front) and writes it back in front of the page.

import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { split, setField, slugOf } from "./frontmatter.js";
import { TEMPLATES, headingOf, nameOf, componentSection } from "./gdd.js";

/** `get()` gives the app's context: { S, h, api, openDoc, toast, setFront, editor() }. */
export function makeGddUi(get) {
  const refreshKey = new PluginKey("gddRefresh");
  let version = 0;                 // bumped when any page may have changed: lists are asked again
  const results = new Map();       // page + "\n" + query → result (or "pending")

  /** Redraw the lists (after other pages changed, with `lists`). */
  function refresh({ lists = false } = {}) {
    if (lists) { version++; results.clear(); }
    const ed = get().editor();
    if (ed && !ed.isDestroyed) ed.view.dispatch(ed.state.tr.setMeta(refreshKey, true));
  }

  // ----------------------------------------------------------------------------- components

  /** The page's frontmatter fields, or null. */
  const front = () => split(get().S.front + "\n").data;

  /**
   * Add a component where the cursor is: its snippet (templates/<name>.md, else its heading and fields)
   * into the page, and its name into the frontmatter's components.
   */
  async function addComponent(c) {
    const { S, api } = get();
    const fm = front();
    if (fm && !fm.components.map(nameOf).includes(c.name)) get().setFront(setField(S.front, "components", [...fm.components, c.name]));
    const r = await api("/api/doc?path=" + encodeURIComponent(TEMPLATES + "/" + c.name + ".md"));
    const snippet = (r.ok ? r.content : componentSection(c)).replace(/^\s+|\s+$/g, "") + "\n";
    const ed = get().editor();
    ed.chain().focus().insertContent(snippet).run();
  }

  /** Is the component already on this page (its section or its name in the frontmatter)? */
  function has(c) {
    if (front()?.components.map(nameOf).includes(c.name)) return true;
    let found = false;
    get().editor()?.state.doc.forEach((n) => { if (n.type.name === "heading" && n.attrs.level === 2 && nameOf(n.textContent) === c.name) found = true; });
    return found;
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
    if (!r.rows.length) box.append(h("div", { class: "q-note" }, "Nothing yet. Pages appear here as they're written."));
    else if (r.kind === "pages") box.append(h("ul", { class: "q-list" }, r.rows.map((p) => h("li", {}, open(p), p.type ? h("span", { class: "q-type" }, p.type) : null))));
    else {
      const named = r.rows.some((x) => x.name);
      box.append(h("table", { class: "q-table" },
        h("thead", {}, h("tr", {}, h("th", {}, "Page"), named ? h("th", {}, headingOf(r.component)) : null, r.columns.map((c) => h("th", {}, c.replace(/^./, (x) => x.toUpperCase()))))),
        h("tbody", {}, r.rows.map((x) => h("tr", {}, h("td", {}, open(x)), named ? h("td", {}, x.name || "") : null, r.columns.map((c) => h("td", {}, plainText(x.fields[c] || ""))))))));
    }
    return box;
  }
  const plainText = (md) => md.replace(/\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]/g, "");

  function decorations(doc) {
    const decos = [];
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

  return { Extension: Extension_, refresh, addComponent, has, front };
}
