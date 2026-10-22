"""
Measures how dense the 2004-era world is: where its banks, workstations, altars, shops, resources
and settlements are, how far apart, and how much of the land is empty. Pass 0 of the area plan.

    git clone --depth 1 https://github.com/2004scape/Server.git
    python3 tools/research/osrs_world_density.py Server/data/src out.json

The main continent only: map squares x 36-53, z 45-62 (the rows from z 70 are minigame and
event rooms, from z 100 dungeons). Tiles are world tiles (map square * 64 + local).
"""
import os, re, sys, glob, json, math, collections
ROOT = sys.argv[1]

def pack(name):
    m = {}
    for line in open(os.path.join(ROOT, 'pack', name)):
        if '=' in line:
            i, n = line.strip().split('=', 1); m[int(i)] = n
    return m

def configs(pattern):
    defs = {}; cur = None
    for f in glob.glob(os.path.join(ROOT, 'scripts', '**', pattern), recursive=True):
        for line in open(f, errors='ignore'):
            line = line.strip(); m = re.match(r'^\[(.+)\]$', line)
            if m: cur = defs.setdefault(m.group(1), {}); continue
            if cur is not None and '=' in line:
                k, v = line.split('=', 1); cur.setdefault(k, v)
    return defs

loc_names, npc_names = pack('loc.pack'), pack('npc.pack')
locdefs, npcdefs = configs('*.loc'), configs('*.npc')
def loc_label(i):
    n = loc_names.get(i, ''); return n, locdefs.get(n, {}).get('name', '').lower()
def npc_label(i):
    n = npc_names.get(i, ''); d = npcdefs.get(n, {})
    return n, d.get('name', '').lower(), ' '.join(v.lower() for k, v in d.items() if k.startswith('op'))

