// The GDD itself: where it is, its top pages, and its system read from How the GDD Works. The types,
// the components and who writes what are defined once, in that page's tables; the Codex reads them
// from it, so changing the system is an edit to the page, never to code.

export const GDD = "docs/gdd";
export const CHARTER = GDD + "/charter.md";
export const SYSTEM = GDD + "/how-the-gdd-works.md";
/** The pages at the top of the page list, in this order, before the type folders. */
export const TOP = [CHARTER, SYSTEM, GDD + "/using-codex.md"];

export const inGdd = (rel) => typeof rel === "string" && rel.startsWith(GDD + "/");
/** Pages about the GDD rather than in it: the top pages, and anything starting with "_". No type needed. */
export const isMeta = (rel) => TOP.includes(rel) || /\/_[^/]*$/.test(rel);

const cells = (l) => l.trim().replace(/^\||\|$/g, "").split("|").map((c) => c.trim());

/** Every table in the part of the page under the first heading matching `heading` (up to the next heading of its level or higher): [{ under, rows }]. */
function tablesIn(text, heading) {
  const lines = (text || "").replace(/\r\n/g, "\n").split("\n");
  const start = lines.findIndex((l) => /^#{1,6}\s/.test(l) && heading.test(l.replace(/^#+\s*/, "")));
  if (start < 0) return [];
  const level = lines[start].match(/^#+/)[0].length;
  const out = [];
  let under = null;
  for (let i = start + 1; i < lines.length; i++) {
    const hd = lines[i].match(/^(#{1,6})\s+(.*)$/);
    if (hd && hd[1].length <= level) break;
    if (hd) { under = hd[2].trim(); continue; }
    if (!/^\s*\|/.test(lines[i])) continue;
    const head = cells(lines[i]).map((c) => c.toLowerCase());
    const rows = [];
    let j = i + 2;
    for (; j < lines.length && /^\s*\|/.test(lines[j]); j++) {
      const c = cells(lines[j]);
      rows.push(Object.fromEntries(head.map((k, n) => [k, c[n] || ""])));
    }
    i = j - 1;
    out.push({ under, rows });
  }
  return out;
}

/** "area-rules" → "Area rules": how a component's section is headed on a page. */
export const headingOf = (name) => name.replace(/-/g, " ").replace(/^./, (c) => c.toUpperCase());
/** A heading as a name to compare: "Area rules", "area rules" and "area-rules" are the same. */
export const nameOf = (heading) => String(heading || "").toLowerCase().trim().replace(/[\s_]+/g, "-");

/**
 * The types, from How the GDD Works' "Types" table: [{ type, folder, covers, examples, headings }].
 * `examples` is what follows the colon in "What it covers" (or all of it), for "entity | monsters, NPCs, pets".
 */
export function readTypes(text) {
  return tablesIn(text, /\btypes\b/i).flatMap((t) => t.rows)
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

/**
 * The components, from How the GDD Works' "Components" tables: [{ name, group, answers, fieldsText, fields, foundOn, generated }].
 * `fields` is the field names when "Fields" is a plain list of them ("Verb, skill (optional), level…"), else [].
 */
export function readComponents(text) {
  const out = [];
  for (const { under, rows } of tablesIn(text, /\bcomponents\b/i)) {
    for (const r of rows) {
      if (!r.component) continue;
      const fieldsText = r.fields || "";
      const parts = fieldsText.split(",").map((x) => x.replace(/\(.*?\)/g, "").trim()).filter(Boolean);
      const plainList = parts.length > 1 && parts.every((x) => x.split(/\s+/).length <= 4);
      out.push({
        name: r.component.toLowerCase(), group: under, answers: r.answers || "", fieldsText,
        fields: plainList ? parts.map((x) => x.replace(/^./, (c) => c.toUpperCase())) : [],
        foundOn: (r["found on"] || "").toLowerCase().split(",").map((x) => x.trim()).filter(Boolean),
        generated: /generated/i.test(fieldsText),
      });
    }
  }
  return out;
}

/**
 * "Who writes what": [{ question, source, label }]. `source` is the component or core heading names its
 * "Described on" mentions (rewards, spawns, steps…); `label` is how a link from such a section shows on the
 * page it points to ("Dropped by", "Found in").
 */
export function readOwners(text, names) {
  return tablesIn(text, /who writes what/i).flatMap((t) => t.rows).map((r) => {
    const on = (r["described on"] || "").toLowerCase();
    const source = names.filter((n) => new RegExp("(^|[^a-z])" + n.replace(/-/g, "[- ]") + "s?([^a-z]|$)").test(on));
    const q = (r["shown elsewhere as"] || "").match(/"([^"]+)"|“([^”]+)”/);
    return { question: r.question || "", source, label: q ? q[1] || q[2] : null };
  }).filter((o) => o.source.length && o.label);
}

/** The whole system from How the GDD Works: { types, components, owners }. */
export function readSystem(text) {
  const types = readTypes(text), components = readComponents(text);
  // Sections (components, core headings) and types: "The combat, consumable or ability doing it".
  const names = [...new Set([...components.map((c) => c.name), ...types.flatMap((t) => t.headings.map(nameOf)), ...types.map((t) => t.type)])];
  return { types, components, owners: readOwners(text, names) };
}

/** A component's standard section: its heading, and a line per field when its fields are a plain list. */
export function componentSection(c) {
  return "## " + headingOf(c.name) + "\n\n" + (c.fields.length && !c.generated ? c.fields.map((f) => "- " + f + ":").join("\n") + "\n" : "");
}

/**
 * A new page of a type: its frontmatter (the type, status stub), its title and the type's core headings.
 * Without a type: just the title. `make` is frontmatter.js's.
 */
export function newPageText(title, type, make = null) {
  const front = type && make ? make({ type: type.type, components: [], groups: [], "based-on": null, status: "stub" }) : "";
  // "A group's Members section is generated from every page that lists it under groups."
  const generated = (hd) => (type?.type === "group" && /^members$/i.test(hd) ? "\n```query\ngroups: this\n```\n" : "");
  return front + "# " + title + "\n" + (type?.headings || []).map((hd) => "\n## " + hd + "\n" + generated(hd)).join("");
}
