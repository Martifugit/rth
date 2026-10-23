using System.Collections.Generic;
using System.Linq;
using Rth.MapTools;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Draws a region's data (<see cref="RegionData"/>) under its scene's <see cref="DataRoot"/>,
    /// with the same footprints, walls and ground the C# compiler (<see cref="RegionCompiler"/>)
    /// gives the server: area kinds (patches, roads, water, walls, fences, houses) through
    /// <see cref="WorldKit"/>, tiles through <see cref="TileGround"/>; placed kinds (trees, rocks, props, creatures, the spawn) as one object
    /// on their tile whose look has no colliders, plus a collider over exactly their footprint.
    /// </summary>
    public static class PieceBuilder
    {
        /// <summary>Clears the data root and draws every piece of the region into it.</summary>
        public static void Build(RegionData region, MapArea area, DataRoot root)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            root.builtIds.Clear();
            var kit = new WorldKit(region.Seed);
            var levels = new Dictionary<int, Transform>();
            Heightfield terrain = TerrainText.Read(region); // null: flat

            Transform Level(int plane)
            {
                if (plane == region.Plane) return root.transform;
                if (levels.TryGetValue(plane, out Transform t)) return t;
                t = WorldKit.Group(root.transform, "Level " + plane);
                t.gameObject.AddComponent<MapLevel>().level = plane;
                return levels[plane] = t;
            }

            if (region.Ground != null)
                WorldKit.Box("Ground", root.transform,
                    new Vector3(region.OriginX + region.Width / 2f, -0.1f, region.OriginY + region.Height / 2f),
                    new Vector3(region.Width, 0.2f, region.Height), Material(kit, region, region.Ground));

            foreach (Piece piece in region.Pieces)
            {
                int plane = piece.Plane ?? region.Plane;
                GameObject go;
                try
                {
                    go = BuildPiece(kit, region, piece, Level(plane), Level, plane == region.Plane ? terrain : null);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[R'th] {region.Id}: piece '{piece.Id}' ({piece.Kind}) couldn't be drawn: {e.Message}");
                    continue;
                }
                var tag = go.AddComponent<DataPiece>();
                tag.pieceId = piece.Id;
                tag.placedPosition = go.transform.position;
                tag.placedYaw = go.transform.eulerAngles.y;
                tag.canTurn = PieceKind.IsPlaced(piece.Kind);
                root.builtIds.Add(piece.Id);
            }
        }

        private static GameObject BuildPiece(WorldKit kit, RegionData region, Piece piece, Transform parent, System.Func<int, Transform> level, Heightfield terrain)
        {
            string name = string.IsNullOrWhiteSpace(piece.Name) ? piece.Id : piece.Name;
            if (PieceKind.IsPlaced(piece.Kind))
            {
                GameObject placed = BuildPlaced(kit, region, piece, parent, level, name);
                // Placed things stand on the region's terrain (built at 0, then lifted with their footprint).
                if (terrain != null) placed.transform.position += Vector3.up * terrain.Sample(piece.X + 0.5f, piece.Y + 0.5f);
                return placed;
            }

            Transform group = WorldKit.Group(parent, name);
            switch (piece.Kind)
            {
                case PieceKind.Patch:
                {
                    int[] r = piece.Rect;
                    WorldKit.Patch(name, group, r[0], r[1], r[2], r[3], Material(kit, region, piece.Material));
                    break;
                }
                case PieceKind.Road:
                    WorldKit.Road(group, "Road", Tiles(TileShapes.Along(piece.Line, piece.Width.Value, region.OriginX, region.OriginY, region.Width, region.Height)), Material(kit, region, piece.Material));
                    break;
                case PieceKind.Water:
                {
                    IEnumerable<(int x, int y)> tiles = piece.Rect != null
                        ? RegionCompiler.RectTiles(piece.Rect)
                        : TileShapes.Along(piece.Line, piece.Width.Value, region.OriginX, region.OriginY, region.Width, region.Height);
                    WorldKit.Water(group, "Water", Tiles(tiles), Material(kit, region, piece.Material));
                    break;
                }
                case PieceKind.Wall:
                    WorldKit.Wall(group, "Wall", piece.From[0], piece.From[1], piece.To[0], piece.To[1], Material(kit, region, piece.Material),
                        piece.Height ?? WorldKit.DefaultStorey, 0.2f, (piece.Gaps ?? new List<int>()).ToArray());
                    break;
                case PieceKind.Fence:
                    WorldKit.Fence(group, "Fence", piece.From[0], piece.From[1], piece.To[0], piece.To[1], Material(kit, region, piece.Material),
                        (piece.Gaps ?? new List<int>()).ToArray());
                    break;
                case PieceKind.House:
                {
                    int[] r = piece.Rect;
                    var side = piece.Door == "N" ? WorldKit.Side.North : piece.Door == "E" ? WorldKit.Side.East : piece.Door == "W" ? WorldKit.Side.West : WorldKit.Side.South;
                    kit.House(group, "House", r[0], r[1], r[2], r[3], side, piece.DoorAt.Value,
                        Material(kit, region, piece.Walls), Material(kit, region, piece.Floor), Material(kit, region, piece.Roof),
                        piece.Height ?? WorldKit.DefaultStorey, piece.Storeys ?? 1);
                    break;
                }
                case PieceKind.Blocker:
                {
                    int[] r = piece.Rect;
                    AddFootprint(group, r[0], r[1], r[2], r[3], 1.5f);
                    break;
                }
                case PieceKind.Tiles:
                {
                    // The rows go into the scene; TileGround builds the mesh and the ground collider from them on load.
                    int[] r = piece.Rect;
                    List<string> keys = piece.Legend.Keys.ToList();
                    var ground = group.gameObject.AddComponent<TileGround>();
                    ground.originX = r[0];
                    ground.originY = r[1];
                    ground.width = r[2] - r[0];
                    ground.height = r[3] - r[1];
                    ground.rows = piece.Rows.ToArray();
                    ground.kinds = string.Concat(keys);
                    ground.materials = keys.Select(k => Material(kit, region, piece.Legend[k].Material)).ToArray();
                    ground.lifts = keys.Select(k => piece.Legend[k].Height ?? 0f).ToArray();
                    ground.walkable = keys.Select(k => piece.Legend[k].Walkable).ToArray();
                    if (terrain != null)
                    {
                        // The terrain's corners under this piece's rectangle, row by row from the south.
                        var corners = new float[(ground.width + 1) * (ground.height + 1)];
                        for (int j = 0; j <= ground.height; j++)
                            for (int i = 0; i <= ground.width; i++)
                                corners[j * (ground.width + 1) + i] = terrain.Corner(r[0] + i, r[1] + j);
                        ground.corners = corners;
                        ground.cliffStep = region.Terrain.CliffStep;
                        ground.cliffMaterial = kit.Mat("LandCliff", Palette.TryParse("CliffRock", out Color cliff) ? cliff : new Color(0.42f, 0.4f, 0.36f));
                        ground.cliffMaterials = keys.Select(k => piece.Legend[k].Cliff is string own ? Material(kit, region, own) : null).ToArray();
                    }
                    ground.Rebuild();
                    break;
                }
                default:
                    throw new System.ArgumentException("unknown kind");
            }
            return group.gameObject;
        }

        /// <summary>A thing on one tile: its look (parts, a model, or the kind's default), then its own collision and markers.</summary>
        private static GameObject BuildPlaced(WorldKit kit, RegionData region, Piece piece, Transform parent, System.Func<int, Transform> level, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(piece.X + 0.5f, 0f, piece.Y + 0.5f);
            Transform look = WorldKit.Group(go.transform, "Look");

            if (piece.Parts != null && piece.Parts.Count > 0)
            {
                foreach (Part part in piece.Parts)
                {
                    GameObject p = GameObject.CreatePrimitive(part.Shape == "cylinder" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    Object.DestroyImmediate(p.GetComponent<Collider>());
                    p.name = string.IsNullOrWhiteSpace(part.Name) ? part.Shape : part.Name;
                    p.transform.SetParent(look, false);
                    p.transform.localPosition = V(part.At);
                    p.transform.localRotation = part.Rot != null ? Quaternion.Euler(V(part.Rot)) : Quaternion.identity;
                    p.transform.localScale = V(part.Size);
                    p.GetComponent<Renderer>().sharedMaterial = Material(kit, region, part.Material);
                }
            }
            else if (!string.IsNullOrWhiteSpace(piece.Model))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(piece.Model);
                if (prefab == null) Debug.LogError($"[R'th] {region.Id}: piece '{piece.Id}' names model '{piece.Model}', which isn't in the project.");
                else ((GameObject)PrefabUtility.InstantiatePrefab(prefab, look)).transform.localPosition = Vector3.zero;
            }
            else if (piece.Kind == PieceKind.Tree)
            {
                var treeKit = new WorldKit(region.Seed ^ StableHash(piece.Id));
                Material bark = treeKit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f));
                Material leaves = treeKit.Mat("Leaves", new Color(0.20f, 0.42f, 0.18f));
                treeKit.Tree(look, piece.X, piece.Y, bark, leaves, piece.Scale ?? 1f, piece.Variant ?? "Tree");
            }
            else if (piece.Kind == PieceKind.Rock)
                WorldKit.Rock(look, piece.X, piece.Y, kit.Mat("Stone", new Color(0.52f, 0.52f, 0.50f)), 0.8f * (piece.Scale ?? 1f));
            else if (piece.Kind == PieceKind.Prop)
                WorldKit.Box("Placeholder", look, go.transform.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), kit.Mat("Placeholder", new Color(0.8f, 0.2f, 0.8f)));

            // The look never collides or carries markers: the piece's data decides those.
            foreach (Collider c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (MonoBehaviour m in look.GetComponentsInChildren<MonoBehaviour>(true))
                if (m is MapBlocker || m is MapWall || m is MapObject || m is MapNpcSpawn || m is MapSpawn) Object.DestroyImmediate(m);
            go.transform.rotation = Quaternion.Euler(0f, piece.Rot ?? 0f, 0f);

            var tiles = RegionCompiler.FootprintTiles(piece).ToList();
            if (tiles.Count > 0)
            {
                float height = 1.2f;
                foreach (Renderer r in look.GetComponentsInChildren<Renderer>()) height = Mathf.Max(height, r.bounds.max.y);
                AddFootprint(go.transform, tiles.Min(t => t.x), tiles.Min(t => t.y), tiles.Max(t => t.x) + 1, tiles.Max(t => t.y) + 1, height);
            }
            if (piece.Kind == PieceKind.Tree) WorldKit.Fades(go.transform, 0.25f);

            if (piece.Object != null)
            {
                var obj = go.AddComponent<MapObject>();
                obj.objectType = piece.Object;
                if (piece.ClimbTo != null)
                {
                    var landing = new GameObject(name + " landing");
                    landing.transform.SetParent(level(piece.ClimbTo[0]), false);
                    landing.transform.position = new Vector3(piece.ClimbTo[1] + 0.5f, 0f, piece.ClimbTo[2] + 0.5f);
                    obj.climbTo = landing.transform;
                }
            }
            if (piece.Kind == PieceKind.Npc)
            {
                var spawn = go.AddComponent<MapNpcSpawn>();
                spawn.npcId = piece.Npc;
                spawn.wanderRadius = piece.Wander ?? -1;
            }
            if (piece.Kind == PieceKind.Spawn) go.AddComponent<MapSpawn>();
            return go;
        }

        /// <summary>
        /// An invisible collider over tiles [x0, x1) x [y0, y1), kept 0.2 inside their edges so the
        /// exporter counts exactly those tiles, with a <see cref="MapBlocker"/>. It is also what the
        /// player clicks.
        /// </summary>
        private static void AddFootprint(Transform owner, int x0, int y0, int x1, int y1, float height)
        {
            var f = new GameObject("Footprint");
            f.transform.SetParent(owner, false);
            f.transform.rotation = Quaternion.identity;
            f.transform.position = new Vector3((x0 + x1) / 2f, height / 2f, (y0 + y1) / 2f);
            f.AddComponent<BoxCollider>().size = new Vector3(x1 - x0 - 0.4f, height, y1 - y0 - 0.4f);
            if (owner.GetComponent<MapBlocker>() == null) owner.gameObject.AddComponent<MapBlocker>();
        }

        /// <summary>A material the region names, created once under Assets/Art/Materials.</summary>
        public static Material Material(WorldKit kit, RegionData region, string name)
        {
            if (name == null) return kit.Mat("Placeholder", new Color(0.8f, 0.2f, 0.8f));
            Color colour = new Color(0.8f, 0.2f, 0.8f);
            if (!region.Materials.TryGetValue(name, out string value) || !Palette.TryParse(value, out colour))
                Debug.LogWarning($"[R'th] {region.Id}: material '{name}' has no colour in the region's materials; drawn magenta.");
            // Keep a material whose colour already rounds to this one, rather than nudging it to the
            // nearest 8-bit value (which would touch every scene that shares it).
            var existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + name + ".mat");
            if (existing != null && existing.HasProperty("_BaseColor") &&
                ColorUtility.ToHtmlStringRGB(existing.GetColor("_BaseColor")) == ColorUtility.ToHtmlStringRGB(colour))
                return existing;
            return kit.Mat(name, colour);
        }

        /// <summary>A hash of a piece's id that is the same on every run (string.GetHashCode isn't), so a tree grows the same each sync.</summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return h;
            }
        }

        private static HashSet<Vector2Int> Tiles(IEnumerable<(int x, int y)> tiles) => new HashSet<Vector2Int>(tiles.Select(t => new Vector2Int(t.x, t.y)));

        private static Vector3 V(float[] a) => new Vector3(a.Length > 0 ? a[0] : 0f, a.Length > 1 ? a[1] : 0f, a.Length > 2 ? a[2] : 0f);
    }
}
