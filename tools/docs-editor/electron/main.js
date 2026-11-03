// The Codex as a desktop app: the engine (server.js) runs inside the app, on 127.0.0.1 and a free
// port only the app knows, and the window shows the Codex page from it. Nothing else changes: the
// same engine and page run in a browser with `npm start`.

import { app, BrowserWindow, Menu, shell } from "electron";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { knownRepo, firstRun } from "./firstrun.js";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..");
const ICON = path.join(ROOT, "assets", "logo.ico"); // the R'th logo: window, taskbar and shortcut

// For checks: another settings folder (CODEX_USERDATA), so a check never touches the real one.
if (process.env.CODEX_USERDATA) app.setPath("userData", path.resolve(process.env.CODEX_USERDATA));

// Its own name in Windows, so the taskbar shows the Codex (and its logo), not "Electron".
app.setAppUserModelId("rth.codex");

// One Codex at a time: opening it again brings the open one forward.
if (!app.requestSingleInstanceLock()) app.quit();

// Links that open a page in the Codex (rth-codex://open?p=docs/...&h=heading): the helper's /open
// page hands Discord's https links over to these (PLAN.md, "The helper"). Registered for this user
// each time the Codex starts, so it always points at the copy that's running.
const PROTOCOL = "rth-codex";
// Unpackaged, the link starts the branded copy electron/launch.js makes (Codex.exe, "R'th Codex"), so
// the browser asks to open "R'th Codex", not "Electron", however this copy was started.
const BRANDED = path.join(ROOT, "node_modules", ".codex-app", "Codex.exe");
if (app.isPackaged) app.setAsDefaultProtocolClient(PROTOCOL);
else app.setAsDefaultProtocolClient(PROTOCOL, process.platform === "win32" && fs.existsSync(BRANDED) ? BRANDED : process.execPath, [ROOT]);
const linkIn = (argv) => argv.find((a) => typeof a === "string" && a.startsWith(PROTOCOL + "://")) || null;
let pendingLink = linkIn(process.argv);

/** Open the page a link names, in the window (or once it's ready). */
function openLink(link) {
  let u;
  try { u = new URL(link); } catch { return; }
  const p = u.searchParams.get("p"), hd = u.searchParams.get("h") || "";
  if (!p || !/^(docs\/.+|[^/]+)\.md$/.test(p) || p.includes("..")) return;
  if (!win || !origin) { pendingLink = link; return; }
  if (win.isMinimized()) win.restore();
  win.focus();
  win.webContents.executeJavaScript(`window.__codex && window.__codex.openDoc(${JSON.stringify(p)}, { hash: ${JSON.stringify(hd)} })`).catch(() => {});
}

let win = null, origin = null;
const boundsFile = () => path.join(app.getPath("userData"), "window.json");

function savedBounds() {
  try { return JSON.parse(fs.readFileSync(boundsFile(), "utf8")); } catch { return null; }
}
function saveBounds() {
  if (!win || win.isDestroyed()) return;
  try { fs.writeFileSync(boundsFile(), JSON.stringify({ ...win.getNormalBounds(), maximized: win.isMaximized() })); } catch {}
}

/** Rebuild the page (public/app.js) if its source is newer, so a shortcut always opens the current Codex. */
async function buildIfStale() {
  if (app.isPackaged) return; // the installed app is built already
  const out = path.join(ROOT, "public", "app.js");
  const built = fs.existsSync(out) ? fs.statSync(out).mtimeMs : 0;
  const src = path.join(ROOT, "src");
  const newest = Math.max(...fs.readdirSync(src).map((f) => fs.statSync(path.join(src, f)).mtimeMs));
  if (newest <= built) return;
  const esbuild = await import("esbuild");
  await esbuild.build({ entryPoints: [path.join(src, "app.js")], bundle: true, format: "esm", minify: true, sourcemap: true, outfile: out, logLevel: "warning" });
}

