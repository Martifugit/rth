using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>Settings: roofs, the interface (lock, scale, reset), and logging out.</summary>
    public sealed class SettingsPanel : UiPanel
    {
        public override string Id => "settings";
        public override string Title => "Settings";
        public override bool OpenByDefault => false;
        public override Vector2 DefaultSize => new Vector2(260f, 300f);

        private Label _roofs;
        private Label _lock;
        private Label _scale;
        private Label _where;

        protected override void Build(VisualElement root)
        {
            UiKit.Text(root, "Display", "heading");
            _roofs = UiKit.Button(root, "", () => RoofHider.ShowRoofs = !RoofHider.ShowRoofs);

            UiKit.Text(root, "Interface", "heading").style.marginTop = 8f;
            _lock = UiKit.Button(root, "", () => Ui.Locked = !Ui.Locked);
            VisualElement scaleRow = UiKit.El(root, "row");
            UiKit.Button(scaleRow, "-", () => Ui.Scale -= 0.125f);
            _scale = UiKit.Text(scaleRow, "");
            _scale.style.flexGrow = 1;
            _scale.style.unityTextAlign = TextAnchor.MiddleCenter;
            UiKit.Button(scaleRow, "+", () => Ui.Scale += 0.125f);
            UiKit.Button(root, "Reset layout", () => Ui.ResetLayout());
            Label help = UiKit.Text(root, "Drag a title bar to move a window, its corner to resize it, a tab to pull it out or onto another window.", "muted");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.fontSize = 10;

            UiKit.Text(root, "Account", "heading").style.marginTop = 8f;
            _where = UiKit.Text(root, "", "muted");
            UiKit.Button(root, "Log out", () => Session.Logout());
        }

        public override void Refresh()
        {
            _roofs.text = RoofHider.ShowRoofs ? "Roofs: shown" : "Roofs: hidden";
            _lock.text = Ui.Locked ? "Interface: locked" : "Interface: unlocked";
            _scale.text = "Scale " + Mathf.RoundToInt(Ui.Scale * 100f) + "%";
            EntityView local = World.Local;
            _where.text = Session.PlayerName + (local != null ? " at " + local.Tile : "");
        }
    }
}
