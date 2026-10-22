"""
Profiles RuneScape 3's world from the wiki data rs3_fetch.py downloads: per region, what there is
to do and at which stage (quests by difficulty, bosses, minigames and D&Ds, dungeons, shops,
banks, lodestones), per city the same within reach, and how far apart the main services are.
Pass 0 of the area plan.

    python3 tools/research/rs3_analyse.py <data dir> out.json
"""
import json, os, re, sys, math, collections
D = sys.argv[1]
load = lambda n: json.load(open(os.path.join(D, n + '.json')))
L, Q, S, M, C, CAT = load('locations'), load('quests'), load('shops'), load('monsters'), load('coordinates'), load('categories')

REGIONS = ['Misthalin', 'Asgarnia', 'Kandarin', 'Karamja', 'Desert', 'Morytania', 'Fremennik', 'Tirannwn', 'Wilderness', 'Anachronia', 'Havenhythe']
def region_of(text):
    """A single league region from a region field, or None for mixed or missing ones."""
    if not text: return None
    t = text.strip().lower()
    for r in REGIONS:
        if t == r.lower(): return r
    return None

# Positions: one per page, on the surface, inside the main map (instanced copies sit far away).
pos = {}
for c in C:
    if c.get('plane', 0) not in (0, None): continue
    x, y = (c.get('x') or [None])[0], (c.get('y') or [None])[0]
    if x is None or y is None or not (1000 < x < 6400 and 1000 < y < 6400): continue
    pos.setdefault(c['page_name'], (x, y))

loc_region = {l['page_name']: region_of(l.get('league_region')) for l in L}
anchors = [(pos[p], r) for p, r in loc_region.items() if r and p in pos]
def nearest_region(p):
    if p not in pos: return None
    x, y = pos[p]
    best = min(anchors, key=lambda a: (a[0][0] - x) ** 2 + (a[0][1] - y) ** 2)
    return best[1] if math.dist(best[0], (x, y)) < 200 else None
PLACES = sorted((p for p, r in loc_region.items() if r and len(p) > 4), key=len, reverse=True)
def place_region(page, text=''):
    """A page's region: its own, the nearest located place's, or the first known place named in `text`."""
    r = loc_region.get(page) or nearest_region(page)
    if r: return r
    for p in PLACES:
        if p in (text or ''): return loc_region[p]
    return None
def mentions(text, place):
    return re.search(r'\b' + re.escape(place) + r'\b', text or '') is not None
def link(text):
    m = re.search(r'\[\[([^\]|#]+)', text or ''); return m.group(1).strip() if m else (text or '').strip()

STAGE = {'Novice': 'early', 'Intermediate': 'early', 'Experienced': 'mid', 'Master': 'late', 'Grandmaster': 'late'}
prof = {r: collections.Counter() for r in REGIONS}
unplaced = collections.Counter()
for q in Q:
    if q.get('quest_type') not in (None, '', 'quest') or q.get('subquest_of'): continue
    r = place_region(link(q.get('start_area')), q.get('start_area'))
    st = STAGE.get(q.get('official_difficulty'))
    if not st: continue
    if r: prof[r]['quests ' + st] += 1
    else: unplaced['quests'] += 1
monster_region = {m['page_name']: region_of(m.get('league_region')) for m in M}
for b in CAT['Bosses']:
    r = monster_region.get(b) or nearest_region(b)
    if r: prof[r]['bosses'] += 1
    else: unplaced['bosses'] += 1
for cat, key in (('Minigames', 'minigames'), ('Distractions and Diversions', 'D&Ds'), ('Dungeons', 'dungeons'), ('Banks', 'banks'), ('Guilds', 'guilds')):
    for p in CAT[cat]:
        r = nearest_region(p)
        if r: prof[r][key] += 1
        else: unplaced[key] += 1
for s in S:
    r = region_of(s.get('league_region'))
    if r: prof[r]['shops'] += 1
lodestones = [p for p in pos if p.endswith(' lodestone') or p.endswith(' Lodestone')]
for p in lodestones:
    r = nearest_region(p)
    if r: prof[r]['lodestones'] += 1
for p, r in loc_region.items():
    if r: prof[r]['named places'] += 1

# Spacing between sites of a kind (nearest neighbour, tiles, Chebyshev as the game counts).
def spacing(pages):
    pts = [pos[p] for p in pages if p in pos and 1800 < pos[p][0] < 4200 and 2400 < pos[p][1] < 4200]
    nn = sorted(min(max(abs(a[0] - b[0]), abs(a[1] - b[1])) for j, b in enumerate(pts) if j != i) for i, a in enumerate(pts)) if len(pts) > 1 else []
    return dict(on_main_map=len(pts), median=nn[len(nn) // 2] if nn else None, p90=nn[int(len(nn) * 0.9)] if nn else None)
spacing_out = {k: spacing(v) for k, v in (('banks', CAT['Banks']), ('lodestones', lodestones), ('dungeons', CAT['Dungeons']),
                                            ('bosses', CAT['Bosses']), ('minigames + D&Ds', CAT['Minigames'] + CAT['Distractions and Diversions']))}

# Cities: what each has within reach (64 tiles for services, 128 for things to do nearby).
def within(page, pages, r):
    if page not in pos: return None
    x, y = pos[page]
    return sum(1 for p in pages if p in pos and max(abs(pos[p][0] - x), abs(pos[p][1] - y)) <= r)
city_rows = []
for c in CAT['Cities']:
    if c not in pos: continue
    quests = sum(1 for q in Q if mentions(q.get('start_area'), c) and q.get('quest_type') in (None, '', 'quest') and not q.get('subquest_of'))
    shops = sum(1 for s in S if mentions(s.get('location'), c))
    city_rows.append(dict(city=c, region=place_region(c), shops=shops, quests_started=quests,
                          banks_64=within(c, CAT['Banks'], 64), lodestone_64=within(c, lodestones, 64),
                          dungeons_128=within(c, CAT['Dungeons'], 128), bosses_128=within(c, CAT['Bosses'], 128),
                          activities_128=within(c, CAT['Minigames'] + CAT['Distractions and Diversions'], 128)))
city_rows.sort(key=lambda r: -(r['shops'] + r['quests_started']))

out = dict(regions={r: dict(prof[r]) for r in REGIONS}, unplaced=dict(unplaced), spacing=spacing_out,
           cities=city_rows, lodestone_count=len(lodestones),
           quest_difficulty=dict(collections.Counter(q.get('official_difficulty') for q in Q if q.get('quest_type') in (None, '', 'quest') and not q.get('subquest_of'))))
json.dump(out, open(sys.argv[2], 'w'), indent=1)
print(json.dumps({k: out[k] for k in ('regions', 'unplaced', 'spacing', 'lodestone_count', 'quest_difficulty')}, indent=1))
for r in city_rows[:30]: print(r)
