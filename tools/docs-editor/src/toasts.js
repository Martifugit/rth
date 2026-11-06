// Short messages at the bottom of the window (toast): a deck at the bottom centre, newest in front, older
// ones tucked behind (at most three show). Pointing at the deck spreads it out and holds every timer; the
// same message again only counts (×2). With actions ([{ label, run }]) a message stays longer (ms), has
// buttons and an ✕; running an action closes it.
//
//   makeToasts({ h, host }) → toast(text, kind = "" | "good" | "bad", { actions, ms })

export function makeToasts({ h, host }) {
  const TOASTS = [];
  let toastsHeld = false;
  function toast(text, kind = "", { actions = null, ms = null } = {}) {
    const same = !actions && TOASTS.find((x) => !x.actions && x.text === text && x.kind === kind);
    if (same) { same.n++; same.count.textContent = "×" + same.n; startToast(same); return same.el; }
    const x = { text, kind, actions, n: 1, ms: ms || (actions ? 12000 : 2600) };
    x.count = h("span", { class: "toast-n" });
    x.el = h("div", { class: "toast " + kind, onmouseenter: () => holdToasts(true), onmouseleave: () => setTimeout(() => holdToasts(!!document.querySelector(".toast:hover")), 80) },
      h("span", { class: "toast-ico" }, kind === "good" ? "✓" : kind === "bad" ? "!" : "✦"),
      h("span", { class: "toast-text" }, text), x.count,
      actions ? actions.map((a) => h("button", { class: "toast-btn", onclick: () => { closeToast(x); a.run(); } }, a.label)) : null,
      actions ? h("button", { class: "toast-x", title: "Close", onclick: () => closeToast(x) }, "✕") : null);
    TOASTS.unshift(x);
    host().append(x.el);
    startToast(x);
    return x.el;
  }
  function startToast(x) { clearTimeout(x.timer); if (!toastsHeld) x.timer = setTimeout(() => closeToast(x), x.ms); stackToasts(); }
  function closeToast(x) {
    clearTimeout(x.timer);
    const i = TOASTS.indexOf(x);
    if (i < 0) return;
    TOASTS.splice(i, 1);
    x.el.classList.add("out");
    setTimeout(() => x.el.remove(), 220);
    stackToasts();
  }
  function holdToasts(on) {
    if (on === toastsHeld) return;
    toastsHeld = on;
    for (const x of TOASTS) { clearTimeout(x.timer); if (!on) x.timer = setTimeout(() => closeToast(x), Math.min(x.ms, 2600)); }
    stackToasts();
  }
  function stackToasts() {
    let y = 0;
    const front = TOASTS[0]?.el.offsetHeight || 0;
    TOASTS.forEach((x, i) => {
      const el = x.el;
      el.style.zIndex = String(100 - i);
      // Collapsed, each one behind shows its top edge a clear step above the one in front (whatever their
      // heights) and is a little smaller and dimmer, so the deck shows there are more.
      const scale = 1 - i * 0.04, lift = front + i * 10 - el.offsetHeight * scale;
      el.style.transform = toastsHeld ? `translate(-50%, ${-y}px)` : `translate(-50%, ${-lift}px) scale(${scale})`;
      el.style.opacity = toastsHeld || i < 3 ? "" : "0";
      el.style.filter = toastsHeld || i === 0 ? "" : `brightness(${1 - i * 0.22})`; // darker, but solid: see-through would show the next one's words
      el.style.pointerEvents = toastsHeld || i === 0 ? "auto" : "none";
      y += el.offsetHeight + 8;
    });
  }

  return toast;
}
