using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Server.Items;
using Rth.Server.Persistence;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>A player's rods' rigs: the parts on each kind of rod, and every piece's condition by item kind.</summary>
    public sealed class PlayerTackle
    {
        /// <summary>By rod item id: the part in each of its slots.</summary>
        public Dictionary<string, Dictionary<AnglingSlot, string>> Rigs = new Dictionary<string, Dictionary<AnglingSlot, string>>();
        /// <summary>Condition (0-100) by item id, rods and parts alike; a kind never worn is at 100.</summary>
        public Dictionary<string, double> Condition = new Dictionary<string, double>();
        /// <summary>The rod whose rig window is open ("": none). Not saved.</summary>
        [JsonIgnore] public string Open = "";
        [JsonIgnore] public string LastSent = "";

        public double ConditionOf(string item) => Condition.TryGetValue(item, out double c) ? c : 100;

        public Dictionary<AnglingSlot, string> RigOf(string rod)
        {
            if (!Rigs.TryGetValue(rod, out Dictionary<AnglingSlot, string>? rig)) Rigs[rod] = rig = new Dictionary<AnglingSlot, string>();
            return rig;
        }
    }

    /// <summary>
    /// Rods and their rigs (docs/fishing.md 4). A rod is a tiered item of a kind (fly, spinning,
    /// ledger), wielded in the main hand or carried; its slots take parts that fit it (its kind,
    /// its tier and below). Right-click a rod, "Rig", to open its rig window; parts go in from the
    /// pack (dragged onto a slot, or "Rig" on the part) and come out with a click. Each kind of
    /// rod keeps its own rig. Rods and parts wear from how a fight went and lose their bonus
    /// gradually; they are mended at a tackle bench (object action "mend": materials and the skill)
    /// or by a tackle-maker (NPC action "mend", for coins; "mend-rare" is the collector's, for
    /// collector parts). The fishing rod is the wielded rod, else the best rod in the pack.
    /// Intent "tackle": "close", "put:packSlot", "unrig:Slot". Saved as "tackle"; sent as the
    /// "tackle" state.
    /// </summary>
    public sealed class AnglingTackleSystem : GameSystem
    {
        public const string IntentKind = AnglingTackleState.IntentKind;
        public const string MendAction = "mend";
        public const string MendRareAction = "mend-rare";
        private const string SaveSection = "tackle";

        public PlayerTackle Of(Player player) => player.Component<PlayerTackle>();

        private AnglingFile Rules => World.Content.Angling;

        protected override void OnAttach()
        {
            World.Get<ItemSystem>().RegisterAction(ItemActions.Rig, (player, use) =>
            {
                if (use.Def.Tackle?.Slot == AnglingSlot.Rod) OpenRig(player, use.Def.Id);
                else Put(player, use.Container, use.Slot);
            });
            World.Get<IntentSystem>().Register(IntentKind, (player, arg) =>
            {
                if (arg == "close") Of(player).Open = "";
                else if (arg.StartsWith("unrig:", StringComparison.Ordinal) && Enum.TryParse(arg.Substring(6), out AnglingSlot slot)) Unrig(player, slot);
                else if (arg.StartsWith("put:", StringComparison.Ordinal) && int.TryParse(arg.Substring(4), out int packSlot))
                    Put(player, World.Get<ItemSystem>().Of(player).Inventory, packSlot);
            });
            World.Get<ObjectSystem>().RegisterAction(MendAction, (player, placed, def) =>
                World.StartInteraction(player, new ObjectOnceInteraction(placed, MendYourself)));
            World.Get<NpcSystem>().RegisterAction(MendAction, (player, npc) => MendForCoins(player, collector: false));
            World.Get<NpcSystem>().RegisterAction(MendRareAction, (player, npc) => MendForCoins(player, collector: true));
        }

        // ------------------------------------------------------------------ which rod

        private static bool IsRod(ItemDef? def) => def?.Tackle?.Slot == AnglingSlot.Rod;

        /// <summary>The rod the player fishes with: the one in hand, else the best in the pack (highest tier); null if none.</summary>
        public ItemDef? Rod(Player player)
        {
            ItemSystem items = World.Get<ItemSystem>();
            ItemDef? held = items.Worn(player, EquipSlot.Weapon);
            if (IsRod(held)) return held;
            ItemContainer pack = items.Of(player).Inventory;
            ItemDef? best = null;
            for (int i = 0; i < pack.Size; i++)
            {
                if (pack.IsEmpty(i)) continue;
                ItemDef? def = World.Content.Item(pack[i].ItemId);
                if (IsRod(def) && (best == null || def!.Tackle!.Tier > best.Tackle!.Tier)) best = def;
            }
            return best;
        }

        /// <summary>Whether the player has this rod in hand or in the pack.</summary>
        private bool Has(Player player, string rod)
        {
            ItemSystem items = World.Get<ItemSystem>();
            return items.Worn(player, EquipSlot.Weapon)?.Id == rod || items.CountOf(player, rod) > 0;
        }

        /// <summary>The part in a slot of the player's fishing rod (the rod itself for <see cref="AnglingSlot.Rod"/>), or null.</summary>
        public ItemDef? Part(Player player, AnglingSlot slot)
        {
            ItemDef? rod = Rod(player);
            if (rod == null) return null;
            if (slot == AnglingSlot.Rod) return rod;
            return Of(player).RigOf(rod.Id).TryGetValue(slot, out string? id) ? World.Content.Item(id) : null;
        }

        /// <summary>A rod's stats from itself and its parts, at their condition (no level bonus: that is the species').</summary>
        public AnglingRigStats Stats(Player player, string? rodId = null)
        {
            PlayerTackle tackle = Of(player);
            ItemDef? rod = rodId != null ? World.Content.Item(rodId) : Rod(player);
            var parts = new List<(AnglingTackleDef, double)>();
            if (rod?.Tackle != null)
            {
                parts.Add((rod.Tackle, tackle.ConditionOf(rod.Id)));
                foreach (var pair in tackle.RigOf(rod.Id))
                    if (World.Content.Item(pair.Value)?.Tackle is AnglingTackleDef t) parts.Add((t, tackle.ConditionOf(pair.Value)));
            }
            return AnglingRules.Rig(Rules, parts);
        }

        // ------------------------------------------------------------------ rigging

        public void OpenRig(Player player, string rod)
        {
            if (IsRod(World.Content.Item(rod))) Of(player).Open = rod;
        }

        /// <summary>The rod a part from the pack goes on: the open rig's, else the fishing rod.</summary>
        private ItemDef? Target(Player player)
        {
            PlayerTackle tackle = Of(player);
            if (tackle.Open.Length > 0 && Has(player, tackle.Open)) return World.Content.Item(tackle.Open);
            return Rod(player);
        }

        /// <summary>Puts a part from a pack slot on the rod; whatever was in its slot goes back to the pack. A rod opens its rig.</summary>
        public void Put(Player player, ItemContainer from, int slot)
        {
            if (!from.InRange(slot) || from.IsEmpty(slot)) return;
            ItemDef? def = World.Content.Item(from[slot].ItemId);
            AnglingTackleDef? part = def?.Tackle;
            if (def == null || part == null) return;
            if (part.Slot == AnglingSlot.Rod)
            {
                OpenRig(player, def.Id);
                return;
            }
            ItemDef? rod = Target(player);
            if (rod?.Tackle == null)
            {
                World.Tell(player, TextKind.Warning, "You need a rod to rig that on.");
                return;
            }
            string? problem = AnglingRules.FitProblem(rod.Tackle, rod.Name.ToLowerInvariant(), part, def.Name.ToLowerInvariant())
                              ?? World.Find<SkillSystem>()?.Check(player, part.Requirements, "rig the " + def.Name.ToLowerInvariant());
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return;
            }
            Dictionary<AnglingSlot, string> rig = Of(player).RigOf(rod.Id);
            rig.TryGetValue(part.Slot, out string? old);
            from.RemoveAt(slot, 1);
            rig[part.Slot] = def.Id;
            if (old != null && World.Get<ItemSystem>().Give(player, old, 1) > 0)
                World.Find<GroundItemSystem>()?.Spawn(old, 1, player.Plane, player.Position, player);
            World.Tell(player, TextKind.Info, "You rig the " + def.Name.ToLowerInvariant() + " on your " + rod.Name.ToLowerInvariant() + ".");
        }

        /// <summary>Takes a part off the open rod (else the fishing rod) and back to the pack.</summary>
        public void Unrig(Player player, AnglingSlot slot)
        {
            ItemDef? rod = Target(player);
            if (rod == null) return;
            Dictionary<AnglingSlot, string> rig = Of(player).RigOf(rod.Id);
            if (!rig.TryGetValue(slot, out string? id)) return;
            ItemSystem items = World.Get<ItemSystem>();
            if (!items.CanGive(player, id, 1))
            {
                World.Tell(player, TextKind.Warning, "You have no room in your pack for it.");
                return;
            }
            rig.Remove(slot);
            items.Give(player, id, 1);
        }

        // ------------------------------------------------------------------ wear and mending

        /// <summary>
        /// Wear from one fight on the rod that fought it: every piece a little, and each by what it
        /// suffered (line: the red; reel: runs; hook and leader: slack and jumps, twice on toothy
        /// fish). Never below 0.
        /// </summary>
        public void Wear(Player player, string rodId, AnglingFight fight, AnglingSpeciesDef species)
        {
            PlayerTackle tackle = Of(player);
            var pieces = tackle.RigOf(rodId).ToList();
            pieces.Add(new KeyValuePair<AnglingSlot, string>(AnglingSlot.Rod, rodId));
            foreach (var pair in pieces)
            {
                AnglingTackleDef? t = World.Content.Item(pair.Value)?.Tackle;
                if (t == null) continue;
                double hard = pair.Key switch
                {
                    AnglingSlot.Line => fight.RedSteps,
                    AnglingSlot.Reel => fight.RunSteps,
                    AnglingSlot.Hook or AnglingSlot.Leader => fight.SlackSteps + fight.JumpCount + fight.LatchSteps / 2,
                    _ => 0,
                };
                double wear = t.WearPerFight + t.WearPerHard * hard / 100.0;
                if ((pair.Key == AnglingSlot.Hook || pair.Key == AnglingSlot.Leader) && species.Teeth && !t.NoTeethWear) wear *= 2;
                tackle.Condition[pair.Value] = Math.Max(0, Math.Round(tackle.ConditionOf(pair.Value) - wear, 2));
            }
        }

        /// <summary>Every rod and part the player could mend now: the fishing rod and its rig, and the open rig's.</summary>
        private IEnumerable<string> Mendable(Player player)
        {
            var seen = new HashSet<string>();
            foreach (ItemDef? rod in new[] { Rod(player), Target(player) })
            {
                if (rod == null) continue;
                if (seen.Add(rod.Id)) yield return rod.Id;
                foreach (string part in Of(player).RigOf(rod.Id).Values)
                    if (seen.Add(part)) yield return part;
            }
        }

        /// <summary>Mends at a tackle bench, from materials, each piece the player has the skill and materials for.</summary>
        public void MendYourself(Player player)
        {
            PlayerTackle tackle = Of(player);
            ItemSystem items = World.Get<ItemSystem>();
            SkillSystem? skills = World.Find<SkillSystem>();
            int mended = 0;
            var problems = new List<string>();
            foreach (string id in Mendable(player).ToList())
            {
                ItemDef? def = World.Content.Item(id);
                AnglingTackleDef? t = def?.Tackle;
                if (def == null || t == null || tackle.ConditionOf(id) >= 100) continue;
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
                tackle.Condition[id] = 100;
                mended++;
            }
            foreach (string p in problems) World.Tell(player, TextKind.Warning, p);
            if (mended > 0)
            {
                World.Animate(player, "work");
                World.Tell(player, TextKind.Info, mended == 1 ? "You mend a piece of your tackle." : "You mend " + mended + " pieces of your tackle.");
            }
            else if (problems.Count == 0) World.Tell(player, TextKind.Info, "Your tackle needs no mending.");
        }

        /// <summary>What a tackle-maker (or the collector) charges to mend the worn pieces.</summary>
        public int MendFee(Player player, bool collector)
        {
            PlayerTackle tackle = Of(player);
            int fee = 0;
            foreach (string id in Mendable(player))
            {
                ItemDef? def = World.Content.Item(id);
                if (def?.Tackle == null || def.Tackle.Collector != collector) continue;
                double worn = (100 - tackle.ConditionOf(id)) / 100.0;
                if (worn <= 0) continue;
                int cost = (int)Math.Ceiling(Math.Max(1, def.Value) * worn * Rules.MendPercent / 100.0) * (collector ? Rules.CollectorMendTimes : 1);
                fee += Math.Max(1, cost);
            }
            return fee;
        }

        public void MendForCoins(Player player, bool collector)
        {
            int fee = MendFee(player, collector);
            string currency = World.Content.Item(Rules.Currency)?.Name.ToLowerInvariant() ?? Rules.Currency;
            if (fee == 0)
            {
                World.Tell(player, TextKind.Info, collector ? "None of the collector's pieces on your rod need mending." : "Nothing on your rod needs mending that I can do.");
                return;
            }
            if (!World.Get<ItemSystem>().Take(player, Rules.Currency, fee))
            {
                World.Tell(player, TextKind.Warning, "Mending your tackle costs " + fee + " " + currency + ".");
                return;
            }
            PlayerTackle tackle = Of(player);
            foreach (string id in Mendable(player).ToList())
                if (World.Content.Item(id)?.Tackle is AnglingTackleDef t && t.Collector == collector) tackle.Condition[id] = 100;
            World.Tell(player, TextKind.Info, "Your tackle is mended, for " + fee + " " + currency + ".");
        }

        // ------------------------------------------------------------------ saving and the client

        private sealed class SavedTackle
        {
            public Dictionary<string, Dictionary<AnglingSlot, string>>? Rigs;
            public Dictionary<string, double>? Condition;
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            SavedTackle? saved = save?.Section<SavedTackle>(SaveSection);
            if (saved == null) return;
            PlayerTackle tackle = Of(player);
            tackle.Rigs = saved.Rigs ?? new Dictionary<string, Dictionary<AnglingSlot, string>>();
            tackle.Condition = saved.Condition ?? new Dictionary<string, double>();
            // A rod or part that no longer exists falls out.
            foreach (string rod in tackle.Rigs.Keys.ToList())
            {
                if (!IsRod(World.Content.Item(rod)))
                {
                    tackle.Rigs.Remove(rod);
                    continue;
                }
                Dictionary<AnglingSlot, string> rig = tackle.Rigs[rod];
                foreach (AnglingSlot slot in rig.Keys.ToList())
                    if (World.Content.Item(rig[slot])?.Tackle == null) rig.Remove(slot);
            }
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            PlayerTackle tackle = Of(player);
            save.SetSection(SaveSection, new SavedTackle
            {
                Rigs = tackle.Rigs.Where(r => r.Value.Count > 0).ToDictionary(r => r.Key, r => r.Value),
                Condition = tackle.Condition,
            });
        }

        public override void SendState(Player player)
        {
            PlayerTackle tackle = Of(player);
            if (tackle.Open.Length > 0 && !Has(player, tackle.Open)) tackle.Open = "";     // banked or dropped: the window closes
            var state = new AnglingTackleState { Open = tackle.Open };
            ItemDef? rod = tackle.Open.Length > 0 ? World.Content.Item(tackle.Open) : Rod(player);
            if (rod?.Tackle != null)
            {
                state.Stats = Stats(player, rod.Id);
                state.RodCondition = tackle.ConditionOf(rod.Id);
                Dictionary<AnglingSlot, string> rig = tackle.RigOf(rod.Id);
                foreach (AnglingSlot slot in rod.Tackle.RodSlots)
                {
                    string id = rig.TryGetValue(slot, out string? item) ? item : "";
                    state.Slots.Add(new AnglingSlotState { Slot = slot, Item = id, Condition = id.Length > 0 ? tackle.ConditionOf(id) : 100 });
                }
            }
            JsonStateMessage message = JsonStateMessage.Of(AnglingTackleState.StateKey, state);
            if (message.Json == tackle.LastSent) return;
            tackle.LastSent = message.Json;
            player.Connection.Send(message);
        }
    }
}
