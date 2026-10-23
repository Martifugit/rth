"""Print an ASCII map of which tiles hold castle-ish locs, per level, around Varrock castle.
Usage: py -I castle_extent.py <Server/data/src>"""
import os, re, sys

SRC = sys.argv[1]
names = {}
for line in open(os.path.join(SRC, "pack", "loc.pack"), encoding="utf-8"):
    if "=" in line:
        i, n = line.strip().split("=", 1)
        names[int(i)] = n

locs = []                                     # (level, wx, wz, id, shape, rot)
for mx in (49, 50, 51):
    for mz in (53, 54, 55):
        fn = os.path.join(SRC, "maps", f"m{mx}_{mz}.jm2")
        if not os.path.exists(fn):
            continue
        sec = None
        for line in open(fn, encoding="utf-8"):
            if line.startswith("===="):
                sec = line.strip("= \n")
                continue
            if sec == "LOC":
                m = re.match(r"(\d+) (\d+) (\d+): (\d+) (\d+) (\d+)", line)
                if m:
                    lv, x, z, i, s, r = map(int, m.groups())
                    locs.append((lv, mx * 64 + x, mz * 64 + z, i, s, r))

X0, X1, Z0, Z1 = int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
for lv in range(4):
    grid = {}
    for (l, x, z, i, s, r) in locs:
        if l != lv or not (X0 <= x <= X1 and Z0 <= z <= Z1):
            continue
        n = names.get(i, "?")
        ch = "C" if "castle" in n else ("w" if "wall" in n else ".")
        if grid.get((x, z)) != "C":
            grid[(x, z)] = ch
    if not grid:
        continue
    print(f"--- level {lv}")
    for z in range(Z1, Z0 - 1, -1):
        print(f"{z} " + "".join(grid.get((x, z), " ") for x in range(X0, X1 + 1)))
