// /open: a link Discord can show (it only makes https links clickable) that hands over to the Codex
// on this computer (rth-codex://open?p=...), at a heading if it names one.

import { codexPage, REPO, esc } from "../../src/codex-page.js";

export function openPage(url, env) {
  const p = url.searchParams.get("p") || "";
  const hd = url.searchParams.get("h") || "";
  if (!/^(docs\/[\w./ '()-]+\.md|[\w.-]+\.md)$/.test(p) || p.includes("..")) return new Response("Not a page of the docs.", { status: 400 });
  const target = "rth-codex://open?" + new URLSearchParams(hd ? { p, h: hd } : { p });
  const title = p.split("/").pop().replace(/\.md$/, "").replace(/[-_]/g, " ");
  const where = `<code>${esc(p)}</code>${hd ? " at “" + esc(hd) + "”" : ""}`;
  // Hand over to the Codex. When it opens, it takes the focus from the browser (straight away, or once
  // "Open" is clicked in the browser's question): then this tab has done its job and closes itself.
  // Browsers let a page close its own tab when it's the tab's only page, as with a link from Discord;
  // otherwise it says so.
  const script = `
  const target = ${JSON.stringify(target)};
  let asked = 0;
  function go() { asked = Date.now(); location.href = target; }
  function done() {
    if (!asked || Date.now() - asked > 60000) return;
    asked = 0;
    setTimeout(() => {
      window.close();
      document.querySelector("section").innerHTML = "<h1>Opened in Codex</h1><p>You can close this tab.</p>";
    }, 300);
  }
  window.addEventListener("blur", done);
  document.addEventListener("visibilitychange", () => { if (document.hidden) done(); });
  document.querySelector("a.btn").addEventListener("click", (e) => { e.preventDefault(); go(); });
  go();`;
  const html = codexPage({
    title: `${title} - R'th Codex`, heading: "Opening in Codex", assets: REPO, script,
    body: `<p>${where}</p>
    <a class="btn" href="${esc(target)}">Open in Codex</a>
    <p><small>Nothing happened? Open Codex once (it registers itself), then click the button.</small></p>`,
  });
  return new Response(html, { headers: { "content-type": "text/html; charset=utf-8" } });
}
