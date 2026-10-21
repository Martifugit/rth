using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// Builds chunky, flat-shaded meshes from tapered prisms: the look of an Old School model,
    /// where every face is one flat colour. Each face gets its own vertices so normals stay hard.
    /// </summary>
    public sealed class LowPolyMesh
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>
        /// A prism from <paramref name="bottomY"/> to <paramref name="topY"/> with an elliptical
        /// cross-section of the given half-sizes at each end (flats sit at the half-size, so four
        /// sides make a box). <paramref name="center"/> shifts it in x/z; <paramref name="topShift"/>
        /// slants the top end.
        /// </summary>
        public LowPolyMesh Prism(int sides, float bottomY, float topY, Vector2 bottomHalf, Vector2 topHalf,
            Vector2 center = default, Vector2 topShift = default, bool capBottom = true, bool capTop = true)
        {
            sides = Mathf.Max(3, sides);
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            float step = Mathf.PI * 2f / sides;
            float offset = step / 2f;                     // a flat face at the front, not a ridge
            float flatToCorner = 1f / Mathf.Cos(step / 2f);
            for (int i = 0; i < sides; i++)
            {
                float a = offset + i * step;
                float cx = Mathf.Sin(a) * flatToCorner, cz = Mathf.Cos(a) * flatToCorner;
                bottom[i] = new Vector3(center.x + cx * bottomHalf.x, bottomY, center.y + cz * bottomHalf.y);
                top[i] = new Vector3(center.x + topShift.x + cx * topHalf.x, topY, center.y + topShift.y + cz * topHalf.y);
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Quad(bottom[j], top[j], top[i], bottom[i]);
            }
            if (capTop) Fan(top, false);
            if (capBottom) Fan(bottom, true);
            return this;
        }

        /// <summary>An axis-aligned box between two corners.</summary>
        public LowPolyMesh Box(Vector3 min, Vector3 max)
        {
            Vector3 size = max - min;
            var c = new Vector2(min.x + size.x / 2f, min.z + size.z / 2f);
            var half = new Vector2(size.x / 2f, size.z / 2f);
            return Prism(4, min.y, max.y, half, half, c);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Corners in clockwise order seen from outside (Unity's front face).
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.Cross(c - a, d - a);
            n.Normalize();
            int start = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
            for (int i = 0; i < 4; i++) _normals.Add(n);
            _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
            _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 3);
        }

        private void Fan(Vector3[] ring, bool facingDown)
        {
            Vector3 n = facingDown ? Vector3.down : Vector3.up;
            // Slanted ends are not quite flat; use the real face normal.
            if (ring.Length >= 3)
            {
                Vector3 real = Vector3.Cross(ring[1] - ring[0], ring[2] - ring[0]).normalized;
                if (Vector3.Dot(real, n) < 0f) real = -real;
                n = real;
            }
            int start = _vertices.Count;
            foreach (Vector3 v in ring)
            {
                _vertices.Add(v);
                _normals.Add(n);
            }
            for (int i = 1; i < ring.Length - 1; i++)
            {
                if (facingDown)
                {
                    _triangles.Add(start); _triangles.Add(start + i + 1); _triangles.Add(start + i);
                }
                else
                {
                    _triangles.Add(start); _triangles.Add(start + i); _triangles.Add(start + i + 1);
                }
            }
        }
    }
}
