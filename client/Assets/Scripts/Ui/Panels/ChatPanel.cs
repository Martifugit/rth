using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Game messages and chat in one scrolling log, with a line to type in. Enter starts typing
    /// (while typing, keys don't trigger hotkeys), Enter again sends, Escape stops.
    /// </summary>
    public sealed class ChatPanel : UiPanel
    {
        private const int MaxLines = 150;

        public override string Id => "chat";
        public override string Title => "Chat";
        public override string DefaultWindow => UiDefaultLayout.Chat;
        public override Vector2 MinSize => new Vector2(240f, 100f);

        private ScrollView _scroll;
        private TextField _input;
        private int _seen;

        protected override void Build(VisualElement root)
        {
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("scroll");
            root.Add(_scroll);

            _input = new TextField { maxLength = ChatMessage.MaxLength };
            _input.AddToClassList("chat-input");
            root.Add(_input);
            _input.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
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

        public override void Observe()
        {
            // Lines arrive whether or not the chat is showing; catch up when it is.
            if (!IsVisible) return;
            int total = Session.LogTotal;
            if (total == _seen) return;
            int missing = Mathf.Min(total - _seen, Session.Log.Count);
            for (int i = Session.Log.Count - missing; i < Session.Log.Count; i++) AddLine(Session.Log[i]);
            _seen = total;
            while (_scroll.contentContainer.childCount > MaxLines) _scroll.contentContainer.RemoveAt(0);
            _scroll.schedule.Execute(() => _scroll.scrollOffset = new Vector2(0f, _scroll.contentContainer.layout.height));
        }

        private void AddLine(GameSession.LogLine line)
        {
            Label label;
            if (line.From != null)
            {
                label = UiKit.Text(_scroll, line.From + ": " + line.Text, "chat-line", "chat-line--public");
            }
            else
            {
                string text = line.Kind == TextKind.Assayer ? "The Assayer: " + line.Text : line.Text;
                label = UiKit.Text(_scroll, text, "chat-line");
                if (line.Kind == TextKind.Warning) label.AddToClassList("chat-line--warning");
                if (line.Kind == TextKind.Assayer) label.AddToClassList("chat-line--assayer");
            }
        }
    }
}
