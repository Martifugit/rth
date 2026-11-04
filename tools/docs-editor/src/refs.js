// References between GDD pages. A page's ID is its file name (slugOf: "goblin" for entities/goblin.md),
// unique across the GDD. Pages refer to each other in three ways, and only these:
//
// - links, by path:                      [Goblin](../entities/goblin.md)
// - frontmatter fields that name pages:   groups: [goblins], based-on: goblin
// - query blocks that filter by a page:   groups: goblins, based-on: goblin, links-to: goblin
//
// Renaming or moving a page rewrites all three everywhere (rewriteRefs), and refsTo lists them, so no
// page is ever named by a string that could quietly go stale.

import { relink, linksOf } from "./links.js";
import { split, setField, slugOf } from "./frontmatter.js";

/** Frontmatter fields whose values are page IDs. */
export const PAGE_FIELDS = ["groups", "based-on"];
/** Query block filters whose values are page IDs ("this" means the page the block is on). */
export const QUERY_KEYS = ["groups", "based-on", "links-to"];

const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const QUERY_BLOCK = /(^[ \t]*```+[ \t]*query[^\n]*\n)([\s\S]*?)(^[ \t]*```+)/gm;

/**
 * The text of `page` after the page at `from` moved to `to` (a new folder, a new file name or both):
 * its links, frontmatter and query blocks point to the new place. When `page` is the moved page itself,
 * its own relative links are rewritten from its new folder too.
 */
export function rewriteRefs(text, page, from, to) {
  let out = relink(text, page, from, to);
  const a = slugOf(from), b = slugOf(to);
  if (a === b) return out;
  const parts = split(out);
  if (parts.data) {
    let front = parts.front;
    if (parts.data.groups.includes(a)) front = setField(front, "groups", parts.data.groups.map((g) => (g === a ? b : g)));
    if (parts.data["based-on"] === a) front = setField(front, "based-on", b);
    out = front + parts.body;
  }
  const line = new RegExp(`^([ \\t]*(?:${QUERY_KEYS.join("|")})[ \\t]*:[ \\t]*)${esc(a)}([ \\t]*\\r?)$`, "gm");
  return out.replace(QUERY_BLOCK, (m, open, body, close) => open + body.replace(line, `$1${b}$2`) + close);
}

/**
 * Every reference to the page at `path`, from `texts` (path → text): [{ from, kind, text }], kind
 * "link" (with the link's text), "group" (lists it under groups), "variant" (based-on), or "query".
 */
export function refsTo(texts, path) {
  const id = slugOf(path);
  const files = new Set(texts.keys());
  const out = [];
  const line = new RegExp(`^[ \\t]*(?:${QUERY_KEYS.join("|")})[ \\t]*:[ \\t]*${esc(id)}[ \\t]*\\r?$`, "m");
  for (const [from, text] of texts) {
    if (from === path) continue;
    for (const l of linksOf(from, text, files)) if (l.to === path) out.push({ from, kind: "link", text: l.text, line: l.line });
    const { data } = split(text);
    if (data?.groups?.includes(id)) out.push({ from, kind: "group" });
    if (data?.["based-on"] === id) out.push({ from, kind: "variant" });
    for (const m of text.matchAll(QUERY_BLOCK)) if (line.test(m[2])) { out.push({ from, kind: "query" }); break; }
  }
  return out;
}

/** A page's text with its title (the first "# " line) changed. */
export const retitle = (text, title) => text.replace(/^#[ \t]+.*?(\r?)$/m, (m, cr) => "# " + title + cr);

// ----------------------------------------------------------------------------- one reference at a time

const LINK_ON_LINE = /(!?)\[((?:[^\[\]]|\[[^\]]*\])*)\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;
const lineAt = (text, line, fn) => { const ls = text.split("\n"); const i = line - 1; if (i < 0 || i >= ls.length) return text; ls[i] = fn(ls[i]); return ls.join("\n"); };
const pathOf = (page, href) => {
  if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith("#")) return null;
  let p = href.split("#")[0];
  try { p = decodeURIComponent(p); } catch {}
  const out = [];
  for (const part of (page.slice(0, page.lastIndexOf("/")) + "/" + p).split("/")) { if (!part || part === ".") continue; if (part === "..") out.pop(); else out.push(part); }
  return out.join("/");
};

/** The links on line `line` of `page` that go to `from` point to `to` instead (the rest of the page is untouched). */
export function relinkLine(text, page, line, from, to) {
  return lineAt(text, line, (l) => relink(l, page, from, to));
}

/** The links on line `line` of `page` that go to `target` become their plain text. */
export function unlinkIn(text, page, line, target) {
  return lineAt(text, line, (l) => l.replace(LINK_ON_LINE, (m, bang, label, href) => (!bang && pathOf(page, href) === target ? label : m)));
}

/** The links on line `line` of `page` that go to `target` lose their "#heading". */
export function dropAnchor(text, page, line, target) {
  return lineAt(text, line, (l) => l.replace(LINK_ON_LINE, (m, bang, label, href) => (pathOf(page, href) === target && href.includes("#") ? m.replace(href, href.slice(0, href.indexOf("#")) || href) : m)));
}

/** `name`, written as plain text on line `line`, becomes a link to `href` (its first plain occurrence: never inside a link). */
export function linkPlain(text, line, name, href) {
  return lineAt(text, line, (l) => {
    const taken = [];
    for (const m of l.matchAll(LINK_ON_LINE)) taken.push([m.index, m.index + m[0].length]);
    const re = new RegExp(`(^|[^\\p{L}\\p{N}])(${esc(name)})(?![\\p{L}\\p{N}])`, "gu");
    for (const m of l.matchAll(re)) {
      const at = m.index + m[1].length;
      if (taken.some(([a, b]) => at >= a && at < b)) continue;
      return l.slice(0, at) + `[${name}](${href})` + l.slice(at + name.length);
    }
    return l;
  });
}

/** A placeholder name (p-something) given its real name everywhere in the text, outside code and link targets. */
export function nameToken(text, token, name) {
  const re = new RegExp(`(?<![\\w-])${esc(token)}(?![\\w-])`, "g");
  let fence = null;
  return text.split("\n").map((l) => {
    const f = l.match(/^\s*(`{3,}|~{3,})/);
    if (fence) { if (f && f[1][0] === fence[0]) fence = null; return l; }
    if (f) { fence = f[1]; return l; }
    // Only the text: inline code and the (target) of a link stay as they are.
    return l.split(/(`[^`]*`|\]\([^)]*\))/).map((part, i) => (i % 2 ? part : part.replace(re, name))).join("");
  }).join("\n");
}
