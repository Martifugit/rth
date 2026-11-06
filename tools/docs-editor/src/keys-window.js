// The Keys and Markdown window (? or the help button). Two tabs:
//  - Keys: every shortcut, one action a row, in cards by what they're for. When a key only works
//    somewhere (in the comment box, while R'th AI's changes are shown), that's a label over its rows,
//    not words in brackets. Keys are named as this keyboard has them (src/keys.js, label).
//  - Markdown: what to type, what it becomes, and how to get it in the page.
//
// KEYS is the list shown; the keys themselves are handled where they work (src/app.js SHORTCUTS, the
// editor, the boxes). test/keys-window.test.js checks every SHORTCUTS key is listed here.

import { label } from "./keys.js";

// A row: [what it does, key, another key for the same thing...]. A key is a spec (src/keys.js);
// "Escape*2" is the key pressed twice. column: which of the two columns the card is in.
export const KEYS = [
  { title: "Writing", column: 0, when: "In a page", parts: [
    { rows: [
      ["Bold", "mod+b"], ["Italic", "mod+i"], ["Strikethrough", "mod+shift+s"], ["Inline code", "mod+e"],
      ["Link", "mod+k"], ["Line break", "shift+Enter"], ["Undo", "mod+z"], ["Redo", "mod+y", "mod+shift+z"],
    ] },
    { label: "Turn the line into", rows: [
      ["Title", "mod+shift+Digit1"], ["Heading", "mod+shift+Digit2"], ["Subheading", "mod+shift+Digit3"],
      ["Heading 4", "mod+shift+Digit4"], ["Heading 5", "mod+shift+Digit5"], ["Heading 6", "mod+shift+Digit6"],
      ["Numbered list", "mod+shift+Digit7"], ["Bulleted list", "mod+shift+Digit8"], ["Checklist", "mod+shift+Digit9"],
      ["Quote", "mod+shift+b"], ["Code block", "mod+alt+c"],
    ], note: "The same keys again turn it back into plain text." },
    { label: "In a list", rows: [["Indent the item", "Tab"], ["Outdent the item", "shift+Tab"]] },
  ] },
  { title: "Finding", column: 1, parts: [
    { rows: [
      ["Search pages and text", "mod+p", "/"], ["Find in this page", "mod+f"],
      ["Find and replace in this page", "mod+h"], ["Find and replace in all pages", "mod+shift+f"],
    ] },
    { label: "In the find bar", rows: [["Next match", "Enter"], ["Previous match", "shift+Enter"]] },
    { label: "In Replace with", rows: [["Replace the match", "Enter"], ["Replace all", "mod+Enter"]] },
  ] },
  { title: "Pages", column: 0, parts: [
    { rows: [
      ["New page", "mod+n"], ["Save", "mod+s"], ["Back", "alt+ArrowLeft"], ["Forward", "alt+ArrowRight"],
      ["To-do", "mod+shift+d"], ["Recent changes", "mod+shift+e"],
    ] },
    { label: "In a page", rows: [["Copy for Discord, with a link back", "mod+shift+c"]] },
  ] },
  { title: "Comments", column: 0, parts: [
    { rows: [
      ["Comment on the selection or page", "mod+m"], ["Next comment", "alt+PageDown"], ["Previous comment", "alt+PageUp"],
      ["All comments", "mod+shift+m"],
    ] },
    { label: "In the comment box", rows: [["Post", "mod+Enter"], ["Close", "Escape"], ["Discard what you wrote", "Escape*2"]] },
  ] },
  { title: "R'th AI", column: 1, parts: [
    { rows: [["Ask R'th AI about the page or selection", "mod+j"]] },
    { label: "In the message box", rows: [
      ["Send", "Enter"], ["New line", "shift+Enter"], ["Switch between Chat and Work", "shift+Tab"], ["Add a page", "@"],
    ] },
    { label: "While R'th AI's changes are shown", rows: [
      ["Next change", "alt+ArrowDown"], ["Previous change", "alt+ArrowUp"],
      ["Accept the suggestion", "alt+Enter"], ["Skip the suggestion", "alt+Backspace"],
    ] },
  ] },
  { title: "Panes and window", column: 1, parts: [
    { rows: [
      ["Show or hide the page list", "mod+Comma", "mod+Backslash"],
      ["Show or hide the right pane", "mod+Period", "mod+shift+Backslash"],
      ["Outline", "alt+Digit1"], ["Links", "alt+Digit2"], ["R'th AI tab", "alt+Digit3"],
      ["Keys and Markdown", "?"], ["Close a window, menu or bar", "Escape"], ["Leave the Markdown view", "Escape"],
    ] },
    { label: "In the desktop app", rows: [["Zoom in", "mod++"], ["Zoom out", "mod+-"], ["Actual size", "mod+Digit0"]] },
  ] },
];

