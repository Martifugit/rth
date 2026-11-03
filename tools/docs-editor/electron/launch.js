// Starts the Codex desktop app. On Windows it runs its own copy of Electron, renamed and stamped
// "R'th Codex" (name, description and logo), so Task Manager and Alt+Tab show the Codex instead of
// "Electron": Windows takes those from the exe's own version info, which no code inside the app can
// change. The copy lives in node_modules/.codex-app and is remade when Electron or the logo changes.
// Elsewhere it runs Electron as it is.

import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..");
const require = createRequire(import.meta.url);

const NAME = "R'th Codex";
const EXE = "Codex.exe";
const ICON = path.join(ROOT, "assets", "logo.ico");

async function brandedExe() {
  const electronDir = path.dirname(require.resolve("electron/package.json"));
  const dist = path.join(electronDir, "dist");
  const version = JSON.parse(fs.readFileSync(path.join(electronDir, "package.json"), "utf8")).version;
  const out = path.join(ROOT, "node_modules", ".codex-app");
  const exe = path.join(out, EXE);
  const stampFile = path.join(out, "stamp.json");
  const stamp = JSON.stringify({ version, icon: fs.existsSync(ICON) ? fs.statSync(ICON).mtimeMs : 0 });

  let current = null;
  try { current = fs.readFileSync(stampFile, "utf8"); } catch {}
  if (current === stamp && fs.existsSync(exe)) return exe;

  console.log(`Making ${NAME}.exe from Electron ${version} (once)...`);
  fs.rmSync(out, { recursive: true, force: true });
  fs.cpSync(dist, out, { recursive: true });
  fs.renameSync(path.join(out, "electron.exe"), exe);
  const { rcedit } = await import("rcedit");
  await rcedit(exe, {
    "version-string": {
      FileDescription: NAME, ProductName: NAME, InternalName: "Codex", OriginalFilename: EXE,
      CompanyName: "R'th", LegalCopyright: "",
    },
    ...(fs.existsSync(ICON) ? { icon: ICON } : {}),
  });
  fs.writeFileSync(stampFile, stamp);
  return exe;
}

let exe;
if (process.platform === "win32") {
  try { exe = await brandedExe(); }
  catch (e) { console.warn(`Couldn't make ${NAME}.exe (${e.message}); running plain Electron.`); }
}
exe ??= require("electron"); // the electron package's main export is the path to its binary

const child = spawn(exe, [ROOT, ...process.argv.slice(2)], { stdio: "inherit", windowsHide: false });
child.on("close", (code) => process.exit(code ?? 0));
