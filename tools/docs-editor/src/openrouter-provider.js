// The AI through OpenRouter (PLAN.md, "AI, built in"): free models, each person's own key and daily
// allowance (src/openrouter-account.js). The engine gathers what the model needs to read (the page, who
// links to it, the rules) and sends it with the conversation, so most answers take one request; the free
// allowance counts requests. When it needs more it can look things up with tools the engine runs
// (search the GDD, read a page, who links here, search and read the game's code: src/assistant.js), at
// most TOOL_ROUNDS rounds a message, and a model that can't use tools answers without them. It changes
// pages by writing edit blocks in its answer, which the engine applies itself:
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

const TOOL_ROUNDS = 4; // lookups a message may take before it must answer (each round is a request)

export function openRouterProvider({ key, models = () => OPENROUTER_MODELS }) {
  const check = async () => (key()
    ? { ok: true, name: "OpenRouter", version: models().join(", ") === "openrouter/free" ? "free models" : models().join(", ") }
    : { ok: false, needsSignIn: "openrouter", error: "R'th AI isn't set up on this computer yet. Sign in with OpenRouter for free AI (your own daily allowance)." });

  /** messages: [{ role, content }]. tools: OpenAI-style function definitions, useTool(name, args) runs one
   *  (returns text, describe(name, args) says it in the activity line); model: this conversation's /model. */
  function run({ messages, onText, onActivity, onThought = () => {}, model = null, tools = null, useTool = null, describe = null }) {
    const ctrl = new AbortController();
    let stopped = false;
    const done = (async () => {
      const k = key();
      if (!k) return { error: "Not signed in with OpenRouter." };
      onActivity("Thinking");
      const list = model ? [model] : models();
      const convo = [...messages];
      let withTools = Array.isArray(tools) && tools.length && useTool ? tools : null;
      let all = "", used = null;
      for (let round = 0; ; round++) {
        // The last round answers without lookups, so a message never costs more than TOOL_ROUNDS + 1 requests.
        const offer = withTools && round < TOOL_ROUNDS ? withTools : null;
        let r;
        try {
          r = await fetch(URL, {
            method: "POST", signal: ctrl.signal,
            headers: { Authorization: "Bearer " + k, "Content-Type": "application/json", "HTTP-Referer": "https://github.com/Martifugit/rth", "X-Title": "R'th Codex" },
            body: JSON.stringify({ model: list[0], models: list.length > 1 ? list : undefined, messages: convo, stream: true, ...(offer ? { tools: offer, tool_choice: "auto", provider: { require_parameters: true } } : {}) }),
          });
        } catch (e) { return { error: stopped ? "Stopped." : "Couldn't reach OpenRouter: " + e.message, text: all }; }
        if (r.status === 429) return { error: "Today's free AI allowance is used up (OpenRouter allows about 50 requests a day). It comes back tomorrow; everything else in Codex keeps working.", text: all };
        if (r.status === 401) return { error: "OpenRouter didn't accept your key. Sign in with OpenRouter again." };
        if (r.status === 402) return { error: "OpenRouter says the account needs credit for this model." };
        if (!r.ok) {
          const body = (await r.text()).slice(0, 300);
          // A model (or every free one up just now) that can't use tools: answer without them, as before.
          if (offer && (r.status === 404 || r.status === 400) && /tool/i.test(body)) { withTools = null; round--; continue; }
          return { error: `OpenRouter said ${r.status}: ${body.slice(0, 200)}`, text: all };
        }
        const start = all.length;
        const got = await readStream(r, (d) => { all += d; onText(d); }, onThought);
        if (got.error) return { error: stopped ? "Stopped." : got.error, text: all };
        used = got.model || used;
        if (!got.calls.length && offer && /<tool_call>/i.test(got.text)) {
          const written = callsInText(got.text);
          all = all.slice(0, start) + got.text.replace(TOOL_CALL_TEXT, "").trim(); // (the streaming view hides them too: shown())
          if (written.length) got.calls = written;
          else { withTools = null; all = all.slice(0, start); round--; continue; } // garbled: ask again without lookups
        }
        if (!got.calls.length) return { text: all, model: used };
        // It asked to look things up: run them, give it the results, and let it carry on.
        convo.push({ role: "assistant", content: got.text || null, tool_calls: got.calls.map((c) => ({ id: c.id, type: "function", function: { name: c.name, arguments: c.args } })) });
        for (const c of got.calls) {
          let args = {};
          try { args = JSON.parse(c.args || "{}"); } catch {}
          onActivity(describe ? describe(c.name, args) : "Looking things up");
          let out;
          try { out = String(await useTool(c.name, args)); } catch (e) { out = "That didn't work: " + e.message; }
          convo.push({ role: "tool", tool_call_id: c.id, content: out.slice(0, 40000) });
        }
        if (all && !all.endsWith("\n")) { all += "\n\n"; onText("\n\n"); }
      }
    })();
    return { done, stop: () => { stopped = true; ctrl.abort(); } };
  }

  return { check, run, kind: "openrouter" };
}

const TOOL_CALL_TEXT = /<tool_call>[\s\S]*?(<\/tool_call>|$)/gi;

/** Tool calls a model wrote into its text instead of making them (<tool_call>{"name", "arguments"}</tool_call>). */
export function callsInText(text) {
  const out = [];
  for (const m of String(text || "").matchAll(/<tool_call>\s*([\s\S]*?)\s*<\/tool_call>/gi)) {
    try {
      const j = JSON.parse(m[1]);
      const name = j.name || j.function?.name;
      const args = j.arguments ?? j.parameters ?? j.function?.arguments ?? {};
      if (name) out.push({ id: "text_" + out.length, name, args: typeof args === "string" ? args : JSON.stringify(args) });
    } catch {}
  }
  return out;
}

/** One streamed reply: its text (handed on as it comes), the model, and any tool calls it made. */
async function readStream(r, onText, onThought = () => {}) {
  let text = "", model = null, buf = "";
  const calls = []; // by index: { id, name, args }
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
        if (ev.error) return { error: "OpenRouter: " + (ev.error.message || "an error"), text, calls: [] };
        model = ev.model || model;
        const delta = ev.choices?.[0]?.delta || {};
        if (delta.reasoning) onThought(delta.reasoning); // a reasoning model's thinking, where the model shares it
        if (delta.content) { text += delta.content; onText(delta.content); }
        for (const t of delta.tool_calls || []) {
          const i = t.index ?? calls.length;
          const c = (calls[i] ||= { id: t.id || "call_" + i, name: "", args: "" });
          if (t.id) c.id = t.id;
          if (t.function?.name) c.name += t.function.name;
          if (t.function?.arguments) c.args += t.function.arguments;
        }
      }
    }
  } catch (e) { return { error: "The answer was cut off: " + e.message, text, calls: [] }; }
  return { text, model, calls: calls.filter((c) => c && c.name) };
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
  return editsIn(text).text.replace(/<edit\b[\s\S]*$/, "").replace(TOOL_CALL_TEXT, "").trim(); // and calls written as text
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
