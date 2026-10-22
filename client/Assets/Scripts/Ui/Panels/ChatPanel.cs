using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The chatbox, a fixed size: filter tabs (all, game, public), game messages and chat, and the
    /// line you type on. Enter starts typing (keys stop being hotkeys), Enter sends, Escape stops.
    /// New channels (private, group) become more filters.
    /// </summary>
    public sealed class ChatPanel : UiPanel
    {
        private const int MaxLines = 200;

        public override string Id => "chat";
        public override string Title => "Chat";
        public override string Icon => "chat";
        public override string DefaultWindow => UiDefaultLayout.Chat;
        public override Vector2? FixedSize => new Vector2(480f, 230f);

        private enum Filter
        {
            All,
            Game,
            Public,
        }

        private ScrollView _scroll;
        private TextField _input;
        private Label _name;
        private Label _hint;
        private int _focusedFrame = -10;
        private readonly List<GameSession.LogLine> _lines = new List<GameSession.LogLine>();
        private readonly List<(Filter Filter, Label Button)> _filterButtons = new List<(Filter, Label)>();
        private Filter _filter = Filter.All;
        private int _seen;

        protected override void Build(VisualElement root)
        {
            VisualElement filters = UiKit.El(root, "chat-tabs");
            foreach (Filter f in new[] { Filter.All, Filter.Game, Filter.Public })
            {
                Filter filter = f;
                Label button = UiKit.Text(filters, f.ToString().ToUpperInvariant(), "chat-tab");
                button.RegisterCallback<ClickEvent>(_ => SetFilter(filter));
                _filterButtons.Add((f, button));
            }

            _scroll = UiKit.Scroll(root);
            _scroll.style.flexShrink = 1;

            VisualElement inputRow = UiKit.El(root, "chat-input-row");
            _name = UiKit.Text(inputRow, "", "bold", "chat-name");
            _input = new TextField { maxLength = ChatMessage.MaxLength };
            _input.AddToClassList("chat-input");
            inputRow.Add(_input);
            _hint = UiKit.Text(_input, "Enter to chat", "muted", "chat-hint");
            _hint.pickingMode = PickingMode.Ignore;
            _input.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            SetFilter(Filter.All);
        }

        private void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                // The Enter that opened the chat arrives here too; it isn't a send.
                if (Time.frameCount - _focusedFrame <= 1)
                {
                    e.StopImmediatePropagation();
                    return;
                }
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
            _focusedFrame = Time.frameCount;
            _input.Focus();
        }

        private void SetFilter(Filter filter)
        {
            _filter = filter;
            foreach ((Filter f, Label button) in _filterButtons) button.EnableInClassList("chat-tab--on", f == filter);
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
            _name.text = Session.PlayerName + ":";
            _hint.style.display = string.IsNullOrEmpty(_input.value) && !UiRoot.IsTyping ? DisplayStyle.Flex : DisplayStyle.None;
            _input.Q(className: "unity-text-field__input")?.SetEnabled(true);
        }

        private bool Passes(GameSession.LogLine line) =>
            _filter == Filter.All || (_filter == Filter.Public) == (line.From != null);

        private void Show(GameSession.LogLine line)
        {
            if (!Passes(line)) return;
            if (line.From != null)
            {
                UiKit.Text(_scroll, UiKit.Colour(Escape(line.From) + ":", UiKit.HeadingColour) + " " + UiKit.Colour(Escape(line.Text), new Color(0.6f, 0.8f, 1f)), "chat-line");
                return;
            }
            // The ferryman's remark on a death is narration with his words quoted ("Dark water, a
            // jetty... \"First time.\""), so it goes in as written, in its own colour.
            Label label = UiKit.Text(_scroll, Escape(line.Text), "chat-line");
            if (line.Kind == TextKind.Warning) label.AddToClassList("chat-line--warning");
            if (line.Kind == TextKind.Ferryman) label.AddToClassList("chat-line--ferryman");
        }

        /// <summary>Stops players' text being read as rich-text tags.</summary>
        private static string Escape(string text) => text.Replace("<", "<noparse><</noparse>");

        private void ScrollToEnd() =>
            _scroll.schedule.Execute(() => _scroll.scrollOffset = new Vector2(0f, _scroll.contentContainer.layout.height)).StartingIn(10);
    }
}
