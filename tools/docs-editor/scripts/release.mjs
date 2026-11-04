// Publishes a new R'th Codex (PLAN.md, step 6): builds the Windows installer (npm run dist) and puts it
// on the game repository's GitHub Releases as "R'th Codex <version>", tagged codex-v<version>, with the
// version's lines from CHANGELOG.md. Installed Codexes find it there by themselves (package.json
// "build.publish": the latest release) and update. Then it's announced in #codex (scripts/announce.mjs).
// Needs `gh` signed in with the right to make releases.
//
//   1. raise "version" in package.json   2. write its section in CHANGELOG.md   3. commit and push
//   4. npm run release
//
// It releases exactly what's on GitHub: it refuses while the Codex's folder has changes that aren't
// committed, or commits that aren't pushed, and tags the commit GitHub has (not whatever main points
// at by the time GitHub gets round to it). GitHub's passing server errors are retried.

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { changes, announce } from "./announce.mjs";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const pkg = JSON.parse(fs.readFileSync(path.join(ROOT, "package.json"), "utf8"));
const v = pkg.version, tag = `codex-v${v}`;
const repo = pkg.build.publish[0].url.match(/github\.com\/([^/]+\/[^/]+)\//)[1];
const sh = (cmd, args, opts = {}) => execFileSync(cmd, args, { cwd: ROOT, stdio: "inherit", shell: process.platform === "win32", ...opts });

try {
  execFileSync("gh", ["release", "view", tag, "--repo", repo], { stdio: "ignore", shell: process.platform === "win32" });
  console.error(`${tag} is out already: raise "version" in package.json first.`);
  process.exit(1);
} catch {}

const whatsNew = changes(v);
if (!whatsNew) { console.error(`Write what's new in ${v} first: a "## ${v}" section in CHANGELOG.md.`); process.exit(1); }

// What's released is what's on GitHub: this folder committed and pushed, nothing of it only here.
const git = (...args) => execFileSync("git", args, { cwd: ROOT, encoding: "utf8" }).trim();
const dirty = git("status", "--porcelain", "--", ".");
if (dirty) {
  console.error("The Codex's folder has changes that aren't committed. Commit and push them first, or set them aside:\n" + dirty);
  process.exit(1);
}
git("fetch", "--quiet", "origin");
const branch = git("rev-parse", "--abbrev-ref", "HEAD");
const upstream = `origin/${branch}`;
if (git("diff", "--name-only", upstream, "HEAD", "--", ".")) {
  console.error(`Commits to the Codex aren't on GitHub yet (${upstream}). Push them first, so the release is what everyone can see.`);
  process.exit(1);
}
const target = git("rev-parse", upstream);

sh("npm", ["run", "dist"]);
const exe = `RthCodexSetup-${v}.exe`;
const files = [exe, `${exe}.blockmap`, "latest.yml"].map((f) => path.join(ROOT, "dist", f));
for (const f of files) if (!fs.existsSync(f)) throw new Error("Missing " + f);

const notes = [
  `The R'th Codex ${v}, for Windows.`,
  "",
  whatsNew,
  "",
  `**Install:** download \`${exe}\` and run it. Windows may say it protected your PC (the installer isn't signed): click **More info**, then **Run anyway**. After that it updates itself.`,
  "",
  "Sign in with Discord inside it. You need the **Codex** role in the R'th Discord to edit and comment.",
].join("\n");
// GitHub's own server errors (a 500 mid-upload) pass: try again a few times. gh removes a half-made
// release itself, so each try starts clean; one that did get published is never made twice.
for (let attempt = 1; ; attempt++) {
  try {
    sh("gh", ["release", "create", tag, ...files, "--repo", repo, "--title", `R'th Codex ${v}`, "--notes", notes, "--latest", "--target", target], { shell: false });
    break;
  } catch (e) {
    let published = false;
    try { execFileSync("gh", ["release", "view", tag, "--repo", repo], { stdio: "ignore" }); published = true; } catch {}
    if (published) { console.log(`${tag} was published despite the error.`); break; }
    if (attempt >= 3) { console.error(`GitHub kept failing (${attempt} tries). Nothing was published; run it again later.`); process.exit(1); }
    console.error(`GitHub failed (try ${attempt} of 3). Trying again in 30 seconds...`);
    await new Promise((r) => setTimeout(r, 30000));
  }
}
console.log(`Published ${tag}: https://github.com/${repo}/releases/tag/${tag}`);
await announce(v, repo);
