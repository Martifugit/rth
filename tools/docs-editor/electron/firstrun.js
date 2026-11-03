// The first run (PLAN.md, step 5): getting the docs. Nobody is asked where to keep them: the Codex
// fetches just the docs (git: a blobless clone with a sparse checkout of docs/) into its own folder
// (userData/Docs), out of the way, and uninstalling removes them with everything else
// (scripts/installer.nsh). The window shows a progress bar while that happens. Someone who has the game's
// repository (the owner, agents) can point the Codex at it instead, from a small link. The answer is
// kept in the app's settings, so this shows once.

import { app, BrowserWindow, dialog, ipcMain } from "electron";
import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..");
const REMOTE = process.env.CODEX_REMOTE || "https://github.com/Martifugit/rth.git";
const BRANCH = process.env.CODEX_BRANCH || "main";
const GIT = () => process.env.CODEX_GIT || "git";

const settingsFile = () => path.join(app.getPath("userData"), "settings.json");
export function settings() { try { return JSON.parse(fs.readFileSync(settingsFile(), "utf8")); } catch { return {}; } }
function save(s) { fs.writeFileSync(settingsFile(), JSON.stringify({ ...settings(), ...s }, null, 2)); }

const isRepo = (dir) => !!dir && fs.existsSync(path.join(dir, ".git")) && fs.existsSync(path.join(dir, "docs"));
/** Where a fetched copy goes: the Codex's own folder. */
const docsFolder = () => path.join(app.getPath("userData"), "Docs");

/**
 * A copy the Codex fetched, wherever it is (copies from before 0.1.4 are where the person chose): marked
 * inside its .git folder, and named in fetched-folder.txt, so uninstalling can find and remove it.
 */
export function markFetched(repo) {
  try {
    fs.writeFileSync(path.join(repo, ".git", "codex-fetched"), "Fetched by the R'th Codex; removed when it's uninstalled.\n");
    fs.writeFileSync(path.join(app.getPath("userData"), "fetched-folder.txt"), repo);
  } catch {}
}

/** Where the docs are, without asking: CODEX_REPO, the saved choice, or the repository this tool is in. */
export function knownRepo() {
  if (process.env.CODEX_FIRSTRUN === "1" && !settings().repo) return null; // checks: ask as if new
  if (process.env.CODEX_REPO) return { repo: path.resolve(process.env.CODEX_REPO), fetched: false };
  const s = settings();
  if (isRepo(s.repo)) { if (s.fetched) markFetched(s.repo); return { repo: s.repo, fetched: !!s.fetched }; }
  const mine = path.resolve(ROOT, "..", "..");
  if (!app.isPackaged && isRepo(mine)) return { repo: mine, fetched: false };
  return null;
}

/** Get the docs (once): resolves to { repo, fetched } when they're in place. */
export function firstRun({ icon }) {
  return new Promise((resolve) => {
    const win = new BrowserWindow({
      width: 560, height: 400, resizable: false, title: "R'th Codex", icon,
      backgroundColor: "#0c0b09", autoHideMenuBar: true,
      webPreferences: { preload: path.join(HERE, "firstrun-preload.cjs"), contextIsolation: true, nodeIntegration: false },
    });
    const send = (type, data) => { if (!win.isDestroyed()) win.webContents.send("firstrun", { type, ...data }); };
    let busy = false;

    // The game's repository, already on this computer (the owner, agents).
    ipcMain.handle("firstrun:choose", async () => {
      if (busy) return null;
      const r = await dialog.showOpenDialog(win, { title: "The game's repository", properties: ["openDirectory"] });
      if (r.canceled || !r.filePaths[0]) return null;
      const dir = r.filePaths[0];
      if (!isRepo(dir)) return { error: "That folder isn't a copy of the game's repository (it needs .git and docs/ in it)." };
      finish({ repo: dir, fetched: false });
      return { ok: true };
    });

    ipcMain.handle("firstrun:fetch", async () => {
      if (busy) return null;
      busy = true;
      try { return await fetchDocs(); } finally { busy = false; }
    });

    async function fetchDocs() {
      const folder = docsFolder();
      if (isRepo(folder)) { finish({ repo: folder, fetched: true }); return { ok: true }; } // fetched before
      fs.rmSync(folder, { recursive: true, force: true }); // a fetch that stopped halfway: start again
      const version = await run(["--version"]);
      if (!version.ok) return { error: "Git isn't on this computer. Install Git for Windows (git-scm.com), then try again." };
      fs.mkdirSync(path.dirname(folder), { recursive: true });

      // Progress: git reports downloading, then resolving, then (checking out the docs) unpacking files.
      // Each phase fills its share of one bar.
      const PHASES = [
        { re: /^Receiving objects:\s+(\d+)%/, from: 0, to: 62, text: "Downloading the docs" },
        { re: /^Resolving deltas:\s+(\d+)%/, from: 62, to: 75, text: "Downloading the docs" },
        { re: /^Updating files:\s+(\d+)%/, from: 80, to: 100, text: "Unpacking the docs" },
      ];
      let shown = -1;
      const progress = (line) => {
        for (const p of PHASES) {
          const m = p.re.exec(line);
          if (!m) continue;
          const pc = Math.round(p.from + (p.to - p.from) * Number(m[1]) / 100);
          if (pc > shown) { shown = pc; send("progress", { percent: pc, text: p.text }); }
        }
      };
      send("progress", { percent: 0, text: "Connecting" });
      const c = await run(["clone", "--filter=blob:none", "--no-checkout", "--progress", "--branch", BRANCH, REMOTE, folder], progress);
      if (!c.ok) return { error: "Couldn't download the docs: " + lastLine(c.err) + " Check the internet connection, then try again." };
      send("progress", { percent: Math.max(shown, 75), text: "Unpacking the docs" });
      const s = await run(["-C", folder, "sparse-checkout", "set", "docs"]);
      if (!s.ok) return { error: "Couldn't set up the docs: " + lastLine(s.err) };
      const k = await run(["-C", folder, "checkout", "--progress", BRANCH], progress);
      if (!k.ok) return { error: "Couldn't unpack the docs: " + lastLine(k.err) };
      send("progress", { percent: 100, text: "Ready" });
      markFetched(folder);
      finish({ repo: folder, fetched: true });
      return { ok: true };
    }

    function finish(v) {
      save(v);
      for (const ch of ["firstrun:choose", "firstrun:fetch"]) ipcMain.removeHandler(ch);
      resolve(v);
      setTimeout(() => { if (!win.isDestroyed()) win.close(); }, 400);
    }
    win.on("closed", () => { if (!knownRepo()) app.quit(); });
    win.loadFile(path.join(HERE, "firstrun.html"));
  });
}

const lastLine = (s) => String(s || "").trim().split("\n").pop();

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
