// A field that picks a page, never free text. What's stored is the page's ID (its file name), so a
// reference can't be misspelt or silently point at nothing; renaming the page rewrites it everywhere
// (src/refs.js). Typing filters the pages by title; the only way to name a page that doesn't exist is
// the explicit "Make …" entry, which makes it.

import { slugOf } from "./frontmatter.js";

/**
 * { h, pages: [{ path, title, note }], placeholder, onPick(page), create: { label(q), run(q) } | null }.
 * Returns the element: an input with a list under it.
 */
// inline: the list is part of the window, always shown and of a fixed height (the link window), rather
// than a dropdown over it: a window that is mostly the list doesn't need to scroll at the fourth result.
export function pagePicker({ h, pages, placeholder = "Find a page…", onPick, create = null, inline = false }) {
  const input = h("input", { class: "field pp-input", placeholder, spellcheck: "false", autocomplete: "off" });
  const list = h("div", { class: "pp-list" + (inline ? "" : " hidden") });
  const wrap = h("div", { class: "pp" + (inline ? " inline" : "") }, input, list);
  let items = [], at = 0;
  const norm = (s) => String(s || "").toLowerCase().trim();

  const draw = () => {
    const q = norm(input.value);
    const asId = q.replace(/[^a-z0-9]+/g, "-");
    const hits = pages.filter((p) => !q || norm(p.title).includes(q) || slugOf(p.path).includes(asId)).slice(0, inline ? 200 : 40);
    items = hits.map((p) => ({ label: [h("span", { class: "pp-title" }, p.title || slugOf(p.path)), p.note ? h("span", { class: "pp-note" }, p.note) : null], run: () => onPick(p) }));
    const typed = input.value.trim(); // kept now: choosing clears the input before running
    if (create && q && !pages.some((p) => norm(p.title) === q)) items.push({ label: h("span", { class: "pp-create" }, create.label(typed)), run: () => create.run(typed) });
    if (!items.length) items.push({ label: h("span", { class: "pp-none" }, q ? "No page by that name" : "No pages to pick"), run: null });
    at = Math.max(0, Math.min(at, items.length - 1));
    list.replaceChildren(...items.map((it, i) => h("button", {
      class: "pp-item" + (i === at ? " on" : ""), disabled: it.run ? null : true,
      onmousedown: (e) => { e.preventDefault(); choose(i); }, onmouseenter: () => { at = i; mark(); },
    }, it.label)));
  };
  const mark = () => [...list.children].forEach((b, i) => b.classList.toggle("on", i === at));
  const open = () => { list.classList.remove("hidden"); draw(); };
  const close = () => { if (!inline) list.classList.add("hidden"); };
  const choose = (i) => {
    const it = items[i];
    if (!it?.run) return;
    input.value = "";
    close();
    if (inline) draw();
    it.run();
  };

  input.addEventListener("focus", open);
  input.addEventListener("input", () => { at = 0; open(); });
  input.addEventListener("blur", () => setTimeout(close, 120));
  input.addEventListener("keydown", (e) => {
    if (e.key === "ArrowDown") { e.preventDefault(); at = Math.min(items.length - 1, at + 1); mark(); list.children[at]?.scrollIntoView({ block: "nearest" }); }
    else if (e.key === "ArrowUp") { e.preventDefault(); at = Math.max(0, at - 1); mark(); list.children[at]?.scrollIntoView({ block: "nearest" }); }
    else if (e.key === "Enter") { e.preventDefault(); choose(at); }
  });
  if (inline) draw(); // shown from the start: every page, before anything's typed
  return wrap;
}
