using System.Collections.Generic;

namespace Rth.Shared.Net
{
    /// <summary>The kinds of <see cref="QueryMessage"/> the server answers.</summary>
    public static class QueryKinds
    {
        /// <summary>Arg: a player's entity id. Answer: a <see cref="PlayerInspect"/>.</summary>
        public const string Inspect = "inspect";
    }

    /// <summary>
    /// What anyone may see of a player when they inspect them: public information only (no pack,
    /// bank, blood or stats). Sent as JSON, so adding a field needs no codec change.
    /// </summary>
    public sealed class PlayerInspect
    {
        public int EntityId;
        public string Name = "";
        public int CombatLevel = 1;
        public int TotalLevel;
        /// <summary>Worn item ids by <see cref="Items.EquipSlot"/> index; "" for an empty slot.</summary>
        public List<string> Equipment = new List<string>();
        /// <summary>A few things worth showing (best skills, quests done), in order.</summary>
        public List<InspectLine> Highlights = new List<InspectLine>();
    }

    /// <summary>One highlight on an inspect card: "Occultism", "42", with an icon id.</summary>
    public sealed class InspectLine
    {
        public string Label = "";
        public string Value = "";
        public string Icon = "";
    }
}
