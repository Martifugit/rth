using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Stations with a draw (docs/skills.md, Smithing: the forge's draw), the active layer of the
    /// making skills that have one. A draw is the station's, shared by everyone: it burns down a
    /// tick at a time; pumping the bellows (the "bellows" action, repeated until it is full) or
    /// feeding it fuel (using a fuel item on it) builds it up, with a little XP. While it roars,
    /// every recipe made at that station is faster (<see cref="RecipeSystem.TicksFor"/>), so a busy
    /// smithy where people take turns at the bellows runs hot for all. Clients near a burning
    /// station get it as the "draw" server state.
    /// </summary>
    public sealed class StationSystem : GameSystem
    {
        /// <summary>One placed station's draw: ticks of burning left.</summary>
        private sealed class Draw
        {
            public double Left;
        }

        private sealed class SentDraws
        {
            public string Last = "";
        }

        private readonly Dictionary<PlacedObject, Draw> _draws = new Dictionary<PlacedObject, Draw>();

        protected override void OnAttach()
        {
            ObjectSystem objects = World.Get<ObjectSystem>();
            objects.RegisterAction(DrawDef.BellowsAction, (player, placed, def) =>
            {
                if (def.Station?.Draw == null)
                {
                    World.Log("Object '" + placed.Type + "' has a bellows action but no draw.");
                    return;
                }
                World.StartInteraction(player, new BellowsInteraction(placed));
            });
        }

        private DrawDef? DefOf(PlacedObject placed) =>
            World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def) ? def.Station?.Draw : null;

        private Draw DrawOf(PlacedObject placed)
        {
            if (!_draws.TryGetValue(placed, out Draw? draw)) _draws[placed] = draw = new Draw();
            return draw;
        }

        /// <summary>Ticks of burning left in a station's draw (0 for none).</summary>
        public double Left(PlacedObject placed) => _draws.TryGetValue(placed, out Draw? d) ? d.Left : 0;

        /// <summary>Whether the station's draw roars now.</summary>
        public bool IsHot(PlacedObject placed)
        {
            DrawDef? def = DefOf(placed);
            return def != null && Left(placed) > Math.Max(0, def.HotAbove);
        }

        /// <summary>What the draw does to a recipe's ticks at this station: its hot scale while it roars, else 1.</summary>
        public double DrawScale(PlacedObject placed) => IsHot(placed) ? DefOf(placed)!.HotTickScale : 1;

        public override void OnTimers()
        {
            foreach (KeyValuePair<PlacedObject, Draw> pair in _draws)
                if (pair.Value.Left > 0) pair.Value.Left = Math.Max(0, pair.Value.Left - 1);
        }

        /// <summary>One pump of the bellows. Returns false when the draw is already full (nothing more to do).</summary>
        public bool Pump(Player player, PlacedObject placed)
        {
            DrawDef? def = DefOf(placed);
            if (def == null || def.Bellows <= 0) return false;
            Draw draw = DrawOf(placed);
            if (draw.Left >= def.Max)
            {
                World.Tell(player, TextKind.Info, "The fire is roaring as hot as it will go.");
                return false;
            }
            draw.Left = Math.Min(def.Max, draw.Left + def.Bellows);
            World.Animate(player, "bellows", placed.Tile);
            World.Find<SkillSystem>()?.AddXp(player, def.Skill, def.Xp);
            return draw.Left < def.Max;
        }

        /// <summary>The fuel entry this item is for the station, or null.</summary>
        public DrawFuel? FuelFor(PlacedObject placed, ItemDef item) =>
            DefOf(placed)?.Fuel.FirstOrDefault(f => f.Item.StartsWith("tag:", StringComparison.Ordinal) ? item.Tags.Contains(f.Item.Substring(4)) : f.Item == item.Id);

        /// <summary>Feeds one of an item to the station's draw. Returns false if it isn't fuel here (or the fire is full).</summary>
        public bool Feed(Player player, PlacedObject placed, ItemDef item)
        {
            DrawDef? def = DefOf(placed);
            DrawFuel? fuel = FuelFor(placed, item);
            ItemSystem? items = World.Find<ItemSystem>();
            if (def == null || fuel == null || items == null) return false;
            Draw draw = DrawOf(placed);
            if (draw.Left + fuel.Ticks / 2.0 > def.Max)
            {
                World.Tell(player, TextKind.Info, "The fire can't take any more for now.");
                return true;
            }
            if (!items.Take(player, item.Id, 1)) return false;
            draw.Left = Math.Min(def.Max, draw.Left + fuel.Ticks);
            World.Animate(player, "work", placed.Tile);
            World.Find<SkillSystem>()?.AddXp(player, def.Skill, def.Xp);
            World.Tell(player, TextKind.Info, "You feed the " + item.Name.ToLowerInvariant() + " to the fire, and it draws hard.");
            return true;
        }

        public override void SendState(Player player)
        {
            var states = new List<DrawState>();
            foreach (KeyValuePair<PlacedObject, Draw> pair in _draws)
            {
                PlacedObject placed = pair.Key;
                DrawDef? def = DefOf(placed);
                if (def == null || pair.Value.Left <= 0 || !World.CanSee(player, placed.Plane, placed.Tile)) continue;
                states.Add(new DrawState
                {
                    Type = placed.Type, Plane = placed.Plane, X = placed.X, Y = placed.Y,
                    // Rounded so the state only changes (and is only sent) every few ticks.
                    Level = Math.Round(pair.Value.Left / def.Max * 20) / 20,
                    Hot = IsHot(placed),
                });
            }
            JsonStateMessage message = JsonStateMessage.Of(DrawDef.StateKey, states);
            SentDraws sent = player.Component<SentDraws>();
            if (message.Json == sent.Last) return;
            sent.Last = message.Json;
            player.Connection.Send(message);
        }
    }

    /// <summary>Walks to a station and works its bellows, a pump every few ticks, until the draw is full or the player stops.</summary>
    public sealed class BellowsInteraction : ObjectInteraction
    {
        private uint _next;

        public BellowsInteraction(PlacedObject station) : base(station) { }

        protected override bool Work(World world, Player player)
        {
            if (world.CurrentTick < _next) return true;
            DrawDef? def = world.Content.Objects.TryGetValue(Object.Type, out ObjectDef? o) ? o.Station?.Draw : null;
            if (def == null) return false;
            _next = world.CurrentTick + (uint)Math.Max(1, def.BellowsTicks);
            return world.Get<StationSystem>().Pump(player, Object);
        }
    }
}
