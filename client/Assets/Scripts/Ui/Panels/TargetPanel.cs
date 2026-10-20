using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>What you are fighting: its name and health. Only shown while there is a target.</summary>
    public sealed class TargetPanel : UiPanel
    {
        public override string Id => "target";
        public override string Title => "Target";
        public override string Icon => "blood";
        public override string DefaultWindow => UiDefaultLayout.Target;
        public override bool Bare => true;
        public override Vector2 MinSize => new Vector2(180f, 44f);

        private VisualElement _box;
        private Label _name;
        private UiKit.Bar _health;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            _box = UiKit.El(root, "actionbar");
            _box.style.borderBottomLeftRadius = _box.style.borderBottomRightRadius = 3f;
            _name = UiKit.Text(_box, "", "heading");
            _name.style.unityTextAlign = TextAnchor.MiddleCenter;
            _name.style.marginTop = 0f;
            _health = new UiKit.Bar(_box, new Color(0.12f, 0.68f, 0.12f), IconLibrary.Get("blood"), 14f);
        }

        public override void Refresh()
        {
            int id = Session.PlayerState?.TargetId ?? 0;
            EntityView target = id != 0 ? World.Find(id) : null;
            _box.style.display = target != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (target == null) return;
            _name.text = target.DisplayName;
            _health.Set(target.HealthPercent / 100f, target.HealthPercent + "%");
            _health.Color = Color.Lerp(new Color(0.75f, 0.12f, 0.08f), new Color(0.12f, 0.68f, 0.12f), target.HealthPercent / 100f);
        }
    }
}
