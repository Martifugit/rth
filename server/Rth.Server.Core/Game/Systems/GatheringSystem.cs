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
    /// Gathering nodes (docs/skills.md 2.3): placed objects whose type has a <see cref="NodeDef"/>,
    /// worked with the "gather" action. An attempt every few ticks succeeds by level and gives XP
    /// and what the node gives (items, points, extras); after its yields the node depletes for a
    /// while, for everyone. A node type can surge: now and then one node of each group wakes and
    /// gives more for a short time (Cruor's hungry founts). Clients get the nodes near them that
    /// are depleted or surging as the "nodes" server state. A node can need a tool kind (and a
    /// lowest tier): the best usable one in the pack or worn is used, and its effects (success,
    /// conservation, extras, transformations) apply (docs/skills.md 2.3).
    /// </summary>
    public sealed partial class GatheringSystem : GameSystem, IObjectUseHandler
    {
        public const string GatherAction = "gather";
        public const string StateKey = "nodes";

        /// <summary>A player's work on one progress node (Mining): the bar, their crack, when their side's heat cycle began.</summary>
        public sealed class Miner
        {
            public PlacedObject? Node;
            public double Progress;
            public double Crack;
            public int CrackLeft;
            public uint HeatFrom;
            public string? LastSent;
        }

        /// <summary>One placed node's live state.</summary>
        public sealed partial class Node
        {
            public Node(PlacedObject placed) => Placed = placed;

            public PlacedObject Placed { get; }
            public int YieldsLeft;
            public uint DepletedUntil;
            public uint SurgingUntil;
            public int Group;
            /// <summary>Fire-setting: when a fire set against it was lit, and until when it burns.</summary>
            public uint FireLitAt;
            public uint FireUntil;
            /// <summary>The tile the fire was laid towards (where its setter stood): it burns at that face.</summary>
            public TileCoord FireTowards;

            public bool IsBurning(uint now) => now < FireUntil;

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
            AttachSkilling();
        }

        /// <summary>Fire-setting is done by using things on the rock: logs lay a fire, water douses it.</summary>
        public bool OnObjectUse(Player player, ItemDef item, PlacedObject placed, ObjectDef def)
        {
            FireSettingDef? fire = def.Node?.Fire;
            if (fire == null) return false;
            if (Matches(fire.Logs, item)) SetFire(player, placed);
            else if (item.Water != null) Douse(player, placed);
            else return false;
            return true;
        }

        public override void OnContentChanged()
        {
            Regroup();
            SpreadRoamers();
        }

        /// <summary>Groups each node type's nodes into clusters (chains within the surge's radius), one surge per cluster.</summary>
        private void Regroup()
        {
            int next = 0;
            foreach (Node n in _nodes) n.Group = -1;
            foreach (Node seed in _nodes)
            {
                NodeDef? def = Def(seed);
                if (seed.Group >= 0 || def == null) continue;
                int radius = GroupRadius(def);
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
                List<Node> awake = group.Where(n => !n.IsDepleted(now) && !n.IsSurging(now) && !n.Dormant).ToList();
                if (awake.Count == 0) continue;
                Node chosen = awake[World.Random.Next(awake.Count)];
                chosen.SurgingUntil = now + (uint)surge.Ticks;
                if (surge.Message != null)
                    foreach (Player p in World.Players)
                        if (World.CanSee(p, chosen.Placed.Plane, chosen.Placed.Tile)) World.Tell(p, TextKind.Info, surge.Message);
            }
            SkillingTimers(now);
        }

        // ------------------------------------------------------------------ working a node

        /// <summary>Why the player can't work this node, or null.</summary>
        public string? Problem(Player player, Node node, NodeDef def)
        {
            if (node.IsDepleted(World.CurrentTick)) return def.DepletedMessage;
            if (node.Dormant) return "There's nothing here now.";
            SkillSystem? skills = World.Find<SkillSystem>();
            if (skills == null) return null;
            string what = What(node, def);
            return skills.Check(player, def.Requirements, what)
                ?? skills.Check(player, new[] { new Rth.Shared.Skills.SkillRequirement { Skill = def.Skill, Level = def.Level } }, what)
                ?? ToolProblem(player, node, def);
        }

        private string What(Node node, NodeDef def) =>
            def.Verb + " the " + World.Content.Objects[node.Placed.Type].Name.ToLowerInvariant();

        // ------------------------------------------------------------------ tools

        /// <summary>Every tool of this kind the player wears or carries.</summary>
        private IEnumerable<ItemDef> ToolsOfKind(Player player, string kind)
        {
            ItemSystem? items = World.Find<ItemSystem>();
            if (items == null) yield break;
            PlayerItems held = items.Of(player);
            foreach (var container in new[] { held.Equipment, held.Inventory })
                for (int i = 0; i < container.Size; i++)
                {
                    if (container.IsEmpty(i)) continue;
                    if (World.Content.Items.TryGetValue(container[i].ItemId, out ItemDef? item) && item.Tool != null && item.Tool.Kind == kind)
                        yield return item;
                }
        }

        private bool CanUse(Player player, ToolDef tool) =>
            World.Find<SkillSystem>()?.Check(player, tool.Requirements, "use it") == null;

        /// <summary>
        /// The tool this player would work the node with: the highest tier they can use (then the
        /// best success bonus), from the pack or worn. Null when the node needs none, or they have none.
        /// </summary>
        public ItemDef? BestTool(Player player, NodeDef def)
        {
            if (def.Tool == null) return null;
            return ToolsOfKind(player, def.Tool)
                .Where(t => t.Tool!.Tier >= def.ToolTier && CanUse(player, t.Tool))
                .OrderByDescending(t => t.Tool!.Tier).ThenByDescending(t => t.Tool!.Success)
                .FirstOrDefault();
        }

        /// <summary>Why the player's tools won't do for this node, or null.</summary>
        public string? ToolProblem(Player player, Node node, NodeDef def)
        {
            if (def.Tool == null || def.ToolOptional || BestTool(player, def) != null) return null;
            string what = What(node, def);
            List<ItemDef> owned = ToolsOfKind(player, def.Tool).ToList();
            ItemDef? goodEnough = owned.Where(t => t.Tool!.Tier >= def.ToolTier).OrderBy(t => t.Tool!.Tier).FirstOrDefault();
            if (goodEnough != null)
                return World.Find<SkillSystem>()?.Check(player, goodEnough.Tool!.Requirements, "use your " + goodEnough.Name.ToLowerInvariant())
                    ?? "You can't use your " + goodEnough.Name.ToLowerInvariant() + ".";
            if (owned.Count > 0) return "You need a better " + def.Tool + " to " + what + ".";
            return def.NoToolMessage ?? "You need " + Article(def.Tool) + " " + def.Tool + " to " + what + ".";
        }

        private static string Article(string word) =>
            word.Length > 0 && "aeiou".IndexOf(char.ToLowerInvariant(word[0])) >= 0 ? "an" : "a";

        /// <summary>The chance of one attempt succeeding for this player (with their best tool).</summary>
        public double SuccessChance(Player player, NodeDef def) => SuccessChance(player, def, BestTool(player, def)?.Tool);

        private double SuccessChance(Player player, NodeDef def, ToolDef? tool)
        {
            int level = World.Find<SkillSystem>()?.Level(player, def.Skill) ?? 1;
            // Gear, buffs and hymns add to it through the gatherSuccess stat, on top of the tool.
            double chance = def.BaseChance + def.PerLevel * (level - def.Level) + (tool?.Success ?? 0)
                            + player.Stats.Get(Rth.Shared.Combat.StatIds.GatherSuccess) / 100.0;
            return Math.Max(def.MinChance, Math.Min(def.MaxChance, chance));
        }

        /// <summary>
        /// Whether a success uses nothing up: the tool's conservation and the gatherConservation
        /// stat (gear, buffs, hymns) each get their own roll.
        /// </summary>
        private bool Conserved(Player player, ToolDef? tool)
        {
            if (tool != null && tool.Conservation > 0 && World.Random.NextDouble() < tool.Conservation) return true;
            int stat = player.Stats.Get(Rth.Shared.Combat.StatIds.GatherConservation);
            return stat > 0 && World.Random.NextDouble() < stat / 100.0;
        }

        /// <summary>
        /// How long a node stays depleted: its respawn time, less a share for every other player
        /// near it, never under its minimum.
        /// </summary>
        public int RespawnTicks(int plane, TileCoord tile, NodeDef def)
        {
            int full = Math.Max(1, def.RespawnTicks);
            if (def.MinRespawnTicks is not int min) return full;
            int others = Math.Max(0, World.EntitiesNear(plane, tile, NodeDef.CrowdRadius).OfType<Player>().Count() - 1);
            double ticks = full * Math.Max(0, 1 - def.RespawnPerPlayer * others);
            return Math.Max(Math.Max(1, min), (int)Math.Round(ticks));
        }

        /// <summary>The player starts working a node: its message, its words, its cut.</summary>
        internal void Begin(Player player, Node node, NodeDef def)
        {
            World.Animate(player, def.Animation, node.Placed.Tile);
            if (def.StartMessage != null) World.Tell(player, TextKind.Info, def.StartMessage);
            if (def.StartSay != null) World.Find<ChatSystem>()?.Say(player, def.StartSay);
            if (def.StartDamage > 0 && player.Blood > 1)
                World.Find<CombatSystem>()?.Damage(player, Math.Min(def.StartDamage, player.Blood - 1), null, HitKind.Normal);
        }

        /// <summary>One attempt. Returns false when the player should stop (depleted, pack full).</summary>
        internal bool Attempt(Player player, Node node, NodeDef def)
        {
            uint now = World.CurrentTick;
            string? toolProblem = ToolProblem(player, node, def);
            if (toolProblem != null)
            {
                World.Tell(player, TextKind.Warning, toolProblem);
                return false;
            }
            ToolDef? tool = BestTool(player, def)?.Tool;
            World.Animate(player, def.Animation, node.Placed.Tile);
            if (def.Progress != null) return Strike(player, node, def, tool);
            if (World.Random.NextDouble() >= SuccessChance(player, def, tool) + TendingBonus(node, def)) return true;
            return Yield(player, node, def, tool);
        }

        /// <summary>
        /// One strike on a progress node: a little XP, and progress towards the next yield by level,
        /// tool, gatherSuccess and the player's crack (which each strike wears down); a full bar
        /// yields. Returns false when the player should stop.
        /// </summary>
        private bool Strike(Player player, Node node, NodeDef def, ToolDef? tool)
        {
            NodeProgressDef progress = def.Progress!;
            Miner miner = MinerOn(player, node);
            SkillSystem? skills = World.Find<SkillSystem>();
            int level = skills?.Level(player, def.Skill) ?? 1;
            double crack = miner.CrackLeft > 0 && def.Fire != null ? 1 + miner.Crack * miner.CrackLeft / Math.Max(1, def.Fire.CrackStrikes) : 1;
            double add = (progress.PerStrike + progress.PerLevel * Math.Max(0, level - def.Level))
                         * (1 + (tool?.Success ?? 0)) * (1 + player.Stats.Get(Rth.Shared.Combat.StatIds.GatherSuccess) / 100.0) * crack;
            if (miner.CrackLeft > 0 && --miner.CrackLeft == 0)
            {
                miner.Crack = 0;
                miner.HeatFrom = World.CurrentTick;     // worked out: the fire heats this face again from now
            }
            if (progress.StrikeXp > 0) skills?.AddXp(player, def.Skill, progress.StrikeXp);
            miner.Progress += add;
            if (miner.Progress < progress.Needed) return true;
            miner.Progress -= progress.Needed;
            bool goOn = Yield(player, node, def, tool);
            if (node.IsDepleted(World.CurrentTick)) miner.Progress = 0;
            return goOn;
        }

        /// <summary>A player's work on this node, started afresh if they were working another.</summary>
        public Miner MinerOn(Player player, Node node)
        {
            Miner miner = player.Component<Miner>();
            if (!ReferenceEquals(miner.Node, node.Placed)) miner = Reset(player, node.Placed);
            return miner;
        }

        private static Miner Reset(Player player, PlacedObject placed)
        {
            Miner miner = player.Component<Miner>();
            miner.Node = placed;
            miner.Progress = 0;
            miner.Crack = 0;
            miner.CrackLeft = 0;
            miner.HeatFrom = 0;
            return miner;
        }

        /// <summary>
        /// This player's side of the stone's heat now (0..1), or -1 with no fire burning. A face still
        /// cracked from their last douse doesn't heat (0) until they have worked the crack out.
        /// </summary>
        public double HeatFor(Player player, Node node, NodeDef def)
        {
            uint now = World.CurrentTick;
            if (def.Fire == null || !node.IsBurning(now)) return -1;
            Miner miner = player.Component<Miner>();
            bool mine = ReferenceEquals(miner.Node, node.Placed);
            if (mine && miner.CrackLeft > 0) return 0;
            uint from = mine ? Math.Max(miner.HeatFrom, node.FireLitAt) : node.FireLitAt;
            return def.Fire.Heat(now - from);
        }

        /// <summary>Sets a fire against the node, from the player's logs (it burns for everyone), then mines on.</summary>
        public void SetFire(Player player, PlacedObject placed)
        {
            Node? node = NodeAt(placed);
            NodeDef? def = node == null ? null : Def(node);
            FireSettingDef? fire = def?.Fire;
            ItemSystem? items = World.Find<ItemSystem>();
            if (node == null || def == null || fire == null || items == null) return;
            uint now = World.CurrentTick;
            if (node.IsDepleted(now))
            {
                World.Tell(player, TextKind.Info, def.DepletedMessage);
                return;
            }
            if (node.IsBurning(now))
            {
                World.Tell(player, TextKind.Info, "A fire is already burning against the rock.");
                return;
            }
            List<ItemDef> logs = World.Content.Items.Values.Where(i => Matches(fire.Logs, i)).ToList();
            int have = logs.Sum(l => items.CountOf(player, l.Id));
            if (have < fire.LogCount)
            {
                World.Tell(player, TextKind.Warning, "You need " + fire.LogCount + " logs to set a fire against the rock.");
                return;
            }
            int left = fire.LogCount;
            foreach (ItemDef log in logs)
            {
                int take = Math.Min(left, items.CountOf(player, log.Id));
                if (take > 0) items.Take(player, log.Id, take);
                left -= take;
                if (left == 0) break;
            }
            node.FireLitAt = now;
            node.FireUntil = now + (uint)Math.Max(1, fire.BurnTicks);
            node.FireTowards = player.Position;
            World.Animate(player, "work", placed.Tile);
            if (fire.LightXp > 0) World.Find<SkillSystem>()?.AddXp(player, def.Skill, fire.LightXp);
            // Its own act: the XP, and the miner clicks the rock to work it again (as tending is).
            World.Tell(player, TextKind.Info, "You stack the logs against the rock and set them burning.");
        }

        /// <summary>
        /// Douses the burning stone with water the player carries: their side cracks as hot as it is
        /// now (the sweet spot is the peak of its glow). Then they click the rock to work it.
        /// </summary>
        public void Douse(Player player, PlacedObject placed)
        {
            Node? node = NodeAt(placed);
            NodeDef? def = node == null ? null : Def(node);
            FireSettingDef? fire = def?.Fire;
            ItemSystem? items = World.Find<ItemSystem>();
            if (node == null || def == null || fire == null || items == null) return;
            if (!node.IsBurning(World.CurrentTick))
            {
                World.Tell(player, TextKind.Info, "There's no fire against the rock to douse.");
                return;
            }
            ItemDef? water = World.Content.Items.Values.FirstOrDefault(i => i.Water != null && items.CountOf(player, i.Id) > 0);
            if (water == null)
            {
                World.Tell(player, TextKind.Warning, "You need water to douse the stone: fill something at a trough or a well.");
                return;
            }
            Miner had = player.Component<Miner>();
            if (ReferenceEquals(had.Node, placed) && had.CrackLeft > 0)
            {
                World.Tell(player, TextKind.Info, "Your face of the rock is still cracked. Work it, and let the fire heat it again.");
                return;
            }
            double heat = Math.Max(0, HeatFor(player, node, def));
            items.Take(player, water.Id, 1);
            if (water.Water!.Leaves != null) items.Give(player, water.Water.Leaves, 1);
            Miner miner = MinerOn(player, node);
            miner.Crack = heat * fire.CrackBonus;
            miner.CrackLeft = fire.CrackStrikes;
            miner.HeatFrom = World.CurrentTick;
            if (fire.DouseXp > 0) World.Find<SkillSystem>()?.AddXp(player, def.Skill, fire.DouseXp * heat);
            World.Tell(player, TextKind.Info, heat >= 0.95 ? "The stone splits with a roar of steam."
                : heat >= 0.6 ? "The stone cracks in a cloud of steam."
                : "The water hisses; the stone was barely warm.");
            World.Animate(player, "work", placed.Tile);
        }

        private static bool Matches(string spec, ItemDef item) =>
            spec.StartsWith("tag:", StringComparison.Ordinal) ? item.Tags.Contains(spec.Substring(4)) : item.Id == spec;

        /// <summary>What a success gives: XP, items, extras, points; then the node may deplete. Returns false when the player should stop.</summary>
        private bool Yield(Player player, Node node, NodeDef def, ToolDef? tool)
        {
            uint now = World.CurrentTick;
            ItemSystem? items = World.Find<ItemSystem>();
            SkillSystem? skills = World.Find<SkillSystem>();
            NodeCatch? caught = PickCatch(player, def);
            List<ItemCost> giving = caught != null ? new List<ItemCost> { new ItemCost { Item = caught.Item, Count = caught.Count } } : def.Items;
            if (items != null && giving.Any(i => !items.CanGive(player, i.Item, i.Count)))
            {
                World.Tell(player, TextKind.Warning, "You don't have enough inventory space.");
                return false;
            }

            bool surging = node.IsSurging(now);
            NodeSurgeDef? surge = def.Surge;
            skills?.AddXp(player, def.Skill, (caught?.Xp ?? def.Xp) * (surging && surge != null ? surge.XpMultiplier : 1));

            var gained = new List<Drop>();
            if (items != null)
            {
                foreach (ItemCost item in Transformed(player, giving, tool, skills))
                {
                    items.Give(player, item.Item, item.Count);
                    gained.Add(new Drop(item.Item, item.Count));
                }
                var tables = new List<string>();
                if (def.Extras != null && !(tool?.ReplacesExtras ?? false)) tables.Add(def.Extras);
                if (tool?.Extras != null) tables.Add(tool.Extras);
                foreach (string table in tables)
                    foreach (Drop extra in DropRoller.Roll(World.Content, table, World.Random))
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
                    if (points != null) World.Tell(player, TextKind.Info, "You gain " + def.PointsAmount + " " + Singular(points.PoolName(def.Points).ToLowerInvariant(), def.PointsAmount) + ".");
                }
            }

            if (node.YieldsLeft <= 0) node.YieldsLeft = World.Random.Next(Math.Max(1, def.YieldsMin), Math.Max(def.YieldsMin, def.YieldsMax) + 1);
            if (!Conserved(player, tool) && World.Random.NextDouble() < DepleteChance(node, def)) node.YieldsLeft--;
            if (node.YieldsLeft > 0) return true;
            if (MoveUsedUp(node, def)) return false;    // a roaming node moves on instead of running out
            node.DepletedUntil = now + (uint)RespawnTicks(node.Placed.Plane, node.Placed.Tile, def);
            node.SurgingUntil = 0;
            World.Tell(player, TextKind.Info, def.DepletedMessage);
            return false;
        }

        /// <summary>
        /// What one success gives, after the tool's transformations: each item rolls once; a
        /// transformation needs its skill's level and gives that skill's XP.
        /// </summary>
        private List<ItemCost> Transformed(Player player, List<ItemCost> giving, ToolDef? tool, SkillSystem? skills)
        {
            var result = new List<ItemCost>();
            foreach (ItemCost item in giving)
            {
                ToolTransform? change = tool?.Transforms.FirstOrDefault(t => t.From == item.Item
                    && (t.Skill == null || skills == null || skills.Level(player, t.Skill) >= t.Level)
                    && World.Random.NextDouble() < t.Chance);
                if (change == null)
                {
                    result.Add(item);
                    continue;
                }
                result.Add(new ItemCost { Item = change.To, Count = item.Count });
                if (change.Skill != null && change.Xp > 0) skills?.AddXp(player, change.Skill, change.Xp);
            }
            return result;
        }

        /// <summary>"pledges" for one is "pledge" (pool names are plural).</summary>
        private static string Singular(string plural, int count) =>
            count == 1 && plural.EndsWith("s", StringComparison.Ordinal) ? plural.Substring(0, plural.Length - 1) : plural;

        // ------------------------------------------------------------------ what clients see

        public override void SendState(Player player)
        {
            uint now = World.CurrentTick;
            var states = new List<NodeState>();
            foreach (Node n in _nodes)
            {
                bool depleted = n.IsDepleted(now), surging = n.IsSurging(now), burning = n.IsBurning(now);
                if ((!depleted && !surging && !burning) || !World.CanSee(player, n.Placed.Plane, n.Placed.Tile)) continue;
                states.Add(new NodeState
                {
                    Type = n.Placed.Type, Plane = n.Placed.Plane, X = n.Placed.X, Y = n.Placed.Y, Depleted = depleted, Surging = surging, Burning = burning,
                    FireX = burning ? n.FireTowards.X : 0, FireY = burning ? n.FireTowards.Y : 0,
                });
            }
            JsonStateMessage message = JsonStateMessage.Of(StateKey, states);
            SentNodes sent = player.Component<SentNodes>();
            if (message.Json != sent.Last)
            {
                sent.Last = message.Json;
                player.Connection.Send(message);
            }
            SendMining(player);
            SendSkilling(player);
        }

        /// <summary>The player's own work on a progress node ("mining"): sent while they work one, empty otherwise.</summary>
        private void SendMining(Player player)
        {
            Miner miner = player.Component<Miner>();
            var state = new MiningState();
            Node? node = miner.Node != null && player.Interaction is GatherInteraction g && ReferenceEquals(g.Object, miner.Node) ? NodeAt(miner.Node) : null;
            NodeDef? def = node == null ? null : Def(node);
            if (node != null && def?.Progress != null)
            {
                state.Type = node.Placed.Type;
                state.Plane = node.Placed.Plane;
                state.X = node.Placed.X;
                state.Y = node.Placed.Y;
                state.Progress = Math.Min(1, miner.Progress / Math.Max(1, def.Progress.Needed));
                state.Heat = HeatFor(player, node, def);
                state.Crack = def.Fire != null && miner.CrackLeft > 0 ? (double)miner.CrackLeft / Math.Max(1, def.Fire.CrackStrikes) : 0;
            }
            JsonStateMessage message = JsonStateMessage.Of(MiningState.StateKey, state);
            if (message.Json == miner.LastSent) return;
            miner.LastSent = message.Json;
            player.Connection.Send(message);
        }
    }

    /// <summary>Walks to a node and works it, an attempt every few ticks, until it depletes or the player stops.</summary>
    public sealed class GatherInteraction : ObjectInteraction
    {
        private bool _started;
        private uint _nextAttempt;

        public GatherInteraction(PlacedObject node) : base(node) { }

        public override void End(World world, Player player) => world.RecalculateStats(player);   // the weapon back in hand

        protected override bool Work(World world, Player player)
        {
            GatheringSystem gathering = world.Get<GatheringSystem>();
            GatheringSystem.Node? node = gathering.NodeAt(Object);
            NodeDef? def = node == null ? null : gathering.Def(node);
            if (node == null || def == null) return false;
            if (def.Trap != null)
            {
                // A trap is set or collected in one act; the trap does the waiting.
                string? trapProblem = node.TrapOwner == null ? gathering.Problem(player, node, def) : null;
                if (trapProblem != null) world.Tell(player, TextKind.Warning, trapProblem);
                else gathering.WorkTrap(player, node, def);
                return false;
            }

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
                // The tool in hand while it works (the swing is made for it); the weapon comes back after.
                string? tool = gathering.BestTool(player, def)?.Id;
                if (tool != null) player.SetHeld(tool);
                gathering.Begin(player, node, def);
                return true;
            }
            if (node.IsDepleted(world.CurrentTick))
            {
                world.Tell(player, TextKind.Info, def.DepletedMessage);
                return false;
            }
            if (node.Dormant) return false;     // it moved off (the player was told)
            if (world.CurrentTick < _nextAttempt) return true;
            _nextAttempt = world.CurrentTick + (uint)Math.Max(1, def.Ticks);
            return gathering.Attempt(player, node, def);
        }
    }
}
