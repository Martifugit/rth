// When the AI box's @ page picker is open, and what it searches for (src/ai.js). Plain rules, tested in
// test/mention-query.test.js:
//
// 1. It opens at an @ that starts the message or follows a space, with the cursor in the word after it.
// 2. Picking a page finishes that mention: whatever is typed after it never opens the picker again.
//    Editing inside the mention itself (deleting back into the title) opens it again to pick another.
// 3. Once a space is typed and no page matches, you're writing a sentence, not a name: it closes.
//    Up to the first space it says "No page by that name", so you see the @ was heard.
// 4. Escape closes it for that @ only; a new @ opens it again.
// 5. A new line, more than 40 characters, or the cursor leaving the word (click, arrows, blur) closes it.

export const MAX = 40;

/**
 * The @ being typed at the cursor, or null when the picker should be closed.
 * @param {string} before   the box's text up to the cursor
 * @param {object} o
 * @param {string[]} [o.picked]     tokens ("@Title") picked into this message
 * @param {number}   [o.dismissed]  index of the @ that Escape closed, or -1
 * @param {(q: string) => boolean} [o.matches]  whether any page matches the query
 * @returns {{ start: number, q: string, none: boolean } | null}  start: the @'s index; none: show "No page"
 */
export function mentionQuery(before, { picked = [], dismissed = -1, matches = () => true } = {}) {
  const m = before.match(new RegExp(`(^|\\s)@([^@\\n]{0,${MAX}})$`));
  if (!m) return null;
  const start = before.length - m[2].length - 1;
  const q = m[2];
  if (start === dismissed) return null;
  // A picked mention with more typed after it (its trailing space at least) is finished.
  const run = "@" + q;
  if (picked.some((t) => run.length > t.length && run.startsWith(t) && /\s/.test(run[t.length]))) return null;
  if (matches(q)) return { start, q, none: false };
  if (/\s/.test(q)) return null;
  return { start, q, none: true };
}
