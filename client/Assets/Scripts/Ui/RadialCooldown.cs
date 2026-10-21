using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// A radial cooldown sweep over a slot: a faint white wedge over the part of the cooldown
    /// still to run, clipped to the slot, clearing clockwise from twelve o'clock until the slot is ready.
    /// </summary>
    public sealed class RadialCooldown : VisualElement
    {
        private static readonly Color Fill = new Color(1f, 1f, 1f, 0.022f); // blended in linear space, so this reads as a light veil

        private float _remaining;

        public RadialCooldown()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0f;
            generateVisualContent += Draw;
        }

        /// <summary>The fraction of the cooldown still to run, 0 (ready) to 1 (just started).</summary>
        public float Remaining
        {
            get => _remaining;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Abs(v - _remaining) < 0.002f) return;
                _remaining = v;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_remaining <= 0f) return;
            Rect rect = contentRect;
            if (rect.width < 1f || rect.height < 1f) return;
            Vector2 centre = rect.center;
            // The wedge still to run, clockwise from where the sweep has reached round to twelve
            // o'clock (angles in degrees, y down, -90 is up), traced along the slot's own edges.
            float end = 270f;
            float start = end - 360f * _remaining;
            var angles = new List<float> { start };
            float corner = Mathf.Atan2(rect.height, rect.width) * Mathf.Rad2Deg;
            foreach (float c in new[] { -corner, corner, 180f - corner, 180f + corner }) // the corners, in order
                if (c > start && c < end) angles.Add(c);
            angles.Add(end);

            Painter2D painter = context.painter2D;
            painter.fillColor = Fill;
            painter.BeginPath();
            painter.MoveTo(centre);
            foreach (float angle in angles) painter.LineTo(OnEdge(rect, angle));
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>Where a ray from the centre at an angle meets the rectangle's edge.</summary>
        private static Vector2 OnEdge(Rect rect, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float tx = Mathf.Abs(dir.x) > 1e-5f ? rect.width / 2f / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-5f ? rect.height / 2f / Mathf.Abs(dir.y) : float.MaxValue;
            return rect.center + dir * Mathf.Min(tx, ty);
        }
    }
}
