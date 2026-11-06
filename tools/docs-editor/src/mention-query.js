// When a text box's picker is open (an @ page in the AI box, /ask or @agent in a comment), and what it
// searches for. Plain rules, tested in test/mention-query.test.js; src/text-picker.js draws the list.
//
// 1. It opens at its character (@, /) that starts the text or follows a space, with the cursor in the
//    word after it. A `startOnly` trigger (/ask) opens only at the very start of the text.
// 2. Picking finishes it: whatever is typed after a picked item never opens the picker again. Editing
//    inside the picked word itself (deleting back into it) opens it again to pick another.
// 3. Once a space is typed and nothing matches, you're writing a sentence, not a name: it closes.
//    Up to the first space it says "nothing by that name", so you see the character was heard.
// 4. Escape closes it for that character only; a new one opens it again.
// 5. A new line, more than 40 characters, or the cursor leaving the word (click, arrows, blur) closes it.

export const MAX = 40;

const esc = (s) => s.replace(/[.*+?^${}()|[\]\\/]/g, "\\$&");

/**
 * The word being picked at the cursor, or null when the picker should be closed.
 * @param {string} before   the box's text up to the cursor
 * @param {object} o
 * @param {string}   [o.char]       the character that opens it ("@")
 * @param {boolean}  [o.startOnly]  only at the start of the text
 * @param {string[]} [o.picked]     tokens ("@Title") picked into this text
 * @param {number}   [o.dismissed]  index of the character that Escape closed, or -1
 * @param {(q: string) => boolean} [o.matches]  whether anything matches the query
 * @returns {{ start: number, q: string, none: boolean } | null}  start: the character's index; none: show "nothing"
 */
export function mentionQuery(before, { char = "@", startOnly = false, picked = [], dismissed = -1, matches = () => true } = {}) {
  const c = esc(char);
  const m = before.match(new RegExp(`(${startOnly ? "^\\s*" : "^|\\s"})${c}([^${c}\\n]{0,${MAX}})$`));
  if (!m) return null;
  const start = before.length - m[2].length - 1;
  const q = m[2];
  if (start === dismissed) return null;
  // A picked item with more typed after it (its trailing space at least) is finished.
  const run = char + q;
  if (picked.some((t) => run.length > t.length && run.startsWith(t) && /\s/.test(run[t.length]))) return null;
  if (matches(q)) return { start, q, none: false };
  if (/\s/.test(q)) return null;
  return { start, q, none: true };
}
