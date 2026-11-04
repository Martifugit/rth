// The GDD itself: where it is, its top pages, and its system read from How the GDD Works. The types,
// the components and who writes what are defined once, in that page's tables; the Codex reads them
// from it, so changing the system is an edit to the page, never to code.

import { split, setField } from "./frontmatter.js";

export const GDD = "docs/gdd";
export const README = GDD + "/README.md";
export const CHARTER = GDD + "/charter.md";
export const SYSTEM = GDD + "/how-the-gdd-works.md";
/** The pages at the top of the page list, in this order: the start page and the pages about the GDD. */
/** The game page: about the game itself, so not a page of a type. */
export const OVERVIEW = GDD + "/rth-overview.md";
export const TOP = [README, CHARTER, SYSTEM, GDD + "/writing.md", GDD + "/using-codex.md", GDD + "/workflow.md", OVERVIEW];
/** One template per type (templates/entity.md) and one snippet per component (templates/action.md). */
export const TEMPLATES = GDD + "/templates";
export const DECISIONS_LOG = GDD + "/decisions/decisions-log.md";

/** Pages the Codex never deletes: the top pages and the templates (types and components depend on them). */
export const undeletable = (rel) => TOP.includes(rel) || String(rel).startsWith(TEMPLATES + "/");
export const inGdd = (rel) => typeof rel === "string" && rel.startsWith(GDD + "/");
/** Pages about the GDD rather than in it: the top pages, the templates, the decisions log, anything starting with "_". No type needed. */
export const isMeta = (rel) => TOP.includes(rel) || rel === DECISIONS_LOG || rel.startsWith(TEMPLATES + "/") || /\/_[^/]*$/.test(rel);

/** Where a type's pages live: its folder, or the GDD's top for a type at the top. */
export const folderPath = (t) => (t.folder ? GDD + "/" + t.folder : GDD);
/** The type whose folder a page (or folder) is in, by its path; a page at the top of the GDD belongs to the type at the top, unless it's one of the top pages. */
export function typeAt(types, path) {
  if (typeof path !== "string") return null;
  const inside = types.filter((t) => t.folder && (path === folderPath(t) || path.startsWith(folderPath(t) + "/")));
  if (inside.length) return inside.sort((a, b) => b.folder.length - a.folder.length)[0];
  const atTop = path.endsWith(".md") && path.slice(0, path.lastIndexOf("/")) === GDD && !TOP.includes(path);
  return atTop || path === GDD ? types.find((t) => !t.folder) || null : null;
}

