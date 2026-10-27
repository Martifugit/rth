# The Bell: the first quest

**Status:** skeleton (2026-10-27). The premise is canon (`docs/lore.md` VI); the steps, names and
rewards are a first shape, to be filled in when Tolling reaches stage 3. Working title.

## The premise

The night before the game begins, Tolling's bell rang for a death. Fishers had pulled a stranger out
of Wennow, and the church laid the body out in the dead-house under the bell tower. In the morning
the stranger got up: the player.

There is no opening scene. New players appear in the dead-house and can walk straight out and go
mining. The quest is there for whoever asks what happened, and the town keeps nudging: the
bell-ringer, the ledger, Edda.

**Sealed:** why the bell rang for the player and why they came back (connected to the revision; how
is not decided). The quest must not answer it. It ends with more questions than it started with.

## What it does

- Tells the player who they are in this world, as far as Tolling knows: a stranger the bell tolled for.
- Teaches the first systems, one a step: talking, the bank, consumables, walking out, combat.
- Introduces the town's people and three threads: the church (the ledger), the occult (Corvin's
  curiosity) and the Dreaming (Edda), and points at the ferryman.

## Steps

| # | Where | Who | What happens | Teaches |
| --- | --- | --- | --- | --- |
| 1 | The dead-house | the bell-ringer (new) | He finds the "body" up and about, and sends the player to the clerk to set it right. | talking, the quest journal |
| 2 | The church's strongbox | Clerk Ambrose | The ledger has the player down as dead; what was found on the body went into the strongbox. | the bank: the first withdrawal is your own belongings |
| 3 | The apothecary | Corvin Sallow | He looks the player over, far more curious than an apothecary should be, and sells the first potions. | consumables, shops |
| 4 | Wennow's south shore (s-wennow-south) | the fishers (new, or one of them) | Where the player was pulled out. Rats on the way; something left at the spot (an idea: an obol, the ferryman's fee, never spent). | walking out, the first fight |
| 5 | The pond | Edda Marsh | Asleep on her feet, she says something only the ferryman would know. | leads into Dreamless |
| 6 | The church | Clerk Ambrose, the bell-ringer | The entry is struck out. The bell-ringer: the bell has never been wrong before. | end: reward, Dreamless opens |

## Hooks it leaves

- **The ferryman** already knows the player: his conversation at the first death gets a line for it
  (death.json's dialogue, a flag set by this quest or by the first death).
- **Someone comes looking**, later and elsewhere: not someone who knew the player, nor someone who
  wishes them well, but someone who knows a little more than they do and gives it out in pieces.
- **The bell** itself: why it rang, who else it has rung for. At the end the bell-ringer mentions
  that Tolling's bell was cast as one of a pair, and the other hangs in the belfry now drowned in
  Knellmere (s-belfry, mid game): the first pull east (`docs/areas/tolling.md`, "Outwards").

## Needs

- Tolling at stage 2: the chapel, the bell tower and the dead-house (`docs/areas/tolling.md`).
- The spawn in the dead-house for a new character (the layout's start place, or a first-login spawn).
- New people: the bell-ringer, the fishers. Existing: Clerk Ambrose, Corvin Sallow, Edda Marsh.
- Built with what exists: quests.json stages and objectives, dialogue, uses.json, flags. No new
  mechanism expected.
