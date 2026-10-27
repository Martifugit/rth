using System.Collections.Generic;
using System.Linq;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Draws a <see cref="PieceKind.Building"/> piece from its kit's models, piece by piece as the
    /// kit's Blender script puts a building together (tools/art/blender/buildings/venaria.py,
    /// volume()): wall tiles along every face and storey, posts at the corners, gables, roof slabs
    /// and verges, a bellcote and a chimney, recoloured per storey. It stands level on the highest
    /// corner of the terrain under it, on a stone foundation, with a floor inside. The walls and
    /// ground floor fade when they hide the player; the upper storeys, gables and roof go away
    /// while the player is inside (<see cref="RoofHider"/>). Its collision is what
    /// <see cref="RegionCompiler"/> gives it: walls on its edges open at the doorways.
    /// </summary>
    public static class BuildingBuilder
    {
        // The kit's Blender axes arrive in Unity as (-x, z, -y): a half turn about the vertical.
        private const float ImportTurn = 180f;

        public static void Build(Transform group, RegionData region, Piece piece, Heightfield terrain)
        {
            BuildingKit kit = Buildings.Check(piece);
            BuildingSpec spec = piece.Building;
            int[] r = piece.Rect;
            int storeys = spec.Storeys ?? 1;

            // Level on the highest corner under it, so no ground pokes through the floor.
            float floorZ = 0f, lowest = 0f;
            if (terrain != null)
            {
                floorZ = float.MinValue;
                lowest = float.MaxValue;
                for (int y = r[1]; y <= r[3]; y++)
                    for (int x = r[0]; x <= r[2]; x++)
                    {
                        floorZ = Mathf.Max(floorZ, terrain.Corner(x, y));
                        lowest = Mathf.Min(lowest, terrain.Corner(x, y));
                    }
            }

            var b = new Assembly(kit, spec, r, floorZ, group);
            b.Walls();
            b.Roof(storeys);

            // The floor (the ground collider players stand on inside) and the foundation under it.
            Material stone = Mat("Stone", Colours(spec, 0));
            GameObject floor = WorldKit.Box("Floor", b.Ground, new Vector3((r[0] + r[2]) / 2f, floorZ + 0.02f, (r[1] + r[3]) / 2f),
                new Vector3(r[2] - r[0], 0.04f, r[3] - r[1]), NatureImporter.For(spec.Floor ?? "Planks"));
            floor.isStatic = true;
            if (floorZ - lowest > 0.01f)
                WorldKit.Box("Foundation", b.Ground, new Vector3((r[0] + r[2]) / 2f, (floorZ + lowest) / 2f - 0.1f, (r[1] + r[3]) / 2f),
                    new Vector3(r[2] - r[0] + 0.26f, floorZ - lowest + 0.2f, r[3] - r[1] + 0.26f), stone, collider: false);

            // A step up to each doorway the ground falls away from.
            foreach ((string side, int at) in Buildings.Doors(spec, kit, r))
            {
                (int ox, int oy) = Buildings.Outside(r, side, at);
                float outside = terrain != null ? terrain.Sample(ox + 0.5f, oy + 0.5f) : 0f;
                if (floorZ - outside < 0.12f) continue;
                Vector2 c = new Vector2(ox + 0.5f, oy + 0.5f), toWall = new Vector2(r[0] + r[2], r[1] + r[3]) / 2f - c;
                Vector2 n = Mathf.Abs(toWall.x) > Mathf.Abs(toWall.y) ? new Vector2(Mathf.Sign(toWall.x), 0f) : new Vector2(0f, Mathf.Sign(toWall.y));
                Vector2 at2 = c + n * 0.32f;
                bool alongX = n.y != 0f;
                WorldKit.Box("Step", b.Ground, new Vector3(at2.x, (floorZ + outside) / 2f - 0.05f, at2.y),
                    alongX ? new Vector3(0.9f, floorZ - outside + 0.1f, 0.36f) : new Vector3(0.36f, floorZ - outside + 0.1f, 0.9f), stone, collider: false);
            }

            // Collision as the compiler sees it: invisible walls on the edges, open at the doorways.
            var doors = Buildings.Doors(spec, kit, r).ToList();
            int[] Gaps(string side) => doors.Where(d => d.side == side).Select(d => (side == "S" || side == "N" ? r[0] : r[1]) + d.at).ToArray();
            Transform collision = WorldKit.Group(group, "Collision");
            WorldKit.Wall(collision, "Wall S", r[0], r[1], r[2], r[1], null, kit.Storey, 0.2f, Gaps("S"));
            WorldKit.Wall(collision, "Wall N", r[0], r[3], r[2], r[3], null, kit.Storey, 0.2f, Gaps("N"));
            WorldKit.Wall(collision, "Wall W", r[0], r[1], r[0], r[3], null, kit.Storey, 0.2f, Gaps("W"));
            WorldKit.Wall(collision, "Wall E", r[2], r[1], r[2], r[3], null, kit.Storey, 0.2f, Gaps("E"));
            foreach (Transform w in collision)
            {
                w.position += Vector3.up * floorZ;
                Object.DestroyImmediate(w.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(w.GetComponent<MeshFilter>());
            }
            // What stands outside the walls blocks its tile (its own group: a blocker's colliders all block).
            foreach ((int x, int y) in Buildings.BlockedOutside(spec, kit, r))
                PieceBuilder.AddFootprint(WorldKit.Group(group, $"Blocks {x},{y}"), x, y, x + 1, y + 1, 1.2f, floorZ);
        }

        /// <summary>A storey's material swaps: the kit's for upper storeys, under the building's own.</summary>
        private static Dictionary<string, string> Colours(BuildingSpec spec, int storey)
        {
            var map = new Dictionary<string, string>();
            if (storey > 0)
                foreach (KeyValuePair<string, string> kv in (BuildingKits.Get(spec.Kit)?.UpperColors ?? new Dictionary<string, string>())) map[kv.Key] = kv.Value;
            SortedDictionary<string, string> own = spec.Colors != null && storey < spec.Colors.Count ? spec.Colors[storey] : null;
            if (own != null) foreach (KeyValuePair<string, string> kv in own) map[kv.Key] = kv.Value;
            return map;
        }

        private static Material Mat(string name, Dictionary<string, string> colours) =>
            NatureImporter.For(colours != null && colours.TryGetValue(name, out string swap) ? swap : name);

        /// <summary>
        /// One building's pieces, placed in volume()'s frame: tiles [0, L) along the ridge (local x) by
        /// [0, D) across it (local y), front (S) at y = 0. A ridge along world Y is that frame turned a
        /// quarter: local +x is north, local +y west.
        /// </summary>
        private sealed class Assembly
        {
            private readonly BuildingKit _kit;
            private readonly BuildingSpec _spec;
            private readonly int[] _r;
            private readonly float _base;
            private readonly bool _alongX;
            private readonly int _l, _d;
            private readonly Dictionary<string, GameObject> _models = new Dictionary<string, GameObject>();
            public readonly Transform Ground, Upper;

            public Assembly(BuildingKit kit, BuildingSpec spec, int[] rect, float floorZ, Transform group)
            {
                _kit = kit;
                _spec = spec;
                _r = rect;
                _base = floorZ;
                _alongX = Buildings.RidgeAlongX(spec, rect);
                _l = _alongX ? rect[2] - rect[0] : rect[3] - rect[1];
                _d = _alongX ? rect[3] - rect[1] : rect[2] - rect[0];
                Ground = WorldKit.Group(group, "Walls");
                WorldKit.Fades(Ground, 0.35f);
                // Upper storeys belong with the roof: they go away with it while the player is inside.
                Upper = WorldKit.Group(group, "Roof");
                WorldKit.Fades(Upper, 0.2f);
                Upper.gameObject.AddComponent<RoofHider>().interior = new RectInt(rect[0], rect[1], rect[2] - rect[0], rect[3] - rect[1]);
            }

            private int Storeys => _spec.Storeys ?? 1;
            private float Jet(int storey) => _spec.Jetty == true && storey > 0 ? _kit.JettyOut : 0f;

            /// <summary>A world side's face as volume() lists it: its local side and its tiles in local order.</summary>
            private List<string> LocalFace(string localSide, int storey)
            {
                if (_alongX) return Buildings.Face(_spec, _kit, _r, localSide, storey);
                // Turned a quarter: local S is the east face, N the west, W the south and E the north (both read backwards).
                switch (localSide)
                {
                    case "S": return Buildings.Face(_spec, _kit, _r, "E", storey);
                    case "N": return Buildings.Face(_spec, _kit, _r, "W", storey);
                    case "W": return Enumerable.Reverse(Buildings.Face(_spec, _kit, _r, "S", storey)).ToList();
                    default: return Enumerable.Reverse(Buildings.Face(_spec, _kit, _r, "N", storey)).ToList();
                }
            }

            private string LocalSide(string worldSide)
            {
                if (_alongX) return worldSide;
                return worldSide == "E" ? "S" : worldSide == "W" ? "N" : worldSide == "S" ? "W" : "E";
            }

            /// <summary>Puts a kit piece at (lx, ly) in the local frame, turned rot degrees anticlockwise as in Blender, z up from the floor.</summary>
            private void Place(Transform parent, string name, float lx, float ly, float rot, float z, Dictionary<string, string> colours, float scaleX = 1f)
            {
                if (!_models.TryGetValue(name, out GameObject model))
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(_kit.Models + "/" + name + ".fbx");
                    _models[name] = model;
                }
                if (model == null)
                {
                    Debug.LogError($"[R'th] building kit '{_kit.Id}': no model {_kit.Models}/{name}.fbx; export the kit from Blender.");
                    return;
                }
                float wx, wy, turn;
                if (_alongX) { wx = _r[0] + lx; wy = _r[1] + ly; turn = rot; }
                else { wx = _r[2] - ly; wy = _r[1] + lx; turn = rot + 90f; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                go.name = name;
                go.transform.position = new Vector3(wx, _base + z, wy);
                go.transform.rotation = Quaternion.Euler(0f, ImportTurn - turn, 0f);
                go.transform.localScale = new Vector3(scaleX, 1f, 1f);
                go.isStatic = true;
                if (colours != null && colours.Count > 0)
                    foreach (Renderer rend in go.GetComponentsInChildren<Renderer>())
                        rend.sharedMaterials = rend.sharedMaterials.Select(m => m != null && colours.TryGetValue(m.name, out string swap) ? NatureImporter.For(swap) : m).ToArray();
            }

            private void PlaceTile(Transform parent, string entry, float x, float y, float rot, float z, Dictionary<string, string> colours)
            {
                (string wall, string[] extras) = Buildings.Split(entry);
                Place(parent, wall, x, y, rot, z, colours);
                if (_kit.Openings.TryGetValue(wall, out string leaf) && !string.IsNullOrEmpty(leaf)) Place(parent, leaf, x, y, rot, z, colours);
                foreach (string a in extras) Place(parent, a, x, y, rot, z, colours);
            }

            public void Walls()
            {
                for (int s = 0; s < Storeys; s++)
                {
                    Transform parent = s == 0 ? Ground : Upper;
                    float z = s * _kit.Storey, jet = Jet(s);
                    Dictionary<string, string> rc = Colours(_spec, s);
                    List<string> south = LocalFace("S", s), north = LocalFace("N", s), west = LocalFace("W", s), east = LocalFace("E", s);
                    for (int i = 0; i < _l; i++)
                    {
                        PlaceTile(parent, south[i], i, 0, 0, z, rc);
                        PlaceTile(parent, north[i], i + 1, _d, 180, z, rc);
                    }
                    for (int j = 0; j < _d; j++)
                    {
                        PlaceTile(parent, west[j], -jet, j + 1, 270, z, rc);
                        PlaceTile(parent, east[j], _l + jet, j, 90, z, rc);
                        if (jet > 0f)
                        {
                            Place(parent, "JettyBeams", 0, j + 1, 270, z, rc);
                            Place(parent, "JettyBeams", _l, j, 90, z, rc);
                        }
                    }
                    if (jet > 0f)                                           // close the corners the jetty opens
                    {
                        Place(parent, _kit.Wall, -jet, 0, 0, z, rc, jet);
                        Place(parent, _kit.Wall, _l, 0, 0, z, rc, jet);
                        Place(parent, _kit.Wall, 0, _d, 180, z, rc, jet);
                        Place(parent, _kit.Wall, _l + jet, _d, 180, z, rc, jet);
                    }
                    foreach (float cx in new[] { -jet, _l + jet })
                        foreach (float cy in new float[] { 0, _d })
                            Place(parent, "Post", cx, cy, 0, z, rc);
                }
            }

            public void Roof(int storeys)
            {
                int half = _d / 2;
                float top = (storeys - 1) * _kit.Storey, jet = Jet(storeys - 1);
                Dictionary<string, string> rc = Colours(_spec, storeys - 1);
                var roof = _spec.RoofColors != null ? new Dictionary<string, string>(_spec.RoofColors) : null;
                string slit = _spec.Slit != null ? LocalSide(_spec.Slit) : null;
                for (int k = 0; k < half; k++)
                    foreach ((string end, float ex) in new[] { ("W", -jet), ("E", _l + jet) })
                    {
                        Place(Upper, slit == end && k == half - 1 ? $"Gable{k}Slit" : $"Gable{k}", ex, k, 90, top, rc);
                        Place(Upper, $"Gable{k}", ex, _d - k, 270, top, rc);
                    }
                for (int i = 0; i < _l; i++)
                {
                    Place(Upper, "RoofEave", i, 0, 0, top, roof);
                    Place(Upper, "RoofEave", i + 1, _d, 180, top, roof);
                    for (int k = 1; k < half; k++)
                    {
                        Place(Upper, "RoofSlope", i, k, 0, top + _kit.Rise * k, roof);
                        Place(Upper, "RoofSlope", i + 1, _d - k, 180, top + _kit.Rise * k, roof);
                    }
                    Place(Upper, "RoofRidge", i, half, 0, top + _kit.Rise * half, roof);
                }
                foreach (float vx in new[] { -_kit.Verge, _l })                 // the roof reaches past the gable ends
                {
                    Place(Upper, "VergeEave", vx, 0, 0, top, roof);
                    Place(Upper, "VergeEave", vx + _kit.Verge, _d, 180, top, roof);
                    for (int k = 1; k < half; k++)
                    {
                        Place(Upper, "VergeSlope", vx, k, 0, top + _kit.Rise * k, roof);
                        Place(Upper, "VergeSlope", vx + _kit.Verge, _d - k, 180, top + _kit.Rise * k, roof);
                    }
                    Place(Upper, "VergeRidge", vx, half, 0, top + _kit.Rise * half, roof);
                }
                float ridgeZ = top + _kit.RoofBase + _kit.Rise * half + _kit.Slab;
                Dictionary<string, string> outer = Colours(_spec, storeys - 1);
                if (_spec.Bellcote != null)
                {
                    float bx = LocalSide(_spec.Bellcote) == "W" ? -jet : _l + jet;
                    Place(Upper, "Bellcote", bx, half, 90, ridgeZ - _kit.Slab - 0.05f, outer);
                }
                (string side, int at)? chimney = Buildings.Chimney(_spec);
                if (chimney.HasValue)
                {
                    string end = LocalSide(chimney.Value.side);
                    int j = chimney.Value.at;
                    // Along the local end, in local order (the world's order reads backwards for a turned building's S and N).
                    if (!_alongX && (chimney.Value.side == "S" || chimney.Value.side == "N")) j = _d - 1 - j;
                    Dictionary<string, string> ground = Colours(_spec, 0);
                    float x, y, rot;
                    if (end == "W") { x = -jet; y = j + 1; rot = 270; }
                    else { x = _l + jet; y = j; rot = 90; }
                    Place(Ground, "ChimneyBreast", x, y, rot, 0, ground);
                    float h = _kit.Storey - 0.1f;
                    while (h < ridgeZ + 0.35f)                                  // stack up past the ridge
                    {
                        Place(Upper, "ChimneyStack", x, y, rot, h, ground);
                        h += 1f;
                    }
                    Place(Upper, "ChimneyCap", x, y, rot, h, ground);
                }
            }
        }
    }
}