/** A type's template made into a new page: its first title line becomes the page's title. */
export function fromTemplate(template, title) {
  return stripPrompts(template.replace(/\r\n/g, "\n")).replace(/^#\s+.*$/m, "# " + title);
}

// A template's guidance is written as HTML comments: one straight under a heading is that section's
// prompt, one after a field ("- Skill: <!-- its page, as a link -->") is the field's hint. Codex shows
// them faintly while the section or field is empty, and they never go into a page.
const COMMENT = /<!--([\s\S]*?)-->/g;
const clean = (s) => s.replace(/\s+/g, " ").trim();

/** A template without its guidance: comments gone, the lines they stood on gone, no doubled blank lines. */
export function stripPrompts(text) {
  return String(text)
    .replace(/^[ \t]*<!--[\s\S]*?-->[ \t]*\n/gm, "")
    .replace(COMMENT, "")
    .replace(/[ \t]+$/gm, "")
    .replace(/\n{3,}/g, "\n\n");
}

/** A template's guidance: { sections: { name: prompt }, fields: { name: hint } }, names as nameOf gives them. */
export function promptsOf(text) {
  const sections = {}, fields = {};
  let under = null, open = false; // the heading just passed, and whether nothing has come after it yet
  const lines = String(text || "").replace(/\r\n/g, "\n").split("\n");
  for (let i = 0; i < lines.length; i++) {
    const l = lines[i];
    const hd = l.match(/^#{1,6}\s+(.*?)\s*$/);
    if (hd) { under = nameOf(hd[1]); open = true; continue; }
    if (!l.trim()) continue;
    if (open && under && /^\s*<!--/.test(l)) {
      let block = l;
      while (!block.includes("-->") && i + 1 < lines.length) block += "\n" + lines[++i];
      sections[under] = clean(block.replace(/^\s*<!--/, "").replace(/-->[\s\S]*$/, ""));
      open = false;
      continue;
    }
    open = false;
    const f = l.match(/^\s*[-*]\s+([^:<]+?)\s*:\s*<!--([\s\S]*?)-->\s*$/);
    if (f) fields[nameOf(f[1])] = clean(f[2]);
  }
  return { sections, fields };
}

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
    .filter((r) => r.type && "folder" in r)
    .map((r) => {
      const covers = r["what it covers"] || "";
      return {
        type: r.type.toLowerCase(),
        // "(top)" (or ".", or nothing) puts the type's pages at the top of the GDD, beside the README.
        folder: /^(\(top\)|\.|—|-)?$/i.test(r.folder.trim()) ? "" : r.folder.replace(/^\/+|\/+$/g, ""),
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
        // "Repeats: yes": a page may hold several (a range's one action per dish), each an H3 in its section.
        repeats: /^(yes|y|✓|x)$/i.test(String(r.repeats || "").trim()),
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

/** A component's section from its template (templates/<name>.md, or null for none yet): the snippet as written, else its heading and fields. */
export function sectionText(c, template) {
  return stripPrompts(template != null ? String(template).replace(/\r\n/g, "\n") : componentSection(c)).replace(/^\s+|\s+$/g, "") + "\n";
}

/** One more instance of a repeating component (a range's second action): an H3 named `name` and the template's fields. */
export function instanceText(c, template, name) {
  const body = sectionText(c, template).replace(/^##\s+.*\n?/, "").replace(/^\s+|\s+$/g, "");
  return "### " + name + "\n\n" + (body ? body + "\n" : "");
}

/** Every page may end with this section: what's still unsolved. */
export const OPEN_QUESTIONS = "Open questions";

/** Is this component usual on pages of this type ("Found on")? */
export const fitsType = (c, type) => c.foundOn.includes(type) || c.foundOn.some((x) => /almost anything/.test(x));

/** The components for a page of this type, each list in the canonical order: { common, more }. */
export function componentsFor(components, type) {
  return { common: components.filter((c) => fitsType(c, type)), more: components.filter((c) => !fitsType(c, type)) };
}

/**
 * Where a section sits on a page of this type: the core headings first (in the type's order), then the
 * components in their canonical order (How the GDD Works' tables, top to bottom), then Open questions.
 * null for a section of the author's own, which keeps its place after the section before it.
 */
export function sectionRank(heading, type, components) {
  const n = nameOf(heading);
  const core = (type?.headings || []).map(nameOf).indexOf(n);
  if (core >= 0) return core;
  const c = components.findIndex((x) => x.name === n);
  if (c >= 0) return 1000 + c;
  if (n === nameOf(OPEN_QUESTIONS)) return 1e6;
  return null;
}

/** The ranks of a page's H2 sections in order, a section of the author's own taking the rank of the one before it. */
function ranks(headings, type, components) {
  let last = -1;
  return headings.map((hd) => { const r = sectionRank(hd, type, components); if (r != null) last = r; return r ?? last; });
}

/** Where a new section `name` goes among a page's H2 headings: the index to insert it before (headings.length: at the end). */
export function insertIndex(headings, name, type, components) {
  const want = sectionRank(name, type, components) ?? 1e6 - 1;
  const at = headings.findIndex((hd) => { const r = sectionRank(hd, type, components); return r != null && r > want; });
  return at < 0 ? headings.length : at;
}

/** The page's H2 sections put in the fixed order, as indexes into `headings`; the same order when it's already right. */
export function canonicalOrder(headings, type, components) {
  const r = ranks(headings, type, components);
  return headings.map((_, i) => i).sort((a, b) => r[a] - r[b] || a - b);
}
export const inOrder = (headings, type, components) => canonicalOrder(headings, type, components).every((x, i) => x === i);

/**
 * Has a section anything written in it, beyond its template's empty fields? `lines` are its lines of text
 * as they read (Markdown marks or not), without its heading. "- Verb:" with nothing after it is empty.
 */
export function hasContent(lines) {
  return lines.some((l) => {
    const t = String(l).replace(/^\s*(?:[-*+]|\d+\.)\s+/, "").replace(/[*_`]/g, "").trim();
    return t && !/^[^:]{1,40}:$/.test(t);
  });
}

/**
 * A new page of a type with the chosen components: `text` (from the type's template, or newPageText) with
 * the components in its frontmatter and their sections after the core headings, in the canonical order.
 * `templates`: component name → its template text (or null for none yet).
 */
export function withComponents(text, names, components, type, templates = new Map()) {
  const chosen = components.filter((c) => names.includes(c.name)); // canonical order, whatever order they were picked in
  if (!chosen.length) return text;
  const parts = split(text);
  if (parts.front) text = setField(parts.front, "components", chosen.map((c) => c.name)) + parts.body;
  let out = text.replace(/\s+$/, "") + "\n";
  for (const c of chosen) {
    const heads = [...out.matchAll(/^##\s+(.*?)\s*$/gm)];
    const i = insertIndex(heads.map((m) => m[1]), c.name, type, components);
    const section = sectionText(c, templates.get(c.name) ?? null);
    if (i >= heads.length) out = out.replace(/\s+$/, "") + "\n\n" + section;
    else out = out.slice(0, heads[i].index) + section + "\n" + out.slice(heads[i].index);
  }
  return out;
}

/**
 * A new page of a type: its frontmatter (the type, status stub), its title and the type's core headings.
 * Without a type: just the title. `make` is frontmatter.js's.
 */
export function newPageText(title, type, make = null) {
  const front = type && make ? make({ type: type.type, components: [], groups: [], "based-on": null, status: "stub" }) : "";
  // (A group's members aren't written: the page's "Links to this page" box gathers them.)
  return front + "# " + title + "\n" + (type?.headings || []).map((hd) => "\n## " + hd + "\n").join("");
}

/**
 * The guidance in every template, for the editor: { types: { type: prompts }, components: { name: prompts } }
 * (promptsOf's shape). `texts`: path → text, the templates among them.
 */
export function readPrompts(texts, system) {
  const of = (name) => promptsOf(texts.get(TEMPLATES + "/" + name + ".md") || "");
  return {
    types: Object.fromEntries(system.types.map((t) => [t.type, of(t.type)])),
    components: Object.fromEntries(system.components.map((c) => [c.name, of(c.name)])),
  };
}

// ----------------------------------------------------------------------------- sections, on a page's text

/** A page's text cut at its H2s (code skipped): { head, sections: [{ heading, text }] }; head + every text is the page. */
function cutAtSections(text) {
  const out = { head: "", sections: [] };
  let fence = null, cur = null;
  for (const line of text.split(/(?<=\n)/)) {
    const f = line.match(/^\s*(`{3,}|~{3,})/);
    if (fence) { if (f && f[1][0] === fence[0]) fence = null; }
    else if (f) fence = f[1];
    else {
      const hd = line.match(/^##[ \t]+(.*?)[ \t]*#*[ \t]*\r?\n?$/);
      if (hd) { cur = { heading: hd[1], text: "" }; out.sections.push(cur); }
    }
    if (cur) cur.text += line; else out.head += line;
  }
  return out;
}
const withCrlf = (orig, fn) => { const crlf = orig.includes("\r\n"); const out = fn(orig.replace(/\r\n/g, "\n")); return crlf ? out.replace(/\n/g, "\r\n") : out; };
const ended = (s) => s.replace(/\s*$/, "") + "\n\n";

/** The page's sections in the fixed order (core headings, components, Open questions), as text. */
export function reorderText(text, type, components) {
  return withCrlf(text, (t) => {
    const { head, sections } = cutAtSections(t);
    if (!sections.length) return t;
    const order = canonicalOrder(sections.map((s) => s.heading), type, components);
    if (order.every((x, i) => x === i)) return t;
    return (ended(head) + order.map((i) => ended(sections[i].text)).join("")).replace(/\n+$/, "\n");
  });
}

/** A section (Markdown starting with its "## " heading) put into the page's text in its place. */
export function insertSectionText(text, section, name, type, components) {
  return withCrlf(text, (t) => {
    const { head, sections } = cutAtSections(t);
    const i = insertIndex(sections.map((s) => s.heading), name, type, components);
    const parts = [head, ...sections.map((s) => s.text)];
    parts.splice(i + 1, 0, section.replace(/\s*$/, "") + "\n");
    return parts.map((p, n) => (n < parts.length - 1 ? ended(p) : p.replace(/\s*$/, "") + "\n")).join("");
  });
}
