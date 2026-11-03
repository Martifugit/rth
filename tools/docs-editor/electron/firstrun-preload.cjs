// The first-run window's bridge to the app (electron/firstrun.js): only these few calls.
const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("codex", {
  defaults: () => ipcRenderer.invoke("firstrun:defaults"),
  choose: () => ipcRenderer.invoke("firstrun:choose"),
  pickParent: () => ipcRenderer.invoke("firstrun:pickParent"),
  fetch: (folder) => ipcRenderer.invoke("firstrun:fetch", folder),
  onProgress: (cb) => ipcRenderer.on("firstrun", (e, m) => { if (m.type === "progress") cb(m.text); }),
});
