// The small "Updating" window (electron/update-splash.ps1), shown while the installer replaces the Codex.
// The Codex can't draw it itself (its files are being replaced), so it copies the script, the logo, the
// stone and Cinzel into a temporary folder and runs it on its own; the new Codex says "done" when its
// window shows, and the splash fades out.

import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..");
const DIR = path.join(os.tmpdir(), "rth-codex-update");

/** Show it; resolves once it's on screen (or after ten seconds (a cold PowerShell can be slow), so an update never waits on it). */
export function showUpdateSplash(version) {
  try {
    fs.rmSync(DIR, { recursive: true, force: true });
    fs.mkdirSync(DIR, { recursive: true });
    // Read through Electron's fs (works inside app.asar), written out where PowerShell can reach them.
    const copy = (from, name) => { try { fs.writeFileSync(path.join(DIR, name), fs.readFileSync(from)); } catch {} };
    copy(path.join(HERE, "update-splash.ps1"), "splash.ps1");
    copy(path.join(ROOT, "assets", "logo.png"), "logo.png");
    copy(path.join(ROOT, "assets", "logo.ico"), "logo.ico");
    copy(path.join(ROOT, "public", "stone.jpg"), "stone.jpg");
    const look = process.env.CODEX_LOOK || path.join(ROOT, "..", "..", "client", "Assets", "Resources");
    copy(path.join(look, "UI", "Fonts", "Cinzel.ttf"), "Cinzel.ttf");
    // It has to outlive the Codex. Node ends its own children with it on Windows, and a detached PowerShell
    // (no console) never runs at all, so a short-lived PowerShell starts the splash as its own process.
    const q = (s) => "'" + String(s).replace(/'/g, "''") + "'";
    const args = ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", path.join(DIR, "splash.ps1"), "-Dir", DIR, ...(version ? ["-Version", version] : [])].map(q).join(",");
    const child = spawn("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
      `Start-Process powershell.exe -WindowStyle Hidden -ArgumentList ${args}`], { stdio: "ignore", windowsHide: true });
    child.on("error", () => {});
    child.unref();
  } catch { return Promise.resolve(false); }
  return new Promise((resolve) => {
    const start = Date.now();
    const look = () => {
      if (fs.existsSync(path.join(DIR, "shown"))) return resolve(true);
      if (Date.now() - start > 10000) return resolve(false);
      setTimeout(look, 100);
    };
    look();
  });
}

/** The new Codex is on screen: a splash still showing fades out. */
export function updateSplashDone() {
  try { if (fs.existsSync(path.join(DIR, "shown"))) fs.writeFileSync(path.join(DIR, "done"), "done"); } catch {}
}
