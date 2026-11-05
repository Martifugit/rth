-- 0.4.1: /ask and "Ask R'th AI" are tags too (src/interactions.js). Run once on the live database:
--   npx wrangler@4 d1 execute rth-codex --remote --file migrations/0002-mentions-channel.sql -y
ALTER TABLE mentions ADD COLUMN kind TEXT NOT NULL DEFAULT 'forum';
ALTER TABLE mentions ADD COLUMN token TEXT;
ALTER TABLE mentions ADD COLUMN question TEXT;
ALTER TABLE mentions ADD COLUMN ref_msg TEXT;
