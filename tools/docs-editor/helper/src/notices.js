// "What changed": GitHub tells the helper about each push to the main branch (the Codex's syncs, an
// agent's commits, anyone's), and the helper posts one short message in #codex naming who changed
// which pages, each a link that opens it in the Codex. Comment files (docs/.notes) and anything
// outside the docs are left out; a push that touches no page posts nothing.

import { installationToken, gh } from "./github.js";
import { post } from "./discord.js";

const MAX_PAGES = 12;

export async function onPush(env, push, origin) {
  const branch = push.repository?.default_branch || "main";
  if (push.ref !== `refs/heads/${branch}` || push.deleted || /^0+$/.test(push.before || "0")) return;
  const isPage = (f) => f.endsWith(".md") && (f.startsWith("docs/") || !f.includes("/")) && !f.startsWith("docs/.notes/");

  const token = (await installationToken(env)).token;
  const cmp = await gh(env, `/repos/${env.REPO}/compare/${push.before}...${push.after}`, token);
  const stat = new Map((cmp.files || []).filter((f) => isPage(f.filename)).map((f) => [f.filename, f]));
  if (!stat.size) return;

  // Who changed what, from the commits. GitHub's message names at most 20 commits: for a bigger push,
  // the pages come from the whole comparison, under everyone who wrote its commits.
  const authors = new Map(); // name -> Set of files
  const commits = push.commits || [];
  if (commits.length >= 20 || (push.size && push.size > commits.length)) {
    const names = [...new Set((cmp.commits || []).map((c) => codexName(c.commit?.author?.name || "Someone")))];
    authors.set(names.length > 3 ? names.slice(0, 3).join(", ") + " and others" : join(names), new Set(stat.keys()));
  } else {
    for (const c of commits) {
      const files = [...(c.added || []), ...(c.modified || []), ...(c.removed || [])].filter(isPage);
      if (!files.length) continue;
      const name = codexName(c.author?.name || c.committer?.name || "Someone");
      if (!authors.has(name)) authors.set(name, new Set());
      for (const f of files) authors.get(name).add(f);
    }
  }
  if (!authors.size) return;

  const lines = [];
  for (const [name, files] of authors) {
    const list = [...files].filter((f) => stat.has(f));
    if (!list.length) continue;
    const shown = await Promise.all(list.slice(0, MAX_PAGES).map(async (f) => {
      const s = stat.get(f);
      if (s.status === "removed") return `~~${pretty(f)}~~ (removed)`;
      const title = await titleOf(env, f, push.after) || pretty(f);
      const link = `[${title.replace(/[\[\]]/g, "")}](<${origin}/open?p=${encodeURIComponent(f)}>)`;
      return s.status === "added" ? `${link} (new page)` : `${link} (+${s.additions} −${s.deletions})`;
    }));
    const more = list.length > MAX_PAGES ? ` and ${list.length - MAX_PAGES} more` : "";
    lines.push(`**${name}** edited ${join(shown)}${more}`);
  }
  if (!lines.length) return;

  // A commit written by a person (not the Codex's "Docs: ..." ones) says why: show its first line.
  const why = (push.commits || []).map((c) => (c.message || "").split("\n")[0]).filter((m) => m && !/^Docs: /.test(m));
  let text = lines.join("\n");
  if (why.length) text += "\n" + why.slice(0, 3).map((m) => "> " + clip(m, 160)).join("\n");
  await post(env, env.DISCORD_NOTICES_CHANNEL, clip(text, 1990));
}

/** The page's own title, its first "# " heading, at that commit (the repository is public). */
async function titleOf(env, file, sha) {
  try {
    const r = await fetch(`https://raw.githubusercontent.com/${env.REPO}/${sha}/${file.split("/").map(encodeURIComponent).join("/")}`);
    if (!r.ok) return null;
    const m = (await r.text()).match(/^#\s+(.+)$/m);
    return m ? m[1].replace(/[*_`]/g, "").trim() : null;
  } catch { return null; }
}

// Codex commits from a Discord sign-in are "Name (Discord)"; the notice is in Discord, so drop that.
const codexName = (n) => n.replace(/\s*\(Discord\)$/, "");
const pretty = (f) => f.split("/").pop().replace(/\.md$/, "").replace(/[-_]/g, " ");
const clip = (s, n) => (s.length > n ? s.slice(0, n - 1) + "…" : s);
const join = (xs) => (xs.length < 2 ? xs.join("") : xs.slice(0, -1).join(", ") + " and " + xs[xs.length - 1]);
