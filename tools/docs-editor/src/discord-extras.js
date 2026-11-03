// Small things that make the Codex and Discord feel like one place (PLAN.md, "Discord, beyond comments").
//
//   Copy for Discord (Ctrl+Shift+C): the selection, or the section you're in, as Discord's Markdown
//     (tables as code blocks, which Discord lacks; headings it can't show as bold), under its 2,000
//     characters, with a link that opens the page in the Codex.
//   Pasting from Discord: a copied conversation ("Alice — Today at 17:12" lines) goes in as a quote
//     with who said what; an image (copied, or a Discord image link) is saved into the docs, because
//     Discord's links expire within a day.

const HELPER = "https://rth-codex.rthapp.workers.dev";
const LIMIT = 2000;

// ----------------------------------------------------------------------------- copy for Discord

/** Markdown from the docs, as Discord shows it. */
export function toDiscord(md) {
  const out = [];
  let table = [];
  const flush = () => { if (table.length) { out.push("```", ...table, "```"); table = []; } };
  for (let line of md.replace(/\r\n/g, "\n").split("\n")) {
    if (/^\s*\|/.test(line)) { table.push(line); continue; }
    flush();
    line = line
      .replace(/^#{4,6}\s+(.*)$/, "**$1**")                       // Discord shows # to ### only
      .replace(/!\[([^\]]*)\]\([^)]*\)/g, (m, alt) => (alt ? `[image: ${alt}]` : "[image]"))
      .replace(/^(\s*[-*]) \[ \] /, "$1 ☐ ").replace(/^(\s*[-*]) \[[xX]\] /, "$1 ☑ ")
      // Links to other pages mean nothing in Discord: keep their words.
      .replace(/\[([^\]]+)\]\((?!https?:|mailto:)[^)]*\)/g, "$1");
    out.push(line);
  }
  flush();
  return out.join("\n").replace(/\n{3,}/g, "\n\n").trim();
}

/** The text to paste in Discord: the passage, then a quiet link to the page (at its heading). */
export function forDiscord(md, { page, title, heading }) {
  const q = new URLSearchParams({ p: page });
  if (heading) q.set("h", heading);
  const link = `\n-# [${(title || page).replace(/[\[\]]/g, "")}](<${HELPER}/open?${q}>)`;
  let body = toDiscord(md);
  const room = LIMIT - link.length - 6; // and room to close a code block cut short
  if (body.length > room) {
    body = body.slice(0, room - 2).replace(/\s+\S*$/, "") + " …";
    if ((body.match(/```/g) || []).length % 2) body += "\n```"; // never leave a code block open
  }
  return body + link;
}

// ----------------------------------------------------------------------------- a pasted conversation

const COZY = /^(.{1,60}?)\s+[—–-]\s+(?:(?:Today|Yesterday) at\s+|\d{1,4}[/.-]\d{1,2}[/.-]\d{1,4},?\s+)?\d{1,2}[:.]\d{2}(?:\s?[AP]M)?\s*$/i;
const COMPACT = /^\[\d{1,2}[:.]\d{2}(?:\s?[AP]M)?\]\s*([^:]{1,60}):\s(.*)$/i;

/** A conversation copied from Discord: [{ name, text }], or null if it isn't one. */
export function discordConversation(text) {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  const msgs = [];
  let heads = 0;
  for (const raw of lines) {
    const line = raw.trimEnd();
    let m;
    if ((m = line.match(COZY))) { heads++; msgs.push({ name: m[1].replace(/^\s*[@]?/, "").trim(), text: [] }); continue; }
    if ((m = line.match(COMPACT))) { heads++; msgs.push({ name: m[1].trim(), text: [m[2]] }); continue; }
    if (!msgs.length) { if (line.trim()) return null; continue; } // text before the first name: not a chat
    msgs[msgs.length - 1].text.push(line);
  }
  if (!heads) return null;
  const out = msgs.map((x) => ({ name: x.name, text: x.text.join("\n").trim() })).filter((x) => x.text);
  return out.length ? out : null;
}

/** As the docs write a quoted conversation: a quote, each message after its speaker's name in bold. */
export function conversationMarkdown(msgs) {
  return msgs.map((x) => `> **${x.name}:** ${x.text.split("\n").join("\n> ")}`).join("\n>\n") + "\n";
}

// ----------------------------------------------------------------------------- images

export const isDiscordImage = (s) => /^https:\/\/(?:cdn\.discordapp\.com|media\.discordapp\.net)\/attachments\/\S+$/i.test(s.trim());

const toBase64 = (file) => new Promise((resolve, reject) => {
  const r = new FileReader();
  r.onload = () => resolve(String(r.result).split(",")[1]);
  r.onerror = reject;
  r.readAsDataURL(file);
});

/**
 * The editor's paste: a Discord conversation or an image (a file, or a Discord image link) is handled
 * here; anything else goes on as usual (return false).
 */
export function makePasteHandler({ S, api, toast }) {
  async function saveImage(body) {
    const r = await api("/api/image", { method: "POST", body: JSON.stringify({ page: S.path, ...body }) });
    if (!r.ok) { toast(r.error || "Couldn't save the image", "bad"); return; }
    S.editor.chain().focus().setImage({ src: r.src, alt: body.alt || "" }).run();
    toast("Saved the image in " + r.saved);
  }
  return (view, event) => {
    const cb = event.clipboardData;
    if (!cb || !S.path) return false;
    const file = [...(cb.files || [])].find((f) => /^image\//.test(f.type));
    if (file) {
      event.preventDefault();
      toBase64(file).then((data) => saveImage({ data, type: file.type, name: file.name }));
      return true;
    }
    const text = cb.getData("text/plain") || "";
    if (isDiscordImage(text)) {
      event.preventDefault();
      saveImage({ url: text.trim() });
      return true;
    }
    const chat = discordConversation(text);
    if (chat) {
      event.preventDefault();
      S.editor.chain().focus().insertContent(conversationMarkdown(chat)).run();
      toast(`Pasted a Discord conversation (${chat.length} message${chat.length > 1 ? "s" : ""}). Ctrl+Z takes it out.`);
      return true;
    }
    return false;
  };
}
