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
        public override Vector2 DefaultSize => new Vector2(460f, 220f);
        public override Vector2 MinSize => new Vector2(320f, 180f);
        public override bool? ServerOpen => !string.IsNullOrEmpty(Session.AssayerText);
        public override void OnClosedByPlayer() => Session.AssayerText = null;

        private Label _text;

        protected override void Build(VisualElement root)
        {
            root.style.backgroundColor = new Color(0.1f, 0.07f, 0.14f);
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = 10f;
            root.style.alignItems = Align.Center;
            UiKit.Icon(root, IconLibrary.Get("balance"), 48f);
            _text = UiKit.Text(root, "");
            _text.style.whiteSpace = WhiteSpace.Normal;
            _text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _text.style.unityFontStyleAndWeight = FontStyle.Italic;
            _text.style.color = new Color(0.88f, 0.84f, 0.96f);
            _text.style.flexGrow = 1;
            _text.style.marginTop = 6f;
            UiKit.Button(root, "Return", () => Ui.Close(this));
        }

        public override void Refresh() => _text.text = Session.AssayerText ?? "";
    }
}
