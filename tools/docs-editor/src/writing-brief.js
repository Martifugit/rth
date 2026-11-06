// How R'th AI writes pages: The Charter and Writing a Page (docs/gdd/writing.md), read fresh from the
// GDD on every new conversation and given to the AI whole, whichever provider answers. Telling a model
// to "read the charter first" isn't enough (it often doesn't, and a model without the repository
// can't), so the rules travel in the prompt. Changing how the AI writes is an edit to those pages,
// never to code.

import { CHARTER, GDD } from "./gdd.js";

export const WRITING = GDD + "/writing.md";

/** The gist, said before the pages, for every provider. */
export const WIKI_RULE = [
  "How to write GDD pages (whenever you write or change one, and when you advise someone writing one):",
  "The GDD is a wiki. A page holds the facts about its own thing and nothing else. Everything else it mentions (a system, a place, a creature, an item, a group, another skill) is linked to its own page, existing or not, with at most a clause on how it touches this page. Never explain a linked thing in place, and never copy a fact, number or list from another page.",
  "If you have a fact that belongs on another page, put it there (a stub from its type's template in docs/gdd/templates/ if the page doesn't exist yet) and link to it. A section that starts explaining something else becomes one sentence and a link. Prefer a short page with many links to a long page that explains.",
  "Keep the type's headings exactly as its template has them, in its order; intent (why) first, numbers last; unsettled things as questions under Open questions. Write plainly: short sentences, present tense, decisions not hedges, no selling words.",
  "After a change, name any links you made to pages that don't exist yet, as to-dos.",
  "The two pages below are the full rules; follow them.",
].join("\n");

/** WIKI_RULE and the two pages, as one block of prompt text. read(rel) gives a page's text or null. */
export async function writingBrief(read) {
  const parts = [WIKI_RULE];
  for (const [rel, title] of [[CHARTER, "The Charter"], [WRITING, "Writing a Page"]]) {
    let text = null;
    try { text = await read(rel); } catch { /* a page that isn't there: left out of the brief */ }
    if (text) parts.push(`=== ${rel} (${title}) ===\n${String(text).replace(/\r\n/g, "\n").trim()}`);
  }
  return parts.join("\n\n");
}

/** What a follow-up message carries: the rule in a line (the session already has the pages). */
export const WIKI_REMINDER = "(When you write a page: its own facts only; everything else linked to its own page, as Writing a Page says.)";
