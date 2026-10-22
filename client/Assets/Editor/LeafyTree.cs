using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>What a leafy tree looks like: its height, trunk and canopy lobes. Sizes in tiles.</summary>
    public sealed class LeafyTreeSpec
    {
        public string Name;
        /// <summary>Tiles it blocks along each side (1 or 2); the trunk stands in the middle of them.</summary>
        public int Footprint = 1;
        public float TrunkTop;
        public float TrunkRadius;
        /// <summary>Canopy lobes: centre (relative to the trunk's foot) and radii.</summary>
        public List<(Vector3 centre, Vector3 radii)> Lobes = new List<(Vector3, Vector3)>();
        public int Seed;
    }

    /// <summary>
    /// Builds the low-poly leafy trees of docs/assets.md: a plain trunk, and for each canopy lobe a
    /// dark solid core inside a slightly larger shell of big faces that each show the whole
    /// leaf-clump texture, its ragged fringe on the face's lowest edge (so the canopy's outline
    /// breaks up and the core shows through the gaps). Three submeshes: 0 bark, 1 core, 2 leaves.
    /// Meshes are saved under Assets/Art/Trees and rebuilt from code each time a builder runs.
    /// </summary>
    public static class LeafyTree
    {
        public const string MeshFolder = "Assets/Art/Trees";

        /// <summary>About 3 tiles tall, blocks one tile, crown about 2.4 across.</summary>
        public static LeafyTreeSpec Small => new LeafyTreeSpec
        {
            Name = "LeafyTreeSmall",
            Footprint = 1,
            TrunkTop = 1.7f,
            TrunkRadius = 0.13f,
            Lobes = { (new Vector3(0f, 2.15f, 0f), new Vector3(1.0f, 0.82f, 1.0f)) },
            Seed = 3,
        };

        /// <summary>About 4 tiles tall, blocks 2 × 2, crown about 3.5 across.</summary>
        public static LeafyTreeSpec Large => new LeafyTreeSpec
        {
            Name = "LeafyTreeLarge",
            Footprint = 2,
            TrunkTop = 2.3f,
            TrunkRadius = 0.22f,
            Lobes =
            {
                (new Vector3(0f, 2.95f, 0f), new Vector3(1.3f, 1.0f, 1.3f)),
                (new Vector3(0.85f, 2.35f, 0.35f), new Vector3(0.85f, 0.7f, 0.85f)),
                (new Vector3(-0.6f, 2.25f, -0.7f), new Vector3(0.8f, 0.66f, 0.8f)),
            },
            Seed = 5,
        };

        /// <summary>The tree's mesh as an asset (made or refreshed), so scenes can reference it.</summary>
        public static Mesh MeshAsset(LeafyTreeSpec spec)
        {
            Directory.CreateDirectory(MeshFolder);
            string path = MeshFolder + "/" + spec.Name + ".asset";
            Mesh built = Build(spec);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            EditorUtility.CopySerialized(built, existing);
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static Mesh Build(LeafyTreeSpec spec)
        {
            var b = new Builder(spec.Seed);
            Trunk(b, spec);
            foreach ((Vector3 centre, Vector3 radii) in spec.Lobes)
            {
                Core(b, centre, radii * 0.8f);
                Shell(b, centre, radii);
            }
            return b.ToMesh(spec.Name);
        }

        // ------------------------------------------------------------------ parts

        private static void Trunk(Builder b, LeafyTreeSpec spec)
        {
            const int sides = 6;
            float r = spec.TrunkRadius;
            (float y, float radius)[] rings = { (0f, r * 1.4f), (spec.TrunkTop * 0.3f, r), (spec.TrunkTop, r * 0.7f) };
            Prism(b, Vector3.zero, Vector3.up, rings, sides, b.Range(0f, 60f));

            // Branches out to the side lobes.
            for (int i = 1; i < spec.Lobes.Count; i++)
            {
                Vector3 from = new Vector3(0f, spec.TrunkTop * 0.7f, 0f);
                Vector3 to = Vector3.Lerp(from, spec.Lobes[i].centre, 0.75f);
                Vector3 dir = to - from;
                Prism(b, from, dir.normalized, new[] { (0f, r * 0.5f), (dir.magnitude, r * 0.35f) }, 4, 45f);
            }
        }

        /// <summary>A tapering prism along an axis; rings are (distance along it, radius). Bark submesh.</summary>
        private static void Prism(Builder b, Vector3 origin, Vector3 axis, (float at, float radius)[] rings, int sides, float turn)
        {
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 other = Vector3.Cross(axis, side);
            var ringStart = new List<int>();
            foreach ((float at, float radius) in rings)
            {
                ringStart.Add(b.Count);
                for (int s = 0; s < sides; s++)
                {
                    float a = (s * 360f / sides + turn) * Mathf.Deg2Rad;
                    Vector3 outward = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                    b.Vertex(origin + axis * at + outward * radius, outward, Vector2.zero);
                }
            }
            for (int k = 0; k + 1 < rings.Length; k++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = ringStart[k] + s, a1 = ringStart[k] + (s + 1) % sides;
                    int b0 = ringStart[k + 1] + s, b1 = ringStart[k + 1] + (s + 1) % sides;
                    Vector3 outward = (b.Position(a0) + b.Position(a1)) * 0.5f - (origin + axis * rings[k].at);
                    b.Triangle(0, a0, b0, b1, outward);
                    b.Triangle(0, a0, b1, a1, outward);
                }
        }

        /// <summary>The dark solid inside the leaves: an icosahedron, smooth-lit.</summary>
        private static void Core(Builder b, Vector3 centre, Vector3 radii)
        {
            (List<Vector3> points, List<int[]> faces) = Icosahedron();
            int start = b.Count;
            foreach (Vector3 u in points) b.Vertex(centre + Vector3.Scale(u, radii), u, Vector2.zero);
            foreach (int[] f in faces)
                b.Triangle(1, start + f[0], start + f[1], start + f[2], points[f[0]] + points[f[1]] + points[f[2]]);
        }

        /// <summary>The leaf shell: 80 big faces, each a little enlarged so they overlap, each showing the whole texture.</summary>
        private static void Shell(Builder b, Vector3 centre, Vector3 radii)
        {
            (List<Vector3> points, List<int[]> faces) = Icosahedron();
            Subdivide(points, faces);
            var jitter = new float[points.Count];
            for (int i = 0; i < jitter.Length; i++) jitter[i] = b.Range(0.9f, 1.08f);

            foreach (int[] f in faces)
            {
                var p = new Vector3[3];
                for (int k = 0; k < 3; k++) p[k] = centre + Vector3.Scale(points[f[k]] * jitter[f[k]], radii);
                Vector3 g = (p[0] + p[1] + p[2]) / 3f;
                Vector3 outward = (points[f[0]] + points[f[1]] + points[f[2]]).normalized;
                for (int k = 0; k < 3; k++) p[k] = g + (p[k] - g) * 1.18f + outward * 0.03f;

                // The fringe goes on the lowest edge (leaves hang), now and then on another one.
                int lowest = 0;
                float best = float.MaxValue;
                for (int e = 0; e < 3; e++)
                {
                    float y = p[e].y + p[(e + 1) % 3].y;
                    if (y < best)
                    {
                        best = y;
                        lowest = e;
                    }
                }
                if (b.Chance(0.2f)) lowest = (lowest + 1) % 3;
                int e0 = lowest, e1 = (lowest + 1) % 3, apex = (lowest + 2) % 3;
                int i0 = b.Vertex(p[e0], points[f[e0]], new Vector2(0f, 0f));
                int i1 = b.Vertex(p[e1], points[f[e1]], new Vector2(1f, 0f));
                int i2 = b.Vertex(p[apex], points[f[apex]], new Vector2(0.5f, 1f));
                b.Triangle(2, i0, i1, i2, outward);
            }
        }

        // ------------------------------------------------------------------ geometry

        private static (List<Vector3>, List<int[]>) Icosahedron()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var faces = new List<int[]>
            {
                new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
                new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
                new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
            };
            return (points, faces);
        }

        private static void Subdivide(List<Vector3> points, List<int[]> faces)
        {
            var mid = new Dictionary<(int, int), int>();
            int Mid(int a, int c)
            {
                (int, int) key = a < c ? (a, c) : (c, a);
                if (mid.TryGetValue(key, out int i)) return i;
                points.Add(((points[a] + points[c]) * 0.5f).normalized);
                return mid[key] = points.Count - 1;
            }
            var next = new List<int[]>();
            foreach (int[] f in faces)
            {
                int ab = Mid(f[0], f[1]), bc = Mid(f[1], f[2]), ca = Mid(f[2], f[0]);
                next.Add(new[] { f[0], ab, ca });
                next.Add(new[] { f[1], bc, ab });
                next.Add(new[] { f[2], ca, bc });
                next.Add(new[] { ab, bc, ca });
            }
            faces.Clear();
            faces.AddRange(next);
        }

        /// <summary>Collects vertices and per-submesh triangles, turning every triangle to face outward.</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _positions = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _triangles = { new List<int>(), new List<int>(), new List<int>() };
            private readonly System.Random _random;

            public Builder(int seed) => _random = new System.Random(seed);

            public int Count => _positions.Count;
            public Vector3 Position(int i) => _positions[i];
            public float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
            public bool Chance(float p) => _random.NextDouble() < p;

            public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                _positions.Add(position);
                _normals.Add(normal.normalized);
                _uvs.Add(uv);
                return _positions.Count - 1;
            }

            public void Triangle(int submesh, int a, int b, int c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(_positions[b] - _positions[a], _positions[c] - _positions[a]);
                if (Vector3.Dot(n, outward) < 0f) (b, c) = (c, b); // Unity draws the side the cross product points to
                _triangles[submesh].Add(a);
                _triangles[submesh].Add(b);
                _triangles[submesh].Add(c);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_positions);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = _triangles.Length;
                for (int s = 0; s < _triangles.Length; s++) mesh.SetTriangles(_triangles[s], s);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
