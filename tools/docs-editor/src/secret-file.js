// A small file kept locked on this computer: the Discord sign-in (account.bin) and the OpenRouter key
// (openrouter.bin). In the desktop app it's encrypted with Electron's safeStorage (Windows: tied to this
// Windows user); in a browser-run Codex (npm start) there's no Electron and it's plain text.
//
// The async safeStorage calls: the sync ones (encryptString, decryptString, isEncryptionAvailable) are
// deprecated and will go, and files they wrote read the same with the async ones. Writes are queued, so
// a sign-out right after a sign-in can't land in the wrong order.

import fsp from "node:fs/promises";

export async function secretFile(file) {
  let safe = null;
  try {
    const { safeStorage } = await import("electron");
    if (safeStorage?.isAsyncEncryptionAvailable && (await safeStorage.isAsyncEncryptionAvailable())) safe = safeStorage;
  } catch {}

  let queue = Promise.resolve();
  /** Save the text (null: delete the file). Callers needn't wait; the returned promise is for those who do. */
  function write(text) {
    queue = queue.then(async () => {
      if (text == null) return fsp.rm(file, { force: true });
      await fsp.writeFile(file, safe ? await safe.encryptStringAsync(text) : text);
    }).catch((e) => console.warn("Couldn't save " + file + ": " + e.message));
    return queue;
  }
  /** The text, or null when there's none (or it can't be read, e.g. copied from another Windows user). */
  async function read() {
    let raw;
    try { raw = await fsp.readFile(file); } catch { return null; }
    if (!safe) return raw.toString("utf8");
    try {
      const { result, shouldReEncrypt } = await safe.decryptStringAsync(raw);
      if (shouldReEncrypt) write(result); // the system's key changed: lock it again with the new one
      return result;
    } catch { return null; }
  }
  return { read, write, encrypted: !!safe };
}
