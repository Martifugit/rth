// Find in all pages (Ctrl+Shift+F): every match in the GDD's pages, by page, and Replace in the pages
// ticked. Only the pages' text is searched and replaced, never frontmatter, where links point or code
// (src/find.js, on the server: /api/find, /api/replace). A replace can be undone from its toast, page by
// page as long as nobody has changed that page since. Clicking a line opens the page at that match,
// with the find bar (src/find-bar.js) on it.
//
//   makeFindAll({ S, h, api, modal, toast, openDoc, save, titleOf, findBar }) → { open(q) }

export function makeFindAll({ S, h, api, modal, toast, openDoc, save, titleOf, findBar }) {
  const opts = { matchCase: false, wholeWord: false };

  function open(initial = "") {
    const find = h("input", { class: "field fa-input", placeholder: "Find in all pages", spellcheck: "false" });
    const rep = h("input", { class: "field fa-input", placeholder: "Replace with", spellcheck: "false" });
    find.value = initial;
    const head = h("div", { class: "fa-head muted small" });
    const note = h("div", { class: "fa-note muted small" }, "Only the pages' text changes: never frontmatter, where links point, or code. Undo is in the message after.");
    const list = h("div", { class: "fa-list" });
    const toggle = (label, title, prop) => {
      const b = h("button", { class: "find-opt" + (opts[prop] ? " on" : ""), title, onclick: () => { opts[prop] = !opts[prop]; b.classList.toggle("on", opts[prop]); run(); } }, label);
      return b;
    };
    const go = h("button", { class: "btn btn-small", disabled: true, onclick: () => replace() }, "Replace");
    let res = { results: [], total: 0 }, skip = new Set(), seq = 0, timer = null, sure = null; // sure: the second click's timer

    const ticked = () => res.results.filter((r) => !skip.has(r.path));
    const draw = () => {
      const q = find.value;
      const t = ticked();
      head.textContent = !q.trim() ? "Type to find text in every page." : res.total ? `${res.total} match${res.total === 1 ? "" : "es"} in ${res.results.length} page${res.results.length === 1 ? "" : "s"}` : "Nothing found.";
      go.disabled = !t.length || !!S.readOnly;
      const n = t.reduce((a, r) => a + r.count, 0);
      go.textContent = !t.length ? "Replace" : sure ? `Click again: replace ${n} in ${t.length} page${t.length === 1 ? "" : "s"}` : `Replace in ${t.length} page${t.length === 1 ? "" : "s"}`;
      go.classList.toggle("btn-gold", !!sure);
      go.title = S.readOnly ? "Sign in with Discord to edit" : "";
      list.replaceChildren(...res.results.map((r) => h("div", { class: "fa-page" },
        h("label", { class: "fa-page-head" },
          h("input", { type: "checkbox", checked: !skip.has(r.path) || null, onchange: (e) => { if (e.target.checked) skip.delete(r.path); else skip.add(r.path); draw(); } }),
          h("span", { class: "fa-title" }, titleOf(r.path)), h("span", { class: "fa-count" }, String(r.count))),
        r.hits.map((x, i) => h("button", { class: "fa-hit", onclick: () => jump(r.path, i) }, h("span", { class: "fa-line" }, String(x.line)), line(x))),
        r.count > r.hits.length ? h("div", { class: "fa-more muted small" }, `and ${r.count - r.hits.length} more`) : null)));
    };
    // The line around its match, the match in bold.
    const line = (x) => {
      const from = Math.max(0, x.at - 50);
      const pre = (from ? "…" : "") + x.text.slice(from, x.at).trimStart();
      return h("span", { class: "fa-text" }, pre, h("b", {}, x.text.slice(x.at, x.at + x.len)), x.text.slice(x.at + x.len, x.at + x.len + 120));
    };

    async function run() {
      const q = find.value, mine = ++seq;
      if (!q.trim()) { res = { results: [], total: 0 }; draw(); return; }
      const r = await api(`/api/find?q=${encodeURIComponent(q)}&case=${opts.matchCase ? 1 : 0}&word=${opts.wholeWord ? 1 : 0}`);
      if (mine !== seq) return;
      res = { results: r.results || [], total: r.total || 0 };
      draw();
    }

    async function jump(page, i) {
      close();
      if (page !== S.path || S.view) await openDoc(page);
      findBar.open({ q: find.value, ...opts, at: i });
    }

    // Two clicks: the first says how much will change, the second changes it.
    async function replace() {
      if (!sure) { sure = setTimeout(() => { sure = null; draw(); }, 4000); draw(); return; }
      clearTimeout(sure); sure = null;
      const t = ticked(), q = find.value, w = rep.value;
      // The open page saved first, so what's replaced is what you see; read again after.
      if (S.dirty && !(await save(false, { quiet: true }))) return;
      const r = await api("/api/replace", { method: "POST", body: JSON.stringify({ q, with: w, ...opts, pages: t.map((x) => x.path) }) });
      if (!r.ok) { toast("Couldn't replace: " + (r.error || r.status), "bad"); return; }
      if (r.pages.some((p) => p.path === S.path)) await openDoc(S.path, { push: false, keepScroll: true });
      toast(`Replaced ${r.count} in ${r.pages.length} page${r.pages.length === 1 ? "" : "s"}`, "", { actions: [{ label: "Undo", run: () => undo(r.undo, r.pages) }] });
      run();
    }
    const unsure = () => { if (sure) { clearTimeout(sure); sure = null; } };

    async function undo(id, pages) {
      const r = await api("/api/replace/undo", { method: "POST", body: JSON.stringify({ id }) });
      if (!r.ok) { toast(r.error || "Couldn't undo the replace", "bad"); return; }
      if (pages.some((p) => p.path === S.path)) await openDoc(S.path, { push: false, keepScroll: true });
      toast(r.kept.length ? `Undone, except in ${r.kept.map(titleOf).join(", ")}: changed since` : "Replace undone");
    }

    find.addEventListener("input", () => { unsure(); skip = new Set(); clearTimeout(timer); timer = setTimeout(run, 200); });
    rep.addEventListener("input", () => { unsure(); draw(); });
    find.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); clearTimeout(timer); run(); } });
    rep.addEventListener("keydown", (e) => { if (e.key === "Enter" && !go.disabled) { e.preventDefault(); replace(); } });
    const close = modal("Find in all pages", h("div", { class: "fa" },
      h("div", { class: "fa-row" }, find, toggle("Aa", "Match case", "matchCase"), toggle("W", "Whole words only", "wholeWord")),
      h("div", { class: "fa-row" }, rep, go), note, head, list), { width: "min(760px, 94vw)" });
    draw();
    if (initial) run();
    setTimeout(() => { find.focus(); find.select(); }, 0);
  }

  return { open };
}
