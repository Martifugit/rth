// The Links tab (what links to this page, what it links to, where it's mentioned), and the special pages
// shown in the main window: Open questions (every GDD page's, in one list), Recent changes (who changed
// which page and where, from git) and Broken links (links to pages or headings that don't exist).

import { slug } from "./links.js";


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
  function invalidate() { cache.clear(); table.data = null; lists.broken = null; }

  async function renderLinks() {
    const pane = $("#linksPane");
    if (!S.path) return;
    const page = S.path;
    const r = await linksFor(page);
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
    pane.replaceChildren(
      ...labelled.map(([label, ls]) => group(label, `Links to this page from those pages' sections that own the relationship`, inboundOf(ls), "")),
      group(labelled.length ? "Also linked from" : "Linked from", "Pages with a link to this one", inbound, labelled.length ? "No other links." : "No page links here yet."),
      group("Links to", "Pages this one links to", outbound, "This page has no links to other pages."),
      r.title ? group("Mentioned in", `Pages that name “${r.title}” without linking to it`, mentions, "Not mentioned anywhere else.") : null);
  }

  // ----------------------------------------------------------------------------- open questions

  // Every GDD page's "Open questions", gathered into one page that reads like the rest of the docs:
  // under each GDD section, each page's questions under its title. A click goes to the question.
  async function renderStatus() {
    const view = $("#statusView");
    if (!table.data) {
      if (!view.childElementCount) view.replaceChildren(h("p", { class: "st-loading" }, "Loading…"));
      const r = await api("/api/board");
      table.data = r.pages || [];
    }
    const order = (p) => {
      const rest = p.path.slice("docs/gdd/".length);
      return (rest.includes("/") ? "1/" : "0/") + rest;
    };
    const pages = table.data.filter((p) => p.questions.length).sort((a, b) => order(a).localeCompare(order(b), "en", { numeric: true }));
    const total = pages.reduce((n, p) => n + p.questions.length, 0);
    const groups = new Map();
    for (const p of pages) {
      const rest = p.path.slice("docs/gdd/".length);
      const folder = rest.includes("/") ? rest.split("/")[0] : "";
      // Grouped by type folder ("entities" → "Entities"); the top pages under "The GDD".
      const name = folder ? folder[0].toUpperCase() + folder.slice(1).replace(/-/g, " ") : "The GDD";
      if (!groups.has(name)) groups.set(name, []);
      groups.get(name).push(p);
    }
    const go = (p, q) => ctx.openDoc(p.path, { hash: "open-questions", find: { q: firstWords(q.text), occurrence: 0 } });
    view.replaceChildren(h("article", { class: "prose oq" },
      h("h1", {}, "Open questions"),
      h("p", { class: "oq-intro" }, total
        ? `${total} decisions still open, from the “Open questions” at the end of ${pages.length} GDD pages. Click one to go to it.`
        : "No open questions: every GDD page's “Open questions” is empty."),
      [...groups].map(([name, list]) => [
        h("h2", {}, name),
        list.map((p) => h("section", { class: "oq-page" },
          h("h3", {}, h("a", { onclick: () => ctx.openDoc(p.path, { hash: "open-questions" }), title: p.path }, p.title || p.path.split("/").pop())),
          h("ol", {}, p.questions.map((q) => h("li", {}, h("a", { class: "oq-q", onclick: () => go(p, q), title: "Go to it" }, q.text)))))),
      ])));
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
      h("span", { class: "ch-time", title: new Date(e.date).toLocaleString() }, timeOf(e.date)),
      h("span", { class: "ch-what" },
        h("b", { class: "ch-who" }, e.mine ? "You" : e.author), " " + verb[e.status] + " ",
        e.exists ? h("a", { onclick: () => open(e), title: e.path }, e.title) : h("span", { class: "ch-gone", title: e.path }, e.title),
        e.sections.length ? [h("span", { class: "ch-in" }, " in "), sectionLinks(e, open)] : null,
        e.ai ? h("span", { class: "ch-ai", title: "Made with the AI" }, "with AI") : null));
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
  async function broken(force = false) {
    if (force || !lists.broken) {
      const [b, c] = await Promise.all([api("/api/broken"), api("/api/checks")]);
      if (b.ok && c.ok) {
        const pages = new Map();
        for (const l of b.links || []) if (l.kind === "page" && l.to.startsWith("docs/gdd/")) {
          if (!pages.has(l.to)) pages.set(l.to, { to: l.to, text: l.text, from: [] });
          pages.get(l.to).from.push(l);
        }
        // A group or a variant's original named in frontmatter but not written: a page to write, in its type's folder.
        const groups = S.types.find((t) => t.type === "group");
        const problems = [];
        for (const p of c.problems || []) {
          const slug = p.kind === "missing-group" ? p.text.match(/“(.+?)”/)?.[1] : null;
          if (!slug || !groups) { problems.push(p); continue; }
          const to = "docs/gdd/" + groups.folder + "/" + slug + ".md";
          if (!pages.has(to)) pages.set(to, { to, text: slug.replace(/-/g, " ").replace(/^./, (x) => x.toUpperCase()), from: [] });
          pages.get(to).from.push({ from: p.path, title: p.title, text: "" });
        }
        lists.broken = { pages: [...pages.values()], headings: (b.links || []).filter((l) => l.kind === "heading" || (l.kind === "page" && !l.to.startsWith("docs/gdd/"))), problems };
        lists.broken.length = lists.broken.pages.length + lists.broken.headings.length + lists.broken.problems.length;
      }
    }
    return lists.broken;
  }

  async function renderBroken() {
    const view = $("#statusView");
    if (!lists.broken) view.replaceChildren(h("p", { class: "st-loading" }, "Loading…"));
    const todo = await broken(true);
    if (S.view !== "broken" || !todo) return;
    const where = (p) => p.replace(/^docs\/gdd\//, "");
    const pageLink = (p, title, opts) => h("a", { onclick: () => ctx.openDoc(p, opts), title: p }, title || where(p));
    const byPage = (list) => { const m = new Map(); for (const x of list) { const k = x.from || x.path; if (!m.has(k)) m.set(k, []); m.get(k).push(x); } return [...m]; };
    view.replaceChildren(h("article", { class: "prose oq bl" },
      h("h1", {}, "To-do"),
      h("p", { class: "oq-intro" }, todo.length
        ? "What the GDD still needs. A link to a page that isn't written yet is a to-do, not a mistake: make it as a stub, two sentences, and follow the links from there."
        : "Nothing to do: every link goes to a written page, and every page has its type and its sections."),
      todo.pages.length ? [h("h2", {}, "Pages to write"), h("ul", { class: "td-list" }, todo.pages.map((x) => h("li", {},
        h("button", { class: "btn btn-gold td-make", onclick: () => ctx.newPage({ path: x.to, title: x.text || "" }) }, "Write it"),
        h("b", {}, x.text || where(x.to)), h("span", { class: "bl-why" }, " " + where(x.to) + " · from "),
        x.from.slice(0, 3).map((l, i) => [i ? ", " : "", pageLink(l.from, l.title, { find: { q: l.text, occurrence: 0 } })]),
        x.from.length > 3 ? h("span", { class: "bl-why" }, ` and ${x.from.length - 3} more`) : null)))] : null,
      todo.problems.length ? [h("h2", {}, "Structure"), byPage(todo.problems).map(([path, ps]) => h("section", { class: "oq-page" },
        h("h3", {}, pageLink(path, ps[0].title)),
        h("ul", { class: "bl-list" }, ps.map((p) => h("li", { class: "td-" + p.kind }, p.text)))))] : null,
      todo.headings.length ? [h("h2", {}, "Links that go nowhere"), byPage(todo.headings).map(([from, ls]) => h("section", { class: "oq-page" },
        h("h3", {}, pageLink(from, ls[0].title)),
        h("ul", { class: "bl-list" }, ls.map((l) => h("li", {},
          h("a", { class: "oq-q", title: l.href, onclick: () => ctx.openDoc(from, { find: { q: l.text, occurrence: 0 } }) }, l.text || l.href),
          h("span", { class: "bl-why" }, (l.section ? l.section + " · " : "") + (l.kind === "heading" ? `“${l.href.split("#").pop()}” isn't a heading on ${where(l.to)}` : `${l.to} isn't in the GDD`)))))))] : null));
  }

  return { linksFor, renderLinks, renderStatus, renderChanges, renderBroken, changes, broken, unseen, invalidate };
}

// The first few words of a question, to find it in the page (a search can't cross bold or links).
function firstWords(text) {
  return text.split(/\s+/).slice(0, 4).join(" ");
}

const timeOf = (iso) => new Date(iso).toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });

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
