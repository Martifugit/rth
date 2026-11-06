// /open: a link Discord can show (it only makes https links clickable) that hands over to the Codex
// on this computer (rth-codex://open?p=...&t=...), at a heading if it names one.
//
// The tab closes itself only once the Codex says it has the link: the Codex posts the link's token
// (t) to /open/ack (electron/main.js, openLink), and the page asks for it every moment. Closing when
// the page lost focus, as it did, also closed it while the browser was asking "Open R'th Codex?",
// before anyone could answer. An older Codex never says so: the tab stays, with its button.

import { codexPage, REPO, esc } from "../../src/codex-page.js";

export function openPage(url, env) {
  const p = url.searchParams.get("p") || "";
  const hd = url.searchParams.get("h") || "";
  if (!/^(docs\/[\w./ '()-]+\.md|[\w.-]+\.md)$/.test(p) || p.includes("..")) return new Response("Not a page of the docs.", { status: 400 });
  const params = hd ? { p, h: hd } : { p };
  const title = p.split("/").pop().replace(/\.md$/, "").replace(/[-_]/g, " ");
  const where = `<code>${esc(p)}</code>${hd ? " at “" + esc(hd) + "”" : ""}`;
  const target = "rth-codex://open?" + new URLSearchParams(params);
  // Hand over to the Codex, then wait for it to say it has the link (for two minutes, as answering the
  // browser's question can take a while). Browsers let a page close its own tab when it's the tab's
  // only page, as with a link from Discord; otherwise it says so.
  const script = `
  const t = crypto.randomUUID();
  const target = ${JSON.stringify(target)} + "&t=" + t;
  let until = 0;
  function go() {
    location.href = target;
    if (until) { until = Date.now() + 120000; return; }
    until = Date.now() + 120000;
    const poll = async () => {
      if (Date.now() > until) { until = 0; return; }
      try {
        const r = await fetch("/open/ack?t=" + t);
        if ((await r.json()).ok) {
          window.close();
          document.querySelector("section").innerHTML = "<h1>Opened in Codex</h1><p>You can close this tab.</p>";
          return;
        }
      } catch {}
      setTimeout(poll, 700);
    };
    setTimeout(poll, 700);
  }
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
