using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// The gathering skills' own ways of working a node (docs/skills.md 7), on top of the one node
    /// mechanism: catches by level (a fishing spot's perch and pike), nodes that come and go instead
    /// of running out (fishing spots, each on its own timer, bubbling before some returns: reading
    /// the water, Fishing's active layer), traps left to fill (eels), and tending (Woodcutting's
    /// active layer: one tree of a cluster at a time needs its stool tended, and then gives more,
    /// for everyone). Clients see the comings and goings, bubbles, traps and tending as the
    /// "gathering" server state, and reading's stacks as a status.
    /// </summary>
    public sealed partial class GatheringSystem : IStatusSource
    {
        public sealed partial class Node
        {
            /// <summary>A node that comes and goes and isn't here now (unseen, unworkable unless bubbling).</summary>
            public bool Dormant;
            /// <summary>When it next comes or goes (0: not started yet), and when it starts bubbling before it comes (0: it won't).</summary>
            public uint NextChange;
            public uint BubbleFrom;

            public bool IsBubbling(uint now) => Dormant && BubbleFrom != 0 && now >= BubbleFrom;
            /// <summary>Who has a trap set on it, and when it was set.</summary>
            public string? TrapOwner;
            public uint TrapSetAt;
            /// <summary>Tending: until when it needs it, and until when it has had it.</summary>
            public uint NeedsTendingUntil;
            public uint TendedUntil;

            public bool NeedsTending(uint now) => now < NeedsTendingUntil;
            public bool IsTended(uint now) => now < TendedUntil;
        }

        /// <summary>Players with reading stacks (Fish whisperer), for their decay and status.</summary>
        private readonly HashSet<Player> _readers = new HashSet<Player>();

        /// <summary>A player's reading-the-water stacks, by status.</summary>
        public sealed class Reading
        {
            public readonly Dictionary<string, (int Stacks, uint NextDecay, NodeReadingDef Def)> Stacks = new Dictionary<string, (int, uint, NodeReadingDef)>();
        }
        private readonly Dictionary<int, uint> _nextTending = new Dictionary<int, uint>();

        private sealed class SentGathering
        {
            public string Last = "";
        }

        private void AttachSkilling()
        {
            World.Get<ObjectSystem>().RegisterAction(NodeTendingDef.Action, (player, placed, def) =>
                World.StartInteraction(player, new ObjectOnceInteraction(placed, p => Tend(p, placed))));
        }

        /// <summary>The radius a node type's nodes are grouped by: its surge's or tending's.</summary>
        private static int GroupRadius(NodeDef def) => def.Surge?.GroupRadius ?? def.Tending?.GroupRadius ?? 0;

        private void SkillingTimers(uint now)
        {
            ComingsAndGoings(now);
            DecayReading(now);
            foreach (IGrouping<int, Node> group in _nodes.Where(n => n.Group >= 0).GroupBy(n => n.Group))
            {
                NodeDef? def = Def(group.First());
                if (def?.Tending is NodeTendingDef tending)
                {
                    if (!_nextTending.TryGetValue(group.Key, out uint at)) _nextTending[group.Key] = at = now + (uint)World.Random.Next(tending.EveryTicks / 2, tending.EveryTicks + 1);
                    if (now < at) continue;
                    _nextTending[group.Key] = now + (uint)Math.Max(1, tending.EveryTicks);
                    // One at a time: not while another of the cluster still waits.
                    if (group.Any(n => n.NeedsTending(now))) continue;
                    List<Node> candidates = group.Where(n => !n.IsDepleted(now) && !n.Dormant && !n.IsTended(now)).ToList();
                    if (candidates.Count == 0) continue;
                    Node chosen = candidates[World.Random.Next(candidates.Count)];
                    chosen.NeedsTendingUntil = now + (uint)Math.Max(1, tending.NeedTicks);
                    if (tending.NeedMessage != null)
                        foreach (Player p in World.Players)
                            if (World.CanSee(p, chosen.Placed.Plane, chosen.Placed.Tile))
                                World.Tell(p, TextKind.Info, tending.NeedMessage.Replace("{where}", Where(p.Position, chosen.Placed.Tile)));
                }
            }
        }

        // ------------------------------------------------------------------ coming and going, reading the water

        /// <summary>Each node that comes and goes, on its own timer: it starts at random in its cycle.</summary>
        private void ComingsAndGoings(uint now)
        {
            foreach (Node node in _nodes)
            {
                NodeRoamDef? roam = Def(node)?.Roam;
                if (roam == null) continue;
                if (node.NextChange == 0)
                {
                    double stay = (roam.MinTicks + roam.MaxTicks) / 2.0, away = (roam.AwayMinTicks + roam.AwayMaxTicks) / 2.0;
                    if (World.Random.NextDouble() < away / (stay + away)) GoAway(node, roam, now, quietly: true);
                    else node.NextChange = now + (uint)World.Random.Next(1, roam.MaxTicks + 1);
                    continue;
                }
                if (now < node.NextChange) continue;
                if (node.Dormant) ComeBack(node, roam, now);
                else GoAway(node, roam, now);
            }
        }

        private static uint Between(Random random, int min, int max) => (uint)random.Next(Math.Max(1, min), Math.Max(min, max) + 1);

        private void GoAway(Node node, NodeRoamDef roam, uint now, bool quietly = false)
        {
            node.Dormant = true;
            node.YieldsLeft = 0;
            node.SurgingUntil = 0;
            node.NextChange = now + Between(World.Random, roam.AwayMinTicks, roam.AwayMaxTicks);
            node.BubbleFrom = World.Random.NextDouble() < roam.BubbleChance ? node.NextChange - (uint)roam.BubbleTicks : 0;
            if (quietly) return;
            foreach (Player p in World.Players)
                if (p.Interaction is GatherInteraction g && ReferenceEquals(g.Object, node.Placed))
                    World.Tell(p, TextKind.Info, roam.Message);
        }

        /// <summary>It comes back: whoever cast into its bubbles is there first, and reads the water.</summary>
        private void ComeBack(Node node, NodeRoamDef roam, uint now)
        {
            node.Dormant = false;
            node.BubbleFrom = 0;
            node.NextChange = now + Between(World.Random, roam.MinTicks, roam.MaxTicks);
            if (roam.Reading == null) return;
            foreach (Player p in World.Players)
                if (p.Interaction is GatherInteraction g && ReferenceEquals(g.Object, node.Placed)) AddReading(p, roam.Reading, now);
        }

        private void AddReading(Player player, NodeReadingDef reading, uint now)
        {
            Reading r = player.Component<Reading>();
            int had = r.Stacks.TryGetValue(reading.Status, out var was) ? was.Stacks : 0;
            r.Stacks[reading.Status] = (Math.Min(reading.MaxStacks, had + 1), now + (uint)reading.DecayTicks, reading);
            _readers.Add(player);
            World.Tell(player, TextKind.Info, reading.Message);
        }

        /// <summary>A stack wears off every decayTicks without a new one.</summary>
        private void DecayReading(uint now)
        {
            foreach (Player p in _readers.ToList())
            {
                Reading r = p.Component<Reading>();
                foreach (string status in r.Stacks.Keys.ToList())
                {
                    var (stacks, next, def) = r.Stacks[status];
                    if (now < next) continue;
                    if (stacks <= 1) r.Stacks.Remove(status);
                    else r.Stacks[status] = (stacks - 1, now + (uint)def.DecayTicks, def);
                }
                if (r.Stacks.Count == 0) _readers.Remove(p);
            }
        }

        /// <summary>The share more a player's reading stacks add to this node's catch chance (0: none).</summary>
        public double ReadingBonus(Player player, NodeDef def)
        {
            NodeReadingDef? reading = def.Roam?.Reading;
            if (reading == null || !_readers.Contains(player)) return 0;
            return player.Component<Reading>().Stacks.TryGetValue(reading.Status, out var s) ? s.Stacks * reading.PerStack : 0;
        }

        /// <summary>How many reading stacks the player has of a status.</summary>
        public int ReadingStacks(Player player, string status) =>
            _readers.Contains(player) && player.Component<Reading>().Stacks.TryGetValue(status, out var s) ? s.Stacks : 0;

        public void AddStatuses(Player player, List<StatusState> statuses)
        {
            if (!_readers.Contains(player)) return;
            foreach (var pair in player.Component<Reading>().Stacks)
                statuses.Add(new StatusState { Id = pair.Key, Magnitude = (int)Math.Round(pair.Value.Stacks * pair.Value.Def.PerStack * 100), EndTick = pair.Value.NextDecay });
        }

        /// <summary>A node that comes and goes and was used up goes at once. Returns false if it doesn't come and go.</summary>
        private bool MoveUsedUp(Node node, NodeDef def)
        {
            if (def.Roam == null) return false;
            GoAway(node, def.Roam, World.CurrentTick);
            return true;
        }

        // ------------------------------------------------------------------ catches

        /// <summary>The catch a success gives at the player's level, picked by weight; null if the node has no catches.</summary>
        private NodeCatch? PickCatch(Player player, NodeDef def)
        {
            if (def.Catches.Count == 0) return null;
            int level = World.Find<SkillSystem>()?.Level(player, def.Skill) ?? 1;
            List<NodeCatch> eligible = def.Catches.Where(c => c.Level <= level).ToList();
            if (eligible.Count == 0) eligible = def.Catches.OrderBy(c => c.Level).Take(1).ToList();
            double roll = World.Random.NextDouble() * eligible.Sum(c => Math.Max(0, c.Weight));
            foreach (NodeCatch c in eligible)
            {
                roll -= Math.Max(0, c.Weight);
                if (roll < 0) return c;
            }
            return eligible[eligible.Count - 1];
        }

        // ------------------------------------------------------------------ tending

        private static readonly string[] Compass = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };

        /// <summary>Where a tile lies from a player, in words: "beside you", "to your north-east".</summary>
        public static string Where(TileCoord from, TileCoord to)
        {
            int dx = to.X - from.X, dy = to.Y - from.Y;
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 1) return "beside you";
            int octant = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
            return "to your " + Compass[(octant + 8) % 8];
        }

        /// <summary>Tends a node that needs it, for everyone working it.</summary>
        public void Tend(Player player, PlacedObject placed)
        {
            Node? node = NodeAt(placed);
            NodeDef? def = node == null ? null : Def(node);
            NodeTendingDef? tending = def?.Tending;
            if (node == null || def == null || tending == null) return;
            uint now = World.CurrentTick;
            if (!node.NeedsTending(now))
            {
                World.Tell(player, TextKind.Info, node.IsTended(now) ? "It has been tended, and is growing well." : "It doesn't need tending just now.");
                return;
            }
            node.NeedsTendingUntil = 0;
            node.TendedUntil = now + (uint)Math.Max(1, tending.TendedTicks);
            World.Animate(player, "work", placed.Tile);
            World.Find<SkillSystem>()?.AddXp(player, def.Skill, tending.Xp);
            World.Tell(player, TextKind.Info, tending.TendedMessage);
        }

        /// <summary>What tending adds to each attempt on this node now.</summary>
        private double TendingBonus(Node node, NodeDef def) => def.Tending != null && node.IsTended(World.CurrentTick) ? def.Tending.SuccessBonus : 0;

        /// <summary>The node's chance to be used up by a success now (tended trees are cut back less).</summary>
        private double DepleteChance(Node node, NodeDef def) =>
            def.DepleteChance * (def.Tending != null && node.IsTended(World.CurrentTick) ? def.Tending.DepleteScale : 1);

        // ------------------------------------------------------------------ traps

        private ItemDef? TrapItem(Player player, NodeTrapDef trap)
        {
            ItemSystem? items = World.Find<ItemSystem>();
            if (items == null) return null;
            return World.Content.Items.Values.FirstOrDefault(i => Matches(trap.Item, i) && items.CountOf(player, i.Id) > 0);
        }

        /// <summary>Sets the player's trap on the node, or collects it. Always a single act.</summary>
        internal void WorkTrap(Player player, Node node, NodeDef def)
        {
            NodeTrapDef trap = def.Trap!;
            ItemSystem? items = World.Find<ItemSystem>();
            if (items == null) return;
            uint now = World.CurrentTick;
            World.Animate(player, def.Animation, node.Placed.Tile);
            if (node.TrapOwner == null)
            {
                ItemDef? item = TrapItem(player, trap);
                if (item == null)
                {
                    World.Tell(player, TextKind.Warning, def.NoToolMessage ?? "You need a trap to set here.");
                    return;
                }
                items.Take(player, item.Id, 1);
                node.TrapOwner = player.Name;
                node.TrapSetAt = now;
                _trapItems[node] = item.Id;
                World.Tell(player, TextKind.Info, "You set your " + item.Name.ToLowerInvariant() + ". Come back when it has had time to fill.");
                return;
            }
            if (node.TrapOwner != player.Name)
            {
                World.Tell(player, TextKind.Info, "Someone else's trap is set here.");
                return;
            }
            int filled = Math.Min(Math.Max(1, trap.Max), (int)((now - node.TrapSetAt) / (uint)Math.Max(1, trap.Ticks)));
            if (filled == 0)
            {
                World.Tell(player, TextKind.Info, "Your trap is still empty. Give it time.");
                return;
            }
            List<string> gives = def.Catches.Count > 0 ? def.Catches.Select(c => c.Item).ToList() : def.Items.Select(i => i.Item).ToList();
            if (gives.Any(item => !items.CanGive(player, item, filled)))
            {
                World.Tell(player, TextKind.Warning, "You don't have room for what's in your trap. Leave it set and come back.");
                return;
            }
            int caught = 0;
            for (int i = 0; i < filled; i++)
            {
                if (World.Random.NextDouble() >= SuccessChance(player, def, null)) continue;
                if (!Yield(player, node, def, null)) break;
                caught++;
            }
            string trapId = _trapItems.TryGetValue(node, out string? id) ? id : "";
            _trapItems.Remove(node);
            node.TrapOwner = null;
            if (trapId.Length > 0 && items.Give(player, trapId, 1) > 0)
                World.Find<GroundItemSystem>()?.Spawn(trapId, 1, player.Plane, player.Position, player);
            World.Tell(player, TextKind.Info, caught == 0 ? "You lift your trap. Nothing stayed in it." : "You lift your trap and empty it.");
        }

        private readonly Dictionary<Node, string> _trapItems = new Dictionary<Node, string>();

        /// <summary>A player leaving lifts their traps and takes them along (the catch is let go), so no weir stays blocked.</summary>
        public override void OnLeaving(Player player)
        {
            _readers.Remove(player);
            ItemSystem? items = World.Find<ItemSystem>();
            foreach (Node node in _nodes.Where(n => n.TrapOwner == player.Name))
            {
                node.TrapOwner = null;
                if (_trapItems.TryGetValue(node, out string? id) && items != null && items.Give(player, id, 1) > 0)
                    World.Log(player.Name + "'s " + id + " was lost: no room to carry it home.");
                _trapItems.Remove(node);
            }
        }

        // ------------------------------------------------------------------ what clients see

        private void SendSkilling(Player player)
        {
            uint now = World.CurrentTick;
            var state = new GatheringWorldState();
            foreach (Node n in _nodes)
            {
                bool dormant = n.Dormant, trap = n.TrapOwner != null, needs = n.NeedsTending(now), tended = n.IsTended(now);
                if ((!dormant && !trap && !needs && !tended) || !World.CanSee(player, n.Placed.Plane, n.Placed.Tile)) continue;
                var at = new NodeRef { Type = n.Placed.Type, Plane = n.Placed.Plane, X = n.Placed.X, Y = n.Placed.Y };
                if (dormant) state.Dormant.Add(at);
                if (n.IsBubbling(now)) state.Bubbling.Add(at);
                if (needs) state.NeedsTending.Add(at);
                if (tended) state.Tended.Add(at);
                if (trap)
                {
                    NodeTrapDef? def = Def(n)?.Trap;
                    bool mine = n.TrapOwner == player.Name;
                    double full = mine && def != null ? Math.Min(1, (now - n.TrapSetAt) / (double)Math.Max(1, def.Ticks * Math.Max(1, def.Max))) : 0;
                    state.Traps.Add(new TrapState { Type = at.Type, Plane = at.Plane, X = at.X, Y = at.Y, Mine = mine, Full = Math.Round(full * 10) / 10 });
                }
            }
            JsonStateMessage message = JsonStateMessage.Of(GatheringWorldState.StateKey, state);
            SentGathering sent = player.Component<SentGathering>();
            if (message.Json == sent.Last) return;
            sent.Last = message.Json;
            player.Connection.Send(message);
        }
    }
}
