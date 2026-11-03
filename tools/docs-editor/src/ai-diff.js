// Finding what changed (PLAN.md, "AI, built in": changes are found, not trusted). Used by the engine,
// to say how many places a turn changed, and by the page, to draw Show changes.
//
//   diffSeq(a, b, same)  the longest common subsequence of two lists, as hunks:
//                        [{ a: [i, j), b: [k, l) }] for each run that differs
//   diffWords(a, b)      a word-level diff of two texts: [{ op: "=", "+", "-", text }]
//   blocks(md)           a Markdown page split into its blocks (paragraphs, lists, tables, headings)

/** Hunks where two sequences differ, by the longest common subsequence. */
export function diffSeq(a, b, same = (x, y) => x === y) {
  const n = a.length, m = b.length;
  // Trim what's the same at both ends first: most edits touch little of a page.
  let s = 0;
  while (s < n && s < m && same(a[s], b[s])) s++;
  let e = 0;
  while (e < n - s && e < m - s && same(a[n - 1 - e], b[m - 1 - e])) e++;
  const A = a.slice(s, n - e), B = b.slice(s, m - e);
  const L = Array.from({ length: A.length + 1 }, () => new Uint32Array(B.length + 1));
  for (let i = A.length - 1; i >= 0; i--)
    for (let j = B.length - 1; j >= 0; j--)
      L[i][j] = same(A[i], B[j]) ? L[i + 1][j + 1] + 1 : Math.max(L[i + 1][j], L[i][j + 1]);
  const hunks = [];
  let i = 0, j = 0, open = null;
  const close = () => { if (open) { open.a[1] = s + i; open.b[1] = s + j; hunks.push(open); open = null; } };
  while (i < A.length || j < B.length) {
    if (i < A.length && j < B.length && same(A[i], B[j])) { close(); i++; j++; continue; }
    if (!open) open = { a: [s + i, 0], b: [s + j, 0] };
    if (j < B.length && (i >= A.length || L[i][j + 1] >= L[i + 1][j])) j++;
    else i++;
  }
  close();
  return hunks;
}

/** Words and the spaces between them, so joining the pieces gives the text back exactly. */
const tokens = (t) => t.match(/\s+|[\p{L}\p{N}'’_-]+|[^\s\p{L}\p{N}]/gu) || [];

export function diffWords(a, b) {
  const A = tokens(a), B = tokens(b);
  const out = [];
  const push = (op, text) => { if (!text) return; const last = out[out.length - 1]; if (last && last.op === op) last.text += text; else out.push({ op, text }); };
  let at = 0, bt = 0;
  for (const hk of diffSeq(A, B)) {
    push("=", A.slice(at, hk.a[0]).join(""));
    push("-", A.slice(hk.a[0], hk.a[1]).join(""));
    push("+", B.slice(hk.b[0], hk.b[1]).join(""));
    at = hk.a[1]; bt = hk.b[1];
  }
  push("=", A.slice(at).join(""));
  return tidy(out);
}

/**
 * Make a word diff readable, as editors' track changes do: a short stretch left the same between two
 * changes (a space, one small word) joins them, and each run of changes reads as what was removed,
 * then what replaced it, instead of words alternating.
 */
function tidy(parts) {
  const out = [];
  for (let i = 0; i < parts.length; i++) {
    const p = parts[i];
    const between = p.op === "=" && i > 0 && i < parts.length - 1 && /^\s*\S{0,3}\s*$/.test(p.text);
    if (between) { out.push({ op: "-", text: p.text }, { op: "+", text: p.text }); continue; }
    out.push({ ...p });
  }
  // Gather each run of changes into one removal followed by one addition.
  const res = [];
  let del = "", add = "";
  const flush = () => { if (del) res.push({ op: "-", text: del }); if (add) res.push({ op: "+", text: add }); del = add = ""; };
  for (const p of out) {
    if (p.op === "=") { flush(); res.push(p); }
    else if (p.op === "-") del += p.text;
    else add += p.text;
  }
  flush();
  return res;
}

/** How much of two texts' word diff is the same (0 to 1): little, and the change reads as a rewrite. */
export function sameShare(parts) {
  let same = 0, all = 0;
  for (const p of parts) { all += p.text.length; if (p.op === "=") same += p.text.length; }
  return all ? same / all : 1;
}

/** A Markdown page's blocks: runs of lines between blank lines (a fenced code block is one block). */
export function blocks(md) {
  const out = [];
  let cur = [], fence = false;
  for (const line of md.replace(/\r\n/g, "\n").split("\n")) {
    if (/^\s*(```|~~~)/.test(line)) fence = !fence;
    if (!fence && !line.trim()) { if (cur.length) { out.push(cur.join("\n")); cur = []; } continue; }
    cur.push(line);
  }
  if (cur.length) out.push(cur.join("\n"));
  return out;
}

/** How many places two versions of a page differ (hunks of blocks). */
export const places = (before, after) => diffSeq(blocks(before), blocks(after)).length;
