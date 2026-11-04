// The AI through OpenRouter (PLAN.md, "AI, built in"): free models, each person's own key and daily
// allowance (src/openrouter-account.js). One request per message, because the free allowance counts
// requests: the engine gathers what the model needs to read and sends it with the conversation so far,
// and the model answers in one go. It changes pages by writing edit blocks in its answer, which the
// engine applies itself (no tool calls, which free models handle unreliably):
//
//   <edit page="docs/x.md">
//   <find>exact text from the page</find>
//   <replace>what replaces it</replace>
//   </edit>
//
// The model is OpenRouter's free router, which picks a free model that's up and can do the job, so the
// Codex never needs a list of free models kept up to date.

const URL = "https://openrouter.ai/api/v1/chat/completions";
export const OPENROUTER_MODELS = (process.env.CODEX_OPENROUTER_MODELS || "openrouter/free").split(",").map((s) => s.trim()).filter(Boolean);

export const EDIT_FORMAT = [
  "To change a page, write an edit block anywhere in your answer (as many as you need, one per place):",
  "<edit page=\"docs/gdd/path/to/page.md\">",
  "<find>the exact text in the page now, word for word, enough of it to be found only once</find>",
  "<replace>what replaces it</replace>",
  "</edit>",
  "For a new page: <edit page=\"docs/gdd/...md\"><find></find><replace>the whole page</replace></edit>. Edit blocks are taken out of your answer before it's shown, so say in a sentence what you changed.",
].join("\n");

export function openRouterProvider({ key, models = () => OPENROUTER_MODELS }) {
  const check = async () => (key()
    ? { ok: true, name: "OpenRouter", version: models().join(", ") === "openrouter/free" ? "free models" : models().join(", ") }
    : { ok: false, needsSignIn: "openrouter", error: "R'th AI isn't set up on this computer yet. Sign in with OpenRouter for free AI (your own daily allowance)." });

  /** messages: [{ role, content }]. */
  function run({ messages, onText, onActivity }) {
    const ctrl = new AbortController();
    let stopped = false;
    const done = (async () => {
      const k = key();
      if (!k) return { error: "Not signed in with OpenRouter." };
      onActivity("Thinking");
      const list = models();
      let r;
      try {
        r = await fetch(URL, {
          method: "POST", signal: ctrl.signal,
          headers: { Authorization: "Bearer " + k, "Content-Type": "application/json", "HTTP-Referer": "https://github.com/Martifugit/rth", "X-Title": "R'th Codex" },
          body: JSON.stringify({ model: list[0], models: list.length > 1 ? list : undefined, messages, stream: true }),
        });
      } catch (e) { return { error: stopped ? "Stopped." : "Couldn't reach OpenRouter: " + e.message }; }
      if (r.status === 429) return { error: "Today's free AI allowance is used up (OpenRouter allows about 50 requests a day). It comes back tomorrow; everything else in the Codex keeps working." };
      if (r.status === 401) return { error: "OpenRouter didn't accept your key. Sign in with OpenRouter again." };
      if (r.status === 402) return { error: "OpenRouter says the account needs credit for this model." };
      if (!r.ok) return { error: `OpenRouter said ${r.status}: ${(await r.text()).slice(0, 200)}` };
      let text = "", model = null, buf = "";
      const dec = new TextDecoder();
      try {
        for await (const chunk of r.body) {
          buf += dec.decode(chunk, { stream: true });
          let nl;
          while ((nl = buf.indexOf("\n")) >= 0) {
            const line = buf.slice(0, nl).trim(); buf = buf.slice(nl + 1);
            if (!line.startsWith("data:")) continue;
            const data = line.slice(5).trim();
            if (data === "[DONE]") continue;
            let ev; try { ev = JSON.parse(data); } catch { continue; }
            if (ev.error) return { error: "OpenRouter: " + (ev.error.message || "an error"), text };
            model = ev.model || model;
            const d = ev.choices?.[0]?.delta?.content;
            if (d) { text += d; onText(d); }
          }
        }
      } catch (e) { return { error: stopped ? "Stopped." : "The answer was cut off: " + e.message, text }; }
      return { text, model };
    })();
    return { done, stop: () => { stopped = true; ctrl.abort(); } };
  }

  return { check, run, kind: "openrouter" };
}

/** The edit blocks in an answer: [{ page, find, replace }], and the answer without them. */
export function editsIn(text) {
  const edits = [];
  const rest = text.replace(/<edit\s+page="([^"]+)"\s*>\s*<find>([\s\S]*?)<\/find>\s*<replace>([\s\S]*?)<\/replace>\s*<\/edit>/g, (m, page, find, replace) => {
    edits.push({ page: page.trim(), find: trimOne(find), replace: trimOne(replace) });
    return "";
  });
  return { edits, text: rest.replace(/\n{3,}/g, "\n\n").trim() };
}
// The block's own line breaks round the text aren't part of it.
const trimOne = (s) => s.replace(/^\n/, "").replace(/\n$/, "");

/** An answer as it streams, without edit blocks (finished or still being written). */
export function shown(text) {
  return editsIn(text).text.replace(/<edit\b[\s\S]*$/, "").trim();
}

/** Apply edits to page texts: { texts (Map page -> text), failed: [{ page, find }] }. */
export function applyEdits(edits, read) {
  const texts = new Map(), failed = [];
  return (async () => {
    for (const e of edits) {
      if (!/^docs\/gdd\/[^]+\.md$/.test(e.page) || e.page.includes("..")) { failed.push({ ...e, why: "not a page in the GDD (docs/gdd/)" }); continue; }
      let t = texts.has(e.page) ? texts.get(e.page) : await read(e.page);
      if (!e.find) { texts.set(e.page, e.replace.endsWith("\n") ? e.replace : e.replace + "\n"); continue; } // a new or whole page
      if (t == null) { failed.push({ ...e, why: "no such page" }); continue; }
      const at = t.indexOf(e.find);
      if (at < 0 || t.indexOf(e.find, at + 1) >= 0) { failed.push({ ...e, why: at < 0 ? "text not found" : "text found more than once" }); continue; }
      t = t.slice(0, at) + e.replace + t.slice(at + e.find.length);
      texts.set(e.page, t);
    }
    return { texts, failed };
  })();
}
