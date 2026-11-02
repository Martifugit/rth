using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Tile triggers: placed objects whose type has a <see cref="TriggerDef"/> (grasping hands, a
    /// snare). A player who steps onto one's tile is held in place (<see cref="Entity.RootedUntilTick"/>),
    /// hurt by a share of their max blood and told why; it then re-arms for them after
    /// <see cref="TriggerDef.RearmTicks"/>. Sprung triggers near a player are sent as the "triggers"
    /// state, so a hidden one shows only while it holds someone. Publishes <see cref="TriggerSprung"/>.
    /// </summary>
    public sealed class TriggerSystem : GameSystem
    {
        public const string StateKey = "triggers";

        private readonly Dictionary<(int Plane, int X, int Y), PlacedObject> _byTile = new Dictionary<(int, int, int), PlacedObject>();
        private readonly List<SprungTrigger> _sprung = new List<SprungTrigger>();
        private int _indexedCount = -1;

        /// <summary>When each trigger may next spring on this player.</summary>
        private sealed class Rearm
        {
            public readonly Dictionary<PlacedObject, uint> Next = new Dictionary<PlacedObject, uint>();
            public bool SawAny;
        }

        private ObjectSystem Objects => World.Get<ObjectSystem>();

        public override void OnContentChanged() => _indexedCount = -1;

        private void Index()
        {
            if (_indexedCount == Objects.Placed.Count) return;
            _indexedCount = Objects.Placed.Count;
            _byTile.Clear();
            foreach (PlacedObject placed in Objects.Placed)
                if (World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def) && def.Trigger != null)
                    _byTile[(placed.Plane, placed.X, placed.Y)] = placed;
        }

        /// <summary>The trigger on a tile, if any.</summary>
        public PlacedObject? At(int plane, TileCoord tile)
        {
            Index();
            return _byTile.TryGetValue((plane, tile.X, tile.Y), out PlacedObject? placed) ? placed : null;
        }

        public override void AfterPlayer(Player player)
        {
            if (player.IsDead) return;
            Index();
            if (_byTile.Count == 0) return;
            // Every tile stepped on this tick, so running doesn't jump a trigger.
            foreach (TileCoord tile in player.StepsThisTick.ToList())
            {
                PlacedObject? placed = At(player.Plane, tile);
                if (placed != null && Spring(player, placed)) return;
            }
        }

        /// <summary>Springs a trigger on a player if it's armed for them. Returns whether it did.</summary>
        public bool Spring(Player player, PlacedObject placed)
        {
            if (!World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def) || def.Trigger == null) return false;
            TriggerDef trigger = def.Trigger;
            uint now = World.CurrentTick;
            Rearm rearm = player.Component<Rearm>();
            if (rearm.Next.TryGetValue(placed, out uint next) && now < next) return false;
            rearm.Next[placed] = now + (uint)trigger.RearmTicks;

            if (trigger.HoldTicks > 0)
            {
                player.RootedUntilTick = now + (uint)trigger.HoldTicks;
                // Held on this tile: a step further on this tick is undone, and walked again once free.
                if (player.Position != placed.Tile)
                {
                    var rest = player.Waypoints.ToList();
                    player.Waypoints.Clear();
                    player.Waypoints.Enqueue(player.Position);
                    foreach (TileCoord step in rest) player.Waypoints.Enqueue(step);
                    player.Position = placed.Tile;
                    player.StepsThisTick.Add(placed.Tile);
                }
            }
            if (trigger.Animation != null) World.Animate(player, trigger.Animation);
            if (trigger.Message != null) World.Tell(player, TextKind.Warning, trigger.Message);
            if (trigger.DamagePercent > 0)
            {
                int amount = System.Math.Max(1, (int)System.Math.Round(player.MaxBlood * trigger.DamagePercent / 100.0));
                World.Get<CombatSystem>().Damage(player, amount, null, HitKind.Bleed);
            }
            _sprung.Add(new SprungTrigger { Type = placed.Type, Plane = placed.Plane, X = placed.X, Y = placed.Y, Tick = now });
            World.Events.Publish(new TriggerSprung(player, placed));
            return true;
        }

        public override void SendState(Player viewer)
        {
            uint now = World.CurrentTick;
            var state = new TriggersState();
            foreach (SprungTrigger s in _sprung)
            {
                int show = World.Content.Objects.TryGetValue(s.Type, out ObjectDef? def) && def.Trigger != null ? def.Trigger.ShowTicks : 4;
                if (now < s.Tick + (uint)show && World.CanSee(viewer, s.Plane, new TileCoord(s.X, s.Y))) state.Sprung.Add(s);
            }
            // Sent while anything is sprung nearby, and once more when it all settles.
            Rearm seen = viewer.Component<Rearm>();
            if (state.Sprung.Count == 0 && !seen.SawAny) return;
            seen.SawAny = state.Sprung.Count > 0;
            viewer.Connection.Send(JsonStateMessage.Of(StateKey, state));
        }

        public override void OnCleanup()
        {
            uint now = World.CurrentTick;
            _sprung.RemoveAll(s => now >= s.Tick + 20);
        }
    }
}
