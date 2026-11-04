// Recent changes: who changed which page, and where in it, read from git. Every sync commit carries the
// person's name (their Discord name, from src/sync.js) and says "(with AI)" when the AI made it.
// server.js runs `git log` with git's own Markdown heading detection (a hunk header names the heading
// above it) and hands the text to parseLog; entries() folds it into one line per person, page and day.

export const LOG_FORMAT = "%x1e%H%x1f%an%x1f%aI%x1f%s%x1f%b%x1d";

/** `git log -p -U0 --format=LOG_FORMAT` output: [{ sha, author, date, subject, ai, files: [{ path, status, sections }] }]. */
export function parseLog(out) {
  const commits = [];
  for (const chunk of out.split("\x1e")) {
    if (!chunk.trim()) continue;
    const end = chunk.indexOf("\x1d");
    const [sha, author, date, subject, body] = (end < 0 ? chunk : chunk.slice(0, end)).split("\x1f");
    const files = [];
    let file = null, hunk = null;
    // A hunk's section: the heading above it ("@@ -39,3 +39,3 @@ ## 1. The roster"), unless the change
    // starts on a heading, when it's that section (by its new name if the heading was renamed).
    const settle = () => {
      if (!hunk) return;
      const own = hunk.startsOnHeading ? (hunk.firstAdded ?? hunk.firstRemoved) : null;
      const section = own || hunk.heading;
      if (section) add(file, section);
      hunk = null;
    };
    for (const line of (end < 0 ? "" : chunk.slice(end + 1)).split("\n")) {
      const d = line.match(/^diff --git a\/(.+?) b\/(.+)$/);
      if (d) { settle(); file = { path: d[2], status: "changed", sections: [] }; files.push(file); continue; }
      if (!file) continue;
      if (line.startsWith("new file mode")) file.status = "added";
      else if (line.startsWith("deleted file mode")) file.status = "deleted";
      else if (line.startsWith("@@ ")) { settle(); hunk = { heading: headingText(line.replace(/^@@[^@]*@@\s?/, "")), started: false }; }
      else if (hunk && /^[+-]/.test(line) && !/^(\+\+\+|---) /.test(line)) {
        const hd = headingText(line.slice(1));
        if (!hunk.started) { hunk.started = true; hunk.startsOnHeading = !!hd; }
        if (line[0] === "+" && hunk.firstAdded === undefined) hunk.firstAdded = hd;
        if (line[0] === "-" && hunk.firstRemoved === undefined) hunk.firstRemoved = hd;
      }
    }
    settle();
    for (const f of files) if (f.status !== "changed") f.sections = [];
    commits.push({
      sha, author: cleanName(author), date, subject: (subject || "").replace(/\s*\(with AI\)\s*$/, ""),
      ai: /\(with AI\)\s*$/.test(subject || "") || /co-authored-by:\s*claude/i.test(body || ""), files,
    });
  }
  return commits;
}

function add(file, heading) { if (!file.sections.includes(heading)) file.sections.push(heading); }

/** A Markdown heading line's text (H2 and deeper: the H1 is the page's title), or null. */
function headingText(line) {
  const m = line.match(/^(#{2,6})\s+(.*?)\s*#*\s*$/);
  return m ? m[2].replace(/!\[[^\]]*\]\([^)]*\)/g, "").replace(/\[([^\]]*)\]\([^)]*\)/g, "$1").replace(/[*_`]+/g, "").trim() || null : null;
}

const cleanName = (n) => (n || "Someone").replace(/\s*\(Discord\)\s*$/, "");

const localDay = (iso) => { const d = new Date(iso); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; };

/**
 * One entry per person, page and day (the Codex syncs often, so one sitting is many commits), newest first:
 * [{ author, mine, ai, date, day, path, title, status, sections, exists }].
 * `titles`: page -> title for pages that exist now; `me`: the names that are this computer's user.
 */
export function entries(commits, { titles = new Map(), me = [], limit = 200 } = {}) {
  const mine = new Set(me.filter(Boolean).map((n) => cleanName(n).toLowerCase()));
  const byKey = new Map(), out = [];
  for (const c of commits) {
    for (const f of c.files) {
      if (!f.path.endsWith(".md")) continue;
      const day = localDay(c.date), key = c.author + "\n" + f.path + "\n" + day;
      let e = byKey.get(key);
      if (!e) {
        if (out.length >= limit) continue;
        // Newest first, so the first commit seen is the latest: it says whether the page stands now.
        e = { author: c.author, mine: mine.has(c.author.toLowerCase()), ai: false, date: c.date, day, path: f.path,
          title: titles.get(f.path) || f.path.split("/").pop().replace(/\.md$/, ""), status: f.status, sections: [], exists: titles.has(f.path) };
        byKey.set(key, e); out.push(e);
      } else if (f.status === "added" && e.status !== "deleted") e.status = "added";
      if (c.ai) e.ai = true;
      for (const s of f.sections) if (!e.sections.includes(s)) e.sections.push(s);
    }
  }
  for (const e of out) if (e.status !== "changed") e.sections = [];
  return out;
}
