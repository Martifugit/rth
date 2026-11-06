// The Keys window (? or the logo's menu): every shortcut, the app's (src/app.js, SHORTCUTS) and the
// editor's own, named as this keyboard has them (src/keys.js, label).

import { label } from "./keys.js";

export const EDITOR_KEYS = [
  ["Writing", [
    ["mod+b", "Bold"], ["mod+i", "Italic"], ["mod+shift+s", "Strikethrough"], ["mod+k", "Add or edit a link"],
    ["mod+shift+Digit1", "Title (Ctrl+Shift+2-6: headings; the same keys again: back to a paragraph)"],
    ["mod+shift+Digit7", "Numbered list"], ["mod+shift+Digit8", "Bulleted list"], ["mod+shift+Digit9", "Checklist"],
    ["mod+shift+b", "Quote"], ["mod+alt+c", "Code block"], ["shift+Enter", "A new line in the same paragraph"],
    ["Tab", "In a list: indent the item (Shift+Tab: back out)"], ["mod+z", "Undo"], ["mod+y", "Redo (also Ctrl+Shift+Z)"],
  ]],
  ["The AI tab's box", [
    ["Enter", "Send (Shift+Enter: a new line)"], ["shift+Tab", "Switch between Chat (its changes wait for you to accept) and Work (it edits the pages)"],
    ["@", "Add a page to the conversation"],
    ["alt+ArrowDown", "With R'th AI's changes shown: the next change (Alt+↑ the one before)"],
    ["alt+Enter", "Accept the suggestion you're on (Alt+Backspace skips it)"],
  ]],
  ["Comments", [
    ["mod+Enter", "Post a comment"],
    ["Escape", "Close the comment box (with words written: twice)"],
  ]],
  ["The window", [["mod++", "Zoom in (Ctrl+- out, Ctrl+0 back to 100%)"]]],
];

export function keysWindow({ h, modal, shortcuts }) {
  const k = (spec, what) => [h("span", { class: "keys-k" }, label(spec).map((x) => h("kbd", {}, x))), h("span", {}, what)];
  const group = (title, rows) => [h("div", { class: "keys-group" }, title), ...rows.flatMap(([spec, what]) => k(spec, what))];
  const app = {};
  for (const x of shortcuts) if (x.what) (app[x.group] ||= []).push([x.keys[0], x.what]);
  modal("Keys", h("div", { class: "keys" },
    Object.entries(app).flatMap(([g, rows]) => group(g, rows)),
    EDITOR_KEYS.flatMap(([g, rows]) => group(g, rows)),
    h("span", { class: "muted small", style: "grid-column:1/-1;margin-top:8px" },
      "Markdown shortcuts work as you type: ## for a heading, - or 1. for a list, > for a quote, ``` for code. Saving only rewrites the paragraphs you changed; the rest of the file stays exactly as written.")),
  { width: "min(760px, 94vw)" });
}

