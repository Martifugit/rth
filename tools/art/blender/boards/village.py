"""The village board: Venaria's village kit put together into a cottage, a longhouse, a village
chapel and a townhouse, face by face from a short description (volume()), the way Unity's
BuildingBuilder does from a region's building pieces; the kit's pieces in a row in front.

The realm's look, in pieces: pale walls, timber frames, stone where there is money or faith;
red only where the faith or the regime touches (church glass, a font's blood, a banner); leaded
glass that repeats, a little on a good house and a lot on a chapel; a narrow pointed window (the
lancet, the wound's shape) in gables and churches; the tithe font by a sworn door.

The pieces are the families parts/walls/village, parts/roofs/village, parts/fixtures/village and
assets/openings/village (the door). With EXPORT set it writes the kit's rules,
world/kits/venaria.json (export the pieces from their families).

    import runpy; runpy.run_path(r"C:\\dev\\rth\\tools\\art\\blender\\boards\\village.py")
"""

import bpy
import json
import math
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)
if __name__ in ("__main__", "<run_path>"):          # run as a script: pick up edits to the kit's modules
    for _m in [m for m in sys.modules if m == "rth_art" or m.split(".")[0] in ("artlib", "assets", "parts", "boards")]:
        del sys.modules[_m]

import rth_art as A  # noqa: E402
from artlib import family  # noqa: E402
from artlib.village import RISE, ROOF_BASE, STOREY  # noqa: E402
import assets.containers.village as containers  # noqa: E402
import assets.dressing.village as dressing  # noqa: E402
import assets.furniture.village as furniture  # noqa: E402
import assets.openings.village as openings  # noqa: E402
import parts.fixtures.village as fixtures  # noqa: E402
import parts.roofs.village as roofs  # noqa: E402
import parts.walls.village as walls  # noqa: E402
from parts.roofs.village import SLAB, VERGE  # noqa: E402
from parts.walls.village import JETTY, MAX_SPAN  # noqa: E402

KIT = [walls, roofs, fixtures, openings]     # the families whose members are the kit's pieces


def build_pieces():
    """The kit's pieces, and the dressing the board's buildings stand among, by the names volume()
    and the buildings use."""
    kit = {}
    for fam in KIT:
        for name, builder in fam.ASSETS.items():
            kit[name] = family.build(name, builder)
    for name, fam, member in (("Font", furniture, "Font"), ("Hay", dressing, "HayBale"),
                              ("Barrel", containers, "Barrel"), ("Crate", containers, "Crate")):
        kit[name] = family.build(name, fam.ASSETS[member])
    return kit


def place(kit, name, coll, x, y, rot=0.0, z=0.0, recolor=None, scale_x=1.0):
    src = kit[name]
    o = bpy.data.objects.new(f"{coll.name}_{name}", src.data)
    o.location = (x, y, z)
    o.rotation_euler = (0, 0, math.radians(rot))
    o.scale = (scale_x, 1, 1)
    coll.objects.link(o)
    if recolor:
        for i, m in enumerate(src.data.materials):
            if m.name in recolor:
                o.material_slots[i].link = "OBJECT"
                o.material_slots[i].material = A.mat(recolor[m.name])
    return o


OPENINGS = {"WallDoorway": "Door"}           # pieces that bring their leaf with them
PLAIN = {"Wall", "WallBraced"}
WINDOWS = {"WallShuttered", "WallLeaded"}

# What may hang on which wall tiles, so dressing never covers a door or a window.
ATTACHMENTS = {"Banner": PLAIN, "Woodpile": PLAIN, "Bench": PLAIN, "Lantern": {"WallDoorway"}}


def place_tile(kit, coll, entry, x, y, rot, z, rc):
    """One wall tile: "Wall", or "Wall+Banner" (attachments only where they're allowed, so they
    never cover a door or a window)."""
    name, *extra = entry.split("+")
    place(kit, name, coll, x, y, rot, z, rc)
    if name in OPENINGS:
        place(kit, OPENINGS[name], coll, x, y, rot, z, rc)
    for a in extra:
        assert a in ATTACHMENTS and name in ATTACHMENTS[a], f"{a} can't go on {name}"
        place(kit, a, coll, x, y, rot, z, rc)


