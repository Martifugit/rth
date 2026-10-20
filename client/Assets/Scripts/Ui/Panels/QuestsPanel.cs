using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The quest list: red not started, yellow in progress, green done, with how many are done
    /// at the top. Click a quest to open its journal entry under it.
    /// </summary>
    public sealed class QuestsPanel : UiPanel
    {
        public override string Id => "quests";
        public override string Title => "Quests";
        public override string Icon => "quests";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 5;
        public override Key Hotkey => Key.Q;

        private static readonly Color NotStarted = new Color(1f, 0.29f, 0.23f);
        private static readonly Color Started = new Color(1f, 1f, 0f);
        private static readonly Color Done = new Color(0.24f, 0.85f, 0.24f);

        private Label _summary;
        private ScrollView _list;
        private string _signature = "";
        private string _open;

        protected override void Build(VisualElement root)
        {
            _summary = UiKit.Text(root, "", "small");
            _summary.style.unityTextAlign = TextAnchor.MiddleCenter;
            _summary.style.marginBottom = 3f;
            VisualElement well = UiKit.El(root, "well");
            well.style.flexGrow = 1;
            _list = new ScrollView();
            _list.AddToClassList("scroll");
            well.Add(_list);
        }

        public override void Refresh()
        {
            var sb = new System.Text.StringBuilder(_open);
            foreach (QuestEntry q in Session.Quests) sb.Append(q.Name).Append(q.State).Append(q.Progress).Append(q.Journal?.Length ?? 0).Append('|');
            string signature = sb.ToString();
            if (signature == _signature) return;
            _signature = signature;

            int done = 0;
            foreach (QuestEntry q in Session.Quests)
                if (q.State == QuestState.Complete) done++;
            _summary.text = "Completed: " + UiKit.Colour(done + "/" + Session.Quests.Count, UiKit.Value);

            _list.Clear();
            if (Session.Quests.Count == 0) UiKit.Text(_list, "No quests yet. Talk to people.", "muted", "small");
            foreach (QuestEntry quest in Session.Quests)
            {
                QuestEntry q = quest;
                Label name = UiKit.Text(_list, quest.Name + (string.IsNullOrEmpty(quest.Progress) ? "" : "  " + quest.Progress));
                name.style.color = quest.State == QuestState.Complete ? Done : quest.State == QuestState.InProgress ? Started : NotStarted;
                name.style.paddingTop = name.style.paddingBottom = 2f;
                name.RegisterCallback<ClickEvent>(_ => _open = _open == q.Name ? null : q.Name);
                name.RegisterCallback<PointerEnterEvent>(_ => name.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f));
                name.RegisterCallback<PointerLeaveEvent>(_ => name.style.backgroundColor = new Color(0f, 0f, 0f, 0f));
                if (_open == q.Name && !string.IsNullOrEmpty(q.Journal))
                {
                    Label journal = UiKit.Text(_list, q.Journal, "small");
                    journal.style.whiteSpace = WhiteSpace.Normal;
                    journal.style.marginLeft = 8f;
                    journal.style.marginBottom = 4f;
                    journal.style.color = new Color(0.9f, 0.85f, 0.72f);
                }
            }
        }
    }
}
