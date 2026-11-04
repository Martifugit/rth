// The GDD itself: where it is, its top pages, and its system read from How the GDD Works. The types
// (and, later, the components) are defined once, in that page's tables; the Codex reads them from it,
// so changing the system is an edit to the page, never to code.

export const GDD = "docs/gdd";
export const CHARTER = GDD + "/charter.md";
export const SYSTEM = GDD + "/how-the-gdd-works.md";
/** The pages at the top of the page list, in this order, before the type folders. */
export const TOP = [CHARTER, SYSTEM, GDD + "/using-codex.md"];

export const inGdd = (rel) => typeof rel === "string" && rel.startsWith(GDD + "/");

/** The rows of the first Markdown table under a heading matching `heading`, as objects keyed by its header row (lower case). */
function tableUnder(text, heading) {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  let i = lines.findIndex((l) => /^#{1,6}\s/.test(l) && heading.test(l.replace(/^#+\s*/, "")));
  if (i < 0) return [];
  for (i++; i < lines.length && !/^\s*\|/.test(lines[i]); i++) if (/^#{1,6}\s/.test(lines[i])) return [];
  const cells = (l) => l.trim().replace(/^\||\|$/g, "").split("|").map((c) => c.trim());
  const head = cells(lines[i] || "").map((c) => c.toLowerCase());
  const rows = [];
  for (i += 2; i < lines.length && /^\s*\|/.test(lines[i]); i++) {
    const c = cells(lines[i]);
    rows.push(Object.fromEntries(head.map((k, j) => [k, c[j] || ""])));
  }
  return rows;
}

/**
 * The types, from How the GDD Works' "Types" table: [{ type, folder, covers, examples, headings }].
 * `examples` is what follows the colon in "What it covers" (or all of it), for "entity | monsters, NPCs, pets".
 */
export function readTypes(text) {
  if (!text) return [];
  return tableUnder(text, /\btypes\b/i)
    .filter((r) => r.type && r.folder)
    .map((r) => {
      const covers = r["what it covers"] || "";
      return {
        type: r.type.toLowerCase(),
        folder: r.folder.replace(/^\/+|\/+$/g, ""),
        covers,
        examples: covers.includes(":") ? covers.slice(covers.indexOf(":") + 1).trim() : covers,
        headings: (r["core headings"] || "").split("·").map((x) => x.trim()).filter(Boolean),
      };
    });
}

/** A new page of a type: its title and the type's core headings. */
export function newPageText(title, type) {
  return "# " + title + "\n" + (type?.headings || []).map((hd) => "\n## " + hd + "\n").join("");
}
