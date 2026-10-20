using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>
    /// The master-detail layout most modal pages use: a list on the left (quests, creatures,
    /// skills), the selected entry in detail on the right. The page fills the list and draws the
    /// detail when asked; this keeps the selection and the look.
    /// </summary>
    public sealed class MasterDetail
    {
        public readonly VisualElement Root;
        public readonly VisualElement Detail;
        private readonly ScrollView _list;
        private readonly ScrollView _detailScroll;
        private readonly Action<string> _draw;
        private readonly Dictionary<string, VisualElement> _rows = new Dictionary<string, VisualElement>();

        public string Selected { get; private set; }

        public MasterDetail(VisualElement parent, Action<string> drawDetail, float listWidth = 240f)
        {
            _draw = drawDetail;
            Root = UiKit.El(parent, "row");
            Root.style.flexGrow = 1;
            Root.style.alignItems = Align.Stretch;
            VisualElement master = UiKit.El(Root, "well", "master");
            master.style.width = listWidth;
            _list = UiKit.Scroll(master);
            _detailScroll = UiKit.Scroll(Root);
            _detailScroll.AddToClassList("detail");
            Detail = _detailScroll.contentContainer;
            Detail.style.paddingRight = 10f;
        }

        public struct Entry
        {
            public string Id;
            public string Text;
            public string Note;
            public string Icon;
            public Color? IconTint;
            public Color? Colour;
        }

        /// <summary>Replaces the list. Keeps the selection if it is still there, else selects the first.</summary>
        public void SetEntries(IList<Entry> entries, string headingText = null)
        {
            _list.Clear();
            _rows.Clear();
            if (headingText != null) UiKit.Heading(_list, headingText);
            foreach (Entry entry in entries)
            {
                string id = entry.Id;
                VisualElement row = UiKit.El(_list, "master-row");
                if (entry.Icon != null)
                {
                    VisualElement icon = UiKit.Icon(row, IconLibrary.Get(entry.Icon), 20f);
                    icon.style.unityBackgroundImageTintColor = entry.IconTint ?? UiKit.IconTint;
                    icon.style.marginRight = 8f;
                }
                Label text = UiKit.Text(row, entry.Text);
                text.style.flexGrow = 1;
                if (entry.Colour.HasValue) text.style.color = entry.Colour.Value;
                if (!string.IsNullOrEmpty(entry.Note)) UiKit.Text(row, entry.Note, "muted", "small");
                row.RegisterCallback<ClickEvent>(_ => Select(id));
                _rows[id] = row;
            }
            if (Selected == null || !_rows.ContainsKey(Selected)) Selected = entries.Count > 0 ? entries[0].Id : null;
            Select(Selected);
        }

        public void Select(string id)
        {
            if (id != null && !_rows.ContainsKey(id)) return;
            Selected = id;
            foreach (KeyValuePair<string, VisualElement> row in _rows) row.Value.EnableInClassList("master-row--selected", row.Key == id);
            Detail.Clear();
            if (id != null) _draw(id);
            _detailScroll.scrollOffset = Vector2.zero;
        }

        /// <summary>Redraws the selected entry (when its data changed).</summary>
        public void Redraw()
        {
            Detail.Clear();
            if (Selected != null) _draw(Selected);
        }
    }
}
