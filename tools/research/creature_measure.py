"""Measure creature models in the 2004scape data: size in tiles, triangles, colours, and how each is
split into rigid parts (its vertex labels: every label group moves as one piece when it animates).
Prints numbers only; nothing is copied out of the data (CLAUDE.md, docs/research/04-osrs-assets.md).
Model reader from castle_measure.py, extended to read the vertex labels.

Usage: py -I creature_measure.py <Server/data/src> model_2937_npc [model_... ...]
"""
import collections, os, struct, sys

T = 128.0                                   # model units to a tile


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
    vlabels = off if hasLabels == 1 else -1
    if hasLabels == 1: off += vc
    if hasAlpha == 1: off += fc
    fverts = off; off += lfo
    fcol = off; off += fc * 2
    off += tfc * 6
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
    labels = [b[vlabels + v] for v in range(vc)] if vlabels >= 0 else [0] * vc
    # the game's frame: x across, y down, z forward; ours: x across, y forward, z up, in tiles
    verts = [(X[i] / T, Z[i] / T, -Y[i] / T) for i in range(vc)]
    return dict(verts=verts, faces=faces, cols=cols, labels=labels)


def measure(m):
    vs = m["verts"]
    ext = [(min(v[k] for v in vs), max(v[k] for v in vs)) for k in range(3)]
    parts = collections.defaultdict(list)
    for v, l in zip(vs, m["labels"]):
        parts[l].append(v)
    return dict(tris=len(m["faces"]), verts=len(vs), colours=len(set(m["cols"])),
                width=ext[0][1] - ext[0][0], length=ext[1][1] - ext[1][0], height=ext[2][1] - ext[2][0],
                parts={l: (len(p), tuple(round(min(v[k] for v in p), 2) for k in range(3)),
                           tuple(round(max(v[k] for v in p), 2) for k in range(3))) for l, p in sorted(parts.items())})


if __name__ == "__main__":
    src = sys.argv[1]
    for name in sys.argv[2:]:
        m = measure(load(os.path.join(src, "models", "npc", name + ".ob2")))
        print("%s: %d tris, %d verts, %d colours; %.2f long, %.2f wide, %.2f high (tiles); %d parts"
              % (name, m["tris"], m["verts"], m["colours"], m["length"], m["width"], m["height"], len(m["parts"])))
        for l, (n, lo, hi) in m["parts"].items():
            print("   part %3d: %3d verts  x %5.2f..%5.2f  y %5.2f..%5.2f  z %5.2f..%5.2f" % (l, n, lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