def volume(kit, coll, ox, oy, length, depth, faces, storeys=1, walls=None, roof=None, jetty=False,
           slit=None, bellcote=None, chimney=None):
    """One block of a building on tiles [ox, ox+length) x [oy, oy+depth), gabled with its ridge
    along X (depth even). faces: {(face, storey): [entry per tile]} for faces "S" (front, -Y),
    "N", "W", "E", tiles listed west to east / south to north; an entry is a piece name with
    optional attachments ("Wall+Banner"); missing tiles are plain "Wall". walls: a recolour per
    storey. roof: the roof's recolour. jetty: upper storeys jut out over the gable ends.
    slit: the end ("W"/"E") whose top gable has the lancet slit. bellcote: the end carrying a
    bell. chimney: (end, tile), a stack in that end wall where the hearth is, or None. Roofs are
    plain slabs: their texture paints the thatch or slate."""
    eave, ridge, v_eave, v_ridge = "RoofEave", "RoofRidge", "VergeEave", "VergeRidge"
    walls = walls or [None] * storeys
    half = depth // 2

    def storey_recolour(s):
        rc = walls[s] if s < len(walls) else None
        if s > 0:
            rc = dict({"Stone": "Timber"}, **(rc or {}))          # upper storeys sit on a timber sill
        return rc

    for s in range(storeys):
        z = s * STOREY
        jet = JETTY if (jetty and s > 0) else 0.0
        rc = storey_recolour(s)

        def tiles(face, n):
            given = faces.get((face, s), [])
            return [given[i] if i < len(given) and given[i] else "Wall" for i in range(n)]

        for i, entry in enumerate(tiles("S", length)):
            place_tile(kit, coll, entry, ox + i, oy, 0, z, rc)
        for i, entry in enumerate(tiles("N", length)):
            place_tile(kit, coll, entry, ox + i + 1, oy + depth, 180, z, rc)
        for j, entry in enumerate(tiles("W", depth)):
            place_tile(kit, coll, entry, ox - jet, oy + j + 1, 270, z, rc)
            if jet:
                place(kit, "JettyBeams", coll, ox, oy + j + 1, 270, z, rc)
        for j, entry in enumerate(tiles("E", depth)):
            place_tile(kit, coll, entry, ox + length + jet, oy + j, 90, z, rc)
            if jet:
                place(kit, "JettyBeams", coll, ox + length, oy + j, 90, z, rc)
        if jet:                                                    # close the corners the jetty opens
            place(kit, "Wall", coll, ox - jet, oy, 0, z, rc, scale_x=jet)
            place(kit, "Wall", coll, ox + length, oy, 0, z, rc, scale_x=jet)
            place(kit, "Wall", coll, ox, oy + depth, 180, z, rc, scale_x=jet)
            place(kit, "Wall", coll, ox + length + jet, oy + depth, 180, z, rc, scale_x=jet)
        for cx in (ox - jet, ox + length + jet):
            for cy in (oy, oy + depth):
                place(kit, "Post", coll, cx, cy, 0, z, rc)
    top = (storeys - 1) * STOREY
    jet = JETTY if (jetty and storeys > 1) else 0.0
    rc = storey_recolour(storeys - 1)
    for k in range(half):
        for end, ex in (("W", ox - jet), ("E", ox + length + jet)):
            name = "Gable1Slit" if (slit == end and k == half - 1) else f"Gable{k}"
            place(kit, name, coll, ex, oy + k, 90, top, rc)
            place(kit, f"Gable{k}", coll, ex, oy + depth - k, 270, top, rc)
    for i in range(length):
        x = ox + i
        place(kit, eave, coll, x, oy, 0, top, roof)
        place(kit, eave, coll, x + 1, oy + depth, 180, top, roof)
        for k in range(1, half):
            place(kit, "RoofSlope", coll, x, oy + k, 0, top + RISE * k, roof)
            place(kit, "RoofSlope", coll, x + 1, oy + depth - k, 180, top + RISE * k, roof)
        place(kit, ridge, coll, x, oy + half, 0, top + RISE * half, roof)
    for vx in (ox - VERGE, ox + length):                          # the roof reaches past the gable ends
        place(kit, v_eave, coll, vx, oy, 0, top, roof)
        place(kit, v_eave, coll, vx + VERGE, oy + depth, 180, top, roof)
        for k in range(1, half):
            place(kit, "VergeSlope", coll, vx, oy + k, 0, top + RISE * k, roof)
            place(kit, "VergeSlope", coll, vx + VERGE, oy + depth - k, 180, top + RISE * k, roof)
        place(kit, v_ridge, coll, vx, oy + half, 0, top + RISE * half, roof)
    ridge_z = top + ROOF_BASE + RISE * half + SLAB
    if bellcote:
        bx = ox - jet if bellcote == "W" else ox + length + jet
        place(kit, "Bellcote", coll, bx, oy + half, 90, ridge_z - SLAB - 0.05, walls[-1] if walls else None)
    if chimney:
        end, j = chimney
        if end == "W":
            x, y, rot = ox - jet, oy + j + 1, 270
        else:
            x, y, rot = ox + length + jet, oy + j, 90
        place(kit, "ChimneyBreast", coll, x, y, rot, 0, walls[0] if walls else None)
        h = STOREY - 0.1
        while h < ridge_z + 0.35:                                  # stack up past the ridge
            place(kit, "ChimneyStack", coll, x, y, rot, h, walls[0] if walls else None)
            h += 1.0
        place(kit, "ChimneyCap", coll, x, y, rot, h, walls[0] if walls else None)


