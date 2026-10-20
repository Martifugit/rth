using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The main action bar. On top, the combat resources: blood across the full width, phlegm and
    /// breath side by side, then the bile axis between its two poles. Below, the ability slots
    /// (keys 1-9) with cooldowns, and a line of chips saying what the current bile and any
    /// effects are doing. More bars will be further instances of the slot row.
    /// </summary>
    public sealed class ActionBarPanel : UiPanel
    {
        public override string Id => "actionbar";
        public override string Title => "Action bar";
        public override string Icon => "book";
        public override string DefaultWindow => UiDefaultLayout.ActionBar;
        public override bool Bare => true;
        public override Vector2 MinSize => new Vector2(320f, 140f);

        private static readonly Color BloodColour = new Color(0.78f, 0.1f, 0.08f);
        private static readonly Color PhlegmColour = new Color(0.45f, 0.66f, 0.4f);
        private static readonly Color BreathColour = new Color(0.42f, 0.66f, 0.92f);
        private static readonly Color YellowBile = new Color(0.92f, 0.74f, 0.14f);
        private static readonly Color BlackBile = new Color(0.42f, 0.24f, 0.6f);

        private UiKit.Bar _blood;
        private UiKit.Bar _phlegm;
        private UiKit.Bar _breath;
        private BileAxis _bile;
        private VisualElement _slotRow;
        private VisualElement _chips;
        private readonly List<Slot> _slots = new List<Slot>();
        private string _barSignature = "";
        private string _chipSignature = "";

        private sealed class Slot
        {
            public string AbilityId;
            public VisualElement Root;
            public VisualElement Cooldown;
            public Label Timer;
        }

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            VisualElement frame = UiKit.El(root, "actionbar");
            frame.style.flexGrow = 1;

            _blood = new UiKit.Bar(frame, BloodColour, IconLibrary.Get("blood"), 16f);
            UiKit.Tooltip(_blood.Root, () => UiKit.Title("Blood") + "\nYour life. Food restores it.");

            VisualElement pair = UiKit.El(frame, "row");
            _phlegm = new UiKit.Bar(pair, PhlegmColour, IconLibrary.Get("potion", PhlegmColour), 14f);
            _phlegm.Root.style.flexGrow = 1;
            _phlegm.Root.style.marginRight = 6f;
            UiKit.Tooltip(_phlegm.Root, () => UiKit.Title("Phlegm") + "\nBuilt by basic abilities, spent by thresholds and ultimates. Fades out of combat.");
            _breath = new UiKit.Bar(pair, BreathColour, IconLibrary.Get("focus"), 14f);
            _breath.Root.style.flexGrow = 1;
            UiKit.Tooltip(_breath.Root, () => UiKit.Title("Breath") + "\nSpent by focuses such as deflection (F). Recovers out of combat.");

            _bile = new BileAxis(frame);
            UiKit.Tooltip(_bile.Root, () => UiKit.Title("Bile") +
                "\n" + UiKit.Colour("Black bile", BlackBile) + " to the left: melancholic, warding." +
                "\n" + UiKit.Colour("Yellow bile", YellowBile) + " to the right: choleric, harder hitting." +
                "\nPale marks: where passives begin. Red marks: fever and torpor.");

            _slotRow = UiKit.El(frame, "row");
            _slotRow.style.justifyContent = Justify.Center;
            _slotRow.style.marginTop = 4f;

            _chips = UiKit.El(frame, "row");
            _chips.style.justifyContent = Justify.Center;
            _chips.style.marginTop = 3f;
        }

        public override void Refresh()
        {
            PlayerStateMessage state = Session.PlayerState;
            ClientContent content = Session.Content;
            if (state == null || content == null) return;

            _blood.Set(state.Blood / (float)Mathf.Max(1, state.MaxBlood), state.Blood + " / " + state.MaxBlood);
            _phlegm.Set(state.Phlegm / (float)Mathf.Max(1, content.MaxPhlegm), state.Phlegm + "%");
            _breath.Set(state.Breath / (float)Mathf.Max(1, state.MaxBreath), state.Breath + " / " + state.MaxBreath);
            _breath.Color = state.Deflect != CombatStyle.None ? new Color(0.65f, 0.82f, 1f) : BreathColour;
            _bile.Set(state.Bile, content);

            string signature = string.Join(",", content.ActionBar);
            if (signature != _barSignature) RebuildSlots(content);
            UpdateSlots(state);
            UpdateChips(state, content);
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
                string id = ability.Id;
                var slot = new Slot { AbilityId = id, Root = UiKit.El(_slotRow, "ability") };
                slot.Root.style.backgroundColor = Color.Lerp(GameText.AbilityColor(ability), Color.black, 0.45f);
                UiKit.Icon(slot.Root, IconLibrary.ForAbility(ability), 32f, "slot__icon").pickingMode = PickingMode.Ignore;
                slot.Cooldown = UiKit.El(slot.Root, "ability__cooldown");
                slot.Cooldown.pickingMode = PickingMode.Ignore;
                string kind = ability.Type == AbilityType.Threshold ? "T" : ability.Type == AbilityType.Ultimate ? "U" : "";
                UiKit.Text(slot.Root, kind, "ability__kind").pickingMode = PickingMode.Ignore;
                UiKit.Text(slot.Root, (i + 1).ToString(), "ability__key").pickingMode = PickingMode.Ignore;
                slot.Timer = UiKit.Text(slot.Root, "", "ability__timer");
                slot.Timer.pickingMode = PickingMode.Ignore;
                slot.Root.RegisterCallback<ClickEvent>(_ => Session.UseAbility(id));
                UiKit.Tooltip(slot.Root, () =>
                {
                    AbilityDef a = Session.Ability(id);
                    return a == null ? null : GameText.Describe(a, Session.TickSeconds, Session.AbilityProblem(a));
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
                slot.Root.EnableInClassList("ability--unusable", Session.AbilityProblem(ability) != null);
                slot.Root.EnableInClassList("ability--queued", state.QueuedAbility == ability.Id);

                // Cooldown: whichever is later, the ability's own or the global one.
                float ready = state.GlobalCooldownReadyTick;
                foreach (CooldownState cd in state.Cooldowns)
                    if (cd.AbilityId == ability.Id) ready = Mathf.Max(ready, cd.ReadyTick);
                float remaining = ready - now;
                float total = Mathf.Max(ability.CooldownTicks, 3);
                slot.Cooldown.style.height = Length.Percent(remaining > 0f ? Mathf.Clamp01(remaining / total) * 100f : 0f);
                float seconds = remaining * Session.TickSeconds;
                slot.Timer.text = seconds > 0.95f ? Mathf.CeilToInt(seconds).ToString() : "";
            }
        }

        /// <summary>Small labels for what is happening to you: the bile state, fever or torpor, effects, combat.</summary>
        private void UpdateChips(PlayerStateMessage state, ClientContent content)
        {
            var rules = new CombatRules
            {
                BileLimit = content.BileLimit,
                BilePassiveThreshold = content.BilePassiveThreshold,
                BileExtreme = content.BileExtreme,
            };
            var chips = new List<(string Text, Color Colour)>();
            if (rules.IsCholeric(state.Bile)) chips.Add(("Choleric +" + rules.CholericBonusPercent(state.Bile) + "% damage", YellowBile));
            else if (rules.IsMelancholic(state.Bile)) chips.Add(("Melancholic -" + rules.MelancholicWardPercent(state.Bile) + "% damage taken", new Color(0.75f, 0.6f, 1f)));
            else chips.Add(("Balanced", UiKit.Muted));
            if (rules.IsFevered(state.Bile)) chips.Add(("Fever", UiKit.Bad));
            if (rules.IsTorpid(state.Bile)) chips.Add(("Torpor", new Color(0.6f, 0.6f, 1f)));
            foreach (EffectState e in state.Effects)
            {
                int secs = Mathf.Max(0, Mathf.CeilToInt((e.EndTick - (float)state.Tick) * Session.TickSeconds));
                chips.Add((e.Effect + " " + e.Magnitude + "% " + secs + "s", UiKit.Good));
            }
            if (state.Deflect != CombatStyle.None) chips.Add(("Deflecting " + state.Deflect.ToString().ToLowerInvariant(), BreathColour));
            if (state.InCombat) chips.Add(("In combat", UiKit.Bad));

            var sb = new System.Text.StringBuilder();
            foreach ((string text, Color _) in chips) sb.Append(text).Append('|');
            if (sb.ToString() == _chipSignature) return;
            _chipSignature = sb.ToString();
            _chips.Clear();
            foreach ((string text, Color colour) in chips)
            {
                Label chip = UiKit.Text(_chips, text, "chip");
                chip.style.color = colour;
            }
        }

        /// <summary>The bile axis: centred, black bile filling left, yellow filling right, with threshold marks and pole icons.</summary>
        private sealed class BileAxis
        {
            public readonly VisualElement Root;
            private readonly VisualElement _track;
            private readonly VisualElement _fill;
            private readonly Label _label;
            private readonly List<VisualElement> _marks = new List<VisualElement>();
            private int _marksFor = int.MinValue;

            public BileAxis(VisualElement parent)
            {
                Root = UiKit.El(parent, "bar-row");
                UiKit.Icon(Root, IconLibrary.Get("draught"), 16f, "bar-row__icon");
                _track = UiKit.El(Root, "bar");
                _track.style.height = 14f;
                _fill = UiKit.El(_track, "bar__fill");
                UiKit.El(_track, "bar__gloss");
                VisualElement centre = UiKit.El(_track);
                centre.style.position = Position.Absolute;
                centre.style.left = Length.Percent(50f);
                centre.style.width = 2f;
                centre.style.marginLeft = -1f;
                centre.style.top = 0f;
                centre.style.bottom = 0f;
                centre.style.backgroundColor = new Color(1f, 1f, 1f, 0.85f);
                // The value sits to the left so the centre line never runs through it.
                _label = UiKit.Text(_track, "", "bar__label");
                _label.style.unityTextAlign = TextAnchor.MiddleLeft;
                _label.style.paddingLeft = 4f;
                VisualElement right = UiKit.Icon(Root, IconLibrary.Get("flare"), 16f, "bar-row__icon");
                right.style.marginLeft = 3f;
                right.style.marginRight = 0f;
            }

            public void Set(int bile, ClientContent content)
            {
                float limit = Mathf.Max(1, content.BileLimit);
                float half = Mathf.Abs(bile) / limit * 50f;
                _fill.style.left = Length.Percent(bile >= 0 ? 50f : 50f - half);
                _fill.style.width = Length.Percent(half);
                _fill.style.backgroundColor = bile > 0 ? YellowBile : BlackBile;
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
                        var m = new VisualElement { pickingMode = PickingMode.Ignore };
                        m.style.position = Position.Absolute;
                        m.style.left = Length.Percent(50f + sign * mark / limit * 50f);
                        m.style.width = 1f;
                        m.style.top = 0f;
                        m.style.bottom = 0f;
                        m.style.backgroundColor = mark == content.BileExtreme ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.45f);
                        _track.Insert(2, m);
                        _marks.Add(m);
                    }
                }
            }
        }
    }
}
