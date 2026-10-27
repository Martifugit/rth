# The Altar of Martyrs: style study and design

**Status:** design agreed (2026-10-27, decisions below); blockout next. The first passes
(`tools/art/blender/buildings/cruor/altar.py`, commits up to `a56a7e6`) were rejected as "a cheap
Valheim copy": random out-of-true shapes and ad-hoc figures. This is the design-first redo.

## 1. What RuneScape's blood places teach

Studied: the Theatre of Blood and Ver Sinhaza (Old School), Castle Drakan (RuneScape 3), and the
owner's `images/rs3-drakan-*`. They set the **technique** for crafted, sacred pieces; the forms
below are our own.

1. **Deliberate, symmetric, bold.** Plinths, pools, arches and stairs are clean and symmetric,
   often radially (a round pool, a ring of spikes pointing out from it, an emblem on the floor).
   Nothing is randomly out of true; wear lives in a few chosen places (a pool of spilt blood on the
   floor), never in the geometry.
2. **Every edge is worked.** Steps, rims, plinths and caps are stacks of simple mouldings: a slab, a
   bevel, a shaft, a cap. The bevels catch the light as pale bands, which is most of what makes
   them read as crafted.
3. **Pale trim on dark masses.** The big surfaces are plain, a mid or dark stone; their edges
   (rails, frames, step noses, rims) are a lighter stone. Colour stays within three or four values
   per piece.
4. **One signature motif, repeated on everything.** Drakan sets a blood-drop gem in a pointed frame
   on every pillar, gate, banner and brazier; the Theatre runs rows of red elongated diamonds along
   every wall. The repetition is what makes a place feel like one faith's.
5. **Ornament is painted or inset, not modelled small.** The Theatre's red diamonds are flat insets;
   Drakan's floor emblem is a flat inlay with a single raised gem. Modelled ornament is few and big:
   spikes along a top, a ring of points, a crest over a door.
6. **Spikes as a crown.** Rows of short triangular spikes on wall tops, pool rims and braziers are
   the blood places' silhouette element (the Theatre's merlons are spikes).
7. **Statues are proper figures.** Well-proportioned, sculpted with intent, pale stone, on a round
   moulded plinth, posed for drama (a raised arm, a weapon held out). The pose and the setting carry
   them, not surface detail.
8. **Blood is clean.** A pool is a flat red disc in a bevelled rim; a channel is a single inlaid
   line from an emblem down the stairs; a spill is a flat star-shaped splash on the floor.

## 2. The design

**Read at the game camera, from the nave (about 10 tiles away):** three pale martyrs back to back on
a tall dark pedestal, their swords raised and fanned, standing in a round red pool ringed with
outward spikes, on a stepped round dais; one red line running to it from the nave. Everything else
is secondary.

**Order of focus:** the martyrs (pale, highest, the only figures), then the pool and the line
(the only red), then the pedestal and its masks, then the dais and braziers (dark, plain).

**Size, against the player (1.8):**

| Part | Size |
| --- | --- |
| Dais | three round steps, 11 tiles across, 0.75 high, pale-nosed steps |
| Pool | 6 tiles across, a bevelled rim 0.3 high, twelve short spikes pointing out round it |
| Pedestal | 2.4 across, 3.2 high above the pool: a moulded base with four spouts, a shaft with the motif inset on each face, a moulded cap |
| Martyrs | 3.0 tall (1.7 times a player), on the pedestal's cap |
| Braziers | four, 1.6 tall, a spike crown round the bowl |

**Materials (four values, flat palette colours, as dressing):**
- dark stone for the pedestal and the dais's risers;
- pale stone for the trims (step noses, rims, mouldings) and the martyrs;
- the faith's red for the pool, the line and the motif's insets, nothing else;
- dark iron for the swords' grips, the braziers and the spikes; one bronze accent on the guards.

**The motif (proposed; our own, not Drakan's drop):** *the sealed drop*, an upright red drop under a
pale bar laid across its top, like wax under a seal: an oath sealed in blood, which is what Troth is
made of. It goes on the pedestal's faces, the braziers, the dais's risers at the four quarters,
and later on the cathedral's banners and the monuments, so the whole faith carries it.

**The martyrs:** designed by hand (decision 4), as clean symmetric figures: a hooded robe with a
flared hem, broad shoulders, one hand on the heart, the other raising a broad sword.

**The spouts:** four plain spouts on the pedestal's base, each with the motif over it, each pouring
a single clean line of blood into the pool.

**Blood:** the pool, one inlaid line from the nave down the dais's steps to the pool (not three
gutters), and a few flat splashes on the floor where it is worn.

## 3. How it is made

1. **Blockout:** every part at its final size in plain grey, the martyrs posed, judged from the game
   camera against the references side by side. Approve before detail.
2. **Detail:** mouldings and bevels, the motif's insets, the spikes, the masks, colour.
3. **In place:** set in the cathedral's east end and judged from the nave.
4. **Rules:** what passes goes into `tools/art/blender/README.md` as the method for centrepieces.

## Decided by the owner (2026-10-27)

1. **The motif is the sealed drop**, to be tried.
2. **Three martyrs:** the empire wants people to aspire to martyrdom.
3. **The altar must look outstanding on its own**, judged alone, not in the cathedral (which is not
   yet true to style either). The faces of the fallen go: the blood leaves the pedestal through four
   plain spouts carrying the motif.
4. **Statues from the posed player body are deferred** until we know more of what makes a good
   outcome; the martyrs are designed by hand for now.
