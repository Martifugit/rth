// Discord, as the R'th Codex bot: who someone is in the server, and posting messages.

const API = "https://discord.com/api/v10";
const bot = (env) => ({ Authorization: "Bot " + env.DISCORD_BOT_TOKEN, "Content-Type": "application/json", "User-Agent": "DiscordBot (https://github.com/Martifugit/rth, 1)" });

/** Someone in the R'th server: their name there, avatar and whether they have the Codex role. Null if not in it. */
export async function member(env, userId) {
  const r = await fetch(`${API}/guilds/${env.DISCORD_GUILD_ID}/members/${userId}`, { headers: bot(env) });
  if (r.status === 404) return null;
  if (!r.ok) throw new Error(`Discord member lookup failed (${r.status})`);
  const m = await r.json();
  const u = m.user;
  return {
    id: u.id,
    name: m.nick || u.global_name || u.username,
    username: u.username,
    avatar: avatarUrl(env, u, m),
    codex: m.roles.includes(env.DISCORD_ROLE_ID),
  };
}

export function avatarUrl(env, user, m) {
  if (m?.avatar) return `https://cdn.discordapp.com/guilds/${env.DISCORD_GUILD_ID}/users/${user.id}/avatars/${m.avatar}.png?size=64`;
  if (user.avatar) return `https://cdn.discordapp.com/avatars/${user.id}/${user.avatar}.png?size=64`;
  const i = Number((BigInt(user.id) >> 22n) % 6n);
  return `https://cdn.discordapp.com/embed/avatars/${i}.png`;
}

/**
 * Keep one message pinned in a channel, replacing the one pinned before (`previous`, if any): the latest
 * Codex release in #codex, so it doesn't drown in "what changed". Discord then posts "... pinned a
 * message" in the channel; that's removed, the release itself was the news. Needs the bot's Pin
 * Messages (and Manage Messages, for the notice) in the channel; without them it says so.
 */
export async function pinOnly(env, channel, id, previous) {
  if (previous && previous !== id) await fetch(`${API}/channels/${channel}/pins/${previous}`, { method: "DELETE", headers: bot(env) }).catch(() => {});
  const r = await fetch(`${API}/channels/${channel}/pins/${id}`, { method: "PUT", headers: bot(env) });
  if (!r.ok) throw new Error(r.status === 403 ? "The bot may not pin messages in that channel: give it Pin Messages (and Manage Messages) there." : `Discord pin failed (${r.status}): ${await r.text()}`);
  try {
    const recent = await (await fetch(`${API}/channels/${channel}/messages?limit=10`, { headers: bot(env) })).json();
    for (const m of Array.isArray(recent) ? recent : [])
      if (m.type === 6 && m.message_reference?.message_id === id) await fetch(`${API}/channels/${channel}/messages/${m.id}`, { method: "DELETE", headers: bot(env) });
  } catch { /* tidying Discord's pin notice is optional */ }
}

/** The bot's latest message in a channel whose text starts with `start` (among the last 100), or null. */
export async function findOwn(env, channel, start) {
  const r = await fetch(`${API}/channels/${channel}/messages?limit=100`, { headers: bot(env) });
  if (!r.ok) throw new Error(`Discord read failed (${r.status})`);
  const list = await r.json();
  return list.find((m) => m.author?.bot && typeof m.content === "string" && m.content.startsWith(start)) || null;
}

/** Post a message in a channel as the bot. Links aren't unfurled into previews. */
export async function post(env, channel, content) {
  const r = await fetch(`${API}/channels/${channel}/messages`, {
    method: "POST", headers: bot(env),
    body: JSON.stringify({ content, flags: 4, allowed_mentions: { parse: [] } }),
  });
  if (!r.ok) throw new Error(`Discord post failed (${r.status}): ${await r.text()}`);
  return r.json();
}
