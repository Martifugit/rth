using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The conversation box: who is speaking, what they say, and your answers (number keys) or
    /// "click to continue" (space). The server opens and closes it; closing it ends the talk.
    /// </summary>
    public sealed class DialoguePanel : UiPanel
    {
        public override string Id => "dialogue";
        public override string Title => "Conversation";
        public override string Icon => "chat";
        public override string DefaultWindow => UiDefaultLayout.Dialogue;
        public override bool OpenByDefault => false;
        public override Vector2 MinSize => new Vector2(360f, 130f);
        public override bool? ServerOpen => Session.Dialogue != null;
        public override void OnClosedByPlayer() => Session.CloseInterface();

        private Label _speaker;
        private Label _text;
        private VisualElement _options;
        private DialogueMessage _shown;

        protected override void Build(VisualElement root)
        {
            VisualElement row = UiKit.El(root, "row");
            row.style.alignItems = Align.FlexStart;
            row.style.flexGrow = 1;

            VisualElement portrait = UiKit.El(row, "socket");
            portrait.style.width = portrait.style.height = 60f;
            portrait.style.marginRight = 8f;
            UiKit.Icon(portrait, IconLibrary.Get("chat"), 48f);

            VisualElement speech = UiKit.El(row);
            speech.style.flexGrow = 1;
            speech.style.flexShrink = 1;
            _speaker = UiKit.Text(speech, "", "heading");
            _speaker.style.marginTop = 0f;
            _text = UiKit.Text(speech, "");
            _text.style.whiteSpace = WhiteSpace.Normal;
            _text.style.marginBottom = 6f;
            _options = UiKit.El(speech);
        }

        public override void Refresh()
        {
            DialogueMessage line = Session.Dialogue;
            if (line == null || line == _shown) return;
            _shown = line;
            _speaker.text = line.Speaker;
            _text.text = line.Text;
            _options.Clear();
            if (line.Options.Count == 0)
            {
                Option("Click here to continue", UiKit.Colour("(space)", UiKit.Muted), () => Session.ChooseDialogue(-1));
                return;
            }
            for (int i = 0; i < line.Options.Count; i++)
            {
                int index = i;
                Option((i + 1) + ". " + line.Options[i], null, () => Session.ChooseDialogue(index));
            }
        }

        private void Option(string text, string hint, System.Action onClick)
        {
            Label option = UiKit.Text(_options, text + (hint != null ? "  " + hint : ""));
            option.style.color = new Color(0.45f, 0.75f, 1f);
            option.style.paddingTop = option.style.paddingBottom = 2f;
            option.style.paddingLeft = 4f;
            option.style.whiteSpace = WhiteSpace.Normal;
            option.RegisterCallback<PointerEnterEvent>(_ =>
            {
                option.style.color = Color.white;
                option.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            });
            option.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                option.style.color = new Color(0.45f, 0.75f, 1f);
                option.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            });
            option.RegisterCallback<ClickEvent>(_ => onClick());
        }
    }
}
