using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>What you are fighting: its name and health. Shown only while there is a target.</summary>
    public sealed class TargetPanel : UiPanel
    {
        public override string Id => "target";
        public override string Title => "Target";
        public override string DefaultWindow => UiDefaultLayout.Target;
        public override bool Bare => true;
        public override Vector2 MinSize => new Vector2(160f, 40f);

        private VisualElement _box;
        private Label _name;
        private UiKit.Bar _health;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            _box = UiKit.El(root);
            _box.style.backgroundColor = new Color(0.24f, 0.21f, 0.16f, 0.92f);
            _box.style.paddingLeft = _box.style.paddingRight = _box.style.paddingTop = 4f;
            _box.style.borderTopLeftRadius = _box.style.borderTopRightRadius = _box.style.borderBottomLeftRadius = _box.style.borderBottomRightRadius = 4f;
            _name = UiKit.Text(_box, "", "heading");
            _name.style.unityTextAlign = TextAnchor.MiddleCenter;
            _name.style.marginBottom = 2f;
            _health = new UiKit.Bar(_box, new Color(0.15f, 0.7f, 0.15f));
        }

        public override void Refresh()
        {
            int id = Session.PlayerState?.TargetId ?? 0;
            EntityView target = id != 0 ? World.Find(id) : null;
            _box.style.display = target != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (target == null) return;
            _name.text = target.DisplayName;
            _health.Set(target.HealthPercent / 100f, target.HealthPercent + "%");
        }
    }
}
