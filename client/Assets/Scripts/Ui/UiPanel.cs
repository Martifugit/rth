using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// One piece of the interface (the pack, the minimap, the chat...). Panels live in windows
    /// the player can move and combine as tabs; resizable ones can be resized. A panel only reads
    /// client state and sends intents through <see cref="GameSession"/>; it never decides outcomes.
    ///
    /// Panels register themselves: any concrete subclass with a parameterless constructor is
    /// found by <see cref="UiRoot"/> and placed by its <see cref="DefaultWindow"/> group. Adding
    /// an interface is adding a class. Bigger, occasional interfaces are <see cref="UiModal"/>s.
    /// </summary>
    public abstract class UiPanel
    {
        /// <summary>Stable id, used in saved layouts: "inventory".</summary>
        public abstract string Id { get; }

        /// <summary>Full name: "Backpack".</summary>
        public abstract string Title { get; }

        /// <summary>Icon id for its tab and title bar (see <see cref="IconLibrary"/>).</summary>
        public virtual string Icon => "menu";

        /// <summary>Which default window this panel starts in (see <see cref="UiDefaultLayout"/>).</summary>
        public virtual string DefaultWindow => Id;

        /// <summary>Order among panels sharing a default window (tab order).</summary>
        public virtual int Order => 0;

        /// <summary>Whether it is open in the default layout.</summary>
        public virtual bool OpenByDefault => true;

        /// <summary>Drawn without a window frame or title (bars, the minimap); moved by a small handle.</summary>
        public virtual bool Bare => false;

        /// <summary>
        /// A size the window always has (bars, chat, bank): it can be moved but not resized. Null
        /// means the player can resize it.
        /// </summary>
        public virtual Vector2? FixedSize => null;

        public virtual Vector2 MinSize => new Vector2(170f, 120f);

        /// <summary>Size of a window made just for this panel.</summary>
        public virtual Vector2 DefaultSize => FixedSize ?? new Vector2(260f, 320f);

        /// <summary>A key that opens it, brings it forward, or closes it again.</summary>
        public virtual Key Hotkey => Key.None;

        /// <summary>
        /// For windows the server opens (bank, shop, dialogue): whether it is open right now, and
        /// the interface follows. Null (the default) means the player opens and closes it.
        /// </summary>
        public virtual bool? ServerOpen => null;

        /// <summary>The player closed it (the X, a hotkey, the panel menu). Server windows tell the server here.</summary>
        public virtual void OnClosedByPlayer()
        {
        }

        /// <summary>
        /// Floating windows (the bank, a shop, the conversation) sit above the HUD, open centred and
        /// may overlap it; everything else is docked and never overlaps another docked window. By
        /// default the windows the server opens float.
        /// </summary>
        public virtual bool Floating => ServerOpen != null;

        /// <summary>
        /// Whether the panel's content scrolls when the window is smaller than it (the default for
        /// resizable panels). A panel that fills its window itself (a grid that resizes its slots,
        /// the chat, the minimap) says false and must fit whatever size it gets.
        /// </summary>
        public virtual bool Scrolls => !Bare && !FixedSize.HasValue;

        /// <summary>What <see cref="Build"/> fills: inside the scroll view when the panel scrolls.</summary>
        public VisualElement Root { get; private set; }

        /// <summary>What the window holds: <see cref="Root"/>, or the scroll view around it.</summary>
        public VisualElement Container { get; private set; }
        protected UiRoot Ui { get; private set; }
        protected GameSession Session => Ui.session;
        protected WorldView World => Ui.world;

        /// <summary>The window it is in, or null when closed.</summary>
        public UiWindow Window { get; internal set; }

        /// <summary>Open, and the tab showing in its window.</summary>
        public bool IsVisible => Window != null && Window.Active == this;

        internal void Create(UiRoot ui)
        {
            Ui = ui;
            Root = new VisualElement { name = Id };
            Root.style.flexGrow = 1;
            Container = Root;
            if (Scrolls)
            {
                // Content that doesn't fit scrolls; it never spills over the window or squashes.
                var scroll = new ScrollView(ScrollViewMode.Vertical) { name = Id + "-scroll" };
                scroll.AddToClassList("panel-scroll");
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.style.flexGrow = 1;
                Root.style.flexShrink = 0;
                scroll.Add(Root);
                Container = scroll;
            }
            Build(Root);
        }

        /// <summary>Makes the panel's elements, once.</summary>
        protected abstract void Build(VisualElement root);

        /// <summary>Brings the elements up to date with the session. Called every frame while visible.</summary>
        public virtual void Refresh()
        {
        }

        /// <summary>Called every frame in game, open or not: for panels that keep track of things (XP gained).</summary>
        public virtual void Observe()
        {
        }
    }
}
