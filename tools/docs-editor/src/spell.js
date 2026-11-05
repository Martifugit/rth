// Spelling, checked by the Codex itself rather than the browser (whose dictionary follows the
// browser's language, so a Norwegian Chrome underlines all of the English docs). US English, plus:
//   - words the docs use again and again (R'th's names: Pennick, Lowe, phlegm, Cruor...), and
//   - the shared word list, docs/.codex/words.txt ("Add to dictionary"). It's in the docs, so the Codex
//     sends it like comments and a word anyone adds is accepted in everyone's Codex. Git merges it line
//     by line (docs/.codex/.gitattributes, merge=union), so two people adding words at once never clash.
// Older Codexes kept the list on each computer (`legacy`): those words move into the shared list once.
// Node only: server.js answers the page's questions with it.

import fsp from "node:fs/promises";
import path from "node:path";
import nspell from "nspell";
import dictionary from "dictionary-en";

const WORD = /[\p{L}][\p{L}'’]*/gu;
const REPEATED = 3; // a word the docs use this often is taken to be meant
const HEADER = "# Words Codex's spelling check accepts, for everyone (one a line). \"Add to dictionary\" adds here.";

/** wordsFile: the shared list. legacy: older per-computer lists, merged in once and then renamed.
 *  onWrite(): the shared list was written (server.js has it sent). */
export function makeSpell({ wordsFile, texts, legacy = [], onWrite = () => {} }) {
  const spell = nspell(dictionary);
  let project = new Set();     // the shared list, lowercase
  let corpus = null;           // lowercase words the docs use often
  const cache = new Map();     // word -> correct?

  async function readList(file) {
    try { return (await fsp.readFile(file, "utf8")).split(/\r?\n/).map((w) => w.trim().toLowerCase()).filter((w) => w && !w.startsWith("#")); }
    catch { return null; }
  }
  async function save() {
    const list = [...project].sort((a, b) => a.localeCompare(b));
    await fsp.mkdir(path.dirname(wordsFile), { recursive: true });
    await fsp.writeFile(wordsFile, HEADER + "\n" + list.join("\n") + "\n", "utf8");
    onWrite();
  }
  async function loadProject() {
    project = new Set((await readList(wordsFile)) || []);
    // An older Codex's own list: its words into the shared list, once (the old file is renamed after).
    let moved = false;
    for (const file of legacy) {
      const old = file && (await readList(file));
      if (!old) continue;
      for (const w of old) if (!project.has(w)) { project.add(w); moved = true; }
      try { await fsp.rename(file, file.replace(/\.txt$/, "") + "-moved-to-docs.txt"); } catch {}
    }
    if (moved) await save();
    cache.clear();
  }
  let ready = loadProject();

  async function loadCorpus() {
    const counts = new Map();
    for (const text of (await texts()).values()) {
      const stripped = text.replace(/```[\s\S]*?```/g, " ").replace(/`[^`\n]*`/g, " ").replace(/\]\([^)]*\)/g, "] ");
      for (const m of stripped.matchAll(WORD)) {
        const w = m[0].replace(/’/g, "'").replace(/'s?$/i, "").toLowerCase();
        if (w.length > 2) counts.set(w, (counts.get(w) || 0) + 1);
      }
    }
    corpus = new Set([...counts].filter(([, n]) => n >= REPEATED).map(([w]) => w));
  }

  /** Whether a word (as written in the page) is spelt right. */
  function correct(raw) {
    const word = raw.replace(/’/g, "'").replace(/^'+|'+$/g, "");
    if (word.length < 2 || /\d/.test(word)) return true;
    if (/^[A-Z]{2,}s?$/.test(word)) return true;                 // acronyms: GDD, NPC, XP
    if (/[a-z][A-Z]/.test(word)) return true;                    // code-ish: camelCase
    if (cache.has(word)) return cache.get(word);
    const lower = word.toLowerCase(), bare = lower.replace(/'s$/, "");
    const ok = spell.correct(word) || spell.correct(lower) || spell.correct(bare)
      || project.has(lower) || project.has(bare) || (corpus && (corpus.has(lower) || corpus.has(bare)));
    cache.set(word, ok);
    return ok;
  }

  return {
    async check(words) {
      await ready;
      if (!corpus) await loadCorpus();
      return words.filter((w) => typeof w === "string" && !correct(w));
    },
    suggest(word) {
      // Two letters swapped is the commonest typo ("teh"), and the dictionary's own suggestions
      // rank it poorly: try those first.
      const swaps = [];
      for (let i = 0; i < word.length - 1; i++) {
        const w = word.slice(0, i) + word[i + 1] + word[i] + word.slice(i + 2);
        if (w !== word && spell.correct(w) && !swaps.includes(w)) swaps.push(w);
      }
      return [...new Set([...swaps, ...spell.suggest(word)])].slice(0, 6);
    },
    async add(word) {
      await ready;
      const w = String(word).trim();
      if (!w || /\s/.test(w)) return false;
      project.add(w.toLowerCase());
      cache.clear();
      await save();
      return true;
    },
    /** The shared list changed on disk (others' words, fetched): read it again. */
    reload() {
      ready = ready.then(async () => { project = new Set((await readList(wordsFile)) || []); cache.clear(); });
      return ready;
    },
    /** The docs changed: count their words again (lazily). */
    invalidate() { corpus = null; cache.clear(); },
  };
}

/**
 * The words of a page as the editor's check sees them (src/spellcheck.js walks the editor's document):
 * not code, link targets, web addresses, paths, file names or the frontmatter. For checks made without
 * the editor (R'th AI's check of what it changed). [{ word, line }].
 */
export function wordsIn(markdown) {
  const out = [];
  let fence = false;
  const lines = String(markdown || "").split("\n");
  let i = 0;
  if (/^---[ \t]*$/.test(lines[0] || "")) { i = 1; while (i < lines.length && !/^---[ \t]*$/.test(lines[i])) i++; i++; }
  for (; i < lines.length; i++) {
    const raw = lines[i];
    if (/^\s*(```|~~~)/.test(raw)) { fence = !fence; continue; }
    if (fence) continue;
    const line = raw.replace(/`[^`\n]*`/g, " ").replace(/\]\([^)]*\)/g, "] ").replace(/<[^>]+>/g, " ")
      .replace(/\b(https?:\/\/|www\.)\S+/g, " ").replace(/\S+\.(md|json|cs|js|png|jpg|webp|fbx|txt|unity)\b/gi, " ").replace(/\S*[\/]\S*/g, " ");
    for (const m of line.matchAll(WORD)) out.push({ word: m[0], line: i + 1 });
  }
  return out;
}
