// Paths between pages, as the docs' links write them: relative to the page's folder, "/" from the
// repository's root. Shared by the page (src/app.js) and the link index (src/links.js).

export const dirOf = (p) => (p.includes("/") ? p.slice(0, p.lastIndexOf("/")) : "");

/** "a/./b/../c" → "a/c". */
export function normalize(p) {
  const out = [];
  for (const part of p.split("/")) {
    if (part === "" || part === ".") continue;
    if (part === "..") out.pop(); else out.push(part);
  }
  return out.join("/");
}

/** A web address or mailto: rather than a path. */
export const isExternal = (href) => /^[a-z][a-z0-9+.-]*:/i.test(href);

/** Where a link on page `from` goes: { path, hash, dir } (dir: it ends in "/"). */
export function resolveHref(from, href) {
  let [p, hash] = href.split("#");
  try { p = decodeURIComponent(p.split("?")[0]); } catch { p = p.split("?")[0]; /* not %-encoded after all */ }
  if (!p) return { path: from, hash: hash || "" };
  const abs = p.startsWith("/") ? normalize(p) : normalize(dirOf(from) + "/" + p);
  return { path: abs, hash: hash || "", dir: p.endsWith("/") };
}

/** The href from page `from` to `to` ("../items/axe.md"). */
export function relativePath(from, to) {
  const a = dirOf(from).split("/").filter(Boolean), b = to.split("/");
  let i = 0;
  while (i < a.length && i < b.length - 1 && a[i] === b[i]) i++;
  return "../".repeat(a.length - i) + b.slice(i).join("/");
}
