"""Measure Varrock castle in the 2004scape data: which pieces it is built from, their sizes,
triangles, colours and textures, and the whole castle's totals. Prints numbers only; nothing is
copied out of the data. Model reader from rpgish tools/research/osrs_assets.py, extended to read
the texture-mapping triangles.

Usage: py -I castle_measure.py <Server/data/src> x0 x1 z0 z1
"""
import collections, glob, math, os, re, struct, sys, statistics

SRC = sys.argv[1]
X0, X1, Z0, Z1 = map(int, sys.argv[2:6])
T = 128.0
SUFFIX = {0: "1", 1: "2", 2: "3", 3: "4", 4: "q", 5: "q", 6: "q", 7: "q", 8: "q", 9: "1",
          10: "8", 11: "8", 12: "a", 13: "s", 14: "d", 15: "f", 16: "g", 17: "h",
          18: "z", 19: "x", 20: "c", 21: "v", 22: "0"}
SHAPE = {0: "wall", 1: "wall corner (diag)", 2: "wall L", 3: "wall corner (square)", 4: "wall decor", 5: "wall decor",
         6: "wall decor", 7: "wall decor", 8: "wall decor", 9: "wall diagonal", 10: "centrepiece", 11: "centrepiece diag",
         12: "roof", 13: "roof", 14: "roof", 15: "roof", 16: "roof", 17: "roof flat", 18: "roof edge", 19: "roof edge",
         20: "roof edge", 21: "roof edge", 22: "ground decor"}


def gsmart(b, p):
    return (b[p] - 0x40, p + 1) if b[p] < 0x80 else (((b[p] << 8) | b[p + 1]) - 0xc000, p + 2)


def load(fn):
    b = open(fn, "rb").read()
    t = b[-18:]
    vc, fc = struct.unpack(">HH", t[0:4]); tfc = t[4]
    hasInfo, prio, hasAlpha, hasSkins, hasLabels = t[5:10]
    lx, ly, lz, lfo = struct.unpack(">HHHH", t[10:18])
    off = 0
    vflags = off; off += vc
    forient = off; off += fc
    if prio == 255: off += fc
    if hasSkins == 1: off += fc
    finfo = off if hasInfo == 1 else -1
    if hasInfo == 1: off += fc
    if hasLabels == 1: off += vc
    falpha = off if hasAlpha == 1 else -1
    if hasAlpha == 1: off += fc
    fverts = off; off += lfo
    fcol = off; off += fc * 2
    ftex = off; off += tfc * 6
    px, py, pz = off, off + lx, off + lx + ly
    X = []; Y = []; Z = []; x = y = z = 0
    for v in range(vc):
        f = b[vflags + v]
        if f & 1: d, px = gsmart(b, px); x += d
        if f & 2: d, py = gsmart(b, py); y += d
        if f & 4: d, pz = gsmart(b, pz); z += d
        X.append(x); Y.append(y); Z.append(z)
    faces = []; a = bb = c = last = 0; p = fverts
    for f in range(fc):
        o = b[forient + f]
        if o == 1:
            d, p = gsmart(b, p); a = d + last; last = a
            d, p = gsmart(b, p); bb = d + last; last = bb
            d, p = gsmart(b, p); c = d + last; last = c
        elif o == 2:
            bb = c; d, p = gsmart(b, p); c = d + last; last = c
        elif o == 3:
            a = c; d, p = gsmart(b, p); c = d + last; last = c
        elif o == 4:
            a, bb = bb, a; d, p = gsmart(b, p); c = d + last; last = c
        faces.append((a, bb, c))
    cols = [struct.unpack(">H", b[fcol + 2 * f:fcol + 2 * f + 2])[0] for f in range(fc)]
    info = [b[finfo + f] for f in range(fc)] if finfo >= 0 else [0] * fc
    alpha = [b[falpha + f] for f in range(fc)] if falpha >= 0 else [0] * fc
    tex = [struct.unpack(">HHH", b[ftex + 6 * i:ftex + 6 * i + 6]) for i in range(tfc)]
    return dict(X=X, Y=Y, Z=Z, faces=faces, cols=cols, info=info, alpha=alpha, tex=tex)


def hsl(c):
    return (c >> 10) & 63, (c >> 7) & 7, c & 127


def measure(m):
    X, Y, Z, F = m["X"], m["Y"], m["Z"], m["faces"]
    area = 0.0; edges = []
    for a, b, c in F:
        u = (X[b] - X[a], Y[b] - Y[a], Z[b] - Z[a]); v = (X[c] - X[a], Y[c] - Y[a], Z[c] - Z[a])
        cr = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
        area += 0.5 * math.sqrt(sum(k * k for k in cr))
        for p, q in ((a, b), (b, c), (c, a)):
            edges.append(math.dist((X[p], Y[p], Z[p]), (X[q], Y[q], Z[q])))
    textured = [i for i in m["info"] if i & 2]
    cols = collections.Counter(c for c, i in zip(m["cols"], m["info"]) if not i & 2)
    texids = collections.Counter(c for c, i in zip(m["cols"], m["info"]) if i & 2)
    # texture repeat: the mapping triangle's P->M and P->N lengths, in tiles
    reps = []
    for (P, M, N) in m["tex"]:
        if max(P, M, N) < len(X):
            reps.append((math.dist((X[P], Y[P], Z[P]), (X[M], Y[M], Z[M])) / T,
                         math.dist((X[P], Y[P], Z[P]), (X[N], Y[N], Z[N])) / T))
    return dict(faces=len(F), h=(max(-y for y in Y) - min(-y for y in Y)) / T if Y else 0,
                top=max(-y for y in Y) / T if Y else 0,
                w=(max(X) - min(X)) / T if X else 0, d=(max(Z) - min(Z)) / T if Z else 0,
                area=area / T / T, edge=statistics.median(edges) / T if edges else 0,
                colours=cols, textured=len(textured), texids=texids, reps=reps,
                flat=sum(1 for i in m["info"] if i & 1), alpha=sum(1 for a in m["alpha"] if a))


