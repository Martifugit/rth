// The Links tab (what links to this page, what it links to, where it's mentioned), and the special pages
// shown in the main window: Recent changes (who changed which page and where, from git) and To-do
// (pages to write, and structure to fix).

import { slug } from "./links.js";
import { pagePicker } from "./page-picker.js";
import { failed } from "./plain-errors.js";


export function makePanels(ctx) {
  const { S, $, h, api } = ctx;
  const cache = new Map();
  const table = { data: null };
  const lists = { changes: null, broken: null };

  // ----------------------------------------------------------------------------- links

  async function linksFor(page) {
    if (!cache.has(page)) cache.set(page, api("/api/links?page=" + encodeURIComponent(page)).then((r) => (r.ok ? r : null)));
    return cache.get(page);
  }
  const generated = new Map(); // page -> a group's members, a skill's training (/api/generated)
  async function generatedFor(page) {
    if (!generated.has(page)) generated.set(page, api("/api/generated?page=" + encodeURIComponent(page)).then((r) => (r.ok ? r : null), () => null));
    return generated.get(page);
  }
  function invalidate() { cache.clear(); generated.clear(); lists.broken = null; }

  async function renderLinks() {
    const pane = $("#linksPane");
    if (!S.path) return;
    const page = S.path;
    const [r, gen] = await Promise.all([linksFor(page), generatedFor(page)]);
    if (page !== S.path || !r) return;
    ctx.setLinksCount(r.inbound.length);
    const byPage = (list, key) => {
      const m = new Map();
      for (const l of list) { if (!m.has(l[key])) m.set(l[key], []); m.get(l[key]).push(l); }
      return [...m];
    };
    const item = (path, title, detail, onclick, cls = "") => h("a", { class: "lk-item " + cls, title: path, onclick },
      h("span", { class: "lk-page" }, title || path.split("/").pop().replace(/\.md$/, "")),
      detail ? h("span", { class: "lk-ctx" }, detail) : null);
    const group = (title, help, items, empty) => h("section", { class: "lk-group" },
      h("h3", { class: "lk-title", title: help }, title, items.length ? h("span", { class: "lk-n" }, String(items.length)) : null),
      items.length ? items : h("p", { class: "lk-empty" }, empty));
    // Links in, by what they mean: "Dropped by" from a rewards section, "Found in" from spawns (How the
    // GDD Works, "Who writes what"); any other link in is "Linked from".
    const labels = new Map();
    for (const l of r.inbound) { const k = l.label || ""; if (!labels.has(k)) labels.set(k, []); labels.get(k).push(l); }
    const inboundOf = (list) => byPage(list, "from").map(([from, ls]) =>
      item(from, ls[0].title, ls[0].section || null, () => ctx.openDoc(from, { find: { q: ls[0].text, occurrence: 0 } })));
    const labelled = [...labels].filter(([k]) => k).sort(([a], [b]) => a.localeCompare(b));
    const inbound = inboundOf(labels.get("") || []);
    const outbound = byPage(r.outbound, "to").map(([to, ls]) => ls[0].broken
      ? item(to, ls[0].text || to.split("/").pop(), "Not written yet: click to make it", () => ctx.newPage({ path: to, title: ls[0].text || "" }), "broken")
      : item(to, ls[0].title, null, () => ctx.openDoc(to)));
    const mentions = r.mentions.map((m) => item(m.from, m.title, m.section || null, () => ctx.openDoc(m.from, { find: { q: r.title, occurrence: 0 } })));
    // From the GDD itself rather than links: pages that list this one as a group, and how a skill is trained.
    const open = (p) => () => ctx.openDoc(p.path);
    const members = (gen?.members || []).map((p) => item(p.path, p.title, null, open(p)));
    const cols = (gen?.training?.columns || []).filter((c) => ["level", "verb", "xp", "tool"].includes(c));
    const plain = (md) => String(md || "").replace(/\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]/g, "");
    const training = (gen?.training?.rows || []).map((x) => item(x.path, x.title, [x.name, ...cols.map((c) => (x.fields?.[c] ? `${c} ${plain(x.fields[c])}` : ""))].filter(Boolean).join(", ") || null, open(x)));
    pane.replaceChildren(...[ // filtered: replaceChildren writes a missing group as the text "null"
      members.length ? group("Members", "Pages that list this one among their groups", members, "") : null,
      training.length ? group("Trained by", "What trains this skill, from those pages' actions", training, "") : null,
      ...labelled.map(([label, ls]) => group(label, `Links to this page from those pages' sections that own the relationship`, inboundOf(ls), "")),
      group(labelled.length ? "Also linked from" : "Linked from", "Pages with a link to this one", inbound, labelled.length ? "No other links." : "No page links here yet."),
      group("Links to", "Pages this one links to", outbound, "This page has no links to other pages."),
      r.title ? group("Mentioned in", `Pages that name “${r.title}” without linking to it`, mentions, "Not mentioned anywhere else.") : null,
    ].filter(Boolean));
  }

  // ----------------------------------------------------------------------------- recent changes

  // When you last looked at Recent changes: others' changes after it are new (marked, and counted in
  // the page list). Before the first look, nothing counts as new.
  const SEEN = "codex.changesSeen";
  const seenAt = () => { let t = Number(localStorage.getItem(SEEN)); if (!t) { t = Date.now(); localStorage.setItem(SEEN, String(t)); } return t; };
  const unseen = (list, since = seenAt()) => list.filter((e) => !e.mine && Date.parse(e.date) > since);

  async function changes(force = false) {
    if (force || !lists.changes) { const r = await api("/api/changes"); if (r.ok) lists.changes = r.changes || []; }
    return lists.changes;
  }

  async function renderChanges() {
    const view = $("#statusView");
    if (!lists.changes) view.replaceChildren(h("p", { class: "st-loading" }, "Loading…"));
    const list = await changes(true);
    if (S.view !== "changes" || !list) return;
    const since = seenAt();
    localStorage.setItem(SEEN, String(Date.now()));
    ctx.countsChanged?.();
    const fresh = unseen(list, since).length;
    const days = new Map();
    for (const e of list) { if (!days.has(e.day)) days.set(e.day, []); days.get(e.day).push(e); }
    const open = (e, section) => e.exists && ctx.openDoc(e.path, section ? { hash: slug(section) } : {});
    const verb = { added: "made", deleted: "deleted", changed: "changed" };
    const row = (e) => h("li", { class: "ch-row" + (!e.mine && Date.parse(e.date) > since ? " new" : "") },
      h("span", { class: "ch-time", title: new Date(e.date).toLocaleString("en-GB", { hourCycle: "h23" }) }, timeOf(e.date)),
      h("span", { class: "ch-what" },
        h("b", { class: "ch-who" }, e.mine ? "You" : e.author), " " + verb[e.status] + " ",
        e.exists ? h("a", { onclick: () => open(e), title: e.path }, e.title) : h("span", { class: "ch-gone", title: e.path }, e.title),
        e.sections.length ? [h("span", { class: "ch-in" }, " in "), sectionLinks(e, open)] : null,
        e.ai ? h("span", { class: "ch-ai", title: "Made with R'th AI" }, "with AI") : null));
    view.replaceChildren(h("article", { class: "prose oq ch" },
      h("h1", {}, "Recent changes"),
      h("p", { class: "oq-intro" }, list.length
        ? (fresh ? `${fresh} new since you last looked. ` : "") + "Who changed which page, and where, newest first. Click a page or a section to go to it."
        : "No changes to the GDD yet."),
      [...days].map(([day, es]) => [h("h2", {}, dayName(day)), h("ul", { class: "ch-list" }, es.map(row))])));
  }

  function sectionLinks(e, open) {
    const shown = e.sections.slice(0, 3);
    const parts = [];
    shown.forEach((sec, i) => {
      if (i) parts.push(", ");
      parts.push(e.exists ? h("a", { class: "ch-sec", onclick: () => open(e, sec) }, sec) : h("span", { class: "ch-sec" }, sec));
    });
    if (e.sections.length > shown.length) parts.push(h("span", { class: "ch-more", title: e.sections.slice(3).join(", ") }, ` and ${e.sections.length - 3} more`));
    return parts;
  }

  // ----------------------------------------------------------------------------- to-do

  // What the GDD still needs, in one list: pages that links (or groups, or based-on) point to but that
  // aren't written yet, each one click from being made; links to headings that aren't there; and the
  // structure checks (src/gdd-index.js): a page with no type, a component without its section...
  // ----------------------------------------------------------------------------- To-do

  // What the GDD still needs (server: src/todo.js), as cards with their fixes right on them. A fix that
  // touches more than one page says which first; every fix can be undone straight after. Fixing a
  // reference goes through the same machinery as renaming a page, so nothing is left half-pointed.

  async function broken(force = false) {
    if (force || !lists.broken) {
      const t = await api("/api/todo");
      if (t.ok && Array.isArray(t.missing)) lists.broken = { ...t, length: t.count };
    }
    return lists.broken;
  }

  const todoState = { filter: "all", folder: "", open: new Set() };
  const where = (p) => String(p || "").replace(/^docs\/gdd\//, "");
  const folderOf = (p) => where(p).split("/").slice(0, -1).join("/");
  const plural = (n, one, many = one + "s") => `${n} ${n === 1 ? one : many}`;
  const gddPages = () => S.files.filter((f) => f.path.startsWith("docs/gdd/") && !ctx.isMeta(f.path)).map((f) => ({ path: f.path, title: f.title, note: folderOf(f.path) }));

  /** Do a fix: say which pages it changes when it's more than one, do it, offer Undo, and draw the To-do again. */
  async function applyFix(fix, what) {
    const dry = await api("/api/fix", { method: "POST", body: JSON.stringify({ fix, dry: true }) });
    if (!dry.ok) return ctx.toast(dry.error || "Couldn't plan that", "bad");
    if (!dry.pages.length) { ctx.toast("Nothing to change"); return renderBroken(); }
    if (dry.pages.length > 1) {
      const list = dry.pages.map((p) => `<li>${esc(p.title || where(p.path))} <span class="muted">${esc(where(p.path))}</span></li>`).join("");
      const ok = await ctx.ask(what, `This changes ${dry.pages.length} pages:<ul class="td-preview">${list}</ul>`, [{ key: "no", label: "Cancel" }, { key: "yes", label: "Change them", cls: "btn-gold" }]);
      if (ok !== "yes") return;
    }
    if (S.path && !(await ctx.save())) return ctx.toast("Couldn't save the open page first, so nothing was changed", "bad");
    const r = await api("/api/fix", { method: "POST", body: JSON.stringify({ fix }) });
    if (!r.ok) return ctx.toast(failed("Couldn't do that", r), "bad");
    if (r.pages.some((p) => p.path === S.path)) S.reloadOpen = true; // the open page is read again when it's next shown
    ctx.toast(`${what}: ${plural(r.pages.length, "page")} changed`, "good", { actions: [{ label: "Undo", run: async () => {
      const u = await api("/api/fix/undo", { method: "POST", body: JSON.stringify({ token: r.token }) });
      if (!u.ok) return ctx.toast(u.error || "Couldn't undo it", "bad");
      if (u.skipped?.length) ctx.toast(`${plural(u.skipped.length, "page")} changed since, so kept: ${u.skipped.map(where).join(", ")}`, "bad");
      refresh();
    } }] });
    refresh();
  }
  const refresh = async () => { lists.broken = null; await ctx.loadTree?.(); ctx.countsChanged(); if (S.view === "broken") renderBroken(); };
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);

  /** A picker that opens in place of a button: pick a page, then `done(page)`. */
  function pickInPlace(label, placeholder, done, cls = "btn") {
    const slot = h("span", { class: "td-pick-slot" });
    const btn = h("button", { class: cls, onclick: () => {
      const picker = pagePicker({ h, pages: gddPages(), placeholder, onPick: (p) => done(p) });
      const cancel = h("button", { class: "td-x", title: "Cancel", onclick: () => slot.replaceChildren(btn) }, "✕");
      slot.replaceChildren(h("span", { class: "td-pick" }, picker, cancel));
      setTimeout(() => picker.querySelector("input")?.focus(), 0);
    } }, label);
    slot.append(btn);
    return slot;
  }

  const openAt = (r) => ctx.openDoc(r.from, r.text ? { find: { q: r.text, occurrence: 0 } } : {});
  const fromLink = (r) => h("a", { class: "td-from", onclick: () => openAt(r), title: r.from }, r.fromTitle || where(r.from));

  function missingCard(c) {
    const froms = [...new Set(c.refs.filter((r) => r.to).map((r) => r.to))];
    const retarget = (p, what = `Pointed everything at “${p.title || where(p.path)}”`) => applyFix({ kind: "retarget", from: froms, id: c.id, to: p.path }, what);
    const KIND = { link: "links to it", group: "lists it as a group", variant: "is a variant of it" };
    const refRow = (r) => h("li", { class: "td-ref" },
      h("div", { class: "td-ref-what" }, fromLink(r), h("span", { class: "td-dim" }, " " + KIND[r.kind]),
        r.context ? h("div", { class: "td-context" }, r.context) : null),
      h("div", { class: "td-ref-do" },
        r.kind === "link"
          ? [pickInPlace("Point elsewhere…", "The page this link means…", (p) => applyFix({ kind: "retarget-one", page: r.from, line: r.line, from: r.to, to: p.path }, "Link pointed elsewhere"), "td-link"),
            h("button", { class: "td-link", title: "Keep the words, drop the link", onclick: () => applyFix({ kind: "unlink", page: r.from, line: r.line, target: r.to }, "Link removed") }, "Unlink")]
          : [pickInPlace("Point elsewhere…", r.kind === "group" ? "The group it means…" : "The page it's a variant of…", (p) => applyFix({ kind: "retarget-ref", page: r.from, field: r.kind === "group" ? "groups" : "based-on", id: c.id, to: p.path }, "Pointed elsewhere"), "td-link"),
            h("button", { class: "td-link", onclick: () => applyFix({ kind: "drop-ref", page: r.from, field: r.kind === "group" ? "groups" : "based-on", id: c.id }, "Taken off") }, "Take it off")]));
    const open = todoState.open.has("m:" + c.id);
    return h("div", { class: "td-item" + (open ? " open" : "") },
      h("div", { class: "td-head" },
        h("div", { class: "td-title" }, c.title),
        h("span", { class: "td-count", title: `Wanted by ${plural(c.pages, "page")}` }, "wanted by ", plural(c.pages, "page"))),
      h("div", { class: "td-sub td-path" }, c.path ? where(c.path) : c.id),
      c.suggestions.length ? h("div", { class: "td-suggest" }, h("span", { class: "td-dim" }, "Did you mean "),
        c.suggestions.map((s, i) => [i ? h("span", { class: "td-dim" }, " or ") : null,
          h("button", { class: "td-sugg", title: `Point every reference at ${where(s.path)}`, onclick: () => retarget(s) }, s.title, h("span", { class: "td-why" }, s.why))]),
        h("span", { class: "td-dim" }, "?")) : null,
      h("div", { class: "td-actions" },
        h("button", { class: "btn btn-gold btn-small", onclick: () => ctx.newPage({ path: c.path || "docs/gdd/" + c.id + ".md", title: c.title, type: c.typeGuess || null, everywhere: true }) }, "Write it"),
        pickInPlace("It's another page…", "The page that's meant…", (p) => retarget(p), "td-link"),
        h("button", { class: "td-toggle", onclick: () => { if (open) todoState.open.delete("m:" + c.id); else todoState.open.add("m:" + c.id); renderBroken(); } },
          h("span", { class: "td-caret" }), (open ? "Hide " : "Show ") + plural(c.refs.length, "reference"))),
      open ? h("ul", { class: "td-refs" }, c.refs.map(refRow)) : null);
  }

  function referenceCard(r) {
    const q = (x) => h("span", { class: "td-name" }, x);
    const title = { plain: [q(r.text), " is named, not linked"], heading: ["A link to ", q("#" + r.anchor), ", which isn't a heading"], outside: ["A link out of the GDD"] }[r.problem];
    const why = {
      plain: `${r.field ? "In " + r.field + ". " : ""}Lists match it by its title, so renaming ${r.toTitle || where(r.to)} would quietly break it.`,
      heading: `${where(r.to)} has no heading “${r.anchor}” (any more).`,
      outside: `${r.to} isn't a page of the GDD.`,
    }[r.problem];
    const actions = {
      plain: [h("button", { class: "btn btn-gold btn-small", onclick: () => applyFix({ kind: "link-field", page: r.from, line: r.line, text: r.text, to: r.to }, `Linked “${r.text}”`) }, "Make it a link")],
      heading: [h("button", { class: "btn btn-gold btn-small", onclick: () => applyFix({ kind: "drop-anchor", page: r.from, line: r.line, target: r.to }, "Linked to the page instead") }, "Link to the page instead"),
        h("button", { class: "td-link", onclick: () => ctx.openDoc(r.to) }, "Open " + where(r.to))],
      outside: [pickInPlace("Point at a GDD page…", "The page this link means…", (p) => applyFix({ kind: "retarget-one", page: r.from, line: r.line, from: r.to, to: p.path }, "Link pointed elsewhere"), "btn btn-gold btn-small"),
        h("button", { class: "td-link", onclick: () => applyFix({ kind: "unlink", page: r.from, line: r.line, target: r.to }, "Link removed") }, "Unlink")],
    }[r.problem];
    return h("div", { class: "td-item" },
      h("div", { class: "td-head" }, h("div", { class: "td-title" }, title), h("span", { class: "td-count" }, "on ", fromLink(r))),
      h("div", { class: "td-sub" }, why),
      r.context ? h("div", { class: "td-context" }, r.context) : null,
      h("div", { class: "td-actions" }, actions));
  }

  function placeholderCard(p) {
    const input = h("input", { class: "field td-name", placeholder: "Its real name", spellcheck: "true" });
    const go = () => input.value.trim() && applyFix({ kind: "name-placeholder", token: p.token, name: input.value.trim() }, `Named ${p.token}`);
    input.addEventListener("keydown", (e) => { if (e.key === "Enter") go(); });
    return h("div", { class: "td-item" },
      h("div", { class: "td-head" }, h("div", { class: "td-title" }, h("span", { class: "td-token" }, p.token)),
        h("span", { class: "td-count" }, "used on ", plural(p.pages, "page"))),
      h("div", { class: "td-sub" }, "Waiting for its real name. On ", [...new Map(p.refs.map((r) => [r.from, r])).values()].map((r, i) => [i ? ", " : "", h("a", { class: "td-from", onclick: () => ctx.openDoc(r.from, { find: { q: p.token, occurrence: 0 } }) }, r.fromTitle || where(r.from))]), "."),
      p.page
        ? h("div", { class: "td-actions" }, h("span", { class: "td-dim" }, "It's also a page's file name: rename that page, and every reference follows. "), h("button", { class: "btn btn-gold btn-small", onclick: () => ctx.openDoc(p.page) }, "Open the page"))
        : h("div", { class: "td-actions" }, input, h("button", { class: "btn btn-gold btn-small", onclick: go }, "Name it everywhere")));
  }

  function structureCard(s) {
    const c = s.component ? s.component.replace(/-/g, " ").replace(/^./, (x) => x.toUpperCase()) : "";
    const fixes = {
      declare: [h("button", { class: "btn btn-gold btn-small", onclick: () => applyFix({ kind: "declare", page: s.from, component: s.component }, `${c} added to the components`) }, `Add ${c} to its components`)],
      "add-section": [h("button", { class: "btn btn-gold btn-small", onclick: () => applyFix({ kind: "add-section", page: s.from, component: s.component }, `${c} section added`) }, `Add the ${c} section`),
        h("button", { class: "td-link", onclick: () => applyFix({ kind: "undeclare", page: s.from, component: s.component }, `${c} taken off`) }, "Take it off the components")],
      undeclare: [h("button", { class: "btn btn-gold btn-small", onclick: () => applyFix({ kind: "undeclare", page: s.from, component: s.component }, `${s.component} taken off`) }, "Take it off the components")],
      reorder: [h("button", { class: "btn btn-gold btn-small", onclick: () => applyFix({ kind: "reorder", page: s.from }, "Sections put in order") }, "Put them in order")],
      "move-folder": s.to ? [h("button", { class: "btn btn-gold btn-small", onclick: async () => {
        if (S.path && !(await ctx.save())) return;
        const r = await api("/api/move", { method: "POST", body: JSON.stringify({ from: s.from, to: s.to }) });
        if (!r.ok) return ctx.toast(failed("Couldn't move it", r), "bad");
        ctx.toast(`Moved to ${where(s.to)}; links to it follow`, "good");
        if (S.path === s.from) { S.path = s.to; S.reloadOpen = true; }
        refresh();
      } }, `Move it to ${folderOf(s.to) || "the top"}/`)] : null,
    }[s.fix];
    return h("div", { class: "td-item" },
      h("div", { class: "td-head" }, h("div", { class: "td-title" }, s.fromTitle || where(s.from)), h("span", { class: "td-count td-path" }, where(s.from))),
      h("div", { class: "td-sub" }, s.text, "."),
      h("div", { class: "td-actions" }, fixes || null, h("button", { class: fixes ? "td-link" : "btn btn-small", onclick: () => ctx.openDoc(s.from) }, fixes ? "Open" : "Open the page to fix it")));
  }

  // What R'th AI's health check found across pages (src/audit.js): the problem, where, and its plan; Fix
  // with R'th AI starts a conversation in Chat mode with the plan, so the fix comes as suggestions.
  const FOUND_KIND = { "contradiction": "Contradiction", "copied-fact": "Copied, not linked", "settled-question": "Settled elsewhere", "missing-link": "Named, not linked", "stale-reference": "Out of date" };
  /** A comment started in Discord, on no page: what it says, and Put it on a page (src/notes.js). */
  function unfiledCard(n) {
    const first = n.thread?.[0] || {};
    const who = first.who?.name || (first.by === "agent" ? "R'th AI" : "Someone");
    return h("div", { class: "td-item" },
      h("div", { class: "td-head" }, h("div", { class: "td-title" }, n.anchor?.title || "A comment"),
        n.thread?.length > 1 ? h("span", { class: "td-count" }, `${n.thread.length} messages`) : null),
      h("div", { class: "td-sub" }, `${who}: ${String(first.text || "").replace(/\s+/g, " ").slice(0, 160)}`),
      h("div", { class: "td-actions" },
        h("button", { class: "btn btn-gold btn-small", onclick: () => ctx.placeComment(n) }, "Put it on a page…")));
  }

  function foundCard(p) {
    const open = todoState.open.has(p.id);
    const at = (i) => h("a", { class: "td-from", title: p.pages[i], onclick: () => ctx.openDoc(p.pages[i], p.quotes?.[i] ? { find: { q: p.quotes[i], occurrence: 0 } } : {}) }, p.titles?.[i] || where(p.pages[i]));
    return h("div", { class: "td-item td-found" },
      h("div", { class: "td-head" },
        h("div", { class: "td-title" }, p.summary),
        h("span", { class: "td-count td-kind" }, FOUND_KIND[p.kind] || p.kind)),
      h("div", { class: "td-sub" }, ...p.pages.flatMap((_, i) => (i ? [", ", at(i)] : [at(i)]))),
      open ? h("div", { class: "td-found-more" },
        p.detail ? h("p", {}, p.detail) : null,
        h("p", {}, h("b", {}, "The plan: "), p.plan),
        h("p", { class: "td-dim" }, `Found by ${p.found?.by || "R'th AI"}${p.found?.at ? ", " + new Date(p.found.at).toLocaleDateString("en-GB", { day: "numeric", month: "short" }) : ""}.`)) : null,
      h("div", { class: "td-actions" },
        h("button", { class: "btn btn-gold btn-small", title: "A conversation with R'th AI about it, in Chat mode: its fix comes as suggestions to accept or skip", onclick: () => ctx.fixWithAi(p) }, "Fix with R'th AI"),
        h("button", { class: "td-link", onclick: () => { open ? todoState.open.delete(p.id) : todoState.open.add(p.id); renderBroken(); } }, open ? "Less" : "More"),
        h("button", { class: "td-link", title: "Take it off the To-do, for everyone", onclick: async () => {
          const r = await api("/api/problems/dismiss", { method: "POST", body: JSON.stringify({ file: p.file, id: p.id }) });
          if (!r.ok) return ctx.toast(failed("Couldn't dismiss it", r), "bad");
          refresh();
        } }, "Dismiss")));
  }

  async function renderBroken() {
    const view = $("#statusView");
    if (!lists.broken) view.replaceChildren(h("p", { class: "st-loading" }, "Loading…"));
    const todo = await broken(true);
    if (S.view !== "broken" || !todo) return;
    const all = [
      ...(todo.found || []).map((x) => ({ cat: "found", pages: x.pages, el: () => foundCard(x) })),
      ...(todo.unfiled || []).map((x) => ({ cat: "unfiled", pages: [], el: () => unfiledCard(x) })),
      ...todo.missing.map((x) => ({ cat: "missing", pages: x.refs.map((r) => r.from).concat(x.path || []), el: () => missingCard(x) })),
      ...todo.references.map((x) => ({ cat: "references", pages: [x.from], el: () => referenceCard(x) })),
      ...todo.placeholders.map((x) => ({ cat: "placeholders", pages: x.refs.map((r) => r.from), el: () => placeholderCard(x) })),
      ...todo.structure.map((x) => ({ cat: "structure", pages: [x.from], el: () => structureCard(x) })),
    ];
    const inFolder = (x) => !todoState.folder || x.pages.some((p) => folderOf(p) === todoState.folder);
    // Each kind is a section of the journal, under its own heading and icon; the tabs pick one or all.
    const CATS = [
      { key: "found", tab: "Problems", title: "Problems found", icon: "quests", blurb: "What R'th AI's health check found across pages, each with a plan." },
      { key: "unfiled", tab: "Comments", title: "Comments to place", icon: "note", blurb: "Started in Discord, so they aren't on a page yet." },
      { key: "missing", tab: "To write", title: "Pages to write", icon: "scripture", blurb: "Linked to, but not written yet." },
      { key: "references", tab: "Links", title: "Links to mend", icon: "links", blurb: "References that lead somewhere wrong." },
      { key: "placeholders", tab: "Names", title: "Names to choose", icon: "note", blurb: "Placeholder names still waiting for real ones." },
      { key: "structure", tab: "Tidying", title: "Pages to tidy", icon: "settings", blurb: "Pages whose sections and components disagree." },
    ];
    const count = (cat) => all.filter((x) => (cat === "all" || x.cat === cat) && inFolder(x)).length;
    if (todoState.filter !== "all" && !count(todoState.filter)) todoState.filter = "all";
    const shown = all.filter((x) => (todoState.filter === "all" || x.cat === todoState.filter) && inFolder(x));
    const folders = [...new Set(all.flatMap((x) => x.pages.map(folderOf)))].filter(Boolean).sort();
    const tab = (key, label) => h("button", { class: "td-tab" + (todoState.filter === key ? " on" : ""), onclick: () => { todoState.filter = key; renderBroken(); } },
      label, h("span", { class: "td-tab-n" }, String(count(key))));
    const group = (c) => {
      const items = shown.filter((x) => x.cat === c.key);
      return items.length ? h("section", { class: "td-group" },
        h("div", { class: "td-group-head" },
          h("span", { class: "ico ico-" + c.icon }),
          h("span", { class: "td-group-title" }, c.title),
          h("span", { class: "td-group-blurb" }, c.blurb)),
        h("div", { class: "td-ledger" }, items.map((x) => x.el()))) : null;
    };
    view.replaceChildren(h("article", { class: "prose td" },
      h("h1", {}, "To-do"),
      todo.audit?.available ? h("div", { class: "td-audit" },
        todo.audit.running ? h("span", { class: "td-dim" }, `R'th AI is checking ${todo.audit.running.length} page${todo.audit.running.length === 1 ? "" : "s"}…`)
          : todo.audit.sweep ? h("span", { class: "td-dim" }, "Checking the whole GDD, a few pages at a time while this Codex is idle.")
          : h("button", { class: "td-link", title: "R'th AI reads every page not checked in its current version, a few at a time while this Codex is idle", onclick: async () => {
            const r = await api("/api/audit/sweep", { method: "POST" });
            ctx.toast(r.pages ? `R'th AI will check ${r.pages} page${r.pages === 1 ? "" : "s"}, a few at a time while you're away` : "Every page has been checked", "good");
            renderBroken();
          } }, "Check the whole GDD"),
        todo.audit.error ? h("span", { class: "td-dim", title: todo.audit.error }, " · The last check didn't finish") : null) : null,
      h("p", { class: "oq-intro td-intro" }, all.length
        ? "What the GDD still needs. Every fix can be undone, and one that changes several pages names them first."
        : "Nothing to do: every reference goes to a written page, every name is real, and every page's structure is in order."),
      all.length ? h("div", { class: "td-filters" },
        h("div", { class: "td-tabs" }, tab("all", "All"), CATS.map((c) => count(c.key) ? tab(c.key, c.tab) : null)),
        folders.length > 1 ? h("select", { class: "field td-folder", onchange: (e) => { todoState.folder = e.target.value; renderBroken(); } },
          h("option", { value: "" }, "Every folder"), folders.map((f) => h("option", { value: f, selected: todoState.folder === f || null }, f))) : null) : null,
      all.length && !shown.length ? h("p", { class: "td-none" }, "Nothing here in this folder.") : null,
      CATS.map(group)));
  }

  return { linksFor, renderLinks, renderChanges, renderBroken, changes, broken, unseen, invalidate };
}

const timeOf = (iso) => new Date(iso).toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hourCycle: "h23" });

/** "Today", "Yesterday", or the date ("Monday 2 November"; the year too when it isn't this one). */
function dayName(day) {
  const [y, m, d] = day.split("-").map(Number);
  const date = new Date(y, m - 1, d), today = new Date();
  today.setHours(0, 0, 0, 0);
  const ago = Math.round((today - date) / 86400000);
  if (ago === 0) return "Today";
  if (ago === 1) return "Yesterday";
  return date.toLocaleDateString("en-GB", { weekday: "long", day: "numeric", month: "long", ...(y !== today.getFullYear() ? { year: "numeric" } : {}) });
}
