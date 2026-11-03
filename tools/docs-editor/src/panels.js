// The Links tab (what links to this page, what it links to, where it's mentioned) and Open questions
// (every GDD page's open questions in one list), shown in the main window.

const SECTION_NAMES = {
  "1-overview": "Overview", "2-gameplay": "Gameplay", "3-story": "Story", "4-content": "Content",
  "5-interface": "Interface", "6-behaviour": "Behaviour", appendices: "Appendices",
};

export function makePanels(ctx) {
  const { S, $, h, api } = ctx;
  const cache = new Map();
  const table = { data: null };

  // ----------------------------------------------------------------------------- links

  async function linksFor(page) {
    if (!cache.has(page)) cache.set(page, api("/api/links?page=" + encodeURIComponent(page)).then((r) => (r.ok ? r : null)));
    return cache.get(page);
  }
  function invalidate() { cache.clear(); table.data = null; }

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
    const inbound = byPage(r.inbound, "from").map(([from, ls]) =>
      item(from, ls[0].title, ls[0].section || null, () => ctx.openDoc(from, { find: { q: ls[0].text, occurrence: 0 } })));
    const outbound = byPage(r.outbound, "to").map(([to, ls]) => ls[0].broken
      ? item(to, to.split("/").pop(), "This page doesn't exist", () => ctx.findInPage(ls[0].text), "broken")
      : item(to, ls[0].title, null, () => ctx.openDoc(to)));
    const mentions = r.mentions.map((m) => item(m.from, m.title, m.section || null, () => ctx.openDoc(m.from, { find: { q: r.title, occurrence: 0 } })));
    pane.replaceChildren(
      group("Linked from", "Pages with a link to this one", inbound, "No page links here yet."),
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
      return (rest.includes("/") ? "1/" + rest : (/^readme/i.test(rest) ? "0/" : "2/") + rest).replace(/(^|\/)(index|readme)\.md$/i, "$1!");
    };
    const pages = table.data.filter((p) => p.questions.length).sort((a, b) => order(a).localeCompare(order(b), "en", { numeric: true }));
    const total = pages.reduce((n, p) => n + p.questions.length, 0);
    const groups = new Map();
    for (const p of pages) {
      const rest = p.path.slice("docs/gdd/".length);
      const folder = rest.includes("/") ? rest.split("/")[0] : "";
      const name = SECTION_NAMES[folder] || "The GDD";
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

  return { linksFor, renderLinks, renderStatus, invalidate };
}

// The first few words of a question, to find it in the page (a search can't cross bold or links).
function firstWords(text) {
  return text.split(/\s+/).slice(0, 4).join(" ");
}
