# Saving

*Draft (2026-11-03). From `CLAUDE.md` (persistence).*

Everything about a character is kept: where they stand, their pack, worn gear and bank, skills,
quests and story flags, task points, the fishing log, and their window and action bar layouts. It is
saved when they log out (or their connection drops), every three minutes, and when the server stops.

- **Logging back in** puts you where you left off. If that spot is no longer open ground (the world
  changed under you), you start at the spawn.
- **Nothing in the world is saved yet** besides players: things players change in the world get
  designed when such features exist.
