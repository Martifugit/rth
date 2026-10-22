"""
The area plan's tool: checks docs/areas/skeleton.json against the rulebook (docs/areas/rules.md,
targets.json), renders it over the world sketch and writes docs/areas/skeleton.md.

    python3 tools/areas/plan.py            # all three
    python3 tools/areas/plan.py check      # only print the checks

Needs Python 3.10+ and Pillow (pip install pillow) for the render.
"""
import json, math, os, re, sys, collections

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
AREAS = os.path.join(ROOT, 'docs', 'areas')
plan = json.load(open(os.path.join(AREAS, 'skeleton.json')))
targets = {t['id']: t for t in json.load(open(os.path.join(AREAS, 'targets.json')))['targets']}
scale = plan['scale']
S = scale['tilesPerSketchPixel']
REGION = scale['regionTiles'] ** 2
areas = plan['areas']
by_id = {a['id']: a for a in areas}

# --------------------------------------------------------------------------------------- checks
results = []
def result(id, status, text): results.append((id, status, text))

# I-REACH: every area reachable from Tolling through links (links count both ways).
graph = collections.defaultdict(set)
one_way = []
for a in areas:
    for l in a['links']:
        if l['to'] not in by_id:
            result('I-REACH', 'fail', f"{a['id']} links to unknown area {l['to']}")
            continue
        graph[a['id']].add(l['to']); graph[l['to']].add(a['id'])
        if not any(b['to'] == a['id'] for b in by_id[l['to']]['links']): one_way.append(f"{a['id']} -> {l['to']}")
seen = {'tolling'}; todo = ['tolling']
while todo:
    for n in graph[todo.pop()]:
        if n not in seen: seen.add(n); todo.append(n)
unreached = [a['id'] for a in areas if a['id'] not in seen]
result('I-REACH', 'ok' if not unreached else 'fail', 'every area reachable from Tolling' if not unreached else 'unreachable: ' + ', '.join(unreached))
if one_way: result('links', 'note', 'links listed on one side only: ' + ', '.join(one_way))

# I-NAMES: every bold name in docs/world-map.md's tables placed exactly once (areas or parts).
names_doc = open(os.path.join(ROOT, 'docs', 'world-map.md')).read()
doc_names = []
for line in names_doc.splitlines():
    if line.startswith('|'):
        for n in re.findall(r'\*\*([^*]+)\*\*', line.split('|')[1]): doc_names.append(n.strip())
NOT_PLACES = {'Venaria', 'Calcinia', 'Sere', 'Concordia', 'Slough', 'Candora', 'Nivil', 'Gall', 'Umber', 'Mordant', 'Sallow',
              'The Sanguine Synod', 'The Verdigris Moon', 'The Quaere'}
labels = [(a['id'], a['name']) for a in areas] + [(p['id'], p['name']) for a in areas for p in a['parts']]
missing, doubled = [], []
for n in dict.fromkeys(doc_names):
    if n in NOT_PLACES: continue
    hits = {i for i, label in labels if re.search(r'\b' + re.escape(n) + r'\b', label)}
    # an area and its own part may both carry the name (Tolling the area, Tolling the town)
    owners = {a['id'] for a in areas if a['id'] in hits or any(p['id'] in hits for p in a['parts'])}
    if not hits: missing.append(n)
    elif len(owners) > 1: doubled.append(f"{n} ({', '.join(sorted(owners))})")
status = 'ok' if not missing and not doubled else 'fail'
result('I-NAMES', status, 'every name in world-map.md placed once' if status == 'ok' else
       '; '.join(x for x in ['unplaced: ' + ', '.join(missing) if missing else '', 'in several areas: ' + ', '.join(doubled) if doubled else ''] if x))

# Sizes: land in the plan against land on the sketch at this scale.
surface = sum(a['sizeRegions'] for a in areas if a['layer'] == 'surface' and a['status'] != 'later')
surface_all = sum(a['sizeRegions'] for a in areas if a['layer'] == 'surface')
under = sum(a['sizeRegions'] for a in areas if a['layer'] == 'underground') + \
        sum(1 for a in areas for p in a['parts'] if p.get('layer') == 'underground') * 0.5
under_outside_candora = sum(a['sizeRegions'] for a in areas if a['layer'] == 'underground' and a['realm'] not in ('Candora', 'Undermost')) + \
        sum(0.5 for a in areas for p in a['parts'] if p.get('layer') == 'underground')
