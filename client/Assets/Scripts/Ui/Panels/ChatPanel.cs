using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The chatbox: game messages and chat on parchment, filter buttons (all, game, public), and
    /// the line you type on, prefixed with your name. Enter starts typing (keys stop being
    /// hotkeys), Enter sends, Escape stops. New channels (private, group) become more filters.
    /// </summary>
    public sealed class ChatPanel : UiPanel
    {
        private const int MaxLines = 200;

        public override string Id => "chat";
        public override string Title => "Chat";
        public override string Icon => "chat";
        public override string DefaultWindow => UiDefaultLayout.Chat;
        public override Vector2 MinSize => new Vector2(260f, 110f);

        private enum Filter
        {
            All,
            Game,
            Public,
        }

        private ScrollView _scroll;
        private TextField _input;
        private Label _name;
        private readonly List<GameSession.LogLine> _lines = new List<GameSession.LogLine>();
        private readonly List<(Filter Filter, Label Button)> _filterButtons = new List<(Filter, Label)>();
        private Filter _filter = Filter.All;
        private int _seen;

        protected override void Build(VisualElement root)
        {
            VisualElement box = UiKit.El(root, "chatbox");
            box.style.flexGrow = 1;
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("scroll");
            box.Add(_scroll);

            VisualElement inputRow = UiKit.El(box, "chat-input-row");
            _name = UiKit.Text(inputRow, "", "chat-name");
            _input = new TextField { maxLength = ChatMessage.MaxLength };
            _input.AddToClassList("chat-input");
            inputRow.Add(_input);
            _input.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            UiKit.Tooltip(inputRow, () => UiKit.Colour("Press Enter to chat.", UiKit.Muted));

            VisualElement filters = UiKit.El(root, "chat-filters");
            foreach (Filter f in new[] { Filter.All, Filter.Game, Filter.Public })
            {
                Filter filter = f;
                Label button = UiKit.Text(filters, f.ToString(), "chat-filter");
                button.RegisterCallback<ClickEvent>(_ => SetFilter(filter));
                _filterButtons.Add((f, button));
            }
            SetFilter(Filter.All);
        }

        private void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                string text = _input.value;
                _input.value = "";
                if (!string.IsNullOrWhiteSpace(text)) Session.Say(text);
                _input.Blur();
                e.StopImmediatePropagation();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                _input.Blur();
                e.StopImmediatePropagation();
            }
        }

        /// <summary>Starts typing (Enter in the game).</summary>
        public void Focus()
        {
            if (Window == null) Ui.Open(this);
            Window?.Activate(this);
            _input.Focus();
        }

        private void SetFilter(Filter filter)
        {
            _filter = filter;
            foreach ((Filter f, Label button) in _filterButtons) button.EnableInClassList("chat-filter--on", f == filter);
            _scroll.contentContainer.Clear();
            foreach (GameSession.LogLine line in _lines) Show(line);
            ScrollToEnd();
        }

        public override void Observe()
        {
            // Keep every new line, shown or not, so switching filters or opening the chat later works.
            int total = Session.LogTotal;
            if (total == _seen) return;
            int missing = Mathf.Min(total - _seen, Session.Log.Count);
            for (int i = Session.Log.Count - missing; i < Session.Log.Count; i++)
            {
                GameSession.LogLine line = Session.Log[i];
                _lines.Add(line);
                Show(line);
            }
            _seen = total;
            while (_lines.Count > MaxLines) _lines.RemoveAt(0);
            while (_scroll.contentContainer.childCount > MaxLines) _scroll.contentContainer.RemoveAt(0);
            ScrollToEnd();
        }

        public override void Refresh()
        {
            _name.text = Session.PlayerName + ": ";
        }

        private bool Passes(GameSession.LogLine line) =>
            _filter == Filter.All || (_filter == Filter.Public) == (line.From != null);

        private void Show(GameSession.LogLine line)
        {
            if (!Passes(line)) return;
            Label label;
            if (line.From != null)
            {
                // Classic public chat: the name in black, what they said in blue.
                label = UiKit.Text(_scroll, "<color=#000000>" + Escape(line.From) + ":</color> <color=#0000b4>" + Escape(line.Text) + "</color>", "chat-line");
            }
            else
            {
                string text = line.Kind == TextKind.Assayer ? "The Assayer: " + line.Text : line.Text;
                label = UiKit.Text(_scroll, Escape(text), "chat-line");
                if (line.Kind == TextKind.Warning) label.AddToClassList("chat-line--warning");
                if (line.Kind == TextKind.Assayer) label.AddToClassList("chat-line--assayer");
            }
        }

        /// <summary>Stops players' text being read as rich-text tags.</summary>
        private static string Escape(string text) => text.Replace("<", "<noparse><</noparse>");

        private void ScrollToEnd() =>
            _scroll.schedule.Execute(() => _scroll.scrollOffset = new Vector2(0f, _scroll.contentContainer.layout.height)).StartingIn(10);
    }
}
