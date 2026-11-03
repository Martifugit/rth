// /open: a link Discord can show (it only makes https links clickable) that hands over to the Codex
// on this computer (rth-codex://open?p=...), at a heading if it names one.

export function openPage(url, env) {
  const p = url.searchParams.get("p") || "";
  const hd = url.searchParams.get("h") || "";
  if (!/^(docs\/[\w./ '()-]+\.md|[\w.-]+\.md)$/.test(p) || p.includes("..")) return new Response("Not a page of the docs.", { status: 400 });
  const target = "rth-codex://open?" + new URLSearchParams(hd ? { p, h: hd } : { p });
  const title = p.split("/").pop().replace(/\.md$/, "").replace(/[-_]/g, " ");
  const esc = (s) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
  const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(title)} · R'th Codex</title>
<style>
  body { margin: 0; height: 100vh; display: grid; place-items: center; background: #0c0b09; color: #ddd3bf; font: 16px/1.5 "Segoe UI", system-ui, sans-serif; }
  main { max-width: 420px; padding: 24px; text-align: center; }
  h1 { font: 700 15px/1 Georgia, serif; letter-spacing: 2px; text-transform: uppercase; color: #e0c387; margin: 0 0 18px; }
  a.open { display: inline-block; margin: 8px 0 18px; padding: 9px 18px; border-radius: 5px; background: #c9a25e; color: #231707; font-weight: 700; text-decoration: none; }
  p { color: #a89c86; margin: 0 0 8px; }
  code { color: #cfd2d6; }
</style></head>
<body><main>
  <h1>R'th Codex</h1>
  <p>Opening <code>${esc(p)}</code>${hd ? " at “" + esc(hd) + "”" : ""} in the Codex…</p>
  <a class="open" href="${esc(target)}">Open in the Codex</a>
  <p>Nothing happened? Open the Codex once (it registers itself), then click the button.</p>
</main>
<script>location.href = ${JSON.stringify(target)};</script>
</body></html>`;
  return new Response(html, { headers: { "content-type": "text/html; charset=utf-8" } });
}
