// The Codex as a desktop app: the engine (server.js) runs inside the app, on 127.0.0.1 and a free
// port only the app knows, and the window shows the Codex page from it. Nothing else changes: the
// same engine and page run in a browser with `npm start`.

import { app, BrowserWindow, Menu, shell } from "electron";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { knownRepo, firstRun } from "./firstrun.js";
import { showUpdateSplash, updateSplashDone } from "./update-splash.js";

// Installed, or run from this folder (electron/launch.js)? Not app.isPackaged alone: Electron decides that by
// the exe's name, and the dev copy is renamed Codex.exe, so it looked installed (no look, update checks).
// Run from a folder, Electron's default app loads it and sets process.defaultApp.
const INSTALLED = app.isPackaged && !process.defaultApp;

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..");
const ICON = path.join(ROOT, "assets", "logo.ico"); // the R'th logo: window, taskbar and shortcut

// For checks: another settings folder (CODEX_USERDATA), so a check never touches the real one.
if (process.env.CODEX_USERDATA) app.setPath("userData", path.resolve(process.env.CODEX_USERDATA));

// Its own name in Windows, so the taskbar shows the Codex (and its logo), not "Electron".
// CODEX_APPID: a check beside an open Codex gets its own taskbar button instead of joining its group.
app.setAppUserModelId(process.env.CODEX_APPID || "rth.codex");

// One Codex at a time: opening it again brings the open one forward.
if (!app.requestSingleInstanceLock()) app.quit();

// Links that open a page in the Codex (rth-codex://open?p=docs/...&h=heading): the helper's /open
// page hands Discord's https links over to these (PLAN.md, "The helper"). Registered for this user
// each time the Codex starts, so it always points at the copy that's running.
const PROTOCOL = "rth-codex";
// Unpackaged, the link starts the branded copy electron/launch.js makes (Codex.exe, "R'th Codex"), so
// the browser asks to open "R'th Codex", not "Electron", however this copy was started.
const BRANDED = path.join(ROOT, "node_modules", ".codex-app", "Codex.exe");
// Run from this folder while an installed Codex is on the computer (npm run dev), the links stay the
// installed one's: a link from Discord shouldn't open the copy being tried out.
const INSTALLED_EXE = path.join(process.env.LOCALAPPDATA || "", "Programs", "rth-docs-editor", "R'th Codex.exe");
if (INSTALLED) app.setAsDefaultProtocolClient(PROTOCOL);
else if (!fs.existsSync(INSTALLED_EXE)) app.setAsDefaultProtocolClient(PROTOCOL, process.platform === "win32" && fs.existsSync(BRANDED) ? BRANDED : process.execPath, [ROOT]);
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
let flushed = false; // what was being typed is saved and sent: the window may close

/**
 * Before the Codex goes (closing, or updating): the page saves what's still being typed (4 s at most),
 * then what was saved here is sent (8 s at most; it goes next time otherwise).
 */
function saveAndSync() {
  return Promise.race([
    win.webContents.executeJavaScript("window.__codex && window.__codex.flush ? window.__codex.flush() : true"),
    new Promise((r) => setTimeout(r, 4000)),
  ]).then((v) => process.env.CODEX_DEBUG && console.log("flush ->", v), (err) => process.env.CODEX_DEBUG && console.log("flush failed", err.message))
    .then(() => Promise.race([fetch(origin + "/api/sync?why=close", { method: "POST" }), new Promise((r) => setTimeout(r, 8000))]))
    .catch(() => {});
}
const boundsFile = () => path.join(app.getPath("userData"), "window.json");

function savedBounds() {
  try { return JSON.parse(fs.readFileSync(boundsFile(), "utf8")); } catch { return null; }
}
function saveBounds() {
  if (!win || win.isDestroyed()) return;
  try { fs.writeFileSync(boundsFile(), JSON.stringify({ ...win.getNormalBounds(), maximized: win.isMaximized() })); } catch { /* the window's place is only a convenience */ }
}

