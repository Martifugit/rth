// Announces a Codex release in #codex: its lines from CHANGELOG.md and a link to download it, through
// the helper (POST /announce, with the owner's agent key, helper/.dev.vars). `npm run release` runs it
// after publishing; on its own it announces a release that's out already.
//
//   node scripts/announce.mjs [version]         (default: package.json's)
//   node scripts/announce.mjs [version] --pin   pin that release's announcement, already posted, in place
//                                               of the one pinned before (new announcements pin themselves)

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

const LIMIT = 1990; // Discord takes 2000 characters a message

/**
 * The Discord message for a release. The link to the release comes first and the footer last, and they
 * always fit: when the changes don't, the list is cut at a whole line and ends "...and N more in the
 * release notes" (a long changelog once cut the message off mid-sentence, link and all).
 */
export function message(version, repo) {
  const url = `https://github.com/${repo}/releases/tag/codex-v${version}`;
  const head = `**R'th Codex ${version}** is out: [release notes and installer](<${url}>)`;
  const foot = "-# Installed Codexes update themselves: once the update has downloaded, the Codex asks you to restart it.";
  const lines = (changes(version) || "").split("\n").filter(Boolean);
  const fits = (list, more) => [head, ...list, more, foot].filter(Boolean).join("\n").length <= LIMIT;
  let shown = lines.length;
  const moreLine = (n) => (n ? `…and ${n} more in the [release notes](<${url}>).` : "");
  while (shown > 0 && !fits(lines.slice(0, shown), moreLine(lines.length - shown))) shown--;
  return [head, ...lines.slice(0, shown), moreLine(lines.length - shown), foot].filter(Boolean).join("\n");
}

const agentKey = () => { try { return fs.readFileSync(path.join(ROOT, "helper", ".dev.vars"), "utf8").match(/^AGENT_KEY=(\S+)/m)?.[1] || null; } catch { return null; } };

/** Pin a release whose announcement is out already (the latest release stays pinned in #codex). */
export async function pinAnnounced(version) {
  const key = agentKey();
  if (!key) { console.log("Not pinned: no agent key (helper/.dev.vars) on this computer."); return false; }
  const r = await fetch(HELPER + "/announce/pin", {
    method: "POST", headers: { Authorization: "Agent " + key, "Content-Type": "application/json" },
    body: JSON.stringify({ version }),
  });
  const j = await r.json().catch(() => ({}));
  if (!r.ok) { console.log(`Not pinned: ${j.error || r.status}`); return false; }
  console.log(`Pinned the ${version} announcement in #codex.`);
  return true;
}

export async function announce(version, repo) {
  const key = agentKey();
  if (!key) { console.log("Not announced in Discord: no agent key (helper/.dev.vars) on this computer."); return false; }
  const r = await fetch(HELPER + "/announce", {
    method: "POST", headers: { Authorization: "Agent " + key, "Content-Type": "application/json" },
    body: JSON.stringify({ text: message(version, repo) }),
  });
  if (!r.ok) { console.log(`Not announced in Discord: the helper said ${r.status} ${await r.text()}`); return false; }
  const j = await r.clone().json().catch(() => ({}));
  if (j.pinned && j.pinned !== true) console.log("Announced, but not pinned: " + j.pinned);
  console.log(`Announced ${version} in #codex.`);
  return true;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const pkg = JSON.parse(fs.readFileSync(path.join(ROOT, "package.json"), "utf8"));
  const version = process.argv.slice(2).find((a) => !a.startsWith("--")) || pkg.version;
  if (process.argv.includes("--pin")) { await pinAnnounced(version); process.exit(0); }
  const repo = pkg.build.publish[0].url.match(/github\.com\/([^/]+\/[^/]+)\//)[1];
  if (!changes(version)) { console.error(`CHANGELOG.md has no "## ${version}" section.`); process.exit(1); }
  await announce(version, repo);
}
