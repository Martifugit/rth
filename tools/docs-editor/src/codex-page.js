// The Codex's look for the few plain web pages it shows outside the app: the helper's (an update is
// needed, opening a page from Discord, sign-in problems) and the engine's (back from signing in). Drawn
// like the first-run window (electron/firstrun.html): the stone over the whole page, and in its middle
// the logo, a Cinzel heading and the words in Alegreya Sans. Nothing boxed.
//
// `assets` says where the fonts, the stone and the logo come from: the engine serves them itself
// (LOCAL); the helper, on the web, links them from the public repository (REPO).

export const LOCAL = { fonts: "/ui/Fonts", stone: "/stone.jpg", logo: "/logo.png" };
const RAW = "https://raw.githubusercontent.com/Martifugit/rth/main";
export const REPO = { fonts: RAW + "/client/Assets/Resources/UI/Fonts", stone: RAW + "/tools/docs-editor/public/stone.jpg", logo: RAW + "/tools/docs-editor/public/logo.png" };

export { esc } from "./text.js";
import { esc } from "./text.js";

/**
 * A whole page: `title` (the tab), `heading` (escaped), `body` (HTML, the caller escapes what it puts in),
 * optional `script` (run at the end).
 */
export function codexPage({ title = "R'th Codex", heading, body, assets = LOCAL, script = "" }) {
  const a = assets;
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(title)}</title>
<style>
  @font-face { font-family: "Cinzel"; src: url("${a.fonts}/Cinzel.ttf"); font-weight: 400 900; }
  @font-face { font-family: "Alegreya Sans"; src: url("${a.fonts}/AlegreyaSans-Medium.ttf"); font-weight: 400 500; }
  @font-face { font-family: "Alegreya Sans"; src: url("${a.fonts}/AlegreyaSans-Bold.ttf"); font-weight: 600 900; }
  body { margin: 0; min-height: 100vh; display: grid; place-items: center; color: #ddd3bf; font: 500 16px/1.55 "Alegreya Sans", "Segoe UI", sans-serif;
    background: linear-gradient(rgba(0, 0, 0, 0), rgba(0, 0, 0, .25)), #0d0c0a url("${a.stone}") 0 0 / 800px repeat fixed; text-shadow: 0 1px 1px rgba(0, 0, 0, .85); }
  main { width: min(440px, calc(100vw - 40px)); padding: 24px 0; text-align: center; }
  .logo { width: 84px; height: 84px; margin-bottom: 8px; filter: drop-shadow(0 2px 4px #000); }
  h1 { margin: 0 0 10px; font: 700 22px/1.3 "Cinzel", Georgia, serif; letter-spacing: .5px; color: #e0c387; }
  p { margin: 0 0 14px; color: #a89c86; }
  p:last-child { margin-bottom: 0; }
  ol { display: inline-block; margin: 0 0 18px; padding-left: 1.3em; text-align: left; }
  li { margin: 0 0 6px; padding-left: 4px; }
  li::marker { font: 700 14px "Cinzel", serif; color: #c9a25e; }
  code { font: 14px "Cascadia Mono", Consolas, monospace; color: #cfd2d6; }
  .alt { font-size: 14.5px; }
  .bad { color: #f0a08f; }
  a { color: #f0d58c; text-decoration: underline; text-decoration-color: rgba(201, 162, 94, .45); text-underline-offset: 3px; }
  a:hover { color: #fff1d2; }
  a.btn { display: inline-block; margin: 2px 0 16px; padding: 6px 16px; border: 1px solid #6b5838; border-radius: 4px; background: rgba(201, 162, 94, .1);
    font-weight: 700; text-decoration: none; }
  a.btn:hover { background: rgba(201, 162, 94, .2); border-color: #8a7148; }
  small { display: block; margin-top: 10px; color: #7d725f; font-size: 13px; }
</style></head>
<body><main>
  <img class="logo" src="${a.logo}" alt="R'th">
  <section>
    <h1>${esc(heading)}</h1>
    ${body}
  </section>
</main>${script ? `<script>${script}</script>` : ""}</body></html>`;
}
