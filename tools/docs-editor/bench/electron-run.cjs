// Runs bench/ai-bench.mjs inside Electron (for --openrouter: the saved key is locked with Electron's
// safeStorage). A main module with top-level await holds back Electron's "ready", so this one waits for
// it first and only then loads the benchmark:  electron bench/electron-run.cjs --old <folder> --openrouter ...
const { app } = require("electron");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
// safeStorage's key is kept in the app's own folder: use the folder of the Codex whose key is wanted.
const dir = (() => { const i = process.argv.indexOf("--key-dir"); return i > 0 ? process.argv[i + 1] : path.join(process.env.LOCALAPPDATA || "", "rth-codex-dev"); })();
app.setPath("userData", dir);
app.whenReady().then(() => import(pathToFileURL(path.join(__dirname, "ai-bench.mjs")).href)).catch((e) => { console.error(e); app.exit(1); });
