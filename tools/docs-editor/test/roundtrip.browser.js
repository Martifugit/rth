// A check run in the browser (the editor needs a DOM): with the Codex open, run in the console
//   await (await import("/repo/tools/docs-editor/test/roundtrip.browser.js")).run()
// For every page and every kind of block, it types into the first such block and checks that the
// merged save renders exactly as the original does (apart from the typed text) and touches only
// that block's lines. Nothing is saved (autosave is held off while it runs).

const KINDS = ["paragraph", "tableCell", "listItem", "heading", "blockquote", "codeBlock", "taskItem"];
const norm = (s) => s.replace(/\s+/g, " ").replace(/> </g, "><").trim();

export async function run() {
  const C = window.__codex;
  const failures = [], checked = {};
  let touched = 0;
  C.S.noSave = true;
  try {
  for (const f of C.S.files.map((x) => x.path)) {
    for (const kind of KINDS) {
      C.S.dirty = false;
      await C.openDoc(f, { push: false });
      const ed = C.S.editor;
      let target = null;
      ed.state.doc.descendants((node, pos) => {
        if (target != null) return false;
        if (node.type.name !== kind && !(kind === "tableCell" && node.type.name === "tableHeader")) return;
        if (!node.textContent) return;
        if (node.isTextblock) { target = pos + node.nodeSize - 1; return false; }
        node.descendants((c, p) => { if (target == null && c.isTextblock && c.textContent) target = pos + 1 + p + c.nodeSize - 1; });
      });
      if (target == null) continue;
      checked[kind] = (checked[kind] || 0) + 1;
      ed.commands.insertContentAt(target, "QQ");
      const merged = C.mergeEdit(C.S.base, C.S.baseline, C.editorMarkdown());
      const before = norm(C.renderHtml(C.S.base)), after = norm(C.renderHtml(merged).replace("QQ", ""));
      const a = C.S.base.split("\n"), b = new Set(merged.split("\n"));
      touched = Math.max(touched, a.filter((l) => !b.has(l)).length);
      if (before !== after) {
        let i = 0;
        while (before[i] === after[i]) i++;
        failures.push({ file: f, kind, before: before.slice(Math.max(0, i - 80), i + 80), after: after.slice(Math.max(0, i - 80), i + 80) });
      }
    }
  }
  } finally {
    C.S.dirty = false;
    await C.openDoc(C.S.files[0].path, { push: false });
    C.S.noSave = false;
  }
  return { checked, failures, mostLinesTouched: touched };
}