land_tiles = surface_all * REGION
try:
    from PIL import Image
    im = Image.open(os.path.join(ROOT, scale['sketch'])).convert('RGB')
    px = im.load(); w, h = im.size; land_px = 0
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if not (r < 30 and g < 30 and b < 30) and not (b > r + 30 and b > g + 15): land_px += 1
    sketch_land = land_px * S * S
    result('size', 'info', f"areas cover {surface_all} regions = {land_tiles/1e6:.2f}M tiles of surface; the sketch's land at {S} tiles/px is {sketch_land/1e6:.2f}M tiles ({100*land_tiles/sketch_land:.0f}%)")
except ImportError:
    result('size', 'info', f"areas cover {surface_all} regions = {land_tiles/1e6:.2f}M tiles (install Pillow to compare with the sketch)")

def in_range(id, value, fmt='{:.2f}'):
    t = targets[id]; lo, hi = t['range']
    return ('ok' if lo <= value <= hi else 'outside', f"{fmt.format(value)} (range {fmt.format(lo)}-{fmt.format(hi)})")

st, txt = in_range('T-UNDER', under_outside_candora / surface)
result('T-UNDER', st, f"underground outside Candora / surface: {txt}; with Candora and Undermost: {under/surface:.2f}")

tiers = collections.Counter(p.get('settlement') for a in areas for p in a['parts'] if p.get('settlement'))
per100k = 100000 / (surface * REGION)
lad = targets['T-LADDER']['range']
towns, villages = tiers['town'] * per100k, tiers['village'] * per100k
ok = lad['towns'][0] <= towns <= lad['towns'][1] and lad['villages'][0] <= villages <= lad['villages'][1]
result('T-LADDER', 'ok' if ok else 'outside', f"{tiers['city']} cities, {tiers['town']} towns, {tiers['village']} villages, {tiers['hamlet']} hamlets: "
       f"{towns:.2f} towns and {villages:.2f} villages per 100k land tiles (ranges {lad['towns']}, {lad['villages']})")

BUILT = {'city': 12000, 'town': 3000, 'village': 1200, 'hamlet': 300}  # built tiles per tier (research: section 2)
built = sum(BUILT[p['settlement']] for a in areas for p in a['parts'] if p.get('settlement')) + BUILT['city']  # Cruor counts double
st, txt = in_range('T-LAND-BUILT', built / (surface * REGION), '{:.0%}')
result('T-LAND-BUILT', st, f"estimated from settlement tiers: {txt}")

cities = sorted(((a['sizeRegions'], a['id']) for a in areas if any(p.get('settlement') == 'city' for p in a['parts'])), reverse=True)
ratio = cities[0][0] / cities[1][0]
st, txt = in_range('T-CITY-DOMINANCE', ratio, '{:.1f}')
result('T-CITY-DOMINANCE', st, f"pass 1 measures size only: {cities[0][1]} {cities[0][0]} regions against {cities[1][1]} {cities[1][0]}: {txt}")
result('T-LAND-SPARSE', 'later', 'needs content (pass 2 onwards)')

def print_checks():
    for id, status, text in results: print(f"{status:8s} {id:17s} {text}")

# --------------------------------------------------------------------------------------- render
REALM_COLOURS = {'Venaria': (92, 150, 70), 'Sere': (196, 150, 80), 'Calcinia': (225, 215, 170), 'Slough': (120, 90, 120),
                 'Candora': (150, 160, 175), 'Undermost': (200, 110, 60), 'Nivil': (210, 230, 245), 'The Cradle': (80, 140, 210), 'The Dreaming': (190, 160, 230)}
LINK_STYLE = {'road': ((240, 225, 190), 3), 'path': ((220, 220, 220), 1), 'ferry': ((90, 170, 255), 2), 'causeway': ((210, 190, 140), 3),
              'pass': ((200, 170, 255), 2), 'cart-line': ((255, 150, 60), 2), 'shaft': ((255, 110, 40), 2), 'dream': ((210, 170, 255), 2)}