// [what you type, what it becomes (HTML, drawn as a page draws it), how to get it in the page: words with
// `typed characters` (or a list of words and { code } parts), or { keys } for a shortcut]
const TYPE = "Type it";
export const MARKDOWN = [
  ["# Title", "<h1>Title</h1>", TYPE],
  ["## Heading", "<h2>Heading</h2>", TYPE],
  ["### Subheading", "<h3>Subheading</h3>", TYPE],
  ["#### Heading 4", "<h4>Heading 4</h4>", TYPE],
  ["**bold**", "<p><strong>bold</strong></p>", TYPE],
  ["*italic*", "<p><em>italic</em></p>", TYPE],
  ["~~struck~~", "<p><s>struck</s></p>", TYPE],
  ["`code`", "<p><code>code</code></p>", TYPE],
  ["[Combat](combat.md)", "<p><a>Combat</a></p>", { keys: "mod+k" }],
  ["- item", "<ul><li>item</li></ul>", TYPE],
  ["1. item", "<ol><li>item</li></ol>", TYPE],
  ["- [ ] task", '<ul class="kw-task"><li><input type="checkbox" disabled> task</li></ul>', "Type `[ ]` at the start of a line"],
  ["> quote", "<blockquote><p>quote</p></blockquote>", TYPE],
  ["```\ncode\n```", "<pre><code>code</code></pre>", ["Type ", { code: "```" }, " and a space"]],
  ["---", "<hr>", TYPE],
  ["| A | B |\n|---|---|\n| 1 | 2 |", "<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>", "Table button in the toolbar"],
  ["![Map](images/map.png)", '<p><img src="/logo.png" alt="" class="kw-img"></p>', "Image button in the toolbar"],
];

export function keysWindow({ h, modal, tab = "keys" }) {
  const keys = (spec) => {
    const twice = spec.endsWith("*2");
    const caps = label(twice ? spec.slice(0, -2) : spec).map((x) => h("kbd", {}, x));
    return h("span", { class: "kw-combo" }, caps, twice ? h("span", { class: "kw-or" }, "twice") : null);
  };
  const row = ([what, ...specs]) => h("div", { class: "kw-row" },
    h("span", { class: "kw-what" }, what),
    h("span", { class: "kw-keys" }, specs.flatMap((s, i) => (i ? [h("span", { class: "kw-or" }, "or"), keys(s)] : [keys(s)]))));
  // "Type `[ ]` at the start": what's in backticks is shown as typed.
  // (Backticks themselves: a list of words and { code } parts.)
  const words = (text) => (Array.isArray(text)
    ? text.map((part) => (part.code ? h("code", {}, part.code) : part))
    : text.split(/`([^`]+)`/).map((part, i) => (i % 2 ? h("code", {}, part) : part)));
  const card = (g) => h("section", { class: "kw-card" },
    h("div", { class: "kw-head" }, h("span", { class: "kw-title" }, g.title), g.when ? h("span", { class: "kw-when" }, g.when) : null),
    g.parts.map((p) => h("div", { class: "kw-part" },
      p.label ? h("div", { class: "kw-label" }, p.label) : null,
      p.rows.map(row),
      p.note ? h("div", { class: "kw-note" }, p.note) : null)));

  const keysPane = h("div", { class: "kw-cards" }, [0, 1].map((c) => h("div", { class: "kw-col" }, KEYS.filter((g) => g.column === c).map(card))));
  const mdPane = h("div", { class: "kw-md" },
    h("div", { class: "kw-md-row kw-md-headrow" }, h("span", {}, "Markdown"), h("span", {}, "Looks like"), h("span", {}, "In the page")),
    MARKDOWN.map(([md, html, how]) => h("div", { class: "kw-md-row" },
      h("code", { class: "kw-md-src" }, md),
      h("div", { class: "prose kw-md-out", html }),
      h("span", { class: "kw-md-how" + (how === TYPE ? " plain" : "") }, typeof how.keys === "string" ? keys(how.keys) : words(how)))),
    h("p", { class: "kw-md-foot" }, "The </> button at the top of a page shows the page as Markdown, to read or edit."));

  const panes = { keys: keysPane, markdown: mdPane };
  const tabs = h("div", { class: "kw-tabs" });
  const body = h("div", { class: "kw-body" });
  const show = (id) => {
    tabs.replaceChildren(...[["keys", "Keys"], ["markdown", "Markdown"]].map(([t, name]) =>
      h("button", { class: "kw-tab" + (t === id ? " on" : ""), onclick: () => show(t) }, name)));
    body.replaceChildren(panes[id]);
  };
  show(tab);
  modal("Keys and Markdown", h("div", { class: "kw" }, tabs, body), { width: "min(980px, 95vw)" });
}
