using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A row of status icons: the buffs or the debuffs the player is under (bile's statuses, fever,
    /// wards from abilities). Each shows its strength or time left; hover for what it does. The
    /// server decides the statuses (<see cref="PlayerStateMessage.Statuses"/>); content describes them.
    /// </summary>
    public abstract class StatusBarPanel : UiPanel
    {
        private const float SlotSize = 34f;

        protected abstract bool Debuffs { get; }
        public override string Icon => "focus";
        public override bool Bare => true;
        public override Vector2? FixedSize => new Vector2(316f, 40f);

        private VisualElement _row;
        private readonly List<StatusSlot> _slots = new List<StatusSlot>();
        private readonly Dictionary<string, StatusDef> _defs = new Dictionary<string, StatusDef>();
        private ClientContent _defsFor;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            _row = UiKit.El(root, "row");
            _row.pickingMode = PickingMode.Ignore;
            _row.style.flexWrap = Wrap.Wrap;
        }

        public override void Refresh()
        {
            PlayerStateMessage state = Session.PlayerState;
            ClientContent content = Session.Content;
            if (content != _defsFor)
            {
                _defsFor = content;
                _defs.Clear();
                if (content != null)
                    foreach (StatusDef d in content.Statuses) _defs[d.Id] = d;
            }

            int shown = 0;
            if (state != null)
            {
                foreach (StatusState s in state.Statuses)
                {
                    StatusDef def = Def(s.Id);
                    if (def.Debuff != Debuffs) continue;
                    if (shown == _slots.Count) _slots.Add(new StatusSlot(_row, Debuffs));
                    _slots[shown++].Set(def, s, Remaining(s));
                }
            }
            for (int i = 0; i < _slots.Count; i++) _slots[i].Root.style.display = i < shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private StatusDef Def(string id) =>
            _defs.TryGetValue(id, out StatusDef def) ? def : new StatusDef { Id = id, Name = id, Debuff = false };

        /// <summary>Seconds left, or a negative number for a status that lasts as long as its cause.</summary>
        private float Remaining(StatusState s) =>
            s.EndTick == 0 ? -1f : Mathf.Max(0f, (s.EndTick - Session.EstimatedTick) * Session.TickSeconds);

        private static string Short(float seconds) =>
            seconds >= 60f ? Mathf.CeilToInt(seconds / 60f) + "m" : Mathf.CeilToInt(seconds) + "s";

        /// <summary>One icon in the bar, reused as statuses come and go.</summary>
        private sealed class StatusSlot
        {
            public readonly VisualElement Root;
            private readonly VisualElement _icon;
            private readonly Label _label;
            private StatusDef _def;
            private StatusState _state;
            private float _remaining;

            public StatusSlot(VisualElement parent, bool debuff)
            {
                Root = UiKit.El(parent, "slot", "status", debuff ? "status--debuff" : "status--buff");
                Root.style.width = SlotSize;
                Root.style.height = SlotSize;
                _icon = UiKit.El(Root, "slot__icon");
                _icon.style.width = SlotSize - 8f;
                _icon.style.height = SlotSize - 8f;
                _icon.pickingMode = PickingMode.Ignore;
                _label = UiKit.Text(Root, "", "status__value");
                _label.pickingMode = PickingMode.Ignore;
                UiKit.Tooltip(Root, Tooltip);
            }

            public void Set(StatusDef def, StatusState state, float remaining)
            {
                if (_def != def) UiKit.SetIcon(_icon, IconLibrary.Get(def.Icon), string.IsNullOrEmpty(def.Color) ? Color.white : WorldView.ParseColor(def.Color));
                _def = def;
                _state = state;
                _remaining = remaining;
                _label.text = remaining >= 0f ? Short(remaining) : state.Magnitude > 0 ? state.Magnitude + "%" : "";
            }

            private string Tooltip()
            {
                if (_def == null) return "";
                string text = UiKit.Title(_def.Name) + "  " + UiKit.Colour(_def.Debuff ? "Debuff" : "Buff", _def.Debuff ? UiKit.Bad : UiKit.Good);
                if (!string.IsNullOrEmpty(_def.Description)) text += "\n" + _def.Description.Replace("{value}", _state.Magnitude.ToString());
                if (_remaining >= 0f) text += "\n" + UiKit.Colour(Mathf.CeilToInt(_remaining) + " seconds left.", UiKit.Muted);
                return text;
            }
        }
    }

    public sealed class BuffBarPanel : StatusBarPanel
    {
        protected override bool Debuffs => false;
        public override string Id => "buffs";
        public override string Title => "Buffs";
        public override string DefaultWindow => UiDefaultLayout.Buffs;
    }

    public sealed class DebuffBarPanel : StatusBarPanel
    {
        protected override bool Debuffs => true;
        public override string Id => "debuffs";
        public override string Title => "Debuffs";
        public override string DefaultWindow => UiDefaultLayout.Debuffs;
    }
}