def render(out):
    from PIL import Image, ImageDraw, ImageFont
    k = 1.6
    base = Image.open(os.path.join(ROOT, scale['sketch'])).convert('RGB')
    base = base.resize((int(base.width * k), int(base.height * k)))
    img = Image.blend(base, Image.new('RGB', base.size, (20, 20, 24)), 0.45)
    d = ImageDraw.Draw(img)
    try: font = ImageFont.truetype('DejaVuSans.ttf', 13)
    except OSError: font = ImageFont.load_default()
    P = lambda a: (a['sketch'][0] * k, a['sketch'][1] * k)
    for a in areas:
        if not a['sketch']: continue
        for l in a['links']:
            b = by_id.get(l['to'])
            if b and b['sketch'] and a['id'] < b['id']:
                c, wdt = LINK_STYLE.get(l['by'], ((255, 255, 255), 1)); d.line([P(a), P(b)], fill=c, width=wdt)
    for a in areas:
        if not a['sketch']: continue
        x, y = P(a)
        r = max(6, math.sqrt(max(a['sizeRegions'], 0.5) * REGION / math.pi) / S * k)
        c = REALM_COLOURS.get(a['realm'], (255, 255, 255))
        if a['layer'] == 'underground':
            d.ellipse([x - r, y - r, x + r, y + r], outline=(255, 140, 50), width=3)
        elif a['layer'] == 'water':
            d.ellipse([x - 8, y - 8, x + 8, y + 8], outline=c, width=3)
        else:
            d.ellipse([x - r, y - r, x + r, y + r], outline=c, width=3 if a['status'] != 'later' else 1)
        label = a['name'] + (' (built)' if a['status'] == 'built' else ' (part built)' if a['status'] == 'part built' else '')
        d.text((x - d.textlength(label, font=font) / 2, y - 7), label, fill=(255, 255, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
    # Legend and the world's tile scale.
    y0 = img.height - 150
    d.rectangle([10, y0 - 10, 300, img.height - 10], fill=(15, 15, 18))
    for i, (kind, (c, wdt)) in enumerate(LINK_STYLE.items()):
        d.line([20, y0 + i * 16 + 7, 60, y0 + i * 16 + 7], fill=c, width=max(2, wdt)); d.text((70, y0 + i * 16), kind, fill=(230, 230, 230), font=font)
    d.ellipse([170, y0, 186, y0 + 16], outline=(255, 140, 50), width=3); d.text((192, y0), 'underground', fill=(230, 230, 230), font=font)
    bar = 500 / S * k
    d.line([170, y0 + 40, 170 + bar, y0 + 40], fill=(255, 255, 255), width=3); d.text((170, y0 + 46), '500 tiles', fill=(230, 230, 230), font=font)
    img.save(out)

# --------------------------------------------------------------------------------------- tables
def tables(out):
    lines = ['# The area plan: pass 1, the skeleton', '',
             'Generated from `skeleton.json` by `tools/areas/plan.py`. Edit the JSON, not this file.', '',
             f"**Scale:** {S} tiles per sketch pixel; a region is {scale['regionTiles']} × {scale['regionTiles']} tiles. "
             'Sizes are in regions and rough. Positions are on the sketch; Tolling stays at tile (0, 0).', '',
             '![The skeleton over the sketch](skeleton.png)', '', '## Checks', '', '| Check | Status | Result |', '| --- | --- | --- |']
    lines += [f"| {id} | {status} | {text} |" for id, status, text in results]
    for realm in dict.fromkeys(a['realm'] for a in areas):
        lines += ['', f"## {realm}", '', '| Area | Parts | Size | Links | Notes |', '| --- | --- | --- | --- | --- |']
        for a in (a for a in areas if a['realm'] == realm):
            parts = '; '.join(p['name'] + (f" ({p['settlement']})" if p.get('settlement') else '') + (' [underground]' if p.get('layer') == 'underground' else '')
                              + (f" [{p['when']}]" if p.get('when') else '') for p in a['parts'])
            links = ', '.join(f"{by_id[l['to']]['name'] if l['to'] in by_id else l['to']} ({l['by']})" for l in a['links'])
            name = a['name'] + ('' if a['layer'] == 'surface' else f" [{a['layer']}]") + ('' if a['status'] == 'planned' else f" ({a['status']})")
            lines.append(f"| **{name}** | {parts} | {a['sizeRegions'] or '-'} | {links} | {a.get('notes', '')} |")
    open(out, 'w').write('\n'.join(lines) + '\n')

if __name__ == '__main__':
    what = sys.argv[1:] or ['check', 'render', 'tables']
    if 'check' in what: print_checks()
    if 'render' in what: render(os.path.join(AREAS, 'skeleton.png'))
    if 'tables' in what: tables(os.path.join(AREAS, 'skeleton.md'))
