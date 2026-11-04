// What the GDD's pages say, gathered: each page's frontmatter, its sections, and the fields written in
// its component sections. From this come generated lists (query blocks), the structure checks, and the
// labels on links in ("Dropped by", "Found in"). Plain functions on text; server.js keeps it fresh.
//
// A component section is an H2 named after the component ("## Trade", "## Area rules"). Its fields are
// "- Name: value" lines. Several instances of one component (a range's one action per dish) are H3s
// under it, each with its own fields.

import { split, slugOf, STATUSES } from "./frontmatter.js";
import { nameOf, isMeta, inGdd, GDD } from "./gdd.js";
import { plain, targetOf, titleOf } from "./links.js";

const FENCE = /^\s*(`{3,}|~{3,})/;
const FIELD = /^\s*[-*]\s+(?:\*\*)?([^:*]+?)(?:\*\*)?\s*:\s*(?:\*\*)?\s*(.*)$/;

/** One page: { path, title, slug, front, sections: [{ heading, name, line, instances: [{ name, fields }] }] }. */
export function pageInfo(path, text) {
  const { data, body } = split(text);
  const offset = text.slice(0, text.length - body.length).split("\n").length - 1;
  const sections = [];
  let sec = null, inst = null, fence = null;
  body.split("\n").forEach((line, i) => {
    const f = line.match(FENCE);
    if (fence) { if (f && f[1][0] === fence[0]) fence = null; return; }
    if (f) { fence = f[1]; return; }
    const hd = line.match(/^(#{2,3})\s+(.*?)\s*#*\s*$/);
    if (hd && hd[1] === "##") {
      sec = { heading: plain(hd[2]), name: nameOf(plain(hd[2])), line: offset + i + 1, instances: [] };
      inst = { name: null, fields: {} };
      sec.instances.push(inst);
      sections.push(sec);
      return;
    }
    if (hd && sec) {
      inst = { name: plain(hd[2]), fields: {} };
      if (!Object.keys(sec.instances[0].fields).length && !sec.instances[0].name) sec.instances.shift();
      sec.instances.push(inst);
      return;
    }
    const m = sec && line.match(FIELD);
    if (m && inst) inst.fields[m[1].trim().toLowerCase()] = m[2].trim();
  });
  return { path, title: titleOf(body), slug: slugOf(path), front: data, sections };
}

/** Every GDD page: { pages: Map(path → info), bySlug: Map(slug → [path]) }. */
export function buildGdd(texts) {
  const pages = new Map(), bySlug = new Map();
  for (const [path, text] of texts) {
    if (!inGdd(path)) continue;
    const info = pageInfo(path, text);
    pages.set(path, info);
    if (!bySlug.has(info.slug)) bySlug.set(info.slug, []);
    bySlug.get(info.slug).push(path);
  }
  return { pages, bySlug };
}

// ----------------------------------------------------------------------------- generated lists

/**
 * A query block's text as filters: "key: value" lines. Keys: type, status, groups, components, based-on
 * (frontmatter); links-to; and "<component>.<field>" (instances of a component whose field matches).
 * "this" means the page the block is on. Also "sort: <field>".
 */
export function parseQuery(text) {
  const out = [];
  for (const line of String(text || "").split("\n")) {
    const m = line.match(/^\s*([\w.-]+)\s*:\s*(.*?)\s*$/);
    if (m) out.push({ key: m[1].toLowerCase(), value: m[2] });
  }
  return out;
}

const norm = (s) => plain(String(s || "")).toLowerCase().trim();

/** Does a written value (maybe a link) name this page? By its title, its slug, or a link to it. */
function names(value, from, target) {
  const v = String(value || "");
  for (const m of v.matchAll(/\]\(\s*<?([^)\s>]+)>?\s*\)/g)) {
    const to = targetOf(from, m[1]);
    if (to === target.path) return true;
  }
  const p = norm(v);
  return !!p && (p === norm(target.title) || p === target.slug || p.split(/\s*,\s*/).some((x) => x === norm(target.title) || x === target.slug));
}

function matches(value, want, from, gdd, here) {
  if (want.toLowerCase() === "this") return here ? names(value, from, here) : false;
  const p = norm(value);
  return p === norm(want) || p.split(/\s*,\s*/).includes(norm(want)) || (gdd.bySlug.get(want) || []).some((t) => names(value, from, gdd.pages.get(t)));
}

/**
 * Run a query from a page. { kind: "pages", rows: [{ path, title, type, status }] } or, with a
 * "<component>.<field>" filter, { kind: "instances", component, columns, rows: [{ path, title, name, fields }] }.
 */
export function runQuery(gdd, links, page, text) {
  const filters = parseQuery(text);
  const here = gdd.pages.get(page) || null;
  const sort = filters.find((f) => f.key === "sort")?.value?.toLowerCase() || "title";
  const comp = filters.find((f) => f.key.includes("."))?.key.split(".")[0] || null;
  const pageOk = (info) => filters.every(({ key, value }) => {
    const fm = info.front || {};
    if (key === "sort" || key.includes(".")) return true;
    if (key === "links-to") {
      const target = value.toLowerCase() === "this" ? page : (gdd.bySlug.get(value) || [])[0];
      return (links.out.get(info.path) || []).some((l) => l.to === target);
    }
    const have = fm[key];
    if (Array.isArray(have)) return have.some((x) => matches(x, value, info.path, gdd, here) || (value.toLowerCase() === "this" && here && x === here.slug));
    return have != null && (matches(have, value, info.path, gdd, here) || (value.toLowerCase() === "this" && here && have === here.slug));
  });
  const pages = [...gdd.pages.values()].filter((p) => p.path !== page && !isMeta(p.path) && pageOk(p));
  const byTitle = (a, b) => String(a.title || a.path).localeCompare(String(b.title || b.path), "en", { numeric: true });
  if (!comp) {
    const rows = pages.map((p) => ({ path: p.path, title: p.title, type: p.front?.type || null, status: p.front?.status || null }));
    return { kind: "pages", rows: rows.sort(sort === "status" ? (a, b) => String(a.status).localeCompare(String(b.status)) || byTitle(a, b) : byTitle) };
  }
  const fieldFilters = filters.filter((f) => f.key.startsWith(comp + "."));
  const rows = [], columns = new Set();
  for (const p of pages) {
    for (const sec of p.sections.filter((s) => s.name === comp)) {
      for (const inst of sec.instances) {
        const ok = fieldFilters.every(({ key, value }) => matches(inst.fields[key.slice(comp.length + 1)], value, p.path, gdd, here));
        if (!ok) continue;
        Object.keys(inst.fields).forEach((k) => columns.add(k));
        rows.push({ path: p.path, title: p.title, name: inst.name, fields: inst.fields });
      }
    }
  }
  const num = (v) => { const n = parseFloat(String(v || "").replace(/[^\d.]/g, "")); return Number.isFinite(n) ? n : Infinity; };
  rows.sort(sort === "title" ? byTitle : (a, b) => num(a.fields[sort]) - num(b.fields[sort]) || byTitle(a, b));
  const shown = [...columns].filter((c) => !fieldFilters.some((f) => f.key === comp + "." + c));
  return { kind: "instances", component: comp, columns: shown, rows };
}

// ----------------------------------------------------------------------------- labels on links in

/** How a link from `from`'s section shows on the page it points to ("Dropped by"), or null. */
export function labelFor(gdd, system, from, section) {
  const info = gdd.pages.get(from);
  if (!info) return null;
  const sec = nameOf(String(section || "").split(" › ")[0]);
  const type = info.front?.type;
  const sections = new Set([...system.components.map((c) => c.name), ...system.types.flatMap((t) => t.headings.map(nameOf))]);
  for (const o of system.owners) {
    if (sec && o.source.includes(sec)) return o.label;
    // A type as the source ("…or ability doing it"), when none of the row's sections belong on that type.
    const own = o.source.filter((s) => sections.has(s));
    const onType = (s) => system.components.find((c) => c.name === s)?.foundOn.includes(type) || system.types.find((t) => t.type === type)?.headings.map(nameOf).includes(s);
    if (type && o.source.includes(type) && !own.some(onType)) return o.label;
  }
  return null;
}

// ----------------------------------------------------------------------------- checks

/**
 * What's wrong with the GDD's structure: [{ path, title, line, kind, text }]. Kinds: "no-frontmatter",
 * "no-type", "unknown-type", "wrong-folder", "unknown-component", "missing-section", "undeclared-section",
 * "unknown-status", "missing-group", "missing-based-on".
 */
export function checks(gdd, system) {
  const out = [];
  const types = new Map(system.types.map((t) => [t.type, t]));
  const comps = new Map(system.components.map((c) => [c.name, c]));
  for (const p of gdd.pages.values()) {
    if (isMeta(p.path)) continue;
    const add = (kind, text, line = 1) => out.push({ path: p.path, title: p.title, line, kind, text });
    const fm = p.front;
    if (!fm) { add("no-frontmatter", "No frontmatter: every page starts with its type and status"); continue; }
    if (!fm.type) add("no-type", "No type");
    else if (!types.has(fm.type)) add("unknown-type", `Unknown type “${fm.type}”`);
    else {
      const folder = GDD + "/" + types.get(fm.type).folder + "/";
      if (!p.path.startsWith(folder)) add("wrong-folder", `A ${fm.type} page belongs in ${folder.slice(GDD.length + 1, -1)}/`);
    }
    if (fm.status && !STATUSES.includes(fm.status)) add("unknown-status", `Unknown status “${fm.status}” (${STATUSES.join(", ")})`);
    const declared = fm.components || [];
    const sectionNames = new Set(p.sections.map((s) => s.name));
    for (const c of declared) {
      const n = nameOf(c);
      if (!comps.has(n)) add("unknown-component", `Unknown component “${c}”`);
      else if (!sectionNames.has(n)) add("missing-section", `${c} is declared but has no section`);
    }
    const declaredSet = new Set(declared.map(nameOf));
    for (const s of p.sections) if (comps.has(s.name) && !declaredSet.has(s.name)) add("undeclared-section", `“${s.heading}” is a component section, but ${s.name} isn't in its components`, s.line);
    for (const g of fm.groups || []) if (!gdd.bySlug.has(g)) add("missing-group", `Group “${g}” has no page yet`);
    if (fm["based-on"] && !gdd.bySlug.has(fm["based-on"])) add("missing-based-on", `Based on “${fm["based-on"]}”, which has no page yet`);
  }
  return out;
}
