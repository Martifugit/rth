// The To-do's fixes (src/todo.js), planned on the pages' text: planFix returns the new text of every page a
// fix changes, so the server can show which pages a fix touches before doing it, write them, and undo
// it. Every fix to a reference goes through src/refs.js, the same as renaming a page.

import { split, setField, slugOf } from "./frontmatter.js";
import { hrefBetween } from "./links.js";
import { rewriteRefs, relinkLine, unlinkIn, dropAnchor, linkPlain, nameToken } from "./refs.js";
import { GDD, TEMPLATES, inGdd, nameOf, reorderText, insertSectionText, sectionText, typeAt } from "./gdd.js";

const editable = (p) => inGdd(p) && !p.startsWith(TEMPLATES + "/");

/**
 * The pages a fix changes: Map(path → new text). `texts`: path → text; `system`: { types, components };
 * `template(name)`: a component's template text or null. Throws on a fix it doesn't know.
 *
 * Fixes: retarget { from: [paths], id, to } (every reference to a missing page goes to `to`),
 * retarget-one { page, line, from, to }, unlink { page, line, target }, drop-anchor { page, line, target },
 * retarget-ref { page, field, id, to }, drop-ref { page, field, id }, link-field { page, line, text, to },
 * name-placeholder { token, name }, declare / undeclare / add-section { page, component }, reorder { page }.
 */
export function planFix(fix, { texts, system, template = () => null }) {
  const out = new Map();
  const set = (p, next) => { if (next !== texts.get(p)) out.set(p, next); };
  const one = (p, fn) => { if (!texts.has(p) || !editable(p)) throw new Error("No such page: " + p); set(p, fn(texts.get(p))); };
  const typeOfPage = (p) => system.types.find((t) => t.type === split(texts.get(p) || "").data?.type) || typeAt(system.types, p);
  const fm = (text, fn) => { const parts = split(text); if (!parts.data) return text; return fn(parts) + parts.body; };
  switch (fix.kind) {
    case "retarget": {
      const froms = [...(fix.from || [])];
      if (fix.id && !froms.some((p) => slugOf(p) === fix.id)) froms.push(GDD + "/" + fix.id + ".md"); // only named in frontmatter
      for (const [p, text] of texts) {
        if (!editable(p)) continue;
        let t = text;
        for (const f of froms) t = rewriteRefs(t, p, f, fix.to);
        set(p, t);
      }
      break;
    }
    case "retarget-one": one(fix.page, (t) => relinkLine(t, fix.page, fix.line, fix.from, fix.to)); break;
    case "unlink": one(fix.page, (t) => unlinkIn(t, fix.page, fix.line, fix.target)); break;
    case "drop-anchor": one(fix.page, (t) => dropAnchor(t, fix.page, fix.line, fix.target)); break;
    case "retarget-ref": case "drop-ref": one(fix.page, (t) => fm(t, ({ front, data }) => {
      const to = fix.kind === "retarget-ref" ? slugOf(fix.to) : null;
      if (fix.field === "groups") return setField(front, "groups", [...new Set(data.groups.map((g) => (g === fix.id ? to : g)).filter(Boolean))]);
      if (fix.field === "based-on" && data["based-on"] === fix.id) return setField(front, "based-on", to);
      return front;
    })); break;
    case "link-field": one(fix.page, (t) => linkPlain(t, fix.line, fix.text, hrefBetween(fix.page, fix.to))); break;
    case "name-placeholder": {
      if (!/^p-[a-z][a-z0-9-]*$/.test(fix.token || "") || !String(fix.name || "").trim()) throw new Error("A placeholder and its name, please");
      for (const [p, text] of texts) if (editable(p)) set(p, nameToken(text, fix.token, fix.name.trim()));
      break;
    }
    case "declare": case "undeclare": one(fix.page, (t) => fm(t, ({ front, data }) => {
      const order = system.components.map((c) => c.name);
      const at = (n) => { const i = order.indexOf(nameOf(n)); return i < 0 ? 1e6 : i; };
      const have = data.components.filter((c) => nameOf(c) !== nameOf(fix.component));
      return setField(front, "components", (fix.kind === "declare" ? [...have, fix.component] : have).sort((a, b) => at(a) - at(b)));
    })); break;
    case "add-section": {
      const c = system.components.find((x) => x.name === nameOf(fix.component));
      if (!c) throw new Error("Not a component: " + fix.component);
      one(fix.page, (t) => insertSectionText(t, sectionText(c, template(c.name)), c.name, typeOfPage(fix.page), system.components));
      break;
    }
    case "reorder": one(fix.page, (t) => reorderText(t, typeOfPage(fix.page), system.components)); break;
    default: throw new Error("Not a fix Codex knows: " + fix.kind);
  }
  return out;
}
