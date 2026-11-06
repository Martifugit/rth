// Finding and replacing text: the plain rules under Find in the page (src/find-bar.js) and Find in all
// pages (server.js, /api/find and /api/replace). Tested in test/find.test.js.
//
// Matching ignores case unless `matchCase`; `wholeWord` needs a non-letter (or the edge) on both sides.
// In a page's Markdown, only what reads as text is replaced: never its frontmatter, where a link points,
// an image's file, a reference definition's target, or the inside of code.

export const MIN = 1;

const isWord = (c) => !!c && /[\p{L}\p{N}_]/u.test(c);

/** Every match of q in text: [{ start, end }], left to right, never overlapping. */
export function findAll(text, q, { matchCase = false, wholeWord = false } = {}) {
  if (!q || q.length < MIN) return [];
  const hay = matchCase ? text : text.toLowerCase();
  const needle = matchCase ? q : q.toLowerCase();
  const out = [];
  for (let i = hay.indexOf(needle); i >= 0; i = hay.indexOf(needle, i + Math.max(1, needle.length))) {
    const end = i + needle.length;
    if (wholeWord && (isWord(text[i - 1]) || isWord(text[end]))) continue;
    out.push({ start: i, end });
  }
  return out;
}

/** The parts of a page's Markdown that are only syntax or addresses, as [start, end) ranges. */
export function protectedRanges(md) {
  const out = [];
  const add = (s, e) => { if (e > s) out.push([s, e]); };
  // Frontmatter.
  const fm = /^---\n[\s\S]*?\n---(\n|$)/.exec(md);
  if (fm) add(0, fm[0].length);
  // Fenced code blocks, then inline code.
  for (const m of md.matchAll(/^(```|~~~)[^\n]*\n[\s\S]*?(^\1[^\n]*$|(?![\s\S]))/gm)) add(m.index, m.index + m[0].length);
  for (const m of md.matchAll(/`[^`\n]+`/g)) add(m.index, m.index + m[0].length);
  // Where links and images point: the (…) after ](, and reference definitions' targets.
  for (const m of md.matchAll(/\]\(([^)\n]*)\)/g)) add(m.index + 2, m.index + m[0].length - 1);
  for (const m of md.matchAll(/^\s{0,3}\[[^\]\n]+\]:\s*\S+.*$/gm)) add(m.index, m.index + m[0].length);
  // HTML comments (the GDD's generated blocks are marked with them).
  for (const m of md.matchAll(/<!--[\s\S]*?-->/g)) add(m.index, m.index + m[0].length);
  return out;
}

/** The matches in a page's Markdown that are in its text, not its syntax. */
export function findInMarkdown(md, q, opts = {}) {
  const guard = protectedRanges(md);
  return findAll(md, q, opts).filter((m) => !guard.some(([s, e]) => m.start < e && m.end > s));
}

/** md with every text match replaced: { text, count }. */
export function replaceInMarkdown(md, q, replacement, opts = {}) {
  const hits = findInMarkdown(md, q, opts);
  let text = md;
  for (let i = hits.length - 1; i >= 0; i--) text = text.slice(0, hits[i].start) + replacement + text.slice(hits[i].end);
  return { text, count: hits.length };
}

/** The line a match sits on, for a list of results: { line (1-based), text, at } where at is its column. */
export function lineOf(md, start) {
  const from = md.lastIndexOf("\n", start - 1) + 1;
  const to = md.indexOf("\n", start);
  return { line: md.slice(0, from).split("\n").length, text: md.slice(from, to < 0 ? md.length : to), at: start - from };
}
