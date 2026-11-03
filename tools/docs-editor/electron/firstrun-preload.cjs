// The first-run window's bridge to the app (electron/firstrun.js): only these few calls.
const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("codex", {
  fetch: () => ipcRenderer.invoke("firstrun:fetch"),
  choose: () => ipcRenderer.invoke("firstrun:choose"),
  onProgress: (cb) => ipcRenderer.on("firstrun", (e, m) => { if (m.type === "progress") cb(m); }),
});