/** Rebuild the page (public/app.js) if its source is newer, so a shortcut always opens the current Codex. */
async function buildIfStale() {
  if (INSTALLED) return; // the installed app is built already
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
  if (INSTALLED) {
    const res = process.resourcesPath;
    process.env.CODEX_LOOK ||= path.join(res, "look");
    const git = path.join(res, "git", "cmd", "git.exe");
    if (fs.existsSync(git)) process.env.CODEX_GIT ||= git;
    // Where Codexes before 0.3.2 kept their own word list: its words move into the shared one once.
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
    backgroundColor: "#040404", show: false,
    // Windows 11 draws a thin light border round a window that isn't maximised: a silver frame on Codex's
    // near-black. Its own colour makes it vanish (Electron keeps it applied as the window is activated).
    accentColor: "#040404",
    // The Codex draws its own title bar (search, New page, Sync, help); Windows keeps its buttons in
    // the corner over it, as in VS Code.
    titleBarStyle: "hidden",
    titleBarOverlay: { color: "#040404", symbolColor: "#aeb2b7", height: 38 }, // silver, as the rest of the title bar
    webPreferences: { contextIsolation: true, nodeIntegration: false, spellcheck: false },
  });
  if (b?.maximized) win.maximize();
  win.setMenuBarVisibility(false);
  win.once("ready-to-show", () => { win.show(); updateSplashDone(); }); // an "Updating" window fades out
  win.on("resize", saveBounds);
  // Ctrl and the mouse wheel (or a touchpad's pinch) zoom too, in the same steps.
  win.webContents.on("zoom-changed", (e, way) => zoom(way === "in" ? 1 : -1));
  win.webContents.on("did-finish-load", () => tellZoom(win.webContents.getZoomFactor(), false));
  win.on("move", saveBounds);

  // Links out of the Codex open in the computer's browser; the window never leaves the Codex.
  win.webContents.setWindowOpenHandler(({ url }) => { if (/^https?:/.test(url)) shell.openExternal(url); return { action: "deny" }; });
  win.webContents.on("will-navigate", (e, url) => { if (!url.startsWith(origin)) { e.preventDefault(); if (/^https?:/.test(url)) shell.openExternal(url); } });
  // Closing: the page saves what's still being typed first, then the window closes. However the close
  // starts (the X, Alt+F4, the page itself), it's held until that save is done (or 4 s have passed).
  let flushing = false;
  const flushThenClose = () => {
    if (flushing) return;
    flushing = true;
    saveAndSync().finally(() => { flushed = true; if (!win.isDestroyed()) win.close(); });
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
  if (INSTALLED) watchForUpdates();
}

/**
 * Updates (PLAN.md, step 6): the installed Codex looks for a newer release on GitHub (the repository's
 * latest release; see package.json's "build.publish") and downloads it quietly. Once it's here, the
 * page covers itself with the update screen (src/app.js, showUpdateScreen): Restart and update saves,
 * sends, installs and opens the Codex again (installUpdate). Closing the Codex installs it too.
 */
const update = { state: "none", version: null, percent: 0 }; // none, downloading, ready, installing
let updater = null;
const tellUpdate = () => {
  if (win && !win.isDestroyed()) win.webContents.executeJavaScript(`window.__codex && window.__codex.updateState && window.__codex.updateState(${JSON.stringify(update)})`).catch(() => {});
};
// The engine (server.js, in this process) asks for it: the page's GET /api/update and its button.
globalThis.codexUpdate = { view: () => ({ ...update }), install: () => installUpdate(), check: () => checkNow() };
// Out of date (src/codex-version.js): look for the update now rather than at the next hourly check.
let checkNow = () => {};

async function installUpdate() {
  if (!updater || update.state !== "ready") return { ok: false, error: "No update is ready." };
  update.state = "installing"; tellUpdate();
  await saveAndSync();
  flushed = true; // saved and sent: the window may close now
  // The small "Updating" window stands in for the Codex while the installer runs (electron/update-splash.js).
  await showUpdateSplash(update.version);
  if (win && !win.isDestroyed()) win.hide();
  // Silently (one-click installer), then start the new Codex.
  setTimeout(() => updater.quitAndInstall(true, true), 100);
  return { ok: true };
}

