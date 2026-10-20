using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The main action bar: the combat resources (blood, phlegm, breath, the bile axis and what
    /// the current bile does) over the ability slots, keys 1-9. Further bars will be more
    /// instances of the slot row once bars can be arranged by the player.
    /// </summary>
    public sealed class ActionBarPanel : UiPanel
    {
        public override string Id => "actionbar";
        public override string Title => "Action bar";
        public override string DefaultWindow => UiDefaultLayout.ActionBar;
        public override bool Bare => true;
        public override Vector2 MinSize => new Vector2(300f, 140f);

        private UiKit.Bar _blood;
        private UiKit.Bar _phlegm;
        private UiKit.Bar _breath;
        private BileAxis _bile;
        private Label _summary;
        private VisualElement _slotRow;
        private readonly List<Slot> _slots = new List<Slot>();
        private string _barSignature = "";

        private sealed class Slot
        {
            public string AbilityId;
            public VisualElement Root;
            public VisualElement Cooldown;
            public Label Timer;
        }

        protected override void Build(VisualElement root)
        {
            root.style.backgroundColor = new Color(0.24f, 0.21f, 0.16f, 0.92f);
            root.style.borderTopLeftRadius = root.style.borderTopRightRadius = 4f;
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = 4f;

            _blood = new UiKit.Bar(root, new Color(0.72f, 0.1f, 0.1f));
            _phlegm = new UiKit.Bar(root, new Color(0.5f, 0.68f, 0.5f));
            _breath = new UiKit.Bar(root, new Color(0.5f, 0.72f, 0.95f));
            _bile = new BileAxis(root);
            _summary = UiKit.Text(root, "", "muted");
            _summary.style.fontSize = 10;
            _summary.style.unityTextAlign = TextAnchor.MiddleCenter;
            _slotRow = UiKit.El(root, "row");
            _slotRow.style.justifyContent = Justify.Center;
            UiKit.Tooltip(_bile.Root, () => "Bile: black (melancholic) to the left, yellow (choleric) to the right.\nWhite marks: passives. Red marks: fever and torpor.");
            UiKit.Tooltip(_breath.Root, () => "Breath: spent by focuses such as deflection (see the Focus tab).");
            UiKit.Tooltip(_phlegm.Root, () => "Phlegm: built by basic abilities, spent by thresholds and ultimates.");
        }

        public override void Refresh()
        {
            PlayerStateMessage state = Session.PlayerState;
            ClientContent content = Session.Content;
            if (state == null || content == null) return;

            _blood.Set(state.Blood / (float)Mathf.Max(1, state.MaxBlood), "Blood " + state.Blood + " / " + state.MaxBlood);
            _phlegm.Set(state.Phlegm / (float)Mathf.Max(1, content.MaxPhlegm), "Phlegm " + state.Phlegm + "%");
            string deflect = state.Deflect == CombatStyle.None ? "" : "  - deflecting " + state.Deflect.ToString().ToLowerInvariant();
            _breath.Set(state.Breath / (float)Mathf.Max(1, state.MaxBreath), "Breath " + state.Breath + " / " + state.MaxBreath + deflect);
            _bile.Set(state.Bile, content);
            _summary.text = BileSummary(state, content, Session.TickSeconds);

            string signature = string.Join(",", content.ActionBar);
            if (signature != _barSignature) RebuildSlots(content);
            UpdateSlots(state);
        }

        private void RebuildSlots(ClientContent content)
        {
            _barSignature = string.Join(",", content.ActionBar);
            _slotRow.Clear();
            _slots.Clear();
            for (int i = 0; i < content.ActionBar.Count; i++)
            {
                AbilityDef ability = Session.Ability(content.ActionBar[i]);
                if (ability == null) continue;
                var slot = new Slot { AbilityId = ability.Id, Root = UiKit.El(_slotRow, "ability") };
                slot.Root.style.backgroundColor = GameText.AbilityColor(ability);
                UiKit.Text(slot.Root, (i + 1).ToString(), "ability__key");
                UiKit.Text(slot.Root, ability.Type == AbilityType.Threshold ? "T" : ability.Type == AbilityType.Ultimate ? "U" : "", "ability__kind");
                UiKit.Text(slot.Root, ability.Name, "ability__name");
                slot.Cooldown = UiKit.El(slot.Root, "ability__cooldown");
                slot.Timer = UiKit.Text(slot.Root, "", "ability__timer");
                string id = ability.Id;
                slot.Root.RegisterCallback<ClickEvent>(_ => Session.UseAbility(id));
                UiKit.Tooltip(slot.Root, () =>
                {
                    AbilityDef a = Session.Ability(id);
                    if (a == null) return null;
                    string problem = Session.AbilityProblem(a);
                    return GameText.Describe(a, Session.TickSeconds) + (problem != null ? "\n\n" + problem : "");
                });
                _slots.Add(slot);
            }
        }

        private void UpdateSlots(PlayerStateMessage state)
        {
            float now = Session.EstimatedTick;
            foreach (Slot slot in _slots)
            {
                AbilityDef ability = Session.Ability(slot.AbilityId);
                if (ability == null) continue;
                bool usable = Session.AbilityProblem(ability) == null;
                slot.Root.style.backgroundColor = usable ? GameText.AbilityColor(ability) : Color.Lerp(GameText.AbilityColor(ability), Color.black, 0.6f);
                slot.Root.EnableInClassList("ability--queued", state.QueuedAbility == ability.Id);

                // Cooldown: whichever is later, the ability's own or the global one.
                float ready = state.GlobalCooldownReadyTick;
                foreach (CooldownState cd in state.Cooldowns)
                    if (cd.AbilityId == ability.Id) ready = Mathf.Max(ready, cd.ReadyTick);
                float remaining = ready - now;
                float total = Mathf.Max(ability.CooldownTicks, 3);
                slot.Cooldown.style.height = Length.Percent(remaining > 0f ? Mathf.Clamp01(remaining / total) * 100f : 0f);
                slot.Timer.text = remaining * Session.TickSeconds > 0.95f ? Mathf.CeilToInt(remaining * Session.TickSeconds) + "s" : "";
            }
        }

        private static string BileSummary(PlayerStateMessage state, ClientContent content, float tickSeconds)
        {
            var rules = new CombatRules
            {
                BileLimit = content.BileLimit,
                BilePassiveThreshold = content.BilePassiveThreshold,
                BileExtreme = content.BileExtreme,
            };
            var parts = new List<string>();
            if (rules.IsCholeric(state.Bile)) parts.Add("Choleric: +" + rules.CholericBonusPercent(state.Bile) + "% damage");
            else if (rules.IsMelancholic(state.Bile)) parts.Add("Melancholic: " + rules.MelancholicWardPercent(state.Bile) + "% less damage taken");
            else parts.Add("Balanced");
            if (rules.IsFevered(state.Bile)) parts.Add("FEVER: your blood is burning");
            if (rules.IsTorpid(state.Bile)) parts.Add("TORPOR: phlegm builds slowly");
            foreach (EffectState e in state.Effects)
            {
                float secs = Mathf.Max(0f, (e.EndTick - (float)state.Tick) * tickSeconds);
                parts.Add(e.Effect + " " + e.Magnitude + "% (" + secs.ToString("0") + "s)");
            }
            if (state.InCombat) parts.Add("in combat");
            return string.Join("   ", parts);
        }

        /// <summary>The bile axis: centred, black bile to the left, yellow to the right, marks at the thresholds.</summary>
        private sealed class BileAxis
        {
            public readonly VisualElement Root;
            private readonly VisualElement _fill;
            private readonly Label _label;
            private readonly List<VisualElement> _marks = new List<VisualElement>();
            private int _marksFor = int.MinValue;

            public BileAxis(VisualElement parent)
            {
                Root = UiKit.El(parent, "bar");
                _fill = UiKit.El(Root, "bar__fill");
                var centre = UiKit.El(Root);
                centre.style.position = Position.Absolute;
                centre.style.left = Length.Percent(50f);
                centre.style.width = 2f;
                centre.style.top = -2f;
                centre.style.bottom = -2f;
                centre.style.backgroundColor = Color.white;
                _label = UiKit.Text(Root, "", "bar__label");
            }

            public void Set(int bile, ClientContent content)
            {
                float limit = Mathf.Max(1, content.BileLimit);
                float half = Mathf.Abs(bile) / limit * 50f;
                _fill.style.left = Length.Percent(bile >= 0 ? 50f : 50f - half);
                _fill.style.width = Length.Percent(half);
                _fill.style.backgroundColor = bile > 0 ? new Color(0.9f, 0.75f, 0.15f) : new Color(0.35f, 0.2f, 0.5f);
                _label.text = "Bile " + (bile > 0 ? "+" : "") + bile;

                int key = content.BilePassiveThreshold * 1000 + content.BileExtreme;
                if (key == _marksFor) return;
                _marksFor = key;
                foreach (VisualElement m in _marks) m.RemoveFromHierarchy();
                _marks.Clear();
                foreach (int mark in new[] { content.BilePassiveThreshold, content.BileExtreme })
                {
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var m = new VisualElement();
                        m.style.position = Position.Absolute;
                        m.style.left = Length.Percent(50f + sign * mark / limit * 50f);
                        m.style.width = 2f;
                        m.style.top = 0f;
                        m.style.bottom = 0f;
                        m.style.backgroundColor = mark == content.BileExtreme ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 1f, 1f, 0.7f);
                        Root.Insert(1, m);
                        _marks.Add(m);
                    }
                }
            }
        }
    }
}