async function open() {
  await buildIfStale();
  // The installed app brings the game's look (fonts, frames, icons) and its own git (MinGit), and keeps
  // the words you add to the dictionary in its own folder.
  if (app.isPackaged) {
    const res = process.resourcesPath;
    process.env.CODEX_LOOK ||= path.join(res, "look");
    const git = path.join(res, "git", "cmd", "git.exe");
    if (fs.existsSync(git)) process.env.CODEX_GIT ||= git;
    process.env.CODEX_WORDS ||= path.join(app.getPath("userData"), "words.txt");
  }
  // Where the docs are: known, or asked once (electron/firstrun.js). A copy the Codex fetched belongs
  // to a contributor, who edits once signed in with Discord with the Codex role.
  const where = knownRepo() || (await firstRun({ icon: fs.existsSync(ICON) ? ICON : undefined }));
  process.env.CODEX_REPO = where.repo;
  if (where.fetched) process.env.CODEX_CONTRIBUTOR = "1";
  // What stays on this computer only (the AI tab's conversations) goes in the app's own folder.
  process.env.CODEX_DATA ||= app.getPath("userData");
  const { startServer } = await import("../server.js");
  const port = await startServer(0, { autoSync: true });
  origin = `http://127.0.0.1:${port}`;

  const b = savedBounds();
  win = new BrowserWindow({
    width: b?.width || 1500, height: b?.height || 920, x: b?.x, y: b?.y,
    minWidth: 900, minHeight: 600,
    title: "R'th Codex", icon: fs.existsSync(ICON) ? ICON : undefined,
    backgroundColor: "#0c0b09", show: false,
    // The Codex draws its own title bar (search, New page, Sync, help); Windows keeps its buttons in
    // the corner over it, as in VS Code.
    titleBarStyle: "hidden",
    titleBarOverlay: { color: "#0c0b09", symbolColor: "#d9c9a6", height: 38 },
    webPreferences: { contextIsolation: true, nodeIntegration: false, spellcheck: false },
  });
  if (b?.maximized) win.maximize();
  win.setMenuBarVisibility(false);
  win.once("ready-to-show", () => win.show());
  win.on("resize", saveBounds);
  win.on("move", saveBounds);

  // Links out of the Codex open in the computer's browser; the window never leaves the Codex.
  win.webContents.setWindowOpenHandler(({ url }) => { if (/^https?:/.test(url)) shell.openExternal(url); return { action: "deny" }; });
  win.webContents.on("will-navigate", (e, url) => { if (!url.startsWith(origin)) { e.preventDefault(); if (/^https?:/.test(url)) shell.openExternal(url); } });
  // Closing: the page saves what's still being typed first, then the window closes. However the close
  // starts (the X, Alt+F4, the page itself), it's held until that save is done (or 4 s have passed).
  let flushed = false, flushing = false;
  const flushThenClose = () => {
    if (flushing) return;
    flushing = true;
    Promise.race([
      win.webContents.executeJavaScript("window.__codex && window.__codex.flush ? window.__codex.flush() : true"),
      new Promise((r) => setTimeout(r, 4000)),
    ]).then((v) => process.env.CODEX_DEBUG && console.log("flush ->", v), (err) => process.env.CODEX_DEBUG && console.log("flush failed", err.message))
      // Then send what was saved here before going (a moment at most; it goes next time otherwise).
      .then(() => Promise.race([fetch(origin + "/api/sync?why=close", { method: "POST" }), new Promise((r) => setTimeout(r, 8000))]))
      .catch(() => {})
      .finally(() => { flushed = true; if (!win.isDestroyed()) win.close(); });
  };
  win.on("close", (e) => {
    if (process.env.CODEX_DEBUG) console.log("close", { flushed, flushing });
    saveBounds();
    if (flushed) return;
    e.preventDefault();
    flushThenClose();
  });
  // The page's own "leave with unsaved changes?" check: not a question here; hold the close and save.
  win.webContents.on("will-prevent-unload", (e) => {
    if (process.env.CODEX_DEBUG) console.log("will-prevent-unload", { flushed });
    if (flushed) e.preventDefault(); // saved already: let it go
    else flushThenClose();           // not yet: this close is cancelled; flushThenClose closes again
  });
  // For checks: CODEX_DEBUG lets the page ask the app to close as the X button does (a page's own
  // window.close() skips the app's close handling, so it can't stand in for the X).
  if (process.env.CODEX_DEBUG) win.on("page-title-updated", (e, title) => { if (title === "codex:close") win.close(); });

  const first = pendingLink ? new URL(pendingLink).searchParams.get("p") : null;
  pendingLink = null;
  await win.loadURL(origin + "/" + (first && /\.md$/.test(first) && !first.includes("..") ? "?p=" + encodeURI(first) : ""));
  if (app.isPackaged) watchForUpdates();
}

/**
 * Updates (PLAN.md, step 6): the installed Codex looks for a newer release on GitHub (the repository's
 * latest release; see package.json's "build.publish"), downloads it quietly and installs it when you
 * close the Codex. The page says when one is ready.
 */
async function watchForUpdates() {
  try {
    const { default: updater } = await import("electron-updater");
    const { autoUpdater } = updater;
    autoUpdater.autoDownload = true;
    autoUpdater.autoInstallOnAppQuit = true;
    autoUpdater.on("update-downloaded", (info) => {
      if (win && !win.isDestroyed()) win.webContents.executeJavaScript(`window.__codex && window.__codex.toast && window.__codex.toast(${JSON.stringify(`R'th Codex ${info.version} is ready: it installs when you close the Codex`)}, "good")`).catch(() => {});
    });
    autoUpdater.on("error", (e) => { if (process.env.CODEX_DEBUG) console.log("update:", e.message); });
    const check = () => autoUpdater.checkForUpdates().catch(() => {});
    check();
    setInterval(check, 6 * 60 * 60 * 1000);
  } catch (e) { if (process.env.CODEX_DEBUG) console.log("updates off:", e.message); }
}

// No visible menu bar, but the usual keys still work (reload, zoom, developer tools).
Menu.setApplicationMenu(Menu.buildFromTemplate([
  { label: "View", submenu: [
    { role: "reload" }, { role: "forceReload" }, { role: "toggleDevTools" }, { type: "separator" },
    { role: "resetZoom" }, { role: "zoomIn" }, { role: "zoomIn", accelerator: "CommandOrControl+=", visible: false }, { role: "zoomOut" },
    { type: "separator" }, { role: "togglefullscreen" },
  ] },
  // No Edit menu: its Ctrl+Z would take undo away from the editor's own. (A Mac build needs one, for
  // copy and paste; that comes with shipping.)
]));

app.on("second-instance", (event, argv) => {
  const link = linkIn(argv);
  if (link) return openLink(link);
  if (win) { if (win.isMinimized()) win.restore(); win.focus(); }
});
app.on("open-url", (event, link) => { event.preventDefault(); openLink(link); }); // macOS
app.whenReady().then(open).catch((e) => { console.error(e); app.quit(); });
// (Not while the first run hands over to the Codex window.)
app.on("window-all-closed", () => { if (win) app.quit(); });
