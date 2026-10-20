using Rth.Client.Ui.Modals;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The quest list: not started, in progress, done, with how many are done. Click a quest to
    /// open it in the journal, where the whole story so far is.
    /// </summary>
    public sealed class QuestsPanel : UiPanel
    {
        public override string Id => "quests";
        public override string Title => "Quests";
        public override string Icon => "quests";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 6;
        public override Key Hotkey => Key.Q;

        public static readonly Color NotStarted = new Color(0.88f, 0.4f, 0.32f);
        public static readonly Color Started = new Color(0.94f, 0.84f, 0.4f);
        public static readonly Color Done = new Color(0.49f, 0.81f, 0.38f);

        private Label _summary;
        private ScrollView _list;
        private string _signature = "";

        protected override void Build(VisualElement root)
        {
            VisualElement top = UiKit.El(root, "row");
            _summary = UiKit.Text(top, "", "small");
            UiKit.El(top, "spacer");
            UiKit.Button(top, "Journal", () => Ui.OpenModal<JournalModal>("quests"));
            VisualElement well = UiKit.El(root, "well");
            well.style.flexGrow = 1;
            well.style.marginTop = 6f;
            _list = UiKit.Scroll(well);
        }

        public static Color ColourOf(QuestState state) => state == QuestState.Complete ? Done : state == QuestState.InProgress ? Started : NotStarted;

        public override void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            foreach (QuestEntry q in Session.Quests) sb.Append(q.Id).Append(q.State).Append(q.Progress).Append('|');
            string signature = sb.ToString();
            if (signature == _signature) return;
            _signature = signature;

            int done = 0;
            foreach (QuestEntry q in Session.Quests)
                if (q.State == QuestState.Complete) done++;
            _summary.text = "Completed " + UiKit.Colour(done + " of " + Session.Quests.Count, UiKit.Value);

            _list.Clear();
            if (Session.Quests.Count == 0) UiKit.Text(_list, "No quests yet. Talk to people.", "muted", "small");
            foreach (QuestEntry quest in Session.Quests)
            {
                string id = quest.Id;
                VisualElement row = UiKit.MasterRow(_list, quest.Name + (string.IsNullOrEmpty(quest.Progress) ? "" : "   " + quest.Progress),
                    () => Ui.OpenModal<JournalModal>("quests", id), ColourOf(quest.State));
                UiKit.Tooltip(row, () => UiKit.Colour("Click to open in the journal.", UiKit.Muted));
            }
        }
    }
}
