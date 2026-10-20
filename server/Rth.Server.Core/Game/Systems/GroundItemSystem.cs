using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed class GroundItem
    {
        public int Id;
        public string ItemId = "";
        public int Count;
        public int Plane;
        public TileCoord Position;
        /// <summary>Account key of the only player who can see it until <see cref="PublicAtTick"/>; null if public from the start.</summary>
        public string? OwnerKey;
        public uint PublicAtTick;
        public uint DespawnAtTick;
        /// <summary>Bumped when the count changes, so viewers get the new stack.</summary>
        public int Version;
    }

    /// <summary>Which ground items (and which version of each) a player's client knows about.</summary>
    public sealed class KnownGroundItems
    {
        public readonly Dictionary<int, int> Versions = new Dictionary<int, int>();
    }

    /// <summary>
    /// Items lying on tiles: loot, dropped items, later anything a skill or event leaves around.
    /// New items belong to one player for a while, then anyone can see them, then they vanish.
    /// Adds the "drop" item action and the walk-over-and-pick-up interaction.
    /// </summary>
    public sealed class GroundItemSystem : GameSystem
    {
        private readonly Dictionary<int, GroundItem> _items = new Dictionary<int, GroundItem>();
        private int _nextId = 1;

        public IEnumerable<GroundItem> Items => _items.Values;

        public GroundItem? Find(int id) => _items.TryGetValue(id, out GroundItem? item) ? item : null;

        protected override void OnAttach()
        {
            World.OnMessage<PickupMessage>((player, m) =>
            {
                if (player.IsDead || Find(m.GroundItemId) == null) return;
                World.StartInteraction(player, new PickupInteraction(m.GroundItemId));
            });
            World.Get<ItemSystem>().RegisterAction(ItemActions.Drop, Drop);
        }

        /// <summary>
        /// Puts an item on a tile. With an owner, only they see it at first. Stackable items join
        /// an existing pile of the same item and owner on the same tile.
        /// </summary>
        public GroundItem Spawn(string itemId, int count, int plane, TileCoord tile, Player? owner)
        {
            uint now = World.CurrentTick;
            var settings = World.Content.World;
            string? ownerKey = owner?.Key;
            bool stackable = World.Content.Item(itemId)?.Stackable ?? false;

            if (stackable)
            {
                GroundItem? pile = _items.Values.FirstOrDefault(g =>
                    g.ItemId == itemId && g.Plane == plane && g.Position == tile && g.OwnerKey == ownerKey && now < g.PublicAtTick);
                if (pile != null)
                {
                    pile.Count += count;
                    pile.Version++;
                    pile.DespawnAtTick = now + (uint)settings.GroundItemDespawnTicks;
                    return pile;
                }
            }

            var item = new GroundItem
            {
                Id = _nextId++,
                ItemId = itemId,
                Count = count,
                Plane = plane,
                Position = tile,
                OwnerKey = ownerKey,
                PublicAtTick = ownerKey == null ? now : now + (uint)settings.GroundItemPrivateTicks,
                DespawnAtTick = now + (uint)settings.GroundItemDespawnTicks,
            };
            _items.Add(item.Id, item);
            return item;
        }

        public void Remove(GroundItem item) => _items.Remove(item.Id);

        public bool IsVisibleTo(GroundItem item, Player viewer) =>
            World.CanSee(viewer, item.Plane, item.Position) &&
            (item.OwnerKey == null || item.OwnerKey == viewer.Key || World.CurrentTick >= item.PublicAtTick);

        private void Drop(Player player, ItemUse use)
        {
            int count = use.Count;
            use.Container.Clear(use.Slot);
            Spawn(use.Def.Id, count, player.Plane, player.Position, player);
        }

        public override void OnZones()
        {
            uint now = World.CurrentTick;
            foreach (GroundItem item in _items.Values.Where(g => now >= g.DespawnAtTick).ToList())
                _items.Remove(item.Id);
        }

        public override void WriteUpdate(Player viewer, WorldUpdateMessage update, ISet<int> visibleEntityIds)
        {
            Dictionary<int, int> known = viewer.Component<KnownGroundItems>().Versions;
            var visible = new HashSet<int>();
            foreach (GroundItem item in _items.Values)
            {
                if (!IsVisibleTo(item, viewer)) continue;
                visible.Add(item.Id);
                if (known.TryGetValue(item.Id, out int version) && version == item.Version) continue;
                known[item.Id] = item.Version;
                update.GroundAdded.Add(new GroundItemAdded { Id = item.Id, ItemId = item.ItemId, Count = item.Count, Plane = item.Plane, Position = item.Position });
            }
            foreach (int id in known.Keys.ToList())
            {
                if (visible.Contains(id)) continue;
                known.Remove(id);
                update.GroundRemoved.Add(id);
            }
        }

        /// <summary>One tick of walking to a ground item and picking it up.</summary>
        internal bool TickPickup(Player player, int groundItemId)
        {
            GroundItem? item = Find(groundItemId);
            if (item == null || !IsVisibleTo(item, player)) return false;

            if (player.Position != item.Position || player.Plane != item.Plane)
            {
                if (player.Waypoints.Count == 0)
                {
                    World.SetPath(player, item.Position);
                    if (player.Waypoints.Count == 0) return false; // can't get there
                }
                World.ProcessMovement(player);
                if (player.Position != item.Position) return true;
            }

            ItemSystem items = World.Get<ItemSystem>();
            int left = items.Give(player, item.ItemId, item.Count);
            if (left == item.Count)
            {
                World.Tell(player, TextKind.Warning, "You don't have enough inventory space.");
                return false;
            }
            if (left == 0) _items.Remove(item.Id);
            else
            {
                item.Count = left;
                item.Version++;
            }
            return false;
        }
    }

    public sealed class PickupInteraction : Interaction
    {
        public PickupInteraction(int groundItemId)
        {
            GroundItemId = groundItemId;
        }

        public int GroundItemId { get; }

        public override bool Tick(World world, Player player) => world.Get<GroundItemSystem>().TickPickup(player, GroundItemId);
    }
}
