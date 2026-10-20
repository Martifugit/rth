using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>The Assayer's words when you die, until you return. Opened by the death message.</summary>
    public sealed class AssayerPanel : UiPanel
    {
        public override string Id => "assayer";
        public override string Title => "The Assayer";
        public override string Icon => "balance";
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(480f, 240f);
        public override bool? ServerOpen => !string.IsNullOrEmpty(Session.AssayerText);
        public override void OnClosedByPlayer() => Session.AssayerText = null;

        private Label _text;

        protected override void Build(VisualElement root)
        {
            root.style.alignItems = Align.Center;
            UiKit.Icon(root, IconLibrary.Get("balance"), 44f).style.unityBackgroundImageTintColor = new Color(0.78f, 0.66f, 0.95f);
            _text = UiKit.Text(root, "", "prose");
            _text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _text.style.unityFontStyleAndWeight = FontStyle.Italic;
            _text.style.color = new Color(0.86f, 0.82f, 0.95f);
            _text.style.flexGrow = 1;
            _text.style.marginTop = 6f;
            UiKit.Button(root, "Return", () => Ui.Close(this));
        }

        public override void Refresh() => _text.text = Session.AssayerText ?? "";
    }
}
