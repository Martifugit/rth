using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// One piece of the interface (the pack, the minimap, the chat...). Panels live in windows
    /// the player can move, resize, combine as tabs and split apart. A panel only reads client
    /// state and sends intents through <see cref="GameSession"/>; it never decides outcomes.
    ///
    /// Panels register themselves: any concrete subclass with a parameterless constructor is
    /// found by <see cref="UiRoot"/> and placed by its <see cref="DefaultWindow"/> group. Adding
    /// an interface is adding a class.
    /// </summary>
    public abstract class UiPanel
    {
        /// <summary>Stable id, used in saved layouts: "inventory".</summary>
        public abstract string Id { get; }

        /// <summary>Full name: "Backpack".</summary>
        public abstract string Title { get; }

        /// <summary>Short label for a tab strip: "Pack".</summary>
        public virtual string Tab => Title;

        /// <summary>Which default window this panel starts in (see <see cref="UiDefaultLayout"/>).</summary>
        public virtual string DefaultWindow => Id;

        /// <summary>Order among panels sharing a default window (tab order).</summary>
        public virtual int Order => 0;

        /// <summary>Whether it is open in the default layout. Big windows (the world map) are not.</summary>
        public virtual bool OpenByDefault => true;

        /// <summary>Drawn without a frame or title (bars, the minimap); moved by a small handle.</summary>
        public virtual bool Bare => false;

        public virtual Vector2 MinSize => new Vector2(170f, 120f);

        /// <summary>Size of a window made just for this panel.</summary>
        public virtual Vector2 DefaultSize => new Vector2(260f, 320f);

        /// <summary>A key that opens it, brings it forward, or closes it again.</summary>
        public virtual Key Hotkey => Key.None;

        public VisualElement Root { get; private set; }
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
