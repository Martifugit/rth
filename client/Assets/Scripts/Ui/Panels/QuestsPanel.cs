using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>The quest list: red not started, yellow in progress, green done. Hover for the journal entry.</summary>
    public sealed class QuestsPanel : UiPanel
    {
        public override string Id => "quests";
        public override string Title => "Quests";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 5;
        public override Key Hotkey => Key.Q;

        private ScrollView _list;
        private string _signature = "";

        protected override void Build(VisualElement root)
        {
            _list = new ScrollView();
            _list.AddToClassList("scroll");
            root.Add(_list);
        }

        public override void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            foreach (QuestEntry q in Session.Quests) sb.Append(q.Name).Append(q.State).Append(q.Progress).Append(q.Journal?.Length ?? 0).Append('|');
            string signature = sb.ToString();
            if (signature == _signature) return;
            _signature = signature;

            _list.Clear();
            if (Session.Quests.Count == 0) UiKit.Text(_list, "No quests yet.", "muted");
            int done = 0;
            foreach (QuestEntry quest in Session.Quests)
            {
                if (quest.State == QuestState.Complete) done++;
                QuestEntry q = quest;
                Label row = UiKit.Text(_list, quest.Name + (string.IsNullOrEmpty(quest.Progress) ? "" : "  (" + quest.Progress + ")"));
                row.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.style.marginBottom = 3f;
                row.style.color = quest.State == QuestState.Complete ? new Color(0.4f, 0.9f, 0.4f)
                    : quest.State == QuestState.InProgress ? new Color(1f, 0.9f, 0.3f)
                    : new Color(0.95f, 0.4f, 0.35f);
                UiKit.Tooltip(row, () => q.Name + "\n\n" + q.Journal);
            }
            if (Session.Quests.Count > 0) UiKit.Text(_list, "Completed: " + done + " / " + Session.Quests.Count, "muted").style.marginTop = 6f;
        }
    }
}
