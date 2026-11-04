// Errors in words anyone can act on. The engine's answers carry either a sentence written for people
// ("The top pages and the templates can't be deleted.") or, when something deeper failed, a technical
// cause ("EBUSY: resource busy or locked", "The helper said 502", a bare 500). failed() keeps the first
// kind as it is and turns the second into what happened, whether the work is safe, and what to do; the
// technical cause goes to the console for whoever fixes it.

const CAUSES = [
  [/EBUSY|EPERM|EACCES|resource busy|locked/i, "the file is open in another program. Close it there, then try again."],
  [/ENOSPC|no space/i, "this computer's disk is full. Free some space, then try again."],
  [/fetch failed|ECONNREFUSED|ENOTFOUND|ETIMEDOUT|EAI_AGAIN|ECONNRESET|network|reach/i, "the internet, or the service it needs, couldn't be reached. Try again in a moment."],
  [/helper said 5\d\d|Internal Server Error|Bad Gateway|Service Unavailable/i, "the online service behind it is having trouble. It's not on your side: try again in a moment."],
  [/Not signed in|said 401/i, "you're signed out. Sign in with Discord (top right), then try again."],
  [/Codex role|said 403/i, "that needs the Codex role in the R'th Discord."],
];

/** True when the engine's message was written for people, not a technical cause. */
function forPeople(r) {
  const e = String(r.error || "");
  if (!e || !r.status || r.status >= 500) return false;
  return !/^[A-Z]+[a-z]*:|E[A-Z]{3,}|Error\b|\bat \S+:\d+|The helper said \d+/.test(e);
}

/** "Couldn't save the page" + the engine's answer → one sentence for the toast. */
export function failed(what, r = {}) {
  if (forPeople(r)) return `${what}: ${r.error}`;
  const raw = String(r.error || ""); // the cause is in the words: the engine's own status is 500 for anything
  if (r.status === 0) return `${what}: the Codex's engine isn't answering. Close the Codex and open it again; your saved work is safe.`;
  console.warn(what + ":", r.status, r.error);
  for (const [re, why] of CAUSES) if (re.test(raw)) return `${what}: ${why}`;
  return `${what}: something went wrong inside the Codex. Try again; if it keeps happening, close the Codex and open it again. Your saved work is safe.`;
}
