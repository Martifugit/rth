using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// A stand-in shown only while editing the layout, for something that isn't always on screen:
    /// a window the server opens (bank, shop, make, conversation, rig, boss timer) or a placeable
    /// overlay (the trophy fishing gauge). It has the real thing's size, frame and icon, so the
    /// player can put it where they want it; when editing ends its place is kept under the real
    /// one's id and the stand-in goes. It has no parameterless constructor, so the panel registry
    /// never creates one by itself.
    /// </summary>
    public sealed class PlaceholderPanel : UiPanel
    {
        /// <summary>The id whose place this stands for: a panel's id, or "overlay:" and an overlay's id.</summary>
        public readonly string ForId;

        private readonly string _title;
        private readonly string _icon;
        private readonly bool _bare;
        private readonly bool _floating;
        private readonly Vector2? _fixed;
        private readonly Vector2 _min;
        private readonly Vector2 _default;
        private readonly Vector2[] _allowed;

        /// <summary>A stand-in for a panel the server opens.</summary>
        public PlaceholderPanel(UiPanel real)
        {
            ForId = real.Id;
            _title = real.Title;
            _icon = real.Icon;
            _bare = real.Bare;
            _floating = real.Floating;
            _fixed = real.FixedSize;
            _min = real.MinSize;
            _default = real.DefaultSize;
            _allowed = real.AllowedSizes;
        }

        /// <summary>A stand-in for an overlay: frameless, of a fixed size, floating over the HUD.</summary>
        public PlaceholderPanel(string overlayKey, string title, Vector2 size)
        {
            ForId = overlayKey;
            _title = title;
            _icon = "menu";
            _bare = true;
            _floating = true;
            _fixed = size;
            _min = size;
            _default = size;
        }

        public override string Id => "placeholder:" + ForId;
        public override string Title => _title;
        public override string Icon => _icon;
        public override bool OpenByDefault => false;
        public override bool Bare => _bare;
        public override bool Floating => _floating;
        public override Vector2? FixedSize => _fixed;
        public override Vector2 MinSize => _min;
        public override Vector2 DefaultSize => _default;
        public override Vector2[] AllowedSizes => _allowed;
        public override bool Scrolls => false;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("placeholder");
            Label text = UiKit.Text(root, _title + "\nshows here when it opens", "placeholder__text");
            text.pickingMode = PickingMode.Ignore;
        }
    }
}
