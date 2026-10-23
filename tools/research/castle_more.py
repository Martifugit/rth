"""More Varrock castle numbers: shell versus furnishing, level heights, floors on each level, the
wall texture, and the shapes of the battlement and wall pieces. Numbers only.
Usage: py -I castle_more.py <Server/data/src> x0 x1 z0 z1"""
import collections, os, re, struct, sys, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import castle_measure as cm  # noqa: E402  (runs its report too; fine)

SRC = cm.SRC
X0, X1, Z0, Z1 = cm.X0, cm.X1, cm.Z0, cm.Z1
SHELL = ("wall", "battlement", "edge", "roof", "slit", "window", "stair", "door", "ladder", "tower", "pillar", "column", "arch")

shell = dressing = 0
shell_n = collections.Counter()
for (lv, x, z, i, s, r) in cm.locs:
    n = cm.names.get(i, "")
    fn, st = cm.model_for(n, s)
    if not st:
        continue
    if any(k in n for k in SHELL) and "railing" not in n and "shield" not in n:
        shell += st["faces"]; shell_n[n] += st["faces"]
    else:
        dressing += st["faces"]
print(f"\n== shell {shell} triangles, dressing {dressing}")
for n, k in shell_n.most_common(25):
    print(f"   {n:32s} {k}")

# level heights and floors from the MAP section: lines 'level x z: h<height> o<overlay>... u<underlay>'
heights = collections.defaultdict(list); over = collections.defaultdict(collections.Counter)
for mx in range(X0 // 64, X1 // 64 + 1):
    for mz in range(Z0 // 64, Z1 // 64 + 1):
        sec = None
        for line in open(os.path.join(SRC, "maps", f"m{mx}_{mz}.jm2"), encoding="utf-8"):
            if line.startswith("===="):
                sec = line.strip("= \n"); continue
            if sec != "MAP":
                continue
            m = re.match(r"(\d+) (\d+) (\d+): (.*)", line)
            if not m:
                continue
            lv, x, z, rest = int(m.group(1)), mx * 64 + int(m.group(2)), mz * 64 + int(m.group(3)), m.group(4)
            if not (X0 <= x <= X1 and Z0 <= z <= Z1):
                continue
            for tok in rest.split():
                if tok[0] == "h":
                    heights[lv].append(int(tok[1:]))
                if tok[0] == "o":
                    over[lv][tok[1:].split(";")[0]] += 1
print("\n== tile height values per level (raw units; the client multiplies by 8? see note):")
for lv, hs in sorted(heights.items()):
    print(f"   level {lv}: n={len(hs)} min {min(hs)} median {sorted(hs)[len(hs)//2]} max {max(hs)}")
flo = {}
for line in open(os.path.join(SRC, "pack", "flo.pack"), encoding="utf-8"):
    if "=" in line:
        i, nme = line.strip().split("=", 1); flo[i] = nme
print("== overlay floors per level (tiles):")
for lv, c in sorted(over.items()):
    print(f"   level {lv}: " + ", ".join(f"{flo.get(k, k)} {v}" for k, v in c.most_common(6)))

# the wall texture: size and colours
def png_pixels(fn):
    b = open(fn, "rb").read(); p = 8; idat = b""; pal = None
    while p < len(b):
        ln = struct.unpack(">I", b[p:p + 4])[0]; t = b[p + 4:p + 8]; d = b[p + 8:p + 8 + ln]; p += 12 + ln
        if t == b"IHDR": w, h, depth, ctype = struct.unpack(">IIBB", d[:10])
        elif t == b"PLTE": pal = [tuple(d[i:i + 3]) for i in range(0, len(d), 3)]
        elif t == b"IDAT": idat += d
    raw = zlib.decompress(idat)
    bpp = {2: 3, 6: 4, 3: 1, 0: 1, 4: 2}[ctype]
    stride = w * bpp; rows = []; prev = bytearray(stride); i = 0
    for y in range(h):
        f = raw[i]; line = bytearray(raw[i + 1:i + 1 + stride]); i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0; up = prev[x]; c = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + up) & 255
            elif f == 3: line[x] = (line[x] + (a + up) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(up - c), abs(a - c), abs(a + up - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else up if pb <= pc else c)) & 255
        rows.append(bytes(line)); prev = line
    px = []
    for row in rows:
        for x in range(w):
            if ctype == 3: px.append(pal[row[x]])
            else: px.append(tuple(row[x * bpp:x * bpp + 3]))
    return w, h, px

for tname in ("wall", "roof", "mossybricks"):
    fn = os.path.join(SRC, "textures", tname + ".png")
    if not os.path.exists(fn):
        continue
    w, h, px = png_pixels(fn)
    lum = [0.299 * r + 0.587 * g + 0.114 * b for r, g, b in px]
    cols = collections.Counter(px)
    s = sorted(lum)
    # brick courses: rows whose mean luminance is a local low (mortar lines)
    rowmean = [sum(lum[y * w:(y + 1) * w]) / w for y in range(h)]
    colmean = [sum(lum[y * w + x] for y in range(h)) / h for x in range(w)]
    dark_rows = [y for y in range(h) if rowmean[y] < sorted(rowmean)[h // 6]]
    print(f"\n== texture {tname}: {w}x{h}, {len(cols)} colours, luminance p10 {s[len(s)//10]:.0f} "
          f"median {s[len(s)//2]:.0f} p90 {s[9*len(s)//10]:.0f}; darkest rows (mortar?) {dark_rows[:20]}")
    print("   top colours:", cols.most_common(6))

# shapes: vertex heights and spans of the castle wall and battlement pieces
for name, suf in (("bigstone_castlewall", "1"), ("castlebattlements", "1"), ("cross_castleedge", "2"), ("castlearrowslit", "q")):
    base = cm.cfg[name]["model"][0]
    m = cm.load(os.path.join(SRC, "models", "loc", f"{base}_{suf}.ob2"))
    ys = sorted({round(-y / 128, 2) for y in m["Y"]}); xs = sorted({round(x / 128, 2) for x in m["X"]}); zs = sorted({round(z / 128, 2) for z in m["Z"]})
    print(f"\n== {name}: {len(m['faces'])} tris; heights {ys}; x {xs}; z {zs}")
