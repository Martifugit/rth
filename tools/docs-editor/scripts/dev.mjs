// The Codex from this folder, beside the installed one, for trying changes before a release:
//
//   npm run dev              the Codex as it is in this folder, signed in as you
//   npm run dev -- --people  with pretend people in the room, to look at the header (CODEX_DEMO_PEOPLE)
//
// It keeps its own settings folder (%LOCALAPPDATA%\rth-codex-dev), so the installed Codex's are never
// touched, and copies your sign-in and settings from the installed one the first time. It doesn't take
// over rth-codex:// links while an installed Codex is there (electron/main.js). It works on the same
// docs as the installed one and shares them the same way: it's a real Codex, not a sandbox.

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const local = process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
const roaming = process.env.APPDATA || path.join(os.homedir(), "AppData", "Roaming");
const dev = path.join(local, "rth-codex-dev");
const installed = path.join(roaming, "rth-docs-editor");

// The first time: the installed Codex's sign-in (and the key that unlocks it), settings and AI setup.
fs.mkdirSync(dev, { recursive: true });
if (!fs.existsSync(path.join(dev, "account.bin")) && fs.existsSync(installed)) {
  for (const f of ["Local State", "account.bin", "openrouter.bin", "ai.json", "settings.json", "window.json"]) {
    try { fs.copyFileSync(path.join(installed, f), path.join(dev, f)); } catch {}
  }
  console.log("Signed in as in the installed Codex (copied once into " + dev + ").");
}

const env = { ...process.env, CODEX_USERDATA: dev };
if (process.argv.includes("--people")) env.CODEX_DEMO_PEOPLE = "1";
const child = spawn(process.execPath, [path.join(ROOT, "electron", "launch.js")], { cwd: ROOT, env, stdio: "inherit" });
child.on("exit", (code) => process.exit(code ?? 0));
