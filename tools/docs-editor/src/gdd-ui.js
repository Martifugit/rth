// The GDD in the editor, after the OSRS Wiki's infobox but much slimmer: the top of a page answers
// "what is this, is it settled, what does it belong to" at a glance, and the bottom shows what other
// pages say about it, computed and kept apart from what the author wrote.
//
// - Above the title, one slim row of badges: the type, the status (click to change it), "Variant of …",
//   the groups (click to open one) and the components (click to jump to the section). The row is the
//   summary of the page's settings; the cog at its end opens them.
// - Page settings (the cog at the end of the row): status; components as a checklist, the ones
//   usual for the type first with their question and the rest under More; groups and what it's a
//   variant of; and the type, whose change moves the file and is deliberate.
// - Sections keep a fixed order: core headings, then components in the canonical order (How the GDD
//   Works' tables), then Open questions. Adding a component puts its section in its place; removing
//   one with something written in it asks whether to move the text to Open questions or delete it.
// - A component that repeats (actions, trades) gets "+ Add action" at the end of its own section.
// - A cut page's title is struck through.
//
// The frontmatter itself never goes into the editor: app.js keeps it aside (S.front) and writes it back
// in front of the page. Everything here edits it through setField, so nobody types YAML.

import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Fragment } from "@tiptap/pm/model";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { split, setField, make, slugOf, STATUSES } from "./frontmatter.js";
import { failed } from "./plain-errors.js";
import { pagePicker } from "./page-picker.js";
import {
  GDD,
  TEMPLATES,
  isMeta,
  inGdd,
  headingOf,
  nameOf,
  folderPath,
  typeAt,
  componentsFor,
  insertIndex,
  canonicalOrder,
  inOrder,
  hasContent,
  sectionText,
  instanceText,
  OPEN_QUESTIONS,
} from "./gdd.js";

// A widget that fails to draw must never take the editor down with it (the page would no longer be
// the page it says it is): it draws nothing and says why in the console.
function safe(draw) {
  return () => {
    try {
      return draw() || document.createElement("span");
    } catch (e) {
      console.error("[gdd] couldn't draw part of the page", e);
      return document.createElement("span");
    }
  };
}

