// Comments from the AI tab (PLAN.md, "AI, built in"): asked to leave something as a comment, or to
// answer the comment that's open, the AI writes a block in its reply, and the engine posts it through
// the comment store, so it lands where comments are: the Discord forum when signed in, docs/.notes/
// when not. Both providers use it (Claude Code never writes comment files itself).
//
//   <comment page="docs/x.md" section="Heading > Subheading" quote="exact words from the page">text</comment>
//   <reply to="THREAD-ID">text</reply>

export const COMMENT_FORMAT = [
  "Comments: only when they ask you to leave something as a comment (or to answer the comment that's open), write a block in your reply; Codex posts it as a comment from you and takes the block out of what's shown. Never write comment files yourself.",
  "A new comment on a page: <comment page=\"docs/gdd/path/to/page.md\" section=\"Heading > Subheading\" quote=\"a few exact words from the page\">the comment, in Markdown</comment> (section and quote are optional; without them it's on the whole page).",
  "A reply in an existing comment thread: <reply to=\"THREAD-ID\">the reply</reply>.",
].join("\n");

const COMMENT = /<comment\s+([^>]*)>([\s\S]*?)<\/comment>/g;
const REPLY = /<reply\s+to="([^"]+)"\s*>([\s\S]*?)<\/reply>/g;

function attrs(s) {
  const out = {};
  for (const m of s.matchAll(/(\w+)="([^"]*)"/g)) out[m[1]] = m[2].replace(/&quot;/g, '"').replace(/&amp;/g, "&");
  return out;
}

/** The comment and reply blocks in an answer, and the answer without them. */
export function commentsIn(text) {
  const comments = [], replies = [];
  let rest = text.replace(COMMENT, (m, a, body) => {
    const x = attrs(a);
    if (x.page) comments.push({ page: x.page.trim(), section: x.section || "", quote: x.quote || "", text: body.trim() });
    return "";
  });
  rest = rest.replace(REPLY, (m, to, body) => { replies.push({ to: to.trim(), text: body.trim() }); return ""; });
  return { comments, replies, text: rest.replace(/\n{3,}/g, "\n\n").trim() };
}

/** An answer as it streams: finished blocks out, and a block still being written hidden. */
export function withoutComments(text) {
  return commentsIn(text).text.replace(/<(comment|reply)\b(?![\s\S]*<\/\1>)[\s\S]*$/, "").trim();
}

const plain = (s) => String(s || "").replace(/[*_`]/g, "").replace(/\s+/g, " ").trim();

/** Where on the page a comment goes, as the Codex anchors one: its section path and quoted words. */
export function anchorFor(pageText, section, quote) {
  const path = String(section || "").split(/\s*[>›]\s*/).map(plain).filter(Boolean);
  const title = plain((String(pageText || "").match(/^#\s+(.+)$/m) || [])[1] || "");
  if (title && path.length && path[0] !== title) path.unshift(title);
  // Quoted words that aren't on the page anchor nothing: the comment goes on the section (or page).
  const q = quote && String(pageText || "").includes(quote) ? quote : "";
  return { anchor: { section: path, quote: q, prefix: "", suffix: "" }, title: title || null };
}
