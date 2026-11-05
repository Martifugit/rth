// After R'th AI changes pages, the Codex checks what it changed (PLAN: R'th AI's tools, step 4): the
// structure checks the To-do runs, links to headings that aren't there, and spelling, on the pages as
// the AI left them. Only what the turn brought in counts: a problem the page had before isn't the AI's.
// The answer shows them as small cards, each with a fix (the To-do's, where there is one) or Dismiss.
//
// Links to pages not written yet aren't problems: the GDD links to what doesn't exist yet on purpose
// (they're to-dos); nor are p- placeholders, groups or variants without a page.

const IGNORE = new Set(["missing-page", "placeholder", "missing-group", "missing-based-on"]);
// The To-do's own fixes for structure problems (src/todo.js, src/fixes.js).
const FIX = { "undeclared-section": "declare", "missing-section": "add-section", "unknown-component": "undeclare", "section-order": "reorder" };

/**
 * tools: src/ai-tools.js. before: Map(page → text) before the turn; after: the same as the turn left them
 * (for suggestions, as they would be). changes: the turn's [{ page, before, after }].
 * → [{ page, kind, line, text, fix?, word? }], at most `max`.
 */
export async function checkTurn({ tools, before, after, changes, max = 12 }) {
  const idxBefore = tools.indexOf(before), idxAfter = tools.indexOf(after);
  const key = (p) => `${p.kind}|${p.word || p.text}`;
  const out = [];
  for (const x of changes) {
    if (x.after == null || !/^docs\/gdd\//.test(x.page) || x.page.includes("/templates/")) continue;
    const now = await tools.problemsOn(x.page, { idx: idxAfter });
    const had = new Set(x.before != null ? (await tools.problemsOn(x.page, { idx: idxBefore })).map(key) : []);
    for (const p of now) {
      if (IGNORE.has(p.kind) || had.has(key(p))) continue;
      const fix = FIX[p.kind] ? { kind: FIX[p.kind], page: x.page, ...(p.component ? { component: p.component } : {}) } : null;
      out.push({ page: x.page, kind: p.kind, line: p.line || null, text: p.text, ...(fix ? { fix } : {}), ...(p.word ? { word: p.word } : {}) });
      if (out.length >= max) return out;
    }
  }
  return out;
}

/** The texts after a turn: the before map with each change applied (a deleted page left out). */
export function afterTexts(before, changes) {
  const m = new Map(before);
  for (const x of changes) { if (x.after == null) m.delete(x.page); else m.set(x.page, x.after); }
  return m;
}
