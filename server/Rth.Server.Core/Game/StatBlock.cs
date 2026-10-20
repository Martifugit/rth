using System.Collections.Generic;

namespace Rth.Server.Game
{
    /// <summary>A player's current stat totals, rebuilt by <see cref="World.RecalculateStats"/> from every contributor.</summary>
    public sealed class StatBlock
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();

        public int Get(string stat) => _values.TryGetValue(stat, out int v) ? v : 0;

        public void Add(string stat, int amount)
        {
            if (amount == 0) return;
            _values[stat] = Get(stat) + amount;
        }

        public IReadOnlyDictionary<string, int> Values => _values;

        internal void Clear() => _values.Clear();
    }

    /// <summary>
    /// A system that adds to player stats (base values, gear, effects, later skills and prayers).
    /// Call <see cref="World.RecalculateStats"/> whenever what it contributes changes.
    /// </summary>
    public interface IStatContributor
    {
        void Contribute(Player player, StatBlock stats);
    }
}
