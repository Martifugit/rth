using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A conversation, in two parts as in the modern game. First the message: the speaker's
    /// portrait, their name, what they say, and a continue button (Space). Then, if there's
    /// anything to answer, the reply: "What would you like to say?" and your options as numbered
    /// buttons (number keys). The server opens and closes it; closing it ends the talk.
    /// </summary>
    public sealed class DialoguePanel : UiPanel
    {
        public override string Id => "dialogue";
        public override string Title => "Conversation";
        public override string Icon => "chat";
        public override string DefaultWindow => UiDefaultLayout.Dialogue;
        public override bool OpenByDefault => false;
        public override bool Bare => true;
        public override Vector2? FixedSize => new Vector2(600f, 230f);
        public override bool? ServerOpen => Session.Dialogue != null;
        public override void OnClosedByPlayer() => Session.CloseInterface();

        private static readonly Key[] Digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        private VisualElement _message;
        private Label _speaker;
        private Label _text;
        private VisualElement _reply;
        private VisualElement _options;
        private DialogueMessage _shown;
        private bool _replying;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            // ---- the message: portrait overlapping the left edge, name, words, continue.
            _message = UiKit.El(root);
            _message.style.flexGrow = 1;
            _message.pickingMode = PickingMode.Ignore;
            VisualElement box = UiKit.El(_message, "window");
            box.style.position = Position.Absolute;
            box.style.left = 50f;
            box.style.right = 0f;
            box.style.top = 40f;
            box.style.bottom = 20f;
            VisualElement bar = UiKit.El(box, "window__titlebar");
            bar.style.justifyContent = Justify.Center;
            _speaker = UiKit.Text(bar, "", "window__title");
            _speaker.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speaker.style.flexGrow = 1;
            _speaker.style.paddingLeft = 60f;
            VisualElement close = UiKit.El(bar, "window__button");
            UiKit.SetIcon(close, IconLibrary.Get("close"));
            close.RegisterCallback<ClickEvent>(_ => Ui.Close(this));
            VisualElement body = UiKit.El(box);
            body.style.flexGrow = 1;
            body.style.paddingLeft = 90f;
            body.style.paddingRight = 20f;
            body.style.justifyContent = Justify.Center;
            _text = UiKit.Text(body, "", "prose");
            _text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _text.style.fontSize = 15;
            Label next = UiKit.Text(box, "›", "button");
            next.style.position = Position.Absolute;
            next.style.bottom = -14f;
            next.style.left = Length.Percent(50f);
            next.style.width = 80f;
            next.style.marginLeft = -40f + 45f;
            next.style.fontSize = 18;
            next.style.paddingTop = next.style.paddingBottom = 0f;
            next.style.backgroundImage = Resources.Load<Texture2D>("UI/Skin/button-gold");
            next.style.color = new Color(0.2f, 0.12f, 0.04f);
            next.RegisterCallback<ClickEvent>(_ => Continue());
            UiKit.Tooltip(next, () => "Continue (Space)");

            VisualElement portrait = UiKit.El(_message);
            portrait.style.position = Position.Absolute;
            portrait.style.left = 0f;
            portrait.style.top = 22f;
            portrait.style.width = portrait.style.height = 124f;
            portrait.style.borderTopLeftRadius = portrait.style.borderTopRightRadius = portrait.style.borderBottomLeftRadius = portrait.style.borderBottomRightRadius = 62f;
            portrait.style.borderTopWidth = portrait.style.borderBottomWidth = portrait.style.borderLeftWidth = portrait.style.borderRightWidth = 3f;
            portrait.style.borderTopColor = portrait.style.borderLeftColor = new Color(0.82f, 0.68f, 0.42f);
            portrait.style.borderBottomColor = portrait.style.borderRightColor = new Color(0.4f, 0.31f, 0.18f);
            portrait.style.backgroundColor = new Color(0.08f, 0.07f, 0.06f);
            portrait.style.alignItems = Align.Center;
            portrait.style.justifyContent = Justify.Center;
            UiKit.Icon(portrait, IconLibrary.Get("character"), 72f).style.unityBackgroundImageTintColor = new Color(0.55f, 0.5f, 0.42f);

            // ---- the reply: a question and numbered answers.
            _reply = UiKit.El(root, "window");
            _reply.style.position = Position.Relative;
            _reply.style.flexGrow = 1;
            _reply.style.marginTop = 20f;
            VisualElement replyBar = UiKit.El(_reply, "window__titlebar");
            Label question = UiKit.Text(replyBar, "WHAT WOULD YOU LIKE TO SAY?", "window__title");
            question.style.unityTextAlign = TextAnchor.MiddleCenter;
            question.style.paddingLeft = 26f;
            VisualElement replyClose = UiKit.El(replyBar, "window__button");
            UiKit.SetIcon(replyClose, IconLibrary.Get("close"));
            replyClose.RegisterCallback<ClickEvent>(_ => Ui.Close(this));
            _options = UiKit.Scroll(_reply);
            _options.style.paddingTop = 8f;
        }

        public override void Refresh()
        {
            DialogueMessage line = Session.Dialogue;
            if (line == null) return;
            if (line != _shown)
            {
                _shown = line;
                _replying = false;
                _speaker.text = line.Speaker.ToUpperInvariant();
                _text.text = line.Text;
                _options.Clear();
                for (int i = 0; i < line.Options.Count; i++)
                {
                    int index = i;
                    VisualElement option = UiKit.El(_options, "button", "row");
                    option.style.alignSelf = Align.Center;
                    option.style.width = 380f;
                    option.style.marginBottom = 4f;
                    UiKit.Text(option, (i + 1) + ".", "muted").style.width = 24f;
                    Label text = UiKit.Text(option, line.Options[i]);
                    text.style.flexGrow = 1;
                    text.style.unityTextAlign = TextAnchor.MiddleCenter;
                    text.style.marginRight = 24f;
                    option.RegisterCallback<ClickEvent>(_ => Session.ChooseDialogue(index));
                }
            }
            _message.style.display = _replying ? DisplayStyle.None : DisplayStyle.Flex;
            _reply.style.display = _replying ? DisplayStyle.Flex : DisplayStyle.None;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || UiRoot.IsTyping || UiRoot.IsModalOpen) return;
            if (!_replying && keyboard.spaceKey.wasPressedThisFrame) Continue();
            else if (_replying)
                for (int i = 0; i < Digits.Length && i < line.Options.Count; i++)
                    if (keyboard[Digits[i]].wasPressedThisFrame) Session.ChooseDialogue(i);
        }

        /// <summary>Past the message: to the answers if there are any, else on to the next line.</summary>
        private void Continue()
        {
            DialogueMessage line = Session.Dialogue;
            if (line == null) return;
            if (line.Options.Count > 0) _replying = true;
            else Session.ChooseDialogue(-1);
        }
    }
}
