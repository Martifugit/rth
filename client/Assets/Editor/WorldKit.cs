using System.Collections.Generic;
using System.IO;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Building blocks for laying out regions in scenes: ground patches, water from tile sets,
    /// houses with doors and hideable roofs, fences, trees, stations and creature spawns. Every
    /// piece carries the right map markers (MapBlocker, MapWall, MapObject, MapNpcSpawn), so the
    /// exporter turns the scene straight into server collision and placements. Coordinates are
    /// tiles: tile (x, z) covers world X [x, x+1], Z [z, z+1].
    /// </summary>
    public sealed class WorldKit
    {
        private const string MaterialDir = "Assets/Art/Materials";
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly System.Random _random;

        public WorldKit(int seed)
        {
            _random = new System.Random(seed);
        }

        public float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
        public int Range(int min, int maxExclusive) => _random.Next(min, maxExclusive);
        public bool Chance(double p) => _random.NextDouble() < p;

        // ------------------------------------------------------------------ materials

        /// <summary>A flat, matte URP material, created once and saved under Assets/Art/Materials.</summary>
        public Material Mat(string name, Color color)
        {
            if (_materials.TryGetValue(name, out Material cached)) return cached;
            Directory.CreateDirectory(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            _materials[name] = material;
            return material;
        }

        // ------------------------------------------------------------------ primitives

        public static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true, float yaw = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A recoloured patch of walkable ground (fields, squares, roads). Its top sits just above the grass.</summary>
        public static GameObject Patch(string name, Transform parent, int x0, int z0, int x1, int z1, Material material, float lift = 0.004f)
        {
            return Box(name, parent, new Vector3((x0 + x1) / 2f, lift - 0.005f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.01f, z1 - z0), material);
        }

        /// <summary>Water over a set of tiles, one box per horizontal run. Blocks movement (MapBlocker).</summary>
        public static void Water(Transform parent, string name, ICollection<Vector2Int> tiles, Material material)
        {
            Transform group = Group(parent, name);
            var byRow = new SortedDictionary<int, List<int>>();
            foreach (Vector2Int t in tiles)
            {
                if (!byRow.TryGetValue(t.y, out List<int> row)) byRow[t.y] = row = new List<int>();
                row.Add(t.x);
            }
            foreach (KeyValuePair<int, List<int>> row in byRow)
            {
                row.Value.Sort();
                int start = row.Value[0];
                int prev = start;
                for (int i = 1; i <= row.Value.Count; i++)
                {
                    if (i < row.Value.Count && row.Value[i] == prev + 1)
                    {
                        prev = row.Value[i];
                        continue;
                    }
                    Box("Water", group, new Vector3((start + prev + 1) / 2f, -0.02f, row.Key + 0.5f), new Vector3(prev + 1 - start, 0.05f, 1f), material)
                        .AddComponent<MapBlocker>();
                    if (i < row.Value.Count) start = prev = row.Value[i];
                }
            }
        }

        /// <summary>Tiles within <paramref name="halfWidth"/> of a polyline, e.g. a river or road.</summary>
        public static HashSet<Vector2Int> Along(IList<Vector2> line, float halfWidth, int size)
        {
            var tiles = new HashSet<Vector2Int>();
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i];
                Vector2 b = line[i + 1];
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - halfWidth) - 1;
                int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + halfWidth) + 1;
                int minZ = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - halfWidth) - 1;
                int maxZ = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + halfWidth) + 1;
                for (int z = Mathf.Max(0, minZ); z <= Mathf.Min(size - 1, maxZ); z++)
                for (int x = Mathf.Max(0, minX); x <= Mathf.Min(size - 1, maxX); x++)
                    if (DistanceToSegment(new Vector2(x + 0.5f, z + 0.5f), a, b) <= halfWidth) tiles.Add(new Vector2Int(x, z));
            }
            return tiles;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>A road: recoloured ground along a polyline, drawn as one patch per tile run.</summary>
        public static void Road(Transform parent, string name, ICollection<Vector2Int> tiles, Material material)
        {
            Transform group = Group(parent, name);
            foreach (Vector2Int t in tiles)
                Box("Road", group, new Vector3(t.x + 0.5f, 0.001f, t.y + 0.5f), new Vector3(1f, 0.01f, 1f), material);
        }

        // ------------------------------------------------------------------ walls and buildings

        /// <summary>A wall along a grid line from (x0,z0) to (x1,z1), with gaps at the given tiles. Blocks edges (MapWall).</summary>
        public static void Wall(Transform parent, string name, int x0, int z0, int x1, int z1, Material material, float height = 1.7f, float thickness = 0.2f, params int[] gaps)
        {
            bool alongX = z0 == z1;
            int from = alongX ? Mathf.Min(x0, x1) : Mathf.Min(z0, z1);
            int to = alongX ? Mathf.Max(x0, x1) : Mathf.Max(z0, z1);
            var gapSet = new HashSet<int>(gaps);
            int runStart = from;
            for (int t = from; t <= to; t++)
            {
                bool end = t == to || gapSet.Contains(t);
                if (!end) continue;
                if (t > runStart)
                {
                    float a = runStart;
                    float b = t;
                    Vector3 center = alongX
                        ? new Vector3((a + b) / 2f, height / 2f, z0)
                        : new Vector3(x0, height / 2f, (a + b) / 2f);
                    Vector3 size = alongX
                        ? new Vector3(b - a + thickness, height, thickness)
                        : new Vector3(thickness, height, b - a + thickness);
                    Box(name, parent, center, size, material).AddComponent<MapWall>();
                }
                runStart = t + 1;
            }
        }

        public enum Side
        {
            North,
            East,
            South,
            West,
        }

        /// <summary>
        /// A house covering tiles [x0,x1) x [z0,z1): floor, four walls with a door gap at tile
        /// <paramref name="doorAt"/> on <paramref name="doorSide"/>, and a roof that hides while the
        /// local player is inside.
        /// </summary>
        public Transform House(Transform parent, string name, int x0, int z0, int x1, int z1, Side doorSide, int doorAt,
            Material walls, Material floor, Material roof)
        {
            Transform house = Group(parent, name);
            Patch("Floor", house, x0, z0, x1, z1, floor, 0.02f);
            Transform wallGroup = Group(house, "Walls");
            Wall(wallGroup, "Wall S", x0, z0, x1, z0, walls, gaps: doorSide == Side.South ? new[] { doorAt } : new int[0]);
            Wall(wallGroup, "Wall N", x0, z1, x1, z1, walls, gaps: doorSide == Side.North ? new[] { doorAt } : new int[0]);
            Wall(wallGroup, "Wall W", x0, z0, x0, z1, walls, gaps: doorSide == Side.West ? new[] { doorAt } : new int[0]);
            Wall(wallGroup, "Wall E", x1, z0, x1, z1, walls, gaps: doorSide == Side.East ? new[] { doorAt } : new int[0]);
            Fades(wallGroup, 0.35f);

            // A simple pitched roof: two slabs meeting at a ridge along the longer side.
            Transform roofGroup = Group(house, "Roof");
            bool ridgeAlongX = x1 - x0 >= z1 - z0;
            float w = x1 - x0 + 0.6f;
            float d = z1 - z0 + 0.6f;
            var c = new Vector3((x0 + x1) / 2f, 2.05f, (z0 + z1) / 2f);
            float span = (ridgeAlongX ? d : w) / 2f;
            float pitch = 32f;
            float slab = span / Mathf.Cos(pitch * Mathf.Deg2Rad);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 offset = ridgeAlongX ? new Vector3(0f, 0.45f, s * span / 2f) : new Vector3(s * span / 2f, 0.45f, 0f);
                GameObject half = Box("Slab", roofGroup, c + offset, ridgeAlongX ? new Vector3(w, 0.12f, slab) : new Vector3(slab, 0.12f, d), roof, collider: false);
                // Each slab tilts so its outer edge is low and its ridge edge high.
                half.transform.rotation = ridgeAlongX ? Quaternion.Euler(s * pitch, 0f, 0f) : Quaternion.Euler(0f, 0f, -s * pitch);
            }
            Fades(roofGroup, 0.2f);
            var hider = roofGroup.gameObject.AddComponent<RoofHider>();
            hider.interior = new RectInt(x0, z0, x1 - x0, z1 - z0);
            return house;
        }

        /// <summary>Lets something fade when it blocks the camera's view of the player.</summary>
        public static Occluder Fades(Transform target, float minAlpha = 0.25f)
        {
            Occluder o = target.GetComponent<Occluder>();
            if (o == null) o = target.gameObject.AddComponent<Occluder>();
            o.minAlpha = minAlpha;
            return o;
        }

        /// <summary>A rail fence along a grid line with gaps; blocks edges like a wall but low and see-through.</summary>
        public static void Fence(Transform parent, string name, int x0, int z0, int x1, int z1, Material material, params int[] gaps)
        {
            Transform group = Group(parent, name);
            Wall(group, "Rail", x0, z0, x1, z1, material, 0.12f, 0.08f, gaps);
            foreach (Transform rail in group)
            {
                Vector3 p = rail.position;
                rail.position = new Vector3(p.x, 0.55f, p.z);
            }
            bool alongX = z0 == z1;
            int from = alongX ? Mathf.Min(x0, x1) : Mathf.Min(z0, z1);
            int to = alongX ? Mathf.Max(x0, x1) : Mathf.Max(z0, z1);
            for (int t = from; t <= to; t++)
            {
                Vector3 at = alongX ? new Vector3(t, 0.4f, z0) : new Vector3(x0, 0.4f, t);
                Box("Post", group, at, new Vector3(0.12f, 0.8f, 0.12f), material, collider: false);
            }
        }

        // ------------------------------------------------------------------ nature and props

        /// <summary>
        /// A tree on a tile: blocks that tile (trunk collider), with a stacked low-poly crown, and
        /// fades when it hides the player. Trees are placed deliberately (they'll be a resource),
        /// RuneScape-sparse: a "large forest" is a few dozen.
        /// </summary>
        public void Tree(Transform parent, int x, int z, Material bark, Material leaves, float scale = 1f, string kind = "Tree")
        {
            var tree = new GameObject(kind);
            Fades(tree.transform, 0.25f);
            tree.transform.SetParent(parent, false);
            tree.transform.position = new Vector3(x + 0.5f, 0f, z + 0.5f);
            tree.AddComponent<MapBlocker>();
            float s = scale * Range(0.85f, 1.2f);
            Box("Trunk", tree.transform, tree.transform.position + Vector3.up * 0.8f * s, new Vector3(0.32f, 1.6f * s, 0.32f), bark);
            int layers = Chance(0.5) ? 2 : 3;
            for (int i = 0; i < layers; i++)
            {
                float w = (1.6f - i * 0.45f) * s;
                Box("Leaves", tree.transform, tree.transform.position + Vector3.up * (1.7f + i * 0.55f) * s, new Vector3(w, 0.7f * s, w), leaves,
                    collider: false, yaw: Range(0f, 90f));
            }
        }

        /// <summary>A clump of reeds: decoration only, walkable.</summary>
        public void Reeds(Transform parent, int x, int z, Material reed)
        {
            Transform clump = Group(parent, "Reeds");
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(x + Range(0.15f, 0.85f), 0f, z + Range(0.15f, 0.85f));
                float h = Range(0.5f, 0.9f);
                Box("Reed", clump, p + Vector3.up * h / 2f, new Vector3(0.05f, h, 0.05f), reed, collider: false, yaw: Range(0f, 90f));
            }
        }

        public static void Rock(Transform parent, int x, int z, Material stone, float size = 0.8f)
        {
            Box("Rock", parent, new Vector3(x + 0.5f, size * 0.35f, z + 0.5f), new Vector3(size, size * 0.7f, size), stone, yaw: (x * 37 + z * 11) % 60)
                .AddComponent<MapBlocker>();
        }

        // ------------------------------------------------------------------ map markers

        public static Transform Station(Transform parent, string name, string type, int x, int z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x + 0.5f, 0f, z + 0.5f);
            go.AddComponent<MapObject>().objectType = type;
            go.AddComponent<MapBlocker>();
            return go.transform;
        }

        public static void Spawn(Transform parent, string npc, int x, int z, int wander = -1)
        {
            var go = new GameObject(npc);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x + 0.5f, 0f, z + 0.5f);
            var spawn = go.AddComponent<MapNpcSpawn>();
            spawn.npcId = npc;
            spawn.wanderRadius = wander;
        }
    }
}
