// A GDD page's frontmatter (How the GDD Works, "3. Frontmatter"): the small block between "---" lines
// at the top of the page. Read and written here, for the server (lists, checks) and the editor (the card
// over the page). Only the shape the GDD uses: "key: value" and "key: [a, b]" lines (lists may also be
// written as "- item" lines under the key). Writing a field changes only its own line, so the rest of
// the block stays exactly as it was typed.

export const FIELDS = ["type", "components", "groups", "based-on", "status"];
export const LISTS = new Set(["components", "groups"]);
export const STATUSES = ["stub", "draft", "agreed", "cut"];

const OPEN = /^---[ \t]*\r?\n/;

/** { front, data, body }: `front` is the block as written (with its "---" lines and the blank line after it), `body` the rest. */
export function split(text) {
  text = text || "";
  const m = text.match(OPEN);
  if (!m) return { front: "", data: null, body: text };
  const close = /^---[ \t]*$/m;
  const rest = text.slice(m[0].length);
  const c = rest.match(close);
  if (!c) return { front: "", data: null, body: text };
  let end = m[0].length + c.index + c[0].length;
  if (text[end] === "\r") end++;
  if (text[end] === "\n") end++;
  while (text.slice(end).match(/^[ \t]*\r?\n/)) end += text.slice(end).match(/^[ \t]*\r?\n/)[0].length; // blank lines after it
  const front = text.slice(0, end);
  return { front, data: parse(rest.slice(0, c.index)), body: text.slice(end) };
}

const unquote = (s) => s.trim().replace(/^(["'])(.*)\1$/, "$2");
const listOf = (v) => v.replace(/^\[|\]$/g, "").split(",").map(unquote).filter(Boolean);

/** The block's fields: { type, components: [], groups: [], "based-on", status, ...any other key }. */
export function parse(inner) {
  const data = {};
  let last = null;
  for (const line of inner.replace(/\r\n/g, "\n").split("\n")) {
    const item = line.match(/^\s+-\s+(.*)$/) || line.match(/^-\s+(.*)$/);
    if (item && last) { data[last] = [...(Array.isArray(data[last]) ? data[last] : []), unquote(item[1])].filter(Boolean); continue; }
    const kv = line.match(/^([A-Za-z][\w-]*)\s*:\s*(.*)$/);
    if (!kv) continue;
    const [, key, raw] = kv;
    last = key;
    const v = raw.trim();
    data[key] = v.startsWith("[") || LISTS.has(key) ? listOf(v) : unquote(v) || null;
  }
  for (const k of LISTS) if (!Array.isArray(data[k])) data[k] = data[k] ? [String(data[k])] : [];
  if (data.type) data.type = String(data.type).toLowerCase();
  if (data.status) data.status = String(data.status).toLowerCase();
  return data;
}

const writeValue = (key, value) => (LISTS.has(key) || Array.isArray(value) ? "[" + (value || []).join(", ") + "]" : value == null ? "" : String(value));

/** A new block, fields in the GDD's order. */
export function make(data) {
  return "---\n" + FIELDS.map((k) => (k + ":" + (writeValue(k, data[k]) ? " " + writeValue(k, data[k]) : ""))).join("\n") + "\n---\n\n";
}

/** The block with one field set (its line rewritten, or added before the closing "---"); "" makes a new block. */
export function setField(front, key, value) {
  if (!front) return make({ [key]: value });
  const lines = front.split("\n");
  const close = lines.findIndex((l, i) => i > 0 && /^---[ \t]*\r?$/.test(l));
  const cr = lines[0].endsWith("\r") ? "\r" : "";
  const line = key + ":" + (writeValue(key, value) ? " " + writeValue(key, value) : "") + cr;
  let at = lines.findIndex((l, i) => i > 0 && i < close && new RegExp("^" + key.replace(/[-]/g, "\\-") + "\\s*:").test(l));
  if (at < 0) { lines.splice(close, 0, line); return lines.join("\n"); }
  let n = 1; // a list written as "- item" lines under the key goes with it
  while (at + n < close && /^\s*-\s/.test(lines[at + n])) n++;
  lines.splice(at, n, line);
  return lines.join("\n");
}

/** A page's name as frontmatter refers to it: its file name without ".md" ("groups: [trees]" is groups/trees.md). */
export const slugOf = (p) => p.split("/").pop().replace(/\.md$/i, "");
