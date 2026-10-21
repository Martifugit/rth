using Rth.Shared.Items;

namespace Rth.Client.Ui
{
    // What can be dragged between slots (see UiRoot.BeginDrag). Drop targets accept the kinds
    // they understand: a bar slot takes abilities, hymns, items and other bar slots; a pack slot takes
    // pack items; the bank takes pack and worn items.

    /// <summary>An item being dragged out of a container slot.</summary>
    public sealed class ItemDrag
    {
        public ContainerKind From;
        public int Slot;
        public string ItemId;
    }

    /// <summary>An ability being dragged from the ability book.</summary>
    public sealed class AbilityDrag
    {
        public string AbilityId;
    }

    /// <summary>A hymn being dragged from the religion tab.</summary>
    public sealed class HymnDrag
    {
        public string HymnId;
    }

    /// <summary>The contents of an action bar slot being dragged (to another slot, or off the bar).</summary>
    public sealed class BarSlotDrag
    {
        public int Bar;
        public int Slot;
    }
}
