using System.Collections.Generic;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The world map: every region the server knows, drawn to scale with its title, and where
    /// you are. A first version; painted region maps and waystone travel come later.
    /// </summary>
    public sealed class WorldMapPanel : UiPanel
    {
        public override string Id => "worldmap";
        public override string Title => "World map";
        public override bool OpenByDefault => false;
        public override Key Hotkey => Key.M;
        public override Vector2 DefaultSize => new Vector2(560f, 420f);
        public override Vector2 MinSize => new Vector2(260f, 200f);

        private VisualElement _canvas;
        private VisualElement _you;
        private readonly List<(WorldRegion Region, VisualElement Box)> _boxes = new List<(WorldRegion, VisualElement)>();
        private int _builtFor = -1;

        protected override void Build(VisualElement root)
        {
            _canvas = UiKit.El(root);
            _canvas.style.flexGrow = 1;
            _canvas.style.backgroundColor = new Color(0.16f, 0.24f, 0.32f); // the unknown is sea-coloured
            _canvas.style.overflow = Overflow.Hidden;
            _you = UiKit.El(_canvas, "minimap__dot");
            _you.style.width = _you.style.height = 8f;
            _you.style.backgroundColor = Color.white;
            UiKit.Text(root, "Unbuilt land is not shown. Waystone travel will be here.", "muted").style.fontSize = 10;
        }

        public override void Refresh()
        {
            IReadOnlyList<WorldRegion> regions = Ui.regions != null ? Ui.regions.Regions : null;
            if (regions == null || regions.Count == 0) return;
            if (regions.Count != _builtFor) Rebuild(regions);

            // Fit every region in the canvas, north up.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (WorldRegion r in regions)
            {
                minX = Mathf.Min(minX, r.X);
                minY = Mathf.Min(minY, r.Y);
                maxX = Mathf.Max(maxX, r.X + r.Width);
                maxY = Mathf.Max(maxY, r.Y + r.Height);
            }
            Vector2 size = _canvas.layout.size;
            if (size.x < 1f) return;
            float margin = 16f;
            float scale = Mathf.Min((size.x - 2 * margin) / Mathf.Max(1f, maxX - minX), (size.y - 2 * margin) / Mathf.Max(1f, maxY - minY));
            Vector2 origin = new Vector2((size.x - (maxX - minX) * scale) / 2f, (size.y - (maxY - minY) * scale) / 2f);
            Vector2 ToCanvas(float x, float y) => new Vector2(origin.x + (x - minX) * scale, origin.y + (maxY - y) * scale);

            foreach ((WorldRegion r, VisualElement box) in _boxes)
            {
                Vector2 topLeft = ToCanvas(r.X, r.Y + r.Height);
                box.style.left = topLeft.x;
                box.style.top = topLeft.y;
                box.style.width = r.Width * scale;
                box.style.height = r.Height * scale;
                box.EnableInClassList("drop-target", Ui.regions.Current != null && Ui.regions.Current.Name == r.Name);
            }
            EntityView local = World.Local;
            _you.style.display = local != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (local != null)
            {
                Vector2 p = ToCanvas(local.transform.position.x, local.transform.position.z);
                _you.style.left = p.x - 4f;
                _you.style.top = p.y - 4f;
                _you.BringToFront();
            }
        }

        private void Rebuild(IReadOnlyList<WorldRegion> regions)
        {
            _builtFor = regions.Count;
            foreach ((_, VisualElement box) in _boxes) box.RemoveFromHierarchy();
            _boxes.Clear();
            foreach (WorldRegion r in regions)
            {
                VisualElement box = UiKit.El(_canvas);
                box.style.position = Position.Absolute;
                box.style.backgroundColor = new Color(0.33f, 0.5f, 0.21f);
                box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 1f;
                box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = new Color(0.2f, 0.3f, 0.12f);
                Label title = UiKit.Text(box, r.Title, "heading");
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.marginTop = 4f;
                title.pickingMode = PickingMode.Ignore;
                _canvas.Insert(0, box);
                _boxes.Add((r, box));
            }
        }
    }
}
