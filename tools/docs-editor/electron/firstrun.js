// The first run (PLAN.md, step 5): where the docs are. Someone with the game's repository (the owner,
// agents) points the Codex at it; anyone else has the Codex fetch just the docs, a partial copy of the
// repository's docs/ folder (git: a blobless clone with a sparse checkout), synced from then on like
// any copy. The answer is kept in the app's settings, so this shows once.

import { app, BrowserWindow, dialog, ipcMain } from "electron";
import { execFile, spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..");
const REMOTE = process.env.CODEX_REMOTE || "https://github.com/Martifugit/rth.git";
const GIT = () => process.env.CODEX_GIT || "git";

const settingsFile = () => path.join(app.getPath("userData"), "settings.json");
export function settings() { try { return JSON.parse(fs.readFileSync(settingsFile(), "utf8")); } catch { return {}; } }
function save(s) { fs.writeFileSync(settingsFile(), JSON.stringify({ ...settings(), ...s }, null, 2)); }

const isRepo = (dir) => !!dir && fs.existsSync(path.join(dir, ".git")) && fs.existsSync(path.join(dir, "docs"));

/** Where the docs are, without asking: CODEX_REPO, the saved choice, or the repository this tool is in. */
export function knownRepo() {
  if (process.env.CODEX_FIRSTRUN === "1" && !settings().repo) return null; // checks: ask as if new
  if (process.env.CODEX_REPO) return { repo: path.resolve(process.env.CODEX_REPO), fetched: false };
  const s = settings();
  if (isRepo(s.repo)) return { repo: s.repo, fetched: !!s.fetched };
  const mine = path.resolve(ROOT, "..", "..");
  if (!app.isPackaged && isRepo(mine)) return { repo: mine, fetched: false };
  return null;
}

/** Ask (once): resolves to { repo, fetched } when the docs are in place. */
export function firstRun({ icon }) {
  return new Promise((resolve) => {
    const win = new BrowserWindow({
      width: 640, height: 560, resizable: false, title: "R'th Codex", icon,
      backgroundColor: "#0c0b09", autoHideMenuBar: true,
      webPreferences: { preload: path.join(HERE, "firstrun-preload.cjs"), contextIsolation: true, nodeIntegration: false },
    });
    const send = (type, data) => { if (!win.isDestroyed()) win.webContents.send("firstrun", { type, ...data }); };

    ipcMain.handle("firstrun:defaults", () => ({ folder: path.join(app.getPath("documents"), "R'th Codex") }));

    ipcMain.handle("firstrun:choose", async () => {
      const r = await dialog.showOpenDialog(win, { title: "The game's repository", properties: ["openDirectory"] });
      if (r.canceled || !r.filePaths[0]) return null;
      const dir = r.filePaths[0];
      if (!isRepo(dir)) return { error: "That folder isn't a copy of the game's repository (it needs .git and docs/ in it)." };
      finish({ repo: dir, fetched: false });
      return { ok: true };
    });

    ipcMain.handle("firstrun:pickParent", async () => {
      const r = await dialog.showOpenDialog(win, { title: "Where to keep the docs", properties: ["openDirectory", "createDirectory"] });
      return r.canceled ? null : r.filePaths[0];
    });

    ipcMain.handle("firstrun:fetch", async (e, folder) => {
      folder = path.resolve(String(folder || ""));
      if (fs.existsSync(folder) && fs.readdirSync(folder).length) {
        if (isRepo(folder)) { finish({ repo: folder, fetched: true }); return { ok: true }; }
        return { error: "That folder isn't empty. Choose an empty or new folder." };
      }
      const version = await run(["--version"]);
      if (!version.ok) return { error: "Git isn't on this computer yet. Install Git for Windows (git-scm.com), then try again. (The installed Codex will bring its own.)" };
      fs.mkdirSync(path.dirname(folder), { recursive: true });
      send("progress", { text: "Fetching the docs…" });
      const c = await run(["clone", "--filter=blob:none", "--sparse", "--progress", REMOTE, folder], (line) => send("progress", { text: line }));
      if (!c.ok) return { error: "Couldn't fetch the docs: " + c.err.trim().split("\n").pop() };
      send("progress", { text: "Keeping only the docs…" });
      const s = await run(["-C", folder, "sparse-checkout", "set", "docs"]);
      if (!s.ok) return { error: "Couldn't narrow the copy to the docs: " + s.err.trim().split("\n").pop() };
      finish({ repo: folder, fetched: true });
      return { ok: true };
    });

    function finish(v) {
      save(v);
      for (const ch of ["firstrun:defaults", "firstrun:choose", "firstrun:pickParent", "firstrun:fetch"]) ipcMain.removeHandler(ch);
      resolve(v);
      setTimeout(() => { if (!win.isDestroyed()) win.close(); }, 50);
    }
    win.on("closed", () => { if (!knownRepo()) app.quit(); });
    win.loadFile(path.join(HERE, "firstrun.html"));
  });
}

function run(args, onLine) {
  return new Promise((resolve) => {
    const p = spawn(GIT(), args, { windowsHide: true, env: { ...process.env, GIT_TERMINAL_PROMPT: "0" } });
    let out = "", err = "";
    p.stdout.on("data", (d) => { out += d; });
    p.stderr.on("data", (d) => {
      err += d;
      if (onLine) for (const l of String(d).split(/[\r\n]+/)) if (l.trim()) onLine(l.trim());
    });
    p.on("error", (e) => resolve({ ok: false, out, err: e.message }));
    p.on("close", (code) => resolve({ ok: code === 0, out, err }));
  });
}
