// Small things done to text everywhere in the page: escaping for HTML, counting, shortening.

/** Text made safe to put in HTML. */
export const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);

/** "1 reply", "3 replies". */
export const plural = (n, one, many = one + "s") => `${n} ${n === 1 ? one : many}`;

/** One line of at most n characters, with … where it was cut. */
export function clip(s, n) {
  s = String(s || "").replace(/\s+/g, " ").trim();
  return s.length > n ? s.slice(0, n - 1).trimEnd() + "…" : s;
}
