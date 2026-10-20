using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>The current task from a task-giver, as a notice, and the point balances tasks earn.</summary>
    public sealed class TaskPanel : UiPanel
    {
        public override string Id => "task";
        public override string Title => "Task";
        public override string Icon => "task";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 6;

        private Label _giver;
        private Label _task;
        private VisualElement _points;
        private string _pointsSignature = "";

        protected override void Build(VisualElement root)
        {
            VisualElement notice = UiKit.El(root, "raised");
            notice.style.paddingLeft = notice.style.paddingRight = notice.style.paddingTop = notice.style.paddingBottom = 6f;
            VisualElement head = UiKit.El(notice, "row");
            UiKit.Icon(head, IconLibrary.Get("task"), 32f).style.marginRight = 6f;
            _giver = UiKit.Text(head, "", "heading");
            _giver.style.whiteSpace = WhiteSpace.Normal;
            _giver.style.flexShrink = 1;
            _task = UiKit.Text(notice, "");
            _task.style.whiteSpace = WhiteSpace.Normal;
            _task.style.marginTop = 4f;

            UiKit.Text(root, "Points", "heading", "small").style.marginTop = 8f;
            _points = UiKit.El(root, "well");
        }

        public override void Refresh()
        {
            TaskMessage task = Session.CurrentTask;
            bool has = task != null && !string.IsNullOrEmpty(task.Description);
            _giver.text = has ? "From " + task.GiverName : "No task";
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
                UiKit.Text(row, p.Key, "small");
                UiKit.El(row, "spacer");
                UiKit.Text(row, p.Value.ToString("N0"), "value", "small");
            }
        }
    }
}
