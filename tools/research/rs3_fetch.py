"""
Downloads structured data from the RuneScape wiki's bucket API (https://runescape.wiki/api.php,
action=bucket) for the area-plan research: locations, quests, shops, resource locations,
monsters and coordinates. Writes one JSON file per bucket. Facts only; nothing is republished.

    python3 tools/research/rs3_fetch.py <out dir> [bucket ...] [--categories]
"""
import json, os, sys, time, urllib.parse, urllib.request
OUT = sys.argv[1]
API = 'https://runescape.wiki/api.php'
UA = {'User-Agent': "rth-research/1.0 (area planning for a private game; contact via github.com/Martifugit)"}

def query(q):
    url = API + '?' + urllib.parse.urlencode({'action': 'bucket', 'format': 'json', 'query': q})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
                d = json.load(r)
            if 'error' in d: raise RuntimeError(d['error'])
            return d['bucket']
        except Exception as e:
            if attempt == 3: raise
            time.sleep(2 ** attempt)

def fetch(bucket, fields, where=None, page=2000):
    rows, offset = [], 0
    sel = ','.join(repr(f) for f in fields)
    while True:
        q = f"bucket({bucket!r}).select({sel})" + (f".where({where})" if where else '') + f".limit({page}).offset({offset}).run()"
        part = query(q)
        rows += part
        if len(part) < page: return rows
        offset += page
        time.sleep(0.5)

SETS = {
    'locations': ('infobox_location', ['page_name', 'name', 'location', 'league_region', 'teleport', 'banks', 'altars', 'shops', 'featuressection', 'is_members_only']),
    'quests': ('infobox_quest', ['page_name', 'name', 'official_difficulty', 'start_area', 'quest_type', 'official_age', 'official_series', 'release_date', 'is_members_only', 'subquest_of']),
    'quest_meta': ('quest', ['page_name', 'official_length', 'official_release_year', 'league_region', 'requirement_skill', 'requirement_skill_level']),
    'shops': ('infobox_shop', ['page_name', 'type', 'location', 'league_region', 'is_members_only']),
    'resources': ('resource_locations', ['page_name', 'location', 'league_region', 'resource_location_type', 'skill', 'resource', 'count', 'requirements']),
    'monsters': ('infobox_monster', ['page_name', 'name', 'combat_level', 'league_region', 'life_points', 'slayer_level', 'is_members_only']),
    'coordinates': ('coordinates', ['page_name', 'type', 'x', 'y', 'plane']),
}
os.makedirs(OUT, exist_ok=True)
for name in [a for a in sys.argv[2:] if not a.startswith('--')] or ([] if '--categories' in sys.argv else SETS):
    bucket, fields = SETS[name]
    rows = fetch(bucket, fields)
    json.dump(rows, open(os.path.join(OUT, name + '.json'), 'w'))
    print(f'{name}: {len(rows)} rows')


def category(title):
    """Page titles in a wiki category (main namespace), following continuation."""
    out, cont = [], {}
    while True:
        params = {'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + title, 'cmlimit': '500',
                  'cmnamespace': '0', 'format': 'json', **cont}
        with urllib.request.urlopen(urllib.request.Request(API + '?' + urllib.parse.urlencode(params), headers=UA), timeout=60) as r:
            d = json.load(r)
        out += [m['title'] for m in d['query']['categorymembers']]
        if 'continue' not in d: return out
        cont = d['continue']


CATEGORIES = ['Bosses', 'Minigames', 'Distractions and Diversions', 'Lodestones', 'Dungeons', 'Cities', 'Towns', 'Villages', 'Guilds', 'Banks']
if '--categories' in sys.argv:
    cats = {}
    for c in CATEGORIES:
        try:
            cats[c] = category(c)
        except Exception as e:
            cats[c] = {'error': str(e)}
        print(c, len(cats[c]))
    json.dump(cats, open(os.path.join(OUT, 'categories.json'), 'w'), indent=0)
