using System;
using System.Collections.Generic;
using Rth.Server.Persistence;

namespace Rth.Server.Game
{
    public sealed class PlayerPoints
    {
        public Dictionary<string, int> Pools = new Dictionary<string, int>();
        public bool Dirty = true;
    }

    /// <summary>
    /// Named point balances per player (yard tokens from the skinning yard, later guild favour,
    /// faction standing...). Earned from tasks, spent in shops whose currency is "points:&lt;pool&gt;".
    /// </summary>
    public sealed class PointsSystem : GameSystem
    {
        private const string SaveSection = "points";

        public PlayerPoints Of(Player player) => player.Component<PlayerPoints>();

        public int Balance(Player player, string pool) => Of(player).Pools.TryGetValue(pool, out int v) ? v : 0;

        public string PoolName(string pool) => World.Content.PointPools.TryGetValue(pool, out var def) ? def.Name : pool;

        public void Add(Player player, string pool, int amount)
        {
            if (amount == 0) return;
            PlayerPoints points = Of(player);
            points.Pools[pool] = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)Balance(player, pool) + amount));
            points.Dirty = true;
        }

        public bool TrySpend(Player player, string pool, int amount)
        {
            if (amount < 0 || Balance(player, pool) < amount) return false;
            Add(player, pool, -amount);
            return true;
        }

        protected override void OnAttach()
        {
            World.Get<ShopSystem>().RegisterCurrency("points:", new PointsCurrency(this));
            World.Get<DialogueSystem>().RegisterToken("points", (p, pool, _) => pool == null ? "" : Balance(p, pool).ToString());
            World.Get<DialogueSystem>().RegisterCondition("points", (p, c, _) => Balance(p, c.Id ?? "") >= c.Count);
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            Dictionary<string, int>? saved = save?.Section<Dictionary<string, int>>(SaveSection);
            if (saved != null) Of(player).Pools = saved;
            Of(player).Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player).Pools);

        private sealed class PointsCurrency : ICurrency
        {
            private readonly PointsSystem _points;

            public PointsCurrency(PointsSystem points)
            {
                _points = points;
            }

            private static string Pool(string currency) => currency.Substring("points:".Length);

            public string Name(string currency) => _points.PoolName(Pool(currency));
            public long Balance(Player player, string currency) => _points.Balance(player, Pool(currency));
            public bool TrySpend(Player player, string currency, int amount) => _points.TrySpend(player, Pool(currency), amount);
            public int Give(Player player, string currency, int amount)
            {
                _points.Add(player, Pool(currency), amount);
                return 0;
            }
        }
    }
}