WATER = {5, 6, 18, 40}
land = set(); walls_by_zone = collections.Counter(); objs_by_zone = collections.Counter()
places = collections.defaultdict(list)   # category -> [(x, z)]
for path in glob.glob(os.path.join(ROOT, 'maps', 'm*_*.jm2')):
    mx, mz = map(int, re.findall(r'm(\d+)_(\d+)', path)[0])
    if not (36 <= mx <= 53 and 45 <= mz <= 62): continue
    sec = None
    for line in open(path):
        line = line.strip()
        if line.startswith('===='): sec = line.strip('= '); continue
        if not line: continue
        head, _, rest = line.partition(':'); parts = head.split()
        lv, x, z = map(int, parts); wx, wz = mx * 64 + x, mz * 64 + z
        if lv != 0: continue
        if sec == 'MAP':
            o = re.search(r'\bo(\d+)', rest); u = re.search(r'\bu(\d+)', rest)
            if (u or o) and not (o and int(o.group(1)) in WATER): land.add((wx, wz))
        elif sec == 'LOC':
            v = rest.split(); lid = int(v[0]); shape = int(v[1]) if len(v) > 1 else 10
            n, d = loc_label(lid); zone = (wx // 8, wz // 8)
            if shape in (0, 1, 2, 3, 9) and not re.search(r'cave|rock|cliff|fence|hedge', n): walls_by_zone[zone] += 1
            if shape in (10, 11): objs_by_zone[zone] += 1
            cat = None
            if d == 'bank booth': cat = 'bank'
            elif d == 'anvil': cat = 'anvil'
            elif d == 'furnace': cat = 'furnace'
            elif d in ('range', 'cooking range'): cat = 'range'
            elif d in ('altar', 'chaos altar'): cat = 'altar'
            elif d == 'spinning wheel': cat = 'spinning wheel'
            elif d == 'loom': cat = 'loom'
            elif d in ('potter\'s wheel', 'pottery oven'): cat = 'pottery'
            elif d in ('well', 'fountain', 'sink', 'water pump'): cat = 'water source'
            elif d in ('trapdoor', 'cave entrance', 'manhole') or (d == 'ladder' and 'down' in locdefs.get(n, {}).get('op1', '').lower()): cat = 'way down'
            elif re.match(r'^(copper|tin|iron|coal|gold|mithril|adamantite|runite|clay|silver)rock', n): cat = 'ore:' + re.match(r'^([a-z]+)rock', n).group(1)
            elif n in ('tree', 'tree2', 'oaktree', 'willow_tree', 'willow_tree2', 'mapletree', 'yewtree', 'magictree'): cat = 'tree:' + n.rstrip('2').replace('_tree', '').replace('tree', '') if n not in ('tree', 'tree2') else 'tree:normal'
            if cat: places[cat].append((wx, wz))
        elif sec == 'NPC':
            nid = int(rest.split()[0]); n, d, ops = npc_label(nid)
            if d == 'banker': places['banker'].append((wx, wz))
            elif 'trade' in ops: places['shopkeeper'].append((wx, wz))
            elif d == 'fishing spot': places['fishing spot'].append((wx, wz))

def sites(points, radius):
    """Groups points within `radius` tiles of each other into sites; returns site centres."""
    pts = list(set(points)); parent = list(range(len(pts)))
    def find(i):
        while parent[i] != i: parent[i] = parent[parent[i]]; i = parent[i]
        return i
    grid = collections.defaultdict(list)
    for i, (x, z) in enumerate(pts): grid[(x // radius, z // radius)].append(i)
    for i, (x, z) in enumerate(pts):
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for j in grid[(x // radius + dx, z // radius + dz)]:
                    if j > i and max(abs(pts[j][0] - x), abs(pts[j][1] - z)) <= radius: parent[find(i)] = find(j)
    groups = collections.defaultdict(list)
    for i, p in enumerate(pts): groups[find(i)].append(p)
    return [(sum(p[0] for p in g) / len(g), sum(p[1] for p in g) / len(g), len(g)) for g in groups.values()]

pct = lambda a, p: sorted(a)[min(len(a) - 1, int(p / 100 * len(a)))] if a else None
cheb = lambda a, b: max(abs(a[0] - b[0]), abs(a[1] - b[1]))
land_list = sorted(land); sample = land_list[::97]

out = {'land_tiles': len(land)}
xs = [p[0] for p in land]; zs = [p[1] for p in land]
out['land_bbox'] = [min(xs), max(xs), min(zs), max(zs)]
stats = {}
for cat in sorted(places):
    s = sites(places[cat], 6 if not cat.startswith(('ore', 'tree', 'fishing')) else 8)
    nn = []
    for i, a in enumerate(s):
        d = min((cheb(a, b) for j, b in enumerate(s) if j != i), default=None)
        if d is not None: nn.append(round(d))
    reach = [min(cheb(t, b) for b in s) for t in sample] if s else []
    stats[cat] = dict(pieces=len(places[cat]), sites=len(s), site_spacing_median=pct(nn, 50),
                      land_within_50=round(100 * sum(r <= 50 for r in reach) / len(reach), 1) if reach else 0,
                      land_within_100=round(100 * sum(r <= 100 for r in reach) / len(reach), 1) if reach else 0,
                      farthest_land=max(reach) if reach else None)
out['services'] = stats

# Settlements: 8x8 zones with at least 10 wall pieces (a house or more), joined edge to edge.
built = {z for z, n in walls_by_zone.items() if n >= 10}
seen = set(); towns = []
TOWNS = {'Varrock': (3212, 3424), 'Lumbridge': (3222, 3218), 'Falador': (2964, 3378), 'East Ardougne': (2662, 3305),
         'West Ardougne': (2530, 3300), 'Draynor': (3093, 3244), 'Al Kharid': (3293, 3174), 'Catherby': (2813, 3447),
         "Seers' Village / Camelot": (2725, 3484), 'Yanille': (2605, 3093), 'Port Sarim': (3023, 3208), 'Rimmington': (2957, 3214),
         'Edgeville': (3093, 3493), 'Barbarian Village': (3082, 3420), 'Taverley': (2895, 3438), 'Burthorpe': (2899, 3544),
         'Canifis': (3496, 3488), 'Brimhaven': (2760, 3178), 'Gnome Stronghold': (2461, 3444), 'Port Khazard': (2660, 3160),
         'Witchaven': (2720, 3285), 'Shilo Village': (2852, 2955), 'Tai Bwo Wannai': (2790, 3065), 'Entrana': (2834, 3335),
         'Musa Point': (2914, 3173), 'Hemenster': (2636, 3440), 'Rellekka': (2650, 3680), 'Ice Mountain / Dwarven Mine': (3010, 3450)}
for z0 in built:
    if z0 in seen: continue
    comp = []; stack = [z0]; seen.add(z0)
    while stack:
        z = stack.pop(); comp.append(z)
        for n in ((z[0] + 1, z[1]), (z[0] - 1, z[1]), (z[0], z[1] + 1), (z[0], z[1] - 1)):
            if n in built and n not in seen: seen.add(n); stack.append(n)
    if len(comp) < 2: continue
    cx = sum(c[0] for c in comp) / len(comp) * 8 + 4; cz = sum(c[1] for c in comp) / len(comp) * 8 + 4
    name, dist = min(((n, cheb((cx, cz), p)) for n, p in TOWNS.items()), key=lambda t: t[1])
    w = (max(c[0] for c in comp) - min(c[0] for c in comp) + 1) * 8; h = (max(c[1] for c in comp) - min(c[1] for c in comp) + 1) * 8
    towns.append(dict(name=name if dist < 50 else f'unnamed at {int(cx)},{int(cz)}', built_tiles=len(comp) * 64, bbox=[w, h],
                      walls=sum(walls_by_zone[c] for c in comp)))
towns.sort(key=lambda t: -t['built_tiles'])
out['settlements'] = towns
out['settlement_count'] = len(towns)
out['built_share_of_land'] = round(100 * sum(t['built_tiles'] for t in towns) / len(land), 1)

# How busy the land is: objects (trees, rocks, furniture, stalls...) per 8x8 land zone.
land_zones = collections.Counter((x // 8, z // 8) for x, z in land)
zones = [z for z, n in land_zones.items() if n >= 32]
dens = [objs_by_zone[z] for z in zones]
out['objects_per_land_zone'] = {'empty (0)': round(100 * sum(d == 0 for d in dens) / len(dens), 1),
                                'sparse (1-4)': round(100 * sum(1 <= d <= 4 for d in dens) / len(dens), 1),
                                'moderate (5-15)': round(100 * sum(5 <= d <= 15 for d in dens) / len(dens), 1),
                                'dense (16+)': round(100 * sum(d >= 16 for d in dens) / len(dens), 1)}
underground = [p for p in glob.glob(os.path.join(ROOT, 'maps', 'm*_*.jm2')) if int(re.findall(r'm\d+_(\d+)', p)[0]) >= 100]
out['underground_map_squares'] = len(underground)
json.dump(out, open(sys.argv[2], 'w'), indent=1)
print(json.dumps(out, indent=1))
