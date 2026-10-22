using System;
using System.Collections.Generic;

namespace Rth.Client.Ui.Modals
{
    /// <summary>
    /// Where the world map looks: its zoom (interface pixels a tile, in a few fixed steps) and pan
    /// (where the map's north-west corner sits in the viewport). Plain maths with no Unity, tested
    /// in EditMode (and by `dotnet test` through tools/client-tests).
    /// Map pixels run east and south from the chart's north-west corner.
    /// </summary>
    public sealed class WorldMapView
    {
        /// <summary>The fixed zoom steps above "the whole map", in pixels a tile.</summary>
        public static readonly float[] Steps = { 0.75f, 1f, 1.5f, 2f, 3f };

        /// <summary>The scale opened at: a tile a pixel, a wide look around you.</summary>
        public const float OpeningScale = 1f;

        public float MapWidth { get; }
        public float MapHeight { get; }
        public float ViewWidth { get; private set; }
        public float ViewHeight { get; private set; }
        public float Scale { get; private set; } = OpeningScale;
        public float PanX { get; private set; }
        public float PanY { get; private set; }

        public WorldMapView(float mapWidthTiles, float mapHeightTiles)
        {
            MapWidth = Math.Max(1f, mapWidthTiles);
            MapHeight = Math.Max(1f, mapHeightTiles);
        }

        /// <summary>The scale that shows the whole map, a little margin round it.</summary>
        public float FitScale => ViewWidth <= 0 || ViewHeight <= 0 ? Steps[0] : Math.Min(ViewWidth / MapWidth, ViewHeight / MapHeight) * 0.96f;

        /// <summary>Every zoom step: the whole map, then the fixed steps above it.</summary>
        public IReadOnlyList<float> Levels
        {
            get
            {
                float fit = FitScale;
                var levels = new List<float> { fit };
                foreach (float s in Steps)
                    if (s > fit * 1.08f) levels.Add(s);
                return levels;
            }
        }

        public int Level
        {
            get
            {
                IReadOnlyList<float> levels = Levels;
                int best = 0;
                for (int i = 1; i < levels.Count; i++)
                    if (Math.Abs(levels[i] - Scale) < Math.Abs(levels[best] - Scale)) best = i;
                return best;
            }
        }

        public bool CanZoomIn => Level < Levels.Count - 1;
        public bool CanZoomOut => Level > 0;

        /// <summary>The viewport's size changed (or became known); keeps the map in view.</summary>
        public void Resize(float width, float height)
        {
            ViewWidth = Math.Max(0f, width);
            ViewHeight = Math.Max(0f, height);
            if (Scale < FitScale) Scale = FitScale;
            Clamp();
        }

        /// <summary>One step in (+1) or out (-1), keeping the map point under the pivot (viewport pixels) where it is.</summary>
        public bool Zoom(int steps, float pivotX, float pivotY)
        {
            IReadOnlyList<float> levels = Levels;
            int to = Math.Max(0, Math.Min(levels.Count - 1, Level + steps));
            return SetScale(levels[to], pivotX, pivotY);
        }

        /// <summary>Zoom to a scale, keeping the map point under the pivot (viewport pixels) where it is.</summary>
        public bool SetScale(float scale, float pivotX, float pivotY)
        {
            scale = Math.Max(FitScale, scale);
            if (Math.Abs(scale - Scale) < 1e-4f) return false;
            float mx = (pivotX - PanX) / Scale, my = (pivotY - PanY) / Scale;
            Scale = scale;
            PanX = pivotX - mx * Scale;
            PanY = pivotY - my * Scale;
            Clamp();
            return true;
        }

        public void PanBy(float dx, float dy)
        {
            PanX += dx;
            PanY += dy;
            Clamp();
        }

        /// <summary>Puts a map point (tiles from the north-west corner) in the middle of the viewport.</summary>
        public void CentreOn(float mapX, float mapY)
        {
            PanX = ViewWidth / 2f - mapX * Scale;
            PanY = ViewHeight / 2f - mapY * Scale;
            Clamp();
        }

        /// <summary>A map point (tiles from the north-west corner) in viewport pixels.</summary>
        public (float x, float y) ToView(float mapX, float mapY) => (PanX + mapX * Scale, PanY + mapY * Scale);

        /// <summary>A viewport pixel as a map point (tiles from the north-west corner).</summary>
        public (float x, float y) ToMap(float viewX, float viewY) => ((viewX - PanX) / Scale, (viewY - PanY) / Scale);

        /// <summary>
        /// Keeps the map on screen: a map smaller than the viewport sits in its middle; a larger
        /// one can't be dragged so far that its edge comes in past the viewport's.
        /// </summary>
        private void Clamp()
        {
            PanX = ClampAxis(PanX, MapWidth * Scale, ViewWidth);
            PanY = ClampAxis(PanY, MapHeight * Scale, ViewHeight);
        }

        private static float ClampAxis(float pan, float content, float view)
        {
            if (content <= view) return (view - content) / 2f;
            return Math.Max(view - content, Math.Min(0f, pan));
        }
    }
}
