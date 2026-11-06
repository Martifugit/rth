// Search (Ctrl+P, or /): pages by name as you type, then every line of text that has it, then "Ask R'th
// AI" (first when it reads as a question, so Enter asks). Enter or a click opens the page at the line.
//
//   makeQuickOpen({ S, h, api, modal, openDoc, askAi, templates }) → quickOpen()

const collator = new Intl.Collator("en", { numeric: true, sensitivity: "base" });

export function makeQuickOpen({ S, h, api, modal, openDoc, askAi, templates }) {
  const TEMPLATES = templates;
  const ai = { ask: (q) => askAi(q) };
  return function quickOpen() {
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
        const ask = { kind: "ask", q: raw, el: h("div", { class: "qo-item qo-ask" }, h("span", { class: "qo-ask-ico" }, "✦"), h("span", { class: "qo-title" }, "Ask R'th AI: "), h("span", { class: "qo-snippet" }, raw)) };
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
  };
}
