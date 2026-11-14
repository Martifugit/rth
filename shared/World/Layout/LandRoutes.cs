using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// Where the roads, rivers and passes really run on the ground, simplified to about a tile and a
    /// half (world/layout/routes.json, written when the blockout first laid the land out; edit it as the
    /// ground changes). The world map draws these lines instead of the layout's straight ones.
    /// </summary>
    public sealed class LandRoutes
    {
        public const string FileName = "routes.json";

        public sealed class Line
        {
            public string Id = "";
            public string Kind = "";
            /// <summary>x, y pairs in whole tiles.</summary>
            public int[] Points = Array.Empty<int>();
        }

        public string Comment = "Where the roads, rivers and passes run on the ground, for the world map.";
        public List<Line> Lines = new List<Line>();

        /// <summary>The file's text: one line per route, so it diffs well.</summary>
        public string ToJson()
        {
            var sb = new System.Text.StringBuilder("{\n  \"comment\": " + JsonConvert.ToString(Comment) + ",\n  \"lines\": [\n");
            for (int k = 0; k < Lines.Count; k++)
            {
                Line l = Lines[k];
                sb.Append("    { \"id\": ").Append(JsonConvert.ToString(l.Id)).Append(", \"kind\": ").Append(JsonConvert.ToString(l.Kind))
                  .Append(", \"points\": [").Append(string.Join(", ", l.Points)).Append("] }").Append(k < Lines.Count - 1 ? ",\n" : "\n");
            }
            return sb.Append("  ]\n}\n").ToString();
        }

        public static LandRoutes Parse(string json) => JsonConvert.DeserializeObject<LandRoutes>(json) ?? new LandRoutes();

        /// <summary>Douglas-Peucker: the fewest points within the tolerance of the line.</summary>
        public static List<float[]> Simplify(List<float[]> pts, float tolerance)
        {
            if (pts.Count < 3) return pts.ToList();
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<(int a, int b)>();
            stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                (int a, int b) = stack.Pop();
                float worst = 0f;
                int at = -1;
                for (int k = a + 1; k < b; k++)
                {
                    float d = Geometry.DistanceToSegment(pts[a][0], pts[a][1], pts[b][0], pts[b][1], pts[k][0], pts[k][1]);
                    if (d > worst) { worst = d; at = k; }
                }
                if (at < 0 || worst <= tolerance) continue;
                keep[at] = true;
                stack.Push((a, at));
                stack.Push((at, b));
            }
            return pts.Where((p, k) => keep[k]).ToList();
        }
    }
}
