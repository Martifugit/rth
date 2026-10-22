#!/usr/bin/env python3
"""
Measures R'th's world scale from the committed assets, without Unity.

Reads the region scenes (Unity YAML), the materials, the character FBX, the textures and the
camera defaults, and prints the tables in docs/research/scale-today.md. Standard library only.

    python3 tools/scale-report/scale_report.py            # markdown report
    python3 tools/scale-report/scale_report.py --json     # the raw numbers

In the Editor, R'th > Art > Validate Art (client/Assets/Editor/ArtValidator.cs) checks real mesh
triangle counts, colours and textures against docs/assets.md. This script exists so the scale
numbers can be re-run anywhere, including a container without Unity.
"""
import json
import math
import os
import re
import struct
import sys
import zlib
from collections import Counter, defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "client", "Assets")
SCENES = os.path.join(ASSETS, "Scenes")

# Unity's built-in meshes: (triangles, local bounds min, max).
BUILTIN_MESHES = {
    10202: ("cube", 12, (-0.5, -0.5, -0.5), (0.5, 0.5, 0.5)),
    10206: ("cylinder", 80, (-0.5, -1.0, -0.5), (0.5, 1.0, 0.5)),
    10207: ("sphere", 760, (-0.5, -0.5, -0.5), (0.5, 0.5, 0.5)),
    10208: ("capsule", 832, (-0.5, -1.0, -0.5), (0.5, 1.0, 0.5)),
    10209: ("plane", 200, (-5.0, 0.0, -5.0), (5.0, 0.0, 5.0)),
    10210: ("quad", 2, (-0.5, -0.5, 0.0), (0.5, 0.5, 0.0)),
}

# ---------------------------------------------------------------------------- Unity YAML


def parse_unity_yaml(path):
    """Returns {fileID: (classID, text)} for every document in a scene or asset."""
    docs = {}
    with open(path, encoding="utf-8", errors="replace") as f:
        text = f.read()
    for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- |\Z)", text, re.S | re.M):
        docs[int(m.group(2))] = (int(m.group(1)), m.group(3))
    return docs


def field(text, name):
    """A field's value; a nested struct (`name:` then indented lines) comes back as "k: v, k: v"."""
    m = re.search(r"^(\s*)" + re.escape(name) + r":[ ]?(.*)$", text, re.M)
    if not m:
        return None
    if m.group(2).strip():
        return m.group(2).strip()
    indent = len(m.group(1))
    lines = []
    for line in text[m.end():].split("\n")[1:]:
        if len(line) - len(line.lstrip()) <= indent:
            break
        lines.append(line.strip())
    return ", ".join(lines)


def vec(s):
    return {k: float(v) for k, v in re.findall(r"(\w+): (-?[\d.eE+-]+)", s)}


def ref_id(s):
    m = re.search(r"fileID: (-?\d+)", s or "")
    return int(m.group(1)) if m else 0


def ref_guid(s):
    m = re.search(r"guid: ([0-9a-f]{32})", s or "")
    return m.group(1) if m else None


def guid_index():
    """Maps asset GUIDs to paths, for scripts, materials and textures."""
    index = {}
    for base in (ASSETS, os.path.join(ROOT, "tools", "unity-map-tools")):
        for dirpath, _, files in os.walk(base):
            for name in files:
                if not name.endswith(".meta"):
                    continue
                with open(os.path.join(dirpath, name), encoding="utf-8", errors="replace") as f:
                    m = re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M)
                if m:
                    index[m.group(1)] = os.path.join(dirpath, name[:-5])
    return index


# ---------------------------------------------------------------------------- maths


