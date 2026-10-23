"""Floor overlays and underlays per level in Varrock castle's box (map square 50_54), with overlay ids
read one-based (the map stores id + 1). Numbers only. Usage: py -I castle_floors.py <Server/data/src>"""
import collections, os, re, sys
SRC = sys.argv[1]
flo = {}
for line in open(os.path.join(SRC, "pack", "flo.pack"), encoding="utf-8"):
    if "=" in line:
        i, n = line.strip().split("=", 1); flo[int(i)] = n
c = collections.defaultdict(collections.Counter); u = collections.defaultdict(collections.Counter)
sec = None
for line in open(os.path.join(SRC, "maps", "m50_54.jm2"), encoding="utf-8"):
    if line.startswith("===="):
        sec = line.strip("= \n"); continue
    m = re.match(r"(\d+) (\d+) (\d+): (.*)", line)
    if sec != "MAP" or not m: continue
    lv, x, z = int(m.group(1)), 3200 + int(m.group(2)), 3456 + int(m.group(3))
    if not (3200 <= x <= 3232 and 3455 <= z <= 3500): continue
    for tok in m.group(4).split():
        if tok[0] == "o": c[lv][flo.get(int(tok[1:].split(";")[0]) - 1)] += 1
        if tok[0] == "u": u[lv][tok[1:]] += 1
for lv in sorted(c): print("overlay (id-1) level", lv, c[lv].most_common(5))
for lv in sorted(u): print("underlay raw level", lv, u[lv].most_common(3))
