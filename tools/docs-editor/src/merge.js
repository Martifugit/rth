// Saving a WYSIWYG edit without rewriting the whole file.
//
// The editor turns Markdown into a document and back, and its Markdown is never quite the
// author's: paragraphs come out on one line, tables lose their spacing, and so on. Saving that
// would turn every save into a diff of the whole file. So we only write back the blocks that
// changed:
//
//   source    the text on disk (or the draft the editor was loaded from)
//   baseline  the editor's own Markdown right after loading `source`
//   current   the editor's Markdown now
//
// Top-level blocks of baseline and current are compared as strings (both come from the same
// serializer). A block that is still the same is written as the source's original text, byte for
// byte; a new or changed block is written as the editor's Markdown, wrapped to the docs' width.

import MarkdownIt from "markdown-it";

const md = new MarkdownIt({ html: false }); // as the editor reads it: the docs hold no raw HTML, and "<Name>" is text

export const WRAP = 100;

/** Top-level blocks of a Markdown text: their line range and their exact text (trimmed). */
export function splitBlocks(src) {
  const tokens = md.parse(src, {});
  const lines = src.split("\n");
  const offsets = [0];
  for (const line of lines) offsets.push(offsets[offsets.length - 1] + line.length + 1);
  const blocks = [];
  for (const t of tokens) {
    if (t.level !== 0 || !t.map || t.nesting === -1) continue;
    const [start, end] = t.map;
    const from = offsets[start];
    const text = src.slice(from, Math.min(offsets[end], src.length)).replace(/\s+$/, "");
    blocks.push({ start, end, from, to: from + text.length, text });
  }
  return blocks;
}

/** Longest common subsequence of two string arrays, as matched index pairs. */
function lcs(a, b) {
  const n = a.length, m = b.length;
  // Trim the common head and tail first: an edit usually touches a few blocks in the middle.
  let head = 0;
  while (head < n && head < m && a[head] === b[head]) head++;
  let tail = 0;
  while (tail < n - head && tail < m - head && a[n - 1 - tail] === b[m - 1 - tail]) tail++;
  const pairs = [];
  for (let i = 0; i < head; i++) pairs.push([i, i]);
  const A = a.slice(head, n - tail), B = b.slice(head, m - tail);
  const N = A.length, M = B.length;
  const dp = Array.from({ length: N + 1 }, () => new Uint32Array(M + 1));
  for (let i = N - 1; i >= 0; i--)
    for (let j = M - 1; j >= 0; j--)
      dp[i][j] = A[i] === B[j] ? dp[i + 1][j + 1] + 1 : Math.max(dp[i + 1][j], dp[i][j + 1]);
  let i = 0, j = 0;
  while (i < N && j < M) {
    if (A[i] === B[j]) { pairs.push([head + i, head + j]); i++; j++; }
    else if (dp[i + 1][j] >= dp[i][j + 1]) i++;
    else j++;
  }
  for (let k = 0; k < tail; k++) pairs.push([n - tail + k, m - tail + k]);
  return pairs;
}

const norm = (s) => s.replace(/\s+/g, " ").trim();

/** Merge the editor's Markdown back into the source, keeping untouched blocks as written. */
export function mergeEdit(source, baseline, current) {
  if (current === baseline) return source;
  const orig = splitBlocks(source);
  const base = splitBlocks(baseline);
  const cur = splitBlocks(current);

  // Which source block each baseline block stands for. Usually the counts match one to one; if
  // the editor merged or split something on loading, pair them by what they render to instead.
  let baseToOrig;
  if (base.length === orig.length) {
    baseToOrig = base.map((_, i) => i);
  } else {
    baseToOrig = new Array(base.length).fill(-1);
    const key = (b) => norm(md.render(b.text));
    for (const [bi, oi] of lcs(base.map(key), orig.map(key))) baseToOrig[bi] = oi;
  }

  const curToOrig = new Array(cur.length).fill(-1);
  const curFromBase = new Array(cur.length).fill(-1); // a changed block: the one it was
  const pairs = lcs(base.map((b) => b.text), cur.map((b) => b.text));
  for (const [bi, ci] of pairs) curToOrig[ci] = baseToOrig[bi];
  // Between two unchanged blocks, as many blocks before as after: those were edited in place.
  let lastB = -1, lastC = -1;
  for (const [bi, ci] of [...pairs, [base.length, cur.length]]) {
    if (bi - lastB === ci - lastC) for (let k = 1; k < bi - lastB; k++) curFromBase[lastC + k] = lastB + k;
    lastB = bi; lastC = ci;
  }

  let out = orig.length ? source.slice(0, orig[0].from) : "";
  let prev = -2;
  cur.forEach((block, ci) => {
    const oi = curToOrig[ci];
    if (ci > 0) out += oi >= 0 && oi === prev + 1 ? source.slice(orig[prev].to, orig[oi].from) : "\n\n";
    const was = curFromBase[ci] >= 0 ? baseToOrig[curFromBase[ci]] : -1;
    out += oi >= 0 ? orig[oi].text
      : was >= 0 ? mergeInside(orig[was].text, base[curFromBase[ci]].text, block.text)
      : wrapBlock(block.text);
    prev = oi >= 0 ? oi : -2;
  });
  const trailing = source.match(/\s*$/)[0];
  return out + (trailing.includes("\n") || source === "" ? "\n" : "");
}

