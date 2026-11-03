// Publishes a new R'th Codex (PLAN.md, step 6): builds the Windows installer (npm run dist) and puts it
// on the game repository's GitHub Releases as "R'th Codex <version>", tagged codex-v<version>. Installed
// Codexes find it there by themselves (package.json "build.publish": the latest release) and update.
// Needs `gh` signed in with the right to make releases.
//
//   1. raise "version" in package.json      2. npm run release

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

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

sh("npm", ["run", "dist"]);
const exe = `RthCodexSetup-${v}.exe`;
const files = [exe, `${exe}.blockmap`, "latest.yml"].map((f) => path.join(ROOT, "dist", f));
for (const f of files) if (!fs.existsSync(f)) throw new Error("Missing " + f);

const notes = [
  `The R'th Codex ${v}, for Windows.`,
  "",
  `**Install:** download \`${exe}\` and run it. Windows may say it protected your PC (the installer isn't signed): click **More info**, then **Run anyway**. After that it updates itself.`,
  "",
  "Sign in with Discord inside it. You need the **Codex** role in the R'th Discord to edit and comment.",
].join("\n");
sh("gh", ["release", "create", tag, ...files, "--repo", repo, "--title", `R'th Codex ${v}`, "--notes", notes, "--latest"], { shell: false });
console.log(`Published ${tag}: https://github.com/${repo}/releases/tag/${tag}`);
