// Fetches MinGit (the official small Git for Windows made for bundling, from git-for-windows' GitHub
// releases) into build/mingit, for the installer to carry (package.json "build.extraResources"), so
// contributors never install git. GPL: its licence files come with it. Kept until a newer one is out.
//
//   node scripts/fetch-mingit.mjs

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const OUT = path.join(ROOT, "build", "mingit");
const STAMP = path.join(OUT, ".version");

const rel = await (await fetch("https://api.github.com/repos/git-for-windows/git/releases/latest", { headers: { "User-Agent": "rth-codex-build" } })).json();
const asset = (rel.assets || []).find((a) => /^MinGit-[\d.]+-64-bit\.zip$/.test(a.name));
if (!asset) throw new Error("No MinGit 64-bit zip in git-for-windows' latest release.");

if (fs.existsSync(STAMP) && fs.readFileSync(STAMP, "utf8") === asset.name && fs.existsSync(path.join(OUT, "cmd", "git.exe"))) {
  console.log(`MinGit: ${asset.name} already here.`);
  process.exit(0);
}

console.log(`MinGit: fetching ${asset.name} (${(asset.size / 1e6).toFixed(1)} MB) from ${asset.browser_download_url}`);
const zip = path.join(ROOT, "build", asset.name);
fs.mkdirSync(path.dirname(zip), { recursive: true });
fs.writeFileSync(zip, Buffer.from(await (await fetch(asset.browser_download_url)).arrayBuffer()));
fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });
// Windows' own tar reads zips (Git Bash's GNU tar, often first on the PATH, doesn't).
const tar = process.platform === "win32" ? path.join(process.env.SystemRoot || "C:\\Windows", "System32", "tar.exe") : "tar";
execFileSync(tar, ["-xf", zip, "-C", OUT]);
fs.rmSync(zip);
fs.writeFileSync(STAMP, asset.name);
console.log(`MinGit: ${execFileSync(path.join(OUT, "cmd", "git.exe"), ["--version"]).toString().trim()}, in build/mingit.`);