// An edited table or list: keep the rows or items that didn't change as they were written.
// An item starts at the list's own indent: a long numbered list pads " 9." to line up with "10.",
// and a nested list sits further in.
const LIST_ITEM = /^( {0,3})(?:[-+*]|\d{1,9}[.)])(?: |$)/;
function listItems(text) {
  const items = [];
  let indent = null;
  for (const line of text.split("\n")) {
    const m = line.match(LIST_ITEM);
    if (indent === null && m) indent = m[1].length;
    if ((m && m[1].length <= indent + 1) || !items.length) items.push([line]);
    else items[items.length - 1].push(line);
  }
  return items.map((lines) => lines.join("\n").replace(/\s+$/, ""));
}
function mergeParts(o, b, c, wrap) {
  if (o.length !== b.length) return null;
  const map = new Array(c.length).fill(-1);
  for (const [bi, ci] of lcs(b, c)) map[ci] = bi;
  return c.map((part, ci) => (map[ci] >= 0 ? o[map[ci]] : wrap(part)));
}
function mergeInside(o, b, c) {
  const isTable = (t) => t.startsWith("|");
  if (isTable(o) && isTable(b) && isTable(c)) {
    const rows = mergeParts(o.split("\n"), b.split("\n"), c.split("\n"), (r) => r);
    if (rows) return rows.join("\n");
  }
  const kind = (t) => (t.match(/^ {0,3}([-+*]|\d{1,9}[.)])(?: |$)/) || [])[1]?.replace(/\d+/, "1");
  if (kind(o) && kind(o) === kind(b) && kind(b) === kind(c)) {
    const items = mergeParts(listItems(o), listItems(b), listItems(c), (item) => wrapBlock(/^\S/.test(o) ? item.replace(/^ +/, "") : item));
    if (items) return items.join(/\n\s*\n(?:[-+*]|\d{1,9}[.)]) /.test(o) ? "\n\n" : "\n");
  }
  return wrapBlock(c);
}

// ----------------------------------------------------------------------------- wrapping

// A word that would start a block of its own if it began a line (a list marker, a heading, a
// quote, a table row): never break a line just before one.
const BLOCK_START = /^(?:[-+*]|\d{1,9}[.)]|#{1,6}|>|\||=+|-{2,}|`{3,}|~{3,})$|^(?:#{1,6}|>|\|)/;

/** Wrap a block of Markdown to `width`, leaving code, tables and headings alone. */
export function wrapBlock(text, width = WRAP) {
  const out = [];
  let fence = null;
  for (const line of text.split("\n")) {
    const f = line.match(/^\s*(?:>\s?)*\s*(`{3,}|~{3,})/);
    if (fence) {
      out.push(line);
      if (f && f[1][0] === fence[0] && f[1].length >= fence.length) fence = null;
      continue;
    }
    if (f) { fence = f[1]; out.push(line); continue; }
    if (line.length <= width || /^\s*(?:>\s?)*\s*(?:#|\|)/.test(line) || /^ {4,}/.test(line)) {
      out.push(line);
      continue;
    }
    out.push(...wrapLine(line, width));
  }
  return out.join("\n");
}

function wrapLine(line, width) {
  const quote = line.match(/^\s*(?:>\s?)*/)[0];
  let rest = line.slice(quote.length);
  const marker = rest.match(/^\s*(?:(?:[-+*]|\d{1,9}[.)])\s+(?:\[[ xX]\]\s+)?)?/)[0];
  rest = rest.slice(marker.length);
  const first = quote + marker;
  const cont = quote.replace(/\s+$/, "") + (quote ? " " : "") + " ".repeat(marker.length);
  const hardBreak = rest.endsWith("\\") || / {2,}$/.test(rest);
  const words = rest.split(/ +/).filter((w) => w !== "");
  if (words.length === 0) return [line];

  const lines = [];
  let cur = first + words[0];
  for (let i = 1; i < words.length; i++) {
    const w = words[i];
    if (cur.length + 1 + w.length > width && !BLOCK_START.test(w) && cur.trim() !== marker.trim()) {
      lines.push(cur);
      cur = cont + w;
    } else {
      cur += " " + w;
    }
  }
  lines.push(cur + (hardBreak && !cur.endsWith("\\") ? "  " : ""));
  return lines;
}

/** Markdown to HTML, as the checks compare it. */
export const renderHtml = (src) => md.render(src);
