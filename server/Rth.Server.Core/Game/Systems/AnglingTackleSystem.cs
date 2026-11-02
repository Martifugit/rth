using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Server.Persistence;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;
using Rth.Server.Items;

namespace Rth.Server.Game
{
    /// <summary>A player's rig: a part in each slot, and every part's condition by item kind.</summary>
    public sealed class PlayerTackle
    {
        public Dictionary<AnglingSlot, string> Slots = new Dictionary<AnglingSlot, string>();
        /// <summary>Condition (0-100) by item id; a kind never worn is at 100. Kept when a part leaves the rig.</summary>
        public Dictionary<string, double> Condition = new Dictionary<string, double>();
        [JsonIgnore] public string LastSent = "";

        public double ConditionOf(string item) => Condition.TryGetValue(item, out double c) ? c : 100;
    }

    /// <summary>
    /// The angler's rig (docs/fishing.md 4): five slots (rod, reel, line, hook, lure), each holding
    /// a tackle part put in from the pack ("Rig") and taken out with the "tackle" intent
    /// ("unrig:Slot"). Parts wear from how a fight went and lose their bonus gradually; they are
    /// mended at a tackle bench (object action "mend": materials and the skill) or by a
    /// tackle-maker (NPC action "mend", for coins; "mend-rare" is the collector's, for collector
    /// parts). Saved as "tackle"; sent as the "tackle" state.
    /// </summary>
    public sealed class AnglingTackleSystem : GameSystem
    {
        public const string IntentKind = "tackle";
        public const string MendAction = "mend";
        public const string MendRareAction = "mend-rare";
        private const string SaveSection = "tackle";

        public PlayerTackle Of(Player player) => player.Component<PlayerTackle>();

        private AnglingFile Rules => World.Content.Angling;

        protected override void OnAttach()
        {
            World.Get<ItemSystem>().RegisterAction(ItemActions.Rig, (player, use) => Rig(player, use.Def, use.Container, use.Slot));
            World.Get<IntentSystem>().Register(IntentKind, (player, arg) =>
            {
                if (arg.StartsWith("unrig:", StringComparison.Ordinal) && Enum.TryParse(arg.Substring(6), out AnglingSlot slot)) Unrig(player, slot);
            });
            World.Get<ObjectSystem>().RegisterAction(MendAction, (player, placed, def) =>
                World.StartInteraction(player, new ObjectOnceInteraction(placed, MendYourself)));
            World.Get<NpcSystem>().RegisterAction(MendAction, (player, npc) => MendForCoins(player, collector: false));
            World.Get<NpcSystem>().RegisterAction(MendRareAction, (player, npc) => MendForCoins(player, collector: true));
        }

        /// <summary>The part in a slot, or null.</summary>
        public ItemDef? Part(Player player, AnglingSlot slot) =>
            Of(player).Slots.TryGetValue(slot, out string? id) ? World.Content.Item(id) : null;

        /// <summary>The rig's stats from its parts at their condition (no level bonus: that is the species').</summary>
        public AnglingRigStats Stats(Player player)
        {
            PlayerTackle tackle = Of(player);
            var parts = new List<(AnglingTackleDef, double)>();
            foreach (var pair in tackle.Slots)
                if (World.Content.Item(pair.Value)?.Tackle is AnglingTackleDef t) parts.Add((t, tackle.ConditionOf(pair.Value)));
            return AnglingRules.Rig(Rules, parts);
        }

        /// <summary>Puts a part from the pack in its slot; whatever was there goes back to the pack.</summary>
        public void Rig(Player player, ItemDef def, ItemContainer from, int slot)
        {
            AnglingTackleDef? tackle = def.Tackle;
            if (tackle == null) return;
            string? problem = World.Find<SkillSystem>()?.Check(player, tackle.Requirements, "rig the " + def.Name.ToLowerInvariant());
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return;
            }
            PlayerTackle rig = Of(player);
            rig.Slots.TryGetValue(tackle.Slot, out string? old);
            from.RemoveAt(slot, 1);
            rig.Slots[tackle.Slot] = def.Id;
            if (old != null)
            {
                ItemSystem items = World.Get<ItemSystem>();
                if (items.Give(player, old, 1) > 0) World.Find<GroundItemSystem>()?.Spawn(old, 1, player.Plane, player.Position, player);
            }
            World.Tell(player, TextKind.Info, "You rig your " + def.Name.ToLowerInvariant() + ".");
        }

        public void Unrig(Player player, AnglingSlot slot)
        {
            PlayerTackle rig = Of(player);
            if (!rig.Slots.TryGetValue(slot, out string? id)) return;
            ItemSystem items = World.Get<ItemSystem>();
            if (!items.CanGive(player, id, 1))
            {
                World.Tell(player, TextKind.Warning, "You have no room in your pack for it.");
                return;
            }
            rig.Slots.Remove(slot);
            items.Give(player, id, 1);
        }

        /// <summary>
        /// Wear from one fight: every part a little, and each by what it suffered (line: the red;
        /// reel: runs; hook: slack and jumps, twice on toothy fish). Never below 0.
        /// </summary>
        public void Wear(Player player, AnglingFight fight, AnglingSpeciesDef species)
        {
            PlayerTackle rig = Of(player);
            foreach (var pair in rig.Slots.ToList())
            {
                AnglingTackleDef? t = World.Content.Item(pair.Value)?.Tackle;
                if (t == null) continue;
                double hard = pair.Key switch
                {
                    AnglingSlot.Line => fight.RedSteps,
                    AnglingSlot.Reel => fight.RunSteps,
                    AnglingSlot.Hook => fight.SlackSteps + fight.JumpCount + fight.LatchSteps / 2,
                    _ => 0,
                };
                double wear = t.WearPerFight + t.WearPerHard * hard / 100.0;
                if (pair.Key == AnglingSlot.Hook && species.Teeth && !t.NoTeethWear) wear *= 2;
                rig.Condition[pair.Value] = Math.Max(0, Math.Round(rig.ConditionOf(pair.Value) - wear, 2));
            }
        }