def quat_rotate(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    # v' = v + 2w(q x v) + 2 q x (q x v)
    cx, cy, cz = y * vz - z * vy, z * vx - x * vz, x * vy - y * vx
    cx2, cy2, cz2 = y * cz - z * cy, z * cx - x * cz, x * cy - y * cx
    return (vx + 2 * (w * cx + cx2), vy + 2 * (w * cy + cy2), vz + 2 * (w * cz + cz2))


class Node:
    def __init__(self, gid, name):
        self.id = gid
        self.name = name
        self.parent = None
        self.children = []
        self.pos = (0.0, 0.0, 0.0)
        self.rot = (0.0, 0.0, 0.0, 1.0)
        self.scale = (1.0, 1.0, 1.0)
        self.mesh = None          # builtin mesh id
        self.materials = []
        self.scripts = set()
        self.fields = {}          # script name -> MonoBehaviour text
        self.collider = False

    def to_world(self, p):
        """Transforms a local point to world space through every parent."""
        node = self
        while node is not None:
            p = (p[0] * node.scale[0], p[1] * node.scale[1], p[2] * node.scale[2])
            p = quat_rotate(node.rot, p)
            p = (p[0] + node.pos[0], p[1] + node.pos[1], p[2] + node.pos[2])
            node = node.parent
        return p

    def world_bounds(self):
        if self.mesh not in BUILTIN_MESHES:
            return None
        _, _, lo, hi = BUILTIN_MESHES[self.mesh]
        pts = [self.to_world((x, y, z)) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
        return tuple(min(p[i] for p in pts) for i in range(3)), tuple(max(p[i] for p in pts) for i in range(3))

    def ancestors(self):
        n = self.parent
        while n is not None:
            yield n
            n = n.parent

    def has_script_above(self, script):
        return script in self.scripts or any(script in a.scripts for a in self.ancestors())

    def descendants(self):
        for c in self.children:
            yield c
            yield from c.descendants()

    def path(self):
        names = [self.name] + [a.name for a in self.ancestors()]
        return "/".join(reversed(names))


def load_scene(path, guids):
    docs = parse_unity_yaml(path)
    nodes = {}
    for fid, (cls, text) in docs.items():
        if cls == 1:
            nodes[fid] = Node(fid, field(text, "m_Name") or "")
    transforms = {}
    for fid, (cls, text) in docs.items():
        go = ref_id(field(text, "m_GameObject"))
        node = nodes.get(go)
        if node is None:
            continue
        if cls == 4:  # Transform
            transforms[fid] = node
            p = vec(field(text, "m_LocalPosition"))
            r = vec(field(text, "m_LocalRotation"))
            s = vec(field(text, "m_LocalScale"))
            node.pos = (p["x"], p["y"], p["z"])
            node.rot = (r["x"], r["y"], r["z"], r["w"])
            node.scale = (s["x"], s["y"], s["z"])
            node._father = ref_id(field(text, "m_Father"))
        elif cls == 33:  # MeshFilter
            node.mesh = ref_id(field(text, "m_Mesh"))
        elif cls == 23:  # MeshRenderer
            node.materials = re.findall(r"guid: ([0-9a-f]{32})", text.split("m_Materials:")[1].split("m_StaticBatchInfo")[0])
        elif cls in (65, 64, 136, 135):  # colliders
            node.collider = "m_IsTrigger: 0" in text
        elif cls == 114:  # MonoBehaviour
            script = guids.get(ref_guid(field(text, "m_Script")), "")
            name = os.path.splitext(os.path.basename(script))[0]
            node.scripts.add(name)
            node.fields[name] = text
    for node in nodes.values():
        father = transforms.get(getattr(node, "_father", 0))
        if father is not None:
            node.parent = father
            father.children.append(node)
    return nodes


# ---------------------------------------------------------------------------- materials


def material_info(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        text = f.read()
    m = re.search(r"- _BaseColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)", text)
    color = tuple(float(v) for v in m.groups()) if m else None
    textures = set(re.findall(r"m_Texture: \{fileID: 2800000, guid: ([0-9a-f]{32})", text))
    return {"name": os.path.splitext(os.path.basename(path))[0], "color": color, "textures": textures}


def hsl(color):
    r, g, b = color
    hi, lo = max(color), min(color)
    light = (hi + lo) / 2
    if hi == lo:
        return 0.0, 0.0, light
    d = hi - lo
    sat = d / (2 - hi - lo) if light > 0.5 else d / (hi + lo)
    if hi == r:
        hue = ((g - b) / d) % 6
    elif hi == g:
        hue = (b - r) / d + 2
    else:
        hue = (r - g) / d + 4
    return hue * 60, sat, light


# ---------------------------------------------------------------------------- textures


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", head[16:24])


# ---------------------------------------------------------------------------- FBX


def fbx_meshes(path):
    """Minimal binary FBX reader: every Geometry's vertex positions and polygon count."""
    with open(path, "rb") as f:
        data = f.read()
    if not data.startswith(b"Kaydara FBX Binary"):
        return None
    version = struct.unpack("<I", data[23:27])[0]
    wide = version >= 7500
    head = 25 if wide else 13

    def read_prop(o):
        t = chr(data[o])
        o += 1
        scalar = {"Y": ("<h", 2), "C": ("<?", 1), "I": ("<i", 4), "F": ("<f", 4), "D": ("<d", 8), "L": ("<q", 8)}
        if t in scalar:
            fmt, n = scalar[t]
            return struct.unpack(fmt, data[o:o + n])[0], o + n
        if t in "fdlib":
            length, enc, clen = struct.unpack("<III", data[o:o + 12])
            o += 12
            raw = data[o:o + clen]
            o += clen
            if enc == 1:
                raw = zlib.decompress(raw)
            fmt = {"f": "f", "d": "d", "l": "q", "i": "i", "b": "?"}[t]
            return list(struct.unpack("<%d%s" % (length, fmt), raw)), o
        if t in "SR":
            length = struct.unpack("<I", data[o:o + 4])[0]
            return data[o + 4:o + 4 + length], o + 4 + length
        raise ValueError("FBX property type " + t)

    def read_node(o):
        if wide:
            end, nprops, _ = struct.unpack("<QQQ", data[o:o + 24])
            o += 24
        else:
            end, nprops, _ = struct.unpack("<III", data[o:o + 12])
            o += 12
        name_len = data[o]
        name = data[o + 1:o + 1 + name_len].decode("ascii", "replace")
        o += 1 + name_len
        if end == 0:
            return None, o
        props = []
        for _ in range(nprops):
            v, o = read_prop(o)
            props.append(v)
        children = []
        while o < end - head:
            child, o = read_node(o)
            if child is None:
                break
            children.append(child)
        return (name, props, children), end

    nodes = []
    o = 27
    while o < len(data) - head:
        node, o = read_node(o)
        if node is None:
            break
        nodes.append(node)

    unit = 1.0
    meshes = []
    for name, props, children in nodes:
        if name == "GlobalSettings":
            for c in children:
                if c[0] != "Properties70":
                    continue
                for p in c[2]:
                    if p[1] and p[1][0] == b"UnitScaleFactor":
                        unit = float(p[1][4])
        if name != "Objects":
            continue
        for c in children:
            if c[0] != "Geometry":
                continue
            label = c[1][1].split(b"\x00")[0].decode("ascii", "replace")
            verts, polys = None, 0
            tris = 0
            for g in c[2]:
                if g[0] == "Vertices":
                    verts = g[1][0]
                elif g[0] == "PolygonVertexIndex":
                    idx = g[1][0]
                    count = 0
                    for i in idx:
                        count += 1
                        if i < 0:
                            polys += 1
                            tris += count - 2
                            count = 0
            if verts:
                pts = [tuple(verts[i:i + 3]) for i in range(0, len(verts), 3)]
                meshes.append({"name": label, "points": pts, "polygons": polys, "triangles": tris})
    return {"unit_cm": unit, "meshes": meshes}


# ---------------------------------------------------------------------------- the report


def camera_defaults():
    with open(os.path.join(ASSETS, "Scripts", "CameraRig.cs"), encoding="utf-8") as f:
        text = f.read()
    out = {}
    for key in ("pitch", "distance", "minPitch", "maxPitch", "minDistance", "maxDistance"):
        m = re.search(r"public float " + key + r" = (-?[\d.]+)f", text)
        out[key] = float(m.group(1)) if m else None
    m = re.search(r"\+ Vector3\.up \* ([\d.]+)f", text) or re.search(r"\) \+ Vector3\.up;", text)
    out["focusHeight"] = float(m.group(1)) if m and m.groups() else 1.0
    with open(os.path.join(SCENES, "Game.unity"), encoding="utf-8") as f:
        game = f.read()
    m = re.search(r"field of view: ([\d.]+)", game)
    out["fov"] = float(m.group(1)) if m else 45.0
    m = re.search(r"far clip plane: ([\d.]+)", game)
    out["far"] = float(m.group(1)) if m else None
    return out


def view(distance, pitch, fov, focus, aspect=16 / 9):
    """What one screen shows, in tiles, for an orbit camera looking at a point `focus` above the ground."""
    p = math.radians(pitch)
    cam_h = focus + distance * math.sin(p)
    cam_back = distance * math.cos(p)
    half = math.radians(fov / 2)

    def ground(angle):  # angle below horizontal of a ray; distance along the ground from the focus
        if angle <= 0:
            return math.inf
        return cam_h / math.tan(angle) - cam_back

    near = -ground(p + half)  # towards the camera
    far = ground(p - half)
    width = 2 * distance * math.tan(half) * aspect
    return {"towardsCamera": near, "beyond": far, "width": width}


def hides_within(height, pitch, focus):
    """A thing this tall, between camera and player, hides the camera's focus point within this many tiles."""
    if height <= focus:
        return 0.0
    return (height - focus) / math.tan(math.radians(pitch))


def measure():
    guids = guid_index()
    mats = {}
    for g, path in guids.items():
        if path.endswith(".mat"):
            mats[g] = material_info(path)

    report = {"scenes": {}, "camera": camera_defaults()}
    scene_files = sorted(f for f in os.listdir(SCENES) if f.endswith(".unity"))
    report["missingScenes"] = sorted(
        f[:-5] for f in os.listdir(SCENES) if f.endswith(".unity.meta") and f[:-5] not in scene_files)

    for scene in scene_files:
        nodes = load_scene(os.path.join(SCENES, scene), guids)
        meshes = [n for n in nodes.values() if n.mesh is not None]
        s = {"objects": len(nodes), "meshes": len(meshes)}

        # Region rectangle and ground.
        area = next((n for n in nodes.values() if "MapArea" in n.scripts), None)
        if area is not None:
            t = area.fields["MapArea"]
            o = vec(field(t, "origin"))
            z = vec(field(t, "size"))
            s["region"] = {"origin": (int(o["x"]), int(o["y"])), "size": (int(z["x"]), int(z["y"]))}

        # Walls (MapWall), split into building walls and fence rails by height.
        walls = []
        for n in meshes:
            if "MapWall" not in n.scripts:
                continue
            lo, hi = n.world_bounds()
            walls.append({"name": n.name, "path": n.path(), "top": hi[1], "height": hi[1] - lo[1]})
        s["walls"] = walls

        # Houses: groups with a RoofHider.
        houses = []
        for n in nodes.values():
            if "RoofHider" not in n.scripts:
                continue
            r = vec(field(n.fields["RoofHider"], "interior"))
            top = max((d.world_bounds()[1][1] for d in n.descendants() if d.mesh is not None), default=0.0)
            house = n.parent
            wall_tops = [d.world_bounds()[1][1] for d in house.descendants() if d.mesh is not None and "MapWall" in d.scripts]
            houses.append({"name": house.name, "w": int(r["width"]), "d": int(r["height"]),
                           "wall": max(wall_tops, default=0.0), "ridge": top})
        s["houses"] = houses

        # Trees: a node with a Trunk child and Leaves children.
        trees = []
        for n in nodes.values():
            names = [c.name for c in n.children]
            if "Trunk" not in names or "Leaves" not in names:
                continue
            lo = [math.inf] * 3
            hi = [-math.inf] * 3
            for c in n.children:
                b = c.world_bounds()
                if b is None:
                    continue
                for i in range(3):
                    lo[i] = min(lo[i], b[0][i])
                    hi[i] = max(hi[i], b[1][i])
            trees.append({"kind": n.name, "height": hi[1], "width": max(hi[0] - lo[0], hi[2] - lo[2])})
        s["trees"] = trees

        # Tall things: any mesh whose top is over 2.5 tiles, grouped by its parent object.
        tall = defaultdict(float)
        for n in meshes:
            lo, hi = n.world_bounds()
            if hi[1] > 2.5:
                owner = n.parent if n.parent is not None and len(n.parent.children) < 40 else n
                tall[owner.path()] = max(tall[owner.path()], hi[1])
        s["tall"] = sorted(tall.items(), key=lambda kv: -kv[1])

        # Ground: colliders that are neither walls nor blockers; highest top per tile.
        ground = {}
        for n in meshes:
            if not n.collider or n.has_script_above("MapWall") or n.has_script_above("MapBlocker"):
                continue
            lo, hi = n.world_bounds()
            if hi[1] - lo[1] > 1.0 and (hi[0] - lo[0]) * (hi[2] - lo[2]) < 4:
                continue  # a post or a column, not ground
            for x in range(math.floor(lo[0] + 0.01), math.ceil(hi[0] - 0.01)):
                for y in range(math.floor(lo[2] + 0.01), math.ceil(hi[2] - 0.01)):
                    ground[(x, y)] = max(ground.get((x, y), -math.inf), hi[1])
        if ground:
            heights = list(ground.values())
            steps = [abs(ground[(x, y)] - ground[(x + dx, y + dy)])
                     for (x, y) in ground for dx, dy in ((1, 0), (0, 1)) if (x + dx, y + dy) in ground]
            reliefs = []
            xs = [k[0] for k in ground]
            ys = [k[1] for k in ground]
            for wx in range(min(xs), max(xs) - 22, 8):
                for wy in range(min(ys), max(ys) - 22, 8):
                    window = [ground[(x, y)] for x in range(wx, wx + 24) for y in range(wy, wy + 24) if (x, y) in ground]
                    if window:
                        reliefs.append(max(window) - min(window))
            s["ground"] = {"tiles": len(ground), "min": min(heights), "max": max(heights),
                           "cliffShare": sum(1 for d in steps if d > 1.0) / max(1, len(steps)),
                           "reliefMax": max(reliefs, default=0.0),
                           "reliefMedian": sorted(reliefs)[len(reliefs) // 2] if reliefs else 0.0}

        # Models: the parent of each mesh (or the mesh itself under a big group).
        models = defaultdict(lambda: {"tris": 0, "mats": set()})
        for n in meshes:
            owner = n.parent if n.parent is not None and len(n.parent.children) < 40 else n
            m = models[owner.id]
            m["name"] = owner.name
            m["tris"] += BUILTIN_MESHES.get(n.mesh, ("?", 0))[1]
            m["mats"].update(n.materials)
        s["models"] = [{"name": m["name"], "tris": m["tris"], "colours": len(m["mats"])} for m in models.values()]
        s["materialsUsed"] = sorted({mats[g]["name"] for n in meshes for g in n.materials if g in mats})
        s["triangles"] = sum(BUILTIN_MESHES.get(n.mesh, ("?", 0))[1] for n in meshes)
        report["scenes"][scene[:-6]] = s

    # Materials: colour statistics over every material the scenes use.
    used = set()
    for s in report["scenes"].values():
        used.update(s["materialsUsed"])
    palette = [m for m in mats.values() if m["name"] in used and m["color"]]
    hsls = [hsl(m["color"]) for m in palette]
    report["palette"] = {
        "materials": len(palette),
        "warm": sum(1 for h, s_, l in hsls if s_ > 0.05 and (h < 60 or h >= 330)) / max(1, len(hsls)),
        "green": sum(1 for h, s_, l in hsls if s_ > 0.05 and 60 <= h < 170) / max(1, len(hsls)),
        "blue": sum(1 for h, s_, l in hsls if s_ > 0.05 and 170 <= h < 260) / max(1, len(hsls)),
        "grey": sum(1 for h, s_, l in hsls if s_ <= 0.05) / max(1, len(hsls)),
        "dark": sum(1 for h, s_, l in hsls if l < 0.5) / max(1, len(hsls)),
        "textured": sorted(m["name"] for m in mats.values() if m["name"] in used and m["textures"]),
    }

    # Textures in the world art (not the interface).
    textures = []
    for dirpath, _, files in os.walk(ASSETS):
        rel = os.path.relpath(dirpath, ASSETS)
        for name in files:
            if name.lower().endswith(".png"):
                size = png_size(os.path.join(dirpath, name))
                kind = "interface" if rel.startswith(os.path.join("Resources", "UI")) or rel.startswith(os.path.join("Resources", "Icons")) else "world"
                textures.append({"path": os.path.join(rel, name), "size": size, "kind": kind})
    report["textures"] = textures

    # The player model.
    fbx = fbx_meshes(os.path.join(ASSETS, "Resources", "Characters", "HumanMale.fbx"))
    if fbx:
        scale = fbx["unit_cm"] / 100.0
        ys = [p[1] * scale for m in fbx["meshes"] for p in m["points"]]
        body = [m for m in fbx["meshes"] if not m["name"].startswith("hair")]
        report["player"] = {
            "height": max(ys) - min(ys) if ys else None,
            "feet": min(ys) if ys else None,
            "parts": {m["name"]: m["triangles"] for m in fbx["meshes"]},
            "triangles": sum(m["triangles"] for m in body) + max((m["triangles"] for m in fbx["meshes"] if m["name"].startswith("hair")), default=0),
        }
    return report


def markdown(r):
    out = []
    w = out.append
    cam = r["camera"]
    p = r.get("player", {})
    ph = p.get("height") or 1.8

    w("## Player\n")
    w("| | Value |\n| --- | --- |")
    w(f"| Height (HumanMale.fbx, top of the art) | {ph:.2f} |")
    w(f"| Triangles (body kit + one hair) | {p.get('triangles')} |")
    w("| Parts | " + ", ".join(f"{k} {v}" for k, v in sorted(p.get("parts", {}).items())) + " |\n")

    for name, s in r["scenes"].items():
        w(f"## {name}\n")
        if "region" in s:
            w(f"Region origin {s['region']['origin']}, size {s['region']['size']}; {s['objects']} objects, {s['meshes']} meshes, {s['triangles']} triangles.\n")
        else:
            w(f"{s['objects']} objects, {s['meshes']} meshes, {s['triangles']} triangles.\n")
        building = [x for x in s["walls"] if x["height"] > 1.0]
        fences = [x for x in s["walls"] if x["height"] <= 1.0]
        if building:
            hs = Counter(round(x["top"], 2) for x in building)
            w("| Walls (MapWall over 1 tile) | Count by top height |\n| --- | --- |")
            w("| building walls | " + ", ".join(f"{h:.2f} × {c}" for h, c in sorted(hs.items())) + " |")
        if fences:
            hs = Counter(round(x["top"], 2) for x in fences)
            w("| fences (rail top) | " + ", ".join(f"{h:.2f} × {c}" for h, c in sorted(hs.items())) + " |")
        w("")
        if s["houses"]:
            w("| House | Footprint | Wall top | Ridge | Ridge ÷ player |\n| --- | --- | --- | --- | --- |")
            for h in s["houses"]:
                w(f"| {h['name']} | {h['w']} × {h['d']} | {h['wall']:.2f} | {h['ridge']:.2f} | {h['ridge'] / ph:.2f} |")
            w("")
        if s["trees"]:
            kinds = defaultdict(list)
            for t in s["trees"]:
                kinds[t["kind"]].append(t)
            w("| Tree kind | Count | Height (min–max) | Crown width (max) | Tallest ÷ player |\n| --- | --- | --- | --- | --- |")
            for k, ts in sorted(kinds.items()):
                hs = [t["height"] for t in ts]
                w(f"| {k} | {len(ts)} | {min(hs):.2f}–{max(hs):.2f} | {max(t['width'] for t in ts):.2f} | {max(hs) / ph:.2f} |")
            w("")
        if s["tall"]:
            w("| Taller than 2.5 tiles | Top | ÷ player |\n| --- | --- | --- |")
            for path, top in s["tall"][:12]:
                w(f"| `{path}` | {top:.2f} | {top / ph:.2f} |")
            w("")
        g = s.get("ground")
        if g:
            w(f"Ground: {g['tiles']} tiles, heights {g['min']:.2f} to {g['max']:.2f}; relief per 24 × 24 window median "
              f"{g['reliefMedian']:.2f}, max {g['reliefMax']:.2f}; cliff steps (over 1 tile) {g['cliffShare'] * 100:.1f} %.\n")
        tris = sorted(m["tris"] for m in s["models"])
        cols = sorted(m["colours"] for m in s["models"])
        if tris:
            w(f"Models (a mesh's parent object): {len(tris)}; triangles median {tris[len(tris) // 2]}, "
              f"90th percentile {tris[int(len(tris) * 0.9)]}, max {tris[-1]}; colours per model median "
              f"{cols[len(cols) // 2]}, max {cols[-1]}.\n")

    w("## Camera\n")
    w("| | Value |\n| --- | --- |")
    for k in ("distance", "minDistance", "maxDistance", "pitch", "minPitch", "maxPitch", "fov", "far", "focusHeight"):
        w(f"| {k} | {cam[k]} |")
    w("")
    w("| Camera | Distance | Pitch | Ground towards the camera | Beyond the player | Width at the player | 1.7 wall hides the focus within | 3.0 tree hides the focus within |")
    w("| --- | --- | --- | --- | --- | --- | --- | --- |")
    rows = [("default", cam["distance"], cam["pitch"]), ("closest, lowest", cam["minDistance"], cam["minPitch"]),
            ("farthest, default pitch", cam["maxDistance"], cam["pitch"]), ("default, lowest pitch", cam["distance"], cam["minPitch"])]
    for label, d, pitch in rows:
        v = view(d, pitch, cam["fov"], cam["focusHeight"])
        beyond = "horizon" if v["beyond"] == math.inf else f"{v['beyond']:.1f}"
        w(f"| {label} | {d:g} | {pitch:g}° | {v['towardsCamera']:.1f} | {beyond} | {v['width']:.1f} | "
          f"{hides_within(1.7, pitch, cam['focusHeight']):.1f} | {hides_within(3.0, pitch, cam['focusHeight']):.1f} |")
    w("")

    pal = r["palette"]
    w("## Colour and textures\n")
    w(f"Materials used by the scenes: {pal['materials']}. Warm (red to ochre) {pal['warm'] * 100:.0f} %, green "
      f"{pal['green'] * 100:.0f} %, blue {pal['blue'] * 100:.0f} %, grey {pal['grey'] * 100:.0f} %; darker than mid "
      f"{pal['dark'] * 100:.0f} %. Textured: {', '.join(pal['textured']) or 'none'}.\n")
    world = [t for t in r["textures"] if t["kind"] == "world"]
    ui = [t for t in r["textures"] if t["kind"] == "interface"]
    w(f"World textures: {len(world)} (" + ", ".join(f"`{t['path']}` {t['size'][0]}×{t['size'][1]}" for t in world if t["size"]) + ").")
    sizes = Counter(t["size"] for t in ui if t["size"])
    w(f"Interface images: {len(ui)} (" + ", ".join(f"{a}×{b} × {c}" for (a, b), c in sizes.most_common(6)) + ").\n")
    if r["missingScenes"]:
        w("Scenes with a .meta but no committed scene file (not measured): " + ", ".join(r["missingScenes"]) + ".\n")
    return "\n".join(out)


if __name__ == "__main__":
    data = measure()
    if "--json" in sys.argv:
        def clean(o):
            if isinstance(o, set):
                return sorted(o)
            if isinstance(o, float) and math.isinf(o):
                return None
            raise TypeError(type(o))
        print(json.dumps(data, default=clean, indent=1))
    else:
        print(markdown(data))
