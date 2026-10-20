using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// A large, occasional interface (the journal, the guide, the world map, settings): centred,
    /// a fixed size, with tabs of related pages. One is open at a time; Escape or a click outside
    /// closes it. Modals register themselves like panels; each lists its tabs.
    /// </summary>
    public abstract class UiModal
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public virtual string Icon => "journal";
        public virtual Vector2 Size => new Vector2(860f, 560f);
        public virtual Key Hotkey => Key.None;

        /// <summary>The pages, in tab order. Built once, when the modal is first opened.</summary>
        protected abstract IEnumerable<ModalTab> CreateTabs();

        public IReadOnlyList<ModalTab> Tabs => _tabs;
        public ModalTab Active { get; private set; }
        public VisualElement Root { get; private set; }

        protected UiRoot Ui { get; private set; }
        protected GameSession Session => Ui.session;

        private readonly List<ModalTab> _tabs = new List<ModalTab>();
        private readonly Dictionary<ModalTab, VisualElement> _tabButtons = new Dictionary<ModalTab, VisualElement>();
        private VisualElement _body;

        internal void Create(UiRoot ui)
        {
            Ui = ui;
            Root = new VisualElement();
            Root.AddToClassList("modal");
            Root.style.width = Size.x;
            Root.style.height = Size.y;

            VisualElement titleBar = UiKit.El(Root, "window__titlebar");
            titleBar.style.height = 36f;
            UiKit.Icon(titleBar, IconLibrary.Get(Icon), 24f, "window__title-icon");
            Label title = UiKit.Text(titleBar, Title.ToUpperInvariant(), "window__title");
            title.style.fontSize = 17;
            VisualElement close = UiKit.El(titleBar, "window__button");
            UiKit.SetIcon(close, IconLibrary.Get("close"));
            close.RegisterCallback<ClickEvent>(_ => Ui.CloseModal());
            UiKit.Tooltip(close, () => "Close (Esc)");

            VisualElement tabRow = UiKit.El(Root, "modal__tabs");
            _body = UiKit.El(Root, "modal__body");

            foreach (ModalTab tab in CreateTabs())
            {
                ModalTab t = tab;
                t.Create(this, ui);
                _tabs.Add(t);
                VisualElement button = UiKit.El(tabRow, "modal-tab");
                UiKit.Icon(button, IconLibrary.Get(t.Icon), 20f, "modal-tab__icon");
                UiKit.Text(button, t.Title.ToUpperInvariant(), "modal-tab__label");
                button.RegisterCallback<ClickEvent>(_ => Show(t));
                _tabButtons[t] = button;
            }
        }

        /// <summary>Shows a tab (by id, or the first), passing it something to focus on (a quest, a skill).</summary>
        public void Show(string tabId, string focus = null)
        {
            ModalTab tab = _tabs.Find(t => t.Id == tabId) ?? (Active ?? (_tabs.Count > 0 ? _tabs[0] : null));
            if (tab != null) Show(tab, focus);
        }

        private void Show(ModalTab tab, string focus = null)
        {
            if (Active != tab)
            {
                Active?.Root.RemoveFromHierarchy();
                Active = tab;
                _body.Add(tab.Root);
                foreach (KeyValuePair<ModalTab, VisualElement> b in _tabButtons) b.Value.EnableInClassList("modal-tab--active", b.Key == tab);
            }
            tab.OnShow(focus);
        }

        public void Refresh() => Active?.Refresh();
    }

    /// <summary>One page of a <see cref="UiModal"/>.</summary>
    public abstract class ModalTab
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public virtual string Icon => "journal";

        public VisualElement Root { get; private set; }
        protected UiRoot Ui { get; private set; }
        protected UiModal Modal { get; private set; }
        protected GameSession Session => Ui.session;

        internal void Create(UiModal modal, UiRoot ui)
        {
            Modal = modal;
            Ui = ui;
            Root = new VisualElement();
            Root.style.flexGrow = 1;
            Build(Root);
        }

        protected abstract void Build(VisualElement root);

        /// <summary>Shown, perhaps asked to focus on something ("dreamless", "occultism").</summary>
        public virtual void OnShow(string focus)
        {
        }

        /// <summary>Called every frame while showing.</summary>
        public virtual void Refresh()
        {
        }
    }
}
