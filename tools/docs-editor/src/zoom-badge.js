// The zoom for a moment after it changes (Ctrl and +, -, 0, or the wheel): a small badge at the top right,
// "110%", gone after a second and a half. The desktop app says each change (electron/main.js, zoom); a
// browser shows its own.

export function zoomBadge({ h }) {
  let el = null, timer = null;
  window.addEventListener("codex-zoom", (e) => {
    const pct = Math.round(Number(e.detail) * 100);
    if (!Number.isFinite(pct)) return;
    if (!el) { el = h("div", { class: "zoom-badge gone", role: "status", "aria-live": "polite" }); document.body.append(el); }
    el.textContent = pct + "%";
    el.classList.remove("gone");
    clearTimeout(timer);
    timer = setTimeout(() => el.classList.add("gone"), 1500);
  });
}
