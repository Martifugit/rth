using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>The current task from a task-giver, and the point balances it earns.</summary>
    public sealed class TaskPanel : UiPanel
    {
        public override string Id => "task";
        public override string Title => "Task";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 6;

        private Label _task;
        private Label _points;

        protected override void Build(VisualElement root)
        {
            UiKit.Text(root, "Current task", "heading");
            _task = UiKit.Text(root, "");
            _task.style.whiteSpace = WhiteSpace.Normal;
            UiKit.Text(root, "Points", "heading").style.marginTop = 8f;
            _points = UiKit.Text(root, "");
        }

        public override void Refresh()
        {
            TaskMessage task = Session.CurrentTask;
            _task.text = task == null || string.IsNullOrEmpty(task.Description) ? "None. Task-givers such as Wenna at the skinning yard hand them out." : task.Description + "\n(from " + task.GiverName + ")";
            var lines = new List<string>();
            if (task != null)
                foreach (KeyValuePair<string, int> p in task.Points) lines.Add(p.Key + ": " + p.Value);
            _points.text = lines.Count > 0 ? string.Join("\n", lines) : "-";
        }
    }
}
