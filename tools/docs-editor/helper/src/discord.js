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

/** Post a message in a channel as the bot. Links aren't unfurled into previews. */
export async function post(env, channel, content) {
  const r = await fetch(`${API}/channels/${channel}/messages`, {
    method: "POST", headers: bot(env),
    body: JSON.stringify({ content, flags: 4, allowed_mentions: { parse: [] } }),
  });
  if (!r.ok) throw new Error(`Discord post failed (${r.status}): ${await r.text()}`);
  return r.json();
}
