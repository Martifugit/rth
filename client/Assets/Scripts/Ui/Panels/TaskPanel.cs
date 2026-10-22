using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>The current task from a task-giver, and the point balances tasks earn.</summary>
    public sealed class TaskPanel : UiPanel
    {
        public override string Id => "task";
        public override string Title => "Task";
        public override string Icon => "task";
        public override string DefaultWindow => UiDefaultLayout.Side2;
        public override int Order => 7;

        private Label _giver;
        private Label _task;
        private VisualElement _points;
        private string _pointsSignature;

        protected override void Build(VisualElement root)
        {
            VisualElement card = UiKit.El(root, "card");
            VisualElement head = UiKit.El(card, "row");
            UiKit.Icon(head, IconLibrary.Get("task"), 28f).style.unityBackgroundImageTintColor = UiKit.IconTint;
            _giver = UiKit.Text(head, "", "heading");
            _giver.style.marginLeft = 8f;
            _task = UiKit.Text(card, "", "prose");
            _task.style.marginTop = 6f;

            UiKit.Heading(root, "Points").style.marginTop = 12f;
            _points = UiKit.El(root, "well");
        }

        public override void Refresh()
        {
            TaskMessage task = Session.CurrentTask;
            bool has = task != null && !string.IsNullOrEmpty(task.Description);
            _giver.text = (has ? "From " + task.GiverName : "No task").ToUpperInvariant();
            _task.text = has ? task.Description : UiKit.Colour("Task-givers hand out work: Wenna at the skinning yard, Corvin at the apothecary.", UiKit.Muted);

            var points = new List<KeyValuePair<string, int>>();
            if (task != null) points.AddRange(task.Points);
            string signature = string.Join(",", points);
            if (signature == _pointsSignature) return;
            _pointsSignature = signature;
            _points.Clear();
            if (points.Count == 0) UiKit.Text(_points, "None yet.", "muted", "small");
            foreach (KeyValuePair<string, int> p in points)
            {
                VisualElement row = UiKit.El(_points, "row");
                UiKit.Text(row, p.Key);
                UiKit.El(row, "spacer");
                UiKit.Text(row, p.Value.ToString("N0"), "value", "bold");
            }
        }
    }
}