FIELDSTONE = {"Plaster": "Stone", "Timber": "Stone", "Stone": "Granite"}
CUT_STONE = {"Plaster": "Cobble", "Timber": "Cobble", "Stone": "Granite"}


def cottage(kit, ox, oy):
    """A poor cottage: timber frame and thatch, shutters, a font and a lantern at the door,
    firewood by the hearth end, the slit in a gable."""
    coll = A.collection("Cottage")
    paint = None
    volume(kit, coll, ox, oy, 4, 4, {
        ("S", 0): ["WallShuttered", "WallDoorway+Lantern", "WallBraced", "WallShuttered"],
        ("E", 0): ["Wall", "WallShuttered", None, None],
        ("W", 0): ["Wall+Woodpile", None, None, "Wall+Woodpile"],
        ("N", 0): [None, "WallShuttered", None, None]},
        walls=[paint], slit="E", chimney=("W", 1))
    place(kit, "Font", coll, ox + 1.15, oy - 0.45)
    place(kit, "Barrel", coll, ox + 4.45, oy - 0.35)


def longhouse(kit, ox, oy):
    """A farm's longhouse: people at the east end by the hearth, beasts in the byre at the west
    end behind the wide doors, all under one long thatch. Hay and crates by the byre, a bench
    by the door."""
    coll = A.collection("Longhouse")
    paint = {"Stone": "Granite"}
    volume(kit, coll, ox, oy, 7, 4, {
        ("S", 0): ["WallBarnDoor", "WallBraced", "Wall+Bench", "WallShuttered", "WallDoorway+Lantern",
                   "WallBraced", "WallShuttered"],
        ("N", 0): [None, "WallBraced", None, "WallShuttered", None, "WallShuttered", None],
        ("W", 0): [None, "WallBraced", "WallBraced", None]},
        walls=[paint], chimney=("E", 1), slit="W")
    place(kit, "Font", coll, ox + 4.15, oy - 0.45)
    place(kit, "Hay", coll, ox - 0.9, oy + 0.4, 15)
    place(kit, "Hay", coll, ox - 0.95, oy + 1.4, -10)
    place(kit, "Crate", coll, ox + 1.4, oy - 0.5, 10)
    place(kit, "Barrel", coll, ox + 1.95, oy - 0.45)