        /// <summary>Mends the rig at a tackle bench, from materials, each part the player has the skill and materials for.</summary>
        public void MendYourself(Player player)
        {
            PlayerTackle rig = Of(player);
            ItemSystem items = World.Get<ItemSystem>();
            SkillSystem? skills = World.Find<SkillSystem>();
            int mended = 0;
            var problems = new List<string>();
            foreach (var pair in rig.Slots)
            {
                ItemDef? def = World.Content.Item(pair.Value);
                AnglingTackleDef? t = def?.Tackle;
                if (def == null || t == null || rig.ConditionOf(pair.Value) >= 100) continue;
                if (t.Collector || t.Repair == null)
                {
                    problems.Add("Only the collector can mend your " + def.Name.ToLowerInvariant() + ".");
                    continue;
                }
                string? problem = skills?.Check(player, t.Repair.Requirements, "mend your " + def.Name.ToLowerInvariant());
                if (problem == null && t.Repair.Materials.Any(m => items.CountOf(player, m.Item) < m.Count))
                    problem = "You need " + string.Join(" and ", t.Repair.Materials.Select(m => m.Count + " " + (World.Content.Item(m.Item)?.Name.ToLowerInvariant() ?? m.Item)))
                              + " to mend your " + def.Name.ToLowerInvariant() + ".";
                if (problem != null)
                {
                    problems.Add(problem);
                    continue;
                }
                foreach (ItemCost m in t.Repair.Materials) items.Take(player, m.Item, m.Count);
                rig.Condition[pair.Value] = 100;
                mended++;
            }
            foreach (string p in problems) World.Tell(player, TextKind.Warning, p);
            if (mended > 0)
            {
                World.Animate(player, "work");
                World.Tell(player, TextKind.Info, mended == 1 ? "You mend a part of your rig." : "You mend " + mended + " parts of your rig.");
            }
            else if (problems.Count == 0) World.Tell(player, TextKind.Info, "Your rig needs no mending.");
        }

        /// <summary>What a tackle-maker (or the collector) charges to mend the rig's worn parts.</summary>
        public int MendFee(Player player, bool collector)
        {
            PlayerTackle rig = Of(player);
            int fee = 0;
            foreach (var pair in rig.Slots)
            {
                ItemDef? def = World.Content.Item(pair.Value);
                if (def?.Tackle == null || def.Tackle.Collector != collector) continue;
                double worn = (100 - rig.ConditionOf(pair.Value)) / 100.0;
                if (worn <= 0) continue;
                int cost = (int)Math.Ceiling(Math.Max(1, def.Value) * worn * Rules.MendPercent / 100.0) * (collector ? Rules.CollectorMendTimes : 1);
                fee += Math.Max(1, cost);
            }
            return fee;
        }

        public void MendForCoins(Player player, bool collector)
        {
            int fee = MendFee(player, collector);
            if (fee == 0)
            {
                World.Tell(player, TextKind.Info, collector ? "None of the collector's parts on your rig need mending." : "Nothing on your rig needs mending that I can do.");
                return;
            }
            ItemSystem items = World.Get<ItemSystem>();
            if (!items.Take(player, Rules.Currency, fee))
            {
                World.Tell(player, TextKind.Warning, "Mending your rig costs " + fee + " " + (World.Content.Item(Rules.Currency)?.Name.ToLowerInvariant() ?? Rules.Currency) + ".");
                return;
            }
            PlayerTackle rig = Of(player);
            foreach (var pair in rig.Slots)
                if (World.Content.Item(pair.Value)?.Tackle is AnglingTackleDef t && t.Collector == collector) rig.Condition[pair.Value] = 100;
            World.Tell(player, TextKind.Info, "Your rig is mended, for " + fee + " " + (World.Content.Item(Rules.Currency)?.Name.ToLowerInvariant() ?? Rules.Currency) + ".");
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            PlayerTackle? saved = save?.Section<PlayerTackle>(SaveSection);
            if (saved == null) return;
            PlayerTackle rig = Of(player);
            rig.Slots = saved.Slots ?? new Dictionary<AnglingSlot, string>();
            rig.Condition = saved.Condition ?? new Dictionary<string, double>();
            // A part whose item no longer exists falls out of the rig.
            foreach (AnglingSlot slot in rig.Slots.Keys.ToList())
                if (World.Content.Item(rig.Slots[slot])?.Tackle == null) rig.Slots.Remove(slot);
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player));

        public override void SendState(Player player)
        {
            PlayerTackle rig = Of(player);
            var state = new AnglingTackleState { Stats = Stats(player) };
            foreach (AnglingSlot slot in Enum.GetValues(typeof(AnglingSlot)))
            {
                string id = rig.Slots.TryGetValue(slot, out string? item) ? item : "";
                state.Slots.Add(new AnglingSlotState { Slot = slot, Item = id, Condition = id.Length > 0 ? rig.ConditionOf(id) : 100 });
            }
            JsonStateMessage message = JsonStateMessage.Of(AnglingTackleState.StateKey, state);
            if (message.Json == rig.LastSent) return;
            rig.LastSent = message.Json;
            player.Connection.Send(message);
        }
    }
}
