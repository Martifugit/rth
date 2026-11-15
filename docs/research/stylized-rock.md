# Stylized rock: how it is made, and what the target looks like

Research for the ground and rock test (`tools/art/blender/ground/GroundTest.blend`), 2026-11-14. The
target is RuneScape 3's reworked regions; the reference shots are the owner's Havenhythe screenshots
(`havenhythe-1` to `-4`). Style only: our rock gets its own shapes (CLAUDE.md, "No Jagex assets").

## 1. How practitioners make it

**One rock asset**
- Sculpt at the size it is used. Three passes: **primary** (the big mass), **secondary** (large flat
  planes cut with trim/flatten brushes), **tertiary** (a few cracks and chips). Keep some flat surfaces;
  don't over-polish; no micro detail in the sculpt (it goes into a tiling detail texture).
- Decimate to a low poly (under ~10k triangles for a hero rock), bake normals and AO from the sculpt.
- Texture with a tiling rock material plus **world-space layers**: grass/moss/sand on every upward face,
  colour variation projected in world space so jammed-together rocks read as one formation.
- Variants come from the main rock (cut, re-sculpt), never new random shapes: "all interesting shapes
  come from the 1 main rock".

**A cliff from rocks**
- Block out first (ground, big forms, focal point) and judge it before detail.
- Primary shapes must read at a glance; vary sizes within each tier.
- Pieces support each other along one direction (one layering); random rotations never cohere.
- Avoid long straight walls, 90-degree angles, narrow flat slabs; walls run in big arcs.
- Don't spam one rock; leave rest areas.
- Scale uniformly, within about +-20%; non-uniform scale is "usually a big no-no".
- Each piece dips into the ground at its foot and into the bench at its top, which hides the seams.
- Steep (80-90 degree) terrain gets a triplanar rock material; Genshin's mountains are rock meshes
  blended into terrain.
- Check values by thresholding a screenshot.

Sources: [Frozenbyte, Level Art: Composition](https://wiki.frozenbyte.com/index.php/Level_Art:_Composition),
[80.lv, Approaching Organic Environment Building](https://80.lv/articles/approaching-organic-environment-building),
[80.lv, Creating Natural Landscapes for Games](https://80.lv/articles/creating-natural-landscapes-for-games/),
[Unity Shader Graph, Production-Ready Rock](https://docs.unity3d.com/Packages/com.unity.shadergraph@14.0/manual/Shader-Graph-Sample-Production-Ready-Rock.html),
[Unity Discussions, cliff terrain blending](https://discussions.unity.com/t/how-to-achieve-this-style-of-cliff-terrain-blending/712663),
[ArtStation, ZBrush modular rock](https://www.artstation.com/artwork/6xX6V).

## 2. What the Havenhythe rock is (measured against the people in shots 2 and 4, ~1.8-2.5 m)

**Primary.** A wall or mountain reads as one body with a level top line, 4-6 m tall, running in arcs.
Above it the land carries on as a grassy or earthy bench.

**Secondary: chunky blocks.** 2-5 m across. Each is a solid with only 2-4 big planes visible, close to a
box or wedge but never square: faces slightly tilted, corners knocked off. All blocks in one wall share
a layering direction (shot 3: slabs dipping ~25 degrees, parallel). Blocks overlap and interlock with dark
gaps between; 2-3 tiers make a wall, upper ones set back.

**Ground slabs.** Thin flat plates, 3-5 m long and 0.3-0.6 m thick, lying near-level and overlapping like
shingles, stepping down toward water (shot 4). Earth and grass fill between them.

**Rims.** Around raised grass plateaus, a thin rock lip (~0.4 m) with a rounded top edge follows the
plateau's edge in big curves (shot 1). The land sits in rock "trays".

**Tops.** Ledge tops are ground, not rock: earth and grass up to the edge, with a fringe of tall dry
golden grass (~0.5 m) along it. Pines grow from the benches.

**Tertiary.** Few. Dark cracks where plates meet, the odd chip. Faces are otherwise calm.

**Paint.**
- Each face is a soft value gradient: lighter at the top of a block, darker at its foot.
- Edges get a thin lighter line (a bevel catching light); crevices go near-black (strong occlusion).
- Surface texture is low-contrast mottling with faint pale veins, never noisy.
- Colour is a neutral light grey with a slight tint per area (warm in shot 3, green-grey in shot 4).
- Shadows are soft; the rock never goes dark overall.

**Around it.** Warm ochre earth, olive and golden grass, lush green leafy clumps by water; turquoise
clear water with white foam bands at falls; strong blue haze on distant rock.

## 3. Our process (agreed 2026-11-14), each step approved before the next

1. This breakdown.
2. Blockout: one cliff ~10 m wide, ~6 m tall, a bench above and a path below, grey masses; game camera.
3. One main block: plane cuts on a solid mass (big, then medium, then a few cracks), judged close up.
4. One shared rock material: occlusion and edges baked from the block, world-space grass on top,
   world colour variation.
5. Low poly and bake; triangle count.
6. Three or four variants cut from the main block, placed by hand into the blockout by the rules in 1.
