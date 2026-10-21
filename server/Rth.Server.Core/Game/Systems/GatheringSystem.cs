using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Gathering nodes (docs/skills.md 2.3): placed objects whose type has a <see cref="NodeDef"/>,
    /// worked with the "gather" action. An attempt every few ticks succeeds by level and gives XP
    /// and what the node gives (items, points, extras); after its yields the node depletes for a
    /// while, for everyone. A node type can surge: now and then one node of each group wakes and
    /// gives more for a short time (Cruor's hungry founts). Clients get the nodes near them that
    /// are depleted or surging as the "nodes" server state.
    /// </summary>
    /// <remarks>Tools (a pickaxe's success bonus, conservation, transformation, extras) come with Mining.</remarks>
    public sealed class GatheringSystem : GameSystem
    {
        public const string GatherAction = "gather";
        public const string StateKey = "nodes";

        /// <summary>One placed node's live state.</summary>
        public sealed class Node
        {
            public Node(PlacedObject placed) => Placed = placed;

            public PlacedObject Placed { get; }
            public int YieldsLeft;
            public uint DepletedUntil;
            public uint SurgingUntil;
            public int Group;

            public bool IsDepleted(uint now) => now < DepletedUntil;
            public bool IsSurging(uint now) => now < SurgingUntil;
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<int, uint> _nextSurge = new Dictionary<int, uint>();

        private sealed class SentNodes
        {
            public string Last = "";
        }

        protected override void OnAttach()
        {
            ObjectSystem objects = World.Get<ObjectSystem>();
            foreach (PlacedObject placed in objects.Placed) _nodes.Add(new Node(placed));
            Regroup();
            objects.RegisterAction(GatherAction, (player, placed, def) =>
            {
                Node? node = NodeAt(placed);
                if (node == null || def.Node == null)
                {
                    World.Log("Object '" + placed.Type + "' has a gather action but no node.");
                    return;
                }
                World.StartInteraction(player, new GatherInteraction(placed));
            });
        }

        public override void OnContentChanged() => Regroup();

        /// <summary>Groups each node type's nodes into clusters (chains within the surge's radius), one surge per cluster.</summary>
        private void Regroup()
        {
            int next = 0;
            foreach (Node n in _nodes) n.Group = -1;
            foreach (Node seed in _nodes)
            {
                NodeDef? def = Def(seed);
                if (seed.Group >= 0 || def == null) continue;
                int radius = def.Surge?.GroupRadius ?? 0;
                seed.Group = next;
                var open = new Queue<Node>(new[] { seed });
                while (open.Count > 0)
                {
                    Node at = open.Dequeue();
                    foreach (Node other in _nodes)
                    {
                        if (other.Group >= 0 || other.Placed.Type != seed.Placed.Type || other.Placed.Plane != at.Placed.Plane) continue;
                        if (other.Placed.Tile.ChebyshevDistance(at.Placed.Tile) > radius) continue;
                        other.Group = next;
                        open.Enqueue(other);
                    }
                }
                next++;
            }
        }

        public NodeDef? Def(Node node) => World.Content.Objects.TryGetValue(node.Placed.Type, out ObjectDef? def) ? def.Node : null;

        public Node? NodeAt(PlacedObject placed) => _nodes.FirstOrDefault(n => ReferenceEquals(n.Placed, placed));

        public IReadOnlyList<Node> Nodes => _nodes;

        // ------------------------------------------------------------------ surges and respawns

        public override void OnTimers()
        {
            uint now = World.CurrentTick;
            foreach (IGrouping<int, Node> group in _nodes.Where(n => n.Group >= 0).GroupBy(n => n.Group))
            {
                Node first = group.First();
                NodeSurgeDef? surge = Def(first)?.Surge;
                if (surge == null) continue;
                if (!_nextSurge.TryGetValue(group.Key, out uint at))
                {
                    _nextSurge[group.Key] = now + (uint)World.Random.Next(surge.EveryTicks / 2, surge.EveryTicks + 1);
                    continue;
                }
                if (now < at) continue;
                // About every EveryTicks, give or take a fifth, so a monument's rhythm isn't a metronome.
                int spread = Math.Max(1, surge.EveryTicks / 5);
                _nextSurge[group.Key] = now + (uint)(surge.EveryTicks + World.Random.Next(-spread, spread + 1));
                List<Node> awake = group.Where(n => !n.IsDepleted(now) && !n.IsSurging(now)).ToList();
                if (awake.Count == 0) continue;
                Node chosen = awake[World.Random.Next(awake.Count)];
                chosen.SurgingUntil = now + (uint)surge.Ticks;
                if (surge.Message != null)
                    foreach (Player p in World.Players)
                        if (World.CanSee(p, chosen.Placed.Plane, chosen.Placed.Tile)) World.Tell(p, TextKind.Info, surge.Message);
            }
        }

        // ------------------------------------------------------------------ working a node

        /// <summary>Why the player can't work this node, or null.</summary>
        public string? Problem(Player player, Node node, NodeDef def)
        {
            if (node.IsDepleted(World.CurrentTick)) return def.DepletedMessage;
            SkillSystem? skills = World.Find<SkillSystem>();
            if (skills == null) return null;
            string what = def.Verb + " the " + World.Content.Objects[node.Placed.Type].Name.ToLowerInvariant();
            return skills.Check(player, def.Requirements, what)
                ?? skills.Check(player, new[] { new Rth.Shared.Skills.SkillRequirement { Skill = def.Skill, Level = def.Level } }, what);
        }

        /// <summary>The chance of one attempt succeeding for this player.</summary>
        public double SuccessChance(Player player, NodeDef def)
        {
            int level = World.Find<SkillSystem>()?.Level(player, def.Skill) ?? 1;
            return Math.Max(def.MinChance, Math.Min(def.MaxChance, def.BaseChance + def.PerLevel * (level - def.Level)));
        }

        /// <summary>The player starts working a node: its message, its words, its cut.</summary>
        internal void Begin(Player player, Node node, NodeDef def)
        {
            if (def.StartMessage != null) World.Tell(player, TextKind.Info, def.StartMessage);
            if (def.StartSay != null) World.Find<ChatSystem>()?.Say(player, def.StartSay);
            if (def.StartDamage > 0 && player.Blood > 1)
                World.Find<CombatSystem>()?.Damage(player, Math.Min(def.StartDamage, player.Blood - 1), null, HitKind.Normal);
        }

        /// <summary>One attempt. Returns false when the player should stop (depleted, pack full).</summary>
        internal bool Attempt(Player player, Node node, NodeDef def)
        {
            uint now = World.CurrentTick;
            if (World.Random.NextDouble() >= SuccessChance(player, def)) return true;

            ItemSystem? items = World.Find<ItemSystem>();
            if (items != null && def.Items.Any(i => !items.CanGive(player, i.Item, i.Count)))
            {
                World.Tell(player, TextKind.Warning, "You don't have enough inventory space.");
                return false;
            }

            bool surging = node.IsSurging(now);
            NodeSurgeDef? surge = def.Surge;
            World.Find<SkillSystem>()?.AddXp(player, def.Skill, def.Xp * (surging && surge != null ? surge.XpMultiplier : 1));

            var gained = new List<Drop>();
            if (items != null)
            {
                foreach (Rth.Shared.Items.ItemCost item in def.Items)
                {
                    items.Give(player, item.Item, item.Count);
                    gained.Add(new Drop(item.Item, item.Count));
                }
                if (def.Extras != null)
                    foreach (Drop extra in DropRoller.Roll(World.Content, def.Extras, World.Random))
                    {
                        items.Give(player, extra.ItemId, extra.Count);
                        gained.Add(extra);
                    }
            }
            if (gained.Count > 0) World.Events.Publish(new ItemsObtained(player, "node:" + node.Placed.Type, gained));

            if (def.Points != null)
            {
                double chance = def.PointsChance * (surging && surge != null ? surge.PointsMultiplier : 1);
                if (World.Random.NextDouble() < chance)
                {
                    PointsSystem? points = World.Find<PointsSystem>();
                    points?.Add(player, def.Points, def.PointsAmount);
                    if (points != null) World.Tell(player, TextKind.Info, "You gain " + def.PointsAmount + " " + points.PoolName(def.Points).ToLowerInvariant() + ".");
                }
            }

            if (node.YieldsLeft <= 0) node.YieldsLeft = World.Random.Next(Math.Max(1, def.YieldsMin), Math.Max(def.YieldsMin, def.YieldsMax) + 1);
            node.YieldsLeft--;
            if (node.YieldsLeft > 0) return true;
            node.DepletedUntil = now + (uint)Math.Max(1, def.RespawnTicks);
            node.SurgingUntil = 0;
            World.Tell(player, TextKind.Info, def.DepletedMessage);
            return false;
        }

        // ------------------------------------------------------------------ what clients see

        public override void SendState(Player player)
        {
            uint now = World.CurrentTick;
            var states = new List<NodeState>();
            foreach (Node n in _nodes)
            {
                bool depleted = n.IsDepleted(now), surging = n.IsSurging(now);
                if ((!depleted && !surging) || !World.CanSee(player, n.Placed.Plane, n.Placed.Tile)) continue;
                states.Add(new NodeState { Type = n.Placed.Type, Plane = n.Placed.Plane, X = n.Placed.X, Y = n.Placed.Y, Depleted = depleted, Surging = surging });
            }
            JsonStateMessage message = JsonStateMessage.Of(StateKey, states);
            SentNodes sent = player.Component<SentNodes>();
            if (message.Json == sent.Last) return;
            sent.Last = message.Json;
            player.Connection.Send(message);
        }
    }

    /// <summary>Walks to a node and works it, an attempt every few ticks, until it depletes or the player stops.</summary>
    public sealed class GatherInteraction : ObjectInteraction
    {
        private bool _started;
        private uint _nextAttempt;

        public GatherInteraction(PlacedObject node) : base(node) { }

        protected override bool Work(World world, Player player)
        {
            GatheringSystem gathering = world.Get<GatheringSystem>();
            GatheringSystem.Node? node = gathering.NodeAt(Object);
            NodeDef? def = node == null ? null : gathering.Def(node);
            if (node == null || def == null) return false;

            if (!_started)
            {
                string? problem = gathering.Problem(player, node, def);
                if (problem != null)
                {
                    world.Tell(player, TextKind.Warning, problem);
                    return false;
                }
                _started = true;
                _nextAttempt = world.CurrentTick + (uint)Math.Max(1, def.Ticks);
                gathering.Begin(player, node, def);
                return true;
            }
            if (node.IsDepleted(world.CurrentTick))
            {
                world.Tell(player, TextKind.Info, def.DepletedMessage);
                return false;
            }
            if (world.CurrentTick < _nextAttempt) return true;
            _nextAttempt = world.CurrentTick + (uint)Math.Max(1, def.Ticks);
            return gathering.Attempt(player, node, def);
        }
    }
}
