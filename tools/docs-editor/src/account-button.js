// The account button in the title bar (src/discord-account.js on the engine's side): "Sign in" with
// Discord, or your Discord avatar with a small menu (your name, what signing in does, Sign out).

const PERSON = '<svg viewBox="0 0 24 24"><circle cx="12" cy="8.5" r="3.5"/><path d="M5 19.5c1.2-3.4 3.9-5 7-5s5.8 1.6 7 5"/></svg>';

export function makeAccountButton({ $, h, api, toast, onChange = () => {} }) {
  let V = { signedIn: false, user: null, member: null };
  const btn = $("#accountBtn");

  function draw() {
    onChange(V);
    btn.replaceChildren();
    btn.classList.toggle("signed-in", V.signedIn);
    if (V.signedIn && V.user) {
      btn.append(h("img", { class: "account-avatar", src: V.user.avatar, alt: "" }));
      btn.title = `${V.user.name}: signed in with Discord`;
    } else {
      btn.innerHTML = PERSON + "<span>Sign in</span>";
      btn.title = "Sign in with Discord, to send your changes and comment under your name";
    }
  }

  async function signIn() {
    const r = await api("/api/account/signin", { method: "POST" });
    if (!r.url) return toast("Couldn't start signing in", "bad");
    window.open(r.url, "_blank");
    toast("Finish signing in in your browser");
  }

  function menu() {
    if (close()) return;
    const m = h("div", { class: "cm-menu account-menu" },
      h("div", { class: "account-head" },
        h("img", { class: "account-avatar big", src: V.user.avatar, alt: "" }),
        h("div", {}, h("div", { class: "account-name" }, V.user.name), h("div", { class: "account-sub" }, "Signed in with Discord"))),
      V.member === false ? h("div", { class: "account-warn" }, "You don't have the Codex role in the R'th Discord any more, so changes can't be sent. Ask the owner.") : null,
      h("div", { class: "account-note" }, "Your changes are saved under your name."),
      h("div", { class: "spell-sep" }),
      h("button", { class: "cm-menu-item", onclick: async () => { close(); V = await api("/api/account/signout", { method: "POST" }); draw(); toast("Signed out"); } }, "Sign out"));
    document.body.append(m);
    const r = btn.getBoundingClientRect();
    m.style.right = Math.max(8, window.innerWidth - r.right) + "px";
    m.style.top = r.bottom + 4 + "px";
    btn.classList.add("on");
    setTimeout(() => document.addEventListener("mousedown", outside, true), 0);
  }
  const outside = (ev) => { if (!ev.target.closest(".account-menu, #accountBtn")) close(); };
  function close() {
    const m = document.querySelector(".account-menu");
    if (!m) return false;
    m.remove();
    btn.classList.remove("on");
    document.removeEventListener("mousedown", outside, true);
    return true;
  }

  btn.addEventListener("click", () => (V.signedIn ? menu() : signIn()));

  return {
    async init() { draw(); V = await api("/api/account"); draw(); },
    /** From the engine: signed in, out, or a sign-in that failed. */
    onEvent(v) {
      const was = V.signedIn;
      V = v;
      draw();
      if (v.error) toast(v.error, "bad");
      else if (v.signedIn && !was) toast(`Signed in as ${v.user.name}`, "good");
    },
    escape: close,
  };
}
