using System;
using System.Collections.Generic;
using System.Linq;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// The layout's landforms (<see cref="ReliefDef"/>, docs/world-plan.md) made ready to raise the
    /// land: each one's shape resolved once, then asked how much it adds at a tile.
    /// </summary>
    public sealed class Relief
    {
        private sealed class Form
        {
            public ReliefDef Def = null!;
            public List<float[]>? Poly, Line;
            public float MinX, MinY, MaxX, MaxY;
            /// <summary>An ellipse's mean radius, for slopes measured in tiles.</summary>
            public float Radius;
        }

        private readonly List<Form> _forms = new List<Form>();
        private readonly List<Form> _terraces = new List<Form>();
        private readonly List<Form> _cliffs = new List<Form>();
        private readonly int _seed;

        public Relief(ContinentData data)
        {
            _seed = data.Seed;
            foreach (ReliefDef d in data.Relief)
            {
                var f = new Form { Def = d };
                if (d.Poly != null) f.Poly = d.Rough > 0 ? Geometry.Roughen(d.Poly, d.Rough, data.Seed) : d.Poly;
                if (d.Line != null) f.Line = d.Line;
                float pad = d.Kind == "scarp" ? d.Reach + 60f : d.Kind == "ridge" ? d.HalfWidth : d.Kind == "crater" ? d.Edge * 4f : d.Kind == "hill" && d.Poly != null ? 0f : 0f;
                if (f.Poly != null || f.Line != null)
                {
                    List<float[]> pts = f.Poly ?? f.Line!;
                    f.MinX = pts.Min(p => p[0]) - pad; f.MaxX = pts.Max(p => p[0]) + pad;
                    f.MinY = pts.Min(p => p[1]) - pad; f.MaxY = pts.Max(p => p[1]) + pad;
                }
                else if (d.Ellipse != null)
                {
                    float grow = d.Kind == "crater" ? 1.6f : 1f;
                    f.MinX = d.Ellipse[0] - d.Ellipse[2] * grow; f.MaxX = d.Ellipse[0] + d.Ellipse[2] * grow;
                    f.MinY = d.Ellipse[1] - d.Ellipse[3] * grow; f.MaxY = d.Ellipse[1] + d.Ellipse[3] * grow;
                    f.Radius = (d.Ellipse[2] + d.Ellipse[3]) / 2f;
                }
                else continue;
                if (d.Kind == "terraces") _terraces.Add(f);
                else if (d.Kind == "cliffs") _cliffs.Add(f);
                else _forms.Add(f);
            }
        }

        public static List<string> Validate(ContinentData data)
        {
            var errors = new List<string>();
            string[] kinds = { "hill", "cone", "mesa", "ridge", "scarp", "crater", "dunes", "tors", "canyons", "terraces", "cliffs" };
            foreach (ReliefDef d in data.Relief)
            {
                if (!kinds.Contains(d.Kind)) errors.Add($"relief {d.Id}: unknown kind '{d.Kind}' ({string.Join(", ", kinds)})");
                bool line = d.Kind == "ridge" || d.Kind == "scarp";
                if (line && (d.Line == null || d.Line.Count < 2)) errors.Add($"relief {d.Id}: a {d.Kind} needs a line");
                if (!line && d.Poly == null && d.Ellipse == null) errors.Add($"relief {d.Id}: a {d.Kind} needs a poly or an ellipse");
                if ((d.Kind == "cone" || d.Kind == "crater") && d.Ellipse == null) errors.Add($"relief {d.Id}: a {d.Kind} needs an ellipse");
                if (d.Kind == "ridge" && d.HalfWidth <= 0) errors.Add($"relief {d.Id}: a ridge needs halfWidth");
            }
            return errors;
        }

        /// <summary>How much the landforms add at a point, in tiles.</summary>
        public float At(float x, float y)
        {
            float sum = 0f;
            foreach (Form f in _forms)
            {
                if (x < f.MinX || x > f.MaxX || y < f.MinY || y > f.MaxY) continue;
                sum += Contribution(f, x, y);
            }
            return sum;
        }

        /// <summary>Whether a point is on a cliff coast (the land keeps its height to the water's edge).</summary>
        public bool OnCliffCoast(float x, float y)
        {
            foreach (Form f in _cliffs)
                if (x >= f.MinX && x <= f.MaxX && y >= f.MinY && y <= f.MaxY && Inside(f, x, y) > 0f) return true;
            return false;
        }

        /// <summary>Steps the land into shelves where a terraces landform says so.</summary>
        public float Terrace(float x, float y, float h)
        {
            foreach (Form f in _terraces)
            {
                if (x < f.MinX || x > f.MaxX || y < f.MinY || y > f.MaxY) continue;
                float w = Inside(f, x, y);
                if (w <= 0f) continue;
                float step = Math.Max(0.2f, f.Def.Step);
                float k = (float)Math.Floor(h / step), frac = h / step - k;
                // Flat shelves, each rising to the next over the last fifth of the step: the risers are cliffs.
                float stepped = step * (k + LandNoise.Smooth((frac - 0.8f) / 0.2f));
                h += (stepped - h) * w;
            }
            return h;
        }

        /// <summary>0 outside the shape, rising to 1 inside over its edge (tiles), for polygons and ellipses.</summary>
        private static float Inside(Form f, float x, float y)
        {
            ReliefDef d = f.Def;
            float edge = Math.Max(1f, d.Edge);
            if (f.Poly != null)
            {
                if (!Geometry.InPolygon(f.Poly, x, y)) return 0f;
                float dist = DistanceToRing(f.Poly, x, y);
                return LandNoise.Smooth(dist / RampedEdge(d, x, y, edge));
            }
            if (d.Ellipse != null)
            {
                float r = EllipseR(d.Ellipse, x, y);
                if (r >= 1f) return 0f;
                return LandNoise.Smooth((1f - r) * f.Radius / RampedEdge(d, x, y, edge));
            }
            return 0f;
        }

        /// <summary>
        /// A mesa's slope along one of its ramps: a corridor from the ramp's point on the rim towards
        /// the middle, where the slope is as long as the ramp, so the top can be walked up to.
        /// </summary>
        private static float RampedEdge(ReliefDef d, float x, float y, float edge)
        {
            if (d.Ramps.Count == 0) return edge;
            float cx, cy;
            if (d.Ellipse != null) { cx = d.Ellipse[0]; cy = d.Ellipse[1]; }
            else { cx = d.Poly!.Average(p => p[0]); cy = d.Poly!.Average(p => p[1]); }
            foreach (RampDef ramp in d.Ramps)
            {
                float ux = ramp.At[0] - cx, uy = ramp.At[1] - cy;
                float len = (float)Math.Sqrt(ux * ux + uy * uy);
                if (len <= 0f) continue;
                ux /= len; uy /= len;
                float along = (x - cx) * ux + (y - cy) * uy;
                if (along < 0f) continue;
                float lateral = Math.Abs(-(x - cx) * uy + (y - cy) * ux);
                float k = LandNoise.Smooth(1f - (lateral - ramp.HalfWidth) / 4f);
                if (k > 0f) edge += (ramp.Length - edge) * k;
            }
            return edge;
        }

        private float Contribution(Form f, float x, float y)
        {
            ReliefDef d = f.Def;
            switch (d.Kind)
            {
                case "hill":
                {
                    if (d.Ellipse != null)
                    {
                        float r = EllipseR(d.Ellipse, x, y);
                        if (r >= 1f) return 0f;
                        float u = 1f - r * r;
                        return d.Height * u * u;
                    }
                    if (!Geometry.InPolygon(f.Poly!, x, y)) return 0f;
                    return d.Height * LandNoise.Smooth(DistanceToRing(f.Poly!, x, y) / Math.Max(1f, d.Edge));
                }
                case "cone":
                {
                    float r = EllipseR(d.Ellipse!, x, y);
                    if (r >= 1f) return 0f;
                    // Concave flanks (steep near the top, spreading at the foot), and a crater in the top.
                    float h = d.Height * (float)Math.Pow(1f - r, 1.35f);
                    if (d.Crater > 0f)
                    {
                        float rc = d.Crater / f.Radius;
                        if (r < rc) h -= d.CraterDepth * (1f - (r / rc) * (r / rc));
                    }
                    return h;
                }
                case "mesa":
                    return d.Height * Inside(f, x, y);
                case "ridge":
                {
                    float dist = Geometry.DistanceToLine(f.Line!, x, y);
                    if (dist >= d.HalfWidth) return 0f;
                    float wobble = 1f + 0.18f * LandNoise.Value(_seed + 31, x / 23f, y / 23f);
                    return d.Height * wobble * LandNoise.Smooth(1f - dist / d.HalfWidth);
                }
                case "scarp":
                    return Scarp(f, x, y);
                case "crater":
                {
                    float r = EllipseR(d.Ellipse!, x, y);
                    float edge = Math.Max(1f, d.Edge) / f.Radius;
                    // A bowl inside, a rim at the edge, and the rim's outer slope falling away.
                    if (r < 1f) return -d.CraterDepth * (1f - r * r) + d.Height * LandNoise.Smooth((r - (1f - edge)) / edge);
                    return d.Height * Math.Max(0f, 1f - (r - 1f) / (edge * 3f));
                }
                case "dunes":
                {
                    float w = Inside(f, x, y);
                    if (w <= 0f) return 0f;
                    double a = d.Angle * Math.PI / 180.0;
                    float u = (float)(x * Math.Cos(a) + y * Math.Sin(a)), v = (float)(-x * Math.Sin(a) + y * Math.Cos(a));
                    float phase = u / d.Scale + 1.3f * LandNoise.Value(_seed + 41, v / (d.Scale * 2.5f), u / (d.Scale * 4f));
                    float frac = phase - (float)Math.Floor(phase);
                    // Long gentle windward slopes and short steep lee faces.
                    float crest = frac < 0.75f ? frac / 0.75f : (1f - frac) / 0.25f;
                    return d.Height * w * crest * crest * (0.75f + 0.25f * LandNoise.Value(_seed + 42, x / 37f, y / 37f));
                }
                case "canyons":
                {
                    // A table of rock split by branching cracks: high everywhere except along the
                    // ridged noise's crests, which drop to the floor between steep walls.
                    float w = Inside(f, x, y);
                    if (w <= 0f) return 0f;
                    float t = LandNoise.Ridged(_seed + 71, x / d.Scale, y / d.Scale);
                    float table = 1f - LandNoise.Smooth((t - 0.72f) / 0.12f);
                    return d.Height * w * (0.25f + 0.75f * table);
                }
                case "tors":
                {
                    float w = Inside(f, x, y);
                    if (w <= 0f) return 0f;
                    float t = LandNoise.Ridged(_seed + 51, x / d.Scale, y / d.Scale);
                    float knob = Math.Max(0f, t - 0.86f) / 0.14f;
                    float swell = 0.5f * (1f + LandNoise.Value(_seed + 52, x / (d.Scale * 3f), y / (d.Scale * 3f)));
                    return w * (d.Height * knob * knob * swell + 0.35f * d.Height * swell);
                }
            }
            return 0f;
        }

        /// <summary>
        /// A step along a line: the land on the high side is raised by the full height for its reach
        /// (then fades), climbing over the edge's width centred on the line, or over a ramp's length
        /// near a ramp.
        /// </summary>
        private float Scarp(Form f, float x, float y)
        {
            ReliefDef d = f.Def;
            (float dist, float side, float px, float py) = Signed(f.Line!, x, y);
            float signed = d.Side == "left" ? -side * dist : side * dist;
            float edge = Math.Max(0.5f, d.Edge);
            // The face wanders a little, so it isn't ruled.
            signed += 1.5f * LandNoise.Value(_seed + 61, x / 9f, y / 9f);
            foreach (RampDef ramp in d.Ramps)
            {
                float along = (float)Math.Sqrt((px - ramp.At[0]) * (px - ramp.At[0]) + (py - ramp.At[1]) * (py - ramp.At[1]));
                float k = LandNoise.Smooth(1f - (along - ramp.HalfWidth) / 6f);
                if (k > 0f) edge += (ramp.Length - edge) * k;
            }
            float rise = LandNoise.Smooth((signed + edge / 2f) / edge);
            if (signed > d.Reach) rise *= Math.Max(0f, 1f - (signed - d.Reach) / 60f);
            return d.Height * rise;
        }

        /// <summary>Distance to a polyline, which side of it a point is (+1 right of its direction, -1 left) and the nearest point.</summary>
        private static (float dist, float side, float px, float py) Signed(List<float[]> line, float x, float y)
        {
            float best = float.MaxValue, side = 1f, bx = 0, by = 0;
            for (int k = 0; k + 1 < line.Count; k++)
            {
                float ax = line[k][0], ay = line[k][1], dx = line[k + 1][0] - ax, dy = line[k + 1][1] - ay;
                float len2 = dx * dx + dy * dy;
                float t = len2 > 0 ? ((x - ax) * dx + (y - ay) * dy) / len2 : 0f;
                // The first and last segments carry on past their ends, so the step has no gap at them.
                float tc = k == 0 ? Math.Min(1f, t) : k == line.Count - 2 ? Math.Max(0f, t) : Math.Max(0f, Math.Min(1f, t));
                if (line.Count == 2) tc = t;
                float qx = ax + tc * dx, qy = ay + tc * dy;
                float dd = (float)Math.Sqrt((x - qx) * (x - qx) + (y - qy) * (y - qy));
                if (dd < best)
                {
                    best = dd;
                    side = dx * (y - ay) - dy * (x - ax) > 0 ? -1f : 1f; // right of the direction is positive
                    bx = qx; by = qy;
                }
            }
            return (best, side, bx, by);
        }

        private static float EllipseR(float[] e, float x, float y)
        {
            float dx = (x - e[0]) / e[2], dy = (y - e[1]) / e[3];
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float DistanceToRing(List<float[]> poly, float x, float y)
        {
            float best = float.MaxValue;
            for (int k = 0; k < poly.Count; k++)
            {
                float[] a = poly[k], b = poly[(k + 1) % poly.Count];
                best = Math.Min(best, Geometry.DistanceToSegment(a[0], a[1], b[0], b[1], x, y));
            }
            return best;
        }
    }
}
