// Announces a Codex release in #codex: its lines from CHANGELOG.md and a link to download it, through
// the helper (POST /announce, with the owner's agent key, helper/.dev.vars). `npm run release` runs it
// after publishing; on its own it announces a release that's out already.
//
//   node scripts/announce.mjs [version]     (default: package.json's)

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const HELPER = (process.env.CODEX_HELPER || "https://rth-codex.rthapp.workers.dev").replace(/\/+$/, "");

/** The version's lines in CHANGELOG.md (its "## version" section), or null. */
export function changes(version) {
  const text = fs.readFileSync(path.join(ROOT, "CHANGELOG.md"), "utf8").replace(/\r\n/g, "\n");
  const m = text.match(new RegExp(`^## ${version.replace(/\./g, "\\.")}\\s*\\n([\\s\\S]*?)(?=^## |(?![\\s\\S]))`, "m"));
  return m ? m[1].trim().replace(/\n {2,}/g, " ") || null : null; // a wrapped line is one line in Discord
}

/** The Discord message for a release. */
export function message(version, repo) {
  const url = `https://github.com/${repo}/releases/tag/codex-v${version}`;
  return [
    `**R'th Codex ${version}** is out.`,
    changes(version) || "",
    `-# Installed Codexes update themselves (a blue Update button appears). New here? [Download the installer](<${url}>).`,
  ].filter(Boolean).join("\n");
}

export async function announce(version, repo) {
  let key = null;
  try { key = fs.readFileSync(path.join(ROOT, "helper", ".dev.vars"), "utf8").match(/^AGENT_KEY=(\S+)/m)?.[1] || null; } catch {}
  if (!key) { console.log("Not announced in Discord: no agent key (helper/.dev.vars) on this computer."); return false; }
  const r = await fetch(HELPER + "/announce", {
    method: "POST", headers: { Authorization: "Agent " + key, "Content-Type": "application/json" },
    body: JSON.stringify({ text: message(version, repo) }),
  });
  if (!r.ok) { console.log(`Not announced in Discord: the helper said ${r.status} ${await r.text()}`); return false; }
  console.log(`Announced ${version} in #codex.`);
  return true;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const pkg = JSON.parse(fs.readFileSync(path.join(ROOT, "package.json"), "utf8"));
  const version = process.argv[2] || pkg.version;
  const repo = pkg.build.publish[0].url.match(/github\.com\/([^/]+\/[^/]+)\//)[1];
  if (!changes(version)) { console.error(`CHANGELOG.md has no "## ${version}" section.`); process.exit(1); }
  await announce(version, repo);
}
