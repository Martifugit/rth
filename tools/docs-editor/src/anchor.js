// Where a note belongs in a page, kept out of the page itself.
//
// An anchor is what the note was made on: the quoted passage, a little text either side of it, and
// the path of headings it sat under (the W3C Web Annotation "text quote" idea plus a fallback).
// Pages change under notes (an agent, git, the owner), so placing a note again is a search:
//
//   1. the exact quote; if it appears more than once, the copy whose surroundings and section match
//   2. a fuzzy match inside the remembered section (the window of words most like the quote)
//   3. the section's heading (the passage is gone, the section isn't)
//   4. lost: the panel lists it under "Lost their place" to re-attach
//
// Plain functions on plain text, so the server, the CLI, the page and the tests share them. The
// page passes the editor's text (blocks joined by "\n") and its sections in that text's offsets.

export const CONTEXT = 32;
const WORD = /[\p{L}\p{N}'’]+/gu;

/** An anchor for text[start, end) inside `sections` (from sectionsOf). */
export function capture(text, start, end, sections) {
  const raw = text.slice(start, end);
  const lead = raw.length - raw.trimStart().length;
  start += lead; end = start + raw.trim().length;
  const sec = sectionAt(sections, start);
  return {
    section: sec ? sec.path : [],
    quote: text.slice(start, end),
    prefix: text.slice(Math.max(0, start - CONTEXT), start),
    suffix: text.slice(end, end + CONTEXT),
  };
}


/**
 * Sections of a text from its headings: [{ path: [h1, h2...], level, start, end, headingEnd }].
 * `headings` is [{ level, text, start }] in text order (start: where the heading's text begins).
 */
export function sectionsOf(headings, textLength) {
  const out = [];
  const stack = [];
  headings.forEach((hd, i) => {
    while (stack.length && stack[stack.length - 1].level >= hd.level) stack.pop();
    stack.push(hd);
    let end = textLength;
    for (let j = i + 1; j < headings.length; j++) if (headings[j].level <= hd.level) { end = headings[j].start; break; }
    out.push({ path: stack.map((x) => x.text), level: hd.level, start: hd.start, end, headingEnd: hd.start + hd.text.length });
  });
  return out;
}

/** The innermost section containing offset `at`. */
export function sectionAt(sections, at) {
  let best = null;
  for (const s of sections) if (s.start <= at && at < Math.max(s.end, s.start + 1) && (!best || s.level > best.level)) best = s;
  return best;
}

const samePath = (a, b) => a.length === b.length && a.every((x, i) => x === b[i]);

/** The section an anchor names: the same path, else the deepest one with the same last heading. */
export function findSection(sections, path) {
  if (!path || !path.length) return null;
  return sections.find((s) => samePath(s.path, path))
    || sections.filter((s) => s.path[s.path.length - 1] === path[path.length - 1]).sort((a, b) => b.level - a.level)[0]
    || null;
}

function commonTail(a, b) { let n = 0; while (n < a.length && n < b.length && a[a.length - 1 - n] === b[b.length - 1 - n]) n++; return n; }
function commonHead(a, b) { let n = 0; while (n < a.length && n < b.length && a[n] === b[n]) n++; return n; }

function words(text, from = 0, to = text.length) {
  const out = [];
  WORD.lastIndex = 0;
  const slice = text.slice(from, to);
  for (let m = WORD.exec(slice); m; m = WORD.exec(slice)) out.push({ w: m[0].toLowerCase(), start: from + m.index, end: from + m.index + m[0].length });
  return out;
}

/** How alike two bags of words are, 0..1 (Dice). */
function likeness(qCounts, qLen, window) {
  const seen = new Map();
  let common = 0;
  for (const x of window) {
    const n = (seen.get(x.w) || 0) + 1;
    seen.set(x.w, n);
    if (n <= (qCounts.get(x.w) || 0)) common++;
  }
  return (2 * common) / (qLen + window.length);
}

export const FUZZY_THRESHOLD = 0.6;

/**
 * Place an anchor in a text. Returns { start, end, how } where how is "exact", "fuzzy", "section"
 * (start/end: the section's heading) or "page" (an anchor with neither), or { how: "lost" }.
 */
export function locate(text, sections, anchor) {
  if (!anchor) return { how: "page", start: 0, end: 0 };
  const sec = findSection(sections, anchor.section);
  const quote = anchor.quote || "";
  if (!quote) {
    if (sec) return { how: "section", start: sec.start, end: sec.headingEnd };
    return anchor.section && anchor.section.length ? { how: "lost" } : { how: "page", start: 0, end: 0 };
  }

  // 1. Exact.
  const hits = [];
  for (let i = text.indexOf(quote); i >= 0; i = text.indexOf(quote, i + 1)) hits.push(i);
  if (hits.length === 1) return { how: "exact", start: hits[0], end: hits[0] + quote.length };
  if (hits.length > 1) {
    let best = hits[0], bestScore = -1;
    for (const i of hits) {
      let score = commonTail(text.slice(Math.max(0, i - CONTEXT), i), anchor.prefix || "")
        + commonHead(text.slice(i + quote.length, i + quote.length + CONTEXT), anchor.suffix || "");
      if (sec && sec.start <= i && i < sec.end) score += CONTEXT;
      if (score > bestScore) { best = i; bestScore = score; }
    }
    return { how: "exact", start: best, end: best + quote.length };
  }

  // 2. Fuzzy, in the section if it still exists (else the whole page).
  const q = words(quote);
  if (q.length) {
    const qCounts = new Map();
    for (const x of q) qCounts.set(x.w, (qCounts.get(x.w) || 0) + 1);
    const pool = sec ? words(text, sec.start, sec.end) : words(text);
    let best = null;
    const sizes = new Set([q.length, Math.max(1, q.length - 1), q.length + 1, Math.max(1, Math.round(q.length * 0.8)), Math.round(q.length * 1.25)]);
    for (const n of sizes) {
      if (n > pool.length) continue;
      for (let i = 0; i + n <= pool.length; i++) {
        const win = pool.slice(i, i + n);
        const s = likeness(qCounts, q.length, win);
        if (!best || s > best.s) best = { s, start: win[0].start, end: win[n - 1].end };
      }
    }
    // A one- or two-word quote must match exactly: fuzzy would land on any common word.
    if (best && best.s >= FUZZY_THRESHOLD && q.length >= 3) return { how: "fuzzy", start: best.start, end: best.end, score: best.s };
  }

  // 3. The section, 4. lost.
  if (sec) return { how: "section", start: sec.start, end: sec.headingEnd };
  return { how: "lost" };
}
