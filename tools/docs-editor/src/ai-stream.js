// An answer as it streams in (src/ai.js): the text revealed smoothly at a pace that follows how much is
// waiting, each new piece fading in, and a status line with a glint running along it while it thinks or
// looks things up.
// The server sends the text in bursts (every 120 ms, or a whole tool round at once); drawn as it came it
// jumps, so the panel keeps its own cursor and eases towards what has arrived.

const FADE_MS = 420;       // how long a newly shown piece takes to fade in
const MIN_CPS = 40;        // characters a second, at the least, while something is waiting
const CATCH_UP = 2.4;      // ...or this many times what's waiting, a second (so a burst settles in ~0.4 s)
const QUIET_MS = 1200;     // writing with nothing new for this long: the status line comes back
const ROTATE_MS = 3200;    // the generic words take turns this often
const GLINT_RUN_MS = 1100; // the glint's run from the first letter to the last (its cycle, in style.css, is 2.2 s)

// What it says while there's nothing more particular to say.
const WORDS = ["Thinking", "Pondering", "Mulling it over", "Weighing it up", "Turning it over", "Considering"];
const GENERIC = new Set(["", "Reading", "Thinking", "Starting", "Writing"]);

/** One running answer: el is its block in the log. render(markdown) gives HTML; scroller is followed
 *  while you're at its end. */
export function answerStream({ h, render, scroller }) {
  const textEl = h("div", { class: "cm-text ai-text" });
  const label = h("span", { class: "ai-shimmer" });
  const status = h("div", { class: "ai-status" }, label);
  const el = h("div", { class: "ai-msg ai-msg-ai ai-streaming" }, textEl, status);

  let target = "", activity = "", shown = 0, drawn = -1;
  let steps = [], base = 0;           // rendered length at each reveal step, and its time; before base all is opaque
  let lastGrowth = 0, wordAt = 0, word = 0, labelText = null;
  let finishing = null, finishBy = 0, alive = true, last = performance.now();

  /** The latest from the server. The first text it's given is shown at once (an answer already under way). */
  function set(run, { instant = false } = {}) {
    const text = run.text || "";
    if (text.length > target.length) lastGrowth = performance.now();
    if (text.length < target.length || !text.startsWith(target.slice(0, Math.min(shown, text.length)))) shown = Math.min(shown, text.length);
    target = text;
    activity = run.activity || "";
    if (instant) { shown = target.length; steps = []; base = Infinity; }
  }

  /** The answer is complete: reveal the rest, then done() (which draws the finished message in its place). */
  function finish(done) { finishing = done; finishBy = performance.now() + 2500; }

  function frame(now) {
    if (!alive) return;
    const dt = Math.min(0.1, (now - last) / 1000); last = now;
    if (el.isConnected) {
      const waiting = target.length - shown;
      if (waiting > 0) shown = Math.min(target.length, shown + Math.max(MIN_CPS, waiting * CATCH_UP) * dt);
      if (finishing && now > finishBy) shown = target.length;
      const n = Math.floor(shown);
      if (n !== drawn || steps.length) draw(n, now);
      drawStatus(now, n < target.length);
      if (finishing && n >= target.length && !steps.length) { const f = finishing; finishing = null; alive = false; f(); return; }
    }
    requestAnimationFrame(frame);
  }

  function draw(n, now) {
    const follow = scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight < 40;
    textEl.innerHTML = render(target.slice(0, n));
    const len = textEl.textContent.length;
    if (n !== drawn) {
      if (base === Infinity) base = len; // shown at once: nothing to fade
      else if (len > (steps.length ? steps[steps.length - 1].len : base)) steps.push({ len, t: now });
      drawn = n;
    }
    while (steps.length && now - steps[0].t >= FADE_MS) base = Math.max(base, steps.shift().len);
    if (steps.length) fadeTail(now);
    if (follow) scroller.scrollTop = scroller.scrollHeight;
  }

  // The characters past base belong to the step that showed them, and fade in from its time.
  function fadeTail(now) {
    const walker = document.createTreeWalker(textEl, NodeFilter.SHOW_TEXT);
    const nodes = [];
    for (let pos = 0, n; (n = walker.nextNode()); pos += n.data.length) if (pos + n.data.length > base) nodes.push([n, pos]);
    for (const [node, start] of nodes) {
      const frag = document.createDocumentFragment();
      let at = 0;
      if (start < base) { frag.append(node.data.slice(0, base - start)); at = base - start; }
      for (const s of steps) {
        const end = Math.min(node.data.length, s.len - start);
        if (end <= at) continue;
        const span = document.createElement("span");
        const k = Math.min(1, (now - s.t) / FADE_MS);
        span.style.opacity = (1 - (1 - k) * (1 - k)).toFixed(3); // ease out
        span.textContent = node.data.slice(at, end);
        frag.append(span);
        at = end;
      }
      if (at < node.data.length) frag.append(node.data.slice(at));
      node.replaceWith(frag);
    }
  }

  function drawStatus(now, revealing) {
    const writing = activity === "Writing" && target;
    const quiet = writing && (revealing || now - lastGrowth < QUIET_MS);
    status.classList.toggle("hidden", !!(quiet || finishing));
    let text;
    if (GENERIC.has(activity)) {
      if (now - wordAt > ROTATE_MS) { if (wordAt) word = (word + 1) % WORDS.length; wordAt = now; }
      text = WORDS[word] + "…";
    } else text = activity;
    if (text !== labelText) {
      labelText = text;
      // One span a letter, each brightening a moment later than the one before: a glint that runs along
      // the words and never dims them (the text keeps its own colour between glints).
      label.replaceChildren(...[...text].map((ch, i, all) => {
        const s = document.createElement("span");
        s.textContent = ch;
        s.style.animationDelay = Math.round((i / all.length) * GLINT_RUN_MS) + "ms";
        return s;
      }));
      label.title = text;
      label.classList.remove("ai-swap"); void label.offsetWidth; label.classList.add("ai-swap"); // fade the new words in
    }
  }

  requestAnimationFrame(frame);
  return { el, set, finish, destroy: () => { alive = false; } };
}
