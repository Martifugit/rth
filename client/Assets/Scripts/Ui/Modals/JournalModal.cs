using System.Collections.Generic;
using System.Linq;
using Rth.Client.Ui.Panels;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>The journal: your quests in full, the achievement diary and the bestiary (J).</summary>
    public sealed class JournalModal : UiModal
    {
        public override string Id => "journal";
        public override string Title => "Journal";
        public override string Icon => "journal";
        public override Key Hotkey => Key.J;

        protected override IEnumerable<ModalTab> CreateTabs() => new ModalTab[] { new QuestJournalTab(), new AchievementsTab(), new BestiaryTab() };
    }

    /// <summary>Every quest: what it's about, what it needs and gives, and the story so far.</summary>
    public sealed class QuestJournalTab : ModalTab
    {
        public override string Id => "quests";
        public override string Title => "Quests";
        public override string Icon => "quests";

        private MasterDetail _view;
        private string _signature;

        protected override void Build(VisualElement root) => _view = new MasterDetail(root, Draw);

        public override void OnShow(string focus)
        {
            _signature = null;
            Refresh();
            if (focus != null) _view.Select(focus);
        }

        public override void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            foreach (QuestEntry q in Session.Quests) sb.Append(q.Id).Append(q.State).Append(q.Progress).Append(q.Log?.Count ?? 0).Append('|');
            if (sb.ToString() == _signature) return;
            _signature = sb.ToString();
            int done = Session.Quests.Count(q => q.State == QuestState.Complete);
            _view.SetEntries(Session.Quests.Select(q => new MasterDetail.Entry
            {
                Id = q.Id,
                Text = q.Name,
                Colour = QuestsPanel.ColourOf(q.State),
                Note = q.State == QuestState.Complete ? "done" : q.Progress,
            }).ToList(), "Quests  " + done + " / " + Session.Quests.Count);
        }

        private void Draw(string id)
        {
            QuestEntry q = Session.Quests.FirstOrDefault(e => e.Id == id);
            if (q.Name == null) return;
            VisualElement d = _view.Detail;
            UiKit.Text(d, q.Name.ToUpperInvariant(), "title-large");
            Label state = UiKit.Text(d, q.State == QuestState.Complete ? "Completed" : q.State == QuestState.InProgress ? "In progress" + (string.IsNullOrEmpty(q.Progress) ? "" : "  " + q.Progress) : "Not started", "bold");
            state.style.color = QuestsPanel.ColourOf(q.State);
            UiKit.Divider(d);
            UiKit.Text(d, q.Summary, "prose");

            if (q.Requirements != null && q.Requirements.Count > 0)
            {
                UiKit.Heading(d, "Requirements").style.marginTop = 10f;
                foreach (string r in q.Requirements) UiKit.Text(d, "•  " + r);
            }

            UiKit.Heading(d, "Journal").style.marginTop = 10f;
            if (q.Log == null || q.Log.Count == 0)
            {
                UiKit.Text(d, "Not started. Find whoever needs help.", "muted", "prose");
            }
            else
            {
                for (int i = 0; i < q.Log.Count; i++)
                {
                    bool current = i == q.Log.Count - 1 && q.State != QuestState.Complete;
                    Label entry = UiKit.Text(d, q.Log[i], "prose");
                    entry.style.marginBottom = 8f;
                    entry.style.color = current ? UiKit.Value : UiKit.TextColour;
                    if (!current && i < q.Log.Count - 1) entry.style.opacity = 0.75f;
                }
            }

            if (q.Rewards != null && q.Rewards.Count > 0)
            {
                UiKit.Heading(d, "Rewards").style.marginTop = 6f;
                foreach (string r in q.Rewards) UiKit.Text(d, "•  " + r, "value");
            }
        }
    }

    /// <summary>The achievement diary: by area, by tier, each with its progress and reward.</summary>
    public sealed class AchievementsTab : ModalTab
    {
        public override string Id => "achievements";
        public override string Title => "Achievements";
        public override string Icon => "achievement";

        private MasterDetail _view;
        private int _version = -1;

        protected override void Build(VisualElement root) => _view = new MasterDetail(root, Draw);

        public override void OnShow(string focus)
        {
            _version = -1;
            Refresh();
            if (focus != null) _view.Select(focus);
        }

        public override void Refresh()
        {
            if (Session.Content == null || _version == Session.StateVersion) return;
            _version = Session.StateVersion;
            AchievementProgress progress = Session.ServerState<AchievementProgress>("achievements");
            var areas = Session.Content.Achievements.GroupBy(a => a.Area).Select(g => new MasterDetail.Entry
            {
                Id = g.Key,
                Text = g.Key,
                Icon = "achievement",
                Note = g.Count(a => progress.Done.Contains(a.Id)) + " / " + g.Count(),
            }).ToList();
            int done = progress.Done.Count;
            _view.SetEntries(areas, "Diary  " + done + " / " + Session.Content.Achievements.Count);
        }

        private void Draw(string area)
        {
            AchievementProgress progress = Session.ServerState<AchievementProgress>("achievements");
            List<AchievementDef> list = Session.Content.Achievements.Where(a => a.Area == area).ToList();
            VisualElement d = _view.Detail;
            UiKit.Text(d, area.ToUpperInvariant() + " DIARY", "title-large");
            UiKit.Divider(d);
            foreach (IGrouping<string, AchievementDef> tier in list.GroupBy(a => a.Tier))
            {
                int tierDone = tier.Count(a => progress.Done.Contains(a.Id));
                UiKit.Heading(d, tier.Key + "   " + tierDone + " / " + tier.Count()).style.marginTop = 8f;
                foreach (AchievementDef a in tier)
                {
                    bool complete = progress.Done.Contains(a.Id);
                    VisualElement card = UiKit.El(d, "card", "row");
                    card.style.marginBottom = 4f;
                    VisualElement icon = UiKit.Icon(card, IconLibrary.Get(complete ? "achievement" : "padlock"), 28f);
                    icon.style.unityBackgroundImageTintColor = complete ? UiKit.Value : UiKit.Muted;
                    icon.style.marginRight = 10f;
                    VisualElement text = UiKit.El(card);
                    text.style.flexGrow = 1;
                    text.style.flexShrink = 1;
                    Label name = UiKit.Text(text, a.Name, "bold");
                    name.style.color = complete ? UiKit.Good : UiKit.TextColour;
                    UiKit.Text(text, a.Description, "prose", "small");
                    if (!complete && a.Objective.Count > 1)
                    {
                        progress.Progress.TryGetValue(a.Id, out int n);
                        var bar = new UiKit.Bar(text, UiKit.Value, 12f);
                        bar.Root.style.marginTop = 3f;
                        bar.Set(n / (float)a.Objective.Count, Mathf.Min(n, a.Objective.Count) + " / " + a.Objective.Count);
                    }
                    string reward = Reward(a.Reward);
                    if (reward.Length > 0) UiKit.Text(text, "Reward: " + reward, "muted", "small");
                }
            }
        }

        private string Reward(Reward r)
        {
            var parts = new List<string>();
            foreach (XpReward xp in r.Xp) parts.Add(xp.Amount.ToString("N0") + " " + SkillName(xp.Skill) + " XP");
            foreach (ItemCost item in r.Items) parts.Add((item.Count > 1 ? item.Count + " " : "") + (Session.Item(item.Item)?.Name ?? item.Item));
            return string.Join(", ", parts);
        }

        private string SkillName(string id) => Session.SkillName(id);
    }

    /// <summary>The bestiary: every creature, how dangerous it is, how many you've killed, and what it has given you.</summary>
    public sealed class BestiaryTab : ModalTab
    {
        public override string Id => "bestiary";
        public override string Title => "Bestiary";
        public override string Icon => "bestiary";

        private MasterDetail _view;
        private int _version = -1;

        protected override void Build(VisualElement root) => _view = new MasterDetail(root, Draw);

        public override void OnShow(string focus)
        {
            _version = -1;
            Refresh();
            if (focus != null) _view.Select(focus);
        }

        public override void Refresh()
        {
            if (Session.Content == null || _version == Session.StateVersion) return;
            _version = Session.StateVersion;
            BestiaryState state = Session.ServerState<BestiaryState>("bestiary");
            var creatures = Session.Content.Npcs.Where(n => n.Attackable).OrderBy(n => n.Name).Select(n => new MasterDetail.Entry
            {
                Id = n.Id,
                Text = n.Name,
                Icon = "bestiary",
                IconTint = state.Creatures.ContainsKey(n.Id) ? UiKit.IconTint : UiKit.Muted,
                Note = state.Creatures.TryGetValue(n.Id, out BestiaryEntry e) ? e.Kills.ToString("N0") : "",
            }).ToList();
            _view.SetEntries(creatures, "Creatures");
        }

        private void Draw(string id)
        {
            NpcInfo npc = Session.Npc(id);
            if (npc == null) return;
            BestiaryState state = Session.ServerState<BestiaryState>("bestiary");
            state.Creatures.TryGetValue(id, out BestiaryEntry entry);
            VisualElement d = _view.Detail;

            UiKit.Text(d, npc.Name.ToUpperInvariant(), "title-large");
            UiKit.Text(d, npc.Examine, "prose", "muted");
            UiKit.Divider(d);

            VisualElement stats = UiKit.El(d, "row");
            stats.style.flexWrap = Wrap.Wrap;
            Stat(stats, "Blood", npc.Blood.ToString("N0"));
            Stat(stats, "Hits up to", npc.MaxHit.ToString("N0"));
            Stat(stats, "Fights with", npc.Style == CombatStyle.None ? "-" : npc.Style.ToString());
            Stat(stats, "Temper", npc.Aggressive ? "Attacks on sight" : "Defends itself");
            Stat(stats, "Killed", entry != null ? entry.Kills.ToString("N0") : "0");

            UiKit.Heading(d, "Drop log").style.marginTop = 10f;
            if (entry == null || entry.Obtained.Count == 0)
            {
                UiKit.Text(d, "Nothing yet. Its drops, and what its carcass yields, are recorded here.", "muted", "prose");
                return;
            }
            VisualElement grid = UiKit.El(d, "slot-grid");
            foreach (KeyValuePair<string, long> item in entry.Obtained.OrderByDescending(kv => kv.Value))
            {
                ItemDef def = Session.Item(item.Key);
                var view = new ItemSlotView(grid);
                view.Show(new ItemSlot { ItemId = item.Key, Count = (int)Mathf.Min(item.Value, int.MaxValue) }, def);
                long count = item.Value;
                UiKit.Tooltip(view.Root, () => (def != null ? UiKit.Title(def.Name) : item.Key) + "\nObtained: " + UiKit.Colour(count.ToString("N0"), UiKit.Value));
            }
        }

        private static void Stat(VisualElement parent, string label, string value)
        {
            VisualElement card = UiKit.El(parent, "card");
            card.style.minWidth = 120f;
            card.style.marginRight = 6f;
            card.style.marginBottom = 6f;
            UiKit.Text(card, label.ToUpperInvariant(), "heading").style.fontSize = 11;
            UiKit.Text(card, value, "value", "bold");
        }
    }
}
