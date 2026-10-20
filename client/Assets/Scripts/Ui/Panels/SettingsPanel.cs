using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>Settings, in sections: display, interface (lock, scale, reset), and account.</summary>
    public sealed class SettingsPanel : UiPanel
    {
        public override string Id => "settings";
        public override string Title => "Settings";
        public override string Icon => "settings";
        public override bool OpenByDefault => false;
        public override Vector2 DefaultSize => new Vector2(270f, 340f);

        private UiKit.Check _roofs;
        private UiKit.Check _lock;
        private Label _scale;
        private Label _where;

        protected override void Build(VisualElement root)
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            root.Add(scroll);

            Section(scroll, "Display");
            VisualElement display = UiKit.El(scroll, "well");
            _roofs = new UiKit.Check(display, "Show roofs", () => RoofHider.ShowRoofs = !RoofHider.ShowRoofs);
            UiKit.Text(display, "Roofs always hide while you're indoors.", "muted", "small").style.whiteSpace = WhiteSpace.Normal;

            Section(scroll, "Interface");
            VisualElement ui = UiKit.El(scroll, "well");
            _lock = new UiKit.Check(ui, "Lock windows in place", () => Ui.Locked = !Ui.Locked);
            VisualElement scaleRow = UiKit.El(ui, "row");
            UiKit.Text(scaleRow, "Scale");
            UiKit.El(scaleRow, "spacer");
            UiKit.Button(scaleRow, "-", () => Ui.Scale -= 0.125f);
            _scale = UiKit.Text(scaleRow, "", "value");
            _scale.style.width = 46f;
            _scale.style.unityTextAlign = TextAnchor.MiddleCenter;
            UiKit.Button(scaleRow, "+", () => Ui.Scale += 0.125f);
            UiKit.Button(ui, "Reset layout", () => Ui.ResetLayout());
            Label help = UiKit.Text(ui, "Drag a title bar to move a window and its corner to resize it. Drag a tab out to give it a window of its own, or onto another window to combine them.", "muted", "small");
            help.style.whiteSpace = WhiteSpace.Normal;

            Section(scroll, "Account");
            VisualElement account = UiKit.El(scroll, "well");
            _where = UiKit.Text(account, "", "small");
            UiKit.Button(account, "Log out", () => Session.Logout());
        }

        private static void Section(VisualElement parent, string title) => UiKit.Text(parent, title, "heading");

        public override void Refresh()
        {
            _roofs.Set(RoofHider.ShowRoofs);
            _lock.Set(Ui.Locked);
            _scale.text = Mathf.RoundToInt(Ui.Scale * 100f) + "%";
            EntityView local = World.Local;
            _where.text = "Logged in as " + UiKit.Colour(Session.PlayerName, UiKit.Value) + (local != null ? UiKit.Colour("  at " + local.Tile, UiKit.Muted) : "");
        }
    }
}