def village_chapel(kit, ox, oy):
    """A village chapel: fieldstone and thatch, a door in the west end under the bell, two red
    lancets each side, one banner by the door."""
    coll = A.collection("VillageChapel")
    volume(kit, coll, ox, oy, 5, 4, {
        ("S", 0): ["Wall+Banner", "WallLancet", "Wall", "WallLancet", "Wall"],
        ("N", 0): ["Wall", "WallLancet", "Wall", "WallLancet", "Wall"],
        ("W", 0): ["Wall", "WallDoorway+Lantern", "Wall", "Wall"],
        ("E", 0): ["Wall", "WallLancet", "WallLancet", "Wall"]},
        walls=[dict(FIELDSTONE, Glaze="SynodRed")], bellcote="W")
    place(kit, "Font", coll, ox - 1.0, oy + 1.5)


def townhouse(kit, ox, oy):
    """A town's house (not a village's): cut stone below, a jettied timber storey above,
    leaded windows, slate."""
    coll = A.collection("Townhouse")
    volume(kit, coll, ox, oy, 5, 4, {
        ("S", 0): ["WallLeaded", None, "WallDoorway", None, "WallLeaded"],
        ("S", 1): ["WallLeaded", "WallBraced", "WallLeaded", "WallBraced", "WallLeaded"],
        ("N", 1): [None, "WallLeaded", None, "WallLeaded", None],
        ("W", 1): [None, "WallLeaded", "WallLeaded", None], ("E", 1): [None, "WallBraced", "WallBraced", None],
        ("W", 0): [None, "WallLeaded", None, None]},
        storeys=2, walls=[CUT_STONE, None], roof={"Thatch": "Slate", "Timber": "DarkRock"}, jetty=True,
        slit="E", chimney=("E", 1))


def write_kit():
    """The kit's rules for the world data (world/kits/venaria.json): read by the region compiler
    (doorways, what blocks outside a wall) and by Unity, which puts buildings together from these
    pieces as volume() does here (shared/World/Data/Buildings.cs, BuildingBuilder.cs)."""
    kit = {
        "id": "venaria",
        "storey": STOREY, "rise": round(RISE, 5), "roofBase": round(ROOF_BASE, 5), "verge": VERGE,
        "jettyOut": JETTY, "slab": SLAB, "wall": "Wall", "maxSpan": MAX_SPAN,
        "pieces": sorted(n for fam in KIT for n in fam.ASSETS),
        "paths": {n: "Assets/%s/%s.fbx" % (family.folder(fam.__file__), n) for fam in KIT for n in sorted(fam.ASSETS)},
        "openings": {name: OPENINGS.get(name, "") for name in ("WallDoorway", "WallBarnDoor")},
        "attachments": {a: sorted(on) for a, on in ATTACHMENTS.items()},
        "blocking": ["Woodpile", "Bench"],
        "upperColors": {"Stone": "Timber"},
    }
    path = os.path.join(A.REPO, "world", "kits", "venaria.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(kit, f, indent=2)
        f.write("\n")


def build():
    A.reset()
    kit = build_pieces()
    result = {name: A.report([o])["triangles"] for name, o in kit.items()}
    for i, o in enumerate(kit.values()):
        o.location = (-4.0 + i * 1.3, -9.0, 0)
    cottage(kit, 0, 0)
    longhouse(kit, 7, 0)
    village_chapel(kit, 18, 0)
    townhouse(kit, 8, 9)
    return kit, result


if __name__ in ("__main__", "<run_path>"):                       # run as a script: the reference village
    if globals().get("EXPORT"):
        write_kit()
    kit, result = build()
    cam = A.preview_rig(focus=(12.0, 3.0, 2.0), distance=28, pitch=35, yaw=-20, ground=18, human_at=(5.5, -1.5))