names = {}
for line in open(os.path.join(SRC, "pack", "loc.pack"), encoding="utf-8"):
    if "=" in line:
        i, n = line.strip().split("=", 1); names[int(i)] = n
texnames = {}
for line in open(os.path.join(SRC, "pack", "texture.pack"), encoding="utf-8"):
    if "=" in line:
        i, n = line.strip().split("=", 1); texnames[int(i)] = n

# loc configs: model, and the lighting/recolour fields
cfg = {}; cur = None
for fn in glob.glob(os.path.join(SRC, "scripts", "**", "*.loc"), recursive=True):
    for line in open(fn, encoding="utf-8", errors="ignore"):
        line = line.strip()
        m = re.match(r"\[(.+)\]", line)
        if m:
            cur = m.group(1); cfg.setdefault(cur, {}); continue
        if cur and "=" in line:
            k, v = line.split("=", 1); cfg[cur].setdefault(k, []).append(v)

locs = []
for mx in range(X0 // 64, X1 // 64 + 1):
    for mz in range(Z0 // 64, Z1 // 64 + 1):
        fn = os.path.join(SRC, "maps", f"m{mx}_{mz}.jm2")
        sec = None
        for line in open(fn, encoding="utf-8"):
            if line.startswith("===="):
                sec = line.strip("= \n"); continue
            if sec == "LOC":
                m = re.match(r"(\d+) (\d+) (\d+): (\d+) (\d+) (\d+)", line)
                if m:
                    lv, x, z, i, s, r = map(int, m.groups())
                    wx, wz = mx * 64 + x, mz * 64 + z
                    if X0 <= wx <= X1 and Z0 <= wz <= Z1:
                        locs.append((lv, wx, wz, i, s, r))

cache = {}
def model_for(name, shape):
    base = (cfg.get(name, {}).get("model") or [None])[0]
    if not base:
        return None, None
    for suf in (SUFFIX.get(shape), "8", "1", "0", "q"):
        fn = os.path.join(SRC, "models", "loc", f"{base}_{suf}.ob2")
        if os.path.exists(fn):
            if fn not in cache:
                cache[fn] = measure(load(fn))
            return os.path.basename(fn), cache[fn]
    return base, None

total = 0; by_name = collections.Counter(); faces_by_name = collections.Counter(); by_shape = collections.Counter()
faces_by_shape = collections.Counter(); per_level = collections.Counter(); used = {}
footprint = set(); missing = collections.Counter(); tex_faces = 0; area = 0.0
colour_faces = collections.Counter()
for (lv, x, z, i, s, r) in locs:
    n = names.get(i, f"#{i}")
    fn, st = model_for(n, s)
    if st is None:
        missing[n] += 1; continue
    by_name[n] += 1; faces_by_name[n] += st["faces"]; by_shape[SHAPE[s]] += 1; faces_by_shape[SHAPE[s]] += st["faces"]
    per_level[lv] += st["faces"]; total += st["faces"]; tex_faces += st["textured"]; area += st["area"]
    used[n] = (fn, st)
    colour_faces.update(st["colours"])
    if lv == 0:
        footprint.add((x, z))

print(f"castle box x {X0}-{X1}, z {Z0}-{Z1}: {len(locs)} placed locs, {total} triangles, "
      f"{tex_faces} textured ({100*tex_faces/max(1,total):.0f}%), surface {area:.0f} sq tiles "
      f"-> {total/max(1,area):.1f} tris per sq tile; tiles with a loc on level 0: {len(footprint)}")
print("triangles by level:", dict(per_level))
print("\nby shape (count, triangles):")
for k, v in by_shape.most_common():
    print(f"  {k:24s} {v:5d} {faces_by_shape[k]:7d}")
print("\nby loc (count, triangles each, total, h x w x d tiles, median edge, colours, textured faces, textures, repeat sizes, config):")
for n, k in faces_by_name.most_common(40):
    fn, st = used[n]
    c = cfg.get(n, {})
    extra = {kk: c[kk][0] for kk in ("sharelight", "ambient", "contrast", "retex1s", "retex1d", "recol1s", "recol1d", "width", "length", "wallwidth") if kk in c}
    texs = {texnames.get(t, t): v for t, v in st["texids"].items()}
    reps = sorted({(round(a, 2), round(b, 2)) for a, b in st["reps"]})[:4]
    cols = [hsl(cc) for cc, _ in st["colours"].most_common(4)]
    print(f"  {n:32s} x{by_name[n]:4d} {st['faces']:4d} {k:6d}  {st['h']:.2f}x{st['w']:.2f}x{st['d']:.2f} e{st['edge']:.2f} "
          f"top{st['top']:.2f} cols{cols} tex{st['textured']} {texs} rep{reps} {extra} [{fn}]")
print("\nmissing models:", dict(missing))
print("\nmost used untextured colours (h 0-63, s 0-7, l 0-127): ",
      [(hsl(c), k) for c, k in colour_faces.most_common(10)])
