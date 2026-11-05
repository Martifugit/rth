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

-- "What changed" in #codex, batched per person (src/notices.js): pages queued by pushes, posted by the
-- timer once their author has been quiet a while.
CREATE TABLE IF NOT EXISTS notice_pages (
  author TEXT NOT NULL,         -- the name the notice shows (the Codex's "(Discord)" dropped)
  path TEXT NOT NULL,
  status TEXT NOT NULL,         -- added, modified or removed, across the whole batch
  first_at INTEGER NOT NULL,    -- ms: first queued, and last changed
  last_at INTEGER NOT NULL,
  origin TEXT NOT NULL,         -- the helper's address, for the /open links
  PRIMARY KEY (author, path)
);
CREATE TABLE IF NOT EXISTS notice_why (
  author TEXT NOT NULL,         -- a commit message a person wrote (not the Codex's "Docs: ..."), with the batch
  msg TEXT NOT NULL,
  at INTEGER NOT NULL
);

-- @R'th AI in a comment thread (src/mentions.js): each tag once, waiting for a Codex that answers, then
-- claimed by one (conn, until: its lease), then answered. told: "offline right now" was said.
CREATE TABLE IF NOT EXISTS mentions (
  msg_id TEXT PRIMARY KEY,      -- the message that tagged R'th AI
  thread TEXT NOT NULL,         -- its post
  asker_id TEXT, asker_name TEXT,
  status TEXT NOT NULL DEFAULT 'waiting',
  conn TEXT, until INTEGER,
  told INTEGER NOT NULL DEFAULT 0,
  at TEXT NOT NULL
);
