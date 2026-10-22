using System.Collections.Generic;
using System.Globalization;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// XP drops: each XP gain rises through a frameless piece of the screen as the skill's icon and
    /// "+123", then fades. Nothing behind them, nothing to click: only the numbers coming in. Gains
    /// that land together stack, newest at the bottom pushing older ones up; a gain that takes a
    /// skill up a level lingers longer with the new level beside it. Moved by the handle every
    /// frameless window has; closed from the window menu like any panel.
    ///
    /// Watches <see cref="GameSession.SkillXp"/>, so it needs nothing from the server beyond the
    /// skills message.
    /// </summary>
    public sealed class XpDropsPanel : UiPanel
    {
        private const float Life = 1.8f;
        private const float LevelLife = 2.8f;
        private const float FadeIn = 0.14f;
        private const float FadeOut = 0.55f;
        /// <summary>Pixels a second a drop rises once nothing is pushing it.</summary>
        private const float RiseSpeed = 30f;
        private const float RowHeight = 23f;
        private const float LevelRowHeight = 27f;
        /// <summary>Drops fade out over this many pixels below the top edge.</summary>
        private const float TopFade = 36f;
        /// <summary>A second gain in the same skill this soon adds to the drop already showing.</summary>
        private const float MergeWindow = 0.25f;
        private const int MaxDrops = 10;

        public override string Id => "xpdrops";
        public override string Title => "XP drops";
        public override string Icon => "xp";
        public override string DefaultWindow => UiDefaultLayout.XpDrops;
        public override bool Bare => true;
        public override Vector2? FixedSize => new Vector2(180f, 200f);

        private readonly Dictionary<string, long> _last = new Dictionary<string, long>();
        /// <summary>Showing drops, oldest first.</summary>
        private readonly List<Drop> _drops = new List<Drop>();
        private readonly Stack<Drop> _pool = new Stack<Drop>();
        private VisualElement _field;
        private string _player;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            _field = UiKit.El(root, "xpdrops");
            _field.pickingMode = PickingMode.Ignore;
        }

        public override void Observe()
        {
            if (Session.PlayerName != _player)
            {
                _player = Session.PlayerName;
                _last.Clear();
                ClearDrops();
            }
            // Logged out or between connections: the next skills message is the whole save, not a gain.
            if (Session.SkillXp.Count == 0)
            {
                _last.Clear();
                return;
            }

            bool first = _last.Count == 0;
            foreach (KeyValuePair<string, long> s in Session.SkillXp)
            {
                long before = _last.TryGetValue(s.Key, out long had) ? had : 0;
                if (!first && s.Value > before && IsVisible) Gain(s.Key, before, s.Value);
                _last[s.Key] = s.Value;
            }
        }

        private void Gain(string skillId, long before, long after)
        {
            SkillDef skill = Session.Skill(skillId);
            int level = 0;
            if (Session.Content != null)
            {
                var curve = Session.Content.XpCurve;
                int cap = curve.MaxFor(skill);
                int now = curve.LevelForXp(after, cap);
                if (now > curve.LevelForXp(before, cap)) level = now;
            }

            // Quick repeats in one skill add up rather than queueing a row each.
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                Drop d = _drops[i];
                if (d.Skill != skillId || Time.time - d.Born > MergeWindow) continue;
                d.Amount += after - before;
                d.Pulse = Time.time;
                if (level > 0) d.SetLevel(level);
                d.ShowAmount();
                return;
            }

            if (_drops.Count >= MaxDrops)
            {
                Recycle(_drops[0]);
                _drops.RemoveAt(0);
            }
            Drop drop = _pool.Count > 0 ? _pool.Pop() : new Drop();
            drop.Begin(_field, skillId, skill, after - before, level);
            _drops.Add(drop);
        }

        public override void Refresh()
        {
            if (_drops.Count == 0) return;
            float height = _field.layout.height > 1f && !float.IsNaN(_field.layout.height) ? _field.layout.height : FixedSize.Value.y;
            float follow = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);

            // Newest first: each sits where its age has carried it, but always at least a row above
            // the one below, so drops that land together stack instead of overlapping.
            float floor = float.NegativeInfinity;
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                Drop d = _drops[i];
                float age = Time.time - d.Born;
                float life = d.Level > 0 ? LevelLife : Life;
                if (age >= life || d.Y > height)
                {
                    Recycle(d);
                    _drops.RemoveAt(i);
                    continue;
                }

                float target = Mathf.Max(age * RiseSpeed, floor);
                d.Y = Mathf.Lerp(d.Y, target, follow);
                floor = Mathf.Max(target, d.Y) + (d.Level > 0 ? LevelRowHeight : RowHeight);

                float alpha = Mathf.Clamp01(age / FadeIn)
                              * Mathf.Clamp01((life - age) / FadeOut)
                              * Mathf.Clamp01((height - RowHeight - d.Y) / TopFade);
                float slide = 1f - Mathf.Clamp01(age / (FadeIn * 1.6f));
                float pulse = Mathf.Max(0f, 1f - (Time.time - d.Pulse) / 0.25f);
                float pop = d.Level > 0 ? Mathf.Lerp(1f, 1.18f, slide) : 1f;
                d.Place(d.Y, alpha, slide * slide * 10f, pop * (1f + 0.12f * pulse));
            }
        }

        private void ClearDrops()
        {
            foreach (Drop d in _drops) Recycle(d);
            _drops.Clear();
        }

        private void Recycle(Drop d)
        {
            d.Root.RemoveFromHierarchy();
            _pool.Push(d);
        }

        /// <summary>One "+123" with its skill's icon; pooled and reused.</summary>
        private sealed class Drop
        {
            public readonly VisualElement Root;
            private readonly VisualElement _shadow;
            private readonly VisualElement _icon;
            private readonly Label _amount;
            private readonly Label _level;
            public string Skill;
            public long Amount;
            public int Level;
            public float Born;
            public float Pulse = float.NegativeInfinity;
            public float Y;

            public Drop()
            {
                Root = UiKit.El(null, "xpdrop");
                Root.pickingMode = PickingMode.Ignore;
                _level = UiKit.Text(Root, "", "xpdrop__level");
                VisualElement iconBox = UiKit.El(Root, "xpdrop__icon-box");
                // Icons can't cast a text shadow, so a dark copy sits a pixel behind.
                _shadow = UiKit.El(iconBox, "xpdrop__icon", "xpdrop__icon--shadow");
                _icon = UiKit.El(iconBox, "xpdrop__icon");
                _amount = UiKit.Text(Root, "", "xpdrop__amount");
                foreach (VisualElement e in new[] { iconBox, _shadow, _icon, _amount, _level }) e.pickingMode = PickingMode.Ignore;
            }

            public void Begin(VisualElement parent, string skillId, SkillDef skill, long amount, int level)
            {
                Skill = skillId;
                Amount = amount;
                Born = Time.time;
                Pulse = float.NegativeInfinity;
                Y = 0f;
                Level = 0;
                Texture2D icon = IconLibrary.Get(skill != null && !string.IsNullOrEmpty(skill.Icon) ? skill.Icon : "xp");
                UiKit.SetIcon(_shadow, icon, new Color(0f, 0f, 0f, 0.75f));
                UiKit.SetIcon(_icon, icon, UiKit.IconTint);
                SetLevel(level);
                ShowAmount();
                Root.style.opacity = 0f;
                parent.Add(Root);
            }

            public void SetLevel(int level)
            {
                Level = level;
                Root.EnableInClassList("xpdrop--level", level > 0);
                _level.text = level > 0 ? "Level " + level : "";
                _level.style.display = level > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void ShowAmount() => _amount.text = "+" + Amount.ToString("N0", CultureInfo.InvariantCulture);

            public void Place(float y, float alpha, float slideX, float scale)
            {
                Root.style.bottom = y;
                Root.style.opacity = alpha;
                Root.style.translate = new Translate(slideX, 0f);
                Root.style.scale = new Scale(new Vector3(scale, scale, 1f));
            }
        }
    }
}
