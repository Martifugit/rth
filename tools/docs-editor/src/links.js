// What links where, and the GDD's state: read from the pages' Markdown, for the Links tab, the notes'
// Related scope and the Board. Plain functions on text; server.js keeps the index fresh.

const LINK = /\[((?:[^\[\]]|\[[^\]]*\])*)\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;
const FENCE = /^\s*(`{3,}|~{3,})/;

const dirOf = (p) => (p.includes("/") ? p.slice(0, p.lastIndexOf("/")) : "");
function normalize(p) {
  const out = [];
  for (const part of p.split("/")) {
    if (part === "" || part === ".") continue;
    if (part === "..") out.pop(); else out.push(part);
  }
  return out.join("/");
}
// Inline code is an example, not a link: blanked out (same length, so positions hold) before looking for links.
const noCode = (line) => line.replace(/`[^`]*`/g, (m) => " ".repeat(m.length));
const isExternal = (href) => /^[a-z][a-z0-9+.-]*:/i.test(href);

/** A link's target page from `from` (null for external links, anchors in the same page, non-pages). */
export function targetOf(from, href) {
  if (!href || isExternal(href) || href.startsWith("#")) return null;
  let [p] = href.split("#");
  try { p = decodeURIComponent(p.split("?")[0]); } catch { /* not %-encoded after all: as written */ }
  if (!p) return null;
  const abs = p.startsWith("/") ? normalize(p) : normalize(dirOf(from) + "/" + p);
  if (abs.endsWith(".md")) return abs;
  return { dir: abs }; // a folder or a file that isn't a page: resolved against the page list later
}

/** Plain text of a Markdown line, for showing it and finding it in the rendered page. */
export function plain(md) {
  return md.replace(/!\[[^\]]*\]\([^)]*\)/g, "").replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/[*_`]+/g, "").replace(/\s+/g, " ").trim();
}

/** Each line's heading path, skipping code. Calls fn(line, index, path, inCode). */
function eachLine(text, fn) {
  const stack = [];
  let fence = null;
  text.split("\n").forEach((line, i) => {
    const f = line.match(FENCE);
    if (fence) { if (f && f[1][0] === fence[0] && f[1].length >= fence.length) fence = null; fn(line, i, stack.map((x) => x.text), true); return; }
    if (f) { fence = f[1]; fn(line, i, stack.map((x) => x.text), true); return; }
    const hd = line.match(/^(#{1,6})\s+(.*?)\s*#*\s*$/);
    if (hd) {
      const level = hd[1].length;
      while (stack.length && stack[stack.length - 1].level >= level) stack.pop();
      stack.push({ level, text: plain(hd[2]) });
    }
    fn(line, i, stack.map((x) => x.text), false);
  });
}

/** The links out of one page: [{ to, text, line, section, context }]. */
export function linksOf(page, text, files) {
  const out = [];
  eachLine(text, (line, i, section, code) => {
    if (code) return;
    LINK.lastIndex = 0;
    for (let m = LINK.exec(noCode(line)); m; m = LINK.exec(noCode(line))) {
      if (m.index > 0 && line[m.index - 1] === "!") continue; // an image
      let to = targetOf(page, m[2]);
      if (to && typeof to === "object") {
        to = files.has(to.dir + "/README.md") ? to.dir + "/README.md" : files.has(to.dir + "/index.md") ? to.dir + "/index.md" : null;
      }
      if (!to || to === page) continue;
      out.push({ to, text: plain(m[1]), line: i + 1, section: section.slice(1).join(" › ") || null, context: plain(line).slice(0, 200) });
    }
  });
  return out;
}

export function titleOf(text) {
  const m = text.match(/^#\s+(.+)$/m);
  return m ? plain(m[1]) : null;
}

/** The index over every page: { out: page -> links, in: page -> [{ from, ...link }], titles }. */
export function buildIndex(pages) {
  const files = new Set(pages.map((p) => p.path));
  const out = new Map(), inn = new Map(), titles = new Map(), texts = new Map();
  for (const { path, text } of pages) {
    texts.set(path, text);
    titles.set(path, titleOf(text));
    const links = linksOf(path, text, files);
    out.set(path, links);
    for (const l of links) {
      if (!inn.has(l.to)) inn.set(l.to, []);
      inn.get(l.to).push({ from: path, ...l });
    }
  }
  return { files, out, in: inn, titles, texts };
}

const GENERIC = new Set(["readme", "index", "overview", "contents", "notes", "sources", "summary", "template"]);
const escapeRe = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

/** Links for one page: linked from, links to (broken flagged), and plain-text mentions of its title. */
export function linksFor(index, page) {
  const inbound = (index.in.get(page) || []).map(({ from, line, section, context, text }) => ({ from, title: index.titles.get(from), line, section, context, text }));
  const outbound = (index.out.get(page) || []).map((l) => ({ ...l, title: index.titles.get(l.to) || null, broken: !index.files.has(l.to) && l.to.startsWith("docs/") }));
  const mentions = [];
  const title = index.titles.get(page);
  if (title && title.length >= 5 && !GENERIC.has(title.toLowerCase()) && !/^template/i.test(title)) {
    const re = new RegExp("(^|[^\\p{L}\\p{N}])" + escapeRe(title) + "(?![\\p{L}\\p{N}])", "u");
    const linked = new Set(inbound.map((x) => x.from));
    for (const [from, text] of index.texts) {
      if (from === page || linked.has(from)) continue;
      let first = null, count = 0;
      eachLine(text, (line, i, section, code) => {
        if (code || /^#{1,6}\s/.test(line)) return;
        const bare = plain(line);
        if (re.test(bare)) { count++; if (!first) first = { line: i + 1, section: section.slice(1).join(" › ") || null, context: bare.slice(0, 200) }; }
      });
      if (count) mentions.push({ from, title: index.titles.get(from), count, ...first });
    }
    mentions.sort((a, b) => b.count - a.count);
  }
  return { title, inbound, outbound, mentions: mentions.slice(0, 20) };
}

/** A GDD page's state: its status line and its open questions. */
export function pageState(text) {
  const lines = text.split("\n");
  let status = null, statusLine = null;
  const h1 = lines.findIndex((l) => /^#\s/.test(l));
  for (let i = h1 + 1; i < Math.min(lines.length, h1 + 6); i++) {
    const l = lines[i].trim();
    if (!l) continue;
    const m = l.match(/^[*_]\s*(Draft|Approved|Proposed|Stub|Retired)\b\s*(?:\(([^)]*)\))?/i);
    if (m) { status = m[1][0].toUpperCase() + m[1].slice(1).toLowerCase(); statusLine = { line: i + 1, date: m[2] || null, text: plain(l) }; }
    break;
  }
  const questions = [];
  let inOq = false, fence = null;
  lines.forEach((line, i) => {
    const f = line.match(FENCE);
    if (fence) { if (f && f[1][0] === fence[0]) fence = null; return; }
    if (f) { fence = f[1]; return; }
    if (/^#{1,6}\s/.test(line)) { inOq = /^##\s+open questions\s*$/i.test(line.trim()); return; }
    if (!inOq) return;
    const item = line.match(/^(\d{1,3}[.)]|[-*+])\s+(.*)$/);
    if (item) questions.push({ line: i + 1, text: plain(item[2]) });
    else if (questions.length && /^\s{2,}\S/.test(line)) questions[questions.length - 1].text += " " + plain(line);
  });
  return { status, statusLine, questions };
}

/** A heading's id in the page, as the Codex makes it (the same rule as GitHub's, near enough). */
export function slug(text) {
  return text.toLowerCase().trim().replace(/[^\p{L}\p{N}\s_-]/gu, "").replace(/\s/g, "-");
}

/** The ids of a page's headings: slugs, with -1, -2... after repeats, as the page's outline gives them. */
export function headingIds(text) {
  const ids = new Set(), seen = new Map();
  eachLine(text, (line, i, section, code) => {
    if (code) return;
    const hd = line.match(/^#{1,6}\s+(.*?)\s*#*\s*$/);
    if (!hd) return;
    let id = slug(plain(hd[1]));
    const n = seen.get(id) || 0;
    seen.set(id, n + 1);
    ids.add(n ? id + "-" + n : id);
  });
  return ids;
}

/**
 * Every link in the docs that goes nowhere: to a page or file that doesn't exist, or to a heading that
 * isn't on its page. [{ from, title, line, section, text, href, to, kind: "page" | "heading" }].
 * `exists(rel)`: is there a file or folder at this path (for links outside the indexed pages)?
 */
export function brokenLinks(index, exists = () => false) {
  const out = [], ids = new Map();
  const idsOf = (p) => { if (!ids.has(p)) ids.set(p, headingIds(index.texts.get(p))); return ids.get(p); };
  for (const [from, text] of index.texts) {
    eachLine(text, (line, i, section, code) => {
      if (code) return;
      LINK.lastIndex = 0;
      for (let m = LINK.exec(noCode(line)); m; m = LINK.exec(noCode(line))) {
        if (m.index > 0 && line[m.index - 1] === "!") continue; // an image
        const href = m[2];
        if (isExternal(href)) continue;
        const hash = href.includes("#") ? href.slice(href.indexOf("#") + 1) : "";
        let to = href.startsWith("#") ? from : targetOf(from, href);
        if (!to) continue;
        const report = (kind) => out.push({ from, title: index.titles.get(from), line: i + 1, section: section.slice(1).join(" › ") || null, text: plain(m[1]), href, to: typeof to === "string" ? to : to.dir, kind });
        if (typeof to === "object") {
          const page = [to.dir + "/README.md", to.dir + "/index.md"].find((p) => index.files.has(p));
          if (!page) { if (!exists(to.dir)) report("page"); continue; }
          to = page;
        }
        if (!index.files.has(to)) { if (!exists(to)) report("page"); continue; }
        if (!hash) continue;
        let id = hash;
        try { id = decodeURIComponent(hash); } catch { /* not %-encoded after all: as written */ }
        const have = idsOf(to);
        if (!have.has(id) && !have.has(slug(id))) report("heading");
      }
    });
  }
  return out;
}

/** The href from page `from` to `to` ("../items/axe.md"). */
export function hrefBetween(from, to) {
  const a = dirOf(from).split("/").filter(Boolean), b = to.split("/");
  let i = 0;
  while (i < a.length && i < b.length - 1 && a[i] === b[i]) i++;
  return "../".repeat(a.length - i) + b.slice(i).join("/");
}

/**
 * A page moved from `from` to `to` (its type changed): the text of `page` with its links put right.
 * Links to the moved page point to its new place; and when `page` is the moved page itself, its own
 * relative links (pages, images) are rewritten from its new folder. Code and other links stay as written.
 */
export function relink(text, page, from, to) {
  const self = page === from;
  const here = self ? to : page;
  const IN = /(!?\[(?:[^\[\]]|\[[^\]]*\])*\]\(\s*<?)([^)\s>]+)(>?(?:\s+"[^"]*")?\s*\))/g;
  let fence = null;
  return text.split("\n").map((line) => {
    const f = line.match(FENCE);
    if (fence) { if (f && f[1][0] === fence[0] && f[1].length >= fence.length) fence = null; return line; }
    if (f) { fence = f[1]; return line; }
    const blank = noCode(line);
    let out = "", last = 0;
    for (const m of blank.matchAll(IN)) {
      const href = m[2];
      if (isExternal(href) || href.startsWith("#") || href.startsWith("/")) continue;
      const [p, ...rest] = href.split("#");
      let target;
      try { target = normalize(dirOf(page) + "/" + decodeURIComponent(p)); } catch { continue; }
      const moved = target === from;
      if (!moved && !self) continue;
      const next = hrefBetween(here, moved ? to : target) + (p.endsWith("/") ? "/" : "") + (rest.length ? "#" + rest.join("#") : "");
      const at = m.index + m[1].length;
      out += line.slice(last, at) + next;
      last = at + href.length;
    }
    return out + line.slice(last);
  }).join("\n");
}
