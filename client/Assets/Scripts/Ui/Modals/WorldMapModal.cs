using System.Collections.Generic;
using System.Linq;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>The world map (M). Waystone travel will become a second tab.</summary>
    public sealed class WorldMapModal : UiModal
    {
        public override string Id => "worldmap";
        public override string Title => "World map";
        public override string Icon => "map";
        public override Key Hotkey => Key.M;

        protected override IEnumerable<ModalTab> CreateTabs() => new ModalTab[] { new WorldMapTab() };
    }

    /// <summary>
    /// A chart of the world: every region the server knows, to scale over open sea, labelled, the
    /// one you're in lit, and a marker where you stand.
    /// </summary>
    public sealed class WorldMapTab : ModalTab
    {
        public override string Id => "map";
        public override string Title => "The Basin";
        public override string Icon => "map";

        private static readonly Color Sea = new Color(0.16f, 0.24f, 0.3f);
        private static readonly Color Land = new Color(0.42f, 0.48f, 0.3f);
        private static readonly Color LandHere = new Color(0.54f, 0.6f, 0.36f);
        private static readonly Color Ink = new Color(0.18f, 0.14f, 0.08f);

        private VisualElement _canvas;
        private VisualElement _you;
        private Label _where;
        private readonly List<(WorldRegion Region, VisualElement Box)> _boxes = new List<(WorldRegion, VisualElement)>();
        private int _builtFor = -1;

        protected override void Build(VisualElement root)
        {
            _canvas = UiKit.El(root, "well");
            _canvas.style.flexGrow = 1;
            _canvas.style.backgroundColor = Sea;
            _canvas.style.overflow = Overflow.Hidden;
            _you = UiKit.Icon(_canvas, IconLibrary.Get("blood"), 22f);
            _you.style.position = Position.Absolute;
            _you.style.unityBackgroundImageTintColor = new Color(1f, 0.3f, 0.2f);

            VisualElement legend = UiKit.El(root, "row");
            legend.style.marginTop = 6f;
            _where = UiKit.Text(legend, "");
            UiKit.El(legend, "spacer");
            UiKit.Text(legend, "Unbuilt land shows as sea. Waystone travel will be added here.", "muted", "small");
        }

        public override void Refresh()
        {
            IReadOnlyList<WorldRegion> regions = Ui.regions?.Regions?.Where(r => !r.Instanced).ToList(); // arenas live far off: not on the map
            if (regions == null || regions.Count == 0) return;
            if (regions.Count != _builtFor) Rebuild(regions);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (WorldRegion r in regions)
            {
                minX = Mathf.Min(minX, r.X);
                minY = Mathf.Min(minY, r.Y);
                maxX = Mathf.Max(maxX, r.X + r.Width);
                maxY = Mathf.Max(maxY, r.Y + r.Height);
            }
            Vector2 size = _canvas.contentRect.size;
            if (size.x < 1f) return;
            const float margin = 40f;
            float scale = Mathf.Min((size.x - 2 * margin) / Mathf.Max(1f, maxX - minX), (size.y - 2 * margin) / Mathf.Max(1f, maxY - minY));
            Vector2 origin = new Vector2((size.x - (maxX - minX) * scale) / 2f, (size.y - (maxY - minY) * scale) / 2f);
            Vector2 ToCanvas(float x, float y) => new Vector2(origin.x + (x - minX) * scale, origin.y + (maxY - y) * scale);

            WorldRegion current = Ui.regions.Current;
            foreach ((WorldRegion r, VisualElement box) in _boxes)
            {
                Vector2 topLeft = ToCanvas(r.X, r.Y + r.Height);
                box.style.left = topLeft.x;
                box.style.top = topLeft.y;
                box.style.width = r.Width * scale;
                box.style.height = r.Height * scale;
                box.style.backgroundColor = current != null && current.Name == r.Name ? LandHere : Land;
            }
            EntityView local = Ui.world.Local;
            _you.style.display = local != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (local != null)
            {
                Vector2 p = ToCanvas(local.transform.position.x, local.transform.position.z);
                _you.style.left = p.x - 11f;
                _you.style.top = p.y - 20f;
                _you.BringToFront();
            }
            _where.text = current != null ? "You are in " + UiKit.Colour(current.Title, UiKit.Value) : "";
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
                box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 2f;
                box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = Ink;
                box.style.alignItems = Align.Center;
                box.style.justifyContent = Justify.Center;
                Label title = UiKit.Text(box, r.Title.ToUpperInvariant(), "title-large");
                title.style.color = Ink;
                title.style.textShadow = new TextShadow { color = new Color(1f, 1f, 1f, 0.25f), offset = new Vector2(0f, 1f) };
                title.pickingMode = PickingMode.Ignore;
                _canvas.Insert(0, box);
                _boxes.Add((r, box));
            }
        }
    }
}
