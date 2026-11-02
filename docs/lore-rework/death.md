# Death

What happens when a player dies. The realm of the dead (p-name to come) becomes a real place
over time; v1 is deliberately small.

## v1 (decided)

- **The place.** A small, very dark landing in the realm of the dead. It has a **bank** and the
  **ferryman**, and nothing else.
- **The ferryman** takes you back to the world of the living. He holds the gear you died with
  and returns it for a fee.
- **Paying with coin.** The fee scales with the value of what you died with.
- **Paying with an obol.** An obol is an item you must be **carrying when you die**: it costs an
  inventory slot. It is relatively expensive, but much cheaper than the coin fee for a player
  dying in end-game gear. It is never a choice in his menu: if you died with one, he says he
  noticed it and took it as payment. So an obol withdrawn from the bank here does nothing.
- **No paying, no gear.** If you can't pay, he takes you back without it. There is no owing him.
  He keeps unpaid gear for **about 24 hours**; pay within that time and it's yours, after it
  it's gone.
- **No kept items.** Everything you die with goes to the ferryman. (RuneScape's kept items exist
  for PvP, which R'th doesn't have.)
- **Obols are sold** in one or more fitting places (to be decided with the world).
- **The long way back.** Instead of the ferry, a player can try to walk back through the realm
  of the dead. It is **really difficult at every stage of the game**, by design:
  - about **three minutes** of a hard route; what matters is progressing fast enough while
    keeping enough blood to reach the end, not a timer as such;
  - **damage scales with your health** (max blood), so a high-level player has no edge;
  - succeed and you get **your gear back for free**.
- **The walker (p-name).** At the end of the long way waits someone who tried it in life and
  failed. They have spent their afterlife helping those who make it, and they hand back your
  gear.

## The long way back (D3, decided 2026-11-02)

The owner's choices, and the first numbers (all data: `trials.json`, `objects.json`, `npcs.json`,
`world/regions/realm_long_way.json`).

- **Where.** The leaning stones on the Landing's south shore (`soul_gate`) open the way: a
  96x96 region south of the Landing ("The Long Way", `realm_long_way`), a causeway through dark
  still water in four stretches, about 250 tiles walked.
- **Who may go.** Only someone the ferryman holds something for, and only empty-handed (the
  grave chest is right there). Running is not possible on the way: the cold is in your legs.
  At walking pace a clean walk takes about two and a half minutes.
- **The cold.** 0.7% of max blood every tick, and blood doesn't return by itself while walking.
- **Shades** (`shade`): the dead who didn't make it. They can't be fought. They drift back and
  forth on set paths: across the ash flats (stretch 2), across the last causeway on two spurs
  (stretch 4) and round the long way through the stone field. Come within two tiles and one
  follows you a little way and strikes for 15% of max blood.
- **Grasping hands** (`grasping_hands`): unseen until they take you. They hold you for 4 ticks
  (the cold keeps biting) and take 8%. They lie thickest on the short line through the stone field
  (stretch 3), with a few on the last causeway. The long way round the field has none, but has a
  shade.
- **Soul lanterns.** Four, one between each pair of stretches. Each warms you once an attempt, for
  35% of max blood.
- **Failing.** Dying on the way wakes you on the Landing again. The ferryman's hold is untouched and
  his clock keeps running (dying again restarts it, as any death does). You can walk again as often
  as you like while he holds something.
- **The walker** waits on the far shore. Reach them and they give back everything the ferryman
  holds, for nothing, and you return as if the fee were paid (death.json `returnTo`).
- **Pace.** `LongWayPaceTests` walks the route with a bot that knows where the hands are and waits
  for shades. It arrives with 20% of its blood. Without the lanterns, or after two minutes'
  dawdling at the start, it dies. A first walk, not knowing where the hands are, should usually fail.
- Hymns and Defence's abilities still help a little against shades; the cold and the hands ignore
  wards.

## Open

| Question | Notes |
| --- | --- |
| The coin fee: what share of value, and how cheap gear must be to go free. | Tuned in data. |
| Where obols are sold, and their price. | With the world. |
| ~~The long way back's route: its hazards and what it looks like.~~ | Decided: see above. |
| The names: the realm of the dead, the ferryman, the walker. | |
