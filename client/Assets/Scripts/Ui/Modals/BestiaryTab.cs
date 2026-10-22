using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.Skills;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>
    /// The bestiary, tabbed inside: Bosses, then one tab per region (where its creatures spawn; a
    /// creature in several regions is in each). In a region, creatures that leave a carcass for a
    /// skill (butchering) have a section of their own, marked with the skill's icon. Each
    /// creature's drop log hints at its notable drops (rare, or only it gives them) as
    /// silhouettes with a rarity word until you have had them, then shows the count; whatever
    /// else it has given you is listed after. Where creatures live, their carcasses and which
    /// drops are notable all come from the server ("bestiaryGuide"), worked out from content.
    /// </summary>
    public sealed class BestiaryTab : ModalTab
    {
        public override string Id => "bestiary";
        public override string Title => "Bestiary";
        public override string Icon => "bestiary";

        private const string BossesTab = "bosses";
        private const string RegionPrefix = "region:";
        private const string BossPrefix = "boss:";

        private static readonly Color Silhouette = new Color(0.03f, 0.025f, 0.02f, 0.92f);
        private static readonly Color BossTint = new Color(0.95f, 0.72f, 0.4f);

        private VisualElement _tabRow;
        private MasterDetail _view;
        private int _version = -1;
        private string _tab;
        /// <summary>Whether the player chose the tab (else it follows where they are standing, once the guide arrives).</summary>
        private bool _chosen;
        private string _tabsSignature;
        private readonly Dictionary<string, Label> _tabButtons = new Dictionary<string, Label>();

        protected override void Build(VisualElement root)
        {
            _tabRow = UiKit.El(root, "bestiary-tabs");
            _view = new MasterDetail(root, Draw);
        }

        public override void OnShow(string focus)
        {
            _version = -1;
            if (!_chosen) _tab = DefaultTab();
            if (focus != null)
            {
                string tab = TabOf(focus);
                if (tab != null) _tab = tab;
                _chosen |= tab != null;
            }
            Refresh();
            if (focus != null) _view.Select(focus);
        }

        public override void Refresh()
        {
            if (Session.Content == null || _version == Session.StateVersion) return;
            _version = Session.StateVersion;
            BuildTabs();
            BestiaryState state = Session.ServerState<BestiaryState>(StateKeys.Bestiary);
            _view.SetEntries(_tab == BossesTab ? BossEntries(state) : CreatureEntries(state, _tab.Substring(RegionPrefix.Length)));
        }

        // ------------------------------------------------------------------ tabs

        private BestiaryGuide Guide => Session.ServerState<BestiaryGuide>(StateKeys.Guide);

        private IEnumerable<(string Id, string Title)> Tabs()
        {
            if (Session.Content == null) yield break;
            if (Session.Content.Bosses.Count > 0) yield return (BossesTab, "Bosses");
            foreach (BestiaryRegion r in Guide.Regions) yield return (RegionPrefix + r.Name, r.Title);
        }

        private void BuildTabs()
        {
            List<(string Id, string Title)> tabs = Tabs().ToList();
            if (_tab == null || !_chosen || tabs.All(t => t.Id != _tab)) _tab = DefaultTab();
            string signature = string.Join("|", tabs.Select(t => t.Id + "=" + t.Title));
            if (signature != _tabsSignature)
            {
                _tabsSignature = signature;
                _tabRow.Clear();
                _tabButtons.Clear();
                foreach ((string id, string title) in tabs)
                {
                    string tabId = id;
                    Label button = UiKit.Text(_tabRow, title.ToUpperInvariant(), "filter-tab", "bestiary-tab");
                    button.RegisterCallback<ClickEvent>(_ =>
                    {
                        _tab = tabId;
                        _chosen = true;
                        _version = -1;
                        Refresh();
                    });
                    _tabButtons[tabId] = button;
                }
            }
            foreach (KeyValuePair<string, Label> b in _tabButtons) b.Value.EnableInClassList("filter-tab--on", b.Key == _tab);
        }

        /// <summary>The region you are standing in if it has creatures, else the first region, else Bosses.</summary>
        private string DefaultTab()
        {
            List<(string Id, string Title)> tabs = Tabs().ToList();
            string here = Ui.regions != null && Ui.regions.Current != null ? RegionPrefix + Ui.regions.Current.Name : null;
            if (here != null && tabs.Any(t => t.Id == here)) return here;
            return tabs.Where(t => t.Id != BossesTab).Select(t => t.Id).FirstOrDefault() ?? (tabs.Count > 0 ? tabs[0].Id : BossesTab);
        }

        /// <summary>The tab that shows an entry ("boss:x", or a creature's first region).</summary>
        private string TabOf(string id)
        {
            if (id.StartsWith(BossPrefix)) return BossesTab;
            BestiaryCreature c = Guide.Creatures.Find(e => e.Id == id);
            if (c == null) return null;
            return RegionPrefix + (c.Regions.Count > 0 ? c.Regions[0] : "");
        }

        private string RegionTitle(string name)
        {
            BestiaryRegion r = Guide.Regions.Find(e => e.Name == name);
            if (r != null) return r.Title;
            WorldRegion w = Ui.regions?.Regions?.FirstOrDefault(e => e.Name == name);
            return w != null && !string.IsNullOrEmpty(w.Title) ? w.Title : name;
        }

        // ------------------------------------------------------------------ the list

        private List<MasterDetail.Entry> BossEntries(BestiaryState state) =>
            Session.Content.Bosses.OrderBy(b => b.Name).Select(b => new MasterDetail.Entry
            {
                Id = BossPrefix + b.Id,
                Text = b.Name,
                Icon = string.IsNullOrEmpty(b.Icon) ? "bestiary" : b.Icon,
                IconTint = state.Bosses.TryGetValue(b.Id, out BossRecord r) && r.Completions > 0 ? BossTint : UiKit.Muted,
                Note = r != null ? r.Completions.ToString("N0") : "",
            }).ToList();

        /// <summary>A region's creatures: the plain ones, then a section per skill their carcasses feed (Butchering).</summary>
        private List<MasterDetail.Entry> CreatureEntries(BestiaryState state, string region)
        {
            var entries = new List<MasterDetail.Entry>();
            List<(BestiaryCreature Guide, NpcInfo Npc)> here = Guide.Creatures
                .Where(c => region.Length == 0 ? c.Regions.Count == 0 : c.Regions.Contains(region))
                .Select(c => (c, Session.Npc(c.Id)))
                .Where(c => c.Item2 != null)
                .ToList();
            bool sections = here.Any(c => c.Guide.Carcass != null);
            foreach (var group in here.GroupBy(c => c.Guide.Carcass?.Skill ?? "").OrderBy(g => g.Key.Length == 0 ? 0 : 1).ThenBy(g => g.Key))
            {
                SkillDef skill = group.Key.Length > 0 ? Session.Skill(group.Key) : null;
                foreach ((BestiaryCreature guide, NpcInfo npc) in group.OrderBy(c => c.Npc.Name))
                {
                    bool met = state.Creatures.TryGetValue(npc.Id, out BestiaryEntry e);
                    entries.Add(new MasterDetail.Entry
                    {
                        Id = npc.Id,
                        Text = npc.Name,
                        Icon = skill != null && !string.IsNullOrEmpty(skill.Icon) ? skill.Icon : "bestiary",
                        IconTint = met ? UiKit.IconTint : UiKit.Muted,
                        Note = met ? e.Kills.ToString("N0") : "",
                        Section = !sections ? null : group.Key.Length == 0 ? "Creatures" : (skill?.Name ?? group.Key),
                    });
                }
            }
            return entries;
        }

        // ------------------------------------------------------------------ the detail

        private void Draw(string id)
        {
            if (id != null && id.StartsWith(BossPrefix))
            {
                DrawBoss(id.Substring(BossPrefix.Length));
                return;
            }
            NpcInfo npc = Session.Npc(id);
            if (npc == null) return;
            BestiaryState state = Session.ServerState<BestiaryState>(StateKeys.Bestiary);
            state.Creatures.TryGetValue(id, out BestiaryEntry entry);
            BestiaryCreature guide = Guide.Creatures.Find(c => c.Id == id);
            VisualElement d = _view.Detail;

            UiKit.Text(d, npc.Name.ToUpperInvariant(), "title-large");
            UiKit.Text(d, npc.Examine, "prose", "muted");
            if (guide != null && guide.Regions.Count > 0)
                UiKit.Text(d, "Lives in " + string.Join(", ", guide.Regions.Select(RegionTitle)) + ".", "prose", "small");
            UiKit.Divider(d);

            VisualElement stats = UiKit.El(d, "row");
            stats.style.flexWrap = Wrap.Wrap;
            Stat(stats, "Blood", npc.Blood.ToString("N0"));
            Stat(stats, "Hits up to", npc.MaxHit.ToString("N0"));
            Stat(stats, "Fights with", npc.Style == CombatStyle.None ? "-" : npc.Style.ToString());
            Stat(stats, "Temper", npc.Aggressive ? "Attacks on sight" : "Defends itself");
            Stat(stats, "Killed", entry != null ? entry.Kills.ToString("N0") : "0");

            if (guide?.Carcass != null) Carcass(d, guide.Carcass);
            DropLog(d, guide?.Notable, entry?.Obtained, "this creature", "kill", "Nothing yet. Its drops, and what its carcass yields, are recorded here.");
        }

        private void DrawBoss(string id)
        {
            BossDef boss = Session.Content.Bosses.Find(b => b.Id == id);
            if (boss == null) return;
            BestiaryState state = Session.ServerState<BestiaryState>(StateKeys.Bestiary);
            state.Bosses.TryGetValue(id, out BossRecord record);
            BestiaryCreature guide = Guide.Bosses.Find(b => b.Id == id);
            VisualElement d = _view.Detail;
            UiKit.Text(d, boss.Name.ToUpperInvariant(), "title-large");
            UiKit.Text(d, boss.Description, "prose", "muted");
            if (!string.IsNullOrEmpty(boss.Region)) UiKit.Text(d, "Fought in " + RegionTitle(boss.Region) + ".", "prose", "small");
            UiKit.Divider(d);
            VisualElement stats = UiKit.El(d, "row");
            stats.style.flexWrap = Wrap.Wrap;
            Stat(stats, "Completions", record != null ? record.Completions.ToString("N0") : "0");
            Stat(stats, "Attempts", record != null ? record.Attempts.ToString("N0") : "0");
            Stat(stats, "Personal best", record != null && record.BestTicks > 0 ? Panels.BossTimerPanel.Duration(record.BestTicks) : "-");
            Stat(stats, "Last run", record != null && record.LastTicks > 0 ? Panels.BossTimerPanel.Duration(record.LastTicks) : "-");
            DropLog(d, guide?.Notable, record?.Obtained, "this boss", "completion", "Nothing yet. What its rewards give you is recorded here.");
        }

        /// <summary>The carcass it leaves, and where and with what level it is worked.</summary>
        private void Carcass(VisualElement d, BestiaryCarcass carcass)
        {
            ItemDef item = Session.Item(carcass.Item);
            RecipeInfo recipe = Session.Content.Recipes.Find(r => r.Id == carcass.Recipe);
            ObjectDef station = Session.Content.Objects.Find(o => o.Id == carcass.Station);
            string skill = Session.SkillName(carcass.Skill);
            bool able = Session.Level(carcass.Skill) >= carcass.Level;

            VisualElement card = UiKit.El(d, "card", "row", "bestiary-carcass");
            VisualElement icon = UiKit.El(card, "slot", "bestiary-carcass__slot");
            VisualElement itemIcon = UiKit.El(icon, "slot__icon");
            UiKit.SetItem(itemIcon, item);
            VisualElement text = UiKit.El(card, "bestiary-carcass__text");
            UiKit.Text(text, "Leaves a carcass: " + (item?.Name ?? carcass.Item), "bold");
            string verb = recipe != null && !string.IsNullOrEmpty(recipe.Verb) ? char.ToUpperInvariant(recipe.Verb[0]) + recipe.Verb.Substring(1) : "Work";
            string where = station != null ? " at a " + station.Name.ToLowerInvariant() : "";
            UiKit.Text(text, verb + " it" + where + " for what it yields. "
                + UiKit.Colour(skill + " " + carcass.Level, able ? UiKit.Good : UiKit.Bad) + ".", "prose", "small");
        }

        /// <summary>
        /// The drop log: first the notable drops as hints (hidden until had), then everything
        /// else this creature has given you.
        /// </summary>
        private void DropLog(VisualElement d, List<NotableDrop> notable, Dictionary<string, long> obtained, string what, string unit, string empty)
        {
            UiKit.Heading(d, "Drop log").style.marginTop = 10f;
            obtained = obtained ?? new Dictionary<string, long>();
            notable = notable ?? new List<NotableDrop>();
            if (notable.Count > 0)
            {
                int had = notable.Count(n => obtained.TryGetValue(n.Item, out long c) && c > 0);
                UiKit.Text(d, "Its rare drops, and those only " + what + " gives: " + had + " / " + notable.Count + " found.", "muted", "small", "prose");
                VisualElement hints = UiKit.El(d, "slot-grid", "bestiary-hints");
                foreach (NotableDrop n in notable) Hint(hints, n, obtained.TryGetValue(n.Item, out long count) ? count : 0, what, unit);
            }

            List<KeyValuePair<string, long>> rest = obtained.Where(kv => kv.Value > 0 && notable.All(n => n.Item != kv.Key)).OrderByDescending(kv => kv.Value).ToList();
            if (rest.Count == 0)
            {
                if (notable.Count == 0) UiKit.Text(d, empty, "muted", "prose");
                return;
            }
            if (notable.Count > 0) UiKit.Heading(d, "Also obtained").style.marginTop = 8f;
            VisualElement grid = UiKit.El(d, "slot-grid");
            foreach (KeyValuePair<string, long> item in rest)
            {
                ItemDef def = Session.Item(item.Key);
                var view = new ItemSlotView(grid);
                view.Show(new ItemSlot { ItemId = item.Key, Count = (int)Mathf.Min(item.Value, int.MaxValue) }, def);
                long count = item.Value;
                UiKit.Tooltip(view.Root, () => (def != null ? UiKit.Title(def.Name) : item.Key) + "\nObtained: " + UiKit.Colour(count.ToString("N0"), UiKit.Value));
            }
        }

        /// <summary>One notable drop: the item and its count once had, else its silhouette and a question mark; the rarity word beneath.</summary>
        private void Hint(VisualElement parent, NotableDrop drop, long count, string what, string unit)
        {
            ItemDef def = Session.Item(drop.Item);
            bool known = count > 0;
            VisualElement cell = UiKit.El(parent, "bestiary-hint");
            cell.EnableInClassList("bestiary-hint--unique", drop.Unique);
            VisualElement slot;
            if (known)
            {
                var view = new ItemSlotView(cell);
                view.Show(new ItemSlot { ItemId = drop.Item, Count = (int)Mathf.Min(count, int.MaxValue) }, def);
                slot = view.Root;
            }
            else
            {
                slot = UiKit.El(cell, "slot", "bestiary-hint__slot--unknown");
                VisualElement icon = UiKit.El(slot, "slot__icon");
                icon.pickingMode = PickingMode.Ignore;
                UiKit.SetIcon(icon, IconLibrary.ForItem(def), Silhouette);
                UiKit.Text(slot, "?", "bestiary-hint__mark").pickingMode = PickingMode.Ignore;
            }
            string word = DropRarity.Word(drop.Chance);
            Label caption = UiKit.Text(cell, word, "bestiary-hint__caption");
            caption.style.color = RarityColour(drop.Chance);

            UiKit.Tooltip(slot, () =>
            {
                string text = known ? UiKit.Title(def?.Name ?? drop.Item) : UiKit.Title("???");
                text += "\n" + UiKit.Colour(word, RarityColour(drop.Chance)) + "  " + UiKit.Colour(Odds(drop.Chance, unit), UiKit.Muted);
                if (drop.Unique) text += "\nOnly " + what + " gives it.";
                text += known ? "\nObtained: " + UiKit.Colour(count.ToString("N0"), UiKit.Value) : "\n" + UiKit.Colour("Not found yet.", UiKit.Muted);
                return text;
            });
        }

        /// <summary>"about 1 in 33 kills" (or a percentage when it's likely).</summary>
        private static string Odds(double chance, string unit) =>
            chance <= 0 ? "" : chance >= 0.2 ? Mathf.RoundToInt((float)chance * 100f) + "% a " + unit : "about 1 in " + Mathf.RoundToInt((float)(1 / chance)).ToString("N0") + " " + unit + "s";

        private static Color RarityColour(double chance) =>
            chance >= 0.25 ? UiKit.Muted
            : chance >= 0.05 ? UiKit.TextColour
            : chance >= 0.01 ? new Color(0.45f, 0.7f, 1f)
            : new Color(0.82f, 0.52f, 1f);

        private static void Stat(VisualElement parent, string label, string value)
        {
            VisualElement card = UiKit.El(parent, "card");
            card.style.minWidth = 120f;
            card.style.marginRight = 6f;
            card.style.marginBottom = 6f;
            UiKit.Text(card, label.ToUpperInvariant(), "heading").style.fontSize = 11;
            UiKit.Text(card, value, "value", "bold");
        }

        private static class StateKeys
        {
            public const string Bestiary = "bestiary";
            public const string Guide = "bestiaryGuide";
        }
    }
}
