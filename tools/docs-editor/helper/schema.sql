-- The helper's database (Cloudflare D1). Discord holds the comments' words; this holds what Discord
-- can't: which page and passage each comment is on, its kind and status, and who wrote the messages the
-- Codex posted (a webhook posts them under the person's name). Apply: npx wrangler d1 execute rth-codex --remote --file schema.sql

CREATE TABLE IF NOT EXISTS comments (
  id TEXT PRIMARY KEY,          -- the forum post's thread id
  page TEXT,                    -- null: unfiled (started in Discord), or scratch
  kind TEXT NOT NULL DEFAULT 'note',
  status TEXT NOT NULL DEFAULT 'open',
  anchor TEXT NOT NULL DEFAULT '{}',
  tags TEXT NOT NULL DEFAULT '[]',
  created TEXT NOT NULL,
  updated TEXT NOT NULL,
  last_msg TEXT,                -- the thread's last message id when its messages were last read
  thread TEXT NOT NULL DEFAULT '[]'  -- the messages as the Codex shows them (rebuilt from Discord)
);
CREATE INDEX IF NOT EXISTS comments_updated ON comments(updated);

CREATE TABLE IF NOT EXISTS posted (
  msg_id TEXT PRIMARY KEY,      -- a message the Codex posted through the webhook
  thread_id TEXT NOT NULL,
  by TEXT NOT NULL,             -- 'person' or 'agent'
  user_id TEXT, name TEXT, avatar TEXT,
  text TEXT NOT NULL,           -- what they wrote (the posted message adds a quote and a link)
  run TEXT                      -- an agent's run (session, cost, edits), as JSON
);

CREATE TABLE IF NOT EXISTS config (k TEXT PRIMARY KEY, v TEXT NOT NULL);
