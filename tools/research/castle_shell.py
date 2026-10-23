import collections, os, sys, statistics
sys.argv = sys.argv[:6]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import io, contextlib
with contextlib.redirect_stdout(io.StringIO()):
    import castle_measure as cm
SHELL = {"bigstone_castlewall", "castlebattlements", "cross_castleedge", "castlearrowslit", "roof_redslate", "roofedge_redslate",
         "brickwall", "brickwallskew", "castlewall", "churchwindow", "poshbigwindow", "orangecurtainedposhbigwindow", "elfroofbeam"}
n = collections.Counter(); f = collections.Counter(); tex = 0; tot = 0; edges = []; area = 0; kinds = set()
for (lv, x, z, i, s, r) in cm.locs:
    nm = cm.names.get(i, "")
    if nm not in SHELL and not ("door" in nm or "stair" in nm or "ladder" in nm):
        continue
    fn, st = cm.model_for(nm, s)
    if not st: continue
    n[nm] += 1; f[nm] += st["faces"]; tot += st["faces"]; tex += st["textured"]; area += st["area"]
    edges += [st["edge"]] * st["faces"]; kinds.add(fn)
print(f"shell: {sum(n.values())} pieces, {len(kinds)} distinct models, {tot} triangles, {100*tex/tot:.0f}% textured, "
      f"surface {area:.0f} sq tiles -> {tot/area:.1f} tris/sq tile, median edge (by face) {statistics.median(edges):.2f}")
for k, v in f.most_common(): print(f"  {k:30s} x{n[k]:4d} {v:6d} ({v/n[k]:.0f} each)")
