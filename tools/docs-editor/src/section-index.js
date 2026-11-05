// A search index over the GDD's sections (PLAN: R'th AI's tools, step 5), so search_gdd finds the
// part of a page a question is about rather than lines that happen to hold its words. Each section (a
// page's text under one heading) is a document, ranked with BM25; the page's title, the section's
// heading path, and the page's summary and terms (written by Claude with index_page, kept in
// docs/.codex/index/) count extra, so a page is found by what it's about, not only by its wording.
//
// Plain functions over texts: refreshed page by page when a page's text changes (by hash), so it costs
// nothing to keep fresh on every search.

import crypto from "node:crypto";
import { split } from "./frontmatter.js";

const STOP = new Set("a an and are as at be but by for from has have he her his i if in into is it its of on or our she so than that the their them then there these they this to was we were what when where which who will with you your not no do does can".split(" "));
const K1 = 1.2, B = 0.75;
const FENCE = /^\s*(```|~~~)/;

export const textHash = (text) => crypto.createHash("sha1").update(String(text || "")).digest("hex").slice(0, 16);

/** Words for the index: lowercase letters and digits, without common words, plurals folded. */
export function terms(text) {
  const out = [];
  for (const m of String(text || "").toLowerCase().matchAll(/[\p{L}\p{N}][\p{L}\p{N}'’]*/gu)) {
    let w = m[0].replace(/['’]s$/, "").replace(/['’]/g, "");
    if (w.length < 2 || STOP.has(w)) continue;
    if (w.length > 4 && w.endsWith("ies")) w = w.slice(0, -3) + "y";
    else if (w.length > 3 && w.endsWith("s") && !w.endsWith("ss")) w = w.slice(0, -1);
    out.push(w);
  }
  return out;
}

/** A page's sections: [{ heading: ["Title", "Section", ...], line, text }], the title's part first. */
export function sectionsOf(text) {
  const all = String(text || "");
  const { body } = split(all);
  const head = all.slice(0, all.length - body.length);
  const offset = head ? head.split("\n").length - 1 : 0;
  const out = [];
  let title = null, path = [], cur = { heading: [], line: offset + 1, lines: [] }, fence = false;
  body.split("\n").forEach((line, i) => {
    if (FENCE.test(line)) { fence = !fence; cur.lines.push(line); return; }
    const hd = !fence && line.match(/^(#{1,6})\s+(.*?)\s*#*\s*$/);
    if (hd) {
      const level = hd[1].length, name = hd[2].replace(/[*_`]/g, "").trim();
      out.push(cur);
      if (level === 1) { title = name; path = []; } else { path = path.slice(0, level - 2); path[level - 2] = name; }
      cur = { heading: [title, ...path].filter(Boolean), line: offset + i + 1, lines: [] };
      return;
    }
    cur.lines.push(line);
  });
  out.push(cur);
  return out.map((s) => ({ heading: s.heading.length ? s.heading : [title].filter(Boolean), line: s.line, text: s.lines.join("\n").trim() })).filter((s) => s.text || s.heading.length > 1);
}

/** The index. summaryOf(path) → { summary, terms } or null (the shared summaries). */
export function makeSectionIndex({ summaryOf = () => null } = {}) {
  const pages = new Map(); // path → { hash, summaryKey, docs: [{ path, title, heading, line, text, tf, len }] }
  let df = new Map(), total = 0, dirty = true;

  function build(path, text) {
    const sum = summaryOf(path);
    const secs = sectionsOf(text);
    const title = secs[0]?.heading[0] || path.split("/").pop().replace(/\.md$/, "");
    const extra = [...terms(title), ...terms(title), ...terms(title), ...(sum ? [...terms(sum.summary), ...(sum.terms || []).flatMap((t) => [...terms(t), ...terms(t)])] : [])];
    const docs = secs.map((s) => {
      const words = [...terms(s.heading.slice(1).join(" ")), ...terms(s.heading.slice(1).join(" ")), ...terms(s.text), ...extra];
      const tf = new Map();
      for (const w of words) tf.set(w, (tf.get(w) || 0) + 1);
      return { path, title, heading: s.heading, line: s.line, text: s.text, tf, len: words.length };
    });
    return { hash: textHash(text), summaryKey: sum ? textHash(JSON.stringify(sum)) : null, docs };
  }

  /** Bring a page up to date (cheap when nothing changed). */
  function update(path, text) {
    if (text == null) { if (pages.delete(path)) dirty = true; return; }
    const have = pages.get(path);
    const sum = summaryOf(path);
    const sk = sum ? textHash(JSON.stringify(sum)) : null;
    if (have && have.hash === textHash(text) && have.summaryKey === sk) return;
    pages.set(path, build(path, text));
    dirty = true;
  }
  /** Keep only these pages (others were deleted). */
  function keep(paths) {
    const want = new Set(paths);
    for (const p of [...pages.keys()]) if (!want.has(p)) { pages.delete(p); dirty = true; }
  }
  function stats() {
    if (!dirty) return;
    df = new Map(); total = 0;
    for (const pg of pages.values()) for (const d of pg.docs) { total++; for (const w of d.tf.keys()) df.set(w, (df.get(w) || 0) + 1); }
    dirty = false;
  }

  /** The best sections for a query: [{ path, title, heading, line, score, snippet }]. */
  function search(query, { limit = 12 } = {}) {
    stats();
    const q = [...new Set(terms(query))];
    if (!q.length || !total) return [];
    let avg = 0;
    for (const pg of pages.values()) for (const d of pg.docs) avg += d.len;
    avg = avg / total || 1;
    const hits = [];
    for (const pg of pages.values()) for (const d of pg.docs) {
      let score = 0, matched = 0;
      for (const w of q) {
        const f = d.tf.get(w);
        if (!f) continue;
        matched++;
        const n = df.get(w) || 0;
        const idf = Math.log(1 + (total - n + 0.5) / (n + 0.5));
        score += idf * (f * (K1 + 1)) / (f + K1 * (1 - B + B * d.len / avg));
      }
      if (!score) continue;
      score *= 0.5 + matched / q.length; // sections with all the words come first
      hits.push({ d, score });
    }
    hits.sort((a, b) => b.score - a.score);
    return hits.slice(0, limit).map(({ d, score }) => ({ path: d.path, title: d.title, heading: d.heading, line: d.line, score: Math.round(score * 100) / 100, snippet: snippet(d.text, q) }));
  }

  return { update, keep, search, size: () => pages.size };
}

/** A few lines of a section, the ones holding the query's words first. */
function snippet(text, q) {
  const lines = String(text).split("\n").map((l) => l.trim()).filter(Boolean);
  const scored = lines.map((l, i) => ({ l, i, n: terms(l).filter((w) => q.includes(w)).length }));
  const best = scored.filter((x) => x.n).sort((a, b) => b.n - a.n || a.i - b.i).slice(0, 3).sort((a, b) => a.i - b.i).map((x) => x.l);
  const out = (best.length ? best : lines.slice(0, 2)).join(" … ");
  return out.length > 360 ? out.slice(0, 359).replace(/\s+\S*$/, "") + "…" : out;
}