// How often an open Codex looks for a new release (at start too): an hour, so a release reaches everyone
// with a Codex open within the hour rather than the next day. A look is one small file (latest.yml).
const CHECK_EVERY_MS = 60 * 60 * 1000;

async function watchForUpdates() {
  try {
    const { default: mod } = await import("electron-updater");
    const { autoUpdater } = mod;
    updater = autoUpdater;
    autoUpdater.autoDownload = true;
    autoUpdater.autoInstallOnAppQuit = true;
    autoUpdater.on("update-available", (info) => { update.state = "downloading"; update.version = info.version; update.percent = 0; tellUpdate(); });
    autoUpdater.on("download-progress", (p) => { const pc = Math.round(p.percent || 0); if (pc !== update.percent) { update.percent = pc; tellUpdate(); } });
    autoUpdater.on("update-downloaded", (info) => { update.state = "ready"; update.version = info.version; update.percent = 100; tellUpdate(); });
    autoUpdater.on("error", (e) => {
      if (process.env.CODEX_DEBUG) console.log("update:", e.message);
      if (update.state === "downloading") { update.state = "none"; tellUpdate(); } // tried again at the next check
    });
    const check = () => autoUpdater.checkForUpdates().catch(() => {});
    checkNow = () => { if (update.state === "none") check(); };
    check();
    setInterval(check, CHECK_EVERY_MS);
  } catch (e) { if (process.env.CODEX_DEBUG) console.log("updates off:", e.message); }
}

// Zoom in the steps a browser uses, and the page says the new zoom for a moment (src/zoom-badge.js).
const ZOOMS = [0.5, 0.67, 0.75, 0.8, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3];
function zoom(dir) {
  if (!win || win.isDestroyed()) return;
  const wc = win.webContents, now = wc.getZoomFactor();
  let next = 1;
  if (dir > 0) next = ZOOMS.find((z) => z > now + 0.001) ?? ZOOMS[ZOOMS.length - 1];
  else if (dir < 0) next = [...ZOOMS].reverse().find((z) => z < now - 0.001) ?? ZOOMS[0];
  wc.setZoomFactor(next);
  tellZoom(next, true);
}
// The page is told the zoom: --zoom keeps the title bar as tall as Windows' own buttons, which don't
// zoom (style.css, body.in-app #app), and show puts up the badge. After every load too, as a reload
// keeps the zoom.
function tellZoom(z, show) {
  if (!win || win.isDestroyed()) return;
  win.webContents.executeJavaScript(`document.documentElement.style.setProperty("--zoom", "${z}");` +
    (show ? `window.dispatchEvent(new CustomEvent("codex-zoom", { detail: ${z} }));` : "")).catch(() => {}); // a page still loading misses one
}

// No visible menu bar, but the usual keys still work (reload, zoom, developer tools). Zoom on Ctrl and
// the + and - keys (on Norwegian keyboards too: Electron names the keys, not the characters), the
// number pad's, and Ctrl+0.
const zoomItem = (label, accelerator, dir, visible = true) => ({ label, accelerator, visible, click: () => zoom(dir) });
Menu.setApplicationMenu(Menu.buildFromTemplate([
  { label: "View", submenu: [
    { role: "reload" }, { role: "forceReload" }, { role: "toggleDevTools" }, { type: "separator" },
    zoomItem("Actual size", "CommandOrControl+0", 0), zoomItem("Actual size", "CommandOrControl+num0", 0, false),
    zoomItem("Zoom in", "CommandOrControl+Plus", 1), zoomItem("Zoom in", "CommandOrControl+=", 1, false), zoomItem("Zoom in", "CommandOrControl+numadd", 1, false),
    zoomItem("Zoom out", "CommandOrControl+-", -1), zoomItem("Zoom out", "CommandOrControl+numsub", -1, false),
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
