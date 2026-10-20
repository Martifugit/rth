using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>What you are fighting: its name and health. Only shown while there is a target.</summary>
    public sealed class TargetPanel : UiPanel
    {
        public override string Id => "target";
        public override string Title => "Target";
        public override string Icon => "bestiary";
        public override string DefaultWindow => UiDefaultLayout.Target;
        public override bool Bare => true;
        public override Vector2? FixedSize => new Vector2(260f, 64f);

        private VisualElement _box;
        private Label _name;
        private UiKit.Bar _health;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            _box = UiKit.El(root, "hud");
            _box.style.flexGrow = 1;
            _name = UiKit.Text(_box, "", "heading");
            _name.style.unityTextAlign = TextAnchor.MiddleCenter;
            _name.style.marginTop = 0f;
            _health = new UiKit.Bar(_box, UiKit.BloodColour, 16f);
            _health.Root.style.flexGrow = 0;
        }

        public override void Refresh()
        {
            int id = Session.PlayerState?.TargetId ?? 0;
            EntityView target = id != 0 ? World.Find(id) : null;
            _box.style.display = target != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (target == null) return;
            _name.text = target.DisplayName.ToUpperInvariant();
            _health.Set(target.HealthPercent / 100f, target.HealthPercent + "%");
            _health.Color = Color.Lerp(new Color(0.7f, 0.12f, 0.08f), new Color(0.2f, 0.62f, 0.18f), target.HealthPercent / 100f);
        }
    }
}