const cap = (s) => String(s || "").replace(/^./, (c) => c.toUpperCase());
const words = (slug) => cap(String(slug || "").replace(/-/g, " "));
export const slugify = (s) =>
  String(s)
    .trim()
    .normalize("NFKD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase()
    .replace(/['’]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
const STATUS_HINT = {
  stub: "Named, barely written",
  draft: "Being written",
  agreed: "Settled by the team",
  cut: "Not in the game",
};

/**
 * `get()` gives the app's context: { S, h, api, toast, modal, ask, openDoc, editor(), menu(ev, items),
 * setFront(front), newPage(prefill), save(), loadTree(), typeIcon(type) }.
 */
export function makeGddUi(get) {
  const refreshKey = new PluginKey("gddRefresh");
  let version = 0; // bumped when any page may have changed: computed parts are asked again
  const results = new Map(); // query lists: page + "\n" + query → result (or "pending")
  const templates = new Map(); // component → its template's text (null: none yet)
  let redrawSettings = null; // the open settings dialog's redraw, while it's open
  let closeSettings = null; // and its close

  /** Redraw the strip and the box (after the frontmatter changed), and with `lists`, ask again for what's computed. */
  function refresh({ lists = false } = {}) {
    if (lists) {
      version++;
      results.clear();
    }
    const ed = get().editor();
    if (ed && !ed.isDestroyed)
      ed.view.dispatch(ed.state.tr.setMeta(refreshKey, true));
    redrawSettings?.();
  }

  const front = () => split(get().S.front + "\n").data;
  /** Is the open page a GDD page with settings (not the start page, a template or the log)? */
  const shown = () => {
    const p = get().S.path;
    return !!p && inGdd(p) && !isMeta(p);
  };
  const typeOfFolder = (path) => typeAt(get().S.types, path);
  const typeOf = (fm) => get().S.types.find((t) => t.type === fm?.type) || null;
  const groupFolder = () => {
    const g = get().S.types.find((t) => t.type === "group");
    return g ? folderPath(g) : GDD + "/groups";
  };
  const pageOfSlug = (slug) =>
    get().S.files.find(
      (f) => slugOf(f.path) === slug && !f.path.startsWith(TEMPLATES + "/"),
    ) || null;
  const editable = () => {
    const { S } = get();
    return !S.readOnly && S.mode === "edit";
  };
  const setFront = (key, value) => {
    get().setFront(setField(get().S.front, key, value));
    refresh();
  };
  /** The page's components in the canonical order (any the system doesn't know at the end, as written). */
  const sorted = (names) => {
    const all = get().S.components.map((c) => c.name);
    const at = (n) => {
      const i = all.indexOf(nameOf(n));
      return i < 0 ? 1e6 : i;
    };
    return [...names].sort((a, b) => at(a) - at(b));
  };

  async function template(name) {
    if (!templates.has(name)) {
      const r = await get().api(
        "/api/doc?path=" + encodeURIComponent(TEMPLATES + "/" + name + ".md"),
      );
      templates.set(name, r.ok ? r.content : null);
    }
    return templates.get(name);
  }

  // ----------------------------------------------------------------------------- the page's sections

  /** The page's H2 sections, top level: [{ heading, name, pos, body, end }] (body: after the heading; end: the next H2, or the end). */
  function sections(doc) {
    const out = [];
    doc.forEach((n, pos) => {
      if (n.type.name !== "heading" || n.attrs.level !== 2) return;
      if (out.length) out[out.length - 1].end = pos;
      out.push({
        heading: n.textContent,
        name: nameOf(n.textContent),
        pos,
        body: pos + n.nodeSize,
        end: doc.content.size,
      });
    });
    return out;
  }
  const sectionOf = (doc, name) =>
    sections(doc).find((s) => s.name === nameOf(name)) || null;

  /** The text of a section's blocks, line by line (its heading left out). */
  function linesOf(doc, sec) {
    const lines = [];
    doc.nodesBetween(sec.body, sec.end, (n) => {
      if (n.type.name === "tableHeader") return false; // a table's column names aren't writing
      if (n.isTextblock) {
        lines.push(n.textContent);
        return false;
      }
      return true;
    });
    return lines;
  }

  function goToSection(name) {
    const ed = get().editor();
    const sec = sectionOf(ed.state.doc, name);
    if (!sec) {
      get().toast(`No “${headingOf(name)}” section yet`);
      return;
    }
    const dom = ed.view.nodeDOM(sec.pos);
    if (dom?.scrollIntoView)
      dom.scrollIntoView({ block: "start", behavior: "smooth" });
    ed.commands.setTextSelection(sec.pos + 1);
  }

  /** Where a new section `name` goes: before the first section that comes after it in the fixed order. */
  function placeFor(doc, name) {
    const secs = sections(doc);
    const i = insertIndex(
      secs.map((s) => s.heading),
      name,
      typeOf(front()),
      get().S.components,
    );
    return i < secs.length ? secs[i].pos : doc.content.size;
  }

  // ----------------------------------------------------------------------------- components

  /** Add a component: its name into the frontmatter, and its section from templates/<name>.md in its place. */
  async function addComponent(c, { jump = true } = {}) {
    const fm = front();
    if (!fm) return;
    if (!fm.components.map(nameOf).includes(c.name))
      setFront("components", sorted([...fm.components, c.name]));
    const ed = get().editor();
    if (!sectionOf(ed.state.doc, c.name)) {
      const snippet = sectionText(c, await template(c.name));
      ed.chain().insertContentAt(placeFor(ed.state.doc, c.name), snippet).run();
    }
    refresh();
    if (jump) requestAnimationFrame(() => goToSection(c.name));
  }

  /**
   * Remove a component. Its section goes with it when nothing is written there; otherwise the author
   * chooses: move the text into Open questions, or delete it. Never deleted without asking.
   */
  async function removeComponent(c) {
    const fm = front();
    if (!fm) return;
    const ed = get().editor();
    const drop = () =>
      setFront(
        "components",
        fm.components.filter((x) => nameOf(x) !== c.name),
      );
    const sec = sectionOf(ed.state.doc, c.name);
    if (!sec) return drop();
    if (hasContent(linesOf(ed.state.doc, sec))) {
      // One dialog at a time: settings step aside while asking, and come back after.
      const reopen = !!closeSettings;
      if (reopen) closeSettings();
      const esc = (x) =>
        String(x).replace(
          /[&<>"]/g,
          (ch) =>
            ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[ch],
        );
      const choice = await get().ask(
        `Remove ${headingOf(c.name)}?`,
        `The <b>${esc(sec.heading)}</b> section has writing in it. Move it into <b>${OPEN_QUESTIONS}</b> to keep it, or delete it with the section.`,
        [
          { key: "cancel", label: "Cancel" },
          { key: "delete", label: "Delete it", cls: "btn-danger" },
          { key: "move", label: "Move to Open questions", cls: "btn-gold" },
        ],
      );
      if (choice === "move" || choice === "delete") {
        const now = sectionOf(ed.state.doc, c.name); // the page may have changed while asking
        if (now && choice === "move") moveToOpenQuestions(now);
        else if (now) ed.view.dispatch(ed.state.tr.delete(now.pos, now.end));
        drop();
      }
      if (reopen) openSettings();
      return;
    } else ed.view.dispatch(ed.state.tr.delete(sec.pos, sec.end));
    drop();
  }

  /** A section's text moved to the end of Open questions (made at the end of the page if there's none), under a line saying where it came from. */
  function moveToOpenQuestions(sec) {
    const ed = get().editor();
    const { state } = ed,
      { schema } = state;
    // What was written, without the template's fields left empty ("- Weaknesses:").
    const keep = (n) =>
      !(n.type.name === "listItem" && !hasContent([n.textContent]));
    const kept = [];
    state.doc.slice(sec.body, sec.end).content.forEach((n) => {
      if (!/list/i.test(n.type.name)) {
        kept.push(n);
        return;
      }
      const items = [];
      n.forEach((x) => {
        if (keep(x)) items.push(x);
      });
      if (items.length) kept.push(n.copy(Fragment.from(items)));
    });
    const body = Fragment.from(kept);
    const tr = state.tr.delete(sec.pos, sec.end);
    let oq = sections(tr.doc).find((s) => s.name === nameOf(OPEN_QUESTIONS));
    if (!oq) {
      tr.insert(
        tr.doc.content.size,
        schema.nodes.heading.create({ level: 2 }, schema.text(OPEN_QUESTIONS)),
      );
      oq = sections(tr.doc).find((s) => s.name === nameOf(OPEN_QUESTIONS));
    }
    const em = schema.marks.italic ? [schema.marks.italic.create()] : [];
    const from = schema.nodes.paragraph.create(
      null,
      schema.text(`From the ${sec.heading} section, when it was removed:`, em),
    );
    tr.insert(oq.end, Fragment.from(from).append(body));
    ed.view.dispatch(tr);
  }

  /** Put the page's sections in the fixed order (a section of the author's own moves with the one before it). */
  function reorder() {
    const ed = get().editor();
    const doc = ed.state.doc;
    const secs = sections(doc);
    if (!secs.length) return;
    const order = canonicalOrder(
      secs.map((s) => s.heading),
      typeOf(front()),
      get().S.components,
    );
    const parts = secs.map((s) => doc.slice(s.pos, s.end).content);
    let frag = Fragment.empty;
    for (const i of order) frag = frag.append(parts[i]);
    ed.view.dispatch(
      ed.state.tr.replaceWith(secs[0].pos, doc.content.size, frag),
    );
    refresh();
  }
  const ordered = () => {
    const ed = get().editor();
    return (
      !ed ||
      inOrder(
        sections(ed.state.doc).map((s) => s.heading),
        typeOf(front()),
        get().S.components,
      )
    );
  };

  /** The type's core headings the page doesn't have, each put in its place. */
  function addCoreHeadings() {
    const ed = get().editor(),
      t = typeOf(front());
    if (!t) return;
    for (const hd of t.headings)
      if (!sectionOf(ed.state.doc, hd))
        ed.chain()
          .insertContentAt(placeFor(ed.state.doc, hd), "## " + hd + "\n")
          .run();
    refresh();
  }
  const missingCore = () => {
    const ed = get().editor(),
      t = typeOf(front());
    return t && ed
      ? t.headings.filter((hd) => !sectionOf(ed.state.doc, hd))
      : [];
  };

  /**
   * One more instance of a repeating component, inside its section: "### <name>" and the template's
   * fields. The first one replaces the empty fields the section started with; an unnamed one already
   * written can be named at the same time, so every instance has its H3.
   */
  async function addInstance(c) {
    const { h, modal } = get();
    const ed = get().editor();
    const sec = sectionOf(ed.state.doc, c.name);
    if (!sec) return addComponent(c);
    // What's above the section's first H3: an unnamed instance, written or still empty.
    let firstH3 = null;
    ed.state.doc.nodesBetween(sec.body, sec.end, (n, pos) => {
      if (firstH3 == null && n.type.name === "heading" && n.attrs.level === 3)
        firstH3 = pos;
      return false;
    });
    const loose = { body: sec.body, end: firstH3 ?? sec.end };
    const looseWritten = hasContent(linesOf(ed.state.doc, loose));
    const one = headingOf(c.name).toLowerCase();
    const name = h("input", {
      class: "field",
      placeholder: `What this ${one} is: Cook shrimp, Chop`,
    });
    const older = looseWritten
      ? h("input", {
          class: "field",
          placeholder: `The ${one} already written (optional)`,
        })
      : null;
    const ok = async () => {
      const n = name.value.trim();
      if (!n) {
        name.focus();
        return;
      }
      close();
      const tpl = await template(c.name);
      const now = sectionOf(ed.state.doc, c.name);
      if (!now) return;
      let first = null;
      ed.state.doc.nodesBetween(now.body, now.end, (x, pos) => {
        if (first == null && x.type.name === "heading" && x.attrs.level === 3)
          first = pos;
        return false;
      });
      const looseNow = { body: now.body, end: first ?? now.end };
      const text = instanceText(c, tpl, n);
      if (
        !hasContent(linesOf(ed.state.doc, looseNow)) &&
        looseNow.end > looseNow.body
      ) {
        ed.chain()
          .insertContentAt({ from: looseNow.body, to: looseNow.end }, text)
          .run(); // the empty fields become the first instance
      } else {
        ed.chain().insertContentAt(now.end, text).run();
        const label = older?.value.trim();
        if (label)
          ed.chain()
            .insertContentAt(now.body, "### " + label + "\n")
            .run();
      }
      refresh();
    };
    for (const el of [name, older])
      el?.addEventListener("keydown", (e) => {
        if (e.key === "Enter") ok();
      });
    const close = modal(`Add ${/^[aeiou]/.test(one) ? "an" : "a"} ${one}`, [
      h("label", {}, "Its name"),
      name,
      older
        ? [
            h(
              "label",
              { style: "margin-top:12px" },
              "And the one already here",
            ),
            older,
            h(
              "p",
              { class: "muted small" },
              "Each gets its own heading in the section.",
            ),
          ]
        : null,
      h(
        "div",
        { class: "modal-actions" },
        h("button", { class: "btn", onclick: () => close() }, "Cancel"),
        h("button", { class: "btn btn-gold", onclick: ok }, "Add"),
      ),
    ]);
    setTimeout(() => name.focus(), 0);
  }

  // ----------------------------------------------------------------------------- the badge row

  function strip(fm) {
    const { S, h } = get();
    const ro = !!S.readOnly;
    const el = h("div", { class: "gdd-strip", contenteditable: "false" });
    if (!fm) {
      const t = typeOfFolder(S.path);
      el.append(
        h("span", { class: "gdd-dim" }, "This page has no type yet."),
        ro || !t
          ? null
          : h(
              "button",
              {
                class: "gdd-link",
                onclick: () => {
                  get().setFront(
                    make({
                      type: t.type,
                      components: [],
                      groups: [],
                      "based-on": null,
                      status: "stub",
                    }),
                  );
                  refresh();
                },
              },
              `Make it ${/^[aeiou]/.test(t.type) ? "an" : "a"} ${t.type} page`,
            ),
      );
      return el;
    }
    const t = typeOf(fm);
    // The type: what kind of page this is.
    el.append(
      h(
        "span",
        {
          class: "gdd-badge gdd-type-badge" + (t ? "" : " bad"),
          title: t
            ? `${cap(t.type)}: ${t.covers}`
            : "Not a type in How the GDD Works",
        },
        h("span", { class: "ico ico-" + get().typeIcon(fm.type) }),
        cap(fm.type || "No type"),
      ),
    );
    // The status: one click to change it, since it changes often.
    const st = STATUSES.includes(fm.status) ? fm.status : "none";
    el.append(
      h(
        "button",
        {
          class: "gdd-badge gdd-status st-" + st,
          disabled: ro || null,
          title: ro ? STATUS_HINT[st] || "Status" : "Change the status",
          onclick: (e) =>
            get().menu(
              e,
              STATUSES.map((s) => [
                h(
                  "span",
                  { class: "gdd-status-item st-" + s },
                  cap(s),
                  h("span", { class: "gdd-menu-hint" }, STATUS_HINT[s]),
                ),
                () => setFront("status", s),
              ]),
            ),
        },
        fm.status ? cap(fm.status) : "No status",
        ro ? null : h("span", { class: "gdd-caret" }),
      ),
    );
    // What it's a variant of, and its groups: links to those pages.
    const pageLink = (slug, cls, label, folder) => {
      const p = pageOfSlug(slug);
      return h(
        "a",
        {
          class: "gdd-badge " + cls + (p ? "" : " todo"),
          title: p
            ? `Open ${p.title || slug}`
            : `No page for “${words(slug)}” yet: click to write it`,
          onclick: () =>
            p
              ? get().openDoc(p.path)
              : get().newPage(
                  folder
                    ? { path: folder + "/" + slug + ".md", title: words(slug) }
                    : { title: words(slug) },
                ),
        },
        label(p?.title || words(slug)),
      );
    };
    if (fm["based-on"])
      el.append(
        pageLink(fm["based-on"], "gdd-variant-badge", (n) => [
          h("span", { class: "gdd-badge-key" }, "Variant of"),
          n,
        ]),
      );
    for (const g of fm.groups)
      el.append(
        pageLink(
          g,
          "gdd-group-badge",
          (n) => [
            h("span", { class: "ico ico-" + get().typeIcon("group") }),
            n,
          ],
          groupFolder(),
        ),
      );
    // The components: the page's sections, to jump to.
    if (fm.components.length) {
      el.append(h("span", { class: "gdd-strip-sep" }));
      el.append(
        ...sorted(fm.components).map((c) => {
          const def = S.components.find((x) => x.name === nameOf(c));
          return h(
            "a",
            {
              class: "gdd-badge gdd-comp-badge" + (def ? "" : " bad"),
              title: def
                ? `${def.answers} (go to the section)`
                : "Not a component in How the GDD Works",
              onclick: () => goToSection(c),
            },
            headingOf(c),
          );
        }),
      );
    }
    if (!ro)
      el.append(
        h(
          "button",
          {
            class: "gdd-strip-cog",
            title: "Page settings",
            onclick: () => openSettings(),
          },
          h("span", { class: "ico ico-settings" }),
        ),
      );
    return el;
  }

  // ----------------------------------------------------------------------------- page settings

  /** The page's settings: status, components, groups and variant, type. Changes apply at once. */
  function openSettings() {
    const { S, h, modal } = get();
    if (!shown() || S.readOnly) return;
    if (S.mode !== "edit") {
      get().toast("Switch back from the source to change the page's settings");
      return;
    }
    let showMore = false,
      changingType = false,
      renaming = false,
      refsInfo = null;
    get().api("/api/refs?page=" + encodeURIComponent(S.path)).then((r) => { if (r.ok && Array.isArray(r.refs)) { refsInfo = r.refs; redrawSettings?.(); } });
    const body = h("div", { class: "ps" });
    const section = (title, ...kids) =>
      h(
        "section",
        { class: "ps-section" },
        h("h3", { class: "ps-title" }, title),
        ...kids,
      );

    const draw = () => {
      const fm = front();
      if (!fm) {
        body.replaceChildren(
          h(
            "p",
            { class: "muted" },
            "This page has no type yet: give it one from the row under its title.",
          ),
        );
        return;
      }
      const t = typeOf(fm);
      const have = new Set(fm.components.map(nameOf));

      // The page itself: its title and its ID (the file name), and how much refers to it.
      const title = pageTitle();
      const id = slugOf(S.path);
      const n = refsInfo ? new Set(refsInfo.map((r) => r.from)).size : null;
      const used = n == null ? "" : n ? `referred to by ${n} page${n > 1 ? "s" : ""}` : "nothing refers to it yet";
      const renameBox = () => {
        const input = h("input", { class: "field ps-inline", value: title, spellcheck: "true" });
        const file = h("span", { class: "muted small" });
        const show = () => { const s = slugify(input.value); file.textContent = s ? `${s}.md` + (n ? `, every reference in ${n} page${n > 1 ? "s" : ""} is updated` : "") : ""; };
        input.addEventListener("input", show);
        input.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); renamePage(input.value); } });
        show();
        setTimeout(() => { input.focus(); input.select(); }, 0);
        return h("div", { class: "ps-rename" }, h("div", { class: "ps-chips" }, input), h("div", { class: "ps-rename-foot" }, file,
          h("span", { class: "ps-rename-btns" },
            h("button", { class: "btn", onclick: () => { renaming = false; draw(); } }, "Cancel"),
            h("button", { class: "btn btn-gold", onclick: () => renamePage(input.value) }, "Rename"))));
      };
      const pageSec = section(
        "Page",
        renaming ? renameBox() : h("div", { class: "ps-type" },
          h("span", { class: "ps-page-title" }, title),
          h("span", { class: "muted small" }, h("code", { class: "ps-id" }, id + ".md"), used ? ", " + used : ""),
          h("button", { class: "btn ps-type-btn", onclick: () => { renaming = true; draw(); } }, "Rename…")),
        !renaming && slugify(title) && slugify(title) !== id
          ? h("p", { class: "ps-warn" }, `The title and the file name differ (${id}.md). `,
            h("button", { class: "gdd-link", onclick: () => renamePage(title) }, `Rename the file to ${slugify(title)}.md`))
          : null,
      );

      // Status
      const status = section(
        "Status",
        h(
          "div",
          { class: "ps-seg" },
          STATUSES.map((s) =>
            h(
              "button",
              {
                class: "ps-seg-btn st-" + s + (fm.status === s ? " on" : ""),
                title: STATUS_HINT[s],
                onclick: () => setFront("status", s),
              },
              cap(s),
            ),
          ),
        ),
      );

      // Components
      const { common, more } = componentsFor(S.components, fm.type);
      const row = (c) =>
        h(
          "label",
          { class: "ps-check" + (have.has(c.name) ? " on" : "") },
          h("input", {
            type: "checkbox",
            checked: have.has(c.name) || null,
            onchange: (e) =>
              (e.target.checked
                ? addComponent(c, { jump: false })
                : removeComponent(c)
              ).then(() => draw()),
          }),
          h(
            "span",
            { class: "ps-check-name" },
            headingOf(c.name),
            c.repeats
              ? h(
                  "span",
                  {
                    class: "ps-repeats",
                    title:
                      "A page can have several: add more inside its section",
                  },
                  "repeats",
                )
              : null,
          ),
          h("span", { class: "ps-check-q" }, c.answers),
        );
      const extra = more.filter((c) => have.has(c.name)); // one ticked from More stays in sight
      const unknown = fm.components.filter(
        (c) => !S.components.some((x) => x.name === nameOf(c)),
      );
      const comps = section(
        "Components",
        h(
          "p",
          { class: "ps-hint" },
          `What this page does. Ticking one adds its section; ${t ? `usual for ${/^[aeiou]/.test(t.type) ? "an" : "a"} ${t.type}` : "the usual ones"} first.`,
        ),
        h(
          "div",
          { class: "ps-checks" },
          (common.length ? common : more).map(row),
          common.length ? extra.map(row) : null,
        ),
        common.length && more.length
          ? h(
              "details",
              {
                class: "ps-more",
                open: showMore || null,
                ontoggle: (e) => {
                  showMore = e.target.open;
                },
              },
              h("summary", {}, `More (${more.length})`),
              h(
                "div",
                { class: "ps-checks" },
                more.filter((c) => !have.has(c.name)).map(row),
              ),
            )
          : null,
        unknown.length
          ? h(
              "p",
              { class: "ps-warn" },
              `Not in How the GDD Works: ${unknown.join(", ")}. `,
              h(
                "button",
                {
                  class: "gdd-link",
                  onclick: () =>
                    setFront(
                      "components",
                      fm.components.filter((c) => !unknown.includes(c)),
                    ),
                },
                "Take them off",
              ),
            )
          : null,
        ordered()
          ? null
          : h(
              "p",
              { class: "ps-warn" },
              "The sections are out of the usual order. ",
              h(
                "button",
                {
                  class: "gdd-link",
                  onclick: () => {
                    reorder();
                    draw();
                  },
                },
                "Put them in order",
              ),
            ),
        missingCore().length
          ? h(
              "p",
              { class: "ps-warn" },
              `Missing ${t.type} headings: ${missingCore().join(", ")}. `,
              h(
                "button",
                {
                  class: "gdd-link",
                  onclick: () => {
                    addCoreHeadings();
                    draw();
                  },
                },
                "Add them",
              ),
            )
          : null,
      );

      // Groups and variant: picked from pages that exist (or made on the spot), stored as their IDs.
      const gFolder = groupFolder();
      const groupPages = S.files
        .filter((f) => f.path.startsWith(gFolder + "/") && !fm.groups.includes(slugOf(f.path)))
        .sort((a, b) => String(a.title).localeCompare(String(b.title)));
      const refChip = (id, hint, remove) => {
        const p = pageOfSlug(id);
        return h("span", { class: "ps-chip" + (p ? "" : " missing"), title: p ? p.path.slice(GDD.length + 1) : `No page is called “${id}”: it was deleted, or renamed outside Codex. Take it off, or pick the right page.` },
          p ? h("a", { class: "ps-chip-link", onclick: () => { closeSettings?.(); get().openDoc(p.path); } }, p.title || id) : [h("span", { class: "ps-chip-warn" }, "!"), id],
          h("button", { class: "ps-chip-x", title: hint, onclick: remove }, "✕"));
      };
      const groupPicker = pagePicker({
        h, pages: groupPages.map((f) => ({ path: f.path, title: f.title })),
        placeholder: fm.groups.length ? "Add another group…" : "Add a group…",
        onPick: (p) => setFront("groups", [...front().groups, slugOf(p.path)]),
        create: { label: (q) => `Make a group page “${q}”`, run: async (q) => { const id = await makePage(q, "group"); if (id) setFront("groups", [...front().groups, id]); } },
      });
      const typedPages = S.files
        .filter((f) => inGdd(f.path) && !isMeta(f.path) && f.path !== S.path)
        .map((f) => ({ path: f.path, title: f.title, type: typeAt(S.types, f.path)?.type || null }))
        .sort((a, b) => (b.type === fm.type) - (a.type === fm.type) || String(a.title).localeCompare(String(b.title)));
      const variantPicker = pagePicker({
        h, pages: typedPages.map((p) => ({ ...p, note: p.type ? cap(p.type) : "" })),
        placeholder: "The page this one is a variant of…",
        onPick: (p) => setFront("based-on", slugOf(p.path)),
        create: t ? { label: (q) => `Make “${q}” (${/^[aeiou]/.test(t.type) ? "an" : "a"} ${t.type} page)`, run: async (q) => { const id = await makePage(q, t.type); if (id) setFront("based-on", id); } } : null,
      });
      const groups = section(
        "Groups",
        h("div", { class: "ps-chips" }, fm.groups.map((g) => refChip(g, "Take it out of this group", () => setFront("groups", front().groups.filter((x) => x !== g))))),
        h("div", { class: "ps-pick" }, groupPicker),
        h("h3", { class: "ps-title ps-sub" }, "Variant of"),
        fm["based-on"]
          ? h("div", { class: "ps-chips" }, refChip(fm["based-on"], "Not a variant", () => setFront("based-on", null)))
          : h("div", { class: "ps-pick" }, variantPicker),
      );

      // Type: shown, and changed only deliberately.
      const pick = h(
        "select",
        { class: "field ps-inline" },
        S.types
          .filter((x) => x.type !== fm.type)
          .map((x) =>
            h("option", { value: x.type }, cap(x.type) + ": " + x.examples),
          ),
      );
      const where = () => {
        const x = S.types.find((y) => y.type === pick.value);
        return x ? (x.folder ? x.folder + "/" : "the top of the GDD") : "";
      };
      const whereEl = h("b", {}, where());
      pick.addEventListener("change", () => {
        whereEl.textContent = where();
      });
      const type = section(
        "Type",
        h(
          "div",
          { class: "ps-type" },
          h(
            "span",
            { class: "gdd-badge gdd-type-badge" },
            h("span", { class: "ico ico-" + get().typeIcon(fm.type) }),
            cap(fm.type || "none"),
          ),
          h(
            "span",
            { class: "muted small" },
            t
              ? (t.folder ? `in ${t.folder}/` : "at the top of the GDD") +
                  `: ${t.headings.join(", ")}`
              : "",
          ),
          changingType
            ? null
            : h(
                "button",
                {
                  class: "btn ps-type-btn",
                  onclick: () => {
                    changingType = true;
                    draw();
                  },
                },
                "Change type…",
              ),
        ),
        changingType
          ? h(
              "div",
              { class: "ps-danger" },
              h(
                "p",
                {},
                h("b", {}, "Changing the type is rare. "),
                "The page moves to the new type's folder (links to it are updated), and its headings stay as they are, so they may no longer match the new type's core headings.",
              ),
              h(
                "div",
                { class: "ps-chips" },
                pick,
                h("span", { class: "muted small" }, "moves it to ", whereEl),
              ),
              h(
                "div",
                { class: "modal-actions" },
                h(
                  "button",
                  {
                    class: "btn",
                    onclick: () => {
                      changingType = false;
                      draw();
                    },
                  },
                  "Cancel",
                ),
                h(
                  "button",
                  {
                    class: "btn btn-danger",
                    onclick: () => changeType(pick.value),
                  },
                  "Change type and move",
                ),
              ),
            )
          : null,
      );

      body.replaceChildren(pageSec, status, comps, groups, type);
    };

    draw();
    redrawSettings = draw;
    const title =
      get().S.files.find((f) => f.path === get().S.path)?.title ||
      slugOf(get().S.path);
    closeSettings = modal("Page settings | " + title, body, {
      width: "min(640px, 94vw)",
      onClose: () => {
        redrawSettings = null;
        closeSettings = null;
      },
    });
  }

  /** The open page's title: its first heading, as written. */
  function pageTitle() {
    let t = null;
    get().editor()?.state.doc.forEach((n) => { if (t == null && n.type.name === "heading" && n.attrs.level === 1) t = n.textContent; });
    return t || slugOf(get().S.path);
  }

  /** Make a page named `title` (a stub of `type`, in its folder); its ID, or null when it couldn't be made. */
  async function makePage(title, type) {
    const { S, api, toast } = get();
    const t = S.types.find((x) => x.type === type);
    const id = slugify(title);
    if (!t || !id) return null;
    const clash = S.files.find((f) => inGdd(f.path) && !f.path.startsWith(TEMPLATES + "/") && slugOf(f.path) === id);
    if (clash) { toast(`“${id}” is already ${clash.title || clash.path}: names are unique across the GDD`, "bad"); return null; }
    const r = await api("/api/new", { method: "POST", body: JSON.stringify({ path: folderPath(t) + "/" + id + ".md", title: title.trim(), type }) });
    if (!r.ok) { toast(failed("Couldn't make the page", r), "bad"); return null; }
    await get().loadTree();
    toast(`Made “${title.trim()}”, a stub`, "good");
    return id;
  }

  /** Rename the open page: its title, and its file name (its ID) to match. Every reference to it follows (server, src/refs.js). */
  async function renamePage(title) {
    const { S, api, toast } = get();
    title = String(title || "").trim();
    const id = slugify(title);
    if (!id) return;
    const from = S.path, to = from.slice(0, from.lastIndexOf("/") + 1) + id + ".md";
    const clash = S.files.find((f) => f.path !== from && inGdd(f.path) && !f.path.startsWith(TEMPLATES + "/") && slugOf(f.path) === id);
    if (clash) { toast(`“${id}” is already ${clash.title || clash.path}: names are unique across the GDD`, "bad"); return; }
    if (!(await get().save())) { toast("Couldn't save the page first, so it wasn't renamed", "bad"); return; }
    const r = await api("/api/move", { method: "POST", body: JSON.stringify({ from, to, title }) });
    if (!r.ok) { toast(failed("Couldn't rename it", r), "bad"); return; }
    closeSettings?.();
    await get().loadTree();
    await get().openDoc(to, { push: false, mode: "edit" });
    history.replaceState({ path: to }, "", "?p=" + encodeURI(to));
    const n = (r.rewritten || []).length;
    toast(`Renamed to “${title}”` + (n ? `; references updated in ${n} page${n > 1 ? "s" : ""}` : ""), "good");
  }

  /** A new type: the file moves to the type's folder (the server puts links right), then the frontmatter says so. */
  async function changeType(type) {
    const { S, api, toast } = get();
    const t = S.types.find((x) => x.type === type);
    if (!t) return;
    const from = S.path,
      to = folderPath(t) + "/" + from.split("/").pop();
    if (to !== from && S.fileSet.has(to)) {
      toast(`There's already a page at ${to.slice(GDD.length + 1)}`, "bad");
      return;
    }
    if (!(await get().save())) {
      toast("Couldn't save the page first, so it wasn't moved", "bad");
      return;
    }
    if (to !== from) {
      const r = await api("/api/move", {
        method: "POST",
        body: JSON.stringify({ from, to }),
      });
      if (!r.ok) {
        toast(failed("Couldn't move it", r), "bad");
        return;
      }
      await get().loadTree();
      await get().openDoc(to, { push: false, mode: "edit" });
      history.replaceState({ path: to }, "", "?p=" + encodeURI(to));
    }
    setFront("type", type);
    await get().save();
    const missing = missingCore();
    toast(
      `Now ${/^[aeiou]/.test(type) ? "an" : "a"} ${type} page${to !== from ? `, in ${t.folder || "the top of the GDD"}` : ""}.` +
        (missing.length ? ` Missing its headings: ${missing.join(", ")}.` : ""),
      "good",
      missing.length
        ? { actions: [{ label: "Add them", run: () => addCoreHeadings() }] }
        : {},
    );
    closeSettings?.();
  }

  // ----------------------------------------------------------------------------- "+ Add action"

  // A component written as a table (rewards, spawns) repeats by rows; one written as fields (actions) by
  // an H3 per instance.
  function addButton(c, sec, doc) {
    const { h } = get();
    const one = headingOf(c.name).toLowerCase();
    const table = tableIn(doc, sec);
    return h("div", { class: "gdd-add-instance", contenteditable: "false" },
      table != null
        ? h("button", { class: "gdd-link", title: "Another row in this table", onclick: () => addRow(c) }, "+ Add a row")
        : h("button", { class: "gdd-link", title: `Another ${one} on this page, with its own heading`, onclick: () => addInstance(c) }, "+ Add " + one));
  }

  /** The position of the last table in a section, or null. */
  function tableIn(doc, sec) {
    let at = null;
    doc.nodesBetween(sec.body, sec.end, (n, pos) => { if (n.type.name === "table") { at = pos; return false; } return true; });
    return at;
  }

  /** A new row at the end of the section's table, the cursor in its first cell. */
  function addRow(c) {
    const ed = get().editor();
    const sec = sectionOf(ed.state.doc, c.name);
    const at = sec && tableIn(ed.state.doc, sec);
    if (at == null) return;
    const table = ed.state.doc.nodeAt(at);

    let rowStart = at + 1;
    for (let i = 0; i < table.childCount - 1; i++) rowStart += table.child(i).nodeSize;
    ed.chain().focus().setTextSelection(rowStart + 3).addRowAfter().run(); // inside the last row's first cell
    const after = ed.state.doc.nodeAt(at);
    let newRow = at + 1;
    for (let i = 0; i < after.childCount - 1; i++) newRow += after.child(i).nodeSize;
    ed.commands.setTextSelection(newRow + 3);

  }

  // ----------------------------------------------------------------------------- the templates' guidance

  // A template's HTML comments are its guidance (src/gdd.js, promptsOf): on a page, a section's prompt
  // shows faintly under its heading while nothing is written there, and a field's hint after it while
  // it's empty. They're drawn, never part of the page.

  /** A section's prompt on a page of this type: from the type's template for a core heading, the component's for a component. */
  function promptFor(fm, name) {
    const P = get().S.prompts;
    return P?.types?.[fm?.type]?.sections?.[name] || P?.components?.[name]?.sections?.[name] || null;
  }

  function promptDecos(doc, fm, decos) {
    const { S, h } = get();
    if (!S.prompts || !fm) return;
    for (const sec of sections(doc)) {
      const prompt = hasContent(linesOf(doc, sec)) ? null : promptFor(fm, sec.name);
      if (prompt) decos.push(Decoration.widget(sec.body, safe(() => h("div", { class: "gdd-prompt", contenteditable: "false" }, prompt)),
        { side: -1, key: "prompt:" + sec.name + ":" + prompt, ignoreSelection: true }));
      const hints = S.prompts.components?.[sec.name]?.fields;
      if (!hints) continue;
      doc.nodesBetween(sec.body, sec.end, (n, pos) => {
        if (n.type.name !== "paragraph") return true;
        const m = n.textContent.match(/^([^:]{1,40}):\s*$/);
        const hint = m && hints[nameOf(m[1])];
        if (hint) decos.push(Decoration.widget(pos + n.nodeSize - 1, safe(() => h("span", { class: "gdd-hint", contenteditable: "false" }, hint)),
          { side: 1, key: "hint:" + pos + ":" + hint, ignoreSelection: true }));
        return false;
      });
    }
  }

  /** On a template itself, its guidance is marked as guidance. */
  function templateDecos(doc, decos) {
    doc.descendants((n, pos) => {
      if (!n.isText) return true;
      for (const m of n.text.matchAll(/<!--[\s\S]*?-->/g)) decos.push(Decoration.inline(pos + m.index, pos + m.index + m[0].length, { class: "tpl-guide" }));
      return false;
    });
  }

  // ----------------------------------------------------------------------------- the bottom box

  const plain = (md) =>
    md.replace(/\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]/g, "");

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
      api("/api/query", {
        method: "POST",
        body: JSON.stringify({ page: S.path, q }),
      }).then((x) => {
        if (v !== version) return;
        results.set(key, x.ok ? x : { error: x.error || "couldn't run it" });
        refresh();
      });
    }
    if (!r || r === "pending") {
      box.append(h("div", { class: "q-note" }, "Gathering…"));
      return box;
    }
    if (r.error) {
      box.append(h("div", { class: "q-note bad" }, r.error));
      return box;
    }
    const open = (p) =>
      h(
        "a",
        {
          class: "q-page",
          onclick: () => get().openDoc(p.path),
          title: p.path,
        },
        p.title || slugOf(p.path),
      );
    if (!Array.isArray(r.rows)) {
      box.append(
        h(
          "div",
          { class: "q-note bad" },
          "Codex needs a restart to gather this list.",
        ),
      );
      return box;
    }
    if (!r.rows.length)
      box.append(
        h(
          "div",
          { class: "q-note" },
          "Nothing yet. Pages appear here as they're written.",
        ),
      );
    else if (r.kind === "pages")
      box.append(
        h(
          "ul",
          { class: "q-list" },
          r.rows.map((p) =>
            h(
              "li",
              {},
              open(p),
              p.type ? h("span", { class: "q-type" }, p.type) : null,
            ),
          ),
        ),
      );
    else {
      const named = r.rows.some((x) => x.name);
      box.append(
        h(
          "table",
          { class: "q-table" },
          h(
            "thead",
            {},
            h(
              "tr",
              {},
              h("th", {}, "Page"),
              named ? h("th", {}, headingOf(r.component)) : null,
              r.columns.map((c) => h("th", {}, cap(c))),
            ),
          ),
          h(
            "tbody",
            {},
            r.rows.map((x) =>
              h(
                "tr",
                {},
                h("td", {}, open(x)),
                named ? h("td", {}, x.name || "") : null,
                r.columns.map((c) => h("td", {}, plain(x.fields[c] || ""))),
              ),
            ),
          ),
        ),
      );
    }
    return box;
  }

  // ----------------------------------------------------------------------------- decorations

  function decorations(doc) {
    const decos = [];
    const { S } = get();
    if (S.path?.startsWith(TEMPLATES + "/")) templateDecos(doc, decos);
    if (shown()) {
      const fm = front();
      const key =
        S.front +
        "|" +
        S.types.length +
        "|" +
        S.components.length +
        "|" +
        S.files.length +
        "|" +
        !!S.readOnly;
      let title = null;
      doc.forEach((n, pos) => {
        if (!title && n.type.name === "heading" && n.attrs.level === 1)
          title = { n, pos };
      });
      if (title && fm?.status === "cut")
        decos.push(
          Decoration.node(title.pos, title.pos + title.n.nodeSize, {
            class: "gdd-cut",
          }),
        );
      const at = title ? title.pos : 0; // the strip sits above the title
      decos.push(
        Decoration.widget(
          at,
          safe(() => strip(fm)),
          {
            side: -1,
            key: "strip:" + key,
            ignoreSelection: true,
            stopEvent: () => true,
          },
        ),
      );
      // "+ Add action" at the end of each repeating component's section.
      if (fm && editable()) {
        const have = new Set(fm.components.map(nameOf));
        for (const sec of sections(doc)) {
          const c = S.components.find((x) => x.name === sec.name);
          if (!c || !have.has(c.name) || (!c.repeats && tableIn(doc, sec) == null)) continue; // repeats, or rows to add
          decos.push(
            Decoration.widget(
              sec.end,
              safe(() => addButton(c, sec, doc)),
              {
                side: -1,
                key: "add:" + c.name + ":" + S.path,
                ignoreSelection: true,
                stopEvent: () => true,
              },
            ),
          );
        }
      }
      promptDecos(doc, fm, decos);
      // (What links here, a group's members and a skill's training are in the Links tab, src/panels.js:
      // the box that repeated them at the bottom of the page is gone.)
    }
    doc.descendants((node, pos) => {
      if (node.type.name !== "codeBlock" || node.attrs.language !== "query")
        return node.isBlock && node.type.name !== "codeBlock";
      const key = S.path + "\n" + node.textContent;
      const r = results.get(key);
      decos.push(
        Decoration.widget(
          pos + node.nodeSize,
          safe(() => listView(node)),
          {
            side: 1,
            key:
              "q:" +
              version +
              ":" +
              key +
              ":" +
              (r ? (r === "pending" ? "p" : "r") : "n"),
            ignoreSelection: true,
            stopEvent: () => true,
          },
        ),
      );
      return false;
    });
    return DecorationSet.create(doc, decos);
  }

  function safeDecorations(doc) {
    try {
      return decorations(doc);
    } catch (e) {
      console.error("[gdd] couldn't lay out the page's header", e);
      return DecorationSet.empty;
    }
  }

  const Extension_ = Extension.create({
    name: "gddUi",
    addProseMirrorPlugins() {
      return [
        new Plugin({
          key: refreshKey,
          state: {
            init: (_, state) => safeDecorations(state.doc),
            apply: (tr, old) =>
              tr.docChanged || tr.getMeta(refreshKey)
                ? safeDecorations(tr.doc)
                : old.map(tr.mapping, tr.doc),
          },
          props: {
            decorations(state) {
              return this.getState(state);
            },
          },
        }),
      ];
    },
  });

  return {
    Extension: Extension_,
    refresh,
    addComponent,
    front,
    openSettings,
    shown,
  };
}
